"""Python in GeometryHelper CAD: commands, prompts, the drawing, and the window, as a C# plugin has them.

Import it anywhere. Inside the app a ``.py`` file loaded with APPLOAD, PYLOAD, the Pythons folder or a drop defines commands
with ``@command`` and adds to the window with the ``add_*`` functions; PYRUN runs a file once. The functions that talk to
the app (``get_point`` and the other prompts, ``cmd``, ``active_drawing``, ``documents``, the ``add_*`` functions) raise
``RuntimeError`` outside it; ``open_file`` and ``new_file`` work on a drawing file with no app, and ``connect`` drives a running
app from outside. Esc at a prompt raises ``Cancelled``, which a bare ``except Exception`` does not swallow. Units are the
drawing's; nothing is converted.

    from geometryhelper.cad import active_drawing, command, get_distance, get_point

    @command("RING", alias="RG", description="Draws a circle of the radius asked")
    def ring() -> None:
        centre = get_point("Specify centre")
        radius = get_distance("Specify radius", base=centre)
        drawing = active_drawing()
        drawing.add_circle(centre, radius)
        drawing.commit()
"""

from __future__ import annotations

import decimal
import importlib
import math
import re
import sys
from collections.abc import Callable, Sequence
from typing import TYPE_CHECKING, Any, TypeVar, cast

from . import _ui
from ._ui import (
    Context as Context,
    DrawingsRequest as DrawingsRequest,
    add_canvas_menu as add_canvas_menu,
    add_drawings_button as add_drawings_button,
    add_drawings_menu as add_drawings_menu,
    add_properties as add_properties,
    add_ribbon_panel as add_ribbon_panel,
    add_ribbon_tab as add_ribbon_tab,
    icon_from_file as icon_from_file,
    icon_from_image as icon_from_image,
    icon_from_path_data as icon_from_path_data,
    menu_item as menu_item,
    ribbon_button as ribbon_button,
    ribbon_panel as ribbon_panel,
    ribbon_tab as ribbon_tab,
    separator as separator,
    show_command_buttons as show_command_buttons,
    split_button as split_button,
)

if TYPE_CHECKING:
    from GeometryHelper.Cad import CadCommandSession
    from GeometryHelper.Geometry import GeoPoint2

try:
    _host: Any = importlib.import_module("GeometryHelper.Cad.Python")
except ImportError:
    _host = None

F = TypeVar("F", bound=Callable[..., object])

_net: Any = importlib.import_module("System")
_cad: Any = importlib.import_module("GeometryHelper.Cad")
_geo: Any = importlib.import_module("GeometryHelper.Geometry")
_INTEGER = re.compile(r"[+-]?[0-9]+", re.ASCII)
_REAL = re.compile(r"[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", re.ASCII)

PAUSE: str = "\\"
"""The answer that hands one prompt of a command fed by ``cmd`` to the user: ``cmd("CIRCLE", PAUSE, 50)`` lets the user pick
the centre and gives the radius 50. It is one backslash, as AutoLISP's ``pause`` is. Esc at that prompt raises ``Cancelled``."""


class Cancelled(KeyboardInterrupt):
    """Esc at a prompt, or a prompt asked after it: the run is cancelled and the drawing is as before it."""


def _script(name: str) -> Any:
    """Returns the script of the run going on; raises ``RuntimeError`` naming the function where there is none."""
    if _host is None:
        raise RuntimeError(f"{name} works inside GeometryHelper CAD only")
    script = _host.CadPythonScript.Current
    if script is None:
        raise RuntimeError(f"{name} works only while a script runs in GeometryHelper CAD.")
    return script


def session() -> CadCommandSession:
    """Return the .NET command session of the drawing the run started in: the raw escape hatch under the Drawing API."""
    return _script("session").Session


def write(text: str) -> None:
    """Write one line or more on the command line: ``write("12 circles drawn")``."""
    _script("write").Write(text)


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


