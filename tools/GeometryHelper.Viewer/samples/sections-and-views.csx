// A block 100 by 60 by 40 with a round bore through it along Y, a pipe through the bore, and a marker box 30 across standing out at one corner:
// something to try the clip planes, the work plane and the several views on. Select the marker and press Ctrl+B to clip to a box round it; press K
// in the front view and click for a plane through the bore; press W and click three corners for a work plane of your own.
// The two views at the end are saved in the scene, and the scene opens at the first of them.

var floor = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 60, 0), new GeoPoint3(0, 60, 0));

GeoSolid3 block = GeoSolid3.Extrude(floor, new GeoVector3(0, 0, 40));

// The bore runs from before the front to beyond the back, so it leaves no skin in the faces it passes through; the pipe has 2 of clearance in it.
GeoSolid3 bore = GeoSolid3.Cylinder(new GeoPoint3(50, -5, 20), new GeoPoint3(50, 65, 20), 10, 48);
GeoSolid3 pipe = GeoSolid3.Cylinder(new GeoPoint3(50, -20, 20), new GeoPoint3(50, 80, 20), 8, 48);

var markerFloor = new GeoPolygon3(
    new GeoPoint3(85, 45, 25), new GeoPoint3(115, 45, 25), new GeoPoint3(115, 75, 25), new GeoPoint3(85, 75, 25));
GeoSolid3 marker = GeoSolid3.Extrude(markerFloor, new GeoVector3(0, 0, 30));

Show(block.WithOpenings(new[] { bore }), layer: "block", name: "block with a bore", color: "#7F8C9A");
Show(pipe, layer: "pipe", name: "pipe", color: "#D2691E");
Show(marker, layer: "marker", name: "marker", color: "#2E9E5B");

// The bore seen from the front, 140 across and with no perspective, and the corner seen from a corner.
Scene.View("the bore from the front", new GeoPoint3(50, -300, 20), new GeoPoint3(50, 0, 20), new GeoVector3(0, 0, 1), ortho: true, width: 140);
Scene.View("the corner", new GeoPoint3(190, -60, 130), new GeoPoint3(95, 55, 35), new GeoVector3(0, 0, 1));

Note("block volume " + block.WithOpenings(new[] { bore }).GetVolume().ToString("0.0") + ", pipe volume " + pipe.GetVolume().ToString("0.0"));
