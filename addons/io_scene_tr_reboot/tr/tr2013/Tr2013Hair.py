from array import array
from typing import cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.Hair import Hair, HairPart, HairPoint, HairPointWeight, HairStrandGroup
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.Vertex import Vertex
from io_scene_tr_reboot.tr.VertexFormat import VertexFormat
from io_scene_tr_reboot.tr.tr2013.Tr2013Mesh import Tr2013Mesh
from io_scene_tr_reboot.tr.tr2013.Tr2013MeshPart import Tr2013MeshPart
from io_scene_tr_reboot.tr.tr2013.Tr2013Model import Tr2013Model
from io_scene_tr_reboot.tr.tr2013.Tr2013VertexAttributeTypes import Tr2013VertexAttributeTypes
from io_scene_tr_reboot.util.Enumerable import Enumerable

class Tr2013Hair(Hair):
    def __init__(self, model_id: int, hair_data_id: int) -> None:
        super().__init__(model_id, hair_data_id)

    @property
    def supports_strand_thickness(self) -> bool:
        return True

    def read(self, reader: ResourceReader) -> None:
        model = Tr2013Model(self.model_id or 0, self.hair_data_id)
        model.read(reader)
        self.from_model(model)

    def write(self, writer: ResourceBuilder) -> None:
        model = self.to_model()
        model.write(writer)

    def from_model(self, model: Tr2013Model) -> None:
        mesh = model.meshes[0]
        for mesh_part in mesh.parts:
            hair_part = HairPart(str(len(self.parts)))

            hair_part_points = cast(list[HairPoint], [None] * len(mesh_part.indices))
            for i, index in enumerate(mesh_part.indices):
                vertex = mesh.vertices[index]
                hair_part_points[i] = HairPoint(
                    Vector(vertex.attributes[Hashes.position]),
                    vertex.attributes[Hashes.thickness][0],
                    [
                        HairPointWeight(0, 1 - vertex.attributes[Hashes.invmass][0])
                    ]
                )

            strand_point_counts = [16] * (len(hair_part_points) // 16)

            hair_part.slave_strands = HairStrandGroup(hair_part_points, strand_point_counts)
            self.parts.append(hair_part)

        if len(model.refs.material_resources) > 0:
            material_resource = model.refs.material_resources[0]
            if material_resource is not None:
                self.material_id = material_resource.id

    def to_model(self) -> Tr2013Model:
        model = Tr2013Model(self.model_id or 0, self.hair_data_id)
        if self.material_id is not None:
            model.refs.material_resources.append(ResourceKey(ResourceType.MATERIAL, self.material_id))

        model.header.flags = 0x1E80
        model.header.max_lod = 3.402823e+38

        mesh = self.create_mesh(model)
        index_base = 0
        for hair_part in self.parts:
            mesh.parts.append(self.create_mesh_part(hair_part, index_base))
            index_base += len(hair_part.slave_strands.points)

        model.meshes.append(mesh)

        return model

    def create_mesh(self, model: Tr2013Model) -> Tr2013Mesh:
        mesh = Tr2013Mesh(model.header)

        vertex_format = VertexFormat(Tr2013VertexAttributeTypes.instance)
        vertex_format.add_attribute(Hashes.position,    vertex_format.types.float3, 0)
        vertex_format.add_attribute(Hashes.tangent,     vertex_format.types.float3, 0)
        vertex_format.add_attribute(Hashes.thickness,   vertex_format.types.float1, 0)
        vertex_format.add_attribute(Hashes.invmass,     vertex_format.types.float1, 0)
        vertex_format.add_attribute(Hashes.global_rot,  vertex_format.types.float4, 0)
        vertex_format.add_attribute(Hashes.local_rot,   vertex_format.types.float4, 0)
        vertex_format.add_attribute(Hashes.refvecs,     vertex_format.types.float3, 0)
        mesh.vertex_format = vertex_format

        mesh.vertices = cast(list[Vertex], [None] * Enumerable(self.parts).sum(lambda p: len(p.slave_strands.points)))
        vertex_idx = 0
        z_axis = Vector((0, 0, 1))
        for hair_part in self.parts:
            for count in hair_part.slave_strands.strand_point_counts:
                if count != 16:
                    raise Exception("All strands must have exactly 16 points")

            for i, point in enumerate(hair_part.slave_strands.points):
                normal = self.get_point_normal(hair_part.slave_strands, i)
                rotation = z_axis.rotation_difference(normal)

                vertex = Vertex()
                vertex.attributes[Hashes.position]   = point.position.to_tuple()
                vertex.attributes[Hashes.tangent]    = normal.to_tuple()
                vertex.attributes[Hashes.thickness]  = (point.thickness,)
                vertex.attributes[Hashes.invmass]    = (1 - point.weights[0].weight if len(point.weights) > 0 else 1,)
                vertex.attributes[Hashes.global_rot] = (rotation.x, rotation.y, rotation.z, rotation.w)
                vertex.attributes[Hashes.local_rot]  = (rotation.x, rotation.y, rotation.z, rotation.w)
                vertex.attributes[Hashes.refvecs]    = normal.to_tuple()
                mesh.vertices[vertex_idx] = vertex
                vertex_idx += 1

        return mesh

    def get_point_normal(self, group: HairStrandGroup, index: int) -> Vector:
        index_in_strand = index & 0xF
        prev_pos: Vector
        cur_pos: Vector
        next_pos: Vector
        if index_in_strand == 0:
            prev_pos = group.points[index    ].position
            cur_pos  = group.points[index + 1].position
            next_pos = group.points[index + 2].position
        elif index_in_strand == 15:
            prev_pos = group.points[index - 2].position
            cur_pos  = group.points[index - 1].position
            next_pos = group.points[index    ].position
        else:
            prev_pos = group.points[index - 1].position
            cur_pos  = group.points[index    ].position
            next_pos = group.points[index + 1].position

        normal = cast(Vector, (prev_pos - cur_pos).cross(next_pos - cur_pos))
        if normal.length_squared > 0:
            normal.normalize()
        else:
            normal = Vector((0, 0, 1))

        return normal

    def create_mesh_part(self, hair_part: HairPart, index_base: int) -> Tr2013MeshPart:
        mesh_part = Tr2013MeshPart()
        mesh_part.center = Vector()
        mesh_part.flags = 0x2B2

        mesh_part.num_vertices = len(hair_part.slave_strands.points)
        mesh_part.indices = array("H", [0]) * len(hair_part.slave_strands.points)
        for i in range(len(hair_part.slave_strands.points)):
            mesh_part.indices[i] = index_base + i

        for i in range(len(mesh_part.texture_indices)):
            mesh_part.texture_indices[i] = 0xFFFFFFFF

        return mesh_part
