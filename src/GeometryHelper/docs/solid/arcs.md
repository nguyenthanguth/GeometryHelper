# Arcs in space


A `GeoPolyline3` is straight by definition, so a chain that curves is its own type, exactly as it is in the
plane. `GeoEdge3` is one piece of it, and it carries the one thing space needs that the plane does not:

| | Stored per piece |
|---|---|
| `GeoEdge2` | `StartPoint`, `EndPoint`, `Bulge` |
| `GeoEdge3` | `StartPoint`, `EndPoint`, `Bulge`, **`Normal`** |

The bulge keeps its meaning — the tangent of a quarter of the swept angle — and the normal says which plane
it bulges in. A chord and a bulge alone are satisfied by an arc in any of the planes through that chord, so
without the normal there is no one arc to mean. A positive bulge sweeps counter-clockwise about the normal.

```csharp
var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0));
var bulged = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, new GeoVector3(0, 0, 1));

bulged.IsArc;       // true
bulged.ToArc();     // the GeoArc3 it draws, radius 50
bulged.GetChord();  // the segment across it
bulged.GetPlane();  // the plane it bulges in
```

Any bulge but nought is an arc, however little it bows, until it bows less than its arc can place its points to:
an edge 0.01 long with a bulge of a thousandth stands 0.000005 off its chord, well inside the point tolerance, and
is measured, cut, moved and asked about as the arc it is. Its `GeoArc3` is built from its circle, and a move is the
arc through where its two ends and its middle land, that middle worked out across the chord; neither is refused for
three points lying within a tolerance of one line. A move that stretches the arc's plane unevenly makes part of an
ellipse of it, and the arc through those three points stands for it, as it always has. An arc is kept as its centre
and radius, so it places its points within a few times its radius times 1E-16 of where they are, and an edge whose sag
is no more than 2.5 parts in 1E16 of how far its centre and its radius reach from the origin is its chord, `IsArc`
false, and measures, cuts and flattens as the segment, keeping its bulge: the edge 100 long with a bulge of a
billionth, of radius 2.5E10, whose arc placed its points 0.00001 off, and the one with a bulge of 1E-12, whose middle
stood 0.008 off, are segments, and one with a bulge of 1E-7 is an arc. Read so, an edge's middle and length are the
chord's, off the true arc's by no more than its sag, which is less than the rounding of the arc's centre; split, its
pieces are straight, their bulge nought; `DxfWriter` writes it as its chord, with no bulge; and the reading follows how
far the edge reaches from the origin along the axes, so moving or turning an edge close to the threshold can change it.
However short the chord, an edge is built: only two ends that are one point to the last digit are refused, as an edge
in the plane refuses only a chord of nought, so an edge 1E-6 long is built, read and moved, and a piece split off an
arc a hair from its end is an edge, read as its chord where it bows less than its centre can be placed to.

## A bar is a chain with a bend at every corner

This is what the types are for. A reinforcing bar is set out the way a schedule sets it out — the points it
turns at, and a bending radius — and the bar itself is that polyline with a tangent arc at every bend:

```csharp
GeoPolylineArc3 bar = new GeoPolyline3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
    new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300)).Fillet(50.0);

bar.Length;                        // 857.1, walked along the arcs, not 900
bar.IsPlanar();                    // false: the second bend leaves the plane of the first
bar.GetClosestPointOnBoundary(p);
bar.DistanceTo(p);
```

Each bend is rounded in the plane of its own two legs, so the bends need not share a plane, and each arc
carries that plane away with it. A bar is shorter than its set-out by `2r - πr/2` at every bend, which is
the number a bar schedule has to carry.

