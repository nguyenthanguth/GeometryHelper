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

The package is **self-contained**: it bundles all required customized xBIM managed assemblies and the 64-bit native geometry engine (`Xbim.Geometry.Engine64.dll`). Targets Windows x64.

## How Winding and Orientations are Handled

SolidGeometry measures volumes and point containment assuming outward-pointing face normals.
Like `TeklaConvert`, `IfcConvert` verifies the signed volume of every converted solid body:
if `body.GetSignedVolume() < 0.0`, all faces are automatically flipped via `face.Flip()` so that
normals consistently point outwards.

## Planar vs Curved Faces

`GeometryHelper.SolidGeometry.Geometry.GeoPolygon3` enforces strict coplanarity at construction.
When an xBIM face is planar (`face.IsPlanar`), its outer boundary loop and any inner hole loops are
read directly as polygons. When a face is curved (such as a cylindrical pipe or rounded fillet),
`FaceConvert` automatically tessellates the surface into planar triangles (`GeoTriangle3` / `GeoFace3`),
preventing `ArgumentException` and accurately capturing curved geometry.

## Quick Start

```csharp
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Converters;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Ifc4.Interfaces;

// 1. Open or retrieve an IFC model from cache
using (var storeCache = IfcStoreCache.GetOrCreate("sample.ifc"))
{
    // 2. Retrieve a specific product by GlobalId
    IIfcProduct product = storeCache.GetProduct("0123456789ABCDEF012345");

    if (product != null)
    {
        // 3. Convert product geometry to GeoSolid3 bodies in global coordinates
        List<GeoSolid3> solids = product.ToGeoSolids();

        foreach (GeoSolid3 solid in solids)
        {
            Console.WriteLine($"Solid volume: {solid.Volume}");
        }
    }

    // 4. Or retrieve complete geometry information with ToProductGeometry
    IfcProductGeometry geom = product.ToProductGeometry();

    Console.WriteLine($"Product Name: {geom.Name}, GUID: {geom.GlobalId}");
    Console.WriteLine($"Total Volume: {geom.TotalVolume:F2}");
    Console.WriteLine($"Bounding Box: Min={geom.BoundingBox.Min}, Max={geom.BoundingBox.Max}");
    Console.WriteLine($"Placement Origin: {geom.Placement.Transform(GeoPoint3.Origin)}");

    // Closed manifold solid bodies
    foreach (GeoSolid3 solid in geom.Solids)
    {
        Console.WriteLine($"  Solid Faces: {solid.Faces.Count}, Volume: {solid.Volume:F2}");
    }

    // Open surface models or thin sheets (if any)
    foreach (GeoFace3 surface in geom.OpenSurfaces)
    {
        Console.WriteLine($"  Surface Area: {surface.Area:F2}");
    }

    // 5. Subtracting openings/voids (doors, windows) automatically
    var optionsWithVoids = new IfcConvertOptions { ApplyVoids = true };
    var allWalls = storeCache.GetProducts<IIfcWall>();
    foreach (var wall in allWalls)
    {
        // Solids will have window and door cutouts subtracted
        var wallSolids = wall.ToGeoSolids(optionsWithVoids);
    }
}
```

## Matrix Transformations

xBIM uses row-vector convention ($v \cdot M$) with translations located in the fourth row (`OffsetX`, `OffsetY`, `OffsetZ`).
`GeoTransform3` uses column-vector convention ($M \cdot v$) with translations located in the fourth column.
`MatrixConvert.ToGeoTransform3` and `MatrixConvert.ToXbimMatrix3D` transpose the linear 3x3 block and swap offset rows/columns
accordingly, ensuring mathematically identical coordinate transformations between the two systems.
