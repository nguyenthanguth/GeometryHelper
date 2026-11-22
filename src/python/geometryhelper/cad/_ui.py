"""What a Python file adds to the window of GeometryHelper CAD, as a C# plugin does: ribbon tabs and panels, buttons, menus
and Properties rows. Not for users: ``geometryhelper.cad`` re-exports the names.

The builders (``ribbon_button``, ``menu_item`` and the rest) make plain .NET objects of ``GeometryHelper.Cad.Plugins`` and
work anywhere. The ``add_*`` functions give those objects to the window and work only while the file loads, as a plugin's
``Initialize`` does: the loader hands each file a host of its own (``begin_load``), and closes it when the load ends.
"""

from __future__ import annotations

import importlib
from collections.abc import Callable, Sequence
from pathlib import Path
from typing import TYPE_CHECKING, Any, TypeVar

if TYPE_CHECKING:
    from ._drawing import Drawing, Entity

T = TypeVar("T")

_net: Any = importlib.import_module("System")
_generic: Any = importlib.import_module("System.Collections.Generic")
_plugins: Any = importlib.import_module("GeometryHelper.Cad.Plugins")

try:
    importlib.import_module("GeometryHelper.Cad.Python")
    _in_app = True
except ImportError:
    _in_app = False


class Load:
    """What a file defines while it loads: its commands, its unload hooks, and the host its additions go to."""

    def __init__(self, host: Any, path: str) -> None:
        self.host = host
        self.file = Path(path).name
        self.commands: list[list[Any]] = []
        self.unloaders: list[Callable[[], object]] = []


current: Load | None = None


def begin_load(host: Any, path: str) -> None:
    """Starts a load: the file's own plugin host takes the additions of the module-level code."""
    global current
    current = Load(host, path)


def end_load() -> Load:
    """Ends the load and gives what it found; later calls of ``add_*`` raise."""
    global current
    found = current or Load(None, "")
    current = None
    return found


def _take(name: str) -> Any:
    """Returns the host of the load going on, or raises where there is none."""
    if not _in_app:
        raise RuntimeError(f"{name} works inside GeometryHelper CAD only")
    if current is None or current.host is None:
        raise RuntimeError(
            f"{name} adds to the window only while its file loads: call it at the top of the file, not from a command."
        )
    return current.host


def _list(item_type: Any, items: Sequence[Any]) -> Any:
    """Makes a .NET ``List<item_type>`` of the items, the shape the C# constructors take."""
    made = _generic.List[item_type]()
    for item in items:
        made.Add(item)
    return made


class Context:
    """What a callback of the window is given, as a plugin's ``CadPluginContext`` gives it, with Python's objects.

    ``drawing`` is the drawing shown (``None`` when none is open), ``selected`` the entities selected in it,
    ``is_command_running`` whether a command waits at a prompt, ``write(text)`` writes a line on the command line and
    ``run_command(text)`` types a command there, as a button of the ribbon does.

        def hello(context: Context) -> None:
            context.write(f"{context.drawing.name}: {len(context.selected)} selected")

    Example:
        def hello(context: Context) -> None:
            drawing = context.drawing
            context.write(f"{drawing.name if drawing else '-'}: {len(context.selected)} selected")   # plan.dxf: 2 selected
    """

    def __init__(self, native: Any) -> None:
        self._native = native
        self._selected: list[Entity] | None = None

    @property
    def drawing(self) -> Drawing | None:
        """The drawing the callback was called for; ``None`` where no drawing is open.

        Example:
            context.drawing.name        # 'plan.dxf'; context.drawing is None when no drawing is open
        """
        from ._local import drawing_of

        document = self._native.Document
        return None if document is None else drawing_of(document)

    @property
    def selected(self) -> list[Entity]:
        """The entities selected in that drawing, in the order the window lists them.

        Example:
            [e.handle for e in context.selected]      # ['2B', '2C'] for two objects selected in the drawing shown
        """
        if self._selected is None:
            from ._local import entity_of

            document = self._native.Document
            self._selected = [] if document is None else [entity_of(document, str(h)) for h in self._native.Selected]
        return self._selected

    @property
    def is_command_running(self) -> bool:
        """Whether a command is running in the window.

        Example:
            if not context.is_command_running:        # False while the command line is at the Command prompt
                context.run_command("ZOOM E")
        """
        return bool(self._native.IsCommandRunning)

    def write(self, text: str) -> None:
        """Write a line on the command line of the window.

        Example:
            context.write("checked 12 drawings")      # one line on the command line
        """
        self._native.Write(text)

    def run_command(self, text: str) -> None:
        """Type a command on the command line, as if the user had: ``context.run_command("CIRCLE")``.

        Example:
            context.run_command("CIRCLE")             # the user is now asked for the centre of a circle
        """
        self._native.RunCommand(text)


