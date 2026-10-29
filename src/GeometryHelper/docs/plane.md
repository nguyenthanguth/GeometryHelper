# Geometry in the plane

2D geometry for engineering drawings: points, vectors, lines, polylines, polygons, faces with holes,
circles, ellipses and oriented rectangles, with distance, projection, containment, intersection, collision,
parallelism, parametrization, merging and splitting over them, and the drafting operations on top:
extending and trimming lines, offsetting curves and regions, and combining regions. Every comparison
is tolerance-aware.

[Geometry in space](solid.md) is the 3D counterpart, built to the same design, and the two are
measured with the same [shared types](common.md).

## Namespaces

| Namespace | Holds |
|---|---|
| `GeometryHelper.Geometry` | `GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoRay2`, `GeoXLine2`, `GeoPolyline2`, `GeoPolygon2`, `GeoFace2`, `GeoCircle2`, `GeoEllipse2`, `GeoSpline2`, `GeoRectangle2`, `GeoTriangle2`, `GeoTransform2` |
| `GeometryHelper.Core` | `Boolean2`, `Collision2`, `Containment2`, `Corner2`, `Distance2`, `Ellipse2`, `Intersection2`, `Lengthen2`, `Merge2`, `Offset2`, `Parallel2`, `Parametrization2`, `Projection2`, `Splition2`, `Triangle2`, `PlanarMap` |
| `GeometryHelper.Meshing` | `GeoMesh2`, `Mesh2`, `MeshKind`, `MeshOptions`, `GridAlignment`: see [meshing the plane](mesh.md) |
| `GeometryHelper.Extension` | `EnumerableExtension` |
| `GeometryHelper` | `Tolerance`, `Angle`, `OffsetOptions`, `GeometryHelperLog` |
| `GeometryHelper.Enums` | `PointLocation`, `LineSide`, `LineEnd`, `LineExtension`, `OffsetJoin` |

Every type carries a `2`, matching the `3` on the solid types, so a program working in both dimensions
imports one namespace and nothing collides.

## Geometric Types

`GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoRay2` (a half-line), `GeoXLine2` (an infinite line), `GeoArc2`, `GeoCircle2`, `GeoEllipse2`, `GeoSpline2` (a NURBS curve), `GeoRectangle2` (rotated rectangle — OBB), `GeoTriangle2`, `GeoPolygon2`, `GeoFace2` (a polygon with holes), `GeoPolyline2`, and the chains that may curve: `GeoEdge2`, `GeoPolylineArc2`, `GeoPolygonArc2`.

