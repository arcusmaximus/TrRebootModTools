from mathutils import Vector
from typing import TYPE_CHECKING, Annotated, Protocol, Sequence, cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.BlenderOperatorBase import BlenderOperatorBase
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup, Prop
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy._typing.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class _GreasePencilStrokePoint(Protocol):
    position: Vector
    radius: float
    opacity: float
    rotation: float
    select: bool
    vertex_color: tuple[float, float, float, float]

class _GreasePencilStrokeSlice(Protocol):
    points: Sequence[_GreasePencilStrokePoint]

class HairWeightPaintingProperties(BlenderPropertyGroup):
    enable: Annotated[bool, Prop("Enable")]

class HairWeightPaintingOperator(BlenderOperatorBase[HairWeightPaintingProperties]):
    bl_idname = "tr_reboot.hair_weight_paint"
    bl_label = "Enter/Leave Hair Weight Painting"
    bl_description = "Convert a hair curve object to/from a Grease Pencil object to allow weight painting"

    @staticmethod
    def static_execute(enable: bool) -> None:
        bpy.ops.tr_reboot.hair_weight_paint(enable = enable)      # type: ignore

    @classmethod
    def poll(cls, context: bpy.types.Context | None) -> bool:
        if context is None or context.active_object is None:
            return False

        return BlenderNaming.try_parse_hair_strand_group_name(context.active_object.name) is not None

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        bl_obj = context.active_object
        if bl_obj is None:
            return { "CANCELLED" }

        bpy.ops.object.mode_set(mode = "OBJECT")

        with OperatorContext.begin(self):
            if self.properties.enable and isinstance(bl_obj.data, bpy.types.Curves):
                self.convert_curves_to_grease_pencil(bl_obj)

                BlenderHelper.select_object(bl_obj)
                bpy.ops.grease_pencil.weightmode_toggle()
            elif not self.properties.enable and isinstance(bl_obj.data, bpy.types.GreasePencilv3):
                self.convert_grease_pencil_to_curves(bl_obj)

            return { "FINISHED" }

    def convert_curves_to_grease_pencil(self, bl_obj: bpy.types.Object) -> None:
        bl_curves = cast(bpy.types.Curves, bl_obj.data)
        material_name = self.get_curves_material_name(bl_obj)

        vertex_group_names: list[str] = []
        for bl_attr in bl_curves.attributes:
            if BlenderNaming.try_parse_bone_name(bl_attr.name) is not None and isinstance(bl_attr, bpy.types.FloatAttribute):
                vertex_group_names.append(bl_attr.name)

        BlenderHelper.select_object(bl_obj)
        bpy.ops.object.convert(target = "GREASEPENCIL")
        if bpy.context.active_object is None:
            return

        bl_obj = bpy.context.active_object
        bl_grease = cast(bpy.types.GreasePencilv3, bl_obj.data)

        bl_grease.stroke_depth_order = "3D"
        self.set_grease_pencil_material_name(bl_obj, material_name)
        for vertex_group_name in vertex_group_names:
            self.convert_grease_pencil_attribute_to_vertex_group(bl_obj, vertex_group_name)

    def convert_grease_pencil_to_curves(self, bl_obj: bpy.types.Object) -> None:
        material_name = self.get_grease_pencil_material_name(bl_obj)

        vertex_group_names = Enumerable(bl_obj.vertex_groups).select(lambda g: g.name).to_list()
        for vertex_group_name in vertex_group_names:
            self.convert_grease_pencil_vertex_group_to_attribute(bl_obj, vertex_group_name)

        BlenderHelper.select_object(bl_obj)
        bpy.ops.object.convert(target = "CURVES")

        if bpy.context.active_object is None:
            return

        bl_obj = bpy.context.active_object
        self.set_curves_material_name(bl_obj, material_name)

    def convert_grease_pencil_attribute_to_vertex_group(self, bl_obj: bpy.types.Object, attr_name: str) -> None:
        bl_grease = cast(bpy.types.GreasePencilv3, bl_obj.data)
        if len(bl_grease.layers) == 0:
            return

        bl_drawing = bl_grease.layers[0].current_frame().drawing
        if bl_drawing is None:
            return

        bl_strokes = cast(Sequence[_GreasePencilStrokeSlice], bl_drawing.strokes)
        if len(bl_strokes) == 0 or len(bl_strokes[0].points) == 0:
            return

        temp_vertex_group_name = "__temp_vertex_group"
        bl_vertex_group = bl_obj.vertex_groups.new(name = temp_vertex_group_name)
        bl_obj.vertex_groups.active = bl_vertex_group

        # "Initialize" the vertex group by setting a weight for a random point.
        # If we don't do this, the geometry node setup will write to a new attribute rather than to the vertex group.
        with BlenderHelper.enter_edit_mode(bl_obj):
            bl_strokes[0].points[0].select = True
            bpy.ops.object.vertex_group_assign()

        self.copy_attribute_using_geometry_nodes(bl_obj, attr_name, temp_vertex_group_name)
        bl_drawing.attributes.remove(bl_drawing.attributes[attr_name])
        bl_obj.vertex_groups[temp_vertex_group_name].name = attr_name

    def convert_grease_pencil_vertex_group_to_attribute(self, bl_obj: bpy.types.Object, vertex_group_name: str) -> None:
        temp_vertex_group_name = "__temp_vertex_group"
        bl_obj.vertex_groups[vertex_group_name].name = temp_vertex_group_name
        self.copy_attribute_using_geometry_nodes(bl_obj, temp_vertex_group_name, vertex_group_name)
        bl_obj.vertex_groups.remove(bl_obj.vertex_groups[temp_vertex_group_name])

    def copy_attribute_using_geometry_nodes(self, bl_obj: bpy.types.Object, from_attr_name: str, to_attr_name: str) -> None:
        bl_node_group = cast(bpy.types.GeometryNodeTree, bpy.data.node_groups.new("__copy_attribute", "GeometryNodeTree"))
        bl_node_group.is_modifier = True
        if bl_node_group.interface is None:
            raise Exception()

        bl_node_group.interface.new_socket("Geometry", in_out = "INPUT",  socket_type = "NodeSocketGeometry")
        bl_node_group.interface.new_socket("Geometry", in_out = "OUTPUT", socket_type = "NodeSocketGeometry")

        bl_input_node  = bl_node_group.nodes.new("NodeGroupInput")
        bl_output_node = bl_node_group.nodes.new("NodeGroupOutput")

        bl_read_attr_node = bl_node_group.nodes.new("GeometryNodeInputNamedAttribute")
        cast(bpy.types.NodeSocketString, bl_read_attr_node.inputs[0]).default_value = from_attr_name

        bl_write_attr_node = bl_node_group.nodes.new("GeometryNodeStoreNamedAttribute")
        cast(bpy.types.NodeSocketString, bl_write_attr_node.inputs[2]).default_value = to_attr_name

        # Group Input.Geometry -> Store Named Attribute.Geometry
        bl_node_group.links.new(bl_input_node.outputs[0], bl_write_attr_node.inputs[0])

        # Named Attribute.Attribute -> Store Named Attribute.Value
        bl_node_group.links.new(bl_read_attr_node.outputs[0], bl_write_attr_node.inputs[3])

        # Store Named Attribute.Geometry -> Group Output.Geometry
        bl_node_group.links.new(bl_write_attr_node.outputs[0], bl_output_node.inputs[0])

        bl_modifier = cast(bpy.types.NodesModifier, bl_obj.modifiers.new("Copy attribute", "NODES"))
        bl_modifier.node_group = bl_node_group

        BlenderHelper.select_object(bl_obj)
        bpy.ops.object.modifier_apply(modifier = bl_modifier.name)

        bpy.data.node_groups.remove(bl_node_group)

    def get_curves_material_name(self, bl_obj: bpy.types.Object) -> str | None:
        bl_curves = cast(bpy.types.Curves, bl_obj.data)
        if len(bl_curves.materials) == 0:
            return None

        bl_material = bl_curves.materials[0]
        if bl_material is None:
            return None

        return bl_material.name

    def set_curves_material_name(self, bl_obj: bpy.types.Object, material_name: str | None) -> None:
        bl_curves = cast(bpy.types.Curves, bl_obj.data)
        if material_name is None:
            bl_curves.materials.clear()
            return

        if len(bl_curves.materials) == 0:
            bl_curves.materials.append(None)

        bl_curves.materials[0] = bpy.data.materials.get(material_name)

    def get_grease_pencil_material_name(self, bl_obj: bpy.types.Object) -> str | None:
        bl_grease = cast(bpy.types.GreasePencilv3, bl_obj.data)
        if len(bl_grease.materials) == 0:
            return None

        bl_material = bl_grease.materials[0]
        if bl_material is None:
            return None

        return BlenderNaming.try_parse_grease_pencil_material_name(bl_material.name)

    def set_grease_pencil_material_name(self, bl_obj: bpy.types.Object, material_name: str | None) -> None:
        bl_grease = cast(bpy.types.GreasePencilv3, bl_obj.data)
        if material_name is None:
            bl_grease.materials.clear()
            return

        material_name = BlenderNaming.make_grease_pencil_material_name(material_name)
        bl_material = bpy.data.materials.get(material_name)
        if bl_material is None:
            bl_material = bpy.data.materials.new(material_name)
            bpy.data.materials.create_gpencil_data(bl_material)
            if bl_material.grease_pencil is not None:
                bl_material.grease_pencil.color = (0.5, 0.5, 0.5, 1)

        if len(bl_grease.materials) == 0:
            bl_grease.materials.append(None)

        bl_grease.materials[0] = bl_material
