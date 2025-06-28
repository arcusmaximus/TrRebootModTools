from typing import TYPE_CHECKING, cast
import bpy
from mathutils import Vector
from io_scene_tr_reboot.BlenderHelper import BlenderBoneGroup, BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BlenderOperatorBase import BlenderOperatorBase
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy._typing.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class RegenerateClothBonesOperator(BlenderOperatorBase[BlenderPropertyGroup]):
    bl_idname = "tr_reboot.regen_cloth_bones"
    bl_label = "Regenerate"
    bl_description = "Create/move/delete cloth bones so they match the vertices in the cloth meshes"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        return context is not None and RegenerateClothBonesOperator.get_selected_armature_obj(context) is not None

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        with OperatorContext.begin(self):
            bl_armature_obj = RegenerateClothBonesOperator.get_selected_armature_obj(context)
            if bl_armature_obj is None:
                return { "FINISHED" }

            BlenderHelper.reset_pose(bl_armature_obj)

            bl_cloth_objs = self.get_cloth_strip_objs_by_skeleton_id(bl_armature_obj)
            self.regenerate(context, bl_armature_obj, bl_cloth_objs)
            return { "FINISHED" }

    @staticmethod
    def get_selected_armature_obj(context: bpy.types.Context) -> bpy.types.Object | None:
        bl_armature_obj = context.object
        while bl_armature_obj is not None:
            if isinstance(bl_armature_obj.data, bpy.types.Armature) and \
               Enumerable(bl_armature_obj.children).any(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name)):
                return bl_armature_obj

            bl_armature_obj = bl_armature_obj.parent

        return None

    @staticmethod
    def get_local_armature_objs(context: bpy.types.Context) -> dict[int, bpy.types.Object]:
        def is_in_local_collection(bl_obj: bpy.types.Object) -> bool:
            return Enumerable(bl_obj.users_collection).any(lambda c: c.name == BlenderNaming.local_collection_name)

        if context.scene is None:
            return {}

        return Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature) and is_in_local_collection(o)) \
                                                .to_dict(lambda o: BlenderNaming.parse_local_armature_name(o.name))

    @staticmethod
    def get_cloth_strip_objs_by_skeleton_id(bl_armature_obj: bpy.types.Object) -> dict[int, list[bpy.types.Object]]:
        bl_empty = Enumerable(bl_armature_obj.children).first_or_none(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name))
        if bl_empty is None:
            return {}

        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bl_cloth_strip_objs = Enumerable(bl_empty.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)) \
                                                           .group_by(lambda o: BlenderNaming.parse_cloth_strip_name(o.name).skeleton_id)
        for bl_cloth_strip_obj in Enumerable(bl_cloth_strip_objs.values()).select_many(lambda l: l):
            cloth_strip_properties = ObjectProperties.get_instance(bl_cloth_strip_obj).cloth
            if not cloth_strip_properties.parent_bone_name:
                raise Exception(f"Cloth strip {bl_cloth_strip_obj.name} has no parent bone. Please select a valid bone in the sidebar or delete the cloth strip.")

            bl_parent_bone = bl_armature.bones.get(cloth_strip_properties.parent_bone_name)
            if bl_parent_bone is None:
                raise Exception(f"Cloth strip {bl_cloth_strip_obj.name} has parent bone [{cloth_strip_properties.parent_bone_name}] which does not exist. Please select a valid bone in the sidebar.")

        return bl_cloth_strip_objs

    def regenerate(self, context: bpy.types.Context, bl_armature_obj: bpy.types.Object, bl_cloth_strip_objs_by_skeleton_id: dict[int, list[bpy.types.Object]]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bl_mesh_objs = Enumerable(bl_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)).to_list()

        bl_local_armature_objs = self.get_local_armature_objs(context)

        for bl_cloth_strip_obj in Enumerable(bl_cloth_strip_objs_by_skeleton_id.values()).select_many(lambda l: l):
            BlenderHelper.select_object(bl_cloth_strip_obj)
            bpy.ops.object.transform_apply()

        bone_groups: dict[str, BlenderBoneGroup]
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            self.unassign_duplicate_vertex_groups(bl_cloth_strip_objs_by_skeleton_id)
            self.clean_unused(bl_armature_obj, bl_cloth_strip_objs_by_skeleton_id, bl_mesh_objs)
            self.close_name_gaps(bl_armature_obj, bl_local_armature_objs)
            bone_groups = self.move_existing_and_add_missing(bl_armature_obj, bl_cloth_strip_objs_by_skeleton_id)

        for bone_name, bone_group in bone_groups.items():
            BlenderHelper.move_bone_to_group(bl_armature_obj, bl_armature.bones[bone_name], bone_group.name, bone_group.palette)

    def unassign_duplicate_vertex_groups(self, bl_cloth_strip_objs_by_skeleton_id: dict[int, list[bpy.types.Object]]) -> None:
        used_vertex_groups_names: set[str] = set()

        for bl_cloth_strip_obj in Enumerable(bl_cloth_strip_objs_by_skeleton_id.values()).select_many(lambda l: l):
            for bl_vertex in cast(bpy.types.Mesh, bl_cloth_strip_obj.data).vertices:
                while len(bl_vertex.groups) > 1:
                    bl_cloth_strip_obj.vertex_groups[bl_vertex.groups[1].group].remove([bl_vertex.index])

                if len(bl_vertex.groups) == 0:
                    continue

                bl_vertex_group = bl_cloth_strip_obj.vertex_groups[bl_vertex.groups[0].group]
                if bl_vertex_group.name in used_vertex_groups_names:
                    bl_vertex_group.remove([bl_vertex.index])
                else:
                    used_vertex_groups_names.add(bl_vertex_group.name)

    def clean_unused(self, bl_armature_obj: bpy.types.Object, bl_cloth_objs_by_skeleton_id: dict[int, list[bpy.types.Object]], bl_mesh_objs: list[bpy.types.Object]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        available_cloth_bone_names: set[str] = Enumerable(bl_armature.edit_bones).select(lambda b: b.name) \
                                                                                 .where(lambda n: BlenderNaming.parse_bone_name(n).global_id is None) \
                                                                                 .to_set()

        used_vertex_group_names: set[str] = set()
        for bl_cloth_obj in Enumerable(bl_cloth_objs_by_skeleton_id.values()).select_many(lambda l: l):
            clothstrip_used_vertex_group_names: set[str] = set()
            for bl_vertex in cast(bpy.types.Mesh, bl_cloth_obj.data).vertices:
                for bl_weight in bl_vertex.groups:
                    clothstrip_used_vertex_group_names.add(bl_cloth_obj.vertex_groups[bl_weight.group].name)

            for vertex_group_name in Enumerable(bl_cloth_obj.vertex_groups).select(lambda g: g.name).to_set().difference(clothstrip_used_vertex_group_names):
                bl_cloth_obj.vertex_groups.remove(bl_cloth_obj.vertex_groups[vertex_group_name])

            used_vertex_group_names.update(clothstrip_used_vertex_group_names)

        for bone_name in available_cloth_bone_names.difference(used_vertex_group_names):
            bl_armature.edit_bones.remove(bl_armature.edit_bones[bone_name])
            available_cloth_bone_names.remove(bone_name)

        for bl_cloth_obj in Enumerable(bl_cloth_objs_by_skeleton_id.values()).select_many(lambda l: l):
            for vertex_group_name in Enumerable(bl_cloth_obj.vertex_groups).select(lambda g: g.name).to_set().difference(available_cloth_bone_names):
                bl_cloth_obj.vertex_groups.remove(bl_cloth_obj.vertex_groups[vertex_group_name])

        available_bone_names: list[str] = Enumerable(bl_armature.edit_bones).select(lambda b: b.name).to_list()
        for bl_mesh_obj in bl_mesh_objs:
            for vertex_group_name in Enumerable(bl_mesh_obj.vertex_groups).select(lambda g: g.name).to_set().difference(available_bone_names):
                bl_mesh_obj.vertex_groups.remove(bl_mesh_obj.vertex_groups[vertex_group_name])

    def close_name_gaps(self, bl_armature_obj: bpy.types.Object, bl_local_armature_objs: dict[int, bpy.types.Object]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_id_sets_by_skeleton_id = Enumerable(bl_armature.edit_bones).select(lambda b: BlenderNaming.parse_bone_name(b.name)) \
                                                                        .group_by(lambda ids: ids.skeleton_id)

        old_to_new_bone_name_mapping: dict[str, str] = {}
        for skeleton_id, bone_id_sets in bone_id_sets_by_skeleton_id.items():
            if skeleton_id is None:
                next_new_local_bone_id = Enumerable(bone_id_sets).count(lambda ids: ids.global_id is not None)
            else:
                bl_local_armature_obj = bl_local_armature_objs.get(skeleton_id)
                if bl_local_armature_obj is None:
                    raise Exception(f"No armature found in collection {BlenderNaming.local_collection_name} for skeleton {skeleton_id}")

                next_new_local_bone_id = Enumerable(cast(bpy.types.Armature, bl_local_armature_obj.data).bones).count(lambda b: BlenderNaming.parse_bone_name(b.name).global_id is not None)

            for bone_id_set in Enumerable(bone_id_sets).where(lambda ids: ids.global_id is None and ids.local_id is not None) \
                                                       .order_by(lambda ids: ids.local_id):
                if bone_id_set.local_id != next_new_local_bone_id:
                    old_bone_name = BlenderNaming.make_bone_name(bone_id_set.skeleton_id, None, bone_id_set.local_id)
                    new_bone_name = BlenderNaming.make_bone_name(bone_id_set.skeleton_id, None, next_new_local_bone_id)
                    old_to_new_bone_name_mapping[old_bone_name] = new_bone_name

                next_new_local_bone_id += 1

        for old_bone_name, new_bone_name in old_to_new_bone_name_mapping.items():
            bl_armature.edit_bones[old_bone_name].name = new_bone_name

    def move_existing_and_add_missing(self, bl_armature_obj: bpy.types.Object, bl_cloth_strip_objs_by_skeleton_id: dict[int, list[bpy.types.Object]]) -> dict[str, BlenderBoneGroup]:
        is_global_armature = BlenderNaming.try_parse_global_armature_name(bl_armature_obj.name) is not None
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_id_sets_by_skeleton_id = Enumerable(bl_armature.edit_bones).select(lambda b: BlenderNaming.parse_bone_name(b.name)) \
                                                                        .group_by(lambda ids: ids.skeleton_id)
        next_local_bone_ids_by_skeleton_id = Enumerable(bone_id_sets_by_skeleton_id.items()).to_dict(lambda p: p[0], lambda p: len(p[1]))

        bone_groups: dict[str, BlenderBoneGroup] = {}
        for skeleton_id, bl_cloth_strip_objs in bl_cloth_strip_objs_by_skeleton_id.items():
            if not is_global_armature:
                skeleton_id = None

            for bl_cloth_strip_obj in bl_cloth_strip_objs:
                cloth_strip_properties = ObjectProperties.get_instance(bl_cloth_strip_obj).cloth
                bl_parent_bone = bl_armature.edit_bones.get(cloth_strip_properties.parent_bone_name)

                for bl_vertex in cast(bpy.types.Mesh, bl_cloth_strip_obj.data).vertices:
                    bl_edit_bone: bpy.types.EditBone | None = None

                    if len(bl_vertex.groups) > 0:
                        bl_edit_bone = bl_armature.edit_bones.get(bl_cloth_strip_obj.vertex_groups[bl_vertex.groups[0].group].name)

                    if bl_edit_bone is None:
                        next_local_bone_id = next_local_bone_ids_by_skeleton_id[skeleton_id]
                        bone_name = BlenderNaming.make_bone_name(skeleton_id, None, next_local_bone_id)
                        bl_edit_bone = bl_armature.edit_bones.new(bone_name)
                        bone_groups[bone_name] = BlenderBoneGroup(BlenderNaming.unpinned_cloth_bone_group_name, BlenderNaming.unpinned_cloth_bone_palette_name)

                        bl_vertex_group = bl_cloth_strip_obj.vertex_groups.new(name = bone_name)
                        bl_vertex_group.add([bl_vertex.index], 1.0, "REPLACE")

                        next_local_bone_ids_by_skeleton_id[skeleton_id] = next_local_bone_id + 1

                    bl_edit_bone.parent = bl_parent_bone
                    bl_edit_bone.head = bl_vertex.co
                    bl_edit_bone.tail = bl_edit_bone.head + Vector((0, 0, 0.01))

        return bone_groups
