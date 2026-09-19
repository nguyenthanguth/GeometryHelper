# GeometryHelper.IfcConvert

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.IfcConvert.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.IfcConvert/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](../../LICENSE)

Converts geometry between IFC models (via xBIM) and GeometryHelper (`GeometryHelper.CommonGeometry`
and `GeometryHelper.SolidGeometry`): points, vectors, transformation
matrices, planar faces, curved surfaces, and 3D solids.

## What It Does

`GeometryHelper.IfcConvert` bridges building information models in IFC (IFC2x3, IFC4, IFC4x3) to pure
computational geometry structures in `GeometryHelper`:
- Converts 3D points (`XbimPoint3D`), vectors (`XbimVector3D`), and homogeneous transformation matrices (`XbimMatrix3D` / `GeoTransform3`).
- Converts IFC boundary representations and swept solids (`IXbimSolid`, `IIfcGeometricRepresentationItem`, `IIfcProduct`, `IIfcMappedItem`) into `GeoSolid3` bodies.
- Handles planar faces with both outer boundaries and inner cutout loops/holes (`InnerBounds`).
- Automatically tessellates curved and non-planar faces into planar triangles, maintaining strict coplanarity compliance for `SolidGeometry`.
- Preserves hierarchical coordinate placements across nested mapped items and product placements.
- Provides thread-safe IFC store caching (`IfcStoreCache`) with GlobalId indexing.

## Installation

```bash
dotnet add package GeometryHelper.IfcConvert
```

The package is **self-contained**: it bundles all required customized xBIM managed assemblies and the 64-bit native geometry engine (`Xbim.Geometry.Engine64.dll`).

The library targets `netstandard2.0`, but it runs only in a **.NET Framework 4.8, Windows x64** process: the bundled xBIM geometry engine is C++/CLI built for .NET Framework 4.7.2. A .NET Core / .NET 5+ project can install the package, build against it and open a model, but every geometry conversion fails when the engine loads. Nothing is thrown: the product comes back without solids, and its `Warnings` read `Conversion failed: FileLoadException: Failed to load Xbim.Geometry.Engine64.dll`.

> [!IMPORTANT]
> **Platform Target Requirement (`x64`):**
> Because the underlying geometry engine is a 64-bit native C++/CLI binary, any consuming application **must run as an x64 process**.
> 
> Running under `AnyCPU` with the default "Prefer 32-bit" setting enabled will result in a `BadImageFormatException`:
> ```
> System.BadImageFormatException: Could not load file or assembly 'GeometryHelper.IfcConvert' or one of its dependencies.
> An attempt was made to load a program with an incorrect format.
> ```
> 
> Ensure your consuming project's `.csproj` specifies `<PlatformTarget>x64</PlatformTarget>`:
> ```xml
> <PropertyGroup>
>   <PlatformTarget>x64</PlatformTarget>
> </PropertyGroup>
> ```
> *(Or if you must use `AnyCPU`, set `<Prefer32Bit>false</Prefer32Bit>` to ensure Windows executes it as a 64-bit process).*

## How Winding and Orientations are Handled

SolidGeometry measures volumes and point containment assuming outward-pointing face normals.
Orientation is propagated across shared edges (neighbouring faces must traverse a shared edge in
opposite directions), so bodies with mixed orientations, such as boolean results, are repaired face
by face. Each connected shell is then turned outwards by the sign of its volume. Hole loops are
stored wound like their boundary, as `GeoSolid3` expects.

## Planar vs Curved Geometry

A solid whose faces and edges are all planar and straight is read face by face: outer loops and hole
loops become `GeoPolygon3` rings. A solid with any curved face **or curved edge** (round columns,
tubes, a plate with a bolt hole) is triangulated as a whole by the geometry engine, so shared edges
are subdivided once and the mesh is watertight. Reading it face by face would collapse circular
loops to their vertices and drop the hole.

`IfcConvertOptions.DeflectionTolerance` is the chord tolerance in output units. The default (0) uses
the deflection xBIM derives from the model's length unit, which keeps round sections within about 0.5 %
of their true volume.

## Conversion Rules

