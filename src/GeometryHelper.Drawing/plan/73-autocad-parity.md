# AutoCAD 2021 parity of the presenter (Part A)

A read-only check, made on 2026-11-10 at `main` bd614aac, of `GeometryHelper.Dxf.Viewer`, `.Dxf.Viewer.Wpf` and
`GeometryHelper.Dxf.App` against AutoCAD 2021's 2D drafting and annotation set. It replaces the list of
[72-autocad-gap.md](72-autocad-gap.md), most of whose big items are built since. The work it leads to is kept in
`PROGRESS.md` at the root of the repository; this page is the full list it is taken from.

What was read: the registrations in `CadCommands*.cs` (149 commands), `CadShortcuts.Default` (109 aliases, 21 keys), the
window's own commands, the prompts of every command, the `*Window.xaml` dialogs, the presenter's guides and `PROGRESS.md`.
Left out: what is built and complete, what `PROGRESS.md` already lists, and DWG, which is out of scope by decision.

Priority is how often a 2D drafter uses it: **High** daily or on most drawings, **Medium** weekly or on some drawings,
**Low** rarely. Size: **S** under a day or about 150 lines, **M** a few days or a new dialog, **L** a new subsystem.

## Summary

101 gaps (a whole command, option set, variable, dialog or behaviour missing) and 19 partial items (built, options
missing).

| Area | High | Medium | Low | Total |
|---|---:|---:|---:|---:|
| Draw | 1 | 4 | 3 | 8 |
| Modify | 1 | 2 | 5 | 8 |
| Text and annotation scaling | 0 | 5 | 1 | 6 |
| Dimensions and leaders | 2 | 3 | 3 | 8 |
| Blocks, attributes, xrefs | 2 | 4 | 2 | 8 |
| Layers and properties | 1 | 7 | 1 | 9 |
| Utilities and command line | 2 | 2 | 5 | 9 |
| View, layouts, viewports, plot | 2 | 6 | 6 | 14 |
| Files | 1 | 2 | 1 | 4 |
| Drafting aids and system variables | 2 | 3 | 5 | 10 |
| Selection | 1 | 1 | 1 | 3 |
| Grips and window behaviours | 2 | 4 | 2 | 8 |
| Dialogs and palettes | 0 | 1 | 5 | 6 |
| **Gaps** | **17** | **44** | **40** | **101** |
| Partial items | 7 | 6 | 6 | 19 |

Out of scope, not counted: 3D, rendering, cloud, BIM, sheet sets, parametric constraints, Action Recorder, LISP and VBA,
CAD Standards, PDF import and underlays, Express Tools, DWG.

## Draw

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| REVCLOUD (Arc length, Object, Rectangular, Polygonal, Freehand, Style) | whole command | High: every revised issue clouds its changes | M |
| SPLINE (Fit, CV, Knots, Close) | whole command (splines are read and drawn) | Medium | L |
| WIPEOUT (create, Frames, Polyline) | making one (wipeouts are drawn) | Medium | M |
| DIVIDE, MEASURE | whole commands | Medium | S |
| CENTERMARK, CENTERLINE | whole commands | Medium | S |
| REGION | whole command | Low | M |
| MLINE, MLSTYLE | whole commands | Low | M |
| SKETCH, SOLID (2D fill), TRACE, BLEND | whole commands | Low | S each |

## Modify

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| PEDIT (Close/Open, Join, Width, Edit vertex, Fit, Spline, Decurve, Ltype gen, Reverse, Multiple; lines and arcs made a polyline) | whole command | High | L |
| OVERKILL | whole command | Medium | M |
| ARRAYEDIT and the Array contextual tab | whole command | Medium | M |
| REVERSE, SETBYLAYER, CHPROP, CHANGE, OOPS | whole commands | Low | S |
| SPLINEDIT | whole command (after SPLINE) | Low | M |

## Text and annotation scaling

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| SCALETEXT, JUSTIFYTEXT | whole commands | Medium | S |
| TXT2MTXT | whole command | Medium | M |
| SPELL | whole command | Medium | M |
| FIELD, UPDATEFIELD | fields | Medium | L |
| Annotative objects: CANNOSCALE set, the status-bar scale list, ANNOAUTOSCALE, OBJECTSCALE, SCALELISTEDIT | the whole scheme | Medium | L |
| TEXTALIGN | whole command | Low | S |

