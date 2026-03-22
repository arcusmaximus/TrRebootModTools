from typing import cast
import bpy
from mathutils import Matrix
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.tr.Animation import AnimationBoneInfo
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.tr.IFactory import IFactory
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

class AnimationExchanger(SlotsBase):
    scale_factor: float
    game: CdcGame
    factory: IFactory

    def __init__(self, scale_factor: float, game: CdcGame) -> None:
        self.scale_factor = scale_factor
        self.game = game
        self.factory = Factories.get(game)

    def get_armature_space_rest_matrices(self, bl_armature_obj: bpy.types.Object) -> dict[int, Matrix]:
        matrices: dict[int, Matrix] = {}
        with BlenderHelper.enter_edit_mode(bl_armature_obj):
            for bl_bone in cast(bpy.types.Armature, bl_armature_obj.data).edit_bones:
                global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
                if global_bone_id is None:
                    continue

                matrices[global_bone_id] = bl_bone.matrix

        return matrices

    def get_bone_infos(self, bl_armature_obj: bpy.types.Object, rest_matrices: dict[int, Matrix]) -> dict[int, AnimationBoneInfo]:
        bone_infos: dict[int, AnimationBoneInfo] = {}
        bl_armature = cast(bpy.types.Armature, bl_armature_obj.data)
        for bl_bone in bl_armature.bones:
            global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.name)
            if global_bone_id is None:
                continue

            parent_global_bone_id: int | None = None
            if bl_bone.parent is not None:
                parent_global_bone_id = BlenderNaming.try_get_bone_global_id(bl_bone.parent.name)

            rest_matrix = rest_matrices[global_bone_id].copy()
            rest_matrix.translation /= self.scale_factor
            bone_infos[global_bone_id] = AnimationBoneInfo(rest_matrix, parent_global_bone_id)

        return bone_infos
