from ctypes import sizeof
from typing import NamedTuple
from mathutils import Matrix
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollisionModel import CollisionModel
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape, CollisionShapeType
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.BlendshapeDriverSet import BlendShapeDriverSet
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.rise.RiseCloth import RiseCloth
from io_scene_tr_reboot.tr.rise.RiseCollisionModel import RiseCollisionModel
from io_scene_tr_reboot.tr.rise.RiseCollisionShape import RiseCollisionShape
from io_scene_tr_reboot.tr.rise.RiseHair import RiseHair
from io_scene_tr_reboot.tr.rise.RiseMaterial import RiseMaterial
from io_scene_tr_reboot.tr.rise.RiseModel import RiseModel
from io_scene_tr_reboot.tr.rise.RiseSkeleton import RiseSkeleton
from io_scene_tr_reboot.util.CStruct import CInt, CLong, CStruct64, CUInt, CULong
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.IoHelper import IoHelper

class _ObjectHeader(CStruct64):
    zone_id: CInt
    field_4: CInt
    zone_name_ref: ResourceReference | None
    type_name_hash: CInt
    field_14: CInt
    field_18: CLong
    field_20: CLong
    num_components: CInt
    field_2C: CInt
    components_ref: ResourceReference | None
    num_scene_item_types: CInt
    field_3C: CInt
    scene_items_ref: ResourceReference | None
    num_scene_item_counts: CInt
    field_4C: CInt
    scene_item_counts_ref: ResourceReference | None
    render_model_ref: ResourceReference | None
    static_render_model_ref: ResourceReference | None
    collision_model_ref: ResourceReference | None
    static_collision_model_ref: ResourceReference | None
    skeleton_ref: ResourceReference | None
    skeleton_item_id: CInt
    field_84: CInt
    blend_shape_drivers_ref: ResourceReference | None
    cloth_definition_ref: ResourceReference | None
    hair_data_ref: ResourceReference | None
    stream_layers_ref: ResourceReference | None

assert(sizeof(_ObjectHeader) == 0xA8)

class _ObjectComponent(CStruct64):
    type_hash: CUInt
    count: CInt
    dtp_ref: ResourceReference | None

assert(sizeof(_ObjectComponent) == 0x10)

class _ObjectSceneItemArray(CStruct64):
    type_hash: CUInt
    count: CInt
    items_ref: ResourceReference | None

assert(sizeof(_ObjectSceneItemArray) == 0x10)

class _ObjectSceneItem(CStruct64):
    transform: Matrix
    field_40: CInt
    id: CInt
    field_48: CLong
    field_50: CLong
    hash: CULong
    dtp_ref: ResourceReference | None
    field_68: CULong

assert(sizeof(_ObjectSceneItem) == 0x70)

class CollectionSceneItem(NamedTuple):
    hash: int
    dtp_ref: ResourceReference
    transform: Matrix

class _MeshRefSceneItem(CStruct64):
    debug_name_ref: ResourceReference | None
    instance_type: CInt
    render_model_id: CInt
    collision_model_id: CInt

assert(sizeof(_MeshRefSceneItem) == 0x14)

class _ModelHostMaterialSwap(CStruct64):
    original_material_ref: ResourceReference | None
    override_material_ref: ResourceReference | None

assert(sizeof(_ModelHostMaterialSwap) == 0x10)

class _ModelHostMaterialSwapList(CStruct64):
    entries_ref: ResourceReference | None
    num_entries: CInt
    material_set_type: CInt
    debug_name_ref: ResourceReference | None

assert(sizeof(_ModelHostMaterialSwapList) == 0x18)

class _ModelHostModel(CStruct64):
    debug_name_ref: ResourceReference | None
    model_ref: ResourceReference | None
    model_slot_type: CInt
    restrict_to_zone: CInt
    zone_ref: CInt
    derived_zones: CInt
    material_swap_lists_ref: ResourceReference | None
    num_material_swap_lists: CInt
    padding_0: CInt

assert(sizeof(_ModelHostModel) == 0x30)

