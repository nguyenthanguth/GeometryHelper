"""The Drawing API, the same over a drawing of the app, a drawing of a running app and a drawing file (D1655 to D1659).

A ``Drawing`` queues what a script does to it (``add_line``, ``modify``, ``erase``, a layer made or changed, an entity's
layer or colour set) and ``commit()`` applies it all as one undo step. What a ``Drawing`` reads and writes it asks of a
backend: ``_local`` (a ``CadDocument``, in the app and headless) or ``_remote`` (a ``CadRemoteDocument``). Reads are
copies: ``Entity.net`` and the styles and blocks are never the live objects of the drawing, so nothing bypasses undo.

    drawing = open_file("plan.dxf")
    drawing.add_line((0, 0), (100, 0), layer="Steel")
    drawing.commit()
"""

from __future__ import annotations

import importlib
import os
from collections.abc import Callable, Iterator, Sequence
from dataclasses import dataclass
from typing import Any, Protocol

_dxf: Any = importlib.import_module("GeometryHelper.Dxf")
_geo: Any = importlib.import_module("GeometryHelper.Geometry")


@dataclass
class Look:
    """What a layer is, as plain values the backends turn into their own layer objects."""

    name: str
    color: int = 7
    true_color: int | None = None
    linetype: str = "Continuous"
    line_weight: int = -3
    description: str = ""
    is_on: bool = True
    is_frozen: bool = False
    is_locked: bool = False
    is_plottable: bool = True


@dataclass
class Op:
    """One queued change: ``insert`` (a copy to add), ``modify`` or ``erase`` of an entity, ``add_layer`` or ``modify_layer``."""

    kind: str
    entity: Entity | None = None
    copy: Any = None
    layout: str | None = None
    handle: str | None = None
    layer: Layer | None = None


class Backend(Protocol):
    """What a ``Drawing`` asks of the drawing underneath it; ``_local`` and ``_remote`` each give it."""

    @property
    def name(self) -> str: ...
    @property
    def path(self) -> str | None: ...
    @property
    def is_active(self) -> bool: ...
    @property
    def is_modified(self) -> bool: ...
    @property
    def net(self) -> Any: ...
    @property
    def current_layer(self) -> str: ...
    @current_layer.setter
    def current_layer(self, value: str) -> None: ...
    @property
    def layouts(self) -> list[str]: ...
    @property
    def current_layout(self) -> str: ...
    @current_layout.setter
    def current_layout(self, value: str) -> None: ...
    @property
    def text_styles(self) -> list[Any]: ...
    @property
    def dimension_styles(self) -> list[Any]: ...
    @property
    def blocks(self) -> list[Any]: ...

    def read(self, dxf_type: str | None, layer: str | None, layout: str | None) -> list[tuple[str, Any, Any]]: ...
    def read_one(self, handle: str) -> tuple[Any, Any] | None: ...
    def selected(self) -> list[tuple[str, Any, Any]]: ...
    def select(self, handles: list[str]) -> None: ...
    def commit(self, ops: list[Op]) -> list[str | None]: ...
    def layers(self) -> list[Look]: ...
    def pick_point(self, message: str, base: Any) -> Any: ...
    def pick_object(self, message: str) -> tuple[str, Any, Any] | None: ...
    def pick_objects(self, message: str) -> list[tuple[str, Any, Any]]: ...
    def save(self) -> None: ...
    def save_as(self, path: str, version: str | None) -> None: ...
    def close(self, discard: bool) -> None: ...


def dxf_type_of(net: Any) -> str:
    """The DXF name of an entity (``LINE``, ``LWPOLYLINE``): the first word of its ``ToString``, which the library writes as
    ``LINE 2F on steel``, since no public member gives the name."""
    return str(net).split(None, 1)[0]


def look_color(look: Look) -> Any:
    """The ``DxfColor`` of a layer look: its true colour where it has one, else its colour of the index."""
    if look.true_color is not None:
        rgb = look.true_color
        return _dxf.DxfColor.FromRgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255)
    return _dxf.DxfColor.FromColorIndex(_dxf.DxfColorMethod.ByAci, look.color)


