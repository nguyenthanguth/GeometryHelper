# Geometry in space

3D geometry for engineering models: points, vectors, lines, rays, planes, triangles, polylines,
polygons, circles, faces with holes, solids, boxes, local coordinate systems and transformations.

It is the counterpart of [geometry in the plane](plane.md) and follows the same shape: immutable
geometry types, operations living in static classes under `Core`, and every operation mirrored as an
instance method on the type it applies to. Both are measured with the same [shared types](common.md).

## Structure

| Namespace | Contents |
|---|---|
| `GeometryHelper.Geometry` | `GeoPoint3`, `GeoVector3`, `GeoLine3`, `GeoRay3`, `GeoPlane3`, `GeoTriangle3`, `GeoPolyline3`, `GeoPolygon3`, `GeoCircle3`, `GeoFace3`, `GeoAabb3`, `GeoObb3`, `GeoSolid3`, `GeoEdge3`, `GeoPolylineArc3`, `GeoPolygonArc3`, `GeoCoordinateSystem3`, `GeoTransform3` |
| `GeometryHelper.Core` | `Boolean3`, `Collision3`, `Containment3`, `Distance3`, `Intersection3`, `Lengthen3`, `Merge3`, `Offset3`, `Parallel3`, `Parametrization3`, `Projection3`, `Splition3`, `PlanarMap` |
| `GeometryHelper.Spatial` | `GeoBvh3` |
| `GeometryHelper.Extension` | `EnumerableExtension` |
| `GeometryHelper` | `Tolerance`, `Angle`, `OffsetOptions`, `GeometryHelperLog` |
| `GeometryHelper.Enums` | `PointLocation`, `PlaneSide`, `LineEnd`, `LineExtension`, `OffsetJoin` |

The last two are the [shared types](common.md), which the plane half uses as well, so a program
working in both dimensions sees one `Tolerance` and one `Angle` rather than two of each.

## Quick Start

```csharp
using GeometryHelper;
using GeometryHelper.Geometry;

var a = new GeoPoint3(0, 0, 0);
var b = new GeoPoint3(3, 4, 0);

double distance = a.DistanceTo(b);              // 5
GeoVector3 direction = a.GetVectorTo(b);        // [3, 4, 0]

var plane = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);
GeoPoint3 flat = plane.Project(new GeoPoint3(2, 3, 7));      // (2, 3, 0)
double signed = plane.SignedDistanceTo(new GeoPoint3(2, 3, 7)); // 7

var box = new GeoObb3(GeoPoint3.Origin, 10, 20, 30);
double volume = box.Volume;                     // 6000
```

## Geometric Types

The shapes split into three families, and which family a shape belongs to decides what you can ask of it:

| Family | Types | Encloses an area | Encloses a volume |
|---|---|---|---|
| Curve | `GeoLine3`, `GeoRay3`, `GeoPolyline3` | no | no |
| Planar region | `GeoTriangle3`, `GeoPolygon3`, `GeoCircle3`, `GeoFace3` | yes | no |
| Volume | `GeoObb3`, `GeoAabb3`, `GeoSolid3` | yes | yes |

Only regions and volumes offer `Contains`. Every shape offers `Locate`, and a curve can only ever
answer `OnSide` or `OutSide`, because a curve has no interior for a point to be inside of.

A `GeoPolyline3` is always an open chain — it has no `IsClosed` flag and never joins its last vertex back
to its first. Geometry meant to enclose something is a `GeoPolygon3`, and `polyline.ToPolygon()` converts
between them. A chain of vertices tracing a square still holds only the points on its path:

```csharp
var traced = new GeoPolyline3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
    new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(0, 0, 0));

var middle = new GeoPoint3(5, 5, 0);

traced.IsPointOn(middle);              // false — a curve has no interior
traced.DistanceTo(middle);             // 5    — measured to the path
traced.ToPolygon().Contains(middle);   // true — now it is a region
traced.ToPolygon().DistanceTo(middle); // 0
```

A planar region is flat, so a point counts as inside it only when it lies on the carrier plane as well as
within the boundary. A point hovering above the middle of a polygon is outside it:

