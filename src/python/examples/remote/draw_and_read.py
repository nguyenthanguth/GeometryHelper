"""Draws a grid in a running GeometryHelper CAD, reads it back, and saves it when you name a file.

Run outside the app, with "Allow other programs to connect" on in its Global settings: python draw_and_read.py [OUT]
A new drawing gets a layer Grid and 22 lines, 11 each way over 1000 by 1000 at 100 spacing, as one undo step; the
lines are read back by layer and kind, and the drawing is saved as OUT (.dxf or .dwg) when it is given.
"""

import sys
from pathlib import Path

from geometryhelper.cad import App
from GeometryHelper.Cad.Remote import CadApplication

LAYER = "Grid"


def main(app: CadApplication, out: str | None) -> None:
    cad = App(app)  # the Drawing API over the connection, as connect() gives it
    drawing = cad.new()
    drawing.layers.add(LAYER)
    for step in range(0, 1001, 100):
        drawing.add_line((step, 0), (step, 1000), layer=LAYER)  # a vertical line
        drawing.add_line((0, step), (1000, step), layer=LAYER)  # a horizontal one
    drawing.commit()  # the layer first, then the lines: one undo step

    lines = drawing.entities("LINE", layer=LAYER)
    print(f"{len(lines)} lines on {LAYER}, total length {sum(line.net.Length for line in lines):.3f}")
    if out is not None:
        drawing.save_as(out)
        print(f"saved {Path(out).name}")


if __name__ == "__main__":
    with CadApplication() as app:
        main(app, sys.argv[1] if len(sys.argv) > 1 else None)
