# Phases

A phase ends when:
- CLAUDE.md's full verify is green;
- a DXF it writes opens in AutoCAD with nothing for AUDIT to fix;
- for a UI phase, the screens match the UI design, checked by screenshots side by side;
- the maintainer has looked at the result before the next phase starts, and nothing is committed until they say so.

Each step is written `[step] -> verify: [check]`. Two parts (A16), each with its own phases; Part A goes first because
Part B shows its sheets in it.

| Phase | Part | What | Status |
|---|---|---|---|
| C1 | A | The viewer: open a DXF, show it, zoom, pan, select, Properties (read), layers, layouts and viewports | built, awaiting review |
| C2 | A | Light editing: grips, move, copy, erase, rotate, scale, Properties edited, simple drawing commands, text and dimension edits, undo, save | built, awaiting review |
| C3 | A | Command plugins: load, unload, reload | built, awaiting review |
| C4 | A | The full drafting commands (trim, extend, offset, fillet, hatch, ...), later, one by one, as they are needed (A18) | later |
| C5 | A | The drawings list: double click opens, right click menu (Open, Close, Close selected, Close other, Close all) | queued 2026-11-09 (maintainer) |
| C6 | A | Object snap interface: Drafting Settings (OSNAP tab), the status-bar snap menu, Shift+right-click snap menu, AutoCAD's markers | queued 2026-11-09 |
| C7 | A | UCS as AutoCAD: UCS command (World, Object, View, Origin, ZAxis, 3point, X/Y/Z, Previous, Save, Restore, Delete), UCSICON, PLAN, named UCS in the file; grid, ortho, polar, snap and coordinates follow it | queued 2026-11-09 |
| C8 | A | Repair of DXF files (RECOVER, AUDIT) and fonts (SHX and TrueType, font substitution, STYLE) | queued 2026-11-09 |
| C9 | A | Linetypes as AutoCAD (LINETYPE manager, load from .lin, create simple and complex, LTSCALE) and HATCH creation (patterns, solid, gradient, from a point or objects) with HATCHEDIT | queued 2026-11-09 |
| G8 | B | Several sheets one after another: views, part list and title block flow onto Sheet 1, 2, 3 ... when they do not fit one | queued 2026-11-09 |
| G0 | B | Research and spec | spec done; the maintainer's list of Tekla faults not collected |
| G1 | B | Hidden-line projection, written to DXF | built, awaiting review |
| G2 | B | Views (viewports) and the drawing document L7, owners | built, awaiting review |
| G3 | B | Dimensions | built, awaiting review (chain texts fitting: in progress) |
| G4 | B | Marks and symbols | built, awaiting review |
| G5 | B | The sheet | built, awaiting review |
| G6 | B | Presets, the Tekla bridge, update from the model, comparison with Tekla | steps 1 to 3 built; Tekla welds unreadable; step 4 not done |
| U1 | B | Model drawing UI: drawing list, New drawing, model properties, Update, Export, Drawing settings | built; not yet checked against the maintainer's own UI design |
| P1 | B | Drawing plugins: the API, a sample, the Plugin manager's Drawing tab | built: Run plugin on the Drawing tab and `PLUGIN`; picks on the canvas and a Run button in the Plugin manager not done |
| G7 | both | Docs, changelog, release | guides and changelog done; packages and release wait for the maintainer |

The UI design is a claude.ai Design artifact (see the README), made first; each UI step is built against it.

## Part A

### C1: the viewer

1. `CadDocument` opens a DXF through `DxfReader` and keeps the `DxfDatabase` -> verify: every DXF the repository's DXF
   tests write opens, and the entity counts per layer match.
2. Display lists per space and per viewport: LINE, ARC, CIRCLE, ELLIPSE, LWPOLYLINE, SPLINE, TEXT, MTEXT, HATCH,
   DIMENSION, INSERT, LEADER, MLEADER, TABLE, WIPEOUT, IMAGE; layers (on, off, frozen, colour), linetypes, lineweights
   -> verify: screenshots of the DXF test files beside AutoCAD's, entity kind by entity kind.
3. The canvas in the standalone app (SkiaSharp): zoom about the cursor, pan, zoom extents, regen -> verify: a drawing of
   100 000 entities opens in under 3 seconds and pans smoothly; timings in a benchmark.
