// One L-shaped plate, 60 by 60 with a 40 by 40 notch, broken into faces three ways, side by side in a row: triangles,
// convex pieces and a grid of cells 12 across; then the same plate standing in space, tilted, as a mesh of space.
// A mesh is a flat shape cut into faces that share their corners, so the corner points are drawn once and every shared edge
// is the very same line in two faces.

GeoPoint2[] L(double dx, double dy) => new[]
{
    new GeoPoint2(dx + 0, dy + 0), new GeoPoint2(dx + 60, dy + 0), new GeoPoint2(dx + 60, dy + 20),
    new GeoPoint2(dx + 20, dy + 20), new GeoPoint2(dx + 20, dy + 60), new GeoPoint2(dx + 0, dy + 60),
};

GeoMesh2 triangles = new GeoPolygon2(L(0, 0)).ToMesh(MeshKind.Triangles);
GeoMesh2 convex = new GeoPolygon2(L(80, 0)).ToMesh(MeshKind.Convex);
GeoMesh2 grid = new GeoPolygon2(L(160, 0)).ToMesh(MeshOptions.Grid(12, 12));

Show(triangles, layer: "triangles", name: "triangles", color: "#1F77B4", props: new { faces = triangles.FaceCount });
Show(convex, layer: "convex", name: "convex pieces", color: "#FF7F0E", props: new { faces = convex.FaceCount });
Show(grid, layer: "grid", name: "grid of 12", color: "#2CA02C", props: new { faces = grid.FaceCount });

// The plate in space: lifted to z = 40 and tilted about the X axis by 25 degrees.
double tilt = 25.0 * Math.PI / 180.0;
GeoPoint3 Lift(GeoPoint2 p) => new GeoPoint3(p.X + 240, p.Y * Math.Cos(tilt), 40 + p.Y * Math.Sin(tilt));

GeoMesh3 tilted = new GeoPolygon3(L(0, 0).Select(Lift)).ToMesh(MeshKind.Triangles);
Show(tilted, layer: "in space", name: "tilted plate", color: "#9467BD", props: new { faces = tilted.FaceCount, area = tilted.Area });

Note("faces: " + triangles.FaceCount + " triangles, " + convex.FaceCount + " convex pieces, " + grid.FaceCount + " grid faces, " + tilted.FaceCount + " in space");
