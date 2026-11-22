"""Lists the layers of a DXF or DWG drawing: the objects of model space on each, and the area of its closed shapes.

Run: python list_layers.py DRAWING   (a .dxf or .dwg file)
Prints one line per layer of the layer table, sorted by name, then the totals. The area is that of the closed
polylines and the circles on the layer, in the drawing's units squared.
"""

import sys
from pathlib import Path

from geometryhelper.cad import Entity, open_file
from GeometryHelper.Dxf import DxfCircle, DxfPolyline


def closed_area(entity: Entity) -> float:
    """The area a closed polyline or a circle encloses; nought for any other object."""
    if isinstance(entity.net, DxfPolyline) and entity.net.Closed:
        return entity.net.Area
    if isinstance(entity.net, DxfCircle):
        return entity.net.Area
    return 0.0


def main(argv: list[str]) -> int:
    if len(argv) != 1 or Path(argv[0]).suffix.lower() not in (".dxf", ".dwg"):
        print("usage: python list_layers.py DRAWING   (a .dxf or .dwg file)", file=sys.stderr)
        return 2
    drawing = open_file(argv[0])
    # A layer name is one name in whatever case, but an object keeps the spelling it was written with ("walls" on a
    # layer the table calls "Walls"), so objects are counted under the table's spelling.
    spelling: dict[str, str] = {layer.name.casefold(): layer.name for layer in drawing.layers}
    counts: dict[str, int] = dict.fromkeys(spelling.values(), 0)
    areas: dict[str, float] = dict.fromkeys(counts, 0.0)
    for entity in drawing.entities(layout="Model"):
        layer = spelling.setdefault(entity.layer.casefold(), entity.layer)
        counts[layer] = counts.get(layer, 0) + 1
        areas[layer] = areas.get(layer, 0.0) + closed_area(entity)
    print("layer\tobjects\tarea")
    for name in sorted(counts, key=lambda name: (name.casefold(), name)):
        print(f"{name}\t{counts[name]}\t{areas[name]:.3f}")
    print(f"total\t{sum(counts.values())}\t{sum(areas.values()):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
