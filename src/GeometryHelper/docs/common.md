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
| `EqualPoint` | `1E-2` | Distance below which two points are the same point. |
| `EqualVector` | `1E-2` | Difference below which two vectors are the same vector; also the length below which a vector has no direction, and the area below which a loop is no polygon. |
| `EqualAngleRad` | 1° | Angular difference for parallel and perpendicular tests. |
| `EqualPlanar` | `5E-2` | Distance from a plane below which a point counts as lying on it. |

**The defaults suit a model in millimetres:** a hundredth of a millimetre for points, and five
hundredths for flatness, which lets through the faces a modeller's own cuts leave a little out of
flat. Tekla Structures left the top face of a notched beam 0.04 mm out at one corner; refused as not
flat, that face was a hole in the beam, which then gave no section and a fifth too little volume.
Geometry in metres wants the defaults a thousand times smaller, and a tolerance of its own:
`new Tolerance(1E-5, 1E-5, Tolerance.DefaultEqualAngleRad, 5E-5)`.

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
wider than `EqualPoint`, so that the glue closes whatever a cut leaves, and first splits a face that
is flat only to the wider `EqualPlanar` into triangles on its own corners.

**Whether two things cross is not a question of angle.** `EqualAngleRad` answers `IsParallelTo` and
`IsPerpendicularTo`, which are about directions. Where two members cross is a point, and two members
are parallel for that purpose only when they draw apart by less than `EqualPoint` along the longer of
them; a flat shape meets a plane wherever it stands off it by more than `EqualPlanar`. So two
ten-metre members crossing at a tenth of a degree cross at a point, though `IsParallelTo` calls their
directions parallel. Only two infinite lines, or two planes, have no length to measure over, and those
still go by the angle.

```csharp
// One setting for both libraries: here, for a model in metres.
Tolerance.Global = new Tolerance(1E-5, 1E-5, Tolerance.DefaultEqualAngleRad, 5E-5);

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
| `ArcTolerance` | `0` | The largest gap allowed between a round corner's chords and the true arc, in drawing units. Zero means 0.2 % of the offset distance, about 50 segments for a full turn. |

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

When an answer surprises, look at the shapes. `GeometryHelper.Export` writes them in three formats, numbers
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
```

Each shape added to an OBJ file is an object of its own under the name given, so a viewer lists them
separately, and coordinates are written exactly. An SVG picture is fitted round everything drawn, and every
line keeps its width however far it is zoomed. WKT rings are written closed, the boundary counter-clockwise and
the holes clockwise, as the format asks.
