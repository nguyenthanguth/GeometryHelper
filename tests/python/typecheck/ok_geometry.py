"""Type-checks clean: points and vectors as the stubs describe them (operators, keywords, overloads)."""

from GeometryHelper import Tolerance
from GeometryHelper.Geometry import GeoPoint2, GeoPoint3, GeoVector3, GeoXLine2, GeoVector2

p = GeoPoint3(1, 2, 3)
q = GeoPoint3(x=4, y=6, z=3)  # keyword arguments by the .NET parameter names
distance: float = p.DistanceTo(q)
offset: GeoVector3 = q - p
moved: GeoPoint3 = p + offset
scaled: GeoVector3 = offset * 2.0
scaled_from_the_left: GeoVector3 = 2.0 * offset
reversed_offset: GeoVector3 = -offset
same: bool = p == q
different: bool = p != q
length: float = offset.Length
text: str = str(p)
x: float = p.X
tolerance: Tolerance = Tolerance.Default

# the parameter named `from` is `from_` in the stubs, positional only; `to` stays a keyword
line = GeoXLine2(GeoPoint2(0, 0), GeoVector2(1, 0))
segment_a = line.ToLine(0.0, 10.0)
segment_b = line.ToLine(0.0, to=10.0)
