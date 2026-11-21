"""Type-checks with exactly the errors marked by an expect comment on their lines."""

from GeometryHelper.Dxf import DxfDatabase, DxfEraseFlags, DxfWriter

# a [Flags] enum combines with itself, not with a number (pythonnet refuses `flag | 1`)
mixed = DxfEraseFlags.ZeroLengthCurve | 1  # expect: reportOperatorIssue

database = DxfDatabase.Parse(DxfWriter().ToString())
# an out parameter is not an argument (it comes back in the tuple), and the tuple has two items
with_placeholder = database.TryGetObject("1F", None)  # expect: reportCallIssue
first, second, third = database.TryGetObject("1F")  # expect: reportAssignmentType
