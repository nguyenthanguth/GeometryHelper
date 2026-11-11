# Geometric Types

The shapes of space, from points to bodies: their types, flatness, the two kinds of box, and solids with openings.

The shapes split into three families, and which family a shape belongs to decides what you can ask of it:

| Family | Types | Encloses an area | Encloses a volume |
|---|---|---|---|
| Curve | [`GeoLine3`](xref:GeometryHelper.Geometry.GeoLine3), [`GeoRay3`](xref:GeometryHelper.Geometry.GeoRay3), [`GeoXLine3`](xref:GeometryHelper.Geometry.GeoXLine3), [`GeoPolyline3`](xref:GeometryHelper.Geometry.GeoPolyline3) | no | no |
| Planar region | [`GeoTriangle3`](xref:GeometryHelper.Geometry.GeoTriangle3), [`GeoPolygon3`](xref:GeometryHelper.Geometry.GeoPolygon3), [`GeoCircle3`](xref:GeometryHelper.Geometry.GeoCircle3), [`GeoEllipse3`](xref:GeometryHelper.Geometry.GeoEllipse3), [`GeoFace3`](xref:GeometryHelper.Geometry.GeoFace3) | yes | no |
| Volume | [`GeoObb3`](xref:GeometryHelper.Geometry.GeoObb3), [`GeoAabb3`](xref:GeometryHelper.Geometry.GeoAabb3), [`GeoSolid3`](xref:GeometryHelper.Geometry.GeoSolid3), [`GeoSphere3`](xref:GeometryHelper.Geometry.GeoSphere3) | yes | yes |

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

## Flatness is enforced

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
of a degree of tilt into a deviation of nearly a millimetre. Three corners are flat whatever the
tolerance, since they always share a plane.

The faces of a body a modeller gives as loops of corners can still come out of flat: Tekla Structures
left the top face beside a notch in a beam 0.04 mm low at one corner. Refused, such a face is a hole in
the body, which then has no section and the wrong volume. `GeoFace3.FromLoops` reads a face from its
loops of corners: one face, holes and all, where they lie flat, and otherwise triangles on the corners
themselves, which keep every edge the face shares with its neighbours, so the body stays as closed as it
was given and no corner moves. A hole off the plane of its boundary is kept the same way. The Tekla
and IFC conversions read faces through it.

```csharp
// One corner 1 mm up: out of flat at the default tolerance, so two triangles on these four corners.
GeoFace3[] faces = GeoFace3.FromLoops(
    new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 1), new GeoPoint3(0, 100, 0) },
    holes: null);
```

Self-intersection is a different matter and is *not* checked on the way in, because the check costs more
than building the polygon does. `polygon.IsSimple()` runs it when you want it: no edge crossing or
touching another except where neighbours share their vertex. A polygon that is not simple is still read
consistently, under the even-odd rule, but its `Area` no longer means what it says. One whose lobes
cancel exactly, a symmetric bow tie, is refused at construction all the same — with no net area it has no
normal, and without a normal there is no plane to be flat in.

A polygon's centroid weighs its area however small, and a face's its holes: the triangle (10, 20, 30),
(10.0009, 20, 30), (10, 20, 30.0009), of 4.05E-7 square units, built within a millionth, balances at
(10.0003, 20, 30.0003), where an area of a millionth or less was taken for none and its first corner given; the square
0.001 by 0.001 standing in the plane y = 0 with a hole 0.0004 by 0.0004 in its corner balances at
(0.000538, 0, 0.000538). Only an area lost in the rounding of the sums it comes from is none.

## Two kinds of box

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

