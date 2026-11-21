"""PENDING (not collected by A10): type-checks clean only when D612 maps a .NET delegate to the pythonnet class and not
to `Callable` (T6): pythonnet refuses a Python function for a delegate parameter or property, and takes
`System.Action[...](function)` and `System.Func[...](function)`. Move to `typecheck/` once the lead has revised D612."""

from System import Action, Func
from System import Exception as NetException

from GeometryHelper import GeometryHelperLog
from GeometryHelper.Cad import CadSystemVariable, CadTrueTypeFonts
from GeometryHelper.Enums import GeometryHelperLogLevel

seen: list[str] = []


def write(level: GeometryHelperLogLevel, message: str, exception: NetException) -> None:
    seen.append(message)


GeometryHelperLog.Writer = Action[GeometryHelperLogLevel, str, NetException](write)
GeometryHelperLog.Enable = True
CadTrueTypeFonts.MeasureWidth = Func[str, bool, bool, str, float](lambda text, bold, italic, font: 1.0)
variable = CadSystemVariable.Real("X", 10.0, Func[float](lambda: 1.5), Action[float](lambda value: None))