```csharp
var square = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
    new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

square.Locate(new GeoPoint3(5, 5, 0));  // Inside
square.Locate(new GeoPoint3(5, 0, 0));  // OnSide
square.Locate(new GeoPoint3(5, 5, 3));  // OutSide — off the plane
```

### Flatness is enforced

A polygon that is not flat has no normal, no area and no interior, so `GeoPolygon3` refuses one at
construction rather than letting every property on it become quietly meaningless. Geometry that wanders
out of a plane is a `GeoPolyline3`.

```csharp
// Throws ArgumentException: these four vertices do not share a plane.
new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
    new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 5));
```

How flat is flat enough is `Tolerance.EqualPlanar`, which is separate from `EqualPoint` because
coplanarity is measured far from the reference point: a polygon several metres across turns a hundredth
of a degree of tilt into a deviation of nearly a millimetre.

Self-intersection is a different matter and is *not* checked on the way in, because the check costs more
than building the polygon does. `polygon.IsSimple()` runs it when you want it: no edge crossing or
touching another except where neighbours share their vertex. A polygon that is not simple is still read
consistently, under the even-odd rule, but its `Area` no longer means what it says. One whose lobes
cancel exactly, a symmetric bow tie, is refused at construction all the same — with no net area it has no
normal, and without a normal there is no plane to be flat in.

### Two kinds of box

`GeoAabb3` is an axis-aligned bound — the cheap test that comes before the expensive one.
`GeoObb3` is an oriented shape that carries its own axes and describes a beam running at an angle
tightly, where an axis-aligned box would only bound it loosely.

```csharp
var bounds = GeoAabb3.FromPoints(new[]
{
    new GeoPoint3(1, 5, -2),
    new GeoPoint3(-3, 0, 4),
});

bounds.Min;    // (-3, 0, -2)
bounds.Max;    // (1, 5, 4)
bounds.Volume; // 4 * 5 * 6 = 120
```

The axes of a `GeoObb3` are made orthonormal on the way in, so a Y direction that is not quite square to
X is corrected rather than producing a skewed box:

```csharp
var box = new GeoObb3(
    GeoPoint3.Origin, 2, 2, 2,
    GeoVector3.XAxis,
    new GeoVector3(0.5, 1, 0));   // not square to X

box.AxisX.IsPerpendicularTo(box.AxisY);  // true
box.AxisX.CrossProduct(box.AxisY).IsEqualTo(box.AxisZ); // true
```

### Solids and openings

A `GeoSolid3` is the set of faces bounding a body. An opening is a whole solid subtracted from it — a duct
through a slab, a recess in a footing — which is not the same thing as a hole in a `GeoFace3`, which is
flat and belongs to a single face.

```csharp
GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
GeoSolid3 duct = new GeoAabb3(new GeoPoint3(4, 4, 4), new GeoPoint3(6, 6, 6)).ToObb().ToSolid();

GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

pierced.Volume;     // 1000 — the gross body
pierced.NetVolume;  //  992 — with the duct removed
pierced.IsClosed(); // true

pierced.Locate(new GeoPoint3(1, 1, 1)); // Inside
pierced.Locate(new GeoPoint3(5, 5, 5)); // OutSide — inside the duct
```

`Volume` is measured by the divergence theorem, so it does not depend on where the solid sits and is
reported unsigned: faces wound inwards give the same answer as faces wound outwards. It does depend on the
boundary being closed, which is what `IsClosed()` is for.

**Every question takes the openings into account.** The faces of a pierced body run straight across its
openings — a plate's top face is a whole square even where a bolt hole passes through it — so nothing reads
the faces alone as where the material ends. A pin through a bolt hole touches nothing, a ray down the hole
crosses nothing, and a point in the hole is measured to its walls:

```csharp
GeoSolid3 plate = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 20)).ToObb().ToSolid()
    .WithOpenings(new[] { new GeoAabb3(new GeoPoint3(40, 40, -1), new GeoPoint3(60, 60, 21)).ToObb().ToSolid() });
GeoSolid3 pin = new GeoAabb3(new GeoPoint3(45, 45, -50), new GeoPoint3(55, 55, 50)).ToObb().ToSolid();

plate.CollidesWith(pin);                          // false — five clear of every wall
plate.DistanceTo(pin);                            // 5
plate.Locate(new GeoPoint3(50, 50, 20));          // OutSide — on the top face as drawn, but in the hole

plate.TryCutOpenings(out GeoSolid3 material);     // the same material with the holes cut in: 10 faces
plate.TriangulateSurface();                       // the mesh of where the material ends, walls and all
```

