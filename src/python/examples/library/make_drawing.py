"""Makes a drawing in code, with no window, and saves it as DXF or DWG.

Run: python make_drawing.py OUT   (OUT ends in .dxf or .dwg)
An empty drawing gets a layer Outline, made current, a rectangle 4000 by 3000 and a circle of radius 500, typed on
the command line of its session in one undo step; the drawing is then saved as OUT.
"""

import sys
from pathlib import Path

import geometryhelper
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dxf import DxfDatabase, DxfWriter


def main(argv: list[str]) -> int:
    if len(argv) != 1 or Path(argv[0]).suffix.lower() not in (".dxf", ".dwg"):
        print("usage: python make_drawing.py OUT   (OUT ends in .dxf or .dwg)", file=sys.stderr)
        return 2
    document = CadDocument.From(DxfDatabase.Parse(DxfWriter().ToString()))  # an empty drawing: layer 0 only
    session = CadCommandSession(document)
    with geometryhelper.undo_group(document, "Outline"):
        for answer in ("LAYER", "M", "Outline", ""):  # Make a layer, called Outline, and Enter to end LAYER
            session.Feed(answer)
        for answer in ("RECTANG", "0,0", "4000,3000"):
            session.Feed(answer)
        for answer in ("CIRCLE", "2000,1500", "500"):
            session.Feed(answer)
    document.SaveAs(argv[0])
    layers = sorted((layer.Name for layer in document.Database.LayerTable), key=lambda name: (name.casefold(), name))
    print(f"objects {document.EntityCount}")
    print(f"layers {', '.join(layers)}")
    print(f"saved {Path(argv[0]).name}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
