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

## 2. The types expose less than the core can do

An audit of `src/GeometryHelper/Geometry` on 26 September 2026 found roughly **55 to 60 directions** that
`Core` already computes and no type offers. Almost none of it needs new arithmetic; it is wiring, and it is
the same gap closed twice already in the plane.

The plane is nearly complete. What is left there:

| Type | Missing |
|---|---|
| `GeoArc2` | `GetShortestLineTo`, `CollidesWith` |
| `GeoEdge2` | `CollidesWith` |
| `GeoFace2` | `GetShortestLineTo`, `CollidesWith`, `GetIntersections`, `GetClosestPointOnBoundary` |

Space is where the gap is:

| Operation | In `Core` | Directions no type offers |
|---|---|---|
| `GetShortestLineTo` | 9 pairs in `Projection3` | **11** — only `GeoLine3` and `GeoSolid3` have it |
| `CollidesWith` | 13 pairs in `Collision3` | **13**, a triangle against another triangle included |
| `GetIntersections` | 5 pairs returning many points | **10** |
| `TryIntersectWith` | | **6** |
| `Translate` | trivial | **13 of the 14** types in space |
| `GetClosestPointOnBoundary` | | `GeoFace2`, `GeoFace3`, `GeoPlane3` |

Two stand out: **`GeoFace3` has no `DistanceTo` at all**, though `Distance3.DistanceTo(GeoFace3, GeoPoint3)`
exists; and **`GeoArc3` is the barest type in the library** — no `CollidesWith`, no `GetIntersections`, no
`Translate`.

### Three steps, each leaving something usable

1. **The cheap asymmetries.** `Translate` on the 13 types in space; `GetClosestPointOnBoundary` on
   `GeoFace2`, `GeoFace3` and `GeoPlane3`; `GeoFace3.DistanceTo`.
2. **Meeting.** `CollidesWith` and `GetIntersections`/`TryIntersectWith` both ways round in space — 29
   directions, wiring throughout.
3. **Joining.** `GetShortestLineTo` in space — 11 directions — and the three left in the plane above.

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
- **`GetClosestEdge` takes a primitive probe only** — a point, a segment, a circle or an arc. The nearest
  edge of one many-edged shape to another is a *pair* of edges, which is a different answer.
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
