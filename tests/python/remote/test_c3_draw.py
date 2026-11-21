"""C3: drawing from a script: a layer, three objects queued with tickets, one commit, the objects read back, one
modified, one deleted, and what one ``U`` takes back.

Every coordinate is an integer or a half, so the DXF text the objects travel as returns them exactly.
"""

from collections.abc import Callable
from typing import Any

from GeometryHelper.Cad.Remote import CadRemoteLayer
from GeometryHelper.Dxf import DxfCircle, DxfColor, DxfColorMethod, DxfLine, DxfMText
from GeometryHelper.Geometry import GeoPoint3, GeoVector3


def _point(point: Any) -> tuple[float, float, float]:
    return (point.X, point.Y, point.Z)


def _steel() -> Any:
    """The layer Steel, in AutoCAD colour 1 (red)."""
    layer = CadRemoteLayer("Steel")
    layer.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)
    return layer


def _on_steel(entity: Any) -> Any:
    entity.Layer = "Steel"
    return entity


def _three_on_steel(drawing: Any, line: Callable[..., Any]) -> list[str]:
    """A line (0, 0) to (100.5, 0), a circle of radius 25 about (50, 50) and an MText, on Steel; their tickets."""
    text = DxfMText()
    text.Contents = "Plate PL10"
    text.Location = GeoPoint3(10, 20, 0)
    text.TextHeight = 5.0
    return [
        drawing.Insert(_on_steel(line(0, 0, 100.5, 0))),
        drawing.Insert(_on_steel(DxfCircle(GeoPoint3(50, 50, 0), GeoVector3(0, 0, 1), 25))),
        drawing.Insert(_on_steel(text)),
    ]


def test_three_objects_inserted_get_the_tickets_one_two_and_three_and_are_not_in_the_drawing_yet(
    drawing: Any, line: Callable[..., Any]
) -> None:
    drawing.AddLayer(_steel())

    tickets = _three_on_steel(drawing, line)

    assert tickets == ["~1", "~2", "~3"]
    assert drawing.GetObjects().Count == 0
    assert [str(layer) for layer in drawing.Layers] == ["0"]


def test_a_commit_gives_one_handle_for_each_object_in_the_order_they_were_inserted(
    drawing: Any, line: Callable[..., Any]
) -> None:
    drawing.AddLayer(_steel())
    tickets = _three_on_steel(drawing, line)

    handles = list(drawing.CommitChanges())

    assert len(handles) == 3
    assert len(set(handles)) == 3
    assert [drawing.HandleOf(entity) for entity in drawing.GetObjects()] == handles
    assert [drawing.HandleOf(drawing.GetObject(ticket)) for ticket in tickets] == handles


def test_the_layer_and_the_objects_come_back_as_they_were_written(drawing: Any, line: Callable[..., Any]) -> None:
    drawing.AddLayer(_steel())
    tickets = _three_on_steel(drawing, line)

    drawing.CommitChanges()

    by_name = {str(layer.Name): layer for layer in drawing.Layers}
    assert sorted(by_name) == ["0", "Steel"]
    assert by_name["Steel"].Color.ColorIndex == 1
    made_line = drawing.GetObject(tickets[0])
    assert isinstance(made_line, DxfLine)
    assert (_point(made_line.StartPoint), _point(made_line.EndPoint)) == ((0, 0, 0), (100.5, 0, 0))
    circle = drawing.GetObject(tickets[1])
    assert isinstance(circle, DxfCircle)
    assert (_point(circle.Center), circle.Radius) == ((50, 50, 0), 25)
    text = drawing.GetObject(tickets[2])
    assert isinstance(text, DxfMText)
    assert text.Contents == "Plate PL10"
    assert [entity.Layer for entity in (made_line, circle, text)] == ["Steel", "Steel", "Steel"]


def test_an_object_read_and_modified_keeps_its_handle_and_the_drawing_has_the_new_value(
    drawing: Any, line: Callable[..., Any]
) -> None:
    drawing.Insert(line(0, 0, 100, 0))
    handle = drawing.CommitChanges()[0]
    read = drawing.GetObject(handle)
    read.EndPoint = GeoPoint3(200.5, 0, 0)

    drawing.Modify(read)
    new_handles = list(drawing.CommitChanges())

    assert new_handles == []
    assert drawing.GetObjects().Count == 1
    assert drawing.HandleOf(drawing.GetObject(handle)) == handle
    assert _point(drawing.GetObject(handle).EndPoint) == (200.5, 0, 0)


def test_an_object_deleted_by_its_handle_is_gone_after_the_commit(drawing: Any, line: Callable[..., Any]) -> None:
    drawing.Insert(line(0, 0, 100, 0))
    drawing.Insert(line(0, 10, 100, 10))
    first, second = drawing.CommitChanges()

    drawing.Delete(first)
    assert drawing.GetObjects().Count == 2
    drawing.CommitChanges()

    assert drawing.GetObject(first) is None
    assert [drawing.HandleOf(entity) for entity in drawing.GetObjects()] == [second]


def test_one_u_takes_the_whole_commit_back_the_layer_too(drawing: Any, line: Callable[..., Any]) -> None:
    drawing.AddLayer(_steel())
    _three_on_steel(drawing, line)
    drawing.CommitChanges()
    assert drawing.GetObjects().Count == 3

    result = drawing.SendCommand("U\n")

    assert result.Completed
    assert drawing.GetObjects().Count == 0
    assert [str(layer) for layer in drawing.Layers] == ["0"]


def test_two_commits_are_two_undo_steps_and_each_u_takes_one_back(drawing: Any, line: Callable[..., Any]) -> None:
    drawing.Insert(line(0, 0, 10, 0))
    drawing.Insert(line(0, 5, 10, 5))
    drawing.CommitChanges()
    drawing.Insert(line(0, 9, 10, 9))
    drawing.CommitChanges()

    drawing.SendCommand("U\n")
    assert drawing.GetObjects().Count == 2

    drawing.SendCommand("U\n")
    assert drawing.GetObjects().Count == 0


def test_words_and_a_layer_name_with_accents_come_back_unchanged(drawing: Any) -> None:
    # The words hold an accented Latin letter (U+00E9), the diameter sign (U+00D8), an en dash (U+2013) and a Vietnamese
    # letter with two marks (U+1EA5), written as escapes so that no editor can change the file's bytes.
    e_acute = "\N{LATIN SMALL LETTER E WITH ACUTE}"
    diameter = "\N{LATIN CAPITAL LETTER O WITH STROKE}"
    en_dash = "\N{EN DASH}"
    a_marks = "\N{LATIN SMALL LETTER A WITH CIRCUMFLEX AND ACUTE}"
    words = f"Th{e_acute}p {diameter}12 {en_dash} t{a_marks}m"
    layer = f"Th{e_acute}p"
    text = DxfMText()
    text.Contents = words
    text.Location = GeoPoint3(1, 2, 0)
    text.TextHeight = 2.5
    text.Layer = layer

    ticket = drawing.Insert(text)
    drawing.CommitChanges()

    back = drawing.GetObject(ticket)
    assert back.Contents == words
    assert back.Layer == layer
    assert sorted(str(known.Name) for known in drawing.Layers) == ["0", layer]


def test_five_hundred_lines_are_one_commit_and_one_u_takes_them_all(drawing: Any, line: Callable[..., Any]) -> None:
    for i in range(500):
        drawing.Insert(line(i, 0, i, 10))

    handles = list(drawing.CommitChanges())

    assert len(set(handles)) == 500
    assert drawing.GetObjects().Count == 500
    drawing.SendCommand("U\n")
    assert drawing.GetObjects().Count == 0