A question about touching or crossing cuts only the openings the probe can reach, so a bolt against a plate
with twenty holes costs one cut. A question about distance cuts them all, because the nearest material can sit
on the rim of an opening the probe never comes near. **A body asked many questions is cut once** with
`TryCutOpenings` and the result asked instead — the same answers, the cutting paid once. `Triangulate` still
meshes the faces as they are; `TriangulateSurface` is the mesh to read as the boundary.

## Operations

| Class | Answers |
|---|---|
| `Parallel3` | is this parallel, perpendicular, coplanar with that |
| `Containment3` | where does a point sit relative to a shape |
| `Distance3` | how far apart are two shapes |
| `Projection3` | which point of a shape is closest to a point |
| `Intersection3` | where exactly do two shapes meet |
| `Lengthen3` | extend or trim a segment along its own line |
| `Offset3` | move a segment sideways, grow or shrink a flat region |
| `Collision3` | do two shapes overlap at all |
| `Parametrization3` | which point sits at a position along a curve |
| `Merge3` | put curves that meet end to end back together |
| `Splition3` | cut a curve, a region or a body into pieces |
| `Boolean3` | join two bodies, keep what they share, take one out of the other |

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

### Asking a body about what is around it

A `GeoSolid3` answers the same questions the flat shapes do, and against every shape in the library that a
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

A `GeoCircle3` is not on the list. The distance from a circle in space to a flat face has no closed form,
and the library does not guess: turn it into a chain first, which says in the call how close an answer you
are asking for.

```csharp
slab.DistanceTo(circle.ToPolylineByChordTolerance(0.1));
```

### And every shape can be asked, not only the body

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

A point, a segment, a ray, a triangle, a polygon, a face, a plane and either kind of box all take part.
Every shape in space also moves, which is the translating transformation and nothing more:

```csharp
var by = new GeoVector3(11, -23, 37);

slab.Translate(by);   beside.Translate(by);   GeoPlane3.XY.Translate(by);
```

#### A segment or a ray against a flat shape

A segment, a ray and a flat region have no thickness between them, so there is nothing to overlap in: they
touch exactly where they cross.

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

### How deep inside, not just whether

`DistanceTo` reads a body as solid through, so a point anywhere inside one is nought away and how far in it
sits cannot be got back out of the answer. `SignedDistanceTo` keeps it, for the three shapes that enclose a
volume — `GeoSolid3`, `GeoObb3` and `GeoAabb3` — and by a `GeoPoint3`, which asks the same thing from the
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

The flat shapes in space are not offered one. A `GeoTriangle3`, a `GeoPolygon3` and a `GeoCircle3` enclose no
volume, so a point is inside one only when it is also on its plane: a sign for them would be negative on a
set of no thickness and would read as though it meant more. `GeoPlane3.SignedDistanceTo` answers the question
that does make sense for something flat, which is which side of it a point lies on.

### Parametrization

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

### Splitting

`Splition3` cuts a curve at a position along it or wherever a plane crosses it, and cuts a region or a
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

A cutter does not have to be a plane. Cutting by a **closed body** sorts the pieces into those inside it
and those outside, because a body has no side the way a plane does:

```csharp
var chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

chain.TrySplitBy(solid, out GeoPolyline3[] inside, out GeoPolyline3[] outside);
// inside holds the stretch within the body, outside the two stretches beyond it
```

The same works against a `GeoObb3` or a `GeoAabb3`, which are cheaper because the crossings come from the
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
// upper.Volume == 600, lower.Volume == 400, both IsClosed()
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

Two consequences worth knowing. A cut can leave one half as **several disconnected shells** — a plane
across both arms of a U leaves two bodies above it — and they come back as one `GeoSolid3`, which measures
and answers containment correctly because each shell is closed and wound outwards. And a plane passing
through a **hole** in a face turns that rim into part of the boundary, so the piece on each side has no
hole where the subject had one.

