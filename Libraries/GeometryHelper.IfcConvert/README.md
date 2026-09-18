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

The package is **self-contained**: it bundles all required customized xBIM managed assemblies and the 64-bit native geometry engine (`Xbim.Geometry.Engine64.dll`). Targets **.NET Framework 4.8, Windows x64** only: the xBIM geometry engine is C++/CLI for .NET Framework, so .NET Core / .NET 5+ projects are stopped at build time.

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
- **Openings** (`ApplyVoids`): openings are cut from the exact B-rep by the geometry engine, in the
  host's frame, so they are also correct with `CoordinateSpace.Local` and for round openings. If an
  opening cannot be cut that way, conversion falls back to the `GeoSolid3` boolean.
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
| Geometry of 500 beams, first request / again | 1.6 s / 1 ms | cached with the model |

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

## Matrix Transformations

xBIM uses row-vector convention ($v \cdot M$) with translations located in the fourth row (`OffsetX`, `OffsetY`, `OffsetZ`).
`GeoTransform3` uses column-vector convention ($M \cdot v$) with translations located in the fourth column.
`MatrixConvert.ToGeoTransform3` and `MatrixConvert.ToXbimMatrix3D` transpose the linear 3x3 block and swap offset rows/columns
accordingly, ensuring mathematically identical coordinate transformations between the two systems.
