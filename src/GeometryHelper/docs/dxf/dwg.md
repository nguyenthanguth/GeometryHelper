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
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs (of several lines too), SOLID, TRACE, 3DFACE, SHAPE (see below), DIMENSION (linear, aligned, angular by two lines or three points, radius, diameter, ordinate), VIEWPORT, HATCH (every boundary kind, pattern, solid fill, gradient), LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, REGION, 3DSOLID and BODY (see below), ACAD_TABLE |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MLEADERSTYLE, IMAGEDEF, IMAGEDEF_REACTOR, DBCOLOR, TABLESTYLE, MATERIAL, VISUALSTYLE, the detail and section view styles (ACDBDETAILVIEWSTYLE, ACDBSECTIONVIEWSTYLE), the manager of the persistent names of subentities beside solids (ACDBASSOCPERSSUBENTMANAGER), the fields and the draw order (FIELD, FIELDLIST, SORTENTSTABLE), the link of a table to data (DATALINK), the cell styles a table style of AutoCAD 2000 to 2007 keeps for a round trip (CELLSTYLEMAP), the data of a dynamic block (the evaluation graph ACAD_EVALUATION_GRAPH with the linear, visibility, flip, rotation, XY, polar, point, base point, lookup and alignment parameters, their grips, the stretch, polar stretch, move, scale, rotate, flip, array and lookup actions and the components of the grip locations, written BLOCKLINEARPARAMETER, BLOCKSTRETCHACTION and so on; ACDB_BLOCKREPRESENTATION_DATA, ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION, and the block properties table with its grip, the user and the constraint parameters, the proxy node and the associative network of variables, dependencies and groups of constraints, written ACDBASSOCNETWORK, ACDBASSOCVARIABLE and so on), and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour (from AutoCAD 2004 with its true colour and transparency, and a colour of a book with its name), linetype scale, visibility, lineweight, owner, reactors, extension dictionary, extended data, and the proxy graphics where AutoCAD saved them |
| Preview | the picture of the file's preview, where it holds one, as the section `THUMBNAILIMAGE` of the DXF, last: a bitmap of Windows without its file header (`DxfDatabase.ThumbnailBitmap` gives its bytes), or a PNG file |

The preview of a drawing AutoCAD has drawn no picture of (a drawing it saves in its console, or from a program) is a
header of 80 zero bytes alone, and gives no section. The sample drawings of AutoCAD 2007 keep a bitmap of 256 by 117
pixels, 8 bits, which comes out whole: the header `28000000 00010000 75000000 0100 0800` and its 31 016 bytes. A
preview that is cut short or broken is left out and does not refuse the file.

A layout's viewports come numbered (group 69) as AutoCAD numbers them, which the file does not say: from AutoCAD 2004
the layout lists them, the one showing the paper first; in an AutoCAD 2000 file the viewport whose view is the paper
itself is number 1 and the others follow in the order they were made. Their frozen layers come under group 341 in a
DXF of AutoCAD 2000 and 331 later, and `DxfViewport.GetFrozenLayers` reads both.

A viewport clipped by an entity keeps the link (`DxfViewport.NonRectClipEntity`, group 340) and the entity's reactor in both
directions: AutoCAD's own DWG of R2000 and R2018 read to the clipped viewport and its boundary, the frame the box of the
boundary, and a drawing written with a viewport clipped to a circle reads back the same, AutoCAD opening both as clipped.

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

A FIELD comes in the groups of AutoCAD's DXF: its evaluator and code (a code of more than 250 letters in pieces of 250
under group 3, the rest under 2), the fields it holds (360) and the objects it names (331), its options and state
(91, 92, 94, 95, 96), the values it keeps by name (93 and, for each, its name under 6 and its value), its own value as
`ACFD_FIELD_VALUE` and the text it shows (301 and its length, 98). A value is a whole number, a real, a text or a date
(its bytes under 92 and 310), with from AutoCAD 2007 its flags, unit, format and text; before then the format of the
field is its group 4. AutoCAD's DXF gives the values by name in the order of their names, whichever order the file keeps
them in, and the reader does the same: a field of a sheet set with its five values (`SheetSetCompId`,
`SheetSetCompName`, `SheetSetFile`, `SheetSetId`, `SheetSetPropertyName`) comes out as AutoCAD's DXF of it. A file that
AutoCAD 2005 saved holds no `ACFD_FIELDTEXT_CHECKSUM` that AutoCAD's DXF of it adds, and the reader does not make one. A
field with a value of a kind not read (a point, a handle, a buffer) is left out. The FIELDLIST holds the fields of the
drawing (90 and 330), the SORTENTSTABLE the entities of a block with the handle each is sorted by (331 and 5, after the
block under 330), and the DATALINK of a table the adapter, description, tooltip and connection (1, 300, 301, 302), its
options, the time of its last update (170 to 177), the objects it fills (94 and 330) and its data map (`CUSTOMDATA`, a
name and a value each). The file holds the map only where bit 8 of the options says so, and DXF leaves that bit out of
group 90 and writes an empty map either way: the data link of a sheet list with options 5 comes out with no entry, the
data link of a spreadsheet with options 0 in DXF (8 in the file) with its three. In the 96 sample drawings of AutoCAD
2021 the 2 646 fields, 46 field lists, 256 draw order tables and 5 data links read as AutoCAD's DXF of them, but for
142 fields whose DXF has the checksum and for the order of the objects a data link fills, which AutoCAD's DXF gives in
the order of its memory and the file in the order of the handles; each of the 4 types in the DWG of R2000, R2004, R2007,
R2010, R2013 and R2018 that AutoCAD 2021 saves of a sample.

