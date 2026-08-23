# GeometryHelper.TeklaConvert

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between Tekla Structures and
[GeometryHelper](https://github.com/nguyenthanguth/GeometryHelper), both ways: points, vectors, line
segments, geometric planes, coordinate systems, bounding boxes, transformation matrices, and the faces
and loops of a Tekla solid.

## Installation

```bash
dotnet add package GeometryHelper.TeklaConvert
```

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

The package is built against the 2020 API, so its assembly references carry
`Tekla.Structures, Version=2020.0.0.0`.

- **A console application or any process you own** resolves that through a binding redirect, which
  `AutoGenerateBindingRedirects` writes for you, so a newer Tekla works without a rebuild.
- **A plugin loaded inside `TeklaStructures.exe`** cannot: you do not own that process's config. Build
  this project from source against the Tekla version you target.

The API surface it uses — `Point`, `Vector`, `LineSegment`, `Matrix`, `GeometricPlane`,
`CoordinateSystem`, `AABB`, `Solid`, `Face`, `Loop` — is unchanged from 2020 through 2026, so a rebuild
against a newer Tekla needs no source changes.

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
