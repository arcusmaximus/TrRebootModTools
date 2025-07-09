from abc import abstractmethod
from ctypes import sizeof
from enum import IntEnum
from typing import ClassVar, cast
from mathutils import Euler, Quaternion, Vector
from io_scene_tr_reboot.tr.BoneConstraint import BoneConstraintType, IBoneConstraint, IBoneConstraint_WeightedPosition, IBoneConstraint_WeightedRotation, IBoneConstraint_FromBlendShapes, IBoneConstraint_LookAt
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CByte, CShort, CStruct64
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.Serializer import Serializer

class _BoneConstraintCommon(CStruct64):
    type: CByte
    padding_0: CByte
    target_bone_local_id: CShort
    num_source_bones: CShort
    padding_1: CShort
    source_bone_local_ids_ref: ResourceReference | None
    source_bone_weights_ref: ResourceReference | None
    extra_data_ref: ResourceReference | None

assert(sizeof(_BoneConstraintCommon) == 0x20)

class _BoneConstraint_LookAt(CStruct64):
    unused_orientation: Quaternion
    inverse_bone_orientation: Quaternion
    bone_local_tangent: Vector
    bone_local_normal: Vector
    pole_dir: Vector
    pole_bone_orientation: Quaternion
    pole_bone_local_id: CShort
    pole_type: CByte

assert(sizeof(_BoneConstraint_LookAt) == 0x63)

class _LookAtPoleType(IntEnum):
    POLE_BONE_POS = 1
    POLE_BONE_ROT = 2
    POLE_DIR = 3
    Z_AXIS = 4

class _BoneConstraint_CopyPosition(CStruct64):
    offset: Vector

assert(sizeof(_BoneConstraint_CopyPosition) == 0x10)

class _BoneConstraint_CopyRotation(CStruct64):
    offset: Quaternion

assert(sizeof(_BoneConstraint_CopyRotation) == 0x10)

class _BoneConstraint_FromBlendShapes(CStruct64):
    position_offsets_ref: ResourceReference | None
    euler_offsets_ref: ResourceReference | None
    source_blend_shape_ids_ref: ResourceReference | None
    use_position_offsets: CByte
    use_euler_offsets: CByte

assert(sizeof(_BoneConstraint_FromBlendShapes) == 0x1A)

class ShadowBoneConstraint(IBoneConstraint):
    concrete_classes: ClassVar[list[type["ShadowBoneConstraint"]]] = []

    type: BoneConstraintType
    target_bone_local_id: int
    source_bone_local_ids: list[int]
    source_bone_weights: list[float]

    def __init__(self) -> None:
        self.target_bone_local_id = 0
        self.source_bone_local_ids = []
        self.source_bone_weights = []

    @staticmethod
    def read(reader: ResourceReader) -> "ShadowBoneConstraint":
        common_data = reader.read_struct(_BoneConstraintCommon)

        constraint = ShadowBoneConstraint.concrete_classes[common_data.type]()
        constraint.type = BoneConstraintType(common_data.type)
        constraint.target_bone_local_id = common_data.target_bone_local_id

        if common_data.source_bone_local_ids_ref is not None:
            reader.seek(common_data.source_bone_local_ids_ref)
            constraint.source_bone_local_ids = list(reader.read_uint16_list(common_data.num_source_bones))

        if common_data.source_bone_weights_ref is not None:
            reader.seek(common_data.source_bone_weights_ref)
            constraint.source_bone_weights = list(reader.read_float_list(common_data.num_source_bones))

        if common_data.extra_data_ref is not None:
            reader.seek(common_data.extra_data_ref)
            constraint.read_extra_data(reader)

        return constraint

    def read_extra_data(self, reader: ResourceReader) -> None:
        ...

    def write(self, writer: ResourceBuilder) -> None:
        common_data = _BoneConstraintCommon()
        common_data.type = ShadowBoneConstraint.concrete_classes.index(self.__class__)
        common_data.target_bone_local_id = self.target_bone_local_id
        common_data.num_source_bones = len(self.source_bone_local_ids)
        common_data.source_bone_local_ids_ref = writer.make_internal_ref()
        common_data.source_bone_weights_ref = writer.make_internal_ref()
        common_data.extra_data_ref = writer.make_internal_ref()
        writer.write_struct(common_data)

        common_data.source_bone_local_ids_ref.offset = writer.position
        writer.write_uint16_list(self.source_bone_local_ids)
        writer.align(4)

        common_data.source_bone_weights_ref.offset = writer.position
        writer.write_float_list(self.source_bone_weights)
        writer.align(0x10)

        common_data.extra_data_ref.offset = writer.position
        self.write_extra_data(writer)

    @abstractmethod
    def write_extra_data(self, writer: ResourceBuilder) -> None:
        ...

    def apply_bone_local_id_changes(self, mapping: dict[int, int]) -> None:
        new_target_bone_id = mapping.get(self.target_bone_local_id)
        if new_target_bone_id is not None:
            self.target_bone_local_id = new_target_bone_id

        for i, old_source_bone_id in enumerate(self.source_bone_local_ids):
            new_source_bone_id = mapping.get(old_source_bone_id)
            if new_source_bone_id is not None:
                self.source_bone_local_ids[i] = new_source_bone_id

    def serialize(self) -> str:
        return Serializer.serialize_object(self)

    @staticmethod
    def deserialize(data: str) -> "ShadowBoneConstraint":
        def create_constraint(values: dict[str, str]) -> object:
            type = ShadowBoneConstraint.concrete_classes[int(values["type"])]
            return type()

        return cast(ShadowBoneConstraint, Serializer.deserialize_object(data, create_constraint))