A plate can also be cut **along a line marked on it**, or **against a body**:

```csharp
plate.TrySplitBy(cutLine, out GeoPolygon3[] halves);              // cut along a drawn chain
plate.TrySplitBy(solid, out GeoPolygon3[] embedded, out GeoPolygon3[] clear);
```

The second answers which part of a plate is embedded in a body. It works by cutting the plate with the
plane of each face of the body in turn — the surface of the body never leaves those planes, so once that is
done no piece can straddle the boundary — and then joining the pieces back up where they agree. What comes
back covers each side exactly, though it may be in more pieces than strictly necessary.

### Extending and trimming

`Lengthen3` changes how long a segment is while keeping it on its own line, as AutoCAD's LENGTHEN, EXTEND
and TRIM do. A segment has no picked point to say which end is meant, so each method takes a `LineEnd`;
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
`TryTrimTo` is that cut with the end named rather than the piece. A `GeoEdge3` answers both ways too:
straight on if it is a leg, round if it is a bend.

### Offsetting

`Offset3` moves a segment sideways and grows or shrinks a flat region within its own plane.

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

The corner joins are the same three AutoCAD's OFFSETGAPTYPE offers — `Miter`, `Round`, `Chamfer`, set
through `OffsetJoin` or `OffsetOptions`, which is documented with the
[shared types](common.md)
package they come from.

**What comes back.** The offset of a region is exact, not its edges moved one by one, so a polygon does
not always give one polygon back. `Offset` returns the boundary loops of the result: none when it shrinks
past its own width, one per piece when it pinches off, and, where a growing polygon closes a gap around
empty space, that space as a hole wound against its outer loop so that signed areas add up. `GeoFace3`
returns faces with their holes attached. A polyline's parallel curve has the loops a tight turn would
make cut away and comes back in pieces when a turn cuts it clean through.

The planar work happens in a frame built on the region's own plane, through the same corner construction
the plane library uses, so a plate lying at an angle offsets exactly as its plan view would. The loops
themselves are resolved by the library's own winding-number solver, where the plane half hands that
part to Clipper2. The test suite checks the two against each other.

### Combining bodies

`Boolean3` joins two solids, keeps the part they share, or takes one out of the other:

```csharp
first.TryUnion(second, out GeoSolid3 joined);
first.TryIntersect(second, out GeoSolid3 shared);
first.TrySubtract(tool, out GeoSolid3 left);
```

Each reports `false` when the answer is nothing at all — two bodies that never touch share nothing, and a
body swallowed whole leaves nothing behind. That is an outcome rather than a failure, which is why it
comes back as `false` rather than as an exception or an empty body.

The method is the cutting above carried up a dimension. Both bodies are divided by one shared set of
planes — the face planes of each of them together — which leaves cells that are each wholly inside or
wholly outside the other, since the surface of a body never leaves the planes of its own faces. The cells
the operation wants are then glued: a face shared by two kept cells appears twice, once each way round,
and dropping both leaves exactly the outer skin.

Two details are worth knowing. Using one shared plane set for both bodies rather than each against the
other is what makes the gluing work where they meet — cut that way both sides of the interface are the
same plane carved by the same knives, so they come out as the same polygon and cancel. And dividing A by
the planes of B already lays a face along every part of the surface of B that runs through A, which is why
a difference is just the cells of A that fall outside B: the walls of the cavity are already there.

**Flat shapes and boxes.** Two areas in one plane are combined by the plane library and the answer lifted
back, so it is exact; and a box is combined through the body it bounds, which is six flat faces and no
fitting at all.

```csharp
var plate = new GeoPolygon3(
    new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
GeoPolygon3 patch = plate.Translate(new GeoVector3(50, 50, 0));

GeoFace3[] joined = plate.Union(patch);      // GeoFace3, because joining two areas can leave a hole
plate.Intersect(patch);                      // what they both cover
plate.Subtract(patch);                       // what is left of the first
plate.Xor(patch);                            // what they cover between them but do not share

var first = new GeoObb3(GeoPoint3.Origin, 100, 100, 100);
var second = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 100);

first.TryUnion(second, out GeoSolid3 both);   // a GeoSolid3: a boolean of two boxes is hardly ever a box
first.TryExpand(10.0, out GeoObb3 bigger);    // and a margin on every face, keeping the axes
```

