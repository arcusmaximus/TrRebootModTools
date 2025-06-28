from ctypes import sizeof
from typing import Literal, cast
from mathutils import Matrix
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollisionModel import CollisionModel
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.tr2013.Tr2013Cloth import Tr2013Cloth
from io_scene_tr_reboot.tr.tr2013.Tr2013Hair import Tr2013Hair
from io_scene_tr_reboot.tr.tr2013.Tr2013LegacyModel import Tr2013LegacyModel
from io_scene_tr_reboot.tr.tr2013.Tr2013Material import Tr2013Material
from io_scene_tr_reboot.tr.tr2013.Tr2013Model import Tr2013Model
from io_scene_tr_reboot.tr.tr2013.Tr2013Skeleton import Tr2013Skeleton
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CFloat, CInt, CShort, CStruct32
from io_scene_tr_reboot.util.CStructTypeMappings import CVec2
from io_scene_tr_reboot.util.DictionaryExtensions import DictionaryExtensions
from io_scene_tr_reboot.util.Enumerable import Enumerable

class Tr2013ObjectHeader(CStruct32):
    padding_1: CArray[CByte, Literal[0x80]]
    cloth_tune_ref_1: ResourceReference | None
    padding_2: CArray[CByte, Literal[0x50]]
    cloth_tune_ref_2: ResourceReference | None
    padding_3: CArray[CByte, Literal[8]]
    num_models: CInt
    model_refs_ref: ResourceReference | None

class _Level(CStruct32):
    padding: CArray[CByte, Literal[0x2C]]
    unit_data_ref: ResourceReference | None
    admd_data_ref: ResourceReference | None

class _UnitData(CStruct32):
    padding: CArray[CByte, Literal[0xDAC]]
    stream_layers_ref: ResourceReference | None
    stream_layer_set_count: CInt
    stream_layers_count: CInt
    stream_layer_sets_ref: ResourceReference | None

class _StreamLayerEntry(CStruct32):
    name_ref: ResourceReference | None
    active: CByte
    preload: CByte
    padding: CArray[CByte, Literal[2]]
    reserved_dram: CInt
    alloced_reserved_dram: CInt
    data_pointer_ref: ResourceReference | None
    reserved_check: CInt

assert(sizeof(_StreamLayerEntry) == 0x18)

class _AdmdData(CStruct32):
    padding: CArray[CByte, Literal[0xB0]]
    num_mesh_instances: CInt
    mesh_instance_refs_ref: ResourceReference | None

class _LightMapProperties(CStruct32):
    uv_scale: CVec2
    uv_offset: CVec2
    texture_ref: ResourceReference | None

class _MeshInstance(CStruct32):
    transform: Matrix
    fixed_transform_ref: ResourceReference | None
    debug_name_ref: ResourceReference | None
    parent_ref: ResourceReference | None
    model_holder_dtp_id: CInt
    drm_name_ref: ResourceReference | None
    lod_level_start: CByte
    lod_level_end: CByte
    flags: CShort
    instance_lod_bias: CFloat
    instance_lod_scale: CFloat
    texture_0_ref: ResourceReference | None
    texture_1_ref: ResourceReference | None
    texture_2_ref: ResourceReference | None
    texture_3_ref: ResourceReference | None
    instance_params_ref: ResourceReference | None
    light_map_properties: _LightMapProperties
    num_override_materials: CInt
    override_materials_ref: ResourceReference | None
    light_mask: CInt
    reflection_lod_bias: CFloat
    collision_type_override: CInt
    collision_terrain_group_id: CInt
    data_ref: ResourceReference | None
    resolve_object_ref: ResourceReference | None
    mesh_instance_ref: ResourceReference | None
    introduction_distance: CFloat
    stream_layer_idx: CInt
    material_ref: ResourceReference | None
    index: CInt

assert(sizeof(_MeshInstance) == 0xBC)

class _MeshInstanceModelHolder(CStruct32):
    type: CInt
    render_model_ref: ResourceReference | None
    collision_model_ref: ResourceReference | None

