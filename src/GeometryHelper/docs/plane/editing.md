# Extending, offsetting and combining

The drafting operations of the plane: extending and trimming lines, offsetting curves and regions, combining regions,
cutting and rounding corners, hulls.

## Extending and trimming

[`Lengthen2`](xref:GeometryHelper.Core.Lengthen2) changes how long a segment is while keeping it on its own line, as AutoCAD's LENGTHEN, EXTEND and TRIM do. A segment has no picked point to say which end is meant, so each method takes a [`LineEnd`](xref:GeometryHelper.Enums.LineEnd); the other end never moves, and the segment never turns round.

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

### Arcs, chains and edges

A segment is not the only thing that can be lengthened. An arc is lengthened
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

[`Offset2`](xref:GeometryHelper.Core.Offset2) moves curves sideways and grows or shrinks regions, as AutoCAD's OFFSET does.

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

| [`OffsetJoin`](xref:GeometryHelper.Enums.OffsetJoin) | Corner | AutoCAD |
|---|---|---|
| `Miter` (default) | the offset edges carried on until they meet | Extend |
| `Round` | an arc about the original vertex, drawn as chords | Fillet |
| `Chamfer` | one segment, square to the corner's bisector at the offset distance | Chamfer |

[`OffsetOptions`](xref:GeometryHelper.OffsetOptions) sets the rest: `MiterLimit` (default 10) cuts a sharp corner off square once it would reach that many distances from its vertex, and `ArcTolerance` bounds the gap between a round corner's chords and the true arc (by default 0.2 % of the distance).

### What comes back

The offset of a region is exact, not the edges moved one by one, so a polygon does not always give one polygon back. `polygon.Offset` returns the boundary loops of the result: a shrinking polygon that pinches off gives one per piece, one that shrinks past its own width gives none, and a growing polygon that closes a gap around empty space gives that space as a hole, wound the other way so that signed areas add up. `face.Offset` on a [`GeoFace2`](xref:GeometryHelper.Geometry.GeoFace2) returns faces with their holes attached, and its holes shrink as the boundary grows. A polyline's parallel curve has the loops a tight turn would make cut away, and comes back in pieces when a turn cuts it clean through. A [`GeoPolyline2`](xref:GeometryHelper.Geometry.GeoPolyline2) gives the edge of the band it sweeps sideways, so a strip that crosses another leg of the polyline stays; a curved chain, offset as a [`GeoPolylineArc2`](xref:GeometryHelper.Geometry.GeoPolylineArc2), keeps only what stands exactly the distance from it. At a turn on the inside sharper than a right angle, the stretch of a moved segment that runs on past the other leg goes with the loop. Where the band's edge lies along another part of the polyline, within the tolerance, it is the polyline itself and goes too: the step (0, 0), (10, 0), (10, 1), (−5, 1) offset by 1 gives only (0, 0) to (−5, 0). Pieces come in order by where most of each lies. [`GeoCircle2`](xref:GeometryHelper.Geometry.GeoCircle2) and [`GeoRectangle2`](xref:GeometryHelper.Geometry.GeoRectangle2) offer `TryOffset`, which keeps them what they are.

## Combining regions

[`Boolean2`](xref:GeometryHelper.Core.Boolean2) combines regions, as AutoCAD's UNION, INTERSECT and SUBTRACT do. A polygon is read under the even-odd rule, as `Contains` reads it, and a face as its boundary less its holes. The answer is always a set of faces, largest first, with boundaries counter-clockwise and holes clockwise.

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