def point3(value: Any) -> Any:
    """A ``GeoPoint3`` from a ``GeoPoint2``, a ``GeoPoint3`` or a tuple or list of 2 or 3 numbers; z is 0 for a 2D point."""
    if isinstance(value, _geo.GeoPoint3):
        return value
    if isinstance(value, _geo.GeoPoint2):
        return _geo.GeoPoint3(value.X, value.Y, 0.0)
    if isinstance(value, (tuple, list)) and len(value) in (2, 3):  # pyright: ignore[reportUnknownArgumentType]
        numbers: list[Any] = list(value)  # pyright: ignore[reportUnknownArgumentType]
        return _geo.GeoPoint3(float(numbers[0]), float(numbers[1]), float(numbers[2]) if len(numbers) == 3 else 0.0)
    raise ValueError(f"A point is a GeoPoint2, a GeoPoint3 or a tuple of 2 or 3 numbers, not {value!r}.")


class Entity:
    """One object of a drawing, as a copy: ``handle``, ``dxf_type`` and ``net``, the ``DxfEntity`` copy.

    ``layer``, ``color`` (the colour index: 256 ByLayer, 0 ByBlock) and ``linetype`` are set here and committed by
    ``Drawing.commit()`` without ``modify``. ``net`` is one copy for the life of the ``Entity``: a change made to it reaches
    the drawing only through ``Drawing.modify(entity)``. An entity just added has the handle ``~1``, ``~2``... until
    ``commit()`` gives it the real one, and its layer, colour and linetype are given to ``add`` until then::

        line = drawing.entities(type="LINE")[0]
        line.layer = "Steel"      # a line from (0, 0) to (100, 0) now on Steel
        drawing.commit()
    """

    def __init__(self, drawing: Drawing, handle: str, net: Any, source: Any = None) -> None:
        self._drawing = drawing
        self._handle = handle
        self._net = net
        self._source = source  # the small drawing the copy lives in: the local backend writes a change from it
        self._type = dxf_type_of(net)

    @property
    def handle(self) -> str:
        """The handle of the object in the drawing (``2F``); ``~n`` for one added and not yet committed."""
        return self._handle

    @property
    def dxf_type(self) -> str:
        """The DXF name of the object: ``"LINE"``, ``"CIRCLE"``, ``"LWPOLYLINE"``, ``"TEXT"``."""
        return self._type

    @property
    def net(self) -> Any:
        """The ``DxfEntity`` copy; after changing it call ``drawing.modify(entity)``."""
        return self._net

    def _set(self, member: str, value: Any) -> None:
        if self._handle.startswith("~"):
            raise ValueError(
                f"The {self._type} {self._handle} is not in the drawing yet: call commit() first, or give {member} to add."
            )
        setattr(self._net, member, value)
        self._drawing._changed(self)  # pyright: ignore[reportPrivateUsage]

    @property
    def layer(self) -> str:
        """The name of the layer the object is on."""
        return str(self._net.Layer)

    @layer.setter
    def layer(self, value: str) -> None:
        self._set("Layer", value)

    @property
    def color(self) -> int:
        """The colour index: 1 to 255, 256 for ByLayer, 0 for ByBlock."""
        return int(self._net.ColorIndex)

    @color.setter
    def color(self, value: int) -> None:
        self._set("ColorIndex", int(value))

    @property
    def linetype(self) -> str:
        """The name of the linetype: ``"ByLayer"``, ``"ByBlock"`` or one of the drawing's."""
        return str(self._net.Linetype)

    @linetype.setter
    def linetype(self, value: str) -> None:
        self._set("Linetype", value)

    def __repr__(self) -> str:
        return f"<Entity {self._type} {self._handle} on {self.layer}>"


