# Closing, making, weighing and merging

## Closing an open solid

A body read out of a model can come in open: two faces meeting on copies of an edge a few thousandths apart, a
corner standing off the edge of the face beside it, a face left out. `Validate()` says where. `TryClose` closes it with
the least change that does it, and says in a [`SolidClosing3`](xref:GeometryHelper.SolidClosing3) what it changed; where the change is not certain it
changes nothing, returns `false`, and says why and where. The body given is not changed, and one valid already comes
back as the very instance, nothing done. Valid here is as the booleans read a body, within the tolerance as given and
the edges paired by overlap along a line only, not as `Validate()` reads it, which pairs two copies of an edge by
their ends as well: a slab 100 by 100 and 0.0012 thick whose side at one end is slid up 0.0002, valid to `Validate`,
is refused within a thousandth, the gap too wide, and one 0.0014 thick slid by 0.0005 is welded closed, 14 cubic
millimetres moved to 11.67.

```csharp
var options = new SolidClosingOptions(Tolerance.Default, maxGap: 0.01, maxHoleArea: 50000.0);

if (part.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report))
{
    double volume = closed.GetVolume(options.Tolerance);
    // report.Repairs: each change, in the order it was made; report.AddedArea; report.VolumeChange
}
else
{
    // report.Failure says why and report.FailureLocation where; nothing was changed
}

part.TryClose(out closed, Tolerance.Default, 0.01);   // welds within the gap, and fills no hole
```

**The gap has to be given.** `MaxGap` is how far apart the corners across a gap may stand and still be made one, and
how far a corner may stand off an edge and still be put on it. It is a question of how the body was made, not of the
tolerance it is judged within: a boolean cut within a hundredth closes within a hundredth, so read within the default
thousandth it can be open by copies of an edge up to a hundredth apart, which a gap of a hundredth reaches. A face left
out is another matter, which no weld closes.

**Nothing is filled unless asked.** `MaxHoleArea`, the most a hole may enclose and still be filled, is nought by
default, and `double.PositiveInfinity` fills any. `MaxOffFlat`, how far out of flat a hole may stand and still be filled
by triangles, is nought by default too, so that only holes flat within the planar tolerance are filled. `Fill` says
which holes are: `FillStrategy.WhenUnambiguous`, the default, only those that can be filled one way as far as the
volume goes; `MinArea` every one that may be, by the way adding the least area; and `None`, none.

**The steps**, each taken only where the ones before did not close the body:

- **Clean.** A face covering nothing within the tolerance is dropped, a face given twice is taken once, and of a face
  and a copy of it turned over, the one wound against the faces round it goes. A face no other face runs an edge of,
  lying back to back on a face of the body, is a sheet of no thickness, and goes too. The faces are then turned so that
  each shell is wound alike, and each closed shell inside no other outwards, with every shell inside it: a shell inside
  another keeps its winding against it, a block within it wound the same way and a cavity wound the other, since both
  read valid. An inside-out box comes back turned, six `Flip`s, and a box with a box inside, both wound outwards, one
  face of the outer turned, gets that face alone turned back. The stretches of edge longer than the gap say first which
  way a face faces, and a shorter one only where it is the whole of an edge of both its faces, so that copies of an
  edge a hair apart turn nothing over. A shell still open keeps its winding until the welds or a fill close it, and a
  shell within the box of one still open waits for that one to close; a shell enclosing no volume, as a face on its own
  does, is left as it is.
- **Fins.** An edge left open past a fin, a face standing off the surface, stops the closing there: a face across the
  loop round it would be the fin again the other way round.
