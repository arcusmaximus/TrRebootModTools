from typing import TYPE_CHECKING
import bpy
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.AnimationImporter import AnimationImporter
from io_scene_tr_reboot.operator.BlenderOperatorBase import ImportOperatorBase, ImportOperatorProperties
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class ImportShadowAnimationOperator(ImportOperatorBase[ImportOperatorProperties]):
    bl_idname = "import_scene.tranim"
    bl_menu_item_name = "Tomb Raider Reboot animation (.trXanim)"
    filename_ext = ".tr9anim;.tr10anim;.tr11anim"

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        if context is None or context.window_manager is None:
            return { "CANCELLED" }

        with OperatorContext.begin(self):
            bl_armature_obj = self.get_target_armature(context)
            if bl_armature_obj is None:
                return { "CANCELLED" }

            return super().invoke(context, event)

    def execute(self, context: bpy.types.Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        with OperatorContext.begin(self):
            bl_armature_obj = self.get_target_armature(context)
            if bl_armature_obj is None:
                return { "CANCELLED" }

            file_path = self.properties.filepath
            game = Collection.get_game_from_file_path(file_path)
            if game is None:
                return { "CANCELLED" }

            importer = AnimationImporter(SceneProperties.get_scale_factor(), game)
            importer.import_animation(file_path, bl_armature_obj)
            return { "FINISHED" }

    def get_target_armature(self, context: bpy.types.Context) -> bpy.types.Object | None:
        bl_selected_obj = context.object
        if bl_selected_obj is not None and isinstance(bl_selected_obj.data, bpy.types.Armature):
            return bl_selected_obj

        if bl_selected_obj and bl_selected_obj.parent and isinstance(bl_selected_obj.parent.data, bpy.types.Armature):
            return bl_selected_obj.parent

        if context.scene is None:
            return None

        bl_armature_objs = Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature) and not self.is_in_local_collection(o)).to_list()
        if len(bl_armature_objs) == 0:
            OperatorContext.log_error("No armature found in scene. Please import a model first.")
            return None

        if len(bl_armature_objs) > 1:
            OperatorContext.log_error("Please select the target armature.")
            return None

        return bl_armature_objs[0]

    def is_in_local_collection(self, bl_obj: bpy.types.Object) -> bool:
        return Enumerable(bl_obj.users_collection).any(lambda c: c.name == BlenderNaming.local_collection_name)
