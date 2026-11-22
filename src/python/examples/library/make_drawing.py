"""Makes a drawing in code, with no window, and saves it as DXF or DWG.

Run: python make_drawing.py OUT   (OUT ends in .dxf or .dwg)
An empty drawing gets a layer Outline, made current, then a rectangle 4000 by 3000 and a circle of radius 500 on it,
applied together by one commit; the drawing is then saved as OUT.
"""

import sys
from pathlib import Path

from geometryhelper.cad import new_file


def main(argv: list[str]) -> int:
    if len(argv) != 1 or Path(argv[0]).suffix.lower() not in (".dxf", ".dwg"):
        print("usage: python make_drawing.py OUT   (OUT ends in .dxf or .dwg)", file=sys.stderr)
        return 2
    drawing = new_file()  # an empty drawing: layer 0 only
    drawing.layers.add("Outline")
    drawing.commit()  # the layer must exist before it can be made current
    drawing.current_layer = "Outline"
    drawing.add_polyline([(0, 0), (4000, 0), (4000, 3000), (0, 3000)], closed=True, layer="Outline")
    drawing.add_circle((2000, 1500), 500, layer="Outline")
    drawing.commit()
    drawing.save_as(argv[0])
    layers = sorted((layer.name for layer in drawing.layers), key=lambda name: (name.casefold(), name))
    print(f"objects {len(drawing.entities())}")
    print(f"layers {', '.join(layers)}")
    print(f"saved {Path(argv[0]).name}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
