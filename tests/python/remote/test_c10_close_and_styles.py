"""C10: close a drawing of the app, and read its text styles, dimension styles and blocks.

``Close()`` closes a drawing with nothing unsaved; a drawing with changes is refused with the app's own message until the
caller says ``discard`` (``Close(True)``), and is then closed without saving. The reads are copies, sorted by name ignoring
case. Every file is written in ``tmp_path``; the drawing read in has the names ``Zeta`` and ``alpha`` (``Wide`` and
``dense``, ``Nut`` and ``bolt``), whose order ignoring case is the reverse of the ordinal one.
"""

from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

from GeometryHelper.Cad.Remote import CadRemoteException
from GeometryHelper.Dxf import DxfBlockTableRecord, DxfDimStyleTableRecord, DxfLine, DxfTextStyleTableRecord
from GeometryHelper.Geometry import GeoPoint3


def _names(items: Any) -> list[str]:
    return [str(item.Name) for item in items]


def _sorted_ignoring_case(names: list[str]) -> list[str]:
    return sorted(names, key=lambda name: (name.upper(), name))


def _styled_file(lines_database: Callable[[int], Any], path: Path) -> Path:
    """A drawing file with two text styles, two dimension styles, two blocks and an anonymous block ``*D``.

    Zeta is arial.ttf 0.9 wide, alpha romans.shx 0.75 wide; Wide has DIMTXT 5, DIMASZ 4, DIMSCALE 50, dense 2, 1.5, 1; the
    block Nut has one line (0, 0)-(5, 0) and bolt two, (0, 0)-(10, 0) and (0, 0)-(0, 10).
    """
    database = lines_database(0)
    for name, font, width in (("Zeta", "arial.ttf", 0.9), ("alpha", "romans.shx", 0.75)):
        style = DxfTextStyleTableRecord()
        style.Name = name
        style.FileName = font
        style.XScale = width
        database.TextStyleTable.Add(style)
    for name, text, arrow, scale in (("Wide", 5.0, 4.0, 50.0), ("dense", 2.0, 1.5, 1.0)):
        dim_style = DxfDimStyleTableRecord()
        dim_style.Name = name
        dim_style.Dimtxt = text
        dim_style.Dimasz = arrow
        dim_style.Dimscale = scale
        database.DimStyleTable.Add(dim_style)
    for name, ends in (("Nut", [(5, 0)]), ("bolt", [(10, 0), (0, 10)])):
        block = DxfBlockTableRecord()
        block.Name = name
        block.Origin = GeoPoint3(0, 0, 0)
        database.BlockTable.Add(block)
        for x, y in ends:
            block.AppendEntity(DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(x, y, 0)))
    anonymous = DxfBlockTableRecord()
    anonymous.Name = "*D"
    database.BlockTable.Add(anonymous)
    database.Save(str(path))
    return path


def test_a_drawing_with_nothing_unsaved_closes_and_leaves_the_list_of_the_app(app: Any) -> None:
    first = app.New()
    second = app.New()

    first.Close()

    assert [str(d.Name) for d in app.Documents] == ["Drawing2.dxf"]
    assert app.ActiveDocument.Id == second.Id


def test_a_call_on_a_closed_drawing_is_refused_with_the_no_longer_open_message(app: Any) -> None:
    closed = app.New()
    app.New()
    closed.Close()

    with pytest.raises(CadRemoteException) as raised:
        closed.GetObjects()

    assert str(raised.value.Message) == "The drawing Drawing1.dxf is no longer open."


def test_a_drawing_with_changes_is_refused_with_the_apps_message_and_stays_open(
    drawing: Any, line: Callable[..., Any]
) -> None:
    drawing.Insert(line(0, 0, 10, 0))
    drawing.CommitChanges()

    with pytest.raises(CadRemoteException) as raised:
        drawing.Close()

    assert str(raised.value.Message) == "Drawing1.dxf has changes not saved: save it, or close it with discard."
    assert drawing.GetObjects().Count == 1