## Dimensions and leaders

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| Associative dimensions (DIMASSOC 2), DIMREASSOCIATE, DIMDISASSOCIATE | a dimension following its geometry | High | L |
| MLEADERSTYLE (Multileader Style Manager) | whole dialog | High | M |
| QLEADER, LEADER | whole commands | Medium | M |
| DIMARC | whole command | Medium | S |
| DIMUPDATE (-DIMSTYLE Apply), DIMOVERRIDE | whole commands | Medium | S |
| DIMJOGGED | whole command | Low | S |
| TOLERANCE | whole command | Low (Medium in mechanical work) | M |
| MLEADEREDIT, MLEADERALIGN, MLEADERCOLLECT | whole commands | Low | M |

## Blocks, attributes, xrefs

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| BEDIT, BCLOSE, BSAVE (Block Editor) | whole editor | High | L |
| Dynamic blocks in use: custom grips (visibility, lookup, stretch, flip, rotate, array) and their rows in Properties | the behaviour | High | L |
| REFEDIT | whole command | Medium | L |
| XCLIP | whole command | Medium | M |
| XBIND, -XREF Bind | binding | Medium | M |
| ATTDISP | whole command | Low | S |
| Dynamic block authoring | parameters and actions | Low | L |

## Layers and properties

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| VP Freeze per viewport: VPLAYER, the VP columns of the Layer manager, LAYVPI | the per-viewport scheme | High | M |
| Layer States Manager (LAYERSTATE, -LAYER State) | dialog and option | Medium | M |
| -LAYER typed while the window is open, with Make, Color, Ltype, LWeight, TRansparency, Plot, Description, Rename | the typed command | Medium | S |
| LAYMRG, LAYDEL, LAYERP | whole commands | Medium | S |
| Layer manager: Plot, Transparency, Description, Plot style columns; filters and search | columns and filters | Medium | M |
| RENAME, -RENAME | command and dialog | Medium | S |
| A live lineweight box, LWEIGHT, CELWEIGHT, COLOR | the box and the commands | Medium | S |
| LAYWALK, LAYCUR, COPYTOLAYER | whole commands | Low | S |

## Utilities and command line

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| SETVAR, and a system variable's name typed as a command | the mechanism | High | M |
| Command line AutoComplete | suggestions as you type, synonyms, mid-string search | High | M |
| Transparent commands ('ZOOM, 'PAN, 'DIST, 'CAL, 'SETVAR, 'LAYER) | the behaviour | Medium | M |
| SELECT | whole command | Medium | S |
| CAL, QUICKCALC, MEASUREGEOM Quick, FILTER | whole commands or the option | Low | M |
| TIME, STATUS, DWGPROPS, MULTIPLE | whole commands | Low | S |

## View, layouts, viewports, plot

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| MSPACE, PSPACE through a viewport (double click, the MODEL/PAPER button), ZOOM nXP | the behaviour | High | L |
| Viewport scale in Properties and the status bar | setting the scale | High | M |
| Viewport Display locked | the property | Medium | S |
| MVIEW Polygonal, Object, 2/3/4, Restore, Lock, ON/OFF; VPCLIP | the options | Medium | M |
| VIEW, -VIEW (View Manager) | command and dialog | Medium | M |
| LAYOUT Delete, Copy, Template, SAveas; the layout tab's menu | options and menu | Medium | M |
| PUBLISH (several layouts or drawings to one PDF) | whole command | Medium | M |
| ISOLATEOBJECTS, HIDEOBJECTS, UNISOLATEOBJECTS | whole commands | Medium | M |
| VPMAX, VPMIN, CHSPACE, MODEL, TILEMODE, REDRAW, CLEANSCREENON/OFF | whole commands | Low | S |
| VPORTS | tiled model viewports | Low | L |

## Files

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| Recent files (application menu, start screen, pinned) | the list | High | S |
| OPEN, SAVE, SAVEALL, CLOSEALL, QUIT typed | the typed commands | Medium | S |
| Drawing Recovery Manager offering autosave copies after a crash | the offer | Medium | S |
| A DXF dropped onto an open drawing inserted as a block | the behaviour | Low | S |

