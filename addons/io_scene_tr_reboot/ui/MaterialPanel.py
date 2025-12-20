import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.MaterialProperties import MaterialProperties

class MaterialPanel(bpy.types.Panel):
    bl_idname = "TR_PT_MaterialPanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "material"
    bl_label = "Tomb Raider Properties"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        return context is not None and context.material is not None and \
               BlenderNaming.try_parse_material_name(context.material.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or context.material is None or self.layout is None:
            return

        props = MaterialProperties.get_instance(context.material)

        bl_row = self.layout.row()
        bl_row.prop(props, "double_sided")
