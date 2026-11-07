# Combining bodies


`Boolean3` joins two solids, keeps the part they share, or takes one out of the other:

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

**What comes back closes within the default tolerance.** Two faces of a result meeting on an edge each have it
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

**How closed is read.** `IsClosed()` and `Validate()` read a body alike, the edges matched along a line within the
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

**Closed as `Validate` reads it, or not made.** A boolean of two closed bodies takes what it makes as closed only where
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

**Thinner than the tolerance is nothing; thicker is cut.** A void, a chip or a slab no thicker than the point
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

**Bodies far out are worked out near the origin.** A union, a common part or a difference, and each cut of
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

**The queries of a body far out are worked out near the origin too.** `TryCutOpenings`, the material its measures read
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

**So is where a point, a ray, a segment or a line stands against it.** `Locate` and `Contains`, `DistanceTo`,
`SignedDistanceTo`, `GetClosestPointOnBoundary` and `GetShortestLineTo` of a point, and `CollidesWith`, `DistanceTo`,
`GetIntersections` and `GetShortestLineTo` of a ray, a segment and a line move such a body into the same frame, once,
keeping the moved body with it, move the probe in coordinate by coordinate and what has a place back out; a probe near
the body moves exactly and comes back as it was given. A `GeoPreparedSolid3` of such a body is prepared again in the
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

**Walls a fraction of the tolerance apart leave no sheet inside the material.** The cut of openings parts a body by the
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

**Openings cut in at once are cut again where they come out open.** Asked below a thousandth, within a million of the
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

**A body keeps the openings its boolean cut in at once.** Asked at a millionth and below, a boolean cuts the openings of
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

**Exactly the tolerance into or apart counts as touching.** Two bodies whose faces lie the tolerance into or clear of
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

**Parts drawn against each other.** The tolerance says how exact a boolean is; how near two faces of two parts
have to be to touch is a question of the model. Tekla Structures draws parts against each other a few thousandths
of a millimetre into or off each other, and cut within a thousandth, a part whose cutter's face stood 0.0049 inside
its own keeps a skin of itself that thick: 110 square metres of surface on a part of 119. `SolidBooleanOptions`
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

A cut skipped can be closed instead. Given `SolidClosingOptions` as well, `TrySubtractAll` closes a cut it would skip
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
metre apart as overlapping and say nothing about it. The whole of the shape has to lie in the plane, every
corner and the middle of every arc: a plate turned half a degree about one of its corners is refused too,
though its plane is parallel within the angle tolerance and passes through that corner. `SharesPlaneWith`
asks beforehand, on `GeoPolygon3`, `GeoFace3` and `GeoPolygonArc3` alike.
