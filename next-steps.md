# Next steps

What is outstanding as of 21 September 2026, with `main` at `a26a040` and the version at 5.1.0.

## 1. The `GetClosestOnBoundary` family is misnamed

Everywhere in the library, `GetClosestOnBoundary` returns the **shortest segment connecting** two shapes.
`Projection2.GetClosestSegment` says so in as many words: *the shortest line segment connecting a point on
the boundary of poly to a point on line*.

The name does not say that. It reads as *the closest thing on my boundary*, which is a different idea and
an easy one to act on: `GeoPolygonArc2` and `GeoPolylineArc2` took the name for exactly that, and return
the **edge of the shape** nearest the other thing. Against the same square and the same far-off segment:

| Call | Returns | What it means |
|---|---|---|
| `GeoPolygon2.GetClosestOnBoundary(line)` | `(100,50)..(300,50)`, length 200 | the connecting segment |
| `GeoCircle2.GetClosestOnBoundary(line)` | `(60,50)..(300,50)`, length 240 | the connecting segment |
| `GeoPolygonArc2.GetClosestOnBoundary(line)` | `(100,0)..(100,100)`, length 100 | an edge of the square |

The lengths give it away: 200 and 240 are distances between the shapes, 100 is the side of the square.

It misleads all the more for sitting beside `GetClosestPointOnBoundary(point)`, which really does return a
point on the shape. Dropping `Point` looks like a change of return type rather than a change of meaning.

### The names, decided

| Method | Returns | Now called |
|---|---|---|
| `GetClosestPointOnBoundary(point)` | a point **on** this shape | unchanged, it is already right |
| `GetClosestOnBoundary(other)` | the segment **joining** the two | **`GetShortestLineTo(other)`** |
| `GetClosestOnBoundary(other)` on the curved chains | an **edge** of this shape | **`GetClosestEdge(other)`** |

`Shortest` is what removes the ambiguity: no existing edge can be picked by being "shortest to X", so the
word can only describe a segment built to be as short as possible. `GetClosestLineTo` was considered and
keeps the trap — *the closest line* still reads as *the closest line of mine*.

### Scope

- 58 public `GetClosestOnBoundary` methods across `GeoLine2`, `GeoCircle2`, `GeoRectangle2`,
  `GeoPolygon2`, `GeoPolyline2`, `GeoLine3`, `GeoPolygonArc2` and `GeoPolylineArc2`
- the 54 `Projection2.GetClosestSegment` statics they call, which carry the same ambiguity
- the two on the curved chains, which change meaning rather than just name, and whose different return
  type — `GeoEdge2` against `GeoLine2` — means a caller stops compiling instead of quietly changing
  behaviour

### Not part of this

Giving the curved chains a real `GetShortestLineTo` returning the connecting `GeoLine2`. The library knows
the **distance** from an arc to a segment, an arc and a circle (`Core.Arc2.DistanceTo`), but not the
**pair of points** that distance is measured between, and the connecting segment cannot be drawn without
them. That is its own piece of work; better to leave the name unused on those two types than to put it on
something approximate.

### Still open: which version

Renaming 58 public methods breaks a published API — these shipped in 5.0.0. The recommendation is to make
both renames together, since they are one mistake, and release the result as **6.0.0** rather than 5.1.0.
Odd-looking a day after 5.0.0, but it is the honest number, and 5.1.0 is not out yet.

The alternative, keeping the 58 old names as `[Obsolete]` forwarders for one release, is not recommended:
it doubles the surface of the library to serve a release that is a day old and has almost certainly not
been taken up.

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

## 3. Releasing

The repository is at 5.1.0 and nuget.org is at 5.0.0, so there is a release to make either way. Which
number it carries depends on the question left open above: 5.1.0 as things stand, or 6.0.0 if the renames
go in first, which is what is recommended.

Set `GeometryHelperVersion` in `Directory.Build.props` to match before tagging — it is the one literal
every package reads, including the Tekla ones through `$(GeometryHelperVersion).$(TeklaVersion)`.

To release, **create a GitHub Release tagged with that version**:
`.github/workflows/release.yml` fires on `release: published` and publishes all six packages.

Do not use *Run workflow* on that workflow to try it out. It has no dry run, and the two push steps are
not guarded by the event type, so a manual run publishes to nuget.org and GitHub Packages for real.

## Optional, and not urgent

The five retired packages — `CommonGeometry`, `PlaneGeometry`, `SolidGeometry`, `ArrangeAlgorithms` and
the unyeared `TeklaConvert` — are deprecated and unlisted on nuget.org, every version, so a search for
"GeometryHelper" returns only the six current packages. They are still on **GitHub Packages**, which has
neither unlisting nor deprecation: the choices there are to delete a package or make it private. There is
little to gain, because that registry answers anonymous reads with 401, so nothing in it is publicly
visible in the first place.
