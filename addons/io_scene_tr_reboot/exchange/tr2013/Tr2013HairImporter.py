from io_scene_tr_reboot.exchange.HairImporter import HairImporter
from io_scene_tr_reboot.tr.Collection import Collection
from io_scene_tr_reboot.tr.Enumerations import ResourceType
from io_scene_tr_reboot.tr.ResourceKey import ResourceKey

class Tr2013HairImporter(HairImporter):
    def get_collection_files_to_store(self, tr_collection: Collection) -> dict[int, bytes]:
        files: dict[int, bytes] = {}

        tr_hair = tr_collection.get_hair()
        if tr_hair is not None and tr_hair.model_id is not None:
            model_reader = tr_collection.get_resource_reader(ResourceKey(ResourceType.DTP, tr_hair.model_id), True)
            if model_reader is not None:
                files[tr_hair.model_id] = model_reader.data

        return files
