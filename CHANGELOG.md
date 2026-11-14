# Changelog

The release notes of the GeometryHelper package in full, newest first. The package carries the notes of its own
release only, and a link here for the rest. GeometryHelper.IfcConvert,
GeometryHelper.TeklaConvert and GeometryHelper.CadConvert carry their own notes in their packages.

## Unreleased

**FIXED.** A block cell of a table was drawn at its insertion point and its row left as it stood. As AutoCAD 2021 lays it out
(`GenerateLayout` and `RecomputeTableBlock` in its console, margins 1.5): a block not fitted to its cell asks the box its extents
fill (an attribute definition left out), at the cell's scale and turn, and the margins: a block 10 by 40 at scale 1 makes its row
43, at scale 2 83, over two merged rows of 8 it makes 21.5 each (41.5 at scale 2), shared as the words of a merged block are; one
wider than its column widens the column first (100 wide in a column 30 wide makes 103, a block turned 45 degrees 38.35534 in the
row and the column), and a block fitted to its cell (`SetAutoScale`) asks nothing. `CadTableEditor` (the presenter's editing of
cells, its Redraw, a typed cell and the least a row's line is dragged to) lays the rows and columns out so, and
`DxfTable.RecomputeTableBlock` draws the INSERT of a block cell by its extents: they are aligned in the cell by its alignment,
in from its sides by the margins (the block above, at the middle of a cell 30 wide and 8 high at x 40 and y -18, stands with its
insertion point at (50, -42), where it stood at (55, -22) off the cell), and a fitted block takes the scale that fits the box its
turned extents fill into the cell less its margins, min((W - 2 margins) / width, (H - 2 margins) / height): 0.125 for that block, 1.425
in a cell 40 wide and 60 high, 0.925 turned 90 degrees, 1.29099 turned 30 (some 80 blocks placed and fitted in the 2 by 2 tables of AutoCAD's console). A block turned by exactly
90, 180 or 270 degrees stands where AutoCAD puts it, off its aligned box by the block's own width and height. The scale in the
cell (144) is not changed.
**FIXED.** A line set on the top left cell of a block of merged cells of a table made in code (`SetGridColor`,
`SetGridLineWeight`, `SetGridVisibility`) is given to the cells along the block's edges, as AutoCAD 2021 reads it from a DXF
file: the title merged across 4 columns with a top of lineweight 50 has it on each of its four cells, a block of 2 rows and 2
columns its top over its first row and its left down its first column (its right and bottom are the lines inside it, read as the
left and top of the next cell), a block of one row its bottom along it and one of one column its right down it; a line another
cell holds of its own is kept, whatever the order they were set in (8 tables of 4 rows and columns, every cell's four lines, equal
to AutoCAD's reading of the same files).
**CHANGED.** A row is laid out by the one line, four thirds of the text height, of its first cell alone, and the other cells ask for
their words: a data row of a table whose data is 8 high holding a first cell given 6 is 11 high (6 times 4/3 and two margins of
1.5), where the empty cells beside it at 8 made it 13.66667; a second cell of 12 holding one word makes it 15, its word and the
margins, where it made 19; an empty cell beside the first adds nothing (32 rows of AutoCAD 2021's console). The presenter laid out
every cell by one line at least.

**CHANGED.** REFEDIT, REFSET and REFCLOSE use the words of AutoCAD 2021's own string table (AcRefEdRes.dll, read as data: its
console has none of the three commands, by script or by standard input, where `Unknown command` is all it says), which the
documentation they were first built from did not give. REFEDIT asks `Select nesting level [Ok/Next] <Next>:` (Enter steps to
the next level, it was `Enter nested reference choice [Ok/Next] <Ok>:`) and `Enter object selection method [All/Nested] <All>:`
(it was `[Automatic/Prompt/Settings] <Automatic>`; the Settings answers are gone: a block with attribute definitions asks
`Display attribute definitions [Yes/No] <No>:` and the lock is `CadDocument.RefEditLocksObjects` alone). It opens saying `2
Added to working set`, says `Object is not a valid reference`, `** At least one object must be selected to edit a reference
**`, `** Cannot REFEDIT anonymous block **` (`dimension or table blocks` for `*D` and `*T`) and refuses a reference
whose scale factors differ in size as AutoCAD does, `** Cannot REFEDIT a non-uniformly scaled block **` (it took one and left
its circle out). REFSET says `Transfer objects between the RefEdit working set and host drawing...`, `3 Added to working set`
or `1 Removed from working set` and `** 2 selected objects are already in the working set **` (`are not in` for Remove);
REFCLOSE asks `Enter option [Save/Discard reference changes] <Save>:` and says `1 object added to BOX`, `1 object removed
from BOX` and `2 block instances updated`. The refusals of BEDIT, a second REFEDIT, QSAVE and SAVEAS, and of REFSET and
REFCLOSE with no edit open, are AutoCAD's lines (`** BEDIT command not allowed during reference editing **` and the rest).
`CadRefEdit.NotAllowed` is `CadRefEdit.NotAllowedFor(command)`, a command's own line, and `CadRefEdit.NotEditing` is AutoCAD's
`** Command not allowed unless a reference is checked out with REFEDIT command **`. Not known: the order of these lines, and
whether the handles of the objects saved back are kept (the presenter writes each as a new object); the console cannot run a long
transaction, which REFEDIT is made on.
**FIXED.** VPMAX, VPMIN and `-VPORTS Toggle` on the Model tab with one tile and none kept did nothing: AutoCAD 2021's console makes
four equal tiles (numbered 2 to 5, the current one first) and keeps them as `*Multiple`, saying `Regenerating model.` four
times; the tile left alone by Toggle or `-VPORTS SIngle` is numbered 3 where the current one was 2 and 2 where it was any other
(CVPORT 3 after four tiles and SIngle, it kept 2), and the tiles brought back by Toggle give the first the number of the tile
that was alone and the others the free ones in order. Probed through accoreconsole fed by standard input.
**NEW.** A table style saved from R2007 or later as R2000 or R2004 keeps the formats of its rows beside it as AutoCAD 2021 does:
the type of the value, its unit and its format string of the data, title and header rows (the groups 90, 91 and 1 after the fill of
each row, which a style of R2004 and before has no place for) go into the XRECORD of the style's extension dictionary
(`ACAD_XREC_ROUNDTRIP`, named after the dictionary's cell style map, made where the dictionary names none) as
`ACAD_ROUNDTRIP_PRE2007_TABLESTYLE`, the three in one run (90, 91, 1, then 92, 93, 2, then 94, 95, 3): `90=512 91=0 1= 92=512 93=0 2=
94=512 95=0 3=` for AutoCAD's Standard, `90=2 91=1 1=%lu2%pr2 92=512 93=0 2= 94=1 95=2 3=%lu1%pr0` for a style of a real of two places
for its data (a distance) and an angle for its header. The styles of AutoCAD's table drawing (two tables, so two styles) and of 8 more
styles of other types, units and formats, saved as R2000, R2004 and R2007, are AutoCAD's group for group, and AutoCAD's `AUDIT` finds
in our files the errors it finds in its own (none in a plain table's R2000 and R2004, 3 in both of AutoCAD's table drawing).
**FIXED.** A table style going up from R2000 or R2004 to R2007 or later lost the formats of its rows and kept the round trip of them:
it now has the 90, 91 and 1 after the fill of each row from that round trip (any type, no unit and no format where there is none, as
AutoCAD's own R2007 file), the XRECORD is taken out with its name in the dictionary, and a style going to R2010 or later has the 280 of
nought AutoCAD writes first in it (a style and a table going to R2010 had none; a table that has a 280 of its own after its flags, which
AutoCAD writes in R2000 too, was taken for one that had it).
**FIXED.** REVCLOUD in the presenter is now AutoCAD 2021's, checked in its console run interactively (14 November: the console
reads its commands from standard input, and then runs the 2021 REVCLOUD with Rectangular, Polygonal, Freehand and Modify; a
script file, which the earlier checks used, runs the older one). The first prompts are `Specify first corner point`
(Rectangular), `Specify start point` (Polygonal) and `Specify first point` (Freehand), where Polygonal and Freehand were swapped;
the `Minimum arc length ...` line is said when the command starts and when the type is changed, not after Arc length or Style.
Rectangular runs from the first corner along the side of its X to the Y of the second, where it ran along the Y first: the
corners (0, 0) and (100, 60) at arcs of 10 give 32 vertices up the left side, along the top and down the right, clockwise,
bulge -0.52056705, and (0, 60) and (100, 0) down the left side, counter-clockwise. Rectangular and Polygonal end with no
message, as AutoCAD's do; Object and Freehand say `Revision cloud finished.` Every cloud carries the data `RevcloudProps` (a
1070 of 0, a 1040 of its approximate arc length) as AutoCAD's do. While REVCLOUDAPPROXARCLEN is nought the first cloud takes a
twentieth of the diagonal of the view in the drawing's units, not 15 or 0.5: a view 100 high, 942 by 534 pixels, gives 10.1389,
AutoCAD's 10.1277 (a tenth of a per cent less, for the pixels its window leaves out), whatever DIMSCALE and the units are;
where there is no view, as in a test, 15 and 0.5 as before. AutoCAD measures a viewport that is not the whole window by a rule
that was only half read (the height counted is twice the smaller of the distances of the viewport's top and bottom from the
window's middle line), so a layout's viewport worked in does not give its length. REVCLOUDARCVARIANCE is a switch, `Enter new
value for REVCLOUDARCVARIANCE <ON>:`, as AutoCAD's. A cloud made before this carries no `RevcloudProps` and is no cloud to
REVCLOUDPROPERTIES.

**FIXED.** REVCLOUD's Modify in the presenter follows AutoCAD 2021's console. After `Pick a side to erase:` it asks `Reverse
direction [Yes/No] <No>:` and says nothing at the end; Enter at a next point ends it with nothing changed; the new arcs are laid
from the start of the path, an arc length apart, the last one shorter, and the cloud begins at the end of the path: for the
corners (0, 0) and (100, 60) at arcs of 10 and the path (50, 0), (50, -23), (80, -23), (80, 0) with the bottom between 50 and 80
picked, the 37 vertices are (80, 0), (80, -6), (80, -16), (77, -23), (67, -23), (57, -23), (50, -20), (50, -10), (50, 0), (40,
0) and on round the old cloud to (90, 0). The arcs cut at the ends of the path keep the shape they had (picked at (45, 0): the
path starts on the arc at (45, -2.6028), bulges -0.24469844 and -0.11067678), and every arc bulges the way the old cloud's do,
the other way for Yes, where the presenter chose outward by the area and began at the cut, and laid the new arcs from the end
of the path when the side kept ran the other way.

**NEW.** REVCLOUDPROPERTIES in the presenter, as AutoCAD 2021's: `Select objects:` (only revision clouds count), `Select property
to change [Arc length]:`, `Specify approximate length of arc <10.0000>:`, the length the first cloud was drawn with. Each cloud
is drawn again in place from its first vertex along the polygon of its own vertices, with arcs of the new length, and
REVCLOUDAPPROXARCLEN is set to it: a rectangle of arcs 10 long given 20 is 16 vertices, the cloud REVCLOUD draws with 20, and the
polygon (0, 0), (73, 0), (80, 41), (20, 52) at 10 given 15 is 15 vertices whose sixth is (72.5898, 4.2770), AutoCAD's, along the
chords of its arcs, which cut the corners.

**NEW.** PEDIT in the presenter makes a polyline of a spline, alone and in Multiple, as AutoCAD 2021's console does: `Specify a
precision <10>:` (`Specify a precision for spline conversion <10>:` in Multiple, once), 0 to 99, the last one used the default,
lines within the precision, the knots of a spline of fit points among the vertices: the spline through (0, 0), (100, 50), (200,
0) and (300, 80) at 2 is the 8 vertices of AutoCAD's, to 1E-6, at 0 its 4 fit points. A precision that gives more than 10 000
vertices asks `Using a precision value greater than 81 may cause performance problems.  Do you want to continue? [Yes/No]: <N>:`,
81 the largest that gives no more (a cubic of seven control points: 9 947 vertices at 81, 10 027 at 82), and No asks again with
it as the default. PEDITACCEPT 1 leaves out the question of turning it into one, not the precision; PLINETYPE 0 makes an
old-style POLYLINE of it. The counts of vertices are `DxfSpline.ToPolylineWithPrecision`'s, AutoCAD's exactly for every spline
of control points probed (48 counts of four splines of degree 2 to 5) and for 96 of 119 counts of seven cubics through fit
points; in the other 23 the presenter makes 1 to 24 vertices more than AutoCAD (up to 2.4 per cent, at precision 8 and over),
and the rule that gives AutoCAD's is not found: the precision rule of its fit splines is a halving tolerance 1 to 4 per cent
looser than the library's, which no function of the spline's bend, length or span fitted. Multiple left splines as they were
before.

**FIXED.** JOIN of objects picked at once in the presenter makes a LWPOLYLINE at every PLINETYPE, as AutoCAD 2021's console run
interactively does (four lines, a line and an arc, a POLYLINE and a line, two POLYLINEs, a POLYLINE, an arc and a line; `2
objects converted to 1 polyline` for a POLYLINE and a line), where it made a POLYLINE at PLINETYPE 0 on a guess. A line joined
into a POLYLINE picked first still leaves a POLYLINE at 0. BOUNDARY at PLINETYPE 0 is still a guess: AutoCAD's console stops
-BOUNDARY with no word.
**FIXED.** A solid fill with a hole, a MTEXT with colours in it and a few other things were saved as R12 (`DxfDatabase.Save` with
"AC1009") unlike AutoCAD 2021's own R12 (found by probing its console, `Database.DxfIn` of side databases then `DxfOut` at R12; 398
code-built cases of fills, patterns and MTEXT runs, 328 of them equal AutoCAD's record for record where 270 did of the 398):
a piece of a fill beside a hole is cut at the corners of its own two sides only, so that a circle of 25 in one of 50 is 136 SOLIDs
and 134 LINEs and one of 10 in 30 142 and 142 (they were 136 and 132, and 216 and 214), a quadrilateral hole in a rectangle 12 or 14
SOLIDs (16), a chevron-shaped hole 9 (13); which of the two sides that meet at a hole's highest or lowest corner are drawn, and
whether the piece beyond the longer is cut at the level where the shorter ends, follows how AutoCAD walks the loop (the side on the
right first leaves them out and cuts, the side on the left first draws them, the eight ways round one hole giving 12, 12, 14, 14,
14, 14, 16 and 16 SOLIDs, each as AutoCAD's); a convex tip of an outer loop still cuts the slanted pieces at its level, and a tip
inside a hole no longer does. A fill on the normal (0, 0, -1) has its SOLIDs written on the extrusion (0, 0, 1), as AutoCAD writes
it. The dots of a pattern that fall on the far end of a line's stretch (DOTS on a rectangle 60 by 30: the dots at x = 60) are left
out. A colour in an MTEXT (`\C3;`, `\c16711680;` read with its lowest byte the red, the index nearest it) gives the TEXT of its run
the colour index as its group 62, as it does the LINEs of the run's stack bar and of letters struck through; the same colour twice in
a row is one run. Still different from AutoCAD: the gradients' own display meshes (152 SOLIDs for a CYLINDER and 332 for a SPHERICAL
one on a rectangle 100 by 60, which are the triangles of a mesh whose corners stand on a 24 by 24 grid of the rectangle and on the
thirds between), the sides at the notch of a star or a W when the loop is walked from some of its corners, and the dashes of a
pattern at 45 and 135 degrees that end exactly on an edge (AutoCAD writes a LINE of no length at some of them).
**FIXED.** Turned words (DIMTEDIT Angle) that stand over the line under DIMTMOVE 1 have no leader when their box is high, as
AutoCAD 2021 draws them: half the height of the turned box, (width / 2 + gap) sin t + (height / 2 + gap) cos t, reaching twice as far
as the leader would rise (twice 4.1154 in the metric styles of the probes), the library drew a leader and a landing of 18.9 and put
their middle past it, where AutoCAD draws neither and puts their middle on the point their start would have been: "1234567890" turned
60 on a line of 5 stands at (3.125, 34.117) with no leader, 8 digits keep theirs; 13 digits turned 30 keep it (8.14 against 8.23) and
14 lose it (8.62). The three probe sets of 2 896 dimensions (words of 1 to 16 digits, angles 30 to 120, lines of 5 to 30, and eight
styles of other sizes, gaps and DIMEXE) had 298 differing and have none, the wide words of tw4 3 differing and now 2, and the other 90
of the 94 probe sets of the harness are byte for byte as they were. Not done (tw17, 58 of 324, with numbers in the harness notes):
words put beyond the end of a slanting line, where AutoCAD moves the dimension line out along its normal by a reach no model of the box
fits (1.658 for "100" turned 20 on a line of 40 at 30 degrees) and on a short line pushes the point to an arrowhead past the end, and the
7 cases of words on a short slanting line under DIMTMOVE 1 where it draws the line twice.
**FIXED.** The words of a multileader the presenter's MLEADER draws stand where AutoCAD 2021 puts them: the middle of their first line is
half their height and the depth of their ink below the baseline above the landing (it was half their height alone), measured in the
font of their text style: "HELLO" of arial.ttf 0.18 high (its O hangs 0.00307 below the baseline) is held 0.09153 above the landing
(154.5767 at 1:50, AutoCAD's 154.5767), "B-12", "x" and "TEXT" 0.09, "gjpqy" 0.11646 and "Default_Text" 0.11499 (9 words, each within
1E-4 of AutoCAD's); a font the presenter does not read leaves half the height. The groups the command writes are AutoCAD's where the
library's template (ezdxf's) wrote others: in the context data the flow of the words by the style (172 of 5, was 1), a background
colour of -1073741824 (91, was -939524096), a background scale of 0 (141, was 1.5) and no automatic column height (295 of 0, was 1); in
the record the overrides of the content type, of 14 and of 18 alone (90 of 279552, was 17072128, with the text colour and the scale in an annotative one), the
colour of the words the style's (92 of -1056964608, was ByLayer) and 295 of 0; and in an annotative multileader the scale in its context
alone (40 of 50 at 1:50) with the record's own scale the style's (45 of 1, was 50, its override of the scale clear). The context of an
annotative multileader made at 1:50 and given 1:100 is group for group AutoCAD's (80 groups each; AutoCAD reads the file back and writes
the same groups). The text width of an annotative dimension at 1:100 that stood at 177.4 for AutoCAD's 177.47 was the width of a font no
one measures (0.7 of the height a letter): with the Arial of the console's Standard "100.0000" is 100.94 wide and the words stand at
177.47, as the probe had them.
**FIXED.** MLEADEREDIT in the presenter makes a leader of its own for an arrowhead on the far side of one line of words, as AutoCAD 2021
does, where the line went to the first leader: the words are measured in the fonts the presenter reads (the folders of the document's
fonts and the Fonts folder of Windows) while the leader is added, so that the new leader's dogleg runs against x and ends a gap past the
right end of their ink, in every context of an annotative multileader: "HELLO" of arial.ttf 0.18 high landed from (150, 150) gets its
landing at 234.6589 at 1:50 and 319.3179 at 1:100, as AutoCAD's. A font that is not read leaves the old landing on the first leader.

**NEW.** A date or a time typed in a table's cell in the presenter (`CadTableEditor.SetText`, TABLEDIT and the editor of a cell) is
a date as AutoCAD 2021 reads it, where it stayed text. In the order of the culture of the machine: 1/1/2020 is a date of the kind 8
and the format `%x`, shown 1/1/2020; 12:30 a time of `%X`, shown 12:30:00 PM (on 30 December 1899, as AutoCAD keeps a time); 1/1/2020
12:30 both, `%c`; the 16 bytes of the date go in 92 and 310 as AutoCAD writes them (1/1/2020 is `E4070100030001000000000000000000`),
with the flags 4 and no alignment of its own. A month in words (Jan 1 2020), a year of two figures (1/1/49 is 2049, 1/1/50 1950),
no year (the present one), 12:30 PM and 1:5 are read; 2/30/2020, 24:00, 12:30:45.5, a weekday, a zone and 3.4.2020 (in en-US)
stay text, as AutoCAD keeps them. A date typed over a date keeps the cell's format and shows in it (`%d/%m/%Y` shows 3/7/2020 as
07/03/2020; `%A`, `%B`, `%H:%M`, `%I:%M %p` and the rest of `strftime`'s letters and M/d/yyyy are written), and anything that
is no date over a cell of a date is refused ("Cell A3 holds a date, and "abc" is not a date.", a number too), as AutoCAD's
`SetValue` throws eInvalidInput. The editor opens a date as AutoCAD's `GetTextString(FormatOption.ForEditing)` does: 1/1/2020,
12:30:00 PM or 1/1/2020 12:30:00 PM whatever its format (`DxfTable.TextStringForEditing`), and `SetFormat` on a date keeps the
format and shows it.

**FIXED.** `DxfTable.SetFormat` and the numbers typed in the presenter showed a number of a format of units as AutoCAD 2021 does
only for `%lu2%prN` and `%lu4%prN`: the units of `%lu6` are the drawing's (LUNITS) and a format with no `%prN` takes its places
(LUPREC), where `%lu4` showed four places at any LUPREC and the other units under `%prN` six places of decimals. 12.3456 under
`%lu6%pr2` is now 1'-0.35" at LUNITS 3 and 1.23E+01 at LUNITS 1, under `%lu3%pr0` 1'-0", under `%lu1%pr0` 1E+01, under `%lu5%pr2`
12 1/4 and under `%lu4` at LUPREC 2 1'-0 1/4"; `%zs1` and `%zs2` leave out nought feet and nought inches of feet and decimals of
an inch (-0.5 under `%lu3%pr2%zs1` is -0.50", 11.99999 under `%lu3%zs2` 1'), as they do of feet and fractions (85 texts of a
probe of 3 690 in AutoCAD 2021's console, under LUNITS 1 to 5).

**FIXED.** The rows of a table the presenter grows for the words in its cells (`CadTableEditor`, the editor of a cell, the least a
row's line is dragged to) were laid out by the text height and the text style of the table style alone, where a table that keeps
looks of its own for its title, header or data rows (the 140 and 7 of its head, which it drew its text with already) is laid out
by those, as AutoCAD 2021's `GenerateLayout` does: with a text height of its own of 8 for its data, an empty data row is 8 times
4/3 and two margins of 1.5, 13.66667, where the style's 4.5 made it 9, three lines make one 37.66667 and 'aaa bbb ccc ddd eee' in
a column 30 wide 64.33333. A cell's own height still comes before them. A row that holds a cell of a text height of its own and
others of the table's own is laid out by the first alone in AutoCAD (a data row of 8 with one cell of 6: 11, not 13.66667); the
presenter takes the tallest, which is the parked difference.

**FIXED.** Cells merged across rows (`CadTableEditor`, the rows growing for wrapped words and for the cell typed in) gave the last
row of the block what its rows lacked; AutoCAD 2021's `GenerateLayout` shares the block's need among its rows in proportion to
the heights they stand at (the heights their own cells need first), each block in the order of its top left cell: 12 lines of 4.5
merged over two rows of 9 make 45.21 each, not 9 and 81.43, over rows of 12 and 20 they make 33.91 and 56.52, and two blocks
sharing a row share one after the other (17 layouts in AutoCAD 2021's console).
**NEW.** A table saved as R2000 or R2004 from R2007 or later keeps the cells of the older form beside it as AutoCAD 2021 does (the
round trip `ACAD_ROUNDTRIP_PRE2007_TABLE`, made in the extension dictionary of the table where it has none and in the XRECORD of its
TABLECONTENT where it has one): a data table with a row for each cell, a checksum of the cell's text (the sum of its characters,
each times its place from 1: 1 579 for Title), a mask of what the cell overrides (its alignment, its background colour and fill,
the sides that have a border of their own, and on the cell across such an edge the side that faces it) and an XRECORD that holds the
value of the cell, with the class `ACDBDATATABLE` in R2000 and `DATATABLE` in R2004. The data table of 21 tables AutoCAD made (plain,
aligned, with styles and heights, colours, borders on each side and on every edge, a block cell with and without an attribute, merged
cells, text with an accent, a break, a format and 300 characters) and saved as R2000 and R2004 is the one ours has, column for column
and value for value, and the two tables of AutoCAD's own drawing as well; AutoCAD reads our R2000 and R2004 files back with their
cells as it saved them (32 of 36 files equal; in the other 4 a border that is not visible, which AutoCAD itself drops in R2000, and a block cell whose scale AutoCAD fits to the cell, are not kept)
and `AUDIT` finds 0 errors in them. Going up, AutoCAD makes the cells of its own R2000 and R2004 files in the form of R2018 that ours
makes (54 of 54).
**FIXED.** A cell of a table going up from R2000 or R2004 had the flag of content (0x40000 in 91) where AutoCAD gives none (a block
cell, with an attribute value or without; an empty cell with a border on its top alone, 17 472), and lacked it where AutoCAD sets it
(an empty cell with a border on its right: 454 336 for the groups 275, 278 and 279); a text of more than 250 characters made the
table the INSERT of its block both ways (AutoCAD cuts it into chunks of 250 in 3 and 1 in R2004 and before, 2 and 1 with the value
again in 303 and 302 in R2007 and later). The groups 275 and 276 of a border are the right and the bottom, not the bottom and the
right as the notes said (the mask of the data table showed it); the flags were right.
**FIXED.** An MTEXT with a background saved as R2000 lost it. It is kept as AutoCAD keeps it, in the extended data of ACAD after the
line spacing (`ACAD_MTEXT_BBRT` to `ACAD_MTEXT_BERT`: the flags in 91, the scale in 46, the colour in 64 as one number, 0xC3 and its
index or 0xC2 and its red, green and blue, empty names in 65 and 66 and the transparency in 442): 18 MTEXTs of 6 kinds (index
colour, the window's background, a true colour, a scale of 2, a transparency of half, none) in R2000, R2004 and R2007 are AutoCAD's
group for group. Going up, the groups 90, 63, 421, 45 and 441 are made of it as AutoCAD does, but for the transparency, which
AutoCAD loses (its 441 is 0 where the data holds 33 554 560) and ours keeps; an MTEXT of R2000 going to R2007 or later also has the
height of its column (46) after its width, as AutoCAD writes it.
**FIXED.** A solid lofted through circles of different radii (a surface of `loft_spl_sur`) saved from R2018 as R2004 or R2007 was
written as the text of R2004 holding the record of R2010, which AutoCAD 2021 opens as a solid of no volume and no area (volume
7 889.8 and area 2 937.9 came out 0 and 0, with two modeling errors); it now goes as the text of R2010, as the bodies R2000 cannot
hold do, and AutoCAD opens it with the volume and the area of the original in R2000, R2004, R2007, R2010 and R2013 (`AUDIT` 0).
Still left out of R2000 to R2010, and written as they are into R2013, a solid swept along a spline (3 574 lines of R2010 with a
`sweep_sur`, against 689 of R2004 with tolerant edges and vertices: AutoCAD's own volumes of it are 7 869.4368 in R2000, 7 869.4905
in R2004 and R2007 and 7 869.4621 in the others, so no form of ours can be AutoCAD's digit for digit).

**NEW.** The presenter edits a block reference in place, as AutoCAD 2021's REFEDIT: REFEDIT and -REFEDIT ask `Select reference:` (a
reference selected before is taken), the nested reference to edit where the point picked reaches one (`Reference: A > B`, `Ok`/`Next`) and
the objects to take (`Automatic`, `Prompt` for points, `Settings` for the attribute definitions and the lock), and copy the objects of the
block into the drawing where the reference draws them, the working set: BOX, a line from (0, 0) to (100, 0) and a circle of 10 about
(50, 30), placed at (1 000, 0), gives a line from (1 000, 0) to (1 100, 0) and a circle about (1 050, 30), and its references draw the block
without them. Every command works on them; the objects outside the set are locked (a selection leaves them out, any other change says `The
line 2F is not in the working set.`) and drawn faded by XFADECTL (0 to 90, 70 as AutoCAD 2021 has it; `CadDocument.XFadeControl`).
REFSET Add puts objects drawn in the drawing into the set and Remove takes them out, to stay in the drawing; REFCLOSE Save writes each object
back into the block as a new object put through the reference's placing the other way and erases the one it came from, every reference of
the block drawn anew (the circle moved 50 up and saved is the circle about (50, 80) in the block), and Discard erases the copies. The
attribute definitions of the block stay in it unless the setting brings them in, and the attributes of the references are not touched. Undo
stops where the edit opened and one undo takes the whole session back after REFCLOSE; a drawing saved in the middle of an edit opens with it
still open (the XRECORD `GH_REFEDIT` of the named object dictionary). REFEDITNAME holds the block's name. An external reference, a block
array and an anonymous block are refused; the Reference Edit dialog, the Refedit toolbar and writing an xref's file back are not built. The
object menu of a block reference starts REFEDIT as Edit Block In-place. The registry has 255 commands.
**NEW.** HATCHSETORIGIN, the Set Origin of a hatch's right-click menu, as AutoCAD 2021 runs it: `Select hatch objects:`, then
`Select new hatch origin:`, and the pattern of each hatch picked laid from the point, one undo step. A hatch keeps its origin in the
base points of its pattern lines, so each line moves by what the origin moves: ANSI32 at 30 degrees and scale 2 set to (10, 5), then
(20, 20), then (0, 0) has its first line based at each of them and the second beside it as before. A solid fill, a gradient and a
user-defined hatch (the library keeps its lines to its spacing, where AutoCAD moves their base point) are left as they are.
**NEW.** TABLEEXPORT, the Export of a table's right-click menu: `Select a table:`, the Export Data file (`Comma Delimited (*.csv)`, or
`Enter file name <file>:` with no window), and the table written as comma separated values, a line for each row, the text of each
cell as it shows with its formatting codes taken off, the fields of a merged block but the first empty, a field with a comma, quote or
line break put in double quotes; `Can't  open file.` for a file that cannot be written. AutoCAD's own console cannot run the command
(it needs its file dialog), so the exact layout of its CSV is not probed.
**NEW.** VPMAX and VPMIN, the Maximize Viewport of a viewport's right-click menu, as AutoCAD 2021's console runs them: VPMAX asks
`Select a viewport to maximize:` (or takes the viewport worked through), gives the viewport the canvas, ten pixels in, at the scale it
had, turns the other viewports of the layout off and works through it; VPMIN puts the frame, the view (a zoom made meanwhile is given
up) and the other viewports back and works on the paper. `A viewport is currently maximized.` and `No viewport is currently
maximized.` as AutoCAD says them; on the Model tab both are `-VPORTS Toggle`. `CadDocument.IsViewportMaximized` and
`MaximizedViewportHandle` say whether one is. Each is one undo step. The presenter does not take the maximizing back when the layout is
changed, as AutoCAD does.
**CHANGED.** The display list of the presenter holds a third less: a drawing of 1 000 000 entities (77 MB; 1 780 000 items: lines, arcs,
circles, polylines with a bulge, texts and one INSERT in fifty) keeps 237 MB of it where it kept 335, and the heap grows by 650 MB once
the drawing is opened, displayed and its pick index made, where it grew by 746 MB (658 MB where it was 755 MB under .NET Framework 4.8);
a drawing of 100 000 entities keeps 23.6 MB where it kept 33.4. Five things, each the same list as before: an item holds its handle and
one object shared by the items of its style and layer, which also holds the clip of a block and the flags few items have, in place of two
references, the clip and three flags (16 bytes an item); a chain of two points, a LINE, is kept as its box and two flags and no array of
vertices, 80 bytes where it took 96 and an array of 56 (`DisplayPolyline2.Xy` makes the list of four points when asked, so it is asked once
and read); a text keeps its strokes, parts, measure, slant, width factor other than 1, and bold and italic in one object made when it has
any, 128 bytes where it took 192; the insertion point of an entity is kept in the entry that finds it, 40 bytes where it took 100, and a
run of the list that reads no other entity is 24 bytes where it was 32. An arc and a circle lose the 16 bytes of the shared look and keep
their box (104 bytes where they took 128): worked out at each ask it made a frame of 1 000 000 entities shown whole 10 per cent slower.
At 1 000 000 entities, .NET 8, medians of three: display 4.5 s to 4.2 s, a frame shown whole 175 to 167 ms, a layer switched on 1 100 to
1 000 ms and off 200 to 190 ms, all other frames and pans unchanged; at 100 000 entities the same within the noise. The display list of 15
drawings (4 of our own and 11 AutoCAD samples) and the pixels of each at 3 views, plain, selected, highlighted and hovered, are
the same bit for bit, as are 200 000 random chains (zeros of either sign, NaN, infinities), arcs and texts, under .NET 8 and .NET Framework
4.8.
**CHANGED.** A drawing of the presenter holds a third less memory once displayed: the objects of its entities and what they have read take half of what they took. A DXF of
1 000 000 entities (77 MB: lines, arcs, circles, polylines with a bulge and texts, one in fifty an INSERT) held 737 MB in the objects of
its database once displayed, the 73 MB of its bytes among them, and holds 364 MB; opened, displayed and its pick index made, the heap
grows by 747 MB where it grew by 1 119 MB (756 MB where it was 1 169 MB under .NET Framework 4.8); a drawing of 100 000 entities
75.7 MB where it was 113.0 MB. Four things, each the same drawing as before: a polyline asked `HasWidth`, as every polyline is when the
display is made, kept the groups of each of its vertices as strings, some 850 bytes of a polyline of four vertices, which it now reads,
asks and lets go of; the entities of a file share one object for each look (layer, colour, true colour, linetype, lineweight and
thickness) where each held one of 72 bytes and a string of its layer, and keep their handles; the fields few entities use (those of a
record that follows another, of an entity made in code, of one changed) are held apart, so that an entity read from a file takes
104 bytes where it took 152; and the shapes of an entity that has one are kept in a list of 24 bytes where a list of one in a
read-only wrapper took 88. Open 0.61 s and display 4.0 s fall to 0.61 s and 3.0 s at 1 000 000 entities (.NET 8; 6.7 s to 5.6 s
under .NET Framework 4.8), a MOVE with its display 0.88 s to 0.38 s, undo and redo with their display (read ahead) 0.25 and 0.21 s to
0.21 s both, and a layer switched on at 100 000 entities stays 70 ms. The display list of 3 drawings at 3 views, plain, selected and
with a highlight, the saved bytes of 12 drawings and of each after a MOVE, its undo and its redo are the same bit for bit.
**CHANGED.** A drawing read ahead for undo or redo that an edit lets go of while its file is being walked stops there, within
4 096 records, and no longer keeps the worker busy to the end of the walk (170 ms at 1 000 000 entities) before the next reading.

**CHANGED.** `DxfHatch.ShadeTintValue` on a gradient of two colours in one-colour mode makes the second colour the tint of the first, as
AutoCAD 2021 does each time it is set (the same value again too): red at 0 and green at 1, the tint set to 0.3, read back as red and
(153, 0, 0), a true colour at the value the second colour had; a first colour of the index is tinted from its palette colour (index 7, 0.3,
gives (77, 77, 77)). A gradient of two colours in two-colour mode, and one of one colour only, keep their colours, as before, and setting the
colours or turning one-colour mode on changes nothing until the tint is set. The tint counts in whole hundredths, cut down, in single
precision, for this colour and for what `EvaluateGradientColorAt` gives of a gradient of one colour: (100, 150, 50) at 0.336 and at 0.333333
ends in (84, 126, 42), that of 0.33, and at 0.994 in (252, 254, 251), that of 0.99 (48 tints of 3 colours read from AutoCAD; it was the
colour of the exact tint, (85, 127, 42)). A gradient of two colours in one-colour mode that is then given a tint now runs to the tint, where
it kept its second colour.
**NEW.** -HATCH's draW boundary closed with Close after two points makes a hatch of the line and back, a loop of no area, as AutoCAD 2021
does (`(0, 0)`, `(100, 0)`, Close gives one External loop whose edges run (0, 0) to (100, 0) and back; retained, the closed polyline of
two vertices; inside a square an island of no area, outside it a loop of its own); two points of one place make none, and two points left
open make none as before. AutoCAD 2021 reads the DXF of such a hatch.
**NEW.** Gap tolerance (HPGAPTOL) takes a loose end of an arc on along its circle, as AutoCAD 2021 does, where it had none: the arc of
radius 50 about (50, 50) from 20 to 90 degrees ends at (96.9846, 67.1010), a line lies at y 64, and the hatch closes at (98, 64), where the
circle meets the line (the tangent would meet it at 98.113), 3 072.7528 square units as AutoCAD hatched; an arc ending straight above or
below a line closes at 99.9099 (4 763.4054), at either of its ends; and the way on of an arc and of a line leaning in meet at the corner
(99.9251, 47.2638) (7 407.87).
**CHANGED.** The gap tolerance (HPGAPTOL) closes loose ends as far as AutoCAD 2021 does, which is further than it did: a loose end that goes
on and meets a line or an arc closes where it reaches no further than twice the gap and a pixel of the view (it was the gap); where it
meets the way another loose end goes on, the shorter way may be no longer than the gap and half a pixel and the longer no longer than
twice the gap and a pixel (it was the gap for each); and two ends facing each other, on one line or not met so, are joined where they stand
no further apart than the gap and half a pixel. AutoCAD's pixel is the height of its view over the 534 pixels of the console's screen (a
view 140 high, 0.262; 280 high, 0.524), the extents of the drawing having none of it (probed: a far line changed nothing); the presenter
takes the pixel of its own view and none where it has no view, as in a test. Read from AutoCAD 2021 at views 140 and 280 high: a line
ending 3 off a bar closes at a gap of 1.4 and not 1.35, 6 off at 2.9 and not 2.7, 20 off at 9.9 and not 9.85 (9.75 and not 9.7 in the view
280 high), 2 off at 0.9 and not 0.85 (the square with the gap of 2 closed at 1 where the guide said not); ends 5 and 5 across a corner
at 4.9 and not 4.8, 3 and 3 at 2.9 and not 2.8, 2 and 8 at 4.4 and not 3.5; two lines 4 apart on one line at 3.9 and not 3; the arc that
stands off a line, reaching 3.263 along its circle, at 1.55 and not 1.5. `CadBoundaryFinder.Find` takes the pixel in an internal overload;
the public one has none.
**NEW.** -HATCH's draW boundary says what AutoCAD 2021's console says of a shape that closes no area: nothing as it ends, and where no hatch
is made `Unable to fill the boundary with solid.` (solid or gradient) or `Unable to hatch the boundary.` (a pattern) as the hatch is asked
for, where it said `A boundary needs three corners at least.` as the shape ended; one point and Enter goes back to the main prompt, as
it does in AutoCAD, keeping the shapes drawn, where it asked for another start point; Close after a line of no length ends the shape.
**CHANGED.** Gap bridging with many loose lines is quicker, with the loops the same bit for bit: the piece a bridge lands on is found
among the pieces near it, not by a pass over all of them that built an arc for each. 8 000 loose lines, each 10 long, scattered over
3 000 by 3 000 with a gap of 5 took 145 ms to find the area round a point (37 ms with no gap) and took 70 ms with the change (100 ms once the wider reach of the entries above is in); 2 000 lines over 1 500 by 1 500 took 21 ms and
took 13.5 ms with it (16 ms with the wider reach) (.NET Framework 4.8, best of 5-7). The loops of the six inputs timed, every cycle, vertex, bulge and
source, are the same bit for bit before and after this change taken alone (.NET Framework 4.8 and .NET 10); the arcs of the previous entry change those of the two inputs that hold arcs.
**CHANGED.** TRIM of a closed multiline takes each element between its own two cuts where an edge meets the elements on different
segments near a corner, as AutoCAD 2021 does (1 111 cases probed, 1 091 equal, the 20 left are float-noise ties of the end stop on
corners that are not right angles): the element nearest the pick says which two cuts the pick lies between, and the piece goes round the
first vertex where the end lies before the start. The square of 100 cut by the edge (120, -30)-(95, 20) and the line x = 50, picked at
(75, 0), leaves the top element from (100, 10) round to (50, 0) and the bottom one from (115, -20) round to (50, -20); a piece that goes
round the first vertex and ends on its first or second segment leaves an element that reaches the new end without a stop.
**CHANGED.** MLEDIT's crosses CC, OC and MC run a segment on past the start of an open multiline's first segment and the end of its last,
and say `Invalid operation on a self intersecting multiline.` changing neither multiline where an element would be broken past the end of
an element another segment goes on from (the L (0, 0)-(100, 0)-(100, 100) at scale 20 crossed by a vertical at x = 85, 95 or 100, or by a
horizontal at y = 5, 10 or 15 across its second segment), as AutoCAD 2021 does. A cut that reaches the end of an element leaves no
stretch after it and, on the last segment, nothing past the last vertex.
**CHANGED.** A grip or STRETCH that moves a vertex of a multiline cut across a vertex reads the cut as AutoCAD 2021 does: a segment whose
list is empty or starts after nought goes on from the segment before, so that where the move makes that one whole the list gains a nought
in front (the L of 100 by 100 cut from (50, 0) to (100, 50), its vertex (100, 0) moved to (100, -10), holds 0 and 60 there), shifted by the
way the vertex moved, with nothing left past the last vertex; a segment that turns keeps such a list as it was, no stretch goes below
nought, and a stretch that moves to nought is kept as nought (cut 10 to 50, the start moved 10 on: 0, 0, 40). 224 moves probed, all equal.
**NEW.** CAL's `getvar`, `w2u`, `u2w`, `rot`, `xof`, `yof`, `zof`, `xyof`, `rxof`, `ryof` and `rzof` in the presenter, with the values and
the words of AutoCAD 2021's accoreconsole: `getvar(textsize)` reads a system variable of the drawing (an angle in radians,
`getvar(snapang)` 0.523598776 for 30 degrees; a switch 0 or 1; a point as `x,y,z`), `getvar(nosuchvar)` says `Unknown AutoCAD
system variable NOSUCHVAR`, a text variable `Only real, integer or point AutoCAD system variables accepted`; `w2u` and `u2w`
change a point between the world and the current user coordinate system (UCS Origin 10,20,0: `w2u([1,2,3])` -9,-18,3);
`rot([1,0,0],[0,0,0],[0,1,0],90)` is 0,0,-1, the point on the axis `The point to be rotated lies on the axis of rotation`.
`CadCalculator.Evaluate(expression, answers, session)` takes the drawing's command line; without one `getvar` knows no variable
and the user system is the world one. FILTER's Select Filter list is now that of acfilterres.dll in full: MultiLine Style,
Shape Position, Shape Name and Transparency (a per cent from 0 to 90, ByLayer or ByBlock, written `(440 . n)` in `filter.nfl`)
are lines (`CadFilterProperty.MultilineStyle`, `ShapePosition`, `ShapeName`, `Transparency`), and Material, Position Marker and
the surfaces Blend, Network, Patch, Fillet, Offset and Extend are kinds of object (the six surfaces are plain SURFACE objects
in AutoCAD 2021, so each matches every plain SURFACE); Add Selected Object names an MLINE `MultiLine` and a shape `Shape`, with
their style, name and position. `-XBIND Block` binds the dimension styles of the dimensions in the block as AutoCAD 2021 does:
a dimension in MYDIM comes in `grid$0$MYDIM` of the same settings, whose text style comes as `grid$0$MYSTYLE`, a dimension in
Standard in `grid$0$Standard` with `grid$0$Standard` for its text style, the drawing's own Standard left as it is. QPLOCATION
is a system variable (0 or 1, `Requires 0 or 1 only.`), kept for the session; the panel is still placed by the cursor.
New: `CadFilterProperty.MultilineStyle`, `ShapePosition`, `ShapeName` and `Transparency`, `CadSelectionFilter.TransparenciesIn`,
`MainViewModel.QuickPropertiesLocation`. The registry is unchanged (no new command).
**CHANGED.** CAL's `rad` on a straight segment of a polyline says `Polyline arc segment must be picked.`, as AutoCAD 2021 does; it
said `Improper type of object picked.`, which a line or any other object still gets.
**NEW.** The screenshot mode also draws `find` (Find and Replace on "plate", its result listed), `attach` (Attach External Reference on
the `grid.dxf` beside the drawing) and `layerstate-new` (New Layer State to Save); Drafting Settings was there as `dsettings[:Tab]`.
**FIXED.** Find and Replace (FIND) did not open: its two group boxes took the style of a text block, so the window threw a
`XamlParseException` ("'TextBlock' TargetType does not match type of element 'GroupBox'") when it was made. The groups are framed boxes
with their titles on the frame now, and the results list was white-headed with a pale selected row under the dark theme: its header, its
rows and the row picked are dark as the theme's.
**FIXED.** The lists of the dialogs scrolled with the pale scroll bar of the system (Rename, Lineweight Settings, Viewports, View
Manager, Load Linetypes, the compare list of Dimension Style Manager): the theme has its own scroll bar now, 12 wide with no arrows, a
rounded thumb lighter under the mouse and blue while dragged, for every list, grid, text box and drop-down where the theme is used. A list
switched off was a white box (the additional angles of Drafting Settings before Additional angles is ticked, the system paints a disabled
list white); it is dimmed instead. A picture drawn by the screenshot mode took its content out of the window and so lost the styles the
window sets for a kind of control; it keeps them now, so it shows what the window shows.
**FIXED.** The name box of Named Viewports clipped a 16-character name ("Plan and sect" of "Plan and section"): the list column is 290
wide instead of 250, the box 128 wide instead of 88 for its words. The description of New Layer State to Save was one line tall at the
bottom of its room; it fills it now.
**FIXED.** The DWG writer made the radians of an angle from its degrees as `degrees * pi / 180`, and the reader the degrees of a
radian as `radians * 180 / pi`, which are not each other's inverse: 236 ARC objects of AutoCAD 2021's own R2018 drawings came back one
bit off in an angle. AutoCAD turns a degree text into radians as the degrees divided by 57.29577951308232 (1 207 of 1 400 typed ARC
angles to the bit, against 993; 545 of 600 TEXT rotations, against 451), so the writer divides so and the reader multiplies by it:
of 470 angles that came back a bit off 422 now come back the same, and 63 of the 236 ARC objects remain, radians no double of degrees
divides to (a degree is the coarser step there, 1E-16 radians). The DXF a DWG is read as has the last digit of some angles another
(an arc end of 160.00000000000006 degrees reads 160.00000000000003), and 88 of the 167 DWGs of the corpus, those with angles, are written with an ulp of one
of them changed.
**FIXED.** The network and the variable of a dynamic block that come from a DWG of AutoCAD 2010 (the action of version 1) are written
to R2013 and R2018 as the action of version 2 with its four numbers more, as AutoCAD 2021 brings them up (6 networks and 35 variables
of Architectural - Metric equal its own R2018 bits; 25 to 63 in each of the other seven drawings that have them), and an action of version 2
written to R2010 or R2007 is of version 1 without them, as AutoCAD takes it down. The 2 633 dynamic-block objects of the eight
sample drawings with a network are all in AutoCAD's DXF of the rewrites, and `AUDIT` finds what it found.
**FIXED.** The true colours and the colours of a book of a DWG of AutoCAD 2000, which AutoCAD keeps as round-trip data (`RTTcAl` extended
data on an entity, an extension dictionary with `ACAD_XREC_ROUNDTRIP` on a layer, `DBColXD` on a DBCOLOR), are taken into the colour
when the drawing is written to R2018, as AutoCAD does saving it as R2004 or later: they were written as extended data of an entity
that shows the nearest index, and a layer's book colour gave no name. 4 layers and 9 entities of true colours and the colour of a
book, saved by AutoCAD as R2000, are written to R2018 with `AUDIT` 0 and AutoCAD's DXF of them has the 37 true colours and 3 names of
its own R2018. A transparency is not in R2000's round-trip data. The `RTMaterial` extended data still passes unchanged.

**NEW.** The app's screenshot mode (`--screenshot out.png plan.dxf --dialog name`) draws the dialogs of rounds 5 to 9, each over the
drawing open in a sample state of its own: `mlstyle`, `mlstyle-new`, `mlstyle-new-page`, `mlstyle-modify`, `mlstyle-load`, `mledit`,
`plotstamp`, `plotstamp-advanced`, `plotstamp-fields`, `imageadjust`, `attext`, `xbind`, `insertobject`, `recovery`, `spell`,
`spell-settings`, `objectscale`, `objectscale-add`, `insertlayout`, `templateoptions`, `saveaspdf`, `pdf-override`, `pdf-options`,
`rename`, `lweight`, `boundary`, `viewports`, `viewports-named` and `filter`. A drawing that lacks what a dialog is made of (Image Adjust
with no picture) says so and exits with 2, as does a run with no drawing.
**FIXED.** Drawing those dialogs showed what no test had: the Advanced Options of Plot Stamp had its log file row under the buttons (the
window was 480 high and its content 502); the Select objects button of Check Spelling showed no icon (its padding left 2 pixels of
28); the check boxes the theme draws never showed their words (Link and Display As Icon of Insert Object, every row of Check Spelling
Settings, Append to current selection set of Quick Select, Match case and List results of Find); the editable boxes showed nothing and
had no place to type in (UI font of Global settings, Font of Plot Stamp's Advanced Options, Value of Quick Select and of Object Selection
Filters, and those of Drafting Settings, Attach Reference and New Layer State); and the description of Template Options was one line
tall and centred, as was the multi-line description of Block Definition, Edit Block and New Layer State. Check Spelling Settings is
30 higher to hold its rows with their words.
**NEW.** Include hyperlinks reaches the objects seen through a layout's viewports: an object of model space with a hyperlink gets a link
in each viewport that shows it, over its box on the paper cut to the frame of the viewport (or to the box of its clipped outline), none
where its layer is frozen in the viewport. A line (1 800, 1 200) to (2 200, 1 800) in a viewport 200 by 150 at 1:20 about (2 000, 1 500)
is a link over (140, 85) to (160, 115) of the sheet, the same as one drawn there; the links of objects drawn in the layout itself are
as before.
**CHANGED.** Esc and Stop stop a drawing opening in the presenter's window within a few milliseconds. The reading of the file looked
at the stop only between its stages, so a DXF of 1 000 000 entities (77 MB) asked to stop while it was read, or while its
entities were listed for the display, went on for up to 0.74 s: asked at 20 to 2 000 ms into the opening, the window was free again
after 0.4 to 0.5 s while the file was read and 0.7 s while its entities were listed. The walk over the records of the file, the
scan of its handles, the listing of its entities of model space and the listing for the display now look at the stop every 4 096
records or entities, and the window is free again 59 ms after Esc at most (1 to 2 ms in most runs, 30 to 40 ms where Esc came while
the bytes of the file were read). An opening that runs to its end takes the time it took (open and display 5.3 to 5.6 s, 8.2 to
8.6 s under .NET Framework 4.8; the walk of the records 220 ms either way) and gives the display list item for item as before.
**NEW.** `CadDocument.Open(path, progress, cancellationToken)`; a DWG is looked at once, before it is read. The library's own
`DxfDatabase.Open` is unchanged.

**CHANGED.** The presenter's display list keeps less memory. The items of a drawing share the strings of its layer names, where
each entity kept a string of its own; the lists of items and of entities drawn give back the room their doubling left empty; and the
list of insertion points of an entity is made for one point. A drawing of 100 000 entities (178 000 items: arcs, lines, polylines
and texts) keeps 35.5 MB of display list where it kept 41.1 MB (estimated from the objects the list holds). The items are the same:
the display list and the pixels of 3 drawings at 3 views, plain, selected and with a highlight, are the same bit for bit, and the
whole drawn again takes 250 to 300 ms and the layer switched on 95 to 110 ms, as before.
**NEW.** -HATCH's draW boundary draws arcs (R2.2 follow-up, hatch3): Arc asks AutoCAD 2021's `Enter an arc boundary option
[Angle/CEnter/CLose/Direction/Line/Radius/Second pt/Undo/Endpoint of arc] <Endpoint>:` (CLose from the second segment on) and takes the
arc segments of PLINE: an end point leaving the way the shape came (east at the first point, or the way the last line was drawn), an
included Angle, a CEnter, a Direction, a Radius, a Second pt; Line goes back to straight segments, CLose closes with an arc leaving
that way, Undo takes the last segment off and goes on in line mode (`All segments already undone.` at the first point), and an end
that makes no arc says `*Invalid*`. Length goes on along the tangent of an arc. A line from (0, 0) to (100, 0), an arc to (100, 50)
and a line to (0, 50), closed, hatches 5 981.75 square units, and the arc closed with CLose from (100, 50) is the arc of radius 125
about (100, -75), as AutoCAD drew it; a shape of two points is a loop where an arc gives it an area (a line and an arc, or an arc
alone). A retained boundary keeps the bulges in its polyline.
**NEW.** Gap tolerance (HPGAPTOL) closes a loose end as AutoCAD 2021 does: a line goes on along its own direction and closes where that
reaches another line or arc (a line ending 2 off the middle of a longer one at HPGAPTOL 5 hatches the box and drops the 20 of the bottom
that stands past it), or where it meets the way another loose end goes on (the ends (100, 0) and (103, 3) of two lines, 3.09 from their
meeting at (103.09, 0), close at that corner where the straight piece between them is 4.24 long), or the other end; two ends not so met
are joined directly, as before, the nearest bridge first. A line leaning in to (97, 3) goes on to (96.907, 0) on the bottom.
**CHANGED.** Boundary set of -HATCH, as AutoCAD 2021's console runs it: a point whose area the candidate set does not hold says
`Valid hatch boundary not found.` and the search goes on among every object, so that with the set of one square a point in another
square hatches that one (AutoCAD did, with both words); a point in nothing says it once. A pick in no set no longer writes
`Analyzing internal islands...` before `Valid hatch boundary not found.`, and a pick in a set writes only the words AutoCAD does.
**CHANGED.** `DxfHatch.EvaluateGradientColorAt` gives AutoCAD 2021's colours: each channel is cut down to a whole number (blue (0, 0, 255)
to a pale yellow (255, 255, 153) is (127, 127, 204) halfway, not (128, 128, 204); the drawn colour of such a gradient follows), and a
gradient of one colour holding only that colour runs to its tint in hue, saturation and lightness, as the presenter's one-colour gradient
already wrote it (red at 0.25 to (128, 0, 0), (100, 150, 50) at 0.75 to (191, 223, 159)), where it mixed red, green and blue towards black
or white. 1 101 channels of 12 two-colour gradients and 25 pairs of colour and tint at 9 values were read from AutoCAD and agree.
**CHANGED.** `DxfSpline.ElevateDegree` raises a spline to degree 25, the order of 26 AutoCAD's Elevate order takes, where it stopped at
10: the 13, 26 and 23 control points AutoCAD 2021 gave for a cubic raised to 12 and 25 and a cubic with an inner knot to 12 are met to
1E-7 and the curve is the same to 1E-9.
**FIXED.** `GeoSpline2`, `GeoSpline3`, the DXF reader and `DxfSpline.ToGeoSpline3` take a degree up to 25, where they refused one past 10
(`ArgumentOutOfRangeException`), and every query is right at those degrees. The cause: the products of Bernstein polynomials that the speed
breaks, the nearest point and the crossings with a circle are made of read their binomial coefficients from a table of 64 rows, and a rational
spline of degree 25 multiplies polynomials up to degree 122, so `Bernstein.Multiply` read past it (`IndexOutOfRangeException` in the nearest
point of the rational spline, found on raising the cap); the table is now 125 rows, the rows to 64 the same to the bit. A cubic raised to degree
11, 17 and 25, plain and rational, has the points of the cubic to 4E-13, its length (395.6538 and 399.5111) to 1.2E-13, and the same bounds,
closest points, crossings with a plane, a line, a circle and another spline, derivatives to order 25 (the forward differences of a Bézier
curve of degree 25, exact), offset and split; 20 random splines of degrees 11 to 25 in the plane have the closest points and crossings that a
sample of 20 000 points shows. Splines of degree 10 and below are the same to the bit (benchmark signatures unchanged).
**CHANGED.** SPLINEDIT's Elevate order in the presenter goes to order 26 as AutoCAD 2021's does (`Requires an integer between 4 and 26.`), and
the spline of degree 25 is drawn on the canvas as the cubic it was raised from.

**CHANGED.** A table going across R2007 (`Save(path, "AC1018")` and back) stays a table where its cells carry more than plain text:
alignment, text height, text style, colours, background, borders of every side, and a block cell. It was the INSERT of its block before.
The cells are AutoCAD's group for group: of 4 tables of AutoCAD 2021 (one with a title, a cell 4 high, one aligned bottom left, red
text and a green background; one of 16 cells with a border of each side, kind and visibility; a linked one) the R2000 and R2004 forms
made from R2018 and the R2018 form made from R2004 are what AutoCAD writes of them (the flags of a border cell, 0x8880, are 297 088 in
R2007 and later and -30 592 in R2004 and before). A cell with a field or a link, or a group the old form has no rule for, still makes
the table its block.
**FIXED.** A table that keeps a TABLECONTENT (a link to a spreadsheet, merged cells in a table of AutoCAD 2021) lost it going up from
R2000 or R2004 (its extension dictionary went, the data link with it), and going down to R2000 or R2004 it was written with the values
of R2007, which AutoCAD discards the drawing for (error 53 reading the file). Its round trip (`ACAD_ROUNDTRIP_2008_TABLE_ENTITY`, its
TABLECONTENT and TABLEGEOMETRY) now goes both ways as AutoCAD writes it: a linked table saved as R2004 and read by AutoCAD 2021 is
linked at the same cell (1, 0), its `AUDIT` finds 0 errors, and the TABLECONTENT of a table of merged cells is AutoCAD's in all 9
directions among R2000, R2004, R2007 and R2018. The data table AutoCAD adds to a table going to R2000 or R2004
(`ACAD_ROUNDTRIP_PRE2007_TABLE`) is taken out going up and not made going down: AutoCAD reads the table, its link and its text height
without it.
**FIXED.** An MTEXT saved as R2000 carries the extended data AutoCAD writes of every one there (ACAD, `MTEXTBEGIN`, its line spacing style
(73) and factor (44) again, a 74 of nought, `MTEXTEND`): the note of a leader and the 6 MTEXTs of a table are AutoCAD's own groups of R2000,
and the data goes when the drawing goes to R2004 or later.
**NEW.** The transform of a solid or surface body may say `reflect` and `shear` (ACIS `transform`, `no_reflect no_shear` until now the
only words): AutoCAD reads both from a file (a box 30 by 20 by 10 with `shear` is 30 by 35 by 10, one with another word a modeling error) and
writes neither (it bakes a mirror into the solid and refuses an uneven scale), and the SAT and the SAB of R2013 and later each keep them.
**NEW.** The extrusion of a closed spline square to its plane (`cylsur` of R2004, `cyl_spl_sur` of R2007 and later) goes between R2004, R2007,
R2010, R2013 and R2018: 6 conversions among the three SAT forms are AutoCAD's records one for one, and the R2007, R2010, R2013 and R2018
files open in AutoCAD 2021 with its volume (2 279.094) and 0 errors in `AUDIT`. The vertex where the closed edge of such a profile meets
the seam is numbered 2 from R2007 (a closed edge alone 0, as before). A solid made by a sweep along a spline, a loft of different circles
and an extrusion at a slant (tolerant edges, a B-spline surface in R2004 that R2000 lacks) are still left as they are.
**NEW.** MLEDIT's tees onto a closed multiline: a stem whose element meets the multiline's element before its start or past its
end, where another segment goes on (a stem 12 from the corner of a square of 200, FOUR at scale 10), says `Invalid operation on a
self intersecting multiline.` as AutoCAD 2021 does, stopping the elements that do meet it and leaving the rest and the multiline
whole; OT and MT onto a closed multiline were probed against AutoCAD (open the near element across the stem, run the inner pair on)
and match.
**CHANGED.** TRIM of a multiline cuts each element at its own hit where an edge meets them on different segments near a corner
(the edge (120, -30)-(95, 20) across (0, 0)-(100, 0)-(100, 100) ends it at (100, 10) with the bottom element stopped at 115), and
leaves it alone where the edge meets one element more often than another. A trim of the start or an extension leaves the last
vertex's stretches as they were, as AutoCAD writes them.
**CHANGED.** MLEDIT's weld (WA) takes out every stop and start between the picks one by one, as AutoCAD 2021: a cut the picks
cover whole is welded, one they cover half is left turned inside out, and round the first vertex of a closed multiline the outer
element is drawn only to the corner, as AutoCAD leaves it.

**NEW.** EXPORTPDF shows Save As PDF as AutoCAD 2021's (designed first): the file with Browse…, Open in viewer when done, Include
plot stamp with its Plot Stamp dialog, Export (Current layout and All layouts in a layout, Display, Extents and Window in model
space, a window's corners asked once the dialog closes), Page Setup Current or Override with Page Setup Override (paper size, plot
style table, orientation, Fit to paper or a scale, Scale lineweights), and Options… for Export to PDF Options. All layouts with an
override of ISO A1 portrait writes a page of 594 by 841 mm for each layout. Open in viewer, the override and Include hyperlinks are
kept for the next EXPORTPDF until the app closes. The choices reach the command as `CadExportJob.PdfOptions`
(`CadExportPdfOptions`); a window that answers only the file exports as before.
**NEW.** Include hyperlinks: the PDF writer makes the first hyperlink of each object plotted a link over its box
(`CadPlotSettings.PdfHyperlinks`); a line linked to `https://example.com/B-12.pdf` is a `/URI` link of the page. Layer information,
bookmarks, merge control and the vector and raster qualities are shown greyed, as the writer always or never does them.

**NEW.** The DWG reader and writer take the block properties table with its grip, the user parameter, the proxy node of a
dynamic block's graph and the associative network of variables, dependencies and their bodies (`ACDBASSOCNETWORK`,
`ACDBASSOCVARIABLE`, `ACDBASSOCDEPENDENCY`, `ACDBASSOCGEOMDEPENDENCY`, `ACDBASSOCVALUEDEPENDENCY`,
`ASSOCDIMDEPENDENCYBODY`, `BLOCKPARAMDEPENDENCYBODY`), and the horizontal, vertical and radial constraint parameters and the
groups of 2D constraints (`ACDBASSOC2DCONSTRAINTGROUP`, 22 classes of node) are read: layouts read from AutoCAD 2021's own DWG bytes,
1 789 objects of its Dynamic Blocks and sheet samples equal its DXF group for group, and the actions of version 2 and the edge names
of a DWG of AutoCAD 2018 read too. A block of two linear parameters with a properties table made in code keeps its graph, table
and grip through a DWG (left out whole before).
**CHANGED.** The band round the arc in which words of an angle put by DIMTEDIT under DIMTMOVE 1 get no leader is refitted over 30 000
probes of AutoCAD 2021 (SHX and Arial, DIMASZ 1.5 to 4, DIMTXT 1.8 to 4, DIMGAP 0.3 to 1.5, DIMADEC 0 and 4, 20 or 72 directions): the
landing counts a unit for a unit level with the vertex and half a unit above it, and the inside edge there stays near 3 whatever the
words. "60°" of Arial put 4 out of an arc of 30 at 85 degrees, 2.75 in, 7 out level with the vertex or 6 out at 45 degrees now
draws the leader (or none) AutoCAD does; the probes of a7 (5 832) differing fall from 101 to 71, a5 (2 938) from 49 to 33, a6 21 to
11, a4 13 to 4, and those of other sizes by about a quarter (6 860 probes of seven styles of other arrowheads, gaps, words and
decimals, from 168 to 126 in Arial and 194 to 137 in SHX); the 30-degree probes of a2, ta2 and ta8 stay at 0 but for one SHX
probe of DIMASZ 4 and DIMGAP 1.5. Still a fit, not AutoCAD's rule.
**FIXED.** Level words of a radius put by hand under DIMTMOVE 2 push its arrowhead out where their box takes the line to the centre or
past it, however far behind it they were put, and within two arrowheads of the centre off the line too: "R20" put 4 behind the centre
on its line keeps the arrowhead in and the line whole (it went out), put 4 along and 3 across it pushes it out (it stayed in). d3 7 and
td3 6 differing of 760 down to 0.
**FIXED.** Level words (DIMTAD 1 and 4) of a radius or a diameter put out past the rim under DIMATFIT 3 stand on a landing an arrowhead
long or above a landing of their own as AutoCAD 2021 fits them to the line: on the landing where the line is longer than the room
their box reaches along it but the arrowheads do not take their place and the words and arrowheads do not both fit (the line is not
longer than the chord through the box and an arrowhead and two gaps, two arrowheads and two gaps for a diameter); above a landing of
their own where the arrowheads fit and are the larger (a tie goes to them) or where both fit. A diameter used to stand its words above
a landing always, a radius on its landing wherever the line was longer than their reach, and no line within 15 degrees of upright put
them on it. "%%c6" put out at 90 degrees now stands on the landing and "%%c7" above one, "R5" at 85 degrees on it, "R10" at 45 above
one (Arial 2.5 high, DIMASZ 2.5, DIMGAP 0.625). Of 924 diameters and radii by 21 angles probed in Arial, 163 differed from
AutoCAD and none do now (8 where the probe gives the viewer the points exact: AutoCAD was given them rounded to six decimals, which
moves a line at exactly 15 degrees to either side of the limit of the near-level words); with the SHX font 153 down to 8, the ties of
"R6" and "R7" straight up falling either way in AutoCAD. t6 15 down to 1 of 1 584; of 91 sets of earlier rounds only r3 (a diameter
of 6 straight up, no AutoCAD pictures) changes besides, to the picture td2 shows.

**NEW.** The 2D constraint group of AutoCAD 2018 (version 2 of its layout: a table of the classes of its nodes, a directory of
every node by class and identifier, then the nodes, the circles and arcs with a fourth real) is read from a DWG of R2018 and R2013
and written to R2018, a group of version 1 (a DWG of AutoCAD 2010) brought up to the version 2 as AutoCAD does when it saves one.
The 34 groups of AutoCAD's R2018 samples equal its DXF group for group (but for the order of the actions they own, a hash order of
its own) and are written to the very bits of the object it saved. All 90 evaluation graphs of AutoCAD's samples, the 18 that lean on a
group among them (left out of an R2018 before), are written, and the 167 sample and test DWGs rewrite to R2018 with the same `AUDIT`
counts as their originals (160 at 0 where the originals have 156), none refused, and the same dynamic-block objects in AutoCAD's DXF
(90 graphs of 90, 34 groups of 34, 1 024 dependencies of 1 024).
**FIXED.** The DWG writer dropped the vertex ids of an `LWPOLYLINE` (group 91, which the constraints of a dynamic block name the edges
of the block by) and wrote the dimensional constraints of a dynamic block with the flag of a block of their picture, which they do not
have: AutoCAD's `AUDIT` counted 34 errors in the rewrite of `Architectural - Metric` (8 polylines and 25 dimensions, one more
besides); the reader and writer keep the ids, and a dimensional constraint is read and written with neither a block (2) nor a type
(70) where that is 0, as AutoCAD's DXF gives it.
**FIXED.** A DWG of AutoCAD 2013 with a dynamic block read 43 of its objects as broken (a dependency on an edge names it by a flag and
a number of a short there too, as in R2018, and the reader took it for a text): it reads with none broken, every object of its
Dynamic Blocks sample equal to AutoCAD's DXF.
**NEW.** -VIEW Restore shows a paper view in the layout it was saved in (a view saved in Sheet 2, restored from Layout1 or the Model
tab, shows Sheet 2), and a view of model space restored in a layout asks `Restoring Model space view.` / `Select Viewport for view:`
and shows the whole view through the viewport picked, which is then the one worked through (a view 600 by 400 into a viewport 200 by
100 is shown 400 high, one 900 by 300 450 high); one undo step. `DxfViewTableRecord.Layout` reads and writes the layout of a view
(group 330 of its XRECORD ADSK_XREC_VTRVIEWINFO, as AutoCAD 2021 writes it); setting a layer snapshot keeps it.
**NEW.** MVIEW's Polygonal option: the points of a polygon as PLINE asks them, a closed polyline made, and the viewport clipped to it,
its frame the box of the polygon.
**CHANGED.** The outline of a viewport clipped by a region is kept between paints and read again once the drawing is edited: 80
microseconds a call before, 0.4 after.
**NEW.** HATCHEDIT's dialog has Click to set new origin, as HATCH's has: the dialog closes, `Specify origin point:` is asked, and the
dialog comes back with the point as Origin X, Y and Specified origin on (Esc brings it back as it was); OK lays the pattern of the
hatch from that origin. The button was greyed in the Hatch Editor.
**FIXED.** `-RENAME VPort` and `CadTiledViewports.Rename` agree. Once a drawing's tiled viewports were loaded, -RENAME renamed the
records of the VPORT table, which the next save wrote over from the configurations the tiles held, so the old name came back; and a
rename made on the tiles was not found by -RENAME. Both now go through the tiles: a configuration `Pair` renamed `Twin` by either way
is `Twin` in `-VPORTS ?`, in the Rename dialog's list and in the saved DXF (2 records), and `Pair` is gone from all three.
**FIXED.** -OSNAP names three snaps as AutoCAD 2021 does (read from its console with each OSMODE bit set): Nearest is `Near` (it was
`Nea`), Geometric center `Gcen` (it was `GCen`), Apparent intersection `App Int` (it was `App`); 16383 reads
`End,Mid,Cen,Node,Quad,Int,Ins,Perp,Tan,Near,Gcen,App Int,Ext,Par`.
**NEW.** CAL's `rad` takes a polyline segment: a polyline of a line, a half circle of radius 50 from (100, 0) to (100, 100) and a line
gives 50 for a pick near the half circle and `Improper type of object picked.` for a pick on a straight segment.

**NEW.** A hatch filled solid, saved as R12 (`DxfDatabase.Save(path, "AC1009")`), is the SOLIDs and LINEs AutoCAD 2021 writes (probed in
its console, `Database.DxfOut` of side databases at R12 beside their R2018, 36 polygons, holes, notches, turned shapes and rounds): the
area is cut at the height of each corner into trapezoids, each two triangles in the plane flipped (the extrusion (0, 0, -1), x and the
height negated), the strips from the top down, then the LINEs of the sides that are not horizontal, those that end in the point of a
V notch or of a bite left out. A rectangle 100 by 60 is 2 SOLIDs and 2 LINEs (it was 2 SOLIDs), with a hole 40 by 20 in it 8 and 8; 32
of the 36 shapes are equal to AutoCAD's own R12 record for record. A gradient is the same SOLIDs with no lines (2 for a LINEAR one of a
rectangle, as AutoCAD). Not done: the star-shaped notch AutoCAD draws the sides of, and the display meshes of the other gradients (152 for a CYLINDER,
332 for a SPHERICAL one on a rectangle 100 by 60).
**NEW.** A multileader saved as R12 is written as AutoCAD writes it (15 probes of one and two lines of words, frames, held at the left,
the middle and the right, four attachments): the frame round the words where the style has one, a POLYLINE of five corners (the first
again) of the colour BYBLOCK round the ink of the words as the fonts lay them out, a landing gap off it, after the arrowhead (words of
txt 2.5 high at (30, 20) give a frame from (29.91, 18.57) to (39.26, 21.25)); the dogleg of words held at the middle or the right with no
frame runs on to the ink of their first line; the words are laid out by the fonts of the drawing, held at the left, as AutoCAD writes
them. All 15 are equal to AutoCAD's own R12 record for record.
**NEW.** The words of an angular dimension saved as R12 are one TEXT of AutoCAD's own form in the dimension's block (its groups in
AutoCAD's order, the place of six figures 2 heights before and 1.77 heights below where the MTEXT was held, a degree sign written
`%%d`), not an INSERT of a block of pieces, so that the blocks after it are numbered as AutoCAD numbers them (they were numbered one
higher for each angular dimension). 6 of 7 angular probes equal AutoCAD's (the seventh has a stack in its words, which AutoCAD flattens with one more space).
**NEW.** The stack in an MTEXT or in the words of a dimension (`\S1/2;`, a tolerance `\S+0.1^-0.1;`) saved as R12 is the TEXT of its
numerator, a LINE for a bar or a slash, and the TEXT of its denominator, laid out as AutoCAD 2021 lays them (probed in its console,
70 MTEXTs and linear, aligned, rotated and radial dimensions made in side databases and written with `DxfOut` at R12; 69 are equal to
its own record for record, one of them in romans.shx). Letters 0.18 high: `1\S1/2;` in a dimension is the whole 1, the 1 0.12 above
the line of words and the 2 0.12 below it with the bar 0.09 above it from x 50.0 to 50.12; the line holding a stack is 0.42 high, so that
held at the top left `A\S1/2;B` has its A and its 2 0.42 below the top and the 1 0.24 above them, and the line after it stands 0.24
below. A tolerance (`^`) has both halves at the left and no bar, a slash (`#`) a LINE at 55 degrees twice the height long, a stack with
no sign (`\S12;`) an empty denominator; a half with no words stands no height for it. `DxfMText.Explode()` gives the same pieces
(it was the one TEXT of the words with the stack read as `1/2`). A stack with a line under, over or through it, in a TrueType font or
beside a big font was still one TEXT here (it is laid out now, below).
**NEW.** Extended data saved as R12 is written as AutoCAD 2021 writes it: the names of its applications in capitals, a layer's (`1003`)
as the table of layers writes it (`a{b}` is `A_B_`), and each point of 1011 to 1013 in five groups, its x, `1021` and `1031` of 0.0,
then its y and z (`1011` 4, 5, 6 is `4`, `0.0`, `0.0`, `5`, `6`); a point of 1010 is as it was. 3 of 3 probes are equal to AutoCAD's
own R12.
**NEW.** The widths of an LWPOLYLINE saved as R12 are the defaults of the POLYLINE as AutoCAD writes them (12 probes, all equal): the
header's `40` and `41` are those of the first vertex (each left out where it is nought) and a vertex holds a width only where it
differs, so that a polyline of constant width 2 is `40` and `41` of 2 and no width on its vertices (each vertex held both). An
attribute of version 0 whose position is free has no AcDbAttr extended data now, as in AutoCAD's own R12; a locked one has it.
**NEW.** A SPLINE saved as R12 is a 3D POLYLINE of 32 vertices for each of its control points, as AutoCAD writes it (4 control points
128, 6 192, 40 1280; a spline through 5 fit points, whose cubic has 7 control points, 224; 29 probes of control and fit splines of
degrees 1 to 5, rational and closed ones among them, equal to AutoCAD's record for record). It was 128 vertices for every spline.
**NEW.** The LINEs of a pattern hatch in a plane other than that of the drawing carry the extrusion of the hatch in the R12 as AutoCAD
writes them (a hatch on the normal (1, 1, 1) has 210, 220 and 230 of 0.577 on each of its lines; 6 probes in tilted planes equal; the order of the lines of a crosshatch such as ANSI37 is below).
**NEW.** A circle filled solid saved as R12 is cut into the chords AutoCAD cuts it into: the fewest for which a chord stands 0.1 off the
circle at the most, 33 at the least (36 for a radius of 25, 76 for 30, 48 for 50, 140 for 100; it was 32 for every circle). The 62 circles
of 5 to 70 and 200 units probed equal AutoCAD's own R12 record for record. A circle with a hole in it, a bulge, an arc and an ellipse were
still cut from the library's own corners (the last three are cut as AutoCAD cuts them below).
**NEW.** The stack under, over or through letters of an MTEXT or a dimension's words (`\LA\S1/2;B\l`, `\O`, `\K`), saved as R12, is laid
out as AutoCAD 2021 lays it out (probed in its console, `Database.DxfIn` of side databases and `DxfOut` at R12: 57 MTEXTs of stacks
with bars, slashes, tolerances, no sign, empty halves, two lines at once, several heights and holds, all equal its own record for
record; they were one TEXT). A line under or over lifts the numerator and the bar by a fifth of the height for each half that has
words (each line again where both are there) and each half's TEXT begins with `%%U` or `%%O` (`%%O%%U` for both): `\LA\S1/2;B\l` of
letters 2.5 high held at the top left at (10, 10) has A at (10, 3.1667), the numerator `%%U1` at (12.9167, 7), the bar at 6.5833 and
`%%U2` at (12.5, 3.1667), where the stack alone has A at 4.1667. The letters struck through (`\K`) are no look of their own and have
no `%%K`: they are one TEXT with the letters about them, and AutoCAD draws the line through them as a LINE of the linetype CONTINUOUS,
half the height above the baseline from where the strike begins to where it ends, written after the last entity of its space (or
before the end of its block), outside the block of the TEXTs; through a stack it draws lines of no length (the numerator's of the
width of its words where the strike begins at the stack). `DxfMText.Explode()` gives the
stack under a line as pieces too (it was one TEXT), and still the `%%K` of letters struck through.
**NEW.** A stack in a TrueType font, or beside a big font, saved as R12, is laid out as AutoCAD 2021 lays it out (63 of 65 probes in 13
fonts, with bars, slashes, tolerances, two lines and several holds; they were one TEXT): in a TrueType font the halves
are as high as the ink of their letters from the lowest to the highest (Arial's 1 and 2 reach 1472 units, not its A's 1466, and a
descender counts), the stack begins where the ink of the letters before it ends (Arial's A is inked to 1369 and moves the pen 1366), the
slash's denominator follows the ink of the numerator by a third of the height, and the letters after it stand as far from the ink of the
stack as the zero has room right of its own ink (98 of 1466 units in Arial, 73 of 1387 in Times New Roman, 229 of 1170 in Courier New):
`A\S1/2;B` of Arial 2.5 high has its B at 14.1576 where the SHX letter would stand at 14.5629. Not done: Palatino Linotype, whose halves
are 0.2 lower than its glyph boxes give, a stack with a letter of the big font in a half, and what follows a stack beside a big font in
a line of letters of both fonts.
**NEW.** A solid fill in a plane other than that of the drawing, saved as R12, is the SOLIDs and LINEs of its strips (it was nothing):
the strips are cut in the plane of the hatch, the SOLIDs stand on the opposite of its normal with their corners as that plane reads
the world points, the LINEs in the world with no extrusion; a rectangle 100 by 60 on the normal (1, 1, 1) is 2 SOLIDs on the
extrusion (-0.577, -0.577, -0.577) and two LINEs of 60 from the origin along the plane's y axis. 3 of 4 probes equal AutoCAD's own R12
(normals (1, 1, 1), (0, 1, 0), (1, 0, 0) at an elevation of 25, a hole in it); the fourth, the normal (0, 0, -1), has its extrusion
(0, 0, 1) written on the SOLIDs by AutoCAD, as the library cannot write one that is the default.
**NEW.** The strips of an arc, a bulge and an ellipse in a solid fill saved as R12 are cut as AutoCAD cuts them: an arc or a bulge into
the fewest chords that stand 0.1 off the circle at the most and the share of 33 its sweep is of a turn at the least (17 chords for a
bulge of 0.5 over a chord of 100, 6 for 1 radian of a radius of 5), an ellipse into as many chords as a circle of its major radius, at
equal steps of its parameter (50 for 50 by 25 or 50 by 20 turned 45 degrees); they were 16 to a half circle. 19 probes of pies,
segments, bulges, half circles and ellipses alone in a fill equal AutoCAD's own R12 record for record. Not done: a round hole in a
fill (a circle of 25 in one of 50 is 136 SOLIDs and 132 LINEs, AutoCAD's 136 and 134; a hole of 10 in a circle of 30 is 216 and 214
where AutoCAD writes 142 and 142), a star (2 LINEs short) and the notches of a hole: AutoCAD joins the pieces beside a hole only where
its corners allow, cuts them at the level of the corner next to the hole's highest, and draws the sides at its lowest, by a rule not
worked out in 18 probes.
**NEW.** An attribute of several lines (an MTEXT held at its top left, R2018's multi-line attribute) saved as R12 is one attribute for
each line, as AutoCAD 2021 writes it (6 of 6 probes equal; it was one ATTDEF or ATTRIB of the lines joined): the tag ends in `_001`,
`_002` and on, the first keeps its place in the list and the others come after the other attributes of the block or the INSERT, those
of an INSERT with `AcDbAttr` of version 0 and the position locked, each at the baseline of its line as the MTEXT lays it out: `MARK` of
`M20\Pgrade 8.8` 2.5 high held at (0, -5) is `MARK_001` `M20` at (0, -7.5) and `MARK_002` `grade 8.8` at (0, -11.667). Left as before:
an attribute held other than at its top left.
**NEW.** The LINEs of a pattern hatch saved as R12 are written in AutoCAD's order: the last pattern line first, and of each line
of the pattern the last piece first (ANSI37, two pattern lines, writes the lines at 135 degrees before those at 45; a line a hole
breaks in two writes the piece beyond the hole first; a dashed line its dashes from the last). 11 of 14 probes of patterns equal AutoCAD's own R12
record for record (the others: dots and dashes at 45 and 135 degrees, where AutoCAD writes one dash more at the edge).
**CHANGED.** The MTEXT that `DxfMLeader.Explode()` gives is laid out in the fonts of the multileader's drawing, so that its
`ActualWidth` and `ActualHeight` are measured (they were 0).
**FIXED.** The words and arrowheads of an arc length dimension (DIMARC) and of an angle of three points are laid out as AutoCAD 2021
does where they only just fit, measured on its console (about 40 000 dimensions made by both): level words fit, and stand beside a
ray under DIMJUST 1 and 2, by what their box takes of the arc where it runs through their middle (their width and the arc symbol
as the sine of the direction of the arc's middle, their height as its cosine, each grown by a gap either side, and a gap at either
end of the arc: the arc of 3 through 40 degrees from 10 put 3.6 out stands them 0.013 nearer their ray than AutoCAD, now within
0.001; 262 of 408 such pictures differed, now none, and 108 of 144 of aligned words above the arc, which leave no gaps round
them, now none); the room for the words between the arrowheads is what is left of the sweep once each arrowhead has taken its
turn of the arc (arcs of 1 through 90 to 165 degrees: the distance that first takes both ran 0.08 too far out, now within 0.01);
where the words and the arrowheads each fit but not both, the ones that take more stay between the rays, and the arc symbol before
aligned words needs 0.15 more than the room both take (aligned words and DIMASZ 0.2 to 1: 282 of 1 900 differed, now 110); the
arc symbol over the words (DIMARCSYM 1) takes 0.3 of their height, not 0.4, and takes the arc out above them (the arc of 10 put
12 out, 3 024 pictures: 608 differed, now 410, of which 379 are the arc point of words in the middle, which AutoCAD sets either
way). An angle of three points fits its level words so (rays 10 long, the arc 12 out: 4 of 432 differed, now none; wide sweeps
of 90 to 165 degrees: 64 of 810, now 20). Every other picture (linear, angular of two lines, radius, diameter, jogged and
ordinate dimensions, DIMARC away from these cases: 68 sets of probes) is as it was.
**FIXED.** Arc length dimensions (DIMARC) and angles of three points in the presenter follow AutoCAD 2021 where the last round left them
(measured on its console, some 24 000 more dimensions made by both). A sweep counts as 90 degrees, and its extension lines run from the
centre, from a millionth of a radian short of it: the arc of 1 from 0 to 1.570796 radians (0.33 millionths short, as a console makes an
arc of 90 degrees from angles rounded to six decimals) put 0.8 out has its arc point on the circle of 0.8, at 30 degrees, where it was
taken for an arc under 90 (60 of 240 pictures differed, now none; the radial sets of arcs of 1 and 3, 216 of 1 428 and 358 of 1 632,
now 13 and 64). Level words off the arc (DIMTAD 2
and 3, arc length and angle) fit by their width, the symbol before them and two gaps times the sine of the direction of the arc's
middle plus their height, the symbol over them and two gaps times its cosine, with none of the gap at either end of the arc that
words on it keep (the old fit joined the two at 45 degrees and was 1 or 2 steps of 0.04 off; angles of three points 30 of 1 728
differed, now none; arcs of 10 put 12 out 587 of 3 456, now 197, 291 of 1 296 with the symbol over the words, now 73, and the
rest of both is the arc length text AutoCAD rounds in its fourth decimal), and they stand with the symbol clear of the arc too:
half the room of the symbol before them further out along the sine, half of what is over them along the cosine (the symbol before
words at 4 degrees stands them 0.165 further out). A dimension moved in along the middle of its arc and followed through a scale
reads its distance from the arc point it holds and the radius the arc has come to, as AutoCAD does: the arc of 1 through 80
degrees from 10 put 0.8 out has its arc point 0.8264 from the centre after a scale by 1.5 (it was 0.8346) and 0.8067 after one by
0.1 (it was 0.8000), and its arc is drawn 0.8067 out (72 pictures of scales 38 differed, now 1; 288 of them, 100, now 4). The group-10
"coin flip" of words in the middle is no rule of AutoCAD's: its pictures put the arc point of words exactly in the middle a third
of the way round, and the 948 rows of the first set and 410 of the second that differed were drawn 40 000 to 154 000 from the
origin, where AutoCAD's own float noise decides it (none of 556 rows within 40 000, a third of those 120 000 out); the same
dimensions drawn 30 apart differ in 158 and 73. Every other set of probes (77: linear, aligned, angular, radius, diameter, jogged,
ordinate, moved words, the other arc sets) is byte-identical.

**NEW.** Dynamic blocks, the grip edits of a polar parameter worked out one after the other, as AutoCAD 2021 works them (probed in its
console on side databases, grips through `MoveGripPointsAt`; about 1 000 random sequences on five kinds of pointer gave its lines to
1E-3). `DxfDynamicBlockReferenceProperty.MoveBasePoint` of a polar parameter no longer refuses a base grip dragged after the end
grip, or after the distance or the angle was set: the end grip to block point (0, 50), the base grip by (30, 0) and the end grip to
(35, 0) leave the first line, which the polar stretch does not hold at its start, from (45.43, 25.72) to (35, 0) and the tip from (35,
0) to (28.998, 9.432), where the lines worked out from the final values alone stand at (0, 0). Each edit turns the lines as they
stand about the base point as it stood then, and moves the points the stretch holds along the new direction by the distance it
gains; the library replays the INSERT's history of grip edits (`ACAD_ENHANCEDBLOCKHISTORY`) to do the same, the Length and the Angle
set between the edits and the Origin included, and refuses a base grip with `NotSupportedException` only where the INSERT keeps no
history that leads to where it stands (the distance or the angle set away from the block's own and the history taken out).
**NEW.** The `Origin` of a linear, an XY or a polar parameter can be set (`DxfDynamicBlockReferenceProperty.Value` of the hidden
property, a `GeoPoint3`), as AutoCAD 2021 sets it: the point is taken as a point of the file's coordinates and worked into the block's
(set to (1 030, 520, 0) on an INSERT at (1 000, 500) it reads (30, 20, 0); at a quarter turn and the scale 2 (10, -15, 0)), and the
parameter's base point and its end point move together to it, the distances and the angle kept: a panel 100 wide from (0, 100) whose
Origin is set to (30, 120) runs from (30, 120) to (130, 120) and the edges its stretches hold move by (30, 20), the base point's off
the line too (a linear parameter's base point may now stand beside its line), and the lines of a pointer stay. It was refused with
`NotSupportedException` before; the Origin of a rotation or a constraint parameter stays read only (`InvalidOperationException`).
**NEW.** `DxfDynamicBlockReferenceProperty.MoveEndPoint` drags the end grip of a linear parameter (the distance along its line) and of
an XY parameter (each distance along its axis) too, each taken to its value set, and the presenter's linear end grip and XY end corner
use it. In a block that keeps a history of grip edits it writes the `GRIPLOC` of the grip where a typed value would have written a
record of the property, and so does the base grip of a linear or an XY parameter (`MoveBasePoint`), as AutoCAD 2021 wrote them.
**CHANGED.** A stretch, a move or an array that follows the base point of a linear or an XY parameter now stays where the base point
stands at the next value set. AutoCAD 2021 kept the left edge of a panel whose base grip went 10 along x at x 10 when Width was set to
150 (right edge at x 160), in memory, after the drawing was written to DXF or DWG and read back, and from the library's own file; the
first circle of an array at x 60, and at 70 after 10 more; the attribute definition LOWER of a plate at (22, 2). The library put them
back at the block's own place at a value set, which AutoCAD did only from an anonymous block whose geometry was there.
**FIXED.** The history of grip edits (`ACAD_ENHANCEDBLOCKHISTORY`) as AutoCAD 2021 keeps it: a block with an array or a scale action
starts one too (a panel with an array, a grid, a bar a scale action scales; a block of stretches, moves, rotates and flips alone none);
a distance is kept in the block's own units (Width 150 of Width 300 on an INSERT scaled by 2, not 300); a `GRIPLOC` of a drag of no more
than 1E-12, or of two drags of one grip that bring it back, is left out, and so is the history a first drag of nowhere would start.
Not done: ATTSYNC of a moved INSERT could not be probed (AutoCAD 2021's console knows neither `ATTSYNC` nor `-ATTSYNC`, and the managed
API has no `AttSync`); a value set tie that AutoCAD broke by floating-point noise once the base
point was moved (Length 185 of 50 to 300 by 10 taken to 190, not 180).
**NEW.** `DxfDynamicBlockReferenceProperty.MoveEndPoint` drags the grip of a point parameter (the position goes to the point) and the
end grip of a rotation parameter (the angle of the point about the base point, from the base angle, taken to its value set), and the
new `MoveCornerPoint(GeoPoint3 to, bool takesXFromEnd)` drags the two corners of an XY parameter other than its end corner (the corner
takes one distance from the point and moves the base point along the other axis, the end point staying). In a block that keeps a
history of grip edits each leaves the one `GRIPLOC` of its grip, how far the grip went in the block's coordinates, where it left
records of the properties and of the base grip: a point grip dragged by (20, 10) keeps (20, 10, 0), a rotation grip taken a quarter
turn about its base point from (60, 0) to (0, 60) keeps (-60, 60, 0), a plate's corner of base x and end y dragged by (10, 5) keeps
(10, 5, 0) and reads Width 90 and Height 55, as AutoCAD 2021 wrote them (AUDIT 0 on five files written so). The presenter's point
grip, rotation grip and XY corners use them. The values after a drag are those the typed values and the base move gave.
**CHANGED.** `MoveEndPoint` on a point or a rotation parameter was refused with `ArgumentException`; it drags the grip now.
**FIXED.** A value set, base move or grip drag on a pointer reads its history of grip edits once, not twice (the walk before the set
and after it are the same): a polar Length set on a pointer with 15 edits behind it takes 0.88 ms, not 0.94 (6 %); the results are
bit for bit those of before (18 scenarios of hashed DXF text and 300 random edit sequences equal).
**NEW.** A drawing saved as R12 (`DxfDatabase.Save(path, "AC1009")`) holds the hatches and multileaders it breaks into as AutoCAD 2021
writes them. The INSERT of a hatch carries the hatch as R14 held it, in the extended data of the application `ACAD`: a pattern hatch
as `HATCH` with its name, scale and angle, what it follows (`ASC_BOUNDS`) and its seed points (`ASC_SEEDPOINT`) where it is
associative, then `R14_HATCH_DATA`; a fill or a gradient `R14_HATCH_DATA` alone: its handle, the axes of its system, elevation and
normal, name, flags, every loop with its lines, arcs, ellipses, splines or polyline vertices, the lines of its pattern and its seed
points. The lines and polylines a pattern hatch follows carry `ACAD ASC_BOUNDS` with the hatch's handle, the INSERT of a multileader
`ACAD_MLEADERVER` 2 (the application is in the table), and the applications a hatch's extended data names are in capitals. The 24
INSERTs of AutoCAD's sample drawing that carry extended data (8 hatches, 15 multileaders and a table's) are equal to AutoCAD's own R12
of it, group for group, and so are 14 of the 15 hatches probed in AutoCAD 2021's console (every kind of boundary, holes, a tilted
plane); the 15th differs by the last digit of a number.
**NEW.** Saved as R12, the polyline of an ellipse is written as AutoCAD writes it (its first vertex as the place of the polyline,
the layer before the handle, every vertex with six figures as C's `%#.6g`: `40.0000`, `1.53457e+06`) and the polyline of a spline with
its place the zeros `0.00000`; a MESH is the polylines of the edges its record lists, in its order and direction (a tetrahedron's were
read from its faces before); the lines of an MLINE come element by element; the pieces carry no turn of nought, no extrusion (0, 0, 1),
no flag 0 of a polyline or vertex, no linetype or colour by layer, as AutoCAD leaves them out; the BLOCK and ENDBLK of a block made
carry the current colour; the pieces of a pattern hatch are by block and those of a fill the linetype CONTINUOUS; a table's pieces are
by layer where its block drew them by block; and two names of one kind that clean to one (`A{1}` and `A_1_`) are told apart as
AutoCAD tells them: `A_1_0` and `A_1_`.
**CHANGED.** Saved as R12, what breaks in a block is an INSERT of an anonymous block of its pieces, as it is in a space (an MTEXT in a
dimension's block, a table's cells, a hatch, a multileader, a solid), where it used to be written in place: AutoCAD does so, and the
blocks are numbered on from the highest of the drawing in the order of the handles of the entities they were made of, those of the
spaces and of the blocks together (of the 66 INSERTs of AutoCAD's sample that AutoCAD's R12 has too, 24 have its block number and the
others one up to three above it: AutoCAD keeps three dimension texts, with a degree sign or a stacked fraction, as TEXTs of its own
regeneration and takes no number for them). An ellipse, a spline
or an LWPOLYLINE in a block is still a polyline in its place, and a ray or an infinite line in a block is a LINE of no length at its
base point, as AutoCAD writes them. The words of an MTEXT copied from a drawing (a table's cells, a dimension's block) are laid out
in that drawing's fonts when they are exploded, so that a table's cells are left-aligned TEXTs at the places AutoCAD puts them.
**FIXED.** Words of a linear dimension turned by DIMTEDIT Angle (or DIMLINEAR's Angle, DIMEDIT Rotate) that do not fit between the
extension lines are laid out as AutoCAD 2021 lays them (probed on 15 735 dimensions in its console, each drawn again from the library's
file: 6 404 differed, 377 now, all of the corners listed at the end). They fit by the reach of their box, grown by DIMGAP, along the line
(its width over the cosine of the turn and its height over the sine: "100", 5.28 wide and 2.5 high, turned 30 needs 7.55, turned 60 6.56,
turned 90 3.8), not by the chord the line takes through them (7.54, 4.39 and 3.8, which still breaks the line and decides the
arrowheads): with the arrowheads they need that reach, or the chord and two arrowheads and gaps, whichever is more ("12345.6789" turned
10 needs 25.8, turned 60 12.9). A DIMLINEAR of 100 at DIMSCALE 100 (0.18 sizes, "100.0000" 100.94 wide) with its words turned 30 has
them at (154.38, 30) outside the second line, centred on it, the arrowheads in (the chord of 72.8 had kept them between the lines);
turned 20, at (171.185, 30). Outside they stand on the line an arrowhead past its end (two where the arrowheads are out) and half the
chord on, level with the end on a slanting line (an aligned dimension of 4 at 30 degrees, "100" turned 60: (-3.436, 30.481)); under
DIMTMOVE 1 the leader runs square off the line to a level landing 6.53 long and the words turn about the middle of their start (4.911,
35.435 for "100" turned 30 on a line of 4), under 2 their middle stands 4.115 above the line; DIMJUST 3 and 4 put them on the extension
line, which is not run on; any DIMTVP but nought lifts them off the line (by DIMTVP times their height under DIMTAD 0, else by the
reach of their turned box across the line between the extension lines and by a gap and half their height outside); DIMTXTDIRECTION
turns them about the other end. Words moved and turned (DIMTEDIT to a point, then Angle) turn the arrowheads out within an arrowhead and
half their chord of an extension line, whatever their height (at 20 or 50 on a line of 100 at DIMSCALE 100, turned 30: both arrowheads
out on lines 18 long past 0 and 100, as AutoCAD draws them; the words keep the line whole where they are wider than it), and stand on
a level landing under DIMTMOVE 1. Counted against AutoCAD's redraw: plain turned words 0 differing of 2 400 (DIMTAD 0 1 2 4, DIMATFIT
0 to 3, DIMTIX, DIMTMOVE 0 to 2) and of 480 at DIMSCALE 100, 0 of 336 on slanting and reversed lines, 2 of 324 moved and turned on
level lines; every other path is unchanged (66 sets of earlier rounds, 144 044 pictures, byte for byte). Not done: turned words wider
than about 3.8 times (DIMEXE and their height) under DIMTMOVE 1, where AutoCAD draws no leader and keeps their middle at their point
(58 of 2 160); a DIMGAP below nought on words that fit exactly; words put beyond the end of a slanting line (their zone, and under
DIMTMOVE 1 a line run on to the landing); the words of a dimension whose words stand exactly on an extension line or exactly over the
middle (AutoCAD's choice of side there follows its rounding).
**NEW.** The ACIS data of R2000 (ACIS 4.00) is turned into the ASM data of the other versions and back when a drawing is saved as
another version of DXF (`DxfDatabase.Save(path, version)`), as AutoCAD 2021 writes it. Where a 3DSOLID, a BODY, a surface or a region
used to keep the SAT of R2010 in an R2000 file and, going up, to be left out of R2013 and R2018, it now comes out as ACIS 4.00 going
down (no `asmheader`, every pointer one lower, no history, no parameters or convexity on an edge, a text as `13 text`, no `full`
before an exact spline) and as the records of the target going up (the parameters of each edge worked out from its vertices and
curve, the convexity `unknown`, a vertex numbered 2 from R2007, an exact curve of ACIS 4.00 dated 20900 in R2010 and later). 35
bodies (boxes, a box turned about two axes, spheres, cones, a torus, wedges, a pyramid, extrusions, revolutions, lofts, a sweep,
booleans, regions with an arc and a hole, a BODY, and planar, extruded, revolved, lofted and swept surfaces) went from R2018, R2013,
R2010, R2007 and R2004 to R2000 and from R2000 to each of them: of the 175 going down 170 are the SAT AutoCAD writes of the same
solid in R2000 to the digit, of the 175 going up 130 are AutoCAD's record for record and 40 differ below 1E-12 in an edge's
parameter or a plane's normal, and AutoCAD opens the 910 files with the volume of each solid and `AUDIT` finding 0 errors. A solid
whose splines are another curve in ACIS 4.00 than in R2004 (a solid swept along a spline, a loft through circles of different
shapes, an extrusion of a closed spline) is left as it was, as before. The DWG writer writes the solids and regions of an R2000 DXF
into a DWG of R2018 now (they were left out).
**NEW.** A visual style going down keeps its round trip, as AutoCAD 2021 does: from R2013 to R2010 the extension dictionary with
the xrecord `ACAD_XREC_ROUNDTRIP` holding the 30 properties R2013 added (`RTVSPost2010Prop28` to `57`), and to R2007 and R2004 also
the flags of how each of the 63 properties is set (`RTVSPropertyOp0` to `62`); a style of R2010 going to R2007 is given the flags and
a style of R2007 going to R2010 loses them. A later upgrade finds the style as it was: 8 pairs of versions of two drawings (24 and 25
styles) come out group for group as AutoCAD's own.
**CHANGED.** A visual style going up from R2004 or R2007 takes how each property is set from its round trip (`RTVSPropertyOp0` to
`27`), where it had set them all: the three styles that give the edges no colour, no jitter and no overhang come out with every
property left to their parent (176 of 0) but their edge modifiers (2), as AutoCAD writes them, in R2010, R2013 and R2018.
**FIXED.** A solid that is turned (a transform with `rotate`) was refused by the SAT and SAB converters, and so left out of a DXF
saved as R2013 or later (from text), of one saved as R2010 or before (from the data store) and of a DWG of R2018; the flag is read and
written now (a box turned 30 degrees about z and 20 about x).
**NEW.** Annotative objects, fourth round (probed in AutoCAD 2021's console, plugin GHAQ of annot4; the library's drawings audited with
no error and read back to the same contexts). MLEADEREDIT on an annotative multileader makes each change (an arrowhead added, a leader
line taken out) in the context of every scale it is shown at as well as in its record, as AutoCAD does: a line added at (100, 200) to
"HELLO" at 1:50 and 1:100 is in both contexts, and a leader added on the right of the words lands against their right end at each scale
(the library's `AddLeaderLine` and `RemoveLeaderLine` still change the record's context alone).
A multileader of three leaders or more is shown at a second scale (it was refused): two or more leaders keep the base of the content
whichever sides they are on and each landing moves from it, as AutoCAD 2021 lays them out (probed with two on the left and with four, two
on each side: a base at (168, 150) stays at 1:100 and the landings left of the words go from 150 to 132, those on the right to 301.3179).
The angular dimension's context at a second scale has its arc point as AutoCAD writes it: a third of the way round the angle where the
words fit between the extension lines and two thirds where they stand outside, so an angle of 45 degrees with its arc 80 out has it at 15
degrees (77.27, 20.71) at 1:50 and at 30 degrees (69.28, 40.00) at 1:100 (36 angles from 20 to 120 degrees, two arcs and three positions
each, equal to AutoCAD's in both contexts). **CHANGED.** The presenter's MLEADER lands as AutoCAD 2021's does (its second point was where
the words stood): the leader line runs from the arrow to the second point, the dogleg goes on from it away from the arrow, and the words
stand past the dogleg and the gap, held at their left for a landing on the right of the arrow and at their right for one on its left,
their first line's middle level with the landing: an arrow at (100, 100) and a landing at (150, 150) in a style of a dogleg 0.36 and a gap
0.09 give a base at (150.36, 150) and words 0.18 high held at (150.45, 150.09), and at 1:50 a base at (168, 150) with the words at
(172.5, 154.5); the base of the content and the first point of the line are written as AutoCAD writes them, so that a second scale is laid
out from them as it should (the base was 4.4 short of it at 1:50), and a block's matrix holds its place (25.36, 10 for a block placed at
(25.36, 10)). The font is not measured, so the words' own height stands where AutoCAD adds the depth of their ink (0.0915 above the
landing for "HELLO" of 0.18, not 0.09). DIMARC words use DIMDEC as AutoCAD does (checked for DIMDEC 0 to 6 against DIMADEC, DIMAUNIT and
DIMLUNIT: only DIMDEC changes them, "314.159" at 3 and "314.1593" at 4). Not done: where the words of an angular dimension stand when
they go outside; the arrowhead on the other side of words in MLEADEREDIT where no font measures them (the line goes to the first leader).

**NEW.** The `Origin` property of each parameter of a dynamic block, as AutoCAD 2021 reads it (probed in its console on side
databases: 25 INSERTs, its values to the last digit, the library's read back with `AUDIT` finding 0 errors on all 25). A linear, XY,
polar, rotation and constraint parameter gives, after its own properties, a hidden property `Origin` in
`DynamicBlockReferencePropertyCollection`: its base point in the block's own coordinates (a `GeoPoint3`, not scaled by the INSERT),
`PropertyTypeCode` 12, no units, `Show` false, `ReadOnly` false for a linear, XY and polar parameter and true for the others; a flip,
a point, a visibility and a lookup parameter give none. A plate whose base point a grip moved by (20, 10) reads Origin (20, 10, 0).
Properties does not list it (it is hidden, as AutoCAD's palette does not). Setting it is refused with `NotSupportedException`.
`DynamicBlockReferencePropertyCollection` therefore has more items than before; filter on `Show` for the properties AutoCAD lists.
**NEW.** The base point of a polar parameter moved by its base grip, and the history of grip edits, as AutoCAD 2021 writes them.
`DxfDynamicBlockReferenceProperty.MoveBasePoint` moves the base point of a polar parameter anywhere, the end point staying and
the distance and the angle taken to their value sets: a pointer from (0, 0) to (100, 0) dragged by (30, 10) reads Length 81.39 and
Angle 354.3 degrees, and with distances of 50 to 300 by 10 and angles every 15 degrees dragged by (20, 15) Length 80 and Angle 345
degrees; the polar stretch then turns what it holds about the new base point (the line from (0, 0) to (100, 0) runs from (5.86, -14.14)
to (55.36, 35.36) once the end grip is dragged to 50 at 45 degrees from a base point at (20, 0)). `MoveEndPoint(GeoPoint3)` drags the
end grip of a polar parameter to a point, the distance and the angle from the base point taken to their value sets. A polar
parameter now lists its base grip always, after the end grip (a pointer lists (1 100, 500) and (1 000, 500)), and the presenter
drags it. The INSERT's history (`ACAD_ENHANCEDBLOCKHISTORY`) is started by the first value set or grip dragged of a block with a
polar parameter and gets a `GRIPLOC` record for each polar grip dragged, the node's id and how far the grip went in that drag in the
block's coordinates: the end grip taken to (0, 50) writes (-100, 50, 0), and on to (10, 0) one record of (-90, 0, 0). A record of
the same node and name as the last one is merged into it (a property's value replaced, GRIPLOC vectors added), as AutoCAD kept
Width 120 alone of 140 and then 120. A polar base point moved after the end grip was dragged, or the distance or angle was set, is
refused with `NotSupportedException`: AutoCAD worked those grip edits out one after the other (the end grip to (0, 50), the base
grip by 30, the end grip to (35, 0) left a line at (45.43, 25.72) where the library, working from the values, has (0, 0)).
**CHANGED.** The distances a base grip leaves are taken to the parameter's value set, and the base point stands where they put it,
the end point staying, as AutoCAD 2021 did: a panel of Width 50 to 300 by 10 whose base grip is dragged by 33 reads Width 70 with the
base point at 30, by 60 Width 50 (the minimum) at 50; a plate of Width 50 to 300 by 10 and Height 20 to 100 by 5 dragged by (33, 13)
reads 70 and 35 with the base point at (30, 15). `MoveBasePoint` left them as dragged before. An axis of an XY parameter the move
leaves alone is not taken.
**CHANGED.** The grip list of a dynamic INSERT, as AutoCAD 2021 gives it: the insertion grip is left out where a custom grip stands
on it (a point parameter at the block's origin gave one grip, a plate with a stretch of its base point 4 and not 5), and a linear
parameter's base grip is listed on a fresh INSERT too, where it was listed only once the INSERT had kept its values: the grip at the
insertion point of a panel whose left edge a stretch of the base point holds, dragged by 30, read Width 70 on a fresh INSERT.
**CHANGED.** The history of grip edits an INSERT keeps, when a value is set, keeps the last value alone where the same property is
set twice in a row (AutoCAD 2021: Width 140 and then 120 left Width 120, in one session or two), where the library added a record
for each.

**FIXED.** The words of an angle of three points and of an arc length dimension (DIMARC) are fitted and placed as AutoCAD 2021 does,
measured on its console (about 10 000 new dimensions made by both, besides the earlier sets): level words of an angle fit by what their box takes of the chord
across the arc's turn, as an arc length dimension's already did (rays 10 long, the arc 12 out, chords of 0.2 to 1.6: 56 of 432
pictures differed, now 4), and run out to the right where the arc point stands more than 0.05 above the vertex, else to the left
(a first ray from -3 to 5 degrees and from 180: 27 of 48 differed, now none). The room the words and arrowheads are fitted
against is the chord and a third of what the arc has beyond it, which counts from wide sweeps (the arc of 1 through 120 degrees
dimensioned on itself keeps its arrowheads between its rays: 66 of 1 428 differed, now 23). An arc length dimension: the arc
point goes to the far side of the words, two thirds round for words nearer the first end than the middle and a third for the
rest, so DIMJUST 1 puts it a third round (3.61 degrees, not 6.39, on the arc of 3 through 10 put 3.6 out: 288 of 864 differed,
now none); words between the rays under DIMJUST 1 and 2 stand an arrowhead and a gap from their ray, then half of what the fit
asked for along the arc (turned words within 0.001, level ones within 0.035: 57 of 108 differed, now 26); the arc symbol over the
words (DIMARCSYM 1) takes 0.4 of their height more (711 of 3 024 differed, now 656). An angle of 60 degrees whose words just fill
its chord (radius 30, text 4 high, DIMADEC 4) now keeps them between its rays, as AutoCAD does. Every other picture is as it was.
**NEW.** A 3DSOLID and a REGION saved as R12 are the edges of their faces, as AutoCAD 2021 writes them (probed in its console:
30 solids and regions made in side databases and written with `DxfOut` at R12, beside their SAT at R2000): face by face, loop by
loop, each edge once for each face that uses it, as a LINE from the start of the edge to its end carrying the normal of its face
where the face is flat ((0, 0, 1) left out), a CIRCLE or an ARC about the normal of a flat face and about its own where the face
is curved, an ellipse as the 3D polyline of 129 vertices, an edge of no curve (the apex of a cone) as a POINT, in an anonymous
block `*U` with an INSERT of the entity's handle (in place in a block). A box 40 by 30 by 20 is 24 LINEs, four to each of its six
faces; a cylinder four CIRCLEs; a cone two CIRCLEs and the POINT of its apex; a sphere an empty block; a region with a circular
hole the circle first and then the four lines of the outer loop; a region off the XY plane has lines that carry its normal,
(0, -0.644, 0.765) for 0.7 radians about X. 27 of the 30 solids and regions made for the probe come out as AutoCAD's own R12,
record for record (the other three hold an ellipse, whose 3D polyline AutoCAD starts at its first vertex and writes with six
figures), and so do the sample's two solids (24 and 30 lines) and its region (7). The ACIS reader gives the wires of any solid, not only of flat faces: a face on a cone, a
sphere, a torus or a spline is passed over to read its edges that are straight or elliptical. Edges that are splines are left
out of the lines and said (`3DSOLID 2`).
**NEW.** An MTEXT saved as R12 is wrapped by the fonts of the drawing: `DxfDatabase.Save` takes the saving drawing's
`FontSearchPath` to the MTEXTs it breaks into TEXTs, so that the long MTEXT of AutoCAD's sample, which was 2 TEXTs, is the 123 of
AutoCAD's own R12. For this an MTEXT of dynamic columns with one column (what AutoCAD 2021 writes for any MTEXT it wrapped) is
laid out as one of none, and a line feed among the words (`^J`) is a new paragraph, as AutoCAD's Explode takes it; the
`ActualWidth`, `ActualHeight`, `GeometricExtents` and Explode of such an MTEXT are measured where the drawing has fonts (the
sample's, 15.2606 wide, as AutoCAD wrote its group 42).
**NEW.** A dynamic block saved as R12 is the anonymous block AutoCAD writes for it, which R12 has in place of dynamic blocks: its
place in the block table kept, its name `*U` and the first number above the anonymous block before it (`my-dynamic-block` between
`*T16` and `*U18` is `*U17`, as AutoCAD's own R12 of the sample has it, where the first free number is taken if none comes before),
with its anonymous flag and the name in its INSERTs; said (`my-dynamic-block as *U17`).
**FIXED.** A drawing saved as R12 that AutoCAD 2021 discarded on opening it, found by opening the R12 of AutoCAD's sample and of its
ten dynamic block drawings (the sample and seven of the ten were discarded; now all of them open, AUDIT 0 errors for the ten, and
the one fault AutoCAD's own R12 of the sample has for the sample, 159 entities selected as in AutoCAD's own): the name of a
symbol is as R12 holds it, any character but a letter, digit, `$`, `-` and `_` an underscore and the name cut to 26 characters
(`{18b73a54-fa43-428c-adcb-c1c7bbdf2c84}` is `_18B73A54-FA43-428C-ADCB-C`, `Plates {1} of the second floor north wing` is
`PLATES__1__OF_THE_SECOND_F`), in the tables and on every entity that names it ("Improper table entry name"); the extended data
of AcDbAttr an attribute keeps its flags in has its application in the table ("Invalid application name in 1001 group"); a block
named anonymous has the flag of an anonymous block ("Invalid anonymous block"). The vertices and the end of a polyline made of a
lightweight one carry its linetype and colour, as AutoCAD's do (AUDIT mended "linetype != owner's").
**CHANGED.** The INSERT that stands in a space for an entity R12 breaks into its pieces is on the drawing's current layer, in its
current colour and linetype (`$CLAYER`, `$CECOLOR`, `$CELTYPE`; layer 0 and no colour or linetype where they are the defaults)
as AutoCAD writes it, where it took the entity's own, which are on the pieces; the INSERT of a hatch is of the linetype CONTINUOUS
with no colour and keeps the colours of its gradient (`GRADIENTCOLOR1ACI`, `GRADIENTCOLOR2ACI`, the indices of the first and the
second) in its extended data. A gradient hatch is still its SOLIDs of one colour: AutoCAD writes the triangles of its own display
mesh, uncoloured, 2 for a LINEAR gradient of any rectangle (as here) and 152 for a CYLINDER, 332 for a SPHERICAL, 56 for a
HEMISPHERICAL and 57 for a CURVED one on a rectangle 100 by 60, which is not worked out here.
**NEW.** LAYOUT and -LAYOUT Template and SAveas show AutoCAD 2021's dialogs in the window with FILEDIA 1 (designed first, Design
v145: InsertLayout, TemplateOptions). Template shows Select Template From File, then Insert Layout(s) (`CadInsertLayoutRequest`):
the file's layouts in the order of its tabs, the first picked, several picked with Ctrl or Shift; OK brings them in as the typed
names do, one undo step, Cancel brings nothing in. SAveas shows Create Drawing File (a .dwt, the AutoCAD 2018 DWG or a DXF version,
which the type gives with no question), and for a .dwt Template Options (`CadTemplateOptionsRequest`): the description, written
into the DWG's AcDb:Template section, and the measurement, English or Metric, as MEASUREMENT; Cancel writes nothing.
**NEW.** EXPORTPDF and -EXPORT write All layouts: one PDF of a page for each layout in the order of the tabs, each the size of its
own sheet and plotted by its own page setup, or by the detailed configuration answered Yes (`CadPlotBook`,
`CadPdfWriter.WritePages`); Layout1 on ISO A3 and a second on ISO A1 are pages of 1 190.5512 by 841.8898 and 2 383.937 by
1 683.7795 points. -EXPORT refused it before.
**NEW.** The plot stamp is drawn on PNG and JPG plots, at 96 pixels to the inch (0.2 inch letters 19.2 pixels high, 9.6 pixels in
from the corner), its line added to the log; on a plot upside down it turns with the plot, a stamp at the lower left standing at
the upper right of the sheet, as AutoCAD's help puts its corner relative to the image (`CadPlotStamp.Footprint` with the turn and
the sheet's units).
**CHANGED.** The application menu has Import (Other Formats, IMPORT) after Save As, and Export lists AutoCAD 2021's entries that
have a command here in its order, DWF, DWFx and PDF, PDF now running EXPORTPDF instead of PLOT, then PNG, JPG, BMP, Block or
objects to a DXF and Other Formats; a click on Export runs EXPORTPDF (design AppMenu, AppMenuImport).
**CHANGED.** LWDEFAULT refuses a width that is none of AutoCAD's 24 with `Cannot set LWDEFAULT to that value.`, AutoCAD 2021's
words in its console (`Invalid lineweight value.` before).
**FIXED.** The DWG container of R2004 and later read the template section of an R2010 to R2018 file as bytes where its description
is UTF-16, so that a metric drawing's MEASUREMENT read 256; it reads 1 now, and the description is read as AutoCAD writes it.
**FIXED.** Dimension pictures, tenth round (each checked against AutoCAD 2021 in its console, 17 000 probes of a radius, a
diameter and an angle among them): level words (DIMTAD 1 and 4) put inside a radius or a diameter under DIMATFIT 3 keep to
the line only where it has the room, two arrowheads and, as it leans from upright, the width of the words and a gap times
the cosine of its angle (the words' height more on a line that runs down), a diameter its whole length; else they stand above
(below) a landing an arrowhead, a gap and the words long, "R8" put 4 out at 30 degrees with the words 1.9 above a landing
7.161 long (the pictures of a set of 1 584 differing 99 down to 15, the 15 being diameters of 6 put out past the rim, and of
1 024 DIMTMOVE 1 and 2 ones 36 down to 8, which are DIMTMOVE 2 words behind the centre whose arrowhead we put outside); words
behind the centre under DIMTMOVE 1 stand above a landing the width of the words and a gap long. The words of a jogged radius
whose box straddles the rim leave the line a box all on their own side of their point, not about it ("R20" of a circle of 20
put 21.795 out: gone from 21.795 to 29.295, not 18.045 to 25.545; 18 pictures of 288 differing down to 0). The leader of
an angle's words put off its arc under DIMTMOVE 1 is drawn past a band that follows the direction the words are put in (6.8
at 30 degrees as before, 7.3 out and 6.8 in level, 3.8 out and 2.1 in straight above the vertex, for "60°" 2.5 high; it was
one band of 6.8 for all directions): 74 of 712 pictures differing down to 15, 584 of 2 938 to 49, 941 of 5 832 to 159 and
160 of 2 346 to 29; words exactly above or below the vertex stand above their point without a leader and under a landing
run under them with one, as AutoCAD draws them. Not done: the band's step of a third of a landing where the point crosses the
height of the angle's arc middle, the wide diameters put out past the rim, the arrowheads of DIMTMOVE 2 words behind a
circle's centre, and a jogged radius with its words exactly on the rim.
**FIXED.** DWG written by `DwgWriter` and refused by AutoCAD 2021 (`ErrorStatus 53`, a filer error), found by writing
AutoCAD's own 113 sample drawings again and opening each. The 2D polyline vertex of R2010 and later puts its id between
its bulge and its tangent direction; it was written after the tangent and not read at all, so a vertex with a tangent
(`Existing Conditions Plan.dwg` and four more drawings hold polylines AutoCAD curve-fitted, a tangent of 90 degrees) was
refused, and the tangent of a vertex AutoCAD wrote was read as 0. An attribute of a DXF of R2007 has one group 280, the
lock of its position, where R2010 and later have the version (0) as well before its tag; the lock was written as the
version, and an attribute with its position locked was refused (`Mechanical - Multileaders.dwg` and two more). Of the 113
drawings read and written again 106 now open with `AUDIT` finding 0 errors and 7 with as many as the original (24, 24, 2,
2, 32, 32, 10); 8 were refused before.
**FIXED.** Three DWG rewrites that AutoCAD 2021 refused or found errors in, each reproduced by a test that was red first. (1)
`splines_AC1024` written as R2018 was refused (a modeling error, code 75005, `ErrorStatus 53`): the SAB made of the SAT of its
two solids, a closed spline extruded and a loft, held two wrong things, found by setting the records against the SAB AutoCAD
saves of the same drawing: the parameter curve of a face (`pcurve`) ended in two whole numbers where the SAB has two reals,
and the surface of a loft (`loft_spl_sur`) lacked the whole number of 0 that the binary form of R2013 and later holds after
its flags. Each solid is now AutoCAD's own SAB byte for byte (the date apart), the loft is read back from AutoCAD's SAB
into the SAT of R2010, and the drawing opens with `AUDIT` finding 0 errors. (2) The drawing of tables written as R2018
gave `AUDIT` 6 errors (`AcDbTableStyle(7B) was not repaired`, as AutoCAD's own file of it and its own save of it do): the
style was made in code with no text style in its data, title and header rows; such a row is written with Standard. (3) A
DXF of R2004 or before written as a DWG kept `\U+1EDB` in the name of a layer, which AutoCAD found "Un-normalized" (the
drawing of properties, 2 errors); the escapes of such a DXF are the letters in every text now, as AutoCAD reads it, and an
escape typed in a DXF of R2007 and later is left. Of the 71 drawings of the tests rewritten 71 now open with `AUDIT`
finding 0 errors (64 before); of AutoCAD's 120 sample and template drawings 113 do and 7 have the errors of their
originals, as before.

**FIXED.** DWG tables that hold a field or a data link: the reader refused them (AutoCAD gives the flag of linked data
as 3, not the 1 of the specification) and `Cover Sheet`, `Title Sheet`, `T-01`, `Mechanical - Data Links` and
`Data Extraction and Multileaders Sample` lost their table, and AutoCAD the 60 fields a table of `Cover Sheet` owns. The
cell of a field gives it in group 344, the TABLECONTENT lists the fields the table owns (92 and 360) and a linked cell
its data link, rows and columns (92, 340, 93, 94, 96); written as R2018 the table keeps them, and AutoCAD's own DXF of
the 113 drawings written again has the 26 tables, 18 table contents and 5 data links of the originals, and the fields of
their tables (2 598 of the originals' 2 646 fields: the 48 in the dynamic blocks of eight sheet drawings are still lost).

**NEW.** DWG: the TABLECONTENT and the DATATABLE (ACDBDATATABLE in a DXF of R2000) a table of AutoCAD 2000 and 2004 keeps
for a round trip are read, as AutoCAD's DXF of the same table gives them: before R2007 a value is its type and value
alone, and the colours of a table content are the long form of R2004 even in R2000. Written as R2018 the DATATABLE is left
out, as AutoCAD does, and the table keeps its TABLECONTENT.

**NEW.** Annotative objects, third round (probed in AutoCAD 2021's console, plugin GHA3): arc length and jogged radius dimensions
are annotative as AutoCAD writes them (an arc length one the angular context class with the point of its arc, a jogged one
`ACDB_RADIMLRGOBJECTCONTEXTDATA_CLASS` with the chord point, the centre it is measured from and the jog) and get a second scale
with a picture of their own; a dimension whose words DIMTEDIT moved or turned is no longer refused: 294 is 0 where the words were
put and 140 their angle in radians, the words stay where they were put at every scale (a DIMLINEAR of 100 with its words at
(70, 350) has them there and its arrowheads, 18 long, outside the extension lines at 1:100), and the points of a context follow
the record (the dimension line's point was left at 330 after DIMTEDIT moved it to 350). A multileader of two leaders, one on each
side of the words, or holding a block, is laid out again at a second scale (the base stays and both landings move, a block's scale
and the diagonal of its matrix are multiplied), and one with its words left of its leader keeps its landing with the words back
from the dogleg's end; all to the last digit of AutoCAD's own (a base of 118 at 1:50 with landings 100 and 170.3547 is 118 with
82 and 222.7094 at 1:100); a multileader of three leaders is still refused. DIMBREAK breaks the picture of every scale: between two
points at the same points, at an object by the break size over the scale (6.25 at 1:50 and 12.5 at 1:100, not 0.125); an angular
dimension of an annotative style carries `DSTYLE {40 0.0}`; an INSERT of an annotative block made by -INSERT is shown at CANNOSCALE
from the start (factors 50 for 1 at 1:50, a context of 1:50) and each attribute gets a context (point 525 at 1:50, 550 at 1:100).

**NEW.** The DWG reader and writer take four more types of AutoCAD 2021's own sample drawings: the FIELD and FIELDLIST of
fields, the SORTENTSTABLE of the draw order of a block and the DATALINK of a table linked to data, which were left out and
counted among the types not read yet. A field is read with its evaluator, its code (over 250 letters in pieces under group 3,
as AutoCAD's DXF gives it), the fields it holds, its options and state, its own value and the values it keeps by name, in the
order of their names as AutoCAD's DXF gives them; a data link with its adapter, options, last update, the objects it fills and
its data map, which the file holds only where bit 8 of its options says so; a draw order table with the sort handle of each
entity. In the 96 sample drawings the 2 646 fields, 46 field lists, 256 draw order tables and 5 data links read as AutoCAD's
DXF of them (142 fields of files that AutoCAD 2005 saved lack the checksum its DXF adds, and the objects a data link fills
come in the order of their handles, not of AutoCAD's memory), in the DWG of R2000, R2004, R2007, R2010, R2013 and R2018. The
writer puts the four types back as AutoCAD does, object for object AutoCAD's own bits but the last bit of a real of 16
places in R2010, R2013 and R2018; it lists their classes where a DXF made in code lists none (a draw order made by
`CreateDrawOrderTable` was left out for that), puts the fields of a DXF of AutoCAD 2004 and before into the form of R2007,
and leaves out of a draw order the entities it does not write. AutoCAD opens the 13 drawings written so, `AUDIT` finding 0
errors. A table that holds a field or a link in a cell is still left out, with its TABLECONTENT, and so are the fields it owns.
The visual styles of a DXF of AutoCAD 2004, 2007 and 2010 are written too, which the writer left out with a Warn line: the 28
properties of the file and the 30 of the record `ACAD_XREC_ROUNDTRIP` of the style's extension dictionary are put into the list
of 58 AutoCAD 2021 writes for R2018 (the dictionary and its record, and the stamp `AcDbSavedByObjectVersion`, taken into the
style), and the 26 styles of each of five sample drawings, saved by AutoCAD as R2004, R2007 and R2010, come out as AutoCAD's own
bits, 390 objects. A DXF of AutoCAD 2000 gives no true colour, so those come with
the index alone. AutoCAD opens the drawings written so, `AUDIT` finding 0 errors.

**FIXED.** The DWG writer gives the owner of a block record that follows its table as AutoCAD does, one before the record
(handle code 8) where it gave the handle itself, and the handle of the block an attribute of a multileader names as a soft
pointer (code 4) where it gave a hard one (5). Of the 21 941 objects of the 18 drawings AutoCAD 2021 made and the 9 samples it
saved as R2018, read to DXF and written again, no object differs in the choice of a handle code from AutoCAD's own bytes any
more (5 did, 4 multileaders and a block record). What still differs is what a DXF cannot say: the last bit of a real of 16
places, the normal (0, 0, 1) that AutoCAD keeps whole in some entities, the extents of an MTEXT, the structure of a table, the
lineweight index 28 that some old files hold for BYLAYER, the plot style handle old files hold.
**FIXED.** DIMARC in the presenter lays the words of a small arc out as AutoCAD 2021 does when DIMTIH keeps them level
(probed on 48 arcs of 1 and 3 from 10 degrees, and on some 30 000 more by sweep, chord, direction, arrowhead and gap): they fit between the
extension lines by what they take of the chord across the turn of the arc, not by their width along it (the arc of 3 through 20
degrees from 10 keeps its words "1.0472" between the extension lines, an arrowhead outside each end; it put them outside
before, its arc point 0.35 off), and words outside stand past the first end to the right where it points up and to the left
where it points down, a gap and the room of the arc symbol from the landing (0.33 for text 0.18 high, where they stood that much
too near). A dimension put on its arc draws its extension lines in from the ends where two arrowheads fit between them and out
where they do not. An arc length dimension moved in along the middle of its arc and scaled until its arc point stands nearer
the centre than the arc (the arc of 1 through 20 degrees put 1.2 out, scaled by 5) is measured on that arc, as AutoCAD does,
and no longer reads the rest of the circle (29.67 for 1.7453). Of the 48 small arcs, 1 differs from AutoCAD's picture where 36
did (that one sweeps 120 degrees); the arc point of a dimension scaled until its words go from out to in still differs from
AutoCAD's by 0.001 to 0.065, which grows with the sweep and the scale. DIMARC words aligned (DIMTIH off) fit and stand as before, and angular dimensions
and every other dimension are drawn as before.

**NEW.** The DWG writer writes the solids of SAT text, which it left out: a REGION, 3DSOLID or BODY of a DXF of AutoCAD 2004,
2007 or 2010 is turned into the SAB AutoCAD 2021 writes for R2018 (`DwgSat.ToSab`, the grammars of the reader turned round),
named by the GUID its extended data kept (`ACAD_OBJECT_CHANGE_GUID`, taken out) or one made of its handle, and kept in the data
store of the file. Eleven solids (box, sphere, cone, cylinder, torus, wedge, pyramid, a plate with a hole, a union, an extrusion
and a revolve) and three regions of each of the three versions open in AutoCAD 2021 with `AUDIT` finding 0 errors, and each
solid has the volume, surface area and centroid of AutoCAD's own DWG of the drawing (the box 6 000 and 2 200, the plate
13 721.42 and 6 000), to the last digit. The ACIS 4.00 text of AutoCAD 2000 is still left out, with the surfaces. The preview
image is kept both ways: a DWG's preview comes as the section `THUMBNAILIMAGE` of the DXF (`DxfDatabase.ThumbnailBitmap`), in
all six versions, and a `THUMBNAILIMAGE` is written as the preview as AutoCAD 2021 lays it out (a header of 80 zero bytes, then
a bitmap or a PNG, in a page of its own at 0x120); AutoCAD gives a 96 by 64 bitmap back through `Database.ThumbnailBitmap`
pixel for pixel, and a broken preview no longer matters to the file. A record of a DXF that has no handle, which a DWG cannot
name, is counted with the types the writer leaves out (the Warn line of `DwgWriter`) where it vanished without a word. VIEWPORT,
HATCH, LEADER, MULTILEADER, MLINE, IMAGE and WIPEOUT written as DWG are AutoCAD's own bytes object for object but the last bit
of a double the 16 digits of a DXF cannot hold, polygon and polyface meshes and MINSERT too, and AutoCAD's DXF of each of 12
drawings we wrote is the DXF of its own DWG of them, with no difference.

**CHANGED.** `DxfColor.ByLayer.ColorValue` is white, 0xFFFFFF, as AutoCAD 2021's `Color.ColorValue` gives it (it was 0, black);
`Red`, `Green` and `Blue` of BYLAYER stay 0 and BYBLOCK stays black, as AutoCAD gives them. Colours compare as before, and the
presenter's colour dialogs already took BYLAYER as white.
**NEW.** PEDIT, JOIN, BOUNDARY and LIST follow-ups in the presenter, checked against AutoCAD 2021's console (probes of 13
November). PEDIT Multiple takes 3D polylines: a 3D polyline selected alone asks no question about lines, arcs and splines and
offers `[Close/Open/Join/Width/Fit/Spline/Decurve/Ltype gen/Reverse/Undo]`; Close, Open, Spline, Decurve and Reverse do to it
what they do alone (the 3DPOLY (0, 0, 0), (100, 0, 10), (100, 100, 20), (0, 100, 30) splined at SPLINETYPE 6 is 13 vertices),
Width and Ltype gen ask their questions and leave it, Fit leaves it, Join asks the fuzz distance and adds none. JOIN takes a
POLYLINE as it takes a LWPOLYLINE: a line joined into the POLYLINE PLINE drew at PLINETYPE 0 leaves it a POLYLINE at 0 and
makes it a LWPOLYLINE of its handle at 1 and 2 (a LWPOLYLINE stays one at 0), and the polyline JOIN makes of lines and arcs
picked at once, like the one -BOUNDARY makes, is a POLYLINE at 0 as PLINE's is (AutoCAD's console runs neither, so those two
follow PLINE).

**FIXED.** LIST of a dimension that overrides its style writes a number longer than twelve characters in scientific form, as
AutoCAD 2021 does (DIMASZ 1 000.5 at LUPREC 8 is `1.000500E+03`, minus 100.5 `-1.00500E+02`, six places and five for a
negative one; the scale-like overrides too; architectural and fractional units never), a linetype override naming none or an
object that is no linetype is `BYBLOCK`, and an override whose handle no object has, a text style of none or a block of the
handle of a layer is left out.

**FIXED.** The snaps Intersection and Extension on a feature control frame take the sides of its rows' rectangles alone, as AutoCAD
2021's console gave them (a line 0.05 above the middle of a frame and one at x 0.3 across it): a line through a frame meets the
sides of the rows (the top and bottom at (0.3, 0.18) and (0.3, -0.18), the left side at (0, 0.05)) and no wall between cells (none
at (0.45, 0.05), where the first wall is), and a wall meets no side. Before, they took the walls and the strokes of the symbols
too.

**NEW.** The base point of a linear or an XY parameter a grip moved, as AutoCAD 2021 works it (probed in its console on plates,
a grid and panels, its own results to the last digit, the library's INSERTs read back with `AUDIT` finding 0 errors).
`DxfDynamicBlockReferenceProperty.MoveBasePoint(GeoVector3)` moves the base point of the parameter a property is of, the end
point staying: a plate 100 by 50 whose XY parameter runs from (0, 0) to (100, 50), moved by (20, 0) with its Height set to 60,
reads Width 80 and Height 60 and keeps its right edge at x 100, where the corner of base x and end y dragged by (20, 10) read
Width 100 in the presenter before. The INSERT keeps the base point and every later value set, `UpdateAnonymousBlocks` and
`DxfBlockReference.GetDynamicGrips` work from there (Width then set to 120 takes the end point to (140, 60)); a stretch, a
move or an array that follows the base point follows the move being made, and `UpdateAnonymous` takes it to where the base
point stands. The three refusals of `Set`, `FollowedFromOwn` and `UpdateAnonymous` for a saved base point away from the block's
own are gone for a linear and an XY parameter that no move carries and no scale or rotate follows; a polar, rotation or flip
parameter's still is refused. The base grip of such a parameter is given by `GetDynamicGrips` where an action follows its base
point, after the end grip (a linear parameter's once the INSERT has kept its values). In the presenter, the two corners of an
XY parameter move the base point along the other axis, as AutoCAD's do, and the base grip of an XY or a linear parameter
drags it; a drag that would take a distance to nought is refused.

**FIXED.** A dynamic block saved from the Block Editor, and `DxfDynamicBlockRepresentation.UpdateAnonymous`, treat the attributes of
the INSERTs of its anonymous blocks as AutoCAD 2021's `UpdateAnonymousBlocks` does (a panel with TAG locked in a stretch and NOTE
free, probed in its console): an ATTRIB whose ATTDEF is locked and held by an action is made anew from the ATTDEF of the new *U
block, its words kept and its place, height, rotation, colour and the rest the definition's (a user's 4 high, turned 1 and
moved ATTRIB is 5 high, turned 0.3 at (1140, 515) after TAG is made 5 high, turned 0.3 and moved to (90, 15)); the other
ATTRIBs are left, none is made for an ATTDEF added and none taken away for one erased. Before, they were left whatever the
definition did.

**CHANGED.** The property of a `DxfDimension` that sets a variable of its dimension style (`Dimasz`, `Dimtad`, `Dimclrd`,
`TextStyle` and the rest) writes the override whether or not the value is the style's own, as AutoCAD 2021's does (DIMASZ,
DIMTXT, DIMSCALE, DIMTAD, DIMSE1, DIMCLRD, DIMLWD, DIMJOGANG, DIMTXSTY, DIMLTYPE and DIMBLK each written at the style's value);
a value set again over an override of the same value changes nothing. It was skipped where the dimension read the value
already.
**NEW.** The presenter's OBJECTSCALE shows AutoCAD 2021's Annotation Object Scale dialog over the annotative objects selected
(`CadObjectScaleRequest`): the Object Scale List with the paper and drawing units of the scale picked, all scales or those
common to all, Delete (greyed where an object would be left with none) and Add… with Add Scales to Object; three texts at 1:1
and 1:50, at 1:50, and at 1:50 and 1:100 list 1:1, 1:50 and 1:100, or 1:50 alone, and OK is one undo step. With no window it
asks -OBJECTSCALE's option. An Output ribbon tab after View: Plot (Plot, Page Setup Manager, Preview, Plot Stamp), Export to
DWF/PDF (Export PDF, Export) and Import. The Plot Stamp dialog picks its two user fields from a list kept by Add / Edit… (User
Defined Fields; `CadPlotStamp.UserFields`) and loads and saves AutoCAD's .pss plot stamp parameter files
(`CadPlotStamp.LoadParameterFile`, `SaveParameterFile`: the PIA file of a zlib stream, as AutoCAD 2021's mm.pss). The Boundary
Creation dialog's New asks `Select objects:` and comes back with Existing set, the areas then found among those objects alone.
The Xbind tree opens and closes its references and kinds. LWUNITS and LWDEFAULT are system variables (LWDEFAULT 33 says
`Invalid lineweight value.`), and Lineweight Settings starts from them.

**CHANGED.** The acad.pgp batch buttons sit in the ribbon's panel bodies where AutoCAD 2021 draws them, no longer in
slide-outs: Home › Draw's Hatch split (Hatch, Gradient, Boundary) under Rectangle and Ellipse, Point moved to Draw's slide-out
beside Divide, Measure and 2D Solid, so Home keeps its widths (no step at 2 300 pixels, 7 at 1 600); Annotate › Text's Check
Spelling and Text Align, Leaders' Align; View's Viewport Configuration in a Model Viewports panel of its own, which makes the
View tab full down to a window 794 wide (694 before).

**NEW.** A REGION clips a viewport, and MVIEW has its Object option (AutoCAD 2021's VPCLIP and MVIEW probed in its console, and
a ring plotted to a PNG). `DxfViewport.ClipTo` takes a REGION whose shape is read: the frame becomes the box of its loops and
the region stays the boundary with the viewport among its reactors; a ring (a circle of radius 40 less one of 15) makes the
frame 80 by 80 and AutoCAD plotted nothing of model space in its hole, a region of two squares apart two windows. The presenter
draws, picks and clips the grid, the image and the plot through every ring of the region, even-odd (`DisplayClip2.Others`,
`FlattenAll`), VPCLIP takes a region picked, and MVIEW Object (`Select object to clip viewport:`) makes a viewport of a closed
polyline, circle, ellipse, spline or region, its frame the box of the object (an open polyline says `Clip object must be
closed.`, any other entity `Invalid viewport clip object.`, the string of AutoCAD's resources). ROTATE and MIRROR of a
viewport that is not clipped work as AutoCAD 2021's do (they were refused): a mirror moves the frame's centre and keeps its
size and its view; a turn by a quarter turn (within a millionth of a radian) turns the frame, swapping its sides for 90 and 270
degrees, and the view; any other turn makes the viewport clipped to a new 2D polyline of its four corners turned, the frame
the box, the twist grown by the angle and the view's centre the turn by the new twist of the centre less the target (a
viewport 200 by 100 about (110, 60) showing (1 000, 500) at 1:10, turned 45 degrees about the origin, is 212.132 square about
(35.355, 120.208), twisted 45 degrees, its view centre (353.553, 1 060.660)).

**FIXED.** MIRROR of a copy of a clipped viewport and its boundary about a line that is not horizontal or vertical, where the
mirrored boundary has another box size (a triangle 80 by 60 becomes 60 by 80): AutoCAD 2021 moves the view's centre by the
step of the frame's centre from where the copy stands over the view's scale (a triangle mirrored about y = x - 50 shows the
view centre (86.667, -56.667) for (20, 10)); it was kept. A boundary that keeps its size keeps the view, as before. A click on
the boundary of a clipped viewport picked nothing where SELECTIONCYCLING is 2 (the default), since the boundary and the viewport
under it made a list of two to choose from: the pair is one object, as AutoCAD counts it. A viewport pasted (PASTECLIP) into a
layout never shown had no viewport 1 for the sheet before it, which AutoCAD's AUDIT answered with `Invalid linetype
eNullObjectId changed to ByLayer`; the layout gets it first, and AUDIT finds nothing.
**NEW.** Annotative dimensions and multileaders at a second scale, as AutoCAD 2021 writes them (probed in accoreconsole, plugin
GHAN4; our drawings audited with no error and read back to the same scales). A dimension drawn by an annotative dimension style
is annotative at the annotation scale of its space: DIMSCALE is that of the scale (50 at 1:50) and written as no override,
the arrowheads, text and gaps the style's paper sizes times it (0.18 is 9), its context `ACDB_ALDIMOBJECTCONTEXTDATA_CLASS` (radius,
diameter, angular and ordinate dimensions each their own class) naming its picture block and the middle of its words. -OBJECTSCALE
adds a context with a picture block of its own laid out again at that scale by the builders that draw a dimension afresh (a DIMLINEAR
of 100 has arrows 9 long inside its extension lines at 1:50 and 18 long at 1:100, the words out beyond the right one), a style change
draws every picture again, and the display draws the picture of CANNOSCALE or of a viewport's scale. The palette's Annotative row
makes a dimension of any style annotative as AutoCAD does: DIMSCALE 0 and each size over the scale as an override (0.18 at 1:50 is
0.0036), the picture kept. The Annotative box of the Dimension Style Manager's Fit tab writes the style's extended data (Modify, New and
Start With keep it), and a multileader drawn in an annotative multileader style is annotative at the annotation scale; at a second scale its
context data is laid out again (text 0.2 high at 1:50 is 0.4 at 1:100, the dogleg 0.36 is 0.72, the words 9.72 right of the landing).
An INSERT of an annotative block is drawn at the scale of the space, its attributes moved out from its point by the same factor. HATCHEDIT
and the Hatch dialog read and write Annotative (the Annotative box). ANNOAUTOSCALE (-4 to 4, kept by the presenter) adds the scale
CANNOSCALE is set to to the annotative objects of model space: 1 those on layers that are on, thawed and not locked, 2 those on locked
layers too, 4 those on layers that are off or frozen too.

**CHANGED.** Deleting the default scale of an annotative object makes the next context the default and rewrites the record from it, as AutoCAD
2021 does (`DxfDBObject.RemoveContext` refused the default before): a TEXT 125 high at 1:50 with 1:100 beside it is 250 high once 1:50
is deleted, an MTEXT is as wide as the context of 1:100, and a hatch's lines, an INSERT's factors, a dimension's block and a
multileader's context data are those of the new default. The entry added after a deletion is named one more than the highest number the
object has held within the session (after `*A2` was deleted, `*A3`), as AutoCAD names it. `DxfDBObject.AddContext` lays a multileader of
one leader holding text out at a second scale.
**FIXED.** Arc length and angular dimensions in the presenter are written as AutoCAD 2021 writes them (probed in its console on
48 arc length and 88 angular cases, then followed through moves and scales). The arc point of an arc length dimension of under
90 degrees put outside its arc is a third of the way between where its extension lines meet the dimension's arc, not a third
along the arc measured: the arc of 50 from 10 to 40 degrees put 70 out has its arc point at 21.45 degrees, where it was at 20 and
0.02 off (0.27 at 100 out of 60 degrees, 0.7 of 89). It goes two thirds of the way, not a third, where the words do not fit
between the extension lines, for an arc length dimension and for an angle of three points or of two lines (all 88 angles of 4
DIMATFIT, DIMTIX and widths match; the arc of 1 through 20 degrees put 1.2 out, in text 0.18 high, has it at 12.8 of its 20
degrees), and an arc length dimension scaled so that its words go in or out puts it again by where they stand now. A Partial
length writes the point further along the arc as its first (13) and the other as its second (14), with the parameters of the
points in the order given counted on from the arc's start (40 and 41: 6.632251 and 7.330383 for the part from 20 to 60 degrees of
the arc from 300 to 100 degrees), where both were the points as given; its ties and those of a whole arc count the end past a
turn (8.028514 for the end at 100 degrees of that arc) and tie the center at the arc's start, 5.235988 and not nought. Flag 128
of a radius and a diameter stays set through a move or a scale of the circle, as AutoCAD's 164 and 163 do (12 cases equal).
**NEW.** DIMARC dimensions the arc segment of a polyline picked on it, as AutoCAD 2021 does, tied to its segment by the marker:
StartPoint and Endpoint by the way the segment's path runs (a clockwise arc starts at its end vertex), parameters counted on from
the arc's start, the Center at the arc's start; the polyline moved, turned or scaled takes the dimension along (4 cases equal
AutoCAD's). A straight segment is "Object selected is not an arc or polyline arc segment.".
**CHANGED.** The grips of a dynamic block reference come in AutoCAD 2021's order, its custom grips first and the insertion
point's last (`CadGrips.Of`; a rectangle of an XY parameter at (1 000, 500): (1 100, 550), (1 000, 550), (1 100, 500), then
(1 000, 500)), where the insertion point came first and moved every index by one; `GripDrag`, `StartGripEdit`,
`CadDynamicBlocks.Click`, `ValuesOf` and `Choose` take the index of a custom grip from nought.
**NEW.** The object items of the canvas menus that have commands now are enabled and run them on the selection: Remove Style
Overrides of a dimension (DIMOVERRIDE Clear, the selection taken at the prompt the option opens), the spline menu (SPLINEDIT
with Fit data, Refine, rEverse, convert to Polyline, Join and Close as the item says), Viewport Clip (VPCLIP) and the image
menu's Adjust (IMAGEADJUST). 3D Edit Bar, Maximize Viewport, Shade plot, Set Origin, Set Boundary, Generate Boundary, Edit Block
In-place, Table Style and Export... stay grey, their commands not built.
**CHANGED.** Recent Input of the Command menu writes the points and numbers in the drawing's units (`100.0000,0.0000`, `@0.0000,50.5000`
at LUNITS 2 and LUPREC 4,), where it showed what was typed, and at Select objects lists
the sets answered, "Selection set (2 items)", newest first, each chosen selecting them again (`CadCommandSession.FeedRecentInput`).
'SETVAR, 'OSNAP, 'ORTHO, 'POLAR, 'GRID and 'SNAP run inside a command as 'PAN and 'ZOOM do.

**NEW.** The dialog items left from rounds 2 to 4 in the presenter, designed first (design canvas v142). The Viewports dialog's
Change view to: a click in a tile of the preview picks it and gives it *Current* or a named model view, written in the tile, and
OK shows the view in that new viewport, its centre kept and its size fitted, or in the tile as it is with *Active Model
Configuration*, all in VPORTS's one undo step; Visual Style lists the drawing's visual styles (2dWireframe as 2D Wireframe,
AutoCAD's internal ones left out), each still drawn as 2D Wireframe; Named Viewports gains Rename (`Viewport configuration
"Plan" already exists.`, `Invalid viewport configuration name.`). New Multiline Style's Fill color and Color lists end in
Select Color..., which shows Select Color (an index colour added to the list, a true colour kept as its nearest index, all an
MLINESTYLE holds), and Linetype... shows Select Linetype with BYLAYER and BYBLOCK first. The Hatch dialog of HATCH gains Click
to set new origin: it closes, `Specify origin point:` is asked, and the same dialog comes back with the point as Origin X, Y.
INSERTOBJ shows the Insert Object dialog (Create New with no object type to make, Create from File with Browse..., Link and
Display As Icon greyed). DRAWINGRECOVERY shows the Drawing Recovery Manager palette: the copies autosave keeps with their
drawing, time and size, and Open. Rename takes a wildcard Old Name: `S-*` to `A-*` renames S-BEAM and S-COLUMN to A-BEAM and
A-COLUMN, each wildcard's letters carried to the new name's, none renamed where one would be refused. Check Spelling gains Undo
(the last answer taken back) and Settings... (Check Spelling Settings: Block Attributes, and Ignore capitalized, mixed case,
uppercase, with numbers and with punctuation; Dimension Text and External References greyed). With no window each command keeps
its command-line form. New public members: `CadTiledViewports.SplitAs(name, whole, views)`, `ShowViews` and `Rename`;
`CadSpellSettings` and `CadSpellCheck.Settings`, `CanUndo` and `Undo`; the window's `InsertObjectViewModel`,
`DrawingRecoveryViewModel`, `BackupFileRow`, `CheckSpellingSettingsViewModel` and their windows,
`MainViewModel.ShowInsertObject` and `ShowDrawingRecovery`, and `SelectLinetypeViewModel(document, current,
offersByLayerAndByBlock)`. Tests: 17 new in the window's suite.
**NEW.** Annotative objects written as AutoCAD 2021 writes them (probed with accoreconsole; the library's drawings are audited by
AutoCAD with no error and read back to the same scales). `DxfDBObject.Annotative = True` now makes a TEXT, an MTEXT, an attribute
definition, a HATCH and a MULTILEADER annotative (it was refused): the extended data AcadAnnotative (a multileader its group 293),
an extension dictionary holding AcDbContextDataManager and ACDB_ANNOTATIONSCALES, and in it the default context `*A1` of the scale
CANNOSCALE names (1:1 in paper space); a hatch's pattern lines are multiplied by 1 over the scale, as AutoCAD does at 1:50 (the
offset 0.0884 becomes 4.42). A block can be made annotative by its own flag. New `AddContext(scale)` (AutoCAD's
`ObjectContexts.AddContext`, the command OBJECTSCALE Add) shows an object at another scale: a TEXT, MTEXT, ATTDEF, HATCH or INSERT of
an annotative block gets a context made from its default as AutoCAD makes it (an MTEXT 40 wide at 1:1 is 2000 wide at 1:50; a hatch's
lines multiplied and its group 40 of 50; an INSERT's factors 1, 50, 100, its attributes moved out from its point). New
`GetContexts()` lists the scales an object is shown at. A multileader, a dimension and the other kinds are refused a second scale.
In the presenter: the Annotative and Annotation scale rows of Properties, `-HATCH` ANnotative making annotative hatches,
`-OBJECTSCALE` (Add, Delete and `?`), `AIOBJECTSCALEADD`, `AIOBJECTSCALEREMOVE` and `ANNOALLVISIBLE`, in AutoCAD's words; TEXT and
MTEXT under an annotative text style are made annotative at the height of the sheet over the scale; and the display draws annotative
TEXT, MTEXT and hatches of a space at CANNOSCALE (1:1 in a layout, a viewport's own scale in it). **CHANGED.** `Annotative = True` on
a TEXT, MTEXT, attribute definition, hatch or multileader, and on a block, no longer throws `NotSupportedException`; on a dimension,
leader, tolerance or attribute it still does, with a new message. Tests: 12 new in the core suite and 11 in the viewer's.
**FIXED.** Dimension pictures against AutoCAD 2021 (probed in accoreconsole: 588 linear dimensions of words put by DIMTEDIT every
half unit across the first extension line, 4 680 more over DIMASZ, DIMTXT, DIMGAP and DIMDEC, 2 352 and 390 of the leader's
distance, 288 jogged radii): words of a linear dimension that reach within an arrowhead and the gap and their width of an
extension line are laid again as AutoCAD lays them. Under DIMTMOVE 0 they are moved out to 5.765 beside the line (a line 2.5 long
run back, the arrowheads inside), a point exactly on the first extension line goes to the far side of the second, and words on
the line between the extension lines turn the arrowheads out, the line whole unless a piece is left before the words; under 1
and 2 words so near leave the arrowheads and the line as they are, the line run on to words past an end only once they clear the
arrowhead's length. Words above the line leave the arrowheads inside. The linear sets that differed from AutoCAD (words near the
lines, 125 of 588) now agree on all 588 and all 4 680 (the second set of 672: 256 differing, now 14, all of a point exactly on an extension line, which AutoCAD decides by rounding noise). DIMTEDIT's leader under DIMTMOVE 1 appears a gap plus the larger of
an arrowhead and half the words' height with DIMEXE off the line, as AutoCAD draws it for any arrowhead, text, gap or extension
(it was a gap and half the height and half the arrowhead: wrong for arrowheads over 2.5). A jogged radius keeps level words
level and on the line under DIMTIH, breaking it where it runs through their box, and draws the stub from the back of the
arrowhead to the box's far end where the words reach past it (162 of 288 pictures differed, 18 now). The arc of an angle whose
words only have a corner clipped by it is broken where the circle meets the box, not up to the words' angle (2 of 112, 0 now).
Letters Arial lacks, a Han character and U+2205, are measured as AutoCAD sets them from another font (U+4E2D 2.7095 wide, 3.2821
high; U+2205 2.4454, 2.6927) where the canvas gives no measure, which fixes the height of their words. Still differing, parked:
words of an angle put past the end of its sector (the distance of the leader by their angle, 74 of 712), a jogged radius whose words are
moved to the jog (18 of 288), level words with DIMTAD 1 or 4 in a tight circle and under DIMTMOVE 1 behind the centre (99 of 1 584,
36 of 1 024). Tests: 23 new in the viewer suite.
**NEW.** Save As between the versions of DXF, the follow-ups to the first rules (`DxfDatabase.Save(path, version)`, `Save(stream,
version)`, `CadDocument.SaveAs`, `SaveCopy`), compared with AutoCAD 2021's own DXF of the same drawing in each version and opened
in its console. The ACIS data of a 3DSOLID, a BODY or a surface is turned from the SAB of the data store (R2013 and R2018) into
the SAT of the entity (R2004, R2007 and R2010; R2000 takes the SAT of R2010) and back, and from the SAT of one version of the
modeler into another's, by its records and not by its geometry: a box 30 by 20 by 10, a sphere, a cylinder, a cone, a torus, a
wedge, a pyramid, an extrusion with an arc and a taper, a revolution, a box with a hole and a loft come out of each of R2004,
R2007, R2010, R2013 and R2018 as the SAT or SAB AutoCAD writes of them in the others, record for record (220 conversions of 11
solids), and AutoCAD opens the 275 files of them in the six versions with the volume each had and `AUDIT` finding 0 errors, but
for the 22 that go from R2000 to R2013 and R2018, left out and said (an ACIS 4.00 text, which no later version writes, is not read;
nor is a solid made by a sweep, whose surface AutoCAD writes another way in each version). A 3DSOLID gets the history R2007 and
later write (`AcDb3dSolid`, 350) and a GUID (2) that names its record in the data store, taken from its
`ACAD_OBJECT_CHANGE_GUID`. A table of plain text cells is given the form of the version it goes to, the cells of R2004 and before
(groups 171 to 178, 145 and the text) or those of R2007 and later (with their flags and `CELL_VALUE`): 30 conversions of a table of
4 rows by 3 columns with merged cells between the six versions have the cells AutoCAD writes, and the round trip of the earlier form
(`ACAD_XREC_ROUNDTRIP`) goes going up; a table with anything else in a cell is still the INSERT of its block. The visual styles of
R2007, R2010 and R2013 are turned into one another's form (the 28 properties they share, the 30 R2013 added read back from the
round trip record of a style of R2010 and before, or the defaults), 21 of the 24 styles of a drawing exact in every direction and
the other 3 differing in the one flag R2007 does not hold. The columns of an MTEXT in the extended data of R2013 and before become
the MTEXT of R2018 with its embedded object, the MTEXTs of the later columns joined into it as AutoCAD joins them (a box of two
columns of 28 with a gutter of 4 is 60 wide). **FIXED.** A solid whose data is SAT going to R2013 or R2018 was written with its text
in the entity, which AutoCAD 2021 reads as no drawing (`Invalid or incomplete DXF input -- drawing discarded`); a solid of R2000 or
R2004 going to R2007 or later had no `AcDb3dSolid` (`Class separator for class AcDb3dSolid expected`, the same); a table with its
round trip going between R2000 and R2004 kept the name of its data table (`ACDBDATATABLE` or `DATATABLE`, the same). A table going to
R2010 or later has its data version (280). **CHANGED.** The visual styles of R2000, R2004 and R2007, one form, are kept going up
among them, where they were left out. A plane's normal that is a unit but for the last bit (0.9999999999999999) is written as it is in
the SAT of a DWG's solid, as AutoCAD writes it, and not divided by its length. Tests: 40 new in the core suite (`DxfSaveVersionFollowUpTests`).

**NEW.** CTAB in the presenter, as AutoCAD 2021's console took it (probed): `CTAB` and `SETVAR CTAB` ask `Enter new value for CTAB
<"Model">:` and show the layout named, in letters of either case, as one undo step named CTAB; Enter keeps the layout, and a name
no layout has, a pattern, `.` and `?` too, says `Cannot set CTAB to that value.` The undo step of a layout switch is named as the
command that made it, as U says it: TILEMODE for TILEMODE, CTAB for CTAB, LAYOUT for LAYOUT Set and a tab clicked. **CHANGED.**
MSPACE and PSPACE typed are undo steps (U takes each back to the viewport worked through before, or the paper), an empty one
where they only say `Already in model space.` or `Already in paper space.`, which they now do; in a layout with no viewport MSPACE
says `There are no active Model space viewports.` (capital M, as AutoCAD writes it) and is a step too. Find and Replace's Zoom to
objects shows the objects' layout as a tab clicked does, one undo step, and -VIEW Restore of a view of another space shows that
space as part of the command's one undo step. **NEW.** -XREF Unload and Reload of a reference another places, in the console's
words: Unload of the parent says each reference it places `orphaned` (`unloaded` where it was already), Unload of the nested name
alone unloads it and it is no longer drawn, Reload under an unloaded parent says `orphaned` and changes nothing, Reload says
`<path> has not changed.` or, where the file changed, `"x" loaded: <path>`, and `"x" has been restored.` where it was unloaded; a
reference no INSERT places is `unreferenced` to Reload and is refused by Bind as nested. **CHANGED.** The Attach and Reload lines
read `"x" loaded: <full path>` where they read `"x" loaded.`. **NEW.** -IMAGE of a drawing with no image says `No images in
current drawing.` at the option, a name no image has `No matching image names found.`, and -XREF Path and pathType say `No
matching xref names found.` before asking a path. Tests: 20 new in the viewer suite, one in the window's.

**NEW.** The dialogs left by earlier rounds in the presenter, designed first (design canvas v139): Multiline Style (MLSTYLE:
Styles, Set Current, New with Create New Multiline Style, Modify, Rename in the list, Delete, Load with Load Multiline Styles,
Save, Description and a preview drawn as MLINE draws, and the New/Modify Multiline Style page of caps, fill, joints and up to 16
elements), Multiline Edit Tools (MLEDIT: the twelve tools as pictures in AutoCAD's rows of four, the tool picked asked as its
typed option), Plot Stamp (PLOTSTAMP: fields, user fields, preview, Advanced Options) with the PLOT dialog's Plot stamp on box,
Image Adjust (IMAGEADJUST: Brightness, Contrast, Fade, preview, Reset; one undo step), the classic Attribute Extraction (ATTEXT:
CDF, SDF or DXX, Select Objects, template and output files) and Xbind (XBIND: the Xrefs tree, Definitions to Bind, Add and
Remove). The Hatch dialog of HATCH and HATCHEDIT gains a gradient of one colour with its Shade to Tint slider, the hatch and
background colours, layer, transparency, the hatch origin (Use current, Specified, Default to boundary extents, Store as default)
and the gap tolerance. MLSTYLE's buttons act at once in its one undo step and Set Current is taken on OK; a specified origin not
stored as default lays one HATCH's hatches alone. Each command keeps its command-line form where no window shows the dialog.
New public members: `CadMlineStyleRequest`, `CadMlineEditRequest`, `CadPlotStampRequest`, `CadImageAdjustRequest`,
`CadAttextRequest` and `CadAttextFormat`, `CadXbindRequest` and `CadXbindReference`, `CadPlotStamp.CopyFrom`,
`CadStylePreview.Mline`. The registry is unchanged (no new command).
**NEW.** FILTER, CAL and -VPORTS follow-ups in the presenter, in the words of AutoCAD 2021 (its resources and accoreconsole,
probed). FILTER's Select Filter list is AutoCAD's: kinds of object, properties and the begin and end of AND, OR, XOR and NOT
groups (`CadFilterProperty`, `CadSelectionFilter.Choices`); a number or a point is tested with `=`, `!=`, `<`, `<=`, `>` or
`>=` (a point by X, Y and Z each, `*` leaving one out: `Arc Radius >= 15.0000`, `Line End X >= 100.0000  Y * 0.0000  Z *
0.0000`), a group may hold groups, and Apply refuses lists that are not well formed with `Invalid Filter list - Begin/end clause
mismatch.`, `Premature end.`, `Empty clause/bad nesting.`, `Wrong number of XOR/NOT operands.` or `Maximum nesting exceeded.`; Add
Selected Object adds what belongs to the object's kind too (an arc's centre and radius). Named lists are kept in AutoCAD's
`filter.nfl` form (`CadSelectionFilter.ToNfl`, `LoadNfl`, `FilePath`, `Reload`; in the app `%AppData%\GeometryHelper\Dxf\filter.nfl`,
read each time FILTER starts), and a name AutoCAD's dialog refuses (`walls.v1`, `*unnamed`) is refused. CAL asks for what an
expression picks, in order: the snap functions `end ins int mid cen nea nod qua per tan` (`>> Select entity for END snap:`,
`Improper type of object picked.`, `No object picked.`), `rad`, `mee vee vee1 dee ille`, and has `vec vec1 nor dpl dpp plt pld ilp`,
`ang(apex,p1,p2)`, `abs` of a point and `cvunit(value,from,to)`; typed as `'CAL` at a prompt for a point or a distance it runs inside
the command (`>>>> Expression:`) and types its value at the prompt once `Resuming LINE command.` is written. -VPORTS MOde asks
`Apply viewport configuration to [Display/Current viewport] <Display>:` and then the options again; the answer is what the
Viewports dialog's Apply to starts with (`CadTiledViewports.AppliesToCurrentViewport`), and 2, 3 and 4 split the current viewport
in either mode, as AutoCAD's do (probed from one viewport to seven). Not built: the redefine prompt of a name in use.
**CHANGED.** CAL writes its value as AutoCAD 2021's command line writes it, whatever the units: `sqrt(16)+2` is 6 (it was 6.0),
`1/3` 0.333333333 (it was 0.333333), `[100,50]+[20,30]` 120,80,0 (it was (120.0 80.0 0.0)), and `2^3^2` is 64 (it was 512: AutoCAD
takes powers from the left); `tan` is the snap function, as AutoCAD's, and the tangent of an angle is `tang`; a word after a
complete expression is `Wrong syntax of expression`.
**NEW.** UNION (`UNI`), SUBTRACT (`SU`) and INTERSECT (`IN`) of regions in the presenter, in AutoCAD 2021's words (probed in
accoreconsole): `Select objects:`, for SUBTRACT `Select solids, surfaces, and regions to subtract from ..` and `Select solids,
surfaces, and regions to subtract ..`, `At least 2 solids, surfaces, or coplanar regions must be selected.`, `No solids, surfaces,
or regions selected.` and `Null region created - deleted`. The first region takes the answer in place, the others are erased,
and the result keeps the first's handle, layer, linetype and lineweight but is ByLayer in colour, as AutoCAD leaves it; each is
one undo step; of regions in several planes those of the plane most of them share are combined and the others stay as they are
(an elevated circle chosen with a box and a circle on the XY plane is left alone). A box 40 by 30 united with a circle of radius 10 on its right edge is one region of area 1 357.08 and perimeter
151.42 whose circle is still an arc, less it 1 042.92, two circles ten apart a lens of 122.84, a box and a circle far apart one
region of two faces (EXPLODE breaks it into a region each), a box less a circle inside it a face with a hole (1 121.46): the
areas, perimeters and number of regions of the 16 cases probed agree with AutoCAD's `BooleanOperation` to the sixth place.
Core: `DxfRegionShape.Unite`, `Subtract`, `Intersect` and `IsCoplanarWith`, `DxfUnknownEntity.SetRegionShape`, and a region of
several faces written as AutoCAD keeps it, one lump and shell for each face, in SAT and SAB for the six versions (AutoCAD
2021 opens them with `AUDIT` at 0 errors and the same areas). The library's booleans know only straight edges: each curve is
cut into chords (a circle on a grid of angles that depends on its radius, so that two regions sharing an arc leave no sliver),
the booleans are run on them and each edge of the answer is traced back to its curve, its ends the exact meeting of two
curves. The registry gains three commands and the defaults three aliases. The first region is the lowest handle where
AutoCAD takes the first picked, as the command line keeps a selection in the order of the handles.
**NEW.** A region is moved, rotated, scaled, mirrored and copied: `DxfEntity.TransformBy` and `GetTransformedCopy` of a REGION
write its ACIS data anew for the body it becomes (every edge and plane moved, the loops in their order, the normal turned over
by a mirror as AutoCAD's is: a region on the XY plane mirrored across x = 50 has the normal (0, 0, -1)), in the groups of the
entity or in its record of the data store; a transformation that scales more one way than another throws
`NotSupportedException`, as AutoCAD refuses it. A copy has a GUID and a record of its own, and one added to a drawing of
another version is written for it. MOVE, ROTATE, SCALE, MIRROR and COPY work on regions, and STRETCH moves the whole region
where a window holds one of its stretch points (the start of each curve of a loop, the centre of a whole circle), as AutoCAD's
`GetStretchPoints` and `MoveStretchPointsAt` do. A region still shows no grips, as AutoCAD's gives none. Regions moved, turned,
scaled, mirrored, tilted and copied, in each of the six versions, open in AutoCAD 2021 with `AUDIT` at 0 errors, and its
`Region.Area`, `Perimeter`, `Normal` and the curves of its `Explode` are the library's, line for line.
**NEW.** A drawing saved as another version (`DxfDatabase.Save`, SAVEAS, DXFOUT) has its regions' ACIS data read and written
again for the version: the SAT of R2010 goes to the data store of R2013 and back, R2013 to R2018, and the texts of R2000, R2004,
R2007 and R2010 into one another; the 90 regions AutoCAD 2021 wrote, each converted to the six versions, 540 files, keep their shapes
and AutoCAD's `Explode` of the converted ones equals the library's in every curve. A region whose body the library does not
read keeps going between texts and is left out going below R2013, as before. A DWG of R2000 to R2010 is still not written
(the DWG writer writes R2018 only).
**FIXED.** Regions AutoCAD 2021 wrote were read wrongly where it had moved, turned or mirrored them or made them by a boolean.
AutoCAD keeps a `transform` record beside the geometry for a move, a turn or a mirror, which the reader ignored: a box moved by
(100, 50) was drawn where it began. Its R2000 data of a boolean gives an arc the bounds of its whole ellipse and cuts it by the
two vertices of the edge, so a lens of two circles of radius 10 read as area 628.32 (not 122.84), a notch 485.84 (not 1 042.92)
and a box with a circle on its edge 1 114.16 (not 1 357.08). An arc of a loop that runs clockwise about the face was exploded
clockwise (normal (0, 0, -1)); AutoCAD's Explode gives it counter-clockwise about the face's normal, and a line whose loop runs
against its edge in the edge's direction. The regions of the probe, 744 of them in six versions, read and explode as AutoCAD's do.
**FIXED.** Tight radii and diameters, and their level words, as AutoCAD 2021 draws them (its own DIMRADIUS and DIMDIAMETER probed
over 40 000 circles of 3 to 30, DIMATFIT 0 to 2, DIMTAD 0, 1 and 4, DIMTOH and DIMTIH, the line at 0 to 360 degrees). What moves
out is decided by the room the words' box takes along the line, grown by DIMGAP: its width on a level line, its height on an
upright one, the length the box reaches along a slanting line where DIMTIH keeps the words level. Words on the line need the
chord of the line through the box and an arrowhead and two gaps to stay in with the arrowhead out, and three arrowheads past
the chord to keep the arrowhead in too: "R15" of Arial 2.5 high keeps its words in from a radius of 10.5 and its arrowhead in
from 14.74, "R12" has its arrowhead outside and its words in the middle of the line it leaves, 4.114 out at 30 degrees;
DIMATFIT 1 keeps the words in from the box alone, 2 keeps the arrowhead in from DIMASZ and two gaps and runs the line on from
the rim. A diameter on its line needs the chord and two arrowheads and two gaps (11.86 for "%%c6" of 4.357), and level words
moved out stand on a landing from the rim and two arrowheads. Of 6 336 tight radii and diameters probed under DIMTIX, DIMTMOVE
and DIMATFIT 603 differed before, 60 do now (the points exactly on the rim, the lines exactly 15 degrees from level and level
words of DIMATFIT 3 inside a tight circle are left); of 41 308 in nine threshold sweeps some 8 400 differed, 332 do, each a
line exactly 15 degrees from level or the first probe of a run.
**FIXED.** Level words under DIMTAD 1 and 4 put past the rim within the room of an arrowhead stand on their landing, as under
DIMTAD 0 ("R8" put 10.4 out at 30 degrees under DIMATFIT 3 stands 16.402 along its landing from 13 out), unless the line runs
within 15 degrees of upright. A radius picked at 30 degrees under DIMTMOVE 1 with level words, the point put 150 degrees round,
runs its line on through the centre to the height of the point and stands the words level there, group 11 the end of the line
(176 of 1 024 such probes differed, 36 do).
**FIXED.** The leader of an angle's words under DIMTMOVE 1 appears where AutoCAD 2021 draws it: once the end of its landing is
1.405 and 0.137 heights of the words off the arc as it leans, the arrowhead, the gap and half the words counting for 0.866 of
their length: 6.6 off for "60°" of Arial 2.5 high, DIMASZ 2.5 and DIMGAP 0.625, 8.8 for "60.00°". Fitted over 5 800 probes in
26 styles to 0.05 of a unit (to 0.1 for wider words), where the old fit missed by 0.25 at a gap of 1.5.
**NEW.** DIM on an arc, Angular: asks "Specify angular dimension location or [Radius/Diameter/Mtext/Text/text aNgle/Undo]" as
AutoCAD 2021's DIM does, and Radius or Diameter go back to the arc's radius or diameter prompt with the words given kept.

**NEW.** OLE objects in the presenter. A frame that holds a bitmap, a pasted picture or one INSERTOBJ made is drawn as its picture
on the canvas, in a plot and in a PDF (one 40 by 20 holding a bitmap red on the left and blue on the right plots red and blue; at
the plot quality Monochrome, black and white), with its frame over it as OLEFRAME says (0 none, 1 shown and plotted, 2, the
default, shown and left off a plot); an object of another application, a metafile or a damaged one is drawn as its frame with its
kind written in the corner. A frame has four corner grips that scale it about the corner across, as AutoCAD 2021's grips do (the
corner of 40 by 20 dragged to (50, 50) makes it 60 by 30 with Lock aspect on), a Properties group OLE (Position X and Y, Width,
Height, Scale width and height in percent, Lock aspect, Type, Plot quality, Source application), and keeps upright under ROTATE
and MIRROR about any line as the GUI keeps it, the centre going round the base point. INSERTOBJ (`IO`) is the Create from File of
the Insert Object dialog for a bitmap or a PNG, a static picture at the upper left of the view, one undo step; PASTECLIP and
PASTESPEC paste a picture on the Windows clipboard the same way where it holds no objects and no text. OLEFRAME, OLEHIDE
(frames of one space or both, in the display and the plot) and OLEQUALITY are variables of the document, kept while it is open.
The registry gains one command and three variables, and the defaults one alias. Create New, Link, the documents of other
applications, OLESCALE, OLERESET and OLEOPEN are not built.
**NEW.** Round 3 of the acad.pgp commands in the presenter, in the words of AutoCAD 2021 (its resources and console, probed in
accoreconsole): IMAGEADJUST (`IAD`) and -IMAGEADJUST set the brightness, contrast and fade of images ("Select image(s):", `Enter
image option [Contrast/Fade/Brightness] <Brightness>:`, `Enter brightness value (0-100) <50>:`, `Value must be an integer between
0 and 100.`; Enter offers the image's own value for one image and the factory's for several, an image on a locked layer is left
as it is), ATTEXT (`DDATTEXT`) and -ATTEXT write the attributes of the blocks to a comma-delimited, space-delimited or DXF extract
file from a template of `BL:` block fields and attribute tags (`NAME C012000`, `PRICE N008002`), record for record as AutoCAD
2021's console writes them with `** Field overflow in record 1`, `** Bad numeric value for field PRICE, record 3` and `5 records
in extract file.`, and the variable CTABLESTYLE (`CT`) makes a table style current (`Cannot set CTABLESTYLE to that value.`).
Their Image Adjust and Attribute Extraction dialogs are in the entry above. The
registry gains ten commands (these four, OPEN, DRAWINGRECOVERY and the four quick views) and one variable, CTABLESTYLE, and the
defaults ten aliases.
**NEW.** OPEN (`OPENURL`, `DXFIN`) in the presenter: the Open dialog, or with FILEDIA 0 `Enter name of drawing to open:` on the
command line; DRAWINGRECOVERY (`DRM`) lists the copies autosave keeps beside the drawings and opens the one chosen; the quick
views QVDRAWING (`QVD`) and QVDRAWINGCLOSE (`QVDC`) show and hide the Drawings list, QVLAYOUT (`QVL`) and QVLAYOUTCLOSE (`QVLC`)
the Model and layout tabs (`MainViewModel.AreSpaceTabsShown`), without the thumbnails AutoCAD draws. RECOVERALL is not built.

**NEW.** `DxfUnknownEntity.CreateRegion(shape, database)` writes a REGION as AutoCAD 2021 writes it, and `GetRegionShape()`
reads one back. `DxfRegionShape.Create(outer, holes, tolerance)` takes the closed loops of lines, arcs, circles, ellipses and
polylines with bulges (the curves of a loop in any order, joined end to end), runs the outer loop counter-clockwise and each
hole clockwise, and gives `Area`, `Perimeter`, `Normal` and `Loops` (`DxfRegionLoop`: `IsHole`, `Curves`, `Area`,
`Perimeter`). The ACIS data is written for the version of the drawing: SAT text in the groups of the entity for R2000 (ACIS
4.00) and R2004, R2007 and R2010 (ASM 208, 212 and 215), each character but a space written 0x9F less it, and for R2013 and
R2018 SAB in a record of the section ACDSDATA (made, with the schemas, where the drawing has none), the entity naming it by a
GUID. A box 40 by 30 with a circular hole of radius 5, a stadium, circles, ellipses, a half ellipse, a loop of lines and an
arc, a triangle out of the XY plane and a rectangle drawn clockwise, twelve regions in a drawing of each of the six versions,
open in AutoCAD 2021 with `AUDIT` finding 0 errors, `LIST` giving the area and perimeter of the shape (1 121.46 and 171.42 for
the box) and `Explode` the curves `DxfUnknownEntity.Explode()` gives, every point and axis the same; written as a DWG of R2018 by
`DwgWriter` they open the same. The records follow AutoCAD's own for the same regions, line for line in the SAT and record for
record in the SAB, numbers compared to nine places as AutoCAD's circles carry errors of 1E-16: the numbering by ASM's order, the hole first and run
clockwise, a closed edge for a circle, the vertex numbers of R2007 and later. The regions AutoCAD wrote (R2000 to R2018, SAT and
SAB) read back to its areas and perimeters. `Explode()` of a REGION gives its curves, a hole before its outer loop, as AutoCAD
gives them (it refused before); a region whose body is not flat faces of lines and arcs of ellipses has no shape. A region is
not written into a DWG of R2000 to R2010 (the DWG writer writes R2018 only), and the data of an erased region stays in the section ACDSDATA until
AutoCAD saves the drawing again (it opens with `AUDIT` at 0 errors).
**NEW.** REGION in the presenter makes the regions: every circle, whole ellipse and closed polyline, and every set of lines, arcs
and open polylines whose ends meet two and two, a region of its own (`5 loops extracted.` `5 Regions created.` as AutoCAD 2021
says it of eight objects; a circle inside a rectangle is a second region, not a hole), as one undo step. The Region object type
of BOUNDARY and -BOUNDARY makes a region of each loop (`BOUNDARY created 2 regions`). A region is drawn by the curves of its
loops, LIST gives its `Area:`, `Perimeter:` and bounding box in AutoCAD's words (checked against AutoCAD 2021's LIST), AREA and
MEASUREGEOM Object give `Area = 1121.4602, Perimeter = 171.4159`, Properties names it Region and gives both numbers, and EXPLODE
breaks it into its curves.
**NEW.** The Shortcuts page of Global settings says what every command does: each of its commands (some 260) has a description
**NEW.** Multilines in the presenter followed up (R2.1), as AutoCAD 2021 does it (probed in accoreconsole): a grip at each vertex
of an MLINE, which moves the vertex as `MoveVertexAt` does (a segment that keeps its way keeps its cuts where they were, one
that turns is whole again), and STRETCH of the vertices inside its window. TRIM and EXTEND of a multiline: an edge counts only
where it crosses every element of a segment, the new end stands where the element that runs furthest still reaches the edge
and each element stops where it meets it (the edge (40, -60)-(60, 60) starts a multiline of scale 20 at 46.67, its top element
at 50); a piece trimmed out between two edges leaves two multilines and a closed one opens; a multiline is an edge for other
objects by its elements. OFFSET, FILLET, CHAMFER, LENGTHEN and BREAK refuse one in AutoCAD's words (`Fillet requires 2 lines,
arcs, or circles.`, `Object can't be broken`). MLEDIT: a pick on nothing asks again without a word, Enter before the first edit
is `2D point or option keyword required.`, a corner joint of a closed multiline or a tee whose stem is closed `Invalid
operation on a closed multiline.`, cuts and welds run from one segment to another, and the merged cross and tee of four
elements break the inner pair across the inner pair (90 to 110 and 95 to 105, AutoCAD's numbers). MLSTYLE typed gains Rename,
Delete, Load and Save (.mln). Library: `DxfMlineBuilder.MoveVertexAt`, `DxfDatabase.RenameMlineStyle` and
`DxfDatabase.SaveMlineStyleFile`, which writes a style as acad.mln is written. 4 core tests and 27 presenter tests, every
number AutoCAD wrote. Left: the Multiline Styles and Multiline Edit Tools dialogs (design first).
**FIXED.** `LoadMlineStyleFile` gave a description the space acad.mln writes before every value (STANDARD's read as " ").
**NEW.** -LAYOUT Template and SAveas (and LAYOUT, which now asks what -LAYOUT asks), as AutoCAD 2021's console does them, probed
with layouts made in a side database. Template asks `Enter file name:` (no extension: .dwt) and `Enter layout name(s) or [?]:`,
takes names parted by commas and wild cards, and brings each sheet in as the last tabs with its page settings, its viewport 1,
everything drawn in it and the layers, linetypes and styles it names; a name the drawing has becomes `Layout3-Plan` (the first N no
layout starts with). SAveas asks the layout and `Enter name of output file:` and writes a template of that one layout, model space
empty, as a DWG of AutoCAD 2018 (.dwt, .dwg) or a DXF (.dxf, after DXFOUT's settings).

**NEW.** A drawing saved with tiles of model space opens with them: the canvas shows the view of the current tile (its centre and
height, the height kept as AutoCAD keeps it) and the others their own, where it used to open fitted to the drawing's extents.
`CadView.ZoomToHeight` gives a view by its centre and height.

**NEW.** XBIND Block binds the layers, linetypes and text styles the block's entities use, as AutoCAD 2021 does: a line on WALL and a
text on DOOR in MYSTYLE come as `grid$0$WALL`, `grid$0$DOOR` and `grid$0$MYSTYLE`, where they came in under their own names.

**CHANGED.** LAYOUT asks AutoCAD 2021's words, the same as -LAYOUT: `Enter layout option [Copy/Delete/New/Template/Rename/SAveas/Set/?]
<set>:` with Set the default (it was New, and its list was New, Rename, Set and ?), and its messages (`Cannot find layout "X".`,
`Layout "X" already exists, not created.`, nothing said of a new layout, `Active Layouts:` for ?). The layout offered is the one
shown, the first tab where Model is shown (it was `<Model>`, which AutoCAD never offers).

**FIXED.** Deleting the layout that held `*Paper_Space` (Layout1 of a new drawing) left a DXF that AutoCAD 2021 refuses to open
("Missing Default entry *Paper_Space in SymbolTable:BLOCK_RECORD"); the layout that follows is moved into its place, and the DXFs
of deleted, copied and imported layouts now AUDIT with 0 errors. Deleting the only layout shown now shows the new Layout1 (it
showed Model), and a copied layout's own viewport is number 1, where it was 2 and showed model space across its sheet.

**FIXED.** With model space tiled, the other tiles were all drawn again at every pan and zoom of the current tile; each is now drawn
again only when what it shows changes (an edit, the selection, its own view or size). With four tiles over a drawing of 40 000
lines a pan or zoom step takes 7 ms where it took 87 ms.

**NEW.** The Shortcuts page of Global settings says what every command does: each of its 250 commands has a description
(`DIVIDE` "Points or blocks at equal parts of an object", `-PAN` "Pan by two points or a displacement"), where only 47 had
one, and a test keeps every new command described.

**NEW.** Four dialogs of AutoCAD 2021 in the presenter, designed first in the design canvas: Rename (RENAME, `REN`), Lineweight
Settings (LWEIGHT, `LW`), Check Spelling (SPELL, `SP`) and Boundary Creation (BOUNDARY, `BO`); each command keeps its
command-line form where no window listens. Rename lists Blocks, Dimension styles, Layers, Linetypes, Multileader styles, Table
styles, Text styles, UCSs, Viewports and Views with the items that can be renamed; Rename To renames in the list at once and
refuses a bad or taken name (`"DOORS" already exists.`), and OK makes every rename as -RENAME would, in one undo step.
Lineweight Settings sets the lineweight of new objects (CELWEIGHT), LWDISPLAY, the units of the list (`0.25 mm` or
`0.010"`), the Default width (LWDEFAULT, now drawn: a line of Default at 0.50 mm draws 0.50 mm wide) and the display scale
(half to twice the width on screen). Check Spelling checks the entire drawing, the space on screen or the objects picked,
with Ignore, Ignore All, Change, Change All and Add to Dictionary; the words changed are written once it closes, in one undo
step. Boundary Creation sets island detection and the object type that -BOUNDARY shares, and Pick Points asks the points.
New: `CadRenameRequest`, `CadRename`, `CadSpellCheck`, `CadBoundaryRequest`, `CadDocument.DefaultLineWeight`,
`GlobalSettings.LineweightsInInches`, `DefaultLineweight`, `LineweightDisplayScale` and `Lineweights`, and in the WPF
presenter the windows and view models of the four dialogs and `CanvasOptions.LineweightScale`.
**NEW.** Ribbon buttons for commands of the acad.pgp batches, each in its panel's slide-out so that the tabs keep their widths:
Home Draw ▾ (Hatch, Gradient, Boundary, Divide, Measure, 2D Solid), Annotate Text ▾ (Check Spelling, Text Align), Leaders ▾
(Align), Dimensions ▾ (Override, after Oblique and Text Angle) and View Views ▾ (Viewport Configuration, VPORTS).
**CHANGED.** RENAME no longer writes "The Rename dialog is not designed yet" before asking -RENAME's questions with no window.
**NEW.** HATCH completed in the presenter (round 2, R2.2), in the words of AutoCAD 2021 (probed in accoreconsole): -HATCH now
offers every keyword AutoCAD does, `[Properties/Select objects/draW boundary/remove Boundaries/Advanced/DRaw order/Origin/
ANnotative/hatch COlor/LAyer/Transparency]`, and Advanced adds Boundary set and Gap tolerance. draW boundary draws shapes point
by point (Close, Undo, Length; one inside another an island; the hatch not associative, as AutoCAD leaves it), remove
Boundaries takes the loops of the objects selected out (Undo, Add boundaries), Origin sets the origin (this hatch's or the
default) or lays the pattern from a corner or the middle of the box round the hatch, hatch COlor, LAyer and Transparency set what
a new hatch takes in place of the current ones (kept in `CadHatchSettings` as AutoCAD keeps HPCOLOR, HPBACKGROUNDCOLOR, HPLAYER
and HPTRANSPARENCY: new `Color`, `BackgroundColor`, `Layer` and `Transparency` properties), Boundary set looks for an area
among the objects selected, and Gap tolerance (HPGAPTOL, 0 to 5000) joins two loose ends that near with a straight edge so that
a square with a gap of 2 in a side is hatched at 3 and not at 1 (`CadBoundaryFinder.Find` takes the candidate set and the gap as
two optional arguments). ANnotative asks its question and says an annotative hatch is not made yet, as the library does not
write the annotation scale a hatch is shown at. The gradient of one colour: `Enter a gradient name or [?/Single color/Two
color]`, then the colour and `Specify a shade or tint value (0-1) <1.0000>:`, written as AutoCAD writes it (two colours, the
colour at 0 and its shade or tint at 1, group 452 of 1 and the value in 462); the end is the colour's hue and saturation at the
lightness of the value, which matches all 45 pairs of colour and tint AutoCAD 2021 was probed with (new `GradientOneColor`,
`GradientTint` and `OriginMode` properties of `CadHatchSettings`, and `CadHatchOriginMode`). The colour prompts of a gradient
are AutoCAD's, `New gradient color [Truecolor/COlorbook] <0,0,255>:`, with Truecolor asking `Red,Green,Blue:`.

**CHANGED.** HATCH draws the hatch behind its boundary, as AutoCAD 2021's HPDRAWORDER of 3 does (-HATCH did already, and
HATCH and -HATCH now share the choice DRaw order makes): a square of four lines hatched by HATCH has the hatch first in the draw
order, where it was last, over its lines. -BOUNDARY and BOUNDARY make no polyline of an area with an ellipse or a spline in it,
as a side or as an island, and say `Spline or ellipse detected. Cannot create boundary.`, as AutoCAD refuses it; they made
polylines through the 64 points of the curve before. HATCHEDIT keeps a hatch of one colour of one colour where it was, and its
tint, instead of making it of two.

**FIXED.** -HATCH with Retain boundary and a loop of an ellipse or a spline (from Select objects) kept an empty polyline for it;
it keeps none, a curve having no corners to make a polyline of.
**NEW.** SPLINEDIT completed in the presenter, in the words of AutoCAD 2021 (probed in accoreconsole, the splines met to 1E-5):
its menu is now `Enter an option [Fit data/Close/Join/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:`. Fit data
has Add, Close or Open, Delete, Kink, Move, Purge, Tangents and toLerance, each fitting the cubic through the points anew as
AutoCAD does (adding (15, 8) after (10, 10) to the spline through (0, 0), (10, 10), (20, 0), (30, 10) and (40, 0) gives the 8
control points and knots AutoCAD wrote; a kink at (20, 0) the knot 28.28 three times and a fourth 1E-8 past it). Join takes the
curves that meet an end, as JOIN does, saying `1 object joined to source`. Refine has Add (a knot at the nearest point), Delete
(a control point), Elevate order, Move and Weight (`Spline is not rational. Will make it so.`). Close and Open now work on a
spline of control points too: Close takes them as the points of one period with uniform knots, in AutoCAD's periodic form (the
cubic of 7 control points has 10 and the knots 0, 0, 0, 0, 1 to 6, 7, 7, 7, 7), and Open gives them back with uniform knots.
Not built: AutoCAD's fit tolerance fitted to (the curve still passes through every point), Elevate order past order 11
(AutoCAD's 26), and the picks of Delete and Select point in the console, which are by screen aperture and could not be probed.
**FIXED.** `DxfSpline.JoinEntity` and `JoinEntities` ran a line on over its length in knots; AutoCAD 2021's JOIN and SPLINEDIT Join
run it on over 1: a line of 22.36 joined to a spline of knots 0 to 56.57 ended at 57.57, not 78.93. The curve is the same, the
knots of the joined spline differ.
**NEW.** Multilines, R2.1 of the presenter's round two (AutoCAD 2021 parity; the prompts, messages and records probed in
accoreconsole): MLINE (`ML`), MLEDIT and MLSTYLE with the variables CMLJUST, CMLSCALE and CMLSTYLE, three commands and
three variables more in the registry. MLINE asks `Current settings: Justification = Top, Scale = 20.00, Style = STANDARD` and
`Specify start point or [Justification/Scale/STyle]:`, then `Specify next point:` with Undo from the second point and Close
from the third; the multiline is previewed as the points and the cursor make it and made when the command ends. MLEDIT asks
`Enter mline editing option [CC/OC/MC/CT/OT/MT/CJ/AV/DV/CS/CA/WA]:` and does the closed, open and merged crosses and tees, the
corner joint, a vertex added and deleted, a cut of one element or all, and the weld, leaving the records AutoCAD leaves
(the style of three elements, scale 1, through (0, 0)-(100, 0) crossed by (50, -50)-(50, 50): CC breaks the first's elements
from 50 to 51, OC also the second's outer ones from 49 to 50, a tee stops its elements at 49, the merged tee's middle one at
49.5; a corner joint runs the elements on past the end by 1, 0.5 and 0). AutoCAD 2021 has no typed MLSTYLE, so the presenter
asks its own: `[?/Current/New/Modify]`, then description, elements, caps, fill, joints and angles; STANDARD and a style a
multiline is drawn by keep their elements. The 29 multilines of a drawing made by these commands, caps, fills, tees,
corners, cuts, moves, scales, rotates and mirrors, open in AutoCAD 2021 with AUDIT finding 0 errors, and AutoCAD's own Explode
of each gives the same lines as ours, 224 of them, to the sixth decimal. New in the library: `DxfMlineBuilder` (`Build`,
`Rebuild`, from points, style, scale, justification, flags and normal, as AutoCAD writes an MLINE: STANDARD through (0, 0),
(100, 0), (100, 100) has its corner's miter at (-0.7071, 0.7071) and its second element at -1.4142; justified at the middle at
scale 20 the elements are 10 and -10), `DxfMlineJustification`, `DxfMlineStyleSettings`, `DxfDatabase.AddMlineStyle`,
`DxfMlineStyle.GetSettings` and `Apply`, and a public constructor of `DxfMlineStyleElement`. An MLINE is now moved by
`TransformBy` and `GetTransformedCopy` (a transformation that scales alike; its scale and cuts follow, a mirror keeps its
elements on the same side), where it was refused; `Explode` of one draws what a corner joint or a tee leaves past the last
vertex or before the first, which it dropped. The presenter draws an MLINE by its Explode.

**NEW.** Batch 5 of acad.pgp's commands in the presenter, output, in the words of AutoCAD 2021 (probed in accoreconsole):
EXPORTPDF (`EPDF`), EXPORTDWF (`EDWF`), EXPORTDWFX (`EDWFX`), IMPORT (`IMP`), PLOTSTAMP (`DDPLOTSTAMP`) with -PLOTSTAMP, and the
variable PDFSHX (`EPDFSHX`): six commands and one variable more in the registry. EXPORTPDF asks the window's Save As PDF dialog
and writes the current layout, or the display of model space, through the writer of -PLOT; with FILEDIA 0 it asks -EXPORT's
questions (the area, `Detailed plot configuration?`, the file). EXPORTDWF and EXPORTDWFX say the presenter has no DWF writer.
-PLOTSTAMP asks `Enter an option [On/OFF/Fields/User Fields/Log file/LOCation/Text Properties/UNits]:` and each option's
questions as AutoCAD's console does; while the stamp is on, the PDF of PLOT, -PLOT, EXPORTPDF and -EXPORT carries the drawing,
layout, date and time, login, plotter, sheet and scale, and two user fields, as text in the corner chosen, and a line of each
plot is added to the log file. PDFSHX 1 writes the words of an SHX text over its strokes in letters no eye sees (1 part in 255
opaque), so that a PDF reader's search finds them. IMPORT asks `Enter import file name:` (the window's Open dialog with FILEDIA
1) and brings a DWG or DXF in as a block, placed as INSERT places one; WMF and the other types of AutoCAD's list are refused.
New: `CadPlotStamp`, `CadPlotStampCorner`, `CadPlotStampUnits`, `CadPlotSettings.PlotStamp` and `PdfShx`,
`CadCommandSession.PlotStamp`, `CadExportJob.Opens` and `CadExportJob.Drawing`. Not built: the Plot Stamp dialog, the stamp on a
PNG or JPG plot, EXPORTPDF's Options and a PDF of all layouts.
**NEW.** Batch 4 (second part) of acad.pgp's commands in the presenter, the command-line forms and the window's own, in the
words of AutoCAD 2021 (read from its resources and probed in accoreconsole): -PAN (`-P`), -MTEXT (`-T`), -LAYOUT (`-LO`), -XBIND
(`-XB`), XBIND (`XB`), SAVE, -OSNAP (`-OS`), QUIT (`EXIT`), COMMANDLINE (`CLI`), COMMANDLINEHIDE, HIDEPALETTES (`POFF`) and
SHOWPALETTES (`PON`): ten aliases and twelve commands more (six in the registry of the commands, six of the window). -PAN
refuses Enter at its first question as AutoCAD does. -MTEXT asks the corners and options of MTEXT and then `MText:` line by line
until a blank one. -LAYOUT has Copy (the page settings and a copy of everything in the sheet), Delete (the layout, its block and
what it holds; the last sheet leaves a new Layout1), New, Rename, Set and `?` in AutoCAD's words, with Template and SAveas listed
and said to be not built. -XBIND binds the layers, linetypes, text styles, dimension styles and blocks an external reference
brought: `grid|WALL` becomes `grid$0$WALL`, `1 Layer(s) bound.`. SAVE asks `Save drawing as <file>:` and the drawing takes the new name,
as AutoCAD's console does. QUIT closes the window through the question about drawings not saved; HIDEPALETTES and Ctrl+Shift+H
hide Properties, the Drawings list and the command line and remember which were shown, SHOWPALETTES brings them back, the ribbon
stays; -OSNAP types the object snaps that are on (`END,MID`, `NONe`). New: `MainViewModel.QuitRequested` and
`MainViewModel.ArePalettesHidden`.
**FIXED.** Undoing the rename of a layout by letters alone (Layout1 to LAYOUT1) left its tab named LAYOUT1; the tab is renamed
back.

**NEW.** Batch 4 of acad.pgp in the presenter, the commands with a window of their own, designed first in the Design canvas
(ViewportsDialog, ViewportsNamed, ModelTiles, SelectionFilters, QuickCalc, QuickProperties) and worded as AutoCAD 2021 (its
resources and accoreconsole probes): VPORTS (`VIEWPORTS`), -VPORTS, FILTER (`FI`), QUICKCALC (`QC`), CAL and QUICKPROPERTIES
(`QP`), the registry growing by 6 commands, the default aliases by 4 and the keys by Ctrl+Shift+P (QPMODE). VPORTS and -VPORTS
split model space into tiled viewports: -VPORTS asks `Enter an option [Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde]
<3>:`, splits the current viewport as AutoCAD does (2 Vertical keeps the right half current, 4 the lower right; the new ones
numbered 3, 4, 5), each new viewport showing the whole of the view it came from, and Save, Restore, Delete, Join, SIngle, ? and
Toggle keep and bring back configurations in AutoCAD's words, each change one undo step; the Viewports dialog applies the
twelve standard configurations to the display or the current viewport and restores the named ones. The tiles are
`CadDocument.TiledViewports` (`CadTiledViewports`), read from the `*Active` records of the VPORT table and written back on
saving as AutoCAD writes them (corners 10/20 and 11/21, view 12/22, 40 and 41; a drawing never split keeps its table as it
was); the window lays the canvas over the current viewport and draws the others with their own views, a click making one
current. FILTER shows Object Selection Filters (Object, Layer, Color and Linetype lines, all of which must hold; Add Selected
Object; named lists for the session) and keeps what passes as the Previous set (`CadSelectionFilter`). QUICKCALC shows the
QuickCalc palette (history, number pad with memory, Scientific, Paste value to command line, and Get Coordinates, Distance,
Angle and Intersection picked in the drawing through CAL); CAL asks `>> Expression:` and evaluates numbers, vectors
`[x,y,z]`, `+ - * / ^`, the trigonometric functions in degrees, `ln log exp exp10 sqr sqrt abs round trunc r2d d2r pi`,
`dist`, `ang`, `ill` and `cur` (`CadCalculator`), writing `Error:` and AutoCAD's reason for a wrong expression. QUICKPROPERTIES
shows the Quick Properties panel (Color, Layer, Linetype, edited in place) near the cursor; QPMODE starts at -1 as AutoCAD
2021's, Ctrl+Shift+P switching it (`<Quick Properties panel on>`). Not built: -VPORTS MOde; the dialog's Change view to and
Visual Style; FILTER's AND/OR/XOR/NOT groups and value tests; QuickCalc's Units Conversion and Variables; CAL's object snap
functions; the CUI lists of Quick Properties and QPLOCATION; QPMODE kept between sessions.
**NEW.** CHANGE (`-CH`), RENAME (`REN`) and -RENAME (`-REN`) in the presenter, in the words of AutoCAD 2021 (read from its
resources and probed in accoreconsole). CHANGE takes a change point (the end of each line nearest it, which with ORTHOMODE on
keeps the line horizontal or vertical; the radius of a lone circle; the insertion point of a lone text or block) or Enter,
which asks each circle its radius, each text its insertion point, style, height, rotation and words, each block its insertion
point and rotation, and an attribute definition its tag, prompt and default value; the Properties option asks Color, Elev,
LAyer, LType, ltScale, LWeight, Thickness and TRansparency until Enter, each offering what the run set last, and asks Material
and Annotative without changing anything. An arc, a polyline or a point has no change point and CHANGE says so. -RENAME
renames a block, dimension style, layer, linetype, multileader style, text style, table style, UCS, view or viewport
configuration with every reference to it, as one undo step, asking the old name again for a wildcard or a name nothing has.
RENAME has no dialog yet (to be designed): it says so and runs -RENAME. Public helpers `CadBlocks.Rename`,
`CadLinetypes.Rename`, `CadMLeaderStyleEditor.Rename` and `CadTableStyleEditor.Rename` join the layer, dimension style and
text style ones. The three commands are added to the registry (218 commands) and the three aliases to the defaults. Not built:
CENTERMARK and CENTERLINE, whose geometry and DXF form could not be probed (the console cannot pick), and the colour of
DIMCENTER's lines.
**NEW.** A linetype of a drawing renamed by setting `DxfLinetypeTableRecord.Name`, with every reference to it by name: the
entities, layers and styles of multilines that draw with it, and the header's `Celtype`, `Dimltype`, `Dimltex1` and
`Dimltex2`; ByLayer, ByBlock and Continuous keep their names, and the name of another linetype is refused. A viewport of the
VPORT table can be renamed the same way, its one record.
**NEW.** Batch 3 of acad.pgp's 2D commands in the presenter, in the words of AutoCAD 2021 (read from its resources and probed
in accoreconsole): SOLID (`SO`), -BOUNDARY (`-BO`), -HATCH (`-H`), GRADIENT (`GD`), HATCHTOBACK (`HB`), REGION (`REG`),
SPLINEDIT (`SPE`) and CLASSICINSERT (`DDINSERT`), the eight added to the 142 default aliases and to the registry (196
commands). SOLID keeps the corners in the order they are picked and goes on from the third and fourth corners of the last
solid; Enter at the fourth makes a triangle. -BOUNDARY has Advanced options (island detection, object type); BOUNDARY takes the
same settings. -HATCH asks for points, Select objects, Properties (typed, as HATCHEDIT), Advanced (Retain boundary, Island
detection, Style, Associativity, separate Hatches) and DRaw order (under the boundary as AutoCAD starts), says `This area
has already been specified.`, and makes one hatch, or one of each pick, as one undo step; GRADIENT is HATCH with a gradient;
HATCHTOBACK sends every hatch of the space to the back. SPLINEDIT has Close and Open for a spline of fit points (the 8 control
points AutoCAD wrote for the spline through (0, 0), (10, 10), (20, 0), (30, 10), (40, 0)), Move vertex, rEverse, convert to
Polyline (53 vertices at precision 10, as AutoCAD made them) and Undo. Not built: AutoCAD's Boundary set, Gap tolerance, Origin,
hatch COlor, LAyer, Transparency and Single color of -HATCH; SPLINEDIT's Fit data, Join and Refine.
**CHANGED.** The typed form of a gradient (-HATCH Properties, HATCHEDIT with no window) asks what AutoCAD asks: `Enter a gradient
name or [?/Two color] <GR_LINEAR>:`, `Specify an angle for the gradient <0>:` and `Center the gradient [Yes/No] <Yes>:`, where
it asked `Enter gradient name` alone and left the angle and the centring as they were.
**NEW.** LAYERP (Layer Previous) and LAYERPMODE in the presenter, with the words of AutoCAD 2021 (read from its resources and
probed in accoreconsole). LAYERP puts back the last change or set of changes made to the settings of layers (on or off,
frozen, locked, plot, colour, linetype, lineweight, transparency, the current layer) without touching any other edit, and
again and again for the sets before: LAYISO turning off two layers and moving the current one is one set, so is everything
the Layer manager changed between its opening and its closing, so is one change of the layer box. A layer added, renamed or
deleted stays as it is. It says `Restored previous layer status.` and, where they apply, the `*Warning*` and `*Note*` lines
(the current layer left off, the previously current layer gone, a linetype no longer loaded), and `*No previous layer
status*` with nothing kept. It is one undo step, so U after it brings the change back and the next LAYERP takes it again,
and a layer edit undone with U leaves the sets. LAYERPMODE asks `Enter LAYERP mode [ON/OFF] <ON>:`; with OFF nothing is kept
and LAYERP says `Layer-Previous is disabled. Use LAYERPMODE to turn it on.` (a command in AutoCAD 2021, not a system
variable: SETVAR says `Unknown variable name.`). New: `CadDocument.LayerPreviousMode`, `CadDocument.BeginLayerSet` and
`EndLayerSet`; Home › Layers has a Previous button. The viewport overrides of a layer (VP Freeze and the rest) are not kept.
**NEW.** The aliases `1` LAYISO, `2` LAYERP, `22` LAYON and `3` LAYOFF of AutoCAD's acad.pgp are default aliases (134 in
all, where the guide said 120). A settings file now lists the default aliases it has seen (`knownDefaultAliases`, as
`knownDefaultKeys` lists keys), and one written before is given the four once, an alias of the same name that the user made
or cleared never overwritten or put back.
**NEW.** ROTATE and MIRROR of a viewport clipped by an entity (VPCLIP) and its boundary as AutoCAD 2021 does them, probed in
accoreconsole (the probes GhVp and GhVq). ROTATE twists the view by the angle (group 51, from 0 up to 360 degrees)
and writes the view's centre as the turn of `2 V - C' - T` by the new twist, V the view's centre before, C' the frame's centre
carried by the rotation and T the view's target, whatever the scale of the view and the shape of the boundary: a viewport 60
by 60 about (110, 60) showing (1 000, 500) at 1:10, turned 45 degrees about the origin, is about (35.355, 120.208), twisted
45 degrees, its view centre (767.107, 2 011.320); turned 90 degrees about its own middle it is twisted 90 degrees with its view
centre at (-940, 1 890). MIRROR (the copy and the erasing one) puts the pair at the mirrored place with the view's centre and
twist as they were, and a copy made by ROTATE with Copy or by MIRROR no longer fails on the viewport: the copy of the
viewport is tied to the copy of its boundary and fitted the same way (`CadViewportClips.TieCopies`).
**NEW.** A clipped viewport copied and pasted into another drawing (COPYCLIP, PASTECLIP) comes along with its boundary, as
AutoCAD 2021's `WblockCloneObjects` brings it: before, COPYCLIP of a viewport failed (a viewport is not put in model space)
and a pair copied by `db.WblockCloneObjects` kept the copy of the viewport naming the original's boundary, or one of the
other drawing. `DxfDatabase.WblockCloneObjects` now ties the copy of a clipped viewport to the copy of its boundary (group 340,
the viewport its only reactor), brings the boundary of a viewport copied alone into another drawing, and leaves a viewport copied
alone within the drawing naming its original's boundary, which does not list it, all as AutoCAD 2021 leaves them. The clipboard
holds the viewports of a layout in a layout of its own drawing; PASTEBLOCK does not take them.
**FIXED.** A pair of a clipped viewport and its boundary moved by MOVE, or by the grip at the viewport's middle, had the centre of
its view shifted by the step of the frame (a pair moved by (20, 5) at 1:10 showed what lay 200 and 50 further on); AutoCAD 2021
keeps the view's centre, the frame going with its boundary and showing what it showed. SCALE, whose step AutoCAD does shift,
is unchanged and now has a test (a pair scaled by 2 about the origin: frame 120 by 120 about (220, 120), view height 1 200,
view centre (2 100, 1 100)).
**NEW.** Tests that the image of a layout (`CadRasterWriter.Draw`, IMAGEOUT, PNGOUT) and its plot to a PNG draw a viewport
clipped to a circle through its outline, counted in pixels (nothing of the model outside the circle where an unclipped viewport
shows walls beyond it), and that a corner of the boundary dragged on the canvas fits the frame of the viewport to it in one
undo step.
**NEW.** The presenter's commands and aliases checked against AutoCAD 2021's acad.pgp. The 68 stock aliases it lacked for
commands it has are added (`DELETE` ERASE, `MO` PROPERTIES, `PRINT` PLOT, `DIMLIN` DIMLINEAR, `DTEXT` TEXT, `DDMODIFY`
PROPERTIES and the rest; 202 aliases now with the four digits, DXFOUT being a command of its own), and thirteen commands with their aliases, each asking AutoCAD 2021's prompts as
its console asked them and making one undo step: DIVIDE (`DIV`) and MEASURE (`ME`), points or a block, aligned or level,
along a line, arc, circle, ellipse, polyline or spline (a circle of 50 about (400, 0) divided in 4 has points at (450, 0),
(400, 50), (350, 0) and (400, -50); a line of 100 measured by 30 from the end picked has points 30, 60 and 90 from it;
`Object isn't that long.` for a length past the curve; every point checked against AutoCAD's to 1E-6); DIMCENTER (`DCE`),
the mark of DIMCEN as lines; COLOR (`COL`, `COLOUR`), the window's Select Color dialog, and -COLOR; LWEIGHT (`LW`) and
-LWEIGHT, rounding a width to the nearest of AutoCAD's (`0.33` gives 0.35 mm); REDRAW (`R`), REDRAWALL (`RA`) and
REGENALL (`REA`); ISOLATEOBJECTS (`ISOLATE`), HIDEOBJECTS and UNISOLATEOBJECTS (`UNISOLATE`, `UNHIDE`), which the canvas's
shortcut menu offered without the commands behind them, hiding objects for the session as AutoCAD 2021 does (their
Visible off; a save writes them shown).

**FIXED.** A radius or diameter dimension in the presenter whose DIMCEN is below nought drew centre lines on a circle too
small for them: AutoCAD 2021 draws them only where the radius is over twice the mark (a circle of 5.01 under -2.5 has
them, one of 5 or 4 the cross alone), where the presenter drew them wherever the radius was over the mark.

**NEW.** DIMOVERRIDE (DOV, DIMOVER) in the presenter, as AutoCAD 2021's (its prompts, defaults and refusals probed in its console):
the dimension variables typed one by one, "Enter new value for dimension variable <1.0000>:", written as overrides of the
dimension style on the dimensions, leaders and feature control frames selected, each dimension drawn anew, one undo step;
Clear overrides takes them away. The variables of R2007 and after (DIMJOGANG, DIMLTYPE, DIMTFILL and the rest) say `Variable
"..." not found.` as AutoCAD does, a whole number out of range `Requires an integer between 0 and 4.`, a size below nought
`Value must be positive.`.
**NEW.** TEXTALIGN (TA): TEXT and MTEXT lined up on the text picked or on a line of two points by an edge, a middle or a
corner of their boxes (Left, Center, Right, TL ... BR), moving across (Current Vertical), up or down (Current Horizontal),
stacked a gap apart (Set spacing) or spaced evenly (Distribute), with AutoCAD's prompts and settings.
**NEW.** MLEADEREDIT (MLE) adds leader lines to a multileader and takes them away; MLEADERALIGN (MLA) moves the content of
multileaders onto a line through the content of one, keeping their spacing, a set gap apart or evenly between two points
(the gap between the content points; make leader segments parallel and MLEADERCOLLECT are not built).
**NEW.** ATTIPEDIT (ATI): the value of an attribute edited in place over it on the canvas, one undo step. SPELL (SP): the
words of TEXT, MTEXT and attributes checked against a small built-in English list and a custom dictionary file, each unknown word offered with
the words nearest it (Ignore, ignore all, Add, Change, a replacement typed, Stop), the command line form of Check Spelling.
MLINE, MLSTYLE and MLEDIT are not built.
**NEW.** The grips on the clip boundary of a clipped block reference or external reference in the presenter, as AutoCAD 2021's
`GetGripPoints` gives them (probed in accoreconsole): a selected reference has its insertion grip, an arrow at the middle of the
boundary's first side and a grip at each corner of the rectangle or vertex of the polygon (`CadGripKind.ClipFlip` and
`CadGripKind.ClipVertex`, listed by `CadGrips.Of`). A corner dragged takes its two sides and keeps the opposite corner, the
rectangle staying one (the corner (1020, 520) of the rectangle (1020, 520) to (1080, 570) dragged to (1010, 510) leaves
(1010, 510) to (1080, 570); onto a side of the opposite corner it says `Boundaries with zero area are not acceptable.`); a
polygon's vertex moves alone; a clip turned inside out stays so, its keyhole made anew about the reference's box. The point is
carried through the matrices the clip keeps, so that a reference scaled, turned or moved after the clip is dragged where the
cursor is. One undo step, GRIP, and every grip mode works. A click on the arrow turns the clip inside out, or back, at once
(`CadGrips.ClickClipFlip`, one undo step): a rectangle of two corners becomes the eleven points of its keyhole and returns to
two. `CadXClips.TryUninvert` takes a keyhole back to its polygon, wherever the ring starts.
**NEW.** EXPLODE of a clipped block reference is checked against AutoCAD 2021 (probed on a rectangle, a polygon, a clip turned
inside out and a clip off): it breaks the whole block, the clip applied to no piece, as the presenter does; a test keeps it so.

**FIXED.** A radius or a diameter under DIMTIX is laid out in the presenter as AutoCAD 2021 draws it, probed in its console
over 2 752 radii and diameters (circles of 3 to 20, DIMATFIT 0 and 3, DIMTAD 0, 1 and 4, DIMTOH and DIMTIH together or apart,
DIMTOFL on and off, points from the centre to 2 radii out): 2 063 of them differed, 75 do now (level words in a circle too
small for them, which differ with and without DIMTIX). Under DIMATFIT 3 the words
stand next to the rim wherever the point was put, where the picture followed the point: "R20" put 40 out at 30 degrees stands
28.6 out in Arial 2.5 high, its line from 22.5 to 32.5, a diameter's far arrowhead outside with its line only where DIMTOFL
is on (else the centre is marked), and level words are decided by DIMTIH and stand on a landing an arrowhead long, not above
the line. Under DIMATFIT 0 to 2 DIMTIX keeps the words inside the radius however tight, as it had them outside: a radius of 5
has its line whole to the rim and its arrowhead outside on its short line, "%%c6" in a diameter of 6 its arrowheads
outside; DIMTOFL off leaves no line and marks the centre. Under DIMTMOVE 1 and 2 the point is not heeded either: the words
stand next to the rim under DIMATFIT 3 (and under 1 whatever DIMATFIT), in the middle under 2 with DIMATFIT 0 to 2.
**FIXED.** DIMTEDIT on a radius or a diameter at DIMTMOVE 0 does as AutoCAD 2021: under DIMATFIT 0 to 2 it turns the line
toward the point (the words at the point, the line whole to the arrowhead; moved past the rim they stand where the fit puts
them, the arrowhead outside but under DIMATFIT 2), and under DIMATFIT 3 it slides the words along the line, as before. Its
state does not decide it: the 164 or 36 the dimension had, which AutoCAD writes by how the words were put, is no test (a
radius under DIMATFIT 3 and DIMTIX is a 36 and slides). Under DIMATFIT 3 words moved within the room next to the rim are
written 36 and pushed out to where DIMRADIUS would put them, and words inside are held back from the rim as they are put
there, the group 11 of both the point of the line they stand by: "R20" of a circle of 20 moved to 25 out stands 28.6 out and
is a 36, moved to 15 out stands 11.25 out and is a 164. Of 1 024 radii and diameters made and moved in AutoCAD's console
(DIMATFIT 0 to 3, DIMTIX, four points and four moves each) 625 differed, 1 does now; of 432 moved twice 300 differed, 1 does.
**FIXED.** The words of ordinate, linear, aligned and angular dimensions that DIMTEDIT, or the Text position rows, moved are
drawn as AutoCAD 2021 draws them when it recomputes the file. Of 289 such dimensions (each kind under DIMTMOVE 0, 1 and 2,
DIMTAD 0 and 1, ten points) 114 differed, 5 do (a point exactly on the first extension line, parked), and of 96 angles with
their words at 6, 30 and 80 degrees 66 differed, 10 do. An ordinate's leader runs to the words, jogged where they stand to
one side of the point, a gap short of them or under them beside the leader, and under DIMTMOVE 2 stays where it ends: the
point (1200, 0) with its words moved to (1208, 50) has its leader up to 35.764, across 8 as it rises 5, up 5 more. A linear
or aligned dimension whose words are put past an end of its line under DIMTMOVE 1 has the line run on to them, to the near
edge of words on it and to the far edge of words above it. An angle's words put on its arc under DIMTMOVE 1 or 2 break the
arc about them, and under 0 send the arrowheads outside, the arc whole, where they cut into an arrowhead's room ("60°" 6
degrees from the first ray); words put under 1 within 6.875 of the arc (arrowheads and words 2.5 high, DIMGAP 0.625) have no
leader, a distance fitted to what AutoCAD draws for eight sizes.
**NEW.** A drawing is saved as R12: `DxfDatabase.Save(path, "AC1009")` and `Save(stream, "AC1009")` write a drawing of R2000 or
later as AutoCAD 2021 writes the same drawing as R12, the rules read from AutoCAD's own R12 of one drawing of 1 300 lines, 130
hatches, 2 tables and 15 multileaders beside its R2018. Four sections only; the 133 header variables of R12 in its order,
`$HANDLING` on and the names in capitals; the tables of R12 with no block records, handles or extended data, no ByBlock or
ByLayer, a TrueType font as `txt`, a dimension style with all its values, arrowheads named by their blocks; `$MODEL_SPACE` and
`$PAPER_SPACE`, the other layouts left out; each entity with the groups R12 has, a viewport's view in the extended data of ACAD
MVIEW. What R12 lacks is broken into what it holds, and said: an LWPOLYLINE is a POLYLINE, an ellipse a 3D polyline of 129
vertices, a spline one of 128, a ray and an infinite line a LINE to the extents of the drawing (each with its handle); an MTEXT,
a pattern HATCH, a LEADER, a MULTILEADER, an MLINE, a table, a MESH (one 3D polyline for each edge) and a HATCH filled solid or
with a gradient (SOLIDs, one colour for a gradient) are the lines, texts and polylines they break into, in an anonymous block
`*U` or `*X` with an INSERT of the entity's handle where they stood in a space, in place where they stood in a block. Left
out and said: a TOLERANCE, a WIPEOUT, an IMAGE, an underlay, an OLE frame, the layouts beyond the first, and a hatch whose
normal is not (0, 0, 1). A drawing of R12 or R14 is still saved as it is only, to any other version.
**NEW.** SAVEAS with FILEDIA 0 (or no window) asks what AutoCAD 2021 asks, in its words, read from its resources: `Current
file format: AutoCAD 2018 Drawing` and `Enter file format [R14(LT98&LT97)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/
2013(LT2013)/2018(LT2018)/Dxf/Template/Standards] <2018>:`; 2018 is the DWG, and the older formats, Template and Standards are
refused with a line; Dxf asks `Current DXF settings: Precision= 16 Format= ASCII Preview= No Version= 2018`, `Enter decimal
places of accuracy (0 to 16) or [Version] <16>:` and `Enter version [R12(LT2)/2000(LT2000)/.../2018(LT2018)] <2018>:` (R14
refused), then `Save drawing as <file>:`. DXFOUT is new, a DXF copy of the drawing that leaves it as it was: the window's Create
DXF File dialog, or `Enter DXF file name <Drawing1>:` and the same settings. `CadDocument.SaveCopy(path, version)` writes the
copy; `CadExportJob.DxfSaveTypes`, `DxfTypeOfVersion` and `DxfVersionOfType` are the DXF types of Save Drawing As (R12 added, as
`AutoCAD R12/LT2 DXF (*.dxf)`), which the window's Save As lists.
**CHANGED.** QSAVE of a drawing with no file, and SAVEAS, with FILEDIA 0 now ask the file format before the file (the file
alone was asked); SAVEAS is a command of the presenter's own set (`CadCommands`) that asks the window for its dialog or the
typed file as QSAVE does (`UiRequested` "SAVEAS", with a `CadExportJob` for the typed file), where the window added it.
`MainViewModel` lists R12 among the DXF types of Save Drawing As.

**FIXED.** Edit Drawing Scales in the presenter now works as AutoCAD 2021's. Reset's question offers the three buttons AutoCAD's
task dialog has, Metric scales (1:1 to 100:1, 17 scales), Imperial scales (1:1 and `1/128" = 1'-0"` to `1'-0" = 1'-0"`, 17)
and Metric and Imperial scales (the metric ones then the 16 imperial ones that are not 1:1, 33), where it offered the
drawing's own list only; `ScaleListEditViewModel.ConfirmReset` now answers a `ScaleListResetChoice` (Cancel, Metric,
Imperial, MetricAndImperial) and `ResetChoice` is gone, and `CadScaleListRequest` has `MetricDefaults`, `ImperialDefaults` and
`MetricAndImperialDefaults` beside `Defaults`. The list takes several scales chosen at once: Delete is live while the choice
holds a scale the drawing does not keep, and over one that holds a scale in use asks `Scales in use cannot be deleted.
Delete selected scales not in use?` before it deletes the others (`ConfirmDeleteInUse`), or says `The scale list must
contain at least one scale.` over a choice that would empty the list (`ShowMessage`); Edit…, Move Up and Move Down stay
for one scale, and the line under the list says `Varies: multiple scales selected.` over several.
**FIXED.** The other questions the presenter's windows asked in words of their own now say what AutoCAD 2021 says, read from
its task dialogs and messages. Purge with Confirm each item asks `Purge - Confirm Purge`, `You are about to purge layer TMP.
What do you want to do?` (the PURGEITEM payload of the command is that sentence now) with Purge this item, Skip this item
and Cancel; a block of a used name asks `Block - Redefine Block`, `"GB" is already defined as a block in this drawing, would
you like to redefine this block reference?` with Redefine block and Don't redefine "GB" (the Block dialog's message box
asks it too); New View over a used name `View - Already Exists`, `Stair core already exists.  Do you want to replace it?`
with Replace and Cancel; New Layer State to Save over a used name `Layer - Overwrite Layer State`, `Do you want to overwrite
Plan with the drawing's current layer state?`; the Block Attribute Manager `The attributes in the current block have been
changed.` and `Do you want to save these changes?`; Write Block `<file> already exists.  Do you want to replace it?`; the
Text Style dialog `The current style has been modified.` and `Do you want to save your changes?`; and the Layer manager
refuses to rename layer 0 or a layer of an xref with `Layer - Rename`, `The selected layer cannot be renamed.` and the layers
that cannot be renamed. The words are in the new `CadQuestionTexts`.
**FIXED.** A new drawing in the presenter (NEW, Ctrl+N) now has the layers of Global settings > Layers, colours and lines:
Visible, Hidden, Centre, Hatch, Dimension, Mark, Symbol, Sheet and Viewport by default, each with its colour, linetype,
lineweight and plot flag (Hidden `#9AA4B1` in HIDDEN at 0.18 mm, Centre `#F85149` in CENTER at 0.18 mm, Viewport not
plotted), and the linetypes HIDDEN and CENTER with them. The table was edited and saved but never applied: a new drawing had
the layer 0 alone. A linetype outside the presenter's own set is drawn Continuous; the current layer stays 0; a drawing
opened from a file is not changed. `CadTemplate.AddLayers(database, layers)` is the new public member that adds them.
**NEW.** Ctrl+3 shows or hides the whole ribbon of the presenter (its tabs and panels), as Ctrl+1 and Ctrl+2 do Properties and
the Drawings list: the default key of the new shortcut target RIBBON (23 default keys now), which a settings file saved
before it gets once when it is read, if Ctrl+3 and RIBBON are both unbound. The typed commands RIBBON (shows the ribbon) and
RIBBONCLOSE (hides it) work as AutoCAD's, with or without a drawing open; `MainViewModel.IsRibbonShown` holds it.
**NEW.** In the presenter's Global settings > Shortcuts a Keys cell takes the keys pressed in it, as the shortcut key box of
AutoCAD's Customize User Interface does: Ctrl+Shift+D pressed shows `Ctrl+Shift+D`, Backspace or Delete clears the keys and
Esc gives back those the cell had; a letter alone or with Shift alone is typing and is not taken. The keys used to be typed.
**NEW.** **Import acad.pgp…**, left of Reset to AutoCAD defaults, reads the aliases of a `.pgp` file you pick
(`ALIAS, *COMMAND`, a hyphen kept, `;` comments and external commands passed over) and merges them into the aliases, the
file's winning, saying how many were added, changed, unchanged, skipped for a command the app does not have, and not read.
**CHANGED.** A notch of the wheel zooms 1.5 times by default (`GlobalSettings.WheelZoomFactor`), where it zoomed 1.2 times; a
settings file that holds a factor keeps it.
**CHANGED.** The presenter's Global settings has eight categories. Drawing defaults, Data sources and the Drawing text rows
of Text and fonts are gone from it, being the defaults of drawings made from a model; `GlobalSettings` still reads and writes
them, and GeometryHelper.Drawing still uses them. Fonts is now **Folder preference**, the font and support file search
folders alone; the replacement font, the font mapping file and the substitutions are on Text and fonts, whose UI font is a
box of the font families installed on Windows, each in its own face, that also takes a name typed.
`GlobalSettingsViewModel.IsFontsPage`, `IsSourcesPage` and `Sources` give way to `IsFolderPreferencePage` and
`IsTextAndFontsPage`.
**CHANGED.** What was left of the Tekla drawing (Part B) in the presenter, GeometryHelper DXF, is gone; the one hook Part B needs
is neutral. The presenter's Global settings no longer shows the Hidden lines colour, the Dimension
decimals, or the DXF export section (the layout, "Sheet in model space", and "Open the folder after export"), and its
categories no longer carry a MODEL tag. The Dimension Style Manager, the dimension style edit dialog and the style boxes of
the Annotate tab no longer mark a style named `GH_MODEL_...` as MODEL or say that an update keeps its changes: such a style
is any other style. The Layer manager no longer has its Owner column (always empty) or its note about layers a model drawing
wrote. The canvas no longer draws a layer named Hidden in the colour of Global settings' hidden lines: a layer is drawn in its
own colour, as AutoCAD draws it. `GlobalSettings` still reads and writes `DefaultPreset`, `DefaultSheetSize`,
`DefaultOrientation`, `DefaultProjection`, `CheckForModelChanges`, `CheckEverySeconds`, `ExportLayout`, `OpenAfterExport`,
`HiddenLines` and `DimensionDecimals` for GeometryHelper.Drawing; the presenter shows and applies none of them.
**BREAKING.** `MainViewModel.NewFromModelRequested`, `CanMakeFromModel`, `NewFromModelCommand` and `WelcomeText` are removed
from the presenter. An extension that makes a document of its own returns a `CadNewDocumentOffer` (the menu name, button
text, tooltip, start-screen line, welcome line and the action) from the new `CadWorkspaceExtension.NewDocumentOffer`; the
view model gives `MainViewModel.NewDocumentOffer`, `HasNewDocumentOffer` and `NewOfferedDocumentCommand`, and Ctrl+N, the
start screen and the New entry of the application menu use it. GeometryHelper.Drawing.Wpf offers "Drawing from a model" this
way. Without an offer nothing about models shows.
**BREAKING.** Removed from the presenter: `CadDimStyleEditor.IsModelStyle`, `StyleRowViewModel.IsModel`,
`GlobalSettingCategory.IsModel`, `LayerRowViewModel.Owner`, `CanvasOptions.HiddenLineColor` and
`CanvasOptions.HiddenLayerName`.
**NEW.** XCLIP (alias XC) in the presenter, as AutoCAD 2021's, word for word as its console prints it (probed in accoreconsole):
`Select objects:` keeps the block references, MINSERTs and external references (`1 was ineligible for clipping.`, `None found.`),
then `Enter clipping option [ON/OFF/Clipdepth/Delete/generate Polyline/New boundary] <New>:`. ON, OFF and Delete act on each
reference that has a clip (`No clip boundary found.` where none has), generate Polyline draws a closed polyline along it on
the current layer, Clipdepth sets a front and a back plane by distance or Remove (`Front clipping plane behind back clipping
plane.  Clipdepth not applied.`), and New boundary asks `Delete old boundary(s)?` where one exists, then Select polyline,
Polygonal (refusing a side of no length or one that crosses another), Rectangular or Invert clip (`Inside mode - Objects inside
boundary will be hidden.`), the boundary taken in the current UCS. One undo step, XCLIP. A reference clipped is drawn, picked,
windowed and plotted inside its boundary alone, a MINSERT cell by cell and an external reference too, with the frame of the
boundary drawn over it while the new system variable XCLIPFRAME (0 to 2: AutoCAD's drawings write 2, a header that gives none reads 1) is not 0 and left out of a plot at
2; a clip moves, turns, scales and copies with its reference. An external reference's context menu offers Clip Xref.
**NEW.** `DxfBlockReference.ClipTo(points, worldToClip)`, `RemoveClip()`, `SpatialFilter` and `ClipOutline`, and the class
`DxfSpatialFilter` (`Points`, `Normal`, `Origin`, `IsEnabled`, `FrontClip`, `BackClip`, `InverseBlockTransform`,
`WorldToClip`, `GetRing()`): the clip of an INSERT as AutoCAD 2021 keeps it, a SPATIAL_FILTER in the dictionary ACAD_FILTER of the
INSERT's extension dictionary, its class listed and counted in CLASSES; AutoCAD reads a drawing of it, DXF and DWG, and its AUDIT
finds nothing. A clip turned inside out is kept as AutoCAD keeps it, a polygon with a slit round the block's extents
(`CadXClips.Inverted`: the eleven points AutoCAD wrote for a rectangle on a block scaled by 2 and turned by 30 degrees).
**NEW.** The DWG reader and writer read and write the SPATIAL_FILTER object (R2000 to R2018), so that a clipped INSERT keeps its
clip in a DWG: AutoCAD 2021's own DWGs of R2004, R2007, R2010 and R2018 read to the same filters, and what the writer writes
reads back to the same records.
**NEW.** The crosshair is pulled onto a grip, as AutoCAD pulls it: while no command runs and no box or pan is under way, the
cursor inside the box of a grip (its square, or the bar of a polyline's segment middle) draws the crosshair with its centre on
the grip's point and the status bar shows that point, so that a click starts the grip edit at exactly it; outside the box the
crosshair is under the pointer again, which the system's own pointer always is.
**NEW.** A drawing is saved as another version of AutoCAD: `DxfDatabase.Save(path, version)` and `Save(stream, version)`
write it as R2000 (AC1015), R2004 (AC1018), R2007 (AC1021), R2010 (AC1024), R2013 (AC1027) or R2018 (AC1032), as AutoCAD
2021's `SaveAs` does, and give back what the version could not hold, a sentence each. Every kind of entity and object stays, as
AutoCAD keeps them when it saves an older version; the header variables, the groups of each record (a true colour and a
transparency before R2004, the background of an MTEXT, the gradient of a hatch, the lighting of a viewport, the handles of a
dimension style's line types, the proxy graphics' size in 92 or 160) and the counts of the classes are those of the
version, read from AutoCAD 2021's own DXF of one drawing of 1 300 lines, 130 hatches, 2 tables and 15 multileaders in each
version: converted from R2018, 735 of its 785 records that carry a handle are group for group what AutoCAD wrote as R2000. A
table becomes the INSERT of its block before R2007, a solid of R2013 or later is left out before R2013, and the visual styles
unless both versions are R2013 or later; AutoCAD 2021 opens each of the six, reads each as the version asked, and audits them
at 0 errors. Saved in its own version a drawing is the bytes `Save(path)` writes; a drawing of R12 to R14 is saved as it is only,
and R12 is not written yet.
**NEW.** The presenter's Save Drawing As lists AutoCAD 2018 Drawing (*.dwg) and a DXF of each of those six versions
as AutoCAD 2021 names them, the drawing's own chosen first, and writes the type chosen: `CadDocument.SaveAs(path, version,
progress)`, kept in `CadDocument.SaveVersion` for Save (Ctrl+S, QSAVE) as AutoCAD's QSAVE keeps the last format, and what the
version could not hold in `CadDocument.LeftOutOfDxf`, said on the command line. It offered the DXF of the drawing's own
version alone, and the DXF was written in that version whatever the drawing was.

**NEW.** `DxfTable.TextStringForEditing(row, col)` gives the text of a cell as AutoCAD 2021's
`GetTextString(FormatOption.ForEditing)` does, and the presenter's editor of a table cell opens with it: a number as it is
to 16 figures with its zeros and the places of its units left off, so that a cell showing 1.5000 opens as 1.5, a third as
0.333333333333333, 1e3 as 1.000000000000000E+03, 1/3 as 10923/32768, 12.5 in feet and inches as 1'-0 1/2", 75.25 in
engineering units as 6'-3.25" and a point as 1.5, 2.25, -0.1 (probed on every unit, with prefixes and thousands of a
format left out). It used to open with the text the cell shows. The words given back unchanged write nothing.
**CHANGED.** A text typed over a cell of a number or a point in the presenter (`CadTableEditor.SetText`, TABLEDIT) is
refused, as AutoCAD 2021's `SetValue` of it throws eInvalidInput and keeps the value: abc, 12mm, 1/1/2020 and 12:30 over
1.5, a number over a point and a point over a number, with "Cell C3 holds a number, and "abc" is not a number."; it used to
turn the cell into text. Empty words still clear the cell, and a cell of words takes any.
**NEW.** Thousands typed with commas in a table cell in the presenter are numbers of AutoCAD 2021's thousands format:
1,500 the whole number 1500 of `%th[\,]` shown 1,500, 1,500.5 the number of `%lu6%th[\,]` shown 1,500.5000, 1,500e2 one of
`%lu1%th[\,]`, and ,500 and 1000,000 read as AutoCAD reads them; a comma before other than three figures (1,50, 1,5000) is
still a point. They stayed text. A cell whose format carries `%th44` or `%th46%ds44` (AutoCAD's own) shows a number typed
in it with its marks: 1234567.891 in `%lu2%pr2%th44` is 1,234,567.89 and 1500.5 in `%lu2%pr2%th46%ds44` is 1.500,50.
**NEW.** A length typed in feet and inches in a table cell is a number of inches, as AutoCAD 2021 reads it: 12'6", 12'-6",
12' 6", 12'6-1/2" and 12' (the number 150, 150, 150, 150.5 and 144 of `%lu4`, shown 12'-6", 12'-6", 12'-6", 12'-6 1/2" and
12'-0"), and 6.5", 12.5' and 12'6.5" as numbers of `%lu3` (shown 0'-6.5000", 12'-6.0000" and 12'-6.5000"); a sign before
all, -12'6" being -150. A space before the figures, 12ft, 12"6 and 12'6"' are not lengths and stay text. Over a whole
number the inches are cut (12'6.5" is 150). The units of engineering (`%lu3`) are now shown, 11.99999 as 1'-0.0000" with its
inches carried into the feet, where they were written as the number alone. Dates and times typed (1/1/2020, 12:30) stay
text: AutoCAD makes dates of them, and the library knows no groups of a date value to write.
**FIXED.** The text of a number of a decimal format (`%lu2%pr2`) in `DxfTable` rounded as the runtime rounds on .NET Core
and .NET 10: 0.015 was shown 0.01, 2.5 at no places 2 and 1.005 1.00, where AutoCAD 2021 and .NET Framework show 0.02, 3
and 1.01. It now rounds half away from nought the 15 figures the number writes, on every runtime, as the units shown
already did.

**FIXED.** The texts of -SCALELISTEDIT and of Edit Drawing Scales are AutoCAD 2021's own, read from its resource files
and probed in its console, where they had been written by hand. -SCALELISTEDIT: an empty name says `Must enter a valid
name.`, a name of 65 characters or more `Scale name too long.` (64 are taken), and the name is asked again (the empty name
used to end Add without a word); deleting the only scale left says `Scale list must contain at least one scale. 1:1 not
deleted.`, where it said the scale was referenced. Edit Drawing Scales: Add Scale and Edit Scale say `No name specified
for this scale.  Please enter a name.` and `Invalid scale number.`; Edit Scale given a name used says `1:25 already
exists.`; Add Scale given a name used asks `1:25 already exists.  Would you like to redefine this scale?` and on Yes
gives that scale the units (it refused the name), or says `1:50 already exists and is a referenced scale. Please enter a
different scale name.` for a scale an object points at; Reset asks in the words of AutoCAD's task dialog (`Scale List -
Reset`, `Resetting will restore a default list of scales for the drawing. Which default scale list do you want to
restore?`) with the button Metric scales or Imperial scales, and Cancel. New `ScaleEditViewModel.ConfirmRedefine`,
`Redefined` and `RedefineQuestion`, `ScaleListEditViewModel.ResetChoice`, `ResetTitle`, `ResetQuestion` and
`ResetDetail`; `CadQuestionWindow.AskResetScaleList` takes the label of the button, and `CadQuestionWindow.AskRedefineScale`
is new.
**CHANGED.** In the presenter a radius or a diameter is written as AutoCAD 2021 writes the one DIMRADIUS or DIMDIAMETER
makes, probed in its console over 192 combinations of DIMATFIT, DIMTMOVE, DIMTIX and the point: group 40 (the leader's
length) is nought, and group 70 is 164 (radius) or 163 (diameter), flag 128 set, where the words stand at the point
given, group 11 the point of the line they stand by, and 36 or 35, group 11 the middle of the words, where the fit or the
room next to the rim put them (DIMATFIT 0 to 2 under DIMTMOVE 0, DIMTIX, or a point put past the rim within two
arrowheads, the gap and half the words' width: "R20" of a circle of 20 about the origin put at (35, 12) writes 164 and
(35, 12) under DIMATFIT 3, 36 and (9.3153, 3.3861) under DIMATFIT 0). Before, the leader held the distance from the rim
and the flag was clear, so that AutoCAD's own regeneration of the file laid the words and lines out elsewhere: of 480
radii and diameters in 12 styles 179 differed after it, 20 do now (diameters of a circle of 8 whose "%%c16" the fonts
measure differently). DIMTEDIT to a point leaves a leader of nought, and its Home writes 36 and stands the words as
DIMRADIUS does for a point on the rim. The words of a diameter put inside under DIMTMOVE 1 stand on the line with one
arrowhead, as a radius's do.

**FIXED.** A radius or a diameter laid out in the presenter differed from the one AutoCAD 2021 makes in three corners,
probed against 3 090 made in its console: 480 in 12 styles (36 differed, 24 do: the same diameters of 8), 1 920 with
the point at the centre, on the rim, 0.985, 0.7 and 1.015 of the radius out, on lines from level to upright, level
words or not (50 differed, 16 do: points exactly on the rim of an axis, which AutoCAD decides by the rounding of its own
coordinates) and 690 on lines every few degrees round the circle for DIMTAD 0, 1 and 4 (214 differed, 28 do: lines exactly
15 degrees from level, likewise). Level words (DIMTOH, DIMTIH), DIMTAD 1 or 4, off a line that runs down keep the room of
one more line of words past two arrowheads, outside the rim and in ("R20" of Arial 0.18 high put 20.3 out straight down
has its landing 20.5438 out, not 20.36); level words on a line within 15 degrees of level stand inside with no landing,
as outside, a gap and half their width past the point ("R20" put 14 out at 3 degrees: line end (13.9808, 0.7327), words'
middle (13.3614, 0.7327)); and a line exactly 15 degrees from level has its landing, inside and out.

**NEW.** In the presenter the command line's Search Content lists each kind of named object under a header row with its
count, as AutoCAD 2021's suggestion list groups its content (design CmdContentSection): WAL typed in a drawing of the
block WALL_TAG and the layers WALLS and WALL-FINISH shows `Blocks 1` over WALL_TAG and `Layers 2` over the two layers.
A click on a header, or Enter or Space with it chosen, collapses or expands the kind and keeps the list open; a kind
collapsed stays so while the app runs. The pointer over a hatch pattern shows a thumbnail of it, 96 pixels square, drawn
by the app from its own pattern lines. New `CommandSuggestionViewModel.IsHeader`, `IsExpanded`, `Chevron`, `IsIndented`,
`HasThumbnail`, `Thumbnail` and `ThumbnailSize`, and `MainViewModel.ToggleSuggestionGroup`.
**CHANGED.** With Separate commands and system variables on, the system variables wait under a collapsed `System
Variables` header after the commands until Tab expands it and chooses the first of them, as AutoCAD 2021's Input Search
Options says ("you need to press Tab to display the list"); they used to be listed at once below a rule. New
`MainViewModel.TabSuggestions`.
**CHANGED.** Input Search Options takes Remember corrections after 1 to 50 mistypes, as AutoCAD 2021's dialog does
(its message, read from AcWindows.dll for reference, is now the presenter's: `Invalid input. Mistypes value must be
between 1 and 50`); it took 2 to 10. `GlobalSettings.AutoCorrectMistypes` takes 1 to 50. The delay out of range is
refused with `Invalid input. Delay time must be between 100 and 10000 milliseconds`.
**FIXED.** -SCALELISTEDIT's Reset and Edit Drawing Scales' Reset put back the imperial scales in a drawing whose header
has no $MEASUREMENT, as AutoCAD 2021 reads such a header as MEASUREMENT 0 (probed: Database.DxfIn of a DXF without it
gives English, with `$MEASUREMENT 1` Metric); they put back the metric ones. The imperial list now starts with 1:1, 17
in all, as AutoCAD's list after Reset, so that a drawing holding no 1:1 gets it back too.
**NEW.** In the presenter the External References palette's Unload, Reload and Detach act on every row selected, as
AutoCAD 2021's palette does: grid and axes selected and unloaded are unloaded by one -XREF and are one undo step, and a
reference and an image detached together are one step too. A nested row in the selection is left out of Unload and
Reload, and Detach refuses it in AutoCAD's words, probed in its console: "Xref column has multiple references. Not
detached." and `"plan"`, the reference that places it; -XREF Detach typed says the same.
**FIXED.** The palette's Unload, Reload, Detach, Change Path and Bind… give -XREF and -IMAGE a name with a back quote
before each character AutoCAD reads as a wildcard (`# @ . * ? ~ [ ] -`), so that a reference named `a#1`, `a.b` or `~t`
acts on itself and not on every name the pattern would match: `a#1` matched nothing and `~t` matched every other
reference, and `a.b` also took `a-b`. Typed, the back quote already worked.
**FIXED.** -XREF Unload, Reload and Detach answer "No matching xref names found." for a name that matches no reference,
and Unload "No matching and loaded xref names found." where each reference it matches is unloaded already (it no longer
writes "is unloaded" for them again), as AutoCAD 2021 answers. AUDIT of AutoCAD 2021 on a drawing with references bound
(BINDTYPE 0 and 1, a reference nested in another bound with it, one left attached), saved as a DXF and as a DWG, found 0
errors; four tests keep it so.

**NEW.** In the presenter showing another space is an undo step, as AutoCAD 2021's U takes it, probed in its console:
a tab clicked, LAYOUT Set and TILEMODE are a step each, named LAYOUT, so that after Model, Layout1 and Layout2 two U go back
to Layout1 and to Model, each layout keeping the view it had, and REDO goes forward. The step keeps no copy of the drawing
(the Database is the same object after U), and the selection, which belongs to a space, is let go each way. New
`CadDocument.ShowSpace(space)` makes the step; TILEMODE no longer counts among the variables with no undo step.
**FIXED.** In the presenter U and REDO move the ZOOM Previous lists with the views, as AutoCAD 2021's do (probed on the
paper of a layout): after ZOOM 2X three times, ZOOM Previous and U, the last zoom is back and the next ZOOM Previous goes
where the first went; after ZOOM 2X three times and U, ZOOM Previous goes to the view before the second zoom, not to the
one U had just left. It holds for model space, the paper and each viewport, and for the wheel's gestures that were joined in
one step.
**FIXED.** The Undo and Redo buttons and the canvas menu's Undo record a wheel turn or a pan made less than 80
milliseconds before, as Ctrl+Z does, so that they take that gesture back and not the step before it. New
`MainViewModel.CommitPendingView` is what the workspace sets to its canvas.
**CHANGED.** The paste preview moves a text, an MTEXT and the words of a dimension with the cursor as it moves a line,
by adding to the numbers of their letters, and no longer lays them out again at each move; an image alone is still drawn
anew. A Clipboard of 5 000 TEXTs and 1 000 MTEXTs, with 300 lines and 20 dimensions (6 480 items), previews in 12 ms a
move under .NET Framework 4.8 and 5 ms under .NET 10, where it took 60 to 70 and 43; what is drawn is the same to the
last pixel.
**NEW.** The presenter's DIM asks as AutoCAD 2021's own DIM does, read from its resource strings: the options are
Angular, Baseline, Continue, Ordinate, aliGn, Distribute, Layer and Undo (G for aliGn, where L was taken for it and LA for
Layer); a circle clicked offers Radius or Diameter and no Angular ("Specify diameter dimension location or
[Radius/Mtext/Text/text aNgle/Undo]"), an arc also Angular. Continue asks "Select extension line origin to continue or
[Baseline]", Baseline "Select extension line origin as baseline or [Continue]" (each turns into the other), then "Specify
second extension line origin or [Select/Undo] <Select>" with Offset ("Specify offset distance") in a baseline row. The row
goes on from the extension line picked, where DIMCONTINUE and DIMBASELINE still go on from the last dimension. AutoCAD's
console runs the old Dim: command for DIM, so these prompts could not be probed there.

**FIXED.** A radius or a diameter whose words are put within the room of an arrowhead of the rim is laid out as AutoCAD
2021 lays it out, probed with 2 742 of them made by DIMRADIUS and DIMDIAMETER in its console (SHX words, DIMTMOVE 0 to 2,
DIMTAD, DIMTOH, DIMTIH, DIMATFIT 0 to 3, DIMTOFL, DIMSCALE, circles of 8 to 40): of those 941 differed in lines, arrowheads
or words, 31 do now (a point exactly on the rim, which AutoCAD decides by the rounding of its own coordinates; a point at the
centre; level words inside next to the rim on an upright or nearly level line). Words put past the rim stand no nearer than two
arrowheads and the gap and half their width, or two arrowheads where DIMTOH keeps them level, however near they were put:
"R20.0000" put 20.2, 20.5 or 20.9 out in acad.dwt's Standard stands 20.979 out, put 21 out at the point; a point on the rim
is outside it, 1E-7 in is inside. A diameter follows DIMATFIT 0 to 2 as a radius does where the words are put outside it,
and words put free under DIMTMOVE 2 over a radius's centre push the arrowheads out when their near end is within an
arrowhead of it (it was at the centre itself). On a line exactly level, the words DIMTOH or DIMTIH keep level stand along
it with no landing, on an exactly upright one the landing runs left where the line runs down and right where it runs up:
"R20" put 25 out straight down has its landing from (-0.18, -25) to (0, -25) and its words at (-0.7994, -25), where they
went right. Aligned and linear dimensions whose points run straight down land level words outside their line the same way,
448 aligned dimensions of 1.6 and 3.2 at 14 angles and either side: 8 differed, none do now.
**NEW.** Viewports clipped by an entity, as AutoCAD 2021's VPCLIP makes them, probed in its console (probe GhVc1) and
checked in its AUDIT, which found nothing in a drawing of three clipped viewports written as DXF and as DWG. In the core
`DxfViewport.NonRectClipEntity` reads the entity (group 340, with the flag 0x10000 of group 90, and the viewport among the
entity's reactors), `ClipTo(entity)` clips the viewport to a circle, a whole ellipse, a closed polyline or a closed
spline of its layout, `FitToClip()` fits the frame to the entity again and `RemoveClip()` takes the clip off, erasing
the entity. The frame is the box of the entity and the view stays in place: a viewport 100 by 80 about (100, 100) at 1:2
clipped to a rectangle from (70, 70) to (150, 140) is 80 by 70 about (110, 105) with its view centre at (20, 10) and its
view height 140, as AutoCAD gives; AutoCAD's own DWG of R2000 and R2018 read to the same boxes. In the presenter VPCLIP
asks AutoCAD's prompts (a polygon of points with Arc, Length, Undo and Close as PLINE asks them, or an object picked,
or Delete), one undo step; the viewport shows model space, its grid and its highlight through the outline of the entity
alone, a circle or the arcs of a polyline kept exact to a tenth of a pixel, in the canvas and in the renderer a plot or an image is drawn by;
a double click works through it only inside the outline, and the cursor, the picks and the snaps of MSPACE stay
inside it. The viewport and its boundary are one object to a pick (`CadViewportClips.WithPartners`, AutoCAD's `2 found`):
ERASE, MOVE, SCALE and COPY take both, the viewport follows its boundary when an edit or a grip changes it, a clipped
viewport has its middle as its only grip, and `DisplayViewport2.Clip` (a `DisplayClip2`) carries the outline. A viewport
that is not clipped is drawn and edited as before.

**NEW.** In the presenter the snap and grid are undo steps, as AutoCAD 2021's U takes them, probed in its console: GRID,
SNAP, SETVAR and a snap or grid variable typed alone (GRIDMODE, SNAPMODE, ORTHOMODE, SNAPUNIT, GRIDUNIT, GRIDDISPLAY,
SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL, SNAPISOPAIR) are a step each, named by the command, so that after a line, GRID 50
and SNAP 25 the first U takes SNAP 25 back, the second GRID 50 and the third the line (the three used to go with the
line in one U, and an edit undone used to take a snap or grid set before it along). F7, F9, F8 and F5 pressed at the prompt,
and the status bar's SNAP, GRID and ORTHO buttons, join the step of the command before them, one U for both under that
command's name, and are a step of their own where no command came before. The Drafting Settings dialog's OK is one step,
DSETTINGS. A drawing counts as changed after one, as AutoCAD's does (DBMOD 4). Such a step keeps no copy of the drawing:
it holds the viewport's snap and grid as they were and as they became, and an undo or a redo of it writes those back,
reading nothing again; in a drawing of 100 000 lines F7 takes 0.3 ms and its U 0.2 ms with a few kilobytes kept, where a
step that keeps the drawing keeps 20 MB and takes 205 ms to undo. An undo of another step reads the drawing and puts the
settings changed since on it. New `CadDocument.ApplySetting(name, property, read, change, replay, joinsPrevious)` makes such
a step, and `CadCommandSession.OpenUndoStep()` opens the undo step of the command running, for a window that changes a
setting while a command asks.
**NEW.** `SheetPacker.Pack` takes groups of `GeoPolygon2` as well as of `GeoRectangle2`, for views cut to a polygon by
VPCLIP: the polygons are packed by their own shape, so that an L- or U-shaped view lets another stand in its notch, with the
same `Sheet`, `PackOptions` and `PackResult` (`Pack(IReadOnlyList<GeoPolygon2[]>, Sheet)` and `Pack(..., PackOptions)`).
Each polygon goes where its top is highest, then furthest left, honouring `StartCorner`, inside the usable area and clear
of those already there: found from the no-fit polygon of each pair, made once and moved, with Clipper2; `Spacing` and
`GroupSpacing` are clear gaps between the polygons themselves; a group laid out afresh is packed together first and placed
as one block by the shape of each polygon in it, a group kept as drawn moves as one. Nine views of a drawing (an L-shaped
plan, a U, a pentagon, a hexagon, two more L-shapes and boxes) take one A3 where their boxes take two; 200 views of up to
168 by 119 take 3 A1 sheets against 4 by their boxes (61.7 % filled against 46.3 %), in 0.3 s on .NET Framework 4.8; 200
boxes given as polygons take the sheets the rectangle overload takes, and fewer with spacings (6 against 7). A null
polygon, one not at finite coordinates, one with no area and one that crosses itself are refused with an
`ArgumentException`; one larger than the usable area is not placed. The result is judged polygon by polygon from the
polygons as moved (new `PolygonJudge`), within the tolerance of the options. `PackPlacement.ViewPolygon` is the polygon
moved, and for a box its four corners; `PackPlacement` has a new constructor taking it, and the old one stays. The
rectangle overload is untouched: 1 000 boxes in their order, tight, and in groups of 3 give the same translations to the bit.
A call that passes a bare `null` for the groups no longer compiles, two overloads fitting it: cast it to the type meant. The
guide, `docs/packing.md`, has the pictures.

**NEW.** GeometryHelper DXF has an icon of its own, a white set square and an orange point on a blue tile, at 16, 24, 32,
48 and 256 pixels, on the exe, the taskbar and Alt+Tab, and an icon for the drawing files associated with it; both are
written from their vector drawing by `Icons/make-icons.ps1`. `--register` associates .dxf and .dwg files with the app for
the user alone (`HKEY_CURRENT_USER\Software\Classes`, no administrator): the ProgID `GeometryHelper.Drawing` with the file
icon and the command `"app.exe" "%1"`, in the Open with list of both extensions, and their default only where no program
has them. `--unregister` takes it out and leaves what other programs wrote. `CadAppHost.WindowIcon` sets the window's icon.
**NEW.** `--tab Home` in the screenshot mode of both apps shows the Home tab; a tab name neither app knows exits with 2,
`--tab Nowhere: no such tab.`, where it used to draw the Home tab without a word.

**NEW.** In the presenter the row of a table cell grows while the cell is typed in, as AutoCAD 2021's in-place editor
shows it: each line typed makes the row as high as the words need (in the Standard table style a data cell of two lines
15.07 high instead of 9, of three 22.57), the lines taken out shrink it back, never below the height it stood at, the
rows under it move down with it and the editor stands where the cell now is. The table is drawn from a preview of the
words, `CadTableLive` (`CadInPlaceEdit.TableLive`), and nothing is written to the drawing until the editor closes, which
is one undo step with the rows the preview showed; Esc leaves the table as it was. A key costs the layout of the one cell
(1.4 ms in a table of 50 rows by 20 columns, where laying the whole table out takes 89 ms), and a key that leaves the row
as it was draws nothing anew.

**NEW.** In the presenter the wheel and the middle button go on the ZOOM Previous list, on the paper of a layout, in
model space and through a viewport alike, as AutoCAD 2021's: the rolls and drags made before the view is still are one
entry of the list of the space or the viewport (20 each), and ZOOM Previous goes back through them with the typed ones. A
double click of the middle button, ZOOM Extents, is an entry as typed. `CadDocument.SetViewportView` puts the view a
viewport showed on its list too (the status bar's scale list does, as AutoCAD's ZOOM nXP does).
**NEW.** In the presenter ZOOM, PAN and PLAN typed in model space and on the paper of a layout, ZOOM Previous, and the
wheel and the middle button there are undo steps, as AutoCAD 2021's console, probed: ZOOM 2X, ZOOM 2X and -PAN took one U
each, ZOOM Extents and ZOOM Previous a step each, in model space and on the paper; U and Ctrl+Z move the view back and
REDO and Ctrl+Y forward, in order with the edits between them. The wheel's and the middle button's gestures made one after
another are one step, as through a viewport. A step of the view alone keeps no copy of the drawing, so U takes it back
without reading the drawing again, in a drawing of a million entities too; the drawing counts as changed after a zoom, as
AutoCAD's (DBMOD 16). New `CadDocument.RecordSpaceView(space, view, before, name, realTime)` makes such a step.
**NEW.** ZOOMWHEEL in the presenter, 0 or 1 as AutoCAD 2021's: 1 turns the wheel's direction round, forward zooming out.
Global settings > Canvas and cursor has it as "Reverse the wheel", the settings file as `"wheelZoomReversed"`
(`GlobalSettings.WheelZoomReversed`), and `CadCanvas.WheelReversed` carries it.
**NEW.** The presenter's PLOT prints on the printers of Windows. They are listed in the Printer/plotter list after
GeometryHelper PDF, PNG and JPG (a network printer marked Windows · network) and can be chosen: the Paper size list is then
the printer's own, each sheet as its driver names it at its size, with the margins of its imageable area, and the plot's
sheet stays where the printer has one of its size (ISO A3 on its side becomes the printer's A3, still on its side).
Number of copies, 1 to 999, is for a printer. OK draws the plot as the PDF writer does, with the same page, scale, offset,
turn, plot style table and lineweights, at up to 300 dots to the inch (a sheet that would take more than 40 million pixels,
as A0, at the resolution that keeps it under: 160), and prints it through WPF; "Plot sent to Office printer." or "The plot
was not printed:" and the reason is written on the command line. A layout's plotter, written by Save changes to layout, is
chosen again when the computer has the printer. A printer is a `IPlotPrinter`, which a host or a test gives a queue of its
own for; `WindowsPlotPrinter.Installed()` lists the real ones, and `MainViewModel.WindowsPrinters` takes them.
**CHANGED.** `PlotDialogViewModel` takes its printers as `IPlotPrinter` objects where it took names with a flag for the
network, and `MainViewModel.WindowsPrinters` gives them so; the printers of Windows are no longer greyed in the list.
`CadPlotSettings` has `IsPrinter`, `UsePrinter` and `SavedPlotter`.

**NEW.** Named page setups in the presenter. PAGESETUP opens the Page Setup Manager, as AutoCAD's: the drawing's page setups
with how each plots, the one the layout is set to, and Set Current, New… (a name and what to start with, then the Page
Setup dialog), Modify… and Import… (the page setups of another drawing, a name already taken becoming `Name (2)`). Add… of
the Plot dialog keeps its settings under a name. Each is one undo step named PAGESETUP. In code,
`CadPlotSettings.SaveAsPageSetup`, `SetCurrentPageSetup`, `CurrentPageSetupName`, `ImportPageSetups`, `PageSetupNames` of
another drawing and `CheckPageSetupName` do the same with no window.

**CHANGED.** The Page Setup Manager and its dialogs follow the Design artifact's board (v130), toward AutoCAD 2021's.
The layout's own setup is the first row of the list, `*Layout1*` (`*Layout1 (A2 plan)*` when a named page setup is current,
`*Model*` from model space), chosen when the manager opens, and Modify… on it edits the layout; the blue "Current" tag is
gone. "Current layout" has a layout icon, "Current page setup" stands in the Page setups group, and "Selected page setup
details" is a group of its own with Device name, Plotter, Plot size with its orientation (`594.00 x 420.00 mm (Landscape)`),
Where and Description (`PageSetupInfo`). A box "Display when creating a new layout", unticked, keeps the new system variable
SHOWPAGESETUPFORNEWLAYOUTS (0 or 1, kept by the session; the presenter makes no layouts of its own). Close is orange-red, as in
every manager, with a Help button, and OK comes before Cancel, with a Help button, in New Page Setup, Add Page Setup, Import
Page Setups and the Plot dialog; Help opens the guide's page on plotting on the documentation site. Right-click on a row of the
list gives Set Current, Rename (the name becomes a box to type in) and Delete, one undo step named PAGESETUP each; the layout's
row has Set Current alone. In code `CadPlotSettings.RenamePageSetup` renames a page setup (a layout set to it follows it) and
`DeletePageSetup` takes one out (a layout set to it keeps its settings and is set to none). New Page Setup starts with a list
box of `<None>`, `<Default output device>`, `<Previous plot>` (the last plot or print of the session), the layout's own setup and
the named page setups, in place of a combo that offered every space as `*Model*`. Import Page Setups says "Source drawing name:"
with the full path on its own line, lists the page setups in columns (a tick, Name, Location: Model or Layout, from the flag 1024
a page setup carries, `CadPlotSettings.PageSetupIsModel`) and shows the details of the one chosen; `ReadPageSetup(database,
name, isModel)` reads a page setup of a drawing opened with no document. Add Page Setup has Import… at the left of its footer,
copying page setups of another drawing in and refusing a name they now take. A red frame marks a refused name.
**CHANGED.** The Plot dialog's printer box names where a printer prints: the port of a local printer (`USB001`) or the share of a
network one (`\office-srv\Office printer`) in place of "Printer", and the list says Windows · USB for a printer on a USB port, as
Windows · network for one on the network (`IPlotPrinterPort`, which `WindowsPlotPrinter` implements). Plot to file, plain, is
offered for a printer, unticked, as AutoCAD leaves it; ticked, OK says in the footer "Plot to file is not supported for this
printer" and prints nothing, writing a plot to a file through a printer's driver not being built.

**NEW.** `DxfDBDictionary.SetAt` adds a named page setup, `db.PlotSettingsDictionary.SetAt("A3 plan", new
DxfPlotSettings())`, written as AutoCAD 2021 writes a PLOTSETTINGS with its class listed in CLASSES; `DxfPlotSettings`
has a public constructor, a settable `PlotSettingsName` and `CopyFrom`, which copies every setting of other page settings,
the name with them, as AutoCAD's does (a page setup copied into the layout Model leaves it model space's). A DWG now reads
and writes the PLOTSETTINGS objects, so that named page setups survive a save as DWG: a drawing with "A3 plan" saved as DWG
reads back with its sheet, scale and plotter, and AutoCAD 2021 reads both the DXF and the DWG, its AUDIT finding 0 errors.

**NEW.** The presenter's status bar has a progress meter, as AutoCAD 2021's: opening a drawing shows "Opening plan.dwg"
with a bar from 0 to 100 % and Stop, and SAVE, SAVEAS and QSAVE show "Saving plan.dwg", the status bar drawn while the
save runs. Commands, plugins and hosts start meters of their own with `CadProgress.Start(label)` (on
`CadCommandContext.Progress`, `CadCommandSession.Progress` or `MainViewModel.Progress`), then `SetLimit` and
`MeterProgress`, or `Report` with a share; a meter with neither sweeps, a meter started inside another is shown in its
place until it stops, and a `using` block stops it when the work throws. `CadDocument.Open(path, progress)`,
`CadDocument.SaveAs(path, progress)` and `CadDrawingFile.Open(path, progress)` tell any `IProgress<double>` how far they
have got, from 0 to 1 and never back: a DWG of 100 000 lines about a hundred times in whole percent, with the same bytes
written and read and the same times as without.
**NEW.** In the presenter Ctrl+2 shows the Drawings list when it is hidden and hides it when it is shown. It is a
default key of the Shortcuts page of Global settings, under DRAWINGS, and can be given another key like any other. A
settings file saved before keeps its own table of keys, and now lists the default keys it has seen (`knownDefaultKeys`);
a default key not on the list is added once when its key is free and no key is bound to its command, so an old file
with Ctrl+1 and F3 alone has Ctrl+2 after it is read, while a Ctrl+2 cleared or given to another command stays so.

**FIXED.** In the presenter Ctrl+1 only showed Properties; as AutoCAD's does, it now hides it when it is shown and shows
it when it is hidden, so that one key runs both ways. PROPERTIES typed still shows Properties and PROPERTIESCLOSE still
hides it.
**NEW.** A polyline's grips in the presenter are AutoCAD 2021's multi-functional ones. The grip in the middle of a
segment is a bar along it, 22 by 6 pixels at the default size (along the chord of an arc segment, turning with the
view), the vertices squares 15 across; a grip under the cursor is pink, as AutoCAD's GRIPHOVER, and a clicked one red.
The cursor resting 400 milliseconds on a vertex opens a menu of Stretch Vertex, Add Vertex and Remove Vertex; on the
middle of a segment Stretch, Add Vertex and Convert to Arc (Convert to Line on an arc segment). Add Vertex puts the
vertex after the grip's, the polyline drawn with it at the cursor, and splits an arc segment into two arcs of the arc
through the new point; Convert to Arc asks for the arc's midpoint (a chord of 100 with the point 20 off its middle gets a
bulge of 0.4); Remove Vertex is grey on a polyline of two vertices. Each is one undo step, with AutoCAD's prompts, and Esc
changes nothing. New in the library: `CadGripMenus.Of`, `CadGripAction`, `CadCommandSession.StartGripAction` and
`CadGrips.AddVertex`, `RemoveVertex`, `ConvertToArc` and `ConvertToLine`; a segment middle's `CadGrip.Direction` is now
the way the segment runs. Ctrl cycling through a grip's options is not built, and the other entities' grips have no menu yet.

**NEW.** Global settings › Appearance › Cursor has a Grip size row, right below Pickbox, and the system variables
GRIPSIZE (1 to 255, 5 by default; a plain grip is a square 2 × size + 5 pixels across, 15 at 5 and 25 at 10) and
GRIPMULTIFUNCTIONAL (0 to 3, 3 by default; bit 2 turns the hover menu on) are kept in the settings file
(`"gripSize"`, `"gripMultifunctional"`). The grips of tables, MTEXT and dynamic blocks scale with the size, so at the
default they are about twice as large as the fixed 8 pixels they had.

**CHANGED.** The triangle grips of an MTEXT (width, height, corner, column grips) and of a table (width, height, size) now
scale from GRIPSIZE like the squares: 15 pixels across at the default size of 5, 25 at 10, where they were a fixed 10 and
12; the table's breaking triangle and the autofill diamond of a selected cell grow with the size too, and are as large as
before at the default. Under the cursor these triangles, a dynamic block's custom grips and a table cell's grips are
AutoCAD's pink (`#FF7F7F`, GRIPHOVER) as the squares are, a clicked grip the hot red; they were hot red as soon as the
cursor touched them.
**CHANGED.** Stretch Vertex from a polyline vertex's hover menu says `** STRETCH **`, as a click on the grip does, where
it said `** STRETCH VERTEX **`: AutoCAD 2021's table of the polyline's grip modes gives Stretch Vertex that line.
**NEW.** Add Vertex of a polyline's hover menu offers Undo: `Specify new vertex point or [Base point/Copy/Undo/eXit]:`.
With Copy on, Undo takes the last vertex made back while the command runs, the arc segment it split given back with its
bulge and widths, and the next vertex is asked after the one before; the whole run stays one undo step. Remove Vertex
beside an arc was checked against AutoCAD's own wording ("replace neighboring segments with line") and already left one
straight segment, with a test now for an arc either side and for the closing arc of a closed polyline.

**NEW.** In a layout of the presenter, every viewport whose grid is on draws it in its frame, whether it is worked
through or not, on the paper and through another viewport, as AutoCAD 2021 keeps a grid by viewport. Only the grid of
the viewport worked through was drawn.

**FIXED.** Through a viewport the presenter's grid is model space's, drawn in dots by GRIDSTYLE 1 (2D model space)
rather than by 4 (layouts), and a twisted viewport keeps it within LIMMIN and LIMMAX as they are turned on the sheet:
through a viewport at 1:500 twisted by 30 degrees, with limits 2 000 by 2 000 about the middle of its frame, the line
900 above the middle is drawn, where it was cut off by the level box of two corners of the limits.

**FIXED.** SNAPMODE 2 in the presenter is kept by each space, as AutoCAD 2021 keeps it by viewport: set in model space,
it reads 2 there again after SNAPMODE 1 is set on the paper of Layout1. It was one value for the window, and 1 set
anywhere cleared it everywhere.

**FIXED.** U and REDO in the presenter show the snap and grid the drawing holds again, of model space and of each
viewport: after a line drawn and GRID 50 typed through a viewport, U shows that viewport's grid off and of the spacing it
had, as the drawing read back holds it. SNAP, GRID and their spacings stayed as they were before the undo.
**NEW.** The canvas's Edit menu takes in AutoCAD 2021's object menus for the objects selected, as AutoCAD merges them:
those of its menu file by kind, the one for a single object or the one for several of a kind, none for objects of two
kinds. One text gives Edit..., one polyline Polyline (Close, Open, Join, Reverse, Width, Curve Fit, Spline Fit and
Decurve, each PEDIT run on it), dimensions Dimension Style and Precision (0 to 0.000000, set as Properties sets it), one
table AutoCAD's table menu (Size Columns Equally: a table of columns 100, 40 and 40 wide gets three of 60, one undo
step; Size Rows Equally, Remove All Property Overrides, Table Indicator Color...), viewports Display Viewport Objects,
Display Locked and Remove Viewport Overrides for All Layers, and hatches, block references, xrefs and images theirs; what
the presenter does not build (Annotative Object Scale, Table Style, Export..., the spline menu) is grey, "Not built
yet.". The Command menu offers Recent Input, the points or numbers typed at prompts of the kind asked, newest first.

**NEW.** PAN and ZOOM run inside a command, typed as 'PAN and 'ZOOM or chosen from the Command menu, whose Pan and Zoom
are no longer grey: their prompts after ">>", then "Resuming LINE command." and the command's prompt again, the objects
"Select objects:" gathered kept, as AutoCAD 2021 runs them (probed in its console); one nests in the other. Another
command typed with an apostrophe gets "** That command may not be invoked transparently **".
`CadCommandSession.IsTransparent` and `CadCommandSession.RecentInput` are new.

**NEW.** SHORTCUTMENU, AutoCAD's bits 0 to 31, 11 by default, kept in the settings file as
`GlobalSettings.ShortcutMenu`: 1 the Default menu, 2 the Edit menu, 4 the Command menu at every prompt, 8 where the
prompt offers options, 16 a right click held a quarter of a second for a menu and a shorter one Enter. The canvas and
`MainViewModel.CanvasMenu` honour it; `CadCanvas.ShortcutMenu` and `CadPicking.WantsCommandMenu(session, shortcutMenu)`
are new.

**FIXED.** A point typed at "Select objects:" is written after the prompt, "Select objects: 50,0" then "1 found"; one on
nothing gives "0 found" and asks again, as AutoCAD 2021 does, where a script stopped at it. Point Filters of the
Shift+right-click menu draws AutoCAD's line after .Z.
**NEW.** `DwgReader` reads REGION, 3DSOLID and BODY of AutoCAD 2004 to 2010, whose ACIS data the file keeps as SAB: the
SAB is turned into the SAT AutoCAD writes of it in the DXF of those versions, line for line, its head (`20800 93 2 0`
and `16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0`), each flag as AutoCAD's word for it and each real to 19
significant digits as `%.19g` writes it (25.4 is `25.39999999999999858`). Twenty solids and regions of AutoCAD 2021, a
box, a loft, a sweep, booleans and regions of splines among them, come out as AutoCAD's DXF of them in each of the
three versions. A record or a subtype of spline the converter does not know leaves its entity out, as before, counted
in the Debug line of the types not read.

**NEW.** `DwgReader` reads SHAPE. `DxfReadOptions.SupportFolders` lists the folders a DWG's files of shapes are looked
for in, and `DwgReader.Open(string, DxfReadOptions)` takes them as `Read` does: with the file of its style found, a
SHAPE gives its name (group 2), else its number (group 70), as AutoCAD writes a shape whose file it does not find.
`DwgWriter` writes a SHAPE that gives its number, under 70 or as a number in group 2, of the first text style of a file
of shapes; one given by its name alone is still left out.

**FIXED.** A line of SAT longer than 255 characters, read from a DWG of AutoCAD 2000, comes out as AutoCAD writes it: in
pieces of 255 under group 3, the last piece under group 1, where it came as group 1 then 3. A line that goes on a
record starts with a space where the file has a tab, as AutoCAD writes it.

**NEW.** `DwgReader` reads the detail and section view styles every drawing of AutoCAD 2013 and later holds, in every
version AutoCAD saves them, and the manager of the persistent names of subentities AutoCAD keeps beside solids, each as
AutoCAD's DXF of it gives: a section view style of its own, its identifier 0.33 high, bend lines overshooting by 0.11 and
a hatch of ANSI37 60 % seen through at three angles, comes out as AutoCAD wrote it in AutoCAD 2000 to 2018. The Debug
line of the types not read no longer names them.

**NEW.** `DwgWriter` writes the detail and section view styles and the manager of subentities, so a new drawing of
AutoCAD 2018 is written whole, with nothing left out; and REGION, 3DSOLID and BODY with their ACIS data, where the
section ACDSDATA of the DXF holds it as SAB (a DXF of AutoCAD 2013 and later): the data store of the file is laid out as
AutoCAD lays it out, a record past 256 KB in pages of its own. A box, a sphere, a cylinder and a region, and a plate of 400 holes of
515 873 bytes of SAB, are written in AutoCAD's own bits but 12 bytes AutoCAD leaves unset; AutoCAD opens them with
`AUDIT` 0 and its DXF of each is the DXF they were written from. A solid of SAT text, as a DXF of an older version gives
it, is still left out, and so is SHAPE.

**NEW.** The Text Editor tab's Bullets and Numbering menu and the editor's right-click Bullets and Lists work as AutoCAD
2021's: Off, Numbered, Lettered (Lowercase, Uppercase) and Bulleted label the selected paragraphs, or the caret's, one
undo step each, "Plate" and "Beam" numbered written `\pxi-3,l3,t3;1.\tPlate\P2.\tBeam`; the ribbon's menu ticks the
label every selected paragraph has, none where they differ. Start, Continue and Allow Auto Bullets and Numbering are
grey, not built, and Allow Bullets and Lists grey and ticked. `CadMTextEditor.NumberingOf` gives a paragraph's label.

**NEW.** An MTEXT in columns has AutoCAD 2021's grips, where it had its location alone: the column width at the first
column's top right, the corner and the height, or for dynamic columns of their own heights a height under each column,
and the gutter at the last column's top right, each doing what AutoCAD's MoveGripPointsAt does (probed on static and
dynamic columns held at the top left, the middle and the bottom right). Two static columns 120 wide with a gutter of 20
keep their total of 260 when the column width grip is dragged, the gutter taking the rest; the gutter grip dragged 50
out makes the gutter 70. New grip kinds `MTextColumnWidth`, `MTextHeight`, `MTextSize`, `MTextGutter` and
`MTextColumnHeight`; one undo step each, through the grip edit too.

**CHANGED.** An MTEXT's grips are drawn as AutoCAD 2021 shows them: the location a square, the width a triangle pointing
along the MTEXT, the height one pointing down and the corner one pointing into it, turned with the MTEXT, where all were
squares; the corners of an MTEXT of no columns carry the way they point in `CadGrip.Direction` and drag as before.

**NEW.** The presenter's canvas shows AutoCAD 2021's right-click menus, as its default SHORTCUTMENU 11 has them, their
items, order and access keys read from AutoCAD's own menu file: with nothing selected the Default menu (Repeat LINE,
Clipboard, Undo, Redo, Pan, Zoom, Quick Select..., Find..., Options...), with objects selected the Edit menu (the items of
the objects, such as Reset Block, then Clipboard, Erase, Move, Copy Selection, Scale, Rotate, Draw Order, Group, Deselect
All, Quick Select..., Find..., Properties), while a command offers options the Command menu (Enter, Cancel, the options,
"Close" and "Undo" at LINE's third point, and Osnap Overrides), and while a grip edit runs the Grips menu (Enter,
Stretch, Move, Rotate, Scale, Mirror, Base Point, Copy, Reference, Undo, Exit). What AutoCAD has and the presenter does
not, as Isolate, SteeringWheels and QuickCalc, is grey. `MainViewModel.CanvasMenu` gives the items; `--dialog
canvas-menu` draws the menu in screenshot mode.

**CHANGED.** A right-click while a command runs is Enter only where the prompt offers no options, as at "Specify first
point:" and "Select objects:"; where it offers some, as LINE's "Specify next point or [Undo]:", it shows the Command menu,
whose first item is Enter, as AutoCAD 2021's does by default.

**NEW.** The implied window works inside a command's "Select objects:" as AutoCAD 2021's does: a click on nothing asks
"Specify opposite corner or [Fence/WPolygon/CPolygon]:" after "Select objects:" on the same line, the next click or a
corner typed ends the box, F, WP or CP turns it into a fence or a polygon from the corner, and what it takes is gathered
for the command ("2 found"), or taken out at "Remove objects:"; Esc cancels the command. `CadCommandSession.StartWindow`
starts it there too, where it returned false.

**CHANGED.** The Shift+right-click menu names Apparent Intersect and Insert as AutoCAD 2021's menu does, where it said
Apparent Intersection and Insertion, and parts its snaps into AutoCAD's three groups by two lines, after Extension and
after Tangent. The OSNAP menu of the status bar and Drafting Settings keep the longer names, as AutoCAD's do.

**FIXED.** A member given a shape or a number that is not finite answers as the shapes of NaN do, or refuses the
argument by its own name through an `ArgumentException`; it no longer throws from inside, naming a radius, a box size, a
control point or a normal of its own working, nor runs on for ever. `Chamfer` of a polygon or a chain with a vertex of
NaN ran on for ever (24 members of the fuzz), and names `polygon` or `polyline` now. `Translate` and `TransformBy` of a
polygon, a face or a body in space, a spline, an edge or a loop of arcs refuse a vector or a transformation that is not
finite, as their constructors refuse such a point, where the square 100 across moved by (NaN, 0, 0) came back with
vertices of NaN that every query then threw for. A box with a corner of NaN is the box about a centre of NaN to `ToObb`,
and meets nothing: the box from (NaN, 30, -10) to (80, 70, 20) gives no points with an arc and is NaN from a segment,
where 273 members threw for a box size of NaN. `TrySplitAt` of an arc, `ConvexHull2.TryOf`, `TrySplitAtParameter` of an
edge, `TryExpand` of a box, `TryGetIntersectionLine` of planes, `TryToPoint2` and `TrySplitBy` by a segment of NaN answer
false, where they answered true with values of NaN or threw; a spline meets no arc, circle or ellipse about a centre of
NaN; a triangle with a corner of NaN meets no body and is NaN from a polygon or a face. Factories and constructors name
the argument: `GeoRay2(origin, through)` names `through`, `GeoPlane3.FromThreePoints` the point, `GeoArc3` its
`endAngle`, `GeoSolid3.Extrude`, `Revolve`, `Sweep` and `Pipe` the profile, the placement or the path, and the forms of
`Chamfer`, `Extend` and `Offset` taking one distance name `distance`. Of 9 950 public members fuzzed with one argument
NaN or infinite, 1 700 threw from inside, hung or answered a `Try` with NaN before, and 33 do now, most of them a
member of a shape of NaN with no argument of its own to name, all throwing and none hanging; every finite call of the
fuzz gives the same bits as before.

**CHANGED.** The last 33 of those members throw what they mean. A member of a shape with a coordinate that is not a
finite number that has to make something of the shape itself throws an `InvalidOperationException`, "The shape has a
coordinate that is not a finite number.", where it threw an `ArgumentException` naming vertices said collinear, a normal
of no length or a distance of its own working: `ToPolygon` of a circle about a centre of NaN, of a chain or a triangle in
space with a corner of NaN, `ToFace3` of such a triangle, `ToSolid` and `TriangulateSurface` of a ball, `Extend` and
`ExtendToLength` of an edge that is an arc, `GetPlaneWith` of a segment, `OffsetInPlane` of a curved chain, `ClipTo` of a
line without ends, and `GetShortestLineTo` a loop of arcs from an arc, a circle, a chain or a curved chain. A writer
handed such a ball still names `sphere`. `ClipTo` names `rect` for a rectangle of NaN, where it named a distance `from`;
`GeoPolygonArc3(frame, loop)` names `frame` or `loop`, where it said its vertices were out of one plane; and
`GeoPolylineArc3` names `vertices` for an edge that bulges from a vertex of NaN, where it named `startPoint`. The
documentation of `GetPlaneWith` gives the `ArgumentException` it has always thrown for a segment of no length or a
direction along it, not an `InvalidOperationException`. The guide now says the part of a shape that is not finite meets
nothing, not the whole shape: the square 100 across with one corner moved to (NaN, 0) still crosses the segment from
(50, 50) to (150, 50) at (100, 50). The fuzz finds none of the 33 left; its 9 950 finite calls give the same bits as
before, under .NET Framework 4.8 and .NET 10.

**NEW.** The presenter's implied window asks on the command line as AutoCAD 2021's does: a click on nothing at the
Command prompt shows "Specify opposite corner or [Fence/WPolygon/CPolygon]:", and DYN shows it with two fields, the X and
the Y of the cursor itself, where it showed nothing. The next click, or a corner typed (110,60), ends the box; F, WP or
CP turns it into a fence or a polygon from the first corner until Enter; Esc writes "Command: Specify opposite corner or
[Fence/WPolygon/CPolygon]: *Cancel*". Its corners take no object snap. `CadCommandSession.StartWindow`,
`EndWindow`, `IsWindowing` and `IsWindowOpen` drive it.

**NEW.** The presenter's cursor carries AutoCAD's badges: while a box is dragged a square 9 pixels across, 12 pixels
right of the cursor and 21 above, filled blue for a window and dashed round a green middle for a crossing; while SCALE
runs, a square with a smaller one filled in its lower left corner.

**CHANGED.** No point prompt of the presenter marks its base point with a green square any more, as none of AutoCAD
2021's does: SCALE's factor shows the amber rubber band alone from it.

**CHANGED.** The Shift+right-click menu takes the access keys of AutoCAD 2021's own menu: t for Mid Between 2 Points and
for Point Filters, g for Tangent, and none for Geometric Center, where it had 2, T and G. Reset Block of the canvas's
right-click menu takes R.

**CHANGED.** The AutoComplete list shows five rows at a time, at the command line and under the box of dynamic input,
the rest scrolled to; the row the arrows choose is scrolled into sight in both, where the one at the cursor did not
follow it.

**FIXED.** The presenter measures the ink of SHX letters as AutoCAD 2021 does, for an MTEXT's lines and a dimension's
words: an arc of less than a half circle reaches the rectangle of its chord and of the line along it touching the arc, a
longer one the square about its circle, so that isocp.shx's 3 is 0.441421 of the height wide where its strokes are 0.4
(31 of the 94 letters of isocp, isocp2, isocp3, isoct, isoct2 and isoct3 were measured narrower); where the pen is put
down counts though it moves no further, monotxt.shx's 1 half the height wide, not a third; and ink left of the pen is not
counted, as in a TrueType font, italic.shx's f 0.809524 wide, not 0.857143, at any oblique angle. Words whose ink has
no width, as "I" of isocp.shx, are measured nought wide, where the picture took 0.7 of their height a letter. Of 1 422
words in 13 SHX fonts measured in AutoCAD, 17 differ now, all words of spaces alone, a hard space or letters the font
lacks, where 308 did.

**NEW.** DIMCONTINUEMODE, kept by the session and 1 as it starts: set to 0, DIMCONTINUE and DIMBASELINE draw their rows
in the current dimension style, overrides and DIMSCALE on the current layer, whatever DIMLAYER or DIM's LAyer names, as
AutoCAD 2021 does; DIMCONTINUE still writes `279 1`.

**FIXED.** A radius and a diameter the presenter makes record their words (group 11) as AutoCAD 2021 does: under DIMTMOVE
1 by the point they were put at, "R20" of Arial put 40 out at 30 degrees in acad.dwt's Standard writing (34.641, 20)
where its middle is (35.440, 20); under DIMTMOVE 0 by the point of the line or landing they stand by, without DIMTAD's
lift. Words put off the line under DIMTMOVE 2 stand level where DIMTIH says so, whatever DIMTOH says, the line broken
about their box; words over the rim, from inside or out, put the arrowheads outside with the centre marked where DIMTOFL
is off, as over a radius's centre. Words kept level outside on a line within 15 degrees of level stand with no landing, a
gap past its end, on a radius and on a linear dimension alike, as AutoCAD 2021 draws them from 5 to 15 degrees: of 1 152
aligned dimensions under DIMTMOVE 0 and DIMTOH, 24 are placed otherwise now, at exactly 15 or 90 degrees off level, where
AutoCAD's own rounding decides, where 104 were; 1 584 radius and diameter dimensions under DIMTMOVE, DIMTOH, DIMTIH and
DIMTAD match AutoCAD's group 11, words and lines.

**CHANGED.** The presenter keeps the layout of each MTEXT from one drawing to the next, REGEN too, and lays the words out
again only where the words, sizes, spacing, columns, the style's width and slant or the faces of the fonts have changed;
the picture is the same to the bit. 10 000 MTEXTs are drawn again after a REGEN in 128 ms under .NET Framework 4.8,
where they took 196 ms; a document drawing them for the first time takes 233 ms, 30 ms more than before, for the
layouts it keeps.

**FIXED.** The presenter drew a TEXT's `%%U`, `%%O` and `%%K` as written. In an SHX font they now draw the lines AutoCAD
2021 draws, under, over and through the letters after them, 0.2 of the height below the baseline, 1.2 heights and half
a height above it, along the letters' pens but 0.15 of the height left of them, and a justified text is boxed with its
lines: "%%uAB" of simplex 10 high centred on nought starts at -8.738, not -8.571. In a TrueType font the codes are left
out; its lines are not drawn yet.

**FIXED.** EXPLODE of an MTEXT gave every TEXT the MTEXT's colour and style. A run of its own colour keeps it (`\C1;`
red, `\C256;` ByLayer, `\C0;` ByBlock, `\c16711680;` the true colour 0,0,255), and a run of an SHX font of its own
(`\Ftxt.shx;`) is a TEXT of a style `MtXpl_txt_shx` of that font, added to the drawing, as AutoCAD 2021's Explode makes
them; a run of a TrueType font of its own stays in the MTEXT's style, as there.

**FIXED.** The MTEXT editor read a width in times (`{\W1.2x;ABC}`) as times 1 in a style of another width, and an edit
wrote it back so: in a style 0.8 wide the letters drawn 0.96 wide became 1.2 wide. They are 0.96 in the editor and are
written `\W0.9600;`. New: a `CadMTextEditor` constructor taking the style's width, and `CadMTextEditor.StyleWidth`.

**FIXED.** MTEXT's Columns option opened the editor with no columns and wrote its own columns whatever the editor did. The
editor opens with them (`CadInPlaceEdit.Frame`), and the MTEXT is written with the columns the editor ends with, No
Columns too.

**FIXED.** In the MTEXT editor, End on a line that breaks between two ideographs put the caret at the start of the next
line, its place being the first of that line. It stands at the end of the line, where Home, Up and Down go on from, and
a click past the end of such a line puts it there too (`CadMTextEditBox.CaretLine`).

**NEW.** `RibbonPanel.Launcher` and `RibbonPanel.LauncherToolTip`: the small arrow at the right of a panel's title that
opens its dialog, drawn on the title bar of the drop-down of the panel collapsed too, as AutoCAD 2021's. The Text
Editor tab's Paragraph panel opens the Paragraph dialog from either.

**NEW.** `CadMTextEditor.SetNumbering` and `CadMTextNumbering`: paragraphs numbered, lettered or bulleted as AutoCAD
2021's Bullets and Numbering writes them, "Plate", "Beam" numbered `\pxi-3,l3,t3;1.\tPlate\P2.\tBeam` and bulleted with
`{\fSymbol|b0|i0|c2|p18;·\t}`; Off takes the list away. The ribbon's menu does not offer it yet.

**NEW.** `DwgReader` reads the MATERIAL and VISUALSTYLE objects of every version, AutoCAD 2000 to 2018, into the
groups AutoCAD's own DXF of them gives: of a material what differs from a new material's values (a red of its own,
RGB 200, 30, 30 at 0.8, glossy at 0.3, gives 91, 44 and nothing of the maps it has not), of a visual style the form of
its version, from AutoCAD 2013 its 58 properties each with how it is applied. Of AutoCAD 2013 and later it reads the
data store too, which holds the ACIS data of the solids, into the section ACDSDATA AutoCAD writes: a record of SAB
bytes for each REGION, 3DSOLID and BODY, a large one (515 873 bytes for a plate of 400 holes) from the pages it is kept
in. Before, the materials, visual styles and ACIS data of the drawing were left out. `DwgWriter` writes MATERIAL and
VISUALSTYLE (of the form of AutoCAD 2013 and later): AutoCAD opens such a drawing with `AUDIT` 0 and its own DXF of it is
the one it was written from, and the 24 visual styles of a new drawing are AutoCAD's own bits.

**CHANGED.** The presenter drags objects as AutoCAD 2021 does, after the maintainer's pictures of its SCALE: while
MOVE, COPY, ROTATE, SCALE, MIRROR, ALIGN, an array or a grip edit waits for its point, the copy at the cursor is drawn in
the objects' own colours, not dashed blue, and the rubber band from the base point is dashed amber, 16 pixels on and 9
off. At MOVE's second point and at ROTATE's angle and SCALE's factor without Copy, the objects are drawn faded where they
stand, a fifth of their colour (a white line on black a grey of 51), rather than highlighted. SCALE's box at the cursor
shows the factor alone, 0.50 with the cursor 0.5 from the base point, where it showed "0.50 < 306.9°". A selection
window is blue and a crossing green, a quarter strong over the drawing, where they were a tenth; both are edged in the
crosshair's colour, the crossing dashed 4 on and 2 off. While a window, a crossing or a lasso is dragged the cursor is
the cross alone, without the pickbox, which comes back once the box ends. The window, crossing, fence or polygon an
option of "Select objects:" draws is drawn alone, without the rubber band and the base point square it had from its
first corner.

**FIXED.** The presenter's screenshot mode showed the real dialog of a command its `--run` typed, as Write Block for
`--run W`, and waited on it: the window hooked its dialogs again once it was given the view model. Such a command asks on
the command line now, as the rest of the screenshot mode does.

**NEW.** `CadMenuItemViewModel.AccessKey`: the Shift+right-click menu takes AutoCAD's access keys while it is open, F for
From, E for Endpoint, r for Nearest and the rest, the letter underlined. `CadPreview.FadesOriginals` says whether a preview
draws the objects it moves faded where they stand.

**NEW.** The MTEXT editor's ruler works as AutoCAD 2021's: the tab type box (Left, Centre, Right, Decimal), a click on
the ruler adding a tab stop at the nearest minor tick, stops dragged along it or off it, the first-line and hanging
indents, the width arrows, and with columns a column's width, the gutter, the total and the height grips; each release
one step of the editor's undo, Esc during a drag putting it back; a right-click gives Set Mtext Width and Set Mtext
Height. The Paragraph dialog, the Column Settings dialog, the Paragraph panel's launcher and the Columns menu are built,
and the paragraph codes are written as AutoCAD's own editor writes them: in an MTEXT 2.5 high a centre stop at 20 is
`\pxtc8;`, a decimal one on a comma at 40 `\pxtD,16;`, a first line at 2 with the rest at 10 `\pxi-3.2,l4;`. New in
`GeometryHelper.Dxf.Viewer`: `CadMTextParagraphFormat`, `CadMTextTabStop`, `CadMTextFrame`, and on `CadMTextEditor`
`FormatOf`, `SetFormat`, `Frame`, `SetFrame`, `BeginChange`, `EndChange` and `InsertColumnBreak`.

**CHANGED.** Line Spacing on the Text Editor tab sets the selected paragraphs' own multiple (`\pxsm1.5;`), as AutoCAD
2021's does, where it set the MTEXT's line spacing factor; More… and Clear Paragraph Spacing are added.

**FIXED.** A decimal tab stop on a comma (`\pxtD,16;`) was read as a left stop at 16 heights; the word after the tab is
set on its comma now, as AutoCAD 2021 sets it, and one with no comma by its end. An MTEXT edited in the MTEXT editor
wrote its column breaks (`\N`) back as paragraph breaks; they are kept.

**FIXED.** `DistanceTo` of a `GeoTriangle3` and a point measured a triangle nearly on one line by a foot along a normal
that is the rounding of its cross product: the point 5E-14 from two corners 1E-13 apart, of the triangle (35, 54, -67),
(88, 96, -11), (88, 96.0000000000001, -11), was measured 2.12 away. Such a triangle, no higher than about 3E-8 of its
length, is measured to its sides now, within its height. `GetClosestPointOnBoundary` and `GetShortestLineTo` of a point
check the point the Voronoi regions find on a long thin triangle against that gap and replace it where it is further
off than the triangle is high: from a point 0.00004 from a triangle 9 000 long and 0.0024 high the point given was 2.08
away. Of 400 000 triangles and points of a fuzz, every distance of a triangle with an area at a thousandth is the same
bit for bit, and 3 174 closest points changed, each one 1E-8 to 26 too far before and none further off now.

**FIXED.** `Fillet` and `TryFilletAt` of `Corner3` took a radius of infinity, alone or in a list; they refuse it with
an `ArgumentOutOfRangeException`, as the corners in the plane do.

**FIXED.** A shape with a coordinate that is not a finite number meets nothing, in the plane and in space:
`GetIntersections` gives no points and the `Try` members answer false, where 352 members gave points of NaN to a fuzz
that gave one shape a NaN or an infinite coordinate. The segment from (NaN, 20) to (150, 30) crosses no side of a
triangle, a polygon, a face or a rectangle; a segment, a ray, a line, a chain or an edge of NaN in space meets no plane,
triangle, polygon, face or segment, nor does a plane of NaN; an arc, a circle or an ellipse about a centre of NaN meets
no flat shape or plane; and `TryOffset` of an arc, a circle or a rectangle about such a centre, or of an arc by a
distance of NaN, `TrySplitAtDistance`, `TryChamferAt`, `TrySplitBy` of a curved chain, `TryExtendTo` and
`TryTrimExtendToCorner` in space and `TryGetBarycentric` of a point of NaN answer false. A spline in the plane and a chain
or a triangle with a corner of NaN or at infinity meet nowhere, where under .NET Framework 4.8 the search never ended
(24 members of the fuzz hung). Every finite answer is the same bit for bit.

**FIXED.** A dynamic block saved from the presenter's Block Editor (BSAVE, or BCLOSE with Save) has the anonymous *U
blocks its INSERTs place worked out anew from the new definition, at each INSERT's values, as AutoCAD 2021's
`UpdateAnonymousBlocks` works them: GH_PANEL, whose INSERTs of Width 150 and 200 placed *U1 and *U2, given a circle of
radius 10 at (50, 25) and saved, has them place new blocks *U3 and *U4, stretched to 150 and 200 with the circle, the
INSERT that shared *U1 sharing *U3, each INSERT keeping its values, and *U1 and *U2 left placed by none, as AutoCAD leaves
them. One undo takes it back with the save. The INSERTs kept showing the block as it was before the save.

**NEW.** LIST writes the overrides of a dimension's style as AutoCAD 2021 does, for every type of dimension: "dimension
style overrides:" after the style, then each variable by name in alphabetical order (" DIMASZ      5.0000",
" DIMCLRD     1 (red)", " DIMBLK      Dot", " DIMJOGANG   30", " DIMSE1      On"), distances in the drawing's units with
nought feet and nought inches after whole feet left out (5", 1', 1'-3 1/2"), DIMSCALE and the other factors to LUPREC
places, DIMJOGANG in the drawing's angle units, arrowheads by AutoCAD's names and DIMFXLEN as DIMFXL.

**FIXED.** A dimension's override of DIMJOGANG is read and written in radians, as AutoCAD 2021 keeps it in the list
DSTYLE: 30 degrees is `1040 0.5235987755982988`. The library read and wrote degrees there, so a dimension AutoCAD gave a
jog angle of 30 degrees read as 0.0091 radians, and one the library or DIMJOGGED wrote at 45 degrees reached AutoCAD as 45
radians. A DIMSTYLE record keeps degrees, as before.

**FIXED.** ATTSYNC of a dynamic block, by Name or by Select, brings in line the INSERTs of its anonymous *U blocks too,
against the dynamic block's own definitions, each keeping its dynamic properties: an attribute an action moves stands
where the INSERT's values put it. GH_PANEL with TAG locked in a stretch of its Width, an INSERT at (1 000, 500) set to a
Width of 150, and the block then given TAG 5 high and a new definition NEW at (60, 25): the INSERT keeps its *U block and
its Width of 150, TAG at (1 145, 520) 5 high, NEW at (1 060, 525). These INSERTs were left as they were. BATTMAN's Sync
and its count of INSERTs found take them in alike.

**FIXED.** LIST writes as AutoCAD 2021 does, checked in accoreconsole: a jogged radius its type, "jogged", and its centre,
chord point, override centre and jog point, where it wrote the rows of the Properties palette; a rotated dimension in a
turned UCS its rotation and obliquing angle counted from the UCS's X axis, so that one turned 30 in a UCS turned 30 is
"horizontal" and one turned 90 "rotation: 60"; and each object's kind as AutoCAD names it, two places before "Layer:",
where a long one ran into it ("MULTILEADERLayer:"): MULTILEADER, LWPOLYLINE for a lightweight polyline, POLYLINE for a 2D
one, BLOCK REFERENCE for a MINSERT, ACAD_TABLE, 3D FACE, the record's type for the others (IMAGE, MLINE, 3DSOLID). Its Y
ordinates, its rotations of 180 and 270 degrees and "associative: yes" were already AutoCAD's, and are now pinned by tests.

**FIXED.** In a layout's paper space of the presenter, the snap and grid variables typed (GRIDUNIT, GRIDDISPLAY,
GRIDMAJOR, SNAPUNIT, SNAPMODE, SNAPBASE, SNAPANG, SNAPSTYL, SNAPISOPAIR) and SNAP and GRID set the layout's own, kept in
its viewport 1 as AutoCAD 2021 keeps them, and the snap on the paper steps by that viewport's spacing: GRIDUNIT 3,3 typed
in Layout1 writes 3 by 3 into its viewport 1 and leaves model space's 4 by 6 as it was. They changed model space's.

**NEW.** Through a viewport (MSPACE) the snap and grid of the presenter are that viewport's own, as AutoCAD 2021 keeps
them: GRID, SNAP and their variables typed there write into it, and its grid is drawn inside its frame, laid in model
space as the viewport shows it. A viewport of the room at 1:500 with a grid of 1 000 draws a line every 2 units of paper
in its frame alone. Model space's snap and grid were used, and no grid was drawn through a viewport.

**FIXED.** A point typed at a SETVAR of the presenter, as SNAPBASE 3,4 or GRIDUNIT 6,7, leaves the last point where it
was, as AutoCAD 2021 leaves LASTPOINT: after ID 10,20 and SNAPBASE 3,4, SNAPUNIT @2,3 is 12,23. It was 5,7.

**FIXED.** A linear dimension the presenter makes with its words past a leader under DIMTMOVE 1 writes their start as
group 11, level with their middle, as AutoCAD 2021 writes it, where the presenter wrote their middle: the "0.5000" of the
dimension of 0.5 DIMCONTINUE adds after one of 100 in acad.dwt's Standard, its middle at (100.8933, 20.4088), is written
as (100.52, 20.4088). Words of DIMTMOVE 0 and 2, words too near the line for a leader, and those of the other kinds keep
their middle, as in AutoCAD.

**FIXED.** DIMCONTINUE and DIMBASELINE draw at the DIMSCALE of the dimension they go on from and put the new dimensions on
its layer, as AutoCAD 2021 does under DIMCONTINUEMODE 1, where the presenter took the current DIMSCALE and layer (and DIM's
LAyer): from a dimension made on L1 at DIMSCALE 2 they go on L1 and write `1070 40, 1040 2` with L2 current at 1, a
baseline row twice DIMDLI further out each time; linear, angular and ordinate rows alike.

**CHANGED.** `GetIntersections` and `TryIntersectWith` of a triangle, a polygon, a face or a plane with a `GeoObb3` or a
`GeoAabb3` flat on some axis, or no thicker than the point tolerance on two, read the box as its rectangle, its segment
or its point, as `CollidesWith` and `DistanceTo` read it, where they gave no points: the plane x = 30 crosses the box
100 by 100 by 0 about (50, 50, 0) at (30, 100, 0) and (30, 0, 0), on its edges, and the box 100 by 0.000002 by 0.000002
about (50, 0, 0) at (30, 0, 0). A box with its body is crossed as before, bit for bit (2.38 million calls of a fuzz
under .NET Framework 4.8 and .NET 10, old against new); an empty box is still crossed nowhere.

**FIXED.** `TryUnion`, `TryIntersect` and `TrySubtract` of a `GeoObb3`, and of a `GeoSolid3` with a box, answer false
with no result for a box no thicker than the point tolerance on two axes, as for one flat on some axis, where they ran on
its sliver body: the box 100 by 0.000002 by 0.000002 joined to a cube 40 on a side it runs through was given the cube
alone, its two ends lost.

**FIXED.** `GetIntersections` of a ray and a box flat on some axis no longer throws "A box with a zero size on some axis
has no faces to build" for a ray starting within the tolerance of a corner and running away from it: it touches the box
at its origin, as one beside a box with its body does (456 throws in 200 000 rays of a fuzz, none now).

**FIXED.** `Locate` and `Contains` of a `GeoObb3` or a `GeoAabb3` put a point that is not a number outside the box, as
a body's `Locate` does; it was inside.

**CHANGED.** A hook of the presenter's LEADER and QLEADER has a vertex of its own wherever AutoCAD 2021 writes one: on a
leader to a frame, new or copied, and on any leader made in a UCS other than the world's, DIMASZ short of the end along
the UCS's X, where these leaders kept the two points picked. `{\Fgdt;j}%%v0.1` copied to a leader from (200, 40) to
(210, 45) at DIMASZ 0.18 gives (200, 40), (209.82, 45), (210, 45), as AutoCAD writes it. A frame on a leader to the left
in a turned UCS is set back its width along the world's X and the leader's end moved to its right side, as AutoCAD does.

**FIXED.** QLEADER writes how far it raised its words from the leader's end as group 213, as AutoCAD 2021 writes it in
any UCS: (0, -0.09, 0) for Middle of top line at DIMTXT 0.18, (0, 0.09, 0) for Middle of bottom line, and their width
along X as well where Always left justify sets them back on the left. It wrote none.

**FIXED.** The snaps Nearest and Perpendicular take the sides of a frame's rows alone, not its walls or symbols, as
AutoCAD 2021 gives them. TEXTEDIT reads a frame's row written by hand that leaves a cell out by what its cells hold:
`{\Fgdt;j}%%v0.1%%vA%%vB` opens as the position, 0.1, to A and B, where A was read as a second tolerance, and `0.1%%vA`
keeps its 0.1 as the first tolerance, where it was lost.

**FIXED.** ATTSYNC keeps the extended data and the extension dictionary of an attribute it makes anew because its tag
stands out of order, as it keeps those of one synced in place: AutoCAD 2021's `SetAttributeFromBlock` kept both, the
dictionary's handle too. An INSERT of GB with GRID before LEVEL, GRID carrying data of an application and an XRECORD,
synced to a block that defines LEVEL first, keeps both on the new GRID; they were lost. ATTSYNC by Select on an INSERT of
a dynamic block asks "ATTSYNC block GH_PANEL?", the dynamic block AutoCAD's `DynamicBlockTableRecord` names, where it
named the anonymous block the INSERT places, "*U1".

**CHANGED.** -ARRAY's corner prompt of a unit cell draws the cell alone, a rectangle from the first corner to the cursor,
as AutoCAD 2021's does; it drew every copy the cell would make.

**NEW.** With DYN on, an angle override typed at a point prompt shows in the angle field locked, as AutoCAD 2021's: `<30`
at LINE's next point shows 30 wherever the cursor is until the point is given.

**FIXED.** LIST writes an ellipse and a dimension as AutoCAD 2021 does, line for line, checked in accoreconsole under four
pairs of ANGBASE and ANGDIR. An elliptical arc from 30 to 120 degrees reads "Start Angle: 60" and "End Angle: 330" with
ANGBASE 90 and ANGDIR 1, after its length, centre, axes and ends; a whole ellipse its area and circumference. A dimension
is a DIMENSION, then whether it is associative, its type (horizontal, vertical, rotated, aligned, angular, arc, radial,
diameter, X or Y ordinate), its defining points, its obliquing and text angles counted from ANGBASE (30 reads 60), the
rotation of a rotated one as an amount turned by ANGDIR alone (30 reads 330 clockwise), its text position and text, its
style and whether it is annotative. Both wrote the rows of the Properties palette, and a dimension's kind ran into its
layer, "ROTATED DIMENSIONLayer".

**FIXED.** `CollidesWith`, `DistanceTo` and `GetIntersections` of a triangle, a polygon, a face, a plane, a solid or
another box against a box no thicker than the point tolerance on two axes, `GeoObb3` or `GeoAabb3`, read its body of
four slivers and two ends below the tolerance and answered wrongly: the box 100 by 0.000002 by 0.000002 was +Infinity
from a cube 40 on a side standing 20 over it, and missed a solid it ran through. Such a box is now read as a box flat on
those axes is, the segment along the third, and on all three its centre: it is 20 from the cube and touches the solid,
and `GetIntersections` of a triangle, a polygon, a face or a plane with it gives no points, as with the flat box, where
the body gave one to four. A side no longer than the point tolerance is thin, as `IsDegenerate()` reads it: a box 100
by 0.001 by 0.001 is a segment at the default tolerance and one 100 by 0.0015 by 0.0015 a body. A box thinner than the
tolerance on one axis alone, the plate 100 by 100 by 0.000002, keeps its body, and a box with at least two sides longer
than the tolerance answers bit for bit as before. Over 79 methods taking a box, 259 of 4 740 answers for such boxes
disagreed with the box flat on the same axes, and 3 do now, boxes 100 000 from the origin whose sides round a hair over
0.001.

**FIXED.** The presenter's linear dimensions lay out words the fit moves out under DIMTMOVE 1 and 2 as AutoCAD 2021 does
where DIMTOH keeps them level, as acad.dwt's Standard does: DIMEXE, half DIMGAP and their height out, square off the
middle of the line, the leader straight to there and a level landing on away from the line, at any angle of it. The
dimension of 0.5 DIMCONTINUE adds after one of 100 in Standard has its leader straight up from (100.25, 20), its landing
on to x 100.43 and its arrowheads outside, as AutoCAD draws it, where the presenter ran the leader to (100.372, 20.405)
and kept the arrowheads inside; "4" of a dimension of 4 under ISO-25's sizes stands 6.5625 off its line, not 5.9375.
Arrowheads need two lengths and two DIMGAPs between the extension lines (6.25 with ISO-25's sizes, where 5.625 was
taken), and words above the line their width and a gap each side: a dimension of 6 under DIMATFIT 2 has its arrowheads
outside, as in AutoCAD.

**FIXED.** DIMCONTINUE and DIMBASELINE draw in the style of the dimension they go on from, with its overrides, as AutoCAD
2021 does under DIMCONTINUEMODE 1: from a dimension made with DIMASZ 4 over ISO-25 they write `41` again, and from one
of another style they are of that style, where the presenter drew them in the current style. DIMBASELINE writes no
DIMTMOVE of its own, nor do rows of ordinate dimensions and QDIM, as AutoCAD 2021 writes them; a baseline dimension
based on a continued one takes its `279 1` with the rest.

**NEW.** `DxfTolerance.GeometricExtents` is the box of the frame's rows as AutoCAD 2021 gives it, where the drawing's
`FontSearchPath` measures its words: each row twice the text height high, each cell its ink and the text height wide,
`{\Fgdt;j}%%v0.1` at (0, 0, 0) in a style of Arial 0.18 high from (0, -0.18, 0) to (0.92296, 0.18, 0), as AutoCAD
gives it to the millionth; a frame of symbols alone needs no font. It had no extents before.

**FIXED.** A cell of a feature control frame mixing gdt symbols and words was drawn up to 0.0043 too wide at DIMTXT
0.18: the diameter sign has 1.5/21 of the text height either side, not 2/21, and the circular and total runouts 2.25/21
on their right, and words inked past their pen (Arial's A and f) set what follows on from their ink, as AutoCAD 2021
lays them out. `{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA%%vB%%vC` in Arial is 2.462701 wide, as in AutoCAD.

**NEW.** TOLERANCE, and the frames of LEADER and QLEADER, write each current dimension variable other than the style's
as an override, as dimensions and leaders write them: DIMTXT 5 typed over ISO-25 gives `1070 140, 1040 5` and rows 10
high, where the frame was drawn by its style alone. TEXTEDIT (DDEDIT) and a double click on a frame open the Geometric
Tolerance dialog filled from its words, and OK writes them, one undo step. Properties shows a frame's Dim style, Dim
line color, Text color and Text height and writes them as overrides. The snaps End and Mid take the rectangles of a
frame's rows alone, not its walls or symbols, and Insertion its point; EXPLODE says `Cannot explode TOLERANCE`, as
AutoCAD 2021 does.

**FIXED.** A frame on a leader to the left is set back the width of its widest row, not its first, and the leader writes
that width and the height of all the rows as groups 40 and 41: a frame of rows 0.553 and 1.271 wide stands 1.271 left of
the end and the leader holds 0.72 high, as AutoCAD 2021 writes it. QLEADER's angle constraints in a turned UCS read the
way the point was picked along the UCS and lay it off along the world, as AutoCAD 2021 does, and a leader to words, a
frame or a block made in a UCS writes the UCS's X as group 211, (0.866, 0.5, 0) in one turned 30 degrees, where it wrote
the world's.

**FIXED.** A new `DxfDatabase` holds the dimension style its header names: ISO-25, current and equal to the header in
every variable, beside Standard, as AutoCAD 2021's acadiso.dwt saved as each version from R2000 to R2018 has them, where
the header named ISO-25 and the drawing held Standard alone, off the header in 20 variables (arrowheads 0.18 long against
the header's 2.5). The 20 are written into ISO-25 and the header as AutoCAD writes them, so that AutoCAD reads the same
values. `new DxfDatabase("AC1015")` is the text of `new DxfWriter().ToString()` but for its code page and ISO-25.

**CHANGED.** The presenter's new dimensions are drawn by the header in every drawing, its DIMSTYLE naming a style of it or
not: where it names none, they are drawn in the style fallen back on (Standard, else the first) and write each variable
other than that style's, and leaders are compared with the same style, where dimensions were drawn by the style alone and
leaders compared with AutoCAD's own values. As AutoCAD 2021, DIMCONTINUE draws and writes DIMTMOVE 1 where it is nought
(`279` first, of linear and angular dimensions), DIMRADIUS and DIMDIAMETER write DIMUPT 1 (`288`) where the style has it
off, and DIMJOGGED writes DIMJOGANG (`50`) whatever the style has.

**CHANGED.** The presenter's new dimensions are drawn by the drawing's current dimension variables and write each that
differs from their style as an override, in the order of their groups, as AutoCAD 2021 does for every kind (linear,
continued, baseline, aligned, angular, radius, diameter, jogged, arc length, ordinate): a drawing saved with DIMASZ 5,
DIMEXO 1, DIMTAD 0 and DIMSCALE 2 over ISO-25 draws arrowheads 10 long and writes `40 41 42 77` in that order, where
the presenter drew by the style and wrote DIMTXT and DIMSCALE alone, DIMTXT first.

**FIXED.** A dimension made with DIMSCALE 0 through a viewport is drawn at one over the viewport's scale and writes it, as
AutoCAD 2021 does: 2 through a viewport of 1:2, words 2.5 high drawn 5 high and `1070 40, 1040 2` written, where the
presenter drew at 1 and wrote DIMSCALE 0. In model space DIMSCALE 0 draws at 1.

**NEW.** The arrowhead lists of the Dimension Style dialog (First, Second, Leader) and the Symbol list of the Modify
Multileader Style dialog show each arrowhead with its picture, as AutoCAD 2021's lists and Leader Settings show them, a
user arrow drawn from its block.

**FIXED.** More of the presenter's angles follow ANGBASE and ANGDIR as AutoCAD 2021's do, each checked in accoreconsole
with ANGBASE 90 and ANGDIR 1. DIMEDIT's obliquing angle is a direction: 30 slants the extension lines of a level
dimension 60 degrees from the X axis, where it slanted them 30. The Properties palette writes and takes the text rotation
of a dimension and the start and end angles of an ellipse counted so: an elliptical arc from 30 to 120 degrees reads 60
and 330, as AutoCAD's LIST writes it. MEASUREGEOM's Angle writes 30 degrees between two lines as 60, as AutoCAD writes it.
The obliquing angle of -STYLE stays as typed, as AutoCAD keeps it.

**NEW.** The angle override of AutoCAD 2021: `<30` typed at a prompt for a point that has a base point writes "Angle
Override: 30", asks the point again and holds it on the line at 30 degrees from the base point, counted as the angle of
`@100<30` is, `<<30` from east counter-clockwise. A distance typed then runs along it, and the window holds the cursor on
it over the grid, ortho and polar; a point typed and an object snap still win, as AutoCAD's help has it. From (0, 200),
`<30` and 100 end the line at (86.6, 250). New: `CadCommandSession.AngleOverride` and `ObjectSnap.AngleOverride`.

**FIXED.** ATTSYNC by Select refuses a block with no attribute definitions before asking whether to sync it, saying "This
block has no attribute definitions." and asking for another block, as Autodesk's help describes; it asked "ATTSYNC block
PLAIN? [Yes/No]" first and then ended. -ARRAY reads a distance between rows typed in the drawing's units, so `2'` in an
architectural drawing puts the rows 24 apart; it took plain numbers only, as the distance between columns did not.

**FIXED.** A disabled button of a presenter dialog is dimmed once: its words were muted and then drawn at 45 per cent as
well, about a quarter of their strength. They are now drawn at 45 per cent, as every flat button of the theme.

**CHANGED.** The paste preview draws the Clipboard's objects once and moves them with the cursor, a text drawn anew: a
Clipboard of 24 000 items, a tenth of them texts, previews in 16 to 22 ms a move where it took 34, and one of 2 400 in
under 1 ms where it took 2.7. What is drawn is the same.

**NEW.** `DxfTolerance`, AutoCAD's `FeatureControlFrame`: a TOLERANCE read from DXF and DWG as a class of its own rather
than a `DxfUnknownEntity`, made in code and written as AutoCAD 2021 writes one (`Text` with its rows as `^J`,
`Location`, `Direction` and `Normal` left out where they are the world's, `DimensionStyleName`), its style's variables
read through its overrides (`GetDimstyleData`), and moved, turned, scaled and mirrored as AutoCAD 2021's `TransformBy`
moves one: mirrored, it is turned over (normal (0, 0, -1)); scaled by 3, a frame of the style's 0.18 and 0.09 holds the
overrides `1070 140, 1040 0.54, 1070 147, 1040 0.27`, DIMTXT then DIMGAP. A drawing whose code looked for
`DxfUnknownEntity` with `DxfName == "TOLERANCE"` finds a `DxfTolerance` now.

**NEW.** TOLERANCE (TOL) in the presenter, with AutoCAD 2021's Geometric Tolerance dialog and its Symbol and Material
Condition dialogs: two rows of a symbol, two tolerances and three datums, the height of a projected zone and the datum
identifier, written as AutoCAD writes group 1, then "Enter tolerance location:" with the frame following the cursor. The
canvas draws a frame as AutoCAD 2021 lays one out, measured in its console: rows twice DIMTXT high whatever DIMGAP, each
cell its ink and DIMTXT wide, a cell of nothing taking no room; `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 in Arial is 0.92296
wide, as in AutoCAD, and a cell mixing a gdt symbol and words comes within 0.005. The symbols are the presenter's own
strokes. A frame has one grip, at its insertion point, and MOVE, COPY, ROTATE, SCALE and MIRROR (readable under MIRRTEXT
0) move it, where they refused it before.

**NEW.** LEADER's Tolerance option and QLEADER's Tolerance type show the Geometric Tolerance dialog and put the frame at
the leader's end; Copy and Reuse Current copy a TOLERANCE there, where they passed it over: on a leader running left the
frame is set back its width, `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 copied to a leader ending at (90, 105) standing at
(89.07704, 105), as AutoCAD 2021 puts it. Leader Settings no longer greys Tolerance.

**CHANGED.** User Arrow... of Leader Settings opens AutoCAD's Select Custom Arrow Block as a dialog of its own over Leader
Settings, rather than a panel under the Arrowhead list: an editable list of the drawing's blocks, a name not found saying
so in the dialog with its box framed orange, and in a drawing of no block the dialog open with its list and OK greyed.
`LeaderSettingsViewModel.UserArrowMessage`, `IsUserArrowNotFound`, `HasUserArrowBlocks` and `CanKeepUserArrow` drive it;
its messages are no longer in `Notice`, and `ToleranceToolTip` is gone.

**CHANGED.** Undo and redo in the presenter hold less memory and give the processor to an edit. The drawing an undo leaves
is the one a redo goes back to; where it was itself read from the bytes of its step and nothing has written into it since,
the worker saves it, sets it against those bytes and takes it as it is where they are the same, rather than reading the
step again, so undo after undo reads one drawing ahead, not two. A drawing read back no longer copies the bytes it is read
from, and its index of handles is sized at the start. On a drawing of 1 000 000 entities a run of undos and redos
allocates about 35 GB where it allocated 47, with 28 to 33 full collections where it had 41 to 46, and a redo 3 s after an
undo takes about 0.3 s under .NET Framework 4.8 where it took 1.6 to 1.9 s. The peak working set is set by the collector
and moved within its noise (4.0 to 4.8 GB either way under .NET Framework 4.8); the drawings held while none is read are
as before. Every drawing an undo or a redo gives is the one saved at that step, byte for byte. An edit stops the readings
still running and they read again after it: an ERASE of 10 000 objects of a drawing of 100 000 straight after an undo
takes about 185 ms under .NET 10 where it took 220 (110 once the readings are done).

**FIXED.** The first undo after the Block Editor saves (BSAVE, or BCLOSE saving) or discards its changes no longer reads its
drawing then: the drawing it goes to is read ahead, as after any step. On a drawing of 100 000 entities the undo of BEDIT
and finding an entity after it take about 1 ms where they took 304, and the undo after the changes are discarded about
1 ms where it took 309.

**CHANGED.** Reading an entity's groups tells its subclass markers (AcDbEntity, AcDbLine) from their bytes where they are
plain ASCII, rather than making a string of each. In a drawing of 100 000 entities written with markers, as AutoCAD writes
them, reading whether each is visible makes 43 MB where it made 51, and drawing again the third of them a layer switched
on 48 to 50 MB where it made 52 to 53, in the same time; the display lists are item for item as before.

**NEW.** A distance or an angle the presenter asks with no base point takes two points, as AutoCAD 2021's command line
does: a point typed or picked at "Enter new value for POLARDIST <0.0000>:" asks "Specify second point:", its rubber band
from the first, and 0,0 then 3,4 sets POLARDIST to 5; SNAPANG 0,0 then 1,1 sets 45. Every such prompt takes them, SETVAR
FILLETRAD, SNAP's spacing and a command's own alike. @dx,dy at the second point is measured from the first, the last
point is left as it was, FROM and the other point modifiers work at either point, the prompt's keywords are taken at the
second (T for Through at an offset distance), Enter there is the prompt's Enter, and text gets the prompt's refusal and is
asked again. Checked in AutoCAD 2021's console.

**CHANGED.** A point SETVAR asks (GRIDUNIT, SNAPUNIT, SNAPBASE, INSBASE, LIMMIN and the others) takes a number alone as a
distance from the last point towards the cursor, as AutoCAD 2021 does: with the last point at (10, 20) and the cursor at
(13, 24), GRIDUNIT 5 is 13,24, where the presenter refused it with "Invalid 2D point.". An angle asked with no base point
refuses text with AutoCAD's "Requires valid numeric angle or two points." rather than "... or second point.", and a point
picked at a distance asked with no base point is the first of two rather than refused.

**NEW.** SNAPGRIDLEGACY, as AutoCAD 2021's: 0 or 1, set by SETVAR or by SNAP Legacy, and kept in the settings file
(`GlobalSettings.SnapGridLegacy`) as AutoCAD keeps it in the registry. With 1 and SNAP on, the canvas holds the cursor on
the snap grid with no point asked too, the crosshair, the coordinates, the object under it and a click or a box corner
going from one grid point to the next (`CadCanvas.SnapGridLegacy`, `MainViewModel.SnapGridLegacy`); a click at (1 130, 70)
over a snap of 500 picks a line through (1 000, 0). SNAP Legacy was kept for the window alone before and changed nothing.

**NEW.** A layout's paper space has a grid of its own, as AutoCAD 2021's: GRID, F7 and GRIDMODE in Layout1 switch the
sheet's grid, kept in the layout's viewport 1, and leave model space's as it was, each shown again with its space. The
canvas draws it on the paper, 8 and 18 per cent darker than the paper, by the spacing, major lines and GRIDDISPLAY bits
of that viewport, within the layout's limits without GRIDDISPLAY 1, and dotted with GRIDSTYLE 4. The paper-space snap
stays model space's.

**NEW.** "Grid too dense to display" is written on the command line once as the grid comes on, or the view or the
spacing goes to where its lines would lie closer than 6 pixels without GRIDDISPLAY 2, and not again at each redraw.

**FIXED.** The Snap and Grid tab of Drafting Settings refused a Polar distance below nought that SETVAR POLARDIST takes,
so that the dialog opened after POLARDIST -1 could not be OK'd without changing it. It takes any number now, -1 kept and
PolarSnap stepping by the snap X spacing, and refuses text with "The polar distance must be a number.".

**FIXED.** The presenter's LEADER and QLEADER write every dimension variable other than the style's as the leader's
override, in the order of their groups, as AutoCAD 2021 writes them, where they wrote five (DIMSCALE, DIMASZ, DIMTAD,
DIMTXT, DIMGAP) and DIMLDRBLK: DIMEXO 0.5, DIMCLRD 1, DIMCLRT 2, the arrow `_Dot` and DIMLWD 0.30 give 42, 176, 178, 341
and 371 in that order, after QLEADER's DIMGAP and DIMTAD. The words typed take DIMCLRT, the current colour where it is
BYBLOCK.

**FIXED.** In a UCS other than the world's, LEADER and QLEADER measure the last segment along the UCS's axes, as AutoCAD
2021 does: the words run along its X, DIMGAP along it from the end, and a segment steeper than 15 degrees to that X is
hooked with no vertex of the hook's own, where the presenter measured it in the world and added a level hook. In a UCS
turned 30 degrees a leader from the UCS's (0, 0) to (10, 10) keeps its two points, (0, 0) and (3.66, 13.66) in the world,
its words 0.625 along the UCS's X from the end; QLEADER raises its words up the world's Y, as AutoCAD 2021 does.

**NEW.** LEADER's Copy and QLEADER's Copy an Object copy an INSERT picked to the leader's end, its scale kept and turned to
the UCS's X, with no hook, and QLEADER's Reuse Current copies an INSERT kept the same way: an INSERT at (500, 500) scaled
1.5 by 2.5 and turned 0.3 is copied to (10, 320) scaled 1.5 by 2.5 and turned 0, as AutoCAD 2021 copies it. A TOLERANCE
is still passed over, as the library cannot move one.

**NEW.** LEADER and QLEADER preview the leader at the cursor while its points are picked, its arrowhead and its line or
spline as the leader made there draws them; QLEADER's preview runs to the point its angle constraint holds, the cursor at
(100, 30) previewing a first segment held level to (100, 0) (`CadPreview`).

**NEW.** The Arrowhead list of QLEADER's Leader Settings shows each arrowhead with its picture
(`CadStylePreview.Arrowhead`) and ends with `User Arrow...`, which lists the drawing's blocks as AutoCAD 2021's Select
Custom Arrow Block does (`CadQLeaderRequest.Blocks`, `LeaderSettingsViewModel.UserArrowBlocks`): the one chosen becomes
the arrow, written on OK as DIMLDRBLK and as group 3 of the settings (`CadQLeaderSettings.UserArrow`).

**FIXED.** Associative dimensions in the presenter tie and follow as AutoCAD 2021 does where they did not. An end
snapped where several lines, arcs or polylines meet is tied to the one drawn last, wherever the cursor stood: the corner
(0, 0) of a line to (100, 0) and a line drawn after it up to (0, 100) is the start of the second. DIMANGULAR of two lines
writes the line selected second as its first (groups 13 and 14). A linear dimension whose origin moves keeps its line as
far from the origin it lay nearer to, but where that would put it nearer the other origin, as far from that one: origins
(0, 0) and (60, 40) with the line between them at y = 20, the first moved to (0, 5), put the line at y = 60, where it
went to 25; and of two origins as far from a line between them, the one the line lies across from counts as the
nearer. The words of a radius or a diameter whose circle is scaled keep their end on the leader's side, "R9.0000"
becoming "R13.5000" moving group 11 0.07 away from the leader, where it stayed. Three points of an angle along one ray
measure nothing, the arc's point on the ray as far out as the point given, where they measured the whole turn; and the
second point of an angle of a circle snapped on an arc of a polyline is tied to that segment at nought, as AutoCAD ties
it. 107 dimensions made and edited in AutoCAD 2021's console were made and edited alike here, 262 states of a dimension
in all: 238 equal AutoCAD's in every point, group 11 and 42 and tie to 1E-4, 118 before. Of the others, AutoCAD
writes flag 128 on every radius and diameter it makes; puts the arc of a part of an arc's length, or of an arc under 90
degrees, up to 0.43 from where ours goes, and that of an angle too small for its words two thirds of the way round, not
a third; measures the whole turn for three points along a ray at 120 degrees one rounding apart; and dimensions an arc of
a polyline with DIMARC, which the presenter does not.

**NEW.** DIMARC in the presenter ties an arc length dimension to its arc, as AutoCAD 2021 does: the ends of the length
as the arc's start and end (a Partial length's as nearest points of the arc at their angles) and the arc's center. It
follows the arc moved, turned, scaled or lengthened, its arc as far out and a third of the way along, its words in the
middle: the arc of 50 from 0 to 120 degrees dimensioned 80 out and lengthened by 10 at its end measures 114.72, its arc
point at (57.72, 55.39) from the center. Its first and second points are the arc's start and end also where it measures
the rest of the circle, and a part's points in the order given, as AutoCAD writes them (groups 13, 14, 40 and 41); they
were the ends of the length measured, in the order the arc runs.

**FIXED.** The words of an angular dimension too wide for its arc, put level past its first ray, run out the way the arc
runs on there, left or right, as AutoCAD 2021 puts them: those of an angle of 3 degrees from -5 degrees go to the left
of the arc run down, where they went right, and those of one from 100 degrees to the right.

**CHANGED.** A number typed in a table's cell in the presenter (`CadTableEditor.SetText`, TABLEDIT and the editor of a
cell) is kept as AutoCAD 2021's `SetValue` with `ConvertTextToValue` keeps it, where decimals and fractions stayed text:
1.5 is a number of the format of the drawing's units (`%lu6`), shown 1.5000 at LUNITS 2 and LUPREC 4; 1e3 one of `%lu1`
shown 1.0000E+03; 1/2 the number 0.5 of `%lu5` shown 1/2, and 1/3 shown 5/16; 1,5 the point (1, 5, 0) shown 1.0000,
5.0000, 0.0000. A number typed in a cell of no alignment of its own sets it top right, as AutoCAD does, the whole numbers
1 and 007 too, which stood where the row's style put them. A number typed over a number keeps the cell's kind and format,
12 over 1.25 shown 12.0000. 1,500, which AutoCAD reads as 1 500 of thousands, feet and inches, and dates stay text.

**CHANGED.** `DxfTable.Fill` with `GenerateSeries` writes each number in the format of the source cell its place repeats,
and shows it in it, as AutoCAD 2021 fills a series, where it wrote six places and kept the target's format: 1.5 of
`%lu6` goes on to 2.5000 and 3.5000 at LUPREC 4, and 1/2 over 1.5 to 2 1/2, 3.5000, 4 1/2 and 5.5000. A source of no
format still gives 3.500000.

**FIXED.** `DxfTable.SetDataType` turning a text into a number in decimal units rounded as the runtime rounds: on .NET Core
and .NET 10 2.5 at no places was shown 2 and 0.015 at two 0.01, where AutoCAD 2021 and .NET Framework show 3 and 0.02.
Both now round half away from nought as the number's 15 figures write it.

**FIXED.** `AddVisibilityParameter` wrote each visibility state listing no parameter, so AutoCAD 2021 read every other
property of the block as not visible in any state and showed none of its grips: a sign of the states Cross, Tick and
Blank with a Width kept Width out of Properties and its arrow off the INSERT. Each state now lists the block's other
parameters, their grips and its actions, those added before the visibility parameter and those added after it, as AutoCAD
lists them in each state of its Door sample, and AutoCAD 2021 reads Width as visible and shows its grip in all three
states. A state of a drawing that leaves a parameter out keeps it out; a parameter added afterwards joins every state.

**CHANGED.** `DxfBlockReference.GetDynamicGrips` gives an XY parameter's grips at its two other corners after the one at
its end, base x with end y and end x with base y, and the grip of a block properties table at its location, of the new
`DxfDynamicBlockGripType.BlockTable`, as AutoCAD 2021 shows them: a rectangle 100 by 50 of an XY parameter from (0, 0)
inserted at (1 000, 500) has its grips at (1 100, 550), (1 000, 550) and (1 100, 500), and a table at (0, -20) its grip
at (1 000, 480). Code that took the grips of such a block by their place in the list finds more of them.

**NEW.** The presenter shows those grips: an XY corner dragged sets the distance of the axis it takes from the end point,
the corner of base x and end y of the rectangle above dragged by (20, 10) a Height of 60, where AutoCAD 2021 also moves
the parameter's base point and reads a Width of 80, the block drawn alike; a block table grip clicked lists the rows,
"100.0000" and "200.0000" for a table of Width, and the one chosen sets Block Table1 and its columns in one undo step.
While one custom grip is dragged, the STRETCH prompt previews the block worked out at the cursor, the drawing left as it
is until the point is picked (`CadDynamicBlocks`, `CadPreview`).

**NEW.** GRIPDYNCOLOR sets the colour of a dynamic block's custom grips, an index of AutoCAD's colours from 1 to 255, 140
(#00BFFF) by default as AutoCAD 2021 starts; it is kept in the settings file (`GlobalSettings.GripDynamicColor`) as
AutoCAD keeps it in the registry, and given to the canvas (`CanvasOptions.CustomGripColor`). The custom grips were drawn
#19B5F0 before.

**NEW.** DIMANGULAR in the presenter measures the angle of a circle, as AutoCAD 2021 does: from the point the circle was
picked at, about its center, to a second point (`Specify second angle endpoint:`), both brought onto the circle, and
ties it as AutoCAD writes the tie: the first point and the center to the circle, the second as a nearest point of what
it was snapped to, a line at the parameter of the point on the circle run on, another arc or circle or a POINT at
nought, with no snap the circle at its angle. A circle of 25 about (200, 0) picked at (225, 0), with the end (300, 40)
of a line from (260, 40) as the second point, measures 21.8 degrees to (223.212, 9.285), tied at -0.9197 along the
line. When the objects move, such a point past the end of its line, or on a POINT, is dropped and stays, and one within
its line goes to its point there, as AutoCAD moves them: the circle moved 10 down, the angle measures 39.72 degrees.

**FIXED.** The PERpendicular snap of the presenter gives the foot on a line or a straight polyline segment run on past
its end, as AutoCAD 2021's does (from (20, 120) square to (100, -20)-(100, 60), the point (100, 120)), and an
associative dimension snapped so is tied at that parameter (1.75), a polyline's foot to the segment run on, and follows
as AutoCAD follows it: the line moved by (20, 7), the origin drawn from a point tied to nothing goes to (120, 0). An arc
still gives no foot off its run, as AutoCAD gives none.

**FIXED.** The arc of an angular dimension made in the presenter goes through a point a third of the way round from the
leg it starts at, as far out as the point given, as AutoCAD 2021 writes group 10 (16 for two lines): put at (50, 550) in
the right angle at (0, 500), it passes (61.237, 535.355), where the point given was kept. An angle of three points whose
objects move keeps its arc as far out at the same share of the angle, inside the legs where it measured less than half a
turn and outside where it measured more, as AutoCAD keeps it, where it stayed where it was; and the words of a radius or
a diameter whose circle is scaled stay where they stood, as AutoCAD keeps group 11, where they moved 0.7. Each
associative dimension that follows writes groups 11 and 42 anew. 23 associative dimensions of every kind, made and then
edited by moves, stretches, rotations and scalings in AutoCAD 2021's console and here, were compared in 20 states:
1 247 of 1 335 points, groups 11 and 42 and ties equal AutoCAD's to 1E-3. The others (an end where two lines meet, the
order of an angle's two lines, the words of a radius or a diameter grown by a digit, one linear dimension line) are set
right by the entry on ends where objects meet above; the words were measured 0.025 to 0.07 apart only because that
comparison drew them in txt, where AutoCAD's template has Standard in arial.ttf.

**NEW.** The External References palette's Bind… shows Bind Xrefs/DGN Underlays (`XrefBindWindow`, `XrefBindViewModel`)
for the drawings selected, the palette now selecting several with Ctrl or Shift (`SelectedRows`): Bind or Insert, started
from BINDTYPE, and OK writes the choice back to BINDTYPE and binds them all as -XREF Bind binds them, in one undo step.
It is offered where the selection holds a drawing loaded or needing reloading, and greyed for one of only drawings
unloaded or not found, or of images. A bind refused binds none, and the lines -XREF Bind writes, "Xref "column" is nested
and cannot be bound." among them, are shown in a box (`XrefBindRefusedWindow`) as well as on the command line. A host sets
`MainViewModel.ShowXrefBind` and `ShowXrefBindRefused`; with no dialog set, Bind… binds by BINDTYPE as it stands. Global
settings › Text and fonts says on top what its Hidden messages keep.

**FIXED.** `CollidesWith` and `DistanceTo` of a triangle, a polygon, a face, a plane, a solid or another box against a
box flat on some axis, `GeoObb3` or `GeoAabb3`, threw "A box with a zero size on some axis has no faces to build"; they
read the box as the shape it has become and answer as usual: flat on one axis its rectangle, on two its segment, on all
three its point. The box 100 by 100 by 0 about (50, 50, 0) is 20 from the triangle with corners (10, 10, 20),
(90, 10, 20) and (10, 90, 20) lying over it, and touched by a square standing upright across it. A box with its faces
gives the same answer bit for bit, an empty `GeoAabb3` is still refused, and `GetFaces()` and `ToSolid()` of a flat box
still throw.

**NEW.** The presenter's Columns › Resize and Rows › Resize, on the right-click menu of a table's cells, start the drag of
the selection's right (or bottom) line as though its edge grip had been clicked: the line follows the cursor, and a
click, Enter or a size typed makes it, one undo step (`CadCanvas.DragCellGrip`, `MainViewModel.CellGripDragRequested`).
A row's line dragged with dynamic input on shows the field of its height left of the table, as a column's shows its
width over it, and takes a height typed.

**CHANGED.** The drags of a table's grips follow the object snaps, ORTHO and polar tracking from the grip, as the other
grip drags do: with Endpoint on, a column's line dragged near the end of a line at 53 makes the column 53 wide, and with
ORTHO the corner's stretch grip dragged right and a little down stretches the width alone. A perpendicular from the grip
to the table's own edge it stands on, which would hold the line where it was, is not taken.

**CHANGED.** A whole number typed in a table's cell (`CadTableEditor.SetText`, TABLEDIT and the editor of a cell) is kept
as a number, as AutoCAD 2021 reads a text it parses: 1 is the number 1 and 007 the number 7, so that the autofill grip
dragged from a typed 1 gives 2, 3 and 4 where it copied the 1. b1, 1.5, 12mm and a number in a cell made to hold text
stay text.

**FIXED.** With dynamic input, the width field of a column dragged in a table whose rows flow up ran from the new line
the wrong way, over the next column; it runs over the column dragged, below the table, where its first row is.

**FIXED.** `GetIntersections` of a triangle, a polygon, a face or a plane with a box flat on some axis, `GeoObb3` or
`GeoAabb3`, or with an empty `GeoAabb3`, threw "A box with a zero size on some axis has no faces to build" or "An empty
bounding box has no shape to convert"; it gives no points, as `TryIntersectWith` answers false: the plane x = 20 and the
box 100 by 100 by 0 cross nowhere. `GetFaces()` and `ToSolid()` of such a box still throw, as before: it has no body.

**FIXED.** The `Try` methods of a segment answered true with points of NaN or infinity where the segment, or the shape
given with it, had a coordinate that is not a finite number: the segment from (NaN, 0) to (0, 100) and the one from the
origin to (0, 100) were given a point of NaN. They answer false now, for a segment, a ray and a line, a circle and an
arc, a chain, a curved chain and a curved loop, a face, a rectangle and a triangle, and for `TrySplitAtDistance`,
`TryExtendTo` a point and `TryTrimExtendToCorner`, whichever of the two shapes the method is asked of; a curved loop with a
vertex or a bulge of NaN, and an arc with an end of NaN, threw from inside and are false too. Every finite answer is the
same bit for bit.

**FIXED.** A fillet radius that is not a positive number, given to `TryFilletAt` or `Fillet` of a chain or a loop, flat or
in space, was refused with "A chamfer distance must be a positive number."; it is refused as a radius.

**NEW.** `DxfDatabase.BindXref(block, referenced, insertBind)` binds an external reference as AutoCAD's `BindXrefs`: its
block becomes an ordinary one about the base point of its drawing, holding a copy of that drawing's model space, the
records it draws by brought under the reference's name (`GRID$0$WALL`, `GRID$1$WALL` where `GRID$0$WALL` is taken), or
merged by name when inserted; the records the reference brought (`GRID|WALL`) are renamed in their places, with what the
drawing set on them. The presenter's -XREF Bind binds by BINDTYPE (0 Bind, 1 Insert, new and kept by the session) as
AutoCAD 2021 does, the references the drawing places with it, in AutoCAD's words for one unloaded, not found or nested
("Duplicate Block "TAG" ignored." when inserting); it refused Bind.

**NEW.** `DxfRasterVariables.GetRasterVariables(db)` makes the variables of images of a drawing with none, as AutoCAD 2021
makes them when IMAGEFRAME is first changed (frames shown, pictures finely, 72 the unit of the drawing: 1 for millimetres,
5 for inches). The presenter's IMAGEFRAME, typed or from the frame box, now sets a drawing with no image yet, which it
refused; the value it has already makes nothing, as in AutoCAD.

**NEW.** `DxfMLeaderStyle.Block`, `BlockColor`, `BlockScale` and `BlockConnectionType` are set as well as read. The
presenter's Modify Multileader Style dialog offers Block content (Source block, one of the drawing's blocks, Attachment,
Color, Scale), and MLEADER in a style of a block asks its attributes ("Enter attribute values", "Enter tag number
<TAGNUMBER>:") and places it past the dogleg as AutoCAD 2021 does: a circle of radius 5 landed at (20, 10) with a dogleg of
0.36 at (25.36, 10). MLEADER in a style of no content draws the leader alone, asking no text. In a drawing of R2007 or
later with no multileader style MLEADER makes Standard current first (AutoCAD's imperial values in an imperial drawing),
where it refused. Scale multileaders to layout and Specify scale are greyed while Annotative is ticked.

**FIXED.** IMAGEFRAME 2 is written as frames shown and not plotted (RASTERVARIABLES 70 of 3), as AutoCAD 2021 writes it; the
presenter wrote 2, the setting AutoCAD's API names `ImageFrameBelow`.

**NEW.** The presenter edits a table's cells on the canvas as AutoCAD 2021 does. Cells selected are framed by the column
letters and row numbers, those of the selection on amber (TABLEINDICATOR 0 or 1, kept on the machine; Table Indicator
Color on the right-click menu of a table and in Global settings › Appearance, `GlobalSettings.TableIndicator` and
`TableIndicatorColor`). A press in a cell dragged selects a range, held at the table's edge and grown to merged blocks.
The cells' edge grips move a line of the table and their autofill diamond fills the cells it is dragged over, a 1
dragged three cells down giving 2, 3 and 4, with a right-click choice of Fill Series, Copy Cells and the rest. A whole
table has AutoCAD's grips (`CadGripKind.TableColumn`, `TableWidth`, `TableHeight`, `TableSize`, `TableBreak`): move,
column lines with the width kept or with Ctrl the table growing, and stretch; the breaking grip is drawn but not offered.
Each drag shows its new line, outline or values, takes a width typed with dynamic input, and is one undo step: Autofill,
Column Width, Row Height or Table Size (`CadTableDrag`). No column goes narrower than AutoCAD's `MinimumColumnWidth`, its
two margins and its tallest text, 9 in Standard. The cells' right-click menu is AutoCAD's, its working items wired and the
rest grey (`TableCellViewModel.Menu`). New in `CadTableEditor`: `Fill`, `SetColumnWidths`, `SetRowHeights`,
`RemoveOverrides`, `CellNear` and `MinimumColumnWidth`; Columns and Rows › Size Equally and Remove All Property
Overrides work.

**FIXED.** The presenter's selection of several rows of a table's cells was drawn as a slanted shape:
`CadTableEditor.Corners` took the top right and bottom left corners from the wrong cells. B3:C4 of the Standard schedule
now runs from (63.5, -20) to (190.5, -38).

**FIXED.** A click in an empty cell of a table, with no object under the pick box, opened a selection box instead of
selecting the cell; it selects the cell.

**FIXED.** Merge › By Row and By Column of the Table Cell tab made one undo step per row or column merged; they make one.

**NEW.** The presenter's Quick Select (QSELECT) has AutoCAD's Select objects button at the right of Apply to: it hides the
dialog while `Select objects:` asks on the command line, and the dialog comes back over the objects picked, Apply to:
Current selection, so a line of DOORS and a text picked offer Multiple, Line and Text. It is greyed while Exclude or
Append is chosen. `QuickSelectViewModel.CanSelectObjects`, `IsSelectingObjects` and `SelectObjects()` are new; the
window answers QSELECT's request with "Select objects".

**NEW.** The presenter's Purge dialog previews an unused multileader style as MLEADER draws one and an unused table
style as the Table Style dialog draws it (`PurgeViewModel.TablePreview`), as it did blocks, text and dimension styles.

**FIXED.** The presenter's Block Attribute Manager (BATTMAN) wrote the changes made to a block without asking when
another block was chosen or Select block pressed; it now asks `Save changes to the attributes of block GB?` first, as
AutoCAD's does, No dropping them (`CadAttributeManagerRequest.HasChanges` and `Revert()`,
`BlockAttributeManagerViewModel.AskSaveChanges`). Move Up and Move Down are greyed on a constant attribute, as AutoCAD's.

**FIXED.** An MTEXT whose record gives neither a direction (group 11) nor an angle (50) reads along the world's x axis put
into its plane, as AutoCAD 2021 reads it, rather than along the plane's own x axis: on the normal (0, 0, -1) `Direction` is
(1, 0, 0) and `Rotation` pi, where it was (-1, 0, 0) and nought, and the display draws it so. The presenter's MIRROR under
MIRRTEXT 1 writes an MTEXT along x mirrored across a level line with no 11, as AutoCAD does; `Normal` set on an MTEXT of
neither still reads along the new plane's x axis, now written as its direction (11 of (-1, 0, 0) for (0, 0, -1)).

**FIXED.** A header with no `$AUPREC` reads `Auprec` 0, and one with no `$MIRRTEXT` reads `Mirrtext` false, as AutoCAD 2021
reads them; they read 2 and true. A drawing without them now shows angles to whole degrees (DIST, MEASUREGEOM, the
Properties palette, prompts) and keeps texts readable under MIRROR.

**FIXED.** The presenter's MIRROR writes a MINSERT as AutoCAD 2021 writes it, as an INSERT with its columns the other way:
3 columns 15 apart and 2 rows 8 apart mirrored across y = 100 are written x factor -1 turned 180, 44 of -15 and 45 of 8,
where they were y factor -1 and 45 of -8. An annotative INSERT is written so in its record and in each of its contexts,
which kept the factors the mirror gave them. A copy of a dynamic block's INSERT made by MIRROR or COPY keeps the values its
parameters are set to: a door flipped stays flipped, where the copy lost its representation and was no longer dynamic. A
multileader turned or mirrored keeps the sizes of its context (41, 140, 145) and its doglegs to the bit: words 4 high
mirrored across a line at 45 degrees stay 4, not 4.000000000000001. Checked against AutoCAD 2021's MIRROR in its console
on 64 cases.

**NEW.** The presenter's SNAP and GRID typed ask as AutoCAD 2021's do, checked in its console. SNAP offers
[ON/OFF/Aspect/Legacy/Style/Type], Aspect left out with an isometric snap, and A where the two spacings differ; Style
turns the snap and the grid over as AutoCAD does (a snap of 10 with a grid of 4 by 6 turned isometric at 5 keeps 8.6603 by
2.5 and a grid of 10.3923 by 3, back to Standard at 4 a snap of 4 by 4 and a grid of 6 by 6), Type sets SNAPTYPE and
POLARDIST. GRID offers [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect]: a spacing, 2x for twice the snap's, Major
(GRIDMAJOR), and aDaptive, Limits and Follow setting the bits of GRIDDISPLAY. Every answer but OFF turns SNAP or GRID on;
nought or text is refused in AutoCAD's words and asked again. SETVAR takes SNAPUNIT, GRIDUNIT, POLARDIST, SNAPTYPE,
GRIDSTYLE and GRIDDISPLAY as AutoCAD does: SNAPUNIT 0,5 gets "Cannot set SNAPUNIT to that value.", GRIDUNIT -2,3 is kept as
2,3, POLARDIST -1 is kept and "abc" gets "Requires numeric distance or two points.", and with an isometric snap SNAPUNIT
4,6 reads back 4,12. SNAPMODE takes 2, as AutoCAD does. The canvas draws the grid as dots where GRIDSTYLE says (1 model
space, 2 the Block Editor, where the grid is now drawn too), within the limits without GRIDDISPLAY 1, not at all where too
dense without 2, and with lines added between as it zooms in with 4. Object snap tracking runs its level and upright
lines along the axes turned by SNAPANG, or those of the isoplane, the polar lines from the base point turned by SNAPANG
too, and PolarSnap steps along a tracking line from the point acquired. `CadSystemVariable.Point` with a take that may
refuse, `CadSystemVariable.Distance` and `CadSystemVariable.Switch`, the canvas's `GridStyle` and `GridDisplay`, and the
window's `MainViewModel.GridStyle` and `MainViewModel.GridDisplay` are new.

**CHANGED.** LWDISPLAY is shown as OFF or ON and takes ON and OFF as well as 0 and 1, "Requires 0 or 1 only, or option
keyword." refusing the rest, as AutoCAD 2021's. A whole-number variable refuses text and numbers not whole in the words
it refuses a value out of range with ("Requires an integer between 0 and 7." for GRIDSTYLE abc); a point of two
coordinates refuses text with "Invalid 2D point."; a distance asked with no base point refuses text with "Requires
numeric distance or two points.", or "Requires numeric distance, two points, or option keyword." where it has options.
`GlobalSettings.PolarDistance` takes a value below nought, as AutoCAD's POLARDIST does, and throws only for one not
finite.

**NEW.** The DWG reader gives the overrides of a table of AutoCAD 2010 and later as AutoCAD 2021 writes them in DXF:
the table's own (93 to 96 and their groups) and each cell's flags and groups (170, 7, 63, 64, 283, 140 and its edges),
its row's looks pushed into it and the line under a title of 0.35 given on each cell of the header below. It reads the
TABLECONTENT and TABLEGEOMETRY a table keeps for its round trip, from AutoCAD 2007 (the TABLEGEOMETRY of every version),
and gives a table style with cell styles of its own (101 on) the CELLSTYLEMAP AutoCAD writes in DXF. A merged range gives
its edges on its last cell alone, without those inside it, and the cells it covers no content format and an empty value:
a title over three columns reads 172 as 0, 0 and 3, its top and right on the third cell. Five table drawings
AutoCAD 2021 made, cells of one override each, a cell of all six, cells of four edges, a table of its kinds of row
overridden and a clean table style of its own, read to AutoCAD's own DXF of them, group for group.

**NEW.** The DWG writer writes the overrides of a table's cells (their alignment, fill, colour, text style, height and
borders) as AutoCAD keeps them, the cell styles of its rows, columns and whole from the TABLECONTENT it keeps for a round
trip or else from its own overrides (93 to 96), and that TABLECONTENT as its own groups give it. AutoCAD 2021 reads each
of the five drawings written so to the DXF it was written from, but `$CMATERIAL`, and `AUDIT` finds 0 errors in each. A
table style's cell styles of its own (101 on) are written from the CELLSTYLEMAP it holds in DXF, each after its own id as
AutoCAD writes it, and the rows, columns and cells that take one keep it: a table taking GhNote (101) and GhWide (102)
by a row, a column and a cell is read by AutoCAD 2021 to the DXF it was written from, its two cell styles group for
group, and `AUDIT` finds 0 errors.

**FIXED.** The DWG writer and reader gave the lines of a table style's rows the wrong places: the inside horizontal line
went to the right and the right to the inside horizontal, and a line's colour was read only where flagged as overridden,
which a table style's lines never are. A data row's red bottom line now stays its bottom line.

**FIXED.** The DWG writer read a table's row heights from the wrong groups where the table gives overrides of its own,
which AutoCAD writes before the heights: a table of 4 rows of 10 was written with rows of 0.

**FIXED.** The DWG writer wrote an MTEXT of an angle and no direction along (cos, sin, 0) whatever its plane: it reads
along its plane's x axis turned by the angle, as AutoCAD takes it, so one of the normal (1, 0, 0) turned 30 degrees
reads along (0, 0.866, 0.5). The DWG reader no longer gives a BLOCK or an ENDBLK a material (347), which AutoCAD does
not write.

**NEW.** The Snap and Grid tab of the presenter's Drafting Settings as AutoCAD 2021's, the dialog now 560 by 580 on every
tab: Snap X and Y spacing with Equal X and Y spacing, Polar distance (POLARDIST), Grid snap or PolarSnap (SNAPTYPE),
Rectangular or Isometric snap (SNAPSTYL), the dotted grid of Grid style (GRIDSTYLE), Grid X and Y spacing (GRIDUNIT),
Major line every (GRIDMAJOR) and Grid behavior (GRIDDISPLAY 1, 2, 4 and 8), each greyed where AutoCAD greys it. With the
isometric snap the Y boxes show twice what the drawing keeps, so a snap of 8.6603 by 2.5 shows 5. OK writes the values
changed, the drawing's into its `*Active` viewport, SNAPTYPE, POLARDIST and GRIDSTYLE into the settings file; a box
refused is edged in red and nothing is written. PolarSnap works: with SNAP on, a point held on a polar angle is taken a
whole number of POLARDIST from the base point (250 takes a cursor 619.7 along to 500), the snap X spacing where POLARDIST
is nought. Checked in AutoCAD 2021's console: a new drawing has SNAPUNIT and GRIDUNIT 0.5 (acad.dwt) or 10 (acadiso.dwt),
GRIDDISPLAY 3, GRIDMAJOR 5, and SNAPTYPE, POLARDIST and GRIDSTYLE 0; GRIDUNIT takes -1,1 as 1,1. `ObjectSnap.PolarSnap`,
`ObjectSnap.PolarDistance`, `GlobalSettings.PolarSnap`, `GlobalSettings.PolarDistance` and `GlobalSettings.GridStyle` are
new.

**NEW.** The presenter applies SNAPBASE, SNAPANG and GRIDMAJOR of the drawing's `*Active` viewport, which it read and
left alone: the snap grid is laid from SNAPBASE and turned by SNAPANG (a snap of 10 turned by 30 degrees puts the cursor at
(9, 4) on (8.6603, 5)), ortho holds the cursor along the turned axes, the polar angles are counted from SNAPANG, the
crosshair turns with it, and the grid is drawn from the same base at the same angle with a major line every GRIDMAJOR.
SNAPSTYL 1 makes the snap isometric, as AutoCAD 2021's: its points where the 30, 90 and 150 degree lines cross, ortho,
the crosshair and the grid on the two axes of the isoplane SNAPISOPAIR names, ISOPLANE, and F5 or Ctrl+E turning to the
next plane, "<Isoplane Top>", while a command runs. SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL and SNAPISOPAIR are set by
SETVAR, refused in AutoCAD's words and written into the drawing. `ObjectSnap.SnapAngle` and `ObjectSnap.Isoplane`, the
`CadIsoplane` enum, `CadSystemVariable.Angle` and `CadSystemVariable.Point`, and the canvas's `GridMajor`, `SnapBase`,
`SnapAngle` and `Isoplane` are new.

**FIXED.** A system variable of 0 and 1 given 2 says "Requires 0 or 1 only.", as AutoCAD 2021 refuses ORTHOMODE, GRIDMODE,
PICKFIRST or FILEDIA 2, where the presenter said "Requires an integer between 0 and 1.".

**NEW.** The presenter's Leader Settings dialog of QLEADER (Settings, or Enter at its first prompt), with AutoCAD 2021's
Annotation, Leader Line & Arrow and Attachment tabs, and QLEADER drawing by every setting: the annotation type (MText,
Copy an Object, Block Reference, None; Tolerance greyed), Annotation Reuse, Prompt for width, Always left justify, Frame
text, straight or spline, the arrowhead, the number of points or No Limit, the angle constraints of the first two
segments, the attachment of text on either side and Underline bottom line. The settings are kept as AutoCAD 2021 keeps
them, in the drawing's `AcadDim` XRECORD (group 90 of 990106) read by `CadQLeaderSettings`, and the arrowhead as
DIMLDRBLK; OK writes both as one undo step. Checked in AutoCAD 2021's console: group 62 is the text on the right side and
63 on the left, 61 of 2 is Reuse Current, a first segment held level takes (100, 30) to (100, 0) and a second held to 45
degrees takes (150, 100) to (175, 75), Frame text writes DIMGAP below nought and Underline bottom line DIMTAD 1. Without
a window, QLEADER says Leader Settings is shown by the window and draws by the settings the drawing has.

**NEW.** The Polar Tracking tab of the presenter's Drafting Settings as AutoCAD 2021's: Additional angles with their list,
New and Delete (POLARADDANG, POLARMODE 4, ten at most, "Not an angle." for one that does not read), Object Snap Tracking
Settings (POLARMODE 2), Polar Angle measurement (POLARMODE 1), and the increment typed or chosen. While polar tracking
holds the cursor, a dotted path runs from the base point through it and the tip says `Polar: 183.8478 < 45°` in the
drawing's units, or `Relative Polar: 100.0000 < 90°` where the angles are counted from the last segment.

**CHANGED.** A new drawing of the presenter (NEW, Ctrl+N) is named Drawing1.dwg, then Drawing2.dwg, as AutoCAD 2021's
DWGNAME of a new drawing is, where it was Drawing1.dxf. Save As offers it as an AutoCAD 2018 Drawing (*.dwg) first, and
QSAVE with FILEDIA 0 asks "Save drawing as <Drawing1.dwg>:", a name typed with no extension saved as a DWG. A DXF is still
written where the name chosen ends in .dxf.

**FIXED.** The presenter's autosave of plan.dwg and plan.dxf open from one folder: both copies were named
plan.autosave1.dxf, so the second pushed the first down the line, to plan.autosave2.dxf, and the oldest out. The copy of a
DWG now keeps its extension in its name, plan.dwg.autosave1.dxf; that of a DXF is plan.autosave1.dxf as before.

**FIXED.** The presenter's MIRROR writes an INSERT as AutoCAD 2021's MIRROR writes it: its x factor turned the other way
rather than its y, turned to its mirrored y axis less a quarter turn (one at (20, 10) mirrored across y = 100 is written
at (20, 190), x factor -1, turned 180 degrees), its ATTRIBs without a normal of (0, 0, 1) written out. With MIRRTEXT 0 an
attribute or TEXT turned over across its baseline is turned about the middle of the ink of its words ("Q1" of arial.ttf
1.5 high about 0.705, not 0.75); with MIRRTEXT 1 the attributes of an INSERT already mirrored keep their normal of
(0, 0, -1). A multileader mirrored across a line at 45 degrees keeps its words held by the same side, as AutoCAD keeps
them, where it was taken for one mirrored across an upright.

**FIXED.** `DxfDBText`, `DxfBlockReference` and `DxfMText` turned in their own plane by `TransformBy` (the presenter's
ROTATE and MIRROR) are written as AutoCAD 2021 writes them: the angle from nought to a whole turn ("ABC" turned by -30
degrees written 330, not -29.999999999999996), the width factor and height as they were where nothing is scaled (no
41 0.9999999999999999), no angle of nought, and no normal of (0, 0, 1) written out. A text turned out of its plane keeps
the angle ezdxf gives it.

**FIXED.** The presenter draws a TEXT with the spaces before and after its words, as AutoCAD justifies it by them: "ABC "
on the right of its baseline stands a space further left than "ABC".

**NEW.** `DwgWriter` writes MULTILEADER and MLEADERSTYLE, ACAD_TABLE with its TABLESTYLE (and the TABLECONTENT and
TABLEGEOMETRY a table keeps for a round trip), the ATTDEF and ATTRIB of several lines with their MTEXT, and
RASTERVARIABLES and WIPEOUTVARIABLES, where it left them out and wrote every handle to them as 0. A table goes in the
form of AutoCAD 2010, its content made of the groups DXF keeps of AutoCAD 2007: a table of 3 by 3 with its title merged
across, written and opened in AutoCAD 2021, gives AutoCAD's own DXF of it back group for group, and its table style is
AutoCAD's own bits. A drawing with an image saves as a DWG with nothing left out, where the presenter said "Left out of
the DWG, not written yet: RASTERVARIABLES 1.". The overrides of a cell and of a whole table are not written yet.

**NEW.** `DwgReader` reads ACAD_TABLE and TABLESTYLE of AutoCAD 2010 to 2018, in the groups DXF keeps of AutoCAD 2007,
and RASTERVARIABLES and WIPEOUTVARIABLES; the tables of AutoCAD 2010, 2013 and 2018 read to AutoCAD's own DXF of them.

**NEW.** The presenter's LEADER (LEAD) and QLEADER (LE) on the command line, in AutoCAD 2021's prompts: Format (Spline,
STraight, Arrow, None), Undo, words typed line by line, and the annotation options Copy, Block, None and Mtext. The
leader is drawn by the current dimension style and DIMSCALE as AutoCAD 2021 draws it: a hook DIMASZ long where the last
segment is steeper than 15 degrees, the MTEXT DIMGAP past the end, and the variables other than the style's written as
the leader's overrides (DIMSCALE 2 writes `1070 40, 1040 2` and words twice as high). QLEADER takes its dialog's default
settings; the Leader Settings dialog and the Tolerance option are not there yet.

**NEW.** MLEADERSCALE and CMLEADERSTYLE through SETVAR, as AutoCAD 2021 keeps them: a style made current gives
MLEADERSCALE its scale, again when it is current already; MLEADERSCALE set scales the multileaders drawn next and leaves
the style alone (2 in a style of scale 5 draws words 4 high at 8); nought draws at 1, or through a viewport of 1:10 at
10. A value below nought, or a name of no style, says `Cannot set ... to that value.`.

**CHANGED.** MLEADER draws at MLEADERSCALE, no longer at the current style's scale: a drawing that keeps no MLEADERSCALE
reads 1, as AutoCAD 2021 reads it, whatever its current style's scale, until a style is made current again.

**NEW.** The presenter's DIMLINEAR has AutoCAD 2021's Mtext, Angle and Rotated options, and DIMALIGNED Mtext and Angle:
0,0 to 100,0 rotated 30 measures 86.6025, and words turned by Angle stand centred on the line, which is broken about their
turned box. DIMTFILL draws its mask behind a dimension's words (the MTEXT's background, 1.25 times their height) and
DIMTXTDIRECTION turns them half round, as AutoCAD 2021 writes them into the dimension's block.

**FIXED.** Dimensions made in the presenter write the middle of their words (group 11) and their measurement (42), where
DIMLINEAR wrote 0,0,0 and none. Text position X and Y of the Properties palette move a linear dimension's line through
the words under DIMTMOVE 0, as AutoCAD 2021 moves it. A jogged ordinate under DIMFXLON starts its leader DIMFXL before
the jog, not at DIMEXO.

**NEW.** The presenter's SCALELISTEDIT shows AutoCAD 2021's Edit Drawing Scales with Add Scale and Edit Scale: Move Up
and Move Down, Delete greyed for a scale the drawing keeps, units greyed for one an object points at, Reset after a
question; OK writes the drawing's list (ACAD_SCALELIST) as one undo step, a renamed or moved scale keeping its SCALE and
handle; with no window it asks -SCALELISTEDIT's prompts. `CadScaleListRequest`, `CadScaleListRow`.

**FIXED.** -SCALELISTEDIT Reset in a drawing whose MEASUREMENT is 0 puts back AutoCAD 2021's 16 imperial scales,
`1/128" = 1'-0"` to `1'-0" = 1'-0"`, not the metric 1:1 to 100:1, as its console resets them.

**NEW.** The presenter's Layer manager renames a layer in place: F2, a slow second click, a double click or Rename
Layer; refused in AutoCAD 2021's -RENAME words (`Cannot rename layer "0".`, `A layer named "DIM" already exists.`,
`Invalid layer name.`); `CadLayerEditor.Rename`.

**FIXED.** Defpoints can be renamed (`DxfLayerTableRecord.Name`), as AutoCAD 2021's -RENAME and entmod rename it;
layer 0 is still refused.

**NEW.** The Layer States Manager's New… asks a description beside the name, and asks before it replaces a state of
the name; Edit… shows AutoCAD's Edit Layer State: the state's layers with their properties changed in the state only,
Add layers and Remove layers, the properties it restores. `CadLayerStatesRequest.Replace`, `Has`, `BeginEdit`, `Edit`,
`CadLayerStateEdit`, `CadLayerStateLayer`.

**NEW.** INPUTSEARCHOPTIONS shows AutoCAD 2021's Input Search Options (mid-string search, the order of the
suggestions, Remember corrections after 2 to 10 mistypes, Separate commands and system variables, the delay, the
Content type list and its order), from the command line's Input Settings too; -INPUTSEARCHOPTIONS asks it as AutoCAD's
console does. INPUTSEARCHOPTIONFLAGS (1 AutoComplete to 16 Mid-string search) is a system variable, and so is
INPUTSEARCHDELAY, which Delay Time… now asks as `Enter new value for INPUTSEARCHDELAY <300>:`. `GlobalSettings`
gains `SortAlphabetically`, `SeparateSystemVariables`, `AutoCorrectMistypes`, `ContentTypes` and `InputSearchFlags`.

**FIXED.** The presenter's drawing commands checked entity by entity against AutoCAD 2021 drawing the same picks.
PLINE keeps the last arc's bulge on its end vertex; a first arc and ARC Enter go on from the last polyline drawn, and
Undo moves that point back; Width after Halfwidth offers the ending width. Angle defaults are written to AUPREC (40.587
is offered as 41), and TEXT under ANGDIR 1 stores 330, not -30. MTEXT with a rotation measures its box along it: corners
30 across and 20 down at 30 degrees make a box 15.98 wide; line spacing carries to the next MTEXT until Height or Style
is chosen. MIRROR keeps a dimension in the drawing's plane and readable under either MIRRTEXT.

**FIXED.** The presenter's MIRROR leaves texts and multileaders as AutoCAD 2021's MIRROR leaves them, checked group by
group in its console. A multileader keeps the drawing's plane (297 0) under either MIRRTEXT; at 0 its words keep reading
along x from the mirrored landing, held by their other side across an upright (171 and 176 turned, bit 14 of 90), the base
point at the left of the words less the gap; at 1 they are mirrored and write 294 1. A copy of a multileader of a block is
based at the block's left and writes its matrix where it stands. At MIRRTEXT 1 a TEXT, an attribute definition and the
attributes of an INSERT keep the normal (0, 0, 1) and are written backwards (71 2), not turned over. At 0 a TEXT is
mirrored by the ink of its words where the fonts read them: "ABC" of txt.shx 2.5 high mirrored across x = 100 starts at
193.333, not 195.5, and its turn is written from 0 to 360 degrees.

**NEW.** The presenter's Properties rows of a dimension add Ext line fixed and its length, Fill color, Fractional type,
Text position X and Y and Text view direction, written as overrides; extension lines follow DIMFXLON as AutoCAD 2021
draws them. MATCHPROP copies the plot style in a drawing of named plot styles (`CadMatchProperties.PlotStyle`) and a
hatch's gradient.

**FIXED.** A hatch's origin is kept through Angle, Scale, Pattern name and MATCHPROP. DIMBREAK's gap where the style
keeps no size is AutoCAD's 0.125, or 3.75 in a metric drawing, not 1.5 arrowheads.

**FIXED.** Point modifiers of the presenter checked against AutoCAD 2021: TK's `Next point  (Press ENTER to end
tracking):` and its upright tie, (100, 0) towards (130, 30) reaching (100, 30); `40,3` at `(need YZ):` gives Y 3, not
40; Enter and text at FROM, M2P, TT, TK and filter prompts answered as AutoCAD answers them; TT and TK no longer nest in
another modifier, nor FROM in TK.

**NEW.** POLARMODE 1 (polar angles from the last segment) and 4 with POLARADDANG (up to ten additional angles, kept in
the settings file); `CadUnits.TryAngles`, `CadSystemVariable.Text(name, get, take)`,
`ObjectSnap.PolarFromLastSegment`/`LastSegment`/`AdditionalPolarAngles`, `CadCommandSession.LastSegment`,
`GlobalSettings.PolarAddedAngles`.

**NEW.** The presenter opens and saves DWG as AutoCAD 2021 does. Ctrl+O shows Select File with Drawing (*.dwg) first and
DXF (*.dxf) after it, a DWG dropped on the window opens, and the file list lists the DWGs of the folder; a file is read as
a DWG of AutoCAD 2000 to 2018 by `DwgReader` where its first bytes say so, and one of a version not read, R14 among them,
is said on the command line, not opened as a recovered DXF. Save As shows Save Drawing As with AutoCAD 2018 Drawing
(*.dwg) first and the DXF of the drawing's own version after it, the type of its file chosen first, and QSAVE of a
drawing opened from a DWG writes it back as a DWG of AutoCAD 2018 (`DwgWriter`); what that writer does not write yet is
said after the save, "Left out of the DWG, not written yet: RASTERVARIABLES 1." A DWG is attached as an external
reference (XATTACH, -XREF, the palette) and inserted (INSERT, -INSERT, the Blocks palette) as a DXF is, and WBLOCK writes
a DWG where the file ends in .dwg. **NEW.** FILEDIA 0 asks on the command line for the file QSAVE of a drawing with no
file ("Save drawing as <Drawing1.dxf>:"), XATTACH ("Enter name of file to attach:", then the Attach External Reference
dialog with it), IMAGEATTACH, and the Browse… of the Blocks palette and the Write Block dialog would ask a file dialog
for; a name typed at "Save drawing as" with no extension takes the offered one's. **NEW.** `CadDrawingFile` (`OpenFilter`,
`IsDrawingName`, `IsDwg`, `Open`), `CadDocument.LeftOutOfDwg`, `MainViewModel.CreateSaveAsJob`, `CadExportJob.Dwg`,
`CadDialogAction.Browse`, `CadInsertRequest.FileDialogs`, `CadWriteBlockRequest.FileDialogs` and `BrowsesOnCommandLine` of
the Blocks palette and Write Block view models. **CHANGED.** `CadDocument.Open` reads a DWG, and `CadDocument.SaveAs`
writes a DWG for a name ending in .dwg, where both read and wrote DXF only.

**FIXED.** The External References palette of the presenter said "Needs reloading" where AutoCAD 2021's help of the
palette's List View says "Needs Reloading". Its other words were checked against that page and stand: Loaded, Unloaded,
Not Found, Unresolved, Orphaned and Unreferenced for the status, the current drawing listed first, and the types of a
drawing attachments or overlays. The page says nothing of a type of a nested reference, so Nested is kept.

**FIXED.** The presenter drew a layer of an external reference the drawing had not changed as the file has it now under
VISRETAIN 1, and synced it on reload. AutoCAD 2021 does not: with VISRETAINMODE 0, its default, it syncs nothing, and its
console kept `x|WALL` green (62 3, 1071 0) when x.dwg made WALL blue, reloaded and opened anew alike. The drawing's
record is now drawn whole. **NEW.** VISRETAIN 0 as AutoCAD 2021 keeps it: what the drawing changed is drawn as it keeps it
until Reload, which writes the file's properties back with 1071 0 (its reload wrote `x|WALL` red back as the file's
blue, 62 5), the rest as the file has it; Unload takes the records out, as its console showed. The presenter does not
write them again when it opens a drawing of VISRETAIN 0.

**NEW.** -LAYER in the presenter takes TRansparency, Plot and Description, asked as AutoCAD 2021 asks them: "Enter
transparency value (0-90):" (95 refused with "Requires an integer between 0 and 90." and asked again), then "Enter name
list of layer(s) for transparency 50% <0>:", Enter taking the current layer; "Enter a plotting preference [Plot/No plot]
<Plot>:"; "Enter layer description:". On a layer of an external reference each is marked in the 1071 of
`AcXRefLayerPropertyOverride` as AutoCAD 2021 marked `b|WALL`: 128, 8 and 1024. 50 per cent is the alpha 127. The Layer
manager has no column for them, and none was added. **NEW.** `CadLayerEditor.SetTransparency`, `SetPlottable` and
`SetDescription`.

**CHANGED.** The presenter's XATTACH and -XREF Attach and Overlay write into the drawing the records AutoCAD 2021 writes
for a drawing it attaches, as its -XREF Attach of x.dwg, its layer WALL green on DASHED, wrote them: the linetype
`x|DASHED` with its dashes and the layer `x|WALL` on it, green, with the file's lineweight, plotting, transparency and
description and `1001 AcXRefLayerPropertyOverride, 1071 0`, each flagged 48 (dependent, resolved), and the block of the
reference 36. Nothing of the file was written before, so a drawing saved by the presenter listed no layer of a reference
in the Layer manager until AutoCAD had saved it. Unload flags them 16 and the block 4, Reload 48 and 36 again with the
records of layers the file has gained, a record the drawing has keeping what it has; each is an undo step where anything
changes, and Detach takes them out. **NEW.** `DxfLayerTableRecord.SetDependent` and `DxfLinetypeTableRecord.SetDependent`
mark a record of an external reference (flags 16 and 32), so that its table takes a name with a | in it.

**NEW.** `DwgWriter.Write` writes a DWG file of AutoCAD 2018 (AC1032), the library's own code as the reader is: a
`DxfDatabase` with every record it holds, or the shapes of a `DxfDocument`, to a path or a stream. The drawing is written
as its DXF and each record put into the object of the DWG that says the same, with the drawing's handles: the header
variables, the classes, the tables, the blocks, LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE,
POLYLINE with its vertices, the meshes, TEXT, MTEXT, ATTDEF, INSERT with its ATTRIBs of one line, SOLID, TRACE, 3DFACE,
DIMENSION, VIEWPORT, HATCH, LEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, and DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE,
IMAGEDEF, DBCOLOR and the small objects of a new drawing, in the compressed container of pages AutoCAD 2004 and later write, every check sum in
place, the same bytes for the same drawing. A type not written yet (MATERIAL, VISUALSTYLE, MULTILEADER, ACAD_TABLE, REGION,
3DSOLID among them) is left out, every handle to it written as 0, and counted in one Warn line. Nineteen test drawings,
each as AutoCAD 2021 wrote its DXF, written as DWG: AutoCAD 2021 opens each and AUDIT finds 0 errors, its own DXF of
each is the source but the types left out, and `DwgReader` reads each back to the source records. A drawing of a few
hundred records takes 0.1 s and some 10 KB, where AutoCAD's own file is 13 to 17 KB, and 100 000 lines 1.1 s. See
[Reading and writing DWG files](src/GeometryHelper/docs/dxf/dwg.md).

**CHANGED.** REVCLOUD's Arc length in the presenter asks AutoCAD 2021's one approximate length, `Specify approximate
length of arc <15.0000>:`, in the place of a minimum and a maximum, and SETVAR sets REVCLOUDAPPROXARCLEN,
REVCLOUDARCVARIANCE and REVCLOUDCREATEMODE, kept by the session. At REVCLOUDARCVARIANCE 1, as AutoCAD starts, each arc is
drawn between two thirds of the length and four thirds, `Minimum arc length: 4.0000   Maximum arc length: 8.0000` for 6;
at 0 each is the length, which draws the clouds checked against AutoCAD's console before. The length is no longer scaled
by DIMSCALE, as AutoCAD 2021 does not scale it: 2021 has no REVCLOUDSCALEMODE, which later releases added to scale it
again. AutoCAD 2021 sizes the first cloud of a drawing by a share of the view's diagonal its help does not give; the
presenter still starts at 15 in millimetres and 0.5 in inches. AutoCAD's console runs only the REVCLOUD of earlier
releases, so this follows AutoCAD 2021's help and variables, read in its console.

**NEW.** PEDIT in the presenter edits a 3D polyline with AutoCAD 2021's options for one, checked in its console: Close and
Open, Join, Edit vertex (Next, Previous, Break, Insert, Move, Regen, Straighten), Spline curve, Decurve, Reverse and Undo,
in AutoCAD's words. Spline curve of the 3DPOLY (0, 0, 0), (100, 0, 10), (100, 100, 20), (0, 100, 30) stores the 13
vertices AutoCAD stores, the curve through (75, 50, 15), and closed, reversed and decurved it is AutoCAD's to 1E-8; a
vertex moved on the spline fits it again, the curve through (47.168, 18.652, 0.879) as in AutoCAD. Join adds nothing, as
AutoCAD 2021's console added nothing to a 3D polyline. PEDIT said before that 3D polylines were not edited yet. Multiple
still leaves them out.

**NEW.** PEDIT Multiple in the presenter joins by Extend, and by Both, at the end of an arc as AutoCAD 2021 does, checked
in its console: the arc is run on or cut back round its circle to where the other end segment, a line or an arc, meets
it, and the other is run on or cut back to there too. Of two crossings the one nearer the two ends together is taken, as
AutoCAD took it where the one nearer the arc's end lay elsewhere. The arc about (0, 0) of radius 100 from 0 to 80 degrees
and the line from (10, 130) to (10, 200), joined at a fuzz of 40, are one polyline through (10, 99.4987), the arc's bulge
0.38517039, to 1E-10 of AutoCAD's. Extend refused any arc before; Both added a segment there.

**FIXED.** PEDIT Multiple's Join by Extend in the presenter takes two ends whose meeting point lies within the fuzz
distance of both, as AutoCAD 2021 does, not two ends within the fuzz of each other: the ends (100, 0) and (130, 30), 42.4
apart, run on to (130, 0) at a fuzz of 35, and the ends (100, 0) and (110, 5), whose lines meet far off, are not joined
by Extend; Both adds a segment between them.

**NEW.** SETVAR in the presenter sets PEDITACCEPT and PLINETYPE, kept by the session, and PEDIT, PLINE, POLYGON and
DONUT honour them as AutoCAD 2021 does, checked in its console. PEDITACCEPT 1 turns a line or an arc picked into a
polyline without `Do you want to turn it into one? <Y>`, and Multiple without asking to convert. PLINETYPE 0 makes PLINE,
POLYGON, DONUT and PEDIT's lines and arcs old-style POLYLINEs, PLINE from (0, 100) to (100, 100) and, 2 to 4 wide, to
(100, 200) a POLYLINE whose second vertex has widths 2 and 4, and keeps a POLYLINE edited alone one; at 1, as at 2, PEDIT
makes it a LWPOLYLINE of its handle again. RECTANG and REVCLOUD make LWPOLYLINEs at 0 too, as AutoCAD's do. AutoCAD names
the first PEDITACCEPT; it has no PEACCEPT.

**FIXED.** A [`DxfViewport`](xref:GeometryHelper.Dxf.DxfViewport) made in code and appended to a layout has the layers
frozen in new viewports (`ViewportVisibilityDefault`, New VP Freeze) frozen in it at once, as AutoCAD 2021's
`AppendEntity` freezes them: its API, given layer GHB so marked, made a viewport with [GHB] frozen at birth, and left the
layout's own viewport 1 with none. Only the presenter's MVIEW froze them before. A copy keeps its original's.

**NEW.** "Isolate Selected Layers" in the shortcut menu of the presenter's Layer Properties Manager turns off every layer
but those chosen, one undo step: of the layers 0, WALLS and COLUMNS, COLUMNS chosen leaves 0 and WALLS off. The item was
grey. "Rename Layer" stays grey, since renaming in the list needs a name cell that can be typed in, which waits for a
design.

**NEW.** -SCALELISTEDIT in the presenter edits the drawing's list of scales (ACAD_SCALELIST), its prompts and answers
word for word as AutoCAD 2021's console gives them: ? lists the scales with their units, Add asks a name and a ratio and
redefines a scale of that name when told Yes, Delete takes out one scale or `*` every one in no use and says "Scale 1:1 is
referenced so it cannot be deleted." for one AutoCAD keeps, Reset puts back the standard 1:1 to 100:1, Exit ends; the
command is one undo step. The status bar's scale list in a layout offers the drawing's list, as AutoCAD's does, and its
Custom... runs -SCALELISTEDIT in place of VPSCALE. The Edit Drawing Scales dialog of SCALELISTEDIT waits for a design.
[`DxfObjectContextCollection.CanRemoveContext`](xref:GeometryHelper.Dxf.DxfObjectContextCollection.CanRemoveContext(System.String))
says whether `RemoveContext` would take a scale out.

**CHANGED.** ZOOM Previous in the presenter goes back through a list of its own for each viewport of a layout, for the
paper of each layout and for model space, 20 views each, as AutoCAD 2021 keeps them. It went back through one list of
every view the drawing's ZOOM and PAN had left, whichever viewport or space they changed, and a viewport's views were kept
as boxes. AutoCAD's console, after 12 zooms in one viewport, answers "No previous view saved." in another, and after a
zoom there gives the first its 12 back in order; zoomed 30 times, a viewport gives 20 back, then "No previous view
saved."; the paper's list is apart.

**CHANGED.** The wheel's rolls and the middle button's drags in a viewport worked through are one undo step together in
the presenter, as AutoCAD 2021 combines consecutive real-time zooms and pans by default (UNDO Control Combine: UNDOCTL is
61 in its console, its 16 set). A roll or a drag held still long enough for the view to be drawn again made a step of
its own, so one drag could leave several. ZOOM and PAN typed stay a step each, as AutoCAD's console undoes ZOOM 2X, ZOOM
2X and -PAN one U each. `CadDocument.SetViewportView` takes `realTime` to join a change to the real-time one before it.

**FIXED.** A layout viewport whose view is twisted, or looks at a target off the origin, is drawn in the presenter where
AutoCAD 2021 draws it, and a twisted one can be worked through. The presenter turned the view the wrong way and took the
view's centre as a point of model space, where AutoCAD keeps it about the target and turned by the twist: AutoCAD's
console, asked TRANS from the world to the view and on to the paper in a viewport 120 by 80 about (100, 100) at 1:5, its
view about (100, 50) twisted 30 degrees, puts (100, 0) at (97.320, 100) on the paper, and in one about (250, 100) looking
at (10, 20) puts the origin at (248, 96); the presenter now does too. MSPACE and a double click enter a twisted viewport,
which they refused: the canvas's view in model units turns with it (`CadView.Twist`), so picks, snaps, boxes, the
crosshair and the UCS icon follow the turn, PAN moves by the model distance given and ZOOM Window fits the box turned.
`CadViewports.ToView` and `FromView` give the view's coordinates of a point and back.

**NEW.** The presenter's window says on a drawing's command line when a file one of its external references was read
from changes on disk, where AutoCAD 2021 shows a balloon over its status bar (XREFNOTIFY 2): "External Reference Files
Have Changed: grid needs reloading. Reload it with -XREF Reload or in the External References palette." Each change is
said once, and again only after the reference was reloaded and its file changed anew. The files were watched only while
the External References palette was open, to refresh it. **NEW.** `MainViewModel.WatchReferenceFiles`, which the
workspace sets.

**NEW.** The External References palette of the presenter lists nested external references as AutoCAD 2021's does: a
reference the drawing places is followed by those its drawing attaches, of the type Nested in the list view, under their
parent and of their own type in the tree view, each level 16 pixels further in. A nested reference whose parent is
unloaded, not found or unresolved stands Orphaned; nested overlays are left out, as AutoCAD loads none. The record
AutoCAD writes in the host for a nested reference (`c` beside `b`, which no INSERT of the host places, as its console
showed for a host attaching `b.dwg` that attaches `c.dwg`) was listed as Unreferenced; it is listed under its parent alone.
**NEW.** `CadExternalReference.Depth`, `ParentName`, `IsNested` and `CadReferenceStatus.Orphaned`; `--dialog xref` and
`xref-tree` draw the palette in screenshot mode. **FIXED.** The palette's names were drawn in the system's black on its
dark rows; they are drawn in the text colour.

**NEW.** XOPEN in the presenter opens the drawing of an external reference picked in a tab of its own, as AutoCAD 2021
opens it in a window of its own. It asks "Select Xref:"; a line picked is answered "Object is not an Xref" and the
question asked again, and Enter ends it saying nothing, as AutoCAD 2021's console answers them. A pick on what a nested
reference draws opens the reference the drawing places, and one whose file is gone is refused as -XREF Reload refuses
it. The registry has 177 commands.

**FIXED.** A layer of an external reference the drawing keeps a record of (`grid|GRID`, as AutoCAD writes it) is drawn
as AutoCAD 2021 draws it under VISRETAIN 1: what the drawing changed of it as the drawing keeps it, the rest as the file
referenced has it now. The presenter drew the record whole, so a layer the file made blue after AutoCAD saved the drawing
stayed green. AutoCAD, probed in its console with -LAYER on `b|WALL` and the drawing written out, marks each change in
the 1071 of the record's `AcXRefLayerPropertyOverride`: 1 Off, 2 Freeze, 4 Lock, 8 Plot, 16 and 256 Color, 32 Ltype, 64
LWeight, 128 TRansparency, 1024 Description, 0 for a layer left as it was. The Layer manager, the layer box and -LAYER
now change such a layer and mark it so, and a change of it in the drawing draws its INSERT anew, which it did not.
**CHANGED.** The current layer refuses a layer of an external reference with "Cannot set CLAYER to that value.", and
-LAYER Set with "Invalid layer name.", as AutoCAD 2021 refuses them; a new layer named with a character AutoCAD refuses,
`grid|X` among them, is refused with `"grid|X" is an invalid layer name.`

**NEW.** Associative dimensions in the presenter tie the PERpendicular and TANgent snaps at a first origin, on polylines,
and in angles of three points and ordinates, as AutoCAD 2021 ties them. At a first extension line origin, an angle's
vertex and an ordinate's feature, the two snaps are drawn from the last point given (LASTPOINT), which the new
`CadPrompt.SnapFrom` hands the window's snap through `ObjectSnap.Snap`; the ends of an angle of three points are drawn
from its vertex and tied with the vertex's own tie as their last point. A polyline's segment is tied by its marker (91, or
92 for the second object of an intersection), a point on its arc by its angle counted on from the arc's start: AutoCAD
2021, probed in its console, wrote the end of a half circle from (100, 200) to (100, 300) as marker 2 and 7.85398 and its
center as 4.71239, and a line drawn across the third segment as 332 the polyline and 92 = 3. A moved object takes its
points where AutoCAD takes them: a tangent drawn again from its point; a perpendicular drawn again where its point is tied
to an object, else kept at its parameter, or, past the end of its line, at the foot from the origin of the world, so a
first origin drawn from (37, 123) square to (100, -20)-(100, 60) goes to (120, 0) when the line moves by (20, 7). The
dimension line of a linear dimension stays as far beyond the nearer origin as it was, 50 for an end of the line dragged
from (100, 0) to (120, 30) with the line 20 above. A drawing of 16 dimensions so tied, made here and opened in AutoCAD
2021, has no errors in AUDIT, and after the same moves and stretches in both every tied point is where AutoCAD puts it on
its own drawing, to 1E-3; AutoCAD's file read here follows the same way.

**FIXED.** STRETCH in the presenter moves the origins and the line point of a linear dimension that lie inside its
window, and keeps the height of a polyline's arc off its chord when one end moves, as AutoCAD 2021 does. It left the
dimension to its tie, and kept the arc's bulge: the half circle from (100, 200) to (100, 300) whose end goes to
(100, 320) now takes a bulge of 100 / 120, its center at (89, 260) where AutoCAD puts it, not (100, 260).

**CHANGED.** Esc in the presenter's MTEXT editor with changes asks as AutoCAD 2021 asks: Multiline Text - Unsaved
Changes, "Do you want to save your text changes?", with Yes, No and Cancel and Always perform my current choice, in the
place of the presenter's own "Save changes to MText?". Yes writes the changes and closes the editor, No drops them, Cancel
goes back to editing, and with nothing changed Esc closes with no question. A Yes or No given with the box ticked is kept
in Global settings as `MTextUnsavedChanges` ("Ask", "Yes" or "No"), as AutoCAD keeps it among its hidden messages, and
Text and fonts › Hidden messages sets it back to Ask; Cancel is never kept. `MainViewModel.AskKeepMTextChanges` now takes
the question, an `MTextUnsavedChangesViewModel`, and `CadQuestionWindow.AskKeepMTextChanges` is gone. The screenshot mode
draws the dialog with `--dialog mtext-unsaved`. Esc in the edit box of a single-line text already kept the old words, as
AutoCAD does, and a test now holds it.

**FIXED.** DIMBREAK in the presenter keeps its breaks as AutoCAD 2021 keeps them, so a dimension drawn anew is broken
again. It cut the lines of the dimension's picture only, and DIMEDIT, a change of style or a grip drew the dimension
whole again. AutoCAD, probed in its console, keeps a BREAKDATA under ACAD_BREAKDATA in the dimension's extension
dictionary: one AcDbBreakPointRef for each object Auto finds crossing (71 0, the object in 331, the object naming the
BREAKDATA among its reactors), one for each break by hand (71 2, the two points picked, in 91 the line: 0 and 1 the
extension lines, 3 the dimension line), and Remove takes it out with the extension dictionary. The presenter writes the
same, and breaks a dimension it draws anew from what it keeps, whether it broke the dimension or AutoCAD did; AutoCAD 2021
opening the drawing reads it, finds no errors in AUDIT, and breaks the dimensions again when it draws them anew.

**CHANGED.** A click in the presenter picks the object drawn on top among all those within the pickbox, as AutoCAD 2021
does, not only among those within a tenth of the pickbox of the nearest. AutoCAD's `(ssget pt)` on two level lines, one
through the point and one drawn later 0.05 to 0.9 of the pickbox above it, picks the later one every time, and 1.1 of
the pickbox above, outside the box, the other; sent to the back by DRAWORDER, the later one loses. Under SORTENTS without
its bit 1 AutoCAD picks the one drawn first, however much nearer the other lies, and so does the presenter, which went by
distance and handle. `PickIndex.Pick` gives its handles in that order.

**FIXED.** DIMEDIT Oblique in the presenter keeps the angle as AutoCAD 2021 keeps it, from the dimension line. It kept the
angle typed, from the x axis, in group 52, where AutoCAD's Oblique is that angle less the line's: AutoCAD 2021, slanting
three dimensions to 60, gives 60 to a level DIMLINEAR, 30 to one rotated 30 and 33.435 to a DIMALIGNED from (0, 100) to
(100, 150), and draws the extension lines of all three at 60. The presenter now writes the same, so a dimension slanted
in it reads the same in other programs, and one slanted elsewhere draws at its angle. The documentation of
[`DxfRotatedDimension.Oblique`](xref:GeometryHelper.Dxf.DxfRotatedDimension.Oblique) and
[`DxfAlignedDimension.Oblique`](xref:GeometryHelper.Dxf.DxfAlignedDimension.Oblique) said the angle ran from the square
of the dimension line; it runs from the line, nought standing for none.

**CHANGED.** The presenter's window opens a drawing in the background. The file is read and its display list and pick
index made on a worker thread, and the drawing is shown once they are made; meanwhile the status bar says "Opening
plan.dxf: drawing 45 %" with a Stop button, the view shown pans and zooms, another drawing open is shown, and Esc or Stop
stops the opening. A command typed or started meanwhile is not run, the command line saying why, so that none runs on a
drawing half drawn. A drawing of 1 000 000 entities that held the window for 15 s while it opened and drew its first
frame now holds it for 0.2 s at most while it opens, then 0.8 s for that frame; the display list is the one it was, item
for item. **NEW.** `MainViewModel.OpenAsync`, `OpensInBackground`, `IsOpening`, `OpeningText`, `CancelOpening` and
`CancelOpeningCommand`, and `CadDocument.PrepareDisplay`, which makes a space's display ahead, telling how far it has got
and stopping when asked. `Open` still opens a drawing before it returns unless `OpensInBackground` is set, as a test or a
screenshot needs.

**CHANGED.** The presenter draws the highlight of a large selection on several threads. Where several thousand items are
selected, the canvas is cut into tiles, a thread a tile, and an item reaching into several tiles is drawn once every one
of their threads has drawn what comes before it; a text, a hatch, a ray or a picture is drawn with the other threads
waiting. The picture is pixel for pixel the one a single thread draws: a test compares the two on a drawing of 100 000
entities, and the pictures of that drawing and of two sample drawings, at three zooms, plain, selected and hovered, with
lineweights on and off, hash as before. All of that drawing selected is highlighted in about 150 ms rather than 300 on
24 processors under .NET Framework 4.8, and with the whole workspace drawn, its canvas 1 000 by 696 pixels, the canvas
draws the selection in about 240 ms rather than 360. A selection crammed into a patch of a few hundred pixels gains
nothing.

**NEW.** The snap and grid a drawing keeps. `db.ViewportTable` holds the tiled viewports of model space as AutoCAD's
`ViewportTable`, each a [`DxfViewportTableRecord`](xref:GeometryHelper.Dxf.DxfViewportTableRecord), and
`db.ViewportTable["*Active"]` gives SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE, SNAPANG, SNAPSTYL, SNAPISOPAIR,
GRIDDISPLAY and GRIDMAJOR as `SnapEnabled`, `GridEnabled`, `SnapIncrements`, `GridIncrements`, `SnapBase`, `SnapAngle`,
`IsometricSnapEnabled`, `SnapPair`, `GridDisplay` and `GridMajor`, read from the groups 75, 76, 14 and 24, 15 and 25, 13
and 23, 50, 77, 78, 60 and 61, as AutoCAD 2021 opened a drawing whose `*Active` has a snap of 2.5 by 5 and a grid of 10
by 20 with SNAPUNIT (2.5, 5) and GRIDUNIT (10, 20). Each is set in place and kept by a save, bit for bit.

**CHANGED.** The presenter's SNAP, GRID, ORTHO and lineweights follow each drawing, as AutoCAD 2021's do. A drawing
opened shows SNAP and GRID with their spacing from its `*Active` viewport, ORTHO from `$ORTHOMODE` and its lineweights
from `$LWDISPLAY`, and its tab shows them again when it is shown; a drawing with none of them takes the window's, as
before: SNAP and ORTHO off, GRID and the lineweights of Global settings, a spacing of 10. Switching one, or setting the
snap spacing, writes it into the drawing shown, so that a save keeps it; LWDISPLAY set with a drawing open no longer
changes Global settings. The snaps step by the drawing's SNAPUNIT and the grid is drawn at its GRIDUNIT, 10 across and
20 up for a GRIDUNIT of (10, 20). POLAR, OTRACK, OSNAP and DYN stay the window's.

**NEW.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader) reads the types it left out, in all six versions (AutoCAD
2000 to 2018): VIEWPORT, so a layout comes with its viewports, numbered as AutoCAD numbers them; HATCH with every kind
of boundary, pattern lines, solid fill and gradient; LEADER, MULTILEADER with MLEADERSTYLE, and TOLERANCE; IMAGE with
IMAGEDEF and IMAGEDEF_REACTOR, and WIPEOUT; MLINE; REGION, 3DSOLID and BODY of AutoCAD 2000 (their SAT text) and from
AutoCAD 2013 (the GUID of their data); ACAD_TABLE to AutoCAD 2007 and TABLESTYLE to 2007; the colour of a book with its
name, from its DBCOLOR; an attribute of several lines of AutoCAD 2018 with its MTEXT; and the proxy graphics of an
entity where AutoCAD saved them. Each was checked against AutoCAD 2021's own DXF of the same drawing in every version:
eight drawings more, 0 differences outside what AutoCAD itself changes as it writes. See the guide page "Reading DWG
files" for what is still left out: tables and table styles from AutoCAD 2010, SHAPE, the ACIS data of AutoCAD 2004 to
2010 and of the data store, MATERIAL, VISUALSTYLE and the other style objects, proxies, OLE frames and the data of
dynamic blocks.

**FIXED.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader), from AutoCAD 2004: an entity of a colour of a book (as
PANTONE Yellow 012 C) was left out as broken, with a Warn line; it is read with that colour. An element of a multiline
style before AutoCAD 2018 in Continuous, or any linetype but ByLayer and ByBlock, came out ByLayer; it comes out with
its linetype.

**FIXED.** `DxfViewport.GetFrozenLayers` and `IsLayerFrozenInViewport` read the frozen layers of a DXF of AutoCAD 2000,
which AutoCAD writes under group 341 rather than 331; they found none.

**FIXED.** The plane the DXF editing model finds for a 3D polyline or a spline no longer throws on points it cannot
place. Points a nanometre off one line, as (0, 0, 0), (1 000, 0, 0) and (2 000, 1E-9, 0), or a normal given of length
1E-7, threw "Cannot normalize a zero-length vector" out of `DxfPolyline3d.GetOffsetCurves`, the offsets of
[`DxfSpline`](xref:GeometryHelper.Dxf.DxfSpline), JOIN and the DWG reader; they now lie in their plane, (0, 0, 1) for those
points. Points that give no plane at all (none, one, all within the point tolerance of the first, any not finite, or
so far apart that the distance between them is past the largest double) give none: a curve of them is offset as one
that lies in no plane, and the DWG reader still leaves out a spline of points 1E+233 apart as a broken object, now by
its own check rather than by the throw.

**FIXED.** `Try` methods that threw on shapes the library accepts now answer. A reflection fuzz of every public `Try`
method, 500 000 calls with ordinary and degenerate shapes, found these:

- A box with a side of nought, as the bounding box of anything flat, threw "A box with a zero size on some axis has no
  faces to build" out of `TryIntersectWith` of an arc, a circle, an ellipse, a triangle, a polygon, a face and a plane,
  out of `TrySplitBy` of a curved chain or loop, and out of the booleans of a box, `GeoObb3.TryUnion`, `TryIntersect`
  and `TrySubtract`, and of a body with a box; an empty `GeoAabb3` threw too. An arc, a circle or an ellipse now meets a
  box flat on one axis where it passes the rectangle across it, the arc upright through (50, 0, 0) and (50, 100, 0) the
  box 100 by 100 by 0 at those two points, and a box flat on two axes or three nowhere; a triangle, a polygon, a face or
  a plane against a box flat or empty answers false, and so does a boolean with a flat box, with no result.
- Two ellipses, one a circle of radius 0.0001 standing upright in the plane of the other within the planar tolerance,
  threw "The major axis needs a direction"; the small one is now laid out by its minor axis.
- `GeoFace2.TrySplitBy` of a face whose boundary or a hole encloses no area at the global tolerance threw; it is not cut,
  and comes back whole on the left.
- `TryChamferAt` of a polygon, 2D or 3D, threw where the cut left fewer than three corners, the triangle (0, 0), (100, 0),
  (0, 100) cut 100 back each way from the origin; it is false, and the polygon stays.

Everything a `Try` method answered before it answers bit for bit the same.

**FIXED.** A spline against a curve far smaller or far larger than itself is answered in milliseconds. The same fuzz found
21 calls of `TryIntersectWith` between a `GeoSpline2` or `GeoSpline3` and a circle, an arc, an ellipse, an edge or
another spline still working after ten seconds, either way round; each now takes under 5 ms, and its places are those
dense sampling of the two curves finds:

- A piece of a curve was looked at all along for a stretch lying along the other, every sixty-fourth of the size of the
  smaller curve: a piece of the circle of radius a million, 1.5 million long, every 0.31 against an ellipse of radius 10.
  Only the share of it near the other curve is looked at now. The ellipse of radii 10 and 5 about (1 000 000, 0) crosses
  that circle at (999 999.9999875, 5) and (999 999.9999875, -5).
- A curve no larger than the point tolerance, as a circle of radius 1E-4 or 1E-12 at the tolerance 0.001, was looked for
  as such a stretch, the other curve every 3E-14 of its length for the circle of 1E-12. It lies along nothing, and is
  not looked for one: the polyline (0, 0), (100, 0), (0, 100) meets the spline of either circle about (100, 0) once.
- A part of such a curve that is not straight was cut until straight against each part of the other curve near it: a
  line through a circle of radius 1E-4 measured 2 048 parts of it, a cubic through it twenty times took 7.9 s. It is
  measured as it is against a straight part, and the other curve is cut instead.
- A curve through a circle of radius 1E-12 a hundred from the origin, where the numbers are rounded to 1E-11, was read
  as running along it at every meeting and met it nowhere; it meets it once.
- An ellipse of radius 1E-12 standing upright in the plane of a `GeoSpline3`, in it only within the planar tolerance,
  threw "The major axis needs a direction", as the fuzz found once the calls before it were answered; it is laid out by
  its minor axis, and a curve through it meets it once.

The places of other splines are bit for bit the same, and found a fifth faster: 6 528 places among 40 splines and 120
circles, ellipses, lines and arcs take 1.51 s, where they took 1.89.

**FIXED.** The presenter's typed lengths and angles: a number past the largest double, as 400 nines in `999…9'`,
`1-999…9/2` or `999…9d`, threw an `OverflowException` out of the prompt in .NET Framework, and `NaNg`, `NaNr` and
`Infinityr` were read as angles of NaN and infinity. Each is now not read.

**FIXED.** A radius dimension made in the presenter with its words put inside the circle under DIMTMOVE 1 or 2 is drawn
as AutoCAD 2021 draws it. Under 1 its words stay where DIMATFIT 3 keeps them whatever DIMATFIT says: "R12" put 11 out
under DIMATFIT 0 stands 3.4 out, not in the middle of the line. Under 2, words on the line over the centre or the rim put
the arrowhead outside the rim, pointing in, and the line runs whole from the centre to the rim: "R20" put 1 out. Put 8 out
they leave the arrowhead inside and the line broken about them, as before. Of 212 pictures of radii from 3 to 26 with
their words put 0.5 to 11 out, 212 match AutoCAD's where 30 of the first 120 did; round 5's radius-3 cases now match too
(154 of 192 there, from 140). The diameters of that round are left as they were.

**FIXED.** DIMTEDIT Left and Right in the presenter put a linear dimension's words where AutoCAD 2021 puts them: an
arrowhead and a gap in from the extension line, then the words with a gap about them, where they stood two arrowheads
in. "100" of Arial on a line at 30 degrees stands 6.733 in from its end, not 7.983. The left of a vertical line is its top,
as AutoCAD takes it, and under DIMTIH the words stand level, centred on the slanted line, which is broken about them.
Of 336 pictures of aligned dimensions at six angles, Left and Right under DIMTAD 0 and 1 and DIMTIH 0 and 1, and words
moved out under DIMTOH 0 and 1, 336 match AutoCAD's where 264 did; DIMTOH already matched.

**NEW.** The presenter has DIMJOGGED (DJO, JOG), AutoCAD 2021's jogged radius, with its prompts word for word ("Select
arc or circle:", "Specify center location override:", "Specify dimension line location or [Mtext/Text/Angle]:", "Specify
jog location:", and "Object selected is not a circle or arc." for anything else). Its line runs from the centre drawn
along the radius's parallel, across at DIMJOGANG and on to the arrowhead at the rim, the jog half way between the two
lines: a circle of 50 about the origin, its centre drawn at (0, -100), its words at (30, 0) and its jog at (15, -50) has its
lines from (0, -100) to (65, -100), on to (-35, 0) and on to (47.5, 0). Words put on the other side of the rim from the
foot of the centre drawn go three quarters of the way from that centre to the rim, as AutoCAD puts them, which is the
radius at -23.6 degrees round 5 could not explain. Its picture is drawn again when its style changes, and its grips are
its chord point, the centre drawn, its jog (the new `CadGripKind.Jog`) and its words. Of 152 pictures drawn by AutoCAD,
varying the words' place, the centre drawn, the jog and DIMJOGANG, 145 match; the seven left have a jog given past the
rim or far off its middle line, DIMJOGANG 90, or words on the line pushed past the arrowhead.

**FIXED.** `DxfRadialDimensionLarge` read and wrote its points under the wrong groups. AutoCAD 2021 writes the centre
drawn as group 13, the jog as 14 and the chord point as 15, where the library took 13 for the chord point, 14 for the
centre drawn and 15 for the jog, as ezdxf 1.4.5 does; a jogged radius AutoCAD made was read with its radius measured to
its jog. `ChordPoint`, `OverrideCenter` and `JogPoint` now read and write AutoCAD's groups. AutoCAD writes nought for
`JogAngle` (group 40) and draws the jog at the dimension's DIMJOGANG.

**FIXED.** The presenter measures a dimension's words with the MTEXT layout the canvas draws them with, the letters by
their ink, and fits them as AutoCAD 2021 does. Words in the canvas's own ghsimplex were taken as 0.7 of their height a
letter; "100" of ghsimplex widened 1.5 times is now 9.107 wide, as AutoCAD lays it out. Words on a linear dimension's
line (DIMTAD 0) need two gaps more each side to stand between the arrowheads, so "9" of ghsimplex, 1.548 wide, moves out
of a dimension of 9; under DIMATFIT 3 words wider than the arrowheads' room stay inside and the arrowheads go out; and
under DIMATFIT 0 to 2 a radius's words go where the fit puts them however far out its line was put, "R20" put 35 out
standing 8.75 from the centre. Of 512 new pictures in ghsimplex, in Arial narrowed and slanted and in ghsimplex widened,
499 match AutoCAD's where 425 did; the 1 522 of earlier rounds match as before (1 443).

**CHANGED.** The presenter's status bar starts with SNAP off and POLAR on, as AutoCAD 2021 starts: SNAPMODE is 0 and
AUTOSNAP 63, whose bit 8 is polar tracking. With SNAP on from the start, a click away from any object snap went to the
nearest point of the 10 by 10 grid; it is now taken where it is, and AUTOSNAP reads 63, not 55. GRID, ORTHO, OTRACK,
OSNAP and DYN start as before, which is AutoCAD's start too, GRID aside: AutoCAD keeps GRIDMODE in each drawing, and the
presenter reads none from the file.

**FIXED.** `DxfTable.IsTitleSuppressed` and `IsHeaderSuppressed` read only the table's own 280 and 281, so a table of no
flag of its own in a table style that leaves the title or the header out was drawn and read with a title and a header it
does not have. They now read the title or header left out where the table's own flag or its style's says so, as AutoCAD
2021 reads a table, and `RowType`, `RecomputeTableBlock`, the row kinds rows put in and taken out keep and the presenter
all follow. A table of four rows in a style that leaves the title out has its first row the header and the rest data,
"R1" in its second row drawn 3 high at the bottom centre at (45, -28.5) as the data, not 5 high at the middle right as a
header; AutoCAD 2021 gave the same rows `_HEADER` and `_DATA` and drew "R1" there. A table's own `280 0` does not show a
title its style leaves out: AutoCAD read one of its own `93 1`, `280 0` in that style without its title.

**NEW.** Tab in the last cell of a table in the presenter adds a row, as AutoCAD 2021's table editor does. The new row goes
under the last, empty, with the cell styles, alignments and height of the last row, and its first cell opens for typing;
the row is an undo step of its own after the cell's words. AutoCAD's `InsertRowsAndInherit` gave a row added under a row
60.43 high a height of 60.43 and the alignments of the row above, and the presenter's row is the same.

**CHANGED.** A table in the presenter grows its rows to hold their words, as AutoCAD 2021 lays a table out. A cell's text
wraps at the cell's width less its margins, laid out as an MTEXT of that width by the presenter's own MTEXT layout, and its
row becomes as high as its tallest cell needs: the words' actual height and the margins above and below, one line of four
thirds of the text height at least. A row is never made lower than it was. This happens whenever the presenter draws a
table anew: a cell typed, Find and Replace, a table style changed or matched, and a table made by TABLE. In AutoCAD's
Standard in ghsimplex, with columns 30 wide, the title "Bill of steel members for the north frame of the hall" makes its
row 39.29 high, "Section and grade" the header's 24.21 and "HEA300 S355 with end plates both sides" its row 45.43, and
each text stands where AutoCAD 2021 put it, all within 1E-5 of AutoCAD's numbers. An MTEXT line also takes a word whose
ink ends exactly on the width, as AutoCAD does: "with end" 27 wide in ghsimplex 4.5 high is one line, where it broke in two.

**FIXED.** The presenter could not snap to, pick or select the entities of a DXF file that writes no handles, as one of
R12 written by hand with neither header nor tables: the pick index holds an entity by its handle, and they had none.
With SNAP and OSNAP on, LINE's next point near the corner (4 000, 3 000) of such a room went to the grid point
(3 990, 2 990) instead of the endpoint. `CadDocument` now gives each entity of such a file a handle on opening it, as
AutoCAD does: the next above every handle of the file, in its order, with `$HANDSEED` above them and `$HANDLING` 1, a
header made where it has none, and reads the drawing anew with them. They are written, so that an undo, which reads the
drawing back from its saved bytes, finds each entity under the same handle, and the drawing is saved with them, as
AutoCAD saves it. The corner snaps as an endpoint, 3 905.12 from (1 000, 500), the column's rim gives its quadrant and
centre, and a wall erased, undone and redone is picked as the same wall. A file with handles opens as before; the snap at
the cursor on the 100 000-entity drawing costs about 1.1 µs either way. The presenter's order was AutoCAD 2021's already
and is now pinned by a test: a point typed is taken as typed, and an object snap under the aperture beats object snap
tracking, which beats polar tracking or ortho, which beat the grid.

**FIXED.** The presenter draws a layer switched off or frozen in a third of the time it took. An entity on a layer that
is not shown is passed over before its own visibility is read from its groups, which cost near a microsecond each, and
a display list made again after an edit is sized as the one before, not grown by doubling. In a drawing of 100 000
entities the layer of a third of them switched off is drawn again in 23 ms, not 85, and everything drawn is item for item
as before. The three tests that time this drawing now run alone: run beside each other, each one's garbage collections
stopped the others' timers, and the layer switched back on, 110 to 160 ms alone, was timed at 230 to 540 ms.

**CHANGED.** Undo and redo in the presenter no longer wait for the whole drawing to be read back on a large drawing. After
every step, undo and redo, a worker thread reads ahead the drawings the next undo and the next redo go to, from the bytes
an undo has always read them from, and makes their index of handles; the undo then takes the drawing ready and draws
again only the entities its step touched. The undo states are also packed and unpacked in pieces of 4 MB side by side. On
a drawing of 1 000 000 entities, medians of five, the undo of a one-line MOVE and its display take 0.4 s where they took
2.2 s once the drawing is read ahead (about 2 s after the step), and 1.5 s where they took 2.6 s when asked at once; the
undo of an ERASE of 10 000 objects takes 0.8 s where it took 3.0 s, and finding an entity by its handle after an undo 0 ms
where it took 0.2 s. Every drawing an undo or a redo gives is the one saved at that step, byte for byte. The read-ahead
holds up to three more drawings, each about 330 MB at that size, so the peak working set rose from about 3.2 GB to 3.7 GB.

**NEW.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader) reads DWG files of AutoCAD 2007, 2008 and 2009 (AC1021),
which it refused by name until now. Their container is read: the file header and the pages coded by Reed-Solomon, the
compression of R2007 and its CRCs of 64 bits, each checked. The eight test drawings of AutoCAD 2021 in AC1021, a drawing
of anonymous blocks and one of 100 000 lines compare with AutoCAD's DXF of them record for record; a drawing of tilted
splines differs only in the two spline normals the other versions differ in. The drawing of 100 000 lines reads in
750 ms, its AC1018 copy in 747 ms in the same runs.

**FIXED.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader), in every version: an object whose decoded values the
library's geometry refuses, such as a damaged SPLINE whose plane cannot be worked out, is left out with the Warn line of
broken objects, the others read. It refused the whole file with a `FormatException` before. Found by the fuzz of the
AC1021 reader, 2 of 8 000 changed files.

**CHANGED.** The colours of [`DxfColor`](xref:GeometryHelper.Dxf.DxfColor) by index are AutoCAD 2021's, as its
`Color.FromColorIndex(ColorMethod.ByAci, i).ColorValue` gives them, where they were ezdxf's table. 197 of the 255
indexes change: the darker shades of each hue (12 was 0xA50000 and is 0xCC0000, 14 was 0x7F0000 and is 0x990000), a few
entries of ezdxf's table that were wrong (109 was 0x135817 and is 0x264C2F), and the greys 250 to 254, which are
0x333333, 0x5B5B5B, 0x848484, 0xADADAD and 0xD6D6D6 (250 was black). The presenter draws these colours, and the nearest
index of a true colour is found among them: 0x333333 is 250, RGB 10, 200, 30 is 102. The indexes 1 to 11, the two
brightest of each hue (10 and 11, 20 and 21, and on) and 255 are unchanged.

**CHANGED.** The presenter's MTEXT editor lays out its words as the canvas draws them, as AutoCAD 2021's in-place
editor does. It used to set each paragraph on one line, 1.25 times its tallest letters below the last, in WPF's own
measure, with stacks written inline. It now wraps at the width and spaces its lines at least or exactly as the MTEXT says,
with its stacks stacked, its tabs, indents, paragraph spacing and columns set and its mask and frame drawn, all by the
display's own layout and fonts. "Pipe {\H2x;BIG} size 1\S1/2; inch" 40 wide in Arial 2.5 high breaks and stands in the
editor where the drawn MTEXT does. The caret and the selection follow the wrapped lines: a word typed past the width takes
the caret to the next line, and Home, End, the arrows up and down and a click work on the lines as drawn. The new
[`CadMTextEditorLayout`](xref:GeometryHelper.Dxf.Viewer.CadMTextEditorLayout) gives the parts and the place of each
letter, and `CadMTextEditBox` takes the `Document` whose fonts set them. Typing in an MTEXT of 2 000 letters costs about
1.5 ms a key, the layout 0.5 ms and the drawing 1 ms. The ruler still shows no paragraph indents or tab stops, and the
columns cannot be dragged.

**FIXED.** The fields of dynamic input in the presenter followed the raw cursor while OSNAP, ortho or polar held the
rubber band elsewhere. They now measure to the point the band ends at, as AutoCAD's do, and stand by that band. With the
cursor 4 pixels off the end (4 000, 0) of a wall, LINE's next point from (1 000, 1 000) showed 3 185.00; it shows
3 162.28 and 341.6, the length and the angle to the end. A distance typed alone goes along the band as well: with ortho
on and the cursor at (1 300, 1 700), 200 typed from (1 000, 1 300) ends the line at (1 000, 1 500), straight up, where
it went towards the cursor before. `CadCursorEventArgs` gives the band's end as `BandX` and `BandY`, and
`MainViewModel.SetCursor` takes it.

**CHANGED.** More second points show the length and the angle in the presenter's dynamic input, where they showed X
and Y: AutoCAD 2021's pointer input gives a second or next point in polar form by default, relative Cartesian for
RECTANG alone. They are the second point of MIRROR's line, of ROTATE's and SCALE's Reference by two points, of ALIGN's
destinations, of BREAK's First point, of DIST, of TEXT's Align and Fit, of DIMLINEAR's and DIMALIGNED's second extension
line, of DIMCONTINUE and DIMBASELINE, the endpoints of an angle in MEASUREGEOM and DIMANGULAR, and the next points of
AREA and of REVCLOUD's Polygonal. MIRROR's second point 300 right of and 400 above the first shows 500.00 and 53.1, not
300.00 and 400.00. The corner of a box (RECTANG, ZOOM's window, MTEXT, REVCLOUD's Rectangular) and the location of a
dimension line or a leader keep X and Y.

**FIXED.** CIRCLE's 3P in the presenter showed one box of text at each of its points, "500.00 < 53.1°" at the second,
because the prompts also take _tan. Its first point now shows the X and Y fields, and its second and third points the
length and the angle from the point before, as AutoCAD 2021's pointer input gives a first point and a second or next
point. A number typed goes into a field and Tab locks it: from (1 000, 0) and (1 000, 400), 600 locked with the cursor at
(1 600, 0) takes the third point 600 along the band. _tan typed still asks for the object touched.

**CHANGED.** The dynamic input of the presenter shows the length and the angle in their fields alone, with no dimension
line, extension lines, angle arc or dashed base line drawn under them. That is every prompt with these fields: the
second point of LINE, PLINE, ARC, CIRCLE's 2P, ELLIPSE's axis, POLYGON's edge, XLINE, RAY, MOVE, COPY, STRETCH, OFFSET
by two points and a grip stretched or moved, a distance asked from a base point and an angle asked from one. The length
sits beside the middle of the rubber band, 8 pixels off it on one side, and the angle 50 pixels along the band from the
base point, 8 pixels off it on the other, so that neither covers the band. The prompt's box never hides a field: where
it would, the length goes to the band's other side or the box moves up or down clear of the fields, whichever moves less.
With the cursor by the canvas's right edge the box stands left of it, and over a band rising 11 degrees it covered the
length, now set below the band. Typing, Tab to lock and a held value holding
the band work as before: 300 locked at a line from (1 000, 1 000) with the cursor at (1 300, 1 400) still ends it at
(1 180, 1 240). RECTANG's X and Y fields and the X and Y of pointer input are unchanged.

**NEW.** EXPORT and -EXPORT in the presenter, as AutoCAD 2021 has them. EXPORT's Export Data dialog lists AutoCAD's
types: 3D DWF, 3D DWFx, ACIS, Bitmap, Block, DXX Extract, Encapsulated PS, IGES, Lithography, Metafile, V7 DGN and V8 DGN.
Bitmap is written as BMPOUT writes it. The other types have no writer and are refused, with nothing written:
`Metafile (*.wmf) has no writer in the presenter: nothing was exported.` -EXPORT asks
`Enter file format [Dwf/dwfX/Pdf] <dwfX>`, then `Enter plot area [Display/Extents/Window] <Display>:` (on a layout
`[Current layout/All layouts]`), then `Detailed plot configuration? [Yes/No] <No>:`, word for word as AutoCAD 2021's
console asks them. It then asks for the file in a Save As PDF dialog, or on the command line with FILEDIA 0, and writes
the PDF with -PLOT's writer. Dwf, dwfX and All layouts are refused. The format answered becomes the next default. The
file dialogs go through `MainViewModel.AskExportFile` and a [`CadExportJob`](xref:GeometryHelper.Dxf.Viewer.CadExportJob),
and Other Formats under Export in the application menu, greyed until now, runs EXPORT. The registry holds 175 commands.

**NEW.** The presenter's command line searches the drawing's content, as AutoCAD 2021's Search Content: below the
commands and system variables, under a rule, the AutoComplete list holds the blocks, layers, hatch patterns, text styles
and dimension styles whose names begin with the letters typed, then those with the letters inside, each kind by name and
marked at the right. WAL typed in a drawing of the block WALL_TAG and the layers WALLS and WALL-FINISH lists
`WALL_TAG Block`, `WALL-FINISH Layer` and `WALLS Layer`; a kind's keyword (BLOCK or INSERT, LAYER, HATCH, TEXTSTYLE or
STYLE, DIMSTYLE) lists every name of the kind. Choosing a row acts on it: a layer is made current through CLAYER, a block
inserted through -INSERT, which then asks the insertion point, a hatch pattern given to HATCH, a text style or a
dimension style made current. Search Content of the command line's Input Settings, greyed until now, switches it and is
kept in the settings file; it is on by default, as in AutoCAD. As AutoCAD's help says, the content is searched at the
command line only, not in the box of dynamic input at the cursor. AutoCAD's choice and order of the kinds (Input Search
Options), its collapsible section and its picture of a hatch pattern under the pointer are not built: they need a dialog
and a list of their own.

**FIXED.** The presenter's dimension pictures follow DIMTMOVE as AutoCAD 2021 draws them. Words the fit moves out of a
linear dimension stand over its line under DIMTMOVE 2, on the side away from the points, DIMEXE, one and a half DIMGAP
and half their height out, their height more where they read below it ("4" of a dimension of 4, 5.9375 below the middle
of its line), and under DIMTMOVE 1 beside a leader to that place and a landing; an angle's go over its arc along the
middle of its sweep, past a leader under 1. DIMTEDIT of a linear dimension under DIMTMOVE 0 runs the line through the
point given and stands the words above it there, on it with the line broken for DIMTAD 0, where the line ran a gap and
half their height under them; under 1 a leader is drawn to words further from the line than DIMGAP and half the heights
of the words and the arrowheads, and none to words nearer; under 2 the words' middle is the point, the line broken where
it runs through them. A dimension of AutoCAD's with its words moved is drawn as AutoCAD drew it. Of 846 pictures
AutoCAD drew for this, 762 match to 0.001. Left: 56 angles whose arc passes exactly through their points, where
AutoCAD's side of one extension line follows the rounding of the coordinates, and 28 radii and diameters of a small
circle made under DIMTMOVE 1 or 2 with their point inside it.

**FIXED.** Two more rules of the presenter's radius, diameter and angular pictures follow AutoCAD 2021. A diameter's
words on its line (DIMTAD 0) need their width and a gap each side, and with the arrowheads inside the arrowheads' room
as well, as a linear dimension's do: "%%c6" 4.357 wide now stands at the centre of a diameter of 6 under DIMATFIT 1,
DIMTOFL's line through the circle broken a gap clear of it, where it stood outside. An angle's extension lines run from
DIMEXO off its points to DIMEXE past the arc wherever that is any length, as a linear dimension's: with its points 20
from the vertex and its arc 20.3 out they run from 20.625 to 21.55, where none were drawn. Checked against 70 new
pictures AutoCAD drew, all matching to 0.001; the 676 of the earlier rounds still match.

**NEW.** PURGE and -PURGE of the presenter purge groups, as AutoCAD 2021's do: the Groups option of -PURGE and of its
All, and a Groups node in the tree of the Purge dialog. A group is purged when it holds nothing, whether it was never
given a member or its members were all erased, named or unnamed: of a drawing with the groups EMPTY and FULL and an
unnamed group whose one line was erased, -PURGE Groups says `Deleting group "*A1".`, `Deleting group "EMPTY".`,
`2 groups deleted.` and keeps FULL, and a second run says `No unreferenced groups found.`. Find Non-Purgeable Items
lists FULL with `Group FULL holds objects.`.

**FIXED.** A disabled drop-down and a disabled button of the presenter look disabled. The shared styles of the theme
gave neither a disabled look, so the Live section, Visual style and Background boxes of New View, the Lighting box of
Drawing Units and the ribbon buttons that need a drawing, or are not built yet, read as ones to press, and a disabled
drop-down still lit its border under the mouse. A disabled drop-down now shows its words muted and its box at half
strength and does not answer the mouse; a disabled button shows its icon and words at 45 per cent.

**FIXED.** The presenter's Layer States Manager restores a state into the current viewport of a layout where "Apply
properties as viewport overrides" is ticked; the tick was read and then ignored, and the layers themselves were changed.
A state that has the layer A red and 0.50 wide, B off and C frozen, restored so into a viewport after A was made green,
leaves A green in the drawing and gives it a red, 0.50 override in that viewport, freezes B and C there, and leaves
every other viewport alone; a property the state and the layer agree on takes its override off, and a layer the state
has on and thawed is thawed there. With "Turn off layers not found in layer state" the layers the state does not name
are frozen in the viewport. One undo step, as the restore into the layers is. On the Model tab, and on the paper with
no viewport chosen, Restore changes the layers as before.

**FIXED.** `DxfLayerStateManager.GetLayerStateNames(includeHidden, false)` left out a layer state imported from a .las
file. AutoCAD 2021 writes such a state with its layers by name (group 8: `XR_DOORS`, `0`), which the library took for
the mark of an xref's state; `-LAYER` stAte `?` lists it with the drawing's own. A state is now an xref's only where it is
named `xref__state` and every layer of its record is named as `xref|layer`, as AutoCAD writes
`70-b-xref-source__GH_XR_STATE` of the layer `70-b-xref-source|XR_WALLS`.

**FIXED.** The presenter's typed and shown angles follow ANGBASE and ANGDIR as AutoCAD 2021's do; they were read from
the X axis counter-clockwise whatever UNITS had set (only an arc's included angle followed ANGDIR). With ANGBASE 90 and
ANGDIR 1, `@100<30` now ends at 50, 86.6 and not 86.6, 50; 30 at ROTATE turns 30 degrees clockwise, the base left out
as AutoCAD leaves it out of a turn; DIST writes a line to the east as "Angle in XY Plane = 90", and LIST, the Properties
palette, the cursor tip, the coordinate readout and the angle box of dynamic input count the same way. `@100<<30` and
`@100<<<30` end at 86.6, 50 whatever the settings. Prompts whose default is a direction offer it counted
("Specify rotation angle or [Copy/Reference] <90>:"). New: `CadUnits.ToDirection` and `FromDirection`,
`CadCommandSession.CountAngle`, and `CadPrompt.AsksTurn` and `AsksPlainAngle`, which say how the session reads an angle
typed at a prompt. A drawing with ANGBASE 0 and ANGDIR 0 behaves as before.

**NEW.** `DwgReader` (namespace `GeometryHelper.Dwg`) reads a DWG file of AutoCAD 2000 (AC1015), 2004 (AC1018), 2010
(AC1024), 2013 (AC1027) and 2018 and later (AC1032) by the library's own code, as the drawing its DXF would give:
`DwgReader.Read` gives the `DxfDocument` of `DxfReader` and `DwgReader.Open` the `DxfDatabase`, from a path or a stream.
Read are the header variables, the classes, the nine tables, the blocks and layouts, LINE, POINT, CIRCLE, ARC, ELLIPSE,
SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE with its vertices, TEXT, MTEXT, ATTDEF, INSERT with its ATTRIBs, SOLID, TRACE,
3DFACE and DIMENSION, and of the objects DICTIONARY, XRECORD, LAYOUT, GROUP and MLINESTYLE, each with its layer,
linetype, colour (from 2004 with its true colour and transparency), lineweight, owner, reactors, extension dictionary
and extended data; texts of 2010 and later, UTF-16 in the file, come out letter for letter. Eight drawings made in
AutoCAD 2021, in each of the five versions, and a drawing of 100 000 lines in each come out group for group as the DXF
AutoCAD writes of them, numbers to nine places; the 100 000 lines read in about 0.75 s, against 0.27 s for AutoCAD's
14 MB DXF of them. The types not read yet, VIEWPORT, HATCH and LEADER among them, are left out and counted by type in
one Debug line; a DWG of another version, AutoCAD 2007 (AC1021) among them, is refused with a `NotSupportedException`
that names it. Every check sum is checked, from 2004 those of every compressed page too: a wrong one in the header, the
classes, the object map or a page refuses the file with a `FormatException`, and one broken object is left out with a
Warn line while the others are read. See the guide page "Reading DWG files".

**CHANGED.** The presenter lays the words of an MTEXT out as AutoCAD 2021 does, where it drew one line a paragraph 1.25
heights apart. The words wrap at the MTEXT's width (group 41) at their spaces alone, never after a hyphen, a word too
long standing whole past the width and `\~` holding two words together; a width of 0 wraps nothing. Lines stand 5/3 of
the height apart times the factor (44): exactly so under `Exactly`, and under `AtLeast` further where a taller run or a
stacked fraction needs the room. `\S` stacks its parts with a bar (`/`), with none (`^`) or along a diagonal (`#`), each
part as high as its ink, and `\A` sets the words beside it at its bottom, middle or top. The paragraph codes `\p` set
indents and margins, the alignment (left, centre, right, justified, distributed), tab stops (left, centre, right,
decimal; every 4 heights where none is set), the space before and after and a line spacing of the paragraph's own.
Static and dynamic columns take the lines that fit their heights, `\N` starting the next. The background mask and the
frame are drawn around the lines, the scale factor less 1 of a height out (1.5 for a frame alone). "II II II" 20 wide in
letters 10 high whose I is inked 1 in and moves the pen 4 is set as "II II" and, 16.67 below it, "II". Held against
AutoCAD's own fragments of 155 MTEXTs (536 lines), in an SHX font and in Arial: 150 have the same breaks with every line
within 0.2 % of the height of where AutoCAD puts it. `DisplayText2.Parts` is now set for every MTEXT: a text for each
run of each line, the mask (`DisplayFill2`) and the frame first; the box of the MTEXT's item is that of its parts.

**CHANGED.** The presenter's grips and EXPLODE of an MTEXT follow its new layout, as AutoCAD 2021's do. An MTEXT with a
width has a grip at each corner of its box besides its location (`CadGripKind.Corner`; the location alone for no width
or for columns), and a corner dragged takes its two sides of the box to the point: "II" over "II" 240 wide at the
origin, its top right corner dragged to (260, -12), is 260 wide with a defined height of 14.67 and located at (0, -12).
`CadGrips.Of` therefore gives four or five grips where it gave one, the location no longer always the first. EXPLODE
makes a TEXT of each run of each line where the picture draws it, and a LINE of the bar of each stack, where it made the
TEXTs of the library's own simpler layout.

**FIXED.** `DxfMText.ShowBorders` set in a drawing of R2018 with no background writes the colour and the scale of a
background (63 256, 45 1.5, 441 0) after its group 90 of 16: AutoCAD 2021 discarded the whole drawing without them
("Missing DXF group code: 90"). In a drawing of R2004 to R2013 it writes none, as before: there AutoCAD discards the
drawing that has them.

**NEW.** DIMARC (DAR), the presenter's arc length dimension, as AutoCAD 2021 makes it and stored as the library's
`DxfArcDimension`: Mtext and Text for its words, Angle for theirs, Partial for the length between two points of the arc,
and Leader, offered past 90 degrees of sweep, for a leader from the words' arc to the arc measured. Below 90 degrees the
extension lines run along the middle of the arc, from 90 up from the centre; the arc symbol stands before the words
(DIMARCSYM 0), over them (1) or nowhere (2); a point in the sector the arc does not sweep dimensions the rest of the
circle. An arc of 30 from 20 to 60 degrees dimensioned 40 out reads "21" 41.88 out at 40 degrees. Its pictures match the
69 AutoCAD drew to 0.01, and AutoCAD draws the dimensions stored the same.

**CHANGED.** The words of the presenter's dimension pictures are measured in the font of their text style as AutoCAD
measures them, not taken as 0.7 of their height a letter: "R20" of arial.ttf 2.5 high is 5.965 wide and 2.553 high. A
TrueType font is read from the Fonts folders of Windows, an SHX font where the fonts set on a document find it. The
526 pictures of linear, radius, diameter and angular dimensions AutoCAD drew now all match to 0.01 (35 before); a radius
of 6 takes "R6" inside, a point on the dimension line has its extension line, and an angle's ticks turn along the arc.

**CHANGED.** DIMTEDIT of an angle under DIMTMOVE 0 runs the arc through the point given and stands the words on it
there, above it as they read; a radius's or a diameter's words slide along its line, which stays, under DIMTMOVE 0 and
1, and stand at the point under 2. A radius or a diameter made under DIMTMOVE 1 or 2 keeps its line through the point
the circle was picked at, as AutoCAD 2021 does. The grips drag the words as before.

**NEW.** PLOT and -PLOT plot to images: GeometryHelper PNG (lossless) and GeometryHelper JPG (quality 90) beside
GeometryHelper PDF. Their sizes are in pixels, 1280 x 720 to 3840 x 2160, and Custom… makes one of 16 to 8 000 pixels a
side, kept between sessions; a raster device starts fitted and centred on white, with no plot style table and colour 7
black, and always plots to a file, asked on OK or with FILEDIA 0 on the command line. Model space fitted to 1920 x 1080 is
a PNG of exactly 1920 by 1080 pixels. New API: `CadPaper.PixelSizes`, `CadPaper.Pixels`, `CadPlotSettings.PngDevice`,
`JpgDevice`, `UseDevice`, `GlobalSettings.CustomPixelSizes` and `CadRasterWriter.WritePlot`.

**CHANGED.** Dynamic input in the presenter works at the cursor as AutoCAD 2021's. At the Command prompt the text typed
shows in a box 18 pixels right of the crosshair and 14 below it, with the AutoComplete list under it; the command line's
own list stays closed while DYN is on. In a command the box shows the prompt as AutoCAD words it, "Specify next point or",
with a ↓ that opens the prompt's options one per row, Esc closing the menu alone. The X and Y fields are relative to the
base point, as DYNPIFORMAT 0, # making them absolute: RECTANG's other corner shows 1 200.00 and 800.00 for a corner
1 200 by 800 from the first, and "500,300" typed there is a corner 500 by 300 from it. LINE's and PLINE's next point
show the length on a dimension line beside the rubber band and the angle on an arc from the X axis.
`MainViewModel.DynamicInputPrompt` no longer carries the options and the colon: "Specify next point or".

**CHANGED.** The presenter's Order Group dialog follows its board: the groups in a Group Name list, and the three boxes
labelled with the ranges the group allows, a group of twelve moved from 3 to 0 reading (0-11), (0-11) and (1-9).
Highlight < now steps through the members on the canvas one at a time, `Object 0 of 11 [Next/Previous/eXit]:`, Enter
bringing Object Grouping back; Object Grouping's own Highlight still shows the whole group at once.

**NEW.** `DxfDatabase.ViewTable`, the named views of a drawing as AutoCAD's `ViewTable`: `DxfViewTableRecord` has its
`CenterPoint`, `Height`, `Width`, `IsPaperspaceView`, `Target`, `ViewDirection`, `ViewTwist` and `LayerState`, the
layer state its layer snapshot is kept in, and is added, renamed and erased as AutoCAD 2021 writes a view: a view of the
window (10, 20) to (110, 80) is written with its centre (60, 50), 100 wide and 60 high, looking down Z with a lens of 50,
pointing at the visual style 2D Wireframe. Its extension dictionary is erased with it.

**NEW.** Layer states in the presenter, as AutoCAD 2021 keeps them: LAYERSTATE (LAS) opens the Layer States Manager (New,
Update, Edit, Rename, Delete, Import, Export, the properties to restore, Turn off layers not found in layer state,
Restore), and -LAYER (-LA), now a command of its own, has stAte with ?, Save, Restore, Edit, Name, Delete, Import and
EXport in AutoCAD's words. A state is an XRECORD of ACAD_LAYERSTATES under the layer table's extension dictionary, its
groups those AutoCAD 2021 wrote of the same drawing; a .las file Export writes of a drawing of the layers 0, A red, B off
and C frozen is AutoCAD 2021's to the byte, and Import reads it. Each run is one undo step; AutoCAD 2021 audits a drawing
with two states with no error and lists them.

**NEW.** Named views in the presenter: VIEW (V) opens the View Manager (Current, Model Views, Layout Views, Preset Views
greyed; Set Current, New, Update Layers, Edit Boundaries, Delete; the name, height and width) and its New View, and -VIEW
(-V) asks ?, Delete, Orthographic, Restore, Save, sEttings (Layer snapshot) and Window as AutoCAD 2021 does, ? listing
`"Stair core"` with M or P. A layer snapshot is the hidden layer state ACAD_VIEWS_ and the view's name, restored with the
view. AutoCAD 2021 audits a drawing with three views, one of a layout, with no error and restores them.

**NEW.** UNITS opens the presenter's Drawing Units, its Sample Output following each change, and Direction Control, whose
pick asks two points; OK writes LUNITS, LUPREC, AUNITS, AUPREC, ANGDIR, INSUNITS and ANGBASE as one undo step. -UNITS
stays typed. `CadUnits` writes 15.5 as 1.55E+01, 15.50, 1'-3.50", 1'-3 1/2" or 15 1/2 and 45 degrees as 45.0000,
45d0'0", 50.0000g, 0.7854r or N 45d0'0" E, and typed lengths and angles are read in the drawing's units: `1'3-1/2"<100`
in architectural units and grads is 15.5 straight up. The status bar shows coordinates in LUNITS where they are not
decimal.

**CHANGED.** The presenter's LAYER on the command line offers stAte after Unlock, as AutoCAD's -LAYER does; the registry
holds 173 commands.

**NEW.** The presenter previews the object a prompt is about to make, as AutoCAD 2021 drags it: `CadPrompt.Preview`
(`CadPreview`) gives the display items of what a point picked at the cursor would make, made by the command's own code,
and the canvas draws them in the current layer's colour. RECTANG asking its other corner draws the rectangle, not a dashed
line to the cursor; CIRCLE (radius, diameter, 2P, 3P, Ttr, Tan Tan Tan), ARC (every way, and on from the last line),
ELLIPSE and its Arc, POLYGON, DONUT, XLINE, RAY, PLINE (lines and every arc option, widths drawn), REVCLOUD, OFFSET,
STRETCH, the frame of MTEXT and DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMORDINATE, DIMCONTINUE and
DIMBASELINE do the same. ROTATE and SCALE move their ghost by a Reference too, and ARRAYRECT and ARRAYPOLAR show every
copy while their options are offered. A preview costs 6 to 26 microseconds for a circle or a rectangle, 0.2 ms for a
linear dimension.

**NEW.** The commands that change objects preview the change too, made by the code that makes it. FILLET and CHAMFER,
hovering the second object, draw both objects trimmed and the arc or line between them; TRIM in Quick mode draws the
piece a pick would cut away faded (`CadPreview.Fades`), EXTEND the object run on to its edge, BREAK the piece its second
point takes out; LENGTHEN DYnamic draws the object with its end at the cursor. HATCH draws the hatch of the areas picked
and the one under the cursor, the area kept while the cursor stays in it; TEXT a square of the height while its height
and rotation are asked, TABLE the table's outline, MLEADER the leader and landing, QDIM the whole row. ROTATE and SCALE by
Reference Points, -ARRAY's unit cell and ALIGN move the ghost as the objects move. A preview at a prompt for one object is
of the object under the cursor, and writes nothing into the history. A FILLET of two lines costs 38 microseconds, a TRIM
26, a hatch of a square with an island 0.1 ms, a QDIM row of three dimensions 0.76 ms.

**NEW.** TRIM and EXTEND preview in Standard mode too, against the cutting or boundary edges chosen, and with Shift held
they preview what the other does, as the pick with Shift then does: `CadPreview.Shift`, which the canvas sets from the
keyboard, is carried by the answer previewed, and `Fades` follows it. A line from (0, 0) to (50, 0) hovered in TRIM with
Shift held previews itself run on to the wall at 100, not faded.

**NEW.** -ARRAY previews its copies at the prompts that had none. The distance between columns can be shown by two
points, as AutoCAD takes it, and the copies follow the second; a Polar array shows its copies while the angle to fill
and the angle between items are asked, each measured from the centre to the cursor, and while it asks whether to rotate
them. Four items round the origin with the cursor at (-10, 10) preview three copies 45 degrees apart, the 135 degrees
the pick there fills.

**NEW.** PASTECLIP and PASTEBLOCK show the Clipboard's objects at the cursor while they ask the insertion point, held
by their base point as the paste puts them, and drawn by the layers and blocks they bring before the drawing has them;
text of another app is shown as the MTEXT it becomes. PASTEORIG asks no point and previews nothing. Nothing comes into
the drawing until the point is picked.

**NEW.** TEXT's preview follows the justification, and shows the words while they are typed. The square of the height
is held at the point as the text will be: a height of 5 at (10, 10) gives the square from (10, 10) to (15, 15) for Left,
from (7.5, 10) to (12.5, 15) for Center, from (7.5, 7.5) to (12.5, 12.5) for Middle, from (10, 5) to (15, 10) for TL; a
text fitted between two points gives the box as long as they are apart. At "Enter text" the canvas draws the words
typed so far in their style, height, rotation and justification, by the code that then makes the line
(`CadPreview.Typed`, which the window sets from the command line).

**NEW.** Dimensional input at the cursor beyond LINE and PLINE. The length and the angle from the first point show at
the second point of ARC, CIRCLE's 2P, ELLIPSE's axis, POLYGON's edge, XLINE, RAY, MOVE, COPY, STRETCH and OFFSET's
distance by two points, and at a grip stretched or moved, with Tab locking a field as before. A prompt for a distance
from a known point shows the length field alone (the radius of CIRCLE and POLYGON, ELLIPSE's other axis), and one for an
angle the angle field alone (ROTATE); RECTANG's other corner keeps X and Y, its two sizes, and SCALE's factor the one
box. A field that holds a number now holds the rubber band, the preview and a click: 300 locked as a line's length from
(1 000, 1 000) with the cursor at (1 300, 1 400) ends the preview and the line at (1 180, 1 240). New API:
`MainViewModel.IsDynamicInputLength`, `IsDynamicInputAngle`, `IsDynamicInputDimensional` and `HoldDynamicInput`.

**FIXED.** ROTATE's ghost in a UCS turned from the world's now turns by the angle the objects are turned: in a UCS turned
30 degrees it turned 30 degrees more than the result.

**NEW.** BATTMAN, the presenter's Block Attribute Manager: the attribute definitions of a block in its order, with the
INSERTs of it counted in the drawing and in model space, the modes as AutoCAD's letters I C V P L M and a tag given twice
in red; Move Up, Move Down, Edit Attribute (Attribute, Text Options and Properties) and Remove, written into the block by
Apply or OK, each one undo step; Sync brings every INSERT in line as ATTSYNC does, the values typed kept, and its Settings
choose the columns of the grid and whether Apply syncs at once. A block GB of GRID, LEVEL and REV with LEVEL moved up and
synced gives each INSERT LEVEL then GRID; AutoCAD 2021 audits the drawing with no error.

**NEW.** ATTDIA is a variable of SETVAR, 0 by default; at 1 INSERT and -INSERT ask the values of a block's attributes in
the presenter's Edit Attributes dialog, eight a page in the block's order, the constant ones left out and a value to be
verified asked once. A title block of eleven attributes opens on Page 1 of 2.

**NEW.** PURGE shows the presenter's Purge dialog: the named objects nothing uses by kind, with counts, a preview of a
block, text style or dimension style, Confirm each item, Purge nested items, the geometry of no length and the empty
texts, Purge Checked Items and Purge All, each purge one undo step; and Find Non-Purgeable Items, the objects in use with
the reasons ("Layer WALLS is the current layer.", "Used by 2 objects in model space and 0 in layouts.", "Referenced by
block GB.") and Select Objects. -PURGE stays typed, and now purges nested items too: a block OUTER inserted nowhere that
holds the only INSERT of INNER takes INNER with it, and the layer only INNER used, where it took one pass before.

**NEW.** CLASSICGROUP, AutoCAD 2021's Object Grouping dialog: the groups with Include Unnamed, Find Name, Highlight,
New, Remove and Add asked on the command line with the dialog hidden, then shown again; Rename, Description, Explode,
Selectable and Re-Order (Order Group: Re-Order, Highlight, Reverse Order) at once; OK keeps every change as one undo
step, Cancel takes them back. The registry holds 167 commands.

**FIXED.** `DxfPolyline2d.CurveFit` (PEDIT Fit) now gives AutoCAD 2021's arcs, vertex by vertex to 1E-11 on 60 polylines
fitted in its console. The tangent between the ends of an open polyline, and everywhere round a closed one, is AutoCAD's
blend of the two segments at the vertex weighted by the turns either side, not the circle through the vertex and its
neighbours: on (0, 0), (100, 0), (100, 50), (0, 80) the tangent at (100, 0) is 64.41 degrees, not 63.43, and a closed
triangle takes the bisector at each corner. The arcs of a segment meet where the curve runs along the chord, or for an
S-shaped segment with radii as the sines of the tangents' angles to it, not at equal tangent lengths. A segment whose
tangent at either end is within a millionth of a radian of its line, and a polyline of two vertices, stay straight with
no vertex added; two vertices on one point split the curve in two there; the vertex added after one given a tangent
keeps its direction (group 50), as AutoCAD writes it.

**FIXED.** `DxfPolyline3d.SplineFit` keeps the vertices of the curve after the first vertex of the frame, the rest of the
frame after them, as AutoCAD 2021 stores them and as the polyline of the plane already did.

**NEW.** A drawing whose CLASSES section lists no DICTIONARYVAR, as one the library makes new and the presenter's NEW
template, is given the class as AutoCAD 2021 lists it (AcDbDictionaryVar, ObjectDBX Classes, proxy flags 0, counted once
from R2004 on) when its first variable kept out of the header is set, where before the variable was refused. Set Current
of the presenter's multileader and table styles now works in a new drawing; AutoCAD 2021 audits such a drawing with no
error and reads CMLEADERSTYLE and CTABLESTYLE as set.

**NEW.** `Annotative` of a multileader style is set, as group 296 with no extended data, as AutoCAD 2021 writes the style
it makes annotative; the Annotative box of the presenter's Modify Multileader Style is enabled, and a new style started
from an annotative one is annotative.

**NEW.** The presenter lays out radius, diameter and angular dimensions as AutoCAD 2021 does: the words by DIMTAD, DIMTVP,
DIMJUST, DIMTIH and DIMTOH, what moves out where they do not fit by DIMATFIT, DIMTIX, DIMSOXD and DIMTOFL, the centre
mark and centre lines of DIMCEN, and an angle's words moved by DIMTEDIT under DIMTMOVE 1 (a leader and a landing) and 2.
A radius of 20 put 45 from its centre has its arrowhead outside the rim and the line from its back; an angle of 8 degrees
has its words and arrowheads outside past its first ray. Checked against 526 pictures AutoCAD 2021 drew of the same
inputs: 511 agree but for the width of the words, which AutoCAD measures by the font, and AUDIT finds no error.

**NEW.** The presenter's DIMTVP lifts the words of DIMTAD 0 by that many times their height (the line broken about them
below 0.7, as AutoCAD 2021), and a DIMGAP below nought draws a box round the words, words above the line standing clear
of it by the gap again.

**FIXED.** The dimension line of the presenter's dimensions ran to the tips of the arrowheads; it now starts at the back
of each, as AutoCAD 2021 draws it: a dimension of 100 with arrowheads of 2.5 has its line from 2.5 to 97.5, closed
filled ones too. The ticks, Dot small, Dot small blank, Integral and None run to the tip and, as AutoCAD's, never move
outside a short dimension.

**NEW.** Dynamic blocks in use, in the core: `DxfBlockReference.GetDynamicGrips()` gives the custom grips AutoCAD shows on
a dynamic INSERT, where its values put them (`DxfDynamicBlockGrip`: `GripType`, `Position`, `BasePoint`, `Direction`,
`PropertyNames`); on AutoCAD 2021's Architectural and Annotation samples every custom grip came out at AutoCAD's point and
in its order, the block properties table's grip apart. `DxfBlockReference.ResetBlock()` puts every value back to the
block's own as AutoCAD's ResetBlock does: the INSERT places the dynamic block again, its AcDbBlockRepresentation goes, its
place and ATTRIBs stay. `DxfDynamicBlockReferenceProperty` gains AutoCAD's `Show` and `VisibleInCurrentVisibilityState`,
and `StateNames`, Not flipped and Flipped of a flip; `DxfBlockTableRecord.GetVisibilityStates()` lists a block's states
with the entities each shows.

**NEW.** Dynamic blocks in the presenter, to AutoCAD 2021: a selected dynamic INSERT shows its custom grips (arrows,
squares, circles, flip arrows and list triangles in the dynamic grip colour); a grip dragged sets its value, taken to the
value set, a flip grip clicked flips, a visibility or lookup grip lists its values at the grip, the current ticked.
Properties has the Custom group (lists as drop-downs, distances and angles typed) and names an anonymous copy by its
dynamic block; RESETBLOCK and the right-click Reset Block put the values back; each change is one undo step. The Block
Editor's Visibility State box is live for a block with states. AutoCAD 2021 read every value the presenter set on the
library's dynamic samples as the library reads it, and AUDIT found no error.

**FIXED.** The presenter's -GROUP on a DXF of R2000 written without its OBJECTS section said "Object reference not set to
an instance of an object."; the drawing is now given the named object dictionary AutoCAD makes on opening it, and the
group goes in, one undo taking both back. New of the Table Style and Multileader Style dialogs, and TABLE making
Standard, failed the same way on such a drawing, and are given the dictionary now too.

**FIXED.** A dimension the presenter made took its style's DIMSCALE, not the drawing's current one: DIMSCALE 10 typed
with ISO-25 current left the next dimension's words 2.5 high. It now takes the header's `$DIMSCALE`, writing it as an
override where it is not the style's, as AutoCAD 2021 does: words 25 high, arrowheads 25 long, and DIMBASELINE and QDIM
spacing their lines DIMDLI times 10 apart.

**NEW.** The presenter's point modifier TK (TRACK, TRACKING), as AutoCAD's: "First tracking point:", then "Next point
(Press ENTER to end tracking):" until Enter, each point taken square from the one before along the UCS's axes, a distance
typed going along the axis nearest the cursor. CIRCLE with TK from 100,0 and a click at (130, 50) is centred at
(100, 50).

**NEW.** The presenter's variables POLARMODE, FILEDIA and CMDDIA, and AUTOSNAP's other bits, as AutoCAD's. POLARMODE
(kept in the settings file, `"polarMode"`) 2 runs object snap tracking on every polar angle and 8 acquires points only
with Shift held (`ObjectSnap.TracksPolarAngles`, `ObjectSnap.AcquiresWithShift`); 1 and 4 are kept, not followed.
AUTOSNAP 1, 2, 4 and 32 switch the snap marker, its name by the cursor, the magnet and the tip of ortho, polar and
tracking (`ObjectSnap.ShowsMarker`, `ShowsSnapTip`, `Magnet`, `ShowsTrackingTip`). FILEDIA 0 makes PNGOUT, JPGOUT,
BMPOUT and the window's SAVEAS ask for the file on the command line (`CadCommandSession.FileDialogs`); CMDDIA 0 makes
PLOT ask -PLOT's prompts. Global settings › Canvas and cursor has a Polar angle field (POLARANG).

**CHANGED.** Object snap tracking with POLAR on runs level and upright alone unless POLARMODE 2 is set, as AutoCAD's
default POLARMODE 0 does; before, it ran on every polar angle whenever POLAR was on.

**NEW.** `DxfOsnapPointRef.LastPointReference` and `WithLastPoint`: the reference of the point a PERpendicular or TANgent
snap was drawn from, which AutoCAD writes after a reference whose group 75 is 1. **FIXED.** Such a DIMASSOC was read
wrong: the last point's reference was taken for the next point, so a DIMLINEAR whose first point was perpendicular lost
its second point's tie; and a reference was always written with 75 = 0.

**NEW.** The presenter ties a dimension's second origin snapped PERpendicular (to a line, an arc or a circle) or TANgent
(to an arc or a circle), with the point it was drawn from, and draws it again from that point when the object changes, as
AutoCAD 2021 does: DIMALIGNED from (0, 0) perpendicular to (100, -20)-(100, 60) measures 100, and 120 once that line is
moved 20 to the right; a tangent from (300, 100) to a circle of 25 about (200, 0) goes from (185.726, 20.524) to
(184.593, 9.688) when the circle moves 10 down.

**FIXED.** INSERT with Basepoint of a block in other units than the drawing landed off: a block in centimetres in a
millimetre drawing, its base point 30 above its origin, came 270 too high. The shift is now scaled by the units too, in
the drag and in the INSERT placed.

**FIXED.** -ATTEDIT took every attribute of an INSERT a crossing box touched; it now takes those the box touches, or a
window encloses, as AutoCAD's, typed (C, W, BOX) or dragged on the canvas
(`CadCommandSession.FeedSelection(handles, window, crossing, remove)`).

**NEW.** The presenter's variables PICKADD (0 makes what is picked last the selection, Shift adding) and ATTREQ (0
inserts with the attributes' defaults, unasked). DYNMODE keeps -3 to 3, negative while DYN is off; ORTHO switched is
written to the drawing's `$ORTHOMODE`, as AutoCAD saves it.

**NEW.** Recent Commands are kept in the settings file from one session to the next (`GlobalSettings.RecentCommands`);
a recent file no longer on the disk is taken off the list when chosen (`GlobalSettings.RemoveRecentFile`); the version
and preview of a recent file are read on a worker thread.

**FIXED.** A dimension the presenter made left its picture behind when moved on its own (MOVE, ROTATE, a grip): the core
wrote it without flag 32 of group 70, which says the block is the dimension's alone and which the core's `TransformBy`
needs to move the picture. `DxfDimension.DimBlock` set to an anonymous block now marks it, as AutoCAD 2021 writes a
DIMLINEAR's 70 as 32. A copy of a dimension made by COPY, ARRAY or a grip's Copy gets a picture block of its own, as
AutoCAD's COPY copies it, so that it is drawn where it stands and not on top of its original.

**NEW.** The presenter's associative dimensions are tied by more commands and snaps, as AutoCAD 2021 writes them: DIM,
DIMCONTINUE and DIMBASELINE (the first point tied to what the origin it starts from was tied to), QDIM (line ends, polyline
vertices, circle centers), DIMORDINATE, DIMANGULAR of two lines (four ends) and of three points, and INTersection snaps (both
objects). A line and its dimension copied together leave the copy tied to the copied line. An ordinate follows its point,
its leader end moved along the axis measured alone; an angle of two lines keeps its arc's share of the angle. AutoCAD
2021 audits a drawing with every case with no error and moves each such dimension with its objects.

**NEW.** The presenter has AutoCAD 2021's Block Editor. BEDIT (BE) shows the Edit Block Definition dialog, &lt;Current
Drawing&gt; first and then the drawing's named blocks with a preview and description, a new name making a new, empty
block; -BEDIT asks `Enter block name or [?]:`. The block opens alone on a background of its own (Global settings,
Block Editor background, #3B4049), the UCS icon at its base point, the Model tab only and the green Block Editor tab;
every drawing and modify command, and ATTDEF, works on the block's own entities, each an undo step that stops where the
block was opened or last saved. BSAVE (BS) redefines the block and draws every INSERT anew, the session one undo step
"BEDIT"; BSAVEAS copies the content to a new block, the one opened left unchanged; BCLOSE (BC) asks Save, Discard or
Cancel, and Discard reads the drawing back as it was. A door of one line given a second and saved shows both in each
of its INSERTs, and one undo takes them back. With nothing selected Properties shows the Block group (description,
units, Allow exploding, Scale uniformly). BEDIT, XREF, LAYOUT, PLOT, MSPACE, QSAVE and the other commands that leave the
block say "Not allowed in the Block Editor.". `CadDocument.OpenBlockEditor`, `SaveBlock`, `SaveBlockAs` and
`CloseBlockEditor`, with `CadBlockEditor`, do the same from code.

**NEW.** The presenter's Layer manager has AutoCAD 2021's VP columns while a layout is current: VP Freeze, New VP Freeze,
VP Color, VP Linetype, VP Lineweight, VP Transparency and VP Plot Style, acting on the viewport worked through or the one
selected on the paper, an override marked blue; its right-click menu freezes and thaws a layer in all viewports, adds a
layer frozen in them all and takes overrides out. VPLAYER (?, Freeze, Thaw, Reset, Newfrz, Vpvisdflt) and LAYVPI are typed
as in AutoCAD, and MVIEW freezes the layers of New VP Freeze in the viewport it makes. A viewport is drawn without the
layers frozen in it and with its overrides: TEXT, cyan, overridden red in the plan viewport is red there alone. Each change
is one undo step, through `CadViewportLayerEditor`. In the core, `DxfLayerViewportProperties`' colour, linetype,
lineweight, transparency and the new `PlotStyleName` are set as AutoCAD 2021 writes them (ADSK_XREC_LAYER_*_OVR), with
`RemoveColorOverride()` and its like and `DxfLayerTableRecord.RemoveViewportOverrides(viewport)`; AutoCAD 2021 audits such
a drawing with no error and reads every override back.

**NEW.** The status bar's right end follows AutoCAD 2021: MODEL is a label on the Model tab, PAPER a raised button on the
paper and MODEL a pressed one through a viewport; the scale opens AutoCAD's list (Scale to fit, 1:1 to 1:100, 2:1 to
100:1, Custom..., which runs the new VPSCALE and asks the scale on the command line); a padlock locks the display of the
viewport worked through or selected, its scale then grey; on the Model tab the place shows the annotation scale
(CANNOSCALE) and its list the drawing's scales.

**NEW.** Associative dimensions, as AutoCAD 2021 keeps them with DIMASSOC 2. The library reads and writes the DIMASSOC
under ACAD_DIMASSOC in a dimension's extension dictionary: `DxfDimension.DimAssoc`, `SetDimAssoc` and `ReleaseDimAssoc`,
`DxfDimAssoc`, `DxfOsnapPointRef` and `DxfOsnapType`, and `DxfDBObject.GetPersistentReactorIds`. In the presenter a
dimension whose points were picked with an object snap (or a line selected for DIMLINEAR, a circle for DIMRADIUS, an arc
for DIMANGULAR) is tied to its objects, and every edit of them carries it along in the same undo step, whatever command or
grip makes the edit: a line from (0, 0) to (100, 0) moved by (10, 5) takes its dimension, 20 above, whole to a line at 25;
its end stretched to (120, 30) measures 120 with the dimension line still 20 up; a circle of 25 scaled twice gives its
radius 50. Erasing the object, or moving the dimension on its own, unties it, as AutoCAD's "Dimension disassociated.".
DIMREASSOCIATE, DIMDISASSOCIATE and -DIMDISASSOCIATE are new commands and DIMASSOC (0 to 2, 2 by default) a system
variable; `CadCommandSession.Feed(CadSnapResult, bool)` hands the window's snap with its point. AutoCAD 2021's AUDIT finds
0 errors in a drawing of the presenter's six kinds of tied dimension, and its own MOVE and SCALE carry them.

**NEW.** The presenter gives dimensions AutoCAD 2021's grips: a linear or aligned one its two extension line origins, the
two ends of its dimension line and its words; a radius its chord point and words, a diameter its two chord points and
words; an angle its vertex, line points, arc and words; an ordinate its feature and leader end. Dragging one rewrites the
dimension's points and draws its picture anew, one undo step. A dimension of 100 from (0, 0) to (100, 0), its line 30
below, dragged by its second origin to (150, 10) measures 150 with its line still 30 below; its words dragged to (60, -50)
stand there with the line under them, as DIMTEDIT puts them at DIMTMOVE 0. `CadGripKind` has `DefinitionPoint`,
`DimensionLine` and `Text`.

**NEW.** The presenter works in model space through a viewport of a layout, as AutoCAD's MSPACE: a double click inside the
viewport, MSPACE (`MS`) or the status bar's PAPER button enters it, its frame drawn thick and the crosshair inside it alone;
a double click off it, PSPACE (`PS`) or MODEL goes back to the paper, and Ctrl+R goes to the next viewport. Through it the
commands draw in model space, picks and snaps work as the viewport shows model space, and the wheel, the middle button,
ZOOM and PAN change the viewport's view, one undo step each; `ZOOM 1/50XP` sets the scale to 1:50, and a viewport whose
display is locked keeps its view. The status bar's scale list and Properties' Standard scale, Custom scale and Display
locked rows set a viewport's scale: 1:50 in a viewport 100 high shows 5 000 of model space. `CadDocument.EnterViewport`,
`LeaveViewport`, `ActiveViewport`, `WorkingSpace`, `SetViewportView` and `CadViewports` are the API.

**NEW.** The presenter's command line has AutoCAD 2021's AutoComplete. With a drawing open and no command running, the
list opens above the command line 300 ms after the last key: LA typed lists LAYER first with "alias LA", then the commands
that begin with LA, the most used first (each command run is counted, `GlobalSettings.CommandUses`, kept in the settings
file), then the system variables marked {x}, then, below a rule, the names with LA inside (CLAYER, PLAN, POLAR). Enter or
Space runs the row chosen, the arrows move it, Esc closes the list and clears what is typed. Ten synonyms are built in (ROUND for FILLET, BEVEL for
CHAMFER, DELETE and REMOVE for ERASE, DUPLICATE for COPY, FLIP for MIRROR, TURN for ROTATE, RESIZE for SCALE, SYMBOL for
INSERT, PRINT for PLOT), shown as `ROUND (FILLET)`. AutoCorrect: LIEN followed by LINE three times has LINE offered first
for LIEN (`GlobalSettings.NoteCorrection`, `CorrectionOf`). The command line's right-click menu has Recent Commands (the
last 20), Input Settings (AutoComplete, AutoCorrect, Search System Variables, Mid-string Search, kept in the settings
file; Search Content greyed; Delay Time…, `Enter the delay time in milliseconds <300>:`, the new INPUTSEARCHDELAY
command), Copy, Copy History, Paste, Paste To CmdLine and Options…. `MainViewModel.ShowSuggestions`, `Suggestions`,
`RunSuggestion`, `RecentCommands` and `CommandLineMenu` are the API.

**NEW.** The G button of the presenter drops the application menu: a search box for the commands; New, Open, Save,
Save As, Export (PDF through PLOT, PNG, JPG, BMP, WBLOCK), Print (Plot, Plot Preview, Page Setup) and Close (the drawing
shown, or all), the entries not built greyed; Options and Exit. Recent Documents shows the pinned files first in the order
pinned, then the others newest first, 9 in all; a file opened by the Open dialog, a drop, the app's command line or the
lists goes to the top, and the list and its pins are kept in the settings file (`GlobalSettings.RecentFiles`,
`NoteFileOpened`, `SetFilePinned`). Resting the pointer on a file shows its preview, path, version, date and size, read
from the top and the end of the file only. `MainViewModel.CreateAppMenu`, `RecentDocuments` and `PinRecent` are the API.

**NEW.** The start screen lists Recent Documents (pinned and recent, with their previews, as a list or as large pictures)
and is GeometryHelper DXF's own where no host makes drawings from a model (`MainViewModel.CanMakeFromModel`): no "New
drawing from a model", `New · Ctrl+N` instead of "Blank drawing", and "Ctrl+O opens a DXF, Ctrl+N starts a new drawing."
on the command line. Ctrl+N makes a new drawing there (`MainViewModel.NewByShortcut`); GeometryHelper Drawing keeps its
items. The screenshot mode draws the new menus with `--dialog app-menu[:export]`, `cmdline-menu`, `input-settings-menu`,
`recent-commands-menu` and `autocomplete:LA`, and fills Recent Documents with `--recent`.

**NEW.** Multileader styles are written as well as read. `DxfMLeaderStyle` has setters for its leaders, landing and
words (`LeaderLineType`, `LeaderLineColor`, `LeaderLineLinetype`, `LeaderLineWeight`, `ArrowSymbol`, `ArrowSize`,
`BreakSize`, `MaxLeaderSegmentsPoints`, `EnableLanding`, `EnableDogleg`, `DoglegLength`, `Scale`, `ContentType`,
`TextStyle`, `TextAngleType`, `TextColor`, `TextHeight`, `TextAlignAlwaysLeft`, `EnableFrameText`,
`TextAttachmentDirection`, `SetTextAttachmentType`, `LandingGap`) and new `FirstSegmentAngleConstraint` and
`SecondSegmentAngleConstraint` (`DxfAngleConstraint`), `ExtendLeaderToText` and `DefaultMText`, each written as AutoCAD
2021 holds it: 45 degrees as 0.785398 radians, no limit of points as 0, scaled to the layout as a scale of 0.
`db.MLeaderStyleDictionary.SetAt("GH_NOTES", new DxfMLeaderStyle { TextHeight = 2.5 })` adds a style with every group
AutoCAD writes, its class listed and counted in CLASSES. The presenter has MLEADERSTYLE (MLS), the Multileader Style
Manager and its Modify dialog of three tabs, Leader Format, Leader Structure and Content, after AutoCAD 2021's: Set
Current, New…, Modify… and Delete (refused for Standard, the current style and a style in use), each one undo step; a
drawing of no style is given a metric Standard, arrows 2.5, break 3.75, landing 8, text 2.5 and a landing gap of 2. A
style modified gives its multileaders its values, those they have of their own kept. MLEADER draws in the current style
(CMLEADERSTYLE), its words and arrows as high as the style's rather than TEXTSIZE, and the Annotate tab's multileader
style box ends in "Multileader style…". Block content is not offered, as MLEADER places words only, and the annotative
box is greyed. MATCHPROP's Property Settings dialog is laid out as designed: the source's value beside each basic
property, its kind and layer under the boxes, and the prompt at the foot. The registry holds 149 commands and 113
aliases. AutoCAD 2021 audits a drawing of R2013 with three styles made and modified so, and multileaders drawn in them,
with no error, and reads every value as written.

**NEW.** The presenter has SETVAR and the system variables drafters set, typed as commands as in AutoCAD 2021: `Enter
variable name or [?] <FILLETRAD>:`, then `Enter new value for FILLETRAD <10.0000>:`; a variable's name typed alone asks the
same, and `?` lists those a pattern of wildcards matches. FILLETRAD 5 writes `$FILLETRAD` 5 into the header as one undo
step; SORTENTS 200 is refused with `Requires an integer between 0 and 127.` and asked again; OSMODE 4133 ticks Endpoint,
Center, Intersection and Extension, and 16391 ticks Endpoint, Midpoint and Center with OSNAP off; EXTMIN says
`EXTMIN = 0.0000,0.0000,0.0000 (read only)`. 47 variables in the window: those of the drawing's header (CLAYER, CECOLOR,
CELTYPE, CELWEIGHT, CELTSCALE, LTSCALE, PSLTSCALE, MSLTSCALE, DIMSCALE, TEXTSIZE, TEXTSTYLE, FILLETRAD, CHAMFERA, CHAMFERB,
PDMODE, PDSIZE, MIRRTEXT, SORTENTS, IMAGEFRAME, CANNOSCALE, INSBASE, LIMMIN, LIMMAX) each one undo step; those of the
session and the window (PICKSTYLE, SELECTIONCYCLING, PICKFIRST, OFFSETDIST, TILEMODE, OSMODE, ORTHOMODE, SNAPMODE, GRIDMODE,
POLARANG, AUTOSNAP, DYNMODE, PICKBOX, APERTURE, LWDISPLAY) at once; and DIMSTYLE, EXTMIN, EXTMAX, UCSNAME, UCSORG, DWGNAME,
DWGPREFIX, DATE and CDATE read only. PICKFIRST 0 (`CadDocument.PickFirst`) makes MOVE ask `Select objects:` though a line
is selected, as AutoCAD's. PICKBOX, APERTURE, LWDISPLAY and POLARANG are kept in the settings file as DYN and OSMODE are
(`MainViewModel.SettingsChanged`), POLARANG 30 as `"polarAngle": 30` of the new `GlobalSettings.PolarAngle`, which Drafting
Settings' increment sets too. PDMODE, PDSIZE, CELTSCALE, PICKSTYLE, SELECTIONCYCLING and IMAGEFRAME are variables of the
table now rather than commands of their own, and CECOLOR, CELTYPE and LTSCALE keep their own prompts when typed alone; the
registry holds 150 commands. `CadCommandRegistry.Variables`, `AddVariable` and `FindVariable`, and `CadSystemVariable`,
are the API.

**NEW.** The presenter's grips have AutoCAD 2021's grip modes. A click on a grip makes it hot and asks `** STRETCH **`,
`Specify stretch point or [Base point/Copy/Undo/eXit]:`; Enter or Space goes on to MOVE, ROTATE (with Reference), SCALE
(with Reference) and MIRROR and back, and STRETCH, MOVE, ROTATE, SCALE and MIRROR typed, or ST, MO, RO, SC and MI, go to a
mode. The modes other than STRETCH take the objects selected, drawn as a ghost: of two lines from (0, 0), MO to (10, 20)
moves both and RO 90 then turns them about (10, 20). Copy leaves the originals and asks again (`** MOVE (multiple) **`),
Undo takes the last copy back, and a first point picked with Ctrl held turns Copy on. Shift+click makes several grips hot,
which stretch together: a circle's centre and a quadrant move it by (10, 10) keeping its radius of 10. A grip pressed and
let go elsewhere is stretched there, as before; the whole grip edit is one undo step. `CadCommandSession.StartGripEdit`,
`IsGripEditing`, `Feed(point, shift, control)` and `CadInput.Control` are the API.

**NEW.** The presenter's Properties panel edits polylines, ellipses, hatches, dimensions, MTEXTs, block references,
viewports and multileaders with AutoCAD 2021's rows, each edit one undo step. Current vertex steps through a polyline (on
the rectangle 30 by 20, vertex 3 reads (30, 20), and Vertex Y 25 moves that corner alone), with its segment widths, Global
width, Elevation, Closed and Linetype generation. An ellipse of radii 20 and 10 given a Major radius of 40 becomes 40 by
20. A hatch takes its Type, Pattern name, Angle, Scale or Spacing, Double, Origin, Island detection and Background color.
A dimension's Lines & Arrows, Text, Fit and Primary Units rows are written as overrides and the dimension drawn again:
Precision 0.000, Suppress trailing zeros No and Dim suffix " TYP" show "300.000 TYP" on a dimension of 300. An MTEXT takes
Style, Justify, Direction, line spacing, Defined width and Columns, and its Contents row opens the MTEXT editor; an INSERT
shows each attribute by its tag and sets its value; a viewport takes Standard scale 1:50, Display locked, and a Height or
Width that keeps its scale; a multileader takes its style, leader type, landing and text. `CadPropertyRow.Choices` lists
the values of a row of a list and `CadSelection.CurrentVertex` the vertex shown.

**NEW.** MATCHPROP asks "Select destination object(s) or [Settings]:", and Settings shows the Property Settings dialog of
AutoCAD 2021 (`CadCommandSession.MatchProperties`): with Layer alone checked, a red line matched to one on Steel goes to
Steel and stays red. The special properties are copied too: a dimension's style and overrides, a text's style and height,
a hatch's pattern, angle and scale, a polyline's width and linetype generation, a viewport's scale, lock and frozen
layers, a table's style and a multileader's style. Plot style, Material, Shadow display and Center object are greyed.

**NEW.** The presenter has PEDIT (PE) and REVCLOUD, with AutoCAD 2021's prompts word for word, storing what AutoCAD
stores (both checked against AutoCAD's own console). PEDIT turns a line or an arc picked into a LWPOLYLINE, and takes
Close, Open, Join, Width, Edit vertex (Next, Previous, Break, Insert, Move, Regen, Straighten, Tangent, Width, with the
vertex marked by a cross), Fit, Spline, Decurve, Ltype gen, Reverse and Undo, one undo step a run; Multiple edits several
at once and joins them within a fuzz distance by Extend, Add or Both: the lines (0, 0) to (100, 0), (103, 0) to (103, 50)
and (100, 50) to (0, 52) joined at 4 are one polyline through (103, 0) and (103, 49.94), as in AutoCAD. Fit and Spline
make the POLYLINE AutoCAD makes, and Spline of (0, 0), (100, 0), (100, 50), (0, 80) gives AutoCAD's 9 vertices of the
curve, to 8 places. REVCLOUD clouds a rectangle, a polygon, the cursor's path or an object it replaces, with Arc length,
Style (Normal, Calligraphy) and Modify: a circle of radius 20 at an arc length of 5 is AutoCAD's 25 vertices of bulge
0.52056705, and the arc lengths are kept divided by DIMSCALE, as AutoCAD keeps them. A drawing made with every option
audits clean in AutoCAD 2021. `CadPrompt.Tracks` says a prompt follows the cursor, as Freehand does.

**FIXED.** `DxfPolyline2d.SplineFit` puts the vertices of the curve after the first vertex of the frame, where AutoCAD 2021
keeps them, rather than after the last; `CurveFit` reads the width at the joint of a tapered segment off the two arcs'
lengths, as AutoCAD does: a segment from width 1 to 3 whose arcs are 25.723 and 24.733 long is 2.0197 wide at its joint,
not 2.

**NEW.** The presenter takes AutoCAD's point modifiers at any prompt for a point, typed or from the top of the
Shift+right-click menu (Temporary track point, From, Mid Between 2 Points, Point Filters with .X .Y .Z .XY .XZ .YZ), with
AutoCAD's prompts. LINE with FROM, the base point 100,50 and the offset `@30,20` starts at (130, 70); M2P (or MTP) between
(0, 0) and (100, 40) gives (50, 20); `.X` of (30, 999) with (888, 40) at `(need YZ):` gives (30, 40), and `40,3` typed
there gives Y 40; TT at the midpoint (2 000, 0) of a wall holds the cursor at (2 000.3, 700) on (2 000, 700) while OTRACK
is on. Coordinates are the UCS's, Z is asked and left out, each point a modifier asks may be snapped, and modifiers nest,
as FROM with M2P for its base point. `CadCommandSession.StartPointModifier`, `TemporaryTrackPoints` and
`ObjectSnap.AcquireTrackPoint` are the API; `CadMenuItemViewModel.Items` holds a submenu.

**NEW.** The presenter's ARC, CIRCLE, PLINE, TEXT and MTEXT take every option AutoCAD 2021 offers, with its prompts word
for word. ARC draws by Start, Center, End, Angle or chord Length, the Center first, by Start, End and Angle, Direction or
Radius, and goes on tangent from the last line or arc at Enter: after a line from (0, 0) to (10, 0), Enter and (20, 10)
give the quarter arc of radius 10 about (10, 10). A point picked with Ctrl held draws the arc clockwise. CIRCLE takes Ttr,
a circle of radius 15 touching two lines that meet square at (0, 0) centred on (15, 15), and Tan, Tan, Tan as 3P with
`_tan` before each pick, the circle touching three sides of a square 40 across of radius 20. PLINE takes Width, Halfwidth
and Length, the widths kept per vertex and PLINEWID set to the ending width, and in arc mode Angle, CEnter, Direction,
Radius and Second pt. TEXT takes Justify, its fifteen justifications, and Style, and Enter at its first prompt goes on
five thirds of the height below the last text. MTEXT takes Height, Justify, Line spacing, Rotation, Style, Width and
Columns at its corner, static and dynamic columns stored as AutoCAD stores them.

**FIXED.** GeometryHelper.Dxf.Viewer: MIRROR now follows the drawing's MIRRTEXT, as AutoCAD 2021 does. At 0, AutoCAD's
default, a TEXT, an MTEXT and the attributes of an INSERT came out written backwards; they now stay readable, the box of
their words mirrored and their reading kept within the half of the circle it was in, as probed in AutoCAD 2021's console
over 96 turns and mirror lines. "ABC" 2.5 high turned 30 degrees at the origin, mirrored across the line y = 10, reads at
330 degrees from (-1.25, 17.835), where AutoCAD puts it; turned 120 degrees and mirrored across an upright it reads at 240.
A text aligned between (0, 0) and (20, 0) mirrored across x = 30 runs from (40, 0) to (60, 0), and an MTEXT attached top
left at the origin, mirrored across x = 40, is attached top right at (80, 0). At 1 they are mirrored with the rest, as before.

**NEW.** GeometryHelper.Dxf.Viewer: "Select objects:" takes the options of AutoCAD 2021: Window, Crossing and BOX by two
corners, Fence, WPolygon and CPolygon by points until Enter, Last, Previous, Group by name, Remove and Add, Undo, Multiple,
SIngle and AUto, beside ALL; Shift held takes out what a click, a box or a lasso picks, "1 found, 1 removed, 2 total";
anything else is "*Invalid selection*" with the options listed. Window, Crossing, BOX, Fence and the polygons draw their
band on the canvas (`CadPrompt.Band`), and `PickIndex.Fence`, `WindowPolygon` and `CrossingPolygon` answer them: over
100 000 lines a fence took 28 milliseconds and a polygon round them all 63 to 69. `FeedSelection` takes `remove` to take
objects out.

**NEW.** GeometryHelper DXF, `GeometryHelper.Dxf.App.exe`, the presenter's own app: the window of the 2D presenter with no
model drawing, built only on `GeometryHelper.Dxf.Viewer` and its controls, to open, edit and plot DXF drawings in place of
AutoCAD. `GeometryHelper.Dxf.App.exe plan.dxf` opens a drawing, as a file association does; its command plugins are kept in
`%AppData%\GeometryHelper\Dxf`, apart from GeometryHelper Drawing's. Both apps now start through `CadAppHost` and show
`CadAppWindow`, both in `GeometryHelper.Dxf.Viewer.Wpf`, and take the same `--screenshot` options; GeometryHelper Drawing
adds its own (`--new-drawing`, `--pick`, `--hand-edit` and the dialogs of the model drawing) through the host's protected
members. Its pictures are the same as before, pixel for pixel but for the clock of a log line, on 34 combinations of
options. `CadWorkspace.AppName` puts
the app's name in the title bar and on the start screen, "GeometryHelper Drawing" unless a host gives its own.

**NEW.** The presenter has PNGOUT, JPGOUT and BMPOUT, as AutoCAD 2021's: the file from the Create Raster File dialog (on
the command line with no window, `Enter file name <C:\Plans\plan.png>:`), then `Select objects or <all objects and
viewports>:`. Enter writes the drawing area as it is shown, at its size in pixels, on its background, with its grid and
without the cursor, grips, UCS icon or selection highlight; objects selected are written alone in the same view. A PNG is
8-bit RGB, a JPEG of quality 90, a bitmap of 24 bits; the same view gives the same bytes. Nothing is printed and nothing
is undone. A 1 920 by 1 080 PNG of a drawing of 100 000 entities is 1.2 MB, written in 0.25 s (.NET Framework 4.8; 0.20
s on .NET 10). `CadImageJob` is what the command hands the window, and `CadRasterWriter` writes one with no window.

**NEW.** The presenter's ribbon has an Insert tab, after a host's tabs and before Annotate, as AutoCAD 2021's for what is
built: Block (Insert, whose arrow drops a gallery of the drawing's blocks, a click on a tile starting INSERT with that
block; Edit Attribute), Block Definition (Create Block and Write Block, Define Attributes, Set Base Point) and Reference
(Attach DXF and Attach Image, Clip, External References, a box that shows and sets IMAGEFRAME, Transparency). What is not
built is greyed with its reason. Annotate and View now shrink as Home does: in a window 1 280 wide every panel of Insert
(852 pixels), Annotate (992) and View (670) is full; Annotate's panels go medium below 1 016, their style boxes 84 pixels
wide instead of 118, and View's below 694. View is now a `RibbonStrip` of the panels Navigate, Views, Interface and
Drafting, each button bound as before. A collapsed panel's name of two words, as Block Definition, goes on two lines.

**NEW.** The presenter's block commands come closer to AutoCAD 2021's. The Enhanced Attribute Editor shows a value on the
canvas as it is typed; Apply keeps it with the editor open, and Cancel takes back only what was typed after the last
Apply, the whole edit one undo step (`CadAttributeEditRequest.Preview` and `Apply`). INSERT drags the block with the
cursor while it asks the insertion point, the scale and the rotation (`CadPrompt.Drag`, a `CadBlockDrag`): a block drawn
about (100, 70) dragged to (500, 500) has its circle about (100, 100) drawn about (500, 530), and the INSERT is then placed
exactly as dragged. -ATTEDIT takes the one attribute clicked, not every attribute of its INSERT, and marks the attribute it
is editing with a cross (`CadPrompt.Mark`). ATTSYNC (?, Name, Select) brings every INSERT of a block, those nested in other
blocks too, in line with its attribute definitions in one undo step: an attribute for each definition in the block's
order, the values of the tags kept, those of tags dropped removed, each placed from its definition. Block Definition
draws the objects selected for a new block. Inserting a block of another drawing whose name the drawing has asks "Block
GB already exists. Redefine?" in the window, `Block "GB" already exists. Redefine it? [Yes/No] <No>:` on the command line
of -INSERT; Yes gives the drawing's block the other's entities, base point and unit. AutoCAD 2021 audited drawings left by
ATTSYNC, -ATTEDIT and a redefinition with no error.

**NEW.** The presenter makes ordinate and jogged dimensions and has AutoCAD's DIM command. DIMORDINATE (DOR) measures a
point from the origin of the UCS along X or Y as its leader runs (Xdatum, Ydatum, Mtext, Text, Angle): (300, 40) with its
leader ending at (310, 80) is an X datum of 300, its leader jogged two arrowheads before its end, as AutoCAD 2021 draws
it. DIMCONTINUE, DIMBASELINE and QDIM's Ordinate go on from an ordinate dimension. DIMJOGLINE (DJL) puts a jog in a linear
dimension's line and Remove takes it out; the jog is kept in the dimension's extended data as AutoCAD keeps it
(ACAD_DSTYLE_DIMJAG_POSITION), and AutoCAD 2021 draws it where the presenter put it. DIM dimensions what is clicked, a
line (or two, for their angle), a circle's diameter, an arc's radius, or two points, and its options run DIMANGULAR,
DIMBASELINE, DIMCONTINUE and DIMORDINATE, align and distribute dimensions, and set the layer new ones go on. AutoCAD 2021
audited drawings of R2000, R2013 and R2018 with 51 dimensions of every kind made so with no error.

**CHANGED.** A linear dimension's picture is laid out from its style as AutoCAD 2021 lays it out, where its words always
stood above the line before: DIMTAD 0 puts them on the line, broken about them by DIMGAP, 2 on the side away from the
points measured and 4 below; DIMJUST puts them next to or over an extension line; DIMTIX, DIMSOXD, DIMATFIT and DIMTOFL
move the words or the arrowheads outside where both do not fit, and DIMTIH and DIMTOH keep the words level. Against the
pictures AutoCAD 2021 draws itself for 86 linear and ordinate dimensions, they differ only by the size of the words,
which the presenter takes from their height where it finds no font (up to 0.5), and by AutoCAD starting the line at the
back of a closed arrowhead. A drawing made new in code, whose Standard style has AutoCAD's DIMTAD of 0, now has its
dimension lines broken about the words.

**NEW.** The presenter attaches and shows external references and images, as AutoCAD 2021 does: XATTACH and IMAGEATTACH
(the Attach External Reference and Attach Image dialogs), the External References palette (XREF, ER, IMAGE), -XREF,
-IMAGE, IMAGECLIP, IMAGEFRAME and TRANSPARENCY. A DXF referenced is read and drawn under its INSERT, its layers named after
it (`grid|GRID`), its attachments followed and its overlays not, and never written into the drawing; one changed on disk
stands Needs reloading until Reload. A PNG, JPEG or BMP is placed at AutoCAD's size, 40 by 30 pixels at 3 779 pixels a
metre 10.584811 by 7.938608 mm, and drawn by its pixels, decoded off the window's thread. A drawing of 100 000 entities
shown as a reference is read and drawn in 0.90 s, where opening it alone takes 0.87 s. `DxfBlockTableRecord.PathName`
is now set as well as read.

**NEW.** Tables made, styled and edited as in AutoCAD 2021. The core library reads and writes table styles
(`DxfTableStyle`, `db.TableStyleDictionary.SetAt(name, new DxfTableStyle())`, a table's `TableStyle` now set as well),
makes tables in code (`new DxfTable(position, rows, columns, rowHeight, columnWidth)`, appended in a new `*T1` block, its
title merged across) and draws a table anew in its block from its cells (`DxfTable.RecomputeTableBlock`): fills, grid
lines by their looks, the text of each cell at its alignment. In the presenter, TABLE (TB) shows the Insert Table
dialog and opens the title for typing, Tab, Shift+Tab and Enter moving on: five columns 63.5 wide and four rows of data
of AutoCAD's Standard make a table 317.5 by 56. -TABLE asks on the command line, TABLEDIT, a double click or F2 types in a
cell, and TABLESTYLE (TS) shows the Table Style dialog with New and Modify (General, Text, Borders). A click in a cell
selects it with the Table Cell tab: rows and columns put in and taken out, merge and unmerge, alignment, fill and
borders, each one undo step; a cell of a table of 100 rows of 20 columns is written and drawn in about 50 ms. AutoCAD
2021 audited drawings of R2000, R2013 and R2018 made so with no error and lists their tables as tables. A table turned
is now drawn where it stands: its block was turned twice.

**NEW.** `GeometryHelper.Dxf.Viewer`, `GeometryHelper.Dxf.Viewer.Wpf` and the `GeometryHelper.Drawing.App` window are a 2D
presenter for any DXF, built as AutoCAD behaves (not packed yet). A file opens as a `CadDocument` drawn from display
lists through SkiaSharp: 100 000 entities open in 0.9 s, a click picks in 0.007 ms and a window of 1 000 entities draws
in under 1 ms. It edits as AutoCAD does, with a command line that has AutoCAD's prompts, names and aliases, object snaps,
grips, Properties edited in place, a layer manager, and undo and redo that give the start file back byte for byte.
Command plugins load and unload at run time (collectible load contexts on .NET 8), and global settings are kept as JSON:
colours, crosshair size, text height and autosave copies that never touch the file. CLOSE and the x on a row of
the file list close a drawing, asking first about unsaved changes. An owner (`CadOwnerRegistry`, tag `GH_OWNER`) keeps
control of the entities it wrote: an edit of one goes to its owner in the same undo step, or is refused in the words the
command line shows.

**CHANGED.** The presenter draws again only what an edit changed. After an edit, an undo or a redo the display list of a
space is patched from the one before: the entities the edit names, those that read a layer switched off, frozen or
recoloured, and those that share a dimension's picture or the words of a leader are drawn anew, and every other entity
keeps its items, the list item for item the one a REGEN makes. On a drawing of a million entities (3.9 million items)
under .NET 8, a line moved is drawn again in 0.46 s instead of 9.6 s and its edit kept for undo in 23 ms instead of
567 ms (the drawing is now compressed for undo on a worker thread), an undo of it is drawn in 0.45 s, and a layer of a
third of the entities switched off in 0.53 s; under .NET Framework 4.8 the line takes 0.59 s instead of 10.6 s and the
layer 0.69 s instead of 8.3 s. The pick index is made in half the time (0.26 s instead of 0.51 s), a block's entities are
listed once a regeneration rather than once an INSERT, and a style of no dashes is shared, so the first display of the
million is 8 to 17 per cent faster and holds 230 MB less. Moving 100 000 of them at once under .NET Framework 4.8 is
drawn again in 1.4 s instead of 9.9 s, though the edit itself takes 6.4 s instead of 4.3 s, the old list being kept until
it is patched. Under .NET 8 with a heap of 4 GB the old presenter ran out at the second edit of a single line; the new one
goes on through that move (4.3 s, drawn in 1.1 s), its undo and a layer switched off and on.

**CHANGED.** The presenter's `PickIndex` hands an XLINE or a RAY to a pick, a snap or a selection only where it passes
through the box asked about or near it, where before every one was handed to every pick and snap. On a drawing of
100 000 lines, arcs and circles with 200 xlines and rays among them, 20 000 snaps take 40 ms instead of 119 to 136 ms and
20 000 picks 20 ms instead of 73 to 98 ms under .NET 8, and 60 ms instead of 220 to 243 ms and 27 ms instead of 108 to
119 ms under .NET Framework 4.8; with no xline they take 22 to 28 ms and 14 to 19 ms. What is picked, snapped and selected
is the same, bit for bit. `ItemsNear` now leaves out an xline or ray that passes the box by further than the box's own
size, unless it is drawn for a block reference, whose insertion point a snap takes from any of its items.

**NEW.** The presenter selects and tidies as AutoCAD 2021 does. QSELECT shows Quick Select over the rows of the
Properties panel (`CadQuickSelect`): Line, Layer, `= Equals`, WALLS selects the lines of WALLS and says `312 item(s)
selected.`, in 0.25 s over 100 000 objects. FIND shows Find and Replace (`CadFind`) over TEXT, MTEXT, attribute values,
dimension and multileader text and table cells, with match case, whole words, wildcards, blocks and hidden items; it
replaces in an MTEXT its words alone, `{\fArial|b1;Bolts M20}` becoming `{\fArial|b1;Bolts M24}`, and Replace All is one
undo step. DRAWORDER (DR) and TEXTTOFRONT write AutoCAD's sort table, which the display follows, through the new
`DxfDrawOrderTable` of the core library (`DxfBlockTableRecord.DrawOrderTable`, `CreateDrawOrderTable`). GROUP (G), -GROUP
and UNGROUP make and change the GROUP objects of ACAD_GROUP, and with PICKSTYLE 1 (Ctrl+Shift+A switches it) a pick of
one member takes its group. -PURGE (PU) deletes the unused blocks, dimension, text, multileader and table styles,
layers, linetypes and plot styles, the lines of no length and the empty texts, asking of each name where told to, and
never what AutoCAD keeps: layer 0, Defpoints, Standard, ByLayer, ByBlock, Continuous or the current ones. Where two or
more objects lie under a click, the list of them is shown near the cursor (SELECTIONCYCLING 2, as AutoCAD's default), and
Ctrl+W switches it off and on as AutoCAD's does; Ctrl+W no longer closes the drawing, which CLOSE and the x on its row do.

**NEW.** The presenter snaps to XLINE and RAY as AutoCAD 2021 does, which it did not at all: Nearest, Perpendicular and
Intersection along their whole length, with lines, arcs, circles and each other, and none behind the base point of a ray;
the base point of a RAY is its endpoint and the root point of an XLINE its midpoint; Extension runs back from a ray's base
point, and Parallel and Apparent Intersection take them as lines. An xline along (1, 1) through the origin and the line
from (100, 0) to (100, 500) give the intersection (100, 100). The ghost MOVE, COPY, ROTATE, SCALE and MIRROR drag draws
them too, across the view as the canvas does. Over 100 000 objects and 200 xlines and rays, a snap takes 0.012 ms.

**CHANGED.** A pick in the presenter takes the object drawn on top among those about as near the cursor, as AutoCAD's does
while SORTENTS holds its bit 1 (its default 127 does): two lines crossing under the cursor give the one drawn later, or
the one DRAWORDER put in front, where the handle decided before. Objects within a tenth of the pickbox of the nearest
count as as near: with a pickbox of 3, a line under the cursor still comes before one on top of it 0.6 away. The Selection
list of SELECTIONCYCLING and Shift+Space cycling run in the same order, the one on top first.

**NEW.** The presenter's ribbon is laid out and shrinks as AutoCAD 2021's. Home has Draw, Modify, Annotation, Layers,
Properties, Groups, Utilities, Clipboard and View; as the window narrows, the steps of each tab's `RibbonStrip` are taken
in turn until its panels fit, and given back as it widens: at 1 600 pixels View, Clipboard, Utilities and Groups are each
one button that drops the full panel below it, with a pin, and Properties, Layers and Draw drop the labels of their small
buttons; Modify never shrinks. Modify gains the Draw Order split (Bring to Front, Send to Back, Bring Above Objects, Send
Under Objects, Bring Text to Front), which gives DRAWORDER its option once the objects are picked
(`MainViewModel.StartCommandWithOptionCommand`); Groups has Group, Ungroup and Group Selection, pressed while PICKSTYLE is
1 (`MainViewModel.IsGroupSelectionOn`); Clipboard the Paste split, Cut, Copy and Copy with Base Point; Utilities Quick
Select. Annotate's Dimensions panel has the Linear and Continue splits, Quick, Adjust Space, Break and a slide-out with
Oblique and Text Angle. The Text Editor tab fits 1 600 pixels, Spell Check and Tools collapsed and Close the last panel.

**NEW.** `GeometryHelper.Drawing` makes a model drawing, in the manner of Tekla's, from a `DrawingSource` of
`DrawingObject` bodies: views along the axes and an isometric view with exact hidden-line projection (2 000 objects in
the front view take 0.3 s), sections with ANSI31 hatches, details, dimensions by rule, marks that keep clear of the
symbols and dimensions, centre lines, weld symbols to ISO 2553, section, detail and level marks. A drawing is one DXF that
also carries its own document, so it opens again with its settings. `Drawing.Update(source, settings)` makes it again with
other settings or a changed model and keeps what was moved or drawn by hand; `Prepare` makes the update on a thread of
its own and `Apply` writes it as one undo step. `ReDimension`, `ReMark`, `AddView`, `AddSection` (a cut along a line drawn
in a view) and `AddDetail` change the settings for one view and are kept through later updates; `ReCentreLines` makes the
drawing again with the centre lines of one view drawn anew. A general arrangement
of 1 000 members is drawn in 1.2 s. The dialogs, the Drawing tab and the commands VIEWSECTION and VIEWDETAIL are in
`GeometryHelper.Drawing.Wpf`.

**NEW.** Weld symbols are placed clear of the view's dimensions, centre lines, section, detail and level marks and of one
another, as marks are: `WeldSymbol.Find` given the dimensions and what else to keep clear of tries the knee up and right of
the weld, up and left, down and right and down and left, nearest first, and the arrow always ends at the weld's middle. A
symbol left of its weld runs its reference line to the left. The fillet a6 of TC5 seen from the front at 1:10, which stood over
a dimension line and a dimension's text, now stands under the base plate; the 36 symbols of TC5 in its front, top and right
views at 1:2 to 1:20 have no overlap. `WeldSymbol.FindOverlaps` lists what overlaps, and the marks of a view are placed after
the symbols and keep clear of them.

**NEW.** A model drawing made in the app is saved at once, once the global settings are applied, into their save folder
(the folder `GeometryHelper Drawing` in the user's Documents where it is empty), named by its number: `B-101.dxf`, or
`B-101 (2).dxf` where that name is taken, so that no file is written over. It then has a file, which Save writes to, and
autosave keeps copies beside it.

**NEW.** TEXT and MTEXT write the width factor of the global settings (0.8 by default), and the dimension commands DIMLINEAR,
DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write the text of a dimension at the text height of the global settings,
kept as a DIMTXT override of the dimension, so that a CAD that draws it again from its style draws its text as high.

**CHANGED.** The presenter's controls draw through SkiaSharp 4.155.0, where they used 3.119.1.

**FIXED.** A part or bolt mark no longer stands on the arrows of a short dimension. A dimension line shorter than two
arrows has its arrows drawn outside it, and marks now keep clear of the line run on by one arrow at each end, as weld
symbols already did: the line of a 25 dimension at 1:20 with text 2.5 high is kept clear of 50 beyond each end. Of 114
marks on five test bodies, 18 moved, all in views with short dimensions.

**FIXED.** The 2D presenter draws an MTEXT at the width factor its words open with (`\W0.8000;` for 0.8, as the T
command and AutoCAD write it) and measures it at that width, as it does a TEXT.

**FIXED.** A model drawing saved and opened again from its DXF, by Open or from the file list, is a model drawing again,
not a plain read-only DXF: its owner is registered, so a mark can be moved and a projected line cannot, it is listed under
"Opened from a file · model drawings" with the Drawing tab, and saving writes to the file it came from. Update from model
asks for the source, since the file does not keep it, and says in words when none is at hand or when the source holds none
of the drawing's objects. **NEW.** `Drawing.Open(CadDocument, DrawingSource, Tolerance)` and
`CadWorkspaceExtension.DocumentOpened`.

**FIXED.** The Text override of a dimension can be set in Properties, as in AutoCAD: `<> TYP` shows `3000 TYP` on a
dimension of 3 000, and an empty text shows the measurement. On a model drawing it is a hand edit and one undo step.

**FIXED.** PLUGINRELOAD no longer asks again whether to load a plugin from a folder outside the trusted folders: a yes is
kept for that folder for the session, never on disk; a no is asked again.

**FIXED.** Marks and weld symbols keep clear of the full width of every dimension arrow, inside a dimension line as well
as outside a short one. Each arrow is taken as the box round it, as long as the dimension's text is high and a third of
that wide. `Marker.FindOverlaps` and `WeldSymbol.FindOverlaps` name "over an arrow of dimension D-3". With the presets' text
sizes one mark and one weld symbol of the test bodies moved; with a dimension text four times the marks', 19 of 120 marks and
6 of 72 weld symbols moved off arrows.

**CHANGED.** A mark that the placing leaves out is tried once more, keeping only its text, not its margin, clear of the
centre lines and cut lines, as `FindOverlaps` counts them. In the general arrangement of 1 000 members 191 of 200 marks now
find a place instead of 185, and every other mark is the same to the bit; two of the six marks rescued have leaders of 10 and
15 m (51 and 77 mm on the paper).

**FIXED.** The presenter draws an MTEXT at its text style's width factor when its words set none of their own, as it
already took the style's slant. ROMANS 2.5 high in a style of width 0.75 is drawn 6.75 wide, and `\W1.2x;` makes it 0.9.

**FIXED.** VIEWSECTION and VIEWDETAIL on a model drawing opened again from its file said "No view of the drawing is
there."; they now make it again from the source its file names first, or say to pick the source in Update from model.

**FIXED.** A model drawing just made and saved into the save folder was listed twice: as the drawing, and as its file once
another file of the folder was opened, or once it was saved with Save and shown again. It is now one row.

**FIXED.** Properties of a model drawing opened again said "Opened from a file" for its source though its file names it; it
now says, for example, "Test bodies (from file)" until the drawing is made again. The out-of-date dot now shows on such a
drawing too, checked in the source its file names without changing it.

**NEW.** The presenter's clipboard as AutoCAD's: COPYCLIP (Ctrl+C), CUTCLIP (Ctrl+X), COPYBASE (Ctrl+Shift+C), PASTECLIP
(Ctrl+V), PASTEBLOCK (Ctrl+Shift+V), PASTEORIG and PASTESPEC, between drawings and between copies of the app, text from other
programs pasted as MTEXT; a line on layer Steel pasted into a drawing whose Steel is blue is drawn blue. TEXTEDIT (ED, or a
double click) edits a TEXT in a box over it, each text one undo step; MTEXT and MTEDIT open the MTEXT editor with its Text
Editor tab, writing AutoCAD's codes: "AB CD" with CD made bold in Arial writes `AB {\fArial|b1|i0|c0|p34;CD}`. An MTEXT of
several looks is drawn run by run, with bold, italic, colours, heights, underline, overline and strikethrough. Word wrap at
the MTEXT width, stacks, columns, fields, bullets and spell check are not built yet. AutoCAD 2021's AUDIT finds nothing in a
drawing changed by all of them.

**NEW.** The presenter makes and places blocks as AutoCAD 2021 does: BLOCK (B) with the Block Definition dialog and -BLOCK,
INSERT (I) with the Blocks palette (Current drawing, Recent, Other drawing) and -INSERT with its attribute prompts, WBLOCK (W)
writing a new metric DXF and -WBLOCK, ATTDEF (ATT) and -ATTDEF, the Enhanced Attribute Editor (EATTEDIT, ATTEDIT, or a double
click on a block with attributes) and -ATTEDIT, and BASE; each run is one undo step. A grid bubble of a circle 10 across, a
line and the attribute GRID, made a block with its base point at the foot of the line and inserted at (500, 500) at scale 2
and turned 90 degrees, asks "Grid letter <A>:"; exploding an INSERT scaled 2 by 1 turns its circle into an ellipse with a
major radius of 20. EXPLODE refuses a block that does not allow exploding. BATTMAN, ATTSYNC and the ATTDIA dialog are not
built yet. AutoCAD 2021's AUDIT finds nothing in a drawing made by these commands or in the files WBLOCK writes.

**NEW.** The presenter's last object snaps, as AutoCAD 2021's: Extension, Parallel, Apparent Intersection, Insertion and
object snap tracking (OTRACK, F11, on the status bar before OSNAP and in Drafting Settings). A rest of half a second on a
point acquires it, seven at most: the end of a line from (0, 0) to (100, 0) rested on gives (150, 0) along its run to a
cursor at (150, 0.4), and with tracking, (100, 30) above it and the crossing (100, 50) with the level line of a second end
at (200, 50), the paths drawn dotted. Parallel holds the rubber band along a line rested on; Apparent Intersection gives
(100, 0) for two lines that stop short of it, or picks one object with a click and takes where the next would meet it;
Insertion takes an INSERT's point from any of its lines, and the point of a TEXT, an MTEXT or an attribute. EXT, PAR, APP,
APPINT and INS typed take them for one point. **CHANGED.** `CadSnapModes` carries AutoCAD's OSMODE values (Node 8, Tangent
256, Nearest 512, Geometric Center 1024); the settings file keeps snaps by name and reads as before. A snap over 100 000
entities with every mode on and seven points acquired takes 6 microseconds; with the modes of before, 1 microsecond, as
before.

**NEW.** The presenter plots to PDF as AutoCAD 2021 does: PLOT (Ctrl+P) shows the Plot dialog, expanded, PAGESETUP the same
dialog as Page Setup, PREVIEW the plot preview over the whole window (pan, zoom in real time, zoom window, zoom original, a
shortcut menu, ESC or ENTER to leave), and -PLOT asks AutoCAD's prompts on the command line. Its plotter, GeometryHelper
PDF, writes one page of the sheet's exact size through SkiaSharp, in vectors: ISO A3 on its side is 1 190.55 by 841.89
points, lines keep their lineweights, solids and hatches are filled, and the text of a TrueType font stays text a reader
finds. The window from (120, 60) to (7 620, 5 360) at 1:20, centred on A3, is 375 by 265, moved 17.5 right and 11 up of the
printable area's corner. ISO A0 to A4 and ANSI A to E, Display, Extents, Limits, Window and Layout, Fit to paper or a scale
of the list (1:1 to 1:1000 and the drawing's own), the offset or centring, landscape, portrait and upside down, and the
tables Monochrome, Grayscale and None made in code. Apply to Layout and Save changes to layout write the page setup into
the LAYOUT's plot settings as AutoCAD does, one undo step, and it reads back after saving; AutoCAD 2021's AUDIT finds
nothing in a drawing saved after it. The printers of Windows are listed but greyed: printing to them is not built yet.
`DxfPlotSettings` sets, as AutoCAD's `PlotSettingsValidator` does, the plotter's name, the plot style table, the plot type
and window, the custom and standard scales, and the flags of centring, plot styles and lineweights.

**NEW.** The presenter edits dimensions as AutoCAD 2021 does: DIMCONTINUE (DCO) and DIMBASELINE (DBA) go on from the last
linear or angular dimension, or one selected, on the same line or DIMDLI further out, with Undo inside; QDIM dimensions the
ends of lines and polylines and the centres of circles in a row, staggered, from a base or as radii and diameters; DIMEDIT
(DED) gives dimensions a new text, turns their words, puts them home or slants their extension lines; DIMTEDIT moves the
words, the dimension line going with them where DIMTMOVE is 0; DIMSPACE spaces parallel dimensions a value apart or by twice
their text height; DIMBREAK gaps the lines of a dimension where lines, polylines, circles and arcs cross them. A row of 100
and 150 continued from a dimension of 100 between (0, 0) and (100, 0) stands on its line 30 below. Each run is one undo
step, and an owned dimension of a model drawing is refused in words. A dimension drawn again keeps its moved and turned
words and its oblique angle. A break of DIMBREAK is drawn into the dimension's picture only, not stored as AutoCAD stores
it, so a dimension drawn again is whole; DIMJOGLINE and ordinate dimensions are not made yet. AutoCAD 2021's AUDIT finds
nothing in a drawing edited by all of them.

**FIXED.** The presenter draws XLINE and RAY, which it drew not at all: an XLINE across the whole view both ways from its
base point and a RAY on from it, as AutoCAD draws them, picked and highlighted anywhere along them like a line (a new
`DisplayRay2`). ZOOM Extents takes their base points only, as AutoCAD's does: a line 100 by 50 from the origin and an XLINE
through (200, 300) zoom to the box from (0, 0) to (200, 300). A crossing box or lasso they run through takes them; a window
never does.

**NEW.** Home › Modify of the presenter's ribbon is laid out as AutoCAD 2021's panel, with buttons for the commands it had
only on the command line: Stretch, Trim with Extend on its arrow, Fillet with Chamfer, Array (rectangular) with Polar Array;
Erase, Explode and Offset by their icons; a row of Break, Join, Lengthen and Align under them; and its title drops Break at
Point, Break, Join, Lengthen and Align, as AutoCAD's panel slides out. A split button shows the command last picked from its
arrow: Extend picked once from Trim's leaves it reading Extend and running EXTEND until Trim is picked again.

**NEW.** The presenter's TRIM (TR), EXTEND (EX), BREAK (BR), BREAKATPOINT, JOIN (J) and LENGTHEN (LEN) as AutoCAD 2021's:
TRIM and EXTEND in the Quick and Standard modes with Fence, Crossing, Edge, eRase and Undo, Shift-pick switching between the
two; each command is one undo step (a line from (0, 0) to (100, 0) crossed by a line at x = 40 and picked at (80, 0) is
trimmed to the line from (0, 0) to (40, 0)). AutoCAD 2021's AUDIT finds nothing in a drawing edited by all of them.

**NEW.** The presenter's modify commands as AutoCAD 2021's: OFFSET (O), FILLET (F), CHAMFER (CHA), STRETCH (S), ARRAY (AR),
ARRAYRECT, ARRAYPOLAR, -ARRAY (-AR) and ALIGN (AL), each one undo step, with their options and their Undo inside (a line from
(0, 0) to (100, 0) offset by 10 to the side of (50, -3) gives the line from (0, -10) to (100, -10)); entities of a model
drawing are refused in words, and copies of them are the presenter's own. Path and associative arrays are not built.
AutoCAD 2021's AUDIT finds nothing in a drawing edited by all of them.

**NEW.** PTYPE (DDPTYPE) shows AutoCAD's Point Style dialog: twenty tiles of PDMODE, each drawn as the canvas draws a point of
that mode, the point size, and whether it is a share of the screen (PDSIZE below nought) or a size in drawing units; choosing
the plus with a circle at 2.5 units writes PDMODE 34 and PDSIZE 2.5 as one undo step.

**NEW.** The presenter's NEW starts from its own metric template, `CadTemplate.CreateMetric()`: the settings of acadiso.dwt
written into the header, so that AutoCAD opens the drawing as metric too, with ISO-25 current (arrowheads and text 2.5, a
decimal comma) and the text styles Standard and Annotative in Arial, as AutoCAD 2021's has them.

**NEW.** The presenter's 20 arrowheads, Closed and Origin indicator 2 among them, make AutoCAD 2021's blocks entity for entity
(`_Dot` a polyline 0.5 wide of two half circles) the first time a dimension style or a multileader uses them; AutoCAD's AUDIT
finds no error in a drawing using all 19. **CHANGED.** Arrowhead blocks are named as AutoCAD names them (`_ArchTick`, not
`_ARCHTICK`).

**NEW.** PTYPE (DDPTYPE), PDMODE and PDSIZE, and points drawn as PDMODE says, in drawing units or as a share of the view;
LWPOLYLINEs with a width are drawn filled, as AutoCAD fills them.

**NEW.** The Hatch dialog has a Pattern file row: Browse… adds a .pat file's patterns after the 48 built-in ones (a built-in
name wins). Load or Reload Linetypes shows the built-in GeometryHelper.lin, then the linetypes of a .lin file chosen by Browse….
A drawing opened whose text styles name SHX files not found shows Missing SHX files: the files, the styles that use them and
what they are drawn in, a replacement to pick for each or FONTALT kept (Global settings' `ShowMissingShxDialog`, on by default).
**FIXED.** `-STYLE` and the Text Style dialog take the built-in font `ghsimplex` as an SHX font, not as a TrueType family.

**CHANGED.** Close is orange-red in the WPF apps, as designed: the footer Close of the Layer manager, Plugin manager, plugin
settings, Text style, Dimension style manager and Compare dimension styles is filled #C2410C, the title-bar cross of every
dialog and of the main window is drawn #F26B3A and fills orange-red under the mouse, and the close cross of an open drawing in
the DRAWINGS list is #F26B3A, #FF9A6E on the current row so it keeps 3 to 1 contrast on #1F4E79.

**NEW.** The presenter carries one font of its own, GeometryHelper Simplex (`CadFonts.BuiltIn`, named `ghsimplex`): the Roman
Simplex letters of the Hershey fonts (U.S. National Bureau of Standards, 1967; notice in the project's THIRD-PARTY-NOTICES.md),
98 letters with the degree, plus-minus and diameter signs, drawn as an SHX font is, its capitals as high as the text. It is
the default replacement font. No folder of AutoCAD's is looked for any more (`CadFonts.DefaultFolders` is empty), and a list
saved before keeps what it holds. FONTALT takes a big-font half (`simplex.shx,gbcbig.shx`), Global settings > Fonts takes a
font mapping file (`FontMapFile`, read by `CadFonts.ReadFontMap`), and the SHX files missing are said once in one line, as
AutoCAD lists them (`CadDocument.FindMissingFonts`, `CadFonts.WithSubstitution`).

**NEW.** The Text Style dialog (STYLE, ST, -STYLE), the Dimension Style Manager (DIMSTYLE, D, DDIM) with its New, Modify,
Override and Compare dialogs, and Style and Height boxes on the Annotate tab, made from the design. Each field of a dimension
style shows the variable it sets, and a style changed redraws the dimensions drawn in it, as one undo step. AutoCAD 2021's
AUDIT finds nothing in a drawing of 4 new text styles, 4 new dimension styles and their dimensions. **FIXED.** The text style
Standard keeps its name, as AutoCAD keeps it.

**NEW.** Select Color's True Color tab picks in the HSL model, on a field of hue and saturation and a luminance bar, or in the
RGB model, on a bar for each of red, green and blue, as AutoCAD's does.

**NEW.** The presenter makes linetypes and hatches: LINETYPE with the Linetype Manager, -LINETYPE, CELTYPE, LTSCALE and
CELTSCALE; HATCH by internal point or by objects, with the Normal, Outer and Ignore island styles, solid and gradient;
HATCHEDIT; BOUNDARY. Its own linetypes (GeometryHelper.lin, 39) carry the names AutoCAD users type: BORDER, CENTER, DASHDOT,
DASHED, DIVIDE, DOT, HIDDEN and PHANTOM, each with a half size (DASHED2, a dash 3 and a gap of 0.75) and a double size, and
ACAD_ISO02W100 to ACAD_ISO15W100 drawn to ISO 128 for a pen of 1.0 mm. Its own patterns (48) are ANSI31 to ANSI38, the ISO and
basic ones, and its own drawings under AR-B816, AR-BRSTD (bricks 200 by 100, joints of 10), AR-CONC, AR-HBONE, AR-PARQ1,
AR-RROOF, AR-RSHKE and AR-SAND, at real size in millimetres. They are the default; a .lin or .pat is read only from a path or
folder the user gives, and AutoCAD's own files are never looked for. AutoCAD 2021's AUDIT finds nothing in a drawing of these
linetypes and seven kinds of hatch. **NEW.** `DxfHatch.SetPatternDefinitions` and `DxfHatch.SetAssociatedObjects`.

**NEW.** The presenter draws text in its style's SHX font, read from the drawing's folder and the folders listed in Global settings, never shipped:
shapes, unifont and big fonts, every pen code, single-stroke letters as AutoCAD draws them; a TEXT of romans.shx `ABC123` 10
high right-aligned on x 100 is inked from 44.762 to 100, as AutoCAD puts it (the ink boxes of 14 fonts of AutoCAD 2021 agree
with its own to 0.001 where the strokes are straight). A font not found is drawn in the replacement font (GeometryHelper Simplex by
default, as FONTALT) and said once on the command line; Global settings > Fonts lists the search folders, the replacement and
a substitution table. A drawing of 10 000 SHX texts builds in 171 ms where it took 47, and draws whole in 135 ms where it took
38.

**NEW.** `DxfDatabase.UcsTable`, with `DxfUcsTable` and `DxfUcsTableRecord`: the named user coordinate systems of a drawing,
added, changed, renamed and erased as AutoCAD writes them. A system turned 30 degrees about Z at (100, 50, 0) is written with
its origin (100, 50, 0) and X axis (0.866, 0.5, 0); renaming or erasing it updates `$UCSNAME` and `$UCSBASE` where they named
it.

**NEW.** The presenter's user coordinate systems as AutoCAD's, in the plane of the drawing: UCS (origin, X and Y points, Z,
View, World, Previous, OBject, NAmed Save/Restore/Delete/?), UCSICON, PLAN, UCSMAN (a list on the command line), and the named
systems kept in the file. Points typed are in the UCS (`*` gives world ones). The grid, snap, ortho, polar, crosshair, icon,
readout and dynamic input follow it. In a UCS turned a quarter turn, `@100<0` draws a line 100 long up the world's Y. The 3D
options (X, Y, Face) and a dialog for UCSMAN are not built.

**NEW.** `ArrangeItem.Rectangle`, a nullable `GeoRectangle2`, sets labels along the long side of the rectangle of a part in
place of a `Leader`. The rows stand on either side of the long side and slide along it, with the gaps `Offset`, `OffsetTop`
and `OffsetBottom` measured from the long edges: a rectangle 2 000 by 300, a label 100 by 40 and the default gap of 50 put the
first rows 70 off the edges, 220 from the middle line. It may be turned by any angle, its long side the width or the height
where that is greater, and `Side` keeps the label to one edge. Items given no rectangle are arranged as before, to the bit
(the six Arranging benchmarks match their signatures).

**NEW.** `DxfDatabase.Recover(path or stream, out DxfRepairReport)` opens a damaged DXF as AutoCAD's RECOVER does, and
`DxfDatabase.Audit(fix)` checks an open drawing as AUDIT does; each reports what it found (`DxfRepairReport`, `DxfRepair`, 26
kinds of `DxfRepairKind`) and what it fixed. Lines that are not group codes, a file cut short, sections missing or out of
order, a BLOCK without ENDBLK, a POLYLINE without SEQEND, numbers that are no numbers or not finite, wrong vertex and loop
counts, duplicate and missing handles, a `$HANDSEED` too low, owners that name nothing, text of R2007+ not in UTF-8, and layers,
linetypes and styles the tables lack are repaired; a sound file opens byte for byte as before (a drawing of 21.8 MB and 36 112
entities reports "No errors found." in 0.25 s). The presenter has RECOVER and AUDIT (Yes is one undo step), and a file that
cannot be read as it is opens as "<name> (recovered)", unsaved.

**NEW.** The presenter's DRAWINGS list works like a list of files: a click selects a row (Ctrl and Shift add to the
selection), a double click or Enter opens it, and Delete does nothing. The right-click menu has Open, Close, Close Selected
Drawings, Close All Other Drawings and Close All Drawings. Three drawings closed with two of them changed bring one question
listing both.

**NEW.** Object snaps as in AutoCAD 2021: Drafting Settings (DSETTINGS, DS, OS) with Snap and Grid, Polar Tracking and Object
Snap tabs; the OSNAP arrow and right click on the status bar; Shift+right-click at a point prompt; END, MID, CEN and the other
keywords typed for the next point only; AutoCAD's markers, 9 pixels across, yellow on a dark canvas; and the new snaps Node and
Geometric Center (an L of 30 by 10 and 10 by 20 snaps to its centroid (11, 11)). **CHANGED.** A POINT snaps as a Node, not an
Endpoint.

**CHANGED.** Reading a DXF entity finds the groups of its record once for the readings that follow one another, not once for
each property: a HATCH whose loops, pattern and extents are read one by one is parsed once, and the placing of an INSERT
(`BlockTransform`) is read in under half the time. The display of a drawing of 16 268 entities and 5 684 INSERTs is built in
0.88 s where it took 1.26 s (.NET Framework 4.8). Nothing read or saved changes, and no memory is kept beyond the last eight
records read.

**NEW.** Dynamic input asks a point in two fields, as AutoCAD 2021's: X and Y, or distance and angle after a base point. Tab
moves between them and locks a number typed. Enter takes the point from the locked fields and the cursor, so 300 locked, the
cursor straight up and Enter draw a line 300 long at 90°. A coordinate typed whole, "@300<0" or "10,20", goes to the command
line as typed. **CHANGED.** While dynamic input is shown, the canvas's own tip names an object snap only ("Endpoint").

**NEW.** CECOLOR sets the colour of new objects ($CECOLOR): `Enter new object color [Truecolor/COlorbook] <BYLAYER>:`. The
colour box of the Home tab's Properties panel shows and sets it, and its swatch and "More colors..." open Select Color. A true
colour is refused, because the header holds an index.

**NEW.** With Select by dragging off, a press on nothing dragged draws a lasso, as AutoCAD's. Drawn clockwise it takes what it
holds whole (solid blue); counter-clockwise, what it touches (dashed green). Esc drops it. **FIXED.** A box clicked at its
first corner keeps that corner on the drawing while the wheel zooms or the middle button pans.

**CHANGED.** The presenter's canvas keeps a picture of the drawing and draws the selection and the entity under the cursor
over it: moving the mouse over a sheet set of 107 565 items costs 3.3 ms, where it cost 254 ms; zooming and panning move the
picture and draw it again once the view is still. Detail no eye could see is spared: items under half a pixel are one pixel,
text under 3 pixels is left out, and a hatch whose lines come closer than 2 pixels is filled with a tint of its colour. The
sheet set shown whole draws in 26 to 84 ms instead of 261 to 281.

**CHANGED.** A box is clicked corner to corner, as AutoCAD does with PICKDRAG 0, and a click on nothing starts one rather than
clearing the selection. **NEW.** Global settings > Canvas and cursor > Select by dragging lets a box be dragged too.
Selected and hovered objects are drawn solid in a highlight colour, 2 pixels wide, no longer dashed; the grid is drawn as
lines, every fifth stronger. **NEW.** `PickIndex.IndicesOf` and `IndicesIn`, `DisplaySegments2.Rings`,
`CadCanvas.SelectByDragging`, `GlobalSettings.SelectByDragging`.

**NEW.** The presenter's Select Color dialog, as AutoCAD's: the 255 colours of the index laid out as AutoCAD lays them, and a
True Color tab with red, green and blue, hue, saturation and luminance, and hex. It opens from a layer's colour cell in the
Layer manager and from "Select Color..." of the Color row of Properties. A layer given red, green and blue 10, 20, 30 is
written 420 = 660510, with the nearest index in 62. `CadLayerEditor.SetColor(document, name, DxfColor)`. **CHANGED.** Giving a
layer a colour of the index now takes out a true colour it had.

**NEW.** Dynamic input (DYN, F12, the status bar, DYN typed, Global settings > Canvas and cursor > Input). Beside the cursor it
shows the prompt of the command running, the cursor's coordinates, or the distance and angle from the base point ("500.00 <
53.1°"), and what is typed. Ctrl+9 shows or hides the command line.

**NEW.** The presenter's command line knows 17 more of AutoCAD's commands, in AutoCAD's prompts and with its aliases, each run
one undo step: XLINE (XL), RAY, POLYGON (POL), DONUT (DO), SELECTALL (Ctrl+A); LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK,
LAYISO, LAYUNISO; MEASUREGEOM (MEA: distance, radius, angle, area), UNITS and -UNITS (UN, $LUNITS, $LUPREC, $AUNITS, $AUPREC,
$ANGBASE, $ANGDIR), and SCRIPT (SCR). A polygon of 6 sides inscribed in a circle of radius 10 is a closed polyline of area
259.8076, its bottom side level; a donut of diameters 10 and 20 is a closed polyline of width 5. ELLIPSE draws an elliptical
Arc; AREA keeps a total with Add area and Subtract area; LAYOUT renames a layout (Rename) and shows one (Set); LAYOUT Delete
waits for the DXF library to erase a layout.

**NEW.** The presenter's Annotate tab (Single Line and Multiline Text; Linear, Aligned, Angular, Radius and Diameter
dimensions; Multileader) and View tab (Zoom Extents, Window, Previous, In, Out, Pan, Regen; Model and Layout; the Drawings
list and the Properties panel shown or hidden; Grid, Snap, Ortho, Polar, Osnap). The Home tab gains Rectangle, Ellipse,
Point and a Utilities group (Distance, Area, ID Point, List).

**NEW.** Presenter commands: NEW makes Drawing1.dxf, SAVEAS, LAYOUT New and ?, also the + after the layout tabs; DIST, AREA,
ID and LIST in AutoCAD's words (DIST from (0, 0) to (100, 100) reads "Distance = 141.4214"); ELLIPSE; POINT; PROPERTIESCLOSE;
GRID, SNAP, ORTHO, POLAR and OSNAP typed. Each drawing command is one undo step.

**CHANGED.** The presenter's buttons for what is not built are hidden (Paste) or say "not built yet"; the comparison with
AutoCAD 2021's six ribbon tabs is in the drawing plan, 72-autocad-gap.md.

**FIXED.** A screenshot run of the app that left a drawing changed waited for ever on the question to save it when the
window closed; it answers "Don't save".

**NEW.** Drawing plugins, shaped as Tekla's drawing plugins are. A class derived from `DrawingPlugin` says what it needs in
`DefineInput` and adds dimensions to a view in `Run`. `Drawing.RunPlugin` runs it and keeps the run in the drawing's DXF, and
every update runs it again, so its dimensions follow the model. The sample Bolt dimensions gives the pitch 300 of TC5's
base-plate bolts, and 340 once they are moved 20 further apart and the drawing is updated, a hand-moved dimension staying
where it was put.

**NEW.** Drawing plugins in the app: Run plugin on the Drawing tab shows the drawing plugins loaded, the view (the one
selected, else the first), the objects of the view of the kinds the plugin asks for, each ticked, and the plugin's settings
with their defaults and ranges, checked as typed. `PLUGIN` asks for the plugin by name, the view and each setting in turn.
Each run is one undo step, refused in words while the drawing is being made again, and a plugin that fails leaves the drawing
as it was and is said on the command line. The dimensions a plugin adds keep clear of those the view has already: on TC5 the
sample's 50, 300, 50 stand on tier 1 on the left of the front view, outside the drawing's own, where before 7 pairs
overlapped.

**NEW.** The plugin manager loads drawing plugins from a `.dll` or a `.ghplugin` whose plugin.json says `"kind": "drawing"`,
through a `DrawingPluginCatalog`. It uses the same shadow copy, trusted folders, startup list and log as command plugins; on
.NET 8, unloading frees the plugin's code.

**CHANGED.** `UpdateReport.PluginRunsToRunAgain` now lists only the runs whose plugin is not at hand. `PluginRunsRun` and
`PluginRunsDropped` (with the reason) say what became of the others.

**FIXED.** Installing a plugin whose id is "...", ". ." or ends in a dot or a space is refused: Windows read such an id as the
plugin folder itself, and installing it deleted every plugin installed.

**FIXED.** A hand edit whose writing fails is taken back alone: an earlier hand edit, a mark moved before, stays in the
drawing and in its file.

**FIXED.** EXPLODE of a dimension, mark or view of a model drawing is refused in words ("This dimension belongs to the model
and cannot be exploded here."); before, the dimension was erased and recorded as a hand edit, and its pieces lost.

**FIXED.** An update prepared before the drawing was made again is refused by `Drawing.Apply`: adding the top view and the
isometric one at once no longer loses the first. In the presenter a second Add view, AUTODIM or AUTOMARK while B1 is being
made says "B1 is being made again; wait for it.", and a drawing closed meanwhile is not written into.

**FIXED.** Export no longer saves the drawing shown from a worker thread, nor ties its database to the exported or temporary
file; the bytes written are unchanged.

**FIXED.** Save As onto the file of another open document, B-101.dxf of a model drawing among them, is refused until that
document is closed.

**FIXED.** An error of any kind from a source during AUTODIM, AUTOMARK, centre lines, VIEWSECTION or VIEWDETAIL is said on
the command line and logged, instead of ending the app or the Tekla it runs in.

**FIXED.** Undoing the first change of a drawing takes its preset and source back in the file and in Properties; redo brings
them again.

**FIXED.** The texts of short neighbouring dimensions no longer stand side by side as one number. In a chain 50, 300, 50
at 1:20 the two 50s took the 300's place and read as "5050"; now a text that fits in the middle of its line keeps it, a text
too long for its step goes past the end of the row, and every text keeps a quarter of its height clear of other texts and of
arrowheads. `Annotator.FindOverlaps` now counts texts closer than that, a text over an arrowhead, and a text put past its
line beside another dimension line along the same axis. Of 954 dimensions on the test bodies at 1:2 to 1:20, 788 are the same
to the bit; the measured values and the ids are the same except in the crowded TC9 front view, which drops two more
hole-edge dimensions at 1:20.

**NEW.** `DrawingDocumentStore.WriteValues` and `ReadValues`: named values the app keeps with a drawing in the XRECORD
`VALUES` of GH_DRAWING. A file without them reads as none, and writing none leaves a drawing as one never given any, byte
for byte.

**FIXED.** A model drawing saved and opened again keeps the source it was made from, its preset and the assemblies it
draws whole. Update from model chooses that source when one of the name is connected, Properties shows the preset
(Assembly, not Custom), and a fitting added to a whole assembly is drawn. Drawing settings, Add view, Auto dimension and
Auto marks work on a drawing opened again from its file: they make it again from the source its file names, or the one
Update from model found its objects in, in one undo step. With none at hand they say to pick its source in Update from
model first, and a source with none of its objects is refused, leaving the drawing as it was.

**FIXED.** Opening a file that a drawing just made was saved as shows that drawing instead of opening a second document
over the same file.

**FIXED.** A text override of a single space is kept, so the dimension shows no text, as in AutoCAD. Every other value is
still trimmed.

**CHANGED.** Global settings' Layers lists Symbol and Viewport (not plotted) by default, every layer a model drawing
writes. A settings file that lists its own table keeps it.

**CHANGED.** Placing labels among many blocks is faster, with the same results to the bit: `Arranger.Run` skips the
obstacles whose box lies farther than the nearest distance already found, and asks the spatial index for the
neighbourhood of each place, not all of them. The densest arranging benchmark goes from 55.8 ms to 49.9 ms.

**CHANGED.** The guides are split into pages of a few hundred lines each, their words unchanged: the plane into
`plane/`, space into `solid/`, and the DXF half of the shared types into `dxf/`, each page in the site's menu. The old
pages stay, each with a list of its pages; a bookmark to a section that moved lands on that list, by the link to the
page the section is on now. A page "How do I" gives a task, the call that does it and the page that tells the rest; the
AutoCAD patterns of the DXF model have a page of their own; and the first mention of each public type on a page links to
its page of the API reference. Every page opens with what it is for, is cut into sections with headings (a bold lead that
opened a paragraph is now a heading), and ends with "See also"; the blocks page is four, two of them on dynamic blocks,
and combining bodies two. No guide page is over 500 lines. Each group of the menu opens on a page listing its pages,
meshing and the checks and reports among them.

**FIXED.** `DxfDBDictionary.SetAt` read and wrote every entry of the dictionary again for each entry added, so that
10 000 XRECORDs added one by one to one dictionary took 84 s and 2 000 took 1.5 s. The entries are now found by name and
by handle without reading the record again, and the entry added, replaced or removed is put into the record's bytes as
they stand, written into one array of the record's new length rather than grown and copied, and as before to the byte:
the 10 000 take 1.1 s and the 2 000 0.02 s. `GetAt`, `Contains`, `Remove` and the erasing of an XRECORD or a GROUP a
dictionary names cost as little: 2 000 entries looked up one by one take 0.001 s, where they took 1.5 s. Every other
edit that puts groups into a record's bytes gains as well: 10 000 vertices added one by one to a LWPOLYLINE in a drawing
take 0.26 s, where they took 0.83 s, and 2 000 loops added to a hatch 0.32 s, where they took 0.56 s.

**NEW.** `DrawingSourceConvert` in GeometryHelper.TeklaConvert reads Tekla model objects into the `DrawingSource` of
GeometryHelper.Drawing, in the current work plane and without changing the model: `FromAssembly` the main part, the
secondaries and the sub-assemblies, set in the main part's frame; `FromPart` one part in its own frame; `FromObjects` a
selection. A part comes with its body, its frame, its name, its PART_POS as its mark, its profile, its material, its
assembly as its parent and ASSEMBLY_POS, LENGTH and WEIGHT as attributes. A bolt is made from its group's data: an M20
group of four, 60 long, gives four cylinders of 24 sides and 20 across along the group's Z axis, as long as the body
Tekla draws for them, with Diameter "20", HoleDiameter "22", Length "60", Count "4", Standard and the parts bolted; a
group with holes only gives holes of 22 through the parts bolted. A weld carries what its symbol reads: a fillet of 5
above the line, 200 long, is WeldType "Fillet", WeldSide "Arrow", WeldSize "a5" and WeldLength "200". Each
GeometryHelper.TeklaConvert.{year} package depends on GeometryHelper.Drawing from now on.

**NEW.** The sheet of a DXF layout is set, as AutoCAD's `PlotSettingsValidator` sets it, where it was only read: a layout
made in code lay on the A3 it was given. `CanonicalMediaName` takes a name that ends in the sheet's size in millimetres
and sets `PlotPaperSize` from it: the ISO sheets A0 to A4 on their sides, `ISO_A2_(594.00_x_420.00_MM)`, or upright,
`ISO_A2_(420.00_x_594.00_MM)`, and a size of one's own as ezdxf names one, `ezdxf_(1000.00_x_500.00_MM)`.
`PlotPaperUnits`, `PlotRotation`, `PlotPaperMargins` and `PlotOrigin` are set too, and each makes the layout's limits the
sheet about its plotted area again, as ezdxf's `page_setup` does. A layout made in code and set to A2 on its side with
margins of 10 lies on a sheet 594 by 420, its limits (-10, -10) to (584, 410), and reads so after saving. A layout of a
file reads as before, and a value set to what it has writes nothing.

**NEW.** An anonymous block is added as AutoCAD adds one: `db.BlockTable.Add(new DxfBlockTableRecord { Name = "*D" })`
adds a block the table names `*D1`, the first number no block, INSERT, dimension or table of the drawing names, marked
anonymous (flag 1 of its BLOCK) and written as ezdxf writes the picture of a dimension; `*U` gives `*U1` and on. So a
dimension made in code carries its own picture: a rotated dimension of 100 given a `*D1` that holds its line and the
text 100 is saved and read back drawn by `*D1` with its two entities, where a block named so was refused before. A
drawing whose dimensions ezdxf drew as `*D1` to `*D11` names the next `*D12`.
**FIXED.** DWG: AutoCAD dropped the data of a dynamic block written again. The anonymous block of a dynamic block that no
INSERT places (`*U6` of `A-01`, true name `Drawing Title`, three attribute definitions) is kept by AutoCAD only with its evaluation
graph; the graph, its linear parameter, stretch action, two grips and four location components, the representation data of
the block's anonymous copies and the purge preventer were left out of the DWG, and AutoCAD took the block for an unused one and
dropped it from the drawing it opened, with its attribute definitions' 6 fields and draw order table: 48 of the 2 646 fields and
8 of the 256 draw order tables of AutoCAD 2021's 120 sample and template drawings, in `A-01`, `A-02`, `A-04`, `A-05`, `AS-01`,
`S-01`, `S-02` and `S-03`. These seven types are read (AutoCAD's DXF of the nine sheets with the block, `S-04` among them, group
for group) and written, from R2007; a graph that names an expression the writer does not write (a flip, a rotation, a visibility
state) is left out with the expressions it owns, as it was. Written again, the 120 drawings give AutoCAD all 2 646 fields and 256
draw order tables, `AUDIT` finds the errors of the originals in 7 and none in 113 (the same numbers), and the 71 test drawings
audit at 0.
**FIXED.** DWG: a real as large as `double.MaxValue`, which AutoCAD keeps for a value not set yet (the grip location components of
a dynamic block), stopped the whole DWG from being read ("The DWG file is broken: Value was either too large or too small for a
Double"): its digits as the runtime writes them rounded past the largest number. It is given as AutoCAD's DXF gives it,
`1.797693134862314E+99`.
**NEW.** DWG: the CELLSTYLEMAP objects of a drawing of AutoCAD 2000 to 2007 (the cell styles a table style keeps for a round trip) are
read, as AutoCAD's DXF gives them, group for group in the table drawings of R2000, R2004 and R2007, and no longer counted among
the types not read.
**FIXED.** DWG: a cell style of a table style's own (101 and up) that names no text style, made in code, is written with the text
style Standard, as the rows of a table style are, which AUDIT could not repair without it.
**FIXED.** DWG: the multibyte codes of a DXF of R2004 and before (`\M+182A0`, the Shift-JIS a) are written as the letters they stand
for, as the `\U+` escapes are, in every name and text; `\u+` and `\m+` in small letters too.
**NEW.** DWG: the evaluation graphs of dynamic blocks with a visibility, flip, rotation, XY, polar, point, base point, lookup or
alignment parameter, and with a polar stretch, move, scale, rotate, flip, array or lookup action, are read and written from R2007 (21
more object types, with their grips; only the linear parameter with a stretch action was before), so AutoCAD keeps the dynamic
behaviour of the block, and the anonymous block of one that no INSERT places. 72 of the 90 evaluation graphs of AutoCAD 2021's 120
sample and template drawings go through (12 before): 41 with a visibility parameter, 14 with a flip, 12 with a rotation, 18 with a
point and 16 with a base point parameter, 10 with a lookup, 24 with a move action, 12 with a scale, 6 with an alignment parameter, 4 with
an array action, 2 with an XY and 2 with a polar parameter. AutoCAD's DXF of the DWGs written again holds their 1 492 objects as the
DXF of the originals does, group for group (15 stretch actions list their entities in AutoCAD's own order), and `AUDIT` finds what it
found before: 113 drawings at 0, 7 with the errors of their originals; the 71 test drawings audit at 0. The other 18 graphs hold a
constraint parameter, a user parameter, a properties table or a proxy node and are left out whole, as before.
**FIXED.** DWG: the unset value of a grip location component of a dynamic block (AutoCAD's DXF gives `double.MaxValue` as
`1.797693134862314E+99`) was written as the number that text names, which AutoCAD read back as `1.797693134862313E+99`; it is written
as the largest real, as AutoCAD keeps it.
**FIXED.** Presenter: the leader of an angle's words put by DIMTEDIT under DIMTMOVE 1 comes and goes at exactly the height of the
arc's middle (probed every 0.1 across it: AutoCAD 2021 draws none for "60°" 19.5 left of the vertex at 15.0, one at 14.9), and between
the height of the vertex and that height the band narrows as the point rises (out by 0.2 landings, in on the left of the vertex by 0.28,
wider by 0.1 on the right): 159 of 5 832 probes differing from AutoCAD down to 101 (a6 29 to 21, a4 15 to 13).
**FIXED.** Presenter: free words of a radius or a diameter (DIMTMOVE 2) stand on its slanting line wherever it runs through their level
box grown by DIMGAP, and the line is broken about the middle of what it takes of the box ("%%c40" put 3 off a diameter of 40 breaks it
from -2 to 5.88 along it, its middle at 1.94, not the foot of their middle); a radius's arrowhead goes out only where their box takes
the line from a quarter of a unit behind the centre to an arrowhead and a half past it (words 3.5 behind the centre of "R8" left it
inside and the line whole, which was pushed out): the 8 differing probes of r6 down to 0, and 176 of 760 in a grid of points along
and across the line down to 7.
**FIXED.** Presenter: the words of a jogged radius put exactly on the rim are inside it (they stay at their point and the box goes to
the inside; 16 of 16 probes), and level words kept to a line of exactly two arrowheads within 15 degrees of level are left off it,
as AutoCAD 2021 does 14 times of 16 (t9 38 to 34, ta9 28 to 18).

## 13.11.0 (2026-11-07)

A minor release: public types and members are added and none is removed or renamed in any of the four libraries. OLE
objects in DXF drawings, a picture pasted, a sheet of Excel embedded or a document linked, read until now as
`DxfUnknownEntity`, are `DxfOle2Frame`: read as AutoCAD 2021 reads them, from the corners in the head of their binary,
with the picture of a static one given back as its .bmp, .wmf or .emf; moved, scaled, mirrored and sized upright as
AutoCAD 2021 does; and made in code from a .bmp or a .png, which AutoCAD's .NET API cannot. Checked in AutoCAD 2021,
whose console read back every frame the library read, moved or made, AUDIT finding no error, and whose GUI drew the
pictures the library made and moved, and against drawings its GUI saved after ROTATE, SCALE, MIRROR and a paste into
paper space. A drawing only read and saved is written as it was, to the byte.

**NEW.** OLE2FRAME entities get a class of their own, `DxfOle2Frame`, as AutoCAD's `Ole2Frame`, with `DxfRectangle3d`,
`DxfOle2FrameItemType` and `DxfOlePictureFormat`. The frame is read from the four corners in the head of its binary, as
AutoCAD 2021 reads it, not from groups 10 and 11, which it ignores: a picture of 3 by 2 pixels pasted with its lower
left corner at (10, 20, 0) and stretched to 30 by 20 gives `Location` (10, 20, 0), the lower left corner, as AutoCAD
2021 gives it; `Position3d` the four corners as held, turned, sheared or tilted; `WcsWidth` 30 and `WcsHeight` 20, the
runs along x and y as AutoCAD works them out (25.98 for the frame 30 wide turned 30 degrees); `ScaleWidth` and
`ScaleHeight` against the size AutoCAD keeps at 100 percent; `Rotation` (radians), `LockAspect` and `AutoOutputQuality`
from the extended data between OLEBEGIN and OLEEND, and `OutputQuality` from group 73. What the object is comes from its
compound file, read by the library's own reader of MS-CFB (FAT, DIFAT past 109 sectors, mini stream, directory tree):
`Type` (static, embedded or linked, as MFC finds it), `UserType` (`Picture (Device Independent Bitmap)`, `OLE Package`),
`IsLinked`, and `LinkName` and `LinkPath` of a link's moniker (`C:\Drawings\cost.xlsx!Sheet1!R1C1:R4C3`,
`C:\Drawings\cost.xlsx`). The library's own `GetCompoundDocument()` gives the compound file as written, and
`GetPictureData(out format)` the picture of a static object as a .bmp, a placeable .wmf or an .emf. `GeometricExtents`
is the box of the corners, `GetGeometry()` their closed `GeoPolyline3`, which `IntersectWith` crosses, and `Explode()`
gives four LINEs along the edges, as AutoCAD 2021 gives four. A frame is copied by `Clone()` into its own drawing,
another or a block with its binary and extended data byte for byte, and a drawing only read and saved is written as it
was. A binary damaged never makes a getter throw: the corners come from 10 and 11 where the head holds none, the kind
and user type from groups 71 and 3 where the compound file cannot be read or names none, and there is no picture, said
in the log at Debug. A frame of no corners at all, as accoreconsole writes one it could not read, stands at the origin
and explodes into nothing, and `GeometricExtents` throws `InvalidOperationException` for it, as for any entity with no
extents, so that `UpdateExt` passes it over. `Rotation` is the number stored, a NaN another writer stored given as it
is. The rules are AutoCAD 2021's own, which read the frame from the head where 10 and 11 said otherwise and gave the
lower left corner as `Location`. The OLEFRAME of R13 stays a `DxfUnknownEntity`.

**NEW.** A `DxfOle2Frame` is moved and sized as AutoCAD 2021 does, upright, checked against drawings its GUI saved:
`TransformBy` takes a move, a scale by one factor and a mirror about a line along x or y, writing the corners into the
head of the binary, which AutoCAD reads, and into groups 10 and 11, only the lines of binary the head is in written
anew; a mirrored frame is the box of its corners, the picture not turned over, and a scaled one keeps its size at 100
percent and scales its text size, as AutoCAD's SCALE does. A turn, a shear or a stretch is refused with
`ArgumentException`. The picture 30 by 20 at (10, 20) scaled by a half is 15 by 10, its `ScaleWidth` 50, which is now
the size against the one AutoCAD keeps at 100 percent (codes 41 and 42 of the extended data). `Position3d`, `WcsWidth`,
`WcsHeight`, `ScaleWidth`, `ScaleHeight`, `LockAspect` and `OutputQuality` are set as AutoCAD 2021 sets them, the upper
left corner kept. A frame copied into model or paper space is given the head's signature, u32 and group 72 AutoCAD
writes for that space; an INSERT exploded places its frames where the placing keeps them upright.

**NEW.** `new DxfOle2Frame(bytes)` makes a static picture of a .bmp or a .png, as AutoCAD 2021 makes one of a picture
pasted, which AutoCAD's .NET API cannot: the bitmap in a compound file of the class StaticDib written by the library's
own writer (MS-CFB version 3, a DIFAT past 7 MB), behind AutoCAD's head and with its extended data. A PNG of any kind is
read by the library's own decoder into a bitmap of 32 bits a pixel, its alpha laid over white. The frame stands at the
picture's natural size in millimetres, 610 by 224 pixels of 96 dots an inch being 161.38 by 59.26, and the same picture
gives the same bytes each time.

**CHANGED.** An OLE2FRAME read from a file is a `DxfOle2Frame`, not a `DxfUnknownEntity`: code that found one by
`DxfUnknownEntity.DxfName == "OLE2FRAME"` finds it by its class. Its `GetGeometry()` and `GeometricExtents` give its
frame where they gave nothing and threw, so `UpdateExt` takes it into the extents; `Explode()` gives four LINEs where it
threw; `IntersectWith` crosses its frame. `TransformBy` moves it where the transformation keeps it upright and refuses
it with `ArgumentException` where not, an INSERT exploded places it where its placing keeps it upright, and the actions
of a dynamic block still refuse it. `DxfReader` reads none, as before.


## Earlier releases

The notes of 13.10.0 and earlier are in [CHANGELOG-ARCHIVE.md](CHANGELOG-ARCHIVE.md).
