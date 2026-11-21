"""A8: ``geometryhelper.undo_group`` (D607): BeginGroup on entry, EndGroup on a normal end, CancelGroup when the block
raises. A group left open makes ``SaveAs`` throw "A command is running", which is why the helper exists."""

from collections.abc import Iterator
from pathlib import Path

import pytest

import geometryhelper
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dxf import DxfDatabase, DxfWriter


@pytest.fixture
def document() -> CadDocument:
    return CadDocument.From(DxfDatabase.Parse(DxfWriter().ToString()), "undo.dxf")


def _circle(document: CadDocument, x: float) -> None:
    """A circle of radius 5 about (x, 0), by the command line."""
    session = CadCommandSession(document)
    for answer in ("CIRCLE", f"{x},0", "5"):
        session.Feed(answer)


def _steps(document: CadDocument) -> Iterator[int]:
    """Undoes until nothing is left, yielding the entity count after each step."""
    while document.Undo():
        yield document.EntityCount


def test_creating_the_context_manager_opens_no_group_until_it_is_entered(document: CadDocument) -> None:
    context = geometryhelper.undo_group(document, "later")
    assert not document.IsInGroup
    with context:
        assert document.IsInGroup
    assert not document.IsInGroup


def test_the_block_gets_the_document_back_as_its_target(document: CadDocument) -> None:
    with geometryhelper.undo_group(document, "same") as target:
        assert target is document


def test_two_circles_drawn_in_a_group_are_one_undo_step(document: CadDocument) -> None:
    with geometryhelper.undo_group(document, "two circles"):
        _circle(document, 0)
        _circle(document, 20)
    assert document.EntityCount == 2
    assert not document.IsInGroup
    assert str(document.NextUndo.Name) == "two circles"
    assert list(_steps(document)) == [0]
    assert not document.CanUndo


def test_two_groups_one_after_the_other_are_two_undo_steps(document: CadDocument) -> None:
    with geometryhelper.undo_group(document, "first"):
        _circle(document, 0)
    with geometryhelper.undo_group(document, "second"):
        _circle(document, 20)
        _circle(document, 40)
    assert document.EntityCount == 3
    assert list(_steps(document)) == [1, 0]


def test_a_block_that_raises_leaves_nothing_and_passes_its_own_exception_on(document: CadDocument) -> None:
    with pytest.raises(ValueError, match="boom"):
        with geometryhelper.undo_group(document, "failing"):
            _circle(document, 0)
            _circle(document, 20)
            assert document.EntityCount == 2
            raise ValueError("boom")
    assert document.EntityCount == 0
    assert not document.IsInGroup
    assert not document.CanUndo


def test_a_dotnet_exception_inside_the_block_cancels_the_group_and_goes_on(
    document: CadDocument, tmp_path: Path
) -> None:
    # CadDocument.Open of a file that is not there (in a folder that is) throws a System.IO.FileNotFoundException after
    # one circle is drawn.
    with pytest.raises(Exception) as raised:
        with geometryhelper.undo_group(document, "failing"):
            _circle(document, 0)
            CadDocument.Open(str(tmp_path / "missing.dxf"))
    assert type(raised.value).__name__ == "FileNotFoundException"
    assert document.EntityCount == 0
    assert not document.IsInGroup


def test_save_as_works_after_a_normal_block(document: CadDocument, tmp_path: Path) -> None:
    with geometryhelper.undo_group(document, "one"):
        _circle(document, 0)
    path = tmp_path / "after-normal.dxf"
    document.SaveAs(str(path))
    assert CadDocument.Open(str(path)).EntityCount == 1


def test_save_as_works_after_a_block_that_raised(document: CadDocument, tmp_path: Path) -> None:
    with pytest.raises(RuntimeError):
        with geometryhelper.undo_group(document, "failing"):
            _circle(document, 0)
            raise RuntimeError("stop")
    path = tmp_path / "after-raise.dxf"
    document.SaveAs(str(path))
    assert CadDocument.Open(str(path)).EntityCount == 0


def test_a_nested_group_is_one_undo_step(document: CadDocument) -> None:
    # CadDocument counts the depth: the inner EndGroup closes nothing, the outer one makes the single step.
    with geometryhelper.undo_group(document, "outer"):
        _circle(document, 0)
        with geometryhelper.undo_group(document, "inner"):
            _circle(document, 20)
        assert document.IsInGroup
        _circle(document, 40)
    assert document.EntityCount == 3
    assert list(_steps(document)) == [0]


def test_a_failing_inner_block_passes_the_original_exception_through_the_outer_block(document: CadDocument) -> None:
    # CadDocument.CancelGroup of the inner block cancels the whole group (the outer circle with it) and closes it. The
    # outer block then finds no group open: its own CancelGroup would throw "No group of edits is open" over the
    # ValueError, so the helper must look before it ends or cancels.
    with pytest.raises(ValueError, match="inner boom"):
        with geometryhelper.undo_group(document, "outer"):
            _circle(document, 0)
            with geometryhelper.undo_group(document, "inner"):
                _circle(document, 20)
                raise ValueError("inner boom")
    assert document.EntityCount == 0
    assert not document.IsInGroup


def test_the_same_document_edits_again_after_a_cancelled_group(document: CadDocument) -> None:
    with pytest.raises(ValueError):
        with geometryhelper.undo_group(document, "failing"):
            _circle(document, 0)
            raise ValueError("first")
    with geometryhelper.undo_group(document, "good"):
        _circle(document, 20)
    assert document.EntityCount == 1
    assert list(_steps(document)) == [0]


def test_a_group_is_ended_when_the_block_leaves_by_return(document: CadDocument) -> None:
    def draw() -> int:
        with geometryhelper.undo_group(document, "returning"):
            _circle(document, 0)
            return document.EntityCount

    assert draw() == 1
    assert not document.IsInGroup
    assert list(_steps(document)) == [0]


def test_a_block_that_does_nothing_leaves_no_undo_step(document: CadDocument) -> None:
    with geometryhelper.undo_group(document, "empty"):
        pass
    assert not document.IsInGroup
    assert document.EntityCount == 0
    assert not document.CanUndo