Everything comes back as `GeoFace3` because joining two areas can leave a hole in the middle and only a
face can hold one — four bars round a square give one face with one hole. **A shape that does not lie in
the first one's plane is refused**, with an `ArgumentException`: projecting it in would report two plates a
metre apart as overlapping and say nothing about it. `SharesPlaneWith` asks beforehand, on
`GeoPolygon3`, `GeoFace3` and `GeoPolygonArc3` alike.

### Merging

`Merge3` is the other direction. `ConsecutiveLines` and `ConsecutivePolylines` take the pieces in the order
given and only ever join a piece to the one after it. `Join` ignores order and direction and reassembles
whatever chains the set actually forms, which is what a bag of edges out of a model needs:

```csharp
GeoPolyline3[] chains = Merge3.Join(new[]
{
    new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(3, 0, 0)), // reversed middle
    new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(8, 4, 0)), // last
    new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)), // reversed first
});

// One chain, 4 vertices, total length 12.
```

`CoplanarFaces` does the same job for a surface. Cutting a body leaves it more finely divided than it needs
to be, and repeated cutting compounds that, so this puts the surface back into as few faces as describe it:

```csharp
GeoSolid3 tidied = Merge3.CoplanarFaces(subdivided);
// same volume, same surface area, fewer faces
```

Faces are grouped by the oriented plane they lie on, so a face and one facing the other way are never
merged — they are different surfaces that happen to be flat in the same place. Merging a ring of faces
keeps the hole in the middle as a hole. Two faces count as touching only where they share a whole edge, so
a T-junction stops that one join rather than the whole group; merging under-joins rather than guessing.

## Arcs in space

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

### A bar is a chain with a bend at every corner

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

A radius per corner works too, read the way the bulges are read: the entry at an index belongs to the vertex
at that index, and nought leaves that corner square. `TryFilletAt` rounds one named corner. A bend with too
little straight run either side to fit its radius is left square rather than forced, and where two bends want
more of the run between them than it is long, the one taking more of it gives way.

Only a corner between two **straight** legs is rounded. A leg that already curves lies in a plane of its own,
which need not be the plane of the corner, so there is no one plane to do the arithmetic in.

### Closed loops have to lie flat

`GeoPolylineArc3` requires no plane, as `GeoPolyline3` requires none. `GeoPolygonArc3` does require one, and
enforces it at construction exactly as `GeoPolygon3` does: a loop that is not flat encloses nothing and has no
inside. A bar bent out of one plane whose ends happen to meet is a `GeoPolylineArc3`, not a loop.

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

A loop in space cannot report its winding through `IsClockwise`, because it is laid out in a frame that turns
with it. What reversing turns over is the plane the loop names.

### Between the plane and space

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

### One bar against another, and against everything else

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

### Cutting a bar to a schedule, and to the openings

```csharp
bar.SplitAtDistances(new[] { 1000.0, 2500.0 }, out GeoPolylineArc3[] lengths);
bar.TrySplitBy(opening, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside);       // a box
bar.TrySplitBy(everyOpening, out GeoPolylineArc3[] within, out GeoPolylineArc3[] clear);   // an array of them
```

A piece is inside when it is inside **any** cutter, so overlapping openings behave as the one region they
cover: consecutive pieces on the same side are joined into one run, and a gap in the array is passed over
rather than throwing. Which side a piece is on is settled at its **middle** and never at an end, because
every end is on a surface by construction and a surface belongs to neither side.

### Cutting corners in space, and measuring from a loop

A chamfer needs no plane at all — it moves back along one leg and forward along the other, so both points
land on the legs themselves. Only a corner between two **straight** legs is cut, the rule the fillet keeps,
because a leg that curves leaves at a tangent.

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
shortcut, so they are refused. `GetClosestEdge` takes a primitive probe only — a point, a segment, an arc,
a circle or an edge — because the nearest edge of one many-edged shape to another is a *pair* of edges.

