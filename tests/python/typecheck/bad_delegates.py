"""PENDING (not collected by A10): the errors the revised D612 should give for what pythonnet refuses (T6)."""

from GeometryHelper import GeometryHelperLog
from GeometryHelper.Cad import CadSystemVariable

GeometryHelperLog.Writer = lambda level, message, exception: None  # expect: reportAttributeAccessIssue
variable = CadSystemVariable.Real("X", 10.0, lambda: 1.5, lambda v: None)  # expect: reportArgumentType|reportCallIssue
