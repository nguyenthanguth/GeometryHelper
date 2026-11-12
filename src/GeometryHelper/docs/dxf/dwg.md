# Reading and writing DWG files

[`DwgReader`](xref:GeometryHelper.Dwg.DwgReader) reads a DWG file as the drawing its DXF would give: the
[`DxfDocument`](xref:GeometryHelper.Dxf.DxfDocument) of [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader), or the
[`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) that `DxfDatabase.Open` opens. Everything the other pages say of a
DXF drawing then holds for the DWG. The reader is the library's own code and needs no other program or package.

```csharp
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;

DxfDocument drawing = DwgReader.Read("plan.dwg", new DxfReadOptions { PlanAs2D = true });   // the shapes, read only
DxfDatabase db = DwgReader.Open("plan.dwg");                                               // the drawing, to walk and edit

foreach (DxfLine line in db.ModelSpace.OfType<DxfLine>()) { ... }

db.Save("plan.dxf");                                                                       // saved as a DXF
```

Both take a path or a stream; a stream is read from where it stands to its end and left open. `Read` takes the
[`DxfReadOptions`](xref:GeometryHelper.Dxf.DxfReadOptions) of `DxfReader`, and so does `Open` from a path, which reads
only their `SupportFolders` (see [Shapes](#shapes)). A drawing opened from a DWG is saved as a
DXF by `Save`, and as a DWG by [`DwgWriter`](xref:GeometryHelper.Dwg.DwgWriter) (see [Writing DWG files](#writing-dwg-files)).

## Which files

| Version | Written by | Read |
|---|---|---|
| AC1015 | AutoCAD 2000, 2000i, 2002, and every later AutoCAD on "Save as AutoCAD 2000" | yes |
| AC1018 | AutoCAD 2004, 2005, 2006 | yes |
| AC1021 | AutoCAD 2007, 2008, 2009 | yes |
| AC1024 | AutoCAD 2010, 2011, 2012 | yes |
| AC1027 | AutoCAD 2013 to 2017 | yes |
| AC1032 | AutoCAD 2018 and later | yes |

A DWG of a version not read, R13 and R14 (AC1012, AC1014) among them, is refused with a
`NotSupportedException` whose message names the version and the release. A file protected by a password is refused the
same way.

From AutoCAD 2004 a DWG is a set of pages, compressed and each with its own check sums, and from AutoCAD 2007 its texts
are UTF-16. AutoCAD 2007 to 2009 code their pages by Reed-Solomon, with a compression and CRCs of 64 bits of their own.
The DXF a DWG is turned into is of the DWG's own version, as AutoCAD's DXF of it would be, so a drawing of AutoCAD 2018
opens as a `DxfDatabase` of AC1032, with what that version has: true colours and transparencies, the material of a
layer, the units of a block.

## What is read

The file is turned, in memory, into the DXF that says the same, record for record and with the handles of the file,
and that DXF is read. Compared with the DXF AutoCAD itself writes of the same DWG, the records are the same group for
group, numbers to nine places: sixteen drawings in each of the six versions read, and a drawing of 100 000 lines in
each.

| Part | Read |
|---|---|
| Header | the header variables of the version, `$DWGCODEPAGE` and `$MEASUREMENT` among them |
| Classes | every class of the file |
| Tables | VPORT, LTYPE, LAYER, STYLE, VIEW, UCS, APPID, DIMSTYLE, BLOCK_RECORD |
| Blocks | every block with its entities; the layouts as `*Paper_Space`, `*Paper_Space0` and on, with their viewports |
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs (of several lines too), SOLID, TRACE, 3DFACE, SHAPE (see below), DIMENSION (linear, aligned, angular by two lines or three points, radius, diameter, ordinate), VIEWPORT, HATCH (every boundary kind, pattern, solid fill, gradient), LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, REGION, 3DSOLID and BODY (see below), ACAD_TABLE to AutoCAD 2007 |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MLEADERSTYLE, IMAGEDEF, IMAGEDEF_REACTOR, DBCOLOR, TABLESTYLE to AutoCAD 2007, MATERIAL, VISUALSTYLE, the detail and section view styles (ACDBDETAILVIEWSTYLE, ACDBSECTIONVIEWSTYLE), the manager of the persistent names of subentities beside solids (ACDBASSOCPERSSUBENTMANAGER), and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour (from AutoCAD 2004 with its true colour and transparency, and a colour of a book with its name), linetype scale, visibility, lineweight, owner, reactors, extension dictionary, extended data, and the proxy graphics where AutoCAD saved them |

A layout's viewports come numbered (group 69) as AutoCAD numbers them, which the file does not say: from AutoCAD 2004
the layout lists them, the one showing the paper first; in an AutoCAD 2000 file the viewport whose view is the paper
itself is number 1 and the others follow in the order they were made. Their frozen layers come under group 341 in a
DXF of AutoCAD 2000 and 331 later, and `DxfViewport.GetFrozenLayers` reads both.

A REGION, 3DSOLID or BODY carries its ACIS data as DXF does. An AutoCAD 2000 file keeps it as SAT text, which comes
out line for line, a line that goes on a record starting with a space where the file has a tab, as AutoCAD writes it.
AutoCAD 2004 to 2010 keep the data as SAB, binary ACIS, where DXF wants SAT text: the reader turns it into the SAT
AutoCAD writes of it in the DXF of those versions, line for line. The head gives the version of the data (20800 in a
file of AutoCAD 2004, 21200 of 2007, 21500 of 2010), the count of records, the count of bodies and the flags, then
`16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT` and an empty date, then the unit and the two tolerances; each flag
becomes AutoCAD's word for it (`forward` or `reversed`, `single` or `double`, `I` for a range without a bound and `F`
before a bound), and a real is written to 19 significant digits, as C's `%.19g` writes it: 25.4 is
`25.39999999999999858` and 1E-6 is `9.999999999999999547e-07`. The normal of a plane, an ellipse, a cone or a torus is
divided by its length first, as AutoCAD does, so that a plane whose normal the file holds as
(-0.30504964162071868, ...) comes out as `-0.3050496416207187367`. A spline breaks over lines where AutoCAD breaks it:
five knots to a line, a control point to a line. A line longer than 255 characters is written, as AutoCAD writes it,
in pieces of 255 under group 3 and its last piece under group 1: the loft below has a line of 493 characters. A box, a
sphere, a cylinder, a region of a circle, a closed spline extruded, a solid lofted through a circle, an ellipse and a
circle, booleans, fillets, a swept and a revolved solid, regions of arcs, an ellipse and a spline, twenty solids and
regions in all, come out as AutoCAD's DXF of them in each of the three versions, every line. The SAT is written from
grammars of the records and of the subtypes of splines AutoCAD writes for those solids; a record, a subtype or a word
they do not know (a cone whose sense is reversed, a sweep of another kind) leaves the entity out, counted with the types
not read, so that no SAT is written that is not AutoCAD's.

From AutoCAD 2013 the data is in a store of the file that DXF writes in a section of its own,
ACDSDATA, and it comes out there as AutoCAD writes it: the five schemas AutoCAD gives ACIS data, then a record for each
REGION, 3DSOLID or BODY read, the handle of its entity (320) and its SAB bytes (94 and 310), the entity itself carrying
the GUID of its record. A box 30 by 20 by 10, a sphere, a cylinder and a region of a circle give four records; a plate
400 by 400 with 400 holes gives one of 515 873 bytes, which the store keeps in a page of its own past 256 KB. AutoCAD
gives the records in the order of its store in a file of AutoCAD 2018 and by handle in one of AutoCAD 2013; the reader
gives them in the order of the store.

A MATERIAL and a VISUALSTYLE come out with the groups AutoCAD's DXF of them gives. Of a material that is what differs
from a new material's values: a material of its own red, RGB 200, 30, 30 at 0.8, glossy at 0.3 and seen through at
0.7, gives its colours (90, 91, 92 with the way of each), 44, 140 and nothing of the maps it has not; one of a picture
in each map gives each file, blend, projection, tiling and matrix where they differ. The settings of AutoCAD 2010 on
(colour bleed, luminance, the normal map) are records of the material's extension dictionary, read as any record. A
visual style comes in the form of its version: each property in a group of its own before AutoCAD 2010, each followed
by how it is applied (176) in AutoCAD 2010, and from AutoCAD 2013 the 58 properties after their count (70 58), a
whole number, a real, a colour (257 for none), a yes or no or a text each.

Every drawing of AutoCAD 2013 and later holds a detail view style and a section view style, and AutoCAD saves them in
the older versions too. Each comes out in the groups of its DXF, in its parts marked 71 0 to 71 4: of a section view
style the identifier and the arrows, the cutting plane and its bends, the view label, then the hatch with its angles.
The file keeps them in an order of its own, which the reader puts back. A section view style of its own, its identifier
0.33 high, arrows 0.43, bend lines 0.37 long overshooting by 0.11, ANSI37 at a scale of 2.5 and 60 % seen through at
0.1, 0.2 and 0.3, comes out as AutoCAD's DXF of it in each version; a colour comes as its index, with 420 for a true
colour, from AutoCAD 2004, and not at all in a DXF of AutoCAD 2000, as AutoCAD writes it. Before AutoCAD 2018 the
display name of a style is not in the style but in a record of its extension dictionary (`ACAD_XREC_ROUNDTRIP`), read as
any record. Beside solids AutoCAD keeps a manager of the persistent names of their subentities: of AutoCAD 2013 and later
its DXF gives the count of the names it gave out as 0 and no names, as AutoCAD writes it, though the file holds them.

Texts come back as they were typed, "Tiếng Việt café 日本語" letter for letter: before AutoCAD 2007 they are in the
drawing's code page, with letters beyond it as `\U+XXXX`, and from AutoCAD 2007 they are UTF-16 in the file and UTF-8 in
the DXF, as AutoCAD's DXF of those versions is.

A true colour of a table record (a layer, a viewport's ambient light) is given with the nearest colour index of the
palette, as AutoCAD writes it; the file holds no index beside it. A layer of RGB 10, 200, 30 gets index 102. The
palette is AutoCAD 2021's (see [`DxfColor`](xref:GeometryHelper.Dxf.DxfColor)).

### Shapes

A DWG keeps a SHAPE by its number in the compiled file of shapes of its text style, where DXF gives its name (group 2).
The name is read from that file where it is found: give the folders to look in, in their order, as
`DxfReadOptions.SupportFolders`, as AutoCAD looks in its support folders. The file the style names, `ltypeshp.shx` say,
is looked for in each folder in turn, with `.shx` where the name has none, and first where the style gives it with its
folder.

```csharp
var options = new DxfReadOptions { SupportFolders = { @"C:\CAD\Support", @"C:\CAD\Fonts" } };
DxfDocument drawing = DwgReader.Read("plan.dwg", options);
DxfDatabase db = DwgReader.Open("plan.dwg", options);
```

Where no folder holds the file, or it has no shape of that number, the SHAPE gives its number under group 70 instead
of its name, as AutoCAD writes a shape whose file it does not find: shape 130 of `ltypeshp.shx` is `2 TRACK1` with the
file in one of the folders and `70 130` without it. AutoCAD's own DXFIN refuses the DXF of such a SHAPE ("Undefined
shape 7" for shape 7 of a file it does not find), and one that gives the number as the name too. The place, size, turn, width, slant, thickness and extrusion come as
AutoCAD writes them, a turn, slant and thickness of 0, a width of 1 and the extrusion (0, 0, 1) left out: a TRACK1 at
(10, 20, 30) of size 2.5, turned 0.3 rad, 0.8 wide, slanted 0.2 rad and 4 thick on the normal (0, 0.6, 0.8) comes out
as AutoCAD's DXF of it in all six versions.

## What is not read yet

A type that is not read is left out, with what it owns, and counted: one Debug line of
[`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) names the types with the count of each
(`DwgReader: types that are not read yet were left out: ACDBDATATABLE 2 TABLECONTENT 1 …`), and one Info line sums the
call.

