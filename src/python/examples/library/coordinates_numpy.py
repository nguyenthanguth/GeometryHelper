"""The vertices of every polyline of a DXF or DWG drawing as numpy arrays, and the extents of all of them.

Run: python coordinates_numpy.py DRAWING   (a .dxf or .dwg file; needs numpy: pip install geometryhelper[numpy])
Prints, in handle order, each polyline's handle, vertex count and length, then the extents of every vertex.
"""

import sys
from pathlib import Path

import numpy as np
from numpy.typing import NDArray

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from geometryhelper.arrays import to_numpy
from GeometryHelper.Dwg import DwgReader
from GeometryHelper.Dxf import DxfDatabase, DxfPolyline


def open_drawing(path: Path) -> DxfDatabase:
    """Reads a DXF or a DWG file, by its extension."""
    return DwgReader.Open(str(path)) if path.suffix.lower() == ".dwg" else DxfDatabase.Open(str(path))


def vertices(polyline: DxfPolyline) -> NDArray[np.float64]:
    """The vertices of a polyline as an array of shape (n, 2), copied from .NET in one block.

    A closed polyline gives its corners once: ToGeoPolyline2 would repeat the first corner at the end.
    """
    if polyline.Closed:
        return to_numpy(polyline.ToGeoPolygon2().Vertices)
    return to_numpy(polyline.ToGeoPolyline2().Vertices)


def main(argv: list[str]) -> int:
    if len(argv) != 1 or Path(argv[0]).suffix.lower() not in (".dxf", ".dwg"):
        print("usage: python coordinates_numpy.py DRAWING   (a .dxf or .dwg file)", file=sys.stderr)
        return 2
    database = open_drawing(Path(argv[0]))
    polylines = [entity for entity in database.ModelSpace if isinstance(entity, DxfPolyline)]
    polylines.sort(key=lambda polyline: int(polyline.Handle, 16))
    arrays: list[NDArray[np.float64]] = []
    for polyline in polylines:
        points = vertices(polyline)
        arrays.append(points)
        print(f"{polyline.Handle}\t{len(points)}\t{polyline.Length:.3f}")
    if arrays:
        every = np.vstack(arrays)
        low, high = every.min(axis=0), every.max(axis=0)
        print(f"extents\t{low[0]:.3f},{low[1]:.3f}\t{high[0]:.3f},{high[1]:.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
