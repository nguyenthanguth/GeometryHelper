# Progress

The list of work on the drawing stack. Part A is the 2D presenter (`GeometryHelper.Cad`, `.Cad.Wpf`, and its app
`GeometryHelper.Cad.App`), which works like AutoCAD 2021. Part B is model drawings (`GeometryHelper.Drawing`), the
Tekla-like side.

- `[x]` done and merged into `main`; `[ ]` to do; `(running)` an agent is on it now.
- The maintainer adds, removes or reorders items here; the lead takes the next open item from the top, ticks it when it
  is merged, and adds what a finished item leaves behind.
- A UI item is designed in the Design artifact first, then built from the design.
- The full item-by-item history of Part A (about 1 100 lines) is in this file at commit `ddf1ff27`
  (`git show ddf1ff27:PROGRESS.md`).

## Part A: the presenter (finished, 2026-11-17)

The maintainer judged Part A done on 2026-11-17. What it covers, in short:

- Files: DXF R12 to R2018 and our own DWG reader and writer (R2000 to R2018), opened by AutoCAD 2021 with AUDIT 0;
  RECOVER, AUDIT, autosave, templates, xrefs, raster images, PDF underlays, OLE frames, WMF in and out.
- Drawing and editing: AutoCAD 2021's 2D draw and modify commands with their options and prompts, grips and grip modes,
  object snaps and tracking, UCS, selection, undo, clipboard, blocks and dynamic blocks, attributes, groups, arrays,
  constraints, MLINE, regions, hatches and gradients.
- Annotation: text and MTEXT with its editor, tables, fields, dimensions (associative, annotative) and multileaders.
- Layouts: viewports (double click in, snaps through them, resize and preview as AutoCAD), layers per viewport, PLOT,
  PUBLISH, page setups, plot styles, PDF and image export.
- The app: ribbon, command line with AutoComplete, Properties with the selection box, palettes, managers and dialogs,
  Global settings, shortcuts and acad.pgp aliases, plugins.

### Parked follow-ups

Small parity and speed follow-ups left by finished items. They stay parked under "features first" unless the
maintainer meets one as a real fault. Details for each are in the history at `ddf1ff27`.

- [ ] Checks only the AutoCAD GUI can give: LAYERP with U and the Layer manager; the guessed prompts of SPELL,
      ATTIPEDIT, MLEADERALIGN Spacing/Distribute; DIMBREAK Auto breaks; filter.nfl; Ctrl cycling of grip modes; the
      Properties kind list order and its reset to All
- [ ] Dialog polish (design3+design4): one footer button order, error brush, MTextMask header drag, SelectTemplate list,
      Plot styles/Publish layout details
- [ ] Ribbon at 125 % and 150 % display scaling; Home panels a few pixels wider than the design
- [ ] PLOT on real printers and Microsoft Print to PDF; -PLOT choosing a printer; smaller PDFs (glyph subsetting)
- [ ] Trans-spatial dimensions (DIMASSOC 2 through a viewport); typed snaps and OTRACK through a viewport; viewports
      with a 3D view direction entered
- [ ] MLINE edits at mitred corners and CJ pairs; annotative turned words on an extension line; MTEXT rare codes
      and column grips; table long texts and data links in old versions
- [ ] DWG and DXF version leftovers: R2013 AuxHeader numbers, spline normals off XY, R12 gradient meshes, version
      converter number parsing
- [ ] Xrefs, REFEDIT, managers and hatch dialogs: GUI-only checks and small wording differences
- [ ] Core: Try audit follow-ups 6 (NaN-shape members)

## Part B: model drawings

The active part. The maintainer may change this list before work starts.

Left by the Part A cleanup (2026-11-12), for the Part B replan: the Drawing app's own settings dialog (ExportLayout,
OpenAfterExport, presets, sources; SettingsSources moved into DrawingWorkspace); hidden lines now drawn in the layer's ACI
8 grey, not #9AA4B1; the model icon on the New offer; the shared "This line belongs to the model" refusal text and the
plugin "drawing" kind names.

- [x] G1 Hidden-line projection written to DXF
- [x] G2 Views (viewports), the drawing document, owners
- [x] G3 Dimensions
- [x] G4 Marks and symbols
- [x] G5 The sheet
- [x] U1 Model drawing UI: drawing list, New drawing, Update, Export, Drawing settings
- [x] P1 Drawing plugins: the API, a sample, Run plugin
- [ ] G0 The maintainer's list of Tekla drawing faults to compare with
- [ ] G3 Dimension chain texts that fit
- [ ] G6 step 4: comparison with Tekla; Tekla welds are unreadable
- [ ] U1 checked against the maintainer's own UI design
- [ ] P1 picks on the canvas and a Run button in the Plugin manager

## Release

- [ ] Review by the maintainer of everything above
- [ ] Packages `GeometryHelper.Drawing` and `GeometryHelper.Cad` on NuGet (only when the work is complete)