def _ask(name: str, prompt: Any) -> Any:
    return _call(_script(name).Ask, prompt)


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
    """Ask for a point, picked or typed; return a keyword as the prompt writes it, the default on Enter, else None.

        centre = get_point("Specify centre")
        corner = get_point("Specify other corner", base=centre, keywords=["Undo"])
    """
    answer = _ask("get_point", _cad.CadPrompt.ForPoint(message, base, _strings(keywords), default))
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
    """Ask for a distance, typed or as two points; return a keyword as the prompt writes it, the default on Enter, else None.

        radius = get_distance("Specify radius", base=centre)   # the user types 50 or picks a point 50 mm away
    """
    return _number_or(
        _ask("get_distance", _cad.CadPrompt.ForDistance(message, base, _strings(keywords), default)), default, float
    )


def get_angle(
    message: str = "Specify angle",
    base: GeoPoint2 | None = None,
    keywords: Sequence[str] | None = None,
    default: str | None = None,
) -> float | str | None:
    """Ask for an angle, typed in degrees or as a direction; return radians, a keyword, the default (degrees, as the prompt shows it) on Enter, else None.

        turn = get_angle("Specify rotation")   # the user types 90 and gets 1.5707963267948966
    """
    return _number_or(
        _ask("get_angle", _cad.CadPrompt.ForAngle(message, base, _strings(keywords), default)),
        default,
        math.radians,
    )


def _keyworded(message: str, keywords: Sequence[str] | None) -> tuple[str, Any]:
    """Returns the message with the keywords bracketed, and a prompt that matches what is typed against them."""
    if not keywords:
        return message, None
    return f"{message} [{'/'.join(keywords)}]", _cad.CadPrompt.ForKeyword(message, _strings(keywords))


def _typed(
    name: str,
    message: str,
    keywords: Sequence[str] | None,
    default: object,
    pattern: re.Pattern[str],
    convert: Callable[[str], Any],
    refusal: str,
) -> Any:
    _script(name)
    shown, matcher = _keyworded(message, keywords)
    while True:
        answer = _ask(name, _cad.CadPrompt.ForString(shown, None if default is None else str(default)))
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
    """Ask for a whole number, asking again after text that is none; return a keyword, the default on Enter, else None.

        count = get_integer("How many", default=3)
    """
    result: int | str | None = _typed(
        "get_integer", message, keywords, default, _INTEGER, int, "Requires an integer value"
    )
    return result


def get_real(
    message: str = "Enter a number", keywords: Sequence[str] | None = None, default: float | None = None
) -> float | str | None:
    """Ask for a number, asking again after text that is none; return a keyword, the default on Enter, else None.

        factor = get_real("Scale factor", ["Auto"], 1.0)
    """
    result: float | str | None = _typed("get_real", message, keywords, default, _REAL, float, "Requires numeric value")
    return result


def get_string(message: str = "Enter text", default: str | None = None, spaces: bool = True) -> str | None:
    """Ask for a text; with ``spaces`` false it ends at the first space. Return the default on Enter, else None.

        name = get_string("Name of the sheet", "A1", spaces=False)
    """
    answer = _ask("get_string", _cad.CadPrompt.ForString(message, default))
    if _kind(answer) != "Text":
        return default
    text = str(answer.Text)
    return text if spaces or not text.strip() else text.split(None, 1)[0]


def get_keyword(message: str, keywords: Sequence[str], default: str | None = None) -> str | None:
    """Ask for one of the keywords; return it as the prompt writes it, or None on Enter with no default.

        mode = get_keyword("Mode", ["Yes", "No"], "No")
    """
    answer = _ask("get_keyword", _cad.CadPrompt.ForKeyword(message, _strings(keywords), default))
    return str(answer.Text) if _kind(answer) == "Keyword" else None


def _entity(handle: str) -> Entity:
    """The entity of the run's drawing with the handle, as the Drawing API gives it."""
    from ._local import entity_of

    return entity_of(_script("get_entity").Document, handle)


def get_entity(message: str = "Select object", keywords: Sequence[str] | None = None) -> Entity | str | None:
    """Ask for one object; return its ``Entity``, a keyword as the prompt writes it, or None on Enter.

        line = get_entity("Select a line")
        if line is not None:
            print(line.handle, line.dxf_type)   # 2B LINE
    """
    answer = _ask("get_entity", _cad.CadPrompt.ForEntity(message, _strings(keywords)))
    kind = _kind(answer)
    if kind == "Selection" and answer.Handles.Count > 0:
        return _entity(str(answer.Handles[0]))
    return str(answer.Text) if kind == "Keyword" else None


