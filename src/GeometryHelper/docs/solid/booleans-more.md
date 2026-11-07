# Combining bodies: far out, openings and touching

The booleans of [Combining bodies](booleans.md) on bodies far from the origin, with walls a fraction of the tolerance
apart, with openings cut at once, and how near two bodies are to touch.

## Bodies far out are worked out near the origin

A union, a common part or a difference, and each cut of
`TrySubtractAll`, of bodies reaching more than 2^20 (a little over a kilometre) and more than 1 024 times the size of
their box out, as at national-grid coordinates (VN-2000 northings run 1.1 to 2.6 thousand million millimetres), moves
them by whole coordinates, with their openings, so that the least corner of their box stands a 256th of their reach from
the origin; works the boolean out there as it is worked out near the origin; and moves what it gives back. Each corner
less the origin is exact, so a corner of the bodies kept comes back as it was given, to the bit, and only the corners
the boolean makes are rounded, once, to about a ten-millionth a thousand million out. Where they stood, a corner was read
on a plane within a trillionth of the coordinates, a thousandth a thousand million out, and walls of openings a fraction
of the tolerance apart could not be told apart: the L above a hundred million out less the box held 130 189.39 within a
thousandth, where it holds 130 189.32, and the box less the L 28 457.23, where it holds 28 457.68; both hold it now, to
3E-5 a thousand million out, as do their union and their common part. Of 1 196 unions, common parts and differences of
prisms with two or three such openings and boxes ten million to a thousand million out, 241 were wrong at a thousandth
and 14 are, 413 at a hundredth and 15, and at a millionth 257 wrong and 134 not worked out, 127 and 49; the 14 left stand
a thousand million out with walls 0.000 15 apart. Of 1 200 more, a face of each box a tenth of the tolerance to the
tolerance off the plane of a wall, 3 that were right came out otherwise, each a face exactly the tolerance off, by no
more than 1.9, which a face so placed can move either way. Booleans so moved take 17 to 26 % longer at a thousandth and
a hundredth, and a third less at a millionth. Within a million of the origin, and below a trillionth of
the coordinates a 256th of the reach out (four millionths a thousand million out), a boolean is worked out where the
bodies stand, as before, to the bit: there their corners carry more rounding from where they stand than the frame allows
for, and moved, 58 of 5 292 that were right at nought, a trillionth and a thousand-millionth came out otherwise.

## The queries of a body far out are worked out near the origin too

`TryCutOpenings`, the material its measures read
(`GetVolume`, `TryGetVolume`, `GetSurfaceArea`, `GetCentroid` and `GetMassProperties`), `Section` and `Mesh3.ToCells`
move such a body alone, by the same gate and the same frame, and move back what has a place: the material and a section
by their corners, the plane of a section in with the body, and a grid by its origin, each cell's box and each cell's
material, where its cells start and end and what each holds as they were worked out. A volume and an area are the same
in the frame as out of it. The L above with its openings a thousand million out measured 153 732.16 within a thousandth,
0.52 too much, and with its openings given the other way round 0.26 too little; its 9 cells 50 by 40 by 20 were off by
up to 0.28 each. A plate 200 by 100 by 3 a hundred million out, with two openings up from its floor whose ceilings stood
0.00015 apart, measured 0.525 too much, and a section square to its length through the deeper opening cut 0.0105 too
much, the two ceilings cut in one plane. Each holds what it holds now, to 1.3E-4 a thousand million out. Of 617 prisms
with two or three such openings, ten million to a thousand million out, each asked its volume, whether it can be trusted,
its area, its centroid, its cut, two sections and its cells along the world's axes and its own, 745 answers were wrong
at a thousandth and 8 are, 853 at a hundredth and 8; at a millionth, which the gate lets through up to a hundred million
out, 73 wrong there and 8. The answers left at a thousandth and a hundredth are of walls 0.000 15 to 0.0015 apart: five
areas 0.02 under to 2.4 over, their volumes right, and two sections 0.0027 and 0.006 off; one of the areas was right and
is 0.0027 over, a wall cut in a step 0.0007 high where it was cut whole, and one grid ten million out lays a cell fewer
than at the origin, as it did. Cells along a star's own axes are left out of the count, since the smallest box round a
star can turn either of several ways. Within a million of the origin, and below the gate's tolerance, nothing changed,
to the bit: 36 729 and 10 764 fuzz lines. Such queries far out take 4 to 12 % longer at a thousandth and a hundredth,
and 10 to 13 % less at a millionth.

