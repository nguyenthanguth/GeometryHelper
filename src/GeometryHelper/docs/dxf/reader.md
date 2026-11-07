# Reading and writing shapes

## Reading DXF

`DxfReader` reads a DXF file into the library's shapes: what `DxfWriter` writes, and what AutoCAD and the other CAD
programs write of the same kinds, ASCII files of R12 (AC1009) to R2018 (AC1032). An INSERT is read as the entities of its
block, placed where it puts them, and the blocks themselves are there too (see Blocks and INSERTs below); a TEXT and an MTEXT
are read as a `DxfText` (see Texts), and the layer table into `LayerTable` (see The layer table).

```csharp
using GeometryHelper.Dxf;

DxfDocument drawing = DxfReader.Read("plan.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (GeoPolygonArc2 outline in drawing.OfType<GeoPolygonArc2>()) { ... }   // the closed polylines with arcs
foreach (DxfShape entity in drawing.OnLayer("steel")) { ... }                 // shape, layer, colour, linetype, handle, groups

DxfDocument text = DxfReader.Parse(dxfText);                                   // or Read(stream)
```

A `DxfDocument` holds the `Shapes` in the order of the file, the `Layers` (those of the layer table in file order,
then any an entity is on that the table does not list, two names differing only in case being one, as in AutoCAD), the
`LayerTable` with the colour and state of each layer of the table, the
`Units` the header gives (`$INSUNITS`, as the `DxfUnits` enum numbers them: `Millimetres` is 4), the `Version`
(`$ACADVER`, `AC1015` for R2000), what was `Skipped`, counted by type in the order of the type names, and the `Warnings`.
Each `DxfShape` has its `Shape`, its `Type` as the file names it, its `Layer`, `Colour` (an AutoCAD colour index from 1
to 255, null by layer or by block), `TrueColour` (0xRRGGBB, group 420), `Linetype` (group 6, null by layer),
`Lineweight` (group 370 in hundredths of a millimetre, one of AutoCAD's 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53,
60, 70, 80, 90, 100, 106, 120, 140, 158, 200 and 211, null by layer, by block, default or any other number, which a
warning counts, and refused by the constructor), `Thickness` (group 39,
nought where none), `Handle` (null in a file of R12) and its `Groups`: every group of its record in the order of the
file, extended data (1000 to 1071) among them, each value the text the file prints, so that nothing of the entity is
lost. Of a POLYLINE they run on through its VERTEX records and its SEQEND. `BlockPath` and `InsertHandles` name the
blocks and the INSERTs an entity was placed through, empty for an entity of the drawing itself. `KeepGroups = false` leaves the groups out, for a large file: 100 000 entities (34 MB) are held in 41 MB
without them and 157 MB with them. A `DxfShape` can also be made in code, of a shape and its look, to be written by
`DxfWriter.Add(entity)`: `new DxfShape(plate, "steel", colour: 1)`.

What each entity is read as, shapes of space unless `PlanAs2D` is set and the entity lies on the floor: one in world
coordinates (POINT, LINE, RAY, XLINE, SPLINE, 3D POLYLINE) where every height of it is nought, whatever its extrusion; one
in the coordinates of its extrusion's plane (CIRCLE, ARC, LWPOLYLINE, 2D POLYLINE, SOLID, TRACE) and an ELLIPSE where the
extrusion is (0, 0, 1) or (0, 0, -1) exactly, or not given, and every height nought:

| Entity | Shape | With `PlanAs2D`, on the floor |
|---|---|---|
| POINT, LINE, RAY, XLINE | `GeoPoint3`, `GeoLine3`, `GeoRay3`, `GeoXLine3` | `GeoPoint2`, `GeoLine2`, `GeoRay2`, `GeoXLine2` |
| LWPOLYLINE, POLYLINE of the plane | `GeoPolyline3` or `GeoPolygon3` (closed); `GeoPolylineArc3` or `GeoPolygonArc3` where an edge has a bulge | `GeoPolyline2`, `GeoPolygon2`, `GeoPolylineArc2`, `GeoPolygonArc2` |
| 3D POLYLINE | `GeoPolyline3` or `GeoPolygon3` | `GeoPolyline2` or `GeoPolygon2` |
| polyface mesh, polygon mesh | `GeoSolid3` of its faces, closed or not as they are; fewer than four faces, each a `GeoFace3` | the same |
| CIRCLE, ARC | `GeoCircle3`, `GeoArc3` | `GeoCircle2`, `GeoArc2` |
| ELLIPSE, whole | `GeoEllipse3` | `GeoEllipse2` |
| ELLIPSE, a part | `GeoSpline3`, exactly the part | `GeoSpline2` |
| SPLINE | `GeoSpline3` of its degree, knots, weights and control points; one of fit points alone, the spline `Interpolate` passes through them | `GeoSpline2` |
| 3DFACE | `GeoTriangle3` of three corners; `GeoFace3` of four in one plane within the planar tolerance; two `GeoTriangle3` cut along the shorter diagonal where they are not | the same |
| SOLID, TRACE | `GeoPolygon3` of its corners in DXF's order 1, 2, 4, 3 | `GeoPolygon2` |
| TEXT, MTEXT | `DxfText`: where it stands, its normal, height, rotation and words | the same |
| LEADER | `GeoPolyline3` through its vertices; splined, the `GeoSpline3` `DxfLeader.Explode()` fits through them; its arrowhead and hook line not read | `GeoPolyline2`, `GeoSpline2` |
| MULTILEADER | a `GeoPolyline3` for each leader line, from where its arrow points to the end of its dogleg; splined, a `GeoSpline3` fitted through the same points; its words or block and arrowheads not read | `GeoPolyline2`, `GeoSpline2` |

