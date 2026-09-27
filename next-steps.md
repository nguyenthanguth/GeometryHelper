# Next steps

Agreed on 2026-09-27 after the review of `Geometry` and `Core`. Every step: a test that fails first, the
fix, a clean `dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental`, all five suites
(main, IfcConvert, TeklaConvert for 2020/2025/2026), commit, push.

## Bugs - done

1. Solid booleans left a sheet inside the body; two shapes counted as lying in one plane when parallel within
   a degree through one shared point (`91a9a47`).
2. Crossings under a degree read as parallel - members, member and plane, arc and plane, face and cutting
   plane; the ray cast behind `Locate` lost crossings the same way (`9217cfc`).
3. An arc or an edge measured a `GeoCircle2` to its rim; two arcs one inside the other's circle were
   measured facing each other; distance to a concave `GeoPolygon3` walked its fan (`b5b3367`).
4. NaN passed the size checks and `TryGetNormal` (`c4243a1`).
5. `IsClosed` called T-junctions and bodies meeting along an edge open (`cff3070`).

## Extensions, after the bugs

1. `GeoSolid3.Prepare()` - a body asked many questions: material cut once, index and box kept.
2. `Clash3` - checking many parts at once: broad phase, in parallel, one result per clash.
3. Making bodies - extrude, sweep along a bar, revolve, cylinder.
4. Section by a plane, mass properties, `GeoObb3` fitted to points, convex hulls.
5. Validity - `IsValid` on the value types (a `default` one is not a shape), `MakeValid` for a polygon
   crossing itself, a scoped `Tolerance`.
6. The review's invariant sweeps as seeded tests in the suite.
7. Export for looking at the geometry - OBJ, SVG, WKT.
8. The members the chains lack beside each other (`StartPoint`, `EndPoint`, `MidPoint`, the split shapes).
