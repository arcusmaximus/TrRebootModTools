from types import TracebackType
from typing import TYPE_CHECKING, ClassVar
import bpy
from io_scene_tr_reboot.util.SlotsBase import SlotsBase

if TYPE_CHECKING:
    from bpy.stub_internal.rna_enums import WmReportItems
else:
    WmReportItems = str

class OperatorContext:
    current_operator: ClassVar[bpy.types.Operator | None] = None
    warnings_logged: ClassVar[bool] = False
    errors_logged: ClassVar[bool] = False

    @staticmethod
    def begin(operator: bpy.types.Operator) -> "RunningOperator":
        return RunningOperator(operator)

    @staticmethod
    def log_info(message: str) -> None:
        OperatorContext.log("INFO", message)

    @staticmethod
    def log_warning(message: str) -> None:
        OperatorContext.log("WARNING", message)
        OperatorContext.warnings_logged = True

    @staticmethod
    def log_error(message: str) -> None:
        OperatorContext.log("ERROR", message)
        OperatorContext.errors_logged = True

    @staticmethod
    def log(type: WmReportItems, message: str) -> None:
        if OperatorContext.current_operator is not None:
            OperatorContext.current_operator.report({ type }, message)

class RunningOperator(SlotsBase):
    def __init__(self, operator: bpy.types.Operator) -> None:
        OperatorContext.current_operator = operator
        OperatorContext.warnings_logged = False
        OperatorContext.errors_logged = False

    def __enter__(self) -> None:
        pass

    def __exit__(self, exc_type: type[BaseException] | None, exc_val: BaseException | None, exc_tb: TracebackType | None) -> None:
        OperatorContext.current_operator = None
