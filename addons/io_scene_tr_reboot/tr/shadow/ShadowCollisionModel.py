from ctypes import sizeof
from enum import IntEnum
from typing import Literal, cast
from mathutils import Vector
from io_scene_tr_reboot.tr.CollisionModel import CollisionBoundingBoxNode, CollisionFace, CollisionMesh, CollisionModel
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CShort, CStruct64, CUInt, CUShort
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class _VertexType(IntEnum):
    INT16 = 0
    FLOAT32 = 1
    FLOAT16 = 2

class _Face(CStruct64):
    indices: CArray[CUInt, Literal[3]]
    adjacency_flags: CByte
    material_idx: CByte
    padding: CShort

assert(sizeof(_Face) == 0x10)

class _Material(CStruct64):
    collision_flags: CShort
    client_flags: CShort
    surface_material_id: CUInt
    material_id: CUInt

assert(sizeof(_Material) == 0xC)

class _BoundingBoxNode(CStruct64):
    min: CVec3
    max: CVec3
    first_face_or_relative_second_child_idx: CUShort
    num_faces: CByte
    exponent: CByte

assert(sizeof(_BoundingBoxNode) == 0x1C)

class _Mesh(CStruct64):
    offset: Vector
    vertices_ref: ResourceReference | None
    faces_ref: ResourceReference | None
    materials_ref: ResourceReference | None
    bounding_box_nodes_ref: ResourceReference | None
    num_bounding_box_nodes: CUInt
    num_faces: CUInt
    num_vertices: CUInt
    num_degenerate_faces: CUInt
    num_non_manifold_edges: CUInt
    vertex_type: CUShort
    height: CUShort
    num_materials: CByte
    padding: CArray[CByte, Literal[7]]

assert(sizeof(_Mesh) == 0x50)

class _MeshInfo(CStruct64):
    mesh_ref: ResourceReference | None
    collision_type_ref: ResourceReference | None

assert(sizeof(_MeshInfo) == 0x10)

