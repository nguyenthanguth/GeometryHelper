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

## 2. Arcs in space cannot be asked anything

`GeoArc3`, `GeoCircle3`, `GeoEdge3`, `GeoPolygonArc3` and `GeoPolylineArc3` answer about **a point and
nothing else**. `GeoArc3` is 633 lines and 55 public members — as much machinery as `GeoArc2`, which answers
about a hundred directions — and `Core` holds nothing for an arc in space but
`Distance3.DistanceTo(GeoCircle3, GeoPoint3)`. Their `.Collision.cs` and `.Intersection.cs` files exist and
are empty; this section is how they get filled.

### 2.1 Why all of it is exact

An arc in space **lies in a plane**, and every flat-faced shape in the library is made of planes. Two
reductions cover every pair, and neither samples anything:

**Reduction A — coplanar, so drop into the plane.** When the other shape lies in the arc's own plane, take
`PlanarMap.FrameOf(arc.GetPlane())`, bring both down with `TryToArc2`, `ProjectToLine2`,
`ProjectToPolygon2`, answer with the whole of `Core.Arc2`, and lift the points back with `ToPoint3`. Exact,
and it reuses code that is already tested to death.

**Reduction B — not coplanar, so the answer is on a line.** Two distinct planes meet in a line
(`Intersection3.TryIntersectWith(plane, plane, out GeoRay3)`). Every point the arc shares with any flat shape
must lie on that line, so: arc against that line gives **at most two candidates**, and each candidate is then
tested against the other shape with `Containment3.Contains`. No iteration, no tolerance bargain.

| Other shape | How |
|---|---|
| `GeoPlane3` | B. Coplanar is the special case: the whole arc lies in the plane, so nothing *crosses* it |
| `GeoLine3`, `GeoRay3` | in the arc's plane → A; otherwise it pierces that plane at one point, so test `arc.IsPointOn` |
| `GeoArc3`, `GeoCircle3` | coplanar → A (`Arc2.GetIntersections`); otherwise B through the two planes |
| `GeoTriangle3`, `GeoPolygon3`, `GeoFace3` | coplanar → A; otherwise B, then `Containment3.Contains(shape, candidate)` |
| `GeoAabb3`, `GeoObb3`, `GeoSolid3` | the surface is flat faces, so the union of arc against each face — `Triangulate()` for a body |

**A `GeoCircle3` is an arc of a whole turn** — `GeoArc3` takes equal start and end angles as exactly that — so
every circle pair is the arc pair with no second implementation. Write the arc one and forward.

### 2.2 Phase 1 — `Core/Arc3.cs`, the reduction kit

Mirrors `Core/Arc2.cs`. Two internal helpers carry the weight:

```
TryReduce(GeoArc3 arc, GeoPlane3 other, Tolerance, out GeoCoordinateSystem3 frame)   // coplanar?
Candidates(GeoArc3 arc, GeoRay3 line, Tolerance)                                    // the <=2 points on a line
```

Then the public pairs, split the way `Core` splits everything else:

| File | Pairs |
|---|---|
| `Arc3.cs` | the helpers, plus plane, segment, ray |
| `Arc3.Curves.cs` | arc, circle |
| `Arc3.Flat.cs` | triangle, polygon, face |
| `Arc3.Bodies.cs` | axis-aligned box, oriented box, solid |

Each pair gets `GetIntersections`, `CollidesWith` and `TryIntersectWith`, each with its tolerance twin.

**Decide before writing:** `CollidesWith` against a body is *not* just "a crossing exists". An arc lying
wholly inside a solid crosses nothing and still touches it, so the rule is
`crossings.Length > 0 || Containment3.Contains(solid, arc.StartPoint)` — the same shape as the ray-against-solid
rule already in `Collision3.Ray.cs`.

### 2.3 Phase 2 — wire it onto the types, both ways

Into the empty files already waiting: `GeoArc3.Intersection.cs`, `GeoArc3.Collision.cs`, `GeoCircle3.*`, and
the reverse direction on `GeoPlane3`, `GeoLine3`, `GeoRay3`, `GeoTriangle3`, `GeoPolygon3`, `GeoFace3`,
`GeoAabb3`, `GeoObb3`, `GeoSolid3`.

**One dependency to settle here, or phase 3 stalls.** `GeoEdge3` dispatch needs `GeoLine3` to answer the same
*shape of call* as `GeoArc3`. It does not: `GeoLine3.TryIntersectWith(plane, out GeoPoint3)` hands back one
point where the arc hands back an array. So phase 2 also adds
`GeoLine3.GetIntersections(GeoPlane3 | GeoTriangle3 | GeoPolygon3 | GeoFace3)` in array form, wrapping the
single point — the same glue `GeoLine2.GetIntersections(GeoEdge2)` already uses, and for the same reason.

### 2.4 Phase 3 — `GeoEdge3`

`IsArc ? ToArc().X(…) : ToLine().X(…)`, and the reverse on every other type. Free once phases 1 and 2 are
done, and governed by the rule `GeoEdge2` already follows: **a direction is offered only where the arc and the
segment both answer with the same shape of call.** Where they do not, leave it out.

### 2.5 Phase 4 — the curved chains, and cutting them

Crossings for `GeoPolylineArc3` and `GeoPolygonArc3` are the union over `GetEdges()`, each edge answered by
phase 3. `CollidesWith` follows.

