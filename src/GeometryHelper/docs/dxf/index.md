# DXF files

The library has three ways into a DXF file, each for its own job:

| To | Use | What it gives |
|---|---|---|
| draw the library's shapes for CAD to open | [`DxfWriter`](xref:GeometryHelper.Dxf.DxfWriter) | a new file of R2000 in millimetres, each shape the entity CAD has for it, on the layers named (see [Writing DXF](reader.md#writing-dxf)) |
| read the geometry of a drawing | [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader), which gives a [`DxfDocument`](xref:GeometryHelper.Dxf.DxfDocument) | the shapes of the plane or of space with their layer and look, every INSERT placed, read only (see [Reading DXF](reader.md#reading-dxf)) |
| change a drawing and save it with nothing else lost | [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) | the drawing as AutoCAD's `Database` holds it, its classes AutoCAD's with Dxf in front (see [Editing a DXF file](#editing-a-dxf-file)) |

`DxfReader` reads a whole drawing into shapes at once, 100 000 entities of eight kinds (34 MB) in 0.54 s on .NET 10, and is the
way to take geometry out of a drawing to work on. `DxfDatabase` reads what it is asked for when it is asked: 100 000 lines of
10.5 MB are opened and saved in 36 ms and walked as [`DxfLine`](xref:GeometryHelper.Dxf.DxfLine) objects in 21 ms, and what is not changed is written back as
it was read. `DxfWriter` makes a file from nothing. The three meet: an entity of the editing model gives the shapes
`DxfReader` would read for it by `GetGeometry()`, and a drawing begins as the text `DxfWriter` writes (see
[The model](#the-model)).

A DWG file of AutoCAD 2000, 2004, 2010, 2013 or 2018 is read into the same two: [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader)`.Read` gives the
`DxfDocument` and `DwgReader.Open` the `DxfDatabase` that the DXF of the drawing would give (see
[Reading and writing DWG files](dwg.md)); [`DwgWriter`](xref:GeometryHelper.Dwg.DwgWriter) writes either as a DWG of AutoCAD 2018.

## Editing a DXF file

### The model

`DxfDatabase` opens a DXF file to edit it as AutoCAD's `Database` edits a drawing, and saves it again with nothing lost. Its
classes are AutoCAD's with Dxf in front, so that a plugin can use both without a clash of names: [`DxfBlockTableRecord`](xref:GeometryHelper.Dxf.DxfBlockTableRecord),
[`DxfLayerTableRecord`](xref:GeometryHelper.Dxf.DxfLayerTableRecord), `DxfLine`, [`DxfMText`](xref:GeometryHelper.Dxf.DxfMText), each with AutoCAD's members and their meanings, points in world coordinates
and angles in radians as AutoCAD's .NET API gives them. Under them is an engine that keeps every byte of the file and
reads what the file holds from those bytes when it is asked for: opened and saved with nothing changed, a file is written
back byte for byte; a change writes the records it is about anew in the file's own style, and every other record keeps its
bytes. One record is read otherwise: a face of a polyface mesh with the marker AcDbVertex before its AcDbFaceRecord, as
GeometryHelper 13.0.0 and 13.1.0 wrote a face made in code, which AutoCAD 2021 refuses to open ("Class separator for class
AcDbFaceRecord expected"), is read with its own marker alone, as AutoCAD writes it, and saved so; nothing else of the file
changes.

```csharp
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

DxfDatabase db = DxfDatabase.Open("plan.dxf");                       // or Open(stream), Parse(text)

foreach (DxfEntity entity in db.ModelSpace) { ... }                  // DxfLine, DxfMText, ..., DxfUnknownEntity
DxfLine line = db.ModelSpace.OfType<DxfLine>().First();
line.EndPoint = new GeoPoint3(100, 0, 0);                            // that record written anew
line.ColorIndex = 1;
db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)) { Layer = "steel" });
db.LayerTable.Add(new DxfLayerTableRecord { Name = "rebar", ColorIndex = 3 });
db.Insunits = DxfUnits.Millimeters;
line.Erase();
db.Save("copy.dxf");                                                 // every other record the same bytes
```

A drawing is saved as another version of AutoCAD with `db.Save("plan2010.dxf", "AC1024")`, or `Save(stream, version)`:
R12 (AC1009), R2000 (AC1015), R2004 (AC1018), R2007 (AC1021), R2010 (AC1024), R2013 (AC1027) and R2018 (AC1032), as AutoCAD 2021's `SaveAs`
writes one; the drawing in the memory stays of its own version, and its `Filename` is the path. A drawing saved in its own
version is the bytes `Save(path)` writes. A drawing of R12 to R14 is saved as it is only (R12 is also written of a drawing of R2000 or later), and a version that is
none of the seven is refused. Every kind of entity and object stays, as AutoCAD keeps them: a drawing of 1 300 lines, 130 hatches, 2 tables
and 15 multileaders saved as R2000 has them all, with the header variables, the groups of each record and the counts of the
classes (which AutoCAD opens no drawing of R2004 or later without) of that version; of its 785 records that carry a handle,
735 are group for group, value for value, what AutoCAD 2021 wrote of the same drawing as R2000, and the others are what
AutoCAD adds to remember what R2000 cannot hold. A true colour or a transparency, the gradient of a
hatch (left filled solid with its first colour before R2004), a viewport's lighting and the other groups a later version brought are left out of an older one; text beyond the code page of a
version before R2007 is written as `\U+XXXX`. An MTEXT of R2000 carries its line spacing again in extended data of ACAD (`MTEXTBEGIN`, 73, 44 and a 74 of nought, `MTEXTEND`), as AutoCAD writes it (the note of a
leader, 6 MTEXTs of a table in each of 3 versions); the data goes when the drawing goes to R2004 or later. The background of an MTEXT goes into that data too, as AutoCAD writes it (`ACAD_MTEXT_BBRT` to `ACAD_MTEXT_BERT`: the flags in 91, the scale in 46, the colour in 64 as one number, 0xC3 and its index or 0xC2 and its red, green and blue, empty names in 65 and 66, the transparency in 442; 18 MTEXTs of 6 kinds equal AutoCAD's in R2000, R2004 and R2007), and comes out of it as the groups 90, 63, 421, 45 and 441 going up. What cannot be given the form of the older version is left out or written
another way, and the call gives it back as sentences: a table whose cells hold a field, a link or a value that is not text becomes the INSERT of the block
it is drawn with; a solid whose ACIS records the library does not place (the splines of an R2000 file, a solid made by a
sweep, whose surface AutoCAD writes another way in each version) is left out of a file of R2013 or later, whose entity may hold no
text (AutoCAD reads such a file as no drawing), and out of one of R2010 or before where its data is the binary of R2013. A solid lofted through circles goes to R2000, R2004 and R2007 as the text of R2010, which AutoCAD reads there with its volume (7 889.8 for circles 10, 5 and 8 wide, 0 when the loft was written in the form of R2004 under that version's head); an MTEXT of R2000 going to R2007 or later has the height of its column (46) AutoCAD writes after its width.

The ACIS data of a 3DSOLID, a BODY or a surface is turned from one form into the other by its records, not by its geometry, as
AutoCAD 2021 writes it in each version: the SAB of the data store (R2013 and R2018, the section ACDSDATA) into the SAT of the
entity (R2004, R2007, R2010 and R2000, which holds ACIS 4.00), the SAT into the SAB, and the SAT of one version of the modeler
into another's (R2004 has no number on a vertex and R2007 and later have one, the binary form has a number on a coedge, R2007
names and holds the exact curve and surface of a spline another way than R2004, and R2010 puts 22600 after the name of three of
them). ACIS 4.00, the SAT of R2000, is the form of R2004 less what the older modeler has not: no `asmheader`, so that every
pointer stands one lower; no history on a record (the whole number of -1 and the pointer after the first value, the number alone
on an attribute and on a transform); no parameters and no convexity on an edge; a text as its length and the word (`13 text`, not
`@13 text`); no fourth enum on the name attribute of a loft; and no word `full` before the spline of an exact curve or surface.
Going down they are taken out (a body of ASM that has a tolerant edge, a swept spline surface or a history that is not -1 is
left as the SAT of R2010, which AutoCAD reads in an R2000 file); going up the parameters of each edge are worked out from its
vertices and its curve (a line's from its root along its direction, an ellipse's as the angle of the vertex from the major axis in
[-pi, pi), a full turn on for a closed edge, a spline's the first and last knot where its ends are the vertices, an edge with no
curve 1 and 0), the convexity is `unknown`, and a vertex gets the number 2 from R2007 (the end of its edge is not known), as
AutoCAD does when it reads R2000. A spline of an exact curve made from ACIS 4.00 is dated 20900 in R2010 and later, not 22600. From R2007 a 3DSOLID has its history (`AcDb3dSolid`, 350 of nought) and in R2013 a GUID
(group 2) that names its record in the data store, the one AutoCAD's extended data `ACAD_OBJECT_CHANGE_GUID` holds before. A box
30 by 20 by 10, a sphere, a cylinder, a cone, a torus, a wedge, a pyramid, an extrusion of a polyline with an arc tapered 0.1, a
revolution, a box with a hole and a loft, 11 solids, come out of each of R2004, R2007, R2010, R2013 and R2018 as the SAT or SAB
AutoCAD 2021 writes of them in each of the others, record for record (220 conversions); AutoCAD opens the 275 files they make in
the six versions with the volume each solid had and `AUDIT` finding 0 errors. R2000 is turned too: 35 bodies (those, a frustum,
an elliptical cylinder, a sliced sphere, a cross of two cylinders, a cone cut by a box, a union, a sweep of a circle along an arc,
a revolution through 270 degrees, a loft of rectangles, a box turned about two axes, a region with a hole and with an arc, a
planar surface and surfaces extruded, revolved, lofted and swept) go from R2018, R2013, R2010, R2007 and R2004 to R2000 and from
R2000 to each of them: of the 175 going down 170 are the SAT AutoCAD 2021 writes of the same solid in R2000, record for record
and digit for digit; of the 175 going up 130 are AutoCAD's record for record and 40 differ in the last digit of an edge's
parameter or of a plane's normal (below 1E-12: AutoCAD's own work leaves 3.6E-16 where an angle is 0); the five of a solid
swept along a spline, whose splines are another curve in ACIS 4.00 than in R2004, are left as they are. AutoCAD opens the 910
files, 350 of them made by the library, with the volume of each solid and `AUDIT` finding 0 errors; a BODY goes the same way (a
box and a cone, 20 files). A solid turned about two axes keeps its transform: the word `rotate` is read and written, and `reflect` and `shear` (AutoCAD bakes a mirror into the geometry of a solid and
refuses to scale one unevenly, so it never writes them, but it reads them: a box 30 by 20 by 10 with `shear` has the extents 30 by 35 by 10, and any other word of the flag is a modeling error).
The extrusion of a closed spline square to its plane (its surface `cylsur` in R2004, `cyl_spl_sur 22600` from R2007, with a flag F before its closing brace from R2010, and the seam where it meets
the closed edge of its profile numbered 2 on its vertex) goes between R2004, R2007 and R2010 as the SAT AutoCAD 2021 writes (6 conversions record for record), and the files it makes of R2013 and R2018 open in
AutoCAD with its volume, 2 279.094, and `AUDIT` finds 0 errors; the extrusion at a slant (`sumsur` with a B-spline surface of its own in R2004 and none in R2000), a sweep along a spline and a loft
of circles, whose edges are tolerant in R2004 and later, are left as they are.

A table whose cells are text or blocks, written in a form of its own before R2007, comes out in the form of the version: each cell's
groups 171 to 178 and 145, its text (1) and its overrides (alignment 170, style 7, height 140, colours 63, 64, 283, borders 275 to 279) in R2004 and before, and from R2007 its flags (91, 92) and its value (301 CELL_VALUE to
304 ACVALUE_END) too, the table of R2010 and later with its data version (280); the flags of a cell's overrides are the low 16 bits of its 91, written as a signed number going down
(the border in 275 and 65, 0x8880, is 297 088 in R2007 and -30 592 in R2004) and with the bit for content (0x40000, a text and no block cell has it, nor has a cell with a top border alone) and for a border at the bottom or on the left (276 and 278, 0x1000 and 0x2000 again 4 bits up) in
R2007; the top border is in 279 (0x4440). The cells of 4 AutoCAD tables (one with a title, text of its own height, alignment, colours and a background, one with a border of each side and each kind on 16 cells, one with
links) come out of each version in the others as AutoCAD writes them, group for group. A table of 4 rows by 3 columns with
merged cells goes between R2000, R2004, R2007, R2010, R2013 and R2018 with the cells AutoCAD 2021 writes in each, 30 conversions, and
AutoCAD opens them all with the text of each cell; the extension dictionary that keeps the round trip of the later form in R2004
and before (ACAD_XREC_ROUNDTRIP) is made going down, with the data table below, and taken out going up unless the table has a TABLECONTENT (a link to a spreadsheet, formats): that round trip stays both ways. Its TABLECONTENT, whose values AutoCAD writes without their flags (93) and their 94, 300, 302 and ACVALUE_END before R2007, and its TABLEGEOMETRY
are the ones AutoCAD writes (9 directions between R2000 and R2018 of a table of merged cells; a linked table, links kept at its cell (1, 0) when AutoCAD upgrades the file, `AUDIT` 0). The data table AutoCAD adds to every table going down to R2000 or R2004 (ACAD_ROUNDTRIP_PRE2007_TABLE, in the XRECORD of the round trip, made for a table that has none) is made too, as AutoCAD writes it: a row for each cell with a checksum of its text (the sum of its characters, each times its place from 1: 1 579 for Title), a mask of what the cell overrides (its alignment, background colour and fill, the sides that have a border of their own and, on the cell across such an edge, the side that faces it) and an XRECORD that holds its value (the text whole, nothing set for a cell with none, 93 of 3 for a block); the 21 tables AutoCAD 2021 made and saved as R2000 and R2004, and the two tables of its own drawing, have the data tables ours has, and AutoCAD reads the cells of ours back as the table it saved (32 of 36 files; in the other 4 a border that is not visible, which AutoCAD itself drops in R2000, and a block cell whose scale AutoCAD fits to the cell, are not kept). The data table is named ACDBDATATABLE in R2000 and DATATABLE from R2004, as the
version names it. A table style keeps the formats of its rows the same way: where R2007 and later hold the type, the unit and the format of the value of the data, title and header rows in the style (90, 91 and 1 after the fill of each row), a style going down to R2000 or R2004 keeps them in the XRECORD of its extension dictionary (ACAD_XREC_ROUNDTRIP, after the dictionary's cell style map) as ACAD_ROUNDTRIP_PRE2007_TABLESTYLE, the three in one run of 90, 91, 1 then 92, 93, 2 then 94, 95, 3 (512 for any type, 0 for no unit: `90=2 91=1 1=%lu2%pr2` is a distance of two places), and gets them back, the XRECORD taken out, going up, where a style of R2010 or later is given the 280 of nought it has first as AutoCAD writes it. A text of more than 250 characters stays a table going both ways (its chunks of 250 in 3 and 1 before R2007, in 2 and 1 with the value again in 303 and 302 from it). The columns of an MTEXT, in its extended data of ACAD (and in the MTEXTs of the later columns it links) up to R2013,
become the one MTEXT of R2018 with its embedded object: the text of the columns joined as AutoCAD joins it, each column keeping the
space it ended at, the width (41) that of all the columns (two of 28 with a gutter of 4 are 60).

The visual styles of R2007, R2010 and R2013 are turned into one another's form: the 28 properties they share are the same in
each, in the groups of their version (R2004 and R2007 one form, R2000 without the true colours, R2010 each property followed by
how it is set (176), R2013 the 28 properties as 90, 40, 62 with 420 and 290, and 30 more), the opacity and the specular level written
negative in R2007 where the face modifiers do not use them. What R2013 added is read back from the round trip record AutoCAD keeps
for it under a style of R2010 and before (`RTVSPost2010Prop`), or is what AutoCAD gives a style that has no others; and what a
version has no place for is kept going down, as AutoCAD keeps it: a style going from R2013 to R2010 gets an extension dictionary
holding an xrecord (`ACAD_XREC_ROUNDTRIP`) with the 30 properties (`RTVSPost2010Prop28` to `57`, each with how it is set), and going
to R2007 and R2004, whose style says nothing of how a property is set, also the flags of all 63 properties (`RTVSPropertyOp0` to
`62`: 1 set, 0 left to the parent style, 2 set by the style itself), which a later upgrade reads back, so that the three styles that
give the edges no colour, no jitter and no overhang (every property the parent's but their edge modifiers) come back as they
were; a style of R2010 going to R2007 is given the flags, a style of R2007 going to R2010 loses them. The 24 visual styles of a
drawing AutoCAD wrote come out of every version in the others as AutoCAD writes them, group for group and with their round trip
(8 pairs of versions of drawings of 24 and 25 styles), the extension dictionary of a style going to R2013 taken out. The round trip
of a table that holds no TABLECONTENT is not made going down: AutoCAD reads the table of the older form as the table it holds.

R12 (`db.Save(path, "AC1009")`) is written as AutoCAD 2021 writes the same drawing as R12 (its own DXF of one drawing in each
version, 1 300 lines, 130 hatches, 2 tables and 15 multileaders, which the rules were read from): four sections only (no
CLASSES, no OBJECTS); the header of R12, 133 variables in its order, `$HANDLING` on and the names of the style, layer and
linetype in capitals; the tables in R12's order with no block records, no handles and no extended data, ByBlock and ByLayer
left out of the linetypes, a TrueType font written `txt`, a dimension style with every one of its values (a style of a later
version holds those that differ from the imperial STANDARD's), and the handles an arrowhead is named by turned into the names
of the blocks; the blocks `$MODEL_SPACE` and `$PAPER_SPACE` with the one paper space, the other layouts left out; each entity
with the groups R12 has (no subclass markers, owners, reactors, lineweight, true colour or transparency), a dimension's
bit 32 of its flags gone, an attribute's two flags as the extended data of AcDbAttr, a viewport's view as the extended data of
ACAD MVIEW. What R12 does not hold is broken into what it does, and said: an LWPOLYLINE is a POLYLINE with its handle, an
ellipse a 3D POLYLINE of 129 vertices and a spline one of 32 vertices for each of its control points (128 for four, 224 for a spline through five fit points, whose cubic has seven), a ray and an infinite line a LINE to the extents of the
drawing (in a block a LINE of no length at its base point, as AutoCAD cuts it), all with their handles; the polyline of an ellipse is written as
AutoCAD writes it, its first vertex as its own place (`10`, `20`, `30`) and every vertex with six figures as C's `%#.6g` gives
them (`40.0000`, `1.53457e+06`), the polyline of a spline with its place the zeros `0.00000`; an MTEXT, a pattern HATCH, a LEADER, a MULTILEADER, an MLINE, a table, a MESH (a 3D
polyline of two vertices for each edge, in the order and the direction the MESH lists them) and a HATCH filled solid or with a
gradient (SOLIDs, one colour for a gradient, a hatch with a normal other than (0, 0, 1) left out) are the lines, texts and
polylines they break into, in an anonymous block `*U` (`*X` for a pattern hatch) with an INSERT that has the entity's handle,
where they stood in a space and where they stood in a block alike; the blocks are numbered on from the highest of the drawing in
the order of the handles of the entities they were made of, those of the spaces and of the blocks together, and carry the
current colour on their BLOCK and ENDBLK. The INSERT is on the drawing's current layer and in its current colour and linetype
(`$CLAYER`, `$CECOLOR`, `$CELTYPE`), the look of the entity being on its pieces: the lines of a pattern hatch by block, the SOLIDs
of a fill of the linetype CONTINUOUS, the lines and texts of a table by layer where its block drew them by block. A hatch's INSERT
keeps the extended data the hatch had (the colours of its gradient, an annotative scale) with its application names in capitals,
and holds the hatch itself as R14 did, in the application `ACAD`: a pattern hatch as `HATCH` with its name, scale and angle (in
radians), what it follows (`ASC_BOUNDS`, its loops last first, each the kind of its edges and the handles it follows) and its seed
points (`ASC_SEEDPOINT`) where it is associative, then `R14_HATCH_DATA`, a fill or a gradient `R14_HATCH_DATA` alone: its
handle, the three axes of its system, its elevation and normal, its name, whether it is solid and associative, each loop with
its edges (lines, arcs and ellipses as their numbers, splines as their knots and control points, a polyline as its vertices and
bulges), the pattern's lines, and its seed points. The objects a pattern hatch follows carry `ACAD ASC_BOUNDS` with the handle of
the hatch, and the INSERT of a multileader says `ACAD_MLEADERVER` 2, an application the table of applications holds. The lines
of an MLINE are written element by element, and no entity of R12 names the linetype or the colour BYLAYER or has the flag, turn or
extrusion that is the default (a flag 0 on a polyline or a vertex, a text turned 0, the normal (0, 0, 1)). A 3DSOLID and a REGION are the edges of their faces, face by face and loop by
loop, as AutoCAD writes them: a box 40 by 30 by 20 is 24 LINEs, four to each face, each carrying the normal of its face
((0, 0, 1) left out); a cylinder is four CIRCLEs, those of the flat ends about their normal and those of the curved face about
their own; the apex of a cone is a POINT, a sphere an empty block, an ellipse a 3D polyline of 129 vertices; an edge that is a
spline is left out, and said. The ACIS reader behind it gives the edges of a face on any surface, not only the planes that a
region has. The words of an MTEXT are laid out in the fonts of the drawing (`FontSearchPath`) as AutoCAD wraps them, so that the
long MTEXT of AutoCAD's sample is the 123 TEXTs of AutoCAD's own R12. A dynamic block, which R12 has not, is written as the
anonymous block AutoCAD gives it: `*U` and the first number above that of the anonymous block before it in the table
(`my-dynamic-block` between `*T16` and `*U18` is `*U17`), its INSERTs naming that. The name of a symbol is as R12 holds it,
a character that is no letter, digit, `$`, `-` or `_` an underscore and the name cut to 26 characters
(`{18b73a54-fa43-428c-adcb-c1c7bbdf2c84}` is `_18B73A54-FA43-428C-ADCB-C`), or AutoCAD discards the drawing; two names of one
kind that come to one are told apart as AutoCAD tells them: the one that cleaning changed takes a number, `A{1}` beside `A_1_`
is `A_1_0` beside `A_1_`. A TOLERANCE, a
WIPEOUT, an IMAGE, an underlay, an OLE frame and the layouts beyond the first are left out and said. A hatch filled solid is
the SOLIDs of horizontal strips, as AutoCAD writes it: the area (the loops, the holes taken out) is cut at the height of each
corner of any loop into trapezoids, one running on over a level while the same two edges bound it, unless both of them are upright or no convex tip of the outline stands at the level (a slanted piece is cut where a tip elsewhere, a foot of an arch, stands), each trapezoid
two triangles in the plane flipped (the extrusion (0, 0, -1), x and the height negated, the third corner written again as the
fourth), the strips from the top down, at a level the piece on the right before the one on the left, and after the SOLIDs the
LINEs of the sides of the trapezoids that are not horizontal (of a triangle the right side before the left), a slanted side that
ends in a corner the area wraps round, the point of a V notch cut down from the top or of a bite taken from below, left out: a
rectangle 100 by 60 is 2 SOLIDs and 2 LINEs, with a hole 40 by 20 in it 8 and 8, a triangle 100 wide and 80 high one SOLID and 2
LINEs (32 of 36 probes of polygons, holes and notches equal AutoCAD's own R12 record for record). A gradient is the same SOLIDs
with no lines, in one colour: AutoCAD writes 2 for a LINEAR gradient of a rectangle, as here, but the triangles of its own display
mesh for the others, 152 for a CYLINDER, 332 for a SPHERICAL one on a rectangle 100 by 60, which are not worked out here. A whole
circle is cut as AutoCAD cuts it, into the fewest chords that stand 0.1 off it at the most and 33 at the least (36 for a radius of
25, 76 for 30, 48 for 50, 140 for 100; the 62 circles of 5 to 70 and 200 probed equal AutoCAD's own R12 record for record). A bulge, an arc and an ellipse are cut as a circle is: an arc into the fewest chords that stand 0.1 off it at the most and
the share of 33 its sweep is of a turn at the least (17 for a bulge of 0.5 over a chord of 100), an ellipse into as many chords as a
circle of its major radius, at equal steps of its parameter (50 for 50 by 25); 19 probes of arcs, bulges and ellipses alone in a
fill equal AutoCAD's own R12 record for record. A fill in a plane other than that of the drawing is the SOLIDs and LINEs of the
strips of its plane, the SOLIDs' corners as the plane of the opposite normal reads the world points of the strips, the LINEs in the
world with no extrusion (4 of 4 probes equal; a plane of the normal (0, 0, -1) has the extrusion (0, 0, 1) written on its SOLIDs, as
AutoCAD writes it). A piece beside a hole is cut at the corners of its own two sides only, so that a hole's two chains of corners,
at other heights, cut the piece on their side and not the other: a circle of 25 in one of 50 is 136 SOLIDs and 134 LINEs and one of
10 in 30 142 and 142, as AutoCAD writes them, and a quadrilateral hole in a rectangle 12 or 14 SOLIDs. Which of a hole's corners the
pieces are cut by across the hole, and whether the two sides that meet at its highest or lowest corner are drawn, depend, as AutoCAD
writes it, on where the loop is walked from: where the side on the right of such a corner comes before the one on its left, the
sides are left out and the level at which the shorter ends cuts the piece beyond the longer, where after, both are drawn and nothing
is cut (the eight ways round a quadrilateral hole of 50, 35, 30 and 10 gave 12, 12, 14, 14, 14, 14, 16 and 16 SOLIDs, each equal). Two sides that meet at a corner of an outline, not of a hole, and reach as far
are left out as before, though AutoCAD draws them from some starts of the loop (a star's notch, a W): 328 of 398 probes of fills, patterns and MTEXT runs
equal AutoCAD's own R12 record for record. The
words of a multileader whose style has a frame are followed by it, a POLYLINE of five corners (the first again) of the colour
BYBLOCK round the ink of the words as the fonts lay them out, a landing gap off it, written after the arrowhead; words held at the
middle or the right with no frame have their dogleg run on to the ink of their first line (words of 8 and 20 held at the middle
from a landing at (30, 20) end it at (47.03, 20) instead of (30.36, 20)); and the words of an angular dimension are one TEXT of
AutoCAD's own form in the dimension's block, not an INSERT of a block of pieces (so it takes no block number): `72` and `71` of
nought, the place 2 heights before and 1.77 heights below where the MTEXT was held, six figures, a degree sign written `%%d`
(`90%%d` of 0.18 high at (29.64, 29.6814)). The words of an MTEXT or a dimension with a stack in them (`\S1/2;`, a tolerance `\S+0.1^-0.1;`) are laid out as AutoCAD lays them
out (69 of 70 probes equal its own R12 record for record, one of them in romans.shx): the TEXT of the numerator, a LINE for a bar
(`/`) or a slash (`#`, 55 degrees, twice the height long), the TEXT of the denominator (written where it is empty too, and where the
stack has no sign, `\S12;`), each as high as the letters of the stack. The bar is as wide as the ink of the wider half and the halves
are centred on it (a tolerance, `^`, holds both at the left); the pen moves on by the width and a third of the height. The line is as
high as the stack, 7/3 of its height for a bar (0.42 for letters 0.18 high), and its baseline is that of the denominator, so that
words held at the top lower their plain letters by 0.24, and a dimension's words, held by `\A1;` at the middle of the line, stand
0.12 above and below the plain ones. A stack with a line under or over it (`\L`, `\O`) is the same lifted by a fifth of the
height for each half that has words (each fifth again where both lines are there), each TEXT of a half beginning with `%%O` and
`%%U`; the letters struck through (`\K`) are no look of their own and have no `%%K`, and the lines AutoCAD draws through them, of the
linetype CONTINUOUS, are written after the last entity of the space, outside the block of the TEXTs, half the height above the
baseline (through a stack they are lines of no length, as AutoCAD writes them); 57 of 57 probes of stacks under, over and through
letters equal AutoCAD's R12. In a TrueType font a stack is as high as the ink of its halves, from the lowest to the highest letter
(Arial's 1 and 2 reach 1472 units, 6 beyond its A), begins where the ink of the letters before it ends, and what follows it stands as
far from its ink as the zero has room right of its own (98 of 1466 units in Arial; 13 fonts probed, Palatino's not equal); beside a
big font it is laid out as beside none (63 of 65 probes in all, a half with a letter of the big font in it not). Extended data is written as AutoCAD writes it: the names of its
applications in capitals, a layer's (`1003`) as the table writes it, each point of 1011 to 1013 in five groups (`1011` x, `1021`
and `1031` of 0.0, then `1021` y and `1031` z). The widths of an LWPOLYLINE are the defaults of the polyline's header (`40` and `41`
of its first vertex, each left out where it is nought), and a vertex holds only the widths that differ from them: constant width 2
is `40` and `41` of 2 on the polyline and none on its vertices. The lines of a pattern in a plane other than that of the drawing
carry the extrusion, the hatch's normal; they are written the last pattern line first, and the pieces a hole breaks a line into
the last first (ANSI37 writes the lines at 135 degrees before those at 45). An attribute of version 0 whose position is free has
no AcDbAttr. An attribute of several lines (an MTEXT held at its top left) is one attribute for each line, the tag ending in
`_001`, `_002` and on, the first keeping its place and the others after the other attributes of the block or the INSERT, those of
an INSERT with `AcDbAttr` of the position locked (`MARK` of `M20\Pgrade 8.8` at (0, -5) is `MARK_001` at (0, -7.5) and `MARK_002` at
(0, -11.667); 6 of 6 probes equal).
Checked in AutoCAD 2021: the drawing above saved as R12 opens, AUDIT finds the one fault AutoCAD's own R12 of it has (a text's
extended data naming a handle that is not there), and selects 159 entities of model space, as AutoCAD's own R12 does; the
ten drawings of dynamic blocks open with a clean AUDIT.

There is no `ObjectId` and no transaction. An object is used directly, as AutoCAD's is once a transaction has opened it:
an object read from the drawing is the same object each time it is reached, a change to it is written at once, and an
object erased stays erased, `IsErased` true, refusing to be changed or copied. What AutoCAD gives as an `ObjectId` the
library gives as the object (`Owner`, `BlockTableRecord`, `DimensionStyle`, `GetBlockReferences`), and a handle as the
text the file prints (`Handle`, `"2F"`). `db.GetObject("2F")` and `db.TryGetObject("2F", out DxfDBObject found)` give the
object of a handle back, as AutoCAD's `GetObjectId` gives its id: an entity of model space, paper space or a block, a VERTEX
and an ATTRIB among them, an object of the OBJECTS section, a record of the layer, linetype, text style, dimension style or
block table, or one of those five tables, letters of either case the same. The records the library has no class for carry a
handle but are not found: the APPID, VPORT, VIEW and UCS tables and their records, and the BLOCK and ENDBLK that open and
close a block. The objects are found in one walk of the drawing, kept until the drawing next changes, so that many lookups
between two changes, in a loop, cost one walk. There is no undo: a change stays in the drawing until it is saved or let go.
Editing is for one thread at a time.

The objects are AutoCAD's classes in AutoCAD's order. A [`DxfDBObject`](xref:GeometryHelper.Dxf.DxfDBObject) is any object of the drawing, a [`DxfEntity`](xref:GeometryHelper.Dxf.DxfEntity) one that
is drawn, and a [`DxfCurve`](xref:GeometryHelper.Dxf.DxfCurve) an entity walked along; the tables are a [`DxfSymbolTable`](xref:GeometryHelper.Dxf.DxfSymbolTable) of [`DxfSymbolTableRecord`](xref:GeometryHelper.Dxf.DxfSymbolTableRecord)s; the
dictionaries, records of data and groups of the OBJECTS section are a [`DxfDBDictionary`](xref:GeometryHelper.Dxf.DxfDBDictionary), a [`DxfXrecord`](xref:GeometryHelper.Dxf.DxfXrecord) and a [`DxfGroup`](xref:GeometryHelper.Dxf.DxfGroup);
a layout is a [`DxfLayout`](xref:GeometryHelper.Dxf.DxfLayout), whose plot settings are a [`DxfPlotSettings`](xref:GeometryHelper.Dxf.DxfPlotSettings). A type of entity the library has no class for yet,
a 3DSOLID or an ACAD_PROXY_ENTITY among them, is a [`DxfUnknownEntity`](xref:GeometryHelper.Dxf.DxfUnknownEntity): kept as the file has it, its look editable,
itself erasable and copied, its geometry not moved. A REGION is one too, with the shape of its flat
faces read and written ([regions](entities.md#regions)).

A drawing begins as a file, or new, of a version named: `new DxfDatabase("AC1024")` is a drawing of R2010 with nothing in
it, the drawing `DxfWriter` writes with no shape, in millimetres, with model space and paper space, two layouts, the layer
0, the text style Standard, the dimension styles Standard and ISO-25 and the named object dictionary, its header naming
AC1024 and, after it, the code page ANSI_1252 (`$DWGCODEPAGE`), as AutoCAD's own empty drawings of each of these versions
name it; entities, blocks and layers are added to it as to any other. Its dimensions are those of AutoCAD 2021's metric
template acadiso.dwt, saved as each of these versions: ISO-25 is current (`$DIMSTYLE`), and the header and ISO-25 hold the
same values, arrowheads and text 2.5, a gap of 0.625, two decimal places and a decimal comma, the 20 that differ from
AutoCAD's own written into both; Standard holds AutoCAD's own, arrowheads 0.18 long, as acadiso.dwt's does. So
`GetDimstyleData()` of a new drawing gives ISO-25 in every variable. The versions are AC1015 (R2000), AC1018, AC1021,
AC1024, AC1027 and AC1032 (R2018); `new DxfDatabase("AC1015")` is the very text of
`DxfDatabase.Parse(new DxfWriter().ToString())` but for that code page and ISO-25, and a version of R12 to R14, or one
written otherwise, is refused (`ArgumentException`). Text of R2007 or later
is written in UTF-8 whatever the code page says, and before R2007 a letter the code page has not as its escape: Korean
text copied into a new drawing of R2018 is written as its letters, and into one of R2004 as `\U+CCA0` and on. A letter
beyond U+FFFF, such as the face U+1F600, is written as the escapes of its two halves, `\U+D83D\U+DE00`, in a drawing of
R2018 too: AutoCAD 2021 refuses a DXF that holds its four bytes of UTF-8 ("DXF read error" on the line after it) and
reads the escapes as the letter. A reader
that takes the code page from the header with the version, as ezdxf does, read such a drawing of R2018 as ANSI_1252 while
it named none, and its recover said "Fixed unicode decoding error" for each line of Korean; it reads it as UTF-8 now. A
later version gives its records what that version added to them, as AutoCAD's own saves of each version give them: from
R2004 on each of the three classes says
how many objects of it there are (one, one and two), without which AutoCAD 2021 opens no drawing of R2004 or later
("Group 91 (db instance count) expected"), and each layout gives its shade plot; from R2007 on model space and paper space
give their unit (none), that they may be exploded and that they scale any way. ezdxf 1.4.5 recovers and audits each of
the six with nothing to fix.

### AutoCAD and GeometryHelper

What each daily pattern of AutoCAD's .NET API is here, side by side, is on its own page: [AutoCAD and GeometryHelper](autocad.md).
