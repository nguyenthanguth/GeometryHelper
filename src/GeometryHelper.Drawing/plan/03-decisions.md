# Decisions

Each decision has a number that never changes. A decision is **proposed** until the maintainer approves it, then
**approved**; a decision overturned stays in the list, marked **replaced by Dn**, so that the reason is not lost.

## Approved

| # | Decision | Why |
|---|---|---|
| A1 | One kind of drawing. Single-part, assembly and general arrangement drawings are presets of settings, not types or phases of their own. | The maintainer, 2026-11-08: the difference is which objects and which rules, not the code. |
| A2 | The output is DXF written through `DxfDatabase`. | It already writes every entity a drawing needs and was checked against AutoCAD 2021; drawings then open in any CAD without Tekla. |
| A3 | Tekla is a reference only through its Open API, its user documentation and its exported output. Its binaries are not decompiled. | Its licence, and the MIT package on NuGet; the maintainer asked about Drawings.dll on 2026-11-08 and this was the answer. |
| A4 | The module lives in `src/GeometryHelper.Drawing`. | The maintainer made the folder, 2026-11-08. |
| A5 | The structure is planned before any code; G1 (hidden-line projection to DXF) is the first phase of code. | The maintainer, 2026-11-08. |

## Proposed

| # | Decision | Recommended because | The other way |
|---|---|---|---|
| D1 | The DXF is the sheet at 1:1 in model space; each view is drawn at its scale; dimensions set `DIMLFAC` to the scale, so they read model lengths. | Every DXF viewer shows it as printed. How Tekla's own DWG export lays a drawing out is to be checked in G0. | Model space at 1:1 of the model and one layout viewport per view: native to AutoCAD, but each view's hidden lines differ, so each would need its own copy, and many viewers show only model space. |
| D2 | Hidden lines are removed exactly on the planar faces of the solids; edges between faces of a faceted curved surface are kept only where they are silhouettes. | The solids are polyhedra already; exact work on them keeps the library's tolerances. | Through a triangle mesh and a depth buffer: simpler, but the lines come out at pixel accuracy. |
| D3 | Holes and bolts come from the source's data (Tekla bolt groups and holes) first; finding holes in bare geometry comes later, with IFC. | Tekla knows them exactly; finding them in faceted geometry is a project of its own. | Find them in the geometry from the start. |
| D4 | `GeometryHelper.Drawing` is a package of its own, released with the others under one version. | It is large, and users of the geometry need not take it; the folder A4 matches the other packages. | A namespace in the core package. |
| D5 | Settings are C# objects with presets; reading them from JSON comes later if wanted. | Least code; tests build them directly. | JSON from the start. |
| D6 | Chains of short segments that a faceted circle or arc leaves (holes, tubes, rounded corners) are fitted back into arcs before they are dimensioned and written. | A hole dimensioned by its centre and drawn as a CIRCLE is what a draughtsman expects. | Keep the facets. |
| D7 | Tests in a project of their own, `tests/GeometryHelper.Drawing.UnitTest`. | Matches one suite per library and keeps the core suite's time. | Inside `GeometryHelper.UnitTest`. |
| D8 | DXF version AC1027 (AutoCAD 2013) unless G0 finds an entity that needs another. | MLEADER and tables are written well there, and AutoCAD 2021 opens it. | R2000 as `DxfWriter` writes, which has no MLEADER. |
| D9 | Third-angle projection by default, first-angle as a setting. | To be checked against the maintainer's Tekla settings in G0. | First-angle by default (ISO in Europe). |

## Open questions

| # | Question | Who |
|---|---|---|
| Q1 | Which faults of Tekla's drawings matter most? They set the order of work in G3 to G5. Collected in [40-tekla-bugs.md](40-tekla-bugs.md). | the maintainer |
| Q2 | The title block: the maintainer's own (a DWG or DXF to read the layout from, never committed) or one drawn in code? | the maintainer |
| Q3 | Which standard for weld symbols: ISO 2553 or AWS A2.4? | the maintainer |
| Q4 | The scales allowed (for example 1:1, 1:2, 1:5, 1:10, 1:15, 1:20, 1:25, 1:50, 1:100) and the sheet sizes used. | the maintainer, defaults in G0 |
| Q5 | The marks: what a part mark and an assembly mark show (position, profile, length, material). | the maintainer, defaults in G0 |
