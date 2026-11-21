"""C4: reading a drawing: every object in the order it was made, filtered by kind, layer and layout, each as an
instance of its own class of ``GeometryHelper.Dxf``, with the handle the app gave it.

The drawing holds four objects, made in this order: a line on Steel, a line on Concrete, a circle on Steel and an MText
on Concrete. ``Kind`` is set from a Python class, which pythonnet passes as the ``System.Type`` of the .NET class
(D805.3), and from the ``System.Type`` itself.
"""

from collections.abc import Callable
from typing import Any

import clr
import pytest

from GeometryHelper.Cad.Remote import CadObjectFilter, CadRemoteException
from GeometryHelper.Dxf import DxfCircle, DxfLine, DxfMText
from GeometryHelper.Geometry import GeoPoint3, GeoVector3


@pytest.fixture
def four(drawing: Any, line: Callable[..., Any]) -> tuple[Any, list[str]]:
    """The drawing of four objects and their handles, in the order they were made."""
    circle = DxfCircle(GeoPoint3(50, 50, 0), GeoVector3(0, 0, 1), 25)
    circle.Layer = "Steel"
    text = DxfMText()
    text.Contents = "Plate PL10"
    text.Location = GeoPoint3(10, 20, 0)
    text.TextHeight = 5.0
    text.Layer = "Concrete"
    for entity in (line(0, 0, 100, 0, "Steel"), line(0, 10, 100, 10, "Concrete"), circle, text):
        drawing.Insert(entity)
    return drawing, list(drawing.CommitChanges())


def _handles(drawing: Any, objects: Any) -> list[str]:
    return [drawing.HandleOf(entity) for entity in objects]


def test_every_object_comes_back_in_the_order_it_was_made_as_an_instance_of_its_own_class(
    four: tuple[Any, list[str]],
) -> None:
    drawing, handles = four

    objects = drawing.GetObjects()

    assert objects.Count == 4
    assert [type(entity) for entity in objects] == [DxfLine, DxfLine, DxfCircle, DxfMText]
    assert _handles(drawing, objects) == handles


def test_a_filter_with_no_member_set_gives_every_object(four: tuple[Any, list[str]]) -> None:
    drawing, handles = four

    assert _handles(drawing, drawing.GetObjects(CadObjectFilter())) == handles


def test_a_kind_set_from_a_python_class_finds_the_objects_of_that_class(four: tuple[Any, list[str]]) -> None:
    drawing, handles = four
    by_kind = CadObjectFilter()
    by_kind.Kind = DxfLine

    found = drawing.GetObjects(by_kind)

    assert _handles(drawing, found) == handles[:2]
    assert [type(entity) for entity in found] == [DxfLine, DxfLine]


def test_a_kind_set_from_the_dotnet_type_finds_the_objects_of_that_type(four: tuple[Any, list[str]]) -> None:
    drawing, handles = four
    by_kind = CadObjectFilter()
    by_kind.Kind = clr.GetClrType(DxfCircle)

    assert _handles(drawing, drawing.GetObjects(by_kind)) == handles[2:3]


def test_the_kind_a_filter_was_given_is_read_back_as_the_dotnet_type(four: tuple[Any, list[str]]) -> None:
    by_kind = CadObjectFilter()
    assert by_kind.Kind is None

    by_kind.Kind = DxfMText

    assert by_kind.Kind.FullName == "GeometryHelper.Dxf.DxfMText"


def test_a_layer_in_the_filter_finds_the_objects_on_it_and_an_unknown_layer_finds_none(
    four: tuple[Any, list[str]],
) -> None:
    drawing, handles = four
    on_steel = CadObjectFilter()
    on_steel.Layer = "Steel"
    on_nothing = CadObjectFilter()
    on_nothing.Layer = "Glass"

    assert _handles(drawing, drawing.GetObjects(on_steel)) == [handles[0], handles[2]]
    assert drawing.GetObjects(on_nothing).Count == 0


def test_a_kind_and_a_layer_together_find_the_objects_that_are_both(four: tuple[Any, list[str]]) -> None:
    drawing, handles = four
    both = CadObjectFilter()
    both.Kind = DxfLine
    both.Layer = "Concrete"

    assert _handles(drawing, drawing.GetObjects(both)) == [handles[1]]


def test_the_layout_model_gives_all_four_and_a_layout_the_drawing_has_not_is_refused(
    four: tuple[Any, list[str]],
) -> None:
    drawing, handles = four
    in_model = CadObjectFilter()
    in_model.Layout = "Model"
    in_sheet = CadObjectFilter()
    in_sheet.Layout = "Sheet9"

    assert _handles(drawing, drawing.GetObjects(in_model)) == handles
    with pytest.raises(CadRemoteException, match="has no layout named Sheet9"):
        drawing.GetObjects(in_sheet)


def test_an_object_asked_for_by_its_handle_is_the_one_with_that_handle(four: tuple[Any, list[str]]) -> None:
    drawing, handles = four

    circle = drawing.GetObject(handles[2])

    assert isinstance(circle, DxfCircle)
    assert drawing.HandleOf(circle) == handles[2]
    assert circle.Radius == 25