4. Layouts and viewports: the layout tabs, a viewport shows its model-space window at its scale, frozen layers per
   viewport -> verify: a layout with two viewports at 1:20 and 1:5 shows both as AutoCAD does.
5. Selection by click, window and crossing; Properties (read-only) for one and for several entities; the layer list ->
   verify: view-model tests; selecting 1 000 entities by window takes under 50 ms.
6. The shell of the window as the design shows it (ribbon, command line, status bar), Global settings with Apply (the
   canvas background changes at once) -> verify: screenshots beside the design; the background survives a restart.

### C2: light editing

1. The command framework: a command is a list of prompts (point, distance, selection, keyword), with the command line,
   dynamic input, Enter and Esc as AutoCAD's -> verify: view-model tests drive a command with no window.
2. Object snaps and ortho, polar, grid snap -> verify: each snap kind on a test drawing picks the exact point to the point
   tolerance.
3. Grips on lines, arcs, circles, polylines, text and dimensions -> verify: dragging each grip changes the entity as
   AutoCAD's does.
4. MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER and the layer manager -> verify: each leaves the
   DXF valid; each is one undo step.
5. Properties edited in place: layer, colour, linetype, geometry, text, dimension overrides -> verify: an edit is one
   undo step and writes the same DXF as AutoCAD would for it.
6. LINE, PLINE, CIRCLE, ARC, TEXT, MTEXT, DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, MLEADER, MVIEW -> verify: the
   saved DXF opens in AutoCAD with AUDIT clean.
7. Save and Save as; undo and redo across all of it; the owner contract (entities with an owner ask it first) -> verify:
   a test owner refuses a move and the entity stays.

### C3: command plugins

1. `[CadCommand]` methods and `CadCommandContext` -> verify: a sample command runs, asks for two points and draws a line.
2. `PLUGINLOAD` / `NETLOAD`, `PLUGINUNLOAD`, `PLUGINRELOAD` through collectible load contexts and shadow copies, trusted
   folders, one undo step per run -> verify: see [60-plugins.md](60-plugins.md), checks for command plugins.
3. The Plugin manager dialog, the plugin template -> verify: install, load, unload, reload and remove from the UI.

### C5 to C9 and G8: requests of 2026-11-09

Written as the maintainer asked; each is a phase with its own checks when it starts.

- **C5.** The drawings list opens a drawing on a double click, not a click; the right-click menu has Open, Close, Close selected, Close other, Close all, each through the window's close path (the question about unsaved changes included). -> verify: view-model tests; a click only selects.
- **C6.** Drafting Settings dialog with the OSNAP tab (Endpoint, Midpoint, Center, Geometric Center, Node, Quadrant, Intersection, Extension, Insertion, Perpendicular, Tangent, Nearest, Apparent Intersection, Parallel, Object Snap On, Tracking), a status-bar drop-down of the modes, the Shift+right-click menu, and AutoCAD's marker shapes and colours at the cursor. -> verify: each mode on a test drawing gives the exact point; screenshots beside AutoCAD's.
- **C7.** UCS in the plane first (Origin, Z rotation, 3point, Object, View, World, Previous, Save, Restore, Delete, the UCS icon, PLAN), kept in the file's UCS table, with grid, snap, ortho, polar, the crosshair, typed coordinates and the readout following it; the 3D parts (X and Y rotation, Face, a Z axis out of the plane) follow only if the presenter ever shows 3D. -> verify: a point typed in a rotated UCS lands where AutoCAD puts it; the saved file reads in AutoCAD with the same UCS.
- **C8.** RECOVER and AUDIT in the presenter and a tolerant reader: a damaged file (bad handles, missing sections, dangling owners, invalid values, a truncated tail) opens with what can be read and a report of what was repaired. Fonts: SHX shape files read from the AutoCAD Fonts folder of the user's machine (never bundled), TrueType through the system, substitution where one is missing, the STYLE command. -> verify: damaged copies of synthetic drawings; text of an SHX style drawn as AutoCAD draws it.
- **C9.** LINETYPE (manager, load from a .lin file, create simple and complex linetypes with text and shapes, LTSCALE, CELTSCALE) and HATCH (pattern, user-defined, solid, gradient; pick an internal point or select objects; associative; island styles; patterns from a .pat file) with HATCHEDIT. No Autodesk file is shipped: a small set of our own patterns and linetypes, and the user's files loaded from disk. -> verify: written files open in AutoCAD with AUDIT clean and draw the same.
- **G8.** The model drawing flows onto several sheets one after another (Sheet 1 of N ...) when its views, part list and title block do not fit one sheet, each sheet a layout of the same DXF, views not cut across a sheet, the title block numbers set. This is the reading as the model drawing; if the maintainer meant Part A's layouts, those already hold any number. -> verify: a general arrangement that needs three sheets gives three layouts; nothing overlaps or leaves a sheet; update keeps them.

