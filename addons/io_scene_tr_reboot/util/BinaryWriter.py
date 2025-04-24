import ctypes
from array import array
from io import SEEK_END, SEEK_SET, BufferedIOBase
from struct import pack
from typing import Sequence
from mathutils import Matrix, Vector
from io_scene_tr_reboot.util.CStruct import CStruct
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class BinaryWriter(SlotsBase):
    stream: BufferedIOBase

    def __init__(self, stream: BufferedIOBase) -> None:
        self.stream = stream

    @property
    def position(self) -> int:
        return self.stream.tell()

    @position.setter
    def position(self, value: int) -> None:
        self.stream.seek(value, SEEK_SET)

    @property
    def size(self) -> int:
        prev_pos = self.position
        self.stream.seek(0, SEEK_END)
        size = self.position
        self.stream.seek(prev_pos, SEEK_SET)
        return size

    def align(self, size: int) -> None:
        while (self.stream.tell() % size) != 0:
            self.write_byte(0)

    def write_bytes(self, value: bytes | bytearray | memoryview) -> None:
        self.stream.write(value)

    def write_byte(self, value: int) -> None:
        self.write_bytes(pack("<b", value))

    def write_int16(self, value: int) -> None:
        self.write_bytes(pack("<h", value))

    def write_int16_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "h")

    def write_uint16(self, value: int) -> None:
        self.write_bytes(pack("<H", value))

    def write_uint16_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "H")

    def write_int32(self, value: int) -> None:
        self.write_bytes(pack("<i", value))

    def write_int32_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "i")

    def write_uint32(self, value: int) -> None:
        self.write_bytes(pack("<I", value))

    def write_uint32_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "I")

    def write_int64(self, value: int) -> None:
        self.write_bytes(pack("<q", value))

    def write_int64_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "q")

    def write_uint64(self, value: int) -> None:
        self.write_bytes(pack("<Q", value))

    def write_uint64_list(self, values: Sequence[int]) -> None:
        self.__write_number_list(values, "Q")

    def write_float(self, value: float) -> None:
        self.write_bytes(pack("<f", value))

    def write_float_list(self, values: Sequence[float]) -> None:
        self.__write_number_list(values, "f")

    def __write_number_list(self, values: Sequence[int | float], type: str) -> None:
        if isinstance(values, array) and values.typecode == type:
            self.write_bytes(memoryview(values))
        else:
            self.write_bytes(pack(f"<{len(values)}{type}", *values))

    def write_vec2d(self, value: Vector) -> None:
        self.write_float(value.x)
        self.write_float(value.y)

    def write_vec2d_list(self, values: Sequence[Vector]) -> None:
        for value in values:
            self.write_vec2d(value)

    def write_vec3d(self, value: Vector) -> None:
        self.write_float(value.x)
        self.write_float(value.y)
        self.write_float(value.z)

    def write_vec3d_list(self, values: Sequence[Vector]) -> None:
        for value in values:
            self.write_vec3d(value)

    def write_vec4d(self, value: Vector) -> None:
        self.write_float(value.x)
        self.write_float(value.y)
        self.write_float(value.z)
        self.write_float(value.w)

    def write_vec4d_list(self, values: Sequence[Vector]) -> None:
        for value in values:
            self.write_vec4d(value)

    def write_mat4x4(self, value: Matrix) -> None:
        for col in range(4):
            for row in range(4):
                self.write_float(value[row][col])

    def write_mat4x4_list(self, values: Sequence[Matrix]) -> None:
        for value in values:
            self.write_mat4x4(value)

    def write_string(self, value: str) -> None:
        self.write_bytes(value.encode())
        self.write_byte(0)

    def write_struct(self, value: CStruct) -> None:
        value.map_fields_to_c(self)
        self.stream.write(value)

    def write_struct_list(self, values: Sequence[CStruct]) -> None:
        if isinstance(values, ctypes.Array):
            self.stream.write(values)
        else:
            for value in values:
                self.write_struct(value)