A box has its six faces, `GetFaces()` and `ToSolid()`, however thin, read from its own numbers rather than against
the global tolerance: a plate 0.002 by 50 by 30 is a body, where it was refused as having a zero size on some axis,
and only a side no more than a millionth of a millionth of how far the box reaches from the origin is nought; it reaches
every query that goes through its solid, and `IsDegenerate()` calls it flat only where a whole side is no longer than the
point tolerance: the plate 0.0015 thick is a box at a tolerance of a thousandth, where half its side was read and it was
flat, and one 0.0005 thick is flat. A box with a side of nought, as the bounding box of a flat shape, has no faces and
`GetFaces()` throws, but no `Try` method does: an arc, a circle or an ellipse meets a box flat on one axis where it
passes the rectangle across it, and a box flat on two axes or three nowhere; a triangle, a polygon, a face or a plane
against a box flat or empty, and a boolean with a flat box, answer false. `GetIntersections` of a triangle, a polygon,
a face or a plane with such a box gives no points, as the `Try` method answers, where it threw: the plane x = 20 and
the box 100 by 100 by 0 cross nowhere. A box,
oriented or not, gives crossings only where a shape comes within the point tolerance of it, as `CollidesWith` reads
it: a segment along X standing 0.000714 off both faces past an edge of the box 10 by 10 by 10, 0.00101 from it, crosses
nothing, where it was given two points, and so for a ray, a line without ends, a chain and an arc ending near a corner.
The other way round, a segment, a ray, a line or a chain that touches the box within the point tolerance without crossing
a face is given the point of it nearest the box: the segment from (5, 10.1, 9.9007) to (5, 9.9007, 10.1), 0.0005 off the
edge where the faces y = 10 and z = 10 meet, is given (5, 10.00035, 10.00035), where it collided and was given nothing.
The point is the same whichever way round the shape is given. One lying wholly inside the box collides and crosses
nothing, as before, and so does one lying along the box within the tolerance, within the planar tolerance of the plane
of the face it stands over or the line of the edge it runs with all along, as the body of the box and an arc in the same
place read it, a curve lying on a surface crossing it nowhere: the chain 0.0005 over the top face is given no point, and
the segment 800 long from 0.0006 over the top face, tilted 9E-6 and so rising 0.0072, touches it at its low end. A ray starting within the
tolerance of the box and running away from it touches it at its origin, and so does one whose short cut stands within
the planar tolerance of the face it starts beside, though no part of it longer than the point tolerance lies level over a
face or along an edge: with a point tolerance of a hundredth and a planar one of five hundredths, the ray from 0.0075
beside a side of the box 100 by 80 by 50, 0.005 from its edge and running away from both, touches it at its origin,
where it collided and was given nothing. Cutting a chain by a box cuts only where it crosses a face, since a touch cuts
nothing.

## Solids and openings

A `GeoSolid3` is the set of faces bounding a body. An opening is a whole solid subtracted from it — a duct
through a slab, a recess in a footing — which is not the same thing as a hole in a `GeoFace3`, which is
flat and belongs to a single face.

```csharp
GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
GeoSolid3 duct = new GeoAabb3(new GeoPoint3(4, 4, 4), new GeoPoint3(6, 6, 6)).ToObb().ToSolid();

GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

pierced.GetVolume();       // 992 — the duct cut out
pierced.GetSurfaceArea();  // 624 — the outside, and the walls of the duct
pierced.GetCentroid();     // (5, 5, 5)
pierced.IsClosed();        // true

pierced.Locate(new GeoPoint3(1, 1, 1)); // Inside
pierced.Locate(new GeoPoint3(5, 5, 5)); // OutSide — inside the duct
```

