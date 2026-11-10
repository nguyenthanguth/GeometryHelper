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
- [x] Core: PEDIT Fit as AutoCAD 2021 (matches 60 polylines to 1E-11), 3D spline vertex order, DICTIONARYVAR listed so      Set Current works in new drawings (multileader and table styles), annotative multileader styles
- [x] Block Editor: BEDIT, -BEDIT, BSAVE, BSAVEAS, BCLOSE to the design (v107), its own space, background and tab; the      screenshot mode never shows a real dialog- [x] Leftovers: -GROUP on an R2000 drawing without OBJECTS, INSERT base point in other units, -ATTEDIT by window, PICKADD,      ATTREQ, DYNMODE values, ORTHOMODE in the drawing, Recent Commands kept, missing recent files dropped, previews off      the UI thread
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
- [ ] Drafting aids: TK (TRACKING); check TT with OTRACK off, "(need YZ):" input and Enter at a modifier prompt
      against AutoCAD (S)
- [ ] Draw: AUDIT a drawing made with every ARC, CIRCLE, PLINE, TEXT and MTEXT option, and compare ARC and PLINE entity
      by entity with AutoCAD's own; check Ctrl, ANGDIR 1 and the MTEXT column defaults against AutoCAD (S)
- [ ] Properties: dimension rows Ext line fixed and length (core DIMFXLENON), Fill color, Fractional type, Text view
      direction, Text position X/Y; hatch origin kept through angle and scale; MATCHPROP plot style and gradient hatch (S)
- [x] Variables: POLARMODE, ATTDIA, FILEDIA, CMDDIA, AUTOSNAP's bits; new dimensions follow DIMSCALE; the polar angle in
      Global settings; TK (TRACKING); the named-dictionary guard in the table and multileader style editors
- [ ] Variables: FILEDIA for QSAVE of a new drawing, XATTACH, IMAGEATTACH, WBLOCK and INSERT's Browse; POLARMODE bits 1
      and 4 (angles from the last segment, POLARADDANG); leaders following DIMSCALE (S)
- [ ] PEDIT: PEACCEPT and PLINETYPE read through SETVAR; a spline turned into a polyline with a precision; 3D polylines;
      Extend for arc ends in a fuzz join (S)
- [ ] REVCLOUD: the 2021 approximate arc length (REVCLOUDARCVARIANCE), REVCLOUDPROPERTIES, a Freehand preview, Modify on
      more than closed polylines (S)
- [ ] Layouts: twisted viewports entered; one undo step per wheel or pan gesture; a ZOOM Previous list per viewport;
      SCALELISTEDIT (Custom… runs VPSCALE now); Rename and Isolate in the Layer manager; New VP Freeze on viewports made
      through the core's API, not only MVIEW (S)
- [ ] Styles: MLEADER with no style makes Standard; Block and None content of multileader styles; the scale options
      greyed when Annotative is ticked (S)
- [x] Associative dimensions round 3: flag 32 written so a plain MOVE takes the picture; copies get their own picture and
      stay tied to copied objects; intersection snaps; ties from DIM, QDIM, DIMCONTINUE, DIMBASELINE, ordinate, angular
- [x] Associative dimensions: PERpendicular and TANgent snaps tied with AutoCAD's "last point" reference
- [ ] Associative dimensions: PER and TAN at a first origin, on polylines, for angular and ordinate; AUDIT of a drawing
      with PER/TAN ties (S)
- [x] Dimension pictures: radius, diameter and angular laid out by DIMTAD, DIMJUST and the fit rules; every line from the
      back of its arrowhead; DIMTVP; the box of DIMGAP below zero (526 AutoCAD pictures compared, 511 match)
- [x] Dimensions: words measured by the drawing's fonts in the pictures (the main gap left); DIMTEDIT on angular and
      radius with DIMTMOVE 0 as AutoCAD; DIMARC; radial dimensions made under DIMTMOVE 1/2 (S)
