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

On a large drawing most of an undo was reading those bytes back and meeting every entity of the drawing read for the first
time, when the display is patched. So after every step, undo and redo, a worker thread reads ahead the drawings the next
undo and the next redo go to, from the same bytes, and makes their index of handles; the undo then takes the drawing ready
and draws again only the entities its step touched. On a drawing of 1 000 000 entities the undo of a one-line MOVE and its
display take about 0.4 s where they took about 2.2 s, once the drawing has been read ahead (about 2 s after the step). An
undo asked sooner waits for a reading already begun, or reads the bytes itself as before, and takes about 1.5 s where it
took 2.6 s. The drawing it gives is the one read from the bytes before, byte for byte. Up to three such drawings are held
besides the one shown, each about 520 MB at that size once read and indexed.

The drawing an undo leaves is the one a redo goes back to, and the drawing a redo leaves the one the next undo goes back to.
Where it was itself read from the bytes of its step and nothing has written into it since, it is not read again: the worker
saves it, sets it against the bytes, and takes it as it is where the two are the same byte for byte. So undo after undo
reads one drawing ahead, not two: at 1 000 000 entities a run of undos and redos allocates about 35 GB where it allocated
47, and a redo 3 s after an undo takes about 0.3 s under .NET Framework 4.8 where its drawing was not yet read and it took
1.6 to 1.9 s. The drawings held when nothing is being read are as before, one each side of the one shown. An edit stops
the readings still running and they read again after it: an ERASE of 10 000 objects of a drawing of 100 000 straight
after an undo takes about 185 ms under .NET 10 where it took 220 (110 once the readings are done). The Block Editor's Save
Block and its changes discarded on closing read ahead the drawing the next undo goes to, as any step does; the first undo
after them, with finding an entity, takes about 1 ms where it read the drawing then and took 300.

After an undo or a redo `Database` is a [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) read from those bytes: a new
one, or the one an undo or a redo left where it goes back to it unchanged. The circle that `FindEntity(handle)` gave before
the undo is not the object it gives after, so keep handles, not entities, across a step. The
[`CadSpace`](xref:GeometryHelper.Dxf.Viewer.CadSpace) objects stay and are pointed at the new drawing.

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
| answers a point | as `x,y`, `@dx,dy`, `@distance<angle` or `distance<angle`; a number alone at a point prompt is a distance along the rubber band, towards the cursor, and at a point SETVAR asks, which has no band, a distance from the last point towards the cursor, as AutoCAD 2021 takes GRIDUNIT 5 |
| answers a distance or an angle with no base point by two points | a point typed or picked asks `Specify second point:`, its rubber band from the first, and the distance between them, or the direction from the first to the second, is the answer, as AutoCAD 2021 takes them: POLARDIST 0,0 then 3,4 is 5, SNAPANG 0,0 then 1,1 is 45. `@dx,dy` at the second is measured from the first, the last point is left as it was, the prompt's keywords and point modifiers are taken there, Enter is the prompt's Enter, and text gets the prompt's refusal ("Requires numeric distance or two points.", "Requires valid numeric angle or two points.") and is asked again |
| answers with a keyword | by its capital letters, as `U` for `Undo` in `[Close/Undo]` |
| selects first, then starts a command | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR and EXPLODE take the selection made before them, and ask "Select objects:" where there is none |
| answers "Select objects:" | by a click, a window, a crossing, a lasso, or one of AutoCAD 2021's options typed (below); Shift held takes out what a click, a box or a lasso picks |
| presses Esc | ends the command; what a LINE drew stays, as in AutoCAD, and what a MOVE had not yet done is not done |

A command that fails part way has its edits taken back. Its outcome is `session.LastOutcome`: `Completed`, `Cancelled` or
`Failed`. A selection of entities on a locked layer is taken without them, and the line says "1 was on a locked layer."

A prompt can carry a preview, `session.Prompt.Preview`, which a window draws under the cursor as AutoCAD 2021 drags the
object about to be made. `Items(cursor)` gives the display items of what a point picked there would make, made by the
command's own code; `Moves(cursor)` gives the transformations the copies of `Handles` are drawn under, as ROTATE by a
Reference or an array's copies; `ShowsBand` says whether the rubber band is drawn with it. RECTANG asking its other
corner, first corner (0, 0), previews at (30, 20) one closed polyline through (0, 0), (30, 0), (30, 20) and (0, 20), the
rectangle a pick there adds.

At a prompt for one object, `Items(cursor)` is what picking the object under the cursor would give, as FILLET's second
object gives both objects trimmed and the arc. The objects a command changes are previewed as they would be, copies of
them made by the command's own code; nothing is written to the drawing or to the history while a preview is made. Where
`Fades` is true the items are pieces the pick would take out rather than objects it makes, as TRIM and
BREAK give them: a line from (-50, 0) to (100, 0) crossed at the origin, hovered at (-25, 0), previews the 50 from
(-50, 0) to (0, 0), drawn faded. A window sets `Shift` while Shift is held, and the answer previewed carries it: TRIM then
previews what EXTEND would do to the object, not faded, and EXTEND the piece TRIM would take out. At a prompt for a line
of text a window sets `Typed` to the text typed so far, and `Items` is that text as the command would make it, wherever
the cursor is: TEXT at "Enter text" previews its line. A prompt for a keyword can carry a preview too, of what Enter
makes, as -ARRAY's "Rotate arrayed objects?" does.

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
x = 40, is attached top right at (80, 0). Where the drawing's fonts read the letters, a TEXT is mirrored by the ink of its
words, as AutoCAD mirrors it: "ABC" of txt.shx 2.5 high at the origin, inked to 6.667, mirrored across x = 100 starts at
193.333; one on the right of its baseline by the box from where its words start to its point, and one in the middle about
its point. Where they are not read, the box of the words is the display's estimate. Turned over across its baseline, a
text is turned about the middle of the ink of its words: "Q1" of arial.ttf 1.5 high, whose Q reaches below the baseline,
about a line 0.705 above it rather than 0.75. At 1 a TEXT, an attribute definition and the attributes of an INSERT are
mirrored in the drawing's plane: their normal stays (0, 0, 1) and they are written backwards (flag 2 of group 71), "ABC"
at the origin mirrored across x = 100 standing at (200, 0) and across y = 100 at (0, 200) turned 180 degrees, never upside
down. The attributes of an INSERT already mirrored, whose normal is (0, 0, -1), keep it and are written backwards too.

An INSERT is written as AutoCAD 2021's MIRROR writes it, under either MIRRTEXT: its x factor turned the other way, its y
and z as they were, and turned to its mirrored y axis less a quarter turn. An INSERT at (20, 10) mirrored across y = 100
is written at (20, 190) with an x factor of -1 turned 180 degrees; across x = 100, -1 turned nought; across y = x, -1
turned 270; one scaled 2 by 3 is written -2 by 3, and one already written -1 by 1 comes back 1 by 1. A MINSERT is written
so too, its columns spaced the other way and its rows as they were: 3 columns 15 apart and 2 rows 8 apart mirrored across
y = 100 are written -1 turned 180 degrees, 44 of -15 and 45 of 8. An annotative INSERT is written so in its record and in
each of its contexts, each from its own factors and angle: its context of 1:2, scaled by 2, mirrored across y = 100 is
written -2 by 2 turned 180. A dynamic block's INSERT keeps the values its parameters are set to, under MIRROR as under COPY:
a door flipped (Hand 1) is copied flipped, the same anonymous block placed, written -1 turned 180 across y = 100.

At MIRRTEXT 1 an MTEXT along x mirrored across a level line is of normal (0, 0, -1) and is written with no direction
(group 11), as AutoCAD writes it: a record of no direction reads along the world's x axis put into its plane, here
(1, 0, 0) turned 180 degrees. A multileader's sizes, the height of its words, its arrows and its landing gap, are kept to
the bit by a mirror: 4 stays 4 across a line at 45 degrees, not 4.000000000000001.

A multileader keeps the drawing's plane under either MIRRTEXT, as AutoCAD's MIRROR keeps it. At 0 its words keep reading
along x and stand as far from the mirrored landing as they stood from the landing, held by their other side where the
mirror crosses the plane's x axis: "ABC" held top left at (30.45, 20.09), landed at (30, 20), mirrored across x = 100 is
held top right at (169.55, 20.09), its base point at the left of the words less the landing gap. A mirror line at 45
degrees turns the plane's x axis square to itself, not back, and the words are held by the same side. At 1 the words are
mirrored with the rest and say they run against the plane's x axis. A multileader of a block is based where a leader from
the left meets the block, the box from 150 to 180 at (150, 25).

### The commands

[`CadCommandRegistry.CreateDefault()`](xref:GeometryHelper.Dxf.Viewer.CadCommandRegistry) makes the registry of 244 commands, named as AutoCAD names them, and of the system variables SETVAR sets:

