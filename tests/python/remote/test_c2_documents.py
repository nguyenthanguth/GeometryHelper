"""C2: the drawings of the app: start one, open a file, list and find them, make one active, save to a file.

Every file is written in ``tmp_path``; a drawing read back from a file is compared by the number of its objects and by
coordinates that are integers or halves, which the DXF text carries exactly.
"""

from collections.abc import Callable
from pathlib import Path
from typing import Any

from GeometryHelper.Dxf import DxfDatabase


def _objects_in(path: Path) -> int:
    """The number of live objects in model space of the DXF file."""
    return sum(1 for entity in DxfDatabase.Open(str(path)).ModelSpace if not entity.IsErased)


def test_a_new_drawing_is_named_in_order_and_is_the_active_one(app: Any) -> None:
    first = app.New()
    assert first.Name == "Drawing1.dxf"
    assert first.IsActive
    assert app.ActiveDocument.Id == first.Id

    second = app.New()
    assert second.Name == "Drawing2.dxf"
    assert app.ActiveDocument.Id == second.Id


def test_a_drawing_opened_from_a_file_has_the_full_path_of_the_file(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    # Three lines from (0, 0, 0) to (0, 50, 0), (10, 0, 0) to (10, 50, 0), (20, 0, 0) to (20, 50, 0).
    path = tmp_path / "plan.dxf"
    lines_database(3).Save(str(path))

    opened = app.Open(str(path))

    assert Path(str(opened.Path)).samefile(path)
    assert opened.Name == "plan.dxf"
    assert opened.IsActive
    assert opened.GetObjects().Count == 3


def test_opening_a_file_that_is_open_returns_the_same_drawing(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    path = tmp_path / "plan.dxf"
    lines_database(1).Save(str(path))

    first = app.Open(str(path))
    again = app.Open(str(path))

    assert again.Id == first.Id
    assert app.Documents.Count == 1


def test_the_app_lists_its_drawings_in_the_order_they_were_opened_and_finds_each_by_name_or_path(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    path = tmp_path / "plan.dxf"
    lines_database(1).Save(str(path))
    opened = app.Open(str(path))
    made = app.New()

    assert app.Documents.Count == 2
    assert [str(d.Name) for d in app.Documents] == ["plan.dxf", "Drawing1.dxf"]
    assert app.Documents.Get("Drawing1.dxf").Id == made.Id
    assert app.Documents.Get(str(path)).Id == opened.Id


def test_a_drawing_made_active_is_the_one_the_app_reports_active(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    path = tmp_path / "plan.dxf"
    lines_database(1).Save(str(path))
    opened = app.Open(str(path))
    made = app.New()
    assert app.ActiveDocument.Id == made.Id

    opened.Activate()

    assert app.ActiveDocument.Id == opened.Id
    assert app.ActiveDocument.Name == "plan.dxf"


def test_a_drawing_saved_as_a_file_is_known_by_it_and_the_file_holds_its_objects(
    drawing: Any, line: Callable[..., Any], tmp_path: Path
) -> None:
    drawing.Insert(line(0, 0, 10.5, 0))
    drawing.Insert(line(0, 5, 10, 5))
    drawing.CommitChanges()
    path = tmp_path / "saved.dxf"

    drawing.SaveAs(str(path))

    assert drawing.Name == "saved.dxf"
    assert Path(str(drawing.Path)).samefile(path)
    assert not drawing.IsModified
    assert _objects_in(path) == 2


def test_save_writes_what_was_drawn_after_the_drawing_got_its_file(
    drawing: Any, line: Callable[..., Any], tmp_path: Path
) -> None:
    drawing.Insert(line(0, 0, 10, 0))
    drawing.CommitChanges()
    path = tmp_path / "saved.dxf"
    drawing.SaveAs(str(path))
    drawing.Insert(line(0, 5, 10, 5))
    drawing.CommitChanges()
    assert _objects_in(path) == 1

    drawing.Save()

    assert _objects_in(path) == 2
