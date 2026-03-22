from array import array
from ctypes import sizeof
from enum import IntEnum
from mathutils import Vector
from typing import TYPE_CHECKING, NamedTuple, Protocol, Sequence, cast
from io_scene_tr_reboot.tr.Animation import Animation, BoneAnimationFrame, IAnimationFrame
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.CStruct import CByte, CInt, CShort, CStruct, CStruct32, CUShort
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class IAnimationHeader(Protocol):
    min_bounds: CVec3
    max_bounds: CVec3
    translation: CVec3
    rotation: CVec3
    anim_id: int
    num_frames: int
    ms_per_frame: int
    num_bones: int
    num_blend_shapes: int

    extra_channel_meta_ref: ResourceReference | None
    extra_channel_values_ref: ResourceReference | None

    precise_bone_data_ref: ResourceReference | None
    camera_cut_frames_ref: ResourceReference | None

    global_bone_ids_ref: ResourceReference | None
    bone_track_flags_ref: ResourceReference | None
    bone_channel_meta_ref: ResourceReference | None
    bone_channel_values_ref: ResourceReference | None

    global_blend_shape_ids_ref: ResourceReference | None
    blend_shape_meta_ref: ResourceReference | None
    blend_shape_values_ref: ResourceReference | None

    end_of_data_ref: ResourceReference | None

class _AnimationHeader(CStruct32, IAnimationHeader if TYPE_CHECKING else object):
    min_bounds: CVec3
    max_bounds: CVec3
    translation: CVec3
    rotation: CVec3
    anim_id: CUShort
    num_frames: CUShort
    ms_per_frame: CUShort
    num_bones: CByte
    num_sections: CByte
    num_extra_channels: CByte
    use_zlib: CByte
    padding0: CShort
    has_root_motion: CInt
    has_subtracted_frame: CInt

    extra_channel_meta_ref: ResourceReference | None
    extra_channel_values_ref: ResourceReference | None

    precise_bone_data_ref: ResourceReference | None
    camera_cut_frames_ref: ResourceReference | None

    global_bone_ids_ref: ResourceReference | None
    bone_track_flags_ref: ResourceReference | None
    bone_channel_meta_ref: ResourceReference | None
    bone_channel_values_ref: ResourceReference | None

    end_of_data_ref: ResourceReference | None

    num_blend_shapes: int
    global_blend_shape_ids_ref: ResourceReference | None
    blend_shape_meta_ref: ResourceReference | None
    blend_shape_values_ref: ResourceReference | None
    _ignored_fields_ = ("num_blend_shapes", "global_blend_shape_ids_ref", "blend_shape_meta_ref", "blend_shape_values_ref")

assert(sizeof(_AnimationHeader) == 0x68)

class _ChannelType(IntEnum):
    BAKED = 0
    FLOAT_CONSTANT = 1
    LINEAR = 2
    EMBEDDED_CONSTANT = 3

class AnimationChannelValueEncoding(IntEnum):
    FLOAT = 0
    FIXED_4096 = 1
    VARIABLE_64 = 2
    VARIABLE_4096 = 3

class LinearAnimationChannelInfo(NamedTuple):
    durations_size: int
    values_size: int
    value_encoding: AnimationChannelValueEncoding

class Tr2013BoneAnimationFrame(BoneAnimationFrame):
    rotation_angle_factor = 1.0
    position_factor = 1.0

