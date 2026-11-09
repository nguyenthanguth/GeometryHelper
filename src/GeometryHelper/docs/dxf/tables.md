# Tables of cells

Tables of cells (ACAD_TABLE): their cells and values, their structure, merging, grid lines, formulas and series, and
their links to data.

## The table and its cells

An ACAD_TABLE is a [`DxfTable`](xref:GeometryHelper.Dxf.DxfTable): its `NumRows`, `NumColumns`, `RowHeight(row)`, `ColumnWidth(col)`, and each cell's
`TextString`, `Value`, `CellType`, merging (`IsMergedCell`, `GetMergeRange`), corners (`GetCellExtents`), block and
attribute values, and the look it holds of its own, `Alignment`, `TextHeight`, `TextStyle` and the colours, null where it
has none and takes its table style's, which `DxfTableStyle` reads. Its record is read once while it stands, every cell
of 1 000 rows in milliseconds. Its cells are written in the form of R2000 (text in 3 and 1) or of R2007 on (between
`301 CELL_VALUE` and `304 ACVALUE_END`, the value in 2 and 1 and its text in 303 and 302), a text longer than 250 letters
cut into pieces of 250 letters as AutoCAD 2021 cuts it, an escape given counted as its one letter: AutoCAD reads a cell of
R2000 in pieces of 2 as empty, and the escapes of each piece alone. `SetTextString` and `SetValue` given the words a cell
has, as written or as read, change nothing. `SetTextString`, `SetRowHeight`, `SetColumnWidth`,
`SetBlockScale`, `SetBlockRotation` and `SetBlockAttributeValue` write that data, the table's block (`*T12`) left for
AutoCAD to draw anew. Scaled alike by `TransformBy`, a table scales the heights of its rows, the widths of its columns, its
cell margins and the text heights and block scales it and its cells override: a table of rows 10, 8 and 8 high scaled by 2
has rows 20, 16 and 16 high, where AutoCAD 2021 measures the rows anew by its font (22, 18 and 18). Mirrored, it stays readable, as AutoCAD 2021's `TransformBy` leaves it: its
normal the mirror's image of its normal, its direction the image reversed, and its point the image of the far end of its
top edge, so that the table at (300, -100), 70 wide, mirrored in the plane x = 280 stands at (190, -100) facing up. Mirrored
so that the image of its direction still reads left to right or upwards, it stands at the image of its point along that
image, its rows running the other way (`FlowDirection` `BottomToTop`, its 70 of 1 written in its head with the flag 0x04 of
its 93, as AutoCAD 2021 writes it): mirrored in y = -150 the table stands at (300, -200) along (1, 0, 0), and in the line at
45 degrees through (280, -100) at (280, -80) along (0, 1, 0), as AutoCAD's do. Scaled, every cell that has no text height of
its own is given the height of its kind of row, scaled, as its own (its 140 with its flag 0x20), as AutoCAD 2021 keeps a
table it scales: the table's own height for that kind of row where it has one, else its table style's, from the style the
table names, else the drawing's Standard, else AutoCAD's own Standard, the title 6 and the header and the data 4.5 in a
drawing of millimetres (`$MEASUREMENT` 1), 0.25 and 0.18 in one of inches. The table of point 54 scaled by 2 has the two
cells of its title 12 high and the others 9, its cell of its own 3.5 now 7. AutoCAD read the heights written as the table's
own instead as 0.18 in every cell, so none is written there. Its rows are still not measured by AutoCAD's font.

## Table styles, new tables and their block