`DistanceTo` measures each bend exactly, for a point near its centre too: at a tolerance of a thousandth, the chain
(0.01, 0, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, 0, 0) with a bulge of 1 on its middle edge, the half circle of
radius 0.001 through (0, 0.001, 0), is 0.0003 from (0, 0.0007, 0), and the point is on it; cut or trimmed by that
point, the chain comes apart at (0, 0.001, 0). `GetClosestPointOnBoundary` carries a point whose foot falls within the
tolerance of a bend's centre to the bend's start, so its line can be longer than the distance there, by at most twice
the foot's offset. A straight leg is measured as it is however short, as in the plane: the chain built of the straight
edges from (0, 0, 0) to (10, 0, 0) and on to (10, 0.0006, 0) is 0.0009 from (10.0009, 0.0006, 0) at a thousandth, and
the point is on it. `GetClosestPointOnBoundary` and the shortest line from the point read that leg as it is too, and
land on (10, 0.0006, 0), 0.0009 away, where they took its start (10, 0, 0), 0.00108 away; so do `GetDistanceAtPoint`
and `GetParameterAtPoint`. A straight `GeoEdge3` on its own that short is still read as its start.

`Clone` and `Translate` keep a chain edge for edge, every arc and leg however small: at a millionth, the chain
(0, 0, 0), (10, 0, 0), (10, 0, 10) filleted at 0.0005 is copied and moved with its arc 0.0007 across, where it was put
back together within the global tolerance of a thousandth and came back turning at a corner.

`Flatten` of a chain or a loop in space throws the arcs away and keeps the chords, each arc the straight side between its
ends, so it keeps the corners and no others, where `Flatten` of a curved chain or loop in the plane cuts each arc into
pieces; `ToPolyline3(chordTolerance)` and `ToPolygon3(chordTolerance)` follow the arcs to the accuracy asked. Every
corner is kept, however short a side: at a millionth, the filleted chain flattens turning at both ends of its arc,
0.0007 apart, and the rectangle 100 by 50 standing in the plane x = 20 filleted at 0.0005 to its eight corners, where
both were put back together within the global tolerance, the chain turning once and the rectangle with four corners.
The polygon a loop flattens to is still checked flat and enclosing an area within the global tolerance, as any
`GeoPolygon3` is: the half disc of radius 1 built at a millionth with its diameter bent to (1, 0.000002, 0), whose three
corners enclose a sliver of 0.000002 square units, is refused as the polygon of those corners is, where it flattened to
the sliver; the half disc of radius 50 bent so, whose corners enclose 0.0001 square units, flattens to them.

A straight polygon or chain in space is turned round, written out and transformed side for side too. `Reverse` of a
`GeoPolygon3` keeps every corner: at a millionth, the triangle (0, 0, 0), (10, 0, 0), (10, 0, 10) with every corner cut
by 0.0003 reverses to its six corners, where it came back three, and a square 100 across built at a hundredth with one
corner 0.005 off the plane of the others reverses, where it was checked flat within the global tolerance again and
refused. `ToPolyline` writes the cut triangle out as a chain of seven vertices, where it came back four, and `ToPolygon`
closes the chain round it into the polygon of its six corners, where it came back three; a last vertex on the first, or
off it by no more than the rounding of its coordinates, 1E-12 of the largest of them, goes, and one further off keeps
the side back to the start. `TransformBy` of a chain,
however it moves it, and of a polygon or a face under a scaling, a stretch or a shear keeps them side for side: the cut
triangle scaled by 2 or sheared keeps its six corners, and the chain (0, 0, 0), (10, 0, 0), (10, 0, 10) scaled by 1E-5 is
the chain 0.0002 long, where it was refused as having fewer than two vertices. Only a corner the transformation lands on
the very same point as the one before it goes. An even scaling, turned, moved or mirrored besides, carries a polygon or
a face as a move does, its area scaled, and checks nothing again: a square 100 across with a corner 0.0009 off flat
scaled by 10 is kept, where it was refused as not flat, and the cut triangle shrunk by a ten-thousandth keeps its six
corners, 0.0007 wide, where it was refused as having no width. A stretch or a shear has the polygon checked flat and
enclosing an area within the global tolerance loosened as far as it stretches any direction: off flat within ten times
the tolerance under a stretch by 10, and wide within half of it under a scaling by 0.2, 0.25 and 0.5, which keeps a
square 0.003 across as one 0.0006 by 0.00075. Neither is made
finer than the global tolerance, so nothing kept before is refused now, and a projection that flattens a polygon to a
line is still refused. A body follows its faces: the box 100 by 70 by 40 with a pocket 0.0004 deep cut across its top
at a millionth, scaled by a half or by 10 and sheared by a thousandth, stays closed within the millionth scaled with it,
every corner kept and its volume the volume times the cube of the scale, and the block whose top is 0.0009 off flat
stays closed scaled by 1 000, or stretched ten times upwards with its volume ten times. It is refused where a face of it
comes out narrower, or further off flat, than the loosened tolerance allows, as a polygon is. `ToPolygon3` and
`ToFace3` lay a polygon or a face of the plane into a frame side for side too: the triangle (0, 0), (10, 0), (10, 10)
with every corner cut by 0.3, shrunk in the plane by a thousandth, keeps its six corners 0.0003 apart, where it was put
back together as three. It is still refused where it encloses no area within the global tolerance. `ToPolyline3` lays
a chain in leg for leg the same way.

