from typing import NamedTuple, cast
import bpy
from mathutils import Matrix, Quaternion
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.shadow.ShadowAnimation import ShadowAnimation
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.IoHelper import IoHelper
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class _ItemAttrKey(NamedTuple):
    global_item_id: int
    attr_idx: int

class ShadowAnimationImporter(SlotsBase):
    scale_factor: float
    bl_context: bpy.types.Context

    def __init__(self, scale_factor: float) -> None:
        self.scale_factor = scale_factor
        self.bl_context = bpy.context

    def import_animation(self, file_path: str, bl_armature_obj: bpy.types.Object) -> None:
        if self.bl_context.scene is None:
            return

        resource_key = Collection.try_parse_resource_file_path(file_path, CdcGame.SOTTR)
        if resource_key is None:
            raise Exception("Invalid filename")

        data: bytes
        with IoHelper.open_read(file_path) as file:
            data = file.read()

        animation = ShadowAnimation(resource_key.id)
        animation.read(ResourceReader(resource_key, data, True, CdcGame.SOTTR))

        self.bl_context.scene.frame_start = 0
        self.bl_context.scene.frame_end = animation.num_frames
        self.bl_context.scene.render.fps = 2500
        self.bl_context.scene.render.fps_base = animation.ms_per_frame

        self.import_bone_animation(bl_armature_obj, animation)
        for bl_mesh_obj in Enumerable(bl_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            self.import_blend_shape_animation(bl_mesh_obj, animation)

    def import_bone_animation(self, bl_armature_obj: bpy.types.Object, animation: ShadowAnimation) -> None:
        BlenderHelper.reset_pose(bl_armature_obj)

        bl_fcurves: dict[_ItemAttrKey, list[bpy.types.FCurve]] = self.create_bone_fcurves(bl_armature_obj, animation)
        rest_matrices: dict[int, Matrix] = self.get_armature_space_rest_matrices(bl_armature_obj)
        rest_rotations: dict[int, Quaternion] = Enumerable(rest_matrices.items()).to_dict(lambda p: p[0], lambda p: p[1].to_quaternion())

        for global_bone_id, bone_frames in animation.bone_frames.items():
            for attr_idx in range(3):
                bl_attr_fcurves = bl_fcurves.get(_ItemAttrKey(global_bone_id, attr_idx))
                if bl_attr_fcurves is None:
                    continue

                for frame_idx, bone_frame in enumerate(bone_frames):
                    attr_value = bone_frame.get_attr_value(attr_idx)
                    if attr_value is None:
                        continue

                    match attr_idx:
                        case 0:
                            attr_value = rest_rotations[global_bone_id].inverted() @ cast(Quaternion, attr_value) @ rest_rotations[global_bone_id]
                        case 1:
                            attr_value = (rest_matrices[global_bone_id].inverted() @ Matrix.Translation(attr_value) @ rest_matrices[global_bone_id]).translation * self.scale_factor
                        case 2:
                            attr_value = [attr_value[2], attr_value[0], attr_value[1]]
                        case _:
                            pass

                    for element_idx in range(len(attr_value)):
                        bl_keyframe = bl_attr_fcurves[element_idx].keyframe_points.insert(frame_idx, attr_value[element_idx])
                        bl_keyframe.interpolation = "LINEAR"

    def create_bone_fcurves(self, bl_armature_obj: bpy.types.Object, animation: ShadowAnimation) -> dict[_ItemAttrKey, list[bpy.types.FCurve]]:
        if bl_armature_obj.animation_data is None:
            bl_armature_obj.animation_data_create()

        if bl_armature_obj.pose is None or bl_armature_obj.animation_data is None:
            return {}

        action_name = BlenderNaming.make_action_name(animation.id, None, None)
        bl_action = bpy.data.actions.get(action_name) or bpy.data.actions.new(action_name)
        bl_action.fcurves.clear()
        bl_armature_obj.animation_data.action = bl_action

        bl_attr_fcurves: dict[_ItemAttrKey, list[bpy.types.FCurve]] = {}

        for bl_bone in bl_armature_obj.pose.bones:
            global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
            if global_bone_id is None:
                continue

            for attr_idx, attr_name in enumerate(["rotation_quaternion", "location", "scale"]):
                bl_element_fcurves: list[bpy.types.FCurve] = []
                bl_attr_fcurves[_ItemAttrKey(global_bone_id, attr_idx)] = bl_element_fcurves
                for element_idx in range(attr_idx == 0 and 4 or 3):
                    bl_element_fcurves.append(bl_action.fcurves.new(f'pose.bones["{bl_bone.name}"].{attr_name}', index = element_idx, action_group = bl_bone.name))

        if hasattr(bl_action, "slots") and len(bl_action.slots) > 0:
            bl_armature_obj.animation_data.action_slot = bl_action.slots[0]

        return bl_attr_fcurves

    def get_armature_space_rest_matrices(self, bl_armature_obj: bpy.types.Object) -> dict[int, Matrix]:
        matrices: dict[int, Matrix] = {}
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for bl_bone in cast(bpy.types.Armature, bl_armature_obj.data).edit_bones:
                global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
                if global_bone_id is None:
                    continue

                matrices[global_bone_id] = bl_bone.matrix

        return matrices

    def import_blend_shape_animation(self, bl_mesh_obj: bpy.types.Object, animation: ShadowAnimation) -> None:
        bl_mesh = cast(bpy.types.Mesh, bl_mesh_obj.data)
        if bl_mesh.shape_keys is None:
            return

        for bl_shape_key in Enumerable(bl_mesh.shape_keys.key_blocks).skip(1):
            bl_shape_key.value = 0
            bl_shape_key.slider_min = -10
            bl_shape_key.slider_max = 10

        bl_fcurves: dict[int, bpy.types.FCurve] = self.create_blend_shape_fcurves(bl_mesh_obj, animation)

        for blend_shape_id, blend_shape_frames in animation.blend_shape_frames.items():
            bl_fcurve = bl_fcurves.get(blend_shape_id)
            if bl_fcurve is None:
                continue

            for frame_idx, blend_shape_frame in enumerate(blend_shape_frames):
                bl_keyframe = bl_fcurve.keyframe_points.insert(frame_idx, blend_shape_frame.value)
                bl_keyframe.interpolation = "LINEAR"

    def create_blend_shape_fcurves(self, bl_mesh_obj: bpy.types.Object, animation: ShadowAnimation) -> dict[int, bpy.types.FCurve]:
        bl_mesh = cast(bpy.types.Mesh, bl_mesh_obj.data)
        if bl_mesh.shape_keys is None:
            return {}

        if bl_mesh.shape_keys.animation_data is None:
            bl_mesh.shape_keys.animation_data_create()
            if bl_mesh.shape_keys.animation_data is None:
                return {}

        mesh_id_set = BlenderNaming.parse_mesh_name(bl_mesh_obj.name)
        action_name = BlenderNaming.make_action_name(animation.id, mesh_id_set.model_data_id, mesh_id_set.mesh_idx)
        bl_action = bpy.data.actions.get(action_name) or bpy.data.actions.new(action_name)
        bl_mesh.shape_keys.animation_data.action = bl_action
        bl_action.fcurves.clear()

        bl_fcurves: dict[int, bpy.types.FCurve] = {}

        for bl_shape_key in Enumerable(bl_mesh.shape_keys.key_blocks).skip(1):
            id_set = BlenderNaming.try_parse_shape_key_name(bl_shape_key.name)
            if id_set is not None and id_set.global_id is not None and id_set.global_id in animation.blend_shape_frames:
                bl_fcurves[id_set.global_id] = bl_action.fcurves.new(f'key_blocks["{bl_shape_key.name}"].value')

        return bl_fcurves
