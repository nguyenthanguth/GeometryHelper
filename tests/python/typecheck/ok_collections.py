"""Type-checks clean: the interface-typed collections pythonnet returns, and .NET lists and arrays built from Python."""

from System import Array
from System.Collections.Generic import List

from GeometryHelper import Tolerance
from GeometryHelper.Extension import GeoPointEnumerableExtension
from GeometryHelper.Geometry import GeoPoint2, GeoPolygon2

a, b, c = GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 3)
polygon = GeoPolygon2(a, b, c)  # separate arguments for a params array
from_list = GeoPolygon2([a, b, c])  # a Python list for the GeoPoint2[] overload
from_array = GeoPolygon2(Array[GeoPoint2]([a, b, c]))

# IReadOnlyList<T> as pythonnet wraps it: Count, an index, iteration and `in`
vertices = polygon.Vertices
how_many: int = vertices.Count
first: GeoPoint2 = vertices[0]
for vertex in vertices:
    x: float = vertex.X
contains: bool = b in vertices

# List<T> has len(); the extension method is a static method of its class (R13)
points: List[GeoPoint2] = List[GeoPoint2]()
points.Add(a)
size: int = len(points)
cleaned: List[GeoPoint2] = GeoPointEnumerableExtension.RemoveConsecutiveNearPoints(points, Tolerance.Default)
