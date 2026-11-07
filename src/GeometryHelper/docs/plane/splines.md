# Splines

NURBS curves of the plane (GeoSpline2): made four ways, exact to rounding, written to and read from DXF, and asked
against every other shape.

[`GeoSpline2`](xref:GeometryHelper.Geometry.GeoSpline2) is a curve that is one smooth sweep and not a chain of pieces: a NURBS, a B-spline whose control points may pull
on it with different strengths, which is what the SPLINE of AutoCAD and the B-spline curves of IFC hold. It is given by its
degree (1 to 10), its control points, its knots and, when it is rational, its weights. The curve is clamped, so it starts at its
first control point and ends at its last, and the control polygon through the points shows where it goes without touching it.

```csharp
var arch = new GeoSpline2(3,
    new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0) },
    new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

arch.GetPointAtParameter(0.5);             // (150, 112.5): the curve, not the 150 of its control polygon
arch.GetAabb();                            // 300 by 112.5, not the 300 by 150 of its control points
arch.Length;                               // 389.8927
arch.GetDerivativeAtKnot(0.0, 1);          // (300, 450): three times the first leg of the polygon
arch.GetTangentAtParameter(0.5);            // (1, 0), level at the top
arch.DistanceTo(new GeoPoint2(150, 200));  // 87.5
arch.Split(0.3);                           // two cubics that meet at (90, 94.5), exactly

var circle = GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 100));
circle.Length;                             // 628.3185307180, 2 pi r to a relative 1E-12 and better
circle.GetCurvatureAtParameter(0.3);        // 0.01, all the way round
circle.DistanceTo(new GeoPoint2(160, 0));  // 60
```

### The knots are kept as given

A spline read from a file goes back out as it came: nothing is rescaled to [0, 1] or
reordered, and the domain, `DomainStart` to `DomainEnd`, is whatever the knots say. Two kinds of parameter are told apart by name,
as the other curves keep theirs. The members named for a **parameter** (`GetPointAtParameter`, `GetTangentAtParameter`,
`GetCurvatureAtParameter`, `GetParameterAtPoint`, `GetDistanceAtParameter`, `Split`) take a share of the domain from 0 to 1, as
every curve in the library does; the ones named for a **knot** (`GetPointAtKnot`, `GetDerivativeAtKnot`, `GetTangentAtKnot`,
`GetCurvatureAtKnot`, `GetKnotAtPoint`, `SplitAtKnot`) take a value of the domain itself, which is what the knots say.
`GetKnotAtParameter` and `GetParameterAtKnot` turn one into the other: over the domain [5, 12] the parameter 0.5 is the knot 8.5.
Splines made here have the domain [0, 1], where the two are the same number. The ones named for a **distance**
(`GetPointAtDistance`, `GetParameterAtDistance`, `GetDistanceAtParameter`, `GetDistanceAtPoint`) go by length; a distance of
nought or less gives the start and the whole length or more gives the end, as for a chain. The knots are checked: clamped (the first
and the last repeated degree + 1 times and no more), not decreasing, no inner knot repeated more than the degree, one weight above
nought for each control point and the largest no more than 1E12 times the smallest (past that the length cannot be worked out
right: a ratio of 1E16 comes out 1E-7 short and one of 1E20 two fifths short), and control points that are not all one point.
`IsRational` says whether any weight is other than one; a plain B-spline stores no weight and is worked on exactly its own numbers.
A closed spline is a clamped one whose first and last control points coincide, `IsClosed()`; it is not periodic, and has no smooth
join at the end. Asked about other shapes, one whose first and last control points are the same to the bit, as the closed flag of a DXF
spline reads it, has no ends: its seam is a point of it like any other, where it comes in along its last span and goes on along its first.

### Making one

