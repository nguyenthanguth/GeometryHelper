# Asking every shape

## And every shape can be asked, not only the body

For a long time a body could be asked about a segment and the segment could not be asked about the body.
That is gone. Every measuring question reads from either side and gives the same answer:

```csharp
GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

var beside  = new GeoLine3(new GeoPoint3(200, 50, 50), new GeoPoint3(300, 50, 50));
var through = new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));

beside.DistanceTo(slab);                  // 100.0, the same as slab.DistanceTo(beside)
through.CollidesWith(slab);               // true,  the same as slab.CollidesWith(through)
through.GetIntersections(slab).Length;    // 2,     in one side and out the other
```

A joining segment is the one thing that is not simply interchangeable. It **leaves the shape it was asked
of and lands on the other**, so the two directions are each other reversed:

```csharp
GeoLine3 away = beside.GetShortestLineTo(slab);   // starts on the segment, ends on the body
GeoLine3 home = slab.GetShortestLineTo(beside);   // starts on the body, ends on the segment

away.StartPoint.IsEqualTo(home.EndPoint);         // true
away.Length == home.Length;                       // true, both 100
```

A point, a segment, a ray, a triangle, a polygon, a face, a plane, either kind of box, a polyline, an arc, a
circle, an edge, and the chains and loops of arcs all take part. Two things are asked from one side only, on
purpose: an ellipse is asked from the ellipse, and a point answers whether it lies on or in a shape with
`IsPointOn` and `Contains` rather than with `CollidesWith` or `GetIntersections`.
Every shape in space also moves, which is the translating transformation and nothing more:

```csharp
var by = new GeoVector3(11, -23, 37);

slab.Translate(by);   beside.Translate(by);   GeoPlane3.XY.Translate(by);
```

### How near is touching, in space

**In space, as in the [plane](../plane.md), `DistanceTo` reads nought only where two shapes truly meet.** That is where they
cross, touch or overlap, or one holds the other, a point on a rim counting as held. Everywhere else it is the gap in space,
measured exactly however small and the same whichever way round it is asked, and `CollidesWith` is whether that gap is no
more than the point tolerance. Nothing is widened by the tolerance and nothing is laid flat. It holds for points, segments,
rays, polylines, triangles, polygons, faces, boxes and bodies against one another, and, for collisions, for arcs, circles
and chains and loops of arcs against all of them. Measured at a thousandth:

* **Straight shapes.** A segment starting 0.0005 off the square 100 by 100, in its plane, is 0.0005 from it, from either
  end, and collides; one hovering 0.0003 over it is 0.0003 from it; a point 0.0004 under the plane of the square and 0.0005
  beside it is 0.00064 away; one ending 0.0012 off its corner is 0.0012 away and does not collide; and one 0.0009 up and
  0.0009 out is 0.00127 away and does not collide, though neither offset is as much as a tolerance.
* **Segments, rays and chains** are as far apart one way as the other. Two segments meeting end to end, one tilted by
  8E-6 radian, are nought apart whichever is asked first; a ray passing through the end of a segment, from 0.00035 below it,
  is nought from it, and one running 0.0009 over a segment is 0.0009 from it and collides.
