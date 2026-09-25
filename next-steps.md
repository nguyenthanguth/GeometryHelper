# Next steps

What is outstanding as of 26 September 2026. The version is **6.0.0**; nuget.org is at 5.1.0.

Work already finished is not described here — it is in the git log and in `PackageReleaseNotes` in
`src/GeometryHelper/GeometryHelper.csproj`, which is what the release body should carry. What is kept below
is the part still open, and the decisions worth not re-opening.

## 1. Releasing 6.0.0

`Directory.Build.props` is at **6.0.0** — a major number because 6.0.0 renames a published API and changes
how an arc decides what it reaches. It is the one literal every package reads, including the Tekla ones
through `$(GeometryHelperVersion).$(TeklaVersion)`.

To release, **create a GitHub Release tagged `v6.0.0`**: `.github/workflows/release.yml` fires on
`release: published` and publishes all six packages.

Do not use *Run workflow* on that workflow to try it out. It has no dry run, and the two push steps are not
guarded by the event type, so a manual run publishes to nuget.org and GitHub Packages for real.

The notes are already written under **NEW IN 6.0.0**. Copy that text into the release body.

## 2. Nothing, and how to check that again

**`Core` and `Geometry` agree everywhere.** There is no pair `Core` computes that some type will not answer,
whichever of the two shapes is in hand. That took five passes: `Translate` and the nearest point, then
meeting, then joining, then the matrix that showed what the audit table had missed, then the last of it.

To check it again after any change, re-derive the matrix rather than trusting this paragraph. Read every
`public` member of `src/GeometryHelper/Geometry/*.cs` and every `public static` of `src/GeometryHelper/Core`,
take the first two `Geo…` arguments of each static as an unordered pair, and report every pair for which
neither shape offers the operation. Two things that scan wrongly and must be allowed for:

- a static whose result is an `out` parameter (`TryIntersectWith`) puts its shapes first, so take the
  arguments in order and stop at two;
- a shape against its own sort reads as one pair, and it is easy to skip. Box-to-box, plane-to-plane and
  triangle-to-triangle distance were missing for exactly that reason.

What remains open is a question of meaning, not of wiring:

| Pair | Missing | Why it is not simply wired |
|---|---|---|
| `GeoFace2` | `DistanceTo` against a shape | the reading is unsettled: nought where they touch, or the reach to the nearest edge of the material? `GetShortestLineTo` answers the second, and `DistanceTo` to a point answers the first |
| `GeoArc3` and the curved chains in space | `CollidesWith`, `GetIntersections`, anything but a point | no closed form; see the arc entry under *Settled* |

**`GeoArc3` is still the barest type in the library**: it moves, and it answers about a point. That is
deliberate.

## 3. What needs a machine with Tekla

`GeometryHelper.TeklaConvert` compiles against 2020, 2025 and 2026, and everything but the read itself is
covered by tests that run without Tekla, because the set-out types turn out to be plain holders. What cannot
be checked here:

- that `RebarGeometry.Shape` and `BendingRadiuses` hold what they are taken to hold;
- **whether Tekla gives one radius per bend or one per point.** `PointConvert.ByVertex` reads a short list as
  one per bend and a full-length list as one per vertex, and tests pin both readings, but only the modeller
  can say which arrives;
- **whether turning the work plane to global really settles the 2020 lapping offset.** The 2020 package does
  it and puts the plane back; 2025 and 2026 compile it away through the `TeklaVersion2020` symbol.

## Settled, and worth not re-opening

- **`DistanceTo` stays unsigned.** Turning its sign over is a break no compiler can catch, and it is also
  asked of two shapes, where a sign would have to mean penetration depth. `SignedDistanceTo` sits beside it,
  as `SignedArea` sits beside `Area`.
- **Whether an arc reaches a point is measured as a distance, never as an angle.** An angular tolerance of a
  degree is nearly two units on a radius of a hundred, which once made arcs look nearer than they are and
  reported crossings clean off the end of an arc.
- **`GeoPolygonArc3` enforces coplanarity; `GeoPolylineArc3` does not.** That is what keeps area, `Locate`,
  `Offset` and `Fillet` exact on the loop: each projects into its own plane and lifts the answer back.
- **A bulge is stored with the plane it bulges in.** Deriving that plane from the neighbouring legs breaks on
  reversing a chain, on splitting one, and at either end.
- **Only a corner between two straight legs is rounded in space.** A curved leg lies in a plane of its own.
- **An arc in space is not measured against anything but a point**, and neither is a circle in space against
  a solid. No closed form; `ToPolyline3(chordTolerance)` puts the accuracy in the call, and a sampled chain
  lies inside its arcs, so a clearance errs on the safe side.
- **A joining segment leaves the shape it was asked of and lands on the other.** `Core` computes each pair
  one way round only, so half the directions on the types turn the answer over. Handing it back as it comes
  gives a segment of the right length pointing backwards, which no length assertion catches.
- **A face's boundary is its outline together with the rim of every hole**, and a probe reaches its material
  when it reaches the outline and no hole holds it whole. A probe crossing no rim is inside that hole,
  outside it, or wrapped around it; the three are told apart by where one point of the probe falls and, for
  a probe with an inside, whether the rim falls within the probe. A ring drawn around a hole looks exactly
  like a speck lying in one until that last question is asked.
- **Two arcs of one circle collide when either holds an end of the other**, not where they cross: two
  circles lying on each other meet along their length rather than at points, so the crossing code answers no
  for two arcs sharing an end.
- **A shape may be asked about its own sort.** Box against box, plane against plane, triangle against
  triangle: each was missing because a pair of one kind reads as one entry and is easy to pass over when
  walking a list of pairs looking for the two directions.
- **`GetClosestEdge` takes a primitive probe only** — a point, a segment, a circle or an arc — and that rule
  is about the *probe*, not about which of the two makes the call. `point.GetClosestEdge(polygon)` is one edge
  and no ambiguity, so it is offered; `polygon.GetClosestEdge(otherPolygon)` is a pair of edges, so it is not.
  This was misfiled once as though the whole reverse direction were settled against.
- **`GetClosestEdge` on the straight shapes takes no tolerance**, because `Core.ClosestEdge2` has no such
  overload for them; only the curved ones do. Any wiring generated for both must follow that.
- **A rebar is only ever read as Tekla works it out.** Set-out readings were written and dropped: those
  points are hardly used and gave a second answer that differed from the model.
- **`GetClosestPointOnBoundary` on `GeoAabb3` and `GeoObb3` clamps into the box**, so an interior point comes
  straight back rather than a point on the surface. The name says otherwise, and
  `Projection3.ProjectToObbSurface` is the real one. Not renamed: a third silent break in one release is too
  many. Worth doing in 7.0.0.

## Verifying

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
