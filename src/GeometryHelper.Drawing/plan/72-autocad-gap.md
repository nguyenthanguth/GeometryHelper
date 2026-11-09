# What the presenter lacks against AutoCAD 2021

A list, made on 2026-11-08 after the user found the Annotate and View tabs dead, of what AutoCAD 2021's ribbon (its six
tabs Home, Insert, Annotate, View, Manage, Output), quick access toolbar, status bar and command set have, against what
Part A (the 2D presenter: `GeometryHelper.Dxf.Viewer`, `.Dxf.Viewer.Wpf`, `Drawing.App`) has. Each item is marked:

- **built**: there, and reached from where AutoCAD puts it; **(r1)** marks what the round "ribbon1" added, **(g1)** what
  the round "cmdgaps" added (commands typed only: no ribbon button yet), **(c56)** what phases C5 and C6 added.
- **cheap**: under some 150 lines on what the library and the presenter have now.
- **big**: needs a new part (a dialog, a clipboard format, a geometry tool, a block editor).
- **out**: out of scope for a 2D presenter of DXF (3D, rendering, cloud, sheet sets).
- **queued**: found by the user and given to the agent after ribbon1 (canvas speed, selection look, the layer colour
  picker, dynamic input).

The rule kept: a button for what does not exist is hidden or built, never left dead; one waiting for a step on hold (C4,
PDF, Print) stays, disabled, its tooltip saying "not built yet".

