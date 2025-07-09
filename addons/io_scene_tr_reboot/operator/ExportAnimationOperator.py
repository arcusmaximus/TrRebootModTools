import os
from typing import TYPE_CHECKING, Annotated, Protocol
import bpy
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.shadow.ShadowAnimationExporter import ShadowAnimationExporter
from io_scene_tr_reboot.operator.BlenderOperatorBase import ExportOperatorBase, ExportOperatorProperties
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import Prop
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class _Properties(ExportOperatorProperties, Protocol):
    apply_lara_bone_fix_constraints: Annotated[bool, Prop("Apply Lara bone fix constraints", default = True)]

class ExportShadowAnimationOperator(ExportOperatorBase[_Properties]):
    bl_idname = "export_scene.tr11anim"
    bl_menu_item_name = "SOTTR animation (.tr11anim)"
    filename_ext = ".tr11anim"

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        if context is None or context.window_manager is None:
            return { "CANCELLED" }

        with OperatorContext.begin(self):
            bl_armature_obj = self.get_source_armature(context)
            if bl_armature_obj is None:
                return { "CANCELLED" }

            animation_id = self.get_animation_id(bl_armature_obj)
            if animation_id is not None:
                folder_path: str
                if self.properties.filepath:
                    folder_path = os.path.split(self.properties.filepath)[0]
                elif context.blend_data is not None and context.blend_data.filepath:
                    folder_path = os.path.split(context.blend_data.filepath)[0]
                else:
                    folder_path = ""

                self.properties.filepath = os.path.join(folder_path, str(animation_id) + self.filename_ext)

            context.window_manager.fileselect_add(self)
            return { "RUNNING_MODAL" }

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        BlenderHelper.switch_to_object_mode()

        with OperatorContext.begin(self):
            bl_armature_obj = self.get_source_armature(context)
            if bl_armature_obj is None:
                return { "CANCELLED" }

            exporter = ShadowAnimationExporter(SceneProperties.get_scale_factor(), self.properties.apply_lara_bone_fix_constraints)
            exporter.export_animation(self.properties.filepath, bl_armature_obj)

            if not OperatorContext.warnings_logged and not OperatorContext.errors_logged:
                OperatorContext.log_info("Animation successfully exported.")

            return { "FINISHED" }

    def get_source_armature(self, context: bpy.types.Context) -> bpy.types.Object | None:
        bl_selected_obj = context.object
        if bl_selected_obj and isinstance(bl_selected_obj.data, bpy.types.Armature):
            return bl_selected_obj

        if bl_selected_obj and bl_selected_obj.parent and isinstance(bl_selected_obj.parent.data, bpy.types.Armature):
            return bl_selected_obj.parent

        if context.scene is None:
            return None

        bl_armature_objs = Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature) and not self.is_in_local_collection(o)).to_list()
        if len(bl_armature_objs) == 0:
            OperatorContext.log_error("No armature found in scene.")
            return None

        if len(bl_armature_objs) > 1:
            OperatorContext.log_error("Please select the armature to export.")
            return None

        return bl_armature_objs[0]

    def is_in_local_collection(self, bl_obj: bpy.types.Object) -> bool:
        return Enumerable(bl_obj.users_collection).any(lambda c: c.name == BlenderNaming.local_collection_name)

    def get_animation_id(self, bl_armature_obj: bpy.types.Object) -> int | None:
        if bl_armature_obj.animation_data and bl_armature_obj.animation_data.action:
            animation_id = BlenderNaming.try_parse_action_name(bl_armature_obj.animation_data.action.name)
            if animation_id is not None:
                return animation_id

        for bl_mesh in Enumerable(bl_armature_obj.children).select(lambda o: o.data).of_type(bpy.types.Mesh):
            if bl_mesh.shape_keys and bl_mesh.shape_keys.animation_data and bl_mesh.shape_keys.animation_data.action:
                animation_id = BlenderNaming.try_parse_action_name(bl_mesh.shape_keys.animation_data.action.name)
                if animation_id is not None:
                    return animation_id

        return None
