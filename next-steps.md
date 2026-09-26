# Next steps

What is being worked on, as of 27 September 2026. Nothing finished is described here: the git log has that.

## Solid against solid: openings, pieces, contact

The user asked for a way to test whether one `GeoSolid3` meets another and get the shared region back. That
already exists — `a.TryIntersect(b, out GeoSolid3 shared)` — and probing it turned up three things that stop
it from being enough for a clash check between Tekla parts.

### What the probe found

| Case | `CollidesWith` | `TryIntersect` | Verdict |
|---|---|---|---|
| Two boxes overlapping, a box inside a box, a turned box | true | true, exact volume | right |
| A plate with a bolt hole against a block over the hole | true | true, 16 000 − 4 000 | right |
| Touching on a face, an edge or a corner only | true | false | right for a hard clash; no contact region exists |
| A bar across two separate blocks | true | true, one solid of 12 faces | right volume, but the two regions cannot be told apart |
| **A pin through a bolt hole, 5 clear of every wall** | **true** | false | **wrong — a false clash** |

The last row is not one bug but a family. Probing every query against a probe lying wholly inside the hole:

| Query | Wrong for |
|---|---|
| `CollidesWith` | segment, ray, polyline, both boxes, another solid |
| `DistanceTo` | segment, ray, polyline, triangle, polygon, both boxes, another solid |
| `GetIntersections` | segment, ray |
| `GetShortestLineTo` | segment, another solid |
| right already | a point, `Locate`, `Contains`, polygon / face / arc collision, `TryIntersect` |

**The cause is one line of documentation.** `GeoSolid3.Triangulate` says the openings are not meshed — they
are whole bodies subtracted from this one — and in the same remark says clash detection, ray casting and
body-to-body distance read this mesh as the boundary of the solid. Both cannot hold. Fifteen call sites in
`Core` read `solid.Triangulate()` as the boundary; the queries that come out right are exactly the ones that
read `solid.Openings` themselves. A plate's top face is a whole square, because the hole is an opening body
and not a hole cut in the face, so a pin through the hole crosses that square and is reported as touching.

For a clash check between Tekla parts this means **every bolted connection is reported as a clash.**

### Found on the way, and fixed first

Writing the tests for A1 turned up three more, each older than this work and each in the way of it.

- **The booleans judged a cell in two pieces by one point.** A plane lying along the wall of a hole does not
  cut the strip either side of it, so that strip came out as one cell in two pieces and a single point
  decided for both. Subtracting a box overlapping an existing hole threw away material nowhere near the box —
  168 000 where it is 180 000 — and `GetNetVolume` inherited it. `Core.Shells3` now separates every cell into
  the pieces that do not touch before it is judged. **Done**, `a0f2563`. It is also the heart of B.
- **`Locate` asked the faces before the openings**, so a point in the middle of a bolt hole at the level of
  the top face was boundary, and an opening's wall standing out past the face, where a through-hole
  overshoots, was boundary too. The openings are asked first now.
- **The nearest point was sought on the faces and on each opening in turn**, then kept only where the body
  called it boundary. That cannot find the rim of a hole — a point of neither alone — so a point below a duct
  came out nought away, and `DistanceTo`, `SignedDistanceTo` and `GetClosestPointOnBoundary` of a point were
  all wrong near an opening. They agreed with the old `Locate`, which is why nothing caught it. Now the
  openings within reach are cut in and the answer is read off the material; an opening farther away than
  the answer found without it cannot change it, so a point far from every hole cuts nothing.

### The plan, in order — each step built, tested and committed before the next

**A1. Cut the openings into the body.** — **Done**, with the three above. `Boolean3.TryCutOpenings(solid, out material)` turns a body with
openings into one without, whose faces are exactly the surface of the material: the outer faces with the
parts over each opening removed, and the walls of each opening where they run through the body. Exposed as
`GeoSolid3.TryCutOpenings(out GeoSolid3 material)` and `GeoSolid3.TriangulateSurface()` — the same name
`GeoFace3` already uses for the mesh that stands for material.