## Ribbon: Home

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Draw | Line, Polyline, Circle (centre-radius, 2P, 3P), Arc | LINE, PLINE, CIRCLE (2P, 3P), ARC | built |
| Draw | Rectangle | RECTANG | built; button (r1) |
| Draw | Ellipse (axis, centre) | ELLIPSE by axis ends or centre, other half or rotation; Arc (start, end or included angle) | built (r1); Arc built (g1) |
| Draw | Multiple Points | POINT, one point a run | built (r1); PTYPE dialog cheap |
| Draw | Circle TTR / TTT, Arc's eleven ways | the common ways only | cheap each |
| Draw | Hatch, Gradient, Boundary | hatches drawn, not made | big (C4) |
| Draw | Polygon, Construction Line, Ray, Spline, Donut, Revision Cloud, Wipeout, Region, Divide, Measure | XLINE (no Offset, no Reference), RAY, POLYGON, DONUT | built (g1); big (SPLINE fit, REVCLOUD, REGION) |
| Modify | Move, Copy, Rotate, Mirror, Scale, Erase, Explode | all | built |
| Modify | Trim, Extend, Offset, Fillet, Chamfer, Stretch, Array, Break, Join, Lengthen | none | big (C4, on hold) |
| Modify | Edit Polyline, Edit Spline, Edit Hatch, Draworder, Align, Overkill, Reverse | none | big (C4) except DRAWORDER, REVERSE cheap |
| Annotation | Text (Multiline, Single Line), Dimension (Linear, Aligned, Angular, Radius, Diameter), Leader | all | built |
| Annotation | Arc length, Jogged, Ordinate dimensions; Table; style boxes (text, dimension, multileader, table) | none | big (dimension kinds), cheap (a read-only style box each) |
| Layers | Layer Properties, layer box, Make Current, Match Layer | all | built |
| Layers | Off, Isolate, Freeze, Lock, Unisolate, Thaw all, Unlock, Layer Previous, Merge, Delete, Walk | the Layer manager does on, freeze, lock, delete; LAYOFF, LAYFRZ, LAYLCK, LAYON, LAYTHW, LAYULK, LAYISO (Off mode), LAYUNISO typed | built (g1), no buttons yet; big (LAYWALK, LAYMRG) |
| Layers | colour picker of the layer dialog | queued | queued |
| Block | Insert, Create, Edit, Edit Attributes | blocks drawn, attributes shown | big |
| Properties | Match Properties | MATCHPROP | built |
| Properties | Colour, Linetype, Lineweight boxes of new objects | shown "ByLayer", not live; tooltip says not built | cheap (CECOLOR, CELTYPE, CELWEIGHT boxes) |
| Properties | List | LIST | built (r1), in Utilities |
| Groups | Group, Ungroup, Group Edit | none | big |
| Utilities | Measure: Distance, Area | DIST, AREA (points or a closed object) | built (r1) |
| Utilities | Measure: Radius, Angle, Volume, Quick | MEASUREGEOM (MEA): Distance, Radius, Angle, ARea | built (g1); Volume, Quick out |
| Utilities | ID Point | ID | built (r1) |
| Utilities | Quick Select, Select All, Point Style | ALL at "Select objects:"; SELECTALL, Ctrl+A | built (g1); big (QSELECT) |
| Clipboard | Paste, Cut, Copy Clip, Copy with Base Point | none; the group is hidden (r1) | big (a clipboard format of our own and AutoCAD's) |
| View | Base view (from 3D) | none | out (Part B makes drawings from models) |

## Ribbon: Insert

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Block | Insert, Create, Edit, Edit Attributes, Block Editor | none | big |
| Block Definition | Create Block, Define Attributes, Manage Attributes | none | big |
| Reference | Attach (DWG, DWF, DGN, PDF, image), Clip, Adjust, Underlay layers, Frames | images and OLE frames read and drawn | big; xrefs out |
| Import | PDF Import | none | out |
| Point Cloud, Data, Linking and Extraction, Location, Content | | none | out |

The tab is left out: nothing in it is real yet.

## Ribbon: Annotate

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Text | Multiline Text, Single Line | MTEXT, TEXT | built; tab (r1) |
| Text | Check Spelling, Find Text, text style box, text height box | none | big (spelling), cheap (FIND read-only, style and height boxes) |
| Dimensions | Linear, Aligned, Angular, Radius, Diameter | all five | built; tab (r1) |
| Dimensions | DIM (one command for all), Arc Length, Jogged, Ordinate, Quick, Continue, Baseline, Dimspace, Dimbreak, Inspect, Jog line, style box, Update | none | big (DIM, QDIM, Continue, Baseline); cheap (style box) |
| Centerlines | Center Mark, Centerline | none | cheap (CENTERMARK as lines) |
| Leaders | Multileader | MLEADER | built; tab (r1) |
| Leaders | Add, Remove, Align, Collect leaders, style box | none | big |
| Tables | Table, Extract Data, Link Data | tables drawn | big |
| Markup | Revision Cloud, Wipeout | wipeouts drawn | big |
| Annotation Scaling | Scale list, Add Current Scale, Add/Delete Scales, Sync | no scale concept: the status bar's 1:1 is text | big; the group is left out |

## Ribbon: View

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Navigate (the navigation bar in 2021) | Zoom Extents, Window, Previous, In, Out; Pan; Regen | ZOOM E, W, P, 2X, 0.5X; PAN; REGEN | built; tab (r1). The buttons start ZOOM, ending a command running: AutoCAD's are transparent ('ZOOM) |
| Viewport Tools | UCS Icon, View Cube, Navigation Bar | the UCS icon on the canvas (UCSICON typed; no ribbon button) | UCS icon built (C7, see [75-ucs.md](75-ucs.md)); View Cube and Navigation Bar out (2D) |
| Views | Model and layout switch | the tabs under the canvas; Model and Layout buttons | built (r1); Layout goes to the first layout, AutoCAD to the last shown |
| Model Viewports | Viewport Configuration, Named, Join, Restore | none | big (tiled viewports) |
| Palettes | Properties, Layer Properties, Tool Palettes, Sheet Set Manager, Design Center, External References | Properties panel, Layer manager | built; Interface toggles (r1); the rest out |
| Interface | File Tabs, Layout Tabs, Switch Windows, Tile, Cascade, Toolbars | the DRAWINGS list and Properties shown or hidden | built (r1); file tabs as AutoCAD's cheap |
| Interface | the file tab's right-click menu (Close, Close All Drawings, Close All Other Drawings) | the DRAWINGS list: a click selects (Ctrl, Shift add), a double click or Enter opens, Delete does nothing; right click: Open, Close, Close Selected Drawings, Close All Other Drawings, Close All Drawings, one question for several drawings with changes | built (c56); New, Save, Save All, Copy Full File Path, Open File Location of AutoCAD's menu cheap |
| Drafting | (status bar in AutoCAD) | Grid, Snap, Ortho, Polar, Osnap toggles, the status bar's own state | built (r1) |

