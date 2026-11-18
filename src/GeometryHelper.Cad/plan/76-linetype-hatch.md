# C9: linetypes and hatches as AutoCAD makes them

Research for C9 of [04-phases.md](04-phases.md), done before the code. What AutoCAD does, what the DXF holds, what ezdxf
does, and what the presenter takes. No Autodesk file is shipped or copied: acad.lin, acadiso.lin, acadltiso.lin, acad.pat
and acadiso.pat are Autodesk's and are read from the user's own AutoCAD at run time, never carried.

## Linetypes

**LINETYPE (LT, LTYPE)** opens the Linetype Manager: a filter, the list of the drawing's linetypes (name, appearance drawn
as a line, description), the current linetype written above it, and the buttons Load..., Delete, Current and Show
details. Details: name, description, global scale factor ($LTSCALE), current object scale ($CELTSCALE), ISO pen width
(which sets the current object scale to the width for an ISO linetype, these being drawn for a pen of 1 mm), "Use paper
space units for scaling" ($PSLTSCALE). Load... opens "Load or Reload Linetypes": the file (acadiso.lin in a metric drawing,
acad.lin in an imperial one, File... to choose another) and its linetypes with their descriptions, several chosen at
once. Delete refuses a linetype in use, ByLayer, ByBlock and Continuous. OK applies.

**-LINETYPE** asks "Current line type: "ByLayer"" then "Enter an option [?/Create/Load/Set]:". ? lists a file's
linetypes; Create asks the name, the file to create or append to, "Descriptive text:" and "Enter linetype pattern (on next
line):" after a printed "A,", appends the definition to the file and says "New linetype definition saved to file." (it does
not load it); Load asks the names (wildcards) and the file; Set asks "Specify linetype name or [?] <ByLayer>:" and refuses a
linetype not loaded. LTSCALE (LTS) and CELTSCALE are variables: "Enter new linetype scale factor <1.0000>:" then
"Regenerating model.".

**The .lin file.** `*NAME,description` (a description up to 47 characters, optional), the pattern on the next line:
`A,` (the one alignment, a pattern starts and ends on a dash) then the lengths, above nought a dash, below a gap, nought a
dot. A complex element follows a length in brackets: a text `["GAS",STANDARD,S=2.54,R=0.0,X=-2.54,Y=-1.27]` (the text
in quotes, then the text style) or a shape `[BAT,ltypeshp.shx,X=-2.54,S=2.54]` (a shape name, then its .shx). S scales,
X and Y offset from the end of the length before, R turns it from the line, A from the x axis, U keeps it upright (turned
from the line but never upside down); an angle in degrees, `r` after it for radians, `g` for grads. `;` starts a comment.

**LTYPE in the DXF** (AutoCAD's order): `2` name, `70` flags, `3` description, `72` 65 (the A), `73` the count of
elements, `40` the total length, then per element `49` the length and `74` its flags (1 rotation absolute, 2 text, 4
shape), and for a complex one `75` the shape number (0 for a text), `340` the handle of the STYLE (a text style, or the
style AutoCAD adds for the .shx with no name and 70 of 1), `46` scale, `50` rotation in degrees, `44` and `45` the offset,
`9` the text. Upright (U) has no flag of its own in the DXF reference; the library writes it as R, as its own loader does.

**ezdxf** (`entities/ltype.py`, `tools/standards.py`, MIT; ideas only): the standard linetypes are ANSI values in inches
times 2.54 for ISO drawings (`ISO_LTYPE_FACTOR`); a complex pattern is compiled to tags by `setup_complex_pattern`,
dashes of the same sign merged when it simplifies one for drawing.

**Taken.** The library already reads a .lin as AutoCAD does (`DxfDatabase.LoadLineTypeFile`, text and shapes, wildcards,
a shape found by name in the .shx beside the file) and writes LTYPE records (`DxfLinetypeTableRecord`). The presenter adds
its own reader of the file into data (`CadLinetypeFile`), for the Load dialog and -LINETYPE ?, Create and Load, a small set
of its own linetypes in millimetres (`GeometryHelper.lin`, not a file: DASHED, HIDDEN, CENTER, PHANTOM, DOT, DASHDOT,
DIVIDE, BORDER and GAS_LINE, the lengths of ISO 128 for a pen of 0.5 mm, our own numbers), and loads a file's linetype
through the library's loader, so that a file of the user's is read as AutoCAD reads it.

## Hatches

**HATCH (H, BHATCH)** asks "Pick internal point or [Select objects/Undo/seTtings]:"; each point finds the smallest closed
area round it among the objects shown ("Analyzing the selected data...", "Analyzing internal islands..."), or says "Valid
hatch boundary not found."; Enter makes one HATCH of every area picked. Select objects takes closed objects (closed
polylines, circles, ellipses, closed splines) as loops. The Hatch Creation contextual tab: Boundaries (Pick Points, Select,
Remove, Recreate), Pattern (the list with previews), Properties (type Solid / Gradient / Pattern / User defined, colours,
transparency, angle, scale or spacing, double, ISO pen width), Origin (set, corners, centre, store as default), Options
(Associative, Annotative, Match Properties, gap tolerance, island detection Normal / Outer / Ignore / none, draw order).
**HATCHEDIT (HE)** opens the same settings on a hatch picked; **BOUNDARY (BO)** makes closed polylines of the area round a
point, islands too.