| Group | Commands |
|---|---|
| view | ZOOM (Window, Extents, All, Previous, and a factor as `2` or `0.5x`; `nXP` through a viewport), PAN, -PAN, REGEN, PROPERTIES, OPTIONS, QSAVE, MSPACE, PSPACE, VPSCALE, VPCLIP (Polygonal, Delete), SCALELISTEDIT, -SCALELISTEDIT (?, Add, Delete, Reset, Exit), VIEW, -VIEW (?, Delete, Orthographic, Restore, Save, sEttings, Window), REDRAW, REDRAWALL, REGENALL, ISOLATEOBJECTS, HIDEOBJECTS, UNISOLATEOBJECTS, VPORTS, -VPORTS (Save, Restore, Delete, Join, SIngle, ?, 2, 3, 4, Toggle) |
| edit | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO |
| draw | LINE, PLINE (Arc, Close, Halfwidth, Length, Undo, Width; arcs by Angle, CEnter, Direction, Radius, Second pt), CIRCLE (3P, 2P, Ttr, and Tan, Tan, Tan as 3P with `_tan` before each pick), ARC (three points; Start, Center, End, Angle or chord Length; Center first; Start, End, Angle, Direction or Radius; Enter to go on from the last line or arc; Ctrl held to draw the other way), RECTANG, ELLIPSE (Arc), POINT, XLINE, RAY, POLYGON, DONUT, TEXT (Justify, Style; Enter goes on below the last text), MTEXT and -MTEXT (Height, Justify, Line spacing, Rotation, Style, Width, Columns), REVCLOUD (Arc length, Object, Rectangular, Polygonal, Freehand, Style, Modify), MLEADER, MLEADERSTYLE, LEADER (Annotation: Tolerance, Copy, Block, None, Mtext; Format: Spline, STraight, Arrow, None; Undo), QLEADER, TOLERANCE, MVIEW, LAYOUT and -LAYOUT (Copy, Delete, New, Template, Rename, SAveas, Set, ?), DIVIDE (Block), MEASURE (Block) |
| shapes and hatching | SOLID, REGION, HATCH, -HATCH (Properties, Select objects, draW boundary, remove Boundaries, Advanced: Boundary set, Retain boundary, Island detection, Style, Associativity, Gap tolerance, separate Hatches; DRaw order, Origin, ANnotative, hatch COlor, LAyer, Transparency, Undo), GRADIENT, HATCHEDIT, HATCHTOBACK, BOUNDARY, -BOUNDARY (Advanced options), SPLINEDIT (Fit data: Add, Close, Delete, Kink, Move, Purge, Tangents, toLerance; Close, Open, Join, Move vertex, Refine: Add, Delete, Elevate order, Move, Weight; rEverse, convert to Polyline, Undo), CLASSICINSERT |
| multilines | MLINE (Justification, Scale, STyle; Close, Undo), MLEDIT (CC, OC, MC, CT, OT, MT, CJ, AV, DV, CS, CA, WA; Undo), MLSTYLE (?, Current, New, Modify, Rename, Delete, Load, Save), and the variables CMLJUST, CMLSCALE and CMLSTYLE (see [Multilines](#multilines)) |
| inquiry | DIST, AREA (Add area, Subtract area), ID, LIST, MEASUREGEOM (Distance, Radius, Angle, ARea) |
| layers | -LAYER (Set, New, ON, OFF, TRansparency, Plot, Freeze, Thaw, LOck, Unlock, Description, stAte: ?, Save, Restore, Edit, Name, Delete, Import, EXport), LAYERSTATE, LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK, LAYISO, LAYUNISO, LAYERP, LAYERPMODE, VPLAYER (?, Freeze, Thaw, Reset, Newfrz, Vpvisdflt), LAYVPI |
| utilities | SELECTALL, UNITS, -UNITS, SCRIPT, SAVE, SETVAR (?), COLOR, -COLOR (Truecolor, COlorbook), LWEIGHT, -LWEIGHT (?), QUICKCALC, CAL, QUICKPROPERTIES |
| modify | TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN, LENGTHEN, PEDIT (Close, Open, Join, Width, Edit vertex, Fit, Spline, Decurve, Ltype gen, Reverse, Undo; Multiple; a 3D polyline's Spline curve too), OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, ARRAYRECT, ARRAYPOLAR, -ARRAY, ALIGN, CHANGE (Properties: Color, Elev, LAyer, LType, ltScale, LWeight, Thickness, TRansparency, Material, Annotative) |
| blocks | BLOCK, -BLOCK, INSERT, -INSERT, WBLOCK, -WBLOCK, ATTDEF, -ATTDEF, EATTEDIT, ATTEDIT, -ATTEDIT, ATTSYNC (?, Name, Select; a block picked with no attribute definitions refused and another asked for, an INSERT of a dynamic block naming the dynamic block, not its anonymous *U block; a dynamic block synced bringing in line the INSERTs of its *U blocks too, against its own definitions, each keeping its dynamic properties and the places they give its attributes; an attribute made anew for a tag out of order keeps its extended data and extension dictionary, as one synced in place does), BATTMAN, ATTEXT, -ATTEXT (Cdf, Sdf, Dxf, Objects), BASE, BEDIT, -BEDIT (?), BSAVE, BSAVEAS, BCLOSE (Save, Discard, Cancel), BAUTHORPALETTE, RESETBLOCK |
| text and clipboard | TEXTEDIT, MTEDIT, COPYCLIP, CUTCLIP, COPYBASE, PASTECLIP, PASTEBLOCK, PASTEORIG, PASTESPEC |
| annotation | DIMOVERRIDE (Clear overrides), TEXTALIGN (alIgnment, Options, Point), MLEADEREDIT (Remove leaders, Add leaders), MLEADERALIGN (Options), ATTIPEDIT, SPELL (All, Dictionary) (see [Annotation commands](#annotation-commands)) |
| plot | PLOT, -PLOT, PREVIEW, PAGESETUP, PNGOUT, JPGOUT, BMPOUT, EXPORT, -EXPORT (Dwf, dwfX, Pdf) ([Plotting](plot.md)) |
| dimension | DIM (Angular, Baseline, Continue, Ordinate, aliGn, Distribute, Layer, Undo), DIMLINEAR (Mtext, Text, Angle, Horizontal, Vertical, Rotated), DIMALIGNED (Mtext, Text, Angle), DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMARC (Mtext, Text, Angle, Partial, Leader), DIMORDINATE (Xdatum, Ydatum, Mtext, Text, Angle), DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK, DIMJOGLINE (Remove), DIMJOGGED (Mtext, Text, Angle), DIMREASSOCIATE (Select object), DIMDISASSOCIATE, -DIMDISASSOCIATE, DIMCENTER |
| tables | TABLE, -TABLE (Style, Width, Height), TABLESTYLE, TABLEDIT |
| selection | QSELECT, FILTER, FIND, DRAWORDER (Above objects, Under objects, Front, Back), TEXTTOFRONT, GROUP, -GROUP (?, Order, Add, Remove, Explode, REName, Selectable, Create), UNGROUP, CLASSICGROUP |
| OLE objects | INSERTOBJ (a bitmap or a PNG, as a static picture), and the variables OLEFRAME, OLEHIDE and OLEQUALITY (see OLE objects below) |
| references and images | XATTACH, IMAGEATTACH, XREF, EXTERNALREFERENCES, IMAGE, -XREF (?, Bind, Detach, Path, pathType, Unload, Reload, Overlay, Attach), XOPEN, XBIND, -XBIND (Block, Dimstyle, LAyer, LType, Style), -IMAGE (?, Detach, Path, Reload, Unload, Attach), IMAGECLIP (ON, OFF, Delete, New boundary), IMAGEADJUST, -IMAGEADJUST (Contrast, Fade, Brightness), XCLIP (ON, OFF, Clipdepth, Delete, generate Polyline, New boundary), TRANSPARENCY |
| housekeeping | PURGE, -PURGE (Blocks, Dimstyles, Groups, LAyers, LTypes, MUltileaderstyles, Plotstyles, SHapes, textSTyles, Tablestyles, Visualstyles, Regapps, Zero-length geometry, Empty text objects, All), RENAME, -RENAME (Block, Dimstyle, LAyer, LType, multileadeRstyle, Style, Tablestyle, Ucs, VIew, VPort) |

**Multilines (R2.1).**

MLINE (`ML`) writes `Current settings: Justification = Top, Scale = 20.00, Style = STANDARD` and asks `Specify start point or
[Justification/Scale/STyle]:`; each option asks its question (`Enter justification type [Top/Zero/Bottom] <top>:`, `Enter
mline scale <20.00>:`, `Enter mline style name or [?]:`, the question mark listing `Loaded mline styles:`) and comes back to
the settings and the start. Then `Specify next point:`, with `[Undo]` from the second point and `[Close/Undo]` from the third,
until Enter. The multiline is drawn under the cursor as the points so far and the cursor make it, and is made as the command
ends, one undo step; a scale of nought or below is taken, as AutoCAD takes it. The settings are the drawing's `$CMLJUST`,
`$CMLSCALE` and `$CMLSTYLE`, which CMLJUST, CMLSCALE and CMLSTYLE set; a drawing with no style STANDARD is given the one
AutoCAD starts with when MLINE or MLSTYLE first runs. The multiline is a `DxfUnknownEntity` written by
[`DxfMlineBuilder`](xref:GeometryHelper.Dxf.DxfMlineBuilder) as AutoCAD writes it, so that MOVE, COPY, ROTATE, SCALE and
MIRROR move it (its scale follows a scale, its elements stay on the same side of its line in a mirror).

MLEDIT asks `Enter mline editing option [CC/OC/MC/CT/OT/MT/CJ/AV/DV/CS/CA/WA]:` (the words AutoCAD's console asks; its window
shows a dialog of the same twelve tools, which the presenter has not designed). The crosses and tees and the corner joint ask
`Select first mline:` and `Select second mline:`, and again `Select first mline or [Undo]:` once a pair was tried; the others
ask `Select mline:` (the cuts and the weld then `Select second point:`). A pick that is no multiline is `Please pick mlines
only.`, two that do not meet `These multilines do not intersect.`, the second point of a cut on another multiline `Please pick
the same mline.`; Undo takes back the last edit of the command; Enter ends it. The records are those AutoCAD 2021 left, the
numbers read back from accoreconsole: for a style of three elements at 0.5, 0 and -0.5 at scale 1 through (0, 0)-(100, 0)
crossed by (50, -50)-(50, 50), CC breaks all three elements of the first from 50 to 51 (the width of the second), OC also
breaks the outer two of the second from 49 to 50, and MC breaks the outer two of each. CT, OT and MT trim or extend the first
(picked on the part to keep) to the line of the second, its elements stopping 1 short of it at the second's near edge, the
merged tee running the middle element on to the second's middle element (49.5); OT and MT also open the second's near element
across the first's width; the end caps of the end moved are left off (flag 4 or 8). CJ moves both ends to where the lines meet
and mitres the elements across the corner, matched by rank from one side of the bend to the other: of (0, 0)-(80, 0) and
(100, -50)-(100, 50) picked at (20, 0) and (100, -30), the first runs on to (100, 0) with its elements 1, 0.5 and 0 past it and
the second stops at 49, 49.5 and the whole. AV puts a vertex on the line at the pick (`(50, 0.3)` gives `(50, 0)`), DV takes the
nearest vertex out, CS cuts the element picked between two picks, CA every element, WA welds what is cut between two picks.
A pick on nothing asks again without a word, and Enter before the first edit is `2D point or option keyword required.`, as
AutoCAD's console answers. CJ with a closed multiline, and a tee whose first (the stem) is closed, are `Invalid operation on a
closed multiline.`; CT onto a closed multiline trims the stem to its near element and leaves the closed one whole. CS, CA and
WA from one segment to another cut from the first pick to the end of its segment, the segments between whole and the next
segment up to the second pick: CA from (50, 0) to (100, 50) on (0, 0)-(100, 0)-(100, 100) at scale 20 leaves the first segment
drawn to 50 and the second from 50 (70 for the bottom element, which starts at the outer corner); round the first vertex of a
closed one AutoCAD starts the cut on the first segment at the vertex, so that a stub of the outer element is left at the
corner, and the presenter does the same. A merged cross or tee of four elements breaks the elements pair by pair from the
outside: the outer pair of one across the outer pair of the other, the inner pair across the inner pair (AutoCAD 2021's
numbers at offsets 1, 0.5, -0.5, -1, scale 10: 90 to 110 and 95 to 105).

**Grips, STRETCH and the modify commands on a multiline.** A multiline has a grip at each vertex of its line, as AutoCAD
2021's `GetGripPoints`; dragging one moves the vertex as `MoveVertexAt` does: a segment that keeps its way keeps its cuts where
they were along it, one that turns is drawn whole again (cut 30 to 60 on (100, 0)-(100, 100), the vertex moved to (100, -10),
the cut is 40 to 70 from it). STRETCH moves the vertices inside its window the same way, and the whole multiline where all are
inside. TRIM and EXTEND work on a multiline as AutoCAD 2021 does: an edge counts only where it crosses every element of a
segment (one that misses an element leaves the multiline as it is); the new end stands on the multiline's line where the
element that runs furthest still reaches the edge, each element stopping where it meets the edge (trimmed by the edge
(40, -60)-(60, 60) and picked before it, (0, 0)-(100, 0) at scale 20 starts at 46.67, its top element at 50); a piece taken
out between two edges leaves two multilines, and a closed one opens between the two cuts round the pick. EXTEND runs the
nearer end on to the nearest edge every element meets. A multiline is a cutting or boundary edge for other objects by its
elements. OFFSET says `Cannot offset that object.`, FILLET `Fillet requires 2 lines, arcs, or circles.`, CHAMFER `Chamfer
requires 2 lines.`, LENGTHEN `Cannot LENGTHEN this object.` and BREAK `Object can't be broken` and asks again, AutoCAD's words.
Esc in the middle of MLINE makes nothing: AutoCAD 2021 adds the multiline to the drawing only when the command ends (its
drawing holds no entity while MLINE asks the next point), and the presenter does the same; the console cannot send Esc, so this
is read from that and not seen.

In the window MLEDIT shows the Multiline Edit Tools dialog: the twelve tools as pictures in AutoCAD's rows of four (Closed
Cross, Closed Tee, Corner Joint, Cut Single; Open Cross, Open Tee, Add Vertex, Cut All; Merged Cross, Merged Tee, Delete
Vertex, Weld All). A tool clicked closes it and MLEDIT asks that tool's multilines as the typed option of its letters; Close
ends the command. MLSTYLE shows the Multiline Style dialog: Current multiline style, the Styles, Set Current, New, Modify,
Rename (the name edited in the list), Delete, Load (Load Multiline Styles: a file, acad.mln to start with, and one of its
styles) and Save, the Description and a preview of the style picked, drawn as MLINE draws it. New asks Create New Multiline
Style (the name and the style to start with), then New Multiline Style: Description; Caps (Line, Outer arc and Inner arcs at
the start and end, the angles from 10 to 170); Fill color; Display joints; Elements with Offset, Color and Linetype, Add (at
most 16) and Delete (one kept). Modify shows the same page, all but the description greyed for STANDARD and a style a
multiline is drawn by. Each button acts at once, as AutoCAD's do, each an edit of MLSTYLE's one undo step; Set Current is
taken on OK, so Cancel keeps the styles made but not the style set current. The refusals are those of the typed questions
below. With no window both ask on the command line.

MLSTYLE has no typed form in AutoCAD 2021 (its console opens the Multiline Styles dialog and waits, and has no -MLSTYLE), so
with no window the presenter asks its own: `Current multiline style: STANDARD`, then `Enter an option [?/Current/New/Modify/Rename/Delete/Load/Save] <exit>:`. New asks
`Enter name of new multiline style:` (kept in capitals in the record, the entry as typed) and then `Enter style option
[Description/Elements/Caps/Fill/Joints/Angles/Done] <Done>:`; Elements lists the elements, the greatest offset first, and asks
`[Add/Delete/Change/Done]` with the offset, colour and linetype of each; Caps toggles `StartLine`, `StartOuter`, `StartInner`,
`EndLine`, `EndOuter` and `EndInner`; Fill is `[Off/Color]`, Joints shows a line across each joint, Angles asks the angles of
the start and end (10 to 170). Modify asks the same of a style of the drawing; STANDARD and a style a multiline is drawn by can
have only their description changed, as the dialog allows. Rename, Delete, Load and Save are the dialog's buttons as questions:
Rename asks the style (the current one on Enter) and `Enter new name for WALLS:`, its multilines named anew (STANDARD is `The
STANDARD multiline style cannot be renamed.`); Delete removes a style that is not STANDARD, current or drawn by a multiline;
Load asks `Enter name of multiline style file <acad.mln>:` (looked for as a support file) and `Enter name of style to load or
[?]:`, the question mark listing the file's styles; Save asks the style and the file and writes it as acad.mln is written,
the file's other styles kept and one of the same name written over.

**OLE objects (R3.2 follow-up).**

A drawing's OLE frames ([`DxfOle2Frame`](xref:GeometryHelper.Dxf.DxfOle2Frame)) are drawn and edited. A frame that holds a bitmap, a
pasted picture or one INSERTOBJ made, shows its picture on the frame as a `DisplayImage2` that carries the bytes of the .bmp
instead of a file name; two drawings holding the same picture decode it once. The frame is drawn over it as OLEFRAME says: 0 no
frame, 1 frames shown and plotted, 2, the default, frames shown and left off a plot (the same `IsNotPlotted` as XCLIPFRAME 2); at 0 a
point on the picture picks it, and its grips are not shown, as AutoCAD 2021 says the frame must be displayed for them to be. An
object the presenter cannot draw, one of another application, a metafile, or a frame whose compound file is damaged, is drawn as its
frame alone with its kind (`Picture (Device Independent Bitmap)`, `OLE Package`) written in its corner, a tenth of its shorter side
high, as the name of an image not found is; AutoCAD asks the application, or shows the picture it kept, which the library does not
read. OLEHIDE leaves frames out of the display and of every plot: 1 those of model space, 2 those of paper space, 3 all. A plot at
the plot quality Monochrome (group 73 of 0, the Plot quality row) draws the picture in black and white, by its luminance, and the
other qualities in its colours. OLEFRAME, OLEHIDE and OLEQUALITY are the document's, set by SETVAR or typed alone
(`Enter new value for OLEFRAME <2>:`, `Requires an integer between 0 and 2.`) and kept while it is open: AutoCAD keeps OLEFRAME in the
drawing and the other two in the registry, but a DXF has no place for them, and none of the three is an undo step.

A frame has four corner grips, from the lower left to the upper left, upper right and lower right, as AutoCAD 2021's
`GetGripPoints` gives them (a frame 40 by 20 with its lower left corner at (10, 20) has (10, 20), (10, 40), (50, 40) and (50, 20)).
Dragging one scales the frame about the corner across from it, as AutoCAD 2021's `MoveGripPointsAt` does (the corner (50, 40) of that
frame dragged to (60, 45) makes it 50 by 25; dragged to (50, 50) it is 40 by 30 with Lock aspect off and 60 by 30 with it on,
both sides by the larger factor, 1.5): each side by the point's distance from the kept corner over the grip's, the
frame kept on its side of that corner however far the point goes, never turned over. MOVE, COPY, SCALE and ARRAY move a frame as the
library does. ROTATE keeps it upright, as AutoCAD 2021's GUI does (its ROTATE of 30 degrees "only moves the picture"): the frame's
centre goes round the base point, the frame as large as it was and along x and y, its rotation still 0; MIRROR about a line along x or y
makes the box of the mirrored corners, the picture not turned over, and about any other line moves the frame's centre to its
mirror image in the same way, which accoreconsole 2021 could not show (it leaves a mirrored frame as it is). The Properties palette
has a group OLE: Position X and Y (the lower left corner), Width and Height, Scale width and Scale height in percent of the
size at 100 percent, Lock aspect, Type (Static, Embedded or Linked, read only), Plot quality (Monochrome, Low graphics or High
graphics) and Source application (read only); Width and Height size the frame from its upper left corner and the other side follows
while Lock aspect is on, Scale width and Scale height take the percent with or without its sign.

INSERTOBJ (`IO`) is AutoCAD's Insert Object dialog reduced to its Create from File, for a bitmap or a PNG: the window's Open dialog
titled Insert Object, of the one type `Picture (*.bmp;*.png)`, or with FILEDIA 0 `Enter name of picture file to insert:`. The picture
becomes a static OLE object (`new DxfOle2Frame(bytes)`: 96 dots an inch where it gives none, so that 40 by 30 pixels are 10.5820 by
7.9365 millimetres) with its upper left corner at the upper left of the view, as AutoCAD places an object inserted, in the space
worked in and on the current layer, its plot quality that of OLEQUALITY where that is 0, 1 or 2, and one undo step. A name that is
not a file is answered `Can't find file.`; one that is no .bmp or .png is refused, `.JPG files cannot be inserted by the presenter,
which takes a bitmap or a PNG picture: nothing was inserted.`, and one that is no picture, `The file is no picture an OLE object
can be made of:` and why. Create New, Link, Display as Icon and the documents of other applications need the application's own
server and are not built, and AutoCAD makes an object of Paint of a bitmap where this makes a static one. PASTECLIP of a picture
on the Windows clipboard (a bitmap another application copied, read as a PNG) makes the same frame where the clipboard holds no
objects and no text, as AutoCAD pastes words as an MTEXT before a picture; PASTESPEC offers it as Image (PNG), and PASTEBLOCK and
PASTEORIG refuse it. OLESCALE and OLEOPEN, OLERESET, OLELINKS, OLECONVERT and COPYLINK are not built.

**Shapes and hatching (batch 3 of acad.pgp).**

The prompts of these were read from AutoCAD 2021 (its resources and accoreconsole probes). SOLID asks `Specify first
point:`, `Specify second point:`, `Specify third point:` and `Specify fourth point or <exit>:`, and keeps the corners in the
order they are picked, so that the area runs 1, 2, 4, 3 round; Enter at the fourth makes a triangle of the third corner
twice. It goes on with `Specify third point:`, the third and fourth corners of the last solid the first and second of the
next (after a triangle, the third corner twice), until Enter; the whole command is one undo step. A strip of (0, 0), (10, 0),
(0, 10), (10, 10), (0, 20), (10, 20) is two solids, the second from (0, 10) and (10, 10).

-BOUNDARY asks `Specify internal point or [Advanced options]:` and makes the area round each point and its islands as closed
polylines, `BOUNDARY created 2 polylines`. Advanced options asks `Enter an option [Island detection/Object type]:` until
Enter: island detection off makes the outer loop alone, and BOUNDARY takes the same settings for the rest of the session.
An object type of Region makes nothing: a region is ACIS data, which the presenter cannot write yet, and the command says so.
BOUNDARY (`BO`) shows the window's Boundary Creation dialog first, as AutoCAD 2021's: Pick Points, Island detection, Boundary
retention (Retain boundaries, always on, and the object type, Polyline or Region) and the boundary set (the current viewport:
every object of the space; New is not built). Pick Points keeps the settings, shared with -BOUNDARY, and asks `Pick internal
point:` until Enter; Cancel ends the command with nothing kept. The request is a public
[`CadBoundaryRequest`](xref:GeometryHelper.Dxf.Viewer.CadBoundaryRequest); with no window BOUNDARY asks the points at once.
An area with an ellipse or a spline in it, as a side of the area or as an island, makes no polyline either: `Spline or
ellipse detected. Cannot create boundary.`, as AutoCAD refuses it (HATCH takes such an area). An ellipse or spline elsewhere
in the drawing does not stop it. With the object type Region, -BOUNDARY and BOUNDARY make a region of each loop instead
(`BOUNDARY created 2 regions`), an island a region of its own and not a hole in the outer one; a whole ellipse is a region, a
spline still refuses. AutoCAD's Boundary set is not built for -BOUNDARY.

REGION asks `Select objects:` and makes a region of every closed loop the objects make: a circle, a whole ellipse and a closed
polyline each alone, and lines, arcs, elliptical arcs and open polylines whose ends meet two and two (four LINEs round a
rectangle are one loop). It says `5 loops extracted.` and `5 Regions created.` as AutoCAD 2021 does of eight objects (a
rectangle, two circles, four lines and a polyline of bulges), the regions made as one undo step on the current layer; a
circle inside a rectangle is a region of its own, not a hole (SUBTRACT, not built, makes one). Lines that meet three at
a point, a loop out of one plane and a closed spline make none (the spline is said). The data is written for the
drawing's version, SAT text up to R2010 and SAB in the data store of the file from R2013
([regions](../dxf/entities.md#regions)); a region is drawn by the curves of its loops, listed by LIST (`Area:`, `Perimeter:`
and the bounding box, as AutoCAD lists one), measured by AREA and MEASUREGEOM (`Area = 1121.4602, Perimeter = 171.4159`),
named Region with its area and perimeter in Properties, and exploded by EXPLODE into its curves as AutoCAD explodes it.
A region is not moved, rotated, scaled or copied yet, and a DWG of R2000 to R2010 leaves it out.

-HATCH writes `Current hatch pattern:  ANSI31` and asks `Specify internal point or [Properties/Select objects/draW
boundary/remove Boundaries/Advanced/DRaw order/Origin/ANnotative/hatch COlor/LAyer/Transparency]:` until Enter, then makes
the hatch as one undo step. A point in an area already specified says `This area has already been specified.`; Undo takes the
last pick back; Select objects takes closed objects and asks again. Properties types the pattern as HATCHEDIT does and goes
back to the prompt; for a gradient it asks `Enter a gradient name or [?/Single color/Two color] <GR_LINEAR>:`, `Specify an
angle for the gradient <0>:` and `Center the gradient [Yes/No] <Yes>:`. Two color asks `New gradient color
[Truecolor/COlorbook] <0,0,255>:` and `New gradient second color:[Truecolor/COlorbook] <255,255,153>:`, each a name, an
index, red, green and blue as `255,128,0`, or Truecolor and `Red,Green,Blue:`; a colour that is none is said so, `A color
number or standard color name is required.`, and asked again. Single color asks the first and then `Specify a shade or tint
value (0-1) <1.0000>:`, and goes back to the prompt with the name and angle as they were, as AutoCAD does. The gradient is
written as AutoCAD writes one of one colour: the colour at 0, its shade or tint at 1, group 452 of 1 and the value in group
462. The end is the colour's hue and saturation at the lightness the value says (red at 0.25 ends in 128, 0, 0; at 1 in white;
(255, 200, 200) at 0.5 in a full red), the way AutoCAD 2021 worked out the ends of 45 pairs of colour and tint it was probed
with; a value outside 0 to 1 is the nearer end.

The Hatch dialog of HATCH's seTtings and of HATCHEDIT has the same settings in a third column: Gradient colours (One color
with a Shade to Tint slider, the second swatch hidden, or Two color), Hatch color, Background, Layer and Transparency (Use
Current, ByLayer, ByBlock or a value from 0 to 90, 95 refused `Requires an integer between 0 and 90.`), Hatch origin (Use
current origin, the Origin X, Y greyed; Specified origin with Origin X, Y, or Default to boundary extents and its corner;
Store as default origin) and Gap tolerance (0 to 5000, `Value must be between 0 and 5000.`). A specified origin not stored
as default lays the hatches of this HATCH alone, the current origin given back when it ends; a corner of the extents is kept
either way, as -HATCH keeps it.

draW boundary asks `Retain polyline boundary? [Yes/No] <N>:`, `Specify start point:`, `Specify next point or [Arc/Length/Undo]:`
(`[Arc/Close/Length/Undo]` from the third point) and `Specify start point for new boundary or <Accept>:`, and makes a loop of
each shape drawn, one inside another an island; Enter leaves a shape open and the hatch closes it across. The hatch is not
associative, and associativity stays off afterwards, as AutoCAD leaves HPASSOC. Length goes on along the last segment; an Arc
segment is not drawn here (draw it with PLINE and use Select objects). remove Boundaries asks `Select objects or [Add
boundaries]:` and takes out the loops the objects selected make (an island by its circle, say; the outer loop going takes the
area with it), asks `Select objects or [Add boundaries/Undo]:` again, Undo bringing the last removal back and Add boundaries
going back to the point prompt. Origin asks `[Use current origin/Set new origin/Default to boundary extents] <Use current
origin>:`: Set new origin asks `Select point:` and `Store as default origin? [Yes/No] <N>:` (Yes keeps it for the next hatches,
No lays this -HATCH's hatches from it alone), and Default to boundary extents asks `[bottom Left/bottom Right/top rIght/top
lEft/Center] <bottom Left>:` and lays each hatch's pattern from that corner, or the middle, of the box round its outer loops,
the corner kept as HPORIGINMODE keeps it. hatch COlor asks `New color [Truecolor/COlorbook/. (for use current)] <use
current>:` and `New background color [Truecolor/COlorbook/. (for none)] <None>:`, LAyer `Specify layer or [. (for use
current)] <use current>:` (`Cannot find layer "NOSUCH".` and asks again for a layer the drawing has not), and Transparency
`Specify transparency value (0-90) or [. (for use current)] <use current>:` (a whole number, ByLayer or ByBlock; the alpha is
255 times what is left, rounded down, 127 for 50), a dot putting each back to the current one; they are kept in the
document's hatch settings, as AutoCAD keeps HPCOLOR, HPBACKGROUNDCOLOR, HPLAYER and HPTRANSPARENCY. ANnotative asks `Create
annotative hatch [Yes/No] <N>:`; the presenter does not write the annotation scale of an annotative hatch yet, so Yes is said
so and the hatch is made as before.

Advanced asks `Enter an option [Boundary set/Retain boundary/Island detection/Style/Associativity/Gap tolerance/separate
Hatches]:` until Enter: Boundary set asks `Specify candidate set for boundary [New/Everything] <Everything>:`, New taking
`Select objects:` and then finding an area among those objects alone (a point where they close none says `Valid hatch
boundary not found.`), Everything all the objects of the space again; Retain boundary keeps polylines of the loops as well
(those of an ellipse or spline have no corners and are left out), Style is `[Ignore/Outer/Normal]`, Gap tolerance asks `Specify
a boundary gap tolerance value <0>:` from 0 to 5000 (`Value must be between 0 and 5000.` outside), says `HPGAPTOL set to 3` and
joins two loose ends no further apart than that with a straight edge of no object, so that a square 100 by 100 with a gap of 2
in one side is hatched at 3 and not at 1 (a gap to the middle of another line is not closed; HATCH takes the same value),
and separate Hatches makes one hatch of each pick. DRaw order asks `Enter draw order [do Not assign/send to Back/bring to
Front/send beHind boundary/bring in front of bounDary] <send beHind boundary>:`, as AutoCAD starts (HPDRAWORDER 3): the hatch
is put under the lowest object of its boundary, and -HATCH and HATCH share the choice. HATCH draws the hatch behind its
boundary now too, as AutoCAD 2021's does: a square of four lines hatched by HATCH has the hatch first in the draw order. A
hatch whose loops no object makes (draW boundary) stays on top. GRADIENT is HATCH with the type set to a gradient, as
AutoCAD's ribbon takes it; HATCHTOBACK sends every hatch of the current space behind the other objects (`1 hatch object(s)
sent to back.`, or `No hatch objects found.`).

SPLINEDIT asks `Select spline:` and `Enter an option [Fit data/Close/Join/Move vertex/Refine/rEverse/convert to
Polyline/Undo/eXit] <eXit>:`: Fit data only for a spline of fit points, Open in place of Close once it is closed, and Join
only while it is open (the prompts and the splines were read from AutoCAD 2021 and met to 1E-5). Close makes a spline of fit
points the periodic cubic AutoCAD makes through them and its first point again (the spline through (0, 0), (10, 10), (20, 0),
(30, 10) and (40, 0) gets the 8 control points AutoCAD wrote for it), and Open the open cubic through the points again; a
spline of control points is closed as AutoCAD closes one, its control points taken as the points of one period with uniform
knots a step apart (the cubic of 7 control points gets AutoCAD's periodic form of 10 control points and the knots 0, 0, 0, 0, 1
to 6, 7, 7, 7, 7), and Open gives the 7 points back with the knots 0, 0, 0, 0, 1, 2, 3, 4, 4, 4, 4, whatever knots it had
(its curve is then the uniform one of those points). Move vertex marks the first control vertex and asks `Specify new location
or [Next/Previous/Select point/eXit] <N>:`: a point moves the marked vertex, which stays marked, and a spline of fit points
loses its fit data. rEverse says `Spline has been reversed.`; convert to Polyline asks `Specify a precision <10>:` and replaces
the spline by a polyline of lines (53 vertices for the spline above at 10) and ends the command.

Fit data asks `Enter a fit data option [Add/Close/Delete/Kink/Move/Purge/Tangents/toLerance/eXit] <eXit>:` (Open for Close
once it is closed). Add asks `Specify existing fit point on spline <exit>:` and then `Specify new fit point to add <exit>:`
for each point, put in after the one before it; at the first point it asks `Specify new point or [After/Before] <exit>:`, a
point typed going before it. Delete takes out the fit point picked, Move is Move vertex on the fit points, and each change
fits the cubic through the points anew, as AutoCAD does; a pick that is no fit point says `Not a fit point.`. Kink asks
`Specify a point on the spline <exit>:` and puts a knot in at the nearest point of the curve until it stands three times (a
knot the spline has already counts), the curve kept and a corner allowed there: at the fit point (20, 0) of the spline above
the knots 28.28 stand three times and the fourth 1E-8 past, as AutoCAD wrote them. Purge takes the fit data out and goes
back to the top menu. Tangents asks `Specify start tangent or [System default]:` and `Specify end tangent or [System
default]:`, a point each: the start tangent runs from the point to the first fit point and the end from the last fit point to
the point, and System default at the end takes both away. toLerance asks `Enter fit tolerance <1.0000E-10>:` and writes it;
AutoCAD fits a curve that passes near the points within it, the presenter keeps the curve through them. Join asks `Select
any open curves to join to source:` and takes what meets an end of the spline, as JOIN does (a line, an arc, a polyline, a
spline), saying `1 object joined to source` and, for the curves it could not take, `, 1 object discarded from the operation`;
the fit data go with it. Refine asks `Enter a vertex editing option [Add/Delete/Elevate order/Move/Weight/eXit] <eXit>:`: Add
(`Specify a point on the spline <exit>:`) puts a knot in at the nearest point, the curve kept and a control point more,
Delete (`Specify control vertex to delete:`, `Not a control vertex.` for a pick that is none) takes a control point out,
Elevate order (`Enter new order <4>:`) raises the degree to one less than the order, Move is Move vertex, and Weight says
`Spline is not rational. Will make it so.` for a spline that is not and asks `Enter new weight (current = 1.0000) or
[Next/Previous/Select point/eXit] <N>:` of each control vertex in turn. The fit data go with the first change of Refine, not
before. Elevate order stops at order 11 (AutoCAD's 26).
CLASSICINSERT shows the same Insert palette as INSERT: AutoCAD's classic Insert dialog asks the same things (the block
or file, insertion point, scale and rotation each given or specified on screen, explode) in another frame, so it is not
built apart.

**The command-line forms and SAVE (batch 4 of acad.pgp).**

The prompts and messages of these were read from AutoCAD 2021 (its resources and accoreconsole probes). -PAN (`-P`) asks
`Specify base point or displacement:` and `Specify second point:` as PAN does, and refuses Enter at the first, `Requires
two points, or a displacement followed by ENTER.`, asking again where PAN ends.

-MTEXT (`-T`) asks the corners and the options of MTEXT (`Specify opposite corner or
[Height/Justify/Line spacing/Rotation/Style/Width/Columns]:`) and then, where MTEXT opens its editor, `MText:` a line at a time
until a blank one; the lines are the paragraphs of one text (`Hello\PWorld`), and a blank first line makes an MTEXT of no words,
as AutoCAD 2021 does.

LAYOUT (`LO`) and -LAYOUT (`-LO`) ask alike, in the words of AutoCAD 2021: `Enter layout option [Copy/Delete/New/Template/Rename/SAveas/Set/?] <set>:`,
Set the default. The layout offered by Set, Rename, Copy, Delete and SAveas is the one shown, or the first tab where Model is
shown (`Enter layout to make current <Layout1>:`). Set says `Cannot find layout "X".`
for a name that is none; `?` writes `Active Layouts:` and a row `Layout: Layout1       Block name: *Paper_Space.` for each
sheet, by name. New asks `Enter new Layout name <Layout2>:`, the first LayoutN not taken, says nothing when it is made, and
`Layout "X" already exists, not created.` otherwise. Rename asks the layout and `Enter new layout name:` (a change of letters
alone is allowed), and says `Layout "Plan" renamed to "Sheet1".`. Copy asks `Enter name of layout to copy <Layout1>:` and `Enter
layout name for copy <Layout1 (2)>:`, makes a layout of the page settings of the first and a copy of everything in its sheet,
its viewports among it, and says `Layout "Layout1" copied to "Layout1 (2)".`. Delete asks `Enter name of layout to delete
<Layout1>:`, takes the layout with its block and what is drawn in it out, and says `Layout "Plan" deleted.`; where it was the
sheet shown, the next tab is shown (else the one before it), and where it was the only sheet a new Layout1 is made and shown, as
AutoCAD 2021 leaves a drawing one; the tabs left keep their numbers (Layout1 1, C 3, D 4), as AutoCAD keeps them, and the next new
layout takes the number after the highest. The sheet whose block is `*Paper_Space`, drawn in the ENTITIES section, cannot just go: a
DXF without that block is refused by AutoCAD 2021 ("Missing Default entry *Paper_Space in SymbolTable:BLOCK_RECORD"), so the layout
that follows it (the last one where there is none) is moved into its place, with its page settings and everything in its sheet,
under its own name.
Model is refused by Copy, Delete and Rename (`May not delete Model tab.`), and a name with one of the characters < > / \ " : ; ? * | , = or a backtick in it, or
longer than 255 letters, by New, Rename and Copy. Each option is one undo step.

Template asks `Enter file name:` (a name with no extension is looked for as .dwt, in the folder of the drawing where it is not
found as typed, and `"nofile.dwt": Can't find file.` says it is not there) and `Enter layout name(s) or [?]:`; `?` writes
`Layout(s) in drawing:` and the sheets of the file, and asks again, names are parted by commas and take wild cards (`Lay*`), a name
the file has no sheet of is passed over, and where none is found `The specified layout(s) were not found in <file>.` is said and the
question asked again; Enter ends it. The file may be a DWT or DWG (read as a DWG) or a DXF. Each sheet comes as the last tabs, with
its page settings and everything in it, its own viewport 1 made again, and its layers, linetypes and styles; the layout shown does not change,
nothing is said, and the whole is one undo step. A sheet takes its own name where the drawing has no layout of that name, else
`Layout3-Plan`: the first N that no layout of the drawing starts with (the numbers AutoCAD 2021 gave: with Layout1, Layout2 and Plan
there, a second Plan was Layout3-Plan and Layout1 then Layout4-Layout1). SAveas asks `Enter layout to save to template <Layout1>:`
and `Enter name of output file:` and writes the drawing as a template of that one layout: the other layouts out, model space empty,
the layers, linetypes, styles and blocks as they are, as AutoCAD 2021 writes it. A name with no extension takes .dwt; .dwt and .dwg
are written as a DWG of AutoCAD 2018, .dxf as a DXF after the questions DXFOUT asks of its settings; a file of the name is replaced
without asking, as AutoCAD's console does. Model says `May not save Model tab.`, a name that is no layout `Layout "x" does not exist.`.
The Template Description and Insert Layout dialogs of AutoCAD are not built: the command line asks as the console does.

-XBIND (`-XB`, XBIND, `XB`) asks `Enter symbol type to bind [Block/Dimstyle/LAyer/LType/Style]:` and `Enter dependent Layer
name(s):`, names with wildcards parted by commas, and binds what an external reference brought into the drawing, as
AutoCAD 2021's console does: the layer `grid|WALL` becomes `grid$0$WALL` (`grid$1$WALL` where that is taken) and is the
drawing's own, with what drew by it and the linetype, text style or dimension style it names following, and `1 Layer(s) bound.`
or `No matching Layer names found.` (a name the drawing made itself is never matched). Block makes the block `grid$0$BLK` of
the block BLK of the drawing grid references, with a copy of what it draws, from the file the reference names; the layers,
linetypes and text styles its entities use are bound with it, as AutoCAD 2021 binds them (a line on WALL and a text on DOOR in
MYSTYLE come as `grid$0$WALL`, `grid$0$DOOR` and `grid$0$MYSTYLE`, the reference's `grid|WALL` gone with them), layer 0 and what
the block does not use left as they were. The command asks the symbol type again
until Enter and is one undo step. In the window XBIND shows the Xbind dialog: the Xrefs on the left, each reference with
its Block, Dimstyle, Layer, Linetype and Textstyle definitions (the blocks read from the file it names), Definitions to Bind
on the right, Add (a definition, or every one under a kind or a reference, each listed once) and Remove between them. OK
binds those listed as -XBIND binds them, `1 Layer(s) bound.` written for each kind, the whole one undo step; with no window
XBIND asks -XBIND's questions.

SAVE asks, with FILEDIA 0 or no window, `Save drawing as <file>:` and, where the file is there, `A drawing with this name
already exists.` and `Do you want to replace it? <N>:`; the drawing is known by the new name afterwards, as after SAVEAS (which
also asks the format), a name ending in .dwg written as a DWG of AutoCAD 2018 and any other as a DXF. With a window and FILEDIA 1
it shows the Save Drawing As dialog, as SAVEAS does.
**Viewports, filters, QuickCalc and Quick Properties (batch 4 of acad.pgp).**

VPORTS (`VIEWPORTS`) shows the Viewports dialog in model space: New Viewports applies a standard configuration (Single, Two:
Vertical and Horizontal, Three: Right, Left, Above, Below, Vertical and Horizontal, Four: Equal, Right and Left) to the whole
display or to the current viewport, previewed, and saves the result under the new name where one is typed; Named Viewports
restores a configuration saved, or deletes it. -VPORTS asks AutoCAD 2021's `Enter an option
[Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde] <3>:`; 2 asks `Enter a configuration option [Horizontal/Vertical]
<Vertical>:`, 3 `Enter a configuration option [Horizontal/Vertical/Above/Below/Left/Right] <Right>:`, and each splits the
current viewport. The viewport that stays current is the one AutoCAD keeps (probed in accoreconsole): the right half of 2
Vertical, the top of 2 Horizontal, the big one of 3 Right, the lower right of 4; the new ones take the next free numbers
(CVPORT 3, 4, 5). Every new viewport shows the whole of what the one split showed, its centre kept, fitted to its own shape:
a view 100 wide and 100 high split by 2 Vertical on a square area is 100 wide and 200 high in each half. SIngle keeps the
current viewport alone (nothing where it is alone already); Save asks `Enter name for new viewport configuration or [?]:`
and, for a name in use, `Viewport configuration "A" already exists.  Replace it? <N>`; Restore asks `Enter name of viewport
configuration to restore or [?]:` and says `Cannot find viewport configuration "NOPE".` before asking again; Delete takes
names and wild cards (`Deleted 2 viewport configurations.`, `Deleted one viewport configuration.`, `No matching viewport
configurations found.`); ? writes `Current configuration:`, each `id# 2` with its `   corners: 0.5000,0.0000
1.0000,1.0000`, and each configuration named; Toggle shows the current viewport alone and brings the others back the next
time, as AutoCAD keeps them under `*Multiple`; Join asks `Select dominant viewport <current viewport>:` and `Select viewport to
join:`, a viewport picked by a click in it, and refuses two that make no rectangle (`The selected viewports do not form a
rectangle.`). Each change is one undo step. In a layout VPORTS and -VPORTS ask MVIEW's prompts, as AutoCAD's do. The tiles are
[`CadDocument.TiledViewports`](xref:GeometryHelper.Dxf.Viewer.CadTiledViewports), read from the `*Active` records of the
drawing's VPORT table and written back when it is saved: the current viewport into the first `*Active` record, which keeps
the snap and grid, the others and each configuration as copies of it with their own corners (10, 20, 11, 21) and views (12,
22, 40, 41). A drawing never split is saved with its VPORT table as it was. The window lays the canvas over the current
viewport, framed in the accent, and draws the others beside it with their own views; a click in one makes it current. A drawing saved with
tiles opens with them: the canvas takes the view of the current viewport (its centre and height, 12, 22 and 40, the height kept
as AutoCAD keeps it, the width following the shape of the canvas), and the others are drawn with theirs; a drawing with one viewport
still opens fitted to its extents. The other viewports are drawn again only when what they show changes (an edit, a selection,
their own view or size), not when the current viewport is panned or zoomed. MOde asks `Apply viewport configuration to
[Display/Current viewport] <Display>:` and then the options again, as AutoCAD 2021's -VPORTS does; the answer is what the
Viewports dialog starts with in Apply to. 2, 3 and 4 split the current viewport whichever it is (probed in accoreconsole
from one viewport to seven: the same corners and the same viewport current in both modes), so the mode changes nothing
there. Change view to and Visual Style of the dialog are not built (a 2D presenter has one view and one style).

FILTER (`FI`) shows the Object Selection Filters dialog. Its Select Filter list is AutoCAD 2021's (read from its resources):
the kinds of object (Arc, Attribute, Block, Circle, Dimension, Hatch, Line, Polyline, Text, Viewport, Xline and the rest),
the properties (Arc Center, Arc Radius, Block Name, Block Position, Block Rotation, Circle Center, Circle Radius, Color,
Dimension Style, Elevation, Layer, Line Start, Line End, Linetype, Linetype Scale, Normal Vector, Point Position, Text
Position, Text Value, Text Style Name, Text Height, Text Rotation, Thickness, Viewport Center, Xdata ID) and the begin and
end of AND, OR, XOR and NOT groups. A line is `Object = Line`, `Layer = WALLS` (wild cards taken), `Color = 1 - Red` (300
says `Invalid Color number.`), or a test on a number or a point with its operator, `=`, `!=`, `<`, `<=`, `>` or `>=`:
`Arc Radius >= 15.0000`, and for a point the operator of each of X, Y and Z, where `*` leaves a coordinate out: `Line End
X >= 100.0000  Y * 0.0000  Z * 0.0000` passes the lines that end at X 100 or more; a rotation is in degrees. Lines outside a
group all have to hold; a group does what its name says (AND all its operands, OR one at least, XOR exactly one of two, NOT
the one), a group may hold groups, and `Apply` refuses a list that is not well formed in AutoCAD's words (`Invalid Filter
list - Begin/end clause mismatch.`, `Premature end.`, `Empty clause/bad nesting.`, `Wrong number of XOR/NOT operands.`,
`Maximum nesting exceeded.`). A filter that takes the circles whose radius is under 1: Object = Circle, Begin NOT, Circle
Radius >= 1, End NOT. Add to List, Substitute, Add Selected Object (the dialog closes while one object is picked, and its
kind, layer, colour, linetype and what belongs to its kind, an arc's centre and radius, a text's position, words, style,
height and rotation, join the list), Edit Item, Delete, Clear List, and named lists (`Null Filter name not allowed.`,
`Invalid characters in Filter name.`, `Invalid Filter name.` for `*unnamed`, `Cannot delete *unnamed Filter list.`) kept in
AutoCAD's own file, `filter.nfl`, which FILTER reads each time it starts: in the app it is `%AppData%\GeometryHelper\Dxf\filter.nfl`,
`Filter.nfl  --  Do not edit this file.` then, for each list, `:ai_lisp|NAME` with its tests as the lists of an AutoLISP `ssget`
filter (`(8 . "WALLS")`, `(-4 . ">=")` and `(40 . 15.0)`, `(-4 . "<NOT")`) and `:ai_str|NAME` with the lines as the dialog
lists them; a file that does not read says `Syntax error in Named Filter List file.  Delete filter.nfl.`
([`CadSelectionFilter.ToNfl`](xref:GeometryHelper.Dxf.Viewer.CadSelectionFilter.ToNfl),
[`LoadNfl`](xref:GeometryHelper.Dxf.Viewer.CadSelectionFilter.LoadNfl)). Apply writes `Applying filter to selection.`, asks
`Select objects:`, says how many `were filtered out`, and keeps what passed as the Previous set, so the next command takes it
with P, then `Exiting filtered selection.`. A test on a property tests the objects that have it: `Arc Radius` an arc, `Text
Height` a text or an MTEXT; AutoCAD tests any object that has the same group code, so combine a property with its Object
line as the help does. Transparency, MultiLine Style, the shapes' properties, the surface kinds Blend, Network, Patch,
Fillet, Offset and Extend, and the redefine prompt `A Filter with this name already exists.` are not built.

QUICKCALC (`QC`) shows the QuickCalc palette, which stays open while commands run: the input, evaluated by Enter or =, the
history (a line double-clicked comes back), the number pad with its memory (MC, MR, MS, M+, M-), Scientific (sin, cos, tan
as `tang`, log, 10^x, asin, acos, atan, ln, e^x, r2d, d2r, abs, rnd, trunc), Clear, Clear History, Paste value to command
line, and the buttons that pick in the drawing: Get Coordinates, Distance Between Two Points, Angle of Line Defined by Two
Points, Intersection of Two Lines Defined by Four Points, each asking `>> Enter a point:` through CAL and putting the value
into the input (refused while a command runs, `Acquiring points from the drawing area is not possible during a command`).
CAL asks `>> Expression:` and writes the value as AutoCAD 2021's command line does, whatever the units: a whole number as
it is, a real number to nine significant digits, a point as `x,y,z`. `1+2*3` is 7, `sqrt(16)+2` 6, `dist([0,0],[300,400])`
500, `1/3` 0.333333333, `ang([0,0],[300,400])` 53.1301024, `[100,50]+[20,30]` 120,80,0; `2^3^2` is 64 (the powers are taken
from the left). It asks for what the expression picks in the drawing, in the order it meets it: `cur` `>> Enter a point:`;
the snap functions `end`, `ins`, `int`, `mid`, `cen`, `nea`, `nod`, `qua`, `per` and `tan` `>> Select entity for END snap:`
(the point of the object picked that the snap gives, wherever on it the pick lay: `end` the end nearest the pick, `qua`
the nearest quadrant; `per` and `tan` from the last point, the foot or touching point nearest the pick; `int` needs the
pick at a crossing; an object with no such point says `Improper type of object picked.`, a pick on nothing `No object
picked.`, and the prompt comes again); `rad` `>> Select circle, arc or polyline segment for RAD function:` (a circle or an
arc; a polyline segment is not built); and `mee`, `vee`, `vee1`, `dee` and `ille`, which pick two or four ends. `dist(end,end)`
picks two ends. The vector functions are `vec(p1,p2)`, `vec1`, `nor(v)`, `nor(p1,p2)`, `nor(p1,p2,p3)`, `dpl(p,p1,p2)`,
`dpp(p,p1,p2,p3)`, `plt(p1,p2,t)`, `pld(p1,p2,dist)`, `ilp`, `ang(apex,p1,p2)` and `ang(apex,p1,p2,normal)`, `abs` of a
point is its length, and `cvunit(value,from,to)` converts the common units of length (mm, cm, m, km, inch, ft, yd, mile,
micron) and angle (degrees, radians, grads). `tan` is the snap, not the tangent of an angle (`tang`, as AutoCAD's); `app`
is no function of AutoCAD 2021's CAL. A wrong expression writes `Error:` and AutoCAD's reason (`Operators '+', '-' require
operands of the same type`, `Division by 0`) and asks again. Typed as `'CAL` at a prompt for a point, a distance or an
angle, CAL runs inside the command: its prompts are written after `>>`, so `>>>> Expression:`, nothing is written for the
value, and once `Resuming LINE command.` is written the value is typed at the prompt that was waiting, the point as
`11,21,0` or the distance as `7`; Esc ends both. The calculator is
[`CadCalculator`](xref:GeometryHelper.Dxf.Viewer.CadCalculator); Units Conversion, Variables, feet and inches, `getvar`,
and the transformations (`rot`, `rotx`) are not built.

QUICKPROPERTIES (`QP`) shows the Quick Properties panel near the cursor over the objects selected (asking `Select objects:`
where none are): the kind and count, then Color, Layer and Linetype, edited as in Properties, one undo step each. QPMODE starts
at -1, as AutoCAD 2021's does; above nought the panel comes up whenever objects are picked with no command running, and
Ctrl+Shift+P switches its sign, saying `<Quick Properties panel on>` or `<Quick Properties panel off>`. The panel's
customization (the CUI's lists by object kind) and QPLOCATION are not built; QPMODE lasts for the session.

A dimension's picture is laid out from its style as AutoCAD 2021 lays it out: DIMTAD puts the words on the line, which
is broken about them by DIMGAP (0), above it (1, and JIS, 3), on the side away from the points measured (2) or below it
(4); DIMJUST puts them in the middle, next to an extension line or over one; and where the words and the arrowheads do
not both fit, DIMTIX, DIMSOXD, DIMATFIT and DIMTOFL say what moves outside, DIMTIH and DIMTOH whether the words stay
level. With ISO-25's sizes a dimension of 100 has its words "100" in the middle, one of 5 its words inside and its
arrowheads outside, and one of 7 its arrowheads inside and its words 3.9 past the second extension line. The arrowheads
need two lengths and two gaps between the extension lines, 6.25 with those sizes (outside at 6.24, inside at 6.26), and
words their width and a gap each side, above the line as on it. The line starts
at the back of each arrowhead, 2.5 in from its tip, but for the ticks, Dot small, Dot small blank, Integral and None,
which take no room and never move out. Words on the line (DIMTAD 0) fit between the arrowheads with two gaps each side:
"9" of ghsimplex, 1.548 wide, moves out of a dimension of 9. Under DIMATFIT 3 words wider than the arrowheads' room stay
inside and the arrowheads go out: "12" of ghsimplex widened 1.5 times, 5.536 wide, stays between extension lines 12
apart. DIMTVP lifts the words of DIMTAD 0 by that many times their height, the line
broken about them below 0.7; a DIMGAP below nought draws a box round the words, the gap clear of them.

DIMTMOVE says where words go that the fit moves out or DIMTEDIT puts by hand. Under 0 they stand beside the line, which
DIMTEDIT moves to run through the point given, the words above it there (on it for DIMTAD 0, the line broken a gap
clear of them, and run on to a gap short of them past an end); kept level by DIMTOH past an end, they stand on a level
landing, or with none a gap past the end where the line is within 15 degrees of level, as AutoCAD 2021 draws them at 5,
10 and 15 degrees. Under 2 the fit puts them over the line, away from the
points, DIMEXE, one and a half DIMGAP and half their height out (their height more where they stand below it as they
read): "4" moved out of a dimension of 4 stands 5.9375 below the middle of its line; DIMTEDIT puts their middle at the
point. Under 1 the same place is a leader's end, the leader from the middle of the line a third as far along again, and
a landing on to the words. Words DIMTOH keeps level stand DIMEXE, half DIMGAP and their height out instead, square off
the middle of the line and level at any angle of it: that "4" 6.5625 below, and the "0.5000" of a continued dimension
of 0.5 in acad.dwt's Standard on a leader straight up from the middle to 0.4088 (Arial's 0.1838 high words, acad.dwt's Standard text style being Arial), a level
landing two gaps long on away from the line, the words a gap past it; DIMTEDIT draws a leader to words further from the line than DIMGAP and half the heights of
the words and the arrowheads, 3.15 with ISO-25's sizes, the words starting at the point, and none to words nearer.
DIMTEDIT Left and Right put the words an arrowhead and a gap in from the extension line on that side as they read (the
top of a vertical line is its left), with a gap about them: "100" of Arial on a line at 30 degrees stands 6.733 in. Under
DIMTIH they stand level, centred on the line, which is broken about them: 6.895 in on that line. Under DIMTMOVE 1 words
put past an end of the line, on its way, have the line run on from the end to them, to the near edge of words on it and
to the far edge of words above it, a gap past each: "100" 5 wide put 20 past the end of a line 100 long has it run on to
116.875 on it and 123.125 above it.
Moved words are drawn by AutoCAD 2021's rules for an ordinate dimension too: DIMTEDIT runs its leader to the words, a gap
short of them on it (DIMTAD 0) or under them where they stand beside it, jogged where the point is to one side of the
feature: the point (1200, 0) measuring 1200, its words moved to (1208, 50), has
its leader up to 35.764, across 8 as it rises 5, then 5 more to 45.764; the first leg is never shorter than two
arrowheads (words moved to (1192, 15): up to 5, across, up 5 more).
Under DIMTMOVE 2 the leader stays where it ends and does not run on under the words.

DIMLINEAR asks `Specify dimension line location or [Mtext/Text/Angle/Horizontal/Vertical/Rotated]:` and DIMALIGNED
`[Mtext/Text/Angle]`, as AutoCAD 2021: Mtext and Text ask for the words, Angle turns them (`Specify angle of dimension
text:`), and Rotated sets the line's angle (`Specify angle of dimension line <0>:`): 0,0 to 100,0 rotated 30 measures
86.6025. Words turned by Angle stand centred on the line whatever DIMTAD says, the line broken where their turned box
and the gap cross it: "100" 5.25 wide and 2.5 high turned 45 breaks it 2.65 each side of the middle. Every dimension
made writes the middle of its words (group 11) and its measurement (42), as AutoCAD writes them with its block; words the
fit puts past a leader under DIMTMOVE 1 are written by their start, level with their middle, as AutoCAD 2021 writes
them: that "0.5000" of a continued dimension of 0.5, its middle at (100.8933, 20.4088), as (100.52, 20.4088).
DIMTFILL draws a mask behind the words, the MTEXT's background 1.25 times their height, of the drawing's background for 1
and of DIMTFILLCLR for 2; DIMTXTDIRECTION, words read from right to left, turns them half round where they stand, a
level dimension's to 180 degrees and an upright one's to 270, as AutoCAD 2021 writes them into its block; under DIMTMOVE
1 a leader's words stand on the other side of their point.

Radius, diameter and angular dimensions follow the same variables. A radius of 20 with its line put 45 from the centre
has its arrowhead outside the rim, its line from the back of it on under the words, and a line from the centre to the
rim by DIMTOFL, or with DIMTOFL off the centre mark of DIMCEN (centre lines too where DIMCEN is below nought); put 8 from
the centre, the arrowhead stands inside and the line runs in past the words. Inside a small circle DIMATFIT 3 keeps the
words where they are put, no nearer the rim than two arrowheads and the gap; 0 to 2 put them in the middle of the line
where they fit, else outside past the rim next to it, however far out the line was put: "R20" put 35 out stands 8.75 from
the centre. DIMTIX puts the words outside next to the rim wherever the point was under DIMATFIT 3 (and under DIMTMOVE 1,
whatever DIMATFIT), a diameter's far arrowhead outside with them and DIMTOFL's line through, or without DIMTOFL the
centre marked; level words are decided by DIMTIH there and stand on a landing an arrowhead long: with Arial 2.5 high,
"R20" put 40 out at 30 degrees stands 28.6 out, its line from 22.5 to 32.5. Under DIMATFIT 0 to 2 (and 0 to 2 with DIMTMOVE 2)
DIMTIX keeps the words inside however tight the radius, the arrowhead going out on its short line where they leave it no
room: a radius of 5 has its line whole to the rim and the arrowhead outside, and with DIMTOFL off no line and the
centre marked. On the line (DIMTAD 0) a radius's words take the arrowhead and two gaps more to fit. A diameter's words on its line (DIMTAD 0) take a gap each side and, with
the arrowheads inside, the arrowheads' room too: "%%c6" 4.357 wide fits a diameter of 6 under DIMATFIT 1, the line
through the circle broken a gap clear of it. An angle's extension lines run from DIMEXO off its points to DIMEXE past
the arc wherever that is any length: points 20 out and the arc 20.3 out give lines from 20.625 to 21.55. An angle of 60
degrees with its arc 30 from the vertex has its arc from the back of one arrowhead to the back of the other, 4.78 to
55.22 degrees, the arrowheads set along their chords; an angle of 8 degrees has its words and arrowheads outside past
its first ray, the arc run on and a line along it to the words, or under DIMTMOVE 1 and 2 over the arc, out along the
middle of its sweep: under 1 past a leader from the arc to two arrowheads beyond it (beyond the farthest point where
that is further), under 2 their middle there, or where the points lie beyond the arc where the words first fit between
the extension lines. DIMJUST 1 puts an angle's words next to its second ray, 2 next to its first. Words of an angle moved by DIMTEDIT stand
on the arc, which then runs through the point given, under DIMTMOVE 0 ("60°" moved 50 out at 30 degrees stands 51.9
out, above the arc as it reads), level past a leader from the middle of the arc under 1, and turned along the arc under
2. Those of a radius or a diameter slide along its line, which stays, to where the point falls on it under 0 and 1, and
stand at the point under 2, but under DIMTMOVE 0 and DIMATFIT 0 to 2 DIMTEDIT turns the line toward the point: "R20" of a
circle of 20 put 35 out at 30 degrees, moved to 15 out at 120 degrees, has its line at 120 degrees, its words 15 out on
it with the line whole to the arrowhead; moved 25 out they stand 28.6 out, the arrowhead outside (inside under DIMATFIT 2,
the words then 26.25 out). Under DIMATFIT 3 words moved within the room next to the rim are pushed out and written 36, those
inside held back from the rim as they are put there (15 out stands 11.25 out), group 11 the point of the line they stand by.
A radius made under 1 or 2 keeps its line through the point the circle was picked at. Words of an angle moved under
DIMTMOVE 1 or 2 onto the arc break it about them, as words in the middle of it do, and under 0 send the arrowheads outside
with the arc drawn whole where they cut into an arrowhead's room ("60°" put 6 degrees from the first ray of an angle of
60 at 35 out); under 1 their leader is drawn only past a distance from the arc, 6.875 with arrowheads and words 2.5 high and a gap of
0.625, fitted to what AutoCAD 2021 draws.
A radius or a diameter put inside under 1 keeps its words as DIMATFIT 3 does, whatever DIMATFIT; under 2, words on its line over a
radius's centre or over the rim, from inside or out (a diameter's from the back of its arrowhead in), put the arrowheads
outside, pointing in, each on a line an arrowhead long, and DIMTOFL runs the line whole from the centre, or the far rim,
to the rim, else DIMCEN marks the centre: "R20" put 1 out under ISO-25, and in acad.dwt's Standard "R20" put 19.4 to
20.6 out. Put 8 out they leave the arrowhead inside and the line broken about them. DIMTIH keeps words put under 2 level,
inside the circle or out, whatever DIMTOH says, and the line is broken where it runs through their box grown by DIMGAP:
"R20" of Arial 0.18 high put 10 out at 30 degrees breaks it from 9.636 to 10.364. Words outside kept level on a line
within 15 degrees of level stand on no landing of their own, a gap past the end of the line, as on a linear dimension.
Words put past the rim but within the room of the arrowhead stand where the fit would put them: of a radius or a diameter
under DIMATFIT 3 (and 0 to 2 for a diameter, as for a radius) they stand no nearer the rim than two arrowheads, the gap and
half their width, or two arrowheads where DIMTOH keeps them level on a landing: "R20.0000" of Arial put 20.2 out from a
circle of 20 in acad.dwt's Standard stands 20.979 out whether it was put 20.2, 20.5 or 20.9 out, and put 21 out stands at the
point. A point on the rim is outside it. Under DIMTMOVE 2 words on a radius's line put over its centre push the
arrowheads out when their near end is within an arrowhead of the centre. On a line exactly level the words DIMTOH or DIMTIH
keep level are the words that stand along it, with no landing; on an exactly upright line the landing runs left where the
line runs down and right where it runs up, outside the rim and in: "R20" put 25 out straight down has its landing from
(-0.18, -25) to (0, -25) and its words' middle at (-0.7994, -25). A linear or aligned dimension whose points run straight
down does the same for level words outside its line.
A radius or a diameter records its words (group 11) as AutoCAD 2021 does: under DIMTMOVE 1 and 2 by the point they were
put at, under 0 by the point of the line or landing they stand by, their middle without DIMTAD's lift: "R20" put 40 out
at 30 degrees under DIMTAD 1 writes (34.641, 20), its middle 0.18 above the line.
A radius or a diameter is written with a leader of nought (group 40) and, where its words stand at the point it was put
at, as one put by hand: group 70 of 164 for a radius and 163 for a diameter (flag 128), group 11 the point they were put
at. Where the fit or the room next to the rim put them, group 70 is 36 and 35 and group 11 the middle of the words as
laid out. That is DIMATFIT 0 to 2 under DIMTMOVE 0, DIMTIX, and a point put past the rim within the room of an arrowhead
(DIMTMOVE 0 or 1): in acad.dwt's Standard with DIMTAD 1, "R20" of a circle of 20 about the origin put at (35, 12) writes
164 and (35, 12) under DIMATFIT 3, 36 and (9.3153, 3.3861) under DIMATFIT 0, and put at (20.3, 0.1) writes 36 and
(20.9782, 0.2852). AutoCAD's own regeneration of such a file keeps the words where they stand, as the presenter draws
them. DIMTEDIT's Home on a radius or a diameter writes 36 and a leader of nought, and sets the words as DIMRADIUS does
for a point on the rim.
Level words (DIMTAD 1 or 4) off a line that runs down keep the room of one more line of words, their height, past two
arrowheads, outside the rim and in: "R20" of Arial 0.18 high, a circle of 20, put 20.3 out straight down has its landing
20.5438 out, not 20.36, and put 19.6 out 19.4562. Level words on a line within 15 degrees of level (but not exactly
15 or exactly level), inside as outside, have no landing: "R20" put 14 out at 3 degrees in a circle of 20 about the
origin, DIMTIH on, DIMTAD 0, has its line end at the point itself, (13.9808, 0.7327), which is group 11, and its words'
middle (13.3614, 0.7327), a gap and half their width past it.
The words are measured in the font of the dimension's text style, laid out as the canvas lays out an MTEXT, as AutoCAD
measures them: "R20" of arial.ttf 2.5 high is 5.965 wide and 2.553 high, "100" of ghsimplex 6.071 wide and widened 1.5
times 9.107. A TrueType font is read from the Fonts folders of Windows, an SHX font where the fonts set on the document
find it, the canvas's own ghsimplex wherever fonts are on; the style's width factor and oblique angle count. An SHX
letter is measured as AutoCAD measures its ink: an arc of less than a half circle reaches the rectangle of its chord
and of the line along it touching the arc, a longer one the square about its circle, so that isocp.shx's 3 is 0.441421
of the height wide where its strokes are 0.4; where the pen is put down counts though it moves no further (monotxt.shx's
1, half the height wide); ink left of the pen, as italic.shx's f, does not count. Words whose ink has no width, as "I"
of isocp.shx, are nought wide; words of a font not found take 0.7 of their height a letter. DIMARC (DAR) dimensions the
length of an arc: below 90 degrees of sweep its extension lines run along the middle of the arc, from 90 up from the
centre; the arc symbol stands before the words (DIMARCSYM 0), over them (1) or nowhere (2); Partial measures between two
points of the arc, and Leader, offered past 90 degrees, draws a leader from the words' arc to the arc measured. A point
in the sector the arc does not sweep dimensions the rest of its circle. DIMORDINATE
(DOR) measures a point from the origin of the UCS along X where its leader runs up or down, along Y where it runs aside:
(300, 40) with its leader ending at (310, 80) is an X datum of 300, the leader jogged two arrowheads before its end.
Under DIMFXLON its first leg runs DIMFXL up to the jog, no nearer the point than DIMEXO: a jog at 30 and DIMFXL 10 start
it at 20.
DIMCONTINUE, DIMBASELINE and QDIM's Ordinate go on from an ordinate dimension, each leader ending level with the last.
DIMJOGLINE (DJL) puts a jog in the line of a linear dimension, kept as AutoCAD keeps it (ACAD_DSTYLE_DIMJAG_POSITION) and
drawn 1.5 times the height of the words; Remove takes it out. DIMEDIT Oblique slants the extension lines of a linear or
an aligned dimension to the angle typed, from the x axis, and keeps it as AutoCAD 2021 keeps it, from the dimension line
(group 52, Oblique): 60 typed for a dimension rotated 30 is kept as 30, for an aligned one from (0, 100) to (100, 150) as
33.435, and -30 for a level one as 330. DIMBREAK keeps its breaks as AutoCAD 2021 keeps them, a BREAKDATA under
ACAD_BREAKDATA in the dimension's extension dictionary: Auto one break for each object that crosses the dimension, naming
it (the object naming the BREAKDATA among its reactors), Manual the two points picked and the line they break (0 and 1
the extension lines, 3 the dimension line); Remove takes it out. Where the style keeps no break size, a break is 0.125
wide, or 3.75 in a drawing of MEASUREMENT 1, times DIMSCALE, as AutoCAD 2021 breaks it. A dimension drawn anew, by DIMEDIT, a change of its
style or a grip, is broken again where it keeps breaks, whether DIMBREAK here or AutoCAD broke it, and AutoCAD 2021 opening
the drawing breaks it again from what the presenter kept: a dimension broken here by hand from (20, -130) to (30, -130),
given new words in AutoCAD, has its line from 0.18 to 20 and from 30 on. DIMJOGGED (DJO, JOG) draws a jogged radius of an arc or a
circle, its line from a centre put in place of one off the drawing: along the radius's parallel, across at DIMJOGANG and
on to the rim, the jog half way between the two lines. A circle of 50 about the origin, its centre drawn at (0, -100),
its words put at (30, 0) and its jog at (15, -50) has its lines from (0, -100) to (65, -100), on to (-35, 0) and on to the
arrowhead at the rim. Words put on the other side of the rim from the foot of the centre drawn go three quarters of the
way from that centre to the rim, as AutoCAD 2021 puts them: put 80 out at 14 degrees they stand at (36.38, -15.905). Its
grips are the chord point, the centre drawn, the jog and the words. DIM dimensions what is clicked: a line, then where its
dimension goes or a second line for the angle; a circle's diameter, an arc's radius; a click on nothing starts a linear
dimension. Its options run the commands of their names, and Layer puts the dimensions made next on a layer. DIM asks as
AutoCAD 2021's own DIM does: a circle offers Radius or Diameter and no angle ("Specify diameter dimension location or
[Radius/Mtext/Text/text aNgle/Undo]"), an arc also Angular; Continue asks "Select extension line origin to continue or
[Baseline]" and Baseline "Select extension line origin as baseline or [Continue]" (each turns into the other), then
"Specify second extension line origin or [Select/Undo] <Select>", with Offset for the spacing of a baseline row; the
row goes on from the extension line picked, where DIMCONTINUE and DIMBASELINE go on from the last dimension.
A new dimension is drawn by the drawing's current dimension variables (its header's), not its style's, and writes each
that differs from the style as an override, in the order of their groups, as AutoCAD 2021 does: DIMSCALE 10 typed with
ISO-25 current draws the words of a dimension 25 high and its arrowheads 25 long, and DIMBASELINE puts each next line
37.5 further out; a drawing saved with DIMASZ 5 and DIMCLRD 1 over ISO-25's 2.5 and ByBlock draws arrowheads 5 long and a
red line, and writes `1070 41, 1040 5` then `1070 176, 1070 1`. Where the drawing's DIMSTYLE names no style of it, the
dimension is drawn by the header all the same, in the style fallen back on (Standard, else the first), each variable other
than that style's written; a leader is compared with the same style. DIMSCALE 0 draws at 1 in model
space and, through a viewport, at one over the viewport's scale, as a leader does: 2 through one of 1:2, written as
`1070 40, 1040 2`, every kind of dimension alike. As AutoCAD 2021 (DIMCONTINUEMODE 1), DIMCONTINUE and DIMBASELINE draw
in the style of the dimension they go on from, with its overrides and its DIMSCALE, on its layer, whatever style, scale
and layer are current and whatever DIM's LAyer names: based on a dimension made with DIMASZ 4 over ISO-25, a dimension
writes `41` again though DIMASZ is back to 2.5, and based on one made on L1 at DIMSCALE 2, it goes on L1 and writes
`1070 40, 1040 2`, DIMBASELINE's line twice DIMDLI further out. DIMCONTINUE draws each linear
and angular dimension with DIMTMOVE 1 where that is nought and writes it first (`279 41` from that base): words too wide
for a continued dimension of 0.5 stand over its line past a leader from its middle. DIMBASELINE adds nothing of its own,
nor do the rows of ordinate dimensions and QDIM; a style that holds DIMTMOVE 1 already gets no `279`. DIMCONTINUEMODE,
kept by the session and 1 as it starts, set to 0 draws the rows in the current style, overrides and DIMSCALE on the
current layer, whatever DIMLAYER or DIM's LAyer names, as AutoCAD 2021 does: from that dimension made on L1 at DIMSCALE
2, with L2 current at 1, DIMCONTINUE goes on L2 writing `279` alone and DIMBASELINE writes nothing, its line DIMDLI
further out. DIMRADIUS and
DIMDIAMETER write DIMUPT 1 (`1070 288, 1070 1`) where the style has it off, and DIMJOGGED writes DIMJOGANG whatever the
style has (`1070 50, 1040 45`), so that a change of the style's jog angle leaves it as it was made.

A dimension made with DIMASSOC 2 (the default) whose points were picked with an object snap is associative, as in AutoCAD
2021: DIMLINEAR, DIMALIGNED and DIM tie each origin snapped with ENDpoint, MIDpoint, CENter, QUAdrant, NODe or NEArest to
its line, arc, circle, polyline segment or point (both ends where a line is selected), and one snapped with INTersection to
the two lines, arcs, circles or polyline segments that cross there. An end where several lines, arcs or polylines meet is
tied to the one drawn last, wherever the cursor stood, as AutoCAD 2021 ties it: the corner (0, 0) of a line to (100, 0)
and a line drawn after it up to (0, 100) is the start of the second. A point snapped with PERpendicular (to a line, an
arc, a circle or a polyline segment; on a line or a straight segment the foot may lie past its end, on the line run on,
as AutoCAD 2021's snap gives it, while an arc gives none off its run) or TANgent (to an arc, a circle or an arc of a
polyline) is tied with the point it was drawn from, as AutoCAD writes it: from (20, 120) square to (100, -20)-(100, 60)
the first origin is (100, 120), tied at parameter 1.75; past the end of a polyline's segment the foot is tied to that
segment. The second origin is drawn from the first, the ends of an angle of three points from its
vertex, and the first origin, the vertex and an ordinate's feature from the last point given (LASTPOINT), which
`CadPrompt.SnapFrom` hands the window's snap. An ordinate's feature keeps no last point, as AutoCAD keeps none. A
segment of a polyline is named by its marker, 1 for the first, and a point on an arc of a polyline by its angle counted
on from the arc's start: a polyline from (0, 200) to (100, 200) and round a half circle to (100, 300) has the end of the
half circle at marker 2 and 7.85398, its center at 4.71239. When the object moves, a tangent is drawn again from its
point, and so is a perpendicular whose point is tied to an object; one drawn from a point tied to nothing keeps its
parameter on the object, or, where that runs past the end of its line, goes to the foot from the origin of the world, as
AutoCAD 2021 moves it: DIMALIGNED from (0, 0) perpendicular to (100, -20)-(100, 60) measures 100, and 120 once that line
is moved 20 to the right; a first origin drawn from (37, 123) square to that line, at parameter 1.7875, goes to (120, 0)
when it moves by (20, 7). DIMRADIUS and DIMDIAMETER tie the center of the circle or arc, DIMANGULAR
of an arc its two ends and center, of two lines the four ends (the line selected second written first, groups 13 and 14,
as AutoCAD writes it), of three points those snapped, DIMARC the ends of the length (the arc's start and end, its first
and second points even where it measures the rest of the circle, or the points of a Partial length in the order given, as
nearest points at their angles) and the arc's center, DIMORDINATE its feature. Three points of an angle along one ray
measure nothing, the arc's point on the ray as far out as the point given wherever that was put, as AutoCAD measures
them; an angle whose legs come to lie along one ray with its arc on them measures nothing too, and the whole turn where
its arc lies off them.
DIMANGULAR of a circle measures from the point the circle was picked at, about its center, to a second point
(`Specify second angle endpoint:`), both brought onto the circle, and ties them as AutoCAD 2021 does: the first and the
center to the circle, the second as a nearest point of what it was snapped to, a line at the parameter of the point on
the circle run on, another arc or circle, an arc of a polyline (its segment the marker) or a POINT at nought, the circle
itself or nothing at its angle on the circle. A circle of 25 about
(200, 0) picked at (225, 0), with the second point the end (300, 40) of a line from (260, 40), measures 21.8 degrees to
(223.212, 9.285), the second point tied at -0.9197 along the line. When the objects move, a second point so tied past
the end of its line, or to a POINT, is dropped and stays where it is, as AutoCAD drops it; one within its line goes to
its point on the line. The arc of any angle goes through a point a third of the way round from the leg it starts at, as
far out as the point given, as AutoCAD writes it (put at (50, 550) in the right angle at (0, 500), it passes (61.237,
535.355)), and an angle of three points that follows its objects keeps its arc that far out at the same share of the
angle, inside the legs where it measured less than half a turn and outside where it measured more;
DIMCONTINUE and DIMBASELINE tie their first point to what the last dimension's second (or the base's first) was tied to,
and QDIM the ends of lines, the vertices of polylines and the centers of circles, all kept in a DIMASSOC as AutoCAD keeps
it. Every edit
that changes those objects, whatever command or grip makes it, carries the dimension with it in the same undo step: a line
from (0, 0) to (100, 0) moved by (10, 5) takes its dimension, 20 above, whole to (10, 5), (110, 5) and a line at 25; its end
stretched to (120, 30) moves the second origin there and measures 120, the dimension line staying 20 up, as STRETCH moves
the origins and the line point of a linear dimension inside its window as AutoCAD's does; the same end dragged by its grip,
the dimension not stretched with it, leaves the dimension line 20 beyond the origin nearer it, at 50, as AutoCAD 2021
leaves it (30 for an end at (120, 10), 20 for one at (120, -30)). Where that would put the line nearer the other origin
than it keeps from the nearer one, it goes as far from the other instead, the same way, as AutoCAD 2021 puts it:
origins (0, 0) and (60, 40) with the line between them at 20, the first moved up to (0, 5), put the line at 60; of two
origins as far from a line between them, the one it lies across from counts as the nearer, else the first. STRETCH of a
polyline's vertex
keeps the height of an arc off its chord, as AutoCAD's does: the half circle from (100, 200) to (100, 300) whose end goes to (100, 320) takes a bulge
of 100 / 120, its center at (89, 260); a circle of 25
scaled twice about its center gives its radius 50 drawn out the same way, its words where they stood, as AutoCAD keeps
them; words that grow or shrink by a digit keep their end on the leader's side, "R9.0000" outside to the right of its
circle becoming "R13.5000" moving its middle 0.07 to the right, as AutoCAD moves group 11. An arc length dimension keeps
its arc as far out, a third of the way along the arc measured, its words in the middle, as AutoCAD moves it with its arc
moved, turned, scaled or lengthened; the ends of its leader (16 and 17) and its angles (40 and 41) stay as AutoCAD leaves
them. Each dimension that follows writes the middle of its words (group 11) and its measurement (42) anew, as AutoCAD
writes them when it updates one. An aligned dimension keeps its line as far from
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
AutoCAD's PLINETYPE 1 and 2 make it; Multiple leaves it a POLYLINE, as AutoCAD does. PEDITACCEPT 1 turns a line or an arc
into a polyline without the question, and Multiple's `Convert Lines, Arcs and Splines to polylines [Yes/No]?` is not
asked. PLINETYPE 0 makes PLINE, POLYGON, DONUT and PEDIT's lines and arcs old-style POLYLINEs and leaves a POLYLINE
edited alone one, as AutoCAD 2021 does at 0: PLINE from (0, 100) to (100, 100) and, 2 to 4 wide, to (100, 200) is a
POLYLINE whose second vertex has widths 2 and 4. RECTANG and REVCLOUD make LWPOLYLINEs whatever it is, as AutoCAD's do.
Both are kept by the session, PEDITACCEPT 0 and PLINETYPE 2 until set. Edit vertex marks the vertex with a cross,
the first one the view shows (`No visible vertices` where it shows none); Next and Previous stop at the ends, Insert puts a
vertex after the marked one as wide as its segment ends and marks it, Break between two vertices of a closed polyline
leaves two open ones, and Straighten at one vertex straightens the segment that leaves it. Multiple joins within a fuzz
distance by Extend, Add or Both: the lines (0, 0) to (100, 0), (103, 0) to (103, 50) and (100, 50) to (0, 52) joined at 4
by Both are one polyline through (103, 0) and (103, 49.94), "2 segments added to polyline". Extend runs an arc at an end
round its circle, on or back, to where the other end segment meets it, of two crossings the one nearer the two ends
together: the arc about (0, 0) of radius 100 from 0 to 80 degrees and the line from (10, 130) to (10, 200) meet at
(10, 99.4987), the arc's bulge 0.38517039, as AutoCAD 2021 joins them; an arc whose circle nothing crosses is not
extended, and Both adds a segment there. Extend joins two ends where they meet within the fuzz of both, however far
apart the ends are: (100, 0) and (130, 30), 42.4 apart, meet at (130, 0) at a fuzz of 35. A run is one undo step, and
Undo takes back its last option.