Region operations, the booleans and the offsets alike, are resolved by [Clipper2](https://github.com/AngusJohnson/Clipper2), which this package carries compiled in. It works on integers, so an answer never depends on rounding luck; the library lays the shapes out in a frame at the first of them before rounding, and gives every vertex of the answer its full precision back afterwards, so a square offset by 2 ends exactly on 2. Vertices closer than the point tolerance are merged, and slivers thinner than it are dropped. Many corners on one line, the points of a zigzag or the teeth of a comb, cost no more than as many anywhere else: a zigzag of 100 000 vertices is united in 0.11 seconds and a comb of 25 000 teeth in 0.09, where Clipper2 as it comes takes 5.9 and 7.4, with the same answer to the point. Shapes joined into the answer one after another, squares standing on a strip, no longer walk the chain of all those joined before them: 50 000 are united in 1.1 seconds, where they took 10.6. Minima at many heights cost more, since every scanbeam walks every edge, but no longer a sort of the edges where none cross between them: a zigzag of 400 000 vertices with its minima at 50 heights and its maxima on one line is united in 1.6 seconds, where it took 5.8, three times its 0.5 with its minima on one line, and with its maxima at 30 heights as well in 7.9, where it took 15.8. Giving the answer its digits back reads only the input edges and vertices near each point, so it no longer grows with the square of the edges: a comb of 40 000 teeth pointing down from its back is offset by a half in 0.8 seconds, where it took 4.4. With its teeth pointing up it still takes half a minute, in Clipper2's own union of it. Long strips laid on a slant, each end crossing the long sides of a thousand others, likewise stay slow in that union: 8 000 of them take about 20 seconds, 16.5 of them in Clipper2.

## Triangles and meshes

[`GeoTriangle2`](xref:GeometryHelper.Geometry.GeoTriangle2) is the triangle of the plane, as [`GeoTriangle3`](xref:GeometryHelper.Geometry.GeoTriangle3) is the triangle of space. It has its signed area and
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

`TriangulateSurface` on [`GeoPolygon2`](xref:GeometryHelper.Geometry.GeoPolygon2), `GeoFace2`, [`GeoPolygonArc2`](xref:GeometryHelper.Geometry.GeoPolygonArc2), `GeoRectangle2` and `GeoCircle2` breaks the shape
into triangles that lie within its material, holes left open, all counter-clockwise. `ToMesh` breaks it into the faces
of a [`GeoMesh2`](xref:GeometryHelper.Meshing.GeoMesh2) instead: triangles, the cells of a grid as formwork panels or tiles are laid, strips, or convex pieces.
[Meshing the plane](../mesh.md) tells how.

## Cutting corners

[`Corner2`](xref:GeometryHelper.Core.Corner2) chamfers a corner: one straight cut across it, measured back along each of the two edges that
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

### Corners that are left alone

A cut has to fit on the edges it is measured along, and neighbouring
corners eat into the edge between them:

| Left alone when | Why |
|---|---|
| the cut is longer than an edge beside it | there is nothing to measure along |
| two neighbours together ask for more than the shared edge is long | the one taking more of that edge is dropped, which may leave room for the rest |
| the corner is straighter than `Tolerance.EqualAngleRad` | the cut would be an edge of zero length |

What was skipped, and why, is written to [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog). `TryChamferAt` reports `false` instead, for
a caller who needs to know about one corner in particular. Every cut is measured on the shape as it came
in, never on a shape half cut already, so the answer does not depend on which vertex the walk began at.

The shape is put back together within the tolerance asked, as a curved one is, so a cut longer than the tolerance
stays and one no longer than it is no edge, the shape turning where the cut starts. At a millionth, the chain (0, 0),
(10, 0), (10, 10) cut by 0.0003 turns at (9.9997, 0) and (10, 0.0003), and the triangle through the same corners cut
by 0.0003 has six corners, where they were put back together within the global tolerance of a thousandth, the two
ends of each cut were one corner, and the chain turned at (9.9997, 0) alone and the triangle came back three. Within a
tenth, the chain cut by 0.03 turns once, at (9.97, 0), where its cut 0.042 long was kept.

### Rounding them instead

A straight chain or a straight polygon can be rounded as well as chamfered. A
rounded corner is an arc, so the answer is the curved type — `GeoPolylineArc2` for a chain,
`GeoPolygonArc2` for a loop — and the rules for which corners are left alone are the chamfer's.

```csharp
plate.Fillet(30.0);                     // GeoPolygonArc2: every corner rounded by thirty
plate.Fillet(new[] { 20.0, 0.0, 20.0, 0.0 });   // a radius each; nought leaves that corner square
chain.TryFilletAt(1, 50.0, out GeoPolylineArc2 rounded);
```

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

### One radius per corner

`Fillet` also takes a list, read the way `GetBulgeAt` is read: the entry at an
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
arc of 144.5°. A radius at which the straight edge's parallel line only touches the arc's moved circle, or misses it,
would round the corner by half a circle, and the corner is left square alike at any size: the cap of the circle of
radius 12 above a chord 4 from its centre is rounded where the chord runs into the arc at 3.5, and left square at
4.000025 within 1E-4, as the same cap 1 000 times smaller is within 1E-7, where the small one was rounded by a half
circle. So is a corner between two arcs whose circles, moved in by the radius, only touch, and a touch is read to the
rounding of the numbers, 1E-12 of the largest coordinate or radius, not to nought: the arcs of radii 10 and 7 about
centres 12 apart, filleted where they cross at 2.5, the radius of the circle filling the lens between them, are left
square at any size, where they were rounded by a half circle at that size and left square at a thousandth of it, and
the cap filleted at exactly 4 is left square, where it was rounded by a half circle at a thousand times its size.

Exactly enough is enough: a square of side 100 filleted at 50 loses every straight edge and comes back
four quarter turns — a circle. Chamfered at 50 it comes back the diamond through the four midpoints. So is
a hair more or less, within the tolerance: where the corners at an edge take all of it, give or take the
tolerance, the edge vanishes and the pieces either side meet at one point, midway along it, or at its far
end when only one of its corners was reshaped. The two ends of a chain never move. Past the tolerance the
corner taking more gives way, as above. The square of side 10 filleted at 5.02 at a tolerance of 0.1 is the
circle of radius 5 through the midpoints of its sides. A loop that would keep fewer than three corners is
given back as it was.

A loop keeps every arc however small, and every cut and side the tolerance keeps, as a chain does; only its corners
are counted within the tolerance. The rectangle 100 by 50 filleted at 0.0005 is eight edges, four of them arcs 0.0007
across, where the loop was put back together within the global tolerance and came back its four corners, 0.0625 square
units short. A straight chain or loop is rounded corner for corner, a leg shorter than the global tolerance and all, so
a corner is named by its vertex: at a millionth, the chain (0, 0), (10, 0), (10.0004, 0.0004), (10.0004, 10), (0, 10)
rounded by 1 at vertex 3 turns at (10.0004, 10), where the short leg was dropped first and vertex 3 was the chain's end.
A side shorter than a hundredth of a millimetre is a side to round beside like any other: at a millionth, the square 10
by 10 at the origin with its corner (0, 0) cut off by a side 0.000007 long is rounded by 0.00001 at (0.000005, 0), where
the side had no direction within the global vector tolerance of 1E-5, no circle was found touching it, and the corner
was left alone.

## See also

- [Geometric types](types.md).
- [Arcs](arcs.md): offsetting chains that curve.
- [Meshing the plane](../mesh.md).
