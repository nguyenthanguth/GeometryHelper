# Circles, ellipses, rays and infinite lines

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

A frame follows a transformation however small its scaling: scaled by a millionth, it keeps its axes, where a scaling
by 1E-5 or less was refused as leaving it none. It is built along an X axis of any length but none the same way:
(6E-7, 8E-7) points it along (0.6, 0.8), where an axis shorter than the global vector tolerance of 1E-5 was refused.

`GeoRectangle2` is the rotated rectangle — the OBB of the plane — so it carries a frame exactly as
`GeoObb3` does in space.

A loop can be turned round with `Reverse()`, which is what decides winding and which way round a pair of
chamfer distances goes. A curved loop is turned round edge for edge: at a millionth, the rectangle 100 by 50 filleted at
0.0005 reverses to its eight edges, four of them arcs 0.0007 across, where it was put back together within the global
tolerance and came back its four corners.

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