### What is exact, and what is sampled

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

## Checking parts against each other

A clash check between parts asks four questions, from cheapest to dearest, and stops at the first that
settles it:

```csharp
foreach (var (a, b) in pairs)
{
    if (!a.GetAabb().CollidesWith(b.GetAabb()))   // boxes apart: nothing to ask
    {
        continue;
    }

    if (!a.CollidesWith(b))                       // no touch at all, openings honoured
    {
        continue;
    }

    GeoSolid3[] clashes = a.Intersect(b);         // one body per region the two share

    if (clashes.Length == 0 && a.TryGetContact(b, out GeoFace3[] bearing))
    {
        // They only touch: bearing is where they lie against each other.
    }

    foreach (GeoSolid3 clash in clashes)
    {
        double overlap = clash.Volume;            // how badly, and clash.Centroid for where
    }
}
```

- **`Intersect` gives one body per clash.** A beam through two separate plates clashes twice, and
  `TryIntersect`, which gives the whole shared region as one body, cannot say where either is. The pieces
  always add up to it. Two regions meeting only along an edge share no volume and are two.
- **Touching is not a clash.** A column standing on a footing shares no volume with it, so `Intersect` is
  empty. `TryGetContact` says where the two lie against each other, face to face — back to back in one
  plane — and a hole in the material under the contact is left out of it. Two bodies meeting only along an
  edge or at a corner have no contact patch; `CollidesWith` is what reports those.
- **`SplitShells`** gives the separate pieces of any body, the same way.
- A part checked against many others is cut once with `TryCutOpenings` first, as above; `CollidesWith`
  between two large meshes builds an index on its own once they are big enough to be worth it.

## Working with large meshes

Every operation above looks at the whole of what it is given, because nothing rules any of it out in
advance. `GeoBvh3` is the index that does: a tree of nested boxes over a triangle mesh, in which a ray that
misses a box misses everything inside it, and a box farther away than the best answer so far cannot hold
anything nearer.

```csharp
var tree = solid.BuildIndex();     // and on GeoFace3; GeoBvh3.FromSolid(solid) is the same tree

tree.DistanceTo(point);            // nearest point on the surface
tree.GetClosestPoint(point);
tree.GetIntersections(ray);        // every crossing of the surface
tree.CollidesWith(otherTree);      // surface contact between two meshes
```

**When it is worth building.** When the *same* body is asked *many* questions. Building costs a sort of the
triangles, so it pays for itself over repeated queries and never on the first one — one distance to one point
is quicker asked of the body. Every geometry type here is immutable, so a mesh never goes stale under its
index: build it once, keep it as long as the body lives. It is called `BuildIndex` and not `GetIndex` because
it does work, and calling it inside the loop it was meant to speed up is slower than not having it.

**Where the library already builds one for you.** Two pairs are expensive enough that they index on their own
and you need do nothing: `Collision3.CollidesWith(solid, solid)` indexes both when the triangle counts
multiplied pass 64 × 64, below which the plain scan wins; and `Distance3.DistanceTo(solid, solid)` indexes
both every time, since two meshes that miss each other have no cheap answer. Everything else takes the body
as it finds it, which is why a loop of your own is where an index of your own pays.

**The index is over triangles and the body is not.** `tree.GetIntersections(ray)` reports one hit per
triangle, so a ray landing on the diagonal that two triangles share is named twice, while
`solid.GetIntersections(ray)` names each place once. Ask the body when you want places; ask the tree when you
want speed and can keep clear of the edges.

The mesh it indexes comes from `GeoSolid3.Triangulate`, which follows the material of every face: a
concave face is traced rather than spanned, and a hole in a face is left open. So a ray fired through the
notch of an L-shaped body, or through a bolt hole in a plate, passes clean through, and the nearest point
on the surface is a point that is really on it. That is what separates it from `GeoFace3.Triangulate`,
which fans the boundary from one vertex and is meant only for the signed sums — area, centroid, volume —
where the part of a fan reaching outside the face cancels against the part overlapping it. Use
`GeoFace3.TriangulateSurface` wherever the triangles stand for material.

