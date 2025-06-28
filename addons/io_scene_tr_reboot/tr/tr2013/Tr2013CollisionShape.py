from ctypes import sizeof
from typing import Protocol, cast
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionCapsule, CollisionSphere, CollisionShapeType
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.util.CStruct import CFloat, CInt, CShort, CStruct, CStruct32
from io_scene_tr_reboot.util.Conditional import coalesce

class _CollisionBone(CStruct32):
    type: CShort
    local_bone_idx: CShort

assert(sizeof(_CollisionBone) == 4)

class _CollisionShape(CStruct32):
    type: CInt
    constraint_type: CInt
    bone1: _CollisionBone
    bone2: _CollisionBone
    radius: CFloat

assert(sizeof(_CollisionShape) == 0x14)

class _ITr2013CollisionShape(Protocol):
    def assign_from_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None: ...
    def assign_to_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None: ...

class Tr2013CollisionShape(CollisionShape):
    @classmethod
    def read(cls, reader: ResourceReader, index: int, skeleton_id: int, global_bone_ids: list[int | None]) -> CollisionShape:
        struct = reader.read_struct(_CollisionShape)
        collision = cls.create(CollisionShapeType(struct.type), skeleton_id, index)
        collision.global_bone_id = cls._convert_dtp_bone_id_to_global(struct.bone1.local_bone_idx, global_bone_ids)
        cast(_ITr2013CollisionShape, collision).assign_from_struct(struct, global_bone_ids)
        return collision

    @classmethod
    def to_struct(cls, collision: CollisionShape, global_bone_ids: list[int | None]) -> CStruct:
        struct = _CollisionShape()
        struct.bone1 = _CollisionBone()
        struct.bone2 = _CollisionBone()

        struct.type = collision.type
        struct.constraint_type = collision.type
        struct.bone1.type = 1
        struct.bone1.local_bone_idx = cls._convert_global_bone_id_to_dtp(collision.global_bone_id, global_bone_ids)
        cast(_ITr2013CollisionShape, collision).assign_to_struct(struct, global_bone_ids)
        return struct


class Tr2013CollisionCapsule(CollisionCapsule, Tr2013CollisionShape, _ITr2013CollisionShape):
    def assign_from_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None:
        self.target_global_bone_id = self._convert_dtp_bone_id_to_global(struct.bone2.local_bone_idx, global_bone_ids)
        self.radius = struct.radius

    def assign_to_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None:
        struct.bone2.type = 1
        struct.bone2.local_bone_idx = self._convert_global_bone_id_to_dtp(coalesce(self.target_global_bone_id, -1), global_bone_ids)
        struct.radius = self.radius

class Tr2013CollisionSphere(CollisionSphere, Tr2013CollisionShape, _ITr2013CollisionShape):
    def assign_from_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None:
        self.radius = struct.radius

    def assign_to_struct(self, struct: _CollisionShape, global_bone_ids: list[int | None]) -> None:
        struct.radius = self.radius

Tr2013CollisionShape.type_mapping = {
    CollisionShapeType.CAPSULE:  Tr2013CollisionCapsule,
    CollisionShapeType.SPHERE:   Tr2013CollisionSphere
}
