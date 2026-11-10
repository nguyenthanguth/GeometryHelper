# Progress

The list of work on the drawing stack: Part A, the 2D presenter (`GeometryHelper.Dxf.Viewer`, `.Wpf`, and its app
`GeometryHelper.Dxf.App`), which aims to work like AutoCAD 2021 and replace it, and Part B, model drawings
(`GeometryHelper.Drawing`). Part A is finished, to AutoCAD's standard, before any work on Part B starts.

- `[x]` done and merged into `main`; `[ ]` to do; `(running)` an agent is on it now.
- The maintainer adds, removes or reorders items here; the lead takes the next open item from the top of each section,
  ticks it when it is merged, and adds what a finished item leaves behind.
- A UI item is designed in the Design artifact first, then built from the design.

## Part A: the presenter

### Done

- [x] C1 Viewer: open a DXF, display, zoom, pan, select, Properties, layers, layouts and viewports
- [x] C2 Light editing: grips, MOVE, COPY, ERASE, ROTATE, SCALE, Properties edited, draw commands, undo, save
- [x] C3 Command plugins: load, unload, reload
- [x] C5 Drawings list: double click opens, right-click Open / Close / Close selected / Close other / Close all
- [x] C6 Object snap UI: Drafting Settings, status-bar and Shift+right-click menus, AutoCAD's markers
- [x] C7 UCS, UCSICON, PLAN, named UCS; grid, ortho, polar and snap follow the UCS
- [x] C8 RECOVER and AUDIT; fonts (SHX, TrueType, FONTALT with the built-in Hershey font, FONTMAP, Missing SHX dialog)
- [x] C9 Linetypes (39 built in) and HATCH (48 patterns built in, solid, gradient) with HATCHEDIT
- [x] C10 Text Style and Dimension Style dialogs, style boxes on Annotate
- [x] Built-in resources: 20 arrowhead blocks, metric NEW template, point styles (PTYPE), pattern-file dialog
- [x] C11 Modify: TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN, LENGTHEN, OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, ALIGN
- [x] C12 Clipboard (Ctrl+C/X/V, COPYBASE, PASTEBLOCK, PASTEORIG, PASTESPEC), TEXTEDIT, the MTEXT editor
- [x] C13 Blocks and attributes: BLOCK, INSERT, WBLOCK, ATTDEF, EATTEDIT, ATTEDIT, BASE
- [x] C14 Dimension editing: DIMCONTINUE, DIMBASELINE, QDIM, DIMEDIT, DIMTEDIT, DIMSPACE, DIMBREAK
- [x] C15 PLOT, -PLOT, PREVIEW, PAGESETUP to vector PDF
- [x] C16 Snaps: Extension, Parallel, Apparent Intersection, Insertion, object snap tracking (F11)
- [x] C17 QSELECT, FIND, DRAWORDER, GROUP, PURGE, selection cycling (Ctrl+W)
- [x] C18b External references (DXF) and raster images (png, jpg, bmp)
- [x] XLINE and RAY: drawn, picked, snapped, previewed while moved
- [x] Picking takes the object drawn on top (draw order)
- [x] Ribbon shrinks to the window as AutoCAD's: Home, Annotate Dimensions panel, Text Editor tab
- [x] C19 Speed at scale: an edit patches the display instead of redrawing it (1-line move at 1 000 000 entities 10.6 s -> 0.6 s)
- [x] C18a Tables: TABLE, -TABLE, TABLESTYLE, TABLEDIT, editing cells, the Table Cell tab
- [x] Ribbon round 3: the Insert tab (Block, Block Definition, Reference, the block gallery); Annotate and View shrink to 1 280 px
- [x] XLINE and RAY: a pick or snap is handed only those passing near it (20 000 snaps with 200 rays 220 -> 60 ms on .NET 4.8)
- [x] GeometryHelper.Dxf.App: the presenter as an app of its own (`GeometryHelper.Dxf.App.exe plan.dxf`); the window and the
      screenshot mode shared with Drawing.App through `CadAppHost`
- [x] Blocks round 2: EATTEDIT live with Apply kept open, INSERT drags the block, -ATTEDIT picks one attribute, ATTSYNC,
      Block Definition previews the selection, redefine a block brought from another drawing
- [x] Associative dimensions (DIMASSOC 2): stored as AutoCAD stores them, following MOVE, STRETCH, grips, SCALE and the
      rest in the same undo step; DIMREASSOCIATE, DIMDISASSOCIATE; AutoCAD's own MOVE carries them too
- [x] MLEADERSTYLE: the Multileader Style Manager and Modify dialog to the design, styles written as AutoCAD writes them,
      MLEADER in the current style; MATCHPROP's Property Settings to the design
- [x] Layers per viewport: VP Freeze, New VP Freeze, VP Color, Linetype, Lineweight, Transparency, Plot Style (as AutoCAD
      stores them), VPLAYER, LAYVPI; the status bar's MODEL/PAPER, scale list and padlock to the design
