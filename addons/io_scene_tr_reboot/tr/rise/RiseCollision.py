from mathutils import Matrix
from io_scene_tr_reboot.tr.Collision import Collision, CollisionBox, CollisionCapsule, CollisionDoubleRadiiCapsule, CollisionSphere, CollisionType
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.Skeleton import ISkeleton

class RiseCollision(Collision):
    @classmethod
    def read(cls, type: CollisionType, hash: int, reader: ResourceReader, transform: Matrix, skeleton: ISkeleton) -> Collision:
        collision = cls.create(type, hash)
        collision._read(reader)
        collision.global_bone_id = cls._convert_dtp_bone_id_to_global(collision.global_bone_id, skeleton)
        collision.transform = transform
        return collision

    @classmethod
    def _convert_dtp_bone_id_to_global(cls, dtp_bone_id: int, skeleton: ISkeleton) -> int:
        return skeleton.bones[dtp_bone_id].global_id or -1

class RiseCollisionSphere(CollisionSphere, RiseCollision):
    def _read(self, reader: ResourceReader) -> None:
        self.radius = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionCapsule(CollisionCapsule, RiseCollision):
    def _read(self, reader: ResourceReader) -> None:
        self.radius = reader.read_float()
        self.length = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionDoubleRadiiCapsule(CollisionDoubleRadiiCapsule, RiseCollision):
    def _read(self, reader: ResourceReader) -> None:
        self.radius_1 = reader.read_float()
        self.radius_2 = reader.read_float()
        self.length = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionBox(CollisionBox, RiseCollision):
    def _read(self, reader: ResourceReader) -> None:
        self.width = reader.read_float()
        self.depth = reader.read_float()
        self.height = reader.read_float()
        self.global_bone_id = reader.read_uint16()

RiseCollision.type_mapping = {
    CollisionType.BOX:                 RiseCollisionBox,
    CollisionType.CAPSULE:             RiseCollisionCapsule,
    CollisionType.DOUBLERADIICAPSULE:  RiseCollisionDoubleRadiiCapsule,
    CollisionType.SPHERE:              RiseCollisionSphere
}
