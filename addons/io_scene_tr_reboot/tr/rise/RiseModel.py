from mathutils import Vector
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.rise.RiseLodLevel import RiseLodLevel
from io_scene_tr_reboot.tr.rise.RiseMesh import RiseMesh
from io_scene_tr_reboot.tr.rise.RiseMeshPart import RiseMeshPart
from io_scene_tr_reboot.tr.rise.RiseModelDataHeader import RiseModelDataHeader
from io_scene_tr_reboot.tr.rise.RiseModelReferences import RiseModelReferences
from io_scene_tr_reboot.tr.tr2013.Tr2013Model import Tr2013ModelBase

class RiseModel(Tr2013ModelBase[RiseModelReferences, RiseModelDataHeader, RiseLodLevel, RiseMesh, RiseMeshPart]):
    def __init__(self, model_id: int) -> None:
        super().__init__(model_id, RiseModelReferences(model_id))
        self.header = RiseModelDataHeader()
        self.header.bound_sphere_center = Vector()
        self.header.bound_box_min = Vector()
        self.header.bound_box_max = Vector()
        self.header.position_scale_offset = Vector()
        self.header.flags = 0xE00
        self.header.pre_tesselation_info_offset = 0xFFFFFFFF

    def create_mesh(self) -> RiseMesh:
        return RiseMesh(self.header)

    def read_header(self, reader: ResourceReader) -> RiseModelDataHeader:
        return reader.read_struct(RiseModelDataHeader)

    def read_lod_levels(self, reader: ResourceReader, count: int) -> list[RiseLodLevel]:
        return reader.read_struct_list(RiseLodLevel, count)

    def read_mesh_part(self, reader: ResourceReader) -> RiseMeshPart:
        return reader.read_struct(RiseMeshPart)
