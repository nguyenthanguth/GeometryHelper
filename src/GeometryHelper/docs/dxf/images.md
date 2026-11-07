# Images, wipeouts and OLE objects

## Images and wipeouts

An IMAGE is a `DxfRasterImage` and a WIPEOUT a `DxfWipeout`, as AutoCAD's `RasterImage` and `Wipeout`; the IMAGEDEF an
image shows is a `DxfRasterImageDef` (`image.ImageDef`), under its name in the dictionary of images
(`DxfRasterImageDef.GetImageDictionary(db)`), and the drawing's RASTERVARIABLES a `DxfRasterVariables` (the entry
ACAD_IMAGE_VARS of the named object dictionary). No pixel is read: the library places, moves and clips the image as its
record says, and the path of the picture is kept as the file writes it.

A picture of 4 by 3 pixels placed at (10, 20) with a pixel 10 across and 10 up gives `GetOrientation` the corner (10, 20,
0) and the sides (40, 0, 0) and (0, 30, 0); `Width` 40, `Height` 30, `ImageWidth` 4, `ImageHeight` 3; and
`Position` (50, 50, 0), the far corner, as AutoCAD 2021 gives it. `SetOrientation` places it anew, the sides divided by
the pixels along them, square or not. Its clip boundary is in pixels counted from the centre of the top left one, y down
(`PixelToModelTransform` takes them to the world, the pixel (0, 0) to (15, 45, 0)): AutoCAD writes a rectangle as two
corners and a polygon with its first corner again at the end. The image is clipped where `IsClipped` (flag 4 of its
display options, which AutoCAD sets on every new image) is on and a boundary was set (280 of 1): `GetVertices()`,
`GetGeometry()` (a closed `GeoPolyline3`), `GeometricExtents` and `Explode()` follow the boundary then, and the frame
otherwise, as AutoCAD 2021 does. Clipped by the triangle of the pixels (0, 0), (3, 0), (1.5, 2.5), the picture shows the
triangle (15, 45, 0), (45, 45, 0), (30, 20, 0), its box (15, 20, 0) to (45, 45, 0); clipped inverted (`IsClipInverted`),
showing what lies outside it, its vertices and lines are still the triangle's and its box the frame's, (10, 20, 0) to (50,
50, 0), as AutoCAD 2021 gives them. A wipeout another writer left with flag 4 of 70 off, or 280 of 0, shows its whole
frame, the origin of its plane among it where `SetFrom` put the frame there, as AutoCAD 2021 gives it. `SetClipBoundary` takes a rectangle
as its five corners, the first again at the end, and writes the first and the third, and a polygon of three corners or
more closed by its first, its last corner within 0.001 of the first in the world whatever the size of a pixel, as AutoCAD
2021 takes and writes them; `SetClipBoundaryToWholeImage` clips by nothing (280 of
0). `IsClipInverted` (290) is read and set from R2010. A WIPEOUT has no picture: its brightness, contrast, fade and display
options are refused, as AutoCAD 2021 refuses or ignores them, and `SetFrom` frames a polygon as AutoCAD 2021 does, in
the square on the box of the corners and the origin of their plane: the rectangle (-30, -10) to (10, 20) in the square of
side 40 at (-30, -10), and a square 10 by 10 at (100 000, 100 000) in the square of side 100 010 at the origin. An inverted
clip is left out of a copy into a drawing before R2010, and taken off there by `IsClipInverted = false`.

`TransformBy` takes any transformation, as AutoCAD 2021 does: scaled 2 along x alone, the picture is 80 by 30; sheared,
its sides lean; mirrored, it is seen from behind. Appended or moved, an image grows the extents of the header by its box,
as any entity does. `GetTransformedCopy` of a wipeout refuses an uneven scale and a shear,
as AutoCAD answers eCannotScaleNonUniformly. A copy appended to its own drawing shows the same IMAGEDEF, with no reactor of
its own, as AutoCAD's `Clone` appended has; appended to another drawing it brings its IMAGEDEF under its name, the
dictionary of images made where that drawing has none, once for any number of images, as AutoCAD's
`WblockCloneObjects` brings it; and the CLASS rows of IMAGE and IMAGEDEF, or of WIPEOUT, are listed where the drawing
lists none, each counted 1, and a row of the class of what was written counted 0 is counted 1, as AutoCAD 2021 ignores the
images and wipeouts of a drawing that lists no class of them and passes over those of a class counted 0, as ezdxf 1.4.5
counts its own. An image erased takes its IMAGEDEF_REACTOR with it and leaves the IMAGEDEF. A drawing of
R12 takes neither. `DxfReader` reads no IMAGE and no WIPEOUT, as before.

