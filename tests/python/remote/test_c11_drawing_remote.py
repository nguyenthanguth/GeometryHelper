"""C11: the Drawing API from outside the app (D1642, D1655, D1656, D1609): the scenarios of ``drawing_scenarios`` through an
``App`` on the test server give the results they give on the other backends; ``App.documents`` lists and finds the drawings
of the app; a drawing that is not the active one is edited; ``close(discard=True)`` closes a drawing with changes.

``connect()`` finds the app in the folder of the real one, which a test never touches, so the tests make the ``App`` over
the ``CadApplication`` the ``app`` fixture connected to the test server (the folder of the test).
"""

from collections.abc import Iterator
from pathlib import Path
from typing import Any

import pytest

import drawing_scenarios
from drawing_scenarios import BLANK, SCENARIOS, Scenario
from GeometryHelper.Cad.Remote import CadRemoteException


@pytest.fixture
def cad_app(app: Any) -> Iterator[Any]:
    """The ``geometryhelper.cad`` App over this test's connection."""
    from geometryhelper.cad import App

    with App(app) as made:
        yield made


def _drawing_for(scenario: Scenario, cad_app: Any, tmp_path: Path) -> Any:
    if scenario.drawing == BLANK:
        return cad_app.new()
    path = tmp_path / "plan.dxf"
    drawing_scenarios.write_file(str(path))
    return cad_app.open(str(path))


@pytest.mark.parametrize("scenario", SCENARIOS, ids=lambda scenario: scenario.name)
def test_a_scenario_through_the_app_gives_the_result_every_backend_gives(
    scenario: Scenario, cad_app: Any, tmp_path: Path
) -> None:
    drawing = _drawing_for(scenario, cad_app, tmp_path)

    # One U of the command line takes back the last step, as a user types it.
    drawing_scenarios.check(scenario, drawing, lambda: drawing.net.SendCommand("U\n"))


def test_the_documents_of_the_app_are_listed_in_its_order_and_found_by_name_or_by_path(
    cad_app: Any, tmp_path: Path
) -> None:
    path = tmp_path / "plan.dxf"
    drawing_scenarios.write_file(str(path))
    cad_app.new()
    cad_app.new()
    cad_app.open(str(path))

    documents = cad_app.documents

    assert [drawing.name for drawing in documents] == ["Drawing1.dxf", "Drawing2.dxf", "plan.dxf"]
    assert len(documents) == 3
    assert documents["Drawing2.dxf"].name == "Drawing2.dxf"
    assert Path(documents[str(path)].path).resolve() == path.resolve()
    assert documents["Drawing1.dxf"].path is None
    assert [drawing.is_active for drawing in documents] == [False, False, True]
    assert cad_app.active_drawing.name == "plan.dxf"


def test_a_document_that_is_not_there_is_a_key_error_that_names_the_key(cad_app: Any) -> None:
    cad_app.new()

    with pytest.raises(KeyError, match="Nope.dxf"):
        cad_app.documents["Nope.dxf"]


def test_a_drawing_that_is_not_the_active_one_is_edited_and_the_active_one_is_left_alone(cad_app: Any) -> None:
    first = cad_app.new()
    second = cad_app.new()
    assert second.is_active

    first.add_line((0, 0), (10, 0))
    first.add_circle((5, 5), 2)
    first.commit()

    assert [entity.dxf_type for entity in first.entities()] == ["LINE", "CIRCLE"]
    assert second.entities() == []
    assert first.is_modified
    assert not second.is_modified


def test_closing_a_drawing_with_changes_is_refused_until_discard_is_given(cad_app: Any) -> None:
    drawing = cad_app.new()
    drawing.add_line((0, 0), (10, 0))
    drawing.commit()

    with pytest.raises(CadRemoteException):
        drawing.close()

    assert len(cad_app.documents) == 1

    drawing.close(discard=True)

    assert len(cad_app.documents) == 0


def test_closing_a_drawing_with_nothing_unsaved_leaves_the_others_open(cad_app: Any) -> None:
    first = cad_app.new()
    cad_app.new()

    first.close()

    assert [drawing.name for drawing in cad_app.documents] == ["Drawing2.dxf"]
