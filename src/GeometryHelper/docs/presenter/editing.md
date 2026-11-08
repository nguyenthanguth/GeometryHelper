# Editing as AutoCAD does

How a drawing is changed in the presenter: every change is one edit and one undo step, a command asks its prompts on a
command line with AutoCAD's names and aliases, a click can snap to a point of the drawing, a grip drags a point, Properties
sets a value in place, and a layer is switched, frozen or locked. All of it works on a
[`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument) with no window, and every example below was run on the plate of
[Opening, drawing and picking](display.md): four lines, a circle and a TEXT.

## One edit, one undo step

Nothing changes the drawing but [`CadDocument.ApplyEdit`](xref:GeometryHelper.Dxf.Viewer.CadDocument). It is given a name for
the step, the changes it plans to existing entities, and the action that writes them:

```csharp
string handle = circle.Handle;
CadEditResult result = document.ApplyEdit("ERASE",
    new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) },
    scope => scope.Document.FindEntity(handle).Erase());
// result: Done (1); document.EntityCount 5; document.NextUndo 'ERASE (1)'
```

| Step | What happens |
|---|---|
| the gate | the document's [`ICadEditGate`](xref:GeometryHelper.Dxf.Viewer.ICadEditGate) is asked about each planned change of an existing entity; one refusal refuses the whole edit, with nothing written |
| the owners | a change to an entity whose owner takes its own edits is handed to the owner first (see [Owners](owners.md)) |
| the action | it writes the change through the entity's own members (`TransformBy`, its setters, `Erase`) and adds entities with `scope.Append(space, entity)`; if it throws, the drawing is read back as it stood, and the result says `Failed` |
| the record | the step is kept for undo, the display lists and pick indexes of the spaces it touched are dropped, and `Changed` is raised |

A [`CadEditResult`](xref:GeometryHelper.Dxf.Viewer.CadEditResult) says `Succeeded`, `Refused` (before anything was written) or
neither (failed), with the `Message` the command line shows and the [`CadEditRecord`](xref:GeometryHelper.Dxf.Viewer.CadEditRecord)s
of what was done. A record has a kind (`Added`, `Erased`, `Transformed`, `PropertyChanged`, `Reshaped`, `TableChanged`,
`SettingChanged`), a handle, and for a property its label and its old and new values as text.

A command that makes several edits is one step: `BeginGroup(name)` opens a group, `EndGroup()` closes it and the outermost
one makes its edits a single step, and `CancelGroup()` takes them all back. While a group is open `CanUndo` is false and
`SaveAs` throws "A command is running: end it before saving."

## Undo and redo

```csharp
document.Undo();                 // the drawing as it stood before the last step
document.Redo();
document.CanUndo; document.NextUndo; document.CanRedo; document.NextRedo;
document.IsModified;             // false again once every step is undone, or the drawing saved
```

The library writes a change into the drawing at once and has no undo of its own, and a record written anew is written in
the library's style, not in the bytes the file had. So an edit is not undone by setting its old values back. The document keeps the drawing
as it stood before the first edit and after every step, each saved to bytes and compressed (a drawing of 10 MB takes about a
tenth of that), and undoing a step reads the drawing it stood at before. Undoing every step gives the drawing back byte
for byte: the plate, saved after six edits and every one undone, is the 5 127 bytes it was opened as.

After an undo or a redo `Database` is a new [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) read from those bytes. The
circle that `FindEntity(handle)` gave before the undo is not the object it gives after, so keep handles, not entities, across
a step. The [`CadSpace`](xref:GeometryHelper.Dxf.Viewer.CadSpace) objects stay and are pointed at the new drawing.

`Save()` writes to the file the drawing was opened from, `SaveAs(path)` to another, which it is then known by. A drawing
opened from a stream or given in memory has no file: `HasFile` is false and `Save()` throws until `SaveAs` gives it one.

## The command line

A [`CadCommandSession`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession) is the command line of one document. It knows no
window: the canvas and the line the user types on call `Feed(point)`, `Feed(text)`, `FeedSelection(handles)`, `Enter()` and
`Escape()`, and a test calls them the same way.

```csharp
var session = new CadCommandSession(document);
session.Feed("L");                  // an alias of LINE
session.Feed("0,100");
session.Feed("@50<0");
session.Feed("@0,20");
session.Enter();
// session.History:
//   Command: L
//   LINE
//   Specify first point: 0,100
//   Specify next point or [Undo]: @50<0
//   Specify next point or [Undo]: @0,20
//   Specify next point or [Close/Undo]:
// document.EntityCount 8; document.NextUndo 'LINE (2)', the two segments one step
```

| The user | Does |
|---|---|
| types a name or an alias and Enter | starts the command; Enter alone runs the last one again; a name that is none gives `Unknown command "FOO".  Press F1 for help.` |
| answers a point | as `x,y`, `@dx,dy`, `@distance<angle` or `distance<angle`; a number alone at a point prompt is a distance along the rubber band, towards the cursor |
| answers with a keyword | by its capital letters, as `U` for `Undo` in `[Close/Undo]` |
| selects first, then starts a command | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR and EXPLODE take the selection made before them, and ask "Select objects:" where there is none |
| answers "Select objects:" | by a click, a window, a crossing, or `ALL` for every entity of the space |
| presses Esc | ends the command; what a LINE drew stays, as in AutoCAD, and what a MOVE had not yet done is not done |

A command that fails part way has its edits taken back. Its outcome is `session.LastOutcome`: `Completed`, `Cancelled` or
`Failed`. A selection of entities on a locked layer is taken without them, and the line says "1 was on a locked layer."

### The commands

[`CadCommandRegistry.CreateDefault()`](xref:GeometryHelper.Dxf.Viewer.CadCommandRegistry) makes the registry of 56 commands, named as AutoCAD names them:

| Group | Commands |
|---|---|
| view | ZOOM (Window, Extents, All, Previous, and a factor as `2` or `0.5x`), PAN, REGEN, PROPERTIES, OPTIONS, QSAVE |
| edit | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO |
| draw | LINE, PLINE, CIRCLE, ARC, RECTANG, ELLIPSE (Arc), POINT, XLINE, RAY, POLYGON, DONUT, TEXT, MTEXT, MLEADER, MVIEW, LAYOUT (New, Rename, Set, ?) |
| inquiry | DIST, AREA (Add area, Subtract area), ID, LIST, MEASUREGEOM (Distance, Radius, Angle, ARea) |
| layers | LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK, LAYISO, LAYUNISO |
| utilities | SELECTALL, UNITS, -UNITS, SCRIPT |
| dimension | DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR |

PROPERTIES, OPTIONS, LAYER and QSAVE ask the window for its panel or dialog (`UiRequested`), and say "is shown by the window"
where none is listening. The window adds CLOSE, NEW, SAVEAS, PROPERTIESCLOSE, GRID, SNAP, ORTHO, POLAR, OSNAP, LAYMCUR and LAYMCH, and the plugin commands. The inquiry commands print in AutoCAD's words: DIST from (0, 0) to (100, 100) says `Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0`; AREA by points or by an object says `Area = 5000.0000, Perimeter = 300.0000`. Each command is a
[`CadCommand`](xref:GeometryHelper.Dxf.Viewer.CadCommand): a name and an iterator that yields each
[`CadPrompt`](xref:GeometryHelper.Dxf.Viewer.CadPrompt) and reads the answer from [`CadCommandContext`](xref:GeometryHelper.Dxf.Viewer.CadCommandContext)`.Input`, so it reads top
to bottom as its prompts do, and a [command plugin](plugins.md) is written the same way.

### Aliases and keys

[`CadShortcuts`](xref:GeometryHelper.Dxf.Viewer.CadShortcuts) holds the aliases and the keys as data. The defaults are
AutoCAD's: 45 aliases (`M` MOVE, `CO` COPY, `E` ERASE, `L` LINE, `PL` PLINE, `C` CIRCLE, `Z` ZOOM, `LA` LAYER, `DLI`
DIMLINEAR, `MV` MVIEW, and the rest) and 12 keys (F3 OSNAP, F7 GRID, F8 ORTHO, F9 SNAP, F10 POLAR, F12 DYN, Delete ERASE,
Ctrl+Z U, Ctrl+Y REDO, Ctrl+S QSAVE, Ctrl+1 PROPERTIES, Ctrl+A SELECTALL). A registry's `Shortcuts` is replaced, from
[global settings](app.md#global-settings), by another set; letters of either case are the same, and a name wins over an
alias of the same letters.

## Object snaps

[`ObjectSnap`](xref:GeometryHelper.Dxf.Viewer.ObjectSnap) turns the cursor into the point a click takes: an object snap, or the
snap grid, or ortho, or polar. It works from the display items under the aperture, through a space's [`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex), so the
lines and arcs of an INSERT snap where the INSERT places them.