A dynamic block is read with its data: the evaluation graph (ACAD_EVALUATION_GRAPH: two numbers, each node by its index,
flags, id, the expression it holds and four numbers, each edge by its index, flags, count, its two nodes and five numbers)
and the expressions its nodes name. These are the parameters (BLOCKLINEARPARAMETER, BLOCKVISIBILITYPARAMETER,
BLOCKFLIPPARAMETER, BLOCKROTATIONPARAMETER, BLOCKXYPARAMETER, BLOCKPOLARPARAMETER, BLOCKPOINTPARAMETER,
BLOCKBASEPOINTPARAMETER, BLOCKLOOKUPPARAMETER and BLOCKALIGNMENTPARAMETER, each with its element, its points, the
connections of its displacements and, where it has them, its labels, value sets and the states a visibility parameter
shows), their grips (BLOCKLINEARGRIP, BLOCKVISIBILITYGRIP, BLOCKFLIPGRIP, BLOCKROTATIONGRIP, BLOCKXYGRIP, BLOCKPOLARGRIP,
BLOCKLOOKUPGRIP and BLOCKALIGNMENTGRIP) with the components of their locations (BLOCKGRIPLOCATIONCOMPONENT), and the
actions (BLOCKSTRETCHACTION with the entities it moves, its frame, the points of each entity and of each node it moves,
its multiplier and angle; BLOCKPOLARSTRETCHACTION, BLOCKMOVEACTION, BLOCKSCALEACTION, BLOCKROTATEACTION, BLOCKFLIPACTION,
BLOCKARRAYACTION and BLOCKLOOKUPACTION with its table of values). The representation data each anonymous block of the
block keeps (ACDB_BLOCKREPRESENTATION_DATA) and the purge preventer (ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION) are read too.
An expression that holds a value (a real of kind 40, a short of kind 70, the only two kinds of AutoCAD 2021's samples)
gives it after an empty text as AutoCAD's DXF does (1, 70 and 140 or 70). The versions of an expression and of an element
(98 and 99) are 33 and 174, as AutoCAD 2021 gives them in the DXF of a drawing of an older version. The entities a stretch
binds are read in the order the file keeps them and written in the order the groups give. AutoCAD keeps them in an order of
its own, which is no order of their handles: the DWG of 13 of the 115 stretch actions of the samples (in the drawings of
Architectural - Metric and Mechanical - Imperial) holds a turn of the handle order, a run from the middle first and then the
handles before it: `4FB8 4FB9 4FBB 4FAA 4FB0 4FB7` for the handles `4FAA` to `4FBB`, the same wherever the entities recur. That
is the order of a hash table by a key that is not the handle: the handle modulo a prime gives the four actions of one block
(2551) and the eight of another (857), and no one prime gives both. AutoCAD's DXF lists them as the file does, but where it
does not: of the stretch actions of the 13 drawings its DXF of the originals lists them as the file does in all but 4 (the
handle order, which the reader gave before, differs in 7), its DXF of the R2010 files it saves from them in all (5 by handle),
and that of the R2018 files in all but 10 (13 by handle), where the file's order is another of the same entities (it is not
the order of the objects in the file either: 26 of the 74 actions of two entities or more). Written again, the 102 stretch actions of the R2018
files are AutoCAD's own bytes, and each of the 13 drawings written to R2010 and R2007 lists them in AutoCAD's DXF as often (5 and 6 of the 13
differ, against 8 and 14 with the handle order). In AutoCAD 2021's 120 sample drawings 72 of the 90 evaluation graphs
are read, as AutoCAD's DXF of each drawing has them, group for group (12 of a linear parameter with a stretch action
alone, the nine sheets `A-01` to `S-04` among them; counting a graph under each kind it holds, 41 have a visibility
parameter, 40 a linear one, 18 a point parameter, 16 a base point parameter, 14 a flip, 12 a rotation, 10 a lookup, 6 an
alignment parameter, 2 an XY and 2 a polar parameter, and 33 a stretch action, 24 a move, 12 a scale, 4 an array). The
other 18 hold a constraint parameter (BLOCKHORIZONTALCONSTRAINTPARAMETER, BLOCKVERTICALCONSTRAINTPARAMETER,
BLOCKRADIALCONSTRAINTPARAMETER), a user parameter (BLOCKUSERPARAMETER), a properties table (BLOCKPROPERTIESTABLE and its grip
BLOCKPROPERTIESTABLEGRIP) and the proxy node of the graph (ACDB_DYNAMICBLOCKPROXYNODE), and lean on the associative network
of the block: the networks (ACDBASSOCNETWORK), the variables (ACDBASSOCVARIABLE), the dependencies (ACDBASSOCDEPENDENCY,
ACDBASSOCGEOMDEPENDENCY, ACDBASSOCVALUEDEPENDENCY) with their bodies (ASSOCDIMDEPENDENCYBODY, BLOCKPARAMDEPENDENCYBODY) and
the groups of 2D constraints (ACDBASSOC2DCONSTRAINTGROUP, with the 22 classes of node of the samples: points, lines, arcs,
circles and the constraints between them). No source gives their layouts; each was read from AutoCAD 2021's own DWG bytes
against its DXF of the same drawing. All 18 graphs are read from a DWG of AutoCAD 2010, which is how the samples are saved:
the 1 789 objects of these types equal AutoCAD's DXF group for group, but for the order of what a group owns (the actions of
a group come in the file in an order of its own, no order of their handles, which the reader gives; AutoCAD's DXF lists them in
a third order that turns the file's into another each time a drawing is saved and read again, so that the DXF of its R2018
file is the order of its R2010 file and the DXF of that the order of the R2018 file; and the nodes by their identifier, as the
reader gives them). A DWG that AutoCAD 2018 saves of the same drawings holds the actions (the networks, variables and groups) of
version 2, which carry four numbers more, and names the way a dependency leans on an edge by a flag and a number of a short
(1 for `AcDbAssocSingleEdgePersSubentId`, 3 for `AcDbAssocEdgePersSubentId`) where the DWG of AutoCAD 2010 gives the class
as a text; both are read. The constraint group of version 2 is another layout, and is read and written too: past the plane,
the actions it owns and the next identifier it holds two numbers and a flag (0 in every sample), the table of the classes of
its nodes (their count and their names, in the order of the first identifier that names each), the count of the nodes, a
directory of every node (the number of its class in the table, from 1, and its identifier) and then the nodes by identifier,
each with its identifier first, then the identifiers it names, its byte and what its class holds (the circles and the arcs hold
a fourth real after their three, 0 in every sample). AutoCAD's DXF of a drawing of 2018 gives the same: the numbers and the
flag, the directory as class name and identifier, and the nodes without their class. The 34 groups of AutoCAD's own R2018 files
equal its DXF group for group but for the order of the actions they own (a hash order of its own: no rule of the handles or of
the indexes of the dependencies gives it), and every one is written again from the DXF the reader makes of its file to the very
bits of AutoCAD's object (its class number apart; the actions come in the order of the file, the handles of the stream
included, which a handle order wrote differently in all 34, and so for the 34 groups of its R2010 files). A group of version 1, which a DWG of AutoCAD 2010 gives, is brought up to the version 2 when it is
written to R2018, as AutoCAD does when it saves one: the action of version 2 with its four numbers 0, the nodes by
identifier, the table and the directory, and a 0 for the fourth real of a circle or an arc. A group of the version 2 is taken
down to the version 1 when it is written to a DWG of R2010 or R2007, as AutoCAD does when it saves such a drawing (see
below). The network and the variable are of the version 2 of an
action (the four numbers more) in a DWG of R2013 and later, and of the version 1 before, whichever version the groups give,
as AutoCAD 2021 writes them: its own R2018, R2013, R2010 and R2007 files of Architectural - Metric hold them at 2, 2, 1 and
1, and the 6 networks and 35 variables of a drawing saved by AutoCAD 2010 and written to R2018 are the bits of AutoCAD's own.
The largest real number, which AutoCAD keeps for a value not yet set, is in its DXF `1.797693134862314E+99`, and so it is
read: its digits as the runtime writes them rounded past the largest number and could not be read back, which stopped the
whole DWG from being read.

The CELLSTYLEMAP of a DWG of AutoCAD 2000 to 2007, which a table style keeps for a round trip (under its extension
dictionary as `ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP`), is read as its cell styles in the layout a table style of
R2010 and later holds them, each followed by its id, kind and name: a count (90), then each cell style as DXF gives it
(its format, six grid formats, `CELLSTYLE_BEGIN`, id, kind and name). A drawing of AutoCAD 2000 to 2007 usually holds two,
and was left out of the DXF as 2 of the types not read; the table drawings of R2000, R2004 and R2007 read them as
AutoCAD's DXF gives them, group for group. AutoCAD gives each map a new handle in its DXF and makes them in another
order; the reader keeps the handle of the file.

Texts come back as they were typed, "Tiếng Việt café 日本語" letter for letter: before AutoCAD 2007 they are in the
drawing's code page, with letters beyond it as `\U+XXXX`, and from AutoCAD 2007 they are UTF-16 in the file and UTF-8 in
the DXF, as AutoCAD's DXF of those versions is.

