// A plate 200 by 120 with its top right corner rounded by a quarter circle of radius 20, a round hole of radius 20, two crossing lines,
// an arc and a label: a scene to try the snaps (F8 to F12), the measuring (F2 distance, F3 angle, F4 radius), box select (drag) and the
// rotation centre (V, then a click) on.
//
// Things to try: snap to the centre of the hole and to the middle of the arc and click both for a distance; click the crossing of the two
// lines (intersection), then the end of the arc and the centre of the hole, for an angle; F4 and a click on the rounded corner gives its
// radius, 20; select the plate and the hole with Shift+click and press F2 for what the library says of the two.

var plate = new GeoPolygonArc2(
    new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 100), new GeoPoint2(180, 120), new GeoPoint2(0, 120) },
    new[] { 0.0, 0.0, Math.Tan(Math.PI / 8.0), 0.0, 0.0 });

Show(plate, layer: "plate", name: "plate with a rounded corner", color: "#1F77B4");
Show(new GeoCircle2(new GeoPoint2(60, 60), 20), layer: "plate", name: "hole", color: "#1F77B4");
Show(new GeoLine2(new GeoPoint2(20, 20), new GeoPoint2(180, 100)), layer: "lines", name: "diagonal up", color: "#D62728");
Show(new GeoLine2(new GeoPoint2(20, 100), new GeoPoint2(180, 20)), layer: "lines", name: "diagonal down", color: "#D62728");
Show(new GeoArc2(new GeoPoint2(150, 60), 30, -0.8, 0.8), layer: "lines", name: "arc", color: "#2CA02C");
Scene.Text(new GeoPoint3(100, -14, 0), "plate 200 x 120", 8.0, layer: "notes");

Note("The plate is 200 by 120; the rounded corner has radius 20 and the hole radius 20, centred at (60, 60).");
Note("The diagonals cross at (100, 60).");
