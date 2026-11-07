# Asking a body

## Operations

| Class | Answers |
|---|---|
| [`Parallel3`](xref:GeometryHelper.Core.Parallel3) | is this parallel, perpendicular, coplanar with that |
| [`Containment3`](xref:GeometryHelper.Core.Containment3) | where does a point sit relative to a shape |
| [`Distance3`](xref:GeometryHelper.Core.Distance3) | how far apart are two shapes |
| [`Projection3`](xref:GeometryHelper.Core.Projection3) | which point of a shape is closest to a point |
| [`Intersection3`](xref:GeometryHelper.Core.Intersection3) | where exactly do two shapes meet |
| [`Lengthen3`](xref:GeometryHelper.Core.Lengthen3) | extend or trim a segment along its own line |
| [`Offset3`](xref:GeometryHelper.Core.Offset3) | move a segment sideways, grow or shrink a flat region |
| [`Collision3`](xref:GeometryHelper.Core.Collision3) | do two shapes overlap at all |
| [`Parametrization3`](xref:GeometryHelper.Core.Parametrization3) | which point sits at a position along a curve |
| [`Merge3`](xref:GeometryHelper.Core.Merge3) | put curves that meet end to end back together |
| [`Splition3`](xref:GeometryHelper.Core.Splition3) | cut a curve, a region or a body into pieces |
| [`Boolean3`](xref:GeometryHelper.Core.Boolean3) | join two bodies, keep what they share, take one out of the other |

Every operation is reachable both ways. The static form names the larger shape first and the point or
curve second; the instance form sits on whichever of the two reads better where you are calling from:

```csharp
var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
var point = new GeoPoint3(4, 3, 0);

Distance3.DistanceTo(line, point);   // 3
line.DistanceTo(point);              // 3
point.DistanceTo(line);              // 3
```

`Intersection3` returns a single result and reports `false` when there is not exactly one. Two shapes that
overlap along a whole line or a whole area — a segment lying in a plane, two coincident planes, two
collinear segments — have no single crossing to name, so they come back `false` rather than picking an
arbitrary point out of the overlap:

```csharp
var crossing = new GeoLine3(new GeoPoint3(0, 0, -5), new GeoPoint3(0, 0, 5));
var lyingIn  = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

crossing.TryIntersectWith(GeoPlane3.XY, out GeoPoint3 hit); // true, hit is (0, 0, 0)
lyingIn.TryIntersectWith(GeoPlane3.XY, out _);              // false
```

`Collision3` answers yes or no and says nothing about where. Two oriented boxes are tested with the
separating axis theorem, which needs the nine cross-product axes as well as the six face axes to catch two
boxes passing each other at an angle:

```csharp
var first  = new GeoObb3(GeoPoint3.Origin, 10, 10, 10);
var beside = new GeoObb3(new GeoPoint3(5, 0, 0), 10, 10, 10);
var apart  = new GeoObb3(new GeoPoint3(11, 0, 0), 10, 10, 10);

first.CollidesWith(beside); // true
first.CollidesWith(apart);  // false
```

## Asking a body about what is around it

A [`GeoSolid3`](xref:GeometryHelper.Geometry.GeoSolid3) answers the same questions the flat shapes do, and against every shape in the library that a
body can be measured against at all:

```csharp
GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

slab.DistanceTo(point);      slab.DistanceTo(line);       slab.DistanceTo(ray);
slab.DistanceTo(triangle);   slab.DistanceTo(polygon);    slab.DistanceTo(polyline);
slab.DistanceTo(plane);      slab.DistanceTo(obb);        slab.DistanceTo(aabb);
slab.DistanceTo(otherSolid);

slab.CollidesWith(line);     slab.CollidesWith(ray);      slab.CollidesWith(polyline);
slab.CollidesWith(polygon);  slab.CollidesWith(face);     slab.CollidesWith(obb);
slab.CollidesWith(aabb);     slab.CollidesWith(otherSolid);

slab.GetIntersections(line);   // where it goes in and where it comes out
slab.GetIntersections(ray);
slab.GetIntersections(plane);  // where the plane cuts its edges

slab.GetClosestPointOnBoundary(point);  // on the surface, even for a point inside
slab.GetShortestLineTo(point);          // the segment out to it
slab.GetShortestLineTo(line);   slab.GetShortestLineTo(ray);
slab.GetShortestLineTo(triangle);
slab.GetShortestLineTo(otherSolid);
```