## So is where a point, a ray, a segment or a line stands against it

`Locate` and `Contains`, `DistanceTo`,
`SignedDistanceTo`, `GetClosestPointOnBoundary` and `GetShortestLineTo` of a point, and `CollidesWith`, `DistanceTo`,
`GetIntersections` and `GetShortestLineTo` of a ray, a segment and a line move such a body into the same frame, once,
keeping the moved body with it, move the probe in coordinate by coordinate and what has a place back out; a probe near
the body moves exactly and comes back as it was given. A [`GeoPreparedSolid3`](xref:GeometryHelper.Spatial.GeoPreparedSolid3) of such a body is prepared again in the
frame the first time it is asked so, and asked there from then on; its `Material`, `Surface` and `Index`, and what it
weighs another body by, stay where the body stands. Where it stood, the material those questions read was cut and read
within a trillionth of the coordinates: of 636 points half a thousandth in and out from the plate above with ceilings
0.0009 apart, a thousand million out, 12 had another nearest point than at the origin, and of 1 908 segments and lines
from them, 32 and 67 were given other crossings, and none is now; of 2 080 rays from the L with a pocket and a hole a
hundred million out at a hundredth, 9, and of the same rays from its prepared form, 48, and 1 and 1 are, a ray along
the world's Y axis; a thousand million out, 19 and 52, and none is. Of 18 000 points, rays, segments and lines drawn
beside boxes, U's, L's, cylinders, plates and slabs with and without openings, turned about in space and ten million to
a thousand million out, each asked against the same body moved back to the origin exactly, 209 answers at a hundredth,
a thousandth and, up to a hundred million out, a millionth were wrong, besides 1 284 of probes standing the tolerance
itself off a face, and 67 and 9 are: crossings of rays starting on or half the tolerance off a face, given or left a
crossing at their origin, as the frame, a 256th of the reach out, reads the origin by the rounding there. Below the
frame's tolerance, at nought and at a millionth a thousand million out, the body is asked where it stands, as before.
Within a million of the origin nothing changed, to the bit, and neither did any boolean, cut, measure, section or cell
far out: what they ask of a body is asked where they work it out. The first question of a body far out costs the move,
2 to 14 microseconds for the bodies above, and the first of a prepared body far out a second preparing, as long as the
first; then each question takes as long as it did, and a prepared ray question in the frame's index 4 to 13 times less
than its material asked as a body.

## Walls a fraction of the tolerance apart leave no sheet inside the material

