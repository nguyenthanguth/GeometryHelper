"""Lists the layers of a DXF or DWG drawing: the objects of model space on each, and the area of its closed shapes.

Run: python list_layers.py DRAWING   (a .dxf or .dwg file)
Prints one line per layer of the layer table, sorted by name, then the totals. The area is that of the closed
polylines and the circles on the layer, in the drawing's units squared.
"""

import sys
from pathlib import Path

import geometryhelper  # noqa: F401  (starts .NET and loads the assemblies)
from GeometryHelper.Dwg import DwgReader
from GeometryHelper.Dxf import DxfCircle, DxfDatabase, DxfEntity, DxfPolyline


def open_drawing(path: Path) -> DxfDatabase:
    """Reads a DXF or a DWG file, by its extension."""
    return DwgReader.Open(str(path)) if path.suffix.lower() == ".dwg" else DxfDatabase.Open(str(path))


def closed_area(entity: DxfEntity) -> float:
    """The area a closed polyline or a circle encloses; nought for any other object."""
    if isinstance(entity, DxfPolyline) and entity.Closed:
        return entity.Area
    if isinstance(entity, DxfCircle):
        return entity.Area
    return 0.0


def main(argv: list[str]) -> int:
    if len(argv) != 1 or Path(argv[0]).suffix.lower() not in (".dxf", ".dwg"):
        print("usage: python list_layers.py DRAWING   (a .dxf or .dwg file)", file=sys.stderr)
        return 2
    database = open_drawing(Path(argv[0]))
    # A layer name is one name in whatever case, but an object keeps the spelling it was written with ("walls" on a
    # layer the table calls "Walls"), so objects are counted under the table's spelling.
    spelling: dict[str, str] = {layer.Name.casefold(): layer.Name for layer in database.LayerTable}
    counts: dict[str, int] = dict.fromkeys(spelling.values(), 0)
    areas: dict[str, float] = dict.fromkeys(counts, 0.0)
    for entity in database.ModelSpace:
        layer = spelling.setdefault(entity.Layer.casefold(), entity.Layer)
        counts[layer] = counts.get(layer, 0) + 1
        areas[layer] = areas.get(layer, 0.0) + closed_area(entity)
    print("layer\tobjects\tarea")
    for name in sorted(counts, key=lambda name: (name.casefold(), name)):
        print(f"{name}\t{counts[name]}\t{areas[name]:.3f}")
    print(f"total\t{sum(counts.values())}\t{sum(areas.values()):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