A DXF of R2004 or before written as a DWG gets its letters in place of the escapes: its text holds a letter beyond the code
page as `\U+1EDB`, and a letter of a multibyte code page as `\M+182A0` (the number after the plus is the code page, 1
Shift-JIS, 2 Big5, 3 Wansung, 4 Johab, 5 GB 2312, then two bytes in four digits; that one is the Japanese letter a,
U+3042), and the DWG keeps the letter, so that the layer `L\U+1EDBp \U+1EA9n` is written `Lớp ẩn`, as AutoCAD writes it (it
found "Un-normalized symbol name" in the escape, 2 errors in AUDIT of the drawing of properties). The escape is put in
place in every text of such a DXF, the names and the words of a TEXT or an MTEXT alike, as AutoCAD does when it reads such
a DXF (`\u+` and `\m+` in small letters are escapes too); in a DXF of R2007 and later an escape is the text typed and is
left. A multibyte code that is no letter of its page is left as written.

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
(`DwgReader: types that are not read yet were left out: ACDB_MLEADEROBJECTCONTEXTDATA_CLASS 10 VP_ENT_HDR 5 …`), and one Info line sums the
call.

- Entities: a REGION, 3DSOLID or BODY of AutoCAD 2004 to 2010 whose SAB holds a record or subtype the grammars do not
  know (see above), the OLE frames, proxies, the surfaces (EXTRUDEDSURFACE, REVOLVEDSURFACE and the others), and every
  entity of a class the list above does not name. No drawing on the machine the reader was checked on holds an OLE frame
  or a proxy (AutoCAD 2021's own 113 sample drawings do not), AutoCAD's console drops the data of an OLE frame, and so
  neither is read nor written until a drawing of AutoCAD's own shows how it lays them out.
- Objects: the other objects of classes, and the data of a dynamic block with a group of a class of 2D constraint node outside
  the 22 of the samples (ACDBASSOC2DCONSTRAINTGROUP). The group is read from a DWG of AutoCAD 2007 to 2010 (the layout of
  version 1) and from one of AutoCAD 2013 and 2018 (version 2, see above), and written to R2018 in the layout of version 2
  (written in the layout of 2010 into a DWG of R2018 it was erased by AutoCAD when it opened the file, with the dependencies it
  owned, and the file was refused with `ErrorStatus` 53, a filer error, for the network that listed it). A dynamic block whose
  evaluation graph names an expression that is left out is left out whole, the graph and every expression it owns, and is an
  ordinary block in the DWG written: the graph of a block with a constraint group is not half of a graph (AutoCAD drops the
  nodes whose expression it does not find). So is a network that names an action left out (the variables and dependencies it
  owns), and a user or constraint parameter whose variable or dependency is. None of the 90 graphs of the sample drawings is
  left out of an R2018 any more (18 were, in the `Architectural`, `Civil`, `Electrical`, `Mechanical` and `Structural`
  drawings); a group of version 2 is written to a DWG of R2010 and R2007 in the first layout (see below).
  A TABLECONTENT that holds the attributes of a block or a cell's cached geometry is left out too, with its ACAD_TABLE;
  no sample drawing has one. A table of fields and links is read: the cell of a field gives it in group 344 and the
  TABLECONTENT lists the fields the table owns (92 and 360), a cell linked to a data link gives the link, its rows and
  its columns (92, 340, 93, 94, 96). The five tables of AutoCAD 2021's sample drawings that hold them (`Cover Sheet`,
  `Title Sheet`, `T-01`, `Mechanical - Data Links`, `Data Extraction and Multileaders Sample`) were left out before,
  and AutoCAD drops the fields no cell names from a drawing it opens: the 60 fields a table of `Cover Sheet` owns were
  gone from AutoCAD's own DXF of the drawing written again, and are there now. AutoCAD's DXF of each table is read group for
  group but for the numbers of the anonymous blocks.
  The DATATABLE a table of AutoCAD 2000 or 2004 keeps beside its TABLECONTENT (ACDBDATATABLE in a DXF of R2000) is read
  as AutoCAD's DXF gives it, its columns of whole numbers (93), reals (40) and the handles of the cell values it owns
  (360); a column of another kind has not been seen, and leaves the object out. Written as R2018 it is left out, as
  AutoCAD leaves it out, the table keeping its TABLECONTENT.
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
  on a layer, and `DBColXD` on the DBCOLOR of a colour of a book. Read, that data is passed on as the file has it; it is
  not turned into the entity's true colour, so the entity shows the indexed colour AutoCAD chose as the nearest. Written to
  R2018 it is turned into the colour, as AutoCAD does when it saves such a drawing as R2004 or later: the entity's true
  colour and the name of its book colour (62, 420, 430), the layer's own colour with its extension dictionary and the
  record in it left out, the DBCOLOR's index, colour and names. AutoCAD 2021 opens a drawing of 4 layers and 9 entities in
  true colours and a colour of the book DIC, saved by it as R2000 and written to R2018 by the library, with `AUDIT` 0 and
  the 37 true colours and 3 names of its own R2018. A transparency is not in the round-trip data of R2000. In the same way an AutoCAD 2004 file keeps the material of an
  entity, which its format has no place for, as extended data of `ACAD` starting with `RTMaterial` and the handle of
  the material. Written to R2018 it is taken into the entity's material, as AutoCAD does reading the file and saving it as
  R2018: the entity has the material and no extended data (the LINE of `matvis` with the handle 78 is AutoCAD's own bytes,
  and so are the BLOCK and ENDBLK of `xclip` with the handle 0, which have none), and AutoCAD's DXF of the rewrite holds
  no `RTMaterial`.
- `$TDCREATE` and `$TDUPDATE` are not given: the file holds the two times as universal time, and they are given as
  `$TDUCREATE` and `$TDUUPDATE`.
- The normal (210) of a planar SPLINE that is not drawn in the XY plane is worked out from its points, its sign by the
  rule that its largest part is positive. AutoCAD agrees on 8 of the 10 splines of the test drawing in five tilted
  planes; for a plane facing (−1, −1, −2) AutoCAD keeps the normal facing down. The curve is the same either way.
- From AutoCAD 2013 the file keeps a periodic spline clamped and marked closed alone; a closed spline of these versions
  is given as periodic too (group 70 bit 2), as AutoCAD writes it.

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
stream. The file is of AC1032, the version AutoCAD 2018 and later save in, unless a version is given: AC1027 (AutoCAD 2013), AC1024
(2010), AC1021 (2007), AC1018 (2004) and AC1015 (2000) are written too, and another version is refused with a `NotSupportedException` that
names it. A drawing of a later version is made into the DXF of the version first,
as `DxfDatabase.Save(path, version)` gives it, and what AutoCAD keeps in such a file in its own way is put in as AutoCAD puts
it: the multileaders and their styles with the extended data `ACAD_MLEADERVER` (before R2010), the texts of the
proxy graphics of a table or a multileader in 8 bits (type 11) and not in 16 (type 38) in R2004, the fields in the form before
R2007, no vertex ids in a polyline before R2010. Each file names the maintenance release AutoCAD 2021 writes for its
version (104 for R2004, 226 for R2010, 125 for R2013, 174 for R2018), and from R2010 its header and classes carry the long of 0
after their size; AutoCAD 2021 took all the memory it could get on an R2010 file that held a table, a multileader, a wipeout
or an image when they lacked them.

