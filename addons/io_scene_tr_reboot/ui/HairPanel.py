from typing import cast
import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.HairWeightPaintingOperator import HairWeightPaintingOperator, HairWeightPaintingProperties

class HairPanel(bpy.types.Panel):
    bl_idname = "TR_PT_HairPanel"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Tomb Raider"
    bl_label = "Hair"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        return context is not None and context.active_object is not None and cls.is_hair_using_curves(context.active_object) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or context.active_object is None or self.layout is None:
            return

        is_using_curves = self.is_hair_using_curves(context.active_object)
        if is_using_curves is None:
            return

        bl_row = self.layout.row()

        label = "Start Weight Painting" if is_using_curves else "Stop Weight Painting"
        bl_op_props = cast(HairWeightPaintingProperties, bl_row.operator(HairWeightPaintingOperator.bl_idname, text = label))
        bl_op_props.enable = is_using_curves

    @classmethod
    def is_hair_using_curves(cls, bl_obj: bpy.types.Object) -> bool | None:
        if BlenderNaming.try_parse_hair_name(bl_obj.name) is None:
            return None

        if isinstance(bl_obj.data, bpy.types.Curves):
            return True
        elif isinstance(bl_obj.data, bpy.types.GreasePencilv3):
            return False
        else:
            return None
