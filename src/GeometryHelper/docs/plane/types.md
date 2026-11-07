# Geometric Types

The shapes of the plane and the questions every pair of them answers: collision, crossings, the nearest point, how
deep inside, splitting.

[`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2), [`GeoVector2`](xref:GeometryHelper.Geometry.GeoVector2), [`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2), [`GeoRay2`](xref:GeometryHelper.Geometry.GeoRay2) (a half-line), [`GeoXLine2`](xref:GeometryHelper.Geometry.GeoXLine2) (an infinite line), [`GeoArc2`](xref:GeometryHelper.Geometry.GeoArc2), [`GeoCircle2`](xref:GeometryHelper.Geometry.GeoCircle2), [`GeoEllipse2`](xref:GeometryHelper.Geometry.GeoEllipse2), [`GeoSpline2`](xref:GeometryHelper.Geometry.GeoSpline2) (a NURBS curve), [`GeoRectangle2`](xref:GeometryHelper.Geometry.GeoRectangle2) (rotated rectangle — OBB), [`GeoTriangle2`](xref:GeometryHelper.Geometry.GeoTriangle2), [`GeoPolygon2`](xref:GeometryHelper.Geometry.GeoPolygon2), [`GeoFace2`](xref:GeometryHelper.Geometry.GeoFace2) (a polygon with holes), [`GeoPolyline2`](xref:GeometryHelper.Geometry.GeoPolyline2), and the chains that may curve: [`GeoEdge2`](xref:GeometryHelper.Geometry.GeoEdge2), [`GeoPolylineArc2`](xref:GeometryHelper.Geometry.GeoPolylineArc2), [`GeoPolygonArc2`](xref:GeometryHelper.Geometry.GeoPolygonArc2).

## Regions and curves

The shapes split into two families, and the distinction decides what you can ask of them:

| Family | Types | Encloses an area |
|---|---|---|
| Region | `GeoCircle2`, `GeoEllipse2`, `GeoRectangle2`, `GeoPolygon2`, `GeoFace2` | yes |
| Curve | `GeoLine2`, `GeoRay2`, `GeoXLine2`, `GeoArc2`, `GeoPolyline2`, `GeoPolylineArc2`, `GeoSpline2` | no |
| Curved loop | `GeoPolygonArc2` | yes, but see below |

A `GeoPolyline2` is always an open chain — it has no `IsClosed` flag and never joins its last vertex back to its first. Geometry meant to enclose something is a `GeoPolygon2`, and `polyline.ToPolygon()` converts between them.

That rule is what decides the answers below. A chain of vertices tracing a square still holds only the points on its path:

```csharp
var traced = new GeoPolyline2(
    new GeoPoint2(0, 0), new GeoPoint2(10, 0),
    new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));

traced.Locate(new GeoPoint2(5, 5));            // OutSide  — a curve has no interior
traced.DistanceTo(new GeoPoint2(5, 5));        // 5.0      — measured to the path
traced.ToPolygon().Locate(new GeoPoint2(5, 5)); // Inside  — now it is a region
traced.ToPolygon().DistanceTo(new GeoPoint2(5, 5)); // 0.0
```

Only regions offer `Contains`; every shape offers `Locate`, and curves report `OnSide` or `OutSide`.