- Reuses the boolean machinery: split the gross body into cells, keep the cells that are material
  (`Locate` already honours openings, nested ones included), glue.
- **Each opening only cuts the cells near it.** Its face planes are infinite, and slicing the whole body by
  them turns a plate with twenty bolt holes into a grid of a thousand cells. A plane from one opening only
  needs to cut the cells whose box meets that opening's box; every other cell is wholly outside it already.
- `false` when the openings leave nothing, which is an outcome and not an error, as for the booleans.
- `Triangulate` keeps its meaning — the faces as they are — and its remark is corrected, so nobody reads it as
  the boundary again. `Volume` does not move: it never used `Triangulate`.

**A2. Make every query read the material.** — **Done.** Each broken query takes the body through A1 first.

- `CollidesWith` and `GetIntersections` need only the openings whose box meets the probe's box. That is
  exact — an opening out of the probe's reach removes nothing the probe could touch — and it keeps a bolt
  against a plate with twenty holes down to cutting one.
- `DistanceTo` and `GetShortestLineTo` need them all: the place of the body nearest a probe can lie inside an
  opening the probe's box never meets, and the true answer is then on that opening's rim.
- `GeoBvh3.FromSolid` and `BuildIndex` index the material too, so a ray down a bolt hole passes clean
  through.
- A caller running many checks against one body cuts it once with `TryCutOpenings` and asks the result, as
  with `BuildIndex`: no hidden cache, the shape stays a value.
- **The test is the audit table above**, for every query and probe, both ways round, plus a fat pin that
  really does bite the plate still reported as a clash, plus every existing test untouched.

**B. One region per clash.** `GeoSolid3.Intersect(GeoSolid3 other)` → `GeoSolid3[]`, one entry per separate
region, empty when they share no volume — the same shape of answer `GeoPolygon3.Intersect` already gives in
the plane. And `GeoSolid3.SplitShells()` → `GeoSolid3[]`, the separate pieces of any body.

- **Pieces are found among the cells, not among the faces.** Two kept cells belong to one piece when they
  share a face, which is the same exact test `TryGlue` already uses to drop interior walls — so no edge has
  to be matched to a neighbour's, and a T-junction left by merging coplanar faces cannot split one piece in
  two. Two pieces touching only along an edge or at a corner share no face, so they stay two: right, since
  they share no volume.
- Not an overload of `TryIntersect`: an `out GeoSolid3[]` beside the `out GeoSolid3` one would make every
  existing call with `out _` ambiguous, which is the trap `GeoLine3.TryIntersectWith` walked into.

**C. What touches, when nothing overlaps.** `GeoSolid3.TryGetContact(GeoSolid3 other, out GeoFace3[] contact)`
— the patches where the two surfaces lie against each other, face to face.

- Two faces are in contact where they are coplanar with **opposite** outward normals; the patch is their
  shared area, which the coplanar lift for `GeoFace3` already computes exactly.
- Both bodies go through A1 first, so a column standing over a bolt hole in a base plate does not report the
  hole as bearing.
- Only contact with area is a face. Two bodies meeting along an edge or at a point have no patch, and
  `CollidesWith` is what reports them; the return type says so rather than inventing a zero-area face.

**D. Write it down.** A clash-check recipe in `solid.md`, with a `[Fact]` for every snippet: a box test to
rule out most pairs, `CollidesWith`, then `Intersect` for the regions and their `Volume`, and
`TryCutOpenings` once per body when it is checked many times. The release notes, and this page cleared.

### Not in this plan

- An `out GeoSolid3[]` form of `TryUnion` and `TrySubtract`. The cell grouping from B would serve them for
  free, but nothing asks for them yet.
- Edge and point contact as geometry. They would need a segment and a point type in the answer.
