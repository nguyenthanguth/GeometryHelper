# GeometryHelper

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

Geometry for engineering drawings and models, in two dimensions and in three, plus the label placement
library that runs on the 2D half and bridges that carry shapes in and out of Tekla Structures, AutoCAD,
and IFC models (via xBIM). Every comparison is tolerance-aware, because coordinates that come out of a BIM model are
never exact.

**Documentation:** [https://nguyenthanguth.github.io/GeometryHelper/](https://nguyenthanguth.github.io/GeometryHelper/) — the guides and every type and member, searchable.

## Packages

Four libraries, versioned and released together as six packages: the Tekla bridge is published once per
Tekla version.

| Package | What it is | NuGet |
|---|---|---|
| [GeometryHelper](src/GeometryHelper/README.md) | The geometry itself: 2D and 3D shapes, every operation over them, meshing the plane and the flat shapes of space into triangles, grids, strips and convex pieces, bodies cut into the cells of a grid, the label placement algorithms, and the packing of boxes onto sheets. Seven guides: [shared types](src/GeometryHelper/docs/common.md), [the plane](src/GeometryHelper/docs/plane.md), [meshing the plane](src/GeometryHelper/docs/mesh.md), [space](src/GeometryHelper/docs/solid.md), [meshing in space](src/GeometryHelper/docs/mesh3.md), [label placement](src/GeometryHelper/docs/arrange.md), [packing boxes onto sheets](src/GeometryHelper/docs/packing.md) | [![v](https://img.shields.io/nuget/v/GeometryHelper.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper/) |

Up to and including 4.0.0 this shipped as four packages — `GeometryHelper.CommonGeometry`,
`GeometryHelper.PlaneGeometry`, `GeometryHelper.SolidGeometry` and `GeometryHelper.ArrangeAlgorithms`.
They are one library now: having the two dimensions in one assembly is what lets a shape be carried
between them, and it retires the machinery that kept them apart.

Three bridges are published as well. The Tekla and AutoCAD bridges do not redistribute vendor assemblies —
you reference the Tekla or AutoCAD DLLs yourself from your installation or NuGet. The IFC bridge is self-contained
and bundles customized xBIM assemblies and native x64 engines directly in the package.

| Package | What it is | You supply | NuGet |
|---|---|---|---|
| [GeometryHelper.TeklaConvert](src/GeometryHelper.TeklaConvert/README.md) | Points, vectors, segments, planes, coordinate systems, bounding boxes, matrices, and the faces and loops of a Tekla solid, plus the objects of IFC reference models (through GeometryHelper.IfcConvert); one package per Tekla Structures version, `GeometryHelper.TeklaConvert.2020`, `GeometryHelper.TeklaConvert.2025` and `GeometryHelper.TeklaConvert.2026` | `Tekla.Structures.dll`, `Tekla.Structures.Drawing.dll`, `Tekla.Structures.Model.dll` of that version | [![2020](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2020.svg?style=flat-square&label=2020)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2020/) [![2025](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2025.svg?style=flat-square&label=2025)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2025/) [![2026](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2026.svg?style=flat-square&label=2026)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2026/) |
| [GeometryHelper.CadConvert](src/GeometryHelper.CadConvert/README.md) | Points, vectors, lines, polylines and polygons with the arcs their bulges draw, arcs, circles and extents, converted both ways with AutoCAD | `acdbmgd.dll` (plugins usually also want `acmgd.dll`, `accoremgd.dll`) | [![v](https://img.shields.io/nuget/v/GeometryHelper.CadConvert.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.CadConvert/) |
| [GeometryHelper.IfcConvert](src/GeometryHelper.IfcConvert/README.md) | Points, vectors, matrices, faces, and 3D solids, converted from IFC models via xBIM | Bundled (Self-contained) | [![v](https://img.shields.io/nuget/v/GeometryHelper.IfcConvert.svg?style=flat-square&label=)](https://www.nuget.org/packages/GeometryHelper.IfcConvert/) |

## How they fit together

```
GeometryHelper   (Clipper2 compiled in)
   │
GeometryHelper.IfcConvert   ── IFC (xBIM)
   └── GeometryHelper.TeklaConvert ── Tekla Structures
GeometryHelper.CadConvert   ── AutoCAD
```

One direction, no cycles. Every bridge is built on `GeometryHelper` and none of them knows about
another, except that the Tekla bridge reads IFC reference models through the IFC one.

`GeometryHelper` carries the clipping engine of [Clipper2](https://github.com/AngusJohnson/Clipper2) 2.0.0,
compiled in from its source in [src/GeometryHelper/Internal/Clipper](src/GeometryHelper/Internal/Clipper), where
it can be mended; its notice is in [THIRD-PARTY-NOTICES.md](src/GeometryHelper/THIRD-PARTY-NOTICES.md).
It resolves offsets and boolean regions in the plane on integers, so an answer never depends on rounding
luck. Its types are internal, so the package depends on nothing and a project that also references the
Clipper2 package sees that one only. The solid half does the same work with its own winding-number
solver, and the test suite checks the two against each other.

## Which one do you need

- Geometry in the plane, in space, or both, and arranging labels or rebar marks in a drawing → **GeometryHelper**.
- Reading solids and drawing coordinates out of a Tekla model, the objects of its IFC reference models included → **GeometryHelper.TeklaConvert.2020**, **GeometryHelper.TeklaConvert.2025** or **GeometryHelper.TeklaConvert.2026**, whichever matches your Tekla, plus the three Tekla assemblies you reference yourself.
- Reading and writing AutoCAD drawing geometry → **GeometryHelper.CadConvert**, plus the AutoCAD assemblies you reference yourself.
- Reading solids, faces, and geometry from an IFC model → **GeometryHelper.IfcConvert** (bundles all required xBIM assemblies and native x64 engine).

## Tolerance

Use the default tolerance. Every method that compares coordinates has an overload without a tolerance, which
reads `Tolerance.Global`, and `Tolerance.Global` starts as `Tolerance.Default`: a thousandth of a millimetre for
points and for flatness, in a model in millimetres. Left as it is, it gives results as exact as the model's own
corners allow.

- **Fine enough to be exact.** Two parts a hundredth apart are apart, and a thin overlap holds what it holds. Of
  218 pairs of slabs from a Tekla model that share material, the common part came out within 450 cubic millimetres
  of the exact volume at the median, where the default up to 10.0.0, a hundredth, came within 1 084.
- **Not finer.** Tekla Structures gives faces a few ten-thousandths of a millimetre out of flat, and parts touching
  across gaps as thin. Below a thousandth those are cut as the slivers they then are: on a model of 1 989 parts,
  cutting each part by the parts it meets took 12.5 seconds within a hundredth, 18 within a thousandth, and 225 and
  251 within 1E-4 and 1E-5, where 31 and 36 of the cuts took more than a minute each.
- **Pass another one only for a reason, to the call that needs it.** A coarser tolerance takes what a modeller
  gives as touching for touching: net volumes to match the ones Tekla Structures reports come from booleans within
  0.05. A model in metres wants the point and planar thresholds a thousand times smaller,
  `new Tolerance(1E-6, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-6)`, set once as `Tolerance.Global`.
- **Never `new Tolerance()`.** `Tolerance` is a struct: made without one of its constructors, every threshold is 0,
  under which only an exact match is equal and a face out of flat by rounding alone is refused.

The [shared types guide](src/GeometryHelper/docs/common.md#tolerance) has the rest.

## Repository layout

| Project | Role | Target |
|---|---|---|
| `src/GeometryHelper` | Geometry in the plane and in space, and label placement | netstandard2.0 |
| `src/GeometryHelper.TeklaConvert` | Tekla Structures bridge, built once per Tekla version (`-p:TeklaVersion=2020`, `2025` or `2026`) | netstandard2.0 |
| `src/GeometryHelper.CadConvert` | AutoCAD bridge | netstandard2.0 |
| `src/GeometryHelper.IfcConvert` | IFC (xBIM) bridge; runs on .NET Framework 4.8 x64 | netstandard2.0 |
| `tests/GeometryHelper.UnitTest` | xUnit; folders `Common`, `Plane`, `Solid`, `Arranging` | net48 |
| `tests/GeometryHelper.TeklaConvert.UnitTest` | xUnit | net48 |
| `tests/GeometryHelper.IfcConvert.UnitTest` | xUnit | net48 |
| `examples/GeometryHelper.ArrangeAlgorithms.CadTest` | AutoCAD 2021 plugin for visual testing | net48 |
| `examples/GeometryHelper.ArrangeAlgorithms.TeklaTest` | Tekla Structures program for rebar mark arrangement | net48 |

The libraries target `netstandard2.0` so that they load into both the .NET Framework hosts that Tekla and
AutoCAD provide and into modern .NET. The test and sample projects target `net48` because that is what
those hosts run.

## Build and Test

```bash
dotnet build GeometryHelper.slnx -c Release
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj                        # Tekla 2020
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -p:TeklaVersion=2025   # Tekla 2025
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -p:TeklaVersion=2026   # Tekla 2026
dotnet test  tests/GeometryHelper.IfcConvert.UnitTest/GeometryHelper.IfcConvert.UnitTest.csproj
```

Warnings are errors in CI, and every public member is documented, so a missing XML comment or a stale
`cref` fails the build rather than landing quietly.

**What CI leaves out, and why.** The two sample applications need AutoCAD and a Tekla installation
respectively, which a hosted runner does not have. `GeometryHelper.CadConvert` compiles against the
AutoCAD assemblies vendored under its `Lib/` folder, but those are mixed-mode and cannot be loaded
outside `acad.exe` — not even to construct a point — so the project has no unit tests. The Tekla bridge
is the exception: the assemblies it needs are committed under
`src/GeometryHelper.TeklaConvert/Lib2020`, `Lib2025` and `Lib2026`, and the Tekla geometry types are
plain data, so it builds and tests in CI like anything else, once for each Tekla version it ships for.

## Licence

MIT.
