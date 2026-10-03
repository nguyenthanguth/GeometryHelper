# Changelog

The release notes of the GeometryHelper package in full, newest first. The package carries the notes of its own
release only, and a link here for the rest. GeometryHelper.IfcConvert,
GeometryHelper.TeklaConvert and GeometryHelper.CadConvert carry their own notes in their packages.

## Unreleased

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