- [x] Command line AutoComplete (most used first, aliases, variables, mid-string, synonyms, AutoCorrect, Input Settings,
      Recent Commands); the application menu with Recent Documents (pins, previews); the DXF app's own start screen
- [x] PEDIT (single and Multiple, every option) and REVCLOUD (Arc length, Object, Rectangular, Polygonal, Freehand, Style,
      Modify), checked vertex by vertex against AutoCAD's own where its console has the command
- [x] Dimension grips: extension origins, dimension line, text, chord points, angular and ordinate points; STRETCH of a
      grip edit measures anew
- [x] Model space through a viewport: double click in, MSPACE/PSPACE, MODEL/PAPER, Ctrl+R, ZOOM nXP, viewport scale in
      Properties and the status bar, Display locked
- [x] Properties: editable rows for ellipses, polylines (vertex browse), hatches, dimensions (overrides by group), MTEXT,
      INSERTs with attributes, viewports, multileaders; MATCHPROP with Settings and special properties
- [x] SETVAR and 47 system variables typed by name (OSMODE, CLAYER, FILLETRAD, PICKFIRST...), drawing ones one undo step,
      window ones kept in the settings file
- [x] Grip modes: Space cycles STRETCH, MOVE, ROTATE, SCALE, MIRROR with Base point, Copy, Undo, Reference; Ctrl copies;
      several hot grips with Shift
- [x] MIRRTEXT: mirrored TEXT, MTEXT and attributes stay readable as AutoCAD keeps them (rule measured on 105 cases)
- [x] Selection: "Select objects:" takes W, C, BOX, F, WP, CP, ALL, L, P, G, R, A, U, M, SI, AU; Shift removes in a command
- [x] Draw options: ARC every AutoCAD way and tangent continue, CIRCLE Ttr and Tan Tan Tan, PLINE widths and arc options,
      TEXT Justify and Style, MTEXT Justify, Line spacing, Rotation, Style, Width, Columns
- [x] Drafting aids: FROM, M2P/MTP, TT and point filters .X .Y .Z .XY .XZ .YZ, typed or from the Shift+right-click menu
- [x] Dimensions round 2: linear pictures laid out by DIMTAD, DIMJUST, DIMGAP, DIMTIH, DIMTOH and the fit rules (checked
      against 89 pictures AutoCAD drew), DIMORDINATE, DIMJOGLINE, DIM
- [x] Images of the view: PNGOUT, JPGOUT, BMPOUT as AutoCAD's (the drawing area as shown, all objects or those selected;
      1920 x 1080 of 100 000 entities written as PNG in 0.25 s)
- [x] AutoCAD parity check: 101 gaps and 19 partial items, listed in full in
      [73-autocad-parity.md](src/GeometryHelper.Drawing/plan/73-autocad-parity.md)

### In progress

### AutoCAD parity: High (daily use), in the order of value to a drafter

The Medium and Low items are in [73-autocad-parity.md](src/GeometryHelper.Drawing/plan/73-autocad-parity.md); copy here
the ones wanted.

- [ ] MIRRTEXT for multileaders, tables and dimension text overrides; text box measured with real fonts (S)
- [ ] -GROUP on an R2000 drawing with no ACAD_GROUP dictionary: "Object reference not set" (check on main) (S)
- [ ] Drafting aids: TK (TRACKING); check TT with OTRACK off, "(need YZ):" input and Enter at a modifier prompt
      against AutoCAD (S)
- [ ] Draw: AUDIT a drawing made with every ARC, CIRCLE, PLINE, TEXT and MTEXT option, and compare ARC and PLINE entity
      by entity with AutoCAD's own; check Ctrl, ANGDIR 1 and the MTEXT column defaults against AutoCAD (S)
- [ ] Properties: dimension rows Ext line fixed and length (core DIMFXLENON), Fill color, Fractional type, Text view
      direction, Text position X/Y; hatch origin kept through angle and scale; MATCHPROP plot style and gradient hatch (S)
- [ ] Variables (running): PICKADD, POLARMODE, ATTREQ, ATTDIA, FILEDIA, CMDDIA; AUTOSNAP's other bits; DYNMODE values; ORTHOMODE
      written to the drawing; new dimensions follow DIMSCALE; polar angle field in Global settings (S)
- [ ] Core (running): `DxfPolyline2d.CurveFit` tangents differ from AutoCAD's for closed polylines (AutoCAD bisects) and open ones of 4
      or more vertices (64.41 against 63.43 degrees in one case); `DxfPolyline3d.SplineFit` vertex order (M)
- [ ] PEDIT: PEACCEPT and PLINETYPE read through SETVAR; a spline turned into a polyline with a precision; 3D polylines;
      Extend for arc ends in a fuzz join (S)
- [ ] REVCLOUD: the 2021 approximate arc length (REVCLOUDARCVARIANCE), REVCLOUDPROPERTIES, a Freehand preview, Modify on
      more than closed polylines (S)
