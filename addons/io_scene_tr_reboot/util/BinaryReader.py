import ctypes
from struct import unpack_from
from typing import Sequence, TypeVar, cast
from mathutils import Matrix, Vector
from io_scene_tr_reboot.util.CStruct import CStruct
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

TStruct = TypeVar("TStruct", bound = CStruct)

class BinaryReader(SlotsBase):
    data: bytes
    position: int

    def __init__(self, data: bytes) -> None:
        self.data = data
        self.position = 0

    def skip(self, length: int) -> None:
        self.position += length

    def align(self, size: int) -> None:
        self.position = (self.position + size - 1) & ~(size - 1)

    def read_bytes(self, length: int) -> memoryview:
        result = self.read_bytes_at(0, length)
        self.position += length
        return result

    def read_bytes_at(self, offset: int, length: int) -> memoryview:
        return memoryview(self.data)[self.position + offset:self.position + offset + length]

    def read_byte(self) -> int:
        result = self.read_byte_at(0)
        self.position += 1
        return result

    def read_byte_at(self, offset: int) -> int:
        return self.data[self.position + offset]

    def read_int16(self) -> int:
        result = self.read_int16_at(0)
        self.position += 2
        return result

    def read_int16_at(self, offset: int) -> int:
        return unpack_from("<h", self.data, self.position + offset)[0]

    def read_int16_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 2).cast("h")

    def read_uint16(self) -> int:
        result = self.read_uint16_at(0)
        self.position += 2
        return result

    def read_uint16_at(self, offset: int) -> int:
        return unpack_from("<H", self.data, self.position + offset)[0]

    def read_uint16_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 2).cast("H")

    def read_int32(self) -> int:
        result = self.read_int32_at(0)
        self.position += 4
        return result

    def read_int32_at(self, offset: int) -> int:
        return unpack_from("<i", self.data, self.position + offset)[0]

    def read_int32_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 4).cast("i")

    def read_uint32(self) -> int:
        result = self.read_uint32_at(0)
        self.position += 4
        return result

    def read_uint32_at(self, offset: int) -> int:
        return unpack_from("<I", self.data, self.position + offset)[0]

    def read_uint32_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 4).cast("I")

    def read_int64(self) -> int:
        result = self.read_int64_at(0)
        self.position += 8
        return result

    def read_int64_at(self, offset: int) -> int:
        return unpack_from("<q", self.data, self.position + offset)[0]

    def read_int64_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 8).cast("q")

    def read_uint64(self) -> int:
        result = self.read_uint64_at(0)
        self.position += 8
        return result

    def read_uint64_at(self, offset: int) -> int:
        return unpack_from("<Q", self.data, self.position + offset)[0]

    def read_uint64_list(self, count: int) -> Sequence[int]:
        return self.read_bytes(count * 8).cast("Q")

    def read_float(self) -> float:
        result = self.read_float_at(0)
        self.position += 4
        return result

    def read_float_at(self, offset: int) -> float:
        return unpack_from("<f", self.data, self.position + offset)[0]

    def read_float_list(self, count: int) -> Sequence[float]:
        return self.read_bytes(count * 4).cast("f")

    def read_vec2d(self) -> Vector:
        result = self.read_vec2d_at(0)
        self.position += 8
        return result

    def read_vec2d_at(self, offset: int) -> Vector:
        return Vector(unpack_from("<2f", self.data, self.position + offset))

    def read_vec2d_list(self, count: int) -> list[Vector]:
        result = cast(list[Vector], [None] * count)
        for i in range(count):
            result[i] = self.read_vec2d()

        return result

    def read_vec3d(self) -> Vector:
        result = self.read_vec3d_at(0)
        self.position += 0xC
        return result

    def read_vec3d_at(self, offset: int) -> Vector:
        return Vector(unpack_from("<3f", self.data, self.position + offset))

    def read_vec3d_list(self, count: int) -> list[Vector]:
        result = cast(list[Vector], [None] * count)
        for i in range(count):
            result[i] = self.read_vec3d()

        return result

    def read_vec4d(self) -> Vector:
        result = self.read_vec4d_at(0).copy()
        self.position += 0x10
        return result

    def read_vec4d_at(self, offset: int) -> Vector:
        return Vector(unpack_from("<4f", self.data, self.position + offset))

    def read_vec4d_list(self, count: int) -> list[Vector]:
        result = cast(list[Vector], [None] * count)
        for i in range(count):
            result[i] = self.read_vec4d()

        return result

    def read_mat4x4(self) -> Matrix:
        result = self.read_mat4x4_at(0)
        self.position += 0x40
        return result

    def read_mat4x4_at(self, offset: int) -> Matrix:
        col1 = unpack_from("<4f", self.data, self.position + offset)
        col2 = unpack_from("<4f", self.data, self.position + offset + 0x10)
        col3 = unpack_from("<4f", self.data, self.position + offset + 0x20)
        col4 = unpack_from("<4f", self.data, self.position + offset + 0x30)
        matrix = Matrix((col1, col2, col3, col4))
        matrix.transpose()
        return matrix

    def read_mat4x4_list(self, count: int) -> list[Matrix]:
        result = cast(list[Matrix], [None] * count)
        for i in range(count):
            result[i] = self.read_mat4x4()

        return result

    def read_string_at(self, offset: int) -> str:
        start_pos = self.position + offset
        end_pos = start_pos
        while self.data[end_pos] != 0:
            end_pos += 1

        return self.data[start_pos:end_pos].decode()

    def read_struct(self, t: type[TStruct]) -> TStruct:
        result = t.from_buffer_copy(self.data, self.position)
        result.map_fields_from_c(self)
        self.position += ctypes.sizeof(t)
        return result

    def read_struct_list(self, t: type[TStruct], count: int) -> list[TStruct]:
        result: list[TStruct] = []
        for _ in range(count):
            result.append(self.read_struct(t))

        return result
