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

## 2. Arcs in space: done

All five phases are done. `Core.Arc3` answers for a plane, a segment, a ray, another arc, a
circle, a triangle, a polygon, a face, either kind of box and a body — and none of it is sampled, because an
arc lies in a plane and every flat-faced shape is made of planes. Two readings carry all of it: coplanar goes
to the radical line of the two circles, and not coplanar goes to the line where the two planes meet. A line
meets a circle at most twice, so one quadratic gives the candidates and the other shape's own test keeps what
it reaches.

`GeoCircle3` forwards to the whole-turn arc. `GeoEdge3` reads itself as its arc or its segment and dispatches.
`GeoPolylineArc3` and `GeoPolygonArc3` take the union over their edges. And a bar can be **cut** — at a point,
at a distance along it, at a plane, at a face, or against a body — with the pieces coming back as chains of
arcs rather than polylines.

### What is left here

Nothing. The three pieces this section named on 26 September are done: cutting by a box or by an array of
cutters, one chain against another, and the loop's `GetShortestLineTo` and `GetClosestEdge`.

`GeoPolygonArc3` works in its own plane — booleans against a coplanar loop or polygon, `Chamfer`,
`TryFilletAt`, `TryChamferAt`, `SignedDistanceTo`, the joining line and the nearest edge. A chain and a
closed loop can both be cut: by a point, a distance, a set of distances, a plane, a face, a body, either
kind of box, or an array of bodies or boxes. An arc in space can be cut in two. A chain can be asked about a
segment, a ray and another chain. And a chain's end leg can be lengthened, which is what a bar wants for
anchorage.

**Decisions taken here, not to be re-opened:**

- **A second shape has to lie in the loop's plane, and is refused where it does not** (`ArgumentException`).
  Projecting it in would report two stirrups a hundred apart as overlapping and say nothing about it.
  `SharesPlaneWith` is public on `GeoPolygon3`, `GeoFace3` and `GeoPolygonArc3` so a caller can ask first.
  **A point is the exception, and not as a compromise**: a point off the plane stands at the same height
  above every point of the boundary, so the nearest place to it is the nearest place to its shadow, and the
  answer is exact.
- **Chain against chain walks every pair of edges**, so the cost grows with the two edge counts multiplied.
  Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any
  arithmetic; the second chain's boxes are worked out once. That is what makes it usable, and it is the
  reason this one was held back while the rest was written.
- **Cutting sorts pieces by the middle of each piece, and joins neighbours on the same side into one run.**
  Every end is on a surface by construction, so an end cannot say which side it is on; and two overlapping
  cutters name a surface inside the region they cover between them, so without the joining a bar would come
  back as three pieces inside one opening. `Splition3` already kept this rule for a straight chain.
- **`GeoLine3` gets no array-form `TryIntersectWith` against another `GeoLine3`.** It already has one handing
  back a single point, and a second differing only in the shape of its `out` makes every existing call
  ambiguous — which the build proved. `GetIntersections` is the list reading; `GeoEdge3` builds its own list
  from that rather than forwarding.
- **A ray is a value type**, so it cannot be refused for being absent. One with no direction reads as its own
  origin and nothing more.

### What stays refused

`DistanceTo` and `GetShortestLineTo` from an arc or a circle in space to anything but a point. A segment wants
a quartic, another circle a degree-eight polynomial. `ToPolyline3(chordTolerance)` is the gateway, and a
sampled chain lies **inside** its arcs, so a clearance worked out that way errs on the safe side. Crossings are
a different question and are exact.

### The traps this work turned up

- **`GeoArc3` measures its angles from whatever pair of axes `GeoPlane3.GetAxes` hands back**, and for the
  plane z = 0 the first of those is the **y** axis. So `new GeoArc3(origin, ZAxis, 100, 0, PI/2)` sweeps the
  *second* quadrant. It is documented — `GetAxes` says a plane has no preferred pair — and a test pins it.
  **Build arcs in tests from three points they pass through**, never from angles.
- **A `Try` method that throws.** `PlanarMap.TryToArc2` fed a whole-turn arc's start, middle and end to
  `FromThreePoints`, and a whole turn's start and end are the same place, so three points were two and it threw
  "three points on one line have no arc through them". `GeoArc3.ProjectToArc2` now lays a whole turn out from
  its centre. Reachable by anyone holding a full-turn arc, not only by the reduction that found it.