The cut of openings parts a body by the
planes of each opening, and between the planes of two walls a fraction of the tolerance apart it leaves cells thinner
than the global tolerance. A face is laid out in its plane within that tolerance however fine the work, and the face of
such a cell, a strip, came to nothing there: it neither took from the face against it nor was taken from, and both
stayed, a sheet bounding no material, which the volume did not notice and the area did. The L above at the origin, its
openings given down first, kept a sheet 0.0009 by 13 under the opening down from the top and measured 0.0234 too much;
the plate with ceilings 0.00015 apart kept one 0.00015 by 100 and measured 0.03 too much, at a hundredth, a thousandth
and a millionth alike. A strip still lying against a face once the gluing has taken what it could is read again with
that face, both laid out within the tolerance of the work, and what they share goes from both. Of 4 500 prisms with two
or three openings whose walls stood 1E-5 to 0.002 apart, within a thousand of the origin, turned about in space or not,
576 areas were wrong at a hundredth and at a thousandth and 579 at a millionth, and 2, 2 and none are: the two a
thousand out, turned, with walls 1.1E-5 and 1.35E-5 apart, cut within 1E-7, where a strip that thin so far out cannot be
built flat, 0.00035 and 0.00053 over where they were 0.0017 and 0.0014. A plate 300 by 200 by 12 turned about in space,
with twelve openings 22 by 22 a row of which stood the tolerance off the walls of the others, kept sheets of 0.019 to
0.144 square millimetres in 23 of 69 cuts at a thousandth, and 5 of those held 0.024 to 1.46 too little; each holds and
measures what it should now. The booleans glue their cells the same way, and of 25 200 of such prisms and boxes, 3 at
the origin that were not worked out are right now. More than a million out a strip is read so only within a tolerance
eight times what rounding can move a corner off a plane there. A body's own queries far out are worked out a 256th of
its reach from the origin (see above), and of 3 600 cuts of such prisms ten million to a thousand million out, 76 areas
wrong at a hundredth and at a thousandth and 104 at a millionth are 5, 5 and 59; the plate with ceilings apart a
thousand million out keeps its sheet, its strips too thin to be laid flat 3.9E6 out. A boolean asked below the gate is
worked out where the bodies stand, within the rounding, and its strips are left: read there, 13 that were right came out
wrong. Read as the triangles lying in its faces (`VolumeMethod.Surface`, which `GetMassProperties` sums), the plate with
ceilings apart held 0.175 too much where fans held it as it is, the triangles of a wall whose corners stood closer than
the tolerance along a side leaving a sliver of it out; it holds what it holds now (see the surface mesh under
[Solids and openings](types.md#solids-and-openings)). A thousand million out, its sheet kept, it holds 0.2 too much, where it
held 0.33: the side of the sheet standing as a face of its own is a seam at the tolerance and has no triangle.

**Openings that come out open cut in at once**, asked at a millionth or below, have the boolean worked out again on the
material `TryCutOpenings` gives, which closes such a cut by a weld or takes the openings out one at a time. The planes of
an opening cut only the pieces of the body its box reaches, and a piece beside one so cut kept a face the other's faces
did not match: a plate 200 by 100 by 3 with two blind openings up from its floor whose ceilings stood 0.00095 apart came
out of that cut open, and the plate less a box across both openings held 22 717.96, 1.05 short, and was not valid, and
the two joined were not worked out. What the operation gives worked out again is taken where what came as before is not
made, not valid, or holds another volume than it, beyond the planar tolerance and the rounding of the coordinates times
its area. Of 12 000 such booleans of prisms with two or three openings whose walls stood 0.000 15 to 0.03 apart and
boxes, between nought and a millionth, the 6 020 within a million of the origin held 221 wrong and trusted, 806 not
worked out and 751 not valid, and hold 38, 55 and 21 now; none that was right moved. Further out, where rounding moves a
corner off a plane by more than a millionth, only what came out made and valid is worked out again: worked out again
wherever it was not, 106 came out wrong and trusted. Above a millionth a boolean is not worked out again.

## Openings cut in at once are cut again where they come out open

Asked below a thousandth, within a million of the
origin, where a boolean's own cut of an operand's openings, each opening cutting the pieces of the body its box reaches,
comes out open or with a piece judged whole, the openings are cut in again with every plane of each opening carried
through every piece it crosses, as the cut once was, so that the faces of pieces side by side match; that is taken
where it closes with no piece judged whole, and given up where it comes to more than four times the pieces. The strip of
a face between two walls a hair apart, the face beside it not cut there, was lost when the two were glued: the plate
above, less the box at a ten-thousandth or a hundred-thousandth, where the boolean is not worked out again, held 1.05
too little and was not valid, and the two joined held as much too little or were not worked out; they hold what they
should now. Of 7 200 such booleans at 1E-5 and 1E-4, the 3 672 within a million of the origin held 96 wrong and
trusted, 60 not worked out and 293 not valid, and hold 83, 4 and none; of 18 000 between nought and a millionth, the
9 180 within a million held 218, 226 and 56, and hold 203, 210 and 29. One prism of each set, close to a million out,
had booleans that were right come out wrong, by 6E-5 and 0.0034. Further out nothing moved, to the bit: there booleans
of bodies so cut again came out right 1 700 times more but wrong 27 times where they were right, by as much as two
tenths. At a thousandth and above nothing moved either: cut again at a hundredth, a plate 300 by 200 by 12 whose twelve
openings stood the tolerance inside its faces, joined to a box at its corner, held 106 too much.

## A body keeps the openings its boolean cut in at once

Asked at a millionth and below, a boolean cuts the openings of
each operand in at once and then asks whether that cut came out open, which cut them again the same way within the same
tolerance; the body keeps the cut, so that the second is read from it. Of the four booleans of 60 of the prisms above
and boxes, at a millionth 308 of 549 such cuts are read so, at 1E-9 303 of 621 and at nought 283 of 755: the booleans
give what they gave, to the bit, in a quarter less time at a millionth and a sixth less at 1E-9.

**Within a few roundings of the coordinates** a union, a common part or a difference reads a corner as on a plane within
what rounding adds to the planar tolerance, a trillionth of the size of the coordinates, as the cut of openings does:
where the planar tolerance is below that trillionth and the point tolerance no less than four tenths of the rounding, a
quadrillionth of the largest coordinate. Within the tolerance alone, rounding put the corners of a face lying in a plane
of the other body on either side of it. A plate 200 by 100 by 3, turned and a hundred thousand out, less a slot 20 wide
across all of it, within its rounding of 9.5E-11, was said to leave nothing, and leaves its two pieces, 54 000; a hundred
million out, less a notch 20 by 20 through one end, within 4.9E-8, it came back closed and 320 short, and holds 58 800;
and a thousand out, joined to a box 50 by 20 standing through it within half its rounding, it held the two whole, 77 000,
and holds 74 000. A union is taken so where it closes wound alike and holds the two less what they share read the same
way, and is worked out within the tolerance alone otherwise. At a tolerance at or above a trillionth of the coordinates
nothing changes.

**Further below the rounding**, below four tenths of it, nought among it, a union, a common part or a difference is
worked out as though asked at the rounding, read as above, and what it gives is welded to close within the tolerance
asked, its corners a rounding apart made one, as `TryCutOpenings` welds its material. Within the tolerance alone the
point tolerance held too little of what rounding moves: at nought such a plate at the origin, turned, less a hole 20 by
20 through it, came back whole, 60 000, and was trusted, and leaves 58 800; a million out, it and a slot 100 by 10
along it were said to share nothing, and share 3 000; and a thousand out, joined to the box 50 by 20, it held
68 588.24, and holds 74 000. Of 71 820 of each, plates and blocks less, sharing and joined to each of their openings,
turned at random at the origin and a thousand to a hundred million out, at nought and a tenth to four tenths of the
rounding, every difference and common part, and every union but 6 not worked out, 5 of them as before, is right and
valid at the tolerance asked, where 17 489 differences, 11 373 common parts and 9 785 unions were wrong or said to be
nothing and trusted. A body open at nought, a cube or a U with cracks up to 0.0001 wide in its faces, is as open within
the rounding, and what a boolean of it gives is as a rule not valid, holding what an open body holds, which depends on
where it is measured from. Nothing of it is checked on the way, as what two closed bodies give is, so it is taken only
where it holds up; where it does not, what the boolean within the tolerance alone gives is taken where that holds up by
the same checks, and the boolean is not worked out otherwise. A difference or a union holds up where it adds up with
the common part of the two, itself holding up as a common part does, the volumes read as `GetVolume` reads them, each
from a corner of its own: read from anywhere in its box, an open body's volume moves by no more than a third of its open
area, the faces' areas summed with their normals, times the diagonal of the box, and the result and the common part,
which share its open faces, by as much again, and that is what the volumes may miss by, with what the faces lying off
flat move the volumes that enter: a third of how far a corner of each face stands off its plane, times its area. A
difference said to be nothing holds up only where the first body adds up with the common part so. A common part said
to be nothing holds up only where the skin of neither body runs into the other, and a common part only where no corner
of it lies outside the body that closes; of two bodies neither of which closes, each is read as though it did, and a
common part holds up only where it has no corner outside either and leaves out no corner of one lying inside the other.
At nought such a cube 10 across, cracked along its top, its bottom and a side, less a box 3 across turned inside it,
came back whole, 999.998, where 973 is left; turned and joined to a box through its corner it held 1 232.638, where the
two hold 1 107.64; and its common part with a slab leaning through it was the whole cube, where they share 76.79. None
of the three is worked out now. Less a slab thousands long through it, sharing 249.85, the cube came back whole too,
and was kept, the boolean within the tolerance alone giving something as wrong; it is not worked out now. A U 30 by 10
by 10 and a box through its notch, both cracked 1.5E-5 wide, came out sharing 3.66 of the 34.02 they share, and their
common part is not worked out now either; such a U less a box dipping 0.81 into its notch's floor was said to leave
nothing, where it leaves 2 402.34, and is not worked out now. Joined to a slab a million long through it, its faces
off flat by what rounding leaves where planes a millionth of a radian apart meet, the cube holds what the two do within
0.01, 59 169 796.05, as it did before the check; the slab read whole is 0.57 short of what it holds. Of 17 178 booleans
of such a cube and a body through it at nought, 742 were made more than 0.01 from what the uncracked cube gives, the
0.01 the cracks move, and 126 said to be nothing; 14 unions are still made so, with slabs thousands long whose faces
off flat move the slab's own volume by more than they miss, and 4 of 15 698 made within 0.01 are not worked out now. Of
36 000 of a U so cracked and a box, 5 381 were made wrong and 799 said to be nothing; 36 are still made wrong, and 231
of 13 630 made within 0.01 of the uncracked U, none of them valid at nought, are not worked out now.

## Exactly the tolerance into or apart counts as touching

Two bodies whose faces lie the tolerance into or clear of
each other, or nearer, lie so to a few bits either way once turned, and a boolean reads them as touching there, closed
across the gap or the slab: their common part is empty, each difference leaves its body whole, and their union holds
what the touching skins allow, the two less the overlap or the two and the gap filled, within the tolerance times the
area where they lie face to face either way, since each face of it is within the tolerance of where it belongs; where
in that band it falls is left to the rounding. Further into each other than the tolerance, the two are cut as they
are, within the same band. Of 1 600 columns 300 by 300 by 1 000 joined by beams 100 to 400 wide and high running 0.8
to 2 tolerances into or clear of their sides, turned at random and as far as 700 metres from the origin, every union,
difference and common part at tolerances of 0.001, 0.01 and 0.05 holds so, 24, 133 and 132 of the 8 000 of each within
the band rather than the exact reading. A body a boolean gives at a tolerance coarser than the default is welded to
close at the default too where that can be done, and a weld moves no corner further than the tolerance; a weld that
tilted the column's face by the tolerance had a beam 160.1 wide and 160.8 high standing exactly 0.01 clear of the
column hold 661 more than the two and the gap, 2.6 times the tolerance times the area where they face each other, and
one 129.2 wide and 366.7 high running exactly 0.01 into it, joined beam first, 605 less than the two less the slab, 1.3
times. Welded with the corners the column's face lies round weighed by the face (see above), they hold the two and
214.5, the gap filled holding 257.4, and the two less 394.9, the slab being 473.9.

## Parts drawn against each other

The tolerance says how exact a boolean is; how near two faces of two parts
have to be to touch is a question of the model. Tekla Structures draws parts against each other a few thousandths
of a millimetre into or off each other, and cut within a thousandth, a part whose cutter's face stood 0.0049 inside
its own keeps a skin of itself that thick: 110 square metres of surface on a part of 119. [`SolidBooleanOptions`](xref:GeometryHelper.SolidBooleanOptions)
says how near is touching: each face of the second body lying parallel to a face of the first with every corner
within `Contact` of its plane is put onto that plane first, and the first body is not moved.

```csharp
var options = new SolidBooleanOptions(Tolerance.Default, contact: 0.01);
part.TrySubtract(cutter, out GeoSolid3 left, options, out BooleanOutcome outcome);
```

A net body, a part less every part it meets, is `TrySubtractAll`: each cut checked as `Validate` checks a body, but
reading it as the booleans do, within the tolerance as given and the edges paired by overlap only; one whose result
is not valid worked out again within the `fallback` tolerance where one is given, and one that is still not valid
skipped, the part kept as it was before it, with the report saying which. A body closed with two faces lying one on
the other the same way round holds the wrong volume, which is why each is checked. Checked by hand after a
`TrySubtract`, `Validate().IsValid` reads a few more results valid than this check does: at a tolerance of nought,
those closed only to the rounding of their coordinates, 1 523 of 54 000 booleans of U bodies within nought and 1E-12
that it read not valid before.

```csharp
var options = new SolidBooleanOptions(Tolerance.Default, contact: 0.01,
    fallback: new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01));

if (part.TrySubtractAll(cutters, out GeoSolid3 net, options, out SubtractReport report))
{
    double volume = net.GetVolume(options.Tolerance);
    double area = net.GetSurfaceArea(options.Tolerance);
    // report.Skipped: the cutters not taken away, by their place in the list.
}
```

Of the 79 864 parts of a Tekla model each cut so by the parts it meets, 3 344 came out within a thousandth alone
with more than 1 % more surface than within a hundredth, 5 466 square metres in all, and 203 with a contact of a
hundredth, 220 square metres. A contact of two hundredths starts to take what is there.

A cut skipped can be closed instead. Given [`SolidClosingOptions`](xref:GeometryHelper.SolidClosingOptions) as well, `TrySubtractAll` closes a cut it would skip
with `TryClose`, the cut within the tolerance first and then the one within the fallback, and takes the body closed only
where it is valid within the booleans' tolerance and holds what a cut can leave: no more than the part before it, and no
less than that less the whole cutter, each within the tolerance times their area. No cut is closed where the part before
it or the cutter carries openings: an opening cut out of a body not closed takes material before any closing can, so
such a cut is skipped as without closing. A part not valid as given is closed at its first cut that would be skipped,
even by a cutter that does not reach it. Every other cut is taken as without it, bit for bit. `report.Closed` lists the
cuts taken closed, `report.Closings` what the closing did to each, and `report.RefusedByVolume` those that closed
holding the wrong volume, which stay skipped:

```csharp
var closing = new SolidClosingOptions(Tolerance.Default, 0.005, maxHoleArea: 100.0, maxOffFlat: 0.01);

if (part.TrySubtractAll(cutters, out GeoSolid3 net, options, closing, out SubtractReport report))
{
    // report.Closed: the cutters taken away closed; report.Skipped: those still not taken away.
}
```

Of the 17 parts of that model with a cut skipped, 35 cuts in all of their 314, closing so takes 9 and leaves 10, the
cuts after one closed taken where they were skipped: the 8 parts whose bodies cut within a hundredth are valid come out
within 0.12 % of those where they were up to 10 % off, the 0.12 % a part no closing changes, and no cut is refused for
its volume.

## Flat shapes and boxes

Two areas in one plane are combined by the plane library and the answer lifted
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

Everything comes back as [`GeoFace3`](xref:GeometryHelper.Geometry.GeoFace3) because joining two areas can leave a hole in the middle and only a
face can hold one — four bars round a square give one face with one hole. **A shape that does not lie in
the first one's plane is refused**, with an `ArgumentException`: projecting it in would report two plates a
metre apart as overlapping and say nothing about it. The whole of the shape has to lie in the plane, every
corner and the middle of every arc: a plate turned half a degree about one of its corners is refused too,
though its plane is parallel within the angle tolerance and passes through that corner. `SharesPlaneWith`
asks beforehand, on [`GeoPolygon3`](xref:GeometryHelper.Geometry.GeoPolygon3), `GeoFace3` and [`GeoPolygonArc3`](xref:GeometryHelper.Geometry.GeoPolygonArc3) alike.

## See also

- [Combining bodies](booleans.md).
- [Clashes and large meshes](clash.md).
- [Volume take-off](../takeoff.md).