class ShadowBoneConstraint_LookAt(ShadowBoneConstraint, IBoneConstraint_LookAt):
    pole_bone_local_id: int | None
    pole_bone_orientation: Quaternion | None
    pole_dir: Vector | None

    bone_local_tangent: Vector
    bone_local_normal: Vector
    inverse_bone_orientation: Quaternion

    def __init__(self) -> None:
        super().__init__()
        self.pole_bone_local_id = None
        self.pole_bone_orientation = None   # type: ignore
        self.pole_dir = None                # type: ignore

        self.bone_local_tangent = Vector()
        self.bone_local_normal = Vector()
        self.inverse_bone_orientation = Quaternion()

    def read_extra_data(self, reader: ResourceReader) -> None:
        extra_data = reader.read_struct(_BoneConstraint_LookAt)
        match extra_data.pole_type:
            case _LookAtPoleType.POLE_BONE_POS:
                self.pole_bone_local_id = extra_data.pole_bone_local_id
                self.pole_bone_orientation = None    # type: ignore
                self.pole_dir = None                # type: ignore
            case _LookAtPoleType.POLE_BONE_ROT:
                self.pole_bone_local_id = extra_data.pole_bone_local_id
                self.pole_bone_orientation = extra_data.pole_bone_orientation
                self.pole_dir = extra_data.pole_dir
            case _LookAtPoleType.POLE_DIR:
                self.pole_bone_local_id = None
                self.pole_bone_orientation = None    # type: ignore
                self.pole_dir = extra_data.pole_dir
            case _:
                self.pole_bone_local_id = None
                self.pole_bone_orientation = None    # type: ignore
                self.pole_dir = None                # type: ignore

        self.bone_local_tangent = extra_data.bone_local_tangent
        self.bone_local_normal = extra_data.bone_local_normal
        self.inverse_bone_orientation = extra_data.inverse_bone_orientation

    def write_extra_data(self, writer: ResourceBuilder) -> None:
        extra_data = _BoneConstraint_LookAt()
        extra_data.unused_orientation = Quaternion()
        extra_data.pole_bone_orientation = Quaternion()
        extra_data.pole_dir = Vector()

        if self.pole_bone_local_id is not None and self.pole_bone_orientation is None and self.pole_dir is None:
            extra_data.pole_type = _LookAtPoleType.POLE_BONE_POS
            extra_data.pole_bone_local_id = self.pole_bone_local_id
            extra_data.pole_bone_orientation = Quaternion()
            extra_data.pole_dir = Vector()
        elif self.pole_bone_local_id is not None and self.pole_bone_orientation is not None and self.pole_dir is not None:
            extra_data.pole_type = _LookAtPoleType.POLE_BONE_ROT
            extra_data.pole_bone_local_id = self.pole_bone_local_id
            extra_data.pole_bone_orientation = self.pole_bone_orientation
            extra_data.pole_dir = self.pole_dir
        elif self.pole_bone_local_id is None and self.pole_bone_orientation is None and self.pole_dir is not None:
            extra_data.pole_type = _LookAtPoleType.POLE_DIR
            extra_data.pole_bone_local_id = -1
            extra_data.pole_bone_orientation = Quaternion()
            extra_data.pole_dir = Vector()
        else:
            extra_data.pole_type = _LookAtPoleType.Z_AXIS
            extra_data.pole_bone_local_id = -1
            extra_data.pole_bone_orientation = Quaternion()
            extra_data.pole_dir = Vector()

        extra_data.bone_local_tangent = self.bone_local_tangent
        extra_data.bone_local_normal = self.bone_local_normal
        extra_data.inverse_bone_orientation = self.inverse_bone_orientation

        writer.write_struct(extra_data)
        writer.align(0x10)

