# Architecture

## Layers

| # | Layer | Job | Builds on |
|---|---|---|---|
| L0 | Source | the objects drawn: a `GeoSolid3` each, with its kind, name, mark, profile, material, parent and attributes | [10-source.md](10-source.md) |
| L1 | Projection | one view direction gives 2D edges, visible or hidden, and the faces a section cuts, each edge knowing its object | [11-projection.md](11-projection.md) |
| L2 | Views | the views of a drawing: along its axes, sections, details, isometric; their scale, crop and depth | [12-views.md](12-views.md) |
| L3 | Annotation | dimensions by rule, marks, centre lines, bolt and weld symbols, placed with nothing overlapping | [13-annotation.md](13-annotation.md) |
| L4 | Sheet | the sheet, its title block, part list and revision table, and where the views go on it | [14-sheet.md](14-sheet.md) |
| L5 | Output | the DXF: layers, linetypes, dimension styles, blocks, DIMENSION entities, hatches | [15-output-dxf.md](15-output-dxf.md) |
| L6 | Settings | what a drawing shows and how, and the presets | [16-settings.md](16-settings.md) |

The Tekla bridge sits outside the layers: it turns Tekla model objects into an L0 source
([17-tekla-bridge.md](17-tekla-bridge.md)).

## How data flows

```
Tekla model ──DrawingSourceConvert (TeklaConvert)──┐
objects built in code ─────────────────────────────┼──► DrawingSource (L0)
IFC (later) ───────────────────────────────────────┘          │
                                                              ▼
                        DrawingSettings (L6) ──► DrawingBuilder
                                                              │
        ┌─────────────────────────────────────────────────────┤
        ▼                                                     ▼
  ViewBuilder (L2) ── per view ──► ViewProjector (L1) ──► projected edges, section faces
        │                                                     │
        ▼                                                     ▼
  Annotator (L3): dimensions, marks, symbols ◄────── points of interest of each view
        │
        ▼
  SheetLayout (L4): view sizes ──► SheetPacker; title block, part list
        │
        ▼
  Drawing (the result, all in sheet millimetres) ──► DrawingDxfWriter (L5) ──► .dxf
```

`Drawing` is a plain result: the views with their lines, hatches, dimensions and marks, all in sheet coordinates, and
the sheet. It can be checked in tests without reading the DXF back, and written to other formats later (SVG for a quick
look through `SvgWriter`, PDF).

## The public API in outline

```csharp
using GeometryHelper.Drawing;

DrawingSource source = DrawingSourceConvert.FromAssembly(assembly);   // Tekla, or new DrawingSource(objects) in code
DrawingSettings settings = DrawingSettings.Assembly;                  // a preset, or settings built in code
Drawing drawing = DrawingBuilder.Build(source, settings);
drawing.SaveDxf("B-101.dxf");
```

Each layer can be used alone, as the library's other modules are:

```csharp
ProjectedView2 view = ViewProjector.Project(solids, viewFrame, tolerance);   // L1 alone: lines of a view
```

Names are proposals until G0 fixes them in the spec; names carry their dimension as a digit, as everywhere in the
library.

## Rules every layer keeps

- **No Tekla below L0.** Only `GeometryHelper.TeklaConvert` touches Tekla. Everything else takes `GeoSolid3` and plain
  data, so every test runs in code.
- **Millimetres.** The model is in millimetres; the sheet is in paper millimetres. A view's scale maps one to the other.
- **Tolerance.** Every comparison of coordinates goes through a `Tolerance` passed in; nothing compares with `==`.
- **Determinism.** The same source and settings give the same DXF to the byte: every sort has a full tie-break, and no
  result follows the order of a `Dictionary` or `HashSet`.
- **Guard and log.** Arguments are checked through `Guard`; `GeometryHelperLog` takes one Info summary per public call,
  a Warn for a caught exception and one aggregated Debug line for expected skips.
- **Speed traps.** No sort-and-sweep along a fixed axis (sweep the axis with the fewest pairs), no `(a << 32) | b` keys in
  a `Dictionary` without their own comparer.
- **Reuse.** Placing marks goes through `Arranging`, placing views through `Packing`, writing through `DxfDatabase`. A
  fault found in one of them is fixed there, with its own test, not worked around here.
