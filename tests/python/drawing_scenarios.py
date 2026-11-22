"""The scenarios of the Drawing API (D1642): one list, run on every backend, so that a script written for one runs on the
others with the same results.

A scenario is a function of a ``Drawing`` that returns what it checks, and the value it must return. The headless runner
(``test_f1_drawing_headless.py``), the remote runner (``remote/test_c11_drawing_remote.py``) and the in-app runner
(``DrawingApiTests.cs``, which runs ``check`` inside the app) take each scenario with a drawing of its own: ``BLANK`` is a
new drawing with nothing in it, ``FILE`` is the drawing ``write_file`` writes, opened from its file. A scenario with
``needs_undo`` is also given ``undo``, which takes back the last step of the drawing the way the backend does (the headless
runner calls ``CadDocument.Undo``, the remote runner types ``U``); the app has no such function from inside a run, so the
C# tests take those steps back themselves.

Every coordinate is an integer or a half, so a drawing that travels as DXF text returns it exactly; angles and other
floats are rounded to six places.
"""

from __future__ import annotations

import math
from collections.abc import Callable
from dataclasses import dataclass
from typing import Any

from GeometryHelper.Dxf import (
    DxfBlockTableRecord,
    DxfDatabase,
    DxfDimStyleTableRecord,
    DxfLine,
    DxfTextStyleTableRecord,
    DxfWriter,
)
from GeometryHelper.Geometry import GeoPoint2, GeoPoint3

BLANK = "blank"
FILE = "file"


@dataclass(frozen=True)
class Scenario:
    """One case: the kind of drawing it needs, what it does to it, and what it must come to."""

    name: str
    drawing: str
    run: Callable[..., object]
    expected: object
    needs_undo: bool = False


def write_file(path: str) -> None:
    """Write the drawing of ``FILE``: a line (0, 0) to (10, 0); text styles ``Zeta`` and ``alpha``; dimension styles
    ``Wide`` and ``dense``; blocks ``Nut`` (one line) and ``bolt`` (two) and an anonymous block ``*D``.

    The order of each pair ignoring case is the reverse of the ordinal one, so that a sort by ordinal fails.
    """
    database = DxfDatabase.Parse(DxfWriter().ToString())
    database.ModelSpace.AppendEntity(DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(10, 0, 0)))
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
    database.Save(path)


def _r(value: Any) -> float:
    return round(float(value), 6)


def _p(point: Any) -> tuple[float, float, float]:
    return (_r(point.X), _r(point.Y), _r(point.Z))


def _xy(point: Any) -> tuple[float, float]:
    return (_r(point.X), _r(point.Y))


def _layer_names(drawing: Any) -> list[str]:
    return sorted(str(layer.name) for layer in drawing.layers)


# ---- Adding ----------------------------------------------------------------------------------------------------------


def _a_line(d: Any) -> object:
    d.add_line((0, 0), (100.5, 0))
    handles = d.commit()
    entities = d.entities()
    line = entities[0]
    return (
        len(handles),
        len(entities),
        list(handles) == [e.handle for e in entities],
        line.dxf_type,
        _p(line.net.StartPoint),
        _p(line.net.EndPoint),
        line.layer,
        line.color,
    )


def _a_circle(d: Any) -> object:
    d.add_circle((50, 50), 25)
    d.commit()
    entities = d.entities()
    circle = entities[0]
    return (len(entities), circle.dxf_type, _p(circle.net.Center), _r(circle.net.Radius))


def _an_arc_in_radians(d: Any) -> object:
    d.add_arc((0, 0), 10, 0.0, math.pi / 2)
    d.commit()
    arc = d.entities()[0]
    return (arc.dxf_type, _p(arc.net.Center), _r(arc.net.Radius), _r(arc.net.StartAngle), _r(arc.net.EndAngle))


def _a_closed_polyline(d: Any) -> object:
    d.add_polyline([(0, 0), (100, 0), (100, 50)], closed=True)
    d.commit()
    polyline = d.entities()[0]
    net = polyline.net
    return (
        polyline.dxf_type,
        [_xy(net.GetPoint2dAt(i)) for i in range(net.NumberOfVertices)],
        bool(net.Closed),
    )


