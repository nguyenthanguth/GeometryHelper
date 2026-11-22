"""The Drawing API over a ``CadDocument``: a drawing of the app (inside a run) and a drawing file (headless).

The commit goes through the public ``CadDocument.ApplyEdit``, as the remote server's commit does
(``CadRemoteServer.Commit.cs``): one undo step, or part of the step of the run that is going on, with the planned changes
asked of the owners of the entities first. Entities and styles are read as copies in a small drawing of their own.
"""

from __future__ import annotations

import importlib
import os
from typing import Any

from ._drawing import Backend, Documents, Drawing, Entity, Look, Op, dxf_type_of, look_color, point3

_net: Any = importlib.import_module("System")
_cad: Any = importlib.import_module("GeometryHelper.Cad")
_dxf: Any = importlib.import_module("GeometryHelper.Dxf")
_geo: Any = importlib.import_module("GeometryHelper.Geometry")

_cache: dict[Any, Drawing] = {}
_queued: list[Drawing] = []


def _host_module() -> Any:
    """The Python host assembly, which only the app has; None outside it."""
    try:
        return importlib.import_module("GeometryHelper.Cad.Python")
    except ImportError:
        return None


def _script() -> Any:
    """The run going on, or None when no script runs (outside the app, or a callback of the window)."""
    host = _host_module()
    return None if host is None else host.CadPythonScript.Current


def _run(name: str) -> Any:
    """The run going on; ``RuntimeError`` naming the function where there is none."""
    if _host_module() is None:
        raise RuntimeError(f"{name} works inside GeometryHelper CAD only")
    script = _script()
    if script is None:
        raise RuntimeError(f"{name} works only while a script runs in GeometryHelper CAD.")
    return script


def _app_host(name: str) -> Any:
    """The window's list of drawings (``ICadRemoteHost``), or None where the app has none."""
    return getattr(_run(name), "DocumentHost", None)


def _name_key(record: Any) -> tuple[str, str]:
    return str(record.Name).upper(), str(record.Name)


def _handle_key(handle: str) -> tuple[int, str]:
    try:
        return int(handle, 16), handle
    except ValueError:
        return 1 << 62, handle


