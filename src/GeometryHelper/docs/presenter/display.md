# Opening, drawing and picking

A drawing opened as a [`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument), turned into what the canvas draws, and
asked what lies under a point or inside a box. Nothing on this page needs a window: the document, its display lists and
its index are plain objects, which is how the presenter's own tests drive them.

The examples use a file of six entities, a rectangle of four lines 100 by 60 on a layer Steel, a circle of 20 across at
its middle on the same layer, and a TEXT "PLATE" 5 high above it at (10, 70).

## The document

```csharp
CadDocument document = CadDocument.Open("plate.dxf");             // or Open(stream, name), or From(database, name)
document.Title;                                                   // "plate.dxf"
document.EntityCount;                                             // 6, the entities of model space
document.Spaces;                                                  // Model, Layout1
document.Layers;                                                  // 0 (white) and Steel (red, #FF0000)
```

| Member | What it gives |
|---|---|
| `Open(path)`, `Open(stream, name)` | the drawing read by [`DxfDatabase.Open`](xref:GeometryHelper.Dxf.DxfDatabase); a stream has no file of its own, so `HasFile` is false and `Save()` refuses where `SaveAs(path)` does not |
| `From(database, name)` | a drawing already in memory, as a model drawing's is |
| a drawing with no handles | one of R12 written by hand, with neither header nor tables, is read anew with a handle for each entity, as AutoCAD gives them on opening it: the next above every handle of the file, in its order, with `$HANDSEED` above them and `$HANDLING` 1, a header made where it has none. The document picks, snaps and edits by handle and an undo reads the drawing back from its saved bytes, so the handles are written, and saved with it, as AutoCAD saves it. Three walls and a column read as LINEs 1 to 3 and the CIRCLE 4, and `Database` is the copy read anew |
| `Database` | the [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase); a new object after an undo or a redo, so hold the handle of an entity, not the entity |
| `FindEntity(handle)`, `FindSpace(handle)` | the entity of a handle, or null where there is none or it is erased; the space that holds it |
| `Layers` | a [`CadLayerInfo`](xref:GeometryHelper.Dxf.Viewer.CadLayerInfo) for each layer: name, colour as 0xRRGGBB, on, frozen, locked, linetype, line weight in millimetres |
| `Selection` | the [`CadSelection`](xref:GeometryHelper.Dxf.Viewer.CadSelection) of the document (see [Selecting](#selecting)) |

## Spaces

A [`CadSpace`](xref:GeometryHelper.Dxf.Viewer.CadSpace) is model space or one layout. `Spaces` lists model space first, then
the layouts in the order of their tabs, a tie going by name; the layout "Model" of the file is not listed twice.
`CurrentSpace` is the one the canvas shows.

A layout is a sheet of paper, and `PaperBounds` says where the paper lies in the layout's units, as AutoCAD shows it behind
the layout: the paper size of its plot settings about its plotted area, the lower left corner of the printable area at the
origin. A layout of A3 on its side with margins of 7.5 at the sides and 20 above and below lies from (-7.5, -20) to
(412.5, 277). Where a layout has no paper size its limits are taken, and where it has neither, what it draws with a margin
of 2 per cent of its larger side. Model space has no paper and its `PaperBounds` is empty.

## What is drawn

`GetDisplay(space)` gives the [`DisplayList2`](xref:GeometryHelper.Dxf.Viewer.DisplayList2) of a space, made the first time
and kept. It holds [`DisplayItem2`](xref:GeometryHelper.Dxf.Viewer.DisplayItem2) objects in drawing order, the first at the
back, and the box that holds them all. Each item carries the handle of the entity it came from, its layer, and a
[`DisplayStyle`](xref:GeometryHelper.Dxf.Viewer.DisplayStyle): colour, line weight and dashes, already worked out.

`PrepareDisplay(space, progress, cancellationToken)` makes the list and the pick index of a space ahead, item for item the
list `GetDisplay` makes, telling `progress` the share of the space's entities drawn every 4 096 of them and stopping, with
an `OperationCanceledException`, when the token is cancelled; a list stopped is not kept, and is made whole when next
asked for. The window calls it on a worker thread for a drawing it opens. A document is not safe to use from two threads
at once, so nothing else may use it until it returns.

| Item | What it is |
|---|---|
| [`DisplayPolyline2`](xref:GeometryHelper.Dxf.Viewer.DisplayPolyline2) | a chain of points, open or closed: a line is one of two points; an ellipse and a spline are sampled into one |
| [`DisplaySegments2`](xref:GeometryHelper.Dxf.Viewer.DisplaySegments2) | many separate segments in one item: the lines of a hatch's pattern |
| [`DisplayRay2`](xref:GeometryHelper.Dxf.Viewer.DisplayRay2) | a line without end from its base point: both ways for an XLINE, on from it for a RAY; the canvas draws the part the view shows (`TryClip`) |
| [`DisplayArc2`](xref:GeometryHelper.Dxf.Viewer.DisplayArc2) | an arc or a whole circle, kept as an arc and flattened by the canvas to the tolerance of the screen |
| [`DisplayFill2`](xref:GeometryHelper.Dxf.Viewer.DisplayFill2) | a filled outline with its holes: a solid hatch, a SOLID, and a WIPEOUT, which masks what is under it |
| [`DisplayText2`](xref:GeometryHelper.Dxf.Viewer.DisplayText2) | a text with its place, height, turn, width factor, justification and font name; the canvas sets the letters |
| [`DisplayViewport2`](xref:GeometryHelper.Dxf.Viewer.DisplayViewport2) | a viewport of a layout: its frame, and the model-space items it shows |

The six entities of the file give six items: four `DisplayPolyline2`, one `DisplayArc2` and one `DisplayText2`, in a box
from (0, 0) to (100, 75). A polyline of a LWPOLYLINE with an arc segment gives a `DisplayPolyline2` and a `DisplayArc2`.

How each entity is drawn:

| Entity | Drawn as |
|---|---|
| LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE | lines and arcs; a bulge stays an arc |
| ELLIPSE, SPLINE | sampled: a spline into 128 segments |
| POINT | a polyline of one point |
| 3DFACE | its edges |
| SOLID, WIPEOUT | a fill; a wipeout is a mask |
| HATCH | a solid fill, a gradient filled with the colour half way along it, or the lines of its pattern cut by its loops; a hatch that holds no lines of its pattern, or would draw more than 20 000, is drawn by its boundary |
| TEXT | a text item; the library has no fonts, so the box of a text is estimated from its height and its characters |
| MTEXT | a text item whose `Parts` are its picture, laid out as AutoCAD 2021 lays it out: the mask and the frame, then a text for each run of each line and a polyline for each line under, over or through a run and for the bar of a stack; see below |
| INSERT, MINSERT | the block's entities under the INSERT's matrix, to a depth of 12 blocks; every item is picked as the INSERT; the values of its ATTRIBs are drawn where the file puts them, an invisible one left out |
| DIMENSION, TABLE | the block AutoCAD drew it with |
| LEADER, MLEADER | exploded, as AutoCAD breaks them: the arrowhead, then the line or spline |
| VIEWPORT | a `DisplayViewport2` |
| IMAGE | the outline of its frame; no pixel is read |
| RAY, XLINE | a `DisplayRay2`, across every view it crosses; its box reaches without end the ways it runs, so a pick anywhere along it finds it, while the box of the list holds its base point alone, as AutoCAD's ZOOM Extents does: a line 100 by 50 from the origin and an XLINE through (200, 300) zoom to the box from (0, 0) to (200, 300). A crossing box or lasso it runs through takes it; a window never does |

An entity that is not visible, or lies on a layer that is off or frozen, is left out. ByLayer and ByBlock colour, line weight
and linetype are resolved here, a line weight left at the default being 0.25 mm, and dashes are the linetype's pattern
times LTSCALE and the entity's own scale. One entity that cannot be drawn never stops the others: it is passed over.

### MTEXT

The words of an MTEXT are set in the fonts of the document (`document.Fonts`), measured letter by letter, by the rules
AutoCAD 2021 was found to follow on 155 MTEXTs of 536 lines:

- **Wrapping.** The words wrap at the defined width (group 41) at their spaces alone: never after a hyphen, a word too
  long stands whole past the width, and `\~` holds two words together. A width of 0 wraps nothing. The attachment (71)
  hangs the lines by a corner, a side or the middle of the box the width and the lines make; a defined height (46)
  taller than the lines moves no line.
- **Line spacing.** From one baseline to the next is 5/3 of the text height times the factor (44). Under `Exactly`
  (73 = 2) it is that whatever the runs. Under `AtLeast` (73 = 1) each line has a top, the height of its tallest run or
  of its stack, its first baseline that far below the top of the MTEXT; the next line's top lies the factor times
  (the top of this line + a third of its tallest run's height + a third of the text height) below this line's top. For
  lines of the text height alone that is the same 5/3; in an MTEXT 2.5 high, the line after one with a run 5 high has
  its top 5 + 5/3 + 2.5/3 = 7.5 below that line's top. An empty paragraph is a line of 5/3 of the height.
- **Stacks.** `\S1/2;` sets its parts one over the other with a bar, `\S1^2;` with none, and `\S1#2;` along a diagonal,
  each part as high as its ink; `\A0;`, `\A1;` and `\A2;` set the words beside a stack at its bottom, middle or top.
- **Paragraphs.** In a `\p` code, `i`, `l` and `r` indent the first line, the left and the right; `ql`, `qc`, `qr`,
  `qj` and `qd` align left, centre, right, justified and distributed; `t` sets tab stops (left, centre, right, decimal;
  with none a stop every 4 heights); `b` and `a` give space before and after the paragraph, and `sm`, `se` and `sa` a
  line spacing of the paragraph's own (a multiple, exactly, at least).
- **Columns.** Static and dynamic columns take the lines that fit their height, each column its width and the gutter
  on from the last; `\N` starts the next column.
- **Mask and frame.** The background mask reaches the scale factor (45) less 1 of a text height beyond the lines, and
  the frame (R2018) stands where a mask of its scale would, 1.5 for a frame with no mask.

"II II II" 20 wide in letters 10 high, whose I is inked 1 in and moves the pen 4, is set as "II II" and, 16.67 below it,
"II": an item whose box is 20 wide and 26.67 high. The box of an MTEXT's item is that of its parts, so picking, window
selection and the highlight follow the lines as they are drawn. Not as AutoCAD: a flow direction (72) of top to bottom
is set left to right, `\pxsm` with a factor is 0.05 of a height off on one line in 536, and `\M+` codes are shown as
written.

The MTEXT editor (TEXTEDIT, MTEXT) sets the words being typed by this same layout, as AutoCAD's in-place editor shows
them as they will look:
[`CadMTextEditorLayout`](xref:GeometryHelper.Dxf.Viewer.CadMTextEditorLayout) lays out the editor's words with the
MTEXT's width, attachment, spacing, columns, mask and frame, and gives the parts the canvas would draw and where each
place of the editor stands. The editor's frame draws those parts at the canvas's zoom, so a line breaks, a baseline lies
and a stack stands in the editor where it will be drawn. In "II II II" 20 wide the caret after the last letter is on the
second line, 7 along it, and the space the first line breaks at stays at that line's end, at 20. A word typed past the
width takes the caret down with it; Home, End and the arrows up and down follow the lines as they wrap; an Enter at the
end makes an empty line for the caret, which the drawn MTEXT does not have. Typing in an MTEXT of 2 000 letters lays it
out again in about 0.5 ms a key and draws it in about 1 ms. The editor still lacks AutoCAD's own aids: its ruler shows no
indents or tab stops of the paragraph, the columns cannot be dragged, and a width code in times of a style whose width
is not 1 is set as if the style's width were 1.

### Viewports

A layout shows model space through its viewports. A `DisplayViewport2` has the frame in paper units (`CenterX`, `CenterY`,
`Width`, `Height`), the point of model space at the middle of the frame (`ViewCenterX`, `ViewCenterY`), `Scale`, the paper
length of one unit of model space (a viewport at 1:20 has 0.05), the `Twist` in radians, the layers frozen in that viewport
only, and the model-space display list it draws, clipped to the frame. `ModelToPaper` is the matrix from one to the other.
Because a viewport draws model space, an edit in model space drops the display lists of every layout too.

### Keeping the lists

A display list and its index are kept until an edit touches the space, when its own are dropped and made again the next time
they are asked for; `Changed` says which spaces and which handles. `Regen()` drops every list, as REGEN does. A change
to the layer table, a layer switched off or given a colour, makes the builder read the table again.

## The view

[`CadView`](xref:GeometryHelper.Dxf.Viewer.CadView) is what part of a space the canvas shows and at what zoom: the point of
the drawing at the middle of the canvas, and the pixels one drawing unit takes. Panning and zooming are arithmetic on those
three numbers, so they are tested here. Y runs up in the drawing and down on the screen.

```csharp
var view = new CadView(800, 600);
view.ZoomTo(display.Bounds);                       // 7.2 pixels to the unit: the plate's 100 by 75 plus 5 per cent each side
view.DrawingToPixel(50, 30, out double px, out double py);   // (400, 354)
view.ZoomAt(100, 100, 1.2);                        // one wheel notch in, about the pixel (100, 100): 8.64 pixels to the unit
view.Pan(40, 0);                                   // the drawing moves 40 pixels to the right
```

| Member | What it does |
|---|---|
| `ZoomTo(box, margin)` | shows a box wholly with a margin of 5 per cent of the canvas on each side, as ZOOM Extents; an empty box shows the origin at 1 pixel to the unit |
| `ZoomAt(px, py, factor)` | zooms about a pixel, the point of the drawing under it staying under it |
| `Pan(dx, dy)` | moves the drawing under the canvas by pixels |
| `ScreenToDrawing`, `DrawingToPixel` | the two ways between a pixel and a point |
| `VisibleBox`, `DrawingToScreen` | the box of the drawing on the canvas, and the matrix from the drawing to the pixels |
| `Resize(w, h)` | a new canvas size, the centre kept |

The zoom is held between 1E-9 and 1E9 pixels to the unit.

## Picking

[`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex) is a grid over the items of one display list that answers which of
them lie near a point or in a box without looking at every item. It works in the drawing's coordinates, never in pixels, so
a caller working in pixels divides by the zoom first. `GetPickIndex(space)` gives the index of a space, made once and kept
with its display list.

| Call | Gives |
|---|---|
| `Pick(x, y, tolerance)` | the handles of the entities drawn within `tolerance` of the point, in the order AutoCAD picks them: the one drawn on top first while the drawing's SORTENTS holds its bit 1 (object selection, in AutoCAD's default 127), else the one drawn first, however near the others lie. Two lines crossing at the point come back the one drawn later first; with a tolerance of 3, a line drawn on top 2.7 away comes before one under the point |
| `IndicesIn(box)` | the places of the items whose box meets the box, in drawing order: what the canvas draws of a view |
| `Window(box)` | the entities drawn wholly inside the box, as a window selection takes them |
| `Crossing(box)` | the entities that cross or lie in the box, as a crossing selection takes them |
| `ItemsNear(box)` | the items whose box meets the box, in drawing order, as object snaps ask for what lies under the aperture |

An entity is picked as a whole: all the items it was drawn as answer with its one handle, and the entities that make up an
INSERT answer as the INSERT. On the plate:

```csharp
index.Pick(50, 40.5, 2);                           // the circle: 0.5 off its rim
index.Window(new DisplayBox(-1, -1, 101, 61));     // the four lines and the circle; the text, above y = 70, is out
index.Window(new DisplayBox(-1, -1, 60, 61));      // the circle and the left edge, the only two wholly inside
index.Crossing(new DisplayBox(-1, -1, 60, 61));    // those two and the bottom and top edges, which it cuts
```

The grid has up to 512 cells a side, the square root of the number of items. An item whose box covers more than a quarter
of the grid is kept in a list of its own, so one large hatch does not slow every cell. On 99 856 lines a pick takes 0.002 ms
and a window of 100 by 100 that takes 100 of them 0.56 ms.

## Selecting

[`CadSelection`](xref:GeometryHelper.Dxf.Viewer.CadSelection) holds the handles of the selected entities, ignoring case.

```csharp
document.Selection.Set(index.Pick(50, 40.5, 2));   // the circle
document.Selection.Add(index.Window(box));         // more
document.Selection.Toggle(handle);                 // as Shift+click does
document.Selection.Changed += (s, e) => { /* Properties and grips follow */ };
```

`Add`, `Remove`, `Toggle`, `Set` and `Clear` raise `Changed` only when the set changes. On the canvas a click picks within the
pickbox, Shift+click toggles, and a click on nothing starts a box that the next click ends: left to right a window, right to left
a crossing, Esc dropping it (Select by dragging in Global settings lets it be dragged too) (see [Settings, autosave and the window](app.md#the-keys-and-the-mouse)).

## See also

- [Editing as AutoCAD does](editing.md): the selection is what MOVE, ERASE and Properties work on.
- [Owners](owners.md): an entity whose owner keeps it read-only is still drawn, picked and selected.
- [Reading DXF](../dxf/reader.md#reading-dxf): the shapes the display is made from.
