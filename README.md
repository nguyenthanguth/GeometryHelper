# GeometryHelper

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

Geometry for engineering drawings and models, in two dimensions and in three, plus the label
placement library the 2D half grew out of. Four packages, versioned and released together.

| Package | What it is | NuGet |
|---|---|---|
| [GeometryHelper.CommonGeometry](Libraries/GeometryHelper.CommonGeometry/README.md) | `Tolerance`, `Angle`, `PointLocation`, `PlaneSide` — the types both geometry libraries need | [![v](https://img.shields.io/nuget/v/GeometryHelper.CommonGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.CommonGeometry/) |
| [GeometryHelper.PlaneGeometry](Libraries/GeometryHelper.PlaneGeometry/README.md) | 2D: points to polygons, with distance, containment, intersection, splitting | [![v](https://img.shields.io/nuget/v/GeometryHelper.PlaneGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.PlaneGeometry/) |
| [GeometryHelper.SolidGeometry](Libraries/GeometryHelper.SolidGeometry/README.md) | 3D: points to solids, plus boolean operations and a BVH for large meshes | [![v](https://img.shields.io/nuget/v/GeometryHelper.SolidGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.SolidGeometry/) |
| [GeometryHelper.ArrangeAlgorithms](Libraries/GeometryHelper.ArrangeAlgorithms/README.md) | 2D label placement: five algorithms that keep labels off each other and off blocked regions | [![v](https://img.shields.io/nuget/v/GeometryHelper.ArrangeAlgorithms.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.ArrangeAlgorithms/) |

```
GeometryHelper.CommonGeometry
   ├── GeometryHelper.PlaneGeometry ── GeometryHelper.ArrangeAlgorithms
   └── GeometryHelper.SolidGeometry

GeometryHelper.TeklaConvert
GeometryHelper.CadConvert
```

One direction, no cycles. `GeometryHelper.PlaneGeometry` and `GeometryHelper.SolidGeometry` do not
know about each other; what they share, they share through `GeometryHelper.CommonGeometry`, so a
program using both sees one `Tolerance` and one `Angle` rather than two of each.

## Which one do you need

- Arranging labels or rebar marks in a drawing → **GeometryHelper.ArrangeAlgorithms**, which brings the rest with it.
- Geometry in the plane and nothing else → **GeometryHelper.PlaneGeometry**.
- Geometry in space → **GeometryHelper.SolidGeometry**.
- Reading solids and drawing coordinates out of a Tekla model → **GeometryHelper.TeklaConvert**.
- Interacting with AutoCAD drawing geometry → **GeometryHelper.CadConvert**.
- Both bridge projects are not published, because the assemblies they need from Tekla and AutoCAD are not ours to redistribute.

## What changed in 3.0.0

Version 2.0.0 of `ArrangeAlgorithms` was one package holding both the label placement algorithms and
the 2D geometry they run on. The geometry was 83% of the code and depended on none of the placement
work, so it became a package of its own.

**Namespaces to update:**

| 2.x | 3.0 |
|---|---|
| `ArrangeAlgorithms` (for `Tolerance`) | `GeometryHelper.CommonGeometry` |
| `ArrangeAlgorithms.Datatype` | `GeometryHelper.CommonGeometry.Datatype` |
| `ArrangeAlgorithms.Enums` | `GeometryHelper.CommonGeometry.Enums` |
| `ArrangeAlgorithms.Core` | `GeometryHelper.PlaneGeometry.Core` |
| `ArrangeAlgorithms.Geometry` | `GeometryHelper.PlaneGeometry.Geometry` |
| `ArrangeAlgorithms.Extension` | removed |
| `ArrangeAlgorithms`, `ArrangeAlgorithms.Algorithms` | `GeometryHelper.ArrangeAlgorithms`, `GeometryHelper.ArrangeAlgorithms.Algorithms` |

**Types to rename.** Every 2D type gained a `2`, matching the `3` that GeometryHelper.SolidGeometry already used:

| | |
|---|---|
| Geometry | `GeoPoint2` `GeoVector2` `GeoLine2` `GeoPolyline2` `GeoPolygon2` `GeoCircle2` `GeoRectangle2` |
| Core | `Collision2` `Containment2` `Distance2` `Intersection2` `Merge2` `Parallel2` `Parametrization2` `Projection2` `Splition2` |

`Parallel2` no longer collides with `System.Threading.Tasks.Parallel`, which the old name did.

**Two things that bite quietly:**

- `Tolerance.Global` is now one setting shared by both geometry libraries rather than one each.
  Setting it for a drawing in the plane sets it for a model in space too.
- If your code sits in a namespace under `ArrangeAlgorithms` and also imports a library with its own
  `Tolerance` — `Autodesk.AutoCAD.Geometry` does — the two now tie where they did not before, because
  ours arrives through a `using` rather than through the enclosing namespace. You will see **CS0104**.
  Name the one you mean: `using Tolerance = GeometryHelper.CommonGeometry.Tolerance;`.

`EnumerableExtension` was removed. Nothing in the repository called it, and its `MaxBy`/`MinBy`
collide with `System.Linq` on .NET 6 and later.

`[TypeForwardedTo]` cannot soften any of this: it only works when a type keeps its full name, and
these changed namespace.

## Repository layout

| Project | Role | Target |
|---|---|---|
| `GeometryHelper.CommonGeometry` | Shared tolerance, angle and enumerations | netstandard2.0 |
| `GeometryHelper.PlaneGeometry` | 2D geometry | netstandard2.0 |
| `GeometryHelper.SolidGeometry` | 3D geometry | netstandard2.0 |
| `GeometryHelper.ArrangeAlgorithms` | Label placement algorithms | netstandard2.0 |
| `GeometryHelper.TeklaConvert` | Converts geometry between Tekla Structures and GeometryHelper | netstandard2.0 |
| `GeometryHelper.CadConvert` | Converts geometry between AutoCAD and GeometryHelper | netstandard2.0 |
| `GeometryHelper.CommonGeometry.UnitTest` | xUnit | net48 |
| `GeometryHelper.PlaneGeometry.UnitTest` | xUnit | net48 |
| `GeometryHelper.SolidGeometry.UnitTest` | xUnit | net48 |
| `GeometryHelper.TeklaConvert.UnitTest` | xUnit | net48 |
| `GeometryHelper.ArrangeAlgorithms.UnitTest` | xUnit | net48 |
| `GeometryHelper.ArrangeAlgorithms.CadTest` | AutoCAD 2021 plugin for visual testing | net48 |
| `GeometryHelper.ArrangeAlgorithms.TeklaTest` | Tekla Structures program for rebar mark arrangement | net48 |

## Build and Test

```bash
dotnet build GeometryHelper.slnx -c Release
dotnet test  Tests/GeometryHelper.CommonGeometry.UnitTest/GeometryHelper.CommonGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.PlaneGeometry.UnitTest/GeometryHelper.PlaneGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.SolidGeometry.UnitTest/GeometryHelper.SolidGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj
dotnet test  Tests/GeometryHelper.ArrangeAlgorithms.UnitTest/GeometryHelper.ArrangeAlgorithms.UnitTest.csproj
```

The two sample projects are excluded from CI: `GeometryHelper.ArrangeAlgorithms.CadTest` needs AutoCAD
assemblies and `GeometryHelper.ArrangeAlgorithms.TeklaTest` needs a Tekla installation, neither of which a hosted runner has. The
Tekla *bridge* is different — the assemblies it needs are committed under
`Libraries/GeometryHelper.TeklaConvert/Lib2020`, so it builds and tests in CI like anything else.

## Licence

MIT.
