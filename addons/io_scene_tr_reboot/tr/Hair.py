from abc import abstractmethod
from typing import NamedTuple
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader

class HairPointWeight(NamedTuple):
    global_bone_id: int
    weight: float

class HairPoint(NamedTuple):
    position: tuple[float, ...]
    thickness: float
    weights: list[HairPointWeight]

class Hair:
    model_id: int | None
    hair_data_id: int
    points: list[HairPoint]
    material_id: int | None

    def __init__(self, model_id: int | None, hair_data_id: int) -> None:
        self.model_id = model_id
        self.hair_data_id = hair_data_id
        self.points = []
        self.material_id = None

    @abstractmethod
    def read(self, reader: ResourceReader) -> None: ...

    @abstractmethod
    def write(self, writer: ResourceBuilder) -> None: ...
