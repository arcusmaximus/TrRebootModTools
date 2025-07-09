import bpy
from typing import cast
from mathutils import Matrix, Vector, Quaternion

class DriverFunctions:
    @staticmethod
    def register():
        for name, func in DriverFunctions.__dict__.items():
            if name.startswith("tr_"):
                bpy.app.driver_namespace[name] = func

    @staticmethod
    def unregister():
        for name in DriverFunctions.__dict__.keys():
            if name.startswith("tr_") and name in bpy.app.driver_namespace:
                del bpy.app.driver_namespace[name]

    @staticmethod
    def tr_dir(from_pos: tuple[float, ...], to_pos: tuple[float, ...]) -> tuple[float, ...]:
        return (Vector(to_pos) - Vector(from_pos)).normalized().to_tuple()

    @staticmethod
    def tr_rotate(vector: tuple[float, ...], rotation: tuple[float, ...]) -> tuple[float, ...]:
        matrix = Quaternion(rotation).to_matrix()
        quat = Matrix((
            (matrix[0][2], matrix[0][0], matrix[0][1]),
            (matrix[1][2], matrix[1][0], matrix[1][1]),
            (matrix[2][2], matrix[2][0], matrix[2][1]),
        )).to_quaternion()
        return (quat @ Vector(vector)).normalized().to_tuple()

    @staticmethod
    def tr_look_at(
        armature_rotation: tuple[float, ...],
        looker_position: tuple[float, ...],
        look_at_positions: list[tuple[float, ...]],
        look_at_weights: list[float],
        pole_dir: tuple[float, ...],
        bone_local_tangent: tuple[float, ...],
        bone_local_normal: tuple[float, ...]
    ) -> tuple[float, ...]:
        look_at_pos = Vector((0, 0, 0))
        for i, pos in enumerate(look_at_positions):
            look_at_pos += Vector(pos) * look_at_weights[i]

        look_dir = (look_at_pos - Vector(looker_position)).normalized()
        look_rotation = DriverFunctions.get_rotation_from_axes(look_dir, pole_dir)

        bone_local_rotation = DriverFunctions.get_rotation_from_axes(
            (bone_local_tangent[1], bone_local_tangent[2], bone_local_tangent[0]),
            (bone_local_normal[1],  bone_local_normal[2],  bone_local_normal[0]),
        )
        bone_local_rotation.invert()

        result = Quaternion(armature_rotation).inverted() @ look_rotation @ bone_local_rotation
        return (result.w, result.x, result.y, result.z)

    @staticmethod
    def get_rotation_from_axes(
        y_axis: tuple[float, ...] | Vector,
        z_axis: tuple[float, ...] | Vector
    ) -> Quaternion:
        if isinstance(y_axis, tuple):
            y_axis = Vector(y_axis)

        if isinstance(z_axis, tuple):
            z_axis = Vector(z_axis)

        x_axis = cast(Vector, y_axis.cross(z_axis)).normalized()
        z_axis = cast(Vector, x_axis.cross(y_axis)).normalized()

        return Matrix((
            (x_axis.x, y_axis.x, z_axis.x),
            (x_axis.y, y_axis.y, z_axis.y),
            (x_axis.z, y_axis.z, z_axis.z)
        )).to_quaternion()

    @staticmethod
    def tr_weighted_pos(
        armature_position: tuple[float, ...],
        armature_rotation: tuple[float, ...],
        bone_positions: list[tuple[float, ...]],
        weights: list[float],
        offset: tuple[float, ...]
    ) -> tuple[float, ...]:
        result = Vector(offset)
        for i, pos in enumerate(bone_positions):
            result += Vector(pos) * weights[i]

        inv_transform = Matrix.LocRotScale(Vector(armature_position), Quaternion(armature_rotation), None)
        inv_transform.invert()
        return (inv_transform @ result).to_tuple()

    @staticmethod
    def tr_weighted_rot(
        armature_position: tuple[float, ...],
        armature_rotation: tuple[float, ...],
        bone_rotations: list[tuple[float, ...]],
        weights: list[float],
        offset: tuple[float, ...]
    ) -> tuple[float, ...]:
        result: Quaternion
        if len(bone_rotations) == 2:
            result = Quaternion(bone_rotations[0]).slerp(Quaternion(bone_rotations[1]), weights[1])
        else:
            result = Quaternion()
            no_rot = Quaternion()
            for i, bone_rotation in enumerate(bone_rotations):
                result = result @ no_rot.slerp(Quaternion(bone_rotation), weights[i])

        result = Quaternion(armature_rotation).inverted() @ result @ DriverFunctions.convert_tr_rotation_to_blender(Quaternion(offset))
        return (result.w, result.x, result.y, result.z)

    @staticmethod
    def convert_tr_rotation_to_blender(rotation: Quaternion) -> Quaternion:
        return Quaternion((rotation.w, -rotation.y, rotation.x, rotation.z))