* **Arcs, circles, chains and loops of arcs** collide exactly where the gap between them in space is no more than the point
  tolerance, in whatever plane they lie, lifted or tilted out of it. A straight side facing the top of an arc touches it as an
  end does: across the dome of radius 50, a side at y = 50.0005 touches it and one at y = 50.0012 does not. A box is read by
  its corners, so one thinner than the tolerance, or a few thousandths across, is asked about as surely as any other. No member
  measures the distance from a curve in space to anything but a point; the gap is worked out inside. On .NET Framework 4.8
  an arc against a triangle takes 1.2 microseconds to ask about where they stand far apart and 6.8 where they stand near.
  An arc or a circle crosses a triangle, a polygon, a face or a box only where it collides with it: an arc starting 0.00107
  beside a side and 0.0007 over the plane, 0.0013 from the region and running away from it, is given no point, where it
  was given one. One that touches it without crossing, tilted or bowing beside a side in its plane, is given the point of
  it nearest the shape, as a segment touching a box is: the arc of radius 5 standing upright 0.0006 beside the side of
  the square 30 by 20 and dipping to 0.0006 over its plane touches it at (15, -0.0006, 0.0006), where it collided and was
  given nothing. One lying level over the material within the tolerance, rising off its plane by no more than the planar
  tolerance round its circle, is given none, as a curve lying on a surface crosses it nowhere; tilted further it touches:
  the arc of radius 300 tilted 9E-6, which rises 0.0027 round its circle, touches the material where it dips to 0.0006
  over it. Cutting a curved chain by a face or a box still cuts only where it crosses. A segment, a ray or a line that
  touches an arc, a circle, a curved edge or a curved chain without crossing it, piercing its plane a hair beside the
  rim, is given the point of the curve nearest it: the segment from (0, 50.0006, 1) to (0, 50.0006, -1) touches the
  circle of radius 50 about the origin at (0, 50, 0), where it collided and was given nothing. Of 20 000 segments
  piercing the plane of an arc from 1E-7 to 0.002 beside its rim, asked of the arc, its circle and its edge, 4 676 times
  one collided and was given no point; none does now. A point of the circle on the line of a segment or a ray, held by
  each within the point tolerance, is given only where the segment or ray, at its point nearest it, comes within the
  point tolerance of the curve, each point on its own: the segment from (0, 0, 0.0008) to (49.9992, 0, 0.0008), ending
  0.00113 from the circle of radius 50 about the origin, is given nothing, where it was given (50, 0, 0.0008) and collided
  with nothing; the one from (-60, 0, 0.0008), passing 0.0008 over the far rim, is given that touch alone; and the one
  from (-60, 0, 0.0006) to (49.9994, 0, 0.0006), ending 0.000849 from the near rim, both. A chain names each place once: two points within the point
  tolerance of each other are one place, and so are two that the edges either side of a corner give where both lie
  within the point tolerance of the corner, a crossing kept over the point of a touch and otherwise the first. Places
  further from a corner are kept however close the legs run: a segment crossing both legs of a hairpin 0.0009 across at
  50 from its tip is given both crossings, 0.043 apart.
* **A segment against a plane** that ends within the planar tolerance of it, without crossing it, touches it at that
  end and is given it, as a triangle or a polygon in the plane gives it: the segment from (-50, 0, 0.5) to
  (50, 0, 0.0005) touches the plane z = 0 at its end, where it collided and was given nothing. One lying in the plane,
  longer than the point tolerance and both its ends within the planar tolerance of it, meets it along its length and
  names no place. The plane asked about the segment, or about an edge that is one, gives the same points.