class DrawingsRequest:
    """What a DRAWINGS menu provider is given for the row clicked: ``path`` of its file, ``drawing`` (``None`` while the
    file is not open) and ``selected_paths``, the paths of every row selected.

    Example:
        def menu(request: DrawingsRequest) -> list[Any]:
            name = request.drawing.name if request.drawing else 'closed'   # 'plan.dxf', or 'closed'
            return [menu_item(f"{name}: {len(request.selected_paths)} rows", run=lambda c: None)]
    """

    def __init__(self, native: Any) -> None:
        self._native = native

    @property
    def path(self) -> str:
        """The full path of the row's file.

        Example:
            request.path                  # 'C:\\\\plans\\\\a.dxf'
        """
        return str(self._native.Path)

    @property
    def drawing(self) -> Drawing | None:
        """The drawing of that file where it is open, else ``None``.

        Example:
            request.drawing               # None while C:\\plans\\a.dxf is not open, else its Drawing
        """
        from ._local import drawing_of

        document = self._native.Document
        return None if document is None else drawing_of(document)

    @property
    def selected_paths(self) -> list[str]:
        """The full paths of all the rows selected, the clicked one among them.

        Example:
            len(request.selected_paths)   # 2 when two rows are selected; the clicked row is one of them
        """
        return [str(path) for path in self._native.SelectedPaths]


def _guarded(function: Callable[..., T], fallback: T, *arguments: object, commits: bool = False) -> T:
    """Calls a callback of the window under the run guard: an error is one line on the command line, never a crash, and
    gives the fallback. An action (``commits``) commits what it queued, when no run is going on, and drops it on error."""
    app: Any = importlib.import_module("geometryhelper._app")
    try:
        result = function(*arguments)
        if commits:
            app.flush_callback()
    except BaseException as error:
        app.report(error, function)
        if commits:
            app.discard_callback()
        return fallback
    return result


def _action(function: Callable[[Context], object]) -> Any:
    """A ``System.Action<CadPluginContext>`` that runs the function with a ``Context``."""

    def call(native: Any) -> None:
        _guarded(function, None, Context(native), commits=True)

    return _net.Action[_plugins.CadPluginContext](call)


def _test(function: Callable[[Context], object]) -> Any:
    """A ``System.Func<CadPluginContext, bool>`` that asks the function with a ``Context``; an error counts as false."""

    def call(native: Any) -> bool:
        return bool(_guarded(function, False, Context(native)))

    return _net.Func[_plugins.CadPluginContext, _net.Boolean](call)


def _items(items: Sequence[Any] | None) -> Any:
    """A .NET list of the menu items a provider returned."""
    return _list(_plugins.CadPluginMenuItem, list(items) if items else [])


def icon_from_path_data(data: str) -> Any:
    """Make an icon from SVG-style path data in a 24 by 24 box: ``icon_from_path_data("M4 12 H20 M12 4 V20")`` is a plus.

    Example:
        plus = icon_from_path_data("M4 12 H20 M12 4 V20")      # a plus in a 24 by 24 box; PathData is that text
    """
    return _plugins.CadPluginIcon.FromPathData(data)


def icon_from_image(data: bytes) -> Any:
    """Make an icon from the bytes of a PNG, JPEG or BMP image: ``icon_from_image(Path("circle16.png").read_bytes())``.

    Example:
        icon = icon_from_image(Path("circle16.png").read_bytes())   # the bytes of the file are the icon's Image
    """
    return _plugins.CadPluginIcon.FromImage(_net.Array[_net.Byte](list(data)))


def icon_from_file(path: str) -> Any:
    """Make an icon from an image file: ``icon_from_file(r"C:\\tools\\circle16.png")``.

    Example:
        icon = icon_from_file(r"C:\\tools\\circle16.png")
    """
    return _plugins.CadPluginIcon.FromFile(path)


