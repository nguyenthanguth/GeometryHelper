# Ellipses and splines in space

## Ellipses in space

[`GeoEllipse3`](xref:GeometryHelper.Geometry.GeoEllipse3) is the ellipse of [the plane half](../plane/curves.md#ellipses) standing in space: a centre, the normal of
its plane, the direction of its major axis in that plane, and the two semi-axes. It is read the way a
[`GeoCircle3`](xref:GeometryHelper.Geometry.GeoCircle3) is read, as a flat disc, and everything it does in its own plane is the plane ellipse's
arithmetic, so its length, its walking and its flattening are the plane ellipse's to the last bit.

```csharp
var ellipse = new GeoEllipse3(center, GeoVector3.ZAxis, GeoVector3.XAxis, 300.0, 100.0);

ellipse.Area;                                   // 94 247.78
ellipse.Length;                                 // 1 336.4893
ellipse.MinorAxis;                              // (0, 1, 0): the normal crossed with the major axis
ellipse.GetPointAtAngle(Math.PI / 4);           // (212.132, 70.7107, 0), the eccentric angle as in the plane
GeoEllipse3.FromCircle(circle);                 // a circle as an ellipse, its angle starting where the circle's does
```

A major axis given a little out of the plane is laid into it; one along the normal is refused. The minor
axis is the normal crossed with the major one, so the angle runs counter-clockwise seen from the tip of the
normal, as a circle's does.

**A disc for distance, the rim for crossings.** A point is in the ellipse when it lies within the planar
tolerance of the plane and inside the outline, and its distance is to the nearest point of the filled disc.
A point above the disc is measured straight down to it; one beside it, to the rim. The rim points are all
at the same height above the plane, so the nearest of them to a point in space is the nearest to its foot,
and that is exact.

```csharp
ellipse.DistanceTo(new GeoPoint3(0, 0, 50));                 // 50, straight down
ellipse.Contains(new GeoPoint3(0, 0, 50));                   // false: off the plane
ellipse.Locate(new GeoPoint3(300.0005, 0, 0));               // OnSide, within the point tolerance of the rim
ellipse.GetClosestPointOnBoundary(new GeoPoint3(400, 200, 80));
                                                             // (275.5969, 39.5057, 0), 218.2534 away
ellipse.GetClosestPointOnBoundary(new GeoPoint3(0, 0, 50));  // (0, 100, 0): at the centre, the end of the minor axis
```

Crossings and collisions are asked of the rim, as they are of a circle's, against everything a circle in
space is asked about and another ellipse besides: planes, segments, rays, arcs, circles, triangles,
polygons, faces, bodies, both boxes, edges and the arc chains. A shape lying in the ellipse's plane is laid
out and answered by the plane ellipse; anything else meets the rim on the line the two planes share. A
segment through the middle of the disc neither crosses nor touches it, as it does not touch a circle's rim. Two
ellipses are read in one plane where either lies within the planar tolerance of the other's plane, whichever is asked
first: the ellipse 192 by 32 in the plan and one 496 by 46 tilted 3E-6 radians, their rims within 0.0003 of each
other at four places, collided asked one way round and not the other; of 4 000 such pairs tilted 1E-7 to 1E-3
radians, 339 answered by the order they were asked in, and none does now. Two ellipses in no one plane are read from
the one that comes first by centre, radii, normal and axis, whichever is asked first, and their rims touch where they
pass within the point tolerance of each other without crossing on the line the planes share, at the middle of the
shortest line between them: the ellipse 300 by 100 in the plan about the origin and an ellipse 200 by 100 leaning 0.01
radians, the end of its major axis 0.0005 above (0, 100, 0) and its rim running square to the first there, touch at
(0, 100, 0.00025), and 0.0011 above, not at all. Of 4 000 pairs drawn to pass within 0.0012 of each other at tilts up to
a radian, 134 answered by the order they were asked in and 1 517 came within 0.00098 of each other and touched neither
way round; none does now.

Where the one rim comes within the tolerance of the other's plane, it is searched for where the gap between the rims
comes within the tolerance, piece by piece, a piece passed over only where bounds on the gap prove it further than the
tolerance: how fast the rim moves, its height above the other's plane, and how far its foot on that plane lies from the
other's ellipse. Each run of the rim within the tolerance of the other is one place, where the gap along it is least: two
copies running along each other within the tolerance for a long way touch at one place, where they come nearest, and
the ellipse 3 000 by 1 000 and its copy 0.0012 above, leaning so that its rim is 0.0001 to 0.0023 above, touch at
(0, -1 000, 0.00005). Two runs are two places wherever the gap rises above the tolerance between them, however near each
other along the rim, unless the other curve runs within the tolerance of the rim all the way between: a circle of radius
247 passing a rim 5 995 by 2 863 within 0.018 and 0.017 at two places 11.4 apart, the gap rising to 0.069 between, at a
tolerance of 0.05, touches it at both, and a circle of radius 2.12 crossing a needle 33.5 by 0.00013 twice, 0.141 apart,
crosses it at both. Pieces not proved clear are halved until the rim runs no more than twice the tolerance along them;
halving stops after 384 readings, which only rims running along each other within a few tolerances for a long way
reach, and what is left is brought down by Brent's method, the one part not proved. A pair not in one plane costs a few
microseconds where the rims are far apart, and up to about a millisecond for long copies running along each other.

The rim read is read at points no further apart than twice the tolerance along it, so two places the other ellipse
passes within the tolerance, the gap rising above it between over a shorter stretch than that, can lie between two
readings and be one; and where the other is narrow, its two sides a few tolerances apart, the rim read can pass both so:
a rim 30 080 by 18 333 passing 0.000013 from either side of a needle 2.78 by 0.0028, leaning 0.56 radians off its plane,
touched it at one side only. So where the other ellipse's rim lies no more than 32 tolerances from its major axis beside
a place found, it is searched too, across its inside, where its other side is, beyond the stretch about the place that
runs within the tolerance, and a place found there is given unless it stands within the tolerance of a place given
already or either rim runs within the tolerance of the other all the way to one: the two touch at both sides now, 0.0026
apart, whichever is asked first, and the end of a rim 100 000 by 1, which a copy 0.001 larger in its plane rings within
the tolerance, is still one place at each end. Searched only within four tolerances, an ellipse 21.25 by 0.93, b² / a
4.07 tolerances at a tolerance of 0.01, passing a rim 5.26 by 3.59 at two places 0.058 apart missed the second, and
searched only within eight, an ellipse 23.46 by 1.379, its rim 9.29 tolerances from its major axis beside the touch,
passing a rim 10 336.8 by 7 088.3 at two places 0.0755 apart, the gap rising 4 % above the tolerance between, did too;
and searched from inside the stretch about the place found, near the end of the major axis, the search read a touch
beyond it as the same place: a rim 36.66 by 30.95 passing an ellipse 21.88 by 0.908, b² / a 3.77 tolerances, within
0.0020 either side of its end, the two places 0.056 apart and the gap rising 4 % above the tolerance of 0.01 between,
touched it once, and touches it at both now. Read in one plane, the same holds. Only the sides of the ellipse not read,
and only where it is so narrow, are parted so: two touches a narrow ellipse's own reading makes one stay one, as a
needle 0.754 by 0.108 read first, passing a rim 31 536 by 14 839 at two places 0.0357 apart at a tolerance of 0.01, the
gap rising 15 % above it between, is given one place. Of 7 500 rims drawn across the end of an ellipse whose b² / a is 2
to 24 tolerances, at tolerances of 0.001, 0.01 and 0.05, each passing it within the tolerance at two places with the gap
rising up to 15 % above it between, 27 are given one place, where 164 were before: 22 read from the ellipse, and 5 read
from the rim, the ellipse 14.9 to 21.9 tolerances wide beside the place and the gap rising under 0.7 % above the
tolerance between.

Two ellipses that each lie within the planar tolerance of the other's plane are read from the
one that comes first too, and give the same points to the last digit either way. A circle or an arc passing the rim
within the point tolerance touches it in the same way: the circle of radius 50 leaning 0.01 radians, the end of its
diameter along X 0.0005 above (0, 100, 0), touches the ellipse 300 by 100 at (0, 100, 0.00025); of 2 000 circles drawn to
pass within 0.0012 of an ellipse's rim, 1 056 came within 0.00098 and collided nowhere, and none does now. Where one rim
crosses the other's plane, the crossing is kept only where it lies within the tolerance of both rims, and two crossings
no further apart than twice the tolerance are one: a rim 182 by 0.004 only grazing the plane of another is read at the
middle of its chord there, which lay 0.0084 off it, and the search gives the place instead. A circle and an arc are
asked about an ellipse as the ellipse is asked about them, `circle.CollidesWith(ellipse)`,
`arc.GetIntersections(ellipse)` and `circle.TryIntersectWith(ellipse, out points)` giving the ellipse's answer and
points, in its order.

Read in one plane, the other curve is laid flat in the plane it is read in with its radii kept, though it may lean off
that plane by up to the planar tolerance, or, where only the one lies within the tolerance of the other's plane, by far
more: an ellipse 1.97 by 0.0023 lying within the tolerance of the plane of one 15 338 by 15 274, which leans 0.02 radians
off its own, saw the huge rim 3.1 off its true place where the two pass 0.00016 apart. So a crossing read flat is kept
only where it lies within the point tolerance of both rims in space, a touch read flat only where the rims come within
the tolerance of each other in space, and the rim is then searched in space for where the two pass within the tolerance
of each other, as rims not in one plane are, a run within it one place; the same ellipse, or an ellipse and a circle with
their centres and radii within the tolerance, still cross nowhere. That pair touches near (-5595.742, -6793.740,
-12510.089) now whichever is asked first, and two ellipses read in one plane give their points in the opposite order
asked the other way.

Two ellipses draw the same curve where their centres and radii are within the point tolerance and their axes along one
line, or both are round within it, their two radii no further apart than it, whatever their axes, as one 100.0004 by
99.9998 and one 100.0003 by 99.9999 turned a quarter about the same centre; or where one is a circle, its two radii one
to within 1E-12 of the larger, and the centres and both radii of the other are within the tolerance of the circle's,
whichever way its axis points, as an ellipse and a circle do: the ellipse 100.0007 by 99.9995 and the circle of radius
100 about its centre cross nowhere asked as a circle, as the circle read by `GeoEllipse3.FromCircle`, where the second
gave four places, or as the circle drawn as an ellipse 100 by 99.99999999999999, its radii a rounding apart, which gave
four too. Two ellipses neither of which is drawn as a circle, nor both round, are not one curve with their axes turned:
one 1.04978 by 1.04840, its radii 0.00138 apart, and one 1.04936 by 1.04903, their centres 0.00032 apart, cross at four
places. Curves drawing the same one touch where either holds where the other starts, or where their rims come within the
point tolerance anywhere else, the rim searched for that, about half a millisecond to a millisecond and a half: an
ellipse 100.0009 by 99.9995 about that circle's centre, its major axis towards where the circle starts and leaning 8E-6
radians, stands 0.0012 off the circle at either start and comes within 0.00042 of it elsewhere, and touches it, the
circle read as an ellipse, and an arc of it passing there.

```csharp
ellipse.GetIntersections(new GeoPlane3(new GeoPoint3(150, 0, 0), GeoVector3.XAxis));
                                                  // (150, -86.6025, 0), (150, 86.6025, 0)
ellipse.GetIntersections(new GeoEllipse3(center, GeoVector3.ZAxis, GeoVector3.YAxis, 200, 150));
                                                  // (±134.1641, ±89.4427, 0): two ellipses in one plane cross four times
```

The line two planes share is found from the shape's own centre, for the ellipse and the circle alike, not from
the world origin, which keeps the crossings within 2E-7 of where they are on planes a thousandth of a degree
apart, a thousand from the origin. Whether a line grazes the rim is judged in drawing units, so an ellipse 210
by 0.23 touched by a plane is touched at one point.

**Seen from elsewhere.** A circle in space stays a circle only under an even scaling, and refuses any other, a
shear or a skew that keeps both lengths among them: round means that the two axes of its plane come out of the
transformation so nearly equally long and square to each other that the circle moved lands within the global point
tolerance of where the transformation puts each of its points, as a ball is read: a turn whose numbers are rounded to six
places on their own moves the circle of radius 100 at the scale it is drawn, 0.000065 off, and not scaled by 1 000, where
it would be 0.065 off, and the skew that sends X to (1, 0, 0) and Y to (0.6, 0.8, 0), which a circle of radius 100 in the plan came through at
radius 100, is refused, since it would be an ellipse. The plane is carried by its normal, made of unit length by its own
length, so an even scaling of 1E5 or more carries it as any other does: the circle of radius 10 scaled by a million is the
circle of radius 10 000 000, where the normal, a millionth long, was read shorter than the vector tolerance and the
scaling refused. A turn, a shift, a mirror or an even scaling of its
plane moves it whole, by its centre and its radius, so an arc of any sweep moves, one a hair short of a whole turn
among them, which threw; under any other transformation, a skew or a stretch of its plane, the arc through where its two
ends and its middle land stands for the ellipse only where it lands within the global point tolerance of where the
transformation puts the rest of it, and is refused otherwise: a stretch of 3E-6 moves the arc of radius 100 sweeping a
whole turn less 1 at the scale it is drawn, 0.00035 off, and refuses it scaled by 1 000, where it would be 0.35 off, as
its circle and a ball are refused, and that skew refuses the arc of radius 100 in the plan from 0.5 to 2 radians, which it
took to an arc of radius 177.7 far off the ellipse. An edge with a bulge is judged the same way, where it was moved
through its three points under any map: the edge of bulge 0.5 from the origin to (10, 0, 0) stretched to twice along a
diagonal of its plane is refused, and stretched by a millionth it moves, 8.3E-7 off. Each point is read from the
moved start, so an edge 100 long bowing by 1E-12 moves under a turn rounded to six places. An arc of a whole turn, which
has no ends to go through, is refused as the circle is, and one past a half turn whose ends the skew brings within the
global point tolerance of each other is refused with an `ArgumentException`; under a turn rounded to a float or to six
places, which carries a circle, such an arc is moved whole as the whole turn is. An ellipse takes every transformation that can be undone, and comes back an ellipse. Seen square onto a
plane, a circle or an ellipse is an ellipse in that plane:

```csharp
var tilted = new GeoCircle3(new GeoPoint3(1000, 500, 200), new GeoVector3(0, -Math.Sin(th), Math.Cos(th)), 300);
                                                  // th = 60°: a circle tilted 60° from the plan

tilted.ProjectToPlane(GeoPlane3.XY);              // an ellipse 300 by 150 about (1000, 500, 0)
tilted.ProjectToEllipse2(GeoCoordinateSystem3.Global);  // the same, as a GeoEllipse2 in plan
GeoEllipse3.FromCircle(circle).TransformBy(GeoTransform3.Scaling(1, 2, 1));
                                                  // a circle of radius 100 stretched: 200 by 100
```

A shape standing square to the plane, which would show as a segment, is refused with an
`InvalidOperationException`; one leaning off square by more than a millionth of a millionth of a radian
comes back, however thin.

Left out, as they are for the plane ellipse or the circle in space: offsetting, since the offset of an
ellipse is not an ellipse; the booleans; elliptical arcs; and the distance to anything but a point, for
which `ToPolylineByChordTolerance` gives a chain that errs on the safe side.

## Splines in space

[`GeoSpline3`](xref:GeometryHelper.Geometry.GeoSpline3) is the NURBS curve of [the plane half](../plane/splines.md) in space: a degree from 1 to 10, control points, knots kept as
they were given and, when it is rational, weights; clamped, so it starts at its first control point and ends at its last. It is made,
read, cut and measured as the plane's is, by the same engine, so everything said there about its exactness holds here: the point at a
parameter, derivatives, tangent, `Length`, `GetAabb` (a [`GeoAabb3`](xref:GeometryHelper.Geometry.GeoAabb3)), `Split`, `Reverse`, `Flatten(chordTolerance)` into a
[`GeoPolyline3`](xref:GeometryHelper.Geometry.GeoPolyline3) (a part no longer than the point tolerance, as `Split` cuts off near an end, the chain of its corners as they are), and the two kinds of parameter: the members named for a parameter (`GetPointAtParameter`, `GetParameterAtPoint`, `Split`)
take a share of the domain from 0 to 1, those named for a knot (`GetPointAtKnot`, `GetKnotAtPoint`, `SplitAtKnot`) a value of the
knots, and those named for a distance (`GetPointAtDistance`, `GetParameterAtDistance`, `GetDistanceAtPoint`) a length. In space a curve has no left to turn to, so `GetCurvatureAtParameter` and `GetCurvatureAtKnot` are never negative.

```csharp
var arch = new GeoSpline3(3,
    new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 150, 60), new GeoPoint3(200, 150, 60), new GeoPoint3(300, 0, 0) },
    new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

arch.GetPointAtParameter(0.5);                   // (150, 112.5, 45)
arch.GetAabb();                                  // 300 by 112.5 by 45, not the 300 by 150 by 60 of its control points
arch.DistanceTo(new GeoPoint3(150, 200, 45));    // 87.5, and point.DistanceTo(arch) is the same number to the bit

var floor = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 100));
floor.DistanceTo(new GeoPoint3(0, 0, 30));       // 104.4: 100 across and 30 up from every point of the rim
```

`GeoSpline3.Interpolate(fitPoints, degree)`, with or without the two tangents, `FromControlPoints`, `FromPolyline`, `FromArc` and
`FromCircle` (any plane: the circle is nine control points and every point of it is on the circle to rounding) are as in the plane. The
nearest point is found as it is there, from the roots of a polynomial on each span, so `DistanceTo`, `GetClosestPointOnBoundary`,
`GetKnotAtPoint`, `GetParameterAtPoint`, `GetShortestLineTo` and `IsPointOn` are exact to rounding and the same from either side;
the boxes of the spans are kept in a tree, built on the first query, so the spans near the point are reached without looking at the rest and
a query takes the logarithm of the number of spans (about 0.01 ms at 100 000, where it took 0.5), with every answer the same to the bit as it was
before the tree. `Interpolate` takes a
[`Tolerance`](xref:GeometryHelper.Tolerance) that decides which neighbouring fit points are one point; points just above it make the curve swing far, as in the plane. A spline is carried by
`TransformBy(GeoTransform3)` by its control points, exactly for every affine map; `ProjectToSpline2(frame)` lays it square onto the
plane of a frame, and `GeoSpline2.ToSpline3(frame)` and `PlanarMap.ToSpline3` put one back, the weights and knots unchanged. A spline
that stands square to the plane has no spline to give there, and is refused.

**Writing, showing and reading from IFC.** `DxfWriter.Add(spline, layer)` writes a spline in space as the SPLINE it is, as
[in the plane](../plane/splines.md), its control points in world coordinates: flagged planar, with the plane's normal turned up in its largest
component as the extrusion, where its control points lie in one plane to the rounding of their coordinates (the circle of radius 25 on the
normal (1, 1, 0) has the extrusion (0.7071, 0.7071, 0)), and with no flag and no extrusion where they do not, as for the cubic through
(0, 0, 0), (100, 0, 0), (100, 100, 0), (100, 100, 100). `ObjWriter.Add(spline, chordTolerance, name)` and `Wkt.Write(spline, chordTolerance)`
write the line of its chords, every corner of `Flatten` kept and the ends exact, as an OBJ line and a `LINESTRING Z`. The viewer draws,
measures, snaps to and reads a spline in space as it does one in the plane; a SPLINE of a DXF file that lies on the floor with its extrusion
up comes back as a [`GeoSpline2`](xref:GeometryHelper.Geometry.GeoSpline2), and any other as a `GeoSpline3`. From an IFC file, `IfcStoreCache.GetSplines(guid)` of
GeometryHelper.IfcConvert gives the `GeoSpline3` of each `IfcBSplineCurveWithKnots` and `IfcRationalBSplineCurveWithKnots` of a product: an
item of any of its representations, the 'Axis' of a member above all, an element of a geometric set, the parent curve of a segment of a
composite curve, the directrix of a swept disk (the part of it the disk runs along, from its `StartParam` to its `EndParam`, which are
values of the knots), or in a mapped item, placed by the mapping and the product as a solid is, the knots each
repeated as the file says ((0, 0.5, 1) repeated (4, 1, 4) is 0, 0, 0, 0, 0.5, 1, 1, 1, 1) and the weights as it gives them. A periodic curve,
whose knots run on past its ends, is left out with a warning, GeometryHelper's splines being clamped.

### Against the other shapes in space

A spline in space answers the five queries about every shape in space, from either side, as a ray and a line without ends do:
`DistanceTo`, `CollidesWith`, `GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`, each with a tolerance and without, about a
segment, a ray, a line without ends, a plane, a polyline, a polygon, a triangle, a face, a box, an oriented box, a solid, a ball, an arc, a
circle, an ellipse, an edge, a curved chain, a curved loop and another spline. The shape's member hands the question to the spline's, so
`plane.DistanceTo(spline)` is `spline.DistanceTo(plane)` to the bit, the crossings are the same points, and the shortest segment is the same
turned round. The other curves in space are not measured against one another (an arc, a circle or an ellipse is asked about a segment or a ray
for whether they touch and where, and no distance is given), but a spline measures the pair itself, so it answers all five about each of them.

```csharp
// arch: the cubic (0, 0, 0), (100, 0, 150), (200, 0, 150), (300, 0, 0), the parabola z = x (300 - x) / 200 in the plane y = 0.
arch.GetIntersections(new GeoPlane3(new GeoPoint3(0, 0, 100), GeoVector3.ZAxis));                  // (100, 0, 100), (200, 0, 100)
arch.DistanceTo(new GeoPlane3(new GeoPoint3(0, 0, 113), GeoVector3.ZAxis));                        // 0.5, over the top
arch.DistanceTo(new GeoSphere3(new GeoPoint3(150, 0, 150), 30));                                   // 7.5: 37.5 from the centre to the top, less the radius
arch.GetIntersections(new GeoAabb3(new GeoPoint3(100, -10, 100), new GeoPoint3(200, 10, 150)));    // (100, 0, 100), (200, 0, 100): in through one edge, out through the other
ring.GetShortestLineTo(new GeoAabb3(new GeoPoint3(-100, -100, -10), new GeoPoint3(100, 100, 10))).Length;  // 10: the ring is inside the box, and the floor and roof are 10 off
```

**The rules are the other curves'.** A flat shape, a triangle, a polygon or a face (the material of it, holes left open), is filled: a spline that
pierces it, or lies in it, is nought from it, and its shortest segment is of no length there. A circle and an ellipse are the discs they are
for the distance, as their own distance to a point reads them, and rims for the crossings: a ring through the middle of a standing disc is
nought from it, collides with it and crosses nothing. A box, an oriented box, a solid (its openings cut in) and a ball are material: a spline
inside is nought from them, crosses nothing, and its shortest segment is the gap out to the skin, as it is for a spline in a closed shape in the
plane (the ring inside the box 200 by 200 by 20 is 10 from it). A plane is a surface without ends and not a half of space, a curved chain
and a curved loop are the paths they are (the ring inside the square outline of a loop is 50 from it, not nought), and the spline itself is a
curve with no inside. The distance is nought only where the two truly meet, a crossing or a touch found to rounding, and otherwise the gap,
measured exactly: it is never read through the tolerance, the planar tolerance is not read at all, and `CollidesWith` is the distance being no
more than the point tolerance, exactly, so the two never disagree. A segment you give no longer than the point tolerance, or a straight edge that
short, is read as its start where it does not truly meet the spline, as every shape reads one.

**A place is a least of the distance.** Two curves in space seldom meet, so a place is where the distance of the spline from the shape is least
within the point tolerance: a crossing is a place where it comes to nought, a touch within the tolerance is another, and the tolerance alone
tells a touch from a near miss (the line 0.0005 over the top of the ring touches it at (0, 50, 0) and the line 0.002 over it touches nothing; a
spline that passes a ten-thousandth of a millimetre from a line is at a place as one that crosses it is). A flat shape is pierced where the spline
passes through it, and crossed at its outline; the ring pierces the vertical polygon across the plane y = 30 at (-40, 30, 0) and then (40, 30, 0),
in order along it from its start at (0, 50, 0). A stationary point of the distance that is a greatest is not a place, nor is a point on a slope,
and one that is a least with no curvature is, which in space a spline over a line that is tangent to it and a thousandth above its plane has: the
distance is of the fourth power there, and is told from a level one by being higher a thousandth of the size of the spline either way along it (or
a hundredth or a tenth, inside the piece, for the cubic y = x³ / 100 under the line along x half a thousandth above its plane, whose distance is of
the sixth power) and lower nowhere on the way. A least that curves up is one only where the distance falls neither way: the cubic's offset from
the line y = 0.0001 stops at its inflection and goes on falling, which is no place, and the line crosses it once, at x = 0.21544. Places no further
apart than the point tolerance are one place, as are two near touches next to each other along the spline within ten times that with no point
between them farther from the shape than at either by more than four snaps (a snap is 1E-13 of the size of the numbers); two crossings are one place
only when they are within the tolerance of each other, and a meeting of two curves in one plane that is one only to the snap and no least
of the distance is left out beside a meeting to the digits, as in the plane (a quadratic sitting on a circle spline of radius 100 laid in a
tilted plane a million out touches it once, where it was given three places), and a least of the distance beside a crossing does not join two. A closed spline, its first and
last control points the same to the bit, has no ends, as in the plane: a line 0.0002 off the plane of a circle spline of radius 100, tangent to it
0.003 of a radian past its seam, touches it once, where the seam was given too.

**How each pair is solved.** Against a segment, a ray or a line without ends the part of a span's offset that stands square to the line is taken
as two signed polynomials in Bernstein form, along two directions square to the line: the span meets the line where both are nought, and a span in
a plane through the line has one of them nought all along and the other its signed offset in that plane, whose roots are the crossings however
shallow (a line a millionth of a radian off the tangent of a cubic in its plane crosses it and is nought from it, where the square of the offset
has a double root that rounding blurs) and whose stationary points are the touches, as in the plane. Out of such a
plane the places it comes nearest are the stationary points of the squared offset, the sum of the two squares, each brought to the least of the
distance by Newton's method on the distance and, where that has no curvature to go by, by a golden section down the slope it is on. Against a
plane the signed offset of each span is a polynomial, whose roots are the crossings and whose stationary points are the touches, and a dip of the
spline a few snaps below a plane between two crossings is a greatest of the distance and no place; against a ball the squared distance from the
centre less the squared radius is one of twice the degree. A flat shape is its plane, taken where the foot of the place stands over the shape, and the segments of its
outline; a box is its six faces and twelve edges and a solid its faces, a face being a flat shape. Against an arc, a circle, an ellipse or another
spline the pair is cut in halves while the hulls are near enough to matter, the nearest bound first, and the nearest pair is brought to rounding
by Newton's method, as it is in the plane (an arc, a circle and an ellipse are each made into the rational spline that is exactly it first). A piece lies along a plane, a
line or a ball when it is within a share of the size of the coordinates of it, 1E-13, which a polygon turned about in space, a hundred thousand out,
carries into the numbers it is made of: two splines in one plane, either tilted and a million from the origin, give what they give in the plane.

**Two curves in one plane, and two that share none.** Two splines that lie in one plane, every control point of both within a snap of it, are
worked out in that plane as two splines of the plane are, and the points found are put back in space, so they give the plane's answers: the sign
of the offset of one from the other tells two curves a hair apart that cross twice, two circles of radius 1 000 and 1 000.00002 with their centres
1E-4 apart cross at an angle of 1E-7 in two places in any plane at a tolerance of a hundred-thousandth (and are one circle at the default one, as
in the plane), and a polyline spline through (0, 0), (100, 0), (100, 100), (200, 150), (150, 300)
and its copy moved (0.0003, 0.0004) along its plane, which lie along each other within the tolerance all the way, cross at the three corners,
(100, 0.0004), (100.0003, 100.00015) and (199.99993, 150.00021). In a plane square to an axis the answers are the plane's to the bit; a tilted plane
is laid flat by two directions of its own, and its answers are those of the plane turned, which for two curves that run a hair from each other the
plane itself can give a place more or fewer for (none of 60 such copies turned by 0.7 radians in the plane, where 5 did, and 4 of 200 more, where 18 did). An arc, a circle, an ellipse, an edge,
a chain of edges, a segment, a ray or a line without ends in the plane of a spline is laid flat with it the same way for the places: a circle spline
of radius 100 moved (0.0003, 0.0005) along its plane is one circle with its circle and with an arc of it, as in the plane, and a segment along the
first side of a polyline spline, 1.15E-6 off it and turned by 7E-8 of a radian, is given the one place the plane gives, where it was given the spline's
start besides; the plane's answer is to the bit in a plane square to an axis, and to a millionth in a tilted one along so slight a crossing. A spline,
a segment and a line nearly on one line lie in no plane the digits can tell, and are measured in space. Two curves that share no
plane can still cross, as a helix crosses a straight spline along its cylinder once a turn. Inside a stretch where they lie along each other, a
place is a least of the distance where the two would cross if brought into one plane: seen along the gap at the least, the first stands on one
side of the second just before it and on the other just after, clear by a hundredth of the tolerance before it goes back. Lifted 0.0002 off the
plane, the copy of the polyline spline comes nearest it, 0.0002 apart, at the same three corners, where seen from above an edge of the one goes
across the other; a spline and its copy moved 0.0005 across its plane with a wobble of 0.0001, lifted off it, come nearest where the wobble brings
them nearest but stay on one side of each other, and meet nowhere, as in the plane. A spline and its own copy moved less than the tolerance out of
every plane are given a place at each least of their distance along the stretch, though the distance there is not nought, since seen along the gap a curve and its copy moved along its tangent cross
where the curve bends: a twisted cubic of 600 points and its copy moved 0.0009 are given 69 places, in about 0.1 s. The stretch is looked at four times as
often as in the plane, closer where the first spline turns, at every join of its pieces and where every join of the other is nearest it. In space
the step of Newton's method on the pair is taken as the cross product of the tangents makes it, which keeps its digits where the product less
the square of the dot has none, and with no damping first. A million out the digits are 2E-10 apart and two circles crossing at 1E-7 are within a snap, 2E-7, of each other for two
millimetres round each crossing: it has the places the rounding gives. A touch of two splines that share no plane, tangent to each other, reads a
snap or two from nought (6E-11 to 1.7E-10 for a spline about 1 000 across, where the snap is 1E-10), Newton's method following the valley of the
nearest pairs until a step gains less than a snap.

**Limits, and what is not offered.** The cutting for the nearest approach of two curves is bounded as it is in the plane (past 40 000 pairs of
parts the answer is the best of the real pairs found, with the nearest bound left in a Debug line), as is the cutting for the places (two million
pairs); two circle splines in one plane are met in closed form as the circles are, as in the plane (of radius 100 000 and 100 000.0005 with
centres 2E-4 apart they are 0.0003 apart in a third of a millisecond, and with centres 0.01 apart cross twice), and two that share no plane are
cut, and near-concentric ones can exhaust the budget. An arc, a circle, an ellipse or another spline is worked out as a pair of splines
and costs that (the arch 300 long against a circle takes about 3 ms for the distance and the crossings, against a ball a few microseconds);
a polygon or a face of many corners is asked, for each place that stands over its box, one pass over its corners, after the box has ruled out the rest. A line meeting a circle in a chord whose depth is under a snap, a line cutting the
circle of radius 1 000 in a chord of 0.02 a hundred thousand out (the depth is 5E-8, the snap 3E-8), meets it to the digits all along the chord, and
gives the places the rounding gives. A triangle with no area is the segment that spans it. Where a pair of curves in space lie along each other, no
place is given along the stretch but a crossing, and for two that share no plane a least of the distance where they would cross in one plane. Two
splines that share no plane and stay within the tolerance of each other round a turn tighter than a millimetre have their parts measured once both
are no larger than the tolerance: the copy of a degree-four spline of ten points, moved 0.00074 along its plane and lifted 0.0002 off it, takes
about 0.1 s for its places in any plane.