A 3D polyline picked is edited with AutoCAD 2021's options for one,
`Enter an option [Close/Join/Edit vertex/Spline curve/Decurve/Reverse/Undo]:` (Open where it is closed), and its vertex
options `[Next/Previous/Break/Insert/Move/Regen/Straighten/eXit] <N>`. Spline curve fits it as SPLINETYPE and SPLINESEGS
ask: the 3DPOLY (0, 0, 0), (100, 0, 10),
(100, 100, 20), (0, 100, 30) is the first vertex of the frame, the 9 of the curve through (75, 50, 15), then the rest of
the frame, and closed it is fitted again round the frame, 32 vertices of the curve, as AutoCAD stores them. Reverse turns
the frame round, the first vertex of a closed one too, and fits it again; Decurve straightens it; a vertex moved or put in
lies at nought, and a fitted polyline is fitted again on its frame. Join asks the objects and adds none, as AutoCAD 2021's
console did for lines and 3D polylines meeting an end. Multiple leaves 3D polylines out.

REVCLOUD draws a cloud round a rectangle (Rectangular, the type it starts with), a polygon (Polygonal), the cursor's path
(Freehand), or an object picked, which it replaces (Object: a line, arc, circle, ellipse, polyline or spline). The cloud is
a LWPOLYLINE of arcs of 110 degrees (bulge 0.52056705) bulging out, their vertices an arc length apart along the path, the
corners not kept; a last arc shorter than half an arc length is taken into the one before. A circle of radius 20 at an arc
length of 5 is the 25 vertices AutoCAD 2021 makes, to 8 places, and the outline 60 by 40 at 7 is 29, from (0, 0) to
(0, 4). Arc length asks AutoCAD 2021's one approximate length, `Specify approximate length of arc <15.0000>:`, kept by
the session as REVCLOUDAPPROXARCLEN and not scaled by DIMSCALE, as in 2021, which has no REVCLOUDSCALEMODE. At
REVCLOUDARCVARIANCE 1, as AutoCAD starts, each arc is drawn between two thirds of it and four thirds, and the first prompt
says so, `Minimum arc length: 4.0000   Maximum arc length: 8.0000` for 6; at 0 each arc is the length, as the clouds above
were checked. REVCLOUDCREATEMODE sets the type it starts with. Calligraphy widens each arc from nought to a tenth of the
longest arc, and Modify redraws the side of a closed cloud picked along a new path. A drawing whose REVCLOUDAPPROXARCLEN
is nought starts at 15 in millimetres and 0.5 in inches; AutoCAD 2021 sizes its first cloud by a share of the view's
diagonal that its help does not give, so that start is not AutoCAD's.

