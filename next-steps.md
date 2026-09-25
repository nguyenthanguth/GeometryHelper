# Next steps

What is outstanding as of 25 September 2026, with the version at 6.0.0 and 5.1.0 published to nuget.org.

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

Worth putting in the release notes, since it is the whole of the break: `GetClosestOnBoundary` →
`GetShortestLineTo`, `GetClosestSegment` → `GetShortestLineTo`, and on the two curved chains →
`GetClosestEdge`.

## 4. Not done, and worth knowing

- **Cross-family collisions and intersections.** A straight shape can now be measured against a curved one
  with `DistanceTo` and `GetShortestLineTo`, but `CollidesWith` and `GetIntersections` still run one way
  only: `loop.CollidesWith(polygon)` compiles, `polygon.CollidesWith(loop)` does not. The Core statics
  exist for everything but `GeoRectangle2`, so this is mostly wiring.
- **`GeoSolid3` against a ray or a circle in space.** `Distance3` has no solid-to-ray or solid-to-circle
  pair, and both want a new primitive rather than a new arrangement of the ones already there.
- **`GetClosestEdge` takes a primitive only**, a point, a segment, a circle or an arc. The nearest edge of
  one many-edged shape to another is really a pair of edges, which is a different answer from the one the
  name promises, so it is not offered.

## Optional, and not urgent

The five retired packages — `CommonGeometry`, `PlaneGeometry`, `SolidGeometry`, `ArrangeAlgorithms` and
the unyeared `TeklaConvert` — are deprecated and unlisted on nuget.org, every version, so a search for
"GeometryHelper" returns only the six current packages. They are still on **GitHub Packages**, which has
neither unlisting nor deprecation: the choices there are to delete a package or make it private. There is
little to gain, because that registry answers anonymous reads with 401, so nothing in it is publicly
visible in the first place.