- **A sampled-arc oracle counts a vertex twice.** A chain crossing a plane exactly at one of its vertices is
  reported by both chords meeting there.
- **A near miss to remember:** `dotnet test --no-build` reported eight passing tests from a build that had
  failed on an analyser rule. Read the build result first, every time.

### The pieces this work leant on and had to work around

All three are closed now: `GeoPolyline3` answers `GetIntersections` against a curved chain or loop,
`GeoPolygon3` can be filleted and chamfered, and `GeoTriangle3` reads as a polygon or a face.

## 3. The families that change geometry: audited and closed

Every matrix drawn before 26 September covered **measuring only**. The families that *change* geometry were
never laid out against the types. They have been now, and the gaps the matrix showed are closed.

**How the matrix was drawn**, so it can be redrawn rather than trusted: `…/scratchpad/shaping.py` reads every
`public` member of `src/GeometryHelper/Geometry/*.cs`, files each one under an operation family by the name it
starts with, and prints a type-by-family grid. A blank cell is then a gap or a decision, never a guess. Two
traps it walked into and now avoids: **`Expand` is an offset under another name** and **`TryUnion` is a
boolean**, and reading them as neither made three closed cells look blank.

| Family | Where it stands |
|---|---|
| **Conversions** | `GeoTriangle3.ToPolygon3` and `ToFace3` name the rebuild a triangle out of `Triangulate` needed before anything taking a polygon would accept it. A triangle with no area is refused; `IsDegenerate` asks first. |
| **Splitting** | `GeoPolygon2` and `GeoFace2` are cut into *areas* by the straight line through a segment, or along a `GeoPolyline2` drawn across them. An arc in space is cut at a parameter, a point or a distance. A curved chain or loop is cut by a box or an array of cutters, and `SplitAtDistances` is on the chain as well as the loop. |
| **Booleans** | `GeoPolygon3` and `GeoFace3` have all four through the coplanar lift, both against each other and against a curved loop. `GeoObb3` has the three solid ones through the body it bounds, offered both ways round with `GeoSolid3`. |
| **Extending and trimming** | Arcs, chains, loops and edges, in the plane and in space. An arc is lengthened *along itself*; a chain by its *end leg*; both outwards only, with the splitting family for the other direction. |
| **Filleting** | `GeoPolyline2`, `GeoPolygon2` and `GeoPolygon3` can be rounded, which the curved types could already do. The answer is the curved type, because a rounded corner is an arc. |
| **Chamfering** | `GeoPolyline3`, `GeoPolygon3` and `GeoPolylineArc3` can be cut back. A chamfer needs no plane, so a chain lying in none is cut all the same; only a corner between two straight legs is cut. |
| **Offsetting** | `GeoPolylineArc3.OffsetInPlane` moves a bent bar to another cover without straightening it. `GeoObb3.TryExpand` gives the oriented box the margin the square one had. |
| **Merging** | Done earlier: `Core.Merge2` gained its default-tolerance twins and both merge classes are reachable through `MergeExtension` and `GeoSolid3.MergeCoplanarFaces`. |

### What the matrix still shows blank, and why that is right

- `GeoRectangle2`, `GeoTriangle3`, `GeoAabb3` and `GeoObb3` are each **one conversion away** from the family
  they lack — `ToPolygon2`, `ToPolygon3`/`ToFace3`, `ToObb`, `ToSolid`. Adding a second way to do the same
  thing would only make the surface bigger.
- **`GeoEdge2` and `GeoEdge3` cannot be offset.** An edge offers a direction only where a segment and a bend
  answer with the **same shape of call**, and here they do not: `GeoLine2.Offset` hands back a segment while
  `GeoArc2.TryOffset` reports whether the radius survived.
- A **circle** cannot be lengthened or cut: it is already a whole turn, and cutting it gives arcs, which the
  plane does not offer either. The two agree, which is the test.
- **`GeoPolyline*` has no booleans.** A chain is not an area.

`GeometryHelper.Spatial` — `GeoBvh2` and `GeoBvh3` — is a standalone index that no shape type points at.
Whether it should stay that way is a decision nobody has taken, and it is the one open question left here.

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
