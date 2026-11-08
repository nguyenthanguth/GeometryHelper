# Making it again

A [`Drawing`](xref:GeometryHelper.Drawing.Drawing) is made, saved as one DXF, opened again, shown in the presenter and made
again from the model as it is now, with the settings it has or others, its hand edits kept and what was drawn by hand left as
it is. This page is the life of a drawing: its members, what an update does to what a person did, and the calls that change
one view.

## Made, saved and opened

```csharp
Drawing drawing = DrawingBuilder.Build(source, settings, Tolerance.Default);    // see Settings
drawing.Save("beam.dxf");                                                       // the sheet and its document, one file

Drawing again = Drawing.Open("beam.dxf", source, Tolerance.Default);            // source may be null
```

| Member | Gives |
|---|---|
| `Id` | the id of the main object: `B1` |
| `Source`, `Settings`, `Tolerance`, `Scale` | the objects it was last made from; a copy of the settings (changing it changes nothing); the [`Tolerance`](xref:GeometryHelper.Tolerance); the scale of the views, 10 for 1:10 |
| `Views`, `Sheet`, `Warnings` | the [`DrawingView`](xref:GeometryHelper.Drawing.DrawingView)s with their lines, dimensions and marks; the sheet; what went wrong, in words |
| `Elements` | the elements of the document: the views, then the settings, the sheet, the title block and the part list, then each view's dimensions and marks |
| `Edits`, `PluginRuns` | the hand edits in the order they were made; the plugin runs |
| `Database` | the DXF it lives in; the presenter's, once it is shown there, since undoing there reads the drawing anew |

`Open` reads the document back from the DXF and finds the entities by their tags. It needs the file to carry a drawing document:
a DXF of any other kind throws `ArgumentException`, `The file ... carries no model drawing.` The drawing opened has its
elements, its edits and its settings as saved, and its views empty: its lines are in its DXF, not in `Views`.

Opened in the app, by Open or from the file list, a DXF that carries a drawing document is opened as `Drawing.Open(document, null, tolerance)` opens it: over the presenter's document, its owner registered, so that a mark is `MoveOnly` there at once and saving writes to the file it came from. It is listed under "Opened from a file · model drawings" until it is updated from a source, which Update from model asks for, since the file does not keep where the drawing was made from. A host that calls `OpenDocument` with a document it read itself gets no drawing: only `MainViewModel.Open(path)` tells the extensions.

```
opened id B1, 0 views, 16 elements, 0 edits, scale 10, sheet A3
```

A drawing opened and not yet updated cannot be changed by one view: `ReDimension("V-FRONT")` throws `A drawing opened from a
file is changed once it is updated from its source.` The first `Update(source)` makes its views, and reports no change where the
source is the one it was made from:

```
Update: objects +0 -0 ~0; elements +0 -0 ~0; edits kept 0, dropped 0; plugin runs 0      (and Views.Count is 3)
```

## Showing a drawing in the presenter

```csharp
CadDocument document = drawing.OpenInPresenter();
```

gives a document over the drawing's DXF with the drawing's [owner](../presenter/owners.md) registered, so that an edit of an
entity the drawing wrote goes through the drawing. The drawing follows the document from then on: `Database` is the document's,
and undoing there takes the hand edit back too. A document made so has no file of its own (`HasFile` is false); `drawing.Save(path)`
writes it. `OpenInPresenter` works on a drawing just opened as well as one just made: the mark of a drawing opened from `beam.dxf`
is `MoveOnly` there, and MOVE of it by (30, 0) records the hand edit `V-FRONT/M-P1 Move: (none) -> 30,0`, which a
`Drawing.Open` of the file saved afterwards reads as one edit and an undo takes back.

Shown in the presenter, a beam drawing of 96 entities (model space and the layout) is held as follows:

| Rule | Entities | What they are |
|---|---|---|
| `ReadOnly` | 82 | the projected lines, the hatch, the symbols, the frame, the title block, the part list |
| `MoveOnly` | 9 | the entities of the mark, and the viewport and the title of each view |
| `Edit` | 4 | the dimensions |
| `Free` | 1 | the viewport of the sheet itself, which has no tag |

