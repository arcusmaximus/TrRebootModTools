from mathutils import Vector
from typing import MutableSequence, Protocol, Sequence

class IMeshPart(Protocol):
    center: Vector
    first_index_idx: int
    num_triangles: int

    indices: Sequence[int]
    texture_indices: MutableSequence[int]
    material_idx: int
    lod_level: int
    draw_group_id: int
    flags: int
    is_hair: bool
    has_8_weights_per_vertex: bool
    has_16bit_skin_indices: bool