* **Segments, rays, lines and chains against a triangle, a polygon or a face** are read alike. The crossing is given
  where it stands within the point tolerance of both and the two touch: a ray starting 0.00076 under the plane of a plate
  and beside the rim of its hole, 0.00113 from the material and running away from it, is given nothing, where it was
  given its origin. One that touches without crossing is given the point of it nearest the region: the segment from
  (-5.0004, 4.9996, 0.0004) to (4.9996, -5.0004, 0.0004), passing 0.0007 off the corner at the origin of the triangle
  (0, 0, 0), (100, 0, 0), (0, 100, 0), is given (-0.0004, -0.0004, 0.0004), where it collided and was given nothing, and
  so a ray, a line and a chain, whose corner touching it is given. One with no crossing of the plane to give, lying level
  over the material within the planar tolerance of the region's plane at both its ends, running in over the rim among
  them, is given none, and so is one lying along a side of the rim within that of the side's line, as a segment lying
  along an edge of a box is. A segment has no crossing where it climbs no more than the planar tolerance from end to
  end, and is read level by its own two ends. A line without ends has none where the segment it is cut to, a millimetre
  past the region, climbs no more than that. A ray is read as the line without ends along it is, and given what that
  line meets at its origin or ahead of it: the ray from 0.0005 over a square 100 by 100, falling 1E-5 for each unit
  along it, climbs 0.001 across the square and is given its crossing 50 along, as its line is, where it was given
  nothing; the one from 0.0002 over it falling 4E-6 climbs 0.0004 and is given nothing, as its line is; and one a
  hundred-millionth or so off the plane and level to rounding is given nothing however near it starts. Where the line
  meets the region only behind the origin, or lies level over it only behind a ray running away from it, a ray whose
  origin touches the region is given its origin. A ray is given a point exactly where `CollidesWith` says it touches the
  region, but where it holds more than the point tolerance of a part of its line lying level over it: the ray from 0.0006
  beside the side of the square and 0.0003 over it, running away from the side at a hundredth, 0.00067 from the square,
  touches it at its origin, where it collided and was given nothing, its own part read along the side; and at a
  tolerance of nought a ray starting 4E-12 over the square and rising from it touches nothing and is given nothing, where
  it was given the crossing of its line 4E-12 behind its origin. `TryIntersectWith` of a ray gives the first point of this list and is
  false where the list is empty, so the ray from 0.0002 over the square falling 4E-6 is refused there too, where it was
  given its crossing of the floor; that of a segment or a line without ends gives the crossing alone, and is false for a
  touch the list gives. Such a ray or line is read by the two ends of the part of it over the region, from where its shadow on the
  region's plane first crosses the rim to where it last does, or beside the side it runs along, and not by where it was
  cut past the region: the ray from 0.0004 over the triangle rising 1E-6 was given its origin, the one from 1E-12 under it
  (10.000001, 20, 0), and the line without ends 0.0009 over the square rising 1E-6 the point over its rim. Every other
  crossing is given: the ray from 0.0009 under the triangle rising 1.5E-5 crosses it 60 along, though the part of it over
  the triangle runs from 0.0009 under to 0.0003 over. A ray and a segment along it can answer otherwise, each read by its
  own ends: from 0.0004 over the square rising 1E-5 the ray stands 0.0013 over the far rim and touches the square at its
  origin, where a segment 40 long along it gives nothing; and from 0.0003 over the triangle dipping 9E-6, level within the
  vector tolerance, the ray and the line along it give nothing, where a segment 150 long gives the crossing (38.333, 20, 0)
  and one 70 long nothing. A part no longer than the point tolerance, of one that only passes a corner or runs
  away from the rim, lies along nothing, and the touch is given. Against a box a ray is read as the segment from its
  origin past the box, and a line as the segment past it both ways, as before. Cutting a curved chain by a face still
  cuts only where it crosses.

A triangle with no area, by `GeoTriangle3.IsDegenerate`, its height over its longest side within the point tolerance,
such as (0, 0, 0), (100, 0, 0), (50, 1E-8, 0), is the segment it has collapsed to: it touches what comes within the
tolerance of it, as its distance says, is crossed where its sides are, and holds the points on them, `OnSide`, and none
inside. The triangle 0.003 by 0.003, which twice its area against the vector tolerance read as having none, is a triangle
to every query: it holds its centroid, which stands 0.001 from two of its sides and so is `OnSide` at a thousandth, and
a segment through its middle crosses it. The area is read at the tolerance a query is asked within, so at 1E-5 the
triangle 100 long and 0.0005 high is a triangle and holds a point in its middle, though `TryGetBarycentric`, which reads
the global tolerance, gives false for it. A triangle whose corners lie on one line to rounding has no plane at all, as
`Normal` reads it, and is measured as its sides; a thin one that has a plane is read as standing off it by the rounding
of its normal.

Seven things are read otherwise, on purpose:

* **A body against a body**, or an oriented box against an oriented or an axis-aligned box, is searched through the
  triangles of the two and reads nought when they are within the point tolerance of each other: two solid boxes 0.0004 apart
  are nought apart, and so is an axis-aligned box 0.0004 from an oriented box or a solid, though the line between them
  (`GetShortestLineTo`) is 0.0004 long. Only `GeoAabb3.DistanceTo(GeoAabb3)` is exact.
* **A plane** reads a ray, a chain, a triangle, a polygon, a face, a box or a body within the point tolerance of it as on
  it: the plane z = 0 is nought from a square 0.0004 above it. A point, a segment and another plane are measured to it exactly.
* **A billionth of a millimetre outside a body** reads as in it, so that rounding in a body turned about in space cannot lose
  a crossing: a point, a segment, a ray or a region 5E-10 above the top of a solid box is nought away, and so is a triangle,
  a polygon or a face that close to an oriented or axis-aligned box, though a point or a segment 5E-10 from the box itself is
  5E-10 away. Further out than a million the billionth grows with the coordinates, to 1E-15 of the largest of the body
  and the point, past their rounding: a point 0.052 outside a U 5E7 out is outside it.
