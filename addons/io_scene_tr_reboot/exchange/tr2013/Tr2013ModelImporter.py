from io_scene_tr_reboot.exchange.ModelImporter import ModelImporter
from io_scene_tr_reboot.tr.Collection import Collection

class Tr2013ModelImporter(ModelImporter):
    def get_collection_files_to_store(self, tr_collection: Collection) -> dict[int, bytes]:
        files: dict[int, bytes] = {}

        object_reader = tr_collection.get_resource_reader(tr_collection.object_ref, True)
        if object_reader is not None:
            files[tr_collection.object_ref.id] = object_reader.data

        for model in tr_collection.get_model_instances():
            model_reader = tr_collection.get_resource_reader(model.model_resource, True)
            if model_reader is not None:
                files[model.model_resource.id] = model_reader.data

        return files