`new GeoSpline2(degree, points, knots, weights)` takes the numbers. `FromControlPoints(degree, points)`
spaces the knots evenly over [0, 1]: seven points of degree 3 have the knots 0, 0, 0, 0, 1/4, 1/2, 3/4, 1, 1, 1, 1.
`Interpolate(fitPoints, degree = 3)` finds the curve through points, as AutoCAD's fit-point splines do, by global
interpolation with chord-length parameters (Piegl and Tiller, 9.2.1): each point is met at the share of the chain's length
that comes before it, the inner knots are the means of a degree's worth of those parameters, and the control points solve one
banded system, so 100 000 points take 58 ms. Four points (0, 0), (100, 50), (200, 0), (300, 50) are met at
0, a third, two thirds and 1. `Interpolate(fitPoints, startTangent, endTangent, degree = 3)` adds the direction the curve
leaves and arrives in; a tangent gives a direction and nothing else, the curve keeps the speed it has on average, which over
[0, 1] is the length of the chain through the points, so three points 100 apart along X with the tangent (1, 0) at each end
give the line through them at an even speed of 200 whatever the length of the tangents. A fit point within the point
tolerance of the one before it is dropped, as a polyline drops a vertex, by the tolerance given (`Interpolate(fitPoints, degree, tolerance)`) or the global one; points just above it are kept, and the curve through three that are 0.0012 apart in a chain 300 long swings 507 000 away, where at a tolerance that drops them it stays within about 13 of the box of the points. Last, `FromPolyline`, `FromArc` and `FromCircle` make a
spline that is exactly the polyline, arc or circle: a circle is the nine control points of a rational quadratic, four pieces of
a quarter turn with the weights 1, 0.7071, 1, ..., and every point of it is at the radius to 4E-14 at a radius of 100, its last control point its first exactly, as for any whole turn; an arc of
a hundred degrees is two pieces of fifty.

### Exact to rounding

The point at a parameter is de Boor's scheme in homogeneous coordinates, a
weighted mean of the control points of the span (11.7 units of rounding, of 2.2E-16 times the size of the spline, at the worst of
400 random splines of degrees up to 10 in the plane, checked against exact fractions). Derivatives to the tenth, the tangent
and the curvature (signed in the plane, positive counter-clockwise; never negative in space) are those of the exact quotient,
to 6E-14. `Length` integrates the speed by Gauss-Legendre of ten points over each Bézier span, the span cut in halves until the
rule over the whole and over its halves agree to 1E-14 of the length of its control polygon, cut again at each place its speed is
least, where a curve that turns back has a corner in the integrand, and a rational span whose weights are more than two to one
apart is cut first: 1.3E-15 against a Romberg integration on 400 random splines. `GetAabb` is the box the curve fills, found from
the roots of the derivative on each span, not the box of the control points. `Split(parameter)` and `SplitAtKnot(knot)` raise the knot to the degree,
so the two parts are the spline exactly and meet at one control point (a cut where the spline stands still, so that a part would be one point, is refused); `Reverse` runs it the other way over the same domain
(each distinct knot is mirrored about the middle of the domain once, so equal knots stay equal and distinct ones stay distinct, a unit in the last
place apart if rounding would join them; the mirrored knots are rounded to the last place of the domain's ends, which moves the curve
by the speed times that). `Translate`, `TransformBy` and `PlanarMap.ToSpline3`/`ProjectToSpline2` carry the control points, which
is exact for every affine map: a circle scaled by 3 along X is the ellipse 300 by 100, all 400 points of it on it to 1E-12.
A transformation that carries every control point to one point is refused.

### Nearest point and distance

`DistanceTo(point)`, `GetClosestPointOnBoundary`, `GetKnotAtPoint` (and `GetParameterAtPoint`, `GetDistanceAtPoint`), `GetShortestLineTo` and
`IsPointOn` find the nearest point of the curve, which is a line and not a region, from either side to the bit
(`point.DistanceTo(spline)`). On each Bézier span the distance stops falling where `(C(t) - q) . C'(t)` is nought, a polynomial
in Bernstein form whose roots are all found by cutting in halves wherever its coefficients change sign twice or more, so a
point inside a loop, one on the axis of a symmetric curve, the centre of an arc, a point at a cusp all give the nearest of
all the places; where two points are equally near the lower parameter is given. A span is solved only if the box of its control
points is nearer than the best found, and the boxes are kept in a tree, built on the first query, so the spans near the point are
reached without looking at the rest: at 100 000 spans of a spiral a query took 0.53 ms before the tree and 0.010 ms after it
(0.70 ms and 0.012 ms under .NET Framework 4.8), and a span solved is about a microsecond; the tree costs about 3 ms, once, and a
spline of up to 24 spans looks at its boxes one by one, which is faster for so few. Every answer is the same to the bit as it was before the
tree. At the worst of 160 random splines and 800 points the distance was 4.3 units of rounding off the exact one.

