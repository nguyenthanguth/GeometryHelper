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

DWG files of version AC1015 are read: the format of AutoCAD 2000, 2000i and 2002, and what every later AutoCAD writes
when a drawing is saved as "AutoCAD 2000/LT2000 Drawing". A DWG of another version, such as AC1018 (AutoCAD 2004) or
AC1032 (AutoCAD 2018), is refused with a `NotSupportedException` whose message names the version and the release; the
later formats are not read yet.

## What is read

The file is turned, in memory, into the DXF that says the same, record for record and with the handles of the file,
and that DXF is read. Compared with the DXF AutoCAD itself writes of the same DWG, the records are the same group for
group, numbers to nine places.

| Part | Read |
|---|---|
| Header | the header variables of AutoCAD 2000, `$DWGCODEPAGE` and `$MEASUREMENT` among them |
| Classes | every class of the file |
| Tables | VPORT, LTYPE, LAYER, STYLE, VIEW, UCS, APPID, DIMSTYLE, BLOCK_RECORD |
| Blocks | every block with its entities; the layouts as `*Paper_Space`, `*Paper_Space0` and on |
| Entities | LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh, polyface mesh) with its vertices, TEXT, MTEXT, ATTDEF, INSERT, alone or as an array, with its ATTRIBs, SOLID, TRACE, 3DFACE, DIMENSION (linear, aligned, angular by two lines or three points, radius, diameter, ordinate) |
| Objects | DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE, and the small objects a new drawing holds (ACDBDICTIONARYWDFLT, DICTIONARYVAR, ACDBPLACEHOLDER, SCALE) |
| Of each | layer, linetype, colour, linetype scale, visibility, lineweight, owner, reactors, extension dictionary and extended data |

Texts are in the drawing's code page, as in a DXF of AutoCAD 2000, with letters beyond it as `\U+XXXX`; they come
back as they were typed, "Tiếng Việt café 日本語" letter for letter.

## What is not read yet

A type that is not read is left out, with what it owns, and counted: one Debug line of
[`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) names the types with the count of each
(`DwgReader: types that are not read yet were left out: HATCH 1 LEADER 1 MATERIAL 3 …`), and one Info line sums the
call.

- Entities: VIEWPORT (so a layout comes without its viewports), HATCH, LEADER, TOLERANCE, MLINE, SHAPE, REGION,
  3DSOLID, BODY, the OLE frames, images and wipeouts, proxies, and every entity of a class the list above does not name.
- Objects: MATERIAL, VISUALSTYLE, TABLESTYLE, MLEADERSTYLE, CELLSTYLEMAP, the detail and section view styles, and the
  other objects of classes. AutoCAD makes these anew in a drawing that lacks them.
- A true colour does not exist in the format of AutoCAD 2000. AutoCAD keeps one as round-trip data: extended data of
  the application `ACAD` that starts with `RTT` on an entity, and an extension dictionary holding `ACAD_XREC_ROUNDTRIP`
  on a layer. That data is passed on as the file has it; it is not turned into the entity's true colour, so the entity
  shows the indexed colour AutoCAD chose as the nearest.
- `$TDCREATE` and `$TDUPDATE` are not given: the file holds the two times as universal time, and they are given as
  `$TDUCREATE` and `$TDUUPDATE`.
- The polygon mesh, the polyface mesh and the INSERT as an array are read as the format describes them, but have not
  been compared with AutoCAD's DXF as the rest has.
- The preview image of the file is skipped.

The order of the OBJECTS section is the root dictionary first and then by handle, not AutoCAD's; nothing reads that
order.

## Broken files

Every check sum of the file is checked.

| The file | What happens |
|---|---|
| is not a DWG: a DXF, any other data | `FormatException`: "The data is not a DWG file…" |
| is a DWG of another version | `NotSupportedException` naming the version |
| has a wrong check sum in the file header, the header variables, the classes or the object map, or is cut short there | `FormatException` naming the part |
| has one object with a wrong check sum, or one that cannot be decoded | that object is left out and the others are read; one Warn line gives the count and the first fault |

Where an entity is left out, those after it in its block are still read. No broken file makes the reader hang: 10 500
copies of the test drawings with bits changed at random, most inside objects under a repaired check sum, were each
read or refused in under 0.14 s.