## Drafting aids and system variables

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| FROM, M2P/MTP, TT, point filters .X .Y .XY | the snap modifiers | High | S |
| MIRRTEXT 0 keeping mirrored text readable | check first: nothing reads MIRRTEXT | High if text comes out backwards | S |
| POLARADDANG, POLARDIST PolarSnap, angles from the last segment | the settings | Medium | S |
| LWDISPLAY on the status bar | the toggle | Medium | S |
| PSLTSCALE, MSLTSCALE | not read | Medium | S |
| Isometric drafting (ISODRAFT, SNAPSTYL, F5) | the aid | Low | M |
| TPY, the Dynamic Input, Quick Properties and Selection Cycling tabs of Drafting Settings, PICKFIRST, PICKADD, PICKAUTO, PICKDRAG, FILEDIA, CMDDIA, EXPERT | toggles, tabs, variables | Low | S |

## Selection

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| "Select objects:" Previous, Last, Fence, WPolygon, CPolygon, Remove, Add, Undo, SIngle, Group, BOX, AUto; Shift+click removal | all but click, window, crossing and ALL | High | M |
| SELECTSIMILAR | whole command | Medium | S |
| ADDSELECTED | whole command | Low | S |

## Grips and window behaviours

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| Grip modes: Space cycles STRETCH, MOVE, ROTATE, SCALE, MIRROR with Base point, Copy, Undo; Ctrl copies; several hot grips | the modes | High | M |
| Dimension grips: text, dimension line, extension origins | dimensions have none | High | M |
| Multifunctional grip menus (polyline vertices and segments, line Lengthen, arc Radius) | the menus | Medium | M |
| Grips of hatches, MTEXT width, ellipses, xlines, multileaders, attributes | these grips | Medium | M |
| Canvas right-click menus (Default, Edit, Command) | the menus | Medium | M |
| Double-click: polyline PEDIT, hatch HATCHEDIT, block BEDIT, viewport MSPACE, leader and dimension text | these actions | Medium | S |
| Quick Properties, rollover tooltips | palette and behaviour | Low | M |

## Dialogs and palettes

| AutoCAD | Missing | Priority | Size |
|---|---|---|---|
| Hatch Creation contextual tab with live preview | the tab | Medium | M |
| Drawing Units dialog | the dialog | Low | S |
| Tool Palettes, DesignCenter, Plot Style Table Editor, Data Extraction | palettes and dialogs | Low | M to L |

## Partial items: built, options missing

| Area | AutoCAD | Missing | Priority | Size |
|---|---|---|---|---|
| Draw | ARC | Center, End, Angle, Direction, Radius, Length; Enter continues tangent | High | M |
| Draw | CIRCLE | Ttr; Tan, Tan, Tan | High | S |
| Text | TEXT | Justify and Style; Enter starts the next line below (check) | High | S |
| Text | MTEXT | Justify, Line spacing, Rotation, Style, Width, Columns at the corner prompt | High | S |
| Draw | PLINE | Halfwidth, Length, Width; arc mode's Angle, CEnter, Direction, Radius, Second pt | High | M |
| Properties | MATCHPROP | Settings dialog and special properties | High | M |
| Properties | PROPERTIES | editable rows for polylines, ellipses, hatches, dimensions, MTEXT, INSERTs, viewports, multileaders | High | L |
| Draw | RECTANG | Chamfer, Fillet, Width, Area, Dimensions, Rotation | Medium | S |
| Modify | COPY | Array, mOde | Medium | S |
| Dimensions | DIMLINEAR and the others | Mtext, Angle, Rotated; Quadrant | Medium | S |
| View | ZOOM | Center, Dynamic, Object, Scale, In, Out | Medium | S |
| Draw | HATCH | preview while picking, -HATCH, gap tolerance, Inherit, Separate; associative update | Medium | M |
| Annotation | MLEADER | Options; landing-first and content-first | Medium | S |
| Modify, Draw, Layers, Inquiry, References | OFFSET Multiple, XLINE Offset and Ang Reference, LAYISO lock and fade, dynamic input in grip edits, DIST Multiple, IMAGEADJUST | the options | Low | S |
