# Combining bodies

Union, subtraction and common part of bodies: what comes back, how closed it is, how near is touching, and parts
drawn against each other.

[`Boolean3`](xref:GeometryHelper.Core.Boolean3) joins two solids, keeps the part they share, or takes one out of the other:

```csharp
first.TryUnion(second, out GeoSolid3 joined);
first.TryIntersect(second, out GeoSolid3 shared);
first.TrySubtract(tool, out GeoSolid3 left);
```

Each reports `false` when the answer is nothing at all — two bodies that never touch share nothing, and a
body swallowed whole leaves nothing behind. That is an outcome rather than a failure, which is why it
comes back as `false` rather than as an exception or an empty body.

`false` is also what comes back where the answer could not be worked out, which the log says why. The
overloads taking an `out BooleanOutcome` tell the two apart, as a net volume worked out by taking cutters
off a part one after another needs: nothing left takes the part to nought, where no answer is a step to
work out some other way.

```csharp
if (!part.TrySubtract(cutter, out GeoSolid3 left, tolerance, out BooleanOutcome outcome)
    && outcome == BooleanOutcome.NotWorkedOut)
{
    // No answer: not nothing left. Work this step out some other way.
}
```

The method is the cutting above carried up a dimension. For a union or a difference both bodies are
divided by one shared set of planes — the face planes of each of them together — which leaves cells that
are each wholly inside or wholly outside the other, since the surface of a body never leaves the planes of
its own faces. The cells
the operation wants are then glued: a face shared by two kept cells appears twice, once each way round,
and dropping both leaves exactly the outer skin.

Two details are worth knowing. Using one shared plane set for both bodies rather than each against the
other is what makes the gluing work where they meet — cut that way both sides of the interface are the
same plane carved by the same knives, so they come out as the same polygon and cancel. Not always vertex
for vertex, though: a knife that reaches the cell on one side and not the one on the other leaves one copy
in two pieces, so faces still lying back to back after that are cancelled by the area they share rather
than left standing inside the body as a sheet of no thickness. And dividing A by the planes of B already
lays a face along every part of the surface of B that runs through A, which is why a difference is just the
cells of A that fall outside B: the walls of the cavity are already there.

An intersection needs no more than that, so it cuts one body only — by the planes of the other that come
near it, and of its own openings — and the one it cuts is the one fewer planes cut. A bent bar cut by the
planes of its own bend as well came apart into thousands of cells, since every plane of a bend runs on
through the rest of the bar; cut by a beam's handful, it is a few.

Once a body has come apart into more than 4 096 cells, each plane cuts it only where the plane's faces are: a cell is
cut by a plane only where the cell's box comes within the tolerance of the box of the faces lying on that plane. A face
of the other body has to come that near a cell to cross it, so the cells are still each wholly inside or wholly
outside the other. Up to 4 096 cells every plane cuts every cell it crosses, as it always did, and what the booleans
give is as it was. Two spiral beams faceted at different stations show what that saves: a ramp 7 600 wide and 300
thick in 32 facets over half a turn, and a girder 600 by 500 in 38 lying 100 into its outer edge. Every plane of the
ramp crosses the girder at many places along the turn, and cut by each wherever it ran, the girder came apart into
36 509 cells for their common part; it makes 5 015 now, and the common part holds the same 64 239 944.184 cubic
millimetres. Faceted 56 and 66 times, as a Tekla model facets such a pair, the girder made 263 911 cells and the common
part took 42 seconds on .NET 10 and 91 on .NET Framework 4.8; it makes 18 345 and takes 2.5 and 3.3. Where either body
or any of their openings does not close, there is no inside for a cell to lie in, and every plane cuts every cell,
however many. A difference or a union whose one body, cut so, came out open and did not add up sealed is not taken
welded closed in the end all the same; what is taken instead is checked, and the boolean is not worked out where that
does not add up either.

**An opening that shares no space with the other body is left out of a common part, and out of the body a difference
takes away.** Its box stays further from the other body's box than the tolerance, or the opening and the other body both
close, their faces stand further apart than twice the tolerance, and no corner of either lies inside the other. Left
out, it changes nothing the boolean gives: the part two bodies share is what they share without it, and a body less one
carrying such an opening is the body less the other whole. A box is not enough on a curved body: a spiral ramp 7 600
wide in 56 facets over half a turn, holding a spiral tube 110 by 100 as an opening 80 under the top of a spiral girder in
66 lying 100 into its outer edge, has the tube's box and the girder's both reaching round the half turn. The girder less
the ramp, the tube cut into it, came apart into 640 222 cells in 202 seconds on .NET 10 and was not worked out; with the
tube left out it is cut into 18 345 in 1.6 seconds and holds what slicing gives, 10 044 789 407.964 cubic millimetres.
The openings of a body whose material the result keeps, either body of a union or the one a difference takes material
from, are cut in as before wherever they lie, so the faces of a union or a difference are still where its material ends.

