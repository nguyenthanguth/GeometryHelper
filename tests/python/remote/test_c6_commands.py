"""C6: the command line from a script. The texts are the ones the command line wrote when the suite was drafted: a
line feed is Enter, so ``"LINE 0,0 100,0\\n\\n"`` starts LINE, answers its two points and ends it with the second Enter
(a trailing space is not an Enter and leaves LINE waiting).
"""

import time
from typing import Any

from System import TimeSpan

from GeometryHelper.Dxf import DxfLine


def _ends(entity: Any) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    start, end = entity.StartPoint, entity.EndPoint
    return ((start.X, start.Y, start.Z), (end.X, end.Y, end.Z))


def test_a_line_command_with_its_points_completes_and_draws_one_line(drawing: Any) -> None:
    result = drawing.SendCommand("LINE 0,0 100,0\n\n")

    assert result.Completed
    objects = drawing.GetObjects()
    assert objects.Count == 1
    assert isinstance(objects[0], DxfLine)
    assert _ends(objects[0]) == ((0, 0, 0), (100, 0, 0))


def test_the_output_of_a_command_holds_its_prompts_with_the_answers_typed(drawing: Any) -> None:
    result = drawing.SendCommand("LINE 0,0 100,0\n\n")

    output = list(result.Output)
    assert output[0] == "Command: LINE"
    assert "Specify first point: 0,0" in output
    assert "Specify next point or [Undo]: 100,0" in output


def test_an_unknown_command_completes_and_the_output_says_it_is_unknown(drawing: Any) -> None:
    result = drawing.SendCommand("FOOBAR\n")

    assert result.Completed
    assert list(result.Output) == ["Command: FOOBAR", 'Unknown command "FOOBAR".  Press F1 for help.']
    assert drawing.GetObjects().Count == 0


def test_a_command_that_waits_for_a_point_is_not_completed_after_the_time_and_the_next_text_finishes_it(
    drawing: Any,
) -> None:
    start = time.perf_counter()
    waiting = drawing.SendCommand("LINE 0,0", TimeSpan.FromMilliseconds(300))
    waited = time.perf_counter() - start

    assert not waiting.Completed
    assert waited >= 0.25
    assert drawing.GetObjects().Count == 0

    finished = drawing.SendCommand("100,50\n\n")

    assert finished.Completed
    objects = drawing.GetObjects()
    assert objects.Count == 1
    assert _ends(objects[0]) == ((0, 0, 0), (100, 50, 0))


def test_two_commands_one_after_the_other_each_complete(drawing: Any) -> None:
    first = drawing.SendCommand("LINE 0,0 100,0\n\n")
    second = drawing.SendCommand("LINE 0,10 100,10\n\n")

    assert first.Completed
    assert second.Completed
    assert drawing.GetObjects().Count == 2
