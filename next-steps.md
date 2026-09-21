# Next steps

What is outstanding as of 21 September 2026, with `main` at `a26a040` and the version at 5.1.0.

## 1. `GetClosestOnBoundary` on the curved chains is misnamed

Everywhere else in the library, `GetClosestOnBoundary` returns the **shortest segment connecting** two
shapes. `Projection2.GetClosestSegment` says so in as many words: *the shortest line segment connecting a
point on the boundary of poly to a point on line*.

`GeoPolygonArc2` and `GeoPolylineArc2` took that name for something else — the **edge of the shape**
nearest the other thing. Against the same square and the same far-off segment:

| Call | Returns | What it means |
|---|---|---|
| `GeoPolygon2.GetClosestOnBoundary(line)` | `(100,50)..(300,50)`, length 200 | the connecting segment |
| `GeoCircle2.GetClosestOnBoundary(line)` | `(60,50)..(300,50)`, length 240 | the connecting segment |
| `GeoPolygonArc2.GetClosestOnBoundary(line)` | `(100,0)..(100,100)`, length 100 | an edge of the square |

The lengths give it away: 200 and 240 are distances between the shapes, 100 is the side of the square.

**The fix**: rename the two curved ones to `GetClosestEdge`, which is what they do. Because the return
type differs — `GeoEdge2` against `GeoLine2` — anything calling them stops compiling rather than quietly
changing behaviour, which is the failure worth having.

**Not part of this fix**: giving the curved chains a real `GetClosestOnBoundary` that returns the
connecting `GeoLine2`. The library knows the **distance** from an arc to a segment, an arc and a circle
(`Core.Arc2.DistanceTo`), but not the **pair of points** that distance is measured between, and the
connecting segment cannot be drawn without them. That is its own piece of work; better to leave the name
unused than to put it on something approximate.

**Decision needed first.** These methods shipped in 5.0.0, so renaming breaks a published API. Either fix
it inside 5.1.0, which is not released yet — the recommendation, since 5.0.0 is a day old — or hold it
for 6.0.0 and carry the wrong name until then.

## 2. A test that passes without testing anything

`tests/GeometryHelper.UnitTest/Plane/Core/PlaneMirrorTests.cs`, inside
`ACircleFindsTheSameNearestPieceWhicheverWayItIsAsked`:

```csharp
GeoLine2 nearest = circle.GetClosestOnBoundary(Polyline());
Assert.True(Distance2.DistanceTo(circle, nearest) <= Distance2.DistanceTo(circle, edge) + 1E-9);
```

`nearest` is the connecting segment, and one of its ends sits on the circle, so the left-hand side is
always nought and the assertion reads `0 <= anything`. It is green and it checks nothing; the comment
above it is wrong for the same reason. Worth fixing alongside the rename, since both come from the same
misreading.

## 3. Releasing 5.1.0

The repository is at 5.1.0 and nuget.org is at 5.0.0. To release, **create a GitHub Release tagged
`v5.1.0`**: `.github/workflows/release.yml` fires on `release: published` and publishes all six packages.

Do not use *Run workflow* on that workflow to try it out. It has no dry run, and the two push steps are
not guarded by the event type, so a manual run publishes to nuget.org and GitHub Packages for real.

## Optional, and not urgent

The five retired packages — `CommonGeometry`, `PlaneGeometry`, `SolidGeometry`, `ArrangeAlgorithms` and
the unyeared `TeklaConvert` — are deprecated and unlisted on nuget.org, every version, so a search for
"GeometryHelper" returns only the six current packages. They are still on **GitHub Packages**, which has
neither unlisting nor deprecation: the choices there are to delete a package or make it private. There is
little to gain, because that registry answers anonymous reads with 401, so nothing in it is publicly
visible in the first place.
