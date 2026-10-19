// A loop of six edges, two of them arcs, offset outward by 8 and inward by 5 with each of the three joins.
// Each result is on a layer of its own, so the layers can be switched on and off in the scene tree to compare them.
//
// The round join fills a corner opened by the offset with an arc, the miter extends the two edges to meet in a sharp point
// (up to the miter limit, ten times the distance), and the chamfer cuts the corner straight across at the distance.

var source = new GeoPolygonArc2(
    new[]
    {
        new GeoPoint2(0, 0),
        new GeoPoint2(80, 0),
        new GeoPoint2(100, 30),
        new GeoPoint2(70, 70),
        new GeoPoint2(30, 55),
        new GeoPoint2(10, 70),
    },
    new[] { 0.0, 0.0, 0.5, 0.0, -0.35, 0.0 });

Show(source, layer: "source", name: "source loop", color: "#202020", props: new { vertices = 6, arcs = 2 });

var joins = new (string Name, OffsetOptions Options, string Color)[]
{
    ("Round", new OffsetOptions(OffsetJoin.Round), "#1F77B4"),
    ("Miter", new OffsetOptions(OffsetJoin.Miter), "#D62728"),
    ("Chamfer", new OffsetOptions(OffsetJoin.Chamfer), "#2CA02C"),
};

foreach (var join in joins)
{
    foreach (double distance in new[] { 8.0, -5.0 })
    {
        GeoPolygonArc2[] result = source.Offset(distance, join.Options);
        string layer = (distance > 0 ? "out " : "in ") + join.Name;

        Show(result, layer: layer, name: layer, color: join.Color, props: new { distance, join = join.Name, loops = result.Length });
        Note(layer + ": " + result.Length + " loop(s), " + result.Sum(loop => loop.VertexCount) + " vertices");
    }
}
