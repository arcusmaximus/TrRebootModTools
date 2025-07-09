from typing import TYPE_CHECKING
import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BlenderOperatorBase import ImportOperatorBase, ImportOperatorProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class BrowseBlendShapeNormalsSourceFileOperator(ImportOperatorBase[ImportOperatorProperties]):
    bl_idname = "tr_reboot.browse_blend_shape_normals_source_file"
    bl_label = "Select"
    bl_description = "Browse for .trXmodeldata file"
    bl_menu_item_name = "Tomb Raider Reboot model"
    filename_ext = ".tr9modeldata;.tr10modeldata;.tr11modeldata"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        bl_obj = context and context.object
        return bl_obj is not None and isinstance(bl_obj.data, bpy.types.Mesh) and BlenderNaming.try_parse_mesh_name(bl_obj.name) is not None

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        bl_obj = context and context.object
        if bl_obj is None:
            return { "CANCELLED" }

        properties = ObjectProperties.get_instance(bl_obj)
        if properties.blend_shape_normals_source_file_path:
            self.properties.filepath = properties.blend_shape_normals_source_file_path
        else:
            mesh_id_set = BlenderNaming.parse_mesh_name(bl_obj.name)
            self.properties.filepath = f"{mesh_id_set.model_data_id}.tr11modeldata"

        return super().invoke(context, event)

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        bl_obj = context and context.object
        if bl_obj is None:
            return { "CANCELLED" }

        ObjectProperties.get_instance(bl_obj).blend_shape_normals_source_file_path = self.properties.filepath
        return { "FINISHED" }