### Regions and curves

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
inside it: [flatten it](#flattening) and the region operations apply.

A `GeoPolygon2` is not checked for self-intersection when it is built, because the check costs more than the building does. `polygon.IsSimple()` runs it when you want it: no edge crossing or touching another except where neighbours share their vertex. A polygon that is not simple is still read consistently — everything here reads it under the even-odd rule — but its `GetArea` no longer means what it says, since a doubled-back lobe counts against the rest.

### Collision and intersection

`CollidesWith` answers whether two shapes overlap, `GetIntersections` returns the crossing points. Every pair is available from both directions, and each has an overload taking an explicit `Tolerance`:

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

**`DistanceTo` reads nought only where two shapes truly meet.** That is where they cross, touch or overlap, or one holds
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
* **Chains and loops** are measured on their arcs. The chain from (-60, 0) to (-50, 0) and then by a bulge of 1 to
  (50, 0) is 49.9991 from the point (0, -0.0009), which lies 0.0009 from the centre of its arc.
* **Triangles** are read by their own sides, however thin. The triangle (0, 0), (0, 1), (0.08, 1) is 0.07 from the
  segment along y = 0.5 that ends at x = -0.07, and collides with it at a tolerance of 0.1, though its top is narrower
  than that; the sliver (0, 0), (10, 0), (0, 0.0005) holds the point (1, 0.0001), at distance nought.
* **Ellipses** read the same way, see [Ellipses](#ellipses), whose near-tangency paragraph says where the answer is only
  exact to about 1E-6. The ellipse 60 by 25 is 0.0005 from a polyline starting 0.0005 off its end.

**Where two shapes truly touch, the distance can read a rounding residue in place of 0**, at most about 1E-13 times the
largest coordinate or radius in the scene (the audit's worst was 4.7E-11, an arc against a flat arc in a scene 150 000
across, 3.2E-16 of its size). Compare a distance with a tolerance, with
`d <= tolerance.EqualPoint` or with `CollidesWith`, and never with `== 0`.

**A segment you give, shorter than the point tolerance, is read as its start.** `DistanceTo` and the collisions that rest on
it read a `GeoLine2`, or a straight `GeoEdge2`, no longer than `EqualPoint` as the point at its start when it does not meet
the other shape: against a segment, a ray, a polyline, a polygon, a rectangle, a triangle, a loop, a chain, an arc and a
circle. A stub that crosses a side of a polygon, a rectangle or a polyline, an edge of a chain or a loop, or an arc, is
nought from it, as any crossing is; against a segment, a ray, a circle or a straight `GeoEdge2` it stays its start; a face
measures it as it is. The sides of a shape, those of a polygon, rectangle, triangle, chain, loop or face, are never read
so, however short, whatever the tolerance: the polygon (0, 0), (0, -50), (0.004, -50), (0.004, 0) is 0.006 from the
segment that runs up from (0.002, 0.006), at a global tolerance of 0.01 as at 0.00001, and not the 0.00632 it is from
the corner at (0, 0). A rectangle 0.0005 wide, thinner than the default tolerance, is measured by its four sides against
an arc, a chain and a loop as it is against a segment.

#### What the projections keep

`GetClosestPointOnBoundary` and `GetShortestLineTo` are projections, and keep two older readings, so their segment is as
long as `DistanceTo` except in these cases. Where two shapes come within the tolerance without meeting, the segment may
be one of no length, a touch. And a point within the tolerance of the centre of an arc, which has no direction, is carried to
the arc's start, so the segment can be longer than the distance, by at most twice the point's offset from the centre:
from (0, -0.0009) to the arc of radius 50 about the origin from (-50, 0) to (50, 0) through (0, -50), it runs from
(-50, 0) and is 50.00000001 long, where the distance is 49.9991. `TryIntersectWith` and `GetIntersections` still accept a
crossing up to the tolerance past a segment's end, because they place points for cutting and extending rather than test
for contact.

The straight shapes and the curved ones are pairs like any other, in either order and for measuring as
well as meeting:

```csharp
poly.DistanceTo(slot);          slot.DistanceTo(poly);
poly.GetShortestLineTo(arc);    rect.CollidesWith(slot);
rect.GetIntersections(arc);     slot.GetClosestEdge(circle);
```

#### A face is material, and a hole is not

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

#### An edge is a segment or an arc

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

### The nearest thing, and the segment to it

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

### How deep inside, not just whether

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

### Splitting

`Splition2` cuts a `GeoLine2` or a `GeoPolyline2` — at a position along it, or wherever a cutter meets it. Pieces come back in order along the subject, so the first piece always holds its start point and the last holds its end point.

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

**What the return value means.** `false` says nothing was cut, not that the call failed. The out parameters are always usable: an array form hands back the subject as a single piece, and a polygon form puts it in whichever of the two arrays matches the side it lies on, leaving the other empty.

**What gets skipped.** Cut positions outside the subject, or landing on one of its endpoints, are not splits. Positions closer together than the tolerance merge into one, and a position within a tolerance of an existing vertex snaps onto it, so no piece and no edge is ever shorter than the tolerance. A point that does not lie on the subject is refused rather than projected onto it — cutting at its projection would be cutting somewhere nobody asked for.

**Against a polygon.** A part running along the boundary counts as inside, matching `Contains`. A path that merely touches the boundary and turns back has not crossed it, so it comes back whole instead of split in two at the touch.

### Cutting a region into regions

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
two, so it is refused rather than answered in part.

## Extending and trimming

`Lengthen2` changes how long a segment is while keeping it on its own line, as AutoCAD's LENGTHEN, EXTEND and TRIM do. A segment has no picked point to say which end is meant, so each method takes a `LineEnd`; the other end never moves, and the segment never turns round.

```csharp
var beam = new GeoLine2(0, 0, 10, 0);

beam.Extend(5.0, LineEnd.End);           // (0,0) -> (15,0); a negative distance shortens
beam.Extend(2.0, 3.0);                   // both ends at once: (-2,0) -> (13,0)
beam.ExtendToLength(8.0, LineEnd.End);   // (0,0) -> (8,0), LENGTHEN Total

var wall = new GeoLine2(20, -5, 20, 5);
beam.TryExtendTo(wall, LineEnd.End, out GeoLine2 reached);                  // (0,0) -> (20,0)
new GeoLine2(0, 0, 30, 0).TryTrimTo(wall, LineEnd.End, out GeoLine2 cut);   // (0,0) -> (20,0)

// FILLET with a radius of zero: each segment keeps its longer part and ends at the corner.
new GeoLine2(0, 0, 8, 0).TryTrimExtendToCorner(new GeoLine2(10, 2, 10, 10), out GeoLine2 a, out GeoLine2 b);
// a: (0,0) -> (10,0)   b: (10,0) -> (10,10)

// Where two segments would cross if drawn long enough, as AutoCAD's Intersect.ExtendBoth.
new GeoLine2(0, 0, 1, 1).TryIntersectWith(new GeoLine2(10, 0, 11, -1), LineExtension.Both, out GeoPoint2 x); // (5, 5)
```

`TryExtendTo` only ever lengthens: the end runs outward to the nearest place the line meets the boundary, which can be a point (the foot of the perpendicular), a segment, a polyline, a polygon, a circle or a rectangle. `TryTrimTo` only ever shortens, back to the nearest crossing still within the segment. An end already on the boundary, within tolerance, satisfies both and is left there, so `line.TryExtendTo(b, end, out fit) || line.TryTrimTo(b, end, out fit)` fits an end to a boundary whichever side of it the end starts on. A boundary segment counts only where it is drawn, as with AutoCAD's default EDGEMODE; to reach the line carrying it, intersect with `LineExtension.Both` and extend to that point.

**Arcs, chains and edges.** A segment is not the only thing that can be lengthened. An arc is lengthened
**along itself** — the centre and the radius stay, the sweep grows, and the distance asked for is arc
length and not chord, which is the only reading that leaves the curve where it was. A chain is lengthened
by its **end leg**, along that leg, so every other leg and bend survives.

```csharp
var arc = new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI / 2.0);

arc.Extend(50.0, LineEnd.End);              // fifty more of curve, same centre and radius
arc.ExtendToLength(200.0, LineEnd.Start);   // two hundred of curve, end where it was
arc.TryExtendTo(new GeoPoint2(0, -100), LineEnd.End, out GeoArc2 round);   // to a point on its own circle
arc.TryTrimTo(arc.GetPointAtParameter(0.5), LineEnd.End, out GeoArc2 half);

var bar = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(400, 0), new GeoPoint2(400, 200));

bar.Extend(300.0, LineEnd.End);             // the last leg carries on; the bend does not move
bar.ExtendToLength(1000.0, LineEnd.Start);  // a thousand long, measured along it
bar.TryTrimTo(new GeoPoint2(200, 0), LineEnd.End, out GeoPolyline2 back);

new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)).Extend(50.0, LineEnd.End);   // straight on
```

A whole turn is the limit for an arc, so an extension that would pass it is refused rather than wrapped,
and so is one that would leave no arc or turn it back the other way. **A chain is carried outwards only**:
shortening one belongs to the splitting family, which keeps its bends, and `TryTrimTo` is that cut with the
end named rather than the piece. An edge carries on straight or carries on round, keeping its radius, and
hands back an edge either way.

## Offsetting

`Offset2` moves curves sideways and grows or shrinks regions, as AutoCAD's OFFSET does.

```csharp
// A curve goes to the left of its direction for a positive distance, to the right for a negative one.
new GeoLine2(0, 0, 10, 0).Offset(2.0);                          // (0,2) -> (10,2)
new GeoLine2(0, 0, 10, 0).OffsetThrough(new GeoPoint2(3, 7));   // (0,7) -> (10,7)

var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
chain.Offset(-1.0);   // one polyline: (0,-1) -> (11,-1) -> (11,10)

// A region grows for a positive distance and shrinks for a negative one, whichever way it runs.
var square = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
square.Offset(2.0);                       // one square, 14 x 14, sharp corners
square.Offset(2.0, OffsetJoin.Round);     // corners rounded to a radius of 2
square.Offset(2.0, OffsetJoin.Chamfer);   // corners cut square to the corner's bisector, 2 out
square.Offset(-5.0);                      // none: it shrinks away
```

The corners that open up on the outside of a turn are closed by the join, the three kinds AutoCAD's OFFSETGAPTYPE offers:

| `OffsetJoin` | Corner | AutoCAD |
|---|---|---|
| `Miter` (default) | the offset edges carried on until they meet | Extend |
| `Round` | an arc about the original vertex, drawn as chords | Fillet |
| `Chamfer` | one segment, square to the corner's bisector at the offset distance | Chamfer |

`OffsetOptions` sets the rest: `MiterLimit` (default 10) cuts a sharp corner off square once it would reach that many distances from its vertex, and `ArcTolerance` bounds the gap between a round corner's chords and the true arc (by default 0.2 % of the distance).

**What comes back.** The offset of a region is exact, not the edges moved one by one, so a polygon does not always give one polygon back. `polygon.Offset` returns the boundary loops of the result: a shrinking polygon that pinches off gives one per piece, one that shrinks past its own width gives none, and a growing polygon that closes a gap around empty space gives that space as a hole, wound the other way so that signed areas add up. `face.Offset` on a `GeoFace2` returns faces with their holes attached, and its holes shrink as the boundary grows. A polyline's parallel curve has the loops a tight turn would make cut away, and comes back in pieces when a turn cuts it clean through. A `GeoPolyline2` gives the edge of the band it sweeps sideways, so a strip that crosses another leg of the polyline stays; a curved chain, offset as a `GeoPolylineArc2`, keeps only what stands exactly the distance from it. At a turn on the inside sharper than a right angle, the stretch of a moved segment that runs on past the other leg goes with the loop. Where the band's edge lies along another part of the polyline, within the tolerance, it is the polyline itself and goes too: the step (0, 0), (10, 0), (10, 1), (−5, 1) offset by 1 gives only (0, 0) to (−5, 0). Pieces come in order by where most of each lies. `GeoCircle2` and `GeoRectangle2` offer `TryOffset`, which keeps them what they are.

## Combining regions

`Boolean2` combines regions, as AutoCAD's UNION, INTERSECT and SUBTRACT do. A polygon is read under the even-odd rule, as `Contains` reads it, and a face as its boundary less its holes. The answer is always a set of faces, largest first, with boundaries counter-clockwise and holes clockwise.

```csharp
var a = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
var b = new GeoPolygon2(new GeoPoint2(5, 5), new GeoPoint2(15, 5), new GeoPoint2(15, 15), new GeoPoint2(5, 15));

a.Union(b);       // one face, area 175
a.Intersect(b);   // one face, area 25
a.Subtract(b);    // one L-shaped face, area 75
a.Xor(b);         // two faces, 150 in all

// A slab with its openings cut in one call: an opening inside becomes a hole, one across the edge a notch.
GeoFace2[] slab = Boolean2.Subtract(outline, openings);
GeoFace2[] floor = Boolean2.Union(tiles);
```

Region operations, the booleans and the offsets alike, are resolved by [Clipper2](https://github.com/AngusJohnson/Clipper2), which this package carries compiled in. It works on integers, so an answer never depends on rounding luck; the library lays the shapes out in a frame at the first of them before rounding, and gives every vertex of the answer its full precision back afterwards, so a square offset by 2 ends exactly on 2. Vertices closer than the point tolerance are merged, and slivers thinner than it are dropped. Many corners on one line, the points of a zigzag or the teeth of a comb, cost no more than as many anywhere else: a zigzag of 100 000 vertices is united in 0.11 seconds and a comb of 25 000 teeth in 0.09, where Clipper2 as it comes takes 5.9 and 7.4, with the same answer to the point. Minima at many heights cost more, since every scanbeam walks every edge, but no longer a sort of the edges where none cross between them: a zigzag of 400 000 vertices with its minima at 50 heights and its maxima on one line is united in 1.6 seconds, where it took 5.8, three times its 0.5 with its minima on one line, and with its maxima at 30 heights as well in 7.9, where it took 15.8. Giving the answer its digits back reads only the input edges and vertices near each point, so it no longer grows with the square of the edges: a comb of 40 000 teeth pointing down from its back is offset by a half in 0.8 seconds, where it took 4.4. With its teeth pointing up it still takes half a minute, in Clipper2's own union of it. Long strips laid on a slant, each end crossing the long sides of a thousand others, likewise stay slow in that union: 8 000 of them take about 20 seconds, 16.5 of them in Clipper2.

## Triangles and meshes

`GeoTriangle2` is the triangle of the plane, as `GeoTriangle3` is the triangle of space. It has its signed area and
winding, perimeter, centroid and angles, its circumcircle and incircle, barycentric coordinates, and where a point is.
It stands in the matrix of the plane as `GeoRectangle2` does, every shape asking it the questions it asks any other, and
answers them as the polygon of its corners does, by its own sides however short or thin: only a triangle whose corners
coincide exactly answers as the segment it is.

```csharp
var t = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(0, 3));

double area = t.Area;                                   // 6
PointLocation where = t.Locate(new GeoPoint2(1, 1));    // Inside
double off = t.DistanceTo(new GeoPoint2(4, 3));         // 2.4, to the long side
t.TryGetCircumcircle(out GeoCircle2 circle);            // centre (2, 1.5), radius 2.5
```

`TriangulateSurface` on `GeoPolygon2`, `GeoFace2`, `GeoPolygonArc2`, `GeoRectangle2` and `GeoCircle2` breaks the shape
into triangles that lie within its material, holes left open, all counter-clockwise. `ToMesh` breaks it into the faces
of a `GeoMesh2` instead: triangles, the cells of a grid as formwork panels or tiles are laid, strips, or convex pieces.
[Meshing the plane](mesh.md) tells how.

## Cutting corners

`Corner2` chamfers a corner: one straight cut across it, measured back along each of the two edges that
meet there, as AutoCAD's CHAMFER does. Cutting a corner square adds no curvature, so what comes back is
the kind of shape that went in — a polygon gives a polygon, a chain gives a chain, and the result goes
straight on into the region operations with nothing to convert.

```csharp
plate.Chamfer(15.0);              // every corner, the same distance along both edges
plate.Chamfer(30.0, 5.0);         // unequal: along the edge coming in, then the edge going out
plate.TryChamferAt(1, 10.0, 20.0, out GeoPolygon2 cut);   // one corner, precisely

chain.Chamfer(10.0);              // a chain keeps both of its end points exactly where they were
```

A corner that was cut becomes two vertices, so a rectangle chamfered at every corner comes back an
octagon. The two distances follow the way the shape runs, so reversing a polygon swaps them. Rounding a
corner into an arc instead is [Fillet](#rounding-corners), which needs a shape that can hold one.

**Corners that are left alone.** A cut has to fit on the edges it is measured along, and neighbouring
corners eat into the edge between them:

| Left alone when | Why |
|---|---|
| the cut is longer than an edge beside it | there is nothing to measure along |
| two neighbours together ask for more than the shared edge is long | the one taking more of that edge is dropped, which may leave room for the rest |
| the corner is straighter than `Tolerance.EqualAngleRad` | the cut would be an edge of zero length |

What was skipped, and why, is written to `GeometryHelperLog`. `TryChamferAt` reports `false` instead, for
a caller who needs to know about one corner in particular. Every cut is measured on the shape as it came
in, never on a shape half cut already, so the answer does not depend on which vertex the walk began at.

**Rounding them instead.** A straight chain or a straight polygon can be rounded as well as chamfered. A
rounded corner is an arc, so the answer is the curved type — `GeoPolylineArc2` for a chain,
`GeoPolygonArc2` for a loop — and the rules for which corners are left alone are the chamfer's.

```csharp
plate.Fillet(30.0);                     // GeoPolygonArc2: every corner rounded by thirty
plate.Fillet(new[] { 20.0, 0.0, 20.0, 0.0 });   // a radius each; nought leaves that corner square
chain.TryFilletAt(1, 50.0, out GeoPolylineArc2 rounded);
```

## Arcs

`GeoArc2` is a piece of a circle: a centre, a radius, the angle it starts at and the angle it sweeps. The
sweep is signed, so the arc knows which way round it goes, and it is what tells a half turn from the rest
of the circle left behind.

```csharp
var arc = new GeoArc2(new GeoPoint2(0, 0), 50.0, 0.0, Math.PI / 2);   // a quarter turn, counter-clockwise
var back = new GeoArc2(new GeoPoint2(0, 0), 50.0, 0.0, Math.PI / 2, clockwise: true);  // the other three quarters

arc.Length;            // 78.54, measured along the curve
arc.EndAngle;          // 1.5708
arc.MidPoint;          // on the curve, halfway round
arc.GetChord();        // the straight segment between its two ends
arc.GetCircle();       // the whole circle it was cut from
```

Two factories build one from what a drawing usually gives you instead:

```csharp
GeoArc2 through = GeoArc2.FromThreePoints(start, somewhereOnIt, end);
GeoArc2 fromCad = GeoArc2.FromBulge(start, end, 0.5);
```

The **bulge** is the tangent of a quarter of the swept angle, the number AutoCAD stores against each
vertex of a polyline: zero is straight, `1` is a half turn counter-clockwise, and the sign says which way
it goes. It is a property of an arc here, not a type of its own — `arc.Bulge` reads it back, and the round
trip through a drawing is exact. The centre stands off the middle of the chord by c (1 - b²) / 4b, for a chord c
and a bulge b, and the radius is c (1 + b²) / 4|b|. Past a bulge of a half both are worked out so, as they stand,
where reading the centre from the radius cancels: the chord of 200 000 with a bulge of 1 - 1E-8 has its centre 0.001
off the chord, where it was put on the chord and the end of the arc 0.002 off. Up to a half the centre is read from
the radius, which keeps the ends of a nearly straight arc on its circle to the last digit. An arc past a half moves in
its last bits: its end lands within a few parts in 1E16 of how far it reaches from the origin, a hair further than
before on a third of them and nearer on the rest.

`Arc2` holds the operations, and they mirror the ones for a line: `ProjectToArc`, `DistanceTo` (to a
point, a segment or another arc), `IsPointOn`, `Locate`, `TryIntersectWith` and `GetIntersections`,
`TrySplitAt`. Moving an arc is `Translate`, `RotateBy` and `TransformBy`; scaling it evenly is fine, a
scaling uneven between the axes or a skew gives the arc through where its ends and its middle land where that lands within
the point tolerance of the ellipse it becomes, and is refused rather than approximated otherwise (see
[Moving geometry](#moving-geometry)).

Two circles that stay within the point tolerance of each other all the way round, their centres and their radii no
more than the tolerance apart put together, are one circle, which meets along its length rather than at points, and
so are the arcs and edges on them: they cross nowhere, and whether they touch is asked by `CollidesWith` and
`DistanceTo`. The circles of radius 1 000 and 1 000.00002 with their centres 1E-4 apart, 1.2E-4 apart at most, are
one at a tolerance of a thousandth, and so are two arcs of one circle whose ends are rounded to six places, their
centres 1.6E-6 apart. Two circles that draw further apart than the tolerance somewhere cross where they truly cross,
however close their centres: the circle of radius 1 000 about the origin and the one of radius 1 000.0005 about
(0.0006, 0), 1.1E-3 apart at most, cross at (-833.33324, 552.77094) and (-833.33324, -552.77094). The difference of
the squared radii is worked out as the product of the difference and the sum of the radii, so a crossing keeps the
digits that rounding the squares of radii a thousand long would lose. Centres no further apart than the point
tolerance are still one centre where the rounding of the numbers leaves the line the circles cross on nowhere in
particular: where the rounding of the largest coordinate or radius, times the sum of the radii over the distance
between the centres, is more than the point tolerance.

`GeoArc3` is the same arc in its own plane, carrying a `Normal` as well. It adds `GetAabb()`, which is
exact rather than the box round the whole circle, and `ProjectToArc2(frame)` to bring it into the plane. An arc
comes down as the whole circle only when it sweeps a whole turn; one within a degree of it is laid out from its
centre, its start and its end too near each other for a circle through three of its points, but from where it starts
and as far as it sweeps: the arc of radius 10 from 1 radian sweeping 359.5 degrees comes down as that arc, where it
came down as the whole circle from angle nought, its start 19.2 from where it is. An arc tilted to the frame comes down as
part of an ellipse, which an arc cannot be, and one within a degree of a whole turn, laid out on its own radius about its
centre, stands off where its points come down: some 0.056 for that arc with its normal (0.2, 0, 1).

### Chains that curve

A `GeoPolyline2` is straight by definition, so an arc-carrying chain is its own type. `GeoEdge2` is one
piece of it — two ends and a bulge — and it is a straight segment until the bulge is not zero:

```csharp
var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(30, 40));     // length 50
var bulged   = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(20, 0), 1.0); // a half turn, length 31.4

bulged.IsArc;        // true
bulged.ToArc();      // the arc it draws
bulged.GetChord();   // the segment across it, whether or not it bulges
bulged.ToLine();     // throws: this edge is an arc
```

An edge that bows less off its chord than the centre of its arc can be placed to, its sag no more than 2.5 parts in
1E16 of how far the centre and the radius reach from the origin, is a straight segment too, and measures, cuts and
flattens as one: the edge from (0, 0) to (100, 0) with a bulge of 1E-12, on a radius of 2.5E13, has `IsArc` false and
its middle at (50, 0), where its arc put the middle at (50.002, 0). It keeps its bulge, moves with it, and `ToArc`
still gives the arc of its bulge to whoever asks for that arc. A bulge of 1E-7 over the same chord, bowing 5E-6, is an arc.
Read as its chord, an edge's middle, its length and the area it bounds are the chord's, off the true arc's by no more
than its sag, which is less than the rounding of the arc's centre: of edges near the threshold the worst middle stood
7.34E-5 off the true arc's, the edge 9 609.5 long with a bulge of -1.53E-8, and the most any stood further off than its
arc put it before was 4.9E-5; a square 8 614 across, 74 million square units, came out 0.334 short. Split, its pieces are straight,
their bulge nought; `HasArcs` counts it as no arc. The reading follows how far the edge reaches from the origin along
the axes, so moving or turning an edge close to the threshold can change it.

`GeoPolylineArc2` is an open chain of those, and `GeoPolygonArc2` the closed loop, laid out the way a
drawing holds them: vertices, with the bulge of the edge *leaving* each one.

```csharp
var chain = new GeoPolylineArc2(
    new[] { new GeoPoint2(0, 0), new GeoPoint2(20, 0), new GeoPoint2(40, 20) },
    new[] { 1.0, 0.0 });                       // the first edge is a half turn, the second straight

chain.HasArcs;         // true
chain.Length;          // 59.7 — along the arc, not across it
chain.GetEdgeAt(0);    // a GeoEdge2
chain.Reverse();       // every bulge changes sign with it

var loop = new GeoPolygonArc2(slotVertices, slotBulges);
new GeoPolylineArc2(existingPolyline);         // widening a straight chain loses nothing
```

A loop closes itself, so repeating the first vertex at the end says nothing it does not already say and
that vertex is dropped — its bulge is kept for the closing edge. Equality comes in the two usual forms:
`Equals` is exact and starts where the loop starts, `IsEqualTo` compares the shape within a tolerance and
does not care which vertex the loop began at.

### Asking a curved chain the usual questions

Everything the straight chains answer, the curved ones answer too, and they answer it **on the arcs**
rather than on the chords. Flattening is for the region operations, not for measuring.

```csharp
slot.DistanceTo(point);            // to the round end, not to the chord across it
slot.Locate(point);                // Inside, OnSide or OutSide
slot.Contains(point);
slot.Centroid;                     // exact: the chord loop, weighted against each arc's own piece
slot.IsSimple();                   // exact too: chords that cross but arcs that do not is simple

slot.GetPointAtDistance(260.0);    // walked along the arcs
slot.GetParameterAtPoint(point);
slot.GetClosestPointOnBoundary(point);
slot.GetClosestEdge(line);         // a GeoEdge2, because the nearest piece may be an arc
slot.GetShortestLineTo(line);      // a GeoLine2 joining the two, measured on the arcs

slot.GetIntersections(knife);      // with a segment, arc, circle, chain or loop, straight or curved
slot.CollidesWith(plate);
slot.TrySplitBy(knife, out GeoPolylineArc2[] halves);
chain.TrySplitAtDistance(at, out GeoPolylineArc2 first, out GeoPolylineArc2 second);
chain.SplitAtDistances(new[] { 50.0, 150.0 });
```

A cut inside an arc leaves two arcs of the same radius, so the pieces put back end to end draw exactly
what went in. Cutting a loop gives open chains, and the run after the last cut carries on through the
vertex the loop happened to be held from.

`Locate` on a curved loop is worth a word, because it is exact with no arc cut up anywhere in it: it is
the straight loop through the vertices, turned inside out once for every piece an arc cuts off its own
chord that the point lies in. That one rule covers an arc bulging out, an arc bulging in, and an arc
sweeping more than half a turn.

### Offsetting without straightening

`Offset` keeps the arcs. It is the one region operation that does not go through Clipper, and the reason
is that offsetting can be done piece by piece: a segment moves sideways, an arc keeps its centre and
changes its radius by the same amount. A fillet of R40 offset by 10 comes back R50 exactly.

```csharp
GeoPolygonArc2[] grown = slot.Offset(20.0);                    // outward; negative shrinks
GeoPolylineArc2[] beside = chain.Offset(-10.0);                // left of the way it runs when positive
slot.Offset(20.0, OffsetJoin.Round);                           // Round, Miter or Chamfer at a corner that opens
```

A corner that closes up is trimmed to where the two moved pieces cross; one that opens is bridged the way
`OffsetJoin` says. `Round` bridges it with a true arc of the offset distance, and `Miter` — the default —
runs both pieces on until they meet, an arc reaching further round its own circle rather than being cut
across, which is what AutoCAD's OFFSET does. Where they would meet further away than
`OffsetOptions.MiterLimit` allows, the corner is squared off at the limit, as the straight offsets square
it: the triangle with a 10 degree tip grown by 1 is cut 10 from the tip, from (-9.973, -0.743) to
(-9.951, -1). Where there is no square to cut, because an arc's circle never reaches it or the miter has no
limit, the corner is rounded off at the distance. `Chamfer` cuts the corner square to its bisector at the
offset distance, touching the arc a round join would draw, between straight pieces leaving the two ends
along their tangents: the square of side 10 grown by 2 is cut from (8 + 2√2, -2) to (12, 2 − 2√2), as the
straight offset cuts it. Two edges a fraction of a degree off straight are mitred like any others. Shrunk far enough a
shape can fall into several pieces or vanish, so the answer is an array. A growing loop that closes a gap
around empty space gives that space as a hole wound the other way, so that signed areas add up, as the
straight offsets do, however the hole's pieces meet.

What tells the folded-over parts from the real ones is the one thing an offset cannot break: every point
of a valid offset stands **exactly** the offset distance from the shape it came from. Each piece is cut
where it comes to the offset distance from another part of the shape, and the stretches standing nearer go;
the rest of the piece stays. A chamfer or a miter is held to its own shape at the corner it fills. The hairpin
1 wide, 10 along x, 1 down and 9 back, offset by -0.6 into its inside, keeps (0, -0.6) to (0.553, -0.6). No
winding rule, no clipper, no tessellation.

An arc whose radius runs out has no offset left, and the offset goes round its two ends instead, on the circle
of the distance about each, whatever the join: that is what stands the distance from the shape there. The
rectangle 100 by 30 with its right side bowed 6 in, a radius of 21.75, grown by 40 with round joins, is rounded
off by the circles of 40 about (100, 0) and (100, 30), which meet at (137.081, 15); the loop encloses 18 397.80
whichever corner it starts from and whichever way it runs. Where the moved pieces meet in another order than
they were cut in, the loop is put together where they meet, and a lens left between two arcs comes back as a
loop of three corners.

A loop that crosses itself is read the way the straight offsets read one, even-odd: a point is inside when it lies
inside an odd number of the simple loops the crossings cut the loop into, and that region is what is offset, with its
arcs kept. The loop from (-7.818, 3.764) round an arc to the origin, round another to (-8.573, 2.412) and straight
back crosses itself at (-1.980, 0.198), where its two arcs meet again beside their corner, and runs round a lens there
the other way; grown by 0.1 with round joins it is one loop of 5.9555, grown by 0.5 one of 14.5114, and shrunk by 0.1
one of 2.6454, as the straight path gives them on the loop cut within 1E-5 (5.9555, 14.5114, 2.6454). A gear of 642
teeth is such a loop, its two arcs at each inner vertex crossing again 0.0079 away and making a slit between them
that is 4E-7 wide; grown by 0.5 it is one loop of 31 667.81, the slits lost in the valleys, and shrunk by 0.5 one of
28 106.67 (the straight path's 28 106.72), every slit holding a disc of 0.5 out of the roots. The five-pointed star drawn
in one stroke, its middle a hole, grown by 0.3 is its outline and a smaller hole, and shrunk by 0.2 five tips. The loops
come back wound as the signed area of the loop says it runs, the outer ones that way and the holes the other. Grown with
miters or chamfers, each tip of the region where two lobes of it cross is a corner of its own, and is mitered or cut as
any corner is: the bow tie from (-10, 2) to (1, -0.2), (1, 0.2) and (-10, -2), its back an arc of bulge -0.25, grown by 1
keeps the miter of its long lobe out to (5.099, 0), past the small lobe, and encloses 54.460, and the loop of four corners
whose first and last edges cross 1.3 degrees apart at (5.723, 5.984), grown by 3 with chamfers, is cut square at both tips
of that crossing and encloses 212.058. Two quarters of the region at a crossing are two lobes when they lie in two pieces
of it once it is cut at every crossing, as the straight path reads them. Where they are one piece running round a hole
they are no tips: the square 10 by 10 whose bottom edge curls up into a loop from (6, 2) to (4, 2), crossing itself at
(5, 1.667), grown by 3 with miters, is 253.263, as the straight path gives it. Where more than two edges cross at one
place, crossings within the tolerance of each other made one, or where a corner of the loop lies within the tolerance of
the crossing, or a piece through it ends at a corner within it, the loop is read within a finer step of the tolerance
where one tells them apart (below); where none does, or where a lobe beside the crossing is a sliver within the
tolerance, the region is joined there round the space between, as when it is rounded; a vertex the loop runs straight
on through is no corner. The DXF polylines' `GetOffsetCurves`, whose miter has no limit, join every crossing so. A loop
that does not cross itself is offset as it always was. A vertex that lies on another edge is a crossing when the edge
before it comes from one side of that edge and the edge after it leaves to the other: the loop from the origin to
(10, 0), up to (10, 10) by an arc of bulge 0.2, back to (5, 0) and on to (3, -6) by an arc of bulge -0.2 crosses itself
at (5, 0), and grown by 0.5 with round joins is one loop of 74.484 (the straight path 74.483), shrunk by 0.5 two of 21.342
and 10.017, the same when the vertex is 0.0005 above or below the edge. A vertex that touches an edge with both its own
edges on one side of it does not cross it.
A spike the loop runs out to and straight back from, between two straight edges whose far ends lie within the point
tolerance of each other, is dropped first, as the straight offsets drop it, the two ends of its foot one corner: the
square 10 by 10 with a needle 0.0005 wide at its foot and 6 tall on its top, grown by 0.3 at a tolerance of 0.001, is the
square grown, 112.283 with round joins, 112.360 mitered and 112.298 chamfered, as the straight path gives them, where it
kept a strip 0.6 wide about the needle; the triangle (0, 0), (10, 0), (5, 10) with such a needle on its top is the
triangle grown, 60.179 mitered. A spike whose sides are arcs is offset as drawn.
A region shrunk with miter corners can lose a small island that a miter wedge covers: the offset arcs run on round their own
circles and are cut square at the miter limit times the distance, and what lies inside that wedge is not left. A loop of five
vertices shrunk by 1 with the default limit of 10 has two loops where round corners give three, the third of 0.203 lying
wholly in a wedge; with a limit of 2 it stays.

A miter that runs a piece on past its neighbour's other end can pinch the loop beyond it off: the loop from (-4, -3) to
(2, 1), round an arc of bulge -0.6 to (5, 8), round an arc of bulge -0.25 to (-4, -5) and straight back, shrunk by 0.3
with miters, is two loops of 11.4715 and 5.9544 (the straight path 11.4714 and 5.9544), and by 0.2 two of 13.0463 and
7.5977.

Beside an arc a chamfer is not always inside the miter, though a miter runs the arc on round its own circle and a chamfer
along its tangent: the loop from the origin along x to (10, 0), round a quarter circle of radius 5 about (5, 0) to
(5, 5) and straight back to (0, 5), grown by 1, is mitered to 76.246 and chamfered to 75.760, and still the chamfer's
corner at (11, -0.414) stands 0.014 outside the circle of radius 6 that the miter runs the arc round.

At a tolerance coarse beside the shape, two moved pieces can cross within the tolerance of where one of them ends, and are
not cut there. Where that leaves the offset open, the loop is offset again within a hundredth of the tolerance, and where
a run is still open within a ten-thousandth: at each step cut at its crossings wherever it crosses itself there, and as
a simple loop only where it crosses itself nowhere there. An answer is taken where it leaves fewer runs open and no fewer
loops, with any loop of it finer than the tolerance, as the answer within the finer step has it. At a tolerance of 0.1
the loop of twelve straight corners in a box 7.9 by 5.1, grown by 4, is one loop of 150.317 with round joins and 186.801
with miters, as the straight path gives them (150.320 and 186.801), and a loop of ten corners whose spike 12 degrees wide
is gone shrunk by 0.3 is one of 9.453; both were none. A loop of eight corners 2.62 by 2.37 with its arcs, cut at its
three crossings and grown by 0.3, is one of 4.886 with a hole of 0.0024 in it, finer than the tolerance, as within 1E-3.

Crossings of a loop that crosses itself that lie within the tolerance of each other, or are linked through others that
do, are one place, as long as each lies within the tolerance of the place, where every edge through it is cut: a row of
forty crossings 0.09 apart is twenty places of two, not one place 3.5 long. A loop with two places too near along an
edge to cut it at both is cut within a hundredth of the tolerance, or failing that a ten-thousandth, and offset within
that: a loop of fifteen corners 1.2 across that cannot be cut within 0.1 or 1E-3 is cut within 1E-5, and grown by 1 at a
tolerance of 0.1 is one loop of 7.858, as within 1E-5. A loop that cannot be cut at any of the three is offset as a
simple loop, as it was before. The loop of eight corners whose three crossings lie 0.070, 0.084 and 0.101 apart, grown
by 0.3 at a tolerance of 0.1, is an outline of 181.68 and three holes, and shrunk by 0.3 the six loops it pinches into,
as within 1E-3; the star {37/18} of radius 16.670 shrunk by 0.3 is its 37 loops of 0.824. A loop that crosses itself at
more than 2 000 places within the tolerance is read within the tolerance only, cut where it can be and otherwise offset
as a simple loop: where its crossings bunch, the work of a loop cut at them grows faster than their square, and 60
diameters of a circle, 6 903 crossings, cut within 1E-5 took 21 s and 1.2 GB. The star {71/35} of radius 10, 2 414
crossings, grown by 0.3 at a tolerance of 0.1 is so the one loop of 302.631 round it.

Grown or shrunk with miters or chamfers, a loop is cut again within a hundredth of the tolerance, and then a
ten-thousandth, where the tolerance does not tell a crossing from what lies beside it. A step the loop cannot be cut at
is passed over; one is taken where it is coarse at fewer places than the step before, until none is left, and the loop
is offset within the last step taken. A step coarse at as many places as the one before, as where three edges cross at
one point, ends it, so the loop is cut at most twice more. A loop of the answer that is a point within the tolerance
asked for, its corners all within the tolerance of each other, is left out: the answer is still within that tolerance.
Within the tolerance two crossings made one place turn the blunt end of the sliver between them into a point, and a
corner of the loop beside a crossing turns the tip there by its own turn, and a miter runs the error on, or squares it
off at its limit. The bow tie from (-10, 2) to (1, -0.2), (1, 0.2)
and (-10, -2), its back an arc of bulge -0.25, with a corner on its first edge 0.005 past the crossing where it turns by
1 degree, grown by 1 with miters at a tolerance of 0.01, keeps the miter of its long lobe and encloses 54.419, where the
miter was left out, 52.501. A loop of ten corners, two of whose edges cross 1.7 degrees apart and are crossed by a third
0.0078 from there, 0.00027 apart, shrunk by 0.3 with miters at 1E-3 is four loops of 24.284, where the corner beside the
point was squared off and cut 0.063 more out; and a loop of four corners whose spike 2.4 degrees wide is crossed by an
edge where it is 0.0057 wide, grown by 3 with miters at 0.01, is 386.351, as within 1E-4, where it was 387.385. A loop
of ten corners in a box 0.95 by 0.86, shrunk by 0.1 with miters at 0.01 and read within a hundredth of it, is the one
loop of 0.022622: the triangle 0.0013 by 0.0014 the finer step leaves beside a crossing is a point within 0.01. Round
joins, and the DXF polylines' `GetOffsetCurves`, read every crossing within the tolerance, as before.

A piece cut back by the corner
before it to no longer than the tolerance is mitered along the edge it was moved from: the tip of 22.6 degrees at
(3.416, -7.019) of a loop that crosses itself, grown by 0.3 at 1E-3, is mitered 1.546 out, as at 1E-4, where it was rounded
off.

**What still needs flattening.** The four boolean operations do, because a boolean cannot be done piece by
piece. They take an optional chord tolerance and hand back the straight types, which say plainly that the
arcs are gone:

```csharp
GeoFace2[] pierced = slot.Subtract(opening);          // GeoFace2: straight throughout
Boolean2.Subtract(slot, opening, 0.05);               // or name the chord tolerance
```

### An index over the edges

`GeoBvh2` is the counterpart of `GeoBvh3` in space: a bounding volume hierarchy over `GeoEdge2`, so one
index serves a straight chain and a curved one alike, with the arcs held as arcs.

```csharp
GeoBvh2 index = cog.BuildIndex();                     // and on GeoPolygon2, GeoPolyline2, GeoPolylineArc2, GeoFace2
GeoBvh2 same = GeoBvh2.FromPolygonArc(cog);           // the same tree, named from the index's side

index.GetClosestPoint(point);
index.DistanceTo(point);                              // to the edges, not to the region they enclose
index.GetIntersections(knife);
index.GetIntersections(line);                         // a GeoRay2 or a GeoXLine2: the line from either side of its origin
index.DistanceTo(other);
index.CollidesWith(other);
```

**When it is worth building.** When the *same* shape is asked *many* questions. Building costs a sort of the
edges, so it pays for itself over repeated queries and never on the first one; for a handful of questions, or
a shape of a handful of edges, the plain methods on the shape are quicker because they build nothing. Every
shape here is immutable, so a tree never goes stale under it: build it once, keep it as long as the shape
lives, and let it go with the shape.

It is called `BuildIndex` and not `GetIndex` because it does work. Calling it inside the loop it was meant to
speed up is slower than not having it at all.

**A face indexes the rim of every hole** along with its outline, because that is the boundary of the material
— the reading the whole library keeps for a face. So a point sitting in a bolt hole is answered by the rim it
sits in, not by the outline far away.

### Flattening

Arcs stop at the door of the region operations. Clipper, which does the booleans and the region offsets,
knows only straight edges, so a chain that curves is flattened first — and that is a conversion you make,
not one the library makes quietly behind you:

```csharp
GeoPolygon2 flat = loop.Flatten();        // automatic: within 0.2 % of each radius
GeoPolygon2 fine = loop.Flatten(0.05);    // no piece strays further than 0.05 from its arc

Boolean2.Subtract(flat, opening);         // now the whole 2D half of the library applies
```

The chords of an arc lie inside it, so a shape bulging outward encloses a little less once flattened, and
one bulging inward a little more. `Flatten` on a chain with no arcs gives back exactly what went in.

The tolerance is kept however fine it is and however shallow or large the arc: the bottom of a rectangle 100 by 50
bowed 0.015 (a bulge of 0.0003, on a radius of 83 333) flattened within 1E-4 is 13 pieces on the arc and an area of
5 000.994, where one chord would give 5 000, and a quarter turn of radius 50 000 within 1E-4 is 12 419 pieces. A circle
of radius 50 000 within 0.01 is 4 968 corners, and an ellipse of radii 50 000 and 25 000 within 0.001 is 12 920. Every
member that takes a chord tolerance (the arcs, the loops and chains with arcs, the circle and the ellipse, the meshing of
them, the pipes and revolved bodies) refuses a curve that the tolerance would cut into more than 1 048 576 pieces, with an
`ArgumentOutOfRangeException`.

What the tolerance cannot keep is a chord shorter than the point tolerance. The result is a polygon or a chain, and those
merge points closer together than the point tolerance (1E-3 by default): an arc of radius 1 sweeping 1 radian within 1E-7
asks for 1 119 pieces and keeps 559, straying 3.99E-7 from the arc, where the old cut, which stopped at 4 096 a turn, gave 652
pieces straying 2.94E-7. So where the chords fall under the point tolerance the merged result can stray further than a
coarser cut that kept its corners: merged chords of up to twice the point tolerance stray up to (2E-3)² / 8 r from an arc of
radius r, 5E-7 on a radius of 1. The writers (`ObjWriter`, `Wkt`, `DxfWriter`) keep every chord.

## A polygon that crosses itself

A `GeoPolygon2` is taken as it is drawn, and one that crosses itself is still a polygon: `IsSimple()`
says whether it does. Its `Area` is then the shoelace sum, in which the lobes wound the other way count
against the rest — a bow tie of two equal triangles measures nought — while `Locate` and the booleans
read the region it covers. `MakeValid()` gives that region as faces that do not cross themselves, one per
piece, so their areas add up to what the other questions see.

```csharp
var bowTie = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(10, 0), new GeoPoint2(0, 10));

bowTie.IsSimple();     // false
bowTie.Area;           // 0: the two triangles set against each other
bowTie.MakeValid();    // two faces of 25
```

## Hulls and fitted rectangles

`ConvexHull2.Of(points)` is the smallest convex polygon holding some points, counter-clockwise, with every
corner a real turn; `TryOf` says false where the points span no area. `GeoRectangle2.Fit(points)` is the
rectangle of least area round them, turned whichever way makes it smallest — a rectangle of least area always
has a side along an edge of the hull, so trying each finds it exactly. Points all in one line give a rectangle
of no height along that line. No point stands more than the tolerance outside the hull, however the points lie: a
corner within the tolerance of the segment between its neighbours is left out, but never one past an end of it, so
points a trillionth apart in X and 15 apart in Y still make their corner.

```csharp
GeoPolygon2 hull = ConvexHull2.Of(points);
GeoRectangle2 tightest = GeoRectangle2.Fit(points);   // turned the way the points run
```

## Rounding corners

`Corner2.Fillet` replaces a corner with an arc of a given radius, tangent to both edges, as AutoCAD's
FILLET does. Rounding creates curvature, so unlike a chamfer it cannot give back the kind of shape that
went in: it works on the arc-carrying chains.

```csharp
GeoPolygonArc2 rounded = new GeoPolygonArc2(plate).Fillet(15.0);   // every corner that fits
GeoPolylineArc2 eased  = chain.Fillet(15.0);                       // both end points stay where they were

rounded.Flatten();                                                 // back to a polygon when you need one
```

A filleted corner becomes an arc between two shortened edges, so a rectangle filleted at every corner
comes back eight edges: four sides and four quarter turns.

**One radius per corner.** `Fillet` also takes a list, read the way `GetBulgeAt` is read: the entry at an
index belongs to the vertex at that index, and a zero leaves that corner alone.

```csharp
plate.Fillet(new[] { 40.0, 10.0, 0.0, 25.0 });   // the third corner stays square
plate.TryFilletAt(1, 30.0, out GeoPolygonArc2 one);   // or one named corner, on its own
```

A list shorter than the shape leaves the rest of the corners alone, and the two ends of a chain have no
corner to round. Where two neighbours together ask for more than the edge between them is long, the one
taking more of it gives way — so a corner asking for 250 yields to one asking for 20, not the other way
round.

`Lengthen2.TryFilletCorner` does the single corner between two segments, and hands back the arc and both
trimmed segments. It finds the corner by extending the two segments, so they need not already meet, and
the order you pass them in does not change the answer.

**Corners that are left alone** follow the chamfer's rules, with one more:

| Left alone when | Why |
|---|---|
| the radius needs more edge than there is | the arc would not be tangent to both |
| two neighbours together ask for more than the shared edge is long | the one taking more of that edge is dropped, which may leave room for the rest |
| the corner is straighter than `Tolerance.EqualAngleRad` | there is nothing to round |
| no circle of the radius touches both edges on the inside of the corner | a circle outside it would run back against the edges and leave a cusp |

What was skipped, and why, is written to `GeometryHelperLog`.

A corner where an edge is already an arc is rounded too, by the one circle of the radius that touches both
edges on the inside of the corner, the arc moved in by the radius standing in for the straight edge's
parallel line. The arc always runs the way the shape runs and touches each edge on the edge itself, never past the
corner: the chain (0, 0), (10, 0), (3, 5) filleted at 0.002 is cut back 0.0062 along both edges by an
arc of 144.5°.

Exactly enough is enough: a square of side 100 filleted at 50 loses every straight edge and comes back
four quarter turns — a circle. Chamfered at 50 it comes back the diamond through the four midpoints. So is
a hair more or less, within the tolerance: where the corners at an edge take all of it, give or take the
tolerance, the edge vanishes and the pieces either side meet at one point, midway along it, or at its far
end when only one of its corners was reshaped. The two ends of a chain never move. Past the tolerance the
corner taking more gives way, as above. The square of side 10 filleted at 5.02 at a tolerance of 0.1 is the
circle of radius 5 through the midpoints of its sides. A loop that would keep fewer than three corners is
given back as it was.

## Circles as polygons

A circle can be cut into straight pieces three ways, and each answers a different question.

```csharp
circle.ToPolygon();                        // automatic: within 0.2 % of the radius, about 50 edges
circle.ToPolygonByChordTolerance(0.5);     // no edge strays further than 0.5 from the circle
circle.ToPolygonBySpacing(25.0);           // no two vertices further apart than 25 along the circumference
circle.ToPolygon(36);                      // exactly 36 edges

circle.ToPolyline(36);                     // the same points as a chain, first point repeated at the end
```

The vertices lie on the circle, so the polygon is **inscribed** and encloses slightly less than the circle
does: about 0.26 % less at the fifty edges the automatic tolerance gives.

A circumference rarely divides by a spacing exactly, so the spacing is a limit rather than a step: the
vertices are spread evenly and no two are further apart than asked, which leaves no short edge at the end.

`GeoCircle3`, `GeoArc2` and `GeoArc3` answer all of it the same way — an arc gives a chain rather than a
loop, since it does not close.

### Working in a local frame

`GeoCoordinateSystem2` is where a drawing sits, the counterpart of `GeoCoordinateSystem3` in space. A
transformation can say the same thing, and `ToTransform()` hands it over in that form, but a
transformation may also scale, mirror or shear, and reading one backwards means inverting a matrix. A
frame is rigid by construction, and reading it backwards is turning the axes round:
`GeoTransform2.ToCoordinateSystem(frame)` does that as a transformation, for a whole shape through
`TransformBy`.

```csharp
var frame = new GeoCoordinateSystem2(origin, xAxis);   // or (origin, angleRad)

frame.ToGlobal(local);                                 // place geometry built about the origin
frame.ToLocal(world);                                  // read it back, no matrix inverted
frame.ToTransform();                                   // the same placement as a GeoTransform2
GeoTransform2.ToCoordinateSystem(frame);               // and the reading back, for whole shapes

plate.CoordinateSystem.ToLocal(point);                 // a rotated rectangle carries its own frame
new GeoRectangle2(frame, 80.0, 40.0);                  // and can be built from one
```

`GeoRectangle2` is the rotated rectangle — the OBB of the plane — so it carries a frame exactly as
`GeoObb3` does in space.

A loop can be turned round with `Reverse()`, which is what decides winding and which way round a pair of
chamfer distances goes.

## Ellipses

`GeoEllipse2` is a region, as a circle is: a centre, the direction of its major axis, and the two
semi-axes, the major one at least as long as the minor. A minor radius larger than the major one is
refused rather than swapped, so every ellipse has one way of being written down.

```csharp
var ellipse = new GeoEllipse2(center, GeoVector2.XAxis, 300.0, 100.0);   // 300 along its axis, 100 across

ellipse.Area;                                  // 94 247.78, pi times 300 times 100
ellipse.Length;                                // 1 336.4893 round the rim
ellipse.Eccentricity;                          // 0.9428
GeoEllipse2.FromCircle(circle);                // a circle as an ellipse, its axis along X
```

**Its rim is walked by the eccentric angle**, the angle AutoCAD and IFC trim an ellipse by: the point at
angle t is the centre plus 300·cos t along the axis and 100·sin t across it. That is not the direction
seen from the centre. `GetPointAtAngle(Math.PI / 4)` is (212.13, 70.71), which lies 18.43° off the axis
seen from the centre, not 45°. `GetPointAtParameter` runs 0 to 1 once round, even in that angle, and the
distance members measure true length along the rim: `GetDistanceAtParameter(0.25)` is 334.1223, exactly
a quarter of the length. The length is worked out by the arithmetic-geometric mean and the lengths along
the rim by Carlson's elliptic integrals, both to rounding, with nothing sampled.

**It answers what a circle answers**, read the same way: as a filled region.

```csharp
ellipse.Contains(new GeoPoint2(250, 50));             // true
ellipse.Locate(new GeoPoint2(300.0005, 0));           // OnSide: within the point tolerance of the rim
ellipse.GetClosestPointOnBoundary(new GeoPoint2(400, 200));   // (275.5969, 39.5057), 203.0629 away
ellipse.SignedDistanceTo(center);                     // -100, the depth to the nearest rim

ellipse.GetIntersections(new GeoLine2(new GeoPoint2(-400, 50), new GeoPoint2(400, 50)));
                                                      // (-259.8076, 50), (259.8076, 50), along the segment
ellipse.DistanceTo(new GeoLine2(new GeoPoint2(-100, 150), new GeoPoint2(100, 150)));   // 50
ellipse.DistanceTo(new GeoEllipse2(new GeoPoint2(0, 250), GeoVector2.YAxis, 100, 50)); // 50
ellipse.GetIntersections(new GeoEllipse2(center, GeoVector2.YAxis, 200, 150));
                                                      // (±134.1641, ±89.4427), in the order of t
```

A point is on the rim when it lies within the point tolerance of it, measured as a distance in drawing
units to the nearest point of the rim, not by how far the ellipse's equation is from one. Distances,
shortest lines, crossings and collisions are offered against every shape a circle offers them against,
and against another ellipse, and `Contains` takes a segment, a circle or an ellipse whole. Crossings with
a straight shape or a chain come back in the circle's order, along the segment and edge by edge; crossings
with a circle, an arc or another ellipse come back in the order of the eccentric angle. Two crossings
within twice the point tolerance of each other are one point, as they are for a circle, and two rims
that come within the point tolerance without crossing touch there, at one point midway between them,
whichever of the two ellipses asks.

A distance from an ellipse is nought only where the two truly meet: a point of one inside the other or on its
rim, or the rims crossing. Otherwise it is the gap, measured exactly, whatever the tolerance, and `CollidesWith`
is true exactly when that gap is within the point tolerance. A polyline, a chain, a loop or the material of a face
0.0005 off the end of the ellipse 60 by 25 is 0.0005 from it, and so is an arc of radius 0.0008 whose nearest
point is that far off.

Everything is exact or exact to rounding, but for the cases below. A segment is crossed by a quadratic. A circle, an arc or
another ellipse is crossed by the quartic the two equations make, its roots polished on both curves. The
nearest point of the rim is found by Eberly's bisection, which holds at the centre, on both axes and for an
ellipse a thousand times longer than it is wide. The distance between two ellipses is the one answer with
no closed form: it is searched from 36 starts round the rim, 32 evenly spread and the four ends of the
axes, and refined, and it agrees with an independent dense search to 3E-13 relative on 80 000 pairs as
thin as a thousand to one. It is also the slow one, some tens of microseconds a call, where the distance
to a point takes about a third of one. `Contains` for another ellipse searches the same way when the
quick reading leaves it in doubt, and so does the touch between two ellipses, which is looked for in the
gap itself only where the rims come near enough to touch.

**Exact only to about 1E-6 near tangency.** Whether two shapes overlap is decided from the sign of an equation that is
rounding where they only just touch, so a gap or a crossing smaller than about 1E-6 can be misread. Two ellipses are the
main case: of 400 000 readings of pairs placed up to 1 apart, 240 were off and every one was at 1E-6 or less, and two
ellipses 1000 by 1 placed 1E-9 apart sometimes read 0. A small arc against a very thin ellipse is the other: an arc of
radius 0.5075 crossing the end of the ellipse 1E5 by 10 by 1E-7 read a gap of 1E-7, and an arc of radius 0.0000025 at
the end of the ellipse 60 by 0.01 read a gap of 2E-8 where it crossed and 0 where it stood 1E-9 clear. An arc far
smaller still than the ellipse can be misread by more: an arc of radius 0.000999 at the end of the major axis of the
ellipse 1E5 by 10, 0.00045 clear of it, reads 0 and collides at a point tolerance of 1E-4. Between shapes of sizes not
so far apart the answers past 1E-6 are exact to rounding, and a collision at a point tolerance above 1E-6 is not
affected.

Ask from the ellipse. The other shapes have no overloads taking one, so it is `ellipse.DistanceTo(polygon)`,
not `polygon.DistanceTo(ellipse)`. An ellipse has no `TryOffset`, since the curve a fixed distance from an
ellipse is not an ellipse, and it takes part in no boolean: flatten it first.

**Flattening** places the corners by the curvature, quadrant by quadrant and mirrored, so the polygon is
symmetric about both axes and has a corner at each end of both. The automatic tolerance is the circle's
share of the minor radius. An ellipse 1 000 by 1 then takes 84 edges, where corners an even step apart
would need about 1 571; the ellipse 300 by 100 takes 64 and encloses 0.175 % less than the ellipse does.
The count is rounded up to a multiple of four, so an ellipse with equal radii takes 52 where the circle
takes 50. The steps stay adaptive however fine the tolerance, up to 1 048 576 corners in all; a tolerance that
needs more is refused with an `ArgumentOutOfRangeException`. `TriangulateSurface` fans from the centre, and `ToMesh` meshes the
flattened rim as it does a circle's.

## Rays

`GeoRay2` is a half-line: an origin, and a direction of unit length it runs along for ever, the
counterpart of `GeoRay3` in space. A direction of no length is refused.

```csharp
var square = new GeoPolygon2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) });
var ray = new GeoRay2(new GeoPoint2(-50, 50), GeoVector2.XAxis);

ray.GetIntersections(square);                 // (100, 50), (0, 50), edge by edge as for a segment
ray.CollidesWith(square);                     // true
ray.Reverse().DistanceTo(square);             // 50, from its origin to the left side
ray.Reverse().GetShortestLineTo(square);      // (-50, 50) -> (0, 50)
ray.IsPointOn(new GeoPoint2(-50.0005, 50));   // true: within the point tolerance of the origin
ray.IsPointOn(new GeoPoint2(-60, 50));        // false: behind the origin
```

**It is asked what a segment is asked**, and every answer reads as the segment's does: a closed shape is
filled for the distance and the collision, so a ray starting inside the square is nought from it, while
the shortest line runs to the outline. Every shape that takes a `GeoLine2` takes a `GeoRay2` too, so
`square.DistanceTo(ray)` gives the same 0. An ellipse is asked from the ellipse, as it is for a segment.

Against any shape but another ray, the ray is answered as the segment from its origin to a point past
the farthest the shape reaches along it. Nothing of the shape lies beyond that point, so the ray answers as
that segment does: the ray above is asked as the segment from (-50, 50) to (101, 50). One thing does show the
cut: two members are parallel when they draw apart by less than the point tolerance along the longer
one drawn, and for a ray that is as far as the other shape reaches.

Two rays are measured directly. The ray from the origin along X and the one from (50, -50) along Y cross
at (50, 0). Two rays that cross at any angle, at or ahead of both origins, are nought apart; a crossing
behind an origin does not count, so two rays leaving origins 0.00156 apart are 0.00156 apart and, at a
tolerance of a thousandth, do not collide. Two rays parallel but for rounding follow the rule of the
infinite lines below: at a sine of 1E-9 or less a crossing further along than a double can place within the
point tolerance is no crossing, so a ray rebuilt through two points a hundred apart of another moved 10 to its
side, a sine of some 1E-14 off it at coordinates of ten thousand, is 10 from it, where 34 793 of 100 000 such
pairs read nought. `TryIntersectWith` still places a crossing up to the tolerance
behind an origin, and gives no point for two within the angle tolerance of parallel, as for two infinite
lines.

`PlanarMap.ProjectToRay2` lays a ray in space out in a frame and `ToRay3` puts it back; a ray along the
frame's normal has no direction in the plane and is refused. `GeoBvh2.GetIntersections` takes a ray as it
takes an infinite line: each edge is asked `edge.GetIntersections(ray)`, so the answer is the same points as
the plain scan's (in tree order) whatever else is indexed, and a branch whose box, grown by the point tolerance, the ray never enters is not
looked at. The edge from (0, 0) to (100, 0.0005), parallel at a thousandth to the ray from (-50, 0) along X, is
crossed nowhere, with or without an edge a million away indexed beside it, where the ray was cut past the box
round everything and crossed it at (0, 0); 200 diagonal rays over 90 000 edges take 50 ms where they took 1.3 s.

## Infinite lines

`GeoXLine2` is a line that runs both ways for ever: a point on it and a direction of unit length, the
counterpart of the XLINE of AutoCAD and of `GeoRay2` with no start. A direction of no length is refused.

```csharp
var square = new GeoPolygon2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) });
var line = new GeoXLine2(new GeoPoint2(-50, 50), GeoVector2.XAxis);

line.GetIntersections(square);                // (100, 50), (0, 50): edge by edge, as for a segment
line.CollidesWith(square);                    // true
line.Offset(100).DistanceTo(square);          // 50: the parallel line through (-50, 150), 100 to the left
line.GetPointAtDistance(-30);                 // (-80, 50): a line has points behind its origin
line.ClipTo(new GeoRectangle2(new GeoPoint2(50, 50), 100, 100));   // (0, 50) -> (100, 50), for drawing
line.IsPointOn(new GeoPoint2(-5000, 50.0005)); // true: nothing stops short at the origin
```

**It is asked what a ray is asked**, and every answer reads as the segment's does: a closed shape is filled
for the distance and the collision, so a line through the square is nought from it, while the shortest line
runs to the outline. Every shape that takes a `GeoRay2` takes a `GeoXLine2` too, from either side, so
`square.DistanceTo(line)` gives the same number to the bit. Unlike the ray and the segment, the line is
asked of an ellipse from both sides, `line.DistanceTo(ellipse)` as well as `ellipse.DistanceTo(line)`. A
point, a segment, a ray and another line are asked too.

Against any shape that has an end to it, the line is answered as the segment that runs from a millimetre
(or a hundred point tolerances, whichever is more) before the nearest the shape reaches along it to the
same past the farthest; the origin is not part of it, so a shape a million along is asked as the stretch
round it. An arc counts for its two ends and for the point of its circle furthest ahead or behind along the
line where the arc holds it, not for its whole circle, so an edge with a bulge of 1E-10 does not stretch the
segment to 1E11. Nothing of the shape lies outside, so the line answers as that segment does: the line above is
asked as the segment from (-1, 50) to (101, 50). One thing shows the cut: two members are parallel when they
draw apart by less than the point tolerance along the longer one drawn.

Parallel lines are as far apart as they are, whichever way they run: the line through the origin along X and
the one through (0, 30) along -X are 30 apart, and nought apart once they cross. Two lines whose directions differ by a sine above 1E-9 always cross, whatever the tolerance and however far
out. At a sine of 1E-9 or less they cross unless they are parallel by rounding: a sine below 1E-15, or a crossing
further along than a double can place within the point tolerance, that is (the larger of the two distances along the
lines from the crossing, plus the largest coordinate of the two origins) times 2.2E-16 above the tolerance, which is
4.5E12 along for a thousandth at coordinates near nought. Two lines 5 apart at an angle of 1E-9 cross 5 billion along
and are nought apart; at 1E-13 they would cross 5E13 along, which is rounding, and they are 5 apart, as a line offset by
10 and rebuilt from two of its own points, a sine of 1E-14 off at coordinates up to a million, is 10 from it and not
nought. At coordinates of ten million such a line is a sine of 1E-11 off and crosses 1E12 along, which can be placed,
so it reads nought. `TryIntersectWith` is as strict as it is for two rays: two
lines within the angle tolerance of parallel give no point, even where they are the same line.
A line and a ray are nought apart where the ray reaches the line, and otherwise as far as the origin of
the ray is from it.

`Equals` reads the fields, so the line from (500, 50) along -X is not equal to the line from (-50, 50) along
X; `IsSameLineAs` reads the line and says they are the same, within the point tolerance for the origins and the
vector tolerance for the directions. `Reverse` runs the same line the other way, `Offset(distance)` moves it to
its left, `GetPerpendicular(point)` stands one square to it, and `GetSide(point)` says on which side of it a
point lies. `ToRay()` is the half ahead of the origin, `ToLine(from, to)` the segment between two signed distances
along it, and `GeoRay2.ToXLine()` and `GeoLine2.ToXLine()` give the line a ray or a segment lies on. `ClipTo(rectangle)` clips a line that crosses the
rectangle exactly; one that misses it by no more than the point tolerance is clipped to the rectangle grown by it, so
its ends are no more than the tolerance outside, and one that misses by more gives null.

`GeoBvh2.GetIntersections` takes a line as it takes a ray. Each edge is asked what the plain scan asks it, `edge.GetIntersections(line)`,
so the answer is the scan's whatever else is indexed, and the branches whose box, grown by the point tolerance, the infinite line never
enters are not looked at; wherever on the line its origin stands, the answer is the same. Over the square 100 by 100, the line
through (500, 50) along X crosses at (0, 50) and (100, 50), where the ray from the same point, pointing away from the square,
crosses nothing.

A line without ends is written by `DxfWriter.Add(line, layer)` as the XLINE entity AutoCAD draws without end (`AcDbXline`, the
origin in groups 10, 20, 30 and the unit direction in 11, 21, 31, at zero height in the plane), and is not counted in the extents of the
drawing beyond its origin, as a ray's is not. `SvgWriter.Add(line, colour)` draws it across the picture from edge to edge both ways, its origin helping to fit the picture as a
ray's does, and `Wkt.Write(line, length)` has no picture to run to, so it writes the segment of that length centred on the origin: the line through
(10, 20) along X, written 100 long, is `LINESTRING (-40 20, 60 20)`. A `default` line, which has no direction, is
refused by all of them with an `ArgumentException`, and a length that is negative, NaN or infinite with an
`ArgumentOutOfRangeException`. The viewer (`tools/GeometryHelper.Viewer`) draws a `GeoXLine2` through the box of the scene with a dot at
its origin, gives its origin and direction among its properties, picks it and snaps to the nearest point of it, and reads an XLINE of a
DXF file back as the line in space.

## Splines

`GeoSpline2` is a curve that is one smooth sweep and not a chain of pieces: a NURBS, a B-spline whose control points may pull
on it with different strengths, which is what the SPLINE of AutoCAD and the B-spline curves of IFC hold. It is given by its
degree (1 to 10), its control points, its knots and, when it is rational, its weights. The curve is clamped, so it starts at its
first control point and ends at its last, and the control polygon through the points shows where it goes without touching it.

```csharp
var arch = new GeoSpline2(3,
    new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0) },
    new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

arch.GetPointAtParameter(0.5);             // (150, 112.5): the curve, not the 150 of its control polygon
arch.GetAabb();                            // 300 by 112.5, not the 300 by 150 of its control points
arch.Length;                               // 389.8927
arch.GetDerivativeAtKnot(0.0, 1);          // (300, 450): three times the first leg of the polygon
arch.GetTangentAtParameter(0.5);            // (1, 0), level at the top
arch.DistanceTo(new GeoPoint2(150, 200));  // 87.5
arch.Split(0.3);                           // two cubics that meet at (90, 94.5), exactly

var circle = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 100));
circle.Length;                             // 628.3185307180, 2 pi r to a relative 1E-12 and better
circle.GetCurvatureAtParameter(0.3);        // 0.01, all the way round
circle.DistanceTo(new GeoPoint2(160, 0));  // 60
```

**The knots are kept as given.** A spline read from a file goes back out as it came: nothing is rescaled to [0, 1] or
reordered, and the domain, `DomainStart` to `DomainEnd`, is whatever the knots say. Two kinds of parameter are told apart by name,
as the other curves keep theirs. The members named for a **parameter** (`GetPointAtParameter`, `GetTangentAtParameter`,
`GetCurvatureAtParameter`, `GetParameterAtPoint`, `GetDistanceAtParameter`, `Split`) take a share of the domain from 0 to 1, as
every curve in the library does; the ones named for a **knot** (`GetPointAtKnot`, `GetDerivativeAtKnot`, `GetTangentAtKnot`,
`GetCurvatureAtKnot`, `GetKnotAtPoint`, `SplitAtKnot`) take a value of the domain itself, which is what the knots say.
`GetKnotAtParameter` and `GetParameterAtKnot` turn one into the other: over the domain [5, 12] the parameter 0.5 is the knot 8.5.
Splines made here have the domain [0, 1], where the two are the same number. The ones named for a **distance**
(`GetPointAtDistance`, `GetParameterAtDistance`, `GetDistanceAtParameter`, `GetDistanceAtPoint`) go by length; a distance of
nought or less gives the start and the whole length or more gives the end, as for a chain. The knots are checked: clamped (the first
and the last repeated degree + 1 times and no more), not decreasing, no inner knot repeated more than the degree, one weight above
nought for each control point and the largest no more than 1E12 times the smallest (past that the length cannot be worked out
right: a ratio of 1E16 comes out 1E-7 short and one of 1E20 two fifths short), and control points that are not all one point.
`IsRational` says whether any weight is other than one; a plain B-spline stores no weight and is worked on exactly its own numbers.
A closed spline is a clamped one whose first and last control points coincide, `IsClosed()`; it is not periodic, and has no smooth
join at the end. Asked about other shapes, one whose first and last control points are the same to the bit, as the closed flag of a DXF
spline reads it, has no ends: its seam is a point of it like any other, where it comes in along its last span and goes on along its first.

**It is made four ways.** `new GeoSpline2(degree, points, knots, weights)` takes the numbers. `FromControlPoints(degree, points)`
spaces the knots evenly over [0, 1]: seven points of degree 3 have the knots 0, 0, 0, 0, 1/4, 1/2, 3/4, 1, 1, 1, 1.
`Interpolate(fitPoints, degree = 3)` finds the curve through points, as AutoCAD's fit-point splines do, by global
interpolation with chord-length parameters (Piegl and Tiller, 9.2.1): each point is met at the share of the chain's length
that comes before it, the inner knots are the means of a degree's worth of those parameters, and the control points solve one
banded system, so 100 000 points take 58 ms. Four points (0, 0), (100, 50), (200, 0), (300, 50) are met at
0, a third, two thirds and 1. `Interpolate(fitPoints, startTangent, endTangent, degree = 3)` adds the direction the curve
leaves and arrives in; a tangent gives a direction and nothing else, the curve keeps the speed it has on average, which over
[0, 1] is the length of the chain through the points, so three points 100 apart along X with the tangent (1, 0) at each end
give the line through them at an even speed of 200 whatever the length of the tangents. A fit point within the point
tolerance of the one before it is dropped, as a polyline drops a vertex, by the tolerance given (`Interpolate(fitPoints, degree, tolerance)`) or the global one; points just above it are kept, and the curve through three that are 0.0012 apart in a chain 300 long swings 507 000 away, where at a tolerance that drops them it stays within about 13 of the box of the points. Last, `FromPolyline`, `FromArc` and `FromCircle` make a
spline that is exactly the polyline, arc or circle: a circle is the nine control points of a rational quadratic, four pieces of
a quarter turn with the weights 1, 0.7071, 1, ..., and every point of it is at the radius to 4E-14 at a radius of 100, its last control point its first exactly, as for any whole turn; an arc of
a hundred degrees is two pieces of fifty.

**Everything is exact to rounding, nothing sampled.** The point at a parameter is de Boor's scheme in homogeneous coordinates, a
weighted mean of the control points of the span (11.7 units of rounding, of 2.2E-16 times the size of the spline, at the worst of
400 random splines of degrees up to 10 in the plane, checked against exact fractions). Derivatives to the tenth, the tangent
and the curvature (signed in the plane, positive counter-clockwise; never negative in space) are those of the exact quotient,
to 6E-14. `Length` integrates the speed by Gauss-Legendre of ten points over each Bézier span, the span cut in halves until the
rule over the whole and over its halves agree to 1E-14 of the length of its control polygon, cut again at each place its speed is
least, where a curve that turns back has a corner in the integrand, and a rational span whose weights are more than two to one
apart is cut first: 1.3E-15 against a Romberg integration on 400 random splines. `GetAabb` is the box the curve fills, found from
the roots of the derivative on each span, not the box of the control points. `Split(parameter)` and `SplitAtKnot(knot)` raise the knot to the degree,
so the two parts are the spline exactly and meet at one control point (a cut where the spline stands still, so that a part would be one point, is refused); `Reverse` runs it the other way over the same domain
(each distinct knot is mirrored about the middle of the domain once, so equal knots stay equal and distinct ones stay distinct, a unit in the last
place apart if rounding would join them; the mirrored knots are rounded to the last place of the domain's ends, which moves the curve
by the speed times that). `Translate`, `TransformBy` and `PlanarMap.ToSpline3`/`ProjectToSpline2` carry the control points, which
is exact for every affine map: a circle scaled by 3 along X is the ellipse 300 by 100, all 400 points of it on it to 1E-12.
A transformation that carries every control point to one point is refused.

**Nearest point and distance.** `DistanceTo(point)`, `GetClosestPointOnBoundary`, `GetKnotAtPoint` (and `GetParameterAtPoint`, `GetDistanceAtPoint`), `GetShortestLineTo` and
`IsPointOn` find the nearest point of the curve, which is a line and not a region, from either side to the bit
(`point.DistanceTo(spline)`). On each Bézier span the distance stops falling where `(C(t) - q) . C'(t)` is nought, a polynomial
in Bernstein form whose roots are all found by cutting in halves wherever its coefficients change sign twice or more, so a
point inside a loop, one on the axis of a symmetric curve, the centre of an arc, a point at a cusp all give the nearest of
all the places; where two points are equally near the lower parameter is given. A span is solved only if the box of its control
points is nearer than the best found, and the boxes are kept in a tree, built on the first query, so the spans near the point are
reached without looking at the rest: at 100 000 spans of a spiral a query took 0.53 ms before the tree and 0.010 ms after it
(0.70 ms and 0.012 ms under .NET Framework 4.8), and a span solved is about a microsecond; the tree costs about 3 ms, once, and a
spline of up to 24 spans looks at its boxes one by one, which is faster for so few. Every answer is the same to the bit as it was before the
tree. At the worst of 160 random splines and 800 points the distance was 4.3 units of rounding off the exact one.

**Flattening** keeps the chord promise. `Flatten(chordTolerance)` returns a chain whose corners are on the curve and from which
the curve is never further than the tolerance, however fine the tolerance and whatever the weights: a span is cut in halves until
every control point of the piece is within the tolerance of its chord (a Bézier piece lies in the hull of its control points), or
the second differences of a polynomial piece, or the height of the middle control point of a conic, say so with less cutting.
The circle of radius 100 within 0.01 is 256 chords, where 223 would hold, and the cubic above is 128. There is no automatic
tolerance, since a spline has no radius to take a share of; a tolerance that needs more than 1 048 576 pieces is refused.
A spline no longer than the point tolerance, which a chain would merge into one point, is the chain of its corners as they are: the
part 0.000245 long that `Split(1E-6)` cuts off the fit through (0, 0), (30, 20), (60, -10), (100, 0) flattens to the chord from its start
to its end, where it was refused. `Split` keeps such a part, exactly, as it keeps any other.

**Writing, showing and reading.** `DxfWriter.Add(spline, layer)` writes a spline as the R2000 SPLINE entity AutoCAD reads: its degree, its
knots, its weights where it is rational and its control points, at height nought, every number to the bit and no fit points, flagged
closed only where its first and last control points are the same point exactly, rational where a weight is other than one, and planar with the extrusion (0, 0, 1).
The arch above is a SPLINE of degree 3 with its 8 knots and 4 control points, and the extents of the drawing are its box, 300 by 112.5.
`SvgWriter.Add(spline, chordTolerance, colour)` draws a spline that is not rational and of degree 3 or less as the Bézier curves it is made
of, cut at its knots, a C command for each piece of a cubic (the arch is `M 0 0 C 100 150 200 150 300 0`), a Q for each of a quadratic and
an L for each of a spline of degree 1; SVG has no curve for the rest, the circle among them, which are drawn through the corners `Flatten`
cuts at within the tolerance. `Wkt.Write(spline, chordTolerance)` writes the `LINESTRING` of those corners, every one kept and the ends the
first and last control points exactly: the circle of radius 100 within 0.01 is 256 chords. See [Common](common.md) for the writers. The
viewer (`tools/GeometryHelper.Viewer`) draws a spline by a cut of its own at its knots, each piece halved until it lies within the chord
tolerance of the scene (a tenth of its point tolerance) and turns no more than 2 degrees, so the arch is 1 722 chords, and a cubic of
40 000 control points is drawn in 0.8 s; it snaps to the two
ends of a spline and to the nearest point of it, picks it, gives its degree, control points, rational or not, domain, closedness and length
among its properties, and reads a SPLINE of a DXF file back by its control points, knots and weights to the bit; one given by its fit points
alone, and a periodic one, are left out with a warning. GeometryHelper.CadConvert converts AutoCAD's `Spline` to and from `GeoSpline3`
through its `NurbsData`, and to a `GeoSpline2` as it is seen in plan (`ToGeoSpline2`, or `TryToGeoSpline2` for false in place of a throw),
which is exact, since a spline seen square onto a plane is the spline of its control points seen so.

### Against the other shapes

A spline answers the five queries about every shape in the plane, from either side, as a ray and a line without ends do:
`DistanceTo`, `CollidesWith`, `GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`, each with a tolerance and without, about a
segment, a ray, a line without ends, a polyline, a polygon, a rectangle, a triangle, a face, an arc, a circle, an ellipse, an edge, a
curved chain, a curved loop and another spline. The shape's member hands the question to the spline's, so `polygon.DistanceTo(spline)` is
`spline.DistanceTo(polygon)` to the bit, the crossings are the same points, and the shortest segment is the same turned round.

```csharp
// arch: the cubic above, the parabola y = x (300 - x) / 200;  ring: the circle spline of radius 50 about the origin.
arch.GetIntersections(new GeoXLine2(new GeoPoint2(-50, 100), GeoVector2.XAxis));          // (100, 100), (200, 100)
arch.DistanceTo(new GeoXLine2(new GeoPoint2(-50, 113), GeoVector2.XAxis));                 // 0.5, over the top
ring.GetIntersections(new GeoLine2(new GeoPoint2(-100, 30), new GeoPoint2(100, 30)));      // (40, 30), (-40, 30): in order along the ring
var square = new GeoPolygon2(new GeoPoint2(-100, -100), new GeoPoint2(100, -100), new GeoPoint2(100, 100), new GeoPoint2(-100, 100));
ring.DistanceTo(square);                                                                   // 0: the ring is inside the square
ring.GetShortestLineTo(square).Length;                                                     // 50: the gap out to its outline
arch.DistanceTo(arch + new GeoVector2(0, 20));                                             // 11.3359: the ends of the copy are nearer than 20
```

**The rules are the other curves'.** A closed shape (a polygon, a rectangle, a triangle, a face, a circle, an ellipse, a curved loop)
is a region: a spline inside it, or in the material of a face, is nought from it, and one in a hole is the gap away from the hole's rim.
The spline itself is a curve with no inside, so it has no `Contains` and no `SignedDistanceTo`. The distance is nought only where the two
truly meet, a crossing or a touch found to rounding, and otherwise the gap, measured exactly: it never reads the tolerance, and a spline
a half of a thousandth from a line is 0.0005 from it. `CollidesWith` is the distance being no more than the point tolerance, exactly, so
the two never disagree. The crossings are those of the boundary, in order along the spline from its start (along the one asked, for two
splines, the same points bit for bit from either), and so is the shortest segment, which runs from the spline to the outline and has no
length where they cross: the ring inside the square is nought from it and 50 from its outline. The place given is the point of the spline,
which is the point of the shape too where they cross.

**A place is a meeting or a least of the distance.** A crossing and a touch are found where the two meet, and a near touch and an end
that comes near where the distance of the spline from the shape is least within the point tolerance: the line 0.0005 over the top of
the ring touches it at (0, 50) at the default tolerance and not at a tolerance of a ten-thousandth. A stationary point of the distance
that is a greatest is not a place, nor is a point on a slope: a line 1E-6 inside the top of the ellipse of radii 100 and 50 meets its rim
at x = -0.02 and 0.02, and the middle between them, where the distance is greatest, is not given; the 2 000-point fit of an arc of radius
1 000 has nine places against the line that is tangent at its top where each span's start on either side of the touch was one, and one
place now. Places no further apart than the point tolerance, one from the next, are one place, as are two near touches next to each other
along the spline within ten times that with no point between them farther from the shape than at either by more than four snaps (the
snap is 1E-13 of the size of the numbers: where a curve touches another the rounding of the numbers leaves the distance as near nought
as the digits tell all along a stretch, and each near touch it finds there is the one). Two meetings are one place only when they are
within the tolerance of each other, whatever lies between them, as the exact curves have it: two crossings 6E-4 apart are one place at a
thousandth and two at a ten-thousandth, and a line that cuts a circle of radius 30 a million from the origin in a chord of 0.009 meets it
twice, for the spline as for the `GeoCircle2`. A line tangent to a circle of radius 1 000 touches it once, however the line is cut into
segments (a polyline of 10 000 segments, 100 000 out): a million out the numbers are 1.2E-10 apart and the two are within a snap of each
other for 0.014 either side of the touch, where the places are the crossings of the rounding, up to five, all within that hundredth of a
millimetre. The snap is 1E-13 of the size of the numbers, which for a line, a segment or a polyline is the sum of its coordinates' sizes:
2E-7 for one a million out along both axes. A meeting that is one only to the snap, no crossing and further from the shape than the
numbers can be evaluated to (sixteen units in their last place), which lies within ten times the tolerance of a meeting to the digits and
is no least of the distance, is left out: a quadratic sitting on the circle spline of radius 100 a million from the origin is 2.3E-8 off it
0.002 either side of the touch, and those two points, on the slope of the distance, were given besides the touch, where the `GeoCircle2`
gives one place; it is given one. Ten million out the two points are 4E-9 off, within what the numbers are evaluated to, and are still
given in place of the touch, a known limit. Of the edges of a polygon, chain or loop, each is asked in turn and a place that another edge is nearer to is
not one of the shape. A closed spline has no end to come near: the line tangent to the circle spline of radius 100 at 0.003 of a radian
touches it once, as it does the `GeoCircle2`, where the seam at (100, 0), 4.5E-4 off the line, was given too as an end. A least
that curves up too little to rise a snap a thousandth of the size of the spline along it is one only where the distance falls neither
way, as in space: the cubic y = x³ / 100 turned by 0.7 of a radian a hundred thousand out was given its inflection, 0.0001 under the
line y = 0.0001, besides the crossing at x = 0.21544, and is given the crossing alone.

**Two crossings a hair apart.** Two curves that cross twice a hair apart run side by side between the crossings, and the nearest points of
two straight parts there are the middle, a greatest of the distance, with no slope for Newton's method to go by; a crossing at a few
millionths of a radian is as near as the digits tell for a long way either side of it. They are found by the offset of the one from the
other along the normal, which changes sign where they cross and nowhere else: at a greatest the distance falls to nought either way
along the curve, the offset is tried where its quadratic model reaches nought and nearer and further, and a change of sign is bisected to
the last digit; and a part's ends are tried the same way. A line 40 or 4 000 long that cuts the circle of radius 1 000 in a chord of
0.004, 0.02, 0.1 or 1 meets the circle spline twice, at x = +-chord / 2, from either side, as it does the `GeoCircle2` (it met it once, or
never, before), and the distance is nought.

**Where the spline lies along the shape.** To rounding, along a segment, a ray, a line, a circle or an arc, a degree-one span along a
segment, a spline cut at a knot beside the whole, the spline of an arc beside the arc: it is nought from it and collides with it but
crosses it nowhere, and no point is given along the stretch: the corner (0, 0), (100, 0), (100, 50) as a spline meets the segment from
(-50, 0) to (150, 0) at (100, 0), where it leaves it, and its first span is not a crossing, as a polyline's is not. A spline and its own
copy cross nowhere and are nought apart. Against an ellipse or another spline the same is so where each stays within the point
tolerance of the other, and the first keeps its distance level, changing by no more than half of it, for a quarter of the size of the
smaller of the two, the size being how far its control points reach from the first, which is the same however the pair is turned (the
span along the axes was, and 14 of 60 copies of a spline moved a few ten-thousandths gave a place more or fewer turned by 0.7 of a radian).
Past an end of the other, within the tolerance of it, the first is along it as far as the tolerance reaches, as an arc is along an arc of
its circle, so a copy that overhangs the other's end and the other's end itself answer alike whichever is asked first, which turning the
pair can change; a point square from the other's end is a least of the distance only where the distance falls on neither way, the other's
inside included; and a crossing found inside the stretch stays a crossing however a meeting found beside it is read. Of those 60 copies
none gives a place more or fewer turned, where 5 did, and of 200 more 4, where 18 did. Two circles of radius 1 000 and 1 000.0005 are 0.0005 apart all round, collide, give no place, and are 0.0005 apart for
`DistanceTo` (not nought: it never reads the tolerance), where at a tolerance of 1E-4 they do not collide and are as far apart. Both are
looked at: a stretch that one curve leaves by more than the tolerance, a dart of a millimetre of a polyline 2E-4 off a line that goes out a
unit and back, is cut out of the stretch, which is cut in two there, and the crossings at the dart's two edges, (506.0002, 0) and
(507.9998, 0), are given from either side, where they were lost. A crossing inside a stretch is given too, where the offset of the one
from the other changes sign and goes more than a hundredth of the tolerance, 1E-5, to each side: a line and one that goes from 2E-4 under it
to 2E-4 over it cross at (500, 0) in 1 000. A change of sign by less is the rounding of curves that are the same to a hundred-millionth: a
lobe of the offset between two changes, its samples off nought on one side (a sample that is nought exactly belongs to none, and the change
lies across it), that goes no further is taken with its neighbours, and between two lobes that go further, one to either side, the curves
cross once, at the change nearest the middle of the small lobes between them, or the next nearest where that one is a jump of the offset at
a corner of the other curve, which bisects to no meeting, or a crossing so shallow that the two run along each other there to the snap,
and failing every change, where Newton's method on the two curves from either side of it settles on a meeting to the hundredth of the
snap it works to, the one nearer the change (a point it stalls on short of that is no crossing, however near, and one it settles on is one
however long the two run along each other round it: two segments a million out that cross at a slope of 2.5E-7 cross once, at their
crossing, 0.00006 off, where they were given no place, or one 0.35 off); between two that go
further to the same side, not at all. So between the first and the last lobe that go further, the crossings given are odd in number where
those two lie to either side and even where not, wherever a change between two such lobes is found to meet; a small lobe before the first
or after the last gives none. A cubic whose offset from a line goes from 1.1E-4 under it to 4.4E-4 over it, crossing it at 20, 40 and 60
with lobes of 7E-6 between, crosses it once in the plane, at 40 at the origin, whichever is asked and however turned, where it was given
three; a million out the crossing at 40 runs along the line within the snap for 0.1 either side (1E-7, 1E-13 of the largest coordinate of the
control points of the two splines; a `GeoLine2` reads the sum of its coordinates, 2E-7 there), and the one at 60 is given; laid in
a tilted plane of space the pair lies in no plane the digits can tell, and is measured in space, where no lobes are read, at all three. A
zigzag of degree one and its copy moved 2.6E-5 along it cross at each corner where the edges either side are 1.8E-5 off each other, and
once across two shallow edges 8.7E-7 off each other, where the exact polylines cross three times. Of 400 random polylines that never turn
back and their copies moved 2.6E-5, 216 are given the exact count of crossings and 314 its parity, where 201 and 270 were, and moved 5E-4,
388 and 395 of 400 more, where 266 and 299 were, and none answers otherwise asked the other way or turned. A polyline that turns back
on itself pays for the rule where its first or last lobe goes no further than the hundredth: the first edge of one 26.8 long lies 7.07E-6
off its copy, and the corner after it, which the exact polylines cross at, is no crossing; of 400 such polylines at the origin and their
copies moved 2.6E-5, 46 are given the exact count and 285 its parity, where 51 and 293 were, and none of them answers otherwise asked the
other way or turned, where 36 did. Where the two lie on each other exactly, to the digits, between two lobes that go further than the
hundredth, the place is fixed by that overlap and not by where a bisection or Newton's method settles in its rounding: a polyline over
a line by 0.0003 to 10, on it from 10 to 50 and under it after, crosses it once, at 10, the end of the overlap next to the earlier lobe
along the spline the work is done from, however the pair is turned (it was given at 34.95 turned by 0.3 of a radian, and the other end
of the overlap could be given as well). That end is the crossing where the curve comes on the other from the earlier lobe's side and
leaves it to the later lobe's; coming on from the side it leaves to, it changes side before the overlap or after it, and crosses there:
over the line to 10, 0.000005 under it to 30 and on it to 50, it crosses at 10. Between two lobes to one side it is no crossing, and
nothing of the overlap is given there. A meeting inside a stretch along at more than a degree from parallel, the default angle
tolerance, with another branch of the other curve than the one the stretch runs along, is given, inside an overlap or at its ends as
well: a cubic that crosses itself at 74 degrees and its copy moved 0.0005 lie along each other all the way, and the copy crosses the
loop at the loop's own crossing, twice, 0.0005 apart, which was missed asked either way, turned and in a tilted plane of space with the
copy in that plane (lifted out of it the copy is measured in space and still misses it); a polyline that lies on a line from 20 to 50
and comes back square across it at 35 crosses it there. The two crossings at the loop are one place, and the one first along the two
curves together stands for it, or where that is the same to a billionth the one nearer the point half way between the curves' starts,
the same asked either way and turned. A copy of a polyline still crosses it at a corner only as its lobes have it, and a branch of the
other curve that comes back within about three sixty-fourths of a stretch along of where it left is not told from the branch the
stretch runs along. A
sine of 21 fit points and a copy moved 0.0005 up are along each other and cross nowhere, where 15 101 places came before, after 22 seconds,
and none comes now in a few milliseconds. The fit through 37 points of a quarter circle against `GeoArc2` of the same arc, which is
solved exactly, gives its true crossings, 72 of them, the fit being the arc to rounding only in the places it was made to pass through,
and off it a little between, to one side then the other; against the circle spline of the same arc, which is a spline and is cut like one, it is a stretch along, within the
tolerance and level all the way, and gives only the crossings that go more than 1E-5 to each side: ten,
all in the first and last tenth of the fit. A touch is not level, its distance going from nought to the tolerance in a few units and past
it, nor is a crossing at any angle a stretch of level distance of that length, so a tangent contact of curves of any curvature is a place.

**How each is solved.** Against a segment, a ray or a line without ends, the signed offset of each Bézier span from the line is a
polynomial in Bernstein form, and the places the span crosses it are its roots, all of them found by cutting in halves wherever the
coefficients change sign, to rounding; its critical points give the touches and the nearest approach, and the ends of the segment
and of the spline are measured too. A ray or a line without ends is cut to the stretch the spline's hull can reach. Against a circle,
an arc or an ellipse the quadratic form of the curve laid into the span is a polynomial of twice the degree, solved the same way; an
ellipse is also a rational spline of four spans, so its nearest approach is found as for a spline. A span that is itself an arc of a
circle, as every span of `FromArc` and `FromCircle` is (a rational quadratic whose two outer control points stand as far from the middle
one, to a millionth of a millionth of the size of the numbers, its middle weight the cosine of half its turn), meets a circle, an arc, or
such a span of another spline in closed form, as the two circles meet by the rule of `GeoCircle2`: at their crossings, or their touch,
where both arcs hold it, and where the two circles are one circle to that rule, within the tolerance of each other all round, along
each other over what both arcs cover and the tolerance past it, which gives no place, the ends of the arcs in it included. For the
distance the two are measured as they are: the crossings where they truly cross, and otherwise the points on the line through the
centres. A span's circle is read from its control points, whose rounding, taken at the size of the radius since an arc is made from
its centre, reaches the centre times four times the square of the radius over the chord, and the closed form is taken only where that
leaves the centre within a hundredth of the tolerance of the true one for the places, and within a snap for the distance: a quarter
turn of radius 100 000 is read to 1E-10, while a flat arc of radius 1 000 000 and chord 300, read to no better than 2.3E-4, or of radius
10 000 000 and chord 1 000 a hundred thousand out, off by 0.6, is cut as any other span, where the circles read so gave one to four
places the arcs do not have. A flat arc so cut can still answer otherwise than the arcs against an arc of nearly its circle, as it
did. Two such arcs that are one circle to the rule however far the reading may be off, their centres and radii within the tolerance of
each other with the doubt on each counted twice, lie along each other as the arcs do: the spline of an arc of radius 849 821 and the arc
of that circle moved 0.00011 and 0.0002 smaller meet nowhere, where the ends of their overlap were two places. Where the doubt is larger
than that leaves room for, the spline of a flat arc is not one circle to the digits of its control points, which hold a circle off the
arc's by up to 0.7 for an arc of radius 69 000 000 and chord 15 000, and it still answers otherwise than the arcs, a known limit: of 1 200
random pairs of arcs of radius 100 to 100 000 000 a thousand from the origin moved and grown less than the tolerance, 307 do, where 517
did, and with their centres within 100 000 of it, 384, where 603 did, every one of them a spline whose circle cannot be read to the
tolerance; the spline of radius 1 000 000 and chord 300 is given two places against the arc of its circle 0.0002 larger, which the arcs
do not have.
Two circle splines nearly about one centre are as near as the digits tell for a long way round, and cut into parts they paired
every part of the one with hundreds of the other's: of radius 100 000 and 100 000.0005 with their centres 0.01 apart they ran through
two million pairs in 16 s and gave no crossing, and were 4.8E-5 apart though they cross; they cross now at (-4 999.995, +-99 874.922), as
the circles do, and are nought apart, each in a third of a millisecond, and so at radius 10 000 and 1 000. Of 200 circle splines moved a
few ten-thousandths to a hundredth, 142 answered otherwise than the circles against the circle they were moved from and 128 against its
spline, and of 300 such random pairs none do. A join of the spans within a snap of a very shallow crossing can still be given as a
place of its own, a known limit: the circle splines of radius 100 000 about the origin and of radius √(0.003² + 100 000²) about
(0.003, 0) cross at (0, 100 000) and (0, -100 000), joins of the first one's spans, and give four places where the circles give two.
Two circles of radius 1 000 and 1 000.00002 with their centres
1E-4 apart are one circle at the default tolerance, as the `GeoCircle2` reads them, and cross at (-200, +-979.8), at an angle of 1E-7, at a
tolerance of a hundred-thousandth. Against another spline both are cut
in halves while their hulls are near enough to matter, until each part is straight to a thousandth and the two cannot run the same way,
or to a billionth where two parts could cross twice, and Newton's method on the two curves, then on the distance along the first,
brings the nearest pair to rounding. For the nearest approach the parts are taken nearest bound first, whichever pieces they are of, so
that the cutting goes where the least can be, and it ends when the nearest bound left is no nearer than the best found less a hair: the
hair is a billionth of the size of the shapes (0.001 at a size of a million millimetres) and never more than a thousandth of the distance
itself, so that a near miss of 0.0009 at a size of a million is not read as 0.01 because a nearer pair was left uncut. The distance to a
segment, a ray, a line, a circle or an arc is exact to rounding; to an ellipse or a spline it is that of a real pair of points, so no
nearer than the truth, and within the hair of it when the cutting ends short of its budget, and to rounding wherever Newton's method
settles, which is wherever the curves cross or the nearest points are not parallel. Two curves that are side by side at a distance that
is the same all along, two circles 0.0005 apart, can be cut to the hair only by cutting every part, which is more than a budget of 40 000
pairs: when it runs out, the curve is first looked at at 128 points and the least of them brought to the real curves (after 2 000 pairs),
so that the best found is near the truth, and the answer is that best. What is promised then is that the answer is the distance of a real
pair of points and the truth lies between it and the nearest bound left, which one Debug line gives with it; the answer is usually far
nearer the truth than the bound is. So the circles of radius 100 000 and 100 000.0005, the second with its centre moved 2E-4 toward 45
degrees, were 0.00029999998 apart when cut, where 0.0003 is exact, with 0.000296 the bound left, in 0.15 s, and the depth-first cutting gave
0.000518, at the wrong place; met as circles they are 0.00029999998 apart in 0.3 ms. Short of the budget a crossing is never passed over for a nearer approach found first, since a part that
could hold a meeting is bounded at nought and comes out before any that cannot, so where the curves meet the distance is nought, exactly,
and they collide; past it a meeting not yet found is not in the answer. The places are cut to a budget of two million pairs, and a stretch
that is not level, or hugs at a distance that makes every part of the one a near match for hundreds of the other, could reach it, and one
Debug line is logged when one does. Two parts that cannot meet are measured once both are no larger than the tolerance, straight or not,
as in space: round a cusp a part is straight only when it is a millionth of the curve, and a tight spline of degree four and its copy
moved 0.00074, where the stretch along them ends short of the cusp, would be cut into two million pairs there, 26 s, and take 0.3 s. Two splines a hair apart and side by side for 6 000 take 0.15 s for
`DistanceTo`. A polyline, polygon, rectangle, triangle, face, chain or loop is its edges, each asked in turn, and an edge farther than the
best found is passed over; the few places that are asked whether another edge is nearer, and the neighbours that are asked whether they
are one near touch, are looked up in a grid of the edges' boxes, so that a million edges across a cubic take 2.2 s and 80 000
across it 0.3 s.

Checked against oracles that share no code with the library: on every run 20 random splines of degrees one to five, with repeated and
thin knots and weights for one in two, against four shapes of each of the 14 kinds, 1 120 pairs, each held to the oracle's places (the
crossings and the near touches within the tolerance, in count and in position); 165 pairs each of a spline with a line, a circle and
an ellipse that touches its tangent, from a ten-thousandth to three thousandths either side; 7 splines of each of 7 nasty kinds (a
million from the origin, control points repeated, all on one line, spans a millionth wide, degree up to ten, weights a thousandfold apart,
a cusp) against 7 kinds of shape; 12 pairs of two splines; every query from both sides, at three tolerances, for 2 520 pairs, to the
bit; and 45 hand cases, and 39 more, at a hundred thousand and a million from the origin, with splines of up to eighty thousand spans and
curves that cross twice a hair apart. The worst distance error was 1.6E-15 of the size of the spline, and 6.4E-9 of it a million from the
origin. A run of four times the draws, which the environment variable SPLINE_FUZZ_DRAWS asks for, once found a distance 5.8E-6 too great,
for a spline all on one line against an ellipse of size 700, where the hair was a hundred-millionth of the size; it is a billionth now,
the run is clean, and that draw is a test of its own.

**Not offered, and why.** `SignedDistanceTo`, `Contains` and `GetClosestEdge` (a spline has neither an inside nor edges), and a
`TryIntersectWith` that gives one point (a spline can meet a shape many times, so it gives the places as an array, as an arc does). A point
asks the spline with `IsPointOn` and `DistanceTo`, as before. Shapes in space (`GeoSpline3` against a plane, a box, a body) follow with the
spline in space.

Not in this release, and why: an `Offset` (the offset of a spline is not a spline; it would be an approximation to a tolerance),
splines as the edges of a `GeoPolylineArc2` chain, periodic knot vectors, and surfaces.

## Moving geometry

`GeoTransform2` is a 3x3 homogeneous matrix covering translation, rotation, scaling and mirroring, the
counterpart of `GeoTransform3` in space. It is applied on the left, so `a.Multiply(b)` means "apply b,
then a", and instances are immutable.

```csharp
GeoTransform2 move   = GeoTransform2.Translation(new GeoVector2(10, -3));
GeoTransform2 turn   = GeoTransform2.Rotation(center, Math.PI / 2);   // or about the origin
GeoTransform2 shrink = GeoTransform2.Scaling(center, 0.5);            // or per axis: Scaling(2, 3)
GeoTransform2 flip   = GeoTransform2.Mirror(axis);                    // across the line a segment carries
GeoTransform2 place  = GeoTransform2.FromFrame(origin, xAxis);        // put a drawing where it belongs

GeoPolygon2 moved = polygon.TransformBy(move.Multiply(turn));         // turn first, then move
GeoPoint2 back = place.Inverse().Transform(point);                    // read a placed drawing back
```

Every shape in the plane carries `TransformBy`, and `Translate` and `RotateBy` for the two common cases:
points, vectors, segments, chains, polygons, faces with their holes, circles, rectangles, arcs, and the
chains that carry arcs.

A bulge measures an arc against its own chord, so moving, turning and scaling evenly leave it untouched;
mirroring changes its sign, because the arc then leans the other way against the same chord.

**What a transformation cannot do.** A circle stays a circle only when every direction is stretched by the
same amount: when the two axes come out so nearly equally long and square to each other that the circle moved lands
within the global point tolerance of where the transformation puts each of its points, as a ball in space is read. A turn
whose numbers are rounded to six places or to a float on their own keeps the two axes alike to the last digit and moves
a circle of any size; a stretch of 3E-6 along X moves the circle of radius 100 at the scale it is drawn, 0.0003 off, and
is refused scaled by 1 000, where it would be 0.3 off. Under a scaling that differs between the axes, or a shear, it would be an ellipse, and the attempt is
refused rather than answered with an averaged radius; the skew that sends X to (1, 0) and Y to (0.6, 0.8) keeps
both lengths, and a circle of radius 100 is refused under it, where it came through at radius 100. Transform `GeoEllipse2.FromCircle(circle)` instead:
a circle of radius 100 scaled by `Scaling(3, 1)` is the ellipse 300 by 100. An ellipse itself takes every
transformation that does not flatten the plane, and stays an ellipse. A rectangle is
refused the same way when it would come out a parallelogram — transform `rectangle.ToPolygon()` when that
is what you want. Its axes are read as a circle as large as its half diagonal reads them: the rectangle 100 by 50 is
moved under a shear of 5E-6 at the scale it is drawn, its corners 0.00018 off, and refused scaled by 1 000, where they
would be 0.18 off.

An arc, and an edge with a bulge, is moved whole by its centre and its radius under a turn, a shift, a mirror or an even
scaling, to the bit as it was, where it lands within the global point tolerance so: a transformation whose axes are one
length and square to each other only to a share of some 1E-9, the rounding of a matrix built from axes, takes a large arc
off, X stretched by a share of 4E-10 and scaled by 1 000 the arc of radius 1E5 sweeping 6 radians 0.04, and such an arc is
read as under a stretch, and a circle, a ball or a rectangle refused. Under any other transformation, a skew or a
stretch, the arc is part of an ellipse, and the arc through where its two ends and its middle land, as an arc in space is
moved, stands for it only where it lands within the global point tolerance of where the transformation puts the rest of
it, everywhere along it, and is refused otherwise: asked at every sixteenth of its sweep only, the arc of radius 87.66 from
0.2 radians sweeping 1.51, under X stretched by 1 + 1.793E-6 and scaled by 84.1, landed 0.0010114 off between two of them,
and was moved. It is refused as the circle under it is: a stretch of 3E-6 along X moves the arc of radius 100 from
0.5 to 2 radians at the scale it is drawn, 0.000017 off, and refuses it scaled by 1 000, where it would be 0.017 off; the
skew that sends Y to (0.000001, 1) moves it 0.0000069 off, the edge of bulge 0.5 from (0, 0) to (100, 0) ending at
(100, 0), so a chain of such edges stays joined; and the skew that sends X to (1, 0) and Y to (0.6, 0.8) refuses it, the
arc of radius 66.09 through its moved ends and middle standing far off the ellipse of radii 126.5 and 63.2 its circle
becomes. A skew that keeps both axes one length is judged as a stretch is: a turn of 0.7529 radians of a stretch of
1 + 4.29E-9 and a shear of 9.28E-5, scaled by 55.57, took the arc of radius 9 069.6 to one 9.16 off, and refuses it.
Each point is read from the moved start, across the arc, so an edge 100 long bowing by 1E-12, of radius 2.5E13, moves
under a turn rounded to six places, where it was refused for want of digits. An arc of a whole turn is the whole circle
and is refused under a skew as the circle is, and so, with an `ArgumentException`, is an arc past a half turn whose ends
the skew brings within the global point tolerance of each other. Under a transformation that carries a circle without
being an exact turn, a turn whose numbers are rounded to six places, such an arc is moved whole as the whole turn is.

**Mirroring turns shapes round.** A polygon that ran counter-clockwise comes back clockwise, its
`SignedArea` changes sign and its `Area` does not. `GetDeterminant` says so in advance: it is the factor
areas are multiplied by, negative when the transformation reverses winding.

`Inverse` judges the determinant against the size of the transformation rather than against zero, so a
drawing scaled down by a thousandth still inverts cleanly while two axes turned nearly onto each other do
not. `TryGetInverse` reports that instead of throwing.

## Asking where something is

Beyond distance and containment, the plane half answers the questions the solid half answers about a
plane, read against a line instead.

```csharp
var cut = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));

point.GetSideOf(cut);                          // LineSide.Left, Right or On
Containment2.GetSide(cut, point);              // the same, named the other way round

first.IsCodirectionalTo(second);               // parallel is not enough: the same way along the line
Projection2.ProjectToInfiniteLine(cut, point); // the foot of the perpendicular, beyond the ends if need be
```

`GetSide` reads a segment as the infinite line carrying it, so a point past either end still has a side;
reversing the segment swaps left and right. `ProjectToInfiniteLine` is `ProjectToLine` without the clamp
to the segment, which is what to use when a segment stands for the line it lies on.

Every region reports its measurements as properties, named alike whatever the shape: `Area`, `Length`,
`Centroid`, and `SignedArea` and `IsClockwise` where winding means something.

```csharp
polygon.Area;  polygon.SignedArea;  polygon.IsClockwise;  polygon.Centroid;
face.Area;     face.Length;         face.Centroid;
rectangle.Area;                     circle.Area;  circle.Length;
```

## Flat shapes that live in space

A plate or a face in a 3D model is flat, so everything here applies to it: lay it out in its own plane
with `PlanarMap`, work on it as an ordinary `GeoPolygon2` or `GeoFace2`, and put the answer back. See
[between the plane and space](solid.md#between-the-plane-and-space).

## Point chains

`GeometryHelper.Extension` covers the step before a polyline exists: a raw list of points,
usually read out of a drawing and carrying more of them than the geometry needs.

```csharp
using GeometryHelper.Extension;

var traced = new List<GeoPoint2>
{
    new GeoPoint2(0, 0), new GeoPoint2(0.0001, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5),
};

// The second point is a hair away from the first and goes.
List<GeoPoint2> thinned = traced.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001)); // 3 points

// One segment per consecutive pair.
List<GeoLine2> segments = thinned.ToGeoLine2s();                                           // 2 segments
```

`RemoveConsecutiveNearPoints` compares each point against the last one *kept*, not against its original
neighbour, which is what guarantees no two points of the result are coincident within the tolerance. A
huddle collapses onto its first point, and collapsing stops as soon as one point escapes the tolerance
around that anchor, so a long run thins rather than vanishes.

The first point always survives; the last one is not privileged. A final point lying within the tolerance
of the one kept before it is dropped like any other, so re-append it yourself when the endpoint matters.

`ToGeoLine2s` leaves the chain open — nothing joins the last point back to the first, so a ring has to
repeat its first point at the end. It does not filter coincident neighbours either, so run
`RemoveConsecutiveNearPoints` first if zero length segments would be a problem.

## Tolerance

`Tolerance` and `Tolerance.Global` are [shared types](common.md): a program working in the plane and in
space sets one tolerance, not two.

`Tolerance.Global` has a static setter, deliberately mirroring
`Autodesk.AutoCAD.Geometry.Tolerance.Global`. Changing it affects the whole application, so set it
once at startup.

> Importing `Autodesk.AutoCAD.Geometry` alongside this library brings a second `Tolerance` into scope
> and the two tie, giving **CS0104**. Name the one you mean:
> `using Tolerance = GeometryHelper.Tolerance;`.

## Build and Test

```bash
dotnet build src/GeometryHelper/GeometryHelper.csproj
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
```

## Licence

MIT. Clipper2, which this package carries compiled in, is under the Boost Software License 1.0.
