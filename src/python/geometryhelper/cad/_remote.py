"""The Drawing API over a running GeometryHelper CAD: ``connect()`` and ``App``, over ``CadApplication``.

A script written for the app, or for a file, runs here with only ``connect()`` changed. The queue is the ``Drawing``'s own and
reaches the app at ``commit()`` through ``CadRemoteDocument.Insert``, ``Modify``, ``Delete`` and ``CommitChanges``, which
the app applies as one undo step; what a script leaves queued when it ends is lost.

    with connect() as app:
        drawing = app.new()
        drawing.add_circle((50, 50), 25)
        drawing.commit()
"""

from __future__ import annotations

import importlib
from types import TracebackType
from typing import Any

from ._drawing import Backend, Documents, Drawing, Look, Op, look_color

_net: Any = importlib.import_module("System")
_dxf: Any = importlib.import_module("GeometryHelper.Dxf")
_remote: Any = importlib.import_module("GeometryHelper.Cad.Remote")


def _layer_of(look: Look) -> Any:
    """The ``CadRemoteLayer`` a look stands for."""
    layer = _remote.CadRemoteLayer(look.name)
    layer.IsOn = look.is_on
    layer.IsFrozen = look.is_frozen
    layer.IsLocked = look.is_locked
    layer.IsPlottable = look.is_plottable
    layer.Color = look_color(look)
    layer.Linetype = look.linetype
    layer.LineWeight = _dxf.DxfLineWeight(look.line_weight)
    layer.Description = look.description
    return layer


class _RemoteBackend:
    """A ``CadRemoteDocument`` as the backend of a ``Drawing``."""

    def __init__(self, app: Any, document: Any) -> None:
        self._app = app
        self._document = document

    def _refresh(self) -> None:
        """Reads the list of drawings, which updates which is shown and which has changes."""
        for _ in self._app.Documents:
            pass

    @property
    def name(self) -> str:
        return str(self._document.Name)

    @property
    def path(self) -> str | None:
        path = str(self._document.Path)
        return path if path else None

    @property
    def is_active(self) -> bool:
        self._refresh()
        return bool(self._document.IsActive)

    @property
    def is_modified(self) -> bool:
        self._refresh()
        return bool(self._document.IsModified)

    @property
    def net(self) -> Any:
        return self._document

    def _pairs(self, entities: Any) -> list[tuple[str, Any, Any]]:
        return [(str(self._document.HandleOf(entity)), entity, None) for entity in entities]

    def read(self, dxf_type: str | None, layer: str | None, layout: str | None) -> list[tuple[str, Any, Any]]:
        if layer is None and layout is None:
            return self._pairs(self._document.GetObjects())
        selector = _remote.CadObjectFilter()
        selector.Layer = layer
        selector.Layout = layout
        return self._pairs(self._document.GetObjects(selector))

    def read_one(self, handle: str) -> tuple[Any, Any] | None:
        found = self._document.GetObject(handle)
        return None if found is None else (found, None)

    def selected(self) -> list[tuple[str, Any, Any]]:
        return self._pairs(self._document.GetSelectedObjects())

    def select(self, handles: list[str]) -> None:
        self._document.Select(_net.Array[_net.String](handles))

    def commit(self, ops: list[Op]) -> list[str | None]:
        document = self._document
        added = 0
        for op in ops:
            if op.kind == "insert":
                document.Insert(op.copy, op.layout)
                added += 1
            elif op.kind == "modify":
                assert op.entity is not None
                document.Modify(op.entity.net)
            elif op.kind == "erase":
                document.Delete(op.handle)
            elif op.kind == "add_layer":
                assert op.layer is not None
                document.AddLayer(_layer_of(op.layer._look))  # pyright: ignore[reportPrivateUsage]
            else:
                assert op.layer is not None
                document.ModifyLayer(_layer_of(op.layer._look))  # pyright: ignore[reportPrivateUsage]
        handles: list[str | None] = [str(handle) for handle in document.CommitChanges()]
        return handles + [None] * (added - len(handles))

    def layers(self) -> list[Look]:
        looks: list[Look] = []
        for layer in self._document.Layers:
            color = layer.Color
            looks.append(
                Look(
                    name=str(layer.Name),
                    color=int(color.ColorIndex),
                    true_color=int(color.ColorValue) if color.IsByColor else None,
                    linetype=str(layer.Linetype),
                    line_weight=int(layer.LineWeight),
                    description=str(layer.Description),
                    is_on=bool(layer.IsOn),
                    is_frozen=bool(layer.IsFrozen),
                    is_locked=bool(layer.IsLocked),
                    is_plottable=bool(layer.IsPlottable),
                )
            )
        return looks

    @property
    def current_layer(self) -> str:
        return str(self._document.CurrentLayer)

    @current_layer.setter
    def current_layer(self, value: str) -> None:
        self._document.CurrentLayer = value

    @property
    def layouts(self) -> list[str]:
        return [str(name) for name in self._document.Layouts]

    @property
    def current_layout(self) -> str:
        return str(self._document.CurrentLayout)

    @current_layout.setter
    def current_layout(self, value: str) -> None:
        self._document.CurrentLayout = value

    @property
    def text_styles(self) -> list[Any]:
        return list(self._document.TextStyles)

    @property
    def dimension_styles(self) -> list[Any]:
        return list(self._document.DimensionStyles)

    @property
    def blocks(self) -> list[Any]:
        return list(self._document.Blocks)

    def pick_point(self, message: str, base: Any) -> Any:
        return self._document.PickPoint(message, base)

    def pick_object(self, message: str) -> tuple[str, Any, Any] | None:
        entity = self._document.PickObject(message)
        return str(self._document.HandleOf(entity)), entity, None

    def pick_objects(self, message: str) -> list[tuple[str, Any, Any]]:
        return self._pairs(self._document.PickObjects(message))

    def save(self) -> None:
        self._document.Save()

    def save_as(self, path: str, version: str | None) -> None:
        self._document.SaveAs(path, version)

    def close(self, discard: bool) -> None:
        self._document.Close(discard)