## What comes back closes within the default tolerance

Two faces of a result meeting on an edge each have it
where their own planes put it. Where a plane of one body passes within the tolerance of a face of the other, the
two copies stand a few hundredths apart: closed within the tolerance the boolean was given, open read within a
finer one. A result open within the default, or within its own tolerance where that is the finer, has its corners
within its tolerance of each other made one, each group at the corner of it that moves the volume least, and each
corner within the tolerance of another face's edge put on that edge. A corner is weighed by the triangle it makes with
the corners its faces turn at either side, past any their edges run straight on through: a column 300 by 300 whose
faces came in 200 to 1 000 strips, joined within a hundredth to a beam framing 0.002 into its flange, had a corner of
the flange taken off the flange's plane to the beam's, weighed by the sliver it made with the corners a strip apart,
and held the two less 25.72 to 374.81 cubic millimetres, closed and valid, where their overlap is 17.04; it holds the
two less 18.96 now, as with the column in one piece. A corner a face lies round, at a hole or a notch in it or part way
along a straight edge, makes no such triangle of the face, and weighs next to nothing. Where the weld tilts faces by
more than a hundredth of the tolerance and moves the volume by more than a tenth of the tolerance times their area, or
tilts one and does not close, it is made again with such a corner weighed by the whole face, each group taken to the
corner that takes the most of it, and again to the corner that moves the volume least; of those that close wound alike
and tilt less of the faces than the first, the one moving the volume less is taken where it moves it less than the
tilt, or, where the first does not close and it lays no face over another, in its place. A column 300 by 300 by 1 000 joined within a hundredth to a beam 160.1 by
160.8 whose end stands exactly 0.01 clear of its side had two corners of the hole in the column's face, 274 255 square
millimetres, taken 0.01 off it to the beam's, and held the two and 918.9 cubic millimetres where filling the gap holds
257.4; it holds the two and 214.5 now, the beam's corners taken onto the column's face. Corners of a group that move
the volume by no more than the rounding of the coordinates times the area they are weighed by, which is to say by
nothing, are told apart by the faces round them: the one that tilts those faces least is taken, and of those still
alike the one moving the volume least, then the first met. A corner moving the volume more is taken as before. A cube
of 100 and a block 40 by 30 running exactly 0.001 into its top, turned and joined within a thousandth, had two corners
0.0003 apart part way along the edges of two faces of 10 000 square millimetres, weighing 3.3E-18 and 2.1E-17 cubic
millimetres, and the weld took one to the other, 0.0003 off its own face, where the other stands on the first one's
face: the union held 1.39 less than the two less their overlap, where the tolerance times the area where they meet is
1.2. It holds 0.39 less now. What comes of it is kept where
it closes within the default and within its own tolerance, and holds its volume within the tolerance times its area.
Of 10 314 cuts within 0.05 of the parts of a Tekla model by the parts they meet, 14 came out open within 0.01 that way;
they close now. A result open within its own tolerance is welded too, where that closes it, and one closed within the
default is left as it is. `Validate()` shows where a body is open, and why.

A weld that would take a corner off the plane of a face it is a corner of keeps the face on its plane instead, where
that closes: the face keeps its corner, a face beside it moving the corner along its own plane keeps it as well, and
the step a few thousandths across left between them is closed by a face of its own. A column 300 by 300 joined, within
a hundredth, to a beam 383 wide and 366 high whose end stands 0.008 clear of the column's face had the parts of the
end beyond the column tilted into wedges, 999.8 cubic millimetres over the two where filling the gap holds 878; it
holds the two and the gap filled now. A box less a cube in its corner had its side, 4 000 square millimetres, taken
off flat by a corner of the top 0.0099 from its edge, and fanned it held 6.5 too little; the sliver between them is a
face of its own now. Where the faces kept on their planes leave the two rims of a gap open, each a loop, the one a few
thousandths over the other corner for corner, with nothing of the body between them, the gap is closed by a strip along
each edge of a rim: a column 300 by 300 and a beam 387 wide and 118 high whose end stands 0.008 clear of its face,
joined within a hundredth, held 41.06 cubic millimetres more than the two and the gap filled, the parts of the end
beyond the column tilted into wedges, and holds the two and the gap filled now. Rims bounding a slab both bodies hold,
a beam's end exactly the tolerance within a column, are no gap. The weld is made as before where the steps do not close as strips a few tolerances wide, where a
face it builds runs back over itself or two of its faces lie over each other in one plane, where keeping the faces on
their planes would move the volume further than tilting them, and where the tilt is not worth it: no face tilted by more
than a hundredth of the tolerance, or the tilt moving the volume by no more than a tenth of the tolerance times the
area of the faces it tilts.

