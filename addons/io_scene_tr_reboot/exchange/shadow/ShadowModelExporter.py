import os
from io_scene_tr_reboot.exchange.ModelExporter import ModelExporter
from io_scene_tr_reboot.tr.Enumerations import CdcGame, ResourceType
from io_scene_tr_reboot.tr.Model import IModel
from io_scene_tr_reboot.tr.ResourceBuilder import ResourceBuilder
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey
from io_scene_tr_reboot.util.IoHelper import IoHelper

class ShadowModelExporter(ModelExporter):
    def __init__(self, scale_factor: float) -> None:
        super().__init__(scale_factor, CdcGame.SOTTR)

    def export_extra_files(self, folder_path: str, object_id: int, tr_model: IModel) -> None:
        model_file_path = os.path.join(folder_path, f"{tr_model.id}.tr11model")
        with IoHelper.open_write(model_file_path) as model_file:
            resource_builder = ResourceBuilder(ResourceKey(ResourceType.MODEL, tr_model.id), self.game)
            tr_model.refs.write(resource_builder)
            model_file.write(resource_builder.build())
