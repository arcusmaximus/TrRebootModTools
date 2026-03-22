from array import array
from ctypes import sizeof
import math
from mathutils import Vector
from typing import Callable, ClassVar, NamedTuple, Sequence, TypeVar, cast

from io_scene_tr_reboot.tr.Animation import Animation, BlendShapeAnimationFrame, BoneAnimationFrame, IAnimationFrame
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.BitStreamWriter import BitStreamWriter
from io_scene_tr_reboot.util.BitstreamReader import BitstreamReader
from io_scene_tr_reboot.util.CStruct import CByte, CFlag, CInt, CLong, CShort, CStruct64, CUShort
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _AnimationDataRefs(CStruct64):
    absence_flags_ref: ResourceReference | None
    fixation_flags_ref: ResourceReference | None
    fixed_attrs_ref: ResourceReference | None
    adjustment_floats_ref: ResourceReference | None
    frame_batch_sizes_ref: ResourceReference | None
    frame_batches_ref: ResourceReference | None

class _AnimationHeader(CStruct64):
    field_0: CLong
    field_8: CLong
    field_10: CLong
    base_position: CVec3
    field_24: CInt
    field_28: CLong
    base_rotation: CVec3
    field_3C: CShort
    num_frames: CUShort
    ms_per_frame: CUShort
    num_bones: CUShort
    field_44: CInt
    field_48: CShort
    num_blend_shapes: CByte
    field_4B: CByte
    flags: CUShort
    num_bone_frame_batches: CUShort
    num_blend_shape_frame_batches: CUShort
    field_52: CShort
    field_54: CInt
    field_58: CLong
    field_60: CLong
    bone_distances_from_parents_ref: ResourceReference | None
    field_70: CLong
    bone_held_frame_numbers_ref: ResourceReference | None
    global_bone_ids_ref: ResourceReference | None
    field_88: CLong
    field_90: CLong
    field_98: CLong
    global_blend_shape_ids_ref: ResourceReference | None
    field_A8: CLong
    field_B0: CLong
    bone_data_refs: _AnimationDataRefs
    blend_shape_data_refs: _AnimationDataRefs
    field_118: CLong
    field_120: CLong
    unknown_ref: ResourceReference | None

    has_bone_scale = CFlag("flags", 0x200)

assert(sizeof(_AnimationHeader) == 0x130)

class _ItemFrameKey(NamedTuple):
    item_idx: int
    frame_idx: int

class _ItemAttributeKey(NamedTuple):
    item_idx: int
    attr_idx: int

class _AnimationDataHeader(NamedTuple):
    fixed_attrs: dict[_ItemAttributeKey, Sequence[float]]
    animated_attr_keys: Sequence[_ItemAttributeKey]
    adjustment_floats: Sequence[float]
    frame_batch_sizes: Sequence[int]

class ShadowBoneAnimationFrame(BoneAnimationFrame):
    rotation_angle_factor = math.pi
    position_factor = 100.0

    def __init__(self) -> None:
        super().__init__(Vector())