**Flattening** keeps the chord promise. `Flatten(chordTolerance)` returns a chain whose corners are on the curve and from which
the curve is never further than the tolerance, however fine the tolerance and whatever the weights: a span is cut in halves until
every control point of the piece is within the tolerance of its chord (a Bézier piece lies in the hull of its control points), or
the second differences of a polynomial piece, or the height of the middle control point of a conic, say so with less cutting.
The circle of radius 100 within 0.01 is 256 chords, where 223 would hold, and the cubic above is 128. There is no automatic
tolerance, since a spline has no radius to take a share of; a tolerance that needs more than 1 048 576 pieces is refused.
A spline no longer than the point tolerance, which a chain would merge into one point, is the chain of its corners as they are: the
part 0.000245 long that `Split(1E-6)` cuts off the fit through (0, 0), (30, 20), (60, -10), (100, 0) flattens to the chord from its start
to its end, where it was refused. `Split` keeps such a part, exactly, as it keeps any other.

### Writing, showing and reading

`DxfWriter.Add(spline, layer)` writes a spline as the R2000 SPLINE entity AutoCAD reads: its degree, its
knots, its weights where it is rational and its control points, at height nought, every number to the bit and no fit points, flagged
closed only where its first and last control points are the same point exactly, rational where a weight is other than one, and planar with the extrusion (0, 0, 1).
The arch above is a SPLINE of degree 3 with its 8 knots and 4 control points, and the extents of the drawing are its box, 300 by 112.5.
`SvgWriter.Add(spline, chordTolerance, colour)` draws a spline that is not rational and of degree 3 or less as the Bézier curves it is made
of, cut at its knots, a C command for each piece of a cubic (the arch is `M 0 0 C 100 150 200 150 300 0`), a Q for each of a quadratic and
an L for each of a spline of degree 1; SVG has no curve for the rest, the circle among them, which are drawn through the corners `Flatten`
cuts at within the tolerance. `Wkt.Write(spline, chordTolerance)` writes the `LINESTRING` of those corners, every one kept and the ends the
first and last control points exactly: the circle of radius 100 within 0.01 is 256 chords. See [Common](../common.md) for the writers. The
viewer (`tools/GeometryHelper.Viewer`) draws a spline by a cut of its own at its knots, each piece halved until it lies within the chord
tolerance of the scene (a tenth of its point tolerance) and turns no more than 2 degrees, so the arch is 1 722 chords, and a cubic of
40 000 control points is drawn in 0.8 s; it snaps to the two
ends of a spline and to the nearest point of it, picks it, gives its degree, control points, rational or not, domain, closedness and length
among its properties, and reads a SPLINE of a DXF file back by its control points, knots and weights to the bit; one given by its fit points
alone, and a periodic one, are left out with a warning. GeometryHelper.CadConvert converts AutoCAD's `Spline` to and from [`GeoSpline3`](xref:GeometryHelper.Geometry.GeoSpline3)
through its `NurbsData`, and to a `GeoSpline2` as it is seen in plan (`ToGeoSpline2`, or `TryToGeoSpline2` for false in place of a throw),
which is exact, since a spline seen square onto a plane is the spline of its control points seen so.

## Against the other shapes

A spline answers the five queries about every shape in the plane, from either side, as a ray and a line without ends do:
`DistanceTo`, `CollidesWith`, `GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`, each with a tolerance and without, about a
segment, a ray, a line without ends, a polyline, a polygon, a rectangle, a triangle, a face, an arc, a circle, an ellipse, an edge, a
curved chain, a curved loop and another spline. The shape's member hands the question to the spline's, so `polygon.DistanceTo(spline)` is
`spline.DistanceTo(polygon)` to the bit, the crossings are the same points, and the shortest segment is the same turned round.

```csharp
// arch: the cubic above, the parabola y = x (300 - x) / 200;  ring: the circle spline of radius 50 about the origin.
arch.GetIntersections(new GeoXLine2(new GeoPoint2(-50, 100), GeoVector2.XAxis));          // (100, 100), (200, 100)
arch.DistanceTo(new GeoXLine2(new GeoPoint2(-50, 113), GeoVector2.XAxis));                 // 0.5, over the top
ring.GetIntersections(new GeoLine2(new GeoPoint2(-100, 30), new GeoPoint2(100, 30)));      // (40, 30), (-40, 30): in order along the ring
var square = new GeoPolygon2(new GeoPoint2(-100, -100), new GeoPoint2(100, -100), new GeoPoint2(100, 100), new GeoPoint2(-100, 100));
ring.DistanceTo(square);                                                                   // 0: the ring is inside the square
ring.GetShortestLineTo(square).Length;                                                     // 50: the gap out to its outline
arch.DistanceTo(arch + new GeoVector2(0, 20));                                             // 11.3359: the ends of the copy are nearer than 20
```

