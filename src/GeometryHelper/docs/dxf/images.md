# Images, wipeouts and OLE objects

Pictures in a drawing: an IMAGE shows a picture file by its path, a WIPEOUT masks what lies under it, and an OLE2FRAME
holds a picture (or a document) inside the drawing itself.

| Entity | Class | AutoCAD's class | The picture is |
|---|---|---|---|
| IMAGE | [`DxfRasterImage`](xref:GeometryHelper.Dxf.DxfRasterImage) | `RasterImage` | a file outside the drawing, named by a [`DxfRasterImageDef`](xref:GeometryHelper.Dxf.DxfRasterImageDef) |
| WIPEOUT | [`DxfWipeout`](xref:GeometryHelper.Dxf.DxfWipeout) | `Wipeout` | none: it hides what is under it |
| OLE2FRAME | [`DxfOle2Frame`](xref:GeometryHelper.Dxf.DxfOle2Frame) | `Ole2Frame` | inside the drawing, as Windows' OLE saved it |

## Images and wipeouts

### At a glance

```csharp
DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);   // ACAD_IMAGE_DICT, made where the drawing has none
var picture = new DxfRasterImageDef(@"C:\pictures\px43.png", new GeoVector2(4, 3)) { ResolutionUnits = DxfUnit.Centimeter };
images.SetAt(DxfRasterImageDef.SuggestName(images, picture.SourceFileName), picture);   // "px43"
var image = new DxfRasterImage(picture);                     // one unit a pixel at the origin: (4, 0, 0) by (0, 3, 0)
image.SetOrientation(new GeoPoint3(10, 20, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
db.ModelSpace.AppendEntity(image);                           // given its IMAGEDEF_REACTOR; picture.GetEntityCount(out _) is 1
var mask = new DxfWipeout();
mask.SetFrom(new[] { new GeoPoint2(-30, -10), new GeoPoint2(10, -10), new GeoPoint2(10, 20), new GeoPoint2(-30, 20), new GeoPoint2(-30, -10) }, new GeoVector3(0, 0, 1));
db.ModelSpace.AppendEntity(mask);                            // the frame of side 40 at (-30, -10)
```

The library reads no pixel. It places, moves and clips an image as its record says, and keeps the path of the picture as
the file writes it. The drawing's settings for images are a [`DxfRasterVariables`](xref:GeometryHelper.Dxf.DxfRasterVariables) (the entry ACAD_IMAGE_VARS of the
named object dictionary); an image's definition is `image.ImageDef`, under its name in
`DxfRasterImageDef.GetImageDictionary(db)`.

### Where an image stands

A picture of 4 by 3 pixels placed at (10, 20) with a pixel 10 across and 10 up gives:

| Member | Gives |
|---|---|
| `GetOrientation` | the corner (10, 20, 0) and the sides (40, 0, 0) and (0, 30, 0) |
| `Width`, `Height` | 40 and 30 |
| `ImageWidth`, `ImageHeight` | 4 and 3, its pixels |
| `Position` | (50, 50, 0), the far corner, as AutoCAD 2021 gives it |
| `PixelToModelTransform` | pixels to the world: the pixel (0, 0) to (15, 45, 0) |

`SetOrientation` places it anew, the sides divided by the pixels along them, square or not.

### Clipping

The clip boundary is in pixels, counted from the centre of the top left pixel, y down. AutoCAD writes a rectangle as two
corners, and a polygon with its first corner again at the end.

An image is clipped where `IsClipped` is on (flag 4 of its display options, which AutoCAD sets on every new image) and a
boundary was set (280 of 1). `GetVertices()`, `GetGeometry()` (a closed [`GeoPolyline3`](xref:GeometryHelper.Geometry.GeoPolyline3)), `GeometricExtents` and
`Explode()` then follow the boundary, and the frame otherwise, as AutoCAD 2021 does.

- Clipped by the triangle of the pixels (0, 0), (3, 0), (1.5, 2.5), the picture above shows the triangle (15, 45, 0),
  (45, 45, 0), (30, 20, 0); its box is (15, 20, 0) to (45, 45, 0).
