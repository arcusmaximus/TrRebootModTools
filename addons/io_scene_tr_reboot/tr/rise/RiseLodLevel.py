from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from io_scene_tr_reboot.tr.Model import ILodLevel
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CFloat, CInt, CStruct64
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class RiseLodLevel(CStruct64, ILodLevel if TYPE_CHECKING else object):
    relative_pivot: CVec3
    num_children: CInt
    sub_tree_size: CInt
    min: CFloat
    max: CFloat
    min_fade: CFloat
    max_fade: CFloat
    inv_min_delta: CFloat
    inv_max_delta: CFloat
    flags: CByte
    padding: CArray[CByte, Literal[3]]

assert(sizeof(RiseLodLevel) == 0x30)
