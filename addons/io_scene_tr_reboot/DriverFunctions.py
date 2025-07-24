import bpy
from typing import ClassVar, cast
from mathutils import Matrix, Vector, Quaternion

class DriverFunctions:
    blender_flip_quat: ClassVar[Quaternion] = Quaternion((0, 0.707107, 0, -0.707107))
    tr_flip_quat: ClassVar[Quaternion] = Quaternion((0, 0.707107, -0.707107, 0))

    @staticmethod
    def register() -> None:
        DriverFunctions.__register()
        bpy.app.handlers.load_post.append(DriverFunctions.on_scene_load)

    @staticmethod
    @bpy.app.handlers.persistent
    def on_scene_load(bl_scene: bpy.types.Scene) -> None:
        DriverFunctions.__register()

    @staticmethod
    def __register() -> None:
        for name, func in DriverFunctions.__dict__.items():
            if name.startswith("tr_"):
                bpy.app.driver_namespace[name] = func

    @staticmethod
    def unregister() -> None:
        bpy.app.handlers.load_post.remove(DriverFunctions.on_scene_load)
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
        bl_looker_pose_bone: bpy.types.PoseBone,
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
        look_rotation = look_rotation @ bone_local_rotation

        return DriverFunctions.world_rotation_to_bone_rotation(bl_looker_pose_bone, look_rotation)

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
        bl_target_pose_bone: bpy.types.PoseBone,
        source_bone_positions: list[tuple[float, ...]],
        source_bone_flip_flags: list[int],
        source_bone_weights: list[float],
        offset: tuple[float, ...]
    ) -> tuple[float, ...]:
        result = Vector(offset)
        for i, pos in enumerate(source_bone_positions):
            result += Vector(pos) * source_bone_weights[i]

        return DriverFunctions.world_position_to_bone_position(bl_target_pose_bone, result)

    @staticmethod
    def tr_weighted_rot(
        bl_target_pose_bone: bpy.types.PoseBone,
        source_bone_rotation_tuples: list[tuple[float, ...]],
        source_bone_flip_flags: list[int],
        source_bone_weights: list[float],
        offset: tuple[float, ...]
    ) -> tuple[float, ...]:
        bone_rotation_quats: list[Quaternion] = []
        for i in range(len(source_bone_rotation_tuples)):
            bone_rotation = Quaternion(source_bone_rotation_tuples[i])
            if source_bone_flip_flags[i] != 0:
                bone_rotation = bone_rotation @ DriverFunctions.blender_flip_quat

            bone_rotation_quats.append(bone_rotation)

        result: Quaternion
        if len(source_bone_rotation_tuples) == 2:
            result = bone_rotation_quats[0].slerp(bone_rotation_quats[1], source_bone_weights[1])
        else:
            result = Quaternion()
            no_rot = Quaternion()
            for i, bone_rotation in enumerate(bone_rotation_quats):
                result = result @ no_rot.slerp(bone_rotation, source_bone_weights[i])

        result = result @ DriverFunctions.convert_tr_rotation_to_blender(Quaternion(offset))
        return DriverFunctions.world_rotation_to_bone_rotation(bl_target_pose_bone, result)

    @staticmethod
    def convert_tr_rotation_to_blender(rotation: Quaternion) -> Quaternion:
        return Quaternion((rotation.w, rotation.x, -rotation.z, rotation.y))

    @staticmethod
    def world_position_to_bone_position(bl_pose_bone: bpy.types.PoseBone, world_space_position: Vector) -> tuple[float, ...]:
        bl_armature_obj = cast(bpy.types.Object, bl_pose_bone.id_data)
        world_space_matrix = Matrix.Translation(world_space_position)
        bone_space_matrix  = bl_armature_obj.convert_space(pose_bone = bl_pose_bone, matrix = world_space_matrix, from_space = "WORLD", to_space = "LOCAL")
        return bone_space_matrix.translation.to_tuple()

    @staticmethod
    def world_rotation_to_bone_rotation(bl_pose_bone: bpy.types.PoseBone, world_space_rotation: Quaternion) -> tuple[float, ...]:
        bl_armature_obj = cast(bpy.types.Object, bl_pose_bone.id_data)
        world_space_matrix = world_space_rotation.to_matrix().to_4x4()
        bone_space_matrix  = bl_armature_obj.convert_space(pose_bone = bl_pose_bone, matrix = world_space_matrix, from_space = "WORLD", to_space = "LOCAL")
        bone_space_rotation = bone_space_matrix.to_quaternion()
        return (bone_space_rotation.w, bone_space_rotation.x, bone_space_rotation.y, bone_space_rotation.z)