```csharp
DxfDatabase db = DxfDatabase.Open("plan.dxf");
DwgWriter.Write(db, "plan.dwg");                         // AC1032
DwgWriter.Write(db, "plan2010.dwg", "AC1024");           // AutoCAD 2010

DxfDocument shapes = DxfReader.Read("plan.dxf");
DwgWriter.Write(shapes, "shapes.dwg");                   // the shapes alone, each on its layer
```

The drawing is written as its DXF, and each record of that DXF is put into the object of the DWG that says the same,
with the handles of the drawing: the header variables, the classes, the tables, the blocks, the entities and the
objects, in the container of pages a DWG of AutoCAD 2004 and later has, compressed, with every check sum. The same
drawing gives the same bytes every time.

A DWG keeps an angle in radians and a DXF in degrees, so the writer turns one into the other, and the reader back. AutoCAD
2021 makes the radians of a degree text as the degrees divided by 57.29577951308232: of 1 400 angles typed into a DXF of ARC
arcs it gives the bits of that division for 1 207 (86 of 100), against 993 for `degrees * pi / 180`; the TEXT rotations
agree (545 of 600, against 451). The writer divides so, and the reader multiplies by 57.29577951308232, so that the radians
AutoCAD wrote come back as they were: of 470 angles in AutoCAD's own R2018 drawings that came back a bit off 422 now do, and
of the 236 ARC objects that were a bit off 63 still are. Those are radians that no double of degrees divides to, as a degree
is the coarser step there; they are one bit off in the last place, about 1E-16 radians. A degree text that AutoCAD turns
into another bit than its quotient (28 of 300 whole degrees, 195 among them) cannot be reproduced by any plain formula
tried (the product with `pi / 180`, a long double, the correctly rounded value).

AutoCAD itself keeps the radians when it does not convert: saving a DWG as a DWG copies the doubles, and only a degree text
is divided. The 63 ARC objects stay one bit off because the DXF between the reader and the writer holds degrees: of a million
radians drawn at random 88 914 (8.9 percent) have no double of degrees that divides to them, and none of those is reached
from a neighbour of the degrees the reader gives (the three doubles each way were tried). Only a DXF that carried the radians
would keep them, and a DXF does not.

A TEXT, ATTRIB or ATTDEF whose alignment point is (0, 0) has none in AutoCAD's DWG (bit 0x02 of its data flags is set, and the
reader gives (0, 0) back), where the writer gave the point as two bit doubles of 132 bits. The 40 TEXT and ATTDEF objects of the 13
`Dynamic Blocks` drawings that were one bit off AutoCAD's own are its bytes now (40 of 40; the ARC objects are the 63 above).

The writer's internal form of R2013 (`DwgWriter` writes R2018 alone) made files AutoCAD 2021 refused at once, `ErrorStatus` 53,
the simplest drawing of a few lines among them. AutoCAD's own R2013 file has a long of 0 after the size of its header and of its
classes, which the writer left out (as it does for R2010, which AutoCAD opens without it), and names the maintenance release 125
in its file header, where the writer named 0. Swapping the sections of an AutoCAD file for the writer's one at a time found the header
and the classes the two that decided it (the objects, their map and the rest were opened as they were); with the long the
drawing opens, whatever release the header names. The writer now gives both as AutoCAD does (the reader looks for the long where the
release is past 3), and the 36 drawings of the tests and of the samples written as R2013 open, with `AUDIT` finding 0 errors in
29 of them and in the others the 24, 24, 2, 2, 32, 32 and 10 of their originals.

The file of AutoCAD 2007 (AC1021) is a container of its own, the one `DwgContainer` reads in DwgContainer.R2007.cs, and the writer
(`DwgContainerWriter21`) makes it from the other end. A file header of 0x80 bytes (the version, the maintenance release 50, the
code page, and the place of the preview) is followed at 0x80 by the header proper of 0x400 bytes: 0x110 bytes that name the places
and CRCs of the page map and the section map, compressed, behind a CRC, a key and the CRC of the compressed data, and coded by
Reed-Solomon (255, 239) in three blocks laid one byte after the other; what AutoCAD leaves in the 259 bytes after them, its memory,
is written as zeros. Then come the pages from 0x480: the page map and its copy, the data pages of each section in turn, the section
map and its copy, and at the end a copy of the 0x400 bytes of the header. A map is compressed and repeated as often as its place
takes (the first copy is read); a data page of the objects, the header variables, the classes, the object map, the template, the
free space, the revision history or the auxiliary header (coding 4) is compressed where that makes it shorter, coded by
Reed-Solomon (255, 251) in blocks laid one byte after the other, and takes 255 bytes of every 251 of its data, to a multiple of 32;
the preview (coding 1) is stored as it is, with the parity of each of its blocks of 251 bytes after it. Both codes were found by
taking the blocks of AutoCAD's own files of R2007 and testing which field and which roots make them codewords: the header and the
maps use the field `x^8 + x^6 + x^5 + x^3 + 1` with the roots 2^239 to 2^254, the data pages `x^8 + x^4 + x^3 + x^2 + 1` with 2^251
to 2^254, and the 2 746 codings of the header, the maps and the data pages in 171 files of AutoCAD 2021 (its 167 SaveAs of the
samples and the tests, and 4 test drawings of R2007) are coded again by `DwgReedSolomon` to the byte. The compression is the variant of
LZ77 `DwgLz21` reads, written by a search of the longest copy of 3 bytes or more from up to 65 535 bytes back
(`DwgLz21.Compress`; the result is read back before it is used, and a page that does not get shorter is stored as it is, as
AutoCAD stores one).

