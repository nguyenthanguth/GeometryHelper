# Infinite lines and balls

## Infinite lines

[`GeoXLine3`](xref:GeometryHelper.Geometry.GeoXLine3) is a line that runs both ways for ever: a point on it and a direction of unit length, the space
counterpart of [`GeoXLine2`](../plane.md) and of the XLINE of AutoCAD, and [`GeoRay3`](xref:GeometryHelper.Geometry.GeoRay3) with no start. A direction of
no length is refused.

```csharp
var box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 20));
var line = new GeoXLine3(new GeoPoint3(-50, 50, 10), GeoVector3.XAxis);

line.GetIntersections(box);                  // (0, 50, 10), (100, 50, 10): in the order the line runs
line.CollidesWith(box);                      // true
line.Translate(new GeoVector3(0, 100, 0)).DistanceTo(box);   // 50: the parallel line through (-50, 150, 10)
line.GetPointAtDistance(-30);                // (-80, 50, 10): a line has points behind its origin
line.ClipTo(box);                            // (0, 50, 10) -> (100, 50, 10), for drawing
line.IsPointOn(new GeoPoint3(-5000, 50, 10.0005));            // true: nothing stops short at the origin
```

**It is asked what a ray is asked**, and every answer reads as the segment's does: a closed shape is filled for the
distance and the collision, so a line through the box is nought from it, while the shortest line runs to the surface. Every
shape that takes a `GeoRay3` takes a `GeoXLine3` too, from either side, so `box.DistanceTo(line)` gives the same number
to the bit, and so do the crossings, the shortest line turned round and the collision: a point, a segment, a ray,
another line, a plane, a triangle, a polygon, a face, a polyline, a box of either kind, a body, and, to be asked
whether they touch and where they cross, an arc, a circle, an ellipse, an edge and the curved chains and loops. Unlike
the ray and the segment, the line is asked of an ellipse from both sides. The curved shapes have no `DistanceTo` or `GetShortestLineTo` to a line, as they have none to a ray or a segment: the
distance from a curve in space to anything but a point has no closed form, and `ToPolyline3(chordTolerance)` is the way to ask. A flat shape is crossed at one point or nowhere, and a line lying in
its plane names no place; `TryIntersectWith` against a plane, a triangle, a polygon or a face gives that point, and
against every other shape the array of points, empty when there is none.

Against any shape that has an end to it, the line is answered as the segment that runs from a millimetre (or a hundred
point tolerances, whichever is more) before the nearest the shape reaches along it to the same past the farthest;
the origin is not part of it, so a shape a million along is asked as the stretch round it. A body counts for the corners of
its box, a circle or an ellipse for its centre brought onto the line less and plus its radius, and an arc for its
two ends and the point of its circle furthest ahead or behind along the line where the arc holds it, so a nearly straight arc
on a circle of a radius of a million does not stretch the segment to a million. Nothing of the shape lies outside, so the
line answers as that segment does, and shares its faults: an ellipse reads the point tolerance where the line crosses its
plane, so a line that passes its rim at 0.0009 at a slant, within the tolerance of a thousandth, may not be seen to touch
it, whatever the length of the segment.

**Two lines in space are skew unless they happen to meet.** They are as far apart as the common perpendicular between
them is long: the line through the origin along X and the one through (0, 0, 30) along Y are 30 apart, as is the one through
(0, 0, 30) along -X, whichever way it runs, and nought where they cross. The perpendicular is worked out to a rounding of its
own, each component of the cross product of the directions as the difference of two products taken exactly, so that two
lines a hundred millimetres apart, at a sine of 1E-8 and with their origins near each other, at coordinates up to ten
million, are held to the exact gap to 3E-14, where the plain arithmetic of the same formula is out by 4E-7; origins ten million
apart along the lines carry the rounding of that distance, 3E-9. Two lines whose directions differ by a sine above 1E-9
always have one, whatever the tolerance and however far out. At a sine of 1E-9 or less they are parallel by rounding when the
perpendicular lies further out than a double can place within the point tolerance, that is (the larger of the two
distances along the lines to its feet, plus the largest coordinate of the two origins) times 2.2E-16 above the tolerance,
which is 4.5E12 along for a thousandth at coordinates near nought, and are as far apart as the nearer origin is from the other
line. Two lines 5 apart in one plane at an angle of 1E-9 cross 5 billion along and are nought apart; at 1E-13 they are 5
apart; and two lines 5 apart with their common perpendicular at the origins are 5 apart at any angle. The segment that joins
two such lines is as long as their distance to what a double can place its ends to, that rounding of the larger distance
along the lines, which the rule holds below the point tolerance, and square to the second line. A line and a plane follow the
same rule: a line starting within the planar tolerance of the plane touches it, as a ray does, nought from it and colliding
with it (and naming no crossing where it lies in it), and one that starts further off crosses it, nought from it, where a double
can place the crossing: at any angle above a sine of 1E-9, and at a sine of 1E-9 or less where the crossing lies no further
than 4.5E12 along for a thousandth (tilted a ten-thousandth of a radian, a line 7 above the floor crosses it 70 000 along).
Beyond that it is read as running level, and is as far from the plane as its origin: the line through (0, 0, 100) along
(1, 0, -1E-11) is 100 from the floor, so a line can read further from a shape than its own ray does where the
crossing cannot be placed, which reads that ray as nought from it. `GetIntersections` and
`TryIntersectWith` of two lines are as strict as for two rays: lines within the angle tolerance of parallel give no point,
even where they are the same line, and a crossing is the middle of the common perpendicular where it is no longer than
the point tolerance, the same point to the bit whichever of the two asks. A line and a ray are nought apart where the ray reaches the
common perpendicular, and otherwise as far as the origin of the ray is from the line.

