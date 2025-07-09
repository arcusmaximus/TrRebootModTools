from ctypes import sizeof
from typing import Literal
from io_scene_tr_reboot.tr.BoneConstraint import IBoneConstraint
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import SkeletonBase
from io_scene_tr_reboot.tr.shadow.ShadowBone import ShadowBone
from io_scene_tr_reboot.tr.shadow.ShadowBoneConstraint import ShadowBoneConstraint
from io_scene_tr_reboot.util.CStruct import CArray, CByte, CLong, CShort, CStruct, CStruct64
from io_scene_tr_reboot.util.Conditional import coalesce
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _SkeletonHeader(CStruct64):
    bone_array_ref: ResourceReference | None
    num_animated_bones: CShort
    num_counterpart_ranges: CShort
    padding_0: CArray[CByte, Literal[4]]
    counterpart_ranges_ref: ResourceReference | None
    num_unused_words_1: CLong
    unused_words_1_ref: ResourceReference | None

    num_bone_id_mappings: CShort
    padding_1: CArray[CByte, Literal[6]]
    bone_id_mappings_ref: ResourceReference | None

    num_blend_shape_id_mappings: CByte
    padding_2: CArray[CByte, Literal[7]]
    blend_shape_id_mappings_ref: ResourceReference | None

    num_bone_constraints: CByte
    padding_3: CArray[CByte, Literal[7]]
    bone_constraint_refs_ref: ResourceReference | None

    num_non_animatable_bone_ids: CByte
    padding_4: CArray[CByte, Literal[7]]
    non_animatable_bone_ids_ref: ResourceReference | None

    bone_min_lod_levels_ref: ResourceReference | None
    local_bone_ids_by_lod_id_ref: ResourceReference | None
    lod_bone_ids_by_local_id_ref: ResourceReference | None

    num_animated_bones_for_lod: CArray[CShort, Literal[3]]
    padding_5: CShort

assert(sizeof(_SkeletonHeader) == 0x88)

class _CounterpartRange(CStruct):
    local_id_range_1_start: CShort
    local_id_range_2_start: CShort
    count: CShort

assert(sizeof(_CounterpartRange) == 6)