## Ribbon: Manage

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Action Recorder | Record, Play | none | out |
| Customization | User Interface, Tool Palettes, Import, Export | Global settings › Shortcuts (aliases and keys) | built in the Settings tab |
| Applications | Load Application (APPLOAD), Run Script, Visual LISP, VBA | PLUGINLOAD, the Plugin manager | built in the Plugins tab; SCRIPT (SCR) built (g1), typed; LISP and VBA out |
| CAD Standards | Configure, Check, Layer Translator | none | out |
| Cleanup | Purge, Overkill | none | big (PURGE needs the library to say what is unused) |
| Drawing utilities (application menu) | Audit, Recover, Units | UNITS and -UNITS (UN, -UN) as -UNITS's prompts | big (AUDIT); UNITS built (g1) on the command line, its dialog cheap |

The tab is left out: what it has that is real lives in Plugins and Settings already.

## Ribbon: Output

| Panel | AutoCAD 2021 | Presenter | Mark |
|---|---|---|---|
| Plot | Plot, Batch Plot, Preview, Page Setup Manager, Plotter Manager | none | big (PDF and Print on hold; the Drawing tab's buttons disabled, "not built yet") |
| Export to DWF/PDF | Export, Export Layout, Options | Part B's EXPORTDXF for a model drawing | big (PDF); DXF built in Part B |

The tab is left out: Plot and PDF are on hold.

## Quick access toolbar and application menu

| AutoCAD 2021 | Presenter | Mark |
|---|---|---|
| New | NEW: Drawing1.dxf, Drawing2.dxf, an empty drawing of R2013 in millimetres | built (r1) |
| Open, Save | Open (Ctrl+O), QSAVE (Ctrl+S) | built |
| Save As | SAVEAS: the window's dialog, or "Save drawing as <file>:" with no window | built (r1) |
| Undo, Redo | U, REDO | built |
| Plot | none | big (on hold) |
| Workspace switch, Web and Mobile | none | out |
| Command search (title bar) | a box that does nothing yet; its tooltip says so | cheap (filter the command names) |

## Status bar

| AutoCAD 2021 | Presenter | Mark |
|---|---|---|
| Model / layout tabs, + | the tabs; + runs LAYOUT New | built; + (r1) |
| Coordinates | X and Y of the cursor | built |
| Grid, Snap, Ortho, Polar, Osnap | the five toggles, F7, F9, F8, F10, F3, and typed (r1) | built |
| Osnap's arrow and right click: the snaps ticked, Object Snap Settings... | the fourteen snaps, the ten built ticked on and off at once and kept in the settings file, the four not built greyed "Not built yet." | built (c56) |
| Drafting Settings (DSETTINGS, DS, OS): Snap and Grid, Polar Tracking, Object Snap, 3D Object Snap, Dynamic Input, Quick Properties, Selection Cycling | Snap and Grid (snap on, X and Y spacing, grid on), Polar Tracking (on, increment angle), Object Snap (on, the fourteen snaps with their markers, Select All, Clear All; tracking greyed) | built (c56); grid spacing, polar spacing, additional angles cheap; the other tabs left out |
| Shift+right-click at a point prompt; END, MID, CEN, GCE, NOD, QUA, INT, PER, TAN, NEA, NON typed | the snap menu (Endpoint to Nearest of those built, None, Osnap Settings...) and the keywords typed, each for the next point alone; EXT, INS, APP, PAR typed say "not built yet" | built (c56); Temporary track point, From, Mid Between 2 Points, Point Filters big |
| AutoSnap markers | AutoCAD's shapes 9 pixels across, 1.5 wide, yellow on a dark canvas and dark on a light one, with the snap's name as the tip | built (c56) |
| Object snaps Endpoint, Midpoint, Center, Geometric Center, Node, Quadrant, Intersection, Perpendicular, Tangent, Nearest | all ten; a POINT is a node, not an endpoint | built; Node and Geometric Center (c56) |
| Object snaps Extension, Insertion, Apparent Intersection, Parallel | none | big (Extension and Parallel need acquired points and tracking lines; Insertion needs the insertion points of blocks and texts beside the display list; Apparent Intersection needs extended crossings) |
| Object snap tracking, Isometric drafting, Dynamic input (F12), Lineweight, Transparency, Selection cycling | none | queued (dynamic input); cheap (LWDISPLAY toggle, Global settings has it); big (tracking) |
| Annotation scale, Annotation visibility, Auto scale | "1:1" as text | big |
| Units, Quick Properties, Lock UI, Isolate Objects, Clean Screen, Customization | none | cheap (Clean Screen: hide both panels; Isolate as a display filter big) |

## Commands

What the command line knows, under AutoCAD's names and aliases:

- View: ZOOM, PAN, REGEN.
- Draw: LINE, PLINE, CIRCLE, ARC, RECTANG, ELLIPSE (r1), POINT (r1), TEXT, MTEXT, MLEADER, MVIEW, DIMLINEAR, DIMALIGNED,
  DIMRADIUS, DIMDIAMETER, DIMANGULAR.
- Modify: MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP.
- Layers and settings: LAYER, LAYMCUR, LAYMCH, PROPERTIES, PROPERTIESCLOSE (r1), OPTIONS, GRID, SNAP, ORTHO, POLAR,
  OSNAP typed (r1: "Enter mode [ON/OFF]", SNAP also a spacing), DSETTINGS (DS, OS) (c56).
- Inquiry (r1): DIST (DI), AREA (AA), ID, LIST (LI, LS).
- Files: NEW (r1), QSAVE, SAVEAS (r1), CLOSE, LAYOUT New and ? (r1, LO), Rename and Set (g1).
- Round g1: XLINE (XL), RAY, POLYGON (POL), DONUT (DO), SELECTALL (Ctrl+A), LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK,
  LAYISO, LAYUNISO, MEASUREGEOM (MEA), UNITS (UN), -UNITS (-UN), SCRIPT (SCR); ELLIPSE Arc; AREA Add area and Subtract area.
- Undo: UNDO, U, REDO.
- Plugins: PLUGINLOAD (NETLOAD), PLUGINUNLOAD, PLUGINRELOAD; Part B: DRAWINGNEW, DRAWINGUPDATE, DRAWINGSETTINGS,
  EXPORTDXF, VIEWSECTION, VIEWDETAIL, AUTODIM, AUTOMARK.

Missing, by the mark:

- **cheap**: DRAWORDER, REVERSE, CENTERMARK, COPYBASE without paste, DIST Multiple points, MODEL (TILEMODE), the drafting
  toggles' AutoCAD prompts in full (GRID's spacing); XLINE Offset and Ang Reference; the UNITS dialog; buttons for the g1
  layer commands. LAYOUT Delete needs the library to erase a layout (`DxfLayout` is not erased yet): big.
