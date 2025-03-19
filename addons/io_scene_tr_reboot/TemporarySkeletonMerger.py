from typing import cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.SkeletonMerger import SkeletonMerger
from io_scene_tr_reboot.util.Enumerable import Enumerable

class TemporaryModelMerger(SkeletonMerger):
    bl_local_collection: bpy.types.Collection

    def __init__(self) -> None:
        bl_local_collection = bpy.data.collections.get(BlenderNaming.local_collection_name)
        if bl_local_collection is None:
            bl_local_collection = bpy.data.collections.new(BlenderNaming.local_collection_name)
            if bpy.context.scene is not None and bpy.context.view_layer is not None:
                bpy.context.scene.collection.children.link(bl_local_collection)
                bpy.context.view_layer.layer_collection.children[bl_local_collection.name].exclude = True

        self.bl_local_collection = bl_local_collection

    def add(self, bl_global_armature_obj: bpy.types.Object | None, bl_local_armature_obj: bpy.types.Object) -> bpy.types.Object:
        if bpy.context.object is not None:
            bpy.ops.object.mode_set(mode = "OBJECT")

        bl_global_armature_obj, bone_renames = self.add_local_armature_to_global(bl_global_armature_obj, bl_local_armature_obj)
        self.apply_bone_renames_to_vertex_groups(bl_local_armature_obj, bone_renames)
        self.apply_bone_renames_to_cloth_strips(bl_local_armature_obj, bone_renames)

        model_id_sets = Enumerable(bl_local_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh))          \
                                                                  .select(lambda o: BlenderNaming.parse_model_name(o.name))     \
                                                                  .distinct()                                                   \
                                                                  .to_list()
        self.move_armature_children(bl_local_armature_obj, bl_global_armature_obj)

        BlenderHelper.move_object_to_collection(bl_local_armature_obj, self.bl_local_collection)
        for model_id_set in model_id_sets:
            bl_local_empty = BlenderHelper.create_object(None, BlenderNaming.make_local_empty_name(model_id_set.object_id, model_id_set.model_id, model_id_set.model_data_id))
            bl_local_empty.parent = bl_local_armature_obj
            BlenderHelper.move_object_to_collection(bl_local_empty, self.bl_local_collection)

        return bl_global_armature_obj

    def add_local_armature_to_global(self, bl_global_armature_obj: bpy.types.Object | None, bl_local_armature_obj: bpy.types.Object) -> tuple[bpy.types.Object, dict[str, str]]:
        local_skeleton_id = BlenderNaming.parse_local_armature_name(bl_local_armature_obj.name)
        global_bone_parent_ids = self.get_global_bone_parents(bl_local_armature_obj)
        global_visible_bone_ids = self.get_visible_global_bones(bl_local_armature_obj)
        global_bone_groups = Enumerable(self.get_bone_groups(bl_local_armature_obj).items()) \
                                .to_dict(lambda p: self.convert_local_bone_name_to_global(local_skeleton_id, p[0]), lambda p: p[1])

        bl_copied_local_armature = cast(bpy.types.Armature, bl_local_armature_obj.data).copy()
        bl_copied_local_armature_obj = BlenderHelper.create_object(bl_copied_local_armature)
        bl_copied_local_armature_obj.show_in_front = True

        bone_renames = self.convert_local_armature_to_global(bl_copied_local_armature_obj, bl_global_armature_obj)

        global_armature_name: str
        if bl_global_armature_obj is None:
            bl_global_armature_obj = bl_copied_local_armature_obj
            global_armature_name = BlenderNaming.make_global_armature_name([local_skeleton_id])
        else:
            global_armature_name = BlenderNaming.make_global_armature_name(
                Enumerable(BlenderNaming.parse_global_armature_name(bl_global_armature_obj.name)).concat([local_skeleton_id]))

            existing_global_bone_groups = self.get_bone_groups(bl_global_armature_obj)
            BlenderHelper.join_objects(bl_global_armature_obj, [bl_copied_local_armature_obj])
            self.apply_global_bone_parents(bl_global_armature_obj, global_bone_parent_ids)
            self.apply_visible_global_bones(bl_global_armature_obj, global_visible_bone_ids)
            self.apply_bone_groups(bl_global_armature_obj, existing_global_bone_groups)

        bl_global_armature_obj.name = global_armature_name
        cast(bpy.types.Armature, bl_global_armature_obj.data).name = global_armature_name
        self.apply_bone_groups(bl_global_armature_obj, global_bone_groups)
        return (bl_global_armature_obj, bone_renames)

    def convert_local_armature_to_global(self, bl_local_armature_obj: bpy.types.Object, bl_existing_global_armature_obj: bpy.types.Object | None) -> dict[str, str]:
        bl_existing_global_armature = bl_existing_global_armature_obj is not None and cast(bpy.types.Armature, bl_existing_global_armature_obj.data) or None
        bl_local_armature = cast(bpy.types.Armature, bl_local_armature_obj.data)
        local_skeleton_id = BlenderNaming.parse_local_armature_name(bl_local_armature_obj.name)

        bone_renames: dict[str, str] = {}
        with BlenderHelper.enter_edit_mode():
            for local_bone_name in Enumerable(bl_local_armature.edit_bones).select(lambda b: b.name).to_list():
                global_bone_name = self.convert_local_bone_name_to_global(local_skeleton_id, local_bone_name)
                if bl_existing_global_armature is None or bl_existing_global_armature.bones.get(global_bone_name) is None:
                    bone_renames[local_bone_name] = global_bone_name
                else:
                    bl_local_armature.edit_bones.remove(bl_local_armature.edit_bones[local_bone_name])

        self.apply_bone_renames_to_armature(bl_local_armature_obj, bone_renames)
        return bone_renames

    def convert_local_bone_name_to_global(self, local_skeleton_id: int, local_bone_name: str) -> str:
        local_bone_ids = BlenderNaming.parse_bone_name(local_bone_name)
        if local_bone_ids.global_id is None:
            return BlenderNaming.make_bone_name(local_skeleton_id, local_bone_ids.global_id, local_bone_ids.local_id)
        else:
            return BlenderNaming.make_bone_name(None, local_bone_ids.global_id, None)
