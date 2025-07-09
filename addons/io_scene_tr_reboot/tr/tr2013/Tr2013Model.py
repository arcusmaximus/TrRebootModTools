from typing import TypeVar, cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import ILodLevel, Model
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.tr.ModelReferences import ModelReferences
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.tr2013.Tr2013LodLevel import Tr2013LodLevel
from io_scene_tr_reboot.tr.tr2013.Tr2013Mesh import ITr2013Mesh, Tr2013Mesh
from io_scene_tr_reboot.tr.tr2013.Tr2013MeshPart import Tr2013MeshPart
from io_scene_tr_reboot.tr.tr2013.Tr2013ModelDataHeader import Tr2013ModelDataHeader
from io_scene_tr_reboot.tr.tr2013.Tr2013ModelReferences import Tr2013ModelReferences
from io_scene_tr_reboot.util.CStruct import CStruct
from io_scene_tr_reboot.util.Enumerable import Enumerable

TModelReferences = TypeVar("TModelReferences", bound = ModelReferences)
TModelDataHeader = TypeVar("TModelDataHeader", bound = IModelDataHeader)
TLodLevel = TypeVar("TLodLevel", bound = ILodLevel)
TMesh = TypeVar("TMesh", bound = ITr2013Mesh)
TMeshPart = TypeVar("TMeshPart", bound = IMeshPart)
class Tr2013ModelBase(Model[TModelReferences, TModelDataHeader, TLodLevel, TMesh, TMeshPart]):
    def read(self, reader: ResourceReader) -> None:
        self.refs.read(reader)
        if self.refs.model_data_resource is None:
            return

        reader.seek(self.refs.model_data_resource)
        model_data_header_pos = reader.position
        self.header = self.read_header(reader)

        reader.position = model_data_header_pos + self.header.lod_levels_offset
        self.lod_levels = self.read_lod_levels(reader, self.header.num_lod_levels)

        blend_shape_names: list[str] | None = None
        if self.header.blend_shape_names_offset != 0:
            reader.position = model_data_header_pos + self.header.blend_shape_names_offset
            blend_shape_names = self.read_blend_shape_names(reader, self.header.num_blend_shapes)

        reader.position = model_data_header_pos + self.header.mesh_headers_offset
        for _ in range(self.header.num_meshes):
            mesh = self.create_mesh()
            mesh.read(reader, model_data_header_pos, blend_shape_names)
            self.meshes.append(mesh)

        reader.position = model_data_header_pos + self.header.index_data_offset
        indices = reader.read_uint16_list(self.header.num_indexes)

        reader.position = model_data_header_pos + self.header.mesh_parts_offet
        mesh_idx: int = 0
        for _ in range(self.header.num_mesh_parts):
            mesh = self.meshes[mesh_idx]
            mesh_part = self.read_mesh_part(reader)
            mesh_part.indices = indices[mesh_part.first_index_idx:mesh_part.first_index_idx + mesh_part.num_primitives * (2 if mesh_part.is_hair else 3)]

            mesh.parts.append(mesh_part)
            if len(mesh.parts) == mesh.mesh_header.num_parts:
                mesh_idx = mesh_idx + 1

    def create_mesh(self) -> TMesh: ...

    def read_header(self, reader: ResourceReader) -> TModelDataHeader: ...

    def read_lod_levels(self, reader: ResourceReader, count: int) -> list[TLodLevel]: ...

    def read_blend_shape_names(self, reader: ResourceReader, num_blend_shapes: int) -> list[str]:
        blend_shape_names: list[str] = []
        for _ in range(num_blend_shapes):
            name_data = reader.read_bytes(0x40)
            name_length = len(name_data)
            while name_length > 1 and name_data[name_length - 1] == 0:
                name_length -= 1

            name_data = name_data[0:name_length]
            name = name_data.tobytes().decode()
            blend_shape_names.append(name)

        return blend_shape_names

    def read_mesh_part(self, reader: ResourceReader) -> TMeshPart: ...

    def write(self, writer: ResourceBuilder) -> None:
        if self.refs.model_data_resource is None:
            raise Exception()

        self.refs.write(writer)
        writer.align(0x20)

        model_data_header_pos = writer.position
        self.refs.model_data_resource = ResourceReference(ResourceType.MODEL, self.refs.model_data_resource.id, model_data_header_pos)
        self.header.signature = int.from_bytes(b"Mesh", "little")
        if self.header.num_bones == 0:
            self.header.num_bones = Enumerable(self.meshes).select_many(lambda m: m.bone_indices).max(default_value = -1) + 1

        self.header.num_blend_shapes = len(self.meshes[0].blend_shapes)
        self.header.num_meshes = len(self.meshes)
        self.header.num_mesh_parts = Enumerable(self.meshes).sum(lambda m: len(m.parts))
        self.header.num_indexes = Enumerable(self.meshes).select_many(lambda m: m.parts).sum(lambda p: len(p.indices))

        if Enumerable(self.meshes).any(lambda m: m.vertex_format.has_attribute(Hashes.skin_indices)):
            self.header.model_type = 1

        if self.header.num_lod_levels != 0 or \
           self.header.pre_tesselation_info_offset != 0xFFFFFFFF:
            raise NotImplementedError()

        self.update_bounding_box()
        writer.write_struct(cast(CStruct, self.header))

        self.header.local_bone_ids_offset = writer.position - model_data_header_pos
        writer.write_int32_list(range(self.header.num_bones))
        writer.align(0x20)

        self.header.mesh_headers_offset = writer.position - model_data_header_pos
        for mesh in self.meshes:
            mesh.write_header(writer)

        for mesh in self.meshes:
            mesh.write_bone_indices(writer, model_data_header_pos)

        for mesh in self.meshes:
            mesh.write_content(writer, model_data_header_pos)

        # Rewrite mesh headers now that they have the content offsets
        meshes_end_pos = writer.position
        writer.position = model_data_header_pos + self.header.mesh_headers_offset
        for mesh in self.meshes:
            mesh.write_header(writer)

        writer.position = meshes_end_pos

        self.header.index_data_offset = writer.position - model_data_header_pos
        cumulative_index_count = 0
        for mesh in self.meshes:
            for mesh_part in mesh.parts:
                mesh_part.first_index_idx = cumulative_index_count
                mesh_part.num_primitives = len(mesh_part.indices) // (2 if mesh_part.is_hair else 3)
                writer.write_uint16_list(mesh_part.indices)

                cumulative_index_count += len(mesh_part.indices)
        writer.align(0x20)

        self.header.mesh_parts_offet = writer.position - model_data_header_pos
        for mesh in self.meshes:
            for mesh_part in mesh.parts:
                writer.write_struct(cast(CStruct, mesh_part))

        # Rewrite model header
        writer.position = 0
        self.refs.write(writer)
        writer.align(0x20)
        writer.write_struct(cast(CStruct, self.header))