### The rules are the other curves'

A closed shape (a polygon, a rectangle, a triangle, a face, a circle, an ellipse, a curved loop)
is a region: a spline inside it, or in the material of a face, is nought from it, and one in a hole is the gap away from the hole's rim.
The spline itself is a curve with no inside, so it has no `Contains` and no `SignedDistanceTo`. The distance is nought only where the two
truly meet, a crossing or a touch found to rounding, and otherwise the gap, measured exactly: it never reads the tolerance, and a spline
a half of a thousandth from a line is 0.0005 from it. `CollidesWith` is the distance being no more than the point tolerance, exactly, so
the two never disagree. The crossings are those of the boundary, in order along the spline from its start (along the one asked, for two
splines, the same points bit for bit from either), and so is the shortest segment, which runs from the spline to the outline and has no
length where they cross: the ring inside the square is nought from it and 50 from its outline. The place given is the point of the spline,
which is the point of the shape too where they cross.

### A place is a meeting or a least of the distance

A crossing and a touch are found where the two meet, and a near touch and an end
that comes near where the distance of the spline from the shape is least within the point tolerance: the line 0.0005 over the top of
the ring touches it at (0, 50) at the default tolerance and not at a tolerance of a ten-thousandth. A stationary point of the distance
that is a greatest is not a place, nor is a point on a slope: a line 1E-6 inside the top of the ellipse of radii 100 and 50 meets its rim
at x = -0.02 and 0.02, and the middle between them, where the distance is greatest, is not given; the 2 000-point fit of an arc of radius
1 000 has nine places against the line that is tangent at its top where each span's start on either side of the touch was one, and one
place now. Places no further apart than the point tolerance, one from the next, are one place, as are two near touches next to each other
along the spline within ten times that with no point between them farther from the shape than at either by more than four snaps (the
snap is 1E-13 of the size of the numbers: where a curve touches another the rounding of the numbers leaves the distance as near nought
as the digits tell all along a stretch, and each near touch it finds there is the one). Two meetings are one place only when they are
within the tolerance of each other, whatever lies between them, as the exact curves have it: two crossings 6E-4 apart are one place at a
thousandth and two at a ten-thousandth, and a line that cuts a circle of radius 30 a million from the origin in a chord of 0.009 meets it
twice, for the spline as for the [`GeoCircle2`](xref:GeometryHelper.Geometry.GeoCircle2). A line tangent to a circle of radius 1 000 touches it once, however the line is cut into
segments (a polyline of 10 000 segments, 100 000 out): a million out the numbers are 1.2E-10 apart and the two are within a snap of each
other for 0.014 either side of the touch, where the places are the crossings of the rounding, up to five, all within that hundredth of a
millimetre. The snap is 1E-13 of the size of the numbers, which for a line, a segment or a polyline is the sum of its coordinates' sizes:
2E-7 for one a million out along both axes. A meeting that is one only to the snap, no crossing and further from the shape than the
numbers can be evaluated to (sixteen units in their last place), which lies within ten times the tolerance of a meeting to the digits and
is no least of the distance, is left out: a quadratic sitting on the circle spline of radius 100 a million from the origin is 2.3E-8 off it
0.002 either side of the touch, and those two points, on the slope of the distance, were given besides the touch, where the `GeoCircle2`
gives one place; it is given one. Ten million out the two points are 4E-9 off, within what the numbers are evaluated to, and are still
given in place of the touch, a known limit. Of the edges of a polygon, chain or loop, each is asked in turn and a place that another edge is nearer to is
not one of the shape. A closed spline has no end to come near: the line tangent to the circle spline of radius 100 at 0.003 of a radian
touches it once, as it does the `GeoCircle2`, where the seam at (100, 0), 4.5E-4 off the line, was given too as an end. A least
that curves up too little to rise a snap a thousandth of the size of the spline along it is one only where the distance falls neither
way, as in space: the cubic y = x³ / 100 turned by 0.7 of a radian a hundred thousand out was given its inflection, 0.0001 under the
line y = 0.0001, besides the crossing at x = 0.21544, and is given the crossing alone.

