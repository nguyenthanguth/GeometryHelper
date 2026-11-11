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
besides the one shown, each about 330 MB at that size, and an edit made while one is read shares the processor with it.

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

[`CadCommandRegistry.CreateDefault()`](xref:GeometryHelper.Dxf.Viewer.CadCommandRegistry) makes the registry of 181 commands, named as AutoCAD names them, and of the system variables SETVAR sets:

| Group | Commands |
|---|---|
| view | ZOOM (Window, Extents, All, Previous, and a factor as `2` or `0.5x`; `nXP` through a viewport), PAN, REGEN, PROPERTIES, OPTIONS, QSAVE, MSPACE, PSPACE, VPSCALE, SCALELISTEDIT, -SCALELISTEDIT (?, Add, Delete, Reset, Exit), VIEW, -VIEW (?, Delete, Orthographic, Restore, Save, sEttings, Window) |
| edit | MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO |
| draw | LINE, PLINE (Arc, Close, Halfwidth, Length, Undo, Width; arcs by Angle, CEnter, Direction, Radius, Second pt), CIRCLE (3P, 2P, Ttr, and Tan, Tan, Tan as 3P with `_tan` before each pick), ARC (three points; Start, Center, End, Angle or chord Length; Center first; Start, End, Angle, Direction or Radius; Enter to go on from the last line or arc; Ctrl held to draw the other way), RECTANG, ELLIPSE (Arc), POINT, XLINE, RAY, POLYGON, DONUT, TEXT (Justify, Style; Enter goes on below the last text), MTEXT (Height, Justify, Line spacing, Rotation, Style, Width, Columns), REVCLOUD (Arc length, Object, Rectangular, Polygonal, Freehand, Style, Modify), MLEADER, MLEADERSTYLE, LEADER (Annotation: Copy, Block, None, Mtext; Format: Spline, STraight, Arrow, None; Undo), QLEADER, MVIEW, LAYOUT (New, Rename, Set, ?) |
| inquiry | DIST, AREA (Add area, Subtract area), ID, LIST, MEASUREGEOM (Distance, Radius, Angle, ARea) |
| layers | -LAYER (Set, New, ON, OFF, TRansparency, Plot, Freeze, Thaw, LOck, Unlock, Description, stAte: ?, Save, Restore, Edit, Name, Delete, Import, EXport), LAYERSTATE, LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK, LAYISO, LAYUNISO, VPLAYER (?, Freeze, Thaw, Reset, Newfrz, Vpvisdflt), LAYVPI |
| utilities | SELECTALL, UNITS, -UNITS, SCRIPT, SETVAR (?) |
| modify | TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN, LENGTHEN, PEDIT (Close, Open, Join, Width, Edit vertex, Fit, Spline, Decurve, Ltype gen, Reverse, Undo; Multiple; a 3D polyline's Spline curve too), OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, ARRAYRECT, ARRAYPOLAR, -ARRAY, ALIGN |
| blocks | BLOCK, -BLOCK, INSERT, -INSERT, WBLOCK, -WBLOCK, ATTDEF, -ATTDEF, EATTEDIT, ATTEDIT, -ATTEDIT, ATTSYNC (?, Name, Select), BATTMAN, BASE, BEDIT, -BEDIT (?), BSAVE, BSAVEAS, BCLOSE (Save, Discard, Cancel), BAUTHORPALETTE, RESETBLOCK |
| text and clipboard | TEXTEDIT, MTEDIT, COPYCLIP, CUTCLIP, COPYBASE, PASTECLIP, PASTEBLOCK, PASTEORIG, PASTESPEC |
| plot | PLOT, -PLOT, PREVIEW, PAGESETUP, PNGOUT, JPGOUT, BMPOUT, EXPORT, -EXPORT (Dwf, dwfX, Pdf) ([Plotting](plot.md)) |
| dimension | DIM (Angular, Baseline, Continue, Ordinate, aLign, Distribute, LAyer, Undo), DIMLINEAR (Mtext, Text, Angle, Horizontal, Vertical, Rotated), DIMALIGNED (Mtext, Text, Angle), DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMARC (Mtext, Text, Angle, Partial, Leader), DIMORDINATE (Xdatum, Ydatum, Mtext, Text, Angle), DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK, DIMJOGLINE (Remove), DIMJOGGED (Mtext, Text, Angle), DIMREASSOCIATE (Select object), DIMDISASSOCIATE, -DIMDISASSOCIATE |
| tables | TABLE, -TABLE (Style, Width, Height), TABLESTYLE, TABLEDIT |
| selection | QSELECT, FIND, DRAWORDER (Above objects, Under objects, Front, Back), TEXTTOFRONT, GROUP, -GROUP (?, Order, Add, Remove, Explode, REName, Selectable, Create), UNGROUP, CLASSICGROUP |
| housekeeping | PURGE, -PURGE (Blocks, Dimstyles, Groups, LAyers, LTypes, MUltileaderstyles, Plotstyles, SHapes, textSTyles, Tablestyles, Visualstyles, Regapps, Zero-length geometry, Empty text objects, All) |
| references and images | XATTACH, IMAGEATTACH, XREF, EXTERNALREFERENCES, IMAGE, -XREF (?, Bind, Detach, Path, pathType, Unload, Reload, Overlay, Attach), XOPEN, -IMAGE (?, Detach, Path, Reload, Unload, Attach), IMAGECLIP (ON, OFF, Delete, New boundary), TRANSPARENCY |

A dimension's picture is laid out from its style as AutoCAD 2021 lays it out: DIMTAD puts the words on the line, which
is broken about them by DIMGAP (0), above it (1, and JIS, 3), on the side away from the points measured (2) or below it
(4); DIMJUST puts them in the middle, next to an extension line or over one; and where the words and the arrowheads do
not both fit, DIMTIX, DIMSOXD, DIMATFIT and DIMTOFL say what moves outside, DIMTIH and DIMTOH whether the words stay
level. With ISO-25's sizes a dimension of 100 has its words "100" in the middle, one of 5 its words inside and its
arrowheads outside, and one of 7 its arrowheads inside and its words 3.9 past the second extension line. The line starts
at the back of each arrowhead, 2.5 in from its tip, but for the ticks, Dot small, Dot small blank, Integral and None,
which take no room and never move out. Words on the line (DIMTAD 0) fit between the arrowheads with two gaps each side:
"9" of ghsimplex, 1.548 wide, moves out of a dimension of 9. Under DIMATFIT 3 words wider than the arrowheads' room stay
inside and the arrowheads go out: "12" of ghsimplex widened 1.5 times, 5.536 wide, stays between extension lines 12
apart. DIMTVP lifts the words of DIMTAD 0 by that many times their height, the line
broken about them below 0.7; a DIMGAP below nought draws a box round the words, the gap clear of them.

DIMTMOVE says where words go that the fit moves out or DIMTEDIT puts by hand. Under 0 they stand beside the line, which
DIMTEDIT moves to run through the point given, the words above it there (on it for DIMTAD 0, the line broken a gap
clear of them, and run on to a gap short of them past an end). Under 2 the fit puts them over the line, away from the
points, DIMEXE, one and a half DIMGAP and half their height out (their height more where they stand below it as they
read): "4" moved out of a dimension of 4 stands 5.9375 below the middle of its line; DIMTEDIT puts their middle at the
point. Under 1 the same place is a leader's end, the leader from the middle of the line a third as far along again, and
a landing on to the words; DIMTEDIT draws a leader to words further from the line than DIMGAP and half the heights of
the words and the arrowheads, 3.15 with ISO-25's sizes, the words starting at the point, and none to words nearer.
DIMTEDIT Left and Right put the words an arrowhead and a gap in from the extension line on that side as they read (the
top of a vertical line is its left), with a gap about them: "100" of Arial on a line at 30 degrees stands 6.733 in. Under
DIMTIH they stand level, centred on the line, which is broken about them: 6.895 in on that line.

DIMLINEAR asks `Specify dimension line location or [Mtext/Text/Angle/Horizontal/Vertical/Rotated]:` and DIMALIGNED
`[Mtext/Text/Angle]`, as AutoCAD 2021: Mtext and Text ask for the words, Angle turns them (`Specify angle of dimension
text:`), and Rotated sets the line's angle (`Specify angle of dimension line <0>:`): 0,0 to 100,0 rotated 30 measures
86.6025. Words turned by Angle stand centred on the line whatever DIMTAD says, the line broken where their turned box
and the gap cross it: "100" 5.25 wide and 2.5 high turned 45 breaks it 2.65 each side of the middle. Every dimension
made writes the middle of its words (group 11) and its measurement (42), as AutoCAD writes them with its block.
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
the centre. On the line (DIMTAD 0) a radius's words take the arrowhead and two gaps more to fit. A diameter's words on its line (DIMTAD 0) take a gap each side and, with
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
stand at the point under 2; a radius made under 1 or 2 keeps its line through the point the circle was picked at.
A radius put inside under 1 keeps its words as DIMATFIT 3 does, whatever DIMATFIT; under 2, words on its line over its
centre or its rim put its arrowhead outside, pointing in, and its line runs whole from the centre to the rim: "R20" put 1
out. Put 8 out they leave the arrowhead inside and the line broken about them.
The words are measured in the font of the dimension's text style, laid out as the canvas lays out an MTEXT, as AutoCAD
measures them: "R20" of arial.ttf 2.5 high is 5.965 wide and 2.553 high, "100" of ghsimplex 6.071 wide and widened 1.5
times 9.107. A TrueType font is read from the Fonts folders of Windows, an SHX font where the fonts set on the document
find it, the canvas's own ghsimplex wherever fonts are on; the style's width factor and oblique angle count. Words of a
font not found take 0.7 of their height a letter. DIMARC (DAR) dimensions the
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
dimension. Its options run the commands of their names, and LAyer puts the dimensions made next on a layer.
A new dimension takes the drawing's current DIMSCALE, not its style's, and writes it as an override where the two
differ, as AutoCAD does: DIMSCALE 10 typed with ISO-25 current draws the words of a dimension 25 high and its arrowheads
25 long, and DIMBASELINE puts each next line 37.5 further out.

A dimension made with DIMASSOC 2 (the default) whose points were picked with an object snap is associative, as in AutoCAD
2021: DIMLINEAR, DIMALIGNED and DIM tie each origin snapped with ENDpoint, MIDpoint, CENter, QUAdrant, NODe or NEArest to
its line, arc, circle, polyline segment or point (both ends where a line is selected), and one snapped with INTersection to
the two lines, arcs, circles or polyline segments that cross there. A point snapped with PERpendicular (to a line, an
arc, a circle or a polyline segment) or TANgent (to an arc, a circle or an arc of a polyline) is tied with the point it
was drawn from, as AutoCAD writes it: the second origin from the first, the ends of an angle of three points from its
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
of an arc its two ends and center, of two lines the four ends, of three points those snapped, DIMORDINATE its feature;
DIMCONTINUE and DIMBASELINE tie their first point to what the last dimension's second (or the base's first) was tied to,
and QDIM the ends of lines, the vertices of polylines and the centers of circles, all kept in a DIMASSOC as AutoCAD keeps
it. Every edit
that changes those objects, whatever command or grip makes it, carries the dimension with it in the same undo step: a line
from (0, 0) to (100, 0) moved by (10, 5) takes its dimension, 20 above, whole to (10, 5), (110, 5) and a line at 25; its end
stretched to (120, 30) moves the second origin there and measures 120, the dimension line staying 20 up, as STRETCH moves
the origins and the line point of a linear dimension inside its window as AutoCAD's does; the same end dragged by its grip,
the dimension not stretched with it, leaves the dimension line 20 beyond the origin nearer it, at 50, as AutoCAD 2021
leaves it (30 for an end at (120, 10), 20 for one at (120, -30)). STRETCH of a polyline's vertex keeps the height of an arc
off its chord, as AutoCAD's does: the half circle from (100, 200) to (100, 300) whose end goes to (100, 320) takes a bulge
of 100 / 120, its center at (89, 260); a circle of 25
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
first cell; the row is an undo step of its own. Five columns 63.5 wide and
four rows of data of AutoCAD's Standard make a table 317.5 wide and 56 high, its title 11 high and its other rows 9.
A table is drawn with its first row in the Title cell style of its table style, its second in the Header and the rest in
the Data, each look a cell does not give of its own; where the table or its style leaves the title out, the first row is
the header, and where the header is left out too, every row is data, as AutoCAD 2021 reads such a table.
-TABLE asks the columns, the rows of data and the point (Style, Width, Height) on the command line, and TABLEDIT a cell
to type in; a double click or F2 on a cell runs it. TABLESTYLE (TS) shows the Table Style dialog
([`CadTableStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableStyleEditor)); a style changed draws its tables anew. A
click in a cell selects it with the Table Cell tab ([`CadTableEditor`](xref:GeometryHelper.Dxf.Viewer.CadTableEditor)):
rows and columns put in and taken out, cells merged and unmerged, their alignment, fill and borders, the arrows moving
the cell and Delete emptying it, each one undo step that draws the table anew in its block. Drawn anew, each row grows to
hold its cells, as AutoCAD lays a table out: a cell's words wrap at the cell's width less its margins, as an MTEXT of that
width, and need their actual height and the margins above and below, one line of four thirds of the text height at
least; a row is as high as its tallest cell needs and is never made lower. In AutoCAD's Standard, "HEA300 S355 with end
plates both sides" in ghsimplex in a column 30 wide takes six lines and makes its row 45.43 high, as AutoCAD 2021 laid it
out, and the row keeps 45.43 when the words are made short again. A cell written in a table of 100 rows of 20 columns
is written and drawn in about 50 ms.

MLEADERSTYLE (MLS), and the last item of the multileader style box of the Annotate tab, show the Multileader Style
Manager ([`CadMLeaderStyleEditor`](xref:GeometryHelper.Dxf.Viewer.CadMLeaderStyleEditor)): Set Current, New…, Modify…
and Delete, which is refused for Standard, the current style and a style a multileader is drawn in. A drawing of no
multileader style is given Standard first, metric: arrows 2.5, a break of 3.75, a landing of 8, text 2.5 high and a
landing gap of 2. Modify writes the Leader Format, Leader Structure and Content tabs into the drawing's MLEADERSTYLE and
gives every multileader of the style its values, those it has of its own kept: a style's text made 2.5 high leaves a
multileader whose words were set 6 high at 6. MLEADER draws in the current style (CMLEADERSTYLE, else Standard): a
multileader of a style of words and arrows 4 high has words and arrows 4 high. Each change is one undo step. Block
content is not offered, as MLEADER places words only, and the annotative box is greyed.

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
[Tolerance/Copy/Block/None/Mtext] <Mtext>:`: Copy makes an MTEXT of a text or an MTEXT picked, Block places an INSERT as
INSERT does, None leaves the leader pointing to nothing, and Mtext opens the MTEXT editor; Tolerance, which needs the
Geometric Tolerance dialog, is refused. The leader is drawn by the current dimension style and DIMSCALE: its MTEXT stands
DIMGAP past the last point, to the right where the last segment runs right or straight up or down, and a hook DIMASZ
long runs on to it where that segment is steeper than 15 degrees: a leader from (0, 0) to (10, 10) with DIMASZ 2.5 ends
(7.5, 10), (10, 10), its words middle-left at (10.625, 10) with DIMGAP 0.625, or bottom-left at (10.625, 10.625) with
DIMTAD 1, as ISO-25 has it. The dimension variables other than the
style's are written as the leader's overrides, as AutoCAD writes them: DIMSCALE 2 gives `1070 40, 1040 2` and words twice
DIMTXT high. QLEADER (LE) draws by the drawing's Leader Settings, which Settings or Enter at `Specify first leader point,
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
square onto the nearest ray, (100, 30) held level from the origin being (100, 0); Copy an Object stands the copy by its
middle; Block Reference asks the block and INSERT's prompts; None and Tolerance (which needs the Geometric Tolerance
dialog, greyed in Leader Settings) end the leader with no annotation; Reuse Next keeps the next words, the settings then
turned to Reuse Current, which copies them to each later leader with nothing asked. A leader arrow other than the
style's is written as the leader's DIMLDRBLK override, `1070 341, 1005` and the handle of `_Dot`. LEADER and QLEADER are
each one undo step.

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
typed form otherwise): the named objects nothing uses by kind, with Confirm each item, Purge nested items, the geometry
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
angle, a hatch pattern's, the new angle of ROTATE's Reference) is from ANGBASE and, with ANGDIR 1, clockwise: with
ANGBASE 90 and ANGDIR 1, `@100<30` ends at 50, 86.6. A turn (ROTATE's angle, an arc's included angle, a block's
rotation, CHAMFER's and LENGTHEN's angles, the angles of a polar array, UCS Z) follows ANGDIR only, so 30 at ROTATE turns
30 degrees clockwise; a point picked there turns by its direction less ANGBASE. An obliquing angle follows neither.
`<<` before the angle reads it in degrees from the X axis counter-clockwise whatever the settings (`@100<<30` ends at
86.6, 50), and `<<<` the same in the drawing's angle units. The angles DIST, LIST, the Properties palette, the tip by the
cursor and the coordinate readout write are counted the same way, east as 90 there; what the drawing keeps stays
counter-clockwise from the X axis. `CadUnits.ToDirection` and `FromDirection` are the two conversions, and
`session.CountAngle(radians)` the second with the drawing's settings.
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
Unreferenced. A nested one is opened from the palette but not unloaded, reloaded, detached or given a path. The window
watches the folders of the files read; one changed on disk is said once on its drawing's command line, where AutoCAD
2021 shows a balloon over its status bar: "External Reference Files Have Changed: grid needs reloading. Reload it with
-XREF Reload or in the External References palette." It is said again only once the reference was reloaded and its file
changed anew.
Unload flags the reference's layers and linetypes 16 and its block 4, as AutoCAD 2021 writes them unloaded, and Reload
flags them 48 and 36 again and writes those of layers and linetypes the file has gained, each an undo step where anything
changes; a record the drawing has keeps what it has. Detach takes them out with the block. No DXF says a reference is
unloaded. One
not found shows its path at its insertion point. -XREF ? prints AutoCAD 2021's table, and Bind is refused. XOPEN asks
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
LEVEL then GRID, their values kept. Move Up and Move Down are greyed on a constant attribute, as AutoCAD's are. Another
block chosen in the list, or Select block, with changes not yet written asks first, as AutoCAD's manager does:
`Save changes to the attributes of block GB?`, Yes writing them and No dropping them. AutoCAD has no command-line form of BATTMAN, and with no window it says so. ATTDIA 1
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
The Visibility State box of the Block Editor tab is live for a block of visibility states
([`CadBlockEditor.VisibilityStates`](xref:GeometryHelper.Dxf.Viewer.CadBlockEditor.VisibilityStates)): the first is shown
when the block opens, and another chosen (`CadDocument.SetBlockEditorVisibilityState`) hides what another state lists and
it does not, which is then neither drawn nor picked; an object drawn in the editor joins every state. Choosing a state is a
view of the block, no undo step. RESETBLOCK puts the dynamic block references selected back to their blocks' own values,
one undo step, and says `2 blocks reset.`, counting every block reference selected as AutoCAD 2021 counts them.
PROPERTIES, OPTIONS, LAYER and QSAVE ask the window for its panel or dialog (`UiRequested`), and say "is shown by the window"
where none is listening. The window adds CLOSE, NEW, SAVEAS, PROPERTIESCLOSE, GRID, SNAP, ORTHO, POLAR, OSNAP, ISOPLANE, LAYMCUR and LAYMCH, and the plugin commands. The inquiry commands print in AutoCAD's words: DIST from (0, 0) to (100, 100) says `Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 0`; AREA by points or by an object says `Area = 5000.0000, Perimeter = 300.0000`. Each command is a
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
| the header of the drawing, written as AutoCAD writes it | CLAYER, CECOLOR, CELTYPE, CELWEIGHT, CELTSCALE, LTSCALE, PSLTSCALE, MSLTSCALE, DIMSCALE, DIMASSOC, CMLEADERSTYLE, MLEADERSCALE, TEXTSIZE, TEXTSTYLE, FILLETRAD, CHAMFERA, CHAMFERB, PDMODE, PDSIZE, MIRRTEXT, SORTENTS, IMAGEFRAME, CANNOSCALE, INSBASE, LIMMIN, LIMMAX | one undo step |
| the document or the session | PICKSTYLE, SELECTIONCYCLING, PICKFIRST, PICKADD, ATTREQ, ATTDIA, FILEDIA, CMDDIA, PEDITACCEPT, PLINETYPE, REVCLOUDAPPROXARCLEN, REVCLOUDARCVARIANCE, REVCLOUDCREATEMODE, OFFSETDIST, TILEMODE (the space shown) | at once, no undo step |
| the window: the status bar, the snaps and Global settings | OSMODE, POLARANG, POLARMODE, POLARADDANG, AUTOSNAP, DYNMODE, PICKBOX, APERTURE | at once, no undo step; kept in the settings file |
| the window's toggles of the drawing shown | ORTHOMODE, SNAPMODE, GRIDMODE, LWDISPLAY | at once, no undo step; written into the drawing, as AutoCAD saves them there |
| read only | DIMSTYLE, EXTMIN, EXTMAX, UCSNAME, UCSORG, DWGNAME, DWGPREFIX, DATE, CDATE | |