## How closed is read

`IsClosed()` and `Validate()` read a body alike, the edges matched along a line within the
point tolerance, stretch by stretch, and two copies of an edge whose ends lie within the tolerance of each other
matched however little of them lies along the other: a cube of 100 whose top stands 0.0005 over the tops of its sides
is closed within a thousandth wherever it lies, one whose top stands 0.0015 over them is open, and a slab 100 by 100
and 0.0012 thick whose side at one end is slid up 0.0002 is closed, where `Validate` read the side's four upright edges
open, each sharing only 0.001 of its line with its twin. An edge no longer than the tolerance is left out unless it is
such a copy of a longer one and its own loop has an edge longer than the tolerance. A gap of exactly the tolerance is
so to a few bits either way, 100.001 less 100 being 0.001 000 000 000 004 8, and reads as the rounding has it. Below
the rounding of the coordinates, a quadrillionth of the largest coordinate of the whole body, not of each part of it,
the edges are matched within that rounding: at a tolerance of nought `Validate` read a cube turned in space, and the
union of two such boxes made within a thousandth, open along nearly every edge, and reads them closed. `IsClosed`
matched the ends of the edges first, each to the first corner seen within the tolerance of it, and called two corners
0.0015 apart one where a corner lay between them: that cube, with a speck of a face at each corner of its top half way
up, was closed to it and open to `Validate`, and open to both with the specks given last.

The reading has its limits, and they are worth knowing. Edges matched along one line are matched one after another,
so a gap wider than the tolerance reads closed where other edges lie in it within the tolerance of either side: the
cube whose top stands 0.0015 over its sides, with a box of 1 standing against each side half way up the gap, reads
closed and valid; and a sliver face with an edge longer than the tolerance, lying in a side of a hole wider than the
tolerance, can read the hole closed the same way, its short copy of the side kept. A speck, a body with no edge longer
than the tolerance, is not closed to `IsClosed`, and is closed to `Validate`, which finds no stretch of it open and
calls it not valid, enclosing nothing. And the booleans, the weld, the cutting and `TryGetVolume` read the bodies they
work on as `IsClosed` did, the ends of the edges matched first, within the tolerance given and no wider: what a boolean
makes is taken as closed where the edges matched along lines within that tolerance close it, and `TryGetVolume` trusts
a material by its corners. The booleans asked below four tenths of the rounding of the coordinates, and
`TryCutOpenings` asked at a point tolerance below it, are the exception: they cut and close what they give within that
rounding, and make its corners a rounding apart one where that closes it within the tolerance given. `TryGetVolume` can trust one `IsClosed` reads open, as the cube with the specks given first, 1 000 005, and refuse one `IsClosed` reads closed, as
that union at nought; its own answer says whether its volume can be trusted. `Mesh3.ToCells` cuts a body into cells
only where it is closed both ways: it refuses the cube with the specks, which it cut into 8 cells before, and still
refuses that union at nought, which read closed it would cut into no cell at all. The IFC readers follow `IsClosed`.

## Closed as `Validate` reads it, or not made

