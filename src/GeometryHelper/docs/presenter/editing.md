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
| answers "Select objects:" | by a click, a window, a crossing, a lasso, or one of AutoCAD 2021's options typed (below); Shift held takes out what a click, a box or a lasso picks |
| presses Esc | ends the command; what a LINE drew stays, as in AutoCAD, and what a MOVE had not yet done is not done |

A command that fails part way has its edits taken back. Its outcome is `session.LastOutcome`: `Completed`, `Cancelled` or
`Failed`. A selection of entities on a locked layer is taken without them, and the line says "1 was on a locked layer."

"Select objects:" takes the options of AutoCAD 2021, each by its capital letters:

| Option | Takes |
|---|---|
| `W`, `C` | a window or a crossing by two corners (`Specify first corner:`, `Specify opposite corner:`), whichever way the box is drawn |
| `BOX` | two corners, a window drawn left to right and a crossing right to left |
| `F` | what a fence crosses, by points (`Specify first fence point:`, `Specify next fence point or [Undo]:`) until Enter |
| `WP`, `CP` | what lies inside a polygon or what it touches, by points (`First polygon point:`, `Specify endpoint of line or [Undo]:`) until Enter |
| `ALL`, `L`, `P` | every object of the space; the last object made that the space shows; the objects the command before took |
| `G` | the members of a group by name (`Enter group name:`) |
| `R`, `A` | from `R` on, what is picked is taken out (`Remove objects:`) until `A` |
| `U` | takes back the last change of the selection |
| `M` | points picked until Enter, then the object at each, two picks at one crossing taking both lines |
| `SI` | ends the selection with the first objects taken |
| `AU` | a click on an object picks it, on nothing starts a box, as without it |