- Clipped inverted (`IsClipInverted`, group 290, read and set from R2010), it shows what lies outside the triangle. Its
  vertices and lines are still the triangle's, and its box is the frame's, (10, 20, 0) to (50, 50, 0), as AutoCAD 2021
  gives them.
- `SetClipBoundary` takes a rectangle as its five corners, the first again at the end, and writes the first and the
  third. It takes a polygon of three corners or more closed by its first: its last corner within 0.001 of the first in
  the world, whatever the size of a pixel, as AutoCAD 2021 takes and writes them.
- `SetClipBoundaryToWholeImage` clips by nothing (280 of 0).

### Wipeouts

A WIPEOUT has no picture: its brightness, contrast, fade and display options are refused, as AutoCAD 2021 refuses or
ignores them. `SetFrom` frames a polygon as AutoCAD 2021 does, in the square on the box of the corners and the origin of
their plane: the rectangle (-30, -10) to (10, 20) in the square of side 40 at (-30, -10), and a square 10 by 10 at
(100 000, 100 000) in the square of side 100 010 at the origin. A wipeout another writer left with flag 4 of 70 off, or
280 of 0, shows its whole frame, the origin of its plane among it where `SetFrom` put the frame there, as AutoCAD 2021
gives it.

### Moving and copying

- `TransformBy` takes any transformation, as AutoCAD 2021 does: scaled 2 along x alone, the picture is 80 by 30; sheared,
  its sides lean; mirrored, it is seen from behind. `GetTransformedCopy` of a wipeout refuses an uneven scale and a
  shear, as AutoCAD answers eCannotScaleNonUniformly.
- Appended or moved, an image grows the extents of the header by its box, as any entity does.
- A copy appended to its own drawing shows the same IMAGEDEF, with no reactor of its own, as AutoCAD's `Clone`
  appended has.
- A copy appended to another drawing brings its IMAGEDEF under its name, the dictionary of images made where that
  drawing has none, once for any number of images, as AutoCAD's `WblockCloneObjects` brings it. The CLASS rows of IMAGE
  and IMAGEDEF, or of WIPEOUT, are listed where the drawing lists none, each counted 1, and a row of the class written
  counted 0 is counted 1: AutoCAD 2021 ignores the images and wipeouts of a drawing that lists no class of them, and
  passes over those of a class counted 0, as ezdxf 1.4.5 counts its own.
- An inverted clip is left out of a copy into a drawing before R2010, and taken off there by `IsClipInverted = false`.
- An image erased takes its IMAGEDEF_REACTOR with it and leaves the IMAGEDEF. A drawing of R12 takes neither IMAGE nor
  WIPEOUT.

### Making them in code

They are made as AutoCAD 2021's API makes them, as the example above shows.

- **The definition** is given its path and its size in pixels, which AutoCAD reads from the file and the library
  cannot. It is added to the dictionary of images under the name `SuggestName` gives: the file's name without its
  extension, then with `_1` once that is taken. It is written as AutoCAD 2021 writes a new one: loaded (280 of 1), a
  pixel (1, 1) in no unit until set. A definition added under a name the dictionary has is refused, as AutoCAD 2021's
  `SetAt` erases the one there and leaves its images out of the file it writes.
- **The drawing** is given its variables of images (ACAD_IMAGE_VARS: frames shown, pictures finely, no unit) and of
  wipeouts (ACAD_WIPEOUT_VARS: frames shown) where it has none, as AutoCAD gives a drawing it opens, and the CLASS rows of
  each class written, counted.
- **The image** is written as AutoCAD 2021 writes one given its definition: shown and clipped by the whole picture (70 of
  7, 280 of 0), its brightness and contrast 50. Appended to a block of the definition's drawing (model space, paper space
  or a block placed by INSERTs), it is given its IMAGEDEF_REACTOR, as AutoCAD's `AssociateRasterDef` gives one, owned by
  the image and listed by the definition. `GetEntityCount` counts the images of a definition as AutoCAD 2021 counts them:
  those of a reactor; a copy, which AutoCAD's `Clone` leaves with none, is not among them.