class ShadowSkeleton(SkeletonBase[ShadowBone]):
    def __init__(self, id: int) -> None:
        super().__init__(id)

    def read(self, reader: ResourceReader) -> None:
        header = reader.read_struct(_SkeletonHeader)
        if header.bone_array_ref is None:
            return

        reader.seek(header.bone_array_ref)
        num_bones = reader.read_int64()
        bones_ref = reader.read_ref()
        if bones_ref is None:
            return

        reader.seek(bones_ref)
        for _ in range(num_bones):
            tr_bone = reader.read_struct(ShadowBone)
            tr_bone.global_id = None
            tr_bone.counterpart_local_id = None
            tr_bone.constraints = []
            self.bones.append(tr_bone)

        self.read_id_mappings(header, reader)
        self.read_bone_counterparts(header, reader)
        self.read_bone_constraints(header, reader)

    def read_id_mappings(self, header: _SkeletonHeader, reader: ResourceReader) -> None:
        if header.bone_id_mappings_ref is not None:
            reader.seek(header.bone_id_mappings_ref)
            for _ in range(header.num_bone_id_mappings):
                global_bone_id = reader.read_uint16()
                local_bone_id = reader.read_uint16()
                self.bones[local_bone_id].global_id = global_bone_id

        if header.blend_shape_id_mappings_ref is not None:
            reader.seek(header.blend_shape_id_mappings_ref)
            for _ in range(header.num_blend_shape_id_mappings):
                global_blend_shape_id = reader.read_uint16()
                local_blend_shape_id = reader.read_uint16()
                self.global_blend_shape_ids[local_blend_shape_id] = global_blend_shape_id

    def read_bone_counterparts(self, header: _SkeletonHeader, reader: ResourceReader) -> None:
        if header.counterpart_ranges_ref is None:
            return

        reader.seek(header.counterpart_ranges_ref)
        while True:
            counterpart_range = reader.read_struct(_CounterpartRange)
            if counterpart_range.count == 0:
                break

            for i in range(counterpart_range.count):
                self.bones[counterpart_range.local_id_range_1_start + i].counterpart_local_id = counterpart_range.local_id_range_2_start + i

    def read_bone_constraints(self, header: _SkeletonHeader, reader: ResourceReader) -> None:
        if header.bone_constraint_refs_ref is None:
            return

        reader.seek(header.bone_constraint_refs_ref)
        for bone_constraint_ref in reader.read_ref_list(header.num_bone_constraints):
            if bone_constraint_ref is None:
                continue

            reader.seek(bone_constraint_ref)
            constraint = ShadowBoneConstraint.read(reader)
            self.bones[constraint.target_bone_local_id].constraints.append(constraint)

    def write(self, writer: ResourceBuilder) -> None:
        header = _SkeletonHeader()
        header.bone_array_ref = writer.make_internal_ref()
        header.num_animated_bones = Enumerable(self.bones).count(lambda b: b.global_id is not None)
        header.counterpart_ranges_ref = writer.make_internal_ref()
        header.unused_words_1_ref = None
        header.num_bone_id_mappings = header.num_animated_bones
        header.bone_id_mappings_ref = writer.make_internal_ref()
        header.num_blend_shape_id_mappings = Enumerable(self.global_blend_shape_ids.keys()).max() + 1 if len(self.global_blend_shape_ids) > 0 else 0
        header.blend_shape_id_mappings_ref = writer.make_internal_ref()
        header.num_bone_constraints = Enumerable(self.bones).sum(lambda b: len(b.constraints))
        header.bone_constraint_refs_ref = writer.make_internal_ref()
        header.non_animatable_bone_ids_ref = None
        header.bone_min_lod_levels_ref = None
        header.local_bone_ids_by_lod_id_ref = writer.make_internal_ref()
        header.lod_bone_ids_by_local_id_ref = writer.make_internal_ref()
        for i in range(3):
            header.num_animated_bones_for_lod[i] = header.num_animated_bones

        writer.write_struct(header)

        header.bone_array_ref.offset = writer.position
        writer.write_int64(len(self.bones))
        bones_ref = writer.write_internal_ref()

        writer.align(0x10)
        bones_ref.offset = writer.position
        writer.write_struct_list(self.bones)

        self.write_id_mappings(header, writer)
        self.write_bone_counterparts(header, writer)
        self.write_bone_constraints(header, writer)

        writer.write_struct(_CounterpartRange())

    def write_id_mappings(self, header: _SkeletonHeader, writer: ResourceBuilder) -> None:
        if header.bone_id_mappings_ref is None or \
           header.blend_shape_id_mappings_ref is None or \
           header.local_bone_ids_by_lod_id_ref is None or \
           header.lod_bone_ids_by_local_id_ref is None:
            raise Exception()

        header.bone_id_mappings_ref.offset = writer.position
        for local_bone_id, bone in enumerate(self.bones):
            if bone.global_id is not None:
                writer.write_uint16(bone.global_id)
                writer.write_uint16(local_bone_id)

        header.blend_shape_id_mappings_ref.offset = writer.position
        if len(self.global_blend_shape_ids) > 0:
            for local_id in range(Enumerable(self.global_blend_shape_ids.keys()).max() + 1):
                global_id = coalesce(self.global_blend_shape_ids.get(local_id), 0xFFFF)
                writer.write_uint16(global_id)
                writer.write_uint16(local_id)

        header.local_bone_ids_by_lod_id_ref.offset = writer.position
        header.lod_bone_ids_by_local_id_ref.offset = writer.position
        for i in range(header.num_animated_bones):
            writer.write_uint16(i)

    def write_bone_counterparts(self, header: _SkeletonHeader, writer: ResourceBuilder) -> None:
        if header.counterpart_ranges_ref is None:
            raise Exception()

        header.counterpart_ranges_ref.offset = writer.position
        for local_bone_id, bone in enumerate(self.bones):
            if bone.counterpart_local_id is not None:
                counterpart_range = _CounterpartRange()
                counterpart_range.local_id_range_1_start = local_bone_id
                counterpart_range.local_id_range_2_start = bone.counterpart_local_id
                counterpart_range.count = 1
                writer.write_struct(counterpart_range)

    def write_bone_constraints(self, header: _SkeletonHeader, writer: ResourceBuilder) -> None:
        if header.bone_constraint_refs_ref is None:
            raise Exception()

        header.bone_constraint_refs_ref.offset = writer.position
        constraint_refs: dict[IBoneConstraint, ResourceReference] = {}
        for local_bone_id, bone in enumerate(self.bones):
            for constraint in bone.constraints:
                constraint.target_bone_local_id = local_bone_id
                constraint_refs[constraint] = writer.write_internal_ref()

        for constraint, constraint_ref in constraint_refs.items():
            constraint_ref.offset = writer.position
            constraint.write(writer)
