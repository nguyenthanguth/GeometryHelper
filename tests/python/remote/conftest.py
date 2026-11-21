"""Fixtures of the remote suite: a headless GeometryHelper CAD in a process of its own, and clients that connect to it.

The server is ``tests/GeometryHelper.Cad.Remote.TestServer``: a console program that hosts the app's pipe server over a
host with no window. It announces itself in a folder of the test's own (``CadApplication()`` reads the folder of the
real app, which a test never touches), so a test runs while the maintainer's app is open, and the client reaches the
folder through the internal ``CadApplication(string folder)`` constructor by reflection, as the C# suite does through
InternalsVisibleTo. The server never builds a drawing by itself: a test makes the drawings it needs.

The runtime and the three assemblies are loaded by ``import geometryhelper`` in the session guard of the conftest above
this folder, which every test of ``tests/python`` runs under; this file imports nothing of the library before it.
"""

import contextlib
import os
import queue
import subprocess
import threading
from collections.abc import Callable, Iterator
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import pytest

SERVER_NAME = "GeometryHelper.Cad.Remote.TestServer.exe"
READY_TIMEOUT = 30.0
ANSWER_TIMEOUT = 10.0
QUIT_TIMEOUT = 10.0


@pytest.fixture(scope="session")
def server_exe() -> Path:
    """The built test server: ``GH_REMOTE_TEST_SERVER`` when set, else its Release output in this repository."""
    named = os.environ.get("GH_REMOTE_TEST_SERVER")
    if named:
        path = Path(named)
    else:
        root = Path(__file__).resolve().parents[3]
        path = root / "tests" / "GeometryHelper.Cad.Remote.TestServer" / "bin" / "Release" / "net8.0" / SERVER_NAME
    if not path.is_file():
        pytest.fail(f"The remote test server is not built: {path}. Build the solution in Release first.")
    return path


@dataclass
class RunningServer:
    """One test server: its process id, the name of its pipe, the folder it announced itself in, and its console."""

    pid: int
    pipe: str
    folder: str
    _process: "subprocess.Popen[str]"
    _lines: "queue.Queue[str | None]"

    def send(self, line: str) -> str:
        """Writes one line to the server's console and returns its one-line answer (``DONE`` or ``ERROR ...``)."""
        assert self._process.stdin is not None
        self._process.stdin.write(line + "\n")
        self._process.stdin.flush()
        return self._answer()

    def _answer(self) -> str:
        try:
            answer = self._lines.get(timeout=ANSWER_TIMEOUT)
        except queue.Empty:
            pytest.fail(f"The test server gave no answer within {ANSWER_TIMEOUT} s.")
        if answer is None:
            pytest.fail(f"The test server ended (exit code {self._process.poll()}) instead of answering.")
        return answer


def _pump(stream: Any, lines: "queue.Queue[str | None]") -> None:
    """Moves the server's output lines to a queue, so that a wait for one can have a time limit."""
    for line in stream:
        lines.put(line.rstrip("\r\n"))
    lines.put(None)


@pytest.fixture
def server(server_exe: Path, tmp_path: Path) -> Iterator[RunningServer]:
    """A test server of its own for each test, started with no drawing, ended at the end of the test.

    What the server wrote to its standard error is printed when the test ends, so that a failing test shows it.
    """
    folder = tmp_path / "remote"
    stderr_path = tmp_path / "server.stderr.txt"
    with stderr_path.open("w", encoding="utf-8") as stderr:
        process = subprocess.Popen(
            [str(server_exe), "--folder", str(folder)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=stderr,
            text=True,
            encoding="utf-8",
        )
        assert process.stdout is not None and process.stdin is not None
        lines: "queue.Queue[str | None]" = queue.Queue()
        threading.Thread(target=_pump, args=(process.stdout, lines), daemon=True).start()
        try:
            try:
                ready = lines.get(timeout=READY_TIMEOUT)
            except queue.Empty:
                pytest.fail(f"The test server was not ready within {READY_TIMEOUT} s.")
            if ready is None or not ready.startswith("READY "):
                code = _exit_code(process)
                pytest.fail(f"The test server said {ready!r} (exit code {code}) instead of READY: {_text(stderr_path)}")
            _, pipe, pid = ready.split(" ")
            yield RunningServer(int(pid), pipe, str(folder), process, lines)
        finally:
            try:
                process.stdin.write("quit\n")
                process.stdin.flush()
                process.wait(timeout=QUIT_TIMEOUT)
            except (OSError, subprocess.TimeoutExpired):
                process.kill()
                process.wait()
            with contextlib.suppress(OSError):
                process.stdin.close()
    text = _text(stderr_path)
    if text:
        print(f"test server stderr: {text}")


def _exit_code(process: "subprocess.Popen[str]") -> int | None:
    """The exit code of a process whose output has ended: it is about to exit, so it is waited for a moment."""
    try:
        return process.wait(timeout=QUIT_TIMEOUT)
    except subprocess.TimeoutExpired:
        return None


def _text(path: Path) -> str:
    return path.read_text(encoding="utf-8").strip()


@pytest.fixture
def connect() -> Iterator[Callable[[str], Any]]:
    """Returns ``connect(folder)``: a ``CadApplication`` connected to the server that announced itself in the folder.

    Each connection is disposed at the end of the test (a second Dispose, where the test used ``with``, is harmless).
    """
    import clr
    from System import Array, Object, String, Type
    from System.Reflection import BindingFlags, TargetInvocationException

    from GeometryHelper.Cad.Remote import CadApplication

    constructor = clr.GetClrType(CadApplication).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic, None, Array[Type]([clr.GetClrType(String)]), None
    )
    if constructor is None:
        pytest.fail("CadApplication has no internal constructor (string folder): the remote suite is out of date.")
    made: list[Any] = []

    def open_connection(folder: str) -> Any:
        try:
            app = constructor.Invoke(Array[Object]([folder]))
        except TargetInvocationException as error:
            raise error.InnerException  # the CadRemoteException the constructor threw, as a script would see it
        made.append(app)
        return app

    yield open_connection
    for app in made:
        app.Dispose()


@pytest.fixture
def app(server: RunningServer, connect: Callable[[str], Any]) -> Any:
    """A ``CadApplication`` connected to this test's server."""
    return connect(server.folder)


@pytest.fixture
def drawing(app: Any) -> Any:
    """A new, empty drawing of the server, shown: ``Drawing1.dxf``."""
    return app.New()


@pytest.fixture
def line() -> Callable[..., Any]:
    """Builds ``line(x1, y1, x2, y2, layer=None)``: a ``DxfLine`` in the plane z = 0, on a layer when one is named."""

    def build(x1: float, y1: float, x2: float, y2: float, layer: str | None = None) -> Any:
        from GeometryHelper.Dxf import DxfLine
        from GeometryHelper.Geometry import GeoPoint3

        made = DxfLine(GeoPoint3(x1, y1, 0), GeoPoint3(x2, y2, 0))
        if layer is not None:
            made.Layer = layer
        return made

    return build
