# Changelog

The release notes of the GeometryHelper package in full, newest first. The package carries the notes of its own
release only, and a link here for the rest. GeometryHelper.IfcConvert,
GeometryHelper.TeklaConvert and GeometryHelper.CadConvert carry their own notes in their packages.

## Unreleased

**FIXED.** `Offset2.Offset` of a `GeoPolygonArc2` that crosses itself, grown or shrunk with miters or chamfers, reads a
crossing the tolerance does not tell from what lies beside it within a finer step; so does `GeoPolygonArc3.Offset`,
which miters. A corner of the loop within the tolerance of a crossing turns the tip of the region there by its own
turn, and such a crossing was joined round the space between, as when rounded, its miter or chamfer left out, though
the corner bent the loop by a degree; and two crossings within the tolerance of each other are one place, which turns
the blunt end of the sliver between them into a point, whose miter ran on, or was squared off at its limit, past where
the loop's own corners take it. Where the tolerance is so coarse at a crossing, the loop is now cut again within a
hundredth of the tolerance, and then a ten-thousandth: a step it cannot be cut at is passed over, one is taken where it
is coarse at fewer places than the step before, until none is left, and the loop is offset within the last step taken.
A step coarse at as many places as the one before, as where three edges cross at one point, ends it, and where no step
is taken the loop is offset as before. A loop of the answer that is a point within the tolerance asked for, its corners
all within it of each other, is left out, so the answer is still within that tolerance. The bow tie from (-10, 2) to
(1, -0.2), (1, 0.2) and (-10, -2), its back an arc of bulge -0.25, with a corner on its first edge 0.005 past the
crossing where it turns by 1 degree, grown by 1 with miters at a tolerance of 0.01, keeps the miter of its long lobe
and encloses 54.419, where it was 52.501; the bow tie of 30 degrees with such a corner turning it by 2 degrees, grown
by 3 with chamfers, is 137.974 where it was 137.199. A loop of ten corners, two of whose edges cross 1.7 degrees apart
and are crossed by a third 0.0078 from there at points 0.00027 apart, shrunk by 0.3 with miters at 1E-3, is four loops
of 24.284 where 0.063 more was cut out beside the point, and shrunk by 1 one of 2.857 where it was 3.358; a loop of
four corners whose spike 2.4 degrees wide is crossed by an edge where it is 0.0057 wide, grown by 3 with miters at
0.01, is 386.351, as within 1E-4, where it was 387.385. A loop of ten corners in a box 0.95 by 0.86, shrunk by 0.1 with
miters at 0.01, is the one loop of 0.022622 it was: the triangle 0.0013 by 0.0014 its finer step leaves beside a
crossing is left out. Round joins, and the DXF polylines' `GetOffsetCurves`, read every crossing within the tolerance,
as before.

Read against the exact-wedge oracle of the entry for the tips of 13.0.0, over the same 21 504 offsets: grown with
miters at 1E-2, 7 were wrong and 3 are, grown with chamfers at 1E-2, 3 and 2, and shrunk with miters 2 at 1E-3 and 1 at
1E-2, and none is; 8 came nearer and none went further. The 5 left at 1E-2 are one loop, a corner of which pokes 0.0075
across an edge, which so crosses both its sides within the tolerance of it; and the loop that does not cross itself,
wrong grown by 3 with chamfers, is as it was. 82 offsets changed, all of loops that cross themselves, offset with
miters or chamfers; of the 74 that were right, 3 moved away from the oracle's area by 0.0002 at most and the rest
towards it, a loop of ten straight corners grown by 3 at 1E-2 by 0.255, to the oracle's 525.435. Every round offset,
every chain, every loop that does not cross itself and every offset of the straight path is the same to the bit, and so
is the `GetOffsetCurves` of 2 720 DXF polylines. Of 1 728 bow ties with a corner within the tolerance of their
crossing, before or past it, turning the loop by half a degree to 135 degrees, at 1E-2 and 1E-3, grown by 0.3, 1 and 3
with miters or chamfers, 1 162 were wrong and 235 are, 927 nearer and none further; those left turn the loop by 45
degrees or more, where the corner and two crossings make a lobe of their own finer than the tolerance, and were wrong
as they are. Of 28 800 offsets of 900 random loops at a twentieth of their size, with miters or chamfers at 1E-2 and
1E-3, 20 have more or fewer loops than they had: 18 as many as within 1E-6 where they had not, and 2 one fewer, a loop
of 0.00001 that is a point within 1E-2, which they kept and the reading within 1E-6 has. A loop cut again within a
finer step takes up to twice as long, and is cut at most twice more: the loop of four corners grown by 3 at 0.01 takes
0.65 milliseconds where it took 0.35 on .NET 10, and 0.28 where 0.16 on .NET Framework 4.8; the bow tie 0.055 where
0.027, and 0.082 where 0.049; and the loop of ten corners shrunk by 0.3 takes 1.22 where it took 1.11, and 2.04 where
1.90. A place where three edges cross at one point is coarse at every step, and ends the steps: 30 diameters of a
circle, crossing at its centre, grown by 0.3 with miters at 1E-3 take 163 milliseconds where they took 150 on .NET 10,
and 329 where 317 on .NET Framework 4.8, and shrunk by 0.05 at 1E-2 57 where 51, and 110 where 101. The 1 200 offsets
of 300 random loops, and the 600 of 150 loops of straight edges, at each join, tolerance and way, take as long as they
took within 3 % on both. 8 new tests.

## 13.1.0

**FIXED.** The booleans of two curved bodies faceted at different stations, and `VolumeTakeoff` of them, no longer
run for minutes. A body was cut into cells by each plane of the other wherever the plane ran, and every plane of a
curved body crosses another curved along the same turn at many places: a spiral ramp 7 600 wide and 300 thick in 32
facets over half a turn and a spiral girder 600 by 500 in 38, lying 100 into the ramp's outer edge, had the girder cut
into 36 509 cells for their common part. Once a body is in more than 4 096 cells, and where both bodies close, a plane
now cuts a cell only where the cell's box comes within the tolerance of the box of the faces lying on that plane, which
is as far as a face of the other body can reach, and the girder is cut into 5 015. Faceted 56 and 66 times, as a Tekla
model facets such a pair, the girder was cut into 263 911 cells and their common part took 42 seconds on .NET 10 and 91
on .NET Framework 4.8; it is cut into 18 345 and takes 2.5 and 3.3, and the take-off of the two, which took 41 and 90
seconds, takes 1.2 and 3.0. Both give the 65 400 402.028 cubic millimetres slicing gives, as they did. The take-off's
test of near-coincident pairs does not send such a pair to slicing: their faces cross at small angles rather than
standing beside each other.

Up to 4 096 cells every plane cuts every cell it crosses, as before, and the booleans of bodies cut into no more give
what they gave, to the bit: 17 452 booleans of cracked cubes and openings, 72 000 of U bodies and boxes, 24 000 volumes
of cracked cubes with openings, 32 400 booleans of a cube and boxes half a tolerance to two tolerances into, onto or
beside it, 600 plates with twelve blind holes cut in at once and 400 bands; and 12 800 joins and cuts of a column and a
beam fail or fall out of their bounds in the same cases by the same amounts. Booleans of plain bodies take the time
they took: whether both bodies close, and their openings, is asked only once a body passes 4 096 cells.

A difference or a union whose one body, cut only where the planes reach, came out open, and sealed did not add up
against the two and their common part, is not taken in the end all the same, welded closed, where the cells of both
bodies cut by every plane of both have a cell judged apart; what is taken instead is checked the same way, and the
boolean is not worked out where it does not add up. A spiral ramp 7 600 wide in 56 facets less a spiral girder in 66
lying 100 into its outer edge, the girder's top 0.002 over the ramp's floor, worked out within a hundredth so, came out
35 554.6 cubic millimetres short, an eighth of the 263 740.0 the two share; it is not worked out, and the take-off
slices it. Made within the fallback, a result is held to the fallback times the area of the common part: the same two
with the girder's top 0.0005 over the floor leave 7 170.1 less than slicing, within the 36 000 a hundredth allows.

**CHANGED.** `IsClosed` reads a body as `Validate` reads it, the edges matched along a line within the point tolerance,
so that the two agree on every body but a speck. `IsClosed` matched the ends of the edges first, each to the first
corner seen within the tolerance of it, and made one of two corners 0.0015 apart where a corner lay between them: a
cube of 100 whose top stood 0.0015 over the tops of its sides, with a speck of a face at each corner of its top half
way up, was closed to it within a thousandth and open to `Validate` along every edge of the top, and open to both with
the specks given last. Of 432 readings of cubes whose tops stand over their sides, with those specks given first or
last or without them, slide along them, bulge out past one or run out at a corner, by half, one, one and a half and
nearly two tolerances, at the origin, turned and far off, within a thousandth, a hundredth and 2^-10, the two disagreed
on 46 and agree on all; a gap of 0.0005 is closed to both wherever it lies and one of 0.0015 open. A gap of exactly
the tolerance is so to a few bits either way, 100.001 less 100 being 0.001 000 000 000 004 8, and reads as the rounding
has it.

Both read two more things as closed than `Validate` did. Two copies of an edge whose ends lie within the tolerance
of each other are matched however little of them lies along the other: a slab 100 by 100 and 0.0012 thick whose side
at one end is slid up 0.0002, each upright edge of the side sharing 0.001 of its line with its twin, read open along
four edges, as did slabs 0.0014 and 0.0016 thick slid by 0.0005 and 0.0009, and is closed and valid. Every edge no
longer than the tolerance is left out, as before, but for one that is a copy of an edge longer than it and belongs
to a loop that has an edge longer than the tolerance itself, which is kept with its copy: a slab 0.0015 thick whose
side at one end runs from 0.0003 to 0.0012 up, its upright edges 0.0009 long beside copies 0.0015 long, is closed, and
`Mesh3.ToCells` cuts it into its cells, as it did. A speck, a loop with no edge longer than the tolerance, keeps none:
a cube of 100 whose top has a square hole 0.0019 across, a sliver triangle in each side of it whose edge of 0.00095
stands 0.000475 in from the side's ends, reads open. And below the rounding of the coordinates, a quadrillionth of the
largest coordinate of the whole body, not of each part, the edges are matched within that rounding: at a tolerance of
nought `Validate` read a cube turned in space, a cylinder of 24 sides at the origin and the union of two turned boxes
made within a thousandth open along nearly every edge, and reads them closed. At every tolerance at or above that
rounding the rounding changes nothing, bit for bit.

The reading has its limits. Edges matched along one line are matched one after another, so a gap wider than the
tolerance reads closed where other edges lie in it within the tolerance of either side: that cube whose top stands
0.0015 over its sides, with a box of 1 standing against each side half way up the gap, reads closed and valid, as it
did. A short copy kept with a side of a hole cuts the open side into stretches no longer than the tolerance in the
same way: a sliver face with an edge longer than the tolerance, lying in a side of a hole wider than the tolerance, can
still read the hole closed. A speck, a body with no edge longer than the tolerance, is not closed to `IsClosed` and is
closed to `Validate`, which calls it not valid, enclosing nothing, as before.

The bodies that read otherwise now are these: a body whose copies of an edge are taken for one only through a
corner between them, which reads open; one closed by twin edges no longer than about the tolerance, which `IsClosed`
read closed and `Validate` open, and both read closed; and one closed only to the rounding of its coordinates, at
a tolerance below that rounding, which both read closed. `Mesh3.ToCells` and the IFC readers, which ask `IsClosed`,
follow it: `ToCells` refuses the cube with the specks given first, which it cut into 8 cells, and refuses as before
a body closed only to the rounding, the union at nought, which read closed it would have cut into no cell at all;
`MeshConvert`, `ProductConvert`, `SolidConvert` and `FlatGaps` read a solid as `IsClosed` reads it. The booleans, the
weld, the cutting of openings, the closing, the takeoff and `TryGetVolume` read the bodies they work on as they did,
the ends of the edges matched first and within the tolerance given and no wider, and every result of them is the same
to the bit, at every tolerance: 41 452 volumes and booleans of cracked cubes with openings within nought, a thousandth,
a hundredth and five hundredths, and 41 452 more within nought and 1E-12; 54 000 booleans of U bodies, and 54 000 more
within nought and 1E-12, of whose results `Validate` reads 1 523 valid it read not valid; 32 400 booleans of a cube
and boxes half a tolerance to two tolerances into, onto or beside it; 12 800 joins and cuts of a column and a beam;
600 plates with blind holes cut and 400 bands; and a cube less a box through it, far off and turned, within nought,
not worked out, with a warning, as before. `TryGetVolume` can so trust a body `IsClosed` reads open, the cube with
the specks given first, 1 000 005, and refuse one it reads closed, the union at nought; its own answer says whether
its volume can be trusted. `TryClose`, and the closing of the cuts `TrySubtractAll` would skip, read a body as the
booleans do, so that one `Validate` now calls valid can still be closed or refused: the slab 0.0012 thick slid by
0.0002 is refused, the gap too wide, and the one 0.0014 thick slid by 0.0005 welded, its 14 cubic millimetres moved
to 11.67. Some results of the booleans that `Validate` called not valid at a tolerance of nought, closed only to the
rounding of their coordinates, now read valid, those 1 523 of the U bodies among them, so a `TrySubtract` checked by
`Validate().IsValid` after it takes them; `TrySubtractAll` checks each cut as the booleans read it, and takes them as
it did. `IsClosed` reads a plate of 3 206 faces with 200 holes in 1.9 milliseconds where it took 9.7 on .NET 10, and
in 4.3 where it took 13 on .NET Framework 4.8, and a cube in 7 microseconds where it took 5 on .NET 10 and in 3 where
it took 5 on .NET Framework; `Validate` takes 2 to 10 % longer on .NET 10 for matching twin edges by their ends, and 4
microseconds more on a body of ten faces with edges no longer than the tolerance; `TryGetVolume`, `GetVolume` and the
booleans run as they ran.

**CHANGED.** Offsetting a curved loop cut at its crossings into many loops no longer slows with the square of them. Each
piece of the offset, and each point the offset is read at, was read against every box along the one axis the edges and
pieces are put in order on, and against every loop the crossings cut the loop into for which side of the region the point
lies on. The long edges along the arms of a plus reach every box along either axis, and the plus of 8 000 teeth 0.5 apart
across each arm, cut at a tolerance of 0.001 into 16 002 loops and grown by 0.3, read 992 million boxes to find where its
pieces cross, for the 189 152 pairs whose crossings are read. Where more than 64 boxes reach a box or a point along the axis,
those that overlap or hold it are found in a tree of the boxes, once the boxes read along the axis pass what making the tree
costs, and are read in the order the axis gives them; the tree is given up where it finds more than a fourth of the boxes,
or, for points, more than a sixteenth of the boxes along the axis, as along the chords of a star, and boxes that run out to infinity or are of no
number are read along the axis only. A region of more than 64 loops is read at a point against the loops whose boxes hold
it. Grown by 0.3 with round joins, the plus of 8 000 teeth to each arm takes 1.3 seconds where it took 20 to 28 on .NET 10, 1.5
where 39 on .NET 8 and 1.1 where 50 to 113 on .NET Framework 4.8; one of 4 000 teeth 0.85 where 5.0, 0.93 where 6.5 and 0.60 where
13; and one of 80 000 teeth to each arm, 160 002 loops, 7.1 seconds on .NET 10, 8.2 on .NET 8 and 13 on .NET Framework 4.8,
where by the square it would take most of an hour. With miters, the plus of 8 000 teeth takes 1.9 seconds where it took 26
on .NET 10 and 1.6 where 58 on .NET Framework 4.8. Loops of every day are offset in the time they took or less: grown by
0.3 at 0.001, a star of 49 points in 44 ms where 70 on .NET 10 and 79 where 119 on .NET Framework 4.8, 100 random corners in
33 ms where 67 and 53 where 140, a zigzag of 500 teeth in 5.3 to 6.4 ms where 7.4 to 7.8 and 13 to 23 where 64, a gear of
1 000 arcs in 51 to 55 ms where 63 to 67, a gear of 200 arcs in the 1.9 ms it took on .NET 10 and the 4.0 to 4.3 on .NET
Framework 4.8; the stars of 21 to 61 points grown and shrunk by 0.3 and 1 at a tenth in the time they took or up to 8 % less on .NET 10,
and in the time they took on .NET Framework 4.8, within the noise. Of 18 000 offsets of 1 000 random loops and chains,
dense loops, stars, diameters and combs, both ways, with mitred, round and chamfered joins at tolerances of 0.001, 0.01 and
0.1, and 1 440 of 720 pluses turned at any angle, with arcs and without, zigzags, gears of up to 2 400 arcs, random walks
and lattices, none gives another loop, edge or bulge, to the bit, nor do 9 000 of the first and all 1 440 of the second with
the trees read for every box and point; nor does any of 120 000 cuts of 20 000 loops at six tolerances, which make 20.8
million loops, nor are the 599 million crossings of those loops found otherwise.

**FIXED.** At a tolerance of nought a segment or a line without ends finds its crossing of a triangle or a face turned
about in space, and of every face of a body it passes through. The crossing a segment finds on a plane stands off it
by the rounding of numbers the size of the coordinates, and the triangle or face read that against the planar
tolerance of nought and refused it, as it refused a ray's before 13.0.0: of 1 000 segments from 30 under
the middle of a triangle turned about in space to 30 over it, 920 crossed nothing, and 928 of the same faces; of the
lines without ends along them 889 and 905; of 1 000 lines through a cylinder of radius 20 and height 50, its axis
leaning in space, 996 were given one crossing or none, 950 by its prepared form, and so were 981 of the 986 segments
that reach through it. A triangle or face now holds the crossing of a segment as it holds a ray's, to 1E-15 of the
largest coordinate of its own and the segment's, on its plane and past its rim, and a segment reaching from further
off than the triangle, the face or the body is large is read from a diagonal short of it, along its line; whether the
crossing lies between the segment's ends is read by the tolerance given. A line without ends is cut past what it is
asked of, as before, and read as that segment; the prepared body holds the crossings of its mesh to the rounding of the
whole mesh, as for a ray. Below the rounding, and only there, two crossings of a seam within eight times it of each
other are one, the segment along the seam of two sides of a cylinder of radius 20, through its axis, from 60, a million
or a hundred million out to as far beyond, being given its two crossings where it was given none; and a segment with an
end within the rounding of a triangle or face, its other end off the plane, meets it at that end where it touches it
as `CollidesWith` reads it, and nowhere else, so the crossing of the plane, worked out to the rounding, no longer falls
either side of the end: of 1 000 segments from 10 off the side of a box turned about in space to a point on it, the
crossing of the triangle under the point and the touch `CollidesWith` reads differed 512 times and differ none, and
the body, which `CollidesWith` and `DistanceTo` read as touched every time, gave 831 of them nothing and gives each
its point. In a body the end is read as the body reads it: met where the segment touches the face, or where the body
holds the end as `CollidesWith` holds it. On a single triangle or face an end so near is crossed only where
`CollidesWith` of the two finds them touching, and at a tolerance of nought it refuses about half of them: of 1 000
segments ending on points of the sides of turned boxes, 617 cross the triangle under their end, and of another 1 000,
some of them far out, 480 the triangle and 535 the face. `CollidesWith` of a segment and a body asks the boxes first as
far apart as the body holds a point, a billionth, or 1E-15 of the largest coordinate of the body and the end further
out than a million, and that reaches every point tolerance below it, not only those below the rounding: the segment
standing up from 5E-10 over the top of a box 100 by 80 by 50, which is nought from it, touched nothing and touches it
at a tolerance of nought, of 1E-12, of 1E-10 and of 3E-10, as does the one from 1E-7 over that box a thousand million
out at nought; of 25 000 prisms' answers its collision disagreed with its distance 69 times and does 4. A segment with a
coordinate that is not a number touches no body, as before. Where it reaches, at a tolerance below the rounding only,
but for that box: `TryIntersectWith` of a segment and a triangle or face and the lists of where they meet, the same of a
line without ends, `GetIntersections` of a segment or a line without ends and a body,
`GeoPreparedSolid3.GetIntersections` and `CollidesWith` of a line without ends, `GeoBvh3.GetIntersections` of one,
`CollidesWith` of a line without ends and a body, and what reads an edge's crossing of a triangle or face: whether two
triangles touch, the crossings of two regions, and the cutting of segments, chains and arc chains by faces and bodies.
At every tolerance at or above the rounding every answer is the same to the bit, but for the collisions that box lets
through: of 518 400 answers drawn at 1E-6, 1E-3 and 1E-2 for boxes, prisms, cylinders, cones and rings turned about in
space, at the origin and out to 1E9 (segments and lines without ends through them, into them, wholly inside, ending or
starting on a face, missing a face by half, three and a hundred times the tolerance, or a hair off it), none changed,
nor did any of 120 000 answers of rays and segments about boxes, U's, cylinders, cones and rings out to 1E9, nor any
boolean of the G2b corpora, at nought included; of the 138 840 answers at or above the rounding drawn at 1E-12 and
1E-10, 1 626 changed, each a segment missing a face by up to 1E-8 that was nought from the body and touched nothing,
and touches it now. At a tolerance of nought, of 172 800 such answers 53 260 were wrong and 131 are: 79 segments ending
or starting on a side of a cylinder or a cone made by `Revolve`, 76 of them a million and more out, where the side
stands off its own plane by billionths and the body does not hold the end either (parked); 48 lines without ends a hair,
1E-11, beside the long thin faces of a cone at the origin to a thousand out, held by a triangle of the prepared body
whose plane stands off its corners by more than the rounding, where 14 were before (parked); and 4 such ends on a single
face of a cone 100 out. Lines a hair off a cone are held so more often in a sample drawn for them: of 2 400 lines a hair
off any face of a cone turned about in space, at the origin to a thousand out, the prepared body read 282 as touching
where it read 96, and of 2 400 a hair under the disk of its base 594 where it read 206, a quarter of them. At 1E-12 and
1E-10, of 345 600 answers 77 347 were wrong and 32 412 are, nearly all of them segments and lines missing a face by
less than the body holds a point within, a billionth, or than the rounding of the coordinates far out; one count rose:
at 1E-12, segments missing a single face by up to a hundred times the tolerance read as crossing it 151 times where 4
did, each a miss smaller than the rounding of its coordinates, as at 1E-12 any coordinate past 1 000 is. A single
segment's crossing of a triangle or a face now asks first whether the coordinates reach the rounding: at a tolerance of a
thousandth, of 64 triangles turned about in space some 2 000 out, it takes 68 nanoseconds on .NET 10 where it took 65,
and 68 where it took 65 for a face, and on .NET Framework 4.8 394 where it took 382 and 407 where it took 391. A
segment through a cylinder of 300 faces turned about in space takes 23.9 microseconds where it took 23.1 on .NET 10 and
70 where it took 75 on .NET Framework 4.8, a line without ends through the prepared cylinder 6.0 where it took 6.2 and
27.8 where it took 27.4, and through a box 0.84 where it took 0.80 and 3.86 where it took 3.66. At a tolerance of
nought they take longer than they did, for now doing the work they skipped, and about as long as at a thousandth: the
line through the prepared cylinder 7.0 microseconds on .NET 10 where it took 4.7, and the segment through the body 23
where it took 3.3. 16 new tests.

**FIXED.** The distance from a curved chain or a curved loop in space to a point is measured to each straight edge as it
is, however short, as it is in the plane. A straight edge no longer than the point tolerance was read as its start
point, so a point beside its far end stood as far from the chain as from that start: at a tolerance of a thousandth,
the chain built of the straight edges from (0, 0, 0) to (10, 0, 0) and on to (10, 0.0006, 0) was 0.00108 from
(10.0009, 0.0006, 0), and the point off it, where it is 0.0009 and on it. `IsPointOn` and `Locate` of a chain and
`IsPointOn` of a loop follow, and so does everything that asks them or the distance: the point's `DistanceTo` of them,
a loop's `SignedDistanceTo` of a point off its plane, a ball's `DistanceTo`, `CollidesWith` and `GetShortestLineTo` to
them and theirs to it, whose line lands on the short edge where it landed on its start. Of 300 000 random chains and
loops of straight edges and arcs from a twentieth of the tolerance to 100 long, many of them no longer than the
tolerance, at tolerances of 0.001 and 0.01, at the origin and out to a million, 63 760 distances were more than a
ten-thousand-millionth of the size of their coordinates off the exact one, 376 points read off the shape that were on
it, and 8 028 balls read the wrong distance and 126 the wrong touch; none does now. Of the 300 000 points about edges,
chains and loops drawn for the bends of 13.0.0, the 2 313 distances off the exact one on chains and loops, each nearest
such an edge, and the 68 wrong answers of `IsPointOn` are gone. A cut of a chain or a loop at a point is made where it
was: a point now on the shape only beside a straight edge no longer than the tolerance, further than the tolerance from
both its ends, cuts nothing and is refused, the chain handed back whole and the loop opened at its start, as before; the
loop (0, 0, 0), (10, 0, 0), (10, 0.0006, 0), (0, 10, 0), built within a ten-thousandth, holds (10.00098, 0.0003, 0) at
a thousandth and is not cut there; of the 300 000 random shapes, every cut at the point is the same to the bit. Every
answer about a chain or a loop with no straight edge as short as the tolerance is the same to the bit, and so is `GetClosestPointOnBoundary` of every one, which, as in the plane, still
carries a point to the start of a straight edge no longer than the tolerance, so that the shortest line from the chain
to (10.0009, 0.0006, 0) is 0.00108 long, from (10, 0, 0), where the distance is 0.0009. A straight edge on its own that
short is read as its start, as a straight `GeoEdge2` or a segment you give is. In the loop's plane, `Locate` and
`SignedDistanceTo` still read a side shorter than `Tolerance.Global` as gone, so they can disagree with `IsPointOn`
there. The distance from a chain of 100 edges takes 8.5 microseconds on .NET 10 where it took 9.6, and 28.5 to 29.1 on
.NET Framework 4.8 where it took 29.1 to 29.2. 7 new tests.

**FIXED.** `TryCutOpenings` at a point tolerance below the rounding of the coordinates, a quadrillionth of the largest of
them, nought among them, no longer returns true with a body left open or wrong. The queries have cut the openings within
that rounding since they were held to it; the cut itself did not, and at nought a plate 200 by 100 by 3 turned about in
space, with a hole 20 by 20 through it, came back as a shell of 7 faces, its bottom lost, and the cut said it had worked.
The openings are cut within the rounding now, and where cutting them in together leaves the material open they are cut
in one at a time, each kept only where it closes, not taken out by a difference, which within the rounding came back
closed and wrong: the plate a million out with two holes 1 and 1.5 deep from under it, the first taken out by a
difference, lost nothing to it and held 57 600 where it holds 56 100. The corners the cut leaves a rounding apart are
then made one, by the weld within the rounding, where that closes the material within the tolerance given, so that
`TryGetVolume` of what comes back trusts it at that tolerance; of the 8 385 cuts below it in the run below, it refused
3 322 without the weld and refuses 155, whose corners stand further apart than the rounding. The plate with the hole comes
back closed, holding 58 800. A planar tolerance below the rounding with a point tolerance that is not cuts as it did:
raised, the point tolerance a thousandth and the planar nought, a plate with a hole flush with each face came back in 35
faces where it comes in 124, and one with two holes overlapping was refused. Of 5 000 plates, boxes and prisms shaped as
an L or a star, with one to three openings through them or blind from either face, turned about in space or not and 0 to
1E9 out, cut at nought, 3 451 came back open, 83 closed and wrong and 422 refused; none comes back open or wrong now, and
29 are refused. Of the 3 417 of them cut at 1E-12 where that is below the rounding, 901 came back wrong, 898 of them with
no opening cut, and 1 507 were refused; none comes back wrong now, and 3 are refused, one of them cut right before. Every
one that comes back is valid at the default tolerance. Openings that touch or overlap each other or lie flush with a
face are refused more often than the rest: of 120 plates turned about in space, 0 to 1E8 out, with a hole flush with
each face, 34 are refused at nought where 20 were and 95 came back wrong or open; of 120 with a hole from over and one
from under overlapping, 37 where 26 were and 89 came back wrong or open. What comes back can be valid at the default
tolerance and not at the rounding, for slivers a rounding thin: 2 of those 120 with the overlapping holes. The 16 583
cuts at 1E-12 where that is at or above the rounding, and at 1E-6, 1E-3 and 1E-2, are the same to the bit, as are the
1 440 at a point tolerance of a thousandth and a planar of nought and 1 440 at the default of the 12 shapes the plates
were drawn from, and the 145 000 answers of the queries tried on prisms with openings and on bodies turned about in space
far out. `GetVolume`, `TryGetVolume`, `GetSurfaceArea` and `GetCentroid` of a body with openings do not cut them so: at
nought that plate measures 60 000, its hole not taken out, and `TryGetVolume` says it can be trusted. At a thousandth the
cut takes the time it took, within the noise: a plate 400 by 200 by 10, turned about in space, with one hole through it
1.7 milliseconds at the origin and 0.31 a million out on .NET 10 and 0.66 on .NET Framework 4.8, with twenty 4.6 to 4.9
and 9.3 to 9.5. At nought, where it gave a body open or wrong, it takes 2.5 where 1.8 and 1.25 where 0.66 at the origin,
0.37 where 0.81 and 0.83 where 1.9 a million out; with twenty holes 7.5 where 4.6 and 16.7 where 9.2 at the origin, and 7.8
where 11.3 and 18.2 where 28.2 a million out, the weld most of what it adds.

**FIXED.** A difference of two closed bodies could leave whole a cell of one that is not convex, where planes of the
other ran through it and would not cut it, and trust what it gave. A U 30 by 10 by 10 whose bottom and outer sides are
cracked under the sides and the floor of a box dipping 0.088 into one of its arms is one cell to the box's planes, and was
read by the middles of its parts either side of each plane and of the four two planes make: those holding the 2.55
cubic millimetres the box takes held more of the U beyond the box, or had their middles in the notch, outside the
material, and were not read. The U less the box held 2 667.83, trusted, where 2 665.28 is left, and with the box dipping
through the floor of the notch, or into both arms and the floor at once, it was left whole the same way. Where one
such plane crosses a cell, or two, or three that meet at a point within the other body's box, or three of which two are
parallel and the third runs across them, the parts all of them make are read too, each by its middle wherever it lies,
since no face of the other body runs between a part and its middle; the U less the box holds 2 665.15. A common part
judged so whichever body is cut, and a union judged so whichever body's cells are kept, are reported as not worked out
rather than taken one way: the box dipping into the U's arm, joined to it, held 3 145.93 where the two hold 3 143.38,
what they share counted twice. A union judged so one way only is taken where the union cut the other way round, the
second whole and the cells of the first beyond it, holds as much within the tolerance times its area. Of 45 000
booleans of cracked U's and cracked boxes in and about their notches, in five runs, 325 differences of the U less the
box trusted and wrong are right, and 5 the difference gave as nothing, where 2 436 to 2 627 are left, are right; 6 more
given as nothing are trusted now and 1.9 to 5.8 off what is left; 3 common parts given as nothing are right and 2
trusted and wrong are not worked out; 4 unions trusted and wrong are right and 2 002 are not worked out, so that none is
trusted and wrong; and 20 unions that counted what the two share twice, 1.13 to 14.38, within the tolerance times their
area, are not worked out. 1 324 wrong before are wrong by another amount, 473 of them nearer and the rest farther by no
more than 0.005, and 7 right move within the bound, 6 of them nearer; none right or refused before is wrong now. The
27 000 booleans of the same U's with boxes not cracked are the same to the bit, as are 24 000 volumes of cracked cubes
with openings and 17 443 of 17 452 booleans of them; the other 9, trusted and wrong at a tolerance of nought, are not
worked out.

Not fixed: a difference judged so with no other result to take is still taken as its cells glue: the cracked box
less the cracked U it dips into, the box's cells judged so by the U's planes, holds 478.10
where 475.54 is left, trusted, and of the 45 000, 1 047 such differences of the box less the U and 576 of the U less
the box are trusted and wrong, as they were or by another amount. Checking such a difference against the other body
cut by every plane of both would leave 13 of the 45 000 trusted and wrong, but refuses 30 differences that are right
within the bound and 166 of the 325 put right here, and is not done. A cell crossed uncut by more than three planes, by
three meeting outside the other body's box or along one line, or by three parallel, is read as before; and where the
part the box takes is within the tolerance times the area of the body it is taken from, as from a comb of 10 notches,
the cell is read as it was. A comb of 2 notches, cracked the same way under the sides of two such boxes taken as one
body, one dipping into the tooth beside each notch, holds 4 391.85 less them where 4 386.75 is left, as it did. The U less the
box takes 3.3 milliseconds on .NET Framework 4.8 where it took 1.8, the least of seven runs, cut now by every plane of
both; a comb of 100 notches less one box dipping into a tooth by its middle notch takes 47 where it took 45, a comb of 10
5.1 as it did, and the comb of 100 less 100 boxes 16.9 seconds where it took 16.5.

**FIXED.** `DxfDatabase.WblockCloneObjects`, and an INSERT of another drawing copied alone and added by `AppendEntity`,
wrote blocks that insert themselves where the drawing copied into holds an INSERT of a block it has not, as a drawing
read from a file may. This drawing's P holding an INSERT of M, and the other drawing's M an INSERT of P, an INSERT of M
copied brought an M that places P, which then placed that M: P, M, P without end, which a reader places 64 deep at most
and AutoCAD's AUDIT takes out. Such a copy is refused now with an `ArgumentException`, and nothing is written; so is one
whose blocks insert one another in the other drawing (M inserting N and N inserting M), and one whose blocks would come
back to the block the copies are added to (an INSERT of Q copied into W, the other drawing's Q inserting W). The blocks a
copy brings are walked once each, with this drawing's blocks of the names they give, before anything is written.
`DxfDatabase.Insert` of the other drawing refused some of these rounds and wrote the rest; it refuses them all now, but as
the merge comes to the block refused, as when it refused before: the blocks merged before it stay written, and so does
that block, empty, which this drawing's INSERT of its name then places. This drawing's P holding an INSERT of M, and the
other drawing's A a line and M an INSERT of P, the merge is refused at M and leaves A and an empty M. A block AutoCAD
names itself is no longer copied under a name an INSERT of either drawing gives of a block neither has: this drawing's P
holding an INSERT of *U1, which it has not, the other drawing's *U1, inserting P, came as *U1, placed by that INSERT, so
that P and *U1 placed each other; it comes as *U2 now, and P's INSERT of *U1 places nothing, as before. Nor does
`DxfBlockReference.ConvertToStaticBlock()` name its new block so: PPP holding an INSERT of *U1, a block the drawing has
not, the static block came as *U1 and PPP placed it; it comes as *U2. `ConvertToStaticBlock(name)` refuses, with an
`ArgumentException` and before anything is written, a name an INSERT of the drawing gives where the new block would come
to insert itself through that INSERT: PPP holding an INSERT of MM, and the block made static an INSERT of PPP, MM is
refused, where the copy into it was refused before and an empty MM left behind. Of 20 000 pairs of drawings of R12 of a
few blocks, with rounds and INSERTs of no block among them, copied six ways (120 000 answers), 2 825 wrote a round: 2 554
are refused now and 271 come under another name with none. 1 064 more differ only by the name an anonymous block is
copied under, where an INSERT of a block neither drawing has gave the name: 20 of them through an INSERT in a block
defined twice, whose second definition the reader passes over, and 2 of them refusals of `Insert` in both, whose blocks
merged before the refusal differ by that name. The rest are the same, bit for bit. Of D7's 6 000 drawings of a few blocks
inserted four ways and edited, 9 wrote a round and are refused. The rest are the same, bit for bit: the 1 907 drawings
and 3 550 answers of the members run over the drawings of the corpus and the fixtures, their 261 resaves, and the
607 940 lines of D8's runs of edits, merges and copies.
`WblockCloneObjects` of an INSERT of 3 000 blocks nested one inside the next takes 133 to 139 milliseconds on .NET
Framework 4.8 where it took 119 to 139, and of 10 000 635 to 675 where 615 to 636, 21 more in the middle of the runs; on
.NET 10, where the runs spread wider, 133 to 203 where 108 to 193, and 302 to 463 where 311 to 387, the middle of the runs
the same. The first anonymous block copied into a drawing reads the names its INSERTs give, once: a dimension copied into
a drawing of 20 000 INSERTs takes 62 milliseconds where it took 46 on .NET 10, and 82 where 69 on .NET Framework; those
copied after it take what they took. Each anonymous block copied into a drawing whose INSERTs name many blocks it has not
reads their names once: 20 INSERTs of *U1 appended one by one to a drawing of 2 000 INSERTs of missing *U blocks take 72
to 74 milliseconds where they took 24, and come as *U2001 to *U2020 where they came as *U1 to *U20, each placed by an
INSERT of a block that was missing.

## 13.0.0

**BREAKING.** The DXF types move from the namespace `GeometryHelper.Export` to `GeometryHelper.Dxf`: a
`using GeometryHelper.Dxf;` replaces `using GeometryHelper.Export;` for `DxfReader`, `DxfWriter`, `DxfDatabase` and every
other `Dxf` type, or joins it where the code writes OBJ, SVG or WKT as well, and a name written in full changes alike:
`GeometryHelper.Export.DxfWriter` is `GeometryHelper.Dxf.DxfWriter`. Each type keeps its name and members as they were.
`ObjWriter`, `SvgWriter` and `Wkt` stay in `GeometryHelper.Export`.

**BREAKING.** The read type of `DxfReader`, `DxfEntity`, is renamed `DxfShape`, so that `DxfEntity` is the entity of the
new editing model, as AutoCAD names its own: `DxfDocument.Entities` is `DxfDocument.Shapes`, `DxfBlock.Entities` is
`DxfBlock.Shapes`, `DxfWriter.Add(DxfEntity)` is `DxfWriter.Add(DxfShape)`, and `new DxfEntity(shape, ...)` is
`new DxfShape(shape, ...)`; `OnLayer` and `OfType<T>` give and walk shapes as they did. Nothing else of the reader changes:
338 test files, in 4 forms each (as written, with a mark of UTF-8, with CRLF, in the code page 932) under
4 sets of options, read to the same groups, looks and shapes, bit for bit, 5 208 readings; and every shape written back
by `DxfWriter.Add(DxfShape)` gives the same bytes, 676 files. The release that ships this is 13.0.0.

**BREAKING.** `DxfGroup`, a group of a file as it stands, is renamed `DxfTypedValue`, as AutoCAD's `TypedValue`, which frees
the name for a GROUP of the drawing: its `Code` is `TypeCode`, a `short` as AutoCAD's is, and its `Value` is the text the
file prints, as it was. Its public constructor is `new DxfTypedValue(typeCode, value)`: `new DxfTypedValue(1000, "B-12")`
is a text of extended data and `new DxfTypedValue(70, "4")` the header's millimetres. `DxfShape.Groups`, `DxfDocument`
and the editing model give and take it where they gave and took a `DxfGroup`.

**BREAKING.** The members of `DxfUnits` are AutoCAD's `UnitsValue`, named and numbered as AutoCAD names and numbers them:
`Undefined` (was `Unitless`), `Millimeters`, `Centimeters`, `Meters`, `Kilometers`, `MicroInches`, `Nanometers`,
`Decimeters`, `Dekameters`, `Hectometers`, `Gigameters`, `Astronomical`, `USSurveyFeet`, `USSurveyInch`, `USSurveyYard`
and `USSurveyMile`; `Inches`, `Feet`, `Miles`, `Mils`, `Yards`, `Angstroms`, `Microns`, `LightYears` and `Parsecs` keep
theirs, and every number is as it was. `DxfReadOptions.ToMillimetres` is `ToMillimeters`, and the look of a shape read is
spelled as AutoCAD spells it: `DxfShape.Colour`, `TrueColour` and `Lineweight` are `Color`, `TrueColor` and `LineWeight`,
and so are the arguments of `new DxfShape(shape, layer, color, trueColor, linetype, lineWeight, thickness)`.

**BREAKING.** `GeoArc2.FromThreePoints` and `GeoArc3.FromThreePoints` throw `InvalidOperationException` for many nearly
collinear points that gave an arc before: those whose middle lies within the point tolerance of the line through the
other two, and those whose arc would miss one of its own points by more than the tolerance. Of 300 000 random triples
nearly on one line, at a thousandth, 35 100 of the 45 415 arcs given in the plane and 26 071 of the 36 386 in space are
refused now; the **FIXED.** entry on the two says why. Code that may meet such points catches the refusal and takes them
as a line, or passes a tolerance of nought, `default(Tolerance)`, as `ToGeoArc2` and `ToGeoArc3` of CadConvert now do
for an AutoCAD arc, which refuses only points on one line to the digit and an arc that would miss them by more than a
thousand-millionth of their size.

**NEW.** `DxfDatabase` opens a DXF file to edit it as AutoCAD's `Database` edits a drawing, and saves it with nothing lost:
`DxfDatabase.Open(path)`, `Open(stream)` and `Parse(text)`, then `Save(path)`, `Save(stream)` or `ToText()`. Opened and
saved with nothing changed, a file is written back byte for byte: its version from R12 to R2018, its line ends, padding,
comments and mark of UTF-8, its code page, and every section, table, object and entity, the HATCH, DIMENSION, LAYOUT and
extended data the library does not read among them; a change writes the records it is about anew in the file's own style
and every other record keeps its bytes. The classes are AutoCAD's with Dxf in front, used directly, with no `ObjectId` and
no transaction: `ModelSpace`, `PaperSpace` and every block are a `DxfBlockTableRecord` of the `BlockTable`, enumerated as
their `DxfEntity` objects (the same object each time), appended to by `AppendEntity` and taken from by an entity's own
`Erase()`; `BlockTable.Add(new DxfBlockTableRecord { Name, Origin })` adds a block; the `LayerTable` (`DxfLayerTableRecord`:
`Color`, `ColorIndex`, `TrueColor`, `LinetypeName`, `LineWeight`, `IsOff`, `IsFrozen`, `IsLocked`, `IsPlottable`), the
`LinetypeTable` (`DxfLinetypeTableRecord`: `AsciiDescription`, `PatternLength`, `NumDashes`, `DashLengthAt`, and new simple
patterns, its `Comments` the same as its description, as ObjectARX keeps them) are found by name and added to, and the
`TextStyleTable` is found by name and read, its `Add` refused until a later release adds text styles; the header is read
and set raw by `GetHeader` and `SetHeader` (its groups each a `DxfTypedValue`) and through 28 typed properties (`Clayer`,
`Insunits`, `Extmin`, `Extmax`, `Handseed`, `Celtype`, `Cecolor`, `Celweight`, `Ltscale`, `Limmin`, `Limmax`, `Insbase`,
`Pdmode`, `Pdsize`, `Textsize`, `Textstyle`, `Dimstyle`, `Lunits`, `Luprec`, `Aunits`, `Auprec`, `Angbase` in radians,
`Angdir`, `Orthomode`, `Fillmode`, `Mirrtext`, `LineWeightDisplay`, `Saveproxygraphics`), those R2000 brought refused in a
file of R12. Every object is a
`DxfDBObject` (`Handle`, `OwnerHandle`, `IsErased`, `XData` set application by application as AutoCAD sets it, its
application registered, `ExtensionDictionary`); every entity a `DxfEntity` (`Layer`, `Color` of the new `DxfColor`,
`ColorIndex`, `TrueColor`, `Linetype`, `LinetypeScale`, `LineWeight` of the new `DxfLineWeight`, `Visible`, `Owner`,
`BlockName`, `TransformBy`, `GetTransformedCopy`, `GetGeometry` as `DxfReader` reads it, `GeometricExtents`, `Ecs`,
`SetPropertiesFrom`, checked whole before anything is written, `SetDatabaseDefaults` of a drawing given or of its own,
`Erase`); a curve a `DxfCurve`, walked as AutoCAD walks one by its parameter and by distance. Model space and paper space
are `*Model_Space` and `*Paper_Space` whatever the file calls them, and stand for their records from the moment the drawing
is opened; a block is walked over the entities it holds when the walk begins, so that `foreach (DxfEntity e in
db.ModelSpace) e.Erase();` erases them all, as in AutoCAD; a true colour's `Color` carries the index nearest it, orange the
index 30, and is written with it; a text of extended data reads its escapes (`\U+0110`) as letters, as a layer's name does.
A record that follows an entity, a VERTEX or an ATTRIB, is owned by it, keeps its own look and extended data, and is put in
or taken out alone, every other keeping its handle. Each type of entity is a class of AutoCAD's name, `DxfLine` among them
(`StartPoint`, `EndPoint`, `Normal`, `Thickness`, `Delta`, `Length`, `Angle`, offsets, splits, extensions), made by
`new DxfLine(start, end) { Layer = "steel", ColorIndex = 1 }` and `db.ModelSpace.AppendEntity(line)`; a type with no class
yet is a `DxfUnknownEntity`, kept as the file has it, its look editable and itself erasable. `Clone()` copies any entity as
it is, a `DxfUnknownEntity` among them, a copy always of its original's class, and `Explode()` is every entity's, refused
with `NotSupportedException` by every class but those the entry on `Explode()` below names. A property given the value it
has changes nothing, to the byte. What is appended takes a handle above every handle of the file and every handle something points at, `$HANDSEED` set above it, the block's
BLOCK_RECORD as its owner and group 67 in paper space; its layer is added to the table where it is not listed, a file of R12
takes no subclass markers and has no SPLINE, ELLIPSE, RAY, XLINE or LWPOLYLINE, lineweights, true colours, or a
linetype scale or visibility of an entity's own, which are refused, nor MTEXT, which is written there as TEXTs (see below),
and an entity of R12, or a copy of one, appended to a drawing of R13 or later is given AutoCAD's subclass markers in their order, its owner and a handle; one of R13 or later
appended to a drawing of R12 keeps the groups R12 has for its type, each from the part of the record it belongs to (a
centred ATTRIB stays centred, its AcDbAttribute part's 71 and 72 no longer read as its text's), its embedded objects left
out. A copy appended to another drawing has its pointers taken to the objects of the same name there, as AutoCAD's deep
clone maps them: a material, plot style or visual style by its dictionary's and its entry's names, a table's record by its
name, any other pointer and a handle of extended data (1005) left out; within one drawing they are kept. A copy of an
associative HATCH, in either drawing, leaves its boundary objects behind and is no longer associative, as AutoCAD's COPY
leaves it. `TransformBy` is
refused by a `DxfUnknownEntity` alone, whose geometry is kept as the file has it, and a copy of an erased entity is refused,
as AutoCAD refuses an erased object. An entity erased takes all its records with it, a POLYLINE its vertices and an INSERT its attributes; a GROUP no
longer lists it (an emptied one goes), nor a list of reactors, a SORTENTSTABLE or a dictionary, the objects it owned go
with it, and at most ten other pointers at it are named in the log. Letters of a code page whose bytes would read back as
UTF-8 are written as their escapes, and a file that names no version but writes subclass markers is written in the forms
of R2000. `$EXTMIN` and `$EXTMAX` grow to hold what is written in model and paper space, are left as they are by what is
written in a block, and are never shrunk. A look, header or table property given the value it has never throws, whatever
the file holds. Held against ezdxf 1.4.5 on its drawings
of R12, R2000, R2007 and R2018: recover and audit find nothing to fix in what the model writes, ezdxf reads each value as
set, and every untouched record keeps its bytes (188, 308, 361 and 363 of them). 100 000 lines of 10.5 MB open and save in
36 ms on .NET 10, are walked as `DxfLine` objects in 21 ms, and 10 000 lines are appended in 96 ms. `DxfWriter` writes the
same bytes.

**CHANGED.** `DxfReader` reads a file in a code page of letters of more than one byte (932, 936, 949, 950, 54936, 51932,
1361 and every other the runtime has) line by line, as AutoCAD reads it: a byte that opens a letter at the end of a line no
longer takes the line feed after it, which made the line after it part of the value, and no state of the page runs on from
one line into the next. The TEXT whose words end in the byte 0x82 before the line feed, in a file of R2000 in the page 932,
followed by a line that is no group code, was read with that line in its words and is refused now naming the line, as
`DxfDatabase` refuses it; and a file whose lines that took the line feed put out of step, refused before, is read, the TEXT's
words ending in the replacement of the lone byte. A file with letters beyond ASCII whose header names a code page that does
not read each byte of ASCII alone as itself is refused with one message that names the page and no line: UTF-16 (1200,
1201) and EBCDIC (37), which read the letters as others and were refused naming whatever line their reading made of the
bytes, and ISO-2022 (50220 to 50229), HZ (52936) and UTF-7, which read an escape, a shift or a sign as a change of state and
left it out, so that the code line ` 10` with ESC ( B after it was read as 10. The national 7-bit pages (IA5 German,
Swedish and Norwegian, 20105 to 20108), T.61 (20261), ISO 6937 (20269) and x-mac-thai (10021), which read a few signs of
ASCII as other letters, are refused too where the file has letters beyond ASCII; AutoCAD writes none of them. Every other file
reads as it did, to the bit: 298 files, those the reader's tests read and 46 others, in up to 9 forms each (with a mark of
UTF-8, CRLF, and letters of Windows-1252, 1250, 932 and UTF-8) under 4 sets of options, with the code pages of Windows
registered and without, 10 616 readings each way; 46 of them under 8 sets of options from their text; and 3 000 random
block graphs. A file in the page 932 is read 0.04 s slower in 0.3 s, a file in any other as fast as before.

**NEW.** `DxfCircle`, `DxfArc`, `DxfEllipse`, `DxfDBPoint`, `DxfRay` and `DxfXline`, AutoCAD's `Circle`, `Arc`, `Ellipse`,
`DBPoint`, `Ray` and `Xline` for a DXF file: every member AutoCAD declares, readable and settable, walked as AutoCAD walks
the curve (by angle on a circle or arc, by eccentric angle on an ellipse, by the distance from the base point on a ray or
xline), and moved as ezdxf moves one. A circle or arc gives its centre in world coordinates though the file holds it in
the system of its normal; an arc's angles and an ellipse's `StartAngle` and `EndAngle` are radians, the latter measured
from the major axis and not the parameters of groups 41 and 42. A circle or arc refuses a transformation that would make
it an ellipse. R12 refuses an ellipse, a ray and an xline.

**NEW.** `DxfPolyline` (LWPOLYLINE), `DxfPolyline2d` and `DxfPolyline3d` (POLYLINE) with their vertices `DxfVertex2d` and
`DxfPolylineVertex3d`, `DxfSpline`, `DxfFace` (3DFACE), `DxfSolid` (SOLID), `DxfPolyFaceMesh` with `DxfPolyFaceMeshVertex`
and `DxfFaceRecord`, and `DxfPolygonMesh` with `DxfPolygonMeshVertex`: the AutoCAD classes of the same names, read, set,
made in code and appended, erased and moved. A polyline is walked by AutoCAD's parameter, the index of a vertex plus how
far along its segment: the polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex is 25.71 long, and its
parameter 1.5 is (15, 5). A spline is walked by its knot value. A vertex set, added or taken out is that one record
written, put in or taken out, every other vertex keeping its handle, and its owner is its polyline; moving a polyline of
2 000 vertices writes them in one pass, 50 milliseconds on .NET 10. A LWPOLYLINE or a SPLINE added to a file of R12 is
refused; a `DxfPolyline2d` is the polyline R12 holds.

**NEW.** The curves of the editing model edit as AutoCAD's do. Every curve gives the `Spline` that is exactly it (a circle of
radius 10 is the rational quadratic of 9 control points) and is projected onto a plane by `GetProjectedCurve` and
`GetOrthoProjectedCurve`: a circle of radius 10 on the floor projected along (0, 0, 1) onto the plane y + z = 0 is a
`DxfEllipse` of radii 14.14 and 10, a polyline with arcs onto a plane not its own the `DxfSpline` of them. The three polylines
and the spline are cut by `GetSplitCurves`, offset by `GetOffsetCurves`, to the right of their way so that a closed polyline
running counter-clockwise grows (the square of side 10 offset by 1 is 12 by 12), the side an arc's radius grows to;
`DxfLine`, `DxfRay` and `DxfXline` offset to that side too, where they went to the left before, so that every curve
agrees. They are run on by `Extend`, a line along itself, an arc round its circle, a spline along the polynomial of its
end span. `DxfPolyline.ConvertFrom` and `ConvertTo`
turn a LWPOLYLINE into a POLYLINE of the plane and back, the one taking the other's place with its handle where asked. A
polyline is fitted by `CurveFit`, `SplineFit` and `ConvertToPolyType`, of the header's SPLINETYPE and SPLINESEGS, and
straightened again; a polygon mesh smoothed by `SurfaceFit` and `ConvertToPolyMeshType`, of SURFTYPE, SURFU and SURFV, and
desmoothed by `Straighten`. A spline gives and takes its whole `NurbsData` and `FitData`, has its degree raised exactly by
`ElevateDegree`, control points put in and taken out (`InsertControlPointAt`, `RemoveControlPointAt`), fit points put in,
moved and taken out with the curve fitted anew through them, `UpdateFitData`, `ModifyPositionAndTangent`, `Rebuild` and
`ToPolyline` and `ToPolylineWithPrecision`. A point or face of a polyface mesh erased keeps the mesh's counts and the
faces' numbers true; a polygon mesh's point has its `VertexType`. A vertex of a POLYLINE appended or set one at a time no
longer reads the whole polyline each time to grow the drawing's extents: 20 000 vertices appended one by one take 0.17 s on
.NET 10, where 10 000 took 81 s, and 10 000 points appended to a polyface mesh 0.05 s, where they took 32 s. A LWPOLYLINE's
vertex put in, set or taken out rewrites only that vertex's groups and the count: 10 000 put in one by one take 0.8 s, where
they took 97 s, and set one by one 1.2 s, where they took 287 s. Straightening a fitted polyline or desmoothing a mesh takes
the made vertices out in one writing: 90 000 in 0.6 s, where they took 29 s. A spline of 200 spans made a polyline of 100
vertices takes 0.04 s, where it took 151 s, the lengths of its spans measured once; one of 1 000 spans is rebuilt of
5 000 control points in 0.05 s, its least squares solved over their band. 500 points of a polyface mesh of 20 000 erased
one at a time take 5 s, where they took 53 s, and no longer grow the extents by the mesh as it stands half renumbered.

**NEW.** `DxfDBText`, `DxfMText`, `DxfBlockReference`, `DxfMInsertBlock`, `DxfAttributeReference` and
`DxfAttributeDefinition` read and edit TEXT, MTEXT, INSERT, MINSERT, ATTRIB and ATTDEF with AutoCAD's names and meanings,
points in world coordinates. An INSERT moved, turned, scaled or given a new normal carries its ATTRIBs with it;
`AttributeCollection.AppendAttribute` adds one, `SetAttributeFromBlock` fills one from its ATTDEF, and erasing one keeps
the others' handles. `Explode` gives the block's entities one level down, as AutoCAD's `BlockReference.Explode` gives
them: the block's ATTDEFs among them, each a `DxfAttributeDefinition` placed as the rest are, and none of the INSERT's
ATTRIBs, whose values stay in its `AttributeCollection`; the INSERT of BOLT whose block holds a CIRCLE, a LINE and the
ATTDEF MARK, and which gives MARK the value B-12, explodes into a circle, a line and the definition MARK. An INSERT whose
block would insert itself is refused. A nested INSERT that cannot hold the placing is exploded in its turn, one of a block
already being exploded further out is refused with an `ArgumentException`, and what exploding makes is counted first and
held to the reader's 2 000 000 (`DxfReadOptions.MaxPlacedEntities`), past which an `InvalidOperationException` is thrown
and nothing is given: a block of 100 INSERTs turned by 20 degrees, of a block of 100 INSERTs turned by 10 degrees, of a
block of 100 lines, inserted at twice its width, explodes into 1 000 000 lines; with 200 lines it is refused in a quarter
of a second. MTEXT columns are read from R2018's embedded object or the ACAD extended data.

**NEW.** The rest of AutoCAD's members of text and blocks. `DxfMText` is set in columns as AutoCAD sets one
(`ColumnType`, `ColumnCount`, `ColumnWidth`, `ColumnGutterWidth`, `ColumnAutoHeight`, `ColumnFlowReversed`,
`SetStaticColumns`, `SetDynamicColumns`, `SetColumnHeight`), written in the MTEXT's embedded object from R2018 and in the
extended data of ACAD before it (`ACAD_MTEXT_COLUMN_INFO`, `ACAD_MTEXT_DEFINED_HEIGHT`), as ezdxf writes and reads them:
three static columns 12.5 wide with gutters of 2.5 take 42.5 across. The values the embedded object repeats, the
attachment AutoCAD reads from it among them, follow the MTEXT's own, a scale stretches the columns as ezdxf stretches them,
and an MTEXT made in code is given the form of the drawing it is added to. Before R2018 the columns after the first are
MTEXTs of their own, linked to the first (`ACAD_MTEXT_COLUMNS`). The first is one text with them, as AutoCAD and ezdxf take
it: `TransformBy` and its `Location`, `Rotation`, `Direction` and `Normal` move them with it as ezdxf moves them,
`TextHeight` and `Height` give them the same, and `Erase` takes them with it; its `Contents` and `Text` are its own
column's words alone, and a block walks and counts it with its linked columns as one, as AutoCAD's database holds
them. A copy of the first, in its drawing or another, holds the
words of them all itself and links to none of them, so that no two MTEXTs claim the same columns, and an INSERT of a block
holding them explodes into that one MTEXT, as ezdxf reads it. `Height` before R2007, which has no group 46, is the height of
columns of one height in the extended data (`ACAD_MTEXT_DEFINED_HEIGHT`), read and set there: 30 for ezdxf's three static
columns 30 high. `LineSpaceDistance` is the spacing factor times
single spacing, 5/3 of the letters' height: 5 for letters 3 high. `SetAttachmentMovingLocation` holds the MTEXT by another
of its points where it stands, by its width and the height of its words (43): the MTEXT 40 wide at (20, 40, 0) held at
its top centre stands at (40, 40, 0). `ShowBorders` is its text frame (flag 16 of group 90), and `BackgroundTransparency`
its group 441, of the new `DxfTransparency`. `DxfBlockReference` has `ExplodeToOwnerSpace`, which appends what `Explode`
gives to the INSERT's block and keeps the INSERT, as AutoCAD keeps it (erase it after, as AutoCAD's EXPLODE does), or
appends nothing where the drawing cannot hold a part, as R12 an ELLIPSE, and refuses a MINSERT of more than one cell,
as AutoCAD's EXPLODE refuses one; `UnitFactor`, the block's units against `$INSUNITS` (25.4 for a block in inches in a drawing of
millimetres, 1 where either is not given); `GeometryExtentsBestFit`, the box of the shapes placed, after a parent
transformation where one is given; `TreatAsBlockRefForExplode`; and `IsDynamicBlock`, `DynamicBlockTableRecord` and
`AnonymousBlockTableRecord`, found by the extended data `AcDbDynamicBlockGUID` and `AcDbBlockRepBTag` of the block records
as ezdxf finds them (the dynamic properties are worked out by AutoCAD's engine of dynamic blocks and are not given). A
nested INSERT that `Explode` gives places the block of the drawing it came from, so that it explodes in its turn, with what
the block now holds. `DxfAttributeReference` and `DxfAttributeDefinition` are made of several lines and of one again
(`IsMTextAttribute`, `IsMTextAttributeDefinition`), give and take the MTEXT they are made of (`MTextAttribute`,
`MTextAttributeDefinition`) as AutoCAD and ezdxf's `set_mtext` do, of type 2 and 4, and place it again where their text
stands (`UpdateMTextAttribute`, `UpdateMTextAttributeDefinition`). Held against ezdxf 1.4.5: recover and audit find
nothing new to fix in what is written, and ezdxf reads every column, background, attribute and dynamic block as set.

**NEW.** The details of the editing model's objects and tables, with AutoCAD's names. Every entity has its `Transparency`
(group 440) as the new `DxfTransparency`, AutoCAD's `Transparency` (BYLAYER, BYBLOCK or an alpha, 127 the 50 per cent of
AutoCAD's dialog, written 0x0200007F) from R2004 on; its `Hyperlinks` (extended data of PE_URL, as AutoCAD writes it);
`GetPlane()` and `IsPlanar`, the plane of its extrusion or the one its shapes span to `Tolerance.Default`; and
`IntersectWith(other, DxfIntersect)` and `BoundingBoxIntersectWith`, worked out by the library's shapes, a line run on as
its infinite line and an arc as its circle where the new `DxfIntersect` says, a closed polyline, a SOLID and a 3DFACE by
their edges, a polyline segment by segment, a point where it lies on the other, and a pair the library cannot cross, a text
among them, refused with `NotSupportedException`. Every object has `CreateExtensionDictionary`
and `ReleaseExtensionDictionary`, its `ExtensionDictionary` being the new `DxfDBDictionary` (entries named, added by
`SetAt`, renamed and taken out), `SetBinaryDataForKey` and `GetBinaryDataForKey` (an XRECORD of 127-byte groups 310 in it),
and `XDataTransformBy`, which moves the world points (1011), displacements (1012), directions (1013), distances (1041) and
scale factors (1042) of its extended data as AutoCAD moves them; an entity's `TransformBy` and `GetTransformedCopy` move them with it, in the same writing of its record, a POLYLINE's
vertices' and an INSERT's attributes' too. An entry taken out, or an object erased, takes the dictionaries and records it
owns with it at every depth. A layer has its `Description` and `Transparency` (the
extended data AutoCAD keeps them in), `ViewportVisibilityDefault` and `IsUsed` (the drawing read once for every layer until it is edited: 400 layers of 300 000
lines in 0.08 seconds), and is renamed by its `Name`, every
entity, vertex, attribute, block and extended data on it and `$CLAYER` taken along, and erased by `Erase()` where nothing is
on it, the LAYER_INDEX and LAYER_FILTER objects a drawing may keep left as they are. A linetype has its complex dashes (`ShapeStyleAt`, `ShapeNumberAt`, `ShapeOffsetAt`, `ShapeRotationAt`,
`ShapeScaleAt`, `TextAt`, `ShapeIsUcsOrientedAt` and their setters, AutoCAD's gas line written as AutoCAD writes it), its
dashes' lengths set in a file, and `Erase()`. The text style table takes new styles, and a style's `FileName`,
`BigFontFileName`, `TextSize`, `XScale`, `ObliquingAngle`, `IsVertical` and TrueType `Font` (the new `DxfFontDescriptor`)
are set. A block has its `Comments`, `Units`, `Explodable`, `BlockScaling`, `PreviewIcon` (the bytes of its 310),
`IsDynamicBlock` and `Hyperlinks`; `GetBlockReferences` and `GetAnonymousBlocks` give its INSERTs and the blocks AutoCAD made
of it as objects (all of 2 000 nested blocks in 0.02 seconds), `AssumeOwnershipOf` moves entities into it keeping their
handles, out of the draw order (SORTENTSTABLE) of the block they leave and onto the end of that of the block they enter, its `Name` renames it with every INSERT
of it, and `Erase()` takes it out where nothing inserts it. The drawing reads and sets 20 more of AutoCAD's variables,
those AutoCAD keeps as DICTIONARYVAR objects rather than in the header (`Cannoscale`, `Cetransparency`, `Tablestyle`,
`MLeaderstyle`, `AnnoAllVisible`, `TstackSize` and the rest), adding the dictionary of variables where a drawing has none,
reads its `ThumbnailBitmap`, and has five more variables of the header as properties, `Splinesegs`, `Splinetype`, `Surftype`, `Surfu` and `Surfv`. Every value set was read back by ezdxf 1.4.5, whose recover and audit found nothing to fix.

**NEW.** Fields, the codes of an MTEXT and its words as RTF, 31 members of AutoCAD's `DBObject`, `DBText`, `MText` and
`Dimension`. `DxfField` is AutoCAD's `Field`, a FIELD of the OBJECTS section (`EvaluatorId`, `IsTextField`, `GetFieldCode`,
`GetChildren`, `GetStringValue`), made in code of one field, `new DxfField("%<\AcVar Login>%")`, or of a text with fields in
it, `new DxfField("Drawn %<\AcVar Date>%", true)`, the text field of the code `Drawn %<\_FldIdx 0>%` with the date its child.
Every object has `GetField`, `SetField` and `RemoveField`, of the words (the property TEXT) or of a property named: the field
and its children written under ACAD_FIELD of the extension dictionary, from R2004 on, in the groups the DXF reference gives a FIELD,
marked not evaluated (a layout not yet read by AutoCAD itself: its state 94 of 0, the empty value's flag 93 of 1, the format
group 4 of R2004 left out, and the groups `GetStringValue` reads, are to be checked on a drawing AutoCAD saved), a field one
of whose fields belongs to an object already refused, the class FIELD added to the CLASSES section where it is not listed, the field the property had erased with
its children, ACAD_FIELD taken out once it is empty; the library does not evaluate a field. `DxfDBText.GetTextWithFieldCodes`
and `DxfMText.GetMTextWithFieldCodes` (AutoCAD's `getTextWithFieldCodes` and `getMTextWithFieldCodes`) give the words with
the codes of their fields in their places, `Drawn %<\AcVar Date \f "dd/MM/yyyy">%`, at every depth, and `ConvertFieldToText`
makes the fields words, keeping what AutoCAD last showed. `DxfDimension.FieldFromMText` and `FieldToMText` copy the field of
the dimension's text from and to an MTEXT of its drawing, every group kept and a field two others name copied once, and
`RemoveTextField` takes it out; `GetChildren` gives null in the place of a handle that names no FIELD. `DxfMText`
names AutoCAD's 19 codes of an MTEXT, `ParagraphBreak` `\P`, `UnderlineOn` `\L`, `HeightChange` `\H` and the rest;
`ContentsRTF` gives its words as RTF and `SetContentsRtf` reads RTF into them, paragraphs, columns, groups, fonts with bold
and italic, colours of the index, the lines under and through, tabs and letters carried over, the sizes, slants, tracking,
alignments, paragraph settings and the line over the letters left out: `B-12\P{\C1;\LM16\l}` is
`{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}}{\colortbl ;\red255\green0\blue0;}B-12\par {\cf1 \ul M16\ulnone }\par}`, a font's
file marked by an ignored group and the last paragraph closed, which comes back as `B-12\P\C1;\LM16`, and `abc\P` and
`\Fromans;abc` as themselves; the RTF of 1 280 000 letters is written in 5 ms on .NET 8. Fields set on TEXTs, MTEXTs and dimensions of R2004, R2007 and R2018 were read back by ezdxf 1.4.5, each
owned by what names it, its recover and audit finding nothing to fix; a FIELD read is a `DxfField` where it was an object of no
class of its own, and a file with fields resaves byte for byte.

**NEW.** The cells of a `DxfTable` as AutoCAD's `Table` has them, 36 members it did not have. A cell's value has its
kind, `DataType` (the new `DxfDataType`, AutoCAD's numbers), `UnitType` (`DxfUnitType`) and `Format`, read from its
`301 CELL_VALUE` from R2007 on, with `GetDataType` (`DxfDataTypeParameter`) and `GetDataFormat`;
`SetValue(row, col, 12.5)` writes a number (90 of 2, 140 12.5, its text 12.5), `SetValue(row, col, 42)` a whole number
of 32 bits and a string a text, the unit and the format kept, and `SetFormat`, `SetDataFormat`, `SetDataType` (the text
12 turned into the whole number 12, the text Parts refused as a number, a unit of several flags as
`Angle | AngleNotTransformed` written as it is) and `ResetValue` write the rest, none of them setting a flag of the
cell's overrides; the library does not format a value, and a date or a point is refused, the library knowing no groups
for one, as the DXF reference does not describe a cell's value. Before R2007 a cell holds its text alone: a value is
written as its text, and a kind or a format is refused. The look of a cell is set as its own override with its flag in
its 177 (91 from R2007 on), each group in the order of the DXF reference: `SetAlignment`, `SetTextHeight`,
`SetTextStyle`, `SetContentColor`, `SetBackgroundColor`, `SetBackgroundColorNone`; a cell that gives no flags is given
those of the overrides it holds, so that none is lost, and a colour is one of the index, a true colour refused.
`TextRotation`, `SetTextRotation` (`DxfRotationAngle`) and `SetRotation` read and write a cell's turn, `IsAutoScale`,
`SetAutoScale` and their forms by content a block cell's fitting, `SetScale` its scale, and `SetCellType` makes a block
cell a text cell. `GetOverrides` gives what a cell holds of its own as AutoCAD's `CellProperties` (`DxfCellProperties`),
`SetOverrides` keeps those given and takes the rest away, `RemoveAllOverrides` takes them all, both keeping the cell's
own kind of value (its flag 0x40000), and `TableStyleOverrides` and `ClearTableStyleOverrides(options)` read and take
away the table's own, in one pass over the table's groups: the cells of a table of 16 001 rows of 2 columns, half of
them of three overrides, are cleared in 0.19 s. The cells are walked row by row, `table.Cells[1, 0]` is a `DxfCell` that
reads and sets one, and `Rows` and `Columns` give `DxfRow` and `DxfColumn` with their `Height` and `Width`; the block of
merged cells `GetMergeRange` gives reads its cells too. `HitTest` gives the cell a point hits (`DxfTableHitTestInfo`),
carried along the direction it is seen in onto the table's plane, on a table turned or mirrored as on a flat one, and
`AttachmentPoint` the point a cell's content is placed at: the header cell (1, 1) of a table at (300, -100, 0) of
columns 40 and 30 wide and rows 10, 8 and 8 high is placed at (355, -114, 0), a cell of a merged block by the block's
top left cell, and the top is the cell's top as it is seen where the rows flow up. None of them draws the table's block
anew. Left out, as no file of these versions has a group for them that the DXF reference or ezdxf gives: a cell's state,
tooltip, cell style, own margins, content layout, custom data and merging of all, a column's name, and the forms by row
type. ezdxf 1.4.5, which has no model of ACAD_TABLE, read the edits of texts, values, looks, alignments and cell types on
drawings of R2000, R2007 and R2018 with nothing for its recover and audit to fix, gave the cells' texts (their 302, or 1
before R2007) as they were set, and found in each cell the groups written; every setter given what a cell has writes
nothing.

**NEW.** The blocks, external references, linetypes and tidying of AutoCAD's `Database` that the library did not have, 14
members. `DxfDatabase.Insert(transform, other, preserveSourceDatabase)` inserts another drawing's model space into this
one's, moved, and `Insert(blockName, other, preserveSourceDatabase)` makes it a block drawn about the other's `$INSBASE`, the
records of the other's tables and its named blocks merged by name, this drawing's own kept, and every entity checked before
anything is written: a line from (0, 0, 0) to (10, 0, 0) on steel inserted 100 along x is the line from (100, 0, 0) to (110,
0, 0) and brings steel. `AttachXref` and `OverlayXref` attach a drawing as an external reference, a block flagged 4 or 12
that names its path, in a file of R12 too; `DetachXref` erases one with its INSERTs and the records it brought (those named
`COLUMN|...`); and `DxfBlockTableRecord.XrefStatus`, of the new `DxfXrefStatus`, says where one stands from its flags, its
INSERTs and whether its path names a file. `LoadLineTypeFile` adds linetypes of a .lin, AutoCAD's gas line written `49 -0.2,
74 2, 75 0, 340` Standard`, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`, a shape found by its name in the compiled .shx beside
the .lin and drawn in a style of that file with no name, `*` loading every linetype the drawing can take and a name already
there refused as AutoCAD's eDuplicateRecordName; `LoadMlineStyleFile` adds a style of a .mln to the dictionary of multiline
styles, made where the drawing has none. An MLINESTYLE is read as the new `DxfMlineStyle` (`Elements` of the new
`DxfMlineStyleElement`, `Filled`, `FillColor`, the caps and the angles of its ends), where it was an object of no class of
its own. `CountEmptyObjects` and `EraseEmptyObjects` count and erase the curves of no length and the texts of no words by
AutoCAD's `EraseFlags` (the new `DxfEraseFlags`), a curve of no length being one whose points are the same to the rounding of
their coordinates, so that a line from (1000, 0, 0) to (1000.0008, 0, 0), 8 long in a block drawn at a scale of 10 000, is
kept, and a LINE of thickness 100 is never empty; `CountHardReferences` counts the hard pointers to objects, the handles of
extended data (a dimension's overrides, by handle, or by name in a drawing of R12) and the arrows a dimension style names in
any version, the layers a viewport freezes and the names a DXF writes in their place (a layer, a linetype, a style, a block,
the header's `$CLAYER` and its kin), and `Purge` gives back those of the objects nothing refers to that AutoCAD does not keep
itself (the Standard styles, the styles of shape files, the materials Global, ByLayer and ByBlock, the plot style Normal,
every visual style, the DICTIONARYVAR variables), never a layer, linetype or block any pointer names, so that each object
given back of a kind the library erases is erased without a refusal; an external reference an INSERT places stays one when
`Insert` or `WblockCloneObjects` copies it, and a drawing with a block that inserts itself, at any depth, is refused before
anything is written, where it was copied cyclic or left the target half-written; `UpdateExt` works `$EXTMIN`, `$EXTMAX`,
`$PEXTMIN` and `$PEXTMAX` out anew from what the spaces hold, shrinking them too. `DxfDBObject.HandOverTo` puts an entity
made in code in another's place, under its handle, owner and reactors, with its extended data and extension dictionary where
asked; `DxfBlockReference.ConvertToStaticBlock` makes the INSERT of a dynamic block place a named or anonymous copy of what
it draws, no longer tied to the dynamic block. ezdxf 1.4.5 read back every drawing a test wrote of its drawings of R12,
R2000 and R2018, its recover and audit finding nothing to fix, and a drawing in which nothing is found to count, purge or
erase resaves byte for byte.

**FIXED.** The walks of the DXF model down blocks nested one inside the next overflowed the thread's stack, which ends the
process and cannot be caught. On .NET 10, 3 000 blocks each holding an INSERT of the one before overflowed it (on .NET
Framework 4.8, a thread of 1 MB from 4 500) when they were filled from the bottom up by `AppendEntity`, when their drawing
was inserted into another by either `DxfDatabase.Insert`, and when the INSERT of the last was measured by
`GeometryExtentsBestFit` under a transformation; 6 000 of them overflowed it when the drawing was saved, as the size of what
that INSERT places was worked out for the extents of the header; and 30 000 when an INSERT was moved into a block by
`AssumeOwnershipOf`, given another block by `BlockTableRecord`, or appended. Each of these walks keeps the blocks it is in
on a stack of its own now, visiting them in the same order, so that 30 000 nested blocks are walked in 40 ms and every
answer is the one it was: 253 drawings of the corpus and the tests resave byte for byte, and the 1 526 drawings `Insert`,
`WblockCloneObjects`, `ExplodeToOwnerSpace` and renaming write of them are the same, byte for byte, as before. A block that
inserts itself, at once or through others, ends each walk as it did: an INSERT that would make one is refused, a box of one
is refused, and the size of one counts nought; exploding and the reader's placing stop at 64 deep, as they did.

**FIXED.** `DxfDatabase.Insert` of a drawing of 3 000 blocks nested one inside the next took 48 s on .NET Framework 4.8 and
19 s on .NET 10, timed on a thread of a large stack, the main thread's overflowing: each INSERT copied was walked down every
block below it twice to find whether it made a round, the check before anything is written walked them again for each block
above, and each block was found by its name by reading the names of the whole block table. Each block of the other drawing
is checked once now, a block is found by its name at once, and a block a walk finds to insert none merged, with every
INSERT under it placing a block of the drawing that is not erased, is not walked again while the merge lasts: 1.0 s on
.NET Framework and 0.9 s on .NET 10, the drawing written the same, byte for byte, and `WblockCloneObjects` of its INSERT
3.8 s and 1.6 s before, 0.59 s and 0.31 s now. Inserting 20 small drawings into one of 2 000 blocks and 6 000 INSERTs
takes no longer than it did (1 061 ms before and 926 ms now on .NET Framework). A block of the other drawing that would
come to insert itself through an INSERT of this drawing that names a block it has not is refused as before; 6 000 drawings of
a few blocks, with rounds and INSERTs of no block among them, inserted four ways, then edited, renamed and looked up, give
the same as before, each line.

**FIXED.** An INSERT made in code of a block erased since was appended, or given that block by `BlockTableRecord`, as if
the block were there: an INSERT of M appended to P after M was erased placed a block the drawing no longer had, and kept
M's record. Such an INSERT is refused now with an `ArgumentException`, as one of a block the drawing has not.

**FIXED.** A block renamed in a drawing of R12 left the arrows the overrides of a dimension name by name under the old
name: DIMBLK, DIMBLK1 and DIMBLK2, each a `1070` of 5, 6 or 7 and then a `1000` of the block's name in ACAD's DSTYLE, so
that the dimension lost its arrows and the block could be erased from under it. They are renamed with the block now, the
dimension's record and the block's alone written anew: the arrow GH_DOT renamed GH_DOT_R12 is `1070 5`, `1000 GH_DOT_R12`.
A drawing of R2000 or later names them by handle (`1005`), which renaming leaves as it is; R12 has no DIMLDRBLK.

**CHANGED.** `DxfDatabase.Insert` and `WblockCloneObjects` take time linear in the blocks of the drawings. Each entity copied
found its layer, linetype, text style and dimension style, and the application of its extended data, by reading every
record of the tables of both drawings, a BLOCK_RECORD for each block among them; each entity copied into a block added in
code sought that block's ENDBLK through the blocks added before it; and each dimension copied read the objects of the other
drawing again for each pointer of its overrides, and sought a free name `*D1`, `*D2` and on from `*D1`. For the length of
each call the drawing copied into keeps the records of its tables by name now, a record added at the end of its table taken
in with them and any other write to the tables reading that table again, and its objects by handle until a table or an
object is written; it keeps what it reads of the drawing copied from too, which is only read; the ENDBLK is sought from both
ends of the blocks added; and a free name from the last one given. A drawing of
10 000 blocks nested one inside the next took 11.5 s to insert on .NET Framework 4.8 and 6.3 s on .NET 10, and takes 1.1 s
and 1.0 s; `WblockCloneObjects` of its INSERT 6.5 s and 3.7 s before, 0.8 s now; 3 000 dimensions, each drawn by a block of
its own and overriding DIMBLK with one arrow block, copied in 8.6 s and 5.4 s, and inserted in 9.4 s and 6.5 s, and take
0.3 s and 0.4 s, and 0.5 s and 0.7 s.

**CHANGED.** `AppendEntity` of an INSERT to a block no INSERT of the drawing names no longer walks down the blocks the
inserted block holds to find whether it would make a round: a block nothing inserts cannot come to insert itself. The names
the INSERTs of the drawing give are read once a walk for a round has gone down more than 64 blocks, and kept from then on,
each INSERT written adding its own, so that each INSERT of a nest built from the bottom up is appended at once, and the
first INSERT appended to a drawing as opened costs what it did: 3 000 blocks, each added and given an INSERT of the one before, took 6.1 s on
.NET Framework 4.8 and 2.7 s on .NET 10, and take 0.14 s and 0.16 s. A round is refused as before, with the same message.

**CHANGED.** `DxfBlockTableRecord.Erase` no longer reads every record of the drawing for an INSERT, a dimension or a table
drawn by the block, an arrow named by it and a pointer to it, three times over: what the records use is read once, when a
block is first erased, and kept, each record put into the drawing counted on and each taken out counted off, so that the
records are read again only for a block one of them may use; and the block table keeps its blocks by name through an
erasing, where none other has the name. 3 000 blocks erased one by one, each looked up by name, took 25.9 s on .NET
Framework 4.8 and 13.8 s on .NET 10, and take 0.15 s and 0.20 s; a block in use is kept as before, with the same message.

**FIXED.** Several threads copying from one drawing at once, each into a drawing of its own by `Insert`, `Insert(name)`
or `WblockCloneObjects`, now and then threw an `ArgumentException` ("An item with the same key has already been added"):
the lists of the records of the other drawing's tables were made as they were first read, by each thread at once. They
are made under a lock now, and nothing else of the drawing copied from is written: eight threads copying one drawing of
60 layers, 40 nested blocks and 200 entities, 60 times over, each give the drawing it gives alone; 6 to 10 of 600 rounds
of four threads threw before, none now.

**CHANGED.** `DxfPolyline.GetPoint3dAt` reads the polyline's normal and elevation once for the record as it stands, not
again for each vertex: every vertex of a polyline of 10 000 read in the world took 36 s on .NET Framework 4.8 and 12 s on
.NET 10, and takes 13 ms and 16 ms, each point the same to the bit.

The four changes above give what they gave before, byte for byte: 253 drawings of the corpus and the tests resave byte
for byte, and the 1 907 drawings `Insert` (into a drawing of their own version, of R12 and of R2018),
`WblockCloneObjects`, `ExplodeToOwnerSpace` and renaming write of them, the drawings the walks of 3 000 nested blocks
write, and every point `GetPoint3dAt` gives of their polylines are the same; 6 000 drawings of rounds, merged, renamed
and erased, 6 000 cases of appending, moving and giving INSERTs another block, 60 000 merges, 3 000 runs of blocks
added, filled, erased, renamed and merged, and 16 000 steps of blocks added, renamed, erased and looked up, each step
with its answer, refusals among them, give the same, each line; ezdxf 1.4.5 finds nothing in any drawing written whose
source it reads with nothing found.

**NEW.** Annotative scaling as a DXF holds it, 5 members of AutoCAD's `Database` and `DBObject`. `DxfDatabase.ObjectContextManager`
gives the drawing's list of scales as AutoCAD's `ObjectContextManager` does, `GetContextCollection("ACDB_ANNOTATIONSCALES")`,
the new `DxfObjectContextCollection` of `DxfAnnotationScale`s (AutoCAD's `ObjectContextCollection` and `AnnotationScale`, of
the new `DxfObjectContext`): the SCALE objects of ACAD_SCALELIST with their `Name`, `PaperUnits`, `DrawingUnits` and
`Scale`, so that 1:50 is 1 to 50 and 0.02, `GetContext`, `HasContext`, `CurrentContext` (the one CANNOSCALE names),
`AddContext`, which writes a SCALE under the first entry name A0, A1 and on the list has not, a 1:1 the unit scale where the
list has none, and makes the list in a drawing of R2007 or later that has none, and `RemoveContext`, refused for the unit
scale, the current scale and a scale an object points at; a scale renamed is renamed in CANNOSCALE too, refused before
anything is written where the drawing cannot write that variable. Every object has `Annotative` (the new
`DxfAnnotativeStates`), read from the extended data AcadAnnotative of a TEXT, an MTEXT, an attribute, a dimension, a leader,
a multileader (or its own 293), a tolerance, a hatch, a style or a block, an INSERT reading its block's, a multileader style
its 296, and `NotApplicable` for a line; set true, a text or dimension style is given that data, and an entity is refused,
its context record of the OBJECTS section waiting for a check on a drawing AutoCAD saved; set false, any annotative entity
loses the data and every context. `HasContext` reads the contexts of an object of any kind by the SCALE each points at, and
`RemoveContext` erases one, the default and the last kept. `PaperOrientation` (the new `DxfPaperOrientationStates`) reads
the extended data AcadAnnoPO, a reading of AutoCAD's not yet checked either. The named object dictionary is found without
copying the OBJECTS section: reading it, the dictionary of groups and CANNOSCALE after each of 2 000 edits of a drawing given
2 000 objects takes 141 ms on .NET 8 where it took 633 ms. Of the 1 705 drawings of the corpus, 1 697 open and resave byte for byte after
`Annotative`, `PaperOrientation` and `HasContext` of every scale are read on every entity of model space, paper space and the
blocks and on every block, text style, dimension style and layer, the 8 others failing to open as before.

**NEW.** Layouts made in code, and the sun and the plotting of a viewport, as AutoCAD's `Layout`, `Viewport` and
`Database` have them, six members the library did not have. `new DxfLayout { LayoutName = "Plan 1-50" }` is added to a
drawing of R2000 or later by `AddToLayoutDictionary(db, null)`: a LAYOUT in the layout dictionary on the tab after the
last, with the plot settings of a sheet of A3 on its side and no plotter (`none_device`, `ISO_A3_(420.00_x_297.00_MM)`,
margins 7.5 and 20, 1:1) and its limits (-7.5, -20) to (412.5, 277), and the block `*Paper_Space1`, the first of
`*Paper_Space0` and on the drawing has not, whose BLOCK_RECORD names it; a block of that name no layout has is taken
where it is given. `Initialize()` gives a layout of paper space the viewport 1 that shows its sheet, about the middle of
its limits and 1.1 times their size, 462 by 326.7 at 1:1, as its last active viewport, and gives a layout that has its
viewport 1 that one; a layout that has viewports and none of id 1 is given it ahead of them, each stacked on moved up one,
as AutoCAD and BricsCAD take the first VIEWPORT for the sheet. Adding 200 layouts with their viewports to one drawing takes
0.14 to 0.25 s once the code is compiled, 0.35 to 0.56 s as the first work of a process (AutoCAD allows 255). `DxfDatabase.GetViewports(true)` gives the viewports of the layout shown where the drawing shows
paper space, and none where it shows model space, whose tiled viewports are records of the VPORT table.
`DxfViewport.PlotWireframe` and `PlotAsRaster` say how a viewport is plotted, by its shading of the plot (170), the
visual style it names (333) or is shown in (348) and its render mode (281); one shaded hidden is neither.
`DxfViewport.SetSun` lights a viewport of R2007 or later by a sun, the new `DxfSun`, AutoCAD's `Sun`: a SUN owned by
the viewport (361), the one it had erased where the viewport owned it, the class SUN listed where the drawing lists
none, read back by `Sun`; its `IsOn`, `SunColor`, `Intensity`, `DateTime` (21
June 2026 at 15:00 written as the Julian day 2461213 and 54 000 seconds past midnight), `IsDaylightSavingsOn`,
`ShadowsOn`, `ShadowType` (the new `DxfShadowType`), `ShadowMapSize` and `ShadowMapSoftness` read and set, and a SUN of
a file reads as one. Left out: a viewport's `ToneOperatorParameters`, whose place in a file neither the DXF reference nor
ezdxf gives, and `Transparent`, not among the flags of its group 90 the DXF reference lists. ezdxf 1.4.5 read the new
layout with its viewports 1 and 2 and the sun with every value set, on drawings of R2000 and R2018, its recover and
audit finding nothing the drawings did not have before.

**FIXED.** An entity appended to the block of a layout of paper space other than the one last shown, `*Paper_Space0` and
on, or moved into one by `AssumeOwnershipOf`, is written in paper space, its group 67 of 1, as AutoCAD and ezdxf write
the entities of those blocks; it was written with none, as an entity of a block is, and one moved there lost its 67. An
entity moved into paper space or such a block by `AssumeOwnershipOf` has each record that follows it in paper space too,
the VERTEXes and SEQEND of a POLYLINE, the ATTRIBs and SEQEND of an INSERT, as an appended one has them; only its own
record was. An entity appended to paper space, model space or any other block is written as before, byte for byte.

**NEW.** The structure of a `DxfTable` as AutoCAD's `Table` changes it, 30 members it did not have. `InsertRows`,
`InsertColumns`, `InsertRowsAndInherit`, `InsertColumnsAndInherit`, `DeleteRows`, `DeleteColumns` and `SetSize` put in
and take out rows and columns, their counts, heights and widths written with them and every cell written anew in one
writing of the record, a block of merged cells grown or shrunk with them: the table of rows 10, 8 and 8 given two rows 6
high before its row 2 has rows 10, 8, 6, 6 and 8, and 16 000 rows go in or out in about 0.3 s; `CanInsertRow`,
`CanInsertColumn`, `CanDeleteRows` and `CanDeleteColumns` say whether they can. `MergeCells` and `UnmergeCells` write a
cell's merging (173, and the block's width and height, 175 and 176, on its top left cell). The lines of the grid are the
cells' own overrides of their edges (`GetGridColor`, `GetGridLineWeight`, `GetGridVisibility`, `GetGridProperty`,
`GridColor`, `GridLineWeight`, `GridVisibility` and their setters, with the new `DxfGridLineType`, `DxfCellEdgeMasks`,
`DxfGridProperties` and `DxfGridPropertyParameter`), and the table's own lines by kind of row with their flags in 94 to
96. `SetFormula` adds a FIELD of AutoCAD's expressions (AcExpr) the cell points at, `GetFormula` and `HasFormula` read
one; the formula is not worked out. `CopyFrom` copies cells of a table of the same drawing (`DxfTableCopyOptions`),
`Fill` fills cells from others, copied or as a series of numbers (`DxfTableFillOptions`), each number rounded to the
places of the source and the step, 0.1 and 0.2 filled to 0.3 and 1, 2 and 4 to 5.5, and with `Reverse` the other way, 10
and 20 filled down to 10, 0, -10; a block of merged cells that loses its top row or first column keeps its content in
the first cell of it left; a formula, from R2004 on, is a FIELD written as `DxfField` writes one, the class FIELD
listed, and a field a cell lets go of is erased where the table owns it and nothing else of the file holds it, a copy of
the table among them; and `SetFromStyle` takes the table's overrides away. The block AutoCAD drew the table with is not
drawn anew by any of them. Left out, as no file of these versions has a group for them that the DXF reference or ezdxf
gives: several contents in a cell, and a grid line's style, linetype and double-line spacing; dates in a series. ezdxf
1.4.5 read every change on drawings of R2000, R2007 and R2018 with nothing for its recover and audit to fix, and gave
the cells' texts as the library reads them.

**NEW.** A drawing's place on the earth, its layer states and its layer filters, as AutoCAD's `Database` gives them, three
members the library did not have. `DxfDatabase.GeoDataObject` is the GEODATA model space's extension dictionary names
ACAD_GEOGRAPHICDATA, read as the new `DxfGeoData`, AutoCAD's `GeoLocationData`, where it was an object of no class of its
own: `new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Local, ReferencePoint = new GeoPoint3(500000, 4100000, 0), HorizontalUnits = DxfUnits.Millimeters, HorizontalUnitsScale = 0.001 }.PostToDb()`
puts a drawing of R2010 or later in millimetres on a local grid at 500 000 east and 4 100 000 north, the extension
dictionary made where model space has none, the GEODATA it had erased, as ezdxf replaces it, and the class GEODATA listed
in the CLASSES section where it lists none; a file of R2010 or later written with no CLASSES section is given no class for
it. Its `DesignPoint`, `ReferencePoint`, `HorizontalUnits` and `VerticalUnits` with their scales, `UpDirection`,
`NorthDirectionVector`, `ScaleEstimationMethod` (the new `DxfScaleEstimationMethod`) and `ScaleFactor`,
`TypeOfCoordinates` (the new `DxfTypeOfCoordinates`), `DoSeaLevelCorrection`, `SeaLevelElevation`,
`CoordinateProjectionRadius`, `CoordinateSystem` (its XML in pieces of 255 characters, a line feed written `^J`, as ezdxf
writes it, no piece ending between the two halves of a letter of two units of UTF-16), `GeoRSSTag` and the three tags of
observation are read and set, and the points of its mesh (the new `DxfMeshPointMap`) by `GetMeshPointMaps`,
`SetMeshPointMaps`, `AddMeshPointMap` and `ResetMeshPointMaps`, its faces kept as they are. One of version 1 (R2009) has
the points of its mesh read from its groups 12 and 13, as a comment of ezdxf maps them, though ezdxf 1.4.5 itself reads no
points from it; it is not given a north direction or points of its mesh. `DxfDatabase.LayerStateManager`, the new
`DxfLayerStateManager`, lists the layer states of the dictionary ACAD_LAYERSTATES of the layer table's extension
dictionary (`GetLayerStateNames`, `HasLayerState`, `LayerStatesDictionary`), and `DxfDatabase.LayerFilters`, the new
`DxfLayerFilterTree` and `DxfLayerFilter`, gives All with the names of the entries of ACAD_LAYERFILTERS nested in it.
That is the dictionary of the filters of AutoCAD 2004 and earlier: the property and group filters of AutoCAD 2005 and
later, kept in AcLyDictionary, are not read, so a drawing saved by AutoCAD 2005 or later most likely lists no filter but
All. Left out: saving, restoring, renaming and deleting a layer state, what a state or a filter holds, groups of layers
and the current filter, whose records' groups neither the DXF reference nor ezdxf gives and no drawing at hand shows; a
point taken to latitude and longitude, which needs the coordinate system's projection; and the angle `NorthDirection`,
from which axis and which way it is measured not confirmed. ezdxf 1.4.5 read every GEODATA a test wrote, set, changed,
replaced and erased, on drawings of R2010 and R2018, with the values set and nothing for its recover and audit to fix; 253
drawings read and saved again, 244 of the reader's corpus and the 9 the tests hold, give the bytes they gave before.

**NEW.** The data links of a `DxfTable` as AutoCAD's `Table` keeps them, read and taken away: `GetDataLink` (of a cell,
of the table, of a range), `GetDataLinkRange`, `IsLinked` and `RemoveDataLink` (of the table, of a cell), with the new
`DxfDataLink`, a DATALINK of the drawing's ACAD_DATALINK (its data adapter, connection string, description, tooltip,
options, update options, `IsValid`, `GetUpdateStatus`, and `GetTargets`, the tables it fills; its `Name` set renames its
entry), `DxfDataLinkOption` and `DxfUpdateDirection`. A range's top left cell names its link after its 92, made odd: the
DATALINK's handle (340), the range's rows (93) and columns (94) and a 96; the table linked over (1, 0) to (2, 1) gives
that range from its cell (2, 1). Only a text cell names a link: a block cell keeps its block whatever its 92. Rows and
columns put in and taken out, `SetSize`, `CopyFrom`, which now takes `SkipDataLink`, and `Fill` keep the links with
their cells as they keep merges. A DATALINK the cells let go of, by `RemoveDataLink` or by its rows taken out, is erased
with what it owns only where it is anonymous and nothing else of the file points at it, a copy of the table among them;
a named one stays, as AutoCAD's Data Link Manager keeps it. Writing a link, `SetDataLink` and a `DxfDataLink` made or
changed in code, is held for the checks in AutoCAD: the DXF reference describes no DATALINK, and the groups read are
those LibreDWG gives, which have no 70 and put the link's custom data between `305 CUSTOMDATA`, `1 DATAMAP_BEGIN` and
`309 DATAMAP_END`; the run 340, 93, 94, 96 is the one LibreDWG gives a cell of a TABLECONTENT, and its place after the
92 of an ACAD_TABLE's cell is the library's guess. The library reads no spreadsheet. ezdxf 1.4.5 read every change on
drawings of R2007 and R2018 with nothing for its recover and audit to fix, the cells' texts as before, and no pointer of
them names a handle the file has not; a table of 16 001 rows has its rows put in, taken out, merged, copied and filled
in the time it took before. The breaking of a table into parts is left out, its groups being in no reference.

**NEW.** The dimension variables of AutoCAD's `Database`, `Dimension` and `Leader` that the library did not have, 84 of
them. `DxfDatabase` reads and sets the 77 `$DIM` variables of the header by their AutoCAD names (`Dimasz` is `$DIMASZ`,
`Dimfxlen` `$DIMFXL`, `DimAssoc` `$DIMASSOC`), each read as AutoCAD's metric drawing has it where the header gives none
(arrowheads and texts 2.5, two decimal places, a comma), the arrowheads, linetypes and text style the records the header
names; a value AutoCAD refuses is refused, a `Dimdec` of 9, and a variable is refused in a file older than the version that
brought it, reading there as a dimension style of that file reads it (DIMDEC 4 in R12), so that the header's data and a
style's agree. `GetDimstyleData` gives them as a `DxfDimStyleTableRecord` in no table and `SetDimstyleData` takes a style's,
writing only those that differ and passing over those the header of the file's version cannot hold. `DxfLeader.GetDimstyleData`
gives the variables a leader is drawn with, its overrides over its style's, and `SetDimstyleData` writes those that differ as
overrides of its extended data, as AutoCAD writes them. The `SetDimstyleData` of a leader and of a dimension refuse a whole
number out of AutoCAD's range as the header does, and, with the header's, leave the text style as it is where the record
names none, where a dimension's wrote the handle 0 before. `Dimtxtdirection`, DIMTXTDIRECTION, is read and set on a dimension
style (group 294, after DIMFXLON's 290) and on a dimension (the override 1070 294), from R2010: a style made in code and added
to a drawing of R2010 or later is now written with `294 0`, and a dimension or a leader copied into a drawing of R2007 or
earlier leaves its override of DIMTXTDIRECTION out, as its other overrides of variables that drawing has not. `DxfDimension.FormatMeasurement` writes a measurement as AutoCAD writes a dimension's: a length scaled by DIMLFAC,
rounded by DIMRND, in scientific, decimal, engineering, architectural, fractional or Windows desktop units, its zeros left
out as DIMZIN says, with DIMDSEP, DIMPOST and the alternate units in brackets; an angle in decimal degrees, degrees, minutes
and seconds, grads or radians; a `[]` in the text stands for the alternate measurement. 40 lengths and 30 angles are written as ezdxf 1.4.5 writes them, and the examples of AutoCAD's
reference as it gives them, 15.5 as `1'-3 1/2"` in architectural units and 10 as `10"[254.00mm]` with millimetres as
alternate units. Every variable set on ezdxf's drawings of R12, R2000, R2007 and R2018, 41, 66, 76 and 77 of them, and the
overrides of leaders, were read back by ezdxf 1.4.5, whose recover and audit found nothing to fix, and the files resave byte
for byte where nothing is set.

**NEW.** The members of AutoCAD's `DBObject`, `Leader` and `MLeader` for styles, planes and one leader line's look that the
library did not have, 12 of them. `SetFromStyle()` takes away what an object holds of its own over its style and answers
whether it has a style its drawing has: a dimension and a leader lose their overrides of the dimension style (the list DSTYLE
of their extended data of ACAD), so that a dimension of arrows 4.5 of its own over a style of 3.5 reads 3.5 again; one of a
style its drawing has not, or in no drawing, answers false and changes nothing. A multileader takes the values of its
multileader style through its properties, its words 2.5 high of its own made the 4 of the style Standard, and its overrides
(90) are set to nought, but for those it cannot take away: a linetype or a text style the style names none of, or one its
drawing has not, a content it does not hold, an arrow or a block a copy in no drawing cannot be given, the attachments of the
top and bottom in a file older than R2010, and the bits of no property the library writes (18 and 26); what it cannot take is
judged before anything is written; one of no context data answers false. Its sizes, unscaled in the style, are scaled once:
Standard scaled 2 gives words 8 high, a gap of 4, arrows drawn 8 and doglegs 16, the multileader's own dogleg (41) scaled with
the leaders' and its own arrow size (42) the style's 4, unscaled. Every other object answers false and changes nothing, a
table among them for now. `DxfMLeaderStyle` is AutoCAD's `MLeaderStyle`, an MLEADERSTYLE of ACAD_MLEADERSTYLE read as the file
holds it (`TextHeight`, `ArrowSize`, `LandingGap`, `DoglegLength`, `LeaderLineType`, `LeaderLineLinetype`, `TextStyle`,
`Block` and the rest of its look, 31 members), and the entries of that dictionary are such styles now, where they were objects
of no class of their own. `GetOverridedMLeaderStyle(style)`, AutoCAD's `getOverridedMLeaderStyle` named as the library names
its members, gives a style made by `new DxfMLeaderStyle()` the values a multileader is drawn with, its style's with its own in
place of those it overrides, as ezdxf's `MLeaderStyleOverride` reads them, attribute for attribute the same as ezdxf's on six
multileaders: the height of the words and the landing gap from the context data and the dogleg from the multileader's own 41,
all three scaled, and the arrow size from its own 42, unscaled. One leader line of a multileader is given its own type,
colour, lineweight and arrow size by `SetLeaderLineType`, `SetLeaderLineColor`, `SetLeaderLineWeight` and
`SetArrowSize(leaderLineIndex, ...)`, from R2010, and read by their `Get`, the multileader's where the line has none: its
groups 170, 92, 171 and 40 after its index (91), with the flags 93 of those it overrides (1, 2, 8 and 16), in the order of the
DXF reference; AutoCAD's own files are yet to confirm the order. A line of no flags whose colour is not BYBLOCK, as ezdxf
writes one, reads that colour, and keeps it when it is given another look of its own. `Explode` draws each line with the look
of its own, BYBLOCK among them: a line splined of its own is a SPLINE among straight lines, one invisible of its own gives
nothing, one of arrows 2 under arrows 4 starts 2 from its tip at scale 1 and 4 from it at scale 2, a size of its own being
unscaled, as the multileader's own; a multileader whose lines have no look of their own explodes as before, part for part.
`SetPlane(plane)` puts a leader in a plane, its normal the plane's and each vertex moved straight onto it, the direction of
its hook laid in it: through (0, 0, 5), (10, 10, 5) and (20, 10, 8), put in the floor, it runs through (0, 0, 0), (10, 10, 0)
and (20, 10, 0). On a multileader it makes the plane of the context data the one given (110, 111 and 112), every point moved
onto it and every direction laid in it, the matrix of its block turned with it, the words of a mirrored multileader still read
from behind its plane. A leader or a multileader put in its own plane changes nothing, a hook of length 2, a normal a hair off
unit length and the leaning axes a shear leaves among them, 221 files of leaders and multileaders byte for byte; one set from
a style it has the values of changes nothing either. Every file written was read by ezdxf 1.4.5, whose recover and audit found
nothing to fix that they did not find in the file it was made from.

**CHANGED.** A dimension reads the variables of its style many times faster, each to the same value: it reads its overrides
and its style once and keeps them until its drawing next changes, and a dimension style reads its record so, where each
variable read them anew. On the rotated dimension of ezdxf's drawing of R2018, which overrides variables of every kind,
`FormatMeasurement` takes 7 microseconds where it took 0.23 ms, `GetDimstyleData` 0.05 ms where it took 1.0 and
`SetDimstyleData` given its own 0.08 ms where it took 2.1, and the `GetDimstyleData` of 1 000 such dimensions takes 17 ms
where it took 1.1 s (.NET 10; on .NET Framework 4.8 3 microseconds, 0.02, 0.03 and 19 ms where they took 0.53, 2.2, 4.5 and
2 300 ms); a leader's `GetDimstyleData` takes 0.04 ms where it took 0.2, and a getter of a style, as `Dimasz`, 0.1
microseconds where it took 3. `Explode` reads no variable and takes the time it took, 20 ms for 1 000 dimensions. Every
getter, `GetDimstyleData`, `SetDimstyleData`, `FormatMeasurement` and `Explode` of the dimensions and leaders of ezdxf's
drawings gives the same text to the byte as before, after each kind of edit: an override set, the style changed, renamed,
added and erased, the dimension moved, turned, copied, erased and given another style.

**NEW.** The rest of the variables of the header AutoCAD's `Database` has as members, 141 of them, read and set by their
AutoCAD names as `Ltscale` is: the settings of drafting (`Attmode`, `Celtscale`, `Chamfera` to `Chamferd`, `Filletrad`,
`Elevation`, `Thickness`, `Plinewid`, `Tracewid`, `Cmljust`, `Cmlscale`, `Surftab1`, `Surftab2`, `Useri1` to `Useri5`,
`Userr1` to `Userr5`, `Measurement`, `EndCaps`, `JoinStyle` and the modes, `Limcheck`, `Qtextmode`, `Regenmode`, `TileMode`
and `Psltscale` among them), the limits and extents of paper space (`Plimmin`, `Plimmax`, `Pextmin`, `Pextmax`, `Pinsbase`,
`Pelevation`), the display and modelling of 3D (`Shadedge`, `HideText`, `ObscuredColor`, `IntersectColor`, `LensLength`,
`StepSize`, `LoftAng1`, `LoftParam`, `Latitude`, `Longitude`, `Cshadow` and the rest), the UCS (`Ucsorg`, `Ucsxdir` and
`Ucsydir`, written together by `db.Ucs = new GeoCoordinateSystem3(...)`; `Ucsname` and `UcsBase` by the name of a record of
the UCS table; the origins of the six orthographic systems by `WorldUcsBaseOrigin` and `SetWorldUcsBaseOrigin`; and the
same of paper space), the times (`Tdcreate`, `Tducreate`, `Tdupdate` and `Tduupdate` as a `DateTime` read from the Julian
day the file writes, so that 2451545.75 is 1 January 2000 at 18:00; `Tdindwg` and `Tdusrtimer` as a `TimeSpan`;
`ResetTimes`; and `TimeZone`, AutoCAD's 76 zones in the new `DxfTimeZone`, with `TimeZoneOffset`, `TimeZoneDescription` and
`SetTimeZoneAsUtcOffset`, -7 giving `Mountain`), the variables that name an object (`Cmaterial`, `dragvs`, `Interfereobjvs`,
`Interferevpvs` and `CmlstyleID` give the MATERIAL, VISUALSTYLE or MLINESTYLE of the drawing's dictionary, and take one of
it), and what the drawing says of itself (`FingerprintGuid`, `VersionGuid`, `HyperlinkBase`, `ProjectName`, `StyleSheet`,
`Menu`, `PlotStyleMode`, `MaintenanceReleaseVersion`, `ApproxNumObjects`, the objects `GetObject` finds). A text is read as
the header writes it, its spaces kept, as ezdxf reads it, and a GUID set in any form is written in braces and capitals, as
AutoCAD writes it. `GetVisualStyleList`, `GetNearestLineWeight` (24 gives 0.25 mm) and
`IsValidLineWeight` answer as AutoCAD's do; `Filename` is the full path the drawing was opened from or last saved to, and
`OriginalFileName` the one it was opened from. The new enums `DxfEndCap`, `DxfJoinStyle` and `DxfMeasurementValue` are
AutoCAD's. Where the header gives none, a variable reads as a new drawing of ezdxf 1.4.5 has it, but for
`MaintenanceReleaseVersion` (nought), `Pextmin` and `Pextmax` (the origin, as `Extmin`), the variables that name an object
(null), and `PsolHeight` and `PsolWidth` (80 and 5, as AutoCAD's metric drawing has them, where ezdxf writes metres, 0.08
and 0.005); set to the value it has, it writes nothing; a value AutoCAD refuses is refused (a `Shadedif` or `HaloGap` of
101, a `LoftAng1` of two pi, a `LoftMag1` above 10, a `Latitude` of 90.5), and a variable R2000, R2004
or R2007 brought is refused in a file of an older version. Every value set on ezdxf's drawings of R12, R2000, R2007 and
R2018, 52, 76, 116 and 116 variables, was read back by ezdxf 1.4.5, whose recover and audit found nothing to fix, and the
files resave byte for byte where nothing is set.

**NEW.** The objects of a drawing with AutoCAD's names. `DxfXrecord`, AutoCAD's `Xrecord`: its `Data` the groups after its
cloning flag, the groups 102 of a list among them, set, `Append`ed and enumerated, made in code and added by
`DxfDBDictionary.SetAt`. `DxfDBDictionary.SetAt` takes a `DxfXrecord`, a `DxfGroup` or a `DxfDBDictionary` made in code (a
group under `*` is anonymous, named `*A1`, `*A2` and on); `TreatElementsAsHard` (280, its entries named by 360 or 350) and
`CopyTo` are new. The drawing's dictionaries are objects: `NamedObjectsDictionary`, `GroupDictionary`, `LayoutDictionary`,
`MLeaderStyleDictionary`, `TableStyleDictionary` and the nine others of AutoCAD's `Database`, null where a drawing has none.
`DxfGroup`, AutoCAD's `Group`: its `Name` the key of its entry, `Description`, `Selectable`, `IsAnonymous`, `SetAnonymous`,
`NumEntities`, `GetAllEntityIds` (the entities), `Append`, `Prepend`, `InsertAt`, `Remove`, `RemoveAt`, `Replace`, `Reverse`,
`Transfer`, `Clear`, `Has`, `GetIndex`, and `SetColor`, `SetColorIndex`, `SetLayer`, `SetLinetype`, `SetLinetypeScale` and
`SetVisibility` on every member; a member put in names the group among its reactors and loses it when taken out, and a
member erased leaves the group at once. `DxfLayout`, AutoCAD's `Layout`, read with its plot settings (`DxfPlotSettings`,
AutoCAD's `PlotSettings`, also for a PLOTSETTINGS): `LayoutName` (renamed with its entry), `TabOrder`, `TabSelected`,
`Limits` (the new `DxfExtents2d`), `Extents`, `BlockTableRecord`, `GetViewports`, the sheet's margins, size, scale (the new
`DxfCustomScale`), rotation, plot type and flags; `DxfDatabase.Layouts` gives them in the order of their tabs. `DxfViewport`,
AutoCAD's `Viewport`, read and set: `CenterPoint`, `Width`, `Height`, `Number`, `On`, `Locked`, `ViewCenter`, `ViewHeight`,
`ViewTarget`, `ViewDirection`, `TwistAngle`, `CustomScale` and `StandardScale` (a viewport 150 high showing 7 500 of model
space is at 1:50), the snap, grid and clipping, the user coordinate system, the plot style, shading and lighting, and
`FreezeLayersInViewport`, `ThawLayersInViewport`, `IsLayerFrozenInViewport` (331); a viewport of a file before R2000 is read and set in
its extended data MVIEW; `TransformBy` moves and scales one on its sheet, its view kept. A layer's overrides in a viewport
are read as AutoCAD keeps them (`GetViewportOverrides`, the new `DxfLayerViewportProperties`, `HasOverrides`,
`HasViewportOverrides`, `RemoveAllOverrides`), and the drawing's properties as its DWGPROPS (`DxfDatabase.SummaryInfo`, the
new `DxfDatabaseSummaryInfo` and `DxfDatabaseSummaryInfoBuilder`; custom properties read from the groups 300 to 309 and
from the list after a group 90, and written in the form the record has). A group left empty is dropped when the drawing is
saved, as AutoCAD drops it; erasing a viewport takes its overrides out of the layers; a viewport appended takes the first
free number of its layout. Every value set in R2000 and R2018 was read back by
ezdxf 1.4.5, and its recover and audit found nothing to fix in any file, R12 among them; ezdxf reads no MVIEW, so the views
set in R12 were read back by the library.

**NEW.** Dimensions, dimension styles and tables, with AutoCAD's names. `DxfDatabase.DimStyleTable` holds the
`DxfDimStyleTableRecord`s, each with AutoCAD's 74 variables a DXF file holds, read and set in their groups (`Dimasz` 41,
`Dimdec` 271, `Dimjogang` 50 in radians, `Dimtfillclr` the second 70, the arrowheads as blocks by handle from R2000 and by
name in R12, `Dimtxsty` and the linetypes as their records), AutoCAD's own value where a group is missing, `GetArrow`; a
style is added whole, its handle as 105, renamed with the dimensions, leaders and tolerances drawn in it and `$DIMSTYLE`, and
erased where nothing uses it. A DIMENSION is a `DxfRotatedDimension`, `DxfAlignedDimension`, `DxfRadialDimension`,
`DxfDiametricDimension`, `DxfLineAngularDimension2`, `DxfPoint3AngularDimension` or `DxfOrdinateDimension` by its kind, and
ARC_DIMENSION and LARGE_RADIAL_DIMENSION are `DxfArcDimension` and `DxfRadialDimensionLarge`: their points in world
coordinates, `Measurement` worked out from them (96.60 for (0, 0, 0) to (100, 20, 0) along 30 degrees, pi / 4 for a level
line and one at 45 degrees), `DimensionText`, `Prefix`, `Suffix`, `TextPosition`, `TextRotation`, `HorizontalRotation`,
`DimensionStyle`, `DimBlock` and `DimBlockPosition`, and every variable of the style read as the dimension draws with it and
set as an override in the list DSTYLE of its extended data of ACAD, `GetDimstyleData` and `SetDimstyleData`. The block AutoCAD
drew a dimension with is not drawn anew when a point or a variable is set; `TransformBy` moves the points as ezdxf's
`transform()` does and the block with them where it is the dimension's alone and placed at no point of its own. An ordinate
measures along the user's axes its 51 gives, carried with it, so that a move or a turn keeps its measurement. A block,
linetype or text style set on a dimension in no drawing, or on a copy, a `Clone()` of it among them, is taken to the record
of its name in the drawing it is added to, and refused where there is none of that name to take it to; a copy for another
drawing keeps the overrides it read and those set on it, each naming the record of its name there. An ACAD_TABLE
is a `DxfTable`: rows, columns, heights and widths, each cell's text, value, kind, merging, corners, block and attribute
values and its own look, in the forms of R2000 and of R2007 on; `SetTextString`, `SetRowHeight`, `SetColumnWidth`,
`SetBlockScale`, `SetBlockRotation` and `SetBlockAttributeValue` write its data, its block left for AutoCAD to draw anew; a
table moved, tilted, mirrored or scaled alike carries its block where no other entity names it, and its record is read once
while it stands, every cell of 1 000 rows in milliseconds; a copy for another drawing keeps the values its block cells give
the attributes of a block that comes with it, and loses those of a block the drawing has of its own. Held against ezdxf 1.4.5:
recover and audit find nothing to fix in what is written, and ezdxf reads every style, override, point and cell as set.

**CHANGED.** A DIMENSION, an ARC_DIMENSION, a LARGE_RADIAL_DIMENSION and an ACAD_TABLE, which were a `DxfUnknownEntity`, are
objects of their own classes. A dimension made in code is appended only where the drawing has a block of the name of the
block that draws it, as ezdxf's audit takes out a dimension with none; a copy brings its own block to another drawing, as
below.

**FIXED.** A copy of an entity that draws by a block AutoCAD names itself, a DIMENSION's `*D1`, an ACAD_TABLE's `*T1` or an
INSERT's `*U1`, appended to another drawing took that drawing's block of the same name, or was refused where it had none: it
now takes a copy of its own block, under the first name of its letter the drawing has not (`*D2` beside a `*D1`), with every
entity of it and any block of a name the drawing has not that it inserts, as AutoCAD's deep clone between databases does; a
copy within its own drawing still shares its block. A DIMENSION is written in the forms of R12 into a drawing of R12, its
block with it, where every entity of the block has a form of R12, an MTEXT written as the TEXTs of its lines. A copy
into another drawing takes with it, by name, the layers, linetypes, text styles and dimension styles it and its block name
or its dimension style overrides (ACAD DSTYLE 1005, now named in the target, not dropped)
that the drawing has not, and a dimension style's arrow blocks, as AutoCAD's deep clone does; names the drawing has are used
as they are. A circle, an arc and a polyline with arcs refuse a shear whose axes come out of one length, as S(2, 1) R(45°),
which was taken for a scale alike and left a circle a circle (a circle of radius 10 so sheared is an ellipse of area
628.32, not a circle of 785.40); a text takes its lean from it, and an MTEXT, which has no lean, refuses it. The
handle of a DIMSTYLE is read from its group 105, as its 5 is the arrow block of R12, and a DIMSTYLE added is given one so.
`DxfEntity.GetPlane` and `GeometricExtents` are virtual, for the classes of types the reader gives no shapes for.

**NEW.** `DxfHatch`, `DxfLeader` and `DxfMLeader`, AutoCAD's `Hatch`, `Leader` and `MLeader`, read and edited with
AutoCAD's members. A HATCH has its pattern (`PatternName`, `PatternType`, `PatternScale`, `PatternAngle`,
`PatternSpace`, `PatternDouble`, `SetHatchPattern`, its lines by `GetPatternDefinitionAt` as the new
`DxfPatternDefinition`, scaled and turned with the pattern as ezdxf scales them, a pattern made by the user given its
lines as AutoCAD makes them), `IsSolidFill`, `HatchStyle`, `Elevation`, `Normal`, its gradient (`SetGradient`,
`GradientName`, `GradientAngle`, `GradientShift`, `ShadeTintValue`, `GradientOneColorMode`, `GetGradientColors` and
`SetGradientColors` as the new `DxfGradientColor`, from R2004), `BackgroundColor`, its seed points, and its loops:
`GetLoopAt` gives each as the new `DxfHatchLoop` of the library's shapes in the world, a polyline as a `GeoPolygonArc3`,
an edge as a `GeoLine3`, `GeoArc3`, `GeoCircle3`, `GeoEllipse3`, or the `GeoSpline3` that is exactly a part of an
ellipse or the spline it is, clockwise ones running clockwise; `AppendLoop`, `InsertLoopAt` and `RemoveLoopAt` change
them, a loop read written back as it was read, and an associative hatch is so no longer once they change, its boundary
handles taken out (`Associative`, `GetAssociatedObjects`, `RemoveAssociatedObjects`). `Area` is worked out exactly on
the loops' own pieces and nested as the style says: a square 30 by 20 round a circle of radius 5 fills 521.46, an island
touching the outer loop taken off as one inside it, a loop given twice counted as the even-odd rule counts it, and a
thousand islands read in a fraction of a second. A loop is spliced into the bytes of the record, so that three thousand
islands appended one at a time take half a second, not 24, and a HATCH copied into a drawing of another version is
written as that version writes it, its gradient dropped before R2004 as AutoCAD's SAVEAS drops it. A hatch whose
association is taken away is taken out of the reactors of its boundary objects, and a leader is put among those of its
annotation, as AutoCAD keeps them; `GetPlane` and `GeometricExtents` give each its plane and the box of its loops,
vertices or points. A LEADER has its vertices (`AppendVertex`, `SetVertexAt`, `VertexAt`, `RemoveLastVertex`),
`HasArrowHead`, `IsSplined`, `HasHookLine`, `Annotation` (the MTEXT, TOLERANCE or INSERT it points to),
`AnnotationOffset`, `DimensionStyleName`, and the overrides `Dimasz`, `Dimclrd`, `Dimgap`, `Dimldrblk`, `Dimlwd`,
`Dimsah`, `Dimscale`, `Dimtad` and `Dimtxt`, read from its extended data of ACAD (DSTYLE), else from its dimension
style, and written there as AutoCAD writes them; a straight leader is walked as a curve by its vertex index. A
MULTILEADER has its content (`ContentType`, `MText` as a `DxfMText`, `TextLocation`, `TextHeight`, `BlockContent`,
`BlockPosition`, `BlockScale`, `BlockRotation`, `GetBlockAttribute`, `SetBlockAttribute`), its look (`ArrowSize`,
`ArrowSymbol`, `DoglegLength`, `LandingGap`, `EnableLanding`, `EnableDogleg`, the colours, types and attachments of its
lines and words, `MLeaderStyle` by name, `Scale`), its leaders and lines by AutoCAD's indices (`AddLeader`,
`AddLeaderLine`, `RemoveLeader`, `RemoveLeaderLine`, `VerticesCount`, `GetVertex`, `SetVertex`, `AddFirstVertex` and the
rest, the last vertex of a line the point its leader's dogleg starts at) and `MoveMLeader`, each property set written in
the MULTILEADER and its context data as AutoCAD keeps them, the bit of the overrides of its style set. Each is made in
code (`new DxfHatch()`, `new DxfLeader()`, `new DxfMLeader()`, which takes the drawing's multileader style when it is
appended) and refused in a file of R12, and is moved by `TransformBy` as ezdxf's `transform()` moves it, a hatch's seed
points and gradient and a leader's offsets past ezdxf; a hatch scaled unlike along its axes has its arcs made ellipse
edges, as ezdxf makes them, and so has one sheared, which ezdxf leaves circles, and a multileader so scaled or sheared
is refused, as AutoCAD refuses it. A leader of fewer than two vertices is refused by `AppendEntity`, as AutoCAD refuses
one. What AutoCAD works out when it draws them, a leader anew (`EvaluateLeader`) and where a multileader's leaders meet
its words, is not worked out. Every edit was read back by ezdxf, whose recover and audit found nothing to fix.

**NEW.** A `DxfHatch` gives the lines its pattern draws as data, as AutoCAD's `EvaluateHatch`, `NumberOfHatchLines`,
`GetHatchLineDataAt` and `GetHatchLinesData` give them: each a `GeoLine2` in the plane's own system, from where it starts
to where it ends, a dot from a point to itself, the very lines `Explode()` draws, to the bit and in its order, none for a
solid fill or a gradient, and a pattern the HATCH holds no lines of refused as `Explode()` refuses it. A square 30 by 20
filled with a pattern made by the user of lines at 45 degrees 3.175 apart, as ANSI31's, draws 11; round a circle of
radius 5 about its middle, 14. They are worked out once and kept while the HATCH is not changed, 32 bytes a line, so that
a million lines read one by one take 0.7 s, as long as reading them all at once. `Explode()` is as it was, part for part,
on the 309 hatches of a pattern among 917 read from 425 drawings, the other 608 refused as before.
`EvaluateGradientColorAt(value)` gives the colour of a gradient at a value along it, by the library's rules, which
AutoCAD's documentation does not spell out and AutoCAD's output has not checked: the red, green and blue of its colours in
proportion, rounded half away from nought, the end colours beyond them, so that AutoCAD's new gradient, blue (0, 0, 255)
to a pale yellow (255, 255, 153), is (128, 128, 204) halfway; a gradient of one colour holding that colour alone runs to
its shade or tint by `ShadeTintValue` at the other end, black at 0, the colour at 0.5 and white at 1.

**NEW.** `DxfEntity.JoinEntity(other)` and `JoinEntities(others)` join curves into a curve as AutoCAD's `JoinEntity` and
`JoinEntities` do, by the rules of its JOIN: the entity's record written anew to run over them, its look kept, and the
others left as they are for the caller to erase. A LINE takes lines along its line, gaps and overlaps and all, and runs from
the lowest of their ends to the highest; an ARC takes arcs of its circle and an ELLIPSE elliptical arcs of its ellipse,
counter-clockwise from its start, gaps filled, the quarter from 0 to pi / 2 joined to the arc from pi to 3 pi / 2 running
to 3 pi / 2, and one that would make the whole circle refused, as AutoCAD makes a CIRCLE of it; several are taken in the
order of their starts from the source's end on, so that their order does not change what they make. The gaps follow the
JOIN command: that AutoCAD's `JoinEntity` fills them too is not confirmed. A LWPOLYLINE and a POLYLINE of the plane take
lines, arcs and polylines of their plane, the whole of each arc within the tolerance of it, end to end at either end, an
arc as a bulge, turned over where its normal is the other way, a polyline with its bulges and widths; a 3D POLYLINE takes
lines and polylines with no arc in any plane; a SPLINE takes lines, arcs, elliptical arcs, polylines and splines, each as
the spline that is exactly it, all raised to the highest degree among them, its flags of planar and linear taken off where
they no longer hold. Ends meet within 0.001 in space, the point tolerance of `Tolerance.Default`, and where they come back
together the polyline or spline is closed: the four sides of a square 10 by 10 given in any order close a polyline of one
of them, of area 100. A piece that would run back over the one at the end it meets is left out, as a line drawn twice is.
`JoinEntities` gives the indices of those joined, and chains 10 000 segments in any order in 0.23 s into a LWPOLYLINE and
0.19 s into a SPLINE on .NET 10, 0.09 s and 0.13 s on .NET Framework 4.8, 40 000 in 0.47 s and 0.44 s, the time growing
with their count. A closed curve, a fitted polyline and a polyline of no vertices are not joined; a POLYLINE's vertices
are written anew, each with a handle of its own. Every joined drawing of R12, R2000 and R2018 was read back by ezdxf,
whose recover and audit found nothing to fix.

**CHANGED.** A HATCH, a LEADER and a MULTILEADER are a `DxfHatch`, a `DxfLeader` and a `DxfMLeader`, no longer a
`DxfUnknownEntity`: `TransformBy` moves them where it refused, and `DxfBlockReference.Explode` gives them, placed as the
rest of the block are, where it left them out; a MULTILEADER under an INSERT that scales it unlike along its axes, or
shears it, is left out still, as it cannot hold that placing.

**FIXED.** A copy of an INSERT in no file, `Clone()` or `GetTransformedCopy` of it, placed its block as the file was read,
not as it was edited since: a block given a line and a circle of a new radius showed neither in the copy's `GetGeometry`.
It now keeps to its block when the block is renamed in the drawing it came from, as AutoCAD's keeps its id, is appended
to another drawing by the block's name now, bringing a copy of its block where that drawing has none of the name, and places
nothing once the block is erased; it explodes, where it threw that the drawing had no block of its name.

**CHANGED.** A copy in no file, and an entity made in code, no longer holds a file of its own: one file holds every copy of a
drawing, and keeps none of them. The explode of an INSERT of 1 000 000 lines (100 INSERTs of 100 INSERTs of 100 lines)
holds 440 MB where it ran out of a heap of 4 GB, about 460 bytes a part where it was some 3 100, and 27 000 parts take 0.49
seconds where they took 0.65.

**NEW.** `DxfDatabase.WblockCloneObjects(entities, owner)` copies entities of a drawing together into a block of another
drawing, or of its own, as AutoCAD's `WblockCloneObjects` copies the objects of ids, and gives the copies in the order given:
each copied as `Clone()` and `AppendEntity` copy one, and what points from one of them to another kept between their copies.
A HATCH copied with every object its loops follow follows their copies, each listing it among its reactors; a LEADER copied
with its MTEXT points to its copy; a GROUP every entity of which is copied into model space or a layout (a group holds no
entity of a block) is copied into the group dictionary there, its
order, description and selectability kept, under its own name or that name and `_1`, `_2` and on where the dictionary has
it, an anonymous one as the next `*A`. Every copy is checked before any is added, so that a set the drawing cannot hold, a
SPLINE for a drawing of R12, changes nothing. A LEADER copied alone into another drawing, by `Clone()` or by
`WblockCloneObjects`, points to nothing (`AnnoType` `NoAnnotation`), where it kept the kind of an annotation it had lost.

**NEW.** An MTEXT goes into a drawing of R12, which has none, as one TEXT for each paragraph (`\P`, a column break `\N`
taken as one, blank paragraphs at its end left out as ezdxf leaves them): its words without their formatting, as
`DxfMText.Text` reads them, the heights of its codes left out, its height, turn and plane, the lines spaced as
`LineSpaceDistance` says and held at the left, the middle or the right as the MTEXT is held, in its text style where the
drawing has it or is given it by name with the copy, else Standard. A paragraph is not wrapped at the MTEXT's width, as
AutoCAD's SAVEAS to R12 wraps it by the letters of its font, which the library does not read. `AppendEntity` of an MTEXT to a drawing of R12 appends the TEXTs and leaves the MTEXT in no file, where it
refused it; an MTEXT of a block copied into a drawing of R12, the `*D` of a dimension among them, is written so, so that a
dimension of R2018 whose picture holds its text in an MTEXT goes into R12 with its picture. An MTEXT of two lines 2.5 high
held at its top left at (10, 20) comes out as two TEXTs, on the baselines 17.5 and 13.33 high.

**CHANGED.** What a drawing of an older version has no room for is left out of what is copied into it, as AutoCAD's SAVEAS to
that version leaves it out, said in the log at Debug: an override of a dimension style (ACAD DSTYLE) of a variable the version
has not, as DIMLTYPE and DIMFXLEN of R2007 on a dimension copied or made in code for a drawing of R2000, or DIMDEC of R2000 for
one of R12; and a group of a dimension style or a layer copied by name with an entity that the version has not, as a
DIMSTYLE's DIMFXLEN (49), DIMJOGANG (50), DIMTFILL (69), DIMTFILLCLR (its second 70), DIMFXLENON (290) and linetypes (345 to
347), DIMTXTDIRECTION (294) of R2010, and a LAYER's material (347) of R2007, in a drawing of R2000. In a drawing of R12 an
override of an arrow block (DIMBLK, DIMBLK1, DIMBLK2) names the block by its name, as R12 writes it (a 1070 of 5, 6 or 7
then a 1000), where the drawing has a block of the name, and is left out, said in the log, where it has none. A text style
an entity copied between drawings of R12 and of R13 or later names is copied by name, as a layer is. A record set on a
dimension in no drawing for a variable the version has not is refused still.

**CHANGED.** A table is moved, turned, mirrored and scaled alike in every direction by `TransformBy`, where a scale or a
mirror was refused: a scale scales the heights of its rows, the widths of its columns, its cell margins and the text heights
and block scales it and its cells override, a table of rows 10, 8 and 8 high scaled by 2 having rows 20, 16 and 16 high, and a
mirror turns its normal round, its cells standing where the mirror puts them; its picture is carried by the same
transformation where it is the table's alone. An INSERT of a block holding a table, exploded scaled alike or mirrored, keeps
the table; under a scale unlike along its axes or a shear, which a table does not hold, the table is left out, as a
MULTILEADER is. A copy of a table for another drawing keeps the values its block cells give the attributes of the block that
comes with it, each pointer (331) naming the copy of its attribute definition, as AutoCAD's deep clone maps it.

**CHANGED.** The viewport 1 of a layout of paper space, which shows the sheet itself, is not erased, as AutoCAD keeps it, and a
layout whose last active viewport (331) is erased no longer names it. A pointer at what was erased that no list holds, as a
table cell's at an erased attribute definition or an XRECORD's at an erased entity, stays where it stands, and the saving
names it in the log once, not again at each saving after.

**NEW.** `Explode()` breaks more entities, as AutoCAD explodes them: `DxfPolyline`, `DxfPolyline2d`, `DxfPolyline3d`,
`DxfMText`, `DxfDimension`, `DxfTable`, `DxfMInsertBlock` and a straight `DxfLeader`, besides the `DxfBlockReference` it broke
before; the entry after this one adds more. A polyline (`DxfPolyline`, `DxfPolyline2d`) gives a LINE for each straight segment and an ARC for each bulged one, with
its look and thickness and on its normal, so that their walls stand up as the polyline's do (an ARC of a polyline on the
normal (0, 0, -1) is on (0, 0, -1), its angles in that normal's system, as AutoCAD and ezdxf give it), its widths left out as
AutoCAD leaves them out, and a `DxfPolyline3d` its lines; an MTEXT a TEXT for each paragraph, as it is written into R12 (not wrapped at its
width, and a part in another font or height not a TEXT of its own, as AutoCAD makes it); a dimension and a table
the entities of the block they are drawn with (`*D`, `*T`), placed where they draw them, as ezdxf's `virtual_entities()`
places a dimension's; a MINSERT the block in every cell, where it gave the first cell alone (six circles for 2 rows by 3
columns of a block of one); a straight LEADER its lines and its arrowhead, an INSERT of its `Dimldrblk` or AutoCAD's closed
filled arrow as a SOLID. `ExplodeToOwnerSpace` refuses a MINSERT, as AutoCAD's EXPLODE refuses one. A closed polyline of two
vertices, each with a bulge of 1 (a donut), gives one ARC for each segment, two half circles, as AutoCAD and ezdxf give it.
A polyline is exploded in time growing with its vertices: 20 000 vertices with bulges in 0.3 s and 10 000 straight in
0.09 s on .NET 8.

**NEW.** `Explode()` breaks the rest of the entities AutoCAD explodes that need no solid modeler, as ezdxf's
`virtual_entities` gives their parts. A polyface mesh (`DxfPolyFaceMesh`) gives a 3DFACE for each face, the edge from a
corner whose number is below nought hidden, a triangle ending on its third corner with the edge back to its first hidden
with the third, and a face of its own layer or colour in it; a pyramid of a square base gives five faces. A polygon mesh
(`DxfPolygonMesh`) gives a 3DFACE for each cell, row after row, closed where it is closed (a grid of 3 by 4 gives 6, closed
both ways 12), a smoothed one the cells of the surface it shows. An MLINE, still a `DxfUnknownEntity`, gives from its record
and its MLINESTYLE its caps, a LINE for each element along each segment in the element's colour and linetype, and the miter
lines at its joints: Standard through four vertices gives six lines, an element cut by MLEDIT a line for each piece, and the
caps an MLINE suppresses are left out. A splined LEADER gives one SPLINE fitted through its vertices as ezdxf fits it, then
its arrowhead. A HATCH of a pattern gives the dashes of its pattern as LINEs cut by the loops its style fills, a dot a line
of no length: a square 30 by 20 of ANSI31 gives 11 lines, cut exactly where they cross an arc, an ellipse or a bulge, and a
pattern that would give more than 2 000 000 parts is refused before anything is made. A MULTILEADER gives its words as an
MTEXT or its block as an INSERT with its attributes, then each leader line's arrowhead and its LINEs to the end of the
dogleg, or one SPLINE: the words on the left of two leaders give an MTEXT, two arrows and five lines. A HATCH filled solid
or with a gradient, a MESH, and a REGION, a 3DSOLID and a BODY, which AutoCAD breaks with its solid modeler, still refuse,
each saying why. The time grows with the size of the entity: an MLINE of 40 000 vertices explodes into 160 000 parts in
1.0 s, a polyface strip of 16 000 faces on 32 002 points in 0.28 s, and a straight leader of 10 000 vertices now in 0.05 s,
where it took 10.5 s, its look read once for all its parts (.NET 8).

**NEW.** The DXF leftovers of the entry above. A splined `DxfLeader` is a curve as the straight one is: its `Spline`,
`GeometricExtents`, `EndParam` and the members that walk it (`GetPointAtParameter`, `GetDistanceAtParameter`,
`GetClosestPointTo`, the derivatives and the rest), which refused it, answer along the spline `Explode()` fits through its
vertices, its parameter from 0 to 1; splined through (0, 0, 0), (10, 10, 0) and (20, 10, 0) its box reaches y = 10.43, as
ezdxf's spline does, past its vertices. A leader exploded is drawn in the look its dimension style gives it, as ezdxf draws
it: its lines, its spline and its arrowhead take the colour of DIMCLRD, the linetype of DIMLTYPE and the lineweight of
DIMLWD, the override else the style's, the colour's true colour taken off; one of them BYBLOCK, as the style Standard has
them, keeps the leader's own look as before, but for a colour BYBLOCK where the leader writes the colour to use for it (77).
A MESH not smoothed, which the entry above refused, gives a 3DFACE for each face of three or four corners, with the MESH's
look, as ezdxf's `MeshBuilder` renders them: a cube of six faces gives six. A MESH smoothed, one with a face of more than four
corners (how AutoCAD breaks one is not known) and one whose faces have a look of their own still refuse, saying which. A
MESH is read by its counts, as AutoCAD writes it, and one missing a count or holding fewer than it counts is refused, as
ezdxf refuses it. A
`DxfMLeader` copied into no drawing by `Clone()` exploded without its block content, its arrow blocks drawn as the closed
filled arrow, with no word of it: it now finds its blocks, its linetype and its text style in the drawing it was copied
from, as a dimension's copy finds its block, and gives what its original gives; where that drawing has erased a block it
names since, `Explode()` refuses with `InvalidOperationException`, the handle of the block said. A `DxfLeader` copied so
reads its dimension style, arrow block and linetype there too, where it read none: `Dimasz` 2.5 of Standard, not 0.18.

**FIXED.** A `DxfMLeader` of a block copied by `Clone()` or `GetTransformedCopy` lost the values of its attributes, its copy
exploding into the block with none and appended to a drawing writing none: the copy now keeps the handles of the attribute
definitions its values are given for, as AutoCAD's copy keeps them, so that the multileader of GKTAG with NUM 42 copied, copied
again or appended back gives NUM 42. Appended to another drawing, each value is given to the attribute definition of the
same tag in the block of the same name there, and left out, with its definition, where there is none. A
straight leader's `GeometricExtents` and `GetPlane` read all its vertices again for each vertex: 200 boxes and points of a
leader of 1 000 vertices took 47.4 s and take 0.22 s, and the plane of one of 4 000 vertices took 5.6 s and takes 3 ms, the
same answers to the bit (.NET 8).

**FIXED.** The DXF follow-ups of the entries above. A `DxfLeader` had no shapes in `GetGeometry()`, so `IntersectWith` met
nothing on it: its geometry is now its curve, the polyline of its vertices or, splined, the spline `Explode()` fits, and the
leader from (0, 0, 0) to (10, 5, 0) meets the line x = 5 at (5, 2.5, 0), as AutoCAD's `IntersectWith` meets a leader;
`IsPlanar`, which read those shapes, agrees with `GetPlane()`. A copy of a `DxfMLeader` appended back to its own drawing
after the attribute definition it gives a value to was erased kept the handle of that definition, which ezdxf reports
missing; the value is left out with it now, as a copy into another drawing leaves it out, and the original keeps its handle,
as AutoCAD leaves it. A MULTILEADER in a block copied by `WblockCloneObjects` lost its block and its arrow blocks where they
were copied after it, and exploded without its INSERT: block W holding a multileader of GKTAG and an INSERT of GKTAG, copied
into another drawing, now places the copy of GKTAG with NUM 42; and a multileader copied alone into a drawing with no block
of its name, or by `WblockCloneObjects` before an INSERT of that block, lost its block, the INSERT alone bringing it: it
brings its block and arrow blocks now, as AutoCAD's deep clone follows its pointers, whatever the order. Speed, with the
same results to the byte, and the same refusals: a splined leader is fitted once and kept until its drawing changes, the
spline it is fitted to kept however many entities are made elsewhere meanwhile, and a SPLINE reads its control points once
so, so that 100 points along a splined leader of 4 000 vertices take 0.1 ms, where they took 1.6 s, and along a SPLINE of 4
000 control points 0.1 ms, where they took 0.22 s; the parts of a leader and of a polyline exploded are written with their
look in one writing, so that a leader of 10 000 vertices drawn in the colour and lineweight of its dimension style explodes
in 31 ms, where it took 57 ms, and a polyline of 10 000 vertices in 48 ms, where it took 58 ms (.NET 8).

**CHANGED.** `DxfReader` reads a LEADER as its curve and a MULTILEADER as the lines of its leaders, where it left them out
and counted them in `Skipped`. A LEADER is a `GeoPolyline3` through its vertices, a vertex on the one before it left out,
or, splined, the `GeoSpline3` `DxfLeader.Explode()` fits through them; a MULTILEADER a `GeoPolyline3` for each leader line,
from where its arrow points through its vertices and the last point of its leader to the end of its dogleg, or, splined, a
`GeoSpline3` fitted through those points; `GeoPolyline2` and `GeoSpline2` on the floor with `PlanAs2D`. Arrowheads, hook
lines and a multileader's words or block are not read, and a leader of fewer than two vertices apart is skipped as a
polyline is. In a block a spline is fitted where the block has the points and then placed, as AutoCAD draws it, so that
under an INSERT scaled unlike along its axes it differs from the one `Explode()` of the INSERT gives, refitted where it is
placed: by 2.21 for the leader through (0, 0, 0), (10, 10, 0) and (20, 10, 0) scaled 3 along y. So an INSERT of a block
holding a leader or a multileader is met on it by `IntersectWith`, and its `GeometricExtents` hold it, where it was missing
from both: the block holding the leader from (0, 0, 0) to (10, 5, 0), inserted at (100, 0, 0) scaled 2, is met by the line
x = 110 at (110, 5, 0); and a leader or multileader appended to a drawing, or edited, grows the extents of its header as a
line does, where it left them as they were. Leaders and multileaders now count against `DxfReadOptions.MaxPlacedEntities`,
a leader one and a multileader one for each leader line, none where its leaders are invisible, so an INSERT placed within
the bound before can be refused now.
A `DxfLeader`'s `GetGeometry()` is the shape the reader reads, to the bit what it gave before. Every other shape the reader
reads is the same, to the bit and in the same order, but where the bound now stops it, and but for the spelling of a
layer: the reader names a layer as it first meets it, so where there is no LAYER table a LEADER on WALL read before a LINE on
Wall now names the layer of both WALL.

**NEW.** A `DxfMLeader` gives the lines of its leaders in `GetGeometry()`, as the reader reads it, where it gave none, so
`IntersectWith` meets a multileader on them, as AutoCAD's meets it, and its `IsPlanar` is true where they lie in one plane:
each leader line a `GeoPolyline3` from where its arrow points, through its vertices and the last point of its leader to the
end of the dogleg, or, splined, a `GeoSpline3` fitted through those points with the tangents `Explode()` fits with. Both
start at the arrow's point, where `Explode()` starts at the back of the arrow, so a splined line and the spline exploded
part by up to about the arrow's size near it, 0.585 on a multileader whose arrow is 4 long. The line from (780, -10, 0)
through (790, -5, 0) to its dogleg from (790, -1.25, 0) to (798, -1.25, 0) is met by the line y = -3 at (790, -3, 0). Its
words or block, its arrowheads and invisible leaders are not met.

**FIXED.** A pointer to a block copied by `WblockCloneObjects` after the entity that holds it was left out but for a
MULTILEADER's: an ACAD_TABLE in a block W lost its `*T` (343) and the block of its cell (340), and a LEADER or a
dimension in W whose dimension style is overridden with an arrow block (DIMLDRBLK, DIMBLK, DIMBLK1, DIMBLK2) lost the
override, where the block came after W. Each names the copy of its block now, whichever of the block and its INSERT comes
first in W, in a drawing of R12 by the block's name (1070 5, 1000 BOLT); and a table copied alone brings the block of its
cell, as AutoCAD's deep clone follows the pointer, where it came only if the table's `*T` inserted it.

**FIXED.** The extents of a drawing's header left empty, the least corner above the most as the 1E+20 and -1E+20 ezdxf
writes for a drawing it has not measured, never grew: a line from (5000, -4000, 0) to (5100, -3900, 10) appended, or a
vertex of a multileader moved to (5000, -4000, 0), left `$EXTMIN` and `$EXTMAX` empty, before saving and after. Each axis
is now grown on its own, as AutoCAD grows them from empty: empty extents become the box of what is appended or moved,
`Extmin` (5000, -4000, 0) and `Extmax` (5100, -3900, 10) for that line, and grow from there; `$PEXTMIN` and `$PEXTMAX`
likewise for paper space; extents the wrong way round along one axis alone, `$EXTMIN` (0, 0, 0) and `$EXTMAX`
(100, -50, 0), take a circle of radius 5 about (200, 10) to (0, 0, 0) and (205, 15, 0), no axis shrunk. What the drawing
held already is not measured into them, and an erase does not shrink them. Extents set by `Extmin` and `Extmax` are grown
from what they are set to, where what had been appended before was brought back into them. An INSERT appended, moved or
copied by `WblockCloneObjects` left them as they were: it grows them by what it places now, each cell of a MINSERT's array,
at the next reading of the header, so that block W of the line from (0, 0, 0) to (10, 0, 0) inserted at (5100, 0, 0) grows
them to x 5110; an attribute of it written places the block anew in the same way, and an INSERT that places nothing (its
block not defined, an external reference, the block of a layout, or past `MaxPlacedEntities`) leaves them as they are.
Extents the header holds as a box are otherwise grown as they were: 271 drawings, opened and saved
untouched or after a line appended to either space, an entity moved, an INSERT appended, a multileader's vertex or a
lightweight polyline's vertex set, are written to the same bytes but for those variables, changed only where they were
empty or an INSERT was appended or moved, and never shrunk on an axis. Growing by an INSERT places its block, at each
reading of the header after the INSERT changed: one that places 1 000 000 lines, moved and the header read, takes about 2 s
each time, 460 MB at its peak, where it took nothing and left the extents short; none where the header has no `$EXTMIN` and
`$EXTMAX` (.NET 10).

**NEW.** A text's and an MTEXT's text style is set by its name, `TextStyleName` (written as the drawing's table names it,
letters of either case the same), or as its record, the new `TextStyle` (AutoCAD's `TextStyleId`): a style the drawing has not,
or a record of another drawing, is refused, and the style the text has changes nothing; an attribute takes it as a text does.
A text, an MTEXT or an attribute in no drawing takes any name, and is refused when it is added to a drawing that has no style
of it (`AppendEntity`, `AppendAttribute`, or an INSERT added with its attributes), unless it is a copy of a drawing that has
the style, which comes with it. `DxfDatabase.GetObject(handle)` and `TryGetObject(handle, out value)`, AutoCAD's
`GetObjectId(handle)`, give the object of a handle: an entity, a VERTEX or an ATTRIB, an object of the OBJECTS section, a
record of the layer, linetype, text style, dimension style or block table, or one of those five tables. The records the
library has no class for (the APPID, VPORT, VIEW and UCS tables and their records, a block's BLOCK and ENDBLK) are not found,
and `TryGetObject` gives null with false. A drawing is walked once, and the walk is kept until the drawing next changes.

**CHANGED.** An INSERT copied into another drawing that has no block of its name, by `Clone()` and `AppendEntity` or by
`WblockCloneObjects`, brings a copy of its block under that name, with the blocks it inserts that the drawing has not and its
description, units, explodability and scaling, as AutoCAD's `WblockCloneObjects` brings them, where it was refused; a drawing
with a block of the name places its own, as before. What the entities of a copied block point to among themselves is kept
between their copies whichever comes first, as AutoCAD translates its map of ids at the end: a LEADER written before its
MTEXT points to its copy, and a HATCH in a block keeps following its boundary, each listed among the reactors of what it
points to. `SetStaticColumns`, `SetDynamicColumns` and `ColumnWidth` give the MTEXT the width of a column (41, and 40 of its
embedded object), as AutoCAD and ezdxf give it.

**FIXED.** A copy of a HATCH whose spline edge has fit points wrote that edge's count of them (97) nought, as if it counted
boundary objects, so that AutoCAD would read the fit points as other groups; it keeps the count now.

**NEW.** The guide's DXF part is rewritten around the editing model, for a reader who knows AutoCAD's .NET API: when to use
`DxfWriter`, `DxfReader` or `DxfDatabase`; a table from AutoCAD's classes and daily patterns (`Database`, transactions and ids,
the block table, the entities, the tables and dictionaries, `TransformBy`, `Explode`, `IntersectWith`, `WblockCloneObjects`,
`Point3d` and `Matrix3d`) to the library's, with the deliberate differences; the editing rules (setters given their own value,
appending, erasing, `TransformBy` class by class, `Explode` class by class, copying within and across drawings and versions);
and what the model does not do. A worked example opens a drawing, moves a layer, changes a dimension style, sets a note in
columns, hatches a plate round its bolt holes, copies a bolt and its dimension into another drawing and saves, every snippet run
against the library; and a page lists 57 points for the maintainer to try in AutoCAD, each with what the library does now and
what result would mean a change.

**FIXED.** Setting the normal of an entity whose file gives its extrusion not of unit length, as (1, 2, 3), wrote a wrong
normal.

**FIXED.** `GeoPolyline3.TryGetPlane` and `IsPlanar` threw a `DivideByZeroException` on a flat chain spanning past the
largest double, as (0, 0, 0), (1E308, 0, 0), (1E308, 1E308, 0); they now return false. A DXF polyline grown by such a vertex
threw the same.

**NEW.** `DxfReader` reads TEXT and MTEXT, which it counted in `Skipped`, as the new `DxfText`: its `Position`, `Normal`,
`Height`, `Rotation`, `Value`, `IsMultiline`, `WidthFactor` and `Width`, the same with `PlanAs2D` as without. A TEXT
stands in the system of its extrusion's plane, at its first alignment point where it is aligned left on its baseline or
aligned or fitted between its two points, the aligned TEXT from (0, 0) to (100, 0) at (0, 0, 0), and at its second where it
is aligned any other way; an MTEXT at its insertion point in the world, turned by its direction, or by its group 50 in
degrees where it has none, each read from the MTEXT's own groups and not from those of the object of its columns that an
MTEXT of R2018 embeds after its group 101; the rotation is from nought up to 2 pi. The words of a TEXT have %%c, %%d and %%p made the signs,
`%%c20 at 45%%d` being `⌀20 at 45°`, %%% a per cent sign and %%nnn the character of its code, and an MTEXT's have their
formatting taken out and their paragraphs a line feed apart, `{\fArial|b1;Bold}\Pline` being `Bold` and `line`, a new
column `\N` a space and a code with no semicolon after it kept as written; control characters in caret form (`^J`, `^I`)
are the characters, and a TEXT keeps to one line, a line feed in it a space. Placed by an INSERT, a text is carried by its axes, its height stretched
with the y axis and a TEXT's width factor with the x axis against it, so the text of a block scaled by 3 along x and 2 along
y is twice as tall and one and a half times as wide for its height; a mirror turns its normal over, as AutoCAD writes the
text of a mirrored block. Held against ezdxf 1.4.5 on 81 texts placed every way an INSERT places them, every position is within
1E-9 of ezdxf's and every normal within 1E-12. A TEXT or MTEXT with no height is counted in `Skipped`, and a text counts
towards `MaxPlacedEntities`. ATTRIB and ATTDEF are still counted, not read. 100 000 texts (16 MB) are read in 0.15 s on
.NET 10. 72 new tests; the suite runs 12 199 of 12 200, one skipped as before.

**NEW.** `DxfDocument.LayerTable` holds the layers of the layer table as the new `DxfLayer`, in the order of the table:
`Color`, `TrueColor`, `Linetype`, `LineWeight`, and whether each `IsOff`, `IsFrozen`, `IsLocked` and `IsPlotted`; a name
spelled again in another case is the first layer, as `Layers` keeps it. `DxfDocument.GetLayer(name)` finds one in either
case, null where the table has none. The layer steel in red, turned off, which the table writes as the colour -1, is
`Color` 1 and `IsOff` true. `Layers` is as it was.

**NEW.** `DxfWriter.Add(DxfText, layer)` writes a text as a TEXT or an MTEXT, and `Add(DxfShape)` a text read with its
look; `DxfText` has a public constructor for a text made in code. An MTEXT is turned by the direction it reads along, and a
control character is written in caret form. Read back, a text has its words, height, width factor, width and normal to the
bit, its position too but for a TEXT on a tilted normal, which comes back within 1E-13 at 30 mm,
and its rotation to the last place, through degrees; ezdxf recovers and audits the file with no error. Every earlier
overload writes the same bytes: the file of one shape of each hashes the same.

**CHANGED.** `DxfReader` reads a file of R2007 or later whose bytes are not all UTF-8 with a warning, each such byte the
replacement character U+FFFD as before, and reads a stream that can seek into one buffer of its length.

**CHANGED.** The bound of 1 000 000 blocks `DxfReader` works out for the sizes of INSERTs is one for the whole reading, the
drawing's INSERTs and those of the blocks read for `Blocks` together, where it was one for each INSERT, so a ring of blocks
each a root of its own cost a million blocks for every root: a ring of 1 000 blocks, each inserted by the drawing, a file
of 174 KB, took 47 s on .NET 10 and takes 0.19 s, and one of 200 9.5 s and 0.18 s. Once the bound is spent, an INSERT
whose size would need another block worked out is refused with a warning of its own, where it was counted with those past
`MaxPlacedEntities`; a size worked out before still places its INSERTs. A reading that works out fewer blocks is as it was
to the bit: 29 test files under 8 sets of options, the 5 with texts but for their texts, and 15 000
random block graphs. Of the 4 that reach the bound, the two size bombs differ in the new warning alone; in the file where
a second INSERT of a block was placed, by working it out again, after the first had been refused, both are refused now;
and in one whose walks each stayed under the bound, 47 INSERTs of the blocks read for `Blocks` are refused.

**CHANGED.** The viewer reads TEXT, MTEXT and the layer table through `DxfReader`, and hands a DXF file to `DxfReader.Read`
as its bytes, so a file before R2007 is read in the code page its header names: the layer St€l of a file of R2000 in
Windows-1252 is St€l, where it was read as Latin-1, and the viewer gives the runtime the code pages of Windows, so the
layer grün € of one in Windows-1250 is grün € too. Its own pass over the file is gone. A text inside a block is drawn
where its INSERT places it, the labels are among the other items in the order of the file, and a TEXT aligned otherwise
than left on its baseline stands at its alignment point.

**CHANGED.** Offsetting a curved loop that crosses itself no longer slows with the square of its crossings where thousands of
them lie within the tolerance of each other. The crossings that are one place were found by trying each against every crossing
before it within the tolerance, one at a time, and a join refused was tried again at every crossing of the same place beside
it: 200 diameters of a circle of radius 10, 79 003 crossings in places of up to 29 626, took 6.2 seconds to grow by 0.3 with
round joins at a tolerance of 0.1 on .NET 10 (10.4 on .NET 8), and 300 diameters 25 (46). They are tried a place at a time
now, each place keeping where its crossings lie, in the same order as before, and a place whose first crossing lies further
than the tolerance from the first of the crossing's own, which cannot be joined to it, is not tried: 0.73 and 1.4 seconds
(0.96 and 1.7). Of 6.6 million crossings in 403 000 clouds bunched at four tolerances, set against the old way in one run,
none is put in another place, and the 3 672 offsets of stars, 1 344 of random loops of 50 to 130 corners, 18 000 of small dense
loops and 1 260 of stars of up to 61 points, at tolerances of 0.1 and 0.001 each, are the same to the bit.

**CHANGED.** Finding the places a curved loop crosses itself at no longer slows with the square of its crossings where they lie
in a long line along the sweep, or where one place is taken in again and again. Each crossing was tried against every place
with a crossing in a strip as wide as the tolerance along the sweep, however far off across it; the places are filed in rows
across the sweep now, a little wider than the tolerance, and only the three rows round the first crossing of the crossing's
own place are read. And two places are made one without reading each crossing of the one taken in where the corners of the
box round its crossings all lie within the tolerance of the other's first crossing. A loop of 80 000 teeth 0.5 apart crossing
its edge along X and 80 000 more crossing its edge along Y, 160 173 crossings in a plus, took 9.4 seconds to find its places
in on .NET 10 (27 on .NET 8) and takes 0.04 once the code is warm, 0.19 to 0.23 as the first call in a process; 24 000
crossings, a tight bunch of 8 000 taken in by 8 000 more, each further left along X and later in a sweep along Y, and 8 000
far off, 0.14 seconds and 0.017 (0.13 and 0.02). The diameters of a circle, the stars and loops of every day take the time
they took, and 200 diameters grown by 0.3 allocate 163.1 MB where they allocated 159.9 at a tolerance of 0.001, and 92.6
where 89.6 at 0.1. Of 19.9 million crossings in 400 000 clouds bunched at four tolerances, set against the way places were
found a crossing at a time before the last round, none is put in another place, nor of 60 000 clouds of ten shapes, shifted
by up to 50 000 and at tolerances of 0 to 0.3, nor of 120 000 clouds at scales of 1E-200 to 1.7E308 and tolerances up to
the largest a double holds, some with infinite or missing coordinates; and 90 000 offsets of 5 000 random loops and chains,
dense loops, stars, diameters and combs, both ways, with mitred, round and chamfered joins at tolerances of 0.001, 0.01 and
0.1, are the same to the bit.

**CHANGED.** Finding where a curved loop crosses itself no longer slows with the square of its edges where many of them lie
side by side across the sweep. The boxes of the edges were swept along one axis, each tried against every box that reaches
it along the sweep however far off across it, and the plus of 80 000 teeth 0.5 apart across each arm was tried in
3 201 720 029 pairs, of which 480 224 have boxes that overlap and are read for crossings at tolerances of 0.001 and 0.1.
Once the boxes tried have missed more than eight for each edge, the rest are filed in rows across the sweep, as wide as the
boxes are on average across it, and each is read against those in its own rows and the few that lie across many, in the
order the sweep read them; a box that reaches no more than 64 along the sweep, or few more than that, is still read the old
way, and so is every box of a loop wider or higher than a double holds, as one with corners at 1E308 and -1E308 is. Once
warm, the plus finds its 160 173 crossings at a tenth in 0.035 seconds where it took 5.1 on .NET 10, 0.052 where 7.4 on
.NET 8 and 0.083 where 4.9 on .NET Framework 4.8. Its crossings at a tenth lie too close along its edges to cut it there,
and taking them and leaving it uncut takes 0.14 to 0.44 seconds where it took 5.0 to 5.5 on .NET 10, and 0.29 to 0.33 where
4.9 on .NET Framework 4.8; at 0.001 it is cut, which still takes some four minutes on .NET 10, 230 seconds where 256 in one
run of each, of which finding its 160 181 crossings is 0.18 as the first call in a process. Stars, gears, diameters and the
loops of every day are swept as they were, and finding their crossings alone takes the time it took within the noise but for
gears: one of 200 arcs takes 0.089 ms where 0.077 on .NET 10, 15 % more, and one of 1 000 arcs 2.28 where 2.16; on .NET 8
they take 4 to 7 % more, and a star of 49 points 0.059 ms where 0.056; on .NET Framework 4.8 they take 4 to 6 % more. A
whole offset of them takes the time it took, within the noise. Of 599 million crossings of 20 000 loops at six tolerances
from 0 to 1, among them combs and pluses of up to 3 000 teeth turned at any angle, gears, random walks, lattices with ties
and edges of no length, and arcs past a half turn, none is found otherwise, nor in another order, nor when every box is read
in rows from the first on, nor of 235 million more of 3 000 loops at six tolerances, among them pluses scaled by up to 1E299
and loops with infinite or missing corners; and 18 000 offsets of 1 000 random loops and chains, dense loops, stars,
diameters and combs, both ways, with mitred, round and chamfered joins at tolerances of 0.001, 0.01 and 0.1, are the same to
the bit.

**CHANGED.** Cutting a curved loop at its crossings no longer slows with the square of them where they cut it into thousands
of loops. Which side of each run between two crossings the region lies on was told by reading the middle of its longest piece
against every loop the crossings cut the loop into, and the plus of 80 000 teeth 0.5 apart across each arm, cut at a
tolerance of 0.001 into 160 002 loops and 320 358 runs, read 51 billion boxes where those of one or two hold each point. The
boxes of the loops are kept in a tree now, halved by where their middles lie, and a point is read against the loops whose
boxes hold it; 64 loops or fewer are each read, as before. The plus is cut in 0.75 to 1.6 seconds where it took 257 to 299 on
.NET 10, in 0.77 to 1.8 where it took more than 600 on .NET 8, and in 1.2 to 1.3 where more than 600 on .NET Framework 4.8;
one of 40 000 teeth to each arm in 0.43 to 0.86 seconds where 37 (.NET 10), 0.43 to 0.87 where 120 (.NET 8) and 0.57 to
0.71 where 285 (.NET Framework 4.8). At a tenth, where the plus is not cut, it takes the time it took. A zigzag of 500 teeth
is cut in 0.57 ms where 0.95 on .NET 10 and in 1.2 where 6.1 on .NET Framework 4.8, and a star of 49 points, gears and random
loops are cut and offset in the time they took, within the noise; a star of 131 to 301 points, its 65 to 150 loops' boxes each
holding most points, is cut up to 9 % slower on .NET Framework 4.8 and 0.6 % on .NET 10. Of 120 000 cuts of 20 000 loops at six tolerances from 0 to 1, among
them combs and pluses turned at any angle, gears, random walks, lattices with ties, arcs past a half turn, and loops moved
ten million off, scaled by 1E-4 or 1E4 or run out to 1E308, 37 738 cut into 20.8 million loops, none gives another loop,
edge or bulge, to the bit; nor are the 599 million crossings of those loops found otherwise, and 18 000 offsets of 1 000
random loops and chains, dense loops, stars, diameters and combs, both ways, with mitred, round and chamfered joins at
tolerances of 0.001, 0.01 and 0.1, are the same to the bit. A whole offset of the plus is still slow: grown by 0.3 at 0.001,
one of 8 000 teeth to each arm takes 20 seconds, four times what one of 4 000 takes.

**CHANGED.** Offsetting a curved loop whose edges run across the whole of it, as a star's do, no longer sorts the edges near
each piece: the edges whose boxes overlap a piece's were put in order by sorting them, and in a star of 61 points every edge
overlaps every other. Where they are many they are put in order by marking them: the stars of 21 to 61 points grown and shrunk
by 0.3 and by 1 at a tolerance of 0.1 take 17.5 seconds in all where they took 32.6 (19.2 and 34.5 on .NET 8), the same to the
bit. The rest of that time is the pruning trying every pair of edges that cross, which a star has.

**CHANGED.** The booleans and `TryCutOpenings` do less work where faces lie back to back. A plate 300 by 200 by 12 with
twelve blind holes, turned and moved off the origin, cut at a tolerance of 0.01, made 45 000 small boolean answers in its
plane, each of a few edges, whose edges were filed under 6.4 million lists made and thrown away: three such plates took 1.9
seconds and 1.4 to 2.2 GB each. Up to 64 edges none is filed now, a point asking each edge whether it would have been; two
faces of one plane lying apart in it by more than four times the tolerance, across a side of either, are not laid out and
shared to find nothing; the faces lying back to back are swept along the axis that leaves the fewest pairs to try; and the
edges that cancel are taken out in one pass. The three plates take 0.91 seconds (0.90 on .NET 8) and 0.4 to 0.6 GB each, and
a column of 12 002 faces united with a beam, 5.3 and 4.7 seconds either way round, takes 2.1 and 1.3 (much the same on .NET
8). Of 18 600 unions, differences and common parts of boxes, columns, beams, cylinders and plates meeting within a few
tolerances, turned and moved off the origin at three tolerances, 600 plates cut at once and 72 000 booleans in the plane none
moves by a bit, nor do the earlier sets of exact-tolerance, band and bent-bar booleans, and a build that worked
out every pair said to lie apart found no area shared by any.

**CHANGED.** A clash bar grazing the faces of a part no longer searches along itself for each triangle near it that it never
comes within the tolerance of: the least distance between the bar's piece and the triangle is worked out outright first, and
only a triangle within the tolerance and a rounding of it is searched as before. 50 bars grazing a facet of a cylinder of
10 000 sides 0.0005 off it, which come near the boxes of 5 000 of its triangles each and within the tolerance of 8, take 0.39
seconds where they took 0.60 (0.49 and 0.66 on .NET 8), the part's preparing included; the 48 000 random bars along the faces
of boxes, L-sections, combs and slotted plates, turned three ways, give their lengths to the bit. Bars grazing a cylinder in a
helix are as slow as they were, 5.1 seconds for 50: their time is in measuring how deep the part reaches into them, a point at
a time.

**CHANGED.** A clash bar running at a slant past a large mesh, or passing the rim of its caps, no longer opens what only the
box of a piece of its centre line comes near. Where the bar check asks the part's index for the triangles a piece comes
within the tolerance of, and for how near a piece comes, a box of the index is passed over where the piece itself stands
further off than that across a direction square to an axis and to the piece, and a triangle where it does across its normal,
or across a direction square to a side and to the piece or to the normal, the reach read a rounding wide by the largest
coordinate of the piece and of the box or the triangle, so that a triangle millions across beside a piece near the origin is
not passed over by the rounding of where its corners stand. Of a cylinder of radius 500 and 10 000 sides, prepared
beforehand, 50 bars of radius 40 running at a slant 20 to 30 clear of it take 6.8 ms on .NET 10 where they took 32 (8.4 and
27 on .NET 8), measuring 13 952 triangles for their nearest approach where they measured 169 522; 50 of radius 10 take 7.7
ms where they took 24 (9.3 and 20); 50 grazing a side 0.0005 off it, through the rim of both caps, are given 4 triangles each
where they were given 2 500, of the slivers the caps are cut into, and take 41 ms where they took 152 (42 and 162), against 28
(24) before a bar's run was counted by the triangles near it; and 50 touching it at a slant take 12 ms where they took 24 (16
and 22). 4 000 random bars by prisms of up to 3 000 sides and by combs, turned and moved up to a million off, take 2.4 to 3.1
seconds on .NET 10 where they took 4.2 to 7.2. Every answer is the same to the bit: those sets on both runtimes, 12 000
random bars, and 48 000 nearest approaches of pieces by random meshes, slivers among them, at three tolerances. In 60 000
askings by random meshes, 17 million triangles a piece is no longer given, of which 10 million were searched as the bar check
searches and none comes within the reach, and those given keep their order; and of 80 000 pieces set at a known distance
from a triangle up to 30 million across, worked out exactly, none is passed over. Asking for the triangles near a piece costs
more in the index where it gives 7 times fewer: 16 000 askings by 40 random meshes take 0.55 to 0.73 seconds where they took
0.36 to 0.46, at best of four, and 0.69 to 0.93 where they took 0.60 to 0.77 run once. The benchmark of bars beside parts
of 12 triangles each runs about 5 per cent slower on .NET Framework 4.8, 4.3 to 5.1 ms where it ran in 4.2 to 5.0.

**CHANGED.** `GeoObb3.Fit` is many times faster on a hull of thousands of faces. The box on each face of the hull was fitted
round every copy of every corner of every face: 3 000 points on a sphere make 6 000 faces and 18 000 copies, sorted on each
face, and took 7.7 seconds on .NET 10 and 15.2 on .NET Framework. Each corner is taken once now, and a face is passed over where no
box on it can be as small as one already found, the least rectangle on it bounded from below by calipers round the corners on the
rim the hull shows along its normal: 8 of the 6 000 faces are fitted, in 0.40 and 0.54 seconds. The box
is the same to the bit, on 3 122 sets of points from cubes, plates and lattices with copies to spheres and ellipsoids, at three
tolerances, on both runtimes.

**CHANGED.** The booleans, offsets and meshes of the plane no longer slow with the square of the corners of a shape on one line.
Clipper2, which resolves them, put each local minimum of a scanline into its list of edges by walking the list from its head,
looked through the whole list for the other side of a maximum that ends on a horizontal, and tried every pair of horizontals on a
line against each other: a zigzag of 100 000 vertices, its 50 000 minima on one line, took 5.9 seconds to unite, and a comb of
25 000 teeth 7.4. They take 0.11 and 0.09 seconds (0.08 and 0.09 on .NET Framework), with the same paths to the point: of 19 200
runs of every operation and fill rule over random, degenerate, comb and zigzag sets, open paths and polytrees among them, none
moves, nor of 14 400 with the new ways forced on wherever they could be taken.

**CHANGED.** Offsetting a polygon, a face or a chain in a plane in space no longer slows with the square of its edges where they
run along the first axis of the frame it is offset in. The region solver swept the edges along that axis, where the teeth of a
comb all overlap: a comb of 4 000 teeth running along it took 4.7 seconds to offset by 0.5. Where a sweep across the axis tries
fewer than half as many pairs it is swept so now, finding the same crossings and trying them in the same order: 0.14 seconds (0.08
on .NET Framework), and of 1 680 offsets of combs both ways, slotted plates, zigzag strips, serpentines and random loops, none moves
by a bit, nor with the sweep across forced on everywhere.

**CHANGED.** Cutting a face into many pieces, closing the halves of a cut body and merging the faces of one plane no longer try
every loop against every other for which lies inside which. A comb face of 5 000 teeth cut across them took 1.2 seconds (about 7
on .NET Framework) for its 5 001 pieces, 25 million tests; a loop is asked of another now only where its first corner lies in the
other's box, grown by the tolerances, and the cut takes 0.05 seconds (0.02). Of 400 cuts and merges of combs, plates with grids of
holes and bodies nested three deep, none moves by a bit, nor with the boxes asked of every set of loops however few.

**CHANGED.** The booleans, offsets and meshes of the plane no longer slow with the square of the edges in giving the points of
their answer back their full precision. Each point Clipper2 hands back is given its digits again from the input vertex it was
rounded from, or from the crossing of two input edges near it, and the edges were looked up in the cells of a grid 64 across the
drawing, each edge filed under every cell of its box: on a long shape a cell held thousands of edges, and every point read them
all. A comb of 10 000, 20 000, 40 000 and 80 000 teeth pointing down from its back took 0.52, 1.43, 4.37 and 15.5 seconds to
offset by -0.5 on .NET 10, where Clipper2 itself takes a third of a second at 40 000; and 8 000 and 16 000 strips 100 000 by 1,
each a tenth along and a thousandth up from the one before, took 3.4 and 9.4 seconds to unite. The edges are found now in cells as
many as the edges, each as much wider than tall as the edges run further across than up, no finer than lets the edges fill sixteen
cells an edge on the whole and no coarser than the 64, and kept only where they were before, in the same order; and each input
vertex is filed once, under its own cell, where it was filed under every cell a step of the grid from it. The comb takes 0.23,
0.44, 0.79 and 2.1 seconds (0.27, 0.41, 0.74 and 2.0 on .NET 8), and the strips 1.3 and 2.3, at their peak in 90 and 100 MB where
they took 100 and 130. The comb with its teeth pointing up from its back is hardly quicker, 1.8, 6.5 and 31 to 37 seconds at 10
000, 20 000 and 40 000 teeth before and 1.3, 5.7 and 21 to 43 now: Clipper2's own union of it slows with the square of the teeth.
Of 2 800 booleans, 1 260 offsets and 300 meshes of stars, rectangles a hair apart, far-off copies and combs at three tolerances
none moves by a bit, nor do the strips, and in a build that ran the old lookups beside the new, over those and the whole unit
suite, every one found the same edges and vertices in the same order.

**CHANGED.** `ConvexHull3` and `GeoObb3.Fit` no longer slow with the square of the points where every point is on the hull. The
hull read each point against every face built before it, standing or gone, and the box worked out its bound on each face of the
hull from every corner and every face: 3 000, 6 000 and 12 000 points on a sphere took 0.14, 0.66 and 2.4 seconds to hull and 0.53,
2.2 and 7.7 to fit on .NET 10. The faces of a hull being built are filed by the way they face, and a point is read only against
those that can stand below it; and past 1 536 edges the corners, and the edges between two faces by their normals, are laid out
in trees of boxes, and a face's bound reads only the corners reaching furthest along its normal and the edges of its rim: 0.03, 0.07
and 0.18 seconds to hull, 0.27, 0.76 and 1.9 to fit (much the same on .NET 8). 10 000 points in a cube take 50 milliseconds as
they did, and 2 000 small sets of 4 to 400 points within about 5 % of what they took. The hull and the box are the same to the bit
on 607 sets of points from cubes, spheres, plates, lattices with copies, cylinders and ellipsoids at four tolerances, and every
face a point saw, every rim and every reach was checked against the old reading in a build that ran both, with the new reading
taken however small the hull, over those and the whole unit suite.

**CHANGED.** Merging the faces of one plane, as the hull, the booleans and `MergeCoplanarFaces` do, no longer asks every group
found so far which plane a face lies in. 12 000, 24 000 and 50 000 triangles each in a plane of its own took 0.14, 0.45 and 1.6
seconds to group; past 64 groups they are filed by their normals in cells a little wider than the vector tolerance, and only those
in the 27 cells round a face's normal are asked, which hold every normal the tolerance can join to it, the first found as before:
16, 26 and 44 milliseconds. The merges of 1 500 sets of nudged triangles at five tolerances, and 1 800 booleans of turned boxes,
600 each of union, subtraction and intersection with 2 315 results, give the same faces to the bit, and every group found was
checked against the old search.

**CHANGED.** Clipper2 no longer sorts the edges of a scanbeam to find where they cross when none does. A zigzag of 100 000,
200 000 and 400 000 vertices with its minima at 50 heights and its maxima on one line took 0.56, 2.0 and 5.8 seconds to unite on
.NET 10, most of it in that sort, which walked the list of edges log n times to swap none; whether they stand in order is seen as
their places at the top of the scanbeam are worked out, and the sort is left out where they do: 0.23, 0.63 and 1.6 seconds (0.17,
0.53 and 1.4 on .NET 8). With its maxima at 30 heights as well, 1.9, 5.0 and 15.8 seconds are 0.95, 2.6 and 7.9: every scanbeam
still walks every edge. Of
2 400 runs of every operation and fill rule over random, degenerate and zigzag sets none moves, and in a build that sorted anyway
the sort found nothing to swap wherever it was left out.

**CHANGED.** Offsetting a comb whose teeth point up, or growing one whose teeth point down, no longer slows with the square of its
teeth, and Clipper2 unites shapes with their local minima at many heights faster. Uniting the raw outline of
the offset, Clipper2 looked left along its list of edges from each tooth's tip for the nearest edge of an output path, past every
tooth not yet reached: a comb of 5 000, 10 000, 20 000 and 40 000 teeth 2 wide and 100 long, 2 apart, pointing up and offset by
-0.5, took 0.28, 0.93, 4.6 and 22 seconds on .NET 10, and pointing down and offset by 0.5 as long, 30 seconds at 40 000. A walk
that passes many edges now keeps the stretch it walked, kept true as edges come to add to output paths, swap places and leave the
list, and a later walk goes on past it: 0.09, 0.21, 0.36 and 0.65 seconds (0.07, 0.14, 0.37 and 0.83 on .NET Framework), as the
comb pointing the other way takes. Each local minimum went into the list by a walk from the nearest one done before it on its
line, past the whole list again for every doubling of those done; once the walks of a line have passed 16 edges for each of its
minima, the edges of the list, where they stand in order of X, are kept, and each walk starts at the last of them below its
minimum. A zigzag of 100 000, 200 000 and 400 000 vertices, its minima at 50 heights and its maxima at 30, took 1.3, 3.5 and 10.7
seconds to unite and takes 0.89, 1.7 and 3.6, two thirds as long at 100 000 vertices and a half to a third beyond (1.4, 3.8 and
10.4 and now 0.75, 1.8 and 3.9 on .NET Framework, the median of three runs each). In six runs of 10 000 sets of random paths,
combs and zigzags, rows of rectangles, nested rings and offset combs, each united, cut, intersected and xored under every fill rule
into paths and polytrees, or offset and combined by the library, the last on the code as it stands, none moves by a bit, nor with
both new ways taken wherever they could be; and other work takes as long as it did.

**CHANGED.** Uniting shapes that stand side by side along a line, squares in a row or rows of them sharing their sides, no
longer slows with the square of their number. Clipper2 stretched every horizontal segment over the whole run of points at its
height, and the strip's top and bottom are one run, so every segment walked all of it to find its left end claimed already; it
walked round the path once for each point it took off a spike doubling back along the shared sides; and the library's cleaning
took each point left along a line out of a list, moving the rest. 5 000, 10 000 and 20 000 squares of side 10 in a row took
1.3, 4.8 and 20 seconds to unite with `Boolean2.Union` on .NET 10, and 20 rows of 2 000 took 3.1. A long run's ends are filed under
each of its points now, the walk round the path goes on past the points whose neighbours have not changed, and the cleaning
links the points both ways round: 0.2, 0.22 and 0.5 seconds, and 0.95 (0.17, 0.13, 0.33 and 0.6 on .NET Framework). Of 10 000
sets of random paths, combs, zigzags, rows of squares, lattices and strips, each united, cut, intersected and xored under every
fill rule into paths and polytrees, or offset and combined by the library, none moves by a bit, nor with the new ways taken
wherever they could be. The benchmarks time an offset comb, a row of squares and a zigzag now, `Plane.Offset.CombUp`,
`Plane.Union.Squares` and `Plane.Union.Zigzag`, each signing as it did at 27340a1.

**FIXED.** A union of two bodies wound alike that comes out wound against itself is worked out again with each cell no
thicker than the tolerance lying within the other body against its face left out, the side of the cell nearest a face
of the other lying on it to the rounding and facing its way, and taken so where that closes wound alike and holds a
volume a union can: no less than the larger, no more than the two, and the two less their common part within the
tolerance times the area of that part's skin. Cut by the other's planes, such a cell, a slab exactly the tolerance
thick, was read on the other's skin and kept beside the other whole: a turned cube of 100 joined to a slab 40 by 30 and
0.001 thick inside its top came out wound against itself round the slab's sides, 1 000 000.4, and is the cube now; a
column 300 by 300 joined to a beam whose end ran exactly a thousandth into it held 100.96 cubic millimetres over their
exact union, 66.35 over the two, and holds 1.50 under the exact union, the beam beyond the column's face. A union that
comes out wound against itself and does not hold such a volume, worked out again or not, is reported as not worked
out, `false`, logged: a turned cube of 100 and a block 40 by 40 by 50 running exactly 0.001 into its top at the corner
held 1 080 003.21, more than the two, 1 080 001.6; and at a hundredth, a column joined to a beam 377 wide running
exactly the tolerance into it held 214.9 over their exact union, wound against itself. One wound against itself that
holds such a volume is kept as it was. Of 6 400 column and beam joins and differences at a thousandth with the beam's
end 0.8 to 2 tolerances into or off the column, 16 that were wound against themselves are 2, 8 of them valid now and 6
not worked out; at a hundredth, 4 are 0, 2 not worked out. Of 5 400 booleans of a cube and boxes exactly the tolerance
into, onto or beside it, 116 unions so wound are none, 102 valid and 14 not worked out; of 400 bodies turned by
rotations of whole-number quaternions, column and beam exactly the tolerance into each other, 28 so wound are 26 valid
and 2 not worked out; of the 12 000 column and I-beam joins and cuts, one union wound against itself, 88 447 862.2, is
not worked out. A union that came out wound alike is the same to the bit, in all of these. Sharing nothing at the
tolerance, as two bodies overlapping by a slab exactly the tolerance thick do, a union worked out again is taken holding
anything from the two less the tolerance times the area where they lie face to face, to the two; one as it came, wound
against itself, only holding the two.

**FIXED.** A union of two bodies whose faces lie against each other a gap within the tolerance apart, the gap's two rims
left open by a weld that keeps every face on its plane, closes the gap by a strip along each edge of a rim, where the
rims are loops a few thousandths apart corner for corner with nothing of the body between them: no face along a rim
faces away from the other rim, and none standing across the gap runs on toward it. The weld put the one rim on the
other before, tilting the faces beside it. A column 300 by 300 joined, within a hundredth, to a beam 387 wide and 118
high whose end stands 0.008 clear of its face held 41.06 cubic millimetres more than the two and the gap filled, the
parts of the beam's end beyond the column tilted into wedges, and holds the two and the gap filled now. Of 6 400 such
joins at a hundredth, 22 held more than that, by up to 144, and none does. Rims that bound a slab both bodies hold, a
beam's end exactly the tolerance within a column, are no gap, and are welded as before.

**FIXED.** A difference or a common part of two closed bodies that meet, that makes a closed body enclosing no more than
a slab the tolerance thick as `Validate` reads it (`SolidIssueKind.NoVolume`), with nothing else wrong with it than such a
slab has (thin faces, short edges, edges three faces share), reports nothing left, `false` with `BooleanOutcome.Empty`,
where it handed the slab back: the common part of a turned cube of 100 and a box 40 by 30 and 0.001 thick inside it was
the box, 1.2 cubic millimetres enclosing nothing, and a plate 10 by 10 and 0.0018 thick less a box over all but 0.0018 of
its width left a rod 0.0018 square, 0.000 032 4 cubic millimetres. Not asked: two bodies apart, the first handed back as
it came, a body that does not close, and what is left of a body that itself encloses no more than such a slab, which is
as it was. Of 5 400 booleans of a cube and boxes exactly the tolerance into, onto or beside it, 58 such common parts are
nothing now; of 12 000 joins, differences and common parts of a column and an I-beam within the tolerance, 11 common
parts, and of 6 000 others 2.

**FIXED.** `TryCutOpenings` gives the material of a closed body with closed openings closed as `Validate` reads it, as a
boolean gives its result, or returns `false`, logged. Cut in at once, it was handed back open: a plate 300 by 200 by 12
with two blind holes ending 0.015 under its top, turned and far from the origin, came out open at a hundredth by four
edges 0.015 long round the top of a hole, and the same plate with twelve such holes, of 30 turnings, came out open for
15 with the holes 0.011 under the top, 9 at 0.015 and at 0.02 and 8 at 0.05, one of them 0.4 cubic millimetres over.
Where the cut at once leaves it open, it is welded within the tolerance where that closes it and moves its volume no
further than moving its corners by the tolerance can, and otherwise the openings are taken out one at a time by a
difference, as `GetVolume` takes them out: all 120 close now, within 0.03 cubic millimetres of what `GetVolume`
measures, and a cut that closed is the same to the bit. The writers, the meshes, the cells and the queries that cut a
body's openings near what they ask about take that material; where none closed can be had, `DxfWriter.Add`,
`TriangulateSurface` and the queries read the material cut in at once, open, as they did, with a warning, and an opening
that taken out one at a time would leave nothing where the cut at once left material is not believed, and warned of. The
booleans and `GetVolume` cut as they did. A cut that closed takes 2 to 5 % longer for the check (1.0 seconds a plate on
.NET 10), and one that came out open 6 to 21 % longer.

**FIXED.** `GeoSolid3.TrySplitBy` with a plane parts a body that closes as `Validate` reads it into two halves that close
so too, or returns `false`. A plane a hair over the tolerance inside a face, and all but parallel to it, cuts off a slab
little thicker than the tolerance, every edge down its sides about the tolerance long, and the slab came out open: a hull
of 14 points 20 across, far from the origin, cut 0.0011 inside a face gave one of 0.022 cubic millimetres open by an edge
0.0011 long. Of 3 000 such hulls cut up to 0.002 off a face and tilted 1E-6 to 1E-2 off it, 842 were split and 79 gave a
half open to `Validate`, 36 of them open to `IsClosed` too. Where a half comes out open the body is cut again within a
tenth of the tolerance, and then a hundredth, and the halves are taken where both close and hold the body within the
tolerance times the section; failing that, what is open is welded closed within the tolerance and taken on the same
terms, as far again as the weld moved it. Of the 79, 78 are cut again at a tenth and one at a hundredth, and none is
welded or refused: the halves of 77 add up to the body within 1E-8 cubic millimetres and of the other two within 0.00025,
where welded first they were up to 0.045 off. None of 20 000 such hulls gives an open half where 493 did, nor of 5 000
at a tolerance of 0.01, where 142 did, or of 1E-4, where 106 did, and in none of them is a weld needed. A split whose
halves closed is the same to the bit, and so are 8 000 other splits of hulls, turned boxes and an L-shaped prism with a
hole and an opening, 4 000 at 0.01 and 2 000 at no tolerance. The check makes a split that closed about a third slower:
0.16 milliseconds a hull of 14 points on .NET 10, the least of ten runs and the median of three such sweeps, where it
took 0.115; a split that came out open takes 0.49 where it took 0.28. With no tolerance the halves come back as cut. A
split whose halves neither a finer cut nor a weld closes is refused, where its open halves were given before: of 4 000
cuts near a face of boxes whose faces carry their own copies of the corners a hair apart, 12, their slabs 5 to 45 % off.

**FIXED.** `GetVolume`, `TryGetVolume` and the other measures of a body with openings could keep an opening in the
material, or take material out with it, and `TryGetVolume` trusted the volume. Cut in at once, the body is parted by the
planes of its openings and each piece judged by one point of it; a plane that runs through a piece and will not cut it,
the rim of that cut not closing, left the piece to be judged whole, part in the opening and part out. The halves
`TrySplitBy` gives of a body cut near a face through an opening came out so: a hull of 14 points 20 across, far from the
origin, cut within a hundredth by a plane 0.019 inside a face, a box 3.5 by 3.5 by 5 standing through the cut, left a
half holding 1 465.33 cubic millimetres where the hull less the box holds 1 434.77 on that side, the box left in; at the
default tolerance another held 854.10 where it holds 856.19, 2.09 of its material taken with its box. Where a piece a
plane of an opening would not cut holds more than the tolerance times its area on each side of the plane, and the middle
of the one part is in the opening and of the other out of it, or two such planes of one opening part it into four of
which one is in the opening and another out, the cut at once is not believed and the openings are taken out one at a
time by a difference: the two halves hold 1 434.58 and 856.19 now. Of 77 372 different halves, at the default tolerance
and at 0.01, of plates with one to three box and prism holes, through and blind, flush with the faces and drawn past
them, split across the holes at many places and angles, of hulls with turned boxes in them, and of hulls cut near a
face through a box, 9 were trusted and off by more than the tolerance times their area, by up to 716 cubic millimetres,
and none is now; 6 more within that bound came 0.6 to 7.4 nearer, and the rest are the same to the bit.
`TryCutOpenings`, and the queries, the meshes and the writers that cut a body's openings, take them out one at a time
too where they took the wrong material: 7 of 8 690 near-face halves, none now. Where the openings will not come out so
either, they read the material cut in at once, as they did, with a warning. The booleans read such an operand as cut
in at once, as they did, and note it in the debug log: a boolean of it can still be off by the piece judged whole.
`GetVolume` takes as long as it did, within the noise of the machine: 3.2 to 3.8 milliseconds a plate on .NET 10, the
least of seven runs, the check costing no more than 2.5 % of it.

**FIXED.** `GetVolume` and `TryGetVolume` could still keep the corner of an opening in the material where three of its
planes, meeting at that corner, ran through a piece of the body and none would cut it: each plane alone, and each two,
part the piece so that the middle of every part is out of the opening or on its skin, and the piece was judged whole by
one point. A cube 10 across whose top, bottom and one side are each two faces meeting along an edge 0.02 off square, the
two copies of the edge 0.0001 apart at one end, is closed within the default tolerance, but x = 5, y = 5 and z = 5 each
cross the crack between the copies at a slant and will not cut it; with the corner of a box, x, y and z from 5 to 12,
standing in it, it held 1 000 cubic millimetres, the box left in, and `TryGetVolume` trusted it. The eight parts three
such planes make are read now as the four of two are, and where one is in the opening and another out the openings are
taken out one at a time by a difference: the cube holds 875.00, the cube less the corner. Of 6 000 such cubes, the
opening a box, upright, tilted, skewed or sheared far out of square, a tetrahedron, a pyramid, a tapered prism, two boxes
sharing the corner, or three tilted planes meeting near the middle, placed upright, turned and far from the origin, at
tolerances of nought, 0.001, 0.01 and 0.05, 86 were trusted and off by more than the tolerance times their area; 49 are
right now, and every other volume is the same to the bit. The 37 left are all at the default tolerance, 0.001, and are
other faults: 32 are taken out by a difference as they should be, but the difference, cutting the cube by the same
planes, leaves the opening in as well; 5 are boxes sheared until the normals of their faces lie a hair off one plane, and
are read as before. The threes are read only where they meet within the opening's box, and only of the first 16 planes
of an opening to cross a piece: a cone of 128 facets has 341 376 threes, all meeting at its tip. `GetVolume` of the
cracked cube with the corner of the box in it takes 1.9 milliseconds on .NET Framework 4.8 and 5.5 on .NET 10, where it
took 0.35 and 1.0 to leave the box in, the least of seven runs; a cube 10 across with three box openings at its corners
takes as long as it did, 3.7 and 11.4 milliseconds, and the cracked cube with a cone of 128 facets standing on it 43
seconds and 12, as it did within the noise of the machine.

**FIXED.** A boolean of two closed bodies could keep or drop whole a cell of one that the other takes an edge or a corner
of, and `TrySubtract`, `TryIntersect`, `GetVolume` and `TryGetVolume` trusted what it gave. A plane of one body that runs
through a cell of the other and will not cut it leaves the cell to be judged whole by one point, and a cell to be kept on
one side of such a plane and not on the other was looked for by one point either side of each plane alone, which for an
edge or a corner of the other body all lie beyond it. The cracked cube of the entry above less a box x from -4.1 to
4.993, y from -3.04 to 5.008 and z from 4.9986 to 11.03, its three planes crossing the cracks at a slant and leaving the
rims of their cuts open by 0.043, 0.0057 and 0.029, beyond the four tolerances a rim is closed across, was cut by every
plane of both into one cell, the cube, taken whole and welded: the difference held 1 000 cubic millimetres where it holds
874.94, and so did the cube with the box as its opening, taken out by that difference. A cell of a body that closes,
cut by the planes of another that closes, is read now as a piece of a body cut for its openings is: by the middles of
its parts either side of each such plane, of the four two planes make and of the eight three make that meet within the
other body's box, each read only where it lies in the material of the cell, out of its openings, since a part of a cell
that is not convex can have its middle outside it; where one is to be kept and another not, the boolean is worked out
another way, and the difference, cutting the box, holds 874.94. A common part whose cells are judged so and glue into
nothing is reported as not worked out, not as nothing in common: a cracked U and a cracked box standing in its notch,
which share 38.14, gave nothing. The parts of a piece, here and where a body's openings are cut in at once, are read
from the point of each plane nearest the piece rather than from the plane's own origin: read from the origins of a
prism's planes up to 8.4 km off, the middle of a quarter of a cube 10 across lay 2.7 km away along the prism. Of the 6 000
cubes of the entry above, the 32 the difference left the opening in and the 5 sheared boxes hold within 0.0033 of what
they should now; 16 right in the sheared family move within the tolerance times their area, 11 nearer and 5 farther by
no more than 0.0021, and every other volume is the same to the bit, on .NET 10 and on .NET Framework 4.8. Of 18 000
differences, common parts and unions of the same cubes and openings, 45 trusted and wrong are right now, 32 differences
and 13 common parts, and 30 common parts at a tolerance of nought given as nothing where they hold more are reported as
not worked out; 13 unions with a prism 17 km long come 117 to 234 cubic millimetres nearer, within the bound before and
after, and the rest are the same to the bit. Of 18 000 more cubes with cracks 0.001 to 0.005 wide, 121 trusted and wrong
are right now and 3 refused are right; 28 right move within the bound, 23 of them nearer, and the rest are the same to
the bit. Of 54 000 differences, common parts and unions of cracked U's and boxes in and about their notches, 1 140
common parts given as nothing where they hold more are reported as not worked out, 2 differences trusted and wrong are
right and 2 common parts trusted and wrong are not worked out; 80 wrong before are wrong by another amount, and the rest
are the same to the bit. None right before is wrong or refused now, and none that holds more is newly given as nothing. A part of a concave
cell whose middle lies outside its material is not read, so a U less a box dipping into its notch is still trusted
and wrong where it was. A
boolean read anew so takes longer: the cracked cube less the box takes 2.7 milliseconds on .NET Framework 4.8 where it
took 2.2, the least of seven runs, and the cube less the prism 2.1 where it took 1.2; one with no cell to read anew, a
box 100 by 60 by 40 less a cube 30 over its corner, takes 0.55 as it did, a plate 1 000 by 500 with 200 holes less a
cope 4.8 seconds where it took 4.7, and a beam less 50 notches in turn 1.0 as it did.

**FIXED.** A boolean, or the closing of a cut, given a tolerance of nought for points ran for ever where what it made
came out open: welding within twice, then four times, nought never ended. Nought welds nothing now, and the body comes
back as made: a difference of two boxes at no tolerance that ran for ever, 3 of 33 pairs, returns.

**FIXED.** The distance from an arc in space to a point is the exact one for a point near the centre of the arc too, as
in the plane. A point whose foot in the arc's plane fell within the point tolerance of the centre was carried to the
arc's start first, which on an arc as small as the tolerance is several times the distance: the half circle of radius
0.001 about the origin in the plan, from (0.001, 0, 0) through (0, 0.001, 0) to (-0.001, 0, 0), was 0.00122 from (0,
0.0007, 0) at a tolerance of a thousandth, and the point off it, where it is 0.0003 and on it; a point of it moved
0.00029 straight in read as much as 0.00171. Of 900 000 random points about arcs of radius 1E-4 to 1E7 sweeping up to a
whole turn, at tolerances of nought, 0.001 and 0.01, the 115 687 whose foot lies within the tolerance of the centre read
a new distance, each one nearer the exact one than it was, but for 108 that moved within the rounding of a centre 1E5
out; 2 789 of them were off the arc and are on it, none the other way, and no point further out reads a bit differently.
`IsPointOn` and `Locate` read the same, and so does everything that asks them, which changes answers about an arc whose
radius is no more than about twice the point tolerance: its crossings with a segment, a ray, a line without ends,
another arc, a circle, an ellipse and the surface of a ball, whether it touches a ball, and `TryTrimTo`. A ball's
distance to such an arc is the exact one, and its shortest line, and the arc's to it, follow the distance and land at
the true contact: the ball of radius 49.99 about (0.0009, 0, 0) is 0.0091 from the lower half of the circle of radius 50
about the origin, from (-50, 0, 0) through (0, -50, 0) to (50, 0, 0), and its line lands on (50, 0, 0), where it was
0.0109 away with a line to (-50, 0, 0). Of 4 000 random arcs of radius 0.2 to 2.2 times the tolerance, at 0.001 and
0.01, asked one question each, 623 answers changed: 60 of 94 segments cross at a second point, the other 34 at another
point, 28 of 43 arcs and 38 of 61 circles at a second point, 1 of each at one where it was two, 37 of 331 balls touch
where they did not and 99 cross at a point where they crossed at none, and 94 trims are made where they were refused;
every point given lies within the tolerance of the arc, read off 20 000 points along it. The projections
`GetClosestPointOnBoundary` and `GetShortestLineTo` to a point keep carrying a point near the centre to the start, so
their line can be longer than the distance, by at most twice the foot's offset from the centre. The distance takes 0.11
microseconds a call on .NET 10 and 0.32 on .NET Framework 4.8, within 4 % of what it did, the least of fifteen runs; a
ball's line takes as long as it did for a centre away from the centre of the arc, and 0.26 microseconds on .NET 10
rather than 0.17 for one near it, which is no longer cut short.

**FIXED.** A whole turn in space keeps every point of its circle, whatever angle it starts at. Where a point falls along
an arc in space is read off the arc laid flat, which was built from the start angle and the start angle plus the sweep;
for a whole turn the two could come back a hair more than a turn apart, and the flat arc wrapped that to a sweep of
nothing, so every point was carried to the start. The whole turn of radius 100 starting at 1.8 radians read the point
half a turn round 200 from it, and off it, at any tolerance, nought too. 875 of the 6 284 starts a thousandth of a
radian apart did so, and 13 721 of 100 000 random ones. The flat arc of a whole turn is a whole turn now. That changes
what leans on where a point falls along an arc in space for a whole turn only: `DistanceTo` a point both ways,
`IsPointOn`, `Locate`, `GetParameterAtPoint`, `GetDistanceAtPoint`, `GetClosestPointOnBoundary`, `GetShortestLineTo` a
point and `TrySplitAt` a point, which left the arc whole, and everything that asks them of a whole turn: its crossings
with a segment, a ray, a line without ends and a plane, with a ball and with a box or a flat region, its collisions and
crossings with an ellipse, a ball's distance, collision and shortest line to it, and the DXF `GetDistAtPoint` and
`GetClosestPointTo` of one. Of the 6 284 counter-clockwise whole turns of radius 100 about (1000, -2000, 500) a
thousandth of a radian apart, at a tolerance of 0.001, 875 crossed a segment through the centre and a plane through it
at one point where they cross at two, read a distance other than nought to a ball of radius 5 on the circle half a turn
round, gave the point 50 above the circle a shortest line longer than 50 and the quarter point a distance along other
than 50π, and none does now; the clash and the near pass read a circle from the angle nought and were right before. Of
800 000 random points about arcs of radius 0.001 to 1E7, centred up to 1E8 out, at tolerances of nought, 0.001 and 0.01,
whole turns, turns within 1E-12 of whole and part turns, 17 897 distances to a whole turn were off by up to twice the
radius, and none is now; 17 900 answers changed, all of them about a whole turn, and every other answer is the same to
the bit. `DistanceTo` takes 0.08 microseconds a call on .NET 10 and 0.30 on .NET Framework 4.8, as it did, the least of
fifteen runs.

**FIXED.** `GeoArc3.Translate` moves the arc it is given. It built the arc again from its start angle and its start angle
plus its sweep, counter-clockwise, as it has since it came in (a64b753, 2026-09-26), so a clockwise arc came back as the
rest of its circle: the arc of radius 100 from 0.5 radians clockwise to -0.5, sweeping 57.3 degrees, moved as the 302.7
degrees from 0.5 counter-clockwise round to -0.5. A whole turn counter-clockwise could wrap to a sweep of nothing, as the
flat arc of one did, and a whole turn clockwise came back counter-clockwise. Only the centre moves now; the normal, the
radius, the start and the sweep are kept to the bit. Of 400 000 random arcs of radius 0.001 to 1E7, centred up to 1E8 out
and moved as far, every clockwise one, 200 036, moved to another curve and none does now; 4 443 of 39 966 whole turns
counter-clockwise moved to another curve, and none does now; 4 405 of 159 998 counter-clockwise part turns moved a last
digit off, their normal normalized again, and none does now.

**FIXED.** Where a curve in space passes within the tolerance of a small arc or circle without crossing it, the touch is
given at the middle of the shortest line between them at any tolerance. The point of the small one at the end of that
line was found at the global point tolerance, whatever tolerance was asked, so a point whose foot fell within a
thousandth of its centre was carried to its start. At a tolerance of a millionth, the ellipse 2 by 1 whose rim passes
4E-7 under the middle (0.0005, 0, 0) of the quarter of the circle of radius 0.0005 about the origin in the plan, from
(0.00035, -0.00035, 0) to (0.00035, 0.00035, 0), touched it at (0.00043, -0.00018, -2E-7), 0.000038 off the arc and
0.000073 off the rim, and touches it at (0.0005, 0, -2E-7); the whole circle was touched 0.00025 off the rim. Of 20 000
random arcs of radius 0.1 to 3 times the global point tolerance, each passed within twice the tolerance by an ellipse, a
circle and a larger arc, at tolerances of 1E-6 to 0.01, 1 993 of 100 000 crossings asked gave a point further than the
tolerance from one of the two curves, all at tolerances under the global one, and none does now. 1 129 more changed, every
point of them within the tolerance of both: 76 give one place more or fewer, the rest the same places moved, 482 of them
at a thousandth and a hundredth. Whether the two collide is the same in all 20 000. What changes is the crossings of an
ellipse with an arc or a circle, of two arcs, an arc and a circle and two circles in space, and with them those of the
surface of a ball with an arc, a circle or an ellipse, where one of the two is no larger than about the global point
tolerance. An ellipse's crossings with such an arc take as long as they did, 7.0 to 7.9 microseconds on .NET 10 and
19.7 to 21.0 on .NET Framework 4.8, the least of fifteen runs.

**FIXED.** An arc cut within a rounding of one of its ends at a tolerance of nought is left whole, as a cut at an end is,
in space and in the plane. The cut was worked out as an angle and each piece built from two angles; one rounding short of
the end the cut landed on the end's own angle, and the piece between them, built from two equal angles, came out a whole
turn. The arc of radius 100 sweeping 1 radian from 0.063, cut at 0.9999999999999999, came apart into the arc and a whole
turn, and the whole turn from 0.238 into two whole turns: of 898 starts seven thousandths of a radian apart, 821 did so
counter-clockwise and 625 clockwise, in space and in the plane alike. A cut a rounding after the start of a whole turn
gave the whole turn first and a piece of 9E-16 radians after it. Of 400 000 random cuts of arcs in space and in the plane
of radius 0.001 to 1E7, part turns, whole turns and turns within 1E-9 of whole, either way round, at parameters within a
few roundings of either end and at random, at tolerances of nought, 0.001 and 0.01, 19 846 came apart into pieces that
did not sweep the arc between them in their shares, all at nought; they are left whole, and every other answer is the
same to the bit. `TrySplitAt` at a parameter, at a point and `TrySplitAtDistance` of `GeoArc3`, and `TrySplitAt` of
`GeoArc2`, follow; nothing inside the library cuts a lone arc so. A cut takes as long as it did, 0.02 to 0.065
microseconds on .NET 10 and 0.05 to 0.09 on .NET Framework 4.8.

**FIXED.** `GeoArc3.FromThreePoints` and `GeoArc2.FromThreePoints` read three points as on one line by the tolerance
they are given, and give no arc that misses them. The points share a line where the middle one lies within the point
tolerance of the line through the other two, a distance, which grows with the chord, or where the triangle they make has
twice an area of no more than the square of the point tolerance. The arc in space read only the cross product, against
the global vector tolerance whatever tolerance was asked, and the arc in the plane only the area, which does not grow
with the chord: at the default tolerance, (0, 7), (5000, 7 + 1.2E-10) and (10 000, 7) were given in the plane the
arc of radius 1E17 that missed its start by 12.5, and (0, 7), (500, 7 + 1.2E-9) and (1000, 7) the arc of radius 1E14 that
missed it by 0.01; both share a line now. (0, 0, 0), (100, 0.01, 0) and (200, 0, 0), the middle 0.01 off the
chord, have the arc of radius 500 000 at a thousandth and share a line at 0.05, in space and in the plane alike; the
chord of 10 000 with the middle 1 off it still has its arc of radius 12 500 000.5 through all three. An arc solved
through three points that would still miss one of them by more than the tolerance and a thousand-millionth of their
size, its radius past the reach of the digits, is refused too. Of 300 000 random triples nearly on one line, twice the
area 1E-12 to 0.1, the chord 0.01 to 1E5, up to 1E5 out, at tolerances of nought, 0.001 and 0.01: at 0.001, 35 100 of
the 45 415 arcs given in the plane and 26 071 of the 36 386 in space are refused now, 10 516 and 7 688 of which missed
one of their points by more than the tolerance; at 0.01, 21 452 of 27 351 in either, 3 600 and 3 679 of which missed; at
nought, where only the rounding can tell, 49 713 in the plane and 13 451 in space that missed by more than a
thousand-millionth of their size, up to 3E7, and in space 15 194 that the global vector tolerance refused are given. No
arc given at 0.001 or 0.01 misses a point by more than the tolerance, where 14 116 in the plane and 11 367 in space did,
and every arc given both before and now is the same to the bit but for its start angle, as the next entry says. Of
100 000 triples whose chord is 10 to 20 000 and twice the area 1E-6 to 1E-4, at the default tolerance, the plane gave
99 983 arcs and space 50 053, missing a point by up to 706 and 67, and both refuse all of them now, the middle within
1E-5 of the chord. Shallow arcs a caller asks for so still come through: `GeoArc3.ProjectToArc2` lays an arc in the
frame's plane whose middle lies within the tolerance of its chord out from its ends and its sweep, as it did one under
the old area. `ToGeoArc2` and `ToGeoArc3` of an AutoCAD arc, which read its ends and its middle, read them at a tolerance
of nought, as AutoCAD drew the arc, and keep it: the arc of radius 1 000 sweeping 0.002 radians, its middle 0.0005 off
its chord of 2, which the global tolerance would read as a line, comes through as it did. Three points take 0.33
microseconds in space on .NET 10 rather than 0.12 and 1.33 on .NET Framework 4.8 rather than 0.51, and 0.11 in the
plane rather than 0.07 and 0.44 rather than 0.23, the arc's points read back against the three; the least of fifteen
runs.

**FIXED.** An arc in space starts at an angle from nought up to a full turn, as its `StartAngle` says, however it was
made, and so does an arc in the plane but for one made from a bulge. An arc moved by `TransformBy`, one through three
points and one built on a circle already known took the angle of its start as it came, which on the lower half of the
circle is below nought: the whole turn of radius 100 from 1 radian, turned 2.5 radians about Z, started at -2.78, and
starts at 3.5; the half circle from (100, 0, 0) through (0, 100, 0) to (-100, 0, 0) started at -1.57, and starts at
4.71. And an angle a rounding below nought, wrapped, came to a whole turn: the arc built from -1E-17 to 1 started at a
whole turn, in space and in the plane, and so did the second piece of the arc from nought clockwise to -1 cut 1E-17 of
the way along; they start at nought now. Of 300 000 random arcs of radius 0.001 to 1E7, centred up to 1E6 out, turned,
mirrored, moved and scaled, through three of their points and from curved edges, 150 265 started below nought, and none
does now; their points moved by no more than 2.2E-15 of their size, the centre's distance from the origin and the radius
together, a rounding, and every other arc is the same to the bit. Of 200 364 arcs in space and in the plane built from
starts a rounding below a whole number of turns, 45 706 started at a whole turn and 36 228 pieces cut from them did, and
none does now; of 199 636 built from random angles none did, and what moved of them is only about an angle a rounding
before the start: 4 557 arcs in space reach such an angle now, 1 252 in the plane read the distance along to it as
nought rather than the whole length, and 445 whole turns in either read the parameter of their own start point as nought
rather than one. What follows is everything that reads an arc made so: `GeoArc3.TransformBy`, `GeoArc3.FromThreePoints`,
`GeoEdge3.ToArc` and so every curved edge and chain in space read as arcs, `PlanarMap.ToArc3`, and the arcs the DXF
reader makes of arcs and hatch edges in space; the DXF writer's chords of a curved chain in space move in their last
digits: on the edge of bulge 0.5 from (10, 0, 0) to (10, 10, 0), the vertex written at a Y of 5.0000000000000036 is
written at 4.999999999999999. `GeoArc2.FromBulge`, and what it builds, still takes the angle of its start as it comes.
A turn takes 0.124 microseconds on .NET 10 rather than 0.120, and 0.51 on .NET Framework 4.8 as it did; an edge read as
an arc 0.083 rather than 0.080, and 0.33 as it did; an arc built from two angles as long as it did.

**FIXED.** The distance from a curved edge in space, a curved chain and a curved loop to a point is the exact one for a
point near the centre of a bend too, as the distance from an arc in space now is. A point whose foot in the plane of a
bend fell within the point tolerance of its centre was carried to the bend's start first, which on a bend as small as
the tolerance is several times the distance: the edge from (0.001, 0, 0) to (-0.001, 0, 0) with a bulge of 1 about Z,
the half circle of radius 0.001 through (0, 0.001, 0), was 0.00122 from (0, 0.0007, 0) at a tolerance of a thousandth,
and so was the chain (0.01, 0, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, 0, 0) with that bend in the middle, and the
point off them, where it is 0.0003 and on them; a point of the edge moved 0.00029 straight in read as much as 0.00171.
Of 300 000 random points about edges, chains and loops whose bend has a radius of 1E-4 to 1E7 and sweeps up to a hair
short of a whole turn, at tolerances of nought, 0.001 and 0.01, the 46 282 whose foot lies within the tolerance of a
bend's centre could read a new distance, and 23 338 do, each one nearer the exact one than it was, but for 5 that moved
within the rounding of a centre 1E5 out; 564 of them were off the shape and are on it, none the other way, and no point
further out reads a bit differently. The distances more than a thousand-millionth of the size of their coordinates off
the exact one went from 7 073 to none on edges, and from 8 497 to 2 313 on chains and loops, every one of those 2 313
nearest a straight edge shorter than the tolerance, which is read as its start point as before; the 69 points that broke
the triangle inequality break it no more. `IsPointOn` and `Locate` of an edge and a chain read the same; a loop's
`Locate` of a point in its plane reads the loop laid flat, as it did, and its `IsPointOn` now agrees with it, where 85
of 59 874 points laid in the plane read on the outline by one and off it by the other. So does everything that asks them
or the distance: the point's `DistanceTo` of them, the loop's `SignedDistanceTo` of a point off its plane, and a ball's
`DistanceTo`, `CollidesWith` and `GetShortestLineTo` to them and theirs to it, whose line follows the distance and lands
at the true contact: the ball of radius 49.99 about (0, -0.0009, 0) is 0.0091 from the edge from (-50, 0, 0) to
(50, 0, 0) with a bulge of 1 about Z, through (0, -50, 0), and from a chain or a loop it bends in, and its line lands on
(0, -50, 0), where it was 0.01 away with a line to (-50, 0, 0). Of 20 000 random bends of radius 0.2 to 2.2 times the
tolerance, at 0.001 and 0.01, asked one question each, 773 points read nearer their distance and 175 of them are on the
edge or chain where they were off, and 624 balls read nearer theirs, 69 touching where they did not; no crossing with a
segment, an arc, a circle or the surface of a ball changed, since those read each bend as its arc already. The
projections `GetClosestPointOnBoundary` and `GetShortestLineTo` to a point keep carrying a point near the centre to the
start, so their line can be longer than the distance, by at most twice the foot's offset from the centre; on a chain or
a loop the start is one candidate, and another edge can still be nearer. The distance takes 0.33 microseconds a call to
an edge and 0.44 to a chain or a loop on .NET 10, and 0.73, 1.02 and 1.04 on .NET Framework 4.8; a ball's line 0.60,
0.80 and 0.81, and 1.38, 1.91 and 1.98; each within 3.1 % of what it did, the least of twenty runs. For a point near a
centre, which is no longer cut short, it takes up to 9 % more.

**FIXED.** A cut or a trim by a point on a curve lands where the distance to the point is measured to, so a point near
the centre of a small bend, which is on it, cuts the bend where it is nearest the point, and a small arc in space is cut
there too. At a tolerance of a thousandth, the chain (0.01, 0, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, 0, 0) with a
bulge of 1 about Z on its middle edge, cut or trimmed by (0, 0.0007, 0), comes apart at (0, 0.001, 0), where it was
refused; the loop (0.01, -0.01, 0), (0.001, 0, 0), (-0.001, 0, 0), (-0.01, -0.01, 0) with that bend second is opened
there, where it was refused; and the half circle itself is cut there, where it was left whole.
`GeoPolylineArc3.TrySplitBy` and `TryTrimTo` and `GeoPolygonArc3.TrySplitBy` of a point read the bend's parameter at no
tolerance, and so does `GeoArc3.TrySplitAt` of a point on the arc; so do the cuts of a chain or a loop by a plane, a
face, a box or a body where they cross a bend near its centre. A point off an arc whose foot falls within the tolerance
of its centre is read as at the centre, which has no direction, and leaves the arc whole, as it did: the half circle of
radius 50 from (50, 0, 0) through (0, 50, 0) to (-50, 0, 0) is not cut by (0.0005, 0.0003, 0), some 50 from it; and a
point exactly at a centre leaves a bend whole. Of the 300 000 points above, 180 cut an arc they lie on where they left it
whole, each at the arc's point nearest them; 154 cut a chain and 154 trim it where they were refused, and 8 cut it and 1
trims it elsewhere; 79 open a loop where they were refused; 43 cuts by a plane through the point changed, each where the
plane crosses a bend within the tolerance of its centre; and no cut, trim or split by a point or a plane further out
changes by a bit. Of the 20 000 random small bends, 147 arcs, 78 chains and 110 loops are cut where they were not, 39
chains are trimmed where they were not, and 72 cuts and trims land elsewhere; 402 of those 446 land at the point of the
shape nearest the point, and the other 44 at a corner or on another edge within the tolerance of it, as a cut within the
tolerance does; 33 of 1 754 cuts of a chain by a plane changed, and none of 1 493 of a loop.

**FIXED.** A segment, a ray or a line without ends that pierces the plane of an arc, a circle, a curved edge or a curved
chain within the point tolerance of the rim without crossing it is given the point of the curve nearest it, as a straight
edge and a triangle give the point of a touch: the segment from (0, 50.0006, 1) to (0, 50.0006, -1) collided with the
circle of radius 50 about the origin and was given no point, and touches it at (0, 50, 0). Of 20 000 segments through the
plane of an arc in space from 1E-7 to 0.002 beside its rim, asked of the arc, its circle and its edge, 4 676 times one
collided and was given nothing, and 1 178 times a ray along one; none does now, and the segment gives the same points
asked from either side. An arc of radius 1E10 to 1E14 a few million from the origin is read as surely. A crossing an
arc, a circle or an edge gave before is the same to the bit. A chain names each place once: two points within the point
tolerance of each other were one place before, and now so are two that the edges either side of a corner give where
both lie within the point tolerance of the corner; of two so taken for one, a crossing is kept over the point of a touch,
and otherwise the first. Of 4 881 segments beside or through the corner of an arc and a straight leg, 3 that were given
two places about 0.001 apart at the corner are given one; places further from a corner are kept however close the two
legs run, as both crossings of a hairpin 0.0009 across, 0.043 apart at 50 from its tip. A point a chain gives where it
only touches stands within the tolerance of both; of 20 000 random pierces no point given before moved further than
the 0.00105 of those 3 merges at the corner, and 2 that touched at a corner just over the tolerance from a crossing are
given both. A
segment that misses is asked whether it touches only where it reaches the tolerance of the
circle's plane and sphere at once: 100 000 random segments against arcs take 36.4 milliseconds on .NET 10, the median
of six runs, where they took 27.9, and as many rays 41.1, where they took 27.2.

**FIXED.** Two ellipses in space are searched along both rims where the one not read is narrow, and an ellipse and a
circle drawing the same curve are answered alike whatever type the circle is given as. The ellipse that comes first is
read at points no further apart along its rim than twice the tolerance, so where the gap between two places the other
passes within the tolerance rises above it over a shorter stretch than that, both lay between two readings and were one:
a rim 30 080 by 18 333 passing 0.000013 from either side of a needle 2.78 by 0.0028, leaning 0.56 radians off its plane,
touched it at one side only, asked either way. Where the other ellipse's rim lies no more than 32 tolerances from its
major axis beside a place found, it is searched too, along 16 tolerances of it about the opposite angle, where its other
side is, beyond the stretch about the place that runs within the tolerance, and a place found there is given unless it
stands within the tolerance of one given already or either rim runs within the tolerance of the other all the way to
one: the two touch at both sides now, 0.0026 apart, and the end of a rim 100 000 by 1, which a copy 0.001 larger in its
plane rings within the tolerance, is still one place. Searched only within four tolerances, an ellipse 21.25 by 0.93, b²
/ a 4.07 tolerances at a tolerance of 0.01, passing a rim 5.26 by 3.59 at two places 0.058 apart, the gap rising 4.4 %
above the tolerance between, was given one, and searched only within eight, an ellipse 23.46 by 1.379, its rim 9.29
tolerances from its major axis beside the touch, passing a rim 10 336.8 by 7 088.3 at two places 0.0755 apart, the gap
rising 4 % above the tolerance between, was too; and searched from inside the stretch about the place found, near the
end of the major axis, a rim 36.66 by 30.95 passing an ellipse 21.88 by 0.908, b² / a 3.77 tolerances, within 0.0020
either side of its end, the two places 0.056 apart and the gap rising 4 % above the tolerance of 0.01 between, was given
one too. Only the other's two sides, and only where it is that narrow, are parted so: two touches the reading of a
narrow ellipse read first makes one stay one, as a needle 0.754 by 0.108 passing a rim 31 536 by 14 839 at two places
0.0357 apart, the gap rising 15 % above the tolerance between at a tolerance of 0.01, is given one place. Of 7 500 rims
drawn across the end of an ellipse whose b² / a is 2 to 24 tolerances, at tolerances of 0.001, 0.01 and 0.05, each
passing it within the tolerance at two places with the gap rising up to 15 % above it between, 27 are given one place,
where 164 were before: 22 read from the ellipse, and 5 read from the rim, the ellipse 14.9 to 21.9 tolerances wide
beside the place and the gap rising under 0.7 % above the tolerance between. An ellipse and a circle read as an ellipse by
`GeoEllipse3.FromCircle`, its two radii one to within 1E-12 of the larger, are the same ellipse where the circle's own
test calls them one circle, the centres and both radii within the point tolerance, whichever way the ellipse's axis
points: the ellipse 100.0007 by 99.9995 and the circle of radius 100 about its centre crossed nowhere asked as a circle
and at four places asked as an ellipse, and cross nowhere either way, as they do with the circle drawn as an ellipse 100
by 99.99999999999999, its radii a rounding apart. Two ellipses neither of which is drawn as a circle are the same
ellipse only with their axes along one line, or with both round within the point tolerance whatever their axes, as
before: one 100.0004 by 99.9998 and one 100.0003 by 99.9999 turned a quarter about one centre cross nowhere, while one
1.04978 by 1.04840, its radii 0.00138 apart, and one 1.04936 by 1.04903 about centres 0.00032 apart still cross at four
places. Curves drawing the same one touch where their rims come within the point tolerance anywhere, not only where
either holds where the other starts: an ellipse 100.0009 by 99.9995 leaning 8E-6 radians, 0.0012 off the circle of
radius 100 at either start and within 0.00042 of it elsewhere, touched nothing, and touches the circle, the circle read
as an ellipse, and an arc of it passing there; where neither start lies on the other the rim is searched, 0.5 to 1.5
milliseconds a call where the starts took 2 to 4 microseconds. Against an independent oracle, the gap sampled along both
curves and every stretch within the tolerance found, linked into touches and its least refined, of 19 500 pairs in 225
corpora (two ellipses, an ellipse and a circle or an arc, two circles, two arcs: grazing, tilted 0.001 to 1 radian,
thin, needles, huge against small, in one plane, copies, near copies, rims running along each other; at tolerances of
0.001, 0.01 and 0.05), 26 answers change and the other 19 474 are the same to the bit. 12 pairs of ellipses are given
more places than they were, each new one 1.08 to 12.2 tolerances from a place given before and the gap between rising
above the tolerance along both curves, by 1.0 % to 66 %, or by 70 tolerances where two thin ellipses cross each other's
sides: 6 of them a touch the oracle finds and the answer missed, the gap rising 12.5 % to 66 % above the tolerance
between, 3 of those a huge rim beside a needle; 3 given places 2.39 to 12.2 tolerances from one given before, the gap
rising 3.5 % to 4.0 %, one of them the two sides of either end of a thin rim 315.68 by 0.646 that a copy 0.01 larger
rings; and 3 given two places 1.08, 1.99 and 1.99 tolerances apart, which the oracle takes for one touch, the gap
between rising 1.0 % and 1.1 %, and across the two thin ellipses 70 tolerances, above the tolerance along both curves,
two touches by the rule that parts runs. 14 near copies drawing the same curve, their rims within 0.036 to 0.998
tolerances of each other, collide where they did not, and still cross nowhere. Asked of a circle and of the circle read
as an ellipse, 18 pairs collided one way and not the other, and none does. Against the oracle of the earlier review, of
1 950 more pairs of ellipses and 348 searched for every region within the tolerance, 4 that collided neither way
collide, 1 touch lost is found, and 5 pairs are given two places within twice the tolerance of each other, the gap
between rising 7 % to 20 % above it along both curves. Pairs of an ellipse and a circle or an arc, of two circles and of
two arcs are answered as before but for those near copies. On .NET 10 and on .NET Framework 4.8, each call the least of
three and each corpus the lesser of two runs, the median call of 135 corpora of two ellipses, an ellipse and a circle,
and an ellipse and an arc is unchanged within 1 % in the middle of them and at most 1.75 times what it was: needles at a
tolerance of 0.01 take 311 microseconds where they took 177 on .NET 10 (189 and 167 on .NET Framework 4.8), and thin
ellipses at 0.05 take 71 where they took 48, and 40.3 where they took 26.8 on .NET Framework 4.8; a near copy of an
ellipse asked whether it touches a circle at that tolerance takes 1.4 times as long on .NET Framework 4.8 (220
microseconds, 158), and pairs in parallel planes, apart in under two microseconds, up to a tenth of a microsecond
longer. Rims drawn across the end of a narrow ellipse take up to 2.25 times as long, 39.9 microseconds where they took
17.7 on .NET Framework 4.8, and 59.7 where they took 37.0 on .NET 10, at a tolerance of 0.001. The slowest calls, about
14 milliseconds for copies and near copies, are as before.

**FIXED.** An ellipse, a circle or an arc in space that passes within the point tolerance of another curve at two places
close together along its rim, the gap rising above the tolerance between, is given both places. The rim was read every
64th of a turn and a piece of it with an end within the tolerance kept whole, so touches that fell in one piece, or in
pieces side by side, were one place: a circle of radius 247 passing a rim 5 995 by 2 863 within 0.018 and 0.017 at two
places 11.4 apart, the gap rising to 0.069 between, at a tolerance of 0.05, was given one, and so was a circle of radius
2.12 crossing a needle 33.5 by 0.00013 twice, 0.141 apart. Such pieces are halved further now, until each is proved
clear, proved within the tolerance all along, or the rim runs no more than twice the tolerance along it, at most 384
readings more a call, and a reading above the tolerance, or a piece proved clear, between two within it parts two runs;
a group that holds one run is answered as before to the bit. Two runs the other curve joins, running within the
tolerance of the rim all the way between, are one place, as read along the other at steps no longer than the tolerance,
and that, rather than both curves passing within the tolerance of the point halfway between, is what makes a place the
search names stand for another, the point halfway asked only where the other cannot tell, an arc's points nearest the
two not both on it: the rim 100 000 by 1 ringed by a copy 0.001 larger in its plane touches it once at each end, as
before, a circle crossing a needle touches it once at each crossing, not at either side of it, the rim 285.55 by 3.38
passing a circle of radius 21.2 at two places 0.348 apart, the gap rising to 1.11 tolerances between, touches it at
both, and so does a needle 19.5 by 0.055 passing an arc of radius 126 within 0.019 and 0.040 at two places 4.3 apart, the
gap rising to 1.17 tolerances between, at a tolerance of 0.05, where the point halfway lay 0.047 off the needle and
0.014 off the arc. A run over the start of a whole turn is one run, the turn known from where it is read rather than
measured: an ellipse 458.57 by 458.56 and a circle nearly on it, read from 2.0169641759523502, where that plus a turn
less itself falls a hair short of a turn, gave a fifth place 143 from the fourth. A place already named stands for a
parted run where it lies on it, read from the rim's point nearest it: the ellipse 313.85 by 4.54 and another 0.001
larger, nearly in its plane, touch at both sides of both ends, four places, where they touched at one end only, and the
rim 67 620 by 6.84 passing one 3.40 by 3.30 at two places 0.167 apart touches it at both. Against an independent
oracle, of 38 900 pairs in 100 corpora (ellipses, circles and arcs grazing, thin, huge, copies, rims running along
each other, needles, near copies and the pairs read in one plane, at tolerances of 0.001, 0.01 and 0.05), 647 are given a
touch they missed, every place within the tolerance of both curves and none on a run already named, 12 lose a second
place on one run, and 12 move within their runs; 7 are given two places where the point halfway made them one, and
the gap between the two rises 0.4 % to 6 % above the tolerance along both curves, so they are two touches. The other 38 222 are answered as before to the bit. On .NET 10, the sum of the mean times
of the 100 corpora is 1.21 times what it was, the median call 1.24 times; copies and rims running along each other take
up to 3.3 times as long at the median (circles running along each other 124 microseconds a call where they took 38,
ellipse copies 174 where they took 69), their slowest as before, and 2 000 ellipses crossed by a circle 13.5
microseconds a call where they took 11.5, ellipses crossed by an ellipse and pairs far apart as before.

**FIXED.** Two ellipses in space read in one plane, and an ellipse read in one plane with a circle or an arc, touch
wherever their rims pass within the point tolerance of each other, and two ellipses give their points in the opposite
order asked the other way. The other curve was laid flat in the plane it is read in with its radii kept, though it may
lean off that plane, and read there alone: an ellipse 1.97 by 0.0023 lying within the planar tolerance of the plane of
one 15 338 by 15 274, which leans 0.02 radians off its own, saw the huge rim 3.1 off its true place where the two pass
0.00016 apart, and touched it neither way round; it touches near (-5595.742, -6793.740, -12510.089) now. At a tolerance
of 0.05, an ellipse 262.6 by 0.041 in the plan saw a circle of radius 12 846, leaning 0.61 radians off it, 2 327 off its
true place where the rims pass 0.0019 apart. A crossing read flat is kept where it lies within the tolerance of both
rims, as before, a touch read flat only where the rims come within the tolerance of each other in space, and the rim is
then searched in space for where the two pass within it, as two rims not in one plane are, a run within the tolerance
one place; the same ellipse, or an ellipse and a circle or an arc with their centres and radii within the tolerance,
still cross nowhere. Against an independent oracle, of 346 pairs of a huge ellipse and a small one read in one plane
108 missed a touch both ways, of 263 more 51, of 146 thin pairs at a tolerance of 0.05 10, of 142 thin ellipses and
circles 13, and of 126 with arcs 1; none does now. A touch read flat was put midway between the rims, within the
tolerance of both where they are up to twice it apart: 16 of those pairs, and 9 more among 1 210 read in one plane,
were given a place where the rims are 1.001 to 1.24 times the tolerance apart, and are not now. Two ellipses read in
one plane were put in order from the one asked first, or both ways from the one lying in the other's plane where only
one does: 96 of the 346 and 94 of the 263 came in the same order both ways, and none does now. Pairs not in one plane
are answered as before to the bit, every one of 6 990; of the 1 210 read in one plane, those that were right are too
but for their order asked from the side not read and for 23: the 9 above, 11 that touch now where the rims come 0.0005
to 0.0009999 apart at a tolerance of 0.001 (0.0499 at 0.05), and 3 touches moved within the tolerance to where the
rims come nearest. Of 171 more pairs read in one plane, one grown by the tolerance and leaning along the other, 47 missed a
touch or a run and 12 were given a place past the tolerance; none is now. The search costs most in one plane, where every rim stands
within the tolerance of the other's plane: on .NET 10, the median of three runs, a call takes 146 microseconds for a
huge ellipse and a small one, where it took 64, 153 (51) where the huge one is a circle, 342 (208) for thin ellipses at a
tolerance of 0.05, 69 (15) for thin ellipses and circles, 66 (44) with arcs, and 65 (12) for a circle hugging an
ellipse; .NET 8 alike. The slowest, thin ellipses running along each other, take 14 milliseconds, as before.

**FIXED.** Two ellipses in space nearly in one plane are read the same whichever is asked first. Read in one plane only
where the first lay within the planar tolerance of the second's, the ellipse 192 by 32 in the plan and one 496 by 46
tilted 3E-6 radians, their rims 0.00027 apart at four places, collided at four places asked one way round and not at
all the other. They are read in one plane now where either lies within the tolerance of the other's: of 4 000 such pairs
tilted 1E-7 to 1E-3 radians, 339 answered by the order, and none does. A pair read in one plane either way, or in
neither, is answered as before to the bit; a pair in neither is asked both ways round whether it is, and 2 000 such pairs
take 4.6 milliseconds on .NET 10, the median of three runs, where they took 3.9.

**FIXED.** Two ellipses in space in no one plane give the same places whichever is asked first, and touch where their
rims pass within the point tolerance of each other without crossing. Read where the rim of the one asked first crossed
the other's plane, kept where the other's rim stood within the tolerance, two rims passing 0.0005 apart square to each
other gave nothing: the ellipse 300 by 100 in the plan about the origin and an ellipse 200 by 100 leaning 0.01 radians,
the end of its major axis 0.0005 above (0, 100, 0) and its rim running square to the first there, collided neither way,
and touch at (0, 100, 0.00025) now; 0.0011 above, they do not. The pair is read from the one that comes first by centre,
radii, normal and axis. Where its rim stands within the tolerance of the other's plane, the rim is searched for where the
gap between the two comes within the tolerance, piece by piece, a piece passed over only where bounds on the gap prove it
further than the tolerance: how fast the rim moves, its height above the other's plane, and how far its foot on that
plane lies from the other's ellipse. Each run of the rim within the tolerance of the other is one place, where the gap
along it is least, at the middle of the shortest line between the rims; two copies running along each other within the
tolerance for a long way touch at one place. Pieces not proved clear are halved until the rim runs no more than twice
the tolerance along them, and halving stops after 384 readings, after which what is left is brought down by Brent's
method, the one part not proved. Which side of the other's ellipse the rim's foot lies on is read from the foot's own
place about that ellipse's centre: an ellipse 93 894 by 93 894 and one 3.3 by 0.0006, passing within 0.00092 of each
other 93 000 from the first's centre, collided neither way, that side lost to rounding, and touch now. Two crossings no
further apart than twice the tolerance are one place, as documented: a thin rim crossing another's plane twice 0.0013
apart was given both. A crossing of the other's plane is kept only where it lies within the tolerance of both rims: the
rim 182 by 0.004 grazing the plane of one 304 by 0.039 was given the middle of its chord there, 0.0084 off its own rim,
asked either way, and is given a place 0.00044 off each now. Of 4 000 pairs drawn to pass within 0.0012 of each
other at tilts of 1E-8 to 1 radian, 134 answered by the order they were asked in and 1 517 came within 0.00098 of each
other and collided neither way; none does now, nor of 8 000 more such, 4 000 tilted 1E-8 to 0.003 radians and 4 000
tilted 0.01 to 1 radian further apart, nor, against an independent oracle, of 1 000 drawn to graze, 1 000 thin rims and
150 copies running along each other, and every place given lies within the tolerance of both rims. The ellipse 3 000 by
1 000 and its copy 0.0012 above, leaning so that its rim is 0.0001 to 0.0023 above, touch at (0, -1 000, 0.00005). A pair
read in one plane is answered as before to the bit, as is a pair in no one plane that gave the same places both ways
round, but where it gave none and now touches, or gave a place off one of the rims; the others move within the
tolerance asked from the side that does not come first. On .NET 10, the median of three runs, 2 000 pairs asked both ways take 1.9 microseconds a call where they meet
nowhere, where they took 0.94, 3.0 nearly in one plane (2.3), and 8.3 passing within 0.0012 of each other (2.1). Of the
pairs not in one plane among 300 copies running along each other, the call takes 112 microseconds on average, 0.84
milliseconds at the 99th hundredth and 0.93 at the most, where it took under 0.01; those in one plane take up to 14.

**FIXED.** Two circles in space give the same places whichever is asked first. Read from the circle asked first alone,
the circle of radius 473.706 about the origin in the plan and the circle of radius 477.624 about (-37.520, -84.167,
0.0009), tilted 4.6E-6 radians off it, crossed at (421.493, -216.198, 0) asked the one way, and at (-442.545, 168.972,
0.001) as well asked the other. Each is read against the other now and a place both readings name is given once, the
first reading that of the circle lying within the planar tolerance of the other's plane or, where both or neither do,
of the one that comes first by centre, radius and normal; asked the other way the points are the same to the bit, in
the opposite order along their line, whichever way the two normals point: the circle of radius 100 about the origin with
its normal up and the circle of radius 90 about (60, 20, 0) with its normal down gave their two crossings in the same
order both ways. In planes parallel within the vector tolerance but further apart than the planar one, which share no
line, the points are put in order along the line square to the one between the centres, as in one plane: the same two
circles in planes 0.005 apart, at a point tolerance of 0.01 and a planar one of 0.0001, came in the order they were
found both ways round, either way the normals pointed, and so did two ellipses. `TryIntersectWith` gives the points `GetIntersections` gives; it read each circle as an arc, in the order
asked, and disagreed with it for 598 and 2 297 of 6 000 pairs. Of 4 000 such pairs tilted 1E-7 to 1E-3 radians, 65 gave
a different number of places by the order they were asked in, and of 6 000 more at tilts up to 1.6 radians, most passing
within 0.0015 of each other, 392; none does now. A pair that gave the same places both ways round is answered as before
to the bit. A ball's crossings with a circle or an arc are read through the circle the ball cuts from the curve's plane,
by this reading, and move with it in their last bits, 5 of 20 000.

**NEW.** `GeoCircle3` and `GeoArc3` are asked about an ellipse in space: `CollidesWith(GeoEllipse3)`,
`GetIntersections(GeoEllipse3)` and `TryIntersectWith(GeoEllipse3)`, each with and without a tolerance, give the answer
and the points, in the same order, that the ellipse gives asked about them. The circle of radius 50 standing upright on the X axis about (250, 0, 0) and the
half of it from (250, 0, 50) round through (300, 0, 0) cross the ellipse 300 by 100 in the plan about the origin at
(300, 0, 0), asked from either side.

**FIXED.** An ellipse in space touches a circle or an arc whose rim passes within the point tolerance of its own without
crossing it on the line their planes share, as two ellipses do. Read only where the one rim crossed the other's plane,
the circle of radius 50 leaning 0.01 radians, the end of its diameter along X 0.0005 above (0, 100, 0) and its rim
running square to the ellipse 300 by 100 in the plan about the origin, collided nowhere, and touches it at
(0, 100, 0.00025) now; 0.0011 above, it does not. The ellipse's rim is searched where it stands within the tolerance of
the other's plane, as against another ellipse. Of 2 000 circles drawn to pass within 0.0012 of an ellipse's rim at tilts
of 1E-8 to 1 radian, 1 056 came within 0.00098 of it and collided nowhere, and 570 of arcs cut from them; of 2 000 more,
1 064 and 548; none does now, and every place given lies within the tolerance of both. Where the ellipse's rim crosses
the other's plane, the crossing is kept only where it lies within the tolerance of both rims, and two no further apart
than twice the tolerance are one, as documented: the ellipse 182 by 0.002 crossing the plane of a circle of radius 303
twice, 0.0011 apart, on the circle both times, was given both. A pair that met before keeps its points to the bit,
but where a crossing lay off the ellipse's rim or within twice the tolerance of another, and where the search finds
places besides: of 400 thin ellipses against circles, 5, and against arcs, 6, one that crossed once given three. In
planes parallel within the vector tolerance, the points come in order along the line square to the one between the
centres. On .NET 10, the median of three runs, 2 000 ellipses and circles take 6.0 microseconds a call where they meet
nowhere, where they took 1.9, and 12 passing within 0.0012 (4.3); with arcs, 6.7 (2.9) and 8.5 (4.9).

**FIXED.** Two arcs in space give the same places whichever is asked first, and two arcs or two circles that
`CollidesWith` reads as touching are given the place. Arcs are read both ways and the places merged, as circles are,
and where the two pass within the point tolerance of each other and no reading names a place there, the middle of the
shortest line between them is given: the arc of radius 100 about the origin in the plan and an arc of radius 50
leaning 0.01 radians, the end of its diameter along X 0.0005 above (0, 100, 0) and running square to the first there,
touch at (0, 100, 0.00025). Read from the arc asked first alone, of 4 000 arcs tilted 1E-7 to 1E-3 radians off each
other 37 gave a different number of places by the order and 356 collided and were given none; of 2 000 pairs of circles
drawn to pass within 0.0012 of each other at any tilt 1 034 collided with no place, and of arcs cut from them 288, with
42 by the order; none does now, nor of 2 000 more. Two arcs of one circle still name no place, and a run of the one within
the tolerance of the other is one place, as for ellipses: the circles of radius 1 000 and 1 000.0005 with their centres
0.0006 apart, within the tolerance of each other most of the way round, are given their two crossings and nothing else.
A pair that gave the same places both ways round is answered as before to the bit. As before, 7 of the 4 000 and 3 of
the 2 000 are given a place where `CollidesWith` reads them 0.0011 to 0.0016 apart, a point within the tolerance of
each; asked one way round before, both ways now. On .NET 10, the median of three runs, 2 000 pairs of arcs asked both
ways take 3.5 microseconds a
call where they meet nowhere, where they took 3.7, and 4.6 passing within 0.0012 (0.67); of circles, 3.3 (4.7) and
8.8 (5.3), and 4.7 (0.69) for the 8 000 above.

The search for rims passing within the tolerance costs most where they run along each other. Of 400 pairs drawn of
each kind, on .NET 10, the median call takes 2 to 80 times what it took: copies of an ellipse running along it 79
microseconds, where they took 1.0, ellipses and circles so 229 (3.7), circles 82 (6.8), and circles grown by the
tolerance and leaning 37 (6.8); pairs drawn to graze each other 16 to 26 (1.9 to 7.3). The search stops after 384
readings, about a millisecond a call.

**FIXED.** Two ellipses that each lie within the planar tolerance of the other's plane give the same points to the last
digit whichever is asked first, read from the one that comes first by centre, radii, normal and axis. Read from the one
asked first, the ellipse 300 by 100 in the plan about the origin and the ellipse 200 by 150 about it, turned a quarter,
gave their four crossings in another order the other way round; of 4 000 pairs drawn to pass within 0.0012 of each
other, the 1 282 read in one plane differed in their last bits by the order, and none does now. A pair asked first from
the one that comes first is answered as before to the bit.

**FIXED.** `GeoArc2.FromBulge`, past a bulge of a half, puts the centre off the middle of the chord by c (1 - b²) / 4b
and gives the radius as c (1 + b²) / 4|b|, each worked out as it stands. The centre, worked out from the radius as the
root of r² - c²/4, cancelled near a half turn: the chord of 200 000 with a bulge of 1 - 1E-8 had its centre on the chord,
0.001 from where it is, and the arc ended 0.002 off its end; it is 5E-12 from it, and the end 1.6E-11. The radius, the
chord over twice the sine of half the sweep, cancelled near a whole turn. Up to a half the two are worked out as before,
to the bit, which keeps the ends of a nearly straight arc on its circle. Every arc built from such a bulge in the plane or
in space, and every curved edge, chain and loop read as arcs, moves in its last bits. Against exact rational arithmetic,
of 20 000 arcs over chords from 0.01 to 10 000 long up to 100 000 from the origin, with bulges from 1E-6 to 1E4, the
10 887 past a half moved: the worst centre came from 8.3E-10 of how far the centre and the radius reach from the origin
to 3.1E-16, the worst end from 1.7E-9 to 7.2E-16, and the worst radius from 1.1E-12 of itself to 3.6E-16; the 316
centres and 1 053 radii that moved further moved by no more than 1.5E-16 and 2.6E-16, the rounding of the numbers. The
ends move too: in a second run, 3 720 of the 9 650 ends past a half landed further from their vertex than before, by
no more than 5.2E-16 of the reach, and the rest nearer or the same.

**FIXED.** A segment ending within the planar tolerance of a plane without crossing it touches it at that end and is
given it, as a triangle or a polygon in the plane gives it: the segment from (-50, 0, 0.5) to (50, 0, 0.0005) collided
with the plane z = 0 and was given no point. One lying in the plane within its planar tolerance at both ends, longer than
the point tolerance, names no place, as before. Of 20 000 segments with an end within 0.002 of a plane, 4 499 collided
and were given nothing, and none is now. The plane asked about the segment or its edge gives the touch too: of 20 000
such segments, 3 371 collided asked from the plane's side and were given no point, and none is now. Straight chains, and
the straight sides of curved ones, take the touch of a side too; where a side's touch and the next side's crossing are
one place, the crossing is kept, as before. A segment the crossing
misses has its ends read against the plane: 200 000 random segments take 3.0 milliseconds on .NET 10, the median of three
runs, where they took 2.4.

**CHANGED.** An edge that bows less off its chord than the centre of its arc can be placed to is its chord: its sag no
more than 2.5 parts in 1E16 of how far the centre and the radius reach from the origin, the rule the distances of the
plane already read it by. `GeoEdge2.IsArc` and `GeoEdge3.IsArc` are false for it, and it measures, cuts and flattens as
the segment, keeping its bulge, and `ToArc` still gives the arc of its bulge to whoever asks for it. The edge from
(0, 0) to (100, 0) with a bulge of 1E-12 put its middle at (50.002, 0), and in space at (50.008, 0, 0); it is at (50, 0).
Near the origin the edges with a bulge under about 1.6E-8 are read so; a bulge of 1E-7 over a chord of 100, bowing 5E-6,
is an arc. Read as its chord, an edge's middle, length and the area it bounds are the chord's, off the true arc's by no
more than its sag, which is less than the rounding of the arc's centre: of edges near the threshold the worst middle stood
7.34E-5 off the true arc's, the edge 9 609.5 long with a bulge of -1.53E-8, and the most any stood further off than its
arc put it before was 4.9E-5; a square 8 614 across, 74 million square units, came out 0.334 short. Split, its pieces are
straight, their bulge nought;
`HasArcs` of `GeoPolylineArc2` and `GeoPolygonArc2`, and the count of curved edges the chains and loops in space print,
leave it out; `DxfWriter` writes an edge in space so read as its chord, with no bulge. The reading follows how far the
edge reaches from the origin along the axes, so moving or turning an edge close to the threshold can change it. `IsArc`
settles nearly every edge by sums along the axes, without placing its arc, in 1.4 nanoseconds on .NET 10, the median of
three runs of 20 million, where it compared the bulge with nought in 0.44; the area and the distances of a loop of 4 000
curved edges take 4 to 9 % longer for it.

**FIXED.** `Arranger.Run` reads the tolerance of its options throughout, never `Tolerance.Global` as it stands at the
time: clearances up to the options' point tolerance count as nought when the most open of the free places is chosen,
and the direction of a leader, the region a placed label leaves for the second pass and a block line shorter than the
tolerance are read by it too. A label 0.0004 clear of a line above it and 0.0008 clear of one below, run at a tolerance
of a ten-thousandth with the global one at a thousandth, went above, both clearances counted as nought by the global
tolerance; it goes below, the more open place. Where the options' tolerance is the global one, as `ArrangeOptions` makes
it unless set, this alone moves no label.

**CHANGED.** Where a label's free places are as open to within a thousandth of the point tolerance of the options, a
millionth at the default, the place tried first wins, so which of two places as open a label takes no longer depends on
where the scene lies: of two places 4.0000000000000036 and 4.0000000000004521 clear, tried in that order, the second won
by its last digits, and the first wins; so does the first of places 0.5 and 0.50000001 clear, at the origin as a million
off it. The place taken moved in 74 decisions of the golden scenes, 20 in the scenes of 6.2.0 and 54 in the clusters of
3a2da08, whose hashes were taken again, every one between clearances within 1.82E-12 of each other; a scene of
clearances of 0.0002 at a ten-thousandth now places alike wherever it lies. The candidates a label is tried at are
worked out from its coordinates as before, and a scene moved far off can still give a label a candidate more or fewer.
The approved signatures of the benchmarks stay as they were. As fast as before.

**FIXED.** A bar checked by its centre line (`ClashBar`) that lies along a face of a part, within the tolerance of it,
runs inside the part for as long as it lies there, half the bar being in the material: a bar of radius 8 and 800 long
laid on the top of a box 1 000 by 200 by 100, half its length over it, was found the radius deep with nought of its
centre line inside, and is 400 inside. Its depth and its place are as before. One drifting off the face runs inside only
until it leaves the tolerance: a bar over that box from 0.0012 over the top to 0.0006 over it across 1 500 is 700
inside, from where it comes within a thousandth of the top to the end of the box. A bar dipping into a plate from within
the tolerance over its top counts the run within the tolerance too: 400 for one 400 long dipping at half a degree from
0.0004 over, where it counted 399.954 from where it crosses the top. A bar laid along an edge of a part counts so too, a
quarter of it in the material. Of 1 198 clashes of 1 760 bars with 96 parts, the 400 of bars laid along a face of their
part 0, 0.0003 or 0.0008 off it moved, each in its length inside alone, which on the parts without openings is the bar
clipped to the face to within 8E-13; none of the others moved, those 0.002 off among them. As fast as before.

**FIXED.** A bar checked by its centre line (`ClashBar`) counts in `LengthInside` every stretch of its centre line
within the tolerance of the part's surface, and none off it, however often it leaves the tolerance and comes back. The
stretches are found triangle by triangle: the gap from a point moving along a line to a triangle shrinks to where it is
least and then grows, so the stretch within the tolerance of each triangle is one run, its ends found by halving, and
the runs put together are where the centre line is within the tolerance; a stretch between them lying in the material
deeper than the tolerance counts too. A piece of the centre line whose middle stood off the part counted none of its
run within the tolerance, and one whose middle lay on the surface counted from its middle as far as its ends lay within
the tolerance, whatever lay between: a bar from 0.0007 over the top of a box 250 before its edge to 0.0015 over it 1 000
along read nought and is 125 inside; one dipping 0.0005 into the side of a box between two places 0.003 off it 200
apart read 28.57 and is 85.71; one rising 1E-5 for each unit from 0.0006 over a top that steps up 0.004 further on,
within the tolerance for 40 over the top and 100 over the step, read nought and is 140; one along the flange of an angle
from 0.0017 to 0.0007 over it, through its leg, read 318.67, the stretch off the tolerance between the leg and where it
comes within it counted, and is 188.68. A run no longer than ten times the tolerance at an end of a piece whose middle
stands off the part is left out, so a bar crossing a face at a slant steeper than about 6 degrees is measured to the
crossing as before: one square through a plate 10 thick is 10 inside, not 10.002. Against an oracle of the signed
distance, sampled every 0.002: of 24 000 bars drawn along and across the faces of a box, a comb, a plate with two slots
and an angle, at the origin, a million off and turned, 157 counted more than the length within the tolerance by more
than 0.02, by up to 270, and 4 378 less; none now counts more and one less, by 0.0207, the runs beside two slanting
crossings left out. Of 6 000 bars over four parts, 2 058 counted less by more than 0.02, by up to 225; one does, by
0.0201. Depth and kind are as they were for all 30 000, and a bar not within the tolerance of the surface anywhere is
as it was to the bit. The benchmarks of bars are as fast as before; the 6 000 bars, two in three of them grazing a face,
take 1.7 times as long on .NET 10, each triangle near a piece of centre line searched.

**FIXED.** A ray meeting a triangle, a polygon or a face is read as the line without ends along it is, by how far that
line climbs across the shape, and given what the line meets at the ray's origin or ahead of it. A ray starting within
the planar tolerance of the shape's plane and level with it within the vector tolerance was read by the part of it over
the shape and given nothing where its line crosses: the ray from 0.0005 over a square 100 by 100, falling 1E-5 for each
unit along it, climbs 0.001 across the square and is given its crossing 50 along, as its line is. One that climbs less
lies level over the shape and is given nothing, as its line is: the ray from 0.0002 over the square falling 4E-6, and
those a hundred-millionth or so off the plane falling 1E-10, however near they start. Where the line meets the shape
only behind the origin, or lies level over it only behind a ray running away from it, a ray whose origin touches the
shape is given its origin, as before. Of 20 000 rays drawn over a triangle, the 954 that crossed it level within the
vector tolerance and got nothing where a segment 3 000 along them got the crossing, 923 get it, and 31, whose line climbs
no more than the planar tolerance across the triangle, lie level over it as their line does. Of 120 000 rays drawn near
level over a triangle, a square and a face with a hole, in the floor and turned and moved out, 1 575 answers moved:
1 527 to agree with the segment 3 000 along the ray, 746 of them level within the vector tolerance and 781 steeper rays
starting within the planar tolerance that were given nothing where their origin touches the shape or they cross it, and
48 to give nothing where their line meets nothing and the segment gives a point. Of 20 000 more rays drawn near level
over the three shapes and 40 000 over a square, none is given a point its line is not, but its origin as above. A ray
takes about 1.5 times as long to meet a triangle, a polygon or a face, read both as a ray and as its line.

**FIXED.** `TryIntersectWith` of a ray against a triangle, a polygon or a face gives the first point `GetIntersections`
gives, and false where that gives none. Since the ray is read as its line the two disagreed: the ray from (10, 50, 0.0002)
over the square 100 by 100, falling 4E-6 for each unit along it, lies level over the square and is given nothing by the
list, but `TryIntersectWith` gave its crossing of the floor at (60, 50, 0); so did the ray from 0.00005 over it falling
1E-6 and the one from 1E-7 over a rectangle 5 000 by 100 falling 1E-10. Each is refused now, and a ray passing the rim at
a slant within the tolerance, given the touch by the list, is given it by `TryIntersectWith` too. Of 20 000 rays drawn
level, slanting, steep and near the rims of a triangle, a square and a face with a hole, turned and moved up to a million
off, 3 371 of the 60 000 answers were true with no point listed, 1 380 false with one, and 14 gave a point further than
the tolerance from the one listed; none is now, and `GetIntersections` is as it was to the bit. A segment and a line
without ends are crossed by `TryIntersectWith` where they cross the plane within the shape, as before; the list gives
that crossing too, and a touch besides (4 834 of the same 60 000 answers), and the hierarchies are held to the plain scan
of that crossing. `TryIntersectWith` of a ray costs what `GetIntersections` does, about ten times the plane crossing it
was: 100 000 calls take 80 to 140 ms on .NET 10 where they took 7 to 10. Code that counts the triangles a ray crosses
with `TryIntersectWith` now counts the rims it touches within the tolerance as well.

**CHANGED.** A bar checked by its centre line (`ClashBar`) that runs at a slant past a large mesh no longer gathers every
triangle in the box of its centre line, only those whose boxes it passes within the tolerance of. Fifty bars running at a
slant past a cylinder of radius 500 and 10 000 sides take 370 to 790 ms on .NET 10 where they took 960 to 1 640, and
fifty that pass it 10 to 20 off take 140 to 360 ms where they took 800 to 1 060; every answer is the same to the bit, as
are those of 48 000 bars along and across a box, a comb, a slotted plate and an angle.

**FIXED.** The copy of Clipper2 kept in the library builds a polytree whose paths split off one another in a ring: it
looked for the owner of a path among those splits by recursion, walked a list it was already walking again from its
start, and overflowed the stack, which ends the process. The splits are walked with a stack of their own now, and a list
already being walked is passed over. Of the 1 300 cases of three seeds of the clip fuzz, the three that overflowed
build their trees, and every other gives the same paths to the bit.

**CHANGED.** `ArrangeItem.GetPlacePoints` and `Arranger.Run` try a label at the same places, moved, wherever the scene
lies. The slides along a leader were stepped off by adding a step to the last, up to the reach of the label, so whether
the last slide, at the reach itself, was tried came and went with the rounding of the sum, and of where the scene lay:
in 236 of 1 618 tries of a label of the golden scenes moved 900 or 100 000 along X its count of places changed, a label
on a leader 60 long having 234 places at the origin and 246 900 along. The slides are now counted,
twenty each way where a step is a twentieth of the reach, the last at the reach itself, and the label is measured from
the middle of its leader rather than from the origin. A label tried at 234 places is tried at 246, the two places of
each row at the full reach added at the end of that row; no place is taken away, and every other stands where it stood
to within 1.5E-11. Of the golden scenes, 60 of the 192 candidate lists of 6.2.0 and 1 533 of the 6 129 lists of the
scenes run gained their last slide; the results of 6.2.0 moved in their last digits only, by 1.82E-12 at most, and nine
labels of the clusters of 3a2da08 moved: three to a place at the full reach, one whose first three free places now hold
such a place and end before the one it took, and five after a label that moved. Their hashes were taken again. Of the
benchmarks, `Arrange.Dense` moved six labels by 2.8E-14 at most, the last digits of a slide counted rather than added
up, and `Arrange.StaticBlocks` two labels, each to a place at the full reach that it had lost, one from (-115, 0) to
(-35, 94) and one from (78.85, 11) to (83, 11); their signatures were approved again. A reach within the point
tolerance of 2 counts as 2 in choosing between steps of a twentieth of it and steps of the label's height: a label 1 by
0.4 on a leader 2.5 long, turned to it, reaches 2 each way and had 246 places at the origin and 66 moved by (196 017.7,
18 214.2), the reach a hair under 2 there; it has 246 at both. Moved by 900, 100 000 or a million along X or both ways,
the golden scenes place every label alike, where 10 of their 41 runs placed up to six labels differently. As fast as
before, within the noise of the benchmarks.

**FIXED.** `Arranger.Run` and `ArrangeItem.GetPlacePoints` arrange a scene moved elsewhere as they arrange it where it
was, moved, also where it stands on an edge the last digits of its coordinates settled. Of 25 678 labels in 374 runs,
the scenes of the tests and the benchmarks and sheets built to stand on such edges, each run moved 1 000, 100 000, a
million and ten million along X, along Y, both ways and by an uneven step, 863 to 977 of the 102 712 placings a
magnitude came out differently, 670 to 741 of them with a different count of places; none do now. Three edges were
left:

- A label as large as `ArrangeOptions.MinimumBoxSize`, measured along its leader and across it from its rounded
  corners, came to a hair less at some places and was left where it stood, with no places at all. Of 200 000 labels 20,
  25 or 30 by 10 at the default 10, on leaders 20 to 119 long at a random angle a third of the time and along an axis
  otherwise, half of them turned to their leader and half not, 6 606 had none near the origin, and 8 181 of as many
  drawn alike up to ten million off it; none have now. It counts as large enough to within the point tolerance.
- Two labels that touch. A place was judged against a label already placed, and the final layout judged two labels,
  only where the boxes around them overlapped, so that two labels touching collided or not as the rounding of where the
  scene lay left their boxes overlapping or a hair apart. They collide wherever the scene lies now, as the next entry
  says. Of the 400 labels of the benchmark `Arrange.Dense`, the one that took candidate 57 of its list, (23.1, -8), at
  the origin, touching a label placed before it 2.8E-14 away, and candidate 61 moved 1 000 along X, takes 61,
  (24.75, -8), at both; no other label of the benchmarks moved, and its signature was approved again.
- Two labels as far from the centre of them all, or as far along with `PlaceMostConstrainedFirst` and
  `PlaceFromInsideOut` off, went in an order the last digits of the centre or of their coordinates decided: of two
  labels on leaders 10.6 apart, kept to the sides facing each other, whose first places are one and the same, centred
  at (15, 5.3), the second went first at a third of 100 places they were moved to. Where a label stands is read from
  the first label now, and counted, as how far it stands from the centre, in whole steps of the point tolerance; labels
  on the same step keep the order they were given in.

What falls within rounding of an edge drawn exactly can still go either way: two lengths within rounding of half a step,
and a block within rounding of the edge of a label's reach. A label 20 by 10 on a leader from (0, 0) to (100, 0), 5 off
it, with a block line from (185, -5) to (185, 5), goes to (-3.25, 10) at the origin and to (0, 10) at 2 of 20 000 places
a thousand to ten million off it, where 15 went elsewhere before. The golden hashes of the tests did not move. As fast
as before: in 16 runs of each, taken in turn on .NET Framework 4.8, `Arrange.Dense` took 220.6 ms at the median and
takes 221.8.

**CHANGED.** `Arranger.Run` judges two labels as it judges a label and a block, within the point tolerance of the
options: a place that comes closer than the tolerance to a label already placed collides with it, and the final layout
reports two labels that close not `Placed`. Two labels were judged on their boxes as they stood, and two standing less
than the tolerance apart were both placed. A row of 12 labels 24 by 10 kept above leaders laid end to end, each leader
24.0005 long, `LookAheadCandidates` 1, at the default thousandth: the labels 0.0005 apart all took their first places,
and one does now, the others sliding on. With leaders as long as the labels, the labels touching, one or two did, as
the scene lay: for labels 24.3, 17.7 and 33.1 wide, 12 of 16 moves of a thousand to ten million placed them
differently, and one does now, wherever the scene lies. With leaders 0.002 longer than the labels, all 12 still do.

**FIXED.** `Offset2.Offset` of a `GeoPolygonArc2` drops a spike the loop runs out to and straight back from, between two
straight edges whose far ends lie within the point tolerance of each other, as the straight path drops it, the two ends
of its foot one corner: the triangle (0, 0), (10, 0), (5, 10) with a needle from its top to (5, 20) and back to
(5.0004, 10), grown by 0.3 with miters, is the triangle grown, 60.179, where it was 67.757. Offset as
drawn, the square 10 by 10 with a needle 0.0005 wide at its foot and 6 tall on its top, grown by 0.3 at a tolerance of
0.001, kept a strip 0.6 wide about the needle, 115.85 with round joins, 117.58 mitered and 115.90 chamfered, and the
same square with a notch 0.0008 wide and 0.01 deep in its bottom grew a miter spike, 113.63; both are the square grown
now, 112.283, 112.360 and 112.298, as the straight path gives them. A spike whose sides are arcs is offset as drawn, and
a needle wider than the tolerance is kept: within 1E-4 the same square grows round its needle, 115.845, as before. Of
the 23 offset repros of the audit only the needle and a notch 0.001 wide move; of 7 344 offsets of stars at each of the tolerances 0.1, 0.001
and 0.0001, 10 800 of loops a long edge into a short arc, 72 000 of random loops, 10 800 of dense loops but 33 at 0.1,
and 2 195 of loops of 50 to 130 corners, none moves. Of 3 024 of loops with rows of teeth 0.0005 to 0.2 wide, 1 118
move: each is the loop without its teeth narrower than the tolerance, nearer the straight path or as near but for 3
within 0.006 where the straight path cuts an arc, and 168 are right now by distance from that loop where they were not.
As fast as before, within the noise, on .NET 10.

**FIXED.** `Offset2.Offset` of a `GeoPolygonArc2` at a tolerance coarse beside the loop no longer comes back empty where
two moved pieces cross within the tolerance of where one of them ends. Such a crossing is not cut, each piece runs on past
it by up to the tolerance, and the runs either side could end further apart than the tolerance, or a stub the slack lets
stand carried one past the next, so that the loop never closed. A loop whose offset leaves a run open is offset again
within a hundredth of the tolerance, and where a run is still open within a ten-thousandth: at each step cut at its
crossings wherever it crosses itself there, whether or not it was cut within the tolerance, and as a simple loop only
where it crosses itself nowhere there. An answer is taken where it leaves fewer runs open and no fewer loops, with any
loop of it finer than the tolerance, as the answer within the finer step has it. At a tolerance of 0.1: the loop of
twelve straight corners in a box 7.9 by 5.1 grown by 4 is one loop of 150.317 with round joins and 186.801 with miters
(the straight path 150.320 and 186.801); a loop of ten corners with a spike 12 degrees wide, shrunk by 0.3, one of 9.453
round and 9.435 chamfered; a star of sixteen corners shrunk by 1 one of 4.779 round and 4.461 chamfered; a comb of three
teeth grown by 0.3 one of 62.296 mitered and 62.158 chamfered; a loop of eight corners 2.62 by 2.37 with its arcs, cut
at its three crossings, grown by 0.3 one of 4.886 with a hole of 0.0024 in it, as within 1E-3; a loop of eight corners
1.91 by 2.01 that crosses itself within 0.1 of a corner, which reads as a touch within 0.1, grown by 0.3 one of 4.102,
as within 1E-3; and the loop from (-8, 0) to the origin, round an arc of bulge -0.44058 to (-0.0036, 0.0035) and on to
(2.633, 6.989), grown by 0.3 one of 36.680 round, 37.029 mitered and 36.734 chamfered, as within 1E-3. Each was empty.

**FIXED.** A curved loop that crosses itself at places nearer each other along an edge than the tolerance is read
even-odd. Its crossings were made one place only where each lay within the tolerance of the first found there, so three
edges crossing in a triangle a tolerance across made two places 0.101 apart, an edge was to be cut twice within the
tolerance, and the loop was offset as if it did not cross itself. Crossings within the tolerance of each other, or linked
through others that are, are now one place, as long as every crossing of it lies within the tolerance of the place, where
every edge through it is cut: a row of forty crossings 0.09 apart is twenty places of two, not one place 3.5 long. Where
two places still lie too near along an edge to cut it at both, the loop is cut within a hundredth of the tolerance, or
failing that a ten-thousandth, and offset within that: a loop of fifteen corners 1.2 across, whose crossings cannot be cut
within 0.1 or 1E-3, is cut within 1E-5 and grown by 1 at a tolerance of 0.1 is one loop of 7.858, as within 1E-5. A loop
that cannot be cut at any of the three is offset as a simple loop, as it was before; at a tolerance of 0.1 none of 2 991
loops of 8 to 27 corners that cross themselves, nor of 611 stars, came to that, 2 088 and 220 of them being cut at a finer
step. The loop of eight corners whose crossings lie 0.070, 0.084 and 0.101 apart, at a tolerance of 0.1, is now as
within 1E-3: grown by 0.3 an
outline of 181.68 and three holes of 11.46, 8.09 and 1.58, shrunk by 0.3 the six loops it pinches into, 46.19 to 0.54
(the straight path 46.15 to 0.54), grown by 1 an outline of 231.08 and holes of 2.78 and 1.40, shrunk by 1 four loops,
29.67 to 0.70, grown by 3 one of 383.16 and shrunk by 3 one of 2.22. It was nothing grown by 0.3, 1 or 3, two loops of
8.09 and 1.58 shrunk by 0.3, one of 1.40 shrunk by 1 and nothing shrunk by 3. The star {37/18} of radius 16.670 shrunk by
0.3 is its 37 loops of 0.824, as the straight path gives it; it was nothing. A loop that crosses itself at more than
2 000 places within the tolerance is read within the tolerance only, cut where it can be and otherwise offset as a simple
loop, as before: where the crossings bunch, the moved pieces of a loop cut at them cross each other some square of their
number of times, and 60 diameters of a circle, 6 903 crossings, cut within 1E-5 took 21 s and 1.2 GB so. The star
{71/35} of radius 10, 2 414 crossings, grown by 0.3 at a tolerance of 0.1 is the one loop of 302.631 round it, as before;
200 diameters, whose 19 900 crossings at the centre are one place, are offset as a simple loop past the cap, as before:
1.8 s within 1E-3, as before. At 0.1 their 79 003 crossings
are cut, and the one loop of 333.80 takes 6.5 s where it took 0.7.

**FIXED.** A miter whose piece before it, or after it, was cut back by the next corner to no longer than the tolerance, or
a hair past its own end, is run on along the whole edge that piece was moved from, where the piece itself gives no
meeting. The tip of 22.6 degrees at (3.416, -7.019) of a loop that crosses itself, grown by 0.3 at 1E-3, is mitered 1.546
out, 54.770 as at 1E-4, where the piece before it, cut to 1.94E-4 and turned backwards, had it rounded off at 54.441; a
chain offset at a tolerance of 0.1 now miters such a corner as it does within 1E-3. A piece turned backwards that ends a
hair past the meeting runs back to it, and not on round nearly a whole turn of its circle.

Of 139 404 curved offsets of a fuzz corpus at tolerances of 0.1, 1E-3 and 1E-4, the fixes move 2 381, most
of them at 0.1, and the rest are the same to the bit; 261 come back that were empty, and none is empty that was not.
Read against the distance from the source and the miter wedges built from the source alone, a point counted wrong only
outside a band twice the tolerance wide about the answer, of the 2 238 fuzz loops among them 425 come nearer the exact
answer and none goes further, the rest the same within the reading; before the last of these fixes the 23 audit cases
and chains came nearer too, the loops to that reading's answer and the chains to the miter they have within 1E-3. 96 offsets within 1E-3 of two loops
whose corner dips 0.0002 and 0.0005 through an edge come back as the same loops in another order. Of 11 964 offsets at
0.1 of loops of 8 to 27 corners that cross themselves, and 2 444 of stars, read against the distance alone, 4 114 and
165 come nearer and none goes further.

The finer steps cost time where a loop crosses itself, the cut loop being offset where the simple one was before. At a
tolerance of 0.1 the 8 448 offsets of one fuzz seed take 1.18 to 1.24 s on .NET 10 where they took 1.06 to 1.17, and
2.31 to 2.34 s on .NET Framework 4.8 where they took 2.04 to 2.23 (another reading gave 1.29 to 1.32 s where 1.07 to
1.16, 11 to 23 % longer); the 2 444 offsets of stars take 77 to 80 s where they took 8.2 to 9.9, and the 4 500 of loops
of 8 to 27 corners 15.7 to 16.8 s where they took 2.8 to 3.2. Most of it goes to stars of 100 to 500 crossings, up to
2 s for four offsets each; the star {63/31} of radius 10, 1 890 crossings, just under the most that are cut finer, takes
2.2 s grown by 0.3 and 4.6 s shrunk, where it took 52 and 7 ms with the same answers. Within 1E-3 and 1E-4 the fuzz takes
as long as before within the noise, and so does a gear of 3 200 teeth, 3.7 to 4.2 s on .NET 10 and 7.2 to 8.0 s on 4.8.

**FIXED.** `GeoSpline2.Flatten` and `GeoSpline3.Flatten` of a spline no longer than the point tolerance give the chain of its
corners as they are, where a chain would merge them into one point and the call threw: the part 0.000245 long that `Split(1E-6)`
cuts off the fit through (0, 0), (30, 20), (60, -10), (100, 0) flattens to the chord from its start to its end. `Split` keeps such a
part, exactly, as it did; `Flatten` throws now only where every corner it cuts at is one point to the bit, a spline that stands still
or a closed one that the chord tolerance leaves cut at its ends alone.

**FIXED.** A touch of a spline far from the origin is given no points on the slope beside it. A meeting of a spline with a shape in
the plane that is one only to the snap (1E-13 of the size of the numbers), no crossing and further off than the numbers are evaluated to
(sixteen units in their last place), which lies within ten times the tolerance of a meeting to the digits and is no least of the
distance, is left out: a quadratic sitting on the circle spline of radius 100 a million from the origin, 2.3E-8 off it 0.002 either side
of the touch, was given three places where the `GeoCircle2` gives one, and is given one, from either side, in the plane and in a tilted
plane of space; bows 600 across sitting on that circle spline 100 000 to 3 000 000 out, three, and are given one. A meeting
to the digits is kept whatever lies beside it, so crossings a few snaps deep are as many places as before: bows 600 across rising 0.05 to
5, cut by a segment spline 1E-8 above their lowest point 100 000 out, 3E-8 a million out and 3E-7 three million out, in chords of 0.15 to
0.47, are crossed twice, as they were (shallower chords far out still give one or three, the rounding's places, as before); and the spline
of the arc of radius 100 about (1 000 000, 1 000 000) turning 0.1 against its own `Flatten(1E-7)`, 2 049 corners, gives 4 097 places, as
it did, and turning 2 against `Flatten(2E-8)`, 65 537 corners, 131 073 in 0.95 s on .NET 10, as it did in 0.89. Ten million out the points beside the touch are 4E-9 off, under what the numbers are evaluated
to, and are still given (not fixed yet), as are the five places of a line tangent to a circle spline of radius 1 000 a million out cut into
10 000 segments, and the four of two circle splines crossing at the joins of their spans.

**FIXED.** A spline of a flat arc and an arc of nearly its circle lie along each other where they are one circle to the rule however
far the reading of the spline's circle may be off: their centres and radii within the tolerance of each other with the doubt of that
reading counted twice. The spline of an arc of radius 849 821 and the arc of that circle moved 0.00011 and 0.0002 smaller were given
the two ends of their overlap as places, where the arcs give none. Of 1 200 random pairs of arcs of radius 100 to 100 000 000 moved and
grown less than the tolerance, 517 answered otherwise as splines than as arcs and 307 do, and with their centres within 100 000 of the
origin 603 and 384; those left are splines whose circle the digits of their control points do not hold to the tolerance (not fixed yet).

**FIXED.** Two splines that lie along each other give the same places whichever is asked and however they are turned. The stretch
along them now runs past the end of either, within the tolerance of it, as an arc runs along an arc of its circle, where a copy that
overhung the other's end ended it short; a point square from the end of the other is no least where the distance falls on into the
other's inside, where it was read as the distance to that end; a crossing found inside the stretch stays one when a meeting found beside
it is a few units of rounding nearer, where it was lost; and the offset of one from the other is read lobe by lobe between its changes
of sign: a lobe that goes no further than a hundredth of the tolerance is taken with its neighbours, and between two lobes that go further,
one to either side, the curves cross once, at the change nearest the middle of the small lobes between them, or the next nearest where
that one is a jump of the offset at a corner of the other curve or a crossing so shallow that the two run along each other to the snap,
and failing every change where Newton's method from either side of it settles on a meeting to the hundredth of the snap, the one nearer
the change, however long the two run along each other round it, and never where it stalls short of that: two segments a million out that
cross at a slope of 2.5E-7 cross once, 0.00006 off their crossing, where they were given none; between two that go further to the same
side not at all. A sample of the offset that is nought exactly lies across a change and hides none. A change of sign inside the stretch was a crossing wherever the stretch swung
further elsewhere, which gave the fit through 37 points of a quarter circle 68 crossings of an ellipse 1E-9 off that circle once crossings
were kept as above; it gives at most 37. A cubic whose offset from a line goes from 1.1E-4 under it to 4.4E-4 over it, crossing it at 20,
40 and 60 with lobes of 7E-6 between, crosses it once in the plane, at 40 at the origin and at 60 a million out, where the crossing at 40
runs along the line within the snap, 1E-7 for two splines there, whichever is asked and however turned; it was given 3, the exact count, and in a tilted plane of space,
where the pair is measured in space and no lobes are read, it is given 3 still. A
zigzag of degree one and its copy moved 2.6E-5 along it cross at each corner where the edges are 1.8E-5 off each other, and once across
two shallow edges 8.7E-7 off each other, where the exact polylines cross three times; of 400 random polylines that never turn back and
their copies moved 2.6E-5, 216 give the exact count where 201 did, and 314 its parity where 270 did, and of 400 more moved 5E-4, 388
and 395 where 266 and 299 did, none of them answering otherwise asked the other way or turned. Polylines that turn back on themselves
pay where a first or last lobe goes no further than a hundredth of the tolerance, which is no crossing by the rule: of 400 at the origin
moved 2.6E-5, 46 give the exact count where 51 did and 285 its parity where 293 did, and none answers otherwise asked the other way or
turned, where 36 did. Of 60 random splines and their copies
moved 0.0001 to 0.0009, 5 gave a place more or fewer turned by 0.7 of a radian and none does, and of 200 more 18 and 4. Of 120 copies
moved 0.0001 to 0.0021, asked three ways (either first, and turned) at two tolerances, 166 places are found that were lost, every one a
crossing, a point of both curves to 1E-14; 24 ends and near touches that were no least of the distance are given no more; and 7
crossings are given no more: a pair 0.011 of the curve apart whose offset swings less than a hundredth of the tolerance between them,
asked three ways, as the rule for a stretch along has it, and one where the copy meets the curve's other branch beside a crossing of the
curve with itself, inside a stretch along, which the other two ways missed before as well (not fixed yet).

What moved: of 60 480 answers of random splines against every shape of the plane and 37 440 against every shape of space, at two
or three tolerances (the draws of the symmetry fuzz tests, eight times over), none, to the bit. The touches far out, the spline arcs that are one circle with
an arc, and splines with their near copies, as above. The space reading of a curve and a copy moved out of every plane, a place at each
least of their distance, is as it was: a curve and its copy moved 0.0005 in its plane give no place, and a curve that is not flat and its
copy moved 0.0005 give 14 (not fixed yet). The draw of a fuzz run that once found a spline on one line 5.8E-6 too far from an ellipse 709 across,
fixed before, is a test of its own. 28 new tests.

**FIXED.** Two splines that lie on each other exactly for a stretch between two lobes of their offset cross once, where they come on
each other, however the pair is turned. A polyline over a line by 0.0003 to 10, on it from 10 to 50 and under it after, was given its
crossing wherever the bisection of a change of sign or Newton's method settled in the rounding of the overlap: at 10 as it lay, at 34.95
turned by 0.3 of a radian, at 30.18 turned by 0.7 with the polyline reversed; and a part of the curve cut across the stretch could give
the other end of the overlap, at 50, besides. It is given at 10, the end of the overlap next to the earlier lobe along the spline the work
is done from, asked either way and turned, 0.000012 short of it a hundred thousand out, where the lobe comes on the line to the digits.
That end is the crossing where the curve comes on the other from the earlier lobe's side and leaves it to the later lobe's; where it
comes on from the side it leaves to, the change of side is before the overlap or after it and is looked for there: the polyline over
the line to 10, 0.000005 under it to 30, on it to 50 and under it after crosses it at 10, and one on it from 10 to 30, 0.000005 over
it to 40 and under it after, at 40. Between two lobes to one side the two do not cross, and the ends of the overlap, given before, are
no places; a crossing of another branch of the curve far from parallel, in the overlap or at its ends, is one: the polyline that lies on
the line from 20 to 50 and comes back square across it at 35, at 20 or at 19.5 crosses it there, once. Of 300 random polylines
over a line, on it exactly between their lobes, asked either way, turned and far out, 112 of 728 answers judged at 1E-3 were wrong and
4 are, and at 1E-2 77 and 2: an overlap that a stretch along begins at, its earlier lobe in another, and one 220 000 out whose lobe is
so flat that it is on the line to the digits 0.0013 before its corner.

**FIXED.** A copy of a spline that crosses itself crosses it at that crossing, inside the stretch the two lie along each other. The
cubic through the control points (0, 0), (150, 100), (-50, 100), (100, 0) crosses itself at (50, 42.857) at 74 degrees, and its copy
moved 0.0005 lies along it within the tolerance all the way; the copy's other branch crosses the cubic there, but a part inside a
stretch along was never cut, and the crossing was never looked for, asked either way, turned, or in a tilted plane of space with the
copy in the loop's plane (a copy lifted out of it by 1E-9 to 0.0004 still misses it, as before: in space a stretch along is read by the
leasts of the distance). A part
inside a stretch along is now cut against a part of the other curve it may meet at more than a degree from parallel, the default
angle tolerance, and such a meeting is kept where it lies on another branch of the other curve than the one the stretch runs along:
further along it than the looks either side of the meeting have their nearest points, those within twice the tolerance over the sine of
the angle passed over, and round the seam of a closed one. A copy of a polyline still crosses it at a corner
only as its lobes have it, as before. Of 200 such loops and their copies moved 0.0001 to 0.01, asked either way, turned and far
out, their 397 crossings at more than 0.02 of a radian were missed in 934 of the 1 588 answers at a tolerance of 1E-3, and in 1 487
at 1E-2, and are in none; 185 of the 200 give as many places as the exact curves have crossings, those within the tolerance of each
other taken as one, where 76 did, and 178 at 1E-2 where 10 did. Of 300 random splines and their copies moved 0.0001 to 0.021, the
1 184 such crossings were missed in 1 027 of 4 736 answers and are in 8, and in 3 354 and 20; 204 give the exact count where 166
did, and 221 where 109 did. Of 300 each of hairpins, figure-eights and splines with cusps and their copies, asked three ways, the
crossings at more than 0.02 of a radian were missed 2 925, 11 873 and 1 471 times at 1E-3 and are 25, 93 and 65 times, and at 1E-2
2 749, 11 102 and 1 016 times and 78, 118 and 130; of tight waves at 1E-2, 5 046 and 112. Two crossings within the tolerance of each other are one place, as before, and
where both are such crossings of another branch, the one first along the two curves together, the shares of the way along each
added, stands for it, not the one nearer the other curve by its rounding, and where the two shares are the same to a billionth, as
for a copy moved along either bisector of the loop's two tangents, the one nearer the point half way between the two curves' starts:
the place is the same asked either way and turned eight ways, for each of 30 such moves of the loop above.

What moved: of 3 600 answers of random pairs of splines, 3 600 of splines with lobes about a line, 2 100 of splines sitting on circles
and circle splines and 2 766 of splines against segments, circles, arcs, ellipses, polylines and arc splines, at tolerances of 0, 1E-3
and 1E-2, none, to the bit, nor any answer at a tolerance of 0. The overlaps (1 413 of 3 600 answers), the loops (1 308 of 2 400) and
the copies (1 069 of 3 600) above, and 417 of 2 400 answers of splines against their own flattening as splines and against copies of
them bent a hair, every one a place gained or moved within the tolerance to another crossing of the pair, but for 14 places of the
copies and three of one loop, where crossings found now join places further apart than the tolerance into one, or another crossing of
such a run stands for it; and the figure-eights give 18 places more in their 1 800 answers, each a real crossing of the exact
curves, steep, of another branch, at a corner of a polyline that runs over itself, which no sample of them had found. A shallow
crossing of another branch next to the end of a stretch along can still be missed at a hundredth.

The cutting costs time where a curve lies along another. Timed twice each, in turn, on .NET 10: 40 random splines and their copies
take 1.16 and 1.15 s where they took 1.02 and 1.01 (13 % longer), 20 loops and their copies 0.82 and 0.81 s where 0.78 and 0.77, 40
overlaps turned 10 ms where 13; but a spline against its own flattening into a spline of degree one is cut until the two are
parallel to the degree, and takes four to six times as long: 20 random splines against theirs 0.37 and 0.36 s where 0.08 and 0.09, and
the arc of radius 100 a million out turning 2 radians against its 65 536 chords as a spline 0.55 to 0.57 s where 0.09 to 0.11 (the
`GeoPolyline2` of them, which is no spline, takes 1.0 s as before). On .NET Framework 4.8: 1.84 and 1.86 s where 1.52 and 1.69 (10 to
21 % longer), 1.38 and 1.44 where 1.18 and 1.31, 18 ms where 22 and 29, 0.60 and 0.66 s where 0.13 and 0.12, and 0.90 to 0.92 s where
0.12 to 0.17. 9 new tests, 36 cases; three earlier tests count the crossings at the spline's own crossing now, which
they left out: a near copy of a degree-two spline that crosses itself twice gives 7 places where 3, as the exact curves cross, and a
copy that overhangs the spline's end 3 where 2, in the plane and in a tilted plane of space.

**FIXED.** A ray is given a point where it meets a triangle, a polygon, a face, a box, a body, a prepared body, an arc or a
circle exactly where `CollidesWith` says the two touch, but where it lies along the shape and names no place, as before:
the list and the collision read a ray starting at the shape apart. A ray starting within the tolerance of a region beside
its rim and running away from it, its line lying level over the region only behind it, collided and was given nothing,
its own part read along the side it leaves: the ray from 0.0006 beside the side of a square 100 by 100 and 0.0003 over
it, running away from the side at a hundredth, 0.00067 from the square, touches it at its origin. So a ray whose line
names only the nearer of two places it touches, behind the origin, is given its own: the ray from (42.126, 51.954), 0.00058
under the hole 40 by 40 in the middle of a square 100 by 100, falling 7.8E-6 for each unit along it, passes 0.00076 under
the rim of the hole at (34.488, 30) and touches it there, where it collided and was given nothing. A body read a ray
starting within the planar tolerance of a face as touching it at its origin, and the collision reads the point
tolerance: with a point tolerance of a hundredth and a planar one of five hundredths, the ray from 0.04 over the top of
the box 100 by 100 by 50, rising a hundredth for each unit along it, was given its origin and collided with nothing, and
is given nothing, while the one from 0.004 over it touches it at its origin as before. The meeting at the origin is given
only where the origin stands within the point tolerance of the surface, whatever the ray meets further on and however
deep in the body it starts: the ray from 0.04 off the inner wall of a U 100 by 100, its prongs and base 20 thick, along
X across the gap, was given its origin besides the two walls of the far prong, and is given those two; the one from 0.04
under the top of the box was given its origin besides where it leaves, and is given where it leaves; and the one from
0.012 inside a box turned in space, under a face, is given where it leaves 59.252 along wherever the box stands, where a
hundred million out it was given that and at the origin its origin besides. With the planar tolerance narrower than the
point, a ray starting between the two off a face and leaving it collided and was given nothing, and is given its origin.
A ray that collides with a body and crosses no face is given its point nearest the surface, where that stands within the
point tolerance of the surface, to the rounding of numbers the size of the body's, or the body holds the origin: at a
tolerance of nought, where the rounding of a body turned about in space refuses the crossing of every face, the ray from
the middle of a turned box 100 by 80 by 50 is given where it leaves, to 1E-14 of where it leaves at a thousandth, where it
was given nothing. An oriented box read the short cut of a ray running past its rim as lying along it by the planar
tolerance: with a planar tolerance of five hundredths the ray from 0.0075 beside a side of the box 100 by 80 by 50, 0.005
from its edge and running away from both, touches it at its origin, where it collided and was given nothing. A prepared
body gives a ray starting within the point tolerance of where its material ends, and crossing no triangle, its origin. A
point of a circle on the line of a segment or a ray, held by each within the point tolerance but not by the two together,
is given no more: the segment from (0, 0, 0.0008) to (49.9992, 0, 0.0008), ending 0.00113 from the circle of radius 50
about the origin, was given (50, 0, 0.0008) and collided with nothing. Each point is weighed on its own, by whether the
segment or ray comes within the tolerance of the curve at its point nearest it: the same segment from (-60, 0, 0.0008),
which passes 0.0008 over the far rim and so collides, was given both points and is given the far one only, while the one
from (-60, 0, 0.0006) to (49.9994, 0, 0.0006), whose end stands 0.000849 from the near rim, touches both rims and is
given both, as before; a ray where no point is left is given the point of the curve nearest it where they touch. At a
tolerance of nought the ray starting 4E-12 over a square and rising from it, which touches nothing, is given nothing,
where it was given the crossing of its line 4E-12 behind its origin. Of 120 000 rays drawn starting within the planar tolerance of a plane, a triangle, a square or
an L, a face with a hole, an oriented and an axis-aligned box, a body and a prepared body, at the origin, near it and a
million out, square to the axes and turned, at tolerances of a thousandth, a hundredth and five hundredths, of nought,
and of a hundredth with planar ones of five hundredths and of a thousandth, 1 759 collided and were given no point and
528 were given points and collided with nothing; 116 do now, all of them prepared bodies at a tolerance of nought, whose
index crosses no face of a body turned about in space and holds the points it locates by those crossings (not fixed yet).
Of 200 000 rays and segments starting at or passing an arc or a circle within the tolerance of its plane and rim, 914 were
given points and collided with nothing, and 916 points were given where the ray or segment comes no nearer the curve
than the tolerance; none is now, and of the answers that collide 141 moved, each giving one point of the two it gave, the
other standing further off. Of 50 000 more drawn through a circle or an arc of radius 0.001 to a million, up to a
thousand million out, not one touch given before is lost. Of the 120 000 rays, 152 that collide with a body moved: the
meeting at an origin within the planar tolerance of a face but further than the point tolerance from the surface, or a
billionth inside it at a tolerance of nought, is given no more. Every other answer of the rays is the same to the bit. A
ray lying in a plane to rounding touches it everywhere and names no place, and one holding a level part of its line over
a region is given none, as before: 1 021 and 6 844 of the 120 000. A ray piercing a polygon or a face takes a fifth to a
third longer to be given its crossing (1.52 microseconds on .NET Framework 4.8 for a square where it took 1.28, 0.64 on
.NET 10 where it took 0.49), one starting on it up to an eighth longer, and one piercing or starting on an oriented box
0.07 to 0.14 microseconds longer. A ray missing a body but passing inside its box, which is asked how near it comes,
takes longer: down the gap of a U from outside its box 10.9 microseconds where it took 9.5, and past the corner of the
box of a cylinder of 64 sides 89.2 where it took 80.5, on .NET 10 compiled in full, the best of three; with the runtime
compiling in tiers the U took 14 to 26 where it took 11 to 16. A ray piercing or starting on a body takes up to 7 %
longer, and a ray missing the flat shapes and the collisions as long, within 0.03 microseconds. 24 new tests.

**FIXED.** A body far from the origin holds a point as one at the origin does. Whether a point is in a body was asked to
within a billionth, and 5E7 out a billionth is less than the rounding of the coordinates: the crossings of the rays counted
from the point stood 1.9E-9 off the planes of the faces they crossed, were refused, and the count came out wrong. The ray
from 0.0516 inside the gap of a U 100 by 100 turned about in space 5E7 out collided with it at a tolerance of five
hundredths; rays along a cylinder of radius 30 turned from a rectangle there, 0.05 off 5 of its 18 side facets, collided
with it at a tolerance of a hundredth; and points 10 inside it under 9 of those facets were 10 from it. None collides now,
and the points are nought from it. Further out than a million a point is held to 1E-15 of the largest coordinate of the
body and the point, a few times the most the rounding leaves there (3E-16 of it, over some 160 000 points up to 1E9
out); nearer, to a billionth as before, and so is a point at infinity or not a number. The list of where a ray meets a
body reads the body's box as far as the body holds a point, where it read a billionth past it: 5E7 out at a tolerance of
nought, the ray from 2.2E-8 over the top of a box 100 by 80 by 50, pointing away, collides with it and is given a point.
The collision of a ray asks the body of its origin wherever the origin stands, where it asked only within a millionth of
the body's box: 2E9 out the ray from 1.4E-6 over the top of the box, pointing away, collides with it as it is nought from
it, and the one from three times as far as the body holds a point does not. The reading is behind `CollidesWith` of a
body with a ray, a line, a segment, a chain, a triangle, a polygon, a face, a ball, an arc, a circle, a curved edge
(`GeoEdge3`) and a chain or loop of arcs (`GeoPolylineArc3`, `GeoPolygonArc3`); `GetIntersections` and
`TryIntersectWith` of a ray with a body; `DistanceTo` between a body and a point, a line, a segment, a ray, a chain, a
triangle, a polygon, a face and a ball; a spline against a body; and `GetShortestLineTo` from a ball to a body. `Locate`
and `Contains` of a body, which read the tolerance given, are as they were. Of 600 000 rays drawn
against boxes, U bodies, cylinders, cones and rings, turned about in space or square to the axes, placed up to 1E9 out, at
tolerances of nought, a thousandth and a hundredth, missing by half, one, one and a half, three and a hundred times the
tolerance (a thousandth at nought), hitting, or starting inside, 10 124 of the 239 525 placed 1E7 to 1E9 out collided
where they missed, 35 197 origins were not nought from a body they were in or were nought from one they were outside, and
30 086 segments a thousandth long from them collided or missed wrongly; none does now. Of the collisions 11 083 changed,
10 124 to the right answer and 959 of rays missing by exactly the tolerance, none to a wrong one. Of the 360 475 placed a
million out or nearer every answer is the same to the bit: the collision, the points given, the distances of the ray, of
a segment 300 long and of its origin, and where the body holds the origin. Of 25 000 rays and segments drawn about
prisms of an L, a star, a box with openings through it and plates with holes, placed up to 4E9 out, 4 083 points of the
17 400 placed 1E7 to 4E9 out were not nought from a body they were in or were nought from one they were outside, and the
collision of 61 rays disagreed with their distance; none does now, and of the 7 600 placed a million out or nearer every answer is the same to the bit. A point near the origin is
held as fast as before; far out it is held as fast as near the origin, where it was held faster and wrongly (the ray out
of the gap of the U 5E7 out 4.02 microseconds on .NET Framework 4.8 where it took 2.97, as at the origin 4.03). A ray
far from a body's box, which is no longer passed by the box before the body is asked of its origin, takes 0.02 to 0.05
microseconds longer: 0.37 where it took 0.32 on .NET Framework 4.8 for a U 200 over its box. 13 new tests.

**FIXED.** At a tolerance of nought a ray cast at a body finds the crossing of each face it passes through, and
the body and its prepared form read the points inside the turned cylinders and cones tried as inside. The crossing
a ray finds on a plane stands off it by the rounding of numbers the size of the coordinates, and the triangle or
face read that against the planar tolerance of nought and refused it on nearly every face of a body turned about in
space or made by `Revolve`: of 1 000 rays from inside a cylinder 40 across and 50 tall, its axis leaning in space,
874 crossed nothing, the prepared body read 111 of 1 000 points inside it as inside and the body 125, and a point
1.6 outside its side was read inside by both. A crossing on the seam of two triangles, rounded a hair outside both,
was held by neither, so a point on the axis of a cylinder whose first ray ran through a seam was read outside:
48 of 49 such points of a cylinder at the origin by its prepared form, 35 by the body. A triangle or face now
holds a crossing to the rounding of the coordinates, 1E-15 of the largest of the body's and the ray's origin, as a
body far out holds a point, on its plane and past its rim; whether the crossing lies ahead of the ray's origin or
behind it is read by the tolerance given, so the point 3.03E-13 over the floor of a box 100 by 80 by 50 is inside
it. Below the rounding, and only there, a crossing within twice it of a rim sends the ray off in another direction,
two crossings of a seam within eight times it of each other along the ray are one, as each is worked out from its
own side (the ray from 60 out towards the axis of a cylinder of 1 080 sides crossed its far seam twice, 5.2E-14
apart where the rounding is 4.9E-14), and a ray from further off than the body is large is read from a diagonal
short of the body, along its own line, so that its crossings are worked out from numbers the size of the body's and
the rounding stays the body's: the ray along the seam of two sides of a cylinder of radius 20, through its axis,
is given its two crossings from 60 out and from a hundred million out, where the prepared body gave none and the
body one or none, and the ray from a thousand million under a box, 5E-6 beside its side, is given nothing, as
`CollidesWith` says. The openings of a body asked at a tolerance below the rounding are chosen and cut within
the rounding: a point 0.0005 under a plate turned about in space, with an opening through it, is 0.0005 from
it at a tolerance of nought, where it was 3.0005. Where it reaches, at a tolerance below the rounding only:
`GeoPreparedSolid3.Locate`, `Contains`, `DistanceTo`, `SignedDistanceTo`, `GetIntersections` and `CollidesWith`
of a ray, `GeoBvh3.GetIntersections` of a ray, `GeoSolid3.Locate` and `Contains`, `GeoRay3.GetIntersections` and
`CollidesWith` of a body, the depth under a triangle the booleans read, and every query of a body with openings
that reads its material. At every tolerance at or above the rounding every answer is the same to the bit, far
out included: of 1 327 104 answers drawn at 1E-6, 1E-3 and 1E-2 for boxes, prisms, cylinders, cones and rings at
the origin and out to 4E9 (points stepped off a facet by multiples of the tolerance and by 3 and 30 times the fine
tolerance a body far out holds a point within, with their distances, and rays from inside, through and away), 33 138
answers of prisms with and without openings out to 4E9 and 133 392 rays and points about boxes, U's, cylinders,
cones and rings out to 1E9, none changed, nor did any boolean of the near-face fuzz runs at a tolerance of a thousandth,
a hundredth or five hundredths. At a tolerance of nought, of 442 368 answers 173 857 were wrong and 1 047 are:
997 rays and 50 points of bodies a million to 4E9 out that start or stand within the fine tolerance of a face,
or between a face and the facets it is meshed into, where either answer holds. Of 16 862 answers of the prisms at
a tolerance of nought, 1 226 points measured more than a millionth off their distance and 531 rays and 546 segments
read as colliding where they missed or the other way round; 3, none and none are now. A ray whose collision disagreed
with its distance did so 141 times and does 10, all of them new: plates with openings a million to 4E9 out, the ray
starting 0.3 or 3 times the fine tolerance outside, where the cut material holds the origin and the body does not
(not fixed yet). The planes of fan triangles far out can be rounded by more than 1E-15 of the coordinates: a point on
the axis of a cylinder of 1 080 sides 1E7 out is still read outside in 10 of 120 readings (not fixed yet). The time at
a tolerance of a thousandth is as it was, within 3 %: on .NET 10 compiled in full a point located in the prepared
cylinder of 300 faces takes 9.02 microseconds where it took 8.97, in the body itself 16.9 where it took 16.9, and a
ray crossing the prepared body 6.38 where it took 6.32; on .NET Framework 4.8 25.3, 52.5 and 20.5 where they took
24.7, 51.9 and 20.4. At a tolerance of nought they take about as long as at a thousandth: a point located in the
body 15.9 microseconds on .NET 10 and 50.0 on .NET Framework 4.8, where it took 8.2 and 23.3 and was read wrong,
and in the prepared body 8.94 and 25.3 where it took 7.90 and 19.2. A boolean at a tolerance of nought still fails
or comes out open, 107 of 108 unions, differences and intersections of a cylinder, a cone, a ring or a box with
a box where all 108 did, and 1E-15 of the coordinates does not close them either (not fixed yet). 15 new tests.

**FIXED.** `Offset2.Offset` of a `GeoPolygonArc2` that crosses itself, grown with miters or chamfers, keeps the miter or
the chamfer at each tip of its region where two lobes of it cross; so does `GeoPolygonArc3.Offset`, which miters. The
region read even-odd lies in two opposite quarters of such a crossing, and the loops that bound it were joined round the
two quarters between, so a tip of the region was no corner of them and its join was never built: where the quarter
across the crossing was too small to hold it, as a lens or a small lobe is, the join was left out. Grown with miters or
chamfers, the loops are now joined round the quarters of the region where they are two lobes, as the straight path
reads them: in two pieces of the region once it is cut at every crossing. Each tip is then mitered or cut as any corner
is, a miter running its two pieces on round their own circles past the crossing. A crossing whose two quarters are one
lobe running round a hole, a place where more than two edges cross, crossings within the tolerance of each other made
one, a crossing a corner of the loop lies within the tolerance of, or a piece through it ends at a corner within it, and
one beside a lobe that is a sliver within the tolerance, are joined as before; a vertex the loop runs straight on
through, within the vector tolerance, is no corner, but one that bends it by half a degree or more still is, so such a
vertex within the tolerance of a crossing still joins the crossing as before, though it moves the loop less than the
tolerance. So is every crossing of a DXF polyline's `GetOffsetCurves`, whose
miter has no limit, as AutoCAD is not known to read a crossing as a corner (to be tried, point 79 of the AutoCAD checks).
The loops of the region's boundary are put in order by where they lie, so the answer does not hang on where the loop was
drawn from. The bow tie from (-10, 2) to (1, -0.2), (1, 0.2) and (-10, -2), its back an arc of bulge -0.25, grown by 1
with miters is mitered out to (5.099, 0) past its small lobe and encloses 54.460, where the miter was cut off at x = 2
and it was 52.539, and with a vertex on its edge 0.005 from the crossing, at a tolerance of 0.01, as without it; a loop
of ten corners crossing itself at nine places, grown by 0.3 with miters, is 92.909 where it was 90.994; a loop of eight
corners whose arc dips 0.04 through an edge, cutting a lens with tips of 2.3 degrees, grown by 3 with miters at a
tolerance of 0.1 is 530.519 where it was 399.260; and a loop of four corners whose first and last edges cross 1.3 degrees
apart, grown by 3 with chamfers, is 212.058 where it was 211.229. The square 10 by 10 whose bottom edge curls up into a
loop, grown by 3 with miters, stays 253.263, as the straight path gives it.

Read against an oracle built apart from the code: the region of a polygon of the loop within 1E-5 grown by the straight
path with round joins, with the miter or chamfer of every corner that opens added, the tips of two lobes at a crossing
among them, a lobe told by the region's pieces with a square 0.002 across cut out about the crossing. An offset is
counted wrong where it and the oracle differ by more than 0.001 in area once each piece of their difference is shrunk by
twice the tolerance. The fuzz offsets 300 random curved loops, 37 random loops of straight edges and 37 curved loops that
do not cross themselves, by four distances each way, with each join and at tolerances of 1E-3 and 1E-2: of each set of a
join, a tolerance and a way, grown or shrunk, 1 488 offsets are judged, 1 192, 148 and 148 of them, and 8, 32 at each join,
whose crossing lies at a vertex or touches, are left out. Grown with miters, 252 of the 1 488 were wrong at 1E-3 and none
is, and 238 at 1E-2 and 7 are; grown with chamfers, 72 at 1E-3 and 1 is, and 52 at 1E-2 and 4 are; each left is as it was
or nearer, one of them a loop that does not cross itself. Of the fuzz's 21 504 offsets, rounded, shrunk and chains among
them, 603 came nearer, by 3 976 in all, and none went further. Read against the straight path on the same vertices or on a
polygon of the loop within 1E-6, none of 5 400 offsets of straight loops, 720 of irregular stars and 1 200 of curved loops
grown with chamfers went further either, and 628 came nearer; nor, against the oracle, did any of 150 of curved loops
grown with miters, 34 of which came nearer. Offsets that were right change too, though not to the bit: their loops are put
together from other pieces. In the fuzz 1 940 mitered offsets that were right moved by 0.097 in area at most and 2 292
chamfered ones by 0.058 at most, within the tolerance, and 8 and 3 of them have a sliver loop finer than the tolerance
more or fewer. Measured by the two-sided distance between boundaries against the straight path, of the 3 671 such
offsets of straight loops, stars and curved loops whose distance could be measured (248 of the curved ones could not), 3
stand further off than the tolerance, and further than they did, by at most 1.9 times the tolerance: crossings where the
pieces of two lobes and a third meet within a few tolerances of each other; 4 more leave out a hole or a loop finer than
the tolerance, as the straight path within the tolerance does. Round offsets, loops shrunk, loops that do not cross
themselves, chains and the straight path are the same to the bit.

Grown with miters or chamfers, a loop that crosses itself takes 1.6 to 3.2 times as long, its tips joined and its runs
put together at each crossing by their ends: the 1 200 mitered offsets of 300 random loops grown at 1E-3 take 0.32 s on
.NET 10 where they took 0.20, the chamfered ones 0.35 s where they took 0.20; a star {61/30} of radius 10 grown by 0.3
with miters takes 1.6 s where it took 0.76, a loop of 80 random corners grown by 1 0.11 to 0.12 s where it took 0.037, and
a gear of 642 teeth as long as before. Every other offset takes as long as before within the noise. 11 new tests.

## 12.3.0

**NEW.** `DxfReader` reads a DXF file into the library's shapes: `Read(path)`, `Read(stream)` and `Parse(text)`, each with
`DxfReadOptions` (`PlanAs2D`, `ToMillimetres`, `Tolerance`, `KeepGroups`), give a `DxfDocument` of the `Entities` in file
order, the `Layers`, the `Units` of `$INSUNITS` (the new enum `DxfUnits`), the `Version`, what was `Skipped` by type and the
`Warnings`, with `OfType<T>()` and `OnLayer(name)`. ASCII files of R12 to R2018 are read, AutoCAD's and the library's:
POINT, LINE, RAY, XLINE, LWPOLYLINE and POLYLINE (bulges as arcs, R12's VERTEX and SEQEND without handles, the curve of
a spline-fitted one, a 2D one at its own elevation), 3D POLYLINE, polyface and polygon meshes (a `GeoSolid3` of their
faces), CIRCLE, ARC, ELLIPSE (a part of one as the spline that is exactly it), SPLINE (by its control points, knots and
weights, clamped by knot insertion where AutoCAD writes a closed one unclamped, or through its fit points), 3DFACE, SOLID
and TRACE, in the system of their extrusion's plane where they have one. Shapes are of space unless `PlanAs2D` asks for
those lying on the floor as shapes of the plane, a mirrored one on the extrusion (0, 0, -1) among them, x turned over as
ezdxf places it and a mirrored arc clockwise from the start the file gives: a closed LWPOLYLINE of a slot 80 by 20 with bulges of 1 is a `GeoPolygonArc2` of 1 514.159 square units.
Every number is read as a correctly rounding parser reads it, the same on .NET Framework as on .NET 10, a number of
300 000 digits in 2.2 ms, so every shape `DxfWriter` writes reads back with every number the file holds to the bit (of
every one of its 42 overloads of `Add`, and of 4 200 random shapes); the angles of an ARC, the axis of an ELLIPSE and the
coordinates of a tilted plane come back to the last place or two. TEXT, HATCH and the other types, an entity with no
shape to it (a LINE of no length, a CIRCLE of no radius, a NaN), and one whose reading throws are counted and the rest
read; a file cut short, or with a group code that is not a number, is refused with a `FormatException` naming the line.
The text of a file before R2007 is read in its code page, Windows-1252 the same on either runtime by a table of its own;
any other code page (1250, 1251, 1253 to 1258, 932, 936, 949, 950) .NET Framework reads, and .NET 10, which has none of
them without CodePagesEncodingProvider, reads as Latin-1 with a warning to save the drawing as R2007 or later.
Each `DxfEntity` keeps its `Layer`, `Colour`, `TrueColour`, `Linetype`, `Lineweight`, `Thickness` and `Handle`, and its
`Groups` (the new `DxfGroup`), every group of it as the file has it, extended data among them. 100 000 entities (34 MB)
are read in 0.54 s on .NET 10 and 0.90 s on .NET Framework 4.8, the document holding 157 MB, and without the groups in
0.35 s and 0.58 s, holding 41 MB. Paper space is not read: a warning counts its entities. 95 new tests; the suite runs
12 083 of 12 084, one skipped as before.

**NEW.** `DxfReader` reads blocks and the INSERTs that place them. Each INSERT is read as the entities of its block, placed
where it puts them, in the order of the block and where the INSERT stands: a MINSERT's array row by row, an INSERT inside a
block within it, to a depth of 64. `DxfReadOptions.ExpandBlocks` (true by default) turns it off, and `DxfDocument.Blocks`,
of the new `DxfBlock` (`Name`, `BasePoint`, `Flags`, `Entities`), holds the blocks in their own coordinates either way. A
turn, an even scaling and a mirror place each shape as the same shape, a mirrored arc running the other way round; an
uneven scaling places a circle as the ellipse it becomes, an arc as the spline that is exactly that part of it, and a
polyline with arcs as its edges, a segment or such a spline each: the circle of radius 3 scaled by 3 along x is the
ellipse 9 by 3, and a scaling even within the point tolerance (1.0000001) keeps the circle of radius 10 a circle.
Splines, ellipses, polylines without arcs, faces and meshes are carried exactly by any placement, a mirrored body still
facing outwards. An entity on layer 0 takes the INSERT's layer, and one BYBLOCK its colour, true colour, linetype and
lineweight, through every level; `DxfEntity.BlockPath` names the blocks an entity was placed through, and the new
`DxfEntity.InsertHandles` the INSERTs. An INSERT of a block inside itself, of one the file does not define, of an external
reference or a MINSERT of more than 1 048 576 cells is counted and said in a warning, the rest read; the blocks of the
layouts are not read. The new `DxfReadOptions.MaxPlacedEntities` (2 000 000 by default) bounds what INSERTs place from
blocks in one reading, an INSERT placed whole or not at all by the size of its block worked out first: the file of 18 KB
whose blocks nest four levels of 100 INSERTs, 100 000 000 lines, is read in 1.4 s, where it ran out of memory, and an
INSERT that would place more than its block was worked out to is taken back whole, so the bound holds for blocks that
insert each other too. A block in a ring of blocks that insert one another is worked out once for all its INSERTs:
1 000 INSERTs of one in a ring of 26, each inserting the next twice, a file of 43 KB, are refused in 1.4 s on .NET 10,
where each worked out a million blocks and the file took 35 s. Held against ezdxf 1.4.5 on 168 entities placed every way, every point is within 1E-9 of where
ezdxf puts it (ezdxf's own INSERT nested in a block is wrong under an uneven outer scaling with an inner turn, and there
the reference composes its matrices), and of 57 600 entities placed by random INSERTs every one lies within 1E-9 of its
size of the block's shape carried by a matrix worked out apart, but for 3 circles and arcs a scaling within the point
tolerance of even keeps round, 9E-4 off. 10 000 INSERTs of a block of 50 entities give 500 000 entities in 0.50 s on
.NET 10, the document holding 157 MB. A point the system of a tilted extrusion takes past the largest double leaves its
entity out, counted as not finite, where it gave infinite coordinates. 39 new tests; the suite runs 12 122 of 12 123, one skipped as before.

**NEW.** `DxfWriter.Add(DxfEntity)` writes an entity read back, or one made in code with the new public constructor
`new DxfEntity(shape, layer, colour, trueColour, linetype, lineweight, thickness)`: its shape by the overload of `Add` for
it, with its layer, colour, true colour, linetype, lineweight and thickness where it has them, a linetype the table lacks
added to it by name, the edges a 3DFACE hid hidden again. A drawing read, written and read again has the same shapes and
the same look, to the bit but for the angles of an arc and the axis of an ellipse; a mirrored arc in space comes back
running the other way over the same points, and BYBLOCK as ByBlock. The other overloads write the same bytes as before:
the file of one shape of each hashes the same.

**CHANGED.** The viewer opens a DXF with `DxfReader`, its own parser gone: it shows what the library reads, blocks placed,
but decodes the file itself, as UTF-8 or else Latin-1, so a letter a file before R2007 keeps in its code page can differ.

**FIXED.** A closed spline, its first and last control points the same to the bit as the closed flag of a DXF spline reads
them, has no ends. Its seam was read as an end, where the places a spline gives are its crossings, its touches and where it
ends near a shape: the line tangent to `GeoSpline2.FromCircle` of radius 100 at 0.003 of a radian was given the seam (100, 0),
4.5E-4 off it, besides the touch, where the `GeoCircle2` gives the touch alone, and so at 0.001 and -0.003 of a radian. The seam
is a point of the spline like any other now, coming in along its last span and going on along its first, for the places, the
touches and the leasts of the distance, in the plane and in space. In the plane this moves the answers that had the seam of a
closed spline within the tolerance of a shape and no place there: of 120 random splines, half of them closed, against the 15
shapes of the plane at three tolerances, the one answer of a circle spline whose seam stood within a hundredth of a rectangle,
which loses its seam.

**FIXED.** A spline made of arcs of circles meets a circle, an arc, an edge or loop of them, or another such spline, as the circles
meet. Two circle splines nearly about one centre were cut into parts until a budget of two million pairs ran out: of radius
100 000 and 100 000.0005 with their centres 0.01 apart they took 16 seconds and gave no crossing, and their distance read 4.8E-5,
though they cross at (-4 999.995, 99 874.922) and (-4 999.995, -99 874.922). A span that is an arc of a circle to rounding, as every
span of `FromArc` and `FromCircle` is, now meets the partner's arc in closed form by the rule of `GeoCircle2.GetIntersections`,
one circle included, where the circle read from its control points is known well enough: its centre and radius within a hundredth of
the tolerance of the true circle's for the places, and within a snap for the distance. The rounding of the control points, taken at
the size of the radius since an arc is made from its centre, reaches the centre times four times the square of the radius over the
chord, so a quarter turn of radius 100 000 is read to 1E-10 and met so, and a flat arc, of radius 10 000 000 and chord 1 000 a hundred
thousand out (its centre off by 0.6 when measured) or of radius 1 000 000 and chord 300 (off by 2.3E-4), is cut as before; read that
far off, the circles' rule gave such an arc against nearly its own circle one to four places the arcs do not have. Those two
near-concentric splines cross where the circles do, and are nought apart, in a third of a millisecond each. And a circle spline moved
a few ten-thousandths to a hundredth at random answered otherwise than the two circles against the circle it was moved from (142 of
200, the seam among them) and against the spline of that circle (128 of 200, missed and doubled crossings); none of 300 such random
pairs does now, from either side. Of 1 600 random pairs of arcs of nearly one circle, moved and grown less than the tolerance, as
splines against the arc and against its spline from either side, of radius 5 to 2 005 none answers otherwise than the arcs where 326
of 400 did, and of radius 100 to 100 000 000 a flat one cut as before still can: 383 of 1 200, where 838 did, and none that did not.
A join of the spans within a snap of a very shallow crossing can
still be given as a place of its own: the circle splines of radius 100 000 about the origin and of radius √(0.003² + 100 000²) about
(0.003, 0), which cross at (0, 100 000) and (0, -100 000), joins of the first one's spans, give four places where the circles give two
(parked). What moved in the plane, of 36 600 answers of random splines against every shape at three tolerances: the places
and the shortest segments of circle and arc splines against arcs, circles, edges, chains and loops of arcs and other arc splines,
the places by up to 4E-9, the distances by up to 1.8E-12, and the ends of the shortest segments by up to 2.4E-5 along the curves,
where two circles' distance is level to rounding; and the places of a circle spline and its copy moved
less than the tolerance, which were the crossings of the two curves and are none now, the two being one circle, as the `GeoCircle2`
says of the circles. Two circle splines of radius 1 000 and 1 000.00002 with their centres 1E-4 apart, within 1.2E-4 of each other
all round, were given two crossings at an angle of 1E-7 at a tolerance of a thousandth and none at a hundred-thousandth; they are
one circle at a thousandth now, as the circles are, and cross at (-200, 979.8) and (-200, -979.8) at a hundred-thousandth, where the
circles do. The circle splines of radius 10 000 and 10 000.0005 with their centres 0.001 apart took 12.5 s for their two crossings
and of radius 1 000 with centres 0.01 apart 0.4 s; each takes a third of a millisecond now. In space two circle splines in one plane
are laid in it and answer the same; two that share no plane are cut as before.

**CHANGED.** The stretches two splines of the plane lie along each other for are measured as they are in space: a quarter of how
far the control points of the smaller reach from its first, which is the same however the pair is turned, and not a quarter of
its span along the axes, which is not. Of 60 random splines and their copies moved a few ten-thousandths, 14 gave a place more or
fewer turned by 0.7 of a radian, and 7 do now, by where the stretches along them end. The places of near copies in the plane move
for it: of 166 copies of 83 random splines that are not arcs, moved 4E-4 or turned and moved (3E-4, -2E-4), 6 give a place more or
fewer where a stretch now ends elsewhere, one gives another place, and 107 give their places moved by up to 2.4E-7, the crossings
inside a stretch bisected from looks spaced by the new size. Two parts of curves that cannot meet
are measured once both are no larger than the tolerance in the plane as in space, straight or not, so that a spline and its copy
within the tolerance round a cusp are not cut into millions of pairs: a tight spline of degree four and its copy moved 0.00074 take
0.3 s, which the stretch measured so would have cut for 26 s. And a least of the distance that curves up too little is one only where
the distance falls neither way, as in space: the cubic y = x³ / 100 turned 0.7 of a radian a hundred thousand out was given its
inflection, 1E-4 under the line y = 0.0001, besides the crossing.

**FIXED.** A segment, a ray or a line without ends lying in the plane of a spline in space is laid in that plane with it for
`GetIntersections`, as an arc, a circle, an ellipse, an edge or a chain of edges is, so the places are the plane's: a segment along
the first side of a polyline spline, 1.15E-6 off it and turned by 7E-8 of a radian, was given the spline's start besides the one
place the plane gives, and 96 of 5 400 such segments, rays and lines near tangent to random splines in three planes answered
otherwise than the plane; 17 do now, all of them a straight spline and a line nearly on one line, which lie in no plane the
digits can tell. The distance and the shortest segment are not changed.

**FIXED.** The hull of points holds every one of them within the point tolerance, its faces merged. The triangles of one plane
merged within the tolerance could stand a face as far again from a point: 5 of 1 000 plates 100 by 100 and from 0.002 to 1 thick
left a point up to 1.08E-3 outside the hull they were built from. Where a point stands further out than the tolerance, the
triangles are merged again within a tenth of the planar tolerance, then a hundredth, and kept as they are where that does not hold
it either; a hull that holds every point merged is the same to the bit, as are 559 of 560 hulls of 14 kinds of cloud.

**FIXED.** `GeoObb3.Fit` weighs two rectangles on one face of the hull as large as it weighs two boxes on two faces: its least
side first, then the middle one, then the longest. The tetrahedron on (0, 0, 0), (24, 0, 0), (0, 18, 0) and (0, 0, 40) was fitted
13.548779 by 27.340984 by 46.647615 as it is and 13.548779 by 30 by 42.513057 turned, as large, the rectangle found first on its long
face; it is fitted the first however it is turned, as each of 10 shapes turned 12 ways is now fitted one box, where the tetrahedron
and another were fitted three. A box as large and as thin that tied was found otherwise, its sides the same to rounding, so that
for a hull that did not change the box can come back with its axes named otherwise, `SizeX`, `SizeY` and `SizeZ` in another order:
the prism 40 long on the right triangle with legs 18 and 24 is still fitted 14.4 by 30 by 40, as 30, 14.4 and 40 along its axes where
it was 30, 40 and 14.4.

**CHANGED.** A ray or a line without ends that lies level over a triangle, a polygon or a face, and crosses its plane nowhere
apart from rounding, meets it nowhere. Each is read so only where it has no crossing of the plane to give. A segment has none
where it climbs no more than the planar tolerance from one end to the other, and is then read level by its own two ends, both
within the planar tolerance of the shape's plane, as before. A line without ends has none where the segment it is cut to, a
millimetre past the shape, climbs no more than that. A ray has none where it starts within the planar tolerance of the plane and
either runs level with it within the vector tolerance or reaches it within the point tolerance of its origin, as one rising off
the plane does. Such a ray or line is read by the two ends of the part of it over the shape, from where its shadow on the shape's
plane first crosses the rim to where it last does, or, beside the shape, by the part of it beside the side of the rim it runs
along, and not by where it was cut past the shape. Every other crossing is given as before: the ray from 0.0009 under the
triangle (0, 0, 0), (100, 0, 0), (0, 100, 0) rising 1.5E-5 crosses it 60 along, though both ends of the part of it over the
triangle lie within the planar tolerance. A ray starting within the planar tolerance was given its origin, which its crossing reads
as where it touches when it rises, or a crossing a hair ahead of it as the rounding of the origin fell, though the segment along it
was given nothing: from 0.0004 over that triangle rising 1E-6, or from 1E-12 under it, (10.000001, 20, 0). A line without ends
was read level by where it was cut, so that 0.0009 over a square and rising 1E-6 it was given the point over the square's rim. A
part no longer than the point tolerance, of one that passes a corner or runs away from the rim, lies along nothing, and the touch
is given. A ray and a segment along it can still answer otherwise, each read by its own ends: from 0.0004 over the square and
rising 1E-5, the ray stands 0.0013 over the far rim and touches the square at its origin, where a segment 40 long gives nothing;
and a ray level with the plane within the vector tolerance that dips through it over the shape gives nothing, as the line without
ends along it does, where a segment along it long enough to climb more than the planar tolerance gives the crossing: from 0.0003
over the triangle and dipping 9E-6, the ray gave (38.333, 20, 0), and a segment 150 long still does, one 70 long nothing. Of 6 000 rays and lines near level over these
shapes at two tolerances, 2 474 answers moved, each from points to none, and no collision moved; segments are not changed.
Against a box a ray and a line are read as before, as the segment from the origin past the box.

**FIXED.** The weld that closes what a boolean gives within the default tolerance keeps every face on its plane where it
can. It made the corners within the tolerance of each other one, each group at the corner of it that moved the volume
least, and put a corner on another face's edge where it stood within the tolerance of it; a corner so moved off the plane
of a face it was a corner of tilted the face, or took it off flat, by up to the tolerance. A column 300 by 300 joined,
within a hundredth, to a beam 383 wide and 366 high whose end stood 0.008 clear of the column's face had the two strips of
the end beyond the column tilted into wedges, and the union held 999.8 cubic millimetres more than the two, where filling
the gap holds 878; and a box 100 by 60 by 40 less a cube 0.89 across the tolerance inside three of its faces had a corner
of its top 0.0099 from the edge put on its side, 4 000 square millimetres taken off flat, and held 6.5 less than the box
less the cube. A face now keeps its corner where the weld would move it off the face's plane, a face beside it moving the
corner along its own plane keeps it too, and the step a few thousandths across left between them is closed by a face of
its own. That weld is taken where every step is a strip within four tolerances of its longest edge, no face it builds runs
back over itself and no two of its faces lie over each other in one plane, what comes of it closes wound alike,
and it moves the volume less than tilting the faces does; and it is tried only where the tilt is worth it, the faces
tilted by more than a hundredth of the tolerance and the tilt moving the volume by more than a tenth of the tolerance times
their area. Otherwise the weld is made as before. The union is the two and the gap filled, to a millionth, and the box less
the cube 239 999.28. Of 1 600 unions and differences of a column and a beam the tolerance into or clear of it at 0.01,
placed at random, 62 held a volume outside both readings and 39 do now, as they did; of 8 000 differences of boxes less
such cubes, 170 were valid outside the readings, by up to 22.6, and 7 are between them now; of 5 400 unions, common parts
and differences of a cube turned at random with chips, slabs and gaps exactly the tolerance thick, 4 unions valid below the
readings are between them. Of 10 000 unions and differences of an I-beam framing into a column, 74 moved, none out of
the readings and two into them, and none of 12 000 results of I-beams, plates and bolt holes on a column moved.

The weld that keeps the faces on their planes, as first made, folded some bodies `Validate` passes: a union of a cube of
100 and another tilted 0.0099 off its face, at a tolerance of a hundredth, closed a step lying on the first cube's top,
facing down over it, with 100 000 square millimetres of surface where the union has 99 998.02 and two faces running back
over themselves; 4 of 896 unions of bodies the tolerance apart and 38 of 12 000 results of I-beams came out so. And a
column and a beam framing 0.002 into its flange, the beam's flange beyond the flange's edge, closed two steps 0.002 by 16
lying on the side of the column's flange facing the other way, which the check, at a millionth, read as lying in another
plane and so as not over it; 18 of 10 000 results of I-beams came out so. Any two faces of that weld lying over each
other in one plane, within the planar tolerance and either way round, or that cannot be told apart, turn it down now.
Faces lying over each other in one plane, either way round, are in as many results as before and no more: 5 of 896
unions of bodies the tolerance apart, 55 of 10 000 results of I-beams and 157 of 600 cuts of openings.

**FIXED.** A beam less a bent bar no longer runs for minutes. Cut one body, the bar's way, the difference came out open by
the bar's end, 0.001 off the beam's floor, holding what it should; cut by every plane of both, 664 planes, it ran into tens
of thousands of cells and most of a gigabyte in half a minute. Where cutting both by every plane of both would take more
than twice the planes and sixteen of the cut that gave an open result, that result is sealed first and taken where it
closes as `Validate` reads it and adds up against the common part; otherwise both are cut as before. The beam 2 000 by 200 by 300 less the bar 16 across
bent through a right angle in forty steps holds 119 941 219.82 in a seventh of a second after its common part, 0.7 cold;
the 69 other bars of the run are the same to the bit.

**CHANGED.** A union of two bodies lying face to face within the tolerance of each other and apart, which came out open on
the way and was welded closed, is taken where it holds the two and the gap filled over where their faces lie against each
other. Checked against the two less their common part only, such a union was not worked out: two cubes of 100 side by side
0.01 apart, turned, joined within a hundredth, closed across the gap at 2 000 000, the two being 1 999 900 and the gap
100; it is taken now. A union that holds neither is not worked out, as before, and one that closed as it came is taken as
it was. So what two bodies exactly the tolerance apart join into still depends on where they lie: the two cubes 0.01
apart, not turned or far from the origin, hold 2 000 000, the two and the gap; turned another way, 1 999 933.3, part of
the gap; turned and far, 1 999 950; and at 0.001 one placement turned is still not worked out. Of 5 400 unions, common
parts and differences of a cube turned at random with chips, slabs and gaps exactly the tolerance thick, 94 unions were
not worked out and 11 are; 79 of the 81 cubes the tolerance apart are joined across the gap, and no union that was made
moved for it.

**CHANGED.** A boolean reads a void, a chip or a slab as thicker than the tolerance by a trillionth of the size of the
coordinates of the two bodies, whichever it cuts, where it read it by those of the corners of each cell. A chip 0.0010000001
deep at the corner of a turned cube of 100 was cut while one as deep along its edge was not, and of a chip 1.17E-10 deeper
than a tolerance of 0.01 at a corner the common part, cutting the block, was cut and the difference, cutting the cube, was
not, so that the two held it twice between them. The common part and both differences read such a chip one way now, cut
or nothing in each, so each difference and the common part add up to the body it was taken from: of 400 chips at depths
across that band, at corners, along edges and inside a face, one read two ways, and none does. A union is not held to
that: where the chip is nothing to the common part, the union holds anything from the two less the chip to the two, the
seam where they meet read part way, and of 400 chips a tolerance deep to a part in 1E-8 either way, 182 unions hold less
than the two with nothing in common, by up to the chip, 12 cubic millimetres, and 11 hold another volume the other way
round, as 183 and 11 did. The corner chip 0.0010000001 deep is read as no thicker than the tolerance, as the edge chip is; a
feature further past the tolerance than the band is cut as before.

**CHANGED.** A segment, a ray, a line without ends or a straight chain against a triangle, a polygon or a face is given
a point where it touches the region within the point tolerance without crossing it, as an arc and a box are: the point
of it nearest the region. `GetIntersections` of `GeoLine3`, `GeoRay3`, `GeoXLine3`, `GeoPolyline3` and `GeoEdge3`, and
of the regions the other way round, gave the crossing of the region's plane or nothing: the segment from (-5.0004,
4.9996, 0.0004) to (4.9996, -5.0004, 0.0004), passing 0.0007 off the corner at the origin of the triangle (0, 0, 0),
(100, 0, 0), (0, 100, 0), collided with it and was given nothing, and is given (-0.0004, -0.0004, 0.0004). One lying
level over the material, within the planar tolerance of the region's plane at both its ends, running in over the rim
among them, is given none, and so is one lying along a side of the rim within that of the side's line, as a segment
lying along an edge of a box is. And a crossing is given only where the two touch: a ray starting 0.00076 under the
plane of a plate and beside the rim of its hole, 0.00113 from the material, was given its origin and did not collide. Of
40 000 such shapes near the planes, rims and corners of triangles, squares and plates with a hole, turned and far from
the origin, 2 459 collided and were given nothing though they did not lie level, 5 were given a point and did not
collide and 7 a point further than the tolerance from the region; none is now, and the answers are the same whichever
way round they are asked. Every other answer is the same to the bit. `TryIntersectWith` with a single point gives the
crossing only, as before, and cutting a curved chain by a face cuts only where it crosses. A list costs about what
`CollidesWith` and the crossing cost together: 20 000 segments near a triangle, 5 ms before, take 16 now on .NET 10 and
10 on .NET Framework 4.8, where `CollidesWith` takes 11 and 6.

**FIXED.** A union, a common part and a difference of two closed bodies give a body closed as `Validate` reads it, or
report that they could not work one out. `IsClosed` matches the ends of the edges first, made one within the tolerance,
and called two copies of an edge about the tolerance apart one edge, which `Validate`, matching them along a line, reads
as two, each open; the boolean took what `IsClosed` called closed and tried nothing else: a box 100 by 60 by 40 less a
cube 0.92 across a hair inside the tolerance off two of its faces, the two turned 0.0061 radians, came back closed to
the one and open by two stretches to the other. Such a result is welded closed now, within the tolerance the boolean was
given, where that holds a volume the operation can give and moves the volume no further than moving each corner it moves
by the point tolerance can, and worked out the other way otherwise; where it stays open the boolean returns false,
`NotWorkedOut`, logged, and `TrySubtractAll` asked to close the cuts it skips is handed what such a cut gave, and closes
it as before. A result that came out open on the way is taken only where its volume adds up: a union holds the two less
their common part, a difference the first less the common part and a common part the two less their union, the other
worked out by the same boolean at the same tolerance, within the tolerance times the area of the common part's skin and
what the weld moved; otherwise, or where that other comes out open on the way too, it is not worked out. The union of a
box 100 by 60 by 40 and a cube 1.25 across standing out through three of its faces a hair more than the tolerance came
out open and, closed, held 240 001.2065, the cube counted whole, where the two less their common part hold 240 000.0032;
it is reported as not worked out now. A result that closes as it comes is taken as before, unchecked. Of 8 000 such
boxes less such cubes, 17 came out closed to `IsClosed` and open to `Validate` and 2 open to both: 13 close, each
holding a volume between the cube cut and the cube taken with the outside through walls the tolerance thick, and 6 are
reported as not worked out. Of 5 400 unions, common parts and differences of a cube turned at random with chips, slabs
and gaps exactly the tolerance thick, 97 came back not valid: 94 are reported as not worked out, among them the two
cubes 0.001 apart closed across the gap at 2 000 000, where the two hold 1 999 990 and share nothing at the tolerance,
and 3 are valid between the readings, closed by a weld from a body holding the two less their common part: the union of
the cube and a block 120 by 120 by 50 running 0.001 into its top at 1 720 012.73, the two with nothing in common at the
tolerance being 1 720 014.4, and two cubes 0.001 apart at 1 999 995, the two being 1 999 990. Of 4 000 unions of the
boxes and cubes, 13 that came out open are reported as not worked out, 8 of them 0.67 to 1.2 over the union, and 2 are
valid within 0.00033 of it. No valid result moved, by a bit, across 40 000 differences, unions, common parts and cuttings
of openings of the boxes, the 28 000 of the boxes and cubes, 6 000 differences closed or not and 6 000 booleans of
columns and beams; and results that close as they come are not checked and keep the volumes they had, outside the
readings as they were: 52 of the 5 400 unions of the cube and the chips, slabs and gaps, valid by up to 2.8 cubic
millimetres outside every reading at a tolerance of a thousandth and 33 at a hundredth, and 177 of 16 000 differences of
the boxes less cubes, by up to 20, most of them at a hundredth. The check reads the edges of most results paired one way
and the other, their corners taken on a grid a sixty-fourth of the tolerance across and edges within two cells of the
tolerance long read in full, and costs little: a plate 3000 by 1000 by 20 drilled with 100 holes one after another takes
11.4 seconds, as before, and a cylinder of 20 002 faces less a box 3.1. Working out the common part costs the unions
that came out open some 5 % more.

**FIXED.** An arc or an edge moved by a skew or a stretch lands within the global point tolerance of where the
transformation puts it everywhere along it, not only at the sixteen points it was asked at: between them it could land
further, and the arc of radius 87.66 from 0.2 radians sweeping 1.51 under X stretched by 1 + 1.793E-6 and scaled by 84.1
was moved 0.0010114 off. The stretch between two points asked is bounded by the second derivative of the square of the
distance from the moved centre, worked out from the axes, and halved until it is settled or a point lands off. An arc
that lands within the tolerance is moved as it was, to the bit; of 40 000 arcs and edges under turns, skews and
stretches, 3 that landed 0.0010002 to 0.0010072 off are refused, and no other moved. Moving 20 000 arcs under a stretch
costs up to a third more, 26 ms against 34 on .NET 10 and 36 against 41 on .NET Framework 4.8.

**FIXED.** A circle, an arc, an edge, a ball and a rectangle moved by a transformation whose axes are one length and
square to each other to a share of 1E-9 are judged by how far they land, as under any other: they were moved whole,
their radius times that share past the tolerance at large sizes, and X stretched by a share of 4E-10 and scaled by 1 000
took the arc of radius 1E5 to one 0.04 off. A circle, a ball or a rectangle that would land further than the global
point tolerance off is refused; an arc or an edge goes through where its ends and its middle land, where that lands
within the tolerance, and is refused otherwise. Of 20 000 circles, balls and rectangles and 40 000 arcs and edges under
turns, skews and stretches, the 45 that landed 0.001 to 0.23 off are refused or land within the tolerance, and 6 circles
and balls the rule bounds by more than they land, 0.0005 to 0.0009 off, are refused with them. An arc moved whole is
asked where its two ends land as well: the arc of radius 2 630 000 from 1.77 radians sweeping 1, X stretched by a share
of 4E-10 and moved by (5, 7, 9), had its start land 0.0010108 off; of 62 208 such arcs in the plane, and as many in
space, 968 landed further than the tolerance off before, and none does now.

**FIXED.** `GeoTransform3.Transform(GeoPlane3)` and `GeoPlane3.TransformBy` carry a plane under an even scaling of 1E5
or more, and so `GeoCircle3.TransformBy` a circle on it: the normal carried by the inverse transpose, a
hundred-thousandth long, was read against the vector tolerance and the plane refused. It is made of unit length by its
own length now, dividing as the plane's constructor divides, so a plane it took comes out to the bit as it did; the
circle of radius 10 scaled by a million is the circle of radius 10 000 000. A circle stretched under such a scaling is
refused as stretched, with an `InvalidOperationException`, where the plane threw an `ArgumentException` first.

**CHANGED.** `GeoObb3.IsDegenerate` reads a whole side against the point tolerance, as the box's faces read it: a plate
50 by 30 and 0.0015 or 0.002 thick is a box at a tolerance of a thousandth, where half its side was read and it was
flat, and one 0.0005 thick is flat. `ToCells` of such a plate lays its cells now, where it laid none, and refuses a
division into cells no thicker than the tolerance, as for any box; clashes and the BVH do not read it.

**CHANGED.** `Validate` calls a body enclosing nothing, `NoVolume`, where it encloses no more than half the point
tolerance times its area: a slab t thick encloses half its faces' area times t, so a slab no thicker than the tolerance.
Read against the whole tolerance, a slab up to twice the tolerance thick enclosed nothing, while the booleans cut and
give such slabs: the common part of a cube of 100 and a block running 0.0015 into its top, 1.8 cubic millimetres, is
valid now. Of the booleans of boxes with chips, slabs and voids 0.0001 to 0.5 thick, 960 such slabs are valid now, each
the same body; the net volumes of a take-off of a column, a beam, a slab and a stiffener overlapping 0.0015 take their
common parts as bodies where they sliced them, and move in the last digits: 9 000 097.919999965 is 9 000 097.919999946,
4 860 144.30000002 is 4 860 144.299999987 and 9 001 569.300000468 is 9 001 569.299999667, and turned far from the origin
they move by 5.7E-6, 5.5E-6 and -1.6E-5. A square rod encloses a quarter of its side times its area, and so encloses
nothing up to twice the tolerance across, where it did up to four times.

**CHANGED.** A union, a common part, a difference and the cutting in of openings cut a void, a chip or a slab thicker
than the point tolerance and a trillionth of the size of the coordinates, however thin it is, and take no notice of one
no thicker. A cell of the cut no thicker than twice the tolerance is placed by the middle of its thickness, which lies
within the tolerance of both its sides, and was read against the other body within the tolerance, on its skin rather
than in it: a cube of 100 less a box 40 by 30 and 0.0015 thick inside it was the cube, 1.8 cubic millimetres too much,
the same with a box 0.002 thick, and with such a box as an opening; and a block running 0.0015 into the cube's top
joined it holding the chip twice, 0.8 too much on 256 000. Such a middle is read within a quarter of the cell's
thickness now, and a point found further than the tolerance inside its cell within half the tolerance, where the
tolerance reads it on the other body's skin. The cube less a box 0.0005 thick is the cube, as it was, and a chip, a slab
or a gap exactly the tolerance thick on a turned body, that thick to a few bits either way, is read as it was. Of 1 650
booleans of boxes with chips, slabs, voids and openings 0.0001 to 0.5 thick or off a face, cut at 0.001 and at 0.01,
turned and not, and three cuts one after another, 275 moved, every one a feature thicker than the tolerance and no
thicker than twice it, or an opening exactly the tolerance off a face (below); none came out open or not valid that was
not before, and the common parts that are slabs no thicker than twice the tolerance read as enclosing nothing to
`Validate`, as such slabs then did (those thicker than the tolerance are valid now; above). Of 2 700 unions, common
parts and differences of a cube turned at random with chips, slabs and gaps exactly the tolerance thick, none moved.
Still as in 12.2.0: of 1 600 unions and differences of a column and a beam the tolerance into or clear of it, placed at
random, 4 hold a volume outside both readings, by up to 24 cubic millimetres; and bolt holes stopping exactly the
tolerance short of a plate's face leave the skin on some placements far from the origin and not on others.

**FIXED.** A body whose opening stood the planar tolerance off a face, to the last bit, came out of the cutting of its
openings open. The cut read a corner on one side of the opening's plane by its distance, 0.0010000000000000009, and
within the point tolerance of where its edge crossed the plane by where that crossing was rounded to,
0.0009999999999976694 off it, and the piece of the face between them lost a corner: a box 100 by 60 by 40 with an
opening 0.1 across 0.001 off three of its faces came out of `TryCutOpenings` open by 25 faces and 7.66 cubic millimetres
short. Cutting openings in, a corner is read on a plane now where it stands off it by no more than the planar tolerance
and a trillionth of the size of the coordinates, so the opening is taken with the outside, closed, at 239 999.998 97. Of
4 356 openings 0.1 to 1.9 across a hair either side of the tolerance off one to three faces of that box, turned, far
from the origin and not, 692 came out open and 10 not valid, and none does now, each holding the volume of one reading
or the other. A union, a common part and a difference read a corner as they did: read so there, a slab exactly the
tolerance thick in or beside a turned body was on the plane at some corners and off it at others, and of 2 700 such
results six that were valid came out open or wound two ways.

**FIXED.** `ConvexHull3.Of` and `TryOf` close and hold every point within the tolerance of their triangles, and
`GeoObb3.Fit` every point, on plates down to a few thousandths thick, and they no longer run out of memory. The hull
took a point only where it stood further than the tolerance off the plane of a face it saw, and the faces of a plate a
few thousandths thick meet at its rim so nearly flat that a point 10 off the rim stands within a thousandth of every
plane; reading which faces a point sees by a test that rounds, it also came out open, wound against itself, or folded
over itself. Of 300 sets of 40 points on plates 0.002 thick, every hull was open or wound against itself and 185 left a
point out, by up to 24, and the boxes fitted round 82 left one out, by up to 13. And where the rim a point left was not
one loop, each further point multiplied the faces: 200 points in a plate 100 by 100 and 0.002 thick ran out of memory,
as did 500, 120 within a thousandth of a cube's faces, 10 000 in a cube and 3 000 on a sphere, and with them
`GeoObb3.Fit` and the `Depth` of a clash. The build stops at the first rim that is not one loop, or as soon as it stands
more triangles than a closed surface on the points taken can have, and a hull that does not close, folds back over
itself, or leaves a point further than the tolerance outside it is built again with an exact test of sides, from the
widest tetrahedron the points give, each point left out only where it is within the tolerance of the triangles. Those
sets take from under a millisecond to an eighth of a second, the whole process in 70 MB at most. Of 9 600 sets on plates
0.0005 to 10 thick, turned and not, none leaves a point out of its triangles and no box leaves one out; merging the
triangles of one plane within the tolerance, as before, stands a face up to 0.0011 off a point on 5 of them. A hull that
closed and held every point is the same to the bit: of 600 sets of 200 points in a cube, on a sphere, on a cylinder,
near a box's faces, on a lattice with copies a hair off its corners, far from the origin and turned, none moved, and one
that was open with 2 983 faces is right now. The check costs up to half as long again on sets of 200 points, and up to
two and a quarter times as long on lattices with copies.

**CHANGED.** `GeoObb3.Fit`, and the `Depth` of a clash read from it, keep of the boxes standing on faces of the hull
that are as small to rounding the thinnest, their least sides compared first, then the middle and then the longest,
where they kept the first found; on one face the rectangle is the first of least area found, as before. A prism 40 long
on a right triangle with legs 18 and 24 was fitted the box 18 by 24 by 40 along its legs, whatever order its corners
came in, and is fitted the one 14.4 by 30 by 40 along its long side, as large; a block 18 by 24 by 40 meeting a prism on
its corner's triangle is 14.4 deep, where it was 18. The clash benchmarks are unchanged to the bit.

**CHANGED.** `GeoArc2.TransformBy`, `GeoEdge2.TransformBy`, `GeoArc3.TransformBy` and `GeoEdge3.TransformBy`, and the
curved chains and loops through them, judge every transformation that is not a turn, a shift, a mirror or an even scaling
by where the arc lands: the arc through where its ends and its middle land is given only where it lands within the global
point tolerance of where the transformation puts the rest of it, asked at every sixteenth of its sweep, and an
`InvalidOperationException` is thrown otherwise, as for a stretch. A skew that keeps both axes equally long was taken
through the three points unasked, and `GeoEdge3` was never judged at all: a turn of 0.7529 radians of a stretch of
1 + 4.29E-9 and a shear of 9.28E-5, scaled by 55.57, took the arc of radius 9 069.6 to one 9.16 off, and the skew that
sends X to (1, 0) and Y to (0.6, 0.8) took the arc of radius 100 to one of radius 66.09 that stands for no part of the
ellipse its circle becomes; both are refused now, while the skew that sends Y to (0.000001, 1) moves that arc 0.0000069
off. The landing is read from the moved start, across the arc, so an edge 100 long bowing by 1E-12, of radius 2.5E13,
moves under a turn rounded to six places, where reading it from its centre refused it for want of digits. Of 20 000
random arcs and edges under skews, stretches and similar maps, in the plane and in space, those that moved landing further
than the tolerance off, by 2 049 points along them, are refused: 1 195 arcs and 1 195 edges in the plane, 966 arcs and
3 165 edges in space; none that moves has moved by a bit, and a turn, a shift, a mirror or an even scaling moves every
one as before. At most 3 in each family that land up to 1.2E-3 off between the sixteen points asked still move.
`ArcTransform2FaultLN2SkewTests`, `CircleTransformFaultLN1SkewTests` and `FlatArc3Tests`, which pinned the arcs and the
edge under those skews to the arc through their three points, are pinned to the refusal and to a slight skew that lands.

**FIXED.** `Arc3.GetIntersections` against a triangle, a polygon, a face or a box, and the circles and curved chains
through it, gave no point where a tilted arc came within the point tolerance of a side, a rim, a corner or a face without
crossing it, though it collided: an arc of radius 5 standing upright 0.0006 beside the side of the square 30 by 20 and
dipping to 0.0006 over its plane, 0.00085 from it. Such an isolated touch is given the point of the arc nearest the shape
now, as a segment touching a box is, and an arc lying level over the material within the tolerance is given none, as a
curve lying on a surface crosses it nowhere. Level is a length, not an angle: the arc lies level where it stands off the
plane by no more than the planar tolerance round its circle, as `IsCoplanar` reads it, so the arc of radius 300 tilted
9E-6, which rises 0.0027 round its circle, touches the face where it dips to 0.0006 over it. Of 20 000 arcs near the
sides, rims and corners of each shape, a third of them within 1.4E-4 of flat, 2 786 near the triangle, 2 191 near the
polygon, 2 473 near the face and 809 near the box that collided and were given no point gain their nearest point: of those
tilted any way 1 776, 1 137, 1 403 and 354, the rest a hair off flat but not level; the 101 tilted ones near the box that
are given none lie inside it. Every point found is within the tolerance of both, and no other answer moved; cutting a
curved chain by a face or a box still cuts only where it crosses, to the bit as before. A segment beside a box reads level
alike now, within the planar tolerance of the plane of the face or the line of the edge at both its ends, where it read
the vector tolerance as an angle: of 200 000 segments near the box 10 by 10 by 10, 3 022 short ones lying that close lose
their touch point and 88 long ones rising further gain one, the segment 800 long tilted 9E-6 over the top of a slab among
them; cutting by a box is unchanged to the bit.

**FIXED.** A segment held within two slabs of a box it runs outside of was given a crossing read off the third slab that
stood up to the tolerance off each of the two faces, so up to 0.00106 from the box: 3 of 100 000 segments near the box
10 by 10 by 10, and 14 rays and 3 chains through them. Such a crossing is the point of the segment nearest the box now,
within the tolerance of it; nothing else moved, and cutting a segment or a chain by a box is unchanged to the bit.

**CHANGED.** The queries of a triangle, `CollidesWith`, `GetIntersections`, `Locate`, `Contains` and `TryGetBarycentric`
among them, read no area by `GeoTriangle3.IsDegenerate`, the height over the longest side against the point tolerance,
where they read twice the area against the vector tolerance, an area against a number with no size: the triangle 0.003 by
0.003 had no area to them, so a segment through its middle neither collided with it nor crossed it and its centroid was
outside it. A triangle with no area is the segment it has collapsed to now, rather than touching nothing: it collides with
what comes within the tolerance of it, by the exact gap its distance has always measured, is crossed where its sides are,
and holds the points within the tolerance of them, `OnSide`, and none inside. `TryGetBarycentric` gives false for a
triangle with no area by `IsDegenerate` at the global tolerance, so for the triangle 100 long and 0.0005 high, twice its
area 0.05, which it gave true for, it gives false; containment and the crossings read the coordinates at the tolerance
they are asked within, so at 1E-5 that triangle holds a point in its middle and a segment through it crosses it, and a
body with an edge chamfered 0.0004 across, prepared at 1E-5, holds the points just inside the chamfer. A triangle whose
corners lie on one line to rounding has no plane at all, as `Normal` reads it, and is measured as its sides: an arc
passing 9E-16 from the middle of one did not collide with it and was given two points, and a point on a side of another
stood 0.843 from it. A thin triangle that has a plane stands off it by the rounding of its normal, which the gap of an arc
reads now. Of 27 000 pairs of a small or thin triangle and a segment, a ray, a polygon, a face, a triangle, a box, a body
or an arc, 11 137 collided otherwise than their distance said and 3 183 were given points where they did not collide;
none is now, and no distance moved. An ellipse against such a triangle is answered as its sides too, where it touched
nothing or threw "no normal". Triangles with area by both readings answer as they did, to the bit. Meshing, booleans and
splitting keep the reading of the area, as they did. `Collision3ThinShapesTests`, `GeoTriangle3Tests` and `InvariantTests`,
which pinned a triangle with no area to touching and holding nothing, are pinned to the new reading.

**FIXED.** A curved chain cut within the point tolerance of one of its corners broke there, between the two edges, and was
cut inside the edge as well, leaving a piece shorter than the tolerance; where the cut stood on the corner to rounding the
piece had no plane, and `TrySplitBy` threw: 28 of 12 000 chains cut by boxes, faces and bodies, a third of them with a
corner within 0.0012 of a face. Such a cut breaks the chain at the corner alone now. None throws, and the answers that
moved, 4 028 cut by an axis-aligned box, 1 031 by an oriented one, 377 by several, 1 427 by a face and 1 108 by a body,
are every one of them chains cut within the tolerance of a corner, a piece shorter than it gone.

**FIXED.** A circle, an arc, an edge, a ball and a rectangle are moved by a transformation that is not an exact turn, shift,
mirror or even scaling only where they land within the global point tolerance of where it puts their points, and refused
otherwise. `GeoArc2.TransformBy`, `GeoEdge2.TransformBy` and `GeoArc3.TransformBy` read the two axes as equally long
against the global vector tolerance outright, a number with no size, while a circle and a ball read them as a share of the
stretch: under a turn rounded to six places unevenly and scaled by 1 000 the circle was carried and every arc on it threw,
and the share let a true stretch through at a large scale, where the shape landed visibly off: a stretch of 3E-6 along X
scaled by 1 000 moved the circle of radius 100 0.3 off and the arc of radius 100 from 0.5 to 2 radians 0.017 off, and
scaled by 1E5 an arc a hair short of a whole turn 46 off. A circle, the whole turn of an arc and a ball are refused now where their radius times the
difference of the two axes and their lean, the dot product over the longer, exceeds the tolerance, the ball reading its
three axes in pairs; an arc whose axes differ in length is the arc through its ends and its middle where that lands within
the tolerance of where the transformation puts the rest of it, asked at every sixteenth of its sweep; a rectangle reads
its axes as a circle as large as its half diagonal. A turn whose numbers are rounded to six places or to a float on their
own keeps the axes alike to the last digit in the plane and moves every shape at any scale, within 4E-8 at 1E5; in space
such a turn leaves them some 1E-6 apart, and moves the circle of radius 100 at the scale it is drawn, 0.000065 off, and
refuses it scaled by 1 000, where it would be 0.065 off. A skew that keeps both lengths takes an arc through its ends and
its middle as before. Of 1 500 random arcs, edges and circles of radius 0.01 to 1 000 under 45 maps in the plane and 35
in space, none that moves has moved by a bit; 13 425 that moved landing further than the tolerance off are refused, and
30 008 that threw move, landing within it. Of 2 000 rectangles up to 500 by 500 under 60 maps, 4 910 that moved are
refused and 253 move that were refused, none moved otherwise, and a scaling to nought still gives a rectangle of no size.

**FIXED.** The constructor of `GeoEdge3` read the length of the chord against the vector tolerance, a number with no
size, and refused any chord of 1E-5 or less: an edge 1E-6 long could not be built, a short arc moved under a turn rounded
to a float threw "Two points in the same place have no chord to bulge", and so did 75 of 12 000 chains cut by an oriented
box near its surface and 91 cut by an axis-aligned one, the cut leaving a piece a hair long. A chord is refused now only
where its two ends are one point to the last digit, as an edge in the plane refuses only a chord of nought, and moving
an edge reads the side it bows to by its length alone. Of 40 000 random edges with chords from 1E-9 to 1 000, at four
tolerances, every one built before is built, read, moved and split to the bit as it was; 8 992 refused at the tolerance
given and 13 160 at the global one are built, and 9 843 moves and 13 298 splits are answered that threw, as are 68 of
the small arcs in space of the transformation fuzz. A piece a billionth of a chord that short has a chord whose
direction is rounding, and is refused as not lying in the plane of the edge. Of those chains, 13 and 14 still throw, cut so
near a corner that the piece's two ends are one; the cuts themselves are as they were. `new GeoEdge3(arc)` read its chord
against the global vector tolerance in the same way, and refused the arc of an edge 1E-6 long as "sweeping a whole turn",
so that edge could not be extended; it tells a whole turn by its sweep now, as `GeoEdge2` does.

**FIXED.** `Arc3.GetIntersections` against a triangle, a polygon or a face, and the circles and curved chains and loops
through it, gave points where `CollidesWith` was false: a point of the arc is read as on the region within the point
tolerance of its plane and of its outline, and the two together reach further than the tolerance off a side, so an arc
starting 0.00107 beside a side and 0.0007 over the plane, 0.0013 from the region, was given a point, for 123 of 6 000 arcs
starting near the side of a polygon and 229 near the rim of a hole. The points are kept now only where the arc touches the
region, as a box keeps them, and none are; a triangle with no area, which touches nothing, is crossed nowhere. Of 12 000
random arcs near a side, a rim or a corner and anywhere, at three tolerances, 23 lose their point against the polygon, the
face or the triangle, 51 answers, and in none of them did the arc collide; the chains through them lose the same points,
the circle of one of those arcs its point against the triangle, and against boxes and bodies, and in cutting by them,
nothing moves. A curved chain cut by a face is cut only at those points, so a spurious cut at a corner 0.0021 from the
face is dropped. Arcs in the plane against polygons had no such fault.

**FIXED.** A segment, a ray, a line without ends or a straight chain that touched a box within the point tolerance without
crossing a face collided with it and crossed nothing, since the slabs of the axes it moves along are read exactly: 19 256
of 200 000 segments near an edge of the box 10 by 10 by 10. It is given the point of it nearest the box now, as the other
shapes give a touch: the segment from (5, 10.1, 9.9007) to (5, 9.9007, 10.1), 0.0005 off the edge, touches it at
(5, 10.00035, 10.00035). `CollidesWith` and a crossing found agree on all 200 000, both ways round, for oriented and
axis-aligned boxes, rays, lines and chains. Of 60 000 random segments at three tolerances, 5 583 gain the point and none
loses or moves one; in 40 straight chains a crossing within the tolerance of a touch is named by whichever is found first,
at most 0.92 of the tolerance from it. A segment wholly inside the box still crosses nothing, and cutting a segment, a
chain or a curved chain by a box cuts where it crosses a face only, to the bit as before. The touch is the same point
whichever way round the shape is given, and it is given only where the shape touches the box at one place: one lying
along the box within the tolerance, within the planar tolerance of the plane of the face it stands over or the line of
the edge it runs with all along, such as a chain 0.0005 over the top face, is given none, as the body of the box and an
arc in the same place give none.
A segment barely moving across a pair of faces is held
within them only where both its ends are within the tolerance of them, so no crossing stands further than the tolerance
from the box (28 crossings of 60 000 segments did) and the answer does not turn on which end comes first. A ray starting
within the tolerance of the box and running away from it, which every corner stands behind, touches it at its origin,
where it was given nothing and collided: the ray from (5, 5, 10.0005) up along Z, and 21 of 100 000 rays near the box in
every direction.

**FIXED.** The fillets and the offsets of curved chains and loops in the plane met two circles with the difference of the
squared radii taken as the difference of two squares, which keeps only the last digits of two radii near 2.5E8 a few
apart: the circles of radius 250 000 007.25 about the origin and 250 000 000 about (40, 30) were crossed 0.035 from where
they cross. It is the product of the difference and the sum now, as the crossings of two circles take it; on 20 000 such
pairs the worst crossing is 4.7E-6 off where it was 2.0. Of 3 000 random chains and loops filleted and offset, 1 263 moved:
by at most 3.5E-5 where an edge some 100 long bulges no more than 1E-6, its radius 2.5E7 or more, and by at most 2E-10
elsewhere.

**FIXED.** `GeoArc2.TransformBy` and `GeoEdge2.TransformBy`, and the curved chains and loops of the plane through them,
moved an arc by its centre and its radius under any transformation that kept both axes equally long, a skew among them:
the skew that sends X to (1, 0) and Y to (0.6, 0.8) took the arc of radius 100 about the origin from 0.5 to 2 radians
to radius 100 about the moved centre, its start 22.7 and its end 44.6 from where they land, and the edge of bulge 0.5 from
(0, 0) to (100, 0) ended 22.05 off, so that 13 844 of 20 000 random chains under skews came apart and were refused. The
plane follows space now: a turn, a shift, a mirror or an even scaling, the axes coming out square and of one length to a
part in 1E9 of the stretch, moves an arc whole as it did, to the bit on 20 000 random arcs, edges and chains each; any
other transformation that keeps both axes equally long gives the arc through where its ends and its middle land, the
middle read across the chord, so that arc is of radius 66.09 about (52.98, 20.16), the edge ends at (100, 0), every chain
stays joined, and the worst end of 20 000 skewed arcs, 1.6 times the radius off, is 1.5E-8 of it. An arc of a whole turn
is the whole circle and is refused under a skew as `GeoCircle2` is, and an arc past a half turn whose ends a skew brings
within the global point tolerance of each other is refused with an `ArgumentException`. Under a transformation that carries
a circle by that rule without being an exact turn, a turn whose numbers are rounded to six places on their own, such an arc
moves whole as the whole turn does, its ends 6.2E-5 off for a radius of 100 as they were, so that from a whole turn down
nothing between moves, throws and moves again as the sweep shrinks. An uneven scaling is refused as before.

**FIXED.** `GeoArc3.TransformBy` threw on a plain turn for an arc a hair short of a whole turn, its sweep from a whole
turn less 1E-15 to less 1E-6: its ends being too close to tell a chord between, the three-point path said "Two points in
the same place have no chord to bulge" or "The X and Y directions must not be parallel". A turn, a shift, a mirror or an
even scaling of the arc's plane, a stretch square to it among them, moves the arc whole now, by its centre and its
radius, so an arc of any sweep moves: of 20 000 random arcs under such maps the 5 723 that threw are answered, and the
11 078 others moved through three points move by at most 1.7E-8 of their radius at the ends, nearer the exact moved ends
in 5 612 and further in 3 853, their middles within 2.8E-8 of the radius where they were up to 1.1E-4 off. Any other
transformation still takes the arc through its ends and its middle, to the bit as before; an arc past a half turn whose
ends it brings within the global point tolerance of each other is moved whole too where the circle would be carried, a turn
rounded to a float or to six places, and refused under a skew with the `ArgumentException` documented now.

**FIXED.** Whether two arcs in the plane truly meet, which settles their distance and whether they collide, took the
difference of the squared radii as the difference of two squares, where their crossings take it as the product of the
difference and the sum: two near-equal arcs of radius 8 633 about centres 0.87 apart crossed at (96 116.465, 92 341.693)
and stood 1.5E-11 apart, and at a tolerance of nought did not collide. Both read it one way now. On 240 000 random pairs
of arcs, both ways round, near-tangent, near-concentric and ending at a crossing among them, `CollidesWith` and
`GetIntersections` are unchanged to the bit, and `DistanceTo` moved in 3 638, all arcs ending a hair from a crossing, by at
most 1.2E-10, between nought and a residual of rounding either way; at a tolerance of nought the 42 pairs that crossed
while standing apart are none.

**FIXED.** `GeoTriangle3.IsDegenerate` read twice the area, a length squared, against the vector tolerance, and
`GeoTriangle3.Normal` and `GetPlane` refused any triangle under the default vector tolerance whatever tolerance the caller
worked to: the surface mesh keeps real triangles 0.002 wide, and a body with an opening 0.002 from its corner threw from
`Normal` in 1 036 of 8 000 queries `GetIntersections`, `GetShortestLineTo`, `DistanceTo` and `CollidesWith` at a
tolerance of 1E-6. A triangle has no area now when its height over its longest side is within the point tolerance, or its
corners lie on one line to rounding; `Normal` is read from the triangle's own numbers and throws only for the second, never
for a triangle with area. Of 40 000 random triangles, `Normal` and `GetPlane` are the same to the bit in the 24 795 that
had them and given in the 15 205 that threw; `IsDegenerate` now says so for 9 777 slivers thinner than the default
tolerance it did not, and at a tolerance of 1E-6 and of 0.01 flips both ways. That body answers every query. The
queries and meshes of the library keep their own reading of no area, twice the area within the vector tolerance, so their
answers are the same to the bit: to them the triangle 0.002 by 0.002 still has none at the default tolerance, its centroid
`OutSide` it and a segment through it not colliding, where `IsDegenerate` calls it a triangle; moving them is a step of
its own. `GeoPolygon3` refused a loop under the vector tolerance in area as collinear: a square 0.002 by 0.002, of 4E-6
square units, and a triangle 0.002 across read with `ToFace3`. Such a loop is kept now where it has width, twice its area
over its span within the point tolerance otherwise: of 20 000 random small loops, 1 928 are kept and none refused or
changed; `GeoFace3.FromLoops` gives the same faces to the bit on all of them; and 1 500 pairs of boxes with chips and
slivers at the tolerance, joined, crossed and subtracted at 0.001 and 0.01, give the same bodies to the bit. The strip
mesh of a face drops pieces by their width already, as the surface mesh does (below).

**FIXED.** `GeoAabb3.GetIntersections` and `GeoObb3.GetIntersections` gave points where `CollidesWith` was false. A segment
that barely moves across a pair of faces was held within them when it ran no further outside than the point tolerance, on
each pair on its own, so that one running along X past an edge of the box 10 by 10 by 10, 0.000714 off both faces and
0.00101 from the box, crossed it twice; a ray, a line without ends and a straight chain went the same way. A face read an
end of an arc standing within the tolerance of its plane and of its outline as on it, so an arc starting 0.00064 off a
corner in each of the three directions, 0.0011 from it, crossed it once. Crossings are kept now only where the shape
comes within the point tolerance of the box, whichever end of a segment stands outside a slab it is held in, so the one
from (-15, 9.99995, 9.99995) to (1, 10.0009, 10.0009), 0.00119 from the edge, crosses nothing either way round, where it
crossed at (0, 10.001, 10.001) one way, and none of 200 000 segments near an edge crosses without colliding: among 6 000 shapes grazing a box at the tolerance in each family, the 160
segments, 177 rays, 180 lines, 117 chains, 1 277 curved chains and 1 916 arcs ending near a corner that crossed without
colliding (188, 206, 198, 135, 1 269 and 1 963 against an oriented box) cross nothing, and every other answer, triangles,
polygons, faces, circles, ellipses and splines among them, is the same to the bit.

**FIXED.** `GeoObb3.GetFaces` and `GeoObb3.ToSolid` refused a box at most twice the global point tolerance on some axis as
having a zero size there, so a plate 0.002 thick had no faces and no body. A side is nought now only when it is nought to
the rounding of the box's own numbers, a millionth of a millionth of how far it reaches from the origin, and each face is
its four corners with the normal and area its polygon would have: of 30 000 random boxes, a quarter of them with a side
from 1E-5 to 1, the 20 817 that had faces have them to the bit, and so do the crossings of an arc through them, and the
9 183 refused have them now. A thin box reaches every path that goes through `ToSolid` with it, collision, distance,
crossings and `TrySubtract` among them, where those threw; `GeoObb3.IsDegenerate()` still says it has no size at the global
point tolerance, as its tolerance says.

**FIXED.** `GeoEdge3.ToArc`, and `Length`, `MidPoint` and everything else that reads an edge in space as its arc, threw
"The X and Y directions must not be parallel" for any edge with a chord under 0.00316: the frame it is read in was built
from the chord and the normal across it, each as long as the chord, and the plane they span, their cross product the
chord squared, was asked of the global vector tolerance. An edge 0.003 long with a bulge of 0.5 is the arc of radius 0.001875
now, and a short arc moved under a turn rounded to a float, which goes through its ends and its middle, moves: of radius 1
sweeping 0.002, of radius 0.01 sweeping 0.3, of radius 10 sweeping 0.0003. The frame is read from rounding alone, made
orthonormal as before: of 40 000 random edges with chords from 1E-4 to 1 000 the 31 332 with chords from 0.00316 up read as
the same arc to the bit, and the 8 668 shorter, which threw, read as theirs.

The suite gains 61 tests for these seven, 38 of them red before.
`SurfaceMeshSmallFacesTests.ATriangleOfASmallFace_HasItsNormalInItsAreaVector` pinned the fault itself, a triangle 0.002
across with no normal; it asserts now that the triangle has area and its normal.

**FIXED.** Two circles whose centres stand no further apart than the point tolerance crossed nowhere, however far apart they drew
elsewhere. The circle of radius 1 000 about the origin and the one of radius 1 000.0005 about (0.0006, 0), as much as 1.1E-3 apart in
places, cross at (-833.33324, 552.77094) and (-833.33324, -552.77094), as the splines made of them find, and
`GeoCircle2.GetIntersections` gave none; nor did the arcs and edges on them, the curved chains and loops made of those, or
`GeoCircle3` and `GeoArc3` in one plane. Two circles that stay within the point tolerance of each other all the way round, their
centres and their radii no more than the tolerance apart put together, are one circle, as before: they cross nowhere and meet along
their length, so the circles of radius 1 000 and 1 000.00002 with their centres 1E-4 apart, 1.2E-4 apart at most, and two arcs of one
circle whose ends are rounded to six places, with their centres 1.6E-6 apart, are cut nowhere. Further apart than that, centres
within the tolerance are one centre only where the rounding of the numbers leaves the line the circles cross on nowhere in
particular: where the rounding of the largest coordinate or radius, times the sum of the radii over the distance between the
centres, is more than the point tolerance. The difference of the squared radii is worked out as the product of the difference and
the sum of the radii, which keeps the digits the squares of radii a thousand long lose. On 60 000 random pairs of circles in the
plane, checked against the crossings worked out to 80 digits: of the 1 902 whose centres are within the tolerance but which draw
further apart than it, 10 are given crossings where they had none, within 9.2E-14 of them; the 11 504 within the tolerance of each
other all round are unchanged; and the 5 375 others whose crossings moved by rounding came nearer in 3 343 and further in 2 021, by at
most 7.6E-8, the worst error falling from 5.2E-5 to 2.1E-7, 39 of them over 1E-6 before and none after. The arcs and edges on such
circles move alike, 11 pairs of arcs given crossings and none of 24 498 pairs of edges near one centre; in space 16 pairs are given
crossings, within 9.4E-8, and the worst error of the others fell from 4.2E-4 to 1.8E-4. What is worked out from the crossings of arcs
moves by that rounding with them: of 3 000 random curved loops offset, 415 moved, by at most 4.7E-10. `CollidesWith` and `DistanceTo`
are unchanged.

**FIXED.** `GeoTransform3.Transform(GeoCircle3)` and `GeoCircle3.TransformBy`, and `GeoTransform2.Transform(GeoCircle2)` and
`GeoCircle2.TransformBy` in the plane, took a circle to stay round when the two axes came out equally long, to the vector tolerance
in drawing units, and never asked whether they came out square to each other. The skew that sends X to (1, 0, 0) and Y to
(0.6, 0.8, 0) keeps both lengths, so the circle of radius 100 came out a circle of radius 100, where it is an ellipse of radii 126.5
and 63.2. Round means now that the two axes come out equally long and square to each other, to the default vector tolerance of the
square of the stretch, the rule `GeoSphere3` reads its transformation by (both read since by how far the shape lands, in the
entry above), so that a turn whose numbers are rounded to six places or to a float still moves a circle; anything else is refused with the `InvalidOperationException` an uneven scaling gets, and
`GeoEllipse3.FromCircle(circle).TransformBy` or `GeoEllipse2.FromCircle(circle).TransformBy` gives the ellipse. On 60 000 random
transformations in space and 60 000 in the plane every turn, shift, mirror and even scaling gives what it gave to the bit; the skews
and shears beyond the tolerance that came through are refused, 2 287 in space and 6 399 in the plane, and so are 36 slight uneven
scalings of a tilted circle in space, whose axes came out equally long to 1E-5 but off square by more. An arc keeps its rule: `GeoArc3.TransformBy` is the arc through where its ends
and its middle land, skewed or not, to the bit as before on 60 000 random arcs, and only an arc of a whole turn, which is the whole
circle, is refused under a skew as the circle is; `GeoArc2.TransformBy` and the edges of the plane are unchanged (both have moved
since, in the entries above). An even scaling of
about 1E5 or more of a circle in space is still refused, with an `ArgumentException`, since the normal of the plane carried with it
comes out shorter than the vector tolerance. `GeoSphere3` is unchanged.

**FIXED.** `GeoArc3.ProjectToArc2`, and `PlanarMap.ProjectToArc2` through it, took any arc sweeping within the global angle
tolerance, a whole degree, of a whole turn for the whole circle from angle nought: the arc of radius 10 about the origin in the plan
from 1 radian sweeping 359.5 degrees came down as the whole circle, its start 19.2 from where it is. An arc is the whole circle now
only when it sweeps a whole turn. One within a degree of it is laid out from its centre, as it was, its start and its end being too
near each other to find a circle through three of its points, but from where it starts and as far as it sweeps. Every other arc
comes down to the bit as it did: of 60 000 random arcs in the plan and tilted, the 13 241 that moved all sweep within a degree of a
whole turn. An arc tilted to the frame comes down as part of an ellipse, which an arc cannot be, so one within a degree of a whole
turn, laid out on its own radius, stands off where its points come down: some 0.056 for that arc with its normal (0.2, 0, 1).

**FIXED.** `DxfWriter.Add(GeoRay2)` and `Add(GeoRay3)` took a `default` ray and wrote a RAY with a direction of nought, which AutoCAD
does not read. It is refused with an `ArgumentException` before anything is written, as a line without ends of no direction is.
`SvgWriter` wrote the picture of a point, a ray or a line without ends alone with a view box of nought by nought, which shows nothing,
and a segment 2E-6 long along X with one of height nought. An axis the view box would hold less than a millionth of, margin and all,
the six places its numbers are written to, is given a unit now, centred on what is drawn: a point, a ray or a line without ends alone
is a view box 1.1 by 1.1, so that the ray along X from (5, 5) alone runs to x = 5.55, and that segment one 0.100002 by 1.1. Every other
picture is written byte for byte as it was: of 2 000 random pictures, the 573 that changed all had a view box nought or a millionth
wide or high.

**FIXED.** Two rays crossed wherever the sine of the angle between them was above 1E-15, however far out the crossing fell, so two
rays parallel but for the rounding of the points they were built through read nought apart and touching wherever that crossing lay
ahead of both: a ray rebuilt through two points a hundred apart of another moved 10 to its side did so in 34 793 of 100 000 pairs at
coordinates of ten thousand. Two rays follow the rule of `GeoXLine2` now: at a sine of 1E-9 or less a crossing further along than a
double can place within the point tolerance, measured by the larger of the two distances along so that both ways round agree, is no
crossing, and those pairs are 10 apart in 100 000 of 100 000 at coordinates of a thousand to a million. `DistanceTo`, `CollidesWith`
and `GetShortestLineTo` between two rays change only there: of 60 000 random pairs, the 1 150 that changed are all at a sine of 1E-9
or less. At any real angle two rays still cross however far out, `TryIntersectWith`, which reads the angle tolerance, is unchanged,
and segments, which do not share the rule, are unchanged.

**FIXED.** `GeoBvh2.GetIntersections(GeoRay2)` cut the ray past the box round everything indexed and asked every edge the segment it
was cut to, so an edge nearly along the ray was judged parallel or not over a length the farthest edges set: the edge from (0, 0) to
(100, 0.0005), parallel at a thousandth to the ray from (-50, 0) along X, was crossed at (0, 0) once an edge a million away was
indexed beside it. Each edge is asked the ray itself now, as the plain scan asks it, and a branch is left alone where the ray, by a
slab test, never enters its box grown by the point tolerance, as `GetIntersections(GeoXLine2)` does. Among 3 000 rays over edges
near-parallel to them and far from them the answer is the same points as the scan's (in tree order) every time, where 8 differed; of
60 000 random rays over random edges, 30 220 crossings moved by rounding, by at most 4.2E-9, to the scan's own to the bit, and in 64,
at a tolerance of 0.01, the tree keeps the other of two crossings within the tolerance of each other that the scan merges. Pruning
by the box of the cut segment let nearly every branch in for a diagonal ray: 200 diagonal rays over 90 000 edges take 50 ms where
they took 1 345 (20 where 532 on .NET 10), and 200 rays from inside 19 where 216.

The suite gains 51 tests for these six, 24 of them red before.

**NEW.** `GeoXLine2`, an infinite line in the plane: a point on it and a direction of unit length, running both ways
for ever, as the XLINE of AutoCAD does and as `GeoRay2` does with no start. It is made from a point and a direction or
through two points, moved with `Translate` and `TransformBy`, and measured along with signed distances
(`GetPointAtDistance(-30)` on the line from (-50, 50) along X is (-80, 50), `GetDistanceAtPoint` gives the foot of a point).
`ToLine(from, to)` and `ClipTo(rectangle)` turn it into a segment for drawing (the line from (-50, 50) along X clipped to the
square 100 by 100 with a corner at the origin is the segment from (0, 50) to (100, 50); a line that misses the rectangle by no
more than the point tolerance is clipped to the rectangle grown by it, and one that misses by more gives null), `ToRay()` into
its half ahead of the origin, and `GeoRay2.ToXLine()` and `GeoLine2.ToXLine()` go the other way. `Offset(distance)` is the
parallel line to its left, `GetPerpendicular(point)` the one through a point square to it, `GetSide(point)` says which side a
point is on, `IsPointOn`, `Locate`, `IsParallelTo` and `IsSameLineAs` (the same infinite line whatever its origin and direction)
ask the usual, and equality reads the fields. It answers the five queries (`DistanceTo`, `CollidesWith`, `GetIntersections` and
`TryIntersectWith`, `GetShortestLineTo`) about every shape in the plane, as a ray does and from either side, so
`square.DistanceTo(line)` is `line.DistanceTo(square)` to the bit, and so are the crossings and the crossing of a line with a
segment or a ray: a point, a segment, a ray, another line, an arc, a circle, an ellipse (the ray and the segment leave the
ellipse to the ellipse; the line is asked of it from both sides), an edge, a polyline, a polygon, a curved chain, a curved loop,
a rectangle, a triangle and a face. A line through the square is nought from it and crosses it at (100, 50) and (0, 50), in that
order; the parallel line 100 to its left is 50 from it; two lines side by side are as far apart as they are whichever way they
run, and nought apart where they cross: always at a sine of the angle above 1E-9, whatever the tolerance, and at a sine of 1E-9
or less unless the crossing, further out than a double can place it within the point tolerance, is rounding (4.5E12 along
for a thousandth, measured by the larger of the two distances along, so that both ways round agree); a line offset by 10 and
rebuilt from two of its own points, a sine of 1E-14 off at coordinates up to a million, is 10 from it, not nought, in 100 000
of 100 000 pairs, and at coordinates of ten million, a sine of 1E-11 and a crossing 1E12 along that can be placed, 62 % of
them still read nought. Against a shape with an end to it the line is answered as
the segment that reaches max(1, 100 point tolerances) past it each way, a millimetre at the default tolerance, which is how the ray
is answered with one end to cut instead of two; an arc counts for its two ends and for the point of its circle furthest along
the line each way where the arc holds it, so an edge with a bulge of 1E-10, on a circle of a radius of 1E11, does not stretch the
segment to 1E11. The suite draws 4 000 random shapes and lines of each of the 16 kinds on every run, 64 000 pairs, against a
closed form that shares no code with the library, from both sides and against the line taken as two rays, with one in three of
the loops and chains carrying an edge of a bulge of 1E-10 to 1E-3, and a fuzz of 4 000 rectangles for `ClipTo`; a separate run
of 100 000 of each kind, 1.6 million pairs, and 100 000 rectangles passed as well. The suite gains 185 tests for the line, and
nothing that was there changes: every earlier test and the benchmark signatures are as they were.

`GeoXLine3` is the same line in space: a point and a direction of unit length, made, moved (`Translate`, `TransformBy`,
`GeoTransform3.Transform`) and measured along, turned into a segment (`ToLine(from, to)`), a ray (`ToRay()`) or a line of the
plane (`ProjectToXLine2(frame)`, back with `GeoXLine2.ToXLine3(frame)` and `PlanarMap.ToXLine3`), cut to a box for drawing
(`ClipTo(box)`: the line from (-50, 50, 10) along X through the box 100 by 100 by 20 with a corner at the origin is the segment
from (0, 50, 10) to (100, 50, 10), one half a thousandth over its top is clipped to the box grown by the tolerance and one two
thousandths over gives null), and `GeoRay3.ToXLine()` and `GeoLine3.ToXLine()` make one. It answers the five queries about every
shape in space a ray is asked about, from either side to the bit, and an ellipse from both sides as the line in the plane does: a
point, a segment, a ray, another line, a plane, a triangle, a polygon, a face, a polyline, a box of either kind and a body, and,
to be asked whether they touch and where they cross, an arc, a circle, an ellipse, an edge, a curved chain and a curved loop
(a curve in space is not measured, for a line as for a ray). That line is nought from the box and crosses it at (0, 50, 10) and
(100, 50, 10); the parallel line 100 beside it is 50 from it. Two lines in space are skew unless they meet: as far apart as their
common perpendicular is long (the line along X through the origin and the one along Y through (0, 0, 30) are 30 apart, whichever
way each runs), nought where they cross, always at a sine of the angle above 1E-9 whatever the tolerance, and at a sine of 1E-9 or
less unless the perpendicular, further out than a double can place it within the point tolerance, is rounding, which is the rule
of the line in the plane. The perpendicular is worked out with each component of the cross product of the directions taken as
the difference of two products to a rounding of its own, so two lines a hundred apart at a sine of 1E-8 are held to the exact gap
to 3E-14 with their origins near each other, 3E-9 with origins ten million apart along them, worked out on whole numbers from the
doubles they are made of, where the plain arithmetic is out by 4E-7, and the segment that joins them is as long as their distance
to what a double can place its ends to; a line starting within the planar tolerance of a plane touches it, as a ray does, and
one that starts further off is nought from it where a double can place the crossing (a line 7 above the floor tilted a
ten-thousandth of a radian crosses it 70 000 along) and as far as its origin where it cannot: the line through (0, 0, 100) along
(1, 0, -1E-11) is 100 from the floor, where its ray, reaching it 1E13 along, is nought. Against a shape with an end to it the line is answered as the segment that reaches max(1, 100 point tolerances) past it
each way: a body for the corners of its box, a circle or an ellipse for its centre and its radius, an arc for its ends and the
extreme of its circle along the line where it holds it; an ellipse shares the segment's reading of its rim at the point tolerance,
so a line that passes it at a slant at 0.0009 may not be seen to touch it. `GeoPlane3.TryGetIntersectionLine(other, out GeoXLine3 line)`
gives the line two planes share, as the ray form does: the floor and the wall x = 100 meet in the line along Y through
(100, 0, 0). The suite draws 3 000 random shapes and lines of each of the 18 kinds on every run, 54 000 pairs, against an oracle
that shares no code with the library, from both sides, as two rays, turned round, moved along itself and moved by a rigid motion,
with 20 000 boxes for `ClipTo`, and 18 000 pairs of two lines and 12 000 of a line and a plane held to exact arithmetic; a separate
run of 100 000 of each kind, 1.8 million pairs, and 100 000 boxes passed as well. The suite gains 256 tests for the line in space.

Both lines go the rest of the way into the library. `GeoBvh2.GetIntersections`, `GeoBvh3.GetIntersections` and, with
`CollidesWith`, `GeoPreparedSolid3` take a line as they take a ray, and give what the plain scan over every edge or triangle gives (each
edge or triangle is asked the line itself, so a near-parallel edge is judged by its own length, whatever else is indexed), without looking at
a branch whose box the infinite line never enters, whichever side of its origin the mesh lies, but for a crossing within rounding of the tolerance: the line through (3, 6, 4) along Z crosses the mesh of the box 10 by 10 by 10
at (3, 6, 0) and (3, 6, 10), where the ray from the same point reaches only the second, and in the plane the line through
(500, 50) along X crosses the square 100 by 100 at (0, 50) and (100, 50), where the ray pointing away crosses nothing.
`DxfWriter.Add(line)` writes the XLINE entity of the R2000 file for either line (`AcDbXline`, the origin in groups 10, 20, 30
and the unit direction in 11, 21, 31), on a layer, and refuses a line with no direction; `SvgWriter.Add(line)` draws it across
the picture from edge to edge both ways, its origin helping to fit the picture as a ray's does, and `ObjWriter.Add(line, length)` and
`Wkt.Write(line, length)` write the segment of that length centred on the origin, as the ray's writers take a length (the line
through (10, 20) along X, written 100 long, is `LINESTRING (-40 20, 60 20)`). The viewer
draws `GeoXLine2` and `GeoXLine3` as scene kinds, through the box of the scene and a twentieth of its diagonal past it, with a dot
at the origin, shows the origin and the unit direction among the properties of the item, picks it and snaps to the nearest point of
it, and reads the XLINE of a DXF file. `GeometryHelper.CadConvert` converts the `Xline` and `Ray` entities both ways,
`ToGeoXLine3` and `ToAcadXline`, `ToGeoRay3` and `ToAcadRay`. Nothing that was there changes: the ray's queries, the writers of the
old shapes and the benchmark signatures are as they were.

**NEW.** `GeoSphere3`, a solid ball in space: a centre and a radius above nought (nought, a negative radius, an infinite one and one
that is not a number are refused). It is read as filled, as a body is and as the disc of a circle is, so a point inside it is nought
from it; `SignedDistanceTo` keeps the depth, tied to `Locate` as for the other volumes (the point (20, 0, 0) is -30 from the ball of
radius 50 about the origin), and `DistanceToSurface` measures to the shell from either side. It has `Volume` (523 598.8 for a radius
of 50), `SurfaceArea` (31 415.9), `GetAabb()`, `Contains`, `Locate`, `IsPointOn`, `GetClosestPointOnBoundary` (the first axis one
radius from the centre, for the centre itself, where every point of the surface is as near), `Translate`, `TransformBy` and
`GeoTransform3.Transform`, which keep a ball a ball under a turn, a shift, a mirror or an even scaling (a radius of 50 scaled by 3 is
150) and refuse a scaling of one axis, a shear and a scaling to nothing, as a circle is refused its stretch, and equality,
`IsEqualTo`, `IsValid` and `ToString`. There is no `FromThreePoints`, as the circle has none.

A ball is a point grown by its radius, so its gap to a shape is the distance from its centre to that shape less the radius, and nought
where the shape reaches into it. The shape's own distance to a point does the measuring, so each is read as it reads a point: a
polygon, a triangle and a face as filled surfaces (a ball of radius 30 about (5, 0, 0) is 5 from the square 200 by 200 with a hole 80
by 80 about the origin, a hole not being material), a circle and an ellipse as the discs they are, a curved loop to its boundary,
a box and a body as their material. `DistanceTo`, `CollidesWith` (the gap no more than the point tolerance: the plane 50.0005 above
the ball of radius 50 about the origin touches it, the plane 50.002 above does not) and `GetShortestLineTo` are answered about a
point (which asks `Contains` and `IsPointOn` for a collision, as it does of every shape), a segment, a ray, an infinite line, an
arc, a circle, an ellipse, an edge, a polyline, a polygon, a curved chain, a curved loop, a triangle, a face, a plane, a box of
either kind, a body and another ball, and every one of those answers them about the ball, to the bit; the shortest line leaves the
surface of the ball for the point of the shape nearest its centre, is as long as the gap, and is the other side's turned round. Unlike
the line and the ray, which are not measured from a curve in space, a ball is, since the distance from a curve to a point has a closed
form: the arc of radius 100 about the origin in the floor, from (100, 0, 0) a quarter turn, is 50 from the ball. Where the shape
reaches into the ball the line has no length and lies at the point of the shape nearest the centre, the centre where a body holds it;
two balls that meet have one at the middle of the stretch of their line of centres that both hold. The nearest point of a polygon
or a face is that of `ProjectToPolygon` and `ProjectToFace`, which keep a foot within the point tolerance past an edge, so the line
may end that far beyond it.

`GetIntersections` and `TryIntersectWith` give the points where the surface of the ball is crossed by a segment, a ray, an infinite
line, an arc, a circle, an ellipse, an edge, a polyline, a curved chain, a curved loop, and by the boundary of a polygon, a triangle
or a face, in order along the line, the curve or the boundary, a corner on the surface once. It is the surface that is crossed, not the
ball: the line through (0, 30, 0) along X crosses the ball of radius 50 about the origin at (-40, 30, 0) and (40, 30, 0), the line
through (0, 50, 0) grazes it at (0, 50, 0), a segment from (-10, 0, 0) to (10, 0, 0) lies inside, collides with the ball and
crosses nowhere, and the square from (-100, -100, 0) to (100, 100, 0), which holds the ball in its middle, is crossed nowhere
while the strip from (-30, -100, 0) to (30, 100, 0) is crossed at (30, -40, 0), (30, 40, 0), (-30, 40, 0) and (-30, -40, 0). A curve
in a plane meets the surface where it meets the circle the plane cuts from it, by the library's own crossing of two curves in a plane.
A plane and another ball cross in a circle, not in points: `TryGetIntersectionCircle` gives it, and `TryGetTangentPoint` the one point
where they only touch within the point tolerance, which is never given with a circle (for two balls halfway between the surfaces on the line of centres, so within the tolerance of both; shells that cross within the tolerance of each other, or a plane that cuts less than that deep, touch at a point though the true circle can be a few units across, 4.5 for a radius of 10 000). `ToSolid` and `TriangulateSurface` refuse a default ball, and a ball of a radius below about 0.01 meshes into a body the library may call open at the default tolerance. The floor cuts the ball of radius 50 about
(0, 0, 30) in the circle of radius 40 about the origin, and the ball of radius 50 about the origin and the ball of radius 30 about
(50, 0, 0) share the circle of radius 28.62 about (41, 0, 0), square to X, the same asked from either ball to the bit but for the
sign of its normal, which points from the ball asked to the other. A body or a box is asked whether it touches the ball and how far
from it, and where their surfaces cross is the clash's question.

`ToSolid(chordTolerance)` and `TriangulateSurface(chordTolerance)` mesh the ball as an icosahedron whose twenty faces are each cut
into n by n triangles, every corner pushed out onto the sphere, with the least n for which no triangle strays further than the
tolerance: a ball of radius 50 within 0.1 is 3 380 triangles, within 0.01 it is 30 420, which hold 0.036 % less than the volume. The
body is closed and faces outwards, a corner shared by two triangles the same to the bit in both so that nothing is welded, and a
tolerance that needs more than 1 048 576 triangles is refused, as it is for the chords of a circle (radius 50 within 0.0001 would
need three million; a ball of radius 1 can be cut within 5.7E-6 and not within 5E-6); nought picks 0.2 % of the radius. A ball goes
into a clash as `ball.ToSolid(0.01)`. The suite gains 169 tests, with an oracle in closed form that shares no code with the library
(36 000 pairs of a ball and a shape of each of the 18 kinds a run, 360 000 in one separate run), and
fuzz of balls against balls and planes (40 000 a run, 4 million in a separate run), which hold the distance, the collision, the
crossings, the circle, the touch and the shortest line to the formulas and the same from both sides to the bit; nothing that was
there changes.

**NEW.** The ball is written, shown and read from IFC. `ObjWriter.Add(GeoSphere3, chordTolerance, name)`, `DxfWriter.Add(GeoSphere3,
chordTolerance, layer)` and `Wkt.Write(GeoSphere3, chordTolerance)` write the triangles of `TriangulateSurface(chordTolerance)` as a body
is written as its triangles: a ball of radius 50 within 0.1 is 3 380 triangles, as OBJ faces, 3DFACEs with every edge visible, or a `POLYHEDRALSURFACE Z`,
and the same bytes as writing those triangles as a mesh. The tolerance is a finite number above nought (a nought for automatic is refused, as it is for a circle's chords)
and one that needs more than 1 048 576 triangles is refused, as is a `default` ball, with nothing written; SVG, a drawing in the plane, has none.
Every earlier output is byte for byte as it was. The viewer has the kind `GeoSphere3`: the scene writer takes it, scenes keep it, and it is drawn as the
mesh the library cuts at the chord tolerance of the scene in at most 262 144 triangles (a ball of radius 50 is 255 380, none further than 0.0012
from the sphere, in 80 ms), with a dot at its centre and three great circles, which are exact, so its centre snaps as a centre, its circles are
picked and snapped to, a clip plane through it leaves the cap of the disc it cuts, and its properties give the centre, the radius, the volume and the
surface area; a ball whose mesh would pass the cap is cut coarser and is warned of where that leaves the mesh further from the sphere than ten point
tolerances (a radius of 500 is). All the balls of a scene share a budget of four million triangles, over which every one is cut at one coarser
tolerance and the scene is warned of it once (40 balls of radius 100, 10.2 million at the chord tolerance, are 3 808 800 triangles within 0.0062).
A ball is not a body of the viewer's clash check, which takes the mesh of `ToSolid`; the gap check measures it exactly.
`GeometryHelper.IfcConvert` gains `IfcStoreCache.GetSpheres(guid, options)`: the `GeoSphere3` of each `IfcSphere` among the body items of a product, at
the root of an `IfcCsgSolid`, or in a mapped item, its centre and radius as the file gives them (scaled by the options, placed by the mapping and the
product as a solid is: a uniform scale of the mapping scales the ball and an uneven one is refused with a warning), with `IncludeAggregatedParts` those of
the parts it aggregates, where `GetSolids` goes on giving the engine's mesh, 1.2 % under its volume for a radius of 0.5 m. A sphere in a Boolean result is not read,
and with `ApplyVoids` an element that openings cut gives none, a warning naming it, a ball with a void in it not being a ball. Every call writes one Info line.

**NEW.** `GeoSpline2` and `GeoSpline3`, the NURBS curve in the plane and in space: one smooth sweep in place of a chain of pieces, a
B-spline whose control points may pull with different strengths, which is what the SPLINE of AutoCAD and the B-spline curves of IFC
hold. A spline is a degree (1 to 10), control points, knots and, if it is rational, weights; it is clamped, so it starts at its first
control point and ends at its last, and the knots are kept as given (nothing is rescaled to [0, 1] or reordered, so a spline read from a
file goes out as it came). They are checked (a clamped knot vector with no inner knot repeated more than the degree, a weight above
nought for each point and the largest no more than 1E12 times the smallest, since past that the length cannot be worked out right: a
ratio of 1E16 came out 1E-7 short and one of 1E20 two fifths short; control points not all one point) and immutable, what they hand out is
read-only. Two kinds of parameter are told apart by name, as the other curves keep theirs: the members named for a parameter
(`GetPointAtParameter`, `GetTangentAtParameter`, `GetCurvatureAtParameter`, `GetParameterAtPoint`, `GetDistanceAtParameter`, `Split`)
take a share of the domain from 0 to 1; those named for a knot (`GetPointAtKnot`, `GetDerivativeAtKnot`, `GetTangentAtKnot`,
`GetCurvatureAtKnot`, `GetKnotAtPoint`, `SplitAtKnot`) take a value of the domain, `DomainStart` to `DomainEnd`, and
`GetKnotAtParameter` and `GetParameterAtKnot` turn one into the other (8.5 is the parameter 0.5 of the domain [5, 12]); those named
for a distance (`GetPointAtDistance`, `GetParameterAtDistance`, `GetDistanceAtParameter`, `GetDistanceAtPoint`) go by length.
`FromControlPoints` spaces the knots evenly over [0, 1], `Interpolate(fitPoints, degree = 3)` is global interpolation with
chord-length parameters (Piegl and Tiller, 9.2.1) solved as one banded system, 100 000 points in 58 ms, and with `startTangent` and
`endTangent` as well (a tangent gives a direction only: the curve leaves at the speed it keeps on average, the length of the chain
through the points over [0, 1]); both take a `Tolerance` that decides which neighbouring fit points are one point, and points just
above it make the curve swing far (the chain (0, 0), (100, 0), (100.0011, 0.0005), (100.0022, 0), (200, 50), (300, 0) kept at the
default tolerance is met at every point and swings 507 000 outside the box of the points, where at a tolerance of 0.01 it stays within
13). `FromPolyline`, `FromArc` and `FromCircle` make a spline that is exactly the polyline, the arc or the circle: a circle is nine
control points of a rational quadratic, every point of it at the radius to 4E-14 at a radius of 100, and 628.31853071796 long.
`GetPointAtKnot` is de Boor's scheme in homogeneous coordinates, `GetDerivativeAtKnot` any order to ten, `GetTangentAtKnot` (the first
derivative that does not vanish, so a spline whose first two control points coincide still has one; it throws where a span has all its
control points one point and the spline stands still), `GetCurvatureAtKnot` (signed in the plane, never negative in space; 0.01 all
round a circle of radius 100), `Length` (Gauss-Legendre of ten points over each Bézier span, cut where the rule over the whole and its
halves disagree, cut at each place the speed is least, where a curve that turns back has a corner in what is integrated, and a
rational span with weights more than two to one apart cut first), `GetAabb` (the box the curve fills, from the roots of the derivative
on each span: 300 by 112.5 for the cubic (0, 0), (100, 150), (200, 150), (300, 0), whose control points fill 300 by 150),
`Split(parameter)` and `SplitAtKnot(knot)` (the knot raised to the degree: two splines that are the spline exactly and meet at one
control point, (90, 94.5) for that cubic at 0.3; a cut that would leave a part with all its control points one point is refused),
`Reverse` (the same domain; each distinct knot is mirrored once, so equal knots stay equal and distinct ones stay distinct, and the
result is checked: a knot 1E-17 from the start, or two knots a unit in the last place apart, no longer give an invalid spline),
`Translate`, `TransformBy` (exact for every affine map by the control points: a circle scaled 3 along X is the ellipse 300 by 100, all
of it), `ToSpline3(frame)` and `ProjectToSpline2(frame)` through `PlanarMap`, and `Flatten(chordTolerance)`, whose chord promise holds
however fine the tolerance and whatever the weights: a span is cut in halves until every control point of a piece is within the
tolerance of its chord, or, for a polynomial piece, its second differences, or, for a conic, the height of its middle control point, say
so with less cutting (the circle of radius 100 within 0.01 is 256 chords, where 223 would hold; the cubic above 128); a tolerance that
needs more than 1 048 576 pieces is refused. Points are asked of it: `DistanceTo`, `GetClosestPointOnBoundary`, `GetKnotAtPoint`,
`GetShortestLineTo` and `IsPointOn`, from either side (`point.DistanceTo(spline)` is the same number to the bit), exact to rounding: on
each span the places where the distance stops falling are the roots of a polynomial in Bernstein form, all found, none missed however
close two lie, and a span is solved only if the box of its control points is nearer than the best found, the boxes being kept in a tree
so that a query does not look at the rest (the next entry). The
centre of a circle is as near every point and gets the lowest parameter, 0. The suite checks the points and the derivatives against
exact fractions worked out from the Cox-de Boor recursion (worst 11.7 units of rounding of the size of the spline for the point, on 800
random splines of degree up to 10 in the plane and space, and 6E-14 for a derivative, on 500), the distances and the box against a
dense sampling polished in 28-digit decimals (4.3 and 2.4 units of rounding), the length against Romberg's integration (1.3E-15 of
the length), the parameter at a length, the cuts, the reverse, the tangents and curvature, the interpolation (a fit point met to 3
units of rounding, an end tangent to 6E-15) and the chord promise (the curve was at most 0.976 of the tolerance from the chain), and
again on splines a million away from the origin, with control points repeated, all on one line, with a span a millionth wide, with
weights a thousandfold apart, with a cusp, and of degree up to 10. The suite gains 169 tests, and nothing that was there changes:
every earlier test and the benchmark signatures are as they were. Not in this release: an offset (the offset of a spline is not a
spline), splines as the edges of a curved chain, periodic knot vectors,
and the writers, the viewer and the bridges.

**NEW.** The five queries of `GeoSpline2` about every other shape in the plane, from either side: `DistanceTo`, `CollidesWith`,
`GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`, each with a tolerance and without, asked of a segment, a ray, a line
without ends, a polyline, a polygon, a rectangle, a triangle, a face, an arc, a circle, an ellipse, an edge, a curved chain, a curved
loop and another spline, and each of those answers them about a spline by handing the question to the spline's member, so
`polygon.DistanceTo(spline)` is `spline.DistanceTo(polygon)` to the bit, the crossings are the same points and the shortest segment the
same turned round (two splines are worked out from the one that comes first in a fixed order, by degree, number of control points, then
the numbers, and give the places in order along the one asked, the same points bit for bit). The rules are those of the other curves. A
closed shape is a region: the ring (the circle spline of radius 50) inside the square from (-100, -100) to (100, 100) is nought from it,
and the shortest segment, which runs from the spline to the outline as it does for every curve, is 50 long. The distance is nought only
where the two truly meet and otherwise the gap, measured exactly and never through the tolerance (a line 0.0005 over the ring is 0.0005
from it), and `CollidesWith` is the distance being no more than the point tolerance, exactly. A place is where the two meet or where the
distance of the spline from the shape is least within the point tolerance, so a crossing, a touch and an end that comes near are each
found, and a stationary point of the distance that is a greatest, or a point on a slope, is not: the line y = 30 meets the ring at
(40, 30) and (-40, 30), the line y = 50 touches it at (0, 50), the line 0.0005 over that touches it there at a thousandth and does not at
a ten-thousandth, and a line 1E-6 inside the top of the ellipse of radii 100 and 50 meets it at x = -0.02 and 0.02 and not in the middle,
where the distance is greatest, which a 2 000-point fit of an arc of radius 1 000 against its tangent line at the top gave nine places
for, from the start of each span on either side of the touch, and gives one for now. Places no further than the point tolerance apart,
one from the next, are one place, and so are two near touches next to each other along the spline within ten times that with no point
between them farther from the shape than at either by more than four snaps (the rounding of the numbers leaves a curve that touches
another as near it as the digits tell all along a stretch, and finds a place there for each digit); two crossings are one place only
when they are within the tolerance of each other, whatever lies between them, as for the exact curves: two crossings 6E-4 apart are
one place at a thousandth and two at a ten-thousandth, a line that cuts a circle of radius 30 a million from the origin in a chord of
0.009 meets it twice, for the spline as for the `GeoCircle2`, and a line tangent to a circle of radius 1 000 touches it once (a polyline
of 10 000 segments a million from the origin, where the numbers are 1.2E-10 apart, up to three places within a hundredth of a
millimetre). Two curves that cross at a shallow angle, so that they run side by side a hair apart between two crossings and the nearest
points of the two there are a greatest of the distance, with no slope for Newton's method to go by, are told apart by the offset of
the one from the other along the normal, which changes sign where they cross: a line 40 or 4 000 long that cuts the circle spline of
radius 1 000 in a chord of 0.004, 0.02, 0.1 or 1 meets it twice, at x = +-chord / 2, from either side, as it does the `GeoCircle2`, where
it met it once or never, and the distance is nought. Where a spline lies along the shape for a length, to rounding (a degree-one span along a segment, a
spline cut at a knot beside the whole, the spline of an arc beside the arc, a copy of itself) it is nought from it and collides with it
and crosses it nowhere, and no point is given along the stretch, as for a polyline along a segment: the corner (0, 0), (100, 0),
(100, 50) as a spline meets the segment from (-50, 0) to (150, 0) at (100, 0) only. Against an ellipse or another spline the same holds
where the two stay within the point tolerance of each other, level (the distance changing by no more than half of it), for a quarter of
the size of the smaller, seen from both of them: two circles of radius 1 000 and 1 000.0005 are 0.0005 apart, collide and give no place at a thousandth, and at
a ten-thousandth are apart and give none, 0.0005 for `DistanceTo` either way, a sine of 21 fit points and its copy moved a ten-thousandth,
a half of a thousandth up are along each other, and so is a spline fitted through 37 points of a quarter circle along the circle
spline of the same radius, where 15 541, 15 101 and 123 places came before, after 10, 22 and 21 seconds, and none comes now, in a few milliseconds
(the fit gives the ten crossings that go more than 1E-5 to each side, all in its first and last tenth, and against the `GeoArc2` of the same arc, which is
solved exactly, its 72 true crossings). A stretch that either curve leaves by more than the tolerance is cut out and the stretch cut in
two there: a polyline 2E-4 off a line that goes out a unit and back in a dart crosses it at (506.0002, 0) and (507.9998, 0), from either side
(the line was looked at but not the polyline, so from one side the dart was lost). A sign change of the offset inside a stretch, by more than a hundredth of the
tolerance to each side, is a crossing and is given: two circles of radius 1 000 and 1 000.00002 with their centres 1E-4 apart cross at
(-200, +-979.8) at an angle of 1E-7. A touch is not level (its distance goes from nought to the tolerance within a few
units) nor is a crossing at any angle, so each is a place. Against a segment, a ray, a line without ends, a circle or an arc a spline is
along the shape only where it is the same to rounding. Against a segment, a ray or a line without ends the
signed offset of each Bézier span from the line is a polynomial in Bernstein form, and against a circle, an arc or an ellipse the quadratic
form of the curve laid into the span is one of twice the degree; their roots are the crossings, all found, none missed, to rounding, and
their critical points the touches and the nearest approach; the distance to those is exact to rounding. An ellipse is also a rational
spline of four spans, and against it and against another spline both are cut in halves while their hulls (measured by their boxes and by
the shadows of their control points on the chord of each part and its square, so that two parts side by side are told their distance) are
near enough to matter, until each part is straight to a thousandth and the two cannot run the same way, or to a billionth where two parts
could cross twice, and Newton's method on the two curves, then on the distance itself along the first, brings the nearest pair to
rounding. For the nearest approach the parts are taken nearest bound first, whichever pieces they are of, and the cutting ends when the
nearest bound left is no nearer than the best found less a hair, a billionth of the size of the shapes (0.001 at a size of a million
millimetres) and never more than a thousandth of the distance itself, and, short of the budget below, a crossing is never passed over for a nearer approach found first:
a line a million long and a cubic that starts 0.005 above it and falls through it at
791 288 are nought apart, where they read 0.005 and did not collide, and a near miss of 0.0009 at a million is 0.0009, and not 0.01 because
a hair of a hundred-millionth of the size left the pair that holds it uncut, as it did. The distance to an ellipse or a spline is that of a real pair of points, no nearer than
the truth and within the hair of it, and is rounding wherever Newton's method settles, which is wherever the curves cross or the nearest
points are not parallel. Two curves side by side at one distance all along, as two circles 0.0005 apart, could be cut to the hair only by
cutting every part, which is past a budget of 40 000 pairs: then the curves are looked at at 128 points, the least of them is brought to
the real curves, the answer is that best, a real pair of points, so that the truth lies between it and the nearest bound left, which one Debug line gives with it (a meeting not yet found is not in the answer: the circle splines of radius 10 000 and 10 000.0005, the second with its centre moved 0.001, cross twice and read 6.3E-7 apart, a known limit). The circles of
radius 100 000 and 100 000.0005, the second with its centre moved 2E-4 toward 45 degrees, are 0.0003 apart, in 0.2 s, where the
cutting depth first gave 0.000518, at the wrong place. The places are cut to a budget of two million pairs, past which one Debug line is
logged, which takes two splines that stay within the tolerance of each other for a stretch that is not level: two near-concentric circle splines that cross, of radius 100 000 and 100 000.0005 with centres 0.01 apart, reach it after 24 s and their two crossings are not given, a known limit.
Two splines side by side a hair apart for 6 000 take about 0.15 s for `DistanceTo`.
The arch (the parabola y = x (300 - x) / 200) and a copy of it 20 higher are 11.3358552330596 apart, the ends of the copy being nearer
than the 20 over the top, and the arch and its upside-down twin cross at (38.20, 50) and (261.80, 50). A ray or a line without ends is cut to
the stretch the spline's hull can reach; a polyline, polygon, rectangle, triangle, face, chain or loop is its edges, an edge farther than
the best found passed over, and a place that another edge is nearer to is not one of the shape; the places found are merged by a grid, so that a
hundred thousand of them cost a hundred thousand looks, and the edges asked whether another is nearer, and the near touches asked whether
they are one, are looked up in a grid of the edges' boxes, so that a polyline of a million edges across a cubic takes 2.2 s and one of 80 000 0.3 s. `GeoSpline2` and `GeoSpline3` point queries are faster as well: the boxes of the
spans are kept in a tree, built on the first query (about 3 ms for 100 000 spans), so that the spans near the point are reached without looking
at the rest, and the answers are the same to the bit (120 splines in the plane and 60 in space, 7 200 queries in all, and 120 and 60 of more than
24 spans, their bits hashed the same before and after): a point query at 100 000 spans of a spiral takes 0.010 ms where it took 0.53 ms, 0.012 ms
where it took 0.70 ms under .NET Framework 4.8, and 6.8 and 2.6 microseconds where it took 9.0 and 9.3 at a thousand; a spline of up to 24
spans looks at its boxes one by one, which is faster for so few. A segment, a line or a circle against a cubic of nine points takes 0.01 to 0.03
ms, an ellipse 0.2 to 0.5 ms, another spline 0.03 to 0.5 ms, a sine of 3 000 spans against a line through its 29 zeros 0.12 ms, a polyline of 100 000
edges 0.1 to 0.17 s (0.16 to 0.42 s under .NET Framework 4.8), and a spiral of 100 000 spans against a line 16 ms the first time and nothing after. The suite
checks every kind against oracles that share no code with the library (the spline sampled densely over every span and finer where it runs within a
segment's length of the shape, crossings found by bisection on a function that changes sign across each piece of the shape, distances by
golden section round each least of the samples, ellipses by Eberly's bisection, two splines by a grid and a compass search): 2 240 pairs of
random splines and shapes of the 14 kinds on every run, held to the oracle's places, the crossings and the near touches within the tolerance,
count and positions; 330 pairs each of a spline and a line, a circle and an ellipse that touches its tangent from a ten-thousandth to
three thousandths either side (those at the tolerance itself, and an ellipse a spline hugs level for a quarter of its size, left out: 274 for the ellipse); 686 of splines a million from the origin, with repeated control points, all on one line, with spans a
millionth wide, of degree up to ten, with weights a thousandfold apart or with a cusp, and 24 pairs of splines; every query from both sides,
at three tolerances, for 5 040 pairs and 200 pairs of splines, the same to the bit; 61 hand cases with the numbers worked out for each shape;
22 near-degenerate ones (tangents, crossings at the ends, partners through a cusp, a loop that crosses itself, a spline of an arc along that
arc, coordinates a million out); 45 more at a hundred thousand and a million from the origin and with splines of two thousand and forty thousand spans,
one for each way a reviewer made the first version fail, and 39 for the second round (crossings a hair apart, a dart out of a stretch, polylines of 80 000 edges, circles a hair apart with their centres moved or the same); and the tree against a plain scan. The worst distance error was 2.5E-15 of the size of
the spline, and 6.4E-9 of it for splines a million and 30 million from the origin, where neighbouring numbers are 1E-10 and 4E-9 apart. The suite gains 213 tests (50 of them in the round after the first review, 39 after the second),
and nothing that was there changes: every earlier test and the benchmark signatures are as they were. Not offered: `SignedDistanceTo`,
`Contains` and `GetClosestEdge` (a spline has neither an inside nor edges), and a `TryIntersectWith` that gives one point (the places are an
array); the shapes in space follow with the spline in space.

**NEW.** The five queries of `GeoSpline3` about every other shape in space, from either side: `DistanceTo`, `CollidesWith`,
`GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`, each with a tolerance and without, asked of a segment, a ray, a line
without ends, a plane, a polyline, a polygon, a triangle, a face, a box, an oriented box, a solid, a ball, an arc, a circle, an
ellipse, an edge, a curved chain, a curved loop and another spline, and each of those answers them about a spline by handing the
question to the spline's member, so `plane.DistanceTo(spline)` is `spline.DistanceTo(plane)` to the bit, the crossings are the same
points and the shortest segment the same turned round (two splines are worked out from the one that comes first, as in the plane).
The rules are those of the other curves in space. A flat shape, a triangle, a polygon or a face, is filled: a spline that pierces it
or lies in it is nought from it and its shortest segment has no length, and it is pierced where the spline passes through as well as
crossed at its outline (the ring, the circle spline of radius 50 about the origin in the floor, pierces the polygon standing across
y = 30 from (-100, 30, -10) to (100, 30, 10) at (-40, 30, 0) and (40, 30, 0), in order along it). A circle and an ellipse are the discs they are for the
distance, as their own distance to a point reads them, and rims for the crossings: the ring through the middle of a standing disc is
nought from it, collides with it and crosses nothing. A box, an oriented box, a solid (its openings cut in: the ring down the middle of
a plate's opening is 10 from its walls) and a ball are material, so a spline inside is nought from them and crosses nothing, and its
shortest segment runs out to the skin: the ring inside the box 200 by 200 by 20 is nought from it and 10 from its skin. A plane is a
surface without ends, a curved chain and a curved loop are the paths they are (the ring inside the square outline of a loop is 50 from
it, not nought), and the distance is nought only where the two truly meet and otherwise the gap, measured exactly: it never reads the
tolerance, the planar tolerance is not read at all, and `CollidesWith` is the distance being no more than the point tolerance, exactly.
In space two curves seldom meet, so a place is where the distance of the spline from the shape is least within the point tolerance: a
crossing is such a place where it comes to nought, and the tolerance alone tells a touch from a near miss (the line 0.0005 over the top
of the ring touches it at (0, 50, 0), the line 0.002 over it touches nothing). A stationary point of the distance that is a greatest is
not a place, nor is a point on a slope, and one that is a least with no curvature is: a spline over a line that is tangent to it, a
thousandth above its plane, has a distance of the fourth power there, and is told from a level one by being higher a thousandth of
the size of the spline either way and lower nowhere on the way (the humps between two near touches, a few snaps deep, are no place,
which a first try took for one), or a hundredth or a tenth inside the piece, for a least of the sixth order: the cubic y = x³ / 100 under
the line along x half a thousandth above its plane touches it once. A least that curves up is one only where the distance falls neither
way, so the line y = 0.0001 crosses that cubic once, at x = 0.21544, and the inflection, where its offset stops and goes on, is no place;
and a least beside a crossing does not join two crossings further apart than the tolerance into one. The arch, the cubic (0, 0, 0), (100, 0, 150), (200, 0, 150), (300, 0, 0), meets the plane z = 100 at
(100, 0, 100) and (200, 0, 100), is 0.5 from z = 113 and 7.5 from the ball of radius 30 about (150, 0, 150), and enters the box from
(100, -10, 100) to (200, 10, 150) through an edge at (100, 0, 100) and leaves it through the other at (200, 0, 100). Against a
segment, a ray or a line without ends the part of a span's offset that stands square to the line is taken as two signed polynomials in
Bernstein form along two directions square to it: the span meets the line where both are nought, and a span in a plane through the line
has its signed offset in that plane, as in the plane, so a line a millionth of a radian off the tangent of a cubic in its plane crosses it
and is nought from it; out of such a plane the stationary points of the squared offset are brought to the least of the distance by Newton's
method on the distance and, where it has no curvature, by a golden section down the slope. Against a plane the signed offset of each span
is a polynomial (a dip a few snaps below the plane between two crossings is a greatest, no place), and against a ball the squared distance from the centre less the squared radius is one of twice the
degree; a flat shape is its plane taken where the foot of the place stands over it, and the segments of its outline; a box is its six
faces and twelve edges, and a solid its faces. Against an arc, a circle, an ellipse or another spline the pair is cut in halves nearest
bound first, with the nearest pair brought to rounding, as in the plane, the hulls measured by their boxes and by the shadows of the
control points on the chord of each part, on the two directions square to it and on the one square to both chords. Two splines that lie
in one plane, every control point within a snap of it, are worked out in that plane as two splines of the plane are and give the plane's
answers, to the bit in a plane square to an axis: two circle splines of radius 1 000 and 1 000.00002 with centres 1E-4 apart cross in two
places at an angle of 1E-7 in any plane, and the polyline spline through (0, 0), (100, 0), (100, 100), (200, 150), (150, 300) and its copy
moved (0.0003, 0.0004) along its plane cross at its three corners (a tilted plane is laid flat by directions of its own, so for two curves
a hair apart it gives what the plane gives turned, which can be a place more or fewer). An arc, a circle, an ellipse, an edge or a chain of
edges in the plane of a spline is laid flat with it the same way for the places: a circle spline of radius 100 moved (0.0003, 0.0005) crosses
its circle three times, as in the plane. Two that share no plane can still cross (a helix and a straight spline along its cylinder, once a
turn), and inside a stretch where they lie along each other a place is a least of the distance where the two would cross if brought into one
plane: the copy lifted 0.0002 off the plane comes nearest, 0.0002 apart, at the same three corners, and a copy with a wobble of 0.0001 that
never crosses the spline in the plane meets it nowhere lifted off it. In space the step of Newton's method on the pair is
written with the cross product of the two tangents, which keeps its digits where the product less the square of the dot has none, and is
taken with no damping first, since the damping of a trillionth of the trace goes a five-hundredth of the way along a matrix whose small
eigenvalue is 1E-7 of it. A piece lies in a plane, along a line or on a ball when it is within 1E-13 of the size of its coordinates and not of
the numbers measured from a point beside it, which a polygon turned about in space and a hundred thousand out carried into those
numbers and gave places for that were not there. Two circle splines of radius 100 000 and 100 000.0005 with centres 2E-4 apart, in any
plane of space, are 0.0003 apart, in about 0.15 s. Known limits: a million out in three coordinates a snap is 2E-7, and two circles
that cross at an angle of 1E-7 are within it for two millimetres either side of each crossing, which gives the places the rounding
gives; a line that cuts the circle of radius 1 000 in a chord of 0.02 a hundred thousand out (a depth of 5E-8, a snap of 3E-8) meets it
to the digits all along the chord; the cutting is bounded as in the plane, with one Debug line past 40 000 pairs for the nearest
approach and two million for the places; a polygon or a face of many corners is asked, for each place that stands over its box, one
pass over its corners; and a spline against an arc, a circle, an ellipse or another spline takes the time of two splines (the arch
against a circle 3 ms for the distance and crossings, where a ball takes 5 microseconds); a touch of two splines that share no plane, tangent
to each other, reads a snap or two from nought (6E-11 to 1.7E-10 for a spline about 1 000 across, where the snap is 1E-10). Two splines
within the tolerance of each other round a turn tighter than a millimetre have their parts measured once both are no larger than the
tolerance, so the copy of a degree-four spline of ten points lifted 0.0002 off its plane takes about 0.1 s in any plane. The shortest
segment from a spline inside a ball or a solid runs to the skin, where the ball's own question about a point gives the point itself and no
length. The suite gains 289 tests: 127 hand cases, one row for each shape and each query with the numbers worked out, an oracle that shares no code with
the library (the spline sampled densely over every span, the least of its distance to each piece found by golden section, shapes
written independently) over 1 620 random pairs of the 18 kinds and 1 800 more built to touch a spline's tangent within a few
ten-thousandths either way, with every query asked from both sides and the same to the bit; 1 080 pairs of a spline and a segment, a ray,
a line, a polyline, a polygon, a triangle, a face, an arc, a circle, an ellipse, an edge or a chain laid in space from the plane's and held
to the plane's answers (pairs whose answer turns on the tolerance, and pairs that run along each other, left out), and 15 splines and their
copies moved along a plane square to an axis held to the plane's places to the bit; 700 of nasty splines (a cusp, repeated control points, all on a line, spans a
millionth wide, weights a thousandfold apart, a million from the origin); and the hard cases (frames tilted and a million out,
crossings behind the ends of the spline, a dart, chords, a spiral of 100 000 spans, a polyline of 100 000 edges, a solid of 30 000
faces). Nothing that was there changes: the plane's answers hash the same, bit for bit, before and after, every earlier test and the
benchmark signatures are as they were, and the plane's spline pairs and queries take the same time to within 1 to 8 %.

**NEW.** The spline is written, shown, and read from AutoCAD and IFC. `DxfWriter.Add(GeoSpline2, layer)` and `Add(GeoSpline3, layer)`
write the R2000 SPLINE entity of the spline's own degree, knots, weights (only where it is rational) and control points, every number to
the bit and no fit points: flagged closed (1) only where its first and last control points are the same point exactly, as they are for the circle
`FromCircle` builds, whose last control point now repeats its first where it was worked out at a whole turn and came 2.4E-14 off it
(so `FromArc` of any whole turn ends on its start to the bit), rational (4) where a weight is other than
one, and planar (8), with the plane's normal turned up in its largest component as the extrusion, where its control points lie in one
plane to the rounding of their coordinates, as a spline in the plane always does. The knot, control point and fit tolerances are written
as 1E-10, and the extents of the drawing grow by the box the curve fills, not by its control points: the cubic (0, 0), (100, 150),
(200, 150), (300, 0) is 300 by 112.5. A file of eight such splines beside lines and arcs reads with no error and no fix in ezdxf's
recover and audit, and netDxf loads all eight; AutoCAD's own AUDIT has not been run on it. `SvgWriter.Add(GeoSpline2, chordTolerance,
colour)` draws a spline that is not rational and of degree 3 or less exactly, as the Bézier pieces it is cut into at its knots, a C, Q
or L command for each (that cubic is `M 0 0 C 100 150 200 150 300 0`), and any other, the circle among them, through chords within the
tolerance; `ObjWriter.Add(GeoSpline3, chordTolerance, name)` and `Wkt.Write(GeoSpline2, chordTolerance)` and `Write(GeoSpline3,
chordTolerance)` write chords, the corners `Flatten` cuts at, every one kept and the ends the first and last control points exactly: the
circle of radius 100 that `FromCircle` builds is 256 chords within 0.01. Every earlier output is byte for byte as it was. The viewer
has the kinds `GeoSpline2` and `GeoSpline3`: the scene writer takes them and a scene keeps them to the bit; they are drawn by a cut of
the viewer's own, never the library's `Flatten`, at the knots into Bézier pieces in one pass, each halved until it lies within the chord
tolerance of the scene and turns no more than 2 degrees (the cubic above is 1 722 chords at 1E-4), once for the count of the scene's points
and its drawing, so that a cubic of 40 000 control points is drawn in 0.8 s, and a scene over its cap of points cuts them coarser with its
other curves and says so; a Bézier piece is cut in at most 65 536 chords, and one that would take more says so. A spline's two ends snap as
ends, where its first or last piece has no length too, and the corners between its chords do not, its nearest point
and its pick go along the chords, within the chord tolerance of it, and its properties give its degree, the number of its control points,
whether it is rational, its domain, whether it is closed (its first and last control points one point exactly) and its length. The viewer's DXF reader reads a SPLINE by its control points,
knots and weights to the bit (100 random splines of degree up to 4, rational and not, come back equal), a `GeoSpline2` where it lies on
the floor with its extrusion up, and leaves one given by its fit points alone, a periodic one, or one whose weights do not match its
control points out with a warning. `GeometryHelper.CadConvert` converts AutoCAD's `Spline` to and from `GeoSpline3` through its
`NurbsData`, its numbers passed over as they are, and to `GeoSpline2` as it is seen in plan, exactly (`TryToGeoSpline2` gives false in place of a
throw); a periodic spline is refused, GeometryHelper's splines being clamped. It is checked to compile, and untested at run time.
`GeometryHelper.IfcConvert` gains `IfcStoreCache.GetSplines(guid, options)`: the `GeoSpline3` of each `IfcBSplineCurveWithKnots` and
`IfcRationalBSplineCurveWithKnots` of a product, an item of any of its representations (the 'Axis' of a member above all), an element of a
geometric set, the parent curve of a segment of a composite curve, the directrix of a swept disk (the part the disk runs along, its
`StartParam` and `EndParam` being values of the knots, cut out exactly), or in a mapped item (an uneven scale of
it carried exactly), its knots each repeated as the file says ((0, 0.5, 1) repeated (4, 1, 4) is 0, 0, 0, 0, 0.5, 1, 1, 1, 1), scaled and
placed as a solid is; a curve met twice in one place is read once, and a periodic curve, or one whose knots and multiplicities do not pair,
is left out with a warning. Every call writes one Info line. The suites gain 63 tests: 26 for the writers, 21 for the viewer, whose five
tests that run over every kind take the two kinds too, and 16 for IFC; one viewer test that took SPLINE for an entity the reader does not
read takes LEADER now.

**FIXED.** The surface mesh of a body dropped any triangle whose doubled area was under 1E-5, comparing a length squared with a
length, so a box 100 by 60 by 40 with a void 0.002 from its corner, which leaves a corner 0.002 thick, was meshed open along 15
edges (69 triangles); it is now closed (72 triangles), and 3 000 random voids near corners, edges and faces mesh closed where
538 did not. A triangle is dropped only when it has no width, within a millionth of the point tolerance. The same rule reads the
faces cut into strips, which are those whose holes touch the boundary, whose rings meet each other or themselves, or that the
clipping cannot reduce: on 6 000 random boolean pairs the union meshes that were open fell from 575 and 507 (two runs of 3 000)
to 73 and 72. Four that were closed now have one open edge about 0.001 long, where the strips keep a crumb of their own cut
points (1.6E-6 square units in one case) beside sliver faces left out by design; the results of the booleans themselves are
unchanged by that.

**CHANGED.** `GeoSolid3.TriangulateSurface` and `Triangulate`, and `GeoFace3.TriangulateSurface`, now return the triangles of
small faces. A face 0.002 by 0.002 at the default tolerance is its two triangles, and a box with a void 0.002 from its corner
meshes to 72 triangles, 6 of them like that, where it was 69. A triangle whose area vector, `GetAreaVector()`, twice its area,
is no longer than the vector tolerance (1E-5) has `GeoTriangle3.IsDegenerate(tolerance)` true, and its `Normal` and `GetPlane()`,
which read `Tolerance.Global`, throw: each half of that face, 4E-6, is one, where a triangle 100 by 0.0008, 0.08, keeps its
normal. `ToFace3(tolerance)` refuses more, a triangle with its area within the vector tolerance or two corners within the point
tolerance of each other, and meshes held such triangles before. Take the normal of a triangle of the mesh
from `GetAreaVector()`. A face thinner than the tolerance is still left out whole, and the call now writes one Debug line to
`GeometryHelperLog` saying how many faces and how much area were left out, counting only the faces left out whole: a slab 100 by
100 and 0.0008 thick, built within 1E-5, is 4 triangles at 0.001, its four sides gone, and 12 at 1E-5. The library's own
questions, the booleans, the preparing of a body and its volume, which mesh a body themselves, say nothing.

**CHANGED.** Results of the booleans, of cutting openings in and of the volumes `Measure3` reads move, towards the exact
answer, wherever a body has a face a few thousandths across, because the meshes they work from keep the triangles that were
dropped. In 5 000 random voids near the corner of a box 91 cuts changed, the largest error in volume falling from 2.7E-8 to
2.3E-10; in 3 000 random pairs of boxes 5 unions and 10 subtractions changed, one union from 48 006.583036559 to
48 006.577472420; and of 3 000 bodies with a void near a box corner, 580 volumes read by the `Surface` and `FlatFaces` methods
of `Measure3` changed, every one closer to the exact figure. The benchmarks do not cover these paths, and their signatures match.

**NEW.** `DxfWriter` writes the curves of space as the curves they are. `Add(GeoArc3)` is an ARC and `Add(GeoCircle3)` a
CIRCLE, each in the coordinate system of its own plane, so AutoCAD draws them as one entity that can be edited, dimensioned and
snapped to, not as a polyline of chords: a circle of radius 25 about (10, 20, 30) standing on the normal (1, 1, 0) is a
CIRCLE with extrusion (0.7071, 0.7071, 0) and centre (7.0711, 30, 21.2132), where 0.1 of chord tolerance was 36 segments. The
centre and the angles follow the arbitrary axis algorithm AutoCAD applies to the extrusion, the normal is turned to point up
in its largest component (a circle on the floor with the normal (0, 0, -1) is written exactly as the `GeoCircle2` is), and an
arc that turns clockwise about the extrusion is written from its end to its start. `Add(GeoPolygonArc3)` and `Add(GeoPolylineArc3)` write a loop or a chain
that lies in one plane as one lightweight polyline in that plane, with the plane's extrusion and elevation and the bulge of each
arc: a slot 80 long and 20 wide (straight sides of 60) with a half circle at each end, in a plane tilted 30 degrees, is four
vertices and two bulges of 1, where chords at 0.01 were 74. A chain that bends out of one plane, such as a bar with bends in two, is a LINE for each
straight edge and an ARC for each bend, each in its own coordinate system, and so is a chain of no arc on one line;
`Add(GeoPolylineArc3, Tolerance)` states the tolerance that decides whether a chain lies in one plane, and an arc that
stands off it by less, bowing square to it, is written straight. A radius that is not above nought and a normal of no
length are refused. The overloads that take a chord tolerance are as they were, and so
is every file written for a shape in the plane or a body: 781 files of the shapes of the earlier round come out byte for byte
the same. Tested over 1 000 normals (the six axes, the cube's corners, normals a hair off the poles, normals either side of
the 1/64 where the algorithm changes its seed, and the rest at random) with the algorithm worked backwards from the written
groups: the start, the middle and the end of every arc come back within 1E-9 of its radius, and so do the vertices and the
arc middles of planar chains and loops in 1 000 planes and of 300 bent bars. netDxf, which has its own arbitrary axis code,
reads a file of tilted circles, arcs, slots, filleted loops, a stirrup and a bar bent in three planes, and each curve lies
on the chords the earlier overloads gave for it to within 2E-5.

**FIXED.** `SvgWriter.Add(GeoArc2)` of an arc that sweeps a whole turn drew nothing. It wrote the arc as a path from the start
round to the same point, `M 10 0 A 10 10 0 1 1 10 0` for a circle of radius 10 about the origin, and SVG leaves out an arc
command whose end is its start, so the picture came out empty. An arc of a whole turn, the same test as `GeoEdge2(GeoArc2)`
refuses it by, is now a `<circle>` of its centre and radius, which the picture is fitted to and which the viewer reads back as
a `GeoCircle2`; so is an arc of more than half a turn whose end prints as its start, the picture having six places (radius 10 and
a sweep 1E-9 short of a turn ends 1E-8 from its start); an arc of nine tenths of a turn, or of 1E-3 short of a turn, is still a path. A chain or a loop of arcs holds no whole turn, but an arc edge of more than half a turn whose ends print equal (a
bulge of 4E9 between (10, 0) and (10, 1E-8)) is written as two arc commands, each half of it, through its middle; `GeoFace2` is
written with straight sides only.

**FIXED.** `ConvexHull2` left corners out of the hull, and so did `GeoRectangle2.Fit`, `GeoObb3.Fit` and the depth of a clash, which
are fitted round it. The chain sorts the points along X and dropped a corner whenever it stood within the tolerance of the line
through its neighbours, so among points whose X differs by a trillionth it dropped the far end of a near-vertical line: the hull
of (0, 0), (1E-12, -15.8), (2E-12, -6.25) and (20, 0) came back as (0, 0), (0, -6.25), (20, 0), 62.5 square units where the hull
is a triangle of 158 with (1E-12, -15.8) a corner. A corner is now dropped by the tolerance only when it lies within it of the
segment between its neighbours, never when it lies past an end of that segment; and the tolerance is spent once over a run of
corners, so that no point stands more than the tolerance outside the hull, where a run of corners each a little off the line
wore it away without bound: 1 000 000 points on a circle of radius 1000 came back as a hull of 20 corners that left the circle by
12.3 or more (3 145 corners now), 100 000 points on a parabola 2.5 across had no hull at all (72 corners now), and 13 874 of 30 000
sets built to wear a hull had a point beyond the tolerance, the worst 16 211 tolerances out, where none of 9 000 does now (the worst
0.99999). The hull is built at the tolerance it is given: the polygon it made filtered its corners against the global 1E-3 again, so
at 1E-6 a square with a corner (9.9995, 10.0004) lost it and left a point 4E-4 outside, and the three points (0, 0), (0.0005, 0)
and (0, 0.0005) threw. Over 400 sets each of points in a vertical sliver 3E-12 wide (a quarter of them off it), 321 left a
point outside the hull by up to 31.1, and none does now; in a horizontal one, 1 of 400 stood 0.00245 outside, now none. A sliver
0.001 wide that had no hull in all 400 sets now has one in 235 of them, where the corners cannot all be spent within the
tolerance, and 5 000 points on a circle of radius 300 give a hull of about 1 565 corners where they gave 1 278. A hull of 2 000
random points takes the time it did (46 ms for 200 on .NET Framework 4.8 against 53). The least box round the corners of the regions two parts share, from which `ClashResult.Depth` is read, had left corners
out in 36 of 1 871 regions over five runs of 359 random pairs and leaves none out now; one pair read 20.126 deep where 24.003 is right.

**FIXED.** `Clash3.Find` gave another answer for a pair when the two parts were listed the other way round. The boolean, the
contact and the walk of the two indexes each work from one part towards the other, so the last bits of the volume, the location,
the distance and the gap followed which was first, and so did the depth: it is the smallest side of the least box fitted round
the corners of the region the two share, and where several boxes tie for the least volume, as a skewed block gives one for each
pair of its parallel faces, which of them is taken follows the rounding of their volumes, and these differ with the order in which
the corners are listed. Of 359 random pairs of turned blocks, slabs with a hole and L-shapes, 78 to 94 differed in the volume, depth or
location checked the other way round in each of five runs, 11 to 23 of them with depths over a millionth apart, up to 4.1 once the
hull above was right; with a clearance of 15, 85 to 104 of 359 differed, in the distance or the gap as well. A pair is now always
measured from the part that sorts first, by its box and then its faces, compared as the bits they are, and what names a side is
turned back to the side it was asked for: each patch of `Contact` faces out of the part listed first and `Gap` runs from it to the
other. Over five runs of 359 pairs each, with and without a clearance, on .NET Framework 4.8 and .NET 10, the volume, depth,
location, contact area, distance and the corners of every overlap and patch are bit for bit the same either way round, and the gap is
the same segment turned the other way. The same holds for one list of two parts in either order, and for a body and its copy
prepared within 1E-3 and within 1E-2.
This changes results, and not only their last bits. A pair listed in the other order than the one that sorts first gets what the
other order gave: its overlap faces, contact patches and gap are measured from the part that sorts first and turned back, so a caller
that compared the raw face loops of older results may see the same shapes with another vertex order or another start of a loop.
Where two parts are turned, the depth and volume are those of the other order's, up to 14.7 apart in the depth in the runs above, and
a depth that grades the clash by `ClashOptions.MinimumDepth` can then make it `Touch` where it was `Hard`, or the other way. A clearance
between parallel faces has no one shortest segment, and now gives the other order's: of the 3 clearances that moved among the 642 results
of the 1 500 blocks of `Clash.Neutral`, two moved their `Location` by 57 and 44 along the faces, with another `Gap`. On that case 257
of the 642 results differ from before, 243 hard in the loops of their overlap faces, 11 touching in their contact patches and 3 near;
on the 4 900 bars of the axis trap, 44 of the 143 results of one set along X do; the count, the pairs and the kinds of both are the same.

**FIXED.** `Offset2.Offset` of a curved loop came back empty, lost a loop, or grew a spike in four more cases. A slot
5.12 wide and 15 deep cut into the rectangle 40 by 30, grown by 5 with miters, was nothing and is the rectangle 50 by
40, 2 000: a stretch of a moved piece lying inside another corner's miter by less than the tolerance was kept, and left
the run open by its own length; the audit's GA 10570 (chamfers, now 81.9007, the straight path's), 22422 and 1094 were
empty in this way. A needle 0.0005 wide and 6 long on a square of side 10, grown by 0.3 at a tolerance of 1E-3, was
nothing and is one loop of 115.8455, as at 1E-4. A miter's run-on that crosses the piece beyond its neighbour now
pinches off the loop beyond it: the loop from (-4, -3) to (2, 1), round an arc of bulge -0.6 to (5, 8), round an arc of
bulge -0.25 to (-4, -5) and straight back, shrunk by 0.3 with miters, was one loop of 5.9544 and is two, 11.4715 and
5.9544, as the straight path gives; more audit cases are right with it. At a crossing where a moved piece was cut
1.9E-4 past its own end, the piece became a sliver running backwards and the corner after it was mitered 35 away: a loop
of 54.44 was 5 945.26. A corner cut square no longer takes a meeting further along its bisector than the limit times the
distance. Other curved loops can move too: of 800 random star loops offset with every join by nine distances at three
tolerances, 91 of 64 800 results change, none loses a loop, and none is further from the straight path than it was.

**FIXED.** `Offset2.Offset` of a curved loop that crosses itself returned nothing, a loop too many, or the wrong loops,
and now reads the loop as the straight offsets read it, even-odd, and keeps its arcs. The offset of a loop is built
piece by piece and joined at its vertices, so it knew nothing of two edges that cross away from one: the run round the loop
missed closing by twice the distance, and was dropped. The loop from (-7.818, 3.764) round an arc to the origin, round
another to (-8.573, 2.412) and back, whose arcs meet again at (-1.980, 0.198), grown by 0.1 was nothing and is one loop of
5.9555 (the straight path 5.9555), by 0.5 nothing and is 14.5114 (14.5114). A gear of 642 teeth, whose arcs at each inner
vertex cross 0.0079 beside it, grown by 0.5 with round corners was two loops, one a hole of 28 106.67 that is not there, and
is one of 31 667.81; shrunk by 0.5 it was nothing and is one of 28 106.67 (28 106.72); grown with mitred corners it was
nothing. A bowtie of two bowed lobes grown by 1 is one loop and shrunk by 0.5 two, and the five-pointed star drawn in one
stroke, its middle a hole, grown by 0.3 is its outline and a smaller hole and shrunk by 0.2 five tips, as the straight path
gives them. Six of the offset audit's loops were empty and are not: the cases 914 and 7994 are right, and 2686 (14
vertices with bulges from -0.6 to 0.6, offset by 0.0005 within (1E-4, 1E-6) with a mitre limit of 2), 10110, 15710 and 17330
come back as the one loop their region gives, with the findings of their joins that a simple loop has. The loop is cut at
its crossings into the pieces between them, each piece turned so that the region lies on its left, joined again at the
crossings as the corners of the region are, and moved together, pruned against the whole shape and against the region. A
vertex that lies on another edge is a crossing when the edge before it comes from one side of that edge and the edge after
it leaves to the other, though the meeting is at the vertex: the loop from the origin to (10, 0), up to (10, 10) by an arc
of bulge 0.2, back to (5, 0) and on to (3, -6) by an arc of bulge -0.2, grown by 0.5 with round joins, was one loop of
10.02 wound the wrong way and is 74.484 (74.483), and a figure eight through one vertex twice, grown by 0.5, was 10.29 and
is 82.726 (82.726). A vertex that touches an edge with both its edges on one side of it is not a crossing, and a loop that
does not cross itself is offset as it always was. Two arcs that leave a corner a hair apart and cross again within the
tolerance of it make a cusp, whichever way their headings say they turn: a loop of five vertices, two of its arcs of
bulges -0.2943 and 0.4856 leaving (-6.435, -3.631) 2E-5 apart, grown by 0.3 was nothing and is one loop of 78.857 (78.857).
Where three pieces of the offset cross within a few hundredths of a millimetre the loop no longer falls open there: a run
that comes back to where it began, or to where it ends, before it is done is split into the loop it makes and the rest; a
run that ends along the edge of another is joined to it; and a stretch thrown away that stands the distance from the shape is
used to close a run with. With miter or chamfer corners, loops the straight path gives (a hole of 4.31 beside two arcs
crossing within 0.0002 of a corner, one of 0.564, one of 2.357, one of 16.26 at a tolerance of 1E-4, a hole of 10.65 that
Round also keeps) were left out. A loop that crosses itself at a tolerance of 0.1 no longer throws "Two points in the same
place have no chord to bulge": a piece that leaves a crossing and comes back to it has no length and is left out, and the
two loops of the fuzz that threw give the loops the straight path gives (183.58 and -0.54 grown by 0.3, 249.89 grown by 1;
four loops grown by 0.3, eight shrunk). Runs of one edge are no loop, and ends of runs meet within three times the point
tolerance: a chamfer of a loop shrunk by 0.002 at a tolerance of 0.001 lost a loop of 15.80 that Round kept. A region shrunk with miter
corners can lose a small island that a miter wedge covers, as it can lose any part a spike reaches: a loop of five vertices shrunk by 1 with the default limit of 10 has two loops where Round
has three, the third of 0.203 lying wholly in the wedges of the corners beside it; with a limit of 2 it stays.
The arc beyond a miter's tip, cut so near its end that the cut was refused, was taken for a chord and covered the offset
beside it: a rectangle with a notch 0.001 deep below its base, as deep as the tolerance, grown by 1 with miter corners
was nothing and is 118.72, the answer a tolerance ten times finer gives.

**FIXED.** The point tests of a loop of arcs, `Locate` and `Contains` of `GeoPolygonArc2` among them, answered wrongly for an
arc of nearly no bulge. The piece an arc cuts off its chord was told from the arc's middle, worked out from its centre, and an
arc of bulge 6.6E-9 across 0.002 has its centre 76 000 away: the middle came out 7E-12 off, on the wrong side of the chord, and
the piece became the whole disc. A loop of four vertices 0.002 across with such an arc held the origin, and did not hold its own
middle. The middle is now told by the sign of the bulge. The loops the fault touched are those with an arc of a bulge below
about 1E-8, which a miter makes of a corner of two edges a hair off straight: a loop offset by 0.002 with miter corners was
nothing, and is the 3 loops (122.29, 31.47, 1.59) that round corners give.

**FIXED.** Flattening a curve kept the chord tolerance only down to 4 096 pieces a turn. An arc sweeping less than 2 pi /
4 096, or a circle or an arc on a large radius, was cut into no more pieces than that allowed whatever tolerance was
asked, so it came back as one chord or short of the tolerance, against what `Flatten`, `ToPolylineByChordTolerance` and
`ToPolyline3` promise (no piece further from the curve than the tolerance). A rectangle 100 by 50 whose bottom bows 0.015 (bulge 0.0003, radius
83 333) flattened within 1E-4 to its four corners and an area of 5 000, where the arc adds a segment of 1.00000002; it is
16 corners and 5 000.994 now, and 126 corners and 5 000.99993 within 1E-6. A quarter turn of radius 50 000 within 1E-4
was 1 024 pieces straying 0.0147, and is 12 419; a circle of radius 50 000 within 0.01 was 4 096 corners straying 0.0147
and is 4 968; an ellipse of radii 50 000 and 25 000 within 0.001 was 4 096 corners straying 0.0147 and is 12 920; the bar of
a pipe of radius 50 000 within 0.01 has 4 968 sides, not 4 096. The count is that of before wherever the cap did not bind (the results
are bit for bit the same), and otherwise the one the tolerance needs, worked out from the arc sine as the writers do. An
arc that a tolerance would cut into more than 1 048 576 pieces is refused with an `ArgumentOutOfRangeException`, as the
writers refuse it: a half turn of radius 50 000 within 1E-10 would take about 25 million. This applies to
`GeoArc2` and `GeoArc3` `ToPolylineByChordTolerance`, `GeoCircle2` and `GeoCircle3` `ToPolygonByChordTolerance` and
`ToPolylineByChordTolerance`, the same of `GeoEllipse2` and `GeoEllipse3` (spaced by the bend of the rim, with as
many corners as the tolerance needs: a thin ellipse 1 000 by 1 within 1E-9 takes 101 049, which an even step in the angle
could not keep within the limit), `GeoPolygonArc2.Flatten`,
`GeoPolylineArc2.Flatten` and `ToPolyline3`, `GeoPolylineArc3.ToPolyline3`, `GeoPolygonArc3.ToPolygon3`, the cylinder
sides of `GeoSolid3.Pipe` and `Revolve`, and so to the arc booleans, `ClashBar` and the meshing that call them. The
results are bit for bit those of before wherever the cap did not bind. The tolerance still cannot keep a chord shorter than
the point tolerance, which the polygons and chains merge: an arc of radius 1 sweeping 1 radian within 1E-7 asks for
1 119 pieces and keeps 559, straying 3.99E-7, where the old cut (652 pieces) strayed 2.94E-7, so where the chords fall under the
point tolerance the merged result can stray further than a coarser cut, up to (2E-3)² / 8 r from an arc of radius r; the writers
keep every chord.

**FIXED.** In space, `DistanceTo` and `CollidesWith` between points and straight shapes (segments, rays, chains,
triangles, polygons, faces, boxes and solids) were not the exact gap. A distance was nought for anything within the
point tolerance, a corner was reached a tolerance past a segment's end, and a shape hovering off a plane by less than
the planar tolerance was read as lying in it. A segment starting 0.0005 off the square 100 by 100 in its plane was
nought from it, from one end and not from the other; one hovering 0.0003 over it was nought away; a point 0.0004 under
the plane and 0.0005 beside it was 0.0004 away; one 0.0009 up and 0.0009 out (0.00127 away) collided. `DistanceTo` is
now nought only where the shapes cross, touch or hold one another (a rim counting), otherwise the exact gap in space,
and `CollidesWith` is exactly whether that gap is no more than `EqualPoint`, from either side, as in the plane. The
audit of 265 968 near-contact records against a 50-digit oracle had, at c586568, 35 336 wrong noughts, 322 gaps read
where the shapes meet, 886 wrong values, 12 506 wrong collisions, 1 084 collisions that disagreed with their own
distance, and 16 pairs whose two ways round differed. It has no wrong nought, no wrong collision and no asymmetry now;
the 12 gaps and 798 values left are all segments half a tolerance long, which are read as their start (below). A fuzz of
135 715 pairs of points, segments, rays, triangles, polygons and faces, thin ones and ones 90 000 from the origin among
them, judged against a 40-digit oracle, finds 20 readings off the exact gap by more than rounding, the worst 2.5E-9 off
it: the plane through so thin a triangle (a point 6E-14 from a sliver 0.0001 wide and 100 long reads 2.5E-9). A polygon
or a face is read flat in its plane about its first corner, so a face 80 000 from the origin is not tilted by the
cancelling of its coordinates, and a point on the rim of a polygon a few ten-thousandths out of flat is on it. A
triangle is measured to a point along its own normal, so that a sliver is measured where it is: a point on the triangle
(0, 0, 0), (100, 0, 0), (50, 0.0001, 0) read 0.00056 away, as the closest-point search of `Projection3` finds it, and is
now nought away.

**FIXED.** Distances between segments, rays and chains are exact and the same whichever is asked first. Two segments meeting
end to end, one tilted by 8E-6 radian, read 2.7E-11 one way and 9.5E-16 the other; two crossing at a slope of 1E-9 read 3E-8
where they cross; a ray whose origin stood within the tolerance of a segment read the origin's distance and ignored where it
went, so a ray through the segment's end from 0.00035 below it read 0.00035, and one running 0.0009 over a segment read 0.00103
while it collided. They are now nought where they meet, otherwise the gap, from either side, and a collision is whether that
gap is within the point tolerance. Over 28 000 pairs of segments, up to 10 000 from the origin and as little as 1E-10 radian
apart, the worst error is 3E-16 of the coordinates where it was 9E-9, and none reads differently the other way round. A
segment you give, no longer than the point tolerance, is still read as its start where it does not truly meet the other shape,
as in the plane. `Projection3` is unchanged, so within the tolerance a shortest line can be of no length where the distance
is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9) over the square is joined at (5, 5, 0.0004), and is 0.0004 from
it; between segments, `GetShortestLineTo` keeps its answer, which can still differ by the order of the arguments.

**FIXED.** Arcs, circles, curved chains and curved loops in space collide with another shape exactly where the gap between
them in space is within the point tolerance, in whatever plane they lie, lifted or tilted out of it; one case is searched and
not proved, below. A straight side facing the middle of an arc, its edge parallel to the arc's tangent, was missed at half a
tolerance though a segment coming at it end first collided (across the top of an arc of radius 50, at y = 50.0005), and an
arc whose end stood 0.0012 off a square's corner collided with it. Of the 12 506 wrong collisions of the audit above, 9 008
were among curved shapes, and none is left; 130 000 pairs of arcs and 60 000 arcs against a segment, ray, triangle, polygon,
face or box, judged by a bounded search in doubles, find none wrong. A segment along an arc's tangent, lifted over its plane
by 0.0009 half a turn round a circle of radius 10 000 whose centre is 10 000 from the origin, read 1.2 away and lost its
collision, and one touching a circle of radius 2000 read 6E-6 off: the roots of the distance along the arc coincide there,
and a step toward one carried the candidate away; every candidate is kept as it came and the distance is minimised round the
doubled ones. The angle is measured from the middle of the arc's sweep, where z = tan(t / 2) is not infinite inside it, and an
arc over a half turn is solved a second time from a quarter turn on: a segment a hair off half a turn round, tilted a few
millionths, on a circle of radius 40 000 or more, read up to 7E-6 too far and lost up to 11 collisions in 40 000 cases, and
reads true now. The one case searched, not proved, is two partial arcs lying nearly on one axis whose distance stays almost
constant just beyond the tolerance, where the proof by halving outruns its budget and the arcs are walked at a thirtieth of a
radian with every low point polished; the exactly coaxial copy of one arc, which is never further from it than the tilt times
its radius and the offset of its centre, settles most of them first. The gap is worked out inside and not offered: no member
measures the distance from a curve in space to anything but a point. An arc against a box no longer throws for a box thinner
than the point tolerance, or a few thousandths across such as 0.018 by 0.0021 by 0.0023: the box is read by its corners, where
its body refused a face too small to be a polygon (29 of 60 000 scenes threw). A circle collides as its rim and a loop as its
outline, so a circle or a loop round a shape touches nothing, while the distance from a point to a circle is to the filled
disc, as it always was. A triangle with no area collides with nothing, and a segment no longer than the tolerance is read as
its start unless it truly meets the curve, as for straight shapes. A single arc costs a few microseconds more to ask about,
measured on net48: an arc against a triangle took 1.2 microseconds and takes 6.8 where the two stand near each other, 0.6 and
1.2 where they stand far; an arc of radius 20 lying 0.0005 beside a triangle's side takes 3.4 microseconds, and one dipping
to 0.0005 over its face 1.2; a chain takes the same as before.

**CHANGED.** Results near contact move, from nought to the gap, in `Distance3.DistanceTo` and the `DistanceTo` members of
the shapes for: a point against a triangle (a sliver), polygon, face or solid; a triangle against a triangle, polygon or face;
a polygon or face against a polygon or face; a segment, ray or chain against a triangle, polygon, face, box or solid; a solid
against a line, ray, chain, triangle, polygon or face; a ray against a ray; a chain against a ray. `Collision3.CollidesWith`
for the same pairs, and for arcs, circles and curved chains and loops, answers whether the exact gap is within `EqualPoint`;
it moves only where the old answer used the old band (a corner reaching a tolerance past a segment's end, a shape laid flat)
or missed a gap inside the tolerance. Eight things are read otherwise, on purpose, and the guide
(the chapter on space) gives each with its numbers: a triangle with no area
touches nothing, whatever its distance, so that for it `CollidesWith` is not `DistanceTo <= EqualPoint`; `DistanceTo` between
two solids or oriented boxes, which searches through their triangles, still reads nought within the point tolerance, so two
oriented boxes 0.0004 apart are nought apart, and so is an axis-aligned box against an oriented box or a solid (only
`GeoAabb3.DistanceTo(GeoAabb3)` is exact); a plane reads a ray, a chain, a region, a box or a body within the point tolerance
of it as on it, so that a plane is nought from a square 0.0004 above it; a point, segment or region within a billionth of a
millimetre outside a body reads as in it, so that rounding in a turned body cannot lose a crossing (a point 5E-10 above a solid
box is nought away); a segment you give, no longer than the tolerance, is read as its start; a circle collides as its rim and a
loop as its outline; `GetShortestLineTo` and `GetIntersections` keep their readings; and the one case of arcs is searched, not
proved.

**CHANGED.** The collision of two triangles, which the clash reads, is now the same whichever is asked first at exactly the
tolerance. A pair 90 000 from the origin standing 0.00099999999999998 apart collided asked of the first triangle and not asked
of the second, because the closest-point search that decided it rounded one way round and the other the other; it now collides
both ways, and a billionth short of the tolerance it collides neither. Clash results move only for pairs that stand at the
tolerance to the last digits: in 135 715 pairs of triangles, boxes and bodies it was the one pair, and every benchmark
signature of the clash is unchanged.

**CHANGED.** Two triangles collide, and so the clash and the contact of bodies made of triangles, where a side of one no
longer than the point tolerance comes within the tolerance of the other: a triangle with the side (0, -0.0005, 0) to (0,
0.0005, 0) and an edge along z at x = 0.0009 collided neither way round though they are 0.0009 apart, its short side
read as its start. In 180 000 random triangle pairs 8 moved from missing to colliding, all from 0.00097 to 0.0009996
apart, and none the other way; in 39 000 pairs of bodies, the clash benchmark scenes among them, none moved, since the
faces of a body have no side that short. Every clash benchmark signature is unchanged.

**NEW.** `DxfWriter` writes shapes as a DXF file of version R2000 in millimetres, for AutoCAD, Tekla Structures and
every CAD viewer: `new DxfWriter().Add(plate, "plate").Add(bar, 0.1, "bar").Save("part.dxf")`. Each shape goes on a
layer, made on first use. In the plane it is written exactly, as the entity CAD has for it: points, segments, circles,
ellipses and rays as themselves, polygons, rectangles, triangles and the rings of a face as closed lightweight
polylines, chains and loops with arcs as polylines with the bulge of each arc, and a clockwise arc from its end to its
start. In space points, segments, rays and ellipses are exact, chains and polygons are 3D polylines, and faces,
bodies, triangles and meshes are 3DFACEs (a plate 100 by 100 by 20 with a 20 by 20 hole is 64) whose inner diagonals
are marked invisible; a box is six 3DFACEs written from its corners, flat ones included. An arc, circle or curved
chain in space is cut into chords no further from it than a chord tolerance the caller states (a circle of radius
50 000 at 0.001 keeps its sag within it, in 15 708 chords), and one needing more than 1 048 576 is refused. A shape
with a number that is not finite is refused before anything of it is written. The tests read the files back group by
group and measure them: the bulge of a quarter arc and its sign, the end points of a clockwise arc, the axes of an
ellipse, the volume under the faces of a body. ezdxf's audit of a file of every shape finds 0 errors and makes 0
fixes, and loading it only adds the eight default dictionaries (ACAD_COLOR, ACAD_MATERIAL, ACAD_MLEADERSTYLE,
ACAD_MLINESTYLE, ACAD_PLOTSETTINGS, ACAD_SCALELIST, ACAD_TABLESTYLE, ACAD_VISUALSTYLE) that ezdxf adds to any file
without them; netDxf loads it with all 15 of its layers kept. An empty `GeoAabb3` is refused with an
`InvalidOperationException`, as `ToObb` refuses it.

**NEW.** `ObjWriter` takes the shapes it left out. `Add(GeoPoint3)` writes a dot, `Add(GeoTriangle3)` a face, and
`Add(GeoAabb3)`, `Add(GeoObb3)` and `Add(GeoCell3)` a box or a cell as its mesh: a box 30 by 20 by 10 is 12 triangles
enclosing 6 000, wound outwards, and a cell is named `cell_i_j_k` as the cells of a grid are. `Add(GeoPolygon3)` writes
a polygon that turns the same way at every corner and goes once round as one face, so a regular hexagon is one `f` of 6,
and any other as its triangles, so an L-shaped polygon 100 by 100 with 50 taken from a corner is 4 triangles covering 7
500 where a viewer fanning one face would cover the notch, and a pentagram is not one face. The triangles are cut within
the tolerance, so a polygon that is not one face and is thinner than it is written as an object with no face: an L with
arms 0.0004 wide, built within 1E-5, is empty at the global tolerance and 4 triangles within 1E-5.
`Add(GeoPolygonArc3)`, `Add(GeoArc3)`, `Add(GeoCircle3)` and `Add(GeoEllipse3)` write a line of chords no further from
the curve than the chord tolerance given, which has to be a finite number above nought: a circle of radius 100 at 0.1 is
71 chords, and its last vertex is its first. A chord shorter than the point tolerance is written all the same: a circle
of radius 1 within 1E-8 is 22 215 chords of 2.8E-4, closed on its own first point. The library's own cut used to stop at 4 096
chords a turn and stray 0.0147 from a circle of radius 50 000 cut within 0.01; the writers work the count out from the
arc sine, as the library does now, and give the library's where that is more: that circle within
0.001 is 15 708 chords, and a curve that would need more than 1 048 576 is refused with an
`ArgumentOutOfRangeException`. The `Add` of a `GeoPolylineArc3` that was there before is cut the same way. On ordinary
input it gives the points of `ToPolyline3` but for the end of each arc, which is the edge's own end point; it keeps an edge or a chord shorter than the point tolerance, which
`ToPolyline3` dropped; it gives the exact count, which the library's cut did not while it stopped at 4 096 a turn, a bend of radius 50 000
within 0.001 being 3 927 chords to a quarter turn where it strayed 0.0147; and at a knife-edge count it may add one
chord, never one fewer. It still takes any tolerance above nought, infinity included, which the chord overloads and the
`Wkt` writers refuse. In the chains and loops of both writers an arc so shallow that its circle cannot be placed, as one of a bulge
of 1E-200 whose centre is not a number, is read as its chord, so that the corner stays where it is: on the chain (0, 0, 0),
(100, 0, 0), (100, 100, 0) the corner was written at x 130.4 for a bulge of 1E-16, as NaN for 1E-200 and refused for 1E-310. `Add(GeoRay3, length)` writes the ray for the length given, and refuses a `default` ray with an
`ArgumentException`. `Add(GeoSolid3)`, `Add(GeoFace3)` and `Add(GeoPolygon3)` each gain an overload taking the
`Tolerance` to triangulate within, in place of `Tolerance.Global`: a slab 100 by 100 and 0.0008 thick, built within
1E-5, is its 12 triangles written within 1E-5, and at the global tolerance its four sides, 0.0008 high, are dropped and
the 4 triangles of the two big faces are left. The calls that existed give the same text.

**NEW.** `SvgWriter` draws the rest of the plane. `Add(GeoEllipse2)` draws an ellipse as an `<ellipse>` turned by its
major axis, and fits the picture to its bounding box: an ellipse of radii 30 and 10 turned 30 degrees reads back as
`rx="30"`, `ry="10"` and `rotate(30 ...)`, and the ellipse's box is 52.9 wide and 34.6 high, the picture 58.2 by 39.9
with its margin. `Add(GeoTriangle2)`, `Add(IEnumerable<GeoTriangle2>)` and `Add(GeoMesh2)` draw a closed path to each
triangle or face, so a mesh of a grid of 4 cells is 4 paths. `Add(GeoRay2)` draws a ray from its origin to the edge of
the picture, which the origin helps to fit: a ray along +X from (0, 0) in a picture of a square 0 to 100 ends at x =
105; a `default` ray is refused with an `ArgumentException`. `Text(at, text, colour)` writes a label that reads upright
however the picture is zoomed, its letters 3 per cent of the larger side of what is drawn high, the margin not counted,
with the text escaped and every character XML 1.0 forbids, such as a control character, shown as U+FFFD so that the
picture still parses. A number that rounds to minus nought is now written 0 where .NET 10 wrote -0 (.NET Framework wrote
0): a point a billionth to the left of the axis is drawn at `cx="0"`. That is the one change to an old output, and only
where it printed -0.

**NEW.** `Wkt.Write` takes the rest of the shapes. `GeoRectangle2`, `GeoTriangle2` and `GeoTriangle3` are polygons;
`GeoFace3` is `POLYGON Z` with its boundary counter-clockwise about its normal and its holes clockwise; lists of points
are `MULTIPOINT`, lists of segments and of chains `MULTILINESTRING`, and lists of `GeoFace3`, a `GeoMesh2` and a
`GeoMesh3` are `MULTIPOLYGON` with a polygon to each face. A `MULTIPOLYGON` of faces that share an edge, such as the
cells of a mesh, parses everywhere but is not valid in the OGC sense (`ST_IsValid` says false), and the same holds for
the existing one of a list of `GeoFace2`. A `GeoSolid3` is a `POLYHEDRALSURFACE Z` of the triangles of where its
material ends, which PostGIS reads: a box 30 by 20 by 10 is 12 triangles whose signed volumes add up to 6 000. Circles,
arcs, ellipses and chains and loops with arcs, in the plane and in space, take a chord tolerance, which has to be a
finite number above nought, and are written through chords no further from the curve than it, with the count worked out
from the arc sine as for OBJ: a chain or a loop is cut arc by arc on each arc's radius, so a bend of
radius 50 000 in a bar is within 0.001, and a curve that would need more than 1 048 576 chords is refused. `GeoRay2` and
`GeoRay3` take a length, and refuse a `default` ray with an `ArgumentException`.

**FIXED.** `ObjWriter`, `Wkt` and `DxfWriter` wrote some coordinates on .NET Framework as another double: its `"R"` prints
15 or 16 digits where 16 or 17 are needed, and the number read back is a step away. Counted on .NET Framework, that is 13
of 400 000 coordinates computed as a mesh gives them (positions up to 10 000 with up to 0.37 added, a third of them
rounded to thousandths) and 257 of 400 000 doubles of random bits. 4.8721540113315704 was written as 4.87215401133157,
3.3824149791069402 as 3.38241497910694 and -2.1661333540296802E-08 as -2.16613335402968E-08. A coordinate is now written
as the shortest of its 15, 16 and 17 digit texts that reads back to exactly that double, judged without the Framework's
parser, which is not correctly rounded either: by one correctly rounded multiplication or division where the digits and
the power of ten are doubles exactly, in integer arithmetic where they are not. Over 1 420 775 doubles, powers of two and
subnormals included, the quick way and the integer way agree on every text, on each runtime. The text is exact on either
runtime, and not always the same on both: the Framework's digits can differ from .NET 10's, and be one longer, where
both read back to the same double. A negative nought is now written `-0` by `ObjWriter` and `Wkt` on both (the Framework
wrote 0); `DxfWriter` and `SvgWriter` have their own rule and still write 0. Writing a surface of 113 600 triangles to
OBJ takes 0.86 s on .NET Framework where it took 0.61 s.

**FIXED.** The distance in the plane is nought only where two shapes truly meet, cross, overlap or one holds the other, and
otherwise the exact gap, whichever way round it is asked, and `CollidesWith` is true exactly where the distance is at
most the point tolerance. The crossing tests behind the distance keep a band of that tolerance, so it read nought wherever
one shape started within it of the other, and followed the direction a segment ran. The pairs are fixed family by family
below: straight shapes, faces, arcs, circles and curved chains and loops, ellipses, and a shape's own sides. Where two
shapes truly touch, the distance may read a rounding residue of up to about 1E-13 times the largest coordinate in place of
0 (the audit's worst was 4.7E-11, an arc against a flat arc in a scene 150 000 across, 3.2E-16 of its size), so compare a
distance with a tolerance, and never with `== 0`.

**FIXED.** The distance from a point or a straight shape to a polygon, a rectangle, a triangle, a face, a polyline, a
ray or a straight loop is nought only where they truly meet. A segment, ray, polyline, polygon or triangle starting 0.0005
off the square 100 by 100 was nought away at a tolerance of 0.001, and the same segment reversed 0.0005; a point 0.0005 off
a polygon, a triangle, a straight loop or a face was nought away. A rectangle kept its band per axis, a factor of the
square root of two too wide at a corner, so a segment starting 0.00085 off its corner in x and in y, 0.0012 away, was
nought away and collided with it; so did two rectangles and a rectangle and a face. They now read the gap, and collide
exactly when it is within the tolerance. Of the 154 548 placements, 13 482 answers that read nought for shapes apart and
250 collisions past the tolerance are now right, and 188 true touches read a rounding residue of at most 2.3E-13 in
place of 0. A rectangle of no width or height, or of next to none, is parted from another by all its axes. The axes of
each rectangle were taken from its sides and one with a side shorter than the vector tolerance lost an axis: a rectangle
10 by 0 at (5, 0) against a square turned 45 degrees with its bottom corner at (5, 1) read nought and collided, and so
did two such rectangles 5 apart; so did one 10 by 0.000001. The axes now come from the angle, and the sides of two
rectangles, or of a rectangle and a polygon, are measured as the sides they are even when shorter than the point
tolerance.

**CHANGED.** The Arranger counts clearances up to the global point tolerance as equal, as it did when a distance within
it read nought, so the exact distances do not let the last digits of a clearance pick the place: a label among obstacles
0.0002 off its places goes to the same place wherever the scene lies along X. Where the old reading depended on the way
an obstacle's line ran, a place can change: with one line starting 0.0005 above the place above a label and another
ending 0.0008 below the place below, at a tolerance of 0.0001, the label went below, and now goes above, the first found
of two places both clear by more than that tolerance.

**FIXED.** The distance from the material of a face is the exact gap from its nearest rim, and nought only where a
shape truly meets the material. It read nought from the old crossing tests, and measured otherwise the length of the
shortest line, which lets a segment run on up to the tolerance past its ends: a segment, chain, polygon, triangle,
rectangle, face or ray whose tip stood up to 1.7 times the tolerance off a corner of the face was nought away, and so was
a circle, an arc or a curved loop or chain half the tolerance off. A segment shorter than the tolerance crossing a rim is
nought away whichever way it runs, and a rectangle of no height is measured where the old reading through a polygon
refused it. A rectangle in an L-shaped hole of a face, 0.0012 from its reflex corner, no longer collides with the face by
the band of its corner per axis. Of 6 000 such shapes of each kind placed 0.5 to 5 times the tolerance off a face with a
hole, outside it or inside the hole, 855 to 987 of each straight kind and about 1 000 of each curved one read wrong; none
do now, and every one collides exactly where its distance is within the tolerance.

**FIXED.** The distance from an arc, a circle, a curved edge, chain or loop to anything is nought only where they truly
meet, and otherwise the exact gap, and they collide exactly where it is at most the point tolerance. A segment 0.0005
above the top of the arc of radius 50 about the origin was nought away at a tolerance of 0.001, and so were two arcs
whose tops stood 0.0005 apart and two whose ends stood 0.0015 apart; a circle of radius 40 and a polyline starting 0.0005
off it were nought apart, and an arc, chain or loop starting 0.0005 off a straight shape or a bowed side was too,
whichever way round it ran. Two arcs of one radius whose circles cross 5E-6 of a radian inside both ends read 2.5E-9
apart; they are nought apart. The crossing of two arcs is now worked out from their circles directly and kept where both
arcs sweep through it, with no slack at the ends. Of the 154 548 placements, 31 036 answers that read nought for shapes
apart are now right, and 262 collisions answered past the tolerance now answer within it. Collisions with arcs in chains
and loops no longer keep a band at the ends of their arcs either: the chain from (0, 0) by (40, 0) with a bulge of -0.35
to (70, 25) and (100, 25), read as a loop or as a chain, and the arc of radius 50 about (25.858, -47.958) from 5.317 to
7.567 radians, which ends 0.00012 from (40, 0), collided at a tolerance of 0.0001; so did a circle and an arc whose
circles stand 0.1 apart at a tolerance of 0.1, when the arc begins 0.0003 of a radian past the nearest point. Of 42 205
shapes of every kind, placed so that a vertex of one stands 0.3 to 1.9 tolerances off a vertex of the other, 35 collided
against their own distance at a thousandth; none do now, nor at 1E-4 or 1E-2. Two arcs of a radius under the point
tolerance about one point, one 0.0001 outside the other, did not collide where their spans overlap, and now do. A circle
of radius 0.0001 about (100.00085, 100.00085) collided with the square 100 by 100 from the origin, 0.0011 away, by the
centre's band per axis; it no longer does, nor does it read nought.

**FIXED.** A point near the centre of an arc is measured exactly. A point, or the centre of a circle, within the point
tolerance of the arc's centre was carried to the arc's start first, which is up to twice the tolerance off: the chain
from (-60, 0) to (-50, 0) and then by a bulge of 1 to (50, 0) was 50.00000001 from (0, -0.0009) at a tolerance of a
thousandth, where it is 49.9991, and so were the edge and the arc alone; the same arc scaled to a radius of 0.0012 was
0.00124 from (0, -0.0003), off it, where it is 0.0009 and on it; the circle of radius 49.9995 about (0.0007, 0.0005) was
0.0012 from the arc of radius 50 about the origin from 3.14159 to 7.9506 radians, and did not collide with it, where it
crosses it. The signed distance of a curved loop reads the same, which moves the 32 planar cases of a point 0.00002 to
0.06 off the centre of an arc of radius 50 from 50 to 49.99998 to 49.94. The projections `GetShortestLineTo` and
`GetClosestPointOnBoundary` keep carrying such a point to the start, so their line can be longer than the distance, by at
most twice the point's offset from the centre: from (0, -0.0009) to the arc of radius 50 about the origin from (-50, 0)
to (50, 0) through (0, -50), at a tolerance of a thousandth, the line runs from (-50, 0) and is 50.00000001 long, where
the distance is 49.9991.

**FIXED.** An edge is read as its chord only where the chord is no further from it than the rounding of the centre route
by which an arc places its points, 2.5 parts in 1E16 of how far its centre and radius reach from the origin, which is
where the two stand equally good. It was 4 parts in 1E15, so an edge of bulge 3.2E-8 over 100, which bows 1.6E-6, was
read as its chord and measured 1.6E-6 off; it is now measured to its arc, within 2.5E-16 of the reach, and so is one of
10 000. A bulge of a ten-millionth is not read so, and two edges that share a vertex read nought from it, also when one
of them bows that little: the edge of bulge 1E-7 from (0, 0) to (100, 0) read 8.8E-8 from a segment leaving (100, 0), and
the distance from a chain, loop, polygon, polyline, rectangle or triangle to an edge, and from an edge to them, took the
edge's arc as it was placed through its centre, which puts its end 1.1E-8 off on a chord of 100 and 1.1E-6 off on a
chord of 10 000 at 100 000 from the origin. All are nought now. The distance from a face reads the same threshold.

**FIXED.** The distance from an ellipse to a polyline, a curved chain, a curved loop or the material of a face is
nought only where they truly meet, and otherwise the exact gap. It read nought wherever the nearest edge came within the
point tolerance, or a loop or polygon held the centre within it, so a polyline starting 0.0005 off the end of the ellipse
60 by 25 was nought away at a tolerance of 0.001, and so was a loop 0.0005 off its rim and a face whose side stood 0.0005
off it; an ellipse in the hole of a face, half a tolerance from the hole's rim, was nought from the face. They now read
0.0005. Of the 154 548 placements, 1 021 answers that read nought for shapes apart are now right (320 polylines, 290
chains, 230 loops and 181 faces) and none went wrong; the collision with an ellipse is now the same test as its distance,
nought within the point tolerance, so the two never disagree. The ellipse against a segment, a ray, a circle or another
ellipse was exact already. Against a polygon, a rectangle, a triangle or a loop it read nought when the shape held the
centre within the point tolerance, so a speck of an ellipse 0.0002 by 0.0001 whose centre stood 0.0004 off a side was
nought away, and is 0.0002 from it. Against an arc it weighed the arc at its two ends alone when the arc was no larger
than the point tolerance, since the projection onto an arc snaps a point within the tolerance of its centre to its
start: the left half of the circle of radius 0.0008 whose nearest point stood 0.0005 off the end of the ellipse 60 by 25
read 0.0013 at a tolerance of 0.001 and did not collide, and now reads 0.0005 and does. That fix also moves
`GetShortestLineTo` of an ellipse and an arc: it joins the true nearest points of a tiny arc, 0.003 long for an arc of
radius 0.0008 standing 0.003 off, where it joined an end of the arc and was 0.0038 long. It is a fix to which point is
found; the rule for when two shapes touch is as it was. A curved edge bowing less than its own rounding is read as its
chord, as it is against a segment or an arc: the edge from (60.0005, -500) to (60.0005, 500) with a bulge of 1E-12, on
a radius of 2.5E14, read nought from the ellipse 60 by 25 and now reads 0.0005. An edge touching the rim is nought away,
where the one from (60, -50) to (60, 50) with a bulge of 1E-8 read 4.8E-7, and so is one starting on it. A collision
with the material of a face skips every rim edge whose box lies further from the ellipse's own box than the point
tolerance and stops at the first within it, and answers as the distance does. The distance between two ellipses placed
less than about 1E-6 apart can still be misread, see the guide.

**FIXED.** A shape's own sides are measured as the sides they are, however short, by every member, and a triangle is read
as the triangle it is. A side shorter than the point tolerance was read as a stub, its start point, which is only for a
segment the caller gives. Against arcs, circles and curved chains and loops: the triangle (0.678308, -0.078659),
(0.627916, 0.0335963), (0.567449, -0.0252647) was 0.1301 from the arc about the origin of radius 0.665358 from 0.111401
to 2.78899 radians at a tolerance of 0.1, where it stands 0.0523 off it and collides, because a triangle with a side that
short was read as its longest side alone; a polygon, rectangle, curved chain or loop with a side that short read as far
off in the same way, so that one 0.13724 off the arc did not collide at a tolerance of 0.138. To a point or a circle,
the triangle (0, 0), (0.004, 0), (0.002, -5) was 0.006325 from the point (0.002, 0.006) at a global tolerance of 0.01,
and 0.006 at 0.00001; it is 0.006 at both, and at the default tolerance the triangle (0, 0), (0.0009, 0), (0.00045, -5)
was 0.00104849 from the circle of radius 0.00015 about (0.00045, 0.0011) and did not collide with it, where it is
0.00095 from it and does. The signed distance of a polygon, curved loop or face reads its sides so too, and so does every
distance and collision against a segment, a ray, a straight edge, a polygon, a polyline, a rectangle, a triangle or a loop:
the polygon (0, 0), (0, -50), (0.004, -50), (0.004, 0) was 0.006325 from the segment that runs up from (0.002, 0.006) at a
global tolerance of 0.01, and is 0.006. A rectangle thinner than the default tolerance, 0.0005 wide and 10 tall, threw an
`ArgumentException` ("A polygon must have at least 3 distinct vertices") from every distance and collision against an arc,
a curved chain or a loop, from their crossings and shortest lines, and from `IsEqualTo`; it is measured by its four sides, so the one whose left side stands at x = 20.00175 is 0.00175
from the arc of radius 20 about the origin. An arc so flat that it is read as its chord is measured by that chord however
short: the arc of radius 1E6 whose chord is 0.004 long is 0.006 from the segment leaving a point 0.006 over its middle at a
tolerance of 0.01, and a segment shorter than the tolerance that crosses it is nought from it. A triangle
whose corners stood within the caller's tolerance of each other was read as its longest side alone by its collisions
with lines, polygons, rectangles, polylines, rays, triangles, edges and faces, by its distance to a ray or a face, by
every other distance reading the global tolerance, and by its signed distance, shortest line, closest point and edge,
containment and intersections: the triangle (0, 0), (0, 1), (0.08, 1) is 0.07 from the segment along y = 0.5 that ends
at x = -0.07, and collides with it at a tolerance of 0.1, where it was 0.11 away and did not; the sliver (0, 0), (10, 0),
(0, 0.0005) had the point (1, 0.0001) inside it, at distance nought, and 0.00035 off by its signed distance. Only
corners that coincide exactly make a triangle a segment now, and no member of a triangle reads the global tolerance for
it: a triangle with corners 0.004 apart is as far from a polygon whether the global tolerance is 1E-5 or 1E-2. The
shortest line from a sliver 100 long and 0.5 high to the point 1 past its tip starts on its short side at a tolerance of
1, as it does at a thousandth. An ellipse reads a triangle so too, by its distance, collision, crossings, shortest line
and closest edge: the ellipse 30 by 10 whose right end stands 0.15 off the left side of the triangle (0, 0), (0, 100),
(0.08, 100) is 0.15 from it and collides with it at a tolerance of 0.17, where it was read 0.19 away, from the longest
side alone, and did not; moved 1.15 nearer, to about (-29, 50), it crosses the left side at (0, 47.44) and (0, 52.56),
where the crossings were read 0.04 to the right of it.

**FIXED.** A segment you give that is shorter than the point tolerance is read as its start where it does not meet the
shape, by every distance to a segment, polyline, polygon, rectangle, triangle, ray, loop or chain; a face measures it as
it is. One that crosses a side of a polygon, a rectangle or a polyline, an edge of a curved chain or loop, or an arc,
reads 0 in every one of them; against a segment, a ray, a circle or a straight edge it is still read from its start.
The stub from (50, -0.0004) to (50, 0.0002) across the edge from (0, 0) to (100, 0) of a curved chain read 0.0002 and now
reads 0. The placements of such a stub that read nought, 126 against straight shapes and 222 against curved ones, now
read this convention; a shape's own sides are never read so.

**FIXED.** An arc lies in a plane when no point of it stands further off it than the planar tolerance. A curved
chain's plane, a curved loop and the `PlanarMap` layouts of an edge and an arc compared the arc's own normal with the
plane's within the angle tolerance, a whole degree: a half circle 1 000 across turned 0.99 degrees out of a loop,
8.64 off it, was taken as lying in it and flattened, and a bend 100 long bowing a ten-millionth turned 2 degrees,
0.00000017 off, was refused. `TryToArc2` also asked for the arc's centre on the plane, which an arc 100 long bowing a
ten-thousandth, of radius 250 000, has 436 off it when turned a tenth of a degree. Of 20 000 arcs with their ends in
the plane, the chain and the loop called 1 124 flat that stood off it, up to 30.2, and refused 2 462 that lay in it;
they now get every one right. The layouts take none that stands off and refuse about 210 that do not, each standing
at least 0.00065 off against the tolerance of 0.001. A whole circle turned out of the frame within the tolerance is
laid out as one, and an arc past a half turn comes down round its centre as it is: an arc of 359.5 degrees in the
frame came down from `TryToArc2` as a whole circle.

**FIXED.** An arc in space that bows little is an arc. A curved edge, bar or loop, the lift of an arc of the plane
into space, moving an arc and laying it out in its own plane all built the arc through its start, middle and end,
and refused three points within the global point tolerance of one line: an edge 0.01 long with a bulge of a
thousandth threw on its length, its middle, every collision and crossing, and every move. Of 22 860 calls across
`GeoEdge3`, `GeoPolylineArc3`, `GeoPolygonArc3`, `GeoArc3` and `PlanarMap.ToArc3` on arcs from a half circle down
to a bulge of a trillionth, 6 984 threw so; none do now. An arc is built from its circle, which also lifts a whole
circle into space and moves one, where three of its points had nothing to stand on. A move is still the arc through
where the ends and the middle land, the middle now worked out across the chord, so a stretch that turns the arc into
part of an ellipse is answered as before. Ordinary arcs come out as before within the last digits: of 20 000, none
moved by more than 0.000000001, and an edge's arc now has the radius and sweep of its bulge exactly, where they
were off by up to two parts in 1E10.

**CHANGED.** Asking whether a body is closed, welding it, closing the slivers a cut leaves, taking a convex hull and
cutting a face by a plane no longer slow with the square of the edges where the points are numbered in order round a
mesh. On a sphere of 319 200 triangles `IsClosed` takes 0.53 s where it took 55 s, and closing its slivers takes
0.66 s where it ran for more than 20 minutes. Results are unchanged.

**CHANGED.** Cutting a face into triangles (`GeoFace3.TriangulateSurface`, `GeoFace2.TriangulateSurface`,
`GeoPolygon2.TriangulateSurface`, and the cutting the booleans do) no longer slows with the square of the corners of a
convex ring. Each ear taken off was checked against every corner of what was left, though only a reflex corner can stand
within an ear, and a convex ring has none; now only the reflex corners are looked at. The base of a cone with 4 000
corners takes 0.01 s where it took 0.10 s, one of 16 000 takes 0.02 s where it took 1.4 s, and one of 64 000 0.06 s where
it took 14.5 s (.NET Framework 4.8: 0.05 s where it took 17.7 s). A star of 16 000 corners, half of them reflex, takes
1.1 s where it took 2.2 s, and a comb of 16 000 corners 0.45 s where it took 1.6 s: those still grow with the corners times
the reflex corners. The triangles are the same to the last bit: of 212 004 random faces (convex rings, rings a hair off
convex, stars, combs, faces with holes, at tolerances of 0, 1E-6, 1E-3 and 0.05), cut in the space and in the plane, none
differs.

**CHANGED.** `GeoSolid3.Validate`, `IsClosed` where it has to match edges by overlap, and `SplitShells` no longer slow
with the square of the edges where many meet at one point. The edges were matched by a sweep along an axis, and the edges
of a cone running to its apex overlap each other along every axis though none lies along another; now the edges lying
along one line are found from the points near each, when the sweep would try more than 64 pairs for each edge, and the
sweep is kept as it was for every other body. A cone of 16 000 sides validates in 0.11 s where it took 6.4 s (.NET
Framework 4.8: 0.14 s where it took 42 s), one of 4 000 in 0.06 s where it took 0.37 s; with its base left out,
`Validate` takes 0.08 s where it took 5.7 s and `IsClosed` 0.08 s where it took 5.9 s. A sphere of 28 800 faces validates
in 0.16 s where it took 0.6 s, and the faces of a double cone of 2 000 sides come apart in 0.012 s where they took 0.28 s.
The issues are the same, in the same order: of 202 002 random bodies (cones, drums, meshes, boxes stacked and sharing
edges, booleans, with faces taken out, turned, doubled, cut by a point, and corners moved by about the tolerance), none
differs in `Validate`, `IsClosed` or `SplitShells`.

## 12.2.0

**FIXED.** A curved loop grown until it closes a gap kept the space it closed round only when the hole's pieces met in
the order they were cut in and were not a lens of two arcs. The square of side 100 with a lens-shaped cavity, grown by
3, lost its hole of 327.84, and a cavity pinched in two by its bowed sides kept one of its two holes. A loop running
against the source is now kept where it lies inside a loop running with it. Holes are wound against their outer loop,
as the straight offsets wind them, and the documentation now says so.

**FIXED.** A flat chain in space has a plane even when, closed from end to start, it encloses no area: the Z through
(0, 0, 2), (10, 0, 2), (0, 10, 2), (10, 10, 2) lies in z = 2, and with an arc on its first edge `Offset3.OffsetInPlane`
offsets it instead of throwing "does not lie in one plane". A curved chain whose vertices lie on one line takes its
arcs' plane: the half circle from (0, 0, 2) to (10, 0, 2) offset by 1 is the half circle of radius 4 inside it.

**FIXED.** Round joins keep within `OffsetOptions.ArcTolerance` down to 1.15E-9 of the distance: the square of side 100
grown by 80 with 1E-4 was drawn in 1 024 chords a turn and stood 3.8E-4 off the arc; it now takes 1 987 a turn and
stands within 1E-4.

**CHANGED.** Offsetting a polygon, a face or a chain in a plane in space no longer slows with the square of its edges
where edges between neighbouring points are counted: a zigzag of 100 000 vertices offset in its plane takes 1.0 s where
it took 15.9 s. Results are unchanged.

**FIXED.** A straight polyline's offset is the edge of the band it sweeps and nothing more. At an inside corner
sharper than a right angle the stretch of each moved segment running on past the other leg is no longer kept: the
chain (10, 10), (0, 0), (10, 0) offset by 1 is one piece through (2.414, 1), where it came with two extra pieces beside
the corner. Where the band's edge lies along another part of the polyline within the tolerance, it is the polyline and
goes: the step (0, 0), (10, 0), (10, 1), (-5, 1) offset by 1 gives (0, 0) to (-5, 0) alone, where it gave two pieces
lying on the chain. Pieces are put in order by the middle of their longest edge, and the ordering no longer grows with
the square of the pieces: a zigzag of 10 000 vertices offset by 1 is one chain in 0.57 s, where it came in 9 998
fragments in 36 s. The same holds for `Offset3.OffsetInPlane` of a `GeoPolyline3`, and of a `GeoPolylineArc3` holding no
arc.

**FIXED.** Curved offsets join an opening corner as the straight ones do. A chamfer is cut square to the corner's
bisector at the distance, touching the round join's arc, not along the chord between the moved ends: the square of side
10 as a `GeoPolygonArc2` grown by 2 encloses 193.255, where the chord came 1.414 from the corner and left 188. A miter
past `OffsetOptions.MiterLimit` is squared off at the limit: the triangle with a 10 degree tip grown by 1 is cut 10 from
the tip, where the chord came within 0.087 of it. Where a miter can neither meet nor be squared off, the corner is
rounded at the distance. Edges 0.8 degrees off straight are mitred: the loop (0, 0), (50, 0), (100, 0.698), (100, 50),
(0, 50) grown by 80 meets at (50.558, -80). At a coarse tolerance pieces folded by a vertex now go: the U 2.2 wide
offset 6.28 into itself at 0.1 kept two round joins 4.08 from it, and gives nothing. Chamfered curved offsets take up
to 2.2 times as long, mitred ones up to 1.7 times, on small shapes.

**FIXED.** A curved offset past an arc whose radius runs out goes round the arc's two ends, and comes back the same
from every start and both ways. The join was drawn about the end of the edge before: the rectangle 100 by 30 with its
right side bowed 6 in, grown by 40 with round joins, stood 40.18 off or came back as nothing, depending on the corner
it started from. It is now one loop of 18 397.80, rounded by the circles of 40 about (100, 0) and (100, 30). The chain
10 along x, a half turn of radius 5 below the axis and 10 more, offset by 10 upwards, is one chain 30.472 long. Loops
whose runs meet out of order, lenses of two arcs, and the outside of a cusp where an arc meets a side reversed are no
longer dropped: the rectangle 20 by 10 whose left side is a half circle bowed in, grown by 2, came back empty.

**FIXED.** Curved offsets keep what stands the offset distance from the shape and nothing else. A corner that closes
is no longer bridged where its moved pieces do not meet: the chain 10 along x and then round an arc of bulge 0.5
curling back, offset by 1 to the left, is the segment to x = 7.271 and the arc from (7.484, -1) on, not one piece with
a chord across the fold. A piece partly folded over is cut where it comes nearer than the distance rather than kept or
dropped whole: the hairpin 1 wide, 10 along x, 1 down and 9 back, offset by -0.6 into its inside, keeps (0, -0.6) to
(0.553, -0.6) where it gave nothing. Chamfers and miters cut short inside a fold are no longer kept, and two moved
pieces lying along each other are kept once. Offsets of curved shapes with hundreds of edges are 8 to 75 times faster;
shapes of a few edges take about twice as long, some 12 to 20 microseconds a call.

**FIXED.** A curved offset at a tolerance of its own no longer throws "The edges of a chain must run end to end": its
pieces were joined within the tolerance passed but put together within the global one. The chain from the origin to
(-5, 0) and round an arc of bulge -0.5 to (-7, 5), offset by 0.3 with an unlimited miter at 0.1, is now one chain from
(0, -0.3) to (-7.256, 5.156). A Round offset past an arc whose radius runs out no longer throws "An arc sweeping a whole
turn has no chord": the U from (10, 0) to the origin, round the half circle of radius 2 about (0, 2) and back to (10, 4),
offset 2.5 to its inside, gives nothing. This applies to `GeoPolylineArc2`, `GeoPolygonArc2`, `GeoPolygonArc3.Offset`
and `OffsetInPlane` of `GeoPolylineArc3`.

**FIXED.** Fillets and chamfers that take an edge whole, give or take the tolerance, no longer throw or leave stubs:
the pieces either side meet at one point, and a chain keeps both its end points. Three sides of the square of side 10
filleted at 5.02 at a tolerance of 0.1 threw "The edges of a chain must run end to end"; they now turn at (10, 5). A
chamfer of 10.05 on a first edge 10 long moved the chain's start to (-0.05, 0), and the square chamfered at 5.04 came
back crossing itself. `Corner3`, which the Tekla bars and ties go through, leaves a bend whose arc is too small for the
tolerance instead of throwing "Two points in the same place have no chord to bulge", and no longer throws "The X and Y
directions must not be parallel". A loop that would keep fewer than three corners is given back unchanged.

**FIXED.** A fillet is now the arc on the inside of the corner, running the way the two edges run and touching
neither past the corner. Where the radius times the cotangent of half the turn was within the point tolerance, a
circle outside the corner could win: the chain (0, 0), (10, 0), (3, 5) filleted at 0.002 ran on to (10.00064, 0) and
turned back through 35.5° for the corner's 144.5°. It now stops 0.0062 short and turns through 144.5°. `Corner3`,
`GeoPolygonArc3.Fillet` and the Tekla bars and ties built through them follow; no real bending radius was affected. A
tip where no circle of the radius fits inside is now left alone and `TryFilletAt` says false: the tip of 157.8° between
arcs of radius 25.2 and 75.5, filleted at 50, used to get an arc outside that grew the area from 167.2 to 185.5. A
corner against an arc that turns by no more than `Tolerance.EqualAngleRad` is left alone, as the guide says. A straight
running on into a tangent arc used to get an arc of no sweep between two copies of the joint 1E-15 apart. Fillets of
curved loops are also faster, up to twelve times where most corners are nearly straight.

**FIXED.** Two segments now collide exactly where their distance is at most the point tolerance, and measure nought
only where they truly cross or overlap. The crossing of their lines was accepted up to the tolerance past the end of
each, so two corners up to twice the tolerance apart collided and measured nought: the segment from the origin to
(10, 0) and the one leaving (10.00135, 0.00078) at 60 degrees, 0.00156 apart, collided at 1E-3 and measured 0. Now they
do not collide at 1E-3, and measure 0.00156 at every tolerance. Rectangles, polygons, triangles, faces, polylines,
curved chains and curved loops follow, in their collisions and in the distances that took nought from a crossing, and a
polygon with two corners 0.00127 apart is now simple at 1E-3. Two rays likewise measure nought only where they cross at
or ahead of both origins. `TryIntersectWith` and the segment's shortest line keep the slack, since they place points
for cutting and extending, and shapes laid out in a plane in space read as before.

**FIXED.** A polygon, rectangle, triangle or face now collides with a segment, a ray, a polyline or another such shape
wherever the two come within the point tolerance, not only where they cross or one starts inside the other. A hole's
rim counts as boundary. The segment from (40, 100.01) to (60, 100.0005) ends 0.0005 above the square 100 by 100. It did
not collide with the square as a polygon, rectangle, triangle or face at a tolerance of 1E-3, though the same square as
a curved loop said it did; now all agree. Distances are unchanged. Label placement follows: a place within the
tolerance of a line or a region kept clear is no longer taken, though their boxes do not overlap. Two labels are still
judged on their boxes. A pair that does not collide is checked about a third slower on shapes of thousands of edges.

**FIXED.** Every pair of curves now collides where the two come within the point tolerance, not only where they cross
or one holds the other. That covers an arc, a curved edge, a polyline against a curve, a curved chain, a curved loop,
and a circle against a curve, and also a polygon, rectangle, triangle or face against an arc, curved chain or curved
loop. An arc ending 0.0005 from another arc did not collide with it even at a tolerance of 0.1, though their distance
was 0.0005; it now collides at 1E-3. A polyline lying along a straight edge of a curved chain collided with it at no
tolerance, though their distance was nought, and now does. Distances are unchanged.

**FIXED.** A curved chain or loop that drops a vertex as a repeat of the one before keeps the bulge of the edge that
still has a length, in the plane and in space. The chain (0, 0), (10, 0), (10.0004, 0), (20, 0) with bulges 0, 0, 1
lost its half circle to the chord, and a rectangle 100 by 50 filleted at radius 0.0005 came back with arcs of radius 35
and 71 and an area of 7 140. Now the half circle is kept, and the rectangle keeps four straight sides and its area of
5 000. A loop built from edges, by a fillet, a chamfer, an offset, a transform or a projection from space, drops an
edge too short to keep, the closing one too. A loop given a repeated first vertex at the end still closes as the guide
says.

**FIXED.** Two circles, and two arcs, now meet at the same points whichever is asked first; the crossings are worked
out from the smaller circle. A nearly flat arc lost its crossings when its huge circle came first. The edge 100 long
of bulge 1E-7 from (0, 0) to (100, 0) missed the arc of radius 40 about (50, 30) from 3.4 radians sweeping 1.6, and
measured 8.357 to it, while asked the other way round they crossed and measured nought. The crossings came out 0.076
off at bulge 1E-7 and up to 982 off at 1E-9; they are now within 1.3E-6 and 1.03E-4. The same held for fillets, mitred
offsets and arcs in space. The chain from (0, 0) by bulge 1E-7 to (100, 0), then by bulge 0.4 to (110, 30), was left
unrounded by a fillet of radius 3, and its offset of 2 took a stray corner at (99.06, 1.77). Both are now rounded and
mitred as beside a straight edge. Crossings of ordinary circles keep their last bit when the smaller is asked first,
and move by at most 2E-12 otherwise.

**FIXED.** A loop that may curve reads a point near the chord of a bowed edge by its true boundary. Only the boundary
itself is read with the tolerance, and every other point is in or out exactly. The square 100 by 100 with its top bowed
20 inwards held the point (50, 99.95), 0.05 under the chord and 19.95 above the arc, at a tolerance of 0.1, and
measured it nought away; it is now outside and 19.95 away. A point exactly on a chord that is not the boundary is read
as lying a hair towards +X of it. A segment or ray now also collides with such a loop wherever it comes within the
point tolerance of the boundary, not only where it crosses it or starts inside, and its distance still measures the
gap. Locating a point in a curved loop is about a fifth faster.

**FIXED.** A segment whose line runs inside a circle within the point tolerance of its rim crosses it where it truly
does, rather than once at the foot of the perpendicular; two crossings within twice the point tolerance of each other
are still one. The segment 50 long from (0, 49.9995) along (1, 0.001) leaves the circle of radius 50 at
(0.1791, 49.99968). It now has a crossing there and a shortest line of no length, where it had no crossing and a
shortest line 21 long. A segment lying inside the rim within the tolerance without crossing it still touches the
circle, or the arc, once: at the foot when that is on it, as before. Arcs, curved chains, rectangles, polygons,
triangles, faces and rays against circles and arcs follow. So do extending and trimming a segment to a circle, and an
end already within the tolerance of the circle is left where it is.

**FIXED.** A segment collides with an open curve (a polyline, an arc, a curved edge or a curved chain) where the two
cross or come within the point tolerance, as two segments do, asked from either side, and so does a ray. Before, the
segment from (0, 0) to (21, 0) lying over the polyline from (10, 0) to (20, 0) did not collide with it, though it is
nought from it. A segment ending 0.0005 short of an arc did not collide at a tolerance of 0.001, and nor did a curved
chain lying along a segment. Distances are unchanged. A segment passing within half a unit of an arc's rim is checked
about twice as slowly, since its distance is now measured.

**FIXED.** Queries in the plane that are given a tolerance read it all the way down. Some read `Tolerance.Global`
underneath, so a segment shorter than the global point tolerance was taken as a point: under a tolerance of 1E-4 the
slivers from (0, 0) to (0.0004, 0) and from (5.0008, 0) to (5.0004, 0) measured 5.0004 apart, and now measure 5. The
same holds for an arc against a segment; a circle against a segment, polygon, rectangle or ray; a rectangle against a
polygon; a face against a rectangle; an ellipse against a polyline, polygon or rectangle; and a point on a segment. The
forms without a tolerance read the global one, as before, and under the default tolerance nothing changes. A shape
still drops its repeated corners under the global tolerance when it is made.

**FIXED.** A segment against a rectangle read whether the rectangle holds the segment's start under
`Tolerance.Global`, whatever tolerance was passed. Under a tolerance of 1E-4 the segment from (50.0002, 0) to (60, 0)
collided with the square 100 by 100 centred at the origin, though it starts two ten-thousandths outside it; it now
reads the tolerance given and does not collide. A polyline or a straight edge against a rectangle follows, and so does
label placement given an `ArrangeOptions.Tolerance` other than the global one. Under the default tolerance nothing
changes.

**NEW.** `GeoRay2`, the ray in the plane: an origin and a direction it runs along for ever, the counterpart of
`GeoRay3`. It is asked what a segment is asked, against every shape in the plane, and every shape that takes a
`GeoLine2` takes a `GeoRay2` back; an ellipse is asked from the ellipse, as for a segment. The answers read as the
segment's do: the ray from (-50, 50) along X crosses the square 0..100 at (100, 50) and (0, 50), edge by edge, and the
same ray turned round is 50 from it. Against a shape the ray is asked as the segment from its origin to a point past
the farthest the shape reaches along it, and answers as that segment does: nothing of the shape lies beyond. Two rays are
measured directly, and are nought apart wherever they meet, at any angle. `PlanarMap.ProjectToRay2` and `ToRay3` carry
a ray between space and a frame, `GeoBvh2.GetIntersections` takes a ray, and `GeoTransform2` transforms one. No
existing answer changes.

## 12.1.0

**FIXED.** `GeoPreparedSolid3` and `GeoBvh3` read a ray from a plane's tolerance band as the body itself does. Their
ray members had kept the old reading after the fix of a ray dipping from the band, below, so the ray from (5, 5, 0.0004) along (1, 0, -0.01) crossed a
box's top at (5.04, 5, 0) for the body and nowhere for its prepared form. The clash's bars go through them: a bar 400
long starting 0.0004 over a plate and dipping into it at half a degree was all inside it, and is now 399.954 inside, as
the closed form gives.

**FIXED.** Two triangles in space give the same answer whichever is asked first, and touch wherever they come within
the point tolerance, as two polygons do. Before, whether they lay in one plane was read from one corner of the second
only, so 211 of 5 000 near-coplanar pairs collided one way round and not the other, and contacts at 1E-5 rad were
missed. Solid collision and the clash go through this test; on the code-built models checked, none of their results
changed, and the clash takes as long as before within 7 %. Two bodies whose surfaces pass just outside the tolerance of
each other are checked about a fifth slower.

**FIXED.** A ray starting within the planar tolerance of a plane lies in it only when it runs level to rounding. Before,
one dipping under the angle tolerance was read as lying in the plane and crossed nothing there: the ray from
(5, 5, 0.0004) along (1, 0, -0.01) missed the square 10 by 10 in the floor it crosses at (5.04, 5, 0). It now crosses
where it reaches height nought, and one rising from the band touches at its origin. Ray against triangle, polygon,
face and body follow, with the ray distances and shortest lines. The ray casts that count crossings, inside `Locate`
and the booleans, keep their reading, and none of their results changes.

**FIXED.** Two oriented boxes whose nearest edges cross at an angle no longer count as touching more than the point
tolerance apart. The separating-axis test allowed the point tolerance along an edge-to-edge axis before that axis was
brought to unit length, so it allowed the tolerance over the sine of the angle between the edges: boxes 0.005 apart
across two skew edges were nought away and collided. Their distance is now the gap between their bodies.

**FIXED.** A body is nought from a plane only where a face of its material reaches it. Before, the distance read all
the body's corners at once, so two pieces of a body either side of a plane, neither touching it, were nought away:
boxes at z 10 to 20 and -20 to -10, made one body by two shells or cut apart by an opening, are now 10 from z = 0, as
the collision and the shortest line already said.

**FIXED.** A polygon or face in space is checked against a body's material, openings included, and a face's holes no
longer hide the contact its own edges make. Before, a face with a hole standing on its corner on the top of a box 40
by 40 by 20, or crossing its top, did not collide with it, though its outline did; and a polygon passing through an
opening in a body, clear of its walls, collided with the face the opening had cut away. Checking a region within reach
of an opening now pays for cutting the opening, about 0.6 ms on .NET Framework 4.8; `TryCutOpenings` cuts a body once
for many questions.

**FIXED.** A segment or a ray lying in the plane of a triangle, polygon or face is laid out in that plane and touches
the shape where it reaches it there. Before, `CollidesWith` said false for the segment from (-10, 10, 0) to (110, 10, 0)
across the square 100 by 100 in the floor, though its distance was nought; a segment lying in a face's hole still
touches nothing. The distances of a segment, chain or ray to a triangle, polygon or face now read nought wherever the
two touch, so a segment lying level 0.0004 over a triangle is nought away, as it collides; the shortest line keeps the
true gap. Two flat regions in one plane, one of them a sliver narrower than the tolerance, no longer throw.

**NEW.** Flat shapes in space say whether and where they meet: `CollidesWith`, `GetIntersections` and
`TryIntersectWith`, from both sides and with and without a `Tolerance`, for 39 pairs that neither side answered.
Triangles, polygons and faces against each other, a plane, a ray and a body; a plane against a ray and a body; ray
against ray; the boxes against these, as their body; and an edge against the arc chains. Two regions in planes that
are not one meet where an edge of either reaches the other, and the crossings named are the ends of what they share: a
square 100 by 100 in the floor and one standing across it along y = 50 cross at (100, 50, 0) and (0, 50, 0). In one
plane the plane half answers. A bare `null` is now ambiguous in `Intersection3.GetIntersections(GeoPlane3, null)` and
`Collision3.CollidesWith(GeoFace3, null)`.

**NEW.** `DistanceTo` between 29 pairs of straight or flat shapes in space that neither side measured, from both sides
and with and without a `Tolerance`, and `GetShortestLineTo` for the 16 of them without a box: faces against every
straight and flat shape and a body, polygons against polygons, triangles and rays, a plane against them, rays against
rays and chains, and the boxes, as their body. Two shapes that touch are nought apart; otherwise the distance is the
least of every edge of each against the other, which is exact: a segment through a face's hole, 15 from its rim, is
15 away.

**NEW.** `GetShortestLineTo` in space wherever `DistanceTo` was already answered: 21 pairs, from both sides and with
and without a `Tolerance`. They are a point to every curve and region, a segment to a plane, a polygon and a chain,
chains to chains, triangles, polygons, planes and bodies, a polygon and a plane to a body, and `GeoEllipse3` to a
point. The line leaves the shape asked and lands on the other; flat regions are read as their distance reads them
(a point 30 above a square is joined straight down, 30 long), bodies surface to surface (a chain inside a box, 10 above
its floor, is joined to it by a segment 10 long where the distance is nought), and a shape lying level within the
point tolerance keeps its true gap, so a chain 0.0004 above a plane gets a segment 0.0004 long; one coming at a
polygon, a triangle or a body end-on within the tolerance is read as crossing it, as the segment to a triangle always
has been. Boxes, two planes and two points have none, on purpose. Every
existing `DistanceTo` gives the same bits as before.

**NEW.** `GeoFace2` is measured by its material, the outline less its holes. `DistanceTo` against Arc, Circle, Edge,
Face, Line, Polygon, PolygonArc, Polyline, PolylineArc, Rectangle and Triangle, and from `GeoEllipse2`, is nought where
the two collide and otherwise the gap between their boundaries: a plate 200 by 200 with a hole 40 by 40 in its middle
is 10 from a circle of radius 10 about its centre and 40 from one about (250, 100). Face against face gains
`CollidesWith`, `GetIntersections`, `TryIntersectWith` and `GetShortestLineTo`; a face lying in the other's hole does
not collide with it.

**NEW.** Every query reads from either side. Wherever one shape could be asked `DistanceTo`, `CollidesWith`,
`GetIntersections`, `TryIntersectWith` or `GetShortestLineTo` about another and the other could not be asked back, it
now can: 87 pairs, among them every shape in space against a `GeoPolyline3`, segments and rays against the arc chains,
and the plane's curves against faces, rectangles and triangles in `TryIntersectWith`, plus seven forms taking a
`Tolerance` that only one side had. Each answers exactly as the other side does, a shortest line reversed so that it
leaves the shape asked. A bare `null` or `default` given as the shape can now match more than one overload and has to
be cast: `GetShortestLineTo` on `GeoLine3`, `GeoPoint3` and `GeoPoint2`, and `GeoLine3.DistanceTo(shape, tolerance)`.
Code already compiled is not affected. A null argument of a class type to the new members throws
`ArgumentNullException`. Ellipses are still asked from the ellipse, and a point answers with `IsPointOn` and
`Contains`.

**NEW.** `GetShortestLineTo(GeoPoint2)` on every shape in the plane, and `GeoPoint2.GetShortestLineTo` to every one
but the ellipse. It runs from the nearest point of the boundary, as every shortest line does: a circle of radius 50
gives the point (80, 0) a segment 30 long, and the point (30, 0), inside it, one 20 long, where `DistanceTo` reads the
disc filled and says 0.

**NEW.** `GeoEllipse3`, an elliptical disc in space, with its static forms in `Core.Ellipse3`. It is read as a
`GeoCircle3` is read: a disc for `DistanceTo`, `Locate`, `Contains` and the nearest point of the surface, the rim for
the nearest point of the boundary and for crossings. Everything in its own plane is `GeoEllipse2`'s arithmetic, so an
ellipse 300 by 100 is 1 336.4893 round whichever way it stands. It takes every transformation that can be undone, an
uneven scaling included, which a circle refuses; `GeoEllipse3.FromCircle` makes a circle one first. `CollidesWith`,
`GetIntersections` and `TryIntersectWith` work against everything a circle in space is asked about, another ellipse
included, with nothing sampled: 2 000 random cases of each kind of pair, planes down to a thousandth of a degree apart,
give no wrong count and no point more than 1.3E-7 off. `ProjectToPlane`, on `GeoCircle3` and `GeoEllipse3`, gives the
shape seen square onto a plane: a circle of radius 300 tilted 60° from the plan is an ellipse 300 by 150 in plan.
`ProjectToEllipse2(frame)` and `GeoEllipse2.ToEllipse3(frame)` carry an ellipse between space and the plane, and
`Mesh3.ToMesh` meshes one, its own axes along the major axis. GeometryHelper.CadConvert reads and writes the AutoCAD
`Ellipse` with `EllipseConvert`, and reads a tilted `Circle` as the ellipse it is in plan.

**CHANGED.** The clash check, `Intersection3.IsSimple` and the triangulation of a face with holes
(`TriangulateSurface`) sweep along whichever of X, Y and Z leaves the fewest pairs to look at, rather than always
along X, and label placement finds what lies near each label through an index rather than by going over every block
and every other label. Every result is the same as before to the last bit: the new benchmark suite signs each result,
and every signature of 12.0.0 holds. Measured on .NET Framework 4.8, median before and after: 4 900 bars 6 000 long
laid side by side along X are checked for clashes in 6.7 ms on one thread rather than 255; a comb of 4 000 edges along X is found
simple, and found not simple with a corner pushed in, in 2.8 ms rather than 33; a comb face of 2 000 edges is
triangulated in 37 ms rather than 51; 5 000 labels on parts far apart are placed in 0.21 s rather than 8.0, 1 000 in
41 ms rather than 341, and 1 000 sharing 300 regions and every leader in 0.14 s rather than 0.54. Parts, edges and
labels that already lay well, along Y or Z or close together, take as long as before, within 2 %.

**FIXED.** A segment nearly tangent to a circle or an arc in space no longer loses the two points it crosses at. The
band within which a line is read as grazing the rim was a half chord of half the point tolerance times the segment's
length, so a segment 1 000 long through a circle of radius 100, 0.0012 inside its rim, merged a chord 0.96 long into its
middle, 0.0012 off the rim, and found nothing; a segment 200 long through a point of the rim, leaning 5E-4 rad off the
normal, lost that point the same way. The band is now half the point tolerance in drawing units, and both give their
points. GeoEllipse3, which hands its arcs to the same code, gains the fix with it.

**FIXED.** Two coplanar circles or arcs in space a hair apart, outside each other, touch where they come within the
point tolerance, as they do in the plane. Circles of radius 250 and 100 with their centres 350.0004 apart gave nothing,
since the radical line runs between the rims, off both; they now touch once, at the middle of the gap, 250.0002 from
the first centre, as GeoCircle2 says. One a hair inside the other was fixed the same way in 12.0.0.

**FIXED.** A circle or an arc in space no longer loses crossings with a plane, a shape or another curve in a plane
nearly its own, far from the origin. The line two planes share was found from the planes' distances to the world
origin, so its error grew with that distance over the sine of the angle between them: two circles about
(30 000, 30 000, 30 000), in planes a thousandth of a degree apart, gave no point for two. It is now found from the
circle's own centre, as GeoEllipse3 finds it, and 2 000 random cases of each kind, planes down to a thousandth of a
degree apart within 1 000 of the origin, give no wrong count and no point more than 1.2E-7 off, where 1 of 300 and 2 of
300 had been wrong.

## 12.0.0

**BREAKING.** Label placement keeps one algorithm, the greedy one. `ArrangeAlgorithmType` and `ArrangeOptions.Algorithm`
are removed, and with them the options only the other four read: `MaxBacktrackSteps`, `AnnealingInitialTemperature`,
`AnnealingCoolingRate` and `ForceIterations`. Bounded backtracking, simulated annealing, force-directed placement and
constraint satisfaction are gone. The greedy placement, the default all along, places every label where it did, bit
for bit: its results still hash to those of 6.2.0, and 400 scenes of 20 to 79 labels, with sides, gaps, caps and
tolerances varied, give the same bytes as 11.2.0 under .NET Framework 4.8 and .NET 10, in the same time. Code that set
`Algorithm = ArrangeAlgorithmType.Greedy`, or left it unset, places every label as before once that line is deleted;
code that set one of the four options deletes those lines too. Code that chose another algorithm deletes the line the
same way, and gets the greedy placement.

**NEW.** `Angle.ToReadableDegrees`, and `Angle.ToReadableRadians` in radians, turn the angle of a text the way up it
reads: into the range above -90° and up to 90°, both raised by a tolerance, by adding or taking off a half turn, any
number of turns wrapped first. The tolerance is a tenth of a radian, 5.73°, unless given, as Tekla Structures takes it,
so 95° stays 95°, 100° gives -80° and 180° gives 0°. The radian form is worked out in radians throughout, rounding
nothing through degrees.

**NEW.** `GeoEllipse2`, an ellipse as a region, and `Core.Ellipse2`, which holds its operations. An ellipse is a centre,
the direction of its major axis and the two semi-axes, the major one at least as long as the minor; `FromCircle` makes
one of a circle. Its rim is walked by the eccentric angle, the angle AutoCAD and IFC trim an ellipse by: on the ellipse
300 by 100, the angle 45° is the point (212.13, 70.71), 18.43° off the axis seen from the centre. It measures its area,
its length by the arithmetic-geometric mean, 1 336.4893 for that ellipse, and lengths along the rim by Carlson's
elliptic integrals, each quarter exactly a quarter of the length. It answers what a circle answers, read as a filled
region and in the circle's terms: containing and locating a point within the point tolerance as a distance to the rim,
the nearest point of the rim by Eberly's bisection, and distances, shortest lines, crossings and collisions against
every shape a circle takes and against another ellipse, with `Contains` for a segment, a circle or an ellipse whole.
Crossings with a segment are a quadratic, and with a circle, an arc or an ellipse the quartic of the two equations, so
nothing is sampled but the distance between two ellipses, whether one holds another, and where two touch, each where the
quick reading leaves it in doubt, searched round the rim and refined; a touch is found whichever ellipse asks, and the
distance agrees with an independent dense search to 3E-13 relative on 80 000 pairs as thin as a thousand to one. Every
transformation that does not flatten the plane takes an ellipse to an ellipse, and a mirror reverses the way its angle
runs. It flattens by its curvature, symmetric about both axes: an ellipse 1 000 by 1 takes 84 edges at the automatic
tolerance, where even steps would need about 1 571. It triangulates and meshes as a circle does. It has no offset, since
the curve a fixed distance from an ellipse is not one, and no boolean: flatten it first. The other shapes take no
ellipse, so a question is asked from the ellipse. `GeoCircle2.TransformBy` still refuses an uneven scaling, and its docs
now point to `FromCircle`.

**NEW.** `GeometryHelper.Takeoff`: `VolumeTakeoff.Run` takes off the volumes of the parts of a model, each bit of
material counted once, by the part ranked first among those holding it: the higher `VolumeItem.Priority` first, cutting
it out of the lower; of two the same, the larger `VolumeItem.Id` where one is given, so that a Tekla part's
`Identifier.ID` takes ties as HDC WBS does, then the earlier in the list. A slab 6 000 by 6 000 by 200 on a beam 300
wide running through a column 400 by 400 loses 0.032 m3 to the column and 0.336 m3 to the beam, the block 300 by 400 by
200 inside the column taken off once, and keeps 6.832 m3; the three parts add up to 8.352 m3, the volume of the three
together. Only what the parts share is cut, never the parts. A common part the booleans cannot make is worked out by
slicing, exact over flat faces with nothing snapped, across the axis that needs the fewest planes: a curved wall and
girder of a Tekla model, whose common part comes back as 4 256 faces and not valid, share 2 226 236 967 mm3 by slicing,
in 0.04 seconds, where Tekla's own boolean gives 2 226 236 773. A pair as near-coincident as that wall and girder, a
hundred or more pairs of faces under a millimetre apart and under a tenth of a degree off parallel, is sliced without
the booleans being tried; on those two they ran a minute or two before failing. Each result gives the gross, the
deductions by the part that kept them, the net and, where something could not be worked out, an issue saying the net is
an upper bound. It runs in parallel, and the results are the same, bit for bit, on one thread or on every processor
(`VolumeTakeoffOptions.MaxDegreeOfParallelism`). On a Tekla model of 30 921 parts, 69 040.90 m3 gross, it keeps 67
369.17 m3 net with a contact of 0.01 and a fallback of 0.01, no result carrying an issue, in 197 seconds on 24
processors.

**FIXED.** A circle a hair inside another, within the point tolerance of touching it without crossing, touched it at a
point read off the radical line of the two, which runs away from both as the difference of the radii shrinks: a circle
of radius 99.94 a centre 0.0593 off the middle of one of 100 touched it at (101.18, 0), 1.18 off both rims. It now
touches at the point between the two rims where they come nearest, (99.99965, 0). An arc of such a circle was missed
altogether, since the arc did not reach the stray point: the left half of a circle of radius 101.5004 about (1.5, 0),
0.0004 outside the disc of radius 100, neither met nor collided with it, though `DistanceTo` said 0.0004; it now touches
it at (-100.0002, 0). The same holds in space, where two coplanar `GeoArc3` or `GeoCircle3` so placed found no point and
did not collide; they now touch between the rims as in the plane. Circles that cross, touch from outside or stand apart
are answered as before, bit for bit.

**FIXED.** A boolean with a contact (`SolidBooleanOptions.Contact`) threw `ArgumentException` when putting the second
body onto the first left too little of it to build a body: a wedge 6 µm thick lying on a face of a box, the triangle
(249.994, 90), (250, 90), (250, 162) carried 900 up, lost its slanted face and its narrow side to nothing.
`TrySubtract`, `TryIntersect`, `TryUnion` and `TrySubtractAll` now take such a body as it came, as they already did
where putting it on would leave it open: the box less the wedge holds 56 249 805.6 and the two share 194.4. A girder and
a wall of a Tekla model lying 6 µm into each other hit it. Every boolean that did not throw is worked out as before, bit
for bit.

## 11.2.0

**NEW.** `TrySubtractAll` takes `SolidClosingOptions` as well, and then closes a cut it would skip: the cut worked out
within the tolerance is closed by `TryClose`, and where that does not close, the cut within the fallback. A body closed
is taken only where it is valid within the booleans' tolerance too and holds what a cut can leave, no more than the body
before the cut and no less than that less the whole tool, each within the tolerance times the area of the two. No cut is
closed where the body before it or the tool carries openings: an opening cut out of a body not closed takes material
before any closing can, and such a cut is skipped as without closing. The volumes are read by the faces, so a body not
valid before the cut can refuse a closing that is right, and a body not valid as given is closed at its first cut that
would be skipped, even by a tool that does not reach it. Every cut taken without closing is taken as before, bit for
bit, and the overload without closing is unchanged. `SubtractReport` says which cuts were closed (`Closed`, counted in
`Taken`), how (`Closings`), and which closed but were refused for their volume (`RefusedByVolume`, still in `Skipped`);
a cut closed from the fallback's result is in `WithinFallback` too. Of the 79 864 parts of a Tekla model cut within a
thousandth with a contact and a fallback of a hundredth, 17 have a cut skipped, 35 of their 314 cuts; closing within a
gap of 0.005, holes up to 100 and 0.01 out of flat, takes 9 of them, one from the fallback's result, and leaves 10, the
cuts after one closed taken where they were skipped, and none refused for its volume. The 8 of those parts whose bodies
cut within a hundredth are valid come out within 0.12 % of those, where skipping left them up to 10 % off; the 0.12 % is
a part no closing changes, and the parts closed come out within 7E-5. The 17 take about two thirds of the time they
took.

## 11.1.0

**NEW.** `GeoSolid3.TryClose` closes an open body with the least change that does it, and says what it changed, or,
changing nothing, why it could not and where: a body read out of a model open by copies of an edge a few thousandths
apart, by a corner standing off the edge of the face beside it, or by a face left out. `SolidClosingOptions` says how:
within which `Tolerance` the result is valid; `MaxGap`, which has to be given, how far apart the corners across a gap may
stand and still be made one; `MaxHoleArea`, the most a hole may enclose and still be filled, nought by default, so that
nothing is filled unless asked; `MaxOffFlat`, how far out of flat a hole may stand and still be filled by triangles,
nought by default; and `Fill`, a `FillStrategy`: a hole filled only where it can be filled one way, `WhenUnambiguous`,
by the least area anyway, `MinArea`, or not at all, `None`. The report, a `SolidClosing3`, lists each change in the order
it was made, a `SolidRepair3` of a `SolidRepairKind`, with the area added and the volume moved; a body not closed reports
the `ClosingFailure` of the first step that could not go on, and a point at the trouble. A body valid already comes back
as the very instance, and `TryClose(out closed, tolerance, maxGap)` welds and fills nothing. Each step is taken only
where the ones before did not close the body:
- Faces covering nothing are dropped, a face given twice is taken once, and a face lying back to back on another with
  every edge of it open goes; the faces are turned so that each shell is wound alike, and each closed shell inside no
  other outwards with every shell inside it, a shell inside another keeping its winding against it, a block within it
  or a cavity: an inside-out box 30 by 20 by 10 comes back with six `Flip`s, 2 200 of area, and a box with a box inside,
  both wound outwards, 6 600, one face of the outer turned, gets that face alone turned back.
- Corners across a gap are welded within the point tolerance, then twice it, four times and so on up to `MaxGap`, the
  least reach that closes the body taken, and only where welds alone close nothing at any reach is a corner standing off
  an edge left open put on it. A box's corner moved 0.003 out in its top is welded back within a gap of 0.005; moved
  0.02, it is `GapTooWide` there, and closed within 0.03. Beside a chamfer whose ends stand 0.07 apart, a corner moved
  0.0015 is welded within two thousandths and the chamfer kept, though the gap allowed is a tenth. Two corners of one
  face are made one only where they are copies of one corner side by side across an edge left open, so that a slot cut
  thinner than the gap stays a slot. No reach is taken that welds a face through another: a strip 0.004 wide left along
  a top, a blade 0.002 thick standing in it, is not welded shut through the blade within 0.005, and is `StillOpen` at
  the blade. A sphere of 6 240 faces with one copy of a corner moved 0.003 closes in a second under .NET Framework 4.8
  and a fifth of one under .NET 10, most of it in checking the body as given and as closed.
- A flat hole is filled by one face on the body's own corners, the loops in its plane inside it the face's holes: a box
  30 by 20 by 10 missing its top by one `Fill` of 600, the volume moved 1 000, where `MaxHoleArea` is at least 600, and a
  plate with a hole through it missing its top by the top again, 800. A plate whose hole has lost its four walls can be
  capped or walled round, and `WhenUnambiguous` takes neither, `HoleAmbiguous`, where `MinArea` takes the caps, 200 of
  area against the walls' 400; a hole slanting or tapering through the plate is paired the same way, corner for corner
  where its rims turn, and never by walls or caps crossing a face.
- A hole out of flat by more than the planar tolerance and no more than `MaxOffFlat` is filled by the triangles of least
  area across it on its own corners, of the ways none of whose triangles lies back to back with a face of the body or
  turns back against the loop, so that an L-shaped top lifted at a corner is filled across its inside, and under
  `WhenUnambiguous` only where those ways close one volume within its area times the planar tolerance: the top of a box
  100 by 100 with a corner lifted 0.005 by two triangles, the two ways across 8.3 apart within a bound of 10, and a hole
  of 200 corners out of flat by 198 triangles in 30 milliseconds. A box 10 by 1 by 1 missing its top and front has one
  loop round both: the least area across it, 15.1, lays a triangle on each end face, and is no way; of the six ways that
  lie on no face, four close the box whole and two 8.3 of its 10, so `WhenUnambiguous` refuses it, and `MinArea` takes
  the two faces again.
- Nothing is made up: an edge left open past a fin, or a corner two holes meet at, is `NonManifold`; a gap wider than
  `MaxGap` that no fill closes is `GapTooWide`, and one a weld would close only through a face standing in it, as a
  blade in a crack, `StillOpen` there; a hole larger than allowed, or out of flat with more than 256 corners, is
  `HoleTooLarge`, as is any hole where no fill is allowed; one further out of flat than allowed is `HoleOffFlat`; and a
  hole whose every fill would cross a face of the body or a fill taken before it, as the top of a box with a post
  standing out through its plane, which would read valid, and any other body not valid at the end, are `StillOpen`.

On real parts. The 1 989 parts of a Tekla model, each valid as read, were damaged five ways and closed within a gap of
0.005, holes of any size filled and up to 0.01 out of flat: with the largest face left out 98.6 % of them closed, with
a face left out at random 99.9 %, with every face on copies of its own corners moved up to 0.002 in its plane 98.2 %,
with both of those 92.7 %, 93.6 % under `MinArea`, and with a face turned over every one. That is 9 736 of the 9 945
bodies, 9 754 under `MinArea`, each valid, none further from the part's volume as read than 13 parts in a million, in
0.8 milliseconds as a median and 3 seconds at most. Of those refused, 103 are `NonManifold`, as where the rim of a
face left out runs twice through one corner, at the foot of an edge where two pieces of a slab meet, and 86 are
`HoleAmbiguous`, 69 under `MinArea`, as where a face left out holds openings and the welds put its rim out of flat.

Of the 35 cuts `TrySubtractAll` skips on the 79 864 parts of a Tekla model, the cuts within a thousandth that came out
open or not valid, 23 closed within a gap of 0.005, holes up to 100 filled, none further from the volume the same cut
within a hundredth gives than 2.1 parts in a million, in 12 milliseconds as a median and 0.3 seconds at most.
Taken in place of the cuts skipped, they bring the 8 of those parts whose bodies cut within a hundredth come out valid
to within 0.12 % of those, where skipping left them up to 10 % off. Of the 12 refused, one holds a hole of 283, closed
where any hole may be filled; the other 11 are not open by a gap or a hole but cut wrong, by fins and by slivers wound
the wrong way, and the same cuts within a hundredth come out not valid either.

## 11.0.1

**FIXED.** The gluing of a union, a difference or an intersection of solids took two faces of one cell, its top and its
bottom where it is thinner than the tolerance, for faces lying back to back, and took from each what they shared, which
left the cell's edges round a hole: a block whose low step's top was out of flat by 0.0015, read as triangles, less a
box whose top lay along it, came out open, 0.7 litres short, and their union 1.3. The faces of one cell, or of the body
taken whole, are not paired with each other now. Of 40 such blocks each of six warps on a grid of 6 by 6, 15 came out
open or not worked out within 0.0012 where 3 do now, and none came out wrong and valid either way. A cell thinner than
the tolerance is kept as the material it is: within a thousandth alone, a part of a Tekla model kept a sheet of 0.0008
it lost before, 4.3 square metres more surface on 2 151; with a contact of a hundredth, as many parts as before.

**FIXED.** `PlanarMap.FrameOf`, and with it `GetFrame()` of a polygon or a face, took its first axis towards the first
corner standing apart from the first, out of the plane or not: two corners a hair apart one over the other, as a face
a cut leaves of a top out of flat can have, made the axis the normal itself, and no frame could be made. The gluing of
a boolean lays faces out in their frames, and threw: a block whose low step's top was out of flat by up to 0.0008 less
a box whose top lay along it was not worked out, nor their union. The axis runs along the first direction that has a
length in the plane now; where a direction was only partly out of the plane, the frame no longer tilts with it.

**FIXED.** A union or a difference of solids that came out open whichever body was cut alone went on to cut both by
every plane of both, and took what their cells glued into where it closed, without asking whether a cell a plane could
not cut was judged apart across it, as the one-body cuts ask: a slab of a Tekla model less a tool beneath it came out
open either way, the tool cut holding what the two share, 40.43 litres; cut by every plane of both, the slab kept a cell
the tool's top could not cut, judged within the tool, and closed three litres short, valid. Where the open one welds
closed, it is taken now, and the slab takes 40.43 litres, closed; where it does not, what the cells glue into is taken
where it holds what the open one does, within the tolerance times its area, and the open one kept otherwise, which says
so to `Validate`, where a closed one with the wrong volume said nothing. Of the 79 864 parts of that model cut one after
another by the parts they meet within a thousandth, with a contact and a fallback of a hundredth, 17 have a cut skipped,
35 cuts, where 19 and 38 had, 87 cuts are taken within the fallback where 136 were, and none comes out more than a
litre from the same cuts within a hundredth, where two did.

**FIXED.** A union, a difference or an intersection of solids whose cut was not clean, a plane crossing a cell and
unable to cut it, took the cell as judged by one point where the other body lay one way to the cell's material on one
side of the plane and the other way to the rest: the check for that wanted a point on each side, and took for that the
middle of the cell's thickness under its largest triangles there, which can lie on the other side. A slab of a Tekla
model, less a tool whose sloping top a plane of the slab's could not cut, had 131 millimetres of the slab above that
plane and no such point found there, and the difference took the cell whole: 18 litres beyond the tool, closed and
valid. The middle is taken as far as the plane now. The difference takes 43.79 litres, what the two share, and the
common part of another slab and the tool beneath it, 44.20 litres where the two share 40.43, comes out 40.43.

**FIXED.** A boolean of solids with a `SolidBooleanOptions.Contact` put a face of the second body onto a plane of the
first that it lay on within the tolerance already, where the boolean took it as lying there anyway, and that could
change the answer: a tool's face 0.0001 off the plane of a slab's sloping face, a face out of flat that reads as
triangles each a few ten-thousandths off it, was put onto it exactly, and the plane, crossing the slab elsewhere, could
not cut it, so the difference took the whole of what lay along it: 61.88 litres from a slab of a Tekla model where the
two share 43.79, a result closed and valid. A face within the tolerance of such a plane is left where it is now. Of the
79 864 parts of that model each cut one after another by the parts it meets within a thousandth, with a contact of a
hundredth, two came out more than a litre short of the same cut within a hundredth; this is one of them.

## 11.0.0

**BREAKING.** The default tolerance is a thousandth of a millimetre for points and for flatness:
`Tolerance.DefaultEqualPoint` and `DefaultEqualPlanar` are 1E-3, and `DefaultEqualVector` is 1E-5, each 1E-2 before; the
angle stays one degree. `Tolerance.Default` and `Tolerance.Global`, which starts as it, are what every overload without a
tolerance reads. Use the default for results as exact as the corners of a model in millimetres are:
- Of 218 pairs of slabs from a Tekla model that share material, the common part came out within 450 cubic millimetres
  of the exact volume at the median, and within 2 882 for nine pairs in ten, where within a hundredth it came within
  1 084 and 11 861.
- The 79 864 parts of a Tekla model read closed, live from Tekla Structures. Of their 667 038 cuts by the parts they meet,
  9 came out open and none could not be worked out, where within a hundredth, of 703 236, 54 came out open and 63 could
  not be worked out; 24 and 17 took more than a minute each, and all of them 318 seconds on 24 threads, where within a
  hundredth they took 226. Cut one after another by the parts they meet, though, 122 of the parts came out open and 6
  could not be worked out, where within a hundredth 44 and 17 did: each cut leaves what is thinner than a hundredth and
  thicker than a thousandth for the next to cut.
- On a model of 1 989 parts, cutting each part by the parts it meets took 18 seconds, where within a hundredth it took
  12.5, none of the cuts open either way; cut one after another, 1 of the parts came out open, and none within a
  hundredth. Two IFC models of steelwork, 141 464 and 129 403 bodies, read closed, the second with 7 of its products
  warned of with their openings cut, where within a hundredth 12 were.
- A finer default is not more exact on such models: Tekla Structures gives faces a few ten-thousandths of a millimetre
  out of flat, and parts touching across gaps as thin, and below a thousandth those are cut as the slivers they then
  are. The 1 989 parts took 225 and 251 seconds within 1E-4 and 1E-5, 31 and 36 of their cuts more than a minute each.
- The vector threshold is far below the point threshold because it is also the area below which a loop is no polygon:
  with a thousandth for both, a triangle 0.005 by 0.08 of a millimetre that Tekla gives at the end of each of 1 791
  girders of that model was refused for its area of 0.0002, and every one of the girders read open.

What changes: two points 0.001 to 0.01 apart are two points, a face that far out of flat is no polygon and is read by
`GeoFace3.FromLoops` as triangles on its corners, a body whose faces meet on copies of an edge that far apart is open, and
a thin overlap of two parts that thick is material a boolean takes away or keeps, where it was taken for touching. A
tolerance passed in is unchanged, and a boolean within one still welds its result until it closes within the default
too, a thousandth now: of 10 314 cuts within 0.05 of the parts of a Tekla model, 23 took a different volume than with
the old default, the most by 2.5%. To have a hundredth back, pass `new Tolerance(0.01, 0.01,
Tolerance.DefaultEqualAngleRad, 0.01)` or set it as `Tolerance.Global`; net volumes to match the ones Tekla Structures
reports want 0.05, as before.
Geometry in metres wants `new Tolerance(1E-6, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-6)`.

**NEW.** `SolidBooleanOptions`, taken by new overloads of `TrySubtract`, `TryUnion` and `TryIntersect` of `Boolean3` and
of `GeoSolid3`, says how a boolean of two solids is worked out: its `Tolerance`; a `Contact`, how far apart a face of the
second body may lie from a face of the first, parallel to it, and still be taken as lying against it; and a `Fallback`
tolerance, within which a boolean whose result `Validate` finds not valid is worked out again. Each face of the second
body so near a face of the first is put onto that face's plane before the boolean, and the first body is not moved.
Tekla Structures draws parts against each other a few thousandths of a millimetre into or off each other, and cut within
a thousandth, a part whose cutter's face stood 0.0049 inside its own kept a skin of itself that thick, 110 square metres
of surface on a part of 119. And `TrySubtractAll` takes bodies out of a solid one after another, each cut checked by
`Validate`, the fallback tried where it is not valid and the cut skipped where that is not either, and says in a
`SubtractReport` which: the net body of a part less every part it meets. Of the 79 864 parts of a Tekla model so cut
within a thousandth, 180 had a cut skipped, 323 cuts, and 3 344 came out with more than 1 % more surface than within a
hundredth, 5 466 square metres in all; with a contact of a hundredth, 109 and 196, and 203 and 220 square metres; with a
fallback of a hundredth as well, 19 and 38. Within a contact of a hundredth the volume is as exact as within a thousandth
but where parts touch: of 206 slabs against their exact net volumes, the errors summed 357 595 cubic millimetres, where
within a thousandth alone they summed 207 323 and within a hundredth 757 750. The overloads taking a `Tolerance` are
unchanged.

**FIXED.** A difference or a union could come back closed with faces wound against each other, which `IsClosed` reads as
closed: a beam from an IFC model less an opening whose side lay against the web within the tolerance, cut within a
thousandth, kept the web twice over a strip 6.6 wide, its two copies facing the same way, and the next opening cut from it
came out open, its common part with the beam as nothing. Such a result is cut the other way round too, and the other is
taken where it closes wound alike; the beam takes its sixteen openings so. Where the other way does no better, the result
is kept as it was: on the parts of a Tekla model most such results come of slivers and hold their volume.

**FIXED.** An intersection cut cleanly neither way took the cells of the first body cut even where cells the planes could
not cut had been judged apart across them, and the other way round they had not: of two bars bent twice and crossing,
within a thousandth, the common part lost an eighth. The other way is taken then, as a difference takes it.

**FIXED.** A face out of flat, split into triangles on its corners by `GeoFace3.FromLoops`, the booleans and the meshes,
could come with a triangle standing up across it: an L with its inner corner a millimetre up, read within a thousandth,
took the ear across its diagonal that the inner corner, seen from the face's frame, passed 0.005 outside, and closed with a
triangle along the diagonal facing sideways. Where a triangle stands up, more than 60° off the face's frame, the face is
split again with every ear blocked ten times wider each time, up to how far it is out of flat, and the first split with
none standing up is taken; with none, the first. Blocking that wide on every face out of flat turned the split away from
good ears onto the sliver along a straight run with a corner lifted on it, which stood up instead, and changed what chained
cuts of a Tekla slab were left with within a hundredth until one of them could not be worked out.

**FIXED.** A union or a difference of solids that came out open with one body cut cleanly went on to cut both bodies by
every plane of both, without cutting the other body first where that took more planes: a slab of a Tekla model cut by the
49 planes of another slab it meets came out open, and cut by the 305 planes of both, into 50 033 cells, open again, in 12
seconds, where the other slab cut by the 234 planes of the first, into 3 833 cells, closes in 2. The other way round is
cut now whatever it costs where the first came out open. And where neither body cuts cleanly, of the two cuts whose cells
are judged alike on both sides of the planes that could not cut them only the first was taken: a slab less another slab it
meets, within a thousandth, came out open by 22 edges, where the second closes; the second is taken now where the first
does not close. Within a thousandth, of the 1 989 parts of a Tekla model cut one after another by the parts they meet, 4
come out open where 6 did; of 667 038 cuts of a selection of 79 864 parts, 28 where 33 did, and none is not worked out
where 1 was; within a hundredth, as many as before.

**FIXED.** A boolean of solids that came out open by copies of an edge a little further apart than its tolerance stayed
open: a slab of a Tekla model, cut one after another within a thousandth by the parts it meets, had a face stepped by
0.00107 where two faces lying back to back across the step were taken from each other, and the faces either side met on
copies of an edge 0.00107 apart, which the weld within the tolerance could not make one. Where that weld does not close
what a boolean gives, the corners of the edges still open, and those only, are welded within twice the tolerance, and
then four times, and the faces built again within the tolerance, so that one a corner moved takes off flat comes as
triangles; what comes of it is kept only where it closes within the tolerance and holds its volume within the wider
reach times its area. Within a thousandth, of the 1 989 parts of that model cut one after another by the parts they
meet, 1 comes out open where 4 did, and none of their 10 198 cuts pair by pair where 1 did; of 667 038 cuts of a
selection of 79 864 parts, 10 where 28 did; within a hundredth, of 703 236, 54 where 93 did. A result closed so can lie
further from the exact one than the open one did, as far as the reach times its area allows: the warped tops of girders
less the girders over them, within a hundredth, took within 3.2 % of what the two share, where open they took within
1.6 %.

**CHANGED.** `ToCells` keeps the point of a needle a cut cannot be made through with the rest of its piece, without a
warning, as far as a thousand point tolerances past the cut, where it was a hundred: a needle tapering at one in five
hundred is thinner than the tolerance for five hundred tolerances of its point.

**FIXED.** `GeoFace3.Flip()` and `Clone()` checked a face's holes against the plane of its boundary again, within the
global tolerance: a face made within a wider tolerance, a hole 0.03 off the plane within 0.05, could not be turned over
or copied, and `GeoSolid3.TurnOutwards()` threw on a body with such a face. Neither changes what was checked when the
face was made, so neither checks it again, as `GeoPolygon3.Flip()` and `Clone()` do not.

**FIXED.** A union, a difference or an intersection left a body open where a face of the one crossed a face of the other
at a slant, a few hundredths apart at the thin end: a cell thinner than the tolerance there lay against its neighbour,
in its own plane, and against its own far side, and the gluing took what it shared from each, so that its face took the
wall with it as well as the face lying against it. A slab of a Tekla model sitting in the opening of another, the
opening's wall crossing the slab's at 5E-5 rad, came out without 750 by 400 of its wall, and read from the faces left
2.9 cubic metres short. What a face shares goes with the nearest face lying against it now, and a face lying against two
that overlap is paired with the nearer only. The slab closes, 99 543 cubic millimetres taken of the wedge of 139 388 the
two share, and every other result of the 20 596 cuts of that model's parts, of 864 pairs of slabs and of 3 000 random
pairs came out as before.

**FIXED.** A union, a difference or an intersection cut within a tolerance wider than the default came out closed within
it and open within the default, where a plane of one body passed within the tolerance of a face of the other: the pieces
cut along the plane met the face's own on copies of their edges a few hundredths apart. Of 10 314 cuts within 0.05 of
the parts of a Tekla model by the parts they meet, 14 came out open within 0.01 so, by copies 0.010 to 0.048 apart, and
so did 13 of its parts cut by all they meet one after another. The corners of a result open within the default, or
within its own tolerance where that is the finer, are made one now where they stand within its tolerance of each other,
each group at the corner of it that moves the volume least, and a corner within the tolerance of another face's edge is
put on that edge; what comes of it is kept where it closes within the default and within its own tolerance, and holds
its volume within the tolerance times its area. The 14 and the 13 close within 0.01, and set beside the same cuts within
a thousandth the 14 came out 41% nearer in all, 8 of them nearer and 6 further. Two more of the parts cut one after
another, open within their own tolerance where two faces had a bend at two places, 18.28 and 0.021 apart, close too. A
result closed within the default is left as it is.

**NEW.** `GeoSolid3.Validate()` says where a body's faces do not close, and why, as a `SolidValidation3`: whether the
faces bound a body, closed, wound alike and outwards, and each `SolidIssue3` found, with its `SolidIssueKind`, where it
is, how large and the faces it is of. A stretch of edge an odd number of faces meet on is an `OpenEdge`; one the faces
on it run the same way is a `SameWayEdge`, a face wound the wrong way round, which `IsClosed` counts as closed; faces
wound inwards are `InsideOut`, and faces enclosing nothing thicker than the tolerance `NoVolume`. Edges four faces meet
on, short edges, faces thinner than the tolerance and faces out of flat are noted, and leave the body valid. The edges
are matched as `IsClosed` matches them, and the two agreed on all 1 989 parts of a Tekla model and on the 10 313 cuts of
them within 0.05 by the parts they meet, within 0.05 and within 0.01.

**CHANGED.** A union, a difference or an intersection whose result holds a volume the operation cannot give is not
handed back: a difference leaving more than the body it was taken from, or less than that less the whole tool; a union
holding less than the larger body or more than both; a common part larger than the smaller body, each beyond the
tolerance times the area it can move across, the tool's for a difference and the smaller body's otherwise. Found cutting
one body, such a result is worked out by cutting both, or the other, instead; found every way, it is reported as not
worked out, with a warning, and `BooleanOutcome.NotWorkedOut`. A slab from a model less a wall that only touched it came
back once 1 172 944 cubic millimetres larger than it was, where the wall's area allows 188 650.

**NEW.** `TrySubtract`, `TryIntersect` and `TryUnion`, of `Boolean3` and of `GeoSolid3`, each with an overload giving a
`BooleanOutcome`: `Made`, `Empty` or `NotWorkedOut`. `false` comes back both where nothing is left and where the answer
could not be worked out, which only the log told apart, and a net volume worked out by taking cutters off a part one
after another reads the two the opposite way: nothing left takes the part to nought, where no answer is a step to work
out some other way. A union that glues into no body is warned of now as not worked out, as its documentation said it
was.

**FIXED.** The union, the difference, the intersection and the cutting of openings glued a result open where they take
from two faces lying back to back the area the two share. Worked out in the plane, a piece of either face no wider on
average than the point tolerance was dropped as a seam, a corner of the end of an I 0.0006 square millimetres across
among them, every side of it longer than the tolerance, while the faces beside it kept their edges round it; and two
edges within the tolerance of each other left a needle between them on the piece kept, 5.9 long beside the I's flange.
The union of the I and a prism of twelve sides passing the corner of its flange was open by both. Each such sliver, no
wider than four point tolerances, is closed by a face across it now, where the faces were closed before the cancelling
and that closes them again. And merging the faces of a plane joins their outlines within the tolerance, where corners a
hair apart are one point to the merged face and two to the faces beside it: the common part of two bent bars crossing,
closed before the merge, came out open by edges a hundredth long. A merge that opens a closed result is not made, and
the result keeps the faces it was glued from, where they are no more than four times the faces merged, or 2 048: a slab
with holes less another, cut into cells, left 42 590 faces where the merge gave 270, and every question asked of it
after, and every cut, took seconds.

**FIXED.** `TrySubtract` cut the body taken away wherever fewer planes of the other reached it than reached the body
material is taken from, and kept that body whole less the cells within it: its faces less those of the cells lying
against them, worked out in the plane, mixed the corners of the two where they lay within the tolerance of each other.
Of two walls from a model, the second over the last 1.8 m of the first and 0.014 to the side of it, which a tolerance of
0.05 takes for nothing, the first's new end took the second's sides, its sides kept their own, and its floor kept a
needle 0.014 wide reaching back past the end: the rest was closed within 0.05 only, and fell 255 000 cubic millimetres
short of the wall left. A difference cuts the body material is taken from now where that costs no more than twice the
planes of cutting the other and sixteen more, and the rest is the box it should be, closed within 0.01. Of 864 pairs of
slabs from a Tekla model, two differences come 317 and 79 cubic millimetres nearer what the two share, and none further.

**FIXED.** A union or a difference neither body of which could be cut cleanly, a plane crossing a cell of each and
leaving it whole, cut both bodies by every plane of both, and that could go wrong. Of a slab from a Tekla model whose
outline ran down to a needle, its tip 0.0136 past the face of a wall the slab otherwise stood clear of, the slab less
the wall came out open and 1 172 944 cubic millimetres larger than it was; two other pairs of the model's slabs grew by
118 887 and, at a tolerance of 0.05, 890 199. A cut whose cells are judged alike on both sides of every plane that
crossed one and could not cut it is taken now where neither body cuts cleanly, as a clean one is: the slab comes back as
it was, as the wall less the slab did, the second pair as well, and the third takes 99 336 away.

## 10.0.0

**BREAKING.** A body is measured as its material. `GeoSolid3.GetVolume()`, `GetSurfaceArea()` and `GetCentroid()`, each
also within a tolerance, give the volume, area and centre of the faces with every opening cut out, the walls of the
openings part of the surface, and `TryGetVolume` says whether the volume can be trusted: every opening cut out and the
material closed. The material is cut once for a tolerance and kept by the body, so measuring it every way,
`GetMassProperties` and `Measure3` with the rest, costs one cut and describes one body. The openings are cut at once,
and where that cannot be worked out or opens a body that closed, one at a time by a difference; one that will not come
out is left in and warned of. A body without openings is measured by its faces at once and nothing is cut: every part
GeometryHelper.TeklaConvert reads is one, its cuts already in its faces, and a slab cut by five other parts matched
Tekla's VOLUME_NET to 5E-12 and its AREA to 5E-12.

Removed, as they read the faces alone and so gave a body with openings the faces' volume, area and centre, duct and all,
as if they were its own: `GeoSolid3.Volume`, `SurfaceArea` and `Centroid`; `NetVolume`, which took each opening's whole
volume off, so that an opening drawn past the body, as a through hole is, took off what is not there and two that
overlap took their overlap off twice, a plate with a hole drawn a millimetre past each face measuring 191 200 for
192 000; and `GetNetVolume()`, which took the openings out one difference at a time and one it could not take out off
whole.
`GetSignedVolume()` is internal: what it was public for, which way a body is wound, `TurnOutwards()` sets right, giving
the body wound outwards, its openings too, and the same body back where nothing had to be turned. `GeoSolid3.ToString`
calls the faces' volume of a body with openings its gross volume.

**NEW.** `GeoTriangle2`, the triangle of the plane, as `GeoTriangle3` is the triangle of space: its signed area and
winding, perimeter, centroid, angles, circumcircle and incircle, barycentric coordinates, where a point is, moves and
transforms, and its way into space and back (`ToTriangle3`, `GeoTriangle3.ProjectToTriangle2`, `PlanarMap`). It
stands in the matrix of the plane as `GeoRectangle2` does: it collides with, measures the distance to, crosses and finds
the shortest segment to every other shape of the plane and to another triangle, holds points, segments and polylines,
gives its closest edge and the signed distance of a point, runs parallel to a segment and is walked along its edges;
the eleven other shapes ask the same of it, a segment extends and trims to it, and the new `Triangle2` class of
`GeometryHelper.Core` answers all of it. A triangle answers as the polygon of its corners does, and one whose corners
stand on each other as the segment it is.

**NEW.** `TriangulateSurface` on `GeoPolygon2`, `GeoFace2`, `GeoPolygonArc2`, `GeoRectangle2` and `GeoCircle2`
breaks the shape into `GeoTriangle2`s that each lie within its material, holes left open, all running
counter-clockwise. The plane meshes as `GeoFace3.TriangulateSurface` meshes a face of space, on the shape's own corners
while its rings stand apart and cut into strips where they touch; a loop with arcs is flattened by a chord tolerance
first, a disc is fanned from its center, and a polygon crossing itself is covered where `MakeValid` says it covers.

**NEW.** `GeometryHelper.Meshing`. `ToMesh` on `GeoPolygon2`, `GeoFace2`, `GeoPolygonArc2`, `GeoRectangle2` and
`GeoCircle2` breaks the shape into a `GeoMesh2`, whose faces are simple polygons with no hole, counter-clockwise, sharing
their corners and meeting edge to edge, of the kind `MeshKind` names:
- `Triangles`, on the shape's own corners, as `TriangulateSurface` gives them;
- `Grid`, the cells of a regular grid, as panels of formwork or tiles are laid: whole where the material holds them, cut
  to the boundary and the holes where they cross them, and split through the middle of a hole one holds whole. The cells
  stand a joint apart, with none at the boundary; along each axis the grid starts at the shape's first side or ends at
  its far one, or puts a cell or a joint on its middle, or has a cell start at an origin; it turns by an angle, and runs
  along a rectangle's own sides. `IsWhole` tells the whole cells from the cut ones;
- `Strips`, trapezoids along a direction, cut at every corner;
- `Convex`, convex pieces, few of them: the triangles merged as Hertel and Mehlhorn merge them, no more than four times
  as many as the fewest there could be.

`MeshOptions` holds the kind and the grid's settings, `Mesh2` does the work, and `GeoMesh2` gives the vertices, the
faces as indices and as polygons, the area, the edges, the boundary, a face's neighbours and the triangles of the faces,
and moves and transforms. `GeoRectangle2.Divide(columns, rows)` divides a rectangle into equal rectangles. 300 000
random faces with holes touching each other and the boundary, meshed every way, are held to all of it.

**NEW.** Meshing in space. `ToMesh` on `GeoPolygon3`, `GeoFace3`, `GeoPolygonArc3`, `GeoCircle3` and `GeoTriangle3`
breaks the shape into a `GeoMesh3`, with the `MeshOptions` of the plane: the shape is laid out in a frame of its plane,
meshed there as the plane meshes it, and put back, its own corners exactly where it has them. A face a hair out of flat
keeps its corners, the points on its sides stay on them, and only the points inside lie on the plane. `GeoMesh3` is a
`GeoMesh2` standing in a plane: its faces run counter-clockwise about `Normal`, `Frame` is the plane they were laid out
in, its X axis the grid's first axis, and `ToMesh2` draws them in it, as an elevation. A `MeshPlacement3` says which way
the grid runs, `MeshAxes`: level with the second axis up the slope, along X on a level face (`World`, the default); along
the long side of the smallest rectangle round the shape (`Own`); `Upright`; along a direction; along a coordinate
system's axes; and `At(origin)` puts a cell's corner on a point of space, so that walls laid from one origin have their
rows at the same heights. In space a grid's origin is a point of space, and `MeshOptions.Origin` is refused there.

**NEW.** Bodies cut into cells. `ToCells` on `GeoSolid3`, `GeoObb3` and `GeoAabb3` cuts the body into a `GeoCellGrid3`
of `GeoCell3`s: each cell a closed body of what the body holds of it, with its indexes along the grid's three axes, the
whole cell as a box, whether it is whole, and its volume; a cell the body holds in several pieces, as across the notch of a
U, gives a cell a piece. The grid gives its frame, the counts along each axis, the cells at an index and the box at any,
and the neighbours that share part of a face. `CellOptions3` divides each axis as its `CellAxis` says, by a size standing
as a `GridAlignment` says or from the placement's origin, by a count, or not at all, with a joint between neighbouring
cells and none at the boundary: `Grid`, `Layers` and `Divide` give the usual ones. The axes come from `MeshPlacement3`:
the world's, a box's own or the smallest box round a body (`Own`, the default for `GeoObb3`), upright along the long side
of its plan, along a direction, or a coordinate system's. A cut coming within the snap distance of a corner of the part it
cuts moves onto it, and none takes off a slice thinner than that, or than four point tolerances, from a side of the part,
so that the slice goes with the cell beside; a part that thin both ways goes whole to the side holding more of it. Past a
joint, where it would go with the joint, a slice thicker than the point tolerance is the cell beyond's. The neighbours
are found where the cells' material meets. The openings are cut in first, or into each cell they meet where the whole
body will not take them or is left open by them, a body that is not closed is refused, and one wound inwards, as a mirror
leaves it, is read the right way out. A box cut along its own sides is cut by arithmetic, a million cells in about two
thirds of a second; any other body is cut by planes, along X, then each slab along Y and each bar along Z, on as many
threads as allowed. The cells hold the body's volume but for the tolerance times the area cut: over about 150 000 random
bodies cut without joints, a median of two parts in ten million million. The OBJ writer adds a `GeoMesh3`, a face that
turns right at no corner as one polygon, and a `GeoCellGrid3`, each cell an object of its own. The guide `docs/mesh3.md`
describes it all, and `docs/mesh3-report.md` draws and measures its cases and how they were checked.

**NEW.** `Measure3` measures a body by the method asked, and every method side by side: the `Volume`, `Mass`,
`Centroid`, `MassProperties` and `SurfaceArea` of its material, openings cut in, the material the body's own `GetVolume`
measures. `VolumeMethod` says how the faces are read: `Fan`, the fan of each face's boundary from its first corner, as
`GeoSolid3.GetVolume` sums it; `Surface`, the triangles lying in each face, as `GetMassProperties` sums them; and
`FlatFaces`, each face laid onto the plane square to its area through the middle of its corners, as Newell's method fits
one, which rests on no triangulation. `AreaMethod` says how an area is: `Faces`, each outline's read flat, or `Surface`,
the triangles'. Over flat faces every method gives the same but for the rounding. Over a face a hair out of flat they
part, as such a face is no one surface: one of four corners with a corner lifted holds a third of the lift times its
area more split along one diagonal, a sixth along the other, and a quarter read flat. `Measure3.Compare` gives a
`MeasureComparison3`: each method's volume, centroid and area, how far apart they come, and how far the faces fall short
of closing, as the volume moves measured from each corner of the body's box. `MassProperties3.Method` says which reading
gave the properties.

**FIXED.** A body wound inwards came out of every operation that reads the winding wrong. `TryCutOpenings` cut its cells
with its own faces facing in and the cuts' faces out, and a 100 by 100 by 20 plate with a hole 20 square through it came
back whole, 200 000 where 192 000 is left. The booleans took a tool so wound for material: the plate less a duct wound
inwards held 202 667 for 192 000, the two together 193 867 for 200 800 and their common part 2 667 for 8 000, and two
bodies apart, one of them wound inwards, joined into nothing. `TrySplitBy` cut the plate 20 thick at 5 into 50 000 and
16 667 for 150 000 and 50 000; a block standing on a plate, one of the two wound inwards, did not lie against it by
`TryGetContact`; and the clash of the plate and the duct held a third of its volume. Each turns its bodies outwards
first, as the meshing did, and gives back bodies wound outwards; a prepared body is turned once, and `TurnOutwards`
keeps its answer with the body, so the clash check runs as fast as before.

**FIXED.** `GeoFace3.Locate` and `Contains`, and `GeoPoint3.LocateIn` a face, read each hole in the hole's own plane.
A hole may stand off the boundary's plane by up to the planar tolerance, as one a modeller cut can, and a point on the
face's plane a thousandth below stood further than the tolerance from the hole's: a point in the hole was inside the
material, one on its rim inside too. So did `GeoFace3.DistanceTo` and `GetClosestPointOnBoundary`, which gave a point in
the hole as its own nearest, at no distance, and with them `GeoSolid3.Locate` and `Contains`, which took a point in a
hole through a plate at the level of its top for one on the plate's side, and `DistanceTo` and
`GetClosestPointOnBoundary`. The holes are read in the plane of the boundary now.

**FIXED.** `GeoSolid3.TrySplitBy` refused a plane that parts a body without crossing it: one passing between two blocks
of one body, or along the edge where two parts of a body meet, as a body cut through the corner of its notch touches
itself. Each half is closed as it is, and was taken for one the plane misses. It is split now, with nothing to cap. A
plane crossing a body where it is thinner than the point tolerance is still refused, as before: the two sides of the
section stand within the tolerance of each other and their edges cancel as one edge run both ways would, but they are
two edges, and the halves would be open by the sliver between them. A sliver no larger than a square of the point
tolerance, as a corner of the body the plane passes through leaves, is the size of the tolerance and taken for none.

**FIXED.** `GeoSolid3.Section` gave, with the section, the face a plane lies along where it also cuts the body, and only
when the plane faced away from the material there: along the floor of an L's notch, facing up it gave the floor with the
arm it cuts, facing down the arm alone; between two blocks standing on each other it gave all of the lower block's top.
It gives where the body has material on both sides of the plane now, as it says, whichever way the plane faces.

**FIXED.** `Merge3.CoplanarFaces` threw where faces the tolerance takes for one plane, one of them turned a hair against
the others, leave an outline that is not flat: a hole of it stood further than the tolerance off the plane of the loop
round it. The booleans and `TryCutOpenings` glue their cells with it, and reported such a cut as not made, with a
warning: a sliver of an opening turned a thousandth of a radian, flush with the floor and a side of a body, was not cut
in. Such faces are left as they were, unmerged.

**FIXED.** `GeoFace3.TriangulateSurface` and `GeoSolid3.Triangulate`, asked within a tolerance finer than the global one,
threw on a face thinner than the global point tolerance whose corners the clipping could not reduce: the strips they fall
back to laid the face out checked against the global tolerance, which took its two long sides for one. A strip 300 long
and 0.006 wide is covered within a tolerance of a ten-thousandth now.

**FIXED.** `GeoSolid3.TrySplitBy` and `GeoFace3.TrySplitBy` refused a cut through a face a hair out of flat where a piece of
the face, measured from a corner of its own and about its own normal, stood further out of flat than the planar
tolerance: from a corner a hair one way, one a hair the other stands off by twice it. The piece was dropped, the half it
belonged to could not close, and a piece of a turned column with a ledge, 0.0146 out of flat so measured, was not cut. A
piece can be no further out of flat than the face it is cut from, and is built within three times the planar tolerance.

**FIXED.** `GeoSolid3.TrySplitBy`, `GeoSolid3.Section` and the cells cut from a body refused a plane through a corner of
the body and within the planar tolerance of another: the cap closing each half was built from its corners as any polygon
is, measured from a corner of its own about the normal of its corners, and a corner a hair off the plane turns that
normal. A column with a ledge 0.109 thick, cut through a corner of the ledge's tip and 0.0084 from the next, had a cap
whose far corners stood 0.058 off so measured; it was refused, and the cells kept the piece whole in the cell above,
reaching 39 mm out of it. A cap lies in the cutting plane, every corner within the planar tolerance of it, and is read in
it now, facing along its normal.

**FIXED.** `GeoSolid3.TrySplitBy`, `GeoSolid3.Section` and the cells cut from a body refused a plane crossing at a slant an
edge two faces share within the tolerance, its ends a few thousandths apart on the two, as the booleans leave them: each
face crossed the plane on its own copy of the edge, and the crossings stood further apart than the copies' ends, 0.0112
for ends 0.0045 apart in height above a plane meeting the edge at 22 degrees, so the rim of the cut did not close. A cell
of a box with a box taken out kept such a piece whole, 56 mm out of its box. The gap is closed across now, where each end
the rim leaves open and the start nearest it are each other's nearest within four point tolerances. The two crossings
are not put on one point: that took the two sides of a tube's wall thinner than the tolerance for one, and cut it.

**FIXED.** `GeoSolid3.TrySplitBy` and the cells left a half open beside a rim closed across that gap. Where a face of
the body runs between two cuts a hair apart, the strip of it between them narrows to nothing, and cut across where it is
narrower than the point tolerance it was taken for no width: its piece kept one corner of the narrow end and the cap the
other, and the faces beside the strip shared their edge on copies 0.0095 apart at its foot. Cut across again, the
strip's two crossings 0.0036 apart were one point, on the far copy; the rim was closed across the 0.0119 to the other,
and the sliver between the copies, 102.8 long, was left open above it. A cell of a thin L came out so, one in ten
thousand random thin bodies. Where a bridged rim leaves its half open, a face across each sliver no wider than four
point tolerances now closes it, and a half closed as it is is left so.

**FIXED.** The booleans took two planes for one where they ran through one point with normals the tolerance lets pass:
of a slab wrapping the corner of another, its face running from the corner along the other's long side, turned 0.32
milliradians from it, stood 5.8 off it 18 m on and was never cut along. The slab less the other, worked out by cutting
it by the planes of both, took nothing of the 15.79 million cubic millimetres the two share. Two planes are one now only
where every corner of the box they cut stands as far from the one as from the other, within the planar tolerance; so for
the planes of a body's faces and of its openings.

**FIXED.** `TryIntersect` and the booleans kept a cell a plane crossed and could not cut whole, judged by one point of
it, where the other body lay the other way to its other side: the slab wrapping the other's corner, cut by the other's
planes, held the tip of the wedge the two share and the part of the slab beyond the other's end in one cell joined at
the corner, which the other's end could not cut, and the common part lost 6.4 m of the wedge, 1.97 million of its 15.79
million cubic millimetres. Where a point of such a cell on each side of the plane is judged apart, the other body is cut
instead, whatever that costs.

**FIXED.** `GeoSolid3.Locate` and `Contains` threw another ray when a crossing landed within twice the point tolerance of
a face's outer rim, but not of the rim of one of its holes: a ray leaving through the wall of a hole and rising through
the hole a hair short of its rim was counted by the face the hole is cut in, and the point came out of the body. The
hole's rim is measured in the plane of the face, as the face holds a crossing against it, so that a hole standing off
the face's plane within the planar tolerance is measured as one in it.

**FIXED.** `GeoSolid3.Locate` and `Contains` called a point of a body thinner than four point tolerances outside, near one
of its sides: every ray from it crossed a face within twice the tolerance of a rim, where a crossing is not trusted, the
side itself wherever a ray reached it, and a point no ray could place was reported outside. A point of a cell of a plate
0.035 thick, 0.013 in from its side and 0.0108 below its top, lay in no cell. Where every ray is refused, the point is
placed by the turns the faces make about it, the solid angle they subtend at it, which no edge makes ambiguous; a boundary
that turns no whole number of times, as one open somewhere can, still claims no point.

**FIXED.** `GeoFace3.TrySplitBy` and `GeoSolid3.TrySplitBy` gave a hole back as a face of material when every corner of
it stood on the rim of the piece it fell in, as a hole touching its face's boundary at two corners does when the cut runs
through its third: the piece covered the hole twice, and a body gained its volume.

**FIXED.** `ConvexHull3.TryOf` and `Of`, and `GeoObb3.Fit`, threw on points whose hull has a face smaller than a polygon
of the tolerance may enclose, as three corners a tenth of a millimetre apart that a boolean leaves make. The hull's faces
are built from their corners as they are now.

**FIXED.** `GeoPolygon2.SignedArea`, `Area`, `IsClockwise` and `Centroid`, and `GeoPolygonArc2.SignedArea` and `Area`,
summed their shoelace from the origin, where the products of coordinates seven kilometres out are near 5E13 and their
last digit is worth a hundredth. A polygon a tenth of a millimetre across there came out with no area at all, or the
wrong winding, a strip of 0.9 mm2 as 0.8984375, and the centroid of a triangle 0.3 mm across 110 km off. They are taken
from the first vertex now, as the planar internals already took theirs, and `GeoFace2.Area` with them.

**FIXED.** `GeoSolid3.GetMassProperties` of a body its openings take whole gave the mass of its faces, as if it had no
openings: a plate inside an opening larger than it every way weighed what the plate does. Nothing is left, and it gives
nothing. Openings that cannot be cut in are still left out, now with a warning in `GeometryHelperLog`. The volume is the
one the tetrahedra give, read the three ways of the divergence theorem together, rather than the one way Eberly's sums
read it: the same where the faces close, but for the rounding.

**FIXED.** Where Clipper2's sweep fails, the booleans, offsets and meshes of the plane get no region, and a warning in
`GeometryHelperLog` says so; they went on before with the part built until the sweep failed, which the engine reported
as an answer. No case has been seen to fail: 16 million random clips, open paths and coordinates up to 2E18 among them,
never made the sweep fail.

**CHANGED.** Separating a body into its pieces, as `GeoSolid3.SplitShells`, `Boolean3.SplitShells`, the booleans and the
cells do, and `GeoSolid3.IsClosed` where edges do not meet end to end, pair the edges by a sweep along the axis that
leaves the fewest pairs to try, instead of along X. Along X every edge of a body thin along it was tried against every
other, and every long edge of a body long along it: a disc a millimetre thick and a drum ten metres long, of 4 096 sides
each, took eleven seconds each and take four hundredths, and a drum cut into 800 slabs across its length took 42 s and
takes 3.7 s. The pieces found are the same. So, for the same reason, do cutting a body by a plane and merging its faces
where they match the edges of a rim: every end of the rim of a cap square to X fell in the run of every edge, and the
cut of a drum of 16 384 sides took 1.1 s square to X, against 0.18 s square to Y. It takes 0.18 s either way, with the
same edges.

**CHANGED.** The package depends on no other package. The clipping engine of Clipper2 2.0.0, which resolves the
regions of the plane, is compiled in from its source, `src/GeometryHelper/Internal/Clipper`, instead of referenced as the
Clipper2 package, so that it can be mended here. Only what GeometryHelper and its tests call is kept: the core, the
engine, the static functions, the pools, and the offset, which the tests hold the library's own offsets against;
rectangle clipping, Minkowski sums, the triangulation and the borrowed `System.HashCode` are left out, and the tests
reference no Clipper2 package either. Its types are internal: they are not part of GeometryHelper, and a project that
references the Clipper2 package as well sees that one only. A project that used Clipper2 through GeometryHelper's
dependency references the Clipper2 package itself now. Built into GeometryHelper as it came, the engine gave the IL of
the package, but for the hash codes of its points, which nothing reads, and one delegate the newer compiler keeps
instead of making it on each call, so every answer was the same. Faults found in it since are mended here, each held by
a test in `TestFixedHere` that failed before; they change no answer GeometryHelper gives, but for a region whose sweep
fails, above. Its source is embedded in the DLL's symbols with the rest, so the package carries
`THIRD-PARTY-NOTICES.md`, its copyright and the Boost licence, as that licence asks. Clipper2's own tests run with the
suite; test 16 of its polygons, which Clipper2 2.0.0 fails upstream as well (#1067), is skipped.
Upstream's later change for it is not taken: it mends that case and puts the union of loops crossing themselves that
`TestCasesFoundHere` holds 1.2 % over its area, while it changed none of 40 000 random cases at four and six decimals.

## 9.0.2

**FIXED.** `GeoFace3.TriangulateSurface`, which `GeoSolid3.Triangulate` and `TriangulateSurface` and everything meshing
a body build on, handed a face back as the fan of its boundary, laid across every hole, whenever ear clipping gave up on
it, and the clipping gave up on faces it should have met. A hole in a row whose edges lie a hair out of line, as the pits
of slabs drawn in Tekla Structures stand 4.66E-8 mm out, was bridged to the far corner of the next hole, along that
hole's edge, and the loop then touched itself: a slab's bottom face of 1 836 m2 was meshed as 5 391 m2, and four of 217
slabs were meshed with their pits covered. Holes a rib of two millimetres or less apart were not got round either, a
corner counting as on the edge of an ear within the point tolerance times the width of the whole face: a tenth of a
millimetre from an edge 100 mm long, on a plate a metre across. A hole is bridged to the nearest corner in the way now,
and a corner blocks an ear within the point tolerance of one of its edges while another ear can be found, within
rounding when none can. The mass properties of such a slab put its centroid up to 14.5 mm off the middle of its
thickness, its volume right only because the faces in question lay level, and the clash check, `GeoBvh3`, distances,
rays, collisions, projections and the OBJ export met material across the holes. `Volume`, `Centroid` and `SurfaceArea`
do not mesh faces and were right, as were the booleans.

**FIXED.** A face whose holes touch each other or its boundary could be meshed wrong and handed back as right. Ear
clipping joins every hole into one loop with the boundary, and where two rings touch, the loop doubles back on itself:
an L whose hole stood against the side of its notch was meshed a third larger than it is, across the notch, and
triangular holes meeting at their corners had a triangle laid across one of them. A face whose rings come within the
point tolerance of each other, or that ear clipping cannot reduce, is cut into strips at its corners instead. Its
material is read as the booleans read it, holes that touch or overlap taken together and one reaching past the boundary
taking away only what it covers, and the triangles keep the face's own corners and meet its edges where the strip lines
cross them. No face is meshed as a fan any more. Of 5 750 random faces with holes in rows a hair out of line, ribs down
to 0.05 mm, holes against each other and against concave boundaries, corners up to 1E-5 off their plane and far from the
origin, 9.0.1 meshed 4 766 wrong; none is now.

## 9.0.1

**FIXED.** `TrySubtract` took material from a body the other only touched, and gave some bodies back larger than they
were, where two lie within a few hundredths of a millimetre of each other: slabs drawn side by side in a Tekla
Structures model, whose shared faces stand a thousandth to a few hundredths apart and turned a fraction of a
microradian. Of 207 concrete slabs whose net volume was taken by subtracting the slabs they meet, nine came out
between 35 and 114 696 mm3 off Tekla's, where `TryIntersect` found the two sharing nothing, and what a difference took
was not what the two had in common, seven times over at worst. With the ten more slabs they meet, the 217 make 864
pairs whose boxes meet: 240 broke V(A) − V(A − B) = V(A ∩ B) by more than a cubic millimetre, 231 took volume from a
pair sharing none, 104 came out larger; now 850 touch only and come back as they were, none takes anything from a
pair sharing nothing, none comes out larger, and 8 break it, by at most 8 676 mm3 against an overlap of 31.5
million, the glue of a difference still closing within the tolerance rather than exactly. A difference that finds nothing of the one body within the other now
hands the first back as it came, as it does a body too far away to meet, rather than gluing it back from the cells the
other's planes cut it into. Slabs a few hundredths into each other do share that much, and a difference takes it;
Tekla Structures' own cuts take nothing for such an overlap, and the booleans within a tolerance of 0.05 take nothing
for it either: of the 217 slabs, two pairs then share anything.

**FIXED.** A cell thinner than twice the point tolerance has no point further than that from its skin, and was taken
for empty: a plane of the tool 0.0165 inside a face of the body cut a sliver off its whole length, and the difference
took the sliver away with the tool, 38 000 mm3 of a slab beyond the other. Such a cell is placed by the middle of
its thickness, and is material; one that thin within the other body counts as touching it.

**FIXED.** A piece of a cut was turned over when it stood more than the angle tolerance off the way it was to face. A
cap three hundredths across with a corner on the plane only within the tolerance stands degrees off it, and the half it
closed had its cap facing into it, and a volume that depended on where it was measured from. Only a piece facing the
other way is turned now.

**FIXED.** Cutting a face along an edge of it lying within the tolerance of the plane, its two pieces stepped across
that edge from different ends, which is the same line only while the edge lies exactly in the plane: a slab's face cut
seventy metres along by a plane within a thousandth of its notch edge lost a sliver of 32 mm2 between its pieces, and
the two halves of the slab ten thousand cubic millimetres. Both pieces cross at the same end now, and the one the edge
borders runs along it.

**FIXED.** A plane crossing a wedge thinner than twice the tolerance, where nothing on one side is wide enough to be a
polygon, counted as a cut that failed, and a boolean threw the whole cut away and cut both bodies by every plane of
both: for two slabs that took sixteen seconds and came out 69 000 mm3 off, and the other way round open. Such a wedge
is left whole and judged by its middle.

**FIXED.** Separating a body into its pieces, as `Boolean3.SplitShells` and the booleans do, dropped a closed piece
thinner on average than the tolerance that no other piece held, taking it for a sheet of no thickness: the slivers a
plane leaves along a slab's face went with it, seventy thousand cubic millimetres of one. It is a piece of its own now.
With this and the slivers above kept, the steel beam of the Tekla IFC export that came out open from the fifteenth of
its sixteen openings cut in turn, where two faces of its web run a hundredth of a millimetre apart, stays closed
through all sixteen, and ends 60 mm3 from the geometry engine's 25 137 804 where it ended 150 from it.

## 9.0.0

Nothing in GeometryHelper itself changed: it is released at 9.0.0 with GeometryHelper.TeklaConvert.

**BREAKING.** In GeometryHelper.TeklaConvert, `FaceConvert.TryReadFace` and `LoopConvert.TryReadLoop` are removed:
read a Tekla face with `FaceConvert.TryReadFaces`. Each read one face, or one polygon, or nothing, and a face out of
flat came back as nothing, its hole off its plane was dropped, so a body put together from them was open where Tekla
holds it closed. One face cannot hold a face out of flat without moving its corners off the edges it shares;
`TryReadFaces` gives the triangles on its own corners instead, and is what `TryToGeoSolid3` has read with since 8.0.0.

## 8.0.0

**NEW.** Boxes onto sheets of paper: `GeometryHelper.Packing`. `SheetPacker.Pack` takes boxes as `GeoRectangle2`,
in groups of those that belong together, and a `Sheet`: A0 to A4, landscape unless turned, or a size of its own
(`Sheet.Custom`), a `Scale`, `OffsetLeft`, `OffsetRight`, `OffsetTop` and `OffsetBottom` round its edges in
millimetres on paper, and an `Origin`, its lower left corner, which `PlaceCorner` can set from any corner or the
middle. Each group is packed as one block, laid out afresh as close together as its boxes go or kept as it
stands (`GroupLayout`), `Spacing` between its boxes and `GroupSpacing` between groups; a group too large for one
sheet is split, in its order, over as many as it takes. The blocks go onto the sheet from its upper left corner,
by the maximal rectangles method, and when it is full a new sheet is begun beside it, on the side `NewSheet`
names, `SheetSpacing` apart. The boxes are only moved: each comes back with the sheet it goes on, its
`Translation`, which moves any point of what it stands for, and its `ViewBox`, the box moved. A box reported
placed is inside the usable area of its sheet and clear of every other; every box that fits a sheet is placed.
`LargestGroupsFirst` and `FillEarlierSheets` trade the order of the groups for fewer sheets: a thousand boxes of
up to a fifth of an A1 sheet take 17 sheets in their order, 14 with both, as few as their area allows.

**NEW.** `GeoTransform3.ToCoordinateSystem` and `GeoTransform2.ToCoordinateSystem`, the reverse of
`FromCoordinateSystem`: the transformation that takes world coordinates into the local coordinates of a
coordinate system. A whole shape now goes into a frame through `TransformBy` as a point goes through `ToLocal`,
where it took `ToTransform().Inverse()` before, and the matrix is read straight off the axes rather than
inverted. A frame given in the coordinates of another nests by multiplying, the outer one read into first:
`ToCoordinateSystem(inner) * ToCoordinateSystem(outer)`.

**BREAKING.** The default tolerance is set for a model in millimetres: `Tolerance.DefaultEqualPoint` and
`DefaultEqualVector` and `DefaultEqualPlanar` are 1E-2, all three 1E-4 before, and `DefaultEqualAngleRad` is
still a degree; `Tolerance.Global` starts from them, and `new Tolerance(point, vector)` takes the planar one.
Points a hundredth apart are now one point, a vector shorter than a hundredth has no direction, a loop of less
than a hundredth of a square unit is no polygon, and a face may stand a hundredth out of flat. Tekla Structures'
own cuts leave faces further out: at 1E-4 `TryToGeoSolid3` dropped such a face without a word, the top of a
notched concrete beam 0.04 mm out at one corner, and the beam came out open, with no section and a fifth too
little volume; it now reads such a face as triangles on its own corners (see `GeoFace3.FromLoops` below), and
the planar threshold stays as narrow as the point one. What turns on the tolerance turns with it: offsets and booleans merge corners a hundredth apart,
force-directed label placement reads boxes a hundredth apart as touching and settles some labels elsewhere (the
other algorithms place every label as 6.2.0 did), and geometry in metres wants a tolerance a thousand times
smaller, `new Tolerance(1E-5, 1E-5, Tolerance.DefaultEqualAngleRad, 1E-5)`, which `IfcConvertOptions` for output
in metres now has to be given.

**FIXED.** What the wider tolerance brought out. `Locate` on a solid took any crossing within a hundred point
tolerances of a face's rim for a graze and cast again, a millimetre at the new default, so every crossing of the
millimetre-wide faces round a bolt, a bar or a turned profile was refused and a point on their axis came out
outside; the band is twice the point tolerance. A prepared solid took two crossings near each other along a ray
for a seam and any others for real surface, but a ray leaving a body past an edge, at a shallow angle to the
face beyond, is held by that face too a little further on, and a point well inside a turned block came out
outside; each crossing is now judged by where it lands on its triangle, as the body's own count judges it. The
booleans cut and glue within one tolerance: a planar threshold wider than the point one left cells standing
past a cutting plane by more than the glue closes, and unions and differences of bent bars came out open. They
work with the planar threshold no wider than the point one, after splitting any face flat only to the wider
threshold into triangles on its own corners, so a face of a Tekla cut still closes; the convex clipping behind
clash volumes does the same, and so does the merge of a convex hull, which had left points it was built from
outside it. `SignedDistanceTo` gave the last bit of distance for a point `Locate` calls on the surface, where
the rule it documents, and the prepared solid, say nought. Sweeps, the joining of pieces after a cut and the
line where two planes meet judged lengths and sines they had just found against the default vector tolerance
instead of their own: a bar bent under half a degree threw, and so did a boolean run tighter than the default.

**NEW.** `Tolerance.Default`, the four defaults together: `DefaultEqualPoint`, `DefaultEqualVector`,
`DefaultEqualAngleRad` and `DefaultEqualPlanar`. `Tolerance.Global` starts as it, and it stays the defaults
whatever `Global` is set to or a scope makes it. `new Tolerance()` reads as the defaults and is not: `Tolerance`
is a struct, and one made without a constructor, like `default(Tolerance)`, has every threshold at 0. Then only
an exact match is equal and a face out of flat by rounding alone is refused: the notched Tekla beam, read so,
lost five of its 26 faces and came out open.

**NEW.** `GeoFace3.FromLoops` reads a face from loops of corners that need not lie flat, as a modeller gives the
faces of a body: one face, holes and all, where they lie flat within the planar tolerance, and otherwise triangles
on the corners themselves, wound as the boundary is, covering the boundary less its holes. Each triangle is exactly
flat and keeps the edges the face shares with its neighbours, so a body read that way is as closed as it was given
and no corner moves; a hole off the plane of its boundary is kept too, and a hole with no area is left out. Read
so, the notched Tekla beam closes with the volume of its concrete even at `new Tolerance()`.

**FIXED.** The surface triangulation of a face with holes fell back to the fan, laid over the holes, when two holes
bridged into the outline reached the same corner of it: the corner then stands in the loop twice, and the second
bridge ran from the copy opening away from its hole, across the first. The side of that beam, with its three
openings, met it, so whatever reads a body's surface as triangles — a prepared solid, a clash, an index, an OBJ
export — took its openings for material. A bridge now leaves from the copy opening toward its hole.

**FIXED.** A polygon of three corners was refused as not flat at a planar tolerance of nought: how far the corners
stand from the plane through them comes out as rounding, not nought. Three corners share a plane whatever they are,
so only a longer loop is measured against one.

**CHANGED.** In GeometryHelper.TeklaConvert, `TryToGeoSolid3` reads each face of a Tekla solid with the new
`FaceConvert.TryReadFaces`, built on `GeoFace3.FromLoops`: a face out of flat, or with a hole off its plane, comes as
triangles on the corners Tekla gave, turned to face the way Tekla says, where it was refused, or lost its hole, and
left the body open. A solid Tekla holds closed now comes out closed whatever the tolerance; only a face with no area
is still left out. `FaceConvert.TryReadFace`, one face or none, reads as before.

**CHANGED.** In GeometryHelper.IfcConvert, a planar face is read from its loops with `GeoFace3.FromLoops`. A hole out
of flat was dropped without a word, and a hole off its face's plane dropped with a warning, and either left the walls
round it open and the hole counted in the volume; a face out of flat went to the geometry engine's mesh. Each now
comes as triangles on the corners the file gives, and the body closes with its holes open. The engine's mesh, and
then the boundary alone with a warning, remain for loops that cannot be split.

**FIXED.** `TrySubtract`, `TryIntersect` and `TryUnion` threw `ArgumentException` ("The two shapes lie in different
planes") instead of returning. Gluing the cells of a boolean took from each other the faces lying back to back, found
by one lying in the plane of the other, but a small face a hair out of a long one's plane lies in it while the long
one's far end stands millimetres off the small one's, and taking the long face from the small one was refused. A steel
beam of a Tekla IFC export, 665 m from the origin and turned a hundredth of a degree off the axes, threw so on seven of
its sixteen openings cut in turn, as IfcConvert cuts them when the geometry engine cannot. The pair is now found when
either face lies in the other's plane, and each is cut in its own plane with the other laid out in it. The search for
a point inside a body, at a tolerance finer than the default, also threw ("Cannot normalize a zero-length vector"): it
took the direction of a triangle at the default tolerance.

**FIXED.** A difference, union or intersection of two closed bodies could come out open where cutting the other of
the two, or both, closes it: ten of the beam's openings left it open. The boolean now tries the other way round, and
then cutting both, before it settles for an open result; the beam comes out closed from fourteen of its sixteen
openings, and with all sixteen cut within 90 mm3 of the geometry engine's 25 137 804. The fifteenth still leaves a seam
where two faces of the web run a hundredth of a millimetre apart, the point tolerance itself.

**FIXED.** `IsClosed` called a closed body open when a short edge a hair out of true lay beside a long one: the two
were matched along the short edge's own direction, carried the length of the long one, where a thousandth became
three tenths. They are matched along the longer.

**CHANGED.** The solid booleans that try (`TryUnion`, `TryIntersect`, `TrySubtract`, and the cutting of openings behind
queries on a body with them) no longer throw when a shape cannot be worked out: they return false and write a warning
with what was thrown to `GeometryHelperLog`. Thrown out of the GeoSolid3 boolean IfcConvert falls back to for openings,
such an exception took the reading of a whole IFC model with it; IfcConvert now also leaves an opening that throws
uncut, with a warning, rather than losing the product.

**CHANGED.** `TransformBy` on `GeoPolygon3`, `GeoFace3` and `GeoSolid3` carries a polygon over as it is when the
transformation keeps every length, a turn, a shift or a mirror, instead of building it again and checking it against
`Tolerance.Global`. Checked again, a polygon at the very edge of the planar tolerance could land a rounding past it and
be refused, and the whole body with it: a girder's face with one corner a hundredth low was, moved 600 m out. A
stretch, a shear or a projection is still checked, and still throws where it collapses a polygon; a sliver face a body
read from IFC carries no longer makes a plain move of that body throw.

**FIXED.** In GeometryHelper.IfcConvert, placing a body rebuilds its faces where they land, and a face flat with
nothing to spare, as the girder's was, could land off flat and was left out, leaving the body open. It is kept as a
face is read, as triangles on its own corners, and only a face with nothing of an area left is counted as left out.

## 7.0.0

**BREAKING.** `ArrangeItem.Offset` only sets: it writes `OffsetTop` and `OffsetBottom` both, and holds nothing of
its own, so code that reads it no longer compiles. `OffsetTop` and `OffsetBottom` are what placement reads, plain
`double`s, 50 each unless set, where they were `double?` taking `Offset` when null; code that set them to null, or
read them as nullable, has to change. Set after a side, `Offset` overwrites that side too, so of
`new ArrangeItem { OffsetTop = 20.0, Offset = 50.0 }` both sides are 50, where in 6.3.0 the top was 20 whatever
the order. Code that sets `Offset` first, or only, places every label where 6.3.0 did: compared byte for byte over
1,560 lines of candidates and runs, gaps of each side's own among them.

**NEW.** `ArrangeItem.Side` keeps a label to one side of its leader: `ArrangeSide.Top`, the side that faces up in
the drawing and the left of a vertical leader, the side of `OffsetTop`; `ArrangeSide.Bottom`, the side of
`OffsetBottom`; or `ArrangeSide.Both`, the default, either, as before. Kept to one side, a label is tried on the
rows of that side alone, the reach its obstacles are gathered within is that side's, and with no free place there
it is left on the first of them and reported not `Placed`, however free the other side; the second pass keeps the
side too. A gap as wide as it goes on the other side was the nearest to this before, and it only made that side
the last one tried. With `Both`, every candidate and every result is what it was, over the same 1,560 lines.

Label placement, put right after a review of 6.3.0. A label with the same gap on both sides of its leader is
placed as before, save where a fix below says otherwise: its candidates and results are those of 6.2.0 and 6.3.0,
compared byte for byte over 825 candidate lists and 105 runs of the five algorithms, the second pass included, and
a test now holds them to what a build of 6.2.0 gives.

**FIXED.** A label given a gap of its own on each side went to the side with the wider gap, the one it was to keep
further from. The rows of the two sides were tried level by level, the leader's left first, so a leader drawn the
other way, greedy placement in open space, and every fallback to a label's first candidate went to whichever side
that happened to be. And greedy placement, which keeps the freest of its first few free places, found each place
in the first row of a side that side's gap clear of the label's own leader, which is among what a label keeps
clear of whenever every leader of a drawing is handed to every label, as both example applications do. The rows
of both sides are now tried nearest first, as `GetPlacePoints` always said, two as far off together as before;
greedy placement counts the room beyond what the wider gap of a side asks for; and the force-directed algorithm
pushes a label sitting right on its own leader towards the side with the smaller gap, where it pushed it along
+X, off to the right of a vertical leader.

**FIXED.** Which side of a leader is its top no longer rests on the tolerance of vectors, which is a length: a
leader is vertical, with its top on the left, within the tolerance's `EqualAngleRad` of vertical. With
`EqualVector` at 1, a horizontal leader passed for a vertical one, and its two gaps swapped with the way it was
drawn. A leader within a degree of vertical, the default, now has its top on the left whichever way it leans.

**FIXED.** The second pass of a run kept clear of the regions of the labels it tried again, and of no others: a
region given only to a label the first pass placed was lost, and a label could go back onto it. Every label keeps
clear of the regions of every item in both passes, as documented.

**FIXED.** `MaxBacktrackSteps` counts steps back and nothing else. Bounded backtracking and constraint satisfaction
counted every label placed as a step, so a run of more labels than steps did the whole search, gave up, and was
placed by the greedy algorithm; and they went a call deeper for each label, which on some hosts ran the stack dry
at a few hundred labels. Both now search in a loop, and a search that never has to go back is never cut short.
Wherever 6.3.0 did not run out of steps they give the results it gave, compared with its recursive searches over
8,000 runs of crowded scenes. Bounded backtracking also measures clearance only where it breaks a tie, which is
all it decides.

**FIXED.** `MaximumCandidates` caps the candidates of a label. It was checked only before each group of four
slides, so every row still gave its two places straight across: a cap of 1 gave 6 candidates, and a million rows
two million.

**FIXED.** `Offset`, `OffsetTop` and `OffsetBottom` refuse NaN and infinity with `ArgumentOutOfRangeException`, as
the rest of the library refuses sizes and distances that are not finite numbers. An infinite gap made candidates
of NaN, and a side's NaN turned the obstacles of the other side off.

**FIXED.** A gap as wide as a number goes, `double.MaxValue` on one side as a "never", made `Arranger.Run` throw:
the second pass made a region of a label moved so far off that the corners of its box ran together. Such a box is
left out of the regions of the second pass, and with the nearer rows tried first it is only reached when nothing
nearer is free. And the reach the obstacles of a label are gathered within takes in the first row of each side as
well as the last, so a gap negative enough to take the rows of one side across the leader and past those of the
other no longer leaves out the obstacles over them.

## 6.3.0

**NEW.** A gap for each side of a label's leader. `ArrangeItem.OffsetTop` is the least gap between the label and
the leader on the side of the leader that faces up in the drawing, and `OffsetBottom` on the side that faces
down, each taking `Offset` when left unset, as both are unless set: a dimension text can sit close above its line
and keep well clear below it. Which side faces up does not depend on which way the leader was drawn, and a
vertical leader has its top on the left, where the text of a vertical dimension stands. The candidates every
algorithm chooses among, the reach the obstacles are gathered within, and the copy the second pass runs on all
follow both. With neither set, every candidate and every result is the one 6.2.0 gives: compared byte for byte
over 128 candidate lists and ten runs of the five algorithms, the second pass included.

## 6.2.0

Nothing in GeometryHelper itself changed: it is released at 6.2.0 with the rest of the set. GeometryHelper.IfcConvert
and GeometryHelper.TeklaConvert read a whole IFC model on every core at once (`IfcStoreCache.GetAllGeometries`, and
`ReferenceModelConvert.ToGeoSolids` / `ToIfcGeometries`): three Tekla reference models of about 21,000 products
each came back in 28 s instead of 85 s. They also give the bodies of one brep back in one order, where the geometry
engine's order changed from one reading of a file to the next. Their notes are in their packages.

## 6.1.0

**NEW.** How deep a clash runs, and a way to leave the shallow ones out. `ClashResult.Depth` is the least
thickness of the region two parts share, the smallest side of the least box round it, and of the deepest region
where they share more than one: a bar grazing a flange by half a millimetre is half a millimetre deep however
long the graze, where its volume grows with the length, and a bar through a plate is as deep as the thinner of
the two. It is measured when first asked for, so a report that never asks pays nothing for it. `ClashOptions`
takes a `minimumDepth` and a `minimumVolume`, both nought unless given: a hard clash shallower or smaller than
them is reported as `Touch`, keeping its overlaps, volume and depth, or left out when touching is not asked for.
The constructor of 6.0.0, `ClashOptions(clearance, includeTouching, maxDegreeOfParallelism)`, stays as it was,
so code built against it runs unchanged; a call naming fewer arguments, as `new ClashOptions(clearance: 25.0)`,
compiles as before and gets the same answers.

**NEW.** Reinforcement checked by its centre line, beside checking it as bodies, which is unchanged. A
`ClashBar` is a bar's centre line, bends as arcs, and its radius, and `Clash3.Find(bars, parts, ...)` checks bars
against parts, as bodies or prepared, with no body built for any bar and no boolean run: a bar runs into a part
where the part comes nearer its centre line than its radius, touches it at exactly the radius, and is too near
within the clearance, the gap measured from the bar's surface. It is exact on the straight runs and within the
chord tolerance on the bends, a thousandth of the radius unless given, and openings are honoured. Each hard clash
says how far the part reaches into the bar, `Depth`, and how much of the centre line runs inside it,
`LengthInside`. Where the centre line stays outside, the depth is the radius less its nearest approach, exactly;
where it runs inside, the radius and as far again as it runs beneath the part's surface, at most the diameter, to
within a sixteenth of the radius. `Overlaps` stays empty and `Volume` nought, so `minimumVolume` does not apply
to a bar, and `minimumDepth` does. Three things read differently from the bar built as a body: the ends are read
rounded, a radius past where the bar ends flat, so the check errs on the side of reporting there; a plate thinner
than the bar reads as the radius and half the plate, where two bodies read the plate's own thickness, since the
plate cuts the bar through however thin it is; and the bar is round, where the body lies up to its chord
tolerance inside it. On a Tekla model, 32 bars against 11,393 IFC bodies found the same 28 hard clashes and 9
near misses both ways, gaps within 0.01 mm and depths within 0.8 mm of each other, in a quarter to a third of the
time.

## 6.0.0

Three breaking changes. The first two have one reason, that a name and a tolerance were each saying
something they did not mean; the third, that label placement wrote its answers into what it was
given.

**RENAMED.** GetClosestOnBoundary returned the shortest segment joining two shapes. The name reads as
"the closest thing on my boundary", which is a different idea, and GeoPolygonArc2 and
GeoPolylineArc2 had taken it for exactly that and returned an edge of the shape instead. The
summaries said the right thing all along, so only the names moved:

- GetClosestOnBoundary(other) is now GetShortestLineTo(other), on GeoLine2, GeoCircle2,
  GeoRectangle2, GeoPolygon2, GeoPolyline2 and GeoLine3.
- Projection2.GetClosestSegment and Projection3.GetClosestSegment are now GetShortestLineTo.
- On GeoPolygonArc2 and GeoPolylineArc2 the old method is GetClosestEdge, because a GeoEdge2 is
  what it hands back. The return type differs, so a caller who takes the new name for the old
  meaning stops compiling rather than quietly getting a different answer.
- GetClosestPointOnBoundary is unchanged: it was already right.

No [Obsolete] forwarders were left behind. Renaming is a one-line change at each call site, and
carrying both names would double the surface of the library for two releases days old.

**MEASURED DIFFERENTLY.** Whether an arc reached a point of its own circle was settled by comparing two
directions within Tolerance.EqualAngleRad, a whole degree by default. A degree of a large arc is a
long way: on a radius of a hundred it is nearly two of whatever the drawing is measured in. Two
things came of that, both wrong by more than rounding:

- Arc2.DistanceTo could weigh a point the arc does not reach and so report the arc nearer than any
  of its own points allow.
- Arc2.GetIntersections could report a crossing lying clean off the end of an arc, and everything
  built on it inherited that: booleans, splitting, offsetting, CollidesWith and Locate.

The question is now asked as a distance rather than as an angle, which is the unit the answer is in.
Arcs that only nearly touch are no longer reported as crossing. Nothing in the existing test suite
had to be amended for this.

**RESHAPED.** Label placement, GeometryHelper.Arranging. Arrange was the label, the entry point and the
answer at once: Arrange.Run wrote each label's Placed and TranslationVector back into the list it was
given, and returned the vectors besides. Its second pass lent the labels relaxed blocks for the while
and handed their own back after, which lost them for good when a label was listed twice, and showed
the loan to anything reading the labels meanwhile. Now the label is only read, and the answer comes
back on its own:

- Arrange is now ArrangeItem, and its properties say what they are for: GeoRectangle2 is Box,
  GeoLine2 is Leader, BaseOffsetFromLine is Offset. BlockPolygons and BlockLines take any
  IReadOnlyList and start empty.
- Arrange.Run(list[, options]) is now Arranger.Run(items[, options]). It returns an ArrangeResult
  for each item, in the same order, holding what the item used to carry: Translation and Placed. A
  null entry is answered with default, not moved and not placed.
- Nothing is written into the items, so the same items can be run again, with other options, or on
  several threads at once.
- ArrangeOptions.Default is a new instance each time it is read. One instance for the whole process
  could be changed by anyone for everyone, and kept the tolerance of whichever thread read it first,
  a Tolerance.Use scope included.

```
Before                                     After
new Arrange { GeoRectangle2 = box,         new ArrangeItem { Box = box,
              GeoLine2 = leader,                             Leader = leader,
              BaseOffsetFromLine = 50 }                      Offset = 50 }
Arrange.Run(list, options)                 ArrangeResult[] results = Arranger.Run(items, options)
list[i].TranslationVector                  results[i].Translation
list[i].Placed                             results[i].Placed
```

The namespace is unchanged; the code now lives in src/GeometryHelper/Arranging, one type to a file.
As with the renames, nothing old is kept alongside.

### NEW. Both halves of the nearest-thing pair now exist on both families of shape.

- GetClosestEdge on GeoPolygon2, GeoPolyline2 and GeoRectangle2, returning the edge of the shape
  nearest a point, a segment, a circle or an arc. The probe is always a single primitive: the
  nearest edge of one many-edged shape to another is really a pair of edges, which is a different
  answer from the one the name promises.
- GetShortestLineTo on GeoPolygonArc2, GeoPolylineArc2, GeoEdge2 and Core.Arc2. The closest point
  pair for an arc against a segment, an arc or a circle is exact, not sampled.
- A straight shape can be measured against a curved one, and asked whether it meets one, in either
  order. GeoRectangle2 is offered to the curved types for the first time.
- GeoSolid3 could only be asked about a point. It now answers DistanceTo for a segment, a ray, a
  triangle, a polygon, a polyline, a plane, either kind of box and another solid; CollidesWith for
  a segment, a ray, a polyline, a polygon, a face, a box and another solid; GetIntersections for a
  segment, a ray and a plane; GetClosestPointOnBoundary; and GetShortestLineTo, built on a new
  triangle-to-triangle closest pair in Projection3.
- A ray is measured as a ray, not as a segment cut to some chosen length.

A GeoCircle3 is deliberately not on that list. The distance from a circle in space to a flat face
has no closed form, so turn it into a chain first and say in the call how close an answer you want.

### ALSO NEW. How deep inside a closed shape a point sits.

DistanceTo reads a closed shape as a filled region, so a point anywhere inside one is nought away and
the depth cannot be got back out of the answer. SignedDistanceTo keeps it: the magnitude is the
distance to the boundary whichever side of it the point is on, and the sign says which side.

- Its whole definition is tied to Locate: negative where Locate answers Inside, nought where it
  answers OnSide, positive where it answers OutSide. So Math.Abs of it is always the distance out to
  the outline. Within the tolerance band around the boundary the sign is not worth reading, because
  the answer there is nought either way.
- Offered by every shape that encloses an area or a volume: GeoCircle2, GeoRectangle2, GeoPolygon2,
  GeoFace2, GeoPolygonArc2, GeoSolid3, GeoObb3 and GeoAabb3. A curved loop is measured on its arcs.
- The boundary of a GeoFace2 is its outline and the rim of every hole, so a point in a hole is off the
  material and is measured to the rim it sits in. The surface of a pierced body is its faces and the
  walls of every opening, and a duct running out past a face bounds nothing out there, so each
  candidate is held against the body and kept only where the body agrees it is on the boundary.
- Not offered for GeoTriangle3, GeoPolygon3 or GeoCircle3. They are flat regions standing in space and
  enclose no volume, so a point is inside one only when it is also on its plane: a sign for them would
  be negative on a set of no thickness and would read as though it meant more.
  GeoPlane3.SignedDistanceTo already answers the question that does make sense for something flat.

DistanceTo is untouched. Turning its sign over instead would have been a change no compiler could
catch, and DistanceTo is also asked of two shapes, where a sign would have to mean penetration depth
and does not. The naming follows SignedArea beside Area, and GeoPlane3.SignedDistanceTo, which the
library has carried all along.

A GeoFace2 also gained the plain DistanceTo to a point, which it had never had at all.

### ALSO NEW. Arcs in space, and with them a reinforcing bar.

A Tekla rebar carries a bending radius, so a bar is a chain of straight runs with a tangent arc at
every bend. Nothing in the library could hold that, because a bulge is a flat idea: a chord and a
bulge are satisfied by an arc in any of the planes through that chord.

- GeoEdge3 is the twin of GeoEdge2 with the one thing space needs, a Normal saying which plane the
  bulge is read in. A positive bulge sweeps counter-clockwise about it.
- GeoPolylineArc3 is the open chain, and requires no plane, exactly as GeoPolyline3 requires none.
  This is the type a bar is.
- GeoPolygonArc3 is the closed loop and does enforce coplanarity, as GeoPolygon3 enforces it. That is
  what keeps its area, its centroid, what is inside it, its offsets and its rounding exact: each is
  answered by laying the loop out in its own plane as a GeoPolygonArc2 and lifting the answer back.
- Corner3.Fillet rounds the corners of a chain in space, one radius or a radius per corner, and each
  corner is rounded in the plane of its own two legs, so the bends need not share a plane. A bar is
  shorter than its set-out by 2r - pi r / 2 at every bend, which is the number a bar schedule carries.
- PlanarMap carries arcs both ways at last, so the round trip closes on a curved plate edge instead of
  flattening it. An arc whose normal runs against the frame changes the sign of its bulge coming down.
- GeoPolyline3 can now be measured against a segment, another chain, a triangle, a polygon, a plane,
  either kind of box and a solid, which is what gives ToPolyline3 somewhere to lead.

MEASURING a curved chain against another shape in space is deliberately not offered. The distance from
an arc to anything but a point has no closed form once the two are not coplanar, so say how closely the
bar should be followed and ask the ordinary question: bar.ToPolyline3(0.1).DistanceTo(slab). A sampled
chain lies inside the arcs it stands for, so a clearance worked out that way errs on the safe side.
Where the two CROSS is another matter, and is exact -- see below.

In GeometryHelper.TeklaConvert, ReinforcementConvert turns a Reinforcement, any sequence of them or a
RebarSet into bars, and RebarGeometryConvert turns one geometry. A bar is only ever read as Tekla works
it out, because the hooks, the offsets and the lapping are settled by then and the set-out points do not
show it. A sequence keeps its grouping: one entry per reinforcement, holding that one's bars.

### ALSO NEW. Whichever of two shapes is in hand can be asked about the other.

Core could work out roughly fifty-five things no type would answer. Some of it was a plain gap, and
some of it ran one way only: a solid could be asked about a segment and a segment could not be asked
about a solid. All of it is now on the types, and none of it changed an answer.

- Translate on all fourteen types in space, which only GeoPoint3 had.
- GetClosestPointOnBoundary on GeoFace2, GeoFace3 and GeoPlane3, and DistanceTo on GeoFace3, which had
  none at all. The plane and space differ here and each follows its own side: in the plane the answer
  is a point of the boundary even for a point on the material, as GeoPolygon2 does; in space it is a
  point of the region, as GeoPolygon3 does.
- CollidesWith, GetIntersections and TryIntersectWith in space, both ways round: thirty-one directions
  across GeoAabb3, GeoObb3, GeoLine3, GeoRay3, GeoPlane3, GeoPolygon3, GeoTriangle3 and GeoFace3.
  GeoLine3 also gained GetIntersection, the nullable answer beside TryIntersectWith that GeoLine2 has
  always had.
- GetShortestLineTo in space on GeoPoint3, GeoLine3, GeoRay3 and GeoTriangle3. The segment leaves the
  shape it was asked of and lands on the other, so opposite directions are each other reversed.
- GeoArc2 can be measured against, joined to, tested against and crossed with every shape in the plane,
  where before it knew only a point, a segment and another arc. Two arcs of one circle collide when
  either holds an end of the other: circles lying on each other meet along their length, not at points.
- GeoEdge2 can be asked whether it touches a shape, not only how far off it is.
- GeoFace2 answers about shapes and not only about points, through the new Core.Face2. The boundary of
  a face is its outline together with the rim of every hole, so crossings are the union over that and
  the reach is the shortest over it. Touching means reaching the material: a probe must reach the
  outline and no hole may hold it whole. A ring drawn around a hole has its centre in the hole and
  crosses no rim, exactly like a speck lying in one, and is told apart by whether the rim falls within
  the probe.
- And every shape can be asked about a face in turn, which is the direction the first pass left out.
- DistanceTo in space now runs both ways: a triangle, a polygon, either box, a plane and a ray can be
  asked how far off the other shapes are, where before several of them could be asked about a point and
  nothing else. A box against a box, a plane against a plane and a triangle against a triangle could not
  be asked at all.
- TryIntersectWith runs both ways in the plane, as GetIntersections already did, and a circle, a curved
  loop and a curved chain can be asked about their own sort.
- Which edge of a many-edged shape is nearest can be asked from either side. The probe is still a single
  primitive -- that rule is about the probe, not about which of the two makes the call.
- A point can be asked how deep inside a shape it sits, through SignedDistanceTo, and in the plane it
  reaches the arcs, the curved chains and the face, which it could not before.

- A GeoEdge2 can be asked about, not only ask. Every shape in the plane takes one, reading it as the
  segment or the arc it stands for, which is how the edge has always read itself.

Core and the shape types now agree everywhere, and so does shape against shape: there is no pair one
of them answers that the other will not, whichever is in hand.

### ALSO NEW. Where an arc in space reaches, and what can be done to it.

A GeoArc3 could say how far a point was and nothing else. Core.Arc3 now answers for a plane, a
segment, a ray, another arc, a circle, a triangle, a polygon, a face, either kind of box and a body,
and none of it is sampled. Two readings carry all of it: two coplanar circles meet on their radical
line, two in different planes on the line where the planes meet, and one quadratic gives the
candidates for the other shape's own test to keep.

- GeoCircle3 forwards to the whole-turn arc; GeoEdge3 reads itself as its arc or its segment and
  dispatches; GeoPolylineArc3 and GeoPolygonArc3 take the union over their edges, naming each place
  once so a crossing at a shared corner is not found twice.
- A bar can be CUT: at a point, at a distance, at a set of distances, by a plane, a face, a body,
  either kind of box, or an array of bodies or boxes. The pieces come back as chains of arcs, so a
  bar stopped at a pour break keeps its bends and with them the length a schedule needs.
- A closed loop can be cut too, and gives open chains: a ring cut once is a strip as long as the ring.
- An arc in space can be cut in two, at a parameter, a point or a length along the curve, keeping its
  centre, its radius and its plane.
- One chain can be asked about another, and about a segment, a ray and a straight GeoPolyline3. That
  one walks every pair of edges, so each edge carries a box round itself and a pair whose boxes cannot
  reach each other is dropped before any arithmetic.
- GeoPolygonArc3 works in its own plane: booleans against a coplanar loop or polygon, Chamfer,
  TryFilletAt, TryChamferAt, SignedDistanceTo, the joining line and the nearest edge.

WHERE two things cross is exact; HOW FAR APART they are is still the refusal, and the line is drawn
there on purpose. A crossing reduces to one quadratic; a distance from an arc to anything but a point
has no closed form at all once they are not coplanar.

A second shape has to lie in the loop's plane and is refused where it does not, with an
ArgumentException. Projecting it in would report two stirrups a hundred apart as overlapping and say
nothing about it. SharesPlaneWith asks beforehand. A POINT is the exception, and not as a compromise:
a point off the plane stands at the same height above every point of the boundary, so the nearest
place to it is the nearest place to its shadow, and the answer is exact.

### ALSO NEW. The families that change geometry, which no matrix had ever covered.

Every audit before this one covered measuring. The operations that change geometry were laid out
against the types for the first time, and the gaps are closed.

- A REGION in the plane can be cut into regions. GeoPolygon2 and GeoFace2 are cut by the straight line
  through a segment -- its length is ignored, as a plane's extent is -- or along a GeoPolyline2 drawn
  across them. The sides are called left and right, of the cutter's own direction, because above and
  below mean nothing in the plane. Space had both readings all along.
- ARCS, CHAINS, LOOPS AND EDGES can be extended and trimmed, where only GeoLine2 and GeoLine3 could.
  An arc is lengthened along itself: the centre and radius stay, the sweep grows, and the distance
  asked for is arc length and not chord. A chain is lengthened by its end leg, so every other leg and
  bend survives -- which is what a bar wants for anchorage. Both outwards only; the splitting family
  shortens one and keeps its bends, and TryTrimTo is that cut with the end named rather than the piece.
- ROUNDING a straight chain or loop. GeoPolyline2, GeoPolygon2 and GeoPolygon3 can be filleted, which
  the curved types could already do. The answer is the curved type, because a rounded corner is an arc.
- CHAMFERING in space. GeoPolyline3, GeoPolygon3 and GeoPolylineArc3 can be cut back. A chamfer needs
  no plane at all -- it moves back along one leg and forward along the other -- so a chain lying in no
  one plane is cut all the same. Only a corner between two straight legs is cut, the rule the fillet
  keeps, because a leg that curves leaves at a tangent.
- BOOLEANS on the flat shapes in space. GeoPolygon3 and GeoFace3 have all four through the coplanar
  lift; GeoObb3 has the three solid ones through the body it bounds, offered both ways round with
  GeoSolid3. Everything flat comes back as GeoFace3, because joining two areas can leave a hole in the
  middle and only a face can hold one.
- OFFSETTING what could not be offset. GeoPolylineArc3.OffsetInPlane moves a bent bar to another cover
  without straightening it: an arc comes back an arc with its radius moved by the distance.
  GeoObb3.TryExpand gives the oriented box the margin the square one already had, keeping its axes.
- CONVERSIONS. GeoTriangle3.ToPolygon3 and ToFace3 name the rebuild a triangle out of Triangulate
  needed before anything taking a polygon would accept it. A triangle with no area is refused, because
  a polygon of three collinear points is not a polygon; IsDegenerate asks first.
- A segment's crossing with another segment is now also readable as a LIST, which is what lets a
  GeoEdge3 be asked about one: an edge offers a direction only where a segment and a bend answer with
  the same shape of call. GeoLine3 keeps its single-point TryIntersectWith and gains no array twin,
  because a second one differing only in the shape of its out would make every existing call ambiguous.

### ALSO NEW. Three more the matrix showed once it was drawn per family rather than per pair.

- A STRAIGHT CHAIN IN SPACE could say how far off nine kinds of shape were and whether it touched
  three. It now answers CollidesWith, GetIntersections and TryIntersectWith about a plane, a segment,
  a ray, an arc, a circle, a triangle, a polygon, a face, either kind of box, a body, and another
  chain. A chain is a run of segments and a GeoLine3 already answered all of it, so each answer is
  the union over the segments with a crossing at a shared vertex named once. Chain against chain
  drops a segment pair whose boxes cannot reach each other before doing any arithmetic.
- A FACE AND A RECTANGLE in the plane had GetIntersections against nine and ten shapes and no
  TryIntersectWith at all. Both now have the twin, for every shape they can be crossed with.
- LOCATE wherever a shape had IsPointOn without it: GeoLine3, GeoPolyline3 and GeoRay3, whose plane
  twins have had it all along, and GeoEdge2, which had neither although GeoEdge3 has both. An open
  shape encloses nothing, so the answer is only ever OnSide or OutSide, never Inside -- not even for
  a chain whose ends happen to meet. GeoPlane3 deliberately keeps GetSide instead: Above, Below or On
  is more than Locate could say. A point keeps IsPointOn alone, because there the point is the one
  asking.

### FIXED. A body's openings, everywhere.

A GeoSolid3 keeps an opening as a whole body subtracted from it, so its faces run straight across
every hole: a plate's top face is a whole square even where a bolt hole passes through it. Fifteen
queries read those faces, or the mesh made from them, as where the material ends -- because
Triangulate said openings are not meshed and, in the same remark, that clash detection reads its mesh
as the boundary. A pin through a bolt hole, five clear of every wall, was a clash, nought away, and
crossed four times. For a clash check between Tekla parts that is every bolted connection.

- CollidesWith, GetIntersections, DistanceTo and GetShortestLineTo against a segment, a ray, a
  polyline, a triangle, a polygon, a face, either box and another body now read the material. So do
  DistanceTo, SignedDistanceTo and GetClosestPointOnBoundary of a point, which were wrong near an
  opening in a way no test caught because they agreed with the old Locate.
- Locate asked the faces before the openings, so the middle of a bolt hole at the level of the top face
  was boundary. The openings are asked first.
- TrySubtract judged a cell in two pieces by one point, so subtracting a box that overlapped an
  existing hole could throw away material nowhere near it; GetNetVolume inherited that. Every cell is
  separated into its pieces before it is judged.
- GeoBvh3.FromSolid, and so BuildIndex, index the material: a ray down a bolt hole passes through.

Touching or crossing cuts in only the openings the probe can reach, so a bolt against a plate with
twenty holes costs one cut. Distance cuts them all, since the nearest material can sit on the rim of
an opening the probe never comes near. A body asked many questions is cut once with TryCutOpenings.

### ALSO NEW. Solid against solid, for a clash report.

- GeoSolid3.TryCutOpenings and TriangulateSurface: the body with its openings cut in, and the mesh of
  where its material ends.
- GeoSolid3.Intersect(other) -> GeoSolid3[]: one body per region two bodies share, so a beam through
  two plates reports two clashes; empty when they share no volume. GeoSolid3.SplitShells: the separate
  pieces of any body. Two regions meeting only along an edge share no volume and are two.
- GeoSolid3.TryGetContact(other, out GeoFace3[]): where two bodies that only touch lie against each
  other, face to face, less any hole in the material under it. Edge and point contact have no patch;
  CollidesWith reports those.

Nothing above changed an answer that was already being given.

### FIXED. What a review of Geometry and Core found.

- Intersecting a bent bar with anything took tens of seconds, and so did a clash check of
  reinforcement with hooks or bends: a group of eight bent bars against one IFC beam took 57 s. The
  intersection cut the bar by the planes of its own bend as well as the other body's, and every such
  plane runs on through the rest of the bar and cuts it again: thousands of cells, and room for them to
  go wrong. A bar bent over a plate came out 2969 where it shares 2967, reaching five below the plate,
  and a stirrup wholly inside a beam threw after half a minute. It now cuts one body only, the one fewer
  planes cut, by the planes of the other that come near it and of its own openings: the eight bars take
  12 ms, and every answer here is exact. Where a plane still crosses a cell and leaves it whole, the
  other body is cut instead, when that costs about the same. (Found testing the clash check in Tekla.)
- The solid booleans were wrong on about one pair in 250 where one body passed a few thousandths from a
  corner of the other, and unions and differences of such pairs could throw. A plane cutting that close
  to a corner leaves a sliver on each face meeting there, under the 1e-4 area a polygon refuses at the
  ordinary tolerance. Refused, the half it belonged to could not close and the cut failed, so a cell was
  left whole across the other body and judged by one point for both sides of it: two blocks sharing
  sixty were found to share nothing. A face crossed with a sliver on one side went whole to the other
  side, or to neither. The pieces of a cut, the caps closing it, the faces joined in one plane and the
  pieces lifted out of a flat boolean are now built as thin as the point tolerance allows, and a crossed
  face gives each side its own pieces. On 47,574 intersections of random convex parts the answers more
  than 1% out fell from 194 to none; checked closer on 1,815 pairs, every union, intersection and
  difference agrees with the same boolean at 1e-10 to a part in a million. On 1,687 pairs among bent
  bars, stars, Ls and Is, 36 answers more than 1% out fell to none, and the 67 unions and differences
  that threw now answer with the right volume; eight of those come out open, faces rejoined across
  thousands of cells not quite meeting. A flat boolean gives the sliver it leaves rather than throwing,
  and takes a face too small for the plane to hold as adding nothing and taking nothing away. The steel
  frame's clash check gives the same results, prepared and checked in 1.6 s rather than 1.9 s. (Found
  fixing the clash check for bent bars.)
- The end face of an I, a channel or any section with an inside corner was meshed across its gaps.
  Ear clipping took the flanges of an I off first and then took as an ear a triangle across the web and
  the gap beside it, since a corner of the web lay on the triangle's edge and only corners strictly
  inside counted; the loop stuck and the face fell back to the fan of its outline, four times its area.
  A prepared column said a point in the plane of its end, a flange's width from the steel, lay on it,
  and a plate with bolt holes meshed that way measured a bolt in its hole at 1.09 instead of 1.96: 31
  of the 400 bolts in the steel frame used to test the clash check. A corner on an ear's edge now
  blocks it, as one inside does, unless it stands on a corner of the ear, as the ends of a hole's
  bridge do. (Found while speeding up the clash check.)
- The solid booleans could leave a sheet of no thickness inside their result. A face between two kept
  cells is found twice, once each way round, and the pair was dropped only when the two copies matched
  vertex for vertex; a cut that reached the cell on one side and not the other left one copy in two
  pieces, and both survived. The volume never noticed, since the two cancel, but a point beside the
  sheet measured a millimetre to the boundary instead of five, TriangulateSurface meshed the sheet and
  SplitShells threw. About one union or difference in two hundred of randomly turned boxes did it.
  Faces left back to back now cancel by the area they share.
- SplitShells, and Intersect through it, threw on a group of faces enclosing nothing -- a lone face,
  or two back to back. Such a group now goes with the piece holding it, and is dropped where no piece
  does.
- Two shapes lie in one plane when the whole of each does. The test was planes parallel within the
  angle tolerance, a whole degree, with one point of one on the other, so a plate turned half a
  degree about a line through that point passed: SharesPlaneWith said true, the flat booleans
  projected it onto the plane instead of refusing it, TryGetContact reported a patch where two bodies
  met along an edge, and the solid booleans could take real boundary crossing a face at a shallow
  angle for the inside of the body. Every corner, and the middle of every arc, now has to lie on the
  plane.
- Crossings under a degree were refused as parallel. Two members were parallel for a crossing when
  the angle between them was under the angle tolerance, a whole degree, however long they were: two
  ten-metre members crossing at 0.9 degrees, 157 apart at their ends, crossed nowhere, while
  CollidesWith said true and DistanceTo nought. The same held for a member against a plane, an arc
  in space against a plane, and a face against the plane cutting it. The booleans lean on the last:
  a box corner poking five hundredths through a face turned under a degree from it went uncut, and
  the union counted it twice. And the ray cast behind Locate lost a crossing that way, so a point
  inside a long member lying under a degree off one of the directions it casts in was reported
  outside the member. Two members are now parallel when they draw apart by less than EqualPoint
  along the longer of them, and a flat shape meets a plane wherever it stands off it by more than
  EqualPlanar. Two infinite lines, and two planes, still go by the angle. IsParallelTo is unchanged:
  it answers about directions.
- DistanceTo from an arc or an edge to a GeoCircle2 measured to the rim. Every closed shape is read as
  the region it encloses, and a segment always read the disc, but an arc inside a disc of radius a
  hundred was seventy away while CollidesWith said the two touched, and a straight edge inside it was
  eighty away from the edge's side and nought from the circle's. GetShortestLineTo still runs rim to
  rim, as it does for every closed shape.
- Two arcs, one lying inside the other's circle, were measured facing each other along the line
  joining the centres rather than on the same side of it, so an arc round a small circle came out
  3.4 from it where the gap was 1.2, and in a random run 49.7 where it was 7.5. DistanceTo and
  GetShortestLineTo between arcs, and every curved edge and chain built on them, weigh all four
  pairings now.
- DistanceTo between a body or a chain in space and a concave GeoPolygon3 used the fan the polygon
  breaks into, which covers a concave polygon only by signed sum: a body sitting in the notch of an L
  measured nought to it while CollidesWith said it did not touch. They use triangles lying inside the
  polygon.
- A size, a distance or a tolerance that was not a number got through where a negative one was
  refused. GeoCircle2, GeoCircle3, GeoObb3, GeoRectangle2, GeoRay3.ToLine and Tolerance each asked
  whether the value was below nought, which NaN is not, and the shape then answered NaN or false to
  every question without a word. TryGetNormal normalised a vector of NaNs into another, so a plane or
  a ray built on one was taken. All of them refuse NaN and infinity now, and a rectangle refuses an
  angle that is not a number.
- IsClosed matched edges by their end points and wanted each used exactly twice. A long edge beside
  two short ones -- which merging coplanar faces and the booleans both leave -- had no partner that
  way, and two blocks meeting along an edge put four faces on one edge; both bodies are watertight
  and measure right, and both were called open, so IfcConvert warned on them and meshed them again.
  A body is closed now when every stretch of every edge is shared by an even number of faces. The
  end points still settle the usual body; only where they leave an edge unmatched is the edge
  matched along its line.
- Ear clipping gave up on a face carrying a row of points along a straight edge -- which merging
  coplanar faces leaves wherever a neighbour had a corner -- because clipping the first ear round the
  loop each time ends with that row and nothing across from it, and no corner of a straight row
  turns. The face then fell back to the fan of its outline, which covers its holes over. A plate
  with its openings cut in came out of TriangulateSurface meshed across every hole, so BuildIndex and
  every query reading the mesh found material there: a ray down a bolt hole hit the plate. What is
  left once the rest is clipped away may now be a row of points with no area.
- GeometryHelperLog.Writer was called on whichever thread logged, from two at once when two did. Work
  spread over every core logs from all of them, and few writers are safe to call that way: logging 64
  messages from eight threads, 63 calls began while another was running. Messages now reach the
  writer, or Trace, one at a time.

### NEW. A body asked many questions.

- GeoSolid3.Prepare() gives a GeoPreparedSolid3 (GeometryHelper.Spatial): the openings cut in once,
  the surface meshed and indexed once, the box kept. It answers Locate, Contains, DistanceTo,
  SignedDistanceTo, GetClosestPointOnBoundary and GetIntersections of a point or a ray, and
  CollidesWith, DistanceTo, Intersect and TryGetContact of another body, as the body does -- a test
  checks every one against the body on random points, rays and boxes -- without cutting the
  openings each time. It is immutable and safe to ask from many threads.
- Clash3.Find (GeometryHelper.Clash, with ClashOptions, ClashResult and ClashKind) checks a set of
  parts against itself, or one set against another, and returns a
  ClashResult per pair that clashes: Hard with the shared regions and their volume, Touch with the
  contact patches, Clearance with the gap when ClashOptions asks for one, and Unresolved with the
  error for a pair whose check threw. The parts are prepared once, only pairs whose boxes come within
  the clearance are looked at, and those are checked in parallel; the results come back in the order
  of the indexes. On 120 random parts it finds exactly what checking every pair by hand finds.

### NEW. Making bodies.

- GeoSolid3.Extrude: a GeoPolygon3 or GeoFace3 along any vector out of its plane, holes running
  through as shafts; a GeoPolygon2, GeoFace2 or GeoPolygonArc2 drawn in a GeoCoordinateSystem3,
  along its Z for a length.
- GeoSolid3.Cylinder between two points, GeoSolid3.Pipe along a GeoPolyline3 or a bent
  GeoPolylineArc3 -- a reinforcing bar from its centre line -- and GeoSolid3.Sweep of any section
  along either, optionally told which way is up.
- GeoSolid3.Revolve of a profile round the Y axis of a placement, through any angle up to a whole
  turn.
  Every body is closed and wound outwards. Along a path the pieces meet mitred and the section does
  not twist, so a section centred on the path gives the section times the path length exactly.

### NEW. Weighing, cutting and fitting.

- GeoSolid3.GetMassProperties(density) gives a MassProperties3: volume, mass, centroid, surface area,
  the moments and products of inertia about the centroid, the principal moments and axes, and
  GetMomentAbout any axis. The integrals are exact for the faces and read the material, so openings
  come out of the weight.
- GeoSolid3.Section(plane): one GeoFace3 per region a plane cuts, holes and all.
- ConvexHull2 and ConvexHull3 (GeometryHelper.Core): the smallest convex polygon or body holding some
  points. GeoRectangle2.Fit gives the rectangle of least area round points, exactly; GeoObb3.Fit a box
  stood on the best face of the hull, the least box for anything with a flat face to stand on.

### NEW. Values that are shapes, and a tolerance for a while.

- IsValid on every value type: whether a value is one a constructor could have made. A default
  GeoPlane3, GeoRay3, coordinate system, arc or GeoCircle3 is not -- no normal, no direction, no
  axes, no radius -- and answers questions without complaint all the same.
- GeoPolygon2.MakeValid and Boolean2.MakeValid: the region a polygon crossing itself covers, as faces
  that do not cross themselves, which is the region Locate and the booleans read. Its Area stays the
  shoelace sum, and now says so.
- Tolerance.Use(tolerance) makes a tolerance the one the overloads without one use on this thread
  until the scope is disposed. Tolerance.Global is swapped whole when set, so it can no longer be
  read half old and half new.

### NEW. Looking at the geometry (GeometryHelper.Export).

- ObjWriter: bodies, faces, meshes and chains in space as a Wavefront OBJ file, one named object each,
  coordinates exact.
- SvgWriter: every shape of the plane as an SVG picture fitted round what is drawn, arcs as arcs, Y up.
- Wkt.Write: points, segments, chains, polygons, faces and sets of faces as well-known text.

### NEW. The chains of the plane and of space side by side.

- StartPoint and EndPoint on GeoPolyline2 and GeoPolylineArc2, which lacked the ends their twins in
  space had; MidPoint on GeoPolyline3, GeoPolylineArc2, GeoPolylineArc3, GeoEdge2 and GeoEdge3.
- TrySplitAtDistance in both shapes on all four chains: the two pieces as first and second, null when
  nothing is cut, and the pieces as a list, the chain whole when nothing is cut.
- GeoPolylineArc3.SplitAtDistances(distances) hands back the pieces as the other chains do, beside the
  form answering true or false.

### FASTER. The clash check, step by step, each answering exactly what it answered before.

Measured on a steel frame of 789 parts with 400 bolt holes, a mesh of 80 crossing bars and 2000 boxes,
24 threads:

- Two indexes are walked nearest pair of boxes first, so the bound drops to near the answer at once
  and the rest is passed over, and GeoPreparedSolid3.GetShortestLineTo walks them rather than meshing
  both bodies again and weighing every pair of faces. The clearance check measures a gap and finds it
  in one walk, which stops at the clearance, where it used to test for a collision twice and measure
  twice. The frame's check: 3.5 s to 1.6 s. Where several segments are shortest alike, between
  parallel faces or round a bolt in its hole, the one given may differ from before; how short it is
  does not.
- Cutting openings in glued the cells back by comparing every face with every face and every edge
  with every edge, and measured the box of every cell and every opening afresh each time one was
  asked for. The faces and edges are now filed by where they lie and compared only with those that
  could match, in the same order and by the same test, and a GeoPolygon3 and a GeoSolid3 measure
  their box once, when they are made. A plate with four bolt holes: 317 ms to 43 ms; with 32 holes,
  1.7 s to 0.6 s. Preparing the frame: 6.3 s to 1.7 s.
- Two triangles one of which lies wholly to one side of the other's plane, clear of it by more than
  twice the larger of the point and planar tolerances, cannot meet: Collision3.CollidesWith and
  Projection3.GetShortestLineTo for two triangles now say so from three distances, where they looked
  for six crossings, and walking two indexes for a collision tests the box of each triangle under a
  leaf before the triangle. The frame's CollidesWith: 1.3 s to 0.35 s on one thread.
- Two parts that collide and are parted by a face of one of them — the one wholly behind the face's
  plane, the other wholly in front, as a beam bearing on a column's flange is — share no volume, and
  the check now takes that from their corners instead of cutting them into cells to find it out. A
  pair nothing parts, a bolt in a hole it fills, goes to the boolean as before. The frame's check on
  one thread: 3.1 s to 1.8 s; 460 of its 469 colliding pairs only touch.
- Two convex parts share one convex region at most, and GeoPreparedSolid3.Intersect now clips it out
  directly — one part cut by each face plane of the other — instead of cutting cells by every plane of
  both. It answers only where the region is plainly one, a clean closed body thicker than ten
  tolerances; a touch, a sliver, or a face the polygon tolerance would lose goes to the general boolean
  as before. The mesh of 80 crossing bars: 3.0 s to 0.04 s. On some convex pairs the general boolean at
  the ordinary tolerance was plainly wrong — nothing shared by an octagonal prism and a block that share
  eight thousand cubic units, by points counted — and the clipping gets those right.
- What two parts share lies where their boxes overlap, so the parting face need only part the parts of
  them inside that overlap: boxes meeting in a face, an edge or a corner settle it at once, as every
  contact square to the axes does, and a beam framing into a column's web between the flanges is parted
  by the web though the flanges reach past its plane. Where one part is convex the region narrows to the
  inside of it as well, which parts such a beam turned askew. All 460 touching pairs of the frame are
  now settled without the boolean, and its check allocates 343 MB rather than 748: 0.35 s to 0.21 s on
  24 threads, where the parallel speed-up grew from 3 to 7 under the workstation garbage collector.

Altogether, the median of three runs on 24 threads: the steel frame, preparing and checking, 9.7 s to
2.0 s under the workstation garbage collector and 7.3 s to 0.65 s under the server one; the bar mesh
3.0 s to 0.04 s; 2000 boxes 39 ms to 3 ms. Every pair and kind of clash found is the same; the gaps of
31 bolts in their holes are now right (see FIXED), and the rest agree with volumes found by counting
random points and gaps found by weighing every pair of triangles.

### FIXED. Found while 6.0.0 was being published, and released in it.

**FIXED.** A union or a difference with a bent bar in it could come out open, and a bar of a few hundred faces
took too long to take part at all. Both bodies were cut by every face plane of both, and every plane of a bend runs
on through the rest of the bar: thousands of cells, glued back together with faces that did not quite meet. They
now cut one body only, as the intersection does, by the planes of the other that come near it: a union keeps the
part of it beyond the other and the other whole, a difference keeps what is left of the body it cuts, or, cutting
the body taken away, the other whole less the part within it. Where the whole body meets the cells their faces
cancel by the area they share. Each body's openings are cut into it first, so the result still carries no openings
of its own. On 1,687 random pairs of bent bars, stars, Ls and Is the eight results that came out open are closed,
the 173 pairs with too many faces to try are now tried and come out right, and the scan runs in 20 s instead of
50 s; a hook of 200 faces through a plate unites and subtracts in well under a second. One union and one
difference in the scan now differ from the volume identities by three and two parts in a million, a third of a
cubic unit on a hundred thousand, within what taking faces out of each other in the plane rounds to.

**FIXED.** A cell the planes before had left in two pieces, the two ends of a bent bar with its middle taken
out, was reported as crossed and left whole by a plane passing between the pieces, which crosses neither. The
cut was then judged unclean, and an intersection cut the other body, or a union the old way. Each piece is now
sorted to its own side of such a plane, or cut.

**FIXED.** A face holds its holes wound the same way as its boundary, whichever way they are given. That is how
the library measures them, by plain subtraction, but the plane winds a hole against its boundary, and faces lifted
from it kept that winding: the booleans of flat shapes, the offsets of a face, anything laid out in a plane and put
back. A body built of such faces added its holes to its volume instead of taking them away: a plate ten by ten by
one with a two by two hole through it measured 98.67 instead of 96.

**FIXED.** GeoSolid3.Centroid left out the holes in a body's faces, so a plate with its bolt holes cut in had its
centroid where the plate without them has it: 49.78 instead of 49.65 for a hundred-square plate with a ten by ten
hole near one corner.

## 5.1.0

Rounding a corner no longer means rounding every corner by the same radius.

- Fillet takes a list of radii, one per vertex, read the way the bulges are read: the entry at an
  index belongs to the vertex at that index. A zero leaves that corner alone, and a list shorter
  than the shape leaves the rest of it alone. A plate wanting forty at one corner, ten at the next
  and a square corner after that is now one call.
- TryFilletAt rounds one named corner and reports whether it had room, as TryChamferAt cuts one.
- Where two neighbouring corners together ask for more of the edge between them than it is long,
  the one taking more of it gives way. That rule was always there; with a radius per corner it
  earns its keep, because a corner asking for 250 of a 300 edge yields to one asking for 20 rather
  than winning by being reached first.
- Everything rounding already knew still holds: a corner against a curve is rounded against it, and
  an arc cut back keeps the circle it was cut from rather than being straightened.

Nothing was removed or changed, so 5.0.0 code builds against this unaltered.

## 5.0.0

One package instead of four. GeometryHelper.CommonGeometry, GeometryHelper.PlaneGeometry,
GeometryHelper.SolidGeometry and GeometryHelper.ArrangeAlgorithms are now GeometryHelper, one
assembly. Keeping the two dimensions apart cost a machinery of shared source files and internal
copies, and it stopped a shape in space from being handed to the plane algorithms at all.

### WHAT TO CHANGE

- Replace the four PackageReference entries with one on GeometryHelper.
- The namespaces are shorter, and every type kept its name:

  ```
  GeometryHelper.CommonGeometry, .Datatype          -> GeometryHelper
  GeometryHelper.CommonGeometry.Enums               -> GeometryHelper.Enums
  GeometryHelper.PlaneGeometry.Geometry, .Solid...  -> GeometryHelper.Geometry
  GeometryHelper.PlaneGeometry.Core, .Solid...Core  -> GeometryHelper.Core
  GeometryHelper.SolidGeometry.Spatial              -> GeometryHelper.Spatial
  the three .Extension namespaces                   -> GeometryHelper.Extension
  GeometryHelper.ArrangeAlgorithms                  -> GeometryHelper.Arranging
  ```

  A program that worked in both dimensions imported two namespaces for shapes and two for
  operations; it now imports one of each, and nothing collides, because every type already
  carried a 2 or a 3.

### WHAT IS THE SAME

- Every type, every member, every overload and every tolerance rule. The merge moved code; it
  did not change an answer: the 1,836 tests the four suites carried before it pass unchanged.
  The four suites were merged as well, into one GeometryHelper.UnitTest holding a folder per
  area, so one run covers the package.
- Tolerance.Global is still one process-wide setting, and it now genuinely cannot be two.
- The plane half still resolves regions with Clipper2 (Boost Software License), which remains
  this package's only dependency; the solid half still uses its own winding-number solver, and
  the suites still check the two against each other.

### CUTTING CORNERS, AND CIRCLES AS POLYGONS

- Corner2 chamfers a corner: one straight cut across it, measured back along each of the two edges
  that meet there, as AutoCAD's CHAMFER does. Chamfer on a polygon gives a polygon and on a chain
  gives a chain, because cutting a corner square adds no curvature, so the result goes straight on
  into the region operations with nothing to convert. A chain keeps both of its end points.
  TryChamferAt cuts one named corner and reports whether it had room.
- A corner is left alone when its cut is longer than an edge beside it, when two neighbours
  together ask for more than the edge between them is long (the one taking more of that edge is
  dropped, which may leave room for the rest), or when the corner is straighter than
  Tolerance.EqualAngleRad. What was skipped is written to GeometryHelperLog. Every cut is measured
  on the shape as it came in, so the answer does not depend on which vertex the walk began at.
- GeoCircle2, GeoCircle3, GeoArc2 and GeoArc3 cut themselves into straight pieces three ways: ToPolygon() takes the
  automatic tolerance of 0.2 % of the radius, about fifty edges; ToPolygonByChordTolerance keeps
  every edge within a distance of the circle; ToPolygonBySpacing puts no two vertices further apart
  than asked along the circumference, spread evenly with no short edge left at the end; and
  ToPolygon(count) gives exactly that many. ToPolyline does the same as an open chain with its
  first point repeated at the end, and an arc gives a chain rather than a loop because it does not
  close. The vertices lie on the circle, so the polygon is inscribed and encloses about 0.26 %
  less at the automatic tolerance. GeoCircle2 had none of this; GeoCircle3 had only the count.
- GeometryHelper.CadConvert now says so when an AutoCAD polyline carrying arcs is read as points:
  the bulges were always dropped silently, which straightens the shape rather than rounding it.

### ARCS

The plane had no arc. A drawing is full of them, and everything that read one had to straighten it
first, so a slot came back a rectangle and a rounded plate came back square.

- GeoArc2 is a piece of a circle: a centre, a radius, the angle it starts at and the angle it
  sweeps. The sweep is signed, so the arc knows which way round it goes and a half turn is told
  from the rest of the circle left behind. FromThreePoints builds one through three points and
  FromBulge from the number AutoCAD stores, and Bulge reads that number back, so a round trip
  through a drawing is exact.
- Core.Arc2 holds the operations, mirroring the ones for a segment: ProjectToArc, DistanceTo to a
  point, a segment or another arc, IsPointOn, Locate, TryIntersectWith and GetIntersections,
  TrySplitAt by parameter or by point. Translate, RotateBy and TransformBy move one; a
  transformation that would make it an ellipse is refused rather than averaged, as elsewhere.
- GeoArc3 is the same arc in its own plane, carrying a Normal. Its GetAabb is the box round the
  arc itself rather than round the whole circle, and ProjectToArc2 brings it into the plane.
- GeoArc2, GeoArc3, GeoCircle2 and GeoCircle3 all cut themselves into straight pieces the same
  four ways, described below.

### CHAINS THAT CURVE

A GeoPolyline2 is straight by definition, and widening it would have made every shape in the
library pay for arcs it does not have. The chains that may curve are their own types instead.

- GeoEdge2 is one piece of a chain: two ends and a bulge, a straight segment until the bulge is
  not zero. It measures along its arc, gives its chord either way, and refuses ToLine when it
  curves and ToArc when it does not.
- GeoPolylineArc2 is the open chain and GeoPolygonArc2 the closed loop, laid out the way a drawing
  holds them: vertices, with the bulge of the edge leaving each one. Both build from a straight
  GeoPolyline2 or GeoPolygon2 without losing anything, reverse with every bulge changing sign, and
  compare two ways - Equals exactly and from the same starting vertex, IsEqualTo within a
  tolerance and, for a loop, whatever vertex it starts at.
- GeoEdge2, GeoPolylineArc2 and GeoPolygonArc2 carry Translate, RotateBy and TransformBy like every
  other shape of the plane. A bulge measures an arc against its own chord, so moving, turning and
  scaling evenly leave it alone; mirroring changes its sign, and an uneven scaling is refused
  because the arc would be part of an ellipse.
- GeoPolygonArc2 gives Area, SignedArea and IsClockwise exactly, counting the piece each arc adds
  beyond its chord. It answers nothing else about what lies inside it: flatten it first.
- A chain that may curve answers everything a straight one does, and answers it on the arcs:
  DistanceTo, Locate, Contains, IsPointOn, GetClosestPointOnBoundary, GetIntersections,
  CollidesWith, TrySplitBy, TrySplitAtDistance, SplitAtDistances, TryChamferAt, the whole
  GetPointAtParameter family, and for a loop Centroid and IsSimple. They live in the Core classes
  beside their straight counterparts - Distance2, Containment2, Intersection2, Collision2,
  Projection2, Parametrization2, Splition2 - and are mirrored on the types themselves.
- Locate on a curved loop is exact with no arc cut up anywhere in it: the straight loop through the
  vertices, turned inside out once for every piece an arc cuts off its own chord that the point
  lies in. That covers an arc bulging out, one bulging in, and one sweeping more than half a turn.
- A cut inside an arc leaves two arcs of the same radius rather than two chords, so the pieces put
  back end to end draw what went in. Cutting a loop gives open chains, and the run after the last
  cut carries on through the vertex the loop happened to be held from.
- Offset keeps the arcs, and is the one region operation that does not go through Clipper. It can
  be done piece by piece - a segment moves sideways, an arc keeps its centre and changes its radius
  by the same amount - so a fillet of R40 offset by 10 comes back R50 exactly. A corner that closes
  up is trimmed to where the moved pieces cross; one that opens is bridged as OffsetJoin says, and
  Round bridges it with a true arc. What tells the folded parts from the real ones is the one thing
  an offset cannot break: every point of a valid offset stands exactly the offset distance from the
  shape it came from, and anything nearer has been folded over. The suite checks it against the
  straight offset through Clipper, which agrees to a part in a hundred thousand.
- The four boolean operations do need flattening, because a boolean cannot be done piece by piece.
  They take an optional chord tolerance and hand back GeoFace2, which says plainly that the arcs
  are gone.
- Miter, the default join, runs both pieces on until they meet, an arc reaching further round its
  own circle rather than being cut across, which is what AutoCAD's OFFSET does at a corner. Where
  the two would never meet, or meet further away than OffsetOptions.MiterLimit allows, it cuts
  straight across instead.
- GeoCoordinateSystem2 is the local coordinate system of the plane, the counterpart of
  GeoCoordinateSystem3 in space: an origin and two orthonormal axes, with ToLocal and ToGlobal for
  points and vectors and ToTransform to hand it over as a GeoTransform2. A transformation could
  already say the same thing, but it may also scale, mirror or shear, and reading one backwards
  means inverting a matrix; a frame is rigid by construction and reads backwards by turning the
  axes round. GeoRectangle2, the rotated rectangle, now carries one and can be built from one, as
  GeoObb3 already did in space.
- GeoPolygon2 and GeoPolygon3 gained Reverse, which the chains already had. Which way a loop runs
  is what tells its inside from its outside to anything reading winding, and it decides which way
  round a pair of chamfer distances goes.
- GeoBvh2 is the counterpart of GeoBvh3 in the plane: a bounding volume hierarchy over GeoEdge2, so
  one index serves a straight chain and a curved one alike, with the arcs held as arcs. It answers
  the nearest point, the distance to a point or to another index, where a segment crosses, and
  whether two sets of edges meet. It measures to the edges rather than to the region they enclose.
- Coverage can be measured on demand with coverlet; see the README for the command. It stands at
  about 90 % of lines and 85 % of branches.
- Flatten() turns a curved chain into the straight one the region operations read, cutting each arc
  within 0.2 % of its radius, and Flatten(chordTolerance) within a distance you name. The chords
  lie inside the arc, so a shape bulging outward encloses a little less once flattened and one
  bulging inward a little more. Clipper, which resolves the booleans and the region offsets, knows
  only straight edges, so this is the door arcs stop at - and it is a conversion you make, not one
  the library makes quietly behind you.

### ROUNDING CORNERS

- Corner2.Fillet replaces a corner with an arc tangent to both edges, as AutoCAD's FILLET does.
  Rounding creates curvature, so unlike Chamfer it cannot give back the kind of shape that went in:
  it works on GeoPolylineArc2 and GeoPolygonArc2, and a rectangle filleted at every corner comes
  back eight edges. A chain keeps both of its end points where they were.
- Fillet also takes one radius per corner, as a list read the way the bulges are: the entry at an
  index belongs to the vertex at that index, and a zero leaves that corner alone. TryFilletAt
  rounds one named corner on its own, as TryChamferAt cuts one. Where two neighbours together ask
  for more than the edge between them is long, the one taking more of it gives way, so a corner
  asking for a large radius yields to a small one rather than the other way round.
- Lengthen2.TryFilletCorner does the single corner between two segments and hands back the arc and
  both trimmed segments. It finds the corner by extending the two, so they need not already meet,
  and the order they are passed in does not change the answer.
- Exactly enough is enough: a square of side one hundred filleted at fifty loses every straight
  edge and comes back four quarter turns, which is a circle, and chamfered at fifty it comes back
  the diamond through the four midpoints. Both used to lose a corner to the dust left by measuring
  an edge, and a fillet that consumed a whole edge used to lose the next arc its bulge.
- A corner is left alone for the reasons a chamfer is, and for one more: when either edge is
  already an arc. A circle tangent to two curves has several answers and picking one is not this
  method's business. What was skipped, and why, is written to GeometryHelperLog.

### GEOTRANSFORM2

The plane had no transformation at all: shapes could be translated and rotated and nothing else.
GeoTransform2 is the 3x3 homogeneous matrix that fills the hole, built and read exactly as
GeoTransform3 is, and applied on the left so that a.Multiply(b) means "apply b, then a".

- Translation, Rotation about the origin or a point, Scaling uniform, per axis or about a point,
  Mirror across the line a segment carries, and FromFrame, which places geometry built about the
  origin and whose inverse reads a placed drawing back into local coordinates.
- Multiply and the * operator combine them; GetDeterminant is the factor areas are multiplied by,
  negative when the transformation reverses winding; Inverse and TryGetInverse undo one, judging
  the determinant against the size of the transformation rather than against zero, so a drawing
  scaled down by a thousandth still inverts cleanly.
- Every shape of the plane carries TransformBy: points, vectors, segments, chains, polygons, faces
  with their holes, circles and rectangles. A circle under a scaling that differs between the axes
  would be an ellipse and a rectangle would be a parallelogram; both are refused rather than
  answered with an averaged shape, as GeoTransform3 already refuses the same of a circle.

### THE PLANE HALF CATCHES UP WITH THE SOLID ONE

- GeoPolygon2 reports its measurements as properties, as every other shape in the library does:
  GetArea(), GetSignedArea(), IsClockwise() and GetCentroid() are now Area, SignedArea,
  IsClockwise and Centroid. GeoCircle2.Circumference is now Length, the name every other curve
  uses. Both are renames; nothing about the answers changed.
- GeoRectangle2 gained Area, which it had no way to report at all, and GeoPoint2 gained Origin.
  GeoFace2 and GeoFace3 gained Centroid, the boundary's with the holes taken out.
- Containment2.GetSide says which side of a segment a point lies on, through the new LineSide
  (Left, Right, On) — the counterpart in the plane of PlaneSide in space — and is mirrored by
  GeoPoint2.GetSideOf. Parallel2.IsCodirectional tells the two ways along a line apart, mirrored
  by GeoVector2.IsCodirectionalTo. Projection2.ProjectToInfiniteLine is ProjectToLine without the
  clamp to the segment.
- GeoPolygon2, GeoPolyline2 and GeoFace2 gained IsEqualTo, the tolerance-aware comparison their
  solid counterparts already had. A polygon is matched whatever vertex it starts at, a chain from
  its start because a chain has ends, and a face hole for hole in whatever order they are held.
- Merge2.JoinBackup is internal now. It was always the plain reading of joining kept to hold the
  fast one against, never meant for drawings, and it no longer sits in the public surface.

### WHAT THIS OPENS

- PlanarMap carries flat geometry between the dimensions: a plate, a face or a chain lying in a
  plane in space is laid out in two dimensions through a GeoCoordinateSystem3, worked on with the
  whole 2D half of the library - offsetting, the booleans, splitting, containment with holes -
  and put back where it came from. GetFrame on a polygon or a face gives a frame that turns with
  the shape, so its plan view is the same drawing wherever it sits in the model. Flattening drops
  the local Z, so ProjectTo... projects a point that is off the plane and TryToPoint2 refuses it.
- Internally the same merge retired the plane-and-vector struct the offset engine used to carry,
  which existed only because the two halves could not see each other's types. The engine now
  works in GeoPoint2 and GeoVector2 like everything else.
