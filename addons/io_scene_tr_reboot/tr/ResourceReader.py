from typing import overload
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.util.BinaryReader import BinaryReader

class ResourceReader(BinaryReader):
    resource: ResourceKey
    references: dict[int, ResourceReference]
    resource_body_pos: int

    pointer_size: int

    @overload
    def __init__(self, other: "ResourceReader", /) -> None: ...

    @overload
    def __init__(self, resource: ResourceKey, data: bytes, has_references: bool, game: CdcGame, /) -> None: ...

    def __init__(self, resource_or_reader: "ResourceKey | ResourceReader", data: bytes = b"", has_references: bool = False, game: CdcGame = CdcGame.TR2013, /) -> None:
        super().__init__(isinstance(resource_or_reader, ResourceReader) and resource_or_reader.data or data)

        if isinstance(resource_or_reader, ResourceReader):
            reader = resource_or_reader
            self.resource = reader.resource
            self.references = reader.references
            self.resource_body_pos = reader.resource_body_pos
            self.pointer_size = reader.pointer_size
            self.position = reader.position
            return

        self.resource = resource_or_reader
        self.references = {}
        self.resource_body_pos = 0
        self.pointer_size = 4 if game == CdcGame.TR2013 else 8
        if not has_references:
            return

        num_internal_refs = self.read_int32()
        num_wide_external_refs = self.read_int32()
        num_int_patches = self.read_int32()
        num_short_patches = self.read_int32()
        num_packed_external_refs = self.read_int32()

        self.resource_body_pos = self.position + num_internal_refs*8 + \
                                                 num_wide_external_refs*(8 if game == CdcGame.TR2013 else 16) + \
                                                 num_int_patches*4 + \
                                                 num_short_patches*8 + \
                                                 num_packed_external_refs*4

        for _ in range(num_internal_refs):
            pointer_pos = self.resource_body_pos + self.read_int32()
            target_offset = self.read_int32()
            self.references[pointer_pos] = ResourceReference(self.resource.type, self.resource.id, target_offset)

        for _ in range(num_wide_external_refs):
            if game == CdcGame.TR2013:
                value = self.read_uint64()
                pointer_pos = self.resource_body_pos + ((value >> 16) & 0x7FFFFF) * 4
                target_offset = (value >> 39)

                prev_pos = self.position
                self.position = pointer_pos
                value = self.read_uint32()
                try:
                    target_type = ResourceType(value >> 24)
                except:
                    target_type = ResourceType.DTP
                target_id = value & 0xFFFFFF
                self.position = prev_pos
            else:
                pointer_pos = self.resource_body_pos + self.read_int32()
                target_type = ResourceType(self.read_int32())
                target_id = self.read_int32()
                target_offset = self.read_int32()
            self.references[pointer_pos] = ResourceReference(target_type, target_id, target_offset)

        self.skip(num_int_patches * 4)
        self.skip(num_short_patches * 8)

        for _ in range(num_packed_external_refs):
            packed_pointer = self.read_uint32()
            target_type = ResourceType(packed_pointer >> 25)
            pointer_pos = self.resource_body_pos + (packed_pointer & 0x1FFFFFF) * 4

            prev_pos = self.position
            self.position = pointer_pos
            target_id = self.read_uint32() & 0x7FFFFFFF
            self.position = prev_pos

            self.references[pointer_pos] = ResourceReference(target_type, target_id, 0)

    def read_ref(self) -> ResourceReference | None:
        ref: ResourceReference | None = self.read_ref_at(0)
        self.skip(self.pointer_size)
        return ref

    def read_ref_at(self, offset: int) -> ResourceReference | None:
        return self.references.get(self.position + offset)

    def read_ref_list(self, count: int) -> list[ResourceReference | None]:
        refs: list[ResourceReference | None] = []
        for _ in range(count):
            refs.append(self.read_ref())

        return refs

    def seek(self, ref: ResourceReference) -> None:
        if ref.type != self.resource.type or ref.id != self.resource.id:
            raise Exception("Attempt to seek to an external resource")

        self.position = self.resource_body_pos + ref.offset