- `AssociateRasterDef` makes an image show another definition of its drawing, its sides kept and its reactor moved. A
  copy of an image, given by `AssociateRasterDef` a definition of the drawing it is to go into, shows that one there,
  with a reactor of its own. `Name` set renames the definition's entry, as AutoCAD 2021 renames it.
- A definition erased takes the images that show it with it, as AutoCAD 2021 takes those associated with it.
- Refused: an image of a definition of another drawing; an IMAGE before R14 and a WIPEOUT before R2000; a wipeout made in
  code and never given its polygon, as AutoCAD 2021 writes one of no boundary and then cannot open the drawing. A copy of
  a wipeout read is refused only where it has no boundary, a polygon of fewer than three corners or a rectangle of fewer
  than two.

AutoCAD 2021 opened every drawing so made, of R2000, R2010 and R2018, new or of its own or ezdxf's, with AUDIT finding no
error; read every image where the library says; gave each definition the count the library gives; and saved them again as
DXF of R2000, R2010 and R2018, which the library reads the same. Making 10 000 wipeouts takes 0.53 s.

### Details and limits

- **A polygon not closed to the bit.** A polygon another writer left not ending on its first corner to the bit, where
  its count (91) matches its corners, is read closed, and written closed wherever the library writes it anew: in a copy
  (the first corner again at the end and the count one more) and by `AssociateRasterDef`. AutoCAD 2021 reads a wipeout of
  four corners so left as of none and shows nothing, and cannot open a drawing that holds such an image, even one whose
  last corner is a millionth of a pixel off its first. The copies so closed it opens with AUDIT finding no error, and
  reads where the library says.
- A copied image or wipeout whose count of corners (91) does not match the corners its record holds is written as it is,
  its polygon not closed, as the library cannot tell which corners AutoCAD would take.
- A drawing only read and saved is written as it was.
- [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader) reads no IMAGE and no WIPEOUT.
- Not here, of AutoCAD's classes: the members that load, show or change the picture's pixels (`RasterImageDef.Load`,
  `Unload`, `OpenImage`, `ImageCopy`, `SetImage`, `Embed`, `ColorDepth`, `FileType`, `Organization` and the rest), which
  need the file read; `RasterImage.Scale`, which AutoCAD works out from the picture loaded; and `Wipeout.HasFrame`, which
  AutoCAD 2021 gave true for every wipeout whatever its drawing said.

## OLE objects

### At a glance

```csharp
// Take the pictures out of a drawing.
foreach (DxfOle2Frame frame in db.ModelSpace.OfType<DxfOle2Frame>())
{
    byte[] picture = frame.GetPictureData(out DxfOlePictureFormat format);   // a .bmp, .wmf or .emf; null where none
    if (format == DxfOlePictureFormat.Dib) File.WriteAllBytes(frame.Handle + ".bmp", picture);
    GeoAabb3 box = frame.GeometricExtents;                                   // (10, 20, 0) to (40, 40, 0)
}

// Embed a picture, then place it.
var logo = new DxfOle2Frame(File.ReadAllBytes("logo.png"));
db.ModelSpace.AppendEntity(logo);                       // given the marks of model space
logo.Position3d = new DxfRectangle3d(new GeoPoint3(0, 50, 0), new GeoPoint3(120, 50, 0),
                                     new GeoPoint3(0, 0, 0), new GeoPoint3(120, 0, 0));
```

An OLE2FRAME is a picture pasted, a sheet of Excel or a file embedded, or a document linked, shown in a frame. The object
is the frame's binary (its groups 310): AutoCAD's head of 128 bytes, which holds the four corners of the frame in world
coordinates, then the compound file Windows' OLE saved the object in. AutoCAD 2021 reads the frame from those corners, not
from groups 10 and 11, which are copies of two of them. The library does the same, so a file another writer moved by 10
and 11 alone shows the frame where AutoCAD shows it; the difference is said once in the log at Debug.

