"""A box with a hole cut through it, measured: volume, surface area and centroid, with GeoSolid3.

Run: python solids.py
A box 1000 by 500 by 200 has a hole of 100 by 100 cut through it by a box that sticks out of both faces. The measures
and the cut all take Tolerance.Default, passed by name to TrySubtract, whose tolerance comes after its out parameter.
"""

import sys

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper import Tolerance
from GeometryHelper.Geometry import GeoAabb3, GeoPoint3, GeoSolid3


def box(x1: float, y1: float, z1: float, x2: float, y2: float, z2: float) -> GeoSolid3:
    """The solid of the box between two opposite corners."""
    return GeoAabb3(GeoPoint3(x1, y1, z1), GeoPoint3(x2, y2, z2)).ToObb().ToSolid()


def main(argv: list[str]) -> int:
    if argv:
        print("usage: python solids.py", file=sys.stderr)
        return 2
    tolerance = Tolerance.Default
    plate = box(0, 0, 0, 1000, 500, 200)
    cutter = box(450, 200, -100, 550, 300, 300)
    done, result = plate.TrySubtract(cutter, tolerance=tolerance)
    if not done:
        print("error: nothing was left of the box", file=sys.stderr)
        return 1
    centroid = result.GetCentroid(tolerance)
    print(f"volume {result.GetVolume(tolerance):.3f}")
    print(f"area {result.GetSurfaceArea(tolerance):.3f}")
    print(f"centroid {centroid.X:.3f},{centroid.Y:.3f},{centroid.Z:.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