```csharp
var snap = new ObjectSnap(Tolerance.Default);
snap.Snap(document.GetPickIndex(document.ModelSpace), new GeoPoint2(99, 1.5), 5);    // Endpoint (100, 0)
snap.Snap(document.GetPickIndex(document.ModelSpace), new GeoPoint2(50.3, 0.4), 5);  // Midpoint (50, 0)
```

| Property | Default | |
|---|---|---|
| `Modes` | Endpoint, Midpoint, Center, Quadrant, Intersection | a [`CadSnapModes`](xref:GeometryHelper.Dxf.Viewer.CadSnapModes) set; Perpendicular, Tangent and Nearest can be added |
| `IsOn` | true | F3 |
| `Ortho`, `Polar`, `PolarAngle` | off, off, a quarter turn | F8, F10 |
| `GridSnap`, `GridX`, `GridY`, `GridOrigin` | off, 10, 10, the origin | F9 |

AutoCAD's order is kept. A snap to a point near the cursor (endpoint, midpoint, centre, quadrant, intersection) wins, the
nearest of them first; then a snap the object under the cursor gives wherever its point lies (perpendicular, tangent, and
the centre of a circle whose rim is under the cursor); then nearest. Where none is found the cursor is put on the grid, then
held level or upright from the base point (ortho) or on a polar angle. The result is a
[`CadSnapResult`](xref:GeometryHelper.Dxf.Viewer.CadSnapResult): the point, which [`CadSnapKind`](xref:GeometryHelper.Dxf.Viewer.CadSnapKind) gave it, and the handle of the object.