### Where a frame stands

A picture of 3 by 2 pixels pasted with its lower left corner at (10, 20, 0) and stretched to 30 by 20 gives:

| Member | Gives |
|---|---|
| `Location` | (10, 20, 0), its lower left corner, as AutoCAD 2021 gives it (the DXF reference has the upper left of group 10) |
| `Position3d` | its four corners as a [`DxfRectangle3d`](xref:GeometryHelper.Dxf.DxfRectangle3d), as they are held: turned, sheared or out of the plane xy |
| `WcsWidth`, `WcsHeight` | 30 and 20: the run of its top along x and of its left along y, so the frame turned 30 degrees is 25.98 wide, as AutoCAD 2021 works them out |
| `ScaleWidth`, `ScaleHeight` | its size in percent of the size AutoCAD keeps for it at 100 percent (codes 41 and 42 of the extended data): 100 as pasted, 50 once scaled by a half |
| `GeometricExtents` | the box of the corners, (10, 20, 0) to (40, 40, 0) |
| `GetGeometry()` | the closed `GeoPolyline3` of the corners from the upper left, which `IntersectWith` crosses |
| `Explode()` | four LINEs along its edges, as AutoCAD 2021 gives four |
| `Rotation`, `LockAspect`, `AutoOutputQuality` | read from the extended data of ACAD between OLEBEGIN and OLEEND (codes 40 in radians, 71 and 70); 0, true and 1 where there is none, as AutoCAD 2021 reads one |
| `OutputQuality` | group 73 |

### What the object is

It comes from the compound file, read by the library's own reader of MS-CFB.

| Member | Gives |
|---|---|
| `Type` | `Static` for a bitmap, a metafile or an enhanced metafile pasted; `Link` for a link; `Embedded` for any other, as MFC's `COleClientItem::GetType` finds it |
| `UserType` | the name of a static picture's class as Windows names it (`Picture (Device Independent Bitmap)`), and of another the name its `\1CompObj` stream holds (`OLE Package`); AutoCAD asks the registry of its machine |
| `LinkName`, `LinkPath` | the source of a link, the display name of its moniker and its file (`C:\Drawings\cost.xlsx!Sheet1!R1C1:R4C3` and `C:\Drawings\cost.xlsx`); empty for an object not linked |
| `GetCompoundDocument()` | the library's own: the compound file as written, which Windows opens as a file of structured storage |
| `GetPictureData(out format)` | the library's own: the picture of a static object as the bytes of its file, a .bmp (`DxfOlePictureFormat.Dib`, 78 bytes for the picture of 3 by 2 pixels of 24 bits), a placeable .wmf or an .emf; null for an object embedded or linked, which AutoCAD draws through its application |

### Moving and sizing

AutoCAD 2021 keeps a frame upright: its ROTATE leaves a picture along x and y, its MIRROR puts it on the other side not
turned over, and its SCALE scales the corners and keeps the size at 100 percent. The library moves a frame the same way.

```csharp
frame.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 0, 0)));   // moved, as AutoCAD moves it
frame.WcsWidth = 60;                 // 60 wide from its upper left corner, as high in measure where LockAspect
frame.ScaleWidth = 50;               // half the width AutoCAD keeps at 100 percent
frame.Position3d = new DxfRectangle3d(new GeoPoint3(10, 60, 0), new GeoPoint3(70, 60, 0),
                                      new GeoPoint3(10, 20, 0), new GeoPoint3(70, 20, 0));
frame.OutputQuality = 0;             // plotted monochrome (group 73)
```

- `TransformBy` takes a move, a scale by one factor and a mirror about a line along x or y. The corners are written into
  the head of the binary, which AutoCAD reads, and into groups 10 and 11; only the lines of binary the head is in are
  written anew. Mirrored or turned half round, the frame is the box of its corners so moved. Scaled, the size of its text
  (code 43) is scaled with it.
