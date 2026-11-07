# Arcs


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
[Moving geometry](moving.md#moving-geometry)).

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

## Chains that curve

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

## Asking a curved chain the usual questions

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

The centroid weighs every vertex and every arc, however near or small: at a millionth, the rectangle 100 by 50 with its
corner (100, 50) rounded by 0.0005 has its centroid at (50, 25) to a billionth, where the vertices were merged within
the global tolerance, the end of the arc went, and the centroid was 0.00008 off.

A cut inside an arc leaves two arcs of the same radius, so the pieces put back end to end draw exactly
what went in. Cutting a loop gives open chains, and the run after the last cut carries on through the
vertex the loop happened to be held from.

`Locate` on a curved loop is worth a word, because it is exact with no arc cut up anywhere in it: it is
the straight loop through the vertices, turned inside out once for every piece an arc cuts off its own
chord that the point lies in. That one rule covers an arc bulging out, an arc bulging in, and an arc
sweeping more than half a turn.

## Offsetting without straightening

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
straight offsets do, however the hole's pieces meet. A loop is put together edge for edge, its corners counted within
the tolerance asked: at a millionth, the rectangle 100 by 50 grown by 0.0004 with round joins keeps its four arcs, 0.00057
across, where the global tolerance of a thousandth dropped them. What a miter fills is read within the tolerance asked
too, however small the miter: a chain of nine corners some 0.005 across, grown by 0.00137 with miters at a billionth, is
one chain, as the same chain a million times larger is within a thousandth, where a stray piece 0.00004 long was kept
inside a miter smaller than the global tolerance. A side shorter than a hundredth of a millimetre moves with the rest: at
a millionth, the square 10 by 10 at the origin with its corner (0, 0) cut off by a side 0.000007 long, grown by a half
with miters, keeps that side, 0.41422 long now, where the side had no direction within the global vector tolerance of
1E-5, stayed where it was, and the corner came out square. An offset by less than 1E-5 is squared off past the miter
limit and cut by its chamfers as a larger one is: at a billionth, the square of side 0.00001 grown by 0.000002 with
chamfers is cut 0.000002 from every corner, where the ways out from the corner to the moved ends had no direction
within the vector tolerance and the corners were cut across their chords. A miter meets a straight piece and an arc's
circle where they cross, or where the line misses the circle by no more than the tolerance, alike at any size: the chain
(-0.001165, -0.002711), (0.00062, -0.001059), (0.002589, 0.00029), (0.000415, 0.000801), (-0.00214, 0.003174),
(-0.001103, 0.000257), its third and fourth edges bowed by 0.24 and -0.24, offset by -0.0000702 with miters at 1.5 within
2E-6, is one chain of eight corners, as the same chain 64 times larger is, where its line missed the moved arc by 0.00014,
seventy times the tolerance, and was taken to touch it, the miss read as an area, and the chain broke in two.

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

## An index over the edges

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

**Every edge of an index is a side of a shape.** Two indexes are measured, and collide, edge against edge as the
shapes they were built from measure their sides: each the side it is however short, never a segment read as its
start, and the tolerance changes nothing in the distance. At a thousandth, the index over the chain coming down from
(5, 5) to (5, 0.0015) and on to (5, 0.0006) is 0.0006 from the one over the segment from the origin to (10, 0), and
collides with it, as the two chains do. The closest point and the distance from a point read a straight edge so too,
as a curved chain does, and an arc within the tolerance: at a hundredth, the index over the polygon (0, 0), (10, 0),
(10, 0.006), (0, 10) gives (10.03, 0.003) the closest point (10, 0.003), 0.03 away, where its edge 0.006 long offered
its start (10, 0), 0.0301496 away.

## Flattening

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

A loop or a chain so small that the merge would leave too few corners to be a polygon or a chain keeps every corner of
its chords instead: the square 0.0005 by 0.0005 flattens to its four corners, and with its sides bowed by a bulge of 0.2
to the corners of their chords, where both were refused for having fewer than three.
