import os
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.Enumerations import CdcGame

class Tr2013CollectionFinder(CollectionFinder):
    def __init__(self, starting_collection_file_path: str) -> None:
        super().__init__(starting_collection_file_path, CdcGame.TR2013)

    def get_root_file_path(self, collection_folder_path: str, collection_name: str) -> str | None:
        if os.path.basename(os.path.dirname(collection_folder_path)) == "streamlayers":
            return os.path.join(collection_folder_path, "0.tr9layer")

        return super().get_root_file_path(collection_folder_path, collection_name)

    def get_collection_name_from_id(self, id: int) -> str | None:
        return None
