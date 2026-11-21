"""C5: selecting from a script. ``Select`` takes an ``IEnumerable<string>``, and pythonnet converts a .NET array or a
.NET ``List[String]`` for it but not a Python list: the TypeError the guide states is pinned here.

The drawing holds three lines, committed in one step, so their handles are in the order they were made.
"""

from collections.abc import Callable
from typing import Any

import pytest
from System import Array, String
from System.Collections.Generic import List


@pytest.fixture
def three(drawing: Any, line: Callable[..., Any]) -> tuple[Any, list[str]]:
    """The drawing of three lines at y = 0, 10 and 20, and their handles in the order they were made."""
    for y in (0, 10, 20):
        drawing.Insert(line(0, y, 100, y))
    return drawing, list(drawing.CommitChanges())


def _selected(drawing: Any) -> list[str]:
    return [drawing.HandleOf(entity) for entity in drawing.GetSelectedObjects()]


def test_nothing_is_selected_in_a_new_drawing(three: tuple[Any, list[str]]) -> None:
    drawing, _ = three

    assert drawing.GetSelectedObjects().Count == 0


def test_handles_in_a_dotnet_array_are_selected_and_read_back_in_the_order_of_the_handles(
    three: tuple[Any, list[str]],
) -> None:
    drawing, handles = three

    drawing.Select(Array[String]([handles[2], handles[0]]))

    assert _selected(drawing) == [handles[0], handles[2]]


def test_handles_in_a_dotnet_list_are_selected(three: tuple[Any, list[str]]) -> None:
    drawing, handles = three

    chosen = List[String]()  # the constructor of a .NET list takes no Python list
    chosen.Add(handles[1])
    chosen.Add(handles[2])

    drawing.Select(chosen)

    assert _selected(drawing) == [handles[1], handles[2]]


def test_a_ticket_of_an_object_committed_is_selected_as_its_handle(drawing: Any, line: Callable[..., Any]) -> None:
    ticket = drawing.Insert(line(0, 0, 100, 0))
    handle = drawing.CommitChanges()[0]

    drawing.Select(Array[String]([ticket]))

    assert _selected(drawing) == [handle]


def test_a_python_list_is_refused_with_a_type_error_and_the_selection_is_left_as_it_was(
    three: tuple[Any, list[str]],
) -> None:
    drawing, handles = three
    drawing.Select(Array[String]([handles[0]]))

    with pytest.raises(TypeError):
        drawing.Select([handles[1]])

    assert _selected(drawing) == [handles[0]]


def test_a_second_selection_replaces_the_first(three: tuple[Any, list[str]]) -> None:
    drawing, handles = three
    drawing.Select(Array[String]([handles[0], handles[1]]))

    drawing.Select(Array[String]([handles[2]]))

    assert _selected(drawing) == [handles[2]]


def test_an_empty_array_clears_the_selection(three: tuple[Any, list[str]]) -> None:
    drawing, handles = three
    drawing.Select(Array[String](handles))
    assert drawing.GetSelectedObjects().Count == 3

    drawing.Select(Array[String]([]))

    assert drawing.GetSelectedObjects().Count == 0
