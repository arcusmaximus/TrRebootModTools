import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties

class CollisionModelPanel(bpy.types.Panel):
    bl_idname = "TR_PT_CollisionModelPanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "data"
    bl_label = "Tomb Raider Properties"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None:
            return False

        bl_obj = context.object
        return bl_obj is not None and isinstance(bl_obj.data, bpy.types.Mesh) and BlenderNaming.try_parse_collision_mesh_name(bl_obj.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or self.layout is None:
            return

        bl_obj = context.object
        if bl_obj is None:
            return

        props = ObjectProperties.get_instance(bl_obj)
        self.layout.prop(props.collision_model, "collision_type_id")
