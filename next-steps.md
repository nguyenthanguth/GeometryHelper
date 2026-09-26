# Next steps

What is still to do, as of 26 September 2026. The version is **6.0.0**; nuget.org is at 5.1.0.

Nothing finished is described here. The git log has that, and `PackageReleaseNotes` in
`src/GeometryHelper/GeometryHelper.csproj` carries what the release body should say. What is kept below is
the work ahead, and the decisions that should shape it rather than be argued again.

## 1. Release 6.0.0

`Directory.Build.props` is at **6.0.0** — a major number because 6.0.0 renames a published API and changes
how an arc decides what it reaches. It is the one literal every package reads, including the Tekla ones
through `$(GeometryHelperVersion).$(TeklaVersion)`.

**Create a GitHub Release tagged `v6.0.0`**: `.github/workflows/release.yml` fires on `release: published`
and publishes all six packages. The notes are already written under **NEW IN 6.0.0**; copy that text into
the release body.

Do not use *Run workflow* on that workflow to try it out. It has no dry run, and the two push steps are not
guarded by the event type, so a manual run publishes to nuget.org and GitHub Packages for real.

## 2. The guides do not describe what the library does

`src/GeometryHelper/docs/` — `common.md`, `plane.md`, `solid.md`, `arrange.md` — was last touched before the
six passes that filled in the measuring surface, and several hundred methods are nowhere in it. Two sentences
are now wrong by omission rather than merely thin:

- `plane.md`, under *How deep inside, not just whether*: signed distance is listed as offered by the shapes
  that enclose an area. A `GeoPoint2` can be asked as well now.
- `solid.md`, under *How deep inside, not just whether*: the same, "for the three shapes that enclose a
  volume". A `GeoPoint3` can be asked too. And the `slab.CollidesWith(...)` list a few lines above it leaves
  out `aabb`.

**Every snippet printed in a guide is executed** by `tests/.../Plane/ReadmeExamplesTests.cs` and its Solid
twin, which check the numbers the prose claims. A new section is not finished until it has a matching
`[Fact]` there. That convention is why the guides can be trusted, and it is the reason writing them is work
rather than typing.

## 3. Space is far behind the plane

The plane is complete: every shape answers about every other, both ways round. Space is not, and the gap is
widest exactly where a reinforcing bar lives. `GeoArc3`, `GeoCircle3`, `GeoEdge3`, `GeoPolygonArc3` and
`GeoPolylineArc3` have, between them, **no crossings, no collisions, no joining segment and no cutting at
all**. They answer about a point, they move, an arc, a circle and a closed loop can be offset, and a chain
can be filleted. That is the whole of it.

Count directions rather than methods when reading what follows, and count them from the source rather than
from this page — the section below was first written from a listing cut short at forty entries, and claimed
`GeoPolyline3` could not be cut when in fact it can be cut twelve ways.

### 3.1 What can be lifted or dispatched, with no new arithmetic

This is the same work done twice in the plane, and it should be done first because it is exact and cheap.

| Type | What it could answer | How |
|---|---|---|
| `GeoPolygonArc3` | everything `GeoPolygonArc2` answers, against any shape sharing its plane | it **enforces coplanarity**, so project to `GeoPolygonArc2`, answer there, lift back — exactly as `Area`, `Locate`, `Offset` and `Fillet` already do |
| `GeoPolylineArc3` | the same, when `IsPlanar()` is true | `TryGetPlane` already decides it; off-plane must refuse rather than approximate |
| `GeoEdge3` | everything `GeoArc3` and `GeoLine3` both answer | `IsArc ? ToArc().X(…) : ToLine().X(…)`, and the reverse on every other type — the pattern `GeoEdge2` now follows, including the rule that a direction is offered only where both answer with the **same shape of call** |

### 3.2 Crossings in space have closed forms and are simply missing

`Core` holds **nothing** for an arc or a circle in space beyond `Distance3.DistanceTo(GeoCircle3, GeoPoint3)`.
Crossings were never refused — they were never written, and each has an exact answer:

- **against a plane.** The arc's own plane meets the given plane in a line; that line meets the circle in at
  most two points; keep those inside the sweep.
- **against a segment or a ray.** It meets the arc's plane at one point, unless it lies in that plane, in
  which case the answer is the plane one.
- **against another arc or circle.** Coplanar reduces to `GeoArc2`, which is exact. Otherwise the two planes
  meet in a line, each circle meets that line in at most two points, and the answer is the points both hold —
  so at most two, and no iteration.
