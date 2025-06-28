from typing import cast
from mathutils import Vector
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.CollisionModel import CollisionModel
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeType
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.shadow.ShadowBone import ShadowBone
from io_scene_tr_reboot.tr.shadow.ShadowBoneConstraint import ShadowBoneConstraint
from io_scene_tr_reboot.tr.shadow.ShadowCloth import ShadowCloth
from io_scene_tr_reboot.tr.shadow.ShadowCollection import ShadowCollection
from io_scene_tr_reboot.tr.shadow.ShadowCollectionFinder import ShadowCollectionFinder
from io_scene_tr_reboot.tr.shadow.ShadowCollisionModel import ShadowCollisionModel
from io_scene_tr_reboot.tr.shadow.ShadowCollisionShape import ShadowCollisionShape
from io_scene_tr_reboot.tr.shadow.ShadowHair import ShadowHair
from io_scene_tr_reboot.tr.shadow.ShadowMesh import ShadowMesh
from io_scene_tr_reboot.tr.shadow.ShadowMeshPart import ShadowMeshPart
from io_scene_tr_reboot.tr.shadow.ShadowModel import ShadowModel
from io_scene_tr_reboot.tr.shadow.ShadowModelReferences import ShadowModelReferences
from io_scene_tr_reboot.tr.shadow.ShadowSkeleton import ShadowSkeleton

class ShadowFactory(IFactory):
    game = CdcGame.SOTTR
    cloth_class = ShadowCloth

    def create_collection_finder(self, starting_collection_file_path: str) -> CollectionFinder:
        return ShadowCollectionFinder(starting_collection_file_path)

    def open_collection(self, object_ref_file_path: str, parent_collection: Collection | None = None) -> Collection:
        return ShadowCollection(object_ref_file_path, parent_collection)

    def create_model(self, model_id: int, model_data_id: int) -> IModel:
        return ShadowModel(model_id, ShadowModelReferences(model_data_id))

    def create_mesh(self, model: IModel) -> IMesh:
        return ShadowMesh(cast(ShadowModel, model).header)

    def create_mesh_part(self) -> IMeshPart:
        tr_mesh_part = ShadowMeshPart()
        tr_mesh_part.center = Vector()
        tr_mesh_part.flags = 0x40030
        return tr_mesh_part

    def create_collision_model(self) -> CollisionModel:
        return ShadowCollisionModel()

    def create_skeleton(self, id: int) -> ISkeleton:
        return ShadowSkeleton(id)

    def create_bone(self) -> IBone:
        return ShadowBone()

    def deserialize_bone_constraint(self, data: str) -> IBoneConstraint | None:
        return ShadowBoneConstraint.deserialize(data)

    def create_cloth(self, definition_id: int, tune_id: int) -> Cloth:
        return ShadowCloth(definition_id, tune_id)

    def create_collision_shape(self, type: CollisionShapeType, skeleton_id: int | None, hash: int) -> CollisionShape:
        return ShadowCollisionShape.create(type, skeleton_id, hash)

    def deserialize_collision_shape(self, data: str) -> CollisionShape:
        return ShadowCollisionShape.deserialize(data)

    def create_hair(self, model_id: int | None, hair_data_id: int) -> Hair:
        return ShadowHair(hair_data_id)