* **A segment you give, no longer than the point tolerance, is read as its start** where it does not truly meet the other
  shape, as it is in the plane, by `DistanceTo`, `CollidesWith`, `GetShortestLineTo`, `GetIntersections` and
  `TryIntersectWith` alike, and so is a straight `GeoEdge3` that short. At a thousandth, the segment from (5, 5, 0.0015)
  down to (5, 5, 0.0006), stopping 0.0006 above the square 10 by 10 in the floor, is 0.0015 from it, joined to it from its
  start and meets it nowhere, as it is to a triangle, a face, a body, a ray, a ball and a spline so aimed; the one from
  (5, 5, 0.0004) to (5, 5, -0.0005), passing through the square, is nought from it, joined to it by a line of no length at
  (5, 5, 0) and meets it there, as it is to a segment it crosses, to the floor and to a ball it reaches into. One standing
  within the tolerance of a shape without meeting it, from (5, 5, 0.0008) down to (5, 5, 0.0002), meets it at its start, as
  a point lying that close does: the square, the floor, a segment, a box, an arc, a ball and every other shape; one lying in
  the plane of the square across its rim, which meets it along a length, so too. The sides of a shape are the sides they are
  however short, and a shortest line is joined to such a side as it is: at a hundredth, the chain from (-7, -7, 0) to the
  origin, on to (0.008, 0, 0) and down to (7, -7, 0) is joined to the point (0.006, 0.02, 0) by a line 0.02 long, from its
  short side, where it was joined from the corner (0.008, 0, 0) by one 0.0201 long; so are a polygon, a face and a
  triangle with such a side, a segment, a ray, a ball or a polyline beside it, and the closest point of a curved chain or
  loop. A curved chain or loop touches a shape by the gap to each straight side as it is: the chain from (-7, -7, 0) to the
  origin and on to (0.008, 0, 0) touches the square, the box or the segment standing on x = 0.017, 0.009 past its end, where
  it was read as touching nothing, its short side 0.017 off from its start. A plane measures a segment exactly, by its
  nearer end, as above, and a point is measured to a stub's start. A stub shorter than the vector tolerance meets a segment
  or the rim of an ellipse where it truly crosses it, as a longer one does, and a segment longer than the tolerance you ask
  for is measured as it is however much shorter than the global one: at a billionth, the segment from (5, 5, 0.0004) to
  (5, 5, -0.0005) crosses the top of a box at (5, 5, 0), where it was read as its start. So does a side of a triangle, a
  polygon or a face crossing a body: the triangle (5, 5, 0.0004), (5, 5, -0.0005), (45, 5, 10) crosses that top first at
  (5, 5, 0), where it was given its short side's start. A straight side of a chain, a loop or a polyline crosses the
  surface of a body as it is however short: at a hundredth, the chain from (2, 5, 0.054) down to (5, 5, 0.004), through
  the top of the box 10 by 10 by 10 standing under the floor to (5, 5, -0.005) and on to (8, 5, -0.055) crosses it, and
  is cut by it, at (5, 5, 0), where its side 0.009 long crossed nothing and the chain was given no point. A polyline is cut
  so too, within the tolerance of (5, 5, 0), where it came back whole, a cut within the tolerance of a corner made at the
  corner; and a segment you give is cut where it crosses however much shorter than the global tolerance: at a billionth,
  the segment from (5, 5, 0.0004) to (5, 5, -0.0005) is cut at (5, 5, 0), where the place was measured along it at the
  global thousandth, at its start, and it came back whole. A straight side meets a triangle, a polygon or a face as it is
  too: the chain lying level 0.004 over the top of that box from (2, 5, 0.004) to (5, 5, 0.004), down through it to
  (5, 5, -0.005) and on level to (8, 5, -0.005), as a curved chain or a polyline, meets the square of the top at (5, 5, 0),
  where it met nothing; a chain whose corner comes within the tolerance of the region is still given the corner, and a
  place two sides name within the tolerance of each other is named once, by the first. A polyline cut at a distance, by
  `SplitAtDistances` or by any of the cuts that use it, starts every piece where the one before it ended, on the polyline:
  at a hundredth, the polyline from (0, 0, 0) to (10, 0, 0) and on to (10, 10, 0) cut at 9.995 comes back as two pieces
  meeting at the corner, where the second started at (10, -0.005, 0), the cut carried back along the next leg off the
  polyline, and cut at 10.005 the second started 0.005 past the corner the first ended at; and its pieces keep what the
  tolerance asked keeps, where at a billionth a piece shorter than the global thousandth threw. The places of the cuts by a
  point, a plane, a polygon, a face or a box are measured along a segment or a leg as it is however much shorter than the
  global tolerance, as those of the cuts by a body are. The closest point of a polyline, a polygon or a face is on a side
  as it is however short, as its shortest line is: at a hundredth, the polygon (0, 0, 0), (10, 0, 0), (10, 0.006, 0),
  (0, 10, 0) gives (10.03, 0.003, 0) the closest point (10, 0.003, 0), 0.03 away, where it gave the side's start
  (10, 0, 0), 0.0301496 away; the readings of whether a point lies on a face, and the shortest line from a ray, still take
  the side's start. And
  `GetDistanceAtPoint` and `GetParameterAtPoint` of a curved chain or loop read a straight side as it is, as its closest
  point does: the chain from (-7, -7, 0) to the origin, on to (0.008, 0, 0) and to (7, -7, 0) puts (0.006, 0.02, 0)
  9.9055 along, 0.006 along its short side, where it put it 9.9075 along, at the start of the next. So do those of a
  polyline and a polygon: at a hundredth, the polyline (0, 0, 0), (10, 0, 0), (10, 0.006, 0), (0, 0.006, 0) puts
  (10.03, 0.003, 0) 10.003 along, halfway, where it put it 10 along, at the start of the leg 0.006 long; and a polygon cut
  by a chain that starts beside such a side meets its outline at the chain's foot on the side, where it met it at the
  side's start. The last piece of a polyline cut at a distance ends where the polyline does: a last leg no longer than the
  tolerance takes the corner before it, as a first one is taken by the start, so at a hundredth the polyline from
  (0, 0, 0) to (10, 0, 0) and on to (10, 0.006, 0) cut at 5 ends its second piece at (10, 0.006, 0), where it ended at the
  corner and the end was lost; cut at 9.995, which would leave only that leg after the cut, it comes back whole.
