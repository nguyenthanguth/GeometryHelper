"""A5: DXF from Python: write, read, edit an entity, save, reopen, compare. The drawings are made in code: line ``i`` of
the test drawing runs from (10 i, 0, 0) to (10 i, 50, 0)."""

import hashlib
from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dxf import DxfCircle, DxfDatabase, DxfLine
from GeometryHelper.Geometry import GeoPoint3

LinesDatabase = Callable[[int], Any]


def _lines(database: Any) -> list[Any]:
    return [entity for entity in database.ModelSpace if isinstance(entity, DxfLine)]


def _draw_circle(session: Any, x: float, radius: float) -> None:
    for answer in ("CIRCLE", f"{x},0", str(radius)):
        session.Feed(answer)


def test_a_hundred_lines_written_to_dxf_are_read_back_with_their_end_points(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    path = tmp_path / "lines.dxf"
    lines_database(100).Save(str(path))
    lines = _lines(DxfDatabase.Open(str(path)))
    assert len(lines) == 100
    assert sorted(line.StartPoint.X for line in lines) == [10.0 * i for i in range(100)]
    assert [(line.StartPoint.Y, line.EndPoint.Y, line.EndPoint.Z) for line in lines] == [(0.0, 50.0, 0.0)] * 100


def test_opening_a_file_leaves_it_unchanged(lines_database: LinesDatabase, tmp_path: Path) -> None:
    path = tmp_path / "lines.dxf"
    lines_database(5).Save(str(path))
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    DxfDatabase.Open(str(path))
    CadDocument.Open(str(path))
    assert hashlib.sha256(path.read_bytes()).hexdigest() == before


def test_a_line_moved_and_put_on_a_new_layer_keeps_both_after_saving_and_reopening(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    # The end of line 3 (30, 50, 0) goes to (30, 80, 0), and the line onto a layer WALLS that did not exist.
    path = tmp_path / "lines.dxf"
    lines_database(5).Save(str(path))
    database = DxfDatabase.Open(str(path))
    third = next(line for line in _lines(database) if line.StartPoint.X == 30.0)
    handle = str(third.Handle)
    third.EndPoint = GeoPoint3(30, 80, 0)
    third.Layer = "WALLS"
    edited = tmp_path / "edited.dxf"
    database.Save(str(edited))
    again = next(line for line in _lines(DxfDatabase.Open(str(edited))) if str(line.Handle) == handle)
    assert (again.StartPoint.X, again.EndPoint.Y, str(again.Layer)) == (30.0, 80.0, "WALLS")
    others = [line for line in _lines(DxfDatabase.Open(str(edited))) if str(line.Handle) != handle]
    assert [line.EndPoint.Y for line in others] == [50.0] * 4


def test_a_circle_drawn_through_the_command_session_is_saved_with_its_centre_and_radius(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    # 10 lines and a circle of radius 25 about (0, 0): 11 entities, the circle among them.
    source = tmp_path / "lines.dxf"
    lines_database(10).Save(str(source))
    document = CadDocument.Open(str(source))
    assert document.EntityCount == 10
    _draw_circle(CadCommandSession(document), 0, 25)
    assert document.EntityCount == 11
    saved = tmp_path / "drawn.dxf"
    document.SaveAs(str(saved))
    reopened = CadDocument.Open(str(saved))
    assert reopened.EntityCount == 11
    (circle,) = [e for e in reopened.Database.ModelSpace if isinstance(e, DxfCircle)]
    assert (circle.Center.X, circle.Center.Y, circle.Radius) == (0.0, 0.0, 25.0)


def test_a_circle_drawn_and_erased_leaves_the_drawing_as_it_was(lines_database: LinesDatabase, tmp_path: Path) -> None:
    # CIRCLE then ERASE with the answer L (the last object): 10 lines, 11 with the circle, 10 again.
    source = tmp_path / "lines.dxf"
    lines_database(10).Save(str(source))
    document = CadDocument.Open(str(source))
    session = CadCommandSession(document)
    _draw_circle(session, 0, 25)
    session.Feed("ERASE")
    session.Feed("L")
    session.Enter()
    assert document.EntityCount == 10
    saved = tmp_path / "erased.dxf"
    document.SaveAs(str(saved))
    assert CadDocument.Open(str(saved)).EntityCount == 10


def test_saving_the_same_document_twice_gives_two_files_with_the_same_lines(
    lines_database: LinesDatabase, tmp_path: Path
) -> None:
    # The files are compared by their lines, not by bytes: a saved file may carry the time it was written.
    source = tmp_path / "lines.dxf"
    lines_database(20).Save(str(source))
    document = CadDocument.Open(str(source))
    first, second = tmp_path / "first.dxf", tmp_path / "second.dxf"
    document.SaveAs(str(first))
    document.SaveAs(str(second))

    def starts(path: Path) -> list[float]:
        return sorted(line.StartPoint.X for line in _lines(CadDocument.Open(str(path)).Database))

    assert starts(first) == starts(second) == [10.0 * i for i in range(20)]


@pytest.mark.parametrize("count", [0, 1])
def test_a_drawing_of_no_line_or_one_line_survives_the_round_trip(
    lines_database: LinesDatabase, tmp_path: Path, count: int
) -> None:
    path = tmp_path / "few.dxf"
    lines_database(count).Save(str(path))
    assert len(_lines(DxfDatabase.Open(str(path)))) == count