AutoCAD 2021 refused the first files with `ErrorStatus` 434 (an unsupported file format) before it read a page, and each refusal
named a rule the format does not state: it reads the header proper only where its data is compressed (the stored form, a negative
length, is refused), reads the section map only where it is compressed, and wants the size a page names in the section map to be the
size of its data (a size of the page of its section is refused, though AutoCAD's own files hold other numbers there). It also checks
the parity of the pages it keeps unpacked (coding 1: the application information, its history and the summary information) against
parity made from the bytes its own memory held after the data, which no other program can write. The section map says how each
section is coded, and AutoCAD reads the application information as well where it is coded 4 (compressed, in blocks of the code of
251 laid one byte after the other, as the header is): AutoCAD's own application information put into the writer's container
unpacked, with the writer's parity, was refused, and coded 4 it opens with `AUDIT` finding 0 errors, so the writer gives the
application information that way, the bytes the later versions hold. The history of the applications and the summary information
are left out: a file made here has no earlier application to list, and the summary information is written into no version yet.
The preview is written only where the drawing has a picture. The objects are encoded as those of R2010 are, with the fields
R2010 added left out, and a drawing of R2010 or later is made into the DXF of R2007 first, as for R2004. A 3DSOLID, a REGION and a
BODY are written as in R2004 and R2010 (see the ACIS data below). The free-space section is the 53 bytes AutoCAD
2021 writes into a file of R2007 (numbers of 32 bits), and the auxiliary header names the version of the file (27, 50) between two
of the application (33, 174). The constraint groups of a dynamic block (ACDBASSOC2DCONSTRAINTGROUP, with the network, actions and
dependencies that go with them) need two extended records AutoCAD keeps, in the dictionary `ACAD_XREC_ROUNDTRIP` of the extension
dictionary of an entity, what R2007 has no place for: the identifiers of the vertices of a LWPOLYLINE (the XRECORD
`VERTEXIDENTIFIER`: the count of the whole numbers that follow, itself one, then each identifier, as bytes) and, on each dimension of a
constraint (the layer `*ADSK_CONSTRAINTS`, an association among its reactors), the mark `ISDYNAMIC` with a 1, the dimension being made
invisible and losing its text; AUDIT finds an error in each group without them (34 for the 5 groups of Architectural - Imperial, 0
with the groups left out). The DXF saved as R2007 from a later version is given both records as AutoCAD gives them (the 8 polylines
and 25 dimensions of Architectural - Metric, each record equal to AutoCAD's own R2007 file, and likewise the other 7 drawings of the
samples with a dynamic block), and the writer keeps the groups where the drawing holds either record: in the first files the groups were
left out where there was none, and with them the evaluation graph and the 60 dependencies of the Mechanical samples, which have no
vertex identifiers but 56 and 12 dimensions of constraints. A drawing that holds neither, as the text of R2010 given to the writer
as it is does, has its groups left out as before. The 167 drawings of AutoCAD 2021's samples and of the tests, each written as R2007
and opened in AutoCAD 2021 with `AUDIT`: all 167 open, 160 with 0 errors, the other 7 with the errors of their originals (24, 24, 2,
2, 32, 32 and 10), and the 4 tables that had 6 each have none; the files of R2000, R2013 and R2018 are the same bytes as before
(501 of 501).

The file of AutoCAD 2000 (AC1015) is not laid out in pages as the later versions are: a header of 0x61 bytes (the version, the
maintenance release 15, the place of the preview, the code page, six locators of a section's number, place and size, a check sum
of the header with the mask of six locators, and a sentinel), then the auxiliary header, the preview, the header variables, the
classes (no long of 0 after the size, no 8 bytes after the end sentinel), the objects, the object map with the places of the objects
as places in the file, and the template. AutoCAD 2021 writes no free-space section in it and neither does the writer. The objects are
those of R2004 in the layout before it (the colours of an entity as an index, the plot settings with the strings of R2000, no
count of the vertices or entities an owner holds but the first and the last), and the writer found six places where AutoCAD's own
R2000 object differs from what the later versions' code made, each by writing the DXF the reader makes of AutoCAD's files of R2000
(`curves`, `polys`, `text` and the other test drawings) and comparing object by object with AutoCAD's bytes: an entity holds the
links to the entity before and after it in its owner's list, left out where they are the handles one before and one after (a bit
of the entity says so: 0x78 is the first line of the model space of `curves`, with none before it and 0x79 after it, 0x79 and 0x7A
have none), a vertex is linked to the vertices around it and the SEQEND, which is in no list, has none; a table record holds the bit
that says it is referenced, set on every record AutoCAD writes; a dimension style holds the bit after its lineweights; a layout lists
the handles of its base UCS and its named UCS (null); an MTEXT holds the bit after its line spacing factor. The objects written so
equal AutoCAD's own bytes (the table records, the dimension style, the layouts, the vertices and the SEQEND, the LINE, CIRCLE,
ELLIPSE, SPLINE, RAY and XLINE, 2 of 3 MTEXT objects of `text`), but where AutoCAD kept an extrusion (0, 0, 1) in full on an
entity a probe had set it on (it is 6 bits longer: the ARC, SOLID, TRACE and TEXT of the test drawings) and the dictionaries that
list the objects the writer leaves out (the materials, the visual styles and the table styles are not written before R2010).
The 167 drawings of AutoCAD 2021's samples and of the tests, each written as R2000 and opened in AutoCAD 2021 with `AUDIT`: all 167
open, 160 with 0 errors, the other 7 with the errors of their originals (24, 24, 2, 2, 32, 32 and 10), and the 4 tables that had 6
each have none. A text beyond the code page is written as its UTF-8 bytes in R2000 as in R2004 (AutoCAD's own file keeps
`\U+1EBF` there), a fault of both that is left.

| Part | Written |
|---|---|
| Header | every header variable of the version; those a DXF does not give (a drawing of `DxfWriter` gives few) as AutoCAD 2021 gives a new drawing |
| Tables | VPORT, LTYPE, LAYER, STYLE, VIEW, UCS, APPID, DIMSTYLE, BLOCK_RECORD |
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs, of one line or of several, SOLID, TRACE, 3DFACE, SHAPE given by its number (see below), DIMENSION (every kind), VIEWPORT, HATCH, LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, ACAD_TABLE, REGION, 3DSOLID and BODY with their ACIS data (see below) |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MLEADERSTYLE, TABLESTYLE, DBCOLOR, IMAGEDEF, IMAGEDEF_REACTOR, RASTERVARIABLES, WIPEOUTVARIABLES, SPATIAL_FILTER (the clip of an INSERT, XCLIP), MATERIAL, VISUALSTYLE (of AutoCAD 2004 and later), the detail and section view styles, the manager of the persistent names of subentities, FIELD, FIELDLIST, SORTENTSTABLE and DATALINK, the data of a dynamic block (its graph and its parameters, grips and actions, the properties table, the user parameter, the proxy node and the network of variables and dependencies and the groups of constraints, see above), the TABLECONTENT and TABLEGEOMETRY a table keeps for a round trip, and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour (index, true colour, transparency, a colour of a book through its DBCOLOR), linetype scale, visibility, lineweight, material, plot style, owner, reactors, extension dictionary, extended data, proxy graphics |

A record of another type is left out and counted: one Warn line of `GeometryHelperLog` names the types with the count of
each (`DwgWriter: types that are not written yet were left out: SHAPE 1 …`). Every handle to a record
left out is written as 0, every dictionary entry naming one is dropped, and the vertices and attributes it owns go
with it. A record that has no handle (a DXF of AutoCAD 12 has none) cannot be named by the DWG and is left out the same
way, counted under its type. Left out so far: a REGION, 3DSOLID or BODY
of a SAT whose records the grammars do not place, and of R2000 one that is not ACIS 4.00 (see below), a SHAPE
given by its name, and the surfaces. A new drawing of AutoCAD 2018 is written whole: nothing of it is left out.

A SHAPE is written where its DXF gives its number: under group 70, as the reader gives a shape whose file of shapes it
does not find, or as the name, `2 130`. A name is not looked up, and a SHAPE given by its name alone, as AutoCAD's DXF
gives it, is left out. DXF does not say which text style a shape is of; the shape is written of the first style of the
table that is of a file of shapes (flag 1). A drawing of TRACK1 and BOX of `ltypeshp.shx` read without the file, so
that each gives its number, is written back with those numbers; AutoCAD opens it, `AUDIT` finds 0 errors, and its own
DXF names the four shapes TRACK1, TRACK1, TRACK1 and BOX again. A fifth shape, number 7 of a file that is nowhere, goes
to `ltypeshp.shx` too, the first style of shapes, where it had its own style in the file read.

A REGION the library made with `DxfUnknownEntity.CreateRegion` for a drawing of R2013 or R2018 is written the same way: twelve
regions of lines, arcs, circles, ellipses and holes, written as a DWG of R2018, open in AutoCAD 2021 with `AUDIT` finding
0 errors and the areas and perimeters of the shapes.