## Grips

[`CadGrips.Of(entity)`](xref:GeometryHelper.Dxf.Viewer.CadGrips) lists the grips of an entity, each a
[`CadGrip`](xref:GeometryHelper.Dxf.Viewer.CadGrip) with its index, its [`CadGripKind`](xref:GeometryHelper.Dxf.Viewer.CadGripKind)
and its place; `document.GripDrag(handle, index, point)` drags one, as one undo step.

| Entity | Grips | Dragging |
|---|---|---|
| LINE | two ends, the middle | an end moves it; the middle moves the line |
| CIRCLE | the centre, four quadrants | the centre moves it; a quadrant changes the radius |
| ARC | two ends, the middle, the centre | the middle bends the arc through the point |
| polylines | each vertex, the middle of each segment | a vertex moves; the middle of a straight segment moves it, of an arc segment bends it |
| TEXT, MTEXT, INSERT | the insertion point | moves it |
| VIEWPORT | four corners, the middle | a corner resizes the frame, the opposite corner, the scale and the centre of the view kept |
| anything else, a DIMENSION among it | none | |

The plate's line has `End 0 (0, 0)`, `End 1 (100, 0)` and `Middle 2 (50, 0)`; dragging grip 1 to (120, 0) leaves its end at
(120, 0, 0). A dimension has no grips because the library does not draw a dimension's block anew: its picture is
AutoCAD's, or the model drawing's, and a point dragged would not be seen to move.

## Properties