class _ModelHostComponent(CStruct64):
    models_ref: ResourceReference | None
    num_models: CInt

assert(sizeof(_ModelHostComponent) == 0xC)

class _ObjectStreamLayersHeader(CStruct64):
    num_stream_layer_drms: CInt
    field_4: CInt
    stream_layer_drms_ref: ResourceReference | None

assert(sizeof(_ObjectStreamLayersHeader) == 0x10)

class _ObjectStreamLayerDrm(CStruct64):
    name_ref: ResourceReference | None
    layers_ref: ResourceReference | None
    num_layers: CInt
    field_14: CInt

assert(sizeof(_ObjectStreamLayerDrm) == 0x18)

class _StreamLayerHeader(CStruct64):
    zone_id: CInt
    zone_type_id: CInt
    num_scene_item_types: CInt
    field_C: CInt
    scene_items_ref: ResourceReference | None

assert(sizeof(_StreamLayerHeader) == 0x18)

class RiseCollection(Collection):
    game = CdcGame.ROTTR

    _header: _ObjectHeader | _StreamLayerHeader
    _components: dict[int, ResourceReference]
    _scene_items_by_type: dict[int, list[CollectionSceneItem]]
    _skeleton: ISkeleton | None
    _collisions: list[CollisionShape] | None

    def __init__(self, root_file_path: str, parent_collection: Collection | None = None) -> None:
        super().__init__(root_file_path, parent_collection)
        self._components = {}
        self._scene_items_by_type = {}
        self._skeleton = None
        self._collisions = None

        reader: ResourceReader | None
        match self._root_file_type:
            case Collection.RootFileType.OBJECTREFERENCE:
                reader = self.get_resource_reader(self.object_ref, True)
                if reader is None:
                    raise Exception("Object file does not exist")

                self._header = reader.read_struct(_ObjectHeader)
                if self._header.components_ref is not None:
                    reader.seek(self._header.components_ref)
                    for _ in range(self._header.num_components):
                        scene_item = reader.read_struct(_ObjectComponent)
                        if scene_item.dtp_ref is not None:
                            self._components[scene_item.type_hash] = scene_item.dtp_ref
            case Collection.RootFileType.STREAMLAYER:
                with IoHelper.open_read(root_file_path) as root_file:
                    root_file_data = root_file.read()

                reader = ResourceReader(ResourceKey(ResourceType.DTP, 0), root_file_data, True, self.game)
                self._header = reader.read_struct(_StreamLayerHeader)
            case _:
                raise Exception()

        if self._header.scene_items_ref is not None:
            reader.seek(self._header.scene_items_ref)
            scene_item_arrays = reader.read_struct_list(_ObjectSceneItemArray, self._header.num_scene_item_types)
            for scene_item_array in scene_item_arrays:
                if scene_item_array.items_ref is None:
                    continue

                scene_items_of_type: list[CollectionSceneItem] = []
                self._scene_items_by_type[scene_item_array.type_hash] = scene_items_of_type

                reader.seek(scene_item_array.items_ref)
                for _ in range(scene_item_array.count):
                    scene_item = reader.read_struct(_ObjectSceneItem)
                    if scene_item.dtp_ref is not None:
                        scene_items_of_type.append(CollectionSceneItem(scene_item.hash, scene_item.dtp_ref, scene_item.transform))

    def get_model_instances(self) -> list[Collection.ModelInstance]:
        instances: list[Collection.ModelInstance] = []

        if isinstance(self._header, _ObjectHeader) and self._header.render_model_ref is not None:
            instances.append(Collection.ModelInstance(self._header.skeleton_ref, self._header.render_model_ref, self._header.collision_model_ref, Matrix()))

        instances.extend(self.__get_model_instances_from_meshrefs())
        instances.extend(self.__get_model_instances_from_modelhosts())

        return instances

    def __get_model_instances_from_meshrefs(self) -> list[Collection.ModelInstance]:
        scene_items = self._scene_items_by_type.get(Hashes.meshref)
        if scene_items is None:
            return []

        instances: list[Collection.ModelInstance] = []
        for scene_item in scene_items:
            reader = self.get_resource_reader(scene_item.dtp_ref, True)
            if reader is None:
                continue

            meshref = reader.read_struct(_MeshRefSceneItem)
            instances.append(
                Collection.ModelInstance(
                    self._header.skeleton_ref if isinstance(self._header, _ObjectHeader) else None,
                    ResourceKey(ResourceType.MODEL, meshref.render_model_id),
                    ResourceKey(ResourceType.DTP, meshref.collision_model_id),
                    scene_item.transform
                )
            )

        return instances

    def __get_model_instances_from_modelhosts(self) -> list[Collection.ModelInstance]:
        component_ref = self._components.get(Hashes.modelhost)
        if component_ref is None:
            return []

        reader = self.get_resource_reader(component_ref, True)
        if reader is None:
            return []

        component = reader.read_struct(_ModelHostComponent)
        if component.models_ref is None:
            return []

        instances: list[Collection.ModelInstance] = []
        reader.seek(component.models_ref)
        for model in reader.read_struct_list(_ModelHostModel, component.num_models):
            if model.model_ref is None:
                continue

            instances.append(
                Collection.ModelInstance(
                    self._header.skeleton_ref if isinstance(self._header, _ObjectHeader) else None,
                    model.model_ref,
                    None,
                    Matrix()
                )
            )

        return instances

    def get_model(self, resource: ResourceKey) -> IModel | None:
        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        model = RiseModel(resource.id)
        model.read(reader)
        return model

    def get_blend_shape_drivers(self) -> BlendShapeDriverSet | None:
        if not isinstance(self._header, _ObjectHeader) or self._header.blend_shape_drivers_ref is None:
            return None

        reader = self.get_resource_reader(self._header.blend_shape_drivers_ref, True)
        if reader is None:
            return None

        drivers = BlendShapeDriverSet()
        drivers.read(reader)
        return drivers

    def _create_material(self) -> Material:
        return RiseMaterial()

    def get_collision_model(self, resource: ResourceKey) -> CollisionModel | None:
        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        model = self._create_collision_model()
        model.read(reader)
        return model

    def _create_collision_model(self) -> CollisionModel:
        return RiseCollisionModel()

    def get_skeleton(self, resource: ResourceKey) -> ISkeleton | None:
        if self._skeleton is not None:
            return self._skeleton

        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        self._skeleton = self._create_skeleton(resource.id)
        self._skeleton.read(reader)
        return self._skeleton

    def _create_skeleton(self, id: int) -> ISkeleton:
        return RiseSkeleton(id)

    def get_collision_shapes(self) -> list[CollisionShape]:
        if self._collisions is not None:
            return self._collisions

        if not isinstance(self._header, _ObjectHeader) or self._header.skeleton_ref is None:
            return []

        skeleton = self.get_skeleton(self._header.skeleton_ref)
        if skeleton is None:
            return []

        global_bone_ids = Enumerable(skeleton.bones).select(lambda b: b.global_id).to_list()

        type_hashes: dict[CollisionShapeType, int] = {
            CollisionShapeType.BOX:                 Hashes.genericboxshapelist,
            CollisionShapeType.CAPSULE:             Hashes.genericcapsuleshapelist,
            CollisionShapeType.DOUBLERADIICAPSULE:  Hashes.genericdoubleradiicapsuleshapelist,
            CollisionShapeType.SPHERE:              Hashes.genericsphereshapelist
        }
        self._collisions = []
        for type, type_hash in type_hashes.items():
            scene_items_of_type = self._scene_items_by_type.get(type_hash)
            if scene_items_of_type is None:
                continue

            for scene_item in scene_items_of_type:
                reader = self.get_resource_reader(scene_item.dtp_ref, scene_item.dtp_ref.id == self.object_ref.id)
                if reader is None:
                    continue

                collision = self._read_collision_shape(type, scene_item.hash, reader, scene_item.transform, skeleton.id, global_bone_ids)
                self._collisions.append(collision)

        return self._collisions

    def _read_collision_shape(self, type: CollisionShapeType, hash: int, reader: ResourceReader, transform: Matrix, skeleton_id: int, global_bone_ids: list[int | None]) -> CollisionShape:
        return RiseCollisionShape.read(type, hash, reader, transform, skeleton_id, global_bone_ids)

    @property
    def cloth_definition_ref(self) -> ResourceReference | None:
        if not isinstance(self._header, _ObjectHeader):
            return None

        return self._header.cloth_definition_ref

    @property
    def cloth_tune_ref(self) -> ResourceReference | None:
        return self._components.get(Hashes.cloth)

    def get_cloth(self) -> Cloth | None:
        if not isinstance(self._header, _ObjectHeader) or self._header.skeleton_ref is None:
            return None

        cloth_definition_ref = self.cloth_definition_ref
        cloth_component_ref  = self.cloth_tune_ref
        skeleton = self.get_skeleton(self._header.skeleton_ref)
        if cloth_definition_ref is None or cloth_component_ref is None or skeleton is None:
            return None

        cloth_definition_reader = self.get_resource_reader(cloth_definition_ref, True)
        cloth_component_reader  = self.get_resource_reader(cloth_component_ref, True)
        if cloth_definition_reader is None or cloth_component_reader is None:
            return None

        global_bone_ids = Enumerable(skeleton.bones).select(lambda b: b.global_id).to_list()
        collisions = self.get_collision_shapes()

        cloth = self._create_cloth(cloth_definition_ref.id, cloth_component_ref.id)
        cloth.read(cloth_definition_reader, cloth_component_reader, skeleton.id, global_bone_ids, collisions)
        return cloth

    def _create_cloth(self, definition_id: int, component_id: int) -> Cloth:
        return RiseCloth(definition_id, component_id)

    def get_hair_resources(self) -> list[Collection.HairResource]:
        if not isinstance(self._header, _ObjectHeader) or self._header.hair_data_ref is None:
            return []

        return [Collection.HairResource(self._header.skeleton_ref, self._header.hair_data_ref)]

    def get_hair(self, resource: ResourceKey) -> Hair | None:
        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        hair = self._create_hair(resource.id)
        hair.read(reader)
        return hair

    def _create_hair(self, hair_data_id: int) -> Hair:
        return RiseHair(hair_data_id)

    def get_collection_instances(self) -> list[Collection.CollectionInstance]:
        instances: list[Collection.CollectionInstance] = []
        instances.extend(self.__get_collection_instances_from_object_refs())
        instances.extend(self.__get_collection_instances_from_stream_layers())
        return instances

    def __get_collection_instances_from_object_refs(self) -> list[Collection.CollectionInstance]:
        object_refs = self._scene_items_by_type.get(Hashes.objectref)
        if object_refs is None:
            return []

        instances: list[Collection.CollectionInstance] = []
        for object_ref in object_refs:
            object_ref_reader = self.get_resource_reader(object_ref.dtp_ref, True)
            if object_ref_reader is None:
                continue

            collection_id = self._read_object_ref_collection_id(object_ref_reader)
            instances.append(Collection.CollectionInstance(collection_id, object_ref.transform))

        return instances

    def _read_object_ref_collection_id(self, reader: ResourceReader) -> int:
        reader.skip(0x40)
        return reader.read_int32()

    def __get_collection_instances_from_stream_layers(self) -> list[Collection.CollectionInstance]:
        if not isinstance(self._header, _ObjectHeader) or self._header.stream_layers_ref is None:
            return []

        reader = self.get_resource_reader(self._header.stream_layers_ref, True)
        if reader is None:
            return []

        header = reader.read_struct(_ObjectStreamLayersHeader)
        if header.stream_layer_drms_ref is None:
            return []

        reader.seek(header.stream_layer_drms_ref)
        instances: list[Collection.CollectionInstance] = []
        for drm in reader.read_struct_list(_ObjectStreamLayerDrm, header.num_stream_layer_drms):
            if drm.name_ref is None:
                continue

            reader.seek(drm.name_ref)
            instances.append(Collection.CollectionInstance(reader.read_string_at(0), Matrix()))

        return instances