class Tr2013Model(Tr2013ModelBase[Tr2013ModelReferences, Tr2013ModelDataHeader, Tr2013LodLevel, Tr2013Mesh, Tr2013MeshPart]):
    def __init__(self, model_id: int, model_data_id: int) -> None:
        super().__init__(model_id, Tr2013ModelReferences(model_data_id))
        self.header = Tr2013ModelDataHeader()
        self.header.num_blend_shapes = 0
        self.header.bound_sphere_center = Vector()
        self.header.bound_box_min = Vector()
        self.header.bound_box_max = Vector()
        self.header.flags = 0xE00
        self.header.pre_tesselation_info_offset = 0xFFFFFFFF

    def create_mesh(self) -> Tr2013Mesh:
        return Tr2013Mesh(self.header)

    def read_header(self, reader: ResourceReader) -> Tr2013ModelDataHeader:
        header = reader.read_struct(Tr2013ModelDataHeader)
        header.num_blend_shapes = 0
        header.blend_shape_names_offset = 0
        return header

    def read_lod_levels(self, reader: ResourceReader, count: int) -> list[Tr2013LodLevel]:
        return reader.read_struct_list(Tr2013LodLevel, count)

    def read_mesh_part(self, reader: ResourceReader) -> Tr2013MeshPart:
        mesh_part = reader.read_struct(Tr2013MeshPart)
        mesh_part.lod_level = 0
        return mesh_part