class Layer:
    """One layer, as a copy: ``name`` (read only) and the settable ``color`` (colour index 1 to 255), ``linetype``,
    ``description``, ``is_on``, ``is_frozen``, ``is_locked`` and ``is_plottable``, committed with the drawing::

        steel = drawing.layers["Steel"]
        steel.is_locked = True      # Steel is locked once commit() runs
        drawing.commit()
    """

    def __init__(self, drawing: Drawing, look: Look, pending: bool = False) -> None:
        self._drawing = drawing
        self._look = look
        self._pending = pending
        self._dirty = False

    def _touch(self) -> None:
        if not self._pending and not self._dirty:
            self._dirty = True
            self._drawing._layer_changed(self)  # pyright: ignore[reportPrivateUsage]

    @property
    def name(self) -> str:
        """The name of the layer."""
        return self._look.name

    @property
    def color(self) -> int:
        """The colour index of the layer, 1 to 255 (a layer of a true colour reads the index nearest it)."""
        return self._look.color

    @color.setter
    def color(self, value: int) -> None:
        if isinstance(value, bool) or not isinstance(value, int) or not 1 <= value <= 255:  # pyright: ignore[reportUnnecessaryIsInstance]
            raise ValueError(f"The colour of layer {self.name} is an index from 1 to 255, not {value!r}.")
        self._look.color = value
        self._look.true_color = None
        self._touch()

    @property
    def linetype(self) -> str:
        """The name of the layer's linetype."""
        return self._look.linetype

    @linetype.setter
    def linetype(self, value: str) -> None:
        self._look.linetype = value
        self._touch()

    @property
    def description(self) -> str:
        """The description of the layer."""
        return self._look.description

    @description.setter
    def description(self, value: str) -> None:
        self._look.description = value
        self._touch()

    @property
    def is_on(self) -> bool:
        """Whether the layer is on."""
        return self._look.is_on

    @is_on.setter
    def is_on(self, value: bool) -> None:
        self._look.is_on = bool(value)
        self._touch()

    @property
    def is_frozen(self) -> bool:
        """Whether the layer is frozen."""
        return self._look.is_frozen

    @is_frozen.setter
    def is_frozen(self, value: bool) -> None:
        self._look.is_frozen = bool(value)
        self._touch()

    @property
    def is_locked(self) -> bool:
        """Whether the layer is locked."""
        return self._look.is_locked

    @is_locked.setter
    def is_locked(self, value: bool) -> None:
        self._look.is_locked = bool(value)
        self._touch()

    @property
    def is_plottable(self) -> bool:
        """Whether the layer is plotted."""
        return self._look.is_plottable

    @is_plottable.setter
    def is_plottable(self, value: bool) -> None:
        self._look.is_plottable = bool(value)
        self._touch()

    def __repr__(self) -> str:
        return f"<Layer {self.name}>"


class Layers:
    """The layers of a drawing: iterable, ``len``, ``[name]`` (ignoring case, ``KeyError`` when none), ``in``, and ``add``.

    Each reading asks the drawing, so a layer added shows after ``commit()``; a ``Layer`` changed and not yet committed is
    kept by the drawing and returned as it is::

        "Steel" in drawing.layers    # False
        drawing.layers.add("Steel", color=1)
        drawing.commit()
        "steel" in drawing.layers    # True
    """

    def __init__(self, drawing: Drawing) -> None:
        self._drawing = drawing
        self._known: dict[str, Layer] = {}

    def _read(self) -> list[Layer]:
        found: list[Layer] = []
        for look in self._drawing._backend.layers():  # pyright: ignore[reportPrivateUsage]
            key = look.name.upper()
            known = self._known.get(key)
            if known is None or not known._dirty:  # pyright: ignore[reportPrivateUsage]
                known = Layer(self._drawing, look)
                self._known[key] = known
            found.append(known)
        return found

    def __iter__(self) -> Iterator[Layer]:
        return iter(self._read())

    def __len__(self) -> int:
        return len(self._read())

    def __contains__(self, name: object) -> bool:
        return isinstance(name, str) and any(layer.name.upper() == name.upper() for layer in self._read())

    def __getitem__(self, name: str) -> Layer:
        for layer in self._read():
            if layer.name.upper() == name.upper():
                return layer
        raise KeyError(f"The drawing {self._drawing.name} has no layer {name!r}.")

    def add(self, name: str, color: int = 7, linetype: str = "Continuous", description: str = "") -> Layer:
        """Queue a new layer; it exists after ``commit()``, and a layer of that name already there refuses the commit."""
        if not isinstance(name, str) or not name.strip():  # pyright: ignore[reportUnnecessaryIsInstance]
            raise ValueError(f"A layer needs a name, not {name!r}.")
        layer = Layer(self._drawing, Look(name.strip(), linetype=linetype, description=description), pending=True)
        layer.color = color
        self._drawing._queue(Op("add_layer", layer=layer))  # pyright: ignore[reportPrivateUsage]
        return layer