They are made in code as AutoCAD 2021's API makes them. A definition is given its path and its size in pixels, which
AutoCAD reads from the file and the library cannot, and added to the dictionary of images under the name `SuggestName`
gives, the file's name without its extension, then with `_1` once that is taken:

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

The definition is written as AutoCAD 2021 writes a new one, loaded (280 of 1), a pixel (1, 1) in no unit until set; the
drawing is given its variables of images (ACAD_IMAGE_VARS: frames shown, pictures finely, no unit) and of wipeouts
(ACAD_WIPEOUT_VARS: frames shown) where it has none, as AutoCAD gives a drawing it opens, and the CLASS rows of each
class written, counted. The image is written as AutoCAD 2021 writes one given its definition: shown and clipped by the
whole picture (70 of 7, 280 of 0), its brightness and contrast 50; appended to a block of the definition's drawing,
model space, paper space or a block placed by INSERTs, it is given its IMAGEDEF_REACTOR, as AutoCAD's
`AssociateRasterDef` gives one, owned by the image and listed by the definition, and `GetEntityCount` counts it as
AutoCAD 2021 counts the images of a definition: those of a reactor, a copy, which AutoCAD's `Clone` leaves with none,
not among them. `AssociateRasterDef` makes an image show another definition of its drawing, its sides kept and its
reactor moved; `Name` set renames the definition's entry, as AutoCAD 2021 renames it. A definition erased takes the
images that show it with it, as AutoCAD 2021 takes those associated with it; an image erased its reactor. A copy of an
image, given by `AssociateRasterDef` a definition of the drawing it is to go into, shows that one there, with a reactor
of its own. A wipeout made in code and never given its polygon is refused, as AutoCAD 2021 writes one of no boundary and
then cannot open the drawing; a copy of one read is refused only where it has no boundary, a polygon of fewer than three
corners or a rectangle of fewer than two. A polygon another writer left not ending on its first corner to the bit, where
its count (91) matches its corners, read closed, is written closed wherever the library writes it anew: in a copy, the
first corner again at the end and the count one more, and by `AssociateRasterDef`. AutoCAD 2021 reads a wipeout of four
corners so left as of none and shows nothing, and cannot open a drawing that holds such an image, even one whose last
corner is a millionth of a pixel off its first; the copies so closed it opens with AUDIT finding no error and reads
where the library says. A drawing only read and saved is written as it was. A definition added under a name the
dictionary has is refused, as AutoCAD 2021's `SetAt` erases the one there and leaves its images out of the file it
writes: add it under the name `SuggestName` gives. An image of a definition of another drawing is refused, and an IMAGE
before R14, a WIPEOUT before R2000. AutoCAD 2021 opened every drawing so made, of R2000, R2010 and R2018, new or of its
own or ezdxf's, with AUDIT finding no error, read every image where the library says, gave each definition the count the
library gives, and saved them again as DXF of R2000, R2010 and R2018, which the library reads the same. Making 10 000
wipeouts takes 0.53 s.

What AutoCAD's classes have that is not here: the members that load, show or change the picture's pixels
(`RasterImageDef.Load`, `Unload`, `OpenImage`, `ImageCopy`, `SetImage`, `Embed`, `ColorDepth`, `FileType`,
`Organization` and the rest), which need the file read; `RasterImage.Scale`, which AutoCAD works out from the picture
loaded; and `Wipeout.HasFrame`, which AutoCAD 2021 gave true for every wipeout whatever its drawing said.

Known limits: a copied image or wipeout whose count of corners (91) does not match the corners its record holds is written
as it is, its polygon not closed, as the library cannot tell which corners AutoCAD would take.

## OLE objects

