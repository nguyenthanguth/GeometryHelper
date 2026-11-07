# Geometry in the plane

2D geometry for engineering drawings: points, vectors, lines, polylines, polygons, faces with holes,
circles, ellipses and oriented rectangles, with distance, projection, containment, intersection, collision,
parallelism, parametrization, merging and splitting over them, and the drafting operations on top:
extending and trimming lines, offsetting curves and regions, and combining regions. Every comparison
is tolerance-aware.

[Geometry in space](solid.md) is the 3D counterpart, built to the same design, and the two are
measured with the same [shared types](common.md).

## Namespaces

| Namespace | Holds |
|---|---|
| `GeometryHelper.Geometry` | [`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2), [`GeoVector2`](xref:GeometryHelper.Geometry.GeoVector2), [`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2), [`GeoRay2`](xref:GeometryHelper.Geometry.GeoRay2), [`GeoXLine2`](xref:GeometryHelper.Geometry.GeoXLine2), [`GeoPolyline2`](xref:GeometryHelper.Geometry.GeoPolyline2), [`GeoPolygon2`](xref:GeometryHelper.Geometry.GeoPolygon2), [`GeoFace2`](xref:GeometryHelper.Geometry.GeoFace2), [`GeoCircle2`](xref:GeometryHelper.Geometry.GeoCircle2), [`GeoEllipse2`](xref:GeometryHelper.Geometry.GeoEllipse2), [`GeoSpline2`](xref:GeometryHelper.Geometry.GeoSpline2), [`GeoRectangle2`](xref:GeometryHelper.Geometry.GeoRectangle2), [`GeoTriangle2`](xref:GeometryHelper.Geometry.GeoTriangle2), [`GeoTransform2`](xref:GeometryHelper.Geometry.GeoTransform2) |
| `GeometryHelper.Core` | [`Boolean2`](xref:GeometryHelper.Core.Boolean2), [`Collision2`](xref:GeometryHelper.Core.Collision2), [`Containment2`](xref:GeometryHelper.Core.Containment2), [`Corner2`](xref:GeometryHelper.Core.Corner2), [`Distance2`](xref:GeometryHelper.Core.Distance2), [`Ellipse2`](xref:GeometryHelper.Core.Ellipse2), [`Intersection2`](xref:GeometryHelper.Core.Intersection2), [`Lengthen2`](xref:GeometryHelper.Core.Lengthen2), [`Merge2`](xref:GeometryHelper.Core.Merge2), [`Offset2`](xref:GeometryHelper.Core.Offset2), [`Parallel2`](xref:GeometryHelper.Core.Parallel2), [`Parametrization2`](xref:GeometryHelper.Core.Parametrization2), [`Projection2`](xref:GeometryHelper.Core.Projection2), [`Splition2`](xref:GeometryHelper.Core.Splition2), [`Triangle2`](xref:GeometryHelper.Core.Triangle2), [`PlanarMap`](xref:GeometryHelper.Core.PlanarMap) |
| `GeometryHelper.Meshing` | [`GeoMesh2`](xref:GeometryHelper.Meshing.GeoMesh2), [`Mesh2`](xref:GeometryHelper.Meshing.Mesh2), [`MeshKind`](xref:GeometryHelper.Meshing.MeshKind), [`MeshOptions`](xref:GeometryHelper.Meshing.MeshOptions), [`GridAlignment`](xref:GeometryHelper.Meshing.GridAlignment): see [meshing the plane](mesh.md) |
| `GeometryHelper.Extension` | [`EnumerableExtension`](xref:GeometryHelper.Extension.EnumerableExtension) |
| `GeometryHelper` | [`Tolerance`](xref:GeometryHelper.Tolerance), [`Angle`](xref:GeometryHelper.Angle), [`OffsetOptions`](xref:GeometryHelper.OffsetOptions), [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) |
| `GeometryHelper.Enums` | [`PointLocation`](xref:GeometryHelper.Enums.PointLocation), [`LineSide`](xref:GeometryHelper.Enums.LineSide), [`LineEnd`](xref:GeometryHelper.Enums.LineEnd), [`LineExtension`](xref:GeometryHelper.Enums.LineExtension), [`OffsetJoin`](xref:GeometryHelper.Enums.OffsetJoin) |

Every type carries a `2`, matching the `3` on the solid types, so a program working in both dimensions
imports one namespace and nothing collides.

## The pages of this guide

- <a id="geometric-types"></a><a id="regions-and-curves"></a><a id="collision-and-intersection"></a><a id="what-the-projections-keep"></a><a id="a-face-is-material-and-a-hole-is-not"></a><a id="an-edge-is-a-segment-or-an-arc"></a><a id="the-nearest-thing-and-the-segment-to-it"></a><a id="how-deep-inside-not-just-whether"></a><a id="splitting"></a><a id="cutting-a-region-into-regions"></a>[Geometric Types](plane/types.md)
- <a id="extending-and-trimming"></a><a id="offsetting"></a><a id="combining-regions"></a><a id="triangles-and-meshes"></a><a id="cutting-corners"></a><a id="a-polygon-that-crosses-itself"></a><a id="hulls-and-fitted-rectangles"></a><a id="rounding-corners"></a>[Extending, offsetting and combining](plane/editing.md)
- <a id="arcs"></a><a id="chains-that-curve"></a><a id="asking-a-curved-chain-the-usual-questions"></a><a id="offsetting-without-straightening"></a><a id="an-index-over-the-edges"></a><a id="flattening"></a>[Arcs](plane/arcs.md)
- <a id="circles-as-polygons"></a><a id="working-in-a-local-frame"></a><a id="ellipses"></a><a id="rays"></a><a id="infinite-lines"></a>[Circles, ellipses, rays and infinite lines](plane/curves.md)
- <a id="splines"></a><a id="against-the-other-shapes"></a>[Splines](plane/splines.md)
- <a id="moving-geometry"></a><a id="asking-where-something-is"></a><a id="flat-shapes-that-live-in-space"></a><a id="point-chains"></a>[Moving and asking](plane/moving.md)

## Tolerance

`Tolerance` and `Tolerance.Global` are [shared types](common.md): a program working in the plane and in
space sets one tolerance, not two.

`Tolerance.Global` has a static setter, deliberately mirroring
`Autodesk.AutoCAD.Geometry.Tolerance.Global`. Changing it affects the whole application, so set it
once at startup.

> Importing `Autodesk.AutoCAD.Geometry` alongside this library brings a second `Tolerance` into scope
> and the two tie, giving **CS0104**. Name the one you mean:
> `using Tolerance = GeometryHelper.Tolerance;`.

## Build and Test

```bash
dotnet build src/GeometryHelper/GeometryHelper.csproj
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
```

## Licence

MIT. Clipper2, which this package carries compiled in, is under the Boost Software License 1.0.