`DistanceTo` reads a body as solid through: a point inside it, or a segment reaching into it, is nothing
away, and so is a body sitting wholly inside another without their surfaces meeting. `GetShortestLineTo`
runs **surface to surface**, so it has a length in all of those cases:

```csharp
var inside = new GeoPoint3(50, 50, 10);

slab.DistanceTo(inside);                  // 0.0  — inside the body
slab.GetShortestLineTo(inside).Length;    // 10.0 — out through the nearest face
```

Both ends of the segment lie on the shapes they came from, so it can be drawn as it is, and it is exactly
as long as the distance wherever the two readings agree at all. The shortest line between two bodies is
found by weighing face against face, skipping any pair whose boxes already stand farther apart than the
best segment so far.

Every pair that has a distance in space has a shortest line too, from either side: a point to every curve and
region, a segment to a plane, a polygon or a chain, and chains to chains, triangles, polygons, planes and bodies.
Flat regions are read as their distance reads them, bodies run surface to surface, and a shape lying level
within the point tolerance of a plane, a polygon or a body keeps its true gap:

```csharp
var square = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
new GeoPoint3(50, 50, 30).GetShortestLineTo(square);   // straight down to (50, 50, 0), 30 long

var box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
var bar = new GeoPolyline3(new GeoPoint3(20, 50, 10), new GeoPoint3(80, 50, 10), new GeoPoint3(80, 50, 60));
bar.DistanceTo(box);                                   // 0.0  — inside the body
bar.GetShortestLineTo(box).Length;                     // 10.0 — down to the floor

var hover = new GeoPolyline3(new GeoPoint3(0, 0, 0.0004), new GeoPoint3(10, 0, 6));
hover.DistanceTo(GeoPlane3.XY);                        // 0.0    — within the point tolerance of touching
hover.GetShortestLineTo(GeoPlane3.XY).Length;          // 0.0004 — the line keeps its true length
```

A shape that crosses a plane meets it in a segment of no length at its first point of contact, along a segment
or edge by edge. A segment or chain that comes at a polygon, a triangle or a body end-on and stops within the point
tolerance of it is read as crossing it, as the segment to a triangle always has been: the segment of no length then
sits at that end, up to the tolerance off the surface. Three kinds of pair have no shortest line, on purpose: a box, whose line goes through
`ToSolid()`; two planes, which share no one segment; and two points, whose segment is `new GeoLine3(a, b)`.

A ray is a half-line, so it cannot be cut into a segment and measured that way without first choosing how
far to cut. It is measured as a ray instead: every answer is one the ray really holds, and a ray starting
inside a body crosses its surface once on the way out.

```csharp
var incoming = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(1, 0, 0));
var leaving  = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(-1, 0, 0));

slab.GetIntersections(incoming).Length;  // 2 — in one side, out the other
slab.DistanceTo(incoming);               // 0.0
slab.DistanceTo(leaving);                // 200.0 — its nearest point is its own origin
```

