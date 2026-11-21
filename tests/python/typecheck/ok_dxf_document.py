"""Type-checks clean: a drawing made in code, saved, read, edited through a document and its command session."""

from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dwg import DwgReader, DwgWriter
from GeometryHelper.Dxf import DxfDatabase, DxfEntity, DxfLine, DxfWriter
from GeometryHelper.Geometry import GeoPoint3

database = DxfDatabase.Parse(DxfWriter().ToString())
database.ModelSpace.AppendEntity(DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(1, 0, 0)))
for entity in database.ModelSpace:
    layer: str = entity.Layer
    if isinstance(entity, DxfLine):
        end: GeoPoint3 = entity.EndPoint
        entity.EndPoint = GeoPoint3(2, 0, 0)

database.Save("a.dxf")
DwgWriter.Write(database, "a.dwg")  # the optional version left out
DwgWriter.Write(database, "a.dwg", "AC1027")
DwgWriter.Write(database, "a.dwg", version="AC1027")
from_dwg: DxfDatabase = DwgReader.Open("a.dwg")

# out parameters are returned in a tuple, after the value of the method (R8)
found, found_object = database.TryGetObject("1F")
is_found: bool = found

document = CadDocument.Open("a.dwg")
session = CadCommandSession(document)
for answer in ("CIRCLE", "0,0", "25"):
    session.Feed(answer)
count: int = document.EntityCount
document.SaveAs("b.dxf")
first: DxfEntity = next(iter(document.Database.ModelSpace))
