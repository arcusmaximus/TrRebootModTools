from abc import abstractmethod
from typing import NamedTuple
from mathutils import Vector
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader

class CollisionFace(NamedTuple):
    indices: list[int]
    material_idx: int

class CollisionBoundingBoxNode(NamedTuple):
    min: Vector
    max: Vector
    children: "list[CollisionBoundingBoxNode]"
    first_face_idx: int
    num_faces: int

class CollisionMesh:
    vertices: list[Vector]
    faces: list["CollisionFace"]
    root_bounding_box_node: CollisionBoundingBoxNode | None
    material_ids: list[int]
    collision_type_id: int | None

    def __init__(self) -> None:
        self.vertices = []
        self.faces = []
        self.root_bounding_box_node = None
        self.material_ids = []
        self.collision_type_id = None

class CollisionModel:
    meshes: list[CollisionMesh]

    def __init__(self) -> None:
        self.meshes = []

    @abstractmethod
    def read(self, reader: ResourceReader) -> None: ...

    @abstractmethod
    def write(self, writer: ResourceBuilder) -> None: ...
