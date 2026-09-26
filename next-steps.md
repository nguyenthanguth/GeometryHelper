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

## 2. Space is far behind the plane

For **measuring** — distance, clearance, nearest point, touching, crossing — the plane is complete: every
shape answers about every other, both ways round. That is the only family anyone has audited; section 3 is
about the ones nobody has. Space is not complete even for measuring, and the gap is widest exactly where a
reinforcing bar lives. `GeoArc3`, `GeoCircle3`, `GeoEdge3`, `GeoPolygonArc3` and
`GeoPolylineArc3` have, between them, **no crossings, no collisions, no joining segment and no cutting at
all**. They answer about a point, they move, an arc, a circle and a closed loop can be offset, and a chain
can be filleted. That is the whole of it.

Count directions rather than methods when reading what follows, and count them from the source rather than
from this page — the section below was first written from a listing cut short at forty entries, and claimed
`GeoPolyline3` could not be cut when in fact it can be cut twelve ways.

### 2.1 What can be lifted or dispatched, with no new arithmetic

This is the same work done twice in the plane, and it should be done first because it is exact and cheap.

| Type | What it could answer | How |
|---|---|---|
| `GeoPolygonArc3` | everything `GeoPolygonArc2` answers, against any shape sharing its plane | it **enforces coplanarity**, so project to `GeoPolygonArc2`, answer there, lift back — exactly as `Area`, `Locate`, `Offset` and `Fillet` already do |
| `GeoPolylineArc3` | the same, when `IsPlanar()` is true | `TryGetPlane` already decides it; off-plane must refuse rather than approximate |
| `GeoEdge3` | everything `GeoArc3` and `GeoLine3` both answer | `IsArc ? ToArc().X(…) : ToLine().X(…)`, and the reverse on every other type — the pattern `GeoEdge2` now follows, including the rule that a direction is offered only where both answer with the **same shape of call** |

### 2.2 Crossings in space have closed forms and are simply missing

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

### 2.3 Cutting a curved chain, which is a Tekla question

`GeoPolyline3` can be cut twelve ways — by a point, a distance, a plane, a polygon, a face, a box, a body,
or a list of any of those. **A `GeoPolylineArc3` cannot be cut at all**, and neither can a `GeoArc3`, though
`GeoArc2.TrySplitAt` exists. A bar stopped at a pour break or trimmed to a face is exactly this question, and
the answer has to keep the bends: cutting a chain of arcs gives chains of arcs, not a polyline.

`GeoPolylineArc3` has no `Offset` either, though `GeoPolygonArc3` has one.

### 2.4 The straight types in space are short too

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

### 2.5 Where to start

The order matters here, because three of these stand on the first one. `GeoEdge3` dispatch reads
`IsArc ? ToArc().X(…) : ToLine().X(…)`, and **there is nothing to dispatch to until `GeoArc3` can answer**.

1. **Arc and circle crossings** (2.2), the plane case first. Nothing else in space can be built without it,
   and it is exact.
2. **`GeoEdge3` dispatch** (2.1). Free once step 1 exists, and it is what lets a reinforcing bar be asked
   the questions a bar is asked.
3. **Cutting a curved chain** (2.3). Every edge is a segment or an arc, so this needs step 1's arc against
   a plane and against a face. It is the most asked-for of these in a Tekla setting.
4. **The straight gaps** (2.4), ordinary wiring once the pairs exist in `Core`.

**The coplanar lift for `GeoPolygonArc3`** (2.1) stands apart: it needs only `GeoPolygonArc2`, which is
complete, so it can be done at any point. Settle one thing before starting it — what a probe that does
**not** lie in the loop's plane should do. Refusing is honest, projecting it is convenient and quietly
wrong, and falling back to `ToPolygon3(chordTolerance)` is neither. The library has no precedent for this
choice.

## 3. Whole families nobody has audited

Every matrix drawn so far covered **measuring only**: `DistanceTo`, `SignedDistanceTo`, `GetShortestLineTo`,
`GetClosestPointOnBoundary`, `GetClosestEdge`, `CollidesWith`, `GetIntersections`, `TryIntersectWith`. The
operations that *change* geometry were never laid out against the types at all, and a first count of them
turns up more than the measuring audit did.

| Family | Where it stands |
|---|---|
| **Merging** | `Core.Merge2` holds four methods and `Core.Merge3` seven, and **no type exposes either**. Joining a bag of segments into chains is bread-and-butter work and is reachable only through `Core`. `Merge2`'s methods also all demand an explicit `Tolerance` with no default-tolerance twin, unlike everything else in the library. |
| **Splitting** | `GeoPolygon2` and `GeoFace2` **cannot be cut at all**, though `GeoPolygon3` and `GeoFace3` can. `Core.Splition2` has no pair for either. Here the plane is behind space, the reverse of everywhere else. |
| **Booleans** | `GeoPolygon2`, `GeoFace2`, `GeoPolygonArc2`, `GeoSolid3` and `GeoAabb3` have them. `GeoObb3`, `GeoFace3` and `GeoPolygon3` do not — and the last two are a coplanar lift away, the same lift as 2.1. |
| **Extending and trimming** | Only `GeoLine2` and `GeoLine3`, which have thirty-two methods apiece. **No arc, no polyline, no chain can be extended or trimmed to meet anything.** It would not start from nothing: `Core.CurveMeet2`, internal, already works out where the endless line behind a segment and the whole circle behind an arc cross, which is the part of trimming that is not bookkeeping. |
| **Filleting** | `GeoPolygon3` has none, though `GeoPolyline3` does and both are chains of straight legs. |
| **Offsetting** | `GeoPolyline3`, `GeoPolylineArc3`, `GeoObb3` and `GeoAabb3` have none. Growing a box by a distance is a one-line answer. |

Before doing any of it, **draw the matrix for that family first**, the way section *Checking the surface*
describes. The measuring audit found roughly a hundred and fifty directions once it was written down, and
none of them were visible before.

`GeometryHelper.Spatial` — `GeoBvh2` and `GeoBvh3` — is a standalone index that no shape type points at.
Whether it should stay that way is a decision nobody has taken.

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
  wants a quartic, another circle a degree-eight polynomial. This is the one refusal in section 2, and it
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
