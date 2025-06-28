from asyncio import Protocol
from typing import ClassVar
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.CollisionModel import CollisionModel
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeType
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.Skeleton import ISkeleton

class IFactory(Protocol):
    game: ClassVar[CdcGame]
    cloth_class: ClassVar[type[Cloth]]

    def create_collection_finder(self, starting_collection_file_path: str) -> CollectionFinder: ...

    def open_collection(self, object_ref_file_path: str, parent_collection: Collection | None = None) -> Collection: ...

    def create_model(self, model_id: int, model_data_id: int) -> IModel: ...

    def create_mesh(self, model: IModel) -> IMesh: ...

    def create_mesh_part(self) -> IMeshPart: ...

    def create_collision_model(self) -> CollisionModel: ...

    def create_skeleton(self, id: int) -> ISkeleton: ...

    def create_bone(self) -> IBone: ...

    def deserialize_bone_constraint(self, data: str) -> IBoneConstraint | None: ...

    def create_cloth(self, definition_id: int, tune_id: int) -> Cloth: ...

    def create_collision_shape(self, type: CollisionShapeType, skeleton_id: int | None, hash: int) -> CollisionShape: ...

    def deserialize_collision_shape(self, data: str) -> CollisionShape: ...

    def create_hair(self, model_id: int | None, hair_data_id: int) -> Hair: ...
