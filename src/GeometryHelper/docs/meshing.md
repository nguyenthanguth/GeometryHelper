# Meshing

`GeometryHelper.Meshing` breaks shapes into smaller pieces: a closed shape of the plane or a flat shape of space into
triangles, the cells of a grid, strips or convex pieces, and a body into the cells of a grid.

| Page | What it covers |
|---|---|
| [Meshing the plane](mesh.md) | polygons, faces with holes, loops with arcs, rectangles, circles and ellipses meshed by `ToMesh` into a [`GeoMesh2`](xref:GeometryHelper.Meshing.GeoMesh2): the four kinds, the grid, the mesh and what is promised |
| [Meshing in space](mesh3.md) | flat shapes standing anywhere in space meshed into a [`GeoMesh3`](xref:GeometryHelper.Meshing.GeoMesh3), and bodies and boxes cut into a [`GeoCellGrid3`](xref:GeometryHelper.Meshing.GeoCellGrid3) of blocks, bays or lifts |

How meshing in space was checked, case by case, is in [Meshing in space: cases and checks](mesh3-report.md).

## See also

- [Extending, offsetting and combining](plane/editing.md#triangles-and-meshes): triangles of the plane without a mesh.
- [Checks and reports](checks.md).
