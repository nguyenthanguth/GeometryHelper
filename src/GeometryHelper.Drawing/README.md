# GeometryHelper.Drawing

Shop and general arrangement drawings made from 3D models, written as DXF: the views of the objects with their
visible and hidden lines and sections, dimensions, marks and a sheet with its title block and part list. It is meant to
do what Tekla Structures' drawings do, without their faults, and without Tekla in the loop below the model reading.

**Status: planning.** There is no code yet. The plan below is the reference for building it: read it before starting
a phase, and keep it current as decisions are taken.

## The plan

| File | What it holds |
|---|---|
| [plan/00-overview.md](plan/00-overview.md) | the goal, the one kind of drawing, scope, what is left out, how Tekla may be used as a reference |
| [plan/01-architecture.md](plan/01-architecture.md) | the layers L0 to L6, how data flows through them, the public API in outline, the rules every layer keeps |
| [plan/02-project-structure.md](plan/02-project-structure.md) | the folders and files, the project file, tests, benchmarks, docs and how the verify takes them in |
| [plan/03-decisions.md](plan/03-decisions.md) | the numbered decisions, proposed or approved, and the questions still open |
| [plan/04-phases.md](plan/04-phases.md) | the phases G0 to G7, each step with its check, and where each stands |
| [plan/10-source.md](plan/10-source.md) | L0: the objects a drawing is made from |
| [plan/11-projection.md](plan/11-projection.md) | L1: hidden-line removal and sections |
| [plan/12-views.md](plan/12-views.md) | L2: views, their frames, scales and crops |
| [plan/13-annotation.md](plan/13-annotation.md) | L3: dimensions, marks, centre lines and symbols |
| [plan/14-sheet.md](plan/14-sheet.md) | L4: the sheet, the title block, the part list, placing the views |
| [plan/15-output-dxf.md](plan/15-output-dxf.md) | L5: what is written to the DXF and how |
| [plan/16-settings.md](plan/16-settings.md) | L6: the settings and their presets |
| [plan/17-tekla-bridge.md](plan/17-tekla-bridge.md) | reading a Tekla model into a drawing source |
| [plan/20-test-cases.md](plan/20-test-cases.md) | the bodies built in code that every phase is checked on |
| [plan/30-research.md](plan/30-research.md) | open-source work and documents to study, and what was taken from each |
| [plan/40-tekla-bugs.md](plan/40-tekla-bugs.md) | the faults of Tekla's drawings this module has to get right |
