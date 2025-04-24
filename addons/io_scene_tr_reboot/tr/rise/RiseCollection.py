from ctypes import sizeof
from typing import NamedTuple
from mathutils import Matrix
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Collision import Collision
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.rise.RiseCloth import RiseCloth
from io_scene_tr_reboot.tr.rise.RiseCollision import RiseCollision, CollisionType
from io_scene_tr_reboot.tr.rise.RiseHair import RiseHair
from io_scene_tr_reboot.tr.rise.RiseMaterial import RiseMaterial
from io_scene_tr_reboot.tr.rise.RiseModel import RiseModel
from io_scene_tr_reboot.tr.rise.RiseSkeleton import RiseSkeleton
from io_scene_tr_reboot.util.CStruct import CInt, CLong, CStruct64, CUInt, CULong
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _ObjectHeader(CStruct64):
    zone_id: CInt
    field_4: CInt
    zone_name_ref: ResourceReference | None
    type_name_hash: CInt
    field_14: CInt
    field_18: CLong
    field_20: CLong
    num_simple_components: CInt
    field_2C: CInt
    simple_components_ref: ResourceReference | None
    num_transformed_component_types: CInt
    field_3C: CInt
    transformed_components_ref: ResourceReference | None
    num_transformed_component_counts: CInt
    field_4C: CInt
    transformed_component_counts_ref: ResourceReference | None
    render_model_ref: ResourceReference | None
    static_render_model_ref: ResourceReference | None
    collision_model_ref: ResourceReference | None
    static_collision_model_ref: ResourceReference | None
    skeleton_ref: ResourceReference | None
    skeleton_item_id: CInt
    field_84: CInt
    pose_space_deformers_ref: ResourceReference | None
    cloth_definition_ref: ResourceReference | None
    hair_data_ref: ResourceReference | None
    object_zone_db_ref: ResourceReference | None

assert(sizeof(_ObjectHeader) == 0xA8)

class _ObjectSimpleComponent(CStruct64):
    type_hash: CUInt
    count: CInt
    dtp_ref: ResourceReference | None

assert(sizeof(_ObjectSimpleComponent) == 0x10)

class _ObjectTransformedComponentArray(CStruct64):
    type_hash: CUInt
    count: CInt
    items_ref: ResourceReference | None

assert(sizeof(_ObjectTransformedComponentArray) == 0x10)

class _ObjectTransformedComponent(CStruct64):
    transform: Matrix
    field_40: CInt
    id: CInt
    field_48: CLong
    field_50: CLong
    hash: CULong
    dtp_ref: ResourceReference | None
    field_68: CULong

assert(sizeof(_ObjectTransformedComponent) == 0x70)

class CollectionTransformedComponent(NamedTuple):
    hash: int
    dtp_ref: ResourceReference
    transform: Matrix

class _MeshRefComponent(CStruct64):
    debug_name_ref: ResourceReference | None
    instance_type: CInt
    render_mesh_id: CInt
    collision_mesh_id: CInt

assert(sizeof(_MeshRefComponent) == 0x14)

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
    mesh_ref: ResourceReference | None
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

