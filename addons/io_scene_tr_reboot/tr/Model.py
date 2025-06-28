from abc import abstractmethod
from typing import Generic, Protocol, TypeVar
from io_scene_tr_reboot.tr.Mesh import IMesh
from io_scene_tr_reboot.tr.MeshPart import IMeshPart
from io_scene_tr_reboot.tr.IModelDataHeader import IModelDataHeader
from io_scene_tr_reboot.tr.ModelReferences import ModelReferences
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader

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
