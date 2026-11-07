# Splitting, extending and offsetting

## Splitting

[`Splition3`](xref:GeometryHelper.Core.Splition3) cuts a curve at a position along it or wherever a plane crosses it, and cuts a region or a
body by a plane. Pieces of a curve come back in order along the subject, so the first piece always holds
its start point and the last holds its end point. Every overload reports `false` when there was nothing to
cut and still hands back the subject as a single piece, so the result is usable either way. Each split is
also reachable as an instance method on the shape being cut — it sits on the subject, since
`plane.Split(line)` would not say which of the two comes back in pieces.

```csharp
var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

line.TrySplitAtDistance(4, out GeoLine3[] pieces); // true: lengths 4 and 6
line.TrySplitAtDistance(0, out _);                 // false — a cut at an endpoint makes no piece
line.TrySplitBy(new GeoPoint3(5, 3, 0), out _);    // false — the point is not on the segment
```

An arc is cut the same way, and keeps its centre, its radius and its plane — only the sweep is shared out,
so the pieces put back end to end draw exactly what went in:

```csharp
GeoArc3 bend = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(71, 71, 0), new GeoPoint3(0, 100, 0));

bend.TrySplitAt(0.25, out GeoArc3[] quarters);                   // at a parameter
bend.TrySplitAt(bend.GetPointAtParameter(0.5), out _);           // at a point, or the place nearest one
bend.TrySplitAtDistance(50.0, out _);                            // at a length along the curve
```

A cut within the tolerance of an end leaves the arc whole. At a tolerance of nought that means a cut within a
rounding of the end: the arc of radius 100 sweeping 1 radian from 0.063, cut at 0.9999999999999999, stays whole
rather than coming apart into itself and a whole turn. Three points have no arc through them where the middle one lies
within the point tolerance of the line through the other two, or the triangle they make has twice an area of no more
than the square of the point tolerance, as in the plane: (0, 0, 0), (100, 0.01, 0) and (200, 0, 0) give the arc of
radius 500 000 at a thousandth and are refused at 0.05. An arc that would miss one of its three points by more than the
tolerance is refused too. An arc in space starts at an angle from nought up to a full turn, however it was made.

A cutter does not have to be a plane. Cutting by a **closed body** sorts the pieces into those inside it
and those outside, because a body has no side the way a plane does:

```csharp
var chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

chain.TrySplitBy(solid, out GeoPolyline3[] inside, out GeoPolyline3[] outside);
// inside holds the stretch within the body, outside the two stretches beyond it
```

The same works against a [`GeoObb3`](xref:GeometryHelper.Geometry.GeoObb3) or a [`GeoAabb3`](xref:GeometryHelper.Geometry.GeoAabb3), which are cheaper because the crossings come from the
slab test rather than from walking a surface. Neighbouring pieces that end up on the same side are joined
back up, so what comes out is the longest run on each side rather than a chain chopped at positions that
separate nothing — a chain running down the shaft of an opening meets the caps at each end without ever
entering material, and that is not a cut.

Several bodies can be given at once, and they behave as their **union**: a stretch is inside when any one
of them holds it. Cutting by each in turn would not do the same thing, because the pieces of the first cut
would have to be sorted again against the second and the runs joined back up by hand.

```csharp
var route = new GeoPolyline3(new GeoPoint3(-10, 5, 5), new GeoPoint3(60, 5, 5));

route.TrySplitBy(new[] { beam, slab }, out GeoPolyline3[] embedded, out GeoPolyline3[] clear);
// embedded holds the stretches buried in either body, clear the stretches in the open
```

Because it is the union being asked about, two bodies that overlap do not each claim their own piece of
the answer, and two meeting face to face leave no cut between them — the run through both comes back
whole. An empty array is the same rule at its limit: nothing holds anything, so the whole chain is clear.

An array of `GeoObb3` or of `GeoAabb3` works the same way and is the cheaper form where the cutters really
are boxes. A `GeoObb3` is a reference, so a null entry is skipped; a `GeoAabb3` is a value with no null to
pass, and the entry left out there is the empty box, which could hold nothing anyway.

Cutting by a **bounded region** cuts only where the subject really goes through it, which is what is wanted
when the cutter stands for a physical plate rather than an endless surface:

```csharp
chain.TrySplitBy(plate, out GeoPolyline3[] pieces);          // only where it pierces the plate
chain.TrySplitBy(plate.GetPlane(), out GeoPolyline3[] more); // anywhere it crosses the carrying plane
```

Cutting a region sorts the result by side, and a concave subject can fall into more than two pieces, which
is why each side comes back as an array:

```csharp
// A U shape opening upwards, cut horizontally through its two arms.
var uShape = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(9, 0, 0), new GeoPoint3(9, 10, 0),
    new GeoPoint3(6, 10, 0), new GeoPoint3(6, 4, 0), new GeoPoint3(3, 4, 0),
    new GeoPoint3(3, 10, 0), new GeoPoint3(0, 10, 0));

var cutter = new GeoPlane3(new GeoPoint3(0, 7, 0), GeoVector3.YAxis);

uShape.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below);
// above.Length == 2 — one piece per arm
// below.Length == 1
```

