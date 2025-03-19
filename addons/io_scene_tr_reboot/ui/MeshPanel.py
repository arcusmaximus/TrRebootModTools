import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BrowseBlendShapeNormalsSourceFileOperator import BrowseBlendShapeNormalsSourceFileOperator
from io_scene_tr_reboot.operator.FixVertexGroupNamesOperator import FixVertexGroupNamesOperator
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties

class MeshPanel(bpy.types.Panel):
    bl_idname = "TR_PT_MeshObjectPanel"
    bl_space_type = "PROPERTIES"
    bl_region_type = "WINDOW"
    bl_context = "data"
    bl_label = "Tomb Raider Properties"

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None:
            return False

        bl_obj = context.object
        return bl_obj is not None and isinstance(bl_obj.data, bpy.types.Mesh) and BlenderNaming.try_parse_mesh_name(bl_obj.name) is not None

    def draw(self, context: bpy.types.Context | None) -> None:
        if context is None or self.layout is None:
            return

        bl_obj = context.object
        if bl_obj is None:
            return

        props = ObjectProperties.get_instance(bl_obj)

        self.layout.operator(FixVertexGroupNamesOperator.bl_idname)

        self.layout.label(text = "Shape Key Normals Source")
        row = self.layout.row(align = True)
        row.prop(props, "blend_shape_normals_source_file_path", text = "")
        row.operator(BrowseBlendShapeNormalsSourceFileOperator.bl_idname, icon = "FILE_FOLDER", text = "")

        self.layout.prop(props.mesh, "draw_group_id")
        self.layout.prop(props.mesh, "flags")
