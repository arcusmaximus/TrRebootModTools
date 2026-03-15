import os
import shutil
from typing import TYPE_CHECKING, Annotated, Protocol, Sequence
import bpy
from bpy.types import Context, Event
from io_scene_tr_reboot.BlenderHelper import BlenderHelper
from io_scene_tr_reboot.BlenderNaming import BlenderCollisionMeshIdSet, BlenderCollisionModelIdSet, BlenderHairStrandGroupIdSet, BlenderMeshIdSet, BlenderModelIdSet, BlenderNaming
from io_scene_tr_reboot.exchange.ClothExporter import ClothExporter
from io_scene_tr_reboot.exchange.CollisionModelExporter import CollisionModelExporter
from io_scene_tr_reboot.exchange.HairExporter import HairExporter
from io_scene_tr_reboot.exchange.ModelExporter import ModelExporter
from io_scene_tr_reboot.ModelSplitter import ModelSplitter
from io_scene_tr_reboot.exchange.SkeletonExporter import SkeletonExporter
from io_scene_tr_reboot.exchange.shadow.ShadowModelExporter import ShadowModelExporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013ClothExporter import Tr2013ClothExporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013ModelExporter import Tr2013ModelExporter
from io_scene_tr_reboot.exchange.tr2013.Tr2013SkeletonExporter import Tr2013SkeletonExporter
from io_scene_tr_reboot.operator.BlenderOperatorBase import ExportOperatorBase, ExportOperatorProperties
from io_scene_tr_reboot.operator.HairWeightPaintingOperator import HairWeightPaintingOperator
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.properties.BlenderPropertyGroup import Prop
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class _Properties(ExportOperatorProperties, Protocol):
    export_skeleton: Annotated[bool, Prop("Export skeleton")]
    export_cloth: Annotated[bool, Prop("Export cloth")]
    export_all_models_of_collection: Annotated[bool, Prop("Export all models of DRM")]
    export_all_tr2013_lara_duplicates: Annotated[bool, Prop("Export all Lara duplicates")]

