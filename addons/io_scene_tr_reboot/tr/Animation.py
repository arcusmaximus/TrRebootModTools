from abc import abstractmethod
import math
from typing import TYPE_CHECKING, ClassVar, NamedTuple, Protocol, Sequence, cast
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.util.SlotsBase import SlotsBase
from mathutils import Matrix, Quaternion, Vector

class AnimationBoneInfo(NamedTuple):
    rest_matrix: Matrix
    parent_global_id: int | None

class IAnimationFrame(Protocol):
    def get_attr(self, attr_idx: int) -> Sequence[float] | None: ...
    def get_attr_raw(self, attr_idx: int) -> Sequence[float] | None: ...

    def set_attr(self, attr_idx: int, value: Sequence[float]) -> None: ...
    def set_attr_raw(self, attr_idx: int, value: Sequence[float]) -> None: ...

class BoneAnimationFrame(SlotsBase, IAnimationFrame if TYPE_CHECKING else object):
    rotation_angle_factor: ClassVar[float]
    position_factor: ClassVar[float]

    rotation: Quaternion | Vector | None
    position: Vector | None
    scale:    Vector | None

    position_offset: Vector

    def __init__(self, position_offset: Vector) -> None:
        self.rotation = None        # type: ignore
        self.position = None        # type: ignore
        self.scale    = None        # type: ignore

        self.position_offset = position_offset

    def get_attr(self, attr_idx: int) -> Sequence[float] | None:
        match attr_idx:
            case 0:
                return cast(Sequence[float] | None, self.get_rotation_as_quat())

            case 1:
                return cast(Sequence[float] | None, self.position)

            case 2:
                return cast(Sequence[float] | None, self.scale)

            case _:
                raise Exception("Invalid attribute index")

    def get_attr_raw(self, attr_idx: int) -> Sequence[float] | None:
        match attr_idx:
            case 0:
                return cast(Sequence[float] | None, self.get_rotation_as_axis_angle())

            case 1:
                if self.position is None:
                    return None

                return cast(Sequence[float], self.position / self.position_factor - self.position_offset)

            case 2:
                return cast(Sequence[float] | None, self.scale)

            case _:
                return None

    def set_attr(self, attr_idx: int, value: Sequence[float]) -> None:
        match attr_idx:
            case 0:
                self.rotation = Quaternion(value)   # type: ignore

            case 1:
                self.position = Vector(value)

            case 2:
                self.scale = Vector(value)

            case _:
                raise Exception("Invalid attribute index")

    def set_attr_raw(self, attr_idx: int, value: Sequence[float]) -> None:
        match attr_idx:
            case 0:
                self.rotation = Vector(value)       # type: ignore

            case 1:
                self.position = (Vector(value) + self.position_offset) * self.position_factor

            case 2:
                self.scale = Vector(value)

            case _:
                pass

    def set_attr_elem_raw(self, attr_idx: int, elem_idx: int, value: float) -> None:
        match attr_idx:
            case 0:
                if self.rotation is None:
                    self.rotation = Vector()        # type: ignore

                self.rotation[elem_idx] = value

            case 1:
                if self.position is None:
                    self.position = Vector()

                self.position[elem_idx] = (value + self.position_offset[elem_idx]) * self.position_factor

            case 2:
                if self.scale is None:
                    self.scale = Vector((1, 1, 1))

                self.scale[elem_idx] = value

            case _:
                raise Exception("Invalid attribute index")

    def get_rotation_as_quat(self) -> Quaternion | None:
        if self.rotation is None or isinstance(self.rotation, Quaternion):
            return self.rotation

        angle = self.rotation.length * self.rotation_angle_factor
        if angle < 0.00000001:
            self.rotation = Quaternion()    # type: ignore
            return

        w = math.cos(angle / 2)
        xyz = self.rotation
        xyz.normalize()
        xyz *= math.sin(angle / 2)
        self.rotation = Quaternion((w, xyz.x, xyz.y, xyz.z))    # type: ignore
        return self.rotation

    def get_rotation_as_axis_angle(self) -> Vector | None:
        if self.rotation is None or isinstance(self.rotation, Vector):
            return self.rotation

        quat = self.rotation
        if 1 - quat.w < 0.00000001:
            return Vector((0, 0, 0))

        angle = math.acos(quat.w) * 2
        self.rotation = Vector((quat.x, quat.y, quat.z)) * (angle / self.rotation_angle_factor / math.sin(angle / 2))   # type: ignore
        return self.rotation

class BlendShapeAnimationFrame(SlotsBase, IAnimationFrame if TYPE_CHECKING else object):
    value: float

    def __init__(self) -> None:
        self.value = 0

    def get_attr(self, attr_idx: int) -> Sequence[float] | None:
        return [self.value]

    def get_attr_raw(self, attr_idx: int) -> Sequence[float] | None:
        return [self.value]

    def set_attr(self, attr_idx: int, value: Sequence[float]) -> None:
        self.value = value[0]

    def set_attr_raw(self, attr_idx: int, value: Sequence[float]) -> None:
        self.value = value[0]

class Animation:
    id: int
    ms_per_frame: int
    num_frames: int
    bone_tracks: dict[int, list[BoneAnimationFrame]]
    blend_shape_tracks: dict[int, list[BlendShapeAnimationFrame]]
    bone_infos: dict[int, AnimationBoneInfo]

    def __init__(self, id: int, bone_infos: dict[int, AnimationBoneInfo]) -> None:
        self.id = id
        self.ms_per_frame = 100
        self.num_frames = 0
        self.bone_tracks = {}
        self.blend_shape_tracks = {}
        self.bone_infos = bone_infos

    @abstractmethod
    def create_bone_frame(self, global_bone_id: int) -> BoneAnimationFrame: ...

    @abstractmethod
    def read(self, reader: ResourceReader) -> None: ...

    @abstractmethod
    def write(self, writer: ResourceBuilder) -> None: ...