class RiseCollection(Collection):
    game = CdcGame.ROTTR

    _header: _ObjectHeader
    _simple_components: dict[int, ResourceReference]
    _transformed_components: dict[int, list[CollectionTransformedComponent]]
    _skeleton: ISkeleton | None
    _collisions: list[Collision] | None

    def __init__(self, object_ref_file_path: str) -> None:
        super().__init__(object_ref_file_path)
        self._simple_components = {}
        self._transformed_components = {}
        self._skeleton = None
        self._collisions = None

        reader = self.get_resource_reader(self.object_ref, True)
        if reader is None:
            raise Exception("Object file does not exist")

        self._header = reader.read_struct(_ObjectHeader)

        if self._header.simple_components_ref is not None:
            reader.seek(self._header.simple_components_ref)
            for _ in range(self._header.num_simple_components):
                component = reader.read_struct(_ObjectSimpleComponent)
                if component.dtp_ref is not None:
                    self._simple_components[component.type_hash] = component.dtp_ref

        if self._header.transformed_components_ref is not None:
            reader.seek(self._header.transformed_components_ref)
            transformed_component_arrays = reader.read_struct_list(_ObjectTransformedComponentArray, self._header.num_transformed_component_types)
            for transformed_component_array in transformed_component_arrays:
                if transformed_component_array.items_ref is None:
                    continue

                components_of_type: list[CollectionTransformedComponent] = []
                self._transformed_components[transformed_component_array.type_hash] = components_of_type

                reader.seek(transformed_component_array.items_ref)
                for _ in range(transformed_component_array.count):
                    component = reader.read_struct(_ObjectTransformedComponent)
                    if component.dtp_ref is not None:
                        components_of_type.append(CollectionTransformedComponent(component.hash, component.dtp_ref, component.transform))

    def get_model_instances(self) -> list[Collection.ModelInstance]:
        instances: list[Collection.ModelInstance] = []

        if self._header.render_model_ref is not None:
            instances.append(Collection.ModelInstance(self._header.skeleton_ref, self._header.render_model_ref, Matrix()))

        instances.extend(self.__get_model_instances_from_meshrefs())
        instances.extend(self.__get_model_instances_from_modelhosts())

        return instances

    def __get_model_instances_from_meshrefs(self) -> list[Collection.ModelInstance]:
        components = self._transformed_components.get(Hashes.meshref)
        if components is None:
            return []

        instances: list[Collection.ModelInstance] = []
        for component in components:
            reader = self.get_resource_reader(component.dtp_ref, True)
            if reader is None:
                continue

            meshref = reader.read_struct(_MeshRefComponent)
            instances.append(
                Collection.ModelInstance(
                    self._header.skeleton_ref,
                    ResourceKey(ResourceType.MODEL, meshref.render_mesh_id),
                    component.transform
                )
            )

        return instances

    def __get_model_instances_from_modelhosts(self) -> list[Collection.ModelInstance]:
        component_ref = self._simple_components.get(Hashes.modelhost)
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
            if model.mesh_ref is None:
                continue

            instances.append(
                Collection.ModelInstance(
                    self._header.skeleton_ref,
                    model.mesh_ref,
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

    def _create_material(self) -> Material:
        return RiseMaterial()

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

    def get_collisions(self) -> list[Collision]:
        if self._collisions is not None:
            return self._collisions

        if self._header.skeleton_ref is None:
            return []

        skeleton = self.get_skeleton(self._header.skeleton_ref)
        if skeleton is None:
            return []

        global_bone_ids = Enumerable(skeleton.bones).select(lambda b: b.global_id).to_list()

        type_hashes: dict[CollisionType, int] = {
            CollisionType.BOX:                 Hashes.genericboxshapelist,
            CollisionType.CAPSULE:             Hashes.genericcapsuleshapelist,
            CollisionType.DOUBLERADIICAPSULE:  Hashes.genericdoubleradiicapsuleshapelist,
            CollisionType.SPHERE:              Hashes.genericsphereshapelist
        }
        self._collisions = []
        for type, type_hash in type_hashes.items():
            components_of_type = self._transformed_components.get(type_hash)
            if components_of_type is None:
                continue

            for component in components_of_type:
                reader = self.get_resource_reader(component.dtp_ref, component.dtp_ref.id == self.object_ref.id)
                if reader is None:
                    continue

                collision = self._read_collision(type, component.hash, reader, component.transform, global_bone_ids)
                self._collisions.append(collision)

        return self._collisions

    def _read_collision(self, type: CollisionType, hash: int, reader: ResourceReader, transform: Matrix, global_bone_ids: list[int | None]):
        return RiseCollision.read(type, hash, reader, transform, global_bone_ids)

    @property
    def cloth_definition_ref(self) -> ResourceReference | None:
        return self._header.cloth_definition_ref

    @property
    def cloth_tune_ref(self) -> ResourceReference | None:
        return self._simple_components.get(Hashes.cloth)

    def get_cloth(self) -> Cloth | None:
        if self._header.skeleton_ref is None:
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
        collisions = self.get_collisions()

        cloth = self._create_cloth(cloth_definition_ref.id, cloth_component_ref.id)
        cloth.read(cloth_definition_reader, cloth_component_reader, global_bone_ids, collisions)
        return cloth

    def _create_cloth(self, definition_id: int, component_id: int) -> Cloth:
        return RiseCloth(definition_id, component_id)

    def get_hair_resource_sets(self) -> list[Collection.HairResourceSet]:
        if self._header.hair_data_ref is None:
            return []

        return [Collection.HairResourceSet(self._header.skeleton_ref, self._header.hair_data_ref)]

    def get_hair(self, resource: ResourceKey) -> Hair | None:
        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        hair = self._create_hair(resource.id)
        hair.read(reader)
        return hair

    def _create_hair(self, hair_data_id: int) -> Hair:
        return RiseHair(hair_data_id)
