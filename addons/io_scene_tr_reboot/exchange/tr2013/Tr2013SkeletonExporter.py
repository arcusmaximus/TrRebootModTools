import bpy
import os
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.SkeletonExporter import SkeletonExporter
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.ResourceReference import ResourceReference
from io_scene_tr_reboot.tr.Skeleton import ISkeleton
from io_scene_tr_reboot.tr.tr2013.Tr2013LegacyModel import Tr2013LegacyModel
from io_scene_tr_reboot.tr.tr2013.Tr2013Skeleton import Tr2013Skeleton
from io_scene_tr_reboot.util.Enumerable import Enumerable

class Tr2013SkeletonExporter(SkeletonExporter):
    def __init__(self, scale_factor: float) -> None:
        super().__init__(scale_factor, CdcGame.TR2013)

    def write_skeleton_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, tr_skeleton: ISkeleton) -> None:
        if not isinstance(tr_skeleton, Tr2013Skeleton):
            raise Exception()

        handled_model_ids: set[int] = set()

        for bl_obj in Enumerable(bl_armature_obj.children):
            model_id: int
            model_data_id: int

            if isinstance(bl_obj.data, bpy.types.Mesh):
                model_id_set = BlenderNaming.parse_model_name(bl_obj.name)
                model_id = model_id_set.model_id
                model_data_id = model_id_set.model_data_id
            elif isinstance(bl_obj.data, bpy.types.Curves):
                hair_id_set = BlenderNaming.parse_hair_strand_group_name(bl_obj.name)
                if hair_id_set.model_id is None:
                    continue

                model_id = hair_id_set.model_id
                model_data_id = hair_id_set.hair_data_id
            else:
                continue

            if model_id in handled_model_ids:
                continue

            handled_model_ids.add(model_id)

            model_bytes = SceneProperties.get_file(model_id)
            model_data_path = os.path.join(folder_path, f"{model_data_id}.tr9modeldata")
            if model_bytes is None or not os.path.isfile(model_data_path):
                continue

            model_resource = ResourceKey(ResourceType.DTP, model_id)
            model_data_resource = ResourceKey(ResourceType.MODEL, model_data_id)

            with open(model_data_path, "rb") as model_data_file:
                model_data_reader = ResourceReader(model_data_resource, model_data_file.read(), True, CdcGame.TR2013)

            model = Tr2013LegacyModel(model_bytes)
            if model.bones_ref is None or model.bone_id_map_ref is None:
                continue

            model_data_builder = ResourceBuilder(model_data_resource, CdcGame.TR2013)
            model_data_builder.write_reader(model_data_reader)

            model.bones_ref = ResourceReference(ResourceType.MODEL, model_data_id, model_data_builder.position)
            tr_skeleton.write_bones(model_data_builder)

            model.bone_id_map_ref = ResourceReference(ResourceType.MODEL, model_data_id, model_data_builder.position)
            tr_skeleton.write_id_mappings(model_data_builder)

            model_path = os.path.join(folder_path, Collection.make_resource_file_name(model_resource, CdcGame.TR2013))
            with open(model_path, "wb") as model_file:
                model_file.write(model.to_bytes())

            with open(model_data_path, "wb") as model_data_file:
                model_data_file.write(model_data_builder.build())