A REGION, 3DSOLID or BODY is written where the section ACDSDATA of its DXF holds its SAB data, as a DXF of AutoCAD 2013
and later gives it: the entity with the GUID of its data, and the data in the data store of the file, the section
AcDb:AcDsPrototype_1b, laid out as AutoCAD lays it out. The records go in the order of the section ACDSDATA, one data
segment holding them all; a record past 256 KB goes in pages of its own, of at most 1 048 496 bytes each; the search
segment lists the handles in their order, each with the place of its record. A box 30 by 20 by 10, a sphere, a
cylinder and a region of a circle are written as AutoCAD's own bits, entity for entity, and their store of 11 520 bytes
too, but the 12 bytes after the index of the segments that AutoCAD leaves as its memory held them; so is the store of
a plate 400 by 400 with 400 holes, its 515 873 bytes of SAB in a page of its own. AutoCAD opens both, `AUDIT` finds 0
errors, and its own DXF of each is the DXF it was written from, the SAB data byte for byte.

A solid of SAT text, as a DXF of AutoCAD 2004, 2007 and 2010 gives it, is turned into the SAB AutoCAD 2021 writes for
a DWG of R2018 (`DwgSat.ToSab`, the grammars of the reader turned round) and written the same way: its extended data
`ACAD_OBJECT_CHANGE_GUID` is the GUID the entity names its data by, taken out of the extended data, and a body whose
data has none gets a GUID made of its handle, so that the same drawing gives the same bytes. Eleven solids (a box, a
sphere, a cone, a cylinder, a torus, a wedge, a pyramid, a plate with a hole, a union, an extrusion of a polyline with
an arc and a revolve) and three regions (discs of radius 7, 9 and 4) of a DXF of each of the three versions are written
so; AutoCAD 2021 opens all of them, `AUDIT` finds 0 errors, and each solid has the volume, surface area and centroid
AutoCAD gives it from its own DWG of the drawing (the box 6 000 and 2 200, the sphere 2 144.66 and 804.25, the plate
13 721.42 and 6 000, a revolve of 270 degrees 2 968.81 and 1 585.94), to the last digit. The ACIS 4.00 text of AutoCAD
2000 is turned too (its records are those of R2004 less the asmheader, the history and the parameters of an edge: see the DXF
versions in the index of this guide), as the box and the region of AutoCAD's own R2000 are; the solids whose splines are another
curve in ACIS 4.00 (a loft through circles of different shapes, an extrusion of a closed spline) are left out.

A REGION, 3DSOLID or BODY of a DWG of R2004, R2007 or R2010 is written the same SAB into the entity itself, as those versions keep it
(no data store, which only R2013 and later have): the flag that says the data is not empty and another of 0, the version 2, the SAB
byte by byte as bits, the wireframe of a new solid (no wires, no silhouettes), from R2007 the count of materials of faces, 0, and of
a 3DSOLID the handle of its history. The SAB is that of the version (head 20800, 21200 or 21500; its data ends in `End-of-ACIS-data`,
where R2013 and later end in `End-of-ASM-data`), made of the SAT the DXF of the version gives, which the entity keeps as it is,
its extended data too. A box 30 by 20 by 10, a sphere, a cylinder and a region of a circle, as AutoCAD's own DWG of R2004, R2007
and R2010 holds them, are encoded again to the bit, entity by entity, but for the date the SAB carries (24 bytes in AutoCAD's, left
empty by the writer as it is in R2018); AutoCAD opens the files written so, `AUDIT` finds 0 errors, `MASSPROP` of the three solids
gives the volume 9 087.1384 in each of R2004, R2007, R2010 and R2013 as it does of the original, and of the region the area 153.9380
and the perimeter 43.9823. The spline solids of the tests (a closed spline extruded 7 up, and a solid lofted through a circle, an
ellipse and a circle), from the DXF of each version into the three, are written where the grammars place them, and have the volume
AutoCAD gives the original (1 654.1254, the two solids of R2004 and of R2007, and 1 654.1366 of R2010; the extrusion alone 588.1790
where its loft has no form in the version): AutoCAD's own lofted solid of R2007 has a parametric curve that ends in one flag, where
that of R2010 has two, and the flag is put in or taken out as the form changes (written alone, it was refused with a modeling error
75004); a solid whose spline has no form in the other version is left out as it is in R2018.

Two values of the SAB of a spline solid cannot be told from the SAT alone. The parameter curve of a face (`pcurve`) ends in
two reals, which the SAT writes `0 0` or `1 0` and which were taken for two whole numbers; and the surface of a loft
(`loft_spl_sur`) holds, in the binary form of R2013 and later, one whole number of 0 more than in the SAT, after the one that
follows its four flags (`F F F F 1 full nubs` in the SAT is `F F F F 1 0 full nubs` in the SAB, put in and taken out as the
form changes). A solid of a closed spline extruded 7 up and a solid lofted through a circle, an ellipse and a circle, of a
DXF of R2010, written as a DWG of R2018 with those two values wrong, were refused by AutoCAD 2021 (a modeling error, code
75005, ErrorStatus 53); written right, each SAB is the SAB AutoCAD 2021 saves of the same drawing, byte for byte but the date, and
opens with `AUDIT` finding 0 errors. The other way, AutoCAD's SAB of each is read back into the SAT of the DXF, line for
line. Lofts of circles, of closed splines and a closed loft, saved by AutoCAD in the same way, agree record by record.

The preview of a DWG is its picture in Explorer and in the Open dialog: where the DXF holds a section `THUMBNAILIMAGE` (a
bitmap of Windows without its file header, or a PNG file, as AutoCAD writes it) it is written as the preview, laid out
as AutoCAD 2021 lays it out: a header of 80 zero bytes (kind 1), then the picture (kind 2, a PNG kind 6), each placed
by its address in the file, in one page of its own at 0x120, the first of the file. AutoCAD gives a drawing so written
its picture back through `Database.ThumbnailBitmap`, pixel for pixel (a bitmap of 96 by 64), and `AUDIT` finds 0 errors.
Without a `THUMBNAILIMAGE` the preview is the empty one the writer always wrote, and the file is the same bytes.

A FIELD, a FIELDLIST, a SORTENTSTABLE and a DATALINK are written from the groups of their DXF, in the layout described
under [What is read](#what-is-read): a value of a field of a DXF of AutoCAD 2004 or before (the format apart under 4,
the values with no flags) is put into the form of R2007, as AutoCAD does when it saves such a drawing as 2018, the
flags of the values being 2 for one kept by name, 3 for one of the field that holds nothing and 4 for one that holds a
value; the values kept by name are written in the order the DXF gives them. The sort handles of a draw order table are
written in the data and its entities in the handles, the entries of an entity that is not written left out with their
sort handles, as are the entities and fields a handle names that is not written. The objects a data link fills are
written in the order of their handles, as AutoCAD's file keeps them, and bit 8 of its options is set where its data
map holds a value. The classes of the four are listed, with the proxy flags and the release AutoCAD gives them, where
the DXF lists none, as a drawing made in code does (a draw order made with `CreateDrawOrderTable` lists no class
SORTENTSTABLE). Against the DWG that AutoCAD 2021 saves, as R2010, R2013 and R2018, of nine of its sample drawings
(`Mechanical - Text and Tables`, `Mechanical - Data Links`, `Cover Sheet`, `A-01` and others), each such object is
written as AutoCAD's own bits but the last bit of a real of 16 places (the 1/96 of a field of a sheet set,
0.0104166666666667). AutoCAD opens the 13 drawings written from the DXF of eight of them and from a draw order made in
code, `AUDIT` finds 0 errors, and its own DXF of each has as many fields, field lists, draw order tables and data links
as the DXF it was written from, and so has the DXF of each table of fields and links of `Cover Sheet`, `Title Sheet`,
`T-01`, `Mechanical - Data Links` and `Data Extraction and Multileaders Sample` written again (the 60 fields a table of
`Cover Sheet` owns, which the table, left out, took with it, are there).

