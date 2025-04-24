from ctypes import sizeof
from typing import TYPE_CHECKING, Literal, Sequence
from mathutils import Vector
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.util.CStruct import CArray, CFlag, CInt, CStruct32

class Tr2013MeshPart(CStruct32, IMeshPart if TYPE_CHECKING else object):
    center: Vector
    first_index_idx: CInt
    num_primitives: CInt
    num_vertices: CInt
    flags: CInt
    draw_group_id: CInt
    order: CInt
    actual_pg: CInt
    pad0: CInt
    pad1: CInt
    pad2: CInt
    material_idx: CInt
    texture_indices: CArray[CInt, Literal[5]]               # type: ignore

    is_hair                  = CFlag("flags", 0x200)        # type: ignore
    has_8_weights_per_vertex = CFlag("flags", 0x8000)       # type: ignore
    has_16bit_skin_indices   = CFlag("flags", 0x10000)      # type: ignore

    lod_level: int
    indices: Sequence[int]

    _ignored_fields_ = ("lod_level", "indices",)

assert(sizeof(Tr2013MeshPart) == 0x50)
