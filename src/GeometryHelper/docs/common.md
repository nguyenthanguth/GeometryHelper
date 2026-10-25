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
millimetre outside a body reads as in it; a segment you give, no longer than `EqualPoint`, is read as its start; a circle
collides as its rim and a loop of arcs as its outline; `GetShortestLineTo` and `GetIntersections` keep their readings; and
two partial arcs lying nearly on one axis are searched and not proved.
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

When an answer surprises, look at the shapes. `GeometryHelper.Export` writes them in four formats, numbers
always with a point for the decimal whatever the culture:

```csharp
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

## Reading DXF

`DxfReader` reads a DXF file into the library's shapes: what `DxfWriter` writes, and what AutoCAD and the other CAD
programs write of the same kinds, ASCII files of R12 (AC1009) to R2018 (AC1032). An INSERT is read as the entities of its
block, placed where it puts them, and the blocks themselves are there too (see Blocks and INSERTs below); a TEXT and an MTEXT
are read as a `DxfText` (see Texts), and the layer table into `LayerTable` (see The layer table).

```csharp
using GeometryHelper.Export;

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

DIMENSION, HATCH, LEADER, MLEADER, IMAGE, MESH, REGION, 3DSOLID, BODY, ATTDEF, ATTRIB, VIEWPORT and any other type are
not read and are counted in `Skipped`. So is an entity with no shape to it, the reason said once for each type in a Debug
line of `GeometryHelperLog`: a LINE no longer than the point tolerance, a polyline of fewer than two distinct vertices, a
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

## Editing a DXF file

`DxfDatabase` opens a DXF file to edit it as AutoCAD's `Database` edits a drawing, and to save it again with nothing lost.
Its classes are AutoCAD's with Dxf in front, so that a plugin can use both: `DxfBlockTableRecord`, `DxfLayerTableRecord`,
`DxfLine`. It keeps every byte of the file and reads what the file holds from them when asked for: opened and saved with
nothing changed, a file is written back byte for byte; a change writes the records it is about anew in the file's own
style, and every other record keeps its bytes. `DxfReader` stays the faster way to read a drawing into shapes alone.

```csharp
using GeometryHelper.Export;
using GeometryHelper.Geometry;

DxfDatabase db = DxfDatabase.Open("plan.dxf");                       // or Open(stream), Parse(text)

foreach (DxfEntity entity in db.ModelSpace) { ... }                  // DxfLine, ..., DxfUnknownEntity
DxfLine line = db.ModelSpace.OfType<DxfLine>().First();
line.EndPoint = new GeoPoint3(100, 0, 0);                            // that record written anew
line.ColorIndex = 1;
db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0)) { Layer = "steel" });
db.LayerTable.Add(new DxfLayerTableRecord { Name = "rebar", ColorIndex = 3 });
db.Insunits = DxfUnits.Millimeters;
line.Erase();
db.Save("copy.dxf");                                                 // every other record the same bytes
```

There is no `ObjectId` and no transaction: an object read from the drawing is the same object each time it is reached,
and a change to it is written at once.

| AutoCAD | GeometryHelper |
|---|---|
| `var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite); btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);` | `db.ModelSpace.AppendEntity(line);` |
| `var layer = (LayerTableRecord)tr.GetObject(lt["steel"], OpenMode.ForWrite); layer.IsOff = true;` | `db.LayerTable["steel"].IsOff = true;` |

What is kept is everything: the version, R12 (AC1009) to R2018 (AC1032); the line ends, a carriage return and a line
feed, a line feed alone, or both in one file; the group codes padded to three places or not; the mark of UTF-8 or none;
the text in the code page of a file before R2007 and in UTF-8 after, byte for byte; comments (999); the sections, tables,
objects and entities the library does not read (CLASSES, OBJECTS, LAYOUT, DICTIONARY, HATCH, DIMENSION, IMAGE and the
rest); extended data, reactors and extension dictionaries; and whatever follows the EOF. A file `DxfReader` refuses is
refused the same way, with the same message.