`Equals` reads the fields, so the line from (500, 50, 10) along -X is not equal to the line from (-50, 50, 10) along X;
`IsSameLineAs` reads the line and says they are the same, within the point tolerance for the origins and the vector
tolerance for the directions. `Reverse` runs the same line the other way, `ToRay()` is the half ahead of the origin,
`ToLine(from, to)` the segment between two signed distances along it, and `GeoRay3.ToXLine()` and `GeoLine3.ToXLine()` give the
line a ray or a segment lies on. `ClipTo(box)` clips a line that crosses the box exactly; one that misses it by no
more than the point tolerance is clipped to the box grown by it, so its ends are no more than the tolerance outside, and
one that misses by more gives null. The line two planes share is
`GeoPlane3.TryGetIntersectionLine(other, out GeoXLine3 line)`: the floor and the wall x = 100 meet in the line along Y through
(100, 0, 0), and parallel planes, apart or one, are refused. It is the line `TryIntersectWith(other, out GeoRay3 ray)`
names as a ray.
A line lies in the plane of a frame as a line of the plane, `line.ProjectToXLine2(frame)`, and a line of the plane
stands in space again with `xline.ToXLine3(frame)`; a line along the normal of the frame has no direction in its plane and is
refused.

`GeoBvh3.GetIntersections` and `GeoPreparedSolid3.GetIntersections` and `CollidesWith` take a line as they take a ray (the prepared body names a seam once, in the order along the line, as the body does), and [`GeoBvh3`](xref:GeometryHelper.Spatial.GeoBvh3) gives what the plain scan gives, each triangle asked
`line.TryIntersectWith(triangle, ...)`: the branches whose box, grown by the larger of the point and planar tolerances, the line
never enters are not looked at, and the line runs both ways from its origin, so the answer is the same whether the origin lies before
the mesh, inside it or past it, but for a crossing within rounding of the tolerance. The line through (3, 6, 4) along Z crosses the mesh of the box 10 by 10 by 10 from the origin at
(3, 6, 0) and (3, 6, 10), where the ray from the same point reaches only the second. Like the ray's, a line through an edge two
triangles share is named by both.

`DxfWriter.Add(line, layer)` writes the XLINE entity (`AcDbXline`, the origin in groups 10, 20, 30 and the unit direction in 11, 21, 31);
`ObjWriter.Add(line, length, name)` and `Wkt.Write(line, length)` write the segment of that length centred on the origin: the line
through (1, 2, 3) along Z, written 50 long, runs from (1, 2, -22) to (1, 2, 28). The AutoCAD bridge, `GeometryHelper.CadConvert`,
converts the `Xline` and `Ray` entities: `xline.ToGeoXLine3()` and `line.ToAcadXline()`, `ray.ToGeoRay3()` and `geoRay.ToAcadRay()`, losing nothing,
since each holds a base point and a unit direction (the direction is made unit length again on reading, so it can differ in its last digit
after a round trip, and an entity whose direction has no length is refused with an `ArgumentException`). The viewer draws a `GeoXLine3` through the box of the scene.

## Balls

