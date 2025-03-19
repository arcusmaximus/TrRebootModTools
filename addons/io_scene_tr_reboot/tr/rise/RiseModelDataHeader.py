from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from mathutils import Vector
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.util.CStruct import CArray, CFlag, CFloat, CInt, CLong, CShort, CStruct64

class RiseModelDataHeader(CStruct64, IModelDataHeader if TYPE_CHECKING else object):
    signature:                  CInt
    flags:                      CInt
    total_data_size:            CInt
    num_indexes:                CInt
    bound_sphere_center:        Vector
    bound_box_min:              Vector
    bound_box_max:              Vector
    position_scale_offset:      Vector
    bound_sphere_radius:        CFloat
    min_lod:                    CFloat
    max_lod:                    CFloat
    max_shadow_lod:             CFloat
    model_type:                 CInt
    sort_bias:                  CFloat
    bone_usage_map:             CArray[CInt, Literal[8]]        # type: ignore
    mesh_parts_offet:           CLong
    mesh_headers_offset:        CLong
    local_bone_ids_offset:      CLong
    lod_levels_offset:          CLong
    index_data_offset:          CLong
    num_mesh_parts:             CShort
    num_meshes:                 CShort
    num_bones:                  CShort
    num_lod_levels:             CShort
    pre_tesselation_info_offset:    CLong
    name_offset:                CLong
    num_blend_shapes:           CInt
    field_CC:                   CInt
    blend_shape_names_offset:   CLong
    auto_bump_scale:            CFloat
    field_DC:                   CInt

    has_vertex_weights = CFlag("flags", 1)          # type: ignore
    has_blend_shapes   = CFlag("flags", 0x4000)     # type: ignore

assert(sizeof(RiseModelDataHeader) == 0xE0)
