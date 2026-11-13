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

User request 2026-11-13 (acad.pgp):
- [x] LAYERP (Layer Previous) with LAYERPMODE (a command in AutoCAD 2021, not a variable), Home › Layers "Previous" button (layerp)
- [x] Default aliases as the maintainer's acad.pgp: 1 LAYISO, 2 LAYERP, 22 LAYON, 3 LAYOFF, merged once into old settings files (layerp)
- [ ] LAYERP follow-ups: check with U and a Layer manager session in the AutoCAD GUI (accoreconsole could not run it); decide whether viewport overrides (VP Freeze etc.) and layer descriptions are tracked; put the Previous button into the Design artifact; check its tooltip wording (S)

Plan: acad.pgp commands the presenter lacks, 2D first (2026-11-13; the exact gap list comes from pgp-cmds1 and may move items between batches; each command with AutoCAD's prompts, one undo step, DXF/DWG as AutoCAD writes it, new dialogs designed first; 3D modelling, mesh, surfaces, render, materials, lights, cameras, point clouds, sheet sets, data links, constraints, animation and cloud commands left out):
- [x] Batch 1, drafting basics (pgp-cmds1): the gap list (scratch GAP.md: 317 pgp commands vs our 202), 69 missing stock aliases, DIVIDE, MEASURE, DIMCENTER, COLOR/-COLOR, LWEIGHT/-LWEIGHT, REDRAW, REDRAWALL, REGENALL, ISOLATEOBJECTS/HIDEOBJECTS/UNISOLATEOBJECTS; fixed DIMCEN<0 centre lines (radius > 2 x mark)
- [x] (superseded: done by pgp-cmds1b, dialogs4, dialogs6; CENTERMARK/CENTERLINE in "Batch 1b leftovers") Batch 1b (left from pgp-cmds1): CHANGE (-CH); RENAME dialog (design first) and -RENAME (blocks, linetypes, multileader and table styles lack rename helpers); Lineweight Settings dialog (design first); CENTERMARK and CENTERLINE (associative, probe their DXF form); DIMCENTER line colour and linetype unconfirmed; ACAD_OBJECTISOLATION record and OBJECTISOLATIONMODE 1; then FILTER, INSERTOBJ, IMAGEADJUST, XBIND, ATTEXT, CTABLESTYLE, -LAYOUT, -MTEXT, -PAN, -OSNAP (M)
- [x] Batch 2, annotation (pgp-cmds2): DIMOVERRIDE (DOV), TEXTALIGN (TA), MLEADERALIGN (MLA), MLEADEREDIT (MLE), ATTIPEDIT (ATI), SPELL (SP, built-in word list and a custom dictionary)
- [x] (superseded: done by mline, fu-mline, dialogs4, dialogs5; MLEADERCOLLECT, Parallel and TEXTALIGN in R3.1) Batch 2b (left from pgp-cmds2): MLINE with MLSTYLE and MLEDIT (needs a core DxfMline builder; group layout noted in the merge commit); MLEADERCOLLECT (a composite block); MLEADERALIGN Parallel; TEXTALIGN on an oblique line as AutoCAD places it; confirm the guessed prompts of SPELL, ATTIPEDIT and MLEADERALIGN Spacing/Distribute in the AutoCAD GUI; a Check Spelling dialog and ribbon buttons (design first) (M)
- [x] Batch 3, shapes and hatching (pgp-cmds3): SOLID, -BOUNDARY (Advanced options), -HATCH (Properties, Select objects, Advanced, DRaw order), GRADIENT, HATCHTOBACK, SPLINEDIT (Close/Open, Move vertex, rEverse, convert to Polyline, Undo), CLASSICINSERT (= INSERT); REGION counts loops but makes none (no ACIS writer)
- [x] (superseded: done by acis, hatch2, splinedit2, dialogs4; what is left is in R2.2, R2.3 and R3.3 follow-ups) Batch 3b (left from pgp-cmds3): an ACIS SAT/SAB writer so REGION and -BOUNDARY Region work (L); the rest of -HATCH (draW boundary, remove Boundaries, Origin, COlor, LAyer, Transparency, ANnotative, Boundary set, Gap tolerance) and Single color gradients; SPLINEDIT Fit data, Join, Refine, Close/Open of control-point splines; plain HATCH drawn behind its boundary as -HATCH is (AutoCAD default); -BOUNDARY on ellipses/splines (AutoCAD refuses them); Boundary Creation and classic Insert dialogs (design first) (M)
- [x] Batch 4a (pgp-cmds4a, design v135-v136): VPORTS/-VPORTS (model tiles, *Active VPORT records read and written as AutoCAD), FILTER (Object Selection Filters), QUICKCALC and CAL, QUICKPROPERTIES with QPMODE (Ctrl+Shift+P)
- [x] Batch 4b (pgp-cmds4b): -PAN, -OSNAP, -LAYOUT, -MTEXT, -XBIND/XBIND (typed), SAVE (the drawing takes the new name, as AutoCAD), QUIT/EXIT, COMMANDLINE/COMMANDLINEHIDE, HIDEPALETTES/SHOWPALETTES (Ctrl+Shift+H); fixed a layout tab left in the wrong case after undoing a rename
- [x] Batch 5, output (pgp-cmds5): EXPORTPDF (typed as -EXPORT Pdf), EXPORTDWF/DWFX (refused: no DWF writer), PLOTSTAMP and -PLOTSTAMP in AutoCAD's words, the stamp on PDF plots with its log, PDFSHX (searchable SHX text), IMPORT of DWG/DXF as a block
- [x] Batch 4a/4b follow-ups 1 (fu-views): saved model tiles shown on opening; other tiles redrawn only when what they show
      changes (30 pan/zoom steps over 40 000 lines: 81-92 ms a step down to 7.1-7.6 ms); LAYOUT in AutoCAD's words (= -LAYOUT);
      -LAYOUT Template and SAveas (DWT/DWG/DXF); the current layout and tab numbers after a delete as AutoCAD; fixed: deleting
      the layout holding *Paper_Space wrote a DXF AutoCAD refuses, a copied or imported sheet's own viewport numbered 2;
      XBIND Block binding its layers, linetypes and text styles as NAME$0$SYMBOL
- [x] Batch 4a/4b follow-ups 2 (fu-views2): FILTER with AutoCAD 2021's property list, AND/OR/XOR/NOT groups, value tests
      and filter.nfl; CAL snap functions, rad, the vector functions and transparent 'CAL, values written as AutoCAD's
      console writes them (%.9g, 120,80,0); -VPORTS MOde
- [x] Dialogs 6 (dialogs6, design v141-v142): Viewports dialog's Change view to, Visual Style list and Named Rename; Select
      Color and Linetype for multiline elements and fill; the Hatch dialog's Click to set new origin; Insert Object dialog
      and Drawing Recovery Manager; Rename with wildcards; Check Spelling Undo and Settings; the FILTER rows in the design
- [ ] Batch 4a/4b follow-ups 3: filter.nfl checked against a 2021 file; FILTER's Transparency, MultiLine Style, Shape and
      surface kinds; CAL rad of a polyline segment, getvar, transformations; QuickCalc Units Conversion and Variables; QP CUI
      lists and QPLOCATION; dimension styles of a block bound by XBIND; Insert Layout and template dialogs (design first);
      -OSNAP rarer short names; whether HIDEPALETTES hides the command line; HATCHEDIT's Click to set new origin; visual
      styles drawn; spelling of dimension text and xrefs; the hidden internal visual style names and Check Spelling's
      punctuation default checked in AutoCAD; CadTiledViewports.Rename and -RENAME VPort agreeing (S)
- [x] (done by dialogs5, dialogs7, dialogs8; the rest in "Dialogs 8 follow-ups") Batch 5b (left from pgp-cmds5; the Plot Stamp dialog and "Plot stamp on" done by dialogs5): the stamp on PNG/JPG plots and turned with an upside-down plot; .pss stamp files; multi-line stamp layout and "." clearing checked in the AutoCAD GUI; PDF of all layouts; app menu Export > PDF running EXPORTPDF and an Import entry (design first); WMF import; ribbon buttons (M)
- [x] Batch 1b first part (pgp-cmds1b): CHANGE (-CH, change point and Properties as AutoCAD 2021 asks), -RENAME (-REN) and RENAME (REN, typed until its dialog is designed), with rename of blocks, linetypes (core), viewport configurations (core), multileader and table styles
- [ ] Batch 1b leftovers: CENTERMARK and CENTERLINE (variables probed: CENTEREXE 0.12, CENTERMARKEXE 1, CENTERLTYPE Center2, CENTERCROSSSIZE 0.1x, CENTERCROSSGAP 0.05x; geometry and DXF form need a pick in the AutoCAD GUI); DIMCENTER line colour and linetype; ACAD_OBJECTISOLATION record; -RENAME refusals of a new name, layer 0/Standard/Continuous and *Active checked against AutoCAD (S)

Lead's plan after batches 4 and 5 (2026-11-13; up to 4 agents at once, basics first, each round merged and verified before
the next takes its place; the lead pauses only at session 95 % or week 98 % and resumes by itself after the reset):
- Round 2, finishing the 2D commands drafters use every day:
  - [x] R2.1 MLINE, MLEDIT (all 12 tools) and typed MLSTYLE (mline), with a public core DxfMlineBuilder and
        DxfDatabase.AddMlineStyle; MLINE drawn on the canvas, moved, copied, scaled and mirrored; AutoCAD 2021 opens our DXF
        and DWG with AUDIT 0, and its Explode of 29 multilines gives our 224 lines to 1E-6
  - [x] R2.1 follow-ups 1 (fu-mline): MLINE grips and STRETCH (core DxfMlineBuilder.MoveVertexAt, cuts kept as AutoCAD),
        TRIM and EXTEND of and to an MLINE, AutoCAD's refusals of OFFSET, FILLET, CHAMFER, LENGTHEN, BREAK; MLEDIT's quiet miss,
        closed refusals, cuts across segments and 4-element merged tees and crosses; MLSTYLE Rename, Delete, Load, Save (.mln)
  - [ ] R2.1 follow-ups 2 (the Multiline Styles and Multiline Edit Tools dialogs done by dialogs5): WA across the first vertex of
        a closed MLINE (AutoCAD's result looks inverted, parked); OT and MT with a closed bar, EXTEND Edge=Extend on oblique
        edges, trims near a corner (S)
  - [x] R2.2 HATCH completed (hatch2): -HATCH draW boundary, remove Boundaries, Origin, COlor, LAyer, Transparency, Boundary
        set, Gap tolerance; Single color gradients (tint as AutoCAD, 45 pairs probed); plain HATCH behind its boundary
        (HPDRAWORDER 3); BOUNDARY refusing ellipse or spline areas; fixed an empty polyline kept by Retain boundary
  - [x] Annotative objects 1 (annot): TEXT, MTEXT, ATTDEF, HATCH, MULTILEADER and blocks made annotative as AutoCAD 2021
        writes them (xdata, extension dictionary, contexts *A1, *A2...), second scales added to TEXT, MTEXT, ATTDEF, HATCH and
        INSERT; -OBJECTSCALE, AIOBJECTSCALEADD/REMOVE, ANNOALLVISIBLE; -HATCH ANnotative; text under an annotative style;
        drawn at CANNOSCALE and at a viewport's scale; AutoCAD reads our 12 objects annotative with their scales, AUDIT 0
  - [x] Annotative objects 2 (annot2): DIMENSION (aligned, linear, radius, diameter, angular, ordinate) and MULTILEADER at
        a second scale with AutoCAD's contexts and pictures; the default context deleted and the next promoted; the *A
        counter; annotative dimension and multileader styles making new ones annotative (the DIMSTYLE Fit tab box);
        ANNOAUTOSCALE; INSERT attributes drawn at the scale; HATCHEDIT Annotative; MOVE/COPY carrying every picture;
        AutoCAD AUDIT 0 with every context kept
  - [x] Annotative objects 3 (annot3): arc-length and jogged dimensions annotative (AutoCAD's context classes); moved or
        turned words at a second scale; multileaders of one or two leaders, text or block content, at a second scale (equal
        to AutoCAD's contexts); DIMBREAK on every scale's picture; angular DSTYLE 40; -INSERT of an annotative block at
        CANNOSCALE with attribute contexts; AUDIT 0 on five checked sets (OBJECTSCALE dialog and design boxes by dialogs7)
  - [x] Annotative objects 4 (annot4): MLEADEREDIT keeping every scale's context; the presenter's MLEADER landed as AutoCAD
        (base and words, the block content's matrix); multileaders of any number of leaders at a second scale; an angular
        context's arc point at a second scale (36 probes equal); DIMARC with DIMDEC found to match AutoCAD already
  - [x] Annotative objects 5a (turnwords): turned words fitted by the reach of their box and placed outside as AutoCAD (the
        reported DIMSCALE 100 case at (154.381, 30)), with DIMTMOVE 1/2, DIMJUST 3/4, DIMTVP, DIMTXTDIRECTION, slanting lines
        and moved-then-turned arrowheads; 17 probe sets 6 404 differing of 15 735 down to 377; 66 older sets byte-identical;
        harness .claude/gk/dims-harness13
  - [ ] Annotative objects 5b: DIMTMOVE 1 with very wide turned words (no leader in AutoCAD; tw9-tw11 298 differ); words
        put beyond a slanting line's end (tw17 58); exact ties; MLEADEREDIT's far-side arrow making its own leader (needs
        measured fonts); words' y offset by the ink depth; context group differences (90, 172, 91, 141, 295, record 45);
        the text width gap 177.4 vs 177.47 (maybe AutoCAD's live picture against its redraw, as found here) (S)
  - [ ] R2.2 follow-ups: draW boundary
        Arc; the Hatch dialog and HATCHEDIT for single colour and the new settings (design first); core one-colour Tinted and
        gradient truncation as AutoCAD (1/255); gap bridges from an end to the middle of a line; Boundary set as AutoCAD's
        console showed it (S)
  - [x] R2.3 SPLINEDIT completed (splinedit2): Fit data (Add, Close, Delete, Kink, Move, Purge, Tangents, toLerance), Join,
        Refine (Add, Delete, Elevate order, Move, Weight), Close/Open of control-point splines, met to 1E-5 against AutoCAD;
        a line joined into a spline runs its knots on by 1 as AutoCAD (core DxfJoin)
  - [ ] R2.3 follow-ups: toLerance refitting near the points as AutoCAD (tolerance 0.5 numbers in the merge); Elevate order
        past 11 (core MostDegree 10, AutoCAD 26); a core DxfJoinTests case for the line knots (S)
  - [x] R2.4 Dialogs (dialogs4, design v137-v138): Rename, Lineweight Settings (LWDEFAULT drawn, display scale), Check
        Spelling, Boundary Creation, and ribbon buttons in the slide-outs (Draw, Text, Leaders, Dimensions, Views)
  - [x] Dialogs 7 (dialogs7, design v143-v144): Annotation Object Scale (OBJECTSCALE) with Add Scales to Object; the Output
        ribbon tab; the batch buttons moved into the panel bodies (Hatch split in Draw, Check Spelling and Text Align, Align,
        Model Viewports); Plot Stamp user fields and .pss files; Boundary Creation New; an expandable Xbind tree; LWUNITS and
        LWDEFAULT
  - [x] Dialogs 8 (dialogs8, design v145): Insert Layout(s) and Template Options for LAYOUT Template and SAveas; the app
        menu's Import and AutoCAD's Export list (PDF runs EXPORTPDF); plot stamps on PNG/JPG and turned with an upside-down
        plot; one PDF of all layouts; LWDEFAULT's refusal in AutoCAD's words; fixed: the template description of R2010-R2018
        DWGs read as bytes (MEASUREMENT 256)
  - [ ] Dialogs 8 follow-ups: EXPORTPDF's own Save As PDF dialog with Current layout / All layouts (design first); .pss
        location codes 1-3 (need a .pss AutoCAD wrote); Collect in the Leaders panel (after MLEADERCOLLECT); WMF import;
        multi-line stamp and "." clearing in the GUI; look at the new windows in the running app (S)
- Round 3, what is left of the gap list (`.claude/gk/leader/pgp-GAP.md`):
  - [ ] R3.1 MLEADERCOLLECT (needs a core composite-block builder), MLEADERALIGN Parallel and TEXTALIGN on an oblique line
        (both need the AutoCAD GUI: the console left Parallel unchanged and shifted typed points by the last text; see the
        splinedit2 merge) (S)
  - [x] R3.2 (pgp-r32): OPEN typed (FILEDIA 0), DRAWINGRECOVERY over autosave copies, IMAGEADJUST/-IMAGEADJUST (typed),
        ATTEXT/-ATTEXT (CDF, SDF, DXX as AutoCAD writes them), CTABLESTYLE, QVDRAWING/QVLAYOUT and their CLOSE
  - [x] OLE frames drawn (fu-ole): the picture of a bmp/png OLE frame on the canvas and in plots (Monochrome by luminance),
        other objects as their frame and type; OLEFRAME, OLEHIDE, OLEQUALITY; four corner grips scaling about the opposite
        corner; ROTATE/MIRROR moving the frame upright; Properties group OLE; INSERTOBJ (IO) of a bmp/png; PASTECLIP of a
        clipboard bitmap
  - [ ] R3.2 follow-ups: RECOVERALL over xrefs; IMAGEADJUST's bitonal refusal; dialogs for IMAGEADJUST, ATTEXT,
        DRAWINGRECOVERY, the QV thumbnails and Insert Object (Create New, Link, Display as Icon) (design first); ATTEXT of
        layouts and nested blocks, a quote in a CDF value, INSERTOBJ/PASTECLIP placement and size, the OLE Properties wording
        and the OLEFRAME 0 grips checked in the AutoCAD GUI; OLE metafiles and cached presentation streams drawn (S)
  - [x] R3.3 ACIS regions (acis): a core SAT/SAB writer and reader of flat regions (DxfRegionShape,
        DxfUnknownEntity.CreateRegion/GetRegionShape), REGION and -BOUNDARY Region making regions, drawn, LIST/AREA/Properties
        with area and perimeter, EXPLODE; 12 regions in each of R2000-R2018 DXF and R2018 DWG open in AutoCAD 2021 with AUDIT
        0 and AutoCAD's own areas, perimeters and Explode curves
  - [x] R3.3 follow-ups 1 (regions2): UNION, SUBTRACT, INTERSECT of regions keeping arcs (core DxfRegionShape.Unite/
        Subtract/Intersect over chords traced back to their curves); MOVE, ROTATE, SCALE, MIRROR, COPY and STRETCH of regions;
        regions converted between SAT and SAB versions on Save As; multi-face regions; fixed: AutoCAD's transform record and
        whole-ellipse arcs read, Explode directions as AutoCAD; 19 regions x 6 versions AUDIT 0, 540 AutoCAD regions converted
        with equal Explode
  - [ ] R3.3 follow-ups 2: regions in DWG R2000-R2010 (DWG writer is R2018 only); -BOUNDARY Region islands and its message
        (GUI); UNION/SUBTRACT taking the first picked region (selection is kept sorted); a region's grips and UNION's colour
        reset checked in the GUI; spline edges and non-planar faces; orphan ACDSRECORDs; a parse cache for thousands of
        regions (M)
- Round 4, the follow-ups listed below (Dimension, Grips, Dialogs, Xrefs, Layouts, Table editing, PLOT, DXF versions, R12,
  VPCLIP, LAYERP), taken top to bottom, basics first.
  - [x] Dialogs 5 (dialogs5, design v139-v140): Multiline Style (with New/Modify page, Load), Multiline Edit Tools, the
        Hatch dialog's third column (one-colour gradient, colours, layer, transparency, origin, gap), Plot Stamp and PLOT's
        "Plot stamp on", Image Adjust, Attribute Extraction, Xbind
  - [ ] Dialogs 5 follow-ups (colours, linetypes and the Hatch origin pick done by dialogs6): Plot Stamp's user-field list
        and .pss files; an expandable Xbind tree; look at the windows in the
        running app (S)
- Checks only the AutoCAD GUI can give (accoreconsole cannot), gathered for the maintainer: LAYERP with U and a Layer
  manager session; the guessed prompts of SPELL, ATTIPEDIT and MLEADERALIGN Spacing/Distribute; DIMBREAK Auto breaks.

User note 2026-11-12 evening (note.md 1-14):
- [x] Global settings (settings-ui, design v133): Shortcuts keys captured as key presses; Import acad.pgp left of "Reset to AutoCAD defaults"; wheel zoom factor default 1.5; UI font as an editable font combo (or Windows font dialog); "Fonts" renamed "Folder preference" with only the folder paths, Font not found and Font mapping moved to "Text and fonts"; Drawing defaults, Data sources and Drawing text removed from Dxf.Viewer (they belong to the Tekla drawing part) (M)
- [x] New drawing gets the default layers of Global settings > Layers, colour and lines; Ctrl+3 toggles the ribbon area like Ctrl+1/Ctrl+2 (RIBBON/RIBBONCLOSE)
- [x] Save Drawing As listing each DXF version plainly (2018 down to 2000): DxfDatabase.Save(path, version), groups trimmed as AutoCAD's own files show, AUDIT 0 in AutoCAD 2021; DWG stays 2018 only (DECIDED by recommendation 2026-11-12)
- [x] DXF versions follow-ups 1: R12 written as AutoCAD's own (exploding what R12 lacks), FILEDIA 0 SAVEAS "Enter file format", DXFOUT and its version list, CadDocument.SaveCopy (saveas2)
- [x] R12 follow-ups 1 (r12b): 3DSOLID and REGION written as their edges, as AutoCAD (27 of 30 probes equal record for
      record, the sample's solids and region identical); MTEXT wrapped by font metrics (123 TEXTs, as AutoCAD); dynamic
      blocks as *U; fixed: symbol names, the AcDbAttr APPID, the anonymous flag and polyline vertex looks that made AutoCAD
      discard the R12 (the sample now audits as AutoCAD's own, 10 dynamic-block drawings AUDIT 0)
- [x] R12 follow-ups 2 (r12c): hatch R14_HATCH_DATA and ACAD_MLEADERVER xdata and ASC_BOUNDS on boundaries (24 of 24 sample
      INSERTs equal AutoCAD's), ellipse and spline polylines in AutoCAD's form, mesh and mline order, pieces in a block as
      INSERTs numbered by handle (95 blocks as AutoCAD), default groups left out, colliding names told apart; 22 probe cases
      open with AutoCAD's counts
- [x] R12 follow-ups 3 (r12d): solid fills as AutoCAD's horizontal strips plus side LINEs (32 of 36 shapes equal record
      for record), multileader frames and measured left-aligned TEXTs (15 of 15 equal), angular dimension words kept as
      one TEXT of AutoCAD's form (no extra *U block); R12 Save of round fills slower as AutoCAD writes 3x the records
- [ ] R12 follow-ups 4: round fills' corner count (circle r 25 -> 36 corners, 30 -> 76, ...); the star notch's inner
      sides; stacked \S words of linear dimensions (3 TEXTs and a bar); xdata normalisation (upper-case names, 5-group
      points); polyline default widths; fit-point SPLINE 255 vertices; tilted-hatch LINE extrusions; gradient band meshes;
      BODY, surfaces and spline-edge solids; annotative decomposition layers (S)
- [x] DXF versions follow-ups 2 (dxfver2): 3DSOLID and surfaces converted SAB <-> SAT across versions (core DwgSat.ToSab;
      220 conversions of 11 solids equal AutoCAD's records, 253 files open with the same volume, AUDIT 0); tables across
      R2007; visual styles across R2007/R2010/R2013; MTEXT columns up to R2018; fixed: R2000/R2004 solids and SAT solids
      to R2013+ written so that AutoCAD discarded the drawing, R2000<->R2004 tables (data table class name)
- [x] DXF versions follow-ups 3 (dxfver3): ACIS 4.00 of R2000 turned both ways (35 bodies x 6 versions: down 170 of 175
      AutoCAD's SAT digit for digit, up 130 equal and 40 within 1E-12, all 910 files open with AutoCAD's volume and area);
      R2000 solids and regions now written into DWG; visual styles' round-trip records written going down and read going
      up (the three styles' 176 flags); fixed: a turned solid (transform rotate) refused; BODY probed
- [ ] DXF versions follow-ups 4: ACIS 4.00 splines (sweep along a spline, loft of different circles, extruded closed
      spline: tolerant records and curve parameters, 3 attempts used); the table round trip going down
      (ACAD_ROUNDTRIP_2008_TABLE_ENTITY); R2000 MTEXTBEGIN xdata of leader MTEXT; reflect and shear transform words (M)
- [x] Cursor drawn onto a grip when it comes within the grip's reach, as AutoCAD; XCLIP / XC checked and made as AutoCAD (grips-xclip) (M)
- [x] XCLIP follow-ups: grips on the clip boundary (corners/vertices and the flip arrow, as AutoCAD's GetGripPoints); EXPLODE ignores the clip (probed, already so); the grip crosshair pull looked at in the running app
- [ ] XCLIP follow-ups 2: REFEDIT; Clipdepth planes in 3D views; the flip arrow's click and direction checked in the AutoCAD GUI; clip vertex drag in a tilted UCS (S)
- [x] Remove what is left of Part B (the Tekla drawing) from Dxf.Viewer; it moves to the Tekla drawing app (partb-cleanup: new-document hook neutral, MODEL tags, Hidden-lines recolour, Layer Owner column, DXF export rows gone; owner contract and drawing-plugin finder kept as neutral hooks) (M)

User note 2026-11-12 (basic features first, advanced later):
- [x] Grips: a polyline's midpoint grip drawn as AutoCAD's short bar, not a square; hovering a vertex grip offers Stretch Vertex / Add Vertex / Remove Vertex, a midpoint grip Stretch / Add Vertex / Convert to Arc; Grip size (GRIPSIZE) under Pickbox size in Global settings > Appearance > Cursor (design first) (M)
- [x] Grips follow-ups: MTEXT triangle and table grips sized from GRIPSIZE like the squares (look on screen at g 5) and their hover pink; Remove Vertex next to an arc and Add Vertex on an arc checked in AutoCAD; Undo inside Add Vertex; a clicked polyline vertex saying ** STRETCH VERTEX **; later: 2D POLYLINE, LINE/ARC/SPLINE/HATCH/dimension menus, Ctrl cycling (S)
- [ ] Grips follow-ups 2: Add Vertex options and the MTEXT triangle size checked in the GUI against AutoCAD; Convert to Line as a pick prompt as AutoCAD's string says (check the GUI); later: 2D POLYLINE, LINE/ARC/SPLINE/HATCH/dimension menus, Ctrl cycling (S)
- [x] Ctrl+1 toggles PROPERTIES (open closes, closed opens); DRAWINGS gets Ctrl+2 by default and toggles the same way (S)
- [x] Progress meter on the status bar for open/save, callable by commands and plugins (CadProgress, design v128) (M)

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
- [x] Leader follow-ups: every differing DIM* override in group order, DIMCLRT, hook along the UCS, INSERT copied and reused, LEADER/QLEADER previews, arrow pictures and User Arrow...
- [x] Leader follow-ups 2: Select Custom Arrow Block dialog, TOLERANCE entity, frame, command and dialogs, LEADER/QLEADER Tolerance, Copy/Reuse of a TOLERANCE
- [x] Tolerance follow-ups: snaps on row rectangles, overrides, editing (dialog, Properties), GeometricExtents, EXPLODE refused, exact gdt widths, leader set-back and 40/41, QLEADER constraints in a UCS, 211
- [x] Tolerance follow-ups 2: hook vertex as AutoCAD (3 vertices in a UCS and to frames), QLEADER 213, Near/Perp on row rectangles, hand-written group 1
- [x] Tolerance follow-ups 3a (fu-misc2): Intersection and Extension snaps on a frame's row sides only, as AutoCAD
- [ ] Tolerance follow-ups 3b: End snap of the picked side only (needs a real aperture); the dialog's rows over six cells
      and hand-written rows; LEADER/QLEADER Tolerance in a UCS; Properties rows and glyph placement (GUI) (S)
- [x] Dimension overrides: every differing variable written in group order (where $DIMSTYLE names a style the drawing has), DIMSCALE 0 through a viewport, arrowhead pictures in the style dialogs
- [x] Dimension overrides follow-ups: blank DxfDatabase holds ISO-25 as acadiso, header drives every dimension and leader, AutoCAD's 279/288/50 extras
- [x] Dimension overrides follow-ups 2: words moved out under DIMTOH as AutoCAD, arrowhead and word fit thresholds, chains in the base's style, 279 only on DIMCONTINUE, 288/50 confirmed
- [x] Dimension follow-ups 3: group 11 by the words' start under DIMTMOVE 1, rows at the base's DIMSCALE on its layer; txt.shx measuring was right (the 0.1838 was Arial)
- [x] Dimension follow-ups 4: SHX ink as AutoCAD (13 fonts), DIMCONTINUEMODE, radial group 11 and DIMTMOVE 2 pictures, the 15-degree no-landing rule
- [x] Dimension follow-ups 5: DIM's radial and continue/baseline prompts (AcDimRes strings); radius/diameter words near the rim (941 of 2742 differing down to 31); upright-line landing direction (8 of 448 down to 0)
- [x] Dimension follow-ups 6: radius/diameter written as AutoCAD writes them (leader 0; 164/163 only where the words stand at the point given, probed over 192 combinations); AutoCAD's regen of our files differing 723 to 0 on 1920 cases; level-word landings near level
- [x] Dimension follow-ups 7: DIMTIX radius/diameter pictures (2 063 of 2 752 differing down to 75); DIMTEDIT at DIMTMOVE 0 by DIMATFIT (625 of 1 024 down to 1); AutoCAD's regen of moved ordinate/linear/aligned/angular words (114 of 289 down to 5)
- [x] Dimension follow-ups 8 (dims8): level words in a tight radius/diameter fit as AutoCAD (the r-sets: 603 differing down
      to 60 of 6 336 probes), the angular leader threshold refitted over 5 800 probes, DIM Angular on an arc switching back
      to Radius/Diameter; harness kept in .claude/gk/dims-harness8
- [x] Dimension follow-ups 9 (dims9): words near an extension line laid as AutoCAD (m3 125 differing of 588 down to 0, m4
      41 of 4 680 to 0), DIMTEDIT's DIMTMOVE 1 leader threshold (gap + max(arrow, height/2 + DIMEXE)), a jogged radius
      keeping level words level with AutoCAD's stubs (j1 162 to 18), the arc break of wide angular words (a2 to 0), letters
      Arial lacks measured as AutoCAD sets them; harness in .claude/gk/dims-harness9
- [x] Dimension follow-ups 10 (dims10): the angular leader band by the point's direction (a4 74 to 15 of 712, a7 941 to 159
      of 5 832), level landings W or L inside a circle under DIMTAD 1/4 (t6 99 to 15, r6 36 to 8), jogged words straddling
      the rim (j1 18 to 0, j2 466 to 16 of 1 136); the arc-length threshold under DIMTMOVE 1 follows the new band; harness
      .claude/gk/dims-harness11
- [x] Dimension follow-ups 11 (dims11): the angular leader band's jump at the arc middle's height (a7 159 to 101, a6 29 to
      21), DIMTMOVE 2 free radial words breaking where the line crosses their box (r6 8 to 0, d3 176 to 7), jogged words on
      the rim counted inside (j2 16 to 0), the near-level tie; harness .claude/gk/dims-harness14 (set a: 4 changed cases
      without AutoCAD pictures)
- [ ] Dimension follow-ups 12: the angle band's shape (a5 49, a7 101; the cos fit is empirical); diameters put out (d2 156
      of 924; t6's D4-D12 windows); the remaining td3 cases (R20 box corner); exact ties at two arrowheads (float noise?);
      AutoCAD pictures for set a's 6-degree cases; points exactly on an aligned extension line; lines at exactly 15 degrees (S)
- [x] PEDIT: PEDITACCEPT and PLINETYPE through SETVAR; 3D polylines (options and vertex editing to 1E-8); Extend for arc
      ends in a fuzz join (20 cases to 1E-10) (S)
- [x] REVCLOUD: the 2021 approximate arc length and REVCLOUDARCVARIANCE (from the help); Freehand preview was already in
- [x] PEDIT/REVCLOUD follow-ups 1 (fu-misc2): PEDIT Multiple with 3D polylines; JOIN with a POLYLINE as AutoCAD (and at
      PLINETYPE 0, inferred for multiple-object JOIN and BOUNDARY)
- [ ] PEDIT/REVCLOUD follow-ups 2: REVCLOUD's first arc length from the view (console gives 0.5 at any view); the spline to
      polyline precision rule (173/226/452/868/1693 vertices at precision 1/2/5/10/20 for one cubic; no formula fits);
      JOIN multiple and BOUNDARY under PLINETYPE 0 checked in the GUI; REVCLOUDPROPERTIES and Modify (S)
- [x] Layouts: twisted viewports drawn and entered as AutoCAD (two bugs: the turn's sense, group 12 in view coordinates);
      one undo step per wheel or pan gesture; ZOOM Previous per viewport (20 each); -SCALELISTEDIT; Isolate in the Layer
      manager; New VP Freeze on viewports made through the core's API (S)
- [x] SCALELISTEDIT dialog, in-place layer rename, Edit Layer State with Description, Input Search Options (designed, checked against AutoCAD 2021)
- [x] Dialog follow-ups: system variables after Tab; Remember corrections 1-50 (AutoCAD's own range); a drawing without $MEASUREMENT resets to the imperial list (probed)
- [x] Dialog follow-ups 2: the Reset/duplicate-scale texts as AutoCAD 2021's (redefine on Add, Reset task dialog)
- [x] Dialog follow-ups 3: Reset's "Metric and Imperial scales" button; multi-select Delete with AutoCAD's in-use question; 8 hand-written question texts as AutoCAD's task dialogs
- [ ] Dialog follow-ups 4 (need the GUI): the Metric and Imperial list order; the save-on-close dialog (acadres 47128 or TaskDialog_Core3); the View Manager's replace question; the "Large Number of Scales" trigger; the choice window's width; visual style content once visual styles exist (S)
- [x] Layouts follow-ups: wheel and middle-button
      views in the ZOOM Previous list; typed ZOOM/PAN as undo steps in model space and paper, as AutoCAD; the screenshot
      mode drawing the white paper (S)
- [x] Layouts follow-ups 2: U after ZOOM P giving the Previous entry back; the layout switch as an undo step (AutoCAD makes it one); the toolbar Undo recording a gesture within 80 ms
- [x] Layouts follow-ups 3a (fu-layouts): CTAB; MSPACE/PSPACE, LAYOUT Set, -VIEW Restore across spaces and Find's Zoom to
      objects as undo steps named as AutoCAD names them ("Already in ..." an empty step, as AutoCAD)
- [ ] Layouts follow-ups 3b: ZOOMFACTOR mapping (GUI); -VIEW Restore of a paper view in the layout it was saved in, and of a
      model view from a layout ("Select Viewport for view:"); the screenshot mode's paper compared with AutoCAD's (S)
- [x] Styles: MLEADER with no style makes Standard; Block and None content of multileader styles; the scale options
      greyed when Annotative is ticked (S)
- [x] Associative dimensions round 3: flag 32 written so a plain MOVE takes the picture; copies get their own picture and
      stay tied to copied objects; intersection snaps; ties from DIM, QDIM, DIMCONTINUE, DIMBASELINE, ordinate, angular
- [x] Associative dimensions: PERpendicular and TANgent snaps tied with AutoCAD's "last point" reference
- [x] Associative dimensions: PER and TAN at a first origin, on polylines, for angular and ordinate; AUDIT 0 in AutoCAD
      and ours; 16 ties followed through 7 states as AutoCAD to 1E-3; STRETCH of rotated dimensions and polyline arcs (S)
- [x] Associative dimensions follow-ups: extended perpendicular, DIMANGULAR on a circle, groups 11/42, the angular arc point (1247 of 1335 replayed values match AutoCAD)
- [x] Associative dimensions follow-ups 2: newest object at a shared end, two-line order, DIMLINEAR line after a move, words from the leader end, polyline arc second point, DIMARC association, zero angles (238 of 262 states match)
- [x] Associative dimensions follow-ups 3 (fu-misc): DIMARC's arc point under 90 degrees and two thirds round when the
      words do not fit, kept after a scale; angular arc points two thirds round likewise (88 of 88 AutoCAD cases equal);
      DIMARC of a polyline arc segment; Partial length order and parameters (16 cases); flag 128 checked
- [x] Associative dimensions follow-ups 4 (dimarc4): DIMARC's level words fitted by what their box takes across the arc's
      turn, as AutoCAD (the 4 regressed rows and 33 of 48 small-arc rows now 0 off; tar sets 7 031 differing of 11 156 down
      to 1 962), the outside side from the arc's start direction, extension lines of a dimension on its arc, the sector
      test after a scale; harness .claude/gk/dims-harness10
- [x] Associative dimensions follow-ups 5 (assoc5): angles fit level words across the arc's turn as DIMARC (tar16 56 of
      432 to 4), the angle's words left or right by its arc point, DIMJUST 1/2 of DIMARC (tar9 288 of 864 to 0), DIMARCSYM
      1 in the fit, the radial room rule (tar24 1 656 of 5 152 to 415); 45 untouched sets byte-identical; harness
      .claude/gk/dims-harness12
- [x] Associative dimensions follow-ups 6 (assoc6): level words on the arc fitted by their width times the sine and their
      height times the cosine of the arc middle's direction (within 0.001 over 340 rows), DIMJUST 1/2 words beside a ray
      (tar17 262 of 408 to 0, tar20 108 of 144 to 0), the room between the arrowheads on wide sweeps, DIMARCSYM 1's height
      (tar16 4 to 0, tar25 64 to 20); 68 other sets byte-identical; harness .claude/gk/dims-harness15
- [ ] Associative dimensions follow-ups 7: the group-10 coin flip of words in the middle (about 790 rows of tar24, 380 of
      tar2); DIMARCSYM 0 radial over 120 degrees; the real rule behind the empirical 0.8-height "bothExtra"; tar13 k0.8 rows
      0.001-0.002 off; level words not on the arc (DIMTAD 2/3) unprobed; AutoCAD's arc-point offset after a scale (S)
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
- [x] Dynamic blocks: visibility states list every parameter, XY corner and block-table grips, live preview of custom grip drags, GRIPDYNCOLOR
- [x] Dynamic block follow-ups 1 (fu-misc): the insertion grip last, as AutoCAD orders grips; grip colour already ACI 140
- [x] Dynamic block follow-ups 2 (dynblock2): the base point of a linear or XY parameter moved by a grip, as AutoCAD 2021
      (corner by (20, 10): Width 80, right edge kept), core DxfDynamicBlockReferenceProperty.MoveBasePoint, base grips where
      a base-following action needs them, base-following stretch/move/array and ATTRIBs following the drag only;
      UpdateAnonymousBlocks after a base move; AUDIT 0 on ten outputs with AutoCAD's values read back
- [x] Dynamic block follow-ups 3 (dynblock3): the hidden Origin property of each parameter as AutoCAD reads it; GRIPLOC
      history for polar grips and the replace-last rule; a polar base moved by its grip (MoveEndPoint, MoveBasePoint);
      value sets applied after a base move; the insertion grip left out where a custom grip stands on it; 26 INSERTs AUDIT
      0, 23 equal AutoCAD's values to 6 places. NOTE for a release: DynamicBlockReferencePropertyCollection has more items
      (the hidden Origin), written up as NEW with the advice to filter on Show
- [x] Dynamic block follow-ups 4 (dynblock4): polar grip edits replayed one after the other from the history, as AutoCAD
      (end, base, end: (45.434873, 25.724788)); base-following stretch, move and array kept at the base point at every
      later value set (settled by probe in memory, DXF and DWG); Origin settable for linear, XY and polar; GRIPLOC for
      linear/XY base and end grips; history kept in block units and started for array and scale blocks; about 1 500 random
      edit sequences equal AutoCAD's, ten files AUDIT 0
- [ ] Dynamic block follow-ups 5: ATTSYNC on a moved INSERT (not in the console or acdbmgd); GRIPLOC for point, rotation
      and other XY corner grips (written as property records now); a value-set tie AutoCAD breaks by float noise; a cheaper
      history replay (+8% on polar sets); the block-table grip menu and glyph (GUI); the Design artifact's grip colour (S)

### To do

- [ ] DWG, our own reader and writer (the maintainer's choice: code of our own, to change at will). Sources: the Open
      Design Specification for .dwg (the main one), ACadSharp (MIT: may be ported, with its notice), LibreDWG (GPLv3: its
      documentation only, never its code). The reader turns a DWG into the library's DXF records, so everything built
      works on DWG; the writer goes back. Checked against DWGs and DXFs AutoCAD 2021 writes through a probe plugin, AUDIT
      of what we write, ACadSharp as a side check in scratch only.
  - [x] W1 Bits and R2000 (AC1015): bit codes, CRC, header, section locator, header variables, classes, tables, blocks,
        the common entities and objects, the handle map (L)
  - [x] DWG follow-ups (dwg7): SAT-text 3DSOLID/BODY/REGION written into DWG R2018 as SAB (11 solids and 3 regions from
        R2004-R2010: AUDIT 0, volumes, areas and centroids equal AutoCAD's); the preview image read and written
        (THUMBNAILIMAGE); VIEWPORT, HATCH, LEADER, MULTILEADER, MLINE, IMAGE, WIPEOUT, meshes and MINSERT checked object by
        object against AutoCAD's own DWG (bytes equal but the last bit of a double); records without a handle counted and
        warned; DxfColor.ColorValue of BYLAYER white as AutoCAD (CHANGED)
  - [x] DWG follow-ups 2 (dwg8): FIELD, FIELDLIST, SORTENTSTABLE and DATALINK read (2 646 fields in 96 sample drawings as
        AutoCAD's DXF gives them) and written; VISUALSTYLE of R2004-R2010 written into R2018 (390 of 390 equal AutoCAD's);
        fixed: a block record owner's handle code and a multileader attribute code; draw order made in code no longer
        dropped; 19 drawings AUDIT 0; DxfColor Red/Green/Blue of ByAci kept as the palette (documented, AutoCAD gives 0)
  - [x] DWG follow-ups 3 (dwg9): fixed two faults that made AutoCAD refuse our rewrites (ErrorStatus 53): the R2010+ 2D
        polyline vertex id written after its tangent, and a locked attribute of an R2007 DXF given version 1; AutoCAD's
        113 sample drawings read and rewritten: 98 AUDIT 0 and 8 refused before, now 106 AUDIT 0 and none refused (the 7
        others have their originals' own errors); tables holding fields and data links read and written; R2000/R2004
        DATATABLE and TABLECONTENT read
  - [x] DWG follow-ups 4a (dwg10): fixed splines_AC1024 refused (SAT-to-SAB: a pcurve's two reals and a loft's extra
        number in R2013+), table styles without a row text style (Standard written), Unicode escapes of old DXF names and
        texts written as letters; 71 of 71 committed test DWGs and 113 of 120 AutoCAD drawings rewritten at AUDIT 0 (the 7
        others carry their originals' errors), none refused
  - [x] DWG follow-ups 5a (dwg11): fixed a data loss: the linear-parameter dynamic blocks' evaluation graph and its six
        object types read and written (ported from ACadSharp, corrected against AutoCAD), so AutoCAD keeps the anonymous
        block with its attribute definitions (120 drawings: FIELD 2 598 to 2 646 and SORTENTSTABLE 248 to 256, equal to the
        originals); graphs we cannot write left out whole; double.MaxValue written as AutoCAD's 1.797693134862314E+99;
        CELLSTYLEMAP of R2000-R2007 read; custom cell styles without a text style; \M+ escapes
  - [x] DWG follow-ups 6a (dwg12): the dynamic block graphs of visibility, flip, rotation, XY, polar, point, base point,
        lookup and alignment parameters and polar stretch, move, scale, rotate, flip, array and lookup actions read and
        written from R2007: 72 of the 90 graphs of AutoCAD's samples go through (12 before), 1 492 objects equal AutoCAD's
        DXF; fixed: the unset value 1.797693134862314E+99 written as AutoCAD's double.MaxValue
  - [ ] DWG follow-ups 7: the other 18 graphs (constraint parameters on ACDBASSOC*, user parameters, properties tables,
        proxy nodes, BLOCKPARAMDEPENDENCYBODY); the order of a stretch action's entity list (15 of 111 differ); dynamic data
        below R2007; sweep, revolve and drafted/path/ruled lofts in ToSab; ACFD_FIELDTEXT_CHECKSUM; OLE2FRAME and proxies;
        surfaces; tcustom parity; TABLEGEOMETRY head bit; a rendered thumbnail; R2000 round-trip colours (M)
  - [x] W2 R2004, R2010, R2013, R2018 (AC1018-AC1032): pages, LZ77 compression, the encrypted header, the object stream
        of 2010 on (L)
  - [ ] W2 follow-ups (palette and colour-book/multi-line attribute drawings done): spline normal sign off XY (8 of 10 match; parked) (S)
  - [x] W3 R2007 (AC1021): its own layout, Reed-Solomon, Unicode strings (M-L); the ACI palette made AutoCAD 2021's
        (197 of 255 indexes were wrong)
  - [x] W4 Every entity and object: VIEWPORT, HATCH, LEADER, MULTILEADER, TOLERANCE, IMAGE, WIPEOUT, MLINE, tables to
        R2007, solids in R2000/R2013/R2018, book colours, multiline attributes; 0 differences in all 6 versions (L)
  - [ ] W4b (done: tables from R2010, TABLECONTENT, CELLSTYLEMAP, RASTERVARIABLES/WIPEOUTVARIABLES since W5b; MATERIAL and
        VISUALSTYLE read in every version and written; ACDSDATA read from R2013; view styles and the subentity manager
        read and written; REGION/3DSOLID/BODY written from R2013 with their AcDs store, AUDIT 0; SAB to SAT for
        R2004-R2010 byte for byte; SHAPE names via DxfReadOptions.SupportFolders, with no SHX the number under group 70
        as AutoCAD writes it, DECIDED by recommendation 2026-11-12): advanced, later: SAT to SAB for writing R2018 from
        SAT text; a SHAPE given by name alone; OLE2FRAME (needs GUI
        samples), proxies, dynamic block data; a _data_ segment past a few records and multi-page blobs unprobed (L)
  - [x] W5 Writing R2018 (AutoCAD 2021's own): `DwgWriter`, our own LZ77; 19 drawings open in AutoCAD with AUDIT 0 and
        round-trip; files 70-75 % of AutoCAD's size (L)
  - [x] W5b: write MULTILEADER, MLEADERSTYLE, ACAD_TABLE (R2010 form, TABLESTYLE, TABLECONTENT), multiline attributes, RASTERVARIABLES/WIPEOUTVARIABLES; reader reads R2010+ tables
  - [x] W5b follow-ups: cell and table overrides both ways, TABLECONTENT (R2007+) and TABLEGEOMETRY read, border edges fixed, clean table sample AUDIT 0, left-out test
  - [x] W6 GeometryHelper DXF: Open and Save As DWG, drag and drop, DWG xrefs (M)
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
- [x] Previews follow-ups 2: a look by eye in the running app; text items moved, not redrawn, in the paste preview (6 000 texts: 65 to 12 ms a move on net48). Done: paste preview 3x faster, -ARRAY row distance in drawing units, TEXT Align (AutoCAD shows no box), ALL on hand-written DXF
- [x] Maintainer's AutoCAD pictures (2026-11-12): SCALE/ROTATE faded originals and own-colour copies, amber band, one scale field; window/crossing colours and edges, plain cross while dragging; Shift+right-click access keys; screenshot mode no longer shows real dialogs
- [x] Pictures follow-ups: implied window prompt with absolute DYN fields and F/WP/CP, no base-point square, cursor badges, AutoCAD's osnap menu letters, AutoComplete list of 5 rows, SCALE of 100 000 timed (25 ms a move)
- [x] Pictures follow-ups 2: canvas right-click menus Default/Edit/Grips/Command from AutoCAD's menu file, SHORTCUTMENU 11, implied window inside Select objects, osnap labels and separators
- [x] Canvas menu follow-ups: AutoCAD's object menus (POP504-536, e.g. the table menu's Size Columns/Rows Equally, Export, Table Style) merged into Edit; Recent Input submenu; Point Filters separator after .Z; a typed point that hits nothing at Select objects; transparent PAN/ZOOM; a SHORTCUTMENU variable and right-click customization; design boards for the four menus; fading of MOVE/ROTATE/ALIGN/grips (needs AutoCAD pictures) (S)
- [x] Canvas menu follow-ups 2a (fu-misc): Remove Style Overrides (DIMOVERRIDE Clear), the spline items, Viewport Clip and
      Image Adjust wired; Recent Input in the drawing's units and "Selection set (N items)"; transparent 'SETVAR, 'OSNAP,
      'ORTHO, 'POLAR, 'GRID, 'SNAP
- [ ] Canvas menu follow-ups 2b: Table Style apply, TABLEEXPORT, HATCHSETORIGIN, REFEDIT, VPMAX items (commands not built);
      the right-click customization dialog and the Command/Edit menu boards (design first); Recent Input format checked in
      the GUI (S)
- [x] Managers follow-ups 2, non-UI parts: viewport overrides on restore; the xref rule of GetLayerStateNames; ANGBASE and
      ANGDIR on typed and shown angles; disabled GhComboBox and GhFlatButton greyed; Groups in PURGE and -PURGE (M)
- [x] Managers follow-ups 3: ANGBASE/ANGDIR in the dimension and ellipse rows, DIMEDIT oblique, MEASUREGEOM, the <a angle override; ATTSYNC refusing a block with no definitions; dialog buttons dimmed once
- [x] Angle follow-ups: ATTSYNC keeps XData and names the dynamic block, the angle override in the DYN field, LIST of ellipses and dimensions as AutoCAD
- [x] ATTSYNC and LIST follow-ups: *U INSERTs synced, LIST names, jogged radius, UCS rotations, Y ordinate and associative pinned
- [x] LIST/blocks follow-ups: BSAVE works out *U blocks again as UpdateAnonymousBlocks, LIST dimension style overrides, DIMJOGANG override in radians
- [x] LIST/blocks follow-ups 2a (fu-misc2): LUNITS 1-3 past 12 characters in scientific form (1.000500E+03); a linetype
      override naming no linetype as AutoCAD lists it; dimension setters writing overrides equal to the style, as AutoCAD;
      ATTRIBs of locked action-held ATTDEFs rebuilt under UpdateAnonymousBlocks
- [ ] LIST/blocks follow-ups 2b: ATTSYNC placement for *U (not in the console or the 2021 API); parameters added or removed
      in the Block Editor (GUI); UpdateAnonymousBlocks probed for actions other than stretch; whether Properties skips a
      typed value equal to the one shown (S)
- [x] Command line and Recent: Search Content; EXPORT and -EXPORT (AutoComplete inside a running command dropped: AutoCAD
      2021 has none) (S)
- [x] Command line follow-ups: collapsible content section, hatch thumbnail on hover (design v132)
- [ ] Command line follow-ups 2: EXPORT writers for Metafile and the other refused types if wanted; -EXPORT Dwf/dwfX and All
      layouts (S)
- [x] Dxf.App: an app icon, file association for .dxf/.dwg (HKCU, --register/--unregister), `--tab Home` in screenshot mode (design v131)
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
- [x] Snap follow-ups 2: SNAP Legacy, paper-space grid toggle and drawing, GRIDDISPLAY 8 and SNAPMODE 2 probed, a number as a distance at point SETVARs, the dialog's negative POLARDIST, "Grid too dense to display"
- [x] Snap follow-ups 3: snap and grid per viewport and paper space, a viewport's grid drawn through it, LASTPOINT kept at SETVAR, SNAPGRIDLEGACY in the settings file
- [x] Snap follow-ups 4: every viewport's grid drawn, GRIDSTYLE through a viewport, twisted limits, SNAPMODE 2 per space, undo reloading snap/grid, the SNAPBASE puzzle (a running snap, no change)
- [x] Snap follow-ups 5: snap/grid SETVAR, GRID/SNAP and F7/F8/F9 as cheap undo steps (F7 joins the previous command as AutoCAD 2021 does, DECIDED by recommendation 2026-11-12)
- [x] Snap follow-ups 6: non-rectangular viewport clips (VPCLIP Polygonal/object/Delete, content, grid, picking and highlight clipped; DXF/DWG AUDIT 0)
- [x] VPCLIP follow-ups 1: ROTATE/MIRROR/MOVE of a clipped pair as AutoCAD (twisted view, about 30 cases to 4 decimals), pasting a clipped viewport into another drawing, image and plot clipped (pixel tests), a drag-vertex canvas test (vpclip2)
- [x] VPCLIP follow-ups 2 (vpclip3): a REGION (with holes, two faces, ellipse) as a viewport's clip, drawn and plotted
      even-odd; MVIEW Object; ROTATE of an unclipped viewport clipped as AutoCAD (view centre R(twist)(V - T), 8 probes) and
      MIRROR of one; MIRROR copy of a clipped pair about a diagonal off the origin (V + (C' - C)/scale when the box changes
      shape); fixed: a click on a clip boundary opened the selection-cycling list, a pair pasted into a layout never shown
      audited with 1 error in AutoCAD (now 0)
- [ ] VPCLIP follow-ups 3: viewport grids seen from the paper and GRIDSTYLE through a viewport against an AutoCAD screenshot
      (GUI); a region off the XY plane as a clip; MVIEW Polygonal and the other MVIEW options; a pixel test of a region clip
      in plots; a cache of OutlineOf for region boundaries (123 us a call) (S)
- [x] POLARDIST answered by two points at SETVAR (AutoCAD: "Specify second point:", stores the distance); needs the command line to take a point at a distance prompt with no base point (deferred by the maintainer 2026-11-11) (S)
- [x] Tests: DisplayPatchSpeedTests flaky under load: each phase best of 3 after a full GC inside a no-GC region;
      0 of 20 failed under load (was 5 of 20), limits unchanged (S)
- [ ] COPYVIEW (not in AutoCAD): the view as an image on the clipboard, also on the canvas right-click menu (optional)
- [x] Tables: the cell right-click menu, column letters and row numbers, autofill and width grips, drag-select a range
- [x] Table editing follow-ups: Resize from the menu, snaps on table drags, row DYN field, up-flowing tables, TABLEINDICATOR on the machine, a typed whole number kept as a number
- [x] Table editing follow-ups 2: decimals, 1e3, fractions and points typed as AutoCAD stores them, series in each source's format; AutoCAD offers a perpendicular back onto the base point (pinned)
- [x] Table editing follow-ups 3: the in-place editor opening a value as AutoCAD's ForEditing text; RealText rounding through Fixed; thousands (%th) and feet-inches typed; text typed over a number refused
- [ ] Table editing follow-ups 4: dates and times typed (needs a DXF sample with a date cell); RealText under LUNITS 3 %pr; band and size-field sides of up-flowing tables (needs a screenshot of AutoCAD); the table-drag-only perpendicular filter (S)
- [x] Tables: rows grow for wrapped text; Tab in the last cell adds a row; first/second/other row cell styles
- [ ] Tables follow-ups (the row growing live while a cell is typed: done 2026-11-12):
      a block merged across rows (shortfall to the last row, unprobed); the table's own per-kind looks (S)

- [ ] Ribbon: check drop-downs and the Insert block gallery at 125 % and 150 % display scaling; pin and Esc by hand
- [ ] Ribbon: Home panels a few pixels wider than their design widths
- [x] Speed: undo without reloading the whole drawing at 1 000 000 entities: read-ahead of the next undo and redo drawings (0.4 s where 2.2 s, once read ahead), states packed in 4 MB pieces; no core fork
- [x] Speed follow-ups: edits stop read-aheads, the drawing left reused (1M: 47 to 35 GB allocated, redo 1.6 s to 0.3 s), first undo after BSAVE/discard read ahead, marker bytes compared
- [ ] Speed follow-ups 2: peak memory at 1M unchanged (each read-ahead side ~520 MB; slimmer core entity objects or no redo read-ahead after a live edit; DECIDED by recommendation 2026-11-12: keep the read-ahead, slim the core entity objects later as its own task); a stop check inside DxfFile Open's record walk; display items are 42 of 49 MB of layer on/off (S)
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
- [x] Try audit follow-ups 2: flat and thin boxes crossed as their rectangle/segment/point, their booleans refused, LiesAlong, NaN Locate
- [x] Try audit follow-ups 3: sliver triangle distance and closest point, Corner3 infinite radius, NaN shapes meet nothing (net48 spline hang gone)
- [x] Try audit follow-ups 4: NaN input: 1 700 faulty members down to 33 (done 2026-11-12); the 33 left are instance members of a NaN shape (DECIDED by recommendation: throw InvalidOperationException "the shape has a coordinate that is not a number") plus GeoXLine2.ClipTo and two arc constructors to name; docs say a NaN shape meets nothing while a polygon with one NaN vertex meets through its finite sides (DECIDED: narrow the docs to the NaN part); GeoLine3.GetPlaneWith documents the wrong exception; Clash.Neutral +3 % to check; DECIDED by recommendation 2026-11-12: keep the LN5 rule (a box thinner than the tolerance on one axis is booleaned as a body, the cube less it is the cube) and the documented segment-in-plane rule (no point), the flat box's outline crossing noted as the one exception (S-M)
- [ ] Try audit follow-ups 6 (advanced, later): re-read the 404 members reporting a meeting with a NaN shape against the narrowed rule; 132 members with their own InvalidOperationException messages; GetPlaneWith names `normal` for a parallel direction (S)
- [x] MTEXT: word wrap at the width, stacked fractions, columns; line spacing 5/3 as AutoCAD (check first)
- [x] MTEXT in-place editor lays out with the drawn MTEXT's layout (wrap, spacing, stacks, columns, mask, frame)
- [x] Speed: DisplayPatchSpeedTests "layer on" failing: no merge to blame; the three 100k speed tests ran in parallel and
      their collections stopped each other's timers (now one serial collection); layer off 85 -> 23 ms by testing the
      layer before the entity's own visibility (S)
- [ ] MTEXT parked: `\pxsm2` with tall runs (0.048 h), flow direction 3, `\pxsa` first line, `\M+` codes, mask with a taller defined height, manual columns at bottom or middle attachment, column grips (M). Done: ruler, dialogs, `\W..x`, caret, EXPLODE run fonts and colours, TEXT %%U/%%O/%%K, warm display of 10 000 MTEXTs
- [x] MTEXT ruler follow-ups: collapsed-panel launcher, Columns option into the editor, \W..x with the style width, caret at soft breaks, numbering codes
- [ ] MTEXT follow-ups 3: Bullets and Numbering menu and AutoCAD's MTEXT and column grips done 2026-11-12; grips with reversed column flow and in R2007-R2013 files (columns in xdata); overflow column grip; column grip drag preview; Start/Continue/Allow Auto Bullets; static/dynamic defaults from no columns (needs GUI); undo-step labels (unclear); TrueType %%U/%%O/%%K lines; first-draw cost of the layout cache; MIRROR/InkOf measuring %%U as letters; core DxfFonts \W..x; a look at the ruler in the running app (S)
- [x] Clipboard: preview of the objects while PASTECLIP asks for the point
- [x] PLOT: print to Windows printers; add named page setups (PAGESETUP manager, PLOTSETTINGS in DWG)
- [ ] PLOT follow-ups: a real print on hardware and Microsoft Print to PDF; -PLOT choosing a printer; Plot to file for a printer (refused today); flag 1024 written for model-space page setups; Help anchors checked on the built docs; vector printing if pixels are not enough for plotters; embed only the glyphs used (smaller PDFs, our own TrueType subsetter) (S)
- [x] Xrefs: XOPEN; editable xref layers with AutoCAD's override bits (AcXRefLayerPropertyOverride); nested xrefs in the
      palette (list and tree); a changed referenced file said on the command line
- [x] Xrefs round 3: XATTACH writes the `NAME|LAYER` and `NAME|LINETYPE` records as AutoCAD does; -LAYER Transparency,
      Plot and Description; VISRETAIN 1 and 0 as AutoCAD; "Needs Reloading" as the 2021 help (S)
- [x] Xrefs follow-ups: Unload/Reload/Detach on every selected row (one step); names with wildcard characters escaped; AUDIT of a saved bound drawing and its DWG save (0 errors, 4 files)
- [x] Xrefs follow-ups 2a (fu-layouts): -XREF Unload/Reload of nested references in AutoCAD's words (orphaned, unreferenced,
      has not changed, loaded: path, has been restored), parent before nested; a nested unload hides the nested drawing;
      -IMAGE and -XREF no-match messages; Bind refusing an unreferenced xref as AutoCAD
- [ ] Xrefs follow-ups 2b: palette Bind for Unreferenced rows (GUI); `N$0$NL` and AcXRefLayerPropertyOverride (console cannot
      tell); screenshot mode for the two windows; AutoCAD's callout blocks for MLEADER block content; the status-bar change
      balloon; grandchildren in the orphaned lines (S)
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

Left by the Part A cleanup (2026-11-12), for the Part B replan: the Drawing app's own settings dialog (ExportLayout, OpenAfterExport, presets, sources; SettingsSources moved into DrawingWorkspace); hidden lines now drawn in the layer's ACI 8 grey, not #9AA4B1; the model icon on the New offer; the shared "This line belongs to the model" refusal text and the plugin "drawing" kind names.

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
