# Part A: the 2D presenter

Part A (A16) opens, shows and edits any 2D drawing held in a `DxfDatabase`: a DXF from anywhere, or a sheet Part B made.
It is AutoCAD 2D in handling and looks, kept small on purpose: it grows in steps (D25), and AutoCAD itself stays the
tool for drafting beyond them. It knows nothing of models, Tekla or drawings.

## K1: the document

`CadDocument` holds one `DxfDatabase` open for editing:

- the file name and whether it has changed;
- the selection (entity handles, in model space or in a layout);
- the current space (model space or one layout) and the current layer, colour and linetype;
- the undo stack, one step per command (and one step per plugin run), built on small edit records (entity, property, old
  value, new value) so that Part B can recognise an edit it owns;
- the list of owners (the owner contract in [01-architecture.md](01-architecture.md)).

It never touches a window. Opening, editing and saving run in a test.

## K2: display

Drawing is two jobs kept apart:

- **Entity to primitives.** One renderer per entity kind turns an entity into 2D primitives: segments, arcs flattened to
  a tolerance of the screen, filled polygons (hatches, wipeouts, solids), glyph runs (text through the fonts the
  document names, and the app's own fallback), images. Dimensions, leaders and tables are exploded into the same
  primitives from their block or from their definition. Layer colour, linetype pattern and lineweight are applied here.
  The result is a `DisplayList2` per space or viewport, cached; a changed entity makes only its own primitives again.
- **Primitives to pixels.** SkiaSharp draws the display list under the view's transform. Picking and snapping never use
  the pixels: a `PickIndex` (a grid or tree over the primitives) answers "which entities are within this box" in the
  document's coordinates.

A **viewport** in a layout is a window: it draws the model-space display list under its own transform (centre, scale,
twist), clipped to its frame, with its frozen layers left out. Zooming inside a viewport is MSPACE; the layout itself is
PSPACE.

What is shown for each entity kind is set against AutoCAD in C1: the same DXF opened in both, screenshot beside
screenshot, one kind at a time.

## K3: editing

- **Commands.** A command is a list of prompts (a point, a distance, an angle, a selection, a keyword, a string), run
  through the command line and the canvas as AutoCAD runs them: the prompt `Specify first point or [Undo]:`, keywords by
  their capital letters, Enter and Space to end, Esc to cancel, right click as Enter. A command is data plus a few lines of
  code, and a test drives it with no window.
- **Select first, then command**, and command first, then select, both work, as in AutoCAD.
- **Object snaps:** endpoint, midpoint, centre, quadrant, intersection, perpendicular, tangent, nearest, plus ortho,
  polar and grid snap. Each marker has its shape and colour as the design shows.
- **Grips:** each entity kind lists its grip points and what dragging each one does (a line's ends and middle, a circle's
  centre and quadrants, a polyline's vertices and mid-segments, a text's insertion point, a dimension's definition points
  and text position).
- **Edits** go through the owner contract: before it changes an entity that has an owner, the document asks the owner.
  Entities with none are edited directly.

The commands of C2: MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, REDO, LINE, PLINE,
CIRCLE, ARC, RECTANG, TEXT, MTEXT, DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR, MLEADER, MVIEW, ZOOM,
PAN, REGEN, PROPERTIES, OPTIONS. These basic commands are enough for now (A18); C4 adds more later, as they are needed.

## K4: command plugins

See [60-plugins.md](60-plugins.md): `[CadCommand]` methods on a class, loaded and unloaded as AutoCAD's NETLOAD does.

## Layers and linetypes

The layer table of the open DXF is shown and edited in the ribbon (the layer box) and the Layer manager: name, on, frozen (in all
layouts or in the current viewport), locked, colour, linetype, lineweight, plot. A layer written by Part B for its
entities carries its owner's name (a small mark in the list); Part A does not stop the user switching it off.

## Reading and writing, the rule

Part A reads and writes only what `DxfDatabase` reads and writes. A DXF with something `DxfDatabase` drops is opened
with a notice listing what was dropped, and saving it does not pretend otherwise. A fault found there is fixed in
`GeometryHelper.Dxf`, with its own test, not worked around in Part A.

## Checks

- Every DXF the repository's DXF tests write opens, and what is shown matches AutoCAD's screen, by screenshots.
- An edit made in Part A and saved opens in AutoCAD with AUDIT clean, and `DxfReader` reads back what was written.
- Every command: a view-model test drives its prompts, checks the result and checks that undo restores the start.
- A fuzz run of random commands on a test drawing: no exception, the DXF always valid.
- Speed: opening and panning a drawing of 100 000 entities, picking in it, timed in the benchmarks (C1).
