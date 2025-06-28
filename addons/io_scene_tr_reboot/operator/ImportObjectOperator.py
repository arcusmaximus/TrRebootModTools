from glob import glob
from typing import TYPE_CHECKING, Annotated, Protocol
import bpy
import os
import re
from mathutils import Matrix
from bpy.types import Context
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.PermanentSkeletonMerger import PermanentModelMerger
from io_scene_tr_reboot.SkeletonMerger import SkeletonMerger
from io_scene_tr_reboot.TemporarySkeletonMerger import TemporaryModelMerger
from io_scene_tr_reboot.exchange.ClothImporter import ClothImporter
from io_scene_tr_reboot.exchange.CollisionModelImporter import CollisionModelImporter
from io_scene_tr_reboot.exchange.CollisionShapeImporter import CollisionShapeImporter
from io_scene_tr_reboot.exchange.HairImporter import HairImporter
from io_scene_tr_reboot.exchange.MaterialImporter import MaterialImporter
from io_scene_tr_reboot.exchange.ModelImporter import ModelImporter
from io_scene_tr_reboot.exchange.SkeletonImporter import SkeletonImporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013HairImporter import Tr2013HairImporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013ModelImporter import Tr2013ModelImporter
from io_scene_tr_reboot.operator.BlenderOperatorBase import ImportOperatorBase, ImportOperatorProperties
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import Prop
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.Factories import Factories
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy._typing.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class _Properties(ImportOperatorProperties, Protocol):
    import_lods:                    Annotated[bool, Prop("Import LODs")]
    import_referenced_objects:      Annotated[bool, Prop("Import referenced objects")]
    split_into_parts:               Annotated[bool, Prop("Split meshes into parts")]
    merge_with_existing_skeletons:  Annotated[bool, Prop("(SOTTR) Merge with existing skeletons", default = True)]
    keep_original_skeletons:        Annotated[bool, Prop("(SOTTR) Keep original skeletons", default = True)]
    scale_factor:                   Annotated[float, Prop("Scale factor", default = SceneProperties.DEFAULT_SCALE_FACTOR, min = SceneProperties.MIN_SCALE_FACTOR)]