class Tr2013Collection(Collection):
    game = CdcGame.TR2013

    __skeletons: dict[ResourceKey, Tr2013Skeleton]
    __model_refs: list[ResourceReference]
    __models: dict[ResourceKey, Tr2013Model]
    __cloth_tune_ref: ResourceReference | None
    __cloth: Tr2013Cloth | None

    __level: _Level | None
    __level_model_instances: dict[str, list[Collection.ModelInstance]] | None

    def __init__(self, root_file_path: str, parent_collection: Collection | None = None) -> None:
        super().__init__(root_file_path, parent_collection)
        self.__skeletons = {}
        self.__model_refs = []
        self.__models = {}
        self.__cloth_tune_ref = None
        self.__cloth = None

        self.__level = None
        self.__level_model_instances = None

        match self._root_file_type:
            case Collection.RootFileType.OBJECTREFERENCE:
                reader = self.get_resource_reader(self.object_ref, True)
                if reader is None:
                    raise Exception("Object file does not exist")

                object_header = reader.read_struct(Tr2013ObjectHeader)

                if object_header.model_refs_ref is not None:
                    reader.seek(object_header.model_refs_ref)
                    model_refs = reader.read_ref_list(object_header.num_models)
                    self.__model_refs = Enumerable(model_refs).of_type(ResourceReference).to_list()

                self.__cloth_tune_ref = object_header.cloth_tune_ref_1

            case Collection.RootFileType.LEVEL:
                with open(root_file_path, "rb") as level_file:
                    level_data = level_file.read()

                reader = ResourceReader(ResourceKey(ResourceType.DTP, 0), level_data, True, CdcGame.TR2013)
                self.__level = reader.read_struct(_Level)

            case Collection.RootFileType.STREAMLAYER:
                pass

    def get_model_instances(self) -> list[Collection.ModelInstance]:
        instances: list[Collection.ModelInstance] = []

        match self._root_file_type:
            case Collection.RootFileType.OBJECTREFERENCE:
                for model_ref in self.__model_refs:
                    model = self.get_model(model_ref)
                    if model is None or self.is_hair_model(model):
                        continue

                    instances.append(Collection.ModelInstance(model_ref, model_ref, None, Matrix.Identity(4)))

            case Collection.RootFileType.LEVEL:
                level_model_instances = self.__get_level_model_instances().get("")
                if level_model_instances is not None:
                    instances.extend(level_model_instances)

            case Collection.RootFileType.STREAMLAYER:
                if isinstance(self.parent_collection, Tr2013Collection):
                    stream_layer_name = self.name[len(self.parent_collection.name) + 1:]
                    stream_layer_model_instances = self.parent_collection.__get_level_model_instances().get(stream_layer_name)
                    if stream_layer_model_instances is not None:
                        instances.extend(stream_layer_model_instances)

        return instances

    def get_model(self, resource: ResourceKey) -> IModel | None:
        if resource.__class__ != ResourceKey:
            resource = ResourceKey(resource.type, resource.id)

        model = self.__models.get(resource)
        if model is not None:
            return model

        model_id: int
        model_data_key: ResourceKey
        match resource.type:
            case ResourceType.DTP:
                legacy_model = self.get_legacy_model(resource)
                if legacy_model is None or legacy_model.new_model_ref is None:
                    return None

                model_id = resource.id
                model_data_key = legacy_model.new_model_ref
            case ResourceType.MODEL:
                model_id = 0
                model_data_key = resource
            case _:
                return None

        reader = self.get_resource_reader(model_data_key, True)
        if reader is None:
            return None

        model = Tr2013Model(model_id, model_data_key.id)
        model.read(reader)
        self.__models[resource] = model
        return model

    def get_collision_model(self, resource: ResourceKey) -> CollisionModel | None:
        return None

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

    def get_collision_shapes(self) -> list[CollisionShape]:
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
            cloth.read(definition_reader, tune_reader, skeleton.id, global_bone_ids, [])
            if len(cloth.strips) == 0:
                continue

            self.__cloth = cloth
            return cloth

        return None

    def get_hair_resources(self) -> list[Collection.HairResource]:
        resource_sets: list[Collection.HairResource] = []
        for resource in self.__model_refs:
            model = self.get_model(resource)
            if model is not None and self.is_hair_model(model):
                resource_sets.append(Collection.HairResource(resource, resource))

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

    def get_collection_instances(self) -> list[Collection.CollectionInstance]:
        level_model_instances = self.__get_level_model_instances()
        return Enumerable(level_model_instances.keys()).where(lambda s: s != "") \
                                                       .select(lambda s: Collection.CollectionInstance(self.name + "_" + s.lower(), Matrix.Identity(4))) \
                                                       .to_list()

    def __get_level_model_instances(self) -> dict[str, list[Collection.ModelInstance]]:
        if self.__level_model_instances is not None:
            return self.__level_model_instances

        self.__level_model_instances = {}
        stream_layer_names = self.__read_stream_layer_names()

        if self.__level is None or self.__level.admd_data_ref is None:
            return self.__level_model_instances

        admd_reader = self.get_resource_reader(self.__level.admd_data_ref, True)
        if admd_reader is None:
            return self.__level_model_instances

        admd_data = admd_reader.read_struct(_AdmdData)
        if admd_data.mesh_instance_refs_ref is None:
            return self.__level_model_instances

        admd_reader.seek(admd_data.mesh_instance_refs_ref)
        for mesh_instance_ref in admd_reader.read_ref_list(admd_data.num_mesh_instances):
            if mesh_instance_ref is None:
                continue

            admd_reader.seek(mesh_instance_ref)
            mesh_instance = admd_reader.read_struct(_MeshInstance)
            model_holder_reader = self.get_resource_reader(ResourceKey(ResourceType.DTP, mesh_instance.model_holder_dtp_id), True)
            if model_holder_reader is None:
                continue

            model_holder = model_holder_reader.read_struct(_MeshInstanceModelHolder)
            if model_holder.render_model_ref is None or model_holder.render_model_ref.type != ResourceType.MODEL:
                continue

            stream_layer_name = stream_layer_names[mesh_instance.stream_layer_idx] if mesh_instance.stream_layer_idx >= 0 else ""
            model_instance = Collection.ModelInstance(None, model_holder.render_model_ref, None, mesh_instance.transform)
            DictionaryExtensions.get_or_add(self.__level_model_instances, stream_layer_name, lambda: []).append(model_instance)

        return self.__level_model_instances

    def __read_stream_layer_names(self) -> list[str]:
        if self.__level is None or self.__level.unit_data_ref is None:
            return []

        reader = self.get_resource_reader(self.__level.unit_data_ref, True)
        if reader is None:
            return []

        unit_data = reader.read_struct(_UnitData)
        if unit_data.stream_layers_ref is None:
            return []

        reader.seek(unit_data.stream_layers_ref)
        stream_layer_names: list[str] = []
        for stream_layer in reader.read_struct_list(_StreamLayerEntry, unit_data.stream_layers_count):
            if stream_layer.name_ref is None:
                raise Exception()

            reader.seek(stream_layer.name_ref)
            stream_layer_names.append(reader.read_string_at(0))

        return stream_layer_names

