from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from mathutils import Vector
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.util.CStruct import CArray, CFlag, CFloat, CInt, CShort, CStruct32, CUInt

class Tr2013ModelDataHeader(CStruct32, IModelDataHeader if TYPE_CHECKING else object):
    signature:                      CInt
    flags:                          CInt
    total_data_size:                CInt
    num_indexes:                    CInt
    bound_sphere_center:            Vector
    bound_box_min:                  Vector
    bound_box_max:                  Vector
    bound_sphere_radius:            CFloat
    min_lod:                        CFloat
    max_lod:                        CFloat
    model_type:                     CInt
    sort_bias:                      CFloat
    bone_usage_map:                 CArray[CInt, Literal[8]]        # type: ignore
    mesh_parts_offet:               CInt
    mesh_headers_offset:            CInt
    local_bone_ids_offset:          CInt
    lod_levels_offset:              CInt
    index_data_offset:              CInt
    num_mesh_parts:                 CShort
    num_meshes:                     CShort
    num_bones:                      CShort
    num_lod_levels:                 CShort
    pre_tesselation_info_offset:    CUInt
    name_offset:                    CInt

    has_vertex_weights = CFlag("flags", 1)          # type: ignore
    has_blend_shapes   = CFlag("flags", 0x4000)     # type: ignore

    num_blend_shapes:               int
    blend_shape_names_offset:       int
    _ignored_fields_ = ("num_blend_shapes", "blend_shape_names_offset")

assert(sizeof(Tr2013ModelDataHeader) == 0x98)
