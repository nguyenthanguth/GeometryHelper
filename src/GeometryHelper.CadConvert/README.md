# GeometryHelper.CadConvert

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.CadConvert.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.CadConvert/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between AutoCAD and
[GeometryHelper](https://github.com/nguyenthanguth/GeometryHelper), both ways: points, vectors, line
segments and `Line` entities, `Ray` and `Xline` entities, polylines and polygons with the arcs their
bulges draw, arcs, circles, ellipses, and `Extents2d`/`Extents3d` as rectangles.

## Installation

```bash
dotnet add package GeometryHelper.CadConvert
```

### You must add the AutoCAD assemblies yourself

This package does **not** contain any AutoCAD assembly. They are Autodesk's to distribute rather than
ours, so it ships only its own code and leaves those references to you. Until you add them, code that
touches this library fails to compile with **CS0012: the type is defined in an assembly that is not
referenced**.

The library itself resolves against **`acdbmgd.dll`** alone — that is where `Point3d`, `Polyline`,
`Circle`, `Line` and `Extents3d` live. A plugin normally also wants `acmgd.dll` and `accoremgd.dll` for
the editor and application APIs, so all three are shown below.

Take them from either source:

**From your AutoCAD installation** — the usual choice, because it guarantees you build against the same
version you run:

```xml
<Reference Include="accoremgd">
  <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\accoremgd.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="acdbmgd">
  <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="acmgd">
  <HintPath>C:\Program Files\Autodesk\AutoCAD 2024\acmgd.dll</HintPath>
  <Private>false</Private>
</Reference>
```

**Or from Autodesk's own package on nuget.org:**

```bash
dotnet add package AutoCAD.NET
```

Keep Copy Local **false**. AutoCAD supplies those assemblies at run time from its own installation, and a
copy sitting next to your plugin risks loading a build that does not match the running AutoCAD.

## Which AutoCAD version

The package is built against the AutoCAD 2024 API (assembly version `24.0.0.0`). Autodesk keeps the
`Autodesk.AutoCAD.Geometry` and `Autodesk.AutoCAD.DatabaseServices` types this library touches stable
across releases, so building from source against another version needs no source changes.

`AutoCAD.NET` on nuget.org carries no `netstandard2.0` assets — version 24–25 ship `net47` and version 26
ships `net10.0` — so a project taking that dependency has to target the framework matching the AutoCAD it
runs in. Referencing the DLLs directly, as above, avoids that constraint.

## Usage

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.CadConvert;
using GeometryHelper.Geometry;

// AutoCAD to GeometryHelper
GeoPoint2 point = new Point3d(100, 200, 0).ToGeoPoint2();
GeoPolyline2 path = acadPolyline.ToGeoPolyline2();
GeoRectangle2 bounds = extents.ToGeoRectangle2();
GeoCircle2 circle = acadCircle.ToGeoCircle2();

// GeometryHelper back to AutoCAD
Polyline drawn = path.ToAcadPolyline();
Line drawnLine = geoLine.ToAcadLine();
Point3d back = point.ToAcadPoint3();
```

### Point chains

`EnumerableExtension` covers the step before a polyline exists: a raw list of points read out of a
drawing, usually carrying more of them than the geometry needs.

```csharp
// Drop points that sit within the tolerance of the point kept before them.
List<Point3d> thinned = points.RemoveConsecutiveNearPoints(0.5);

// One segment per consecutive pair; the chain is left open.
List<LineSegment3d> segments = thinned.ToLineSegments3d();

LineSegment3d longest = segments.GetLongestLength();
```

`RemoveConsecutiveNearPoints` compares each point against the last one *kept*, not against its original
neighbour, which is what guarantees no two points of the result are within the tolerance of each other.
The first point always survives; the last one is not privileged, so re-append it yourself when the
endpoint matters.

## Arcs and bulges

An AutoCAD polyline carries a bulge against each vertex, and a vertex with a bulge is joined to the next
one by an arc. Reading such a polyline as points alone used to straighten it silently, so a slot came
back a rectangle. It no longer does.

```csharp
// Keeps every arc: the bulge AutoCAD stores is the bulge GeometryHelper stores.
GeoPolylineArc2 chain = acadPolyline.ToGeoPolylineArc2();
GeoPolygonArc2 loop   = acadPolyline.ToGeoPolygonArc2();

// Cuts the arcs into straight pieces, within 0.2 % of each radius, and says so in GeometryHelperLog.
GeoPolyline2 path = acadPolyline.ToGeoPolyline2();
GeoPolygon2 area  = acadPolyline.ToGeoPolygon2(0.05);   // or name the chord tolerance yourself

// Refuses rather than approximates, for a caller that needs the shape exactly.
if (acadPolyline.TryToGeoPolygon2(out GeoPolygon2 exact)) { ... }

// And back, bulge for bulge; the loop comes back closed.
Polyline drawnChain = chain.ToAcadPolyline();
Polyline drawnLoop  = loop.ToAcadPolyline();
```

`ToGeoPolyline2` and `ToGeoPolygon2` still return the straight types they always did, so nothing that
used them has to change — they now trace the arc instead of cutting across it. Work on the shape with
the whole of GeometryHelper, then write it back:

```csharp
GeoPolygonArc2 rounded = acadPolyline.ToGeoPolygonArc2().Fillet(25.0);
acadPolyline.Erase();
blockTable.AppendEntity(rounded.ToAcadPolyline());
```

The `Arc` and `Circle` entities convert on their own too:

```csharp
GeoArc2 flat = acadArc.ToGeoArc2();     // in plan; throws if the arc is tilted out of XY
GeoArc3 real = acadArc.ToGeoArc3();     // whatever plane it lies in
acadArc.TryToGeoArc2(out GeoArc2 read); // false rather than throwing

Arc drawn = flat.ToAcadArc();
Circle hole = new GeoCircle2(center, 12.5).ToAcadCircle();
```

An AutoCAD arc always sweeps counter-clockwise about its normal and has nowhere to put a direction of
its own, so a clockwise `GeoArc2` is written with its two angles exchanged: the same curve, read back
counter-clockwise.

## Ellipses

An AutoCAD `Ellipse` is held as a centre, a normal, a major axis as long as the major radius, and the
ratio of the minor radius to the major one, which is what a `GeoEllipse3` holds, so it reads and writes
exactly.

```csharp
GeoEllipse3 real = acadEllipse.ToGeoEllipse3();         // in its own plane
GeoEllipse2 flat = acadEllipse.ToGeoEllipse2();         // as it is seen in plan
acadEllipse.TryToGeoEllipse2(out GeoEllipse2 read);     // false rather than throwing

Ellipse drawn = real.ToAcadEllipse();                   // a whole turn, 0 to 2π
Ellipse drawnFlat = flat.ToAcadEllipse();               // in the XY plane
```

GeometryHelper has no elliptical arc, so an `Ellipse` entity that sweeps less than a whole turn is
refused with an `ArgumentException` rather than closed up. Reading in plan throws an
`InvalidOperationException` for an ellipse standing square to the XY plane, which would flatten into a
segment.

A `Circle` tilted out of the plan is an ellipse in plan, and `ToGeoEllipse2` reads it as one: a circle of
radius 300 tilted 60° from the plan is an ellipse 300 by 150. `ToGeoCircle2` keeps only its centre and
radius, and a tilted `Arc` is still refused by `ToGeoArc2`, since in plan it is an elliptical arc.

```csharp
GeoEllipse2 outline = tiltedCircle.ToGeoEllipse2();
tiltedCircle.TryToGeoEllipse2(out GeoEllipse2 seen);   // false when it stands square to XY
```

## Rays and infinite lines

An AutoCAD `Ray` and `Xline` are each held as a base point and a unit direction, which is what a
`GeoRay3` and a `GeoXLine3` hold, so they convert with nothing lost; the direction is made unit length again on reading, so
it can differ in its last digit after a round trip, and an entity whose direction has no length is refused with an
`ArgumentException`. A ray runs for ever from its base
point, an `Xline` for ever both ways.

```csharp
GeoRay3 ray = acadRay.ToGeoRay3();
GeoXLine3 axis = acadXline.ToGeoXLine3();

Ray drawnRay = ray.ToAcadRay();
Xline drawnAxis = axis.ToAcadXline();

// In the plan: lay the line out in a frame, here the world's.
GeoXLine2 inPlan = axis.ProjectToXLine2(GeoCoordinateSystem3.Global);
```

A `default` ray or line, which has no direction, is refused with an `ArgumentException` on its way to
AutoCAD, which has no entity for it.

These conversions are checked to compile and are untested at run time: AutoCAD's entities cannot be made outside AutoCAD.

## Licence

MIT. The AutoCAD assemblies this library compiles against are covered by Autodesk's own licence terms,
not by this one.