class _LocalBackend:
    """A ``CadDocument`` as the backend of a ``Drawing``; ``in_app`` is true for the drawings of a run."""

    def __init__(self, document: Any, in_app: bool) -> None:
        self._document: Any = document
        self._in_app = in_app
        self._closed: str | None = None

    @property
    def document(self) -> Any:
        if self._closed is not None:
            raise RuntimeError(f"The drawing {self._closed} is closed.")
        return self._document

    def _host(self) -> Any:
        if not self._in_app:
            return None
        script = _script()
        return None if script is None else getattr(script, "DocumentHost", None)

    # ---- what the drawing is ----------------------------------------------------------------------------------------

    @property
    def name(self) -> str:
        return self._closed if self._closed is not None else str(self._document.Title)

    @property
    def path(self) -> str | None:
        file_name = self.document.FileName
        return str(file_name) if file_name is not None and os.path.isabs(str(file_name)) else None

    @property
    def is_active(self) -> bool:
        host = self._host()
        if host is None:
            return True
        active = host.ActiveDocument
        return active is not None and active == self.document

    @property
    def is_modified(self) -> bool:
        return bool(self.document.IsModified)

    @property
    def net(self) -> Any:
        return self.document

    # ---- reading ----------------------------------------------------------------------------------------------------

    def _spaces(self, layout: str | None) -> list[Any]:
        spaces = [space for space in self.document.Spaces if not space.IsBlockEditor]
        if layout is None:
            return spaces
        found = [space for space in spaces if str(space.Name).upper() == layout.upper()]
        if not found:
            raise ValueError(f"The drawing {self.name} has no layout named {layout!r}.")
        return found

    def _pack(self, entities: list[Any]) -> list[tuple[str, Any, Any]]:
        """Copies of the entities in a small drawing of their own, as the remote server packs what it sends."""
        if not entities:
            return []
        fragment = _dxf.DxfDatabase("AC1027")
        database = self.document.Database
        pairs: list[tuple[Any, Any]] = []
        try:
            copies = database.WblockCloneObjects(_net.Array[_dxf.DxfEntity](entities), fragment.ModelSpace)
            pairs = [(entity, copies[index]) for index, entity in enumerate(entities)]
        except _net.ArgumentException:
            for entity in entities:
                try:
                    copy = database.WblockCloneObjects(_net.Array[_dxf.DxfEntity]([entity]), fragment.ModelSpace)[0]
                    pairs.append((entity, copy))
                except _net.ArgumentException:
                    continue
        return [
            (str(original.Handle), copy, fragment)
            for original, copy in pairs
            if copy.Handle and not copy.IsErased and original.Handle
        ]

    def read(self, dxf_type: str | None, layer: str | None, layout: str | None) -> list[tuple[str, Any, Any]]:
        found: list[Any] = []
        for space in self._spaces(layout):
            for entity in space.Block:
                if entity.IsErased:
                    continue
                if layer is not None and str(entity.Layer).upper() != layer.upper():
                    continue
                if dxf_type is not None and dxf_type_of(entity).upper() != dxf_type:
                    continue
                found.append(entity)
        return self._pack(found)

    def read_one(self, handle: str) -> tuple[Any, Any] | None:
        entity = self.document.FindEntity(handle)
        if entity is None or entity.IsErased or self.document.FindSpace(handle) is None:
            return None
        packed = self._pack([entity])
        return (packed[0][1], packed[0][2]) if packed else None

    def selected(self) -> list[tuple[str, Any, Any]]:
        entities: list[Any] = []
        for handle in sorted((str(h) for h in self.document.Selection.Handles), key=_handle_key):
            entity = self.document.FindEntity(handle)
            if entity is not None and not entity.IsErased:
                entities.append(entity)
        return self._pack(entities)

    def select(self, handles: list[str]) -> None:
        for handle in handles:
            entity = self.document.FindEntity(handle)
            if entity is None or entity.IsErased:
                raise ValueError(f"The drawing {self.name} has no object {handle!r}.")
        self.document.Selection.Set(_net.Array[_net.String](handles))

    # ---- the commit -------------------------------------------------------------------------------------------------

    def _space_named(self, layout: str | None) -> Any:
        if not layout:
            return self.document.ModelSpace
        for space in self._spaces(None):
            if str(space.Name).upper() == layout.upper():
                return space
        raise ValueError(f"The drawing {self.name} has no layout named {layout!r}.")

    def _existing(self, handle: str) -> Any:
        entity = self.document.FindEntity(handle)
        if entity is None or entity.IsErased:
            raise RuntimeError(f"The drawing {self.name} has no object {handle}.")
        if self.document.FindSpace(handle) is None:
            raise RuntimeError(
                f"The object {handle} is inside a block of {self.name}, not in a space, and is not changed from outside."
            )
        return entity

    def commit(self, ops: list[Op]) -> list[str | None]:
        document = self.document
        layer_ops = [op for op in ops if op.kind in ("add_layer", "modify_layer")]
        steps = [op for op in ops if op.kind not in ("add_layer", "modify_layer")]
        planned: list[Any] = []
        batch: Any = None
        for op in steps:
            if op.kind == "insert":
                if batch is None:
                    batch = _dxf.DxfDatabase("AC1027")
                copy = op.copy
                # The app draws a dimension's picture; the copy only needs a block of its own to be added with.
                if isinstance(copy, _dxf.DxfDimension) and copy.DimBlock is None:
                    picture = _dxf.DxfBlockTableRecord()
                    picture.Name = "*D"
                    batch.BlockTable.Add(picture)
                    copy.DimBlock = picture
                batch.ModelSpace.AppendEntity(copy)
                self._space_named(op.layout)
            else:
                handle = op.handle if op.kind == "erase" else op.entity.handle  # pyright: ignore[reportOptionalMemberAccess]
                assert handle is not None
                self._existing(handle)
                kind = _cad.CadEditRecordKind.Erased if op.kind == "erase" else _cad.CadEditRecordKind.Reshaped
                planned.append(_cad.CadEditRecord(kind, handle, "Python"))
        inserted: list[str | None] = []

        def apply(scope: Any) -> None:
            inserted.clear()
            for op in layer_ops:
                assert op.layer is not None
                self._write_layer(scope, op.layer._look, op.kind == "add_layer")  # pyright: ignore[reportPrivateUsage]
            i = 0
            while i < len(steps):
                op = steps[i]
                if op.kind == "insert":
                    space = self._space_named(op.layout)
                    group = [op.copy]
                    i += 1
                    while (
                        i < len(steps)
                        and steps[i].kind == "insert"
                        and str(self._space_named(steps[i].layout).Name) == str(space.Name)
                    ):
                        group.append(steps[i].copy)
                        i += 1
                    self._insert_group(scope, batch, space, group, inserted)
                    continue
                handle = op.handle if op.kind == "erase" else op.entity.handle  # pyright: ignore[reportOptionalMemberAccess]
                assert handle is not None
                if not scope.IsHandled(handle):
                    old = document.FindEntity(handle)
                    if op.kind == "erase":
                        old.Erase()
                    else:
                        self._replace(old, handle, op.entity)  # pyright: ignore[reportArgumentType]
                i += 1

        script = _script() if self._in_app else None
        name = str(getattr(script, "StepName", "PYRUN")) if script is not None else "PYTHON"
        result = document.ApplyEdit(
            name, _net.Array[_cad.CadEditRecord](planned), _net.Action[_cad.CadEditScope](apply)
        )
        if not result.Succeeded:
            raise RuntimeError(str(result.Message))
        return inserted

    def _insert_group(self, scope: Any, batch: Any, space: Any, group: list[Any], inserted: list[str | None]) -> None:
        # A layer the drawing lacks is made by the copy; the layer box and the Layer manager are told.
        layers = self.document.Database.LayerTable
        missing = sorted(
            {str(e.Layer) for e in group if str(e.Layer) and not layers.Has(str(e.Layer))},
            key=lambda n: (n.upper(), n),
        )
        copies = batch.WblockCloneObjects(_net.Array[_dxf.DxfEntity](group), space.Block)
        for copy in copies:
            # An MTEXT copied into a drawing of R12 is written as TEXTs and is in no space itself.
            if not copy.Handle or copy.IsErased:
                inserted.append(None)
                continue
            scope.Added(space, copy)
            inserted.append(str(copy.Handle))
        for layer in missing:
            scope.Record(_cad.CadEditRecord(_cad.CadEditRecordKind.TableChanged, None, "Layer", None, layer))

    def _replace(self, old: Any, handle: str, entity: Entity) -> None:
        """Writes a changed copy over the object of the drawing, which keeps its handle (AutoCAD's ``HandOverTo``). A
        dimension keeps the picture it had: the app's own redrawing of it is not a public member."""
        space = self.document.FindSpace(handle)
        made = entity._source.WblockCloneObjects(  # pyright: ignore[reportPrivateUsage]
            _net.Array[_dxf.DxfEntity]([entity.net]), space.Block
        )[0]
        fresh = made.Clone()
        made.Erase()
        spare: Any = None
        if isinstance(fresh, _dxf.DxfDimension) and isinstance(old, _dxf.DxfDimension) and old.DimBlock is not None:
            spare = fresh.DimBlock
            fresh.DimBlock = old.DimBlock
        old.HandOverTo(fresh, True, True)
        if spare is not None and spare.IsAnonymous:
            spare.Erase()

    # ---- layers -----------------------------------------------------------------------------------------------------

    def layers(self) -> list[Look]:
        looks: list[Look] = []
        for layer in self.document.Database.LayerTable:
            true_color = layer.TrueColor
            looks.append(
                Look(
                    name=str(layer.Name),
                    color=int(layer.ColorIndex),
                    true_color=None if true_color is None else int(true_color),
                    linetype=str(layer.LinetypeName),
                    line_weight=int(layer.LineWeight),
                    description=str(layer.Description),
                    is_on=not layer.IsOff,
                    is_frozen=bool(layer.IsFrozen),
                    is_locked=bool(layer.IsLocked),
                    is_plottable=bool(layer.IsPlottable),
                )
            )
        return looks

    def _write_layer(self, scope: Any, look: Look, add: bool) -> None:
        layers = self.document.Database.LayerTable
        if not self.document.Database.LinetypeTable.Has(look.linetype):
            raise RuntimeError(f"The linetype {look.linetype} is not in the drawing {self.name}.")
        if add:
            if layers.Has(look.name):
                raise RuntimeError(f'Layer "{look.name}" already exists.')
            layer = _dxf.DxfLayerTableRecord()
            layer.Name = look.name
        else:
            if not layers.Has(look.name):
                raise RuntimeError(f'The drawing {self.name} has no layer "{look.name}".')
            layer = layers[look.name]
        layer.Color = look_color(look)
        layer.LinetypeName = look.linetype
        layer.LineWeight = _dxf.DxfLineWeight(look.line_weight)
        layer.IsOff = not look.is_on
        layer.IsFrozen = look.is_frozen
        layer.IsLocked = look.is_locked
        layer.IsPlottable = look.is_plottable
        if add:
            layers.Add(layer)
            scope.Record(_cad.CadEditRecord(_cad.CadEditRecordKind.TableChanged, None, "New layer", None, look.name))
        layer.Description = look.description

    @property
    def current_layer(self) -> str:
        return str(self.document.Database.Clayer)

    @current_layer.setter
    def current_layer(self, value: str) -> None:
        result = _cad.CadLayerEditor.SetCurrent(self.document, value)
        if not result.Succeeded:
            raise RuntimeError(str(result.Message))

    # ---- layouts, styles, blocks --------------------------------------------------------------------------------------

    @property
    def layouts(self) -> list[str]:
        return [str(space.Name) for space in self._spaces(None)]

    @property
    def current_layout(self) -> str:
        space = self.document.CurrentSpace
        return str((space if space is not None else self.document.ModelSpace).Name)

    @current_layout.setter
    def current_layout(self, value: str) -> None:
        space = self._space_named(value)
        host = self._host()
        if host is not None and not self.is_active:
            host.Activate(self.document)
        result = self.document.ShowSpace(space)
        if not result.Succeeded:
            raise RuntimeError(str(result.Message))

    def _scratch(self) -> Any:
        """A copy of the whole drawing, from which the records of a table are taken as copies of their own."""
        return _dxf.DxfDatabase.Parse(self.document.Database.ToText())

    @property
    def text_styles(self) -> list[Any]:
        return sorted(list(self._scratch().TextStyleTable), key=_name_key)

    @property
    def dimension_styles(self) -> list[Any]:
        return sorted(list(self._scratch().DimStyleTable), key=_name_key)

    @property
    def blocks(self) -> list[Any]:
        return sorted(
            (
                block
                for block in self._scratch().BlockTable
                if not block.IsLayout
                and not block.IsAnonymous
                and not block.IsDependent
                and not str(block.Name).startswith("*")
            ),
            key=_name_key,
        )

    # ---- the user ---------------------------------------------------------------------------------------------------

    def _asks(self, name: str) -> None:
        script = _run(name)
        if not self._in_app or script.Document != self.document:
            raise RuntimeError(f"{name} asks in the drawing of the run, not in {self.name}.")

    def pick_point(self, message: str, base: Any) -> Any:
        if not self._in_app:
            raise RuntimeError("pick_point works inside GeometryHelper CAD only")
        self._asks("pick_point")
        from . import get_point  # pyright: ignore[reportAttributeAccessIssue]

        point: Any = get_point(message, None if base is None else _geo.GeoPoint2(base.X, base.Y))
        return None if point is None or isinstance(point, str) else point3(point)

    def pick_object(self, message: str) -> tuple[str, Any, Any] | None:
        if not self._in_app:
            raise RuntimeError("pick_object works inside GeometryHelper CAD only")
        self._asks("pick_object")
        from . import get_entity  # pyright: ignore[reportAttributeAccessIssue]

        found: Any = get_entity(message)
        if isinstance(found, Entity):
            return found.handle, found.net, found._source  # pyright: ignore[reportPrivateUsage]
        return None

    def pick_objects(self, message: str) -> list[tuple[str, Any, Any]]:
        if not self._in_app:
            raise RuntimeError("pick_objects works inside GeometryHelper CAD only")
        self._asks("pick_objects")
        from . import get_selection  # pyright: ignore[reportAttributeAccessIssue]

        chosen: list[Any] = get_selection()
        return [(e.handle, e.net, e._source) for e in chosen]  # pyright: ignore[reportPrivateUsage]

    # ---- the file ---------------------------------------------------------------------------------------------------

    def save(self) -> None:
        host = self._host()
        if host is not None:
            host.Save(self.document)
        else:
            self.document.Save()

    def save_as(self, path: str, version: str | None) -> None:
        host = self._host()
        if host is not None:
            host.SaveAs(self.document, path, version)
        elif version is None:
            self.document.SaveAs(path)
        else:
            self.document.SaveAs(path, version, None)

    def close(self, discard: bool) -> None:
        document = self.document
        host = self._host()
        if host is not None:
            if document.IsModified and not discard:
                raise RuntimeError(f"{self.name} has changes not saved: save it, or close it with discard.")
            host.Close(document)
        self._closed = self.name
        _cache.pop(document, None)


