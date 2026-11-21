"""A6: DWG from Python: ``DwgWriter.Write``, ``DwgReader.Open``, ``CadDocument.Open`` of the DWG, edit, ``SaveAs``
.dwg and .dxf, reopen both. The drawings are made in code: line ``i`` runs from (10 i, 0, 0) to (10 i, 50, 0)."""

import hashlib
from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dwg import DwgReader, DwgWriter
from GeometryHelper.Dxf import DxfCircle, DxfLine

LinesDatabase = Callable[[int], Any]


def _lines(database: Any) -> list[Any]:
    return [entity for entity in database.ModelSpace if isinstance(entity, DxfLine)]


def test_a_hundred_lines_written_to_dwg_are_read_back_with_their_end_points(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    path = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(100), str(path))
    lines = _lines(DwgReader.Open(str(path)))
    assert len(lines) == 100
    assert sorted(line.StartPoint.X for line in lines) == [10.0 * i for i in range(100)]
    assert [(line.StartPoint.Y, line.EndPoint.Y, line.EndPoint.Z) for line in lines] == [(0.0, 50.0, 0.0)] * 100


@pytest.mark.parametrize("version", ["AC1015", "AC1018", "AC1024", "AC1027", "AC1032"])
def test_the_dwg_version_given_by_keyword_is_the_one_in_the_file_header(
    lines_database: LinesDatabase, tmp_path: Path, version: str
) -> None:
    # A DWG file starts with its version as six ASCII characters. The same ten lines come back from each version.
    path = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(10), str(path), version=version)
    assert path.read_bytes()[:6] == version.encode("ascii")
    assert len(_lines(DwgReader.Open(str(path)))) == 10


def test_the_version_left_out_gives_the_newest_the_writer_knows(lines_database: LinesDatabase, tmp_path: Path) -> None:
    path = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(3), str(path))
    assert path.read_bytes()[:6] == b"AC1032"


def test_opening_a_dwg_leaves_it_unchanged(lines_database: LinesDatabase, tmp_path: Path) -> None:
    path = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(5), str(path))
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    DwgReader.Open(str(path))
    CadDocument.Open(str(path))
    assert hashlib.sha256(path.read_bytes()).hexdigest() == before


def test_a_dwg_opened_as_a_document_and_edited_is_saved_as_dwg_and_as_dxf(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    # 100 lines and a circle of radius 25 about (0, 0): 101 entities in the document, in the DWG and in the DXF.
    source = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(100), str(source))
    document = CadDocument.Open(str(source))
    assert document.EntityCount == 100
    session = CadCommandSession(document)
    for answer in ("CIRCLE", "0,0", "25"):
        session.Feed(answer)
    assert document.EntityCount == 101
    as_dwg, as_dxf = tmp_path / "edited.dwg", tmp_path / "edited.dxf"
    document.SaveAs(str(as_dwg))
    document.SaveAs(str(as_dxf))
    for path in (as_dwg, as_dxf):
        reopened = CadDocument.Open(str(path))
        assert reopened.EntityCount == 101
        (circle,) = [e for e in reopened.Database.ModelSpace if isinstance(e, DxfCircle)]
        assert (circle.Center.X, circle.Center.Y, circle.Radius) == (0.0, 0.0, 25.0)
    assert as_dwg.read_bytes()[:2] == b"AC"
    assert b"SECTION" in as_dxf.read_bytes()[:40]


def test_a_drawing_saved_as_dwg_and_dxf_has_the_same_lines_in_both(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    source = tmp_path / "lines.dwg"
    DwgWriter.Write(lines_database(30), str(source))
    document = CadDocument.Open(str(source))
    as_dwg, as_dxf = tmp_path / "copy.dwg", tmp_path / "copy.dxf"
    document.SaveAs(str(as_dwg))
    document.SaveAs(str(as_dxf))

    def ends(path: Path) -> list[tuple[float, float, float, float]]:
        lines = _lines(CadDocument.Open(str(path)).Database)
        return sorted((line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y) for line in lines)

    assert ends(as_dwg) == ends(as_dxf) == [(10.0 * i, 0.0, 10.0 * i, 50.0) for i in range(30)]