- **Welds.** The whole body is welded shut before any loop of it is read as a hole. The corners of the edges left open
  are made one within the point tolerance, then twice it, four times and so on while below the gap, and within the gap
  last, the first reach that closes the body taken, so that no corner moves further than it has to. A box's top with a
  corner moved 0.003 out in its plane is welded back within a gap of 0.005, the copy moving 0.003 onto the corner the
  faces beside it keep; beside a chamfer whose ends stand 0.07 apart, a corner moved 0.0015 is welded within two
  thousandths and the chamfer is kept, though the gap allowed is a tenth. The nearest corners go first, each group to
  the corner of it that bends the faces round it least, each face weighed by its area, so that no weld tilts a large cap.
  Only corners of edges left open move, so a slot 0.0035 wide cut into the box stays a slot while a corner 0.003 off
  elsewhere on it is welded within 0.004; and two corners of one face are made one only where they are copies of one
  corner side by side on its ring across an edge left open, so that a slot whose mouth opens beside a gap keeps its
  mouth. A face welded stays one face where its corners lie flat about their middle, so a cap of a thousand corners
  tilted by a hair is not broken into triangles. No reach is taken that welds a face through another, an edge of either
  reaching through the other by more than the gap on both sides where the faces they were built from did not cross: a
  strip 0.004 wide left along a top, a blade 0.002 thick standing in it, is not welded shut through the blade, and the
  body is `StillOpen` at the blade, the gap not too wide but blocked; a blade standing through the top away from the
  strip is the body's own, and the strip is welded shut beside it. What a reach leaves open is read again only where
  its faces changed: a sphere of 6 240 faces with one copy of a corner moved 0.003 is welded closed in a second under
  .NET Framework 4.8 and a fifth of one under .NET 10, most of it in checking the body as given and as closed.
- **Corners on edges.** Only where no reach of welds alone closes the body is a corner standing within the reach of an
  edge left open, between its ends, put on it, the edge split there: moved onto it within the corner's own face where
  the crack lies in that face's plane, and the edge bent through the corner where it stands otherwise. No reach is
  taken that leaves a ring running out to a corner and straight back, or a face lying back to back with another, both
  of which read valid and are no body; nor one whose faces sweep out more volume than the reach times their area.
- **Flat holes.** What is left open is followed round into loops. A loop flat within the planar tolerance is filled by
  one face on the body's own corners, bit for bit, the loops lying in its plane inside it the face's holes: a box 30 by
  20 by 10 missing its top is filled by one `Fill` of 600, and a plate with a hole through it missing its top by the top
  again, its hole and all, 800. Two such loops at the two ends of a hole through a shell whose walls are missing,
  facing apart and turning at as many corners, can cap it or be joined by walls round it, straight, slanting or
  tapering, each wall flat and convex and crossing no face; where both ways lie on no face of the body, as for a plate
  whose hole has lost its four walls, `WhenUnambiguous` takes neither and `MinArea` takes the one adding less area,
  there the caps, 200 against 400.
- **Holes out of flat.** A loop further out of flat than the planar tolerance, and no further than `MaxOffFlat`, is
  filled by the triangles of least area across it on its own corners, of the ways none of whose triangles lies back to
  back with a face of the body or turns back against the loop, folding over the outside of a concave rim: an L-shaped
  top lifted a little at a corner is filled across its inside. `WhenUnambiguous` takes them only where those ways close
  one volume within the loop's area times the planar tolerance, the uncertainty a face called flat carries already: the
  top of a box 100 by 100 with a corner lifted 0.005 is filled by two triangles, which close 8.3 more one way across
  than the other, within a bound of 10, and lifted 0.05 it is ambiguous. A loop of 200 corners out of flat is filled by
  198 triangles in a few hundredths of a second; one of more than 256 is not filled, and nor is one with another loop
  inside it.
- **The check.** Once the fills close the body, every shell is read again for which way it faces, as the cleaning reads
  a closed shell: one inside no other wound inwards is turned whole, with every shell inside it. The body is then
  checked as the welds' is: valid within the tolerance, no ring doubling back, and no face new to it lying back to back
  with another. No fill is taken that crosses a face of the body, or of a fill taken before it, an edge of either
  through the inside of the other, though such a body reads valid: a box missing its top, with a post 4 by 4 standing 5
  out through the top's plane, is `StillOpen` at the post, not closed round it holding 6 208. Caps that would cross are
  no way, and the walls are taken where they cross nothing; a fill that only touches a face, along an edge or at a
  point, crosses nothing.

A gap too wide for the welds is a loop like any other, and filled where fills are allowed and it can be: a strip 0.03
wide missing along the top of a box is too wide for a gap of 0.005, and closed by one face of 20 by 0.03 where
`MaxHoleArea` is at least 0.6. The reason given for a body not closed is that of the first step that could not go on,
where none after it closed the body either.

**What it refuses, and why.** `Failure` says, and `FailureLocation` is a point at the trouble:

