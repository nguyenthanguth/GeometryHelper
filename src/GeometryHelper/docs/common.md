# Shared types

What both halves of the library are measured and described with: `Tolerance`, `Angle`, the
enumerations, `OffsetOptions`, and `GeometryHelperLog`. They sit at the root of the `GeometryHelper`
namespace, so one `using GeometryHelper;` brings them all in.

They know nothing about any particular shape, which is why one `Tolerance` and one `Angle` serve
[the plane](plane.md), [space](solid.md), [label placement](arrange.md) and [packing](packing.md) alike.

## Tolerance

Every comparison in either library that can be affected by floating point error takes a `Tolerance`,
and every such method has an overload without one that reads `Tolerance.Global`. Neither library
compares coordinates with `==`.

| Threshold | Default | Measures |
|---|---|---|
| `EqualPoint` | `1E-3` | Distance below which two points are the same point. |
| `EqualVector` | `1E-5` | Difference below which two vectors are the same vector; also the length below which a vector has no direction, and the area below which a loop is no polygon. |
| `EqualAngleRad` | 1° | Angular difference for parallel and perpendicular tests. |
| `EqualPlanar` | `1E-3` | Distance from a plane below which a point counts as lying on it. |

**Use the defaults.** They suit a model in millimetres: a thousandth of a millimetre for points and for
flatness alike, so a point that is one with another is on every plane that other is on. That is as fine as
the corners of a modelled part are exact to. Two parts a hundredth apart are apart, and a thin overlap holds
what it holds: of 218 pairs of slabs from a Tekla model that share material, the common part came out within
450 cubic millimetres of the exact volume at the median, where the default up to 10.0.0, a hundredth, came
within 1 084. A finer tolerance is no more exact. Tekla Structures gives faces a few ten-thousandths of a
millimetre out of flat and parts touching across gaps as thin, and below a thousandth those are cut as the
slivers they then are: cutting each part of a model of 1 989 parts by the parts it meets took 12.5 seconds within
a hundredth, 18 within a thousandth, and 225 and 251 within 1E-4 and 1E-5, where 31 and 36 of the cuts took more
than a minute each.

A modeller's own cuts leave faces further out of flat than the tolerance: Tekla Structures left the top face
of a notched beam 0.04 mm out at one corner. Refused as not flat, such a face was a hole in the beam, which
then gave no section and a fifth too little volume; the Tekla and IFC conversions read it instead as triangles
on its own corners (`GeoFace3.FromLoops`, see [space](solid.md)), and the beam closes.

The vector threshold is far below the point threshold because it is also the area below which a loop is no
polygon: Tekla Structures gives a triangle 0.005 by 0.08 of a millimetre at the end of each of 1 791 girders of
one model, which a vector threshold of a thousandth refused for its area of 0.0002, leaving every girder open.

