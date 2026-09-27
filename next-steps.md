# Next steps

Agreed on 2026-09-27 after the review of `Geometry` and `Core`. Every step: a test that fails first, the
fix, a clean `dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental`, all five suites
(main, IfcConvert, TeklaConvert for 2020/2025/2026), commit, push.

## Bugs, in this order

1. **Solid booleans leave a sheet inside the body.** `Boolean3.TryGlue` cancels the two copies of a face
   between kept cells only when they match vertex for vertex; a copy cut in two, or carrying a point along
   its edge, survives with its twin. Cancel faces lying back to back by the area they share. `Shells3` must
   not throw on a group of faces that cannot be a body.
2. **Crossings under a degree read as parallel.** `Intersection2`/`Intersection3` compare the sine of the
   angle with `EqualAngleSin` whatever the length. Two members are parallel only when they draw apart by
   less than `EqualPoint` along the longer of them.
3. **An arc or an edge against a `GeoCircle2` measures to the rim.** Read the disc, as `GeoLine2` does.
4. **NaN passes the size checks** of `GeoCircle2`, `GeoCircle3`, `GeoObb3`, `GeoRectangle2`, `GeoRay3`.
5. **`IsClosed` says open** for T-junctions and for bodies meeting along an edge. Match edges stretch by
   stretch.

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