An OLE2FRAME is a `DxfOle2Frame`, as AutoCAD's `Ole2Frame`: a picture pasted, a sheet of Excel or a file embedded, or a
document linked, shown in a frame. The object is the frame's binary (its groups 310): AutoCAD's head of 128 bytes, which
holds the four corners of the frame in world coordinates, then the compound file Windows' OLE saved the object in.
AutoCAD 2021 reads the frame from those corners and not from groups 10 and 11, which are copies of two of them; the
library does the same, so that a file another writer moved by 10 and 11 alone shows the frame where AutoCAD shows it, the
difference said once in the log at Debug.

A picture of 3 by 2 pixels pasted with its lower left corner at (10, 20, 0) and stretched to 30 by 20 gives `Location`
(10, 20, 0), its lower left corner, as AutoCAD 2021 gives it, where the DXF reference has the upper left of group 10;
`Position3d` its four corners as a `DxfRectangle3d`, as they are held, turned, sheared or out of the plane xy; `WcsWidth`
30 and `WcsHeight` 20, the run of its top along x and of its left along y, so that the frame turned 30 degrees is 25.98
wide, as AutoCAD 2021 works them out; `ScaleWidth` and `ScaleHeight` its size in percent of the size AutoCAD keeps for
it at 100 percent (codes 41 and 42 of the extended data), 100 for the picture as pasted and 50 once scaled by a half;
`GeometricExtents` the box of the corners, (10, 20, 0) to (40, 40, 0); `GetGeometry()` the closed `GeoPolyline3` of the
corners from the upper left, which `IntersectWith` crosses; and `Explode()` four LINEs along its edges, as AutoCAD 2021
gives four. `Rotation` (in radians), `LockAspect` and `AutoOutputQuality` are read from the extended data AutoCAD keeps
beside the binary (of ACAD, between OLEBEGIN and OLEEND: its codes 40, 71 and 70), and `OutputQuality` from group 73; a
frame with no such data reads 0, true and 1, as AutoCAD 2021 reads one.

What the object is comes from its compound file, read by the library's own reader of MS-CFB. `Type` is `Static` for a
bitmap, a metafile or an enhanced metafile pasted, `Link` for a link and `Embedded` for any other, as MFC's
`COleClientItem::GetType` finds it; `UserType` is the name of a static picture's class as Windows names it (`Picture
(Device Independent Bitmap)`), and of another the name its `\1CompObj` stream holds (`OLE Package`), where AutoCAD asks
the registry of its machine; `LinkName` and `LinkPath` are the source of a link, the display name of its moniker and its
file (`C:\Drawings\cost.xlsx!Sheet1!R1C1:R4C3` and `C:\Drawings\cost.xlsx`), empty for an object not linked. Two members
are the library's own: `GetCompoundDocument()` gives the compound file as written, which Windows opens as a file of
structured storage, and `GetPictureData(out format)` the picture of a static object as the bytes of its file, a .bmp
(`DxfOlePictureFormat.Dib`, 78 bytes for the picture of 3 by 2 pixels of 24 bits), a placeable .wmf or an .emf; null for
an object embedded or linked, which AutoCAD draws through its application.

```csharp
foreach (DxfOle2Frame frame in db.ModelSpace.OfType<DxfOle2Frame>())
{
    byte[] picture = frame.GetPictureData(out DxfOlePictureFormat format);   // a .bmp, .wmf or .emf; null where none
    if (format == DxfOlePictureFormat.Dib) File.WriteAllBytes(frame.Handle + ".bmp", picture);
    GeoAabb3 box = frame.GeometricExtents;                                   // (10, 20, 0) to (40, 40, 0)
}
```

A binary damaged is read as far as it holds, and no getter throws: the corners from the head where it holds them, else
those of a frame along x and y from 10 to 11; the kind and the user type from groups 71 and 3 where the compound file
cannot be read, or holds no user type, and no picture, which is said in the log at Debug. A frame of no corners at all,
as accoreconsole writes one it could not read (70, 73 and its extended data alone), stands at the origin with no width,
as AutoCAD 2021 gives it, has no extents (`GeometricExtents` throws `InvalidOperationException`, and `UpdateExt` passes
it over) and explodes into nothing. AutoCAD 2021 empties a frame whose compound file it cannot read when it opens the
drawing, its corners and user type gone, and says nothing. `Rotation` is the number stored, a NaN another writer
stored given as it is; AutoCAD 2021 writes 0 for the NaN it holds in memory after a quarter turn.

A frame is copied as it is, by `Clone()` into its own drawing or another, or into a block, its binary and extended data
byte for byte, and given the marks AutoCAD 2021 writes for the space it is copied into: the head's signature 0x5580
and its u32 at 102 of 1 in model space, 0x5581 and 0 in paper space, and group 72 of 0 and 1. A drawing only read and
saved is written as it was.

AutoCAD 2021 keeps a frame upright: its ROTATE leaves a picture along x and y, its MIRROR puts it on the other side not
turned over, its SCALE scales the corners and keeps the size at 100 percent. `TransformBy` moves a frame so: a move, a
scale by one factor and a mirror about a line along x or y are taken, the corners written into the head of the binary,
which AutoCAD reads, and into groups 10 and 11, only the lines of binary the head is in written anew; mirrored or turned
half round, the frame is the box of its corners so moved; scaled, the size of its text (code 43) is scaled with it. A
turn, a shear, a stretch along one axis or a move out of the plane xy is refused with `ArgumentException`, nothing
written, and so is an INSERT exploded whose placing would do one of those, which leaves the frame out; the actions of a
dynamic block refuse a frame. The picture 30 by 20 at (10, 20) scaled by a half about (10, 20) is 15 by 10 there, its
`ScaleWidth` 50; mirrored about x = 40 it stands from (40, 20) to (70, 40).

```csharp
frame.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 0, 0)));   // moved, as AutoCAD moves it
frame.WcsWidth = 60;                 // 60 wide from its upper left corner, as high in measure where LockAspect
frame.ScaleWidth = 50;               // half the width AutoCAD keeps at 100 percent
frame.Position3d = new DxfRectangle3d(new GeoPoint3(10, 60, 0), new GeoPoint3(70, 60, 0),
                                      new GeoPoint3(10, 20, 0), new GeoPoint3(70, 20, 0));
