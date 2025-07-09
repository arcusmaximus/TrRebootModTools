import bpy
import math
from typing import Any, Literal, NamedTuple, cast
from mathutils import Matrix, Quaternion, Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderBoneIdSet, BlenderNaming
from io_scene_tr_reboot.DriverFunctions import DriverFunctions
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectSkeletonProperties
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.BoneConstraint import BoneConstraintType, IBoneConstraint, IBoneConstraint_WeightedPosition, IBoneConstraint_WeightedRotation, IBoneConstraint_LookAt
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class _ConstrainedBoneSet(NamedTuple):
    tr_bone: IBone
    bl_constrained_pose_bone: bpy.types.PoseBone
    bl_helper_pose_bone: bpy.types.PoseBone | None

class SkeletonImporter(SlotsBase):
    scale_factor: float
    bl_target_collection: bpy.types.Collection | None

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

                with BlenderHelper.enter_edit_mode():
                    self.create_bones(bl_armature, tr_skeleton)

                self.assign_counterparts(bl_armature_obj, tr_skeleton)
                self.create_constraints(bl_armature_obj, tr_skeleton)
            else:
                bl_armature_obj = BlenderHelper.create_object(bl_armature)

            bl_armature_obj.show_in_front = True
            ObjectSkeletonProperties.set_global_blend_shape_ids(bl_armature_obj, tr_skeleton.global_blend_shape_ids)
            BlenderHelper.move_object_to_collection(bl_armature_obj, self.bl_target_collection)

            bl_armature_objs[skeleton_resource] = bl_armature_obj

        return bl_armature_objs

    def create_bones(self, bl_armature: bpy.types.Armature, tr_skeleton: ISkeleton) -> None:
        bone_child_indices: list[list[int]] = []
        bone_heads: list[Vector] = []

        for i, tr_bone in enumerate(tr_skeleton.bones):
            bone_child_indices.append([])
            if tr_bone.parent_id < 0:
                bone_heads.append(tr_bone.relative_location * self.scale_factor)
            else:
                bone_heads.append(bone_heads[tr_bone.parent_id] + tr_bone.relative_location * self.scale_factor)
                bone_child_indices[tr_bone.parent_id].append(i)

        for i, tr_bone in enumerate(tr_skeleton.bones):
            bl_bone = bl_armature.edit_bones.new(BlenderNaming.make_bone_name(None, tr_bone.global_id, i))

            orientation_matrix = tr_bone.absolute_orientation.to_matrix()
            orientation_matrix = Matrix((
                (orientation_matrix[0][1], orientation_matrix[0][2], orientation_matrix[0][0]),
                (orientation_matrix[1][1], orientation_matrix[1][2], orientation_matrix[1][0]),
                (orientation_matrix[2][1], orientation_matrix[2][2], orientation_matrix[2][0])
            ))
            orientation = orientation_matrix.to_quaternion()

            bl_bone.head = bone_heads[i]
            tail_offset: Vector
            if tr_bone.global_id is None:
                tail_offset = Vector((0, 0, 0.01))
            else:
                bone_length: float
                if len(bone_child_indices[i]) > 0:
                    avg_child_head = Enumerable(bone_child_indices[i]).avg(lambda idx: bone_heads[idx])
                    bone_length = (avg_child_head - bone_heads[i]).length
                else:
                    bone_length = tr_bone.distance_from_parent * self.scale_factor

                tail_offset = (orientation @ Vector((0, 1, 0))) * max(bone_length, 0.5 * self.scale_factor)

            bl_bone.tail = bl_bone.head + tail_offset

            relative_orientation = bl_bone.matrix.to_quaternion().inverted() @ orientation
            bl_bone.roll = relative_orientation.angle
            if (relative_orientation.w < 0) != (relative_orientation.y < 0):
                bl_bone.roll = -bl_bone.roll

            if tr_bone.parent_id >= 0:
                bl_bone.parent = bl_armature.edit_bones[BlenderNaming.make_bone_name(None, tr_skeleton.bones[tr_bone.parent_id].global_id, tr_bone.parent_id)]

    def assign_counterparts(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        for i, tr_bone in enumerate(tr_skeleton.bones):
            if tr_bone.counterpart_local_id is None:
                continue

            bone_name = BlenderNaming.make_bone_name(None, tr_bone.global_id, i)
            counterpart_bone_name = BlenderNaming.make_bone_name(None, tr_skeleton.bones[tr_bone.counterpart_local_id].global_id, tr_bone.counterpart_local_id)
            BoneProperties.get_instance(bl_armature.bones[bone_name]).counterpart_bone_name = counterpart_bone_name

    def create_constraints(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> None:
        if bl_armature_obj.pose is None:
            raise Exception("Armature has no pose")

        DriverFunctions.register()
        constrained_bone_sets = self.create_helper_bones(bl_armature_obj, tr_skeleton)
        for constrained_bone_set in constrained_bone_sets:
            for tr_constraint in constrained_bone_set.tr_bone.constraints:
                self.create_constraint(bl_armature_obj, constrained_bone_set, tr_skeleton, tr_constraint)

        # Hack to make the now-constrained bones settle into place
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            pass

    def create_helper_bones(self, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> list[_ConstrainedBoneSet]:
        if bl_armature_obj.pose is None:
            return []

        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        constrained_bone_id_sets: list[BlenderBoneIdSet] = []
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for i, tr_bone in enumerate(tr_skeleton.bones):
                if len(tr_bone.constraints) == 0 or tr_bone.global_id is None:
                    continue

                constrained_bone_id_sets.append(BlenderBoneIdSet(None, tr_bone.global_id, i))

                bl_helper_bone = bl_armature.edit_bones.new(BlenderNaming.make_helper_bone_name(tr_bone.global_id))
                bl_helper_bone.tail = (0, 1, 0)

        constrained_bone_sets: list[_ConstrainedBoneSet] = []
        for constrained_bone_id_set in constrained_bone_id_sets:
            if constrained_bone_id_set.global_id is None or constrained_bone_id_set.local_id is None:
                raise Exception()

            tr_bone = tr_skeleton.bones[constrained_bone_id_set.local_id]

            constrained_bone_name = BlenderNaming.make_bone_name(constrained_bone_id_set)
            bl_constrained_bone = bl_armature.bones[constrained_bone_name]
            bl_constrained_pose_bone = bl_armature_obj.pose.bones[constrained_bone_name]
            BlenderHelper.add_bone_to_group(bl_armature_obj, bl_constrained_bone, BlenderNaming.constrained_bone_group_name, BlenderNaming.constrained_bone_palette_name)

            helper_bone_name = BlenderNaming.make_helper_bone_name(constrained_bone_id_set.global_id)
            bl_helper_bone = bl_armature.bones[helper_bone_name]
            bl_helper_pose_bone = bl_armature_obj.pose.bones[helper_bone_name]
            BlenderHelper.add_bone_to_group(bl_armature_obj, bl_helper_bone, BlenderNaming.helper_bone_group_name, BlenderNaming.helper_bone_palette_name, False)

            constrained_bone_sets.append(_ConstrainedBoneSet(tr_bone, bl_constrained_pose_bone, bl_helper_pose_bone))

        return constrained_bone_sets

    def create_constraint(self, bl_armature_obj: bpy.types.Object, constrained_bone_set: _ConstrainedBoneSet, tr_skeleton: ISkeleton, tr_constraint: IBoneConstraint) -> None:
        bl_bone = constrained_bone_set.bl_constrained_pose_bone.bone
        prop_constraints = BoneProperties.get_instance(bl_bone).constraints
        prop_constraint = prop_constraints.add()
        prop_constraint.data = tr_constraint.serialize()

        match tr_constraint.type:
            case BoneConstraintType.LOOK_AT:
                self.create_look_at_constraint(bl_armature_obj, constrained_bone_set, tr_skeleton, cast(IBoneConstraint_LookAt, tr_constraint))

            case BoneConstraintType.COPY_POSITION:
                self.create_weighted_position_constraint(bl_armature_obj, constrained_bone_set, tr_skeleton, cast(IBoneConstraint_WeightedPosition, tr_constraint))

            case BoneConstraintType.COPY_ROTATION:
                self.create_weighted_rotation_constraint(bl_armature_obj, constrained_bone_set, tr_skeleton, cast(IBoneConstraint_WeightedRotation, tr_constraint))

            case _:
                pass

    def create_look_at_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        constrained_bone_set: _ConstrainedBoneSet,
        tr_skeleton: ISkeleton,
        tr_constraint: IBoneConstraint_LookAt
    ) -> None:
        (tr_bone, bl_constrained_pose_bone, bl_helper_pose_bone) = constrained_bone_set
        if tr_bone.parent_id < 0 or bl_helper_pose_bone is None:
            return

        bl_curves = cast(list[bpy.types.FCurve], bl_helper_pose_bone.driver_add("rotation_quaternion"))
        for elem_idx, bl_curve in enumerate(bl_curves):
            bl_driver = cast(bpy.types.Driver, bl_curve.driver)
            armature_rotation = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "rotation_quaternion")
            looker_position   = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, tr_skeleton, tr_constraint.target_bone_local_id, "location")
            look_at_positions = self.make_driver_expr_for_bones_attr(bl_driver, bl_armature_obj, tr_skeleton, tr_constraint.source_bone_local_ids, "location")
            look_at_weights   = self.float_list_to_string(tr_constraint.source_bone_weights)

            pole_dir: str
            if tr_constraint.pole_bone_local_id is not None and tr_constraint.pole_dir is None:
                pole_position = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, tr_skeleton, tr_constraint.pole_bone_local_id, "location")
                pole_dir = f"tr_dir({looker_position},{pole_position})"
            elif tr_constraint.pole_dir is not None and tr_constraint.pole_bone_local_id is not None and tr_constraint.pole_bone_orientation is not None:
                pole_dir = self.float_tuple_to_string(tr_constraint.pole_dir)
                pole_rotation = self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, tr_skeleton, tr_constraint.pole_bone_local_id, "rotation_quaternion")
                pole_dir = f"tr_rotate({pole_dir},{pole_rotation})"
            elif tr_constraint.pole_dir is not None:
                pole_dir = self.float_tuple_to_string(tr_constraint.pole_dir)
            else:
                pole_dir = "(0,0,1)"

            bone_local_tangent = self.float_tuple_to_string(tr_constraint.bone_local_tangent)
            bone_local_normal = self.float_tuple_to_string(tr_constraint.bone_local_normal)
            bl_driver.expression = f"tr_look_at({armature_rotation},{looker_position},{look_at_positions},{look_at_weights},{pole_dir},{bone_local_tangent},{bone_local_normal})[{elem_idx}]"

        bl_constraint = cast(bpy.types.CopyRotationConstraint, bl_constrained_pose_bone.constraints.new("COPY_ROTATION"))
        bl_constraint.target = bl_armature_obj
        bl_constraint.subtarget = bl_helper_pose_bone.name

    def create_weighted_position_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        constrained_bone_set: _ConstrainedBoneSet,
        tr_skeleton: ISkeleton,
        tr_constraint: IBoneConstraint_WeightedPosition
    ) -> None:
        (_, bl_constrained_pose_bone, bl_helper_pose_bone) = constrained_bone_set
        if bl_helper_pose_bone is None:
            return

        self.create_weighted_attr_drivers(
            bl_armature_obj,
            bl_helper_pose_bone,
            tr_skeleton,
            "tr_weighted_pos",
            tr_constraint.source_bone_local_ids,
            tr_constraint.source_bone_weights,
            tr_constraint.offset,
            "location"
        )
        bl_constraint = cast(bpy.types.CopyLocationConstraint, bl_constrained_pose_bone.constraints.new("COPY_LOCATION"))
        bl_constraint.target = bl_armature_obj
        bl_constraint.subtarget = bl_helper_pose_bone.name

    def create_weighted_rotation_constraint(
        self,
        bl_armature_obj: bpy.types.Object,
        constrained_bone_set: _ConstrainedBoneSet,
        tr_skeleton: ISkeleton,
        tr_constraint: IBoneConstraint_WeightedRotation
    ) -> None:
        (_, bl_constrained_pose_bone, bl_helper_pose_bone) = constrained_bone_set
        if bl_helper_pose_bone is None:
            return

        self.create_weighted_attr_drivers(
            bl_armature_obj,
            bl_helper_pose_bone,
            tr_skeleton,
            "tr_weighted_rot",
            tr_constraint.source_bone_local_ids,
            tr_constraint.source_bone_weights,
            tr_constraint.offset,
            "rotation_quaternion"
        )
        bl_constraint = cast(bpy.types.CopyRotationConstraint, bl_constrained_pose_bone.constraints.new("COPY_ROTATION"))
        bl_constraint.target = bl_armature_obj
        bl_constraint.subtarget = bl_helper_pose_bone.name

    def create_weighted_attr_drivers(
        self,
        bl_armature_obj: bpy.types.Object,
        bl_helper_pose_bone: bpy.types.PoseBone,
        tr_skeleton: ISkeleton,
        driver_func_name: str,
        source_bone_local_ids: list[int],
        source_bone_weights: list[float],
        offset: Vector | Quaternion,
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> None:
        bl_curves = cast(list[bpy.types.FCurve], bl_helper_pose_bone.driver_add(attr_name))
        for elem_idx, bl_curve in enumerate(bl_curves):
            bl_driver = cast(bpy.types.Driver, bl_curve.driver)
            armature_position = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "location")
            armature_rotation = self.make_driver_expr_for_obj_attr(bl_driver, bl_armature_obj, None, "rotation_quaternion")
            bone_attrs        = self.make_driver_expr_for_bones_attr(bl_driver, bl_armature_obj, tr_skeleton, source_bone_local_ids, attr_name)
            weights_expr      = self.float_list_to_string(source_bone_weights)
            offset_expr       = self.float_tuple_to_string(offset)
            bl_driver.expression = f"{driver_func_name}({armature_position},{armature_rotation},{bone_attrs},{weights_expr},{offset_expr})[{elem_idx}]"

    def make_driver_expr_for_bones_attr(
        self,
        bl_driver: bpy.types.Driver,
        bl_armature_obj: bpy.types.Object,
        tr_skeleton: ISkeleton,
        local_bone_ids: list[int],
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> str:
        expr = "["
        for i, local_bone_id in enumerate(local_bone_ids):
            if i > 0:
                expr += ","

            expr += self.make_driver_expr_for_bone_attr(bl_driver, bl_armature_obj, tr_skeleton, local_bone_id, attr_name)

        expr += "]"
        return expr

    def make_driver_expr_for_bone_attr(
        self,
        bl_driver: bpy.types.Driver,
        bl_armature_obj: bpy.types.Object,
        tr_skeleton: ISkeleton,
        local_bone_id: int,
        attr_name: Literal["location"] | Literal["rotation_quaternion"]
    ) -> str:
        tr_bone = tr_skeleton.bones[local_bone_id]
        bone_name = BlenderNaming.make_bone_name(None, tr_bone.global_id, local_bone_id)
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
