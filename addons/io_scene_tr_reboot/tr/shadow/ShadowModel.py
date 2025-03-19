from mathutils import Vector
from io_scene_tr_reboot.tr.Model import Model
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.shadow.ShadowMesh import ShadowMesh
from io_scene_tr_reboot.tr.shadow.ShadowMeshPart import ShadowMeshPart
from io_scene_tr_reboot.tr.shadow.ShadowModelDataHeader import ShadowModelDataHeader
from io_scene_tr_reboot.tr.shadow.ShadowModelReferences import ShadowModelReferences
from io_scene_tr_reboot.util.Enumerable import Enumerable

class ShadowModel(Model[ShadowModelReferences, ShadowModelDataHeader, ShadowMesh, ShadowMeshPart]):
    def __init__(self, model_id: int, refs: ShadowModelReferences) -> None:
        super().__init__(model_id, refs)
        self.header = ShadowModelDataHeader()
        self.header.bound_sphere_center = Vector()
        self.header.bound_box_min = Vector()
        self.header.bound_box_max = Vector()
        self.header.position_scale_offset = Vector()
        self.header.flags = 0xE00
        self.header.pre_tesselation_info_offset = 0xFFFFFFFF

    def read(self, reader: ResourceReader) -> None:
        self.header = reader.read_struct(ShadowModelDataHeader)
        reader.skip(self.header.name_length)
        reader.align(0x20)

        if self.header.pre_tesselation_info_offset != 0xFFFFFFFF:
            raise NotImplementedError()

        reader.skip(self.header.num_lod_levels * 0x40)
        reader.skip(self.header.num_bones * 4)
        reader.align(0x20)

        for _ in range(self.header.num_meshes):
            mesh = ShadowMesh(self.header)
            mesh.read_header(reader)
            self.meshes.append(mesh)

        for mesh in self.meshes:
            mesh.read_bone_indices(reader)

        for mesh in self.meshes:
            mesh.read_content(reader)

        if self.header.blend_shape_names_offset != 0:
            reader.skip(self.header.num_blend_shapes * 0x40)

        indices = reader.read_uint16_list(self.header.num_indexes)
        reader.align(0x20)

        mesh_idx: int = 0
        for _ in range(self.header.num_mesh_parts):
            mesh: ShadowMesh = self.meshes[mesh_idx]
            mesh_part: ShadowMeshPart = reader.read_struct(ShadowMeshPart)
            mesh_part.indices = indices[mesh_part.first_index_idx:mesh_part.first_index_idx + mesh_part.num_triangles * 3]

            mesh.parts.append(mesh_part)
            if len(mesh.parts) == mesh.mesh_header.num_parts:
                mesh_idx = mesh_idx + 1

        if reader.position != len(reader.data):
            raise Exception("Unread data remaining at end of file")

    def write(self, writer: ResourceBuilder) -> None:
        self.header.signature = int.from_bytes(b"Mesh", "little")
        self.header.num_bones = Enumerable(self.meshes).select_many(lambda m: m.bone_indices).max(default_value = -1) + 1
        self.header.num_blend_shapes = len(self.meshes[0].blend_shapes)
        self.header.num_meshes = len(self.meshes)
        self.header.num_mesh_parts = Enumerable(self.meshes).sum(lambda m: len(m.parts))
        self.header.num_indexes = Enumerable(self.meshes).select_many(lambda m: m.parts).sum(lambda p: len(p.indices))

        if self.header.num_bones > 0:
            self.header.model_type = 1

        writer.write_struct(self.header)

        if self.header.name_length != 0 or \
           self.header.num_lod_levels != 0 or \
           self.header.pre_tesselation_info_offset != 0xFFFFFFFF:
            raise NotImplementedError()

        writer.write_int32_list(range(self.header.num_bones))
        writer.align(0x20)

        for mesh in self.meshes:
            mesh.write_header(writer)

        for mesh in self.meshes:
            mesh.write_bone_indices(writer)

        for mesh in self.meshes:
            mesh.write_content(writer)

        cumulative_index_count = 0
        for mesh in self.meshes:
            for mesh_part in mesh.parts:
                mesh_part.first_index_idx = cumulative_index_count
                mesh_part.num_triangles = len(mesh_part.indices) // 3
                writer.write_uint16_list(mesh_part.indices)

                cumulative_index_count += len(mesh_part.indices)
        writer.align(0x20)

        for mesh in self.meshes:
            for mesh_part in mesh.parts:
                writer.write_struct(mesh_part)