- **Body only**: the `Body` representation is converted (then `Body-*`, then `Facetation`). `Box`,
  `Axis`, `Clearance` and similar representations are ignored so they cannot add extra solids.
- **Mapped items**: world = MappingTarget × MappingOrigin × local, the order IfcOpenShell uses.
  (xBIM's own scene builder applies them the other way round, and ISO 10303-43 reads the origin as
  inverted. All three agree when MappingOrigin is the identity, which covers most exports.)
- **Openings** (`ApplyVoids`, off by default for speed): with `ApplyVoids = true`, openings are cut
  from the exact B-rep by the geometry engine, in the host's frame, so they are also correct with
  `CoordinateSpace.Local` and for round openings. If an opening cannot be cut that way, conversion falls
  back to the `GeoSolid3` boolean. Cutting costs time: on a Tekla steel model, the first conversion of
  500 beams (183 of them with bolt holes or cuts) took 1.5 s uncut and 29 s with openings cut. Most of
  those cut beams also came back not closed (a warning says so): their volumes remain plausible, but
  point containment may be off.
- **Model-wide queries** (`GetAllSolids`, `EnumerateSolids`, `EnumerateGeometries`) skip openings,
  spatial elements, annotations, grids, virtual elements, ports and structural analysis items unless
  `IncludeNonPhysicalProducts` is set. Queries by GlobalId or type name are not filtered.
- **Assemblies**: with `IncludeAggregatedParts`, a product also returns the parts it aggregates, for
  example a Tekla `IfcElementAssembly`, which has no body of its own.
- **Units**: property and quantity units come from the value when present, otherwise from the project
  unit for that measure. Tekla, for example, exports lengths in mm but areas in m2 and volumes in m3.
  Properties attached to the product's type are included, and occurrence values override them.
- **Duplicate GlobalIds** are listed in `IfcStoreCache.DuplicateGlobalIds`; only the first product
  with such an id is reachable by GUID.

## Choosing Products by Name

`SkipNames` leaves out the products whose names match, and `OnlyNames` keeps only those. A product left out comes
back empty, and its name is checked before any geometry is built, so it costs next to nothing. Both lists ignore
case and take `*` as a wildcard. A name matching both is skipped, and a product with no name matches only `"*"`
in `OnlyNames`.

```csharp
// Everything but the bolts.
var noBolts = new IfcConvertOptions { SkipNames = { "Bolt assembly" } };

// Only the main members, or only the plates: the names are those the model gives its parts.
var members = new IfcConvertOptions { OnlyNames = { "BEAM", "GIRDER", "PRD_COLUMN", "COLUMN" } };
var plates = new IfcConvertOptions { OnlyNames = { "*PLATE" } };

// AddOnlyNames and AddSkipNames take names one by one or as a list, trim them, ignore blank ones and chain,
// which suits names read from a settings file or a text box.
var fromSettings = new IfcConvertOptions().AddOnlyNames(" BEAM", "GIRDER ", "").AddSkipNames("Bolt assembly");
```

Reading every product of a 115 MB Tekla steel model (21,188 products) took about 17 minutes, 96 % of it on its
5,512 bolts. Leaving the bolts out took 35 s, only the main members 3.5 s, and only the plates 3.8 s.

Both lists also apply to the parts an assembly aggregates (`IncludeAggregatedParts`), so with `OnlyNames`, list
the parts wanted as well as the assembly. Neither applies to the openings that cut a product.

## Conversion Warnings

A representation item that fails is skipped rather than failing the whole product, and every such
problem is recorded in `IfcProductGeometry.Warnings` (`HasWarnings` for a quick check). Examples: an item
that produced no solid, a shape the engine reports as invalid (skipped, because reading it can fault inside
the native engine), faces left out, a solid that is not closed, openings that fell back to the `GeoSolid3`
boolean. Messages about an item start with its STEP entity label, e.g.
`#123 IfcExtrudedAreaSolid: It produced no solid.`, so the item can be found in the IFC file.

```csharp
foreach (IfcProductGeometry g in model.EnumerateGeometries())
{
    foreach (string warning in g.Warnings)
    {
        Console.WriteLine($"{g.GlobalId} {g.IfcType}: {warning}");
    }
}
```