A triangle out of either of them reads as a polygon or as a face, which is what the boolean, offset and
splitting families take:

```csharp
foreach (GeoTriangle3 triangle in body.Triangulate())
{
    if (triangle.IsDegenerate())
    {
        continue;                       // three collinear points are not a polygon, so this is refused
    }

    GeoFace3 face = triangle.ToFace3();  // and ToPolygon3() for the outline alone
}
```

`Collision3.CollidesWith(solid, solid)` builds one internally once the meshes are large enough to be worth
it, and falls back to comparing every pair below that — below the threshold, the plain scan wins.

One caveat, shared with any triangle mesh: a ray running exactly along an edge is reported by both
triangles that share it. Anything counting crossings to tell inside from outside must keep clear of edges,
which is why `Containment3` throws its ray again in another direction when a hit lands near one.

## Point chains

`GeometryHelper.Extension` covers the step before a polyline exists: a raw list of points,
usually read out of a model and carrying more of them than the geometry needs.

```csharp
using GeometryHelper.Extension;

var traced = new List<GeoPoint3>
{
    new GeoPoint3(0, 0, 0), new GeoPoint3(0.0001, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(5, 0, 5),
};

// The second point is a hair away from the first and goes.
List<GeoPoint3> thinned = traced.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001)); // 3 points

// One segment per consecutive pair.
List<GeoLine3> segments = thinned.ToGeoLine3s();                                           // 2 segments
```

`RemoveConsecutiveNearPoints` compares each point against the last one *kept*, not against its original
neighbour, which is what guarantees no two points of the result are coincident within the tolerance. A
huddle collapses onto its first point, and collapsing stops as soon as one point escapes the tolerance
around that anchor, so a long run thins rather than vanishes. Distance is measured in all three
dimensions, so two points that share X and Y but differ in Z are not fused.

The first point always survives; the last one is not privileged. A final point lying within the tolerance
of the one kept before it is dropped like any other, so re-append it yourself when the endpoint matters.

`ToGeoLine3s` leaves the chain open — nothing joins the last point back to the first, so a ring has to
repeat its first point at the end. It does not filter coincident neighbours either, so run
`RemoveConsecutiveNearPoints` first if zero length segments would be a problem.

## Tolerance

Nothing in this library compares coordinates with `==`. Every comparison that floating point error can
affect takes a `Tolerance`, and every such method has an overload without one that reads `Tolerance.Global`.

```csharp
var a = GeoPoint3.Origin;
var b = new GeoPoint3(1e-9, 0, 0);

a.IsEqualTo(b);  // true  — within the default tolerance
a.Equals(b);     // false — exact comparison

a.IsEqualTo(new GeoPoint3(0.05, 0, 0), new Tolerance(0.1, 0.1)); // true
```

`Tolerance` carries four thresholds: `EqualPoint` for coincidence, `EqualVector` for direction,
`EqualAngleRad` for parallelism, and `EqualPlanar` for flatness.

Degenerate input is treated as "no answer" rather than being guessed at. A zero-length vector has no
direction, so it is neither parallel nor perpendicular to anything, and `Normalize()` on it throws while
`TryGetNormal` reports the failure:

```csharp
GeoVector3.Zero.IsParallelTo(GeoVector3.XAxis);        // false
GeoVector3.Zero.TryGetNormal(out GeoVector3 unit);     // false
GeoVector3.Zero.Normalize();                           // throws InvalidOperationException
```

## Between the plane and space

A plate, a face, a chain marked on a face: flat geometry lying in a plane in space. `PlanarMap` lays it
out in two dimensions, so that everything [the plane half](plane.md) can do — offsetting, the booleans,
splitting, containment with holes — applies to it, and puts the answer back where it came from.

```csharp
GeoCoordinateSystem3 frame = plate.GetFrame();   // origin at the first vertex, Z along the normal

GeoFace2 flat = plate.ProjectToFace2(frame);     // the plan view of the plate
GeoFace2[] cut = flat.Subtract(openings);        // work in the plane
GeoFace3 back = cut[0].ToFace3(frame);           // and put it back in space
```

