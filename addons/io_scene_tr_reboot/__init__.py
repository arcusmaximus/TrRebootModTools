bl_info = {
    "name": "TR Reboot mesh support",
    "description": "Import/export files for the Tomb Raider Reboot games",
    "author": "arc_",
    "blender": (4, 0, 0),
    "version": (1, 8, 1),
    "location": "File > Import-Export",
    "support": "COMMUNITY",
    "category": "Import-Export"
}

from typing import Callable, Protocol, cast
import bpy

from io_scene_tr_reboot.util.CStructTypeMappings import CStructTypeMappings
from io_scene_tr_reboot.tr.TrCStructTypeMappings import TrCStructTypeMappings
CStructTypeMappings.register()
TrCStructTypeMappings.register()

from io_scene_tr_reboot.DriverFunctions import DriverFunctions
from io_scene_tr_reboot.operator.BrowseBlendShapeNormalsSourceFileOperator import BrowseBlendShapeNormalsSourceFileOperator
from io_scene_tr_reboot.operator.ExportAnimationOperator import ExportShadowAnimationOperator
from io_scene_tr_reboot.operator.ExportModelOperator import ExportModelOperator
from io_scene_tr_reboot.operator.ImportAnimationOperator import ImportShadowAnimationOperator
from io_scene_tr_reboot.operator.ImportObjectOperator import ImportObjectOperator
from io_scene_tr_reboot.operator.FixVertexGroupNamesOperator import FixVertexGroupNamesOperator
from io_scene_tr_reboot.operator.HairWeightPaintingOperator import HairWeightPaintingOperator
from io_scene_tr_reboot.operator.PinClothBonesOperator import PinClothBonesOperator
from io_scene_tr_reboot.operator.RegenerateClothBonesOperator import RegenerateClothBonesOperator
from io_scene_tr_reboot.operator.ToggleCollisionVisibilityOperator import ToggleCollisionVisibilityOperator
from io_scene_tr_reboot.operator.UnpinClothBonesOperator import UnpinClothBonesOperator
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup
from io_scene_tr_reboot.properties.BoneProperties import BoneClothProperties, BoneConstraintProperties, BoneProperties
from io_scene_tr_reboot.properties.ObjectProperties import ObjectClothProperties, ObjectCollisionModelProperties, ObjectCollisionShapeProperties, ObjectMeshProperties, ObjectProperties, ObjectSkeletonProperties
from io_scene_tr_reboot.properties.SceneProperties import SceneFileProperties, SceneProperties
from io_scene_tr_reboot.properties.ToolSettingProperties import ToolSettingProperties
from io_scene_tr_reboot.ui.BonePanel import BonePanel
from io_scene_tr_reboot.ui.ClothBonesPanel import ClothBonesPanel
from io_scene_tr_reboot.ui.ClothSpringPanel import ClothSpringPanel
from io_scene_tr_reboot.ui.ClothStripPanel import ClothStripPanel
from io_scene_tr_reboot.ui.CollisionModelPanel import CollisionModelPanel
from io_scene_tr_reboot.ui.CollisionsPanel import CollisionsPanel
from io_scene_tr_reboot.ui.HairPanel import HairPanel
from io_scene_tr_reboot.ui.MeshPanel import MeshPanel
from io_scene_tr_reboot.ui.ScenePanel import ScenePanel

class ITrMenuOperator(Protocol):
    bl_idname: str
    bl_menu: bpy.types.Menu
    bl_menu_item_name: str

menu_operators: list[ITrMenuOperator] = [
    ImportObjectOperator,
    ExportModelOperator,
    ImportShadowAnimationOperator,
    ExportShadowAnimationOperator
]

other_classes: list[type] = [
    BrowseBlendShapeNormalsSourceFileOperator,
    FixVertexGroupNamesOperator,
    HairWeightPaintingOperator,
    PinClothBonesOperator,
    UnpinClothBonesOperator,
    RegenerateClothBonesOperator,
    ToggleCollisionVisibilityOperator,

    BonePanel,
    ClothBonesPanel,
    ClothStripPanel,
    ClothSpringPanel,
    CollisionModelPanel,
    CollisionsPanel,
    HairPanel,
    MeshPanel,
    ScenePanel
]

custom_property_groups: list[type[BlenderPropertyGroup]] = [
    BoneClothProperties,
    BoneConstraintProperties,
    BoneProperties,
    ObjectClothProperties,
    ObjectCollisionModelProperties,
    ObjectCollisionShapeProperties,
    ObjectMeshProperties,
    ObjectSkeletonProperties,
    ObjectProperties,
    SceneFileProperties,
    SceneProperties,
    ToolSettingProperties
]

menu_item_funcs: dict[ITrMenuOperator, Callable[[bpy.types.Menu, bpy.types.Context], None]] = {}

def make_menu_item_func(operator: ITrMenuOperator) -> Callable[[bpy.types.Menu, bpy.types.Context], None]:
    def draw_menu_item(menu: bpy.types.Menu, context: bpy.types.Context) -> None:
        if menu.layout is None:
            return

        menu.layout.operator(operator.bl_idname, text = operator.bl_menu_item_name)

    return draw_menu_item

def register() -> None:
    for operator in menu_operators:
        bpy.utils.register_class(cast(type, operator))
        draw_menu_item = make_menu_item_func(operator)
        operator.bl_menu.append(draw_menu_item)
        menu_item_funcs[operator] = draw_menu_item

    for cls in other_classes:
        bpy.utils.register_class(cls)

    for cls in custom_property_groups:
        cls.register()

    DriverFunctions.register()

def unregister() -> None:
    for operator in menu_operators:
        bpy.utils.unregister_class(cast(type, operator))
        operator.bl_menu.remove(menu_item_funcs[operator])
        del menu_item_funcs[operator]

    for cls in other_classes:
        bpy.utils.unregister_class(cls)

    for cls in custom_property_groups:
        cls.unregister()

    DriverFunctions.unregister()