## Moving Converted Geometry

To place a product somewhere else, for example where a Tekla reference model was inserted, use
`IfcProductGeometry.TransformBy` rather than `GeoSolid3.TransformBy` on each body:

```csharp
IfcProductGeometry placed = geometry.TransformBy(transform, options.Tolerance);
```

The conversion builds faces with a finer area threshold than `Tolerance.Global` (`EqualPoint` squared rather than
`EqualVector`), so a triangulated or cut body can carry thin sliver faces that are valid there.
`GeoSolid3.TransformBy` checks every face against `Tolerance.Global` again and throws on the whole body: on a
115 MB Tekla IFC, 3 of 500 beams read with `ApplyVoids = true` were lost that way. `TransformBy` rebuilds the
faces with the tolerance of the conversion, so pass the `Tolerance` of the options the product was read with
(the default is `Tolerance.Global`, as in the options). It returns a copy: bodies, open surfaces and `Placement`
are carried, the GlobalId, name, type, tag and warnings are kept, and the cached geometry is left unchanged. What
the transformation itself collapses, such as a body scaled down to nothing, is left out and added to `Warnings`.

## Quick Start

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;

string[] ifcFiles = { @"C:\Models\Building-A.ifc", @"C:\Models\Building-B.ifc" };
var options = new IfcConvertOptions { TargetUnit = LengthUnit.Meters };

foreach (string ifcFile in ifcFiles)
{
    // 1. Get the model from the process-wide cache (pure GeometryHelper, zero xBIM compile-time dependencies).
    //    The first call for a file parses it; later calls with the same path return the same instance at once,
    //    together with every geometry and property it has already converted.
    //    Pass the full path of the file; a relative path throws ArgumentException.
    IfcStoreCache model = IfcStoreCache.GetOrCreate(ifcFile);

    // 2. Query product metadata catalog (super lightweight, zero 3D conversion cost)
    IReadOnlyList<IfcProductMetadata> catalog = model.GetProductCatalog();
    Console.WriteLine($"{Path.GetFileName(ifcFile)}: {catalog.Count} products, unit {model.OriginalLengthUnit}");

    // 3. Loop over every product: complete geometry information (solids, surfaces, AABB, warnings)
    foreach (IfcProductMetadata product in catalog)
    {
        IfcProductGeometry geom = model.GetGeometry(product.GlobalId, options);
        if (geom == null || geom.IsEmpty)
        {
            continue;
        }

        Console.WriteLine($"Name: {geom.Name}, GUID: {geom.GlobalId}, Type: {geom.IfcType}");
        Console.WriteLine($"  Total Volume: {geom.TotalVolume:F4} m3");
        Console.WriteLine($"  Bounding Box: Min={geom.BoundingBox.Min}, Max={geom.BoundingBox.Max}");

        foreach (GeoSolid3 s in geom.Solids)
        {
            Console.WriteLine($"  Solid Faces: {s.Faces.Count}, Volume: {s.Volume:F4} m3");
        }

        foreach (GeoFace3 surface in geom.OpenSurfaces)
        {
            Console.WriteLine($"  Surface Area: {surface.Area:F4} m2");
        }

        foreach (string warning in geom.Warnings)
        {
            Console.WriteLine($"  Warning: {warning}");
        }
    }

    // 4. Retrieve all solids of a specific IFC type (e.g. IfcWall, IfcBeam, IfcColumn)
    var optionsWithVoids = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, ApplyVoids = true };
    IReadOnlyList<GeoSolid3> wallSolids = model.GetSolidsByType("IfcWall", optionsWithVoids);
    Console.WriteLine($"Loaded {wallSolids.Count} wall solids with voids subtracted.");
}