* **A circle collides as its rim and a loop of arcs as its outline**, so a circle or a loop round a shape touches nothing:
  the circle of radius 40 about the origin does not touch the square 20 by 20 in its middle, and touches the segment from
  (40.0005, 0, 0) to (80, 0, 0). The distance from a point to a circle is to the filled disc, as it always was: nought at the
  centre, and 5 for a point 5 above it. A loop's is to its outline.
* **Projections and intersections keep their readings.** `GetShortestLineTo` and `GetIntersections` ask where the shapes
  are, not whether they are near, so within the tolerance a shortest line can be of no length where the distance is the gap:
  the segment from (5, 5, 0.0004) to (5, 5, 9) over the square is joined at (5, 5, 0.0004) and is 0.0004 from it. Between
  segments, the line can still differ by the order of the arguments. A segment no longer than the tolerance is joined and
  met as the bullet above says.
* **One case is searched and not proved**: two partial arcs lying nearly on one axis whose distance stays almost constant
  just beyond the tolerance, which are walked finely with every low point polished.

### A segment or a ray against a flat shape

A segment, a ray and a flat region have no thickness between them, so there is nothing to overlap in: they
touch exactly where the gap between them is no more than the point tolerance, and cross where that gap is nought.

```csharp
var flat = new GeoTriangle3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
var piercing = new GeoLine3(new GeoPoint3(20, 10, -50), new GeoPoint3(20, 10, 50));

piercing.CollidesWith(flat);   // true
flat.CollidesWith(piercing);   // true
```

A `GeoFace3` is material with holes in it, so a run down the middle of a hole touches nothing at all:

```csharp
var plate = new GeoFace3(
    new GeoPolygon3(
        new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0),
        new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)),
    new[]
    {
        new GeoPolygon3(
            new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0),
            new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0))
    });

new GeoLine3(new GeoPoint3(20, 20, -50), new GeoPoint3(20, 20, 50)).CollidesWith(plate);   // true
new GeoLine3(new GeoPoint3(50, 50, -50), new GeoPoint3(50, 50, 50)).CollidesWith(plate);   // false
```

