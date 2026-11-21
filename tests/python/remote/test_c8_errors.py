"""C8: what goes wrong and how a script sees it. A refusal of the app is a ``CadRemoteException`` with the app's own
message; a missing argument is the .NET ``ArgumentNullException`` or ``ArgumentOutOfRangeException``, which pythonnet
raises as itself. The connection is good for the next request after either.
"""

from collections.abc import Callable
from typing import Any

import pytest
from System import Array, ArgumentNullException, ArgumentOutOfRangeException, String, TimeSpan

from GeometryHelper.Cad.Remote import CadRemoteException


def test_opening_a_file_that_is_not_there_is_refused_and_the_message_names_the_path(app: Any, tmp_path: Any) -> None:
    missing = str(tmp_path / "missing.dxf")

    with pytest.raises(CadRemoteException) as raised:
        app.Open(missing)

    assert missing in str(raised.value.Message)
    assert not raised.value.IsCancelled


def test_a_ticket_used_before_the_commit_is_refused_and_the_message_says_so(
    drawing: Any, line: Callable[..., Any]
) -> None:
    ticket = drawing.Insert(line(0, 0, 100, 0))

    with pytest.raises(CadRemoteException) as raised:
        drawing.GetObject(ticket)

    assert str(raised.value.Message) == f"The object {ticket} is not in the drawing yet: call CommitChanges first."


def test_a_ticket_made_known_by_the_commit_is_accepted(drawing: Any, line: Callable[..., Any]) -> None:
    ticket = drawing.Insert(line(0, 0, 100, 0))
    drawing.CommitChanges()

    assert drawing.GetObject(ticket) is not None


def test_a_missing_handle_is_the_dotnet_argument_null_exception(drawing: Any) -> None:
    with pytest.raises(ArgumentNullException, match="handle"):
        drawing.GetObject(None)


def test_a_missing_selection_path_and_text_are_each_an_argument_null_exception(app: Any, drawing: Any) -> None:
    with pytest.raises(ArgumentNullException):
        drawing.Select(None)
    with pytest.raises(ArgumentNullException):
        drawing.SendCommand(None)
    with pytest.raises(ArgumentNullException):
        app.Open(None)


def test_a_negative_time_to_wait_is_an_argument_out_of_range_exception(drawing: Any) -> None:
    with pytest.raises(ArgumentOutOfRangeException):
        drawing.SendCommand("ZOOM E\n", TimeSpan.FromSeconds(-1))


def test_saving_a_drawing_that_has_no_file_is_refused_until_it_is_saved_as(drawing: Any) -> None:
    with pytest.raises(CadRemoteException, match="has no file yet: use SaveAs"):
        drawing.Save()


def test_the_connection_serves_the_next_request_after_a_refused_one(app: Any, tmp_path: Any) -> None:
    with pytest.raises(CadRemoteException):
        app.Open(str(tmp_path / "missing.dxf"))

    made = app.New()

    assert made.Name == "Drawing1.dxf"
    assert app.Documents.Count == 1


def test_selecting_a_handle_the_drawing_has_not_is_refused_and_the_message_names_it(drawing: Any) -> None:
    with pytest.raises(CadRemoteException, match="has no object ZZZ"):
        drawing.Select(Array[String](["ZZZ"]))


def test_deleting_a_handle_the_drawing_has_not_is_refused_by_the_commit(drawing: Any) -> None:
    drawing.Delete("ZZZ")

    with pytest.raises(CadRemoteException, match="has no object ZZZ"):
        drawing.CommitChanges()