def _remember(drawing: Drawing) -> None:
    if not any(drawing is queued for queued in _queued):
        _queued.append(drawing)


def drawing_of(document: Any) -> Drawing:
    """The ``Drawing`` of a ``CadDocument`` of the app, the one every call in a run gets; the app's prompts use it."""
    drawing = _cache.get(document)
    if drawing is None:
        backend: Backend = _LocalBackend(document, True)
        drawing = Drawing(backend, _remember)
        _cache[document] = drawing
    return drawing


def entity_of(document: Any, handle: str) -> Entity:
    """The ``Entity`` of a ``CadDocument`` of the app with the handle; ``KeyError`` when it has none."""
    return drawing_of(document).entity(handle)


def commit_pending() -> None:
    """Commit what the drawings of the app queued, each as one undo step (inside the run's, for the run's own drawing)."""
    drawings = list(_queued)
    _queued.clear()
    _cache.clear()
    for index, drawing in enumerate(drawings):
        try:
            drawing.commit()
        except BaseException:
            for rest in drawings[index + 1 :]:
                rest._discard()  # pyright: ignore[reportPrivateUsage]
            raise


def discard_pending() -> None:
    """Drop what the drawings of the app queued."""
    drawings = list(_queued)
    _queued.clear()
    _cache.clear()
    for drawing in drawings:
        drawing._discard()  # pyright: ignore[reportPrivateUsage]