- Entities: a REGION, 3DSOLID or BODY of AutoCAD 2004 to 2010 whose SAB holds a record or subtype the grammars do not
  know (see above), the OLE frames, proxies, and every entity of a class the list above does not name.
- Objects: the TABLECONTENT a table of AutoCAD 2000 or 2004 keeps for a round trip (from
  AutoCAD 2007 it is read, and the TABLEGEOMETRY beside it in every version), DATATABLE, the data of dynamic blocks,
  and the other objects of classes. AutoCAD makes most of these anew in a drawing
  that lacks them. A TABLECONTENT that holds a cell linked to data, the attributes of a block, fields or a cell's
  geometry is left out too: no test drawing has shown AutoCAD's DXF of those yet.
- An ACAD_TABLE from AutoCAD 2010 holds its content itself, rows, columns, cells, merged ranges; it is given in the
  groups DXF keeps of AutoCAD 2007, as AutoCAD writes it: the table's own overrides (93 to 96 and their groups, in its
  head), its sizes, then each cell with how it is merged (173, 175, 176), its own overrides and its value. A cell's
  looks are its own, else those of its row and column, else those of a cell style of the table style's own where they
  differ from those of its kind of row, written 170, 7, 63, 64, 283, 140; its edges are its top and left (and its right
  and bottom in the last column and row), each its own border, else the one the cell beside it shares, else its row's,
  each a lineweight, whether it is hidden (1 hidden, where that is overridden) and a colour. A merged range gives its
  edges on its last cell alone, those of its first cell, without the edges inside it, and the cells it covers give no
  flag of a content format and an empty value: a title over three columns has the edges (172) 0, 0 and 3, its top and
  right on its third cell. The line under a title of 0.35 comes out on each cell of the header below as `279 35`,
  `69 0`, as AutoCAD writes it. A table of 4 by 3 whose cells each override one look, one of all six and one of four
  edges comes out as AutoCAD's own DXF of it, group for group, and so does one whose header is red in GhMono, its title
  filled green and its data 0.3 high at the bottom right with blue lines inside. A table whose row, column and cell take
  cell styles of the style's own does not yet: 9 of its 15 cells differ from AutoCAD's DXF in their edges and flags.
  What a cell of AutoCAD 2010 has beyond those groups, several contents, is not given.
  Its TABLESTYLE is given the same way: its direction and margins, then the rows of data, title and header from the
  cell styles _DATA, _TITLE and _HEADER, each line of the six by its own (top, inside horizontal, bottom, left, inside
  vertical, right). A table style with cell styles of its own (101 on) is given the CELLSTYLEMAP AutoCAD writes in DXF
  alone, with every cell style of the style, under its extension dictionary as
  `ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP`, with the next handles of the drawing.
