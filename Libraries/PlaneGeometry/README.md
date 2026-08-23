# PlaneGeometry

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/ArrangeAlgorithms/blob/main/LICENSE)

2D geometry for engineering drawings: points, vectors, lines, polylines, polygons, circles and
oriented rectangles, with distance, projection, containment, intersection, collision, parallelism,
parametrization, merging and splitting over them. Every comparison is tolerance-aware.

This code shipped inside [ArrangeAlgorithms](https://www.nuget.org/packages/ArrangeAlgorithms/) up to
version 2.0.0. It became a package of its own in 3.0.0 because it is geometry, not label placement,
and nothing in it depends on the placement algorithms.

[SolidGeometry](https://www.nuget.org/packages/SolidGeometry/) is the 3D counterpart, built to the
same design and sharing `Tolerance`, `Angle` and `PointLocation` through
[CommonGeometry](https://www.nuget.org/packages/CommonGeometry/).

## Installation

```bash
dotnet add package PlaneGeometry
```

## Namespaces

| Namespace | Holds |
|---|---|
| `PlaneGeometry.Geometry` | `GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoPolyline2`, `GeoPolygon2`, `GeoCircle2`, `GeoRectangle2` |
| `PlaneGeometry.Core` | `Collision2`, `Containment2`, `Distance2`, `Intersection2`, `Merge2`, `Parallel2`, `Parametrization2`, `Projection2`, `Splition2` |
| `CommonGeometry` | `Tolerance` |
| `CommonGeometry.Datatype` | `Angle` |
| `CommonGeometry.Enums` | `PointLocation` |

Every type carries a `2`, matching the `3` in SolidGeometry, so a program working in both dimensions
can import both without aliasing anything.

## Geometric Types

`GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoCircle2`, `GeoRectangle2` (rotated rectangle — OBB), `GeoPolygon2`, `GeoPolyline2`.

### Regions and curves

The shapes split into two families, and the distinction decides what you can ask of them:

| Family | Types | Encloses an area |
|---|---|---|
| Region | `GeoCircle2`, `GeoRectangle2`, `GeoPolygon2` | yes |
| Curve | `GeoLine2`, `GeoPolyline2` | no |

A `GeoPolyline2` is always an open chain — it has no `IsClosed` flag and never joins its last vertex back to its first. Geometry meant to enclose something is a `GeoPolygon2`, and `polyline.ToPolygon()` converts between them.

That rule is what decides the answers below. A chain of vertices tracing a square still holds only the points on its path:

```csharp
var traced = new GeoPolyline2(
    new GeoPoint2(0, 0), new GeoPoint2(10, 0),
    new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));

traced.Locate(new GeoPoint2(5, 5));            // OutSide  — a curve has no interior
traced.DistanceTo(new GeoPoint2(5, 5));        // 5.0      — measured to the path
traced.ToPolygon().Locate(new GeoPoint2(5, 5)); // Inside  — now it is a region
traced.ToPolygon().DistanceTo(new GeoPoint2(5, 5)); // 0.0
```

Only regions offer `Contains`; every shape offers `Locate`, and curves report `OnSide` or `OutSide`.

### Collision and intersection

`CollidesWith` answers whether two shapes overlap, `GetIntersections` returns the crossing points. Every pair is available from both directions, and each has an overload taking an explicit `Tolerance`:

```csharp
rect.CollidesWith(line);        line.CollidesWith(rect);
rect.CollidesWith(poly);        poly.CollidesWith(rect);
circle.CollidesWith(polyline);  polyline.CollidesWith(circle);
rect.CollidesWith(otherRect);   poly.CollidesWith(otherPoly);   line.CollidesWith(otherLine);

GeoPoint2[] points = poly.GetIntersections(line);
```

### Splitting

`Splition2` cuts a `GeoLine2` or a `GeoPolyline2` — at a position along it, or wherever a cutter meets it. Pieces come back in order along the subject, so the first piece always holds its start point and the last holds its end point.

Cutting at a position:

```csharp
Splition2.TrySplitBy(line, point, out GeoLine2 first, out GeoLine2 second);
Splition2.TrySplitAtDistance(polyline, 12.5, out GeoPolyline2 head, out GeoPolyline2 tail);

GeoLine2[] pieces = Splition2.SplitAtDistances(line, new[] { 2.0, 5.0, 8.0 });
```

Cutting with another shape. A single cutter that can only meet a segment once fills two pieces; anything that can meet it repeatedly fills an array:

```csharp
Splition2.TrySplitBy(line, cutter, out GeoLine2 first, out GeoLine2 second);
Splition2.TrySplitBy(polyline, cutter, out GeoPolyline2[] pieces);

// Several cutters at once, and points already known to lie on the subject.
Splition2.TrySplitBy(line, new[] { cutterA, cutterB }, out GeoLine2[] byLines);
Splition2.TrySplitBy(polyline, new[] { new GeoPoint2(3, 0) }, out GeoPolyline2[] byPoints);
```

Splitting against a `GeoPolygon2` sorts the result by which side of the boundary each part falls on, and keeps each run whole rather than breaking it into segments:

```csharp
Splition2.TrySplitBy(line,     polygon, out GeoLine2[] inside,     out GeoLine2[] outside);
Splition2.TrySplitBy(polyline, polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns);

// Several polygons behave as their union.
Splition2.TrySplitBy(polyline, new[] { polygonA, polygonB }, out GeoPolyline2[] within, out GeoPolyline2[] beyond);
```

Every split is also reachable from the shape being cut, which is usually how it reads better:

```csharp
line.TrySplitBy(point, out GeoLine2 first, out GeoLine2 second);
line.TrySplitAtDistance(4.0, out first, out second);
line.TrySplitBy(polygon, out GeoLine2[] inside, out GeoLine2[] outside);
GeoLine2[] pieces = line.SplitAtDistances(new[] { 2.0, 5.0, 8.0 });

polyline.TrySplitBy(cutter, out GeoPolyline2[] parts);
polyline.TrySplitBy(polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns);
```

The instance methods live on the shape being cut, not on the cutter: `polygon.Split(line)` would leave it unclear which of the two comes back in pieces.

**What the return value means.** `false` says nothing was cut, not that the call failed. The out parameters are always usable: an array form hands back the subject as a single piece, and a polygon form puts it in whichever of the two arrays matches the side it lies on, leaving the other empty.

**What gets skipped.** Cut positions outside the subject, or landing on one of its endpoints, are not splits. Positions closer together than the tolerance merge into one, and a position within a tolerance of an existing vertex snaps onto it, so no piece and no edge is ever shorter than the tolerance. A point that does not lie on the subject is refused rather than projected onto it — cutting at its projection would be cutting somewhere nobody asked for.

**Against a polygon.** A part running along the boundary counts as inside, matching `Contains`. A path that merely touches the boundary and turns back has not crossed it, so it comes back whole instead of split in two at the touch.

## Tolerance

`Tolerance` and `Tolerance.Global` come from the `CommonGeometry` package, which SolidGeometry shares,
so a program using both libraries sets one tolerance rather than two. See
[its README](https://github.com/nguyenthanguth/ArrangeAlgorithms/blob/main/CommonGeometry/README.md).

`Tolerance.Global` has a static setter, deliberately mirroring
`Autodesk.AutoCAD.Geometry.Tolerance.Global`. Changing it affects the whole application, so set it
once at startup.

> If your own code sits in a namespace under `ArrangeAlgorithms` and also imports
> `Autodesk.AutoCAD.Geometry`, the two `Tolerance` types now tie where they did not before, because
> ours arrives through a `using` rather than through the enclosing namespace. Name the one you mean:
> `using Tolerance = CommonGeometry.Tolerance;`.

## Build and Test

```bash
dotnet build Libraries/PlaneGeometry/PlaneGeometry.csproj
dotnet test  Tests/PlaneGeometry.UnitTest/PlaneGeometry.UnitTest.csproj
```

## Licence

MIT.