class ImportObjectOperator(ImportOperatorBase[_Properties]):
    bl_idname = "import_scene.trobjectref"
    bl_menu_item_name = "Tomb Raider Reboot object (.trXobjectref)"
    filename_ext = ".tr9objectref;*.tr9level;.tr10objectref;.tr10layer;.tr11objectref;.tr11layer;.tr11model;.tr11dtp"

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        self.properties.scale_factor = SceneProperties.get_scale_factor()
        return super().invoke(context, event)

    def draw(self, context: Context) -> None:
        if self.layout is None:
            return

        self.layout.prop(self.properties, "import_lods")
        self.layout.prop(self.properties, "import_referenced_objects")
        self.layout.prop(self.properties, "split_into_parts")
        if not self.properties.import_referenced_objects:
            self.layout.prop(self.properties, "merge_with_existing_skeletons")
            if self.properties.merge_with_existing_skeletons:
                self.layout.prop(self.properties, "keep_original_skeletons")

        self.layout.prop(self.properties, "scale_factor")

    def execute(self, context: Context | None) -> set[OperatorReturnItems]:
        if context is None:
            return { "CANCELLED" }

        if context.object is not None:
            bpy.ops.object.mode_set(mode = "OBJECT")

        game = self.get_game_from_file_path(self.properties.filepath)
        if game is None:
            return { "CANCELLED" }

        SceneProperties.set_game(game)
        SceneProperties.set_scale_factor(self.properties.scale_factor)

        with OperatorContext.begin(self):
            self.import_file(self.properties.filepath, game, context)
            BlenderHelper.view_all()
            return { "FINISHED" }

    def import_file(self, file_path: str, game: CdcGame, context: Context) -> None:
        if bpy.context.scene is None:
            return

        if re.search(r"\.tr\d+model$", file_path) is not None:
            self.import_isolated_model(file_path, game)
            return

        if re.search(r"\.tr\d+dtp$", file_path) is not None:
            self.import_isolated_collision_model(file_path, game)
            return

        if self.properties.import_referenced_objects:
            bl_temp_collection = BlenderHelper.get_or_create_collection("__temp")
            BlenderHelper.set_collection_excluded(bl_temp_collection, True)

            collection_finder = Factories.get(game).create_collection_finder(file_path)
            imported_collection_objs: dict[str, bpy.types.Object] = {}
            self.import_collection_recursive(file_path, None, collection_finder, imported_collection_objs, bl_temp_collection, game, context)
            collection_finder.log_missing_files()

            for bl_collection_obj in imported_collection_objs.values():
                BlenderHelper.move_object_to_collection(bl_collection_obj, None, True)

            bpy.data.collections.remove(bl_temp_collection)
        else:
            self.import_collection(file_path, None, None, game, context)

        CollisionModelImporter.assign_material_colors()

    def import_isolated_collision_model(self, file_path: str, game: CdcGame) -> None:
        model_resource = Collection.try_parse_resource_file_path(file_path, game)
        if model_resource is None:
            return

        model_folder_path = os.path.dirname(file_path)
        if os.path.basename(model_folder_path) != "Dtp":
            return

        drm_folder_path = os.path.dirname(model_folder_path)
        if not drm_folder_path.endswith(".drm"):
            return

        collection_name = os.path.splitext(os.path.basename(drm_folder_path))[0]
        collection_file_paths = glob(os.path.join(drm_folder_path, collection_name + ".tr*"))
        if len(collection_file_paths) == 0:
            collection_file_paths = glob(os.path.join(drm_folder_path, "*.tr*layer"))
            if len(collection_file_paths) == 0:
                return

        tr_collection = Factories.get(game).open_collection(collection_file_paths[0])
        tr_instance = Enumerable(tr_collection.get_model_instances()).first_or_none(lambda i: i.collision_model_resource == model_resource)
        if tr_instance is None:
            return

        tr_instance = Collection.ModelInstance(
            tr_instance.skeleton_resource,
            tr_instance.model_resource,
            tr_instance.collision_model_resource,
            Matrix.Identity(4)
        )

        collection_empty_name = BlenderNaming.make_collection_empty_name(tr_collection.name, tr_collection.id)
        bl_collection_obj = BlenderHelper.create_object(None, collection_empty_name)

        importer = self.create_collision_model_importer(self.properties.scale_factor, None, game)
        importer.import_model_instance(tr_collection, tr_instance, bl_collection_obj)
        return

    def import_isolated_model(self, file_path: str, game: CdcGame) -> None:
        model_resource = Collection.try_parse_resource_file_path(file_path, game)
        if model_resource is None:
            return

        model_folder_path = os.path.dirname(file_path)
        if os.path.basename(model_folder_path) != "Model":
            return

        drm_folder_path = os.path.dirname(model_folder_path)
        if not drm_folder_path.endswith(".drm"):
            return

        collection_name = os.path.splitext(os.path.basename(drm_folder_path))[0]
        collection_file_paths = glob(os.path.join(drm_folder_path, collection_name + ".tr*"))
        if len(collection_file_paths) == 0:
            collection_file_paths = glob(os.path.join(drm_folder_path, "*.tr*layer"))
            if len(collection_file_paths) == 0:
                return

        tr_collection = Factories.get(game).open_collection(collection_file_paths[0])

        material_importer = MaterialImporter()
        material_importer.import_from_collection(tr_collection)

        model_importer = self.create_model_importer(
            self.properties.scale_factor,
            self.properties.import_lods,
            self.properties.split_into_parts,
            None,
            game
        )
        tr_instance = Enumerable(tr_collection.get_model_instances()).first_or_none(lambda i: i.model_resource == model_resource)
        if tr_instance is not None:
            collection_empty_name = BlenderNaming.make_collection_empty_name(tr_collection.name, tr_collection.id)
            bl_collection_obj = BlenderHelper.create_object(None, collection_empty_name)
            tr_instance = Collection.ModelInstance(
                tr_instance.skeleton_resource,
                tr_instance.model_resource,
                tr_instance.collision_model_resource,
                Matrix.Identity(4)
            )
            model_importer.import_model_instance(tr_collection, tr_instance, bl_collection_obj, {})
        else:
            tr_model = tr_collection.get_model(model_resource)
            if tr_model is not None:
                model_importer.import_model(tr_collection, tr_model, None)

    def import_collection_recursive(
        self,
        file_path: str,
        tr_parent_collection: Collection | None,
        collection_finder: CollectionFinder,
        imported_collection_objs: dict[str, bpy.types.Object],
        bl_target_collection: bpy.types.Collection | None,
        game: CdcGame,
        context: Context
    ) -> bpy.types.Object:
        bl_collection_obj = imported_collection_objs.get(file_path)
        if bl_collection_obj is not None:
            bl_collection_obj = BlenderHelper.duplicate_object(bl_collection_obj, False, True)
            return bl_collection_obj

        (tr_collection, bl_collection_obj) = self.import_collection(file_path, tr_parent_collection, bl_target_collection, game, context)
        imported_collection_objs[file_path] = bl_collection_obj

        for child_instance in tr_collection.get_collection_instances():
            child_file_path = collection_finder.find_root_file(child_instance.id_or_name)
            if child_file_path is None:
                continue

            bl_child_collection_obj = self.import_collection_recursive(child_file_path, tr_collection, collection_finder, imported_collection_objs, bl_target_collection, game, context)
            child_transform = child_instance.transform.copy()
            child_transform.translation = child_transform.translation * self.properties.scale_factor
            bl_child_collection_obj.matrix_local = child_transform
            bl_child_collection_obj.parent = bl_collection_obj

        return bl_collection_obj

    def import_collection(
        self,
        file_path: str,
        tr_parent_collection: Collection | None,
        bl_target_collection: bpy.types.Collection | None,
        game: CdcGame,
        context: Context
    ) -> tuple[Collection, bpy.types.Object]:
        tr_collection = Factories.get(game).open_collection(file_path, tr_parent_collection)
        scale_factor = self.properties.scale_factor

        material_importer = self.create_material_importer()
        material_importer.import_from_collection(tr_collection)

        skeleton_importer = self.create_skeleton_importer(scale_factor, bl_target_collection, game)
        bl_armature_objs = skeleton_importer.import_from_collection(tr_collection)

        bl_collection_obj: bpy.types.Object
        if len(bl_armature_objs) == 1:
            bl_collection_obj = Enumerable(bl_armature_objs.values()).first()
        else:
            collection_empty_name = BlenderNaming.make_collection_empty_name(tr_collection.name, tr_collection.id)
            bl_collection_obj = BlenderHelper.create_object(None, collection_empty_name)
            bl_collection_obj.hide_set(True)
            for bl_armature_obj in bl_armature_objs.values():
                bl_armature_obj.parent = bl_collection_obj

        model_importer = self.create_model_importer(
            scale_factor,
            self.properties.import_lods,
            self.properties.split_into_parts,
            bl_target_collection,
            game
        )
        model_importer.import_from_collection(tr_collection, bl_collection_obj, bl_armature_objs)

        collision_model_importer = self.create_collision_model_importer(scale_factor, bl_target_collection, game)
        collision_model_importer.import_from_collection(tr_collection, bl_collection_obj)

        bl_armature_obj = Enumerable(bl_armature_objs.values()).first_or_none(lambda o: Enumerable(o.children).any())
        if bl_armature_obj is not None:
            collision_shape_importer = self.create_collision_shape_importer(scale_factor, bl_target_collection, game)
            collision_shape_importer.import_from_collection(tr_collection, bl_armature_obj)

            cloth_importer = self.create_cloth_importer(scale_factor, bl_target_collection, game)
            cloth_importer.import_from_collection(tr_collection, bl_armature_obj)

        hair_importer = self.create_hair_importer(scale_factor, bl_target_collection, game)
        hair_importer.import_from_collection(tr_collection, bl_collection_obj, bl_armature_objs)

        if game == CdcGame.SOTTR and \
           self.properties.merge_with_existing_skeletons and \
           not self.properties.import_referenced_objects and \
           bl_armature_obj is not None:
            self.merge(context)

        return (tr_collection, bl_collection_obj)

    def merge(self, context: bpy.types.Context) -> None:
        if context.scene is None:
            return

        bl_armature_objs = Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature)).to_list()
        bl_global_armature_obj = Enumerable(bl_armature_objs).first_or_none(lambda o: BlenderNaming.try_parse_global_armature_name(o.name) is not None)
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
        match = re.match(r".tr(\d+)", os.path.splitext(file_path)[1])
        if match is None:
            return None

        return CdcGame(int(match.group(1)))

    def create_material_importer(self) -> MaterialImporter:
        return MaterialImporter()

    def create_skeleton_importer(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> SkeletonImporter:
        return SkeletonImporter(scale_factor, bl_target_collection)

    def create_model_importer(self, scale_factor: float, import_lods: bool, split_into_parts: bool, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> ModelImporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013ModelImporter(scale_factor, import_lods, split_into_parts, bl_target_collection)
            case _:
                return ModelImporter(scale_factor, import_lods, split_into_parts, bl_target_collection)

    def create_collision_model_importer(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> CollisionModelImporter:
        return CollisionModelImporter(scale_factor, bl_target_collection)

    def create_collision_shape_importer(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> CollisionShapeImporter:
        return CollisionShapeImporter(scale_factor, bl_target_collection)

    def create_cloth_importer(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> ClothImporter:
        return ClothImporter(scale_factor, bl_target_collection)

    def create_hair_importer(self, scale_factor: float, bl_target_collection: bpy.types.Collection | None, game: CdcGame) -> HairImporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013HairImporter(scale_factor, bl_target_collection)
            case _:
                return HairImporter(scale_factor, bl_target_collection)