| [`ClosingFailure`](xref:GeometryHelper.Enums.ClosingFailure) | |
|---|---|
| `NonManifold` | an edge left open past a fin longer than the gap, a corner two holes meet at, or a shell that cannot be wound alike, as a Möbius strip |
| `GapTooWide` | corners further apart than the gap across a gap only a weld closes; the point is where the gap is widest |
| `HoleTooLarge` | a hole enclosing more than `MaxHoleArea`, or one out of flat of more than 256 corners; or none may be filled, `MaxHoleArea` nought or `FillStrategy.None` |
| `HoleOffFlat` | a hole further out of flat than `MaxOffFlat` |
| `HoleAmbiguous` | under `WhenUnambiguous`, a hole that can be filled more than one way, the ways closing different volumes; or, whatever the strategy, a hole with another inside it where either is out of flat |
| `StillOpen` | the body still not valid after every step, as where every fill of a hole would lie back to back with a face of the body or cross one, or cross a fill taken before it, or a gap a weld would close only through a face standing in it, as a blade in a crack; or not worked out, which the log says |

Two faces of a box left out side by side show what ambiguous means. A box 10 by 1 by 1 without its top and its front
has one loop round the two, of six corners and 0.47 out of flat. Of the fourteen ways across it, the least area, 15.1,
cuts the box along its diagonal, but each end of that cut is a triangle lying on the end face it stands in, and is no
way. Of the six ways lying on no face, four are the two faces again and close the box whole, 10, and two cut across its
corner and close 8.3: `WhenUnambiguous` refuses the hole, and `MinArea` takes the two faces again, 20 of area.

```csharp
GeoSolid3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 1, 1)).ToObb().ToSolid();
GeoSolid3 open = new GeoSolid3(box.Faces.Where(face => face.Normal.Z < 0.5 && face.Normal.Y > -0.5)); // top, front out

var oneWay = new SolidClosingOptions(Tolerance.Default, 0.005, double.PositiveInfinity, maxOffFlat: 1.0);
open.TryClose(out _, oneWay, out SolidClosing3 refused);          // refused.Failure: HoleAmbiguous

var leastArea = new SolidClosingOptions(Tolerance.Default, 0.005, double.PositiveInfinity, 1.0, FillStrategy.MinArea);
open.TryClose(out GeoSolid3 whole, leastArea, out SolidClosing3 filled);
// filled.Repairs: one Fill of 20; whole.GetVolume() is 10
```

**Reading the report.** `Repairs` lists each change in the order it was made, a [`SolidRepair3`](xref:GeometryHelper.SolidRepair3) of a [`SolidRepairKind`](xref:GeometryHelper.Enums.SolidRepairKind)
whose `Size` is the kind's: a `Drop` of the area dropped, a `Flip` of the area turned over, a `Weld` of the furthest a
corner of the group moved, a `SplitEdge` of how far the corner stood off the edge, and a `Fill`, one per hole, of the
area added across it, its `Location` the middle of the faces added. `AddedArea` is the area of the faces added.
`VolumeChange` is what the body closed holds less what the faces given held, each measured from the middle of its own
box: faces that do not close hold a volume only from where they are measured, so a box with a face left out holds five
sixths of itself from its middle, and the face filling it adds the sixth, 1 000 on the box of 6 000. Welds alone move it
by no more than the gap times the area they touched. A body not closed reports no change: no repairs, nought added and
nought moved, with the reason and the point. The openings of the body are carried to the result as they are, the same
instances.

**On real parts.** The 1 989 parts of a Tekla model, each valid as read, were damaged five ways and closed within a gap
of 0.005, holes of any size filled and up to 0.01 out of flat: 9 736 bodies of the 9 945 closed, each valid and none
further from the part's volume as read than 13 parts in a million, in 0.8 milliseconds as a median. With its largest
face left out a part closed 98.6 % of the time, with a face at random 99.9 %, with every face on copies of its corners
moved up to 0.002 in its plane 98.2 %, and with a face turned over every time. The rest were refused with the reason,
most of them `NonManifold`, as where the rim of a face left out runs twice through a corner. Of the 35 cuts
`TrySubtractAll` skips on the 79 864 parts of a Tekla model, open or not valid within a thousandth, 23 closed, none
further from the same cut within a hundredth than 2.1 parts in a million; of the others, one holds a hole larger than
the 100 allowed, and 11 are cut wrong, by fins and by slivers wound the wrong way, which no closing undoes.

## Making bodies

A body can be made by moving a flat profile: straight out of its plane, along a path, or round an axis.

