# Moving and asking

## Moving geometry

`GeoTransform2` is a 3x3 homogeneous matrix covering translation, rotation, scaling and mirroring, the
counterpart of `GeoTransform3` in space. It is applied on the left, so `a.Multiply(b)` means "apply b,
then a", and instances are immutable.

```csharp
GeoTransform2 move   = GeoTransform2.Translation(new GeoVector2(10, -3));
GeoTransform2 turn   = GeoTransform2.Rotation(center, Math.PI / 2);   // or about the origin
GeoTransform2 shrink = GeoTransform2.Scaling(center, 0.5);            // or per axis: Scaling(2, 3)
GeoTransform2 flip   = GeoTransform2.Mirror(axis);                    // across the line a segment carries
GeoTransform2 place  = GeoTransform2.FromFrame(origin, xAxis);        // put a drawing where it belongs

GeoPolygon2 moved = polygon.TransformBy(move.Multiply(turn));         // turn first, then move
GeoPoint2 back = place.Inverse().Transform(point);                    // read a placed drawing back
```

Every shape in the plane carries `TransformBy`, and `Translate` and `RotateBy` for the two common cases:
points, vectors, segments, chains, polygons, faces with their holes, circles, rectangles, arcs, and the
chains that carry arcs.

A bulge measures an arc against its own chord, so moving, turning and scaling evenly leave it untouched;
mirroring changes its sign, because the arc then leans the other way against the same chord.

A curved loop is copied, moved, turned and transformed edge for edge, every arc and side however small, by `Clone`,
`Translate`, `RotateBy` and `TransformBy`: at a millionth, the rectangle 100 by 50 filleted at 0.0005 keeps its eight
edges, four of them arcs 0.0007 across, and scaled to a tenth keeps arcs 0.00007 across, where it was put back
together within the global tolerance of a thousandth and came back its four corners. A segment of any length carries
the line `Mirror` reflects across: the segment from (0, 0) to (0.000007, 0.000007) mirrors (1, 0) to (0, 1), where it
had no direction within the global vector tolerance of 1E-5 and was refused.

