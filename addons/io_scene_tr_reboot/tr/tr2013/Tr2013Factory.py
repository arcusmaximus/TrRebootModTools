from typing import cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeType
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.tr2013.Tr2013Bone import Tr2013Bone
from io_scene_tr_reboot.tr.tr2013.Tr2013Cloth import Tr2013Cloth
from io_scene_tr_reboot.tr.tr2013.Tr2013Collection import Tr2013Collection
from io_scene_tr_reboot.tr.tr2013.Tr2013CollectionFinder import Tr2013CollectionFinder
from io_scene_tr_reboot.tr.tr2013.Tr2013CollisionShape import Tr2013CollisionShape
from io_scene_tr_reboot.tr.tr2013.Tr2013Hair import Tr2013Hair
from io_scene_tr_reboot.tr.tr2013.Tr2013Mesh import Tr2013Mesh
from io_scene_tr_reboot.tr.tr2013.Tr2013MeshPart import Tr2013MeshPart
from io_scene_tr_reboot.tr.tr2013.Tr2013Model import Tr2013Model
from io_scene_tr_reboot.tr.tr2013.Tr2013Skeleton import Tr2013Skeleton

class Tr2013Factory(IFactory):
    game = CdcGame.TR2013
    cloth_class = Tr2013Cloth

    def create_collection_finder(self, starting_collection_file_path: str) -> CollectionFinder:
        return Tr2013CollectionFinder(starting_collection_file_path)

    def open_collection(self, object_ref_file_path: str, parent_collection: Collection | None = None) -> Collection:
        return Tr2013Collection(object_ref_file_path, parent_collection)

    def create_model(self, model_id: int, model_data_id: int) -> IModel:
        return Tr2013Model(model_id, model_data_id)

    def create_mesh(self, model: IModel) -> IMesh:
        return Tr2013Mesh(cast(Tr2013Model, model).header)

    def create_mesh_part(self) -> IMeshPart:
        return Tr2013MeshPart()

    def create_skeleton(self, id: int) -> ISkeleton:
        return Tr2013Skeleton(id)

    def create_bone(self) -> IBone:
        bone = Tr2013Bone()
        bone.min = Vector()
        bone.max = Vector()
        bone.last_vertex = 0xFFFF
        bone.info_ref = None
        return bone

    def deserialize_bone_constraint(self, data: str) -> IBoneConstraint | None:
        return None

    def create_cloth(self, definition_id: int, tune_id: int) -> Cloth:
        return Tr2013Cloth(definition_id, tune_id)

    def create_collision_shape(self, type: CollisionShapeType, skeleton_id: int | None, hash: int) -> CollisionShape:
        return Tr2013CollisionShape.create(type, skeleton_id, hash)

    def deserialize_collision_shape(self, data: str) -> CollisionShape:
        return Tr2013CollisionShape.deserialize(data)

    def create_hair(self, model_id: int | None, hair_data_id: int) -> Hair:
        if model_id is None:
            raise Exception()

        return Tr2013Hair(model_id, hair_data_id)
