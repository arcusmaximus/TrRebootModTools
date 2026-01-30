from array import array
import random
from typing import Iterable, NamedTuple, Sequence, cast
import bpy
import os
import re
from mathutils import Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderBoneIdSet, BlenderModelIdSet, BlenderNaming
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.MaterialProperties import MaterialProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties, ObjectSkeletonProperties
from io_scene_tr_reboot.tr.BlendShape import BlendShape
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.Vertex import Vertex
from io_scene_tr_reboot.tr.VertexFormat import VertexFormat
from io_scene_tr_reboot.tr.VertexOffsets import VertexOffsets
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.IoHelper import IoHelper
from io_scene_tr_reboot.util.SlotsBase import SlotsBase
from io_scene_tr_reboot.util.SpatialIndex import SpatialIndex

class _BlenderMeshMaps(NamedTuple):
    color_maps: dict[int, bpy.types.ByteColorAttribute]
    uv_maps: dict[int, bpy.types.MeshUVLoopLayer]
    vertex_groups: dict[int, bpy.types.VertexGroup]

class _VertexWeight:
    bone_id: int
    weight: int

    def __init__(self, bone_id: int, weight: int) -> None:
        self.bone_id = bone_id
        self.weight = weight

class ModelExporter(SlotsBase):
    scale_factor: float
    game: CdcGame
    factory: IFactory

    bl_context: bpy.types.Context

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game
        self.factory = Factories.get(game)
        self.bl_context = bpy.context

    def export_model(self, folder_path: str, ids: BlenderModelIdSet, bl_mesh_objs: list[bpy.types.Object], bl_armature_obj: bpy.types.Object | None) -> None:
        self.validate_blender_objects(bl_mesh_objs)

        global_bone_ids: dict[int, int | None] | None = None
        global_blend_shape_ids: dict[int, int] | None = None
        if bl_armature_obj is not None:
            bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
            global_bone_ids = Enumerable(bl_armature.bones).select(lambda b: BlenderNaming.try_parse_bone_name(b.name)) \
                                                           .of_type(BlenderBoneIdSet) \
                                                           .to_dict(lambda ids: ids.local_id or 0, lambda ids: ids.global_id)
            global_blend_shape_ids = ObjectSkeletonProperties.get_global_blend_shape_ids(bl_armature_obj)

        tr_model = self.create_model(ids.model_id, ids.model_data_id, bl_mesh_objs, global_bone_ids, global_blend_shape_ids)

        model_data_file_path = os.path.join(folder_path, f"{ids.model_data_id}.tr{self.game}modeldata")
        with IoHelper.open_write(model_data_file_path) as model_data_file:
            resource_builder = ResourceBuilder(ResourceKey(ResourceType.MODEL, ids.model_data_id), self.game)
            tr_model.write(resource_builder)
            model_data_file.write(resource_builder.build())

        self.export_extra_files(folder_path, ids.object_id, tr_model)

    def validate_blender_objects(self, bl_objs: list[bpy.types.Object]) -> None:
        pass

    def export_extra_files(self, folder_path: str, object_id: int, tr_model: IModel) -> None:
        pass

    def create_model(self, model_id: int, model_data_id: int, bl_objs: list[bpy.types.Object], global_bone_ids: dict[int, int | None] | None, blend_shape_global_ids: dict[int, int] | None) -> IModel:
        tr_model = self.factory.create_model(model_id, model_data_id)
        tr_model.refs.material_resources = Enumerable(bl_objs).select(lambda o: o.data)                                          \
                                                              .cast(bpy.types.Mesh)                                              \
                                                              .select_many(lambda m: m.materials)                                \
                                                              .of_type(bpy.types.Material)                                       \
                                                              .select(lambda m: BlenderNaming.parse_material_name(m.name))       \
                                                              .distinct()                                                        \
                                                              .select(lambda i: ResourceKey(ResourceType.MATERIAL, i))           \
                                                              .cast(ResourceKey | None)                                          \
                                                              .to_list()

        tr_model.header.max_lod = 3.402823e+38
        tr_model.header.has_vertex_weights = Enumerable(bl_objs).any(lambda o: len(o.vertex_groups) > 0)
        tr_model.header.num_bones = len(global_bone_ids) if global_bone_ids is not None else 0
        self.apply_bone_usage_map(tr_model, bl_objs)

        bl_shape_keys = Enumerable(bl_objs).select(lambda o: o.data)        \
                                           .cast(bpy.types.Mesh)            \
                                           .select_many(lambda m: (m.shape_keys and Enumerable(m.shape_keys.key_blocks).skip(1) or []))
        if bl_shape_keys.any():
            tr_model.header.has_blend_shapes = True
            tr_model.header.num_blend_shapes = bl_shape_keys.max(lambda s: self.get_shape_key_local_id(s, blend_shape_global_ids)) + 1

        blend_shape_normals_source_file_path: str | None = None
        for bl_obj in bl_objs:
            tr_mesh = self.create_mesh(tr_model, bl_obj, blend_shape_global_ids)
            if tr_mesh is None:
                continue

            tr_model.meshes.append(tr_mesh)
            properties = ObjectProperties.get_instance(bl_obj)
            if properties.blend_shape_normals_source_file_path:
                blend_shape_normals_source_file_path = properties.blend_shape_normals_source_file_path

        self.unsign_normals(tr_model)
        self.calc_bounding_box(tr_model)

        if blend_shape_normals_source_file_path:
            self.transfer_blend_shape_normals(tr_model, blend_shape_normals_source_file_path)

        return tr_model

    def apply_bone_usage_map(self, tr_model: IModel, bl_mesh_objs: list[bpy.types.Object]) -> None:
        max_bones = len(tr_model.header.bone_usage_map) * 32

        for bl_vertex_group in Enumerable(bl_mesh_objs).select_many(lambda o: o.vertex_groups):
            local_id = BlenderNaming.try_get_bone_local_id(bl_vertex_group.name)
            if local_id is None:
                continue

            if local_id >= max_bones:
                raise Exception(f"Model references too many bones (can have a maximum of {max_bones})")

            tr_model.header.bone_usage_map[local_id // 32] |= 1 << (local_id & 0x1F)

    def create_mesh(self, tr_model: IModel, bl_obj: bpy.types.Object, blend_shape_global_ids: dict[int, int] | None) -> IMesh | None:
        with BlenderHelper.prepare_for_model_export(bl_obj):
            if not Enumerable(bl_obj.modifiers).of_type(bpy.types.TriangulateModifier).any():
                bl_modifier = cast(bpy.types.TriangulateModifier, bl_obj.modifiers.new("Triangulate", "TRIANGULATE"))
                bl_modifier.keep_custom_normals = True

            bl_mesh = self.get_evaluated_bl_mesh(bl_obj)
            if len(bl_mesh.polygons) == 0:
                return None

            if hasattr(bl_mesh, "calc_normals_split"):
                getattr(bl_mesh, "calc_normals_split")()

            if len(bl_mesh.uv_layers) > 0:
                bl_mesh.calc_tangents()

            bl_mesh_maps = _BlenderMeshMaps(
                self.collect_bl_color_maps(bl_obj, bl_mesh),
                self.collect_bl_uv_maps(bl_obj, bl_mesh),
                self.collect_bl_vertex_groups(bl_obj)
            )

            tr_mesh = self.factory.create_mesh(tr_model)
            tr_mesh.bone_indices = list(bl_mesh_maps.vertex_groups.keys())
            use_8_weights_per_vertex = tr_mesh.vertex_format.types.weightsuhalf4 is not None and len(tr_mesh.bone_indices) - 1 <= 0xFF

            self.populate_vertex_format(tr_mesh.vertex_format, bl_mesh_maps)
            bl_corner_to_tr_vertex = self.create_vertices(tr_mesh, bl_mesh, bl_mesh_maps, use_8_weights_per_vertex)
            self.create_mesh_parts(tr_model, tr_mesh, bl_obj, bl_mesh, bl_corner_to_tr_vertex, use_8_weights_per_vertex)
            self.create_blend_shapes(tr_model, tr_mesh, bl_obj, bl_mesh, bl_corner_to_tr_vertex, blend_shape_global_ids)
            self.shrink_uvs(tr_mesh, bl_mesh_maps)

            return tr_mesh

    def collect_bl_color_maps(self, bl_obj: bpy.types.Object, bl_mesh: bpy.types.Mesh) -> dict[int, bpy.types.ByteColorAttribute]:
        attr_name_hashes = [Hashes.color1, Hashes.color2]
        if len(bl_mesh.color_attributes) > len(attr_name_hashes):
            raise Exception(f"Mesh {bl_obj.name} has more than {len(attr_name_hashes)} color attributes, which is not supported.")

        bl_color_maps: dict[int, bpy.types.ByteColorAttribute] = {}
        for i, bl_color_map in enumerate(bl_mesh.color_attributes):
            if not isinstance(bl_color_map, bpy.types.ByteColorAttribute):
                raise Exception(f"Color attribute {i} in mesh {bl_obj.name} must use the Byte Color format. Please convert or delete it.")

            if bl_color_map.domain != "POINT":
                raise Exception(f"Color attribute {bl_color_map.name} in mesh {bl_obj.name} must use the Vertex domain. Please convert or delete it.")

            bl_color_maps[attr_name_hashes[i]] = bl_color_map

        return bl_color_maps

    def collect_bl_uv_maps(self, bl_obj: bpy.types.Object, bl_mesh: bpy.types.Mesh) -> dict[int, bpy.types.MeshUVLoopLayer]:
        attr_name_hashes = [Hashes.texcoord1, Hashes.texcoord2, Hashes.texcoord3, Hashes.texcoord4]
        if len(bl_mesh.uv_layers) > len(attr_name_hashes):
            raise Exception(f"Mesh {bl_obj.name} has more than {len(attr_name_hashes)} UV maps, which is not supported.")

        bl_uv_maps: dict[int, bpy.types.MeshUVLoopLayer] = {}
        for i, bl_uv_map in enumerate(bl_mesh.uv_layers):
            bl_uv_maps[attr_name_hashes[i]] = bl_uv_map

        return bl_uv_maps

    def collect_bl_vertex_groups(self, bl_obj: bpy.types.Object) -> dict[int, bpy.types.VertexGroup]:
        return Enumerable(bl_obj.vertex_groups).to_dict(lambda g: BlenderNaming.get_bone_local_id(g.name))

    def populate_vertex_format(self, tr_vertex_format: VertexFormat, bl_mesh_maps: _BlenderMeshMaps) -> None:
        tr_vertex_format.hash = random.randint(0, 0xFFFFFFFFFFFFFFFF)
        tr_vertex_format.add_attribute(Hashes.position, tr_vertex_format.types.float3, 0)
        tr_vertex_format.add_attribute(Hashes.normal,   tr_vertex_format.types.vectorc32, 0)
        tr_vertex_format.add_attribute(Hashes.binormal, tr_vertex_format.types.vectorc32, 0)
        tr_vertex_format.add_attribute(Hashes.tangent,  tr_vertex_format.types.vectorc32, 0)

        for attr_name_hash in bl_mesh_maps.color_maps.keys():
            tr_vertex_format.add_attribute(attr_name_hash, tr_vertex_format.types.color32, 0)

        for attr_name_hash in bl_mesh_maps.uv_maps.keys():
            tr_vertex_format.add_attribute(attr_name_hash, tr_vertex_format.types.texcoords2, 0)

        if len(bl_mesh_maps.vertex_groups) > 0:
            tr_vertex_format.add_attribute(Hashes.skin_weights, tr_vertex_format.types.weightsuhalf4 or tr_vertex_format.types.weightsc32, 0)
            tr_vertex_format.add_attribute(Hashes.skin_indices, tr_vertex_format.types.ushort4 or tr_vertex_format.types.indicesc32, 0)

    def create_vertices(self, tr_mesh: IMesh, bl_mesh: bpy.types.Mesh, bl_mesh_maps: _BlenderMeshMaps, use_8_weights_per_vertex: bool) -> list[int]:
        bl_vertex_to_tr_vertices: list[list[int] | None] = [None] * len(bl_mesh.vertices)
        bl_corner_to_tr_vertex: list[int] = [-1] * len(bl_mesh.loops)
        for bl_corner_idx, bl_corner in enumerate(bl_mesh.loops):
            bl_vertex_idx = bl_corner.vertex_index
            tr_vertex_idxs = bl_vertex_to_tr_vertices[bl_vertex_idx]
            if tr_vertex_idxs is None:
                tr_vertex_idxs = []
                bl_vertex_to_tr_vertices[bl_vertex_idx] = tr_vertex_idxs

            tr_vertex_idx = -1
            for existing_tr_vertex_idx in tr_vertex_idxs:
                if self.can_reuse_tr_vertex_for_bl_corner(tr_mesh.vertices[existing_tr_vertex_idx], bl_mesh, bl_mesh_maps, bl_corner_idx):
                    tr_vertex_idx = existing_tr_vertex_idx
                    break

            if tr_vertex_idx < 0:
                tr_mesh.vertices.append(self.create_vertex(bl_mesh, bl_mesh_maps, bl_corner_idx, use_8_weights_per_vertex))
                tr_vertex_idx = len(tr_mesh.vertices) - 1
                tr_vertex_idxs.append(tr_vertex_idx)

            bl_corner_to_tr_vertex[bl_corner_idx] = tr_vertex_idx

        return bl_corner_to_tr_vertex

    def can_reuse_tr_vertex_for_bl_corner(self, tr_vertex: Vertex, bl_mesh: bpy.types.Mesh, bl_mesh_maps: _BlenderMeshMaps, bl_corner_idx: int) -> bool:
        bl_corner = bl_mesh.loops[bl_corner_idx]

        if not self.are_vectors_equal(tr_vertex.attributes[Hashes.normal], bl_corner.normal):
            return False

        for attr_name_hash, bl_uv_map in bl_mesh_maps.uv_maps.items():
            if not self.are_vectors_equal(tr_vertex.attributes[attr_name_hash], bl_uv_map.data[bl_corner_idx].uv):
                return False

        return True

    def create_vertex(self, bl_mesh: bpy.types.Mesh, bl_mesh_maps: _BlenderMeshMaps, bl_corner_idx: int, use_8_weights_per_vertex: bool) -> Vertex:
        bl_corner = bl_mesh.loops[bl_corner_idx]
        bl_vertex_idx = bl_corner.vertex_index
        bl_vertex = bl_mesh.vertices[bl_vertex_idx]

        tr_vertex = Vertex()
        tr_vertex.attributes[Hashes.position] = cast(tuple[float, ...], bl_vertex.co / self.scale_factor)
        tr_vertex.attributes[Hashes.normal]   = cast(tuple[float, ...], bl_corner.normal.copy())
        tr_vertex.attributes[Hashes.binormal] = cast(tuple[float, ...], -bl_corner.bitangent)
        tr_vertex.attributes[Hashes.tangent]  = cast(tuple[float, ...], -bl_corner.tangent)

        for attr_name_hash, bl_color_map in bl_mesh_maps.color_maps.items():
            tr_vertex.attributes[attr_name_hash] = tuple(cast(Sequence[float], bl_color_map.data[bl_vertex_idx].color))

        for attr_name_hash, bl_uv_map in bl_mesh_maps.uv_maps.items():
            uv_coord = bl_uv_map.data[bl_corner_idx].uv
            if abs(uv_coord.x) > 15 or abs(uv_coord.y) > 15:
                raise Exception(f"Mesh {bl_mesh.name} has out-of-bounds UV coordinates. Please check and fix UV map {bl_uv_map.name}. U and V coordinates should be in the range [-15..15].")

            tr_vertex.attributes[attr_name_hash] = cast(tuple[float, ...], uv_coord.copy())

        if len(bl_mesh_maps.vertex_groups) == 0:
            return tr_vertex

        skin_index_count = use_8_weights_per_vertex and 8 or 4

        vertex_weights = Enumerable(bl_vertex.groups).select(lambda w: _VertexWeight(w.group, int(w.weight * 255))) \
                                                     .where(lambda w: w.weight > 0)                                 \
                                                     .order_by_descending(lambda w: w.weight)                       \
                                                     .take(skin_index_count)                                        \
                                                     .to_list()

        if len(vertex_weights) > 0:
            weight_sum = Enumerable(vertex_weights).sum(lambda w: w.weight)
            vertex_weights[0].weight += 255 - weight_sum
            if vertex_weights[0].weight < 0:
                raise Exception(f"Mesh {bl_mesh.name} has overweighted vertices. Please normalize the vertex weights.")

        skin_indices = [0, 0, 0, 0]
        skin_weights = [0, 0, 0, 0]
        weight_idx = 0

        for bl_vertex_weight in vertex_weights:
            skin_indices[weight_idx % 4] |= bl_vertex_weight.bone_id << (8 * (weight_idx // 4))
            skin_weights[weight_idx % 4] |= bl_vertex_weight.weight  << (8 * (weight_idx // 4))
            weight_idx += 1

        tr_vertex.attributes[Hashes.skin_indices] = tuple(skin_indices)
        tr_vertex.attributes[Hashes.skin_weights] = tuple(skin_weights)

        return tr_vertex

    def create_mesh_parts(self, tr_model: IModel, tr_mesh: IMesh, bl_obj: bpy.types.Object, bl_mesh: bpy.types.Mesh, bl_corner_to_tr_vertex: list[int], use_8_weights_per_vertex: bool) -> None:
        if len(bl_mesh.materials) == 0:
            raise Exception(f"Mesh {bl_obj.name} has no materials assigned. Please add at least one material.")

        props = ObjectProperties.get_instance(bl_obj).mesh

        bl_faces_by_material_idx: dict[int, list[bpy.types.MeshPolygon]] = Enumerable(bl_mesh.polygons).group_by(lambda p: p.material_index)
        for bl_material_idx, bl_faces in bl_faces_by_material_idx.items():
            bl_material = bl_mesh.materials[bl_material_idx]
            if bl_material is None:
                raise Exception(f"Mesh {bl_obj.name} has faces referencing an empty material slot. Please populate or delete any such slots.")

            tr_material_id = BlenderNaming.parse_material_name(bl_material.name)
            double_sided = MaterialProperties.get_instance(bl_material).double_sided

            tr_mesh_part = self.factory.create_mesh_part()
            tr_mesh_part.center = Vector()
            tr_mesh_part.draw_group_id = props.draw_group_id
            tr_mesh_part.flags = props.flags
            tr_mesh_part.has_8_weights_per_vertex = use_8_weights_per_vertex
            tr_mesh_part.has_16bit_skin_indices = not use_8_weights_per_vertex

            tr_mesh_part.material_idx = Enumerable(tr_model.refs.material_resources).index_of(lambda m: m is not None and m.id == tr_material_id)
            for i in range(len(tr_mesh_part.texture_indices)):
                tr_mesh_part.texture_indices[i] = 0xFFFFFFFF

            num_single_side_indices = len(bl_faces) * 3
            tr_mesh_part.indices = array("H", [0]) * (num_single_side_indices * (2 if double_sided else 1))
            tr_index_idx = 0
            for bl_face in bl_faces:
                if bl_face.loop_total != 3:
                    raise Exception(f"Mesh {bl_obj.name} contains non-triangular faces. Please triangulate manually or with a Triangulate modifier.")

                for bl_corner_idx in cast(Iterable[int], bl_face.loop_indices):
                    tr_vertex_idx = bl_corner_to_tr_vertex[bl_corner_idx]
                    if tr_vertex_idx > 0xFFFF:
                        raise Exception(f"Mesh {bl_obj.name} has too many vertices (maximum is 65536). Please decimate or split it.")

                    tr_mesh_part.indices[tr_index_idx] = tr_vertex_idx
                    tr_index_idx += 1

            if double_sided:
                for tr_index_idx in range(0, len(bl_faces) * 3, 3):
                    tr_mesh_part.indices[num_single_side_indices + tr_index_idx + 0] = tr_mesh_part.indices[tr_index_idx + 2]
                    tr_mesh_part.indices[num_single_side_indices + tr_index_idx + 1] = tr_mesh_part.indices[tr_index_idx + 1]
                    tr_mesh_part.indices[num_single_side_indices + tr_index_idx + 2] = tr_mesh_part.indices[tr_index_idx + 0]

            tr_mesh.parts.append(tr_mesh_part)

    def create_blend_shapes(self, tr_model: IModel, tr_mesh: IMesh, bl_obj: bpy.types.Object, bl_mesh: bpy.types.Mesh, bl_corner_to_tr_vertex: list[int], blend_shape_global_ids: dict[int, int] | None) -> None:
        tr_mesh.blend_shapes = [None] * tr_model.header.num_blend_shapes
        if bl_mesh.shape_keys is None:
            return

        tr_vertex_to_bl_corner: list[int] = [-1] * len(tr_mesh.vertices)
        for bl_corner_idx, tr_vertex_idx in enumerate(bl_corner_to_tr_vertex):
            tr_vertex_to_bl_corner[tr_vertex_idx] = bl_corner_idx

        base_vertex_positions = [0] * (len(bl_mesh.vertices) * 3)
        bl_mesh.vertices.foreach_get("co", base_vertex_positions)

        base_corner_normals = [0] * (len(bl_mesh.loops) * 3)
        bl_mesh.loops.foreach_get("normal", base_corner_normals)

        shape_vertex_positions = [0] * (len(bl_mesh.vertices) * 3)
        shape_corner_normals = [0] * (len(bl_mesh.loops) * 3)

        bl_obj.show_only_shape_key = True

        if hasattr(bl_mesh, "use_auto_smooth"):
            setattr(bl_mesh, "use_auto_smooth", False)

        color_offset = Vector((0.0, 0.0, 0.4, 0.0))                         # Z component = normal blending suppression strength
        for bl_shape_key_idx in range(1, len(bl_mesh.shape_keys.key_blocks)):
            bl_obj.active_shape_key_index = bl_shape_key_idx
            tr_blend_shape_idx = self.get_shape_key_local_id(cast(bpy.types.ShapeKey, bl_obj.active_shape_key), blend_shape_global_ids)

            bl_mesh = self.get_evaluated_bl_mesh(bl_obj)
            if hasattr(bl_mesh, "calc_normals_split"):
                getattr(bl_mesh, "calc_normals_split")()

            bl_mesh.vertices.foreach_get("co", shape_vertex_positions)
            bl_mesh.loops.foreach_get("normal", shape_corner_normals)

            tr_blend_shape = BlendShape()
            for tr_vertex_idx, bl_corner_idx in enumerate(tr_vertex_to_bl_corner):
                bl_vertex_idx = bl_mesh.loops[bl_corner_idx].vertex_index

                base_position = (
                    base_vertex_positions[3 * bl_vertex_idx + 0],
                    base_vertex_positions[3 * bl_vertex_idx + 1],
                    base_vertex_positions[3 * bl_vertex_idx + 2]
                )
                base_normal = (
                    base_corner_normals[3 * bl_corner_idx + 0],
                    base_corner_normals[3 * bl_corner_idx + 1],
                    base_corner_normals[3 * bl_corner_idx + 2]
                )
                bl_shape_vertex = bl_mesh.vertices[bl_vertex_idx]
                bl_shape_corner = bl_mesh.loops[bl_corner_idx]

                if self.are_vectors_equal(base_position, bl_shape_vertex.co): # and \
                   #self.are_vectors_equal(base_normal, bl_shape_corner.normal):
                    continue

                bl_shape_corner = bl_mesh.loops[bl_corner_idx]
                tr_blend_shape.vertices[tr_vertex_idx] = VertexOffsets(
                    (bl_shape_vertex.co - Vector(base_position)) / self.scale_factor,
                    bl_shape_corner.normal - Vector(base_normal),
                    color_offset
                )

            if len(tr_blend_shape.vertices) > 0:
                tr_mesh.blend_shapes[tr_blend_shape_idx] = tr_blend_shape

        bl_obj.show_only_shape_key = False

    def are_vectors_equal(self, a: Vector | tuple[float, ...], b: Vector | tuple[float, ...]) -> bool:
        for i in range(len(a)):
            if abs(a[i] - b[i]) > 0.0001:
                return False

        return True

    def unsign_normals(self, tr_model: IModel) -> None:
        for tr_mesh in tr_model.meshes:
            for tr_vertex in tr_mesh.vertices:
                self.unsign_vector(tr_vertex, Hashes.normal)
                self.unsign_vector(tr_vertex, Hashes.binormal)
                self.unsign_vector(tr_vertex, Hashes.tangent)

    def unsign_vector(self, tr_vertex: Vertex, attr_name_hash: int) -> None:
        vector = tr_vertex.attributes.get(attr_name_hash)
        if vector is None:
            return

        tr_vertex.attributes[attr_name_hash] = (
                                                   min(max((vector[0] + 1.0) / 2.0, 0.0), 1.0),
                                                   min(max((vector[1] + 1.0) / 2.0, 0.0), 1.0),
                                                   min(max((vector[2] + 1.0) / 2.0, 0.0), 1.0),
                                                   0.0
                                               )

    def shrink_uvs(self, tr_mesh: IMesh, bl_mesh_maps: _BlenderMeshMaps) -> None:
        for tr_vertex in tr_mesh.vertices:
            for attr_name_hash in bl_mesh_maps.uv_maps:
                vector = tr_vertex.attributes[attr_name_hash]
                tr_vertex.attributes[attr_name_hash] = (vector[0] / 16.0, (1 - vector[1]) / 16.0)

    def calc_bounding_box(self, tr_model: IModel) -> None:
        x_min: float = 0.0
        y_min: float = 0.0
        z_min: float = 0.0

        x_max: float = 0.0
        y_max: float = 0.0
        z_max: float = 0.0

        first: bool = True

        for tr_mesh in tr_model.meshes:
            for tr_vertex in tr_mesh.vertices:
                position = tr_vertex.attributes[Hashes.position]
                if first:
                    (x_min, y_min, z_min) = position
                    (x_max, y_max, z_max) = position
                    first = False
                else:
                    x_min = min(x_min, position[0])
                    y_min = min(y_min, position[1])
                    z_min = min(z_min, position[2])

                    x_max = max(x_max, position[0])
                    y_max = max(y_max, position[1])
                    z_max = max(z_max, position[2])

        tr_model.header.bound_box_min = Vector((x_min, y_min, z_min))
        tr_model.header.bound_box_max = Vector((x_max, y_max, z_max))

    def get_evaluated_bl_mesh(self, bl_obj: bpy.types.Object) -> bpy.types.Mesh:
        bl_obj_eval = bl_obj.evaluated_get(self.bl_context.evaluated_depsgraph_get())
        bl_mesh_eval = cast(bpy.types.Mesh, bl_obj_eval.data)
        return bl_mesh_eval

    def get_shape_key_local_id(self, bl_shape_key: bpy.types.ShapeKey, blend_shape_global_ids: dict[int, int] | None) -> int:
        id_set = BlenderNaming.parse_shape_key_name(bl_shape_key.name)
        if id_set.global_id is None or blend_shape_global_ids is None:
            return id_set.local_id

        mapping = Enumerable(blend_shape_global_ids.items()).first_or_none(lambda p: p[1] == id_set.global_id)
        if mapping is None:
            raise Exception(f"No mapping found for global blend shape ID {id_set.global_id}. Please use a matching skeleton, fix the global ID of shape key {bl_shape_key.name}, or delete the shape key.")

        return mapping[0]

    def transfer_blend_shape_normals(self, tr_target_model: IModel, source_file_path: str) -> None:
        tr_source_model = self.load_model(source_file_path)
        if tr_source_model is None:
            OperatorContext.log_warning(f"Couldn't load shape key source file {source_file_path} - skipping transfer.")
            return

        tr_source_mesh = Enumerable(tr_source_model.meshes).order_by_descending(lambda m: Enumerable(m.blend_shapes).count(lambda b: b is not None)).first_or_none()
        if tr_source_mesh is None:
            return

        source_vertex_lookup = SpatialIndex[int](0.001, False)
        for source_vertex_idx in Enumerable(tr_source_mesh.parts).select_many(lambda p: p.indices).distinct():  # Skip loose vertices
            source_vertex_pos = Vector(tr_source_mesh.vertices[source_vertex_idx].attributes[Hashes.position])
            source_vertex_lookup.add(source_vertex_pos, source_vertex_idx)

        for tr_target_mesh in tr_target_model.meshes:
            if not Enumerable(tr_target_mesh.blend_shapes).any(lambda b: b is not None):
                continue

            target_vertex_idx_by_source_vertex_idx: list[int | None] = [None] * len(tr_source_mesh.vertices)
            for target_vertex_idx, tr_target_vertex in enumerate(tr_target_mesh.vertices):
                target_vertex_pos = Vector(tr_target_vertex.attributes[Hashes.position])
                source_vertex_idx = source_vertex_lookup.find_nearest_item(target_vertex_pos)
                if source_vertex_idx is not None:
                    target_vertex_idx_by_source_vertex_idx[source_vertex_idx] = target_vertex_idx
                    tr_source_vertex = tr_source_mesh.vertices[source_vertex_idx]
                    tr_target_vertex.attributes[Hashes.normal] = tr_source_vertex.attributes[Hashes.normal]

            for blend_shape_idx, tr_target_blend_shape in enumerate(tr_target_mesh.blend_shapes):
                if tr_target_blend_shape is None or blend_shape_idx >= len(tr_source_mesh.blend_shapes):
                    continue

                tr_source_blend_shape = tr_source_mesh.blend_shapes[blend_shape_idx]
                if tr_source_blend_shape is None:
                    continue

                for source_vertex_idx, source_vertex_offsets in tr_source_blend_shape.vertices.items():
                    target_vertex_idx = target_vertex_idx_by_source_vertex_idx[source_vertex_idx]
                    if target_vertex_idx is None:
                        continue

                    tr_target_blend_shape.vertices[target_vertex_idx] = source_vertex_offsets

    def load_model(self, file_path: str) -> IModel | None:
        if not os.path.isfile(file_path):
            return None

        match = re.match(r".tr(\d+)model", os.path.splitext(file_path)[1])
        if match is None:
            return None

        game = int(match.group(1))
        if game < 9 or game > 11:
            return None

        game = CdcGame(game)

        tr_model = Factories.get(game).create_model(0, 0)
        with IoHelper.open_read(file_path) as source_file:
            has_references = game != CdcGame.SOTTR
            tr_model.read(ResourceReader(ResourceKey(ResourceType.MODEL, 0), source_file.read(), has_references, self.game))

        return tr_model