### Two crossings a hair apart

Two curves that cross twice a hair apart run side by side between the crossings, and the nearest points of
two straight parts there are the middle, a greatest of the distance, with no slope for Newton's method to go by; a crossing at a few
millionths of a radian is as near as the digits tell for a long way either side of it. They are found by the offset of the one from the
other along the normal, which changes sign where they cross and nowhere else: at a greatest the distance falls to nought either way
along the curve, the offset is tried where its quadratic model reaches nought and nearer and further, and a change of sign is bisected to
the last digit; and a part's ends are tried the same way. A line 40 or 4 000 long that cuts the circle of radius 1 000 in a chord of
0.004, 0.02, 0.1 or 1 meets the circle spline twice, at x = +-chord / 2, from either side, as it does the `GeoCircle2` (it met it once, or
never, before), and the distance is nought.

### Where the spline lies along the shape

To rounding, along a segment, a ray, a line, a circle or an arc, a degree-one span along a
segment, a spline cut at a knot beside the whole, the spline of an arc beside the arc: it is nought from it and collides with it but
crosses it nowhere, and no point is given along the stretch: the corner (0, 0), (100, 0), (100, 50) as a spline meets the segment from
(-50, 0) to (150, 0) at (100, 0), where it leaves it, and its first span is not a crossing, as a polyline's is not. A spline and its own
copy cross nowhere and are nought apart. Against an ellipse or another spline the same is so where each stays within the point
tolerance of the other, and the first keeps its distance level, changing by no more than half of it, for a quarter of the size of the
smaller of the two, the size being how far its control points reach from the first, which is the same however the pair is turned (the
span along the axes was, and 14 of 60 copies of a spline moved a few ten-thousandths gave a place more or fewer turned by 0.7 of a radian).
Past an end of the other, within the tolerance of it, the first is along it as far as the tolerance reaches, as an arc is along an arc of
its circle, so a copy that overhangs the other's end and the other's end itself answer alike whichever is asked first, which turning the
pair can change; a point square from the other's end is a least of the distance only where the distance falls on neither way, the other's
inside included; and a crossing found inside the stretch stays a crossing however a meeting found beside it is read. Of those 60 copies
none gives a place more or fewer turned, where 5 did, and of 200 more 4, where 18 did. Two circles of radius 1 000 and 1 000.0005 are 0.0005 apart all round, collide, give no place, and are 0.0005 apart for
`DistanceTo` (not nought: it never reads the tolerance), where at a tolerance of 1E-4 they do not collide and are as far apart. Both are
looked at: a stretch that one curve leaves by more than the tolerance, a dart of a millimetre of a polyline 2E-4 off a line that goes out a
unit and back, is cut out of the stretch, which is cut in two there, and the crossings at the dart's two edges, (506.0002, 0) and
(507.9998, 0), are given from either side, where they were lost. A crossing inside a stretch is given too, where the offset of the one
from the other changes sign and goes more than a hundredth of the tolerance, 1E-5, to each side: a line and one that goes from 2E-4 under it
to 2E-4 over it cross at (500, 0) in 1 000. A change of sign by less is the rounding of curves that are the same to a hundred-millionth: a
lobe of the offset between two changes, its samples off nought on one side (a sample that is nought exactly belongs to none, and the change
lies across it), that goes no further is taken with its neighbours, and between two lobes that go further, one to either side, the curves
cross once, at the change nearest the middle of the small lobes between them, or the next nearest where that one is a jump of the offset at
a corner of the other curve, which bisects to no meeting, or a crossing so shallow that the two run along each other there to the snap,
and failing every change, where Newton's method on the two curves from either side of it settles on a meeting to the hundredth of the
snap it works to, the one nearer the change (a point it stalls on short of that is no crossing, however near, and one it settles on is one
however long the two run along each other round it: two segments a million out that cross at a slope of 2.5E-7 cross once, at their
crossing, 0.00006 off, where they were given no place, or one 0.35 off); between two that go
further to the same side, not at all. So between the first and the last lobe that go further, the crossings given are odd in number where
those two lie to either side and even where not, wherever a change between two such lobes is found to meet; a small lobe before the first
or after the last gives none. A cubic whose offset from a line goes from 1.1E-4 under it to 4.4E-4 over it, crossing it at 20, 40 and 60
with lobes of 7E-6 between, crosses it once in the plane, at 40 at the origin, whichever is asked and however turned, where it was given
three; a million out the crossing at 40 runs along the line within the snap for 0.1 either side (1E-7, 1E-13 of the largest coordinate of the
control points of the two splines; a [`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2) reads the sum of its coordinates, 2E-7 there), and the one at 60 is given; laid in
a tilted plane of space the pair lies in no plane the digits can tell, and is measured in space, where no lobes are read, at all three. A
zigzag of degree one and its copy moved 2.6E-5 along it cross at each corner where the edges either side are 1.8E-5 off each other, and
once across two shallow edges 8.7E-7 off each other, where the exact polylines cross three times. Of 400 random polylines that never turn
back and their copies moved 2.6E-5, 216 are given the exact count of crossings and 314 its parity, where 201 and 270 were, and moved 5E-4,
388 and 395 of 400 more, where 266 and 299 were, and none answers otherwise asked the other way or turned. A polyline that turns back
on itself pays for the rule where its first or last lobe goes no further than the hundredth: the first edge of one 26.8 long lies 7.07E-6
off its copy, and the corner after it, which the exact polylines cross at, is no crossing; of 400 such polylines at the origin and their
copies moved 2.6E-5, 46 are given the exact count and 285 its parity, where 51 and 293 were, and none of them answers otherwise asked the
other way or turned, where 36 did. Where the two lie on each other exactly, to the digits, between two lobes that go further than the
hundredth, the place is fixed by that overlap and not by where a bisection or Newton's method settles in its rounding: a polyline over
a line by 0.0003 to 10, on it from 10 to 50 and under it after, crosses it once, at 10, the end of the overlap next to the earlier lobe
along the spline the work is done from, however the pair is turned (it was given at 34.95 turned by 0.3 of a radian, and the other end
of the overlap could be given as well). That end is the crossing where the curve comes on the other from the earlier lobe's side and
leaves it to the later lobe's; coming on from the side it leaves to, it changes side before the overlap or after it, and crosses there:
over the line to 10, 0.000005 under it to 30 and on it to 50, it crosses at 10. Between two lobes to one side it is no crossing, and
nothing of the overlap is given there. A meeting inside a stretch along at more than a degree from parallel, the default angle
tolerance, with another branch of the other curve than the one the stretch runs along, is given, inside an overlap or at its ends as
well: a cubic that crosses itself at 74 degrees and its copy moved 0.0005 lie along each other all the way, and the copy crosses the
loop at the loop's own crossing, twice, 0.0005 apart, which was missed asked either way, turned and in a tilted plane of space with the
copy in that plane (lifted out of it the copy is measured in space and still misses it); a polyline that lies on a line from 20 to 50
and comes back square across it at 35 crosses it there. The two crossings at the loop are one place, and the one first along the two
curves together stands for it, or where that is the same to a billionth the one nearer the point half way between the curves' starts,
the same asked either way and turned. A copy of a polyline still crosses it at a corner only as its lobes have it, and a branch of the
other curve that comes back within about three sixty-fourths of a stretch along of where it left is not told from the branch the
stretch runs along. A
sine of 21 fit points and a copy moved 0.0005 up are along each other and cross nowhere, where 15 101 places came before, after 22 seconds,
and none comes now in a few milliseconds. The fit through 37 points of a quarter circle against [`GeoArc2`](xref:GeometryHelper.Geometry.GeoArc2) of the same arc, which is
solved exactly, gives its true crossings, 72 of them, the fit being the arc to rounding only in the places it was made to pass through,
and off it a little between, to one side then the other; against the circle spline of the same arc, which is a spline and is cut like one, it is a stretch along, within the
tolerance and level all the way, and gives only the crossings that go more than 1E-5 to each side: ten,
all in the first and last tenth of the fit. A touch is not level, its distance going from nought to the tolerance in a few units and past
it, nor is a crossing at any angle a stretch of level distance of that length, so a tangent contact of curves of any curvature is a place.

### How each is solved

Against a segment, a ray or a line without ends, the signed offset of each Bézier span from the line is a
polynomial in Bernstein form, and the places the span crosses it are its roots, all of them found by cutting in halves wherever the
coefficients change sign, to rounding; its critical points give the touches and the nearest approach, and the ends of the segment
and of the spline are measured too. A ray or a line without ends is cut to the stretch the spline's hull can reach. Against a circle,
an arc or an ellipse the quadratic form of the curve laid into the span is a polynomial of twice the degree, solved the same way; an
ellipse is also a rational spline of four spans, so its nearest approach is found as for a spline. A span that is itself an arc of a
circle, as every span of `FromArc` and `FromCircle` is (a rational quadratic whose two outer control points stand as far from the middle
one, to a millionth of a millionth of the size of the numbers, its middle weight the cosine of half its turn), meets a circle, an arc, or
such a span of another spline in closed form, as the two circles meet by the rule of `GeoCircle2`: at their crossings, or their touch,
where both arcs hold it, and where the two circles are one circle to that rule, within the tolerance of each other all round, along
each other over what both arcs cover and the tolerance past it, which gives no place, the ends of the arcs in it included. For the
distance the two are measured as they are: the crossings where they truly cross, and otherwise the points on the line through the
centres. A span's circle is read from its control points, whose rounding, taken at the size of the radius since an arc is made from
its centre, reaches the centre times four times the square of the radius over the chord, and the closed form is taken only where that
leaves the centre within a hundredth of the tolerance of the true one for the places, and within a snap for the distance: a quarter
turn of radius 100 000 is read to 1E-10, while a flat arc of radius 1 000 000 and chord 300, read to no better than 2.3E-4, or of radius
10 000 000 and chord 1 000 a hundred thousand out, off by 0.6, is cut as any other span, where the circles read so gave one to four
places the arcs do not have. A flat arc so cut can still answer otherwise than the arcs against an arc of nearly its circle, as it
did. Two such arcs that are one circle to the rule however far the reading may be off, their centres and radii within the tolerance of
each other with the doubt on each counted twice, lie along each other as the arcs do: the spline of an arc of radius 849 821 and the arc
of that circle moved 0.00011 and 0.0002 smaller meet nowhere, where the ends of their overlap were two places. Where the doubt is larger
than that leaves room for, the spline of a flat arc is not one circle to the digits of its control points, which hold a circle off the
arc's by up to 0.7 for an arc of radius 69 000 000 and chord 15 000, and it still answers otherwise than the arcs, a known limit: of 1 200
random pairs of arcs of radius 100 to 100 000 000 a thousand from the origin moved and grown less than the tolerance, 307 do, where 517
did, and with their centres within 100 000 of it, 384, where 603 did, every one of them a spline whose circle cannot be read to the
tolerance; the spline of radius 1 000 000 and chord 300 is given two places against the arc of its circle 0.0002 larger, which the arcs
do not have.
Two circle splines nearly about one centre are as near as the digits tell for a long way round, and cut into parts they paired
every part of the one with hundreds of the other's: of radius 100 000 and 100 000.0005 with their centres 0.01 apart they ran through
two million pairs in 16 s and gave no crossing, and were 4.8E-5 apart though they cross; they cross now at (-4 999.995, +-99 874.922), as
the circles do, and are nought apart, each in a third of a millisecond, and so at radius 10 000 and 1 000. Of 200 circle splines moved a
few ten-thousandths to a hundredth, 142 answered otherwise than the circles against the circle they were moved from and 128 against its
spline, and of 300 such random pairs none do. A join of the spans within a snap of a very shallow crossing can still be given as a
place of its own, a known limit: the circle splines of radius 100 000 about the origin and of radius √(0.003² + 100 000²) about
(0.003, 0) cross at (0, 100 000) and (0, -100 000), joins of the first one's spans, and give four places where the circles give two.
Two circles of radius 1 000 and 1 000.00002 with their centres
1E-4 apart are one circle at the default tolerance, as the `GeoCircle2` reads them, and cross at (-200, +-979.8), at an angle of 1E-7, at a
tolerance of a hundred-thousandth. Against another spline both are cut
in halves while their hulls are near enough to matter, until each part is straight to a thousandth and the two cannot run the same way,
or to a billionth where two parts could cross twice, and Newton's method on the two curves, then on the distance along the first,
brings the nearest pair to rounding. For the nearest approach the parts are taken nearest bound first, whichever pieces they are of, so
that the cutting goes where the least can be, and it ends when the nearest bound left is no nearer than the best found less a hair: the
hair is a billionth of the size of the shapes (0.001 at a size of a million millimetres) and never more than a thousandth of the distance
itself, so that a near miss of 0.0009 at a size of a million is not read as 0.01 because a nearer pair was left uncut. The distance to a
segment, a ray, a line, a circle or an arc is exact to rounding; to an ellipse or a spline it is that of a real pair of points, so no
nearer than the truth, and within the hair of it when the cutting ends short of its budget, and to rounding wherever Newton's method
settles, which is wherever the curves cross or the nearest points are not parallel. Two curves that are side by side at a distance that
is the same all along, two circles 0.0005 apart, can be cut to the hair only by cutting every part, which is more than a budget of 40 000
pairs: when it runs out, the curve is first looked at at 128 points and the least of them brought to the real curves (after 2 000 pairs),
so that the best found is near the truth, and the answer is that best. What is promised then is that the answer is the distance of a real
pair of points and the truth lies between it and the nearest bound left, which one Debug line gives with it; the answer is usually far
nearer the truth than the bound is. So the circles of radius 100 000 and 100 000.0005, the second with its centre moved 2E-4 toward 45
degrees, were 0.00029999998 apart when cut, where 0.0003 is exact, with 0.000296 the bound left, in 0.15 s, and the depth-first cutting gave
0.000518, at the wrong place; met as circles they are 0.00029999998 apart in 0.3 ms. Short of the budget a crossing is never passed over for a nearer approach found first, since a part that
could hold a meeting is bounded at nought and comes out before any that cannot, so where the curves meet the distance is nought, exactly,
and they collide; past it a meeting not yet found is not in the answer. The places are cut to a budget of two million pairs, and a stretch
that is not level, or hugs at a distance that makes every part of the one a near match for hundreds of the other, could reach it, and one
Debug line is logged when one does. Two parts that cannot meet are measured once both are no larger than the tolerance, straight or not,
as in space: round a cusp a part is straight only when it is a millionth of the curve, and a tight spline of degree four and its copy
moved 0.00074, where the stretch along them ends short of the cusp, would be cut into two million pairs there, 26 s, and take 0.3 s. Two splines a hair apart and side by side for 6 000 take 0.15 s for
`DistanceTo`. A polyline, polygon, rectangle, triangle, face, chain or loop is its edges, each asked in turn, and an edge farther than the
best found is passed over; the few places that are asked whether another edge is nearer, and the neighbours that are asked whether they
are one near touch, are looked up in a grid of the edges' boxes, so that a million edges across a cubic take 2.2 s and 80 000
across it 0.3 s.

Checked against oracles that share no code with the library: on every run 20 random splines of degrees one to five, with repeated and
thin knots and weights for one in two, against four shapes of each of the 14 kinds, 1 120 pairs, each held to the oracle's places (the
crossings and the near touches within the tolerance, in count and in position); 165 pairs each of a spline with a line, a circle and
an ellipse that touches its tangent, from a ten-thousandth to three thousandths either side; 7 splines of each of 7 nasty kinds (a
million from the origin, control points repeated, all on one line, spans a millionth wide, degree up to ten, weights a thousandfold apart,
a cusp) against 7 kinds of shape; 12 pairs of two splines; every query from both sides, at three tolerances, for 2 520 pairs, to the
bit; and 45 hand cases, and 39 more, at a hundred thousand and a million from the origin, with splines of up to eighty thousand spans and
curves that cross twice a hair apart. The worst distance error was 1.6E-15 of the size of the spline, and 6.4E-9 of it a million from the
origin. A run of four times the draws, which the environment variable SPLINE_FUZZ_DRAWS asks for, once found a distance 5.8E-6 too great,
for a spline all on one line against an ellipse of size 700, where the hair was a hundred-millionth of the size; it is a billionth now,
the run is clean, and that draw is a test of its own.

### Not offered, and why

`SignedDistanceTo`, `Contains` and `GetClosestEdge` (a spline has neither an inside nor edges), and a
`TryIntersectWith` that gives one point (a spline can meet a shape many times, so it gives the places as an array, as an arc does). A point
asks the spline with `IsPointOn` and `DistanceTo`, as before. Shapes in space (`GeoSpline3` against a plane, a box, a body) follow with the
spline in space.

Not in this release, and why: an `Offset` (the offset of a spline is not a spline; it would be an approximation to a tolerance),
splines as the edges of a [`GeoPolylineArc2`](xref:GeometryHelper.Geometry.GeoPolylineArc2) chain, periodic knot vectors, and surfaces.

## See also

- [Circles, ellipses, rays and infinite lines](curves.md).
- [Ellipses and splines in space](../solid/curves.md).
