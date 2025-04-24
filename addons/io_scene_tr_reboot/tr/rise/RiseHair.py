from array import array
import ctypes
from ctypes import sizeof
import random
from mathutils import Matrix, Vector
from typing import TYPE_CHECKING, Callable, Iterator, Literal, NamedTuple, Protocol, Sequence, TypeVar, cast, overload
from io_scene_tr_reboot.tr.Hair import Hair, HairPart, HairPoint, HairPointWeight
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CFloat, CInt, CStruct64, CUInt, CUShort
from io_scene_tr_reboot.util.CStructTypeMappings import CMatrix, CVec2, CVec3, CVec4
from io_scene_tr_reboot.util.Conditional import coalesce
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SpatialIndex import SpatialIndex

class HairAssetWrapper(CStruct64):
    num_bone_ids: CByte
    padding: CArray[CByte, Literal[7]]
    bone_ids_ref: ResourceReference | None
    hair_data_ref: ResourceReference | None
    hair_data_size: CUInt

assert(sizeof(HairAssetWrapper) == 0x1C)

class HairAssetBoundingBox(CStruct64):
    min: CVec3
    max: CVec3

assert(sizeof(HairAssetBoundingBox) == 0x18)

class HairAssetRange(CStruct64):
    start_offset: CInt
    end_offset: CInt

assert(sizeof(HairAssetRange) == 8)

class HairAssetMasterStrands(CStruct64):
    root_bind_frames: HairAssetRange
    descriptors: HairAssetRange

assert(sizeof(HairAssetMasterStrands) == 0x10)

class HairAssetMasterVertexWeight(CStruct64):
    value: CUShort
    bone_idx: CUShort

assert(sizeof(HairAssetMasterVertexWeight) == 4)

class HairAssetMasterVertexSkinning(CStruct64):
    weights: CArray[HairAssetMasterVertexWeight, Literal[4]]

assert(sizeof(HairAssetMasterVertexSkinning) == 0x10)

class HairAssetMasterVertices(CStruct64):
    bind_positions_mesh_space: HairAssetRange
    bind_positions_local_space: HairAssetRange
    rest_lengths: HairAssetRange
    diameters_for_collision: HairAssetRange
    skinning_data: HairAssetRange
    bind_inv_frames: HairAssetRange
    descriptors: HairAssetRange

assert(sizeof(HairAssetMasterVertices) == 0x38)

class HairSlaveStrandSkinning(CStruct64):
    root_bind_position: CVec3
    bind_position_packing_scale: CFloat
    master_strand_index_2: CUShort
    master_strand_index_1: CUShort
    packed_weight: CUInt

assert(sizeof(HairSlaveStrandSkinning) == 0x18)

class HairAssetSlaveStrands(CStruct64):
    descriptors: HairAssetRange
    cumulative_vertex_counts: HairAssetRange
    skinning_datas: HairAssetRange
    noise_offsets: HairAssetRange

assert(sizeof(HairAssetSlaveStrands) == 0x20)

class HairAssetSlaveVertices(CStruct64):
    local_bind_positions: HairAssetRange
    tex_coords: HairAssetRange
    slave_strand_indices: HairAssetRange
    skinning_datas: HairAssetRange

assert(sizeof(HairAssetSlaveVertices) == 0x20)

class IHairAssetRenderingData(Protocol):
    slave_strip_indices: HairAssetRange

class HairAssetRenderingData(CStruct64, IHairAssetRenderingData if TYPE_CHECKING else object):
    slave_strip_indices: HairAssetRange

assert(sizeof(HairAssetRenderingData) == 8)

class HairAssetCollisionCapsule(CStruct64):
    position0: CVec3
    position1: CVec3
    half_height: CFloat
    radius: CFloat
    skinning_transform_index: CUInt

assert(sizeof(HairAssetCollisionCapsule) == 0x24)

class HairAssetCollisionData(CStruct64):
    capsules: HairAssetRange

assert(sizeof(HairAssetCollisionData) == 8)

class IHairAsset(Protocol):
    tag: int
    version: int
    size: int
    num_groups: int
    num_skinning_transforms: int
    num_master_strands: int
    num_master_vertices: int
    num_slave_strands: int
    num_slave_vertices: int
    bind_pose_aabb: HairAssetBoundingBox
    bind_pose_slave_offsets_reference_position: CVec3
    slave_offsets_scale: float
    occlusion_sphere_radius: float
    group_name_offsets: HairAssetRange
    master_strands: HairAssetMasterStrands
    master_vertices: HairAssetMasterVertices
    slave_strands: HairAssetSlaveStrands
    slave_vertices: HairAssetSlaveVertices
    rendering_data: IHairAssetRenderingData
    collision_data: HairAssetCollisionData

class HairAsset(CStruct64, IHairAsset if TYPE_CHECKING else object):
    tag: CUInt
    version: CInt
    size: CUInt
    num_groups: CInt
    num_skinning_transforms: CInt
    num_master_strands: CInt
    num_master_vertices: CInt
    num_slave_strands: CInt
    num_slave_vertices: CInt
    bind_pose_aabb: HairAssetBoundingBox
    bind_pose_slave_offsets_reference_position: CVec3
    slave_offsets_scale: CFloat
    group_name_offsets: HairAssetRange
    master_strands: HairAssetMasterStrands
    master_vertices: HairAssetMasterVertices
    slave_strands: HairAssetSlaveStrands
    slave_vertices: HairAssetSlaveVertices
    rendering_data: HairAssetRenderingData      # type: ignore
    collision_data: HairAssetCollisionData

    occlusion_sphere_radius: float
    _ignored_fields_ = ("occlusion_sphere_radius",)

assert(sizeof(HairAsset) == 0xEC)

class _HairStrandDescriptor(NamedTuple):
    group_idx: int
    first_vertex_idx: int
    num_vertices: int