### C4: the full drafting commands (on hold)

TRIM, EXTEND, OFFSET, FILLET, CHAMFER, BREAK, JOIN, STRETCH, ARRAY, HATCH editing, block and attribute editing, xref,
and the rest. Nothing here starts until a command is needed (A18). Until then AutoCAD opens the DXF.

## Part B

### G0: research and spec

1. Study FreeCAD TechDraw, OpenCascade's HLR and build123d's projection, Tekla's user documentation on drawings and
   drawing plugins, and AutoCAD's MVIEW and layouts -> verify: [30-research.md](30-research.md) says what each does and
   what is taken from it.
2. Collect the faults of Tekla's drawings from the maintainer -> verify: each in [40-tekla-bugs.md](40-tekla-bugs.md) with
   what Tekla does and what is right.
3. Build the test bodies of [20-test-cases.md](20-test-cases.md) in code -> verify: each is a closed, valid `GeoSolid3` (by
   `Validate`) and its volume is the one worked out by hand.
4. Check the .NET runtime of Tekla 2020, 2025, 2026 and the AutoCAD in use (D12) -> verify: written into 03-decisions.md.
5. Settle the proposed decisions, the names of the public types, the owner contract and the file format of the drawing
   document inside the DXF (A19) -> verify: every D in [03-decisions.md](03-decisions.md) approved or replaced, and the layer files carry
   the final names.

### G1: hidden-line projection

1. Edge extraction: sharp edges, silhouettes, smooth edges dropped -> verify: counts per test body worked out by hand.
2. Occlusion: each edge cut into visible and hidden pieces -> verify: the three main views of every test body against
   lines worked out by hand, to the point tolerance.
3. Edges shared by touching objects drawn once -> verify: TC8 gives no doubled line.
4. Arcs fitted to faceted circles (D6) -> verify: every hole of TC1 and TC2 comes out as one circle of its diameter.
5. Sections: the cut faces and what lies behind the cut -> verify: section areas against `GeoSolid3.Section`.
6. A first DXF of the three views, lines on layers by visibility, shown in Part A (C1) -> verify: opens in AutoCAD, AUDIT
   clean; the maintainer looks at it.
7. Speed -> verify:
   - benchmark on TC10, timings recorded;
   - a fuzz run of random boxes and cuts: no exception, and no edge both visible and hidden.

### G2: views and the drawing document

