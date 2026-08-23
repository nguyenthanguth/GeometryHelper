# GeometryHelper.CadConvert

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.CadConvert.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.CadConvert/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between AutoCAD and
[GeometryHelper](https://github.com/nguyenthanguth/GeometryHelper), both ways: points, vectors, line
segments and `Line` entities, polylines, polygons, circles, and `Extents2d`/`Extents3d` as rectangles.

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
using GeometryHelper.PlaneGeometry.Geometry;

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

## Licence

MIT. The AutoCAD assemblies this library compiles against are covered by Autodesk's own licence terms,
not by this one.
