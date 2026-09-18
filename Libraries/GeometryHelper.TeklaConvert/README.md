# GeometryHelper.TeklaConvert

[![Tekla 2020](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2020.svg?style=flat-square&label=Tekla%202020)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2020/)
[![Tekla 2025](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2025.svg?style=flat-square&label=Tekla%202025)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2025/)
[![Tekla 2026](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2026.svg?style=flat-square&label=Tekla%202026)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2026/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between Tekla Structures and
[GeometryHelper](https://github.com/nguyenthanguth/GeometryHelper), both ways: points, vectors, line
segments, geometric planes, coordinate systems, bounding boxes, transformation matrices, and the faces
and loops of a Tekla solid.

## Installation

Install the package whose year matches the Tekla Structures you build for:

```bash
dotnet add package GeometryHelper.TeklaConvert.2020   # Tekla Structures 2020
dotnet add package GeometryHelper.TeklaConvert.2025   # Tekla Structures 2025
dotnet add package GeometryHelper.TeklaConvert.2026   # Tekla Structures 2026
```

All three carry the same code, namespaces and `GeometryHelper.TeklaConvert.dll`; they differ only in the Tekla
Open API they are compiled against (see [Which Tekla version](#which-tekla-version)). They replace the former
`GeometryHelper.TeklaConvert` package, which was built against 2020 only: switching needs no code change.

### You must add two Tekla assemblies yourself

This package does **not** contain `Tekla.Structures.dll` or `Tekla.Structures.Drawing.dll`. They are
Trimble's to distribute rather than ours, so the package ships only its own code and leaves those two
references to you. Until you add them, code that touches this library fails to compile with **CS0012:
the type is defined in an assembly that is not referenced**.

Take them from either source:

**From your Tekla Structures installation** — the usual choice, because it guarantees you build against
the same version you run:

```xml
<Reference Include="Tekla.Structures">
  <HintPath>C:\Program Files\Tekla Structures\2026.0\bin\Tekla.Structures.dll</HintPath>
  <Private>False</Private>
</Reference>
<Reference Include="Tekla.Structures.Drawing">
  <HintPath>C:\Program Files\Tekla Structures\2026.0\bin\Tekla.Structures.Drawing.dll</HintPath>
  <Private>False</Private>
</Reference>
```

**Or from Trimble's own packages on nuget.org** (published from 2024 onward):

```bash
dotnet add package Tekla.Structures
dotnet add package Tekla.Structures.Drawing
```

Keep `Private`/Copy Local **false**. Tekla loads those assemblies from its own installation at run time,
and a copy sitting next to your plugin risks loading a build that does not match the running Tekla.

## Which Tekla version

Tekla's Open API assemblies are strong-named per release, so each package is compiled against one
Tekla version and its references carry that version:

| Package | Package version | DLL version | References |
|---|---|---|---|
| `GeometryHelper.TeklaConvert.2020` | `x.y.z` | `x.y.z.2020` | `Tekla.Structures, Version=2020.0.0.0` |
| `GeometryHelper.TeklaConvert.2025` | `x.y.z` | `x.y.z.2025` | `Tekla.Structures, Version=2025.0.0.0` |
| `GeometryHelper.TeklaConvert.2026` | `x.y.z` | `x.y.z.2026` | `Tekla.Structures, Version=2026.0.0.0` |

- **A plugin loaded inside `TeklaStructures.exe`** cannot redirect one Tekla version to another (you do
  not own that process's config), so install the package for the Tekla it runs in.
- **A console application or any process you own** can also run against a newer Tekla than its package
  through a binding redirect, which `AutoGenerateBindingRedirects` writes for you.

The Tekla year is part of the package id rather than the version, so a package update never moves you to
a build for another Tekla, and both packages carry the version the rest of GeometryHelper is released under
(`GeometryHelper.TeklaConvert.2026` 4.0.0 depends on `GeometryHelper.SolidGeometry` 4.0.0). The assembly
and file version of `GeometryHelper.TeklaConvert.dll` add the year (`4.0.0.2026`), so the DLL itself tells
which Tekla it was built for.

The API surface it uses — `Point`, `Vector`, `LineSegment`, `Matrix`, `GeometricPlane`,
`CoordinateSystem`, `AABB`, `Solid`, `Face`, `Loop` — is unchanged from 2020 through 2026.

### Building from source

The project builds for one Tekla version at a time, chosen by the `TeklaVersion` property (2020 when
omitted), against the assemblies committed under `Lib2020`, `Lib2025` or `Lib2026`:

```bash
dotnet build Libraries/GeometryHelper.TeklaConvert/GeometryHelper.TeklaConvert.csproj -p:TeklaVersion=2026
dotnet test  Tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -p:TeklaVersion=2026
```

Each version builds into its own `bin/Tekla{year}` and `obj/Tekla{year}` folders. Supporting another Tekla
version means adding its `Lib{year}` folder and its year to `TEKLA_VERSIONS` in the two GitHub workflows.

## Usage

```csharp
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;

var tolerance = new Tolerance(1E-2, 1E-4);

// Tekla to GeometryHelper
GeoPoint3 point = new TSG.Point(1000, 2000, 3000).ToGeoPoint3();
GeoLine3 line = segment.ToGeoLine3();

if (teklaSolid.TryToGeoSolid3(out GeoSolid3 body, tolerance))
{
    double volume = body.Volume;
    body.TrySubtract(otherBody, out GeoSolid3 remainder);
}

// GeometryHelper back to Tekla
TSG.Point back = point.ToTeklaPoint();
TSG.Matrix matrix = transform.ToTeklaMatrix();
```

## What is checked rather than trusted

Tekla hands back what its modeller happens to hold; `GeometryHelper.SolidGeometry` asks for flatness, a
closed boundary, and normals that point out of the body. The conversions do that checking, because a
body that only looks right measures wrong later and says nothing about why.

- A coordinate system whose Y axis is not quite square to its X axis is squared up.
- Each face is turned to agree with the normal Tekla gives it, and the finished body is turned inside
  out if its signed volume says the whole surface arrived reversed. Without that, volume still measures
  the same but every containment query answers backwards.
- A face that cannot be made sense of is skipped rather than thrown on, which leaves the body no longer
  closed — so ask `IsClosed()` before trusting a volume.

Tekla models in millimetres with coordinates that can run to hundreds of thousands, and a face of a
twelve metre member is rarely flat to the last decimal. The default `EqualPlanar` is often too tight for
that, so pass a `Tolerance` suited to the model rather than relying on the default.

## Licence

MIT. The Tekla Structures assemblies this library compiles against are covered by Trimble's own licence
terms, not by this one.