A solid is cut the same way, and the two halves come back closed:

```csharp
GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();

cube.TrySplitBy(GeoPlane3.XY.Offset(4), out GeoSolid3 upper, out GeoSolid3 lower);
// upper.GetVolume() == 600, lower.GetVolume() == 400, both IsClosed()
```

The body may be concave and its faces may carry holes. Each face is cut on its own, and the new surface
closing each half is built from the edges the cut left behind: in a closed body every edge is traversed
once by the face beside it, so the same edge traversed the other way belongs to the cap. That section may
be several loops, and one loop may sit inside another, so the loops are nested into faces rather than
assumed to be a single boundary:

```csharp
// A square tube — a block with a shaft through it. The section across it is a ring, not a disc.
tube.TrySplitBy(GeoPlane3.XY.Offset(5), out GeoSolid3 upper, out GeoSolid3 lower);
// each half is closed, and its cap is one face carrying one hole
```

Which loop lies inside which is asked only of loops whose boxes meet, so many pieces cost little more than
a few: a comb face of 5 000 teeth cut across them comes back in its 5 001 pieces in 0.05 seconds, where
asking every pair took 1.2.

Two consequences worth knowing. A cut can leave one half as **several disconnected shells** — a plane
across both arms of a U leaves two bodies above it — and they come back as one [`GeoSolid3`](xref:GeometryHelper.Geometry.GeoSolid3), which measures
and answers containment correctly because each shell is closed and wound outwards. And a plane passing
through a **hole** in a face turns that rim into part of the boundary, so the piece on each side has no
hole where the subject had one.

A body that closes as `Validate` reads it comes apart into two halves that close so too, or `TrySplitBy` returns
`false`. A plane a hair over the tolerance inside a face, and all but parallel to it, cuts off a slab little thicker
than the tolerance, every edge down its sides about the tolerance long, and cut within the tolerance such a slab can
come out open. The body is then cut again within a tenth of the tolerance, and then a hundredth, and failing both the
slab is welded closed; the halves are taken only where they hold the body within the tolerance times the section. A
hull 20 across cut 0.0011 inside a face gives a slab of 0.022 cubic millimetres, closed, and of 3 000 hulls cut so none
gives an open half, where 79 did, the halves of 77 adding up to the body within 1E-8 cubic millimetres. A body that
does not close is cut as it is.

A plate can also be cut **along a line marked on it**, or **against a body**:

```csharp
plate.TrySplitBy(cutLine, out GeoPolygon3[] halves);              // cut along a drawn chain
plate.TrySplitBy(solid, out GeoPolygon3[] embedded, out GeoPolygon3[] clear);
```

An end of the chain that meets the outline within the tolerance of a corner is cut from the corner, as a polyline cut
within the tolerance of a vertex is, so both pieces keep the corner and they add up to the plate. At a hundredth, the
square 10 by 10 cut from (0.004, 0, 0) to (10, 5, 0) gives the triangle of 25 square units from the corner (0, 0, 0) and
the rest, of 75; the corner was cut off, and a sliver of 0.02 square units was in neither piece. Of two corners within
the tolerance the nearer is taken, and a chain whose two ends land on one corner is refused.

The second answers which part of a plate is embedded in a body. It works by cutting the plate with the
plane of each face of the body in turn — the surface of the body never leaves those planes, so once that is
done no piece can straddle the boundary — and then joining the pieces back up where they agree. What comes
back covers each side exactly, though it may be in more pieces than strictly necessary.

## Extending and trimming

[`Lengthen3`](xref:GeometryHelper.Core.Lengthen3) changes how long a segment is while keeping it on its own line, as AutoCAD's LENGTHEN, EXTEND
and TRIM do. A segment has no picked point to say which end is meant, so each method takes a [`LineEnd`](xref:GeometryHelper.Enums.LineEnd);
the other end never moves, and the segment never turns round.

```csharp
var beam = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

beam.Extend(5.0, LineEnd.End);           // (0,0,0) -> (15,0,0); a negative distance shortens
beam.Extend(2.0, 3.0);                   // both ends at once: (-2,0,0) -> (13,0,0)
beam.ExtendToLength(8.0, LineEnd.End);   // (0,0,0) -> (8,0,0), LENGTHEN Total

var wall = new GeoPlane3(new GeoPoint3(20, 0, 0), GeoVector3.XAxis);
beam.TryExtendTo(wall, LineEnd.End, out GeoLine3 reached);   // (0,0,0) -> (20,0,0)
```

`TryExtendTo` only ever lengthens, out to the nearest place the line meets the boundary; `TryTrimTo` only
ever shortens, back to the nearest crossing still within the segment. The boundary can be a point, a
segment, a plane, a polygon, a face or a solid — a solid is met at its faces, so a segment running towards
a body stops at its surface and one running out of a body is cut where it leaves it. An end already on
the boundary within tolerance satisfies both, so `line.TryExtendTo(b, end, out fit) ||
line.TryTrimTo(b, end, out fit)` fits an end to a boundary whichever side of it the end starts on.