class _HairStrandReference(NamedTuple):
    part_idx: int
    strand_idx_in_part: int
    first_vertex_idx_in_part: int
    num_vertices: int

class _HairMasterVertexDescriptor(NamedTuple):
    strand_idx: int
    index_in_strand: int

T = TypeVar("T")
TStruct = TypeVar("TStruct", bound = CStruct64)

class RiseHair(Hair):
    HEADER_MAGIC = 0xDC4A6B8D
    FOOTER_MAGIC = 0x23B59472

    def __init__(self, hair_data_id: int) -> None:
        super().__init__(None, hair_data_id)

    @property
    def supports_strand_thickness(self) -> bool:
        return False

    def read(self, reader: ResourceReader) -> None:
        wrapper = reader.read_struct(HairAssetWrapper)
        if wrapper.bone_ids_ref is None or wrapper.hair_data_ref is None:
            return

        reader.seek(wrapper.bone_ids_ref)
        bone_ids = reader.read_uint16_list(wrapper.num_bone_ids)

        reader.seek(wrapper.hair_data_ref)
        hair_data_pos = reader.position
        asset = self.read_asset(reader)
        reader.position = hair_data_pos

        self.read_part_names(reader, asset)
        self.read_master_strand_groups(reader, asset, bone_ids)
        self.read_slave_strand_groups(reader, asset)

    def read_asset(self, reader: ResourceReader) -> IHairAsset:
        return reader.read_struct(HairAsset)

    def read_part_names(self, reader: ResourceReader, asset: IHairAsset) -> None:
        name_offsets = self.read_uint32_array(reader, asset.group_name_offsets)
        for i in range(0, len(name_offsets), 2):
            start_offset, end_offset = name_offsets[i], name_offsets[i + 1]
            name = reader.read_bytes_at(start_offset, end_offset - 1 - start_offset).tobytes().decode()
            self.parts.append(HairPart(name))

    def read_master_strand_groups(self, reader: ResourceReader, asset: IHairAsset, bone_ids: Sequence[int]) -> None:
        strand_descriptors   = self.read_strand_descriptor_array(reader, asset.master_strands.descriptors)
        vertex_positions     = self.read_vec4_array(reader, asset.master_vertices.bind_positions_mesh_space)
        vertex_skinning_data = self.read_array(reader, HairAssetMasterVertexSkinning, asset.master_vertices.skinning_data)

        for strand_descriptor in strand_descriptors:
            strand_group = self.parts[strand_descriptor.group_idx].master_strands
            strand_group.strand_point_counts.append(strand_descriptor.num_vertices)
            for vertex_idx in range(strand_descriptor.first_vertex_idx, strand_descriptor.first_vertex_idx + strand_descriptor.num_vertices):
                position = vertex_positions[vertex_idx]

                weights: list[HairPointWeight] = []
                for weight in vertex_skinning_data[vertex_idx].weights:
                    if weight.value > 0:
                        weights.append(HairPointWeight(bone_ids[weight.bone_idx], weight.value / 0xFFFF))

                strand_group.points.append(HairPoint(position, 0.1, weights))

    def read_slave_strand_groups(self, reader: ResourceReader, asset: IHairAsset) -> None:
        strand_descriptors = self.read_strand_descriptor_array(reader, asset.slave_strands.descriptors)
        strand_skinning_datas   = self.read_array(reader, HairSlaveStrandSkinning, asset.slave_strands.skinning_datas)
        packed_vertex_positions = self.read_uint32_array(reader, asset.slave_vertices.local_bind_positions)
        for strand_idx, strand_descriptor in enumerate(strand_descriptors):
            strand_group = self.parts[strand_descriptor.group_idx].slave_strands
            strand_group.strand_point_counts.append(strand_descriptor.num_vertices)

            strand_skinning_data = strand_skinning_datas[strand_idx]
            base_position = strand_skinning_data.root_bind_position.to_vector()
            packing_scale = strand_skinning_data.bind_position_packing_scale
            for vertex_idx in range(strand_descriptor.first_vertex_idx, strand_descriptor.first_vertex_idx + strand_descriptor.num_vertices):
                vertex_position = self.unpack_slave_vertex_position(packed_vertex_positions[vertex_idx], base_position, packing_scale)
                strand_group.points.append(HairPoint(vertex_position, 0.1, []))

                base_position = vertex_position

    def write(self, writer: ResourceBuilder) -> None:
        wrapper = HairAssetWrapper()
        wrapper.bone_ids_ref = writer.make_internal_ref()
        wrapper.hair_data_ref = writer.make_internal_ref()
        writer.write_struct(wrapper)

        bone_ids = self.collect_bone_ids()
        wrapper.num_bone_ids = len(bone_ids)
        wrapper.bone_ids_ref.offset = writer.position
        writer.write_uint16_list(bone_ids)
        writer.align(8)

        wrapper.hair_data_ref.offset = writer.position
        asset = self.write_asset(writer, bone_ids)
        wrapper.hair_data_size = asset.size

        writer.position = 0
        writer.write_struct(wrapper)

    def write_asset(self, writer: ResourceBuilder, bone_ids: list[int]) -> IHairAsset:
        asset = self.create_asset()

        asset_pos = writer.position
        writer.write_bytes(b"\0" * sizeof(cast(CStruct64, asset)))
        writer.position = asset_pos

        self.set_asset_global_properties(asset, bone_ids)
        self.append_part_names(writer, asset)
        master_strands  = self.append_master_strands(writer, asset)
        master_vertices = self.append_master_vertices(writer, asset, bone_ids)
        self.append_slave_strands_and_vertices(writer, asset, master_strands, master_vertices)
        self.append_rendering_data(writer, asset)
        self.append_collision_data(writer, asset)

        writer.position = writer.size
        writer.write_uint32(RiseHair.FOOTER_MAGIC)
        asset.size = writer.position - asset_pos

        writer.position = asset_pos
        writer.write_struct(cast(CStruct64, asset))
        writer.position = writer.size
        return asset

    def create_asset(self) -> IHairAsset:
        asset = HairAsset()
        asset.version = 20
        return asset

    def set_asset_global_properties(self, asset: IHairAsset, bone_ids: list[int]) -> None:
        asset.tag = RiseHair.HEADER_MAGIC
        asset.num_groups = len(self.parts)
        asset.num_skinning_transforms = len(bone_ids)
        asset.num_master_strands  = Enumerable(self.parts).sum(lambda p: len(p.master_strands.strand_point_counts))
        asset.num_master_vertices = Enumerable(self.parts).sum(lambda p: len(p.master_strands.points))
        asset.num_slave_strands   = Enumerable(self.parts).sum(lambda p: len(p.slave_strands.strand_point_counts))
        asset.num_slave_vertices  = Enumerable(self.parts).sum(lambda p: len(p.slave_strands.points))

        asset.bind_pose_aabb = self.get_bounding_box()
        asset.bind_pose_slave_offsets_reference_position = CVec3.from_coords(
            (asset.bind_pose_aabb.min.x + asset.bind_pose_aabb.max.x) / 2,
            (asset.bind_pose_aabb.min.y + asset.bind_pose_aabb.max.y) / 2,
            (asset.bind_pose_aabb.min.z + asset.bind_pose_aabb.max.z) / 2
        )
        asset.slave_offsets_scale = max(
            asset.bind_pose_aabb.max.x - asset.bind_pose_aabb.min.x,
            asset.bind_pose_aabb.max.y - asset.bind_pose_aabb.min.y,
            asset.bind_pose_aabb.max.z - asset.bind_pose_aabb.min.z
        )
        asset.occlusion_sphere_radius = asset.slave_offsets_scale

    def append_part_names(self, writer: ResourceBuilder, asset: IHairAsset) -> None:
        asset_pos = writer.position
        writer.position = writer.size

        name_offsets: list[int] = []
        for part in Enumerable(self.parts):
            name_offsets.append(writer.position - asset_pos)
            writer.write_string(part.name)
            name_offsets.append(writer.position - asset_pos)

        writer.align(8)

        writer.position = asset_pos
        asset.group_name_offsets = self.append_uint32_array(writer, name_offsets)

    def append_master_strands(self, writer: ResourceBuilder, asset: IHairAsset) -> list[_HairStrandDescriptor]:
        descriptors = cast(list[_HairStrandDescriptor], [None] * asset.num_master_strands)
        packed_descriptors = array("I", [0]) * asset.num_master_strands
        root_bind_frames = cast(list[Matrix], [None] * asset.num_master_strands)

        strand_idx = 0
        first_vertex_idx = 0
        for part_idx, part in enumerate(self.parts):
            first_vertex_idx_in_part = 0
            for strand_idx_in_part, num_strand_vertices in enumerate(part.master_strands.strand_point_counts):
                if num_strand_vertices < 2:
                    raise Exception("Master strands must have at least 2 vertices")

                descriptor = _HairStrandDescriptor(part_idx, first_vertex_idx, num_strand_vertices)
                descriptors[strand_idx] = descriptor
                packed_descriptors[strand_idx] = self.pack_strand_descriptor(descriptor)

                strand_y_axis = self.get_master_strand_y_axis(part, first_vertex_idx_in_part, num_strand_vertices)
                root_bind_frames[strand_idx] = self.get_master_vertex_matrix(part, strand_idx_in_part, strand_y_axis, first_vertex_idx_in_part, 0)

                strand_idx += 1
                first_vertex_idx += num_strand_vertices
                first_vertex_idx_in_part += num_strand_vertices

        asset.master_strands = HairAssetMasterStrands()
        asset.master_strands.root_bind_frames = self.append_matrix_array(writer, root_bind_frames)
        asset.master_strands.descriptors      = self.append_uint32_array(writer, packed_descriptors)
        return descriptors

    def append_master_vertices(self, writer: ResourceBuilder, asset: IHairAsset, bone_ids: list[int]) -> list[_HairMasterVertexDescriptor]:
        descriptors = cast(list[_HairMasterVertexDescriptor], [None] * asset.num_master_vertices)
        packed_descriptors = array("I", [0]) * asset.num_master_vertices

        bind_positions_mesh_space  = cast(list[Vector], [None] * asset.num_master_vertices)
        bind_positions_local_space = cast(list[Vector], [None] * asset.num_master_vertices)
        rest_lengths = array("f", [0]) * asset.num_master_vertices
        diameters_for_collision = array("f", [1]) * asset.num_master_vertices * 2
        skinning_datas  = (HairAssetMasterVertexSkinning * asset.num_master_vertices)()
        bind_inv_frames = cast(list[Matrix], [None] * asset.num_master_vertices)

        strand_idx = 0
        vertex_idx = 0
        for part in self.parts:
            vertex_idx_in_part = 0
            for strand_idx_in_part, num_strand_vertices in enumerate(part.master_strands.strand_point_counts):
                strand_y_axis = self.get_master_strand_y_axis(part, vertex_idx_in_part, num_strand_vertices)
                for vertex_idx_in_strand in range(num_strand_vertices):
                    descriptor = _HairMasterVertexDescriptor(strand_idx, vertex_idx_in_strand)
                    descriptors[vertex_idx] = descriptor
                    packed_descriptors[vertex_idx] = self.pack_master_vertex_descriptor(descriptor)

                    point = part.master_strands.points[vertex_idx_in_part]
                    bind_positions_mesh_space[vertex_idx] = Vector((point.position.x, point.position.y, point.position.z, vertex_idx_in_strand / (num_strand_vertices - 1)))
                    if vertex_idx_in_strand == 0:
                        bind_positions_local_space[vertex_idx] = Vector()
                    else:
                        offset = point.position - part.master_strands.points[vertex_idx_in_part - 1].position
                        rest_lengths[vertex_idx - 1] = offset.length
                        if vertex_idx_in_strand == 1:
                            bind_positions_local_space[vertex_idx] = offset
                        else:
                            bind_positions_local_space[vertex_idx] = bind_inv_frames[vertex_idx - 2].to_3x3() @ offset

                    skinning_datas[vertex_idx]  = self.get_master_vertex_skinning_data(point, bone_ids)
                    bind_inv_frames[vertex_idx] = self.get_master_vertex_matrix(part, strand_idx_in_part, strand_y_axis, vertex_idx_in_part, vertex_idx_in_strand).inverted()

                    vertex_idx += 1
                    vertex_idx_in_part += 1

                strand_idx += 1

        asset.master_vertices = HairAssetMasterVertices()
        asset.master_vertices.bind_positions_mesh_space  = self.append_vec4_array(writer, bind_positions_mesh_space)
        asset.master_vertices.bind_positions_local_space = self.append_vec3_array(writer, bind_positions_local_space)
        asset.master_vertices.rest_lengths    = self.append_float_array(writer, rest_lengths)
        asset.master_vertices.diameters_for_collision = self.append_float_array(writer, diameters_for_collision)
        asset.master_vertices.skinning_data   = self.append_array(writer, skinning_datas)
        asset.master_vertices.bind_inv_frames = self.append_matrix_array(writer, bind_inv_frames)
        asset.master_vertices.descriptors     = self.append_uint32_array(writer, packed_descriptors)
        return descriptors

    def get_master_vertex_skinning_data(self, point: HairPoint, bone_ids: list[int]) -> HairAssetMasterVertexSkinning:
        skinning = HairAssetMasterVertexSkinning()
        total_int_weight = 0
        for i in range(4):
            skinning.weights[i] = HairAssetMasterVertexWeight()
            if i < len(point.weights):
                int_weight = int(point.weights[i].weight * 0xFFFF)
                total_int_weight += int_weight
                skinning.weights[i].bone_idx = bone_ids.index(point.weights[i].global_bone_id)
                skinning.weights[i].value = int_weight

        skinning.weights[0].value += 0xFFFF - total_int_weight
        return skinning

    def get_master_strand_y_axis(self, part: HairPart, first_vertex_idx_in_part: int, num_strand_vertices: int) -> Vector:
        if num_strand_vertices < 3:
            return Vector((0, 1, 0))

        first_vertex_pos = part.master_strands.points[first_vertex_idx_in_part].position
        middle_vertex_pos = part.master_strands.points[first_vertex_idx_in_part + num_strand_vertices // 2].position
        last_vertex_pos = part.master_strands.points[first_vertex_idx_in_part + num_strand_vertices - 1].position
        x_axis = last_vertex_pos - first_vertex_pos
        y_axis = cast(Vector, x_axis.cross(middle_vertex_pos - first_vertex_pos))
        if y_axis.length_squared < 0.0001:
            return Vector((0, 1, 0))

        y_axis.normalize()
        return y_axis

    def get_master_vertex_matrix(self, part: HairPart, strand_idx_in_part: int, strand_y_axis: Vector, vertex_idx_in_part: int, vertex_idx_in_strand: int) -> Matrix:
        vertex_position = part.master_strands.points[vertex_idx_in_part].position

        if vertex_idx_in_strand < part.master_strands.strand_point_counts[strand_idx_in_part] - 1:
            x_axis_start_pos = vertex_position
            x_axis_end_pos   = part.master_strands.points[vertex_idx_in_part + 1].position
        else:
            x_axis_start_pos = part.master_strands.points[vertex_idx_in_part - 1].position
            x_axis_end_pos   = vertex_position

        x_axis = x_axis_end_pos - x_axis_start_pos
        if x_axis.length_squared < 0.0001:
            x_axis = Vector((1, 0, 0))
        else:
            x_axis.normalize()

        z_axis = cast(Vector, strand_y_axis.cross(x_axis))
        if z_axis.length_squared < 0.0001:
            z_axis = cast(Vector, Vector((0, 1, 0)).cross(x_axis))
            if z_axis.length_squared < 0.0001:
                z_axis = Vector((0, 0, 1))
            else:
                z_axis.normalize()
        else:
            z_axis.normalize()

        y_axis = cast(Vector, x_axis.cross(z_axis))

        return Matrix((
            (x_axis.x, y_axis.x, z_axis.x, vertex_position.x),
            (x_axis.y, y_axis.y, z_axis.y, vertex_position.y),
            (x_axis.z, y_axis.z, z_axis.z, vertex_position.z),
            (0,        0,        0,        1)
        ))

    def append_slave_strands_and_vertices(
        self,
        writer: ResourceBuilder,
        asset: IHairAsset,
        master_strands: list[_HairStrandDescriptor],
        master_vertices: list[_HairMasterVertexDescriptor]
    ) -> None:
        skinning_calculator = _SlaveStrandSkinningCalculator(self, master_strands, master_vertices)

        strand_descriptors              = array("I", [0]) * asset.num_slave_strands
        strand_cumulative_vertex_counts = array("I", [0]) * asset.num_slave_strands
        strand_skinning_datas           = (HairSlaveStrandSkinning * asset.num_slave_strands)()
        strand_noise_offsets            = array("H", [0]) * ((asset.num_slave_strands + 1) & ~1)

        vertex_positions      = array("I", [0]) * asset.num_slave_vertices
        vertex_tex_coords     = array("I", [0]) * asset.num_slave_vertices
        vertex_strand_indices = array("H", [0]) * ((asset.num_slave_vertices + 1) & ~1)
        vertex_skinning_datas = array("H", [0]) * ((asset.num_slave_vertices + 1) & ~1)

        output_strand_idx = 0
        output_vertex_idx = 0
        for source_strand in self.get_slave_strand_output_order():
            part = self.parts[source_strand.part_idx]
            num_strand_vertices = part.slave_strands.strand_point_counts[source_strand.strand_idx_in_part]

            strand_descriptor = _HairStrandDescriptor(source_strand.part_idx, output_vertex_idx, num_strand_vertices)
            strand_base_position = part.slave_strands.points[source_strand.first_vertex_idx_in_part].position
            strand_packing_scale = self.calc_slave_strand_packing_scale(part, source_strand.first_vertex_idx_in_part, num_strand_vertices)
            strand_skinning_calc = skinning_calculator.calculate(source_strand)

            strand_descriptors[output_strand_idx] = self.pack_strand_descriptor(strand_descriptor)

            strand_skinning = HairSlaveStrandSkinning()
            strand_skinning.root_bind_position = CVec3.from_vector(strand_base_position)
            strand_skinning.bind_position_packing_scale = strand_packing_scale
            strand_skinning.master_strand_index_1 = strand_skinning_calc.master_strand_indices[0]
            strand_skinning.master_strand_index_2 = strand_skinning_calc.master_strand_indices[1]
            strand_skinning.packed_weight = self.pack_slave_strand_skinning_weight(strand_skinning_calc.weight)
            strand_skinning.map_fields_to_c()
            strand_skinning_datas[output_strand_idx] = strand_skinning

            strand_noise_offsets[output_strand_idx] = int(output_strand_idx / asset.num_slave_strands * 0xFFFF)

            prev_vertex_pos = strand_base_position
            for vertex_idx_in_strand in range(num_strand_vertices):
                position = part.slave_strands.points[source_strand.first_vertex_idx_in_part + vertex_idx_in_strand].position
                vertex_positions[output_vertex_idx] = self.pack_slave_vertex_position(position, prev_vertex_pos, strand_packing_scale)
                vertex_strand_indices[(output_vertex_idx & ~1) + (1 - (output_vertex_idx & 1))] = output_strand_idx

                vertex_skinning_calc = strand_skinning_calc.vertices[vertex_idx_in_strand]
                vertex_skinning_datas[(output_vertex_idx & ~1) + (1 - (output_vertex_idx & 1))] = self.pack_slave_vertex_skinning(
                    vertex_skinning_calc.master_vertex_indices_in_strand,
                    vertex_skinning_calc.weight
                )

                prev_vertex_pos = position
                output_vertex_idx += 1

            strand_cumulative_vertex_counts[output_strand_idx] = output_vertex_idx
            output_strand_idx += 1

        asset.slave_strands = HairAssetSlaveStrands()
        asset.slave_strands.descriptors              = self.append_uint32_array(writer, strand_descriptors)
        asset.slave_strands.cumulative_vertex_counts = self.append_uint32_array(writer, strand_cumulative_vertex_counts)
        asset.slave_strands.skinning_datas           = self.append_array(writer, strand_skinning_datas)
        asset.slave_strands.noise_offsets            = self.append_uint16_array(writer, strand_noise_offsets)

        asset.slave_vertices = HairAssetSlaveVertices()
        asset.slave_vertices.local_bind_positions = self.append_uint32_array(writer, vertex_positions)
        asset.slave_vertices.skinning_datas       = self.append_uint16_array(writer, vertex_skinning_datas)
        asset.slave_vertices.tex_coords           = self.append_uint32_array(writer, vertex_tex_coords)
        asset.slave_vertices.slave_strand_indices = self.append_uint16_array(writer, vertex_strand_indices)

    def append_rendering_data(self, writer: ResourceBuilder, asset: IHairAsset) -> None:
        slave_strip_indices = array("I", [0]) * (asset.num_slave_vertices * 2 + asset.num_slave_strands)

        target_idx = 0
        idx_to_write = 0
        for part in self.parts:
            for num_strand_vertices in part.slave_strands.strand_point_counts:
                for _ in range(num_strand_vertices * 2):
                    slave_strip_indices[target_idx] = idx_to_write
                    target_idx += 1
                    idx_to_write += 1

                slave_strip_indices[target_idx] = 0xFFFFFFFF
                target_idx += 1

        asset.rendering_data = self.create_rendering_data()
        asset.rendering_data.slave_strip_indices = self.append_uint32_array(writer, slave_strip_indices)

    def create_rendering_data(self) -> IHairAssetRenderingData:
        return HairAssetRenderingData()

    def append_collision_data(self, writer: ResourceBuilder, asset: IHairAsset):
        asset.collision_data = HairAssetCollisionData()
        asset.collision_data.capsules = self.append_array(writer, (HairAssetCollisionCapsule * 0)())

    def collect_bone_ids(self) -> list[int]:
        bone_ids: set[int] = set()
        for part in self.parts:
            for point in part.master_strands.points:
                for weight in point.weights:
                    bone_ids.add(weight.global_bone_id)

        return Enumerable(bone_ids).order_by(lambda id: id).to_list()

    def get_bounding_box(self) -> HairAssetBoundingBox:
        min_x =  100000
        min_y =  100000
        min_z =  100000

        max_x = -100000
        max_y = -100000
        max_z = -100000

        for point in Enumerable(self.parts).select_many(lambda p: p.master_strands.points):
            min_x = min(min_x, point.position.x)
            min_y = min(min_y, point.position.y)
            min_z = min(min_z, point.position.z)

            max_x = max(max_x, point.position.x)
            max_y = max(max_y, point.position.y)
            max_z = max(max_z, point.position.z)

        bounding_box = HairAssetBoundingBox()
        bounding_box.min = CVec3.from_coords(min_x, min_y, min_z)
        bounding_box.max = CVec3.from_coords(max_x, max_y, max_z)
        return bounding_box

    def calc_slave_strand_packing_scale(self, part: HairPart, first_vertex_idx_in_part: int, num_vertices: int) -> float:
        max_coord_difference = 0
        for vertex_idx_in_part in range(first_vertex_idx_in_part, first_vertex_idx_in_part + num_vertices - 1):
            offset = part.slave_strands.points[vertex_idx_in_part + 1].position - part.slave_strands.points[vertex_idx_in_part].position
            max_coord_difference = max(abs(offset.x), abs(offset.y), abs(offset.z), max_coord_difference)

        return max_coord_difference / 0x1FF

    def get_slave_strand_output_order(self) -> Iterator[_HairStrandReference]:
        # The game only renders the first X% of slave strands depending on distance,
        # so we need to make sure that at any percentage, the trimmed list has a good spatial distribution
        lookup = SpatialIndex[_HairStrandReference](0.8)
        for part_idx, part in enumerate(self.parts):
            first_vertex_idx_in_part = 0
            for strand_idx_in_part, num_strand_vertices in enumerate(part.slave_strands.strand_point_counts):
                strand_ref = _HairStrandReference(part_idx, strand_idx_in_part, first_vertex_idx_in_part, num_strand_vertices)
                lookup.add(part.slave_strands.points[first_vertex_idx_in_part + num_strand_vertices // 2].position, strand_ref)
                first_vertex_idx_in_part += num_strand_vertices

        sorted_cells = Enumerable(lookup.grid.items()).order_by(lambda p: p[0]).select(lambda p: p[1]).to_list()
        if len(sorted_cells) == 0:
            return

        max_strands_per_cell = Enumerable(sorted_cells).max(lambda strands_of_cell: len(strands_of_cell))
        for strand_idx_in_cell in range(max_strands_per_cell):
            for strands_of_cell in sorted_cells:
                if strand_idx_in_cell < len(strands_of_cell):
                    yield strands_of_cell[strand_idx_in_cell].item

    def read_uint16_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[int]:
        return self.read_array(reader, CUShort, range, reader.read_uint16_list)

    def read_uint32_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[int]:
        return self.read_array(reader, CUInt, range, reader.read_uint32_list)

    def read_float_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[float]:
        return self.read_array(reader, CFloat, range, reader.read_float_list)

    def read_vec2_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[Vector]:
        return self.read_array(reader, CVec2, range, reader.read_vec2d_list)

    def read_vec3_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[Vector]:
        return self.read_array(reader, CVec3, range, reader.read_vec3d_list)

    def read_vec4_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[Vector]:
        return self.read_array(reader, CVec4, range, reader.read_vec4d_list)

    def read_matrix_array(self, reader: ResourceReader, range: HairAssetRange) -> Sequence[Matrix]:
        return self.read_array(reader, CMatrix, range, reader.read_mat4x4_list)

    def read_strand_descriptor_array(self, reader: ResourceReader, _range: HairAssetRange) -> list[_HairStrandDescriptor]:
        packed_descriptors = self.read_uint32_array(reader, _range)
        descriptors: list[_HairStrandDescriptor] = cast(list[_HairStrandDescriptor], [None] * len(packed_descriptors))
        for i, packed_descriptor in enumerate(packed_descriptors):
            descriptors[i] = self.unpack_strand_descriptor(packed_descriptor)

        return descriptors

    @overload
    def read_array(self, reader: ResourceReader, item_type: type[T], range: HairAssetRange) -> Sequence[T]: ...

    @overload
    def read_array(self, reader: ResourceReader, item_type: type, range: HairAssetRange, read_func: Callable[[int], Sequence[T]], /) -> Sequence[T]: ...

    def read_array(self, reader: ResourceReader, item_type: type, range: HairAssetRange, read_func: Callable[[int], Sequence[T]] | None = None) -> Sequence[T]:
        prev_pos = reader.position
        reader.position += range.start_offset

        num_items = (range.end_offset - range.start_offset) // sizeof(cast(type[ctypes.Structure], item_type))
        if read_func is not None:
            items = read_func(num_items)
        else:
            items = cast(Sequence[T], reader.read_struct_list(cast(type[CStruct64], item_type), num_items))

        reader.position = prev_pos
        return items

    def append_uint16_array(self, writer: ResourceBuilder, items: Sequence[int]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_uint16_list)

    def append_uint32_array(self, writer: ResourceBuilder, items: Sequence[int]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_uint32_list)

    def append_float_array(self, writer: ResourceBuilder, items: Sequence[float]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_float_list)

    def append_vec2_array(self, writer: ResourceBuilder, items: Sequence[Vector]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_vec2d_list)

    def append_vec3_array(self, writer: ResourceBuilder, items: Sequence[Vector]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_vec3d_list)

    def append_vec4_array(self, writer: ResourceBuilder, items: Sequence[Vector]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_vec4d_list)

    def append_matrix_array(self, writer: ResourceBuilder, items: Sequence[Matrix]) -> HairAssetRange:
        return self.append_array(writer, items, writer.write_mat4x4_list)

    @overload
    def append_array(self, writer: ResourceBuilder, items: ctypes.Array[TStruct]) -> HairAssetRange: ...

    @overload
    def append_array(self, writer: ResourceBuilder, items: Sequence[T], write_func: Callable[[Sequence[T]], None] | None = None) -> HairAssetRange: ...

    def append_array(self, writer: ResourceBuilder, items: Sequence[T] | ctypes.Array[TStruct], write_func: Callable[[Sequence[T]], None] | None = None) -> HairAssetRange:
        base_pos = writer.position
        writer.position = writer.size

        start_pos = writer.position
        if write_func is not None:
            write_func(cast(Sequence[T], items))
        else:
            writer.write_struct_list(cast(Sequence[CStruct64], items))

        end_pos = writer.position

        range = HairAssetRange()
        range.start_offset = start_pos - base_pos
        range.end_offset = end_pos - base_pos

        writer.position = base_pos
        return range

    def unpack_strand_descriptor(self, packed_descriptor: int) -> _HairStrandDescriptor:
        first_vertex_idx = packed_descriptor & 0x3FFFFF
        packed_descriptor >>= 22

        num_vertices = (packed_descriptor & 0x1F) + 1
        packed_descriptor >>= 5

        group_idx = packed_descriptor

        return _HairStrandDescriptor(group_idx, first_vertex_idx, num_vertices)

    def pack_strand_descriptor(self, descriptor: _HairStrandDescriptor) -> int:
        packed_descriptor = descriptor.group_idx

        packed_descriptor <<= 5
        packed_descriptor |= descriptor.num_vertices - 1

        packed_descriptor <<= 22
        packed_descriptor |= descriptor.first_vertex_idx
        return packed_descriptor

    def unpack_master_vertex_descriptor(self, packed_descriptor: int) -> _HairMasterVertexDescriptor:
        index_in_strand = packed_descriptor & 0xFFFF
        packed_descriptor >>= 16

        strand_idx = packed_descriptor

        return _HairMasterVertexDescriptor(strand_idx, index_in_strand)

    def pack_master_vertex_descriptor(self, descriptor: _HairMasterVertexDescriptor) -> int:
        return (descriptor.strand_idx << 16) | descriptor.index_in_strand

    def pack_slave_strand_skinning_weight(self, weight: float) -> int:
        packed_weight = int(weight * 63)

        packed_weight <<= 20
        packed_weight |= random.randint(600, 0xFFFFE)    # Seems like a viewing distance above which slave strands snap to a master strand

        return packed_weight

    def unpack_slave_vertex_position(self, packed_position: int, base_position: Vector, packing_scale: float) -> Vector:
        z = packed_position & 0x1FF
        packed_position >>= 9
        z_sign = packed_position & 1
        packed_position >>= 1

        y = packed_position & 0x1FF
        packed_position >>= 9
        y_sign = packed_position & 1
        packed_position >>= 1

        x = packed_position & 0x1FF
        packed_position >>= 9
        x_sign = packed_position & 1

        return Vector((
            x * (-1 if x_sign else 1) * packing_scale + base_position.x,
            y * (-1 if y_sign else 1) * packing_scale + base_position.y,
            z * (-1 if z_sign else 1) * packing_scale + base_position.z
        ))

    def pack_slave_vertex_position(self, position: Vector, base_position: Vector, packing_scale: float) -> int:
        x = min(int(abs((position.x - base_position.x) / packing_scale)), 0x1FF)
        x_sign = 1 if position.x < base_position.x else 0

        y = min(int(abs((position.y - base_position.y) / packing_scale)), 0x1FF)
        y_sign = 1 if position.y < base_position.y else 0

        z = min(int(abs((position.z - base_position.z) / packing_scale)), 0x1FF)
        z_sign = 1 if position.z < base_position.z else 0

        packed_position = x_sign
        packed_position <<= 9
        packed_position |= x

        packed_position <<= 1
        packed_position |= y_sign
        packed_position <<= 9
        packed_position |= y

        packed_position <<= 1
        packed_position |= z_sign
        packed_position <<= 9
        packed_position |= z

        return packed_position

    def pack_slave_vertex_skinning(self, master_vertex_indices_in_strand: tuple[int, int], weight: float) -> int:
        skinning = master_vertex_indices_in_strand[1]

        skinning <<= 5
        skinning |= master_vertex_indices_in_strand[0]

        skinning <<= 6
        skinning |= int(weight * 63)

        return skinning

class _SlaveVertexSkinningResult(NamedTuple):
    master_vertex_indices_in_strand: tuple[int, int]
    weight: float

class _SlaveStrandSkinningResult(NamedTuple):
    master_strand_indices: tuple[int, int]
    weight: float
    vertices: list[_SlaveVertexSkinningResult]

class _SlaveStrandSkinningCalculator:
    class _PartInfo(NamedTuple):
        master_vertex_lookup: SpatialIndex[_HairMasterVertexDescriptor]
        first_master_vertex_idx: int

    hair: Hair
    master_strands: list[_HairStrandDescriptor]
    master_vertices: list[_HairMasterVertexDescriptor]

    part_infos: list[_PartInfo]

    def __init__(self, hair: Hair, master_strands: list[_HairStrandDescriptor], master_vertices: list[_HairMasterVertexDescriptor]) -> None:
        self.hair = hair
        self.master_strands = master_strands
        self.master_vertices = master_vertices

        self.part_infos = []
        master_vertex_idx = 0
        for part in hair.parts:
            master_vertex_lookup = SpatialIndex[_HairMasterVertexDescriptor](1)
            self.part_infos.append(_SlaveStrandSkinningCalculator._PartInfo(master_vertex_lookup, master_vertex_idx))
            for master_vertex in part.master_strands.points:
                master_vertex_lookup.add(master_vertex.position, master_vertices[master_vertex_idx])
                master_vertex_idx += 1

    def calculate(self, slave_strand: _HairStrandReference) -> _SlaveStrandSkinningResult:
        master_strand_indices, slave_vertex_search_states = self.choose_master_strands(slave_strand)
        return self.calc_skinning_using_master_strands(slave_strand, master_strand_indices, slave_vertex_search_states)

    def choose_master_strands(self, slave_strand: _HairStrandReference) -> tuple[tuple[int, int], list[SpatialIndex.SearchState[_HairMasterVertexDescriptor]]]:
        search_states: list[SpatialIndex.SearchState[_HairMasterVertexDescriptor]] = []
        master_strand_votes: tuple[dict[int, int], dict[int, int]] = ({}, {})

        master_vertex_lookup = self.part_infos[slave_strand.part_idx].master_vertex_lookup
        part_slave_vertices = self.hair.parts[slave_strand.part_idx].slave_strands.points
        for slave_vertex_idx_in_strand in range(slave_strand.num_vertices):
            slave_vertex_pos = part_slave_vertices[slave_strand.first_vertex_idx_in_part + slave_vertex_idx_in_strand].position
            master_strand_indices, search_state = master_vertex_lookup.find_nearby_items_mapped(slave_vertex_pos, tuple[int, int], self.get_two_different_master_strands)
            if master_strand_indices is None:
                if len(search_state.items) == 0:
                    raise Exception("No master strands found for slave strand")

                master_strand_indices = (search_state.items[0].strand_idx, search_state.items[0].strand_idx)

            self.vote_for_master_strand(master_strand_votes[0], master_strand_indices[0])
            self.vote_for_master_strand(master_strand_votes[1], master_strand_indices[1])
            search_states.append(search_state)

        chosen_master_strand_indices = (
            self.get_winning_master_strand(master_strand_votes[0]),
            self.get_winning_master_strand(master_strand_votes[1])
        )
        return (chosen_master_strand_indices, search_states)

    def get_two_different_master_strands(self, nearby_master_vertices: list[_HairMasterVertexDescriptor]) -> tuple[int, int] | None:
        if len(nearby_master_vertices) < 2:
            return None

        for i in range(1, len(nearby_master_vertices)):
            if nearby_master_vertices[i].strand_idx != nearby_master_vertices[0].strand_idx:
                return (nearby_master_vertices[0].strand_idx, nearby_master_vertices[i].strand_idx)

        return None

    def vote_for_master_strand(self, votes: dict[int, int], master_strand_idx: int) -> None:
        vote_count = coalesce(votes.get(master_strand_idx), 0)
        votes[master_strand_idx] = vote_count + 1

    def get_winning_master_strand(self, votes: dict[int, int]) -> int:
        winning_master_strand_idx: int | None = None
        max_vote_count: int | None = None
        for master_strand_idx, vote_count in votes.items():
            if max_vote_count is None or vote_count > max_vote_count:
                winning_master_strand_idx = master_strand_idx
                max_vote_count = vote_count

        if winning_master_strand_idx is None:
            raise Exception("No master strands found")

        return winning_master_strand_idx

    def calc_skinning_using_master_strands(
        self,
        slave_strand: _HairStrandReference,
        master_strand_indices: tuple[int, int],
        slave_vertex_search_states: list[SpatialIndex.SearchState[_HairMasterVertexDescriptor]]
    ) -> _SlaveStrandSkinningResult:
        part_slave_vertices = self.hair.parts[slave_strand.part_idx].slave_strands.points
        master_vertex_lookup = self.part_infos[slave_strand.part_idx].master_vertex_lookup

        inter_strand_weights: list[float] = []
        vertex_skinnings: list[_SlaveVertexSkinningResult] = []

        for slave_vertex_idx_in_strand in range(slave_strand.num_vertices):
            slave_vertex_pos = part_slave_vertices[slave_strand.first_vertex_idx_in_part + slave_vertex_idx_in_strand].position
            master_vertex_indices_in_strand, _ = master_vertex_lookup.find_nearby_items_mapped(
                slave_vertex_pos,
                tuple[int, int],
                lambda nearby_master_vertices: self.get_two_master_vertices_of_strand(nearby_master_vertices, master_strand_indices[0]),
                slave_vertex_search_states[slave_vertex_idx_in_strand]
            )
            if master_vertex_indices_in_strand is None:
                master_vertex_indices_in_strand = (0, 0)

            master_strand_0_vertex_0_pos = self.get_master_vertex_position(master_strand_indices[0], master_vertex_indices_in_strand[0])
            master_strand_1_vertex_0_pos = self.get_master_vertex_position(master_strand_indices[1], master_vertex_indices_in_strand[0])
            inter_strand_weights.append(self.get_weight_between_points(master_strand_0_vertex_0_pos, slave_vertex_pos, master_strand_1_vertex_0_pos))

            master_strand_0_vertex_1_pos = self.get_master_vertex_position(master_strand_indices[0], master_vertex_indices_in_strand[1])
            inter_vertex_weight = self.get_weight_between_points(master_strand_0_vertex_0_pos, slave_vertex_pos, master_strand_0_vertex_1_pos)
            vertex_skinnings.append(_SlaveVertexSkinningResult(master_vertex_indices_in_strand, inter_vertex_weight))

        return _SlaveStrandSkinningResult(master_strand_indices, Enumerable(inter_strand_weights).avg(), vertex_skinnings)

    def get_two_master_vertices_of_strand(self, nearby_master_vertices: list[_HairMasterVertexDescriptor], master_strand_idx: int) -> tuple[int, int] | None:
        vertex_ids_in_strand: list[int] = []
        for master_vertex in nearby_master_vertices:
            if master_vertex.strand_idx != master_strand_idx:
                continue

            vertex_ids_in_strand.append(master_vertex.index_in_strand)
            if len(vertex_ids_in_strand) < 2:
                continue

            return (vertex_ids_in_strand[0], vertex_ids_in_strand[1])

        return None

    def get_master_vertex_position(self, strand_idx: int, vertex_idx_in_strand: int) -> Vector:
        strand = self.master_strands[strand_idx]
        part = self.hair.parts[strand.group_idx]
        part_info = self.part_infos[strand.group_idx]
        return part.master_strands.points[strand.first_vertex_idx + vertex_idx_in_strand - part_info.first_master_vertex_idx].position

    def get_weight_between_points(self, left_position: Vector, query_position: Vector, right_position: Vector) -> float:
        left_to_query = query_position - left_position
        left_to_right = right_position - left_position
        square_distance = left_to_right.length_squared
        if square_distance < 0.00001:
            return 0

        return min(max(left_to_query.dot(left_to_right) / square_distance, 0), 1)
