"""E2: the examples of ``src/python/examples/remote``, each run on a connection to the test server (P5 D1002, D1007).

Each example is imported from its path with ``importlib`` under a name that is not ``__main__``, so that the block that
connects to the real app does not run, and its ``main`` is called with the connection of the ``app`` fixture. What it
printed is read with ``capsys``; what it drew is read back through the same connection. ``pick_and_mark`` waits for a
point the server has no user to give, so a timer answers it as in C7: ``type 120,80`` types the point, ``escape`` is
Esc; the timer fires 0.5 s after it starts, as C7's does (D1007 says 0.3 s: the pick is waiting long before either).

The ``ZOOM E`` of ``pick_and_mark`` is not sent to the server (see ``_Viewless``): it has no view, and the call would
wait 30 s on an ERASE the ``E`` starts. The test notes the call and its place after the commit instead.
"""

import importlib.util
import threading
from collections.abc import Callable
from pathlib import Path
from types import ModuleType
from typing import Any

import pytest

from GeometryHelper.Cad import CadDocument
from GeometryHelper.Cad.Remote import CadObjectFilter
from GeometryHelper.Dxf import DxfCircle, DxfLine, DxfMText
from GeometryHelper.Geometry import GeoPoint3, GeoVector3

EXAMPLES = Path(__file__).resolve().parents[3] / "src" / "python" / "examples" / "remote"
ANSWER_AFTER = 0.5
Output = Callable[[], str]


def _load(name: str) -> ModuleType:
    path = EXAMPLES / name
    assert path.is_file(), f"{path} does not exist: the example is not written"
    spec = importlib.util.spec_from_file_location(f"example_{path.stem}", path)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@pytest.fixture
def printed(capsys: pytest.CaptureFixture[str]) -> Output:
    """What the example printed so far, read once: ``printed()`` gives the text and starts over."""
    return lambda: capsys.readouterr().out


def _answer_later(server: Any, text: str) -> tuple[threading.Timer, list[str]]:
    """Starts a timer that sends ``text`` to the server's console after ``ANSWER_AFTER`` s; the list gets its answer."""
    answers: list[str] = []
    timer = threading.Timer(ANSWER_AFTER, lambda: answers.append(server.send(text)))
    timer.start()
    return timer, answers


def _of_kind(drawing: Any, kind: type, layer: str | None = None) -> list[Any]:
    wanted = CadObjectFilter()
    wanted.Kind = kind
    if layer is not None:
        wanted.Layer = layer
    return list(drawing.GetObjects(wanted))


def _ends(entity: Any) -> tuple[tuple[float, float], tuple[float, float]]:
    """The two ends of a line in the plane z = 0, the lower first, so that a line drawn either way compares equal."""
    ends = ((entity.StartPoint.X, entity.StartPoint.Y), (entity.EndPoint.X, entity.EndPoint.Y))
    return (min(ends), max(ends))


GRID = sorted(
    [((100.0 * i, 0.0), (100.0 * i, 1000.0)) for i in range(11)]
    + [((0.0, 100.0 * i), (1000.0, 100.0 * i)) for i in range(11)]
)


# ---- draw_and_read.py --------------------------------------------------------------------------------------------


def test_draw_and_read_draws_a_grid_of_twenty_two_lines_on_a_layer_of_its_own_and_reads_them_back(
    app: Any, printed: Output
) -> None:
    # 11 vertical lines (x = 0, 100, ... 1000, each from y = 0 to 1000) and 11 horizontal: 22 lines of 1000 = 22 000.
    _load("draw_and_read.py").main(app, None)

    assert printed().splitlines() == ["22 lines on Grid, total length 22000.000"]
    drawing = app.ActiveDocument
    assert drawing.GetObjects().Count == 22
    assert sorted(_ends(line) for line in _of_kind(drawing, DxfLine, "Grid")) == GRID
    assert [str(layer.Name) for layer in drawing.Layers] == ["0", "Grid"]


@pytest.mark.parametrize("extension", ["dxf", "dwg"])
def test_draw_and_read_given_a_file_saves_the_drawing_there_and_says_so(
    app: Any, printed: Output, tmp_path: Path, extension: str
) -> None:
    out = tmp_path / f"grid.{extension}"

    _load("draw_and_read.py").main(app, str(out))

    assert printed().splitlines() == ["22 lines on Grid, total length 22000.000", f"saved grid.{extension}"]
    saved = [e for e in CadDocument.Open(str(out)).Database.ModelSpace if isinstance(e, DxfLine)]
    assert sorted(_ends(line) for line in saved if str(line.Layer) == "Grid") == GRID
    assert len(saved) == 22


def test_draw_and_read_called_twice_makes_a_second_drawing_and_does_not_add_to_the_first(
    app: Any, printed: Output
) -> None:
    main = _load("draw_and_read.py").main

    main(app, None)
    main(app, None)

    assert printed().splitlines() == ["22 lines on Grid, total length 22000.000"] * 2
    assert [drawing.GetObjects().Count for drawing in app.Documents] == [22, 22]


# ---- pick_and_mark.py --------------------------------------------------------------------------------------------

Call = tuple[str, tuple[Any, ...], dict[str, Any]]