class ShadowCollisionModel(CollisionModel):
    def read(self, reader: ResourceReader) -> None:
        num_meshes = reader.read_int32()
        reader.skip(4)
        for mesh_info in reader.read_struct_list(_MeshInfo, num_meshes):
            if mesh_info.mesh_ref is None:
                continue

            reader.seek(mesh_info.mesh_ref)
            mesh = self.read_mesh(reader)
            mesh.collision_type_id = mesh_info.collision_type_ref.id if mesh_info.collision_type_ref is not None else None
            self.meshes.append(mesh)

    def read_mesh(self, reader: ResourceReader) -> CollisionMesh:
        mesh_struct = reader.read_struct(_Mesh)
        mesh = CollisionMesh()

        if mesh_struct.vertices_ref is not None:
            if mesh_struct.vertex_type != _VertexType.FLOAT32:
                raise Exception("Collision mesh uses an unsupported vertex type")

            reader.seek(mesh_struct.vertices_ref)
            for vertex in reader.read_struct_list(CVec3, mesh_struct.num_vertices):
                mesh.vertices.append(vertex.to_vector())

        if mesh_struct.faces_ref is not None:
            reader.seek(mesh_struct.faces_ref)
            for face in reader.read_struct_list(_Face, mesh_struct.num_faces):
                mesh.faces.append(CollisionFace(list(face.indices), face.material_idx))

        if mesh_struct.materials_ref is not None:
            reader.seek(mesh_struct.materials_ref)
            for material in reader.read_struct_list(_Material, mesh_struct.num_materials):
                mesh.material_ids.append(material.surface_material_id)

        if mesh_struct.bounding_box_nodes_ref is not None:
            reader.seek(mesh_struct.bounding_box_nodes_ref)
            node_structs = reader.read_struct_list(_BoundingBoxNode, mesh_struct.num_bounding_box_nodes)
            nodes: list[CollisionBoundingBoxNode] = []
            for node_struct in node_structs:
                nodes.append(
                    CollisionBoundingBoxNode(
                        node_struct.min.to_vector(),
                        node_struct.max.to_vector(),
                        [],
                        node_struct.first_face_or_relative_second_child_idx if node_struct.num_faces > 0 else -1,
                        node_struct.num_faces
                    )
                )
            for i in range(len(nodes)):
                if node_structs[i].num_faces == 0:
                    nodes[i].children.append(nodes[i + 1])
                    nodes[i].children.append(nodes[i + node_structs[i].first_face_or_relative_second_child_idx])

            mesh.root_bounding_box_node = nodes[0]

        return mesh

    def write(self, writer: ResourceBuilder) -> None:
        writer.write_int32(len(self.meshes))
        writer.align(8)

        mesh_infos: list[_MeshInfo] = []
        for mesh in self.meshes:
            mesh_info = _MeshInfo()
            mesh_info.mesh_ref = writer.make_internal_ref()
            if mesh.collision_type_id is not None:
                mesh_info.collision_type_ref = ResourceReference(ResourceType.DTP, mesh.collision_type_id, 0)
            else:
                mesh_info.collision_type_ref = None

            mesh_infos.append(mesh_info)
            writer.write_struct(mesh_info)

        for i, mesh in enumerate(self.meshes):
            cast(ResourceReference, mesh_infos[i].mesh_ref).offset = writer.position
            self.write_mesh(mesh, writer)

    def write_mesh(self, mesh: CollisionMesh, writer: ResourceBuilder) -> None:
        mesh_struct = _Mesh()
        bounding_box_node_structs = self.make_bounding_box_node_structs(mesh)

        mesh_struct.offset = Vector()
        mesh_struct.vertex_type = _VertexType.FLOAT32

        mesh_struct.vertices_ref            = writer.make_internal_ref()
        mesh_struct.faces_ref               = writer.make_internal_ref()
        mesh_struct.materials_ref           = writer.make_internal_ref()
        mesh_struct.bounding_box_nodes_ref  = writer.make_internal_ref()

        mesh_struct.num_vertices            = len(mesh.vertices)
        mesh_struct.num_faces               = len(mesh.faces)
        mesh_struct.num_materials           = len(mesh.material_ids)
        mesh_struct.num_bounding_box_nodes  = len(bounding_box_node_structs)

        writer.write_struct(mesh_struct)

        mesh_struct.vertices_ref.offset = writer.position
        writer.write_vec3d_list(mesh.vertices)

        mesh_struct.faces_ref.offset = writer.position
        for face in mesh.faces:
            face_struct = _Face()
            for i, index in enumerate(face.indices):
                face_struct.indices[i] = index

            face_struct.material_idx = face.material_idx
            writer.write_struct(face_struct)

        mesh_struct.materials_ref.offset = writer.position
        for material_id in mesh.material_ids:
            material_struct = _Material()
            material_struct.collision_flags = 3
            material_struct.surface_material_id = material_id
            writer.write_struct(material_struct)

        mesh_struct.bounding_box_nodes_ref.offset = writer.position
        writer.write_struct_list(bounding_box_node_structs)

    def make_bounding_box_node_structs(self, mesh: CollisionMesh) -> list[_BoundingBoxNode]:
        node_structs: list[_BoundingBoxNode] = []
        if mesh.root_bounding_box_node is None:
            return node_structs

        def add_node(node: CollisionBoundingBoxNode) -> None:
            node_struct = _BoundingBoxNode()
            node_index = len(node_structs)
            node_structs.append(node_struct)

            node_struct.min = CVec3.from_vector(node.min)
            node_struct.max = CVec3.from_vector(node.max)
            if len(node.children) > 0:
                if len(node.children) != 2:
                    raise Exception("Non-leaf bounding box nodes must have exactly 2 children")

                add_node(node.children[0])
                node_struct.first_face_or_relative_second_child_idx = len(node_structs) - node_index
                add_node(node.children[1])
            else:
                node_struct.first_face_or_relative_second_child_idx = node.first_face_idx
                node_struct.num_faces = node.num_faces

        add_node(mesh.root_bounding_box_node)
        return node_structs
