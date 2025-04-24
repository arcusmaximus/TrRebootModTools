from abc import abstractmethod
from typing import NamedTuple
from mathutils import Vector
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader

class HairPointWeight(NamedTuple):
    global_bone_id: int
    weight: float

class HairPoint(NamedTuple):
    position: Vector
    thickness: float
    weights: list[HairPointWeight]

class HairStrandGroup(NamedTuple):
    points: list[HairPoint]
    strand_point_counts: list[int]

class HairPart:
    name: str
    master_strands: HairStrandGroup
    slave_strands: HairStrandGroup

    def __init__(self, name: str) -> None:
        self.name = name
        self.master_strands = HairStrandGroup([], [])
        self.slave_strands = HairStrandGroup([], [])

class Hair:
    model_id: int | None
    hair_data_id: int
    material_id: int | None
    parts: list[HairPart]

    def __init__(self, model_id: int | None, hair_data_id: int) -> None:
        self.model_id = model_id
        self.hair_data_id = hair_data_id
        self.material_id = None
        self.parts = []

    @property
    @abstractmethod
    def supports_strand_thickness(self) -> bool: ...

    @abstractmethod
    def read(self, reader: ResourceReader) -> None: ...

    @abstractmethod
    def write(self, writer: ResourceBuilder) -> None: ...