TABLE (TB) shows the Insert Table dialog ([`CadInsertTableRequest`](xref:GeometryHelper.Dxf.Viewer.CadInsertTableRequest)),
asks `Specify insertion point:` (or two corners for Specify window) and opens the title for typing in the MTEXT editor:
Tab goes to the next cell, Shift+Tab to the one before, Enter to the one below and Esc ends. Tab in the last cell adds a
row under it, as AutoCAD's table editor does, of the cell styles and the height of the last row and empty, and goes to its
first cell; the row is an undo step of its own. While a cell is typed in, its row grows to hold the lines as they come and
shrinks back as they are taken out, never below the height it stood at, and the rows under it move down with it, as
AutoCAD's in-place editor shows them: in AutoCAD's Standard style a data cell typed with two lines makes its row 15.07
high instead of 9, with three lines 22.57, and the editor stands where the cell now is. The table is drawn anew from a
preview of the words ([`CadTableLive`](xref:GeometryHelper.Dxf.Viewer.CadTableLive)) only while a row differs from what it
stood at, and costs one cell's layout a key (1.4 ms in a table of 50 rows by 20 columns, against 89 ms to lay the whole
table out); nothing is written to the drawing until the editor closes, which is one undo step with the rows the preview
showed, and Esc leaves the table as it was. Five columns 63.5 wide and
four rows of data of AutoCAD's Standard make a table 317.5 wide and 56 high, its title 11 high and its other rows 9.
A table is drawn with its first row in the Title cell style of its table style, its second in the Header and the rest in
the Data, each look a cell does not give of its own; where the table or its style leaves the title out, the first row is
the header, and where the header is left out too, every row is data, as AutoCAD 2021 reads such a table.
-TABLE asks the columns, the rows of data and the point (Style, Width, Height) on the command line, and TABLEDIT a cell
to type in; a double click or F2 on a cell runs it. TABLESTYLE (TS) shows the Table Style dialog
([`CadTableStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableStyleEditor)); a style changed draws its tables anew. A
click in a cell selects it with the Table Cell tab ([`CadTableEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableEditor)):
rows and columns put in and taken out, cells merged and unmerged, their alignment, fill and borders, the arrows moving
the cell and Delete emptying it, each one undo step that draws the table anew in its block. A number typed in a cell is
kept as a number, as AutoCAD 2021 reads a text it parses: 1 is the whole number 1 and 007 the whole number 7, 1.5 a
number shown in the drawing's units (1.5000 at LUNITS 2 and LUPREC 4, 1.50 at LUPREC 2), 1e3 one shown 1.0000E+03, and
1/2 one of 0.5 shown 1/2 (1-1/2 and 1 1/2 shown 1 1/2, 1/3 shown 5/16 at LUPREC 4), each set top right where the cell has
no alignment of its own; 1,5 is the point (1, 5, 0), shown 1.0000, 5.0000, 0.0000. The autofill counts each on in its
format, 1.5 to 2.5000, 3.5000 and 4.5000 and 1/2 to 1 1/2, 2 1/2 and 3 1/2, and copies a point. A number typed over a
number keeps the cell's kind and format, as AutoCAD keeps them: 12 over 1.25 is shown 12.0000, and 1.5 over the whole
number 12 is the whole number 1. Thousands typed with commas, 1,500, are a number of the thousands format (the whole
number 1500 of `%th[\,]` shown 1,500; 1,500.5 shown 1,500.5000; a comma before other than three figures, as 1,50, is a
point), and a length in feet and inches, 12'6", 12'-6", 12'6-1/2" or 6 1/2", is a number of inches (12'6" the number 150
shown 12'-6"; 6.5" and 12.5' a number of the engineering units, shown 0'-6.5000" and 12'-6.0000"), each set top right;
a length with a space before its figures, 12ft and 12"6 are not read as lengths and stay text. Dates and times (1/1/2020,
12:30) stay text, the library writing no date. The editor opens a number as AutoCAD 2021's
`GetTextString(FormatOption.ForEditing)` writes it ([`DxfTable.TextStringForEditing`](xref:GeometryHelper.Dxf.DxfTable.TextStringForEditing(System.Int32,System.Int32))),
the number as it is to 16 figures with its zeros and the places of its units left off: a cell that shows 1.5000 opens as 1.5,
a third as 0.333333333333333, 1e3 as 1.000000000000000E+03, 1/3 as 10923/32768, 12.5 in feet and inches as 1'-0 1/2" and a
point as 1.5, 2.25, -0.1; the words that opened are kept when they are given back. A text typed over a cell of a number
or a point is refused, the message "Cell C3 holds a number, and "abc" is not a number." and the cell left as it was, as
AutoCAD 2021's SetValue of it throws eInvalidInput (12mm, 1/1/2020 and 12:30 too, and a number over a point or a point over a number);
empty words still clear the cell. In a cell that holds no number, b1 and 12mm stay text, as does a number in a cell made to
hold text. Drawn anew,
each row grows to
hold its cells, as AutoCAD lays a table out: a cell's words wrap at the cell's width less its margins, as an MTEXT of that
width, and need their actual height and the margins above and below, one line of four thirds of the text height at
least; a row is as high as its tallest cell needs and is never made lower. In AutoCAD's Standard, "HEA300 S355 with end
plates both sides" in ghsimplex in a column 30 wide takes six lines and makes its row 45.43 high, as AutoCAD 2021 laid it
out, and the row keeps 45.43 when the words are made short again. A cell written in a table of 100 rows of 20 columns
is written and drawn in about 50 ms.

While cells are selected the table is framed by its column letters (A, B ... Z, AA) and row numbers, those of the
selection on amber, as AutoCAD 2021 draws them with TABLEINDICATOR 1; TABLEINDICATOR 0 hides them, and the variable is
kept on the machine, as AutoCAD keeps it in the registry. Their fill is Table Indicator Color, on the right-click menu of
a whole table and in Global settings › Appearance. A press in a cell dragged across others selects the range from it to
the cell under the cursor, held at the table's edge off it and grown to every merged block it meets, so that a range
touching the title takes every column. The cells carry a grip at the middle of each side and the autofill diamond at
their bottom right corner. An edge grip moves that line of the table: the column left of it (or the row above it)
changes alone and the table grows or shrinks with it; no column goes narrower than AutoCAD's minimum, its two margins and
its tallest text (9 in Standard), nor a row lower than its text needs. The autofill diamond dragged down, up, right or
left fills the cells passed over from the selection ([`CadTableDrag`](xref:GeometryHelper.Dxf.Viewer.CadTableDrag)),
showing their coming values and the last one in a tip: a 1 dragged three cells down gives 2, 3 and 4, and dragged up
counts back, as AutoCAD 2021 fills them; a right click while dragging chooses Fill Series, Fill Series Without
Formatting, Copy Cells, Copy Cells Without Formatting or Fill Formatting Only. A whole table selected has its move grip at
the top left, a column grip at each line between columns, which moves the line with the table's width kept (with Ctrl
the column left of it alone changes and the table follows), stretch grips for its width, its height and both, and the
table breaking grip, drawn grey and not offered. A line dragged shows the new line dashed and "C width = 38.0000" or
"Row 3 height = 15.0000", or with dynamic input a field over the table (left of it for a row) that takes the size typed;
Esc drops it, and each drag is one undo step (Autofill, Column Width, Row Height, Table Size). The drags follow the
object snaps, ORTHO and polar tracking from the grip, as the other grip drags do: with Endpoint on, a column's line
dragged near the end of a line at 53 goes to 53, and with ORTHO the corner's stretch grip dragged right and a little down
stretches the width alone. A table whose rows flow up is worked the same way from its first row at the bottom: the line
over its fourth row dragged up 6 makes that row 6 higher, and a 1 dragged up three cells gives 2, 3 and 4. The
right-click menu of cells is AutoCAD's: Background Fill…, Alignment, Remove All Property Overrides, Edit Text, Delete All
Contents, Columns and Rows (Insert, Delete, Size Equally, Resize), Merge, Unmerge and Properties work; Cut, Copy, Paste,
Cell Style, Borders…, Locking, Data Format…, Match Cell, Data Link…, Insert, Manage Content… and Quick Properties are
grey. Columns › Resize and Rows › Resize start the drag of the selection's right (or bottom) line as though its edge grip
had been clicked: the line follows the cursor, and a click, Enter or a size typed makes it.