class App:
    """A running GeometryHelper CAD, to use as ``with connect() as app``: ``documents``, ``active_drawing``, ``open``,
    ``new`` and ``run_command``. Leaving the ``with`` ends the connection and drops what the drawings still have queued.

    ``App(cad_application)`` takes a ``CadApplication`` already made, which ``connect()`` makes for you::

        with connect() as app:
            print([drawing.name for drawing in app.documents])   # ['plan.dxf', 'Drawing2.dxf']
    """

    def __init__(self, cad_application: Any) -> None:
        self._app = cad_application
        self._drawings: dict[int, Drawing] = {}

    def __enter__(self) -> App:
        return self

    def __exit__(
        self, error_type: type[BaseException] | None, error: BaseException | None, traceback: TracebackType | None
    ) -> None:
        for drawing in self._drawings.values():
            drawing._discard()  # pyright: ignore[reportPrivateUsage]
        self._app.Dispose()

    def _drawing(self, document: Any) -> Drawing:
        key = int(document.Id)
        drawing = self._drawings.get(key)
        if drawing is None:
            backend: Backend = _RemoteBackend(self._app, document)
            drawing = Drawing(backend)
            self._drawings[key] = drawing
        return drawing

    def _listed(self) -> list[Drawing]:
        return [self._drawing(document) for document in self._app.Documents]

    @property
    def documents(self) -> Documents:
        """The drawings the app has open, in its order."""
        return Documents(self._listed)

    @property
    def active_drawing(self) -> Drawing | None:
        """The drawing shown, or None where the app has none open."""
        document = self._app.ActiveDocument
        return None if document is None else self._drawing(document)

    def open(self, path: str) -> Drawing:
        """Open a drawing file in the app, which shows it: ``app.open("plan.dxf")``."""
        return self._drawing(self._app.Open(path))

    def new(self, template: str | None = None) -> Drawing:
        """Make a new drawing in the app, from a template file where one is given, and show it."""
        return self._drawing(self._app.New(template))

    def run_command(self, drawing: Drawing, text: str) -> Any:
        """Type ``text`` at the command line of a drawing, as a user types it (``"\\n"`` is Enter), and return the
        ``CadRemoteCommandResult``: ``app.run_command(drawing, "ZOOM E\\n")``."""
        return drawing.net.SendCommand(text)


def connect(pid: int | None = None, drawing: str | None = None) -> App:
    """Connect to a running GeometryHelper CAD: the only one, the one with process id ``pid``, or the one that has the file
    ``drawing`` open. It needs "Allow other programs to connect" on in the app's Global settings::

        with connect() as app:
            app.new().add_line((0, 0), (100, 0))
    """
    CadApplication = _remote.CadApplication
    if pid is not None and drawing is not None:
        raise ValueError(f"Give the process id ({pid}) or the drawing ({drawing!r}), not both.")
    if drawing is not None:
        return App(CadApplication.ForDocument(drawing))
    if pid is None:
        return App(CadApplication())
    for running in CadApplication.GetRunning():
        if int(running.ProcessId) == pid:
            return App(CadApplication(running))
    raise RuntimeError(f"No running GeometryHelper CAD has the process id {pid}.")
