"""Draws a grid in a running GeometryHelper CAD, reads it back, and saves it when you name a file.

Run outside the app, with "Allow other programs to connect" on in its Global settings: python draw_and_read.py [OUT]
A new drawing gets a layer Grid and 22 lines, 11 each way over 1000 by 1000 at 100 spacing, as one undo step; the
lines are read back by layer and kind, and the drawing is saved as OUT (.dxf or .dwg) when it is given.
"""

import sys
from pathlib import Path

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper.Cad.Remote import CadApplication, CadObjectFilter, CadRemoteLayer
from GeometryHelper.Dxf import DxfLine
from GeometryHelper.Geometry import GeoPoint3

LAYER = "Grid"


def grid_line(x1: float, y1: float, x2: float, y2: float) -> DxfLine:
    """A line in the plane z = 0, on the layer Grid."""
    line = DxfLine(GeoPoint3(x1, y1, 0), GeoPoint3(x2, y2, 0))
    line.Layer = LAYER
    return line


def main(app: CadApplication, out: str | None) -> None:
    drawing = app.New()
    drawing.AddLayer(CadRemoteLayer(LAYER))
    for step in range(0, 1001, 100):
        drawing.Insert(grid_line(step, 0, step, 1000))  # a vertical line
        drawing.Insert(grid_line(0, step, 1000, step))  # a horizontal one
    drawing.CommitChanges()  # the layer first, then the lines: one undo step

    only_grid_lines = CadObjectFilter()  # set the properties after making it: pythonnet has no keyword initialiser
    only_grid_lines.Kind = DxfLine
    only_grid_lines.Layer = LAYER
    lines = [entity for entity in drawing.GetObjects(only_grid_lines) if isinstance(entity, DxfLine)]
    print(f"{len(lines)} lines on {LAYER}, total length {sum(line.Length for line in lines):.3f}")
    if out is not None:
        drawing.SaveAs(out)
        print(f"saved {Path(out).name}")


if __name__ == "__main__":
    with CadApplication() as app:
        main(app, sys.argv[1] if len(sys.argv) > 1 else None)
