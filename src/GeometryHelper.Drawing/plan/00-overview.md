# Overview

## The goal

The maintainer builds Tekla Structures tools, and Tekla's drawings have too many faults to rely on. This module makes
the drawings itself: it reads the objects of a model, draws their views with visible and hidden lines and sections,
dimensions and marks them, lays the views out on a sheet with a title block and a part list, lets the user look at them
and edit them, and writes a DXF that AutoCAD, DWG TrueView or the repository's own viewer opens.

## Two parts (A16)

The work is split into two parts, one built on the other:

```
Part B: model drawing (like Tekla)    a 3D model -> views -> dimensions and marks tied to model objects
          |                           -> update from the model keeping hand edits; drawing plugins as Tekla's
          | writes its sheet into (one way only)
          v
Part A: 2D presenter (like AutoCAD)   open, show, select and edit a 2D drawing: model space, layouts and
          |                           viewports, layers, commands and shortcuts as AutoCAD's; command plugins
          | rests on
          v
GeometryHelper.Dxf (exists)           DxfDatabase, DxfLayout, DxfViewport; reading and writing DXF
```

- **Part A, the 2D presenter**, shows and edits any 2D drawing held in a `DxfDatabase`: a DXF from anywhere, or a sheet
  Part B made. It knows nothing of models, Tekla or drawings. On its own it is already useful: it opens every DXF.
- **Part B, the model drawing**, makes drawings from a model, the way Tekla does, and writes each sheet into a
  `DxfDatabase` that Part A shows. Its core runs with no UI at all: in a command-line tool, hosted in Tekla, in tests.
- **One way only.** Part B uses Part A; Part A never refers to Part B. Where Part B needs a say in editing (a projected
  line must not be dragged, a moved mark must survive an update), Part A asks the owner of the entity through one small
  contract (the owner contract in [01-architecture.md](01-architecture.md)).

One app holds both: open a DXF and you get Part A; open or make a model drawing and Part B adds its panels, its ribbon
tab and its commands on top.

## Why two parts

- Part A has its foundation already: `DxfDatabase` reads and writes layouts, viewports, dimensions, blocks and tables,
  checked against AutoCAD 2021. Part A is the screen and the editing on top of it.
- Plugins as Tekla has them (A15) need drawing objects that know their model object. A plain 2D editor cannot give that:
  its lines are only lines. So the Tekla-like plugins belong to Part B, and the AutoCAD-like command plugins to Part A.
- Rebuilding all of AutoCAD is a task with no end. Part A grows in steps (C1 to C4 in [04-phases.md](04-phases.md)), and
  the full drafting commands come only when they are asked for; until then AutoCAD itself opens the DXF.

## Standalone, usable in many places

GeometryHelper.Drawing is a presenter of drawings in its own right (A7), not tied to Tekla:

- **Sources plug in** (Part B): Tekla (through TeklaConvert), IFC (through IfcConvert), objects built in code. Each is
  only a converter into a `DrawingSource`.
- **Any DXF opens** (Part A), with no source at all.
- **The cores have no UI:** Part A's document, commands and snaps, and Part B's whole drawing, run without WPF.
- **The UI is a set of reusable controls:** the canvas, Properties, the layer list, the drawing list, Global settings.
  They run in the standalone app, and can be hosted in a Tekla plugin window.

## One kind of drawing (Part B)

Tekla has single-part, assembly, cast-unit and general arrangement drawings, each with its own code and settings. Here
there is one `Drawing`. What a drawing shows is decided by two things only:

- the objects given to it: one plate, a beam with its fittings, a whole frame;
- the settings it is made with: which views, which scales, which dimensions and marks.

A "single-part" or "general arrangement" drawing is a preset of settings, not a type (A1).

## Scope

Part A, in:

- opening and saving DXF through `DxfDatabase`; model space and layouts; viewports shown at their scale (A11);
- showing every entity `DxfDatabase` reads, with layers, colours, linetypes and lineweights;
- an AutoCAD-like interface (A8): the canvas in the middle, Properties on the right, a left panel listing the drawing files,
  the ribbon, the command line, the Model and Layout tabs and the status bar;
- selecting (click, window, crossing), grips, Properties edited in place, layers switched on and off;
- AutoCAD's shortcuts and commands by default (A12), editing commands in steps (C2), more drafting commands only when
  asked (C4);
- command plugins, loaded at any time as NETLOAD does, unloaded and reloaded (A14);
- Global settings, always there (A9): change the defaults (for example the canvas background, black by default) and
  Apply.

Part B, in:

- steel parts and assemblies: plates, profiles, tubes, cuts, chamfers, holes, bolts, welds;
- views as windows into the model (A11): views along the drawing's axes, sections, details, isometric; each with its
  direction, scale, crop and depth, its lines made again from the model;
- automatic dimensions, marks, centre lines, bolt and weld symbols, with nothing overlapping;
- a sheet of a standard or custom size, a title block, a part list, a revision table;
- the drawing document (L7): every object with an id and a link to the model, hand edits kept through an update;
- drawing plugins that work like Tekla's (A10, A15), on model objects;
- a drawing list, model properties in Properties, and the New drawing, Drawing settings, Update and Export dialogs;
- reading the objects from Tekla through its Open API.

Later, once the above works:

- concrete parts with reinforcement and bending schedules;
- PDF output and printing;
- IFC as a source;
- the full drafting commands of Part A (C4), command by command as the maintainer asks for them.

Out:

- any 3D in the UI (A13): no orbiting or rotating, no 3D model window; the canvas is a flat sheet with zoom and pan, and
  the 3D model is only projected into 2D views inside Part B's core;
- DWG files: Part A reads and writes DXF only;
- writing back into Tekla's own drawings, which would bring back Tekla's faults;
- decompiling Tekla's or Autodesk's binaries.

## Tekla and AutoCAD as references

Tekla and AutoCAD are the yardsticks, used only in these ways:

- Tekla's Open API, to read the model and the settings of existing drawings;
- the user documentation of Tekla and AutoCAD, for the concepts and options (views, MVIEW, plugins, settings, commands);
- their output: a drawing Tekla makes for an object is exported to DXF or DWG and set beside ours for the same object; a
  DXF Part A saves is opened in AutoCAD and checked with AUDIT.

Their binaries are never decompiled or copied from. Their licences forbid it, and the packages are published under MIT
on NuGet, where nothing can be taken back. The interface follows AutoCAD's layout and handling; it uses no Autodesk icons,
logos or artwork.

## Success

Part A:

- Every DXF the repository's DXF tests write, and the maintainer's own (looked at locally, never committed), opens and
  looks as it does in AutoCAD, checked by screenshots side by side.
- A DXF opened, edited and saved opens in AutoCAD 2021 with nothing for AUDIT to fix.
- A drawing of 100 000 entities opens in under 3 seconds and pans and zooms smoothly (targets set roughly, measured in
  C1).

Part B:

- Every view of the test bodies in [20-test-cases.md](20-test-cases.md) has the lines a draughtsman would draw, checked
  by numbers in tests and by eye.
- Every fault listed in [40-tekla-bugs.md](40-tekla-bugs.md) has a test that our drawing passes.
- A drawing of an assembly of 50 parts is made in under a second, and of a frame of 1 000 parts in under a minute
  (targets set roughly in G0, measured and fixed in G1).
- A mark moved by hand and a dimension edited by hand are still there after the model changes and the drawing is
  updated.

Both:

- The interface matches the UI design, checked by screenshots side by side.
- A sample command plugin and a sample drawing plugin run without any change to the module's code.