`GetVolume`, `GetSurfaceArea` and `GetCentroid` measure the material: the faces with every opening cut out, the
walls of the openings part of the surface. A body without openings — every part `GeometryHelper.TeklaConvert`
reads is one, its cuts already in its faces — is measured by its faces at once, and nothing is cut. A body with
openings has them cut out once for the tolerance, and the cut is kept, so the three, `GetMassProperties` and
[`Measure3`](xref:GeometryHelper.Core.Measure3) describe one material for the price of one cut. An opening that cannot be cut out is left in the material
and the log warns of it; `TryGetVolume` says whether the volume can be trusted, every opening cut out and the
material closed. Cut in at once, the body is parted by the planes of its openings and each piece is judged by one point
of it, so a plane that runs through a piece and will not cut it, leaving it part in an opening and part out, has the
openings taken out one at a time by a difference instead: a half of a hull cut near a face within a hundredth, a box
3.5 by 3.5 by 5 standing through the cut, held 1 465.33 cubic millimetres with the box left in, and `TryGetVolume`
trusted it; it holds 1 434.58 now, where the hull less the box holds 1 434.77 on that side of the plane. An opening
that will not come out so, or is said by the difference to take all that is left where the cut at once left material,
has the openings cut as `TryCutOpenings` cuts them before it is left in: a plate 200 by 100 by 3 turned about in space
and a million out, with a notch 20 by 20 through one end and one 20 wide and 1 deep across the other, flush with three
faces, measured 58 800 at the rounding of its coordinates, the second notch left in, and measures 56 800. A union, a
common part or a difference reads a cell the same way where a plane of the other body, or of either body where both are
cut by every plane of both, runs through it and will not cut it: where both bodies close, by the middles of its parts
either side of each such plane, of the four two make and of the eight three make, each read where it lies in the cell's
material; where one is to be kept and another not, the boolean is worked out another way or reported as not worked out.
A cube 10 across whose faces carry cracks 0.0001 wide, less the corner of a box none of whose three planes will cut it,
held 1 000 cubic millimetres with the box left in, and holds 874.94 now. Where one such plane crosses a cell, or two,
or three that meet at a point within the other body's box, or three of which two are parallel and the third runs across
them, the parts all of them make are read too, each by its middle wherever it lies, since no face of the other body runs
between a part and its middle: a U 30 across whose bottom and sides are cracked under the sides and the floor of a box
dipping 0.088 into one of its arms was left whole, 2 667.83 cubic millimetres where 2 665.28 is left, the part the box
takes held by no part one or two of its planes make without more of the U beyond the box; it holds 2 665.15 now. A cell
crossed so by more planes, by three meeting outside the other body's box or along one line, or by three parallel, is
read as before, and can still be judged whole. A common part judged so whichever body is cut, and a union judged so
whichever body's cells are kept, are reported as not worked out; a union judged so one way only is taken where the
union cut the other way round, the second body whole, holds as much. What such a cell puts on the wrong side of the
planes is weighed too, by the parts all of them make judged otherwise than the cell, against the tolerance times the
area of what the boolean gives; more than three planes are weighed so where they fall into no more than three families
of parallel planes, as the four sides of two boxes and the floor they share do. A cell that cannot be weighed is read as
before. A difference with no other result to take is not worked out where that is more, unless
the other body's cells within it, cut by the same planes, weigh less: the cracked box less that U, the box's cells
judged so by the U's planes, held 478.10 where 475.54 is left, trusted, and is not worked out now, 2.55 of it on the
wrong side where the tolerance times the area of what is left is 0.41. A cut judged alike on both sides of the planes
is weighed so as well: a box dipping 0.028 into an arm of that U, less the U, was left whole, 0.84 over where the bound
is 0.45, the U's 0.84 beyond the box no more than the tolerance times the U's area, and is not worked out now; and of
two ways of cutting a common part, the one that weighs more than that is not taken where the other weighs less: a box
whose floor lies 0.014 under the floor of the U's notch, both turned, shares a slab 0.52 with it, which came out as
nothing and comes out whole now.

