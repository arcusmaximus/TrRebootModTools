from typing import cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderBoneGroupSet, BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.properties.BoneProperties import BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectProperties
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.util.Enumerable import Enumerable

class SkeletonMerger:
    factory: IFactory

    def __init__(self, game: CdcGame) -> None:
        self.factory = Factories.get(game)

    def add(self, bl_target_armature_obj: bpy.types.Object | None, bl_source_armature_obj: bpy.types.Object, /) -> bpy.types.Object:
        ...

    def get_global_bone_parents(self, bl_armature_obj: bpy.types.Object) -> dict[int, int]:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_parents: dict[int, int] = {}
        for bl_bone in bl_armature.bones:
            if bl_bone.parent is None:
                continue

            child_bone_id_set  = BlenderNaming.try_parse_bone_name(bl_bone.name)
            parent_bone_id_set = BlenderNaming.try_parse_bone_name(bl_bone.parent.name)
            if child_bone_id_set is None or child_bone_id_set.global_id is None or \
               parent_bone_id_set is None or parent_bone_id_set.global_id is None:
                continue

            bone_parents[child_bone_id_set.global_id] = parent_bone_id_set.global_id

        return bone_parents

    def apply_global_bone_parents(self, bl_armature_obj: bpy.types.Object, bone_parents: dict[int, int]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_names_by_global_id = self.get_bone_names_by_global_id(bl_armature_obj)
        with BlenderHelper.enter_edit_mode():
            for child_bone_id, parent_bone_id in bone_parents.items():
                bl_child_bone  = bl_armature.edit_bones[bone_names_by_global_id[child_bone_id]]
                bl_parent_bone = bl_armature.edit_bones[bone_names_by_global_id[parent_bone_id]]
                bl_child_bone.parent = bl_parent_bone

    def get_bone_groups(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str] | None = None) -> dict[str, BlenderBoneGroupSet]:
        result: dict[str, BlenderBoneGroupSet] = {}
        for bl_bone in cast(bpy.types.Armature, bl_armature_obj.data).bones:
            bone_name = bl_bone.name
            if renames is not None:
                bone_name = renames.get(bone_name) or bone_name

            result[bone_name] = BlenderHelper.get_bone_groups(bl_bone)

        return result

    def apply_bone_groups(self, bl_armature_obj: bpy.types.Object, bone_groups: dict[str, BlenderBoneGroupSet]):
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        for bone_name, bone_group_set in bone_groups.items():
            BlenderHelper.set_bone_groups(bl_armature_obj, bl_armature.bones[bone_name], bone_group_set)

    def intersect_non_deforming_bones(self, source_bone_groups: dict[str, BlenderBoneGroupSet], target_bone_groups: dict[str, BlenderBoneGroupSet]) -> None:
        group_name = BlenderNaming.non_deforming_bone_group_name
        for bone_name in Enumerable(source_bone_groups.keys()).intersect(target_bone_groups.keys()):
            in_source = group_name in source_bone_groups[bone_name].group_names
            in_target = group_name in target_bone_groups[bone_name].group_names
            if not (in_source and in_target):
                if in_source:
                    source_bone_groups[bone_name].group_names.remove(group_name)

                if in_target:
                    target_bone_groups[bone_name].group_names.remove(group_name)

    def get_bone_names_by_global_id(self, bl_armature_obj: bpy.types.Object) -> dict[int, str]:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        bone_names_by_global_id: dict[int, str] = {}
        for bl_bone in bl_armature.bones:
            bone_id_set = BlenderNaming.try_parse_bone_name(bl_bone.name)
            if bone_id_set is not None and bone_id_set.global_id is not None:
                bone_names_by_global_id[bone_id_set.global_id] = bl_bone.name

        return bone_names_by_global_id

    def apply_bone_renames_to_armature_and_children(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str]) -> None:
        self.apply_bone_renames_to_armature(bl_armature_obj, renames)
        self.apply_bone_renames_to_cloth_strips(bl_armature_obj, renames)

    def apply_bone_renames_to_armature(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str]) -> None:
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)

        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for from_bone_name, to_bone_name in renames.items():
                bl_bone = bl_armature.edit_bones.get(from_bone_name)
                if bl_bone is not None:
                    bl_bone.name = to_bone_name

        for bl_bone in bl_armature.bones:
            bone_properties = BoneProperties.get_instance(bl_bone)
            new_counterpart_bone_name = renames.get(bone_properties.counterpart_bone_name)
            if new_counterpart_bone_name is not None:
                bone_properties.counterpart_bone_name = new_counterpart_bone_name

        self.apply_bone_renames_to_armature_drivers(bl_armature_obj, renames)
        self.apply_bone_renames_to_armature_constraints(bl_armature_obj, renames)

    def apply_bone_renames_to_armature_drivers(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str]) -> None:
        if bl_armature_obj.animation_data is None:
            return

        for bl_curve in bl_armature_obj.animation_data.drivers:
            bl_driver = bl_curve.driver
            if bl_driver is None:
                continue

            for bl_variable in bl_driver.variables:
                from_bone_name = bl_variable.targets[0].bone_target
                if from_bone_name == "":
                    continue

                to_bone_name = renames.get(from_bone_name)
                if to_bone_name is None:
                    continue

                bl_variable.targets[0].bone_target = to_bone_name

    def apply_bone_renames_to_armature_constraints(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str]) -> None:
        if bl_armature_obj.pose is None:
            return

        local_id_changes: dict[int, int] = {}
        for old_name, new_name in renames.items():
            old_id_set = BlenderNaming.parse_bone_name(old_name)
            new_id_set = BlenderNaming.parse_bone_name(new_name)
            if old_id_set.local_id is not None and new_id_set.local_id is not None and old_id_set.local_id != new_id_set.local_id:
                local_id_changes[old_id_set.local_id] = new_id_set.local_id

        for bl_bone in bl_armature_obj.pose.bones:
            for bl_constraint in bl_bone.constraints:
                if isinstance(bl_constraint, bpy.types.CopyLocationConstraint) or isinstance(bl_constraint, bpy.types.CopyRotationConstraint):
                    from_bone_name = bl_constraint.subtarget
                    if from_bone_name == "":
                        continue

                    to_bone_name = renames.get(from_bone_name)
                    if to_bone_name is None:
                        continue

                    bl_constraint.subtarget = to_bone_name

            if len(local_id_changes) == 0:
                continue

            bone_properties = BoneProperties.get_instance(bl_bone.bone)
            for prop_constraint in bone_properties.constraints:
                tr_constraint = self.factory.deserialize_bone_constraint(prop_constraint.data)
                if tr_constraint is None:
                    continue

                tr_constraint.apply_bone_local_id_changes(local_id_changes)
                prop_constraint.data = tr_constraint.serialize()

    def apply_bone_renames_to_vertex_groups(self, bl_armature_obj: bpy.types.Object, mappings: dict[str, str]) -> None:
        for bl_mesh_obj in Enumerable(bl_armature_obj.children_recursive).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            for from_bone_name, to_bone_name in mappings.items():
                bl_vertex_group = bl_mesh_obj.vertex_groups.get(from_bone_name)
                if bl_vertex_group is None:
                    continue

                bl_vertex_group.name = to_bone_name

    def apply_bone_renames_to_cloth_strips(self, bl_armature_obj: bpy.types.Object, renames: dict[str, str]) -> None:
        bl_cloth_empty = Enumerable(bl_armature_obj.children).first_or_none(lambda o: not o.data and BlenderNaming.is_cloth_empty_name(o.name))
        if bl_cloth_empty is None:
            return

        for bl_cloth_strip_obj in Enumerable(bl_cloth_empty.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            cloth_properties = ObjectProperties.get_instance(bl_cloth_strip_obj).cloth
            if not cloth_properties.parent_bone_name:
                continue

            new_parent_bone_name = renames.get(cloth_properties.parent_bone_name)
            if not new_parent_bone_name:
                continue

            cloth_properties.parent_bone_name = new_parent_bone_name

    def move_armature_children(self, bl_source_armature_obj: bpy.types.Object, bl_target_armature_obj: bpy.types.Object) -> None:
        for bl_mesh_obj in Enumerable(bl_source_armature_obj.children_recursive).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            bl_armature_modifier = Enumerable(bl_mesh_obj.modifiers).of_type(bpy.types.ArmatureModifier).first_or_none()
            if bl_armature_modifier is not None and bl_armature_modifier.object == bl_source_armature_obj:
                bl_armature_modifier.object = bl_target_armature_obj

        for bl_obj in Enumerable(bl_source_armature_obj.children):
            bl_obj.parent = bl_target_armature_obj