def get_selection() -> list[Entity]:
    """Ask "Select objects:" until Enter; return the ``Entity`` objects, or an empty list where none was selected.

    In a command made with ``@command(..., uses_selection=True)`` it returns the objects selected before the command started
    without asking, as MOVE does, and asks only when none were.

        entities = get_selection()
        print(len(entities), "selected")   # 3 selected
    """
    handles = _call(_script("get_selection").AskSelection)
    return [_entity(str(handle)) for handle in handles]


def _number_text(value: float) -> str:
    text = repr(float(value))
    if "inf" in text or "nan" in text:
        raise ValueError(f"{value} cannot be typed at a prompt.")
    return format(decimal.Decimal(text), "f") if "e" in text else text


def _answer(value: object) -> Any:
    """Turns one answer of ``cmd`` into what the host feeds."""
    if isinstance(value, str):
        return value
    if isinstance(value, bool):
        raise TypeError("cmd takes text, numbers, points and lists of handles, not a bool.")
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
        return _net.Array[_net.String]([str(getattr(handle, "handle", handle)) for handle in handles])
    if isinstance(value, _geo.GeoPoint3):
        point: Any = value
        return f"{_number_text(point.X)},{_number_text(point.Y)}"
    return value


def cmd(*answers: object) -> None:
    """Run a command to its end inside the run's undo step; the first answer is its name, the others are fed in order.

    An empty text is Enter, a text is typed, a number is typed, a ``GeoPoint2`` or ``(x, y)`` is a point picked, a
    ``GeoPoint3`` or ``(x, y, z)`` is typed as ``x,y``, and a list of handles (or of entities) answers "Select objects:".
    ``PAUSE`` hands that one prompt to the user. A command that still asks after the last answer is cancelled and
    ``RuntimeError`` is raised.

        cmd("CIRCLE", (10, 20), 50)        # a circle of radius 50 at 10,20
        cmd("CIRCLE", PAUSE, 50)           # the user picks the centre
    """
    fed = _net.Array[_net.Object]([_answer(answer) for answer in answers])
    _call(_script("cmd").Command, fed)


def command(
    name: str,
    *,
    alias: str | None = None,
    description: str | None = None,
    uses_selection: bool = False,
) -> Callable[[F], F]:
    """Make the decorated function a command of every drawing when its file is loaded.

    The command is typed at the command line (or started from a ribbon button) by ``name`` or by ``alias``; it runs as one
    undo step named after it. ``uses_selection=True`` makes ``get_selection()`` return the objects selected before the
    command started, without asking.

        @command("RING", alias="RG", description="Draws a circle", uses_selection=False)
        def ring() -> None:
            ...
    """
    if not name or any(character.isspace() for character in name):
        raise ValueError(f"A command name is one word, not {name!r}.")

    def decorate(function: F) -> F:
        if _ui.current is not None:
            _ui.current.commands.append([name, alias, description, function, uses_selection])
        return function

    return decorate


def on_unload(function: F) -> F:
    """Run the decorated function when its file is unloaded, loaded again or the app closes: the twin of a plugin's
    ``Terminate``. An error in it is one line on the command line and the unload goes on.

        @on_unload
        def bye() -> None:
            print("tools.py is gone")
    """
    if _ui.current is not None:
        _ui.current.unloaders.append(function)
    return function


def _begin_load(host: Any, path: str) -> None:  # pyright: ignore[reportUnusedFunction]
    _ui.begin_load(host, path)


def _end_load() -> _ui.Load:  # pyright: ignore[reportUnusedFunction]
    return _ui.end_load()


# The Drawing API (D1655 to D1658).
from ._drawing import (  # noqa: E402
    Documents as Documents,
    Drawing as Drawing,
    Entity as Entity,
    Layer as Layer,
)
from ._local import (  # noqa: E402
    active_drawing as active_drawing,
    documents as documents,
    new_drawing as new_drawing,
    new_file as new_file,
    open_drawing as open_drawing,
    open_file as open_file,
)
from ._remote import App as App, connect as connect  # noqa: E402