Most such cells are cut along a crack: a body whose faces meet on two copies of an edge a hair apart closes within the
tolerance and no closer, and where a plane of the other body crosses the crack within a few thousandths, the cut leaves
a gap at the plane wider than it bridges, and the cell whole. So a union, a common part or a difference that is not
worked out is worked out once more where either body closes within the tolerance and not at the rounding of its
coordinates: each such body has its corners within the tolerance of each other made one, as a boolean welds what it
gives, and its faces lying in one plane merged, and the boolean is worked out on what that gives, by the same checks.
The cracked box less that U, its cracks 1.7E-5 wide and slanting 0.004 either side of the U's planes, leaves 475.54
now, as it should, and the two share 2.55 and join into 3 143.38, all three not worked out before. Of 45 000 booleans of
such a U and a box, both cracked 1E-5 to 1E-4 wide, at a thousandth, a hundredth and 0.05, 1 619 differences, 2 106
common parts and 2 063 unions were not worked out, all but 23 of them at a thousandth, and every one is right now. What
was worked out as the bodies came is as it was, to the bit, and so is what is still not worked out; far out, the bodies
are welded in the frame they are worked out in. At nought a crack is a crack, and nothing is welded.

The volume is measured by the divergence theorem, so it does not depend on where the solid sits and is
reported unsigned: faces wound inwards give the same answer as faces wound outwards, and `TurnOutwards()` turns
such a body the right way out for the booleans and the containment queries, which read the winding. It does
depend on the boundary being closed, which is what `IsClosed()` is for: every stretch of every edge shared by an
even number of faces. A long edge beside two short ones is matched stretch by stretch, and two blocks meeting
along an edge put four faces on it and are closed; a missing face leaves a stretch with one, and a fin
standing off the surface one with three.

`Validate()` says where. It matches the edges as `IsClosed()` does, and gives a [`SolidValidation3`](xref:GeometryHelper.SolidValidation3): whether the faces
bound a body, closed, wound alike and outwards, and each thing found, a [`SolidIssue3`](xref:GeometryHelper.SolidIssue3) with its kind, where it is, how
large, and the faces it is of. A stretch of edge an odd number of faces meet on is an `OpenEdge`. A stretch the two
faces on it run the same way is a `SameWayEdge`: a face wound the wrong way round, which `IsClosed()` counts as
closed. Faces wound inwards are `InsideOut`. Edges four faces meet on, short edges, faces thinner than the tolerance
and faces out of flat are noted, and leave the body valid.

```csharp
GeoSolid3 lidless = new GeoSolid3(slab.Faces.Where(face => face.Normal.Z < 0.5)); // the slab without its top

SolidValidation3 check = lidless.Validate();
check.IsValid;     // false
check.Issues;      // four OpenEdge stretches round the rim of the gap, each 10 long
check.Issues[0];   // SolidIssue3[OpenEdge 10 long at (5, 10, 10), face 2; 1 one way, 0 the other]
```

### Every question takes the openings into account