Then the one the Tekla work actually wants. `GeoPolyline3` can be cut twelve ways — by a point, a distance, a
plane, a polygon, a face, a box, a body, or a list of any of those — and **a curved chain cannot be cut at
all**. A bar stopped at a pour break or trimmed to a face is exactly this. Build it on the phase-4 crossings,
and **the pieces must come back as chains of arcs, not polylines**: cutting a bar must not straighten its
bends.

### 2.6 Phase 5 — the coplanar lift for `GeoPolygonArc3`

Independent of everything above: it needs only `GeoPolygonArc2`, which is complete. Project to
`GeoPolygonArc2`, answer, lift back — as `Area`, `Locate`, `Offset` and `Fillet` already do.

**Blocked on one decision.** What should a probe that does *not* lie in the loop's plane do? Refusing is
honest, projecting it is convenient and quietly wrong, and falling back to `ToPolygon3(chordTolerance)` is
neither. Settle that first; the library has no precedent.

### 2.7 What stays refused

`DistanceTo` and `GetShortestLineTo` from an arc or a circle in space to anything but a point. A line wants a
quartic, another circle a degree-eight polynomial. `ToPolyline3(chordTolerance)` is the gateway, and a sampled
chain lies **inside** its arcs, so a clearance worked out that way errs on the safe side. Crossings are a
different question and are exact — see 2.1.

### 2.8 How each phase is tested

Three checks, and the second is the one that makes this safe without hand-computing every case:

1. **Exact values on geometry worked out by hand** — an arc of radius 100 about the origin in the XY plane
   crosses the plane `x = 50` at `(50, ±86.602540)`.
2. **Against a sampled arc.** `arc.GetIntersections(x)` must agree with
   `arc.ToPolylineByChordTolerance(0.01).GetIntersections(x)` to within the chord tolerance. A wrong reduction
   shows up here immediately, and it needs no arithmetic from me.
3. **Against the plane library, where the case is coplanar.** Reduction A must give exactly what
   `Arc2.GetIntersections` gives for the same shapes laid flat.

Plus, as everywhere: both directions agree, every method has its tolerance twin, and null is refused.

### 2.9 The straight types are short too

Separate from the arcs, and ordinary wiring once the pairs exist in `Core`:

| Type | Missing |
|---|---|
| `GeoPolyline3` | `GetIntersections`, `GetShortestLineTo`, `TryIntersectWith`, `CollidesWith` against anything but a body, `Offset` |
| `GeoTriangle3` | `GetIntersections`, and any cutting at all |
| `GeoPolygon3` | `GetIntersections`, `GetShortestLineTo` |
| `GeoFace3` | `GetIntersections`, `GetShortestLineTo`, `DistanceTo` against a shape |
| `GeoAabb3`, `GeoObb3` | `GetShortestLineTo` against anything, `DistanceTo` against a triangle, polygon, face, plane, segment or ray, `TryIntersectWith` |
| `GeoPlane3` | `GetIntersections` against a segment, ray, face, polygon or chain |
| `GeoSolid3` | `TryIntersectWith`; `GetIntersections` against a polygon, face or triangle |

## 3. Whole families nobody has audited

Every matrix drawn so far covered **measuring only**: `DistanceTo`, `SignedDistanceTo`, `GetShortestLineTo`,
`GetClosestPointOnBoundary`, `GetClosestEdge`, `CollidesWith`, `GetIntersections`, `TryIntersectWith`. The
operations that *change* geometry were never laid out against the types at all, and a first count of them
turns up more than the measuring audit did.

**Merging is done** — `Core.Merge2` gained the default-tolerance twins it was missing, and both merge classes
are reachable through `MergeExtension` and `GeoSolid3.MergeCoplanarFaces`. It is the pattern for the rest of
this section: the arithmetic was there, the way in was not.

| Family | Where it stands |
|---|---|
| **Conversions** | `GeoTriangle3` has no `ToPolygon3` or `ToFace3`, so a triangle from `Triangulate` has to be rebuilt from its three corners to be used as a face. |
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
- **Measuring lives in four partial files per shape type**, named after the `Core` class that computes the
  answer: `GeoLine2.Distance.cs` mirrors `Distance2`, `.Projection.cs` mirrors `Projection2`, and so on
  through `.Collision.cs` and `.Intersection.cs`. Four suffixes rather than eight operation names, and the
  boundary is not invented — it is the same split `Core` already uses. **All four exist for all 27 shape
  types, empty where nothing is offered yet**, so the place to add a direction is already there and named;
  `GeoArc3.Collision.cs` being empty is the work item in section 2, stated as a file. The six types that are
  not shapes — `GeoVector2/3`, `GeoTransform2/3`, `GeoCoordinateSystem2/3` — have none, since a direction has
  no distance to a polygon. The splitter is kept at `scratchpad/split2.py` and can be run again.
- **A refactor that must not change the API is proved, not asserted.** Diff the sorted `<member name=` lines
  of `bin/Release/netstandard2.0/GeometryHelper.xml` before and after: 5,119 both times, no difference, and
  the test count unchanged at 2,287. Cheap, and it turns "I was careful" into a fact.
- **Both directions live on the types, and the surface being large is the accepted price.** Decided at
  6.0.0, having counted it: two thirds of `GeoArc2`'s hundred and fifty members are measuring wiring, and
  section 2 will add about as much again to the types in space. Moving the family to extension methods was
  considered and turned down. `a.DistanceTo(b)` compiling whatever is in hand is worth more than a short
  member list, it needs no `using` in a script, and it is what lets a test compare the two directions
  against each other — which is how nearly every mis-wiring this week was caught. So **wire both
  directions by default**, and do not re-propose extension methods for it.
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