A radius per corner works too, read the way the bulges are read: the entry at an index belongs to the vertex
at that index, and nought leaves that corner square. `TryFilletAt` rounds one named corner. A bend with too
little straight run either side to fit its radius is left square rather than forced, and where two bends want
more of the run between them than it is long, the one taking more of it gives way.

Only a corner between two **straight** legs is rounded. A leg that already curves lies in a plane of its own,
which need not be the plane of the corner, so there is no one plane to do the arithmetic in.

## Closed loops have to lie flat

`GeoPolylineArc3` requires no plane, as `GeoPolyline3` requires none. `GeoPolygonArc3` does require one, and
enforces it at construction exactly as `GeoPolygon3` does: a loop that is not flat encloses nothing and has no
inside. A bar bent out of one plane whose ends happen to meet is a `GeoPolylineArc3`, not a loop.

An arc lies in the plane when no point of it stands further off the plane than `EqualPlanar`, the test its
corners pass. The angle between its own plane and the loop's decides nothing on its own: a half circle 1 000
across turned 0.99 degrees out of the loop stands 8.64 off it and is refused, and a side 100 long bowing a
ten-millionth of its chord lies in the loop however its own plane is turned. `IsPlanar`, `TryGetPlane` and
the `PlanarMap` layouts read an arc the same way. The stand-off is read from the chord, as the corner of the
box round the arc that stands furthest off, so an arc of a huge radius is judged as surely as a half circle;
the box can stand off a little further than the arc, up to two thirds more, which errs on refusing an arc right
at the tolerance. An arc past a half turn is read round its circle, exactly.

That one rule is what makes everything else exact. Area, centroid, what is inside, offsetting and rounding
are all answered by laying the loop out in its own plane as a `GeoPolygonArc2`, working there, and lifting
the answer back:

```csharp
GeoPolygonArc3 tie = new GeoPolygonArc3(new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
    new GeoPoint3(300, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(40.0);

tie.Area;                  // counting each arc against its own chord
tie.Centroid;
tie.Contains(point);       // a point off the plane of the tie is outside it
tie.Offset(25.0);          // moved in the plane of the tie, arcs kept as arcs
tie.ToPolygonArc2();       // laid out in its own frame
```

What is on the loop, inside it or nearest it in its plane, `Locate`, `Contains`, `SignedDistanceTo`, `GetClosestEdge`
and the shortest line to a shape in its plane, is worked out on every side the loop has, as `IsPointOn` and
`DistanceTo` measure them. A loop built within a finer tolerance than the global one keeps a side shorter than that,
and `ToPolygonArc2`, built as a `GeoPolygonArc2` is, drops it. At a ten-thousandth, the
loop (0, 0, 0), (10, 0, 0), (10, 0.0006, 0), (0, 10, 0), built within a billionth, has (10.00005, 0.0003, 0) on its
side 0.0006 long and (9.9998, 0.0003, 0) inside it, 0.0002 deep, and that side is the edge nearest the first.

