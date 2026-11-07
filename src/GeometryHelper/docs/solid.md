# Geometry in space

3D geometry for engineering models: points, vectors, lines, rays, planes, triangles, polylines,
polygons, circles, faces with holes, solids, boxes, local coordinate systems and transformations.

It is the counterpart of [geometry in the plane](plane.md) and follows the same shape: immutable
geometry types, operations living in static classes under `Core`, and every operation mirrored as an
instance method on the type it applies to. Both are measured with the same [shared types](common.md).

## Structure

| Namespace | Contents |
|---|---|
| `GeometryHelper.Geometry` | `GeoPoint3`, `GeoVector3`, `GeoLine3`, `GeoRay3`, `GeoXLine3`, `GeoPlane3`, `GeoTriangle3`, `GeoPolyline3`, `GeoPolygon3`, `GeoCircle3`, `GeoEllipse3`, `GeoFace3`, `GeoAabb3`, `GeoObb3`, `GeoSolid3`, `GeoSphere3`, `GeoEdge3`, `GeoPolylineArc3`, `GeoPolygonArc3`, `GeoCoordinateSystem3`, `GeoTransform3` |
| `GeometryHelper.Core` | `Boolean3`, `Collision3`, `Containment3`, `Distance3`, `Intersection3`, `Lengthen3`, `Merge3`, `Offset3`, `Parallel3`, `Parametrization3`, `Projection3`, `Splition3`, `Ellipse3`, `PlanarMap` |
| `GeometryHelper.Spatial` | `GeoBvh3` |
| `GeometryHelper.Extension` | `EnumerableExtension` |
| `GeometryHelper` | `Tolerance`, `Angle`, `OffsetOptions`, `GeometryHelperLog` |
| `GeometryHelper.Enums` | `PointLocation`, `PlaneSide`, `LineEnd`, `LineExtension`, `OffsetJoin` |

The last two are the [shared types](common.md), which the plane half uses as well, so a program
working in both dimensions sees one `Tolerance` and one `Angle` rather than two of each.

## Quick Start

```csharp
using GeometryHelper;
using GeometryHelper.Geometry;

var a = new GeoPoint3(0, 0, 0);
var b = new GeoPoint3(3, 4, 0);

double distance = a.DistanceTo(b);              // 5
GeoVector3 direction = a.GetVectorTo(b);        // [3, 4, 0]

var plane = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);
GeoPoint3 flat = plane.Project(new GeoPoint3(2, 3, 7));      // (2, 3, 0)
double signed = plane.SignedDistanceTo(new GeoPoint3(2, 3, 7)); // 7

var box = new GeoObb3(GeoPoint3.Origin, 10, 20, 30);
double volume = box.Volume;                     // 6000
```

## The pages of this guide

- <a id="geometric-types"></a><a id="flatness-is-enforced"></a><a id="two-kinds-of-box"></a><a id="solids-and-openings"></a>[Geometric Types](solid/types.md)
- <a id="operations"></a><a id="asking-a-body-about-what-is-around-it"></a><a id="how-deep-inside-not-just-whether"></a><a id="a-body-asked-many-questions"></a>[Asking a body](solid/asking.md)
- <a id="and-every-shape-can-be-asked-not-only-the-body"></a><a id="how-near-is-touching-in-space"></a><a id="a-segment-or-a-ray-against-a-flat-shape"></a><a id="flat-shapes-against-each-other"></a><a id="parametrization"></a>[Asking every shape](solid/shapes.md)
- <a id="infinite-lines"></a><a id="balls"></a>[Infinite lines and balls](solid/lines-balls.md)
- <a id="splitting"></a><a id="extending-and-trimming"></a><a id="offsetting"></a>[Splitting, extending and offsetting](solid/splitting.md)
- <a id="combining-bodies"></a>[Combining bodies](solid/booleans.md)
- <a id="closing-an-open-solid"></a><a id="making-bodies"></a><a id="weighing-a-body-and-cutting-it"></a><a id="hulls-and-fitted-boxes"></a><a id="merging"></a>[Closing, making, weighing and merging](solid/making.md)
- <a id="arcs-in-space"></a><a id="a-bar-is-a-chain-with-a-bend-at-every-corner"></a><a id="closed-loops-have-to-lie-flat"></a><a id="one-bar-against-another-and-against-everything-else"></a><a id="cutting-a-bar-to-a-schedule-and-to-the-openings"></a><a id="cutting-corners-in-space-and-measuring-from-a-loop"></a><a id="what-is-exact-and-what-is-sampled"></a>[Arcs in space](solid/arcs.md)
- <a id="ellipses-in-space"></a><a id="splines-in-space"></a><a id="against-the-other-shapes-in-space"></a>[Ellipses and splines in space](solid/curves.md)
- <a id="checking-parts-against-each-other"></a><a id="how-deep-a-clash-runs"></a><a id="reinforcement-by-its-centre-line"></a><a id="working-with-large-meshes"></a><a id="point-chains"></a>[Clashes and large meshes](solid/clash.md)
- <a id="between-the-plane-and-space"></a><a id="working-in-a-local-frame"></a><a id="tekla-structures"></a>[Between the plane and space, and local frames](solid/frames.md)

## Tolerance

Nothing in this library compares coordinates with `==`. Every comparison that floating point error can
affect takes a `Tolerance`, and every such method has an overload without one that reads `Tolerance.Global`.

```csharp
var a = GeoPoint3.Origin;
var b = new GeoPoint3(1e-9, 0, 0);

a.IsEqualTo(b);  // true  — within the default tolerance
a.Equals(b);     // false — exact comparison

a.IsEqualTo(new GeoPoint3(0.05, 0, 0), new Tolerance(0.1, 0.1)); // true
```

`Tolerance` carries four thresholds: `EqualPoint` for coincidence, `EqualVector` for direction,
`EqualAngleRad` for parallelism, and `EqualPlanar` for flatness.

Degenerate input is treated as "no answer" rather than being guessed at. A zero-length vector has no
direction, so it is neither parallel nor perpendicular to anything, and `Normalize()` on it throws while
`TryGetNormal` reports the failure:

```csharp
GeoVector3.Zero.IsParallelTo(GeoVector3.XAxis);        // false
GeoVector3.Zero.TryGetNormal(out GeoVector3 unit);     // false
GeoVector3.Zero.Normalize();                           // throws InvalidOperationException
```

## Build and Test

```bash
dotnet build src/GeometryHelper/GeometryHelper.csproj
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj
```

Every snippet in this README is covered by a unit test in `ReadmeExamplesTests`.

## License

MIT
