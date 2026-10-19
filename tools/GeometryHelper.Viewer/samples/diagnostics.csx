// One of each fault the diagnostics (Ctrl+D) look for, each on a plate of its own so that they can be run on the whole scene or on one:
//
//   bodies   a box 20 by 20 by 20 with its top left out (4 open edges, red), a box with its first face turned over (4 edges the two faces run the
//            same way, orange), a box with a fin standing on its front top edge (that edge is used by three faces, magenta, and the fin's other
//            three edges are open), and a triangle mesh whose two triangles run their shared edge the same way
//   loops    a bow tie (one crossing, at (1, -59)), a square with a needle spike 20 high and 0.0008 wide at its base, a square with a
//            corner given twice 0.0004 apart, a triangle 30 long and 0.0005 high (a sliver), two arcs that bulge into each other across
//            a rectangle (two crossings), and a face 0.005 off flat (the distance of its farthest corner from the plane through its first, as the library measures it)
//   gap      two squares 0.0034 apart: select both, tick Gap and Run; the gap is drawn 10 000 times larger beside its true number
//   pieces   three squares of 1, 4 and 9 square units on one layer: tick Pieces, and each gets its number, area and colour
//
// Press Ctrl+D, tick the checks, Run (on the selection, or on everything when nothing is selected), then click a finding to zoom to it.
// The shapes that hold a fault the library would put right as it builds them (a vertex 0.0004 from another, a face off flat) are written into the
// scene as the numbers they are, since a scene holds what it is given.

Scene.Layer("bodies", "#1F77B4").Layer("loops", "#2CA02C").Layer("gap", "#8C564B").Layer("pieces", "#7F7F7F");

string R(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
string Pts2(params double[] xy) => "[" + string.Join(",", Enumerable.Range(0, xy.Length / 2).Select(i => "[" + R(xy[2 * i]) + "," + R(xy[2 * i + 1]) + "]")) + "]";
string Pts3(params double[] xyz) => "[" + string.Join(",", Enumerable.Range(0, xyz.Length / 3).Select(i => "[" + R(xyz[3 * i]) + "," + R(xyz[3 * i + 1]) + "," + R(xyz[3 * i + 2]) + "]")) + "]";

void Raw(string kind, string json, string name, string layer, string color = null)
{
    Scene.Scene.Items.Add(new SceneItem { Kind = kind, Name = name, Layer = layer, Color = color, Data = System.Text.Json.JsonDocument.Parse(json).RootElement.Clone() });
}

GeoSolid3 Box(double x, double y)
{
    var floor = new GeoPolygon3(new GeoPoint3(x, y, 0), new GeoPoint3(x + 20, y, 0), new GeoPoint3(x + 20, y + 20, 0), new GeoPoint3(x, y + 20, 0));
    return GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 20));
}

// Bodies.
GeoSolid3 whole = Box(0, 0);
Show(new GeoSolid3(whole.Faces.Where(face => face.Normal.Z < 0.5)), layer: "bodies", name: "open box");

GeoSolid3 second = Box(40, 0);
var turned = second.Faces.ToList();
turned[0] = new GeoFace3(new GeoPolygon3(turned[0].Boundary.Vertices.Reverse()), null);
Show(new GeoSolid3(turned), layer: "bodies", name: "box with a face turned over");

GeoSolid3 third = Box(80, 0);
var fin = new GeoFace3(new GeoPolygon3(new GeoPoint3(80, 0, 20), new GeoPoint3(100, 0, 20), new GeoPoint3(100, 0, 40), new GeoPoint3(80, 0, 40)), null);
Show(new GeoSolid3(third.Faces.Concat(new[] { fin })), layer: "bodies", name: "box with a fin");

Raw("GeoMesh3", "{\"kind\":\"Convex\",\"points\":" + Pts3(120, 0, 0, 140, 0, 0, 140, 20, 0, 120, 20, 0) + ",\"faces\":[[0,1,2],[0,3,2]]}", "mesh with a triangle turned over", "bodies");

// Loops.
Show(new GeoPolygon2(new GeoPoint2(0, -60), new GeoPoint2(2, -58), new GeoPoint2(2, -60), new GeoPoint2(0, -58)), layer: "loops", name: "bow tie");
Raw("GeoPolygon2", "{\"points\":" + Pts2(20, -60, 30, -60, 30, -50, 25.0004, -50, 25, -30, 24.9996, -50, 20, -50) + "}", "needle", "loops");
Raw("GeoPolygon2", "{\"points\":" + Pts2(40, -60, 50, -60, 50, -59.9996, 50, -50, 40, -50) + "}", "doubled corner", "loops");
Raw("GeoTriangle2", "{\"points\":" + Pts2(60, -60, 90, -60, 75, -59.9995) + "}", "sliver", "loops");
Raw("GeoPolygonArc2", "{\"points\":" + Pts2(60, -45, 70, -45, 70, -39, 60, -39) + ",\"bulges\":[-0.8,0,-0.8,0]}", "arcs bulging into each other", "loops");
Raw("GeoFace3", "{\"outer\":" + Pts3(80, -45, 0, 90, -45, 0, 90, -35, 0.01, 80, -35, 0) + ",\"holes\":[]}", "face off flat", "loops");

// The gap.
Raw("GeoPolygon2", "{\"points\":" + Pts2(0, -120, 10, -120, 10, -110, 0, -110) + "}", "gap A", "gap");
Raw("GeoPolygon2", "{\"points\":" + Pts2(10.0034, -120, 20, -120, 20, -110, 10.0034, -110) + "}", "gap B", "gap");

// The pieces.
Raw("GeoPolygon2", "{\"points\":" + Pts2(40, -120, 41, -120, 41, -119, 40, -119) + "}", "piece 1", "pieces");
Raw("GeoPolygon2", "{\"points\":" + Pts2(45, -120, 47, -120, 47, -118, 45, -118) + "}", "piece 2", "pieces");
Raw("GeoPolygon2", "{\"points\":" + Pts2(50, -120, 53, -120, 53, -117, 50, -117) + "}", "piece 3", "pieces");

Note("Ctrl+D opens the diagnostics. Run them on everything, or select one shape first; the gap needs exactly the two squares 'gap A' and 'gap B' selected.");
Note("The bow tie crosses itself at (1, -59). The needle's tip is (25, -30). The doubled corner is (50, -60) and (50, -59.9996).");