A boolean of two closed bodies takes what it makes as closed only where
its edges, matched along lines within the tolerance the boolean was given, close it, as `Validate` reads all but the
bodies above: one open so is welded as above, kept where it closes and the weld moves the volume no further than moving
each corner it moves by the point tolerance can, and otherwise worked out by cutting both bodies by every plane of
both, or for a common part the other body; where it stays open the boolean says it could not be worked out, `false`,
logged, rather than handing it back, and `TrySubtractAll` asked to close the cuts it skips is handed what it gave. A
corner moved by a distance sweeps, on each face it is a corner of, no more than the distance times a third of the
triangle it makes with its two neighbours. A result that came out open on the way is taken only where its volume adds
up: a union holds the two less their common part, a difference the first less the common part, a common part the two
less their union, the other worked out by the same boolean at the same tolerance, within the tolerance times the area
of the common part's skin and what the weld moved. The union of a box 100 by 60 by 40 and a cube 1.25 across standing
out through three of its faces a hair more than the tolerance came out open and, closed, held 240 001.2065, the cube
counted whole, where the two less their common part hold 240 000.0032, and is not worked out. A union of two bodies
lying face to face within the tolerance of each other and apart, welded closed, is taken where it holds the two and
the gap filled over where their faces lie against each other. Two cubes of 100 side by side 0.01 apart, turned, closed
across the gap at a tolerance of a hundredth into a body of 2 000 000, the two, 1 999 900, and the gap, 0.01 by
10 000 square millimetres. What two bodies exactly the tolerance apart join into still depends on where they lie: turned
another way, the two cubes hold 1 999 933.3, part of the gap. Two bodies that share nothing at the tolerance, a gap or
an overlap no deeper than it between them, are taken joined into anything from the two less twice the tolerance times
the area where they lie face to face, the overlap and the faces where they meet the tolerance further in, to the two and
the gap, and no more. A column 300 by 300 by 1 000 and a beam 239.2 wide and 344.9 high running exactly 0.001 into its
side, turned, came out open and, closed, held the two less 13.75, the slab where they overlap being 82.5, and was not
worked out, held to the two exactly; it is taken now, as are two cubes of 100 turned exactly a thousandth apart that hold
the two and half the gap, and a block whose foot runs exactly 0.001 into a turned cube's top at its corner that holds the
two less 0.67 where the slab is 0.4. What a weld closes from a body that adds up is taken, and
can hold a volume between the readings of a feature exactly the tolerance thick: the union of a cube of 100 and a block
120 by 120 by 50 running 0.001 into its top holds 1 720 012.73, the two with nothing in common at the tolerance being
1 720 014.4 and the cube and the block beyond it 1 720 004.4. A result that closes as it comes is taken unchecked. Of
8 000 boxes 100 by 60 by 40 less a cube 0.1 to 1.9 across a hair either side of the tolerance off one to three of their
faces, turned or not, far from the origin or not, 17 came out closed by their corners, as `IsClosed` read them then,
and open by two stretches to `Validate`, and 2 open to both: 13 close now, each holding a volume between the cube cut
and the cube taken with the outside, and 6 are reported as not worked out. No result closed to both moved, by a bit,
across 40 000 differences, unions, common parts and cuttings of openings of those boxes.

Where both bodies would be cut by every plane of both, and that costs far more than cutting one of them did, which gave
a result that came out open, that result is sealed first and taken where it closes and adds up. A beam 2 000 by 200 by
300 less a bar 16 across bent through a right angle in forty steps, its end 0.001 short of the beam's floor, cut by the
664 planes of both, ran for minutes into tens of thousands of cells; it takes under a second now.

## Thinner than the tolerance is nothing; thicker is cut