The faces of a pierced body run straight across its
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
on the rim of an opening the probe never comes near. **A body asked many questions is prepared once** with
`Prepare()` — see [A body asked many questions](asking.md#a-body-asked-many-questions) — or cut once with
`TryCutOpenings` and the result asked instead: the same answers, the cutting paid once. The material of a closed
body with closed openings comes back closed as `Validate` reads it, as a boolean's does, or not at all: where cutting
the openings in at once leaves a crack, it is welded within the tolerance, and where that does not close it the
openings are taken out one at a time by a difference. A plate 300 by 200 by 12 with twelve blind holes ending 0.011
under its top, turned and far from the origin, came back open at a hundredth for 15 of 30 turnings; every one closes
now, within 0.03 cubic millimetres of what `GetVolume` measures. What the differences leave is refused where
`Validate` reads it as not valid, and each reads a corner as on a plane within the planar tolerance and the rounding
of the coordinates, a trillionth of their size, as cutting in at once does. Read within the tolerance alone, with the
twelve holes ending the tolerance under its top, to a few bits, a plate cut within a thousandth came back wound two
ways at its top and was handed back as its material; it holds 666 657.36 now, eleven holes through and one under a
skin 0.0010002 thick. Of 600 such plates, their holes through, flush with the faces, blind, near an edge, or boxes
hidden inside, cut at 1E-3 and 1E-2, 17 came back valid and as much as 157 cubic millimetres off and 16 were refused;
every one of them now holds what its skins can leave. A difference read so that will not come out is worked out
within the tolerance alone, as before. Cut in together, each opening keeps or loses a skin the tolerance thin as it
would cut in alone: twelve boxes hidden 0.01 inside the faces of a plate, one 1.7E-7 deeper than the tolerance and
another 5.7E-6 nearer the faces, came back valid and 9.68 cubic millimetres over the most its skins can leave, the
plane of the deeper box's floor having cut a skin off the whole plate that the nearer kept; it loses its skins now, and
`GetVolume` reads it so too. Walls of two openings, or of an opening and the body, standing apart within the tolerance
of one plane are each cut in their own: the plane of one opening cuts all it reaches, and a plane of another within the
tolerance of it was read as that plane, however far away. An L 100 by 80 by 50, a million out, with two blind openings
27 apart whose walls stood 0.0009 apart across it, cut within a thousandth, held 153 674.08 where it holds 153 673.56,
its section 0.014 off, and with the openings the other way round 153 673.30; such openings are cut within a finer
tolerance, between that gap and the most that walls meeting within the tolerance, or a face off flat, stand off one
plane, where the body and its openings close within it and what it gives closes within the tolerance asked, and
`GetVolume`, `Section` and `Mesh3.ToCells` read it so too. Walls that meet are made one plane, as before. `Triangulate` still
meshes the faces as they are; `TriangulateSurface` is the mesh to read as the boundary.

A triangle of the surface mesh is left out only when its corners lie on a line within a millionth of the point
tolerance; a face 0.002 by 0.002 is its two triangles at the default tolerance. A triangle has no area,
`IsDegenerate(tolerance)`, when its height over its longest side is within the point tolerance, or its corners lie on
one line to rounding: each half of that face, 0.0014 high, is a triangle, with its `Normal` and its `GetPlane()` and
read as a face, where they threw; one 100 by 0.0008 has no area at the default tolerance and keeps its normal all the
same. `Normal` is read from the triangle's own numbers and throws only for corners on a line to rounding. A polygon
under the vector tolerance in area is kept where it has width in the same way, so a square 0.002 by 0.002 is one; the
meshes read no area as they did, and the queries of a triangle read it by `IsDegenerate` (see
[how near is touching](shapes.md#how-near-is-touching-in-space)). A face thinner than the tolerance is a seam at
it and has no triangle: a slab 100 by 100 and 0.0008 thick, built within 1E-5, is 12 triangles at 1E-5 and 4 at 0.001,
its four sides gone, and a Debug line in [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) says how many faces were left out whole and how much area
they had; a face meshed in part is not counted. Such a mesh is closed within the tolerance, the top and the bottom
meeting where the sides were, but not edge by edge: the slab's 4 triangles leave 8 edges with no triangle on the other
side. Triangulate at the tolerance the body was built within for a mesh closed edge by edge, the slab's 12 triangles at
1E-5.

Short of that, the surface mesh of a face covers what the face covers, to rounding, however near its corners stand to
edges they are not on. A face whose rings come within the tolerance of each other is cut into strips, and the corners of
each ring were read within the tolerance first: the wall of an opening 50 by 1.50015, with corners at 1.5 up both its
sides where the ceiling of another opening 0.00015 lower met it, had those corners merged, one kept on each side, and was
meshed 0.00375 short at a thousandth; a notch 0.0002 wide was closed, and a corner a hundredth of the tolerance off a
straight run dropped. The plate the wall was cut from, read as the triangles lying in its faces, held 0.175 too much. The
corners are read within rounding now, so the wall is its 75.0075 and the plate holds what it holds. Where the clipping is
left with a needle of a face, every ear of it blocked by corners within rounding of its sides, the face is cut into
strips too, where the needle was dropped: a plate 81.43 by 17.44 with a hole 0.0000016 above its bottom lost 0.000066 at
a millionth.

## See also

- [Asking a body](asking.md).
- [Asking every shape](shapes.md).
- [How do I](../howto.md).
