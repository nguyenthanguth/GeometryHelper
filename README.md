# GeometryHelper

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

Geometry for engineering drawings and models, in two dimensions and in three, plus the label placement
library that runs on the 2D half and bridges that carry shapes in and out of Tekla Structures and
AutoCAD. Every comparison is tolerance-aware, because coordinates that come out of a BIM model are
never exact.

## Packages

Six packages, versioned and released together.

| Package | What it is | NuGet |
|---|---|---|
| [GeometryHelper.CommonGeometry](Libraries/GeometryHelper.CommonGeometry/README.md) | `Tolerance`, `Angle`, `PointLocation`, `PlaneSide` — the types both geometry libraries need | [![v](https://img.shields.io/nuget/v/GeometryHelper.CommonGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.CommonGeometry/) |
| [GeometryHelper.PlaneGeometry](Libraries/GeometryHelper.PlaneGeometry/README.md) | 2D: points to polygons, with distance, containment, intersection, splitting | [![v](https://img.shields.io/nuget/v/GeometryHelper.PlaneGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.PlaneGeometry/) |
| [GeometryHelper.SolidGeometry](Libraries/GeometryHelper.SolidGeometry/README.md) | 3D: points to solids, plus boolean operations and a BVH for large meshes | [![v](https://img.shields.io/nuget/v/GeometryHelper.SolidGeometry.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.SolidGeometry/) |
| [GeometryHelper.ArrangeAlgorithms](Libraries/GeometryHelper.ArrangeAlgorithms/README.md) | 2D label placement: five algorithms that keep labels off each other and off blocked regions | [![v](https://img.shields.io/nuget/v/GeometryHelper.ArrangeAlgorithms.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.ArrangeAlgorithms/) |

Two bridges are published as well. Neither redistributes a vendor assembly — you reference the Tekla or
AutoCAD DLLs yourself, from your own installation or from the vendor's package on nuget.org — so each one
needs that extra step after `dotnet add package`. Their READMEs say exactly which.

| Package | What it is | You supply | NuGet |
|---|---|---|---|
| [GeometryHelper.TeklaConvert](Libraries/GeometryHelper.TeklaConvert/README.md) | Points, vectors, segments, planes, coordinate systems, bounding boxes, matrices, and the faces and loops of a Tekla solid | `Tekla.Structures.dll`, `Tekla.Structures.Drawing.dll` | [![v](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert/) |
| [GeometryHelper.CadConvert](Libraries/GeometryHelper.CadConvert/README.md) | Points, vectors, lines, polylines, polygons, circles and extents, converted both ways with AutoCAD | `acdbmgd.dll` (plugins usually also want `acmgd.dll`, `accoremgd.dll`) | [![v](https://img.shields.io/nuget/v/GeometryHelper.CadConvert.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.CadConvert/) |

## How they fit together

```
GeometryHelper.CommonGeometry
   ├── GeometryHelper.PlaneGeometry ── GeometryHelper.ArrangeAlgorithms
   └── GeometryHelper.SolidGeometry

GeometryHelper.TeklaConvert ── Tekla Structures
GeometryHelper.CadConvert   ── AutoCAD
```

One direction, no cycles. `GeometryHelper.PlaneGeometry` and `GeometryHelper.SolidGeometry` do not know
about each other; what they share, they share through `GeometryHelper.CommonGeometry`, so a program using
both sees one `Tolerance` and one `Angle` rather than two of each.

## Which one do you need

- Arranging labels or rebar marks in a drawing → **GeometryHelper.ArrangeAlgorithms**, which brings the geometry with it.
- Geometry in the plane and nothing else → **GeometryHelper.PlaneGeometry**.
- Geometry in space → **GeometryHelper.SolidGeometry**.
- Only `Tolerance` and `Angle`, to sit between your own libraries → **GeometryHelper.CommonGeometry**.
- Reading solids and drawing coordinates out of a Tekla model → **GeometryHelper.TeklaConvert**, plus the two Tekla assemblies you reference yourself.
- Reading and writing AutoCAD drawing geometry → **GeometryHelper.CadConvert**, plus the AutoCAD assemblies you reference yourself.

## Repository layout

| Project | Role | Target |
|---|---|---|
| `Libraries/GeometryHelper.CommonGeometry` | Shared tolerance, angle and enumerations | netstandard2.0 |
| `Libraries/GeometryHelper.PlaneGeometry` | 2D geometry | netstandard2.0 |
| `Libraries/GeometryHelper.SolidGeometry` | 3D geometry | netstandard2.0 |
| `Libraries/GeometryHelper.ArrangeAlgorithms` | Label placement algorithms | netstandard2.0 |
| `Libraries/GeometryHelper.TeklaConvert` | Tekla Structures bridge | netstandard2.0 |
| `Libraries/GeometryHelper.CadConvert` | AutoCAD bridge | netstandard2.0 |
| `Tests/GeometryHelper.CommonGeometry.UnitTest` | xUnit | net48 |
| `Tests/GeometryHelper.PlaneGeometry.UnitTest` | xUnit | net48 |
| `Tests/GeometryHelper.SolidGeometry.UnitTest` | xUnit | net48 |
| `Tests/GeometryHelper.TeklaConvert.UnitTest` | xUnit | net48 |
| `Tests/GeometryHelper.ArrangeAlgorithms.UnitTest` | xUnit | net48 |
| `Samples/GeometryHelper.ArrangeAlgorithms.CadTest` | AutoCAD 2021 plugin for visual testing | net48 |
| `Samples/GeometryHelper.ArrangeAlgorithms.TeklaTest` | Tekla Structures program for rebar mark arrangement | net48 |

The libraries target `netstandard2.0` so that they load into both the .NET Framework hosts that Tekla and
AutoCAD provide and into modern .NET. The test and sample projects target `net48` because that is what
those hosts run.

## Build and Test

```bash
dotnet build GeometryHelper.slnx -c Release
dotnet test  Tests/GeometryHelper.CommonGeometry.UnitTest/GeometryHelper.CommonGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.PlaneGeometry.UnitTest/GeometryHelper.PlaneGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.SolidGeometry.UnitTest/GeometryHelper.SolidGeometry.UnitTest.csproj
dotnet test  Tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj
dotnet test  Tests/GeometryHelper.ArrangeAlgorithms.UnitTest/GeometryHelper.ArrangeAlgorithms.UnitTest.csproj
```

Warnings are errors in CI, and every public member is documented, so a missing XML comment or a stale
`cref` fails the build rather than landing quietly.

**What CI leaves out, and why.** The two sample applications need AutoCAD and a Tekla installation
respectively, which a hosted runner does not have. `GeometryHelper.CadConvert` compiles against the
AutoCAD assemblies vendored under its `Lib/` folder, but those are mixed-mode and cannot be loaded
outside `acad.exe` — not even to construct a point — so the project has no unit tests. The Tekla bridge
is the exception: the two assemblies it needs are committed under
`Libraries/GeometryHelper.TeklaConvert/Lib2020`, and the Tekla geometry types are plain data, so it
builds and tests in CI like anything else.

## Licence

MIT.
