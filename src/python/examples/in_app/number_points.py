"""Numbers the points you pick: a text of height 2.5 with the next number at each point, until Enter.

Run in GeometryHelper CAD: PYRUN, then this file. It asks for the first number (1 on Enter), then for points.
The whole run is one undo step, so one U takes every number back.
"""

from geometryhelper.cad import cmd, get_integer, get_point, write
from GeometryHelper.Geometry import GeoPoint2


def number_points() -> None:
    first = get_integer("First number", default=1)
    if not isinstance(first, int):
        return
    number = first
    while True:
        point = get_point("Specify point")
        if not isinstance(point, GeoPoint2):  # Enter: no more points
            break
        cmd("TEXT", point, 2.5, 0, str(number), "")  # start point, height, rotation, the text, Enter to end TEXT
        number += 1
    count = number - first
    if count == 0:
        write("No point picked.")
    else:
        write(f"{count} point{'s' if count != 1 else ''} numbered, {first} to {number - 1}.")


number_points()