- **`CollidesWith` follows from those** for the crossing cases, and from containment plus crossing for a box
  or a body.

### 3.3 Cutting a curved chain, which is a Tekla question

`GeoPolyline3` can be cut twelve ways — by a point, a distance, a plane, a polygon, a face, a box, a body,
or a list of any of those. **A `GeoPolylineArc3` cannot be cut at all**, and neither can a `GeoArc3`, though
`GeoArc2.TrySplitAt` exists. A bar stopped at a pour break or trimmed to a face is exactly this question, and
the answer has to keep the bends: cutting a chain of arcs gives chains of arcs, not a polyline.

`GeoPolylineArc3` has no `Offset` either, though `GeoPolygonArc3` has one.

### 3.4 The straight types in space are short too

Counted as directions, not methods. `GeoPolyline3` is better served than it looks — it is the cutting and the
measuring that are there, and the crossing and joining that are not.

| Type | Missing |
|---|---|
| `GeoPolyline3` | `GetIntersections`, `GetShortestLineTo`, `TryIntersectWith`, `CollidesWith` against anything but a body, `Offset` |
| `GeoTriangle3` | `GetIntersections`, and any cutting at all |
| `GeoPolygon3` | `GetIntersections`, `GetShortestLineTo` |
| `GeoFace3` | `GetIntersections`, `GetShortestLineTo`, `DistanceTo` against a shape |
| `GeoAabb3`, `GeoObb3` | `GetShortestLineTo` against anything, `DistanceTo` against a triangle, polygon, face, plane, segment or ray, `TryIntersectWith` |
| `GeoPlane3` | `GetIntersections` against a segment, ray, face, polygon or chain |
| `GeoSolid3` | `TryIntersectWith`; `GetIntersections` against a polygon, face or triangle |

### 3.5 Where to start

1. **`GeoEdge3` dispatch and the coplanar lift** (3.1). No new arithmetic, and it is what makes a reinforcing
   bar answer the questions a bar is asked.
2. **Cutting a curved chain** (3.3). The most asked-for of these in a Tekla setting, and it needs only the
   crossing against a plane and a face to stand on.
3. **Arc and circle crossings** (3.2), beginning with the plane case, because everything else leans on it.
4. **The straight gaps** (3.4), which are ordinary wiring once the pairs exist in `Core`.

## 4. What needs a machine with Tekla

`GeometryHelper.TeklaConvert` compiles against 2020, 2025 and 2026, and everything but the read itself is
covered by tests that run without Tekla. What cannot be checked here:

- that `RebarGeometry.Shape` and `BendingRadiuses` hold what they are taken to hold;
- **whether Tekla gives one radius per bend or one per point.** `PointConvert.ByVertex` reads a short list as
  one per bend and a full-length list as one per vertex, and tests pin both readings, but only the modeller
  can say which arrives;
- **whether turning the work plane to global really settles the 2020 lapping offset.** The 2020 package does
  it and puts the plane back; 2025 and 2026 compile it away through the `TeklaVersion2020` symbol.

## Decisions worth not re-opening

These are kept because they govern the work above, not as a record of what was done.

- **Distance from an arc or a circle in space to anything but a point or a plane has no closed form.** A line
  wants a quartic, another circle a degree-eight polynomial. This is the one refusal in section 3, and it
  covers *distance only* — crossings are exact and are listed as work, not as refused.
  `ToPolyline3(chordTolerance)` is the gateway, and a sampled chain lies **inside** its arcs, so a clearance
  worked out that way errs on the safe side.
- **`DistanceTo` stays unsigned.** Turning its sign over is a break no compiler can catch, and it is also
  asked of two shapes, where a sign would have to mean penetration depth. `SignedDistanceTo` sits beside it.
- **Whether an arc reaches a point is measured as a distance, never as an angle.** An angular tolerance of a
  degree is nearly two units on a radius of a hundred, which once made arcs look nearer than they are and
  reported crossings clean off the end of an arc.
- **`GeoPolygonArc3` enforces coplanarity; `GeoPolylineArc3` does not.** That is what makes the lift in 3.1
  exact rather than an approximation, and it is why an off-plane chain must refuse instead of guessing.
- **A bulge is stored with the plane it bulges in.** Deriving that plane from the neighbouring legs breaks on
  reversing a chain, on splitting one, and at either end.
