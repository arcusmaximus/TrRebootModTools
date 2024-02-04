import bpy
from io_scene_sottr.BlenderHelper import BlenderHelper
from io_scene_sottr.BlenderNaming import BlenderNaming
from io_scene_sottr.operator.BlenderOperatorBase import BlenderOperatorBase
from io_scene_sottr.properties.BlenderPropertyGroup import BlenderPropertyGroup
from io_scene_sottr.util.Enumerable import Enumerable

class UnpinClothBonesOperator(BlenderOperatorBase[BlenderPropertyGroup]):
    bl_idname = "tr11.mark_cloth_bones_free"
    bl_label = "Unpin"
    bl_description = "Enable physics for the selected bones, making them free to flutter around"

    @classmethod
    def poll(cls, context: bpy.types.Context) -> bool:
        return context.object and \
               context.object.mode == "POSE" and \
               Enumerable(context.object.children).any(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name)) and \
               Enumerable(context.selected_pose_bones).any(UnpinClothBonesOperator.is_cloth_bone)
    
    def execute(self, context: bpy.types.Context) -> set[str]:
        for bl_bone in Enumerable(context.selected_pose_bones).where(UnpinClothBonesOperator.is_cloth_bone):
            BlenderHelper.move_bone_to_group(context.object, bl_bone.bone, None, None)
        
        return { "FINISHED" }

    @staticmethod
    def is_cloth_bone(bl_pose_bone: bpy.types.PoseBone) -> bool:
        bone_id_set = BlenderNaming.try_parse_bone_name(bl_pose_bone.name)
        return bone_id_set is not None and bone_id_set.global_id is None
    