def _points_of_every_kind(d: Any) -> object:
    d.add_line(GeoPoint2(1, 2), GeoPoint3(3, 4, 0))
    d.add_line((5, 6), (7, 8, 0))
    d.commit()
    return sorted((_p(e.net.StartPoint), _p(e.net.EndPoint)) for e in d.entities())


def _a_text(d: Any) -> object:
    d.add_text("Plate", (0, 100), 5.0)
    d.commit()
    text = d.entities()[0]
    return (text.dxf_type, str(text.net.TextString), _p(text.net.Position), _r(text.net.Height))


def _an_mtext_with_layer_and_color(d: Any) -> object:
    d.layers.add("Steel", color=1)
    d.commit()
    d.add_mtext("Plate PL10", (10, 20), 5.0, layer="Steel", color=1)
    d.commit()
    text = d.entities()[0]
    return (
        text.dxf_type,
        str(text.net.Contents),
        _p(text.net.Location),
        _r(text.net.TextHeight),
        text.layer,
        text.color,
    )


def _nothing_is_applied_before_the_commit(d: Any) -> object:
    d.add_line((0, 0), (10, 0))
    d.add_circle((0, 0), 5)
    before = len(d.entities())
    handles = d.commit()
    return (before, len(handles), len(d.entities()))


def _a_second_commit_with_nothing_queued(d: Any) -> object:
    d.add_line((0, 0), (10, 0))
    d.commit()
    return list(d.commit())


# ---- Reading ---------------------------------------------------------------------------------------------------------


def _the_filters_of_entities(d: Any) -> object:
    d.layers.add("Steel", color=1)
    d.commit()
    d.add_line((0, 0), (10, 0))
    d.add_line((0, 5), (10, 5), layer="Steel")
    d.add_circle((0, 0), 5, layer="Steel")
    d.commit()
    return (
        len(d.entities()),
        len(d.entities(type="line")),
        len(d.entities(layer="Steel")),
        len(d.entities(type="CIRCLE", layer="Steel")),
        len(d.entities(type="ARC")),
    )


def _an_entity_by_handle(d: Any) -> object:
    d.add_line((0, 0), (10, 0))
    handle = d.commit()[0]
    found = d.entity(handle)
    try:
        d.entity("ZZZZ")
        missing = False
    except KeyError:
        missing = True
    return (found.handle == handle, found.dxf_type, missing)


def _a_selection(d: Any) -> object:
    d.add_line((0, 0), (10, 0))
    d.add_line((0, 5), (10, 5))
    d.add_line((0, 9), (10, 9))
    first, second, _ = d.commit()
    d.select([d.entity(first), second])
    return sorted(e.handle for e in d.selection()) == sorted([first, second])


# ---- Changing --------------------------------------------------------------------------------------------------------


def _layer_and_color_set_on_an_entity(d: Any) -> object:
    d.layers.add("Steel", color=3)
    d.add_line((0, 0), (10, 0))
    d.commit()
    line = d.entities()[0]
    line.layer = "Steel"
    line.color = 1
    d.commit()
    again = d.entities()[0]
    return (line.handle == again.handle, again.layer, again.color, len(d.entities()))


def _a_changed_net_and_modify(d: Any) -> object:
    d.add_line((0, 0), (100, 0))
    d.commit()
    line = d.entities()[0]
    net = line.net
    net.EndPoint = GeoPoint3(200.5, 0, 0)
    d.modify(line)
    d.commit()
    again = d.entities()[0]
    return (again.handle == line.handle, _p(again.net.EndPoint), len(d.entities()))


def _a_changed_net_without_modify(d: Any) -> object:
    d.add_line((0, 0), (100, 0))
    d.commit()
    net = d.entities()[0].net
    net.EndPoint = GeoPoint3(999, 0, 0)
    d.commit()
    return _p(d.entities()[0].net.EndPoint)


