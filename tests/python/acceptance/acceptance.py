"""Acceptance script of the geometryhelper package.

In a fresh virtual environment with the wheel installed (``pip install "<wheel>[numpy]"``) it makes a drawing in code,
saves it as DXF and as DWG, reads both back, edits the DWG through a document and its command session, saves the
result in both formats, and reads geometry into numpy. Nothing comes from outside: the files are written to a
temporary folder that is removed at the end. It exits 0 when every step holds; pyright reports no error on it.
"""

import tempfile
from pathlib import Path

import numpy as np

import geometryhelper
from geometryhelper import undo_group
from geometryhelper.arrays import to_numpy, to_points2
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dwg import DwgReader, DwgWriter
from GeometryHelper.Dxf import DxfDatabase, DxfLine, DxfWriter
from GeometryHelper.Geometry import GeoPoint3, GeoPolygon2


def count_lines(database: DxfDatabase) -> int:
    return sum(1 for entity in database.ModelSpace if isinstance(entity, DxfLine))


def main() -> None:
    print(f"geometryhelper {geometryhelper.__version__} on {geometryhelper.runtime()}")
    with tempfile.TemporaryDirectory() as folder:
        work = Path(folder)

        # A drawing of 20 lines made in code, saved as DXF and as DWG.
        drawing = DxfDatabase.Parse(DxfWriter().ToString())
        for i in range(20):
            drawing.ModelSpace.AppendEntity(DxfLine(GeoPoint3(10 * i, 0, 0), GeoPoint3(10 * i, 50, 0)))
        dxf_path, dwg_path = work / "lines.dxf", work / "lines.dwg"
        drawing.Save(str(dxf_path))
        DwgWriter.Write(drawing, str(dwg_path))

        # Both read back: 20 lines each.
        assert count_lines(DxfDatabase.Open(str(dxf_path))) == 20
        assert count_lines(DwgReader.Open(str(dwg_path))) == 20

        # The DWG opened as a document and edited: a circle of radius 25 about the origin, one undo step.
        document = CadDocument.Open(str(dwg_path))
        session = CadCommandSession(document)
        with undo_group(document, "circle"):
            for answer in ("CIRCLE", "0,0", "25"):
                session.Feed(answer)
        assert document.EntityCount == 21

        # Saved in both formats and reopened: 21 entities each.
        edited_dwg, edited_dxf = work / "edited.dwg", work / "edited.dxf"
        document.SaveAs(str(edited_dwg))
        document.SaveAs(str(edited_dxf))
        assert CadDocument.Open(str(edited_dwg)).EntityCount == 21
        assert CadDocument.Open(str(edited_dxf)).EntityCount == 21

        # Geometry into numpy: a rectangle 40 by 30, its area by the shoelace sum against the library's.
        polygon = GeoPolygon2(to_points2(np.array([[0.0, 0.0], [40.0, 0.0], [40.0, 30.0], [0.0, 30.0]])))
        corners = to_numpy(polygon.Vertices)
        x, y = corners[:, 0], corners[:, 1]
        shoelace = 0.5 * abs(float(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1))))
        assert abs(shoelace - float(polygon.Area)) < 1e-9
        assert abs(shoelace - 1200.0) < 1e-9

    print("acceptance: ok")


if __name__ == "__main__":
    main()