The line counts as AutoCAD's: `1 found`, `2 found (1 duplicate), 3 total`, `1 found, 1 removed, 2 total`. Anything else is
`*Invalid selection*` with `Expects a point or` and the options. Window, Crossing, BOX, Fence, WPolygon and CPolygon draw
their band on the canvas as their points are given (`CadPrompt.Band`), and
[`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex) answers them: `Fence(points)`, `WindowPolygon(ring)` and
`CrossingPolygon(ring)` over 100 000 lines each take under 70 milliseconds.

MIRROR follows the drawing's MIRRTEXT, as AutoCAD 2021 does. At 0, AutoCAD's default, a TEXT, an MTEXT and the attributes
of an INSERT stay readable: the box of their words is mirrored, and they read within the half of the circle they read in
before. "ABC" 2.5 high turned 30 degrees at the origin, mirrored across the line y = 10, reads at 330 degrees from
(-1.25, 17.835); turned 120 degrees and mirrored across an upright, it reads at 240. A text aligned between (0, 0) and
(20, 0) mirrored across x = 30 runs from (40, 0) to (60, 0); an MTEXT attached top left at the origin, mirrored across
x = 40, is attached top right at (80, 0). At 1 they are mirrored with the rest.

### The commands

[`CadCommandRegistry.CreateDefault()`](xref:GeometryHelper.Dxf.Viewer.CadCommandRegistry) makes the registry of 167 commands, named as AutoCAD names them, and of the system variables SETVAR sets:

| Group | Commands |
|---|---|
| view | ZOOM (Window, Extents, All, Previous, and a factor as `2` or `0.5x`; `nXP` through a viewport), PAN, REGEN, PROPERTIES, OPTIONS, QSAVE, MSPACE, PSPACE, VPSCALE |
| edit | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO |
| draw | LINE, PLINE (Arc, Close, Halfwidth, Length, Undo, Width; arcs by Angle, CEnter, Direction, Radius, Second pt), CIRCLE (3P, 2P, Ttr, and Tan, Tan, Tan as 3P with `_tan` before each pick), ARC (three points; Start, Center, End, Angle or chord Length; Center first; Start, End, Angle, Direction or Radius; Enter to go on from the last line or arc; Ctrl held to draw the other way), RECTANG, ELLIPSE (Arc), POINT, XLINE, RAY, POLYGON, DONUT, TEXT (Justify, Style; Enter goes on below the last text), MTEXT (Height, Justify, Line spacing, Rotation, Style, Width, Columns), REVCLOUD (Arc length, Object, Rectangular, Polygonal, Freehand, Style, Modify), MLEADER, MLEADERSTYLE, MVIEW, LAYOUT (New, Rename, Set, ?) |
| inquiry | DIST, AREA (Add area, Subtract area), ID, LIST, MEASUREGEOM (Distance, Radius, Angle, ARea) |
| layers | LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK, LAYISO, LAYUNISO, VPLAYER (?, Freeze, Thaw, Reset, Newfrz, Vpvisdflt), LAYVPI |
| utilities | SELECTALL, UNITS, -UNITS, SCRIPT, SETVAR (?) |
| modify | TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN, LENGTHEN, PEDIT (Close, Open, Join, Width, Edit vertex, Fit, Spline, Decurve, Ltype gen, Reverse, Undo; Multiple), OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, ARRAYRECT, ARRAYPOLAR, -ARRAY, ALIGN |
| blocks | BLOCK, -BLOCK, INSERT, -INSERT, WBLOCK, -WBLOCK, ATTDEF, -ATTDEF, EATTEDIT, ATTEDIT, -ATTEDIT, ATTSYNC (?, Name, Select), BATTMAN, BASE, BEDIT, -BEDIT (?), BSAVE, BSAVEAS, BCLOSE (Save, Discard, Cancel), BAUTHORPALETTE |
| text and clipboard | TEXTEDIT, MTEDIT, COPYCLIP, CUTCLIP, COPYBASE, PASTECLIP, PASTEBLOCK, PASTEORIG, PASTESPEC |
| plot | PLOT, -PLOT, PREVIEW, PAGESETUP, PNGOUT, JPGOUT, BMPOUT ([Plotting](plot.md)) |
| dimension | DIM (Angular, Baseline, Continue, Ordinate, aLign, Distribute, LAyer, Undo), DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMORDINATE (Xdatum, Ydatum, Mtext, Text, Angle), DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK, DIMJOGLINE (Remove), DIMREASSOCIATE (Select object), DIMDISASSOCIATE, -DIMDISASSOCIATE |
| tables | TABLE, -TABLE (Style, Width, Height), TABLESTYLE, TABLEDIT |
| selection | QSELECT, FIND, DRAWORDER (Above objects, Under objects, Front, Back), TEXTTOFRONT, GROUP, -GROUP (?, Order, Add, Remove, Explode, REName, Selectable, Create), UNGROUP, CLASSICGROUP |
| housekeeping | PURGE, -PURGE (Blocks, Dimstyles, LAyers, LTypes, MUltileaderstyles, Plotstyles, SHapes, textSTyles, Tablestyles, Visualstyles, Regapps, Zero-length geometry, Empty text objects, All) |
| references and images | XATTACH, IMAGEATTACH, XREF, EXTERNALREFERENCES, IMAGE, -XREF (?, Bind, Detach, Path, pathType, Unload, Reload, Overlay, Attach), -IMAGE (?, Detach, Path, Reload, Unload, Attach), IMAGECLIP (ON, OFF, Delete, New boundary), TRANSPARENCY |

A dimension's picture is laid out from its style as AutoCAD 2021 lays it out: DIMTAD puts the words on the line, which
is broken about them by DIMGAP (0), above it (1, and JIS, 3), on the side away from the points measured (2) or below it
(4); DIMJUST puts them in the middle, next to an extension line or over one; and where the words and the arrowheads do
not both fit, DIMTIX, DIMSOXD, DIMATFIT and DIMTOFL say what moves outside, DIMTIH and DIMTOH whether the words stay
level. With ISO-25's sizes a dimension of 100 has its words "100" in the middle, one of 5 its words inside and its
arrowheads outside, and one of 7 its arrowheads inside and its words 3.9 past the second extension line. DIMORDINATE
(DOR) measures a point from the origin of the UCS along X where its leader runs up or down, along Y where it runs aside:
(300, 40) with its leader ending at (310, 80) is an X datum of 300, the leader jogged two arrowheads before its end.
DIMCONTINUE, DIMBASELINE and QDIM's Ordinate go on from an ordinate dimension, each leader ending level with the last.
DIMJOGLINE (DJL) puts a jog in the line of a linear dimension, kept as AutoCAD keeps it (ACAD_DSTYLE_DIMJAG_POSITION) and
drawn 1.5 times the height of the words; Remove takes it out. DIM dimensions what is clicked: a line, then where its
dimension goes or a second line for the angle; a circle's diameter, an arc's radius; a click on nothing starts a linear
dimension. Its options run the commands of their names, and LAyer puts the dimensions made next on a layer.

A dimension made with DIMASSOC 2 (the default) whose points were picked with an object snap is associative, as in AutoCAD
2021: DIMLINEAR, DIMALIGNED and DIM tie each origin snapped with ENDpoint, MIDpoint, CENter, QUAdrant, NODe or NEArest to
its line, arc, circle, polyline segment or point (both ends where a line is selected), and one snapped with INTersection to
the two lines, arcs or circles that cross there; DIMRADIUS and DIMDIAMETER tie the center of the circle or arc, DIMANGULAR
of an arc its two ends and center, of two lines the four ends, of three points those snapped, DIMORDINATE its feature;
DIMCONTINUE and DIMBASELINE tie their first point to what the last dimension's second (or the base's first) was tied to,
and QDIM the ends of lines, the vertices of polylines and the centers of circles, all kept in a DIMASSOC as AutoCAD keeps
it. Every edit
that changes those objects, whatever command or grip makes it, carries the dimension with it in the same undo step: a line
from (0, 0) to (100, 0) moved by (10, 5) takes its dimension, 20 above, whole to (10, 5), (110, 5) and a line at 25; its end
stretched to (120, 30) moves the second origin there and measures 120, the dimension line staying 20 up; a circle of 25
scaled twice about its center gives its radius 50 drawn out the same way. An aligned dimension keeps its line as far from
the line measured, square to it: 20 above (0, 0)-(100, 0), the end stretched to (120, 30), its line point goes to
(115.149, 49.403). TRIM, EXTEND and LENGTHEN carry the end they move; an ordinate's leader end follows its point along the
axis measured alone, (100, -40) to (110, -40) for a point moved by (10, 5); an angle of two lines keeps its arc as far from
the vertex and at the same share of the angle. Erasing the object, or moving the dimension
without it, unties the dimension, which keeps its points. DIMREASSOCIATE asks each selected dimension for its points again,
`Specify first extension line origin or [Select object] <next>:`, and ties those snapped; DIMDISASSOCIATE (and
-DIMDISASSOCIATE) unties those selected, `1 disassociated.`. A copy of a dimension alone is tied to nothing; copied with
its objects, it is tied to their copies, as AutoCAD's COPY ties it. Each copy of a dimension (COPY, ARRAY, a grip's Copy)
gets a picture block of its own, and a dimension made here is written with flag 32 of group 70, so a plain MOVE takes its
picture with it. The window hands the
snap with its point through `CadCommandSession.Feed(CadSnapResult, bool)`; a point typed or fed with `Feed(GeoPoint2)` ties
nothing.

PEDIT (PE) asks AutoCAD 2021's prompts and stores what AutoCAD stores, checked against AutoCAD's own console. A line or an
arc picked is made a LWPOLYLINE after `Do you want to turn it into one? <Y>`, an arc's from its start counter-clockwise:
the arc through (100, 0), (150, 50) and (200, 0) is the vertex (200, 0) of bulge 1, then (100, 0). Fit and Spline make the
LWPOLYLINE the POLYLINE AutoCAD makes, its handle kept: Spline of (0, 0), (100, 0), (100, 50), (0, 80) at SPLINETYPE 6
and SPLINESEGS 8 is the first vertex of the frame (flag 16), the 9 of the curve (flag 8) from (0, 0) through (75, 28.75)
to (0, 80), then the rest of the frame. A POLYLINE left straight when it is edited alone is a LWPOLYLINE again, as
AutoCAD's PLINETYPE 2 makes it; Multiple leaves it a POLYLINE, as AutoCAD does. Edit vertex marks the vertex with a cross,
the first one the view shows (`No visible vertices` where it shows none); Next and Previous stop at the ends, Insert puts a
vertex after the marked one as wide as its segment ends and marks it, Break between two vertices of a closed polyline
leaves two open ones, and Straighten at one vertex straightens the segment that leaves it. Multiple joins within a fuzz
distance by Extend, Add or Both: the lines (0, 0) to (100, 0), (103, 0) to (103, 50) and (100, 50) to (0, 52) joined at 4
by Both are one polyline through (103, 0) and (103, 49.94), "2 segments added to polyline". A run is one undo step, and
Undo takes back its last option.

REVCLOUD draws a cloud round a rectangle (Rectangular, the type it starts with), a polygon (Polygonal), the cursor's path
(Freehand), or an object picked, which it replaces (Object: a line, arc, circle, ellipse, polyline or spline). The cloud is
a LWPOLYLINE of arcs of 110 degrees (bulge 0.52056705) bulging out, their vertices an arc length apart along the path, the
corners not kept; a last arc shorter than half an arc length is taken into the one before. A circle of radius 20 at an arc
length of 5 is the 25 vertices AutoCAD 2021 makes, to 8 places, and the outline 60 by 40 at 7 is 29, from (0, 0) to
(0, 4). The arc lengths are kept between runs divided by DIMSCALE and used times it, as AutoCAD keeps them; the maximum is
no more than three times the minimum. Calligraphy widens each arc from nought to a tenth of the longest arc, and Modify
redraws the side of a closed cloud picked along a new path. A metric drawing starts at 15.

TABLE (TB) shows the Insert Table dialog ([`CadInsertTableRequest`](xref:GeometryHelper.Dxf.Viewer.CadInsertTableRequest)),
asks `Specify insertion point:` (or two corners for Specify window) and opens the title for typing in the MTEXT editor:
Tab goes to the next cell, Shift+Tab to the one before, Enter to the one below and Esc ends. Five columns 63.5 wide and
four rows of data of AutoCAD's Standard make a table 317.5 wide and 56 high, its title 11 high and its other rows 9.
-TABLE asks the columns, the rows of data and the point (Style, Width, Height) on the command line, and TABLEDIT a cell
to type in; a double click or F2 on a cell runs it. TABLESTYLE (TS) shows the Table Style dialog
([`CadTableStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableStyleEditor)); a style changed draws its tables anew. A
click in a cell selects it with the Table Cell tab ([`CadTableEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableEditor)):
rows and columns put in and taken out, cells merged and unmerged, their alignment, fill and borders, the arrows moving
the cell and Delete emptying it, each one undo step that draws the table anew in its block. A cell written in a table of
100 rows of 20 columns is written and drawn in about 50 ms.

MLEADERSTYLE (MLS), and the last item of the multileader style box of the Annotate tab, show the Multileader Style
Manager ([`CadMLeaderStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadMLeaderStyleEditor)): Set Current, New…, Modify…
and Delete, which is refused for Standard, the current style and a style a multileader is drawn in. A drawing of no
multileader style is given Standard first, metric: arrows 2.5, a break of 3.75, a landing of 8, text 2.5 high and a
landing gap of 2. Modify writes the Leader Format, Leader Structure and Content tabs into the drawing's MLEADERSTYLE and
gives every multileader of the style its values, those it has of its own kept: a style's text made 2.5 high leaves a
multileader whose words were set 6 high at 6. MLEADER draws in the current style (CMLEADERSTYLE, else Standard): a
multileader of a style of words and arrows 4 high has words and arrows 4 high. Each change is one undo step. Block
content is not offered, as MLEADER places words only, and the annotative box is greyed.

