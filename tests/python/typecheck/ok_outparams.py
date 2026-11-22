"""Type-checks clean: an out parameter with a parameter after it is left out, and the parameter is a keyword argument."""

from GeometryHelper import SolidBooleanOptions, Tolerance
from GeometryHelper.Core import Boolean3, Intersection2
from GeometryHelper.Enums import BooleanOutcome
from GeometryHelper.Geometry import GeoLine2, GeoObb3, GeoPoint2, GeoPoint3, GeoPolyline2, GeoSolid3

plate = GeoObb3(GeoPoint3(0, 0, 0), 100, 100, 10).ToSolid()
cutter = GeoObb3(GeoPoint3(0, 0, 0), 20, 20, 30).ToSolid()
tolerance = Tolerance.Default

made, left = plate.TrySubtract(cutter, tolerance=tolerance)
left_by_the_box: GeoSolid3 = left
made_here, left_here = Boolean3.TrySubtract(plate, cutter, tolerance=tolerance)
made_with_options, left_with_options, outcome = plate.TrySubtract(cutter, options=SolidBooleanOptions.Default)
came_out: BooleanOutcome = outcome
has_volume, volume = plate.TryGetVolume(tolerance=tolerance)
total: float = volume

line = GeoLine2(GeoPoint2(0, 0), GeoPoint2(10, 10))
crossed, crossing = Intersection2.TryIntersectWith(line, line, tolerance=tolerance)

# Overloads that differ only by an out parameter: pythonnet binds the shorter one, so the longer one is reached by
# naming one of its own out parameters, with a placeholder that is never read.
plain_made, plain_left = plate.TrySubtract(cutter)
named_made, named_left, named_how = plate.TrySubtract(cutter, outcome=BooleanOutcome.Made)
named_tolerance_made, named_tolerance_left, named_tolerance_how = plate.TrySubtract(
    cutter, tolerance=tolerance, outcome=BooleanOutcome.Made
)
joined_made, joined_solid, joined_how = Boolean3.TryUnion(plate, cutter, outcome=BooleanOutcome.Made)
how_many: BooleanOutcome = joined_how

polyline = GeoPolyline2(GeoPoint2(0, 0), GeoPoint2(10, 0), GeoPoint2(10, 10))
whole_made, whole_pieces = polyline.TrySplitAtDistance(5.0)
halves_made, first_half, second_half = polyline.TrySplitAtDistance(5.0, second=None)
first_piece: GeoPolyline2 = first_half