MLEADERSTYLE (MLS), and the last item of the multileader style box of the Annotate tab, show the Multileader Style
Manager ([`CadMLeaderStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadMLeaderStyleEditor)): Set Current, New…, Modify…
and Delete, which is refused for Standard, the current style and a style a multileader is drawn in. A drawing of no
multileader style is given Standard first, metric: arrows 2.5, a break of 3.75, a landing of 8, text 2.5 high and a
landing gap of 2. Modify writes the Leader Format, Leader Structure and Content tabs into the drawing's MLEADERSTYLE and
gives every multileader of the style its values, those it has of its own kept: a style's text made 2.5 high leaves a
multileader whose words were set 6 high at 6. The Symbol list of Leader Format shows each arrowhead with its picture, as
AutoCAD's list shows it and as the First, Second and Leader lists of the Dimension Style dialog show theirs, a user arrow
drawn from its block. MLEADER draws in the current style (CMLEADERSTYLE, else Standard): a
multileader of a style of words and arrows 4 high has words and arrows 4 high. Each change is one undo step. In a drawing
of R2007 or later with no style to draw in, MLEADER first makes Standard current, metric as above, or in an imperial
drawing (MEASUREMENT 0) AutoCAD 2021's own: text and arrows 0.18, a landing of 0.36, a gap of 0.09 and a break of 0.125,
in the same undo step. A style of no content draws the leader alone, asking no text, its dogleg from the landing away from
the arrow, as AutoCAD 2021 draws it. A style of block content (Type Block on the Content tab: Source block, one of the
drawing's own blocks, Attachment Center extents or Insertion point, Color and Scale) asks "Enter attribute values" and
each attribute's prompt with its default, "Enter tag number <TAGNUMBER>:", and places the block past the end of the
dogleg, its box met on the side towards the arrow: the circle of radius 5 of TAG, landed at (20, 10) with a dogleg of 0.36,
stands at (25.36, 10), as AutoCAD 2021 placed it. AutoCAD's own blocks of the list (Detail callout, Slot, Circle, Box,
Hexagon, Triangle) are not offered. Scale multileaders to layout and Specify scale are greyed while Annotative is ticked.

MLEADERSCALE is the scale of the multileaders drawn next, as in AutoCAD 2021: making a style current, by Set Current or
`CMLEADERSTYLE`, gives it the style's scale (nought for an annotative style), even for the style already current; set by
itself it leaves the style alone, so that with a style of scale 5 current, MLEADERSCALE 2 draws words 4 high at 8 while
the style keeps 5. Nought draws at 1, or through a viewport of 1:10 at 10; a value below nought says `Cannot set
MLEADERSCALE to that value.`, and a name of no style at CMLEADERSTYLE `Cannot set CMLEADERSTYLE to that value.`. A scale
other than the style's is the multileader's own, as AutoCAD writes it. The variable is kept in the drawing's
DICTIONARYVAR MLEADERSCALE, which AutoCAD 2021 reads (a `$MLEADERSCALE` of the header it ignores), and reads 1 in a drawing
that has none, whatever its current style's scale.

LEADER (LEAD) asks as AutoCAD 2021 does, on the command line: `Specify leader start point:`, `Specify next point:`, then
`Specify next point or [Annotation/Format/Undo] <Annotation>:`. Format makes the leader a spline or straight, with an
arrow or none; Undo takes the last point back. The words are typed line by line at `Enter first line of annotation text
or <options>:` and `Enter next line of annotation text:`, or Enter gives `Enter an annotation option
[Tolerance/Copy/Block/None/Mtext] <Mtext>:`: Copy makes an MTEXT of a text or an MTEXT picked, or copies an INSERT picked
to the leader's end, its scale kept and turned to the X axis of the UCS, with no hook (an INSERT at (500, 500) scaled 1.5
by 2.5 and turned 0.3 is copied to (10, 320) scaled 1.5 by 2.5 and turned 0, as AutoCAD 2021 copies it); a TOLERANCE
is copied there along the UCS's X, its insertion point on the end where the leader runs right and set back its first
row's width where it runs left, so that its right side meets the end: `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 in Arial copied
to a leader ending at (90, 105) stands at (89.07704, 105), as AutoCAD 2021 puts it, and a leader steeper than 15 degrees
takes a hook as words do: copied to a leader from (200, 40) to (210, 45), the frame stands at (210, 45) and the leader
runs through (209.82, 45), DIMASZ short of the end. Block places an INSERT as INSERT does, None leaves the leader
pointing to nothing, and Mtext opens the MTEXT editor; Tolerance shows the Geometric Tolerance dialog and puts its frame
at the leader's end as Copy puts a copied one, Cancel or an empty dialog leaving the leader pointing to nothing (with no
window to show the dialog, the option is asked again). While the points are picked the leader is previewed at the cursor, its arrowhead and its line or
spline as the leader made there draws them. The leader is drawn by the current dimension style and DIMSCALE: its MTEXT stands
DIMGAP past the last point, to the right where the last segment runs right or straight up or down, and a hook DIMASZ
long runs on to it where that segment is steeper than 15 degrees: a leader from (0, 0) to (10, 10) with DIMASZ 2.5 ends
(7.5, 10), (10, 10), its words middle-left at (10.625, 10) with DIMGAP 0.625, or bottom-left at (10.625, 10.625) with
DIMTAD 1, as ISO-25 has it. Every dimension variable other than the
style's is written as the leader's override, as AutoCAD writes them, in the order of their groups: DIMSCALE 2 gives `1070
40, 1040 2` and words twice DIMTXT high, and DIMEXO 0.5, DIMCLRD 1, DIMCLRT 2, the arrow `_Dot` and DIMLWD 0.30 give 42,
176, 178, 341 and 371 in that order. Words typed take DIMCLRT, the current colour where it is BYBLOCK. In a UCS other
than the world's, a moved one too, the last segment is measured along the UCS's axes, as AutoCAD 2021 measures it: the
words run along its X, DIMGAP along it from the end (and DIMGAP up its Y with DIMTAD 1), and a leader steeper than 15
degrees to that X takes its hook along that X, the hook's vertex DIMASZ short of the end; in a UCS turned 30 degrees a
leader from the UCS's (0, 0) to (10, 10) ends at the world's (3.66, 13.66), its hook's vertex at (3.50, 13.57) with
DIMASZ 0.18, its words 0.625 along the UCS's X from there. A frame on a leader to the left there is set back its width
along the world's X and the leader's end moved to the frame's right side, as AutoCAD 2021 does: from the UCS's (0, 400)
to (-10, 400), the end picked at the world's (-208.660, 341.410) moves to (-208.784, 341.872). QLEADER (LE) draws by the drawing's Leader Settings, which Settings or Enter at `Specify first leader point,
or [Settings] <Settings>:` shows where there is a window (with none, the command line says so and QLEADER draws by the
settings the drawing has). AutoCAD 2021 keeps them per drawing, in an XRECORD under the key `AcadDim` of the named object
dictionary with group 90 of 990106, and `CadQLeaderSettings` reads and writes that record as AutoCAD's QLEADER reads it
(checked in its console): 60 the annotation type, 61 the reuse (2 is Reuse Current, 340 the words kept), 62 the text on
the right side and 63 on the left, 64 Underline bottom line, 65 Spline, 66 No Limit, 67 the Maximum points, 68 Prompt for
width, 69 Always left justify, 70 and 71 the angle constraints, 72 Frame text, 170 the tab shown. QLEADER never writes it
itself; the dialog's OK writes it, and the arrowhead as DIMLDRBLK, an override of the current dimension style, in one
undo step. With the defaults it takes three points, asks `Specify text width <0.0000>:` and sets the middle of the words'
top line at the end on the right and of their bottom line on the left; DIMGAP and DIMTAD 0 are written as overrides on
every leader, as AutoCAD 2021 writes them. Each setting is followed as AutoCAD 2021 follows it: a row of the Attachment
tab hangs the words from their top, middle or bottom, `Middle of top line` half DIMTXT above the end; Underline bottom
line writes DIMTAD 1 and stands them DIMGAP above it; Frame text writes DIMGAP below nought; Always left justify asks no
width and sets the words' right edge DIMGAP short of an end on their right; a segment held to an angle takes the point
square onto the nearest ray, (100, 30) held level from the origin being (100, 0), and the preview at the cursor runs to
the point held, so that the cursor at (100, 30) previews the segment to (100, 0); Copy an Object stands the copy of words
by its middle and copies an INSERT or a TOLERANCE as LEADER's Copy does; Block Reference asks the block and INSERT's
prompts; Tolerance shows the Geometric Tolerance dialog after the points and puts its frame at the end as LEADER does;
None ends the leader with no annotation, and so do Tolerance's Cancel and an empty dialog; Reuse Next keeps the next
words or frame, the settings then turned to Reuse Current, which copies them to each later leader with nothing asked,
and copies an INSERT or a TOLERANCE kept to the leader's end as Copy does. In a turned UCS QLEADER raises its words up
the world's Y, as AutoCAD 2021 does: Middle of top line half DIMTXT above the end whatever the UCS. How far the words'
point is raised the leader writes as group 213, DIMGAP left out, as AutoCAD 2021 writes it in any UCS: (0, -0.09, 0)
for Middle of top line at DIMTXT 0.18, (0, 0.09, 0) for Middle of bottom line, their width along X as well where
Always left justify sets them back on the left, and none for the other rows, Underline bottom line and LEADER. Its angle
constraints are held there as AutoCAD 2021 holds them, the way the point was picked read along the UCS's axes but the
point laid off along the world's: in a UCS turned 30 degrees, the UCS's (310, 503) from its (300, 500) held level is 10
along the world's X, and its (305, 510) held to 45 degrees is (7.5, 7.5) on in the world. A leader to words, a frame or a
block made in a UCS other than the world's writes the UCS's X as its horizontal direction (group 211, (0.866, 0.5, 0)
in a UCS turned 30 degrees), one of no annotation the world's X. A leader arrow other
than the style's is written as the leader's DIMLDRBLK override, `1070 341, 1005` and the handle of `_Dot`. The
Arrowhead list of Leader Settings shows each arrowhead with its picture, drawn as a leader draws it, and ends with
`User Arrow...`, which opens AutoCAD's Select Custom Arrow Block over the dialog, Leader Settings dimmed and blocked
until it closes: an editable list of the drawing's blocks (all but layouts, anonymous blocks and external references),
A to Z whatever their case, the block in use as the arrow chosen when it opens, else the user arrow kept, else the first.
OK takes a name typed in either case: the block becomes the arrow, joins the list just above `User Arrow...` and is
written on OK of Leader Settings as DIMLDRBLK and as group 3 of the settings; a name the drawing has not leaves the
dialog open, its box framed orange and the line under it saying `The drawing has no block TAGX.`. In a drawing of no
block the dialog still opens, its list greyed, OK greyed while its box is empty, and the line says `The drawing has no
block to use as an arrow. Make one with BLOCK first, or Cancel.`; Cancel, Esc and the close button leave the arrow as
it was. LEADER and QLEADER are each one undo step.

TOLERANCE (TOL) shows the Geometric Tolerance dialog, every box empty, then asks `Enter tolerance location:`, the frame
following the cursor by its insertion point until the point is picked; it is put along the X axis of the UCS in the
current layer and dimension style, each current dimension variable other than the style's written as an override, as
dimensions and leaders write them (DIMTXT 5 typed over ISO-25 gives `1070 140, 1040 5` and rows 10 high), one undo step.
The dialog has two rows of a symbol (the Sym box opens the Symbol
dialog of the fourteen symbols and the blank), two tolerances (the box before a value toggles the diameter sign, the box
after it opens the Material Condition dialog of M, L, S and the blank) and three datums (a letter and its material
condition), then the height of a projected zone with its P and the datum identifier; OK writes them as AutoCAD writes a
TOLERANCE's group 1, a row of nothing left out, a cell left empty kept between its neighbours: the position of a diameter
0.1 at MMC to the datums A, B and C is `{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%v%%vA%%vB%%vC`. Cancel, or OK with every box
empty, makes no frame; with no window the command line says so. A frame is drawn as AutoCAD 2021 lays one out (measured
in its console): every row twice DIMTXT times DIMSCALE high whatever DIMGAP, the insertion point the middle of the left
side of the first row, each next row under the one before on that side with its own width, each cell as wide as its
ink and DIMTXT, a cell of nothing taking no room. The words are measured in the dimension style's text style and the
gdt symbols take the room AutoCAD's gdt font gives them, 2/21 of the height either side of most, 1.5/21 of the diameter
and 2.25/21 right of the runouts, drawn with the presenter's own strokes (board GdtGlyphs); words inked past their pen,
as Arial's A and f, set what follows on from their ink. `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 in Arial is 0.92296 wide and
`{\Fgdt;n}0.1{\Fgdt;m}` 1.009563, as in AutoCAD. Lines are in DIMCLRD, words and symbols in DIMCLRT. The snaps End,
Mid, Nearest and Perpendicular take the sides of each row's rectangle alone, none of the walls between cells or of the
symbols, and Insertion the frame's insertion point, as AutoCAD 2021 gives them. Selected, a frame shows one grip, at
its insertion point; MOVE, COPY, ROTATE and SCALE move it as AutoCAD does (SCALE writes DIMTXT and DIMGAP scaled as
overrides), and MIRROR keeps it readable under MIRRTEXT 0, its normal kept, and turns it over under MIRRTEXT 1. EXPLODE
refuses one with `Cannot explode TOLERANCE`, as AutoCAD 2021 does. TEXTEDIT (DDEDIT) and a double click open the
Geometric Tolerance dialog filled from the frame's words, its rows, height with its P and datum identifier read back
from group 1, and OK writes the words it ends with, one undo step. A row written by hand that leaves a box's cell out
is read by what its cells hold: a first cell that is no symbol is the first tolerance, and letters alone where a
tolerance would be are the first datum, so that `{\Fgdt;j}%%v0.1%%vA%%vB` opens as the position, 0.1, to A and B, and OK
writes it in the dialog's layout, `{\Fgdt;j}%%v0.1%%v%%vA%%vB`, drawn the same, a cell of nothing taking no room
(AutoCAD 2021 draws `{\Fgdt;j}%%v0.1%%vA` and `{\Fgdt;j}%%v0.1%%v%%vA` both 1.271051 wide in Arial). Properties shows a frame's Dim style (Misc), Dim
line color (Lines & Arrows), Text color and Text height (Text), each variable as the frame reads it, and writes a value
typed as an override of its style, as a dimension's rows are written. A frame on a leader to the left is set back the
width of its widest row, and the leader holds that width and the height of all its rows (groups 40 and 41), as AutoCAD
2021 writes them.

QSELECT and FIND show the window's Quick Select and Find and Replace dialogs, whose logic is
[`CadQuickSelect`](xref:GeometryHelper.Dxf.Viewer.CadQuickSelect) and [`CadFind`](xref:GeometryHelper.Dxf.Viewer.CadFind).
Quick Select compares the rows of the Properties panel: of a plan of 312 lines on layer WALLS among others, Line, Layer,
`= Equals`, WALLS selects the 312 and says `312 item(s) selected.`; numbers are compared within the tolerance of a point,
so Length `= 200` takes a line 200.0004 long. Its Select objects button, at the right of Apply to, hides the dialog
while `Select objects:` asks on the command line; the objects picked become the selection and the dialog comes back with
Apply to: Current selection, its types those of the objects picked, so a line of DOORS and a text picked offer Multiple,
Line and Text. The button is greyed while Exclude or Append to current selection set is chosen. FIND searches TEXT, MTEXT by its words, attribute values, dimension text,
multileader text and table cells, and replaces in MTEXT its words alone: `{\fArial|b1;Bolts M20}` with M20 replaced by
M24 is `{\fArial|b1;Bolts M24}`. Replace and Replace All are one undo step each. Over 100 000 objects Quick Select by
layer takes 0.25 s and FIND 0.04 s.

DRAWORDER and TEXTTOFRONT write the sort table AutoCAD writes, a SORTENTSTABLE under ACAD_SORTENTS in the extension
dictionary of the space's block ([`DxfDrawOrderTable`](xref:GeometryHelper.Dxf.DxfDrawOrderTable)), and the display draws
in its order: a line sent to the back with `DR`, Back is drawn under every other object. GROUP makes a GROUP of the
ACAD_GROUP dictionary, named with Name, unnamed (`*A1`) without; with PICKSTYLE 1, the default, a pick of one member
takes the whole of a selectable group, and Ctrl+Shift+A switches that off and on (`<Group off>`). -PURGE never deletes
what AutoCAD keeps: layer 0 and Defpoints, the linetypes ByLayer, ByBlock and Continuous, the styles Standard and the
current ones, the blocks of the spaces, the plot style Normal and every visual style; a group it deletes when the group
holds nothing, named or unnamed (`Deleting group "*A1".`), whether it was never given a member or its members were all
erased; with "Verify each name to be purged?" Yes it asks `Purge layer "EMPTY1"? [Yes/No] <No>:` of each. It purges pass after pass until nothing more is
unused, so that a block OUTER inserted nowhere, holding the only INSERT of INNER, takes INNER with it, and the layer only
INNER's circle was on; a name answered No is not asked again. PURGE shows the Purge dialog where there is a window (the
typed form otherwise): the named objects nothing uses by kind, with Confirm each item (which asks of each object in
AutoCAD 2021's task dialog `Purge - Confirm Purge`, `You are about to purge layer TMP. What do you want to do?`, with
Purge this item, Skip this item and Cancel), Purge nested items, the geometry
of no length and the empty texts, and a preview of the item chosen: a block, or a text, dimension or multileader style
drawn as its dialog draws it, white on black, or a table style drawn on white as the Table Style dialog draws it; and in
Find Non-Purgeable Items the objects in use with the reasons, `Layer WALLS is the
current layer.`, `Used by 2 objects in model space and 0 in layouts.`, `Referenced by block GB.`. Each purge is one undo
step. CLASSICGROUP shows AutoCAD's Object Grouping dialog (GROUP being the command-line form in AutoCAD 2021): New, Add,
Remove, Find Name and Highlight ask on the command line and bring the dialog back; Rename, Description, Explode,
Selectable and Re-Order change the group at once; OK keeps every change as one undo step and Cancel takes them back.
Re-Order… opens Order Group: the groups in a list, and Remove from position, Enter new position and Number of objects
labelled with the ranges the group allows, so a group of twelve moved from 3 to 0 reads (0-11), (0-11) and (1-9). Its
Highlight < hides both dialogs and steps through the members on the canvas one at a time, `Object 0 of 11
[Next/Previous/eXit]:`, Next and Previous going round; eXit or Enter brings Object Grouping back.
LAYERSTATE shows the Layer States Manager and -LAYER State asks the same on the command line: a state is an XRECORD of
ACAD_LAYERSTATES, written group for group as AutoCAD 2021 writes it (mask 2047 for every property, the layers last first
by handle), restored in the properties its mask holds, and exported to a .las file AutoCAD 2021 writes to the byte; each
run is one undo step. With "Apply properties as viewport overrides" ticked while a viewport of a layout is worked
through, or is the one selected on the paper, Restore leaves the layers alone and writes the state into that viewport:
a colour, linetype, lineweight or transparency of the state that is not the layer's own becomes its override there (one
that is the layer's own takes the override off), a layer the state has off or frozen is frozen in the viewport and one
it has on and thawed is thawed there, and with "Turn off layers not found in layer state" the layers the state does not
name are frozen there too. Locking, plotting, the freeze in new viewports and the current layer have no form of one
viewport and stay as they are. New… shows New Layer State to Save, its name (the drawing's states offered in its list)
and an optional Description, which the manager's Description column shows; a name the drawing has is asked about first,
and Yes replaces that state with the layers as they stand and the new description, the properties it restores kept.
Edit… shows Edit Layer State over the state chosen: the layers it saved, by name, each with On, Freeze, Lock, Plot, Color
(Select Color), Linetype (Select Linetype), Lineweight, Transparency (0 to 90), Plot Style (greyed) and New VP Freeze,
changed in the state only; a layer that differs from the drawing's, in a property the state restores, is marked ●, one the
drawing no longer has ○ (kept, passed over on restore); Add layers offers the drawing's layers the state does not name,
Remove layers takes out those chosen, and Layer properties to restore greys the column of a property unticked. OK writes
the state as an edit of LAYERSTATE's one undo step, the drawing's layers left as they are. VIEW shows the View Manager and its New View, -VIEW asks on the command line: a view is a record of
the VIEW table (`db.ViewTable`), its layer snapshot the hidden state ACAD_VIEWS_ and its name, restored with it; Edit
Boundaries and Define window ask two corners and bring the dialog back, Cancel takes the run back. UNITS shows Drawing Units
and its Direction Control, writing LUNITS, LUPREC, AUNITS, AUPREC, ANGDIR, INSUNITS and ANGBASE on OK as one step; typed
lengths and angles are read in those units, `1'3-1/2"` in architectural units and `100` as grads where AUNITS is 2;
a number past the largest double, or `NaNg`, is not read as one.
A typed angle is counted as AutoCAD counts it. A direction (the angle of `@100<30`, a text's rotation, an xline's
angle, a hatch pattern's, the new angle of ROTATE's Reference, DIMEDIT's Rotate and Oblique) is from ANGBASE and, with
ANGDIR 1, clockwise: with ANGBASE 90 and ANGDIR 1, `@100<30` ends at 50, 86.6, and DIMEDIT Oblique 30 slants the
extension lines of a level dimension 60 degrees from the X axis. A turn (ROTATE's angle, an arc's included angle, a
block's rotation, CHAMFER's and LENGTHEN's angles, the angles of a polar array, UCS Z) follows ANGDIR only, so 30 at ROTATE
turns 30 degrees clockwise; a point picked there turns by its direction less ANGBASE. The obliquing angle of a text style
(-STYLE) follows neither. `<<` before the angle reads it in degrees from the X axis counter-clockwise whatever the
settings (`@100<<30` ends at 86.6, 50), and `<<<` the same in the drawing's angle units. The angles DIST, LIST, the
Properties palette (with the text rotation of a dimension and the start and end angles of an ellipse, from its major
axis), the tip by the cursor and the coordinate readout write are counted the same way, east as 90 there; MEASUREGEOM's
Angle writes the angle it measures as AutoCAD 2021 does, as the direction of that many degrees is written, so 30 degrees
between two lines reads 60 with ANGBASE 90 and ANGDIR 1. LIST writes an ellipse and a dimension line for line as AutoCAD
2021 does: an elliptical arc from 30 to 120 degrees "Start Angle: 60" and "End Angle: 330" with ANGBASE 90 and ANGDIR 1,
the obliquing and text angles of a dimension as directions (30 reads 60), and the rotation of a rotated dimension as an
amount turned, by ANGDIR alone (30 reads 330 clockwise, whatever ANGBASE is). In a UCS turned 30 degrees a dimension's
points are the UCS's and its rotation and obliquing angle are counted from the UCS's X axis, so one turned 30 is
horizontal and one turned 90 rotated by 60, its text angle left as it is; a jogged radius writes its centre, chord point,
override centre and jog point. Each object's kind stands two places before its layer, as AutoCAD names it:
"MULTILEADER  Layer:", "LWPOLYLINE  Layer:", "BLOCK REFERENCE  Layer:" for an INSERT or a MINSERT, "ACAD_TABLE  Layer:".
What the drawing keeps stays counter-clockwise from the X axis. A dimension that overrides its style lists
"dimension style overrides:" after its style, then each variable overridden by name in alphabetical order, the name set
in twelve places after a space, as AutoCAD 2021 lists them for every type of dimension: " DIMASZ      5.0000",
" DIMBLK      Dot", " DIMCLRD     1 (red)", " DIMJOGANG   30", " DIMSE1      On", " DIMTXSTY    GHSTYLE". Distances
are written in the drawing's units, nought feet and nought inches after whole feet left out (5", 1', 1'-3 1/2" in
architectural units); DIMSCALE, DIMALTF, DIMLFAC, DIMTVP and DIMTFAC as plain numbers to LUPREC places; DIMJOGANG in the
drawing's angle units; colours 1 to 7 with their names, others by number, BYBLOCK and BYLAYER; arrowheads by AutoCAD's
names (ArchTick, ClosedFilled for none) and a user arrow by its block's name; DIMFXLEN and its switch as DIMFXL and
DIMFXLON.
`CadUnits.ToDirection` and `FromDirection` are the two conversions, and `session.CountAngle(radians)` the second with
the drawing's settings.

`<30` typed at a prompt for a point that has a base point, as LINE's next point, is AutoCAD's angle override: the
session writes "Specify next point or [Undo]: <30 Angle Override: 30", asks the same prompt again and keeps the direction
in `session.AngleOverride` until the point is given. The angle is read as that of `@100<30`, from ANGBASE and clockwise
with ANGDIR 1, and `<<30` from east, counter-clockwise, written back as the drawing counts it ("<<30 Angle Override: 60"
with ANGBASE 90 and ANGDIR 1). A distance typed then runs along the override, on the side of the base point the cursor
is: from (0, 200), `<30` and 100 end at (86.6, 250). The window holds the cursor on the line through the base point at that
angle through `ObjectSnap.AngleOverride`, over the grid, ortho and polar; as AutoCAD 2021's help has it, "coordinates and
object snaps take precedence over an angle override", so a point typed is taken as typed and an endpoint under the
aperture still wins. With no base point, as LINE's first point, `<30` is not a point.
With SELECTIONCYCLING 2, AutoCAD's default, a
click where two lines cross shows the list of both near the cursor, the one drawn on top first, `Line · 0` and
`Line · WALLS`, and the row chosen is taken as a click on that line alone; with 1 or 2, Shift+Space arms cycling, and each
click at the crossing then takes the other line in place of the one before. A plain click there takes the line on top, as
AutoCAD's does while SORTENTS holds its bit 1 (its default 127 does): of all the objects within the pickbox, the one last
in the draw order wins, however much nearer the others lie, so a line sent to the back with DRAWORDER is no longer the one
picked. Under SORTENTS without that bit the one drawn first wins. AutoCAD 2021's `(ssget pt)` on two level lines, one
through the point and one drawn later 0.9 of the pickbox above it, picks the later one; 1.1 of the pickbox above, outside
the box, the other; under SORTENTS 0, the one drawn first. Ctrl+W
switches cycling between 0 and 2, as in AutoCAD.

XATTACH and -XREF Attach or Overlay attach a DXF as an external reference: a block flagged 4 (12 overlaid) naming the
file, and an INSERT of it, one undo step. The presenter reads the file and draws its model space under the INSERT, its
layers named after the reference (`grid|GRID`) and its layer 0 taking the INSERT's. Of the file, the drawing takes the
records AutoCAD 2021 writes for it, as its -XREF Attach of x.dwg wrote them: for each linetype but ByBlock, ByLayer and
Continuous, `x|DASHED` with its description and dashes; for each layer but 0, `x|WALL` with its colour, linetype
(`x|DASHED`), lineweight, on, frozen, locked, plotted, transparency and description, and `1001
AcXRefLayerPropertyOverride, 1071 0`; each flagged 48 (dependent, resolved), and its block 36. Its entities are not
written. Its own attachments are followed, its overlays not. The file is looked for as AutoCAD looks: the path saved, from
the folder of the drawing that names it, then the file's name there, in the drawing's folder and in the support folders.
A grid of 1 000 by 1 attached at (100, 200) from `D:\Plans\xref\grid.dxf` into `D:\Plans\plans\host.dxf` is saved as
`..\xref\grid.dxf` (Relative path; Full path until the drawing is saved), draws its line from (100, 200) to (1 100, 200),
and picks as the INSERT. [`document.Xrefs`](xref:GeometryHelper.Dxf.Viewer.CadXrefManager) lists each reference and image
with its status: Loaded, Unloaded, Not Found, Unresolved, Unreferenced, or Needs Reloading once its file changed on disk.
Each reference the drawing places is followed by those its drawing attaches, nested, its overlays left out as AutoCAD
leaves them: the External References palette lists a nested one once, of the type Nested, and its tree view shows it
under its parent, 16 pixels further in, of its own type. One whose parent is unloaded, not found or unresolved is
Orphaned. The record AutoCAD writes in the drawing for a nested reference is listed under its parent alone, not as
Unreferenced. A nested one is opened from the palette but not unloaded, reloaded, detached or given a path there; typed, -XREF
Unload and Reload name it as AutoCAD 2021's console does (below). The palette's
Unload, Reload and Detach act on every row selected (Ctrl or Shift), the references in one -XREF and the images in one
-IMAGE, so that grid and axes selected and unloaded are one undo step, and a selection of a reference and an image detached
is one too; a nested row in the selection is left out of Unload and Reload, and Detach names it to -XREF, which refuses it
as AutoCAD 2021 does ("Xref column has multiple references. Not detached." and the reference that places it, `"plan"`,
quoted). A selection of nested rows alone leaves all three greyed. The palette gives -XREF and -IMAGE each name with a
back quote before every character AutoCAD reads as a wildcard (`# @ . * ? ~ [ ] -` and the back quote), as AutoCAD's own
palette must: `a#1` unloads only a#1 (typed as `a#1` it matches nothing, "No matching xref names found.", and `a.b` also
takes `a-b`), and `~t` is the name and not "anything but t". The same back quote works typed (``-XREF Unload a`#1,`~t``).
-XREF Unload, Reload and Detach answer "No matching xref names found." for a name that matches no reference, and Unload
"No matching and loaded xref names found." where every reference it matches is unloaded already (one that is loaded among
unloaded ones is the only one unloaded and written); -XREF Path and pathType say it too, before asking a path. A reference
placed by another (plan places column, the host keeping a record of column) is unloaded and reloaded by its own name: -XREF
Unload plan writes `Xref "plan": path`, `"plan" is unloaded.` and, for column, `Xref "column": path`, `"column" is orphaned.`
(`"column" is unloaded.` where column was unloaded already), and Unload * says each of the two once, plan first; a reference
orphaned counts as loaded for Unload. Reload of column while plan is unloaded writes only `Xref "column": path` and `"column"
is orphaned.` and changes nothing; Reload of plan writes `Reload Xref "plan": path`, `<full path> has not changed.` or, where the
file changed, `"plan" loaded: <full path>`, and `"plan" has been restored.` where it was unloaded or orphaned, then each reference
it places that is unloaded, `Xref "column": path` and `"column" is unloaded.`. A reference no INSERT places, here or in a drawing
it is nested in, is unloaded as any other; Reload says `Xref "x": path`, `"x" is unreferenced.` and clears its unloaded state,
and Bind refuses it as it refuses a nested one. A nested reference unloaded is no longer drawn. -IMAGE of a drawing with no image
definition answers `No images in current drawing.` at the option and asks no names (Attach excepted), and a name no image has
`No matching image names found.` (the list prints its table with 0 definitions and 0 instances). A drawing with a reference
bound is saved as a DXF or a DWG that
AutoCAD 2021's AUDIT finds no error in (BINDTYPE 0 and 1, a reference nested in another bound with it, another left
attached): no record of the reference is dependent any more. The window
watches the folders of the files read; one changed on disk is said once on its drawing's command line, where AutoCAD
2021 shows a balloon over its status bar: "External Reference Files Have Changed: grid needs reloading. Reload it with
-XREF Reload or in the External References palette." It is said again only once the reference was reloaded and its file
changed anew.
Unload flags the reference's layers and linetypes 16 and its block 4, as AutoCAD 2021 writes them unloaded, and Reload
flags them 48 and 36 again and writes those of layers and linetypes the file has gained, each an undo step where anything
changes; a record the drawing has keeps what it has. Detach takes them out with the block. No DXF says a reference is
unloaded. One
not found shows its path at its insertion point. -XREF ? prints AutoCAD 2021's table. -XREF Bind binds the references
named, as AutoCAD 2021 binds them, by BINDTYPE (0 Bind, the default, 1 Insert, kept by the session): Bind brings the
drawing's layers, linetypes, text and dimension styles and blocks under the reference's name, `grid$0$GRID`, the number
raised past a name the drawing has; Insert merges them by name, the drawing's own kept, and says each block it has already
("Duplicate Block "TAG" ignored."). The reference's block becomes an ordinary block drawn where the reference was, and the
references its drawing places are bound with it under their own names (`column$0$GRID`), its overlays left out; one undo
step. A reference unloaded is refused ("Cannot bind unloaded xref(s).  Reload them first:"), one whose file, or that of a
reference it places, is not found too ("xref "column" is currently not found. Reload or detach before binding."), and a
nested one alone "is nested and cannot be bound". The palette's Bind… shows Bind Xrefs/DGN Underlays for the drawings
selected, Ctrl or Shift selecting several: its Bind Type, Bind or Insert, starts from BINDTYPE, and OK writes the choice
back to BINDTYPE and binds them all as -XREF Bind binds them, in one undo step; Cancel changes nothing. Bind… is offered
where the selection holds a drawing loaded, or one that Needs Reloading, a nested one too, and greyed for a selection of
only drawings unloaded or not found, or of images. A bind refused binds none of the selection, and the lines -XREF Bind
writes are shown in a box as well as on the command line: grid-base and annex selected, annex unloaded, are answered
"Cannot bind unloaded xref(s).  Reload them first:" and "Xref "annex" is unloaded.", with "Reload annex, then bind
again."; a nested row selected alone is told to bind its parent. The "Duplicate Block" lines of Insert go to the command
line alone.
XOPEN asks
"Select Xref:" and opens the drawing of the reference picked in a tab of its own; a line picked is answered "Object is not
an Xref" and the question asked again, and Enter ends it, as AutoCAD 2021's console answers them. A layer of a
reference the drawing keeps a record of, as AutoCAD writes `grid|GRID` (flags 48), is changed in the drawing as AutoCAD
2021 changes it: on, frozen, locked, colour, linetype and lineweight, from the Layer manager, the layer box or -LAYER,
and transparency, plotting and description from -LAYER, each change marked in the 1071 of the record's
`AcXRefLayerPropertyOverride` as AutoCAD marks it (1 Off, 2 Freeze, 4 Lock, 8 Plot, 16 and 256 Color, 32 Ltype, 64
LWeight, 128 TRansparency, 1024 Description). Under VISRETAIN 1 the record is drawn whole and kept on reload, as AutoCAD
2021 keeps it with VISRETAINMODE 0, its default, which syncs nothing: `x|WALL` green in the record stayed green when x.dwg
made WALL blue, reloaded and opened anew alike. Under VISRETAIN 0 what is marked is drawn as the drawing keeps it and the
rest as the file has it, and Reload writes the file's properties back with 1071 0, as AutoCAD 2021's reload wrote
`x|WALL` made red back as the file's blue; Unload takes the records out. The presenter takes VISRETAINMODE as 0; it does
not write the records again from the file when it opens a drawing of VISRETAIN 0, as AutoCAD does. The current layer, a new name with a | and deleting are refused in AutoCAD 2021's words:
"Cannot set CLAYER to that value." (the layer box, the Layer manager, CLAYER), "Invalid layer name." (-LAYER Set) and
`"grid|X" is an invalid layer name.` (-LAYER New). IMAGEATTACH
and -IMAGE Attach place a PNG, JPEG or BMP at AutoCAD's size: 40 by 30 pixels saved at 3 779 pixels a metre are
10.584811 by 7.938608 mm, one of no resolution one unit wide. The picture is drawn by its pixels, decoded once off the
window's thread, clipped by IMAGECLIP (Rectangular, Polygonal, Select polyline, Invert clip), its frame shown by
IMAGEFRAME 1 or 2 (2 kept as frames shown and not plotted, 70 of 3, as AutoCAD 2021 writes it; a drawing with no image yet given its RASTERVARIABLES when IMAGEFRAME is first changed, 72 the unit of the drawing, 1 for millimetres), its transparent pixels showing what is under it with TRANSPARENCY ON; one not found is its frame with
its file's name. A drawing of 100 000 entities shown as a reference is read and drawn in 0.90 s, where opening it alone
takes 0.87 s (.NET Framework 4.8). AutoCAD 2021 reads no DXF as an external reference ("Unable to load"): it keeps the
block and the INSERTs of a drawing the presenter saved, AUDIT finds no error, and its -XREF ? lists them.

