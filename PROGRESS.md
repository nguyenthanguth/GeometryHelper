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
- [ ] Variables (running): POLARMODE, ATTDIA (needs an Edit Attributes dialog), FILEDIA, CMDDIA; AUTOSNAP's other bits; new
      dimensions follow DIMSCALE (the header's $DIMSCALE written as an override); polar angle field in Global settings;
      TK (TRACKING); the named-dictionary guard in the table and multileader style editors (S)
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
- [ ] Associative dimensions (running): PERpendicular and TANgent snaps tied (AutoCAD writes a second "last point" reference after
      one of 75 = 1, which the core's DIMASSOC neither reads apart nor writes) (S)
- [ ] Dimension pictures (running): radius, diameter and angular follow DIMTAD, DIMJUST and the fit rules; the line from the back of
      an open arrowhead (_CLOSED, _CLOSEDBLANK); DIMTVP; DIMGAP below zero (box round the words) (M)
- [ ] Blocks (running): dynamic blocks in use: custom grips (visibility, lookup, stretch, flip, rotate, array) and their Properties
      rows (L)

### AutoLISP

Measured on the maintainer's 22 routines (`D:\Autodesk\AutoCAD\AutoCAD 2021\Support\AutoLisp`, read only, never copied
into the repository): 282 different functions; the most used are setq, defun, command (112 calls: LAYER, -DIMSTYLE, UCS,
UNDO, PEDIT, REVCLOUD, RECTANG, COPY, ZOOM, -PURGE, AUDIT…), if, assoc, cdr, cons, car, ssget (44), entget (44), setvar,
mapcar, lambda, vl-position, getpoint, polar, entmod; 25 files' worth of vla-/vlax- calls (get/put-property,
invoke-method, ename->vla-object, ActiveDocument, Blocks, Layers, TextString, TrueColor, StartUndoMark/EndUndoMark,
GetAttributes, EffectiveName, SelectionSets); 2 files with DCL dialogs (E1, FDVAL); 1 file driving Excel by COM
(TE_TableExport). Each stage is done when the routines that need only that stage run in the presenter as in AutoCAD.

- [ ] L1 The interpreter: AutoLISP's reader (comments, strings, quote, dotted pairs), evaluation, defun with local
      variables after /, lambda, setq, if/cond/while/repeat/foreach/progn/and/or/not, the list, number and string
      functions (car..cadddr, cons, list, append, assoc, subst, nth, member, reverse, length, mapcar, apply, +..sqrt, fix,
      float, rtos, atof, atoi, itoa, distof, angtos, strcat, substr, strlen, strcase, wcmatch, chr, ascii, read, type,
      eq/equal/=), princ/prin1/print/prompt/alert, *error*, the LOAD and APPLOAD commands, (L)
- [ ] L2 The drawing: entget/entmod/entmake/entdel/entupd/entnext/entlast/entsel/nentsel, handent, the selection sets
      (ssget with W, C, X, P, L, :S, _:L, filters with -4 groups; ssadd, ssdel, sslength, ssname, ssmemb, sssetfirst,
      ssgetfirst), tblsearch/tblnext/tblobjname, namedobjdict/dictsearch/dictnext, getvar/setvar for the SETVAR table,
      getpoint/getcorner/getdist/getangle/getreal/getint/getstring/getkword with initget, polar/angle/distance/inters/
      trans/osnap, command and command-s and vl-cmdf through the session (pause, "" for Enter), defun c:NAME as a command,
      acaddoc.lsp loaded with each drawing, undo grouping of a routine's run (L)
- [ ] L3 Visual LISP functions: vl-load-com, vl-position, vl-sort, vl-remove(-if/-if-not), vl-string-search,
      vl-string->list, vl-list->string, vl-princ-to-string, vl-consp, vl-catch-all-apply/-error-p/-error-message,
      vl-filename-mktemp, vl-file-delete, file I/O (open, read-line, write-line, close, findfile) (M)
- [ ] L4 ActiveX in LISP: vlax-ename->vla-object and back, vlax-get/put-property, vlax-invoke-method, vlax-for,
      vlax-make-variant, vlax-3d-point, vlax-safearray->list, vlax-get-acad-object, vla-get-ActiveDocument, and the
      vla- members the routines use (Blocks, Layers, Item, TextString, TrueColor, ColorIndex, Layer, Name, ObjectName,
      EffectiveName, GetAttributes, TagString, GetBoundingBox, Delete, StartUndoMark/EndUndoMark, SelectionSets), mapped
      onto the presenter's objects (L)
- [ ] L5 DCL: load_dialog/new_dialog/start_dialog/done_dialog/unload_dialog, set_tile/get_tile/action_tile/mode_tile,
      start_list/add_list/end_list, the DCL file read and drawn as a dark WPF dialog (design first) (M)
- [ ] L6 Trust and safety: a trusted folders list (as AutoCAD's TRUSTEDPATHS and SECURELOAD), a warning before loading
      a routine from elsewhere, no COM to other programs (vlax-get-or-create-object "Excel.Application" refused with a
      message) (S)
### To do- [ ] Layer States Manager, View Manager with New View, Drawing Units with Direction Control (designed in v110) (M)
- [ ] Command line and Recent: AutoComplete inside a running command; Search Content; an EXPORT command (S)
- [ ] Dxf.App: an app icon, file association for .dxf (installer or registry), `--tab Home` in screenshot mode
- [ ] Images of an area: PNG and JPG devices in PLOT and -PLOT ("GeometryHelper PNG", "GeometryHelper JPG", as AutoCAD's
      PublishToWeb PNG/JPG), plot area Window, Extents, Display or Layout, size in pixels (1920 x 1080, 3840 x 2160 or
      typed) in place of a paper (design first)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [ ] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [ ] Tables: rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: disabled buttons barely look greyed (GhFlatButton); Home panels a few pixels wider than their design widths
- [ ] Speed: undo without reloading the whole drawing (2.5 s at 1 000 000 entities): first the cheap wins (pack the undo state in parallel chunks, build the handle index lazily), then a core `DxfDatabase` fork that shares the read bytes (recommended over an in-place journal through the core)
- [ ] Speed: draw the first display off the UI thread (10–13 s at 1 000 000 entities)
- [ ] Speed: frame at extents at 1 000 000 entities (0.8–1.1 s, inside Skia); level of detail would change pixels (needs a decision)
- [ ] Speed: highlight of 100 000 selected objects (1.2–1.6 s)
- [ ] Blocks (running): BATTMAN, the Block Attribute Manager (designed in v110)
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
- [ ] PURGE dialog and Group Manager (running; designed in v110); Draw Order has no dialog in AutoCAD
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
