"""What the Python host of GeometryHelper CAD runs inside the interpreter: output into lines, input, and the guards that
turn an error, a stop or an exit into a result the host reads. Not for users: ``geometryhelper.cad`` is their module.

Every guard returns a list of four strings, ``[status, summary, traceback, error]``, where status is ``completed``,
``cancelled`` or ``failed``, summary is the one line for the command line (``ZeroDivisionError: division by zero (tool.py,
line 3)``), traceback is the whole of it without the host's own frames, and error is the summary without the file and line.
"""

from __future__ import annotations

import builtins
import codeop
import gc
import importlib.util
import io
import sys
import traceback
from collections.abc import Callable
from types import CodeType, TracebackType
from typing import Any, cast

from geometryhelper import cad

_host: Any = importlib.import_module("GeometryHelper.Cad.Python")

_THIS_FILE = __file__
_compiler = codeop.CommandCompiler()
_compiled: CodeType | None = None
_real_input = builtins.input
_installed: list[io.TextIOBase] = []
_found: Any = None


class _Out(io.TextIOBase):
    """A stdout or stderr that gathers text into whole lines and hands each to the host."""

    def __init__(self, emit: Callable[[str, bool], None], is_error: bool) -> None:
        self._emit = emit
        self._is_error = is_error
        self._rest = ""

    def writable(self) -> bool:
        return True

    @property
    def encoding(self) -> str:  # pyright: ignore[reportIncompatibleVariableOverride]
        return "utf-8"

    def write(self, text: str, /) -> int:
        self._rest += text
        if "\n" in self._rest:
            *lines, self._rest = self._rest.split("\n")
            for line in lines:
                self._emit(line.rstrip("\r"), self._is_error)
        return len(text)

    def flush(self) -> None:
        if self._rest:
            rest, self._rest = self._rest, ""
            self._emit(rest, self._is_error)


class _In(io.TextIOBase):
    """A stdin that asks the user for a line at the command line."""

    def readable(self) -> bool:
        return True

    def readline(self, size: int | None = -1, /) -> str:
        return (cad.get_string("Python input") or "") + "\n"


def install(emit: Callable[[str, bool], None]) -> None:
    """Replaces stdout, stderr, stdin and ``input`` for the runs of the app."""
    out, err = _Out(emit, False), _Out(emit, True)
    _installed[:] = [out, err]
    sys.stdout = out
    sys.stderr = err
    sys.stdin = _In()

    def ask(prompt: object = "") -> str:
        return cad.get_string(str(prompt).rstrip() or "Python input") or ""

    builtins.input = ask


def new_console() -> dict[str, Any]:
    """Returns the globals of a drawing's console."""
    return {"__name__": "__console__"}


def _is_host(frame_file: str) -> bool:
    return frame_file == _THIS_FILE


def _is_net(error: BaseException) -> bool:
    return type(error).__module__.split(".")[0] in ("System", "Python", "GeometryHelper")


def _one_line(error: BaseException) -> str:
    """Returns ``Type: message`` as one line: the stack trace pythonnet appends to a .NET exception's text is cut off, and every
    line end left in the message becomes a space (.NET Framework words ``ArgumentNullException`` over two lines)."""
    lines = traceback.format_exception_only(type(error), error)[-1].replace("\r\n", "\n").split("\n")
    if _is_net(error):
        for i, line in enumerate(lines):
            if line.startswith("   at "):
                lines = lines[:i]
                break
    return " ".join(part for part in (line.strip() for line in lines) if part)