## Hand edits

An edit made in the presenter to something the drawing owns is a hand edit: a [`DrawingEdit`](xref:GeometryHelper.Drawing.DrawingEdit)
of the id of the element, the kind of edit, and its values, kept in the document and in the DXF at once, in the same undo
step. The kinds and the form of their values are those of the presenter's [`CadEdit`](xref:GeometryHelper.Dxf.Viewer.CadEdit).
A mark `V-FRONT/M-P1` dragged 40 up is `new DrawingEdit("V-FRONT/M-P1", CadEditKind.Move, newValue: "0,40")`, with a [`CadEditKind`](xref:GeometryHelper.Dxf.Viewer.CadEditKind) as the presenter names it.

| Element | Keeps | Refuses, with why |
|---|---|---|
| a mark, a view | a move | `a mark is only moved` |
| a dimension | a move, a text override, its erasing | `a dimension keeps a move, a text override or its erasing, not a rotate` |
| anything else | nothing | `the symbol V-FRONT/SM-B is not edited by hand` |

A move must read as `dx,dy`. The presenter tells the owner of the edit before it is made, so a refusal there is the words the command
line shows (`This line belongs to the model and cannot be changed here.`) and the drawing is as it was.

## Update

```csharp
UpdateReport report = drawing.Update(newSource);                  // the same settings
UpdateReport other = drawing.Update(newSource, newSettings);      // other settings, copied; null for the ones it has
```

`Update` makes the drawing again from the model as it is now: views, dimensions, marks, symbols and sheet. It takes out only the
entities it wrote, those with its tag, and writes them anew; applies again the hand edits that still apply; takes out and
reports those that no longer do; leaves what was drawn by hand; and keeps the plugin runs. Shown in the presenter, the update is
one undo step there, and undoing it brings back the DXF, the hand edits, the settings and the views as they were (the drawing
keeps the last twenty states for that).

The beam of the first look made 3 500 long instead of 3 000 is updated in 33 ms, with a mark dragged 40 up:

```
Update: objects +0 -0 ~2; elements +0 -0 ~8; edits kept 1, dropped 0; plugin runs 0
```

Two objects changed (the beam, whose body moved, and the plate, which moved with it), eight elements changed (the three views, the
four overall dimensions and the mark), and the overall dimension of the front view reads 3500. The scale
went from 1:10 to 1:15, since the views no longer fit at 1:10. The hand edit is kept. A line drawn by hand on the sheet before the
update, from (0, -500) to (500, -500), is still there after it, and the number of the drawing's own entities is the same, 94.

When the plate is taken out of the model, the update reports `objects +0 -1 ~0; elements +0 -5 ~4; edits kept 0, dropped 1`, and
the edit is dropped with its reason: `V-FRONT/M-P1 Move: (none) -> 0,40: the object P1 is no longer in the model`. An edit is
dropped when an object it names is gone (`the object P1 is no longer in the model`, or `the objects P1, P2 are no longer in the
model`), when the drawing no longer makes the element (`the drawing no longer makes the mark V-FRONT/M-P1`), when it has nothing of
that id, or when the kind of edit is not one the element keeps.

### The report

An [`UpdateReport`](xref:GeometryHelper.Drawing.UpdateReport) lists, each in the order of the ids, character by character:

| Member | Holds |
|---|---|
| `AddedObjects`, `RemovedObjects`, `ChangedObjects` | ids of the model's objects the drawing had not, had and no longer has, and has with a body, frame or text that changed, to the bit; `ChangedObjects` is empty for a drawing opened from a file with no source to compare |
| `AddedElements`, `RemovedElements`, `ChangedElements` | ids of the views, dimensions and marks, likewise |
| `EditsKept` | the hand edits that still apply, applied again, in the order they were made |
| `EditsDropped` | a [`DroppedEdit`](xref:GeometryHelper.Drawing.DroppedEdit) for each that no longer applies: the `Edit` and the `Reason` in words; they are taken out of the drawing |
| `PluginRunsToRunAgain` | the plugin runs the drawing keeps; nothing runs them yet |