`Area`, `SignedArea`, `IsClockwise`, `Centroid` and `IsSimple` read the loop as it is too, and `Reverse` turns it round
edge for edge. At a millionth, the rectangle 100 by 50 standing in the plane x = 20 filleted at 0.0005 encloses
4 999.99999979 and reverses to its eight edges, where it was laid out within the global tolerance, read 4 999.9375 and
reversed to its four corners; with only its corner (20, 100, 50) rounded, its centroid is (20, 50, 25) to a billionth,
where it was 0.00008 off. Within a tolerance coarser than an edge, the edges either side of it meet across it, so the
filleted rectangle is simple within a millionth and not within a thousandth, as the same loop in the plane is.
`Clone`, `Translate` and `TransformBy` keep the loop edge for edge as well: the filleted rectangle copied, moved, turned
or mirrored keeps its eight edges, where it was put back together within the global tolerance and came back its four
corners.

The shaping works on the loop as it is too. `Fillet`, `Chamfer`, `TryFilletAt`, `TryChamferAt` and `Offset` lay it out
corner for corner and lift what comes of it back edge for edge, so they keep what the same work keeps in the plane, every
arc however small and a corner named by its vertex, and `Corner3.Fillet` and `TryFilletAt` read a `GeoPolygon3` the same
way. At a millionth, the rectangle 100 by 50 standing in the plane x = 20 filleted at 0.0005 is eight edges, four of them
arcs 0.0007 across, where it came back its four corners, 0.0625 square units short; and the loop (0, 0, 0), (10, 0, 0),
(10.0004, 0.0004, 0), (10.0004, 10, 0), (0, 10, 0) rounded by 1 at vertex 3 turns at (10.0004, 10, 0), where it lost its
side 0.00057 long and was rounded at (0, 10, 0).

A loop in space cannot report its winding through `IsClockwise`, because it is laid out in a frame that turns
with it. What reversing turns over is the plane the loop names.

## Between the plane and space

`PlanarMap` carries arcs both ways now, so the round trip closes on a curved edge instead of flattening it:

```csharp
GeoCoordinateSystem3 frame = plate.GetFrame();

PlanarMap.TryToPolylineArc2(frame, edgeInModel, out GeoPolylineArc2 laidOut);
GeoPolylineArc2 moved = laidOut.Offset(10.0)[0];
GeoPolylineArc3 backInModel = PlanarMap.ToPolylineArc3(frame, moved);
```

`ToArc3`, `ToEdge3` and `ToPolylineArc3` lift; `TryToArc2`, `TryToEdge2` and `TryToPolylineArc2` lay out
again and **refuse** what does not lie in the frame rather than flattening it quietly. An arc whose normal
runs against the frame is the same arc seen from behind, so its bulge changes sign coming down; that is what
keeps the trip exact whichever way the frame was built.

## One bar against another, and against everything else

A bar is asked about a plane, a segment, a ray, an arc, a circle, a triangle, a polygon, a face, either
box, a body — and about another bar.

```csharp
bar.GetIntersections(new GeoLine3(new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0)));
bar.GetIntersections(new GeoRay3(new GeoPoint3(200, -50, 0), new GeoVector3(0, 1, 0)));
bar.CollidesWith(otherBar);                       // one chain against another
stirrup.GetIntersections(bar);                    // a loop against a chain, either way round
bar.GetIntersections(setOut);                     // and against a straight GeoPolyline3
```

**Chain against chain walks every pair of edges**, so the work grows with the two edge counts multiplied
rather than added: two forty-edge bars are sixteen hundred edge pairs. What makes it usable is that each
edge carries a box round itself, so a pair whose boxes cannot reach each other is dropped before any
arithmetic is done, and bars in a model are mostly far apart. Two pieces lying along each other meet along
a length and name no place, so `CollidesWith` is what says they touch.

## Cutting a bar to a schedule, and to the openings

