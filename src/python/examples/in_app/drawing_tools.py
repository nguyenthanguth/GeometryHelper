"""Two commands of your own for GeometryHelper CAD: RECTC (alias RC) and LAYERCOUNT.

Run in GeometryHelper CAD: PYLOAD, then this file; the commands are there until the app closes.
RECTC draws a rectangle about a centre you pick, of a width and a height you give; LAYERCOUNT counts the objects on
each layer.
"""

from collections import Counter

from geometryhelper.cad import active_drawing, command, get_distance, get_point, write
from GeometryHelper.Geometry import GeoPoint2


@command("RECTC", alias="RC")
def rectangle_by_centre() -> None:
    centre = get_point("Specify centre")
    if not isinstance(centre, GeoPoint2):
        return
    width = get_distance("Width", base=centre, default="1000")
    height = get_distance("Height", base=centre, default="500")
    if not isinstance(width, float) or not isinstance(height, float):
        return
    if width <= 0 or height <= 0:
        write("The width and the height must be more than 0.")
        return
    left, right = centre.X - width / 2, centre.X + width / 2
    bottom, top = centre.Y - height / 2, centre.Y + height / 2
    active_drawing().add_polyline([(left, bottom), (right, bottom), (right, top), (left, top)], closed=True)


@command("LAYERCOUNT")
def layer_count() -> None:
    drawing = active_drawing()
    # Counted under the layer table's spelling: an object on "walls" is on the layer the table calls "Walls".
    counts = Counter(
        drawing.layers[entity.layer].name if entity.layer in drawing.layers else entity.layer
        for entity in drawing.entities(layout="Model")
    )
    for name in sorted(counts, key=lambda name: (name.casefold(), name)):
        write(f"{name}: {counts[name]}")
