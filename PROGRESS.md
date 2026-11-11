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

- [x] MIRRTEXT for tables and dimensions (MIRROR keeps a dimension in the plane, as AutoCAD)
- [x] MIRRTEXT: MLEADER under MIRROR, MIRRTEXT 1 TEXT/ATTDEF/ATTRIB (71 = 2, +Z), TEXT placed by its font ink under MIRRTEXT 0
- [x] MIRROR: INSERT (41 = -1, turn of the mirrored y axis), ATTRIBs without 210, rotations within 0..360, TEXT spaces kept, 45-degree multileaders
- [x] MIRROR follow-ups 2: MTEXT with no 11 read along world x, MINSERT, annotative INSERT, dynamic flip kept (and on COPY/ARRAY), MLEADER sizes exact, missing $AUPREC and $MIRRTEXT read as 0
- [ ] MIRROR follow-ups 3: GeoTransform3.Mirror across 45 degrees gives 9.999999999999957 for 10 (plane from a normalized normal); annotative copy writes {ACAD_REACTORS where AutoCAD writes none (S)
- [x] Drafting aids: TK (TRACKING); check TT with OTRACK off, "(need YZ):" input and Enter at a modifier prompt
      against AutoCAD (S)
- [x] Draw: AUDIT a drawing made with every ARC, CIRCLE, PLINE, TEXT and MTEXT option, and compare ARC and PLINE entity
      by entity with AutoCAD's own; check Ctrl, ANGDIR 1 and the MTEXT column defaults against AutoCAD (S)
- [x] Properties: dimension rows Ext line fixed and length (core DIMFXLENON), Fill color, Fractional type, Text view
      direction, Text position X/Y; hatch origin kept through angle and scale; MATCHPROP plot style and gradient hatch (S)
- [x] Dimension pictures: DIMTFILL mask, DIMTXTDIRECTION, ordinate jog under DIMFXLON, Text position under DIMTMOVE 0, DIMLINEAR Mtext/Angle/Rotated and groups 11/42, flaky Plugins tests (folders deleted by a parallel run)
- [ ] Dimension follow-ups: DIMSCALE on DIMBREAK's default gap (accoreconsole draws no Auto breaks: needs AutoCAD with graphics); radius words via the palette under DIMTMOVE 0 (AutoCAD: words at the point and a hook); DIMTFILLCLR ByBlock written 256 (core refuses ByBlock); right-to-left words in the DIMTMOVE 1 fit path; DIMLINEAR Text prompt default <100.0000> (ours <<>>); MATCHPROP gradient unconfirmed; Plot Style box name; Left-to-Right/Not stacked wording (S)
- [x] Variables: POLARMODE, ATTDIA, FILEDIA, CMDDIA, AUTOSNAP's bits; new dimensions follow DIMSCALE; the polar angle in
      Global settings; TK (TRACKING); the named-dictionary guard in the table and multileader style editors
- [x] Variables: FILEDIA for QSAVE of a new drawing, XATTACH, IMAGEATTACH, WBLOCK and INSERT's Browse; POLARMODE bits 1
      and 4 (angles from the last segment, POLARADDANG); leaders following DIMSCALE (S): MLEADER does not follow it in AutoCAD either
- [ ] Follow-ups of aids-vars: refusal message for a modifier picked from the menu where it may not nest (S)
- [x] MLEADERSCALE, CMLEADERSTYLE, LEADER and QLEADER (command line) as AutoCAD 2021
- [x] QLEADER Leader Settings (AcadDim xrecord, DIMLDRBLK) and the Polar tab's additional angles, tracking and measurement, with the polar tooltip
- [ ] Leader follow-ups: QLEADER "User Arrow..." and arrow pictures in Leader Settings; angle constraints not previewed by the rubber band; Reuse Current of an INSERT; LEADER Tolerance (no TOLERANCE command); Copy of INSERT/TOLERANCE; drag preview; other DIM* overrides (DIMCLRD, DIMLDRBLK...); hook along the UCS; DIMSCALE 0 through a viewport for dimensions (1/viewport scale); dimensions writing every differing variable as an override (S)
- [x] PEDIT: PEDITACCEPT and PLINETYPE through SETVAR; 3D polylines (options and vertex editing to 1E-8); Extend for arc
      ends in a fuzz join (20 cases to 1E-10) (S)
- [x] REVCLOUD: the 2021 approximate arc length and REVCLOUDARCVARIANCE (from the help); Freehand preview was already in
- [ ] PEDIT/REVCLOUD follow-ups: a spline turned into a polyline with a precision (parked after 3 models, counts in
      D:/claude/scratch/pedit-rev/b1-b5.out.txt); BOUNDARY and JOIN under PLINETYPE 0; Multiple with 3D polylines;
      REVCLOUD's first size from the view; REVCLOUDPROPERTIES and Modify (2021 REVCLOUD cannot be run headless) (S)
- [x] Layouts: twisted viewports drawn and entered as AutoCAD (two bugs: the turn's sense, group 12 in view coordinates);
      one undo step per wheel or pan gesture; ZOOM Previous per viewport (20 each); -SCALELISTEDIT; Isolate in the Layer
      manager; New VP Freeze on viewports made through the core's API (S)
- [x] SCALELISTEDIT dialog, in-place layer rename, Edit Layer State with Description, Input Search Options (designed, checked against AutoCAD 2021)
- [ ] Dialog follow-ups: AutoCAD lists system variables separately only after Tab (ours below a rule); Remember corrections range 2-10 and the Reset/duplicate-scale texts from the design (unverified); Visual Style content finds nothing (no visual styles); a drawing without $MEASUREMENT resets to the metric list (S)
- [ ] Layouts follow-ups: wheel and middle-button
      views in the ZOOM Previous list; typed ZOOM/PAN as undo steps in model space and paper, as AutoCAD; the screenshot
      mode drawing the white paper (S)
- [x] Styles: MLEADER with no style makes Standard; Block and None content of multileader styles; the scale options
      greyed when Annotative is ticked (S)
- [x] Associative dimensions round 3: flag 32 written so a plain MOVE takes the picture; copies get their own picture and
      stay tied to copied objects; intersection snaps; ties from DIM, QDIM, DIMCONTINUE, DIMBASELINE, ordinate, angular
- [x] Associative dimensions: PERpendicular and TANgent snaps tied with AutoCAD's "last point" reference
- [x] Associative dimensions: PER and TAN at a first origin, on polylines, for angular and ordinate; AUDIT 0 in AutoCAD
      and ours; 16 ties followed through 7 states as AutoCAD to 1E-3; STRETCH of rotated dimensions and polyline arcs (S)
- [ ] Associative dimensions follow-ups: PER to a foot past a line's end (extended perpendicular); DIMANGULAR on a circle;
      groups 11 and 42 written; the three-point angular arc point as AutoCAD picks it (S)
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
  - [ ] W2 follow-ups (palette and colour-book/multi-line attribute drawings done): spline normal sign off XY (8 of 10 match; parked) (S)
  - [x] W3 R2007 (AC1021): its own layout, Reed-Solomon, Unicode strings (M-L); the ACI palette made AutoCAD 2021's
        (197 of 255 indexes were wrong)
  - [x] W4 Every entity and object: VIEWPORT, HATCH, LEADER, MULTILEADER, TOLERANCE, IMAGE, WIPEOUT, MLINE, tables to
        R2007, solids in R2000/R2013/R2018, book colours, multiline attributes; 0 differences in all 6 versions (L)
  - [ ] W4b: tables from R2010 (TABLECONTENT, CELLSTYLEMAP), ACDSDATA, SAB to SAT for R2004-R2010, SHAPE names, MATERIAL,
        VISUALSTYLE, RASTERVARIABLES/WIPEOUTVARIABLES, OLE2FRAME, proxies, dynamic block data (L)
  - [x] W5 Writing R2018 (AutoCAD 2021's own): `DwgWriter`, our own LZ77; 19 drawings open in AutoCAD with AUDIT 0 and
        round-trip; files 70-75 % of AutoCAD's size (L)
  - [x] W5b: write MULTILEADER, MLEADERSTYLE, ACAD_TABLE (R2010 form, TABLESTYLE, TABLECONTENT), multiline attributes, RASTERVARIABLES/WIPEOUTVARIABLES; reader reads R2010+ tables
  - [x] W5b follow-ups: cell and table overrides both ways, TABLECONTENT (R2007+) and TABLEGEOMETRY read, border edges fixed, clean table sample AUDIT 0, left-out test
  - [ ] W5b follow-ups 2: custom cell styles written (ids 101+, AutoCAD AUDIT 0) and merged range edges done; reader parity on tcustom (9 of 15 cells differ, parked after 3 attempts); R2000/R2004 TABLECONTENT; TABLEGEOMETRY head bit; records without a handle dropped silently; DwgReader summary and NotReadYet stale; ACIS (AcDs), AC1018/1024/1027 and R2000/R2007 containers (S-L)
  - [x] W6 GeometryHelper DXF: Open and Save As DWG, drag and drop, DWG xrefs (M)
  - [ ] W6 follow-ups (AUDIT of edited DWGs 0 errors, Drawing1.dwg and autosave names done): Save As listing every DXF version (needs a DXF version converter); BYLAYER's ColorValue white in AutoCAD (ours 0) (S)
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
- [ ] Managers follow-ups 3: ANGBASE/ANGDIR still raw
      in dimension and ellipse Properties rows, DIMEDIT oblique, -STYLE obliquing, MEASUREGEOM angle, the `<45` override;
      ATTSYNC by Select on a block with no definitions and its extended data (AutoCAD unchecked: no ATTSYNC in
      accoreconsole); GhDialogButton dimmed twice when disabled (S)
- [x] Command line and Recent: Search Content; EXPORT and -EXPORT (AutoComplete inside a running command dropped: AutoCAD
      2021 has none) (S)
- [ ] Command line follow-ups: collapsible content section, hatch thumbnail on hover, visual
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
- [x] Snapping: SNAP now starts off (SNAPMODE 0) and POLAR on (AUTOSNAP 63), as AutoCAD 2021 (S)
- [x] Status bar per drawing: SNAP, GRID and their spacing from the `*Active` VPORT (new `DxfViewportTable`), ORTHO from
      `$ORTHOMODE`, LWDISPLAY from `$LWDISPLAY`, each tab its own; written back on switch (S)
- [x] Status bar follow-ups: SNAPBASE, SNAPANG and GRIDMAJOR read but not applied to snapping and the grid; isometric
      snap (SNAPSTYL 1) (S). DECIDED by the user 2026-11-11: GRID off and LWDISPLAY on stay the defaults
- [x] Drafting Settings Snap and Grid tab (designed) and PolarSnap
- [x] Snap follow-ups: SNAP and GRID options, SETVAR of the snap and grid variables, GRIDSTYLE dots and GRIDDISPLAY bits, OTRACK and polar rays by SNAPANG/isoplane, PolarSnap along tracking, LWDISPLAY ON/OFF
- [ ] Snap follow-ups 2: SNAP Legacy cursor behaviour; paper-space grid (own GRIDMODE); GRIDDISPLAY 8 stored only; SNAPMODE 2 meaning; GRIDUNIT "5" as a distance from the last point; the Drafting Settings dialog refuses a negative POLARDIST that SETVAR takes; "Grid too dense to display" message (S)
- [ ] POLARDIST answered by two points at SETVAR (AutoCAD: "Specify second point:", stores the distance); needs the command line to take a point at a distance prompt with no base point (deferred by the maintainer 2026-11-11) (S)
- [x] Tests: DisplayPatchSpeedTests flaky under load: each phase best of 3 after a full GC inside a no-GC region;
      0 of 20 failed under load (was 5 of 20), limits unchanged (S)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [x] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [ ] Table editing follow-ups: Columns/Rows Resize (start the width drag); ORTHO/OSNAP/polar on table drags; a DYN field for row heights; up-flowing tables in CellNear and the drags; TABLEINDICATOR kept in registry or drawing (unverified); a typed 1 in the editor stored as a number or text; a look in the running app (S)
- [x] Tables: rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles
- [ ] Tables follow-ups: the row growing live while a cell is typed (the canvas must redraw the table during the edit);
      a block merged across rows (shortfall to the last row, unprobed); the table's own per-kind looks (S)

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: Home panels a few pixels wider than their design widths
- [x] Speed: undo without reloading the whole drawing at 1 000 000 entities: read-ahead of the next undo and redo drawings (0.4 s where 2.2 s, once read ahead), states packed in 4 MB pieces; no core fork
- [ ] Speed follow-ups: undo read-ahead costs about 0.5 GB more peak at 1M entities; an edit made while a read-ahead runs
      shares the CPU (100k ERASE right after an undo 98 -> 269 ms); the first undo after BSAVE reads inline; per-entity
      allocation in the core parser (`ParsedOf` pair lists) is the next cost of layer on/off (S)
- [x] Speed: open a drawing in the background; the window held at most 0.2 s at 1 000 000 entities (was 15.4 s), Stop and Esc cancel
- [x] Speed: frame at extents at 1 000 000 entities (0.8–1.1 s, inside Skia); level of detail would change pixels. DECIDED (maintainer 2026-11-11): kept as is, exact pixels first
- [x] Speed: highlight of a large selection drawn on several threads (100k at extents 295 -> 150 ms, same pixels)
- [x] Dimensions: radius, diameter and angular pictures follow DIMTAD, DIMJUST and the fit rules (linear and ordinate do)
- [x] Dimensions: the dimension line starts at the back of a closed arrowhead, as AutoCAD draws it
- [x] Dimensions: DIMTVP, DIMTMOVE 1's leader for words moved out by fit, DIMGAP below zero (box round the words);
      DIMTEDIT on a DIMTAD 0 dimension puts the line through the words
- [x] Dimensions round 6: words sized by the font; DIMJOGGED (145 of 152 pictures); DIMTEDIT Left/Right and DIMTOH on
      slanted lines (336 of 336); small radii under DIMTMOVE 1/2 (212 of 212); LARGE_RADIAL_DIMENSION groups 13/14/15
      read as AutoCAD writes them (M)
- [ ] Dimensions follow-ups 2: DIM offering Jogged and Arc length (2016+ DIM prompts not recordable headless); DIM edits
      Mtext in the editor; parked: DIMJOGGED with the jog past the rim or far off its middle (4), DIMJOGANG 90 (2), words
      pushed past the arrowhead (1); slanted Arial in angular dimensions (13 of 512, 0.1-0.4 off); r5's 14 diameter and
      24 angle cases; angles whose arc runs through their points (float side choice) (M)
- [x] Spline intersections against tiny or huge circles, arcs, ellipses no longer hang (21 cases, now 0.04-4.5 ms;
      ordinary spline intersections 1.89 -> 1.51 s, bit-identical)
- [x] Try audit follow-ups: GetIntersections on flat/empty boxes, NaN/infinite input false in the segment Try members, fillet radius message
- [ ] Try audit follow-ups 2: flat boxes in CollidesWith/DistanceTo done (rectangle, segment, point); boxes thinner than the tolerance on two axes give wrong answers through the body path (122 of 1 805 twin calls, e.g. 100 x 2E-6 x 2E-6 gives +Infinity for 20; old fault, a fix changes non-flat results: maintainer's decision); GetFaces/ToSolid of a flat box throw as documented (a TryGetFaces would need the maintainer); non-Try GetIntersections with NaN shapes; Corner3.RequirePositive lets +infinity through; the NaN fuzz over other types' Try members; 1E300 overflow in TryExtendTo/TryTrimTo(GeoCircle2) parked (metre-irrelevant) (S-M)
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
- [x] Xrefs: XOPEN; editable xref layers with AutoCAD's override bits (AcXRefLayerPropertyOverride); nested xrefs in the
      palette (list and tree); a changed referenced file said on the command line
- [x] Xrefs round 3: XATTACH writes the `NAME|LAYER` and `NAME|LINETYPE` records as AutoCAD does; -LAYER Transparency,
      Plot and Description; VISRETAIN 1 and 0 as AutoCAD; "Needs Reloading" as the 2021 help (S)
- [ ] Xrefs follow-ups: -XREF Bind done (Bind and Insert types); palette Bind... and its dialog done; Unload/Reload/Detach on every selected row; Bind... for Unreferenced rows?; names with wildcard characters escaped; screenshot mode for the two windows; AUDIT of a saved bound drawing and its DWG save; AutoCAD's built-in callout blocks for MLEADER block content; `N$0$NL` keeps AcXRefLayerPropertyOverride in AutoCAD; the change balloon in the status bar (needs
      design) (S)
- [x] Images: IMAGEFRAME in a drawing with no image yet (needs RASTERVARIABLES made in core)
- [x] Managers: PURGE previews of table and multileader styles (the managers had them; Groups kind was done); BATTMAN asks before dropping changes, constants do not move
- [ ] Managers follow-ups 4: ATTDIA Cancel and Object Grouping Cancel unverifiable headless (ours: defaults placed, changes taken back); BATTMAN counts model space only where AutoCAD counts the current space; Quick Select's Append should look at the whole drawing (S)
- [x] Selection: QSELECT's "select objects" button

### Decisions for the maintainer

- [x] Esc in the in-place editors, as AutoCAD 2021: TEXT keeps its old words; MTEXT with changes asks "Multiline Text -
      Unsaved Changes" (Yes / No / Cancel, "Always perform my current choice" kept in Global settings > Hidden messages);
      designed in v115 (S)
- [x] Design: add the Hidden messages row to the SettingsText artboard (S)
- [x] Booleans with a flat box answer false for union, intersect and subtract (the user's choice 2026-11-11)
- [x] DIMBREAK breaks stored as AutoCAD stores them (BREAKDATA in the dimension's extension dictionary), read and drawn
      again on redraw; AUDIT 0 in AutoCAD
- [x] The oblique angle of a dimension is measured from the dimension line, as AutoCAD (presenter and core's doc fixed)
- [x] Picking: the object on top wins anywhere in the pickbox (SORTENTS bit 1), else the one drawn first, as AutoCAD
- [x] DIMBREAK's automatic gap of a new imperial drawing: AutoCAD 0.125 (its default), the presenter 1.5 x DIMASZ (S). DECIDED (maintainer 2026-11-11): follow AutoCAD (task dim-props)

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
