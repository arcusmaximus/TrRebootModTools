from typing import TYPE_CHECKING
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BlenderOperatorBase import BlenderOperatorBase
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class PinClothBonesOperator(BlenderOperatorBase[BlenderPropertyGroup]):
    bl_idname = "tr_reboot.mark_cloth_bones_fixed"
    bl_label = "Pin"
    bl_description = "Disable physics for the selected bones, making them stick to the body"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        return context is not None and \
               context.object is not None and \
               context.object.mode == "POSE" and \
               Enumerable(context.object.children).any(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name)) and \
               Enumerable(context.selected_pose_bones).any(PinClothBonesOperator.is_cloth_bone)

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None or context.object is None:
            return { "CANCELLED" }

        for bl_bone in Enumerable(context.selected_pose_bones).where(PinClothBonesOperator.is_cloth_bone):
            BlenderHelper.remove_bone_from_group(bl_bone.bone, BlenderNaming.unpinned_cloth_bone_group_name)
            BlenderHelper.add_bone_to_group(
                context.object,
                bl_bone.bone,
                BlenderNaming.pinned_cloth_bone_group_name,
                BlenderNaming.pinned_cloth_bone_palette_name
            )

        return { "FINISHED" }

    @staticmethod
    def is_cloth_bone(bl_pose_bone: bpy.types.PoseBone) -> bool:
        bone_id_set = BlenderNaming.try_parse_bone_name(bl_pose_bone.name)
        return bone_id_set is not None and bone_id_set.global_id is None