class ExportModelOperator(ExportOperatorBase[_Properties]):
    bl_idname = "export_scene.trmodel"
    bl_menu_item_name = "Tomb Raider Reboot model (.trXmodeldata)"
    filename_ext = ""

    def invoke(self, context: Context | None, event: Event) -> set[OperatorReturnItems]:
        if context is None or context.window_manager is None:
            return { "CANCELLED" }

        game = SceneProperties.get_game()

        with OperatorContext.begin(self):
            bl_mesh_objs = self.get_mesh_objects_to_export(context, False)
            bl_collision_mesh_objs = self.get_collision_mesh_objects_to_export(context)
            bl_hair_objs = self.get_hair_strand_group_objects_to_export(context, False)
            if len(bl_mesh_objs) == 0 and len(bl_collision_mesh_objs) == 0 and len(bl_hair_objs) == 0:
                OperatorContext.log_error("Nothing selected to export.")
                return { "CANCELLED" }

            folder_path: str
            if self.properties.filepath:
                folder_path = os.path.split(self.properties.filepath)[0]
            elif context.blend_data is not None and context.blend_data.filepath:
                folder_path = os.path.split(context.blend_data.filepath)[0]
            else:
                folder_path = ""

            export_id: int
            if len(bl_mesh_objs) > 0:
                export_id = BlenderNaming.parse_model_name(Enumerable(bl_mesh_objs).first().name).model_data_id
                ExportModelOperator.filename_ext = f".tr{game}modeldata"
            elif len(bl_collision_mesh_objs) > 0:
                export_id = BlenderNaming.parse_collision_model_name(Enumerable(bl_collision_mesh_objs).first().name).model_id
                ExportModelOperator.filename_ext = f".tr{game}dtp"
            else:
                export_ids = BlenderNaming.parse_hair_strand_group_name(Enumerable(bl_hair_objs).first().name)
                export_id = export_ids.hair_data_id
                ExportModelOperator.filename_ext = f".tr{game}modeldata" if export_ids.model_id is not None else f".tr{game}dtp"

            self.properties.filepath = os.path.join(folder_path, str(export_id) + ExportModelOperator.filename_ext)
            self.properties.filter_glob = "*" + ExportModelOperator.filename_ext
            context.window_manager.fileselect_add(self)
            return { "RUNNING_MODAL" }

    def draw(self, context: Context) -> None:
        if self.layout is None:
            return

        game = SceneProperties.get_game()

        if Enumerable(context.selected_objects).any(lambda o: self.is_armature(self.get_collection_wrapper_object(o))):
            if not self.requires_skeleton_export(game):
                self.layout.prop(self.properties, "export_skeleton")

            if not self.requires_cloth_export(game):
                self.layout.prop(self.properties, "export_cloth")

        if Enumerable(context.selected_objects).any(lambda o: self.is_static_mesh_collection(self.get_collection_wrapper_object(o))):
            self.layout.prop(self.properties, "export_all_models_of_collection")

        if game == CdcGame.TR2013:
            self.layout.prop(self.properties, "export_all_tr2013_lara_duplicates")

    def execute(self, context: Context | None) -> set[OperatorReturnItems]:
        if context is None or context.view_layer is None:
            return { "CANCELLED" }

        BlenderHelper.switch_to_object_mode()

        game = SceneProperties.get_game()

        with OperatorContext.begin(self):
            bl_local_collection = context.view_layer.layer_collection.children.get(BlenderNaming.local_collection_name)
            was_local_collection_excluded = False
            if bl_local_collection is not None:
                was_local_collection_excluded = bl_local_collection.exclude
                bl_local_collection.exclude = False

            folder_path = os.path.split(self.properties.filepath)[0]
            bl_hair_strand_group_objs = self.get_hair_strand_group_objects_to_export(context, True)
            bl_collision_mesh_objs = self.get_collision_mesh_objects_to_export(context)
            bl_unsplit_mesh_objs = self.get_mesh_objects_to_export(context, False)
            bl_split_mesh_objs = self.get_mesh_objects_to_export(context, True)

            self.export(context, folder_path, bl_unsplit_mesh_objs, bl_split_mesh_objs, bl_collision_mesh_objs, bl_hair_strand_group_objs, game)

            if bl_local_collection is not None:
                bl_local_collection.exclude = was_local_collection_excluded

            if game == CdcGame.TR2013:
                if self.properties.export_all_tr2013_lara_duplicates:
                    self.create_tr2013_lara_duplicates(folder_path)

                self.create_tr2013_model_dtp_duplicates(folder_path)

            if not OperatorContext.warnings_logged and not OperatorContext.errors_logged:
                OperatorContext.log_info("Model successfully exported.")

            return { "FINISHED" }

    def export(
        self,
        context: bpy.types.Context,
        folder_path: str,
        bl_unsplit_mesh_objs: list[bpy.types.Object],
        bl_split_mesh_objs: list[bpy.types.Object],
        bl_collision_mesh_objs: list[bpy.types.Object],
        bl_hair_strand_group_objs: list[bpy.types.Object],
        game: CdcGame
    ):
        scale_factor = SceneProperties.get_scale_factor()

        model_exporter = self.create_model_exporter(scale_factor, game)
        for model_id_set, bl_mesh_objs_of_model in Enumerable(bl_split_mesh_objs).group_by(lambda o: BlenderNaming.parse_model_name(o.name)).items():
            bl_armature_obj = bl_mesh_objs_of_model[0].parent
            if bl_armature_obj is not None and not isinstance(bl_armature_obj.data, bpy.types.Armature):
                bl_armature_obj = None

            model_exporter.export_model(folder_path, model_id_set, bl_mesh_objs_of_model, bl_armature_obj)

        collision_model_exporter = self.create_collision_model_exporter(scale_factor, game)
        for model_id_set, bl_mesh_objs_of_model in Enumerable(bl_collision_mesh_objs).group_by(lambda o: BlenderNaming.parse_collision_model_name(o.name)).items():
            collision_model_exporter.export_model(folder_path, model_id_set, bl_mesh_objs_of_model)

        hair_exporter = self.create_hair_exporter(scale_factor, game)
        for bl_strand_group_objs_of_hair in Enumerable(bl_hair_strand_group_objs).group_by(lambda o: BlenderNaming.parse_hair_strand_group_name(o.name).hair_data_id).values():
            hair_exporter.export_hair(folder_path, bl_strand_group_objs_of_hair)

        if self.properties.export_cloth or self.requires_cloth_export(game):
            for bl_armature_obj in Enumerable(bl_unsplit_mesh_objs).concat(bl_hair_strand_group_objs)           \
                                                                   .select(self.get_collection_wrapper_object)  \
                                                                   .of_type(bpy.types.Object)                   \
                                                                   .distinct()                                  \
                                                                   .where(lambda o: isinstance(o.data, bpy.types.Armature)):
                cloth_exporter = self.create_cloth_exporter(scale_factor, game)
                cloth_exporter.export_cloths(folder_path, bl_armature_obj, self.get_local_armatures(context))

        if self.properties.export_skeleton or self.requires_skeleton_export(game):
            skeleton_exporter = self.create_skeleton_exporter(scale_factor, game)
            for bl_armature_obj in Enumerable(bl_split_mesh_objs).concat(bl_hair_strand_group_objs)                         \
                                                                 .select(lambda o: o.parent)                                \
                                                                 .of_type(bpy.types.Object)                                 \
                                                                 .where(lambda o: isinstance(o.data, bpy.types.Armature))   \
                                                                 .distinct():
                skeleton_exporter.export(folder_path, bl_armature_obj)

    def get_mesh_objects_to_export(self, context: bpy.types.Context, split_global_meshes: bool) -> list[bpy.types.Object]:
        if context.scene is None:
            return []

        mesh_obj_names = dict[BlenderMeshIdSet, str]()
        for selected_object_name in Enumerable(context.selected_objects).where(lambda o: not self.is_in_local_collection(o) and not self.is_collision_mesh(o)).select(lambda o: o.name).to_list():
            bl_collection_obj = self.get_collection_wrapper_object(bpy.data.objects[selected_object_name])
            if bl_collection_obj is None:
                continue

            bl_parent_objs: list[bpy.types.Object] = [bl_collection_obj]
            if split_global_meshes and self.is_global_armature(bl_collection_obj):
                bl_parent_objs = ModelSplitter().split(bl_collection_obj)

            only_model_id_set: BlenderModelIdSet | None = None
            if self.is_static_mesh_collection(bl_collection_obj) and not self.properties.export_all_models_of_collection:
                only_model_id_set = BlenderNaming.try_parse_model_name(selected_object_name)

            for bl_mesh_obj in Enumerable(bl_parent_objs).select_many(lambda o: o.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
                mesh_id_set = BlenderNaming.try_parse_mesh_name(bl_mesh_obj.name)
                if mesh_id_set is None or mesh_id_set in mesh_obj_names:
                    continue

                if only_model_id_set is not None and BlenderModelIdSet(mesh_id_set.object_id, mesh_id_set.model_id, mesh_id_set.model_data_id) != only_model_id_set:
                    continue

                mesh_obj_names[mesh_id_set] = bl_mesh_obj.name

        bl_mesh_objs = Enumerable(mesh_obj_names.values()).select(lambda n: bpy.data.objects[n]).to_list()
        return bl_mesh_objs

    def get_collision_mesh_objects_to_export(self, context: bpy.types.Context) -> list[bpy.types.Object]:
        bl_mesh_objs = dict[BlenderCollisionMeshIdSet, bpy.types.Object]()

        for bl_selected_obj in Enumerable(context.selected_objects).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            selected_model_id_set = BlenderNaming.try_parse_collision_model_name(bl_selected_obj.name)
            if selected_model_id_set is None:
                continue

            bl_collection_obj = self.get_collection_wrapper_object(bl_selected_obj)
            if bl_collection_obj is None:
                continue

            bl_collision_empty = Enumerable(bl_collection_obj.children).first_or_none(lambda o: o.data is None and BlenderNaming.is_collision_empty_name(o.name))
            if bl_collision_empty is None:
                continue

            for bl_mesh_obj in Enumerable(bl_collision_empty.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
                mesh_id_set = BlenderNaming.try_parse_collision_mesh_name(bl_mesh_obj.name)
                if mesh_id_set is None or mesh_id_set in bl_mesh_objs:
                    continue

                if not self.properties.export_all_models_of_collection and BlenderCollisionModelIdSet(mesh_id_set.object_id, mesh_id_set.model_id) != selected_model_id_set:
                    continue

                bl_mesh_objs[mesh_id_set] = bl_mesh_obj

        return list(bl_mesh_objs.values())

    def get_hair_strand_group_objects_to_export(self, context: bpy.types.Context, exit_weight_paint_mode: bool) -> list[bpy.types.Object]:
        hair_obj_name_dict = dict[BlenderHairStrandGroupIdSet, str]()
        for bl_obj in Enumerable(context.selected_objects):
            bl_collection_obj = self.get_collection_wrapper_object(bl_obj)
            if bl_collection_obj is None:
                continue

            for bl_child_obj in bl_collection_obj.children_recursive:
                hair_id_set = BlenderNaming.try_parse_hair_strand_group_name(bl_child_obj.name)
                if hair_id_set is None or hair_id_set in hair_obj_name_dict:
                    continue

                hair_obj_name_dict[hair_id_set] = bl_child_obj.name

        hair_obj_names = list(hair_obj_name_dict.values())
        for i in range(len(hair_obj_names) - 1, -1, -1):
            bl_obj = bpy.data.objects[hair_obj_names[i]]
            if isinstance(bl_obj.data, bpy.types.Curves):
                continue

            if isinstance(bl_obj.data, bpy.types.GreasePencil):
                if exit_weight_paint_mode:
                    BlenderHelper.select_object(bl_obj)
                    HairWeightPaintingOperator.static_execute(False)

                continue

            hair_obj_names.pop(i)

        return Enumerable(hair_obj_names).select(lambda n: bpy.data.objects[n]).to_list()

    def get_collection_wrapper_object(self, bl_obj: bpy.types.Object | None) -> bpy.types.Object | None:
        while bl_obj is not None:
            if self.is_collection_empty(bl_obj) or self.is_global_armature(bl_obj) or self.is_local_armature(bl_obj):
                return bl_obj

            bl_obj = bl_obj.parent

        return None

    def is_static_mesh_collection(self, bl_collection_obj: bpy.types.Object | None) -> bool:
        return bl_collection_obj is not None and bl_collection_obj.data is None and Enumerable(bl_collection_obj.children).any(self.is_mesh)

    def is_collection_empty(self, bl_obj: bpy.types.Object) -> bool:
        return bl_obj.data is None and BlenderNaming.try_parse_collection_empty_name(bl_obj.name) is not None

    def is_armature(self, bl_obj: bpy.types.Object | None) -> bool:
        return bl_obj is not None and (self.is_global_armature(bl_obj) or self.is_local_armature(bl_obj))

    def is_global_armature(self, bl_obj: bpy.types.Object) -> bool:
        return isinstance(bl_obj.data, bpy.types.Armature) and BlenderNaming.try_parse_global_armature_name(bl_obj.data.name) is not None

    def is_local_armature(self, bl_obj: bpy.types.Object) -> bool:
        return isinstance(bl_obj.data, bpy.types.Armature) and BlenderNaming.try_parse_local_armature_name(bl_obj.data.name) is not None

    def get_local_armatures(self, context: bpy.types.Context) -> dict[int, bpy.types.Object]:
        if context.scene is None:
            return {}

        return Enumerable(context.scene.objects).where(lambda o: isinstance(o.data, bpy.types.Armature) and self.is_in_local_collection(o)) \
                                                .to_dict(lambda o: BlenderNaming.parse_local_armature_name(o.name))

    def is_in_local_collection(self, bl_obj: bpy.types.Object) -> bool:
        return Enumerable(bl_obj.users_collection).any(lambda c: c.name == BlenderNaming.local_collection_name)

    def is_mesh(self, bl_obj: bpy.types.Object) -> bool:
        return isinstance(bl_obj.data, bpy.types.Mesh) and BlenderNaming.try_parse_mesh_name(bl_obj.name) is not None

    def is_collision_mesh(self, bl_obj: bpy.types.Object) -> bool:
        return isinstance(bl_obj.data, bpy.types.Mesh) and BlenderNaming.try_parse_collision_mesh_name(bl_obj.name) is not None

    def is_hair_strand_group_object(self, bl_obj: bpy.types.Object) -> bool:
        return BlenderNaming.try_parse_hair_strand_group_name(bl_obj.name) is not None

    def create_model_exporter(self, scale_factor: float, game: CdcGame) -> ModelExporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013ModelExporter(scale_factor)
            case CdcGame.SOTTR:
                return ShadowModelExporter(scale_factor)
            case _:
                return ModelExporter(scale_factor, game)

    def create_collision_model_exporter(self, scale_factor: float, game: CdcGame) -> CollisionModelExporter:
        return CollisionModelExporter(scale_factor, game)

    def requires_skeleton_export(self, game: CdcGame) -> bool:
        return game == CdcGame.TR2013

    def create_skeleton_exporter(self, scale_factor: float, game: CdcGame) -> SkeletonExporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013SkeletonExporter(scale_factor)
            case _:
                return SkeletonExporter(scale_factor, game)

    def requires_cloth_export(self, game: CdcGame) -> bool:
        return game == CdcGame.TR2013

    def create_cloth_exporter(self, scale_factor: float, game: CdcGame) -> ClothExporter:
        match game:
            case CdcGame.TR2013:
                return Tr2013ClothExporter(scale_factor)
            case _:
                return ClothExporter(scale_factor, game)

    def create_hair_exporter(self, scale_factor: float, game: CdcGame) -> HairExporter:
        return HairExporter(scale_factor, game)

    def create_tr2013_lara_duplicates(self, folder_path: str) -> None:
        self.create_file_duplicates(folder_path, (16648, 16663, 16716, 16729, 106832, 106837, 106881, 106886), ".tr9dtp")
        self.create_file_duplicates(folder_path, (16650, 16665, 16718, 16731, 106834, 106839, 106883, 106888), ".tr9dtp")
        self.create_file_duplicates(folder_path, (16645, 16660, 16713, 16726, 23789, 23807, 106878, 23800), ".tr9modeldata")

    def create_tr2013_model_dtp_duplicates(self, folder_path: str) -> None:
        model_id_groups: list[tuple[int, ...]] = [
            (2624, 53920),
            (2781, 85275, 87633, 95591),
            (2851, 2862, 33865),
            (2906, 2926, 23746),
            (3446, 4659),
            (3820, 5035, 19133, 19567, 21424, 21855, 30291, 30722, 34419, 34853, 36074, 37072, 37782, 40491, 40922, 44084, 44515, 45980, 46411, 47404, 47835, 49305, 49740, 51490, 51921, 58954, 59945, 61367, 61798, 63181, 64172, 65168, 66159, 69393, 69824, 71269, 71700, 77000, 77431, 78770, 79201, 80388, 80819, 82750, 83181, 90205, 90636, 91076, 91507, 91948, 92379, 92820, 93251, 93689, 94120, 94564, 94995, 96500, 97491, 109233, 109664, 118121, 118552),
            (5124, 5128, 5132, 5140, 89079),
            (5208, 5228, 5301, 5352, 5359, 5481, 5498, 5527, 5540, 5818, 6270, 6363, 6598, 6677, 6782, 6856, 6864, 7049, 7610, 41516),
            (5272, 5287, 5373, 5422, 5876, 5888, 5907, 5911, 5926, 5937, 5959, 5977, 5983, 6109, 6452, 6975, 7076, 7084, 7603, 7666, 31023, 31027, 31031, 31035, 31040, 31047, 31055, 31063, 31070, 31075, 31081, 31083, 31208, 31212, 31216, 31220, 31224, 31229, 31245, 31252, 41343, 85298, 85303),
            (5332, 5348, 23233),
            (5647, 5671, 5704, 5730),
            (5945, 5951, 5965, 6026, 6032, 6037, 6054, 6255, 6262, 6805, 7684),
            (6475, 38020),
            (6476, 38021),
            (6477, 38022),
            (6478, 38023),
            (6479, 38024),
            (6480, 38025),
            (6481, 38026),
            (6482, 38027),
            (6744, 23897, 50318, 50476, 50497),
            (7504, 26833, 41334, 41340, 56775, 56781, 56787, 68451, 68971, 70382, 70427, 70854, 71803, 72232, 72761, 73334, 73925, 74377, 74816, 75721, 76159, 76594, 77945, 79262, 79278, 79297, 79329, 79343, 79351, 79360, 79374, 79383, 79402, 79420, 79955, 81409, 81883, 82330, 83762, 84226, 85248, 106267),
            (7781, 7794, 7801, 7807),
            (7922, 8039, 8068),
            (7923, 8040, 8069, 105611),
            (7924, 8041, 8070),
            (7925, 8042, 8071, 105612),
            (7926, 8043, 8072),
            (7927, 8044, 8073, 105613),
            (7928, 8045, 8074),
            (7970, 7999, 8009, 8017, 8025),
            (7971, 8000, 8010, 8018, 8026),
            (7972, 8001, 8011, 8019, 8027),
            (7973, 8002, 8012, 8020, 8028),
            (7974, 8003, 8013, 8021, 8029),
            (7975, 8004, 8014, 8024, 8030),
            (8038, 8067),
            (8095, 8112),
            (8277, 8280),
            (8361, 8469),
            (8362, 8470),
            (8363, 8471),
            (8364, 8472),
            (8365, 8473),
            (8366, 8474),
            (8367, 8475),
            (8368, 8476),
            (8369, 8477),
            (8370, 8478),
            (8371, 8479),
            (8372, 8480),
            (8373, 8481),
            (8374, 8482),
            (8375, 8483),
            (8376, 8484),
            (8377, 8485),
            (8378, 8486),
            (8379, 8487),
            (8380, 8488),
            (8381, 8489),
            (8382, 8490),
            (8383, 8491),
            (8384, 8492),
            (8385, 8493),
            (8386, 8494),
            (8387, 8495),
            (8388, 8496),
            (8391, 8497),
            (8743, 19638, 19669, 19673),
            (8986, 57634),
            (9010, 9014),
            (11609, 87520),
            (11610, 87521),
            (11704, 95596),
            (12188, 12804),
            (12726, 48837, 48853),
            (13021, 13027),
            (13785, 13803, 60735),
            (13786, 13804, 60736),
            (13787, 13805, 60737),
            (13788, 13806, 60738),
            (13789, 13807, 60739),
            (13790, 13808, 60740),
            (13791, 13809, 60741),
            (14182, 14219),
            (14183, 14220),
            (14184, 14221),
            (14185, 14222),
            (14186, 14223),
            (14187, 14224),
            (14188, 14225),
            (14189, 14226),
            (14190, 14227),
            (14191, 14228),
            (14192, 14229),
            (14193, 14230),
            (14194, 14231),
            (14195, 14232),
            (14196, 14233),
            (14197, 14234),
            (14373, 14440, 39576, 87999, 95532, 102550, 108232),
            (14374, 14441, 39577, 88000, 95533, 102551, 108233),
            (14375, 14442, 39578, 88001, 95534, 102552, 108234),
            (14376, 14443, 39579, 88002, 95535, 102553, 108235),
            (14377, 14444, 39580, 88003, 95536, 102554, 108236),
            (14378, 14445, 39581, 88004, 95537, 102555, 108237),
            (14379, 14446, 39582, 88005, 95538, 102556, 108238),
            (14380, 14447, 39583, 88006, 95539, 102557, 108239),
            (14381, 14448, 39584, 88007, 95540, 102558, 108240),
            (14382, 14449, 39585, 88008, 95541, 102559, 108241),
            (14383, 14450, 39586, 88009, 95542, 102560, 108242),
            (14384, 14451, 39587, 88010, 95543, 102561, 108243),
            (14385, 14452, 39588, 88011, 95544, 102562, 108244),
            (14386, 14453, 39589, 88012, 95545, 102563, 108245),
            (14387, 14454, 39590, 88013, 95546, 102564, 108246),
            (14388, 14455, 39591, 88014, 95547, 102565, 108247),
            (14389, 14456, 39592, 88015, 95548, 102566, 108248),
            (14390, 14457, 39593, 88016, 95549, 102567, 108249),
            (14649, 14695),
            (16332, 16341, 16413, 16422, 16433, 16442, 16460, 16469, 16473, 16484, 16494, 16504, 16513, 16522, 16531, 16542, 16551, 16563, 16574, 16581, 16590, 16595, 16605, 16610, 16623, 16634, 16738, 16746, 16749, 16758, 16769, 16783, 35243, 37258, 51096),
            (17657, 17668),
            (17722, 17726),
            (18021, 103408),
            (18534, 28636, 28643, 28650, 28657),
            (18555, 18576, 18599),
            (18789, 19221),
            (21039, 21923, 21935, 23760, 102228),
            (21103, 21532),
            (21958, 24173),
            (22041, 22048, 31262, 31317, 31326, 31334, 31339, 31344, 31350, 31357, 31368, 31376, 31387, 31393, 31399, 31405, 31411, 31417, 31423, 31429, 31435, 31441, 31447, 31453, 31459, 31465, 31471, 31477, 31483, 31489, 31495, 31501, 31507, 31514, 31520, 31527, 31535, 31542, 31548, 31554, 31559, 31565, 31572, 31582, 31601, 31609, 31614, 31620, 31638, 31647, 31653, 31658, 31666, 31675, 31681, 31687, 31692, 31698, 31704, 31711, 31722, 31728, 31738, 31744, 31750, 31755, 31760, 31836, 31848, 31854, 31859, 31868, 31885, 31893, 31900, 31911, 31922, 31928, 31935, 31942, 31953, 31965, 31972, 31979, 31985, 32003, 32010, 32020, 32068, 32084, 32093, 32099, 32107, 32114, 32122, 32128, 32133, 32139, 32145, 32151, 32156, 32161, 32167, 32173, 32179, 32185, 32191, 32197, 32202, 32208, 32229, 32236, 32243, 32250, 32256, 32288, 32295, 32302, 32316, 32322, 32328, 32334, 32344, 32353, 32362, 32370, 32377, 32384, 32395, 32413, 32426, 32437, 32447, 32463, 32475, 32480, 32486, 32492, 32513, 32522, 32531, 32538, 43636, 43644, 95623, 95630, 95636, 95642),
            (23245, 34914),
            (23254, 23265, 23271, 23276, 23281, 23286, 23292),
            (23407, 23435),
            (23757, 62322, 62326),
            (23772, 29892, 29898),
            (23780, 34014, 34018, 34029),
            (23792, 106834),
            (23803, 106888),
            (23810, 106839),
            (23818, 78361, 78383),
            (23833, 45359, 46965, 48296, 48772, 48920, 50895),
            (23848, 53803),
            (23872, 58115, 58119, 58127),
            (23884, 60939, 60978, 105868),
            (23892, 62287, 62341, 62349, 62354),
            (23909, 84740),
            (23917, 106893, 108819, 108830, 108839),
            (24095, 24106),
            (24382, 25508),
            (24401, 24410, 24416),
            (24422, 24428),
            (24450, 57639),
            (24465, 24592, 108705),
            (24466, 24593, 108706),
            (24467, 24594, 108707),
            (24468, 24595, 108708),
            (24469, 24596, 108709),
            (24470, 24597, 108710),
            (24471, 24598, 108711),
            (24493, 24497),
            (24718, 24722, 47893),
            (24731, 24736, 47894),
            (24746, 24750, 47895),
            (24796, 67681),
            (24801, 47896),
            (24954, 24960),
            (24970, 26401),
            (24979, 24985, 25023, 25238, 25729, 25742, 25967, 25985, 26024, 26071, 26084, 26137, 26186),
            (25255, 25258),
            (25498, 25502),
            (25565, 25570, 25579),
            (26036, 26038),
            (26078, 26143),
            (26150, 26154, 26160),
            (26562, 26576),
            (26604, 26621),
            (26791, 26799),
            (28716, 34984),
            (28717, 34985),
            (29223, 55955),
            (29229, 29238),
            (29230, 29239),
            (29231, 29240),
            (29641, 118660),
            (29666, 118670),
            (29973, 30402),
            (30795, 30808),
            (30833, 30854),
            (31171, 113167),
            (31274, 31280, 31284, 31289, 31292, 31297, 31305, 85310, 85315, 121464),
            (31874, 32457, 32469, 32498, 43595, 43602, 43629, 43655, 43660, 95210, 95216, 95256, 95262, 95270, 95276, 95282, 95288, 95294, 95302, 95307, 95312, 95317, 95343, 95361, 95367, 95389, 95427, 95456),
            (32214, 95351),
            (32221, 95357),
            (32310, 32505),
            (32550, 32604),
            (32552, 32605),
            (32553, 32606),
            (32554, 32607),
            (32556, 32608),
            (32558, 32609),
            (32560, 32610),
            (32561, 32611),
            (32562, 32612),
            (32565, 32613),
            (32566, 32614),
            (32567, 32615),
            (32568, 32616),
            (32569, 32617),
            (32828, 32953, 111855, 113382, 113492, 113705, 115699, 116510, 116613, 116791),
            (33349, 33456, 114050, 116040, 116136, 116320, 116622),
            (33705, 33725),
            (33747, 42766),
            (33803, 112919),
            (33822, 114861),
            (33840, 33851),
            (34236, 34668),
            (35016, 35020),
            (35120, 35143, 35160),
            (35184, 35198),
            (35205, 35213),
            (35781, 36777),
            (38085, 38103),
            (38427, 38431),
            (38435, 38534),
            (38436, 38535),
            (38437, 38536),
            (38438, 38537),
            (38439, 38538),
            (38440, 38539),
            (38441, 38540),
            (38442, 38541),
            (38443, 38542),
            (38446, 38543),
            (38447, 38544),
            (38448, 38545),
            (38449, 38546),
            (38450, 38547),
            (38451, 38548),
            (38452, 38549),
            (38453, 38550),
            (38454, 38551),
            (38455, 38552),
            (38456, 38553),
            (38457, 38554),
            (38458, 38555),
            (38459, 38556),
            (38460, 38557),
            (38461, 38558),
            (38462, 38559),
            (38463, 38560),
            (38464, 38561),
            (38465, 38562),
            (38466, 38563),
            (38467, 38564),
            (38468, 38565),
            (38469, 38566),
            (38470, 38567),
            (38471, 38568),
            (38472, 38569),
            (38473, 38570),
            (38474, 38571),
            (38475, 38572),
            (38476, 38573),
            (38477, 38574),
            (38478, 38575),
            (38479, 38576),
            (38480, 38577),
            (38481, 38578),
            (38482, 38579),
            (38770, 38834),
            (39575, 87998, 102549, 108231),
            (39618, 39627),
            (40414, 40843),
            (42540, 43193),
            (43322, 43330, 53300, 112665),
            (43565, 43575, 43580, 43585, 43661),
            (44025, 44454),
            (45921, 46350),
            (47345, 47774),
            (48380, 54975),
            (49244, 49679),
            (50948, 50957, 54995),
            (51077, 51082),
            (51087, 51090, 51099),
            (51438, 51867),
            (52204, 52922, 53214, 53231, 53232, 53236, 53241, 53244, 53251, 53255, 53258, 53262, 53268, 53273),
            (52229, 52239, 52251, 52284, 52307, 52319, 52330, 52367, 52381, 52393, 52416, 52432, 52450, 52464, 52479, 52494, 52509, 52524, 52540, 52555, 52571, 52587, 52654, 52692, 52708, 52720, 52731, 52770, 52781, 52798, 52813, 52830, 52842, 52854, 112936),
            (52270, 52297, 52344, 52356, 52743, 52755, 52866, 52878),
            (52607, 52642),
            (52627, 52909),
            (52916, 85802),
            (52938, 52950),
            (53042, 53078, 53367),
            (53065, 53071, 53360),
            (53082, 53086, 53091, 53095, 53100),
            (53106, 53110),
            (53116, 111505),
            (53146, 112983),
            (53189, 114510),
            (53200, 53221, 114521),
            (53201, 53222),
            (53202, 53223),
            (53203, 53224),
            (53204, 53225),
            (53205, 53226),
            (54013, 54028, 54045),
            (54014, 54029, 54046),
            (54015, 54030, 54047),
            (54016, 54031, 54048),
            (54017, 54032, 54049),
            (54018, 54033, 54050),
            (54131, 54139),
            (54283, 54441),
            (54309, 54320),
            (54431, 54436),
            (54813, 54830),
            (54814, 54831),
            (57730, 57747, 57760, 57773, 57786, 57799, 57812, 57825, 57838, 57851, 57893, 57906, 57919),
            (57867, 57880),
            (58899, 59888),
            (60556, 60574),
            (60557, 60575),
            (60558, 60576),
            (60559, 60577),
            (60560, 60578),
            (60561, 60579),
            (60562, 60580),
            (60563, 60581, 60640),
            (60564, 60582, 60641),
            (60565, 60583, 60642),
            (60566, 60584, 60643),
            (60567, 60585, 60644),
            (60568, 60586, 60645),
            (60569, 60587, 60646),
            (60570, 60588, 60647),
            (60571, 60589, 60648),
            (60572, 60590, 60649),
            (60663, 60665),
            (60851, 60860, 60866),
            (60892, 60902),
            (61366, 61795),
            (63180, 64169),
            (65167, 66156),
            (67735, 67739),
            (69392, 69821),
            (71268, 71697),
            (71774, 71791),
            (75290, 77942),
            (76999, 77428),
            (77917, 85246),
            (78769, 79198),
            (79289, 79303),
            (80387, 80816),
            (82749, 83178),
            (85284, 85288, 85292, 85296, 89085),
            (85543, 85558),
            (85544, 85559),
            (85545, 85560),
            (85679, 121474),
            (85680, 121475),
            (85681, 121476),
            (85682, 121477),
            (85683, 121478),
            (85684, 121479),
            (85685, 121480),
            (85686, 121481),
            (85687, 121482),
            (85688, 121483),
            (85689, 121484),
            (85757, 85763),
            (85758, 85764),
            (85759, 85765),
            (85760, 85766),
            (87584, 87587),
            (88865, 88885, 88905),
            (90222, 90651),
            (91093, 91522),
            (91965, 92394),
            (92837, 93266),
            (93706, 94135),
            (94581, 95010),
            (95196, 105557, 105560, 105563, 105567, 105571, 105575, 105579, 105583, 105587, 105591, 105595, 105599),
            (95233, 95238),
            (95330, 95377, 95385, 95396),
            (95438, 95442),
            (95480, 95484, 95489, 95494, 95498, 95501, 95511, 95515),
            (96537, 97526),
            (97684, 97702),
            (97721, 97723),
            (102388, 102393),
            (103386, 103389),
            (103397, 103400),
            (105541, 105543, 105547, 105551),
            (109288, 109717),
            (109976, 110002, 110062),
            (110150, 110243, 110249),
            (110281, 110283, 110287, 110291, 111332, 111342, 111492, 111497, 112490, 112923, 114136, 116801),
            (110362, 110470, 110878, 111119),
            (110537, 114332),
            (110649, 110795, 111026, 111266, 114003),
            (111387, 111390),
            (111736, 112662),
            (111745, 111748),
            (111999, 112121, 112241, 112363, 112485),
            (112688, 112727, 112776, 112823, 112855, 116806, 117044),
            (112716, 112849, 112881, 116832, 117070),
            (112717, 112852, 112882, 116833, 117071),
            (112718, 112853, 112883, 116834, 117072),
            (112954, 112966, 114297, 114312),
            (113752, 113753),
            (113869, 114416, 114497, 114605, 114690),
            (113870, 114691),
            (113939, 113941),
            (114417, 114498),
            (118176, 118605)
        ]
        for model_id_group in model_id_groups:
            self.create_file_duplicates(folder_path, model_id_group, ".tr9dtp")

    def create_file_duplicates(self, folder_path: str, ids: Sequence[int], extension: str) -> None:
        file_paths = Enumerable(ids).select(lambda id: os.path.join(folder_path, f"{id}{extension}")).to_list()
        exported_file_path = Enumerable(file_paths).where(os.path.isfile).order_by_descending(os.path.getmtime).first_or_none()
        if exported_file_path is None:
            return

        for file_path in file_paths:
            if file_path != exported_file_path:
                shutil.copyfile(exported_file_path, file_path)