def ribbon_button(
    text: str,
    command: str | None = None,
    run: Callable[[Context], object] | None = None,
    can_run: Callable[[Context], object] | None = None,
    icon: Any = None,
    size: str = "standard",
    show_text: bool = True,
    tooltip: str | None = None,
    description: str | None = None,
) -> Any:
    """Make a ribbon button that starts a command (``command="CIRCLE"``) or calls a function (``run=hello``).

    Give one of ``command`` and ``run``. ``can_run(context)`` says whether the button is enabled and counts as false when it
    raises. ``size`` is ``"standard"`` (16 pixel icon beside the text) or ``"large"`` (32 pixels over it).

        ribbon_button("Circle", command="CIRCLE", icon=icon_from_path_data("M4 12 H20"), size="large")

    Example:
        ribbon_button("Circle", command="CIRCLE", icon=icon_from_path_data("M4 12 H20 M12 4 V20"), size="large")
                                      # a large button Circle that starts CIRCLE; the icon is a plus
    """
    if (command is None) == (run is None):
        raise ValueError(f"ribbon_button needs exactly one of command and run, got command={command!r} and run={run!r}.")
    sizes = {"standard": _plugins.CadRibbonItemSize.Standard, "large": _plugins.CadRibbonItemSize.Large}
    if size not in sizes:
        raise ValueError(f"ribbon_button size is 'standard' or 'large', not {size!r}.")
    button = (
        _plugins.CadRibbonButton(text, command)
        if run is None
        else _plugins.CadRibbonButton(text, _action(run))
    )
    if can_run is not None:
        button.CanRun = _test(can_run)
    if icon is not None:
        button.Icon = icon
    button.Size = sizes[size]
    button.ShowText = show_text
    button.ToolTip = tooltip
    button.Description = description
    return button


def split_button(text: str, buttons: Sequence[Any], remembers_last: bool = True) -> Any:
    """Make a split button: the top runs the button used last (or always the first with ``remembers_last=False``) and the
    arrow lists them all. ``buttons`` are made by ``ribbon_button``, at least one.

    Example:
        split_button("More", [ribbon_button("One", command="LINE"), ribbon_button("Two", command="CIRCLE")])
                                      # the top runs the button used last; the arrow lists One and Two
    """
    split = _plugins.CadRibbonSplitButton(text, _list(_plugins.CadRibbonButton, buttons))
    split.RemembersLast = remembers_last
    return split


def ribbon_panel(title: str, items: Sequence[Any]) -> Any:
    """Make a ribbon panel holding the buttons and split buttons given: ``ribbon_panel("Draw", [circle, more])``.

    Example:
        ribbon_panel("Draw", [ribbon_button("Circle", command="CIRCLE"), ribbon_button("Hello", run=lambda c: None)])
                                      # a panel Draw with two buttons, Circle then Hello
    """
    panel = _plugins.CadRibbonPanel(title)
    for item in items:
        panel.Items.Add(item)
    return panel


def ribbon_tab(
    id: str,
    title: str,
    panels: Sequence[Any],
    show_when: Callable[[Context], object] | None = None,
) -> Any:
    """Make a ribbon tab. ``id`` is unique among the tabs of all files and plugins; ``show_when(context)`` makes it a
    contextual tab shown while it returns true (and hidden when it raises).

    Example:
        ribbon_tab("tools", "Tools", [ribbon_panel("Draw", [])])           # id "tools", title "Tools"
        ribbon_tab("lines", "Lines", [], show_when=lambda c: bool(c.selected))   # shown while something is selected
    """
    tab = _plugins.CadRibbonTab(id, title)
    for panel in panels:
        tab.Panels.Add(panel)
    if show_when is not None:
        tab.ShowWhen = _test(show_when)
    return tab


def menu_item(
    text: str,
    run: Callable[[Context], object] | None = None,
    enabled: bool = True,
    checked: bool | None = None,
    items: Sequence[Any] | None = None,
) -> Any:
    """Make a menu item that calls ``run``, or opens a submenu of ``items`` made by ``menu_item``. ``checked`` is ``None``
    for no tick, ``True`` for a ticked item and ``False`` for a tick box not ticked.

    Example:
        menu_item("Selected 1", run=lambda c: None)                       # a plain item
        menu_item("Sub", items=[menu_item("Inner", run=lambda c: None)])   # a submenu with one item, Inner
        menu_item("Grid", run=lambda c: None, checked=True)                # a ticked item
    """
    if (run is None) == (items is None):
        raise ValueError(f"menu_item needs exactly one of run and items, got run={run!r} and items={items!r}.")
    if items is not None:
        return _plugins.CadPluginMenuItem(text, _list(_plugins.CadPluginMenuItem, items), enabled, checked)
    assert run is not None
    return _plugins.CadPluginMenuItem(text, _action(run), enabled, checked)


