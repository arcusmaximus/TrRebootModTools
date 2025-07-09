from typing import Protocol
from mathutils import Vector

class IModelDataHeader(Protocol):
    signature:                  int
    flags:                      int
    num_indexes:                int
    bound_box_min:              Vector
    bound_box_max:              Vector
    bound_sphere_center:        Vector
    bound_sphere_radius:        float
    min_lod:                    float
    max_lod:                    float
    has_vertex_weights:         bool
    has_blend_shapes:           bool

    model_type:                 int

    mesh_parts_offet:           int
    mesh_headers_offset:        int
    local_bone_ids_offset:      int
    lod_levels_offset:          int
    index_data_offset:          int

    num_mesh_parts:             int
    num_meshes:                 int
    num_bones:                  int
    num_lod_levels:             int

    bone_usage_map:             list[int]

    num_blend_shapes:           int
    blend_shape_names_offset:   int

    pre_tesselation_info_offset: int