The data of a dynamic block is written, from AutoCAD 2007, from the groups of its DXF in the layouts of the reader: an
expression that holds a real or a short keeps it, one of another kind is written with no value, which AutoCAD works out
again; the purge preventer names the block that owns the dictionary it stands in; the unset value of a grip location, which
DXF gives as `1.797693134862314E+99`, is written as the largest real, which is what AutoCAD keeps (the number the text
names is another, and AutoCAD's DXF of the DWG gave it back as `1.797693134862313E+99`). A properties table (with its grip), a user parameter, the proxy node of the graph and the network of variables and
dependencies are written in the layouts of the reader, so that a block of linear parameters with a properties table made in
code keeps its graph, its table and its grip. A graph that names an expression the writer does not write (a constraint
parameter whose dependency is not written, because the group that owns it is not) is left out with the expressions it owns,
and so is a network that lists an action not written and what the network owns, counted with the types left out, so that
no graph is half of one. All 90 graphs of the sample drawings are written to R2018 (72 were before the constraint group of
version 2 was written: the other 18 lean on one). AutoCAD
takes the anonymous block of a dynamic block that no INSERT
places (`*U6` of the sheet `A-01`, true name `Drawing Title`, three attribute definitions) for an unused one where it
finds no graph, and drops it from the drawing it opens, with the six fields and the draw order table of the attribute
definitions: 6 of the 2 646 fields and 1 of the 256 draw order tables of the 120 sample drawings in each of the sheets
`A-01`, `A-02`, `A-04`, `A-05`, `AS-01`, `S-01`, `S-02` and `S-03` (48 fields and 8 tables). Written with the graph, the
drawing opens with `AUDIT` 0, and AutoCAD's DXF of it has all 2 646 fields and 256 draw order tables, the 12 to 37
objects of the dynamic block of each of the nine sheets as the DXF of the original, group for group, and the ATTDEF,
CIRCLE and INSERT of the block's anonymous copies and their object context data as many as in the original.

The 72 graphs of the sample drawings that are written have 1 492 objects, and AutoCAD's DXF of the DWG holds each as the
DXF of the original does, group for group; 15 stretch actions list their entities in another order, AutoCAD's own. The
drawings open with `AUDIT` as the originals do (113 with 0 errors, 7 with the 24, 24, 2, 2, 32, 32 and 10 of their
originals), and so do the 71 DWGs of the tests. A panel made in code for each kind of parameter and action (a visibility
parameter, a flip, a rotation, an XY, a polar stretch, a move, a scale, an array, a lookup, an alignment and a base
point with a point parameter) is written, opens with `AUDIT` 0 and comes back from AutoCAD's DXF as it was sent.

The 18 graphs that lean on a constraint group are written as well, so that all 90 are. Each object of them, read from the
R2018 DWG AutoCAD saves of the 13 `Dynamic Blocks` drawings and written again, has the very bits of the object AutoCAD saved
(the 34 groups, their networks, variables, dependencies and dependency bodies, but for the class number and the order of the
actions of a group), and two entities of the constraint blocks had to be written as AutoCAD writes them for the file to open with
`AUDIT` as the original does. A 2D
polyline keeps the id of each vertex (group 91, after its point; the constraints name the edges of the block by them): written
without, the DWG of `Architectural - Metric` had 8 polylines AUDIT did not accept. A dimensional constraint has no block of its
picture, and AutoCAD's DXF gives it with neither the block (2) nor a type (70) where that is 0 (a radius with its text moved
has 132, not 164, which is 4 and 128 without the 32 every other dimension has): its flags have the bit of the block clear, and
written with it set each of the 25 of that DWG was an error (34 with the polylines and one more). The 167 DWGs of AutoCAD's 96
sample drawings and the tests, rewritten to R2018, open with `AUDIT` as their originals do (160 with 0 errors where the
originals have 156, the other 7 the 24, 24, 2, 2, 32, 32 and 10 of theirs; the 4 `table` drawings of the tests had 6 each), and
AutoCAD's DXF of each rewrite holds as many of every type of object of a dynamic block as the original's: 90 evaluation
graphs of 90, 34 groups of 34, 330 value dependencies, 414 on geometry, 280 others, 302 variables, 42 networks, 180 bodies. The
two exceptions are the anonymous blocks AutoCAD drops as unused (`Structural Base1` of `S-02` and one of `S-04`) that were
dropped before. A DWG of AutoCAD 2013 is read the same way as one of 2018: the edge names of the dependencies (a flag and a
number of a short) were read as texts there, and the 43 objects of its sample that lean on them were left out.

The dynamic block of a DWG of R2000 or R2004 is read the same way. The 26 files AutoCAD 2021 saves of the 13 `Dynamic Blocks`
drawings as R2000 and R2004 read as many objects of every type as AutoCAD's DXF of each has; before, a lookup action was left
out of them as broken, and with it the lookup parameter, the linear parameter, the grips, the stretch and the graph that lean on
it (14 objects of a panel made in code with one lookup, and 21 of the 391 of Architectural - Metric), because the table's
values, texts of the data before R2007, were looked for in a string stream that those versions have not.

