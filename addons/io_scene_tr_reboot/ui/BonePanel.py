import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties

class BonePanel(bpy.types.Panel):
    bl_idname = "TR_PT_BonePanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "bone"
    bl_label = "TR Bone Properties"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None:
            return False

        bl_bone = context.bone
        if not bl_bone:
            return False

        return BlenderNaming.try_parse_bone_name(bl_bone.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or self.layout is None:
            return

        bl_bone = context.bone
        if bl_bone is None:
            return

        props = BoneProperties.get_instance(bl_bone)
        self.layout.prop(props, "counterpart_bone_name", icon = "BONE_DATA")
