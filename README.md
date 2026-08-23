# GeometryHelper

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

Geometry for engineering drawings and models, in two dimensions and in three, plus the label
placement library the 2D half grew out of. Four packages, versioned and released together.

| Package | What it is | NuGet |
|---|---|---|
| [CommonGeometry](CommonGeometry/README.md) | `Tolerance`, `Angle`, `PointLocation`, `PlaneSide` — the types both geometry libraries need | [![v](https://img.shields.io/nuget/v/CommonGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/CommonGeometry/) |
| [PlaneGeometry](PlaneGeometry/README.md) | 2D: points to polygons, with distance, containment, intersection, splitting | [![v](https://img.shields.io/nuget/v/PlaneGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/PlaneGeometry/) |
| [SolidGeometry](SolidGeometry/README.md) | 3D: points to solids, plus boolean operations and a BVH for large meshes | [![v](https://img.shields.io/nuget/v/SolidGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/SolidGeometry/) |
| [ArrangeAlgorithms](ArrangeAlgorithms/README.md) | 2D label placement: five algorithms that keep labels off each other and off blocked regions | [![v](https://img.shields.io/nuget/v/ArrangeAlgorithms.svg?style=flat-square&label=)](https://www.nuget.org/packages/ArrangeAlgorithms/) |

```
CommonGeometry
   ├── PlaneGeometry ── ArrangeAlgorithms
   └── SolidGeometry ── SolidGeometry.Tekla
```

One direction, no cycles. `PlaneGeometry` and `SolidGeometry` do not know about each other; what they
share, they share through `CommonGeometry`, so a program using both sees one `Tolerance` and one
`Angle` rather than two of each.

## Which one do you need

- Arranging labels or rebar marks in a drawing → **ArrangeAlgorithms**, which brings the rest with it.
- Geometry in the plane and nothing else → **PlaneGeometry**.
- Geometry in space → **SolidGeometry**.
- Reading solids out of a Tekla model → **SolidGeometry.Tekla**, which is in this repository but is
  not published, because the two Tekla assemblies it needs are not ours to redistribute.

## What changed in 3.0.0

Version 2.0.0 of `ArrangeAlgorithms` was one package holding both the label placement algorithms and
the 2D geometry they run on. The geometry was 83% of the code and depended on none of the placement
work, so it became a package of its own.

**Namespaces to update:**

| 2.x | 3.0 |
|---|---|
| `ArrangeAlgorithms` (for `Tolerance`) | `CommonGeometry` |
| `ArrangeAlgorithms.Datatype` | `CommonGeometry.Datatype` |
| `ArrangeAlgorithms.Enums` | `CommonGeometry.Enums` |
| `ArrangeAlgorithms.Core` | `PlaneGeometry.Core` |
| `ArrangeAlgorithms.Geometry` | `PlaneGeometry.Geometry` |
| `ArrangeAlgorithms.Extension` | removed |
| `ArrangeAlgorithms`, `ArrangeAlgorithms.Algorithms` | unchanged |

**Types to rename.** Every 2D type gained a `2`, matching the `3` that SolidGeometry already used:

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
  Name the one you mean: `using Tolerance = CommonGeometry.Tolerance;`.

`EnumerableExtension` was removed. Nothing in the repository called it, and its `MaxBy`/`MinBy`
collide with `System.Linq` on .NET 6 and later.

`[TypeForwardedTo]` cannot soften any of this: it only works when a type keeps its full name, and
these changed namespace.

## Repository layout

| Project | Role | Target |
|---|---|---|
| `CommonGeometry` | Shared tolerance, angle and enumerations | netstandard2.0 |
| `PlaneGeometry` | 2D geometry | netstandard2.0 |
| `SolidGeometry` | 3D geometry | netstandard2.0 |
| `ArrangeAlgorithms` | Label placement algorithms | netstandard2.0 |
| `SolidGeometry.Tekla` | Converts geometry between Tekla Structures and SolidGeometry | net48 |
| `CommonGeometry.UnitTest` | xUnit | net48 |
| `PlaneGeometry.UnitTest` | xUnit | net48 |
| `SolidGeometry.UnitTest` | xUnit | net48 |
| `SolidGeometry.Tekla.UnitTest` | xUnit | net48 |
| `ArrangeAlgorithms.UnitTest` | xUnit | net48 |
| `ArrangeAlgorithms.CadTest` | AutoCAD 2021 plugin for visual testing | net48 |
| `ArrangeAlgorithms.TeklaTest` | Tekla Structures program for rebar mark arrangement | net48 |

## Build and Test

```bash
dotnet build GeometryHelper.slnx -c Release
dotnet test  Tests/CommonGeometry.UnitTest/CommonGeometry.UnitTest.csproj
dotnet test  Tests/PlaneGeometry.UnitTest/PlaneGeometry.UnitTest.csproj
dotnet test  Tests/SolidGeometry.UnitTest/SolidGeometry.UnitTest.csproj
dotnet test  Tests/SolidGeometry.Tekla.UnitTest/SolidGeometry.Tekla.UnitTest.csproj
dotnet test  Tests/ArrangeAlgorithms.UnitTest/ArrangeAlgorithms.UnitTest.csproj
```

The two sample projects are excluded from CI: `ArrangeAlgorithms.CadTest` needs AutoCAD assemblies and
`ArrangeAlgorithms.TeklaTest` needs a Tekla installation, neither of which a hosted runner has. The
Tekla *bridge* is different — the assemblies it needs are committed under
`SolidGeometry.Tekla/Lib2020`, so it builds and tests in CI like anything else.

## Licence

MIT.
