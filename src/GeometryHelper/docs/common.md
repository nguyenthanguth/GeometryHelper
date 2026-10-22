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
| `DxfWriter` | in the plane `GeoPoint2`, `GeoLine2`, `GeoRay2`, `GeoXLine2`, `GeoArc2`, `GeoCircle2`, `GeoEllipse2`, `GeoPolyline2`, `GeoPolylineArc2`, `GeoPolygon2`, `GeoPolygonArc2`, `GeoRectangle2`, `GeoTriangle2`, `GeoFace2`, `GeoSpline2` (as a SPLINE); in space `GeoPoint3`, `GeoLine3`, `GeoRay3`, `GeoXLine3`, `GeoEllipse3`, `GeoSpline3` (as a SPLINE), `GeoPolyline3`, `GeoPolygon3`, `GeoTriangle3`, triangles, `GeoFace3` and `GeoSolid3` (each with a `Tolerance` overload), `GeoMesh3`, `GeoAabb3` and `GeoObb3` (as six faces), `GeoSphere3` (with a chord tolerance, a 3DFACE for each triangle of its surface), and `GeoArc3`, `GeoCircle3`, `GeoPolylineArc3` and `GeoPolygonArc3` as the curves they are (each also with a chord tolerance, for chords); each on a layer; and a `DxfEntity` `DxfReader` read, with its look (see Reading DXF) |

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
block, placed where it puts them, and the blocks themselves are there too (see Blocks and INSERTs below).

```csharp
using GeometryHelper.Export;

DxfDocument drawing = DxfReader.Read("plan.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (GeoPolygonArc2 outline in drawing.OfType<GeoPolygonArc2>()) { ... }   // the closed polylines with arcs
foreach (DxfEntity entity in drawing.OnLayer("steel")) { ... }                 // shape, layer, colour, linetype, handle, groups

DxfDocument text = DxfReader.Parse(dxfText);                                   // or Read(stream)
```

A `DxfDocument` holds the `Entities` in the order of the file, the `Layers` (those of the layer table in file order,
then any an entity is on that the table does not list, two names differing only in case being one, as in AutoCAD), the
`Units` the header gives (`$INSUNITS`, as the `DxfUnits` enum numbers them: `Millimetres` is 4), the `Version`
(`$ACADVER`, `AC1015` for R2000), what was `Skipped`, counted by type in the order of the type names, and the `Warnings`.
Each `DxfEntity` has its `Shape`, its `Type` as the file names it, its `Layer`, `Colour` (an AutoCAD colour index from 1
to 255, null by layer or by block), `TrueColour` (0xRRGGBB, group 420), `Linetype` (group 6, null by layer),
`Lineweight` (group 370 in hundredths of a millimetre, one of AutoCAD's 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53,
60, 70, 80, 90, 100, 106, 120, 140, 158, 200 and 211, null by layer, by block, default or any other number, which a
warning counts, and refused by the constructor), `Thickness` (group 39,
nought where none), `Handle` (null in a file of R12) and its `Groups`: every group of its record in the order of the
file, extended data (1000 to 1071) among them, each value the text the file prints, so that nothing of the entity is
lost. Of a POLYLINE they run on through its VERTEX records and its SEQEND. `BlockPath` and `InsertHandles` name the
blocks and the INSERTs an entity was placed through, empty for an entity of the drawing itself. `KeepGroups = false` leaves the groups out, for a large file: 100 000 entities (34 MB) are held in 41 MB
without them and 157 MB with them. A `DxfEntity` can also be made in code, of a shape and its look, to be written by
`DxfWriter.Add(entity)`: `new DxfEntity(plate, "steel", colour: 1)`.

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