QSELECT and FIND show the window's Quick Select and Find and Replace dialogs, whose logic is
[`CadQuickSelect`](xref:GeometryHelper.Dxf.Viewer.CadQuickSelect) and [`CadFind`](xref:GeometryHelper.Dxf.Viewer.CadFind).
Quick Select compares the rows of the Properties panel: of a plan of 312 lines on layer WALLS among others, Line, Layer,
`= Equals`, WALLS selects the 312 and says `312 item(s) selected.`; numbers are compared within the tolerance of a point,
so Length `= 200` takes a line 200.0004 long. FIND searches TEXT, MTEXT by its words, attribute values, dimension text,
multileader text and table cells, and replaces in MTEXT its words alone: `{\fArial|b1;Bolts M20}` with M20 replaced by
M24 is `{\fArial|b1;Bolts M24}`. Replace and Replace All are one undo step each. Over 100 000 objects Quick Select by
layer takes 0.25 s and FIND 0.04 s.

DRAWORDER and TEXTTOFRONT write the sort table AutoCAD writes, a SORTENTSTABLE under ACAD_SORTENTS in the extension
dictionary of the space's block ([`DxfDrawOrderTable`](xref:GeometryHelper.Dxf.DxfDrawOrderTable)), and the display draws
in its order: a line sent to the back with `DR`, Back is drawn under every other object. GROUP makes a GROUP of the
ACAD_GROUP dictionary, named with Name, unnamed (`*A1`) without; with PICKSTYLE 1, the default, a pick of one member
takes the whole of a selectable group, and Ctrl+Shift+A switches that off and on (`<Group off>`). -PURGE never deletes
what AutoCAD keeps: layer 0 and Defpoints, the linetypes ByLayer, ByBlock and Continuous, the styles Standard and the
current ones, the blocks of the spaces, the plot style Normal and every visual style; with "Verify each name to be
purged?" Yes it asks `Purge layer "EMPTY1"? [Yes/No] <No>:` of each. It purges pass after pass until nothing more is
unused, so that a block OUTER inserted nowhere, holding the only INSERT of INNER, takes INNER with it, and the layer only
INNER's circle was on; a name answered No is not asked again. PURGE shows the Purge dialog where there is a window (the
typed form otherwise): the named objects nothing uses by kind, with Confirm each item, Purge nested items, the geometry
of no length and the empty texts; and in Find Non-Purgeable Items the objects in use with the reasons, `Layer WALLS is the
current layer.`, `Used by 2 objects in model space and 0 in layouts.`, `Referenced by block GB.`. Each purge is one undo
step. CLASSICGROUP shows AutoCAD's Object Grouping dialog (GROUP being the command-line form in AutoCAD 2021): New, Add,
Remove, Find Name and Highlight ask on the command line and bring the dialog back; Rename, Description, Explode,
Selectable and Re-Order change the group at once; OK keeps every change as one undo step and Cancel takes them back.
With SELECTIONCYCLING 2, AutoCAD's default, a
click where two lines cross shows the list of both near the cursor, the one drawn on top first, `Line · 0` and
`Line · WALLS`, and the row chosen is taken as a click on that line alone; with 1 or 2, Shift+Space arms cycling, and each
click at the crossing then takes the other line in place of the one before. A plain click there takes the line on top, as
AutoCAD's does while SORTENTS holds its bit 1 (its default 127 does): of the objects within a tenth of the pickbox of the
nearest, the one last in the draw order wins, so a line sent to the back with DRAWORDER is no longer the one picked. Ctrl+W
switches cycling between 0 and 2, as in AutoCAD.

