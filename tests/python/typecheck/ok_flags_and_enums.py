"""Type-checks clean: enums are classes with class variables; a [Flags] enum combines with itself."""

from GeometryHelper.Cad import CadSupportKind
from GeometryHelper.Dxf import DxfEraseFlags

both: DxfEraseFlags = DxfEraseFlags.ZeroLengthCurve | DxfEraseFlags.EmptyText
only_text: DxfEraseFlags = both & DxfEraseFlags.EmptyText
without: DxfEraseFlags = both & ~DxfEraseFlags.EmptyText
as_number: int = int(both)
kind: CadSupportKind = CadSupportKind.Fonts
kind_number: int = int(kind)
