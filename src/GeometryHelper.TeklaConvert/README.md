# GeometryHelper.TeklaConvert

[![Tekla 2020](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2020.svg?style=flat-square&label=Tekla%202020)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2020/)
[![Tekla 2025](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2025.svg?style=flat-square&label=Tekla%202025)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2025/)
[![Tekla 2026](https://img.shields.io/nuget/v/GeometryHelper.TeklaConvert.2026.svg?style=flat-square&label=Tekla%202026)](https://www.nuget.org/packages/GeometryHelper.TeklaConvert.2026/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between Tekla Structures and
[GeometryHelper](https://github.com/nguyenthanguth/GeometryHelper), both ways: points, vectors, line
segments, geometric planes, coordinate systems, bounding boxes, transformation matrices, and the faces
and loops of a Tekla solid. It also reads the objects of IFC reference models into solids placed where
Tekla shows them (see [IFC reference models](#ifc-reference-models)).

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

Each also brings [GeometryHelper.IfcConvert](https://www.nuget.org/packages/GeometryHelper.IfcConvert/) (xBIM,
about 16 MB), which reads the IFC reference models.

### You must add three Tekla assemblies yourself

This package does **not** contain `Tekla.Structures.dll`, `Tekla.Structures.Drawing.dll` or
`Tekla.Structures.Model.dll`. They are Trimble's to distribute rather than ours, so the package ships only
its own code and leaves those three references to you. Until you add them, code that touches this library
fails to compile with **CS0012: the type is defined in an assembly that is not referenced**.

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
<Reference Include="Tekla.Structures.Model">
  <HintPath>C:\Program Files\Tekla Structures\2026.0\bin\Tekla.Structures.Model.dll</HintPath>
  <Private>False</Private>
</Reference>
```

**Or from Trimble's own packages on nuget.org** (published from 2024 onward):

```bash
dotnet add package Tekla.Structures
dotnet add package Tekla.Structures.Drawing
dotnet add package Tekla.Structures.Model
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
(`GeometryHelper.TeklaConvert.2026` 4.0.0 depends on `GeometryHelper` 4.0.0). The assembly
and file version of `GeometryHelper.TeklaConvert.dll` add the year (`4.0.0.2026`), so the DLL itself tells
which Tekla it was built for.

The API surface it uses — `Point`, `Vector`, `LineSegment`, `Matrix`, `GeometricPlane`,
`CoordinateSystem`, `AABB`, `Solid`, `Face`, `Loop` — is unchanged from 2020 through 2026.

### Building from source

The project builds for one Tekla version at a time, chosen by the `TeklaVersion` property (2020 when
omitted), against the assemblies committed under `Lib2020`, `Lib2025` or `Lib2026`:

```bash
dotnet build src/GeometryHelper.TeklaConvert/GeometryHelper.TeklaConvert.csproj -p:TeklaVersion=2026
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -p:TeklaVersion=2026
```

Each version builds into its own `bin/Tekla{year}` and `obj/Tekla{year}` folders. Supporting another Tekla
version means adding its `Lib{year}` folder and its year to `TEKLA_VERSIONS` in the two GitHub workflows.

## Usage

```csharp
using GeometryHelper;
using GeometryHelper.Geometry;
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

## IFC reference models

`ReferenceModelObjectConvert` and `ReferenceModelConvert` read the objects of IFC reference models into
`GeoSolid3` bodies, in the **current work plane**, like every other coordinate the Tekla API returns
(`Part.GetSolid()` included). The IFC file is read through
[GeometryHelper.IfcConvert](https://www.nuget.org/packages/GeometryHelper.IfcConvert/) in millimetres at the
reference model's `Scale`, and placed where the reference model was inserted.

```csharp
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;

// The IFC objects selected in the model view (Tekla.Structures.Model has a ModelObjectSelector too, hence the full name).
var selected = new List<ReferenceModelObject>();
ModelObjectEnumerator selection = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
while (selection.MoveNext())
{
    if (selection.Current is ReferenceModelObject referenceObject)
    {
        selected.Add(referenceObject);
    }
}

// The bodies of the selected objects, from any number of reference models and IFC files ...
GeoSolid3[] solids = selected.ToGeoSolids();

// ... or each IFC product whole: GlobalId, name, type, bodies, open surfaces and conversion warnings.
IReadOnlyList<IfcProductGeometry> products = selected.ToIfcGeometries();

// Every physical product of the reference models those objects belong to (each reference model once) ...
GeoSolid3[] everything = selected.Select(o => o.GetReferenceModel()).ToGeoSolids();

// ... or each of them whole.
IReadOnlyList<IfcProductGeometry> everyProduct = selected.Select(o => o.GetReferenceModel()).ToIfcGeometries();

// To see them in the model view: face boundaries red, holes white (see Drawing in the model view).
solids.DrawToTekla();
new Model().CommitChanges();
```

| Method | Returns |
|---|---|
| `ToGeoSolids()` on a `ReferenceModelObject` or a sequence of them | The bodies of the objects' IFC products |
| `ToIfcGeometries()` on a sequence of `ReferenceModelObject` | One `IfcProductGeometry` per product, in the order given |
| `ToGeoSolids()` on a `ReferenceModel` or a sequence of them | Every physical product of the IFC file |
| `ToIfcGeometries()` on a `ReferenceModel` or a sequence of them | One `IfcProductGeometry` per physical product of the IFC file |
| `ReferenceModelObject.GetIfcGuid()` | The IFC GlobalId (`EXTERNAL.GUID`), or null |
| `ReferenceModel.GetIfcFilePath()` | The full path of the IFC file of the active revision, or null |
| `ReferenceModel.GetIfcToGlobal()`, `GetIfcToWorkPlane()` | The placement, for geometry you read yourself |
| `ReferenceModel.CreateIfcConvertOptions(options)` | The options to read with: millimetres and the reference model's scale |

Worth knowing:

- **Nothing is thrown for a bad file.** A reference model that is not IFC (DWG, SKP...), a file that cannot be
  read and an object that cannot be converted are left out; an empty result means nothing could be read. What
  was left out, and why, is written to `GeometryHelperLog` (GeometryHelper), along with one line per call
  saying how many products were converted.
- **Options.** Without options, a selected assembly returns its parts (`IncludeAggregatedParts`), as Tekla
  selects them, and openings are left uncut (`ApplyVoids`) for speed. Pass
  `new IfcConvertOptions { ApplyVoids = true, IncludeAggregatedParts = true }` to cut bolt holes and other
  openings as Tekla shows them. That is much slower on steel (500 beams took 29 s instead of 1.5 s), and
  cut bodies can come back not closed. The unit, the scale and the coordinate space always follow the
  reference model. `ReferenceModel.ToGeoSolids()` never adds aggregated parts: it visits the parts
  themselves, which would otherwise be counted twice.
- **Speed: choose products by name.** Bolts are what makes a whole Tekla model slow to read: every product of
  a 115 MB steel model took about 17 minutes, 96 % of it on its 5,512 bolts. Leave them out with
  `SkipNames = { "Bolt assembly" }` (35 s), or keep only what you need with `OnlyNames`, for example
  `{ "BEAM", "GIRDER", "PRD_COLUMN", "COLUMN" }` (3.5 s). The names are those the model gives its parts; both
  lists ignore case and take `*` as a wildcard, and apply to an assembly's parts too. See
  [Choosing Products by Name](https://github.com/nguyenthanguth/GeometryHelper/tree/main/src/GeometryHelper.IfcConvert#choosing-products-by-name).
- **Several reference models.** Objects are grouped by reference model, so each is placed with its own
  position, rotation and scale. The same IFC file inserted twice is parsed once and placed twice.
- **Assemblies.** Selecting an assembly *and* its parts returns those parts twice.
- **The work plane** is the one current when you call. It is switched to global for a moment to read where
  each reference model sits, and restored before any IFC file is read, so do not call from several threads
  at once.
- **Cache.** Each IFC file is parsed once per process; a plugin keeps it for the whole Tekla session. A new
  revision of a reference model usually has a new `ActiveFilePath` and is parsed afresh. If a file is
  overwritten in place, call `IfcStoreCache.ClearGlobalCache(path)`, or the old geometry keeps being returned.
- **Run time.** These methods need .NET Framework 4.8 x64, which every Tekla plugin runs on, and
  `Xbim.Geometry.Engine64.dll`, which the build copies next to your plugin: ship it with the extension
  (`.tsep`). The rest of this package never loads xBIM.
- **A broken IFC file can take Tekla down.** The xBIM geometry engine is native code, and an access violation
  inside it cannot be caught by .NET Framework: it ends the process. Save the model before converting files
  you do not know.

## Drawing in the model view

`GeometryDraw` draws GeometryHelper geometry as control polycurves, the temporary lines of the model view, to
check where a result lies. Like everything the Tekla API is given, they are placed in the current work plane,
so what `Part.GetSolid()` or `ToGeoSolids()` returned is drawn where it came from.

| Method | Draws |
|---|---|
| `DrawPolyline(closed)` on a sequence of Tekla `Point` or `GeoPoint3` | A polyline through the points, closed back to the first one if asked |
| `DrawToTekla()` on a `GeoPolyline3` | The polyline |
| `DrawToTekla()` on a `GeoPolygon3` | The closed polygon |
| `DrawToTekla()` on a `GeoFace3` | Its outer boundary in red and every hole in white |
| `DrawToTekla()` on a `GeoSolid3` or a sequence of them | Every face: boundaries in red, holes in white |
| `DrawToTekla()` on a `GeoArc3` or a `GeoCircle3` | The curve, cut into straight pieces within 0.2 % of its radius |

- Colours (`ControlObjectColorEnum`) and the line type (`ControlObjectLineType`, solid when left out) can be
  passed. A polyline or polygon takes one colour, red when left out. A face or solid takes two, one for outer
  boundaries and one for holes, red and white when left out:
  `face.DrawToTekla(ControlObjectColorEnum.BLUE, ControlObjectColorEnum.YELLOW, ControlObjectLineType.DashedLine)`.
- The view shows them after `Model.CommitChanges()`. The methods do not commit, so drawing a thousand faces
  costs one commit rather than a thousand.
- Each returns the polycurves it inserted; `Delete()` on them takes them away again.
- A polyline with fewer than two distinct points, or one Tekla will not insert, is skipped; one Tekla refuses is
  written to `GeometryHelperLog`.

## Reinforcement

Tekla sets a bar out the way a schedule does: the points it turns at, and a bending radius for each turn.
The bar is **not** that polyline. It is that polyline with a tangent arc at every bend, so it is shorter
than its set-out and it does not pass through its own corners. `GeoPolylineArc3` holds exactly that.

```csharp
foreach (GeoPolylineArc3 bar in reinforcement.ToGeoPolylineArc3s())
{
    total += bar.Length;
}
```

A bar is always read **as Tekla works it out**, never as it was typed in, because asking Tekla for its
geometries means the hooks, the offsets and the lapping are all settled. It reads on any reinforcement at
all: a single bar, a group, a curved or circle group, a mesh or a strand. A `RebarSet` is not a
`Reinforcement` but holds them, so it answers the same call.

A geometry that cannot be read is passed over rather than stopping the rest, and the number passed over is
reported through `GeometryHelperLog`.

### Many at once

```csharp
Reinforcement[] bars = model.GetModelObjectSelector()
    .GetAllObjectsWithType(ModelObject.ModelObjectEnum.REBAR_GROUP)
    .ToArray<Reinforcement>();

GeoPolylineArc3[] all = bars.ToGeoPolylineArc3s();
```

Worth using over a loop of single calls rather than only shorter to write: where a Tekla build needs the
work plane turned to global first, this turns it once for the whole lot and puts it back once, instead of
once per reinforcement. A null in the sequence is passed over.

### Tekla Structures 2020

Tekla 2020 hands back the geometry of a **lapped** bar in the wrong place unless the current work plane is
the global one. The 2020 package therefore turns the work plane to global before reading and puts it back
afterwards, whether the read finished or threw. The 2025 and 2026 packages do not need it, and there the
whole thing compiles away to nothing: the workaround lives in that one year's DLL, through a
`TeklaVersion2020` compilation symbol the build defines from the year.

The work plane is model-wide state, so the restore matters more than the change; it happens in a `finally`,
by way of `IDisposable`.

### The pieces underneath

| Extends | File | What for |
|---|---|---|
| `IEnumerable<TSG.Point>` | `PointConvert` | points and a radius per bend into a bent chain |
| `TSG.PolyLine` | `PolyLineConvert` | the same, from a Tekla polyline, both ways |
| `TSM.Polygon` | `PolygonConvert` | the run of points Tekla lays shapes out with, both ways |
| `TSM.RebarGeometry` | `RebarGeometryConvert` | one bar as Tekla worked it out |
| the `Reinforcement` family | `ReinforcementConvert` | the reads above |

Tekla gives one radius per **bend**, so a bar of four points has two, and the first belongs to the second
point because nothing turns at the start of a bar. A list already as long as the points is taken as it is,
which is the layout `GeometryHelper` itself uses. A bend with too little straight run either side to fit its
radius is left square rather than forced, and where two bends want more of the run between them than it is
long, the one taking more of it gives way.

Everything but the read itself works on plain data holders, so it runs in the tests without Tekla installed.
`GetRebarGeometries`, and the work plane the 2020 package turns, need the modeller.

## What is checked rather than trusted

Tekla hands back what its modeller happens to hold; `GeometryHelper` asks for flatness, a
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