XATTACH and -XREF Attach or Overlay attach a DXF as an external reference: a block flagged 4 (12 overlaid) naming the
file, and an INSERT of it, one undo step. The presenter reads the file and draws its model space under the INSERT, its
layers named after the reference (`grid|GRID`) and its layer 0 taking the INSERT's; nothing of it is written into the
drawing. Its own attachments are followed, its overlays not. The file is looked for as AutoCAD looks: the path saved, from
the folder of the drawing that names it, then the file's name there, in the drawing's folder and in the support folders.
A grid of 1 000 by 1 attached at (100, 200) from `D:\Plans\xref\grid.dxf` into `D:\Plans\plans\host.dxf` is saved as
`..\xref\grid.dxf` (Relative path; Full path until the drawing is saved), draws its line from (100, 200) to (1 100, 200),
and picks as the INSERT. [`document.Xrefs`](xref:GeometryHelper.Dxf.Viewer.CadXrefManager) lists each reference and image
with its status: Loaded, Unloaded, Not Found, Unresolved, Unreferenced, or Needs reloading once its file changed on disk.
Unload and Reload change what is shown, not the drawing, and are not undone; no DXF says a reference is unloaded. One
not found shows its path at its insertion point. -XREF ? prints AutoCAD 2021's table, and Bind is refused. IMAGEATTACH
and -IMAGE Attach place a PNG, JPEG or BMP at AutoCAD's size: 40 by 30 pixels saved at 3 779 pixels a metre are
10.584811 by 7.938608 mm, one of no resolution one unit wide. The picture is drawn by its pixels, decoded once off the
window's thread, clipped by IMAGECLIP (Rectangular, Polygonal, Select polyline, Invert clip), its frame shown by
IMAGEFRAME 1 or 2, its transparent pixels showing what is under it with TRANSPARENCY ON; one not found is its frame with
its file's name. A drawing of 100 000 entities shown as a reference is read and drawn in 0.90 s, where opening it alone
takes 0.87 s (.NET Framework 4.8). AutoCAD 2021 reads no DXF as an external reference ("Unable to load"): it keeps the
block and the INSERTs of a drawing the presenter saved, AUDIT finds no error, and its -XREF ? lists them.

BATTMAN shows the Block Attribute Manager of the block of an INSERT selected, else of the first block with attributes:
its attribute definitions in the block's order, the modes as AutoCAD's letters (I invisible, C constant, V verify, P
preset, L lock position, M multiple lines), a tag given twice in red. Move Up, Move Down, Edit (tag, prompt, default,
modes, text options and properties) and Remove change the list; Apply and OK write it into the block, each one undo step,
and the INSERTs keep their attributes until Sync runs ATTSYNC's logic on every one of them, or at once with "Apply changes
to existing references" in its Settings. A block GB of GRID, LEVEL and REV with LEVEL moved up and synced gives each INSERT
LEVEL then GRID, their values kept. AutoCAD has no command-line form of BATTMAN, and with no window it says so. ATTDIA 1
makes INSERT and -INSERT ask the values of the attributes in the Edit Attributes dialog, eight a page in the block's
order, the constant ones left out and a value to be verified asked once; Cancel places the defaults.

BEDIT (BE) opens a block in the Block Editor, as AutoCAD 2021's: the Edit Block Definition dialog lists
`<Current Drawing>` (model space itself) and the named blocks, no layout, external reference or anonymous block, and a
name not in it makes a new, empty block; -BEDIT asks `Enter block name or [?]:`. The block is shown alone in a space of
its own ([`CadDocument.BlockEditor`](xref:GeometryHelper.Dxf.Viewer.CadBlockEditor), `CadSpace.IsBlockEditor`), where
every command draws into and modifies the block's own entities, each an undo step; undo stops where the block was opened
or last saved. BSAVE (BS) redefines the block and draws every INSERT anew (their attributes are not synchronised, as
ATTSYNC is not run), merging the session so far into one undo step, `BEDIT`; BSAVEAS copies the content to a new block
and goes on with it, the one opened left as it was. BCLOSE (BC) asks `Save changes to DOOR? [Save/Discard/Cancel]
<Save>:` of a block with changes not saved; Discard reads the drawing back as it stood when the block was opened or last
saved, so a new block never saved is not made. A door of one line given a second and saved shows both in every INSERT,
and one undo after BCLOSE takes the session back. Commands that leave the block or work on another space (BEDIT, XREF,
LAYOUT, PLOT, PREVIEW, PAGESETUP, MSPACE, PSPACE, MVIEW, QSAVE, SAVEAS) say `Not allowed in the Block Editor.`.
PROPERTIES, OPTIONS, LAYER and QSAVE ask the window for its panel or dialog (`UiRequested`), and say "is shown by the window"
where none is listening. The window adds CLOSE, NEW, SAVEAS, PROPERTIESCLOSE, GRID, SNAP, ORTHO, POLAR, OSNAP, LAYMCUR and LAYMCH, and the plugin commands. The inquiry commands print in AutoCAD's words: DIST from (0, 0) to (100, 100) says `Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0`; AREA by points or by an object says `Area = 5000.0000, Perimeter = 300.0000`. Each command is a
[`CadCommand`](xref:GeometryHelper.Dxf.Viewer.CadCommand): a name and an iterator that yields each
[`CadPrompt`](xref:GeometryHelper.Dxf.Viewer.CadPrompt) and reads the answer from [`CadCommandContext`](xref:GeometryHelper.Dxf.Viewer.CadCommandContext)`.Input`, so it reads top
to bottom as its prompts do, and a [command plugin](plugins.md) is written the same way.

