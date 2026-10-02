# Meshing in space: cases and checks

What [meshing in space](mesh3.md) does, case by case, drawn and measured, and how it was checked: the tests, the random
shapes and bodies it was held to, the faults those found, and what is left as a limit. Every number here comes from the
code as it is; the cases are the examples of the guide, which run as tests.

The pictures look at the scene from the front right, above. A whole cell is blue and a cut one orange, in three shades by
which way a face looks; the triangles of a mesh take the palette of [the plane](mesh.md). Cells are drawn moved apart from
the middle of their grid, so that each shows.

## Flat shapes

| Case | Shape | Options | Faces | Whole | Checked |
|---|---|---|---|---|---|
| A wall standing in space | a face 7215 by 3012 in the plane y = 0, a door and a window | `Grid(1200, 600, joint: 3)` | 29 | 13 | the same panels as its elevation in the plane |
| A gable roof | two sides 6000 by 3000, 30 degrees up | `Grid(400, 300)` | 150 + 150 | all | rows level, the second axis up each slope |
| A plate lying askew | a rectangle 2400 by 1200 turned about two axes | `Grid(600, 400)`, `World` and `Own` | 21 and 12 | 3 and 12 | `Own` runs along its sides whichever corner its loop starts at |
| Two walls at a corner | 5000 and 4000 long, 2900 high | `Grid(1200, 600, joint: 10)`, one origin | 30 and 24 | 12 and 8 | the rows stand at the same heights on both |
| A disc on a slope | radius 1500, its normal (1, -1, 2) | `Triangles`, `Grid(400, 400)` centred | 50 and 61 | 29 | the vertices on the disc's plane, within its rim |
| A slot standing up | 2400 by 600, round ends | `Strips`, `Convex` | 25 and 1 | | the slot's corners are vertices exactly |

![A wall standing in space with a door and a window, in panels of 1200 by 600 with joints of 3](images/mesh3/wall.svg)

The wall is laid out in a frame of its plane, its first axis level along it and its second up it, as its elevation is
drawn, and meshed there as the plane meshes it: the same 29 panels and the same 13 whole ones, put back on the wall.

![Tiles on the two sides of a gable roof in level rows up the slope](images/mesh3/roof.svg)

![A plate lying askew: on the left its grid runs level, on the right along its own sides](images/mesh3/plate-own.svg)

The plate's sides are not level, so a level grid crosses it, 21 faces and only 3 of them whole. Along its own sides it
divides into its 12 cells.

![Two walls meeting at a corner, laid from one origin](images/mesh3/walls-origin.svg)

![A disc fanned into triangles, and in cells of 400](images/mesh3/disc.svg)

![A slot in strips, and as one convex piece](images/mesh3/slot.svg)

## Bodies in cells

| Case | Body | Options | Cells | Whole | Volume held |
|---|---|---|---|---|---|
| A box | 1000 by 600 by 400 | `Grid(300, 300, 300)` | 16 | 6 | 240 000 000, all of it |
| An L-shaped footing | 2000 square, 800 high, its step at 1000.005 | `Grid(500, 500, 400)` | 24 | 24 | 2 400 004 000, all of it |
| A step 30 past a line | the same footing, its step at 1030 | `Grid(500, 500, 0)`, snap of the tolerance and of 50 | 14 and 12 | 12 and 6 | all of it |
| A U | 3000 by 2000, 500 high | `Grid(0, 1000, 0)` | 3, two of them pieces of one cell | 0 | 2 400 000 000, all of it |
| A slab with a shaft | 12000 by 8000 by 250, a shaft 1500 by 1200 | `Grid(6000, 4000, 0)` | 4 | 0 | 23 550 000 000, the net volume |
| A cylinder | radius 600, 2000 high, 48 sides | `Grid(300, 300, 500)` | 64 | 16 | 2 255 492 601.6, all of it |
| A wall turned in plan | 6000 by 200 by 3000, turned 30 degrees | `Grid(1000, 0, 1000)`, `Upright` and `World` | 18 and 18 | 18 and 0 | all of it |
| A wall of blocks | 4000 by 190 by 2400 | blocks of 390 by 190, joints of 10 | 120 | 120 | 1 689 480 000, less the joints |
| A column in lifts | 400 square, 3500 high | `Layers(1000)` | 4 | 3 | 1000, 1000, 1000 and 500 high |
| A box turned in space | 2000 by 1000 by 600 | `Grid(500, 500, 300)`, `Own` and `World` | 16 and 54 | 16 and 2 | 1 200 000 000, all of it |

![A box in cells of 300](images/mesh3/box-cells.svg)

![An L-shaped footing in cells of 500 by 500 by 400](images/mesh3/footing.svg)

The footing's step stands five thousandths past the line between two columns of cells. The cut along that line snaps onto
the step, so that no slice five thousandths thick is cut off: every one of the 24 cells is whole, the cells beside the step
500.005 long and the ones past it 499.995.

![A step 30 past a line: a cell of its own with the point tolerance, left; with the cell beside with a snap distance of 50, right](images/mesh3/snap.svg)

With the step 30 past the line, the point tolerance leaves the slice 30 thick as two cells of their own, on the left. A snap
distance of 50 moves the cut onto the step, through the whole footing: the slice goes with the cells beside, and the cells
either side of the cut, 530 and 470 long, are no longer whole, on the right.

