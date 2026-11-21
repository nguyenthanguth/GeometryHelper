"""Union, difference, intersection and offset of two squares, with GeoPolygon2.

Run: python regions.py
Two squares of 100 (A from 0,0 and B from 50,50) are combined, and A is grown and shrunk by 10; each result is
printed as its area. Every call passes Tolerance.Default (a thousandth of a millimetre).
"""

import sys
from collections.abc import Iterable

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper import Tolerance
from GeometryHelper.Enums import OffsetJoin
from GeometryHelper.Geometry import GeoFace2, GeoPoint2, GeoPolygon2


def square(x: float, y: float, side: float) -> GeoPolygon2:
    """A square of the given side with its lower left corner at (x, y)."""
    return GeoPolygon2(GeoPoint2(x, y), GeoPoint2(x + side, y), GeoPoint2(x + side, y + side), GeoPoint2(x, y + side))


def faces_area(faces: Iterable[GeoFace2]) -> float:
    """The area of the faces, a hole of a face already taken off."""
    return sum(face.Area for face in faces)


def main(argv: list[str]) -> int:
    if argv:
        print("usage: python regions.py", file=sys.stderr)
        return 2
    tolerance = Tolerance.Default
    a = square(0, 0, 100)
    b = square(50, 50, 100)
    print(f"union {faces_area(a.Union(b, tolerance)):.3f}")
    print(f"subtract {faces_area(a.Subtract(b, tolerance)):.3f}")
    print(f"intersect {faces_area(a.Intersect(b, tolerance)):.3f}")
    for distance in (10, -10):  # a positive distance grows the region, a negative one shrinks it
        grown = a.Offset(distance, OffsetJoin.Miter, tolerance)
        print(f"offset {distance:+d} {sum(polygon.Area for polygon in grown):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