A curved chain is copied, moved and turned edge for edge as well, by `Clone`, `Translate` and `RotateBy`: at a
millionth, the chain (0, 0), (10, 0), (10, 10) filleted at 0.0005 keeps its arc 0.0007 across, where it was put back
together within the global tolerance and came back turning at a corner. A straight polygon or chain is moved and turned
side for side by `Translate` and `RotateBy`: the triangle (0, 0), (10, 0), (10, 10) with every corner cut by 0.0003 at a
millionth keeps its six corners, where it came back three, and the chain through the same corners keeps both ends of its
cut. `TransformBy` of a polygon, a chain or a face, and `GeoTransform2.Transform` of them, keep them side for side too,
whatever the transformation: the cut triangle turned, moved or mirrored keeps its six corners, and the square 10 by 10
scaled by 1E-5 is the square 0.0001 across, where it was refused as having fewer than three corners; only a corner the
transformation lands on the very same point as the one before it goes. `Reverse` of a polygon or a chain turns it round
corner for corner: the cut triangle reverses to its six corners, where it came back three. `ToPolyline` of a polygon
writes it out side for side, the cut triangle a chain of seven vertices, where it came back four, and `ToPolygon` of a
chain closes it corner for corner: the chain round the cut triangle is the polygon of its six corners, where it came back
three. A last vertex on the first, or off it by no more than the rounding of its coordinates, 1E-12 of the largest of
them and of one at the least, is the chain written closed, and goes: the chain round the square 10 across from
(100, 100) ending 1E-12 off its start is that square, where one ending 0.0001 off keeps the side back to its start.
A chain, a polygon or a face goes into a frame in space and comes out of one side for side as well, by `ToPolyline3`,
`ToPolygon3`, `ToFace3` and the `ProjectTo…2` of space: a chain with a step of 0.0004 keeps the step, and only a side
seen end-on, along the Z axis of the frame, goes ([Between the plane and space](../solid/frames.md#between-the-plane-and-space)).

**What a transformation cannot do.** A circle stays a circle only when every direction is stretched by the
same amount: when the two axes come out so nearly equally long and square to each other that the circle moved lands
within the global point tolerance of where the transformation puts each of its points, as a ball in space is read. A turn
whose numbers are rounded to six places or to a float on their own keeps the two axes alike to the last digit and moves
a circle of any size; a stretch of 3E-6 along X moves the circle of radius 100 at the scale it is drawn, 0.0003 off, and
is refused scaled by 1 000, where it would be 0.3 off. Under a scaling that differs between the axes, or a shear, it would be an ellipse, and the attempt is
refused rather than answered with an averaged radius; the skew that sends X to (1, 0) and Y to (0.6, 0.8) keeps
both lengths, and a circle of radius 100 is refused under it, where it came through at radius 100. Transform `GeoEllipse2.FromCircle(circle)` instead:
a circle of radius 100 scaled by `Scaling(3, 1)` is the ellipse 300 by 100. An ellipse itself takes every
transformation that does not flatten the plane, and stays an ellipse. A rectangle is
refused the same way when it would come out a parallelogram — transform `rectangle.ToPolygon()` when that
is what you want. Its axes are read as a circle as large as its half diagonal reads them: the rectangle 100 by 50 is
moved under a shear of 5E-6 at the scale it is drawn, its corners 0.00018 off, and refused scaled by 1 000, where they
would be 0.18 off.

An arc, and an edge with a bulge, is moved whole by its centre and its radius under a turn, a shift, a mirror or an even
scaling, to the bit as it was, where it lands within the global point tolerance so: a transformation whose axes are one
length and square to each other only to a share of some 1E-9, the rounding of a matrix built from axes, takes a large arc
off, X stretched by a share of 4E-10 and scaled by 1 000 the arc of radius 1E5 sweeping 6 radians 0.04, and such an arc is
read as under a stretch, and a circle, a ball or a rectangle refused. Under any other transformation, a skew or a
stretch, the arc is part of an ellipse, and the arc through where its two ends and its middle land, as an arc in space is
moved, stands for it only where it lands within the global point tolerance of where the transformation puts the rest of
it, everywhere along it, and is refused otherwise: asked at every sixteenth of its sweep only, the arc of radius 87.66 from
0.2 radians sweeping 1.51, under X stretched by 1 + 1.793E-6 and scaled by 84.1, landed 0.0010114 off between two of them,
and was moved. It is refused as the circle under it is: a stretch of 3E-6 along X moves the arc of radius 100 from
0.5 to 2 radians at the scale it is drawn, 0.000017 off, and refuses it scaled by 1 000, where it would be 0.017 off; the
skew that sends Y to (0.000001, 1) moves it 0.0000069 off, the edge of bulge 0.5 from (0, 0) to (100, 0) ending at
(100, 0), so a chain of such edges stays joined; and the skew that sends X to (1, 0) and Y to (0.6, 0.8) refuses it, the
arc of radius 66.09 through its moved ends and middle standing far off the ellipse of radii 126.5 and 63.2 its circle
becomes. A skew that keeps both axes one length is judged as a stretch is: a turn of 0.7529 radians of a stretch of
1 + 4.29E-9 and a shear of 9.28E-5, scaled by 55.57, took the arc of radius 9 069.6 to one 9.16 off, and refuses it.
Each point is read from the moved start, across the arc, so an edge 100 long bowing by 1E-12, of radius 2.5E13, moves
under a turn rounded to six places, where it was refused for want of digits. An arc of a whole turn is the whole circle
and is refused under a skew as the circle is, and so, with an `ArgumentException`, is an arc past a half turn whose ends
the skew brings within the global point tolerance of each other. Under a transformation that carries a circle without
being an exact turn, a turn whose numbers are rounded to six places, such an arc is moved whole as the whole turn is.

**Mirroring turns shapes round.** A polygon that ran counter-clockwise comes back clockwise, its
`SignedArea` changes sign and its `Area` does not. `GetDeterminant` says so in advance: it is the factor
areas are multiplied by, negative when the transformation reverses winding.

`Inverse` judges the determinant against the size of the transformation rather than against zero, so a
drawing scaled down by a thousandth still inverts cleanly while two axes turned nearly onto each other do
not. `TryGetInverse` reports that instead of throwing.

## Asking where something is

Beyond distance and containment, the plane half answers the questions the solid half answers about a
plane, read against a line instead.

```csharp
var cut = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 10));

point.GetSideOf(cut);                          // LineSide.Left, Right or On
Containment2.GetSide(cut, point);              // the same, named the other way round

first.IsCodirectionalTo(second);               // parallel is not enough: the same way along the line
Projection2.ProjectToInfiniteLine(cut, point); // the foot of the perpendicular, beyond the ends if need be
```

`GetSide` reads a segment as the infinite line carrying it, so a point past either end still has a side;
reversing the segment swaps left and right. `ProjectToInfiniteLine` is `ProjectToLine` without the clamp
to the segment, which is what to use when a segment stands for the line it lies on.

Every region reports its measurements as properties, named alike whatever the shape: `Area`, `Length`,
`Centroid`, and `SignedArea` and `IsClockwise` where winding means something.

```csharp
polygon.Area;  polygon.SignedArea;  polygon.IsClockwise;  polygon.Centroid;
face.Area;     face.Length;         face.Centroid;
rectangle.Area;                     circle.Area;  circle.Length;
```

A polygon's centroid weighs its area however small: the L 0.001 by 0.001 with legs 0.00025 wide, of 4.4E-7 square
units, balances at (0.000339, 0.000339), where an area of a millionth or less was taken for none and its corners were
averaged to (0.000417, 0.000417). Only corners on one line, whose area is lost in the rounding of their coordinates,
have none, and are averaged. A face's centroid weighs its holes the same way: the square 0.001 by 0.001 with a hole
0.0004 by 0.0004 in its corner, of 8.4E-7 square units, balances at (0.000538, 0.000538), where the boundary's centroid,
(0.0005, 0.0005), was given.

## Flat shapes that live in space

A plate or a face in a 3D model is flat, so everything here applies to it: lay it out in its own plane
with `PlanarMap`, work on it as an ordinary `GeoPolygon2` or `GeoFace2`, and put the answer back. See
[between the plane and space](../solid/frames.md#between-the-plane-and-space).

## Point chains

`GeometryHelper.Extension` covers the step before a polyline exists: a raw list of points,
usually read out of a drawing and carrying more of them than the geometry needs.

```csharp
using GeometryHelper.Extension;

var traced = new List<GeoPoint2>
{
    new GeoPoint2(0, 0), new GeoPoint2(0.0001, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5),
};

// The second point is a hair away from the first and goes.
List<GeoPoint2> thinned = traced.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001)); // 3 points

// One segment per consecutive pair.
List<GeoLine2> segments = thinned.ToGeoLine2s();                                           // 2 segments
```

`RemoveConsecutiveNearPoints` compares each point against the last one *kept*, not against its original
neighbour, which is what guarantees no two points of the result are coincident within the tolerance. A
huddle collapses onto its first point, and collapsing stops as soon as one point escapes the tolerance
around that anchor, so a long run thins rather than vanishes.

The first point always survives; the last one is not privileged. A final point lying within the tolerance
of the one kept before it is dropped like any other, so re-append it yourself when the endpoint matters.

`ToGeoLine2s` leaves the chain open — nothing joins the last point back to the first, so a ring has to
repeat its first point at the end. It does not filter coincident neighbours either, so run
`RemoveConsecutiveNearPoints` first if zero length segments would be a problem.
