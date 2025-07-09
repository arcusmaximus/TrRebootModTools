from abc import abstractmethod
from typing import Generic, Protocol, TypeVar
from mathutils import Vector
from io_scene_tr_reboot.tr.Hashes import Hashes
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.tr.ModelReferences import ModelReferences
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.util.Enumerable import Enumerable

class ILodLevel(Protocol):
    min: float
    max: float

class IModel(Protocol):
    id: int
    refs: ModelReferences
    header: IModelDataHeader
    lod_levels: list[ILodLevel]
    meshes: list[IMesh]

    def read(self, reader: ResourceReader) -> None: ...
    def write(self, writer: ResourceBuilder) -> None: ...

TModelReferences = TypeVar("TModelReferences", bound = ModelReferences)
TModelDataHeader = TypeVar("TModelDataHeader", bound = IModelDataHeader)
TLodLevel = TypeVar("TLodLevel", bound = ILodLevel)
TMesh = TypeVar("TMesh", bound = IMesh)
TMeshPart = TypeVar("TMeshPart", bound = IMeshPart)
class Model(IModel, Generic[TModelReferences, TModelDataHeader, TLodLevel, TMesh, TMeshPart]):
    id: int
    refs: TModelReferences
    header: TModelDataHeader            # type: ignore
    lod_levels: list[TLodLevel]
    meshes: list[TMesh]

    def __init__(self, model_id: int, refs: TModelReferences) -> None:
        self.id = model_id
        self.refs = refs                # type: ignore
        self.lod_levels = []            # type: ignore
        self.meshes = []                # type: ignore

    @abstractmethod
    def read(self, reader: ResourceReader) -> None: ...

    @abstractmethod
    def write(self, writer: ResourceBuilder) -> None: ...

    def update_bounding_box(self) -> None:
        min_pos = Vector(( 9999,  9999,  9999))
        max_pos = Vector((-9999, -9999, -9999))
        for vertex in Enumerable(self.meshes).select_many(lambda m: m.vertices):
            position = vertex.attributes[Hashes.position]

            min_pos.x = min(min_pos.x, position[0])
            min_pos.y = min(min_pos.y, position[1])
            min_pos.z = min(min_pos.z, position[2])

            max_pos.x = max(max_pos.x, position[0])
            max_pos.y = max(max_pos.y, position[1])
            max_pos.z = max(max_pos.z, position[2])

        self.header.bound_box_min = min_pos
        self.header.bound_box_max = max_pos
        self.header.bound_sphere_center = (min_pos + max_pos) / 2
        self.header.bound_sphere_radius = (max_pos - min_pos).length / 2
