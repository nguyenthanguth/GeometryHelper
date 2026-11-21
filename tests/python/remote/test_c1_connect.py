"""C1: connecting to a running app (D801, D804): ``with CadApplication`` (pythonnet makes every IDisposable a context
manager), the process and window the connection reports, a block that raises, and what is left running after it.

The server starts with no drawing, so the app has nothing open until a test makes it. ``CadApplication.GetRunning()``
and ``CadApplication()`` read the folder of the real app and are not run here: the hand check of the real app covers
them (D811).
"""

from collections.abc import Callable
from typing import Any

import pytest

from GeometryHelper.Cad.Remote import CadApplication, CadRemoteException


def test_the_connection_reports_the_process_and_the_window_of_the_server(
    server: Any, connect: Callable[[str], Any]
) -> None:
    with connect(server.folder) as app:
        assert isinstance(app, CadApplication)
        assert app.Running.ProcessId == server.pid
        assert app.Running.Title == "GeometryHelper CAD (test server)"


def test_the_block_of_with_gets_the_connection_itself(server: Any, connect: Callable[[str], Any]) -> None:
    app = connect(server.folder)
    with app as entered:
        assert entered is app


def test_an_app_with_no_drawing_open_has_no_active_drawing(app: Any) -> None:
    assert app.ActiveDocument is None
    assert app.Documents.Count == 0


def test_an_exception_raised_in_the_block_comes_out_unchanged(server: Any, connect: Callable[[str], Any]) -> None:
    with pytest.raises(ValueError, match="boom"):
        with connect(server.folder):
            raise ValueError("boom")


def test_a_dotnet_exception_raised_in_the_block_comes_out_as_itself(
    server: Any, connect: Callable[[str], Any], tmp_path: Any
) -> None:
    missing = str(tmp_path / "nothing.dxf")
    with pytest.raises(CadRemoteException) as raised:
        with connect(server.folder) as app:
            app.Open(missing)
    assert missing in str(raised.value.Message)


def test_the_app_keeps_its_drawings_after_the_block_ends(server: Any, connect: Callable[[str], Any]) -> None:
    with connect(server.folder) as first:
        made = first.New()
    with connect(server.folder) as second:
        assert second.Documents.Count == 1
        assert second.ActiveDocument.Name == made.Name


def test_two_connections_at_once_see_the_same_drawings(server: Any, connect: Callable[[str], Any]) -> None:
    first = connect(server.folder)
    second = connect(server.folder)
    first.New()
    assert second.Documents.Count == 1
    second.New()
    assert [str(d.Name) for d in first.Documents] == ["Drawing1.dxf", "Drawing2.dxf"]


def test_a_folder_with_no_app_announced_refuses_the_connection(connect: Callable[[str], Any], tmp_path: Any) -> None:
    with pytest.raises(CadRemoteException, match="No GeometryHelper CAD is running"):
        connect(str(tmp_path / "nobody"))