A TABLESTYLE of ACAD_TABLESTYLE is a [`DxfTableStyle`](xref:GeometryHelper.Dxf.DxfTableStyle) (a table's `TableStyle`, which
it also sets): its `Description`, `FlowDirection`, `HorizontalCellMargin` and `VerticalCellMargin`, `IsTitleSuppressed` and
`IsHeaderSuppressed`, and for the title, the header and the data `TextStyleName`, `TextHeight`, `Alignment`, `Color`,
`BackgroundColor`, `IsBackgroundColorNone` and the lineweight, colour and showing of each of the six grid lines, read and
set as AutoCAD's `TableStyle` names them. A group the record leaves out reads as AutoCAD's Standard has it, of the
drawing's units: the title 6 and the header and data 4.5 high, margins of 1.5, in millimetres. `new DxfTableStyle()` is
that Standard, added by `db.TableStyleDictionary.SetAt("GH_SCHEDULE", style)` with every group AutoCAD writes of a style
(the data's cell, then the title's and the header's, each its text, fill and six lines); no map of cell styles is
written, and AutoCAD 2021 audited drawings of R2000, R2013 and R2018 holding them with no error.

`new DxfTable(position, rows, columns, rowHeight, columnWidth)` makes a table in no file; appended, it names the drawing's
current table style, its first row is merged across as AutoCAD's title, and it is drawn in a new anonymous block, `*T1`
and on (`new DxfBlockTableRecord { Name = "*T" }` names one so too); in a drawing before R2007 its cells take the form of
R2000. `RecomputeTableBlock()` draws a table anew in its block from its cells, as AutoCAD's does: a SOLID for each filled
cell, a LINE for each run of a grid line of one look, none inside merged cells, an MTEXT for each cell's text at its
alignment, as wide as the cell less its margins, and an INSERT for each block cell, each look the cell's own, else the
table's of its row's kind, else its style's. A table of rows 9 high and columns 30 wide, its title merged across three
columns, is drawn with 9 lines; one of 100 rows of 20 columns, every cell written, in about 40 ms on .NET 8. Rows keep
their heights: a text taller than its row is not made room for. The block of a table is placed at the table's point,
its entities turned along the table's direction, as `TransformBy` carries them.

## Values and their kinds

A cell's value has its kind, `DataType` (`Long`, `Double` or `String`, the value's 90 from R2007 on), what its number
measures, `UnitType`, and its format, `Format` (`%lu2%pr2`), each in the cell's `301 CELL_VALUE`.
`SetValue(row, col, 12.5)` writes a number of the kind `Double`, its unit kept and its format taken away, as AutoCAD 2021
took a format of feet and inches away from a cell given 100.125, its text as AutoCAD writes and shows it, 12.500000, and
`SetValue(row, col, 42)` a whole number. A value of the flag 1 (93 of 1, 5 or 7, as AutoCAD writes a block cell's, a
formula's and an empty cell's) AutoCAD 2021 reads as none, whatever text it holds, so a text or value given to such a cell
takes the flags 6 AutoCAD writes for one it is given: the block cell made a text cell and given M16 read empty before.
From R2007 on a value or a text set gives the cell the flag of its kind of value (`91 262144`) where it has it not, a cell
reset, read of no value or put in, as AutoCAD 2021 gave it.
`SetFormat`, `SetDataType` (as AutoCAD 2021 turns a value: the text 12 into the whole number 12, 12.7 into 12 and -12.7
into -12, the text 12 into the number 12 shown 12.0000 in the drawing's units under `%lu6` and 1e3 shown 1.0000E+03 under
`%lu1`, the text Parts into a number of no value, `93 7` and an empty text, each but a text of a whole number losing the
flag 2 of its value, `93 6` made 4; a unit of several flags as `Angle | AngleNotTransformed`, 65 538, written as it is; a
kind other than a text, a whole number and a number refused, the library knowing no groups for it) and
`ResetValue` write the rest, in the cell's value: none of them sets a flag of the cell's overrides, so `GetOverrides`
gives what it gave, but for a block cell given a format, which loses its flag 0x40000. `SetFormat` writes a number's text anew by a decimal format (`%lu2%prN`) and by one of feet and
inches (`%lu4`): 12.5 under `%lu4%pr2` is 1'-0 1/2", to the nearest 1/2^N of an inch (four where no `%prN` is given), a
half away from nought, -0.25 -0'-0 1/4" and -0.001 0'-0", `%zs1` leaving nought feet out (7 1/4") and `%zs2` nought
inches (1'), as AutoCAD 2021 wrote them; it leaves the text as it is under one of another unit, and for a number of more
feet than 32 bits hold, which AutoCAD writes as garbage or in powers of ten. Only a number keeps a format: a whole
number, a text or a value of another kind given one is written with none, 7 given `%lu2%pr2` still shown 7; a value of
none becomes the empty one of no kind (`93 5`, `90 512`), even for an empty format; a block cell keeps none and loses its
flag 0x40000; and each value given a format loses the flag 2 of its value (`93 6` made 4), as AutoCAD 2021 wrote them.
`ResetValue` leaves a value of none (`93 7`, `90 0`, the
cell's flag 0x40000 taken out), as AutoCAD 2021 wrote it and read it as of no kind, and takes the looks of the content
with it: the cell's own text style, text height and colour (7, 140, 64), but those alike the table's own looks of its
row's kind, and its turn (145 made nought), its alignment, background and lines kept. B-12 of GH_TXT, 5 high, red, turned
90 degrees, bottom right on green keeps bottom right on green; in a table of its own data of 9, GH_TXT, red and top left
it keeps those, which `ClearTableStyleOverrides(1)` then takes away with the table's while the drawing is open, and which
`ClearTableStyleOverrides` of 0 or 2 leaves the value `93 7`, `90 0` it was given, as AutoCAD 2021 did. A block cell reset
becomes a text cell of no value (`91 0`, `93 7`, `90 0`), its block gone, as AutoCAD 2021 emptied the block cell M12; the
library refused it before. From R2007 on, `SetTextString` of an empty text does what `ResetValue` does, as AutoCAD 2021
wrote it: B-12, 1.5 or the empty text of the title's hidden cell given `""` are of no value, where the library wrote an
empty text. A cell of a file before R2007 holds its
text alone, so a value set there is written as its text, a number as the invariant culture writes it (12.5, whole), and
reads back as one, and a kind or a format other than a text and none is refused. A date or a point is refused as a value: the library
knows no groups for one, as the DXF reference does not describe a cell's value. The look of a cell is set as its own
override with its flag in the cell's 177 (91 from R2007 on), in the order the DXF reference lists them: `SetAlignment`,
`SetTextHeight`, `SetTextStyle`, `SetContentColor`, `SetBackgroundColor` (a colour of the index, BYLAYER or BYBLOCK; a
true colour refused) and `SetBackgroundColorNone`, a background filled written `283 0` with a colour of its own (`63 0`,
BYBLOCK, where the cell has none) and one unfilled `283 1`, or no override at all where its row's kind is unfilled, as
AutoCAD 2021 writes and reads them; `SetTextRotation` and `SetRotation` write a cell's turn (145), `SetAutoScale` a
block cell's fitting (174), and `SetCellType` makes a block cell a text cell of no value, its block and attribute values
taken out. An override the cell has already is written in its place, before the cell's 92 where AutoCAD 2021
writes its own (`170 5`, `7 GH_TXT`, `140 3.5`): `SetTextStyle` of such a cell wrote a second 7 after its value, which
AutoCAD read and the library did not. From R2007 on a cell's overrides set are all written there, in AutoCAD's order
(170, 7, 63, 64, 283, 140, then the right, bottom, left and top edge, each its lineweight, showing and colour), those it
held after its value moved with them; before R2007 after its text, where AutoCAD writes them. `GetOverrides` gives what a
cell holds of its own as AutoCAD's `CellProperties`, its flag of its kind of value (0x40000) as `DataType | DataFormat`,
as AutoCAD 2021 reads that flag from a file; `SetOverrides` gives it those named as AutoCAD 2021 gives them, its
flags exactly those, its alignment and background kept whatever is named, one it has not given the value AutoCAD gives
(the alignment its row shows, a background of none, the colour by block, the text height 0.18 of the content's own format,
a text style of none), its colour, text style, height and kind of value not named taken away, so that the cell takes them
from its table style: Qty given `TextHeight` is written `91 32`, `140 0.18`. `RemoveAllOverrides` takes them
all, wherever they stand outside the cell's value, and the edges of its lines from its edge flags (172), as AutoCAD
2021's own took `170 5`, `7 GH_TXT` and `140 3.5` out from before the 92 of its cell (1, 0) and wrote a cell of `172 9`
as `172 0`, both keeping the cell's own kind of value (its flag 0x40000), for which the library knows no groups, but in a
block cell, which loses it; it also turns the cell's text or block back to nought (145), and a cell of a row of the
title's or the header's kind out of its place keeps the looks of its kind (`170 5`, `140 6.0` in the title moved down by a
row put in before it, and in a row put in that takes the title's look, the title shown), as AutoCAD 2021 wrote them; and
`TableStyleOverrides` and `ClearTableStyleOverrides` read and take away the table's own (its flags 93 to 96 and the
groups after its last cell, or in its head after those flags and before its heights, where AutoCAD 2021 writes them),
in one pass over the table's groups: the cells of a table of 16 001 rows of 2 columns, half
of them of three overrides, are cleared in 0.19 s, where taking each group out on its own took 6.6 s.

### Clearing overrides

`ClearTableStyleOverrides` of 0 or 2 leaves each cell of no value the empty value of no kind (`93 5`, `90 512`) and each
block cell without its flag 0x40000, and of 2 gives each cell the table's own looks of its row's kind as its own, as
AutoCAD 2021 wrote them, so that a table of its own data of 9, GH_TXT, red and top left still shows them in each cell. A
cell emptied while the drawing is open keeps its `93 7`, `90 0`, wherever rows or columns put in or taken out moved it
since, as AutoCAD 2021 kept it: one reset or given an empty text, one put in by `InsertRows`, `InsertColumns` or
`SetSize`, and one a merge emptied, the merge made on load among them; the table given a row before its row 2 and
cleared of 0 writes `93 7` in both cells of the new row, and `93 5`, `90 512` in a cell read of no value from the file.
A clear of any option leaves the title and the header left out as they are (280, 281 and bits 0 and 1 of 93); of 0 and 2
it turns each cell's text and block back to nought (145), and gives a cell of a row of the title's or the header's kind
out of its place the looks of its kind, as AutoCAD 2021 did: the table given a row before its title and cleared of 0
writes `93 3`, `280 1`, `281 1`, its title cells `170 5` and `140 6.0` and its header cells `170 5`, and AutoCAD then
merges the new first row on load as it merges its own. AutoCAD keeps the table's own looks of the data with each row of
data, so that a row put in before the first row of data, which takes them from the table and gives them to the old rows'
cells, leaves them with those rows while the drawing is open: a clear of 2 and `RemoveAllOverrides` give B-12 and M12
back GH_TXT, red, 9 and top left, wherever the rows went since, and a clear of 0 or 1 takes them from the rows with the
table's own. AutoCAD keeps the looks of every row with the row, and reads the table's own looks of a kind from its first
row of that kind; the library follows the rows the same way while the drawing is open. A row `InsertRows` or `SetSize`
puts in keeps none, so that the table of its own data looks given a row after B-12 and cleared of 2 writes the new row's
cells `91 0`; a row `InsertRowsAndInherit` puts in keeps those of the row whose look it takes; and where `DeleteRows`
takes out the first row of data, the table's own looks of the data are those of the next: the row put in at 0 taken out
again gives the table its looks back as they were (`93 6886016`), and B-12 taken out after a row put in after it leaves
it none. The kinds of the rows are read from the TABLECONTENT once for each writing of it, and only where it holds
more rows of the title's or the header's style than the table shows: a clear of a table of 400 rows takes as long as it
did before the kinds were read, and taking away the overrides of its cells one by one too.

## Walking and setting cells

The cells are
walked row by row (`foreach (DxfCell cell in table)`), and `table.Cells[1, 0]` is a [`DxfCell`](xref:GeometryHelper.Dxf.DxfCell) that reads and sets one,
`cell.TextString = "Count"`, `cell.Alignment = DxfCellAlignment.MiddleCenter`; `table.Rows[0].Height` and
`table.Columns[1].Width` are read and set, and the block of merged cells `GetMergeRange(0, 1)` gives reads its cells,
`range[0, 0].TextString`. `HitTest(point, viewVector)` gives the cell a point hits, carried along the direction it is
seen in onto the table's plane, on a table turned or mirrored as on a flat one: on the table of columns 40 and 30 wide
and rows 10, 8 and 8 high at (300, -100, 0), (350, -112, 5) seen from above hits the cell (1, 1). `AttachmentPoint` is
where a cell's content is placed, in from its sides by the margins as its alignment says, AutoCAD's STANDARD style's
alignment taken where the cell has none of its own: the header cell (1, 1) is placed at (355, -114, 0). A cell of a
block of merged cells is placed by the block's top left cell, which holds its content, and the top is the cell's top as
it is seen whichever way the rows flow: with the rows of that table flowing up, the cell (2, 0), from y -82 to -74, is
placed at the top middle at (320, -74.06, 0). None of these draws the table's block anew. What a file of these versions
has no group for is not there: a cell's state, tooltip, cell style, margins of its own, content layout, custom data and
merging of all, and a column's name; nor are the forms by row type (`SetAlignment(align, rowTypes)` and the like), whose
groups after the last cell the DXF reference lists without their order.

## Changing the structure

A table's structure changes as AutoCAD's does, each change written in one writing of its record, its block left for
AutoCAD to draw anew. `InsertRows(row, height, rows)` and `InsertColumns(col, width, columns)` put in rows and columns
of empty text cells, written in the form of the file (text in 1 before R2007, from R2007 on a `301 CELL_VALUE` of no
value, `93 7` and `90 0`, as AutoCAD 2021 writes a new cell and reads as of no value), with
their counts (91, 92) and heights and widths (141, 142), written as reals as AutoCAD 2021 writes every real of a table (`141
9.0` for a row 9 high, `142 20.0` for a column 20 wide): the table of rows 10, 8 and 8 given two rows 6 high before its
row 2 has rows 10, 8, 6, 6 and 8, and a column 25 wide put in before its column 1 grows the title merged across it to
three columns; so does one put in before column 0 or after the last, Parts kept in (0, 0), as AutoCAD 2021 grows a
title merged across the table, where a merge of another row moves on with its columns. A column put in before the
title's first column or after its last merges the title anew, as AutoCAD 2021 merged it: its other cells emptied, of no
value, under its top line, and the line under the title its bottom line across every column, a green line under Qty
made the yellow of the line under Mark; one put in among its columns grows it as it is. A row put in before the title or
the header is of data, as AutoCAD 2021 keeps it: the record tells a row's kind by its place alone, so the table leaves
the title out (`280 1`) or the header (`281 1`), and the title or header moved down, read as data, takes as its own the looks
of its kind the data's are not (`170 5` and `140 6.0` for the title of AutoCAD's Standard, `170 5` for the header) and
the table's own of its kind; a row put in at or before the first row of data takes the table's own looks of the data
away from it, the rows of data given them as their own, as AutoCAD reads them from its first row of data. AutoCAD keeps a
kind with each row, the style of its row in the TABLECONTENT (1 the title, 2 the header, 3 data), and the library reads
it there: the title moved down by a row put in before it is the title again once that row is taken out. The first row put
into a table of one row is the header, wherever it goes, and the rest of data, as AutoCAD 2021 gave them: the table of
its title alone given two rows by `SetSize` or `InsertRows` has its header shown again (no `281`), whether it was left out
by the rows taken or by `IsHeaderSuppressed`, and a row put after a lone row of data is a header out of its place, read
as data with the header's `170 5`. `InsertRowsAndInherit` and
`InsertColumnsAndInherit` give the new cells the look of a row or column (the overrides with their flags, the turn, and
from R2007 on the kind, unit and format of the value, its empty value `93 5` as AutoCAD writes it, of the kind 512 for
a value of no kind), wherever its overrides stand, with the edges of its lines (172), every line of it, rows and columns
alike: the row put in under a cell whose top and left are red has its top and left red, so that the line between them
reads red, as AutoCAD 2021 wrote it; not its content. A block of merged cells along that row or column is merged along
each new one, the title's too, and a new row of the title's or the header's look is of that kind, its cells given the
looks of the kind as a title moved down is, unless it goes in the kind's place: from the title before the header, the
new row merged across and 6 high, read as data, and the header left out. A new cell of a column `InsertColumns` or `SetSize` puts in takes as its own the looks its row keeps
(alignment, text style, background, colour, text height): AutoCAD 2021 gave the new header cell of
a table of its own header of 9 `140 9.0` with its flag 0x20, and read a cell without them by its table style alone,
4.5; a new row's cells it gave none. A row of the title's or the header's kind out of its place gives its new cell the
looks of its kind, the title moved down `170 5` and `140 6.0`, and a row put in gives its new cell none. `DeleteRows`
and `DeleteColumns` take them out, a block of merged cells shrunk to what is left of it and unmerged where one cell is
left, each cell left keeping its own content, empty where `MergeCells` emptied it, as AutoCAD 2021 left them: the title
of two columns that loses its first keeps the empty text of its second, not Parts. Taking out the title or the header
leaves it out (`280 1` or `281 1`, the header first and shown where the title goes), and the table's own looks of a kind
of which no row is left go from its head with their flags, the header's background with the header, the data's text
style with the last row of data. The first edit of a table while its drawing is open, whatever it is (a value, a look, a
row, a fill, a move, or its own colour, layer, linetype, lineweight, linetype scale, visibility or transparency as an
entity), merges across the table first the rows AutoCAD 2021 merges when it loads a file, as `MergeCells`
merges them, and writes them so, as AutoCAD writes them whenever it saves the drawing: the first row, whatever its kind
and whether the title is shown, and any other row of the title's kind, where no block of merged cells reaches out of it
and every cell after the block of its first cell is a text cell of no value or of an empty text, whatever its colour,
background or alignment. The title Parts unmerged over an empty cell comes out merged across the table; one with X, a
space or nought beside it, a block, or a block merged down from it, stays as it is. Later edits merge nothing: a title
unmerged and given X beside Parts stays unmerged. A table read and not edited is written as it was read. The hidden
cell of a row of the title's kind moved down keeps the title's looks, `140 6.0` of AutoCAD's Standard, and is given them
where it held a height of its own, as AutoCAD 2021 kept them.

### Resizing

`SetSize(numRows, numCols)` cuts or carries on the table from its bottom and right, the new rows 4/3 of the text
height the table style gives the data and twice the margin above and below (9 for AutoCAD's Standard of 4.5 and 1.5,
6.333 for a style of 2.5, 12 for the table's own margin of 3, whatever the rows above) and the new columns as wide as
AutoCAD 2021 makes them, the text height of the first row whose new cell is merged
with none and the margins either side: the header's 4.5 and 1.5 of AutoCAD's Standard, 7.5, under a title merged across
the table, which grows over them; the title's 6, 9 wide, where it is unmerged; 1 wide in a table of one row under its
title merged across it, which has no row whose new cell is merged with none. No other block grows with columns or rows
put in after it: a title merged across some of the columns, or a column merged down to the last row, stays as it is, and
grows only by rows or columns put in among it, as AutoCAD 2021 left them; `CanInsertRow`, `CanInsertColumn`, `CanDeleteRows` and `CanDeleteColumns` say whether they can, one row and one
column at least kept. 16 000 rows go in or out in about 0.3 s on .NET 10, as one setter of a cell takes.

### Merging

`MergeCells(range)` gives the top left cell of the range the width and height of the block (175, 176) and `173 0`, and
marks the others merged (173 of 1), as AutoCAD 2021 writes them, a block within the range taken into it and one across
its edge refused, and `UnmergeCells` takes back each block the range meets, its top left cell given `175 1` and `176 1`,
as AutoCAD writes a cell of no block and each new cell of a row or column put in. The other cells of the block are
emptied as AutoCAD 2021 empties them: text cells of
no value, no block, their text style, height and colour of their own taken away unless they are the table's own looks
of their row's kind, their alignment and background kept, and in the TABLECONTENT of no content (`95 0`); the edges of
the block take the lines of its corners, its top and left the top left cell's, its right the top right cell's and its
bottom the bottom left cell's, so that a green line over Qty goes where the line over Mark is none, and the lines within
it stay. A block is read from those spans of its top left cell alone, as AutoCAD 2021 reads
it: AutoCAD writes that cell `173 0` and the others of the block `173 1`, and read a `173 1` in a cell of no spans as no
merge. The edges of a cell are its own overrides: `SetGridColor(row, column, gridLineType,
color)`, `SetGridLineWeight`, `SetGridVisibility` and `SetGridProperty` write its top, right, bottom and left colours
(69, 65, 66, 68), lineweights (279, 275, 276, 278) and showing (289, 285, 286, 288) with their flags, and the edge in
the cell's edge flags (172: 1 top, 2 right, 4 bottom, 8 left), without which AutoCAD 2021 reads no line of a cell
wherever its group stands, and
`SetGridProperty(range, ...)` the lines of a block, those inside on the cells either side of each line. A line between
two cells is one line, the edge of one cell: AutoCAD 2021 writes it as the top or left of the cell below or right of it,
and reads it from that cell where the cell holds the edge (its edge flag and one of the edge's overrides at least), else
from the bottom or right of the other where that one holds it, each property of the line from the edge that holds it.

### Grid lines

So `GetGridColor`, `GetGridLineWeight`, `GetGridVisibility`, `GetGridProperty` and `GridColor(row, col, edge)` read the
bottom of (1, 1) as the top of (2, 1) where (2, 1) holds its top, green there and red in (1, 1) reading green, and the
top of (2, 1) of a colour alone with the bottom of (1, 1) of a lineweight alone reading no lineweight; null where no edge
holds the line. The bottom or right of a cell set is set on the cell below or right of it too where that one holds the
line, and a top or left set that the cell does not hold takes first the properties the cell above or left of it gives
the line, so that a line set reads as set and keeps what was not set. The showing is 0 hidden and any other drawn, as
AutoCAD 2021 read it (its own DXF writes 1 for a line it hides, which it reads back drawn). `GridColor(gridlineType,
rowType)` and its setter `SetGridColor(color, borders, rows)`, with the lineweights and showing alike, read and write
the table's own lines of the title, the header or the data, with their flags in 94, 95 and 96, the groups of a code in
the order of their flags and after the table's colours of its rows: written in its head after its flags, where AutoCAD
2021 writes them and reads them onto each cell of the kind (it read those after the last cell as none), and after the
last cell only where the table holds its own there already; `HorizontalCellMargin`, `VerticalCellMargin`, `IsTitleSuppressed`,
`IsHeaderSuppressed` and `FlowDirection` read the table's own there too (`93 8` ... `96 0`, `40 3.0` before its
heights), and a table scaled scales the text heights it gives its rows there. A table of no margin of its own takes its
style's (the TABLESTYLE's 40 and 41), else that of AutoCAD's own Standard, 1.5 in a drawing of millimetres and 0.06 in
one of inches, as AutoCAD 2021 reads it, read from the style once and kept until the drawing is changed. `SetFormula(row, column, 0, "=A2+B2")`, from R2004 on,
adds a FIELD of AutoCAD's expressions (AcExpr, field code `\AcExpr (A2+B2)`) owned by the table, written as [`DxfField`](xref:GeometryHelper.Dxf.DxfField)
writes one with the class FIELD listed, and points the cell's 344 at it, after its turn (145) and before its 92, as
AutoCAD 2021 wrote one, its value left for AutoCAD to work out; a field
a cell lets go of, by a text, a value or a formula set, by its row or column taken out or by a copy or a fill over it,
is erased with its children where the table owns it and nothing else of the file points at it (a copy of the table,
which holds its original's fields, an XRECORD, another field), and taken out of the drawing's ACAD_FIELDLIST; a field
kept whose owner goes is given to a field that names it, else to the table. A copy of a table in its own drawing shares
the original's fields: erasing the original table leaves the copy's 344s naming nothing, a limit listed for later;

### Formulas

`GetFormula` and `HasFormula` read one back, or one AutoCAD wrote as a field of text holding it, from the cell's field in
the table's TABLECONTENT where the cell's 344 names no record, as AutoCAD 2021 wrote the formula of a linked cell; the
link taken away, such a formula is written in the cell's own FIELD, as AutoCAD read none there until the cell had one. `CopyFrom(source,
options, sourceRange, targetRange)` copies cells of a table of the same drawing, content, look and merges, growing the
table with `ExpandOrContractTable`, which never shrinks it (a copy past the last row of the header and data rows makes
the table of 3 rows one of 5), the content alone with `SkipFormat`, the look alone with `SkipContent`; a cell's field is
not copied, and the other options are refused. A cell emptied while the drawing is open (reset, given an empty text, put
in, or emptied by a merge) holds no content to give, as AutoCAD 2021 copied and filled from one: `CopyFrom`, and `Fill`
with `CopyContent`, leave the target's content as it was, Z staying Z, and a format copied gives it the cell's own look
alone (alignment, background, lines), the target keeping its text style, height, colour and kind of value; one read of no
value from the file is copied as none. With `GenerateSeries` a source of no value of either kind gives the target none of
no kind (`91 0`). A cell given a content by a copy or a fill is no longer one emptied in the session, so a clear writes
one given none as one read so (`93 5`, `90 512`). `Fill(fillRange, sourceRange, options)` fills the rows of the fill range
down the source's columns (or its columns along the source's rows with `Row`), as AutoCAD 2021 fills them: each cell the
source cell its place repeats from the fill range's first cell; above (or left of) the source and longer than it, back
from the fill range's last cell, the source's last next to it, 2.5 and 3.5 into rows 10 to 12 giving 3.5, 2.5 and 3.5,
in whole lengths of the source, the cells of one that would reach before the table's first row or column left as they
are (four sources from column 7 into columns 0 to 6 fill 3 to 6 alone); a cell a merge hides is left as it is too, the
title's second cell filled from Parts staying empty; `CopyContent` the content alone, winning over a series,
`CopyFormat` the look and kind alone (a cell of no value given one of none of that kind, one of a value of another kind
turned into it as `SetDataType` turns it, the text 12 under the look of 1.5 the number 12 shown 12.0000), both the cell
whole, into the header and the title as into rows of data;

### Filling a series

`GenerateSeries` carries the numbers on from the source's last, wherever the fill range stands, 10 and 20 to 30 and 40,
each rounded to the places of the source and the step, to 15 figures (0.1 and 0.2 to 0.3, shown 0.300000; 1, 2 and 4 to
5.5 and 7), and with `Reverse` back from the source's first, the fill range's last cell next to it, 10 and 20 to -20, -10
and 0. Options of none of the three change nothing, and a fill range that holds a cell of the source is refused, as
AutoCAD refuses it; a text is a number only before R2007, and dates are not carried on.
`SetFromStyle()` takes the table's and its cells' overrides away, as `ClearTableStyleOverrides(0)` does. What a file of
these versions has no group for is not there: several contents in one cell, and a line's style, linetype and the spacing
of a double line.

## Links to data

A range of a table's cells is linked to data as AutoCAD links one to a spreadsheet, from R2007 on; the library reads
such links, takes them away and puts them in. A [`DxfDataLink`](xref:GeometryHelper.Dxf.DxfDataLink) is a DATALINK of the drawing's
`DataLinkDictionary` (ACAD_DATALINK), read with its `DataAdapterId` (`AcExcel`), `ConnectionString`
(`parts.xlsx!Sheet1!A1:B2`), `Description`, `ToolTip`, `DataLinkOption`, `UpdateOption`, `IsValid` and
`GetUpdateStatus`; its `Name` set renames its entry. AutoCAD 2021 names a link in the TABLECONTENT it keeps for the table,
which the table's extension dictionary holds under ACAD_XREC_ROUNDTRIP through an XRECORD of
ACAD_ROUNDTRIP_2008_TABLE_ENTITY: the range's top left cell there has its 92 odd after its custom data, then the
DATALINK's handle (340), the range's rows (93) and columns (94) and a 96, and its state (90) the flag 4 of a linked cell;
the ACAD_TABLE's own cells name no link. `GetDataLink` and `GetDataLinkRange` read it from every cell of the range, and
`IsLinked` is true at its top left cell, whatever its state, at any other cell of it whose state in the TABLECONTENT
has the flag 4, and at any other AutoCAD lays out from the ACAD_TABLE: its cell of the TABLECONTENT holding no content,
or one whose checksum disagrees with the cell's text in the ACAD_TABLE. AutoCAD 2021 read its own range (1, 0) to (2, 1),
whose other cells held an empty value of the checksum of their text, linked at (1, 0) alone, each cell giving the range
and the link, and at (2, 1) too once its state was given the flag 4; the library's range of cells of no content it read
linked at all four; a text, a number or an empty value of an agreeing checksum not linked, of a disagreeing one linked. A
table the drawing's ACDB_RECOMPOSE_DATA does not list has no link of its TABLECONTENT, as AutoCAD read none of its own
73 with that record listing its style alone or nothing, and `SetDataLink` lists it; a cell of
the flag 4 in no range is linked where it holds a content (its 95 above nought), with no range and no link, as AutoCAD read
the title (0, 0) of Parts and a cell of data (2, 1) of no text given it beside a range over (1, 0) to (1, 1), and not
where it holds none, as AutoCAD read (0, 1) given it beside the range of (1, 0) to (2, 1); a cell of the TABLECONTENT is taken as the
table's of its row and column whatever rows the TABLECONTENT has, as AutoCAD
2021 read the link of (1, 0) of a table given a fourth row by a program that left its TABLECONTENT of three: AutoCAD's table
linked over (1, 0) to (2, 1), which it read back as (1, 0) alone, gives (1, 0) to (1, 0), and `link.GetTargets()` gives the
table. `SetDataLink(row, column, link)` and `SetDataLink(range, link)` put a link in as AutoCAD 2021's does, to a DATALINK
of the drawing: the range's top left cell of the TABLECONTENT given the flag 4, its 92 made odd and `340` link, `93` rows,
`94` columns, `96 1` after it, and the DATALINK tied to the table and its TABLECONTENT (`94 2`, `330 F6`, `330 187`). The
cells keep what they hold until AutoCAD updates the link; a range that meets a linked one is refused. A table of no
TABLECONTENT, as a program other than AutoCAD writes one, is given one from R2007 on, as AutoCAD 2021 gave the ezdxf table
of its 73 one: an XRECORD of ACAD_ROUNDTRIP_2008_TABLE_ENTITY under ACAD_XREC_ROUNDTRIP in the table's extension
dictionary, the TABLECONTENT of the table's columns and rows, each cell of no content and of the checksum of its text, its
merged cells and its style, the table and its style listed in ACDB_RECOMPOSE_DATA, without which AutoCAD reads no
TABLECONTENT at all, and the class TABLECONTENT; no TABLEGEOMETRY, as AutoCAD read its own 73 without one. AutoCAD 2021
read the library's link of (1, 0) so made, in R2018 and R2007, and a range of more cells linked at every cell, as the
library reads it. A drawing before R2007 holds no TABLECONTENT,
and its table is not linked. `RemoveDataLink` takes the links
away as AutoCAD 2021's does: the TABLECONTENT's cell made unlinked in place (92 even, the run out, the flag 4 cleared from
it and from every other cell of its range, as AutoCAD read a cell of the range left with it as linked with no range and
saved it so; a cell of the flag 4 outside every range is left as it is), the DATALINK left in the drawing, named or `Anonymous`, the table and what it owns taken out of the objects it lists
itself tied to (`94 2`, `330 F6`, `330 149` comes out `94 0`). Rows and columns put in or taken out are put in and taken
out of the TABLECONTENT too, as AutoCAD 2021's own `InsertRows`, `DeleteRows`, `InsertColumns`, `DeleteColumns` and
`SetSize` write them: AutoCAD reads the links, the style of each row (title, header or data) and the borders of the cells
from the TABLECONTENT, cell by cell, so a row put in above AutoCAD's link at (1, 0) left in a TABLECONTENT of the old rows
was read linked at the new row, and a row of data moved up into the header's place read as a header. Each new row is one
of data of empty cells (of the style of the row whose look it takes with `InsertRowsAndInherit`, each cell then of the
format of that row's cell, its margins and lines, and of an empty value of the flags 5 where that cell holds a content,
as AutoCAD 2021 wrote them), each new column of the
width given, the heights and widths written as reals (`40 10.0`) as AutoCAD writes them; a link moves with its cell and
grows or shrinks as the table's merged cells do, and goes with its cell, its DATALINK then no longer tied to the
TABLECONTENT, the table kept among its targets, as AutoCAD wrote it (`94 1`, `330 F6`); a field a cell taken out held
(AutoCAD's formula of a linked cell) goes out of the list after the rows (`92 1`, `360 192` comes out `92 0`) and is
erased where nothing left holds it. The merged cells the TABLECONTENT lists move with its rows, and `MergeCells` and `UnmergeCells` list them so,
though AutoCAD reads a table's merges, as its text, values and formats, from the ACAD_TABLE. A TABLECONTENT of fewer rows
than its table, as a program left one, gets no row past its last, and one past the table's last row or column names
none. The library reads no
spreadsheet: the cells keep what they hold, and `UpdateDataLink` is not there.

A link named in a cell of the ACAD_TABLE, the run 340, 93, 94, 96 straight after the cell's 92 made odd, is read as well:
it was the library's guess, which AutoCAD reads no link from, and which makes AutoCAD read the cells after it awry; every
cell of its range is `IsLinked`, as before. Only a text cell names a link so: a block cell's 340 names its block, whatever its 92. Rows and columns put in and taken out,
`SetSize` and `CopyFrom` move, grow and shrink such a range as they do a block of merged cells, `CopyFrom` bringing the
links wholly within its source unless `SkipDataLink` is given, and a cell filled from a linked one is not linked. A link
whose rows or columns are all taken out goes with them, and its DATALINK, with what it owns, where its `DataLinkOption` is
`Anonymous` and nothing else of the file points at it (another table, a copy of this one), the dictionary that holds it
aside.

A `DxfDataLink` is neither made in code nor has its groups set ([Checking against AutoCAD](../dxf-autocad-checks.md),
points 73 to 78). The DXF reference describes no DATALINK and ezdxf keeps one as it reads it; the groups read
are those LibreDWG and AutoCAD 2021 give, which have no 70, the count of the objects the link is tied to (94) with a 330
each, what it owns (360) and its custom data between `305 CUSTOMDATA`, `1 DATAMAP_BEGIN` and `309 DATAMAP_END`. An `UpdateOption` of 1 179 649, as LibreDWG's example gives
it, is SkipFormat (0x20000) and AllowSourceUpdate (0x100000), which make 1 179 648, and a 0x1 no flag of AutoCAD's
`UpdateOption` explains. The breaking of a table into parts (`BreakEnabled` and the rest) is not there either: its
groups are in no reference.

## See also

- [Hatches, leaders and dimensions](annotation.md).
- [Editing rules and what the model does not do](rules.md).