class ShadowAnimation(Animation):
    bone_attr_element_size_mapping:        ClassVar[list[int]] = [0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 23]
    blend_shape_attr_element_size_mapping: ClassVar[list[int]] = [0, 1, 2, 3, 4, 5, 6, 7, 8,  9,  10, 11, 12, 14, 16, 23]

    bone_distances_from_parent: Sequence[float]

    def __init__(self, id: int) -> None:
        super().__init__(id, {})
        self.bone_distances_from_parent = []

    def create_bone_frame(self, global_bone_id: int) -> BoneAnimationFrame:
        return ShadowBoneAnimationFrame()

    def read(self, reader: ResourceReader) -> None:
        header = reader.read_struct(_AnimationHeader)
        self.ms_per_frame = header.ms_per_frame
        self.num_frames = header.num_frames

        if header.bone_distances_from_parents_ref is not None:
            reader.seek(header.bone_distances_from_parents_ref)
            self.bone_distances_from_parent = reader.read_float_list(header.num_bones)

        #if header.bone_held_frame_numbers_ref is not None:
        #    reader.seek(header.bone_held_frame_numbers_ref)
        #    while True:
        #        held_frame_num = reader.read_int32()
        #        if held_frame_num == 0:
        #            break

        self.read_bone_frames(header, reader)
        self.read_blend_shape_frames(header, reader)

    def read_bone_frames(self, header: _AnimationHeader, reader: ResourceReader) -> None:
        if header.global_bone_ids_ref is None:
            return

        reader.seek(header.global_bone_ids_ref)
        global_bone_ids = reader.read_uint16_list(header.num_bones)
        for global_bone_id in global_bone_ids:
            self.bone_tracks[global_bone_id] = []

        def fetch_bone_frame(bone_frame_key: _ItemFrameKey) -> BoneAnimationFrame:
            global_bone_id = global_bone_ids[bone_frame_key.item_idx]
            bone_frames = self.bone_tracks[global_bone_id]
            if len(bone_frames) <= bone_frame_key.frame_idx:
                bone_frames.append(self.create_bone_frame(global_bone_id))

            return bone_frames[bone_frame_key.frame_idx]

        self.read_animation_data(
            header.bone_data_refs,
            header.num_bone_frame_batches,
            header.num_bones,
            header.has_bone_scale and 3 or 2,
            3,
            ShadowAnimation.bone_attr_element_size_mapping,
            fetch_bone_frame,
            reader
        )

    def read_blend_shape_frames(self, header: _AnimationHeader, reader: ResourceReader) -> None:
        if header.global_blend_shape_ids_ref is None:
            return

        reader.seek(header.global_blend_shape_ids_ref)
        global_blend_shape_ids = reader.read_uint16_list(header.num_blend_shapes)
        for global_blend_shape_id in global_blend_shape_ids:
            self.blend_shape_tracks[global_blend_shape_id] = []

        def fetch_frame_blend_shape(blend_shape_frame_key: _ItemFrameKey) -> BlendShapeAnimationFrame:
            blend_shape_frames = self.blend_shape_tracks[global_blend_shape_ids[blend_shape_frame_key.item_idx]]
            if len(blend_shape_frames) <= blend_shape_frame_key.frame_idx:
                blend_shape_frames.append(BlendShapeAnimationFrame())

            return blend_shape_frames[blend_shape_frame_key.frame_idx]

        self.read_animation_data(
            header.blend_shape_data_refs,
            header.num_blend_shape_frame_batches,
            header.num_blend_shapes,
            1,
            1,
            ShadowAnimation.blend_shape_attr_element_size_mapping,
            fetch_frame_blend_shape,
            reader
        )

    def read_animation_data(
        self,
        data_refs: _AnimationDataRefs,
        num_frame_batches: int,
        num_items: int,
        num_attrs_per_item: int,
        num_elements_per_attr: int,
        element_size_mapping: list[int],
        fetch_item_frame: Callable[[_ItemFrameKey], IAnimationFrame],
        reader: ResourceReader
    ) -> None:
        data_header = self.read_animation_data_header(data_refs, num_frame_batches, num_items, num_attrs_per_item, num_elements_per_attr, reader)
        if data_header is None or data_refs.frame_batches_ref is None:
            return

        num_animated_attrs = len(data_header.animated_attr_keys)

        reader.seek(data_refs.frame_batches_ref)
        next_frame_batch_start_pos = reader.position

        element_sizes_per_attr = [0] * num_animated_attrs
        adjustment_bytes: Sequence[int]
        bitstream_reader = BitstreamReader(reader.data)

        for frame_batch_idx, frame_batch_size in enumerate(data_header.frame_batch_sizes):
            reader.position = next_frame_batch_start_pos
            next_frame_batch_start_pos += frame_batch_size * 4

            packed_encoded_attr_element_sizes = reader.read_uint32_list((num_animated_attrs * 4 + 31) // 32)
            for item_attr_idx in range(num_animated_attrs):
                encoded_attr_element_size = (packed_encoded_attr_element_sizes[item_attr_idx // 8] >> (28 - (item_attr_idx % 8) * 4)) & 0xF
                element_sizes_per_attr[item_attr_idx] = element_size_mapping[encoded_attr_element_size]

            adjustment_bytes = reader.read_bytes(num_animated_attrs * num_elements_per_attr * 2)
            adjustment_bytes_as_float = Enumerable(adjustment_bytes).select(lambda b: b / 255.0).to_list()
            reader.align(4)

            bitstream_reader.seek(reader.position)

            for frame_idx in range(frame_batch_idx * 16, min((frame_batch_idx + 1) * 16, self.num_frames)):
                for item_attr_key, fixed_attr_value in data_header.fixed_attrs.items():
                    item_frame = fetch_item_frame(_ItemFrameKey(item_attr_key.item_idx, frame_idx))
                    item_frame.set_attr_raw(item_attr_key.attr_idx, fixed_attr_value)

                adjustment_floats_idx = 0
                adjustment_bytes_idx = 0
                for item_attr_idx, item_attr_key in enumerate(data_header.animated_attr_keys):
                    element_size = element_sizes_per_attr[item_attr_idx]
                    attr_value: list[float] = []
                    for _ in range(num_elements_per_attr):
                        element_value: float
                        if element_size == 0:
                            element_value = (adjustment_bytes[adjustment_bytes_idx + 1] << 8) | adjustment_bytes[adjustment_bytes_idx]
                            element_value /= 0xFFFF
                        else:
                            element_value = bitstream_reader.read(element_size)
                            element_value /= (1 << element_size) - 1
                            element_value = element_value * adjustment_bytes_as_float[adjustment_bytes_idx + 1] + adjustment_bytes_as_float[adjustment_bytes_idx]

                        element_value = element_value * data_header.adjustment_floats[adjustment_floats_idx + num_elements_per_attr] + data_header.adjustment_floats[adjustment_floats_idx]
                        attr_value.append(element_value)
                        adjustment_floats_idx += 1
                        adjustment_bytes_idx += 2

                    adjustment_floats_idx += num_elements_per_attr

                    item_frame = fetch_item_frame(_ItemFrameKey(item_attr_key.item_idx, frame_idx))
                    item_frame.set_attr_raw(item_attr_key.attr_idx, attr_value)

    def read_animation_data_header(
        self,
        data_refs: _AnimationDataRefs,
        num_frame_batches: int,
        num_items: int,
        num_attrs_per_item: int,
        num_elements_per_attr: int,
        reader: ResourceReader
    ) -> _AnimationDataHeader | None:
        if data_refs.absence_flags_ref  is None or      \
           data_refs.fixation_flags_ref is None or      \
           data_refs.fixed_attrs_ref    is None or      \
           data_refs.adjustment_floats_ref is None or   \
           data_refs.frame_batch_sizes_ref is None or   \
           data_refs.frame_batches_ref is None:
            return None

        reader.seek(data_refs.absence_flags_ref)
        absence_flags = reader.read_uint32_list((num_items * num_attrs_per_item + 31) // 32)

        reader.seek(data_refs.fixation_flags_ref)
        fixation_flags = reader.read_uint32_list((num_items * num_attrs_per_item + 31) // 32)

        reader.seek(data_refs.fixed_attrs_ref)
        fixed_attrs: dict[_ItemAttributeKey, Sequence[float]] = {}
        animated_attrs: list[_ItemAttributeKey] = []

        flag_idx = 0
        for item_idx in range(num_items):
            for attr_idx in range(num_attrs_per_item):
                flag_int_idx, flag_int_bit_idx = divmod(flag_idx, 32)
                flag_idx += 1

                is_absent = (absence_flags[flag_int_idx]  >> (31 - flag_int_bit_idx)) & 1
                is_fixed  = (fixation_flags[flag_int_idx] >> (31 - flag_int_bit_idx)) & 1
                if is_absent:
                    continue

                if is_fixed:
                    fixed_attrs[_ItemAttributeKey(item_idx, attr_idx)] = reader.read_float_list(num_elements_per_attr)
                else:
                    animated_attrs.append(_ItemAttributeKey(item_idx, attr_idx))

        reader.seek(data_refs.adjustment_floats_ref)
        adjustment_floats = reader.read_float_list(len(animated_attrs) * num_elements_per_attr * 2)

        reader.seek(data_refs.frame_batch_sizes_ref)
        frame_batch_sizes: Sequence[int]
        if num_frame_batches > 0:
            frame_batch_sizes = reader.read_uint16_list(num_frame_batches)
        else:
            frame_batch_sizes = [0]

        return _AnimationDataHeader(fixed_attrs, animated_attrs, adjustment_floats, frame_batch_sizes)

    def write(self, writer: ResourceBuilder) -> None:
        header = _AnimationHeader()
        header.ms_per_frame = self.ms_per_frame
        header.num_frames = self.num_frames
        header.bone_held_frame_numbers_ref = None
        header.num_bone_frame_batches        = len(self.bone_tracks) > 0        and (self.num_frames + 15) // 16 or 0
        header.num_blend_shape_frame_batches = len(self.blend_shape_tracks) > 0 and (self.num_frames + 15) // 16 or 0
        header.base_position = CVec3()
        header.base_rotation = CVec3()

        header.num_bones = len(self.bone_tracks)
        header.global_bone_ids_ref = writer.make_internal_ref()
        header.bone_distances_from_parents_ref = writer.make_internal_ref()
        header.bone_data_refs = self.create_animation_data_refs(writer)

        header.num_blend_shapes = len(self.blend_shape_tracks)
        header.global_blend_shape_ids_ref = writer.make_internal_ref()
        header.blend_shape_data_refs = self.create_animation_data_refs(writer)

        header.flags = 0x304
        header.unknown_ref = writer.make_internal_ref()

        writer.write_struct(header)

        header.bone_distances_from_parents_ref.offset = writer.position
        writer.write_float_list(self.bone_distances_from_parent)

        header.global_bone_ids_ref.offset = writer.position
        writer.write_uint16_list(list(self.bone_tracks.keys()))
        writer.align(4)

        header.global_blend_shape_ids_ref.offset = writer.position
        writer.write_uint16_list(list(self.blend_shape_tracks.keys()))
        writer.align(4)

        encoded_element_size = 14
        self.write_animation_data(
            self.bone_tracks,
            3,
            3,
            encoded_element_size,
            ShadowAnimation.bone_attr_element_size_mapping[encoded_element_size],
            header.bone_data_refs,
            writer
        )
        self.write_animation_data(
            self.blend_shape_tracks,
            1,
            1,
            encoded_element_size,
            ShadowAnimation.blend_shape_attr_element_size_mapping[encoded_element_size],
            header.blend_shape_data_refs,
            writer
        )

        header.unknown_ref.offset = writer.position
        writer.write_int32(0)

    def create_animation_data_refs(self, writer: ResourceBuilder) -> _AnimationDataRefs:
        refs = _AnimationDataRefs()
        refs.absence_flags_ref      = writer.make_internal_ref()
        refs.fixation_flags_ref     = writer.make_internal_ref()
        refs.fixed_attrs_ref        = writer.make_internal_ref()
        refs.adjustment_floats_ref  = writer.make_internal_ref()
        refs.frame_batch_sizes_ref  = writer.make_internal_ref()
        refs.frame_batches_ref      = writer.make_internal_ref()
        return refs

    TAnimationFrame = TypeVar("TAnimationFrame", bound = IAnimationFrame)

    def write_animation_data(
        self,
        item_frames: dict[int, list[TAnimationFrame]],
        num_attrs_per_item: int,
        num_elements_per_attr: int,
        encoded_element_size: int,
        element_size: int,
        data_refs: _AnimationDataRefs,
        writer: ResourceBuilder
    ) -> None:
        if data_refs.frame_batches_ref is None:
            return

        data_header = self.collect_animation_data_header(item_frames, num_attrs_per_item, num_elements_per_attr, element_size)
        self.write_animation_data_header(data_header, data_refs, len(item_frames), num_attrs_per_item, writer)

        data_refs.frame_batches_ref.offset = writer.position

        num_animated_attrs = len(data_header.animated_attr_keys)
        packed_encoded_element_sizes = array("I", [0]) * ((num_animated_attrs * 4 + 31) // 32)
        adjustment_bytes = bytearray([0]) * (num_animated_attrs * num_elements_per_attr * 2)
        for item_attr_idx in range(num_animated_attrs):
            packed_encoded_element_sizes[item_attr_idx // 8] |= encoded_element_size << (28 - (item_attr_idx % 8) * 4)
            for elem_idx in range(num_elements_per_attr):
                adjustment_bytes[(item_attr_idx * num_elements_per_attr + elem_idx) * 2 + 1] = 0xFF

        item_ids = list(item_frames.keys())
        for frame_batch_idx in range((self.num_frames + 15) // 16):
            writer.write_uint32_list(packed_encoded_element_sizes)
            writer.write_bytes(adjustment_bytes)
            writer.align(4)

            bitstream_writer = BitStreamWriter(writer.stream)
            for frame_idx in range(frame_batch_idx * 16, min((frame_batch_idx + 1) * 16, self.num_frames)):
                adjustment_floats_idx = 0
                for attr_key in data_header.animated_attr_keys:
                    attr_value = item_frames[item_ids[attr_key.item_idx]][frame_idx].get_attr_raw(attr_key.attr_idx)
                    if attr_value is None:
                        raise Exception()

                    for elem_idx in range(num_elements_per_attr):
                        offset_adjustment_float = data_header.adjustment_floats[adjustment_floats_idx]
                        scale_adjustment_float  = data_header.adjustment_floats[adjustment_floats_idx + num_elements_per_attr]
                        element_value = (attr_value[elem_idx] - offset_adjustment_float) / scale_adjustment_float
                        element_value = int(element_value * ((1 << element_size) - 1))
                        bitstream_writer.write(element_value, element_size)
                        adjustment_floats_idx += 1

                    adjustment_floats_idx += num_elements_per_attr

            bitstream_writer.flush()
            writer.align(4)

        writer.write_uint64(0)

    def write_animation_data_header(
        self,
        data_header: _AnimationDataHeader,
        data_refs: _AnimationDataRefs,
        num_items: int,
        num_attrs_per_item: int,
        writer: ResourceBuilder
    ) -> None:
        if data_refs.absence_flags_ref      is None or \
           data_refs.fixation_flags_ref     is None or \
           data_refs.fixed_attrs_ref        is None or \
           data_refs.adjustment_floats_ref  is None or \
           data_refs.frame_batch_sizes_ref  is None:
            return

        animated_attr_keys_set = set(data_header.animated_attr_keys)
        absence_flags  = array("I", [0]) * ((num_items * num_attrs_per_item + 31) // 32)
        fixation_flags = array("I", [0]) * ((num_items * num_attrs_per_item + 31) // 32)
        flag_int_idx = 0
        flag_int_bit = 1 << 31
        absence_flags_int = 0
        fixation_flags_int = 0
        for item_idx in range(num_items):
            for attr_idx in range(num_attrs_per_item):
                attr_key = _ItemAttributeKey(item_idx, attr_idx)
                if attr_key in data_header.fixed_attrs:
                    fixation_flags_int |= flag_int_bit
                elif attr_key not in animated_attr_keys_set:
                    absence_flags_int |= flag_int_bit

                flag_int_bit >>= 1
                if flag_int_bit == 0:
                    absence_flags[flag_int_idx] = absence_flags_int
                    fixation_flags[flag_int_idx] = fixation_flags_int
                    absence_flags_int = 0
                    fixation_flags_int = 0
                    flag_int_idx += 1
                    flag_int_bit = 1 << 31

        if flag_int_idx < len(absence_flags):
            absence_flags[flag_int_idx]  = absence_flags_int
            fixation_flags[flag_int_idx] = fixation_flags_int

        data_refs.absence_flags_ref.offset = writer.position
        writer.write_uint32_list(absence_flags)

        data_refs.fixation_flags_ref.offset = writer.position
        writer.write_uint32_list(fixation_flags)

        data_refs.fixed_attrs_ref.offset = writer.position
        for fixed_attr_value in data_header.fixed_attrs.values():
            writer.write_float_list(fixed_attr_value)

        data_refs.adjustment_floats_ref.offset = writer.position
        writer.write_float_list(data_header.adjustment_floats)

        data_refs.frame_batch_sizes_ref.offset = writer.position
        writer.write_uint16_list(data_header.frame_batch_sizes)
        writer.align(4)

    def collect_animation_data_header(
        self,
        item_frames: dict[int, list[TAnimationFrame]],
        num_attrs_per_item: int,
        num_elements_per_attr: int,
        element_size_in_bits: int
    ) -> _AnimationDataHeader:
        fixed_attrs: dict[_ItemAttributeKey, list[float]] = {}
        animated_attr_min_values: dict[_ItemAttributeKey, list[float]] = {}
        animated_attr_max_values: dict[_ItemAttributeKey, list[float]] = {}

        for item_idx, global_item_id in enumerate(item_frames.keys()):
            for attr_idx in range(num_attrs_per_item):
                for item_frame in item_frames[global_item_id]:
                    attr_value = item_frame.get_attr_raw(attr_idx)
                    if attr_value is None:
                        continue

                    attr_key = _ItemAttributeKey(item_idx, attr_idx)
                    if attr_key not in animated_attr_min_values:
                        existing_fixed_attr_value = fixed_attrs.get(attr_key)
                        if existing_fixed_attr_value is None:
                            fixed_attrs[attr_key] = list(attr_value)
                        elif (tuple(existing_fixed_attr_value) != tuple(attr_value)):
                            animated_attr_min_values[attr_key] = list(fixed_attrs[attr_key])
                            animated_attr_max_values[attr_key] = list(fixed_attrs[attr_key])
                            del fixed_attrs[attr_key]

                    if attr_key in animated_attr_min_values:
                        min_attr_value = animated_attr_min_values[attr_key]
                        max_attr_value = animated_attr_max_values[attr_key]
                        for elem_idx in range(num_elements_per_attr):
                            min_attr_value[elem_idx] = min(min_attr_value[elem_idx], attr_value[elem_idx])
                            max_attr_value[elem_idx] = max(max_attr_value[elem_idx], attr_value[elem_idx])

        adjustment_floats: list[float] = []
        for attr_key, min_attr_value in animated_attr_min_values.items():
            adjustment_floats.extend(cast(Sequence[float], min_attr_value))

            max_attr_value = animated_attr_max_values[attr_key]
            for elem_idx in range(num_elements_per_attr):
                elem_scale = max_attr_value[elem_idx] - min_attr_value[elem_idx]
                if elem_scale > 0.0001:
                    adjustment_floats.append(elem_scale)
                else:
                    adjustment_floats.append(1.0)

        num_animated_attrs = len(animated_attr_min_values)
        elem_sizes_size_in_ints = (num_animated_attrs * 4 + 31) // 32
        adjustment_bytes_size_in_ints = (num_animated_attrs * num_elements_per_attr * 8 * 2 + 31) // 32
        animated_vectors_size_in_ints = (16 * num_animated_attrs * num_elements_per_attr * element_size_in_bits + 31) // 32
        frame_batch_size_in_ints = elem_sizes_size_in_ints + adjustment_bytes_size_in_ints + animated_vectors_size_in_ints
        num_frame_batches = ((self.num_frames + 15) // 16)
        frame_batch_sizes = [frame_batch_size_in_ints] * num_frame_batches

        return _AnimationDataHeader(
            cast(dict[_ItemAttributeKey, Sequence[float]], fixed_attrs),
            list(animated_attr_min_values.keys()),
            adjustment_floats,
            frame_batch_sizes
        )