def active_drawing() -> Drawing:
    """The drawing the run started in (not the one shown later): ``active_drawing().add_line((0, 0), (10, 0))``."""
    return drawing_of(_run("active_drawing").Document)


def documents() -> Documents:
    """The drawings of the app, in the order it lists them; only the drawing of the run where the app has no list."""
    script = _run("documents")

    def listed() -> list[Drawing]:
        host = getattr(script, "DocumentHost", None)
        if host is None:
            return [drawing_of(script.Document)]
        return [drawing_of(document) for document in host.Documents]

    return Documents(listed)


def open_drawing(path: str) -> Drawing:
    """Open a drawing file in the app, as OPEN does, and return it (the open one where it already is)."""
    host = _app_host("open_drawing")
    if host is None:
        raise RuntimeError("open_drawing needs the list of drawings of the app's window, and there is none.")
    return drawing_of(host.Open(os.path.abspath(path)))


def new_drawing(template: str | None = None) -> Drawing:
    """Make a new drawing in the app, from a template file where one is given, and return it."""
    host = _app_host("new_drawing")
    if host is None:
        raise RuntimeError("new_drawing needs the list of drawings of the app's window, and there is none.")
    return drawing_of(host.New(None if template is None else os.path.abspath(template)))


def open_file(path: str) -> Drawing:
    """Open a DXF or DWG file with no app: ``open_file("plan.dxf")``. Changes reach the file after ``save()``."""
    return Drawing(_LocalBackend(_cad.CadDocument.Open(os.path.abspath(path)), False))


def new_file() -> Drawing:
    """Make a blank drawing (AutoCAD 2013, layer 0 only) with no app and no file: ``new_file().save_as("new.dxf")``."""
    return Drawing(_LocalBackend(_cad.CadDocument.From(_dxf.DxfDatabase("AC1027")), False))
