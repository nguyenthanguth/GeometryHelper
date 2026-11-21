"""Scripts inside GeometryHelper CAD: the drawing, the command line, and prompts that behave as a command's do.

Works in the PYTHON palette, in PYRUN and in PYLOAD of the app only. A script touches the drawing through the .NET object
model, ``document()`` and ``session()`` give it the drawing the run started in, and the functions here ask the user: a point
picked with the object snaps or typed, a distance, an angle, a number, a text, a keyword, one object, a selection. Esc at a
prompt raises ``Cancelled``, which a bare ``except Exception`` does not swallow. Units are the drawing's; nothing is converted.

    from geometryhelper.cad import command, get_distance, get_point

    centre = get_point("Specify centre")
    radius = get_distance("Specify radius", base=centre)
    command("CIRCLE", centre, radius)
"""

from __future__ import annotations

import decimal
import math
import importlib
import re
import sys
from collections.abc import Callable, Sequence
from typing import TYPE_CHECKING, Any, TypeVar, cast

if TYPE_CHECKING:
    from GeometryHelper.Cad import CadCommandSession, CadDocument
    from GeometryHelper.Geometry import GeoPoint2

try:
    _host: Any = importlib.import_module("GeometryHelper.Cad.Python")
except ImportError:
    raise ImportError(
        "geometryhelper.cad works inside GeometryHelper CAD only: in the PYTHON palette, PYRUN or PYLOAD."
    ) from None

F = TypeVar("F", bound=Callable[..., object])

_net: Any = importlib.import_module("System")
_cad: Any = importlib.import_module("GeometryHelper.Cad")
_geo: Any = importlib.import_module("GeometryHelper.Geometry")
_INTEGER = re.compile(r"[+-]?[0-9]+", re.ASCII)
_REAL = re.compile(r"[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", re.ASCII)
_loading: list[list[Any]] | None = None


class Cancelled(KeyboardInterrupt):
    """Esc at a prompt, or a prompt asked after it: the run is cancelled and the drawing is as before it."""


def _script() -> Any:
    script = _host.CadPythonScript.Current
    if script is None:
        raise RuntimeError("geometryhelper.cad works only while a script runs in GeometryHelper CAD.")
    return script


def document() -> CadDocument:
    """Return the drawing the run started in (not the one shown later)."""
    return _script().Document


def session() -> CadCommandSession:
    """Return the command session of that drawing."""
    return _script().Session


def selection() -> list[str]:
    """Return the handles of the objects selected now, in order."""
    return sorted((str(handle) for handle in document().Selection.Handles), key=str.upper)


def write(text: str) -> None:
    """Write one line or more on the command line and in the palette."""
    _script().Write(text)


def _strings(items: Sequence[str] | None) -> Any:
    return None if items is None else _net.Array[_net.String](list(items))


def _call(method: Callable[..., Any], *arguments: object) -> Any:
    """Calls a method of the run's script, turning what it throws into what Python code expects."""
    sys.stdout.flush()
    sys.stderr.flush()
    try:
        return method(*arguments)
    except _host.CadPythonCancelledException:
        raise Cancelled("Function cancelled") from None
    except _net.InvalidOperationException as error:
        raise RuntimeError(str(error.Message)) from None
    except _net.ArgumentException as error:
        raise ValueError(str(error.Message)) from None


def _ask(prompt: Any) -> Any:
    return _call(_script().Ask, prompt)


def _kind(answer: Any) -> str:
    return str(answer.Kind.ToString())


def _as_point(text: str) -> Any:
    parts = text.split(",")
    try:
        return _geo.GeoPoint2(float(parts[0]), float(parts[1])) if len(parts) == 2 else text
    except ValueError:
        return text


def get_point(
    message: str = "Specify point",
    base: GeoPoint2 | None = None,
    keywords: Sequence[str] | None = None,
    default: str | None = None,
) -> GeoPoint2 | str | None:
    """Ask for a point, picked or typed; return a keyword as the prompt writes it, the default on Enter, else None."""
    answer = _ask(_cad.CadPrompt.ForPoint(message, base, _strings(keywords), default))
    kind = _kind(answer)
    if kind == "Point":
        return answer.Point
    if kind == "Keyword":
        return str(answer.Text)
    return None if default is None else _as_point(default)


def _number_or(answer: Any, default: str | None, convert: Callable[[float], float]) -> float | str | None:
    kind = _kind(answer)
    if kind == "Number":
        return float(answer.Number)
    if kind == "Keyword":
        return str(answer.Text)
    if default is None:
        return None
    try:
        return convert(float(default))
    except ValueError:
        return default


def get_distance(
    message: str = "Specify distance",
    base: GeoPoint2 | None = None,
    keywords: Sequence[str] | None = None,
    default: str | None = None,
) -> float | str | None:
    """Ask for a distance, typed or as two points; return a keyword as the prompt writes it, the default on Enter, else None."""
    return _number_or(_ask(_cad.CadPrompt.ForDistance(message, base, _strings(keywords), default)), default, float)


def get_angle(
    message: str = "Specify angle",
    base: GeoPoint2 | None = None,
    keywords: Sequence[str] | None = None,
    default: str | None = None,
) -> float | str | None:
    """Ask for an angle, typed in degrees or as a direction; return radians, a keyword, the default (degrees, as the prompt shows it) on Enter, else None."""
    return _number_or(_ask(_cad.CadPrompt.ForAngle(message, base, _strings(keywords), default)), default, math.radians)


