# Clipper2Lib

The C# library of [Clipper2](https://github.com/AngusJohnson/Clipper2), by Angus Johnson, compiled into
GeometryHelper from source instead of referenced as a package. `Internal/Planar/ClipperRegion.cs` resolves every region
of the plane with it: the booleans of `Boolean2`, the raw loops an offset builds, and the pieces of a mesh.

## Where it comes from

Clipper2 2.0.0: the folder `CSharp/Clipper2Lib` at commit
[`f39457d`](https://github.com/AngusJohnson/Clipper2/tree/f39457d9b795beb936ca0c99c123dfaca5fb5f74/CSharp/Clipper2Lib)
of 17 December 2025. The repository has no tag for 2.0.0; the tag `Clipper2_2.0.1` has the same C# library. Built on
its own, that commit gives the IL of the `Clipper2` 2.0.0 package on nuget.org, method for method.

| File | What it holds |
|---|---|
| `Clipper.Core.cs` | points, paths, rectangles, `ClipType`, `FillRule`, `InternalClipper` |
| `Clipper.Engine.cs` | the sweep that clips: `ClipperBase`, `Clipper64`, `ClipperD`, `PolyTree64`, `PolyTreeD` |
| `Clipper.cs` | the static `Clipper` front: `Union`, `Intersect`, `InflatePaths`, `Area`, `SimplifyPaths` and the rest |
| `Clipper.Offset.cs` | `ClipperOffset` |
| `Clipper.RectClip.cs` | `RectClip64`, `RectClipLines64` |
| `Clipper.Minkowski.cs` | Minkowski sum and difference |
| `Clipper.Triangulation.cs` | constrained Delaunay triangulation (beta upstream) |
| `HashCode.cs` | `System.HashCode` for .NET Standard 2.0 |
| `PooledList.cs` | the pools the engine reuses its objects from |

## Changes from upstream

These, and nothing else:

1. Every type declared directly in the namespace is `internal` instead of `public` (33 types). They are not part of
   GeometryHelper's API, and a project that references both GeometryHelper and the Clipper2 package sees one
   `Clipper2Lib` only. Members keep their `public`.
2. `#nullable enable` at the top of `Clipper.Offset.cs`, `Clipper.Triangulation.cs`, `HashCode.cs` and
   `PooledList.cs`. Upstream turns nullable reference types on in its project file, GeometryHelper does not.
3. LF line endings and no byte order mark, as every file in this repository.

The namespace stays `Clipper2Lib`. The Z variant (`USINGZ`, namespace `Clipper2ZLib`) is not compiled: GeometryHelper
does not define the symbol.

Compiled into GeometryHelper, the library gives the IL of the package again, but for the visibility of the 33 types
and one delegate. GeometryHelper is built with the latest C#, whose compiler, from C# 11 on, keeps the `Comparison`
that `ClipperBase.ConvertHorzSegsToJoins` sorts with in a static field instead of making a new one on each call
(upstream builds with C# 8). `HorzSegSort` is static and holds no state, so the sort is the same.

When you change a file here, add the change to the list above, so that it is carried over when the folder is next
brought up to date with upstream.

## Its own tests

Clipper2's C# tests (`CSharp/Tests/Tests1`) run with GeometryHelper's, in `tests/GeometryHelper.UnitTest/Clipper2Lib`,
moved from MSTest to xUnit, with the cases they read from `Tests/` of the Clipper2 repository. Clipper2 2.0.0 fails one
of them upstream as well: test 16 of `Polygons.txt`, a triangle less a triangle that cuts it in two, comes back as one
piece where two are stored. `TestClosedPath16` holds that case, skipped. `TestCasesFoundHere` holds a case found here,
a union 2.0.0 gives within 0.09 % of its area, against the fix below.

## Bringing it up to date

1. Copy the `.cs` files of `CSharp/Clipper2Lib` at the new commit over these.
2. Make the changes above again.
3. Run the tests, Clipper2's own among them.
4. Write the commit here.

Upstream has fixed the C# library twice since 2.0.0, and neither fix is in this copy:
[`6a36be1`](https://github.com/AngusJohnson/Clipper2/commit/6a36be1) of 16 January 2026, in `Clipper.Triangulation.cs`
(#1052, #1055, #1056), and [`4da1564`](https://github.com/AngusJohnson/Clipper2/commit/4da1564) of 22 February 2026,
`FixSelfIntersects` in `Clipper.Engine.cs` (#1067), which mends test 16. GeometryHelper calls `ClipperD` only, and every
closed path it hands back passes through `FixSelfIntersects`; it does not call the triangulation.

`4da1564` is not taken as it stands. On 20 000 random cases of loops crossing themselves and each other, on integers from
0 to 1 000, it changes 172 answers: 61 come nearer the area resolved a millionth finer, 92 go further from it, and the
union of `TestCasesFoundHere` goes from 0.09 % short to 1.2 % over. On integers from 0 to 40 it changes 656 of 20 000,
191 nearer and 413 further. At six decimals it changes none of 20 000, nor any of 20 000 cells cut as the grid mesh cuts
them; at two decimals it splits one path in two in 2 of 20 000. GeometryHelper's tests, and 100 000 random meshes, pass
with it and without it.

## Licence

Boost Software License 1.0, in [LICENSE](LICENSE). `HashCode.cs` is the .NET Foundation's `System.HashCode` under the
MIT licence, with Yann Collet's xxHash32 under the BSD 2-Clause licence. Its notices are at the top of the file.