`ToString()` gives the one line above.

## Prepare and apply

Making is the slow part of an update and touches neither the drawing nor its DXF, so a window can do it on a thread of its own:

```csharp
PreparedUpdate prepared = drawing.Prepare(newSource, null);       // on any thread; null settings for the ones it has
UpdateReport report = drawing.Apply(prepared);                    // on the presenter's thread
```

`Update(source, settings)` is `Apply(Prepare(source, settings))`. A [`PreparedUpdate`](xref:GeometryHelper.Drawing.PreparedUpdate)
carries the `Drawing`, the `Source`, a copy of the `Settings`, the `Scale` the views come out at and the `Warnings`, and `IsApplied`. It is
applied once, to the drawing that prepared it: a second `Apply` throws `The update has been applied already.`, and one given to another
drawing throws `ArgumentException`. The hand edits are judged as they are when the update is applied, so one made while it was
being prepared is kept, or reported, as any other. The beam is prepared in 9 ms and applied in 8; the class's own measure for the 1 000
members of a general arrangement is about a second to prepare and a tenth of that to apply.

## Adding and changing one view

Six calls change the settings for one view and make the drawing again from its source, the hand edits kept, as `Update(source,
settings)` does; each is one undo step in the presenter, and each returns an `UpdateReport`. They throw
`InvalidOperationException` for a drawing opened and not yet updated, and `ArgumentException` for a view the drawing has not.

| Call | Does | On the beam of the first look |
|---|---|---|
| `ReDimension(viewId)` | dimensions the view by the rules, whatever its kind; kept through later updates ([`ViewSettings`](xref:GeometryHelper.Drawing.ViewSettings)`.DimensionedViews`) | `ReDimension("V-RIGHT")` adds ten dimensions: the chain through the plate's holes, the hole edges and the overalls |
| `ReMark(viewId)` | marks the view as the first view is marked; kept (`MarkedViews`) | `ReMark("V-TOP")` adds `V-TOP/M-P1` |
| `ReCentreLines(viewId)` | draws the centre lines of a view again from the holes, bolts and tubes it sees | nothing changes where the source has not |
| `AddView(kind)` | adds a view along the axes or the isometric view; a section, a detail or a free view is refused | `AddView(`[`ViewKind`](xref:GeometryHelper.Drawing.ViewKind)`.Isometric)` adds `V-ISOMETRIC` |
| `AddSection(name, viewId, cutLine, depth)` | adds a section cut along a line drawn in a view along the axes | `AddSection("B", "V-FRONT", new `[`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2)`(P(1500, -200), P(1500, 200)), 100)` adds `V-SECTION-B` with its two dimensions and the mark `V-FRONT/SM-B` |
| `AddDetail(name, viewId, area, scale)` | adds a detail of a rectangle of a view, at a scale no smaller than the view's | `AddDetail("C", "V-FRONT", new `[`GeoRectangle2`](xref:GeometryHelper.Geometry.GeoRectangle2)`(P(2950, 0), 300, 400), 5)` adds `V-DETAIL-C` and `V-FRONT/DM-C` |

The name is the letter of the section or detail, and a name a drawing has already is refused. `AddDetail` takes the scale the
detail is drawn at, 5 for 1:5, and works out its enlargement from the scale of its view: the detail above is 1:5 beside views
at 1:15, and stays twice as large as the view when a later update takes another scale for the views.

The settings are what keeps the change: after the calls above, `drawing.Settings` has one section, one detail, the kinds front,
top, right and isometric, and `DimensionedViews` holding `V-RIGHT`. `ReCentreLines` changes no setting, since every view has its
centre lines: it only makes the drawing again.

## See also

- [Settings, scale and sheet](settings.md): the settings an update may change, and how the scale is chosen again.
- [The document in the DXF](document.md): where the hand edits and the elements are kept.
- [Owners](../presenter/owners.md): how the presenter hands an edit to the drawing.
- [Views, projection, sections and details](views.md): what the added views are.