A ray is given a place where it meets a body exactly where `CollidesWith` says the two touch, which reads the ray by the
point tolerance and the body to a billionth, or further out than a million to 1E-15 of its largest coordinate. One
starting within the planar tolerance of a face and rising from it
touches the face at its origin only where the origin stands within the point tolerance of the surface, whatever the ray
meets further on and however deep in the body it starts: with a point tolerance of a hundredth and a planar one of five
hundredths, the ray from 0.04 over the top of the box 100 by 100 by 50, rising a hundredth for each unit along it, is
given nothing, where it was given its origin and collided with nothing, and the one from 0.004 over it touches it at its
origin; the ray from 0.04 off the inner wall of a U, its prongs and base 20 thick, across the gap, is given the two walls
of the far prong, and the one from 0.04 under the top of the box where it leaves, where each was given its origin
besides. One that collides and crosses no face is given its point nearest the surface, where that stands within the
point tolerance of it, to the rounding of numbers the size of the body's, or the body holds the origin: at a tolerance of
nought the ray from the middle of a box 100 by 80 by 50 turned 0.7 of a radian is given where it leaves, where the
rounding refused the crossing of every face and it collided and was given nothing. A prepared solid gives a ray that
starts within the point tolerance of where its material ends and crosses no triangle its origin, as its `CollidesWith`
reads it.

However small the tolerance, a triangle or face holds the crossing of a ray to the rounding of the coordinates,
1E-15 of the largest of the body's and the ray's origin, as a body far out holds a point (see above), on its plane
and past its rim; whether the crossing lies ahead of the ray's origin is read by the tolerance given. Below the
rounding, and only there, a crossing within twice it of a rim sends the ray off in another direction, two crossings
of a seam within eight times it of each other along the ray are one, as each is worked out from its own side,
and a ray from further off than the body is large is read from a diagonal short of the body, along its own line,
so that its crossings are worked out from numbers the size of the body's and the rounding stays the body's. At
a tolerance of nought the cylinder 40 across and 50 tall, its axis leaning in space, holds the 1 000 points tried
inside it, where its prepared form read 111 inside and the body 125, and so does the cone tried; a point on the axis
of a cylinder is inside it though its first ray runs through the seam of two sides; and the ray along that seam,
through the axis, is given its two crossings once each from 60 out or from a hundred million out, where it was
given none or one. The planes of fan triangles far out can be rounded by more than this, and a point on the axis of
a cylinder of 1 080 sides 1E7 out may still be read outside. The openings of a body asked at a tolerance below the
rounding are cut within the rounding: a point 0.0005 under a plate turned about in space, with an opening through
it, is 0.0005 from it at a tolerance of nought, where it was 3.0005. `TryCutOpenings` cuts them so too where its
point tolerance is below the rounding, and where cutting them in together leaves the material open it cuts them in one
at a time, each kept only where it closes, rather than taking them out by a difference, which within the rounding came
back closed and wrong; corners the cut leaves a rounding apart are made one where that closes the material within the
tolerance given. That plate, 200 by 100 by 3 with a hole 20 by 20 through it, cut at nought, comes back closed and
holding 58 800, where it came back as a shell of 7 faces, its bottom lost, and the cut said it had worked. Openings
that touch or overlap each other or lie flush with a face are refused more often than they were cut wrong, and the
material can be valid at the default tolerance and not at the rounding, for slivers a rounding thin. At every
tolerance at or above the rounding nothing changes.

