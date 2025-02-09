from mathutils import Matrix
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.rise.RiseCollection import RiseCollection
from io_scene_tr_reboot.tr.rise.RiseCollision import CollisionType
from io_scene_tr_reboot.tr.shadow.ShadowCloth import ShadowCloth
from io_scene_tr_reboot.tr.shadow.ShadowCollision import ShadowCollision
from io_scene_tr_reboot.tr.shadow.ShadowMaterial import ShadowMaterial
from io_scene_tr_reboot.tr.shadow.ShadowModel import ShadowModel
from io_scene_tr_reboot.tr.shadow.ShadowModelReferences import ShadowModelReferences
from io_scene_tr_reboot.tr.shadow.ShadowSkeleton import ShadowSkeleton

class ShadowCollection(RiseCollection):
    game = CdcGame.SOTTR

    def get_model(self, resource: ResourceKey) -> IModel | None:
        refs_reader = self.get_resource_reader(resource, True)
        if refs_reader is None:
            return None

        refs = ShadowModelReferences()
        refs.read(refs_reader)
        if refs.model_data_resource is None:
            return None

        data_reader = self.get_resource_reader(refs.model_data_resource, False)
        if data_reader is None:
            return None

        model = ShadowModel(resource.id, refs)
        model.read(data_reader)
        return model

    def _create_material(self) -> Material:
        return ShadowMaterial()

    def _create_skeleton(self, id: int) -> ISkeleton:
        return ShadowSkeleton(id)

    def _read_collision(self, type: CollisionType, hash: int, reader: ResourceReader, transform: Matrix, global_bone_ids: list[int | None]):
        return ShadowCollision.read(type, hash, reader, transform, global_bone_ids)

    def _create_cloth(self, definition_id: int, component_id: int) -> Cloth:
        return ShadowCloth(definition_id, component_id)
