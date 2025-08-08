from enum import IntEnum
from typing import ClassVar, Literal, NamedTuple, cast
from mathutils import Matrix, Vector
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CArray, CFloat, CInt, CShort, CStruct64

class _NodeType(IntEnum):
    BONE_TRANSFORM = 0
    MULTIPLY = 1
    NORMALIZE = 2
    CURVE_MAP = 3

class BlendShapeDriverBone(CStruct64):
    global_bone_id: CInt
    cone_angle: CFloat
    min_positive_angle: CFloat
    max_positive_angle: CFloat
    min_negative_angle: CFloat
    max_negative_angle: CFloat
    padding0: CInt
    bone_axis: Vector
    primary_axis: Vector
    attachment_matrix: Matrix

class BlendShapeDriverCurveMap(CStruct64):
    curve_points: CArray[CFloat, Literal[11]]

class _Link(CStruct64):
    from_node_idx: CShort
    to_node_idx: CShort
    from_slot_idx: CShort
    to_slot_idx: CShort

class _Graph(CStruct64):
    num_nodes: CInt
    num_links: CInt
    node_refs_ref: ResourceReference | None
    links_ref: ResourceReference | None

class BlendShapeDriverNodeInput(NamedTuple):
    node: "BlendShapeDriverNode"
    from_slot: int
    to_slot: int

class BlendShapeDriverNode(NamedTuple):
    properties: CStruct64
    inputs: list[BlendShapeDriverNodeInput]

class BlendShapeDriverOutput(NamedTuple):
    node: BlendShapeDriverNode
    slot: int
    global_blend_shape_id: int

class BlendShapeDriverSet:
    node_property_types: ClassVar[dict[_NodeType, type[CStruct64]]] = {
        _NodeType.BONE_TRANSFORM: BlendShapeDriverBone,
        _NodeType.CURVE_MAP:      BlendShapeDriverCurveMap
    }

    outputs: list[BlendShapeDriverOutput]

    def __init__(self) -> None:
        self.outputs = []

    def read(self, reader: ResourceReader) -> None:
        num_graphs = reader.read_int32()
        reader.skip(4)
        for graph in reader.read_struct_list(_Graph, num_graphs):
            if graph.node_refs_ref is None or graph.links_ref is None:
                continue

            nodes: list[BlendShapeDriverNode] = []
            reader.seek(graph.node_refs_ref)
            for node_ref in reader.read_ref_list(graph.num_nodes):
                if node_ref is None:
                    raise Exception()

                reader.seek(node_ref)
                node_type = cast(_NodeType, reader.read_int32())
                node_properties_type = BlendShapeDriverSet.node_property_types.get(node_type)
                if node_properties_type is None:
                    raise Exception(f"Pose space deformer node type {node_type} is not supported")

                node_properties = reader.read_struct(node_properties_type)
                nodes.append(BlendShapeDriverNode(node_properties, []))

            reader.seek(graph.links_ref)
            for link in reader.read_struct_list(_Link, graph.num_links):
                from_node = nodes[link.from_node_idx]
                if link.to_slot_idx >= 0:
                    to_node = nodes[link.to_node_idx]
                    to_node.inputs.append(BlendShapeDriverNodeInput(from_node, link.from_slot_idx, link.to_slot_idx))
                else:
                    self.outputs.append(BlendShapeDriverOutput(from_node, link.from_slot_idx, link.to_node_idx))