`ModelSpace`, `PaperSpace` and each block of the `BlockTable` are a `DxfBlockTableRecord`: its `Name`, `Origin`,
`IsLayout`, `IsAnonymous` and `IsFromExternalReference`, and its entities, each the `DxfEntity` of its type's class or a
`DxfUnknownEntity` for a type the library has no class for yet, kept as the file has it, its look editable. Model space
and paper space are named `*Model_Space` and `*Paper_Space`, as AutoCAD names them, whatever the file calls them (ezdxf's
files of R12 call them `$Model_Space` and `$Paper_Space`), and stand for their records from the moment the drawing is
opened, with their handles and extended data. Every entity has AutoCAD's `Layer`, `Color`, `ColorIndex`, `TrueColor`,
`Linetype`, `LinetypeScale`, `LineWeight` and `Visible`, its `Handle`, `OwnerHandle`, `XData` and
`ExtensionDictionary`, its `Owner` and `BlockName`, `GetGeometry()`, the library's shapes for it as `DxfReader` reads it,
and `TransformBy`, which every class of entity has but `DxfUnknownEntity`: the geometry of a type the library keeps as the
file has it is not changed, and a HATCH refuses to be moved. A true colour's `Color` carries the index nearest it, orange
(0xFF8000) the index 30, and is written with it. A curve is a `DxfCurve`, walked as AutoCAD walks one; the line from
(0, 0, 0) to (30, 40, 0) is 50 long, its parameter the distance from its start. A property given the value it has changes
nothing, to the byte.

A block is walked over the entities it holds when the walk begins, so that `foreach (DxfEntity e in db.ModelSpace)
e.Erase();` erases them all, as it does in AutoCAD: an entity erased on the way is passed over, one appended on the way is
not reached, and nothing throws. A text of extended data is read with its escapes as letters, as a layer's name is, so that
a letter the file's code page has not, written as `\U+0110`, comes back as the letter it was set as.

`AppendEntity` writes an entity made in code at the end of the ENTITIES section, or before the ENDBLK of a block, with a
handle above every handle of the file and every handle something points at, `$HANDSEED` set above it, the block's
BLOCK_RECORD as its owner from R13 on and group 67 in paper space. Its layer is added to the layer table where it is not
listed, in the colour 7 with CONTINUOUS, and the applications of its extended data to the table of applications. A file
of R12 takes no subclass markers and has no SPLINE, ELLIPSE, MTEXT, RAY, XLINE or LWPOLYLINE, nor lineweights, true
colours, or a linetype scale or visibility of an entity's own: those are refused, by `AppendEntity` and by the setters
alike. An entity of a file of R12, or a copy of one (`GetTransformedCopy`, `GetOffsetCurves`, `GetSplitCurves`), appended
to a drawing of R13 or later is written in the forms AutoCAD writes there: its subclass markers in AutoCAD's order, the
marker AcDbEntity, its owner and a handle of its own. One of R13 or later appended to a drawing of R12 is written in the
forms of R12, as AutoCAD saves a drawing as R12: each record keeps the groups R12 has for its type, each read from the
part of the record it belongs to, so that the centred ATTRIB whose AcDbAttribute part writes 71 and 72 again keeps its
justification, and loses its markers, owner, lineweight, material and plot style, and an object embedded after its 101,
as a multi-line attribute's, which R12 reads as one line. `Erase()` takes an entity out with every record of it, a
POLYLINE its vertices and end and an INSERT its attributes, and its handle is not given again; when the file is saved, a GROUP no longer lists it, and one it
leaves empty goes, a list of reactors, a SORTENTSTABLE and a dictionary no longer hold it, the objects it owned go with it,
and any other pointer at it stays, the log naming up to ten of them (Warn).

`Clone()` copies any entity as it is, as AutoCAD's `Clone` does, a `DxfUnknownEntity` among them, and
`GetTransformedCopy` copies and moves one; a copy is of the class of its original, a vertex a vertex. A copy appended to
the drawing it came from points at what its original points at. Appended to another drawing, its pointers are taken to
the objects of the same name there, as AutoCAD's deep clone between drawings maps them: a material (347), a plot style
(390) or a visual style (348) by the names of its dictionary and its entry, a record of a table by its name; a pointer to
an object the drawing has no namesake of is left out, so that the drawing's default holds, and a handle of extended data
(1005) is left out, as AutoCAD's mapping leaves it null. The LINE of the material Steel_mat at 2F copied into a drawing
whose Steel_mat is 57 points at 57; into one with no Steel_mat it has no material. A copy of an associative HATCH leaves its
boundary objects behind: each path counts the pointers it keeps (97 of 0 where it keeps none), and a hatch left with none is
no longer associative (71 of 0), as AutoCAD's COPY leaves it.