FILLETRAD 5 writes `$FILLETRAD` 40 `5` into the header and U takes it back to 10. OSMODE 4133 ticks Endpoint, Center,
Intersection and Extension, and 16391 ticks Endpoint, Midpoint and Center with OSNAP off. With no window, ORTHOMODE and
LWDISPLAY are the drawing's `$ORTHOMODE` and `$LWDISPLAY`; the window answers them, and SNAPMODE and GRIDMODE, with the
toggles of the drawing shown, and writes each switched into that drawing: ORTHO into `$ORTHOMODE`, the lineweights into
`$LWDISPLAY`, SNAP and GRID into its `*Active` viewport, as AutoCAD saves them with the drawing (see the status bar in
[the app](app.md)).
PICKFIRST 0 makes a command that works on objects ask `Select objects:` whatever is selected before it: with a line
selected, MOVE asks for objects rather than its base point. PICKADD 0 makes what is picked last the selection, Shift
adding to it, at `Select objects:` and on the canvas: a click on line A, then on line B, leaves B alone; 1, the default,
and 2 add, Shift taking out. ATTREQ 0 makes INSERT give each attribute its default without asking; ATTDIA 1, 0 by
default, makes it ask them in the Edit Attributes dialog where there is a window. DYNMODE keeps -3 to
3, negative while DYN is off, so that F12 brings the same value back; the one box of dynamic input shows both pointer and
dimensional input whichever of 1 to 3 is set. The window keeps PICKBOX, APERTURE, LWDISPLAY and POLARANG in
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
| polylines | each vertex, the middle of each segment | a vertex moves; the middle of a straight segment moves it, of an arc segment bends it |
| TEXT, INSERT | the insertion point | moves it |
| MTEXT | the location and the corners of its box (top left, top right, bottom left, bottom right; the location first where it is at none of them); the location alone for an MTEXT of no width or in columns | the location moves it; a corner takes its two sides of the box to the point, which sets the defined width and height, the location staying at the point of the new box the attachment names |
| VIEWPORT | four corners, the middle | a corner resizes the frame, the opposite corner, the scale and the centre of the view kept |
| linear and aligned DIMENSION | the two extension line origins, the two ends of the dimension line, the words | an origin measures anew, the dimension line kept where it is; an end of the line moves the line through the point; the words move as DIMTEDIT moves them |
| radius and diameter DIMENSION | the chord point (a diameter's two), the words | a chord point goes round the circle towards the point |
| angular DIMENSION | the vertex and the two line points (an angle between two lines: the ends of both lines), the arc, the words | a point measures anew; the arc runs through the point |
| ordinate DIMENSION | the feature, the leader's end | moves that point |
| dynamic INSERT | the insertion point, then its custom grips | a linear arrow sets the distance along its parameter, a rotation circle the angle about its base, a point, polar or XY square its values; a flip arrow clicked flips; a visibility or lookup triangle clicked lists its values |
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
holds no defined height, a corner changes the width alone.

EXPLODE breaks an MTEXT into a TEXT for each run of each line, where the picture draws it and of its height, width
factor and slant, with `%%U`, `%%O` or `%%K` before the words of a run lined under, over or through, and a LINE for
the bar of each stack, all in the MTEXT's text style, layer and colour. A run in a font or colour of its own (`\f`,
`\C`) becomes a TEXT in the MTEXT's style and colour, as in AutoCAD.

