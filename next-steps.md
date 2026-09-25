# Next steps

What is outstanding as of 25 September 2026, with the version at 6.0.0 and 5.1.0 published to
nuget.org. The release is section 3; the work in progress is section 6.

## 1. The `GetClosestOnBoundary` rename — done

Everywhere in the library, `GetClosestOnBoundary` returned the **shortest segment connecting** two shapes.
The name did not say that. It read as *the closest thing on my boundary*, which is a different idea and an
easy one to act on: `GeoPolygonArc2` and `GeoPolylineArc2` took the name for exactly that and returned an
**edge of the shape** instead.

The summaries said the right thing all along — *"Finds the shortest line segment connecting a point on the
boundary of `poly` to a point on `line`"* — so only the names had to move:

| Was | Returns | Now |
|---|---|---|
| `GetClosestPointOnBoundary(point)` | a point **on** this shape | unchanged, it was already right |
| `GetClosestOnBoundary(other)` | the segment **joining** the two | `GetShortestLineTo(other)` |
| `Projection2.GetClosestSegment(a, b)` | the same segment | `Projection2.GetShortestLineTo(a, b)` |
| `Projection3.GetClosestSegment(a, b)` | the same, in space | `Projection3.GetShortestLineTo(a, b)` |
| `GetClosestOnBoundary(other)` on the curved chains | an **edge** of this shape | `GetClosestEdge(other)` |

*Shortest* is what removes the ambiguity: no existing edge can be picked by being "shortest to X", so the
word can only describe a segment built to be as short as possible. `GetClosestLineTo` was considered and
keeps the trap — *the closest line* still reads as *the closest line of mine*.

On the two curved chains the return type differs too — `GeoEdge2` against `GeoLine2` — so a caller who
takes the new name for the old meaning stops compiling rather than quietly getting a different answer.

No `[Obsolete]` forwarders were left behind. They would have doubled the surface of the library to serve
two releases that are days old.

### Done since: the curved chains have a real `GetShortestLineTo`

The closest **point pair** for arc-to-segment, arc-to-arc and arc-to-circle turned out to be a short step
from the distance, because `Core.Arc2.DistanceTo` already weighed exactly the right candidates and only
threw the winning pair away. `Core.Arc2.GetShortestLineTo` keeps it, `Core.ArcChain2.ShortestLineTo` walks
a run of edges with it, and `Projection2` offers it on both curved types against every shape their
`DistanceTo` accepts. `GetClosestEdge` was given to the straight shapes at the same time, so both halves
of the pair now exist on both families.

## 2. An arc reached a degree too far

Found while building the above, and fixed. Whether an arc reached a point of its own circle was settled by
comparing two directions within `Tolerance.EqualAngleRad`, **a whole degree** by default. A degree of a
large arc is a long way: on a radius of a hundred it is nearly two of whatever the drawing is measured in.

Two things came of that, both wrong by more than rounding:

- `Arc2.DistanceTo(arc, line)` and `DistanceTo(arc, arc)` could weigh a point the arc does not reach, and
  so report the arc **nearer** than any of its own points allow. A case turned up by sweeping reported
  141.887268 where the true distance, brute-forced over the arc, is 141.888535.
- `Arc2.GetIntersections` could report a crossing lying **1.3 clear of the arc**, on a radius of 112.6.
  Everything built on it inherited that: booleans, splitting, offsetting, `CollidesWith`, `Locate`.

The question is now asked as a distance rather than as an angle, which is the unit the answer is in:
`ProjectToArc` clamps onto the arc, and the candidate is either the clamped point or nothing. The shape of
the candidate sets is unchanged, and so is the reasoning behind them; all of the existing tests passed
without amendment. Five tests in `ArcExactnessTests` pin it, each held against a brute-force reading of the
same geometry rather than a number written down once, and all five fail if the angular test is put back.

**This is a behaviour change in shipped code.** Arcs that only nearly touch are no longer reported as
crossing. It belongs in the 6.0.0 notes beside the rename.

## 3. Releasing 6.0.0

`Directory.Build.props` is at **6.0.0** — a major number because the rename above breaks a published API,
and 5.0.0 and 5.1.0 are both out. It is the one literal every package reads, including the Tekla ones
through `$(GeometryHelperVersion).$(TeklaVersion)`.

To release, **create a GitHub Release tagged `v6.0.0`**: `.github/workflows/release.yml` fires on
`release: published` and publishes all six packages.