`$EXTMIN` and `$EXTMAX` hold model space and paper space: an entity of a block appended, moved or changed leaves them as
they are, as AutoCAD leaves them until the INSERTs of the block are regenerated.

The header is read and set raw by `GetHeader("$INSUNITS")` and `SetHeader("$LTSCALE", new DxfTypedValue(40, "2"))`, and through
AutoCAD's properties: `Clayer`, `Insunits`, `Extmin`, `Extmax`, `Handseed`, `Celtype`, `Cecolor`, `Celweight`, `Ltscale`,
`Limmin`, `Limmax`, `Insbase`, `Pdmode`, `Pdsize`, `Textsize`, `Textstyle`, `Dimstyle`, `Lunits`, `Luprec`, `Aunits`,
`Auprec`, `Angbase` (in radians; the file writes degrees), `Angdir`, `Orthomode`, `Fillmode`, `Mirrtext`,
`LineWeightDisplay`, `Saveproxygraphics`, `Splinesegs`, `Splinetype`, `Surftype`, `Surfu` and `Surfv`. A variable R2000 brought, `$INSUNITS`, `$CELWEIGHT`, `$LWDISPLAY` and
`$PROXYGRAPHICS` among them, is refused in a file of R12, as AutoCAD writes none there. `$EXTMIN` and
`$EXTMAX` grow to hold what is written and are never shrunk: an entity whose own record is written whole, as a LINE moved
or a POLYLINE given a new flag, grows them at once; a run of groups of its record written in place (a LWPOLYLINE's vertex,
a SPLINE's knots) or a record that follows it (a VERTEX put in or set in a POLYLINE, a point of a mesh) grows them by the
entity as it stands when the header is next read or the file saved, once however many vertices were written, so that a
vertex moved out and back between two readings leaves no trace.
What is written is in the file's own style: its line end, its padding, the numbers as `DxfWriter` prints them, and the text in its encoding, a letter the page cannot hold, or whose
bytes would read back as UTF-8, written as its escape (`\U+00E9`). A file that names no version is written as one of R12,
or of R2000 where its records carry subclass markers. Editing is for one thread at a time.

Circles, arcs, ellipses, points, rays and xlines are `DxfCircle`, `DxfArc`, `DxfEllipse`, `DxfDBPoint`, `DxfRay` and
`DxfXline`, with AutoCAD's members. A CIRCLE whose groups 10, 20 and 30 are (10, 20, 5) on the extrusion (0, 0, -1) has its
`Center` at (-10, 20, -5), as AutoCAD gives it; setting `Normal` keeps that centre where it is in the world. An arc's
`StartAngle` and `EndAngle` are radians from nought up to 2 pi, written as degrees. On the ellipse with major axis
(10, 0, 0) and ratio 0.5, `StartParam` 0.7854 (pi / 4) is the point (7.071, 3.536, 0), whose `StartAngle` is 0.4636.
`new DxfArc(center, normal, 10, 0, Math.PI / 2)` then `db.ModelSpace.AppendEntity(arc)` adds a quarter circle; a DXF of R12
refuses an ellipse, a ray or an xline.

A polyline of a file of R2000 or later is a `DxfPolyline`: `pl.GetPoint2dAt(1)` is its second vertex in the plane of its
normal, `pl.GetPoint3dAt(1)` the same in the world, and `pl.SetBulgeAt(1, 1.0)` makes the segment leaving it a half circle.
A POLYLINE is walked as AutoCAD walks one, `foreach (DxfVertex2d v in polyline2d) v.Bulge = 0;`, and grows by
`AppendVertex(new DxfVertex2d(point, 0, 0, 0, 0))`; a vertex is written, put in or taken out as its own record, the others
keeping their handles, and its `Owner` is the polyline.

An INSERT of BOLT at (50, 60, 0) turned by 45 degrees and scaled by (1.5, -1.5, 2) has `Position` (50, 60, 0), `Rotation`
0.785 and a `BlockTransform` that takes BOLT's origin (1, 2, 0) to the insertion point (50, 60, 0); the block's point
(0, 0, 0) lands at (46.82, 61.06, 0). Setting `Position` to (60, 55, 1) moves its ATTRIB MARK from (50, 59, 0) to
(60, 54, 1). `Name` is the block it places; `BlockName` is the block it is in.
`Explode()` gives the entities of its block placed where it puts them, as AutoCAD's `BlockReference.Explode` does: the
block's ATTDEFs among them, placed as the rest are, and none of the INSERT's ATTRIBs, whose values stay in its
`AttributeCollection`; every other entity refuses it, as AutoCAD answers eNotApplicable. An INSERT inside that cannot hold
the placing is exploded in its turn; one of a block already being exploded further out is refused, and the whole explode
is held to the 2 000 000 entities the reader places at most. An INSERT that comes out of it places the block of the drawing
it came from, so that it explodes in its turn, with what that block now holds. `ExplodeToOwnerSpace()` appends what
`Explode()` gives to the block the INSERT is in and keeps the INSERT, as AutoCAD does: `insert.ExplodeToOwnerSpace();
insert.Erase();` explodes it as AutoCAD's EXPLODE does, the attributes turned back into their definitions. Where the
drawing cannot hold a part, as R12 an ELLIPSE, nothing stays appended; a MINSERT of more than one cell is refused, as its
`Explode()` gives its first cell alone. `UnitFactor` takes the block's units to the drawing's (`$INSUNITS`): 25.4 for a block in inches
inserted in millimetres, 1 where either gives none. `GeometryExtentsBestFit()` is the box of the shapes placed, and
`GeometryExtentsBestFit(parentTransform)` the same after an outer INSERT's `BlockTransform`. A dynamic block is found as
ezdxf finds it, by the extended data `AcDbDynamicBlockGUID` of its BLOCK_RECORD, or `AcDbBlockRepBTag` of the anonymous
block that stands for it (`IsDynamicBlock`, `DynamicBlockTableRecord`, `AnonymousBlockTableRecord`); its dynamic
properties are AutoCAD's engine's to work out, and are not given. Its attributes are `DxfAttributeReference` objects
(`Tag`, `TextString`, owned by the INSERT); an ATTDEF is a `DxfAttributeDefinition` with its `Prompt`. An attribute of
several lines embeds an MTEXT from R2018: `IsMTextAttribute` makes one of its value where its text stands, or takes it out,
`MTextAttribute` gives and takes the MTEXT, its text made to agree as ezdxf's `set_mtext` makes it, and
`UpdateMTextAttribute()` places the MTEXT again where the text stands, after the attribute is moved or set from its
definition. A MINSERT is a `DxfMInsertBlock` with its `Columns`, `Rows`, `ColumnSpacing` and `RowSpacing`.

A TEXT is a `DxfDBText` (`TextString`, `HorizontalMode`, `IsMirroredInX` and the rest of AutoCAD's) and an MTEXT a
`DxfMText` (`Contents` with its codes, `Text` without them, `Location`, `Attachment`). An MTEXT is set in columns by
`SetStaticColumns(12.5, 2.5, 3)`, three columns 42.5 across, `SetDynamicColumns`, `SetColumnHeight` and the column
properties, written in its embedded object from R2018 and in the extended data of ACAD before, as AutoCAD and ezdxf write
them; `LineSpaceDistance` is its spacing factor times 5/3 of the height of its letters, `SetAttachmentMovingLocation` holds
it by another point without moving its words, `ShowBorders` draws its text frame, and `BackgroundTransparency` is a
`DxfTransparency`. Before R2018 the columns after the first are MTEXTs of their own, linked to the first, which carries
them: moved, turned, given a new normal, letter height or box height, or erased, it takes them with it, while its
`Contents` holds its own column's words alone. The linked columns of an MTEXT
of a file before R2018, the MTEXT records its extended data of ACAD lists in `ACAD_MTEXT_COLUMNS`, are part of that MTEXT,
as AutoCAD's database has them: a block walks and counts the main MTEXT alone, which carries its columns, so
that moving every MTEXT of model space moves each column once; an MTEXT that lists its own handle among its columns is
walked still. A copy of the
first holds the words of them all and links to none, so no two MTEXTs claim the same columns; and `Height`, before R2007,
is the height of the columns in the extended data of ACAD. A SPLINE is a `DxfSpline` of its
`Degree`, `ControlPoints`, knots and weights; a 3DFACE a `DxfFace` (`GetVertexAt`, `IsEdgeVisibleAt`), a SOLID a
`DxfSolid` (`GetPointAt`), and the meshes a `DxfPolyFaceMesh` and a `DxfPolygonMesh`, whose vertices and face records are
walked as their polyline's are.

Every entity has AutoCAD's `Transparency`, group 440, as a `DxfTransparency`: BYLAYER where the record gives none, BYBLOCK,
or an alpha from 0, clear, to 255, solid, `new DxfTransparency(127)` the 50 per cent of AutoCAD's dialog, written
33 554 559 (0x0200007F); a file older than R2004 has none and refuses one. Its `Hyperlinks` are those of its extended data
of PE_URL, `line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" })`
written as AutoCAD writes them. `GetPlane()` gives the plane it lies in, that of its extrusion through its first point
where all of it lies there, else the one its shapes span, to `Tolerance.Default`, and `IsPlanar` whether there is one: a 3D
polyline with a corner 0.002 off the plane of the others has none. `IntersectWith(other, DxfIntersect.ExtendThis)` gives
the points where two entities meet, by the library's shapes for them: the line from (0, 0, 0) to (10, 0, 0) meets the
circle of radius 3 about (10, 0, 0) at (7, 0, 0), and, run on as its infinite line, at (13, 0, 0) too; an arc runs on as
its circle. A closed polyline, a SOLID and a 3DFACE meet along their edges, not across their inside: the square 4 by 4
about the origin meets the line along the x axis at (-2, 0, 0) and (2, 0, 0), and a line standing through its middle not at
all. A polyline meets segment by segment, a point meets what it lies on, and a pair the library cannot cross, a text among
them, throws `NotSupportedException`. `BoundingBoxIntersectWith` gives where the boxes of two entities meet.

Every object can be given an extension dictionary, `CreateExtensionDictionary()`, an empty DICTIONARY owned by it and
written as AutoCAD writes one, reached as its `ExtensionDictionary`, a `DxfDBDictionary`: `SetAt("GH_DATA", new
DxfDBDictionary())` adds an entry, `SetName` renames one, `Remove` takes one out with its object, and
`ReleaseExtensionDictionary()` takes the dictionary out once it is empty. `SetBinaryDataForKey("GH_BIN", bytes)` keeps bytes
there, in an XRECORD of groups 310 of 127 bytes each, and `GetBinaryDataForKey` reads them back. `XDataTransformBy`
moves the extended data as AutoCAD moves it: a position of the world (1011) moved, a displacement (1012) turned and
scaled, a direction (1013) turned, a distance (1041) and a scale factor (1042) scaled, a plain point (1010) kept. An entity's `TransformBy` and `GetTransformedCopy` move its extended data so, in the same writing of its record, a POLYLINE that of its vertices and an INSERT that of its attributes; one with none of those groups keeps the bytes of its extended data. `Remove` of an entry erases the dictionaries and records it owns at every depth, and an object erased takes its extension dictionary with everything in it.

The drawing's own dictionaries are objects too: `NamedObjectsDictionary`, and those it names, `GroupDictionary`
(ACAD_GROUP), `LayoutDictionary` (ACAD_LAYOUT), `MLeaderStyleDictionary`, `TableStyleDictionary` and the others AutoCAD's
`Database` names by id; null where a drawing has none, as one of R12. `SetAt` adds a `DxfDBDictionary`, a `DxfXrecord` or a
`DxfGroup` made in code; `TreatElementsAsHard` says whether a dictionary owns its entries hard (280 of 1, its entries named
by 360), and `CopyTo` copies its entries into an array. An XRECORD is a `DxfXrecord`, its `Data` the groups after its
cloning flag, the groups 102 of a list among them: `db.NamedObjectsDictionary.SetAt("GH_PLATE", new DxfXrecord { Data =
new[] { new DxfTypedValue(1, "B-12"), new DxfTypedValue(40, "12.5") } })` keeps a text and a real with the drawing.

A group is a `DxfGroup`, named by its entry of the group dictionary: `db.GroupDictionary.SetAt("BEAMS", new
DxfGroup("Beams of grid A", true))`, then `group.Append(beam)`, which writes the beam's handle last among the group's (340)
and the group into the beam's reactors, as AutoCAD writes a member. `Prepend`, `InsertAt`, `Remove`, `RemoveAt`, `Replace`,
`Reverse`, `Transfer` and `Clear` reorder the members, `GetAllEntityIds` gives them as entities, and `SetColorIndex`,
`SetLayer` and the other setters of the look set every member at once. A large group is filled by `Append` of a collection, in
one writing: one at a time is fine for groups as drawings have them (8 000 in half a second), but 32 000 take some 15 seconds.
A group added under `*` is anonymous, `*A1` the first
name its dictionary has not. An entity erased leaves its groups at once, and a group left empty is dropped when the drawing
is saved, as AutoCAD drops it, whatever emptied it: its last member erased, `Clear`, `RemoveAt`, or never given one. A group
the file was read with empty is kept as it was. An entity copied into another drawing takes with it the records of the
tables it names that the drawing has not (its layer, linetype, text style, dimension style) and a copy of the block AutoCAD
names itself it draws by (a dimension's `*D`, a table's `*T`, an INSERT's `*U`), as AutoCAD's deep clone between databases does.

The layouts are `db.Layouts`, `DxfLayout` objects in the order of their tabs: `LayoutName` (set, the layout dictionary's
entry renamed with it; Model keeps its name), `TabOrder`, `TabSelected` (the layout the drawing opens on), `Limits`,
`Extents` (empty for a layout never worked out, as ezdxf's 1E+20 above -1E+20 says), `BlockTableRecord` and
`GetViewports()`, with the plot settings read as AutoCAD's `PlotSettings` has them: a layout of A3 on its side has
`PlotPaperSize` (420, 297) in millimetres. A viewport is a `DxfViewport`: one 200 by 150 about (150, 100) on the sheet,
showing model space about (5 000, 3 000) with a `ViewHeight` of 7 500, has `CustomScale` 0.02 and `StandardScale`
`Scale1To50`; its view, snap, grid, user coordinate system, plot style, shading and lighting are read and set, its layers
frozen and thawed by `FreezeLayersInViewport` and `ThawLayersInViewport` (331, by handle), and `TransformBy` moves and
scales it on its sheet, its view kept, a turn refused. A viewport of a file before R2000 keeps its view in its extended data MVIEW, read
and set there; what those files have not, as the user coordinate system, reads as AutoCAD's default and is refused when set. A
viewport made in code goes to a layout of paper space, given the next `Number` of the layout; `On = true` turns it on.

A layer's look in a viewport is `layer.GetViewportOverrides(viewport)`, a `DxfLayerViewportProperties` of its colour,
linetype, lineweight and transparency there, each the override AutoCAD keeps in the layer's extension dictionary
(ADSK_XREC_LAYER_COLOR_OVR and the three others) else the layer's own; `RemoveAllOverrides()` takes them out. The drawing's
properties are `db.SummaryInfo`, its DWGPROPS, set as AutoCAD sets them:
`db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-12" }.ToDatabaseSummaryInfo()`. Its
custom properties are read from the groups 300 to 309 (`name=value`) and from the list after a group 90 (a count, then each
name and value in two groups 1); they are written in the form the record has, the list where it has one or is made new in a
drawing of R2004 or later, so that it never holds two sets. Erasing a viewport takes its overrides out of the layers.

A layer has its `Description` and `Transparency`, kept in the extended data of AcAecLayerStandard and AcCmTransparency as
AutoCAD keeps them, `ViewportVisibilityDefault` (frozen in new viewports, flag 2) and `IsUsed`: layer 0, Defpoints, the
current layer and any layer an entity is on, those appended in code among them. The first `IsUsed` reads the drawing once
for every layer, and the answers hold until the drawing is edited: 400 layers of a drawing of 300 000 lines take 0.08
seconds. Setting the `Name` of a layer renames it with every reference to it: the
layer steel renamed Steel_S355 has every entity of it, in model space, paper space and the blocks, its vertices and
attributes among them, on Steel_S355, a layer named in extended data (1003) too, and `$CLAYER` where it was the current
one; layer 0 and Defpoints keep their names. `Erase()` takes a layer out where nothing is on it, nothing points at it and
it is not 0, Defpoints or the current layer, the table's count lowered by one. The LAYER_INDEX and LAYER_FILTER objects a
drawing may keep are left as they are.

A linetype's dashes carry their shapes and texts: AutoCAD's gas line is `SetTextAt(1, "GAS")`, `SetShapeStyleAt(1,
db.TextStyleTable["Standard"])`, `SetShapeScaleAt(1, 0.1)` and `SetShapeOffsetAt(1, new GeoVector2(-0.1, -0.05))` on its
gap of 0.2, written `49 -0.2, 74 2, 75 0, 340` the style's handle `, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`; a dash of a
file has its length set too, and a linetype nothing draws with is erased. A text style is added to the `TextStyleTable`,
`new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx", TextSize = 2.5 }`, and its font, height, width,
slant and TrueType `Font` (a `DxfFontDescriptor`, `1001 ACAD, 1000 Arial, 1071 34` as AutoCAD keeps it) are set.

A block has its `Comments` (the BLOCK's 4), `Units`, `Explodable` and `BlockScaling` (its BLOCK_RECORD's 70, 280 and 281,
from R2007 on), `PreviewIcon` (the bytes of its 310), `IsDynamicBlock` and `Hyperlinks`. `GetBlockReferences(true)` gives
its INSERTs, as AutoCAD's `GetBlockReferenceIds` gives their ids, and `GetBlockReferences(false)` those of the blocks that
hold one of it too; `GetAnonymousBlocks()` gives the blocks AutoCAD made of a dynamic block. Setting its `Name` renames it
with every INSERT of it; `Erase()` takes it out, BLOCK, entities, ENDBLK and BLOCK_RECORD, where nothing inserts it; and
`AssumeOwnershipOf(entities)` moves entities of the drawing into it, each keeping its handle, out of the draw order
(SORTENTSTABLE) of the block it leaves and onto the end of that of the block it enters.

The drawing reads and sets the variables AutoCAD keeps out of the header, as DICTIONARYVAR objects of the dictionary
AcDbVariableDictionary: `Cannoscale` (a scale of the drawing's list of scales), `Cetransparency` (a whole per cent, so the
alpha 127 is 50), `Tablestyle`, `MLeaderstyle`, `DetailViewStyle` and `SectionViewStyle` (by name, one of the drawing's),
`MLeaderscale`, `AnnoAllVisible`, `MsLtScale`, `MsOleScale`, `LayerEval`, `LightingUnits`, `LightsInBlocks`,
`TStackAlign`, `TstackSize`, `HpInherit`, `Isolines`, `Facetres`, `PdfFrame` and `PlineEllipse`; AutoCAD's default where the
drawing gives none, as a drawing of ezdxf, to which a variable set adds the dictionary of variables. `ThumbnailBitmap` reads
the bytes of the THUMBNAILIMAGE section.

A copy in no file, and an entity made in code, holds its own records alone: one file holds every copy of a drawing and
keeps none of them, about 460 bytes a copy, so that the explode of an INSERT of 1 000 000 lines holds 440 MB. A copy of an
INSERT places its block as the drawing it was copied from has it now, edits and all, and keeps to it when it is renamed
there: appended to another drawing it names the block by its name now, and is refused where that drawing has no block of
that name; a copy whose block was erased since places nothing.

The curves edit as AutoCAD's do, each result a new entity of the same look in no file, to be appended:

```csharp
DxfPolyline pl = db.ModelSpace.OfType<DxfPolyline>().First();
IReadOnlyList<DxfCurve> pieces = pl.GetSplitCurves(new[] { 1.5 });   // cut half way along the second segment
IReadOnlyList<DxfCurve> outer = pl.GetOffsetCurves(5.0);              // to the right of its way: a counter-clockwise loop grows
pl.Extend(pl.EndParam + 0.5);                                         // the last segment run on by half its length or angle
DxfSpline spline = pl.Spline;                                         // the spline that is exactly the polyline
DxfCurve flat = spline.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));
```

A segment cut keeps the part of its arc and its widths read off along it: the polyline (0, 0), (10, 0), (10, 10) with a
bulge of 1 on its second vertex cut at 1.5 is (0, 0), (10, 0), (15, 5) and (15, 5), (10, 10), each arc of bulge 0.414. An
offset keeps arcs as arcs and runs the corners on until they meet, as AutoCAD's OFFSETGAPTYPE 0; the square of side 10
running counter-clockwise offset by 1 is 12 by 12, running clockwise 8 by 8. Every curve offsets to that side, the right of
its way seen from the tip of its normal, where an arc's radius grows, as AutoCAD offsets a circle and an arc: the line from
(0, 0, 0) to (30, 40, 0) offset by 5 runs from (4, -3, 0). The exceptions are an arc, a circle and an ellipse given a normal
by `GetOffsetCurvesGivenPlaneNormal`: their radius grows with a distance above nought whichever way the normal points, as
AutoCAD offsets them by their radius, so that under the normal turned over an arc and a polyline of that one arc offset to
opposite sides. An ellipse is offset as the spline of the curve at the distance, as AutoCAD gives one. A spline is offset
piece by piece between its corners (knots standing as many times as its degree where its direction turns), each piece a
cubic fitted through points of the true offset, more of them only in the spans that need them, until it keeps within
1E-3 of the true offset both ways; the pieces are joined as a polyline's offsets are, so that the L of degree 2 from
(0, 0) to (10, 0) to (10, 10) offset by 1 has its corner at (11, -1). A span that does not keep within 1E-3 at 512 points
is given as fitted. A cubic of 300 spans with one corner gives an offset of some 9 600 control points in 0.6 s. A spline
is run on along the polynomial of its end span. A curve projected keeps its kind where it can: a circle of radius 10 on
the floor projected along (0, 0, 1) onto the plane y + z = 0 is an ellipse of radii 14.14 and 10.

`DxfPolyline.ConvertFrom(polyline2d, true)` and `ConvertTo(true)` turn a LWPOLYLINE into a POLYLINE of the plane and back
in its place, its handle, owner, look, extended data and extension dictionary kept, the old object erased, as AutoCAD hands
the object id over; with `false` the new polyline is made in no file and the old one kept. A polyline of the plane is
fitted with `CurveFit` (a pair of arcs on each segment through every vertex) or `SplineFit` (a uniform B-spline on its
vertices as a frame, of the header's SPLINETYPE, 5 quadratic or 6 cubic, and SPLINESEGS segments to each span), a 3D
polyline with `SplineFit`, a polygon mesh with `SurfaceFit` (of SURFTYPE, SURFU and SURFV), each straightened by
`Straighten` or `ConvertToPolyType` / `ConvertToPolyMeshType`; the vertices of the frame keep their handles. The reader reads
a fitted polyline along the vertices the fitting made, and a smoothed mesh as its grid.

A spline gives and takes its `NurbsData` and `FitData` whole. `ElevateDegree` raises its degree with the curve kept to the
rounding, `InsertControlPointAt` and `RemoveControlPointAt` put a control point in and take one out with a knot,
`InsertFitPointAt`, `SetFitPointAt` and `RemoveFitPointAt` fit it anew through its points (its knots the lengths of the
chords, so that a spline through (0, 0, 0), (30, 40, 0) and (60, 0, 0) runs from 0 to 100), `ModifyPositionAndTangent`
makes it pass a point in a direction, `Rebuild` fits it again of another degree and count of points, and `ToPolyline` /
`ToPolylineWithPrecision` give the polyline along it, of arcs where it is flat, within a thousandth of the spline's size
at precision 0 and a tenth of that for each 10 more, never finer than a millionth of its size: the cubic on (0, 0),
(10, 10), (20, 10) and (30, 0) made lines gives 65 vertices at precision 10 and 513 from precision 30 up, the floor
reached, so that a precision above about 30 gives the same polyline. A point or face
of a polyface mesh erased keeps the mesh's counts and the faces' numbers of its points true. Vertices of a POLYLINE
appended or set one at a time cost about the same each, however many the polyline has: 20 000 appended one by one take a
fifth of a second, 10 000 points of a polyface mesh a twentieth. A LWPOLYLINE's vertex put in, set or taken out rewrites
only that vertex's groups and the count: 10 000 put in one by one take 0.8 s, set one by one 1.2 s. Straightening a
fitted polyline or desmoothing a mesh takes the made vertices out in one writing: a polyline fitted to 90 000 vertices is
straightened in 0.6 s.
