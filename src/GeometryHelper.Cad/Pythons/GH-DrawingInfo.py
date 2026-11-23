r"""GH-DrawingInfo.py -- a sample command: GHPYINFO writes what the drawing holds.

Try it: type GHPYINFO at the command line. It only reads; the drawing is not changed. Loading this file writes
nothing. It is the Python twin of GH-DrawingInfo.lsp (GHINFO) and of the DrawingInfo plugin (GHDWGINFO).

The output for a drawing of two circles and a line, a layer Steel and a block BOLT:

    Drawing: Plan.dwg in C:\Projects
    Current layer: Steel
    Model space: 3 objects
      CIRCLE   2
      LINE     1
    Layers: 2, blocks: 1
    Extents: (-10.00,-10.00) to (120.00,80.00)

Delete this file if you do not want the command: every .py file in this folder loads when the app starts.
"""

import os
from collections import Counter

from geometryhelper.cad import active_drawing, command, write


@command("GHPYINFO")
def drawing_info() -> None:
    drawing = active_drawing()
    folder = os.path.dirname(drawing.path) if drawing.path else "not saved yet"
    write(f"Drawing: {drawing.name} in {folder}")
    write(f"Current layer: {drawing.current_layer}")

    # The objects of model space, counted by type and listed by name, so the lines come in the same order every time.
    counts = Counter(entity.dxf_type for entity in drawing.entities(layout="Model"))
    write(f"Model space: {sum(counts.values())} objects")
    for name in sorted(counts):
        write(f"  {name:<9}{counts[name]}")

    write(f"Layers: {len(drawing.layers)}, blocks: {len(drawing.blocks)}")

    # The extents are an empty box (the least corner above the greatest) until something is drawn.
    least, greatest = drawing.net.Database.Extmin, drawing.net.Database.Extmax
    if least.X > greatest.X:
        write("Extents: none")
    else:
        write(f"Extents: ({least.X:.2f},{least.Y:.2f}) to ({greatest.X:.2f},{greatest.Y:.2f})")
