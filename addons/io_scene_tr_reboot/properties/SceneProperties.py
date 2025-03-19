import base64
from typing import Annotated
import bpy
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderAttachedPropertyGroup, BlenderPropertyGroup, BlenderPropertyGroupCollection, EnumProp, EnumPropItem, Prop
from io_scene_tr_reboot.tr.Enumerations import CdcGame
from io_scene_tr_reboot.util.Enumerable import Enumerable

class SceneFileProperties(BlenderPropertyGroup):
    id: Annotated[int, Prop("File ID")]
    data: Annotated[str, Prop("File data")]

class SceneProperties(BlenderAttachedPropertyGroup[bpy.types.Scene]):
    property_name = "tr11_properties"

    game: Annotated[
        str,
        EnumProp(
            "Game",
            [
                EnumPropItem(CdcGame.TR2013, "Tomb Raider (2013)"),
                EnumPropItem(CdcGame.ROTTR,  "Rise of the Tomb Raider"),
                EnumPropItem(CdcGame.SOTTR,  "Shadow of the Tomb Raider")
            ],
            default = CdcGame.SOTTR
        )
    ]
    files: Annotated[BlenderPropertyGroupCollection[SceneFileProperties], Prop("Files")]

    @staticmethod
    def get_game() -> CdcGame:
        if bpy.context.scene is None:
            return CdcGame.SOTTR

        props = SceneProperties.get_instance(bpy.context.scene)
        return CdcGame[props.game]

    @staticmethod
    def set_game(game: CdcGame) -> None:
        if bpy.context.scene is None:
            return

        props = SceneProperties.get_instance(bpy.context.scene)
        props.game = game.name

    @staticmethod
    def get_file(id: int) -> bytes | None:
        if bpy.context.scene is None:
            return None

        props = SceneProperties.get_instance(bpy.context.scene)
        file = Enumerable(props.files).first_or_none(lambda f: f.id == id)
        if file is None:
            return None

        return base64.b64decode(file.data)

    @staticmethod
    def set_file(id: int, data: memoryview | bytes) -> None:
        if bpy.context.scene is None:
            return

        props = SceneProperties.get_instance(bpy.context.scene)
        file = Enumerable(props.files).first_or_none(lambda f: f.id == id)
        if file is None:
            file = props.files.add()
            file.id = id

        file.data = base64.b64encode(data).decode()
