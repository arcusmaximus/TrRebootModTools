from typing import NamedTuple, cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderBoneIdSet, BlenderNaming
from io_scene_tr_reboot.SkeletonMerger import SkeletonMerger
from io_scene_tr_reboot.properties.ObjectProperties import ObjectSkeletonProperties
from io_scene_tr_reboot.util.Enumerable import Enumerable

class _BoneRenames(NamedTuple):
    target_renames: dict[str, str]
    source_renames: dict[str, str]
    existing_target_bones_in_source: list[str]

class PermanentModelMerger(SkeletonMerger):
    def add(self, bl_target_armature_obj: bpy.types.Object | None, bl_source_armature_obj: bpy.types.Object) -> bpy.types.Object:
        if bl_target_armature_obj is None:
            return bl_source_armature_obj

        if BlenderNaming.try_parse_global_armature_name(bl_target_armature_obj.name) is not None or \
           BlenderNaming.try_parse_global_armature_name(bl_source_armature_obj.name) is not None:
            raise Exception("Can't merge an armature that has been previously merged with \"Keep original skeletons\" enabled.")

        bone_renames = self.get_bone_renames(bl_target_armature_obj, bl_source_armature_obj)
        self.apply_bone_renames_to_armature_and_children(bl_target_armature_obj, bone_renames.target_renames)
        self.apply_bone_renames_to_armature_and_children(bl_source_armature_obj, bone_renames.source_renames)

        bone_parents = self.get_global_bone_parents(bl_source_armature_obj)
        visible_global_bone_ids = self.get_visible_global_bones(bl_source_armature_obj)
        source_bone_groups = self.get_bone_groups(bl_source_armature_obj)
        target_bone_groups = self.get_bone_groups(bl_target_armature_obj)

        self.move_armature_children(bl_source_armature_obj, bl_target_armature_obj)
        self.remove_bones(bl_source_armature_obj, bone_renames.existing_target_bones_in_source)
        self.add_blend_shape_id_mappings(bl_target_armature_obj, bl_source_armature_obj)
        BlenderHelper.join_objects(bl_target_armature_obj, [bl_source_armature_obj])

        self.apply_global_bone_parents(bl_target_armature_obj, bone_parents)
        self.apply_visible_global_bones(bl_target_armature_obj, visible_global_bone_ids)
        self.apply_bone_groups(bl_target_armature_obj, source_bone_groups)
        self.apply_bone_groups(bl_target_armature_obj, target_bone_groups)

        return bl_target_armature_obj

    def get_bone_renames(self, bl_target_armature_obj: bpy.types.Object, bl_source_armature_obj: bpy.types.Object) -> _BoneRenames:
        target_bone_id_sets = self.get_sorted_bone_id_sets(bl_target_armature_obj)
        source_bone_id_sets = self.get_sorted_bone_id_sets(bl_source_armature_obj)

        target_bone_id_sets_by_global_id = Enumerable(target_bone_id_sets).where(lambda ids: ids.global_id is not None) \
                                                                          .to_dict(lambda ids: ids.global_id)

        target_mappings: dict[str, str] = {}
        source_mappings: dict[str, str] = {}
        existing_target_bones_in_source: list[str] = []

        # Clear out global source bones that already exist in the target
        source_bone_idx = 0
        while source_bone_idx < len(source_bone_id_sets):
            source_bone_id_set = source_bone_id_sets[source_bone_idx]
            if source_bone_id_set.global_id is None:
                break

            target_bone_id_set = target_bone_id_sets_by_global_id.get(source_bone_id_set.global_id)
            if target_bone_id_set is None:
                source_bone_idx += 1
                continue

            source_bone_name = BlenderNaming.make_bone_name(source_bone_id_set)
            target_bone_name = BlenderNaming.make_bone_name(target_bone_id_set)
            if source_bone_name != target_bone_name:
                source_mappings[source_bone_name] = target_bone_name

            existing_target_bones_in_source.append(target_bone_name)
            source_bone_id_sets.pop(source_bone_idx)

        num_target_global_bones = Enumerable(target_bone_id_sets).count(lambda ids: ids.global_id is not None)
        num_target_local_bones = len(target_bone_id_sets) - num_target_global_bones

        num_source_global_bones = source_bone_idx
        num_source_local_bones = len(source_bone_id_sets) - num_source_global_bones

        # Move local target bones to make room for (remaining) global source bones
        self.add_bone_name_mappings(target_mappings, target_bone_id_sets, num_target_global_bones, num_target_local_bones, num_target_global_bones + num_source_global_bones)

        # Move local source bones to the end
        self.add_bone_name_mappings(source_mappings, source_bone_id_sets, num_source_global_bones, num_source_local_bones, num_target_global_bones + num_source_global_bones + num_target_local_bones)

        # Move global source bones into gap created above
        self.add_bone_name_mappings(source_mappings, source_bone_id_sets, 0, num_source_global_bones, num_target_global_bones)

        return _BoneRenames(target_mappings, source_mappings, existing_target_bones_in_source)

    def add_bone_name_mappings(self, mappings: dict[str, str], bone_id_sets: list[BlenderBoneIdSet], from_start_idx: int, count: int, to_start_idx: int) -> None:
        offset = to_start_idx - from_start_idx
        if offset == 0:
            return

        for from_idx in reversed(range(from_start_idx, from_start_idx + count)):
            bone_id_set = bone_id_sets[from_idx]
            if bone_id_set.local_id is None:
                raise Exception(f"Bone with missing local ID encountered")

            from_bone_name = BlenderNaming.make_bone_name(bone_id_set)
            to_bone_name = BlenderNaming.make_bone_name(None, bone_id_set.global_id, from_idx + offset)
            mappings[from_bone_name] = to_bone_name

    def get_sorted_bone_id_sets(self, bl_armature_obj: bpy.types.Object) -> list[BlenderBoneIdSet]:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_id_sets = Enumerable(bl_armature.bones).select(lambda b: BlenderNaming.parse_bone_name(b.name)) \
                                                    .order_by(lambda ids: ids.local_id) \
                                                    .to_list()

        # Sanity check
        in_global_bones = True
        for expected_local_id, bone_id_set in enumerate(bone_id_sets):
            if bone_id_set.local_id != expected_local_id:
                actual_bone_name = BlenderNaming.make_bone_name(bone_id_set)
                expected_bone_name = BlenderNaming.make_bone_name(bone_id_set)
                raise Exception(f"Non-contiguous local bone IDs in armature {bl_armature_obj.name}: expecting bone {actual_bone_name} to be named {expected_bone_name}")

            if in_global_bones:
                if bone_id_set.global_id is None:
                    in_global_bones = False
            else:
                if bone_id_set.global_id is not None:
                    raise Exception(f"Armature {bl_armature_obj.name} contains mixed global and local bones")

        return bone_id_sets

    def remove_bones(self, bl_armature_obj: bpy.types.Object, bone_names: list[str]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for bone_name in bone_names:
                bl_armature.edit_bones.remove(bl_armature.edit_bones[bone_name])

    def add_blend_shape_id_mappings(self, bl_target_armature_obj: bpy.types.Object, bl_source_armature_obj: bpy.types.Object) -> None:
        target_mappings = ObjectSkeletonProperties.get_global_blend_shape_ids(bl_target_armature_obj)
        source_mappings = ObjectSkeletonProperties.get_global_blend_shape_ids(bl_source_armature_obj)

        target_mappings.update(source_mappings)

        ObjectSkeletonProperties.set_global_blend_shape_ids(bl_target_armature_obj, target_mappings)