Everything goes through a frame, and the same frame has to be used both ways or the answer lands
somewhere else, so hold on to it. Where the frame comes from decides what the local coordinates mean:

| Frame from | Origin and first axis | Use it when |
|---|---|---|
| `polygon.GetFrame()`, `face.GetFrame()` | the first vertex, along the first edge | the plan view should be the same drawing wherever the shape sits in the model |
| `PlanarMap.FrameOf(plane)` | the plane's own origin and axes | several shapes have to share one set of local coordinates |
| `new GeoCoordinateSystem3(origin, xAxis, yAxis)` | whatever you name | the local coordinates mean something to you |

Every flat type makes the trip: `GeoPoint3`, `GeoVector3`, `GeoLine3`, `GeoPolyline3`, `GeoPolygon3` and
`GeoFace3` go out with `ProjectTo…`, and `GeoPoint2` … `GeoFace2` come back with `To…3`.

**Off the plane.** Flattening drops the local Z, so a point that does not lie on the plane has no honest
answer. `ProjectToPoint2` projects it and says so in its name; `TryToPoint2` refuses it when it is farther
away than `Tolerance.EqualPlanar`, which is how to check that geometry really is where you think it is.

```csharp
point.TryToPoint2(frame, out GeoPoint2 flat);   // false when the point is off the plane
point.ProjectToPoint2(frame);                   // always answers, by projecting
```

**Winding.** A frame whose Z axis agrees with the shape's normal keeps the shape running the way it did;
one that opposes it mirrors the plan view, so a counter-clockwise polygon comes out clockwise. A frame
taken from the shape itself always agrees.

## Working in a local frame

`GeoCoordinateSystem3` moves geometry between world coordinates and a local frame, and its axes are always
orthonormal whatever is passed in. `ToLocal` and `ToGlobal` are exact inverses of each other.

```csharp
var frame = new GeoCoordinateSystem3(
    new GeoPoint3(10, -20, 30),
    new GeoVector3(1, 1, 0),
    new GeoVector3(-1, 1, 1));

var point = new GeoPoint3(3, -7, 11);
frame.ToGlobal(frame.ToLocal(point)).IsEqualTo(point);  // true
```

`GeoTransform3` is a 4x4 matrix applied on the left, so `a.Multiply(b)` means "apply b, then a". A plane
normal is carried by the inverse transpose rather than by the matrix itself, so it stays perpendicular to
the surface even under a non-uniform scaling.

```csharp
GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(10, 0, 0))
    .Multiply(GeoTransform3.RotationZ(Math.PI / 2));

motion.Transform(GeoPoint3.Origin);  // (10, 0, 0)
motion.Inverse().Transform(motion.Transform(GeoPoint3.Origin)); // back to the origin
```

## Tekla Structures

`GeometryHelper.TeklaConvert` converts geometry between Tekla Structures and this library: points, vectors,
segments, planes, coordinate systems, bounding boxes, transformation matrices, and the faces and loops of
a Tekla solid. It is a separate project, so the core library carries no dependency on Tekla.

```csharp
using GeometryHelper.TeklaConvert;

teklaSolid.TryToGeoSolid3(out GeoSolid3 body, tolerance);

body.Volume;
body.TrySubtract(otherBody, out GeoSolid3 left);
```

Three things are checked rather than trusted on the way in. A Tekla coordinate system whose Y axis is not
quite square to its X axis is squared up. Each face is turned to agree with the normal Tekla gives it, and
the finished body is turned inside out if its signed volume says the whole surface arrived reversed —
without that, volume still measures the same but every containment query answers backwards. And a face
that cannot be made sense of is skipped rather than thrown on, which leaves the body no longer closed, so
ask `IsClosed()` before trusting a volume.

Tekla models in millimetres with coordinates that can run to hundreds of thousands, and a face of a twelve
metre member is rarely flat to the last decimal. The default `EqualPlanar` is often too tight for that, so
pass a `Tolerance` suited to the model rather than relying on the default.

## Build and Test

```bash
dotnet build src/GeometryHelper/GeometryHelper.csproj
dotnet test  tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj
dotnet test  tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj
```

Every snippet in this README is covered by a unit test in `ReadmeExamplesTests`.

## License

MIT