A dynamic block's custom grips are those AutoCAD 2021 shows, where the INSERT's values put them
([`DxfBlockReference.GetDynamicGrips`](xref:GeometryHelper.Dxf.DxfBlockReference.GetDynamicGrips)), each a `CadGrip` of a
custom kind (`CustomLinear`, `CustomPoint`, `CustomRotation`, `CustomFlip`, `CustomList`) carrying the core grip in
`Custom`; the window draws them in the dynamic grip colour, of AutoCAD's shapes. A grip dragged, by `GripDrag` or by
the STRETCH of a grip edit, asks a value worked out in the block's own coordinates, which the library takes to the
parameter's value set and works the block out with, into an anonymous block still named by the dynamic block: the panel of
a Width of 100, 150 or 200 at (1 000, 500) dragged by its arrow to (1 180, 530) is 200 wide, one undo step `GRIP`. A flip
grip clicked flips the block ([`CadDynamicBlocks.Click`](xref:GeometryHelper.Dxf.Viewer.CadDynamicBlocks)); a visibility or
lookup grip lists its values, the one shown ticked (`ValuesOf`), and one chosen is set (`Choose`). The right-click menu of a
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
Layout1 goes into model space. Ctrl+R goes on to the next viewport (`NextViewport`).

The wheel, the middle button and ZOOM and PAN change the viewport's view, not the paper. ZOOM and PAN typed are one undo
step each, as AutoCAD 2021's console undoes ZOOM 2X, ZOOM 2X and -PAN one U each. The wheel's rolls and the middle
button's drags made one after another are one step together, however often the view is drawn again between them, as
AutoCAD combines consecutive real-time zooms and pans by default (UNDO Control Combine, UNDOCTL 61 holding its 16);
`SetViewportView(handle, center, height, name, realTime: true)` joins such a change to the one before. ZOOM Previous goes
back through views of the viewport worked through alone, as AutoCAD keeps a list for each viewport, for the paper of each
layout and for model space: a viewport never zoomed answers "No previous view saved." while another has views to go back
to. Each list keeps the last 20 views typed ZOOM and PAN left, as AutoCAD 2021's console, zoomed 30 times, gives 20 back.
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
as AutoCAD's help says; a name another scale has is refused. Reset asks first, then deletes every scale the drawing does
not keep and adds the default ones it lacks. OK writes the list as it stands as one undo step: the scales taken out
deleted, the ones renamed or given other units changed, the new ones added, each in its place (Move Up and Move Down
move the entry of ACAD_SCALELIST, the SCALE and its handle kept); Cancel writes nothing. With no window to show the
dialog, SCALELISTEDIT asks -SCALELISTEDIT's prompts. VPSCALE writes `PS:MS == 1:20` and asks
a new scale, as 1:37.5. -SCALELISTEDIT asks as AutoCAD 2021's console does, word for word: `Enter option
[?/Add/Delete/Reset/Exit] <Add>:`; ? lists each scale with its paper and drawing units and the scale they give; Add asks
`Enter name for new scale:` and `Enter scale ratio:` (1:3 or 2.5:1; anything else answers `Invalid entry. Expects a ratio
entered in the following format: "1:1"`), and for a name the list has asks `GH already exists.  Redefine scale?`; Delete
takes one scale (`GH scale deleted.`, `Scale NOSUCH not found.`) or `*` every one in no use, and keeps the unit scale, the
current annotation scale and one an object points at (`Scale 1:1 is referenced so it cannot be deleted.`); Reset, after
`Reset scale list to defaults? [Yes/No] <No>:`, takes out every scale in no use and adds back the default list after the
ones kept: the metric 1:1 to 100:1, or, where the header's MEASUREMENT is 0 (imperial), the 16 imperial scales
`1/128" = 1'-0"` (0.0078125 to 12) to `1'-0" = 1'-0"`, as AutoCAD 2021's console resets them (a drawing with no MEASUREMENT
takes the metric list). The command is one undo step. AutoCAD's console accepts a ratio of 0:1, which the presenter refuses, and its Reset leaves 1:1
alone there, having no profile's default list. Beside it the padlock locks and unlocks the viewport's display, one undo step; a locked viewport's scale is grey and
its list greyed. On the paper with no viewport selected neither shows; on the Model tab the place shows the annotation
scale (CANNOSCALE), its list the drawing's scales, and MODEL is a label rather than a button.
[`CadViewports`](xref:GeometryHelper.Dxf.Viewer.CadViewports) has the arithmetic: the canvas's view in model units
(`ModelView`) and the view a viewport takes back from it (`ViewOf`), a box fitted to the frame (`Fit`), and the scales'
names. A viewport whose view is not a plan is not entered. One whose view is twisted, as DVIEW TWist leaves it (group 51),
is shown and worked through turned, as AutoCAD 2021 shows it: the view's centre is kept about its target and turned by
the twist (`ToView` and `FromView`, AutoCAD's TRANS from the world to the view), so through a viewport 120 by 80 about
(100, 100) at 1:5, its view about (100, 50) twisted 30 degrees, the point (100, 0) of model space is at (97.320, 100) on
the paper, where AutoCAD's console puts it. Entered, the canvas's view in model units turns with it (`CadView.Twist`):
picks, snaps and boxes (taken as turned polygons), the crosshair and the UCS icon follow the turn, PAN moves the view by
the model distance given, and ZOOM Window fits the box as the view turns it.

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
