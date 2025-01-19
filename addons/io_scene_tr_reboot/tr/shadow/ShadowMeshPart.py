from ctypes import sizeof
from typing import TYPE_CHECKING, Literal, Sequence
from mathutils import Vector
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.util.CStruct import CArray, CFlag, CInt, CLong, CShort, CStruct64

class ShadowMeshPart(CStruct64, IMeshPart if TYPE_CHECKING else object):
    center: Vector
    first_index_idx: CInt
    num_triangles: CInt
    num_vertices: CInt
    flags: CInt
    draw_group_id: CInt
    order: CInt
    actual_pg: CInt
    lod_level: CShort
    field_2E: CShort
    material_idx: CLong
    texture_indices: CArray[CLong, Literal[5]]              # type: ignore

    has_8_weights_per_vertex = CFlag("flags", 0x8000)       # type: ignore
    has_16bit_skin_indices   = CFlag("flags", 0x10000)      # type: ignore

    indices: Sequence[int]

    _ignored_fields_ = ("indices",)

assert(sizeof(ShadowMeshPart) == 0x60)
