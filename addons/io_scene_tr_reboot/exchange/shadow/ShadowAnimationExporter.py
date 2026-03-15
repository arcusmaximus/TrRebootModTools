from typing import cast
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.AnimationExporter import AnimationBoneConstraintParams, AnimationExporter
from io_scene_tr_reboot.tr.Animation import Animation
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.shadow.ShadowAnimation import ShadowAnimation
from io_scene_tr_reboot.util.Enumerable import Enumerable

class ShadowAnimationExporter(AnimationExporter):
    def __init__(self, scale_factor: float, apply_lara_bone_fix_constraints: bool) -> None:
        super().__init__(scale_factor, apply_lara_bone_fix_constraints, CdcGame.SOTTR)

    def export_armature_animation(self, tr_animation: Animation, bl_armature_obj: bpy.types.Object) -> None:
        super().export_armature_animation(tr_animation, bl_armature_obj)

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

        cast(ShadowAnimation, tr_animation).bone_distances_from_parent = Enumerable(tr_animation.bone_tracks.keys()).select(lambda id: bone_distances_from_parent[id]).to_list()

    def get_bone_fix_constraints(self) -> tuple[list[AnimationBoneConstraintParams], str]:
        constraints = [
            # Arms
            AnimationBoneConstraintParams(105, 97, False),
            AnimationBoneConstraintParams(123, 97, False),
            AnimationBoneConstraintParams(106, 102, False),
            AnimationBoneConstraintParams(124, 102, False),

            # Legs
            AnimationBoneConstraintParams(120, 111, True),
            AnimationBoneConstraintParams(121, 115, True)
        ]
        return (constraints, "tr11_lara.drm")
