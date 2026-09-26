# Geometry in the plane

2D geometry for engineering drawings: points, vectors, lines, polylines, polygons, faces with holes,
circles and oriented rectangles, with distance, projection, containment, intersection, collision,
parallelism, parametrization, merging and splitting over them, and the drafting operations on top:
extending and trimming lines, offsetting curves and regions, and combining regions. Every comparison
is tolerance-aware.

[Geometry in space](solid.md) is the 3D counterpart, built to the same design, and the two are
measured with the same [shared types](common.md).

## Namespaces

| Namespace | Holds |
|---|---|
| `GeometryHelper.Geometry` | `GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoPolyline2`, `GeoPolygon2`, `GeoFace2`, `GeoCircle2`, `GeoRectangle2`, `GeoTransform2` |
| `GeometryHelper.Core` | `Boolean2`, `Collision2`, `Containment2`, `Corner2`, `Distance2`, `Intersection2`, `Lengthen2`, `Merge2`, `Offset2`, `Parallel2`, `Parametrization2`, `Projection2`, `Splition2`, `PlanarMap` |
| `GeometryHelper.Extension` | `EnumerableExtension` |
| `GeometryHelper` | `Tolerance`, `Angle`, `OffsetOptions`, `GeometryHelperLog` |
| `GeometryHelper.Enums` | `PointLocation`, `LineSide`, `LineEnd`, `LineExtension`, `OffsetJoin` |

Every type carries a `2`, matching the `3` on the solid types, so a program working in both dimensions
imports one namespace and nothing collides.

## Geometric Types

`GeoPoint2`, `GeoVector2`, `GeoLine2`, `GeoArc2`, `GeoCircle2`, `GeoRectangle2` (rotated rectangle — OBB), `GeoPolygon2`, `GeoFace2` (a polygon with holes), `GeoPolyline2`, and the chains that may curve: `GeoEdge2`, `GeoPolylineArc2`, `GeoPolygonArc2`.

### Regions and curves

The shapes split into two families, and the distinction decides what you can ask of them:

| Family | Types | Encloses an area |
|---|---|---|
| Region | `GeoCircle2`, `GeoRectangle2`, `GeoPolygon2`, `GeoFace2` | yes |
| Curve | `GeoLine2`, `GeoArc2`, `GeoPolyline2`, `GeoPolylineArc2` | no |
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
exactly as long as the distance.

```csharp
var plate = new GeoRectangle2(0, 0, 100, 100);
var hole = new GeoCircle2(new GeoPoint2(50, 50), 10.0);

plate.DistanceTo(hole);                 // 0.0  — the circle is inside the plate
plate.GetShortestLineTo(hole).Length;   // 40.0 — out to the nearest side
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

**What comes back.** The offset of a region is exact, not the edges moved one by one, so a polygon does not always give one polygon back. `polygon.Offset` returns the boundary loops of the result: a shrinking polygon that pinches off gives one per piece, one that shrinks past its own width gives none, and a growing polygon that closes a gap around empty space gives that space as a hole, wound the other way so that signed areas add up. `face.Offset` on a `GeoFace2` returns faces with their holes attached, and its holes shrink as the boundary grows. A polyline's parallel curve has the loops a tight turn would make cut away, and comes back in pieces when a turn cuts it clean through. `GeoCircle2` and `GeoRectangle2` offer `TryOffset`, which keeps them what they are.

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

Region operations, the booleans and the offsets alike, are resolved by [Clipper2](https://github.com/AngusJohnson/Clipper2), which this package references. It works on integers, so an answer never depends on rounding luck; the library lays the shapes out in a frame at the first of them before rounding, and gives every vertex of the answer its full precision back afterwards, so a square offset by 2 ends exactly on 2. Vertices closer than the point tolerance are merged, and slivers thinner than it are dropped.

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
trip through a drawing is exact.

`Arc2` holds the operations, and they mirror the ones for a line: `ProjectToArc`, `DistanceTo` (to a
point, a segment or another arc), `IsPointOn`, `Locate`, `TryIntersectWith` and `GetIntersections`,
`TrySplitAt`. Moving an arc is `Translate`, `RotateBy` and `TransformBy`; scaling it evenly is fine, and a
transformation that would make it an ellipse is refused rather than approximated.

`GeoArc3` is the same arc in its own plane, carrying a `Normal` as well. It adds `GetAabb()`, which is
exact rather than the box round the whole circle, and `ProjectToArc2(frame)` to bring it into the plane.

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
across, which is what AutoCAD's OFFSET does. Where they would never meet, or meet further away than
`OffsetOptions.MiterLimit` allows, it cuts straight across instead. Shrunk far enough a
shape can fall into several pieces or vanish, so the answer is an array.

What tells the folded-over parts from the real ones is the one thing an offset cannot break: every point
of a valid offset stands **exactly** the offset distance from the shape it came from. A piece standing
nearer has been folded over by a neighbour and goes. No winding rule, no clipper, no tessellation.

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
GeoBvh2 index = GeoBvh2.FromPolygonArc(cog);          // or FromPolygon, FromPolyline, FromPolylineArc

index.GetClosestPoint(point);
index.DistanceTo(point);                              // to the edges, not to the region they enclose
index.GetIntersections(knife);
index.DistanceTo(other);
index.CollidesWith(other);
```

It is worth building when the same shape is asked many questions; for a handful, the plain methods on the
shape are quicker, because they build nothing.

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
| either edge is already an arc | a circle tangent to two curves has several answers, and picking one is not this method's business |

What was skipped, and why, is written to `GeometryHelperLog`.

Exactly enough is enough: a square of side 100 filleted at 50 loses every straight edge and comes back
four quarter turns — a circle. Chamfered at 50 it comes back the diamond through the four midpoints.

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
frame is rigid by construction, and reading it backwards is turning the axes round.

```csharp
var frame = new GeoCoordinateSystem2(origin, xAxis);   // or (origin, angleRad)

frame.ToGlobal(local);                                 // place geometry built about the origin
frame.ToLocal(world);                                  // read it back, no matrix inverted
frame.ToTransform();                                   // the same placement as a GeoTransform2

plate.CoordinateSystem.ToLocal(point);                 // a rotated rectangle carries its own frame
new GeoRectangle2(frame, 80.0, 40.0);                  // and can be built from one
```

`GeoRectangle2` is the rotated rectangle — the OBB of the plane — so it carries a frame exactly as
`GeoObb3` does in space.

A loop can be turned round with `Reverse()`, which is what decides winding and which way round a pair of
chamfer distances goes.

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
same amount; under a scaling that differs between the axes it would be an ellipse, which this library has
no type for, and the attempt is refused rather than answered with an averaged radius. A rectangle is
refused the same way when it would come out a parallelogram — transform `rectangle.ToPolygon()` when that
is what you want.

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

MIT. Clipper2, which this package references, is under the Boost Software License 1.0.
