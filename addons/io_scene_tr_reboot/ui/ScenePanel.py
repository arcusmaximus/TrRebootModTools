import bpy
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties

class ScenePanel(bpy.types.Panel):
    bl_idname = "TR_PT_ScenePanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "scene"
    bl_label = "Tomb Raider Properties"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        return context is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or context.scene is None or self.layout is None:
            return

        props = SceneProperties.get_instance(context.scene)
        self.layout.prop(props, "game")