def _erase_by_entity_and_by_handle(d: Any) -> object:
    d.add_line((0, 0), (10, 0))
    d.add_line((0, 5), (10, 5))
    d.add_line((0, 9), (10, 9))
    d.commit()
    entities = d.entities()
    d.erase(entities[0])
    d.erase(entities[1].handle)
    before = len(d.entities())
    d.commit()
    left = d.entities()
    return (before, len(left), left[0].handle == entities[2].handle)


# ---- Layers ----------------------------------------------------------------------------------------------------------


def _layers_added_and_changed(d: Any) -> object:
    d.layers.add("Steel", color=1, description="Beams")
    d.commit()
    steel = d.layers["Steel"]
    first = (
        steel.name,
        steel.color,
        steel.description,
        steel.is_on,
        steel.is_frozen,
        steel.is_locked,
        steel.is_plottable,
    )
    steel.is_on = False
    steel.color = 5
    steel.is_locked = True
    d.commit()
    again = d.layers["steel"]
    return (first, (again.name, again.color, again.is_on, again.is_locked), _layer_names(d))


def _a_layer_found_ignoring_case_and_a_missing_one(d: Any) -> object:
    d.layers.add("Steel")
    d.commit()
    found = d.layers["STEEL"].name
    try:
        d.layers["Nope"]
        missing = False
    except KeyError:
        missing = True
    return (found, missing, "steel" in d.layers, "Nope" in d.layers, len(d.layers))


def _the_current_layer(d: Any) -> object:
    before = d.current_layer
    d.layers.add("Steel")
    d.commit()
    d.current_layer = "Steel"
    d.commit()
    return (before, d.current_layer)


# ---- Styles, blocks and the file -------------------------------------------------------------------------------------


def _styles_and_blocks_read_in_order(d: Any) -> object:
    text = [str(s.Name) for s in d.text_styles if str(s.Name) in ("Zeta", "alpha")]
    dimension = [str(s.Name) for s in d.dimension_styles if str(s.Name) in ("Wide", "dense")]
    blocks = [str(b.Name) for b in d.blocks]
    bolt = next(b for b in d.blocks if str(b.Name) == "bolt")
    return (text, dimension, blocks, sum(1 for _ in bolt), d.is_modified)


def _a_changed_style_copy_changes_nothing(d: Any) -> object:
    style = next(s for s in d.text_styles if str(s.Name) == "alpha")
    style.XScale = 0.5
    again = next(s for s in d.text_styles if str(s.Name) == "alpha")
    return (_r(again.XScale), d.is_modified)


def _save_commits_first(d: Any) -> object:
    d.add_line((0, 5), (10, 5))
    d.save()
    saved = DxfDatabase.Open(d.path)
    return (sum(1 for _ in saved.ModelSpace), d.is_modified)


# ---- Undo ------------------------------------------------------------------------------------------------------------


def _one_commit_is_one_undo_step(d: Any, undo: Callable[[], None]) -> object:
    d.layers.add("Steel")
    d.add_line((0, 0), (10, 0))
    d.add_circle((0, 0), 5)
    d.commit()
    undo()
    return (len(d.entities()), _layer_names(d))


def _two_commits_are_two_undo_steps(d: Any, undo: Callable[[], None]) -> object:
    d.add_line((0, 0), (10, 0))
    d.commit()
    d.add_circle((0, 0), 5)
    d.commit()
    undo()
    return [e.dxf_type for e in d.entities()]


def _a_modify_and_an_erase_in_one_commit_are_one_step(d: Any, undo: Callable[[], None]) -> object:
    d.add_line((0, 0), (10, 0))
    d.add_line((0, 5), (10, 5))
    d.commit()
    first, second = d.entities()
    first.color = 1
    d.erase(second)
    d.commit()
    undo()
    entities = d.entities()
    return (len(entities), sorted(e.color for e in entities))


