import bpy
import math
from typing import Any, ClassVar, Literal, NamedTuple, cast
from mathutils import Matrix, Quaternion, Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectSkeletonProperties
from io_scene_tr_reboot.tr.BoneConstraint import BoneConstraintType, IBoneConstraint, IBoneConstraint_WeightedPosition, IBoneConstraint_WeightedRotation, IBoneConstraint_LookAt
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class _BoneTransform(NamedTuple):
    global_id: int | None
    head: Vector
    tail: Vector
    orientation: Quaternion
    is_flipped: bool

class SkeletonImporter(SlotsBase):
    scale_factor: float
    bl_target_collection: bpy.types.Collection | None

    blender_flip_quat: ClassVar[Quaternion] = Quaternion((0, 0.707107, 0, -0.707107))
    tr_flip_quat:      ClassVar[Quaternion] = Quaternion((0, 0.707107, -0.707107, 0))

    def __init__(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None = None) -> None:
        self.scale_factor = scale_factor
        self.bl_target_collection = bl_target_collection

    def import_from_collection(self, tr_collection: Collection) -> dict[ResourceKey, bpy.types.Object]:
        bl_armature_objs: dict[ResourceKey, bpy.types.Object] = {}
        model_skeleton_resources = Enumerable(tr_collection.get_model_instances()).select(lambda i: i.skeleton_resource).of_type(ResourceKey)
        hair_skeleton_resources  = Enumerable(tr_collection.get_hair_resources()).select(lambda s: s.skeleton_resource).of_type(ResourceKey)
        for skeleton_resource in model_skeleton_resources.concat(hair_skeleton_resources).distinct():
            tr_skeleton = tr_collection.get_skeleton(skeleton_resource)
            if tr_skeleton is None or len(tr_skeleton.bones) == 0:
                continue

            armature_name = BlenderNaming.make_local_armature_name(tr_collection.name, tr_skeleton.id)

            bl_armature = bpy.data.armatures.get(armature_name)
            bl_armature_obj: bpy.types.Object
            if bl_armature is None:
                bl_armature = bpy.data.armatures.new(armature_name)
                bl_armature.display_type = "STICK"
                bl_armature_obj = BlenderHelper.create_object(bl_armature)

                bone_transforms = self.calc_bone_transforms(tr_skeleton)
                self.create_bones(bl_armature_obj, tr_skeleton, bone_transforms)
                self.assign_counterparts(bl_armature_obj, tr_skeleton)
                self.create_constraints(bl_armature_obj, tr_skeleton, bone_transforms)
            else:
                bl_armature_obj = BlenderHelper.create_object(bl_armature)

            bl_armature_obj.show_in_front = True
            ObjectSkeletonProperties.set_global_blend_shape_ids(bl_armature_obj, tr_skeleton.global_blend_shape_ids)
            BlenderHelper.move_object_to_collection(bl_armature_obj, self.bl_target_collection)

            bl_armature_objs[skeleton_resource] = bl_armature_obj

        return bl_armature_objs

    def calc_bone_transforms(self, tr_skeleton: ISkeleton) -> list[_BoneTransform]:
        child_indices: list[list[int]] = []
        heads: list[Vector] = []
        for i, tr_bone in enumerate(tr_skeleton.bones):
            child_indices.append([])
            if tr_bone.parent_id < 0:
                heads.append(tr_bone.relative_location * self.scale_factor)
            else:
                heads.append(heads[tr_bone.parent_id] + tr_bone.relative_location * self.scale_factor)
                child_indices[tr_bone.parent_id].append(i)

        transforms: list[_BoneTransform] = []
        existing_bone_flip_states: dict[int, bool] = self.get_existing_bone_flip_states()
        for i, tr_bone in enumerate(tr_skeleton.bones):
            head = heads[i]
            orientation = self.to_blender_orientation(tr_bone.absolute_orientation)

            tail_offset: Vector
            is_flipped: bool | None
            if tr_bone.global_id is None:
                tail_offset = Vector((0, 0, 0.01))
                is_flipped = False
            else:
                length: float
                if len(child_indices[i]) > 0:
                    avg_child_head = Enumerable(child_indices[i]).avg(lambda idx: heads[idx])
                    length = (avg_child_head - heads[i]).length
                else:
                    length = tr_bone.distance_from_parent * self.scale_factor

                tail_offset = (orientation @ Vector((0, 1, 0))) * max(length, 0.5 * self.scale_factor)

                is_flipped = existing_bone_flip_states.get(tr_bone.global_id)
                if is_flipped is None and tr_bone.parent_id >= 0:
                    parent_head = heads[tr_bone.parent_id]
                    if (head + tail_offset - parent_head).length < (head - tail_offset - parent_head).length:
                        tail_offset = -tail_offset
                        orientation = orientation @ SkeletonImporter.blender_flip_quat
                        is_flipped = True

            transforms.append(_BoneTransform(tr_bone.global_id, head, head + tail_offset, orientation, is_flipped or False))

        return transforms

    def get_existing_bone_flip_states(self) -> dict[int, bool]:
        bone_flip_states: dict[int, bool] = {}
        if bpy.context.scene is None:
            return bone_flip_states

        for bl_obj in bpy.context.scene.objects:
            bl_armature = bl_obj.data
            if not isinstance(bl_armature, bpy.types.Armature):
                continue

            for bl_bone in bl_armature.bones:
                bone_ids = BlenderNaming.try_parse_bone_name(bl_bone.name)
                if bone_ids is None or bone_ids.global_id is None:
                    continue

                bone_flip_states[bone_ids.global_id] = BoneProperties.get_instance(bl_bone).is_flipped

        return bone_flip_states

    def create_bones(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton, bone_transforms: list[_BoneTransform]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        with BlenderHelper.enter_edit_mode():
            for i, tr_bone in enumerate(tr_skeleton.bones):
                bl_bone = bl_armature.edit_bones.new(BlenderNaming.make_bone_name(None, tr_bone.global_id, i))
                bl_bone.head = bone_transforms[i].head
                bl_bone.tail = bone_transforms[i].tail

                relative_x_axis = bl_bone.matrix.to_quaternion().inverted() @ bone_transforms[i].orientation @ Vector((1, 0, 0))
                bl_bone.roll = -math.atan2(relative_x_axis.z, relative_x_axis.x)

                if tr_bone.parent_id >= 0:
                    parent_bone_name = BlenderNaming.make_bone_name(None, tr_skeleton.bones[tr_bone.parent_id].global_id, tr_bone.parent_id)
                    bl_bone.parent = bl_armature.edit_bones[parent_bone_name]

        for i, tr_bone in enumerate(tr_skeleton.bones):
            bl_bone = bl_armature.bones[BlenderNaming.make_bone_name(None, tr_bone.global_id, i)]
            BoneProperties.get_instance(bl_bone).is_flipped = bone_transforms[i].is_flipped
            if len(tr_bone.constraints) > 0:
                BlenderHelper.add_bone_to_group(bl_armature_obj, bl_bone, BlenderNaming.constrained_bone_group_name, BlenderNaming.constrained_bone_palette_name)

    def assign_counterparts(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        for i, tr_bone in enumerate(tr_skeleton.bones):
            if tr_bone.counterpart_local_id is None:
                continue

            bone_name = BlenderNaming.make_bone_name(None, tr_bone.global_id, i)
            counterpart_bone_name = BlenderNaming.make_bone_name(None, tr_skeleton.bones[tr_bone.counterpart_local_id].global_id, tr_bone.counterpart_local_id)
            BoneProperties.get_instance(bl_armature.bones[bone_name]).counterpart_bone_name = counterpart_bone_name

    def create_constraints(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton, bone_transforms: list[_BoneTransform]) -> None:
        if bl_armature_obj.pose is None:
            return

        for i, tr_bone in enumerate(tr_skeleton.bones):
            bl_pose_bone = bl_armature_obj.pose.bones[BlenderNaming.make_bone_name(None, tr_bone.global_id, i)]
            for tr_constraint in tr_bone.constraints:
                self.create_constraint(bl_armature_obj, bone_transforms, bl_pose_bone, tr_constraint)

    def create_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        bl_pose_bone: bpy.types.PoseBone,
        tr_constraint: IBoneConstraint
    ) -> None:
        constraint_properties = BoneProperties.get_instance(bl_pose_bone.bone).constraints.add()
        constraint_properties.data = tr_constraint.serialize()

        match tr_constraint.type:
            case BoneConstraintType.LOOK_AT:
                self.create_look_at_constraint(bl_armature_obj, bone_transforms, bl_pose_bone, cast(IBoneConstraint_LookAt, tr_constraint))

            case BoneConstraintType.WEIGHTED_POSITION:
                self.create_weighted_position_constraint(bl_armature_obj, bone_transforms, bl_pose_bone, cast(IBoneConstraint_WeightedPosition, tr_constraint))

            case BoneConstraintType.WEIGHTED_ROTATION:
                self.create_weighted_rotation_constraint(bl_armature_obj, bone_transforms, bl_pose_bone, cast(IBoneConstraint_WeightedRotation, tr_constraint))

            case _:
                pass

    def create_look_at_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        bl_pose_bone: bpy.types.PoseBone,
        tr_constraint: IBoneConstraint_LookAt
    ) -> None:
        bl_curves = cast(list[bpy.types.FCurve], bl_pose_bone.driver_add("rotation_quaternion"))
        for elem_idx, bl_curve in enumerate(bl_curves):
            bl_driver = cast(bpy.types.Driver, bl_curve.driver)
            bl_driver.use_self = True
            #armature_rotation = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "rotation_quaternion")
            looker_position   = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, bone_transforms, tr_constraint.target_bone_local_id, "location")
            look_at_positions = self.make_driver_expr_for_bones_attr(bl_driver, bl_armature_obj, bone_transforms, tr_constraint.source_bone_local_ids, "location")
            look_at_weights   = self.float_list_to_string(tr_constraint.source_bone_weights)

            pole_dir_expr: str
            if tr_constraint.pole_bone_local_id is not None and tr_constraint.pole_dir is None:
                pole_position = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, bone_transforms, tr_constraint.pole_bone_local_id, "location")
                pole_dir_expr = f"tr_dir({looker_position},{pole_position})"
            elif tr_constraint.pole_bone_local_id is not None and tr_constraint.pole_dir is not None:
                pole_dir = tr_constraint.pole_dir
                if bone_transforms[tr_constraint.pole_bone_local_id].is_flipped:
                    pole_dir = SkeletonImporter.tr_flip_quat @ pole_dir

                pole_dir_expr = self.float_tuple_to_string(pole_dir)
                pole_bone_rotation = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, bone_transforms, tr_constraint.pole_bone_local_id, "rotation_quaternion")
                pole_dir_expr = f"tr_rotate({pole_dir_expr},{pole_bone_rotation})"
            elif tr_constraint.pole_dir is not None:
                pole_dir_expr = self.float_tuple_to_string(tr_constraint.pole_dir)
            else:
                pole_dir_expr = "(0,0,1)"

            bone_local_tangent = tr_constraint.bone_local_tangent
            bone_local_normal  = tr_constraint.bone_local_normal
            if bone_transforms[tr_constraint.target_bone_local_id].is_flipped:
                bone_local_tangent = SkeletonImporter.tr_flip_quat @ bone_local_tangent
                bone_local_normal  = SkeletonImporter.tr_flip_quat @ bone_local_normal

            bone_local_tangent_expr = self.float_tuple_to_string(bone_local_tangent)
            bone_local_normal_expr  = self.float_tuple_to_string(bone_local_normal)
            bl_driver.expression = f"tr_look_at(self,{looker_position},{look_at_positions},{look_at_weights},{pole_dir_expr},{bone_local_tangent_expr},{bone_local_normal_expr})[{elem_idx}]"

    def create_weighted_position_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        bl_pose_bone: bpy.types.PoseBone,
        tr_constraint: IBoneConstraint_WeightedPosition
    ) -> None:
        self.create_weighted_attr_drivers(
            bl_armature_obj,
            bone_transforms,
            bl_pose_bone,
            "tr_weighted_pos",
            tr_constraint.source_bone_local_ids,
            tr_constraint.source_bone_weights,
            tr_constraint.offset,
            "location"
        )

    def create_weighted_rotation_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        bl_pose_bone: bpy.types.PoseBone,
        tr_constraint: IBoneConstraint_WeightedRotation
    ) -> None:
        offset = tr_constraint.offset
        if bone_transforms[tr_constraint.target_bone_local_id].is_flipped:
            offset = offset @ SkeletonImporter.tr_flip_quat

        self.create_weighted_attr_drivers(
            bl_armature_obj,
            bone_transforms,
            bl_pose_bone,
            "tr_weighted_rot",
            tr_constraint.source_bone_local_ids,
            tr_constraint.source_bone_weights,
            offset,
            "rotation_quaternion"
        )

    def create_weighted_attr_drivers(
        self,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        bl_pose_bone: bpy.types.PoseBone,
        driver_func_name: str,
        source_bone_local_ids: list[int],
        source_bone_weights: list[float],
        offset: Vector | Quaternion,
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> None:
        bl_curves = cast(list[bpy.types.FCurve], bl_pose_bone.driver_add(attr_name))
        for elem_idx, bl_curve in enumerate(bl_curves):
            bl_driver = cast(bpy.types.Driver, bl_curve.driver)
            bl_driver.use_self = True
            #armature_position = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "location")
            #armature_rotation = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "rotation_quaternion")
            bone_attrs        = self.make_driver_expr_for_bones_attr(bl_driver, bl_armature_obj, bone_transforms, source_bone_local_ids, attr_name)
            bone_flip_flags   = self.bone_flip_flags_to_string(bone_transforms, source_bone_local_ids)
            weights_expr      = self.float_list_to_string(source_bone_weights)
            offset_expr       = self.float_tuple_to_string(offset)
            bl_driver.expression = f"{driver_func_name}(self,{bone_attrs},{bone_flip_flags},{weights_expr},{offset_expr})[{elem_idx}]"

    def make_driver_expr_for_bones_attr(
        self,
        bl_driver: bpy.types.Driver,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        local_bone_ids: list[int],
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> str:
        expr = "["
        for i, local_bone_id in enumerate(local_bone_ids):
            if i > 0:
                expr += ","

            expr += self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, bone_transforms, local_bone_id, attr_name)

        expr += "]"
        return expr

    def make_driver_expr_for_bone_attr(
        self,
        bl_driver: bpy.types.Driver,
        bl_armature_obj: bpy.types.Object,
        bone_transforms: list[_BoneTransform],
        local_bone_id: int,
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> str:
        bone_name = BlenderNaming.make_bone_name(None, bone_transforms[local_bone_id].global_id, local_bone_id)
        return self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, bone_name, attr_name)

    def make_driver_expr_for_obj_attr(
        self,
        bl_driver: bpy.types.Driver,
        bl_obj: bpy.types.Object,
        bone_name: str | None,
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> str:
        coords: str
        transform_type_prefix: str
        match attr_name:
            case "location":
                coords = "xyz"
                transform_type_prefix = "LOC_"
            case "rotation_quaternion":
                coords = "wxyz"
                transform_type_prefix = "ROT_"

        expr = "("
        for i, coord in enumerate(coords):
            bl_var = bl_driver.variables.new()
            bl_var.name = f"v{len(bl_driver.variables)}"
            bl_var.type = "TRANSFORMS"
            bl_var.targets[0].id = bl_obj
            if bone_name is not None:
                bl_var.targets[0].bone_target = bone_name

            bl_var.targets[0].transform_type = cast(Any, transform_type_prefix + coord.upper())
            bl_var.targets[0].rotation_mode = "QUATERNION"

            if i > 0:
                expr += ","

            expr += bl_var.name

        expr += ")"
        return expr

    def float_list_to_string(self, values: list[float]) -> str:
        return "[" + ",".join(Enumerable(values).select(self.float_to_string)) + "]"

    def float_tuple_to_string(self, value: tuple[float, ...] | Vector | Quaternion) -> str:
        if isinstance(value, Vector):
            value = (value.x, value.y, value.z)
        elif isinstance(value, Quaternion):
            value = (value.w, value.x, value.y, value.z)

        return "(" + ",".join(Enumerable(value).select(self.float_to_string)) + ")"

    def float_to_string(self, value: float) -> str:
        if abs(value) < 0.00001:
            return "0"

        fpart, _ = math.modf(value)
        if fpart == 0:
            value = int(value)
        else:
            value = round(value, 5)

        return str(value)

    def bone_flip_flags_to_string(self, bone_transforms: list[_BoneTransform], local_bone_ids: list[int]) -> str:
        result = "["
        for i, local_bone_id in enumerate(local_bone_ids):
            if i > 0:
                result += ","

            result += "1" if bone_transforms[local_bone_id].is_flipped else "0"

        result += "]"
        return result

    def to_blender_orientation(self, tr_orientation: Quaternion) -> Quaternion:
        matrix = tr_orientation.to_matrix()
        matrix = Matrix((
            (matrix[0][1], matrix[0][2], matrix[0][0]),
            (matrix[1][1], matrix[1][2], matrix[1][0]),
            (matrix[2][1], matrix[2][2], matrix[2][0])
        ))
        return matrix.to_quaternion()
