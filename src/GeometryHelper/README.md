# GeometryHelper

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Geometry for engineering drawings and models, in two dimensions and in three, with the label placement
algorithms that run on the 2D half. Written in C# and targeting `netstandard2.0`, so it loads into the
.NET Framework hosts that Tekla Structures and AutoCAD provide as well as into modern .NET.

Every comparison that floating point error can affect takes a `Tolerance`, because coordinates that come
out of a BIM model are never exact.

## Installation

```bash
dotnet add package GeometryHelper
```

## Quick start

```csharp
using GeometryHelper;            // Tolerance, Angle, OffsetOptions
using GeometryHelper.Enums;      // LineEnd, OffsetJoin, PointLocation
using GeometryHelper.Geometry;   // every Geo type, 2D and 3D
using GeometryHelper.Core;       // every operation

// In the plane: grow a slab, then cut an opening out of it.
var slab = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(4000, 0),
                           new GeoPoint2(4000, 2000), new GeoPoint2(0, 2000));
var duct = new GeoPolygon2(new GeoPoint2(1000, 500), new GeoPoint2(1400, 500),
                           new GeoPoint2(1400, 900), new GeoPoint2(1000, 900));

GeoPolygon2 grown = slab.Offset(50.0)[0];                 // 4100 x 2100
GeoFace2 pierced = Boolean2.Subtract(grown, duct)[0];     // one face, one hole

// In space: a beam trimmed where it runs into a wall.
var beam = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(3000, 0, 0));
var wall = new GeoPlane3(new GeoPoint3(2500, 0, 0), GeoVector3.XAxis);
beam.TryTrimTo(wall, LineEnd.End, out GeoLine3 cut);      // (0,0,0) -> (2500,0,0)
```

## What is inside

| Namespace | Holds |
|---|---|
| `GeometryHelper` | `Tolerance`, `Angle`, `OffsetOptions`, `GeometryHelperLog` |
| `GeometryHelper.Enums` | `PointLocation`, `PlaneSide`, `LineSide`, `LineEnd`, `LineExtension`, `OffsetJoin` |
| `GeometryHelper.Geometry` | 30 immutable shapes: `GeoPoint2` … `GeoFace2` in the plane, `GeoPoint3` … `GeoSolid3` in space, arcs and the chains that carry them, and a transformation and a local coordinate system for each dimension |
| `GeometryHelper.Core` | 28 operation classes, each dimension mirroring the other: `Boolean2`/`Boolean3`, `Offset2`/`Offset3`, `Distance2`/`Distance3`, … plus `Arc2`, `Corner2` for chamfering and rounding, and `PlanarMap`, which carries flat shapes between the two |
| `GeometryHelper.Spatial` | `GeoBvh2` and `GeoBvh3`, the bounding volume hierarchies for large chains and meshes |
| `GeometryHelper.Extension` | turning raw point lists into geometry |
| `GeometryHelper.Arranging` | label placement: `Arrange`, `ArrangeOptions`, five algorithms |

Every operation is reachable both ways: the static form names the larger shape first, and the instance
form sits on whichever of the two reads better where you are calling from.

Because both dimensions are one assembly, a flat shape in space can be laid out in its own plane, worked
on with the whole 2D half of the library, and put back:

```csharp
GeoCoordinateSystem3 frame = plate.GetFrame();
GeoFace2 flat = plate.ProjectToFace2(frame);
GeoFace3 back = flat.Subtract(openings)[0].ToFace3(frame);
```

## Guides

The whole of it, searchable, with every type and member: [https://nguyenthanguth.github.io/GeometryHelper/](https://nguyenthanguth.github.io/GeometryHelper/).


| Guide | Covers |
|---|---|
| [Shared types](https://github.com/nguyenthanguth/GeometryHelper/blob/main/src/GeometryHelper/docs/common.md) | `Tolerance`, `Angle`, the enumerations, `OffsetOptions`, `GeometryHelperLog` |
| [Geometry in the plane](https://github.com/nguyenthanguth/GeometryHelper/blob/main/src/GeometryHelper/docs/plane.md) | points to polygons and faces; extending, trimming, offsetting, combining regions |
| [Geometry in space](https://github.com/nguyenthanguth/GeometryHelper/blob/main/src/GeometryHelper/docs/solid.md) | points to solids; splitting, boolean bodies, meshes, local frames |
| [Label placement](https://github.com/nguyenthanguth/GeometryHelper/blob/main/src/GeometryHelper/docs/arrange.md) | five algorithms behind one entry point |

## Coverage

```bash
dotnet test tests/GeometryHelper.UnitTest --collect:"XPlat Code Coverage"
```

writes a Cobertura file under `TestResults`. Only this library is counted.

## Build and Test

```bash
dotnet build src/GeometryHelper/GeometryHelper.csproj
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
```

Warnings are errors in CI, and every public member is documented, so a missing XML comment or a stale
`cref` fails the build rather than landing quietly. Every example printed in the guides is also a test.

## Licence

MIT. [Clipper2](https://github.com/AngusJohnson/Clipper2), which this package references and which
resolves regions in the plane, is under the Boost Software License 1.0.
