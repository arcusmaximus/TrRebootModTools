from typing import NamedTuple, Sequence, cast
import bpy
import math
import re
from mathutils import Matrix, Quaternion
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.shadow.ShadowAnimation import BlendShapeAnimationFrame, BoneAnimationFrame, ShadowAnimation
from io_scene_tr_reboot.util.DictionaryExtensions import DictionaryExtensions
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.IoHelper import IoHelper
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class _BoneConstraintParams(NamedTuple):
    tracking_bone_id: int
    target_bone_id: int
    opposite_direction: bool

class ShadowAnimationExporter(SlotsBase):
    scale_factor: float
    apply_lara_bone_fix_constraints: bool

    def __init__(self, scale_factor: float, apply_lara_bone_fix_constraints: bool) -> None:
        self.scale_factor = scale_factor
        self.apply_lara_bone_fix_constraints = apply_lara_bone_fix_constraints

    def export_animation(self, file_path: str, bl_armature_obj: bpy.types.Object) -> None:
        if bpy.context.scene is None:
            return

        BlenderHelper.switch_to_object_mode()

        resource_key = Collection.try_parse_resource_file_path(file_path, CdcGame.SOTTR)
        if resource_key is None:
            raise Exception("Invalid target filename")

        animation = ShadowAnimation(resource_key.id)
        animation.num_frames = bpy.context.scene.frame_end
        animation.ms_per_frame = int(bpy.context.scene.render.fps_base)

        self.export_armature_animation(animation, bl_armature_obj)
        for bl_mesh_obj in Enumerable(bl_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            self.export_mesh_animation(animation, bl_mesh_obj)

        resource_builder = ResourceBuilder(ResourceKey(ResourceType.ANIMATION, 0), CdcGame.SOTTR)
        animation.write(resource_builder)
        with IoHelper.open_write(file_path) as file:
            file.write(resource_builder.build())

    def export_armature_animation(self, animation: ShadowAnimation, bl_armature_obj: bpy.types.Object) -> None:
        if self.apply_lara_bone_fix_constraints:
            self.apply_bone_fix_constraints(
                bl_armature_obj,
                [
                    # Arms
                    _BoneConstraintParams(105, 97, False),
                    _BoneConstraintParams(123, 97, False),
                    _BoneConstraintParams(106, 102, False),
                    _BoneConstraintParams(124, 102, False),

                    # Legs
                    _BoneConstraintParams(120, 111, True),
                    _BoneConstraintParams(121, 115, True)
                ]
            )

        self.bake_bone_constraints(bl_armature_obj)

        bl_bone_fcurves: dict[int, dict[int, list[bpy.types.FCurve | None]]] = self.collect_bone_fcurves(bl_armature_obj)
        bone_distances_from_parent: dict[int, float] = {}

        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for bl_bone in cast(bpy.types.Armature, bl_armature_obj.data).edit_bones:
                global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
                if global_bone_id is None:
                    continue

                if bl_bone.parent:
                    bone_distances_from_parent[global_bone_id] = (bl_bone.head - bl_bone.parent.head).length / self.scale_factor
                else:
                    bone_distances_from_parent[global_bone_id] = 1.0

                bl_attr_fcurves = bl_bone_fcurves.get(global_bone_id)
                if bl_attr_fcurves is not None:
                    self.export_bone_animation(animation, bl_bone, global_bone_id, bl_attr_fcurves)

        animation.bone_distances_from_parent = Enumerable(animation.bone_frames.keys()).select(lambda id: bone_distances_from_parent[id]).to_list()

    def export_bone_animation(self, animation: ShadowAnimation, bl_bone: bpy.types.EditBone, global_bone_id: int, bl_attr_fcurves: dict[int, list[bpy.types.FCurve | None]]) -> None:
        bone_frames: list[BoneAnimationFrame] = []
        rest_matrix = bl_bone.matrix
        rest_rotation = rest_matrix.to_quaternion()

        for frame_idx in range(animation.num_frames):
            bone_frame = BoneAnimationFrame()
            for attr_idx, bl_elem_fcurves in bl_attr_fcurves.items():
                attr_value = attr_idx == 0 and [1.0, 0.0, 0.0, 0.0] or [0.0, 0.0, 0.0]
                for elem_idx, bl_elem_fcurve in enumerate(bl_elem_fcurves):
                    if bl_elem_fcurve is not None:
                        attr_value[elem_idx] = bl_elem_fcurve.evaluate(frame_idx)

                match attr_idx:
                    case 0:
                        attr_value = rest_rotation @ Quaternion(attr_value) @ rest_rotation.inverted()
                    case 1:
                        attr_value = (rest_matrix @ Matrix.Translation(attr_value) @ rest_matrix.inverted()).translation / self.scale_factor
                    case 2:
                        attr_value = [attr_value[1], attr_value[2], attr_value[0]]
                    case _:
                        pass

                bone_frame.set_attr_value(attr_idx, cast(Sequence[float], attr_value))

            bone_frames.append(bone_frame)

        animation.bone_frames[global_bone_id] = bone_frames

    def collect_bone_fcurves(self, bl_armature_obj: bpy.types.Object) -> dict[int, dict[int, list[bpy.types.FCurve | None]]]:
        bl_bone_fcurves: dict[int, dict[int, list[bpy.types.FCurve | None]]] = {}
        if not bl_armature_obj.animation_data or not bl_armature_obj.animation_data.action:
            return bl_bone_fcurves

        attr_names = Enumerable(["rotation_quaternion", "location", "scale"])
        for bl_fcurve in bl_armature_obj.animation_data.action.fcurves:
            match = re.fullmatch(r'pose\.bones\["(\w+)"\]\.(\w+)', bl_fcurve.data_path)
            if match is None or len(bl_fcurve.keyframe_points) == 0:
                continue

            global_bone_id = BlenderNaming.try_get_bone_global_id(match.group(1))
            attr_idx = attr_names.index_of(match.group(2))
            if global_bone_id is None or attr_idx < 0:
                continue

            bl_attr_fcurves = DictionaryExtensions.get_or_add(bl_bone_fcurves, global_bone_id, lambda: {})
            bl_element_fcurves = DictionaryExtensions.get_or_add(bl_attr_fcurves, attr_idx, lambda: [None] * (attr_idx == 0 and 4 or 3))
            bl_element_fcurves[bl_fcurve.array_index] = bl_fcurve

        return bl_bone_fcurves

    def export_mesh_animation(self, animation: ShadowAnimation, bl_mesh_obj: bpy.types.Object) -> None:
        bl_mesh_fcurves = self.collect_mesh_fcurves(bl_mesh_obj)

        for global_blend_shape_id, bl_fcurve in bl_mesh_fcurves.items():
            if global_blend_shape_id in animation.blend_shape_frames:
                continue

            blend_shape_frames: list[BlendShapeAnimationFrame] = []
            for frame_idx in range(animation.num_frames):
                blend_shape_frame = BlendShapeAnimationFrame()
                blend_shape_frame.value = bl_fcurve.evaluate(frame_idx)
                blend_shape_frames.append(blend_shape_frame)

            animation.blend_shape_frames[global_blend_shape_id] = blend_shape_frames

    def collect_mesh_fcurves(self, bl_mesh_obj: bpy.types.Object) -> dict[int, bpy.types.FCurve]:
        bl_mesh_fcurves: dict[int, bpy.types.FCurve] = {}
        bl_mesh = cast(bpy.types.Mesh, bl_mesh_obj.data)
        if not bl_mesh.shape_keys or not bl_mesh.shape_keys.animation_data or not bl_mesh.shape_keys.animation_data.action:
            return bl_mesh_fcurves

        for bl_fcurve in bl_mesh.shape_keys.animation_data.action.fcurves:
            match = re.fullmatch(r'key_blocks\["(\w+)"\]\.value', bl_fcurve.data_path)
            if match is None or len(bl_fcurve.keyframe_points) == 0:
                continue

            global_blend_shape_id = BlenderNaming.parse_shape_key_name(match.group(1)).global_id
            if global_blend_shape_id is None:
                continue

            bl_mesh_fcurves[global_blend_shape_id] = bl_fcurve

        return bl_mesh_fcurves

    def apply_bone_fix_constraints(self, bl_armature_obj: bpy.types.Object, constraints: Sequence[_BoneConstraintParams]) -> None:
        if bl_armature_obj.pose is None:
            return

        bl_bones: dict[int, bpy.types.PoseBone] = {}
        for bl_bone in bl_armature_obj.pose.bones:
            global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
            if global_bone_id is not None:
                bl_bones[global_bone_id] = bl_bone

        missing_bone_ids: list[str] = Enumerable(constraints).select(lambda c: c.tracking_bone_id)     \
                                                             .where(lambda id: id not in bl_bones)     \
                                                             .order_by(lambda id: id)                  \
                                                             .select(lambda id: str(id))               \
                                                             .to_list()
        if len(missing_bone_ids) > 0:
            OperatorContext.log_warning(f"Bones with IDs {', '.join(missing_bone_ids)} not found - limbs may not be animated properly ingame. Please make sure you have tr11_lara.drm imported.")
            return

        for constraint in constraints:
            bl_tracking_bone = bl_bones[constraint.tracking_bone_id]
            bl_target_bone   = bl_bones[constraint.target_bone_id]
            if len(bl_tracking_bone.constraints) > 0:
                return

            if constraint.opposite_direction:
                bl_location_constraint = cast(bpy.types.CopyLocationConstraint, bl_tracking_bone.constraints.new("COPY_LOCATION"))
                bl_location_constraint.target = bl_armature_obj
                bl_location_constraint.subtarget = bl_target_bone.name
                bl_location_constraint.target_space = "POSE"
                bl_location_constraint.owner_space = "POSE"

                bl_rotation_constraint = cast(bpy.types.TransformConstraint, bl_tracking_bone.constraints.new("TRANSFORM"))
                bl_rotation_constraint.target = bl_armature_obj
                bl_rotation_constraint.subtarget = bl_target_bone.name
                bl_rotation_constraint.use_motion_extrapolate = True
                bl_rotation_constraint.target_space = "POSE"
                bl_rotation_constraint.owner_space = "POSE"

                bl_rotation_constraint.map_from = "ROTATION"
                bl_rotation_constraint.from_rotation_mode = "YZX"
                bl_rotation_constraint.from_max_x_rot = 2*math.pi
                bl_rotation_constraint.from_max_y_rot = 2*math.pi
                bl_rotation_constraint.from_max_z_rot = 2*math.pi

                bl_rotation_constraint.map_to = "ROTATION"
                bl_rotation_constraint.to_euler_order = "YZX"
                bl_rotation_constraint.to_min_x_rot = 0
                bl_rotation_constraint.to_max_x_rot = 2*math.pi
                bl_rotation_constraint.to_min_y_rot = 3*math.pi
                bl_rotation_constraint.to_max_y_rot = math.pi
                bl_rotation_constraint.to_min_z_rot = math.pi
                bl_rotation_constraint.to_max_z_rot = 3*math.pi
                bl_rotation_constraint.mix_mode_rot = "REPLACE"
            else:
                bl_transforms_constraint = cast(bpy.types.CopyTransformsConstraint, bl_tracking_bone.constraints.new("COPY_TRANSFORMS"))
                bl_transforms_constraint.target = bl_armature_obj
                bl_transforms_constraint.subtarget = bl_target_bone.name
                bl_transforms_constraint.target_space = "POSE"
                bl_transforms_constraint.owner_space = "POSE"

    def bake_bone_constraints(self, bl_armature_obj: bpy.types.Object) -> None:
        if bpy.context.scene is None or bl_armature_obj.pose is None:
            return

        BlenderHelper.select_object(bl_armature_obj)
        bpy.ops.object.mode_set(mode = "POSE")

        with BlenderHelper.temporarily_show_all_bones(bl_armature_obj):
            for bl_bone in bl_armature_obj.pose.bones:
                bl_bone.bone.select = len(bl_bone.constraints) > 0

            bpy.ops.nla.bake(
                frame_start = bpy.context.scene.frame_start,
                frame_end   = bpy.context.scene.frame_end,
                only_selected = True,
                visual_keying = True,
                use_current_action = True
            )

        bpy.ops.object.mode_set(mode = "OBJECT")