A ray is a half-line, so where it starts decides as much as where it points. One starting inside a box
touches it without entering anywhere:

```csharp
GeoAabb3 crate = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));

new GeoRay3(new GeoPoint3(50, 50, 50), new GeoVector3(1, 0, 0)).CollidesWith(crate);    // true, inside it
new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(1, 0, 0)).CollidesWith(crate);   // true, into it
new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(-1, 0, 0)).CollidesWith(crate);  // false, away
```

### Flat shapes against each other

Triangles, polygons and faces in space say whether and where they meet each other, a plane, a ray and a body, and how
far apart they are, from either side. They are read filled, a face by its material. Two in planes that are not one meet
where an edge of either reaches the other, because the ends of every stretch they share lie on an edge of one of them,
so the crossings named are those ends, however finely a side is cut into pieces; two within the point tolerance of
each other touch, and their distance is the gap, 0.0004 for a square lying level 0.0004 over another; two in one plane are laid out with `PlanarMap` and the plane half
answers:

```csharp
var floor = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
var wall = new GeoPolygon3(
    new GeoPoint3(-20, 50, -30), new GeoPoint3(120, 50, -30), new GeoPoint3(120, 50, 30), new GeoPoint3(-20, 50, 30));

floor.CollidesWith(wall);        // true
floor.GetIntersections(wall);    // (100, 50, 0), (0, 50, 0): where the wall's line runs out of the floor
```

Apart, the distance is the least of every edge of each against the other, which is exact, since two flat regions
apart always have their nearest pair on an edge of one of them. A segment through a face's hole is measured to the
hole's rim:

```csharp
var tilted = new GeoPolygon3(
    new GeoPoint3(0, 0, 40), new GeoPoint3(100, 0, 40), new GeoPoint3(100, 100, 80), new GeoPoint3(0, 100, 80));
floor.DistanceTo(tilted);                  // 40, from (0, 0, 0) to (0, 0, 40)

var plate = new GeoFace3(floor, new[] { new GeoPolygon3(
    new GeoPoint3(30, 30, 0), new GeoPoint3(70, 30, 0), new GeoPoint3(70, 70, 0), new GeoPoint3(30, 70, 0)) });
var pin = new GeoLine3(new GeoPoint3(50, 45, -20), new GeoPoint3(50, 45, 20));
pin.CollidesWith(plate);                   // false — it passes through the hole
pin.GetShortestLineTo(plate);              // (50, 45, 0) to (50, 30, 0), 15 long, out to the hole's rim
```

A plane meets a shape where its corners' heights change sign or reach nought within the point tolerance, and a body
where one of its faces straddles it, so a body in two pieces either side of a plane touches it nowhere. Two rays cross
where two segments on the same lines would: a ray from (0, -1, 0) half a degree off the X axis crosses it at
(114.59, 0, 0), and two rays along one line touch and name no point. A box is answered as its `ToSolid()`, and has no
shortest line of its own. Left out, on purpose: two planes, which meet in a line; and the crossings of a body or a box
with another, which are the booleans' and the clash's.

## Parametrization

A **parameter** is normalized: 0 is the start of a curve and 1 its end. A **distance** is a true arc length
from the start. Values outside the natural range follow the shape of the curve — a line segment
extrapolates along the infinite line carrying it, a polyline clamps because an open chain has no single
direction to extend along, and a circle wraps.

```csharp
var segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
segment.GetPointAtParameter(2.0);   // (20, 0, 0) — extrapolates

var chain = new GeoPolyline3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0));
chain.GetPointAtDistance(5.0);      // (3, 2, 0)
chain.GetPointAtDistance(100.0);    // (3, 4, 0) — clamps

var square = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
    new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
square.GetPointAtParameter(0.25);   // (10, 0, 0) — a quarter of the way round
square.GetPointAtParameter(1.25);   // the same point again — wraps
```

A polygon is measured around its boundary starting at its first vertex, and a circle around its
circumference. Both wrap, so there is no out-of-range parameter for a closed curve.
