"""Type-checks with exactly the errors marked by an expect comment on their lines."""

from GeometryHelper import Tolerance
from GeometryHelper.Dxf import DxfDatabase, DxfEraseFlags, DxfWriter
from GeometryHelper.Geometry import GeoObb3, GeoPoint3

# a [Flags] enum combines with itself, not with a number (pythonnet refuses `flag | 1`)
mixed = DxfEraseFlags.ZeroLengthCurve | 1  # expect: reportOperatorIssue

database = DxfDatabase.Parse(DxfWriter().ToString())
# an out parameter is not an argument (it comes back in the tuple), and the tuple has two items
with_placeholder = database.TryGetObject("1F", None)  # expect: reportCallIssue
first, second, third = database.TryGetObject("1F")  # expect: reportAssignmentType

# an out parameter with a parameter after it is left out only when that parameter is a keyword argument
plate = GeoObb3(GeoPoint3(0, 0, 0), 100, 100, 10).ToSolid()
cutter = GeoObb3(GeoPoint3(0, 0, 0), 20, 20, 30).ToSolid()
positional_tolerance = plate.TrySubtract(cutter, Tolerance.Default)  # expect: reportCallIssue
placeholder_and_tolerance = plate.TrySubtract(cutter, None, Tolerance.Default)  # expect: reportCallIssue
volume_with_a_positional_tolerance = plate.TryGetVolume(Tolerance.Default)  # expect: reportCallIssue
