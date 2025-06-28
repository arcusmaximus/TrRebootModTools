from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from io_scene_tr_reboot.tr.Model import ILodLevel
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CFloat, CInt, CStruct32
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class Tr2013LodLevel(CStruct32, ILodLevel if TYPE_CHECKING else object):
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

assert(sizeof(Tr2013LodLevel) == 0x30)