def _describe(error: BaseException, show_file: bool) -> tuple[str, str, str]:
    """Returns the summary line, the traceback and the error line of an error, the host's frames left out.

    A .NET exception that pythonnet chained to an error as its cause is left out of the traceback: it repeats the error.
    The summary and the error line are one line each, whatever the message holds.
    """
    link: BaseException | None = error
    while link is not None:
        for name in ("__cause__", "__context__"):
            chained: BaseException | None = getattr(link, name)
            if chained is not None and _is_net(chained):
                setattr(link, name, None)
        link = link.__cause__ or link.__context__
    tb: TracebackType | None = error.__traceback__
    while tb is not None and _is_host(tb.tb_frame.f_code.co_filename):
        tb = tb.tb_next
    last: TracebackType | None = tb
    while last is not None and last.tb_next is not None:
        last = last.tb_next
    text = "".join(traceback.format_exception(type(error), error, tb)).rstrip("\n")
    line = _one_line(error)
    summary = line
    if show_file:
        if isinstance(error, SyntaxError) and error.filename:
            summary += f" ({error.filename.replace(chr(92), '/').rsplit('/', 1)[-1]}, line {error.lineno})"
        elif last is not None:
            name = last.tb_frame.f_code.co_filename.replace("\\", "/").rsplit("/", 1)[-1]
            summary += f" ({name}, line {last.tb_lineno})"
    return summary, text, line


def _pending(name: str) -> None:
    """Calls ``commit_pending`` or ``discard_pending`` of the Drawing API, which commits or drops what the drawings queued."""
    try:
        local = importlib.import_module("geometryhelper.cad._local")
    except ModuleNotFoundError as missing:
        if missing.name != "geometryhelper.cad._local":
            raise
        return
    action: Callable[[], object] | None = getattr(local, name, None)
    if action is not None:
        action()


def _finishing(action: Callable[[], object]) -> Callable[[], object]:
    """Wraps a run's code: what the Drawing API queued is committed when the code ends well (inside the run's undo step), and
    dropped when it raises, so that an error before ``commit()`` leaves the drawing as it was."""

    def finished() -> None:
        try:
            action()
        except SystemExit as stop:
            _pending("commit_pending" if stop.code is None or stop.code == 0 else "discard_pending")
            raise
        except BaseException:
            _pending("discard_pending")
            raise
        _pending("commit_pending")

    return finished


def flush_callback() -> None:
    """Commits what a callback of the window queued, unless a run is going on, whose end commits it."""
    if _host.CadPythonScript.Current is None:
        _pending("commit_pending")


def discard_callback() -> None:
    """Drops what a callback of the window queued before it raised, unless a run is going on."""
    if _host.CadPythonScript.Current is None:
        _pending("discard_pending")


def report(error: BaseException, function: object = None) -> None:
    """Writes one ``Python error:`` line for an error of a callback of the window: the error's text, and the file of the
    function when the error's own frame is elsewhere."""
    summary, _, _ = _describe(error, True)
    code = getattr(getattr(function, "__func__", function), "__code__", None)
    name = None if code is None else code.co_filename.replace("\\", "/").rsplit("/", 1)[-1]
    if name is not None and f"({name}," not in summary:
        summary += f" [{name}]"
    (_installed[1] if len(_installed) > 1 else sys.stderr).write(f"Python error: {summary}\n")
    for stream in _installed:
        stream.flush()


def _guard(action: Callable[[], object], show_file: bool, commits: bool = True) -> list[str]:
    try:
        (_finishing(action) if commits else action)()
    except SystemExit as exit_:
        if exit_.code is None or exit_.code == 0:
            return ["completed", "", "", ""]
        summary, text, line = _describe(exit_, show_file)
        return ["failed", summary, text, line]
    except KeyboardInterrupt as stopped:
        summary, text, line = _describe(stopped, False)
        return ["cancelled", "KeyboardInterrupt", text, line]
    except BaseException as error:
        summary, text, line = _describe(error, show_file)
        return ["failed", summary, text, line]
    finally:
        # The host's own writers, not sys.stdout and sys.stderr: a script may have set those to None or to a closed file.
        for stream in _installed:
            stream.flush()
    return ["completed", "", "", ""]