class ShadowBoneConstraint_WeightedPosition(ShadowBoneConstraint, IBoneConstraint_WeightedPosition):
    offset: Vector

    def __init__(self) -> None:
        super().__init__()
        self.offset = Vector()

    def read_extra_data(self, reader: ResourceReader) -> None:
        extra_data = reader.read_struct(_BoneConstraint_CopyPosition)
        self.offset = extra_data.offset

    def write_extra_data(self, writer: ResourceBuilder) -> None:
        extra_data = _BoneConstraint_CopyPosition()
        extra_data.offset = self.offset
        writer.write_struct(extra_data)

class ShadowBoneConstraint_WeightedRotation(ShadowBoneConstraint, IBoneConstraint_WeightedRotation):
    offset: Quaternion

    def __init__(self) -> None:
        super().__init__()
        self.offset = Quaternion()

    def read_extra_data(self, reader: ResourceReader) -> None:
        extra_data = reader.read_struct(_BoneConstraint_CopyRotation)
        self.offset = extra_data.offset

    def write_extra_data(self, writer: ResourceBuilder) -> None:
        extra_data = _BoneConstraint_CopyRotation()
        extra_data.offset = self.offset
        writer.write_struct(extra_data)

class ShadowBoneConstraint_FromBlendShapes(ShadowBoneConstraint, IBoneConstraint_FromBlendShapes):
    position_offsets: list[Vector] | None
    rotation_offsets: list[Quaternion] | None
    source_blend_shape_ids: list[int]

    def __init__(self) -> None:
        super().__init__()
        self.position_offsets = None
        self.rotation_offsets = None
        self.source_blend_shape_ids = []

    def read_extra_data(self, reader: ResourceReader) -> None:
        extra_data = reader.read_struct(_BoneConstraint_FromBlendShapes)

        if extra_data.position_offsets_ref is not None and extra_data.use_position_offsets != 0:
            reader.seek(extra_data.position_offsets_ref)
            self.position_offsets = reader.read_vec4d_list(len(self.source_bone_local_ids))

        if extra_data.euler_offsets_ref is not None and extra_data.use_euler_offsets != 0:
            reader.seek(extra_data.euler_offsets_ref)
            eulers = reader.read_vec4d_list(len(self.source_bone_local_ids))
            self.rotation_offsets = Enumerable(eulers).select(self.euler_to_quat).to_list()

        if extra_data.source_blend_shape_ids_ref is not None:
            reader.seek(extra_data.source_blend_shape_ids_ref)
            self.source_blend_shape_ids = list(reader.read_uint16_list(len(self.source_bone_local_ids)))

    def write_extra_data(self, writer: ResourceBuilder) -> None:
        extra_data = _BoneConstraint_FromBlendShapes()
        extra_data.position_offsets_ref = writer.make_internal_ref() if self.position_offsets is not None else None
        extra_data.euler_offsets_ref = writer.make_internal_ref() if self.rotation_offsets is not None else None
        extra_data.source_blend_shape_ids_ref = writer.make_internal_ref()
        extra_data.use_position_offsets = 1 if self.position_offsets is not None else 0
        extra_data.use_euler_offsets = 1 if self.rotation_offsets is not None else 0
        writer.write_struct(extra_data)
        writer.align(0x10)

        if self.position_offsets is not None and extra_data.position_offsets_ref is not None:
            extra_data.position_offsets_ref.offset = writer.position
            writer.write_vec4d_list(self.position_offsets)

        if self.rotation_offsets is not None and extra_data.euler_offsets_ref is not None:
            extra_data.euler_offsets_ref.offset = writer.position
            eulers = Enumerable(self.rotation_offsets).select(self.quat_to_euler).to_list()
            writer.write_vec4d_list(eulers)

        extra_data.source_blend_shape_ids_ref.offset = writer.position
        writer.write_uint16_list(self.source_blend_shape_ids)

        writer.align(0x10)

    def euler_to_quat(self, euler: Vector) -> Quaternion:
        return Euler((euler.x, euler.y, euler.z)).to_quaternion()

    def quat_to_euler(self, quat: Quaternion) -> Vector:
        euler = quat.to_euler()
        return Vector((euler.x, euler.y, euler.z))

ShadowBoneConstraint.concrete_classes = [
    ShadowBoneConstraint_LookAt,
    ShadowBoneConstraint_WeightedPosition,
    ShadowBoneConstraint_WeightedRotation,
    ShadowBoneConstraint_FromBlendShapes
]
