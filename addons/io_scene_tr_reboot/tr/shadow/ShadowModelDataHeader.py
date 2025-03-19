from ctypes import sizeof
from typing import TYPE_CHECKING, Literal
from mathutils import Vector
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.util.CStruct import CArray, CFlag, CFloat, CInt, CLong, CShort, CStruct64

class ShadowModelDataHeader(CStruct64, IModelDataHeader if TYPE_CHECKING else object):
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
    min_screen_size_lod:        CFloat
    max_screen_size_lod:        CFloat
    min_shadow_screen_size_lod: CFloat
    lod_mode:                   CInt
    model_type:                 CInt
    sort_bias:                  CFloat
    bone_usage_map:             CArray[CInt, Literal[32]]       # type: ignore
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
    name_length:                CInt
    field_134:                  CInt
    name_offset:                CLong
    num_blend_shapes:           CInt
    field_144:                  CInt
    blend_shape_names_offset:   CLong
    auto_bump_scale:            CFloat
    field_154:                  CInt
    field_158:                  CInt
    field_15C:                  CInt

    has_vertex_weights = CFlag("flags", 1)          # type: ignore
    has_blend_shapes   = CFlag("flags", 0x4000)     # type: ignore

assert(sizeof(ShadowModelDataHeader) == 0x160)
