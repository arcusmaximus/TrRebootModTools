from ctypes import sizeof
from enum import IntEnum
from typing import TYPE_CHECKING, Sequence, cast
from io_scene_tr_reboot.tr.Animation import BlendShapeAnimationFrame, BoneAnimationFrame
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.tr2013.Tr2013Animation import AnimationChannelValueEncoding, IAnimationHeader, LinearAnimationChannelInfo, Tr2013Animation, Tr2013BoneAnimationFrame
from io_scene_tr_reboot.util.CStruct import CByte, CInt, CStruct64, CUShort
from io_scene_tr_reboot.util.CStructTypeMappings import CVec3

class _AnimationHeader(CStruct64, IAnimationHeader if TYPE_CHECKING else object):
    min_bounds: CVec3
    max_bounds: CVec3
    translation: CVec3
    rotation: CVec3
    anim_id: CUShort
    num_frames: CUShort
    ms_per_frame: CUShort
    num_blend_shapes: CByte
    num_bones: CByte
    num_sections: CByte
    num_extra_channels: CByte
    use_zlib: CByte
    padding0: CByte
    has_root_motion: CInt
    has_subtracted_frame: CInt
    padding1: CInt

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

assert(sizeof(_AnimationHeader) == 0xA8)

class _ChannelSizeEncoding(IntEnum):
    EMBEDDED = 0
    BYTE = 1
    SHORT = 2

class _LinearSegmentDurationReader:
    inner: ResourceReader
    next_nibble_high: bool
    current_byte: int

    def __init__(self, inner: ResourceReader) -> None:
        self.inner = inner
        self.next_nibble_high = False
        self.current_byte = 0

    def read(self) -> int:
        value = self.__read_nibble()
        if value == 0xF:
            low = self.__read_nibble()
            high = self.__read_nibble()
            value = (high << 4) | low

        return value

    def __read_nibble(self) -> int:
        nibble_high = self.next_nibble_high
        self.next_nibble_high = not nibble_high
        if not nibble_high:
            self.current_byte = self.inner.read_byte()
            return self.current_byte & 0xF
        else:
            return self.current_byte >> 4

class RiseBoneAnimationFrame(Tr2013BoneAnimationFrame):
    pass

class RiseAnimation(Tr2013Animation):
    def create_bone_frame(self, global_bone_id: int) -> BoneAnimationFrame:
        return RiseBoneAnimationFrame(self.get_bone_position_offset(global_bone_id))

    def read_header(self, reader: ResourceReader) -> IAnimationHeader:
        return reader.read_struct(_AnimationHeader)

    def read_bone_track_flags(self, flag_reader: ResourceReader) -> int:
        flags = flag_reader.read_byte()
        if flags & 0x88:
            flags_high = flag_reader.read_byte()
            flags = ((flags_high << 8) | flags) & 0x7777

        return flags

    def read_blend_shape_tracks(self, header: IAnimationHeader, reader: ResourceReader) -> None:
        if header.global_blend_shape_ids_ref is None or \
           header.blend_shape_meta_ref is None or \
           header.blend_shape_values_ref is None:
            return

        meta_reader = ResourceReader(reader)
        meta_reader.seek(header.blend_shape_meta_ref)

        value_reader = ResourceReader(reader)
        value_reader.seek(header.blend_shape_values_ref)

        reader.seek(header.global_blend_shape_ids_ref)
        global_blend_shape_ids = reader.read_uint16_list(header.num_blend_shapes)
        for global_blend_shape_id in global_blend_shape_ids:
            meta_reader.skip(4)
            self.blend_shape_tracks[global_blend_shape_id] = self.read_blend_shape_track(meta_reader, value_reader)

    def read_blend_shape_track(self, meta_reader: ResourceReader, value_reader: ResourceReader) -> list[BlendShapeAnimationFrame]:
        values = self.read_channel(meta_reader, value_reader)
        frames = cast(list[BlendShapeAnimationFrame], [None] * self.num_frames)
        for i, value in enumerate(values):
            frames[i] = BlendShapeAnimationFrame()
            frames[i].value = value

        return frames

    def read_linear_channel_info(self, channel_header: int, meta_reader: ResourceReader) -> LinearAnimationChannelInfo:
        value_encoding = (channel_header >> 2) & 3
        size_encoding = (channel_header >> 4) & 3
        durations_size: int
        values_size: int
        match size_encoding:
            case _ChannelSizeEncoding.EMBEDDED:
                values_size    = (channel_header >> 6) & 0x1F
                durations_size = (channel_header >> 11)
            case _ChannelSizeEncoding.BYTE:
                durations_size = meta_reader.read_byte()
                values_size = meta_reader.read_byte()
            case _ChannelSizeEncoding.SHORT:
                durations_size = meta_reader.read_uint16()
                values_size = meta_reader.read_uint16()
            case _:
                raise Exception(f"Unrecognized channel size type {size_encoding}")

        return LinearAnimationChannelInfo(durations_size, values_size, AnimationChannelValueEncoding(value_encoding))

    def read_linear_channel_segment_durations(self, meta_reader: ResourceReader, channel_info: LinearAnimationChannelInfo) -> Sequence[int]:
        durations_pos = meta_reader.position
        duration_reader = _LinearSegmentDurationReader(meta_reader)

        durations: list[int] = []
        frame = 1
        while frame < self.num_frames:
            duration = duration_reader.read()
            if duration > 0:
                durations.append(duration)
                frame += duration

        meta_reader.position = durations_pos + ((channel_info.durations_size + 1) & ~1)
        return durations

    def create_header(self) -> IAnimationHeader:
        return _AnimationHeader()

    def write_bone_track_flags(self, writer: ResourceBuilder, flags: int) -> None:
        if flags > 0xFF:
            writer.write_byte((flags & 0xFF) | 0x88)
            writer.write_byte(flags >> 8)
        else:
            writer.write_byte(flags)

    def write_blend_shape_tracks(self, header: IAnimationHeader, writer: ResourceBuilder) -> None:
        header.num_blend_shapes = len(self.blend_shape_tracks)
        if header.num_blend_shapes == 0:
            return

        header.global_blend_shape_ids_ref = writer.make_internal_ref()
        for blend_shape_id in self.blend_shape_tracks.keys():
            writer.write_uint16(blend_shape_id)

        writer.align(4)

        resource_key = ResourceKey(ResourceType.ANIMATION, self.id)
        meta_writer  = ResourceBuilder(resource_key, CdcGame.ROTTR)
        value_writer = ResourceBuilder(resource_key, CdcGame.ROTTR)
        for track in self.blend_shape_tracks.values():
            meta_writer.write_int32(0)
            self.write_channel(track, 0, 0, AnimationChannelValueEncoding.FIXED_4096, meta_writer, value_writer)

        header.blend_shape_meta_ref = writer.make_internal_ref()
        writer.write_builder(meta_writer)
        writer.align(4)

        header.blend_shape_values_ref = writer.make_internal_ref()
        writer.write_builder(value_writer)