BATTMAN shows the Block Attribute Manager of the block of an INSERT selected, else of the first block with attributes:
its attribute definitions in the block's order, the modes as AutoCAD's letters (I invisible, C constant, V verify, P
preset, L lock position, M multiple lines), a tag given twice in red. Move Up, Move Down, Edit (tag, prompt, default,
modes, text options and properties) and Remove change the list; Apply and OK write it into the block, each one undo step,
and the INSERTs keep their attributes until Sync runs ATTSYNC's logic on every one of them, or at once with "Apply changes
to existing references" in its Settings. A block GB of GRID, LEVEL and REV with LEVEL moved up and synced gives each INSERT
LEVEL then GRID, their values kept. Move Up and Move Down are greyed on a constant attribute, as AutoCAD's are. Another
block chosen in the list, or Select block, with changes not yet written asks first, as AutoCAD's manager does:
`The attributes in the current block have been changed.` and `Do you want to save these changes?` (AutoCAD's Block
Attribute Manager Alert), Yes writing them and No dropping them. AutoCAD has no command-line form of BATTMAN, and with no window it says so. ATTDIA 1
makes INSERT and -INSERT ask the values of the attributes in the Edit Attributes dialog, eight a page in the block's
order, the constant ones left out and a value to be verified asked once; Cancel places the defaults.