```csharp
var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

GeoSolid3 plate  = GeoSolid3.Extrude(outline, placement, 20);          // a GeoPolygon2, GeoFace2 or GeoPolygonArc2
GeoSolid3 slab   = GeoSolid3.Extrude(face, new GeoVector3(0, 0, 200)); // a GeoPolygon3 or GeoFace3, any way out of its plane
GeoSolid3 bolt   = GeoSolid3.Cylinder(head, tip, 8, 32);               // corners on the circle
GeoSolid3 bar    = GeoSolid3.Pipe(centreLine, 8, 0.1);                 // a GeoPolylineArc3, bends and all
GeoSolid3 member = GeoSolid3.Sweep(section, path);                     // the section's Y up where the path allows
GeoSolid3 ring   = GeoSolid3.Revolve(profile, placement, 2 * Math.PI, 0.1); // X from the axis, Y along it
```

Every body comes back closed and wound outwards whichever way round the profile was drawn. A hole in the
profile runs through the extrusion as a shaft. Carried along a path, the section stands square to it at either
end and lies on the plane halving each bend, so the pieces meet mitred; it turns with the path about the axis
of each bend and nothing else, so it does not twist. With the section centred on the path, the volume is the
section times the path's length exactly — the wedge a mitre adds outside a bend is the one it takes inside.
Curves come back as facets: a bend, a circle or a revolution is cut into chords no further than the chord
tolerance from the curve, and nought picks one from the radius.

A path turning back on itself at one vertex — sharper than about 170 degrees — is refused, and so is a profile
revolved across its own axis. A section larger than a tight bend folds over itself at the inside of the bend;
that is not checked.

## Weighing a body, and cutting it

```csharp
MassProperties3 steel = plate.GetMassProperties(7.85E-6);   // kg per cubic millimetre

steel.Mass;                 // the density times the volume, openings taken out
steel.Centroid;             // the centre of mass
steel.Ixx; steel.Ixy;       // moments and products about the centroid, parallel to the world's axes
steel.PrincipalMoments;     // smallest first, with PrincipalAxes turning with them
steel.GetMomentAbout(hinge, GeoVector3.XAxis);   // about any other axis

GeoFace3[] cut = plate.Section(new GeoPlane3(new GeoPoint3(0, 0, 10), GeoVector3.ZAxis));
```

The integrals are exact for the faces — the divergence theorem over the surface, after Eberly — and are taken
from the middle of the body's box, so a part far from the origin loses nothing to the size of its coordinates.
Like every other question, both read the material: a bolt hole comes out of the weight, moves the centroid,
and is a hole in the section. A section is one face per region the plane cuts, facing along the plane's normal;
a plane that misses the body, or only lies along one of its faces, gives none.

**Reading the faces more than one way.** [`Measure3`](xref:GeometryHelper.Core.Measure3) measures a body by the method asked, and every method side by side:

```csharp
double net    = Measure3.Volume(plate, VolumeMethod.Surface);        // the material, openings cut in
double kg     = Measure3.Mass(plate, 7.85E-6, VolumeMethod.Surface); // the density times the material's volume
double skin   = Measure3.SurfaceArea(plate, AreaMethod.Faces);       // the walls of the openings included
GeoPoint3 mid = Measure3.Centroid(plate, VolumeMethod.FlatFaces);

MeasureComparison3 every = Measure3.Compare(plate);
every.GetVolume(VolumeMethod.Fan);   // each method's volume, centroid and surface area
every.VolumeSpread;                  // how far apart the methods come
every.ReferenceSpread;               // how far the faces fall short of closing
```

| [`VolumeMethod`](xref:GeometryHelper.Enums.VolumeMethod) | Each face read as | |
|---|---|---|
| `Fan` | the fan of its boundary from its first corner, less its holes' | the quickest; what `GeoSolid3.GetVolume` sums |
| `Surface` | the triangles lying in it, on its own corners, holes left open | a closed surface of triangles; what `GetMassProperties` sums |
| `FlatFaces` | flat, on the plane square to its area through the middle of its corners | rests on no triangulation |

Over flat faces the three give the same volume, centroid and moments but for the rounding, as the divergence theorem
is exact for them. They part only over a face a hair out of flat, as the planar tolerance lets one be, and such a face
is no one surface: a face of four corners with one of them lifted holds a third of the lift times its area more split
along the diagonal through that corner, a sixth more split along the other, and a quarter more read flat. So the
spread is the answer to which is right — it says how far the faces leave the volume open — and where it matters,
`Surface` is the reading that stays consistent: cut a body in two, and the halves read so hold what it held, where read
by their fans, each face flat through its first corner, they held a ten-thousandth more. A fan sums to that whatever the
face's shape, so `Fan` moves by a third of a face's area times how far its first corner stands off its middle plane,
and it is the way Tekla Structures reports a part's volume. `ReferenceSpread` is the other doubt: faces that meet within
the tolerance but not on one another, as copies of one edge a few thousandths apart, leave gaps that the volume holds
or not depending on where it is measured from. Both spreads are the rounding for a body whose flat faces close.

