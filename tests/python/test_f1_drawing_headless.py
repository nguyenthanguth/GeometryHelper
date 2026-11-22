"""F1: the Drawing API on a file, with no app (D1650, D1655, D1656, D1642): ``open_file`` and ``new_file`` give a ``Drawing``
over a ``CadDocument``; the scenarios of ``drawing_scenarios`` give the results they give on the other backends; the picks
raise ``RuntimeError`` because nobody is there to ask; ``save_as`` then ``open_file`` reads back the same objects; and a
function that needs the app says so in the words of D1650. Every file is written in ``tmp_path``.
"""

from collections.abc import Callable
from pathlib import Path
from typing import Any

import pytest

import drawing_scenarios
from drawing_scenarios import BLANK, SCENARIOS, Scenario
from geometryhelper.cad import new_file, open_file


def _drawing_for(scenario: Scenario, tmp_path: Path) -> Any:
    if scenario.drawing == BLANK:
        return new_file()
    path = tmp_path / "plan.dxf"
    drawing_scenarios.write_file(str(path))
    return open_file(str(path))


@pytest.mark.parametrize("scenario", SCENARIOS, ids=lambda scenario: scenario.name)
def test_a_scenario_on_a_file_gives_the_result_every_backend_gives(scenario: Scenario, tmp_path: Path) -> None:
    drawing = _drawing_for(scenario, tmp_path)

    drawing_scenarios.check(scenario, drawing, drawing.net.Undo)


def test_a_new_file_has_no_path_and_no_entity_and_two_of_them_are_two_drawings() -> None:
    first = new_file()
    second = new_file()

    first.add_line((0, 0), (10, 0))
    first.commit()

    assert first.path is None
    assert len(first.entities()) == 1
    assert second.entities() == []


@pytest.mark.parametrize("pick", ["pick_point", "pick_object", "pick_objects"])
def test_a_pick_on_a_file_raises_a_runtime_error_because_nobody_can_be_asked(pick: str) -> None:
    drawing = new_file()

    with pytest.raises(RuntimeError):
        getattr(drawing, pick)("Pick")


def test_a_drawing_saved_as_and_opened_again_reads_back_the_same_objects(tmp_path: Path) -> None:
    drawing = new_file()
    drawing.layers.add("Steel", color=1)
    drawing.commit()
    drawing.add_line((0, 0), (100.5, 0), layer="Steel")
    drawing.add_circle((50, 50), 25)
    drawing.commit()
    path = tmp_path / "saved.dxf"

    drawing.save_as(str(path))
    again = open_file(str(path))

    def shape(read: Any) -> list[tuple[str, str, int]]:
        return sorted((entity.dxf_type, entity.layer, entity.color) for entity in read.entities())

    assert Path(drawing.path).resolve() == path.resolve()
    assert Path(again.path).resolve() == path.resolve()
    assert shape(again) == shape(drawing) == [("CIRCLE", "0", 256), ("LINE", "Steel", 256)]
    line = next(entity for entity in again.entities() if entity.dxf_type == "LINE")
    assert (line.net.EndPoint.X, line.net.EndPoint.Y) == (pytest.approx(100.5), pytest.approx(0.0))
    assert [str(layer.name) for layer in again.layers if str(layer.name) == "Steel"] == ["Steel"]


def _needs_the_app() -> list[tuple[str, Callable[[], object]]]:
    import geometryhelper.cad as cad

    return [
        ("get_point", lambda: cad.get_point("Where")),
        ("get_distance", lambda: cad.get_distance("How far")),
        ("get_angle", lambda: cad.get_angle("Which way")),
        ("get_integer", lambda: cad.get_integer("How many")),
        ("get_real", lambda: cad.get_real("How much")),
        ("get_string", lambda: cad.get_string("Name")),
        ("get_keyword", lambda: cad.get_keyword("Which", ["One", "Two"])),
        ("get_entity", lambda: cad.get_entity("Pick")),
        ("get_selection", lambda: cad.get_selection()),
        ("cmd", lambda: cad.cmd("CIRCLE", (0, 0), 5)),
        ("active_drawing", lambda: cad.active_drawing()),
        ("documents", lambda: cad.documents()),
        ("add_ribbon_panel", lambda: cad.add_ribbon_panel(cad.ribbon_panel("Tools", []))),
        ("show_command_buttons", lambda: cad.show_command_buttons(False)),
    ]


@pytest.mark.parametrize("name", [name for name, _ in _needs_the_app()])
def test_a_function_that_needs_the_app_says_so_in_the_words_of_the_spec(name: str) -> None:
    call = dict(_needs_the_app())[name]

    with pytest.raises(RuntimeError) as raised:
        call()

    assert str(raised.value) == f"{name} works inside GeometryHelper CAD only"