Do not use *Run workflow* on that workflow to try it out. It has no dry run, and the two push steps are
not guarded by the event type, so a manual run publishes to nuget.org and GitHub Packages for real.

The release notes are already written, in `PackageReleaseNotes` in `src/GeometryHelper/GeometryHelper.csproj`
under **NEW IN 6.0.0**. They cover both breaking changes — the rename and the arc measurement — and the new
surface. Copy that text into the GitHub Release body.

## 4. Done since, and what it leaves

Everything on the list that was outstanding has been closed except one, and that one is closed
deliberately:

- **Cross-family collisions and intersections** now run both ways. `polygon.CollidesWith(loop)`,
  `rect.GetIntersections(arc)` and the rest compile, and `GeoRectangle2` is offered to the curved types for
  the first time, in all four of `DistanceTo`, `GetShortestLineTo`, `CollidesWith` and `GetIntersections`.
- **`GeoSolid3` against a ray** is done, measured as a ray rather than as a segment cut to some chosen
  length. `DistanceTo`, `CollidesWith`, `GetIntersections` and `GetShortestLineTo` all take one.
- **`GeoSolid3` against a `GeoCircle3` is deliberately not offered.** The distance from a circle in space to
  a flat face has no closed form: it needs a polynomial root solve, and a sampled answer under an exact
  name is worse than no answer. `circle.ToPolylineByChordTolerance(0.1)` says in the call how close an
  answer is being asked for, and the guide points at it.
- **`GetClosestEdge` takes a primitive only** &#8212; a point, a segment, a circle or an arc. The nearest
  edge of one many-edged shape to another is really a pair of edges, which is a different answer from the
  one the name promises, so it is not offered.

The four hand-written guides and the package release notes are up to date with all of it. Every snippet in
`plane.md` and `solid.md` runs as a test in `ReadmeExamplesTests`, which is why they can be trusted.

## 5. Signed distance, added after the rest

`DistanceTo` reads a closed shape as a filled region and answers nothing at all for a point inside one, so
how far in it sat could not be got back out. `SignedDistanceTo` keeps it: the magnitude is the distance to
the boundary whichever side of it the point is on, and the sign says which side.

The question asked was whether `DistanceTo` itself should turn negative inside. It should not, and the
reasons are worth keeping:

- A sign flip is a change **no compiler can catch**. Every `if (d < tolerance)`, `d == 0` and `d <= 0`
  changes meaning in silence. The rename above was deliberately shaped so callers stop compiling instead.
- `DistanceTo` is also asked of two shapes and answers nought when they overlap. A sign there would have to
  mean penetration depth, which is a different and harder quantity, so `DistanceTo` would be signed in one
  overload and unsigned in thirty.
- The library already had the pattern: `Area` beside `SignedArea`, `Volume` beside `GetSignedVolume`, and for
  a plane `DistanceTo(plane, point)` is literally `Math.Abs(plane.SignedDistanceTo(point))`.

The definition is tied to `Locate` — negative for `Inside`, nought for `OnSide`, positive for `OutSide` — and
that is what makes the two safe to lean on together. It is tested over a grid rather than at chosen points.

Two things fell out of building it, both fixed:

- `GeoFace2` had **no** `DistanceTo` to a point at all. It has one now.
- `GetClosestPointOnBoundary` on `GeoAabb3` and `GeoObb3` **clamps a point into the box**, so it hands an
  interior point straight back rather than a point on the surface. The name says otherwise, and the signed
  distance uses `Projection3.ProjectToObbSurface` instead. Worth renaming one day; not done here, because it
  is another silent break and this release already carries two.

## 6. Arcs in space: `GeoPolylineArc3` and `GeoPolygonArc3`

**In progress.** Asked for on 25 September 2026 with a concrete reason: a Tekla `rebar` carries a bending
radius, so a bar is a chain of straight runs with a tangent arc at every bend. That is a curve in space, and
nothing in the library could hold it.

### What makes it representable

A bulge is a 2D idea and does not lift on its own: a chord plus a bulge is ambiguous in space, because the arc
could lie in any of the infinitely many planes through that chord. Adding **one** thing fixes it.

| | Stored per piece |
|---|---|
| `GeoEdge2` | `StartPoint`, `EndPoint`, `Bulge` |
| `GeoEdge3` | `StartPoint`, `EndPoint`, `Bulge`, **`Normal`** |