How near two faces of two parts have to be to touch is not a tolerance but a question of the model, and a boolean of
two solids takes it apart: `SolidBooleanOptions.Contact` puts the faces of the second body that lie parallel to the
first's within it onto them, and the tolerance stays as exact as it is everywhere else (see
[space](solid.md#combining-bodies)). Within a thousandth alone, parts drawn a few thousandths into each other leave
skins as thin as that on what is cut.

In space, as in the plane, `DistanceTo` is nought only where two shapes truly meet, a rim counting, and otherwise the exact
gap, however small, and `CollidesWith` is whether that gap is no more than `EqualPoint`: a segment 0.0005 off a square in its
plane is 0.0005 from it and collides at a thousandth, and one 0.0012 off its corner does not. A shape off a plane by less than
`EqualPlanar` is not laid flat for its distance; it is as far in space as it is, and a triangle with no area is the segment
it has collapsed to, touching as its distance says. Seven things are read otherwise, on purpose,
and [space](solid.md#how-near-is-touching-in-space) gives each with its numbers: `DistanceTo` between two bodies, or an oriented box and an oriented or axis-aligned box, reads
nought within `EqualPoint` (only `GeoAabb3` against `GeoAabb3` is exact); a plane reads a ray, a chain, a region, a box or a
body within `EqualPoint` of it as on it, so the plane z = 0 is nought from a square 0.0004 above it; a shape a billionth of a
millimetre outside a body (further out than a million, 1E-15 of the largest coordinate) reads as in it; a segment you
give, no longer than `EqualPoint`, is read as its start; a circle collides as its rim and a loop of arcs as its outline;
`GetShortestLineTo` and `GetIntersections` keep their readings; and two partial arcs lying nearly on one axis are searched and not proved.
Pass a coarser tolerance only to the call that needs it. Within one, what touches within it is taken for touching:
net volumes to match the ones Tekla Structures reports come from booleans within 0.05. Geometry in metres wants
the point and planar thresholds a thousand times smaller, set once for the program:
`new Tolerance(1E-6, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-6)`.

`Tolerance.Default` is the four defaults together. `Tolerance.Global` starts as it, and it stays the
defaults whatever `Tolerance.Global` is set to. `new Tolerance()` is not the defaults: `Tolerance` is a
struct, and a struct made without one of its constructors, as `new Tolerance()` and
`default(Tolerance)` are, has every threshold at 0. Then only an exact match is equal, and a face out
of flat by rounding alone is refused; the notched beam, read so, lost five of its 26 faces and came
out open.

`EqualPlanar` is separate from `EqualPoint` because coplanarity is measured far from the reference
point. A face twelve metres long that is tilted by a hundredth of a degree deviates by about two
millimetres at its far end — far more than `EqualPoint` allows, yet still flat enough to work with.
Only the solid half of the library uses it. A boolean cuts and glues within a planar threshold no
wider than `EqualPoint`, so that the glue closes whatever a cut leaves: given a wider `EqualPlanar`,
it first splits a face that is flat only to that into triangles on its own corners.

**Whether two things cross is not a question of angle.** `EqualAngleRad` answers `IsParallelTo` and
`IsPerpendicularTo`, which are about directions. Where two members cross is a point, and two members
are parallel for that purpose only when they draw apart by less than `EqualPoint` along the longer of
them; a flat shape meets a plane wherever it stands off it by more than `EqualPlanar`. So two
ten-metre members crossing at a tenth of a degree cross at a point, though `IsParallelTo` calls their
directions parallel. Only two infinite lines, or two planes, have no length to measure over, and those
still go by the angle.

```csharp
// One setting for both libraries: here, for a model in metres.
Tolerance.Global = new Tolerance(1E-6, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-6);

// Or pass one explicitly, which is what to do when a single operation needs to be looser or
// tighter than the rest of the program.
bool same = first.IsEqualTo(second, new Tolerance(1E-6, 1E-6));
```

`Tolerance.Global` is a single shared setting. Changing it for a drawing in the plane changes it for
a model in space as well. Setting it swaps it whole, so a thread reading it while another sets it sees
the old tolerance or the new one, never a mix of the two.

Where one thread needs another tolerance for a while, open a scope instead of setting the shared one:

```csharp
using (Tolerance.Use(new Tolerance(1E-1, 1E-1)))
{
    plate.CollidesWith(bolt);   // within a tenth, on this thread; every other thread is untouched
}
```

Scopes nest, and each one disposed puts back what it replaced. A scope belongs to its thread: work handed
to other threads sees the shared setting, which is why a method that spreads its work, such as
`Clash3.Find`, takes the tolerance as it stands when it is called and passes it on.

## Valid values

Every constructor refuses what is not a shape — a radius or a size that is negative or not a number, a
direction of no length — but a `default` value never meets a constructor. An element of a new array, or
the `out` of a `Try` method that said false, is a plane with no normal, a ray with no direction or a
coordinate system with no axes, and it answers questions without complaint all the same: the plane is
nought from everything. `IsValid`, on every value type, says whether a value is one a constructor could
have made.

```csharp
var planes = new GeoPlane3[4];
planes[0].IsValid;                     // false: no normal
new GeoPoint3(double.NaN, 0, 0).IsValid; // false
default(GeoPoint3).IsValid;            // true: the origin is a point like any other
```

## Angle

An angle stored in radians and readable as either unit. There is deliberately no public constructor
taking a bare double: a number on its own does not say which unit it is in, so the unit is named at
the point of creation.

```csharp
Angle right = Angle.FromDegrees(90.0);
double radians = right.Radians;          // 1.5707963...

Angle turned = right + Angle.FromDegrees(300.0);
Angle wrapped = turned.Normalize();       // into [0, 2π)
Angle signed  = turned.NormalizeSigned(); // into (-π, π]
```

`Normalize` and `NormalizeSigned` differ in where they put the cut: the first is what to use for a
bearing, the second for a difference between two directions, where -170° is a nearer answer than
190°.

A text has a third cut. `Angle.ToReadableDegrees` turns its angle, in degrees, the way up it reads:
anything more than a right angle past upright, plus a tolerance, is turned a half turn, so 180° reads
as 0° and 100° as -80°. The tolerance keeps a text standing about upright from turning over from one
side to the other; it is a tenth of a radian, 5.73°, unless given, the value Tekla Structures takes,
so 95° stays 95°.

```csharp
Angle.ToReadableDegrees(180.0);                        // 0
Angle.ToReadableDegrees(100.0);                        // -80
Angle.ToReadableDegrees(95.0);                         // 95, within the tenth of a radian
Angle.ToReadableDegrees(92.0, Angle.FromDegrees(1.0)); // -88
Angle.ToReadableRadians(Math.PI);                      // 0, the same in radians, worked out in radians
```

## PointLocation

Where a point sits relative to a shape: `Inside`, `OutSide`, or `OnSide`.

What counts as `Inside` depends on the family the shape belongs to. A volume encloses a region of
space. A region encloses an area — in space that means a point counts as inside only when it lies on
the carrier plane as well as within the boundary. A curve encloses nothing and never reports
`Inside`.

## PlaneSide

Which side of an oriented plane a point lies on: `Above`, `Below`, or `On`. The sides are named after
the plane normal rather than after world up, because a plane carries its own orientation and may
point anywhere.

## LineEnd

Which end of a segment an extend, trim or lengthen operation moves: `Start` or `End`. A segment runs from
its start point to its end point, so its two ends are told apart by that order rather than by position.
The end that is not named stays where it is, and the segment never turns round.

## LineSide

Which side of a directed segment a point lies on: `Left`, `Right` or `On`, seen looking along the segment
from its start to its end. It is the counterpart in the plane of `PlaneSide` in space, and a segment is
read as the infinite line carrying it, so a point beyond either end still has a side.

## LineExtension

Which of two segments an intersection may reach past, by reading it as the infinite line carrying it:
`None`, `First` (the segment the method is called on), `Second` (the argument) or `Both`. A segment that is
not extended has to contain the intersection itself, within tolerance; an extended one only has to point at
it, however far away. This is what the `Intersect` option does for AutoCAD's `IntersectWith`.

## OffsetJoin

How an offset closes the gap that opens at a corner, where the two offset edges pull apart — at a convex
corner when a region grows, at a concave one when it shrinks. On the inside of a turn the offset edges
overlap instead and are cut where they meet, whatever the join. The three kinds are AutoCAD's offset gap
types.

| `OffsetJoin` | Corner | OFFSETGAPTYPE |
|---|---|---|
| `Miter` | the offset edges carried on until they meet | 0, Extend |
| `Round` | an arc about the original vertex, drawn as chords | 1, Fillet |
| `Chamfer` | one segment square to the corner's bisector, at the offset distance | 2, Chamfer |

## OffsetOptions

The join together with the two numbers that bound it. It is immutable, so one instance can be kept as a
setting and shared between threads.

```csharp
OffsetOptions sharp = OffsetOptions.Default;                              // Miter, limit 10, arcs automatic
var cut    = new OffsetOptions(OffsetJoin.Miter, miterLimit: 2.0);        // sharp corners cut off past 2 x d
var always = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);// no corner is ever cut off
var round  = new OffsetOptions(OffsetJoin.Round, arcTolerance: 0.5);      // chords within 0.5 of the arc
```

| Property | Default | Means |
|---|---|---|
| `Join` | — | Which of the three corners to build. |
| `MiterLimit` | `10` | How far a sharp corner may reach from its vertex, as a multiple of the offset distance; past it the corner is cut off square at that distance. At least 1, and 10 keeps every corner of 11.5° or wider sharp. |
| `ArcTolerance` | `0` | The largest gap allowed between a round corner's chords and the true arc, in drawing units. Zero means 0.2 % of the offset distance, about 50 segments for a full turn. It is met down to 1.15E-9 of the distance (65 536 chords a turn) and to about a hundredth of the point tolerance. |

`GetArcTolerance(distance)` gives the gap a round corner is actually drawn to, which is where that 0.2 %
is resolved. Each number is ignored by the joins it does not apply to — a `Round` join never reads
`MiterLimit` — but equality compares all three regardless, so two sets that would offset alike are not
necessarily equal.

The offsets themselves are in the two geometry libraries; these types only say what they should do.

## GeometryHelperLog

Where the GeometryHelper libraries report what they left out or could not do. Conversions that walk many
objects skip what they cannot read rather than throw, so that one bad object does not cost the rest;
`GeometryHelperLog` says what was skipped and why, which is how an empty or short result gets explained.

```csharp
using System;
using System.IO;
using GeometryHelper;

// Messages go to System.Diagnostics.Trace, category "GeometryHelper", unless a writer is set: the Output
// window of Visual Studio shows them while debugging, and DebugView shows them from a plugin.
GeometryHelperLog.Writer = (level, message, exception) =>
    File.AppendAllText(@"C:\Temp\geometryhelper.log",
                       GeometryHelperLog.Format(level, message, exception) + Environment.NewLine);

GeometryHelperLog.Enable = false;   // nothing is written; true by default
```

| Method | Level | Used for |
|---|---|---|
| `GeometryHelperLog.Debug` | `Debug` | Something skipped for an expected reason: a reference model that is not IFC, a GlobalId the file does not hold |
| `GeometryHelperLog.Info` | `Info` | The outcome of a call, such as how many products were converted |
| `GeometryHelperLog.Warn` | `Warn` | Something left out because it failed: a file that cannot be read, a body that cannot be rebuilt |
| `GeometryHelperLog.Err` | `Err` | A failure the caller should know about |

- Each method takes the message and, optionally, the exception behind it. `GeometryHelperLog.Format` gives the text the
  default writer uses, for example `WARN The IFC file cannot be read. [IOException: The file is in use.]`.
- Writing never throws: a writer that fails is ignored, so logging cannot break the work it reports on.
- The writer is called one message at a time, even when work spread over several threads (a clash check, the
  objects of an IFC file) logs from all of them, so it need not be safe to call from two threads at once. It
  is called on those threads, though: a writer showing messages on a form hands them over with `BeginInvoke`,
  since `Invoke` waits for the form's thread, which may be waiting for that very work.
- `Enable` and `Writer` are shared by the whole process, like `Tolerance.Global`: set them once at start-up.

## Licence

MIT.

## Looking at the geometry

When an answer surprises, look at the shapes. `GeometryHelper.Export` and `GeometryHelper.Dxf` write them in four
formats, numbers always with a point for the decimal whatever the culture:

```csharp
using GeometryHelper.Dxf;
using GeometryHelper.Export;

new ObjWriter()                      // space: any 3D viewer opens OBJ
    .Add(plate, "plate")             // a body as the mesh of where its material ends
    .Add(bar, 0.1, "bar")            // a bent bar as a line, bends cut into chords
    .Save("clash.obj");

new SvgWriter()                      // the plane: any browser opens SVG
    .Add(outline, "black", "#eeeeee")
    .Add(cut, "red")                 // arcs drawn as arcs, Y up as in the drawing
    .Save("cut.svg");

string text = Wkt.Write(face);       // POLYGON ((...), (...)): GIS tools, databases, online viewers

new DxfWriter()                      // both: AutoCAD, Tekla and every CAD viewer open DXF
    .Add(outline, "plate")           // in the plane as the entities CAD has, arcs as arcs
    .Add(plate, "plate3d")           // a body as 3DFACEs
    .Add(bar, "bar")                 // a bent bar as LINEs and ARCs, each bend in the coordinates of its own plane
    .Save("part.dxf");
```

Each shape added to an OBJ file is an object of its own under the name given, so a viewer lists them separately, and
coordinates are written exactly, as a text that reads back to the very number on .NET Framework and on .NET 10 alike. The
digits can differ between the two, a digit longer on the Framework now and then, and a negative nought is written -0 on both. An SVG picture is fitted
round everything drawn, and every line keeps its width however far it is zoomed. WKT rings are written closed, the
boundary counter-clockwise and the holes clockwise, as the format asks.

What each writer takes:

| Writer | Shapes |
|---|---|
| `ObjWriter` | `GeoSolid3`, `GeoFace3`, `GeoPolygon3` (each with a `Tolerance` overload), `GeoTriangle3`, triangles, `GeoMesh3`, `GeoCellGrid3` and `GeoCell3`, `GeoAabb3` and `GeoObb3` (as their 12 triangles), `GeoPoint3` (a dot), `GeoLine3`, `GeoPolyline3`; with a chord tolerance `GeoPolylineArc3`, `GeoPolygonArc3`, `GeoArc3`, `GeoCircle3`, `GeoEllipse3`, `GeoSpline3`, `GeoSphere3` (as the triangles of its surface); `GeoRay3` and `GeoXLine3` with a length |
| `SvgWriter` | `GeoPoint2`, `GeoLine2`, `GeoArc2`, `GeoEdge2`, `GeoCircle2`, `GeoEllipse2`, `GeoPolyline2`, `GeoPolylineArc2`, `GeoPolygon2`, `GeoPolygonArc2`, `GeoRectangle2`, `GeoTriangle2` and lists of `GeoTriangle2`, `GeoMesh2`, `GeoFace2`, `GeoRay2`, `GeoXLine2`, `GeoSpline2` (with a chord tolerance, for the splines SVG has no curve for), and `Text` for a label |
| `Wkt` | `GeoPoint2` and `GeoPoint3`, segments, chains, polygons, `GeoRectangle2`, `GeoTriangle2` and `GeoTriangle3`, `GeoFace2` and `GeoFace3`; lists of points as `MULTIPOINT`, of segments and chains as `MULTILINESTRING`, of `GeoFace2` and `GeoFace3` as `MULTIPOLYGON`; `GeoMesh2` and `GeoMesh3` as `MULTIPOLYGON`; `GeoSolid3` as a `POLYHEDRALSURFACE Z`, and with a chord tolerance `GeoSphere3` as one; with a chord tolerance too the circles, arcs, ellipses, splines and chains and loops with arcs, in the plane and in space; `GeoRay2`, `GeoRay3`, `GeoXLine2` and `GeoXLine3` with a length |
| `DxfWriter` | in the plane `GeoPoint2`, `GeoLine2`, `GeoRay2`, `GeoXLine2`, `GeoArc2`, `GeoCircle2`, `GeoEllipse2`, `GeoPolyline2`, `GeoPolylineArc2`, `GeoPolygon2`, `GeoPolygonArc2`, `GeoRectangle2`, `GeoTriangle2`, `GeoFace2`, `GeoSpline2` (as a SPLINE); in space `GeoPoint3`, `GeoLine3`, `GeoRay3`, `GeoXLine3`, `GeoEllipse3`, `GeoSpline3` (as a SPLINE), `GeoPolyline3`, `GeoPolygon3`, `GeoTriangle3`, triangles, `GeoFace3` and `GeoSolid3` (each with a `Tolerance` overload), `GeoMesh3`, `GeoAabb3` and `GeoObb3` (as six faces), `GeoSphere3` (with a chord tolerance, a 3DFACE for each triangle of its surface), and `GeoArc3`, `GeoCircle3`, `GeoPolylineArc3` and `GeoPolygonArc3` as the curves they are (each also with a chord tolerance, for chords); a `DxfText` as a TEXT or an MTEXT; each on a layer; and a `DxfShape` `DxfReader` read, with its look (see Reading DXF) |

OBJ and WKT have no curves, so a curve is written through chords and the caller says how far from the curve they may
stray: a circle of radius 100 at a chord tolerance of 0.1 is 71 chords, none more than 0.1 inside it. The tolerance has
to be a finite number above nought; there is no automatic one, and a chord shorter than the point tolerance is written
all the same. The writers work the count out from the arc sine, and give the library's wherever that is
more: a circle of radius 50 000 within 0.001 is 15 708
chords, a quarter arc of that radius 3 927, an ellipse of radii 50 000 and 25 000 15 708, and a bend of that radius in a
bar or a loop is cut the same way on its own radius. No curve is cut into more than 1 048 576 chords: a tolerance that
needs more is refused with an `ArgumentOutOfRangeException`. The `ObjWriter.Add` of a `GeoPolylineArc3` that was there
before takes any tolerance above nought, infinity included, which gives each bend one chord, where the `Wkt` writers and
the other chord overloads refuse infinity. A ray has no end, so OBJ and WKT take the length to draw it for, and SVG
draws it on to the edge of the picture, which its origin helps to fit. A line without ends has no start either: SVG
draws it across the picture from edge to edge both ways, its origin helping to fit the picture as a ray's does (the line along X
through (50, 50) over the square 0 to 100 runs from x = -5 to x = 105), and OBJ and WKT take it with a length and write the
segment of that length centred on its origin (the line through (10, 20) along X, written 100 long, is
`LINESTRING (-40 20, 60 20)`); a negative, NaN or infinite length is refused with an `ArgumentOutOfRangeException`. A `default`
ray or line, which has no direction, is refused by all three with an `ArgumentException`, and by DXF, which would write a direction
of nought AutoCAD does not read. An axis the SVG view box would hold less than a millionth of, margin and all, nought at the
six places it is written to, is given a unit centred on what is drawn: a point, a ray or a line without ends alone is a view box
1.1 by 1.1, where it was nought by nought and showed nothing, so the ray along X from (5, 5) alone runs to x = 5.55, and a
segment 2E-6 long along X is one 0.100002 by 1.1, where it was nought high. SVG draws an ellipse as an ellipse, turned by its major axis, and fits
the picture to its bounding box: an ellipse of radii 30 and 10 (60 across by 20) turned 30 degrees is 52.9 wide and 34.6
high. SVG draws an arc of a whole turn as a circle, since an SVG arc command that ends where it starts is left out of the
picture: the arc of radius 10 about the origin from angle 0 round to angle 0 is a `<circle r="10">`, where its path
`M 10 0 A 10 10 0 1 1 10 0` drew nothing. So is an arc of more than half a turn whose end prints as its start, since the
picture has six places.

A `GeoSphere3` is a body with no flat sides, so OBJ, DXF and WKT write it as the triangles of its surface within a chord tolerance, as a
body is written as its triangles, and the caller says how far from the sphere they may lie: a ball of radius 50 within 0.1 is 3 380
triangles, wound outwards with every corner on the sphere, so that OBJ has 3 380 `f` lines over 10 140 vertices, DXF 3 380 3DFACEs
(each triangle a face of its own, every edge visible) and WKT one `POLYHEDRALSURFACE Z` of 3 380 triangles. They are the triangles of
`TriangulateSurface(chordTolerance)`, and the same bytes as writing those triangles as a mesh. The tolerance has to be a finite number
above nought (the ball's own nought for an automatic one is refused here, as it is for every curve), and one that would need more than
1 048 576 triangles is refused with an `ArgumentOutOfRangeException`: radius 50 within 0.0001 would take three million. A `default` ball,
which has no radius, is refused with an `ArgumentException`, and so is one whose centre is not a number, with nothing written. SVG has
no ball, being a drawing in the plane.

A `GeoSpline2` or `GeoSpline3` is a NURBS, which DXF has: `DxfWriter` writes it as the R2000 SPLINE entity of its own degree, knots,
weights (only where it is rational) and control points, every number to the bit and no fit points, flagged closed (1) only where its first
and last control points are the same point exactly (ends that are only near do not count; the circle `FromCircle` builds ends on its start
exactly and is flagged), rational (4) where a weight is other than one, and planar (8), with the plane's normal
turned up in its largest component as the extrusion, where its control points lie in one plane to the rounding of their coordinates, as
a spline in the plane always does; the knot, control point and fit tolerances are written as 1E-10. The extents of the drawing grow by
the box the curve fills and not by its control points: the cubic with the control points (0, 0), (100, 150), (200, 150), (300, 0) is 300
by 112.5. SVG draws a spline that is not rational and of degree 3 or less exactly, to the six places of its numbers, cut at its knots into the Bézier pieces it is made of,
a C command for each piece of a cubic (that cubic is `M 0 0 C 100 150 200 150 300 0`), a Q for each of a quadratic and an L for each of a
spline of degree 1; it has no curve for a rational spline, a circle among them, nor for a degree above 3, and draws those through chords
within the tolerance it is given. OBJ and WKT write a spline through chords, the corners `Flatten` cuts it at, every one kept, its ends
the first and last control points exactly: the circle of radius 100 that `FromCircle` builds is 256 chords within 0.01.

An OBJ polygon that turns the same way at every corner and goes once round is one face, so a hexagon is one `f` of 6 and
a viewer shows it as it is; any other, an L or a pentagram, is cut into triangles, so an L-shaped polygon 100 by 100
with 50 taken from a corner is 4 triangles covering 7 500, where a viewer fanning it from its first corner would cover
the notch. `GeoSolid3`, `GeoFace3` and `GeoPolygon3` triangulate at `Tolerance.Global` unless given one, and the
triangles are cut within it. A slab 100 by 100 and 0.0008 thick, built within 1E-5, is its 12 triangles when written
within 1E-5; at the global tolerance its four sides, 0.0008 high, are dropped and the 4 triangles of its two big faces
are what is left. By the same rule a polygon that is not one face and is thinner than the tolerance is written as an
object with no face at all: an L-shaped polygon with arms 0.0004 wide, built within 1E-5, is empty at the global
tolerance and 4 triangles within 1E-5, where an equally thin strip, which is one face, is written at either. Give the
tolerance it was built within. A WKT face in space has no clockwise of its own, so its boundary is written
counter-clockwise about its normal and its holes clockwise about it.

A `MULTIPOLYGON` of the cells of a mesh, or of any faces that share an edge, parses everywhere but is not valid in the
OGC sense, whose polygons touch at points only: PostGIS's `ST_IsValid` says false of it. This holds for `Wkt.Write` of a
`GeoMesh2` or a `GeoMesh3` and of a list of `GeoFace2` or `GeoFace3` alike.

`SvgWriter.Text(at, "label", "green")` writes a label that reads upright however the picture is zoomed, its letters 3
per cent of the larger side of what is drawn high, the margin not counted. The text is escaped, so `a < b` is shown as
written, and a character XML 1.0 forbids, such as a control character, is shown as U+FFFD so that the picture still
parses. SVG numbers are written to six places, and one that rounds to minus nought is written 0.

`DxfWriter`, the writer of DXF, is set out with the reader and the editing model under [Writing DXF](#writing-dxf).

## DXF files

The library has three ways into a DXF file, each for its own job:

| To | Use | What it gives |
|---|---|---|
| draw the library's shapes for CAD to open | `DxfWriter` | a new file of R2000 in millimetres, each shape the entity CAD has for it, on the layers named (see [Writing DXF](#writing-dxf)) |
| read the geometry of a drawing | `DxfReader`, which gives a `DxfDocument` | the shapes of the plane or of space with their layer and look, every INSERT placed, read only (see [Reading DXF](#reading-dxf)) |
| change a drawing and save it with nothing else lost | `DxfDatabase` | the drawing as AutoCAD's `Database` holds it, its classes AutoCAD's with Dxf in front (see [Editing a DXF file](#editing-a-dxf-file)) |

`DxfReader` reads a whole drawing into shapes at once, 100 000 entities of eight kinds (34 MB) in 0.54 s on .NET 10, and is the
way to take geometry out of a drawing to work on. `DxfDatabase` reads what it is asked for when it is asked: 100 000 lines of
10.5 MB are opened and saved in 36 ms and walked as `DxfLine` objects in 21 ms, and what is not changed is written back as
it was read. `DxfWriter` makes a file from nothing. The three meet: an entity of the editing model gives the shapes
`DxfReader` would read for it by `GetGeometry()`, and a drawing begins as the text `DxfWriter` writes (see
[The model](#the-model)).

## Editing a DXF file

### The model

`DxfDatabase` opens a DXF file to edit it as AutoCAD's `Database` edits a drawing, and saves it again with nothing lost. Its
classes are AutoCAD's with Dxf in front, so that a plugin can use both without a clash of names: `DxfBlockTableRecord`,
`DxfLayerTableRecord`, `DxfLine`, `DxfMText`, each with AutoCAD's members and their meanings, points in world coordinates
and angles in radians as AutoCAD's .NET API gives them. Under them is an engine that keeps every byte of the file and
reads what the file holds from those bytes when it is asked for: opened and saved with nothing changed, a file is written
back byte for byte; a change writes the records it is about anew in the file's own style, and every other record keeps its
bytes. One record is read otherwise: a face of a polyface mesh with the marker AcDbVertex before its AcDbFaceRecord, as
GeometryHelper 13.0.0 and 13.1.0 wrote a face made in code, which AutoCAD 2021 refuses to open ("Class separator for class
AcDbFaceRecord expected"), is read with its own marker alone, as AutoCAD writes it, and saved so; nothing else of the file
changes.

```csharp
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

DxfDatabase db = DxfDatabase.Open("plan.dxf");                       // or Open(stream), Parse(text)

foreach (DxfEntity entity in db.ModelSpace) { ... }                  // DxfLine, DxfMText, ..., DxfUnknownEntity
DxfLine line = db.ModelSpace.OfType<DxfLine>().First();
line.EndPoint = new GeoPoint3(100, 0, 0);                            // that record written anew
line.ColorIndex = 1;
db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)) { Layer = "steel" });
db.LayerTable.Add(new DxfLayerTableRecord { Name = "rebar", ColorIndex = 3 });
db.Insunits = DxfUnits.Millimeters;
line.Erase();
db.Save("copy.dxf");                                                 // every other record the same bytes
```

There is no `ObjectId` and no transaction. An object is used directly, as AutoCAD's is once a transaction has opened it:
an object read from the drawing is the same object each time it is reached, a change to it is written at once, and an
object erased stays erased, `IsErased` true, refusing to be changed or copied. What AutoCAD gives as an `ObjectId` the
library gives as the object (`Owner`, `BlockTableRecord`, `DimensionStyle`, `GetBlockReferences`), and a handle as the
text the file prints (`Handle`, `"2F"`). `db.GetObject("2F")` and `db.TryGetObject("2F", out DxfDBObject found)` give the
object of a handle back, as AutoCAD's `GetObjectId` gives its id: an entity of model space, paper space or a block, a VERTEX
and an ATTRIB among them, an object of the OBJECTS section, a record of the layer, linetype, text style, dimension style or
block table, or one of those five tables, letters of either case the same. The records the library has no class for carry a
handle but are not found: the APPID, VPORT, VIEW and UCS tables and their records, and the BLOCK and ENDBLK that open and
close a block. The objects are found in one walk of the drawing, kept until the drawing next changes, so that many lookups
between two changes, in a loop, cost one walk. There is no undo: a change stays in the drawing until it is saved or let go.
Editing is for one thread at a time.

The objects are AutoCAD's classes in AutoCAD's order. A `DxfDBObject` is any object of the drawing, a `DxfEntity` one that
is drawn, and a `DxfCurve` an entity walked along; the tables are a `DxfSymbolTable` of `DxfSymbolTableRecord`s; the
dictionaries, records of data and groups of the OBJECTS section are a `DxfDBDictionary`, a `DxfXrecord` and a `DxfGroup`;
a layout is a `DxfLayout`, whose plot settings are a `DxfPlotSettings`. A type of entity the library has no class for yet,
an IMAGE, a REGION or a 3DSOLID among them, is a `DxfUnknownEntity`: kept as the file has it, its look editable, itself
erasable and copied, its geometry not moved.

There is no `new DxfDatabase()`. A drawing begins as a file, and one made from nothing begins as the drawing `DxfWriter`
writes with no shape in it: `DxfDatabase.Parse(new DxfWriter().ToString())` is a drawing of R2000 in millimetres with model
space and paper space, two layouts, the dimension style Standard and the named object dictionary, to which entities,
blocks and layers are added as to any other.

### AutoCAD and GeometryHelper

For a reader who knows AutoCAD's .NET API, what each daily pattern is here. The classes not named keep AutoCAD's name with
Dxf in front and their members' names.

| AutoCAD | GeometryHelper | The difference |
|---|---|---|
| `new Database(false, true); db.DxfIn(path, log)` | `DxfDatabase.Open(path)`, `Open(stream)`, `Parse(text)` | A DXF file only, ASCII, R12 to R2018; no DWG and no binary DXF. No `new DxfDatabase()`: start from `Parse(new DxfWriter().ToString())`. |
| `db.DxfOut(path, 16, version)`, `db.SaveAs(path, version)` | `db.Save(path)`, `Save(stream)`, `ToText()` | Saved in the version, code page and style it was read in; a drawing is not turned into another version. An entity appended from a drawing of another version is turned into this one's forms. |
| `HostApplicationServices.WorkingDatabase`, `Document.LockDocument()` | none | No application and no document: the drawing is the object `Open` gave. |
| `using (Transaction tr = db.TransactionManager.StartTransaction()) { ...; tr.Commit(); }` | none | Every change is written at once; nothing is rolled back. |
| `(Line)tr.GetObject(id, OpenMode.ForWrite)` | the object itself | An object is reached through what holds it (`db.ModelSpace`, a table, `entity.Owner`) and is the same object each time; no `UpgradeOpen`. |
| `ObjectId`, `id.Handle`, `db.GetObjectId(false, handle, 0)` | the object; `obj.Handle`, a text such as `"2F"`; `db.GetObject(handle)`, `db.TryGetObject(handle, out value)` | Entities (VERTEX and ATTRIB among them), objects of the OBJECTS section, and the layer, linetype, text style, dimension style and block tables and their records are found; APPID, VPORT, VIEW and UCS records and a block's BLOCK and ENDBLK, which have no class, are not. A handle of no object throws `KeyNotFoundException` from `GetObject`, as AutoCAD answers eUnknownHandle. A handle of an erased object throws it too, at once, where AutoCAD's `GetObjectId` still gives the erased object's id and fails only when it is opened (eWasErased); `TryGetObject` gives false for both. |
| `ObjectIdCollection`, `DBObjectCollection` | `IReadOnlyList<DxfEntity>` and the like | `Explode()`, `GetOffsetCurves`, `GetSplitCurves`, `GetBlockReferences` give lists of objects. |
| `var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead)` | `db.BlockTable` | Also `db.LayerTable`, `LinetypeTable`, `TextStyleTable`, `DimStyleTable`. |
| `bt[BlockTableRecord.ModelSpace]` opened for write | `db.ModelSpace`, `db.PaperSpace`, `db.BlockTable["BOLT"]` | Named `*Model_Space` and `*Paper_Space` whatever the file calls them. |
| `btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true)` | `db.ModelSpace.AppendEntity(line)` | One call; the entity is given a handle and an owner, and its layer is added to the layer table where it is not listed. |
| `foreach (ObjectId id in btr)` | `foreach (DxfEntity e in btr)` | The entities as objects, walked over those the block holds when the walk begins. |
| `entity.Erase()` | `entity.Erase()` | Takes every record of it out; no `Erase(false)` to bring it back. |
| `new Line(p1, p2)`, `new Circle(c, n, r)`, `new Arc(...)`, `new Ellipse(...)`, `new DBPoint(p)` | `new DxfLine(p1, p2)`, `new DxfCircle(c, n, r)`, `new DxfArc(...)`, `new DxfEllipse(...)`, `new DxfDBPoint(p)` | Points are `GeoPoint3`, directions `GeoVector3`. |
| `Polyline`, `Polyline2d`, `Polyline3d`, `PolyFaceMesh`, `PolygonMesh`, `Spline`, `Ray`, `Xline`, `Face`, `Solid` | `DxfPolyline`, `DxfPolyline2d`, `DxfPolyline3d`, `DxfPolyFaceMesh`, `DxfPolygonMesh`, `DxfSpline`, `DxfRay`, `DxfXline`, `DxfFace`, `DxfSolid` | Their vertices are `DxfVertex2d`, `DxfPolylineVertex3d`, `DxfPolyFaceMeshVertex`, `DxfFaceRecord` and `DxfPolygonMeshVertex`. |
| `DBText`, `MText` | `DxfDBText`, `DxfMText` | No fonts: a text's `GeometricExtents` is its insertion point, and `AdjustAlignment` is not there. `TextStyleId` is `TextStyle`, the record, beside `TextStyleName`. |
| `BlockReference`, `MInsertBlock`, `AttributeReference`, `AttributeDefinition` | `DxfBlockReference`, `DxfMInsertBlock`, `DxfAttributeReference`, `DxfAttributeDefinition` | `new DxfBlockReference(position, block)` takes the block record, not its id; `AttributeCollection.AppendAttribute` takes the attribute itself. |
| `Hatch`, `Leader`, `MLeader` | `DxfHatch`, `DxfLeader`, `DxfMLeader` | A loop is a `DxfHatchLoop` of the library's shapes; a line of `GetHatchLinesData` is a `GeoLine2`; `EvaluateLeader` is not there. |
| `RotatedDimension`, `AlignedDimension`, `RadialDimension`, `DiametricDimension`, `LineAngularDimension2`, `Point3AngularDimension`, `ArcDimension`, `OrdinateDimension`, `RadialDimensionLarge` | the same with Dxf in front | `RecomputeDimensionBlock` is not there: the block that draws a dimension is left for AutoCAD to draw anew. |
| `Table` | `DxfTable` | Cells read and set; its block is left for AutoCAD to draw anew (`RecomputeTableBlock` is not there); a shear or a scale unlike along its axes refused. `table.Cells[r, c]` is a `DxfCell`, `Rows` and `Columns` lists of `DxfRow` and `DxfColumn`; `SetTextStyle` takes the record, not its id. The forms by row type (`SetAlignment(align, rowTypes)` and the like) are not there. |
| `Viewport`, `Layout`, `PlotSettings` | `DxfViewport`, `DxfLayout`, `DxfPlotSettings` | `db.Layouts` walks the layouts in the order of their tabs; plot settings are read only. `new DxfLayout { LayoutName = "Plan" }` is added by `AddToLayoutDictionary(db, null)`, its block made, and `Initialize()` gives it its viewport 1. `SetSun` takes the `DxfSun` itself and gives it back. |
| `LayerTableRecord`, `LinetypeTableRecord`, `TextStyleTableRecord`, `DimStyleTableRecord` | `DxfLayerTableRecord`, `DxfLinetypeTableRecord`, `DxfTextStyleTableRecord`, `DxfDimStyleTableRecord` | Found by name, either case: `db.LayerTable["steel"]` is the layer Steel, and added by `Add`. A layer, a dimension style and a block are renamed by `Name` with every reference to them; a layer, a linetype, a dimension style and a block are erased by `Erase()` where nothing uses them. |
| `lt.Has("steel")`, `lt["steel"]` giving an `ObjectId` | `db.LayerTable.Has("steel")`, `db.LayerTable["steel"]` giving the record | Null where the table has none. |
| `layer.LinetypeObjectId = ltId` | `layer.LinetypeName = "DASHED"` | Records point at records by name where AutoCAD's point by id, and at the record itself where AutoCAD's property is the record (`Dimtxsty`, `Dimblk`). |
| `DBDictionary`, `Xrecord`, `Group`, `db.NamedObjectsDictionaryId`, `db.GroupDictionaryId` | `DxfDBDictionary`, `DxfXrecord`, `DxfGroup`, `db.NamedObjectsDictionary`, `db.GroupDictionary` | The dictionaries are objects; `SetAt` takes the object made in code. |
| `ResultBuffer`, `TypedValue` | `IReadOnlyList<DxfTypedValue>`, `DxfTypedValue` | A value is the text the file prints: `new DxfTypedValue(1040, "2.5")`. Setting `XData` registers its application. |
| `Entity.TransformBy(Matrix3d)`, `GetTransformedCopy` | `TransformBy(GeoTransform3)`, `GetTransformedCopy` | Each class refuses what it cannot hold, as AutoCAD's does (see [Editing rules](#editing-rules)). |
| `Entity.Explode(DBObjectCollection)` | `Explode()` giving a list | The library explodes an INSERT, a MINSERT of one cell, the three polylines, a polyface and a polygon mesh, an MTEXT, a dimension, a table, a leader, a multileader, an MLINE, a HATCH of a pattern and a MESH not smoothed, as AutoCAD's do; every other entity refuses with `NotSupportedException`, a MINSERT of more than one cell, which AutoCAD refuses too, a HATCH filled solid or with a gradient, a MESH smoothed, a REGION, a 3DSOLID and a BODY among them, which AutoCAD explodes (see [What the model does not do](#what-the-model-does-not-do)). |
| `BlockReference.ExplodeToOwnerSpace()` | `ExplodeToOwnerSpace()` | The same: the parts appended, the INSERT kept; a MINSERT gives its first cell alone, as AutoCAD 2021's did. |
| `entity.IntersectWith(other, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero)` | `entity.IntersectWith(other, DxfIntersect.ExtendThis)` giving `IReadOnlyList<GeoPoint3>` | Worked out on the library's shapes; a pair it cannot cross, a text among them, throws `NotSupportedException`, where AutoCAD crosses the box of a TEXT's or an MTEXT's words, which needs the fonts. |
| `GeometricExtents` giving `Extents3d` | `GeometricExtents` giving `GeoAabb3` | Of the shapes `GetGeometry()` gives; a text has no fonts to give it a box. |
| `Curve.GetPointAtParameter`, `GetDistAtPoint`, `GetOffsetCurves`, `GetSplitCurves`, `Extend` | the same on `DxfCurve` | Offsets go the side AutoCAD's do, a line to the left of its way and the rest to the right, as below. |
| `Curve.GetGeCurve()` giving a `Curve3d` | `GetGeCurve()` giving the library's shape (`GeoLine3`, `GeoArc3`, ...) as an `object` | `GetGeometry()` gives the same as `DxfReader` reads it, a LEADER its curve, a `GeoPolyline3` or, splined, a `GeoSpline3`, and a MULTILEADER the lines of its leaders. |
| `Clone()`, `db.WblockCloneObjects(ids, ownerId, map, DuplicateRecordCloning.Ignore, false)` | `entity.Clone()` then `other.ModelSpace.AppendEntity(copy)`; `db.WblockCloneObjects(entities, other.ModelSpace)` giving the copies | Records of the tables and the blocks a copy draws by come with it where the other drawing has none of their names, its own used where it has (see [Editing rules](#editing-rules)). No `IdMapping`: the copies are given back in the order of the entities. |
| `Point3d`, `Vector3d`, `Point2d`, `Vector2d` | `GeoPoint3`, `GeoVector3`, `GeoPoint2`, `GeoVector2` | CadConvert turns one into the other: `point.ToGeoPoint3()`, `point.ToAcadPoint3()`. |
| `Matrix3d.Displacement(v)`, `Rotation(angle, axis, centre)`, `Scaling(factor, centre)`, `Mirroring(plane)` | `GeoTransform3.Translation(v)`, `RotationAxis(centre, axis, angle)`, `Scaling(centre, factor)`, `Mirror(plane)` | `a * b` applies `b` first, as `Matrix3d`'s product does. `Matrix3d.AlignCoordinateSystem` from one system to another is `FromCoordinateSystem(to) * ToCoordinateSystem(from)`. |
| `Extents3d`, `Plane`, `CoordinateSystem3d`, `Tolerance` | `GeoAabb3`, `GeoPlane3`, `GeoCoordinateSystem3`, `Tolerance` | |
| `Color`, `LineWeight`, `Transparency` | `DxfColor`, `DxfLineWeight`, `DxfTransparency` | `DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)` and `FromRgb(255, 128, 0)`, as AutoCAD's. |
| `db.Clayer = layerId`, `db.Insunits`, `Application.SetSystemVariable("LTSCALE", 2.0)` | `db.Clayer = "steel"`, `db.Insunits`, `db.Ltscale = 2` | Variables of the header and of the OBJECTS section as properties; any other by `GetHeader("$NAME")` and `SetHeader`. |
| `db.Dimasz`, `db.Dimblk = blockId`, `db.GetDimstyleData()`, `leader.SetDimstyleData(style)` | `db.Dimasz`, `db.Dimblk = block`, `db.GetDimstyleData()`, `leader.SetDimstyleData(style)` | The `$DIM` variables of the header read as AutoCAD's metric drawing has them where the header gives none; a block, linetype or text style is the record itself. `dim.FormatMeasurement(value, "")` writes the measurement as AutoCAD does, stacked fractions in the codes of an MTEXT; tolerances, limits and the arc symbol are not part of it. |
| `MLeaderStyle`, `mleader.getOverridedMLeaderStyle(style)` | `DxfMLeaderStyle`, `mleader.GetOverridedMLeaderStyle(new DxfMLeaderStyle())` | A multileader style is read, not written: the styles of `db.MLeaderStyleDictionary` as the file holds them, and one made in code given what a multileader is drawn with; `PostMLeaderStyleToDb` is not there. |
| `db.Insert(Matrix3d, other, true)`, `db.Insert("PLATE", other, true)` | `db.Insert(GeoTransform3, other, true)`, `db.Insert("PLATE", other, true)` giving the block | The other drawing's model space, its tables and named blocks merged by name; the other drawing is never changed. |
| `db.Purge(ids)` leaving in `ids` what may be erased; `db.CountHardReferences(ids, counts)` | `db.Purge(objects)` giving those back as a list; `db.CountHardReferences(objects, counts)` | The references a DXF writes by name (a layer, a linetype, a style, a block) counted with the hard pointers. |
| `db.GeoDataObject` giving an `ObjectId`, `GeoLocationData`, `geo.PostToDb()` | `db.GeoDataObject` giving the `DxfGeoData`, `geo.PostToDb()` | `BlockTableRecordId` is `BlockTableRecord`, the record; `PostToDb` gives nothing back, the GEODATA itself now in the file. `EraseFromDb` is `Erase()`. `NorthDirection` is read only, worked out from `NorthDirectionVector`; `TransformToLonLatAlt`, `TransformFromLonLatAlt` and `UpdateTransformationMatrix` are not there. The members of `DxfTypeOfCoordinates` and `DxfScaleEstimationMethod` drop AutoCAD's prefixes: `CoordinateTypeLocal` is `Local`, `ScaleEstMethodUnity` is `Unity`. A `DxfMeshPointMap` is read only, where AutoCAD's `MeshPointMap` sets its two points. |
| `db.LayerStateManager.GetLayerStateNames(false, false)`, `LayerStatesDictionaryId(false)` | `db.LayerStateManager.GetLayerStateNames(false, false)`, `LayerStatesDictionary` | A state is hidden where its mask has the flag 0x8000 and of an xref where its record names its layers by name; `GetLayerStateNames()` gives every state. `LayerStatesDictionary` is read only and makes no dictionary. Saving, restoring, renaming, deleting, importing and exporting a state are not there. |
| `db.LayerFilters` giving a `LayerFilterTree` | `db.LayerFilters` giving a `DxfLayerFilterTree` | Read only: no `Current`, and a tree is not set back. A `DxfLayerFilter`'s `Name` has no setter; `FilterExpression` and `Filter(layer)` are not there. |
| `Highlight`, `Draw`, grips, overrules, events, reactors of the runtime | none | They need AutoCAD running. |

### Opening and saving

`DxfDatabase.Open(path)` and `Open(stream)` read the bytes of a file, `Parse(text)` its text; `Save(path)` and `Save(stream)`
write them, and `ToText()` gives the text as it would be saved. What is kept is everything: the version, R12 (AC1009) to
R2018 (AC1032), as `Version` names it; the line ends, a carriage return and a line feed, a line feed alone, or both in one
file; the group codes padded to three places or not; the mark of UTF-8 or none; the text in the code page of a file before
R2007 and in UTF-8 after, byte for byte, `CodePage` naming it (`ANSI_1252`, `UTF-8`); comments (999); the sections, tables,
objects and entities the library does not read (CLASSES, IMAGE, objects of a type with no class and the rest); extended data,
reactors and extension dictionaries; and whatever follows the EOF. A file is read as `DxfReader` reads it (see [Reading
DXF](#reading-dxf)), and one it refuses is refused the same way, with the same message: a binary DXF, one cut short, one whose
header names a code page that does not read each byte of ASCII as itself. `Warnings` says what was said of reading its text,
a file before R2007 whose text is UTF-8 or one in a code page the runtime has not; it is empty for most files.

What is written is in the file's own style: its line end, its padding, the numbers as `DxfWriter` prints them, and the text
in its encoding, a letter the page cannot hold, or whose bytes would read back as UTF-8, written as its escape (`\U+00E9`).
A file that names no version is written as one of R12, or of R2000 where its records carry subclass markers. Held against
ezdxf 1.4.5 on its drawings of R12, R2000, R2007 and R2018, recover and audit find nothing to fix in what the model writes,
ezdxf reads each value as set, and every untouched record keeps its bytes (186, 306, 359 and 361 of them; of the two
more the header has, `$PEXTMIN` and `$PEXTMAX`, which ezdxf leaves empty, grow to hold what the edits draw in paper space).

### The drawing

The header is read and set raw by `GetHeader("$INSUNITS")` and `SetHeader("$LTSCALE", new DxfTypedValue(40, "2"))`, and
through AutoCAD's properties: `Clayer`, `Insunits`, `Extmin`, `Extmax`, `Handseed`, `Celtype`, `Cecolor`, `Celweight`,
`Ltscale`, `Limmin`, `Limmax`, `Insbase`, `Pdmode`, `Pdsize`, `Textsize`, `Textstyle`, `Dimstyle`, `Lunits`, `Luprec`,
`Aunits`, `Auprec`, `Angbase` (in radians; the file writes degrees), `Angdir`, `Orthomode`, `Fillmode`, `Mirrtext`,
`LineWeightDisplay`, `Saveproxygraphics`, `Splinesegs`, `Splinetype`, `Surftype`, `Surfu` and `Surfv`. A variable R2000
brought, `$INSUNITS`, `$CELWEIGHT`, `$LWDISPLAY` and `$PROXYGRAPHICS` among them, is refused in a file of R12, as AutoCAD
writes none there. Setting `Clayer` to a layer the table does not list adds it, as an entity's `Layer` does.

Every other variable of the header AutoCAD's `Database` has as a member is read and set by its AutoCAD name in the same
way: `db.Chamfera = 10`, `db.Filletrad`, `db.TileMode`, `db.Plimmax`, `db.Useri1`, `db.Latitude`, `db.LoftAng1` and the rest,
as the API reference of `DxfDatabase` lists them, each giving its variable (`$CHAMFERA`) and its default. Where the header
gives none, a variable reads as a new drawing of ezdxf 1.4.5 has it, but for `MaintenanceReleaseVersion` (nought),
`Pextmin` and `Pextmax` (the origin, as `Extmin`), the variables that name an object (null), and `PsolHeight` and
`PsolWidth` (80 and 5, as AutoCAD's metric drawing has them, where ezdxf writes 0.08 and 0.005 in metres). R2004 and R2007
brought some, which are refused in a file of an older version as those of R2000 are. A text (`HyperlinkBase`,
`ProjectName`) is read as written, its spaces kept, and a GUID set is written in braces and capitals, as AutoCAD writes it.
A few read differently from the file:

- the UCS: `Ucsorg`, `Ucsxdir` and `Ucsydir` read the header, and `db.Ucs = new GeoCoordinateSystem3(origin, xAxis, yAxis)`
  writes all three, as AutoCAD's `Ucs` does, its axes made of unit length and square; `Ucsname`, `UcsBase` and those of
  paper space are the name of a record of the UCS table, null for none; `WorldUcsBaseOrigin(DxfOrthographicView.TopView)`
  reads `$UCSORGTOP`.
- the times: `Tdcreate` and the other dates are a `DateTime` read from the Julian day the file writes (2451545.75 is
  1 January 2000 at 18:00), `Tdindwg` and `Tdusrtimer` a `TimeSpan`; `ResetTimes()` makes the drawing new now; `TimeZone` is
  one of AutoCAD's zones in `DxfTimeZone`, and `SetTimeZoneAsUtcOffset(-7)` picks `Mountain`, the first of three.
- the objects: `Cmaterial`, `dragvs`, `Interfereobjvs`, `Interferevpvs` and `CmlstyleID` give the MATERIAL, VISUALSTYLE or
  MLINESTYLE of the drawing's dictionary that the header names, null where it has none of that name, and take one of that
  dictionary; `ApproxNumObjects` counts the objects `GetObject` finds.
- the file: `Filename` is the full path the drawing was opened from or last saved to, `OriginalFileName` the one it was
  opened from; for a drawing parsed from text or opened from a stream the second is null, and the first until it is saved
  to a path.

The dimension variables of the header, those new dimensions take, are properties too, by AutoCAD's names: `db.Dimasz`
is `$DIMASZ`, `db.Dimfxlen` is `$DIMFXL`, 77 of them. Where the header gives none, a variable reads as AutoCAD's metric
drawing (acadiso.dwt) has it, as a new drawing of ezdxf 1.4.5 does: arrowheads and texts 2.5, extension lines 1.25 past the
dimension line and 0.625 off the points, two decimal places, a comma, trailing zeros left out, the text above the line;
`Dimaltf` 1 / 25.4 and `Dimjogang` pi / 4, where ezdxf writes them to 11 and 12 places. The arrowheads (`Dimblk`,
`Dimldrblk`), the linetypes (`Dimltype`) and the text style (`Dimtxsty`) are the records the header names, by name, null
where the drawing has none of the name; `db.Dimblk = db.BlockTable["_ARCHTICK"]` writes `_ARCHTICK`. `Dimjogang` is in
radians, as the header holds it. A value AutoCAD refuses is refused, a `Dimdec` of 9 or a `Dimlunit` of 7, and a variable a
later version brought is refused in a file of an older one: `Dimdec` (R2000) in R12, `DimAssoc` (R2004) and `Dimjogang`
(R2007) in R2000, `Dimtxtdirection` (R2010) in R2007. Such a variable reads as a dimension style of that file reads it,
AutoCAD's own (acad.dwt's: `Dimdec` 4, `Dimdsep` '.', `Dimtxsty` null in R12), so that the header's data and a style's agree
there, and set to that value it writes nothing. `db.GetDimstyleData()` gives them as a `DxfDimStyleTableRecord` in no table,
named as `Dimstyle`.

Three `SetDimstyleData` give a style's variables, each writing only those that differ from what it reads and checking every
value before it writes any; a record that names no text style leaves the text style as it is in all three:

- `db.SetDimstyleData(style)` makes them those new dimensions take, `Dimstyle` left as it is. A variable the header of the
  file's version has not is passed over whatever the record holds, as AutoCAD's SAVEAS to that version leaves it out:
  DIMARCSYM, which a style holds from R2000 and the header from R2007, in a drawing of R2000. A whole number out of AutoCAD's
  range is refused, as the properties refuse it.
- `dim.SetDimstyleData(style)` and `leader.SetDimstyleData(style)` write them as overrides. A variable the file's version has
  not is passed over where the record holds AutoCAD's own value and refused where it holds another, as their properties
  refuse it; a whole number out of AutoCAD's range, a `Dimdec` of 9, and a size of the arrows, the text or the scale below
  nought are refused, as the header refuses them.

`$EXTMIN` and `$EXTMAX` hold model space and paper space. They grow to hold what is appended or moved, each axis on its
own, and an erase does not shrink them: an entity whose own record is written whole, as a LINE moved or a POLYLINE given a
new flag, grows them at once; a run of groups of its record written in place (a LWPOLYLINE's vertex, a SPLINE's knots) or
a record that follows it (a VERTEX put in or set in a POLYLINE, a point of a mesh) grows them by the entity as it stands
when the header is next read or the file saved, once however many vertices were written, so that a vertex moved out and
back between two readings leaves no trace. An INSERT appended, moved or copied by `WblockCloneObjects` grows them by what it
places, its `GeometricExtents`, each cell of a MINSERT's array among it, read in the same way when the header is next read
or the file saved, so that its block is placed once and as the drawing then has it: block W of the line from (0, 0, 0) to
(10, 0, 0) inserted at (5100, 0, 0) grows them to x 5110. An attribute of an INSERT written places its block anew in the
same way. An INSERT that places nothing, its block not defined, an external reference or the block of a layout, or one
that would place more than `MaxPlacedEntities`, leaves them as they are. The block is placed again at each reading of the
header after the INSERT changed, and only where the header holds the extents: an INSERT placing 1 000 000 lines, moved and
the header read, takes about 2 s each time, and none where the header has no `$EXTMIN` and `$EXTMAX`. An entity of a
block appended, moved or changed leaves them as they are, as AutoCAD leaves them until the INSERTs of the block are regenerated. Extents the header leaves empty, the least
corner above the most, as the 1E+20 and -1E+20 ezdxf writes for a drawing it has not measured, become the box of what is
appended or moved, as AutoCAD grows them from empty: a line from (5000, -4000, 0) to (5100, -3900, 10) appended to such a
drawing makes `Extmin` (5000, -4000, 0) and `Extmax` (5100, -3900, 10), before saving and after. What the drawing held
already is not measured into them. An axis the wrong way round alone is made the box's along it and the others grown as
ever: `$EXTMIN` (0, 0, 0) and `$EXTMAX` (100, -50, 0) with a circle of radius 5 about (200, 10) appended are (0, 0, 0) and
(205, 15, 0).

`db.UpdateExt(true)` works the extents out anew, as AutoCAD's `UpdateExt` does: `$EXTMIN` and `$EXTMAX` the box of the
`GeometricExtents` of what model space holds, `$PEXTMIN` and `$PEXTMAX` that of paper space, so that they shrink as well as
grow. An entity with no extents is passed over, a text counts as the point it stands at, and a space with nothing of extents
in it is given AutoCAD's empty extents, 1E+20 for the least corner and -1E+20 for the most; a value the header has already,
to the bit, is not written. A header that still reaches (5110, 300, 0) for a line drawn and erased since, with a circle of
radius 5 about (100, 50) left in model space, is given (95, 45, 0) to (105, 55, 0). The library takes an INSERT's placed
shapes either way, so `doBestFit` changes nothing.

The drawing reads and sets the variables AutoCAD keeps out of the header, as DICTIONARYVAR objects of the dictionary
AcDbVariableDictionary: `Cannoscale` (a scale of the drawing's list of scales), `Cetransparency` (the number an entity's
440 holds, as AutoCAD 2021 reads it: the alpha 127 is 33554559, and a per cent written before reads BYLAYER), `Tablestyle`, `MLeaderstyle`, `DetailViewStyle` and `SectionViewStyle` (by name, one of the drawing's),
`MLeaderscale`, `AnnoAllVisible`, `MsLtScale`, `MsOleScale`, `LayerEval`, `LightingUnits`, `LightsInBlocks`,
`TStackAlign`, `TstackSize`, `HpInherit`, `Isolines`, `Facetres`, `PdfFrame` and `PlineEllipse`; AutoCAD's default where the
drawing gives none, as a drawing of ezdxf, to which a variable set adds the dictionary of variables. `ThumbnailBitmap` reads
the bytes of the THUMBNAILIMAGE section.

The drawing's properties are `db.SummaryInfo`, set as AutoCAD sets them:
`db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-12" }.ToDatabaseSummaryInfo()`. A
drawing AutoCAD saved as R2004 or later keeps them in its header and has no DWGPROPS: its texts in `$TITLE`, `$AUTHOR` and
the like, and each custom property a `$CUSTOMPROPERTYTAG` followed by a `$CUSTOMPROPERTY`, as AutoCAD 2021 saved a drawing of
R2018 titled Plate B-12 with Project and Checked by after its `$DWGCODEPAGE`. They are read from there, and set there in a
drawing of R2004 or later, as many custom properties as there are, an empty text leaving its variable out and a DWGPROPS the
drawing had taken out, as AutoCAD saved such a drawing. A drawing of R2000 keeps them in its DWGPROPS, as AutoCAD 2021 saves
one: its custom properties are read from the groups 300 to 309 (`name=value`) and from a list after a group 90 (a count,
then each name and value in two groups 1), and written in the ten groups alone, ten at most, their count after the base of
the hyperlinks, as AutoCAD writes and reads them: it reads none of the list. The base of the hyperlinks is written to the
header's `$HYPERLINKBASE` too, where AutoCAD reads it, and read from there where it is not empty.

### The tables

`ModelSpace`, `PaperSpace` and each block of the `BlockTable` are a `DxfBlockTableRecord`: its `Name`, `Origin`,
`IsLayout`, `IsAnonymous` and `IsFromExternalReference`, and its entities, each the `DxfEntity` of its type's class or a
`DxfUnknownEntity`. Model space and paper space are named `*Model_Space` and `*Paper_Space`, as AutoCAD names them,
whatever the file calls them (ezdxf's files of R12 call them `$Model_Space` and `$Paper_Space`), and stand for their records
from the moment the drawing is opened, with their handles and extended data.
`BlockTable.Add(new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(0, 0, 0) })` adds a block, which is filled
by its own `AppendEntity`. A block has its
`Comments` (the BLOCK's 4), `Units`, `Explodable` and `BlockScaling` (its BLOCK_RECORD's 70, 280 and 281, from R2007 on),
`PreviewIcon` (the bytes of its 310), `IsDynamicBlock` and `Hyperlinks`. `GetBlockReferences(true)` gives its INSERTs, as
AutoCAD's `GetBlockReferenceIds` gives their ids, and `GetBlockReferences(false)` those of the blocks that hold one of it too
(all of 2 000 nested blocks in 0.02 seconds); `GetAnonymousBlocks()` gives the blocks AutoCAD made of a dynamic block.
Setting its `Name` renames it with every INSERT of it, and the arrows the overrides of a dimension of R12 name by name
(`1070 5`, `6` or `7`, then `1000` the name), as a drawing of R2000 or later names them by handle, which needs no renaming;
`Erase()` takes it out, BLOCK, entities, ENDBLK and BLOCK_RECORD, where nothing inserts it, and an INSERT made of it before
is refused from then on, as one of a block the drawing has not; and `AssumeOwnershipOf(entities)` moves entities of the
drawing into it, each keeping its handle, out of the draw order (SORTENTSTABLE) of the block it leaves and onto the end of
that of the block it enters. 3 000 blocks nested one inside the next are built from the bottom up, each added and given an
INSERT of the one before, in 0.11 seconds, and erased one by one, each looked up by name, in 0.2 seconds.

The `LayerTable` holds a `DxfLayerTableRecord` for each layer, found by its name in either case: its `Color`, `ColorIndex`,
`TrueColor`, `LinetypeName`, `LineWeight`, `IsOff`, `IsFrozen`, `IsLocked` and `IsPlottable`; its `Description` and
`Transparency`, kept in the extended data of AcAecLayerStandard and AcCmTransparency as AutoCAD keeps them;
`ViewportVisibilityDefault` (frozen in new viewports, flag 2); and `IsUsed`: layer 0, Defpoints, the current layer and any
layer an entity is on, those appended in code among them. The first `IsUsed` reads the drawing once for every layer, and the
answers hold until the drawing is edited: 400 layers of a drawing of 300 000 lines take 0.08 seconds. Setting the `Name` of
a layer renames it with every reference to it: the layer steel renamed Steel_S355 has every entity of it, in model space,
paper space and the blocks, its vertices and attributes among them, on Steel_S355, a layer named in extended data (1003) too,
and `$CLAYER` where it was the current one; layer 0 and Defpoints keep their names. `Erase()` takes a layer out where nothing
is on it, nothing points at it and it is not 0, Defpoints or the current layer, the table's count lowered by one. The
LAYER_INDEX and LAYER_FILTER objects a drawing may keep are left as they are.

A layer's look in a viewport is `layer.GetViewportOverrides(viewport)`, a `DxfLayerViewportProperties` of its colour,
linetype, lineweight and transparency there, each the override AutoCAD keeps in the layer's extension dictionary
(ADSK_XREC_LAYER_COLOR_OVR and the three others) else the layer's own; `RemoveAllOverrides()` takes them out, and erasing a
viewport takes its overrides out of the layers.

The layer states and layer filters a drawing keeps in the layer table's extension dictionary are read and listed:
`db.LayerStateManager.GetLayerStateNames()` gives the entries of ACAD_LAYERSTATES, Plan and Sections for a drawing with the
two, `GetLayerStateNames(false, false)` leaves out a state hidden (the flag 0x8000 of its mask, group 91, as AutoCAD 2021
saves one) and one of an xref (its layers named by name, group 8, not by handle), `HasLayerState("plan")` finds one in
either case and `LayerStatesDictionary` is the dictionary, each state an XRECORD of it. `db.LayerFilters.Root` is All, and
nested in it, as AutoCAD 2021 lists them, the property filters and groups of AutoCAD 2005 and later (the XRECORDs of
ACLYDICTIONARY, each named by its group 300), the filters of AutoCAD 2004 and earlier (the entries of ACAD_LAYERFILTERS,
AutoCAD keeping a property filter in both, given once), then the three AutoCAD makes, All Used Layers, Unreconciled New
Layers and Viewport Overrides: a drawing with the property filter GH_STEEL and the group GH_NOTES gives those two, then the
three. A state is not saved, restored, renamed or deleted, and a filter not changed. A drawing's place on the earth,
AutoCAD's `GeoLocationData`, is a `DxfGeoData`, `db.GeoDataObject` the GEODATA model space's extension dictionary names
ACAD_GEOGRAPHICDATA, null where the drawing holds none:
`new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Local, ReferencePoint = new GeoPoint3(500000, 4100000, 0), HorizontalUnits = DxfUnits.Millimeters, HorizontalUnitsScale = 0.001 }.PostToDb()`
puts a drawing of R2010 or later in millimetres on a local grid at 500 000 east and 4 100 000 north, the GEODATA it had
erased and the class GEODATA listed where the drawing lists none; a drawing older than R2010 is refused, and so is any block
but model space, as AutoCAD 2021 refuses paper space. A `new DxfGeoData()` is AutoCAD's new one: of version 3 (90), of no
known type of coordinates, in metres, its scale judged at the reference point; its class is listed with a count of 1, as
AutoCAD 2021 lists it. `NorthDirection` is the angle of `NorthDirectionVector` from
the Y axis towards X, as AutoCAD gives it: (1, 0) is a quarter turn, (-1, 0) minus one. The class is
listed in the CLASSES section, so a file of R2010 or later written with none is given the GEODATA and no class for it. Its
design and reference points, units and their scales, up and north directions, how the scale is judged, the correction to
the sea's level, the coordinate system's XML (in pieces of 255 characters, `^J` a line feed, as ezdxf writes it, a piece
never ending between the two halves of a letter of two units of UTF-16), the tags and the points of the mesh
(`GetMeshPointMaps`, `SetMeshPointMaps`, `AddMeshPointMap`, `ResetMeshPointMaps`) are read and set. One of version 1 (R2009)
has its points of the mesh read from its groups 12 and 13, as a comment of ezdxf maps them, though ezdxf 1.4.5 itself reads
no points from it; it is not given a north direction or points of its mesh. Taking a point to latitude and longitude needs
the coordinate system's projection and is not there.

The `LinetypeTable` holds a `DxfLinetypeTableRecord` for each linetype: its `AsciiDescription`, `PatternLength`,
`NumDashes` and `DashLengthAt`, a new one of simple dashes added by `Add`. A linetype's dashes carry their shapes and texts:
AutoCAD's gas line is `SetTextAt(1, "GAS")`, `SetShapeStyleAt(1, db.TextStyleTable["Standard"])`, `SetShapeScaleAt(1, 0.1)`
and `SetShapeOffsetAt(1, new GeoVector2(-0.1, -0.05))` on its gap of 0.2, written `49 -0.2, 74 2, 75 0, 340` the style's
handle `, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`; a dash of a file has its length set too, and a linetype nothing draws
with is erased. A text style is added to the `TextStyleTable`,
`new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx", TextSize = 2.5 }`, and its font, height, width,
slant and TrueType `Font` (a `DxfFontDescriptor`, `1001 ACAD, 1000 Arial, 1071 34` as AutoCAD keeps it) are set.

The dimension styles are the `DimStyleTable`, each a `DxfDimStyleTableRecord` with AutoCAD's 74 variables of a DXF file,
each read and set in its group: `Dimasz` 41, `Dimtxt` 140, `Dimdec` 271, `Dimjogang` 50 (radians; the file writes degrees),
`Dimtfillclr` the record's second 70 (the first is its flags), `Dimtxtdirection` 294 (from R2010), the arrowheads `Dimblk`, `Dimblk1`, `Dimblk2` and `Dimldrblk`
as the blocks they are drawn with (by handle, 342 to 344 and 341; by name, 5 to 7, in R12, ARCHTICK for the block
_ARCHTICK; null for the closed filled one), `Dimtxsty` a `DxfTextStyleTableRecord` and `Dimltype`, `Dimltex1` and `Dimltex2`
linetypes. A variable the record gives no group for reads as AutoCAD's own, that of acad.dwt: arrowheads 0.18 long, four
decimal places. A style made in code, `new DxfDimStyleTableRecord { Name = "GH_25", Dimasz = 2.5 }`, is added whole, every
variable its file's version has, its handle in group 105 as AutoCAD writes a DIMSTYLE's; renamed by its `Name`, the
dimensions, leaders and tolerances drawn in it, the styles AutoCAD keeps for its kinds of dimension (GH_25$0) and
`$DIMSTYLE` follow; `Erase()` takes out one nothing is drawn in that is not Standard or the current style.

### Dictionaries, records of data and groups

The drawing's own dictionaries are objects: `NamedObjectsDictionary`, and those it names, `GroupDictionary` (ACAD_GROUP),
`LayoutDictionary` (ACAD_LAYOUT), `MLeaderStyleDictionary`, `TableStyleDictionary` and the others AutoCAD's `Database`
names by id; null where a drawing has none, as one of R12. `SetAt` adds a `DxfDBDictionary`, a `DxfXrecord` or a `DxfGroup`
made in code; `TreatElementsAsHard` says whether a dictionary owns its entries hard (280 of 1, its entries named by 360),
and `CopyTo` copies its entries into an array. An XRECORD is a `DxfXrecord`, its `Data` the groups after its cloning flag,
the groups 102 of a list among them:

```csharp
db.NamedObjectsDictionary.SetAt("GH_PLATE", new DxfXrecord
{
    Data = new[] { new DxfTypedValue(1, "B-12"), new DxfTypedValue(40, "12.5") },   // a text and a real kept with the drawing
});
```

Every object can be given an extension dictionary, `CreateExtensionDictionary()`, an empty DICTIONARY owned by it and
written as AutoCAD writes one, reached as its `ExtensionDictionary`: `SetAt("GH_DATA", new DxfDBDictionary())` adds an
entry, `SetName` renames one, `Remove` takes one out with its object, and `ReleaseExtensionDictionary()` takes the
dictionary out once it is empty. `SetBinaryDataForKey("GH_BIN", bytes)` keeps bytes there, in an XRECORD of groups 310 of
127 bytes each, and `GetBinaryDataForKey` reads them back. `Remove` of an entry erases the dictionaries and records it owns at
every depth, and an object erased takes its extension dictionary with everything in it.

Extended data is `XData`, a list of `DxfTypedValue`s set application by application as AutoCAD sets it, each application
named in the groups given having its data replaced and one the table of applications does not list registered:
`line.XData = new[] { new DxfTypedValue(1001, "GH"), new DxfTypedValue(1000, "B-12") }`. `GetXDataForApplication("GH")`
gives one application's. A text of extended data is read with its escapes as letters, as a layer's name is, so that a letter
the file's code page has not, written as `\U+0110`, comes back as the letter it was set as. `XDataTransformBy` moves the
extended data as AutoCAD moves it: a position of the world (1011) moved, a displacement (1012) turned and scaled, a direction
(1013) turned, a distance (1041) and a scale factor (1042) scaled, a plain point (1010) kept.

A group is a `DxfGroup`, named by its entry of the group dictionary:
`db.GroupDictionary.SetAt("BEAMS", new DxfGroup("Beams of grid A", true))`, then `group.Append(beam)`, which writes the
beam's handle last among the group's (340) and the group into the beam's reactors, as AutoCAD writes a member. `Prepend`, `InsertAt`, `Remove`, `RemoveAt`, `Replace`,
`Reverse`, `Transfer` and `Clear` reorder the members, `GetAllEntityIds` gives them as entities, and `SetColorIndex`,
`SetLayer` and the other setters of the look set every member at once. A large group is filled by `Append` of a collection,
in one writing: one at a time is fine for groups as drawings have them (8 000 in half a second), but 32 000 take some 15
seconds. A group added under `*` is anonymous, `*A1` the first name its dictionary has not, and given a name, by its `Name`
or by `GroupDictionary.SetName`, it is anonymous no longer. An entity erased leaves its
groups at once. A group left empty is kept, named or anonymous, as AutoCAD 2021 keeps it, whatever emptied it: its last
member erased, `Clear`, `RemoveAt`, or never given one; the anonymous *A1, *A2 and *A3 so emptied are in the drawing AutoCAD
saves, each of no member.

### Other drawings, external references and tidying up

`db.Insert(transform, other, true)` inserts another drawing into model space, as AutoCAD's `Database.Insert` does: every
entity of its model space copied, as `WblockCloneObjects` copies entities together, and moved by the transformation, and the
records of its tables and its named blocks merged by name, this drawing's own kept where it has one of the name. A drawing
of a line from (0, 0, 0) to (10, 0, 0) on the layer steel, inserted by `GeoTransform3.Translation(new GeoVector3(100, 0, 0))`,
gives this one the line from (100, 0, 0) to (110, 0, 0) and the layer steel where it had none. `db.Insert("PLATE", other,
true)` makes the other drawing's model space the block PLATE instead, drawn about its base point (`$INSBASE`), and adds
nothing to model space; a name the other drawing has a block of is refused, as its block would come under the same name.
Every entity is checked before anything is written, so that a SPLINE into a drawing of R12, or an INSERT the transformation
would shear, refuses the whole; so does a block that inserts itself, X holding an INSERT of X or Y and Z inserting each
other, which AutoCAD's AUDIT takes out, and an entity of a type the library has no class for, a SHAPE or an IMAGE, in a model
space moved by anything but the identity, as its geometry is not moved (`NotSupportedException`). A block of it that would
come to insert itself only through an INSERT of this drawing of a block it has not is refused before anything is written
too, the blocks to be merged walked with those the copies bring: this drawing's P holding an INSERT of M, the other's A a
line and M an INSERT of P, neither A nor M comes. So is `db.Insert("NEWB", other, true)` where this drawing's P holds an
INSERT of NEWB and the other's model space one of P, which NEWB would hold. The extents grow by the
entities where they were moved to, and the other drawing is left as it is, whatever `preserveSourceDatabase` says. Its paper
space is not inserted, nor its external references but one an INSERT of its model space places, which comes with the INSERT
an external reference still, its BLOCK flagged 4 (12 overlaid) and naming its drawing as before. A dynamic block comes as a
static one: its copy draws what it drew, without the data that makes it dynamic, which AutoCAD's own insert keeps. A
drawing of 10 000 blocks nested one inside the next is inserted in about a second, and 3 000 dimensions each drawn by a
block of its own are copied in under half a second; nothing of the drawing copied from is written,
so that several threads may copy from it at once. The walks down nested blocks keep no limit of depth, but exploding and the reader's placing stop at 64
deep, as they did.

`db.AttachXref("column.dwg", "COLUMN")` attaches a drawing as an external reference: a block whose BLOCK is flagged 4 (12
for `OverlayXref`) and names the path in its group 1, with no entities, which AutoCAD fills when it opens the drawing; an
INSERT of the block places it. `XrefStatus`, of the new `DxfXrefStatus`, says where one stands, from its flags, its INSERTs
and its path: `Unreferenced` while no INSERT places it, `FileNotFound` where the path names no file, as written or from the
folder of the drawing's `Filename`, `Resolved` where AutoCAD saved it resolved (70 of 36), else `Unresolved`; no DXF says one
is unloaded. `db.DetachXref(block)` erases its INSERTs, the records it brought, those named `COLUMN|...` in the layer,
linetype, text style, dimension style and block tables, and its block.

`db.LoadLineTypeFile("GAS_LINE", "acad.lin")` adds a linetype of a file of linetypes as AutoCAD loads one: its description and
dashes, a dash's text in the text style the file names, and a shape found by its name in the compiled file of shapes beside
the .lin (`ltypeshp.shx`), drawn in the drawing's style of that file, one with no name (70 of 1) added where it has none, as
AutoCAD adds it. AutoCAD's gas line, `A,.5,-.2,["GAS",STANDARD,S=.1,U=0.0,X=-0.1,Y=-.05],-.25`, comes out three dashes 0.95
long, the text written `49 -0.2, 74 2, 75 0, 340` Standard's handle `, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`. A name of
wildcards, `*` or `DASH?`, loads each linetype that matches and passes over those the drawing has or cannot take; a name of
none is loaded or refused, a linetype of its name already there as AutoCAD's eDuplicateRecordName. A drawing of R12 takes the
simple linetypes alone. `db.LoadMlineStyleFile("STANDARD", "acad.mln")` adds a multiline style of a .mln to the dictionary of
multiline styles, made where the drawing has none, each of its elements drawn with a linetype the drawing has. A style of
that dictionary is a `DxfMlineStyle`, read as AutoCAD's `MlineStyle`: its `Elements` (each a `DxfMlineStyleElement` of
`Offset`, `Color` and `Linetype`), `Filled`, `FillColor`, the caps and the angles of its ends.

`db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve)` and `EraseEmptyObjects` count and erase the empty objects of every
space and block by AutoCAD's flags: the curves of no length and the texts of no words, a TEXT of spaces and an MTEXT of
formatting alone. A curve is of no length where no coordinate of its points is apart by more than 4 eps times the larger
magnitude of their coordinates (eps the machine epsilon, 2.2E-16, so 4 to 8 units of the last place), not to a tolerance: a
block may be drawn at any scale, and a line from (1000, 0, 0) to (1000.0008, 0, 0) in a block an INSERT draws at a scale of
10 000 is 8 long there, so it is kept, while one whose ends are 1.1E-13 apart at x 1000, one unit of the last place, is
empty. A LINE, a CIRCLE, an ARC or a polyline with a thickness is never empty, as it draws a segment of that height: a LINE
from (5, 5, 0) to (5, 5, 0) of thickness 100 is kept. `db.CountHardReferences(objects, counts)` adds to each count, in one
walk, the hard pointers that name the object (340 to 349, 360 to 369, 390 to 399, 480 and 481), the handles of extended data
of any application (1005, as a dimension's overrides name its arrow blocks and text style), the layers a viewport freezes
(331), and the names a DXF writes where AutoCAD's database holds a hard pointer: the layer and linetype of each entity, the
style of a text, of a cell of a table and of a table style, the dimension style of a dimension, the block of an INSERT, the
arrows of a dimension style (its 5, 6 and 7, in any version) and of a dimension's override in a drawing of R12 (`1070 5`
then `1000` the block's name), a layer's linetype, a layer a viewport of a drawing before R2000 freezes by name, and the
header's `$CLAYER`, `$CELTYPE`, `$TEXTSTYLE`, `$DIMSTYLE` and their kin; an entity counts its block besides.
`db.Purge(objects)` gives back those of them nothing refers to and AutoCAD does not keep itself: layer 0 and Defpoints, the
linetypes ByBlock, ByLayer and Continuous, the Standard styles (text, dimension, multiline, multileader and table), every text
style of a file of shapes, the materials Global, ByLayer and ByBlock, the plot style Normal, every visual style, the
DICTIONARYVAR variables and those of images and of wipeouts, the blocks of the layouts, the external references and what they
brought. A layer, a linetype or a block any pointer names, a soft one too, is kept, as its `Erase` refuses it: of the layers
0, steel with a line on it, one a viewport freezes and an empty one, the empty one. A layer, a linetype, a dimension style, a
block, an XRECORD and a GROUP given back are erased by their own `Erase`, which refuses none of them; a text style, an
MLINESTYLE, a MATERIAL, a DICTIONARYVAR and the other objects of the OBJECTS section are not erased by the library yet, and
their `Erase` throws `NotSupportedException`. A linetype only a layer of the list draws with is kept until that layer is
erased and the purge run again, as AutoCAD's is.

### What every entity has

Every entity has AutoCAD's `Layer`, `Color`, `ColorIndex`, `TrueColor`, `Linetype`, `LinetypeScale`, `LineWeight`,
`Visible` and `Transparency`, its `Handle`, `OwnerHandle`, `XData`, `Hyperlinks` and `ExtensionDictionary`, its `Owner` and
`BlockName`, and `GetGeometry()`, the library's shapes for it as `DxfReader` reads it, a LEADER its curve and a
MULTILEADER the lines of its leaders. A true colour's `Color` carries the
index nearest it, orange (0xFF8000) the index 30, and is written with it. `Transparency`, group 440, is a `DxfTransparency`:
BYLAYER where the record gives none, BYBLOCK, or an alpha from 0, clear, to 255, solid, `new DxfTransparency(127)` the 50 per
cent of AutoCAD's dialog, written 33 554 559 (0x0200007F); a file older than R2004 has none and refuses one. Its
`Hyperlinks` are those of its extended data of PE_URL, written as AutoCAD writes them:
`line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" })`.
`SetPropertiesFrom` takes the look of another entity, checked whole before anything is written, and `SetDatabaseDefaults`
that of a drawing's current variables. `line.HandOverTo(circle, true, true)` puts an entity made in code in the line's place,
as AutoCAD's `HandOverTo` hands the id over: the circle is written where the line stood in its block, under its handle, owner
and reactors, so that a group that held the line holds the circle, with the line's extended data and extension dictionary
where the flags keep them, else its own and none; the line is in no file from then on, a copy of what it was.

`GeometricExtents` is the box of its shapes, a `GeoAabb3`; an entity with none, an IMAGE or a LINE of no length, throws, as
AutoCAD throws for an entity with no extents. A text has no fonts here, so its box is the point it stands at. `GetPlane()`
gives the plane it lies in, that of its extrusion through its first point where all of it lies there, else the one its shapes
span, to `Tolerance.Default`, and `IsPlanar` whether there is one: a 3D polyline with a corner 0.002 off the plane of the
others has none. `IntersectWith(other, DxfIntersect.ExtendThis)` gives the points where two entities meet, by the library's
shapes for them: the line from (0, 0, 0) to (10, 0, 0) meets the circle of radius 3 about (10, 0, 0) at (7, 0, 0), and, run
on as its infinite line, at (13, 0, 0) too; an arc runs on as its circle. A closed polyline, a SOLID and a 3DFACE meet along
their edges, not across their inside: the square 4 by 4 about the origin meets the line along the x axis at (-2, 0, 0) and
(2, 0, 0), and a line standing through its middle not at all. A polyline meets segment by segment, a point meets what it lies
on, and a pair the library cannot cross, a text among them, throws `NotSupportedException`. `BoundingBoxIntersectWith`
gives where the boxes of two entities meet.

### Curves

Lines, circles, arcs, ellipses, points, rays and xlines are `DxfLine`, `DxfCircle`, `DxfArc`, `DxfEllipse`, `DxfDBPoint`,
`DxfRay` and `DxfXline`, with AutoCAD's members. A CIRCLE whose groups 10, 20 and 30 are (10, 20, 5) on the extrusion
(0, 0, -1) has its `Center` at (-10, 20, -5), as AutoCAD gives it; setting `Normal` keeps that centre where it is in the
world. An arc's `StartAngle` and `EndAngle` are radians from nought up to 2 pi, written as degrees. On the ellipse with major
axis (10, 0, 0) and ratio 0.5, `StartParam` 0.7854 (pi / 4) is the point (7.071, 3.536, 0), whose `StartAngle` is 0.4636.
`new DxfArc(center, normal, 10, 0, Math.PI / 2)` then `db.ModelSpace.AppendEntity(arc)` adds a quarter circle.

A LWPOLYLINE is a `DxfPolyline`: `pl.GetPoint2dAt(1)` is its second vertex in the plane of its normal, `pl.GetPoint3dAt(1)`
the same in the world (every vertex of a polyline of 10 000 in 15 ms), `pl.AddVertexAt(2, point, bulge, 0, 0)` puts one in, and `pl.SetBulgeAt(1, 1.0)` makes the segment
leaving it a half circle. A POLYLINE is a `DxfPolyline2d` or a `DxfPolyline3d`, walked as AutoCAD walks one,
`foreach (DxfVertex2d v in polyline2d) v.Bulge = 0;`, and grows by `AppendVertex(new DxfVertex2d(point, 0, 0, 0, 0))`; a
vertex is written, put in or taken out as its own record, the others keeping their handles, and its `Owner` is the polyline. A SPLINE
is a `DxfSpline` of its `Degree`, `ControlPoints`, `Knots`, `Weights` and `FitPoints`, a 3DFACE a `DxfFace`
(`GetVertexAt`, `IsEdgeVisibleAt`), a SOLID a `DxfSolid` (`GetPointAt`), and the meshes a `DxfPolyFaceMesh` and a
`DxfPolygonMesh`, whose vertices and face records are walked as their polyline's are. A point or face of a polyface mesh
erased keeps the mesh's counts and the faces' numbers of its points true.

A curve is a `DxfCurve`, walked as AutoCAD walks one: the line from (0, 0, 0) to (30, 40, 0) is 50 long, its parameter the
distance from its start; a circle or an arc is walked by its angle, an ellipse by its eccentric angle, a ray or an xline by
the distance from its base point, a polyline by the index of a vertex plus how far along its segment, and a spline by its knot
value. The polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex is 25.71 long, and its parameter 1.5 is
(15, 5). The curves edit as AutoCAD's do, each result a new entity of the same look in no file, to be appended:

```csharp
DxfPolyline pl = db.ModelSpace.OfType<DxfPolyline>().First();
IReadOnlyList<DxfCurve> pieces = pl.GetSplitCurves(new[] { 1.5 });   // cut half way along the second segment
IReadOnlyList<DxfCurve> outer = pl.GetOffsetCurves(5.0);              // to the right of its way: a counter-clockwise loop grows
pl.Extend(pl.EndParam + 0.5);                                         // the last segment run on by half its length or angle
DxfSpline spline = pl.Spline;                                         // the spline that is exactly the polyline
DxfCurve flat = spline.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));
```

A segment cut keeps the part of its arc and its widths read off along it: the polyline above cut at 1.5 is (0, 0), (10, 0),
(15, 5) and (15, 5), (10, 10), each arc of bulge 0.414. An offset keeps arcs as arcs and runs the corners on until they meet,
as AutoCAD's OFFSETGAPTYPE 0; the square of side 10 running counter-clockwise offset by 1 is 12 by 12, running clockwise 8 by
8. A closed polyline that crosses itself has no offset, either way: `GetOffsetCurves` and `GetOffsetCurvesGivenPlaneNormal`
of a `DxfPolyline`, a `DxfPolyline2d` or a `DxfPolyline3d` give none and say why in one Debug line, as AutoCAD 2021 gives none
of a bow tie. It crosses itself where a side passes through another, coming from one side of it and leaving to the other: the
bow tie (-10, 1), (10, -1), (10, 1), (-10, -1) at the origin, and the square (0, 0), (6, 0.06), (4, 0.06), (10, 0), (10, 10),
(0, 10), whose first and third sides cross at (5, 0.05) and curl into a loop of 0.01 square units, which AutoCAD gave no
offset inwards and outwards one running out to x = 206 on a square 10 wide. A corner that only touches another side, both
its own sides on one side of it, as (5, 0) of (0, 0), (10, 0), (10, 10), (5, 0), (0, 10), is no crossing, nor is a spike
narrower than the point tolerance (1E-3) at its foot, nor a corner given twice: such a polyline, and an open one that crosses
itself, is offset as before. `Offset2.Offset` of a `GeoPolygonArc2` that crosses itself is not changed by this: it reads the
loop even-odd, as it did. A polyline and a spline offset to that side, the right of their way seen from the tip of their normal, where an arc's
radius grows, as AutoCAD 2021 offsets them; a line, a ray and an xline to the left, as AutoCAD offsets a line: the line from
(0, 0, 0) to (30, 40, 0) offset by 5 runs from (-4, 3, 0). The exceptions
are an arc, a circle and an ellipse given a normal by `GetOffsetCurvesGivenPlaneNormal`: their radius grows with a distance
above nought whichever way the normal points, as AutoCAD offsets them by their radius, so that under the normal turned over an
arc and a polyline of that one arc offset to opposite sides. An ellipse is offset as the spline of the curve at the distance,
as AutoCAD gives one. A spline is offset piece by piece between its corners (knots standing as many times as its degree where
its direction turns), each piece a cubic fitted through points of the true offset, more of them only in the spans that need
them, until it keeps within 1E-3 of the true offset both ways; the pieces are joined as a polyline's offsets are, so that the L
of degree 2 from (0, 0) to (10, 0) to (10, 10) offset by 1 has its corner at (11, -1). A span that does not keep within 1E-3
at 512 points is given as fitted. A cubic of 300 spans with one corner gives an offset of some 9 600 control points in 0.6 s.
A spline is run on along the polynomial of its end span. Every curve gives the `Spline` that is exactly it (a circle of
radius 10 is the rational quadratic of 9 control points), but for a polyline of the plane fitted with a spline, which gives
the B-spline it is fitted with, on its frame, as AutoCAD 2021's `Polyline2d.Spline` does: the frame (0, 0), (10, 10),
(20, 0), (30, 10), (40, 0) fitted by a cubic gives the cubic of those 5 control points, not a spline through its 17 vertices
drawn. A curve projected keeps its kind where it can: a circle of
radius 10 on the floor projected along (0, 0, 1) onto the plane y + z = 0 is an ellipse of radii 14.14 and 10.

`DxfPolyline.ConvertFrom(polyline2d, true)` and `ConvertTo(true)` turn a LWPOLYLINE into a POLYLINE of the plane and back
in its place, its handle, owner, look, extended data and extension dictionary kept, the old object erased, as AutoCAD hands
the object id over; with `false` the new polyline is made in no file and the old one kept. A polyline of the plane is
fitted with `CurveFit` (a pair of arcs on each segment through every vertex) or `SplineFit` (a uniform B-spline on its
vertices as a frame, of the header's SPLINETYPE, 5 quadratic or 6 cubic, and SPLINESEGS segments to each span), a 3D
polyline with `SplineFit`, a polygon mesh with `SurfaceFit` (of SURFTYPE, SURFU and SURFV, spans each way, so that an open
mesh smoothed at 6 and 6 is given 7 rows of 7 points, as AutoCAD 2021's PEDIT makes them), each straightened by
`Straighten` or `ConvertToPolyType` / `ConvertToPolyMeshType`; the vertices of the frame keep their handles. The reader reads
a fitted polyline along the vertices the fitting made, and a smoothed mesh as its grid. Every vertex of a POLYLINE carries
the polyline's layer, linetype, colour, lineweight, linetype scale, visibility, true colour and transparency, written on it
when it is made and when the polyline's are set, as AutoCAD keeps them (its AUDIT reports a vertex of another colour); a
face of a polyface mesh keeps a colour of its own.

A spline gives and takes its `NurbsData` and `FitData` whole. `ElevateDegree` raises its degree with the curve kept to the
rounding, `InsertControlPointAt` and `RemoveControlPointAt` put a control point in and take one out with a knot,
`InsertFitPointAt`, `SetFitPointAt` and `RemoveFitPointAt` fit it anew through its points (its knots the lengths of the
chords, so that a spline through (0, 0, 0), (30, 40, 0) and (60, 0, 0) runs from 0 to 100), `ModifyPositionAndTangent`
makes it pass a point in a direction, `Rebuild` fits it again of another degree and count of points, and `ToPolyline` /
`ToPolylineWithPrecision` give the polyline along it. Made lines, it is AutoCAD 2021's: the spline's whole parameter range
halved until the spline's point halfway along each piece lies within 1 / (M (p + 1)^2) of the middle of its chord, M the
largest length of its second derivative and p the precision. The cubic on (0, 0), (10, 10), (20, 10) and (30, 0), whose
second derivative is 60 everywhere, gives 33, 65, 257, 1 025 and 4 097 vertices at 0, 1, 10, 30 and 99, and the cubics of
two and three spans of the checks 50 and 78 at 0, 4 750 and 7 562 at 99, as AutoCAD gives them; so do a quadratic of three
spans and a cubic of a double knot (29 and 76 vertices at 0). A spline of degree 5 and a rational one are given more than
AutoCAD gives them (117 where AutoCAD gives 96, 371 where it gives 130, at 0): AutoCAD reads their bend otherwise, and how
is not found yet. A periodic spline has each of its knots in the place of the vertex nearest it, and a closed one its last
vertex on its first, the polyline closed: the closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), its knots the
lengths of the chords, is those four points and the first again at 0, its 48 vertices at 10 the range's 64ths with the knot
40 in the place of the 64th at 39.05, as AutoCAD 2021 gave them; its arcs are bent as the pieces the halving made. Made
arcs, for a flat spline, it has the same vertices, each segment the arc that turns as the spline's
tangent turns along it, its bulge the tangent of a quarter of that turn, as AutoCAD 2021 makes them: the cubic of one span
at 0 gives 33 vertices, its first arc, from (0, 0) to (0.9375, 0.908), of bulge -0.00806, the tangent turning from 45
degrees to 43.15.

`ToPolyline(n)` is the library's own reading of a count: n vertices set evenly along the spline's length, each segment a
line or the arc through the spline's point halfway along it. AutoCAD 2021's `ToPolyline(n)` keeps a different promise: it
was seen to give the cubic of one span 33 vertices for every n from 2 to 50 and 65 for 99, the cubic of two spans 50 for
every n from 3 to 99, and the cubic of three spans 78 for every n from 4 to 99, about what it gives at precision 0, and to
refuse the smaller n (2 for two spans, 2 and 3 for three) with `eInvalidInput`.

Curves are joined as AutoCAD's JOIN joins them, by `JoinEntity(other)` and `JoinEntities(others)` of the entity they join
into: its record is written anew to run over them, and the others are left as they are, for the caller to erase. A
`DxfLine` takes lines along its line, gaps and overlaps and all, and runs from the lowest of their ends to the highest: the
line from (0, 0, 0) to (10, 0, 0) joined to the one from (25, 0, 0) to (15, 0, 0) runs to (25, 0, 0). A `DxfArc` takes arcs
of its circle, and a `DxfEllipse` elliptical arcs of its ellipse, counter-clockwise from its start, the gaps filled: the
quarter from 0 to pi / 2 joined to the arc from pi to 3 pi / 2 runs to 3 pi / 2; one that would make it whole is refused,
as AutoCAD makes a CIRCLE of it. Several arcs are taken in the order of their starts counter-clockwise from the source's
end, so that the order they are given in changes nothing. The gaps follow the JOIN command: that AutoCAD's `JoinEntity`
fills them too is not confirmed. A `DxfPolyline` or `DxfPolyline2d` takes lines, arcs and polylines of its plane, the whole
of each arc within the tolerance of it, end to end at either end, each turned to run on along it, an arc as a bulge, a
polyline with its bulges and widths: the polyline (0, 0), (10, 0) joined to the half circle of radius 5 about (10, 5) from
(10, 0) is (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex. A `DxfPolyline3d` takes lines and polylines
with no arc, in any plane, as AutoCAD's JOIN makes a spline of one joined to a curve; a `DxfSpline` takes lines, arcs,
elliptical arcs, polylines and splines, as their `Spline`, all raised to the highest degree among them and given by their
control points. Ends meet within 0.001 in space, the point tolerance of `Tolerance.Default`, and where they come back
together the polyline or spline is closed. `JoinEntities` chains its pieces on from the entity's end, then back from its
start, the one of lowest index each time, and gives the indices of those joined: the four sides of a square given in any
order close a polyline of one of them. A piece that would run back over the one at the end it meets is left out, as a line
drawn twice is: the line from (10, 0) back to (0, 0) does not close the polyline (0, 0), (10, 0) into a loop of no area. A
closed curve, a fitted polyline and a polyline of no vertices are not joined, and a POLYLINE's vertices are written anew,
each with a handle of its own.

What it costs: a vertex of a POLYLINE appended or set one at a time costs about the same however many the polyline has, 20 000
appended one by one in a fifth of a second and 10 000 points of a polyface mesh in a twentieth; a LWPOLYLINE's vertex put in,
set or taken out rewrites only that vertex's groups and the count, 10 000 put in one by one in 0.8 s and set one by one in
1.2 s; moving a polyline of 2 000 vertices writes them in one pass, 50 milliseconds; straightening a fitted polyline or
desmoothing a mesh takes the made vertices out in one writing, 90 000 in 0.6 s; a spline of 200 spans made a polyline of 100
vertices takes 0.04 s, and one of 1 000 spans is rebuilt of 5 000 control points in 0.05 s; 10 000 lines in any order are
joined into a LWPOLYLINE in 0.23 s and into a SPLINE in 0.19 s, all on .NET 10.

### Text

A TEXT is a `DxfDBText` (`TextString`, `Position`, `AlignmentPoint`, `Height`, `Rotation`, `WidthFactor`, `Oblique`,
`HorizontalMode`, `VerticalMode`, `IsMirroredInX` and the rest of AutoCAD's) and an MTEXT a `DxfMText` (`Contents` with
its codes, `Text` without them, `Location`, `TextHeight`, `Width`, `Attachment`). `LineSpaceDistance` is the spacing factor
times single spacing, 5/3 of the letters' height: 5 for letters 3 high. `SetAttachmentMovingLocation` holds the MTEXT by
another of its points where it stands, by its width and the height of its words (43): the MTEXT 40 wide at (20, 40, 0) held at
its top centre stands at (40, 40, 0). `ShowBorders` draws its text frame, and `BackgroundTransparency` is a
`DxfTransparency`. `BackgroundFill` or `UseBackgroundColor` turned on writes the background's colour (63) and scale (45)
where the MTEXT has none, BYLAYER and 1.5, and from R2007 its transparency (441) by layer: AutoCAD 2021 refuses the whole
file over an MTEXT whose background is on without a 63 and a 45. A text's, an attribute's and an MTEXT's style is set by its name, `TextStyleName`, written as the
drawing's table names it, letters of either case the same, or as its record, `TextStyle` (AutoCAD's `TextStyleId`):
`text.TextStyle = db.TextStyleTable["GH_TXT"]` writes `7 GH_TXT`. A style the drawing has not, or a record of another
drawing, is refused. A text, an MTEXT or an attribute made in code takes any style name, and is refused when it is added to a
drawing that has no style of that name, by `AppendEntity`, by `AppendAttribute`, or as an INSERT's attribute when the INSERT is
appended; a copy of a drawing that has the style brings it instead. In a drawing of R12 an MTEXT of a style the drawing has not
is refused before it would become TEXTs.

An MTEXT is set in columns as AutoCAD sets one: `SetStaticColumns(12.5, 2.5, 3)` is three columns 12.5 wide with gutters of
2.5, 42.5 across, the MTEXT's `Width` (41, and 40 of its embedded object) made 12.5, the width of a column, as AutoCAD and
ezdxf make it, and so do `SetDynamicColumns` and `ColumnWidth`; `SetDynamicColumns`, `SetColumnHeight` and the column properties (`ColumnType`, `ColumnCount`,
`ColumnWidth`, `ColumnGutterWidth`, `ColumnAutoHeight`, `ColumnFlowReversed`) the rest, written in its embedded object from
R2018 and in the extended data of ACAD before (`ACAD_MTEXT_COLUMN_INFO`, `ACAD_MTEXT_DEFINED_HEIGHT`), as AutoCAD and ezdxf
write them. Before R2018 the columns after the first are MTEXTs of their own, linked to the first (`ACAD_MTEXT_COLUMNS`),
which carries them: moved, turned, given a new normal, letter height or box height, or erased, it takes them with it, while
its `Contents` holds its own column's words alone. They are part of that MTEXT, as AutoCAD's database has them: a block walks
and counts the main MTEXT alone, so that moving every MTEXT of model space moves each column once; an MTEXT that lists its
own handle among its columns is walked still. A copy of the first holds the words of them all and links to none, so no two
MTEXTs claim the same columns; and `Height`, before R2007, is the height of the columns in the extended data of ACAD, 30 for
ezdxf's three static columns 30 high.

The words of a text can hold fields, kept as AutoCAD keeps them: a `DxfField`, AutoCAD's `Field`, is a FIELD the object's
extension dictionary names in its dictionary ACAD_FIELD under the property TEXT. `GetField()` gives the field of an object's
words, `GetField(propertyName)` that of another property, and `SetField(new DxfField("Drawn %<\AcVar Date>%", true))` gives
a TEXT, an MTEXT, an attribute or a dimension one, from R2004 on, written in the groups the DXF reference gives a FIELD: the
text field (`_text`) of the code `Drawn %<\_FldIdx 0>%` and its child of AcVar, each owned by what names it, marked not
evaluated, and the class FIELD listed. That layout has not yet been read by AutoCAD itself: its state (94 of 0), the empty
value's flag (93 of 1) and the format of R2004 (group 4, which libredwg reads and the library does not write) are to be
checked on a drawing AutoCAD saved, and so is which group `GetStringValue()` reads the value AutoCAD kept from (302, else 1;
the 301 after it is not read). A field made of an object's property, `%<\AcObjProp Object(%<\_ObjId 2130>%).Area>%`, names no
object here and cannot be evaluated. A field or one it holds already given to an object is refused. The words of the text are not changed; the library does not evaluate a field, and AutoCAD writes its value into
them when it next evaluates its fields. `RemoveField()` takes a field out with its children, out of the drawing's
ACAD_FIELDLIST too where it has one, and ACAD_FIELD with it once it is empty. `GetTextWithFieldCodes()` of a TEXT and
`GetMTextWithFieldCodes()` of an MTEXT give the words with the codes of their
fields in their places, `Drawn %<\AcVar Date \f "dd/MM/yyyy">%`, and `ConvertFieldToText()` makes the fields words, keeping
what AutoCAD last showed. A dimension's `FieldFromMText` and `FieldToMText` copy the field of its text from and to an MTEXT of
its own drawing, every group of it kept and a field two others name copied once, and `RemoveTextField` takes it out. A field's
`GetChildren()` gives its children in the order of its 360s, null in the place of a handle that names no FIELD, so that each
keeps the index of its placeholder `%<\_FldIdx n>%`.

An object can be annotative, which AutoCAD draws at the size its scale gives it on the sheet. The drawing's list of scales
is `db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")`, AutoCAD's `ObjectContextCollection`: each a
`DxfAnnotationScale`, a SCALE of ACAD_SCALELIST with its `Name`, `PaperUnits` and `DrawingUnits`, so that 1:50 is 1 to 50 and
its `Scale` 0.02, in the order of the dictionary; `CurrentContext` is the one `Cannoscale` names, and renaming it renames
CANNOSCALE, refused before anything is written where the drawing cannot write that variable.
`AddContext(new DxfAnnotationScale { Name = "1:100", PaperUnits = 1, DrawingUnits = 100 })` adds a scale under the first entry
name A0, A1 and on the list has not, the list made where a drawing of R2007 or later has none, and a scale of as many units of
the paper as of the drawing made the unit scale where the list has none; `RemoveContext("1:100")` takes it out, refused for
the unit scale, the current scale and a scale an object points at. `Annotative` reads the extended data AcadAnnotative of a
TEXT, an MTEXT, an attribute, a dimension, a leader, a multileader (or its own 293), a tolerance, a hatch, a style or a block,
an INSERT reading its block's, and gives `NotApplicable` for a line. Set true, it makes a text style or a dimension style
annotative by that data alone; an entity is refused, since AutoCAD shows an annotative entity at a scale by a context record
of the OBJECTS section that the library does not write until that record is checked on a drawing AutoCAD saved. `HasContext`
reads an object's contexts, of any kind, under ACDB_ANNOTATIONSCALES of AcDbContextDataManager of its extension dictionary, by
the scale each points at (its 340); `RemoveContext` erases one, the default and the last refused, and `Annotative =
DxfAnnotativeStates.False` erases them all with the data. `PaperOrientation` reads whether an annotative object keeps the way
up of the sheet from the extended data AcadAnnoPO, a reading of AutoCAD's that is not yet checked either.

`DxfMText` names the codes of an MTEXT as AutoCAD's `MText` does: `ParagraphBreak` and `LineBreak` are `\P`, `UnderlineOn` is
`\L`, `HeightChange` `\H`, `BlockBegin` `{`, and so the 19 of them. `ContentsRTF` gives its words as RTF, the style's font
first in the font table, and `SetContentsRtf` reads RTF into them: paragraphs, columns, groups, fonts with bold and italic,
colours of AutoCAD's index, the lines under and through, tabs, spaces that do not break and letters are carried over; the
heights, widths, slants, tracking and alignments of the letters, the paragraph's settings and the line over them are left
out, and a stack is its two parts with a slash between. `B-12\P{\C1;\LM16\l}` in the style Standard of the font txt is
`{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}}{\colortbl ;\red255\green0\blue0;}B-12\par {\cf1 \ul M16\ulnone }\par}`,
a font's file marked by the group `{\*\ghfontfile}` other readers pass over and the last paragraph closed by a mark, which
`SetContentsRtf` leaves out; that RTF comes back as `B-12\P\C1;\LM16`, the same words in the same look, `abc\P` as `abc\P`
and `\Fromans;abc` as `\Fromans;abc`.

### Blocks and attributes

An INSERT is a `DxfBlockReference`. The INSERT of BOLT at (50, 60, 0) turned by 45 degrees and scaled by (1.5, -1.5, 2) has
`Position` (50, 60, 0), `Rotation` 0.785 and a `BlockTransform` that takes BOLT's origin (1, 2, 0) to the insertion point;
the block's point (0, 0, 0) lands at (46.82, 61.06, 0). Setting `Position` to (60, 55, 1) moves its ATTRIB MARK from
(50, 59, 0) to (60, 54, 1): an INSERT moved, turned, scaled or given a new normal carries its attributes with it. `Name` is
the block it places, `BlockTableRecord` that block; `BlockName` is the block it is in.
`new DxfBlockReference(position, block)` places a block of the drawing.

Its attributes are `DxfAttributeReference` objects (`Tag`, `TextString`, owned by the INSERT) in its
`AttributeCollection`: `AppendAttribute` adds one, `SetAttributeFromBlock` fills one from its ATTDEF, and erasing one keeps
the others' handles. An ATTDEF is a `DxfAttributeDefinition` with its `Prompt`. An attribute of several lines embeds an
MTEXT from R2018: `IsMTextAttribute` makes one of its value where its text stands, or takes it out, `MTextAttribute` gives
and takes the MTEXT, its text made to agree as ezdxf's `set_mtext` makes it but for its value, the words of every line joined
by spaces as AutoCAD 2021 reads it (`LINE ONE LINE TWO LINE THREE` for three lines, where ezdxf writes the first). AutoCAD
writes an empty group 1 in an ATTRIB of several lines it makes, but reads the library's as its own, and a reader that does
not read the MTEXT needs the words there, so the ATTRIB keeps them. An ATTDEF of several lines writes the MTEXT's contents
in its group 1, its lines apart by `\P` (`LINE ONE\PLINE TWO\PLINE THREE`), as AutoCAD 2021 writes its own; made one of one
line it keeps its first line. The `TextString` of an ATTRIB or ATTDEF of several lines is the words of the lines of its
MTEXT joined by spaces, whatever its group 1 holds, as AutoCAD 2021 reads both: `LINE ONE LINE TWO LINE THREE` of the
definition above, and of an ATTRIB AutoCAD wrote with an empty group 1. Setting it sets the words of the MTEXT, which keeps
its place and look, its backslashes and braces written as codes, and its group 1 made to agree: the ATTRIB of three lines set
to `M24 GALV` is an MTEXT of one line `M24 GALV`, still an attribute of several lines. `UpdateMTextAttribute()` places the MTEXT
again where the text stands, after the attribute is moved or set from its definition. A MINSERT is a `DxfMInsertBlock` with
its `Columns`, `Rows`, `ColumnSpacing` and `RowSpacing`.

`UnitFactor` takes the block's units to the drawing's (`$INSUNITS`): 25.4 for a block in inches inserted in millimetres, 1
where either gives none. `GeometryExtentsBestFit()` is the box of the shapes placed, and
`GeometryExtentsBestFit(parentTransform)` the same after an outer INSERT's `BlockTransform`. A dynamic block is found as
ezdxf finds it, by the extended data `AcDbDynamicBlockGUID` of its BLOCK_RECORD, or `AcDbBlockRepBTag` of the anonymous
block that stands for it (`IsDynamicBlock`, `DynamicBlockTableRecord`, `AnonymousBlockTableRecord`); its dynamic properties
are AutoCAD's engine's to work out, and are not given. `insert.ConvertToStaticBlock("BOLT_500")` makes the INSERT place a
new block BOLT_500 holding copies of what it draws, the anonymous block's entities (or the dynamic block's, where it places
that), its tie to the dynamic block (the entry AcDbBlockRepresentation of its extension dictionary) taken out;
`ConvertToStaticBlock()` names the copy as AutoCAD names an anonymous block, `*U1` the first the drawing has not, and no
INSERT of it names: a drawing whose PPP holds an INSERT of `*U1`, a block it has not, gets `*U2`. A name given that an
INSERT of the drawing names, which would then place the new block, is refused where that block would come to insert itself
through it, PPP holding an INSERT of MM and the block made static an INSERT of PPP; nothing is written then. Exploding an
INSERT is set out under [Editing rules](#editing-rules).

### Hatches

A HATCH is a `DxfHatch`. Its loops are given in the world as the library's shapes: `hatch.GetLoopAt(0).Polyline` is the
`GeoPolygonArc3` of a loop of a polyline, and `Curves` the lines, arcs, circles, ellipses and splines of a loop of edges,
each running the way the loop runs, a part of an ellipse as the `GeoSpline3` that is exactly it.
`AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.External, square))` adds one from shapes in the hatch's plane, and
`AppendLoop(type, points, bulges)` one of AutoCAD's vertices and bulges in the plane's own system; a loop read from a hatch
is written back as it was read. A change to the loops of an associative hatch takes its association away with the handles of its boundary objects,
and takes the hatch out of their reactors, as AutoCAD's does. A loop is spliced into the bytes of the record, so that three
thousand islands appended one at a time take half a second. `Area` is exact on arcs, ellipses and splines, the islands taken
off as `HatchStyle` says: a square 30 by 20 round a circle of radius 5 fills 521.46, and a loop given twice counts as the
even-odd rule counts it, a square given twice filling nothing. `GetPlane()` is the plane of its normal at its elevation and
`GeometricExtents` the box of its loops.

`SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID")` fills it solid, and any other pattern of AutoCAD's file is
written by its name with a count of no lines (78 of 0), which AutoCAD 2021 refuses a file without; `PatternScale` and `PatternAngle` scale and turn
the lines of its pattern with them (`GetPatternDefinitionAt`), and a pattern made by the user (`_USER`, `PatternSpace`,
`PatternDouble`) is given its lines as AutoCAD makes them. `SetGradient` fills it with one of AutoCAD's gradients from
R2004, its colours `DxfGradientColor`s, and `EvaluateGradientColorAt(0.5f)` is the colour halfway along it by the
library's rules, red, green and blue mixed in proportion, not checked against AutoCAD: AutoCAD's new gradient, blue to a
pale yellow, gives (128, 128, 204). `GetHatchLinesData()` gives the lines `Explode()` draws, to the bit, as `GeoLine2`s in
the plane's own system, and `NumberOfHatchLines` and `GetHatchLineDataAt(i)` the same one by one, worked out once while
the hatch is not changed: the square 30 by 20 of a pattern made by the user, lines at 45 degrees 3.175 apart, draws 11. A
file older than R2000 has no HATCH and refuses one.

### Leaders and multileaders

A LEADER is a `DxfLeader`, a curve through its vertices (`AppendVertex`, `SetVertexAt`, `VertexAt`) that points to its
`Annotation`, an MTEXT, a TOLERANCE or an INSERT of the drawing, which lists the leader among its reactors, as AutoCAD keeps
it; a leader of fewer than two vertices is refused by `AppendEntity`, as AutoCAD refuses one. `Dimasz`, `Dimgap`, `Dimclrd`
and the other overrides read the extended data of ACAD (`DSTYLE`) where the leader has them and its dimension style where it
has not, and are written there as AutoCAD writes them: `leader.Dimasz = 3.5` writes `1070 41, 1040 3.5`. A straight leader
is walked as a curve by the index of its vertices, 1.5 halfway along its second segment; a splined one (`IsSplined`) along the
spline `Explode()` fits through its vertices, its parameter run over the lengths of its chords as AutoCAD 2021 runs it, and
its `Spline` is that spline: through (0, 0, 0), (10, 10, 0) and (20, 10, 0) it runs from 0 to 24.14 and rises to y = 10.43,
past its vertices, but its `GeometricExtents` is the box of its vertices, (0, 0, 0) to (20, 10, 0), as AutoCAD gives it. The
spline is fitted once and kept until the drawing is next changed, so that a hundred points along a leader of 4 000 vertices
take a fraction of a millisecond, not 1.6 s. `GetGeometry()` gives the leader's curve, the polyline of its vertices or that
spline, as `DxfReader` reads a LEADER, and `IntersectWith` meets the leader on it, as AutoCAD's does: the leader from
(0, 0, 0) to (10, 5, 0) meets the line x = 5 at (5, 2.5, 0). An INSERT of a block holding a leader is met on it too, and its
`GeometricExtents` hold it: the block holding that leader, inserted at (100, 0, 0) scaled 2, is met by the line x = 110 at
(110, 5, 0). `IsPlanar` agrees with `GetPlane()`: false where a vertex lies off the plane of its normal.
`GetDimstyleData()` gives every variable the leader is drawn with, its override else its style's, as a
`DxfDimStyleTableRecord` in no table; `SetDimstyleData(style)` writes those that differ from what the leader reads as
overrides, a block by its handle, so that `plain.SetDimstyleData(styled.GetDimstyleData())` gives one leader the arrow and
gap of another, and a leader given its own data changes nothing. `SetFromStyle()` takes the overrides away, the list DSTYLE of
its extended data, so that a leader of `Dimasz` 3.5 over the style Standard reads Standard's 2.5 again, and answers true, as
a dimension's does, where its drawing has the style it names; one of a style its drawing has not, and any other object,
answers false and changes nothing. `SetPlane(plane)` gives the leader the plane's normal
and moves each vertex straight onto the plane, the direction of its hook laid in it: the leader through (0, 0, 5),
(10, 10, 5) and (20, 10, 8) put in the floor runs through (0, 0, 0), (10, 10, 0) and (20, 10, 0), and a leader put in its own
plane changes nothing.

A MULTILEADER is a `DxfMLeader`: its words (`MText`, `TextLocation`, `TextHeight`) or block (`BlockContent`,
`BlockPosition`, `BlockScale`, `BlockRotation`, `SetBlockAttribute`), its look, and its leaders and their lines by AutoCAD's
indices (`AddLeader`, `AddLeaderLine`, `GetVertex`, `SetVertex`). A line's last vertex is the point its leader's dogleg
starts at, which every line of the leader ends at: `mleader.GetLastVertex(0)` is that point, and `SetLastVertex` moves it
for them all. A property set is written in the MULTILEADER and in its context data, where AutoCAD keeps both, and sets its bit
of the overrides of the multileader style. `new DxfMLeader()` takes the drawing's multileader style when it is appended.
`GetGeometry()` gives the lines of its leaders, as `DxfReader` reads a MULTILEADER, and `IntersectWith` meets the
multileader on them: each leader line a `GeoPolyline3` from its first vertex, where its arrow points, through its vertices
and the last point of its leader to the end of the dogleg, the path `Explode()` draws; splined, a `GeoSpline3` fitted
through those points with the tangents `Explode()` fits with. Both start at the arrow's point, where `Explode()` starts the
line at the back of the arrow, so that a splined line and the spline exploded part by up to about the arrow's size near the
arrow (0.585 on the multileader below, splined, its arrow 4 long). The line from (780, -10, 0) through (790, -5, 0) to its
dogleg from (790, -1.25, 0) to (798, -1.25, 0) is met by the line y = -3 at (790, -3, 0) and by the line x = 794 at
(794, -1.25, 0). Its words or block, its arrowheads and invisible leaders are not met. An INSERT of a block holding a
multileader is met on its lines too.

The multileader style a multileader is drawn with is a `DxfMLeaderStyle`, as AutoCAD's `MLeaderStyle`: the styles of
`db.MLeaderStyleDictionary` are read as the file holds them (`TextHeight`, `ArrowSize`, `LandingGap`, `DoglegLength`,
`LeaderLineType`, `LeaderLineLinetype`, `TextStyle`, `Block` and the rest of its look), and
`mleader.GetOverridedMLeaderStyle(style)`, AutoCAD's `getOverridedMLeaderStyle` named as the library names its members, gives
a style made by `new DxfMLeaderStyle()` the style's values with the multileader's own in place of those it overrides (the
bits of its 90), as ezdxf's `MLeaderStyleOverride` reads them: a multileader of Standard with words 2.5 high of its own gives
`TextHeight` 2.5 and Standard's arrows, 4 long. The height of the words and the landing gap are read from the context data
and the dogleg from the multileader's own 41, all scaled, the arrow size from its own 42, unscaled. `SetFromStyle()` goes
the other way: the multileader takes its style's values, its words 4 high again, and its bits are set to nought, but for
those of what it cannot take (a linetype or a text style the style names none of or its drawing has not, a content it does
not hold, an arrow or a block a copy in no drawing cannot be given, the attachments of the top and bottom in a file older
than R2010) and of the properties the library does not write (bits 18 and 26). Its sizes are the style's scaled once by the
style's scale: Standard scaled 2 gives words 8 high, a gap of 4 and arrows and doglegs drawn 8 and 16 long. A dimension, a
leader or a multileader of a style its drawing has not answers false and changes nothing. One leader line is given a look
of its own by
`SetLeaderLineType`, `SetLeaderLineColor`, `SetLeaderLineWeight` and `SetArrowSize(leaderLineIndex, value)`, from R2010:
its groups 170, 92, 171 and 40 after its index and the flags 93 of those it overrides, in the order of the DXF reference;
their `Get` give the line's own where it has one and the multileader's where it has not, and `Explode()` draws the line with
its own type, colour, linetype, lineweight and arrow size: a line splined of its own is a SPLINE among straight lines, one
invisible of its own gives nothing, and a size of its own, unscaled as `ArrowSize` is, is drawn times the `Scale`. `SetPlane(plane)` puts the multileader in a plane: the plane of its context data the
one given, its x axis laid in it, every point moved straight onto it and every direction laid in it, the matrix of its block
turned with it, the words of a mirrored multileader still read from behind the plane; its words at (800, 0, 0) put in the
plane z = 5 are at (800, 0, 5), and a multileader put in its own plane changes nothing, the leaning axes a shear leaves
among what it keeps.

### Dimensions

A DIMENSION is a `DxfRotatedDimension`, `DxfAlignedDimension`, `DxfRadialDimension`, `DxfDiametricDimension`,
`DxfLineAngularDimension2`, `DxfPoint3AngularDimension` or `DxfOrdinateDimension` by its kind, an ARC_DIMENSION a
`DxfArcDimension` and a LARGE_RADIAL_DIMENSION a `DxfRadialDimensionLarge`, each a `DxfDimension` with AutoCAD's points
in world coordinates (`XLine1Point`, `DimLinePoint`, `Center`, `ChordPoint`, `ArcPoint` ...), `TextPosition`,
`DimensionText`, `DimensionStyle` and `Measurement`, worked out from the points as AutoCAD works it out: the rotated
dimension of (0, 0, 0) and (100, 20, 0) along 30 degrees measures 96.60, the angle of a level line and one at 45 degrees
pi / 4. A variable of the style reads as the dimension draws with it, its override (the list DSTYLE of its extended data of
ACAD) or its style's, and set is written as an override, the style kept: `dim.Dimasz = 4.5` writes `1070 41, 1040 4.5`.
An ordinate measures along the user's axes its 51 gives (the negative of their angle from the x of the normal's system),
which a move or a turn carries, so its measurement stays. `Dimtxtdirection` is DIMTXTDIRECTION, the override 1070 294 from
R2010.

`dim.FormatMeasurement(dim.Measurement, dim.DimensionText)` gives the text AutoCAD writes for a measurement, from the
variables the dimension reads. A length is multiplied by `Dimlfac` (a negative one taken as its size in paper space and as 1
elsewhere), rounded by `Dimrnd` and written in `Dimlunit`: scientific `1.55E+01`, decimal `15.50`, engineering `1'-3.50"`,
architectural `1'-3 1/2"`, fractional `15 1/2` (15.5 to two places, or to a sixteenth, fractions not stacked), or as the
Windows desktop writes it under the culture of the thread; `Dimzin` leaves zeros out (8 makes 12.50 12.5; the low two bits
keep or leave nought feet and nought inches), `Dimdsep` is the decimal separator, `Dimpost` (`<> mm`, or `mm` alone as a
suffix) goes about it, and where `Dimalt` is on its alternate units follow in brackets, `10"[254.00mm]`, or stand in the
place of a `[]` of the text, `<> ([])` giving `10" (254.00mm)`. A fraction stacked by `Dimfrac` is written in the codes of an
MTEXT, `{\H0.7x;\S1/2;}` across or `{\H0.7x;\S1#2;}` aslant, its height `Dimtfac`. Scientific units write the power of ten
of two figures at least, `1.000E-20`, `1.500E+300`. An angle of an angular dimension, in radians, is written in `Dimaunit`
to `Dimadec` places: `45°`, `45°30'15"`, `50g` or `0.7854r`. A number is rounded half away from nought as its 15 significant
figures, a .NET decimal, so 2.675 to two places is 2.68 under .NET Framework and .NET alike, where the double nearest 2.675
lies just below it and ezdxf's `format_text`, rounding that double, gives 2.67. Tolerances, limits, the arc symbol and the R
or diameter sign before a radius are not part of the text.

What draws a dimension is the block AutoCAD drew for it, `DimBlock` (`*D7`), placed at `DimBlockPosition`: the library
writes a point, a text or a variable and leaves that block as it was, for AutoCAD to draw anew when it next draws the
dimension, so AutoCAD shows the old picture until then. A dimension made in code is given its `DimBlock`, a block of the
drawing, before it is appended. A block, linetype or text style set on a dimension in no drawing, or on a copy, is held as
the record it is (a `Clone()` holds it too) and taken to the record of its name in the drawing it is added to, refused where
there is none of that name to take it to.

### Tables of cells

An ACAD_TABLE is a `DxfTable`: its `NumRows`, `NumColumns`, `RowHeight(row)`, `ColumnWidth(col)`, and each cell's
`TextString`, `Value`, `CellType`, merging (`IsMergedCell`, `GetMergeRange`), corners (`GetCellExtents`), block and
attribute values, and the look it holds of its own, `Alignment`, `TextHeight`, `TextStyle` and the colours, null where it
has none and takes its table style's, which the library does not read. Its record is read once while it stands, every cell
of 1 000 rows in milliseconds. Its cells are written in the form of R2000 (text in 2 and 1) or of R2007 on (between
`301 CELL_VALUE` and `304 ACVALUE_END`, the text in 302), and `SetTextString`, `SetRowHeight`, `SetColumnWidth`,
`SetBlockScale`, `SetBlockRotation` and `SetBlockAttributeValue` write that data, the table's block (`*T12`) left for
AutoCAD to draw anew. Scaled alike by `TransformBy`, a table scales the heights of its rows, the widths of its columns, its
cell margins and the text heights and block scales it and its cells override: a table of rows 10, 8 and 8 high scaled by 2
has rows 20, 16 and 16 high, where AutoCAD 2021 measures the rows anew by its font (22, 18 and 18). Mirrored, it stays readable, as AutoCAD 2021's `TransformBy` leaves it: its
normal the mirror's image of its normal, its direction the image reversed, and its point the image of the far end of its
top edge, so that the table at (300, -100), 70 wide, mirrored in the plane x = 280 stands at (190, -100) facing up. Mirrored
so that the image of its direction still reads left to right or upwards, it stands at the image of its point along that
image, its rows running the other way (`FlowDirection` `BottomToTop`, its 70 of 1 written in its head with the flag 0x04 of
its 93, as AutoCAD 2021 writes it): mirrored in y = -150 the table stands at (300, -200) along (1, 0, 0), and in the line at
45 degrees through (280, -100) at (280, -80) along (0, 1, 0), as AutoCAD's do. Scaled, every cell that has no text height of
its own is given the height of its kind of row, scaled, as its own (its 140 with its flag 0x20), as AutoCAD 2021 keeps a
table it scales: the table's own height for that kind of row where it has one, else its table style's, from the style the
table names, else the drawing's Standard, else AutoCAD's own Standard, the title 6 and the header and the data 4.5 in a
drawing of millimetres (`$MEASUREMENT` 1), 0.25 and 0.18 in one of inches. The table of point 54 scaled by 2 has the two
cells of its title 12 high and the others 9, its cell of its own 3.5 now 7. AutoCAD read the heights written as the table's
own instead as 0.18 in every cell, so none is written there. Its rows are still not measured by AutoCAD's font.

A cell's value has its kind, `DataType` (`Long`, `Double` or `String`, the value's 90 from R2007 on), what its number
measures, `UnitType`, and its format, `Format` (`%lu2%pr2`), each in the cell's `301 CELL_VALUE`.
`SetValue(row, col, 12.5)` writes a number of the kind `Double`, its text 12.5, its unit and format kept, and
`SetValue(row, col, 42)` a whole number; `SetFormat`, `SetDataType` (the text 12 turned into the whole number 12, the
text Parts refused as a number, a unit of several flags as `Angle | AngleNotTransformed`, 65 538, written as it is) and
`ResetValue` write the rest, in the cell's value: none of them sets a flag of the cell's overrides, so `GetOverrides`
gives what it gave. The library does not format a value: AutoCAD writes its text by the format when it draws the table
anew. A cell of a file before R2007 holds its text alone, so a value set there is written as its text and reads back as
one, and a kind or a format other than a text and none is refused. A date or a point is refused as a value: the library
knows no groups for one, as the DXF reference does not describe a cell's value. The look of a cell is set as its own
override with its flag in the cell's 177 (91 from R2007 on), in the order the DXF reference lists them: `SetAlignment`,
`SetTextHeight`, `SetTextStyle`, `SetContentColor`, `SetBackgroundColor` (a colour of the index, BYLAYER or BYBLOCK; a
true colour refused) and `SetBackgroundColorNone`; `SetTextRotation` and `SetRotation` write a cell's turn (145),
`SetAutoScale` a block cell's fitting (174), and `SetCellType` makes a block cell a text cell, its block and attribute
values taken out. `GetOverrides` gives what a cell holds of its own as AutoCAD's `CellProperties`, `SetOverrides` keeps
those given and takes the rest away, so that the cell takes them from its table style, `RemoveAllOverrides` takes them
all, both keeping the cell's own kind of value (its flag 0x40000), for which the library knows no groups; and
`TableStyleOverrides` and `ClearTableStyleOverrides` read and take away the table's own (its flags 93 to 96 and the
groups after its last cell), in one pass over the table's groups: the cells of a table of 16 001 rows of 2 columns, half
of them of three overrides, are cleared in 0.19 s, where taking each group out on its own took 6.6 s. The cells are
walked row by row (`foreach (DxfCell cell in table)`), and `table.Cells[1, 0]` is a `DxfCell` that reads and sets one,
`cell.TextString = "Count"`, `cell.Alignment = DxfCellAlignment.MiddleCenter`; `table.Rows[0].Height` and
`table.Columns[1].Width` are read and set, and the block of merged cells `GetMergeRange(0, 1)` gives reads its cells,
`range[0, 0].TextString`. `HitTest(point, viewVector)` gives the cell a point hits, carried along the direction it is
seen in onto the table's plane, on a table turned or mirrored as on a flat one: on the table of columns 40 and 30 wide
and rows 10, 8 and 8 high at (300, -100, 0), (350, -112, 5) seen from above hits the cell (1, 1). `AttachmentPoint` is
where a cell's content is placed, in from its sides by the margins as its alignment says, AutoCAD's STANDARD style's
alignment taken where the cell has none of its own: the header cell (1, 1) is placed at (355, -114, 0). A cell of a
block of merged cells is placed by the block's top left cell, which holds its content, and the top is the cell's top as
it is seen whichever way the rows flow: with the rows of that table flowing up, the cell (2, 0), from y -82 to -74, is
placed at the top middle at (320, -74.06, 0). None of these draws the table's block anew. What a file of these versions
has no group for is not there: a cell's state, tooltip, cell style, margins of its own, content layout, custom data and
merging of all, and a column's name; nor are the forms by row type (`SetAlignment(align, rowTypes)` and the like), whose
groups after the last cell the DXF reference lists without their order.

A table's structure changes as AutoCAD's does, each change written in one writing of its record, its block left for
AutoCAD to draw anew. `InsertRows(row, height, rows)` and `InsertColumns(col, width, columns)` put in rows and columns
of empty text cells, written in the form of the file (text in 1 before R2007, in a `301 CELL_VALUE` from R2007 on), with
their counts (91, 92) and heights and widths (141, 142): the table of rows 10, 8 and 8 given two rows 6 high before its
row 2 has rows 10, 8, 6, 6 and 8, and a column 25 wide put in before its column 1 grows the title merged across it to
three columns. `InsertRowsAndInherit` and `InsertColumnsAndInherit` give the new cells the look of a row or column (the
overrides with their flags, the turn, and from R2007 on the unit and format of the value), not its content. `DeleteRows`
and `DeleteColumns` take them out, a block of merged cells shrunk to what is left of it and unmerged where one cell is
left, its top left cell's content and look moved to the first cell of it left where its top row or first column goes,
and `SetSize(numRows, numCols)` cuts or carries on the table from its bottom and right, the new rows as high as the
last; `CanInsertRow`, `CanInsertColumn`, `CanDeleteRows` and `CanDeleteColumns` say whether they can, one row and one
column at least kept. 16 000 rows go in or out in about 0.3 s on .NET 10, as one setter of a cell takes.
`MergeCells(range)` marks each cell of the range merged (173 of 1) and gives its top left cell the width and height of
the block (175, 176), a block within the range taken into it and one across its edge refused, and `UnmergeCells` takes
back each block the range meets. The edges of a cell are its own overrides: `SetGridColor(row, column, gridLineType,
color)`, `SetGridLineWeight`, `SetGridVisibility` and `SetGridProperty` write its top, right, bottom and left colours
(69, 65, 66, 68), lineweights (279, 275, 276, 278) and showing (289, 285, 286, 288) with their flags, and
`SetGridProperty(range, ...)` the lines of a block, those inside on the cells either side of each line; the cell beside
one is left as it is. `GetGridColor`, `GetGridLineWeight`, `GetGridVisibility`, `GetGridProperty` and `GridColor(row,
col, edge)` read a cell's own, null where it has none. `GridColor(gridlineType, rowType)` and its setter
`SetGridColor(color, borders, rows)`, with the lineweights and showing alike, read and write the table's own lines of
the title, the header or the data after its last cell, with their flags in 94, 95 and 96, the groups of a code in the
order of their flags and after the table's colours of its rows. `SetFormula(row, column, 0, "=A2+B2")`, from R2004 on,
adds a FIELD of AutoCAD's expressions (AcExpr, field code `\AcExpr (A2+B2)`) owned by the table, written as `DxfField`
writes one with the class FIELD listed, and points the cell's 344 at it, its value left for AutoCAD to work out; a field
a cell lets go of, by a text, a value or a formula set, by its row or column taken out or by a copy or a fill over it,
is erased with its children where the table owns it and nothing else of the file points at it (a copy of the table,
which holds its original's fields, an XRECORD, another field), and taken out of the drawing's ACAD_FIELDLIST; a field
kept whose owner goes is given to a field that names it, else to the table. A copy of a table in its own drawing shares
the original's fields: erasing the original table leaves the copy's 344s naming nothing, a limit listed for later;
`GetFormula` and `HasFormula` read one back, or one AutoCAD wrote as a field of text holding it. `CopyFrom(source,
options, sourceRange, targetRange)` copies cells of a table of the same drawing, content, look and merges, growing the
table with `ExpandOrContractTable`, which never shrinks it (a copy past the last row of the header and data rows makes
the table of 3 rows one of 5), the content alone with `SkipFormat`, the look alone with `SkipContent`; a cell's field is
not copied, and the other options are refused. `Fill(fillRange, sourceRange, options)` fills down (or along with `Row`)
from the source, copying it, or with `GenerateSeries` carrying its numbers on, 10 and 20 to 30 and 40, each rounded to
the places of the source and the step, to 15 figures (0.1 and 0.2 to 0.3; 1, 2 and 4 to 5.5 and 7), and with `Reverse`
the other way, 10 and 20 to 10, 0 and -10; a text is a number only before R2007, and dates are not carried on.
`SetFromStyle()` takes the table's and its cells' overrides away, as `ClearTableStyleOverrides(0)` does. What a file of
these versions has no group for is not there: several contents in one cell, and a line's style, linetype and the spacing
of a double line.

A range of a table's cells is linked to data as AutoCAD links one to a spreadsheet, from R2007 on; the library reads
such links and takes them away, and writes no new one yet. A `DxfDataLink` is a DATALINK of the drawing's
`DataLinkDictionary` (ACAD_DATALINK), read with its `DataAdapterId` (`AcExcel`), `ConnectionString`
(`parts.xlsx!Sheet1!A1:B2`), `Description`, `ToolTip`, `DataLinkOption`, `UpdateOption`, `IsValid` and
`GetUpdateStatus`; its `Name` set renames its entry. A range's top left cell names its link after its 92, made odd: the
DATALINK's handle (340), the range's rows (93) and columns (94) and a 96 of nought. `GetDataLink`, `GetDataLinkRange`
and `IsLinked` read it from every cell of the range: the table linked over (1, 0) to (2, 1) gives that range from its
cell (2, 1), and `link.GetTargets()` gives the table. Only a text cell names a link: a block cell's 340 names its block,
whatever its 92. Rows and columns put in and taken out, `SetSize` and `CopyFrom` move, grow and shrink a linked range as
they do a block of merged cells, `CopyFrom` bringing the links wholly within its source unless `SkipDataLink` is given,
and a cell filled from a linked one is not linked. `RemoveDataLink` takes the links away and leaves the DATALINK the cells
let go of in the drawing, named or `Anonymous`, as AutoCAD 2021's `RemoveDataLink` leaves it. A link whose rows or columns
are all taken out goes with them, and its DATALINK, with what it owns, where its `DataLinkOption` is `Anonymous` and
nothing else of the file points at it (another table, a copy of this one), the dictionary that holds it aside. The library
reads no spreadsheet: the cells keep what they hold, and `UpdateDataLink` is not there.

Writing a link is held for the checks in AutoCAD ([Checking against AutoCAD](dxf-autocad-checks.md), points 73 to 78):
`SetDataLink` is not there, and a `DxfDataLink` is neither made in code nor has its groups set. The DXF reference
describes no DATALINK and ezdxf keeps one as it reads it; the groups read are those LibreDWG gives, which have no 70 and
put the link's custom data between `305 CUSTOMDATA`, `1 DATAMAP_BEGIN` and `309 DATAMAP_END`. The run 340, 93, 94, 96 is
the one LibreDWG gives a cell of a TABLECONTENT, whose 92 says it has linked data; that a cell of an ACAD_TABLE names
its link so, after the 92 of R2007, is the library's guess. An `UpdateOption` of 1 179 649, as LibreDWG's example gives
it, is SkipFormat (0x20000) and AllowSourceUpdate (0x100000), which make 1 179 648, and a 0x1 no flag of AutoCAD's
`UpdateOption` explains. The breaking of a table into parts (`BreakEnabled` and the rest) is not there either: its
groups are in no reference.

### Layouts and viewports

The layouts are `db.Layouts`, `DxfLayout` objects in the order of their tabs: `LayoutName` (set, the layout dictionary's
entry renamed with it; Model keeps its name), `TabOrder`, `TabSelected` (the layout the drawing opens on), `Limits`,
`Extents` (empty for a layout never worked out, as ezdxf's 1E+20 above -1E+20 says), `BlockTableRecord` and
`GetViewports()`, with the plot settings read as AutoCAD's `PlotSettings` has them: a layout of A3 on its side has
`PlotPaperSize` (420, 297) in millimetres. A viewport is a `DxfViewport`: one 200 by 150 about (150, 100) on the sheet,
showing model space about (5 000, 3 000) with a `ViewHeight` of 7 500, has `CustomScale` 0.02 and `StandardScale`
`Scale1To50`; its view, snap, grid, user coordinate system, plot style, shading and lighting are read and set, and its layers
frozen and thawed by `FreezeLayersInViewport` and `ThawLayersInViewport` (331, by handle). A viewport of a file before R2000
keeps its view in its extended data MVIEW, read and set there, its twist and its snap angle in radians as AutoCAD 2021
reads and writes them there, a snap of 30 degrees as 0.5235987755982988 (degrees from R2000); what those files have not, as the user coordinate system,
reads as AutoCAD's default and is refused when set. A viewport made in code goes to a layout of paper space, given the next
`Number` of the layout; `On = true` turns it on. `Number` is the id the file writes (69), on or off, where AutoCAD 2021's
gives -1 for a viewport turned off or of a layout not shown. The viewport 1 of a layout of paper space, which shows the sheet itself, is
not erased, as AutoCAD keeps it; erasing another takes the overrides the layers keep for it out of their extension
dictionaries, and a layout whose last active viewport (331) it was names its viewport 1 instead, as AutoCAD 2021 does.

A layout is made as AutoCAD makes one, `var plan = new DxfLayout { LayoutName = "Plan 1-50" }`, then
`plan.AddToLayoutDictionary(db, null)`: a LAYOUT in the layout dictionary on the tab after the last, with the plot
settings of a sheet of A3 on its side and no plotter (`none_device`, `ISO_A3_(420.00_x_297.00_MM)`, margins 7.5 at the
sides and 20 above and below, 1:1), its limits the sheet about its plotted area, (-7.5, -20) to (412.5, 277), and the block
`*Paper_Space1`, the first of `*Paper_Space0`, `*Paper_Space1` and on the drawing has not, whose BLOCK_RECORD names the
layout; a block of that name no layout has is taken where it is given. `plan.Initialize()` gives it the viewport 1 that
shows its sheet, about the middle of its limits, (202.5, 128.5), 1.1 times their size, 462 by 326.7, at 1:1, as ezdxf
makes a layout's main viewport, ahead of the viewports the layout has, those stacked on (68 of 1 or more) moved up one and
one that is off left so, AutoCAD and BricsCAD
taking the first VIEWPORT for the sheet; a layout that has its viewport 1 is given that one. `db.GetViewports(true)` gives the
viewports of the layout the drawing shows where it shows paper space (`TileMode` false), and none in model space, whose
tiled viewports are records of the VPORT table. `PlotWireframe` and `PlotAsRaster` say how a viewport is plotted, by its
`ShadePlot`, the visual style it names (333) or is shown in (348) and its render mode (281): one shaded wireframe or shown
in 2D is plotted as wires, one rendered or shown Realistic as a raster image, one shaded hidden as neither. A viewport of
R2007 or later is lit by a sun, `viewport.SetSun(new DxfSun { DateTime = new DateTime(2026, 6, 21, 15, 0, 0) })`: a
SUN owned by the viewport (361), the one it had erased where the viewport owned it, the class SUN listed where the
drawing lists none, `viewport.Sun` read back, its `IsOn`, `SunColor`, `Intensity`, `DateTime` (the Julian day 2461213
and 54 000 seconds past midnight), `IsDaylightSavingsOn`, `ShadowsOn`, `ShadowType`, `ShadowMapSize` and
`ShadowMapSoftness` read and set, a new sun's ezdxf's (AutoCAD's own not confirmed). Where the sun stands in the sky is AutoCAD's to work out from the
drawing's location, and a viewport's `ToneOperatorParameters` and `Transparent` are not there: neither the DXF reference
nor ezdxf gives where a file keeps them.

### Editing rules

**A property given the value it has writes nothing.** Every setter of an entity, a table record, the header and the
dictionaries compares first: given the value it has, it changes nothing, to the byte, and never throws, whatever the file
holds. A change writes the record it is about anew in the file's own style, or, for a vertex of a long polyline, the knots of a
spline or a loop of a hatch, the run of groups it is about, and every other record keeps its bytes.

**Appending.** `AppendEntity` writes an entity made in code at the end of the ENTITIES section, or before the ENDBLK of a
block, with a handle above every handle of the file and every handle something points at, `$HANDSEED` set above it, the
block's BLOCK_RECORD as its owner from R13 on and group 67 in paper space, the blocks of its other layouts
(`*Paper_Space0` and on) among it. Its layer is added to the layer table where it is
not listed, in the colour 7 with CONTINUOUS, and the applications of its extended data to the table of applications. A file
of R12 takes no subclass markers and has no SPLINE, ELLIPSE, RAY, XLINE, LWPOLYLINE, HATCH, LEADER or MULTILEADER, nor
lineweights, true colours, or a linetype scale or visibility of an entity's own: those are refused, by `AppendEntity` and by
the setters alike, as an ELLIPSE is: "A ELLIPSE cannot be added to a DXF of version AC1009, which has no such type: R13
(AC1012) brought it." Nor has R12 an MTEXT, which is written there as one TEXT for each paragraph (`\P`, a column break `\N`
taken as one, blank paragraphs at its end left out): its words without their formatting, as `DxfMText.Text` reads them, its
height, turn and plane, the lines spaced as `LineSpaceDistance` says and held at the left, the middle or the right as the
MTEXT is held, in its text style where the drawing has it or is given it by name with the copy, else Standard. The TEXTs are
appended in its place and the MTEXT stays in no file: an MTEXT of two lines 2.5 high held at its top left at (10, 20) comes
out as two TEXTs on the baselines 17.5 and 13.33 high. A paragraph is not wrapped at the MTEXT's width, as AutoCAD's SAVEAS to
R12 wraps it by the letters of its font, which the library does not read.

**Walking while editing.** A block is walked over the entities it holds when the walk begins, so that
`foreach (DxfEntity e in db.ModelSpace) e.Erase();` erases them all, as it does in AutoCAD: an entity erased on the way is
passed over, one appended on the way is not reached, and nothing throws.

**Erasing.** `Erase()` takes an entity out with every record of it, a POLYLINE its vertices and end and an INSERT its
attributes, and its handle is not given again; when the file is saved, a GROUP no longer lists it, and one it leaves empty
goes, a list of reactors, a SORTENTSTABLE and a dictionary no longer hold it, the objects it owned go with it, and any other
pointer at it, as a table cell's at an erased attribute definition or an XRECORD's at an erased entity, stays where it stands,
the saving naming up to ten of them in the log (Warn) once, not again at each saving after. A table record and a block are
erased only where nothing uses them, as each says above.

**Moving.** `TransformBy` moves, turns, scales or mirrors an entity as AutoCAD's does: its record is written anew with the
new geometry, its look kept, and its extended data moved with it in the same writing, a POLYLINE that of its vertices and an
INSERT that of its attributes; one with none of the groups `XDataTransformBy` moves keeps the bytes of its extended data.
`GetTransformedCopy` moves a copy. Each class refuses what it cannot hold, with an `ArgumentException` and nothing written,
and an entity held in the system of its normal (a circle, an arc, a polyline of the plane, a text, an INSERT, a SOLID, a
hatch, a dimension) refuses a transformation that flattens that plane:

| Class | Takes | Refuses |
|---|---|---|
| `DxfPolyline3d`, `DxfSpline`, `DxfFace`, the meshes | any | nothing |
| `DxfLine`, `DxfDBPoint`, `DxfRay`, `DxfXline` | any | one that takes the normal of a line or a point, or the direction of a ray or an xline, to no length |
| `DxfSolid` | any, its corners held in the system of its new normal | a flattening |
| `DxfCircle`, `DxfArc` | a scale alike in their plane, a turn, a mirror | a scale more one way than the other, or a shear: a circle of radius 5 scaled by 2 along x would be an ellipse |
| `DxfEllipse` | any that leaves it an ellipse | a flattening |
| `DxfPolyline`, `DxfPolyline2d` | any, where they have no arcs; with arcs, a scale alike in their plane | with arcs, an uneven scale or a shear, which would make the arcs ellipses |
| `DxfDBText`, `DxfAttributeReference`, `DxfAttributeDefinition` | any, a shear given to the text as its lean; a mirror turns its normal over | a flattening; an attribute of several lines, a shear |
| `DxfMText` | a turn, a mirror, a scale even or not, the columns with it | a shear, which it cannot lean by, and a flattening |
| `DxfBlockReference`, `DxfMInsertBlock` | a turn, a mirror and an uneven scale, taken into `ScaleFactors`; the attributes with it | a shear, which its axes cannot hold, and a flattening |
| `DxfHatch` | any: under an uneven scale or a shear its arcs become ellipse edges, as ezdxf makes them; seed points and gradient moved | a flattening |
| `DxfLeader` | any, its offsets with it | a flattening |
| `DxfMLeader` | a turn, a mirror, a scale alike | an uneven scale or a shear, as AutoCAD refuses it |
| the dimensions | any, the points moved as ezdxf's `transform()` moves them, the block with them where it is the dimension's alone and placed at no point of its own | a flattening |
| `DxfTable` | a move, a turn, a tilt, a mirror and a scale alike in every direction, its sizes scaled with it, the block with it where no other entity names it | a shear or a scale unlike along its axes, which would make its cells other than the rectangles AutoCAD lays it out in |
| `DxfViewport` | a move and a scale alike, on its sheet, its view kept | a turn, a mirror or a stretch |
| `DxfUnknownEntity` | nothing | everything, with `NotSupportedException`: its geometry is kept as the file has it |

**Exploding.** `Explode()` is every entity's, as AutoCAD's is, and gives new entities in no file, to be appended. Each class
breaks as AutoCAD breaks it:

| Class | Breaks into |
|---|---|
| `DxfBlockReference` | the entities of its block placed where it puts them, one level down: the block's ATTDEFs among them, each a `DxfAttributeDefinition` placed as the rest are, and none of the INSERT's ATTRIBs, whose values stay in its `AttributeCollection` |
| `DxfMInsertBlock` | the block in its one cell, as an INSERT; a MINSERT of more than one cell is refused with `NotSupportedException`, as AutoCAD 2021's `Explode` refused one of a circle in 2 rows by 3 columns (eNotApplicable) |
| `DxfPolyline`, `DxfPolyline2d` | a LINE for each straight segment and an ARC for each bulged one, in its order, each with its look and thickness, its widths left out as AutoCAD leaves them out, and a segment of no length too: a closed square of side 10 with one side bulged by 1 gives three lines and a half circle. Each LINE and ARC is on the polyline's normal, as AutoCAD gives it, so that their walls stand where its wall stood: a polyline on (0, 0, -1) at elevation 5 with a thickness of 2 gives walls from z = -5 down to -7. A POLYLINE fitted to a curve or a spline breaks along the vertices it is drawn through |
| `DxfPolyline3d` | a LINE for each segment between two vertices it is drawn through, the closing one among them: the polyline through (0, 0, 0), (1, 2, 3) and (4, 5, 6) gives two lines |
| `DxfMText` | a TEXT for each paragraph, as it is written into a drawing of R12 (see Appending): B-12 and M16 on two lines, 2.5 high, held at the top left at (10, 20), give two TEXTs on the baselines 17.5 and 13.33 high; a paragraph is not wrapped at its width, and a part of it in another font or height is not given a TEXT of its own, as AutoCAD gives it |
| the dimensions | the entities of the block they are drawn with (`*D7`), placed where the dimension draws them, as ezdxf's `virtual_entities()` places them, but the POINTs on Defpoints that mark where it measures, which AutoCAD 2021's `Explode()` leaves out: the picture AutoCAD drew last, so a point set since is not in it |
| `DxfTable` | the entities of its block (`*T12`), the lines of its cells and their words, placed where the table draws them: the picture AutoCAD drew last, so a cell's text set since is not in it |
| `DxfPolyFaceMesh` | a 3DFACE for each face, in the order of the file, its corners the points its numbers name and the edge from a corner whose number is below nought hidden; a face of three corners ends on its third, and the edge from its third corner back to its first, the 3DFACE's fourth, is hidden with the third. A face record's own layer and colour are not the face's, as AutoCAD 2021's `Explode` gives a face of a record on a layer of its own, and of colour 1, the mesh's layer and BYLAYER. Its corners are its numbers up to the first of nought, as the reader takes them: a face of fewer than three corners so, (1, 2, 0, 4) among them, or one naming a point the mesh has not, is left out. A pyramid of a square base gives five faces |
| `DxfPolygonMesh` | a 3DFACE for each cell of its grid, row after row, from its corner (m, n) to (m + 1, n), (m + 1, n + 1) and (m, n + 1), closed where the mesh is closed: a grid of 3 by 4 gives 6 faces, closed both ways 12. A smoothed mesh breaks as AutoCAD 2021's `Explode` breaks one: its points of flag 8, one more than `MSurfaceDensity` and `NSurfaceDensity` along an open direction, read as a grid of the densities themselves, row after row from the first, each cell from (m, n) to (m, n + 1), (m + 1, n + 1) and (m + 1, n): the 7 by 7 points of a mesh smoothed at 6 and 6 give 25 faces, the first of the points 0, 1, 7 and 6, as AutoCAD's do; where those points fill neither grid, it breaks into the cells of its frame |
| `DxfLeader` | as AutoCAD 2021's `Explode()` gives it: where it has an arrow, its arrowhead first, at the first vertex pointing away from the second, `Dimasz` times `Dimscale` long: an INSERT of its `Dimldrblk` block, or AutoCAD's closed filled arrow as a SOLID a third as wide as long, its corners the one on the right of the leader, the tip and the one on its left; then, straight, one LWPOLYLINE through its vertices (a LINE between each two where they stand off the plane of its normal, which a polyline cannot hold), or splined, one SPLINE through them, of degree 3, leaving along its first segment and reaching along its last, its knots the lengths of the chords, and its fit data the vertices it runs through and those two directions, as AutoCAD's has three fit points; the line starts at the arrow's back. Each in the leader's own look, its colour, true colour, linetype and lineweight, whatever DIMCLRD, DIMLTYPE and DIMLWD say. The leader from (0, 0, 0) through (10, 10, 0) to (20, 10, 0) with an arrow 2.5 long gives a SOLID and a polyline from (1.77, 1.77, 0); the vertex AutoCAD adds where the words of its annotation end is AutoCAD's to work out from its font and is not among them |
| `DxfMLeader` | as AutoCAD 2021's `Explode()` gives it: its words as an MTEXT or its block as an INSERT with its attributes, then, leader by leader, its dogleg as a LINE from the last point of the leader, and line by line an LWPOLYLINE to the last point of the leader, or one SPLINE for a splined line, its knots the lengths of the chords, its fit data the points it runs through and its directions at the ends, then the arrowhead at the first vertex, where the first segment is more than twice as long as the arrow; each line starts at the back of its arrow, and the closed filled arrow is a SOLID a third as wide as long, its corners the one on the right, the tip and the one on the left. A colour BYBLOCK is the multileader's own; the arrowheads have its own lineweight. The words on the right of two leader lines with a dogleg of 8 give an MTEXT, the dogleg, and a polyline and a SOLID for each line. The lines AutoCAD draws under or over the words, as long as the words are wide, are AutoCAD's to work out and are not among them. A copy in no file (`Clone()`) finds its blocks, linetype and text style in the drawing it was copied from and gives what its original gives, the values of its attributes among it; one whose block that drawing has erased since is refused with `InvalidOperationException`, the handle of the block said |
| an MLINE (`DxfUnknownEntity`) | from its record and its MLINESTYLE, for an open one its start caps, a LINE for each element along each segment, the miter lines at each joint where the style draws them, then its end caps: a line across the ends as two LINEs from the outer elements to the point halfway between them, an outer arc as one half circle, or two quarter ARCs where the outer elements' colours differ, and inner arcs joining each pair of inner elements. Each LINE has its element's colour and linetype. An element broken by MLEDIT gives a LINE for each piece left, caps the MLINE leaves off are not drawn, and the fill is left out. Standard through four vertices gives six lines |
| a MESH (`DxfUnknownEntity`) not smoothed | a 3DFACE for each face, in the order of its list of faces, with the MESH's look and every edge drawn: a face of four corners a 3DFACE of them, a triangle one ending on its third corner, as ezdxf's `MeshBuilder` renders them; a cube 10 by 10 by 10 of eight vertices and six faces gives six 3DFACEs. A face of more than four corners breaks into a strip of triangles, as AutoCAD 2021 breaks one: from two corners on, the rest taken by turns from the far end and the near one, each triangle after the first the other way round, from its first two corners, or from its last two back the other way where it faces into a closed MESH, whose faces AutoCAD turns to face out: the corners 0 to 4 alone, or in an open MESH, give (0, 1, 4), (2, 4, 1) and (4, 2, 3), as the base of a pyramid facing up into it (4, 3, 0), (2, 0, 3) and (0, 2, 1); 0 to 5 alone (0, 1, 5), (2, 5, 1), (5, 2, 4) and (3, 4, 2), as such a base (5, 4, 0), (3, 0, 4), (0, 3, 1) and (2, 1, 3). A face of four corners or three that faces into a closed MESH is turned too, its corners read from its last back, as AutoCAD 2021 explodes one: the square base (0, 1, 2, 3) of a pyramid facing up into it gives the 3DFACE of (3, 2, 1, 0), a side (0, 3, 4) facing in (4, 3, 0); every other face is given as the file holds it. A face given a colour of its own keeps it on its 3DFACEs. A MESH smoothed (level 1 or more), and one that gives a face a material or a transparency of its own, or an edge or a vertex anything of its own, are refused with `NotSupportedException`, saying which |
| `DxfHatch` of a pattern | the dashes of each line of its pattern, as the HATCH holds them, cut by the loops its style fills by the even-odd rule, each a LINE, a dot a LINE of no length, laid from the base point of its line, as ezdxf's `hatch_entity` draws them; an arc, an ellipse and a polyline's bulge cut exactly, a spline within each sixteenth of a span of its knots. A dot that falls on a loop where its line crosses it is kept, and a line that runs along a straight edge gives nothing there, as AutoCAD 2021 gives nothing: DOTS 1.5875 apart in a square 30 by 20 from its corner give 228 dots, none on its bottom edge. In the outer style the loops flagged external or outermost are those it is cut by, as AutoCAD and ezdxf take them, ignoring islands those flagged external; where none is flagged, by how deep each lies. A square 30 by 20 of ANSI31 gives 11 lines; round a circle of radius 5 about its middle in the normal style, 14, the three lines that cross the circle broken in two. A pattern that would give more than 2 000 000 parts, or cross its loops more than 32 000 000 times (half a gigabyte of crossings), is refused before anything is made, and so is one whose loops lie more than 2^52 of its spacings or dash lengths from its base point |

The library explodes exactly these classes. Every other entity refuses with `NotSupportedException`: those AutoCAD does not
break, a LINE or a CIRCLE, as AutoCAD answers eNotApplicable for them, and some AutoCAD does break, listed under [What the
model does not do](#what-the-model-does-not-do), among them a HATCH filled solid or with a gradient, a MESH smoothed, and a
REGION, a 3DSOLID and a BODY, which AutoCAD breaks with its solid modeler, each refused with its reason. A dimension or a table whose
drawing has no block of the name it is drawn by, and an MLINE whose drawing has no MLINESTYLE of its handle or name, throw
`InvalidOperationException`.

An INSERT inside the block that cannot hold the placing is exploded in its turn; one of a block already being exploded further
out is refused with an `ArgumentException`; and what exploding an INSERT or a MINSERT makes is counted first and held to the
reader's 2 000 000 (`DxfReadOptions.MaxPlacedEntities`), past which an `InvalidOperationException` is thrown and nothing is
given: a block of 100 INSERTs turned by 20 degrees, of a block of 100 INSERTs turned by 10 degrees, of a block of 100 lines,
inserted at twice its width, explodes into 1 000 000 lines; with 200 lines it is refused before anything is made, in 0.8 s
the first time in a process and 0.26 s after. An INSERT
that comes out of it places the block of the drawing it came from, so that it explodes in its turn, with what that block now
holds. A HATCH, a LEADER, a MULTILEADER and an ACAD_TABLE of the block are placed as the rest are; a MULTILEADER or an
ACAD_TABLE under an INSERT that scales it unlike along its axes, or shears it, is left out, as it cannot hold that placing,
while one scaled alike or mirrored is kept.

`ExplodeToOwnerSpace()`, a member of `DxfBlockReference` as of AutoCAD's `BlockReference`, appends what `Explode()` gives to
the block the INSERT is in, in the order of the block, and keeps the INSERT with its ATTRIBs, as AutoCAD does:
`insert.ExplodeToOwnerSpace(); insert.Erase();` explodes it as AutoCAD's EXPLODE does, the attributes turned back into their
definitions. Where the drawing cannot hold a part, as R12 the ELLIPSE a circle comes out as under a scale unlike along its
axes, the parts appended before it are erased again. A MINSERT gives its first cell alone, as AutoCAD 2021's
`ExplodeToOwnerSpace` gave one circle of a MINSERT of a circle in 2 rows by 3 columns; its `GeometryExtentsBestFit` still
measures every cell.

**Copying.** `Clone()` copies any entity as it is, as AutoCAD's `Clone` does, a `DxfUnknownEntity` among them, an INSERT with
its attributes, and `GetTransformedCopy` copies and moves one; a copy is of the class of its original, a vertex a vertex, in
no file until it is appended. An erased entity refuses to be copied. A copy appended to the drawing it came from points at what
its original points at, and shares the block that draws it, a dimension's `*D`, a table's `*T`, an INSERT's `*U`. A copy of a
multileader keeps the attribute definitions its values are given for, and appended to another drawing gives each value to
the definition of the same tag in the block of the same name there, leaving it out where there is none; appended back to its
own drawing, it leaves out the value of a definition erased since, as ezdxf would otherwise find the definition missing. The
original keeps its handle of the erased definition, as AutoCAD leaves it.

Appended to another drawing, a copy's pointers are taken to the objects of the same name there, as AutoCAD's deep clone
between drawings maps them: a material (347), a plot style (390) or a visual style (348) by the names of its dictionary and its
entry, a record of a table by its name; a pointer to an object the drawing has no namesake of is left out, so that the
drawing's default holds, and a handle of extended data (1005) is kept in its place as null (0), as AutoCAD 2021's
`CopyObjects` keeps it, so that the groups after it keep theirs (`WblockCloneObjects` names the copy where what it names is
copied with it). The LINE of
the material Steel_mat at 2F copied into a drawing whose Steel_mat is 57 points at 57; into one with no Steel_mat it has no
material. A copy brings what it names that the drawing has not, and uses what the drawing has of the same name:

- the records of the tables, its layer, linetype, text style and dimension style, a dimension style's arrow blocks among
  them, by name, and the overrides of its dimension style (ACAD DSTYLE) naming the records of their names there;
- a block AutoCAD names itself it draws by, a dimension's `*D`, a table's `*T`, an INSERT's `*U`, under the first name of
  its letter the drawing has not (`*D2` beside a `*D1`), with every entity of it and any block of a name the drawing has not
  that it inserts;
- an INSERT's named block, under its name, with the blocks it inserts that the drawing has not and its description, units,
  explodability and scaling, as AutoCAD's `WblockCloneObjects` brings it; where the drawing has a block of the name, the
  copy places the drawing's own;
- every block it points to by a hard pointer so too, as AutoCAD's deep clone follows its pointers to them: a MULTILEADER's
  block and arrow blocks, the multileader of GKTAG copied alone into a drawing with no GKTAG bringing GKTAG and giving NUM 42
  to the copy of its attribute; and the block of an ACAD_TABLE's cell, which the table copied alone brings though its `*T`
  does not insert it.

What the entities of a block copied point to among themselves, a LEADER to its MTEXT or a HATCH to its boundary, is kept
between their copies whichever comes first in the block, each listed among the reactors of what it points to, as AutoCAD
translates its map of ids once every object is copied. A MULTILEADER of a block copied places the copy of its block, and draws
its arrows by the copies of theirs, where those blocks are copied with it, though after it: a block W holding a multileader
of the block GKTAG and an INSERT of GKTAG, copied into another drawing, brings GKTAG, and W's multileader places that GKTAG
with the value of its attribute. So does every other pointer to a block copied after the entity that holds it, whichever
of the two comes first in the block: an ACAD_TABLE points to the copies of its `*T` (343) and of the blocks of its cells
(340), and a LEADER or a dimension whose style is overridden with an arrow block (DIMLDRBLK, DIMBLK, DIMBLK1, DIMBLK2) names
the copy of that block, by its handle, or by its name in a drawing of R12 (1070 5, 1000 BOLT). A copy of an INSERT places its block as the drawing it was copied from
has it now, edits and all, keeps to it when it is renamed there, and places nothing once it is erased.

`db.WblockCloneObjects(entities, owner)` copies entities of a drawing together into a block of another drawing, or of its
own, as AutoCAD's `WblockCloneObjects` copies the objects of ids, and gives the copies in the order of the entities: each
copied as `Clone()` and `AppendEntity` copy one, and what one of them points to among the others pointing to the copy of that
other. A HATCH copied with every object its loops follow follows their copies and stays associative; a LEADER copied with its
MTEXT, TOLERANCE or INSERT points to its copy, and a handle of extended data (1005) naming one of the others names its
copy: the LINE whose data names a CIRCLE, copied with it, names the CIRCLE's copy. A LEADER copied alone into another
drawing brings its annotation with it, which it points at by a hard pointer (340), as AutoCAD 2021's `WblockCloneObjects`
brings it; copied alone into its own drawing it points to nothing (`AnnoType` `NoAnnotation`), where `Clone()` and
`AppendEntity` keep its pointer. A HATCH copied without every object it follows follows none. No GROUP is copied, as
AutoCAD 2021's `WblockCloneObjects` and COPYCLIP copy none; `db.Insert` copies a drawing's groups with it, each all of
whose entities come into model space under its own name, or that name and `_1`, `_2` and on where the dictionary has it, an
anonymous one as the next `*A1`. Every copy is checked before any is added, so that a set the drawing cannot hold, a SPLINE for a drawing of
R12, changes nothing. A set whose blocks would come to insert themselves once written is refused whole too, with an
`ArgumentException`: a drawing whose block P holds an INSERT of M, a block it has not, as a drawing read from a file may,
refuses an INSERT of another drawing's M that inserts P, which would bring an M placing P and a P placing that M without
end. So are blocks of the other drawing that insert one another, and blocks brought that would come back to the block the
copies are added to; and a copy appended alone by `AppendEntity` is refused alike. A block AutoCAD names itself that a copy
brings takes no name an INSERT, a dimension or a table of this drawing gives of a block it has not, nor one an INSERT in a
block the set brings gives of a block neither drawing has: a DIMENSION here naming a `*D1` the drawing has not leaves the
copy of another's `*D1` to come as `*D2`, rather than drawing by it. An INSERT in a block brought that names a block its
drawing has not is copied as it is, naming it, and the set says how many there were, and their names, in one Debug line of
`GeometryHelperLog`. A rectangle of four LINEs with a HATCH that follows them, copied together into another drawing, comes
out as four LINEs and a HATCH that follows those four, each LINE listing the HATCH among its reactors.

What a copy by `Clone()` and `AppendEntity` leaves behind: a copy of an associative HATCH, in its own drawing or another,
leaves its boundary objects, each path counting the pointers it keeps (97 of 0 where it keeps none), and is no longer
associative (71 of 0), as AutoCAD's COPY leaves it; a LEADER appended to another drawing points to nothing (`AnnoType`
`NoAnnotation`), its annotation not coming with it; a copy of an ACAD_TABLE for another drawing keeps the values
its block cells give the attributes of a block that comes with it, each pointer (331) naming the copy of its attribute
definition, and loses those of a block the drawing has of its own, each pointer left out with its value (300) and the cell's
count (179) made to agree; a copy of the first column of an MTEXT of linked columns holds the words of them all and links to
none.

**Versions.** An entity of a file of R12, or a copy of one (`GetTransformedCopy`, `GetOffsetCurves`, `GetSplitCurves`),
appended to a drawing of R13 or later is written in the forms AutoCAD writes there: its subclass markers in AutoCAD's order,
the marker AcDbEntity, its owner and a handle of its own. One of R13 or later appended to a drawing of R12 is written in the
forms of R12, as AutoCAD saves a drawing as R12: each record keeps the groups R12 has for its type, each read from the part of
the record it belongs to, so that the centred ATTRIB whose AcDbAttribute part writes 71 and 72 again keeps its justification,
and loses its markers, owner, lineweight, material and plot style, and an object embedded after its 101, as a multi-line
attribute's, which R12 reads as one line; an MTEXT is written as TEXTs, as Appending says. A DIMENSION is written in the forms
of R12 into a drawing of R12, its block with it, where every entity of the block has a form of R12, an MTEXT of the block
written as the TEXTs of its lines, so that a dimension of R2018 whose picture holds its text in an MTEXT goes into R12 with
its picture. A text style an entity copied between drawings of R12 and of R13 or later names is copied by name, as a layer is,
and the dimension style of a dimension, a leader or a tolerance copied into a drawing of R12 is written in the form of R12:
its name, flags and the variables R12 has, its texts (3, 4) and its arrow blocks by name (5, 6, 7) after its flags, a block
the drawing has not and a text it has none of written empty, as AutoCAD 2021 asks for all five ("Omitted group 5"), its text
style and the variables of later versions left out, said in the log. A dimension copied into a drawing before R2010 is
written without its version (280 of AcDbDimension), which R2010 brought and AutoCAD 2021 refuses in a DIMENSION of R2000
("Unexpected DXF group code: 280").
A HATCH copied into a drawing of another version is written as that version writes it, the count of fit points of a spline
edge as the version has it and a gradient dropped before R2004, as AutoCAD's SAVEAS drops it.

What a drawing of an older version has no room for is left out of what is copied into it, as AutoCAD's SAVEAS to that version
leaves it out, and said in the log at Debug: an override of a dimension style (ACAD DSTYLE) of a variable the version has not,
as DIMLTYPE and DIMFXLEN of R2007 on a dimension copied or made in code for a drawing of R2000, or DIMDEC of R2000 for one of
R12; and a group of a dimension style or a layer copied by name with an entity that the version has not, as a DIMSTYLE's
DIMFXLEN (49), DIMJOGANG (50), DIMTFILL (69), DIMTFILLCLR (its second 70), DIMFXLENON (290), linetypes (345 to 347) and
DIMTXTDIRECTION (294), and a LAYER's true colour and colour book (420, 430) of R2004 and material (347, 348) of R2007, in a
drawing of R2000; a LAYER's plotting, lineweight and plot style (290, 370, 390) of R2000 go too in a drawing of R13 or R14,
the versions taken as ezdxf gives them. In a drawing of R12 an override of an arrow block
(DIMBLK, DIMBLK1, DIMBLK2) names the block by its name, as R12 writes it, where the drawing has a block of the name, and is left
out, said in the log, where it has none. A record set on a dimension in no drawing for a variable the version has not is
refused.

**Memory.** A copy in no file, and an entity made in code, holds its own records alone: one file holds every copy of a
drawing and keeps none of them, about 460 bytes a copy, so that the explode of an INSERT of 1 000 000 lines holds 440 MB.

### What the model does not do

- It draws nothing anew. The block that draws a dimension (`*D`) or a table (`*T`) is left as it was when a point, a text, a
  cell or a variable of its style changes, for AutoCAD to draw anew; until AutoCAD does, it shows the old picture. A
  dimension's `Measurement` is worked out from its points all the same.
- It has no fonts. A text's `GeometricExtents` is the point it stands at; an MTEXT's `ActualWidth` and `ActualHeight` are what
  the file says, nought where it says nothing; where the words of an MTEXT wrap, and the box of a fitted or aligned TEXT, are
  AutoCAD's to work out, and so is where it wraps an MTEXT exploded or written into R12 as TEXTs.
- It knows no pattern file. A pattern of AutoCAD's acad.pat the HATCH does not hold the lines of, ANSI31 given by
  `SetHatchPattern` among them, holds no lines (`NumberOfPatternDefinitions` 0), its name written for AutoCAD to take the
  lines from its own file, and `Explode()` and `GetHatchLinesData()` refuse it. A leader drawn anew (`EvaluateLeader`),
  where a multileader's leaders meet its words, and the size of those words in its `GeometricExtents` are not worked out: a
  multileader made in code starts its leader's dogleg at the base point of its content (10 of the context data), at the
  origin where its block is placed after its leader line is added.
- It gives no dynamic properties of a dynamic block: they are AutoCAD's engine's to work out. `ConvertToStaticBlock` copies
  what the INSERT draws as it stands; `ResetBlock` is not there. `db.Insert` of another drawing brings a dynamic block of it
  as a static one, without the data that makes it dynamic, which AutoCAD's own insert keeps.
- It resolves no external reference: `AttachXref` writes the block that names the drawing, and reading that drawing into it
  is AutoCAD's work when it opens this one, so an INSERT of it places nothing here and `XrefStatus` is never `Unloaded`.
- It does not explode every entity AutoCAD explodes. `Explode()` refuses with `NotSupportedException` a HATCH filled solid
  or with a gradient, whose parts AutoCAD's EXPLODE makes and the library does not work out; a MESH smoothed, which AutoCAD
  breaks into the 3DFACEs of the faces its smoothing makes, and one whose faces have a material or a transparency of their
  own; a REGION, a 3DSOLID, a BODY and a surface, which AutoCAD breaks with its solid modeler (ACIS);
  and every other type the library keeps as a `DxfUnknownEntity` but the MLINE and the MESH. It gives a dimension's and a
  table's parts as their last picture, not as their data now, and AutoCAD 2021 draws a table anew before it explodes it;
  and it leaves out the vertex AutoCAD adds to an exploded leader where the words of its annotation end, and the lines
  under or over a multileader's words, whose length the width of the words decides.
- It makes no drawing from nothing but `DxfWriter`'s, reads no DWG and no binary DXF, and saves a drawing in its own version
  only.
- It has none of AutoCAD's members that need AutoCAD running: `ObjectId`, transactions, documents, drawing to the screen
  (`Draw`, `Highlight`), grips and object snaps, overrules, events and undo.

What is still to be checked in AutoCAD itself is listed in [Checking against AutoCAD](dxf-autocad-checks.md), and a whole
session of editing, run against the library, is in [Editing a drawing, step by step](dxf-editing-example.md).

## Reading DXF

`DxfReader` reads a DXF file into the library's shapes: what `DxfWriter` writes, and what AutoCAD and the other CAD
programs write of the same kinds, ASCII files of R12 (AC1009) to R2018 (AC1032). An INSERT is read as the entities of its
block, placed where it puts them, and the blocks themselves are there too (see Blocks and INSERTs below); a TEXT and an MTEXT
are read as a `DxfText` (see Texts), and the layer table into `LayerTable` (see The layer table).

```csharp
using GeometryHelper.Dxf;

DxfDocument drawing = DxfReader.Read("plan.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (GeoPolygonArc2 outline in drawing.OfType<GeoPolygonArc2>()) { ... }   // the closed polylines with arcs
foreach (DxfShape entity in drawing.OnLayer("steel")) { ... }                 // shape, layer, colour, linetype, handle, groups

DxfDocument text = DxfReader.Parse(dxfText);                                   // or Read(stream)
```

A `DxfDocument` holds the `Shapes` in the order of the file, the `Layers` (those of the layer table in file order,
then any an entity is on that the table does not list, two names differing only in case being one, as in AutoCAD), the
`LayerTable` with the colour and state of each layer of the table, the
`Units` the header gives (`$INSUNITS`, as the `DxfUnits` enum numbers them: `Millimetres` is 4), the `Version`
(`$ACADVER`, `AC1015` for R2000), what was `Skipped`, counted by type in the order of the type names, and the `Warnings`.
Each `DxfShape` has its `Shape`, its `Type` as the file names it, its `Layer`, `Colour` (an AutoCAD colour index from 1
to 255, null by layer or by block), `TrueColour` (0xRRGGBB, group 420), `Linetype` (group 6, null by layer),
`Lineweight` (group 370 in hundredths of a millimetre, one of AutoCAD's 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53,
60, 70, 80, 90, 100, 106, 120, 140, 158, 200 and 211, null by layer, by block, default or any other number, which a
warning counts, and refused by the constructor), `Thickness` (group 39,
nought where none), `Handle` (null in a file of R12) and its `Groups`: every group of its record in the order of the
file, extended data (1000 to 1071) among them, each value the text the file prints, so that nothing of the entity is
lost. Of a POLYLINE they run on through its VERTEX records and its SEQEND. `BlockPath` and `InsertHandles` name the
blocks and the INSERTs an entity was placed through, empty for an entity of the drawing itself. `KeepGroups = false` leaves the groups out, for a large file: 100 000 entities (34 MB) are held in 41 MB
without them and 157 MB with them. A `DxfShape` can also be made in code, of a shape and its look, to be written by
`DxfWriter.Add(entity)`: `new DxfShape(plate, "steel", colour: 1)`.

What each entity is read as, shapes of space unless `PlanAs2D` is set and the entity lies on the floor: one in world
coordinates (POINT, LINE, RAY, XLINE, SPLINE, 3D POLYLINE) where every height of it is nought, whatever its extrusion; one
in the coordinates of its extrusion's plane (CIRCLE, ARC, LWPOLYLINE, 2D POLYLINE, SOLID, TRACE) and an ELLIPSE where the
extrusion is (0, 0, 1) or (0, 0, -1) exactly, or not given, and every height nought:

| Entity | Shape | With `PlanAs2D`, on the floor |
|---|---|---|
| POINT, LINE, RAY, XLINE | `GeoPoint3`, `GeoLine3`, `GeoRay3`, `GeoXLine3` | `GeoPoint2`, `GeoLine2`, `GeoRay2`, `GeoXLine2` |
| LWPOLYLINE, POLYLINE of the plane | `GeoPolyline3` or `GeoPolygon3` (closed); `GeoPolylineArc3` or `GeoPolygonArc3` where an edge has a bulge | `GeoPolyline2`, `GeoPolygon2`, `GeoPolylineArc2`, `GeoPolygonArc2` |
| 3D POLYLINE | `GeoPolyline3` or `GeoPolygon3` | `GeoPolyline2` or `GeoPolygon2` |
| polyface mesh, polygon mesh | `GeoSolid3` of its faces, closed or not as they are; fewer than four faces, each a `GeoFace3` | the same |
| CIRCLE, ARC | `GeoCircle3`, `GeoArc3` | `GeoCircle2`, `GeoArc2` |
| ELLIPSE, whole | `GeoEllipse3` | `GeoEllipse2` |
| ELLIPSE, a part | `GeoSpline3`, exactly the part | `GeoSpline2` |
| SPLINE | `GeoSpline3` of its degree, knots, weights and control points; one of fit points alone, the spline `Interpolate` passes through them | `GeoSpline2` |
| 3DFACE | `GeoTriangle3` of three corners; `GeoFace3` of four in one plane within the planar tolerance; two `GeoTriangle3` cut along the shorter diagonal where they are not | the same |
| SOLID, TRACE | `GeoPolygon3` of its corners in DXF's order 1, 2, 4, 3 | `GeoPolygon2` |
| TEXT, MTEXT | `DxfText`: where it stands, its normal, height, rotation and words | the same |
| LEADER | `GeoPolyline3` through its vertices; splined, the `GeoSpline3` `DxfLeader.Explode()` fits through them; its arrowhead and hook line not read | `GeoPolyline2`, `GeoSpline2` |
| MULTILEADER | a `GeoPolyline3` for each leader line, from where its arrow points to the end of its dogleg; splined, a `GeoSpline3` fitted through the same points; its words or block and arrowheads not read | `GeoPolyline2`, `GeoSpline2` |

A LEADER is read as its curve, as AutoCAD's `IntersectWith` meets a leader: its vertices are in world coordinates, a
vertex within the point tolerance of the one kept before it is left out, and a leader of fewer than two vertices apart has
no shape. Splined, it is the cubic through its vertices that leaves along its first segment and reaches along its last,
its parameter from 0 to 1, as ezdxf's `fit_points_to_cad_cv` fits it: through (0, 0, 0), (10, 10, 0) and (20, 10, 0) it
rises to y = 10.43. In a block, the spline is fitted through the vertices where the block has them and then placed, as
AutoCAD draws the leader in its block: an INSERT scaled 2 along x places the control points scaled so, not a spline
fitted through the scaled vertices. `Explode()` of such an INSERT gives the leader with its vertices placed, which is fitted
anew where it is drawn, so under a scale unlike along its axes the two splines part: by 2.21 for the leader through
(0, 0, 0), (10, 10, 0) and (20, 10, 0) under an INSERT scaled 3 along y. A MULTILEADER is read so, each leader line as the chain or the spline `DxfMLeader` sets
out (see [Leaders and multileaders](#leaders-and-multileaders)).

The coordinates of a CIRCLE, ARC, LWPOLYLINE, 2D POLYLINE, SOLID and TRACE are in the system of their extrusion's plane
and are taken to the world by the arbitrary axis algorithm, as `DxfWriter` writes them: the CIRCLE at (10, 20, 5) on
the extrusion (0, 0, -1) is centred at (-10, 20, -5), and an ARC from 0 to 90 degrees on that extrusion runs clockwise
seen from above, as AutoCAD draws a mirrored arc. On the floor that extrusion turns x over, exactly: with `PlanAs2D` the
ARC of radius 10 about the origin from 0 to 90 degrees is the clockwise `GeoArc2` from (-10, 0) to (0, 10), from pi - a
to pi - b, starting where the arc read in space starts; a polyline's bulges are negated and its vertices kept in their
order; a part of an ELLIPSE and a SPLINE keep the direction of the file; and the points are those ezdxf works out. A 2D POLYLINE stands at its
own elevation, its group 30, whatever heights its vertices give, or at the first vertex's where it gives none: ezdxf's
polyline at the elevation 50 is at z = 50, its vertices written at 0. A part of an ELLIPSE is a spline of rational
quadratic pieces of at most a quarter turn of its eccentric angle, each the stretched image of the arc
`GeoSpline2.FromArc` makes, so every point of it is on the ellipse to rounding: a quarter of the ellipse 60 by 30 is 3
control points, three quarters 7. A SPLINE whose knots are not clamped, as AutoCAD writes a closed one, is clamped by
knot insertion, the same curve over the span its knots give; one of fewer fit points than its degree asks for is read
at one degree less than its points, and a warning says so. A closed polyline of two vertices with an arc, a circle drawn
as two half circles, is a loop with its first half circle cut at its middle, so that it has the three vertices a loop
has: two half circles of radius 10 are a `GeoPolygonArc2` of area 314.159. A closed polyline that encloses no area, or
in space does not lie in one plane, is a chain that ends on its first vertex, in the plane as in space, and a warning
says so. Of a POLYLINE fitted with a spline the curve drawn is read and the frame left out. A point or vector group
missing a part reads it as nought, as the DXF reference has it: the extrusion 210 = 1, 230 = 1 is (0.7071, 0, 0.7071).

Every number is read as the file prints it, the double nearest it, on .NET Framework as on .NET 10, whose parsers do not
always agree: so a file `DxfWriter` wrote gives back every coordinate, radius, bulge, knot and weight it holds to the bit.
What the file holds in another form comes back to the last place or two: the angles of an ARC are degrees, an ELLIPSE
keeps the end of its major axis and the ratio of its radii, and a circle, arc or polyline in a tilted plane its
coordinates in that plane's system, so an arc of radius 40 on the normal (1, 2, 3) comes back within 1E-11 at its ends.
A chain or loop with arcs that lies flat at one height can carry a normal a hair off up, (1.6E-19, 0, 1) as the library
fits its plane; the writer writes that extrusion, and such a chain comes back to the rounding of that plane, within
1E-12 of its largest coordinate.

DIMENSION, HATCH, IMAGE, MESH, MLINE, REGION, 3DSOLID, BODY, ATTDEF, ATTRIB, VIEWPORT and any other type are
not read and are counted in `Skipped`. So is an entity with no shape to it, the reason said once for each type in a Debug
line of `GeometryHelperLog`: a LINE no longer than the point tolerance, a polyline or LEADER of fewer than two distinct vertices, a MULTILEADER with no leader line of two, a
CIRCLE or ARC of no radius, an ELLIPSE of no axis, a TEXT or MTEXT of no height, or with none given, a number that is not
a finite number, a SPLINE the library does not take (a periodic one among them whose knots cannot be clamped, which a
warning names too), a 3DFACE of fewer than three distinct corners. `DxfReadOptions.Tolerance` (the default tolerance unless set; the global
one is never read) says what has no shape: a LINE 0.0004 long is left out within the default tolerance and read within
1E-5. An entity whose reading throws, such as a SPLINE of fit points 1E200 apart, whose system cannot be solved, is left
out and counted too, the others read, with a warning and one Warn line of `GeometryHelperLog` for each type. A group of
the look that cannot be read (a colour of 1.0, a true colour past a 32-bit number) is left out, and the entity read
without it. A number the scaling of `ToMillimeters` takes past the largest double (1E307 metres) leaves its entity out,
counted as one with a number that is not finite, and such a thickness is left out alone, and so does a point the
system of a tilted extrusion takes there: the CIRCLE at y = -1.7E308 and a height of 1.7E308 on the extrusion
(0.6, 0, 0.8) would be at an x of 2.38E308. The entities of paper space are not read, and a warning counts them, a POLYLINE with its
vertices one. One Info line for each call says how many entities were read and how many skipped, by type.

A file that is not a DXF, or is cut short, is refused with a `FormatException` that names the line: a group code that
is not a whole number, a code with no value after it, no EOF, a section with no ENDSEC, no ENTITIES section. A binary
DXF is refused the same way. The text of a file of R2007 or later is UTF-8; that of an older one is UTF-8 where its bytes
are (a warning says so), else in the code page its header names (`$DWGCODEPAGE`, ANSI_1252 where it names none), read
the same on either runtime: Windows-1252 by a table of its own, since .NET 10 does not have it, so the layer
Wand–€Außen reads so on both. Any other code page (1250, 1251, 1253 to 1258, 932, 936, 949, 950) is read as the runtime
has it: .NET Framework has them all, but .NET (Core) has none without CodePagesEncodingProvider, which the library does not
bring, so there its text is read as Latin-1 and a warning names the code page and says to save the drawing as a DXF of
R2007 or later, whose text is UTF-8; a program that calls
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` once, from the package System.Text.Encoding.CodePages, has
them all, as the viewer does. A file of R2007 or later whose bytes are not all UTF-8 is read with the replacement
character U+FFFD for each that is not, and a warning says so. A file is read line by line, as AutoCAD reads it: in a
code page of letters of more than one byte (932, 936, 949, 950, 54936 and the like) a byte that opens a letter at the
end of a line does not take the line feed after it; and a file with letters beyond ASCII whose header names a code page
that does not read each byte of ASCII alone as itself, UTF-16 (1200, 1201), EBCDIC (37), ISO-2022 (50220 to 50229), HZ
(52936) or UTF-7, which read an escape, a shift or a sign as a change of state and leave it out, or one of the national
7-bit pages (20105 to 20108, 20261, 20269) or x-mac-thai (10021), which read a few signs of ASCII as other letters, is
refused with a `FormatException` that names the code page and no line; AutoCAD writes none of them. A name written with the
escapes of R2000 (`\U+00E9`) is read as its letters. A number is read from its first 19 digits and checked in integers,
the digits past 800 counting only as whether any is not nought: a number of 300 000 digits takes 2.2 ms on .NET 10.

The numbers are kept as the file has them unless `ToMillimeters` is set, which scales points, radii, elevations,
thicknesses and the lengths of axes by the unit of the header: a drawing in metres has its numbers times 1 000, one in
inches times 25.4. Angles, bulges, ratios, weights, knots and directions have no unit and are kept. A file with no unit,
or one the reference does not list, is not scaled, and a warning says so.

Reading 100 000 entities of eight kinds (34 MB) takes 0.54 s on .NET 10 and 0.90 s on .NET Framework 4.8 with the
groups, the document holding 157 MB, and 0.35 s and 0.58 s without them, holding 41 MB.

`DxfWriter.Add(entity)` writes an entity read back: its shape by the overload of `Add` for it, on its layer, with its
colour, true colour, linetype and lineweight after the layer, in the order AutoCAD writes them, and its thickness on the
LINE, POINT, CIRCLE, ARC and LWPOLYLINE that have one, each only where the entity has it. A drawing read, written and
read again gives the same shapes and the same look: every number to the bit but the angles of an arc and the axis of an
ellipse, which go through degrees and the ratio once more. A linetype the writer's table does not have is added to it
by name, with no pattern, so that AutoCAD opens the file and draws the line solid until the drawing is given the
linetype's pattern. A face of three or four corners with no hole, as a 3DFACE reads, is one 3DFACE again, the edges the
file hid hidden again; any other face and a body, a polyface mesh among them, is its triangles at the global tolerance.
What is not kept: an ARC runs counter-clockwise about its extrusion and no other way, so a clockwise `GeoArc2`, written
from its end to its start, comes back counter-clockwise over the same points, with `PlanAs2D` as without, and a circle or
an arc in space on an extrusion that points down is written about its normal turned up, so a mirrored arc comes back
running the other way over the same points too; a
linetype is written as the table spells it, so BYBLOCK comes back ByBlock; the entity's `Groups` are not written, and it
is given a handle of its own. A shape with no overload of `Add`, a `GeoSphere3` among them, which needs a chord
tolerance, is refused with an `ArgumentException` naming its type. The overloads that were there write the same bytes as
before.

### Texts

A TEXT and an MTEXT are each a `DxfText`, with `PlanAs2D` as without, a text having no twin of the plane:

```csharp
foreach (DxfText label in drawing.OfType<DxfText>()) { ... }   // Position, Normal, Height, Rotation, Value, IsMultiline, WidthFactor, Width
```

A TEXT stands in the system of its extrusion's plane, taken to the world as a CIRCLE is: at its first alignment point
(group 10) where it is aligned left on its baseline (72 and 73 nought, or not given) or aligned or fitted between its two
points (72 3 or 5 with 73 nought), which run from the first to the second, as ezdxf places them, so the aligned TEXT from
(0, 0) to (100, 0) stands at (0, 0, 0); and at its second (11) where it is aligned any other way, centred, right, in the
middle or by its top or bottom, as AutoCAD places it from that point. Its `Rotation` is its group 50 in degrees made
radians, counter-clockwise about its `Normal` from the x axis of the normal's own system, and its `WidthFactor` its group
41, 1 where it gives none. An MTEXT stands at its insertion point (10) in world
coordinates, whichever corner its attachment names; its rotation is that of its direction (11) in the system of its
extrusion, or its group 50 in degrees where it has no direction (degrees, as AutoCAD writes it, though the DXF reference
says radians), its `Width` the width its paragraphs wrap at (41, nought for none), and its `WidthFactor` 1. Every
`Rotation` is brought into a turn, from nought up to but not including 2 pi, on every path, a text made in code too: a TEXT
turned -90 degrees is turned 3 pi / 2. A TEXT at (3, 4, 5) on the extrusion (0, 0, -1) stands at (-3, 4, -5), its normal
(0, 0, -1): it reads mirrored seen from above.

`Value` holds the words. A TEXT's are as written; an MTEXT's with its formatting taken out: `\P` is a new paragraph, a
line feed in `Value`, `\N`, a new column, a space, `\~` a space, the codes that end in a semicolon (font, height, colour,
width, tracking, slant, stacking, paragraph) go with their arguments, `\L`, `\O` and `\K` go alone, `\\`, `\{` and `\}`
are the characters, a stack `\S1/2;` is 1/2, and the braces that group are taken off, so `{\fArial|b1;Bold}\Pline` is
`Bold` and `line` on two lines; a code with no semicolon after it is kept as written, as ezdxf keeps it, `\M+1A4A1` among
them. The pieces of 250 characters an MTEXT is written in (group 3) run on into its last (1). In both, the special
characters are read from the left as AutoCAD reads them: %%c, %%d and %%p, in either case, are the diameter, degree and
plus-minus signs, so `%%c20 at 45%%d` is `⌀20 at 45°`, %%% is a per cent sign, so `50%%%d` is `50%d`, and %%nnn, three
digits, the character of that code, `%%065` an A; %%u, %%o and any other are kept as written. A letter written as an
escape, `\U+00E9`, is the letter, as in a name, and so is the text `\U+0041` written in the words, which reads as an A: the
file cannot tell the two apart. A control character in AutoCAD's caret form is the character, `^J` a line feed, `^M` a
carriage return and `^I` a tab, and `^` followed by a space a caret. A TEXT is one line, so a line feed in its words,
however written, is a space, and a carriage return is left out of either kind: `a^Jb` is `a b` in a TEXT and `a` and `b`
on two lines in an MTEXT.

Placed by an INSERT, a text is carried by the x and y axes of its own frame: it stands where its point lands, faces the
normal those carried axes span, made unit, and is turned to the carried x axis in the system of that normal; its height is
stretched as the y axis is, and a TEXT's width factor as the x axis is against the y axis, an MTEXT's width as the x axis.
A text in a block scaled by 3 along x and 2 along y is twice as tall and, a TEXT, one and a half times as wide for its
height. A mirror turns the normal over, as AutoCAD writes the text of a mirrored block: a TEXT reading along x from (1, 0)
in a block placed with a scale of -1 along x stands at (-1, 0, 0), its normal (0, 0, -1) and its rotation nought, so that
it reads along -x, mirrored, as the block shows it. Where a placement shears a text, which neither entity can hold, the
reader keeps the length of the carried y axis as the height; ezdxf leans the text by an oblique angle and takes the height
square to the baseline, smaller by the cosine of that angle. Held against ezdxf 1.4.5 on 81 texts placed every way an
INSERT places them, every position is within 1E-9 of ezdxf's, every normal within 1E-12, and every rotation within 1E-9.
A text counts one entity towards `MaxPlacedEntities`, and one flattened by its INSERT onto a line is counted in `Skipped`.

`DxfWriter.Add(text, layer)` writes a `DxfText`, read or made with its constructor, as a TEXT, aligned left on its
baseline and turned by its group 50 in degrees, or an MTEXT, attached at its top left and turned by the direction it
reads along (11) in world coordinates, in the text style Standard, its letters beyond ASCII as escapes, a control character
in caret form (a tab `^I`) and a caret as `^` and a space, and an MTEXT's backslashes, braces and paragraphs as the codes
that keep them, no piece of it ending on a caret; `Add(entity)` writes one with its look. Read back, a text is the same:
its words, height, width factor, width and normal to the bit, its position too where the normal is (0, 0, 1) or
(0, 0, -1) or the text an MTEXT, a TEXT on a tilted normal to the rounding of its plane's system (within 1E-13 at 30 mm),
and its rotation to the last place, through degrees or a direction. What is not kept: the alignment and attachment of a
text read from a file, which is written where it stands, and a %%c, %%d, %%p, %%% or %%nnn or the text `\U+0041` in the
words, which comes back as the character it stands for. ezdxf recovers and audits such a file with no error.

### The layer table

`LayerTable` holds the layers of the layer table in its order, each a `DxfLayer`: its `Name`, its `Colour` (the AutoCAD
colour index, 1 to 255, whether the layer is on or off; null where the table gives none or one outside that range), its
`TrueColour` (group 420), `Linetype` (6) and `Lineweight` (370, null for the default -3), and whether it `IsOff` (the
table writes the colour of a layer turned off below nought), `IsFrozen` (flag 1 of group 70), `IsLocked` (flag 4) and
`IsPlotted` (group 290, true where the table does not say). A record whose name an earlier one has in another case is left
out, the first kept, as `Layers` keeps its spelling; a layer only an entity names is in `Layers` and not in the table.
`GetLayer(name)` finds a layer of the table by its name in either case, and gives null where the table has none:

```csharp
DxfLayer steel = drawing.GetLayer("STEEL");   // the layer Steel of the table: Colour 1, IsOff false, Linetype DASHED
```

### Blocks and INSERTs

The blocks of the file are in `Blocks`, in the order of the file, and each INSERT of the drawing is read as the entities
of its block, placed where the INSERT puts them, in the order of the block and where the INSERT stands among the
entities; a file that defines its blocks after its entities reads the same. `ExpandBlocks = false` leaves the INSERTs
out, counted in `Skipped`, and reads the blocks all the same.

```csharp
DxfDocument drawing = DxfReader.Read("frame.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (DxfShape entity in drawing.Shapes) { ... }   // the bolts of the block BOLT placed, each with BlockPath "BOLT"
foreach (DxfBlock block in drawing.Blocks) { ... }        // Name, BasePoint, Flags and its own Entities, unplaced
```

An INSERT places its block as AutoCAD does: the block's base point taken to the origin, the scales along x, y and z
(groups 41, 42 and 43, each 1 where not given), the turn about z (50, degrees, exact for a whole number of quarter turns),
the shift to the insertion point (10, 20, 30, in the system of the INSERT's extrusion), then that system (210). A
MINSERT's array of 70 columns by 71 rows is placed row after row, each cell shifted by its column times the column
spacing (44) and its row times the row spacing (45) along the turned axes, not scaled, as ezdxf places it; cells a
spacing of nought shifts alike are placed once, as ezdxf places them, and an array of more than 1 048 576 cells is not
placed. A MINSERT is an INSERT in the file, its type INSERT and its subclass AcDbMInsertBlock, so one left out is counted
in `Skipped` under INSERT, as the file names it; the warning for an array too large to place calls it a MINSERT. An
INSERT inside a block is placed within it and the two placements composed, to a depth of 64. An INSERT that cannot be
placed is counted in `Skipped` and said in a warning, the rest read: one of a block the file does not define
(the warning names it), of a block inside itself, at once or through others (the warning names the first chain,
`A > B > A`), one nested deeper than 64, one of an external reference (a block of flag 4, whose drawing is another file),
one of the block of a layout, and one that would take the entities placed from blocks past `MaxPlacedEntities`. The blocks of the layouts, `*Model_Space` and `*Paper_Space`, are not among the
`Blocks`, and a warning counts their entities: the drawing is read from its ENTITIES section.

Blocks placed inside blocks multiply: a block of 100 INSERTs of a block of 100 INSERTs of a block of 100 INSERTs of a line
is a file of 18 KB that places 100 000 000 lines. `DxfReadOptions.MaxPlacedEntities` (2 000 000 by default, above nought)
bounds the entities placed from blocks in one reading, the drawing's first, in the order of the file, then those of the
`Blocks` read on their own. An INSERT is placed whole or not at all: how many entities its block places at the most is
worked out once for each block, through its INSERTs, before any is placed, and an INSERT that would take the count past
the bound places none, is counted in `Skipped`, and one warning names the bound and how many entities the INSERTs left
out would have placed. A block inside itself counts nought where it is met again, as it is not placed again there, and a
size worked out through such a block is worked out again for each INSERT met inside another block, but kept for every
INSERT of the block the working out started from. Whatever the
working out says, the count of what is placed holds the bound too: an INSERT whose entities would pass it is taken back
whole, its entities and what its placing counted, and counted with the others left out. That file is read in 1.4 s on .NET 8, holding 340 MB, where it ran out of memory; the one of three
levels places its 1 000 000 lines in 2.5 s, holding 685 MB with what its blocks read on their own place. Working out
stops at 1 000 000 blocks in all, for the whole reading, the drawing's INSERTs and those of the blocks read on their own
together: once they are spent, an INSERT whose size would need one more block worked out is refused, counted in `Skipped`
and said in a warning of its own, and so are the other INSERTs of the same block, without working it out again; a size
worked out before still places its INSERTs, that of a block that met no block inside itself and that worked out for the
INSERTs of a block from which a walk began, whether it met one or not. A file that works out fewer
reads as it did. 1 000 INSERTs of a block in a ring of 26 blocks, each inserting the next twice, a file of 43 KB, are read
in 1.4 s on .NET 10, where they took 35 s, and a ring of 1 000 blocks each inserted by the drawing, each a root of its own
working out, a file of 174 KB, in 0.19 s, where it took 47 s when the bound was one for each INSERT.

What a placement makes of each shape:

| Shape in the block | Under a turn, an even scaling or a mirror | Under an uneven scaling |
|---|---|---|
| POINT, LINE, RAY, XLINE, chain or polygon without arcs, 3D POLYLINE, 3DFACE, SOLID, TRACE, mesh | carried point by point | the same |
| SPLINE | its control points carried, its knots and weights kept, which is exactly the spline's image | the same |
| SPLINE of fit points alone | the spline through its points in the block, then carried | the same |
| ELLIPSE | the ellipse of the images of its semi-axes; a part, the spline that is exactly that part | the same |
| CIRCLE | a circle, its radius times the scale | the ellipse it becomes |
| ARC | an arc, running the way its image runs | the spline that is exactly that part of the ellipse |
| LWPOLYLINE or 2D POLYLINE with arcs | the chain or loop with its bulges, its arcs about the placed normal | a segment for each straight edge and a spline for each arc edge, each an entity with the look and handle of the polyline, in its order |
| TEXT, MTEXT | the text carried by its axes, a mirror turning its normal over | the same, its height and width factor stretched by its axes |

So every shape placed is the image of the block's shape to the rounding of its numbers: the circle of radius 3 scaled by
3 along x is the ellipse 9 by 3, and a polyline with two arcs placed so is four entities, a spline, a segment, a spline
and a segment. A mirror turns an arc over: the ARC of radius 4 about (5, 5) from 30 to 200 degrees, in a block whose
base point is (2, 1), placed at (-100, 0) with a scale of -1 along x, is the clockwise `GeoArc2` about (-103, 4) from
(-106.464, 6). A scaling so nearly even that a circle placed whole lands within the point tolerance of
`DxfReadOptions.Tolerance` of where the scaling puts each of its points keeps the circle, the arc and the chain with arcs
of the plane it scales so, by the rule of `GeoTransform3.Transform(GeoCircle3)`: the circle of radius 10 scaled by
1.0000001 along y is the circle of radius 10, and by 1.001 the ellipse 10.01 by 10. A placed entity lies on the floor,
for `PlanAs2D`, where its placed plane's normal is straight up or down and every height of it nought, so a block drawn
in the plan and placed turned about z, scaled or mirrored is read as shapes of the plane, and one placed on a tilted
extrusion as shapes of space. Held against ezdxf 1.4.5 on a drawing of 168 entities placed every way an INSERT places
them, every point read is within 1E-9 of where ezdxf puts it; ezdxf's own virtual INSERT of a block inside a block is
wrong under an uneven outer scaling with an inner turn, so there the two matrices ezdxf gives are composed.

An entity of a block on layer 0 is on the layer of the INSERT that placed it, and one whose colour, linetype or
lineweight is BYBLOCK has the INSERT's, its true colour too where it has none of its own, through every level of
nesting; BYLAYER stays BYLAYER. In the drawing itself there is no block to take a look from: a colour BYBLOCK is no colour
and the linetype is named as written. An entity on a layer of its own whose colour or linetype is BYBLOCK, placed by an
INSERT that is itself BYLAYER, is drawn by AutoCAD in the colour and linetype of the INSERT's layer; the reader gives it no
colour and no linetype, on its own layer, which reads as that layer's: the entity has no field for a look taken from
another layer. The thickness is stretched as the placement stretches the extrusion, and turned over where the placed
extrusion points against the entity's own: 10 is 30 under a scale of 3 along z and -30 under one of -3, and a mirror in x
leaves it 10. Each entity keeps its own `Handle`, that of the block's entity; `BlockPath` names the
blocks it was placed through, outermost first (`FRAME`, `BOLT`), and `InsertHandles` the INSERTs, one to each name,
null for an INSERT of R12, which has no handle, the cells of a MINSERT sharing its handle. An ATTRIB of an INSERT and
an ATTDEF of a block are text, counted in `Skipped`.

Each of the `Blocks` holds its own entities in its own coordinates, read with the same options, an INSERT inside it
placed within it, layer 0 and BYBLOCK kept as they are. `Skipped` and the `Warnings` are of the drawing as placed: an
entity of a block that is not read is counted each time a block places it, so a HATCH in a block placed 100 times counts
100, and what the blocks read on their own leave out is not counted. Reading 10 000 INSERTs of a block of 50 entities
(12 lines, 10 circles, 10 arcs, 8 closed polylines with arcs, 5 splines and 5 points; 1.9 MB of text) gives 500 000
entities in 0.50 s on .NET 10 and 0.55 s on .NET 8, the document holding 157 MB with the groups or without them, since
the places of one entity of a block share its groups; scaled unevenly, the 820 000 entities they give take 0.96 s and
1.00 s and 448 MB.

`DxfWriter.Add(entity)` writes a placed entity as it is, in the drawing itself, with its look: the file written has no
blocks, and reads back as the same shapes, every number to the bit but the angles of an arc and the axis of an ellipse.

## Writing DXF

`DxfWriter` writes a DXF file of version R2000 in millimetres, which AutoCAD, Tekla Structures and every CAD viewer
open. Each shape goes on a layer named as given (layer 0 when none is), made on first use in white with the continuous
line, since CAD colours by layer. Names are cut at 255 characters, the characters DXF refuses and the comma become `_`,
and letters beyond ASCII are written as escapes such as `\U+00E9`; two names that differ only in case, accented letters
included, are one layer, named as first seen. In the plane a shape is written as the entity CAD has for it, not as
chords: a polygon, rectangle, triangle or ring of a face as a closed lightweight polyline, a chain or loop with arcs as
one with the bulge of each arc edge (the same number `GeoEdge2.Bulge` gives, 1 for a semicircle), a circle, an arc, an
ellipse, a ray, a line without ends and a spline as a CIRCLE, ARC, ELLIPSE, RAY, XLINE and SPLINE. DXF arcs run counter-clockwise only, so a clockwise `GeoArc2` is
written from its end to its start; a face is its rings, not a filled area, which `SvgWriter` draws. In space a point,
segment, ray, line without ends, ellipse and spline are written as they are, a chain and a polygon as a 3D polyline, and anything with a surface as
3DFACEs: one per triangle of a face or a body (a box 100 by 100 by 20 with a hole 20 by 20 through it is 64), one per
face of a mesh that has three or four corners and turns one way, and six for a `GeoAabb3` or `GeoObb3`, written from its
corners, so a box 100 by 100 by 0.002, or by nothing, is written too, and an empty `GeoAabb3` is refused with an
`InvalidOperationException`, as `ToObb` refuses it. Where a face, a side of a body or a face of a mesh is cut into
triangles, the edges between its own triangles are marked invisible, so a wireframe shows its outline; triangles given
as they are keep every edge. A curve in space (`GeoArc3`, `GeoCircle3`, and chains and loops with arcs) is written as
itself when no chord tolerance is given. An arc is an ARC and a circle a CIRCLE, each in the coordinate system of its
own plane: the normal, turned if need be to point up in its largest component, is the extrusion, the centre is in the
plane's coordinates by the arbitrary axis algorithm AutoCAD itself applies (x is the world y crossed with the normal
where its x and y are both less than 1/64 from nought, the world z crossed with it otherwise), and the angles are degrees
counter-clockwise about the extrusion from that x axis. A circle of radius 25 about (10, 20, 30) standing on the normal
(1, 1, 0) is a CIRCLE with extrusion (0.7071, 0.7071, 0) and centre (7.0711, 30, 21.2132); an arc that turns clockwise
about the extrusion is written from its end to its start, so an arc on the floor with the normal (0, 0, -1) is written
about up, and is written exactly as the `GeoArc2` or `GeoCircle2` of the same arc is, with no extrusion groups. A chain or
loop that lies in one plane is one lightweight polyline in that plane: the extrusion is the plane's normal turned up in
the same way, the elevation is the height along it, the vertices are the plane's x and y and each arc edge keeps its
bulge times the cosine between its normal and the extrusion (negated where they point apart, nought for an arc that
bows square to the plane, which the planar tolerance lets stand off it). A slot 80 long and 20 wide (straight sides of
60) with a half circle at each end, in a plane tilted 30 degrees about the x axis, is four vertices and two bulges of 1,
and `Add(chain, tolerance)` states the tolerance that judges whether a chain lies in one plane (the global one without
it). A chain that bends out of one plane, such as a reinforcing bar, is a LINE for each straight edge and an ARC for
each arc edge, each in its own coordinate system, in the order of the chain; an arc edge too shallow to place a circle
for is read by its chord, and a chain with no arc whose vertices lie on one line, which lies in no one plane, is a LINE
for each edge. An arc or circle with a radius that is not a finite number above nought, or a normal of no length, is
refused with an `ArgumentOutOfRangeException` or an `ArgumentException`. Given a chord tolerance instead (`Add(arc, 0.1)`), any of them is cut into chords no further from it
than that, with no default: a circle of radius 100 at 0.1 is 71 chords, one of radius 50 000 at 0.001 is 15 708, and one
of radius 25 at 0.1 is 36 where the CIRCLE is one entity. A curve that would need more than 1 048 576 chords, a chain or loop
counted whole, is refused with an `ArgumentOutOfRangeException`. A shape holding a number that is not finite is refused
with an `ArgumentException` and leaves nothing in the file. The file is plain ASCII, with the extents of what was drawn
in its header and the view opened on them.

A drawing `DxfWriter` writes is also where a drawing made from nothing begins for the editing model:
`DxfDatabase.Parse(new DxfWriter().Add(outline, "plate").ToString())` opens it to be edited as any other (see [The
model](#the-model)).
