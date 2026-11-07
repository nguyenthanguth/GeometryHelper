# Overview

## The goal

The maintainer builds Tekla Structures tools, and Tekla's drawings have too many faults to rely on. This module makes
the drawings itself: it reads the objects of a model, draws their views with visible and hidden lines and sections,
dimensions and marks them, lays the views out on a sheet with a title block and a part list, and writes a DXF that
AutoCAD, DWG TrueView or the repository's own viewer opens.

## One kind of drawing

Tekla has single-part, assembly, cast-unit and general arrangement drawings, each with its own code and settings. Here
there is one `Drawing`. What a drawing shows is decided by two things only:

- the objects given to it: one plate, a beam with its fittings, a whole frame;
- the settings it is made with: which views, which scales, which dimensions and marks.

A drawing of one plate and a drawing of a frame go through the same code. A "single-part" or "general arrangement"
drawing is a preset of settings, not a type.

## Scope

In:

- steel parts and assemblies: plates, profiles, tubes, cuts, chamfers, holes, bolts, welds;
- views along the drawing's axes, sections, details, an isometric view;
- automatic dimensions, marks, centre lines, bolt and weld symbols, with nothing overlapping;
- a sheet of a standard or custom size, a title block, a part list, a revision table;
- a DXF written through `DxfDatabase`, with real DIMENSION entities, layers and linetypes;
- reading the objects from Tekla through its Open API.

Later, once the above works:

- concrete parts with reinforcement and bending schedules;
- updating a drawing when the model changes, keeping what was moved by hand;
- PDF output;
- objects read from IFC.

Out:

- editing drawings interactively; the drawing is made, then opened in CAD;
- writing back into Tekla's own drawings, which would bring back Tekla's faults.

## Tekla as a reference

Tekla is the yardstick, used only in these ways:

- its Open API, to read the model and the settings of existing drawings;
- its user documentation, for the concepts and options a drawing has;
- its output: a drawing Tekla makes for an object is exported to DXF or DWG and set beside ours for the same object.

Tekla's binaries are never decompiled or copied from. Its licence forbids it, and the package is published under MIT
on NuGet, where nothing can be taken back. Code written from Tekla's code would also bring its faults with it.

## Success

- Every view of the test bodies in [20-test-cases.md](20-test-cases.md) has the lines a draughtsman would draw, checked
  by numbers in tests and by eye in AutoCAD.
- Every fault listed in [40-tekla-bugs.md](40-tekla-bugs.md) has a test that our drawing passes.
- A drawing of an assembly of 50 parts is made in under a second, and of a frame of 1 000 parts in under a minute
  (targets, to be measured in G1 and set firm in G0).
- The DXF opens in AutoCAD 2021 with nothing for AUDIT to fix.