The bulge keeps its meaning, the tangent of a quarter of the swept angle. The normal says which plane it bulges
in. Invariant checked at construction: a non-zero bulge needs a non-zero normal perpendicular to the chord.

For a rebar every arc is a fillet at a corner, so the plane **could** be derived from the two neighbouring
segments. It is stored explicitly anyway, because deriving it breaks on reversing a chain, on splitting one, and
at either end.

### The three types

- **`GeoEdge3`** — one piece: a segment when the bulge is nought, an arc when it is not.
- **`GeoPolylineArc3`** — free in space, **coplanarity not required**, exactly as `GeoPolyline3` says of itself.
  This is the type a rebar is. Holds vertices, bulges and normals.
- **`GeoPolygonArc3`** — closed and **coplanarity enforced**, as `GeoPolygon3` already enforces it. That makes
  it largely a façade over a frame plus a `GeoPolygonArc2`: area, `Locate`, `Offset` and the booleans all come
  back **exactly** by projecting into its own plane, working in 2D and lifting the answer. None of the arc
  machinery is written twice. A closed bar too far out of plane is held as a `GeoPolylineArc3` whose ends meet.

### What is exact and what is not

`GeoArc3` today measures to a **point** and to nothing else. There is no arc-to-segment in space, and that is
the same polynomial root solve declined for circle-to-triangle in section 4.

**Exact**: `Length`, walking the chain (`GetPointAtDistance`, `GetParameterAtPoint`, `GetDistanceAtPoint`),
`GetClosestPointOnBoundary(point)`, `DistanceTo(point)`, `GetAabb`, `TransformBy`, `Reverse`, `IsPlanar` and
`TryGetPlane`, `Flatten`, filleting a corner, and **everything** on `GeoPolygonArc3`, because it projects.

**Not exact**: the chain against a `GeoLine3`, a `GeoSolid3`, another arc, or any intersection in space. The way
through is `ToPolyline3(chordTolerance)`, the explicit approximation `GeoArc3.ToPolylineByChordTolerance` and
`GeoCircle3` already use, so the accuracy sits in the call rather than hidden in the answer.

### Steps, each leaving something usable

| Step | Work | Rough size |
|---|---|---|
| 1 | `GeoEdge3` and its tests | 400 lines |
| 2 | `GeoPolylineArc3`: construction, walking, distance to a point, transform, reverse, planarity, flatten | 900 |
| 3 | **`Fillet` for chains in space** — `GeoPolyline3.Fillet(radius)`, a radius per corner, `TryFilletAt` | 300 |
| 4 | `PlanarMap`: `ToArc3`, and both directions for the curved chains | 200 |
| 5 | `GeoPolygonArc3` and its operations through the plane | 800 |
| 6 | Measuring against other shapes in space via `ToPolyline3(chordTolerance)`, written up as approximate | 300 |
| 7 | `RebarConvert` in `GeometryHelper.TeklaConvert` | 300 |

**A rebar is usable after step 3**: take the points and the bending radius from Tekla, then
`GeoPolyline3.Fillet(radius)`. Each corner takes the plane of its two segments, the three points project into it,
the 2D fillet already written does the work, and the result lifts back. Exact, nothing sampled.

**Step 7 can be compiled here but not run.** `Tekla.Structures.Model.dll` is in the repo, so it builds; the 74
Tekla tests pass without Tekla installed only because they never touch `Tekla.Structures.Model`, and a rebar
converter must. So that step ships as code to be tried on a machine with Tekla.

### Decided with the user

- `GeoPolygonArc3` **enforces coplanarity**. The trade is that a stirrup further out of plane than
  `Tolerance.EqualPlanar` is refused; that shape belongs in a `GeoPolylineArc3`.
- Step 7 **is in scope**.

## Optional, and not urgent

The five retired packages — `CommonGeometry`, `PlaneGeometry`, `SolidGeometry`, `ArrangeAlgorithms` and
the unyeared `TeklaConvert` — are deprecated and unlisted on nuget.org, every version, so a search for
"GeometryHelper" returns only the six current packages. They are still on **GitHub Packages**, which has
neither unlisting nor deprecation: the choices there are to delete a package or make it private. There is
little to gain, because that registry answers anonymous reads with 401, so nothing in it is publicly
visible in the first place.
