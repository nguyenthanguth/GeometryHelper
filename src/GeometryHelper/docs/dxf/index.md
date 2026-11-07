# DXF files

The library has three ways into a DXF file, each for its own job:

| To | Use | What it gives |
|---|---|---|
| draw the library's shapes for CAD to open | `DxfWriter` | a new file of R2000 in millimetres, each shape the entity CAD has for it, on the layers named (see [Writing DXF](reader.md#writing-dxf)) |
| read the geometry of a drawing | `DxfReader`, which gives a `DxfDocument` | the shapes of the plane or of space with their layer and look, every INSERT placed, read only (see [Reading DXF](reader.md#reading-dxf)) |
| change a drawing and save it with nothing else lost | `DxfDatabase` | the drawing as AutoCAD's `Database` holds it, its classes AutoCAD's with Dxf in front (see [Editing a DXF file](#editing-a-dxf-file)) |

`DxfReader` reads a whole drawing into shapes at once, 100 000 entities of eight kinds (34 MB) in 0.54 s on .NET 10, and is the
way to take geometry out of a drawing to work on. `DxfDatabase` reads what it is asked for when it is asked: 100 000 lines of
10.5 MB are opened and saved in 36 ms and walked as `DxfLine` objects in 21 ms, and what is not changed is written back as
it was read. `DxfWriter` makes a file from nothing. The three meet: an entity of the editing model gives the shapes
`DxfReader` would read for it by `GetGeometry()`, and a drawing begins as the text `DxfWriter` writes (see
[The model](#the-model)).

## Editing a DXF file

### The model

`DxfDatabase` opens a DXF file to edit it as AutoCAD's `Database` edits a drawing, and saves it again with nothing lost. Its
classes are AutoCAD's with Dxf in front, so that a plugin can use both without a clash of names: `DxfBlockTableRecord`,
`DxfLayerTableRecord`, `DxfLine`, `DxfMText`, each with AutoCAD's members and their meanings, points in world coordinates
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

The objects are AutoCAD's classes in AutoCAD's order. A `DxfDBObject` is any object of the drawing, a `DxfEntity` one that
is drawn, and a `DxfCurve` an entity walked along; the tables are a `DxfSymbolTable` of `DxfSymbolTableRecord`s; the
dictionaries, records of data and groups of the OBJECTS section are a `DxfDBDictionary`, a `DxfXrecord` and a `DxfGroup`;
a layout is a `DxfLayout`, whose plot settings are a `DxfPlotSettings`. A type of entity the library has no class for yet,
a REGION, a 3DSOLID or an ACAD_PROXY_ENTITY among them, is a `DxfUnknownEntity`: kept as the file has it, its look editable,
itself erasable and copied, its geometry not moved.

A drawing begins as a file, or new, of a version named: `new DxfDatabase("AC1024")` is a drawing of R2010 with nothing in
it, the drawing `DxfWriter` writes with no shape, in millimetres, with model space and paper space, two layouts, the layer
0, the text style and the dimension style Standard and the named object dictionary, its header naming AC1024 and, after it,
the code page ANSI_1252 (`$DWGCODEPAGE`), as AutoCAD's own empty drawings of each of these versions name it; entities,
blocks and layers are added to it as to any other. The versions are AC1015 (R2000), AC1018, AC1021, AC1024, AC1027 and
AC1032 (R2018); `new DxfDatabase("AC1015")` is the very text of `DxfDatabase.Parse(new DxfWriter().ToString())` but for that
code page, and a version of R12 to R14, or one written otherwise, is refused (`ArgumentException`). Text of R2007 or later
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

For a reader who knows AutoCAD's .NET API, what each daily pattern is here. The classes not named keep AutoCAD's name with
Dxf in front and their members' names.

| AutoCAD | GeometryHelper | The difference |
|---|---|---|
| `new Database(false, true); db.DxfIn(path, log)` | `DxfDatabase.Open(path)`, `Open(stream)`, `Parse(text)` | A DXF file only, ASCII, R12 to R2018; no DWG and no binary DXF. |
| `new Database(true, true)` | `new DxfDatabase("AC1024")` | The version is named, AC1015 (R2000) to AC1032 (R2018); the drawing is in millimetres. |
| `db.DxfOut(path, 16, version)`, `db.SaveAs(path, version)` | `db.Save(path)`, `Save(stream)`, `ToText()` | Saved in the version, code page and style it was read in; a drawing is not turned into another version. An entity appended from a drawing of another version is turned into this one's forms. |
| `HostApplicationServices.WorkingDatabase`, `Document.LockDocument()` | none | No application and no document: the drawing is the object `Open` gave. |
| `using (Transaction tr = db.TransactionManager.StartTransaction()) { ...; tr.Commit(); }` | none | Every change is written at once; nothing is rolled back. |
| `(Line)tr.GetObject(id, OpenMode.ForWrite)` | the object itself | An object is reached through what holds it (`db.ModelSpace`, a table, `entity.Owner`) and is the same object each time; no `UpgradeOpen`. |
| `ObjectId`, `id.Handle`, `db.GetObjectId(false, handle, 0)` | the object; `obj.Handle`, a text such as `"2F"`; `db.GetObject(handle)`, `db.TryGetObject(handle, out value)` | Entities (VERTEX and ATTRIB among them), objects of the OBJECTS section, and the layer, linetype, text style, dimension style and block tables and their records are found; APPID, VPORT, VIEW and UCS records and a block's BLOCK and ENDBLK, which have no class, are not. A handle of no object throws `KeyNotFoundException` from `GetObject`, as AutoCAD answers eUnknownHandle. A handle of an erased object throws it too, at once, where AutoCAD's `GetObjectId` still gives the erased object's id and fails only when it is opened (eWasErased); `TryGetObject` gives false for both. |
| `ObjectIdCollection`, `DBObjectCollection` | `IReadOnlyList<DxfEntity>` and the like | `Explode()`, `GetOffsetCurves`, `GetSplitCurves`, `GetBlockReferences` give lists of objects. |
| `var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead)` | `db.BlockTable` | Also `db.LayerTable`, `LinetypeTable`, `TextStyleTable`, `DimStyleTable`. |
| `bt[BlockTableRecord.ModelSpace]` opened for write | `db.ModelSpace`, `db.PaperSpace`, `db.BlockTable["BOLT"]` | Named `*Model_Space` and `*Paper_Space` whatever the file calls them. |
| `btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true)` | `db.ModelSpace.AppendEntity(line)` | One call; the entity is given a handle and an owner, and its layer is added to the layer table where it is not listed. |
| `foreach (ObjectId id in btr)` | `foreach (DxfEntity e in btr)` | The entities as objects, walked over those the block holds when the walk begins. |
| `entity.Erase()` | `entity.Erase()` | Takes every record of it out; no `Erase(false)` to bring it back. |
| `new Line(p1, p2)`, `new Circle(c, n, r)`, `new Arc(...)`, `new Ellipse(...)`, `new DBPoint(p)` | `new DxfLine(p1, p2)`, `new DxfCircle(c, n, r)`, `new DxfArc(...)`, `new DxfEllipse(...)`, `new DxfDBPoint(p)` | Points are `GeoPoint3`, directions `GeoVector3`. |
| `Polyline`, `Polyline2d`, `Polyline3d`, `PolyFaceMesh`, `PolygonMesh`, `Spline`, `Ray`, `Xline`, `Face`, `Solid` | `DxfPolyline`, `DxfPolyline2d`, `DxfPolyline3d`, `DxfPolyFaceMesh`, `DxfPolygonMesh`, `DxfSpline`, `DxfRay`, `DxfXline`, `DxfFace`, `DxfSolid` | Their vertices are `DxfVertex2d`, `DxfPolylineVertex3d`, `DxfPolyFaceMeshVertex`, `DxfFaceRecord` and `DxfPolygonMeshVertex`. |
| `DBText`, `MText` | `DxfDBText`, `DxfMText` | No fonts: a text's `GeometricExtents` is its insertion point, and `AdjustAlignment` is not there. `TextStyleId` is `TextStyle`, the record, beside `TextStyleName`. |
| `BlockReference`, `MInsertBlock`, `AttributeReference`, `AttributeDefinition` | `DxfBlockReference`, `DxfMInsertBlock`, `DxfAttributeReference`, `DxfAttributeDefinition` | `new DxfBlockReference(position, block)` takes the block record, not its id; `AttributeCollection.AppendAttribute` takes the attribute itself. |
| `RasterImage`, `Wipeout`, `RasterImageDef`, `RasterVariables` | `DxfRasterImage`, `DxfWipeout`, `DxfRasterImageDef`, `DxfRasterVariables` | Read, moved, clipped and copied; no pixel is read. `Orientation` is `GetOrientation` and `SetOrientation`, an origin and two sides of their lengths, where `GeoCoordinateSystem3` would make them unit and square. `ImageDefId` is `ImageDef`, the object. See [Images and wipeouts](images.md#images-and-wipeouts). |
| `Ole2Frame`, `Rectangle3d`, `Ole2Frame.ItemType` | `DxfOle2Frame`, `DxfRectangle3d`, `DxfOle2FrameItemType` | Read, copied, moved and sized as AutoCAD 2021 does, upright; a static picture made of a .bmp or a .png by `new DxfOle2Frame(bytes)`, which AutoCAD's .NET API cannot. The frame is read from and written to the corners in its binary, as AutoCAD reads it; `GetCompoundDocument()` and `GetPictureData(out format)` are the library's own. `OleObject` and `Position2d`, of the running application and its window, are not there. See [OLE objects](images.md#ole-objects). |
| `Hatch`, `Leader`, `MLeader` | `DxfHatch`, `DxfLeader`, `DxfMLeader` | A loop is a `DxfHatchLoop` of the library's shapes; a line of `GetHatchLinesData` is a `GeoLine2`; `EvaluateLeader` is not there. |
| `RotatedDimension`, `AlignedDimension`, `RadialDimension`, `DiametricDimension`, `LineAngularDimension2`, `Point3AngularDimension`, `ArcDimension`, `OrdinateDimension`, `RadialDimensionLarge` | the same with Dxf in front | `RecomputeDimensionBlock` is not there: the block that draws a dimension is left for AutoCAD to draw anew. |
| `Table` | `DxfTable` | Cells read and set; its block is left for AutoCAD to draw anew (`RecomputeTableBlock` is not there); a shear or a scale unlike along its axes refused. `table.Cells[r, c]` is a `DxfCell`, `Rows` and `Columns` lists of `DxfRow` and `DxfColumn`; `SetTextStyle` takes the record, not its id. The forms by row type (`SetAlignment(align, rowTypes)` and the like) are not there. |
| `Viewport`, `Layout`, `PlotSettings` | `DxfViewport`, `DxfLayout`, `DxfPlotSettings` | `db.Layouts` walks the layouts in the order of their tabs; plot settings are read only. `new DxfLayout { LayoutName = "Plan" }` is added by `AddToLayoutDictionary(db, null)`, its block made, and `Initialize()` gives it its viewport 1. `SetSun` takes the `DxfSun` itself and gives it back. |
| `LayerTableRecord`, `LinetypeTableRecord`, `TextStyleTableRecord`, `DimStyleTableRecord` | `DxfLayerTableRecord`, `DxfLinetypeTableRecord`, `DxfTextStyleTableRecord`, `DxfDimStyleTableRecord` | Found by name, either case: `db.LayerTable["steel"]` is the layer Steel, and added by `Add`. A layer, a dimension style and a block are renamed by `Name` with every reference to them; a layer, a linetype, a dimension style and a block are erased by `Erase()` where nothing uses them. |
| `lt.Has("steel")`, `lt["steel"]` giving an `ObjectId` | `db.LayerTable.Has("steel")`, `db.LayerTable["steel"]` giving the record | Null where the table has none. |
| `layer.LinetypeObjectId = ltId` | `layer.LinetypeName = "DASHED"` | Records point at records by name where AutoCAD's point by id, and at the record itself where AutoCAD's property is the record (`Dimtxsty`, `Dimblk`). |
| `DBDictionary`, `Xrecord`, `Group`, `db.NamedObjectsDictionaryId`, `db.GroupDictionaryId` | `DxfDBDictionary`, `DxfXrecord`, `DxfGroup`, `db.NamedObjectsDictionary`, `db.GroupDictionary` | The dictionaries are objects; `SetAt` takes the object made in code. |
| `ResultBuffer`, `TypedValue` | `IReadOnlyList<DxfTypedValue>`, `DxfTypedValue` | A value is the text the file prints: `new DxfTypedValue(1040, "2.5")`. Setting `XData` registers its application. |
| `Entity.TransformBy(Matrix3d)`, `GetTransformedCopy` | `TransformBy(GeoTransform3)`, `GetTransformedCopy` | Each class refuses what it cannot hold, as AutoCAD's does (see [Editing rules](rules.md#editing-rules)). |
| `Entity.Explode(DBObjectCollection)` | `Explode()` giving a list | The library explodes an INSERT, the three polylines, a polyface and a polygon mesh, an MTEXT, a dimension, a table, a leader, a multileader, an MLINE, a HATCH of a pattern and a MESH not smoothed, as AutoCAD's do; every other entity refuses with `NotSupportedException`, a MINSERT of one cell or more, which AutoCAD refuses too, a HATCH filled solid or with a gradient, a MESH smoothed, a REGION, a 3DSOLID and a BODY among them, which AutoCAD explodes (see [What the model does not do](rules.md#what-the-model-does-not-do)). |
| `BlockReference.ExplodeToOwnerSpace()` | `ExplodeToOwnerSpace()` | The same: the parts appended, the INSERT kept; a MINSERT gives its first cell alone, as AutoCAD 2021's did. |
| `entity.IntersectWith(other, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero)` | `entity.IntersectWith(other, DxfIntersect.ExtendThis)` giving `IReadOnlyList<GeoPoint3>` | Worked out on the library's shapes; a pair it cannot cross, a text among them, throws `NotSupportedException`, where AutoCAD crosses the box of a TEXT's or an MTEXT's words, which needs the fonts. |
| `GeometricExtents` giving `Extents3d` | `GeometricExtents` giving `GeoAabb3` | Of the shapes `GetGeometry()` gives; a text has no fonts to give it a box. |
| `Curve.GetPointAtParameter`, `GetDistAtPoint`, `GetOffsetCurves`, `GetSplitCurves`, `Extend` | the same on `DxfCurve` | Offsets go the side AutoCAD's do, a line to the left of its way and the rest to the right, as below. |
| `Curve.GetGeCurve()` giving a `Curve3d` | `GetGeCurve()` giving the library's shape (`GeoLine3`, `GeoArc3`, ...) as an `object`; typed, with no cast, `line.ToGeoLine3()`, `polyline.ToGeoPolygonArc2()` and the rest, named as CadConvert names its own | `GetGeometry()` gives the same as `DxfReader` reads it, a LEADER its curve, a `GeoPolyline3` or, splined, a `GeoSpline3`, and a MULTILEADER the lines of its leaders. A typed conversion of the plane refuses an entity off the floor, where CadConvert's drops z (see [Typed shapes](entities.md#typed-shapes)). |
| `Curve.SetFromGeCurve(curve3d)` | `SetFromGeCurve(shape)` | Takes the shape of space and of the plane, and a polyline a chain or a loop, each written as `DxfWriter` writes it. |
| `Clone()`, `db.WblockCloneObjects(ids, ownerId, map, DuplicateRecordCloning.Ignore, false)` | `entity.Clone()` then `other.ModelSpace.AppendEntity(copy)`; `db.WblockCloneObjects(entities, other.ModelSpace)` giving the copies | Records of the tables and the blocks a copy draws by come with it where the other drawing has none of their names, its own used where it has (see [Editing rules](rules.md#editing-rules)). No `IdMapping`: the copies are given back in the order of the entities. |
| `Point3d`, `Vector3d`, `Point2d`, `Vector2d` | `GeoPoint3`, `GeoVector3`, `GeoPoint2`, `GeoVector2` | CadConvert turns one into the other: `point.ToGeoPoint3()`, `point.ToAcadPoint3()`. |
| `Matrix3d.Displacement(v)`, `Rotation(angle, axis, centre)`, `Scaling(factor, centre)`, `Mirroring(plane)` | `GeoTransform3.Translation(v)`, `RotationAxis(centre, axis, angle)`, `Scaling(centre, factor)`, `Mirror(plane)` | `a * b` applies `b` first, as `Matrix3d`'s product does. `Matrix3d.AlignCoordinateSystem` from one system to another is `FromCoordinateSystem(to) * ToCoordinateSystem(from)`. |
| `Extents3d`, `Plane`, `CoordinateSystem3d`, `Tolerance` | `GeoAabb3`, `GeoPlane3`, `GeoCoordinateSystem3`, `Tolerance` | |
| `Color`, `LineWeight`, `Transparency` | `DxfColor`, `DxfLineWeight`, `DxfTransparency` | `DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)` and `FromRgb(255, 128, 0)`, as AutoCAD's. |
| `db.Clayer = layerId`, `db.Insunits`, `Application.SetSystemVariable("LTSCALE", 2.0)` | `db.Clayer = "steel"`, `db.Insunits`, `db.Ltscale = 2` | Variables of the header and of the OBJECTS section as properties; any other by `GetHeader("$NAME")` and `SetHeader`. |
| `db.Dimasz`, `db.Dimblk = blockId`, `db.GetDimstyleData()`, `leader.SetDimstyleData(style)` | `db.Dimasz`, `db.Dimblk = block`, `db.GetDimstyleData()`, `leader.SetDimstyleData(style)` | The `$DIM` variables of the header read as AutoCAD's metric drawing has them where the header gives none; a block, linetype or text style is the record itself. `dim.FormatMeasurement(value, "")` writes the measurement as AutoCAD does, stacked fractions in the codes of an MTEXT; tolerances, limits and the arc symbol are not part of it. |
| `MLeaderStyle`, `mleader.getOverridedMLeaderStyle(style)` | `DxfMLeaderStyle`, `mleader.GetOverridedMLeaderStyle(new DxfMLeaderStyle())` | A multileader style is read, not written: the styles of `db.MLeaderStyleDictionary` as the file holds them, and one made in code given what a multileader is drawn with; `PostMLeaderStyleToDb` is not there. |
| `db.Insert(Matrix3d, other, true)`, `db.Insert("PLATE", other, true)` | `db.Insert(GeoTransform3, other, true)`, `db.Insert("PLATE", other, true)` giving the block | The other drawing's model space, its tables and named blocks merged by name; the other drawing is never changed. |
| `db.Purge(ids)` leaving in `ids` what may be erased; `db.CountHardReferences(ids, counts)` | `db.Purge(objects)` giving those back as a list; `db.CountHardReferences(objects, counts)` | The references a DXF writes by name (a layer, a linetype, a style, a block) counted with the hard pointers. |
| `db.GeoDataObject` giving an `ObjectId`, `GeoLocationData`, `geo.PostToDb()` | `db.GeoDataObject` giving the `DxfGeoData`, `geo.PostToDb()` | `BlockTableRecordId` is `BlockTableRecord`, the record; `PostToDb` gives nothing back, the GEODATA itself now in the file. `EraseFromDb` is `Erase()`. `NorthDirection` is read only, worked out from `NorthDirectionVector`; `TransformToLonLatAlt`, `TransformFromLonLatAlt` and `UpdateTransformationMatrix` are not there. The members of `DxfTypeOfCoordinates` and `DxfScaleEstimationMethod` drop AutoCAD's prefixes: `CoordinateTypeLocal` is `Local`, `ScaleEstMethodUnity` is `Unity`. A `DxfMeshPointMap` is read only, where AutoCAD's `MeshPointMap` sets its two points. |
| `db.LayerStateManager.GetLayerStateNames(false, false)`, `LayerStatesDictionaryId(false)` | `db.LayerStateManager.GetLayerStateNames(false, false)`, `LayerStatesDictionary` | A state is hidden where its mask has the flag 0x8000 and of an xref where its record names its layers by name; `GetLayerStateNames()` gives every state. `LayerStatesDictionary` is read only and makes no dictionary. Saving, restoring, renaming, deleting, importing and exporting a state are not there. |
| `db.LayerFilters` giving a `LayerFilterTree` | `db.LayerFilters` giving a `DxfLayerFilterTree` | Read only: no `Current`, and a tree is not set back. A `DxfLayerFilter`'s `Name` has no setter; `FilterExpression` and `Filter(layer)` are not there. |
| `Highlight`, `Draw`, grips, overrules, events, reactors of the runtime | none | They need AutoCAD running. |