**The .pat file.** `*NAME,description`, then a line per family: `angle, x-origin, y-origin, delta-x, delta-y [, dash...]`,
the deltas along the line and across it, in the line's own frame; dashes as in a linetype, six at most. AutoCAD looks for
a predefined pattern in acadiso.pat (metric) or acad.pat, a custom one in a file of its name on the support path.

**HATCH in the DXF.** `10 20 30` elevation point, `210..230` normal, `2` name, `70` solid fill, `71` associative, `91`
the count of loops, each `92` its flags (1 external, 2 polyline, 4 derived, 8 textbox, 16 outermost), then a polyline
(`72` has bulge, `73` closed, `93` vertices, `10 20 42`) or edges (`93` edges, `72` type 1 line 2 arc 3 ellipse 4 spline),
then `97` the count of source objects and their `330` handles; `75` style (0 normal, 1 outer, 2 ignore), `76` pattern type
(0 user, 1 predefined, 2 custom), `52` angle, `41` scale or spacing, `77` double, `78` the count of pattern lines, each `53`
angle, `43 44` base point, `45 46` offset (turned into the plane, not along and across as the file has it), `79` dashes
and `49` each; `47` pixel size, `98` seed points; from R2004 the gradient `450` 1, `451`, `452` one colour, `453` colours,
`460` angle, `461` shift (0 centred off, 0.25 on in the dialog), `462` tint, `463` and `63`/`421` per colour, `470` the
name (LINEAR, CYLINDER, INVCYLINDER, SPHERICAL, INVSPHERICAL, HEMISPHERICAL, INVHEMISPHERICAL, CURVED, INVCURVED; the
dialog's GR_LINEAR...). An associative hatch is also among the reactors (`102 {ACAD_REACTORS`) of each source object.

**ezdxf** (`entities/hatch.py`, `tools/pattern.py`): a .pat is compiled by turning each line's offset by its angle into the
DXF's frame (`PatternFileCompiler`); `scale_pattern` scales and turns base point and offset; ezdxf does not make hatches
associative. LibreCAD keeps its patterns as small DXF files of lines, not .pat; not taken.

**Taken.** The library writes a hatch of AutoCAD's file as AutoCAD 2021 does (`PatternSearchPath`: the lines of
acadiso.pat at a scale and angle to 1E-13), user-defined lines, solid and gradients, the area by island style. Two
accessors were added: `DxfHatch.SetPatternDefinitions` for a pattern no file of the drawing holds (our own), and
`DxfHatch.SetAssociatedObjects` for the handles and reactors of an associative hatch. The presenter carries a small set of
its own patterns in millimetres (`GeometryHelper.pat`, not a file), written from the geometry: ANSI31 (45 degrees, 3.175
apart), ANSI32, ANSI33, ANSI34, ANSI37, ANGLE, BRICK, CROSS, DASH, DOTS, EARTH, HONEY, LINE, NET, SQUARE, STEEL and ZIGZAG;
a pattern found in the user's acadiso.pat is taken from there first, as AutoCAD would. The area round a point is found by
the presenter's own planar arrangement of the lines, arcs, circles, polylines and ellipses shown (the library has no
public region finder: `PlanarMap` carries shapes between plane and space, and the winding-region code is internal).

## Checked in AutoCAD 2021

A drawing made by the presenter's commands (the nine linetypes of its own set, FENCELINE1, HOT_WATER_SUPPLY and ZIGZAG of
the user's acadiso.lin through the library's loader, one made by -LINETYPE Create; seven hatches: ANSI31 of acadiso.pat
round two nested islands, HONEY of the own set at scale 2, 15 degrees and an origin, lines of the user's doubled, solid,
GR_CYLIN, BRICK in half a circle, ANSI37 of a circle selected; a BOUNDARY polyline; a HATCHEDIT) opens in accoreconsole and
AUDIT finds 0 errors. A loop flagged Derived (4, as AutoCAD flags the loops of points picked) makes AutoCAD refuse the
file, "expected group code 47": it then wants the pixel size, which the library does not write. Associative hatches with
their handles and reactors are taken without it. The presenter flags the outer loop External and the islands with none.

## Left out

Hatch preview while picking, gap tolerance, text boxes as islands, annotative hatches, transparency, separate hatches, draw
order, Recreate boundary, re-making an associative hatch when its objects move (the association is kept and written, and
said), the U flag of a complex linetype as its own (written as R), Reload of a linetype loaded (the library cannot change
how many dashes a record of the file has), shapes in the presenter's own linetypes (a shape is loaded only from a file,
through the library).
