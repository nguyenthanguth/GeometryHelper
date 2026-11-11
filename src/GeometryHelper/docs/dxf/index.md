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
a REGION, a 3DSOLID or an ACAD_PROXY_ENTITY among them, is a [`DxfUnknownEntity`](xref:GeometryHelper.Dxf.DxfUnknownEntity): kept as the file has it, its look editable,
itself erasable and copied, its geometry not moved.

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
