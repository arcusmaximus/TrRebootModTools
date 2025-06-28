import bpy
from io_scene_tr_reboot.operator.ToggleCollisionVisibilityOperator import ToggleCollisionVisibilityOperator

class CollisionsPanel(bpy.types.Panel):
    bl_idname = "TR_PT_CollisionsPanel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Tomb Raider"
    bl_label = "Collisions"

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or self.layout is None:
            return

        bl_row = self.layout.row()
        bl_row.operator(ToggleCollisionVisibilityOperator.bl_idname)
