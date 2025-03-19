import bpy
from io_scene_tr_reboot.exchange.ModelExporter import ModelExporter
from io_scene_tr_reboot.tr.Enumerations import CdcGame

class Tr2013ModelExporter(ModelExporter):
    def __init__(self, scale_factor: float) -> None:
        super().__init__(scale_factor, CdcGame.TR2013)

    def validate_blender_objects(self, bl_objs: list[bpy.types.Object]) -> None:
        for bl_obj in bl_objs:
            if len(bl_obj.vertex_groups) > 42:
                raise Exception(f"TR2013 only supports 42 vertex groups per mesh, but object {bl_obj.name} has {len(bl_obj.vertex_groups)}. Please reduce the vertex groups or split the mesh.")
