from abc import abstractmethod
from enum import IntEnum
from typing import ClassVar, NamedTuple, cast
from mathutils import Matrix
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.util.Conditional import coalesce
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.Serializer import Serializer

class CollisionShapeType(IntEnum):
    SPHERE = 0
    CAPSULE = 1
    PLANE = 2
    WEDGE = 3
    BOX = 4
    FACE = 5
    DOUBLERADIICAPSULE = 6

class CollisionShapeKey(NamedTuple):
    type: CollisionShapeType
    skeleton_id: int | None
    hash: int

class CollisionShapeInput(NamedTuple):
    reader: ResourceReader
    transform: Matrix

class CollisionShape:
    type_mapping: ClassVar[dict[CollisionShapeType, type["CollisionShape"]]] = {}

    @classmethod
    def create(cls, type: CollisionShapeType, skeleton_id: int | None, hash: int) -> "CollisionShape":
        return cls.type_mapping[type](skeleton_id, hash)

    hash: int
    skeleton_id: int | None
    global_bone_id: int
    transform: Matrix | None

    def __init__(self, skeleton_id: int | None, hash: int) -> None:
        self.hash = hash
        self.skeleton_id = skeleton_id
        self.global_bone_id = -1
        self.transform = None       # type: ignore

    @property
    def type(self) -> CollisionShapeType:
        return Enumerable(self.type_mapping.items()).first(lambda p: p[1] == type(self))[0]

    @abstractmethod
    def _read(self, reader: ResourceReader) -> None: ...

    def serialize(self) -> str:
        return Serializer.serialize_object(self, { "type": str(self.type) })

    @classmethod
    def deserialize(cls, data: str) -> "CollisionShape":
        def create_collision(values: dict[str, str]) -> CollisionShape:
            type = CollisionShapeType(int(values["type"]))
            skeleton_id = values.get("skeleton_id")
            if skeleton_id is not None:
                skeleton_id = int(skeleton_id)

            hash = int(values["hash"])
            return cls.create(type, skeleton_id, hash)

        return cast(CollisionShape, Serializer.deserialize_object(data, create_collision))

    @staticmethod
    def _convert_dtp_bone_id_to_global(dtp_bone_id: int, global_bone_ids: list[int | None]) -> int:
        return coalesce(global_bone_ids[dtp_bone_id], -1)

    @staticmethod
    def _convert_global_bone_id_to_dtp(global_bone_id: int, global_bone_ids: list[int | None]) -> int:
        return Enumerable(global_bone_ids).index_of(global_bone_id)

class CollisionSphere(CollisionShape):
    radius: float

    def __init__(self, skeleton_id: int | None, hash: int) -> None:
        super().__init__(skeleton_id, hash)
        self.radius = 1.0

class CollisionCapsule(CollisionShape):
    radius: float
    length: float | None
    target_global_bone_id: int | None

    def __init__(self, skeleton_id: int | None, hash: int) -> None:
        super().__init__(skeleton_id, hash)
        self.radius = 1.0
        self.length = None
        self.target_global_bone_id = None

class CollisionDoubleRadiiCapsule(CollisionShape):
    radius_1: float
    radius_2: float
    length: float

    def __init__(self, skeleton_id: int | None, hash: int) -> None:
        super().__init__(skeleton_id, hash)
        self.radius_1 = 1.0
        self.radius_2 = 1.0
        self.length = 1.0

class CollisionBox(CollisionShape):
    width: float
    depth: float
    height: float

    def __init__(self, skeleton_id: int | None, hash: int) -> None:
        super().__init__(skeleton_id, hash)
        self.width = 1.0
        self.depth = 1.0
        self.height = 1.0
