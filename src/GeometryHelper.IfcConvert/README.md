# GeometryHelper.IfcConvert

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.IfcConvert.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.IfcConvert/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/GeometryHelper/blob/main/LICENSE)

Converts geometry between IFC models (via xBIM) and GeometryHelper (`GeometryHelper`): points, vectors, transformation
matrices, planar faces, curved surfaces, and 3D solids.

## What It Does

`GeometryHelper.IfcConvert` bridges building information models in IFC (IFC2x3, IFC4, IFC4x3) to pure
computational geometry structures in `GeometryHelper`:
- Converts 3D points (`XbimPoint3D`), vectors (`XbimVector3D`), and homogeneous transformation matrices (`XbimMatrix3D` / `GeoTransform3`).
- Converts IFC boundary representations and swept solids (`IXbimSolid`, `IIfcGeometricRepresentationItem`, `IIfcProduct`, `IIfcMappedItem`) into `GeoSolid3` bodies.
- Handles planar faces with both outer boundaries and inner cutout loops/holes (`InnerBounds`).
- Automatically tessellates curved and non-planar faces into planar triangles, maintaining strict coplanarity compliance for `GeometryHelper`.
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

GeometryHelper measures volumes and point containment assuming outward-pointing face normals.
Orientation is propagated across shared edges (neighbouring faces must traverse a shared edge in
opposite directions), so bodies with mixed orientations, such as boolean results, are repaired face
by face. Each connected shell is then turned outwards by the sign of its volume. Hole loops are
stored wound like their boundary, as `GeoSolid3` expects.

## Planar vs Curved Geometry

A solid whose faces and edges are all planar and straight is read face by face: outer loops and hole
loops become `GeoPolygon3` rings. A face that strays out of flat, or a hole off its face's plane, as
rounding in the file leaves some, comes as triangles on its own corners (`GeoFace3.FromLoops`), which
keep every edge it shares with its neighbours, so the body stays closed and its holes stay open. A solid with any curved face **or curved edge** (round columns,
tubes, a plate with a bolt hole) is triangulated as a whole by the geometry engine, so shared edges
are subdivided once and the mesh is watertight. Reading it face by face would collapse circular
loops to their vertices and drop the hole.

`IfcConvertOptions.DeflectionTolerance` is the chord tolerance in output units. The default (0) uses
the deflection xBIM derives from the model's length unit, which keeps round sections within about 0.5 %
of their true volume.

## Balls

A ball in an IFC file, an `IfcSphere` among the items of a product's body, at the root of an `IfcCsgSolid` among them, or in a mapped item, comes out of
`GetSolids` as the engine's mesh of triangles, which lies inside the sphere: a ball of radius 0.5 m is 1.2 % under its volume at the default
deflection. `IfcStoreCache.GetSpheres(guid, options)` reads the sphere itself as a `GeoSphere3`, its centre the position the file gives and its
radius the radius, scaled by the options and placed by the product as a solid is (the placement is left off with `CoordinateSpace.Local`):

```csharp
IReadOnlyList<GeoSphere3> balls = model.GetSpheres(guid, new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters });
GeoSolid3 mesh = balls[0].ToSolid(0.01);   // a mesh of your own choice of chord tolerance
```

A ball of radius 250 at (1, 2, 3) in a model in millimetres comes with exactly that centre and radius (0.25 at (0.001, 0.002, 0.003) in metres).

A ball in a mapped item is placed by the mapping as a solid is, the scale of its `IfcCartesianTransformationOperator3D` included: scaled by 2, a ball of
radius 0.5 is one of radius 1; one scaled unevenly (`IfcCartesianTransformationOperator3DnonUniform`) is an ellipsoid, which is left out with a Warn.
With `IncludeAggregatedParts` the balls of the parts a product aggregates come too, in the world, or in the frame of the assembly with
`CoordinateSpace.Local`. A sphere inside a Boolean result (a ball with a flat cut off it) is not read, nor is a sphere of no radius; a placement
is rigid, so only a mapping can stretch a ball, and one that stretches it unevenly leaves it out with a Warn. With `ApplyVoids` an element that openings cut gives none of its own balls, a Warn naming
it and saying why: a ball with a void in it is not a ball, and `GetSolids` gives the ball with the void cut. Every call writes one Info line, for a
GlobalId the file does not hold too; a sphere left out is a Warn, and the items of the body that are not spheres are counted in one Debug line.
A product with no ball, or one not found, gives an empty list.

## Splines

A B-spline curve in an IFC file, an `IfcBSplineCurveWithKnots` or an `IfcRationalBSplineCurveWithKnots`, comes out of `IfcStoreCache.GetSplines(guid, options)`
as a `GeoSpline3` with the file's degree, control points, knots and weights. It is read from every representation of the product, the `Axis` of a
member above all: as an item itself, among the elements of an `IfcGeometricSet` or `IfcGeometricCurveSet`, as the parent curve of a segment of an
`IfcCompositeCurve`, as the directrix of an `IfcSweptDiskSolid` (the path of a bar or a pipe), and in a mapped item; a curve met twice in one place, as an
axis and as the directrix of a disk that runs along all of it, is read once. A disk that runs from a `StartParam` to an `EndParam` gives the part of its
directrix between them: the parameter of a B-spline curve is the value of its knots, so the part is the spline cut exactly at those values with
`SplitAtKnot`; a pair outside the curve's domain, or not in order, gives the whole curve with a Warn, and so does a trim of a composite directrix,
whose parameters are the composite's and are not read.

