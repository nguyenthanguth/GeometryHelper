# GeometryHelper.Drawing

Two parts, one window (A16):

- **Part A, a 2D presenter like AutoCAD.** It opens, shows and edits any 2D drawing held in a `DxfDatabase`: a DXF from
  anywhere, model space and layouts with viewports, layers, AutoCAD's commands and shortcuts, and command plugins loaded
  at any time as NETLOAD does. It knows nothing of models or Tekla.
- **Part B, a model drawing like Tekla's.** It makes drawings from a 3D model (Tekla, IFC, code): views with visible and
  hidden lines and sections, dimensions and marks tied to model objects, a sheet with its title block and part list,
  update from the model with hand edits kept, and drawing plugins that work like Tekla's. It writes each sheet into
  Part A's document.

Part B uses Part A; Part A never refers to Part B. Open a DXF and you get Part A; open or make a model drawing and Part B
adds its panels, ribbon tab and commands on top. Both cores run with no UI; the UI is a set of reusable controls and a
standalone app.

**Status: built, not yet packed.** Both parts exist and have guides; the plan below is still the reference for what is left.

- **Part A, the 2D presenter,** is `GeometryHelper.Dxf.Viewer` (the core, no window), `GeometryHelper.Dxf.Viewer.Wpf` (the
  controls) and `GeometryHelper.Dxf.App` (its own window, GeometryHelper DXF). It opens a DXF as a `CadDocument`, draws it from display lists,
  picks and selects, edits as AutoCAD does (command line with 32 commands and their aliases, object snaps, grips, Properties,
  layers, undo and redo that give the file back byte for byte), keeps the entities of an owner under the owner contract, loads
  command plugins while it runs, keeps global settings as JSON, autosaves and closes drawings. The guides start at
  [The 2D presenter](../GeometryHelper/docs/presenter/index.md).
- **Part B, the model drawing,** is `GeometryHelper.Drawing` (the core) and `GeometryHelper.Drawing.Wpf` (the dialogs and the
  Drawing tab), with `DrawingSourceConvert` in `GeometryHelper.TeklaConvert` reading Tekla. It makes views with exact
  hidden-line removal, sections and details, dimensions by rule, marks, centre lines, weld, section, detail and level marks and
  a sheet with its title block and part list, writes them into one DXF with its own document, and makes the drawing again from
  the model with the hand edits kept. The guides start at [Model drawings](../GeometryHelper/docs/drawing/index.md).
- **Not built yet:** the drawing plugins of [plan/60-plugins.md](plan/60-plugins.md) (the document records their runs, nothing
  runs them), the drafting commands of C4 beyond the 32 of C2, weld symbols to AWS A2.4, and a circular crop of a detail.

Where the plan and the code differ, the code and the guides are right: a command plugin, for one, is an iterator that yields
its prompts, not the blocking method the plan's sample shows. Read the plan before starting a phase, and keep it current as
decisions are taken.

## Where things live

The plan lives in the files below and nowhere else. The UI design is a claude.ai Design artifact
(https://claude.ai/artifact/J3yNSVKeLNqd6aJnnVQMZ3): an overview, the main window in ten states (A1 to A6 for Part A, B1
to B4 for Part B), every dialog and four components, all on one canvas. The maintainer edits it by hand.

## The plan

| File | What it holds |
|---|---|
| [plan/00-overview.md](plan/00-overview.md) | the goal, the two parts, scope of each, what is left out, how Tekla and AutoCAD may be used |
| [plan/01-architecture.md](plan/01-architecture.md) | the layers K1 to K5 (Part A) and L0 to L8 and plugins (Part B), data flow, the owner contract, the public API in outline, the rules every layer keeps |
| [plan/02-project-structure.md](plan/02-project-structure.md) | the five projects, folders and files, tests, benchmarks, docs and how the verify takes them in |
| [plan/03-decisions.md](plan/03-decisions.md) | the numbered decisions, approved or proposed, and the questions still open |
| [plan/04-phases.md](plan/04-phases.md) | Part A phases C1 to C4, Part B phases G0 to G7, U1 and P1, each step with its check |
| [plan/70-presenter.md](plan/70-presenter.md) | Part A: the document, display, editing, layers, reading and writing, checks |
| [plan/10-source.md](plan/10-source.md) | L0: the objects a drawing is made from |
| [plan/11-projection.md](plan/11-projection.md) | L1: hidden-line removal and sections |
| [plan/12-views.md](plan/12-views.md) | L2: a view is a window into the model (like a Tekla view or AutoCAD's MVIEW) |
| [plan/13-annotation.md](plan/13-annotation.md) | L3: dimensions, marks, centre lines and symbols |
| [plan/14-sheet.md](plan/14-sheet.md) | L4: the sheet, the title block, the part list, placing the views |
| [plan/15-output-dxf.md](plan/15-output-dxf.md) | L5: what Part B writes into the DXF document and how |
| [plan/16-settings.md](plan/16-settings.md) | Global settings (shared) and drawing settings (presets, Part B) |
| [plan/17-tekla-bridge.md](plan/17-tekla-bridge.md) | reading a Tekla model into a drawing source |
| [plan/20-test-cases.md](plan/20-test-cases.md) | the bodies built in code that every Part B phase is checked on |
| [plan/30-research.md](plan/30-research.md) | open-source work and documents to study, and what was taken from each |
| [plan/40-tekla-bugs.md](plan/40-tekla-bugs.md) | the faults of Tekla's drawings Part B has to get right |
| [plan/50-ui.md](plan/50-ui.md) | the interface: layout, the screens of the design, interaction, shortcuts and commands |
| [plan/60-plugins.md](plan/60-plugins.md) | the two kinds of plugin, loading and unloading, safety, the samples |
| [plan/51-figma-prompts.md](plan/51-figma-prompts.md) | superseded: prompts for Figma's AI, kept as a record only |