In space two segments almost never cross, so `TryTrimExtendToCorner` — FILLET with a radius of zero —
asks that they pass within `EqualPoint` of each other. Where they do, each keeps its longer part and both
end at the meeting point; where they pass wider than that, it reports `false` rather than inventing a
corner one of them does not touch.

**Arcs, chains and edges.** An arc is lengthened **along itself** — the centre, the radius and the plane
stay, the sweep grows, and the distance asked for is arc length and not chord. A chain is lengthened by its
**end leg**, along that leg, which is how a bar's end is pulled out for anchorage without disturbing a bend.

```csharp
GeoArc3 bend = GeoArc3.FromThreePoints(new GeoPoint3(100, 0, 0), new GeoPoint3(71, 71, 0), new GeoPoint3(0, 100, 0));

bend.Extend(50.0, LineEnd.End);                 // fifty more of curve, same centre, radius and plane
bend.ExtendToLength(200.0, LineEnd.Start);      // two hundred of curve, end where it was
bend.TryTrimTo(bend.GetPointAtParameter(0.5), LineEnd.End, out GeoArc3 half);

GeoPolylineArc3 bar = new GeoPolyline3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0)).Fillet(50.0);

bar.Extend(300.0, LineEnd.End);                 // the last leg carries on; the bend keeps its radius
bar.ExtendToLength(1000.0, LineEnd.Start);      // a thousand long, measured along the arcs
bar.TryTrimTo(bar.GetPointAtDistance(200.0), LineEnd.End, out GeoPolylineArc3 back);
```

A whole turn is the limit for an arc, so an extension past it is refused rather than wrapped. **A chain is
carried outwards only** — shortening one belongs to the splitting family, which keeps its bends, and
`TryTrimTo` is that cut with the end named rather than the piece. A [`GeoEdge3`](xref:GeometryHelper.Geometry.GeoEdge3) answers both ways too:
straight on if it is a leg, round if it is a bend.

## Offsetting

[`Offset3`](xref:GeometryHelper.Core.Offset3) moves a segment sideways and grows or shrinks a flat region within its own plane.

A segment in space has no left until a plane is named, so every line offset takes a direction as well as
a distance:

```csharp
var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

line.OffsetInPlane(2.0, GeoVector3.ZAxis);            // (0,2,0) -> (10,2,0): left, seen from the normal
line.Offset(2.0, new GeoVector3(0, 1, 1));            // towards a direction; only its square part counts
line.Offset(lateral: 2.0, vertical: 3.0, up: GeoVector3.ZAxis);  // sideways and up in one call
line.OffsetThrough(new GeoPoint3(3, 7, 0));           // the parallel through a point
```

A region is offset in the plane it already lies in, so the result comes back exactly in that plane:

```csharp
var square = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
    new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

square.Offset(2.0);                       // one square, 14 x 14, sharp corners
square.Offset(2.0, OffsetJoin.Round);     // corners rounded to a radius of 2
square.Offset(-5.0);                      // none: it shrinks away

plate.Offset(-1.0);                       // a GeoFace3 keeps its holes, which grow as the boundary shrinks
chain.OffsetInPlane(1.0, GeoVector3.ZAxis);   // a GeoPolyline3 needs a plane, like a segment
circle.TryOffset(2.0, out GeoCircle3 wider); // a circle stays a circle
```

A flat chain has its plane even when it encloses no area: the Z through (0, 0, 2), (10, 0, 2), (0, 10, 2),
(10, 10, 2) lies in z = 2, and a curved chain whose vertices lie on one line takes the plane its arcs turn in.

The corner joins are the same three AutoCAD's OFFSETGAPTYPE offers — `Miter`, `Round`, `Chamfer`, set
through [`OffsetJoin`](xref:GeometryHelper.Enums.OffsetJoin) or [`OffsetOptions`](xref:GeometryHelper.OffsetOptions), which is documented with the
[shared types](../common.md)
package they come from.

**What comes back.** The offset of a region is exact, not its edges moved one by one, so a polygon does
not always give one polygon back. `Offset` returns the boundary loops of the result: none when it shrinks
past its own width, one per piece when it pinches off, and, where a growing polygon closes a gap around
empty space, that space as a hole wound against its outer loop so that signed areas add up. [`GeoFace3`](xref:GeometryHelper.Geometry.GeoFace3)
returns faces with their holes attached. A polyline's parallel curve has the loops a tight turn would
make cut away and comes back in pieces when a turn cuts it clean through.

The planar work happens in a frame built on the region's own plane, through the same corner construction
the plane library uses, so a plate lying at an angle offsets exactly as its plan view would. The loops
themselves are resolved by the library's own winding-number solver, where the plane half hands that
part to Clipper2. The test suite checks the two against each other. The solver finds where edges cross by
sweeping them along the axis of the frame they overlap least along, so a comb of 4 000 teeth whose teeth
run along the frame's first axis is offset in 0.14 seconds, where sweeping along the teeth took 4.7.
