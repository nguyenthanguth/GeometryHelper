# Hatches, leaders and dimensions

Hatches and their patterns, leaders and multileaders, and dimensions: the annotation of a drawing.

## Hatches

A HATCH is a [`DxfHatch`](xref:GeometryHelper.Dxf.DxfHatch). Its loops are given in the world as the library's shapes: `hatch.GetLoopAt(0).Polyline` is the
[`GeoPolygonArc3`](xref:GeometryHelper.Geometry.GeoPolygonArc3) of a loop of a polyline, and `Curves` the lines, arcs, circles, ellipses and splines of a loop of edges,
each running the way the loop runs, a part of an ellipse as the [`GeoSpline3`](xref:GeometryHelper.Geometry.GeoSpline3) that is exactly it.
`AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.External, square))` adds one from shapes in the hatch's plane, and
`AppendLoop(type, points, bulges)` one of AutoCAD's vertices and bulges in the plane's own system; a loop read from a hatch
is written back as it was read. A change to the loops of an associative hatch takes its association away with the handles of its boundary objects,
and takes the hatch out of their reactors, as AutoCAD's does. A loop is spliced into the bytes of the record, so that three
thousand islands appended one at a time take half a second. `Area` is exact on arcs, ellipses and splines, the islands taken
off as `HatchStyle` says: a square 30 by 20 round a circle of radius 5 fills 521.46, and a loop given twice counts as the
even-odd rule counts it, a square given twice filling nothing. `GetPlane()` is the plane of its normal at its elevation and
`GeometricExtents` the box of its loops.

### Patterns

`SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID")` fills it solid, and any other pattern of AutoCAD's file is
written by its name with a count of no lines (78 of 0), which AutoCAD 2021 refuses a file without, unless
`PatternSearchPath` finds it (below); `PatternScale` and `PatternAngle` scale and turn
the lines of its pattern with them (`GetPatternDefinitionAt`), and a pattern made by the user (`_USER`, `PatternSpace`,
`PatternDouble`) is given its lines as AutoCAD makes them. `SetGradient` fills it with one of AutoCAD's gradients from
R2004, its colours [`DxfGradientColor`](xref:GeometryHelper.Dxf.DxfGradientColor)s, and `EvaluateGradientColorAt(0.5f)` is the colour halfway along it by the
library's rules, red, green and blue mixed in proportion, not checked against AutoCAD: AutoCAD's new gradient, blue to a
pale yellow, gives (128, 128, 204). `GetHatchLinesData()` gives the lines `Explode()` draws, to the bit, as [`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2)s in
the plane's own system, and `NumberOfHatchLines` and `GetHatchLineDataAt(i)` the same one by one, worked out once while
the hatch is not changed: the square 30 by 20 of a pattern made by the user, lines at 45 degrees 3.175 apart, draws 11. A
file older than R2000 has no HATCH and refuses one.

### Pattern files

A drawing reads no pattern file unless it is told where to look, and the library carries none of AutoCAD's.
`DxfDatabase.PatternSearchPath` takes folders separated by semicolons, searched in their order as AutoCAD searches its
support path, and is empty when a drawing is opened or made: empty, nothing is read and a pattern of AutoCAD's file holds
no lines, as above. Given the folders of the caller's own pattern files, `SetHatchPattern` gives the hatch the lines of the
pattern as AutoCAD 2021 gives them (check P4c):

```csharp
DxfDatabase db = DxfDatabase.Open("plan.dxf");
db.PatternSearchPath = @"C:\Users\me\AppData\Roaming\Autodesk\AutoCAD 2021\R24.0\enu\Support;D:\Patterns";
var hatch = new DxfHatch();
hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);
db.ModelSpace.AppendEntity(hatch);   // one line at 45 degrees, offset (-2.245, 2.245); Explode() gives 11 LINEs
```

- The file is acadiso.pat in a drawing of millimetres and acad.pat in one of inches, as `Measurement` says (a header of
  no `$MEASUREMENT` is read as millimetres, though AutoCAD 2021 opens such a file as inches), or a file named after the
  pattern, `GRID.pat` for GRID: a pattern of AutoCAD's file is looked for in the drawing's file first, a custom one in its
  own file first. Each file is read once, from the first folder that has it, and the first pattern of a name in it is taken,
  whatever its case. The hatch is written of the type of the file the pattern is found in, its name in capitals, as AutoCAD
  writes it.
- Each line is turned by `PatternAngle` and scaled by `PatternScale` about the origin of the plane: its angle the file's
  and the turn, from 0 up to 360, its base point turned and scaled, its offset, across it in the file, turned by its angle
  and scaled, its dashes scaled. Setting either writes the lines anew from the file: `0, 0,0, 0,2` at scale 3 and 90
  degrees is the line at 90 degrees, offset (-6, 0). `PatternDouble` adds no line to such a pattern, as AutoCAD adds none.
- A hatch in no drawing is given its lines when it is added to one; a copy of a hatch of no lines is not. A hatch read
  from a file keeps the lines the file holds; given the pattern of the name it has again, it keeps them, or is given them
  where it holds none.
- A file is read as AutoCAD 2021 reads one. A pattern begins at a line whose first character is `*`, its name all before
  the first comma; comments from a `;`, blank lines and the blanks about numbers are passed over, commas running together
  count as one, and a comma at the end of a line is a dash of nought. A pattern AutoCAD refuses is refused: a line of
  fewer than five numbers or more than six dashes, a number it cannot read (`2x`, `1e400`), or a pattern of no line; so is
  one refused in the first file that has it, not looked for in the other. A pattern found nowhere, or refused, is left with
  no lines, said once at Debug, or at Warn for one refused and a file that cannot be read; nothing throws.
- Every pattern of AutoCAD 2021's acad.pat and acadiso.pat, at scale 1, 2.5 at 30 degrees and 0.5 at -45 degrees, 918
  hatches, gives the lines AutoCAD gives to 1E-13; on squares off the origin AutoCAD draws the library's hatch, and
  `Explode()` breaks it, into AutoCAD's own LINEs, 84 034 of 84 034, and AUDIT finds nothing to fix. On squares whose edges
  the pattern's lines run along, AutoCAD's own hatch and its drawing of the library's differ by a few LINEs on those edges
  (82 956 of 83 063 the same), and the library's `Explode()` by a few more (82 774).
- Writing the lines takes 0.02 ms for ANSI31 and 0.4 ms for GRAVEL, of 41 lines, on .NET 10, each file read once; with
  no folders a pattern is set as fast as before.

## Leaders and multileaders

### Leaders

A LEADER is a [`DxfLeader`](xref:GeometryHelper.Dxf.DxfLeader), a curve through its vertices (`AppendVertex`, `SetVertexAt`, `VertexAt`) that points to its
`Annotation`, an MTEXT, a TOLERANCE or an INSERT of the drawing, which lists the leader among its reactors, as AutoCAD keeps
it; a leader of fewer than two vertices is refused by `AppendEntity`, as AutoCAD refuses one. `Dimasz`, `Dimgap`, `Dimclrd`
and the other overrides read the extended data of ACAD (`DSTYLE`) where the leader has them and its dimension style where it
has not, and are written there as AutoCAD writes them: `leader.Dimasz = 3.5` writes `1070 41, 1040 3.5`. A straight leader
is walked as a curve by the index of its vertices, 1.5 halfway along its second segment; a splined one (`IsSplined`) along the
spline `Explode()` fits through its vertices, its parameter run over the lengths of its chords as AutoCAD 2021 runs it, and
its `Spline` is that spline: through (0, 0, 0), (10, 10, 0) and (20, 10, 0) it runs from 0 to 24.14 and rises to y = 10.43,
past its vertices, but its `GeometricExtents` is the box of its vertices, (0, 0, 0) to (20, 10, 0), as AutoCAD gives it. The
spline is fitted once and kept until the drawing is next changed, so that a hundred points along a leader of 4 000 vertices
take a fraction of a millisecond, not 1.6 s. `GetGeometry()` gives the leader's curve, the polyline of its vertices or that
spline, as [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader) reads a LEADER, and `IntersectWith` meets the leader on it, as AutoCAD's does: the leader from
(0, 0, 0) to (10, 5, 0) meets the line x = 5 at (5, 2.5, 0). An INSERT of a block holding a leader is met on it too, and its
`GeometricExtents` hold it: the block holding that leader, inserted at (100, 0, 0) scaled 2, is met by the line x = 110 at
(110, 5, 0). `IsPlanar` agrees with `GetPlane()`: false where a vertex lies off the plane of its normal.
`GetDimstyleData()` gives every variable the leader is drawn with, its override else its style's, as a
[`DxfDimStyleTableRecord`](xref:GeometryHelper.Dxf.DxfDimStyleTableRecord) in no table; `SetDimstyleData(style)` writes those that differ from what the leader reads as
overrides, a block by its handle, so that `plain.SetDimstyleData(styled.GetDimstyleData())` gives one leader the arrow and
gap of another, and a leader given its own data changes nothing. `SetFromStyle()` takes the overrides away, the list DSTYLE of
its extended data, so that a leader of `Dimasz` 3.5 over the style Standard reads Standard's 2.5 again, and answers true, as
a dimension's does, where its drawing has the style it names; one of a style its drawing has not, and any other object,
answers false and changes nothing. `SetPlane(plane)` gives the leader the plane's normal
and moves each vertex straight onto the plane, the direction of its hook laid in it: the leader through (0, 0, 5),
(10, 10, 5) and (20, 10, 8) put in the floor runs through (0, 0, 0), (10, 10, 0) and (20, 10, 0), and a leader put in its own
plane changes nothing.

### Multileaders

A MULTILEADER is a [`DxfMLeader`](xref:GeometryHelper.Dxf.DxfMLeader): its words (`MText`, `TextLocation`, `TextHeight`) or block (`BlockContent`,
`BlockPosition`, `BlockScale`, `BlockRotation`, `SetBlockAttribute`), its look, and its leaders and their lines by AutoCAD's
indices (`AddLeader`, `AddLeaderLine`, `GetVertex`, `SetVertex`). A line's last vertex is the point its leader's dogleg
starts at, which every line of the leader ends at: `mleader.GetLastVertex(0)` is that point, and `SetLastVertex` moves it
for them all; where the dogleg is not drawn, its `EnableDogleg` false or its leader lines splined, the last vertex is the end
the dogleg would have, which the lines run on to, as AutoCAD 2021 reads it: a dogleg of 8 from (90, 48, 0) taken away
leaves the line ending at (98, 48, 0). A leader added by `AddLeader` has no landing yet, as AutoCAD 2021 writes it (290 of
nought): it draws no dogleg, a line added to it has no vertex, and the first vertex given a line of it (`AddFirstVertex` or
`AddLastVertex`) becomes its landing, the content left where it is; words at (100, 50, 0) given a second leader whose line
is given (-20, 80, 0), its last vertex then set to (40, 70, 0), stand at (160, 40, 0), as AutoCAD's own. `RemoveFirstVertex`
and `RemoveLastVertex` take a line down to one vertex, as AutoCAD 2021 does: the line from (0, 0, 0) to words at (100, 50,
0) is left (90, 48, 0) by the first, drawn as its dogleg alone, and (0, 0, 0) by the last, its dogleg drawn on to (8, 0,
0), the words left where they are. A line keeps that one vertex, which AutoCAD crashed taking out of a drawing it had read,
and the last vertex of a leader of two lines, which both end at, is not taken out, as AutoCAD refuses it. `SetDogleg` lands a
leader of no landing, or one whose dogleg it turns, on the side of the content the dogleg runs from, on the left of words
or either side of a block, so that a vertex given a line of it then goes before the landing: against x, the box at
(100, 50, 0) is met from (128, 55, 0). A property set is written in the MULTILEADER and in its context data, where AutoCAD
keeps both, and sets its bit of the overrides of the multileader style; set to the value it has, it sets the bit alone,
as AutoCAD 2021 sets it, but the colour of the words, the content, the block, the direction of the attachment, the
annotation scale, the places and the style, which change nothing, and the block's scale and turn, which lay the block out
again as any other value does. `new DxfMLeader()` takes the drawing's multileader
style when it is appended, with the style's linetype of the leader lines (341), its arrow block (342) and the text style of
its words (343, and 340 of the words, Standard where the style names none), as AutoCAD 2021 writes them; so does
`MLeaderStyle` set, where the multileader does not override them (bits 2, 8 and 11), a style of no arrow block taking the
multileader's away. `ArrowSize` set is written anew even where it is the size the multileader has, its bit set and the
arrows drawn its size times the `Scale`, as AutoCAD writes it: 5 at a scale of 2 draws them 10. `TextAttachmentType` is the
left side's, as AutoCAD's; the right is set by `SetTextAttachmentType`.

### Sizes

The sizes are read and set unscaled, as AutoCAD 2021's API reads and sets them, and drawn times the `Scale`: `TextHeight`
and `LandingGap` the context's 41 and 145 over the scale, `ArrowSize` its 140 over the scale (else the multileader's own
42, which a line of no size of its own reads), `DoglegLength` the first leader's dogleg over the scale (else the
multileader's own 41) and `BlockScale` the multileader's own 10, the block drawn times the scale (16). Words drawn 8 high
with a gap drawn 4 at a scale of 2 read 4 and 2, and a gap of 3 set there is drawn 6. `BlockRotation` reads the
multileader's own turn (43), as AutoCAD reads it, the block drawn with the turn of the context data (46). Words given by
`MText` to a multileader in a drawing give it their text style (343, and 340 of the words), with its bit of the overrides
(11) where it was not the multileader's, as AutoCAD 2021 takes it: words that name none are of Standard, and words given
back as `MText` read them change nothing. Words that name a text style, given to a multileader in no drawing, keep it when
the multileader is added to a drawing that has it, as AutoCAD 2021 keeps the words of one made in memory; words that name
none take its style's. Words given set bit 18 of the overrides, and their colour becomes the multileader's `TextColor`, with
bit 15 but for BYBLOCK, as AutoCAD 2021 writes them (check D38): an MTEXT made in code, BYLAYER, makes the text colour
BYLAYER, and red words make it red; read back by `MText`, the words carry their red. A new multileader, of words, a block
or none, runs its leaders on to its content (`ExtendLeaderToText`, 295 of 1) in a drawing of R2013 or later, and writes
no 295 in an older one; a new leader line has no colour of its own (no 92), drawn in `LeaderLineColor`, and reads
`GetLeaderLineColor` BYBLOCK whatever that colour is; a line given BYBLOCK writes its flag (93 2) and no 92, as AutoCAD 2021
writes them (check D38b). `TextAlignmentType` is read from the context's 176, as AutoCAD reads it: words lined up on the left
and mirrored across the YZ plane read on the right. A new multileader is of words (`ContentType` `MTextContent`, 172 of 2)
though it holds none (`HasContent` false), as AutoCAD 2021 makes one; its first leader line is landed at its point, its
dogleg towards the base point, the line of that one vertex: from (30, 40, 0), a dogleg along -x from there.

### Words and landings

Words given put the base point of the content (10 of the context data), where AutoCAD lands a leader it adds, where a
leader from the left would meet them, as AutoCAD 2021 puts it where no leader is landed yet (check D38b): the landing gap
short of the place given and as far below as the left's attachment says, which needs no font. "ABC" 4 high given at
(100, 50, 0), met at the middle of the top line, put it at (98, 48, 0), so that AutoCAD, adding a leader to the library's
file, lands it from (90, 48, 0) and leaves the words; "Ag" of Arial on a multileader of txt.shx stays at (100, 49.92, 0),
landed at (90, 47.33, 0), where AutoCAD moved the words before. Words held at their top centre or right have their left edge
at the place given and line the content up so (176), as AutoCAD 2021 puts them: "ABC" of txt.shx held at the top right
stands at (110.67, 50, 0) where `FontSearchPath` measures it, and is met from (90, 48, 0) on the left and from
(120.67, 48, 0) on the right. Words given anew where no leader is landed take the base point with them; words given after
their leaders move to meet the first, at (10, 2, 0) for a line landed at (0, 0, 0). Words turned in the plane turn it with
them where no leader is landed yet, its x axis their direction, as AutoCAD 2021 turns it: "ABC" turned half round make the
x axis (-1, 0, 0) and the base point (102, 52, 0), are met from (250, 0, 0) by a dogleg from (110, 52, 0) along -x, and move
as words along the plane do.

### Adding leader lines

`AddLeaderLine(point)` lands a new leader where AutoCAD 2021 lands it, where it can without a font: from a point on the
left of words held at their top left and met on their sides, the dogleg runs along the plane's x axis to the landing gap
short of the words' left edge, as far below their top as the attachment of the left side says (none at the top of the top
line, half the height of the words at its middle, all of it at its bottom and a fifth more under it, and the middle, the
middle of the bottom line, the bottom and a fifth more under it as the lines stand, 5/3 of the height times the spacing
apart, counted by their breaks). Words "ABC" 4 high held at (100, 50, 0), with a gap of 2 and a dogleg of 8, met at the
middle of their top line, are met by a leader from (0, 0, 0) whose dogleg runs from (90, 48, 0) to (98, 48, 0); three
lines met at their middle, from (90, 41.33, 0). A block is met on the side of its box the point is on, with no gap: the
dogleg ends where the block's shapes meet the line along x through the middle of its box (or through its position, where
`BlockConnectionType` is its base), the outermost such point on that side, else that point of the line. The box of lines
from (-10, -5) to (20, 15) placed at (100, 50, 0) is met from (0, 0, 0) by a dogleg from (82, 55, 0) and from (250, 0, 0)
by one from (128, 55, 0); a circle of radius 10 with a line across it, by doglegs ending at the circle, (90, 50, 0) and
(110, 50, 0), though the line reaches further right. AutoCAD lands words on the right as far right as its font makes them
wide, and decides which side by their middle, so a point on the right of their left edge, and words turned off the plane,
held at their top centre or right with no font to tell their width, or met at their top or bottom keep the old landing: the
first leader, or one made at the base point of the content, its dogleg ending there for a point on its left, as AutoCAD 2021
lands a leader it adds (check D38b).

The content keeps its landings as AutoCAD 2021 keeps them when something is set afterwards. `TextLocation` set moves the
doglegs with the words: the words moved from (100, 50, 0) to (120, 80, 0) move that dogleg to (110, 78, 0). `LandingGap`
moves the words along x by as much as the gap grew, with leaders or none, and the doglegs on their right by twice it: a gap
of 5 for 2 moves "ABC" to (103, 50, 0) and a dogleg on its right from (120.67, 48, 0) to (126.67, 48, 0). `DoglegLength`
and `SetDoglegLength` move the words or block away from the one leader that meets it, on one side, and, where leaders meet
it on both sides or several on one, land those leaders anew instead: a dogleg of 15 moves words met on the right to (93,
50, 0), and leaves words met on both sides where they are, the doglegs then starting at (83, 48, 0) and (127.67, 48, 0);
of two leaders landed at (150, 38, 0) on the left of words at (160, 40, 0), the second given 15 starts at (143, 38, 0). `SetLastVertex` (and
`SetVertex` of the last vertex) carries the content and every other leader's landing with the landing it moves: words met
from (90, 48, 0), the vertex set to (50, 60, 0), stand at (60, 62, 0). `TextHeight` and `Scale` move words met on their left
only (to (100, 53, 0) for a height of 10, to (110, 52, 0) at a scale of 2), and an attachment set moves them up or lands
the leaders on the right anew by as much more as that side is met below their top. Words given anew by `MText` keep their
landing rather than take the place given. Where AutoCAD would land the words by their font's width (words met on their
right whose height or scale changes), the words are left where they are. `SetFromStyle()` moves nothing. Words mirrored
across their plane, which run against its x axis and say so (294 of 1, as AutoCAD writes them and the library keeps them
readable), are moved by these as AutoCAD 2021 moves them, held at their right and their up the other way (check D38): "ABC"
held at (110.67, 50, 0), met from (90, 48, 0), stands at (117.67, 50, 0) for a dogleg of 15, at (113.67, 50, 0) for a gap
of 5, and at (60.67, 72, 0) for the vertex set to (40, 70, 0). What keeps their left edge as they grow, their height, the
scale and the words given anew (at (126.67, 53, 0) 10 high, 26.67 wide from 100), needs their width, and is done where
the drawing's `FontSearchPath` measures them; with no font they are left, as before. Words of no leader meet the base point
again at a scale of 2, at (102, 52, 0), and met at the bottom line on the left, at (100, 52.8, 0), as AutoCAD 2021 moves
them; met only on their right, the left set puts the base point where it now meets them (check D38b).

### Laying leaders out again

Each of these lays the leaders out again as AutoCAD 2021 does, so a leader moved off its landing (by `SetLastVertex` of
another, say) is landed anew on the left of the words or on either side of a block: of two leaders on the left of "ABC" at
(160, 40, 0), the second moved off to (40, 70, 0), a dogleg given to either, `DoglegLength`, `LandingGap`, `TextHeight` or
`Scale` lands it from (150, 38, 0) again. Words met by several leaders on their left move under `Scale` by the gap alone,
to (162, 42, 0) at a scale of 2, their doglegs from (142, 38, 0). A block keeps the place where a leader from the left meets
it, the base point of the content: `BlockScale` and `BlockRotation` move the block so that it is met there still, with
leaders or none, and land the leaders anew, the box at (100, 50, 0) met at (90, 55, 0) standing at (110, 45, 0) scaled by
2, its leader on the right from (158, 55, 0); `BlockPosition` moves that point and every dogleg with the block, a dogleg
from (82, 55, 0) to (112, 65, 0) for the box moved to (130, 60, 0). Set the block's scale and turn before `BlockContent`
for the block to stay where `BlockPosition` puts it. The block's matrix (47) is written as its transform where the scale,
the turn or `TransformBy` is set, and left where the block is moved, by `BlockPosition` or with a landing, as AutoCAD 2021
leaves it. `BlockRotation` turns the block from the x axis of the multileader's plane: 30 degrees on a multileader turned a
quarter draws it turned 120.

Attachment 8, the underline of every line, underlines the words as AutoCAD 2021 writes it: where a leader meets them on a
side of that attachment, "ABC" is written `\LABC`, whether the attachment, the leader or the words come last; words that
begin with `\L` or have no letters are left, and the attachment set away from 8 takes the `\L` off again.

### Doglegs

The length of the doglegs is written as AutoCAD 2021 writes it: the
multileader's own (`DoglegLength`, 41) unscaled, each leader's (40) times the `Scale`, `GetDoglegLength` the leader's over
the scale (nought for a leader met at the top or bottom of the words), so that Standard's 8 at a scale of 2 is 8, drawn
16. `Scale` scales the width and the height of the words too (43 and 44), but for words in columns, whose width, columns
and gutter AutoCAD keeps, their height alone scaled; a block is drawn scaled too, and, met by one leader, moves so that the
leader's longer dogleg meets it at its end, the dogleg's start kept: the box of lines from (-10, -5) to (20, 15) at (100,
50, 0) met from the left stands at (118, 45, 0) at a scale of 2; met on both sides it stays and its leaders are landed anew.
`TransformBy` leaves the scale and the multileader's own sizes as they are, as AutoCAD does: scaled by 2 the multileader is
still of scale 1, its 41 still 8 and 42 still 4, its doglegs drawn 16 and its arrows 8, which `DoglegLength` and
`ArrowSize` read, as AutoCAD reads them; its block is drawn twice the size with `BlockScale` still 1 (10 left) and its
matrix written as the block's transform where it stands. A `TransformBy` that scales, by 2 or by 0.5, turned or mirrored
too, marks the scale as the multileader's own (bit 24 of its 90), as AutoCAD 2021 marks it; a move, a turn or a mirror
alone does not. A block turned half round about an axis of its plane is drawn
mirrored, its normal turned over and its scales kept, as AutoCAD writes it; a block mirrored is drawn where the mirror puts
it, its normal mirrored with it, its x scale negative and its turn read from its y axis, the multileader's plane of the
mirrored normal, an x axis on the side the old one keeps and its y across them, as AutoCAD writes them: the box turned 30
degrees and mirrored across the XY plane is of normal (0, 0, -1), scales (-1, 1, 1) and turn 330 degrees. Words mirrored
are carried as ezdxf carries them; AutoCAD keeps them readable by their width. A mirror that turns the plane's x axis back
(across the YZ plane, or a plane of normal (1, 0.2, 0)), of a block or of words kept readable, lines the content up the
other way, the context's alignment (176) of the left made the right's and the right's the left's, a centred one kept, and
sets bit 14 of the overrides, as AutoCAD 2021 writes it (check D38); mirrored back, the alignment is the left's again and
the bit stays; a block's says so in 294, 1 and then 0 again (check D38b). Of the two ways to read words kept readable, the
library keeps the one turned, in the system of their normal, nearer the turn they had, as AutoCAD 2021 keeps them (check
D38b): across the XZ plane, or a plane of normal (0.2, 1, 0), "ABC" met from (0, 0, 0) runs the mirrored way, upright, met
again by the mirrored leader at its attachment, standing at (100, -46, 0) for a landing at (90, -48, 0); across the XY plane
it runs against x, as above; turned a quarter first, across XY, it runs up y still.
`GetGeometry()` gives the lines of its leaders, as `DxfReader` reads a MULTILEADER, and `IntersectWith` meets the
multileader on them: each leader line a [`GeoPolyline3`](xref:GeometryHelper.Geometry.GeoPolyline3) from its first vertex, where its arrow points, through its vertices
and the last point of its leader to the end of the dogleg, the path `Explode()` draws; splined, a `GeoSpline3` fitted
through those points with the tangents `Explode()` fits with. Both start at the arrow's point, where `Explode()` starts the
line at the back of the arrow, so that a splined line and the spline exploded part by up to about the arrow's size near the
arrow (0.585 on the multileader below, splined, its arrow 4 long). The line from (780, -10, 0) through (790, -5, 0) to its
dogleg from (790, -1.25, 0) to (798, -1.25, 0) is met by the line y = -3 at (790, -3, 0) and by the line x = 794 at
(794, -1.25, 0). Its words or block, its arrowheads and invisible leaders are not met. An INSERT of a block holding a
multileader is met on its lines too.

### Multileader styles

The multileader style a multileader is drawn with is a [`DxfMLeaderStyle`](xref:GeometryHelper.Dxf.DxfMLeaderStyle), as AutoCAD's `MLeaderStyle`: the styles of
`db.MLeaderStyleDictionary` are read as the file holds them (`TextHeight`, `ArrowSize`, `LandingGap`, `DoglegLength`,
`LeaderLineType`, `LeaderLineLinetype`, `TextStyle`, `Block` and the rest of its look), and
`mleader.GetOverridedMLeaderStyle(style)`, AutoCAD's `getOverridedMLeaderStyle` named as the library names its members, gives
a style made by `new DxfMLeaderStyle()` the style's values with the multileader's own in place of those it overrides (the
bits of its 90), as ezdxf's `MLeaderStyleOverride` reads them: a multileader of Standard with words 2.5 high of its own gives
`TextHeight` 2.5 and Standard's arrows, 4 long. The height of the words and the landing gap are read from the context data,
scaled, the dogleg from the multileader's own 41 and the arrow size from its own 42, unscaled. `SetFromStyle()` goes
the other way: the multileader takes its style's values, its words 4 high again, and its bits are set to nought, but for
those of what it cannot take (a linetype or a text style the style names none of or its drawing has not, a content it does
not hold, an arrow or a block a copy in no drawing cannot be given, the attachments of the top and bottom in a file older
than R2010) and of the properties the library does not write (bits 18, 26 and 30). Its sizes are the style's scaled once by the
style's scale: Standard scaled 2 gives words 8 high, a gap of 4 and arrows and doglegs drawn 8 and 16 long. A dimension, a
leader or a multileader of a style its drawing has not answers false and changes nothing. One leader line is given a look
of its own by
`SetLeaderLineType`, `SetLeaderLineColor`, `SetLeaderLineWeight` and `SetArrowSize(leaderLineIndex, value)`, from R2010:
its groups 170, 92, 171 and 40 after its index and the flags 93 of those it overrides, in the order of the DXF reference;
their `Get` give the line's own where it has one and the multileader's where it has not, and `Explode()` draws the line with
its own type, colour, linetype, lineweight and arrow size: a line splined of its own is a SPLINE among straight lines, one
invisible of its own gives nothing, and a size of its own, unscaled as `ArrowSize` is, is drawn times the `Scale`. A look
set to the one the line has is written and flagged all the same, as AutoCAD 2021 flags it. `SetPlane(plane)` puts the multileader in a plane: the plane of its context data the
one given, its x axis laid in it, every point moved straight onto it and every direction laid in it, the matrix of its block
turned with it, the words of a mirrored multileader still read from behind the plane; its words at (800, 0, 0) put in the
plane z = 5 are at (800, 0, 5), and a multileader put in its own plane changes nothing, the leaning axes a shear leaves
among what it keeps.

## Dimensions

A DIMENSION is a [`DxfRotatedDimension`](xref:GeometryHelper.Dxf.DxfRotatedDimension), [`DxfAlignedDimension`](xref:GeometryHelper.Dxf.DxfAlignedDimension), [`DxfRadialDimension`](xref:GeometryHelper.Dxf.DxfRadialDimension), [`DxfDiametricDimension`](xref:GeometryHelper.Dxf.DxfDiametricDimension),
[`DxfLineAngularDimension2`](xref:GeometryHelper.Dxf.DxfLineAngularDimension2), [`DxfPoint3AngularDimension`](xref:GeometryHelper.Dxf.DxfPoint3AngularDimension) or [`DxfOrdinateDimension`](xref:GeometryHelper.Dxf.DxfOrdinateDimension) by its kind, an ARC_DIMENSION a
[`DxfArcDimension`](xref:GeometryHelper.Dxf.DxfArcDimension) and a LARGE_RADIAL_DIMENSION a [`DxfRadialDimensionLarge`](xref:GeometryHelper.Dxf.DxfRadialDimensionLarge), each a [`DxfDimension`](xref:GeometryHelper.Dxf.DxfDimension) with AutoCAD's points
in world coordinates (`XLine1Point`, `DimLinePoint`, `Center`, `ChordPoint`, `ArcPoint` ...), `TextPosition`,
`DimensionText`, `DimensionStyle` and `Measurement`, worked out from the points as AutoCAD works it out: the rotated
dimension of (0, 0, 0) and (100, 20, 0) along 30 degrees measures 96.60, the angle of a level line and one at 45 degrees
pi / 4. A variable of the style reads as the dimension draws with it, its override (the list DSTYLE of its extended data of
ACAD) or its style's, and set is written as an override, the style kept: `dim.Dimasz = 4.5` writes `1070 41, 1040 4.5`.
An ordinate measures along the user's axes its 51 gives (the negative of their angle from the x of the normal's system),
which a move or a turn carries, so its measurement stays. `Dimtxtdirection` is DIMTXTDIRECTION, the override 1070 294 from
R2010.

### The measurement's text

`dim.FormatMeasurement(dim.Measurement, dim.DimensionText)` gives the text AutoCAD writes for a measurement, from the
variables the dimension reads. A length is multiplied by `Dimlfac` (a negative one taken as its size in paper space and as 1
elsewhere), rounded by `Dimrnd` and written in `Dimlunit`: scientific `1.55E+01`, decimal `15.50`, engineering `1'-3.50"`,
architectural `1'-3 1/2"`, fractional `15 1/2` (15.5 to two places, or to a sixteenth, fractions not stacked), or as the
Windows desktop writes it under the culture of the thread; `Dimzin` leaves zeros out (8 makes 12.50 12.5; the low two bits
keep or leave nought feet and nought inches), `Dimdsep` is the decimal separator, `Dimpost` (`<> mm`, or `mm` alone as a
suffix) goes about it, and where `Dimalt` is on its alternate units follow in brackets, `10"[254.00mm]`, or stand in the
place of a `[]` of the text, `<> ([])` giving `10" (254.00mm)`. A fraction stacked by `Dimfrac` is written in the codes of an
MTEXT, `{\H0.7x;\S1/2;}` across or `{\H0.7x;\S1#2;}` aslant, its height `Dimtfac`. Scientific units write the power of ten
of two figures at least, `1.000E-20`, `1.500E+300`. An angle of an angular dimension, in radians, is written in `Dimaunit`
to `Dimadec` places: `45°`, `45°30'15"`, `50g` or `0.7854r`. A number is rounded half away from nought as its 15 significant
figures, a .NET decimal, so 2.675 to two places is 2.68 under .NET Framework and .NET alike, where the double nearest 2.675
lies just below it and ezdxf's `format_text`, rounding that double, gives 2.67. Tolerances, limits, the arc symbol and the R
or diameter sign before a radius are not part of the text.

