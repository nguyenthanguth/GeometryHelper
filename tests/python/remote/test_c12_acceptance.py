"""C12: the acceptance command C1 of D1677 from outside. The function body is the one the app runs as ``@command("C1")``
(``tests/python/c1_acceptance.py``); here it edits a drawing that is **not** the active one, taken by name from the
documents of the app, and one ``U`` typed in that drawing takes the whole edit back.

The test server has no user: each pick is answered from a thread that writes lines to the server's console, 0.5 s apart
(``type x,y`` types a point, which picks the object under it; ``type`` alone is Enter), as ``test_c7_picks`` does.
"""

import threading
import time
from collections.abc import Callable
from typing import Any

from c1_acceptance import move_the_selection_to_the_layer_of_the_picked_object

from geometryhelper.cad import App
from GeometryHelper.Cad.Remote import CadRemoteLayer

ANSWER_GAP = 0.5


def _answers_later(server: Any, lines: list[str]) -> tuple[threading.Thread, list[str]]:
    """Starts a thread that sends each line to the server's console, ``ANSWER_GAP`` s apart; the list gets the answers."""
    answers: list[str] = []

    def send() -> None:
        for line in lines:
            time.sleep(ANSWER_GAP)
            answers.append(server.send(line))

    thread = threading.Thread(target=send)
    thread.start()
    return thread, answers


def _layers_by_height(document: Any) -> list[tuple[float, str]]:
    """(y, layer) of every line of the drawing, the lowest first: the drawing's lines lie level at their own heights."""
    return sorted((float(entity.StartPoint.Y), str(entity.Layer)) for entity in document.GetObjects())


def test_c1_from_outside_moves_the_selection_in_a_drawing_that_is_not_active_and_one_u_undoes_it(
    server: Any, app: Any, line: Callable[..., Any]
) -> None:
    # Drawing1: a line on A at y = 0 (the object to pick), lines on B at y = 50 and 150 and on 0 at y = 100. C1 picks the
    # first and selects the lines at 50 and 100: both go to A, the line at 150 stays on B. Drawing2 is made last, so it
    # is the one shown, and has nothing in it.
    target = app.New()
    target.AddLayer(CadRemoteLayer("A"))
    target.AddLayer(CadRemoteLayer("B"))
    target.CommitChanges()
    for y, layer in ((0, "A"), (50, "B"), (100, None), (150, "B")):
        target.Insert(line(0, y, 100, y, layer))
    target.CommitChanges()
    other = app.New()
    cad = App(app)
    drawing = cad.documents["Drawing1.dxf"]
    assert not drawing.is_active
    assert _layers_by_height(target) == [(0, "A"), (50, "B"), (100, "0"), (150, "B")]
    thread, answers = _answers_later(server, ["type 50,0", "type 50,50", "type 50,100", "type"])

    moved = move_the_selection_to_the_layer_of_the_picked_object(drawing)
    thread.join()

    assert answers == ["DONE"] * 4
    assert moved == 2
    assert _layers_by_height(target) == [(0, "A"), (50, "A"), (100, "A"), (150, "B")]
    assert other.GetObjects().Count == 0

    cad.run_command(drawing, "U\n")

    assert _layers_by_height(target) == [(0, "A"), (50, "B"), (100, "0"), (150, "B")]
    assert other.GetObjects().Count == 0