class _Viewless:
    """A connection that passes every call on and notes it, but does not send a command to the server.

    The test server has no view. ``SendCommand("ZOOM E\\n")`` there is answered ``There is no view to zoom.`` at once,
    the ``E`` that follows is typed at the Command prompt, where it is ERASE, and ERASE waits for objects: the call
    waits its 30 s and ends with the command not completed (measured). In the app ZOOM takes the E as its option. So the
    commands are noted and answered with nothing; everything else, drawings included, goes to the server.
    """

    def __init__(self, target: Any, calls: list[Call]) -> None:
        self._target = target
        self._calls = calls

    def __getattr__(self, name: str) -> Any:
        value = getattr(self._target, name)
        if type(value).__name__ == "CadRemoteDocument":
            return _Viewless(value, self._calls)
        if not callable(value):
            return value

        def call(*arguments: Any, **keywords: Any) -> Any:
            self._calls.append((name, arguments, keywords))
            if name == "SendCommand":
                return None
            result = value(*arguments, **keywords)
            return _Viewless(result, self._calls) if type(result).__name__ == "CadRemoteDocument" else result

        return call


def test_pick_and_mark_gives_the_circle_to_the_point_typed_and_starts_a_drawing_when_none_is_active(
    server: Any, app: Any, printed: Output
) -> None:
    # The server starts with no drawing, so main makes one. A circle of radius 50 about (120, 80), the only object.
    module = _load("pick_and_mark.py")
    assert module.PICK_TIMEOUT.TotalSeconds == 60
    timer, answers = _answer_later(server, "type 120,80")

    status = module.main(_Viewless(app, []))
    timer.join()

    assert answers == ["DONE"]
    assert status == 0
    assert printed().splitlines() == ["circle at 120.000,80.000"]
    assert app.Documents.Count == 1
    (circle,) = list(app.ActiveDocument.GetObjects())
    assert isinstance(circle, DxfCircle)
    assert (circle.Center.X, circle.Center.Y, circle.Center.Z, circle.Radius) == (120, 80, 0, 50)


def test_pick_and_mark_uses_the_drawing_that_is_active_and_starts_no_other(
    server: Any, app: Any, drawing: Any
) -> None:
    module = _load("pick_and_mark.py")
    timer, answers = _answer_later(server, "type 120,80")

    status = module.main(_Viewless(app, []))
    timer.join()

    assert (answers, status) == (["DONE"], 0)
    assert app.Documents.Count == 1
    (circle,) = list(drawing.GetObjects())
    assert isinstance(circle, DxfCircle)
    assert (circle.Center.X, circle.Center.Y) == (120, 80)


def test_pick_and_mark_cancelled_with_escape_says_so_returns_one_and_draws_nothing(
    server: Any, app: Any, drawing: Any, printed: Output
) -> None:
    # The drawing is active, so main starts none and the cancelled pick leaves it empty.
    module = _load("pick_and_mark.py")
    timer, answers = _answer_later(server, "escape")

    status = module.main(_Viewless(app, []))
    timer.join()

    assert answers == ["DONE"]
    assert status == 1
    assert printed().splitlines() == ["Cancelled."]
    assert drawing.GetObjects().Count == 0


def test_pick_and_mark_asks_for_the_centre_with_its_time_limit_and_zooms_to_the_extents_after_the_commit(
    server: Any, app: Any, drawing: Any
) -> None:
    module = _load("pick_and_mark.py")
    calls: list[Call] = []
    timer, answers = _answer_later(server, "type 120,80")

    module.main(_Viewless(app, calls))
    timer.join()

    assert answers == ["DONE"]
    names = [name for name, _, _ in calls]
    (pick,) = [call for call in calls if call[0] == "PickPoint"]
    assert pick[1] == ("Pick the centre",)
    assert pick[2] == {"timeout": module.PICK_TIMEOUT}
    (zoom,) = [call for call in calls if call[0] == "SendCommand"]
    assert zoom[1] == ("ZOOM E\n",)
    assert names.index("PickPoint") < names.index("CommitChanges") < names.index("SendCommand")


# ---- open_documents.py -------------------------------------------------------------------------------------------


def _text() -> Any:
    made = DxfMText()
    made.Contents = "Plate PL10"
    made.Location = GeoPoint3(10, 20, 0)
    made.TextHeight = 5.0
    return made


def test_open_documents_lists_each_drawing_with_its_objects_by_class_and_marks_the_active_one(
    app: Any, printed: Output
) -> None:
    # Drawing1: a line, a circle, a line (classes sorted by name, not by the order made: DxfCircle before DxfLine).
    # Drawing2: one MText, the active drawing (the second drawing is activated last). Drawing3: nothing in it.
    first, second, third = app.New(), app.New(), app.New()
    first.Insert(DxfLine(GeoPoint3(0, 0, 0), GeoPoint3(10, 0, 0)))
    first.Insert(DxfCircle(GeoPoint3(5, 5, 0), GeoVector3(0, 0, 1), 3))
    first.Insert(DxfLine(GeoPoint3(0, 1, 0), GeoPoint3(10, 1, 0)))
    first.CommitChanges()
    second.Insert(_text())
    second.CommitChanges()
    second.Activate()

    result = _load("open_documents.py").main(app)

    lines = printed().splitlines()
    assert result is None
    assert lines[:2] == ["Drawing1.dxf\t3\tDxfCircle:1, DxfLine:2", "Drawing2.dxf *\t1\tDxfMText:1"]
    assert len(lines) == 3
    assert lines[2].split("\t")[:2] == [str(third.Name), "0"]


def test_open_documents_with_no_drawing_open_ends_without_an_error(app: Any) -> None:
    assert app.Documents.Count == 0

    assert _load("open_documents.py").main(app) is None
