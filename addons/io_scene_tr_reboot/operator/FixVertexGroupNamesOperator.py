from typing import TYPE_CHECKING, cast
import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BlenderOperatorBase import BlenderOperatorBase
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class FixVertexGroupNamesOperator(BlenderOperatorBase[BlenderPropertyGroup]):
    bl_idname = "tr_reboot.fix_vertex_group_names"
    bl_label = "Fix Vertex Group Names"
    bl_description = "Add/update/remove local bone IDs in the vertex group names to match the bones of the parent armature"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None:
            return False

        bl_obj = context.object
        return bl_obj is not None and \
               isinstance(bl_obj.data, bpy.types.Mesh) and \
               BlenderNaming.try_parse_mesh_name(bl_obj.name) is not None and \
               bl_obj.parent is not None and \
               isinstance(bl_obj.parent.data, bpy.types.Armature) and \
               (BlenderNaming.try_parse_global_armature_name(bl_obj.parent.name) is not None or \
                BlenderNaming.try_parse_local_armature_name(bl_obj.parent.name) is not None)

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        bl_mesh_obj = context and context.object
        if bl_mesh_obj is None:
            return { "CANCELLED" }

        bl_armature_obj = bl_mesh_obj.parent
        if bl_armature_obj is None:
            return { "CANCELLED" }

        if BlenderNaming.try_parse_global_armature_name(bl_armature_obj.name) is not None:
            self.rename_for_global_armature(bl_mesh_obj)
        else:
            self.rename_for_local_armature(bl_mesh_obj, bl_armature_obj)

        return { "FINISHED" }

    def rename_for_global_armature(self, bl_mesh_obj: bpy.types.Object) -> None:
        for old_vertex_group_name in Enumerable(bl_mesh_obj.vertex_groups).select(lambda g: g.name).to_list():
            old_vertex_group_id_set = BlenderNaming.try_parse_bone_name(old_vertex_group_name)
            if old_vertex_group_id_set is None or old_vertex_group_id_set.global_id is None or old_vertex_group_id_set.local_id is None:
                continue

            new_vertex_group_name = BlenderNaming.make_bone_name(None, old_vertex_group_id_set.global_id, None)
            bl_mesh_obj.vertex_groups[old_vertex_group_name].name = new_vertex_group_name

    def rename_for_local_armature(self, bl_mesh_obj: bpy.types.Object, bl_armature_obj: bpy.types.Object) -> None:
        bl_amature = cast(bpy.types.Armature, bl_armature_obj.data)
        local_ids_by_global_id: dict[int, int] = {}
        for bl_bone in bl_amature.bones:
            bone_id_set = BlenderNaming.try_parse_bone_name(bl_bone.name)
            if bone_id_set is None or bone_id_set.global_id is None or bone_id_set.local_id is None:
                continue

            local_ids_by_global_id[bone_id_set.global_id] = bone_id_set.local_id

        for old_vertex_group_name in Enumerable(bl_mesh_obj.vertex_groups).select(lambda g: g.name).to_list():
            old_vertex_group_id_set = BlenderNaming.try_parse_bone_name(old_vertex_group_name)
            if old_vertex_group_id_set is None or old_vertex_group_id_set.global_id is None:
                continue

            new_local_id = local_ids_by_global_id.get(old_vertex_group_id_set.global_id)
            if new_local_id is None or new_local_id == old_vertex_group_id_set.local_id:
                continue

            new_vertex_group_name = BlenderNaming.make_bone_name(None, old_vertex_group_id_set.global_id, new_local_id)
            bl_mesh_obj.vertex_groups[old_vertex_group_name].name = new_vertex_group_name