- A true colour does not exist in the format of AutoCAD 2000. AutoCAD keeps one as round-trip data: extended data of
  the application `ACAD` that starts with `RTT` on an entity, and an extension dictionary holding `ACAD_XREC_ROUNDTRIP`
  on a layer. That data is passed on as the file has it; it is not turned into the entity's true colour, so the entity
  shows the indexed colour AutoCAD chose as the nearest. In the same way an AutoCAD 2004 file keeps the material of an
  entity, which its format has no place for, as extended data of `ACAD` starting with `RTMaterial` and the handle of
  the material; it is passed on as the file has it.
- `$TDCREATE` and `$TDUPDATE` are not given: the file holds the two times as universal time, and they are given as
  `$TDUCREATE` and `$TDUUPDATE`.
- The normal (210) of a planar SPLINE that is not drawn in the XY plane is worked out from its points, its sign by the
  rule that its largest part is positive. AutoCAD agrees on 8 of the 10 splines of the test drawing in five tilted
  planes; for a plane facing (−1, −1, −2) AutoCAD keeps the normal facing down. The curve is the same either way.
- From AutoCAD 2013 the file keeps a periodic spline clamped and marked closed alone; a closed spline of these versions
  is given as periodic too (group 70 bit 2), as AutoCAD writes it.