### The block that draws a dimension

What draws a dimension is the block AutoCAD drew for it, `DimBlock` (`*D7`), placed at `DimBlockPosition`: the library
writes a point, a text or a variable and leaves that block as it was, for AutoCAD to draw anew when it next draws the
dimension, so AutoCAD shows the old picture until then. A dimension made in code is given its `DimBlock`, a block of the
drawing, before it is appended. Its picture is made as AutoCAD makes an anonymous block, a block named `*D` added to the
drawing: `var picture = new DxfBlockTableRecord { Name = "*D" }; db.BlockTable.Add(picture);` names it `*D1`, the first
number no block, INSERT, dimension or table of the drawing names, and marks it anonymous (`IsAnonymous`, flag 1 of its
BLOCK), as ezdxf writes the picture of a dimension. A rotated dimension of 100 whose `*D1` holds its dimension line and an
MTEXT `100` is saved and read back drawn by `*D1` with its two entities; a drawing whose dimensions ezdxf drew as `*D1` to
`*D11` names the next `*D12`. A block, linetype or text style set on a dimension in no drawing, or on a copy, is held as
the record it is (a `Clone()` holds it too) and taken to the record of its name in the drawing it is added to, refused where
there is none of that name to take it to.

## See also

- [Tables of cells](tables.md).
- [Fonts and text](text.md): the words of leaders and dimensions.
- [Editing rules and what the model does not do](rules.md).
