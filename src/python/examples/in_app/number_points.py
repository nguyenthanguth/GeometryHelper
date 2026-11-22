"""Numbers the points you pick: a text of height 2.5 with the next number at each point, until Enter.

Run in GeometryHelper CAD: PYRUN, then this file. It asks for the first number (1 on Enter), then for points.
The texts are added to the drawing and applied when the run ends, as one undo step, so one U takes every number back.
"""

from geometryhelper.cad import active_drawing, get_integer, get_point, write
from GeometryHelper.Geometry import GeoPoint2


def number_points() -> None:
    first = get_integer("First number", default=1)
    if not isinstance(first, int):
        return
    drawing = active_drawing()
    number = first
    while True:
        point = get_point("Specify point")
        if not isinstance(point, GeoPoint2):  # Enter: no more points
            break
        drawing.add_text(str(number), point, 2.5)
        number += 1
    count = number - first
    if count == 0:
        write("No point picked.")
    else:
        write(f"{count} point{'s' if count != 1 else ''} numbered, {first} to {number - 1}.")


number_points()
