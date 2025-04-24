from typing import cast
import bpy
import os
from mathutils import Matrix, Quaternion, Vector
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderBoneIdSet, BlenderNaming
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectSkeletonProperties
from io_scene_tr_reboot.tr.Bone import IBone
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.util.Enumerable import Enumerable
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class SkeletonExporter(SlotsBase):
    scale_factor: float
    game: CdcGame
    factory: IFactory

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game
        self.factory = Factories.get(game)

    def export(self, folder_path: str, bl_armature_obj: bpy.types.Object) -> None:
        skeleton_id = BlenderNaming.parse_local_armature_name(bl_armature_obj.name)
        tr_skeleton = self.factory.create_skeleton(skeleton_id)

        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            self.add_blend_shape_id_mappings(tr_skeleton, bl_armature_obj)
            self.add_bones(tr_skeleton, bl_armature_obj)

        self.write_skeleton_file(folder_path, bl_armature_obj, tr_skeleton)

    def write_skeleton_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> None:
        writer = ResourceBuilder(ResourceKey(ResourceType.DTP, tr_skeleton.id), self.game)
        tr_skeleton.write(writer)

        file_path = os.path.join(folder_path, Collection.make_resource_file_name(writer.resource, self.game))
        with open(file_path, "wb") as file:
            file.write(writer.build())

    def add_blend_shape_id_mappings(self, tr_skeleton: ISkeleton, bl_armature_obj: bpy.types.Object) -> None:
        tr_skeleton.global_blend_shape_ids = ObjectSkeletonProperties.get_global_blend_shape_ids(bl_armature_obj)

    def add_bones(self, tr_skeleton: ISkeleton, bl_armature_obj: bpy.types.Object) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_ids: dict[bpy.types.EditBone, BlenderBoneIdSet] = Enumerable(bl_armature.edit_bones).to_dict(lambda b: b, lambda b: BlenderNaming.parse_bone_name(b.name))

        bone_with_missing_id = Enumerable(bone_ids.items()).first_or_none(lambda p: p[1].local_id is None)
        if bone_with_missing_id is not None:
            raise Exception(f"Bone {bone_with_missing_id[0].name} in armature {bl_armature_obj.name} doesn't have a local ID. " +
                             "Exporting merged skeletons isn't supported - please export an outfit piece skeleton instead.")

        adding_global_bones = True
        prev_local_id = -1
        for bl_edit_bone, bone_id_set in Enumerable(bone_ids.items()).order_by(lambda p: p[1].local_id):
            if bone_id_set.local_id is None:
                raise Exception()

            if bone_id_set.local_id != prev_local_id + 1:
                raise Exception(f"Gap in bone numbering: no bone found with local ID {prev_local_id + 1} in armature {bl_armature_obj.name}. " +
                                 "Please ensure that the local IDs start at 0 and are contiguous.")

            if bone_id_set.global_id is None:
                adding_global_bones = False
            elif not adding_global_bones:
                raise Exception(f"Bone {bl_edit_bone.name} in armature {bl_armature_obj.name} has a global ID even though the one before it doesn't. " +
                                 "The local bone IDs should be numbered so that all the bones with global IDs come first.")

            tr_bone = self.factory.create_bone()
            bl_bone = bl_armature.bones[bl_edit_bone.name]
            self.set_bone_common_fields(tr_bone, bl_edit_bone, bone_id_set, bone_ids)
            self.add_bone_counterpart(tr_bone, bl_bone)
            self.add_bone_constraints(tr_bone, bl_bone)

            tr_skeleton.bones.append(tr_bone)
            prev_local_id = bone_id_set.local_id

    def set_bone_common_fields(self, tr_bone: IBone, bl_edit_bone: bpy.types.EditBone, bone_id_set: BlenderBoneIdSet, bone_ids: dict[bpy.types.EditBone, BlenderBoneIdSet]) -> None:
        if bone_id_set.local_id is None:
            raise Exception()

        tr_bone.global_id = bone_id_set.global_id

        if bl_edit_bone.parent:
            parent_id_set = bone_ids[bl_edit_bone.parent]
            if parent_id_set.local_id is None:
                raise Exception()

            if parent_id_set.local_id >= bone_id_set.local_id:
                raise Exception(f"Parent {bl_edit_bone.parent.name} has a local ID that's higher than that of its child {bl_edit_bone.name}. It should be lower instead.")

            tr_bone.parent_id = parent_id_set.local_id
            tr_bone.relative_location = (bl_edit_bone.head - bl_edit_bone.parent.head) / self.scale_factor

            z_axis = (bl_edit_bone.tail - bl_edit_bone.head).normalized()
            y_axis = cast(Vector, z_axis.cross((z_axis.y, z_axis.z, z_axis.x)))
            x_axis = cast(Vector, z_axis.cross(y_axis))
            tr_bone.absolute_orientation = Matrix(((x_axis.x, y_axis.x, z_axis.x),
                                                    (x_axis.y, y_axis.y, z_axis.y),
                                                    (x_axis.z, y_axis.z, z_axis.z))).to_quaternion()
        else:
            tr_bone.parent_id = -1
            tr_bone.relative_location = bl_edit_bone.head / self.scale_factor
            tr_bone.absolute_orientation = Quaternion()

        tr_bone.distance_from_parent = tr_bone.relative_location.length

    def add_bone_counterpart(self, tr_bone: IBone, bl_bone: bpy.types.Bone) -> None:
        counterpart_bone_name = BoneProperties.get_instance(bl_bone).counterpart_bone_name
        if len(counterpart_bone_name) > 0:
            counterpart_bone_id_set = BlenderNaming.try_parse_bone_name(counterpart_bone_name)
            if counterpart_bone_id_set is None or counterpart_bone_id_set.local_id is None:
                raise Exception(f"Invalid counterpart bone name {counterpart_bone_name} in bone {bl_bone.name}")

            tr_bone.counterpart_local_id = counterpart_bone_id_set.local_id
        else:
            tr_bone.counterpart_local_id = None

    def add_bone_constraints(self, tr_bone: IBone, bl_bone: bpy.types.Bone) -> None:
        tr_bone.constraints = []
        for prop_constraint in BoneProperties.get_instance(bl_bone).constraints:
            tr_constraint = self.factory.deserialize_bone_constraint(prop_constraint.data)
            if tr_constraint is not None:
                tr_bone.constraints.append(tr_constraint)
