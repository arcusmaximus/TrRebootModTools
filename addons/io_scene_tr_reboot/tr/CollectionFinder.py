from abc import abstractmethod
from glob import glob
import os
import re
from typing import overload
from io_scene_tr_reboot.operator.OperatorContext import OperatorContext
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.util.Enumerable import Enumerable

class CollectionFinder:
    game: CdcGame
    starting_collection_folder_path: str
    platform_folder_path: str | None
    search_paths: list[str]

    failed_collection_names: set[str]

    def __init__(self, starting_collection_file_path: str, game: CdcGame) -> None:
        self.game = game
        self.starting_collection_folder_path = os.path.dirname(starting_collection_file_path)
        self.platform_folder_path = None
        self.search_paths = []
        self.failed_collection_names = set()

        match = re.match(r"^.+[\\/]pc(x64)?-w[\\/]", starting_collection_file_path)
        if match is None:
            return

        self.platform_folder_path = match.group(0)
        self.search_paths.append(self.platform_folder_path)
        self.search_paths.append(os.path.join(self.platform_folder_path, "streamlayers"))

    @overload
    def find_root_file(self, id: int, /) -> str | None: ...

    @overload
    def find_root_file(self, name: str, /) -> str | None: ...

    def find_root_file(self, id_or_name: int | str) -> str | None:
        collection_name: str
        if isinstance(id_or_name, int):
            found_collection_name = self.get_collection_name_from_id(id_or_name)
            if found_collection_name is None:
                return None

            collection_name = found_collection_name
        else:
            collection_name = id_or_name

        collection_file_path = os.path.join(self.starting_collection_folder_path, f"{collection_name}.tr{self.game}objectref")
        if os.path.isfile(collection_file_path):
            return collection_file_path

        for search_path in self.search_paths:
            collection_folder_path = os.path.join(search_path, f"{collection_name}.drm")
            if not os.path.isdir(collection_folder_path):
                continue

            collection_file_path = self.get_root_file_path(collection_folder_path, collection_name)
            if collection_file_path is not None:
                return collection_file_path

        self.failed_collection_names.add(collection_name)
        return None

    @abstractmethod
    def get_collection_name_from_id(self, id: int) -> str | None: ...

    def get_root_file_path(self, collection_folder_path: str, collection_name: str) -> str | None:
        collection_file_path = os.path.join(collection_folder_path, f"{collection_name}.tr{self.game}objectref")
        if os.path.isfile(collection_file_path):
            return collection_file_path

        layer_file_paths = glob(os.path.join(collection_folder_path, f"*.tr{self.game}layer"))
        if len(layer_file_paths) == 1:
            return layer_file_paths[0]

        return None

    def log_missing_files(self) -> None:
        for collection_name in Enumerable(self.failed_collection_names).order_by(lambda n: n):
            OperatorContext.log_warning(f"Failed to find referenced DRM {collection_name} - is it extracted?")