![A U whose row over the arms holds two pieces](images/mesh3/u-pieces.svg)

The row over the arms holds two pieces of one cell, (0, 1, 0) pieces 0 and 1. The base meets both arms, and the arms do
not meet each other: the neighbours of cell 0 are cells 1 and 2, and each arm's only neighbour is cell 0.

![A slab with a shaft in four pour bays](images/mesh3/slab-bays.svg)

The shaft is cut in first, so each bay is cut to it; together they hold the slab's net volume to the cubic millimetre.
Bay 0 meets bays 1 and 2 across their shared sides, and bay 3 meets bays 1 and 2.

![A cylinder in cells of 300 by 300 by 500](images/mesh3/cylinder.svg)

![A wall turned 30 degrees in plan, cut upright along itself and along the world's axes](images/mesh3/wall-upright.svg)

![A wall of blocks 390 by 190 with joints of 10](images/mesh3/masonry.svg)

![A column poured in lifts of 1000](images/mesh3/lifts.svg)

![A box turned in space, along its own axes and along the world's](images/mesh3/turned-box.svg)

## How it was checked

**Tests.** 31 tests of flat shapes in space, 39 of cells, 11 that run the guide's examples, and 626 that replay the fuzz
below: the 26 cases that once found a fault and 600 others. A flat mesh is held, laid out in its frame, to every promise
of the meshes of the plane: faces simple, counter-clockwise and edge to edge, the material covered once and nothing else;
and in space to its own: vertices on the plane, the shape's corners exactly, a side's points on the side in space. A grid
of cells is held to closed cells of the volumes they say, within their boxes, in order, points of the body in one cell and
none outside it, and none of the cutting warned of.

**Random shapes and bodies.** A port of the fuzz runs as the replay tests above. The flat shapes are stars of 3 to 12
corners with up to two holes, laid on random planes near the origin and seven kilometres out, a quarter of them a hair out
of flat, meshed in every kind, by grids of random cells, joints, angles and alignments, placed every way and from origins
near and far. The bodies are prisms of stars with holes, unions and differences of boxes, cylinders of 8 to 48 sides,
slabs with openings, L shapes whose step stands a hair to either side of a line of cells, and U shapes, turned and moved
the same ways, cut by random sizes, counts, whole axes, joints and snap distances along every kind of axes.

| Run | Cases | Failed |
|---|---|---|
| Flat shapes, the final runs | 300 000 | none |
| Bodies, the final run | 200 000 | none |
| Bodies, all runs | 483 000 | 23, every one fixed and replayed as a test |

Across the bodies cut without joints, the cells' volumes add up to the body's to a median of two parts in ten million
million; 99.9 per cent within seven parts in a million million; and two parts in a million at the most, where a face of the
body stood within the tolerance of a cut and the wedge between went with it.

**Mutations.** 43 deliberate faults were put into the new code one at a time, and the 7 fixes below undone one at a time:
46 of the 50 made a test fail. The 4 left change nothing a test can see: two take away a defence, the turn of a loop with
arcs whose plane points the other way and the check that neighbouring cells were cut by the same plane; one measures a
face's area by the length of its area vector rather than along the normal, which is the same for a flat face; and one moves
a grid's origin by one more whole cell, which gives the same grid.

## Faults found on the way

The random bodies found five faults in what was there before, each now fixed with a test that failed without it.

| Fault | Where | What it did |
|---|---|---|
| A hole a hair off its face's plane read in its own plane | `GeoFace3.Locate`, `Contains` | a point in the hole was inside the material |
| A plane parting a body without crossing it refused | `GeoSolid3.TrySplitBy` | a cut between the two arms of a U, or along the edge where two parts of a body meet, was taken for a plane that misses the body |
| A ray grazing the rim of a hole counted | `GeoSolid3.Locate`, `Contains` | a point of a plate whose first ray rose through a hole a hair short of its rim came out of the plate |
| A hole with every corner on the rim taken for material | `GeoFace3.TrySplitBy`, `GeoSolid3.TrySplitBy` | a cut through a hole touching its face's boundary gave the hole back as a face; a cell of a plate held 49 000 cm3 more than the plate |
| A face of a hull smaller than a polygon of the tolerance | `ConvexHull3`, `GeoObb3.Fit` | three corners a tenth of a millimetre apart made the hull throw |

The same runs shaped the cutting itself, in two more of the seven fixes: no cut takes off less than four point
tolerances, and the point of a needle a cut cannot take off stays with the rest of it. Before the cells were committed they
had also made the joints be cut out whatever the snap distance.

## Limits

- The volumes of the cells add up to the body's but for the tolerance times the area cut, as above.
- A cell keeps the point of a needle a cut could not take off: a part thinner across than the point tolerance where the cut
  meets it, standing up to a hundred point tolerances past the cell's box.
- The cells are bodies of their own. Neighbours meet on the planes between them, but a vertex of one does not have to be a
  vertex of the other: the grid is not a conforming mesh of the kind a finite element solver reads.
- Only flat shapes have a surface mesh. The faces of a body are not laid out in panels together, and a body is not broken
  into tetrahedra.