- [ ] Layouts: twisted viewports entered; one undo step per wheel or pan gesture; a ZOOM Previous list per viewport;
      SCALELISTEDIT (Custom… runs VPSCALE now); Rename and Isolate in the Layer manager; New VP Freeze on viewports made
      through the core's API, not only MVIEW (S)
- [ ] Styles (running): DICTIONARYVAR class in drawings made new, so Set Current works for multileader and table styles; MLEADER with
      no style makes Standard; Block and None content; the Annotative flag written (S)
- [ ] Associative dimensions (running): an aligned dimension with one end stretched (AutoCAD moves its line), a line and its dimension
      copied together stay tied, intersection and other snaps tied, ties from DIM, QDIM, DIMCONTINUE, DIMBASELINE, ordinate
      and two-line angular; check whether a plain MOVE leaves a presenter-made dimension's picture behind (flag 32) (M)
- [ ] Blocks: BEDIT, the Block Editor (L, design first) (design running)
- [ ] Blocks: dynamic blocks in use: custom grips (visibility, lookup, stretch, flip, rotate, array) and their Properties
      rows (L)

### To do
- [ ] Command line and Recent: previews read off the UI thread; AutoComplete inside a running command; Search Content;
      Recent Commands kept between sessions; missing files dropped from Recent; an EXPORT command (S)
- [ ] Dxf.App: an app icon, file association for .dxf (installer or registry), `--tab Home` in screenshot mode
- [ ] Images of an area: PNG and JPG devices in PLOT and -PLOT ("GeometryHelper PNG", "GeometryHelper JPG", as AutoCAD's
      PublishToWeb PNG/JPG), plot area Window, Extents, Display or Layout, size in pixels (1920 x 1080, 3840 x 2160 or
      typed) in place of a paper (design first)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [ ] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [ ] Tables: rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles
- [ ] Tables: set the current table style in drawings made new (DICTIONARYVAR class in the template)

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: disabled buttons barely look greyed (GhFlatButton); Home panels a few pixels wider than their design widths
- [ ] Speed: undo without reloading the whole drawing (2.5 s at 1 000 000 entities): first the cheap wins (pack the undo state in parallel chunks, build the handle index lazily), then a core `DxfDatabase` fork that shares the read bytes (recommended over an in-place journal through the core)
- [ ] Speed: draw the first display off the UI thread (10–13 s at 1 000 000 entities)
- [ ] Speed: frame at extents at 1 000 000 entities (0.8–1.1 s, inside Skia); level of detail would change pixels (needs a decision)
- [ ] Speed: highlight of 100 000 selected objects (1.2–1.6 s)
- [ ] Blocks: BATTMAN, the Block Attribute Manager (design first)
- [ ] Blocks: INSERT with a base point picked lands wrong for a block of other units (the shift is not scaled by the unit
      factor)
- [ ] Blocks: -ATTEDIT by a window takes only the attributes inside the window
- [ ] Dimensions: radius, diameter and angular pictures follow DIMTAD, DIMJUST and the fit rules (linear and ordinate do)
- [ ] Dimensions: the dimension line starts at the back of a closed arrowhead, as AutoCAD draws it; words sized by the
      font, not estimated
- [ ] Dimensions: DIMTVP, DIMTMOVE 1's leader for words moved out by fit, DIMGAP below zero (box round the words);
      DIMTEDIT on a DIMTAD 0 dimension puts the line through the words
- [ ] Dimensions: DIM offers Jogged and Arc length, edits Mtext in the editor
- [ ] MTEXT: word wrap at the width, stacked fractions, columns; line spacing 5/3 as AutoCAD (check first)
- [ ] Clipboard: preview of the objects while PASTECLIP asks for the point
- [ ] PLOT: print to Windows printers; add named page setups; embed only the glyphs used (smaller PDFs)
- [ ] Xrefs: XOPEN, a balloon when a file changes, editable xref layers (`NAME|LAYER`), nested xrefs in the palette
- [ ] Images: IMAGEFRAME in a drawing with no image yet (needs RASTERVARIABLES made in core)
- [ ] PURGE dialog, Group Manager, Draw Order dialogs (design first)
- [ ] Selection: QSELECT's "select objects" button

### Decisions for the maintainer

- [ ] Esc while editing a TEXT in place: cancel (now) or keep what was typed (AutoCAD?)
- [ ] DIMBREAK breaks are drawn only, not stored as AutoCAD stores them: a DXF of AutoCAD 2021 with a broken dimension
      would let them be stored
- [ ] The oblique angle of a dimension: from the x axis (presenter) or from the dimension line (core's doc)
- [ ] Picking: the object on top wins among those within a tenth of the pickbox (now), or always within the whole pickbox

## Part B: model drawings

On hold: Part A is finished first, and the maintainer may change Part B before it goes on.

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
- [ ] Packages `GeometryHelper.Drawing` and `GeometryHelper.Dxf.Viewer` on NuGet (only when the work is complete)
