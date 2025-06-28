from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.tr.rise.RiseCollectionFinder import RiseCollectionFinder

class ShadowCollectionFinder(RiseCollectionFinder):
    def __init__(self, starting_collection_file_path: str) -> None:
        super().__init__(starting_collection_file_path, CdcGame.SOTTR)
