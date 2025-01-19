import bpy
import os
from io_scene_tr_reboot.BlenderNaming import BlenderNaming
from io_scene_tr_reboot.exchange.ClothExporter import ClothExporter
from io_scene_tr_reboot.properties.SceneProperties import SceneProperties
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.tr.ResourceReader import ResourceReader
from io_scene_tr_reboot.tr.tr2013.Tr2013Collection import Tr2013ObjectHeader
from io_scene_tr_reboot.tr.tr2013.Tr2013ModelReferences import Tr2013ModelReferences
from io_scene_tr_reboot.util.Enumerable import Enumerable

class Tr2013ClothExporter(ClothExporter):
    def __init__(self, scale_factor: float) -> None:
        super().__init__(scale_factor, CdcGame.TR2013)

    def write_cloth_definition_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, definition_builder: ResourceBuilder) -> None:
        for bl_mesh_obj in Enumerable(bl_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)):
            model_id_set = BlenderNaming.parse_model_name(bl_mesh_obj.name)
            model_data_resource = ResourceKey(ResourceType.MODEL, model_id_set.model_data_id)
            model_data_file_path = os.path.join(folder_path, f"{model_data_resource.id}.tr9modeldata")
            if not os.path.isfile(model_data_file_path):
                continue

            model_refs = Tr2013ModelReferences(model_id_set.model_data_id)
            with open(model_data_file_path, "rb") as model_data_file:
                model_data_reader = ResourceReader(model_data_resource, model_data_file.read(), True, CdcGame.TR2013)

            model_refs.read(model_data_reader)
            model_data_reader.position = model_data_reader.resource_body_pos

            model_data_builder = ResourceBuilder(model_data_resource, CdcGame.TR2013)
            model_data_builder.write_reader(model_data_reader)

            model_refs.cloth_definition_ref = model_data_builder.make_internal_ref()
            model_data_builder.write_builder(definition_builder)

            model_data_builder.position = 0
            model_refs.write(model_data_builder)

            with open(model_data_file_path, "wb") as model_data_file:
                model_data_file.write(model_data_builder.build())

            break

    def write_cloth_tune_file(self, folder_path: str, bl_armature_obj: bpy.types.Object, tune_builder: ResourceBuilder) -> None:
        scene_properties = SceneProperties.get_instance(bpy.context.scene)
        object_id = Enumerable(bl_armature_obj.children).where(lambda o: isinstance(o.data, bpy.types.Mesh)) \
                                                        .select(lambda o: BlenderNaming.parse_model_name(o.name).object_id) \
                                                        .first_or_none()
        if object_id is None:
            return

        object_content = SceneProperties.get_file(scene_properties, object_id)
        if object_content is None:
            raise Exception()

        object_resource = ResourceKey(ResourceType.DTP, object_id)
        object_reader = ResourceReader(object_resource, object_content, True, CdcGame.TR2013)
        header = object_reader.read_struct(Tr2013ObjectHeader)

        object_builder = ResourceBuilder(object_resource, CdcGame.TR2013)
        object_builder.write_reader(object_reader)

        header.cloth_tune_ref = object_builder.make_internal_ref()
        object_builder.write_builder(tune_builder)

        object_builder.position = 0
        object_builder.write_struct(header)

        object_file_path = os.path.join(folder_path, Collection.make_resource_file_name(object_resource, CdcGame.TR2013))
        with open(object_file_path, "wb") as object_file:
            object_file.write(object_builder.build())
