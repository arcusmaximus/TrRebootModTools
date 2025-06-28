from abc import abstractmethod
from enum import IntEnum
import os
import re
from typing import ClassVar, Iterable, NamedTuple
from mathutils import Matrix
from io_scene_tr_reboot.tr.CollisionModel import CollisionModel
from io_scene_tr_reboot.tr.CollisionShape import CollisionShape
from io_scene_tr_reboot.tr.Hair import Hair
from io_scene_tr_reboot.tr.Material import Material
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.Cloth import Cloth
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class Collection(SlotsBase):
    class RootFileType(IntEnum):
        OBJECTREFERENCE = 0
        LEVEL = 1
        STREAMLAYER = 2

    class CollectionInstance(NamedTuple):
        id_or_name: int | str
        transform: Matrix

    class ModelInstance(NamedTuple):
        skeleton_resource: ResourceKey | None
        model_resource: ResourceKey
        collision_model_resource: ResourceKey | None
        transform: Matrix

    class HairResource(NamedTuple):
        skeleton_resource: ResourceKey | None
        hair_resource: ResourceKey

    __resource_type_extensions: ClassVar[dict[ResourceType, list[str]]] = {
        ResourceType.ANIMATION:     [".trXanim"],
        ResourceType.DTP:           [".trXdtp", ".trXlevel", ".trXlayer"],
        ResourceType.MATERIAL:      [".trXmaterial"],
        ResourceType.MODEL:         [".trXmodel", ".trXmodeldata"],
        ResourceType.PSDRES:        [".trXpsdres"],
        ResourceType.SCRIPT:        [".trXscript"],
        ResourceType.SHADERLIB:     [".trXshaderlib"],
        ResourceType.TEXTURE:       [".dds"],
        ResourceType.TRIGGER:       [".trXtrigger"]
    }

    game: ClassVar[CdcGame]

    folder_path: str
    name: str
    object_ref: ResourceReference
    parent_collection: "Collection | None"

    _root_file_type: RootFileType
    __resource_paths: dict[ResourceKey, str]
    __resource_readers: dict[ResourceKey, ResourceReader]

    def __init__(self, root_file_path: str, parent_collection: "Collection | None" = None) -> None:
        self.folder_path = os.path.split(root_file_path)[0]
        self.name = os.path.splitext(os.path.basename(self.folder_path))[0]

        if root_file_path.endswith("objectref"):
            self._root_file_type = Collection.RootFileType.OBJECTREFERENCE
            object_ref_data: bytes
            with open(root_file_path, "rb") as object_ref_file:
                object_ref_data = object_ref_file.read()

            object_ref_reader = ResourceReader(ResourceKey(ResourceType.OBJECTREFERENCE, -1), object_ref_data, True, self.game)
            object_ref = object_ref_reader.read_ref()
            if object_ref is None:
                raise Exception("No reference to object .dtp file")

            self.object_ref = object_ref
        elif root_file_path.endswith("level"):
            self._root_file_type = Collection.RootFileType.LEVEL
            self.object_ref = ResourceReference(ResourceType.DTP, 0, 0)
        elif root_file_path.endswith("layer"):
            self._root_file_type = Collection.RootFileType.STREAMLAYER

            layer_key = Collection.try_parse_resource_file_path(root_file_path, self.game)
            if layer_key is None:
                layer_key = ResourceKey(ResourceType.DTP, 0)

            self.object_ref = ResourceReference(layer_key.type, layer_key.id, 0)
        else:
            raise Exception(f"Could not parse file name {root_file_path}")

        self.parent_collection = parent_collection

        self.__resource_paths = {}
        self.__resource_readers = {}
        self.__scan_resources()

    @property
    def id(self):
        return self.object_ref.id

    @abstractmethod
    def get_model_instances(self) -> list[ModelInstance]: ...

    @abstractmethod
    def get_model(self, resource: ResourceKey) -> IModel | None: ...

    @abstractmethod
    def get_collision_model(self, resource: ResourceKey) -> CollisionModel | None: ...

    def get_material(self, resource: ResourceKey) -> Material | None:
        reader = self.get_resource_reader(resource, True)
        if reader is None:
            return None

        material = self._create_material()
        material.read(reader)
        return material

    @abstractmethod
    def _create_material(self) -> Material: ...

    @abstractmethod
    def get_skeleton(self, resource: ResourceKey) -> ISkeleton | None: ...

    @abstractmethod
    def get_collision_shapes(self) -> list[CollisionShape]: ...

    @property
    @abstractmethod
    def cloth_definition_ref(self) -> ResourceReference | None: ...

    @property
    @abstractmethod
    def cloth_tune_ref(self) -> ResourceReference | None: ...

    @abstractmethod
    def get_cloth(self) -> Cloth | None: ...

    @abstractmethod
    def get_hair_resources(self) -> list[HairResource]: ...

    @abstractmethod
    def get_hair(self, resource: ResourceKey) -> Hair | None: ...

    @abstractmethod
    def get_collection_instances(self) -> list[CollectionInstance]: ...

    def get_resources(self, resource_type: ResourceType) -> Iterable[ResourceKey]:
        return Enumerable(self.__resource_paths.keys()).where(lambda r: r.type == resource_type)

    def get_resource_file_path(self, resource: ResourceKey) -> str | None:
        if resource.__class__ != ResourceKey:
            resource = ResourceKey(resource.type, resource.id)

        return self.__resource_paths.get(resource)

    def get_resource_name(self, resource: ResourceKey) -> str | None:
        file_path = self.get_resource_file_path(resource)
        if file_path is None:
            return None

        match = re.match(r"(.+)\.\d+", os.path.splitext(os.path.basename(file_path))[0])
        if match is None:
            return None

        return match.group(1)

    @staticmethod
    def make_resource_file_name(resource: ResourceKey, game: CdcGame) -> str:
        return str(resource.id) + Collection.__resource_type_extensions[resource.type][0].replace("X", str(game))

    @staticmethod
    def try_parse_resource_file_path(file_path: str, game: CdcGame) -> ResourceKey | None:
        file_name, file_extension = os.path.splitext(os.path.basename(file_path))
        match = re.search(r"(?:^|\.)(\d+)$", file_name)
        if match is None:
            return None

        resource_id = int(match.group(1))
        for resource_type, resource_type_extensions in Collection.__resource_type_extensions.items():
            if file_extension.lower().replace(str(game), "X") in resource_type_extensions:
                return ResourceKey(resource_type, resource_id)

        return None

    def get_resource_reader(self, resource: ResourceKey, has_references: bool) -> ResourceReader | None:
        resource_key = resource.__class__ == ResourceKey and resource or ResourceKey(resource.type, resource.id)
        reader = self.__resource_readers.get(resource_key)
        if reader is None:
            file_path = self.get_resource_file_path(resource_key)
            if file_path is None:
                return None

            file_data: bytes
            with open(file_path, "rb") as file:
                file_data = file.read()

            reader = ResourceReader(resource_key, file_data, has_references, self.game)
            self.__resource_readers[resource_key] = reader
        else:
            reader = ResourceReader(reader)

        reader.position = reader.resource_body_pos + (resource.offset if isinstance(resource, ResourceReference) else 0)
        return reader

    def __scan_resources(self) -> None:
        for sub_folder_path, _, file_names in os.walk(self.folder_path):
            for file_name in file_names:
                resource_key = self.try_parse_resource_file_path(file_name, self.game)
                if resource_key is not None:
                    self.__resource_paths[resource_key] = os.path.join(sub_folder_path, file_name)