[`GeoSphere3`](xref:GeometryHelper.Geometry.GeoSphere3) is a solid ball: a centre and a radius above nought (nought, a negative radius, an infinite one and a number that is
not one are refused). It is a volume, so it offers `Contains` and `Locate`, `Volume` (a ball of radius 50 holds 523 598.8) and
`SurfaceArea` (31 415.9), `GetAabb()`, `SignedDistanceTo` (negative inside, tied to `Locate` as for the other volumes) and
`DistanceToSurface`, which measures to the shell from either side. It moves with `Translate` and `TransformBy`, which keeps a ball
a ball under a turn, a shift, a mirror or an even scaling and refuses what would make it an ellipsoid, a scaling of one axis or a
shear, as a circle is refused its stretch.

```csharp
var ball = new GeoSphere3(GeoPoint3.Origin, 50);

ball.DistanceTo(new GeoPoint3(20, 0, 0));          // 0: a ball is filled, like a body
ball.SignedDistanceTo(new GeoPoint3(20, 0, 0));    // -30: thirty inside the surface
ball.DistanceTo(new GeoPoint3(200, 0, 0));         // 150
ball.GetShortestLineTo(new GeoPoint3(200, 0, 0));  // (50, 0, 0) -> (200, 0, 0)

ball.GetIntersections(new GeoXLine3(new GeoPoint3(0, 30, 0), GeoVector3.XAxis));   // (-40, 30, 0), (40, 30, 0)

ball.TryGetIntersectionCircle(new GeoPlane3(new GeoPoint3(0, 0, 30), GeoVector3.ZAxis), out GeoCircle3 cut);
// true: the circle of radius 40 about (0, 0, 30), square to Z

GeoSolid3 mesh = ball.ToSolid(0.01);               // 30 420 triangles, none further than 0.01 from the sphere
```

**It is asked what a ray is asked, and more.** A ball is a point grown by its radius, so its gap to any shape is the distance
from its centre to that shape less the radius, and nought where the shape reaches into it. The shape's own distance to a point
does the measuring, so every shape is read as it reads a point: a point, a curve and a plane as they are, a polygon, a triangle and
a face as filled surfaces (a hole is not material: a ball of radius 30 about (5, 0, 0) is 5 from the square 200 by 200 with a hole 80 by
80 about the origin), a circle and an ellipse as the discs they are, a curved loop to its outline (a ball in the middle of a large curved loop is apart from it), a box and a body as their
material, and another ball as a ball: the ball of radius 10 about (200, 0, 0) is 140 from the one above, 200 less 60, whichever is
asked. Unlike the line and the ray, which are not measured from a curve in space, a ball is, because the distance from a curve to a
point has a closed form: the arc of radius 100 about the origin in the floor, a quarter turn from (100, 0, 0), is 50 from the ball.
Every shape in space answers `DistanceTo`, `CollidesWith`, `GetShortestLineTo` and the crossings below about a ball as the ball
answers about it, to the bit, and the shortest line turned round is the one the other side gives: a point, a segment, a ray, a line,
an arc, a circle, an ellipse, an edge, a polyline, a polygon, a curved chain, a curved loop, a triangle, a face, a plane, a box of
either kind, a body and a ball. A point answers with `Contains` and `IsPointOn` and not `CollidesWith`, as it does for every shape.

`CollidesWith` is the gap being no more than the point tolerance: the plane 50.0005 above the ball of radius 50 about the origin touches it
and the plane 50.002 above does not. The shortest line leaves the surface of the ball for the point of the shape nearest its centre and is as
long as the gap; where the shape reaches into the ball it has no length and lies at that point, the centre itself where a body holds it.
Near the centre of a small arc, where the tolerance would read the centre as a point with no direction, the line still lands
where the ball is nearest the arc: the ball of radius 49.99 about (0.0009, 0, 0) is joined to the lower half of the circle of radius 50
about the origin by a line 0.0091 long that lands on (50, 0, 0).
A bend of a curved edge, chain or loop is read the same way: the ball of radius 49.99 about (0, -0.0009, 0) is joined
to the edge from (-50, 0, 0) to (50, 0, 0) with a bulge of 1, through (0, -50, 0), by a line 0.0091 long that lands on
(0, -50, 0).
Two balls that meet have one of no length at the middle of the stretch of the line between their centres that both hold.