- **big**: TRIM, EXTEND, OFFSET, FILLET, CHAMFER, STRETCH, ARRAY, BREAK, JOIN, LENGTHEN, PEDIT, HATCH and HATCHEDIT (C4,
  on hold); COPYCLIP, CUTCLIP, PASTECLIP; BLOCK, INSERT, WBLOCK, ATTDEF, EATTEDIT, BEDIT; DIM, QDIM, DIMCONTINUE,
  DIMBASELINE, DIMSTYLE, STYLE, MLEADERSTYLE; TABLE; GROUP; QSELECT; PURGE, AUDIT; PLOT, EXPORTPDF; SPLINE; REVCLOUD;
  WIPEOUT made; tiled viewports (VPORTS); FIND and SPELL.
- **out**: 3D (VIEWCUBE, ORBIT, solids, render), xrefs, PDF import, sheet sets, action recorder, LISP and VBA.

## Found by the user, queued for the next agent

- The canvas: speed on big drawings, the look of the selection, box selection and the grid (CadCanvas.cs,
  SkiaCanvasRenderer.cs).
- The layer colour picker of the LAYER dialog.
- F12 and dynamic input.

## Other controls found dead or saying "later"

| Control | Before | After ribbon1 |
|---|---|---|
| Annotate and View tabs | disabled | built |
| Quick access New | disabled, "comes in a later step" | NEW |
| + after the layout tabs | disabled | LAYOUT New |
| Start screen "Blank drawing" | disabled, no command | NEW |
| Home › Clipboard › Paste | enabled look, no command | group hidden until PASTECLIP is built |
| Home › Properties: the three ByLayer boxes | tooltip "comes in a later step" | tooltip "not built yet" |
| Title bar command search | tooltip "comes in a later step", does nothing | tooltip "not built yet: type a command in the command line" |
| Settings tab buttons without a tooltip of their own | showed the style's "comes in a later step" | each names its Global settings page |
| Drawing tab › Export › PDF and Print | disabled, "comes in a later step", tooltip not shown while disabled | disabled, "not built yet", shown while disabled |
| Export dialog chips "PDF · later", "Print · later"; Drawing settings "Save as preset…" | disabled, "later" | left as they are (Part B, on hold) |
| Global settings › Appearance "Dark (Light comes later)" | text | left as it is |
