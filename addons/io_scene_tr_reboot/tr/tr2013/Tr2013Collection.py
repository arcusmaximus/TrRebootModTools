from typing import Literal, cast
from mathutils import Matrix
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.tr2013.Tr2013Cloth import Tr2013Cloth
from io_scene_tr_reboot.tr.tr2013.Tr2013Hair import Tr2013Hair
from io_scene_tr_reboot.tr.tr2013.Tr2013LegacyModel import Tr2013LegacyModel
from io_scene_tr_reboot.tr.tr2013.Tr2013Material import Tr2013Material
from io_scene_tr_reboot.tr.tr2013.Tr2013Model import Tr2013Model
from io_scene_tr_reboot.tr.tr2013.Tr2013Skeleton import Tr2013Skeleton
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CInt, CStruct32
from io_scene_tr_reboot.util.Enumerable import Enumerable

class Tr2013ObjectHeader(CStruct32):
    padding_1: CArray[CByte, Literal[0x80]]
    cloth_tune_ref_1: ResourceReference | None
    padding_2: CArray[CByte, Literal[0x50]]
    cloth_tune_ref_2: ResourceReference | None
    padding_3: CArray[CByte, Literal[8]]
    num_models: CInt
    model_refs_ref: ResourceReference | None

class Tr2013Collection(Collection):
    game = CdcGame.TR2013

    __skeletons: dict[ResourceKey, Tr2013Skeleton]
    __model_refs: list[ResourceReference]
    __models: dict[ResourceKey, Tr2013Model]
    __cloth_tune_ref: ResourceReference | None
    __cloth: Tr2013Cloth | None

    def __init__(self, object_ref_file_path: str) -> None:
        super().__init__(object_ref_file_path)

        object_reader = self.get_resource_reader(self.object_ref, True)
        if object_reader is None:
            raise Exception("Object file does not exist")

        object_header = object_reader.read_struct(Tr2013ObjectHeader)

        if object_header.model_refs_ref is not None:
            object_reader.seek(object_header.model_refs_ref)
            model_refs = object_reader.read_ref_list(object_header.num_models)
            self.__model_refs = Enumerable(model_refs).of_type(ResourceReference).to_list()
        else:
            self.__model_refs = []

        self.__skeletons = {}
        self.__models = {}
        self.__cloth = None
        self.__cloth_tune_ref = object_header.cloth_tune_ref_1

    def get_model_instances(self) -> list[Collection.ModelInstance]:
        instances: list[Collection.ModelInstance] = []
        for model_ref in self.__model_refs:
            model = self.get_model(model_ref)
            if model is None or self.is_hair_model(model):
                continue

            instances.append(Collection.ModelInstance(model_ref, model_ref, Matrix()))

        return instances

    def get_model(self, resource: ResourceKey) -> IModel | None:
        if resource.__class__ != ResourceKey:
            resource = ResourceKey(resource.type, resource.id)

        model = self.__models.get(resource)
        if model is not None:
            return model

        legacy_model = self.get_legacy_model(resource)
        if legacy_model is None or legacy_model.new_model_ref is None:
            return None

        reader = self.get_resource_reader(legacy_model.new_model_ref, True)
        if reader is None:
            return None

        model = Tr2013Model(resource.id, legacy_model.new_model_ref.id)
        model.read(reader)
        self.__models[resource] = model
        return model

    def _create_material(self) -> Material:
        return Tr2013Material()

    def get_skeleton(self, resource: ResourceKey) -> ISkeleton | None:
        skeleton = self.__skeletons.get(resource)
        if skeleton is not None:
            return skeleton

        legacy_model = self.get_legacy_model(resource)
        if legacy_model is None or legacy_model.bones_ref is None or legacy_model.bone_id_map_ref is None:
            return None

        bones_reader = self.get_resource_reader(legacy_model.bones_ref, True)
        id_mappings_reader = self.get_resource_reader(legacy_model.bone_id_map_ref, True)
        if bones_reader is None or id_mappings_reader is None:
            return None

        skeleton = Tr2013Skeleton(legacy_model.bones_ref.id)
        skeleton.read_bones(bones_reader)
        skeleton.read_id_mappings(id_mappings_reader)
        self.__skeletons[resource] = skeleton
        return skeleton

    def get_collisions(self) -> list[Collision]:
        return []

    @property
    def cloth_definition_ref(self) -> ResourceReference | None:
        for model_ref in self.__model_refs:
            model = cast(Tr2013Model | None, self.get_model(model_ref))
            if model is not None and model.refs.cloth_definition_ref is not None:
                return model.refs.cloth_definition_ref

        return None

    @property
    def cloth_tune_ref(self) -> ResourceReference | None:
        return self.__cloth_tune_ref

    def get_cloth(self) -> Cloth | None:
        if self.__cloth is not None:
            return self.__cloth

        for model_ref in self.__model_refs:
            model = cast(Tr2013Model | None, self.get_model(model_ref))
            if model is None or model.refs.cloth_definition_ref is None:
                continue

            skeleton = self.get_skeleton(model_ref)
            if skeleton is None:
                return None

            definition_reader = self.get_resource_reader(model.refs.cloth_definition_ref, True)
            tune_reader = self.get_resource_reader(self.__cloth_tune_ref, True) if self.__cloth_tune_ref is not None else None
            if definition_reader is None:
                return None

            global_bone_ids = Enumerable(skeleton.bones).select(lambda b: b.global_id).to_list()

            cloth = Tr2013Cloth(model.refs.cloth_definition_ref.id, self.object_ref.id)
            cloth.read(definition_reader, tune_reader, global_bone_ids, [])
            if len(cloth.strips) == 0:
                continue

            self.__cloth = cloth
            return cloth

        return None

    def get_hair_resource_sets(self) -> list[Collection.HairResourceSet]:
        resource_sets: list[Collection.HairResourceSet] = []
        for resource in self.__model_refs:
            model = self.get_model(resource)
            if model is not None and self.is_hair_model(model):
                resource_sets.append(Collection.HairResourceSet(resource, resource))

        return resource_sets

    def get_hair(self, resource: ResourceKey) -> Hair | None:
        model = self.get_model(resource)
        if not isinstance(model, Tr2013Model) or not self.is_hair_model(model) or model.refs.model_data_resource is None:
            return None

        hair = Tr2013Hair(model.id, model.refs.model_data_resource.id)
        hair.from_model(model)
        return hair

    def get_legacy_model(self, resource: ResourceKey) -> Tr2013LegacyModel | None:
        reader = self.get_resource_reader(resource, False)
        if reader is None:
            return None

        return Tr2013LegacyModel(reader.data)

    def is_hair_model(self, model: IModel) -> bool:
        return Enumerable(model.meshes).select_many(lambda m: m.parts).any(lambda p: p.is_hair)