- The preview image of the file is skipped.

The order of the OBJECTS section is the root dictionary first and then by handle, not AutoCAD's; nothing reads that
order.

## Broken files

Every check sum of the file is checked.

| The file | What happens |
|---|---|
| is not a DWG: a DXF, any other data | `FormatException`: "The data is not a DWG file…" |
| is a DWG of another version, or protected by a password | `NotSupportedException` naming the version |
| has a wrong check sum in the file header, the header variables, the classes or the object map, or is cut short there | `FormatException` naming the part |
| from AutoCAD 2004: has a page whose check sum is wrong, that does not decompress to its size, or that names more data than the file can hold | `FormatException` |
| from AutoCAD 2007 to 2009: has a wrong CRC in its file header, its page map, its section map or a page | `FormatException`; a byte changed in the parity of a Reed-Solomon block changes nothing read, and the file reads |
| has one object with a wrong check sum, one that cannot be decoded, or one whose values the library's geometry refuses (a damaged spline whose points lie too far apart for their plane to be worked out) | that object is left out and the others are read; one Warn line gives the count and the first fault |

Where an entity is left out, those after it in its block are still read. No broken file makes the reader hang or eat
memory. 10 500 copies of the drawings of AutoCAD 2000 and 12 800 of the later versions, with bits changed at random
(half of the later ones inside the decompressed objects under a repaired check sum), were each read or refused in under
0.14 s, the process never above 36 MB; 8 000 copies of the drawings of AutoCAD 2007, changed the same way, in under
0.15 s and 58 MB; 19 200 copies of the drawings of hatches, viewports, leaders, images, multilines, tables and solids
in every version, half changed inside the objects of those types, in under 0.22 s and 75 MB. The sections of one file may decompress to at most 64 times the size of the file
and 8 MB more, a page to at most 1 MB; AutoCAD's drawings take 5 times their size or less.