TEXT, MTEXT, DIMENSION, HATCH, LEADER, MLEADER, IMAGE, MESH, REGION, 3DSOLID, BODY, ATTDEF, ATTRIB, VIEWPORT and any
other type are not read and are counted in `Skipped`. So is an entity with no shape to it, the reason said once for
each type in a Debug line of `GeometryHelperLog`: a LINE no longer than the point tolerance, a polyline of fewer than
two distinct vertices, a CIRCLE or ARC of no radius, an ELLIPSE of no axis, a number that is not a finite number, a
SPLINE the library does not take (a periodic one among them whose knots cannot be clamped, which a warning names too), a
3DFACE of fewer than three distinct corners. `DxfReadOptions.Tolerance` (the default tolerance unless set; the global
one is never read) says what has no shape: a LINE 0.0004 long is left out within the default tolerance and read within
1E-5. An entity whose reading throws, such as a SPLINE of fit points 1E200 apart, whose system cannot be solved, is left
out and counted too, the others read, with a warning and one Warn line of `GeometryHelperLog` for each type. A group of
the look that cannot be read (a colour of 1.0, a true colour past a 32-bit number) is left out, and the entity read
without it. A number the scaling of `ToMillimetres` takes past the largest double (1E307 metres) leaves its entity out,
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
has it: .NET Framework has them all, but .NET 10 has none without CodePagesEncodingProvider, which the library does not
bring, so there its text is read as Latin-1 and a warning names the code page and says to save the drawing as a DXF of
R2007 or later, whose text is UTF-8. A name written with the
escapes of R2000 (`\U+00E9`) is read as its letters. A number is read from its first 19 digits and checked in integers,
the digits past 800 counting only as whether any is not nought: a number of 300 000 digits takes 2.2 ms on .NET 10.

The numbers are kept as the file has them unless `ToMillimetres` is set, which scales points, radii, elevations,
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

### Blocks and INSERTs

The blocks of the file are in `Blocks`, in the order of the file, and each INSERT of the drawing is read as the entities
of its block, placed where the INSERT puts them, in the order of the block and where the INSERT stands among the
entities; a file that defines its blocks after its entities reads the same. `ExpandBlocks = false` leaves the INSERTs
out, counted in `Skipped`, and reads the blocks all the same.

```csharp
DxfDocument drawing = DxfReader.Read("frame.dxf", new DxfReadOptions { PlanAs2D = true });

foreach (DxfEntity entity in drawing.Entities) { ... }   // the bolts of the block BOLT placed, each with BlockPath "BOLT"
foreach (DxfBlock block in drawing.Blocks) { ... }        // Name, BasePoint, Flags and its own Entities, unplaced
```

An INSERT places its block as AutoCAD does: the block's base point taken to the origin, the scales along x, y and z
(groups 41, 42 and 43, each 1 where not given), the turn about z (50, degrees, exact for a whole number of quarter turns),
the shift to the insertion point (10, 20, 30, in the system of the INSERT's extrusion), then that system (210). A
MINSERT's array of 70 columns by 71 rows is placed row after row, each cell shifted by its column times the column
spacing (44) and its row times the row spacing (45) along the turned axes, not scaled, as ezdxf places it; cells a
spacing of nought shifts alike are placed once, as ezdxf places them, and an array of more than 1 048 576 cells is not
placed. An INSERT inside a block is placed within it and the two placements composed, to a depth of 64. An INSERT that
cannot be placed is counted in `Skipped` and said in a warning, the rest read: one of a block the file does not define
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
size worked out through such a block is worked out again on the way of each INSERT, not kept from another. Whatever the
working out says, the count of what is placed holds the bound too: an INSERT whose entities would pass it is taken back
whole, its entities and what its placing counted, and counted with the others left out. That file is read in 1.4 s on .NET 8, holding 340 MB, where it ran out of memory; the one of three
levels places its 1 000 000 lines in 2.5 s, holding 685 MB with what its blocks read on their own place.

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
entity of a block that is not read is counted each time a block places it, so a TEXT in a block placed 100 times counts
100, and what the blocks read on their own leave out is not counted. Reading 10 000 INSERTs of a block of 50 entities
(12 lines, 10 circles, 10 arcs, 8 closed polylines with arcs, 5 splines and 5 points; 1.9 MB of text) gives 500 000
entities in 0.50 s on .NET 10 and 0.55 s on .NET 8, the document holding 157 MB with the groups or without them, since
the places of one entity of a block share its groups; scaled unevenly, the 820 000 entities they give take 0.96 s and
1.00 s and 448 MB.

`DxfWriter.Add(entity)` writes a placed entity as it is, in the drawing itself, with its look: the file written has no
blocks, and reads back as the same shapes, every number to the bit but the angles of an arc and the axis of an ellipse.
