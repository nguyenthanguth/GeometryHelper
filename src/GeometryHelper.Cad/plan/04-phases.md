# Phases

A phase ends when:
- CLAUDE.md's full verify is green;
- a DXF it writes opens in AutoCAD with nothing for AUDIT to fix;
- for a UI phase, the screens match the UI design, checked by screenshots side by side;
- the maintainer has looked at the result before the next phase starts, and nothing is committed until they say so.

Each step is written `[step] -> verify: [check]`. This file holds Part A, the 2D presenter. Part B, the model drawing, moved to
the private repository TeklaHelper.DrawingManager on 2026-11-18 with its phases (G0 to G7, U1, P1).

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
| C10 | A | Style forms as AutoCAD: the Text Style dialog (STYLE) and the Dimension Style Manager (DIMSTYLE) with the style boxes of the Annotate tab, and a style of text, dimension and multileader applied by the commands | queued 2026-11-09 |
| C11 | A | Modify commands: TRIM, EXTEND, OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, BREAK, JOIN, LENGTHEN, ALIGN | queued 2026-11-09 (Part A first) |
| C12 | A | Clipboard (COPYCLIP, CUTCLIP, PASTECLIP, COPYBASE, PASTEBLOCK) and text edited in place (TEXTEDIT, the MTEXT editor) | queued |
| C13 | A | Blocks and attributes: BLOCK, INSERT, WBLOCK, ATTDEF, ATTEDIT, a light block editor | queued |
| C14 | A | Dimension editing: DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK | queued |
| C15 | A | PLOT and PDF | queued |
| C16 | A | The snaps left: Extension, Parallel, Apparent Intersection, Insertion, object snap tracking | queued |
| C17 | A | QSELECT, FIND, DRAWORDER, GROUP, PURGE, selection cycling | queued |
| C18 | A | Tables, external references, images | queued |
| C19 | A | Speed at scale: open, display and edit of drawings of a million entities | queued |

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
   folders, one undo step per run -> verify: see docs/presenter/plugins.md, checks for command plugins.
3. The Plugin manager dialog, the plugin template -> verify: install, load, unload, reload and remove from the UI.

### C5 to C10: requests of 2026-11-09

Written as the maintainer asked; each is a phase with its own checks when it starts.

- **C5.** The drawings list opens a drawing on a double click, not a click; the right-click menu has Open, Close, Close selected, Close other, Close all, each through the window's close path (the question about unsaved changes included). -> verify: view-model tests; a click only selects.
- **C6.** Drafting Settings dialog with the OSNAP tab (Endpoint, Midpoint, Center, Geometric Center, Node, Quadrant, Intersection, Extension, Insertion, Perpendicular, Tangent, Nearest, Apparent Intersection, Parallel, Object Snap On, Tracking), a status-bar drop-down of the modes, the Shift+right-click menu, and AutoCAD's marker shapes and colours at the cursor. -> verify: each mode on a test drawing gives the exact point; screenshots beside AutoCAD's.
- **C7.** UCS in the plane first (Origin, Z rotation, 3point, Object, View, World, Previous, Save, Restore, Delete, the UCS icon, PLAN), kept in the file's UCS table, with grid, snap, ortho, polar, the crosshair, typed coordinates and the readout following it; the 3D parts (X and Y rotation, Face, a Z axis out of the plane) follow only if the presenter ever shows 3D. -> verify: a point typed in a rotated UCS lands where AutoCAD puts it; the saved file reads in AutoCAD with the same UCS.
- **C8.** RECOVER and AUDIT in the presenter and a tolerant reader: a damaged file (bad handles, missing sections, dangling owners, invalid values, a truncated tail) opens with what can be read and a report of what was repaired. Fonts: SHX shape files read from the AutoCAD Fonts folder of the user's machine (never bundled), TrueType through the system, substitution where one is missing, the STYLE command. -> verify: damaged copies of synthetic drawings; text of an SHX style drawn as AutoCAD draws it.
- **C9.** LINETYPE (manager, load from a .lin file, create simple and complex linetypes with text and shapes, LTSCALE, CELTSCALE) and HATCH (pattern, user-defined, solid, gradient; pick an internal point or select objects; associative; island styles; patterns from a .pat file) with HATCHEDIT. No Autodesk file is shipped: a small set of our own patterns and linetypes, and the user's files loaded from disk. -> verify: written files open in AutoCAD with AUDIT clean and draw the same.
- **C10.** Style forms, which the first plan lacked (the gap report lists them as missing; C8 only names a STYLE command). The **Text Style** dialog (STYLE): list of styles, preview, Set Current, New, Delete; font name (TrueType families and SHX files found), font style, Use Big Font, height, width factor, oblique angle, backwards, upside down, vertical; the styles are the file's STYLE table, one undo step per Apply, a style in use cannot be deleted. The **Dimension Style Manager** (DIMSTYLE, D): the styles list with preview, Set Current, New, Modify, Override, Compare; the tabs Lines, Symbols and Arrows, Text, Fit, Primary Units, Alternate Units, Tolerances, each field one dimension variable (DIMCLRD, DIMEXE, DIMEXO, DIMASZ, DIMBLK..., DIMTXSTY, DIMTXT, DIMGAP, DIMTAD, DIMTIH, DIMATFIT, DIMSCALE, DIMLFAC, DIMLUNIT, DIMDEC, DIMPOST, DIMALT..., DIMTOL...) written into the DIMSTYLE table record as AutoCAD writes it (groups 3, 40-47, 71-79, 140-148, 170-179, 270-279, 340, 341-345...) with the preview drawn from the same dimension block builder the presenter draws with, so the picture and the dimension agree. The style boxes of the Annotate tab (text style, dimension style, multileader style) and the current-style variables (TEXTSTYLE, DIMSTYLE current) drive what TEXT, MTEXT, the DIM commands and MLEADER create. The model drawing's own dimension styles (GH_MODEL_10, GH_MODEL_20, ...) are marked as owned by the model drawing: a hand change in the Manager is allowed as an override and is kept by an update, like any hand edit. -> verify: view-model tests; a style set in the dialog is written as AutoCAD writes it and AutoCAD draws a dimension in it the same (AUDIT clean, one rendered comparison); screenshots beside AutoCAD's dialogs.

### C4: the full drafting commands (on hold)

TRIM, EXTEND, OFFSET, FILLET, CHAMFER, BREAK, JOIN, STRETCH, ARRAY, HATCH editing, block and attribute editing, xref,
and the rest. Nothing here starts until a command is needed (A18). Until then AutoCAD opens the DXF.