```csharp
bar.SplitAtDistances(new[] { 1000.0, 2500.0 }, out GeoPolylineArc3[] lengths);
bar.TrySplitBy(opening, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside);       // a box
bar.TrySplitBy(everyOpening, out GeoPolylineArc3[] within, out GeoPolylineArc3[] clear);   // an array of them
```

A piece is inside when it is inside **any** cutter, so overlapping openings behave as the one region they
cover: consecutive pieces on the same side are joined into one run, and a gap in the array is passed over
rather than throwing. Which side a piece is on is settled at its **middle** and never at an end, because
every end is on a surface by construction and a surface belongs to neither side.

## Cutting corners in space, and measuring from a loop

A chamfer needs no plane at all — it moves back along one leg and forward along the other, so both points
land on the legs themselves. Only a corner between two **straight** legs is cut, the rule the fillet keeps,
because a leg that curves leaves at a tangent. A straight chain is put back together within the tolerance asked, as a
loop's cut corners and a curved chain's are: at a millionth, the chain (0, 0, 0), (10, 0, 0), (10, 10, 0) cut by 0.0003
turns at (9.9997, 0, 0) and (10, 0.0003, 0), where the two ends of the cut, 0.00042 apart, were one corner within the
global tolerance; within a thousandth or coarser, a cut no longer than the tolerance is no edge, and the chain turns
where it starts.

```csharp
crooked.Chamfer(50.0);                     // a GeoPolyline3 that lies in no one plane, all the same
crooked.Chamfer(80.0, 20.0);               // unequal: back along the way in, then along the way out
loop.Fillet(30.0);                         // a GeoPolygon3 rounded: a GeoPolygonArc3 comes back
bar.OffsetInPlane(30.0, GeoVector3.ZAxis); // and a bent bar moved to another cover, bends and all

stirrup.GetShortestLineTo(point);          // leaves the boundary, lands on the point
stirrup.GetClosestEdge(point);             // which edge that was
stirrup.GetShortestLineTo(coplanarPlate);  // to a shape in the same plane
```

**A point needs no coplanarity and every other shape does.** That is not a compromise: a point off the
plane stands at the same height above every point of the boundary, so the nearest place to it is the
nearest place to its shadow, and the answer is exact. Two shapes in different planes have no such
shortcut, so they are refused. The plane is read within the planar tolerance, so at a tolerance of nought a shape is
refused wherever the rounding of its coordinates leaves a corner off the loop's plane, which a tilted plane nearly always
does: a polygon laid into the plane of a tilted loop through `PlanarMap` was refused 1 032 times in 1 032, and one in the
plane z = 0 none of 968 times. `GetClosestEdge` takes a primitive probe only — a point, a segment, an arc,
a circle or an edge — because the nearest edge of one many-edged shape to another is a *pair* of edges.
A chain, a polygon, a face or another curved loop is laid out in the loop's plane corner for corner, as the loop is,
so a side shorter than the global tolerance is measured to: at a millionth, the square 10 by 10 at the origin is 0.5
from the polygon (10.5004, 5), (16, 2), (16, 8), (10.5, 5), built within a finer tolerance, where the line ran 0.5004 to
the corner before the side 0.0004 long.

## What is exact, and what is sampled

Everything worked out on the curve itself is exact, because a `GeoArc3` answers the point nearest another
point in closed form: length, walking, the nearest point, the box, moving, reversing, planarity, flattening,
filleting, and **everything** on `GeoPolygonArc3`, because it projects.

**Where** two things cross is exact; **how far apart** they are is the refusal, and the line between the two
is drawn there on purpose. A crossing reduces to one quadratic — two coplanar circles meet on their radical
line, two in different planes on the line where their planes meet — and the distance from an arc to anything
but a point has no closed form at all once they are not coplanar. It wants a polynomial root solve, and a
sampled answer under an exact name is worse than no answer. Say how closely the bar should be followed and
ask the ordinary question instead:

```csharp
bar.ToPolyline3(0.1).DistanceTo(slab);
bar.ToPolyline3(0.1).CollidesWith(slab);
```