## Speed

A DWG is read in about two and a half times the time of reading AutoCAD's DXF of it, as it is turned into that DXF
first. A drawing of 100 000 lines, 1.9 MB as a DWG of AutoCAD 2018 and 14 MB as its DXF:

| Version | DwgReader.Read | of which to DXF | DxfReader.Read of AutoCAD's DXF |
|---|---|---|---|
| AC1015 (5.2 MB) | 758 ms | 477 ms | 292 ms |
| AC1018 | 720 ms | 447 ms | 266 ms |
| AC1021 (2.0 MB) | 750 ms | 471 ms | 270 ms |
| AC1024 | 762 ms | 456 ms | 246 ms |
| AC1027 | 748 ms | 418 ms | 247 ms |
| AC1032 | 771 ms | 445 ms | 303 ms |

The best of five reads on one machine, .NET Framework 4.8; AC1021 was timed later, on the machine under other load,
where AC1018 took 747 to 796 ms in the same runs. A small drawing of a few hundred objects reads in 1 or 2 ms.

## Writing DWG files

[`DwgWriter`](xref:GeometryHelper.Dwg.DwgWriter)`.Write` writes a `DxfDatabase`, with every record it holds, or the
shapes of a `DxfDocument`, as the drawing [`DxfWriter`](xref:GeometryHelper.Dxf.DxfWriter) makes of them, to a path or a
stream. The file is of AC1032, the version AutoCAD 2018 and later save in; another version is refused with a
`NotSupportedException` that names it.

```csharp
DxfDatabase db = DxfDatabase.Open("plan.dxf");
DwgWriter.Write(db, "plan.dwg");                         // AC1032

DxfDocument shapes = DxfReader.Read("plan.dxf");
DwgWriter.Write(shapes, "shapes.dwg");                   // the shapes alone, each on its layer
```

The drawing is written as its DXF, and each record of that DXF is put into the object of the DWG that says the same,
with the handles of the drawing: the header variables, the classes, the tables, the blocks, the entities and the
objects, in the container of pages a DWG of AutoCAD 2004 and later has, compressed, with every check sum. The same
drawing gives the same bytes every time.

| Part | Written |
|---|---|
| Header | every header variable of the version; those a DXF does not give (a drawing of `DxfWriter` gives few) as AutoCAD 2021 gives a new drawing |
| Tables | VPORT, LTYPE, LAYER, STYLE, VIEW, UCS, APPID, DIMSTYLE, BLOCK_RECORD |
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs, of one line or of several, SOLID, TRACE, 3DFACE, SHAPE given by its number (see below), DIMENSION (every kind), VIEWPORT, HATCH, LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, ACAD_TABLE, REGION, 3DSOLID and BODY with their ACIS data (see below) |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MLEADERSTYLE, TABLESTYLE, DBCOLOR, IMAGEDEF, IMAGEDEF_REACTOR, RASTERVARIABLES, WIPEOUTVARIABLES, MATERIAL, VISUALSTYLE of AutoCAD 2013 and later, the detail and section view styles, the manager of the persistent names of subentities, the TABLECONTENT and TABLEGEOMETRY a table keeps for a round trip, and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour (index, true colour, transparency, a colour of a book through its DBCOLOR), linetype scale, visibility, lineweight, material, plot style, owner, reactors, extension dictionary, extended data, proxy graphics |

