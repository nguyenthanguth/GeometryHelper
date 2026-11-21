"""Two commands of your own for GeometryHelper CAD: RECTC (alias RC) and LAYERCOUNT.

Run in GeometryHelper CAD: PYLOAD, then this file; the commands are there until the app closes.
RECTC draws a rectangle about a centre you pick, of a width and a height you give; LAYERCOUNT counts the objects on
each layer.
"""

from collections import Counter

from geometryhelper.cad import cad_command, command, document, get_distance, get_point, write
from GeometryHelper.Geometry import GeoPoint2


@cad_command("RECTC", alias="RC")
def rectangle_by_centre() -> None:
    centre = get_point("Specify centre")
    if not isinstance(centre, GeoPoint2):
        return
    width = get_distance("Width", base=centre, default="1000")
    height = get_distance("Height", base=centre, default="500")
    if not isinstance(width, float) or not isinstance(height, float):
        return
    corner = (centre.X - width / 2, centre.Y - height / 2)
    opposite = (centre.X + width / 2, centre.Y + height / 2)
    command("RECTANG", corner, opposite)


@cad_command("LAYERCOUNT")
def layer_count() -> None:
    counts = Counter(entity.Layer for entity in document().Database.ModelSpace)
    for name in sorted(counts, key=lambda name: (name.casefold(), name)):
        write(f"{name}: {counts[name]}")
