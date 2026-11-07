# How do I

A task, the call that does it, and the page that tells the rest. Every call takes a [`Tolerance`](xref:GeometryHelper.Tolerance) in an overload of its own
where it compares coordinates; the pages say which.

## In the plane

| To | Call | Page |
|---|---|---|
| measure how far apart two shapes are | `a.DistanceTo(b)`; the segment between them `a.GetShortestLineTo(b)` | [Geometric types](plane/types.md#the-nearest-thing-and-the-segment-to-it) |
| know whether two shapes touch or overlap | `a.CollidesWith(b)` | [Geometric types](plane/types.md#collision-and-intersection) |
| find where two shapes cross | `a.GetIntersections(b)` | [Geometric types](plane/types.md#collision-and-intersection) |
| know how deep inside a closed shape a point is | `shape.SignedDistanceTo(point)` | [Geometric types](plane/types.md#how-deep-inside-not-just-whether) |
| know whether a point is inside, outside or on the edge | `shape.Locate(point)`; on a line `line.IsPointOn(point)` | [Geometric types](plane/types.md) |
| find the nearest point of a boundary | `shape.GetClosestPointOnBoundary(point)`; the nearest edge `GetClosestEdge` | [Geometric types](plane/types.md#the-nearest-thing-and-the-segment-to-it) |
| split a region or a chain by a line | `shape.TrySplitBy(line, ...)`, `Splition2.TrySplitBy` | [Geometric types](plane/types.md#splitting) |
| cut a chain at a distance, or at several | `Splition2.TrySplitAtDistance`, `Splition2.SplitAtDistances` | [Geometric types](plane/types.md#splitting) |
| extend or trim a line to another | `line.TryExtendTo(...)`, `line.TryTrimTo(...)`, `Extend`, `ExtendToLength` | [Extending, offsetting and combining](plane/editing.md#extending-and-trimming) |
| offset a curve or a region | `shape.Offset(distance, tolerance)` | [Extending, offsetting and combining](plane/editing.md#offsetting) |
| unite, subtract, intersect or xor regions | `Boolean2.Union`, `Boolean2.Subtract`; `a.Union(b)`, `a.Subtract(b)`, `a.Xor(b)` | [Extending, offsetting and combining](plane/editing.md#combining-regions) |
| round or cut a corner | `Fillet`, `TryFilletAt`; `Chamfer`, `TryChamferAt`; two lines to their corner `TryTrimExtendToCorner` | [Extending, offsetting and combining](plane/editing.md#cutting-corners) |
| check that a polygon does not cross itself | `polygon.IsSimple()` | [Extending, offsetting and combining](plane/editing.md#a-polygon-that-crosses-itself) |
| take the hull of points, or the rectangle that fits them | `ConvexHull2.Of(points)`, `GeoRectangle2.Fit(points)` | [Extending, offsetting and combining](plane/editing.md#hulls-and-fitted-rectangles) |
| make an arc through three points, or of a bulge | `GeoArc2.FromThreePoints`, `GeoArc2.FromBulge` | [Arcs](plane/arcs.md) |
| straighten a chain that curves | `chain.Flatten(...)` | [Arcs](plane/arcs.md#flattening) |
| turn a circle or an ellipse into a polygon | `circle.ToPolygon(...)`; an ellipse of a circle `GeoEllipse2.FromCircle` | [Circles, ellipses, rays and infinite lines](plane/curves.md) |
| make a spline of a circle | `GeoSpline2.FromCircle` | [Splines](plane/splines.md) |
| take a point along a curve | `GetPointAtDistance`, `GetPointAtParameter` | [Geometric types](plane/types.md) |
| move, turn, scale or mirror a shape | `shape.TransformBy(GeoTransform2.Translation(...))`, `Rotation`, `Scaling`, `Mirror` | [Moving and asking](plane/moving.md#moving-geometry) |
| work in a frame of its own | `GeoTransform2.FromFrame`, `GeoTransform2.ToCoordinateSystem` | [Moving and asking](plane/moving.md#moving-geometry) |
| project a point onto a line | `Projection2.ProjectToInfiniteLine` | [Moving and asking](plane/moving.md#asking-where-something-is) |
| know which side of a line a point is | `line.GetSideOf(point)`, `Containment2.GetSide` | [Moving and asking](plane/moving.md#asking-where-something-is) |
| clean points that repeat | `points.RemoveConsecutiveNearPoints(...)` | [Moving and asking](plane/moving.md#point-chains) |

## In space

| To | Call | Page |
|---|---|---|
| measure, test and cross shapes of space | `a.DistanceTo(b)`, `a.GetShortestLineTo(b)`, `a.CollidesWith(b)`, `a.GetIntersections(b)` | [Asking every shape](solid/shapes.md) |
| know where a point is against a body | `solid.Locate(point)`, `solid.SignedDistanceTo(point)` | [Asking a body](solid/asking.md) |
| make a body of a profile | `GeoSolid3.Extrude(...)`; a box `obb.ToSolid()` | [Closing, making, weighing and merging](solid/making.md#making-bodies) |
| subtract, unite or intersect bodies | `a.TrySubtract(b, ...)`, `a.TryUnion(b, ...)`, `a.Intersect(b)`; many cuts at once `TrySubtractAll` | [Combining bodies](solid/booleans.md) |
| close a body that is open | `solid.TryClose(...)` | [Closing, making, weighing and merging](solid/making.md#closing-an-open-solid) |
| weigh a body | `solid.GetVolume()`, `GetSurfaceArea()`; `Measure3.Volume`, `Measure3.Mass` | [Closing, making, weighing and merging](solid/making.md#weighing-a-body-and-cutting-it) |
| box a body, or points | `solid.GetAabb()`; the box that fits points `GeoObb3.Fit(points)`, their hull `ConvexHull3.Of(points)` | [Closing, making, weighing and merging](solid/making.md#hulls-and-fitted-boxes) |
| merge faces that lie in one plane | `Merge3.CoplanarFaces`, `Merge3.Join` | [Closing, making, weighing and merging](solid/making.md#merging) |
| split a body or a shape | `shape.TrySplitBy(...)`, `TrySplitAt`, `TrySplitAtDistance` | [Splitting, extending and offsetting](solid/splitting.md#splitting) |
| offset a flat shape in its plane | `shape.OffsetInPlane(...)` | [Splitting, extending and offsetting](solid/splitting.md#offsetting) |
| make an arc of space through three points | `GeoArc3.FromThreePoints` | [Arcs in space](solid/arcs.md) |
| find which parts clash | `Clash3.Find(...)` | [Clashes and large meshes](solid/clash.md) |
| take a flat shape of space into the plane and back | `PlanarMap.TryToPolylineArc2`, `PlanarMap.ToPolylineArc3`; a face's frame `face.GetFrame()` | [Between the plane and space, and local frames](solid/frames.md) |
| take the net volume of a model, each overlap once | `VolumeTakeoff.Run(items, options)` | [Volume take-off](takeoff.md) |

## DXF

| To | Call | Page |
|---|---|---|
| read the shapes of a drawing | `DxfReader.Read(path)`, `DxfReader.Parse(text)` | [Reading and writing shapes](dxf/reader.md#reading-dxf) |
| write shapes for CAD to open | `new DxfWriter().Add(shape, layer)`, `Save` | [Reading and writing shapes](dxf/reader.md#writing-dxf) |
| open a drawing to edit and save it | `DxfDatabase.Open(path)`, `db.Save(path)` | [DXF files](dxf/index.md#the-model) |
| find what AutoCAD's call is here | the table of patterns | [AutoCAD and GeometryHelper](dxf/autocad.md) |
| add an entity | `db.ModelSpace.AppendEntity(entity)` | [The drawing, its tables and layouts](dxf/drawing.md) |
| add a layer | `db.LayerTable.Add(new DxfLayerTableRecord { ... })` | [The drawing, its tables and layouts](dxf/drawing.md#the-tables) |
| take an entity's shape, typed | `line.ToGeoLine3()`, `polyline.ToGeoPolygonArc2()`, ... | [Entities and curves](dxf/entities.md#typed-shapes) |
| give an entity a shape of the library | `entity.SetFromGeCurve(shape)` | [Entities and curves](dxf/entities.md#typed-shapes) |
| move, turn or mirror an entity | `entity.TransformBy(GeoTransform3.Translation(...))` | [Editing rules and what the model does not do](dxf/rules.md#editing-rules) |
| break an entity or an INSERT into parts | `entity.Explode()` | [Editing rules and what the model does not do](dxf/rules.md#editing-rules) |
| offset or split a curve as AutoCAD does | `curve.GetOffsetCurves(distance)`, `curve.GetSplitCurves(...)` | [Entities and curves](dxf/entities.md#curves) |
| make a dynamic block | `AddLinearParameter`, `AddStretchAction`, `AddLookupParameter`, ... | [Blocks and attributes](dxf/blocks.md) |
| fill a boundary with a hatch | `hatch.AppendLoop(...)`, `hatch.SetHatchPattern(...)` | [Hatches, leaders and dimensions](dxf/annotation.md#hatches) |
| place a picture by its file | `new DxfRasterImageDef(path, size)`, `new DxfRasterImage(definition)`, `SetOrientation` | [Images, wipeouts and OLE objects](dxf/images.md#images-and-wipeouts) |
| take the picture out of an OLE object | `frame.GetPictureData(out format)` | [Images, wipeouts and OLE objects](dxf/images.md#ole-objects) |
| embed a .bmp or a .png in the drawing | `new DxfOle2Frame(bytes)` | [Images, wipeouts and OLE objects](dxf/images.md#ole-objects) |

## Meshing, label placement and packing

| To | Call | Page |
|---|---|---|
| cut a region into triangles | `face.ToTriangles(...)`; a surface `TriangulateSurface` | [Meshing the plane](mesh.md) |
| lay panels or tiles over a region | `face.ToMesh(MeshOptions.Grid(...))`, `MeshOptions.Strips(...)` | [Meshing the plane](mesh.md) |
| cut a body into cells | `solid.ToCells(CellOptions3.Grid(...))`, `CellOptions3.Layers(...)` | [Meshing in space](mesh3.md) |
| place labels clear of each other | `Arranger.Run(items, options)` | [Label placement](arrange.md) |
| pack drawing views onto sheets | `SheetPacker.Pack(...)` | [Packing boxes onto sheets](packing.md) |