class Tr2013Animation(Animation):
    def create_bone_frame(self, global_bone_id: int) -> BoneAnimationFrame:
        return Tr2013BoneAnimationFrame(self.get_bone_position_offset(global_bone_id))

    def get_bone_position_offset(self, global_bone_id: int) -> Vector:
        bone_info = self.bone_infos.get(global_bone_id)
        if bone_info is None or bone_info.parent_global_id is None:
            return Vector()
        else:
            # Unlike Blender (and SOTTR), whose animations store bone positions relative to the bone's own rest position,
            # TR2013 (and ROTTR) store values relative to the *parent* bone's position,
            # so we need to subtract the rest position difference to compensate
            return self.bone_infos[bone_info.parent_global_id].rest_matrix.translation - bone_info.rest_matrix.translation

    def read(self, reader: ResourceReader) -> None:
        header = self.read_header(reader)
        self.ms_per_frame = header.ms_per_frame
        self.num_frames = header.num_frames

        self.read_bone_tracks(header, reader)
        self.read_blend_shape_tracks(header, reader)

    def read_header(self, reader: ResourceReader) -> IAnimationHeader:
        header = reader.read_struct(_AnimationHeader)
        header.num_blend_shapes = 0
        header.blend_shape_meta_ref = None
        header.blend_shape_values_ref = None
        return header

    def read_bone_tracks(self, header: IAnimationHeader, reader: ResourceReader) -> None:
        if header.global_bone_ids_ref is None or \
           header.bone_track_flags_ref is None or \
           header.bone_channel_meta_ref is None or \
           header.bone_channel_values_ref is None:
            return

        flag_reader = ResourceReader(reader)
        flag_reader.seek(header.bone_track_flags_ref)
        flag_reader.skip(2)

        meta_reader = ResourceReader(reader)
        meta_reader.seek(header.bone_channel_meta_ref)

        value_reader = ResourceReader(reader)
        value_reader.seek(header.bone_channel_values_ref)
        value_reader.skip(4)

        reader.seek(header.global_bone_ids_ref)
        global_bone_ids = reader.read_uint16_list(header.num_bones)
        for global_bone_id in global_bone_ids:
            self.bone_tracks[global_bone_id] = self.read_bone_track(global_bone_id, flag_reader, meta_reader, value_reader)

    def read_bone_track(self, global_bone_id: int, flag_reader: ResourceReader, meta_reader: ResourceReader, value_reader: ResourceReader) -> list[BoneAnimationFrame]:
        flags = self.read_bone_track_flags(flag_reader)
        track = cast(list[BoneAnimationFrame], [None] * self.num_frames)
        for i in range(self.num_frames):
            track[i] = self.create_bone_frame(global_bone_id)

        for attr_idx in range(3):
            for elem_idx in range(4):
                channel_exists = (flags & 1) != 0
                flags >>= 1
                if not channel_exists:
                    continue

                channel_values = self.read_channel(meta_reader, value_reader)
                for frame, channel_value in enumerate(channel_values):
                    track[frame].set_attr_elem_raw(attr_idx, elem_idx, channel_value)

        return track

    def read_bone_track_flags(self, flag_reader: ResourceReader) -> int:
        return flag_reader.read_uint16()

    def read_blend_shape_tracks(self, header: IAnimationHeader, reader: ResourceReader) -> None:
        pass

    def read_channel(self, meta_reader: ResourceReader, value_reader: ResourceReader) -> Sequence[float]:
        if self.num_frames == 1:
            return [value_reader.read_float()]

        channel_header = meta_reader.read_uint16()
        channel_type = channel_header & 3
        match channel_type:
            case _ChannelType.FLOAT_CONSTANT:
                return self.read_float_constant_channel(value_reader)

            case _ChannelType.EMBEDDED_CONSTANT:
                return self.read_embedded_constant_channel(channel_header)

            case _ChannelType.BAKED:
                return self.read_baked_channel(meta_reader, value_reader)

            case _ChannelType.LINEAR:
                return self.read_linear_channel(channel_header, meta_reader, value_reader)

            case _:
                raise Exception(f"Unrecognized channel type {channel_type}")

    def read_float_constant_channel(self, value_reader: ResourceReader) -> Sequence[float]:
        value = value_reader.read_float()
        values = array("f", [0]) * self.num_frames
        for i in range(self.num_frames):
            values[i] = value

        return values

    def read_embedded_constant_channel(self, channel_header: int) -> Sequence[float]:
        value = (channel_header >> 2) / 4096.0
        values = array("f", [0]) * self.num_frames
        for i in range(self.num_frames):
            values[i] = value

        return values

    def read_baked_channel(self, meta_reader: ResourceReader, value_reader: ResourceReader) -> Sequence[float]:
        value_type = meta_reader.read_uint16()
        values: Sequence[float]
        match value_type:
            case AnimationChannelValueEncoding.FLOAT:
                values = value_reader.read_float_list(self.num_frames)

            case AnimationChannelValueEncoding.FIXED_4096:
                packed_values = value_reader.read_int16_list(self.num_frames)
                values = array("f", [0]) * self.num_frames
                for i, packed_value in enumerate(packed_values):
                    values[i] = packed_value / 4096.0

                if self.num_frames & 1:
                    value_reader.skip(2)

            case _:
                raise Exception(f"Unknown value type {value_type}")

        return values

    def read_linear_channel(self, channel_header: int, meta_reader: ResourceReader, value_reader: ResourceReader) -> Sequence[float]:
        channel_info         = self.read_linear_channel_info(channel_header, meta_reader)
        segment_durations    = self.read_linear_channel_segment_durations(meta_reader, channel_info)
        segment_start_values = self.read_linear_channel_segment_values(value_reader, channel_info, len(segment_durations))

        frame_values = array("f", [0]) * self.num_frames
        segment_idx = 0
        segment_start_frame = 0
        frame_values[0] = segment_start_values[0]

        for frame in range(1, self.num_frames):
            if frame == segment_start_frame + segment_durations[segment_idx]:
                frame_values[frame] = frame_values[segment_start_frame] + segment_start_values[segment_idx + 1]
                segment_idx += 1
                segment_start_frame = frame
            else:
                frame_values[frame] = frame_values[segment_start_frame] + segment_start_values[segment_idx + 1] * (frame - segment_start_frame) / segment_durations[segment_idx]

        return frame_values

    def read_linear_channel_info(self, channel_header: int, meta_reader: ResourceReader) -> LinearAnimationChannelInfo:
        value_encoding = meta_reader.read_uint16()
        num_values = meta_reader.read_uint16()

        durations_size = num_values - 1
        values_size: int
        match value_encoding:
            case AnimationChannelValueEncoding.FLOAT:
                values_size = num_values * 4

            case AnimationChannelValueEncoding.FIXED_4096:
                values_size = num_values * 2

            case _:
                raise Exception(f"Unrecognized value encoding {value_encoding}")

        return LinearAnimationChannelInfo(durations_size, values_size, value_encoding)

    def read_linear_channel_segment_durations(self, meta_reader: ResourceReader, channel_info: LinearAnimationChannelInfo) -> Sequence[int]:
        durations_pos = meta_reader.position
        durations = meta_reader.read_bytes(channel_info.durations_size)
        meta_reader.position = durations_pos + ((channel_info.durations_size + 1) & ~1)
        return durations

    def read_linear_channel_segment_values(self, value_reader: ResourceReader, channel_info: LinearAnimationChannelInfo, num_segments: int) -> Sequence[float]:
        values_pos = value_reader.position
        values: Sequence[float]
        match channel_info.value_encoding:
            case AnimationChannelValueEncoding.FLOAT:
                values = value_reader.read_float_list(1 + num_segments)

            case AnimationChannelValueEncoding.FIXED_4096:
                packed_values = value_reader.read_int16_list(1 + num_segments)
                values = array("f", [0]) * (1 + num_segments)
                for i, packed_value in enumerate(packed_values):
                    values[i] = packed_value / 4096.0

            case AnimationChannelValueEncoding.VARIABLE_64:
                values = array("f", [0]) * (1 + num_segments)
                for i in range(1 + num_segments):
                    value = value_reader.read_byte()
                    if (value & 0x80) == 0:
                        if value & 0x40:
                            value = -((~value & 0x3F) + 1)

                        value *= 4
                    else:
                        value = ((value & 0x7F) << 8) | value_reader.read_byte()
                        if value & 0x4000:
                            value = -((~value & 0x3FFF) + 1)

                    values[i] = value / 64.0

            case AnimationChannelValueEncoding.VARIABLE_4096:
                values = array("f", [0]) * (1 + num_segments)
                for i in range(1 + num_segments):
                    value = value_reader.read_byte()
                    if value == 0x80:
                        high = value_reader.read_byte()
                        low  = value_reader.read_byte()
                        value = (high << 8) | low
                        if value & 0x8000:
                            value = -((~value & 0x7FFF) + 1)
                    else:
                        if value & 0x80:
                            value = -((~value & 0x7F) + 1)

                        value *= 0x20

                    values[i] = value / 4096.0

        value_reader.position = values_pos + ((channel_info.values_size + 3) & ~3)
        return values

    def write(self, writer: ResourceBuilder) -> None:
        header = self.create_header()
        header.min_bounds = CVec3()
        header.max_bounds = CVec3()
        header.translation = CVec3()
        header.rotation = CVec3()
        header.anim_id = self.id
        header.num_frames = self.num_frames
        header.ms_per_frame = self.ms_per_frame

        header.extra_channel_meta_ref = None
        header.extra_channel_values_ref = None
        header.precise_bone_data_ref = None
        header.camera_cut_frames_ref = None

        header.global_bone_ids_ref = None
        header.bone_track_flags_ref = None
        header.bone_channel_meta_ref = None
        header.bone_channel_values_ref = None

        header.global_blend_shape_ids_ref = None
        header.blend_shape_meta_ref = None
        header.blend_shape_values_ref = None

        header.end_of_data_ref = None

        writer.write_struct(cast(CStruct, header))

        self.write_bone_tracks(header, writer)
        self.write_blend_shape_tracks(header, writer)

        header.end_of_data_ref = writer.make_internal_ref()
        writer.write_int32(0)

        writer.position = 0
        writer.write_struct(cast(CStruct, header))

    def create_header(self) -> IAnimationHeader:
        return _AnimationHeader()

    def write_bone_tracks(self, header: IAnimationHeader, writer: ResourceBuilder) -> None:
        header.num_bones = len(self.bone_tracks)
        if header.num_bones == 0:
            return

        header.global_bone_ids_ref = writer.make_internal_ref()
        for global_bone_id in self.bone_tracks.keys():
            writer.write_uint16(global_bone_id)

        writer.align(4)

        resource_key = ResourceKey(ResourceType.ANIMATION, self.id)
        flags_writer = ResourceBuilder(resource_key, CdcGame.ROTTR)
        meta_writer  = ResourceBuilder(resource_key, CdcGame.ROTTR)
        value_writer = ResourceBuilder(resource_key, CdcGame.ROTTR)
        for track in self.bone_tracks.values():
            self.write_bone_track(track, flags_writer, meta_writer, value_writer)

        header.bone_track_flags_ref = writer.make_internal_ref()
        writer.write_int16(0)
        writer.write_builder(flags_writer)
        writer.align(4)

        header.bone_channel_meta_ref = writer.make_internal_ref()
        writer.write_builder(meta_writer)
        writer.align(4)

        header.bone_channel_values_ref = writer.make_internal_ref()
        writer.write_int32(0)
        writer.write_builder(value_writer)

    def write_bone_track(self, track: list[BoneAnimationFrame], flags_writer: ResourceBuilder, meta_writer: ResourceBuilder, value_writer: ResourceBuilder) -> None:
        flags = 0
        if track[0].rotation is not None:
            flags |= 0x007
        if track[0].position is not None:
            flags |= 0x070
        if track[0].scale is not None:
            flags |= 0x700

        self.write_bone_track_flags(flags_writer, flags)

        for attr_idx in range(3):
            if track[0].get_attr_raw(attr_idx) is None:
                continue

            encoding = AnimationChannelValueEncoding.FIXED_4096 if attr_idx == 0 else AnimationChannelValueEncoding.FLOAT
            for elem_idx in range(3):
                self.write_channel(track, attr_idx, elem_idx, encoding, meta_writer, value_writer)

    def write_bone_track_flags(self, writer: ResourceBuilder, flags: int) -> None:
        writer.write_uint16(flags)

    def write_blend_shape_tracks(self, header: IAnimationHeader, writer: ResourceBuilder) -> None:
        pass

    def write_channel(self, track: Sequence[IAnimationFrame], attr_idx: int, elem_idx: int, encoding: AnimationChannelValueEncoding, meta_writer: ResourceBuilder, value_writer: ResourceBuilder) -> None:
        if self.num_frames == 1:
            attr_value = track[0].get_attr_raw(attr_idx)
            if attr_value is None:
                raise Exception()

            value_writer.write_float(attr_value[elem_idx])
            return

        meta_writer.write_uint16(_ChannelType.BAKED)
        meta_writer.write_uint16(encoding)
        match encoding:
            case AnimationChannelValueEncoding.FLOAT:
                for frame in track:
                    attr_value = frame.get_attr_raw(attr_idx)
                    if attr_value is None:
                        raise Exception()

                    value_writer.write_float(attr_value[elem_idx])

            case AnimationChannelValueEncoding.FIXED_4096:
                for frame in track:
                    attr_value = frame.get_attr_raw(attr_idx)
                    if attr_value is None:
                        raise Exception()

                    value_writer.write_int16(int(attr_value[elem_idx] * 4096.0))

                value_writer.align(4)

            case _:
                raise Exception()