A `GeoPolygonArc2` does enclose an area, and gives it exactly, but it answers nothing else about what is
inside it: [flatten it](arcs.md#flattening) and the region operations apply.

A `GeoPolygon2` is not checked for self-intersection when it is built, because the check costs more than the building does. `polygon.IsSimple()` runs it when you want it: no edge crossing or touching another except where neighbours share their vertex. A polygon that is not simple is still read consistently — everything here reads it under the even-odd rule — but its `GetArea` no longer means what it says, since a doubled-back lobe counts against the rest.

## Collision and intersection

`CollidesWith` answers whether two shapes overlap, `GetIntersections` returns the crossing points. Every pair is available from both directions, and each has an overload taking an explicit [`Tolerance`](xref:GeometryHelper.Tolerance):

```csharp
rect.CollidesWith(line);        line.CollidesWith(rect);
rect.CollidesWith(poly);        poly.CollidesWith(rect);
circle.CollidesWith(polyline);  polyline.CollidesWith(circle);
rect.CollidesWith(otherRect);   poly.CollidesWith(otherPoly);   line.CollidesWith(otherLine);

GeoPoint2[] points = poly.GetIntersections(line);
```

Two shapes collide exactly where they come within the point tolerance of each other: where they cross, where
one holds the other, or where their boundaries pass within `EqualPoint`, measured along the arcs. Closed shapes
count as filled and open curves as curves, so the answer is the same whichever way round they are asked and
whatever kind holds the edges.

### `DistanceTo` reads nought only where two shapes truly meet

That is where they cross, touch or overlap, or one holds
the other, a point on a rim counting as held. Everywhere else it is the gap, measured exactly however small, and the
same whichever way round it is asked. A collision is therefore a distance of at most `EqualPoint`, and a gap smaller
than that collides without being nought. Measured, at a tolerance of a thousandth:

* **Straight shapes.** The segment from (40, 100.01) to (60, 100.0005) collides with the square 100 by 100, though it
  ends 0.0005 above it, and is 0.0005 from it; one ending 0.002 above does not collide and is 0.002 from it. Two segments
  whose corners stand 0.00156 apart neither collide nor measure nought.
* **Arcs and circles.** The upper half of the circle of radius 50 about the origin and a segment 0.0005 above its top
  are 0.0005 apart, and so are two arcs whose tops stand 0.0005 apart; two arcs whose ends stand 0.0015 apart are 0.0015
  apart and do not collide, and two arcs of one radius that cross 0.000005 of a radian inside both their ends are nought
  apart. An edge that bows so little that its chord is no further from it than the rounding of the centre route by which
  an arc places its points, a bulge under about 1.6E-8 on an edge near the origin, is read as its chord; the edge from
  (0, 0) to (100, 0) of bulge 1E-7 is measured to its arc, and reads nought from an edge or a segment that shares its end.
  An arc smaller than the tolerance is measured to the point facing a segment that passes near its centre: at a
  hundredth, the half circle of radius 0.004 below the origin is 0.004 from the segment along y = -0.008, where its ends
  are 0.008 off.
* **Chains and loops** are measured on their arcs. The chain from (-60, 0) to (-50, 0) and then by a bulge of 1 to
  (50, 0) is 49.9991 from the point (0, -0.0009), which lies 0.0009 from the centre of its arc.
* **Triangles** are read by their own sides, however thin. The triangle (0, 0), (0, 1), (0.08, 1) is 0.07 from the
  segment along y = 0.5 that ends at x = -0.07, and collides with it at a tolerance of 0.1, though its top is narrower
  than that; the sliver (0, 0), (10, 0), (0, 0.0005) holds the point (1, 0.0001), at distance nought.
* **Ellipses** read the same way, see [Ellipses](curves.md#ellipses), whose near-tangency paragraph says where the answer is only
  exact to about 1E-6. The ellipse 60 by 25 is 0.0005 from a polyline starting 0.0005 off its end.

**Where two shapes truly touch, the distance can read a rounding residue in place of 0**, at most about 1E-13 times the
largest coordinate or radius in the scene (the audit's worst was 4.7E-11, an arc against a flat arc in a scene 150 000
across, 3.2E-16 of its size). Compare a distance with a tolerance, with
`d <= tolerance.EqualPoint` or with `CollidesWith`, and never with `== 0`.

### A segment you give, shorter than the point tolerance, is read as its start

`DistanceTo`, `CollidesWith` and
`GetShortestLineTo` read a `GeoLine2`, or a straight `GeoEdge2`, no longer than `EqualPoint` as the point at its start
where it does not truly meet the other shape, against every shape in the plane and either way round, as space reads it:
at a thousandth, the segment from (5, 0.0015) to (5, 0.0006), pointing at the segment from the origin to (10, 0) and
stopping 0.0006 short of it, is 0.0015 from it, does not collide with it, and is joined to it by the line from (5, 0) to
its start; the one from (5, -0.0015) to (5, -0.0006) is as far from the square 10 by 10 on the origin, and a stub so
aimed at an arc, an ellipse, a spline or a face is as far from it as its start is. Where the stub truly meets the shape
it is in contact however short: one crossing a side, an edge or an arc, or running into a circle, is nought from it and
joined to it by a line of no length where it crosses, and `GetIntersections` and `TryIntersectWith` give that crossing:
the segment from (5, 0.0004) to (5, -0.0005) meets the segment along the x axis at (5, 0). A point is measured to a
stub's start. The sides of a shape, those of a polygon, rectangle, triangle, chain, loop or face, are never read so,
however short, whatever the tolerance: the polygon (0, 0), (0, -50), (0.004, -50), (0.004, 0) is 0.006 from the
segment that runs up from (0.002, 0.006), at a global tolerance of 0.01 as at 0.00001, and not the 0.00632 it is from
the corner at (0, 0). A rectangle 0.0005 wide, thinner than the default tolerance, is measured by its four sides against
an arc, a chain and a loop as it is against a segment.

### What the projections keep

`GetClosestPointOnBoundary` and `GetShortestLineTo` are projections, and keep two older readings, so their segment is
as long as `DistanceTo` except in these cases. Where two shapes come within the tolerance without meeting, the segment
may be one of no length, a touch, and only there: two sides or two arcs that would meet within the tolerance past the
ends of both can be twice as far apart, and are joined by the gap. Two squares 100 by 100 whose corners stand 0.00085
apart in x and in y are joined at a thousandth by the line between the corners, 0.0012 long. A point within the
tolerance of the centre of an arc, which has no direction, is carried to the arc's start, so the segment can be longer
than the distance, by at most twice the point's offset from the centre: from (0, -0.0009) to the arc of radius 50 about
the origin from (-50, 0) to (50, 0) through (0, -50), it runs from (-50, 0) and is 50.00000001 long, where the distance
is 49.9991. A point beside a straight side no longer than the tolerance is joined to the side as it is, of a polygon, a
polyline, a face, a triangle, a rectangle, a curved chain or a loop alike, so the line is as long as the distance: at a
hundredth, the polygon (0, 0), (10, 0), (10, 0.006), (0, 10) is 0.03 from (10.03, 0.003), and its line to that point
runs from (10, 0.003), 0.03 long, where it ran from the side's start (10, 0), 0.0301496 long, as a segment that short on
its own is read. The closest point of a triangle reads its sides so too: the triangle (0, 0), (10, 0), (10, 0.0006) gives
(10.03, 0.0003) the foot (10, 0.0003), where it gave the side's start. `GetDistanceAtPoint` and `GetParameterAtPoint` of
the loop on the same corners put the point 10.003 round it, where they put it 10 round, at the side's start. Those of a
polygon, a polyline, a triangle and a rectangle, which read the global tolerance, measure a side shorter than it as it
is too: the triangle (0, 0), (10, 0), (10, 0.0006) puts (10.003, 0.0003) 10.0003 round, where it put it 10 round, at
the start of the side 0.0006 long, and a polyline with such a leg is cut on it where a point on it or a line crossing
it asks, where it was cut at the leg's start. Against
anything else such a side is the side it is, for
`GetShortestLineTo` as for `GetClosestEdge`: the loop's line to the segment from (10.03, 0.003) to (10.05, 0.003) runs
from (10, 0.003) and is 0.03 long. The closest edge of a polygon, a polyline or a rectangle takes no tolerance: it measures every side exactly, and
reads a segment you give no longer than the global tolerance as its start. `TryIntersectWith` and `GetIntersections`
still accept a crossing up to the tolerance past a segment's end, because they place points for cutting and extending
rather than test for contact; a segment you give no longer than the tolerance has no direction to reach along, and meets
a straight side only where it truly crosses it.
Against a circle or an arc they place a touch exactly where the segment comes within the
tolerance of the rim, as the distance to the rim says, however far its line runs on before it would cross: at a
thousandth, the segment from (0.05, 50.0005) to (10, 50.0005) touches the circle of radius 50 about the origin at its
start, 0.000525 off, and the one from (0.0009, 0.0019) to (1, 0.0019) does not touch the circle of radius 0.001 about the
origin, its start 0.0011 off, though its line passes within the tolerance just behind it. A segment that runs into the
rim and stops within the tolerance of it meets it once, where it crosses; and an arc whose end lies within the tolerance
of a segment touches it there, where `CollidesWith` says they collide, wherever its circle crosses the segment's line.
So the shortest line from a shape with straight sides to a circle or an arc is of no length only where the two come
within the tolerance.

The straight shapes and the curved ones are pairs like any other, in either order and for measuring as
well as meeting:

```csharp
poly.DistanceTo(slot);          slot.DistanceTo(poly);
poly.GetShortestLineTo(arc);    rect.CollidesWith(slot);
rect.GetIntersections(arc);     slot.GetClosestEdge(circle);
```

### A face is material, and a hole is not

A `GeoFace2` is a region with holes in it, so touching one means reaching its **material**. Its boundary is
the outline together with the rim of every hole, and a probe lying wholly inside a hole reaches nothing —
even though it is inside the outline:

```csharp
var outline = new GeoPolygon2(
    new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200));
var cut = new GeoPolygon2(
    new GeoPoint2(80, 80), new GeoPoint2(120, 80), new GeoPoint2(120, 120), new GeoPoint2(80, 120));

var plate = new GeoFace2(outline, new[] { cut });
var inHole = new GeoLine2(new GeoPoint2(90, 90), new GeoPoint2(110, 110));

plate.Boundary.CollidesWith(inHole);   // true  — it is inside the outline
plate.CollidesWith(inHole);            // false — but over a hole, so it touches no material

var right = new GeoLine2(new GeoPoint2(-50, 100), new GeoPoint2(250, 100));

plate.GetIntersections(right).Length;  // 4 — the two sides and the two rims
```

A ring drawn *around* a hole has its centre in that hole and crosses no rim either, and it does reach the
material. The two cases look identical until the rim is asked whether it falls within the probe:

```csharp
plate.CollidesWith(new GeoCircle2(new GeoPoint2(100, 100), 60));   // true  — it encircles the hole
plate.CollidesWith(new GeoCircle2(new GeoPoint2(100, 100), 10));   // false — it lies in the hole
```

The distance reads the face the same way, by its material. It is nought where the two touch, and otherwise the
gap between their boundaries, so a shape in a hole is measured to the hole's rim and one outside to the outline.
The gap is measured exactly: at a tolerance of a thousandth, a triangle whose tip stands 0.0012 off a corner of
the plate is 0.0012 away and does not collide with it, and a circle 0.0005 off is 0.0005 away and does.
`GetShortestLineTo` keeps the older reading of a touch, see [what the projections keep](#what-the-projections-keep).
Two faces collide where their materials meet; a face lying in the other's hole does not:

```csharp
plate.DistanceTo(new GeoCircle2(new GeoPoint2(100, 100), 10));    // 10.0 — in the hole, ten from its rim
plate.DistanceTo(new GeoCircle2(new GeoPoint2(250, 100), 10));    // 40.0 — outside, forty from the outline
plate.DistanceTo(new GeoCircle2(new GeoPoint2(100, 100), 400));   // 0.0  — it holds the whole plate

var insert = new GeoFace2(new GeoPolygon2(
    new GeoPoint2(90, 90), new GeoPoint2(110, 90), new GeoPoint2(110, 110), new GeoPoint2(90, 110)), new GeoPolygon2[0]);

plate.CollidesWith(insert);                 // false — it sits in the hole
plate.DistanceTo(insert);                   // 10.0
plate.GetShortestLineTo(insert);            // from the hole's rim to the insert's side, 10 long
```

### An edge is a segment or an arc

A `GeoEdge2` is a chord with a bulge, so it is one or the other, and it is read as whichever it is — in both
directions, and for every operation the segment and the arc both answer:

```csharp
var bend = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0);   // a half turn

bend.DistanceTo(poly);     poly.DistanceTo(bend);
bend.CollidesWith(rect);   rect.CollidesWith(bend);
bend.GetIntersections(circle);
```

A straight edge answers exactly what its segment answers, and a curved one exactly what its arc answers:

```csharp
var flat = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));

flat.DistanceTo(poly) == flat.ToLine().DistanceTo(poly);   // true
bend.DistanceTo(poly) == bend.ToArc().DistanceTo(poly);    // true
```

## The nearest thing, and the segment to it

Three questions sound alike and are not. Each has its own name, and the return type tells them apart:

| Asking | Answer | Method |
|---|---|---|
| where on me is nearest that point | a point **on this shape** | `GetClosestPointOnBoundary(point)` |
| what joins me to that shape | the segment **between the two** | `GetShortestLineTo(other)` |
| which piece of me faces that thing | an **edge of this shape** | `GetClosestEdge(probe)` |

```csharp
GeoPoint2 on = poly.GetClosestPointOnBoundary(point);   // on the polygon
GeoLine2 across = poly.GetShortestLineTo(circle);       // one end on each shape
GeoLine2 facing = poly.GetClosestEdge(circle);          // one of the polygon's own sides
GeoEdge2 curved = slot.GetClosestEdge(circle);          // a GeoEdge2: the nearest piece may be an arc
```

`GetShortestLineTo` measures **boundary to boundary**, so a shape lying wholly inside a closed one still
reports the gap out to the outline. `DistanceTo` reads a closed shape as a filled region and answers
nothing at all for that same pair. The two disagree there on purpose; everywhere else the segment is
as long as the distance, but for the two cases in [what the projections keep](#what-the-projections-keep).

```csharp
var plate = new GeoRectangle2(0, 0, 100, 100);
var hole = new GeoCircle2(new GeoPoint2(50, 50), 10.0);

plate.DistanceTo(hole);                 // 0.0  — the circle is inside the plate
plate.GetShortestLineTo(hole).Length;   // 40.0 — out to the nearest side
```

A point is a shape like any other here. Every shape in the plane gives the segment to a point, and a point
gives it back the other way round, from itself to the shape:

```csharp
var disc = new GeoCircle2(new GeoPoint2(0, 0), 50.0);

disc.GetShortestLineTo(new GeoPoint2(80, 0));    // (50, 0) to (80, 0), 30 long, as DistanceTo says
disc.GetShortestLineTo(new GeoPoint2(30, 0));    // (50, 0) to (30, 0), 20 long, where DistanceTo says 0
new GeoPoint2(30, 0).GetShortestLineTo(disc);    // the same segment, from the point to the rim
```

`GetClosestEdge` takes a point, a segment, a circle or an arc, and nothing else. The nearest edge of one
many-edged shape to another is really a *pair* of edges, which is a different answer from the one the name
promises, so it is not offered.

That rule is about the **probe**, not about which of the two makes the call. A point can be asked which edge
of a polygon faces it, and gets the same edge the polygon names:

```csharp
var below = new GeoPoint2(50, -50);

below.GetClosestEdge(poly);   // the same side poly.GetClosestEdge(below) returns
```

Every edge is weighed on its own, so a probe inside a closed shape still names the edge nearest it rather
than reporting nothing, and where two edges are equally near the earlier one in the shape's own order wins.

## How deep inside, not just whether

`DistanceTo` reads a closed shape as a filled region, so a point anywhere inside one is nought away and how
far in it sits cannot be got back out of the answer. `SignedDistanceTo` keeps it:

```csharp
var plate = new GeoRectangle2(0, 0, 100, 100);

plate.DistanceTo(new GeoPoint2(50, 50));         // 0.0    — inside, and that is all it says
plate.SignedDistanceTo(new GeoPoint2(50, 50));   // -50.0  — fifty in from the nearest side
plate.SignedDistanceTo(new GeoPoint2(10, 50));   // -10.0
plate.SignedDistanceTo(new GeoPoint2(-20, 50));  //  20.0  — outside
plate.SignedDistanceTo(new GeoPoint2(0, 50));    //   0.0  — on the edge
```

The whole definition is tied to `Locate`, which is what makes it safe to lean on:

| `Locate` answers | `SignedDistanceTo` |
|---|---|
| `Inside` | negative |
| `OnSide` | nought |
| `OutSide` | positive |

So `Math.Abs(shape.SignedDistanceTo(point))` is always the distance out to the outline, and the sign carries
the rest. Inside the tolerance band around the boundary the sign is not worth reading, because the answer
there is nought either way and which side of nought it lands on turns on rounding.

Offered by every shape that encloses an area — `GeoCircle2`, `GeoRectangle2`, `GeoPolygon2`, `GeoFace2` and
`GeoPolygonArc2` — and by a `GeoPoint2`, which asks the same question from the other side:

```csharp
new GeoPoint2(50, 50).SignedDistanceTo(plate);   // -50.0, the same answer plate gives about the point
```

A curved loop is measured on its arcs, and an arc counts only where it actually reaches:

```csharp
var slot = new GeoPolygonArc2(
    new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
    new[] { 0.0, 1.0, 0.0, 0.0 });          // the right-hand side bulges out to x = 150

slot.SignedDistanceTo(new GeoPoint2(130, 50));   // -20.0 — inside the bulge
```

The boundary of a `GeoFace2` is its outline **and** the rim of every hole, so a point in a hole is off the
material and is measured to the rim it sits in rather than out to the outline:

```csharp
var hole = new GeoPolygon2(
    new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60), new GeoPoint2(40, 60));
var pierced = new GeoFace2(plate.ToPolygon(), new[] { hole });

pierced.SignedDistanceTo(new GeoPoint2(50, 50));   //  10.0 — in the hole, ten from its rim
pierced.SignedDistanceTo(new GeoPoint2(25, 50));   // -15.0 — on the material
```

This is what edge distance is, so it is one call rather than three:

```csharp
Math.Abs(plate.SignedDistanceTo(bolt)) >= 40.0 && plate.SignedDistanceTo(bolt) < 0.0;
```

The library names it the way it names `SignedArea` beside `Area`, and `GeoPlane3.SignedDistanceTo` has
carried the same idea for a flat surface all along.

## Splitting

[`Splition2`](xref:GeometryHelper.Core.Splition2) cuts a `GeoLine2` or a `GeoPolyline2` — at a position along it, or wherever a cutter meets it. Pieces come back in order along the subject, so the first piece always holds its start point and the last holds its end point.

Cutting at a position:

```csharp
Splition2.TrySplitBy(line, point, out GeoLine2 first, out GeoLine2 second);
Splition2.TrySplitAtDistance(polyline, 12.5, out GeoPolyline2 head, out GeoPolyline2 tail);

GeoLine2[] pieces = Splition2.SplitAtDistances(line, new[] { 2.0, 5.0, 8.0 });
```

Cutting with another shape. A single cutter that can only meet a segment once fills two pieces; anything that can meet it repeatedly fills an array:

```csharp
Splition2.TrySplitBy(line, cutter, out GeoLine2 first, out GeoLine2 second);
Splition2.TrySplitBy(polyline, cutter, out GeoPolyline2[] pieces);

// Several cutters at once, and points already known to lie on the subject.
Splition2.TrySplitBy(line, new[] { cutterA, cutterB }, out GeoLine2[] byLines);
Splition2.TrySplitBy(polyline, new[] { new GeoPoint2(3, 0) }, out GeoPolyline2[] byPoints);
```

Splitting against a `GeoPolygon2` sorts the result by which side of the boundary each part falls on, and keeps each run whole rather than breaking it into segments:

```csharp
Splition2.TrySplitBy(line,     polygon, out GeoLine2[] inside,     out GeoLine2[] outside);
Splition2.TrySplitBy(polyline, polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns);

// Several polygons behave as their union.
Splition2.TrySplitBy(polyline, new[] { polygonA, polygonB }, out GeoPolyline2[] within, out GeoPolyline2[] beyond);
```

Every split is also reachable from the shape being cut, which is usually how it reads better:

```csharp
line.TrySplitBy(point, out GeoLine2 first, out GeoLine2 second);
line.TrySplitAtDistance(4.0, out first, out second);
line.TrySplitBy(polygon, out GeoLine2[] inside, out GeoLine2[] outside);
GeoLine2[] pieces = line.SplitAtDistances(new[] { 2.0, 5.0, 8.0 });

polyline.TrySplitBy(cutter, out GeoPolyline2[] parts);
polyline.TrySplitBy(polygon, out GeoPolyline2[] insideRuns, out GeoPolyline2[] outsideRuns);
```

The instance methods live on the shape being cut, not on the cutter: `polygon.Split(line)` would leave it unclear which of the two comes back in pieces.

### What the return value means

`false` says nothing was cut, not that the call failed. The out parameters are always usable: an array form hands back the subject as a single piece, and a polygon form puts it in whichever of the two arrays matches the side it lies on, leaving the other empty.

### What gets skipped

Cut positions outside the subject, or landing on one of its endpoints, are not splits. Positions closer together than the tolerance merge into one, and a position within a tolerance of an existing vertex snaps onto it, so no piece and no edge is ever shorter than the tolerance. A point that does not lie on the subject is refused rather than projected onto it — cutting at its projection would be cutting somewhere nobody asked for.

### However short

A segment is measured along as it is however much shorter than the global tolerance, whatever cuts it: at a billionth, the segment from (5, 0.0004) to (5, -0.0005) is cut at (5, 0) by a point there or by a line, a polyline or a polygon crossing it there, where the place was measured at the global thousandth, at the segment's start, and the segment came back whole.

### Against a polygon

A part running along the boundary counts as inside, matching `Contains`. A path that merely touches the boundary and turns back has not crossed it, so it comes back whole instead of split in two at the touch.

## Cutting a region into regions

Everything above cuts a *curve* and hands back curves. A region is cut by the straight line through a
segment, which is what a plane is to space: unbounded, so the segment's length is ignored and a stub in
the middle of a shape cuts all of it. The two sides are **left** and **right** of the segment's own
direction, because above and below mean nothing here.

```csharp
var plate = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200));
var cutter = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 250));   // travelling in +y

plate.TrySplitBy(cutter, out GeoPolygon2[] left, out GeoPolygon2[] right);
// left: the small-x half, right: the other; reversing the cutter swaps them and changes nothing else

// A face is cut holes and all: a hole the line misses stays a hole, one it crosses opens into the outline.
new GeoFace2(plate).TrySplitBy(cutter, out GeoFace2[] nearSide, out GeoFace2[] farSide);

// Or along a chain drawn across the region, which is the bounded cutter: the pieces keep its vertices.
var dogLeg = new GeoPolyline2(new GeoPoint2(0, 100), new GeoPoint2(100, 60), new GeoPoint2(200, 100));
plate.TrySplitBy(dogLeg, out GeoPolygon2[] pieces);
```

Each side is an array because one cut can part a concave region into several pieces — a U cut across its
prongs gives two above and one below. Both ends of a cut line have to sit on the boundary and everything
between them has to stay inside: a chain that leaves and comes back would divide the region into more than
two, so it is refused rather than answered in part. An end that meets the boundary within the tolerance of a corner is
cut from the corner, so both pieces keep it and they add up to the region: at a hundredth, the square 10 by 10 cut from
(0.004, 0) to (10, 5) gives the triangle of 25 square units from (0, 0) and the rest, of 75, where the corner was cut off.
The region and its pieces are taken corner for corner at the tolerance asked, by a line as by a chain, so a side shorter
than the global tolerance stays: at a millionth, the line through (0.0005, 0) and (0, 0.0005) cuts the square 10 by 10 at
the origin into its corner, a triangle of 0.000000125 square units, and the rest, where the global tolerance of a
thousandth read the corner as no polygon and threw.

## See also

- [Extending, offsetting and combining](editing.md).
- [Moving and asking](moving.md).
- [How do I](../howto.md).