A record of another type is left out and counted: one Warn line of `GeometryHelperLog` names the types with the count of
each (`DwgWriter: types that are not written yet were left out: SHAPE 1 …`). Every handle to a record
left out is written as 0, every dictionary entry naming one is dropped, and the vertices and attributes it owns go
with it. Left out so far: a VISUALSTYLE of the form before AutoCAD 2013 and a REGION, 3DSOLID or BODY of SAT text, as a
DXF of an older version gives them; and a SHAPE given by its name. A new drawing of AutoCAD 2018 is written whole:
nothing of it is left out.

A SHAPE is written where its DXF gives its number: under group 70, as the reader gives a shape whose file of shapes it
does not find, or as the name, `2 130`. A name is not looked up, and a SHAPE given by its name alone, as AutoCAD's DXF
gives it, is left out. DXF does not say which text style a shape is of; the shape is written of the first style of the
table that is of a file of shapes (flag 1). A drawing of TRACK1 and BOX of `ltypeshp.shx` read without the file, so
that each gives its number, is written back with those numbers; AutoCAD opens it, `AUDIT` finds 0 errors, and its own
DXF names the four shapes TRACK1, TRACK1, TRACK1 and BOX again. A fifth shape, number 7 of a file that is nowhere, goes
to `ltypeshp.shx` too, the first style of shapes, where it had its own style in the file read.

A REGION, 3DSOLID or BODY is written where the section ACDSDATA of its DXF holds its SAB data, as a DXF of AutoCAD 2013
and later gives it: the entity with the GUID of its data, and the data in the data store of the file, the section
AcDb:AcDsPrototype_1b, laid out as AutoCAD lays it out. The records go in the order of the section ACDSDATA, one data
segment holding them all; a record past 256 KB goes in pages of its own, of at most 1 048 496 bytes each; the search
segment lists the handles in their order, each with the place of its record. A box 30 by 20 by 10, a sphere, a
cylinder and a region of a circle are written as AutoCAD's own bits, entity for entity, and their store of 11 520 bytes
too, but the 12 bytes after the index of the segments that AutoCAD leaves as its memory held them; so is the store of
a plate 400 by 400 with 400 holes, its 515 873 bytes of SAB in a page of its own. AutoCAD opens both, `AUDIT` finds 0
errors, and its own DXF of each is the DXF it was written from, the SAB data byte for byte.

A MATERIAL and a VISUALSTYLE are written from the groups their DXF gives, a group not given taking a new material's
value. Of AutoCAD's new drawing the 24 visual styles and the three materials are AutoCAD's own bits but the last bit of
the matrices of the materials, 1/48 written in DXF to 16 places; so are a material of colours of its own and one of a
picture in every map. AutoCAD opens the drawing of these (accoreconsole), `AUDIT` finds 0 errors, and its own DXF of it
is the DXF it was written from.

The detail and section view styles are written from the groups of their DXF, a group not given taking the value of
AutoCAD's style of a new drawing. The detail view style of AutoCAD's new drawing is AutoCAD's own bits; its section
view style, and a detail and a section view style of every property their own, are too but the last bit of a double
DXF writes to 16 places (an angle of the hatch, 1.570796326794896). AutoCAD opens the
drawing of these, `AUDIT` finds 0 errors, and its own DXF of it is the DXF it was written from. The manager of the
persistent names of subentities is written as its DXF gives it.