A segment or a line without ends is held so too: a triangle or face holds its crossing to the rounding of the
coordinates, 1E-15 of the largest of its own, the body's and the segment's, on its plane and past its rim, and a
segment reaching from further off than the body is large is read from a diagonal short of it. Below the rounding, two
crossings of a seam within eight times it are one, and an end within the rounding of a face meets the surface there
where the segment touches the face, or the body, as `CollidesWith` reads it. At a tolerance of nought the segment
through the cylinder 40 across and 50 tall, its axis leaning in space, is given its two crossings, and so is the line
without ends along it, by the body and its prepared form, where nearly all were given one or none. The segment from
10 off the side of a box turned about in space to a point on it is given that point, where 831 of 1 000 were given
nothing. On a single triangle or face an end so near is crossed only where `CollidesWith` of the two finds them
touching, which at a tolerance of nought it does for about half the points on a face it was tried at. `CollidesWith`
of a segment and a body asks the boxes first as far apart as the body holds a point, a billionth, or 1E-15 of the
largest coordinate further out than a million; that reaches every point tolerance below it, not only those below the
rounding, so the segment from 5E-10 over the top of a box touches it at a tolerance of nought, 1E-12, 1E-10 or 3E-10,
as it is nought from it at each. A segment with a coordinate that is not a number touches nothing, as before. The
booleans at a tolerance of nought are worked out within the rounding too; see the booleans further below the rounding.
`GetVolume`, `TryGetVolume`, `GetSurfaceArea`, `GetCentroid`
and `GetMassProperties` of a body with openings, asked at a point tolerance below the rounding, measure the material
`TryCutOpenings` gives: at nought the plate with the hole through it measures 58 800, where it measured 60 000, the
hole not taken out, and `TryGetVolume` said that could be trusted. Where `TryCutOpenings` refuses them, they are cut
within what rounding can move a corner off a plane, a trillionth of the size of the coordinates, as `Section` cuts them:
such a plate turned another way, with an opening 32.7 by 51 down from its top to 0.9 over its bottom, measured nought at
nought and the whole plate, 60 000, at 1E-15, and measures 56 497.83 at both. Where that refuses them too, or the body's
own faces do not close within the rounding, `TryGetVolume` says the volume cannot be trusted. `TriangulateSurface`,
`DxfWriter.Add` and `ProjectToSolid` cut the openings within the rounding too, and `ProjectToSolid` reads the faces
they leave within it: at nought the plate's surface was meshed into triangles holding 29 999.99, its bottom lost, and
a point in the middle of the hole was projected 1.5 from it, onto a face across the hole, where the hole's wall stands
10 off. Asked at a point tolerance below the rounding, `Section` cuts the material by its plane, and `Mesh3.ToCells`
cuts the body and its openings, within what rounding can move a corner off a plane, a trillionth of the size of the
coordinates, and each cell whose corners that leaves a rounding apart is welded to close within the tolerance asked. At nought the two faces beside an
edge each cut their own copy of it a rounding apart, and no cut closed: the plate had no section, where a plane square
to its face cuts 20 000, or 19 600 with the hole; cut into cells 60 by 40 it came out one cell of 60 000 with the hole
or without it, where twelve hold it, 58 800 with the hole; and a box 100 by 60 by 40 turned against a grid along the
world's axes came out one cell, where the grid lays 19 through it. So they are cut too at and a little above the
rounding, while the point tolerance is below both a trillionth of the size of the coordinates and a millionth: of
8 000 planes through prisms with openings, 871 cut nothing or the wrong area at the rounding and 547 at twice it, and
of 2 000 such bodies 668 and 452 came out in the wrong cells; none does now but those a thousand million out at twice
the rounding, 2E-6. At a millionth and above, the default tolerance among them, a section and the cells are cut as
they were, to the bit, but for walls of openings standing apart within the tolerance of one plane. A body `ToCells`
refuses at the tolerance asked it still refuses.

A [`GeoCircle3`](xref:GeometryHelper.Geometry.GeoCircle3) is not on the list. The distance from a circle in space to a flat face has no closed form,
and the library does not guess: turn it into a chain first, which says in the call how close an answer you
are asking for.

```csharp
slab.DistanceTo(circle.ToPolylineByChordTolerance(0.1));
```

## How deep inside, not just whether

`DistanceTo` reads a body as solid through, so a point anywhere inside one is nought away and how far in it
sits cannot be got back out of the answer. `SignedDistanceTo` keeps it, for the three shapes that enclose a
volume — `GeoSolid3`, [`GeoObb3`](xref:GeometryHelper.Geometry.GeoObb3) and [`GeoAabb3`](xref:GeometryHelper.Geometry.GeoAabb3) — and by a [`GeoPoint3`](xref:GeometryHelper.Geometry.GeoPoint3), which asks the same thing from the
other side.

