from typing import cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision, CollisionType
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.rise.RiseBone import RiseBone
from io_scene_tr_reboot.tr.rise.RiseCloth import RiseCloth
from io_scene_tr_reboot.tr.rise.RiseCollection import RiseCollection
from io_scene_tr_reboot.tr.rise.RiseCollision import RiseCollision
from io_scene_tr_reboot.tr.rise.RiseHair import RiseHair
from io_scene_tr_reboot.tr.rise.RiseMesh import RiseMesh
from io_scene_tr_reboot.tr.rise.RiseMeshPart import RiseMeshPart
from io_scene_tr_reboot.tr.rise.RiseModel import RiseModel
from io_scene_tr_reboot.tr.rise.RiseSkeleton import RiseSkeleton

class RiseFactory(IFactory):
    game = CdcGame.ROTTR
    cloth_class = RiseCloth

    def open_collection(self, object_ref_file_path: str) -> Collection:
        return RiseCollection(object_ref_file_path)

    def create_model(self, model_id: int, model_data_id: int) -> IModel:
        if model_data_id != model_id:
            raise Exception()

        return RiseModel(model_id)

    def create_mesh(self, model: IModel) -> IMesh:
        return RiseMesh(cast(RiseModel, model).header)

    def create_mesh_part(self) -> IMeshPart:
        return RiseMeshPart()

    def create_skeleton(self, id: int) -> ISkeleton:
        return RiseSkeleton(id)

    def create_bone(self) -> IBone:
        bone = RiseBone()
        bone.min = Vector()
        bone.max = Vector()
        bone.info_ref = None
        return bone

    def deserialize_bone_constraint(self, data: str) -> IBoneConstraint | None:
        return None

    def create_cloth(self, definition_id: int, tune_id: int) -> Cloth:
        return RiseCloth(definition_id, tune_id)

    def create_collision(self, type: CollisionType, hash: int) -> Collision:
        return RiseCollision.create(type, hash)

    def deserialize_collision(self, data: str) -> Collision:
        return RiseCollision.deserialize(data)

    def create_hair(self, model_id: int | None, hair_data_id: int) -> Hair:
        return RiseHair(hair_data_id)
