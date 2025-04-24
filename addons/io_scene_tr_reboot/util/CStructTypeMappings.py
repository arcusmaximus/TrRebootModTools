from mathutils import Matrix, Quaternion, Vector
from io_scene_tr_reboot.util.CStruct import CFloat, CStruct, CStructTypeMapping

class CVec2(CStruct):
    x: CFloat
    y: CFloat

    @staticmethod
    def from_coords(x: float, y: float) -> "CVec2":
        c_value = CVec2()
        c_value.x = x
        c_value.y = y
        return c_value

    @staticmethod
    def from_vector(mapped_value: Vector) -> "CVec2":
        c_value = CVec2()
        c_value.x = mapped_value.x
        c_value.y = mapped_value.y
        return c_value

    def to_vector(self) -> Vector:
        return Vector((self.x, self.y))

class CVec3(CStruct):
    x: CFloat
    y: CFloat
    z: CFloat

    @staticmethod
    def from_coords(x: float, y: float, z: float) -> "CVec3":
        c_value = CVec3()
        c_value.x = x
        c_value.y = y
        c_value.z = z
        return c_value

    @staticmethod
    def from_vector(mapped_value: Vector) -> "CVec3":
        c_value = CVec3()
        c_value.x = mapped_value.x
        c_value.y = mapped_value.y
        c_value.z = mapped_value.z
        return c_value

    def to_vector(self) -> Vector:
        return Vector((self.x, self.y, self.z))

class CVec4(CStruct):
    x: CFloat
    y: CFloat
    z: CFloat
    w: CFloat

    @staticmethod
    def from_coords(x: float, y: float, z: float, w: float) -> "CVec4":
        c_value = CVec4()
        c_value.x = x
        c_value.y = y
        c_value.z = z
        c_value.w = w
        return c_value

    @staticmethod
    def from_vector(mapped_value: Vector) -> "CVec4":
        vec = CVec4()
        vec.x = mapped_value.x
        vec.y = mapped_value.y
        vec.z = mapped_value.z
        vec.w = 0.0
        return vec

    def to_vector(self) -> Vector:
        return Vector((self.x, self.y, self.z))

class CQuat(CStruct):
    x: CFloat
    y: CFloat
    z: CFloat
    w: CFloat

    @staticmethod
    def from_coords(x: float, y: float, z: float, w: float) -> "CQuat":
        c_value = CQuat()
        c_value.x = x
        c_value.y = y
        c_value.z = z
        c_value.w = w
        return c_value

    @staticmethod
    def from_quaternion(mapped_value: Quaternion) -> "CQuat":
        quat = CQuat()
        quat.w = mapped_value.w
        quat.x = mapped_value.x
        quat.y = mapped_value.y
        quat.z = mapped_value.z
        return quat

    def to_quaternion(self) -> Quaternion:
        return Quaternion((self.w, self.x, self.y, self.z))

class CMatrix(CStruct):
    m00: CFloat
    m01: CFloat
    m02: CFloat
    m03: CFloat

    m10: CFloat
    m11: CFloat
    m12: CFloat
    m13: CFloat

    m20: CFloat
    m21: CFloat
    m22: CFloat
    m23: CFloat

    m30: CFloat
    m31: CFloat
    m32: CFloat
    m33: CFloat

    @staticmethod
    def from_matrix(mapped_value: Matrix) -> "CMatrix":
        matrix = CMatrix()
        matrix.m00 = mapped_value[0][0]
        matrix.m01 = mapped_value[1][0]
        matrix.m02 = mapped_value[2][0]
        matrix.m03 = mapped_value[3][0]

        matrix.m10 = mapped_value[0][1]
        matrix.m11 = mapped_value[1][1]
        matrix.m12 = mapped_value[2][1]
        matrix.m13 = mapped_value[3][1]

        matrix.m20 = mapped_value[0][2]
        matrix.m21 = mapped_value[1][2]
        matrix.m22 = mapped_value[2][2]
        matrix.m23 = mapped_value[3][2]

        matrix.m30 = mapped_value[0][3]
        matrix.m31 = mapped_value[1][3]
        matrix.m32 = mapped_value[2][3]
        matrix.m33 = mapped_value[3][3]
        return matrix

    def to_matrix(self) -> Matrix:
        return Matrix(((self.m00, self.m10, self.m20, self.m30),
                       (self.m01, self.m11, self.m21, self.m31),
                       (self.m02, self.m12, self.m22, self.m32),
                       (self.m03, self.m13, self.m23, self.m33)))

class _VectorTypeMapping(CStructTypeMapping[Vector, CVec4]):
    def map_from_c(self, c_value: CVec4, offset: int, context: object) -> Vector:
        return c_value.to_vector()

    def map_to_c(self, mapped_value: Vector, offset: int, context: object) -> CVec4:
        return CVec4.from_vector(mapped_value)

class _QuaterionTypeMapping(CStructTypeMapping[Quaternion, CQuat]):
    def map_from_c(self, c_value: CQuat, offset: int, context: object) -> Quaternion:
        return c_value.to_quaternion()

    def map_to_c(self, mapped_value: Quaternion, offset: int, context: object) -> CQuat:
        return CQuat.from_quaternion(mapped_value)

class _MatrixTypeMapping(CStructTypeMapping[Matrix, CMatrix]):
    def map_from_c(self, c_value: CMatrix, offset: int, context: object) -> Matrix:
        return c_value.to_matrix()

    def map_to_c(self, mapped_value: Matrix, offset: int, context: object) -> CMatrix:
        return CMatrix.from_matrix(mapped_value)

class CStructTypeMappings:
    @staticmethod
    def register():
        CStruct.register_type_mappings(
            _VectorTypeMapping(),
            _QuaterionTypeMapping(),
            _MatrixTypeMapping()
        )
