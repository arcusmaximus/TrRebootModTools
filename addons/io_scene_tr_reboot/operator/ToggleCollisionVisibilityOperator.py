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

class ToggleCollisionVisibilityOperator(BlenderOperatorBase[BlenderPropertyGroup]):
    bl_idname = "tr_reboot.toggle_collision_visibility"
    bl_label = "Toggle Collision Visibility"
    bl_description = "Toggle between showing rendered geometry and collisions"

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None or context.scene is None:
            return { "CANCELLED" }

        bl_first_collision_obj = Enumerable(context.scene.objects).first_or_none(self.is_collision_obj)
        if bl_first_collision_obj is None:
            return { "CANCELLED" }

        show_collisions = bl_first_collision_obj.hide_get()
        BlenderHelper.switch_to_object_mode()

        for bl_obj in context.scene.objects:
            if isinstance(bl_obj.data, bpy.types.Armature):
                if BlenderNaming.try_parse_global_armature_name(bl_obj.name) is not None or \
                   BlenderNaming.try_parse_local_armature_name(bl_obj.name) is not None:
                    bl_obj.hide_set(show_collisions)
            elif isinstance(bl_obj.data, bpy.types.Mesh):
                if BlenderNaming.try_parse_mesh_name(bl_obj.name) is not None:
                    bl_obj.display_type = "WIRE" if show_collisions else "TEXTURED"
                    bl_obj.hide_select = show_collisions
                elif self.is_collision_obj(bl_obj):
                    bl_obj.hide_set(not show_collisions)
                elif BlenderNaming.try_parse_cloth_strip_name(bl_obj.name) is not None:
                    bl_obj.hide_set(show_collisions)

        return { "FINISHED" }

    def is_collision_obj(self, bl_obj: bpy.types.Object) -> bool:
        return isinstance(bl_obj.data, bpy.types.Mesh) and (
               BlenderNaming.try_parse_collision_mesh_name(bl_obj.name) is not None or
               BlenderNaming.try_parse_collision_shape_name(bl_obj.name) is not None)
