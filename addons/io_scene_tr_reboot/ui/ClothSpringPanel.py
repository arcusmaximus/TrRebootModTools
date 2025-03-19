import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.properties.ToolSettingProperties import ToolSettingProperties
from io_scene_tr_reboot.tr.Factories import Factories

class ClothSpringPanel(bpy.types.Panel):
    bl_idname = "TR_PT_ClothSpringPanel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Tomb Raider"
    bl_label = "Cloth Springs"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        cloth_class = Factories.get(SceneProperties.get_game()).cloth_class
        if not cloth_class.supports.spring_specific_stretchiness:
            return False

        if context is None or context.edit_object is None:
            return False

        return BlenderNaming.try_parse_cloth_strip_name(context.edit_object.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or context.scene is None or self.layout is None:
            return

        properties = ToolSettingProperties.get_instance(context.scene)
        self.layout.prop(properties, "cloth_spring_stretchiness")