A void, a chip or a slab no thicker than the point
tolerance is within the tolerance of nothing, and a boolean takes no notice of it: a cube of 100 less a box 40 by 30
and 0.0005 thick inside it is the cube. One thicker than the tolerance and a trillionth of the size of the
coordinates of the two bodies is cut however thin: less a box 0.0015 thick, the cube holds 999 998.2, the two have that
box in common, and joined to a block running 0.0015 into its top they hold the chip once. One exactly the tolerance
thick on a turned body is that thick to a few bits either way, and is read as no thicker. The trillionth is of the two
bodies' coordinates, whichever is cut, so the common part and both differences read a chip one way, and each
difference and the common part add up to the body it was taken from: a chip 0.0010000001 deep at the corner of a turned
cube of 100 and one as deep along its edge are both read as no thicker than a tolerance of 0.001, where the corner was
cut. A union of the two holds anything from the two less such a chip to the two, the seam where they meet read part
way. A union that comes out wound against itself, two bodies wound alike, is worked out again with each cell no thicker
than the tolerance lying within the other body against its face left out, and taken so where that closes wound alike and
holds a volume a union can: no less than the larger, no more than the two, and the two less their common part within
the tolerance times the area of that part's skin. A turned cube of 100 joined to a slab 40 by 30 and 0.001 thick inside
its top came out wound against itself round the slab's sides, 1 000 000.4, and is the cube now. One that comes out
wound against itself that way too is worked out once more with a corner read as on a plane within what rounding adds to
the tolerance, a trillionth of the size of the coordinates, as the cut of openings reads it, and taken where that closes
wound alike and holds a volume a union can: where the two share nothing, no less than the two with the faces of one
lying face to face on the other's moved onto them, whole, by the tolerance. A column 300 by 300 by 1 000 and a beam
386.7 wide and 364.8 high running exactly 0.001 into its side, turned and moved, came out wound against itself every
way, the slab of the beam's end kept beside the column whole, and was not worked out; it holds the two less 125.26 now,
the slab where they overlap being 109.45 and the beam's whole end 141.07. The same cube joined to a block 40 by 40 by 50
running exactly 0.001 into its top at the corner, which came out so holding 1 080 003.21, more than the two, holds
1 080 000.8, the block's foot being 1.6. One wound against itself that holds no such volume any way is not worked out:
a beam 377.4 wide and 107.5 high running exactly 0.01 into the column, joined within a hundredth, came out so, and
worked out once more held 573.26 less than the two, less than the column and the beam beyond its face, 405.6 less. The
weld that closed it tilted a part of the column's face, 133 880 square millimetres, by the tolerance; welded with each
group taken to the corner that moves the volume least (see above), it holds the two less 364.0, the slab where they
overlap being 322.4.
A difference or a common part of two closed bodies that makes a closed body enclosing no more than a slab the tolerance thick, as `Validate` reads it (`SolidIssueKind.NoVolume`),
and nothing else wrong with it than such a slab has, leaves nothing, and says so, `false` with `BooleanOutcome.Empty`: the
common part of that cube and such a slab inside it was the slab, 1.2 cubic millimetres enclosing nothing, and a plate 10
by 10 and 0.0018 thick less a box over all but 0.0018 of its width left a rod 0.0018 square. Two bodies apart, a body
that does not close, and what is left of a body itself enclosing no more than such a slab are not asked: the slab at the
cube's top less the cube is the slab as it came. An opening whose face stands the
tolerance off the body's, to the last bit, is read as touching it, as one a hair nearer is: a box 100 by 60 by 40
with an opening 0.1 across 0.001 off three of its faces comes out closed, the opening taken with the outside,
239 999.998 97, where it came out open 7.66 short.

**Walls of a body's openings standing apart within the tolerance of one plane** are each kept in their own by a union,
a common part or a difference, as `TryCutOpenings` keeps them: the openings of both bodies are cut in, and the operation
worked out, within the finer tolerance the cut of openings takes for them, and what that gives is taken where it closes
within the tolerance asked. A boolean cut the openings of its bodies in at once within the tolerance, and the plane of
one opening, cutting all it reached, took in the wall of another within the tolerance of it, however far away. An L 100
by 80 by 50 with two blind openings 27 apart whose walls stood 0.0009 apart across it, less a box 65 by 20 by 40 across
both walls, within a thousandth or a hundredth, held 130 189.39 one way round and 130 189.26 the other, where it holds
130 189.32, and the box less the L 28 457.23 and 28 457.88, where it holds 28 457.68; both hold it now, as do their union
and their common part. Of 4 800 such booleans of prisms with two or three such openings and boxes, turned about in space
or not, at the origin and a thousand to a thousand million out, at 1E-3 and 1E-2, 1 363 that were wrong are right now,
every one a million out and nearer among them, and none that was right moved. Where a wall of the other body lies within
the tolerance of the plane of a wall of the one, the boolean reads the two as before, and so it does ten million and
more out, where the finer tolerance would come within eight trillionths of the size of the coordinates: worked out so,
3 that were right came out wrong. A wall of an opening turned the other way from a wall of the body it stands within
the tolerance of the plane of, as an opening's wall 0.0004 off an L's inner wall and 5 along it, is found standing apart
too, by `TryCutOpenings`, `GetVolume`, `Section` and `Mesh3.ToCells` as well: that L measured 0.09 too little.

## See also

- [Combining bodies: far out, openings and touching](booleans-more.md): bodies far from the origin, walls a
  fraction of the tolerance apart, openings, and how near is touching.
- [Closing, making, weighing and merging](making.md).
- [Volume take-off](../takeoff.md): net volumes, each overlap once.
- [Clashes and large meshes](clash.md).
