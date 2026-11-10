# Reading DWG files

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
[`DxfReadOptions`](xref:GeometryHelper.Dxf.DxfReadOptions) of `DxfReader`. A drawing opened from a DWG is saved as a
DXF: the library does not write DWG files.

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
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs (of several lines too), SOLID, TRACE, 3DFACE, DIMENSION (linear, aligned, angular by two lines or three points, radius, diameter, ordinate), VIEWPORT, HATCH (every boundary kind, pattern, solid fill, gradient), LEADER, MULTILEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, REGION, 3DSOLID and BODY (see below), ACAD_TABLE to AutoCAD 2007 |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, MLEADERSTYLE, IMAGEDEF, IMAGEDEF_REACTOR, DBCOLOR, TABLESTYLE to AutoCAD 2007, and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour (from AutoCAD 2004 with its true colour and transparency, and a colour of a book with its name), linetype scale, visibility, lineweight, owner, reactors, extension dictionary, extended data, and the proxy graphics where AutoCAD saved them |

A layout's viewports come numbered (group 69) as AutoCAD numbers them, which the file does not say: from AutoCAD 2004
the layout lists them, the one showing the paper first; in an AutoCAD 2000 file the viewport whose view is the paper
itself is number 1 and the others follow in the order they were made. Their frozen layers come under group 341 in a
DXF of AutoCAD 2000 and 331 later, and `DxfViewport.GetFrozenLayers` reads both.

A REGION, 3DSOLID or BODY carries its ACIS data as DXF does. An AutoCAD 2000 file keeps it as SAT text, which comes
out line for line. From AutoCAD 2013 the data is in a store of the file that DXF writes in a section of its own
(ACDSDATA), which is not written yet: the entity comes with the GUID of its record but without its shape. AutoCAD 2004
to 2010 keep it as SAB, binary ACIS, where DXF wants SAT text; those entities are not read.

Texts come back as they were typed, "Tiếng Việt café 日本語" letter for letter: before AutoCAD 2007 they are in the
drawing's code page, with letters beyond it as `\U+XXXX`, and from AutoCAD 2007 they are UTF-16 in the file and UTF-8 in
the DXF, as AutoCAD's DXF of those versions is.

A true colour of a table record (a layer, a viewport's ambient light) is given with the nearest colour index of the
palette, as AutoCAD writes it; the file holds no index beside it. A layer of RGB 10, 200, 30 gets index 102. The
palette is AutoCAD 2021's (see [`DxfColor`](xref:GeometryHelper.Dxf.DxfColor)).

## What is not read yet

A type that is not read is left out, with what it owns, and counted: one Debug line of
[`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) names the types with the count of each
(`DwgReader: types that are not read yet were left out: MATERIAL 3 VISUALSTYLE 24 …`), and one Info line sums the
call.

- Entities: ACAD_TABLE from AutoCAD 2010 (its content is a TABLECONTENT of its own, which DXF writes in the form of
  AutoCAD 2007), SHAPE (DXF names the shape, and only the SHX file of its style gives the name of its number), REGION,
  3DSOLID and BODY of AutoCAD 2004 to 2010 (see above), the OLE frames, proxies, and every entity of a class the list
  above does not name.
- Objects: MATERIAL, VISUALSTYLE, TABLESTYLE from AutoCAD 2010, CELLSTYLEMAP, TABLECONTENT, TABLEGEOMETRY, DATATABLE,
  RASTERVARIABLES, WIPEOUTVARIABLES, the detail and section view styles, the data of dynamic blocks, and the other
  objects of classes. AutoCAD makes most of these anew in a drawing that lacks them.
- A true colour does not exist in the format of AutoCAD 2000. AutoCAD keeps one as round-trip data: extended data of
  the application `ACAD` that starts with `RTT` on an entity, and an extension dictionary holding `ACAD_XREC_ROUNDTRIP`
  on a layer. That data is passed on as the file has it; it is not turned into the entity's true colour, so the entity
  shows the indexed colour AutoCAD chose as the nearest.
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