- [x] Previews as AutoCAD draws them: RECTANG the rectangle, CIRCLE, ARC, ELLIPSE, POLYGON, DONUT, XLINE/RAY, PLINE with
      widths and arcs, REVCLOUD, ROTATE/SCALE/MIRROR (Reference too), ARRAY, OFFSET, STRETCH, every dimension, the MTEXT
      frame; each preview made by the same code as the result
- [x] BATTMAN with Edit Attribute and Settings, the Edit Attributes dialog of ATTDIA, the PURGE dialog (nested purge in
      -PURGE too), Object Grouping (CLASSICGROUP), to the v110 boards
- [x] Dynamic blocks in use: custom grips at AutoCAD's points (visibility, lookup, linear, flip, rotation, XY), the
      Custom group in Properties, RESETBLOCK, the Block Editor's visibility box; AutoCAD reads every value as written
- [ ] Dynamic blocks: the core's AddVisibilityParameter lists no parameter in any state (AutoCAD then hides them); XY
      corner and block-table grips; a live preview while a custom grip is dragged; GRIPDYNCOLOR (S)

### To do

- [ ] DWG, our own reader and writer (the maintainer's choice: code of our own, to change at will). Sources: the Open
      Design Specification for .dwg (the main one), ACadSharp (MIT: may be ported, with its notice), LibreDWG (GPLv3: its
      documentation only, never its code). The reader turns a DWG into the library's DXF records, so everything built
      works on DWG; the writer goes back. Checked against DWGs and DXFs AutoCAD 2021 writes through a probe plugin, AUDIT
      of what we write, ACadSharp as a side check in scratch only.
  - [x] W1 Bits and R2000 (AC1015): bit codes, CRC, header, section locator, header variables, classes, tables, blocks,
        the common entities and objects, the handle map (L)
  - [ ] W1 follow-ups (into W4): VIEWPORT, HATCH, LEADER, MLINE, IMAGE, WIPEOUT, OLE, proxies, styles objects; polygon
        mesh, polyface and MINSERT checked on AutoCAD drawings; R2000 round-trip colours; the preview image
  - [x] W2 R2004, R2010, R2013, R2018 (AC1018-AC1032): pages, LZ77 compression, the encrypted header, the object stream
        of 2010 on (L)
  - [ ] W2 follow-ups: the shared ACI palette (DxfColor) has 250 black where AutoCAD's 250-255 greys are 51, 80, 105,
        130, 190, 255 (worked round in the DWG reader only); test drawings with a colour-book colour (group 430) and an
        R2018 multi-line attribute; spline normal sign off XY (8 of 10 match; parked) (S)
  - [x] W3 R2007 (AC1021): its own layout, Reed-Solomon, Unicode strings (M-L); the ACI palette made AutoCAD 2021's
        (197 of 255 indexes were wrong)
  - [ ] W4 Every entity and object: hatch, dimension, MTEXT, multileader, table, dynamic block data, xref, image,
        layouts; proxies kept as they are (L)
  - [ ] W5 Writing R2018 (AutoCAD 2021's own), then R2000-R2013 if wanted (L)
  - [ ] W6 GeometryHelper DXF: Open and Save As DWG, drag and drop, DWG xrefs (M)
- [x] Dynamic input at the cursor as AutoCAD's with DYN on: what is typed at the Command prompt shows in a box at the
      cursor with the AutoComplete list under it, the prompt and its options (the ▼ menu) at the cursor in a command,
      the command line keeping its own copy; with DYN off only the command line (M) (designed in v111)
- [x] Previews round 2: FILLET and CHAMFER hovering the second object, TRIM and EXTEND Quick mode, LENGTHEN Dynamic,
      HATCH pick points, TEXT, TABLE, MLEADER, QDIM, BREAK, ALIGN, the Points of ROTATE/SCALE Reference, -ARRAY (M)
- [x] Tests: the Wpf suite's 25 red tests came from its classes loading XAML in parallel (a race inside WPF), so the
      suite runs one test at a time; its picture goes to its own folder, and a view model made in a test keeps its
      settings in a file of the tests (S)
- [x] Tests: other fixed %TEMP% paths two worktrees can collide on (render and plot outputs of Dxf.Viewer.UnitTest,
      Drawing.UnitTest and Drawing.Wpf.UnitTest) move to a folder per run (S)
- [x] Layer States Manager, View Manager with New View, Drawing Units with Direction Control (designed in v110) (M)
- [x] Previews follow-ups: TRIM in Standard mode and Shift swapping TRIM and EXTEND; -ARRAY distances without a base point
      and polar -ARRAY; the TEXT box by justification; BREAK and the -ARRAY unit cell checked against what AutoCAD shows
- [ ] Previews follow-ups 2: a look by eye in the running app (rounds 2 and 3); -ARRAY row distance by two points; TEXT
      Align box; -ARRAY unit cell as a rectangle only, if AutoCAD shows only that (check); paste preview speed on a large
      clipboard; `ALL` selecting nothing on a minimal hand-written DXF (cause unknown) (S)
- [x] Managers follow-ups 2, non-UI parts: viewport overrides on restore; the xref rule of GetLayerStateNames; ANGBASE and
      ANGDIR on typed and shown angles; disabled GhComboBox and GhFlatButton greyed; Groups in PURGE and -PURGE (M)
- [ ] Managers follow-ups 3: an Edit Layer State dialog and a description in New (needs design); ANGBASE/ANGDIR still raw
      in dimension and ellipse Properties rows, DIMEDIT oblique, -STYLE obliquing, MEASUREGEOM angle, the `<45` override;
      ATTSYNC by Select on a block with no definitions and its extended data (AutoCAD unchecked: no ATTSYNC in
      accoreconsole); GhDialogButton dimmed twice when disabled (S)
- [x] Command line and Recent: Search Content; EXPORT and -EXPORT (AutoComplete inside a running command dropped: AutoCAD
      2021 has none) (S)
- [ ] Command line follow-ups: Input Search Options dialog, collapsible content section, hatch thumbnail on hover, visual
      styles (need design); EXPORT writers for Metafile and the other refused types if wanted; -EXPORT Dwf/dwfX and All
      layouts (S)
- [ ] Dxf.App: an app icon, file association for .dxf (installer or registry), `--tab Home` in screenshot mode
- [x] Images of an area: PNG and JPG devices in PLOT and -PLOT ("GeometryHelper PNG", "GeometryHelper JPG", as AutoCAD's
      PublishToWeb PNG/JPG), plot area Window, Extents, Display or Layout, size in pixels (1920 x 1080, 3840 x 2160 or
      typed) in place of a paper (designed in v111)
- [x] Dynamic input: dimensional input (length and angle boxes) beyond LINE and PLINE (M)
- [x] Dynamic input (user 2026-11-10): while drawing LINE and the other commands with length and
      angle fields, draw no dimension line, extension lines or angle arc; keep the length and angle values in their boxes (S)
- [x] Dynamic input: with OSNAP the fields follow the raw cursor, not the snapped point the band is drawn to (S)
- [x] Dynamic input follow-ups: MIRROR and other commands still on X and Y; CIRCLE 3P; angle field from a reference line;
      grip rotate and scale tested on their own (S)
- [x] Snapping: a handle-less R12 DXF gave no object snaps (now given handles on opening, as AutoCAD does); the snap
      order was already AutoCAD's and is pinned by a test (S)
- [ ] Snapping: the presenter starts with SNAP on, AutoCAD's SNAPMODE default is 0 (S)
- [ ] Tests: DisplayPatchSpeedTests still fails about 1 run in 6 under other builds' load (layer on 0.73 of whole); take
      each phase as the best of a few runs instead of one, limits unchanged (S)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [ ] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [ ] Tables (running): rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: Home panels a few pixels wider than their design widths
- [x] Speed: undo without reloading the whole drawing at 1 000 000 entities: read-ahead of the next undo and redo drawings (0.4 s where 2.2 s, once read ahead), states packed in 4 MB pieces; no core fork
- [ ] Speed follow-ups: undo read-ahead costs about 0.5 GB more peak at 1M entities; an edit made while a read-ahead runs
      shares the CPU (100k ERASE right after an undo 98 -> 269 ms); the first undo after BSAVE reads inline; per-entity
      allocation in the core parser (`ParsedOf` pair lists) is the next cost of layer on/off (S)
- [ ] Speed: draw the first display off the UI thread (10–13 s at 1 000 000 entities)
- [ ] Speed: frame at extents at 1 000 000 entities (0.8–1.1 s, inside Skia); level of detail would change pixels (needs a decision)
- [ ] Speed: highlight of 100 000 selected objects (1.2–1.6 s)
- [x] Dimensions: radius, diameter and angular pictures follow DIMTAD, DIMJUST and the fit rules (linear and ordinate do)
- [x] Dimensions: the dimension line starts at the back of a closed arrowhead, as AutoCAD draws it
- [x] Dimensions: DIMTVP, DIMTMOVE 1's leader for words moved out by fit, DIMGAP below zero (box round the words);
      DIMTEDIT on a DIMTAD 0 dimension puts the line through the words
- [ ] Dimensions follow-ups (running): words sized by the font, not estimated; DIMJOGGED (prompts recorded, the jog picture not yet
      derived: one chord at -23.6 deg unexplained); DIM offering Jogged and Arc length (2016+ DIM prompts not recordable
      headless); DIM edits Mtext in the editor; parked: angles whose arc runs through their points (56 pictures, float
      side choice), radius 3 circles under DIMTMOVE 1/2 with the point inside (28 pictures); DIMTEDIT Left/Right and
      DIMTOH on slanted lines unprobed (M)
- [x] MTEXT: word wrap at the width, stacked fractions, columns; line spacing 5/3 as AutoCAD (check first)
- [x] MTEXT in-place editor lays out with the drawn MTEXT's layout (wrap, spacing, stacks, columns, mask, frame)
- [x] Speed: DisplayPatchSpeedTests "layer on" failing: no merge to blame; the three 100k speed tests ran in parallel and
      their collections stopped each other's timers (now one serial collection); layer off 85 -> 23 ms by testing the
      layer before the entity's own visibility (S)
- [ ] MTEXT follow-ups: the editor's ruler (indents, tab stops), column drag, `\W1.2x;` in a style of width not 1, the
      caret at a soft break between ideographs; paragraph, column and tab dialogs and ribbon controls (need design); triangle width/height grips;
      parked: `\pxsm2` with tall runs (0.048 h), flow direction 3, `\pxsa` first line, `\M+` codes, mask with a taller
      defined height, manual columns at bottom or middle attachment, column grips; EXPLODE keeping run fonts and colours;
      TEXT draws %%U/%%O/%%K lines; warm display cost of 10 000 MTEXTs 290 -> 430 ms (a per-entity cache?) (M)
- [x] Clipboard: preview of the objects while PASTECLIP asks for the point
- [ ] PLOT: print to Windows printers; add named page setups; embed only the glyphs used (smaller PDFs)
- [ ] Xrefs: XOPEN, a balloon when a file changes, editable xref layers (`NAME|LAYER`), nested xrefs in the palette
- [ ] Images: IMAGEFRAME in a drawing with no image yet (needs RASTERVARIABLES made in core)
- [ ] Managers: previews of table and multileader styles and a Groups kind in PURGE; check BATTMAN's block change, ATTDIA Cancel and Object Grouping Cancel against AutoCAD (S)
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