### Aliases and keys

[`CadShortcuts`](xref:GeometryHelper.Dxf.Viewer.CadShortcuts) holds the aliases and the keys as data. The defaults are
AutoCAD's: 119 aliases (`M` MOVE, `CO` COPY, `E` ERASE, `L` LINE, `PL` PLINE, `C` CIRCLE, `Z` ZOOM, `LA` LAYER, `DLI`
DIMLINEAR, `MV` MVIEW, `MS` MSPACE, `PS` PSPACE, `BE` BEDIT, `BS` BSAVE, `BC` BCLOSE, and the rest) and 21 keys (F3 OSNAP, F7 GRID, F8 ORTHO, F9 SNAP, F10 POLAR, F11 OTRACK, F12 DYN, Delete ERASE,
Ctrl+Z U, Ctrl+Y REDO, Ctrl+S QSAVE, Ctrl+1 PROPERTIES, Ctrl+A SELECTALL, Ctrl+C COPYCLIP, Ctrl+X CUTCLIP, Ctrl+V PASTECLIP, Ctrl+Shift+C COPYBASE, Ctrl+Shift+V PASTEBLOCK, Ctrl+P PLOT, Ctrl+Shift+A PICKSTYLE, Ctrl+W SELECTIONCYCLING). A registry's `Shortcuts` is replaced, from
[global settings](app.md#global-settings), by another set; letters of either case are the same, and a name wins over an
alias of the same letters.

### System variables

SETVAR asks as AutoCAD 2021 does, `Enter variable name or [?] <FILLETRAD>:` offering the variable named last, then `Enter
new value for FILLETRAD <10.0000>:`; a variable's name typed alone, or an alias of it (`LTS`), asks the same where no
command has the name. `?` lists those a pattern of wildcards matches, `CHAMFER?` giving CHAMFERA and CHAMFERB with their
values. Enter keeps a value; a whole number out of range is refused in AutoCAD's words and asked again, `SORTENTS` 200
saying `Requires an integer between 0 and 127.`, and a real nought where it must be above nought says `Value must be
positive and nonzero.`; a variable read only says its value, `EXTMIN = 0.0000,0.0000,0.0000 (read only)`. Each one is read
and set where the presenter keeps it, listed in [`CadCommandRegistry.Variables`](xref:GeometryHelper.Dxf.Viewer.CadCommandRegistry):

| Kept in | Variables | A change |
|---|---|---|
| the header of the drawing, written as AutoCAD writes it | CLAYER, CECOLOR, CELTYPE, CELWEIGHT, CELTSCALE, LTSCALE, PSLTSCALE, MSLTSCALE, DIMSCALE, DIMASSOC, TEXTSIZE, TEXTSTYLE, FILLETRAD, CHAMFERA, CHAMFERB, PDMODE, PDSIZE, MIRRTEXT, SORTENTS, IMAGEFRAME, CANNOSCALE, INSBASE, LIMMIN, LIMMAX | one undo step |
| the document or the session | PICKSTYLE, SELECTIONCYCLING, PICKFIRST, PICKADD, ATTREQ, ATTDIA, OFFSETDIST, TILEMODE (the space shown) | at once, no undo step |
| the window: the status bar, the snaps and Global settings | OSMODE, ORTHOMODE, SNAPMODE, GRIDMODE, POLARANG, AUTOSNAP, DYNMODE, PICKBOX, APERTURE, LWDISPLAY | at once, no undo step; kept in the settings file |
| read only | DIMSTYLE, EXTMIN, EXTMAX, UCSNAME, UCSORG, DWGNAME, DWGPREFIX, DATE, CDATE | |

FILLETRAD 5 writes `$FILLETRAD` 40 `5` into the header and U takes it back to 10. OSMODE 4133 ticks Endpoint, Center,
Intersection and Extension, and 16391 ticks Endpoint, Midpoint and Center with OSNAP off. With no window, ORTHOMODE and
LWDISPLAY are the drawing's `$ORTHOMODE` and `$LWDISPLAY`; the window answers them with its ORTHO and its lineweights,
and writes ORTHO switched into the drawing's `$ORTHOMODE` too, as AutoCAD saves it with the drawing.
PICKFIRST 0 makes a command that works on objects ask `Select objects:` whatever is selected before it: with a line
selected, MOVE asks for objects rather than its base point. PICKADD 0 makes what is picked last the selection, Shift
adding to it, at `Select objects:` and on the canvas: a click on line A, then on line B, leaves B alone; 1, the default,
and 2 add, Shift taking out. ATTREQ 0 makes INSERT give each attribute its default without asking; ATTDIA 1, 0 by
default, makes it ask them in the Edit Attributes dialog where there is a window. DYNMODE keeps -3 to
3, negative while DYN is off, so that F12 brings the same value back; the one box of dynamic input shows both pointer and
dimensional input whichever of 1 to 3 is set. The window keeps PICKBOX, APERTURE, LWDISPLAY and POLARANG in
the settings file as it keeps DYN and the snaps, POLARANG 30 written `"polarAngle": 30`. POLARMODE, FILEDIA and
CMDDIA are not variables of the presenter yet.
CECOLOR, CELTYPE and LTSCALE typed alone ask their own prompts, as before (`Enter new object color [Truecolor/COlorbook]
<BYLAYER>:`); SETVAR asks them in its words, a text in quotes, `<"ByLayer">`. A window adds its own with
`registry.AddVariable(CadSystemVariable.Integer(...))` or `CadSystemVariable.Real(...)`.

## User coordinate systems

UCS sets the system points are typed in: give an origin, then a point on the X axis, or type Z and an angle to turn it. A point typed as `10,20` is then measured from that origin along its axes, and `*10,20` is the world point. The grid, ortho and polar tracking run along its axes, the crosshair and the icon turn with it, and the status bar and dynamic input show its coordinates. In a UCS turned a quarter turn, `@100<0` draws a line 100 long up the world's Y. Objects are still saved in world coordinates. UCS NAmed Save keeps a system under a name in the drawing's UCS table, and Restore makes it current again; UCS Previous goes back through the last ten, and a UCS change is not undone by U. UCSICON turns the icon on or off and puts it at the origin or at the lower left, PLAN zooms to extents, and UCSMAN lists the named systems on the command line. A UCS read from a file that is not in the plane of the drawing is listed but not made current: the presenter shows the drawing from above. The 3D options (X, Y, Face) say they are not available in a 2D view.

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
| `Modes` | Endpoint, Midpoint, Center, Quadrant, Intersection | a [`CadSnapModes`](xref:GeometryHelper.Dxf.Viewer.CadSnapModes) set with AutoCAD's OSMODE values (Endpoint 1 to Parallel 8192); Node, Insertion, Perpendicular, Tangent, Nearest, GeometricCenter, ApparentIntersection, Extension and Parallel can be added |
| `IsOn` | true | F3 |
| `Tracking` | off | F11, object snap tracking |
| `Ortho`, `Polar`, `PolarAngle` | off, off, a quarter turn | F8, F10 |
| `GridSnap`, `GridX`, `GridY`, `GridOrigin` | off, 10, 10, the origin | F9 |

AutoCAD's order is kept. A snap to a point near the cursor (endpoint, midpoint, centre, quadrant, intersection, apparent
intersection, insertion, the crossing of two tracking lines) wins, the nearest of them first; then a snap the object under
the cursor gives wherever its point lies (perpendicular, tangent, the centre of a circle whose rim is under the cursor, the
insertion point of a block or a text under it); then a point on an extension, a parallel or a tracking line; then nearest.
Where none is found the cursor is put on the grid, then held level or upright from the base point (ortho) or on a polar
angle. The result is a [`CadSnapResult`](xref:GeometryHelper.Dxf.Viewer.CadSnapResult): the point, which
[`CadSnapKind`](xref:GeometryHelper.Dxf.Viewer.CadSnapKind) gave it, the handle of the object, and the dotted `Tracks` drawn
to it.

