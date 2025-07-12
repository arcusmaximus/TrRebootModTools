from enum import IntEnum
from mathutils import Quaternion, Vector
from typing import Protocol
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder

class BoneConstraintType(IntEnum):
    LOOK_AT = 0
    WEIGHTED_POSITION = 1
    WEIGHTED_ROTATION = 2
    FROM_BLEND_SHAPES = 3

class IBoneConstraint(Protocol):
    type: BoneConstraintType
    target_bone_local_id: int
    source_bone_local_ids: list[int]
    source_bone_weights: list[float]

    def write(self, writer: ResourceBuilder) -> None: ...

    def apply_bone_local_id_changes(self, mapping: dict[int, int]) -> None: ...

    def serialize(self) -> str: ...

class IBoneConstraint_LookAt(IBoneConstraint, Protocol):
    pole_bone_local_id: int | None
    pole_bone_orientation: Quaternion | None
    pole_dir: Vector | None
    bone_local_tangent: Vector
    bone_local_normal: Vector

class IBoneConstraint_WeightedPosition(IBoneConstraint, Protocol):
    offset: Vector

class IBoneConstraint_WeightedRotation(IBoneConstraint, Protocol):
    offset: Quaternion

class IBoneConstraint_FromBlendShapes(IBoneConstraint, Protocol):
    position_offsets: list[Vector] | None
    rotation_offsets: list[Quaternion] | None
    source_blend_shape_ids: list[int]