A LEADER is read as its curve, as AutoCAD's `IntersectWith` meets a leader: its vertices are in world coordinates, a
vertex within the point tolerance of the one kept before it is left out, and a leader of fewer than two vertices apart has
no shape. Splined, it is the cubic through its vertices that leaves along its first segment and reaches along its last,
its parameter from 0 to 1, as ezdxf's `fit_points_to_cad_cv` fits it: through (0, 0, 0), (10, 10, 0) and (20, 10, 0) it
rises to y = 10.43. In a block, the spline is fitted through the vertices where the block has them and then placed, as
AutoCAD draws the leader in its block: an INSERT scaled 2 along x places the control points scaled so, not a spline
fitted through the scaled vertices. `Explode()` of such an INSERT gives the leader with its vertices placed, which is fitted
anew where it is drawn, so under a scale unlike along its axes the two splines part: by 2.21 for the leader through
(0, 0, 0), (10, 10, 0) and (20, 10, 0) under an INSERT scaled 3 along y. A MULTILEADER is read so, each leader line as the chain or the spline `DxfMLeader` sets
out (see [Leaders and multileaders](annotation.md#leaders-and-multileaders)).

The coordinates of a CIRCLE, ARC, LWPOLYLINE, 2D POLYLINE, SOLID and TRACE are in the system of their extrusion's plane
and are taken to the world by the arbitrary axis algorithm, as `DxfWriter` writes them: the CIRCLE at (10, 20, 5) on
the extrusion (0, 0, -1) is centred at (-10, 20, -5), and an ARC from 0 to 90 degrees on that extrusion runs clockwise
seen from above, as AutoCAD draws a mirrored arc. On the floor that extrusion turns x over, exactly: with `PlanAs2D` the
ARC of radius 10 about the origin from 0 to 90 degrees is the clockwise `GeoArc2` from (-10, 0) to (0, 10), from pi - a
to pi - b, starting where the arc read in space starts; a polyline's bulges are negated and its vertices kept in their
order; a part of an ELLIPSE and a SPLINE keep the direction of the file; and the points are those ezdxf works out. A 2D POLYLINE stands at its
own elevation, its group 30, whatever heights its vertices give, or at the first vertex's where it gives none: ezdxf's
polyline at the elevation 50 is at z = 50, its vertices written at 0. A part of an ELLIPSE is a spline of rational
quadratic pieces of at most a quarter turn of its eccentric angle, each the stretched image of the arc
`GeoSpline2.FromArc` makes, so every point of it is on the ellipse to rounding: a quarter of the ellipse 60 by 30 is 3
control points, three quarters 7. A SPLINE whose knots are not clamped, as AutoCAD writes a closed one, is clamped by
knot insertion, the same curve over the span its knots give; one of fit points alone is read as the cubic AutoCAD 2021
fits through them whatever degree it asks (two points the straight cubic of 4 control points), and a warning says so. A closed polyline of two vertices with an arc, a circle drawn
as two half circles, is a loop with its first half circle cut at its middle, so that it has the three vertices a loop
has: two half circles of radius 10 are a `GeoPolygonArc2` of area 314.159. A closed polyline that encloses no area, or
in space does not lie in one plane, is a chain that ends on its first vertex, in the plane as in space, and a warning
says so. Of a POLYLINE fitted with a spline the curve drawn is read and the frame left out. A point or vector group
missing a part reads it as nought, as the DXF reference has it: the extrusion 210 = 1, 230 = 1 is (0.7071, 0, 0.7071).

Every number is read as the file prints it, the double nearest it, on .NET Framework as on .NET 10, whose parsers do not
always agree: so a file `DxfWriter` wrote gives back every coordinate, radius, bulge, knot and weight it holds to the bit.
What the file holds in another form comes back to the last place or two: the angles of an ARC are degrees, an ELLIPSE
keeps the end of its major axis and the ratio of its radii, and a circle, arc or polyline in a tilted plane its
coordinates in that plane's system, so an arc of radius 40 on the normal (1, 2, 3) comes back within 1E-11 at its ends.
A chain or loop with arcs that lies flat at one height can carry a normal a hair off up, (1.6E-19, 0, 1) as the library
fits its plane; the writer writes that extrusion, and such a chain comes back to the rounding of that plane, within
1E-12 of its largest coordinate.

DIMENSION, HATCH, IMAGE, MESH, MLINE, REGION, 3DSOLID, BODY, ATTDEF, ATTRIB, VIEWPORT and any other type are
not read and are counted in `Skipped`. So is an entity with no shape to it, the reason said once for each type in a Debug
line of `GeometryHelperLog`: a LINE no longer than the point tolerance, a polyline or LEADER of fewer than two distinct vertices, a MULTILEADER with no leader line of two, a
CIRCLE or ARC of no radius, an ELLIPSE of no axis, a TEXT or MTEXT of no height, or with none given, a number that is not
a finite number, a SPLINE the library does not take (a periodic one among them whose knots cannot be clamped, which a
warning names too), a 3DFACE of fewer than three distinct corners. `DxfReadOptions.Tolerance` (the default tolerance unless set; the global
one is never read) says what has no shape: a LINE 0.0004 long is left out within the default tolerance and read within
1E-5. An entity whose reading throws, such as a SPLINE of fit points 1E200 apart, whose system cannot be solved, is left
out and counted too, the others read, with a warning and one Warn line of `GeometryHelperLog` for each type. A group of
the look that cannot be read (a colour of 1.0, a true colour past a 32-bit number) is left out, and the entity read
without it. A number the scaling of `ToMillimeters` takes past the largest double (1E307 metres) leaves its entity out,
counted as one with a number that is not finite, and such a thickness is left out alone, and so does a point the
system of a tilted extrusion takes there: the CIRCLE at y = -1.7E308 and a height of 1.7E308 on the extrusion
(0.6, 0, 0.8) would be at an x of 2.38E308. The entities of paper space are not read, and a warning counts them, a POLYLINE with its
vertices one. One Info line for each call says how many entities were read and how many skipped, by type.

A file that is not a DXF, or is cut short, is refused with a `FormatException` that names the line: a group code that
is not a whole number, a code with no value after it, no EOF, a section with no ENDSEC, no ENTITIES section. A binary
DXF is refused the same way. The text of a file of R2007 or later is UTF-8; that of an older one is UTF-8 where its bytes
are (a warning says so), else in the code page its header names (`$DWGCODEPAGE`, ANSI_1252 where it names none), read
the same on either runtime: Windows-1252 by a table of its own, since .NET 10 does not have it, so the layer
Wand–€Außen reads so on both. Any other code page (1250, 1251, 1253 to 1258, 932, 936, 949, 950) is read as the runtime
has it: .NET Framework has them all, but .NET (Core) has none without CodePagesEncodingProvider, which the library does not
bring, so there its text is read as Latin-1 and a warning names the code page and says to save the drawing as a DXF of
R2007 or later, whose text is UTF-8; a program that calls
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` once, from the package System.Text.Encoding.CodePages, has
them all, as the viewer does. A file of R2007 or later whose bytes are not all UTF-8 is read with the replacement
character U+FFFD for each that is not, and a warning says so. A file is read line by line, as AutoCAD reads it: in a
code page of letters of more than one byte (932, 936, 949, 950, 54936 and the like) a byte that opens a letter at the
end of a line does not take the line feed after it; and a file with letters beyond ASCII whose header names a code page
that does not read each byte of ASCII alone as itself, UTF-16 (1200, 1201), EBCDIC (37), ISO-2022 (50220 to 50229), HZ
(52936) or UTF-7, which read an escape, a shift or a sign as a change of state and leave it out, or one of the national
7-bit pages (20105 to 20108, 20261, 20269) or x-mac-thai (10021), which read a few signs of ASCII as other letters, is
refused with a `FormatException` that names the code page and no line; AutoCAD writes none of them. A name or a text
written with escapes (`\U+00E9`, or `\u+00e9`) is read as its letters, in a file of any version, as AutoCAD 2021 reads
them; `\U+0000` and an escape of fewer than four digits are kept as written. A multibyte code, `\M+182A0`, the number of a
code page and two bytes, is read as the letter of those bytes where they are one letter of two bytes, or a letter alone
and nought, as AutoCAD reads it. A number is read from its first 19 digits and checked in integers,
the digits past 800 counting only as whether any is not nought: a number of 300 000 digits takes 2.2 ms on .NET 10.

The numbers are kept as the file has them unless `ToMillimeters` is set, which scales points, radii, elevations,
thicknesses and the lengths of axes by the unit of the header: a drawing in metres has its numbers times 1 000, one in
inches times 25.4. Angles, bulges, ratios, weights, knots and directions have no unit and are kept. A file with no unit,
or one the reference does not list, is not scaled, and a warning says so.

Reading 100 000 entities of eight kinds (34 MB) takes 0.54 s on .NET 10 and 0.90 s on .NET Framework 4.8 with the
groups, the document holding 157 MB, and 0.35 s and 0.58 s without them, holding 41 MB.

`DxfWriter.Add(entity)` writes an entity read back: its shape by the overload of `Add` for it, on its layer, with its
colour, true colour, linetype and lineweight after the layer, in the order AutoCAD writes them, and its thickness on the
LINE, POINT, CIRCLE, ARC and LWPOLYLINE that have one, each only where the entity has it. A drawing read, written and
read again gives the same shapes and the same look: every number to the bit but the angles of an arc and the axis of an
ellipse, which go through degrees and the ratio once more. A linetype the writer's table does not have is added to it
by name, with no pattern, so that AutoCAD opens the file and draws the line solid until the drawing is given the
linetype's pattern. A face of three or four corners with no hole, as a 3DFACE reads, is one 3DFACE again, the edges the
file hid hidden again; any other face and a body, a polyface mesh among them, is its triangles at the global tolerance.
What is not kept: an ARC runs counter-clockwise about its extrusion and no other way, so a clockwise `GeoArc2`, written
from its end to its start, comes back counter-clockwise over the same points, with `PlanAs2D` as without, and a circle or
an arc in space on an extrusion that points down is written about its normal turned up, so a mirrored arc comes back
running the other way over the same points too; a
linetype is written as the table spells it, so BYBLOCK comes back ByBlock; the entity's `Groups` are not written, and it
is given a handle of its own. A shape with no overload of `Add`, a `GeoSphere3` among them, which needs a chord
tolerance, is refused with an `ArgumentException` naming its type. The overloads that were there write the same bytes as
before.

### Texts

A TEXT and an MTEXT are each a `DxfText`, with `PlanAs2D` as without, a text having no twin of the plane:

```csharp
foreach (DxfText label in drawing.OfType<DxfText>()) { ... }   // Position, Normal, Height, Rotation, Value, IsMultiline, WidthFactor, Width
```

A TEXT stands in the system of its extrusion's plane, taken to the world as a CIRCLE is: at its first alignment point
(group 10) where it is aligned left on its baseline (72 and 73 nought, or not given) or aligned or fitted between its two
points (72 3 or 5 with 73 nought), which run from the first to the second, as ezdxf places them, so the aligned TEXT from
(0, 0) to (100, 0) stands at (0, 0, 0); and at its second (11) where it is aligned any other way, centred, right, in the
middle or by its top or bottom, as AutoCAD places it from that point. Its `Rotation` is its group 50 in degrees made
radians, counter-clockwise about its `Normal` from the x axis of the normal's own system, and its `WidthFactor` its group
41, 1 where it gives none. An MTEXT stands at its insertion point (10) in world
coordinates, whichever corner its attachment names; its rotation is that of its direction (11) in the system of its
extrusion, or its group 50 in degrees where it has no direction (degrees, as AutoCAD writes it, though the DXF reference
says radians), its `Width` the width its paragraphs wrap at (41, nought for none), and its `WidthFactor` 1. Every
`Rotation` is brought into a turn, from nought up to but not including 2 pi, on every path, a text made in code too: a TEXT
turned -90 degrees is turned 3 pi / 2. A TEXT at (3, 4, 5) on the extrusion (0, 0, -1) stands at (-3, 4, -5), its normal
(0, 0, -1): it reads mirrored seen from above.

`Value` holds the words. A TEXT's are as written; an MTEXT's with its formatting taken out: `\P` is a new paragraph, a
line feed in `Value`, `\N`, a new column, a space, `\~` the space that does not break (U+00A0), the codes that end in a semicolon (font, height, colour,
width, tracking, slant, stacking, paragraph) go with their arguments, `\L`, `\O` and `\K` go alone, `\\`, `\{` and `\}`
are the characters, a stack `\S1/2;` is 1/2, and the braces that group are taken off, so `{\fArial|b1;Bold}\Pline` is
`Bold` and `line` on two lines; a code with no semicolon after it is kept as written, as ezdxf keeps it; `\U+0000` is
left out, and `\M+1A4A1`, kept as written in a TEXT, is the letter of its first byte, U+FF64, as AutoCAD's `Text` reads
it. The pieces of 250 characters an MTEXT is written in (group 3) run on into its last (1). In both, the special
characters are read from the left as AutoCAD reads them: %%c, %%d and %%p, in either case, are the diameter (∅, U+2205),
degree and plus-minus signs, so `%%c20 at 45%%d` is `∅20 at 45°`; in a TEXT %%% is a per cent sign, so `50%%%d` is `50%d`,
and %%nnn, three digits, the character of that code, `%%065` an A, where an MTEXT keeps both as written, as AutoCAD 2021's
`Text` gives them and draws them (an MTEXT `x%%%y` 10 high is 46.7 wide, a TEXT of it 26.7, as wide as `x%y`); %%u, %%o
and any other are kept as written. A letter written as an
escape, `\U+00E9`, is the letter, as in a name, and so is the text `\U+0041` written in the words, which reads as an A: the
file cannot tell the two apart. An MTEXT's escapes are read before its formatting, as AutoCAD 2021 reads them: `\\U+0110`
is a backslash and Đ, and `\U+005CP` a new line. A control character in AutoCAD's caret form is the character, `^J` a line feed, `^M` a
carriage return and `^I` a tab, `^` followed by a space a caret, a caret and any other character the one 64 below it, and
`^@` ends the words, as AutoCAD 2021 reads them. A TEXT is one line, so a line feed in its words,
however written, is a space, and a carriage return is left out of either kind: `a^Jb` is `a b` in a TEXT and `a` and `b`
on two lines in an MTEXT.

Placed by an INSERT, a text is carried by the x and y axes of its own frame: it stands where its point lands, faces the
normal those carried axes span, made unit, and is turned to the carried x axis in the system of that normal; its height is
stretched as the y axis is, and a TEXT's width factor as the x axis is against the y axis, an MTEXT's width as the x axis.
A text in a block scaled by 3 along x and 2 along y is twice as tall and, a TEXT, one and a half times as wide for its
height. A mirror turns the normal over, as AutoCAD writes the text of a mirrored block: a TEXT reading along x from (1, 0)
in a block placed with a scale of -1 along x stands at (-1, 0, 0), its normal (0, 0, -1) and its rotation nought, so that
it reads along -x, mirrored, as the block shows it. Where a placement shears a text, which neither entity can hold, the
reader keeps the length of the carried y axis as the height; ezdxf leans the text by an oblique angle and takes the height
square to the baseline, smaller by the cosine of that angle. Held against ezdxf 1.4.5 on 81 texts placed every way an
INSERT places them, every position is within 1E-9 of ezdxf's, every normal within 1E-12, and every rotation within 1E-9.
A text counts one entity towards `MaxPlacedEntities`, and one flattened by its INSERT onto a line is counted in `Skipped`.

`DxfWriter.Add(text, layer)` writes a `DxfText`, read or made with its constructor, as a TEXT, aligned left on its
baseline and turned by its group 50 in degrees, or an MTEXT, attached at its top left and turned by the direction it
reads along (11) in world coordinates, in the text style Standard, its letters beyond ASCII as escapes, a control character
in caret form (a tab `^I`) and a caret as `^` and a space, and an MTEXT's backslashes, braces and paragraphs as the codes
that keep them, no piece of it ending on a caret; `Add(entity)` writes one with its look. Read back, a text is the same:
its words, height, width factor, width and normal to the bit, its position too where the normal is (0, 0, 1) or
(0, 0, -1) or the text an MTEXT, a TEXT on a tilted normal to the rounding of its plane's system (within 1E-13 at 30 mm),
and its rotation to the last place, through degrees or a direction. What is not kept: the alignment and attachment of a
text read from a file, which is written where it stands, and a %%c, %%d or %%p, a %%% or %%nnn of a TEXT, or the text `\U+0041` in the
words, which comes back as the character it stands for. ezdxf recovers and audits such a file with no error.

### The layer table

`LayerTable` holds the layers of the layer table in its order, each a `DxfLayer`: its `Name`, its `Colour` (the AutoCAD
colour index, 1 to 255, whether the layer is on or off; null where the table gives none or one outside that range), its
`TrueColour` (group 420), `Linetype` (6) and `Lineweight` (370, null for the default -3), and whether it `IsOff` (the
table writes the colour of a layer turned off below nought), `IsFrozen` (flag 1 of group 70), `IsLocked` (flag 4) and
`IsPlotted` (group 290, true where the table does not say). A record whose name an earlier one has in another case is left
out, the first kept, as `Layers` keeps its spelling; a layer only an entity names is in `Layers` and not in the table.
`GetLayer(name)` finds a layer of the table by its name in either case, and gives null where the table has none:

```csharp
DxfLayer steel = drawing.GetLayer("STEEL");   // the layer Steel of the table: Colour 1, IsOff false, Linetype DASHED
```

### Blocks and INSERTs

The blocks of the file are in `Blocks`, in the order of the file, and each INSERT of the drawing is read as the entities
of its block, placed where the INSERT puts them, in the order of the block and where the INSERT stands among the
entities; a file that defines its blocks after its entities reads the same. `ExpandBlocks = false` leaves the INSERTs
out, counted in `Skipped`, and reads the blocks all the same.

```csharp
DxfDocument drawing = DxfReader.Read("frame.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (DxfShape entity in drawing.Shapes) { ... }   // the bolts of the block BOLT placed, each with BlockPath "BOLT"
foreach (DxfBlock block in drawing.Blocks) { ... }        // Name, BasePoint, Flags and its own Entities, unplaced
```

An INSERT places its block as AutoCAD does: the block's base point taken to the origin, the scales along x, y and z
(groups 41, 42 and 43, each 1 where not given), the turn about z (50, degrees, exact for a whole number of quarter turns),
the shift to the insertion point (10, 20, 30, in the system of the INSERT's extrusion), then that system (210). A
MINSERT's array of 70 columns by 71 rows is placed row after row, each cell shifted by its column times the column
spacing (44) and its row times the row spacing (45) along the turned axes, not scaled, as ezdxf places it; cells a
spacing of nought shifts alike are placed once, as ezdxf places them, and an array of more than 1 048 576 cells is not
placed. A MINSERT is an INSERT in the file, its type INSERT and its subclass AcDbMInsertBlock, so one left out is counted
in `Skipped` under INSERT, as the file names it; the warning for an array too large to place calls it a MINSERT. An
INSERT inside a block is placed within it and the two placements composed, to a depth of 64. An INSERT that cannot be
placed is counted in `Skipped` and said in a warning, the rest read: one of a block the file does not define
(the warning names it), of a block inside itself, at once or through others (the warning names the first chain,
`A > B > A`), one nested deeper than 64, one of an external reference (a block of flag 4, whose drawing is another file),
one of the block of a layout, and one that would take the entities placed from blocks past `MaxPlacedEntities`. The blocks of the layouts, `*Model_Space` and `*Paper_Space`, are not among the
`Blocks`, and a warning counts their entities: the drawing is read from its ENTITIES section.

Blocks placed inside blocks multiply: a block of 100 INSERTs of a block of 100 INSERTs of a block of 100 INSERTs of a line
is a file of 18 KB that places 100 000 000 lines. `DxfReadOptions.MaxPlacedEntities` (2 000 000 by default, above nought)
bounds the entities placed from blocks in one reading, the drawing's first, in the order of the file, then those of the
`Blocks` read on their own. An INSERT is placed whole or not at all: how many entities its block places at the most is
worked out once for each block, through its INSERTs, before any is placed, and an INSERT that would take the count past
the bound places none, is counted in `Skipped`, and one warning names the bound and how many entities the INSERTs left
out would have placed. A block inside itself counts nought where it is met again, as it is not placed again there, and a
size worked out through such a block is worked out again for each INSERT met inside another block, but kept for every
INSERT of the block the working out started from. Whatever the
working out says, the count of what is placed holds the bound too: an INSERT whose entities would pass it is taken back
whole, its entities and what its placing counted, and counted with the others left out. That file is read in 1.4 s on .NET 8, holding 340 MB, where it ran out of memory; the one of three
levels places its 1 000 000 lines in 2.5 s, holding 685 MB with what its blocks read on their own place. Working out
stops at 1 000 000 blocks in all, for the whole reading, the drawing's INSERTs and those of the blocks read on their own
together: once they are spent, an INSERT whose size would need one more block worked out is refused, counted in `Skipped`
and said in a warning of its own, and so are the other INSERTs of the same block, without working it out again; a size
worked out before still places its INSERTs, that of a block that met no block inside itself and that worked out for the
INSERTs of a block from which a walk began, whether it met one or not. A file that works out fewer
reads as it did. 1 000 INSERTs of a block in a ring of 26 blocks, each inserting the next twice, a file of 43 KB, are read
in 1.4 s on .NET 10, where they took 35 s, and a ring of 1 000 blocks each inserted by the drawing, each a root of its own
working out, a file of 174 KB, in 0.19 s, where it took 47 s when the bound was one for each INSERT.

What a placement makes of each shape:

| Shape in the block | Under a turn, an even scaling or a mirror | Under an uneven scaling |
|---|---|---|
| POINT, LINE, RAY, XLINE, chain or polygon without arcs, 3D POLYLINE, 3DFACE, SOLID, TRACE, mesh | carried point by point | the same |
| SPLINE | its control points carried, its knots and weights kept, which is exactly the spline's image | the same |
| SPLINE of fit points alone | the spline through its points in the block, then carried | the same |
| ELLIPSE | the ellipse of the images of its semi-axes; a part, the spline that is exactly that part | the same |
| CIRCLE | a circle, its radius times the scale | the ellipse it becomes |
| ARC | an arc, running the way its image runs | the spline that is exactly that part of the ellipse |
| LWPOLYLINE or 2D POLYLINE with arcs | the chain or loop with its bulges, its arcs about the placed normal | a segment for each straight edge and a spline for each arc edge, each an entity with the look and handle of the polyline, in its order |
| TEXT, MTEXT | the text carried by its axes, a mirror turning its normal over | the same, its height and width factor stretched by its axes |

So every shape placed is the image of the block's shape to the rounding of its numbers: the circle of radius 3 scaled by
3 along x is the ellipse 9 by 3, and a polyline with two arcs placed so is four entities, a spline, a segment, a spline
and a segment. A mirror turns an arc over: the ARC of radius 4 about (5, 5) from 30 to 200 degrees, in a block whose
base point is (2, 1), placed at (-100, 0) with a scale of -1 along x, is the clockwise `GeoArc2` about (-103, 4) from
(-106.464, 6). A scaling so nearly even that a circle placed whole lands within the point tolerance of
`DxfReadOptions.Tolerance` of where the scaling puts each of its points keeps the circle, the arc and the chain with arcs
of the plane it scales so, by the rule of `GeoTransform3.Transform(GeoCircle3)`: the circle of radius 10 scaled by
1.0000001 along y is the circle of radius 10, and by 1.001 the ellipse 10.01 by 10. A placed entity lies on the floor,
for `PlanAs2D`, where its placed plane's normal is straight up or down and every height of it nought, so a block drawn
in the plan and placed turned about z, scaled or mirrored is read as shapes of the plane, and one placed on a tilted
extrusion as shapes of space. Held against ezdxf 1.4.5 on a drawing of 168 entities placed every way an INSERT places
them, every point read is within 1E-9 of where ezdxf puts it; ezdxf's own virtual INSERT of a block inside a block is
wrong under an uneven outer scaling with an inner turn, so there the two matrices ezdxf gives are composed.

An entity of a block on layer 0 is on the layer of the INSERT that placed it, and one whose colour, linetype or
lineweight is BYBLOCK has the INSERT's, its true colour too where it has none of its own, through every level of
nesting; BYLAYER stays BYLAYER. In the drawing itself there is no block to take a look from: a colour BYBLOCK is no colour
and the linetype is named as written. An entity on a layer of its own whose colour or linetype is BYBLOCK, placed by an
INSERT that is itself BYLAYER, is drawn by AutoCAD in the colour and linetype of the INSERT's layer; the reader gives it no
colour and no linetype, on its own layer, which reads as that layer's: the entity has no field for a look taken from
another layer. The thickness is stretched as the placement stretches the extrusion, and turned over where the placed
extrusion points against the entity's own: 10 is 30 under a scale of 3 along z and -30 under one of -3, and a mirror in x
leaves it 10. Each entity keeps its own `Handle`, that of the block's entity; `BlockPath` names the
blocks it was placed through, outermost first (`FRAME`, `BOLT`), and `InsertHandles` the INSERTs, one to each name,
null for an INSERT of R12, which has no handle, the cells of a MINSERT sharing its handle. An ATTRIB of an INSERT and
an ATTDEF of a block are text, counted in `Skipped`.

Each of the `Blocks` holds its own entities in its own coordinates, read with the same options, an INSERT inside it
placed within it, layer 0 and BYBLOCK kept as they are. `Skipped` and the `Warnings` are of the drawing as placed: an
entity of a block that is not read is counted each time a block places it, so a HATCH in a block placed 100 times counts
100, and what the blocks read on their own leave out is not counted. Reading 10 000 INSERTs of a block of 50 entities
(12 lines, 10 circles, 10 arcs, 8 closed polylines with arcs, 5 splines and 5 points; 1.9 MB of text) gives 500 000
entities in 0.50 s on .NET 10 and 0.55 s on .NET 8, the document holding 157 MB with the groups or without them, since
the places of one entity of a block share its groups; scaled unevenly, the 820 000 entities they give take 0.96 s and
1.00 s and 448 MB.

`DxfWriter.Add(entity)` writes a placed entity as it is, in the drawing itself, with its look: the file written has no
blocks, and reads back as the same shapes, every number to the bit but the angles of an arc and the axis of an ellipse.

## Writing DXF

`DxfWriter` writes a DXF file of version R2000 in millimetres, which AutoCAD, Tekla Structures and every CAD viewer
open. Each shape goes on a layer named as given (layer 0 when none is), made on first use in white with the continuous
line, since CAD colours by layer. Names are cut at 255 characters, the characters DXF refuses and the comma become `_`,
and letters beyond ASCII are written as escapes such as `\U+00E9`; two names that differ only in case, accented letters
included, are one layer, named as first seen. In the plane a shape is written as the entity CAD has for it, not as
chords: a polygon, rectangle, triangle or ring of a face as a closed lightweight polyline, a chain or loop with arcs as
one with the bulge of each arc edge (the same number `GeoEdge2.Bulge` gives, 1 for a semicircle), a circle, an arc, an
ellipse, a ray, a line without ends and a spline as a CIRCLE, ARC, ELLIPSE, RAY, XLINE and SPLINE. DXF arcs run counter-clockwise only, so a clockwise `GeoArc2` is
written from its end to its start; a face is its rings, not a filled area, which `SvgWriter` draws. In space a point,
segment, ray, line without ends, ellipse and spline are written as they are, a chain and a polygon as a 3D polyline, and anything with a surface as
3DFACEs: one per triangle of a face or a body (a box 100 by 100 by 20 with a hole 20 by 20 through it is 64), one per
face of a mesh that has three or four corners and turns one way, and six for a `GeoAabb3` or `GeoObb3`, written from its
corners, so a box 100 by 100 by 0.002, or by nothing, is written too, and an empty `GeoAabb3` is refused with an
`InvalidOperationException`, as `ToObb` refuses it. Where a face, a side of a body or a face of a mesh is cut into
triangles, the edges between its own triangles are marked invisible, so a wireframe shows its outline; triangles given
as they are keep every edge. A curve in space (`GeoArc3`, `GeoCircle3`, and chains and loops with arcs) is written as
itself when no chord tolerance is given. An arc is an ARC and a circle a CIRCLE, each in the coordinate system of its
own plane: the normal, turned if need be to point up in its largest component, is the extrusion, the centre is in the
plane's coordinates by the arbitrary axis algorithm AutoCAD itself applies (x is the world y crossed with the normal
where its x and y are both less than 1/64 from nought, the world z crossed with it otherwise), and the angles are degrees
counter-clockwise about the extrusion from that x axis. A circle of radius 25 about (10, 20, 30) standing on the normal
(1, 1, 0) is a CIRCLE with extrusion (0.7071, 0.7071, 0) and centre (7.0711, 30, 21.2132); an arc that turns clockwise
about the extrusion is written from its end to its start, so an arc on the floor with the normal (0, 0, -1) is written
about up, and is written exactly as the `GeoArc2` or `GeoCircle2` of the same arc is, with no extrusion groups. A chain or
loop that lies in one plane is one lightweight polyline in that plane: the extrusion is the plane's normal turned up in
the same way, the elevation is the height along it, the vertices are the plane's x and y and each arc edge keeps its
bulge times the cosine between its normal and the extrusion (negated where they point apart, nought for an arc that
bows square to the plane, which the planar tolerance lets stand off it). A slot 80 long and 20 wide (straight sides of
60) with a half circle at each end, in a plane tilted 30 degrees about the x axis, is four vertices and two bulges of 1,
and `Add(chain, tolerance)` states the tolerance that judges whether a chain lies in one plane (the global one without
it). A chain that bends out of one plane, such as a reinforcing bar, is a LINE for each straight edge and an ARC for
each arc edge, each in its own coordinate system, in the order of the chain; an arc edge too shallow to place a circle
for is read by its chord, and a chain with no arc whose vertices lie on one line, which lies in no one plane, is a LINE
for each edge. An arc or circle with a radius that is not a finite number above nought, or a normal of no length, is
refused with an `ArgumentOutOfRangeException` or an `ArgumentException`. Given a chord tolerance instead (`Add(arc, 0.1)`), any of them is cut into chords no further from it
than that, with no default: a circle of radius 100 at 0.1 is 71 chords, one of radius 50 000 at 0.001 is 15 708, and one
of radius 25 at 0.1 is 36 where the CIRCLE is one entity. A curve that would need more than 1 048 576 chords, a chain or loop
counted whole, is refused with an `ArgumentOutOfRangeException`. A shape holding a number that is not finite is refused
with an `ArgumentException` and leaves nothing in the file. The file is plain ASCII, with the extents of what was drawn
in its header and the view opened on them.

A drawing `DxfWriter` writes is also where a drawing made from nothing begins for the editing model:
`DxfDatabase.Parse(new DxfWriter().Add(outline, "plate").ToString())` opens it to be edited as any other (see [The
model](index.md#the-model)).