// 5. Cached models stay in memory until released. Release a model when no code uses it any more.
IfcStoreCache.ClearGlobalCache(ifcFiles[0]);                     // one model
IfcStoreCache.ClearGlobalCache();                                 // every cached model
```

### `GetOrCreate` or `Open`

Measured on a 115 MB Tekla IFC (25 000 products):

| | Time | Memory |
|---|---|---|
| `Open` (every call parses the file again) | about 6 s | released on `Dispose` |
| `GetOrCreate`, first call for a path | about 6 s | about 200 MB kept |
| `GetOrCreate`, later calls for the same path | 0 ms | same instance |
| Geometry of 500 beams, first request / again (the default, openings not cut) | 1.5 s / 1 ms | cached with the model |
| The same with `ApplyVoids = true` (openings cut) | 29 s / 1 ms | cached with the model |

- Use `GetOrCreate` when the same files are read again during the process's life, for example a plugin
  command run several times, or several features querying one model. It does not make the first opening
  faster, so for files each read only once, `Open` inside `using` frees memory as soon as possible.
- `Dispose` does nothing on a cached instance, so wrapping it in `using` does not release it. Call
  `ClearGlobalCache(path)` or `ClearGlobalCache()` once no code uses the model any more; clearing a model
  that another caller is still using leaves that caller with a disposed model.
- `GetOrCreate` and `ClearGlobalCache(path)` take the **full path** of the file (`C:\Models\A.ifc` or
  `\\server\share\A.ifc`) and throw `ArgumentException` for anything relative, including `\Models\A.ifc` and
  `C:A.ifc`: a relative path depends on the current folder, so one file could be cached twice. Different
  spellings of the same full path (`/` or `\`, `.` and `..` segments, letter case) share one cached model.
- The cache does not watch the file. After the file changes on disk, call `ClearGlobalCache(path)` to reload it.
- Opening happens under the cache lock, so threads opening different files wait for each other.
- `IfcStoreCache.GetGlobalCacheInfo()` lists the cached paths and their product counts.

## Using with Tekla Structures

`GeometryHelper.TeklaConvert` reads the objects of IFC reference models through this package: it finds the
IFC file of each reference model, reads it in millimetres at the reference model's scale, and places the
result where Tekla shows it, in the current work plane. Install the `GeometryHelper.TeklaConvert.{year}`
package of your Tekla version, which brings this one, and see its
[IFC reference models](https://github.com/nguyenthanguth/GeometryHelper/tree/main/Libraries/GeometryHelper.TeklaConvert#ifc-reference-models)
section for the details.

The example draws every face of the selected reference objects as control polycurves, outer boundaries in red
and holes in white:

```csharp
using System.Collections.Generic;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

public static class IfcReferenceSample
{
    public static void DrawSelectedReferenceObjects()
    {
        Model model = new Model();

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

        // In the current work plane, which is where control polycurves are drawn too: no work plane to switch.
        GeoSolid3[] solids = selected.ToGeoSolids();

        // Every face as control polycurves (GeometryDraw): outer boundaries red, holes white.
        ControlPolycurve[] drawn = solids.DrawToTekla();

        model.CommitChanges();
        Operation.DisplayPrompt($"{solids.Length} solid(s), {drawn.Length} polycurve(s), from {selected.Count} reference object(s).");
    }
}
```

Notes:
- **GUIDs**: the `EXTERNAL.GUID` report property of a reference object is its IFC GlobalId, the key of every
  `IfcStoreCache` query. `ReferenceModelObject.GetIfcGuid()` reads it, and
  `ReferenceModel.GetReferenceModelObjectByExternalGuid(guid)` goes the other way.
- **Cache lifetime**: a plugin or extension runs inside the Tekla process, so each IFC file is parsed once and
  every later run is served from the cache. A standalone `.exe` is a new process each time, so it parses the
  files on every run.
- **Curved parts** are triangulated, so a round tube draws hundreds of polycurves. Fine for a visual
  check; for anything else, work with the `GeoSolid3` itself.

## Matrix Transformations

xBIM uses row-vector convention ($v \cdot M$) with translations located in the fourth row (`OffsetX`, `OffsetY`, `OffsetZ`).
`GeoTransform3` uses column-vector convention ($M \cdot v$) with translations located in the fourth column.
`MatrixConvert.ToGeoTransform3` and `MatrixConvert.ToXbimMatrix3D` transpose the linear 3x3 block and swap offset rows/columns
accordingly, ensuring mathematically identical coordinate transformations between the two systems.