A sampled chain lies inside the arcs it stands for, so it is never nearer to anything outside the bar than
the bar is: a clearance worked out this way errs on the safe side. The same bargain `GeoCircle3` offers
through `ToPolylineByChordTolerance`.

Two coplanar circles that stay within the point tolerance of each other all the way round, their centres and their
radii no more than the tolerance apart put together, are one circle, as in the plane, and cross nowhere: the circles of
radius 1 000 and 1 000.00002 with their centres 1E-4 apart, and two arcs of one circle. Two that draw further apart
than that somewhere cross on their radical line however close their centres: the circles of radius 1 000 and
1 000.0005 with their centres 6E-4 apart cross at (-833.33324, 552.77094, 0) and (-833.33324, -552.77094, 0) in the
plan, and at the same points turned with them in any plane. Centres no further apart than the point tolerance are still
one centre where the rounding of the numbers leaves the line nowhere in particular. Centres further apart than that are
crossed however near they are, below the vector tolerance of 1E-5 too: at a millionth, the arcs of radius 10 about the
origin and of radius 9.999999 about (0.000002, 0, 0) cross at (5.0000007, ±8.6602536, 0), as they do a hundred times
larger, where the radical line, as long as the centres are apart, was read within the vector tolerance as a length, taken
for no line at all, and one crossing lost. The radical line is laid in the plane of the circles: two of radius 39.7 in a
plane leaning every way, about centres 1.4E-7 apart, cross twice on both at a ten-millionth, where the line leant out of
the plane by the rounding of the centres and the crossings lay up to 1.4E-7 off both circles. A segment crossing an arc is read so too: one longer than the point tolerance
crosses where it does however short, so at a millionth the segment from (0, 9.9999995, 0) to (0, 10.0000015, 0) meets
the arc of radius 10 about the origin at (0, 10, 0), where it was given its start.

Two circles, and two arcs, give the same places, to the last digit, whichever is asked first. Each is read against the
other and a place both readings name is given once, the first reading that of the one lying within the planar
tolerance of the other's plane, or, where both or neither do, of the one that comes first by centre, radius, normal
and angles. Read from the circle asked first alone, the circle of radius 473.706 about the origin in the plan and the
circle of radius 477.624 about (-37.520, -84.167, 0.0009), tilted 4.6E-6 radians off it, crossed at
(421.493, -216.198, 0) asked the one way and at (-442.545, 168.972, 0.001) as well asked the other; both ways give both
now. Of 4 000 such pairs tilted 1E-7 to 1E-3 radians, 65 gave a different number of places by the order they were
asked in, and of arcs cut from them 37, and none does. Asked the other way round, the points come in the opposite order
along their line, whichever way the two normals point: the circle of radius 100 about the origin with its normal up and
the circle of radius 90 about (60, 20, 0) with its normal down cross at (72.222, -69.166, 0) and (16.278, 98.666, 0) in
that order, and asked from the second, the other way. In planes parallel within the vector tolerance but further apart
than the planar one, the points are put in order along the line square to the one between the centres, as in one
plane, so that the same two circles 0.005 apart, at a point tolerance of 0.01 and a planar one of 0.0001, give them in
the opposite order asked the other way round too. `TryIntersectWith` gives the points `GetIntersections` gives, and a
ball's crossings with a circle or an arc, read through the circle the ball cuts from the curve's plane, are this reading's.

Two arcs or two circles that `CollidesWith` reads as touching, within the point tolerance of each other, are given the
place, as two ellipses are: where no reading names one there, the middle of the shortest line between them. The arc of
radius 100 about the origin in the plan and an arc of radius 50 leaning 0.01 radians, the end of its diameter along X
0.0005 above (0, 100, 0) and running square to the first there, touch at (0, 100, 0.00025), and 0.0011 above, not at
all. Of 2 000 pairs of circles drawn to pass within 0.0012 of each other at tilts up to a radian, 1 034 collided and
were given no place, and of arcs cut from them 288; none is now. Two arcs of one circle still name no place.