A table is written in the form of AutoCAD 2010, its content inside the ACAD_TABLE, made of the groups DXF keeps of
AutoCAD 2007: the sizes of its rows and columns, each cell's value, and a merged range for each cell that spans others
(175 and 176); its first row takes the title's cell style and its second the header's, unless its table style
suppresses them. A cell's own overrides (its flags, 91) are written as AutoCAD keeps them: its alignment and fill in
its cell style, its colour, text style and height in its content's format, each edge it gives as a border of the cell,
a colour by block, a lineweight of -2 and a showing not given taken as no override. Where the table keeps a
TABLECONTENT for its round trip, of as many rows and columns, the cell style of the whole table and of each row and
column, the cell style each takes and the borders of a cell that gives none of its own come from it, as AutoCAD reads
them; the TABLECONTENT is written as its own groups give it. Else the table's own overrides (93 to 96) go into the cell
style of each row of their kind and its margins into the table's own. A table style is written with its rows of data,
title and header as the cell styles _DATA, _TITLE and _HEADER beside Table, its margins kept where they are not
AutoCAD's 0.06, a border where it is not by block, of the default lineweight and shown, on its own line of the six.
The cell styles of a table style's own (101 on) are written after those three from the CELLSTYLEMAP the style holds in
DXF, each after its own id as AutoCAD writes it, and a row, column or cell that takes one keeps it; the rows always come
from the style's groups, which an edit changes, not from the map. The CELLSTYLEMAP itself, with the extension
dictionary of the table style that holds it, is not written, as a DWG of AutoCAD has none. A style whose map cannot be
read, or a cell that takes a style the map does not hold, is written as before: the row takes the data's style, the
column or cell none, and the looks AutoCAD writes into each of its cells in DXF are kept as the cells' own. AutoCAD
works out the places of the cells' words again as it opens the file.

An attribute of several lines is written with its MTEXT, as AutoCAD writes it, the text's extents left for AutoCAD to
work out; a multileader with its context, its leaders before its content; RASTERVARIABLES and WIPEOUTVARIABLES, which a
drawing with images or wipeouts made in code holds, with their settings.

Proof: the eight drawings of the reader's tests and the eight of hatches, viewports, leaders, images, multilines, tables,
solids and meshes, each as AutoCAD 2021 wrote its DXF, written by `DwgWriter`:

- read back by `DwgReader`, they give the records of the DXF they were written from, but those of the types left out and
  the dictionary entries and handles that named them;
- AutoCAD 2021 opens each (accoreconsole) and `AUDIT` finds 0 errors;
- AutoCAD's own DXF of each is the DXF it was written from, but the types left out, which AutoCAD makes anew with new
  handles (`$CMATERIAL` names its new material);
- object for object, the bits are AutoCAD's own for 848 of the 953 objects of the first eight and 835 of the 908 of
  the second; the others differ where the DXF does not carry what the file does: the last bit of a double of 16
  places, a normal of (0, 0, 1) that AutoCAD writes whole for some entities, the extents of an MTEXT.

The same proof for the types written since: three multileaders and two multileader styles, an attribute definition and
an attribute of two lines each, two tables of 3 by 3 and 2 by 4 with merged cells and two table styles, and a drawing
with an image and a wipeout made in code. Each reads back to the records it was written from; the table styles, the
multileader styles and the TABLECONTENT are AutoCAD's own bits, and of the multileaders and attributes all but the last
bit of a double of 16 places and the extents of the words. AutoCAD opens each, and its own DXF of each is the DXF it was
written from. `AUDIT` finds 0 errors in each but the drawing of tables, where it finds the same 6 in the table style of
the second table as in AutoCAD's own file of that drawing.

And for the overrides of tables: five drawings, a table of cells each overriding one look, a table of the overrides
of AutoCAD 2007 a table gives its kinds of row, two of a clean table style of its own with borders, margins and a
merged range, and a table of a cell of all six overrides and cells of four edges. Each reads back to the records it was
written from, AutoCAD's own DXF of each is the DXF it was written from but `$CMATERIAL`, and `AUDIT` finds 0 errors in
each. A sixth, a table whose row, column and cell take two cell styles of the style's own, GhNote (101) and GhWide
(102): its table style is AutoCAD's own bits but the one that says it has no extension dictionary, AutoCAD's own DXF of
it is the DXF it was written from but the types left out, its two cell styles group for group, and `AUDIT` finds 0
errors.

A drawing of 100 to 450 records is written in about 0.1 s, in some 10 KB: AutoCAD's own file of the same drawing is
13 to 17 KB. A drawing of 100 000 lines is written in 1.1 s from its `DxfDatabase`, in 2.1 MB to AutoCAD's 1.9 MB.