1. The drawing frame from the source (the main object's coordinate system) -> verify: TC2 lying along any axis gives the
   same views.
2. A view is a viewport (A11): direction, scale, crop, depth; its lines made again when they change -> verify: changing one
   view's crop makes the lines of that view only.
3. Views along the axes, sections, details, isometric; the scale chosen from the allowed list -> verify: per view, the
   lines of G1, and each view fits its space on the sheet.
4. The drawing document L7: stable ids, model links, hand-edit records, save and open (D15) -> verify: saved and opened
   again gives the same drawing.
5. Owners (D24): every entity written carries its owner and id; `DrawingOwner` answers Part A -> verify: in Part A a
   projected line cannot be moved, a mark can, and the move is a hand-edit record; undoing it removes the record.

### G3: dimensions

1. Points of interest of a view: ends, outline corners, hole and bolt centres, work points -> verify: per test body.
2. Rules: overall, chain, cumulative, ordinate, holes by group -> verify: dimension values against the model.
3. Tiers outside the view, spacing, no dimension crossing another or the text of another -> verify: a test counts
   overlaps, and it is zero on every test body.

### G4: marks and symbols

1. Part, assembly and bolt marks placed through `Arranging` -> verify: no overlaps, every leader on its object.
2. Centre lines, section and detail marks, levels, weld symbols -> verify: per test body.

### G5: the sheet

1. Sheet size and frame, title block filled from the source and settings -> verify: attribute values in the DXF.
2. Part list as a DXF table -> verify: rows and quantities against the source.
3. Views placed through `SheetPacker` with their annotation -> verify: nothing overlaps, nothing off the sheet.
4. DXF export as layout and viewports (D10) and as the sheet in model space -> verify: both open in AutoCAD with AUDIT
   clean, each viewport at its scale.

### G6: presets, Tekla bridge, update, comparison

1. Presets for a part, an assembly and a general arrangement -> verify: each test body drawn with each preset.
2. `DrawingSourceConvert` reading assemblies, parts, bolts and welds -> verify:
   - TeklaConvert tests in code;
   - a run against the maintainer's running Tekla only with their go-ahead.
3. Update when the model changes, keeping hand edits -> verify: TC2 made longer and updated, the overall dimension follows,
   a mark dragged by hand stays where it was, a line drawn by hand on the sheet is still there.
4. Side by side with Tekla's exported drawing of the same objects -> verify: each fault of
   [40-tekla-bugs.md](40-tekla-bugs.md) shown right in ours.

### U1: the model drawing UI

Details: [50-ui.md](50-ui.md).

1. The drawing list (search, filters, grouping by source), opening a drawing in Part A's canvas -> verify: view-model
   tests; the drawings of TC1 to TC5 open.
2. Model properties in Properties for a view, a dimension and a mark; the Drawing ribbon tab -> verify: screenshots beside
   the design.
3. New drawing, Drawing settings, Update from model and Export dialogs -> verify: flow "make, edit, update, export" runs
   from start to end.

### P1: drawing plugins

Details: [60-plugins.md](60-plugins.md).

1. `DrawingPlugin`, its input definition, the run context, declared settings, API version; plugins kept in the drawing
   and run again on update -> verify: a sample plugin that dimensions every bolt group runs on TC5; after the model
   changes and the drawing is updated its dimensions follow.
2. The Drawing tab of the Plugin manager, `.ghplugin` packages, the startup list -> verify: install, load, unload,
   reload, run and remove a drawing plugin from the UI.

Done so far: `DrawingPlugin`, `DrawingPluginInput`, `DrawingPluginContext`, `DrawingPluginCatalog`,
`Drawing.RunPlugin` and `Drawing.Plugins`; runs kept as `GH_PLUGIN_RUN` records and run again in `Prepare`; the sample
Bolt dimensions in `tests/GeometryHelper.Drawing.SamplePlugin`, run on TC5 and followed when its bolts move 20 apart, a
hand edit kept; drawing plugins loaded by the one `CadPluginManager` (`"kind": "drawing"`, its finder set by the
catalog), the collectible unload checked on .NET 8 by the Net8Runner; the Drawing chip of the Plugin manager lists them,
and its Load, Reload, Unload and Remove work on them; Run plugin on the Drawing tab (a dialog of the plugin, the view,
the objects ticked and the settings checked as typed) and `PLUGIN` on the command line, each one undo step through the
drawing's busy guard; a plugin's dimensions kept clear of the view's own (no overlap on TC5); a drawing plugin rebuilt
while loaded and reloaded runs its new code (the Net8Runner); a .dll from a folder not trusted asked about first. Not done:
picks on the canvas, a Run button in the Plugin manager (it lives in Part A, which knows no drawing; it needs a seam in
`MainViewModel.CreatePluginManager`), an alias for `PLUGIN` (`PL` is PLINE's), marks and lines added by a plugin.

## G7: docs, changelog, release

1. Guides in plain English with measured examples, a group in the docs site for each part with its own index page ->
   verify: docfx `--warningsAsErrors` 0 warnings, no menu node without an href.
2. Changelog **NEW.** entries, package release notes -> verify: the release check.
3. Release as CLAUDE.md says, only when the maintainer says so -> verify: all three workflows green.
