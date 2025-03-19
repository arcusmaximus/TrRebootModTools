from typing import TYPE_CHECKING, Annotated, Protocol
import bpy
import os
import re
from bpy.types import Context
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.PermanentSkeletonMerger import PermanentModelMerger
from io_scene_tr_reboot.SkeletonMerger import SkeletonMerger
from io_scene_tr_reboot.TemporarySkeletonMerger import TemporaryModelMerger
from io_scene_tr_reboot.exchange.ClothImporter import ClothImporter
from io_scene_tr_reboot.exchange.CollisionImporter import CollisionImporter
from io_scene_tr_reboot.exchange.HairImporter import HairImporter
from io_scene_tr_reboot.exchange.ModelImporter import ModelImporter
from io_scene_tr_reboot.exchange.SkeletonImporter import SkeletonImporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013HairImporter import Tr2013HairImporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013ModelImporter import Tr2013ModelImporter
from io_scene_tr_reboot.operator.BlenderOperatorBase import ImportOperatorBase, ImportOperatorProperties
from io_scene_tr_reboot.operator.OperatorCommon import OperatorCommon
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import Prop
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy._typing.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class _Properties(ImportOperatorProperties, Protocol):
    import_lods:                    Annotated[bool, Prop("Import LODs")]
    split_into_parts:               Annotated[bool, Prop("Split meshes into parts")]
    merge_with_existing_skeletons:  Annotated[bool, Prop("(SOTTR) Merge with existing skeletons", default = True)]
    keep_original_skeletons:        Annotated[bool, Prop("(SOTTR) Keep original skeletons", default = True)]

class ImportObjectOperator(ImportOperatorBase[_Properties]):
    bl_idname = "import_scene.trobjectref"
    bl_menu_item_name = "Tomb Raider Reboot object (.trXobjectref)"
    filename_ext = ".tr9objectref;.tr10objectref;.tr11objectref"

    def draw(self, context: Context) -> None:
        if self.layout is None:
            return

        self.layout.prop(self.properties, "import_lods")
        self.layout.prop(self.properties, "split_into_parts")
        self.layout.prop(self.properties, "merge_with_existing_skeletons")
        if self.properties.merge_with_existing_skeletons:
            self.layout.prop(self.properties, "keep_original_skeletons")

    def execute(self, context: Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        game = self.get_game_from_file_path(self.properties.filepath)
        if game is None:
            return { "CANCELLED" }

        SceneProperties.set_game(game)

        with OperatorContext.begin(self):
            tr_collection = Factories.get(game).open_collection(self.properties.filepath)

            skeleton_importer = self.create_skeleton_importer(OperatorCommon.scale_factor, game)
            bl_armature_objs = skeleton_importer.import_from_collection(tr_collection)

            model_importer = self.create_model_importer(
                OperatorCommon.scale_factor,
                self.properties.import_lods,
                self.properties.split_into_parts,
                game
            )
            model_importer.import_from_collection(tr_collection, bl_armature_objs)

            bl_armature_obj = Enumerable(bl_armature_objs.values()).first() if len(bl_armature_objs) == 1 else None
            if bl_armature_obj is not None:
                collision_importer = self.create_collision_importer(OperatorCommon.scale_factor, game)
                collision_importer.import_from_collection(tr_collection, bl_armature_obj)

                cloth_importer = self.create_cloth_importer(OperatorCommon.scale_factor, game)
                cloth_importer.import_from_collection(tr_collection, bl_armature_obj)

            hair_importer = self.create_hair_importer(OperatorCommon.scale_factor, game)
            hair_importer.import_from_collection(tr_collection, bl_armature_obj)

            if game == CdcGame.SOTTR and self.properties.merge_with_existing_skeletons and bl_armature_obj is not None:
                self.merge(context)

            BlenderHelper.view_all()
            return { "FINISHED" }

    def merge(self, context: bpy.types.Context) -> None:
        if context.scene is None:
            return

        bl_armature_objs = Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature)).to_list()
        bl_global_armature_obj = Enumerable(bl_armature_objs).first_or_none(lambda o: BlenderNaming.is_global_armature_name(o.name))
        if bl_global_armature_obj is None and len(bl_armature_objs) == 1:
            return

        merger: SkeletonMerger = self.properties.keep_original_skeletons and TemporaryModelMerger() or PermanentModelMerger()
        for bl_armature_obj in bl_armature_objs:
            if bl_armature_obj == bl_global_armature_obj:
                continue

            if Enumerable(bl_armature_obj.children).any(lambda o: not o.data and BlenderNaming.is_local_empty_name(o.name)):
                continue

            bl_global_armature_obj = merger.add(bl_global_armature_obj, bl_armature_obj)

    def get_game_from_file_path(self, file_path: str) -> CdcGame | None:
        match = re.match(r".tr(\d+)objectref", os.path.splitext(file_path)[1])
        if match is None:
            return None

        return CdcGame(int(match.group(1)))

    def create_skeleton_importer(self, scale_factor: float, game: CdcGame) -> SkeletonImporter:
        return SkeletonImporter(scale_factor)

    def create_model_importer(self, scale_factor: float, import_lods: bool, split_into_parts: bool, game: CdcGame) -> ModelImporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013ModelImporter(scale_factor, import_lods, split_into_parts)
            case _:
                return ModelImporter(scale_factor, import_lods, split_into_parts)

    def create_collision_importer(self, scale_factor: float, game: CdcGame) -> CollisionImporter:
        return CollisionImporter(scale_factor)

    def create_cloth_importer(self, scale_factor: float, game: CdcGame) -> ClothImporter:
        return ClothImporter(scale_factor)

    def create_hair_importer(self, scale_factor: float, game: CdcGame) -> HairImporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013HairImporter(scale_factor)
            case _:
                return HairImporter(scale_factor)
