// Writes, with the library's own writers, the files the viewer opens besides scripts and scenes, into the folder "files" beside this script, wherever the
// viewer is started from, and shows the plates it wrote. Open any of them with File > Open, or drop it on the window:
//   plates.obj and plates.dxf   three steel plates, the first with a square hole through it; the second runs into the first and the third lies on it.
//                               The clash check (Ctrl+D, tick Clash, Run) finds one pair that clashes, plate one and plate two, which share 36000 and go 20
//                               deep, and two that touch, plate one and plate three over 5600 and plate two and plate three over 1800
//   drawing.dxf and drawing.svg a plate with a hole, a circle, an arc, an ellipse and a loop with arcs in the plane, on layers
//   shapes.wkt                  the same kinds as well-known text
// The files in samples/files were made by this script.

// The folder of this script: the host gives the script its path, which the compiler hands to a caller-file-path parameter.
string HereFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
string folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(HereFile()), "files");
System.IO.Directory.CreateDirectory(folder);
string At(string name) => System.IO.Path.Combine(folder, name);

GeoSolid3 Plate(double x, double y, double z, double sx, double sy, double sz)
    => GeoSolid3.Extrude(
        new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + sx, y, z), new GeoPoint3(x + sx, y + sy, z), new GeoPoint3(x, y + sy, z)),
        new GeoVector3(0, 0, sz));

// The first plate, 100 by 60 by 20, with a hole 20 by 20 through it; the second, 100 by 60 by 20 too, 70 along; the third on top of the first.
GeoSolid3 hole = Plate(15, 20, -1, 20, 20, 22);
GeoSolid3 first = Plate(0, 0, 0, 100, 60, 20).WithOpenings(new[] { hole });
GeoSolid3 second = Plate(70, 0, 0, 100, 60, 20);
GeoSolid3 third = Plate(0, 0, 20, 100, 60, 20);
Tolerance tol = Tolerance.Default;

new ObjWriter().Add(first, tol, "plate one").Add(second, tol, "plate two").Add(third, tol, "plate three").Save(At("plates.obj"));
new DxfWriter().Add(first, tol, "plate one").Add(second, tol, "plate two").Add(third, tol, "plate three").Save(At("plates.dxf"));

// A drawing in the plane.
var outline = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(120, 0), new GeoPoint2(120, 80), new GeoPoint2(0, 80));
var face = new GeoFace2(outline, new[] { new GeoPolygon2(new GeoPoint2(20, 20), new GeoPoint2(50, 20), new GeoPoint2(50, 50), new GeoPoint2(20, 50)) });
var circle = new GeoCircle2(new GeoPoint2(90, 40), 18.5);
var arc = new GeoArc2(new GeoPoint2(60, -40), 30, 0.4, 2.7);
var oval = new GeoEllipse2(new GeoPoint2(200, 40), new GeoVector2(Math.Cos(0.5), Math.Sin(0.5)), 45, 20);
var loop = new GeoPolygonArc2(new[] { new GeoPoint2(150, -60), new GeoPoint2(230, -60), new GeoPoint2(230, -20), new GeoPoint2(150, -20) }, new[] { 0.0, 0.35, 0.0, -0.2 });
var line = new GeoLine2(new GeoPoint2(-20, -20), new GeoPoint2(260, 100));

new DxfWriter().Add(face, "plate").Add(circle, "marks").Add(arc, "marks").Add(oval, "marks").Add(loop, "cut").Add(line, "construction").Save(At("drawing.dxf"));
new SvgWriter().Add(face, "black", "#dddddd").Add(circle, "#cc0000").Add(arc, "#cc0000").Add(oval, "#0044cc").Add(loop, "#007700").Add(line, "gray").Text(new GeoPoint2(2, 84), "plate with a hole", "black").Save(At("drawing.svg"));

System.IO.File.WriteAllText(
    At("shapes.wkt"),
    string.Join("\n", Wkt.Write(face), Wkt.Write(circle, 0.05), Wkt.Write(arc, 0.05), Wkt.Write(oval, 0.05), Wkt.Write(loop, 0.05), Wkt.Write(line), Wkt.Write(new GeoPoint2(60, 40)), Wkt.Write(first)) + "\n");

Show(first, name: "plate one");
Show(second, name: "plate two");
Show(third, name: "plate three");
Note("written to " + folder);