```csharp
IReadOnlyList<GeoSpline3> axes = model.GetSplines(guid, new IfcConvertOptions { TargetUnit = LengthUnit.Millimeters });
GeoPolyline3 drawn = axes[0].Flatten(0.01);   // chords of your own choice of tolerance
```

The file gives each distinct knot once with the number of times it is repeated, and the spline has them repeated: the knots (0, 0.5, 1) repeated
(4, 1, 4) are 0, 0, 0, 0, 0.5, 1, 1, 1, 1. The control points are scaled to the target unit and placed by the product as a solid is (the placement is left
off with `CoordinateSpace.Local`), and by the mapping of a mapped item, an uneven scale of it included, which a NURBS carries exactly; the knots and
the weights are kept as the file gives them. GeometryHelper's splines are clamped, so a curve whose knots run on past its ends, a periodic one, is left
out with a Warn, as is one whose knots and multiplicities do not pair or whose numbers do not make a spline. An `IfcIndexedPolyCurve` holds no B-spline;
a B-spline that is the basis of an `IfcTrimmedCurve` is only a part of itself there, and one in the outline of a profile lies in a frame of the solid it
is swept into, so neither is read; the `IfcBezierCurve` of IFC2X3 gives no knots and is not read either. With `IncludeAggregatedParts` the splines of
the parts a product aggregates come too. Every call writes one Info line; the items with no B-spline in them are counted in one Debug line.

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
- **Model-wide queries** (`GetAllGeometries`, `GetAllSolids`, `EnumerateSolids`, `EnumerateGeometries`) skip
  openings, spatial elements, annotations, grids, virtual elements, ports and structural analysis items unless
  `IncludeNonPhysicalProducts` is set. Queries by GlobalId or type name are not filtered.
- **Every product at once**: `GetAllGeometries` returns the products `EnumerateGeometries` gives, in its order,
  all converted before it returns, and a file xBIM holds in memory (`IsInMemory`) is converted on every core at
  once, each product under the tolerance in force where you call. `GetAllSolids` converts the same way. The
  answers are the ones converting one product at a time gives. Three Tekla models of about 21,000 products each,
  read whole outside Tekla on 24 logical processors, converted in 28 s instead of 85 s, and in 14 s instead of
  80 s with the server garbage collector. `EnumerateGeometries` converts as it is read instead, and holds fewer
  products in memory at once.
- **Bodies in one order**: where the geometry engine cuts one brep into several shells, the bodies come back in
  order of where they lie, low corner first, and their warnings in the order of their text. The engine hands the
  shells back in an order that is not the same from one reading of a file to the next.
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
`GeoSolid3.TransformBy`, given a transformation that stretches or shears, checks every face against
`Tolerance.Global` again and throws on the whole body (a move that keeps every length it carries over as it is): on
a 115 MB Tekla IFC, 3 of 500 beams read with `ApplyVoids = true` were once lost that way. `TransformBy` rebuilds the
faces with the tolerance of the conversion, and keeps a face that lands a rounding off flat as triangles on its own
corners, so pass the `Tolerance` of the options the product was read with
(the default is `Tolerance.Global`, as in the options). It returns a copy: bodies, open surfaces and `Placement`
are carried, the GlobalId, name, type, tag and warnings are kept, and the cached geometry is left unchanged. What
the transformation itself collapses, such as a body scaled down to nothing, is left out and added to `Warnings`.

## Quick Start

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;

string[] ifcFiles = { @"C:\Models\Building-A.ifc", @"C:\Models\Building-B.ifc" };
// Tolerances are in the output unit, and the default suits millimetres: in metres, a thousand times smaller.
var metres = new Tolerance(1E-6, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-6);
var options = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, Tolerance = metres };

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
            Console.WriteLine($"  Solid Faces: {s.Faces.Count}, Volume: {s.GetVolume():F4} m3");
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
    var optionsWithVoids = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, Tolerance = metres, ApplyVoids = true };
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
- Geometry that several products share -- a representation map every bolt of a size maps, a brep several
  beams point at -- is built once per file and settings and placed for each product, unless openings are cut
  from it (`ApplyVoids`). 2,711 products of a 116 MB Tekla model took 7.4 s instead of 3.6 minutes.
- `IsInMemory` says whether xBIM read the file whole into memory or keeps it in a database on disk, as it does
  a 114 MB one. The products of a file in memory can be converted from several threads at once; convert those
  of a file on disk one at a time.
- `IfcStoreCache.GetGlobalCacheInfo()` lists the cached paths and their product counts.

## Using with Tekla Structures

`GeometryHelper.TeklaConvert` reads the objects of IFC reference models through this package: it finds the
IFC file of each reference model, reads it in millimetres at the reference model's scale, and places the
result where Tekla shows it, in the current work plane. Install the `GeometryHelper.TeklaConvert.{year}`
package of your Tekla version, which brings this one, and see its
[IFC reference models](https://github.com/nguyenthanguth/GeometryHelper/tree/main/src/GeometryHelper.TeklaConvert#ifc-reference-models)
section for the details.

The example draws every face of the selected reference objects as control polycurves, outer boundaries in red
and holes in white:

```csharp
using System.Collections.Generic;
using GeometryHelper.Geometry;
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
