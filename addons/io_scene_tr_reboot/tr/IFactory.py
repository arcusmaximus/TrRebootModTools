from asyncio import Protocol
from typing import ClassVar
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision, CollisionType
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.Skeleton import ISkeleton

class IFactory(Protocol):
    game: ClassVar[CdcGame]
    cloth_class: ClassVar[type[Cloth]]

    def open_collection(self, object_ref_file_path: str) -> Collection: ...

    def create_model(self, model_id: int, model_data_id: int) -> IModel: ...

    def create_mesh(self, model: IModel) -> IMesh: ...

    def create_mesh_part(self) -> IMeshPart: ...

    def create_skeleton(self, id: int) -> ISkeleton: ...

    def create_bone(self) -> IBone: ...

    def deserialize_bone_constraint(self, data: str) -> IBoneConstraint | None: ...

    def create_cloth(self, definition_id: int, tune_id: int) -> Cloth: ...

    def create_collision(self, type: CollisionType, hash: int) -> Collision: ...

    def deserialize_collision(self, data: str) -> Collision: ...

    def create_hair(self, model_id: int | None, hair_data_id: int) -> Hair: ...