def run_file(source: str, path: str, folder: str) -> list[str]:
    """Runs a file's source as ``python path`` does: fresh globals named ``__main__``, its folder first on ``sys.path``."""

    def action() -> None:
        code = compile(source, path, "exec")
        exec(code, {"__name__": "__main__", "__file__": path})

    saved_argv = sys.argv
    sys.argv = [path]
    sys.path.insert(0, folder)
    try:
        return _guard(action, True)
    finally:
        sys.argv = saved_argv
        if folder in sys.path:
            sys.path.remove(folder)


def load_file(source: str, path: str, folder: str, module_name: str, host: Any) -> list[str]:
    """Imports a file as a fresh module of the given name; the ``@command`` and ``@on_unload`` calls made meanwhile are
    collected, and the additions to the window go to ``host``, the file's own plugin host."""

    def action() -> None:
        spec = importlib.util.spec_from_file_location(module_name, path)
        if spec is None or spec.loader is None:
            raise ImportError(f"{path} cannot be loaded as a Python module.")
        module = importlib.util.module_from_spec(spec)
        sys.modules[module_name] = module
        try:
            exec(compile(source, path, "exec"), module.__dict__)
        except BaseException:
            sys.modules.pop(module_name, None)
            raise

    sys.path.insert(0, folder)
    global _found
    cad._begin_load(host, path)  # pyright: ignore[reportPrivateUsage]
    try:
        return _guard(action, True)
    finally:
        _found = cad._end_load()  # pyright: ignore[reportPrivateUsage]
        if folder in sys.path:
            sys.path.remove(folder)


def loaded_commands() -> list[Any]:
    """Returns what ``@command`` collected in the last ``load_file``, as ``[name, alias, description, function,
    uses_selection]`` lists."""
    return _found.commands if _found is not None else []


def loaded_unloaders() -> list[Any]:
    """Returns the functions ``@on_unload`` collected in the last ``load_file``, in the order they were defined."""
    return _found.unloaders if _found is not None else []


def call(function: Callable[[], object]) -> list[str]:
    """Runs the function of a command a loaded file defined."""
    return _guard(function, True)


def call_hook(function: Callable[[], object]) -> list[str]:
    """Runs the ``@on_unload`` function of a file: no run is going on, so nothing is committed."""
    return _guard(function, True, False)


def console_compile(text: str) -> list[str]:
    """Compiles a palette entry as ``python -i`` does: ``incomplete``, ``error`` with the error, or ``ok``."""
    global _compiled
    _compiled = None
    try:
        code = _compiler(text, "<console>", "single")
    except (SyntaxError, ValueError, OverflowError) as error:
        summary, trace, line = _describe(error, False)
        return ["error", summary, trace, line]
    if code is None:
        return ["incomplete", "", "", ""]
    _compiled = code
    return ["ok", "", "", ""]


def console_run(namespace: dict[str, Any]) -> list[str]:
    """Runs the entry ``console_compile`` accepted in a drawing's console globals."""
    code = _compiled
    assert code is not None
    return _guard(lambda: exec(code, namespace), False)


def uninstall() -> None:
    """Gives the streams and ``input`` back, so that no .NET object is left in Python's hands at shutdown."""
    sys.stdout = sys.__stdout__
    sys.stderr = sys.__stderr__
    sys.stdin = sys.__stdin__
    builtins.input = _real_input
    _installed.clear()
    global _found
    _found = None
    # A function of a script and a delegate made from it, kept in the globals the function reads, hold each other through
    # .NET, where Python's collector cannot see: only emptying the globals lets go of the delegate. Without that,
    # pythonnet's shutdown on .NET Framework (inside an AppDomain that is not the default) cannot serialise it and throws.
    for name in [name for name in sys.modules if name.startswith("geometryhelper_user_")]:
        sys.modules[name].__dict__.clear()
    for candidate in gc.get_objects():
        if isinstance(candidate, dict):
            namespace = cast("dict[str, Any]", candidate)
            if namespace.get("__name__") in ("__main__", "__console__"):
                namespace.clear()