Extension, Parallel and tracking work from points acquired by resting the cursor, as AutoCAD's do: `Acquire` is called
when the cursor pauses (the window waits half a second), and lets go a point rested on again; seven are kept at most, and
`ClearAcquired` lets them all go once a point is taken. A line from (0, 0) to (100, 0), its end rested on with Extension
on, gives (150, 0) to a cursor at (150, 0.4); with tracking on instead, it gives (100, 30) to a cursor at (100.4, 30), and
with a second end acquired at (200, 50), the crossing (100, 50) of the two tracking lines. Parallel acquires the line
under the cursor and holds the rubber band along the line through the base point parallel to it. Apparent Intersection
gives where two objects under the cursor would cross run on, lines as endless lines and arcs as whole circles: two lines
(0, 0) to (98, 0) and (100, 2) to (100, 50) give (100, 0). With one object under the cursor it offers it as the first of
an Extended Apparent Intersection; a click picks it (`TakeFirstObject`) and takes no point, and the object under the
cursor after gives where the two would cross. Insertion gives the insertion point of an INSERT from anywhere on its lines,
and the point a TEXT, an MTEXT or an attribute is placed by.

An XLINE and a RAY snap along their whole length, as AutoCAD's do: Nearest, Perpendicular and Intersection (with lines,
arcs, circles and other xlines and rays) however far from their base points, and none behind the base point of a ray. The
base point of a RAY is its endpoint, and the root point of an XLINE its midpoint, as AutoCAD gives MID for an xline.
Extension runs on back from the base point of a ray, Parallel follows either, and Apparent Intersection runs both on as
lines. An xline along (1, 1) through the origin and the line from (100, 0) to (100, 500) give the intersection (100, 100);
a ray along +X from (100, -500) gives the perpendicular from (500, 80) at (500, -500), and none from (0, 80).

### Point modifiers

Any prompt for a point takes AutoCAD's point modifiers, typed or chosen from the top of the Shift+right-click menu
(Temporary track point, From, Mid Between 2 Points, Point Filters), through
[`CadCommandSession`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession)`.StartPointModifier`:

| Typed | Asks | Gives |
|---|---|---|
| FROM | `Base point:`, then `<Offset>:` | the offset point: `@dx,dy` and `@distance<angle` are measured from the base point |
| M2P, MTP | `First point of mid:`, `Second point of mid:` | the point midway |
| TT | `Specify temporary OTRACK point:` | no point: the prompt is asked again, and while OTRACK is on tracking lines run from the point |
| .X .Y .Z .XY .XZ .YZ | `of`, then `(need YZ):` and so on | the coordinates named from the first point, the rest from the second |

