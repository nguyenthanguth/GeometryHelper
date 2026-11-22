"""Type-checks clean: a member that takes null takes None, and an enum is made from a number."""

from GeometryHelper import GeometryHelperLog
from GeometryHelper.Cad import CadTrueTypeFonts
from GeometryHelper.Cad.Remote import CadObjectFilter
from GeometryHelper.Dxf import DxfEndCap, DxfEraseFlags, DxfLine

# CadObjectFilter: null for every layer, every layout and every kind
everything = CadObjectFilter()
everything.Layer = "Steel"
everything.Layer = None
everything.Layout = None
everything.Kind = DxfLine
everything.Kind = None
layer_set: str = everything.Layer

# a delegate property takes None, which puts the default back
GeometryHelperLog.Writer = None
CadTrueTypeFonts.MeasureWidth = None

# an enum is made from a number, a [Flags] one from a combination too
erased: DxfEraseFlags = DxfEraseFlags(3)
round_cap: DxfEndCap = DxfEndCap(1)
number: int = int(erased)