def test_closing_with_discard_closes_a_drawing_with_changes_and_leaves_its_file_as_it_was(
    app: Any, lines_database: Callable[[int], Any], line: Callable[..., Any], tmp_path: Path
) -> None:
    path = tmp_path / "plan.dxf"
    lines_database(1).Save(str(path))
    before = path.read_bytes()
    opened = app.Open(str(path))
    opened.Insert(line(0, 100, 10, 100))
    opened.CommitChanges()

    opened.Close(True)

    assert app.Documents.Count == 0
    assert path.read_bytes() == before


# The keyword form is a .NET optional parameter called by name; whether pythonnet takes it on both runtimes is for the
# coder to confirm, so the test is marked and reports an unexpected pass instead of failing.
@pytest.mark.xfail(reason="Close(discard=True): keyword call of a .NET optional parameter, not confirmed", strict=False)
def test_closing_with_discard_by_keyword_closes_a_drawing_with_changes(
    app: Any, drawing: Any, line: Callable[..., Any]
) -> None:
    drawing.Insert(line(0, 0, 10, 0))
    drawing.CommitChanges()

    drawing.Close(discard=True)

    assert app.Documents.Count == 0


def test_closing_the_shown_drawing_shows_another_and_closing_the_last_leaves_none_active(app: Any) -> None:
    first = app.New()
    second = app.New()
    assert app.ActiveDocument.Id == second.Id

    second.Close()

    assert app.ActiveDocument.Id == first.Id

    first.Close()

    assert app.ActiveDocument is None
    assert app.Documents.Count == 0


def test_the_text_styles_dimension_styles_and_blocks_are_read_sorted_ignoring_case(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    opened = app.Open(str(_styled_file(lines_database, tmp_path / "styled.dxf")))

    text_styles = _names(opened.TextStyles)
    dim_styles = _names(opened.DimensionStyles)
    blocks = _names(opened.Blocks)

    assert text_styles == _sorted_ignoring_case(text_styles)
    assert text_styles.index("alpha") < text_styles.index("Zeta")
    assert dim_styles == _sorted_ignoring_case(dim_styles)
    assert dim_styles.index("dense") < dim_styles.index("Wide")
    assert blocks == ["bolt", "Nut"]


def test_the_styles_and_the_blocks_come_with_their_values(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    opened = app.Open(str(_styled_file(lines_database, tmp_path / "styled.dxf")))

    alpha = next(s for s in opened.TextStyles if str(s.Name) == "alpha")
    wide = next(s for s in opened.DimensionStyles if str(s.Name) == "Wide")
    bolt = next(b for b in opened.Blocks if str(b.Name) == "bolt")

    assert str(alpha.FileName) == "romans.shx"
    assert alpha.XScale == pytest.approx(0.75)
    assert (wide.Dimtxt, wide.Dimasz, wide.Dimscale) == (pytest.approx(5.0), pytest.approx(4.0), pytest.approx(50.0))
    assert sum(1 for _ in bolt) == 2


def test_a_changed_copy_changes_nothing_in_the_app(
    app: Any, lines_database: Callable[[int], Any], tmp_path: Path
) -> None:
    opened = app.Open(str(_styled_file(lines_database, tmp_path / "styled.dxf")))
    style = next(s for s in opened.TextStyles if str(s.Name) == "alpha")
    style.XScale = 0.5
    block = next(b for b in opened.Blocks if str(b.Name) == "bolt")
    block.AppendEntity(DxfLine(GeoPoint3(1, 1, 0), GeoPoint3(2, 2, 0)))

    again = next(s for s in opened.TextStyles if str(s.Name) == "alpha")
    block_again = next(b for b in opened.Blocks if str(b.Name) == "bolt")

    assert again.XScale == pytest.approx(0.75)
    assert sum(1 for _ in block_again) == 2
    assert not opened.IsModified