LINE with FROM, the base point 100,50 and the offset `@30,20` starts at (130, 70); M2P between (0, 0) and (100, 40) gives
(50, 20), and M2P at the radius of a circle centred at (0, 0) between (0, 0) and (60, 80) gives a radius of 50. `.X` of
(30, 999) with (888, 40) gives (30, 40). At `(need YZ):` the coordinates can be typed alone, in the order X, Y, Z:
`40,3` gives Y 40. Z, which a plan has none of, is asked as AutoCAD asks it and left out. Coordinates are those of the
UCS. Each point a modifier asks may be snapped (END typed, or the menu) and may be given by another modifier, as FROM
with M2P for its base point. Enter at a modifier's prompt is not a point and is asked again; Esc cancels the command.

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
| linear and aligned DIMENSION | the two extension line origins, the two ends of the dimension line, the words | an origin measures anew, the dimension line kept where it is; an end of the line moves the line through the point; the words move as DIMTEDIT moves them |
| radius and diameter DIMENSION | the chord point (a diameter's two), the words | a chord point goes round the circle towards the point |
| angular DIMENSION | the vertex and the two line points (an angle between two lines: the ends of both lines), the arc, the words | a point measures anew; the arc runs through the point |
| ordinate DIMENSION | the feature, the leader's end | moves that point |
| anything else | none | |

The plate's line has `End 0 (0, 0)`, `End 1 (100, 0)` and `Middle 2 (50, 0)`; dragging grip 1 to (120, 0) leaves its end at
(120, 0, 0). A dimension's grips are AutoCAD 2021's, and dragging one rewrites its points of definition and draws its
picture anew, as DIMEDIT does. A dimension of 100 from (0, 0) to (100, 0), its line 30 below, dragged by its second origin
to (150, 10) measures 150 with its line still 30 below; dragged by an end of its line to (20, -60), its line runs 60 below,
and words placed by DIMTEDIT go down with it. Its words dragged to (60, -50) stand there, the line moved to run under them
where DIMTMOVE is 0 and a leader drawn to them where it is 1. A drag that would measure nothing, an origin onto the other,
is refused.

## Model space through a viewport

In a layout, model space is worked in through a viewport as in AutoCAD: MSPACE (`MS`), a double click inside a viewport's
frame or the status bar's PAPER button enters it, and PSPACE (`PS`), a double click off every frame or the MODEL button
goes back to the paper. [`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument)`.EnterViewport(handle)` and
`LeaveViewport()` do the same in code, and `ActiveViewport` is the viewport worked through, null on the paper. While one
is, `WorkingSpace` is model space: the commands draw there, picks and snaps work in model units as the viewport shows them,
and the canvas draws the viewport's frame thick, the crosshair inside it alone. A line drawn through a viewport of
Layout1 goes into model space. Ctrl+R goes on to the next viewport (`NextViewport`).

The wheel, the middle button and ZOOM and PAN change the viewport's view, not the paper, one undo step each (the wheel's
notches one step once the view is still). ZOOM `nXP` sets its scale, n paper units to the model unit: `1/50XP` in a
viewport 100 high shows 5 000 of model space, 1:50. A viewport whose display is locked keeps its view, and the paper is
zoomed instead. The status bar's scale list and Properties' Standard scale (1:1 to 100:1) and Custom scale (0.02) set the
view's height to the frame's over the scale, about the same centre; Display locked is a row of Properties too. The list
opens on a click of the scale, as AutoCAD 2021's: Scale to fit, which fits model space's extents to the frame, the standard
scales, and Custom..., which runs VPSCALE: it writes `PS:MS == 1:20` and asks a new scale, as 1:37.5 (SCALELISTEDIT is not
built). Beside it the padlock locks and unlocks the viewport's display, one undo step; a locked viewport's scale is grey and
its list greyed. On the paper with no viewport selected neither shows; on the Model tab the place shows the annotation
scale (CANNOSCALE), its list the drawing's scales, and MODEL is a label rather than a button.
[`CadViewports`](xref:GeometryHelper.Dxf.Viewer.CadViewports) has the arithmetic: the canvas's view in model units
(`ModelView`) and the view a viewport takes back from it (`ViewOf`), a box fitted to the frame (`Fit`), and the scales'
names. A viewport whose view is turned, or not a plan, is not entered.

### Grip modes

A click on a grip makes it hot and starts AutoCAD 2021's grip modes
([`session.StartGripEdit(hotGrips)`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession)): the command line says
`** STRETCH **` and asks `Specify stretch point or [Base point/Copy/Undo/eXit]:`, the hot grip the base point. Enter or Space
goes on to `** MOVE **` (`Specify move point`), `** ROTATE **` (`Specify rotation angle`, with Reference), `** SCALE **`
(`Specify scale factor`, with Reference) and `** MIRROR **` (`Specify second point`), and back to STRETCH; STRETCH, MOVE,
ROTATE, SCALE and MIRROR typed, or ST, MO, RO, SC and MI, go to that mode. STRETCH moves the hot grips; the other modes
take the objects selected, drawn as a ghost as MOVE draws them. Base point gives another base, Copy leaves the originals
and asks again until eXit, Enter or Esc (`** MOVE (multiple) **`), Undo takes the last copy back, and a first point picked
with Ctrl held turns Copy on. Shift+click makes several grips hot, and they stretch together: a line's two ends move it
whole, and a circle's centre and a quadrant move it keeping its radius. A grip pressed and let go elsewhere is stretched
there, as before. The whole grip edit is one undo step, named GRIP, and Enter after it runs the command run last. Of two
lines from (0, 0), the grip at (0, 0) and MO to (10, 20) moves both; RO 90 then turns them a quarter turn about (10, 20).

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

The other kinds have AutoCAD 2021's rows too, each set the same way:

| Kind | Rows that can be set | Read only |
|---|---|---|
| Polyline | Current vertex, Vertex X and Y, Start and End segment width, Global width, Elevation, Closed, Linetype generation | Vertices, Length, Area |
| Ellipse | Center X, Y and Z, Major radius, Minor radius, Radius ratio, Start and End angle | Area |
| Hatch | Type, Pattern name, Angle, Scale, Spacing, Double, Origin X and Y, Elevation, Island detection, Background color | Associative, Area |
| Dimension | Dim style; the Lines & Arrows, Text, Fit and Primary Units rows of the Dimension Style dialog's variables; Text override | Measurement |
| MTEXT | Contents, Style, Justify, Direction, Text height, Rotation, Line space factor, distance and style, Defined width, Columns, Position | |
| Block reference | Position, Scale X, Y and Z, Rotation, each attribute by its tag | Name, the Dynamic Block rows |
| Viewport | Center, Height, Width, On, Display locked, Standard scale, Custom scale | Clipped, Visual style |
| Multileader | Multileader style, Leader type, Arrowhead, Horizontal landing, Landing distance, Contents, Text style, Text height | |

Current vertex steps through a polyline's vertices and is no step of undo
([`CadSelection.CurrentVertex`](xref:GeometryHelper.Dxf.Viewer.CadSelection.CurrentVertex)): on the rectangle (0, 0)
(30, 0) (30, 20) (0, 20), vertex 3 shows Vertex X 30.0000 and Vertex Y 20.0000, and Vertex Y typed as 25 moves that corner
alone. A row of a dimension is written as an override of its variable, the style left as it is, and the dimension drawn
again: Precision 0.000 with Suppress trailing zeros No and Dim suffix " TYP" turns a dimension of 300 under ISO-25 into
"300.000 TYP". Arrow 1 makes the arrows separate (DIMSAH), the other end keeping the arrow it showed. A Major radius of 40
scales an ellipse of radii 20 and 10 whole, to 40 and 20; a Minor radius above the major is refused. A Height or Width of a
viewport keeps its scale, the view growing with it. A row of a list ([`CadPropertyRow.Choices`](xref:GeometryHelper.Dxf.Viewer.CadPropertyRow.Choices))
is a drop-down in the window, and the Contents row of one MTEXT offers its MTEXT editor.

## MATCHPROP

MATCHPROP (MA) asks for the source object, says the settings it copies ("Current active settings: Color Layer Ltype
Ltscale Lineweight Transparency Thickness Dim Text Hatch Polyline Viewport Table Multileader") and asks "Select
destination object(s) or [Settings]:". Settings shows the Property Settings dialog, whose check boxes are
[`CadCommandSession.MatchProperties`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession.MatchProperties), every one until
changed: with Layer alone checked, a red line matched to a line on Steel goes to Steel and stays red. The special
properties are copied between objects of one kind: a dimension's style and its overrides (drawn again), a text's or an
MTEXT's text style and height (and a text's oblique angle and width factor onto a text), a hatch's pattern, angle and scale
or spacing and double, a polyline's linetype generation and its width where the source has one width all along, a
viewport's scale, display lock and frozen layers, a table's table style and a multileader's style. Plot style, Material,
Shadow display and Center object are greyed.

## Layers

CECOLOR, or the colour box of the Properties panel, sets the colour new objects take: ByLayer, ByBlock, a name or an index 1 to 255. A line drawn after CECOLOR red is red; a true colour is refused, because the header holds an index.

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

### Layers in the viewports of a layout

While a layout is current the Layer manager shows AutoCAD 2021's VP columns: VP Freeze, New VP Freeze, VP Color, VP
Linetype, VP Lineweight, VP Transparency and VP Plot Style. They act on the viewport worked through, else on the one
viewport selected on the paper, and are grey with neither; on the Model tab they are hidden. A value that differs from the
layer's own in that viewport alone is marked blue. VP Plot Style reads `Color_7` and is grey in a drawing plotted by colour
(a CTB table), as AutoCAD's. Its right-click menu has "VP Freeze Layer in All Viewports", "VP Thaw Layer in All Viewports",
"New Layer VP Frozen in All Viewports" and "Remove Viewport Overrides for", on the layers chosen or all, in the current
viewport or all. [`CadViewportLayerEditor`](xref:GeometryHelper.Dxf.Viewer.CadViewportLayerEditor) makes these edits, each
one undo step:

| Call | Does |
|---|---|
| `TargetViewport(document)` | the viewport the columns act on: the one worked through, else the one selected alone |
| `SetFrozen(document, viewports, layers, frozen)`, `SetFrozenInAllViewports` | the layers frozen or thawed in viewports, the viewport's own list (group 331) |
| `SetFrozenInNewViewports(document, layer, frozen)` | the layer frozen in each viewport made from now on (flag 2 of its group 70); MVIEW freezes it in the new one |
| `AddFrozenInAllViewports(document, name)` | a new layer frozen in every viewport of every layout |
| `SetColor`, `SetLinetype`, `SetLineWeight`, `SetTransparency`, `SetPlotStyle` | an override of the layer in one viewport |
| `RemoveOverrides(document, layers, viewport)` | the overrides taken out, in one viewport or, given null, in all |

The display draws a viewport without the layers frozen in it and with its overrides: the layer TEXT, cyan, overridden red
in the plan viewport is drawn red there and cyan in the detail viewport. Transparency is not drawn, the layer's own nor an
override.

VPLAYER asks `Enter an option [?/Freeze/Thaw/Reset/Newfrz/Vpvisdflt]:` until Enter, all one undo step. Freeze, Thaw and
Reset take layer names, with commas and wild cards (`D*,TEXT`), or on Enter the layers of objects selected, then the
viewports: `[All/Select/Current/Except current] <Current>:`, All being those of the layout shown; on the Model tab every
viewport of every layout. Reset sets each layer as its New VP Freeze says; Newfrz adds layers frozen in all viewports;
Vpvisdflt sets New VP Freeze; `?` lists the layers frozen in a viewport. LAYVPI, through a viewport, freezes the layer of
each object picked in every other viewport, of all layouts or of the current one (Settings), and Undo thaws the last again.

## See also

- [Opening, drawing and picking](display.md): the document, the display lists and the index the snaps and grips use.
- [Owners](owners.md): why an edit is sometimes refused with "belongs to the model".
- [Command plugins](plugins.md): a command written outside the presenter.
- [Entities and curves](../dxf/entities.md) and [Editing rules](../dxf/rules.md): what each entity's `TransformBy` accepts.
