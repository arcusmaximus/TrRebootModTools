import bpy
from bpy.types import Context
from bpy_extras.io_utils import ImportHelper, ExportHelper
from typing import TYPE_CHECKING, Annotated, Any, Generic, Protocol, TypeVar, cast
from io_scene_tr_reboot.properties.BlenderPropertyGroup import BlenderPropertyGroup, Prop
from io_scene_tr_reboot.util.Enumerable import Enumerable

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import OperatorReturnItems
else:
    OperatorReturnItems = str

class BlenderOperatorMetaClass(type(bpy.types.Operator)):
    def __new__(cls: type, class_name: str, bases: tuple[type, ...], namespace: dict[str, Any]) -> type:
        orig_bases: tuple[Any, ...] | None = namespace.get("__orig_bases__")
        if class_name != "BlenderOperatorBase" and orig_bases is not None:
            base_class_alias = Enumerable(orig_bases).first_or_none(lambda b: hasattr(b, "__origin__") and issubclass(getattr(b, "__origin__"), BlenderOperatorBase))
            if base_class_alias is not None:
                annotations: dict[str, Any] = {}
                property_class: type | None = base_class_alias.__args__[0]
                while property_class is not None and isinstance(property_class, type) and property_class != BlenderPropertyGroup:
                    annotations.update(property_class.__annotations__)
                    property_class = Enumerable(property_class.__bases__).first_or_none(BlenderOperatorMetaClass.is_property_group_type)

                namespace["__annotations__"] = BlenderPropertyGroup.convert_annotations(class_name, annotations)

        return cast(type, super()).__new__(cls, class_name, bases, namespace)

    @staticmethod
    def is_property_group_type(t: type) -> bool:
        if t == type:
            return False

        return t == BlenderPropertyGroup or Enumerable(t.__bases__).any(BlenderOperatorMetaClass.is_property_group_type)

TProperties = TypeVar("TProperties", bound = BlenderPropertyGroup)

class BlenderOperatorBase(Generic[TProperties], bpy.types.Operator, metaclass = BlenderOperatorMetaClass):
    properties: TProperties                                                                                     # type: ignore


class BlenderMenuOperatorBase(Generic[TProperties], BlenderOperatorBase[TProperties], Protocol if TYPE_CHECKING else object):    # type: ignore
    bl_menu: bpy.types.Menu
    bl_menu_item_name: str


class ImportOperatorProperties(BlenderPropertyGroup, Protocol):
    filepath: Annotated[str, Prop("File path", subtype = cast(Any, "FILE_PATH"))]
    filter_glob: Annotated[str, Prop("Filter", options = { "HIDDEN" })]

TImportProperties = TypeVar("TImportProperties", bound = ImportOperatorProperties)

class ImportOperatorBase(Generic[TImportProperties], BlenderMenuOperatorBase[TImportProperties], Protocol if TYPE_CHECKING else object):
    bl_menu = cast(bpy.types.Menu, bpy.types.TOPBAR_MT_file_import)
    bl_label = "Import"

    filename_ext: str

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        self.properties.filter_glob = self.filename_ext.replace(".", "*.")
        return ImportHelper.invoke(self, context, event)            # type: ignore

    def check(self, context: Context | None) -> bool:
        return ImportHelper.check(self, context)                    # type: ignore



class ExportOperatorProperties(BlenderPropertyGroup, Protocol):
    filepath: Annotated[str, Prop("File path", subtype = cast(Any, "FILE_PATH"))]
    filter_glob: Annotated[str, Prop("Filter", options = { "HIDDEN" })]
    check_existing: Annotated[bool, Prop("Check Existing", default = True, options = { "HIDDEN" })]

TExportOperatorProperties = TypeVar("TExportOperatorProperties", bound = ExportOperatorProperties)

class ExportOperatorBase(Generic[TExportOperatorProperties], BlenderOperatorBase[TExportOperatorProperties], Protocol if TYPE_CHECKING else object):   # type: ignore
    bl_menu = cast(bpy.types.Menu, bpy.types.TOPBAR_MT_file_export)
    bl_label = "Export"
    check_extension = True

    filename_ext: str

    def invoke(self, context: bpy.types.Context | None, event: bpy.types.Event) -> set[OperatorReturnItems]:
        self.properties.filter_glob = self.filename_ext.replace(".", "*.")
        return ExportHelper.invoke(self, context, event)            # type: ignore

    def check(self, context: Context | None) -> bool:
        return ExportHelper.check(self, context)                    # type: ignore