def _keyworded(message: str, keywords: Sequence[str] | None) -> tuple[str, Any]:
    """Returns the message with the keywords bracketed, and a prompt that matches what is typed against them."""
    if not keywords:
        return message, None
    return f"{message} [{'/'.join(keywords)}]", _cad.CadPrompt.ForKeyword(message, _strings(keywords))


def _typed(
    message: str,
    keywords: Sequence[str] | None,
    default: object,
    pattern: re.Pattern[str],
    convert: Callable[[str], Any],
    refusal: str,
) -> Any:
    shown, matcher = _keyworded(message, keywords)
    while True:
        answer = _ask(_cad.CadPrompt.ForString(shown, None if default is None else str(default)))
        if _kind(answer) != "Text":
            return default
        text = str(answer.Text).strip()
        keyword = None if matcher is None else matcher.MatchKeyword(text)
        if keyword is not None:
            return str(keyword)
        if pattern.fullmatch(text):
            return convert(text)
        session().Write(refusal + (" or option keyword." if keywords else "."))


def get_integer(
    message: str = "Enter an integer", keywords: Sequence[str] | None = None, default: int | None = None
) -> int | str | None:
    """Ask for a whole number, asking again after text that is none; return a keyword, the default on Enter, else None."""
    result: int | str | None = _typed(message, keywords, default, _INTEGER, int, "Requires an integer value")
    return result


def get_real(
    message: str = "Enter a number", keywords: Sequence[str] | None = None, default: float | None = None
) -> float | str | None:
    """Ask for a number, asking again after text that is none; return a keyword, the default on Enter, else None."""
    result: float | str | None = _typed(message, keywords, default, _REAL, float, "Requires numeric value")
    return result


def get_string(message: str = "Enter text", default: str | None = None, spaces: bool = True) -> str | None:
    """Ask for a text; with ``spaces`` false it ends at the first space. Return the default on Enter, else None."""
    answer = _ask(_cad.CadPrompt.ForString(message, default))
    if _kind(answer) != "Text":
        return default
    text = str(answer.Text)
    return text if spaces or not text.strip() else text.split(None, 1)[0]


def get_keyword(message: str, keywords: Sequence[str], default: str | None = None) -> str | None:
    """Ask for one of the keywords; return it as the prompt writes it, or None on Enter with no default."""
    answer = _ask(_cad.CadPrompt.ForKeyword(message, _strings(keywords), default))
    return str(answer.Text) if _kind(answer) == "Keyword" else None


def get_entity(message: str = "Select object", keywords: Sequence[str] | None = None) -> str | None:
    """Ask for one object; return its handle, a keyword as the prompt writes it, or None on Enter."""
    answer = _ask(_cad.CadPrompt.ForEntity(message, _strings(keywords)))
    kind = _kind(answer)
    if kind == "Selection" and answer.Handles.Count > 0:
        return str(answer.Handles[0])
    return str(answer.Text) if kind == "Keyword" else None


def get_selection() -> list[str]:
    """Ask "Select objects:" until Enter; return the handles, or an empty list where none was selected."""
    return [str(handle) for handle in _call(_script().AskSelection)]


def _number_text(value: float) -> str:
    text = repr(float(value))
    if "inf" in text or "nan" in text:
        raise ValueError(f"{value} cannot be typed at a prompt.")
    return format(decimal.Decimal(text), "f") if "e" in text else text


def _answer(value: object) -> Any:
    """Turns one answer of ``command`` into what the host feeds."""
    if isinstance(value, str):
        return value
    if isinstance(value, bool):
        raise TypeError("command takes text, numbers, points and lists of handles, not a bool.")
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return _number_text(value)
    if isinstance(value, tuple):
        numbers: tuple[float, ...] = tuple(value)  # pyright: ignore[reportUnknownVariableType, reportUnknownArgumentType]
        if len(numbers) == 2:
            return _geo.GeoPoint2(float(numbers[0]), float(numbers[1]))
        if len(numbers) == 3:
            return f"{_number_text(numbers[0])},{_number_text(numbers[1])}"
        raise ValueError("A point is (x, y) or (x, y, z).")
    if isinstance(value, list):
        handles = cast(list[Any], value)
        return _net.Array[_net.String]([str(handle) for handle in handles])
    if isinstance(value, _geo.GeoPoint3):
        point: Any = value
        return f"{_number_text(point.X)},{_number_text(point.Y)}"
    return value


def command(*answers: object) -> None:
    """Run a command to its end inside the run's undo step; the first answer is its name, the others are fed in order.

    An empty text is Enter, a text is typed, a number is typed, a ``GeoPoint2`` or ``(x, y)`` is a point picked, a
    ``GeoPoint3`` or ``(x, y, z)`` is typed as ``x,y``, and a list of handles answers "Select objects:". A command that
    still asks after the last answer is cancelled and ``RuntimeError`` is raised.
    """
    fed = _net.Array[_net.Object]([_answer(answer) for answer in answers])
    _call(_script().Command, fed)


def cad_command(name: str, *, alias: str | None = None, description: str | None = None) -> Callable[[F], F]:
    """Make the decorated function a command of the drawings when its file is loaded with PYLOAD."""
    if not name or any(character.isspace() for character in name):
        raise ValueError("A command name is one word.")

    def decorate(function: F) -> F:
        if _loading is not None:
            _loading.append([name, alias, description, function])
        return function

    return decorate


def _begin_load() -> None:  # pyright: ignore[reportUnusedFunction]
    global _loading
    _loading = []


def _end_load() -> list[Any]:  # pyright: ignore[reportUnusedFunction]
    global _loading
    found = _loading or []
    _loading = None
    return found
