"""Type-checks clean: the module geometryhelper.cad as a script of GeometryHelper CAD uses it (P3 D709)."""

from collections.abc import Sequence

from geometryhelper import cad
from geometryhelper.cad import Cancelled, cad_command
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Geometry import GeoPoint2

document: CadDocument = cad.document()
session: CadCommandSession = cad.session()
handles: list[str] = cad.selection()

cad.write("a line")
cad.command("CIRCLE", (0, 0), 50)
cad.command("_LINE", GeoPoint2(0, 0), (100, 0), "")
cad.command("ERASE", handles)
cad.command("MOVE", 1, 2.5)

point: GeoPoint2 | str | None = cad.get_point("Where", GeoPoint2(0, 0), ["Close"], "Close")
if isinstance(point, GeoPoint2):
    x: float = point.X
radius: float | str | None = cad.get_distance("Radius", GeoPoint2(0, 0))
angle: float | str | None = cad.get_angle()
count: int | str | None = cad.get_integer("How many", default=3)
factor: float | str | None = cad.get_real("Factor", ["Auto"], 1.0)
name: str | None = cad.get_string("Name", "Anon", spaces=False)
keywords: Sequence[str] = ("Yes", "No")
mode: str | None = cad.get_keyword("Mode", keywords, "No")
picked: str | None = cad.get_entity("Pick", ["Last"])
chosen: list[str] = cad.get_selection()


@cad_command("HELLO", alias="HI", description="Says hello")
def hello() -> None:
    cad.write("hello")


@cad_command("COUNT")
def number_of_things() -> int:
    return len(cad.selection())


try:
    cad.get_point()
except Cancelled:
    pass
