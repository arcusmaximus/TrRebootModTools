import os
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.tr.CollectionFinder import CollectionFinder
from io_scene_tr_reboot.tr.Enumerations import CdcGame

class RiseCollectionFinder(CollectionFinder):
    zone_names: list[str]
    zone_id_lookup_failed: bool

    def __init__(self, starting_collection_file_path: str, game: CdcGame = CdcGame.ROTTR) -> None:
        super().__init__(starting_collection_file_path, game)
        self.zone_names = []
        self.zone_id_lookup_failed = False

        if self.platform_folder_path is None:
            return

        zone_ids_file_path = os.path.join(self.platform_folder_path, "zonelist.ids")
        if os.path.isfile(zone_ids_file_path):
            self.read_zone_names(zone_ids_file_path)

    def read_zone_names(self, file_path: str) -> None:
        with open(file_path) as file:
            counts = file.readline().strip()
            num_drms = int(counts.split(",")[1])
            self.zone_names = [""] * (1 + num_drms)

            for line in file:
                split_line = line.strip().split(",")
                drm_id = int(split_line[0])
                drm_name = split_line[1]
                self.zone_names[drm_id] = drm_name

    def get_collection_name_from_id(self, id: int) -> str | None:
        if len(self.zone_names) == 0 and not self.zone_id_lookup_failed:
            self.zone_id_lookup_failed = True

        if id < 1 or id >= len(self.zone_names):
            return None

        return self.zone_names[id]

    def log_missing_files(self) -> None:
        super().log_missing_files()
        if self.zone_id_lookup_failed:
            OperatorContext.log_warning("Failed to read zonelist.ids - is it extracted?")