[`CadProperties.Describe(document, space, handles)`](xref:GeometryHelper.Dxf.Viewer.CadProperties) gives what the Properties panel shows for a selection, a
[`CadSelectionInfo`](xref:GeometryHelper.Dxf.Viewer.CadSelectionInfo) with the text of the selection box ("Circle (1)") and
[`CadPropertyGroup`](xref:GeometryHelper.Dxf.Viewer.CadPropertyGroup)s of [`CadPropertyRow`](xref:GeometryHelper.Dxf.Viewer.CadPropertyRow)s.
For the circle: General (Color ByLayer, Layer Steel, Linetype ByLayer, Linetype scale 1.0000, Lineweight ByLayer) and Geometry
(Center X 50.0000, Center Y 30.0000, Radius 10.0000, Diameter 20.0000, Circumference 62.8319). Entities that differ in a row
show `*Varies*`; with nothing selected the panel shows the drawing's current colour, layer, linetype, linetype scale,
lineweight and transparency, the plot style and the view.

[`CadPropertyEditor.Set(document, handles, label, value)`](xref:GeometryHelper.Dxf.Viewer.CadPropertyEditor) types a value back into a row, as AutoCAD's palette does, one undo
step:

```csharp
CadPropertyEditor.Set(document, new[] { handle }, "Radius", "12.5");     // Done (1); the step is 'Properties (1)'
```

The rows that can be set are Color (ByLayer, ByBlock, Red to White, an index 1 to 255, or `255,128,0`), Layer, Linetype,
Linetype scale and Lineweight of every entity; the ends of a line; the centre, radius, diameter and circumference of a
circle; the centre, radius and start and end angles of an arc; the contents, height, rotation and position of a text. A value
that cannot be taken, a row that cannot be set and an entity on a locked layer are refused with the reason: with Steel
locked, setting the radius gives `Refused: The circle 105 is on a locked layer.` With nothing selected the first five rows set
the drawing's current settings, which new entities take.

The row "Text override" of a dimension is set as in AutoCAD: `<>` stands for the measurement, so `<> TYP` on a dimension of
3 000 shows `3000 TYP`, and an empty text shows the measurement. The text of the dimension's own picture is written again
and one undo takes it back. On a model drawing the edit goes through its owner as a hand edit (see
[Owners](owners.md#what-the-model-drawing-makes-of-each)).

## Layers

Click a layer's colour in the Layer manager to choose it in Select Color: a colour of the index (1 to 255) or a true colour. A true colour is kept in the file as red, green and blue (group 420), with the nearest colour of the index beside it (group 62) for programs that read only the index. AREA after Add area keeps a total: a rectangle 100 by 50 added and a circle of radius 10 taken away leave `Total area = 4685.8407`. SCRIPT feeds each line of a .scr file to the command line as typed, a blank line being Enter, and stops at the first line refused.

[`CadLayerEditor`](xref:GeometryHelper.Dxf.Viewer.CadLayerEditor) makes the edits of the layer table that the layer box, the
Layer manager and the LAYER command make, each one undo step, refused where AutoCAD refuses:

| Call | Does | Refused when |
|---|---|---|
| `Add(document, name)` | a layer of colour White and linetype Continuous | the drawing has a layer of the name, either case: `Layer "notes" already exists.` |
| `SetCurrent`, `SetOn`, `SetFrozen`, `SetLocked` | the flags | a frozen layer is set current: `Cannot set a frozen layer current.` |
| `SetColor`, `SetLinetype`, `SetLineWeight` | the look | |
| `Delete` | the layer | objects are on it: `Layer "Steel" cannot be deleted: objects are on it.` |

A layer turned off or frozen takes its entities out of the display lists; a locked layer keeps them drawn but out of any
selection a command gathers. `IsLocked(database, name)` asks.

## See also

- [Opening, drawing and picking](display.md): the document, the display lists and the index the snaps and grips use.
- [Owners](owners.md): why an edit is sometimes refused with "belongs to the model".
- [Command plugins](plugins.md): a command written outside the presenter.
- [Entities and curves](../dxf/entities.md) and [Editing rules](../dxf/rules.md): what each entity's `TransformBy` accepts.
