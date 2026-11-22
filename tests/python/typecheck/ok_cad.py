"""Type-checks clean: the module geometryhelper.cad as a script of GeometryHelper CAD uses it (P3 D709, D1650 to D1654)."""

from collections.abc import Sequence

from geometryhelper import cad
from geometryhelper.cad import Cancelled, Entity, command, on_unload
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Geometry import GeoPoint2

document: CadDocument = cad.session().Document
session: CadCommandSession = cad.session()

cad.write("a line")
cad.cmd("CIRCLE", (0, 0), 50)
cad.cmd("_LINE", GeoPoint2(0, 0), (100, 0), "")
cad.cmd("CIRCLE", cad.PAUSE, 50)
cad.cmd("MOVE", 1, 2.5)

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
picked: Entity | str | None = cad.get_entity("Pick", ["Last"])
chosen: list[Entity] = cad.get_selection()
cad.cmd("ERASE", chosen)


@command("HELLO", alias="HI", description="Says hello")
def hello() -> None:
    cad.write("hello")


@command("COUNT", uses_selection=True)
def number_of_things() -> int:
    return len(cad.get_selection())


@on_unload
def goodbye() -> None:
    cad.write("goodbye")


try:
    cad.get_point()
except Cancelled:
    pass
