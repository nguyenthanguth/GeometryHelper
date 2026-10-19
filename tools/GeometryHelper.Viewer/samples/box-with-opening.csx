// A box 100 by 60 by 40 with a round hole 24 across through its middle and a pocket 30 by 20 cut from its top.
// The hole and the pocket are openings of the body: solids subtracted from it, not holes in its faces.
// What the viewer draws is the material left, with the openings cut in; the openings themselves show as lines.

var floor = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));

GeoSolid3 box = GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 40));

// The hole runs from below the bottom to above the top, so it leaves no skin in the faces it passes through.
GeoSolid3 hole = GeoSolid3.Cylinder(new GeoPoint3(35, 30, -5), new GeoPoint3(35, 30, 45), 12, 32);

// The pocket starts 15 below the top and reaches past it.
var pocketFloor = new GeoPolygon3(
    new GeoPoint3(60, 20, 25), new GeoPoint3(90, 20, 25), new GeoPoint3(90, 40, 25), new GeoPoint3(60, 40, 25));
GeoSolid3 pocket = GeoSolid3.Extrude(pocketFloor, new GeoVector3(0, 0, 20));

GeoSolid3 part = box.WithOpenings(new[] { hole, pocket });

Show(part, layer: "part", name: "box with hole and pocket", color: "#1F77B4", props: new { width = 100, depth = 60, height = 40 });
Scene.Text(new GeoPoint3(50, 30, 75), "volume " + part.GetVolume().ToString("0.0"), 5.0, layer: "notes");

Note("volume of the material " + part.GetVolume().ToString("0.0") + " (the box alone is 240000)");
Note("closed: " + part.IsClosed());