def separator() -> Any:
    """Make a separator line for a menu.

    Example:
        menu_item("A", run=lambda c: None), separator(), menu_item("B", run=lambda c: None)   # a line between A and B
    """
    return _plugins.CadPluginMenuItem.Separator()


def add_ribbon_tab(tab: Any) -> None:
    """Add a tab made by ``ribbon_tab`` to the ribbon, after the Plugins tab. Works while the file loads only.

    Example:
        circle = ribbon_button("Circle", command="CIRCLE")
        add_ribbon_tab(ribbon_tab("tools", "Tools", [ribbon_panel("Draw", [circle])]))   # a tab Tools with one panel Draw
    """
    _take("add_ribbon_tab").AddRibbonTab(tab)


def add_ribbon_panel(panel: Any) -> None:
    """Add a panel made by ``ribbon_panel`` to the Plugins tab, which shows while a file has added one. Works while the
    file loads only.

    Example:
        add_ribbon_panel(ribbon_panel("Extras", [ribbon_button("Line", command="LINE")]))   # a panel Extras on the Plugins tab
    """
    _take("add_ribbon_panel").AddRibbonPanel(panel)


def add_drawings_button(
    icon: Any,
    tooltip: str,
    run: Callable[[Context], object],
    can_run: Callable[[Context], object] | None = None,
) -> None:
    """Add a button to the header of the DRAWINGS list. Works while the file loads only.

    Example:
        add_drawings_button(icon_from_path_data("M4 12 H20"), "Check drawing", lambda c: c.write("checked"))
                                      # a button on the DRAWINGS list; clicking it writes 'checked'
    """
    host = _take("add_drawings_button")
    button = _plugins.CadPluginButton(icon, tooltip, _action(run))
    if can_run is not None:
        button.CanRun = _test(can_run)
    host.AddDrawingsButton(button)


def add_drawings_menu(provider: Callable[[DrawingsRequest], Sequence[Any]]) -> None:
    """Add items to the right-click menu of a DRAWINGS row. ``provider(request)`` returns ``menu_item`` objects each time
    the menu opens; it gives none when it raises. Works while the file loads only.

    Example:
        def rows(request: DrawingsRequest) -> list[Any]:
            return [menu_item(f"{request.path}|{len(request.selected_paths)}", run=lambda c: None)]
        add_drawings_menu(rows)       # one item in the menu of a row: 'C:\\\\plans\\\\a.dxf|2' with two rows selected
    """
    host = _take("add_drawings_menu")

    def call(native: Any) -> Any:
        return _items(_guarded(provider, [], DrawingsRequest(native)))

    host.AddDrawingsMenu(_plugins.CadDrawingsMenuProvider(call))


def add_canvas_menu(provider: Callable[[Context], Sequence[Any]]) -> None:
    """Add items to the right-click menu of the canvas. ``provider(context)`` returns ``menu_item`` objects each time the
    menu opens; it gives none when it raises. Works while the file loads only.

    Example:
        def items(context: Context) -> list[Any]:
            return [menu_item(f"Selected {len(context.selected)}", run=lambda c: None), separator()]
        add_canvas_menu(items)        # 'Selected 1' with one object selected, then a separator
    """
    host = _take("add_canvas_menu")

    def call(native: Any) -> Any:
        return _items(_guarded(provider, [], Context(native)))

    host.AddCanvasMenu(_plugins.CadCanvasMenuProvider(call))


def add_properties(provider: Callable[[Any], Any]) -> None:
    """Add rows to Properties. ``provider(request)`` is given the .NET ``CadPropertyRequest`` and returns the .NET
    ``CadSelectionInfo`` to show (or ``None`` to leave the rows as they are, as it does when it raises). Works while the file
    loads only.

    Example:
        def rows(request: Any) -> Any:
            request.Banner = "from python"      # the .NET CadPropertyRequest, changed in place
            return None                          # None leaves the rows as they are
        add_properties(rows)
    """
    host = _take("add_properties")

    def call(native: Any) -> Any:
        return _guarded(provider, None, native)

    host.AddProperties(_plugins.CadPropertyProvider(call))


def show_command_buttons(shown: bool) -> None:
    """Say whether this file's commands get a button each, as a plugin's ``ShowsCommandButtons`` does. Works while the
    file loads only.

    Example:
        show_command_buttons(False)   # the commands of this file get no button of their own on the ribbon
    """
    _take("show_command_buttons").ShowsCommandButtons = shown
