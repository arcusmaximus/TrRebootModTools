from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from io_scene_tr_reboot.tr.Model import ILodLevel
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CFloat, CInt, CStruct64
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class ShadowLodLevel(CStruct64, ILodLevel if TYPE_CHECKING else object):
    relative_pivot: CVec3
    num_children: CInt
    sub_tree_size: CInt
    lod_mode: CInt
    min: CFloat
    max: CFloat
    min_fade: CFloat
    max_fade: CFloat
    max_screen_size: CFloat
    max_opaque_screen_size: CFloat
    min_opaque_screen_size: CFloat
    min_screen_size: CFloat
    flags: CByte
    lod_start: CByte
    lod_end: CByte
    padding: CArray[CByte, Literal[5]]

assert(sizeof(ShadowLodLevel) == 0x40)