SCENARIOS: list[Scenario] = [
    Scenario("a_line", BLANK, _a_line, (1, 1, True, "LINE", (0, 0, 0), (100.5, 0, 0), "0", 256)),
    Scenario("a_circle", BLANK, _a_circle, (1, "CIRCLE", (50, 50, 0), 25.0)),
    Scenario("an_arc_in_radians", BLANK, _an_arc_in_radians, ("ARC", (0, 0, 0), 10.0, 0.0, 1.570796)),
    Scenario("a_closed_polyline", BLANK, _a_closed_polyline, ("LWPOLYLINE", [(0, 0), (100, 0), (100, 50)], True)),
    Scenario("points_of_every_kind", BLANK, _points_of_every_kind, [((1, 2, 0), (3, 4, 0)), ((5, 6, 0), (7, 8, 0))]),
    Scenario("a_text", BLANK, _a_text, ("TEXT", "Plate", (0, 100, 0), 5.0)),
    Scenario(
        "an_mtext_with_layer_and_color",
        BLANK,
        _an_mtext_with_layer_and_color,
        ("MTEXT", "Plate PL10", (10, 20, 0), 5.0, "Steel", 1),
    ),
    Scenario("nothing_is_applied_before_the_commit", BLANK, _nothing_is_applied_before_the_commit, (0, 2, 2)),
    Scenario("a_second_commit_with_nothing_queued", BLANK, _a_second_commit_with_nothing_queued, []),
    Scenario("the_filters_of_entities", BLANK, _the_filters_of_entities, (3, 2, 2, 1, 0)),
    Scenario("an_entity_by_handle", BLANK, _an_entity_by_handle, (True, "LINE", True)),
    Scenario("a_selection", BLANK, _a_selection, True),
    Scenario("layer_and_color_set_on_an_entity", BLANK, _layer_and_color_set_on_an_entity, (True, "Steel", 1, 1)),
    Scenario("a_changed_net_and_modify", BLANK, _a_changed_net_and_modify, (True, (200.5, 0, 0), 1)),
    Scenario("a_changed_net_without_modify", BLANK, _a_changed_net_without_modify, (100, 0, 0)),
    Scenario("erase_by_entity_and_by_handle", BLANK, _erase_by_entity_and_by_handle, (3, 1, True)),
    Scenario(
        "layers_added_and_changed",
        BLANK,
        _layers_added_and_changed,
        (("Steel", 1, "Beams", True, False, False, True), ("Steel", 5, False, True), ["0", "Steel"]),
    ),
    Scenario(
        "a_layer_found_ignoring_case_and_a_missing_one",
        BLANK,
        _a_layer_found_ignoring_case_and_a_missing_one,
        ("Steel", True, True, False, 2),
    ),
    Scenario("the_current_layer", BLANK, _the_current_layer, ("0", "Steel")),
    Scenario(
        "styles_and_blocks_read_in_order",
        FILE,
        _styles_and_blocks_read_in_order,
        (["alpha", "Zeta"], ["dense", "Wide"], ["bolt", "Nut"], 2, False),
    ),
    Scenario("a_changed_style_copy_changes_nothing", FILE, _a_changed_style_copy_changes_nothing, (0.75, False)),
    Scenario("save_commits_first", FILE, _save_commits_first, (2, False)),
    Scenario("one_commit_is_one_undo_step", BLANK, _one_commit_is_one_undo_step, (0, ["0"]), needs_undo=True),
    Scenario("two_commits_are_two_undo_steps", BLANK, _two_commits_are_two_undo_steps, ["LINE"], needs_undo=True),
    Scenario(
        "a_modify_and_an_erase_in_one_commit_are_one_step",
        BLANK,
        _a_modify_and_an_erase_in_one_commit_are_one_step,
        (2, [256, 256]),
        needs_undo=True,
    ),
]


def named(name: str) -> Scenario:
    """The scenario of a name; ``KeyError`` when there is none."""
    for scenario in SCENARIOS:
        if scenario.name == name:
            return scenario
    raise KeyError(name)


def check(scenario: Scenario, drawing: Any, undo: Callable[[], None] | None = None) -> None:
    """Run the scenario on the drawing and raise ``AssertionError`` naming what was expected and what came."""
    observed = scenario.run(drawing, undo) if scenario.needs_undo else scenario.run(drawing)
    assert observed == scenario.expected, f"{scenario.name}: expected {scenario.expected!r}, got {observed!r}"