- The picture 30 by 20 at (10, 20) scaled by a half about (10, 20) is 15 by 10 there, its `ScaleWidth` 50; mirrored about
  x = 40, it stands from (40, 20) to (70, 40).
- Refused with `ArgumentException`, nothing written: a turn, a shear, a stretch along one axis or a move out of the plane
  xy. An INSERT exploded whose placing would do one of those leaves the frame out; the actions of a dynamic block refuse
  a frame.
- `Position3d` takes a rectangle along x and y only. `WcsWidth`, `WcsHeight`, `ScaleWidth` and `ScaleHeight` keep the
  upper left corner, as AutoCAD 2021's do, and size the other side in the same measure where `LockAspect` is true.
- `LockAspect` and `OutputQuality` (0, 1 or 2) are written where AutoCAD keeps them, the extended data AutoCAD writes for
  a frame made where a record has none. `Rotation` and `AutoOutputQuality` are read only: AutoCAD 2021 turns no frame,
  and its setter of the second also changes the quality.
- A frame is copied as it is, by `Clone()` into its own drawing or another, or into a block, its binary and extended data
  byte for byte, and given the marks AutoCAD 2021 writes for the space it is copied into: the head's signature 0x5580
  and its u32 at 102 of 1 in model space, 0x5581 and 0 in paper space, and group 72 of 0 and 1.

### Embedding a picture

`new DxfOle2Frame(bytes)` makes a static picture of a .bmp or a .png, as AutoCAD 2021 makes one of a picture pasted,
which its .NET API cannot: the bitmap as the stream CONTENTS of a compound file of the class StaticDib, as Windows' OLE
saves it, behind AutoCAD's head and with the extended data AutoCAD writes.

- A PNG of any kind (grey, true colour or a palette, with or without alpha, 1 to 16 bits a sample, interlaced or not, of
  up to 64 million pixels) is read into a bitmap of 32 bits a pixel, its alpha laid over white. A JPEG or another kind is
  refused; turn it into one of these first.
- The frame stands with its lower left corner at the origin, at the picture's natural size in millimetres: its pixels
  over its pixels a metre (3 780, 96 dots an inch, where it gives none). 610 by 224 pixels of 96 dots an inch are 161.38
  by 59.26.
- The same picture gives the same bytes each time.

### Details and limits

- **A binary damaged** is read as far as it holds, and no getter throws: the corners from the head where it holds them,
  else those of a frame along x and y from 10 to 11; the kind and the user type from groups 71 and 3 where the compound
  file cannot be read or holds no user type, and no picture, which is said in the log at Debug. AutoCAD 2021 empties a
  frame whose compound file it cannot read when it opens the drawing, its corners and user type gone, and says nothing.
- **A frame of no corners at all**, as accoreconsole writes one it could not read (70, 73 and its extended data alone),
  stands at the origin with no width, as AutoCAD 2021 gives it, has no extents (`GeometricExtents` throws
  `InvalidOperationException`, and `UpdateExt` passes it over) and explodes into nothing.
- `Rotation` is the number stored, a NaN another writer stored given as it is; AutoCAD 2021 writes 0 for the NaN it holds
  in memory after a quarter turn.
- A drawing only read and saved is written as it was.
- Not given: `OleObject`, the object of the running application, and `Position2d`, the frame in the pixels of the window
  it is shown in; a file holds neither.
- The OLEFRAME of R13, an object of OLE 1 that AutoCAD's .NET API has no class for, stays a
  [`DxfUnknownEntity`](xref:GeometryHelper.Dxf.DxfUnknownEntity). `DxfReader` reads no OLE2FRAME.

## See also

- [AutoCAD and GeometryHelper](autocad.md): the AutoCAD patterns of the model side by side.
- [Editing rules and what the model does not do](rules.md): how every class moves and explodes.
- [The DXF checks against AutoCAD](../dxf-autocad-checks.md): what AutoCAD 2021 did with the drawings the library made.
