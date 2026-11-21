"""Totals the length and the closed area of the objects you select.

Run in GeometryHelper CAD: PYRUN, then this file; it asks "Select objects:" as a command does.
Lines, arcs, circles (their circumference) and polylines add to the length; circles and closed polylines to the area.
"""

from geometryhelper.cad import document, get_selection, write
from GeometryHelper.Dxf import DxfArc, DxfCircle, DxfLine, DxfPolyline


def selection_lengths() -> None:
    handles = get_selection()
    if not handles:
        write("Nothing selected.")
        return
    length = 0.0
    area = 0.0
    skipped = 0
    for handle in handles:
        entity = document().FindEntity(handle)
        if isinstance(entity, DxfCircle):
            length += entity.Circumference
            area += entity.Area
        elif isinstance(entity, (DxfLine, DxfArc)):
            length += entity.Length
        elif isinstance(entity, DxfPolyline):
            length += entity.Length
            area += entity.Area if entity.Closed else 0.0
        else:
            skipped += 1
    write(f"{len(handles)} objects: length {length:.3f}, closed area {area:.3f}, {skipped} skipped")


selection_lengths()
