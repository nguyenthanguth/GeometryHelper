"""C7: asking the user to pick. The server has no user: each test answers the pick it waits in from a timer, by writing
a line to the test server's console (``type <text>`` types on the drawing's command line, ``escape`` is Esc).

The timer starts just before the pick is sent and fires 0.5 s later: a request takes about 40 microseconds (R2), so
the pick is waiting long before the answer comes. An answered pick is given 10 s, twenty times that wait, so a slow
machine fails nothing; a pick nobody answers is given 0.3 s or 0.5 s and ends on its own timeout.
"""

import threading
import time
from collections.abc import Callable
from typing import Any

import pytest
from System import TimeSpan

from GeometryHelper.Cad.Remote import CadRemoteException
from GeometryHelper.Dxf import DxfLine
from GeometryHelper.Geometry import GeoPoint3

ANSWER_AFTER = 0.5


def _answer_later(server: Any, text: str) -> tuple[threading.Timer, list[str]]:
    """Starts a timer that sends ``text`` to the server's console after ``ANSWER_AFTER`` s; the list gets its answer."""
    answers: list[str] = []
    timer = threading.Timer(ANSWER_AFTER, lambda: answers.append(server.send(text)))
    timer.start()
    return timer, answers


def test_a_point_typed_for_the_pick_is_the_point_the_script_gets(server: Any, drawing: Any) -> None:
    timer, answers = _answer_later(server, "type 120,80")

    picked = drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromSeconds(10))
    timer.join()

    assert answers == ["DONE"]
    assert isinstance(picked, GeoPoint3)
    assert (picked.X, picked.Y, picked.Z) == (120, 80, 0)


def test_a_pick_with_a_base_point_and_a_timeout_given_by_keyword_returns_the_point_typed(
    server: Any, drawing: Any
) -> None:
    timer, answers = _answer_later(server, "type 12.5,8")

    picked = drawing.PickPoint("Pick the end", basePoint=GeoPoint3(1, 1, 0), timeout=TimeSpan.FromSeconds(10))
    timer.join()

    assert answers == ["DONE"]
    assert (picked.X, picked.Y, picked.Z) == (12.5, 8, 0)


def test_a_pick_nobody_answers_ends_after_its_timeout_with_an_exception_that_is_not_a_cancel(drawing: Any) -> None:
    start = time.perf_counter()
    with pytest.raises(CadRemoteException) as raised:
        drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromMilliseconds(500))
    waited = time.perf_counter() - start

    assert not raised.value.IsCancelled
    assert "did not pick in time" in str(raised.value.Message)
    assert waited >= 0.45


def test_escape_at_a_pick_raises_an_exception_that_is_a_cancel(server: Any, drawing: Any) -> None:
    timer, answers = _answer_later(server, "escape")

    with pytest.raises(CadRemoteException) as raised:
        drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromSeconds(10))
    timer.join()

    assert answers == ["DONE"]
    assert raised.value.IsCancelled
    assert "cancelled" in str(raised.value.Message)


def test_a_pick_after_an_ended_pick_works_on_the_same_connection(server: Any, drawing: Any) -> None:
    with pytest.raises(CadRemoteException):
        drawing.PickPoint("Pick the corner", timeout=TimeSpan.FromMilliseconds(300))
    timer, answers = _answer_later(server, "type 5,6")

    picked = drawing.PickPoint("Pick again", timeout=TimeSpan.FromSeconds(10))
    timer.join()

    assert answers == ["DONE"]
    assert (picked.X, picked.Y) == (5, 6)


def test_a_point_typed_on_a_line_picks_that_line(server: Any, drawing: Any, line: Callable[..., Any]) -> None:
    drawing.Insert(line(0, 0, 100, 0))
    handle = drawing.CommitChanges()[0]
    timer, answers = _answer_later(server, "type 50,0")

    picked = drawing.PickObject("Select the line", TimeSpan.FromSeconds(10))
    timer.join()

    assert answers == ["DONE"]
    assert isinstance(picked, DxfLine)
    assert drawing.HandleOf(picked) == handle