class Drawing:
    """One drawing: read it, queue changes, and ``commit()`` them as one undo step.

    The same members work on a drawing of the app (``active_drawing()``), of a running app (``connect()``) and of a file
    (``open_file``, ``new_file``). ``net`` is the ``CadDocument``, or the ``CadRemoteDocument`` outside the app. A point is a
    ``GeoPoint2``, a ``GeoPoint3`` or a tuple of 2 or 3 numbers; angles are in radians; units are the drawing's::

        drawing.add_line((0, 0), (100, 0))
        drawing.add_circle((50, 50), 25)
        handles = drawing.commit()       # two handles, one undo step
    """

    def __init__(self, backend: Backend, on_queue: Callable[[Drawing], None] | None = None) -> None:
        self._backend = backend
        self._on_queue = on_queue
        self._ops: list[Op] = []
        self._changed_entities: list[Entity] = []
        self._changed_layers: list[Layer] = []
        self._tickets = 0
        self._noted = False
        self._layers = Layers(self)

    # ---- the queue ------------------------------------------------------------------------------------------------

    def _note(self) -> None:
        if self._on_queue is not None and not self._noted:
            self._noted = True
            self._on_queue(self)

    def _queue(self, op: Op) -> None:
        self._ops.append(op)
        self._note()

    def _changed(self, entity: Entity) -> None:
        if not any(entity is known for known in self._changed_entities):
            self._changed_entities.append(entity)
        self._note()

    def _layer_changed(self, layer: Layer) -> None:
        self._changed_layers.append(layer)
        self._note()

    def _discard(self) -> None:
        """Drops what is queued; a changed layer or entity keeps the value it was given."""
        for layer in self._changed_layers:
            layer._dirty = False  # pyright: ignore[reportPrivateUsage]
        self._ops.clear()
        self._changed_entities.clear()
        self._changed_layers.clear()
        self._layers._known.clear()  # pyright: ignore[reportPrivateUsage]
        self._noted = False

    # ---- what the drawing is --------------------------------------------------------------------------------------

    @property
    def name(self) -> str:
        """The name of the drawing, as its tab shows it: ``plan.dxf``."""
        return self._backend.name

    @property
    def path(self) -> str | None:
        """The full path of the file, or None for a drawing never saved."""
        return self._backend.path

    @property
    def is_active(self) -> bool:
        """Whether this is the drawing shown (always true for a drawing of a file)."""
        return self._backend.is_active

    @property
    def is_modified(self) -> bool:
        """Whether the drawing differs from the file it was opened from or last saved as."""
        return self._backend.is_modified

    @property
    def net(self) -> Any:
        """The ``CadDocument`` of the drawing, or the ``CadRemoteDocument`` outside the app."""
        return self._backend.net

    # ---- reading --------------------------------------------------------------------------------------------------

    def entities(self, type: str | None = None, layer: str | None = None, layout: str | None = None) -> list[Entity]:
        """The objects of the drawing, as copies in the order the drawing lists them, of one DXF ``type`` (``"line"``, ignoring
        case), one ``layer`` and one ``layout`` where given: ``drawing.entities(type="CIRCLE", layer="Steel")``."""
        found = self._backend.read(None if type is None else type.upper(), layer, layout)
        entities = [Entity(self, handle, net, source) for handle, net, source in found]
        if type is None:
            return entities
        return [entity for entity in entities if entity.dxf_type.upper() == type.upper()]

    def entity(self, handle: str) -> Entity:
        """The object of a handle; ``KeyError`` when the drawing has none: ``drawing.entity("2F")``."""
        found = None if handle.startswith("~") else self._backend.read_one(handle)
        if found is None:
            raise KeyError(f"The drawing {self.name} has no object {handle!r}.")
        return Entity(self, handle, found[0], found[1])

    def selection(self) -> list[Entity]:
        """The objects selected now, in the order they were made."""
        return [Entity(self, handle, net, source) for handle, net, source in self._backend.selected()]

    def select(self, items: Sequence[Entity | str]) -> None:
        """Select these objects (entities or handles) and nothing else: ``drawing.select([line, "2F"])``."""
        handles: list[str] = []
        for item in items:
            handle = item.handle if isinstance(item, Entity) else str(item)
            if handle.startswith("~"):
                raise ValueError(f"The object {handle} is not in the drawing yet: call commit() first.")
            handles.append(handle)
        self._backend.select(handles)

    # ---- adding ---------------------------------------------------------------------------------------------------

    def _made(self, entity: Any, props: dict[str, Any]) -> Any:
        for key, value in props.items():
            if key == "layer":
                entity.Layer = str(value)
            elif key == "color":
                entity.ColorIndex = int(value)
            elif key == "linetype":
                entity.Linetype = str(value)
            else:
                raise TypeError(f"{key!r} is not a property an object is made with: give layer, color or linetype.")
        return entity

    def add(self, dxf_entity: Any, layout: str | None = None) -> Entity:
        """Queue a copy of any ``GeometryHelper.Dxf`` entity, in model space or in ``layout``; the ``Entity`` returned has
        the handle ``~1``, ``~2``... until ``commit()``. The entity given is not kept: a later change to it adds nothing."""
        self._tickets += 1
        entity = Entity(self, f"~{self._tickets}", dxf_entity.Clone())
        self._queue(Op("insert", entity=entity, copy=dxf_entity.Clone(), layout=layout))
        return entity

    def add_line(self, start: Any, end: Any, **props: Any) -> Entity:
        """Queue a line: ``drawing.add_line((0, 0), (100.5, 0), layer="Steel")``. ``props``: ``layer``, ``color``, ``linetype``."""
        return self.add(self._made(_dxf.DxfLine(point3(start), point3(end)), props))

    def add_circle(self, center: Any, radius: float, **props: Any) -> Entity:
        """Queue a circle: ``drawing.add_circle((50, 50), 25)`` is a circle of radius 25 about (50, 50)."""
        circle = _dxf.DxfCircle()
        circle.Center = point3(center)
        circle.Radius = float(radius)
        return self.add(self._made(circle, props))

    def add_arc(self, center: Any, radius: float, start_angle: float, end_angle: float, **props: Any) -> Entity:
        """Queue an arc, counter-clockwise from ``start_angle`` to ``end_angle`` in **radians**:
        ``drawing.add_arc((0, 0), 10, 0.0, math.pi / 2)`` is the quarter of a circle of radius 10 in the first quadrant."""
        arc = _dxf.DxfArc(point3(center), float(radius), float(start_angle), float(end_angle))
        return self.add(self._made(arc, props))

    def add_polyline(self, points: Sequence[Any], closed: bool = False, **props: Any) -> Entity:
        """Queue a polyline through the points (x and y; the z of the first is its elevation), closed where asked:
        ``drawing.add_polyline([(0, 0), (100, 0), (100, 50)], closed=True)`` is a triangle of 2500 square units."""
        corners = [point3(point) for point in points]
        if not corners:
            raise ValueError("A polyline needs at least one point.")
        polyline = _dxf.DxfPolyline()
        for index, corner in enumerate(corners):
            polyline.AddVertexAt(index, _geo.GeoPoint2(corner.X, corner.Y), 0.0, 0.0, 0.0)
        polyline.Elevation = float(corners[0].Z)
        polyline.Closed = bool(closed)
        return self.add(self._made(polyline, props))

    def add_text(self, text: str, insert: Any, height: float, rotation: float = 0.0, **props: Any) -> Entity:
        """Queue a single-line text at ``insert``, ``height`` high, turned ``rotation`` radians:
        ``drawing.add_text("Plate", (0, 100), 5.0)``."""
        made = _dxf.DxfDBText()
        made.TextString = str(text)
        made.Position = point3(insert)
        made.Height = float(height)
        made.Rotation = float(rotation)
        return self.add(self._made(made, props))

    def add_mtext(self, text: str, insert: Any, height: float, width: float = 0.0, **props: Any) -> Entity:
        """Queue a multi-line text at ``insert``, ``height`` high, wrapped at ``width`` (0 for no wrapping):
        ``drawing.add_mtext("Plate PL10", (10, 20), 5.0)``."""
        made = _dxf.DxfMText()
        made.Contents = str(text)
        made.Location = point3(insert)
        made.TextHeight = float(height)
        if width:
            made.Width = float(width)
        return self.add(self._made(made, props))

    # ---- changing -------------------------------------------------------------------------------------------------

    def _own(self, entity: Entity, verb: str) -> None:
        if not isinstance(entity, Entity) or entity._drawing is not self:  # pyright: ignore[reportUnnecessaryIsInstance, reportPrivateUsage]
            raise ValueError(f"{verb} takes an Entity read from the drawing {self.name}, not {entity!r}.")
        if entity.handle.startswith("~"):
            raise ValueError(f"The {entity.dxf_type} {entity.handle} is not in the drawing yet: call commit() first.")

    def modify(self, entity: Entity) -> None:
        """Queue the changes made to ``entity.net``; not needed for ``layer``, ``color`` and ``linetype``, which commit alone."""
        self._own(entity, "modify")
        self._queue(Op("modify", entity=entity))

    def erase(self, entity_or_handle: Entity | str) -> None:
        """Queue the erasing of an object, given as an ``Entity`` or its handle: ``drawing.erase("2F")``."""
        if isinstance(entity_or_handle, Entity):
            self._own(entity_or_handle, "erase")
            handle = entity_or_handle.handle
        else:
            handle = str(entity_or_handle)
            if handle.startswith("~"):
                raise ValueError(f"The object {handle} is not in the drawing yet: call commit() first.")
        self._queue(Op("erase", handle=handle))

    def commit(self) -> list[str]:
        """Apply everything queued as one undo step, and return the handles of the objects added, in the order they were
        added; ``[]`` when nothing was queued. The layers are made first, so an object can be put on a layer made in the same
        commit. A commit the drawing refuses changes nothing and raises."""
        ops = list(self._ops)
        for entity in self._changed_entities:
            ops.append(Op("modify", entity=entity))
        for layer in self._changed_layers:
            ops.append(Op("modify_layer", layer=layer))
        added = [op for op in ops if op.kind == "insert"]
        pending = [op.layer for op in ops if op.kind == "add_layer" and op.layer is not None]
        changed_layers = self._changed_layers[:]
        self._ops.clear()
        self._changed_entities.clear()
        self._changed_layers.clear()
        self._noted = False
        if not ops:
            return []
        try:
            handles = self._backend.commit(ops)
        finally:
            for layer in changed_layers:
                layer._dirty = False  # pyright: ignore[reportPrivateUsage]
            self._layers._known.clear()  # pyright: ignore[reportPrivateUsage]
        for layer in pending:
            layer._pending = False  # pyright: ignore[reportPrivateUsage]
        made: list[str] = []
        for op, handle in zip(added, handles + [None] * (len(added) - len(handles))):
            if handle is not None and op.entity is not None:
                op.entity._handle = handle  # pyright: ignore[reportPrivateUsage]
                made.append(handle)
        return made

    # ---- layers, styles, blocks, layouts --------------------------------------------------------------------------

    @property
    def layers(self) -> Layers:
        """The layers: ``drawing.layers["Steel"]``, ``drawing.layers.add("Steel", color=1)``."""
        return self._layers

    @property
    def current_layer(self) -> str:
        """The name of the layer new objects go on; setting it takes effect at once, not at ``commit()``."""
        return self._backend.current_layer

    @current_layer.setter
    def current_layer(self, name: str) -> None:
        self._backend.current_layer = name

    @property
    def text_styles(self) -> list[Any]:
        """Copies of the drawing's ``DxfTextStyleTableRecord``, sorted by name ignoring case, then ordinal; changing one
        changes nothing in the drawing."""
        return self._backend.text_styles

    @property
    def dimension_styles(self) -> list[Any]:
        """Copies of the drawing's ``DxfDimStyleTableRecord``, sorted as ``text_styles`` are."""
        return self._backend.dimension_styles

    @property
    def blocks(self) -> list[Any]:
        """Copies of the drawing's ``DxfBlockTableRecord`` with their objects, sorted as ``text_styles`` are; the blocks of
        the layouts (``*Model_Space``) and the anonymous ones (``*D1``) are left out."""
        return self._backend.blocks

    @property
    def layouts(self) -> list[str]:
        """The names of the layouts, ``["Model", "Layout1"]``."""
        return self._backend.layouts

    @property
    def current_layout(self) -> str:
        """The name of the layout shown; setting it shows that layout at once."""
        return self._backend.current_layout

    @current_layout.setter
    def current_layout(self, name: str) -> None:
        self._backend.current_layout = name

    # ---- the user -------------------------------------------------------------------------------------------------

    def pick_point(self, message: str, base: Any = None) -> Any:
        """Ask the user for a point, a ``GeoPoint3``: the prompt of the command line in the app, a wait on the app from
        outside; ``RuntimeError`` on a drawing of a file. ``base`` draws a rubber band from that point."""
        return self._backend.pick_point(message, None if base is None else point3(base))

    def pick_object(self, message: str) -> Entity | None:
        """Ask the user for one object and return it, or None where the user gave none; ``RuntimeError`` on a file."""
        found = self._backend.pick_object(message)
        return None if found is None else Entity(self, found[0], found[1], found[2])

    def pick_objects(self, message: str) -> list[Entity]:
        """Ask the user for a selection and return its objects; ``RuntimeError`` on a drawing of a file."""
        return [Entity(self, handle, net, source) for handle, net, source in self._backend.pick_objects(message)]

    # ---- the file -------------------------------------------------------------------------------------------------

    def save(self) -> None:
        """Commit what is queued, then save to the file the drawing came from or was last saved as."""
        self.commit()
        self._backend.save()

    def save_as(self, path: str, version: str | None = None) -> None:
        """Commit what is queued, then save as ``path``: a DWG where it ends in .dwg, else a DXF, of ``version`` (``"AC1027"``)
        where given, else the drawing's own."""
        self.commit()
        self._backend.save_as(os.path.abspath(path), version)

    def close(self, discard: bool = False) -> None:
        """Close the drawing; one with changes not saved is refused unless ``discard`` is true. What is queued is dropped.
        A drawing of a file is only let go."""
        self._discard()
        self._backend.close(discard)

    def __repr__(self) -> str:
        return f"<Drawing {self.name}>"


class Documents:
    """The drawings of an app, in the order it lists them: iterable, ``len`` and ``[name_or_path]`` (``KeyError`` naming the
    key where none is open)::

        for drawing in documents():
            print(drawing.name, drawing.is_active)
        plan = documents()["plan.dxf"]
    """

    def __init__(self, lister: Callable[[], list[Drawing]]) -> None:
        self._lister = lister

    def __iter__(self) -> Iterator[Drawing]:
        return iter(self._lister())

    def __len__(self) -> int:
        return len(self._lister())

    def __getitem__(self, key: str) -> Drawing:
        drawings = self._lister()
        if os.path.isabs(key):
            full = os.path.normcase(os.path.normpath(key))
            for drawing in drawings:
                if drawing.path is not None and os.path.normcase(os.path.normpath(drawing.path)) == full:
                    return drawing
        for drawing in drawings:
            if drawing.name.upper() == key.upper():
                return drawing
        raise KeyError(f"No drawing named {key!r} is open.")
