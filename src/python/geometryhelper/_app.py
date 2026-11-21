"""What the Python host of GeometryHelper CAD runs inside the interpreter: output into lines, input, and the guards that
turn an error, a stop or an exit into a result the host reads. Not for users: ``geometryhelper.cad`` is their module.

Every guard returns a list of three strings, ``[status, summary, traceback]``, where status is ``completed``, ``cancelled``
or ``failed``, summary is the last line of the traceback (``ZeroDivisionError: division by zero (tool.py, line 3)``), and
traceback is the whole of it without the host's own frames.
"""

from __future__ import annotations

import builtins
import codeop
import importlib.util
import io
import sys
import traceback
from collections.abc import Callable
from types import CodeType, TracebackType
from typing import Any

from geometryhelper import cad

_THIS_FILE = __file__
_compiler = codeop.CommandCompiler()
_compiled: CodeType | None = None
_real_input = builtins.input


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
    sys.stdout = _Out(emit, False)
    sys.stderr = _Out(emit, True)
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


def _describe(error: BaseException, show_file: bool) -> tuple[str, str]:
    """Returns the summary line and the traceback of an error, the host's frames left out.

    A .NET exception that pythonnet chained to an error as its cause is left out of the traceback: it repeats the error.
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
    summary = traceback.format_exception_only(type(error), error)[-1].strip()
    if show_file:
        if isinstance(error, SyntaxError) and error.filename:
            summary += f" ({error.filename.replace(chr(92), '/').rsplit('/', 1)[-1]}, line {error.lineno})"
        elif last is not None:
            name = last.tb_frame.f_code.co_filename.replace("\\", "/").rsplit("/", 1)[-1]
            summary += f" ({name}, line {last.tb_lineno})"
    return summary, text


def _guard(action: Callable[[], object], show_file: bool) -> list[str]:
    try:
        action()
    except SystemExit as exit_:
        if exit_.code is None or exit_.code == 0:
            return ["completed", "", ""]
        summary, text = _describe(exit_, show_file)
        return ["failed", summary, text]
    except KeyboardInterrupt as stopped:
        summary, text = _describe(stopped, False)
        return ["cancelled", "KeyboardInterrupt", text]
    except BaseException as error:
        summary, text = _describe(error, show_file)
        return ["failed", summary, text]
    finally:
        sys.stdout.flush()
        sys.stderr.flush()
    return ["completed", "", ""]


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


def load_file(source: str, path: str, folder: str, module_name: str) -> list[str]:
    """Imports a file as a fresh module of the given name; ``cad_command`` calls made meanwhile are collected."""

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
    cad._begin_load()  # pyright: ignore[reportPrivateUsage]
    try:
        return _guard(action, True)
    finally:
        if folder in sys.path:
            sys.path.remove(folder)


def loaded_commands() -> list[Any]:
    """Returns what ``cad_command`` collected since ``load_file`` began, as ``[name, alias, description, function]`` lists."""
    return cad._end_load()  # pyright: ignore[reportPrivateUsage]


def call(function: Callable[[], object]) -> list[str]:
    """Runs the function of a command ``PYLOAD`` defined."""
    return _guard(function, True)


def console_compile(text: str) -> list[str]:
    """Compiles a palette entry as ``python -i`` does: ``incomplete``, ``error`` with the error, or ``ok``."""
    global _compiled
    _compiled = None
    try:
        code = _compiler(text, "<console>", "single")
    except (SyntaxError, ValueError, OverflowError) as error:
        summary, trace = _describe(error, False)
        return ["error", summary, trace]
    if code is None:
        return ["incomplete", "", ""]
    _compiled = code
    return ["ok", "", ""]


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