- **Only a corner between two straight legs is rounded in space.** A curved leg lies in a plane of its own.
- **A joining segment leaves the shape it was asked of and lands on the other.** `Core` computes each pair
  one way round only, so half the directions on the types turn the answer over. Handing it back as it comes
  gives a segment of the right length pointing backwards, which no length assertion catches.
- **A face's boundary is its outline together with the rim of every hole**, and a probe reaches its material
  when it reaches the outline and no hole holds it whole. A probe crossing no rim is inside that hole,
  outside it, or wrapped around it; the three are told apart by where one point of the probe falls and, for
  a probe with an inside, whether the rim falls within the probe.
- **Two arcs of one circle collide when either holds an end of the other**, not where they cross: two circles
  lying on each other meet along their length rather than at points.
- **A `GeoEdge2` — and a `GeoEdge3` when it follows — is always read as its segment or its arc**, and a
  direction is offered only where those two answer with the same shape of call. Where they do not, leave it
  out rather than paper over it. The single hand-written exception is
  `GeoLine2.GetIntersections(GeoEdge2)`, because two straight pieces meet at one point and that is still a
  list of length one.
- **Wiring follows the tolerance overloads of the shapes underneath, not the house style.** Where
  `GeoLine2.DistanceTo(GeoPolygon2)` takes no tolerance, neither does the edge's.
- **`GetClosestEdge` takes a primitive probe only** — a point, a segment, a circle or an arc — and that rule
  is about the *probe*, not about which of the two makes the call. `Core.ClosestEdge2` has no tolerance
  overload for the straight owners, only for the curved ones; generated wiring must follow that.
- **A shape may be asked about its own sort.** Box against box, plane against plane, triangle against
  triangle: a pair of one kind reads as one entry and is easy to pass over.
- **A rebar is only ever read as Tekla works it out.** Set-out readings were written and dropped: those
  points are hardly used and gave a second answer that differed from the model.
- **`GetClosestPointOnBoundary` on `GeoAabb3` and `GeoObb3` clamps into the box**, so an interior point comes
  straight back rather than a point on the surface. The name says otherwise, and
  `Projection3.ProjectToObbSurface` is the real one. Renaming it is a 7.0.0 job.
- **`GeoFace2.DistanceTo` against a shape is still unsettled**: nought where they touch, or the reach to the
  nearest edge of the material? `GetShortestLineTo` answers the second, and `DistanceTo` to a point answers
  the first. Settle the reading before wiring it.

## Checking the surface

Do not trust a claim that a gap is closed — re-derive it, in **two** readings, because the first alone misses
things:

1. **Against `Core`.** Read every `public` member of `src/GeometryHelper/Geometry/*.cs` and every
   `public static` of `src/GeometryHelper/Core`, take the first two `Geo…` arguments of each static in order
   as an unordered pair, and report every pair for which neither shape offers the operation.
2. **Shape against shape.** Lay the types out as a grid — row the shape in hand, column the shape asked
   about, cell the operations available — and look for cells that are not their own mirror. This is what
   finds a gap `Core` has no pair for at all: `GeoEdge2` as something to be asked *about* was invisible to
   the first reading, because an edge is always read as its segment or its arc and `Core` names neither.

Three things scan wrongly and must be allowed for:

- a static whose result is an `out` parameter (`TryIntersectWith`) puts its shapes first, so take the
  arguments in order and stop at two;
- a shape against its own sort reads as one pair, and is easy to skip;
- two overloads can share a return type and still not be interchangeable. `GeoLine2.TryIntersectWith` hands
  back one point against a segment and a list against an arc, so anything dispatching between them must
  compare the whole argument list.

## Verifying a change

`dotnet build` reports a clean build without compiling anything when the tree has not changed, and
**analyzers only run when the compiler runs**. Verify with:

```
dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental
```

and read the count. The same goes for each `-p:TeklaVersion=` build, which has its own `obj` folder. A test
run under `--no-build` may be answering from the DLL as it was before the edit.

## Optional, and not urgent

The five retired packages — `CommonGeometry`, `PlaneGeometry`, `SolidGeometry`, `ArrangeAlgorithms` and the
unyeared `TeklaConvert` — are deprecated and unlisted on nuget.org, every version, so a search for
"GeometryHelper" returns only the six current packages. They are still on **GitHub Packages**, which has
neither unlisting nor deprecation: the choices there are to delete a package or make it private. There is
little to gain, because that registry answers anonymous reads with 401, so nothing in it is publicly visible
in the first place.
