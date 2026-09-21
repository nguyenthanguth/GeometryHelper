# GeometryHelper

Geometry for engineering drawings and models, in two dimensions and in three, plus the label placement
library that runs on the 2D half and bridges that carry shapes in and out of Tekla Structures, AutoCAD and
IFC models. Every comparison is tolerance-aware, because coordinates that come out of a BIM model are
never exact.

```bash
dotnet add package GeometryHelper
```

## Where to start

| | |
|---|---|
| [Shared types](../src/GeometryHelper/docs/common.md) | `Tolerance`, `Angle`, the enumerations, `OffsetOptions`, `GeometryHelperLog` |
| [Geometry in the plane](../src/GeometryHelper/docs/plane.md) | points to polygons and faces; arcs, offsetting, combining regions |
| [Geometry in space](../src/GeometryHelper/docs/solid.md) | points to solids; splitting, boolean bodies, meshes, local frames |
| [Label placement](../src/GeometryHelper/docs/arrange.md) | five algorithms behind one entry point |
| [AutoCAD](../src/GeometryHelper.CadConvert/README.md) | both ways with AutoCAD, arcs and bulges included |
| [Tekla Structures](../src/GeometryHelper.TeklaConvert/README.md) | both ways with Tekla, and IFC reference models |
| [IFC](../src/GeometryHelper.IfcConvert/README.md) | IFC models into solids, through xBIM |
| [API Reference](xref:GeometryHelper.Geometry) | every type and member of all four, from the XML documentation |

## A first look

```csharp
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Core;

// A slot: two straight sides and a half circle at each end, held the way a drawing holds it.
var slot = new GeoPolygonArc2(
    new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
    new[] { 0.0, 1.0, 0.0, 1.0 });

slot.Area;                          // exact, counting what each arc adds beyond its chord
slot.Contains(new GeoPoint2(215, 25));   // true — the round end reaches past the chord

// Offsetting keeps the arcs: a round end of 25 grows to 45, about the same centre.
GeoPolygonArc2 grown = slot.Offset(20.0)[0];

// The booleans cannot, and say so by handing back the straight type.
var opening = new GeoPolygon2(new GeoPoint2(80, 15), new GeoPoint2(120, 15),
                              new GeoPoint2(120, 35), new GeoPoint2(80, 35));

GeoFace2[] pierced = slot.Subtract(opening);
```

## The packages

| Package | What it is |
|---|---|
| [GeometryHelper](https://www.nuget.org/packages/GeometryHelper/) | the geometry itself, 2D and 3D, and the label placement algorithms |
| [GeometryHelper.CadConvert](https://www.nuget.org/packages/GeometryHelper.CadConvert/) | both ways with AutoCAD, arcs and bulges included |
| [GeometryHelper.TeklaConvert.2020](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2020/) · [2025](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2025/) · [2026](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2026/) | both ways with Tekla Structures, one package per version |
| [GeometryHelper.IfcConvert](https://www.nuget.org/packages/GeometryHelper.IfcConvert/) | IFC models into solids, through xBIM |

The library targets `netstandard2.0` and its only dependency is
[Clipper2](https://github.com/AngusJohnson/Clipper2) (Boost Software License).
