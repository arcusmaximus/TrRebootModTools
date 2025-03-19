import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Factories import Factories

class ClothStripPanel(bpy.types.Panel):
    bl_idname = "TR_PT_ClothStripPanel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Tomb Raider"
    bl_label = "Cloth Strip"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None:
            return False

        bl_obj = context.active_object
        return bl_obj is not None and BlenderNaming.try_parse_cloth_strip_name(bl_obj.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or self.layout is None:
            return

        bl_obj = context.active_object
        if bl_obj is None:
            return

        props = ObjectProperties.get_instance(bl_obj).cloth
        cloth_class = Factories.get(SceneProperties.get_game()).cloth_class
        self.layout.prop(props, "parent_bone_name", icon = "BONE_DATA")
        self.layout.prop(props, "gravity_factor")

        if cloth_class.supports.strip_buoyancy_factor:
            self.layout.prop(props, "buoyancy_factor")

        self.layout.prop(props, "wind_factor")

        if cloth_class.supports.strip_pose_follow_factor:
            self.layout.prop(props, "stiffness")

        self.layout.prop(props, "rigidity")

        if not cloth_class.supports.mass_specific_bounceback_factor:
            self.layout.prop(props, "bounceback_factor")

        self.layout.prop(props, "dampening")

        (advanced_header, advanced_content) = self.layout.panel("TR_PT_ClothStripPanel_Advanced", default_closed = True)
        advanced_header.label(text = "Advanced")
        if advanced_content:
            advanced_content.prop(props, "transform_type")
            advanced_content.prop(props, "max_velocity_iterations")
            advanced_content.prop(props, "max_position_iterations")
            advanced_content.prop(props, "relaxation_iterations")
            advanced_content.prop(props, "sub_step_count")
            advanced_content.prop(props, "fixed_to_free_slop")
            advanced_content.prop(props, "free_to_free_slop")
            if cloth_class.supports.strip_free_to_free_slop_z:
                advanced_content.prop(props, "free_to_free_slop_z")

            advanced_content.prop(props, "mass_scale")
            advanced_content.prop(props, "time_delta_scale")
            if cloth_class.supports.strip_blend_to_bind_time:
                advanced_content.prop(props, "blend_to_bind_time")