**Where the surface is crossed** is asked with `GetIntersections` and `TryIntersectWith`, of a segment, a ray, a line, an arc, a circle,
an ellipse, an edge, a polyline, a curved chain, a polygon, a triangle, a face and a curved loop. It is the surface that is crossed, not the
ball: a line through the middle crosses it twice, in order along the line, one that grazes it within the point tolerance once, at its
foot, and a line or curve that lies wholly inside collides with the ball and crosses it nowhere, as does a curve that lies on the surface itself. A flat shape is crossed where its
boundary meets the surface, so the square from (-100, -100, 0) to (100, 100, 0) holds the ball of radius 50 and is crossed nowhere, while
the strip from (-30, -100, 0) to (30, 100, 0) is crossed at (30, -40, 0), (30, 40, 0), (-30, 40, 0) and (-30, -40, 0), in the order its sides are
met. A curve in a plane meets the surface where it meets the circle that plane cuts from it, by the library's own crossing of two curves in a plane;
a corner on the surface counts once. A body or a box is asked whether it touches the ball and how far from it, and where their surfaces cross is
the clash's question, asked of `ToSolid`. A plane and another ball do not cross in points but in a circle:
`TryGetIntersectionCircle` gives it, in the plane, about the foot of the centre (the ball of radius 50 about (0, 0, 30) cut by the floor is the circle of
radius 40 about the origin), and for two balls about the point on the line between the centres where their surfaces cross, square to it, with
its normal along the line from the ball asked to the other: the ball of radius 50 about the origin and the ball of radius 30 about (50, 0, 0)
share the circle of radius 28.62 about (41, 0, 0). `TryGetTangentPoint` gives the one point where they only touch, within the point tolerance either
side (for two balls halfway between the surfaces on the line of centres, so within the tolerance of both), and the two are never both true. Touching is read within the
tolerance, so shells that cross within it of each other, or a plane that cuts less than it deep, touch at a point though the true circle can be a few units across: sqrt(2 R tol), 4.5 for a radius R of 10 000; balls on one centre share the whole surface or none of it, and give neither. Asked from either ball the
circle is the same to the bit but for the sign of its normal.

**Meshing.** `ToSolid(chordTolerance)` is a closed body of triangles facing outwards and `TriangulateSurface(chordTolerance)` its triangles: an
icosahedron of twenty triangles, each cut into n by n by lines parallel to its sides, with every corner pushed out onto the sphere and the least n
for which no triangle strays further than the tolerance from it. A ball of radius 50 within 0.1 is 20 by 13 by 13, 3 380 triangles, and within 0.01
20 by 39 by 39, 30 420, which hold 0.036 % less than `Volume`, as the corners lie on the sphere and the body inside it. A tolerance that needs more
than 1 048 576 triangles is refused, as it is for the chords of a circle: radius 50 within 0.0001 would need three million, and a ball of radius 1 can be
cut within 5.7E-6 and not 5E-6. A tolerance of nought picks 0.2 % of the radius. A default ball is refused, and one of a radius below about 0.01 has triangles thinner than the point tolerance and may mesh into a body the library calls open at the default tolerance. A corner shared by two triangles is the same to the bit in both, so the
body is closed with no weld. A ball goes into a clash as `ball.ToSolid(0.01)`, which is as exact as the tolerance.

**Writing, showing and reading from IFC.** [`ObjWriter`](xref:GeometryHelper.Export.ObjWriter), [`DxfWriter`](xref:GeometryHelper.Dxf.DxfWriter) and [`Wkt`](xref:GeometryHelper.Export.Wkt) take a ball with a chord tolerance and write the triangles of
`TriangulateSurface(chordTolerance)`, as they write a body: the ball of radius 50 within 0.1 is 3 380 triangles in each (a 3DFACE apiece in DXF, a
`POLYHEDRALSURFACE Z` in WKT), the tolerance a finite number above nought and one that needs more than 1 048 576 triangles refused; see
[Common](../common.md) for the writers. The viewer shows a ball as that mesh cut at its chord tolerance (a tenth of the point tolerance of the scene) in
at most 262 144 triangles, with a dot at its centre and three great circles, which are exact, and its centre snaps as a centre. A ball
of radius 50 is drawn in 255 380 triangles, none further than 0.0012 from the sphere; one of radius 5 000
in as many, within 0.114, and the item is warned of where the mesh may lie further from the sphere than ten point tolerances. All the balls of a scene
together are drawn in at most four million triangles: 40 balls of radius 100, which would take 10.2 million, are all cut within 0.0062 of their spheres
instead, 3 808 800 triangles, and the scene is warned of it once. From an IFC file,
`IfcStoreCache.GetSpheres(guid)` of GeometryHelper.IfcConvert gives the `GeoSphere3` of each `IfcSphere` among the items of a product's body, at the
root of an `IfcCsgSolid` among them, or in a mapped item (placed by the mapping, a uniform scale of it scaling the ball), with its position and radius as the
file gives them, where `GetSolids` gives the same ball as the engine's mesh, 1.2 % under its volume for a radius of 0.5 m. An element that openings cut
gives no ball when `ApplyVoids` is set, a ball with a void in it not being a ball.