`AreaMethod.Faces` gives each face's area as its outline does, read flat, and `AreaMethod.Surface` the area of the
triangles lying in it, a hair more where the face is out of flat. The volume, mass, centroid and surface area are the
material's, openings cut in, and a body its openings take whole holds nothing. The material is the one the body's
own `GetVolume` measures, cut once and kept, so measuring it every way costs one cut.

A body cut into many pieces at once, the blocks, bays or lifts of a grid, is what `ToCells` gives, described with
[meshing in space](../mesh3.md).

## Hulls and fitted boxes

`ConvexHull3.Of(points)` is the smallest convex body holding some points, closed and with coplanar triangles
merged, so the hull of a box's corners is the box. `GeoObb3.Fit(points)` stands a box on each face of that hull
in turn, with the smallest rectangle round the points on that face, and keeps the smallest: exactly the least
box for a block, a prism, or anything with a flat face to stand on, and close to it otherwise. Of the boxes
standing on faces of the hull that are as small to rounding it keeps the thinnest, its least side first, then
the middle one, and so of two rectangles on one face: a prism 40 long on a right triangle with legs 18 and 24 is
fitted the box 14.4 by 30 by 40 along its long side, not the one 18 by 24 by 40 along its legs, and the
tetrahedron on (0, 0, 0), (24, 0, 0), (0, 18, 0) and (0, 0, 40) the box 13.548779 by 27.340984 by 46.647615 however
it is turned, where turned it was given the one 13.548779 by 30 by 42.513057 as large, found first on its long face.
Of boxes that tie to rounding the one kept can have its axes named otherwise than before for the same hull: the prism is
14.4 by 30 by 40 still, but its `SizeX`, `SizeY` and `SizeZ` read 30, 14.4 and 40 where they read 30, 40 and 14.4.
Points all in one plane give a box of no depth.

The hull holds every point within the tolerance of its triangles, and closes, on plates down to a few thousandths
thick: the faces of such a plate meet at its rim so nearly flat that a point 10 off the rim stands within a
thousandth of every plane, and a hull that leaves a point out so, does not close, or folds back over itself, is
built again with an exact test of which side of a face a point stands on. A hull that closes and holds every point
is kept as it is built. Points all within the tolerance of a plane span no volume; a plate no thicker than twice the
tolerance lies within it of its middle plane, and its hull is found or not by the plane it starts from. Merging the
triangles of one plane within the tolerance can stand a face as far again from a point: up to 1.08E-3 off on 5 of
1 000 plates 100 by 100 and from 0.002 to 1 thick. Every point is held within the tolerance of the merged hull: where
one is not, the triangles are merged again within a tenth of the planar tolerance, then a hundredth, and left as they
are where that does not hold it either; a hull that holds every point merged is kept as it was, to the bit. A point is read only against the faces that can stand below it, found by the way
they face, so the work grows little faster than the points even where every one is on the hull: 10 000 points in a
cube take 50 milliseconds, and 12 000 on a sphere 0.18 seconds, where they took 2.4. Fitting the box round 3 000 on
a sphere takes 0.27 seconds and round the 12 000 1.9, where they took 0.53 and 7.7: each corner of the hull is
taken once, a face is passed over where no box on it can be as small as one found already, so 8 of the hull's
6 000 faces are fitted, and what a face can stand is read from the corners and rim edges near it, laid out in
trees.

```csharp
GeoObb3 box = GeoObb3.Fit(part.Faces.SelectMany(face => face.Boundary.Vertices));
```

## Merging

[`Merge3`](xref:GeometryHelper.Core.Merge3) is the other direction. `ConsecutiveLines` and `ConsecutivePolylines` take the pieces in the order
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
An edge within the tolerance of two others is taken for the edge it shares with the nearer, so a face
thinner than the tolerance keeps its area: its two long sides are within the tolerance of each other, but the
face beside it shares one of them exactly. A side 2 000 by 300 cut by a line from one bottom corner to 0.0011
over the other, and across by four more, merges back into the one face of 600 000 square millimetres it was,
where it came out 0.53 short.
