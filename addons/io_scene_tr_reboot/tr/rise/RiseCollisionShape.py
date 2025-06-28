from mathutils import Matrix
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionBox, CollisionCapsule, CollisionDoubleRadiiCapsule, CollisionSphere, CollisionShapeType
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader

class RiseCollisionShape(CollisionShape):
    @classmethod
    def read(cls, type: CollisionShapeType, hash: int, reader: ResourceReader, transform: Matrix, skeleton_id: int, global_bone_ids: list[int | None]) -> CollisionShape:
        collision = cls.create(type, skeleton_id, hash)
        collision._read(reader)
        collision.global_bone_id = cls._convert_dtp_bone_id_to_global(collision.global_bone_id, global_bone_ids)
        collision.transform = transform
        return collision

class RiseCollisionSphere(CollisionSphere, RiseCollisionShape):
    def _read(self, reader: ResourceReader) -> None:
        self.radius = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionCapsule(CollisionCapsule, RiseCollisionShape):
    def _read(self, reader: ResourceReader) -> None:
        self.radius = reader.read_float()
        self.length = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionDoubleRadiiCapsule(CollisionDoubleRadiiCapsule, RiseCollisionShape):
    def _read(self, reader: ResourceReader) -> None:
        self.radius_1 = reader.read_float()
        self.radius_2 = reader.read_float()
        self.length = reader.read_float()
        self.global_bone_id = reader.read_uint16()

class RiseCollisionBox(CollisionBox, RiseCollisionShape):
    def _read(self, reader: ResourceReader) -> None:
        self.width = reader.read_float()
        self.depth = reader.read_float()
        self.height = reader.read_float()
        self.global_bone_id = reader.read_uint16()

RiseCollisionShape.type_mapping = {
    CollisionShapeType.BOX:                 RiseCollisionBox,
    CollisionShapeType.CAPSULE:             RiseCollisionCapsule,
    CollisionShapeType.DOUBLERADIICAPSULE:  RiseCollisionDoubleRadiiCapsule,
    CollisionShapeType.SPHERE:              RiseCollisionSphere
}