BEDIT (BE) opens a block in the Block Editor, as AutoCAD 2021's: the Edit Block Definition dialog lists
`<Current Drawing>` (model space itself) and the named blocks, no layout, external reference or anonymous block, and a
name not in it makes a new, empty block; -BEDIT asks `Enter block name or [?]:`. The block is shown alone in a space of
its own ([`CadDocument.BlockEditor`](xref:GeometryHelper.Dxf.Viewer.CadBlockEditor), `CadSpace.IsBlockEditor`), where
every command draws into and modifies the block's own entities, each an undo step; undo stops where the block was opened
or last saved. BSAVE (BS) redefines the block and draws every INSERT anew (their attributes are not synchronised, as
ATTSYNC is not run), merging the session so far into one undo step, `BEDIT`. A dynamic block saved so has the anonymous
*U blocks its INSERTs place worked out anew from the new definition at each INSERT's values, as AutoCAD 2021's
`UpdateAnonymousBlocks` works them: a panel whose INSERTs of Width 150 and 200 placed *U1 and *U2, given a circle and
saved, has them place *U3 and *U4, each stretched to its width with the circle, INSERTs that shared *U1 sharing *U3, and
*U1 and *U2 left in the drawing placed by none, as AutoCAD leaves them; BSAVEAS copies the content to a new block
and goes on with it, the one opened left as it was. BCLOSE (BC) asks `Save changes to DOOR? [Save/Discard/Cancel]
<Save>:` of a block with changes not saved; Discard reads the drawing back as it stood when the block was opened or last
saved, so a new block never saved is not made. A door of one line given a second and saved shows both in every INSERT,
and one undo after BCLOSE takes the session back. Commands that leave the block or work on another space (BEDIT, XREF,
LAYOUT, PLOT, PREVIEW, PAGESETUP, MSPACE, PSPACE, MVIEW, QSAVE, SAVEAS) say `Not allowed in the Block Editor.`.
The Visibility State box of the Block Editor tab is live for a block of visibility states
([`CadBlockEditor.VisibilityStates`](xref:GeometryHelper.Dxf.Viewer.CadBlockEditor.VisibilityStates)): the first is shown
when the block opens, and another chosen (`CadDocument.SetBlockEditorVisibilityState`) hides what another state lists and
it does not, which is then neither drawn nor picked; an object drawn in the editor joins every state. Choosing a state is a
view of the block, no undo step. RESETBLOCK puts the dynamic block references selected back to their blocks' own values,
one undo step, and says `2 blocks reset.`, counting every block reference selected as AutoCAD 2021 counts them.
DIVIDE and MEASURE put points, or a block with Block (turned along the curve unless `Align block with object? [Yes/No]
<Y>:` is answered No), at equal lengths along a line, arc, circle, ellipse, polyline or spline, as AutoCAD 2021 does:
DIVIDE asks for 2 to 32767 segments and puts one between each, and on a closed curve one at its start too, so a circle
of 50 about (400, 0) divided in 4 has points at (450, 0), (400, 50), (350, 0) and (400, -50); MEASURE steps the length
given from the end nearest the pick, a closed curve from its start, and says `Object isn't that long.` for a length past
the curve: a line from (0, 0) to (100, 0) measured by 30 from its far end has points at 70, 40 and 10. DIMCENTER draws
the centre mark of DIMCEN on a circle or an arc as lines of the current layer: a cross DIMCEN long each way, and below
nought centre lines from twice DIMCEN out to DIMCEN past the rim, only where the radius is over twice DIMCEN (a circle of
5.01 under -2.5 has them, one of 5 the cross alone; a radius dimension follows the same rule). COLOR shows the window's
Select Color dialog for the colour of new objects, and -COLOR asks `Enter default object color [Truecolor/COlorbook]
<1 (red)>:`; -LWEIGHT asks `Enter default lineweight for new objects or [?]:` and rounds a width to the
nearest of AutoCAD's, `0.33` to 0.35 mm with a word saying so. LWEIGHT (`LW`, `LINEWEIGHT`) shows the window's Lineweight
Settings dialog, as AutoCAD 2021's: the Lineweights list (ByLayer, ByBlock, Default and the 24 widths, each with its
sample), whose line picked is the lineweight of new objects (CELWEIGHT, one undo step named LWEIGHT where it changed); Units
for Listing in millimetres (`0.25 mm`) or inches (`0.010"`), Display Lineweight (LWDISPLAY), Default (the width a lineweight
of Default is drawn with, AutoCAD's LWDEFAULT, 0.25 mm until set) and Adjust Display Scale (lines drawn from half to twice
their width on screen, a 0.50 mm line about 2 pixels at the middle); the last three are kept in the global settings
(`GlobalSettings.LineweightsInInches`, `DefaultLineweight`, `LineweightDisplayScale`) and every drawing open is drawn again
with them. With no window LWEIGHT asks -LWEIGHT's question. REDRAW and REDRAWALL have nothing left to draw, and
REGENALL regenerates as REGEN does. ISOLATEOBJECTS hides every other object of the space, HIDEOBJECTS those selected,
as AutoCAD 2021 hides them (their Visible off) for the session only: a save writes them shown, and UNISOLATEOBJECTS shows
them again, saying `1 object(s) unisolated.` or `No objects to unisolate.`. Each is one undo step.
CHANGE (`-CH`) asks `Select objects:` and then `Specify change point or [Properties]:`, as AutoCAD 2021 does. A point moves
the end of each line nearest it, so a line from (0, 0) to (100, 0) given (110, 5) ends at (110, 5), and with ORTHOMODE on
keeps the line horizontal or vertical (the other end fixed, along x where the point is further along x than y, else along
y); a lone circle takes the distance from its centre as its radius, a circle from (200, 0) of 10 given (215, 0) becomes 15.
Among several objects the point is for the lines only, and each circle, text and block says `Change point ignored` and asks
its own questions. Enter asks them too: `Specify new circle radius <no change>:`, and for a text `Specify new text
insertion point <no change>:`, `Enter new text style <Standard>:`, `Specify new height <5.0000>:`, `Specify new rotation
angle <0>:` and `Enter new text <NOTE>:` (a lone text given a point takes it as the insertion point and skips the first),
for a block `Specify new block insertion point <no change>:` and `Specify new block rotation angle <0>:`, and for an
attribute definition the tag, the prompt and the default value after the rotation. A height of 0 says `Value must be
positive and nonzero.` and asks again; a style the drawing lacks says `Style "NoSuch" not defined - use STYLE command.` and
goes on. An arc, a polyline or a point has no change point: `No changeable object selected`. Properties asks `Enter property
to change [Color/Elev/LAyer/LType/ltScale/LWeight/Thickness/TRansparency/Material/Annotative]:` until Enter: Color (`New
color [Truecolor/COlorbook] <BYLAYER>:`, with `Invalid color number.` for 0 or 256 or past 255), Elev (every point of a
line, circle, arc, text, block or point given the level, a polyline its elevation), LAyer and LType (a name the drawing lacks
is refused, `Cannot find layer "X".` asking again, `Linetype "X" not found.  Use LINETYPE command to load it.` going back to
the property), ltScale (nought is `Value must be nonzero.`, a negative one is taken as its size), LWeight (`0.33` rounds to
0.35 mm with a word), Thickness and TRansparency (0 to 90 or ByLayer or ByBlock, `Requires an integer between 0 and 90.`).
Each prompt offers the value the run gave last, else the drawing's current one, and Enter takes it. Material and Annotative
are asked and change nothing: the presenter holds no materials and CHANGE leaves annotation to Properties. One undo step.
-RENAME asks `Enter object type to rename [Block/dEtailviewstyle/Dimstyle/LAyer/LType/Material/multileadeRstyle/seCtionviewstyle/Style/Tablestyle/Ucs/VIew/VPort]:`,
then `Enter old block name:` and `Enter new block name:` (`dimension style`, `layer`, `linetype`, `multileader style`, `text style`, `table style`, `ucs`, `view` or `viewport configuration` in the
others' places). An old name with a wildcard says `Invalid block name.` and asks again, as does one nothing has
(`Cannot find block "Nope".`); a new name that is refused is said and asked again. Every reference follows the new name: the
INSERTs of a block, the entities, layers, multiline styles and the current linetype of a linetype, the texts, attributes and
tables of a text style, the dimensions of a dimension style, and the current multileader or table style. Layer 0, Standard
and the linetypes ByLayer, ByBlock and Continuous keep their names. Material, dEtailviewstyle and seCtionviewstyle say the
drawing holds none. One undo step (a UCS rename is made on the UCS manager and is not a step, as the UCS command's are).
RENAME (`REN`) shows the window's Rename dialog, laid out as AutoCAD 2021's: the Named Objects list (Blocks, Dimension styles,
Layers, Linetypes, Multileader styles, Table styles, Text styles, UCSs, Viewports, Views), the Items of the kind picked
(those that can be renamed, sorted by their letters: no layer 0, no Standard style, no ByLayer, ByBlock or Continuous), Old
Name, and Rename To with the name typed. Rename To renames the item in the list at once and refuses an empty name, one with
any of `< > / \ " : ; ? * | , = `` ` in it, or another item's name (`"DOORS" already exists.`); OK hands the renames to
RENAME, which makes them in order as -RENAME would, all in one undo step, and writes any refusal on the command line; Cancel
renames nothing. With no window RENAME asks -RENAME's questions. The request is a public
[`CadRenameRequest`](xref:GeometryHelper.Dxf.Viewer.CadRenameRequest) of [`CadRename`](xref:GeometryHelper.Dxf.Viewer.CadRename)s.
The helpers behind it are public: `CadBlocks.Rename`, `CadLinetypes.Rename`, `CadMLeaderStyleEditor.Rename` and
`CadTableStyleEditor.Rename` join `CadLayerEditor.Rename`, `CadDimStyleEditor.Rename` and `CadTextStyleEditor.Rename`.
PROPERTIES, OPTIONS and LAYER ask the window for its panel or dialog (`UiRequested`), and say "is shown by the window"
where none is listening; QSAVE (of a drawing with no file), SAVEAS and DXFOUT ask it for its file dialog, and with none listening or FILEDIA 0 ask the file on the command line as AutoCAD does (see the application guide). The window adds CLOSE, QUIT, NEW, PROPERTIESCLOSE, RIBBON, RIBBONCLOSE, COMMANDLINE, COMMANDLINEHIDE, HIDEPALETTES, SHOWPALETTES, -OSNAP, GRID, SNAP, ORTHO, POLAR, OSNAP, ISOPLANE, LAYMCUR and LAYMCH, and the plugin commands. The inquiry commands print in AutoCAD's words: DIST from (0, 0) to (100, 100) says `Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0`; AREA by points or by an object says `Area = 5000.0000, Perimeter = 300.0000`. Each command is a
[`CadCommand`](xref:GeometryHelper.Dxf.Viewer.CadCommand): a name and an iterator that yields each
[`CadPrompt`](xref:GeometryHelper.Dxf.Viewer.CadPrompt) and reads the answer from [`CadCommandContext`](xref:GeometryHelper.Dxf.Viewer.CadCommandContext)`.Input`, so it reads top
to bottom as its prompts do, and a [command plugin](plugins.md) is written the same way.

### Annotation commands

DIMOVERRIDE (DOV, DIMOVER) asks as AutoCAD 2021's does (probed in its console): "Enter dimension variable name to override
or [Clear overrides]:", then "Enter new value for dimension variable <current>:" for each name, "Enter dimension variable name
to override:" after the first, Enter, and "Select objects:". The current value is the drawing's own, to four places: DIMSCALE
`<1.0000>`, a flag `<On>` or `<Off>`, a whole number `<0>`, a colour `<BYBLOCK>`, an arrowhead `<ClosedFilled>`. A name AutoCAD
does not know says `Variable "XYZ" not found.`, and so do the variables of R2007 and after (DIMJOGANG, DIMLTYPE, DIMLTEX1,
DIMLTEX2, DIMFXL, DIMFXLON, DIMTFILL, DIMTFILLCLR, DIMARCSYM); a whole number out of its range says `Requires an integer
between 0 and 4.` (DIMTAD 7), a size below nought `Value must be positive.`, a flag that is neither On nor Off `Invalid option
keyword.`, and each asks again. The values are written one at a time in the order of their groups as overrides of the dimension
style on the dimensions, leaders and feature control frames selected, each dimension drawn anew, one undo step: DIMTAD 0 and
DIMSCALE 2 typed in that order give a linear dimension of the overrides `40 77`, and its words 5 high where they were 2.5.
Clear overrides takes every override of the objects selected away, as `SetFromStyle` does; objects of another kind are left
alone. Enter at a value keeps the one shown and writes no override.

TEXTALIGN (TA) keeps AutoCAD's settings: "Current settings: Align = Left, Spacing Mode = Current Vertical" and "Select text
objects to align [alIgnment/Options]:". alIgnment asks "Select alignment direction [Left/Center/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR]
<Left>:", Options "Enter an option [Distribute/Set spacing/current Vertical/current Horizontal] <current Vertical>:" (Set spacing
asks "Set spacing <0.000000>:"). After the TEXT and MTEXT are selected (others are filtered out, `1 was filtered out.`),
"Select text object to align to [Point]:" takes the text they line up with, or Point two points that give a line ("Pick first
point", "Pick second point or [Options]:"). The texts move by the boxes they are drawn in: Left, Center and Right line up an
edge or the middle across, the two-letter ones a corner or the middle of a side. Current Vertical keeps each text's height and
moves it across, so that its edge lies on the text picked or on the line; Current Horizontal keeps its place along and moves it
up or down; Set spacing stacks them a gap apart above and below the text picked, which stays; Distribute spaces them evenly
between the top one and the bottom one. A line within a degree of the way the texts cannot reach (a level one with Current
Vertical) says `The base line is near horizontal or vertical, aligned texts may be out of available area, alignment is
discarded.` and moves nothing. Three texts 5 high at (0, 0), (10, 20) and (30, 50) lined up on the left edge of the first have
that edge in common. The settings stay for the next run in the same window; the Left, Center and Right alignments stand at
the middle of the box when the texts move up or down.

MLEADEREDIT (MLE) takes "Select a multileader:", then "Specify leader arrowhead location or [Remove leaders]:": each point
adds a leader line whose arrowhead stands there (`DxfMLeader.AddLeaderLine`), Enter ends; under Remove leaders "Select leader to
remove or [Add leaders]:" takes the leader line nearest the point within the pick aperture away (`No leader line there.` where
none is), and the last line of a multileader is kept (`A multileader keeps at least one leader line.`). Each change is a step
of the one undo step. MLEADERALIGN (MLA) takes "Select multileaders:", writes "Current mode: Use current spacing" and asks
"Select multileader to align to or [Options]:" and "Specify direction:": the content of each (its words' attachment point, or
its block's position) moves with its landing onto the line through the content of the one picked along the direction, the
arrowheads staying. Options asks "Enter an option [Distribute/make leader segments Parallel/specify Spacing/Use current
spacing] <Use current spacing>:": Use current spacing keeps each content's place along the line, specify Spacing ("Specify
spacing <0.0000>:") puts them that far apart along it in the order they stand, the one picked staying, and Distribute asks two
points ("Specify first point or [Options]:", "Specify second point:") and spaces the contents evenly between them. The gaps are
measured between the content points, not their extents; make leader segments Parallel says `Make leader segments parallel is
not built here: the mode stays as it was.` MLEADERCOLLECT, which joins block multileaders into one, is not built.

ATTIPEDIT (ATI) asks "Select an attribute:" and edits the one clicked (that of the block drawn nearest the click, as -ATTEDIT
takes it; the first of a block given by its handle) in place, in the box over it on the canvas, writing its value as one undo
step; a click on the block's own lines says `Selected object is not an attribute.` An attribute of several lines is not edited
here. SPELL (SP) shows the window's Check Spelling dialog, as AutoCAD 2021's: Where to check (Entire drawing, every space;
Current space/layout; Selected objects, whose button closes the dialog for `Select objects:` and shows it again), Start, Not in
dictionary, Suggestions with the replacement typed above them, Ignore, Ignore All, Change, Change All (the word changed
wherever it comes again), Add to Dictionary, the main dictionary (the built-in English list) and a custom dictionary file.
Change with nothing typed takes the first suggestion and with none says `No suggestions.`. Nothing is written while the
dialog is open: Close writes the words changed, each text one edit of SPELL's one undo step, and appends the words added to
the custom dictionary file. The check is a public [`CadSpellCheck`](xref:GeometryHelper.Dxf.Viewer.CadSpellCheck). With no
window SPELL is the command line form of Check Spelling: "Select objects or [All/Dictionary]:" takes objects, or Enter or
All every TEXT, MTEXT and attribute of the space, and Dictionary "Enter custom dictionary file <none>:", a text file of words one
to a line (`Cannot read custom dictionary.` where it cannot be read). Each word not in the built-in list (about 700 English
words and drafting terms, the endings -s, -es, -ed, -ing, -ly, -er, -est and 's tried; words in capitals, with a digit or of
one letter are passed over, as AutoCAD's settings pass uppercase words and words with numbers) is written with the words
nearest it, `Not in dictionary: "wrold". Suggestions: world`, and "Enter replacement or [Ignore/ignore aLl/Add/Change/Stop]
<Ignore>:" takes a replacement typed, Change (the first suggestion), Add (to the custom dictionary and its file), Ignore, ignore
all, or Stop. The words changed of a text are written together, the format codes of an MTEXT kept in it, and the whole
check is one undo step; `Spelling check complete.` ends it.

IMAGEADJUST (IAD) and -IMAGEADJUST set the brightness, contrast and fade of images (groups 281, 282 and 283 of the IMAGE), -IMAGEADJUST on the
command line as AutoCAD 2021's -IMAGEADJUST asks it, IMAGEADJUST too where no window shows its dialog. "Select image(s):" takes the images (a
line picked with them is not counted, and with no image the command ends), "Enter image option [Contrast/Fade/Brightness]
<Brightness>:" the option and "Enter brightness value (0-100) <50>:" its value, a whole number from 0 to 100; 150, -5, 30.5 and
`abc` are each refused with `Value must be an integer between 0 and 100.` and asked again. Enter takes the value offered, which is
the image's own where one is selected and the factory's (50 for brightness and contrast, 0 for fade) where several are, so that
Enter then gives them all the factory's: two images of brightness 70 and 50 selected together offer 50 and both become 50. An
image on a locked layer is left as it is, `The image is on a locked layer.` The change is one undo step, and the canvas draws the
picture lighter or darker as it was already drawn. AutoCAD refuses a bitonal picture, `Bitonal images cannot be adjusted.`; the
presenter keeps no colour depth of an image definition and so refuses none, writing the values the picture then ignores. ATTEXT (DDATTEXT) and -ATTEXT write the attributes of the blocks to a file, on the command line as AutoCAD 2021's console
asks it (-ATTEXT, and ATTEXT where no window shows its dialog): "Enter extraction type or enable object selection [Cdf/Sdf/Dxf/Objects] <C>:",
for Objects "Select objects:" and "Enter attribute extraction type [Cdf/Sdf/Dxf] <C>:", then for a comma-delimited (CDF) or
space-delimited (SDF) file "Enter template file name:" (the last one named is offered; `.txt` is added to a name with no
extension; `"names.txt": Can't find file.` asks again) and for every kind "Enter extract file name <plan>:" (`.txt`, or `.dxx` for a
DXF extract, added to a name with no extension, the folder of the drawing the home of one with no folder, a file there already
written over, and the template refused as the extract file, `The template and extract files cannot be the same.`). The template
has a line for each field, its name and its format, C or N then three figures of width and three of decimals: `NAME C012000` is the
text of the attribute NAME in up to 12 characters, `PRICE N008002` its number in up to 8 with 2 decimals, and the block fields
`BL:LEVEL` (1, as only the blocks of a space are read), `BL:NAME`, `BL:X`, `BL:Y`, `BL:Z`, `BL:NUMBER` (the place of the block among
the block references read, those with no attribute counted), `BL:HANDLE`, `BL:LAYER`, `BL:ORIENT` (in degrees), `BL:XSCALE`,
`BL:YSCALE`, `BL:ZSCALE` and `BL:XEXTRUDE`, `BL:YEXTRUDE`, `BL:ZEXTRUDE` (the normal); letters of either case are the same, and
`C:DELIM` and `C:QUOTE` lines (`C:DELIM ;`) give a CDF file its own delimiter and quote, comma and apostrophe where it gives none.
A line that is no field, a field twice or no field at all is told, `** Invalid field specification:` and the line,
`** Duplicate field specification:` and the line, or `** No Attribute fields specified.`, and nothing is written. A tag the block
has not gives blank text or nought. The blocks that have attributes are written in the order of the drawing, model space and then
each layout (or the objects selected), one record each: with the fields of drawing `parts`, a block ITEM at (10, 20) with NAME
`Bolt M8` and PRICE `1.5` and a template of `BL:NAME C008000`, `BL:X N007002`, `NAME C012000`, `PRICE N008002` is
`'ITEM', 10.00,'Bolt M8', 1.50` in a CDF file, where a number keeps a place for its sign, and `ITEM      10.00Bolt M8         1.50` in an
SDF file, text to the left and numbers to the right of their widths. A value too long for its field is cut to it and told once a
record, `** Field overflow in record 1`; a value of a numeric field that is no number is told, `** Bad numeric value for field
PRICE, record 3`, and written as nought; `5 records in extract file.` ends it. A DXF extract holds each block as an INSERT with
its layer, name, insertion point, and the scales, turn and normal that are not 1, 0 and Z, its ATTRIBs (value, tag, flags) and a
SEQEND, then EOF, `20 objects in extract file.` for 5 blocks of 2 attributes. Compared with AutoCAD 2021's console the
files are the same record for record, the handles apart; the file is UTF-8 without a mark where AutoCAD writes the system's code
page, the SEQEND carries no handle, a quote inside a CDF value is written as it is, and a block nested in another is not read, as
AutoCAD does not. CTABLESTYLE (CT) is the table style new tables take: "Enter new value for CTABLESTYLE <"Standard">:" makes the style
of that name current, one undo step, and a name of no style says `Cannot set CTABLESTYLE to that value.`, as CMLEADERSTYLE does.

In the window, IMAGEADJUST shows the Image Adjust dialog once the images are selected: Brightness, Contrast and Fade, each a
slider and a box from 0 to 100 (150 is kept as 100), a preview of the first image's picture adjusted as the canvas draws it
(contrast about the middle, brightness, then fade towards black; "No picture found" where its file is not found), and Reset to
50, 50 and 0. It starts at the image's own values where one is selected and at 50, 50 and 0 where several are; OK gives every
image not on a locked layer the three values, one undo step named IMAGEADJUST. ATTEXT shows the classic Attribute Extraction
dialog: File Format (CDF, SDF or DXX, the output file's extension following), Select Objects (the dialog closes, `Select
objects:` is asked and it comes back with `Number found: 2`; none selected is the whole drawing), Template File (greyed for
DXX) and Output File, the drawing's name with `.txt` to start with. OK refuses a CDF or SDF extract with no template, then
writes the extract as -ATTEXT does, a template not there said `"missing.txt": Can't find file.` and nothing written.

### Aliases and keys

[`CadShortcuts`](xref:GeometryHelper.Dxf.Viewer.CadShortcuts) holds the aliases and the keys as data. The defaults are
AutoCAD's: the 248 aliases of AutoCAD 2021's acad.pgp for the commands the presenter has (DXFOUT is a command of its own) and the four digits of the maintainer's (`1` LAYISO, `2` LAYERP, `22` LAYON, `3` LAYOFF), 252 in all, old names as `DTEXT` TEXT and `DDMODIFY` PROPERTIES among them (`M` MOVE, `CO` COPY, `E` ERASE, `L` LINE, `PL` PLINE, `C` CIRCLE, `Z` ZOOM, `LA` LAYER, `DLI`
DIMLINEAR, `MV` MVIEW, `MS` MSPACE, `PS` PSPACE, `BE` BEDIT, `BS` BSAVE, `BC` BCLOSE, and the rest) and 24 keys (F3 OSNAP, F7 GRID, F8 ORTHO, F9 SNAP, F10 POLAR, F11 OTRACK, F12 DYN, Delete ERASE,
Ctrl+Z U, Ctrl+Y REDO, Ctrl+S QSAVE, Ctrl+1 PROPERTIES (shows or hides Properties), Ctrl+2 DRAWINGS (shows or hides the Drawings list), Ctrl+3 RIBBON (shows or hides the ribbon), Ctrl+A SELECTALL, Ctrl+C COPYCLIP, Ctrl+X CUTCLIP, Ctrl+V PASTECLIP, Ctrl+Shift+C COPYBASE, Ctrl+Shift+V PASTEBLOCK, Ctrl+P PLOT, Ctrl+Shift+A PICKSTYLE, Ctrl+W SELECTIONCYCLING, Ctrl+Shift+P QPMODE (switches the Quick Properties panel)). A registry's `Shortcuts` is replaced, from
[global settings](app.md#global-settings), by another set; letters of either case are the same, and a name wins over an
alias of the same letters. A settings file written before an alias joined the defaults gets it once: the file lists the
default aliases it has seen (`knownDefaultAliases`, beside `knownDefaultKeys`), and one that is not on the list is added
when the file has no alias of that name, so that an old file read now also has `1`, `2`, `22` and `3`, and an alias of the
user's own with such a name, or one he cleared, is never overwritten or put back.

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
| the header of the drawing, written as AutoCAD writes it | CLAYER, CECOLOR, CELTYPE, CELWEIGHT, CELTSCALE, LTSCALE, PSLTSCALE, MSLTSCALE, DIMSCALE, DIMASSOC, CMLEADERSTYLE, CTABLESTYLE, MLEADERSCALE, TEXTSIZE, TEXTSTYLE, FILLETRAD, CHAMFERA, CHAMFERB, PDMODE, PDSIZE, MIRRTEXT, SORTENTS, IMAGEFRAME, XCLIPFRAME, CANNOSCALE, INSBASE, LIMMIN, LIMMAX | one undo step |
| the document or the session | PICKSTYLE, SELECTIONCYCLING, SHOWPAGESETUPFORNEWLAYOUTS, PICKFIRST, PICKADD, ATTREQ, ATTDIA, BINDTYPE, FILEDIA, CMDDIA, PDFSHX, PEDITACCEPT, PLINETYPE, DIMCONTINUEMODE, REVCLOUDAPPROXARCLEN, REVCLOUDARCVARIANCE, REVCLOUDCREATEMODE, OFFSETDIST, OLEFRAME, OLEHIDE, OLEQUALITY | at once, no undo step |
| the window: the status bar, the snaps and Global settings | OSMODE, POLARANG, POLARMODE, POLARADDANG, POLARDIST, SNAPTYPE, GRIDSTYLE, SNAPGRIDLEGACY, AUTOSNAP, DYNMODE, PICKBOX, GRIPSIZE, GRIPMULTIFUNCTIONAL, APERTURE, ZOOMWHEEL, TABLEINDICATOR, GRIPDYNCOLOR | at once, no undo step; kept in the settings file |
| the window's toggles and snap of the drawing shown | ORTHOMODE, SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, GRIDDISPLAY, SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL, SNAPISOPAIR | written into the drawing, as AutoCAD saves them there, and one undo step that keeps no copy of the drawing (see below) |
| the window's lineweight toggle | LWDISPLAY | at once, no undo step; written into the drawing |
| read only | DIMSTYLE, EXTMIN, EXTMAX, UCSNAME, UCSORG, DWGNAME, DWGPREFIX, DATE, CDATE | |

FILLETRAD 5 writes `$FILLETRAD` 40 `5` into the header and U takes it back to 10. OSMODE 4133 ticks Endpoint, Center,
Intersection and Extension, and 16391 ticks Endpoint, Midpoint and Center with OSNAP off. With no window, ORTHOMODE and
LWDISPLAY are the drawing's `$ORTHOMODE` and `$LWDISPLAY`; the window answers them, and SNAPMODE and GRIDMODE, with the
toggles of the drawing shown, and writes each switched into that drawing: ORTHO into `$ORTHOMODE`, the lineweights into
`$LWDISPLAY`, SNAP and GRID into its `*Active` viewport, as AutoCAD saves them with the drawing (see the status bar in
[the app](app.md)); in a layout's paper space the snap and grid variables are the layout's own, kept in the viewport 1 of
its sheet, and through a viewport that viewport's, as AutoCAD 2021 keeps them. A point typed at SETVAR leaves the last
point (LASTPOINT) where it was.

A snap or grid variable changed is an undo step that keeps no copy of the drawing: it holds the viewport's snap and grid
as they were and as they became, and U and REDO write those back, reading the drawing again no more than ZOOM does (an
F7 pressed and undone in a drawing of 100 000 lines takes 0.3 ms and 0.2 ms and keeps a few kilobytes, where a step that
keeps the drawing keeps 20 MB and takes 205 ms to undo). AutoCAD 2021's console, probed,
undoes them so: GRID, SNAP, SETVAR and a variable typed alone (GRIDMODE, SNAPUNIT, GRIDUNIT) are a step each, named by the
command, so that after a line, GRID 50 and SNAP 25 the first U takes SNAP 25 back, the second GRID 50 and the third the
line; F7, F9, F8 and F5 pressed at the prompt, and the status bar's SNAP, GRID and ORTHO buttons, join the step of the
command before them, one U for both under that command's name (F7 after a LINE is taken back with the line, and a second
line drawn after it leaves the grid as F7 left it when U takes that line back), and are a step of their own where no
command came before them. A drawing counts as changed after one, as AutoCAD's does (DBMOD 4). The Drafting Settings
dialog's OK is one step, DSETTINGS. LWDISPLAY and the variables kept in the settings file (OSMODE, GRIDSTYLE, ...) are
still not steps.
PICKFIRST 0 makes a command that works on objects ask `Select objects:` whatever is selected before it: with a line
selected, MOVE asks for objects rather than its base point. PICKADD 0 makes what is picked last the selection, Shift
adding to it, at `Select objects:` and on the canvas: a click on line A, then on line B, leaves B alone; 1, the default,
and 2 add, Shift taking out. ATTREQ 0 makes INSERT give each attribute its default without asking; ATTDIA 1, 0 by
default, makes it ask them in the Edit Attributes dialog where there is a window. DYNMODE keeps -3 to
3, negative while DYN is off, so that F12 brings the same value back; the one box of dynamic input shows both pointer and
dimensional input whichever of 1 to 3 is set. GRIPDYNCOLOR, the colour of a dynamic block's custom grips, takes an
index of AutoCAD's colours from 1 to 255, 140 (#00BFFF) by default as AutoCAD 2021 starts; AutoCAD keeps it in the
registry and writes none into a drawing (probe GhDb1), and the window keeps it in the settings file
(`"gripDynamicColor": 140`). GRIPSIZE, 1 to 255 and 5 by default, is the size of the grips (`"gripSize": 5`): a plain grip is a
square 2 × size + 5 pixels across, 15 at 5 and 25 at 10 (see [Hover menus](#hover-menus)).
GRIPMULTIFUNCTIONAL, 0 to 3 and 3 by default (`"gripMultifunctional": 3`), turns the hover menu of a multi-functional
grip on with its bit 2; bit 1 is AutoCAD's Ctrl cycling, which the presenter keeps in the variable but does not act on.
The window keeps PICKBOX, GRIPSIZE, APERTURE, LWDISPLAY and POLARANG in
the settings file as it keeps DYN and the snaps, POLARANG 30 written `"polarAngle": 30`, and POLARMODE too: its 2 runs
the lines of object snap tracking on every polar angle (0, AutoCAD's default, runs them level and upright alone), and its 8
acquires a point only with Shift held; its 1 counts the polar angles from the last segment drawn, a line or the end of
an arc (from the X axis while none is drawn), and its 4 holds the additional angles of POLARADDANG as they are, not their
multiples. POLARADDANG takes up to ten angles parted by semicolons, read in the drawing's angle units and kept from 0 up
to 360 as the drawing writes angles: `33.5;-10` is kept as `33.50;350.00` at AUPREC 2, `.` empties it, and `10;;20`
gets "Cannot set POLARADDANG to that value."; the settings file keeps it as `"polarAddedAngles"`. The Polar Tracking tab
of Drafting Settings sets them too: Additional angles with its list, New opening a row at its end that Enter or a click
elsewhere keeps where it reads as an angle ("Not an angle." otherwise, the row kept open) and Esc drops, Delete, ten at
most; Track orthogonally only or using all polar angle settings; Absolute or Relative to last segment; Apply writes
POLARMODE with its 8 kept. While polar tracking holds the cursor, with DYN off, a dotted path runs from the base point
through it to the edge of the view and the tip by the cursor says `Polar: 183.8478 < 45°`, the distance in LUNITS and
LUPREC and the angle in AUNITS and AUPREC from the X axis of the UCS, or `Relative Polar: 100.0000 < 90°`, counted from
the last segment, where POLARMODE 1 counts from one. AUTOSNAP's bits are AutoCAD's: 1 the marker, 2 the snap's name by the cursor, 4 the magnet (off, the rubber
band stays at the cursor while the marker shows the snap, which a click still takes), 8 POLAR, 16 OTRACK, 32 the tip
of ortho, polar and tracking; the marker, tips and magnet are kept for the session of the window. FILEDIA 0 makes PNGOUT,
JPGOUT, BMPOUT and the window's SAVEAS ask for the file on the command line, as in a script; CMDDIA 0 makes PLOT ask
-PLOT's prompts. Both are kept by the session, as ATTREQ. ATTDIA 1 makes INSERT ask the attribute values in
the Edit Attributes dialog, as ATTREQ 1 asks them.
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
angle. So an object snap under the aperture beats grid snap, ortho and polar tracking, as AutoCAD 2021's help has it:
"Ortho is ignored when you enter coordinates or specify an object snap" (About Orthogonal Locking), and "coordinates
and object snaps take precedence over an angle override", which itself overrides Grid Snap, Ortho mode and PolarSnap
(About Specifying Distances, Lengths, and Angles). A point typed at the prompt is taken as typed, never snapped. With
SNAP and OSNAP on and a grid of 10, LINE's next point from (1 000, 500) with the cursor at (3 987, 2 988) takes the
endpoint (4 000, 3 000), 3 905.12 long, not the grid point (3 990, 2 990). The entities of a file with no handles, as
one of R12 written by hand, snap as those of any other: the document gives each a handle on opening it (see [The document](display.md#the-document)). The result is a
[`CadSnapResult`](xref:GeometryHelper.Dxf.Viewer.CadSnapResult): the point, which
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
| TK, TRACK, TRACKING | `First tracking point:`, then `Next point  (Press ENTER to end tracking):` until Enter | the point tracked: each next point taken square from the one before, along the axis it lies farther along, upright where it lies as far along both |
| .X .Y .Z .XY .XZ .YZ | `of`, then `(need YZ):` and so on | the coordinates named from the first point, the rest from the second |

LINE with FROM, the base point 100,50 and the offset `@30,20` starts at (130, 70); M2P between (0, 0) and (100, 40) gives
(50, 20), and M2P at the radius of a circle centred at (0, 0) between (0, 0) and (60, 80) gives a radius of 50. CIRCLE
with TK from 100,0 and a click at (130, 50) centres the circle at (100, 50), the middle of a plate 200 by 100; a distance
typed at `Next point` runs along the axis nearest the cursor, so 60 with the cursor at (80, 10) reaches (60, 0). `.X` of
(30, 999) with (888, 40) gives (30, 40). At `(need YZ):` the answer is a point whose Y is taken,
`40,3` giving Y 3 as AutoCAD 2021 gives it; where one coordinate is asked, a number alone is that coordinate. Z, which a
plan has none of, is asked as AutoCAD asks it and left out. Coordinates are those of the UCS. Each point a modifier asks
may be snapped (END typed, or the menu). A modifier may be given by another at its prompt, as FROM with M2P for its base
point, but TT and TK only at the command's own prompt, and FROM not at TK's `Next point`. Enter or text at TT's or a
filter's prompt, and Enter at FROM's, go to the prompt they were typed at, so Enter at FROM's `Base point:` at LINE's
next point ends the LINE; FROM asks again for text, M2P for text and Enter, saying "Point or option keyword required."
where the command's prompt has options and "Invalid point." where it has none; TK says "Invalid 2D point.", at
`First tracking point:` going back to the command's prompt, and Enter at its `Next point` ends tracking. Esc cancels
the command.

## Grips

[`CadGrips.Of(entity)`](xref:GeometryHelper.Dxf.Viewer.CadGrips) lists the grips of an entity, each a
[`CadGrip`](xref:GeometryHelper.Dxf.Viewer.CadGrip) with its index, its [`CadGripKind`](xref:GeometryHelper.Dxf.Viewer.CadGripKind)
and its place; `document.GripDrag(handle, index, point)` drags one, as one undo step.

| Entity | Grips | Dragging |
|---|---|---|
| LINE | two ends, the middle | an end moves it; the middle moves the line |
| CIRCLE | the centre, four quadrants | the centre moves it; a quadrant changes the radius |
| ARC | two ends, the middle, the centre | the middle bends the arc through the point |
| polylines | each vertex (a square), the middle of each segment (a bar along the segment) | a vertex moves; the middle of a straight segment moves it, of an arc segment bends it; the cursor resting on either opens a menu (see below) |
| TEXT, INSERT | the insertion point | moves it |
| MTEXT | the location and the corners of its box (top left, top right, bottom left, bottom right; the location first where it is at none of them); the location alone for an MTEXT of no width or in columns | the location moves it; a corner takes its two sides of the box to the point, which sets the defined width and height, the location staying at the point of the new box the attachment names |
| VIEWPORT | four corners, the middle | a corner resizes the frame, the opposite corner, the scale and the centre of the view kept |
| linear and aligned DIMENSION | the two extension line origins, the two ends of the dimension line, the words | an origin measures anew, the dimension line kept where it is; an end of the line moves the line through the point; the words move as DIMTEDIT moves them |
| radius and diameter DIMENSION | the chord point (a diameter's two), the words | a chord point goes round the circle towards the point |
| angular DIMENSION | the vertex and the two line points (an angle between two lines: the ends of both lines), the arc, the words | a point measures anew; the arc runs through the point |
| ordinate DIMENSION | the feature, the leader's end | moves that point |
| dynamic INSERT | the insertion point, then its custom grips | a linear arrow sets the distance along its parameter, a rotation circle the angle about its base, a point, polar or XY square its values (an XY corner the one of its axes the end gives); a flip arrow clicked flips; a visibility, lookup or block table triangle clicked lists its values |
| anything else | none | |

The plate's line has `End 0 (0, 0)`, `End 1 (100, 0)` and `Middle 2 (50, 0)`; dragging grip 1 to (120, 0) leaves its end at
(120, 0, 0). A dimension's grips are AutoCAD 2021's, and dragging one rewrites its points of definition and draws its
picture anew, as DIMEDIT does. A dimension of 100 from (0, 0) to (100, 0), its line 30 below, dragged by its second origin
to (150, 10) measures 150 with its line still 30 below; dragged by an end of its line to (20, -60), its line runs 60 below,
and words placed by DIMTEDIT go down with it. Its words dragged to (60, -50) stand there, the line moved to run under them
where DIMTMOVE is 0 and a leader drawn to them where it is 1. A drag that would measure nothing, an origin onto the other,
is refused.

An MTEXT's grips are AutoCAD 2021's too. Its box is its defined width across and its defined height down, or with no
defined height the height of its lines. "II" over "II" in letters 10 high, 240 wide and attached top left at the origin,
has its grips at (0, 0), (240, 0), (0, -26.67) and (240, -26.67). The top right one dragged to (260, -12) leaves the
MTEXT 260 wide with a defined height of 14.67, located at (0, -12); the bottom left one dragged by the same leaves it 220
wide and 38.67 high at (20, 0). The words wrap anew at the new width. The box is never lower than one text height, the
side dragged staying at the point, and a box dragged to no width is refused. In a drawing before R2007, whose MTEXT
holds no defined height, a corner changes the width alone. The canvas draws them as AutoCAD 2021 shows them: the
location a square, the top right a triangle pointing along the MTEXT, the bottom left one pointing down, the bottom right
one pointing into its corner, and the top left a square where the location is elsewhere; the triangles turn with the
MTEXT. Each triangle is as large as a plain grip's square, 15 pixels across at the default GRIPSIZE of 5 and 25 at 10, and
is pink under the cursor and red once clicked, as the squares are.

An MTEXT in columns has the grips AutoCAD 2021's GetGripPoints gives it, each doing what its MoveGripPointsAt does, the
location kept. Two static columns 120 wide with a gutter of 20 and 100 high, attached top left at the origin, have the
location at (0, 0), the column width at (120, 0), the corner at (260, -100), the height at (0, -100) and the gutter at
(260, 0), the last column's top right. Dragged 50 to the right, the column width grip makes the columns 130 wide with no
gutter, as static columns keep their total of 260, and dragged 30 to the left 90 wide with a gutter of 80; the gutter
grip keeps the columns 120 wide and makes the gutter 70, the total 310, and dragged left it stops at no gutter; the
corner does that and sets the height; the height grip sets the height of the columns. Dynamic columns have the same
grips, the corner and the gutter grip at the last column the words fill, but the column width grip keeps the gutter, the
total following, and so does the corner. Dynamic columns of their own heights have no corner and no height grip: a
triangle under the middle of each column sets that column's height. Attached at the right or the bottom, the grips stand
at the other side and grow the other way: attached bottom right the column width grip is at (-120, 0), and 50 to the
right makes the columns 70 wide. A width or a height stops at the text height and a gutter at nought.

EXPLODE breaks an MTEXT into a TEXT for each run of each line, where the picture draws it and of its height, width
factor and slant, with `%%U`, `%%O` or `%%K` before the words of a run lined under, over or through, and a LINE for
the bar of each stack, all in the MTEXT's layer. The runs keep their colours and SHX fonts, as AutoCAD 2021's Explode
keeps them: in an MTEXT of blue, `{\C1;CD}` is a red TEXT, `\C256;` ByLayer, `\C0;` ByBlock and `\c16711680;` the true
colour 0,0,255, and the rest blue; `{\Ftxt.shx;CD}` is a TEXT in a text style `MtXpl_txt_shx` of txt.shx, which the
drawing is given if it has none, in the same undo step. A run in a TrueType font of its own (`\fArial|b1;`) is a TEXT
in the MTEXT's style, as in AutoCAD.

A dynamic block's custom grips are those AutoCAD 2021 shows, where the INSERT's values put them
([`DxfBlockReference.GetDynamicGrips`](xref:GeometryHelper.Dxf.DxfBlockReference.GetDynamicGrips)), each a `CadGrip` of a
custom kind (`CustomLinear`, `CustomPoint`, `CustomRotation`, `CustomFlip`, `CustomList`) carrying the core grip in
`Custom`; the window draws them in the dynamic grip colour, GRIPDYNCOLOR, of AutoCAD's shapes. A grip dragged, by
`GripDrag` or by the STRETCH of a grip edit, asks a value worked out in the block's own coordinates, which the library
takes to the parameter's value set and works the block out with, into an anonymous block still named by the dynamic block:
the panel of a Width of 100, 150 or 200 at (1 000, 500) dragged by its arrow to (1 180, 530) is 200 wide, one undo step
`GRIP`. While one custom grip is dragged, the STRETCH prompt previews the block worked out at the cursor, as AutoCAD
2021 redraws it: a copy of the INSERT in a drawing of its own, the drawing itself left as it is until the point is picked.
An XY parameter shows three grips, as AutoCAD does: its end sets Width and Height; the corner of base x and end y sets the
Height alone, and the corner of end x and base y the Width alone. AutoCAD moves the parameter's base point along the other
axis, reading a Width of 80 where the presenter keeps 100 for the first corner of a rectangle 100 by 50 dragged by (20,
10), and draws the block as the presenter does where no action follows the base point (probe GhDb1). A flip grip clicked
flips the block ([`CadDynamicBlocks.Click`](xref:GeometryHelper.Dxf.Viewer.CadDynamicBlocks)); a visibility, lookup or
block table grip lists its values, the one shown ticked (`ValuesOf`), and one chosen is set (`Choose`): a block table
lists its rows, each its cells parted by commas, "100.0000" and "200.0000" for a table of Width, and the row chosen sets
Block Table1 to its index and Width with it. The right-click menu of a
selection holding a dynamic block offers Reset Block, which runs RESETBLOCK. AutoCAD 2021 read every value the presenter set
so on the library's dynamic samples as the library reads it (96 values, 61 grip edits and 6 resets), and AUDIT found no
error.

## Model space through a viewport

In a layout, model space is worked in through a viewport as in AutoCAD: MSPACE (`MS`), a double click inside a viewport's
frame or the status bar's PAPER button enters it, and PSPACE (`PS`), a double click off every frame or the MODEL button
goes back to the paper. [`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument)`.EnterViewport(handle)` and
`LeaveViewport()` do the same in code, and `ActiveViewport` is the viewport worked through, null on the paper. While one
is, `WorkingSpace` is model space: the commands draw there, picks and snaps work in model units as the viewport shows them,
and the canvas draws the viewport's frame thick, the crosshair inside it alone. A line drawn through a viewport of
Layout1 goes into model space. Ctrl+R goes on to the next viewport (`NextViewport`). MSPACE and PSPACE typed are one undo step
each, named for the command, that U takes back to the viewport worked through before or the paper, as AutoCAD 2021's console
undoes them; so is one that only says "Already in model space.", "Already in paper space." or "There are no active Model space
viewports.", an empty step (the console undid six commands, those three included, one U each). On the Model tab both say
"** Command not allowed in Model Tab **" and make no step. CTAB, the layout shown, is read as `Enter new value for CTAB <"Model">:`
and set by a name in letters of either case, spelled as the layout spells it: one undo step named CTAB, where TILEMODE's is named
TILEMODE and LAYOUT Set's LAYOUT; a name that is no layout's, a pattern (`Lay*`), `.` and `?` too, is refused as AutoCAD refuses it,
`Cannot set CTAB to that value.` Find and Replace's Zoom to objects shows the layout of the objects as a tab clicked does, one undo
step. -VIEW Restore of a paper view from the Model tab shows the layout as part of the command's one undo step: one U takes back to the
Model tab, as AutoCAD 2021's console does. (It restores a paper view in the layout the view was saved in; the presenter keeps no
such layout in the VIEW record, so it shows the layout shown, else the first.)

The wheel, the middle button and ZOOM and PAN change the viewport's view, not the paper. ZOOM and PAN typed are one undo
step each, as AutoCAD 2021's console undoes ZOOM 2X, ZOOM 2X and -PAN one U each. The wheel's rolls and the middle
button's drags made one after another are one step together, however often the view is drawn again between them, as
AutoCAD combines consecutive real-time zooms and pans by default (UNDO Control Combine, UNDOCTL 61 holding its 16);
`SetViewportView(handle, center, height, name, realTime: true)` joins such a change to the one before. ZOOM Previous goes
back through views of the viewport worked through alone, as AutoCAD keeps a list for each viewport, for the paper of each
layout and for model space: a viewport never zoomed answers "No previous view saved." while another has views to go back
to. Each list keeps the last 20 views ZOOM, PAN, the wheel and the middle button left, as AutoCAD 2021's console, zoomed
30 times, gives 20 back; the wheel's rolls and the middle button's drags made before the view is still are one view of
the list, a viewport's view set by `SetViewportView` another. ZOOM Extents by a double click of the middle button
counts as typed.

Model space and the paper are zoomed and panned the same way: ZOOM, PAN, PLAN, the wheel and the middle button on the
paper, in model space and through a viewport whose display is locked are undo steps too, one each for what is typed (as
AutoCAD 2021's console undoes ZOOM 2X, ZOOM 2X, -PAN and ZOOM Extents one U each, in model space and on the paper), and
ZOOM Previous is a step of its own. A step of the view alone keeps no copy of the drawing: U moves the view back to the
part of the space it showed, and the drawing is not read again, so a wheel gesture in a drawing of a million entities
costs nothing. U and REDO move the ZOOM Previous lists with the views, as AutoCAD 2021's do: after ZOOM 2X three times, ZOOM Previous and U the last zoom is back and the next ZOOM Previous goes where the first one went; after ZOOM 2X three times and U, ZOOM Previous goes to the view before the second zoom, not to the one U left. Showing another space (a tab clicked, LAYOUT Set, TILEMODE; `CadDocument.ShowSpace(space)` in code) is a step too, named LAYOUT, which U and REDO take back and forward without reading the drawing again, the selection let go each time. `CadDocument.RecordSpaceView(space, view, before, name, realTime)` makes such a step and the list entry;
the canvas calls it once the view is still. A drawing so zoomed counts as changed, as AutoCAD's does (DBMOD 16).
ZOOM `nXP` sets its scale, n paper units to the model unit: `1/50XP` in a viewport 100 high shows 5 000 of model space,
1:50. A viewport whose display is locked keeps its view, and the paper is
zoomed instead. The status bar's scale list and Properties' Standard scale (1:1 to 100:1) and Custom scale (0.02) set the
view's height to the frame's over the scale, about the same centre; Display locked is a row of Properties too. The list
opens on a click of the scale, as AutoCAD 2021's: Scale to fit, which fits model space's extents to the frame, the scales
of the drawing's list (ACAD_SCALELIST; the standard ones where it has none), and Custom..., which runs SCALELISTEDIT.
SCALELISTEDIT shows AutoCAD 2021's Edit Drawing Scales: the drawing's list in its order, Add… and Edit… (Add Scale and
Edit Scale: Name appearing in scale list, Paper units = Drawing units), Move Up, Move Down, Delete and Reset, and the
scale chosen under the list, `1 paper unit = 50 drawing units`. Delete is greyed for a scale the drawing keeps (the unit
scale, the current annotation scale, one an object points at); Edit Scale greys the units of a scale an object points at,
as AutoCAD's help says. Their messages are AutoCAD 2021's, read from its resources: an empty name says `No name
specified for this scale.  Please enter a name.`, units that are not numbers `Invalid scale number.`, and Edit Scale
given a name another scale has `1:25 already exists.`. Add Scale given a name of the list asks `1:25 already exists.
Would you like to redefine this scale?` (two spaces after the full stop) and, on Yes, gives that scale the units; where an
object points at the scale, or it is the unit scale, it says `1:50 already exists and is a referenced scale. Please
enter a different scale name.`. Reset asks in AutoCAD's task dialog, `Scale List - Reset`, `Resetting will restore a
default list of scales for the drawing. Which default scale list do you want to restore?`, with the detail
`Unreferenced scales will be purged from the drawing and the default scales will be added. Any changes you made to the
scale list will be lost.` and the buttons Metric scales, Imperial scales, Metric and Imperial scales and Cancel; the list
chosen is the metric 1:1 to 100:1 (17 scales), the imperial 1:1 and `1/128" = 1'-0"` to `1'-0" = 1'-0"` (17), or the metric
ones then the 16 imperial ones that are not 1:1 (33, `CadScaleListRequest.MetricAndImperialDefaults`). Reset then deletes
every scale the drawing does not keep and adds the ones of that list it lacks. The list takes several scales chosen at
once (Ctrl and Shift), as AutoCAD's does: Delete is live while the choice holds a scale the drawing does not keep, Edit…,
Move Up and Move Down for one scale only, and the line under the list says `Varies: multiple scales selected.`. Delete
over a choice that holds a scale the drawing keeps asks `Scales in use cannot be deleted. Delete selected scales not in
use?` and, on Yes, deletes the others, the scale that stands where the first of them stood chosen then; over a choice that
would leave the list empty it says `The scale list must contain at least one scale.`. OK writes the list as it stands as one undo step: the scales taken out
deleted, the ones renamed or given other units changed, the new ones added, each in its place (Move Up and Move Down
move the entry of ACAD_SCALELIST, the SCALE and its handle kept); Cancel writes nothing. With no window to show the
dialog, SCALELISTEDIT asks -SCALELISTEDIT's prompts. VPSCALE writes `PS:MS == 1:20` and asks
a new scale, as 1:37.5. -SCALELISTEDIT asks as AutoCAD 2021's console does, word for word: `Enter option
[?/Add/Delete/Reset/Exit] <Add>:`; ? lists each scale with its paper and drawing units and the scale they give; Add asks
`Enter name for new scale:` (an empty name says `Must enter a valid name.` and one of more than 64 characters `Scale name
too long.`, and the name is asked again) and `Enter scale ratio:` (1:3 or 2.5:1; anything else answers `Invalid entry. Expects a ratio
entered in the following format: "1:1"`), and for a name the list has asks `GH already exists.  Redefine scale?`; Delete
takes one scale (`GH scale deleted.`, `Scale NOSUCH not found.`) or `*` every one in no use, and keeps the unit scale, the
current annotation scale and one an object points at (`Scale 1:1 is referenced so it cannot be deleted.`), and the last
scale of the list (`Scale list must contain at least one scale. 1:1 not deleted.`); Reset, after
`Reset scale list to defaults? [Yes/No] <No>:`, takes out every scale in no use and adds back the default list after the
ones kept: the metric 1:1 to 100:1, or, where the header's MEASUREMENT is 0 (imperial), 1:1 and the 16 imperial scales
`1/128" = 1'-0"` (0.0078125 to 12) to `1'-0" = 1'-0"`, as AutoCAD 2021's console resets them. A drawing with no
MEASUREMENT in its header takes the imperial list, as AutoCAD 2021 reads such a header as MEASUREMENT 0. The command is one undo step. AutoCAD's console accepts a ratio of 0:1, which the presenter refuses, and its Reset leaves 1:1
alone there, having no profile's default list. Beside it the padlock locks and unlocks the viewport's display, one undo step; a locked viewport's scale is grey and
its list greyed. On the paper with no viewport selected neither shows; on the Model tab the place shows the annotation
scale (CANNOSCALE), its list the drawing's scales, and MODEL is a label rather than a button.
The windows ask in AutoCAD 2021's own words, read from its task dialogs and messages. A block of another drawing, or one
typed in Block, whose name the drawing uses asks `Block - Redefine Block`, `"GB" is already defined as a block in this
drawing, would you like to redefine this block reference?` with Redefine block and Don't redefine "GB". New View over a name
used asks `View - Already Exists`, `Stair core already exists.  Do you want to replace it?` with Replace and Cancel. New
Layer State to Save over a name used asks `Layer - Overwrite Layer State`, `Do you want to overwrite Plan with the drawing's
current layer state?`. The Layer manager refuses to rename layer 0 or a layer of an xref with `Layer - Rename`, `The
selected layer cannot be renamed.` and the layers that cannot be: `Layer 0`, `Xref-dependent layers`. Write Block over a
file asks `C:\blocks\gb.dwg already exists.  Do you want to replace it?`, and the Text Style dialog, with a change not
applied, `The current style has been modified.` and `Do you want to save your changes?`. The question of the plugin outside
the trusted folders, and the one over a file that cannot be read, are the presenter's own.
[`CadViewports`](xref:GeometryHelper.Dxf.Viewer.CadViewports) has the arithmetic: the canvas's view in model units
(`ModelView`) and the view a viewport takes back from it (`ViewOf`), a box fitted to the frame (`Fit`), and the scales'
names. A viewport whose view is not a plan is not entered. One whose view is twisted, as DVIEW TWist leaves it (group 51),
is shown and worked through turned, as AutoCAD 2021 shows it: the view's centre is kept about its target and turned by
the twist (`ToView` and `FromView`, AutoCAD's TRANS from the world to the view), so through a viewport 120 by 80 about
(100, 100) at 1:5, its view about (100, 50) twisted 30 degrees, the point (100, 0) of model space is at (97.320, 100) on
the paper, where AutoCAD's console puts it. Entered, the canvas's view in model units turns with it (`CadView.Twist`):
picks, snaps and boxes (taken as turned polygons), the crosshair and the UCS icon follow the turn, PAN moves the view by
the model distance given, and ZOOM Window fits the box as the view turns it.

### Clipping a viewport

VPCLIP clips a viewport of the layout shown as AutoCAD 2021's does, word for word as its console prints them: `Select viewport
to clip:` (a line or a circle picked says `Object selected was not a viewport.` and asks again), then `Select clipping
object or [Polygonal] <Polygonal>:`. A closed polyline, circle, ellipse or closed spline picked becomes the border of the
viewport and the frame its box, the view kept in place (a viewport 200 by 100 about (110, 60) at 1:10, clipped to a circle
of radius 30 about (110, 60), is 60 by 60 there with its view height 600); an open polyline says `Clip object must be
closed.`, a viewport `Invalid: Clip entity not currently regen'ed.`, and the command asks `Select object to clip viewport:`
again. Polygonal asks `Specify start point:` and `Specify next point or [Arc/Length/Undo]:` (with Close from the second
segment on) as PLINE does, the arcs by their end point or Angle, CEnter, Direction, Radius and Second pt (`Enter an arc boundary
option`), and Enter closes the polygon with a straight edge; two points and Enter say `Clip polyline must have at least 3
vertices.` and change nothing. The polygon is a LWPOLYLINE on the current layer, which stays an entity of the layout; Delete,
for a viewport that is clipped (`Viewport does not have clip boundary associated.` for one that is not), erases the boundary
and leaves the frame as it was. Each run is one undo step, VPCLIP, and clipping a viewport again leaves the boundary it had
before in the layout. Worked through a viewport (MSPACE), VPCLIP writes
`Switching to Paper space.`, asks on the paper and goes back into the viewport, writing `Switching to Model space.`, as
AutoCAD 2021's console does. The viewport and its boundary are one object to a pick and to every command that takes objects, as
AutoCAD 2021 counts them (`2 found`): picking either selects both, ERASE of either erases both, MOVE, SCALE and COPY take
both (the copy of the viewport tied to the copy of its boundary), and the viewport follows its boundary whenever an edit
changes it, a corner dragged by its grip too: the frame is the box of the boundary again, its view keeping its scale and
its place. A clipped viewport has its middle as its only grip, which moves the boundary with it. The view is left as AutoCAD
2021 leaves it (probed in its console on a viewport 60 by 60 about (110, 60) showing (1 000, 500) at 1:10): MOVE and MIRROR
keep the view's centre, the frame going with its boundary, so that a pair moved by (20, 5) is about (130, 65) and shows what it
showed; SCALE by 2 about the origin scales the frame (120 by 120 about (220, 120)) and the height of its view (1 200) alike
and moves the view's centre to (2 100, 1 100); ROTATE turns the view by the angle (its twist, group 51, taken from 0 up to
360 degrees) and writes the view's centre as the turn of `2 V - C' - T` by the new twist, V the view's centre before, C' the
frame's centre carried by the rotation and T the view's target, whatever the scale of the view: turned 45 degrees about the
origin, the viewport above is about (35.355, 120.208), twisted 45 degrees, with its view centre at (767.107, 2 011.320), and
turned 90 degrees about its own middle it stays where it is, twisted 90 degrees, its view centre at (-940, 1 890). A copy
made by COPY, ROTATE Copy or MIRROR is tied to the copy of its boundary and fitted the same way. Copied (COPYCLIP) and pasted
(PASTECLIP) into another drawing the pair comes along whole, the copy of the viewport naming the copy of its boundary (group
340) and listed as its reactor, as AutoCAD 2021's `WblockCloneObjects` brings it; PASTEBLOCK does not take viewports, and
a paste into model space is refused. An image of the layout (IMAGEOUT, PNGOUT) and its plot to a PNG show the model only
inside the outline of the boundary, as the canvas does (no pixel of the model lies outside a circle clip, where an unclipped
viewport shows walls beyond it); a plot to a PDF is drawn by the same renderer. Through the viewport (MSPACE) the cursor, the picks and the snaps work inside the outline alone, the thick frame follows it,
and the viewport's grid is drawn inside it. [`CadViewportClips`](xref:GeometryHelper.Dxf.Viewer.CadViewportClips) gives
`WithPartners` and `WhyNotClip`.

### Clipping a block or an external reference

XCLIP (alias XC) clips the block references and external references of the selection as AutoCAD 2021's does, word for word as
its console prints them. `Select objects:` keeps the references, a MINSERT among them; any other object is `1 was ineligible
for clipping.`, and a selection of none says `None found.` and ends. Then `Enter clipping option [ON/OFF/Clipdepth/Delete/
generate Polyline/New boundary] <New>:`. ON and OFF switch the clip of each reference that has one, Delete takes it off
(the extension dictionary stays, as AutoCAD leaves it), and generate Polyline draws a closed polyline on the current layer
along it; each says `No clip boundary found.` and asks again where none of the references has a clip. Clipdepth, with no
clip, says `Xref must contain a clip boundary before specifying a clipdepth.`; else it asks `Specify front clip point or
[Distance/Remove]:` and `Specify back clip point or [Distance/Remove]:`, a distance from the boundary (a point on the
screen is on the boundary's plane, 0) or Remove, Enter keeping the plane, and a front plane behind the back one says `Front
clipping plane behind back clipping plane.  Clipdepth not applied.`. The planes are written and kept, and the flat display
leaves them out: a clip is drawn as its boundary alone.

New boundary asks `Delete old boundary(s)? [Yes/No] <Yes>:` where a reference has a boundary (No ends the command and
changes nothing), writes `Outside mode - Objects outside boundary will be hidden.`, and asks `Specify clipping boundary or
select invert option: [Select polyline/Polygonal/Rectangular/Invert clip] <Rectangular>:`. Rectangular asks `Specify first
corner:` and `Specify opposite corner:`, the sides along the axes of the current UCS. Polygonal asks `Specify first
point:` and `Specify next point or [Undo]:` until Enter, refusing a point that makes a side of no length (`Invalid point,
polygon segment is zero length.`) or crosses another (`Invalid point, polygon segments cannot intersect.`); fewer than three
points or no area say `Boundaries with zero area are not acceptable.`. Select polyline takes the vertices of a polyline or a
2D polyline, open or closed, its arcs left out, a rectangle of four corners square to the UCS kept as its two corners, and
says `Selected object is not a polyline.` for another object. Invert clip says `Inside mode - Objects inside boundary will be
hidden.` (and the first words again when chosen twice) and keeps the part cut away, as AutoCAD does: a polygon that goes out
along a slit from the vertex nearest the edge of the block's box, 5 per cent beyond its extents, round it against the turn of
the polygon, and back in, 4E-5 wide. A rectangle inverted on a block 100 by 100 at (1000, 500), scaled by 2 and turned by 30
degrees, is the eleven points AutoCAD wrote. The boundary is kept in the UCS that is current, with its matrices, so that
it goes with the reference when that is moved, turned, scaled or copied, as AutoCAD keeps it. Each run is one undo step,
XCLIP. The context menu of an external reference offers Clip Xref.

A clipped reference is drawn inside its boundary alone, on screen, in a plot and in the images the presenter writes:
every item the reference draws carries the region it is shown in ([`DisplayItem2.BlockClip`](xref:GeometryHelper.Dxf.Viewer.DisplayItem2),
a [`DisplayClipRegion`](xref:GeometryHelper.Dxf.Viewer.DisplayClipRegion)), within the region of a clipped block it stands
in, and the canvas clips to it. Of a clipped reference a pick, a window and a crossing meet only what is shown: a click on a
line of the block outside the boundary picks nothing, and a window round the boundary alone takes the reference. Each cell of
a MINSERT is clipped by its own place, and the attributes of the reference are clipped too. The frame of the boundary is drawn
over the reference in its colour while XCLIPFRAME (kept in the drawing, 0 to 2: AutoCAD's drawings write 2, a header that gives none reads 1) is not 0; at 2 it
is not plotted. It is picked as the reference. [`CadXClips`](xref:GeometryHelper.Dxf.Viewer.CadXClips) gives `WorldToClip`, `AsRectangle`, `Inverted` and `TryUninvert`. A tilted
UCS's boundary is drawn as its projection on the plane of the drawing.

EXPLODE of a clipped reference breaks the whole block, as AutoCAD 2021's does (probed on a rectangle, a polygon, a clip
turned inside out and a clip switched off): a block of a square 100 by 100 and a circle, clipped to a rectangle 40 by 50, gives
the whole square and the whole circle, the clip applied to neither and kept by no piece.

A selected reference that is clipped has, besides its insertion grip, the grips AutoCAD 2021's `GetGripPoints` gives it
(probed in accoreconsole, whatever the clip's state and XCLIPFRAME): an arrow at the middle of the boundary's first side
and a grip at each corner of the rectangle, from its lower left counter-clockwise, or at each vertex of the polygon, in
order. A corner dragged takes the two sides it is on and keeps the opposite corner, the rectangle staying a rectangle (the
corner (1020, 520) of the rectangle (1020, 520) to (1080, 570) dragged to (1010, 510) leaves (1010, 510) to (1080, 570); one
dragged past the opposite corner leaves the rectangle in order; one dragged onto a side of it, "Boundaries with zero area are
not acceptable."); a vertex of a polygon moves alone. The grip's point is put into the boundary's own system through the
matrices the clip keeps, so that a reference moved, turned or scaled after the clip is dragged where the cursor is. A clip
turned inside out is dragged the same way, its keyhole made anew about the box that holds the reference. Each drag is one
undo step, GRIP, and any of the grip modes works on a corner. The arrow, a blue triangle across the first side, points to
what the clip hides (down on the bottom side of a rectangle in Outside mode, up in Inside mode); a click on it turns the clip
inside out, or back, at once, one undo step, with no grip edit started: a rectangle of two corners becomes the eleven points
of its keyhole and returns to two corners. [`CadGrips.ClickClipFlip`](xref:GeometryHelper.Dxf.Viewer.CadGrips) does it, and
the grips are the kinds `ClipVertex` and `ClipFlip` of [`CadGripKind`](xref:GeometryHelper.Dxf.Viewer.CadGripKind).

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

### Hover menus

A polyline's grips are AutoCAD 2021's multi-functional ones. A vertex is a square, `2 × GRIPSIZE + 5` pixels across (15 at
the default of 5); the middle of a segment is a bar along it, 22 by 6 at 5 (37 by 10 at 10, 10 by 3 at 1), lying along the
chord of an arc segment, which is the tangent at the arc's middle; a rotated view turns the bar with its segment. A grip
under the cursor is pink (`#FF7F7F`, AutoCAD's GRIPHOVER) and a clicked one red, both unedged; the others are the grip
colour with a 1 pixel dark edge. The grip is hit within its own square, the bar within its length by the square's side. While the cursor is inside that box, with
no command running and no box or pan under way, the crosshair is drawn with its centre on the grip's point, as AutoCAD pulls it, the
status bar shows that point, and a click starts the grip edit at exactly it; leaving the box, the crosshair is under the pointer
again. The pointer of the operating system is not moved.
The other grips follow the same size and the same colours: the triangles of an MTEXT and of a table's width, height and
size are 15 across at the default and 25 at GRIPSIZE 10, the table's breaking triangle and the autofill diamond of a
selected cell (14 across at the default) grow with it too, and a dynamic block's custom grips and a table cell's grips
turn pink under the cursor, a custom grip red once clicked.

Once the cursor has rested 400 milliseconds on a grip, with GRIPMULTIFUNCTIONAL's bit 2 set, no command running and no
second grip hot, its menu opens 19 pixels right of the grip and 24 below it (left and above where it would leave the
canvas): a light popup, rows of 22 pixels, the row under the cursor highlighted, an item that cannot be chosen grey.

| Grip | Items |
|---|---|
| vertex | Stretch Vertex, Add Vertex, Remove Vertex |
| middle of a straight segment | Stretch, Add Vertex, Convert to Arc |
| middle of an arc segment | Stretch, Add Vertex, Convert to Line |

The menu closes when the cursor leaves both the grip and the menu, on Esc, on a click elsewhere, and when the selection
or the command changes. A click on the grip itself is the grip edit as before, `** STRETCH **`. Choosing a row calls
[`session.StartGripAction(handle, gripIndex, action)`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession); the items of a
grip are [`CadGripMenus.Of(document, handle, gripIndex)`](xref:GeometryHelper.Dxf.Viewer.CadGripMenus). Each item is one
undo step named GRIP, and Esc at its prompt changes nothing:

- Stretch Vertex is the grip edit, the command line saying `** STRETCH **` as a click does: AutoCAD 2021's table of the
  polyline's grip modes (probed in `acdb24res.dll`) gives Stretch Vertex that line, `** STRETCH VERTEX **` being the
  section line vertex's. Stretch moves a straight segment by the offset, both ends, or bends an arc segment through the
  point.
- Add Vertex says `** ADD VERTEX **` and asks `Specify new vertex point or [Base point/Copy/Undo/eXit]:` with the polyline
  drawn with the vertex at the cursor. The vertex goes after the vertex of the grip (after the last vertex of an open
  polyline it extends the end); a segment split keeps its widths, the width at the point being where the point falls along
  it. A straight segment splits in two straight ones; an arc segment in two arcs of the arc through its start, the new
  point and its end: a half circle from (0, 0) to (20, 0), given (10, -10), becomes two quarter circles of bulge 0.4142.
  Copy on keeps asking, each vertex after the one just made, until eXit, Enter or Esc, and Undo takes the last vertex
  made back while it runs: the polyline is read back as it was before that vertex, the segment it split with its bulge and
  widths, and the next vertex is asked after the one before. Undo with no vertex made yet changes nothing. The whole run
  stays one undo step.
- Remove Vertex takes the vertex out at once, and AutoCAD 2021 words it "Remove vertex, and replace neighboring segments
  with line": the segment that now joins its neighbours is straight, whether the segment either side of the vertex was an
  arc or not. It is grey on a polyline of two vertices, and a closed polyline of three keeps two.
- Convert to Arc says `** CONVERT TO ARC **` and asks `Specify midpoint of arc segment:`, the polyline drawn bent through the
  cursor; a chord of 100 with the point 20 off its middle gets a bulge of 0.4 (the sagitta over half the chord), above
  nought where the point is to the right of the way the segment runs. A point on the line of the segment is refused.
- Convert to Line makes an arc segment straight at once.

The static members are `CadGrips.AddVertex`, `RemoveVertex`, `ConvertToArc` and `ConvertToLine`, each one undo step.
Other grips (LINE, ARC, SPLINE, HATCH, dimension text) have no menu yet, and Ctrl cycling through a grip's options is not
built: Ctrl held as the first point is picked still turns Copy on.

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
| Dimension | Dim style; the Lines & Arrows, Text, Fit and Primary Units rows of the Dimension Style dialog's variables; Text override; Ext line fixed and Ext line fixed length (DIMFXLON, DIMFXL), Fill color, Fractional type, Text position X and Y, Text view direction | Measurement |
| MTEXT | Contents, Style, Justify, Direction, Text height, Rotation, Line space factor, distance and style, Defined width, Columns, Position | |
| Block reference | Position, Scale X, Y and Z, Rotation, each attribute by its tag, the Custom rows of a dynamic block | Name (a dynamic block's own for its anonymous copy) |
| Viewport | Center, Height, Width, On, Display locked, Standard scale, Custom scale | Clipped, Visual style |
| Multileader | Multileader style, Leader type, Arrowhead, Horizontal landing, Landing distance, Contents, Text style, Text height | |

Current vertex steps through a polyline's vertices and is no step of undo
([`CadSelection.CurrentVertex`](xref:GeometryHelper.Dxf.Viewer.CadSelection.CurrentVertex)): on the rectangle (0, 0)
(30, 0) (30, 20) (0, 20), vertex 3 shows Vertex X 30.0000 and Vertex Y 20.0000, and Vertex Y typed as 25 moves that corner
alone. A row of a dimension is written as an override of its variable, the style left as it is, and the dimension drawn
again: Precision 0.000 with Suppress trailing zeros No and Dim suffix " TYP" turns a dimension of 300 under ISO-25 into
"300.000 TYP". Arrow 1 makes the arrows separate (DIMSAH), the other end keeping the arrow it showed. Ext line fixed On
with a length of 10 draws, for points 30 from the dimension line, extension lines that start 10 short of it, as AutoCAD
2021 draws them. Text position X and Y move the words as DIMTEDIT moves them: under DIMTMOVE 0 a linear dimension's line
moves to run through the point, as AutoCAD 2021 moves it, so words of DIMTAD 1 at 50, 21.875 above a line at 20 set to X
30 keep the Y, the line moves up to 21.875 and the words stand above it again; under 1 and 2 the line stays. Angle, Scale
or Pattern name of a hatch keeps its origin, as AutoCAD keeps it: ANSI31 at (2, 0) turned
to 30 degrees still starts at (2, 0). A Major radius of 40
scales an ellipse of radii 20 and 10 whole, to 40 and 20; a Minor radius above the major is refused. A Height or Width of a
viewport keeps its scale, the view growing with it. A row of a list ([`CadPropertyRow.Choices`](xref:GeometryHelper.Dxf.Viewer.CadPropertyRow.Choices))
is a drop-down in the window, and the Contents row of one MTEXT offers its MTEXT editor.

A dynamic block reference has the Custom group between Geometry and Misc
([`CadProperties.CustomGroup`](xref:GeometryHelper.Dxf.Viewer.CadProperties.CustomGroup)), as AutoCAD's palette: each
property it shows and the visibility state shown holds, by its name. A visibility state, a lookup's row and a flip's state
(by the names its parameter gives them, Not flipped and Flipped) are drop-downs; a distance is typed to 4 places, an angle in
degrees. Its rows are set by the property's name through
[`CadDynamicBlocks.SetProperty`](xref:GeometryHelper.Dxf.Viewer.CadDynamicBlocks),
one undo step `Properties`, a value taken to the value set: the panel's Width typed 170 is 150. Misc's Name reads
the dynamic block's name, not that of the anonymous block the INSERT places.

## MATCHPROP

MATCHPROP (MA) asks for the source object, says the settings it copies ("Current active settings: Color Layer Ltype
Ltscale Lineweight Transparency Thickness PlotStyle Dim Text Hatch Polyline Viewport Table Multileader") and asks "Select
destination object(s) or [Settings]:". Settings shows the Property Settings dialog, whose check boxes are
[`CadCommandSession.MatchProperties`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession.MatchProperties), every one until
changed: with Layer alone checked, a red line matched to a line on Steel goes to Steel and stays red. The special
properties are copied between objects of one kind: a dimension's style and its overrides (drawn again), a text's or an
MTEXT's text style and height (and a text's oblique angle and width factor onto a text), a hatch's pattern, angle and scale
or spacing and double, or its gradient (name, angle, shift, tint and colours), the destination keeping its origin, a polyline's linetype generation and its width where the source has one width all along, a
viewport's scale, display lock and frozen layers, a table's table style and a multileader's style. Material, Shadow display
and Center object are greyed; Plot style is copied in a drawing of named plot styles and greyed where plot styles follow
the colour.

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
| `Rename(document, name, newName)` | the name, with every object on the layer; the spaces around it taken off, letters of another case taken | layer 0: `Cannot rename layer "0".`; a name used, either case: `A layer named "DIM" already exists.`; an empty name, a character a name cannot hold (&lt; &gt; / \ " : ; ? * \| , = and the backquote) or a layer of an xref: `Invalid layer name.` |

LAYERP (alias `2`, the Previous button of Home › Layers) puts back the last change or set of changes made to the settings of
layers, and nothing else: on or off, frozen, locked, plot, colour, linetype, lineweight, transparency and the current layer.
Repeated, it goes back one set at a time. A set is what one command does (LAYISO turning off nine layers and moving the
current one is one set), everything the Layer manager changed between its opening and its closing, or one change made on
its own, as the layer box makes one. A layer added, renamed or deleted is not a setting: it stays, and a layer renamed or
deleted since is left out. The words are AutoCAD 2021's: `Restored previous layer status.`, then where they apply `*Warning*
The current layer is turned off.`, `*Note* The previously current layer was not set because it no longer exists.` and `*Note*
The linetype of one or more layers could not be restored because the linetype is no longer loaded.`; with nothing kept
`*No previous layer status*`. LAYERP is one undo step, so U after it brings the change back, and the next LAYERP takes it
again; a layer edit undone with U is taken out of the sets with it. LAYERPMODE asks `Enter LAYERP mode [ON/OFF] <ON>:`: with
OFF changes are no longer kept and LAYERP says `Layer-Previous is disabled. Use LAYERPMODE to turn it on.`; the mode is
`CadDocument.LayerPreviousMode`, and `BeginLayerSet` and `EndLayerSet` make everything between them one set, as the Layer
manager does. At most 100 sets are kept. LAYERPMODE is a command in AutoCAD 2021 and not a system variable (SETVAR says
`Unknown variable name.`), and here too. The viewport overrides of a layer (VP Freeze and the rest) are not kept.

The Layer manager renames a layer in place: F2, a slow second click on the chosen name, a double click on it, or Rename
Layer (F2) of its right-click menu opens the name cell with the whole name chosen; Enter or a click elsewhere keeps the
name typed, as one undo step, Esc keeps the old one. Layer 0 and a layer of an xref are refused at once, the cell left
shut and the message shown (Rename Layer is greyed for them); a name used or one with a character a name cannot hold is
refused on Enter, the message shown and the cell left open with the name typed. The messages are AutoCAD 2021's -RENAME
ones, checked in its console; Defpoints is renamed, as -RENAME and entmod rename it there.

A layer turned off or frozen takes its entities out of the display lists; a locked layer keeps them drawn but out of any
selection a command gathers. `IsLocked(database, name)` asks.

### Layers in the viewports of a layout

While a layout is current the Layer manager shows AutoCAD 2021's VP columns: VP Freeze, New VP Freeze, VP Color, VP
Linetype, VP Lineweight, VP Transparency and VP Plot Style. They act on the viewport worked through, else on the one
viewport selected on the paper, and are grey with neither; on the Model tab they are hidden. A value that differs from the
layer's own in that viewport alone is marked blue. VP Plot Style reads `Color_7` and is grey in a drawing plotted by colour
(a CTB table), as AutoCAD's. Its right-click menu has "VP Freeze Layer in All Viewports", "VP Thaw Layer in All Viewports",
"New Layer VP Frozen in All Viewports" and "Remove Viewport Overrides for", on the layers chosen or all, in the current
viewport or all; and "Isolate Selected Layers", which turns off every layer but those chosen, the current one too where it
is not chosen, one undo step, as AutoCAD's help describes the item. [`CadViewportLayerEditor`](xref:GeometryHelper.Dxf.Viewer.CadViewportLayerEditor) makes these edits, each
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