frame.OutputQuality = 0;             // plotted monochrome (group 73)
```

`Position3d` takes a rectangle along x and y only; `WcsWidth`, `WcsHeight`, `ScaleWidth` and `ScaleHeight` keep the
upper left corner, as AutoCAD 2021's do, and size the other side in the same measure where `LockAspect` is true;
`LockAspect` and `OutputQuality` (0, 1 or 2) are written where AutoCAD keeps them, the extended data AutoCAD writes for a
frame made where a record has none. `Rotation` and `AutoOutputQuality` are read only: AutoCAD 2021 turns no frame, and
its setter of the second also changes the quality.

`new DxfOle2Frame(bytes)` makes a static picture of a .bmp or a .png, as AutoCAD 2021 makes one of a picture pasted,
which its .NET API cannot: the bitmap as the stream CONTENTS of a compound file of the class StaticDib, as Windows' OLE
saves it, behind AutoCAD's head and with the extended data AutoCAD writes. A PNG of any kind (grey, true colour or a
palette, with or without alpha, 1 to 16 bits a sample, interlaced or not, of up to 64 million pixels) is read into a
bitmap of 32 bits a pixel, its alpha laid over white; a JPEG or another kind is refused, the caller turning it into one
of these first. The frame stands with its lower left corner at the origin at the picture's natural size in millimetres,
its pixels over its pixels a metre (3 780, 96 dots an inch, where it gives none): 610 by 224 pixels of 96 dots an inch
are 161.38 by 59.26. The same picture gives the same bytes each time.

```csharp
var logo = new DxfOle2Frame(File.ReadAllBytes("logo.png"));
db.ModelSpace.AppendEntity(logo);                       // given the marks of model space
logo.Position3d = new DxfRectangle3d(new GeoPoint3(0, 50, 0), new GeoPoint3(120, 50, 0),
                                     new GeoPoint3(0, 0, 0), new GeoPoint3(120, 0, 0));
```

`OleObject`, the
object of the running application, and `Position2d`, the frame in the pixels of the window it is shown in, are not given:
a file holds neither. The OLEFRAME of R13, an object of OLE 1 that AutoCAD's .NET API has no class for, stays a
`DxfUnknownEntity`. `DxfReader` reads no OLE2FRAME, as before.