```csharp
GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));
GeoSolid3 cube = box.ToObb().ToSolid();

cube.DistanceTo(new GeoPoint3(50, 50, 50));         // 0.0    — inside, and that is all it says
cube.SignedDistanceTo(new GeoPoint3(50, 50, 50));   // -50.0  — fifty in from the nearest face
cube.SignedDistanceTo(new GeoPoint3(50, 50, 10));   // -10.0
cube.SignedDistanceTo(new GeoPoint3(50, 50, -20));  //  20.0  — outside
box.SignedDistanceTo(new GeoPoint3(50, 50, 50));    // -50.0  — either kind of box answers too

new GeoPoint3(50, 50, 50).SignedDistanceTo(cube);   // -50.0  — and so does the point
```

The definition is tied to `Locate`: negative where it answers `Inside`, nought where it answers `OnSide`,
positive where it answers `OutSide`. So `Math.Abs(body.SignedDistanceTo(point))` is the distance out to the
surface and the sign carries the rest.

The surface of a pierced body is its faces **and** the walls of every opening, openings of openings
included, because that is what `Locate` calls the boundary. A point in the material beside a duct is measured
to the wall of the duct rather than out to the far skin:

```csharp
GeoSolid3 duct = new GeoAabb3(new GeoPoint3(40, 40, -10), new GeoPoint3(60, 60, 110)).ToObb().ToSolid();
GeoSolid3 pierced = cube.WithOpenings(new[] { duct });

pierced.SignedDistanceTo(new GeoPoint3(30, 50, 50));   // -10.0 — to the wall of the duct at x = 40
cube.SignedDistanceTo(new GeoPoint3(30, 50, 50));      // -30.0 — the same point in an unpierced body
pierced.SignedDistanceTo(new GeoPoint3(50, 50, 50));   //  10.0 — in the duct, so off the material
```

A duct bored right through runs out past both faces, and the part of its wall out there bounds nothing, so
each candidate is held against the body and kept only where the body agrees it is on the boundary.

The flat shapes in space are not offered one. A [`GeoTriangle3`](xref:GeometryHelper.Geometry.GeoTriangle3), a [`GeoPolygon3`](xref:GeometryHelper.Geometry.GeoPolygon3) and a `GeoCircle3` enclose no
volume, so a point is inside one only when it is also on its plane: a sign for them would be negative on a
set of no thickness and would read as though it meant more. `GeoPlane3.SignedDistanceTo` answers the question
that does make sense for something flat, which is which side of it a point lies on.

## A body asked many questions

`Prepare()` does once what the body's own questions do every time they are asked: it cuts the openings in,
meshes where the material ends and indexes the mesh, and keeps the box. The [`GeoPreparedSolid3`](xref:GeometryHelper.Spatial.GeoPreparedSolid3) it gives back
answers as the body does — the same locations, distances, crossings and clashes — only without paying for any
of that again. It is a snapshot, immutable and safe to ask from many threads at once.

```csharp
GeoPreparedSolid3 plate = pierced.Prepare();       // openings cut, surface meshed and indexed: once

plate.Locate(point);                               // as pierced.Locate(point)
plate.SignedDistanceTo(point);
plate.GetClosestPointOnBoundary(point);
plate.GetIntersections(ray);                       // each place once, in the order the ray meets them

foreach (GeoSolid3 bolt in bolts)
{
    GeoPreparedSolid3 prepared = bolt.Prepare();

    plate.CollidesWith(prepared);                  // the two indexes, and a corner for one inside the other
    plate.DistanceTo(prepared);
    plate.Intersect(prepared);                     // one body per clash
}
```

`Material` is the body with its openings cut in, `Surface` the mesh and `Index` the tree over it, for whatever
the prepared body does not ask itself. Preparing costs a cut of every opening and a sort of the mesh, so it
pays from the second question on; a body asked one thing is quicker asked directly.