A constraint group of the version 2 (the DXF of a drawing AutoCAD 2018 saved) is written to R2010 and R2007 in the layout of the
version 1, as AutoCAD 2021 does when it saves such a drawing in those versions (its DXF of R2010 holds the smallest group of
Architectural - Metric in 423 groups where its DXF of R2018 holds 430): the action at version 1 without its four numbers, the two
numbers and the flag after the next identifier left out, and the nodes by identifier, each with its class as a text, its
identifier, its byte and its identifiers, and what its class holds, a circle and an arc with three reals where the version 2
has four (the fourth is 0 in every sample, and is dropped). The group was left out of those versions, with the network that listed
it, and so were the evaluation graph and the dependencies of the block. The 8 drawings of the `Dynamic Blocks` samples that hold
groups (34 in all), saved by AutoCAD as R2018 and written as R2010 and as R2007 (the records of the DXF of R2007 as above), open in
AutoCAD with `AUDIT` finding the errors of AutoCAD's own R2010 file of each (none in the Architectural, Mechanical - Imperial and
Structural drawings, 2 in each Civil, 10 in Mechanical - Metric, 24 and 32 in the Annotation and Electrical drawings, which hold no
group), and AutoCAD's DXF of each of the 16 files has every group of the R2018 file's DXF group for group and every other object of
the dynamic block but for the order of the entities of a stretch action (1 to 12 in a drawing, AutoCAD's own order). The data
stream of each of the 34 groups equals the data stream of the group in AutoCAD's own DWG of R2010 of the same drawing, bit for bit,
and so does its handle stream, the actions the group owns in the order of the file (written in ascending handles, which no file of
AutoCAD's has, the handle stream of all 34 differed). The 180
drawings (the 167 of the corpus and the 13 R2018 saves) written as each of the six versions are the same bytes as before but for
the 16 files of R2007 and R2010 that hold groups and the 8 of R2000 that hold bodies (1 056 of 1 080).

The DXF save of R2010 and R2007 (`db.Save(path, "AC1024")` and `"AC1021"`, and every DWG, which reads the same text) takes the group
down to that layout too, the converter doing what the writer did: the actions of the network, the variables and the groups at
version 1 without their four numbers more, and the nodes of a group by identifier with their class, their group (the handle of
the group), their identifier and byte, the identifiers they name and what their class holds, a circle and an arc with three reals.
A group whose node is of a class the library does not know (`AcConcentricConstraint`, `AcConstrainedEllipse`, `AcConstrainedBoundedEllipse`,
which a constraint of two curves concentric and an ellipse give) is left in the layout it has, which AutoCAD does not read in a
file of R2010 (`ACDBASSOC2DCONSTRAINTGROUP ... Premature end of object`, the drawing discarded: the DXF saves of the 8 samples
with groups were refused so before, and open as AutoCAD's own DXF of R2010 does now, with `AUDIT` finding 0, 2, 10 and 0 errors in
Architectural - Metric, Civil - Imperial, Mechanical - Metric and Structural - Imperial, in R2010 and in R2007). Of the 3 204 dynamic
objects of the 13 drawings, each in R2010 and in R2007, every one equals AutoCAD's DXF of the R2018 file saved in that version, group
for group and in order, but one: a group of Architectural - Metric whose actions AutoCAD lists in another order in its DXF of R2010
than in that of R2018 (its DXF of R2007 lists them as that of R2018 does). The circle and the arc of the group were set up in AutoCAD in 3 drawings of 7 curves each (circles, arcs
turned the other way and one through the zero angle, a circle with a thickness; fixed, tangent, concentric, of equal radius
and solved): the fourth real of the second layout stayed 0 in each of the 21 and in the 34 groups of the samples, and an ellipse
holds its ratio, 0.5 and 0.4, in a place of its own, so nothing was found where the first layout keeps the fourth real.

A REGION, 3DSOLID or BODY of a DWG of R2000 is written with its SAT, the text of ACIS 4.00, in the entity, as AutoCAD 2000's file
keeps it: the flag of not empty, a bit AutoCAD 2000 sets to 1 where the later versions leave it at 0, the version 1, the text in
blocks of at most 4 096 bytes, each with its size ahead of it and a 0 after the last, and the wireframe of a new solid, with no count
of materials of faces and no handle of a history. Each line of the DXF is a line of the text as the groups hold it (each
character but a space 0x9F less it, a caret held alone where DXF writes it with a space after), followed by a carriage return and a
line feed, a line that starts with a space starting with a tab as the reader gives a tab. AutoCAD's own DWG of R2000 of a box 30 by
20 by 10 and a region, and of two spline solids (the second with 9 836 bytes of text in blocks of 4 096, 4 096 and 1 644), are
read and written again to the very bytes of each entity (4 of 4). The box, the sphere, the cylinder and the region (`acis`) written
from the DXF of R2004, R2010, R2013 and R2018, the box with its region (`solids`) and the plate of 400 holes (`acisbig`, 515 873
bytes of SAB) written to R2000 open in AutoCAD with `AUDIT` finding 0 errors, and `MASSPROP` gives the
volume AutoCAD gives the original and its own SaveAs to R2000 (9 087.1384 for the three solids, area 153.9380 and perimeter 43.9823
for the region; 6 000 and 314.1593; 1 398 938.0702 for the plate). A body whose ACIS the DXF versions cannot place as 4.00 (the two
spline solids of each of the DXF of R2004, R2007 and R2010) is left out of R2000 as before: R2000 holds that version alone.

A 2D polyline of R2010 and later puts the id of a vertex (group 91) between its bulge and its tangent direction (group
50). It was written after the tangent and not read, so a polyline AutoCAD had curve-fitted (a vertex with a tangent of
90 degrees) was refused by AutoCAD when written (ErrorStatus 53, `Existing Conditions Plan.dwg`) and the tangent of
one AutoCAD wrote was read as 0. An attribute of a DXF of R2007 has one group 280, the lock of its position, where a
DXF of R2010 and later has the version (0) before its tag as well; the lock was written as the version, and an
attribute with its position locked was refused in the same way (`Mechanical - Multileaders.dwg` and two more of the
sample drawings). Each of AutoCAD 2021's 113 sample drawings, read and written again, opens in AutoCAD: 106 with `AUDIT`
finding 0 errors and 7 with as many as the original has (`Annotation`, `Civil` and `Electrical` in Imperial and Metric
and `Mechanical - Metric`, where attributes name an object the file does not hold: 24, 24, 2, 2, 32, 32 and 10); none
is refused, where 8 were (5 by the vertices, 3 by the attributes).

A MATERIAL and a VISUALSTYLE are written from the groups their DXF gives, a group not given taking a new material's
value. Of AutoCAD's new drawing the 24 visual styles and the three materials are AutoCAD's own bits but the last bit of
the matrices of the materials, 1/48 written in DXF to 16 places; so are a material of colours of its own and one of a
picture in every map. AutoCAD opens the drawing of these (accoreconsole), `AUDIT` finds 0 errors, and its own DXF of it
is the DXF it was written from.

A VISUALSTYLE of a DXF of AutoCAD 2004, 2007 or 2010 keeps 28 properties in groups of their own (71, 72, 73, 90, 40 and
on, each followed by 176, how it is applied, from AutoCAD 2010), and the 30 that AutoCAD 2013 added in a record of the
extension dictionary of the style, `ACAD_XREC_ROUNDTRIP`: `RTVSPost2010Prop28` to `57` with a value in the group of its
kind and `RTVSPost2010PropOp28` to `57` (in a style of AutoCAD 2004 and 2007 also `RTVSPropertyOp0` to `27`, how the 28
are applied). It is written as AutoCAD 2021 writes the style when it saves the drawing as R2018: the 58 properties in
one list (`70 58`), the 28 in the order of the file and the 30 of the record, and the dictionary and its record left
out of the file, taken into the style, with the stamp `AcDbSavedByObjectVersion` of its extended data, which AutoCAD
brings up to its own version. A style of AutoCAD 2004 and 2007 gives the opacity and the specular gloss it does not apply
(40 and 41) as negative numbers, written positive. The 26 visual styles of each of five sample drawings of AutoCAD 2021,
as the DXF of R2004, R2007 and R2010 that AutoCAD wrote of them (15 drawings, 390 styles), are AutoCAD's own bits object
for object; so are the 25 of a new drawing in a DXF of AutoCAD 2010, and 24 of them in one of AutoCAD 2004 and 2007,
where the style Realistic keeps its lighting model as 2 where AutoCAD's R2018 has 3, which the DXF cannot give back. The
26 styles of a DXF of AutoCAD 2000 come with the index of their colours but not the true colour, which that DXF does not
give. AutoCAD opens the six drawings written from the DXF of PB-EX61 in R2000, R2004, R2007 and R2010 and of two other
samples in one version each, `AUDIT` finds 0 errors, and its own DXF of the first four has the visual styles of its own
DXF of the R2018 file, but for the true colour of the one written from R2000.

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
works out the places of the cells' words again as it opens the file. A cell style of the style's own that names no text style
(one made in code without it) is written with Standard, as a row is, which AUDIT cannot repair without it.

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
written from. `AUDIT` finds 0 errors in each. The drawing of tables is so since the text style of its rows: the table
style of its second table was made in code with no text style in its data, title and header rows, AutoCAD's own file of
the drawing holds it so, and `AUDIT` of that finds 6 errors (`AcDbTableStyle was not repaired`, three in each of two
passes), as it does of AutoCAD's own save of it. Such a row is written with the text style Standard, as the styles
AutoCAD makes itself have, and `AUDIT` finds 0; a row that names a style keeps it.

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
