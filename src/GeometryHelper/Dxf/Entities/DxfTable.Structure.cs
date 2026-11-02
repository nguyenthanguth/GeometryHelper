using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The structure of a table changed as AutoCAD's <c>Table</c> changes it: rows and columns put in and taken out, cells
    /// merged and unmerged, cells copied from another table and filled in a series, and the table given back to its style.
    /// </summary>
    public sealed partial class DxfTable
    {
        #region Rows and columns

        /// <summary>
        /// Gets whether rows can be put in before a row, as AutoCAD's <c>CanInsertRow</c>: before any of them or after the last
        /// (<see cref="NumRows"/>), where the table's record holds its cells, one height for each row and one width for each
        /// column. A row put in within a block of merged cells, or a range of cells linked to data, grows it, so neither stops
        /// it.
        /// </summary>
        /// <param name="index">The row the new ones go before, from nought; <see cref="NumRows"/> for after the last.</param>
        /// <returns>Whether they can.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public bool CanInsertRow(int index)
        {
            CheckEditable();
            Layout layout = Read();
            return Holds(layout) && index >= 0 && index <= layout.Rows;
        }

        /// <summary>
        /// Gets whether columns can be put in before a column, as AutoCAD's <c>CanInsertColumn</c>: before any of them or after
        /// the last (<see cref="NumColumns"/>), where the table's record holds its cells, heights and widths, as
        /// <see cref="CanInsertRow(int)"/>.
        /// </summary>
        /// <param name="index">The column the new ones go before, from nought; <see cref="NumColumns"/> for after the last.</param>
        /// <returns>Whether they can.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public bool CanInsertColumn(int index)
        {
            CheckEditable();
            Layout layout = Read();
            return Holds(layout) && index >= 0 && index <= layout.Columns;
        }

        /// <summary>
        /// Gets whether rows can be taken out, as AutoCAD's <c>CanDeleteRows</c>: one or more of the table's, one row at least
        /// left, where the table's record holds its cells, heights and widths; a block of merged cells a row is taken out of
        /// shrinks, so no merge stops it. The table of 3 rows can lose 2 of them, not 3.
        /// </summary>
        /// <param name="index">The first row taken out, from nought.</param>
        /// <param name="nCount">How many.</param>
        /// <returns>Whether they can.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public bool CanDeleteRows(int index, int nCount)
        {
            CheckEditable();
            Layout layout = Read();
            return Holds(layout) && index >= 0 && nCount >= 1 && (long)index + nCount <= layout.Rows && nCount < layout.Rows;
        }

        /// <summary>
        /// Gets whether columns can be taken out, as AutoCAD's <c>CanDeleteColumns</c>: one or more of the table's, one column at
        /// least left, as <see cref="CanDeleteRows(int, int)"/>.
        /// </summary>
        /// <param name="index">The first column taken out, from nought.</param>
        /// <param name="nCount">How many.</param>
        /// <returns>Whether they can.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public bool CanDeleteColumns(int index, int nCount)
        {
            CheckEditable();
            Layout layout = Read();
            return Holds(layout) && index >= 0 && nCount >= 1 && (long)index + nCount <= layout.Columns && nCount < layout.Columns;
        }

        /// <summary>
        /// Puts rows in, as AutoCAD's <c>InsertRows</c>: as many rows as asked, of the height given, before a row (after the
        /// last for <see cref="NumRows"/>), each of empty text cells that take their look from the table style; the count of rows
        /// (91) and their heights (141) are written with them. A block of merged cells the rows go into grows by them, one they
        /// go above moves down with its rows, and so does a range of cells linked to data. The rows go into the table's
        /// TABLECONTENT too, where it has one, as AutoCAD 2021's <c>InsertRows</c> writes them: rows of data of empty cells,
        /// a link the TABLECONTENT names moving and growing with its cells, as AutoCAD reads it there, and the merged ranges it
        /// lists alike. The record is written once, its cells anew, so that 16 000 rows go in in time linear in its groups; the
        /// block AutoCAD drew the table with is not drawn anew. The table of rows 10, 8 and 8 given two rows 6 high before its row 2 has rows 10, 8, 6, 6 and 8, its cell B-12
        /// now in row 4. Rows put in before the title or the header are of data, as AutoCAD 2021 keeps them: the record tells
        /// a row's kind by its place alone, so the table leaves the title out (<c>280 1</c>) where a row goes in before it and
        /// the header (<c>281 1</c>) where one goes in before it, and the cells of each, data now, take as their own the looks
        /// of their kind the data's are not, the title's <c>170 5</c> and <c>140 6.0</c> of AutoCAD's Standard, the header's
        /// <c>170 5</c>, and the table's own of their kind (D28d r1 to r7). Rows put in at or before the first row of data take
        /// the table's own looks of the data away from it, the rows of data that were given them as their own, as AutoCAD reads
        /// those looks from the first row of data (D28d d1 to d3). The first row put into a table of one row is the header and
        /// the rest data, as AutoCAD 2021 gave them, the header shown again where the table had left it out, and a header put
        /// after a lone row of data read as data in the header's alignment (D28e s8, s15, s17, e07 to e12). The table given a
        /// row before its title reads every row as data, Parts still 6 high and in the middle.
        /// </summary>
        /// <param name="row">The row the new ones go before, from nought; <see cref="NumRows"/> for after the last.</param>
        /// <param name="height">The height of each, a finite number above nought.</param>
        /// <param name="rows">How many, one or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is none of nought to <see cref="NumRows"/>, the count is below one, or the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void InsertRows(int row, double height, int rows)
        {
            Layout layout = Structured();
            CheckPlace(row, layout.Rows, nameof(row), "row");
            CheckCount(rows, nameof(rows));
            CheckCells((long)layout.Rows + rows, layout.Columns, nameof(rows));
            Guard.Positive(height, nameof(height), "A row's height has to be a finite number above nought.");
            InsertLines(layout, true, row, rows, Numbers.Exact(height + 0.0), -1);
        }

        /// <summary>
        /// Puts rows in that take the look of another, as AutoCAD's <c>InsertRowsAndInherit</c>: as <see cref="InsertRows"/>, the
        /// rows of the height of the row given, and each cell of them an empty text cell with the look the cell of that row in
        /// its column holds of its own: its overrides with their flags (177, or 91 from R2007 on), wherever they stand outside
        /// its value, and the edges of its lines (172), its turn (145), and from R2007 on the kind (90, 512 for a value of no
        /// kind), unit (94) and format (300) of its value, the value the empty one of the flags 5 AutoCAD 2021 wrote; not its
        /// content. The blocks of merged cells across that row are merged across each new row alike, the title's too (D28d m1,
        /// m3, i1). The new rows are of that row's kind, as AutoCAD 2021 keeps them: of the title's or the header's where it is
        /// the title or the header, each cell given as its own the looks of that kind the data's are not, the title's
        /// <c>170 5</c> and <c>140 6.0</c> of AutoCAD's Standard, and the table's own of that kind, unless it is put in the
        /// place of that kind; a title or header moved out of its place leaves it out (<c>280 1</c>, <c>281 1</c>) and takes
        /// those looks alike, as <see cref="InsertRows"/> writes them (D28d i1 to i9). The table given a row of its title's
        /// look after its last has a fourth row merged across, 6 high, read as data. In the TABLECONTENT the new rows take that
        /// row's style, a row of data from a row of data, and each cell the format of that row's cell in its column as it
        /// stands, its margins and lines, and an empty value of the flags 5 with the format of that cell's content where it
        /// holds one, as AutoCAD 2021 wrote them (D28c i1 to i7).
        /// </summary>
        /// <param name="index">The row the new ones go before, from nought; <see cref="NumRows"/> for after the last.</param>
        /// <param name="inheritFrom">The row whose look they take, one of the table's as it stands before they go in.</param>
        /// <param name="numRows">How many, one or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the place is none of nought to <see cref="NumRows"/>, the row whose look they take is not one of the table's, or the count is below one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void InsertRowsAndInherit(int index, int inheritFrom, int numRows)
        {
            Layout layout = Structured();
            CheckPlace(index, layout.Rows, nameof(index), "row");
            CheckIndex(inheritFrom, layout.Rows, nameof(inheritFrom), "row");
            CheckCount(numRows, nameof(numRows));
            CheckCells((long)layout.Rows + numRows, layout.Columns, nameof(numRows));
            InsertLines(layout, true, index, numRows, layout.Groups.List[layout.RowAt[inheritFrom]].Value, inheritFrom);
        }

        /// <summary>
        /// Takes rows out, as AutoCAD's <c>DeleteRows</c>: their cells, their heights (141) and their count (91). A block of
        /// merged cells shrinks to the rows of it left, and is unmerged where one cell of it is left; where its top row goes,
        /// the first cell of it left keeps its own content, empty where <see cref="MergeCells"/> emptied it, as AutoCAD 2021
        /// kept it (D28e k1, k4), where the library moved the top left cell's there. A block below the rows moves up. The
        /// rows left keep their kinds, as AutoCAD keeps a kind with each row: the title or the header taken out is left out
        /// (<c>280 1</c>, <c>281 1</c> and their flags in 93), the header first and shown where the title goes, and a title or
        /// header moved back into its place by the rows taken is shown again, its 280 or 281 taken out (D28e x1 to x17); the
        /// table's own looks of a kind of which no row is left go from its head with their flags, as AutoCAD took the
        /// header's background and height out with the header (x8). A range of cells linked to data shrinks and moves alike, its link named by its new top
        /// left cell, and goes where none of it is left, its DATALINK erased as <see cref="RemoveDataLink()"/> erases one: where it
        /// is anonymous and nothing else of the file points at it. The rows are taken out of the table's TABLECONTENT too, as
        /// AutoCAD 2021's <c>DeleteRows</c> takes them out: a link the TABLECONTENT names goes with its cell, its DATALINK kept and
        /// no longer tied to the TABLECONTENT (<c>94 1</c>, <c>330 F6</c>). The record is written once, its cells anew; the block
        /// AutoCAD drew the table with is not drawn anew. The table of rows 10, 8 and 8 that loses its row 1 has rows 10 and 8, its cell
        /// B-12 now in row 1. The rows AutoCAD 2021 merges across when it loads a file are merged first, as
        /// <see cref="MergeCells"/> merges them, and written so, as AutoCAD writes them after its own <c>DeleteRows</c>: the
        /// first row, whatever its kind and whether the title is shown, and any other row of the title's kind, where no block
        /// of merged cells reaches out of it and each cell after the block of its first cell is a text cell of no value or of
        /// an empty text (D33b w01 to w38, m01 to m27). The title Parts left unmerged over an empty cell is written merged
        /// across the two columns; one with X, a space or nought beside it, or a block, is left as it is.
        /// </summary>
        /// <param name="row">The first row taken out, from nought.</param>
        /// <param name="rows">How many, one or more, one row at least left.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the rows are not of the table's, or none or all of them are taken out.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void DeleteRows(int row, int rows)
        {
            Layout layout = Structured();
            CheckTaken(row, rows, layout.Rows, nameof(row), nameof(rows), "row");
            DeleteLines(MergedAsLoaded(layout), true, row, rows);
        }

        /// <summary>
        /// Puts columns in, as AutoCAD's <c>InsertColumns</c>: as many columns as asked, of the width given, before a column
        /// (after the last for <see cref="NumColumns"/>), as <see cref="InsertRows"/> puts rows in, the count (92) and the
        /// widths (142) written with them; the title merged across every column grows by those put in, first, last or among
        /// them, its text kept in its first cell, as AutoCAD 2021's <c>InsertColumns</c> grew it, where a merge of another row
        /// moves on with its columns. Columns put in before the title's first column or after its last merge it anew, as AutoCAD
        /// 2021 merged it: its other cells emptied, of no value, under the title's top line, and the line under the title its
        /// bottom line across every column, the green line under Qty where the line under Mark is yellow made yellow; a column
        /// put in among its columns grows it as it is (D28d g1 to g4). Each new cell takes as its own the looks the table gives of its own to its kind of row
        /// (its alignment, text style, background, colour and text height), as AutoCAD 2021 gave the new header cell of a table
        /// of its own header of 9 <c>140 9.0</c> with the flag 0x20, and reads a cell without them by its table style alone
        /// (D28c o3). The table of columns 40 and 30 given one 25 wide before its column 1 has columns 40, 25 and 30, its title
        /// merged across the three; given one before its column 0, Parts still in (0, 0) across the three.
        /// </summary>
        /// <param name="col">The column the new ones go before, from nought; <see cref="NumColumns"/> for after the last.</param>
        /// <param name="width">The width of each, a finite number above nought.</param>
        /// <param name="columns">How many, one or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column is none of nought to <see cref="NumColumns"/>, the count is below one, or the width is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void InsertColumns(int col, double width, int columns)
        {
            Layout layout = Structured();
            CheckPlace(col, layout.Columns, nameof(col), "column");
            CheckCount(columns, nameof(columns));
            CheckCells(layout.Rows, (long)layout.Columns + columns, nameof(columns));
            Guard.Positive(width, nameof(width), "A column's width has to be a finite number above nought.");
            InsertLines(layout, false, col, columns, Numbers.Exact(width + 0.0), -1);
        }

        /// <summary>
        /// Puts columns in that take the look of another, as AutoCAD's <c>InsertColumnsAndInherit</c>: as
        /// <see cref="InsertRowsAndInherit"/> puts rows in, the columns of the width of the column given, each cell with every
        /// line of the cell it takes the look of, and the blocks of merged cells down that column merged down each new one
        /// (D28d m2). The title is merged anew over columns put in at its edge, as <see cref="InsertColumns"/> merges it, before
        /// the new cells take their look, so that a new cell of the title is of no value and one under it takes the title's
        /// bottom line, as AutoCAD 2021 wrote them (D28d g5, b1, D28c i5).
        /// </summary>
        /// <param name="col">The column the new ones go before, from nought; <see cref="NumColumns"/> for after the last.</param>
        /// <param name="inheritFrom">The column whose look they take, one of the table's as it stands before they go in.</param>
        /// <param name="numCols">How many, one or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the place is none of nought to <see cref="NumColumns"/>, the column whose look they take is not one of the table's, or the count is below one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void InsertColumnsAndInherit(int col, int inheritFrom, int numCols)
        {
            Layout layout = Structured();
            CheckPlace(col, layout.Columns, nameof(col), "column");
            CheckIndex(inheritFrom, layout.Columns, nameof(inheritFrom), "column");
            CheckCount(numCols, nameof(numCols));
            CheckCells(layout.Rows, (long)layout.Columns + numCols, nameof(numCols));
            InsertLines(layout, false, col, numCols, layout.Groups.List[layout.ColumnAt[inheritFrom]].Value, inheritFrom);
        }

        /// <summary>
        /// Takes columns out, as AutoCAD's <c>DeleteColumns</c>: their cells, their widths (142) and their count (92), as
        /// <see cref="DeleteRows"/> takes rows out; the title merged across the columns of a table of two that loses the first
        /// is unmerged, the one cell left of its own empty text, as AutoCAD 2021 left it (D28e x5), Parts gone with the first.
        /// The rows AutoCAD merges across when it loads a file are merged first, as <see cref="DeleteRows"/> merges them: the
        /// title of three columns left unmerged over empty cells, losing its middle column, is written merged across the two
        /// left (D33b m05, m12).
        /// </summary>
        /// <param name="col">The first column taken out, from nought.</param>
        /// <param name="columns">How many, one or more, one column at least left.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the columns are not of the table's, or none or all of them are taken out.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void DeleteColumns(int col, int columns)
        {
            Layout layout = Structured();
            CheckTaken(col, columns, layout.Columns, nameof(col), nameof(columns), "column");
            DeleteLines(MergedAsLoaded(layout), false, col, columns);
        }

        /// <summary>
        /// Gives the table a number of rows and of columns, as AutoCAD's <c>SetSize</c>: rows and columns taken out from the
        /// bottom and the right, or put in there, each new row 4/3 of the text height the table style gives the data and twice
        /// the margin above and below a cell's content (<see cref="VerticalCellMargin"/>), 9 for AutoCAD's Standard of 4.5 and
        /// 1.5 whatever the rows above, as AutoCAD 2021 made them (D28d h1 to h9), the first row put into a table of one row
        /// the header, shown again where the table had left it out, as <see cref="InsertRows"/> puts it in (D28e s4 to s6),
        /// rows taken out leaving the kinds of those left as <see cref="DeleteRows"/> does, and each new column as wide as AutoCAD
        /// 2021 made one, the text height of its first row whose new cell is merged with none (the header, where the title is
        /// merged across the table) and the margins either side of a cell's content (7.5 for AutoCAD's Standard, 4.5 and 1.5;
        /// 9 where the title is unmerged, its 6; 1 in a table of one row, its title merged across it, D28c w1), of empty text
        /// cells, those of the new columns in the rows the table had given its own looks of their kind of row, as
        /// <see cref="InsertColumns"/> gives them; the title merged across every column is merged anew over the new ones, as
        /// <see cref="InsertColumns"/> merges it (D28d g4), and no other
        /// block grows, a column merged down to the last row nor a title merged across some of the columns (D28c w3, m1); blocks
        /// of merged cells and ranges linked to data cut where they reach past the new size, in the TABLECONTENT too, as
        /// <see cref="InsertRows"/> and <see cref="DeleteRows"/> write it. Nothing is written where the table is of that size.
        /// The table of 3 rows and 2 columns given 5 and 2 has two more rows 9 high; given 3 and 3, a column 7.5 wide, its title
        /// across the three.
        /// </summary>
        /// <param name="numRows">The number of rows, one or more.</param>
        /// <param name="numCols">The number of columns, one or more.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is below one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void SetSize(int numRows, int numCols)
        {
            CheckCount(numRows, nameof(numRows));
            CheckCount(numCols, nameof(numCols));
            CheckCells(numRows, numCols, nameof(numRows));
            Layout layout = Structured();

            if (numRows == layout.Rows && numCols == layout.Columns)
            {
                return;
            }

            int rows = layout.Rows;
            int columns = layout.Columns;
            List<string> heights = Sizes(layout, layout.RowAt, numRows);
            List<string> widths = Sizes(layout, layout.ColumnAt, numCols);
            List<DxfCellRange> found = Blocks(layout);
            bool title = numCols > columns && TitleAcross(found, columns);
            string width = numCols > columns ? NewColumnWidth(layout, title) : null;
            string height = numRows > rows ? NewRowHeight() : null;

            for (int c = columns; width != null && c < numCols; c++)
            {
                widths[c] = width;
            }

            for (int r = rows; height != null && r < numRows; r++)
            {
                heights[r] = height;
            }

            bool valued = Item.File.Supports(1021);
            var cells = new List<KeyValuePair<int, string>>[numRows * numCols];
            List<KeyValuePair<int, string>>[] looks = numCols > columns ? RowKindLooks(layout) : null;
            TitleMerge merge = title ? MergeTitleAgain(layout, valued) : null;

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    List<KeyValuePair<int, string>> cell = r < rows && c < columns ? merge?.Cells[r * columns + c] ?? Slice(layout, r * columns + c)
                        : r < rows ? NewCell(valued, looks, r) : NewCell(valued);
                    cells[r * numCols + c] = merge != null && r < rows && c >= columns ? merge.NewCell(cell, r, valued) : cell;
                }
            }

            var blocks = new List<DxfCellRange>();

            foreach (DxfCellRange b in found)
            {
                if (b.TopRow < numRows && b.LeftColumn < numCols)
                {
                    AddBlock(blocks, b.TopRow, b.LeftColumn, Math.Min(b.BottomRow, numRows - 1), title && IsTitle(b, columns) ? numCols - 1 : Math.Min(b.RightColumn, numCols - 1));
                }
            }

            RowKinds kinds = numRows > rows ? Kinds(layout, cells, numCols, rows, numRows - rows, -1, valued, false) : numRows < rows ? DeletedKinds(layout, numRows, rows - numRows) : null;
            Rebuild(layout, numRows, numCols, heights, widths, cells, blocks, LinksCut(layout, numRows, numCols));

            if (numRows != rows)
            {
                if (numRows < rows)
                {
                    ContentDeleted(true, numRows, rows - numRows);
                }
                else
                {
                    ContentInserted(true, rows, numRows - rows, heights[rows], -1, 0, rows == 1);
                }
            }

            if (numCols != columns)
            {
                if (numCols < columns)
                {
                    ContentDeleted(false, numCols, columns - numCols);
                }
                else
                {
                    ContentInserted(false, columns, numCols - columns, widths[columns], -1, title ? columns : 0);
                }
            }

            kinds?.WriteHead(this);
        }

        /// <summary>Puts rows (or columns) in at a place, new or taking the look of a row (or column), and writes the record anew.</summary>
        private void InsertLines(Layout layout, bool rowsOf, int at, int count, string size, int inheritFrom)
        {
            int rows = layout.Rows;
            int columns = layout.Columns;
            int newRows = rowsOf ? rows + count : rows;
            int newColumns = rowsOf ? columns : columns + count;
            bool valued = Item.File.Supports(1021);
            var heights = new List<string>();
            var widths = new List<string>();

            foreach (int place in layout.RowAt)
            {
                heights.Add(layout.Groups.List[place].Value);
            }

            foreach (int place in layout.ColumnAt)
            {
                widths.Add(layout.Groups.List[place].Value);
            }

            (rowsOf ? heights : widths).InsertRange(at, Repeat(size, count));
            var cells = new List<KeyValuePair<int, string>>[newRows * newColumns];
            List<KeyValuePair<int, string>>[] looks = !rowsOf && inheritFrom < 0 ? RowKindLooks(layout) : null;
            List<DxfCellRange> found = Blocks(layout);
            bool title = !rowsOf && TitleAcross(found, columns);

            // Columns put in at the title's edge merge it anew, as AutoCAD 2021 merged it (D28d g1 to g5): its other cells and
            // the row under it changed before any new cell takes the look of one of them.
            TitleMerge merge = title && (at == 0 || at == columns) ? MergeTitleAgain(layout, valued) : null;

            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newColumns; c++)
                {
                    int line = rowsOf ? r : c;
                    List<KeyValuePair<int, string>> cell;

                    if (line < at || line >= at + count)
                    {
                        int oldRow = rowsOf && r >= at + count ? r - count : r;
                        int oldColumn = !rowsOf && c >= at + count ? c - count : c;
                        cell = merge?.Cells[oldRow * columns + oldColumn] ?? Slice(layout, oldRow * columns + oldColumn);
                    }
                    else if (inheritFrom >= 0)
                    {
                        int source = rowsOf ? inheritFrom * columns + c : r * columns + inheritFrom;
                        List<KeyValuePair<int, string>> changed = merge?.Cells[source];

                        // A cell of the title merged anew is of no value, as is the new one that takes its look (D28d g5, b1).
                        cell = changed != null && r == 0 ? new List<KeyValuePair<int, string>>(changed)
                            : Inherited(changed != null ? View(changed) : layout.CellAt(source / columns, source % columns), valued);
                    }
                    else
                    {
                        cell = merge != null ? merge.NewCell(NewCell(valued, looks, r), r, valued) : NewCell(valued, looks, r);
                    }

                    cells[r * newColumns + c] = cell;
                }
            }

            var blocks = new List<DxfCellRange>();

            foreach (DxfCellRange b in found)
            {
                // The title merged across the table grows by the columns put in wherever they go, its text kept first.
                if (title && IsTitle(b, columns))
                {
                    AddBlock(blocks, 0, 0, 0, newColumns - 1);
                    continue;
                }

                int first = rowsOf ? b.TopRow : b.LeftColumn;
                int last = rowsOf ? b.BottomRow : b.RightColumn;
                int newFirst = at <= first ? first + count : first;
                int newLast = at <= last ? last + count : last;
                AddBlock(blocks, rowsOf ? newFirst : b.TopRow, rowsOf ? b.LeftColumn : newFirst, rowsOf ? newLast : b.BottomRow, rowsOf ? b.RightColumn : newLast);
            }

            if (title && at == 0)
            {
                List<KeyValuePair<int, string>> put = cells[0];
                cells[0] = cells[count];
                cells[count] = put;

                // The title merged anew, the new cells after its text are of no value under its top line, the one next to it
                // left of the line right of its text, as AutoCAD 2021 wrote them (D28d g2, b3).
                for (int k = 1; merge != null && inheritFrom >= 0 && k <= count; k++)
                {
                    cells[k] = merge.NewCell(NewCell(valued), 0, valued);
                }

                if (merge?.RightOfCorner != null)
                {
                    cells[count] = WithLine(cells[count], 3, merge.RightOfCorner, valued);
                }
            }

            List<DxfCellRange> copied = inheritFrom >= 0 ? MergesAlong(found, rowsOf, inheritFrom, at, count, blocks) : null;
            RowKinds kinds = rowsOf ? Kinds(layout, cells, newColumns, at, count, inheritFrom, valued) : null;

            Rebuild(layout, newRows, newColumns, heights, widths, cells, blocks, LinksInserted(layout, rowsOf, at, count));
            ContentInserted(rowsOf, at, count, size, inheritFrom, title ? columns : 0, rowsOf && inheritFrom < 0 && rows == 1);

            foreach (DxfCellRange block in copied ?? new List<DxfCellRange>())
            {
                ContentMerged(block, true);
            }

            kinds?.WriteHead(this);
        }

        /// <summary>
        /// Whether the table's title is merged across all its columns, one of its blocks given over its first row: a title AutoCAD 2021 grew
        /// by the columns its <c>InsertColumns</c> put in first, last or among them, and its <c>SetSize</c> put in, where a merge
        /// of another row it moved on (D28b c1 to c8, s1, s2); a title unmerged it left so.
        /// </summary>
        private bool TitleAcross(List<DxfCellRange> blocks, int columns) => blocks.Exists(b => IsTitle(b, columns)) && !IsTitleSuppressed;

        private static bool IsTitle(DxfCellRange b, int columns) => b.TopRow == 0 && b.BottomRow == 0 && b.LeftColumn == 0 && b.RightColumn == columns - 1;

        /// <summary>
        /// The table with the rows merged across that AutoCAD 2021 merges when it loads a file, merged as
        /// <see cref="MergeCells"/> merges them, so that rows and columns are taken out of what AutoCAD holds in memory, which
        /// it writes out after an edit (D33b w01 to w38, m01 to m27): the first row, whatever its kind and whether the title is
        /// shown or not, and any other row of the title's kind (TABLEROW 90 1), where the table has two columns or more, no
        /// block of merged cells reaches from the row into another, the row is not merged across already, and each cell of
        /// it after the block of its first cell is a text cell of no value or of an empty text, its alignment, colour or
        /// background whatever they are. A cell of a text, a space or nought among them, or a block cell, leaves the row as it
        /// is. The table as it stands where no row merges.
        /// </summary>
        private Layout MergedAsLoaded(Layout layout)
        {
            int columns = layout.Columns;

            if (columns < 2)
            {
                return layout;
            }

            List<DxfCellRange> found = Blocks(layout);
            int[] kinds = null;
            var merged = new List<int>();

            // The kinds of the rows, read from the TABLECONTENT, are read only for a row past the first that would merge.
            for (int r = 0; r < layout.Rows; r++)
            {
                if (!MergesOnLoad(layout, found, r))
                {
                    continue;
                }

                kinds = r > 0 ? kinds ?? KeptKinds(layout) : kinds;

                if (r == 0 || kinds[r] == 0)
                {
                    merged.Add(r);
                }
            }

            foreach (int r in merged)
            {
                MergeCells(new DxfCellRange(r, 0, r, columns - 1));
            }

            return merged.Count == 0 ? layout : Structured();
        }

        /// <summary>Whether AutoCAD 2021 merges a row across when it loads the table, as <see cref="MergedAsLoaded"/> says.</summary>
        private static bool MergesOnLoad(Layout layout, List<DxfCellRange> blocks, int row)
        {
            int columns = layout.Columns;
            int first = 1;

            foreach (DxfCellRange b in blocks)
            {
                if (b.TopRow > row || b.BottomRow < row)
                {
                    continue;
                }

                if (b.TopRow != row || b.BottomRow != row || (b.LeftColumn == 0 && b.RightColumn == columns - 1))
                {
                    return false;
                }

                first = b.LeftColumn == 0 ? b.RightColumn + 1 : first;
            }

            for (int c = first; c < columns; c++)
            {
                if (!EmptyOnLoad(layout.CellAt(row, c)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a cell is empty as AutoCAD 2021 loads a row: a text cell of no value or of an empty text, of no field; a block cell, or one of a whole number or a number, is not.</summary>
        private static bool EmptyOnLoad(CellView cell)
        {
            if (cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell || cell.Head(344) != null)
            {
                return false;
            }

            if (cell.ValueFrom < 0)
            {
                return cell.Text().Length == 0;
            }

            int flags = int.TryParse(cell.InValue(93)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : 6;

            if ((flags & 1) != 0)
            {
                return true;
            }

            string kind = (cell.InValue(90) ?? "4").Trim();
            return (kind == "4" || kind == "512" || kind == "0") && cell.Text().Length == 0;
        }

        /// <summary>Takes rows (or columns) out from a place, and writes the record anew.</summary>
        private void DeleteLines(Layout layout, bool rowsOf, int at, int count)
        {
            int rows = layout.Rows;
            int columns = layout.Columns;
            int newRows = rowsOf ? rows - count : rows;
            int newColumns = rowsOf ? columns : columns - count;
            var heights = new List<string>();
            var widths = new List<string>();

            for (int r = 0; r < rows; r++)
            {
                if (!rowsOf || r < at || r >= at + count)
                {
                    heights.Add(layout.Groups.List[layout.RowAt[r]].Value);
                }
            }

            for (int c = 0; c < columns; c++)
            {
                if (rowsOf || c < at || c >= at + count)
                {
                    widths.Add(layout.Groups.List[layout.ColumnAt[c]].Value);
                }
            }

            var cells = new List<KeyValuePair<int, string>>[newRows * newColumns];

            for (int r = 0; r < newRows; r++)
            {
                for (int c = 0; c < newColumns; c++)
                {
                    int oldRow = rowsOf && r >= at ? r + count : r;
                    int oldColumn = !rowsOf && c >= at ? c + count : c;
                    cells[r * newColumns + c] = Slice(layout, oldRow * columns + oldColumn);
                }
            }

            var blocks = new List<DxfCellRange>();

            foreach (DxfCellRange b in Blocks(layout))
            {
                int first = rowsOf ? b.TopRow : b.LeftColumn;
                int last = rowsOf ? b.BottomRow : b.RightColumn;

                // The lines of the block left, numbered as they are after the others are taken out.
                int newFirst = first < at ? first : first >= at + count ? first - count : at;
                int newLast = last < at ? last : last >= at + count ? last - count : at - 1;

                if (newLast < newFirst)
                {
                    continue;
                }

                // The block's top left cell taken out, the first cell of it left keeps its own content, empty where the block
                // was merged as AutoCAD merges, as AutoCAD 2021 left it (D28e x5, k1, k3, k4).
                AddBlock(blocks, rowsOf ? newFirst : b.TopRow, rowsOf ? b.LeftColumn : newFirst, rowsOf ? newLast : b.BottomRow, rowsOf ? b.RightColumn : newLast);
            }

            RowKinds kinds = rowsOf ? DeletedKinds(layout, at, count) : null;
            Rebuild(layout, newRows, newColumns, heights, widths, cells, blocks, LinksDeleted(layout, rowsOf, at, count));
            ContentDeleted(rowsOf, at, count);
            kinds?.WriteHead(this);
        }

        #endregion

        #region Merging

        /// <summary>
        /// Merges a block of cells into one, as AutoCAD's <c>MergeCells</c>: each cell of it marked merged (173 of 1), its top
        /// left cell given the block's width and height in cells (175, 176), the others 0 and 0, as <see cref="GetMergeRange"/>
        /// reads them; the content of its top left cell is the block's, and the others are emptied as AutoCAD 2021 empties them
        /// (<see cref="EmptyMerged"/>), where the library kept them unseen. A block of merged cells within the range becomes part
        /// of it. The title of the table of 3 rows and 2 columns, merged with
        /// the row below it, (0, 0) to (1, 1), gives that range from each of its four cells. The table's TABLECONTENT lists the
        /// range among its merged ranges, those it meets taken out, as AutoCAD 2021 lists a merge, though AutoCAD reads a table's
        /// merges from its ACAD_TABLE. Nothing is written for a range of one cell, or one merged so already; the block AutoCAD
        /// drew the table with is not drawn anew.
        /// </summary>
        /// <param name="range">The block of cells, within the table.</param>
        /// <exception cref="ArgumentNullException">Thrown when the range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range reaches past the table's rows or columns.</exception>
        /// <exception cref="ArgumentException">Thrown when a block of merged cells reaches across the range's edge, partly in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void MergeCells(DxfCellRange range)
        {
            Layout layout = Structured();
            CheckRange(range, layout, nameof(range));

            if (range.TopRow == range.BottomRow && range.LeftColumn == range.RightColumn)
            {
                return;
            }

            var blocks = new List<DxfCellRange>();
            bool changed = false;

            foreach (DxfCellRange b in Blocks(layout))
            {
                if (!Meets(b, range))
                {
                    blocks.Add(b);
                }
                else if (Within(b, range))
                {
                    changed |= !b.Equals(range);
                }
                else
                {
                    throw new ArgumentException("The block of merged cells " + b + " reaches across the edge of the range " + range + ": unmerge it first, or merge a range that holds it whole.", nameof(range));
                }
            }

            changed |= !Blocks(layout).Contains(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn));
            AddBlock(blocks, range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn);

            if (changed)
            {
                List<KeyValuePair<int, string>>[] cells = AllSlices(layout);
                EmptyMerged(layout, cells, range, Item.File.Supports(1021));
                Rebuild(layout, layout.Rows, layout.Columns, null, null, cells, blocks, Links(layout));
                ContentMerged(range, true, true);
            }
        }

        /// <summary>
        /// Empties the cells of a block merged but its top left, as AutoCAD 2021's <c>MergeCells</c> emptied them (D28e p8, p9,
        /// q1 to q7, m2 to m5): each a text cell of no value (<c>93 7</c>, <c>90 0</c>, the flag 0x40000 taken out), its block
        /// and attribute values taken out, and its text style, text height and colour of its own (7, 140, 64), which AutoCAD
        /// holds with the content, taken out where they are not the table's own looks of its row's kind; its alignment and
        /// background kept, as AutoCAD kept Mark's <c>170 5</c> and took its <c>7 GH_TXT</c> and <c>140 3.5</c> away, and
        /// kept the looks of a table of its own data of GH_TXT, 9, red and top left. The edges of the block are those its
        /// corners give: its top and left the top left cell's, its right the top right cell's, its bottom the bottom left
        /// cell's, each read as AutoCAD reads a line between two cells, given to each cell along it and taken from the cell
        /// beyond, so that a green line over Qty went where the line over Mark was none and red where it was red; the lines
        /// within the block are kept. The cells given are changed in place.
        /// </summary>
        private void EmptyMerged(Layout layout, List<KeyValuePair<int, string>>[] cells, DxfCellRange range, bool valued)
        {
            int rows = layout.Rows;
            int columns = layout.Columns;
            int top = range.TopRow;
            int left = range.LeftColumn;
            int bottom = range.BottomRow;
            int right = range.RightColumn;
            Func<int, int, int, CellLine> line = (row, column, edge) => LineOf(View(cells[row * columns + column]), edge);
            CellLine over = line(top, left, 0) ?? (top > 0 ? line(top - 1, left, 2) : null);
            CellLine before = line(top, left, 3) ?? (left > 0 ? line(top, left - 1, 1) : null);
            CellLine after = (right + 1 < columns ? line(top, right + 1, 3) : null) ?? line(top, right, 1);
            CellLine under = (bottom + 1 < rows ? line(bottom + 1, left, 0) : null) ?? line(bottom, left, 2);
            List<KeyValuePair<int, string>>[] looks = RowKindLooks(layout);

            for (int row = top; row <= bottom; row++)
            {
                for (int column = left; column <= right; column++)
                {
                    if (row != top || column != left)
                    {
                        cells[row * columns + column] = Emptied(cells[row * columns + column], looks?[row], valued);
                    }
                }
            }

            Action<int, int, int, CellLine> give = (row, column, edge, given) => cells[row * columns + column] = WithLine(cells[row * columns + column], edge, given, valued);

            for (int column = left + 1; column <= right; column++)
            {
                give(top, column, 0, over);

                if (over == null && top > 0)
                {
                    give(top - 1, column, 2, null);
                }

                give(bottom, column, 2, under);

                if (bottom + 1 < rows)
                {
                    give(bottom + 1, column, 0, null);
                }
            }

            for (int row = top + 1; row <= bottom; row++)
            {
                give(row, left, 3, before);

                if (before == null && left > 0)
                {
                    give(row, left - 1, 1, null);
                }

                give(row, right, 1, after);

                if (right + 1 < columns)
                {
                    give(row, right + 1, 3, null);
                }
            }
        }

        /// <summary>A cell of a block merged emptied, as <see cref="EmptyMerged"/> says: of no value, no block, and of no text style, height or colour of its own but those of the table's own looks of its row's kind given.</summary>
        private static List<KeyValuePair<int, string>> Emptied(List<KeyValuePair<int, string>> groups, List<KeyValuePair<int, string>> looks, bool valued)
        {
            CellView cell = View(groups);
            int mask = 0;

            foreach (int code in new[] { 7, 140, 64 })
            {
                int bit = Layout.CellOverrides[code];
                string own = cell.Override(code, bit);
                int at = looks == null ? -1 : looks.FindIndex(g => g.Key == code);
                mask |= own != null && (at < 0 || !SameLook(own, looks[at].Value)) ? bit : 0;
            }

            if (mask != 0)
            {
                cell.RemoveOverrides(mask);
                cell = View(cell.Groups.List);
            }

            if (cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell)
            {
                int end = cell.ValueFrom >= 0 ? cell.ValueFrom : cell.OverridesFrom;

                for (int i = end - 1; i > cell.From; i--)
                {
                    int code = cell.Groups.List[i].Key;

                    if (code == 340 || code == 144 || code == 179 || code == 331 || code == 300)
                    {
                        cell.Groups.RemoveAt(i);
                    }
                }

                cell.Groups.List[cell.From] = new KeyValuePair<int, string>(171, "1");
                cell = View(cell.Groups.List);
            }

            if (!cell.HoldsNoValue())
            {
                cell.SetNoValue();
            }

            return cell.Groups.List;
        }

        /// <summary>
        /// Unmerges the blocks of merged cells a range meets, as AutoCAD's <c>UnmergeCells</c>: each cell of them marked
        /// unmerged (173, 175 and 176 of 0), each showing its own content again, and taken out of the merged ranges the table's
        /// TABLECONTENT lists; nothing where the range meets no block. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="range">The cells, within the table.</param>
        /// <exception cref="ArgumentNullException">Thrown when the range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range reaches past the table's rows or columns.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void UnmergeCells(DxfCellRange range)
        {
            Layout layout = Structured();
            CheckRange(range, layout, nameof(range));
            var blocks = new List<DxfCellRange>();
            bool changed = false;

            foreach (DxfCellRange b in Blocks(layout))
            {
                if (Meets(b, range))
                {
                    changed = true;
                }
                else
                {
                    blocks.Add(b);
                }
            }

            if (changed)
            {
                Rebuild(layout, layout.Rows, layout.Columns, null, null, AllSlices(layout), blocks, Links(layout));
                ContentMerged(range, false);
            }
        }

        #endregion

        #region Copying and filling

        // The options of CopyFrom the library carries out.
        private const DxfTableCopyOptions CopyOptions = DxfTableCopyOptions.ExpandOrContractTable | DxfTableCopyOptions.SkipContent
            | DxfTableCopyOptions.SkipFormat | DxfTableCopyOptions.SkipMerges | DxfTableCopyOptions.TableCopyRowHeight | DxfTableCopyOptions.TableCopyColumnWidth
            | DxfTableCopyOptions.SkipField | DxfTableCopyOptions.SkipFormula | DxfTableCopyOptions.SkipDataLink;

        /// <summary>
        /// Copies cells of another table of the drawing, or of this one, into this table, as AutoCAD's <c>CopyFrom(source,
        /// options, sourceRange, targetRange)</c>: the cells of the source range, row by row, from the top left cell of the
        /// target range on, each with its content (its kind, text or value, block and attribute values) and its look (its
        /// overrides and their flags); the blocks of merged cells wholly within the source range merged alike, those of this
        /// table the copy meets unmerged. A field (344) a cell points at is the source cell's own, and is not copied: the copy
        /// holds the text the cell shows, so SkipField and SkipFormula are taken and change nothing. The ranges of cells linked to
        /// data wholly within the source range are linked alike, to the same DATALINKs, and those of this table the copy meets
        /// taken out, as merges are. Cells past this table's last row or column are left out, unless
        /// <see cref="DxfTableCopyOptions.ExpandOrContractTable"/> grows the table to hold them, its new rows and columns as high
        /// and wide as the source's; the library only grows a table with it, never
        /// shrinks one. The record is written once; the block AutoCAD drew the table with is not drawn anew. The
        /// header and data rows of the table of 3 rows, (1, 0) to (2, 1), copied to (3, 0) with ExpandOrContractTable make it a
        /// table of 5 rows, Mark and Qty in row 3 and B-12 in row 4.
        /// </summary>
        /// <param name="source">The table copied from, of this drawing; this table itself among them.</param>
        /// <param name="options"><see cref="DxfTableCopyOptions.None"/>, or of <see cref="DxfTableCopyOptions.ExpandOrContractTable"/>,
        /// <see cref="DxfTableCopyOptions.SkipContent"/> (the look alone copied), <see cref="DxfTableCopyOptions.SkipFormat"/> (the
        /// content alone), <see cref="DxfTableCopyOptions.SkipMerges"/> (the merges of this table kept, none copied),
        /// <see cref="DxfTableCopyOptions.SkipField"/> and <see cref="DxfTableCopyOptions.SkipFormula"/> (no field is copied in any case),
        /// <see cref="DxfTableCopyOptions.SkipDataLink"/> (the data links of this table kept, none copied),
        /// <see cref="DxfTableCopyOptions.TableCopyRowHeight"/> and <see cref="DxfTableCopyOptions.TableCopyColumnWidth"/> (the
        /// heights and widths of the source's rows and columns given to those copied to).</param>
        /// <param name="sourceRange">The cells copied, within the source; null for every cell of it.</param>
        /// <param name="targetRange">Where they go, by its top left cell, one of this table's, or just past its last row or column
        /// with <see cref="DxfTableCopyOptions.ExpandOrContractTable"/>; null for the place of the source range.</param>
        /// <returns>The cells copied to.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the source is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the source is of another drawing, or the options ask for what the library does not carry out.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the source range reaches past the source, or the copy begins at no cell of this table.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a table's record does not hold its cells, heights and widths, or this table is erased.</exception>
        public DxfCellRange CopyFrom(DxfTable source, DxfTableCopyOptions options, DxfCellRange sourceRange, DxfCellRange targetRange)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if ((options & ~CopyOptions) != 0)
            {
                throw new ArgumentException("The library copies a table's cells with ExpandOrContractTable, SkipContent, SkipFormat, SkipMerges, SkipField, SkipFormula, SkipDataLink, TableCopyRowHeight and TableCopyColumnWidth: " + (options & ~CopyOptions) + " asks for what a file of these versions has no groups for, or for rules of AutoCAD's own.", nameof(options));
            }

            if (!IsResident || !source.IsResident || !ReferenceEquals(source.Item.File, Item.File))
            {
                throw new ArgumentException("The table copied from is of another drawing, whose cells point at its own blocks and fields: copy it into this drawing first, with WblockCloneObjects.", nameof(source));
            }

            Layout from = source.Structured();
            Layout layout = Structured();
            DxfCellRange taken = sourceRange ?? new DxfCellRange(0, 0, from.Rows - 1, from.Columns - 1);
            CheckRange(taken, from, nameof(sourceRange));
            int top = targetRange?.TopRow ?? taken.TopRow;
            int left = targetRange?.LeftColumn ?? taken.LeftColumn;

            bool grow = (options & DxfTableCopyOptions.ExpandOrContractTable) != 0;

            // Where the table grows, the copy may begin just past its last row or column.
            if (top > layout.Rows - (grow ? 0 : 1) || left > layout.Columns - (grow ? 0 : 1))
            {
                throw new ArgumentOutOfRangeException(nameof(targetRange), targetRange, "The copy begins at a cell of the table's " + layout.Rows.ToString(CultureInfo.InvariantCulture) + " rows of " + layout.Columns.ToString(CultureInfo.InvariantCulture) + " columns" + (grow ? ", or just past its last row or column" : string.Empty) + ": (" + top.ToString(CultureInfo.InvariantCulture) + ", " + left.ToString(CultureInfo.InvariantCulture) + ") is not one.");
            }

            int high = taken.BottomRow - taken.TopRow + 1;
            int wide = taken.RightColumn - taken.LeftColumn + 1;
            int rows = grow ? Math.Max(layout.Rows, top + high) : layout.Rows;
            int columns = grow ? Math.Max(layout.Columns, left + wide) : layout.Columns;
            high = Math.Min(high, rows - top);
            wide = Math.Min(wide, columns - left);
            var copied = new DxfCellRange(this, top, left, top + high - 1, left + wide - 1);

            var heights = new List<string>();
            var widths = new List<string>();

            for (int r = 0; r < rows; r++)
            {
                bool copiedRow = r >= top && r < top + high && ((options & DxfTableCopyOptions.TableCopyRowHeight) != 0 || r >= layout.Rows);
                heights.Add(copiedRow ? from.Groups.List[from.RowAt[taken.TopRow + r - top]].Value : layout.Groups.List[layout.RowAt[r]].Value);
            }

            for (int c = 0; c < columns; c++)
            {
                bool copiedColumn = c >= left && c < left + wide && ((options & DxfTableCopyOptions.TableCopyColumnWidth) != 0 || c >= layout.Columns);
                widths.Add(copiedColumn ? from.Groups.List[from.ColumnAt[taken.LeftColumn + c - left]].Value : layout.Groups.List[layout.ColumnAt[c]].Value);
            }

            bool valued = Item.File.Supports(1021);
            bool content = (options & DxfTableCopyOptions.SkipContent) == 0;
            bool look = (options & DxfTableCopyOptions.SkipFormat) == 0;
            var cells = new List<KeyValuePair<int, string>>[rows * columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    List<KeyValuePair<int, string>> target = r < layout.Rows && c < layout.Columns ? Slice(layout, r * layout.Columns + c) : NewCell(valued);

                    if (r >= top && r < top + high && c >= left && c < left + wide)
                    {
                        CellView cell = from.CellAt(taken.TopRow + r - top, taken.LeftColumn + c - left);

                        // Both copied as the source cell holds them; the look alone, or the content alone, with the other of the cell here.
                        target = content && look ? WithoutField(Slice(cell)) : look ? WithLook(target, cell) : content ? WithLook(WithoutField(Slice(cell)), View(target)) : target;
                    }

                    cells[r * columns + c] = target;
                }
            }

            var blocks = new List<DxfCellRange>();
            bool merges = (options & DxfTableCopyOptions.SkipMerges) == 0;

            foreach (DxfCellRange b in Blocks(layout))
            {
                if (!merges || !Meets(b, copied))
                {
                    blocks.Add(b);
                }
            }

            foreach (DxfCellRange b in merges ? Blocks(from) : new List<DxfCellRange>())
            {
                if (Within(b, taken) && b.BottomRow - taken.TopRow < high && b.RightColumn - taken.LeftColumn < wide)
                {
                    AddBlock(blocks, top + b.TopRow - taken.TopRow, left + b.LeftColumn - taken.LeftColumn, top + b.BottomRow - taken.TopRow, left + b.RightColumn - taken.LeftColumn);
                }
            }

            // The links alike: this table's the copy meets go, the source's wholly within its range come, unless they are skipped.
            var links = new List<LinkedRange>();
            bool linksToo = (options & DxfTableCopyOptions.SkipDataLink) == 0;

            foreach (LinkedRange l in Links(layout))
            {
                if (!linksToo || !Meets(l.Range, copied))
                {
                    links.Add(l);
                }
            }

            foreach (LinkedRange l in linksToo ? Links(from) : new List<LinkedRange>())
            {
                if (Within(l.Range, taken) && l.BottomRow - taken.TopRow < high && l.RightColumn - taken.LeftColumn < wide)
                {
                    links.Add(l.With(top + l.TopRow - taken.TopRow, left + l.LeftColumn - taken.LeftColumn, top + l.BottomRow - taken.TopRow, left + l.RightColumn - taken.LeftColumn));
                }
            }

            Rebuild(layout, rows, columns, heights, widths, cells, blocks, links);
            return copied;
        }

        /// <summary>
        /// Fills cells from others, as AutoCAD's <c>Fill</c> does when a cell's grip is dragged, as AutoCAD 2021 filled them
        /// (D33 f01 to f20, g01 to g26): the rows of the fill range down the columns of the source range, or its columns along
        /// the rows of the source with <see cref="DxfTableFillOptions.Row"/>, whatever columns (or rows) the fill range names;
        /// each cell given the source cell its place repeats, from the fill range's first cell on, so that 1.5 and 2.5 filled
        /// into rows 5 and 6 give 1.5 and 2.5. <see cref="DxfTableFillOptions.CopyContent"/> copies the content alone, the cell
        /// keeping its look and flags, and wins over a series; <see cref="DxfTableFillOptions.CopyFormat"/> copies the look, the
        /// flags and the kind of value, a cell of no value given one of none of that kind (<c>93 5</c>), one of a value of that
        /// kind keeping it, and one of a value of another kind turned into it as <see cref="SetDataType(int, int, DxfDataType, DxfUnitType)"/>
        /// turns it, Mark given the look of 1.5 a number of none, the text 12 the number 12 shown 12.0000 (D33b f02, f23 to
        /// f26, f31, f32); the two together copy the cell whole. The header and the title are filled as rows of data are
        /// (D33b f01 to f20). <see cref="DxfTableFillOptions.GenerateSeries"/> without CopyContent
        /// carries the numbers of the source on, the cell given its kind of value (0x40000) and its look kept unless
        /// CopyFormat is given: on from the source's last number by the step from its first to its last (one for a source of
        /// one number) through the fill range, wherever it stands, 1.5 and 2.5 going on to 3.5, 4.5 and 5.5 below them or
        /// above them alike; with <see cref="DxfTableFillOptions.Reverse"/> back from the source's first number, the fill
        /// range's last cell next to it, 1.5 and 2.5 to -1.5, -0.5 and 0.5, and 10 and 20 to -20, -10 and 0. Reverse alone
        /// changes nothing of a copy. A number of a series is rounded to the places of decimals of its source and its step,
        /// and to 15 significant figures, 0.1 and 0.2 going on to 0.3, written of six places from R2007 on (0.300000), a whole
        /// number where every source is one and so is the step; a source of a text that is no number is copied, the cell given
        /// its kind of value. Options of none of the three change nothing; a fill range that holds a cell of the source is
        /// refused, as AutoCAD 2021 refused it (<c>eInvalidInput</c>). The numbers are those of a whole number or a number value,
        /// or before R2007 a text that reads as one. A date is not carried on: the library knows no groups of a date value. The
        /// record is written once; the block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="fillRange">The cells filled: its rows (its columns along the rows), within the table.</param>
        /// <param name="sourceRange">The cells filled from, within the table.</param>
        /// <param name="options">How, of the flags of <see cref="DxfTableFillOptions"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when a range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a range reaches past the table, or the options hold a flag none of <see cref="DxfTableFillOptions"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the rows of the fill range (its columns, along the rows) meet the source's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void Fill(DxfCellRange fillRange, DxfCellRange sourceRange, DxfTableFillOptions options)
        {
            if (((int)options & ~0x7F) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options, "The options are made of the flags of DxfTableFillOptions.");
            }

            Layout layout = Structured();
            CheckRange(fillRange, layout, nameof(fillRange));
            CheckRange(sourceRange, layout, nameof(sourceRange));
            bool along = (options & DxfTableFillOptions.Row) != 0;
            int from = along ? fillRange.LeftColumn : fillRange.TopRow;
            int to = along ? fillRange.RightColumn : fillRange.BottomRow;
            int start = along ? sourceRange.LeftColumn : sourceRange.TopRow;
            int end = along ? sourceRange.RightColumn : sourceRange.BottomRow;

            if (from <= end && to >= start)
            {
                throw new ArgumentException("The fill range " + fillRange + " holds " + (along ? "columns" : "rows") + " of the source " + sourceRange + ", which AutoCAD 2021 refuses (eInvalidInput).", nameof(fillRange));
            }

            bool content = (options & DxfTableFillOptions.CopyContent) != 0;
            bool look = (options & DxfTableFillOptions.CopyFormat) != 0;
            bool series = (options & DxfTableFillOptions.GenerateSeries) != 0 && !content;

            if (!content && !look && !series)
            {
                return;
            }

            bool reverse = (options & DxfTableFillOptions.Reverse) != 0;
            List<KeyValuePair<int, string>>[] cells = AllSlices(layout);
            int count = end - start + 1;
            int lines = along ? sourceRange.BottomRow - sourceRange.TopRow + 1 : sourceRange.RightColumn - sourceRange.LeftColumn + 1;
            bool changed = false;

            for (int line = 0; line < lines; line++)
            {
                // The source cells of the line in their order, their numbers where each holds one, and the places of decimals
                // the most precise of them is written to.
                var sources = new CellView[count];
                var numbers = new double[count];
                bool numeric = true;
                bool whole = true;
                int places = 0;

                for (int k = 0; k < count; k++)
                {
                    sources[k] = along ? layout.CellAt(sourceRange.TopRow + line, start + k) : layout.CellAt(start + k, sourceRange.LeftColumn + line);
                    numeric &= NumberOf(sources[k], out numbers[k], out bool isWhole, out int decimals);
                    whole &= isWhole;
                    places = Math.Max(places, decimals);
                }

                // The step from the first to the last, one for a source of one number.
                double by = count == 1 ? 1.0 : (numbers[count - 1] - numbers[0]) / (count - 1);
                whole &= Math.Floor(by) == by;

                // Whole numbers and a whole step, each within the 53 bits a double holds exactly, go on in whole arithmetic, so
                // that 1E15 and 1E15 + 1 go on to 1E15 + 2, which 15 figures would round away.
                bool integral = Integral(by);

                for (int k = 0; k < count; k++)
                {
                    integral &= Integral(numbers[k]);
                }

                // The places of the step count too, as written to 15 figures: 1, 2 and 4 go on by 1.5 to 5.5, 7 and 8.5.
                places = Math.Max(places, Decimals(by.ToString("G15", CultureInfo.InvariantCulture)));

                for (int place = from; place <= to; place++)
                {
                    int r = along ? sourceRange.TopRow + line : place;
                    int c = along ? place : sourceRange.LeftColumn + line;
                    CellView source = sources[(place - from) % count];
                    List<KeyValuePair<int, string>> target = cells[r * layout.Columns + c];
                    List<KeyValuePair<int, string>> made;

                    if (series && numeric)
                    {
                        // On from the last number through the fill range, or back from the first in reverse, the fill range's
                        // last cell next to it.
                        int steps = reverse ? -(to - place + 1) : place - from + 1;
                        long first = reverse ? (long)numbers[0] : (long)numbers[count - 1];
                        double value = (reverse ? numbers[0] : numbers[count - 1]) + steps * by;
                        double rounded = integral && Exact(first, steps, (long)by, out long exact) ? exact : Rounded(value, places);
                        made = look ? Formatted(target, source) : target;
                        made = WithSeriesNumber(made, rounded, whole && rounded >= int.MinValue && rounded <= int.MaxValue);
                    }
                    else if (content && !look)
                    {
                        made = WithFlags(WithLook(WithoutField(Slice(source)), View(target)), View(target).Flags);
                    }
                    else if (look && !content && !series)
                    {
                        made = Formatted(target, source);
                    }
                    else
                    {
                        // The cell whole, or a series of texts that are no numbers: the content copied, the cell given its kind
                        // of value.
                        made = WithoutField(Slice(source));
                        made = look ? made : WithFlags(WithLook(made, View(target)), (View(target).Flags ?? 0) | DataTypeFlag);
                    }

                    changed |= !SameGroups(made, target);
                    cells[r * layout.Columns + c] = made;
                }
            }

            if (changed)
            {
                Rebuild(layout, layout.Rows, layout.Columns, null, null, cells, Blocks(layout), Links(layout));
            }
        }

        /// <summary>
        /// A cell given the look, flags and kind of value of another, as AutoCAD 2021's Fill of CopyFormat gave them (D33 f04,
        /// f20, g15): its value kept where it holds one of that kind, else one of none of the other's kind, its flag 2 taken
        /// away (93 5); a value of another kind turned into the other's, a text, a whole number or a number, as
        /// <see cref="SetDataType(int, int, DxfDataType, DxfUnitType)"/> turns it, Mark made a number of none, the text 12 the
        /// number 12 shown 12.0000 and 2.5 the whole number 2 (D33b f02, f23 to f26, f31, f32).
        /// </summary>
        private List<KeyValuePair<int, string>> Formatted(List<KeyValuePair<int, string>> target, CellView look)
        {
            List<KeyValuePair<int, string>> made = WithFlags(WithLook(target, look), look.Flags);
            CellView cell = View(made);
            int flags = int.TryParse(cell.InValue(93)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : 6;

            if (cell.ValueFrom >= 0 && look.ValueFrom >= 0 && (flags & 1) != 0)
            {
                cell.WriteValue(look.InValue(90) ?? "4", new List<KeyValuePair<int, string>>(), string.Empty, null, null, false, (flags & ~2).ToString(CultureInfo.InvariantCulture));
            }
            else if (cell.ValueFrom >= 0 && look.ValueFrom >= 0 && int.TryParse(look.InValue(90)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind)
                && (kind == (int)DxfDataType.String || kind == (int)DxfDataType.Long || kind == (int)DxfDataType.Double)
                && (cell.InValue(90) ?? "4").Trim() != kind.ToString(CultureInfo.InvariantCulture))
            {
                Turn(cell, (DxfDataType)kind, look.InValue(94) ?? "0");
            }

            return cell.Groups.List;
        }

        /// <summary>A cell's groups given the flags of its overrides (177, or 91 from R2007 on); null leaves them as they are.</summary>
        private static List<KeyValuePair<int, string>> WithFlags(List<KeyValuePair<int, string>> groups, int? flags)
        {
            CellView cell = View(groups);

            if (flags == null || cell.Flags == flags)
            {
                return groups;
            }

            cell.SetFlags(flags.Value, cell.ValueFrom >= 0 ? 91 : 177);
            return cell.Groups.List;
        }

        /// <summary>
        /// A cell's groups given a number of a series, as AutoCAD 2021's Fill wrote it (D33 f02, f18, g06): of its kind in its
        /// value from R2007 on, the flags of the value 4 and the number shown of six places (3.500000), the cell given its kind
        /// of value (0x40000); before R2007 as its text.
        /// </summary>
        private static List<KeyValuePair<int, string>> WithSeriesNumber(List<KeyValuePair<int, string>> groups, double value, bool whole)
        {
            CellView cell = View(groups);
            Written written = whole ? Whole((int)value) : Real(value, nameof(value));

            if (cell.ValueFrom < 0)
            {
                cell.SetText(written.Text);
                return cell.Groups.List;
            }

            cell.WriteValue(((int)written.Kind).ToString(CultureInfo.InvariantCulture), written.Data, whole ? written.Text : RealText(value, null), null, null, false, "4");
            return WithFlags(cell.Groups.List, (View(cell.Groups.List).Flags ?? 0) | DataTypeFlag);
        }

        /// <summary>
        /// Takes away what the table and its cells hold of their own over the table style, as AutoCAD's <c>SetFromStyle</c>
        /// gives an object the look of its style: as <see cref="ClearTableStyleOverrides(int)"/> of 0 does, the table's flags 93
        /// to 96 set to nought, its own groups after its last cell and the overrides of every cell taken out; nothing where it
        /// holds none. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <returns>True: a table takes its look from its table style, the one it names (342) or, naming none, AutoCAD's
        /// Standard, which such a table is taken to have, so that a table of a file that names none is set from it too.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased and holds overrides.</exception>
        public override bool SetFromStyle()
        {
            ClearTableStyleOverrides(0);
            return true;
        }

        /// <summary>
        /// The number of a cell where it holds one: from R2007 on of its whole number or number value, before R2007 of its text
        /// where it reads as one; and the places of decimals it is written to, 0.25 to 2.
        /// </summary>
        private static bool NumberOf(CellView cell, out double number, out bool whole, out int decimals)
        {
            whole = false;
            number = 0.0;
            decimals = 0;

            if (cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell)
            {
                return false;
            }

            object value = cell.Value();
            string text = cell.ValueFrom >= 0 ? (value is double ? cell.InValue(140) : null) : value as string;

            switch (value)
            {
                case int read:
                    number = read;
                    whole = true;
                    return true;
                case double read:
                    number = read;
                    decimals = Decimals(text);
                    return true;
                case string _ when cell.ValueFrom >= 0:
                    return false;
                case string words when int.TryParse(words.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read):
                    number = read;
                    whole = true;
                    return true;
                case string words when Numbers.TryRead(words, out double read) && Guard.IsFinite(read):
                    number = read;
                    decimals = Decimals(text);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether a number is whole and within the 53 bits a double holds a whole number exactly in.</summary>
        private static bool Integral(double value) => Math.Floor(value) == value && Math.Abs(value) <= 9007199254740992.0;

        /// <summary>A number of a series in whole arithmetic, from a whole number by a count of whole steps; false where it passes the 53 bits.</summary>
        private static bool Exact(long from, int steps, long by, out long value)
        {
            try
            {
                value = checked(from + steps * by);
                return Math.Abs(value) <= 9007199254740992L;
            }
            catch (OverflowException)
            {
                value = 0;
                return false;
            }
        }

        /// <summary>The places of decimals a number is written to: 2 for 0.25, 5 for 1.5E-04, nought for 1500.</summary>
        private static int Decimals(string text)
        {
            string written = (text ?? string.Empty).Trim();
            int e = written.IndexOfAny(new[] { 'E', 'e' });
            int exponent = 0;

            if (e >= 0 && !int.TryParse(written.Substring(e + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent))
            {
                exponent = 0;
            }

            string mantissa = e >= 0 ? written.Substring(0, e) : written;
            int point = mantissa.IndexOf('.');
            int fraction = point < 0 ? 0 : mantissa.Length - point - 1;
            return Math.Max(0, fraction - exponent);
        }

        /// <summary>
        /// A number of a series rounded to the places of decimals of its source and its step, and to 15 significant figures,
        /// so that 0.1 and 0.2 go on to 0.3, not to the 0.30000000000000004 their sum gives, and 1E-20 and 2E-20 to 3E-20.
        /// </summary>
        private static double Rounded(double value, int places)
        {
            double rounded = places <= 15 ? Math.Round(value, places) : value;
            return Guard.IsFinite(rounded) ? double.Parse(rounded.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture) + 0.0 : rounded;
        }

        #endregion

        #region Formulas

        /// <summary>
        /// Gets whether a cell holds a formula, as AutoCAD's <c>HasFormula</c>: its field (344) one of AutoCAD's expressions
        /// (AcExpr), as <see cref="GetFormula(int, int, int)"/> reads it.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>Whether it holds one.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public bool HasFormula(int row, int column, int contentIndex) => GetFormula(row, column, contentIndex) != null;

        /// <summary>
        /// Gets the formula of a cell, as AutoCAD's <c>GetFormula</c>: the expression of the FIELD the cell's 344 points at, of
        /// the evaluator AcExpr, or of the one field within a field of text (<c>_text</c>, its code <c>%&lt;\_FldIdx 0&gt;%</c>)
        /// that is one; its field code <c>\AcExpr (A1+B1)</c> is given as <c>=A1+B1</c>, a format after it (<c>\f "%lu2"</c>)
        /// left out. Null for a cell of no field, or of a field of another kind, as a date. A 344 that names no record of the
        /// file, as AutoCAD 2021 wrote the formula of a linked cell (its 192, written in none), is read from the cell's field in
        /// the table's TABLECONTENT, where AutoCAD keeps it (its 195 of <c>\AcExpr (1+2)</c>). The library does not work a
        /// formula out: the cell's value is the one AutoCAD last wrote.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The formula, from its <c>=</c>, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public string GetFormula(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            string handle = Cell(row, column).Head(344)?.Trim();

            if (string.IsNullOrEmpty(handle) || !IsResident)
            {
                return null;
            }

            string formula = FormulaOf(Item.File, handle, true);

            if (formula == null && Item.File.ObjectItem(handle) == null)
            {
                string field = ContentFieldOf(row, column);
                formula = field == null ? null : FormulaOf(Item.File, field, true);
            }

            return formula;
        }

        /// <summary>
        /// Sets the formula of a cell, as AutoCAD's <c>SetFormula</c>, from R2004 on, which brought fields: a FIELD added at the
        /// end of the OBJECTS section, owned by
        /// the table, of the evaluator AcExpr and the field code <c>\AcExpr (A1+B1)</c> for <c>=A1+B1</c>, written as
        /// <see cref="DxfField"/> writes a field made in code (marked not yet worked out, its status 95 of 1), the class FIELD
        /// listed in the CLASSES section where it is not, and the cell's 344 pointed at it. The cell's value is left as it is,
        /// for AutoCAD to work out when it evaluates the field; the field the cell pointed at before is erased with its
        /// children where the table owns it and nothing else of the file points at it. Nothing is written where the cell holds
        /// that formula. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="formula">The formula, from its <c>=</c>: <c>=Sum(A1:B2)</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the formula is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the formula does not begin with <c>=</c>, has nothing after it, has a line break in it, or would not be read back as it is given, its parentheses not paired.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, the file is older than R2004 (AC1018), which brought fields, or the table is erased.</exception>
        public void SetFormula(int row, int column, int contentIndex, string formula)
        {
            CheckContent(contentIndex);

            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            string expression = formula.Trim();

            if (!expression.StartsWith("=", StringComparison.Ordinal) || expression.Substring(1).Trim().Length == 0 || formula.IndexOf('\n') >= 0 || formula.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A formula begins with =, as =Sum(A1:B2), and is written on one line: " + formula + " is not one.", nameof(formula));
            }

            expression = expression.Substring(1).Trim();
            string code = "\\AcExpr (" + expression + ")";

            if (!string.Equals(Expression(code), "=" + expression, StringComparison.Ordinal))
            {
                throw new ArgumentException("The formula " + formula + " would not be read back from its field as it is given: its parentheses do not pair, or a quote is left open.", nameof(formula));
            }

            CheckText(CellToWrite(row, column), row, column);

            if (!Item.File.Supports(1018))
            {
                throw new InvalidOperationException("A DXF older than R2004 (AC1018) has no FIELD to hold a formula: fields came with it.");
            }

            if (string.Equals(GetFormula(row, column, 0), "=" + expression, StringComparison.Ordinal))
            {
                return;
            }

            WriteFormula(row, column, code);
        }

        /// <summary>
        /// Writes a field code into a text cell as <see cref="SetFormula"/> writes one: a FIELD of it owned by the table, the
        /// cell's 344 pointed at it, the field it pointed at before erased where nothing else holds it.
        /// </summary>
        private void WriteFormula(int row, int column, string code)
        {
            CheckEditable();
            string old = CellToWrite(row, column).Head(344);
            DxfItem field = DxfField.WriteOwned(Item.File, Handle, new DxfField("%<" + code + ">%"));
            CellView cell = CellToWrite(row, column);
            cell.SetHead(344, field.Handle);
            cell.Groups.Write(Order);
            EraseFields(new[] { old });
        }

        /// <summary>
        /// The formula of a FIELD: of the evaluator AcExpr, its expression from its <c>=</c>; of a field of text whose code is
        /// its one field, that field's; null for any other.
        /// </summary>
        private static string FormulaOf(DxfFile file, string handle, bool outer)
        {
            DxfItem field = file.ObjectItem(handle);

            if (field == null || !string.Equals(field.Type, "FIELD", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string evaluator = null;
            var code = new System.Text.StringBuilder();
            var children = new List<string>();
            bool inCode = false;

            foreach (DxfTypedValue group in file.ObjectGroupsOf(field))
            {
                int key = group.TypeCode;
                evaluator = key == 1 && evaluator == null ? group.Value.Trim() : evaluator;
                bool first = key == 2 && code.Length == 0 && !inCode;
                inCode = first || (inCode && key == 3);

                if (inCode)
                {
                    code.Append(group.Value);
                }

                if (key == 360)
                {
                    children.Add(group.Value.Trim());
                }
            }

            if (string.Equals(evaluator, "AcExpr", StringComparison.Ordinal))
            {
                return Expression(code.ToString());
            }

            return outer && string.Equals(evaluator, "_text", StringComparison.Ordinal) && children.Count == 1 && code.ToString().Trim() == "%<\\_FldIdx 0>%"
                ? FormulaOf(file, children[0], false)
                : null;
        }

        /// <summary>The expression of an AcExpr field code, <c>\AcExpr (A1+B1) \f "%lu2"</c> or within <c>%&lt; &gt;%</c>, as <c>=A1+B1</c>; null where it is not one.</summary>
        private static string Expression(string code)
        {
            string text = code.Trim();

            if (text.StartsWith("%<", StringComparison.Ordinal) && text.EndsWith(">%", StringComparison.Ordinal) && text.Length >= 4)
            {
                text = text.Substring(2, text.Length - 4).Trim();
            }

            if (!text.StartsWith("\\AcExpr", StringComparison.Ordinal))
            {
                return null;
            }

            text = text.Substring(7).Trim();

            if (!text.StartsWith("(", StringComparison.Ordinal))
            {
                int format = text.IndexOf("\\f", StringComparison.Ordinal);
                text = (format >= 0 ? text.Substring(0, format) : text).Trim();
                return text.Length == 0 ? null : "=" + text;
            }

            int depth = 0;
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char letter = text[i];
                quoted = letter == '"' ? !quoted : quoted;
                depth += quoted ? 0 : letter == '(' ? 1 : letter == ')' ? -1 : 0;

                if (!quoted && depth == 0)
                {
                    string inner = text.Substring(1, i - 1).Trim();
                    return inner.Length == 0 ? null : "=" + inner;
                }
            }

            return null;
        }

        #endregion

        #region The record written anew

        /// <summary>Whether the table's record holds its cells, one height for each row and one width for each column.</summary>
        private static bool Holds(Layout layout)
            => layout.Rows >= 1 && layout.Columns >= 1 && (long)layout.Rows * layout.Columns == layout.CellStart.Count
                && layout.RowAt.Count == layout.Rows && layout.ColumnAt.Count == layout.Columns;

        /// <summary>The table's layout read anew to have its structure changed: refused where its record does not hold its cells, heights and widths.</summary>
        private Layout Structured()
        {
            Layout layout = ReadToWrite();

            if (!Holds(layout))
            {
                throw new InvalidOperationException("The ACAD_TABLE " + (Handle ?? string.Empty) + " has " + layout.Rows.ToString(CultureInfo.InvariantCulture) + " rows of " + layout.Columns.ToString(CultureInfo.InvariantCulture) + " columns, and its record holds " + layout.CellStart.Count.ToString(CultureInfo.InvariantCulture) + " cells, " + layout.RowAt.Count.ToString(CultureInfo.InvariantCulture) + " heights and " + layout.ColumnAt.Count.ToString(CultureInfo.InvariantCulture) + " widths: its structure is changed only where they agree.");
            }

            return layout;
        }

        /// <summary>
        /// Writes the record anew in one writing: its counts of rows and columns (91, 92), the heights and widths given (141, 142,
        /// where given) in the place of those it had, its cells, row by row, each of the groups given, and the merging (173, 175,
        /// 176) of each cell made to agree with the blocks given where it does not already, and the links to data of each made
        /// to agree with the links given, named by their top left cells alone; the groups before its cells and after its last
        /// kept as they are. Nothing is written where the record comes out the same.
        /// </summary>
        private void Rebuild(Layout layout, int rows, int columns, IList<string> heights, IList<string> widths, IList<List<KeyValuePair<int, string>>> cells, IList<DxfCellRange> blocks, IList<LinkedRange> links)
        {
            DxfDimGroups g = layout.Groups;
            int first = layout.CellStart[0];
            List<string> held = CellFields(layout);
            List<string> linked = LinkHandles(layout);
            LinkedRange[] anchored = Anchors(cells.Count, columns, links);
            var list = new List<KeyValuePair<int, string>>(g.List.Count + cells.Count);
            bool rowsSet = false;
            bool columnsSet = false;
            bool sizesSet = heights == null;

            for (int i = 0; i < first; i++)
            {
                KeyValuePair<int, string> group = g.List[i];
                bool head = i >= g.Sub.From;

                if (head && group.Key == 91 && !rowsSet)
                {
                    rowsSet = true;
                    list.Add(Counted(group, rows));
                }
                else if (head && group.Key == 92 && !columnsSet)
                {
                    columnsSet = true;
                    list.Add(Counted(group, columns));
                }
                else if (head && !sizesSet && (group.Key == 141 || group.Key == 142))
                {
                    sizesSet = true;
                    AddSizes(list, heights, widths);
                }
                else if (!head || heights == null || (group.Key != 141 && group.Key != 142))
                {
                    list.Add(group);
                }
            }

            if (!sizesSet)
            {
                AddSizes(list, heights, widths);
            }

            int[] owner = Owners(rows, columns, blocks);

            for (int k = 0; k < cells.Count; k++)
            {
                List<KeyValuePair<int, string>> cell = cells[k];
                int b = owner[k];

                if (b < 0)
                {
                    cell = Merged(cell, 0, 0, 0);
                }
                else
                {
                    DxfCellRange block = blocks[b];
                    bool corner = block.TopRow * columns + block.LeftColumn == k;
                    cell = corner ? Merged(cell, 1, block.RightColumn - block.LeftColumn + 1, block.BottomRow - block.TopRow + 1) : Merged(cell, 1, 0, 0);
                }

                // A link is named by a text cell alone: a block cell keeps its 340, its block, and a link it is the top left cell of goes.
                list.AddRange(IsTextCell(cell, 0) ? Linked(cell, anchored[k]) : cell);
            }

            for (int i = layout.CellsEnd; i < g.List.Count; i++)
            {
                list.Add(g.List[i]);
            }

            g.List.Clear();
            g.List.AddRange(list);

            if (!g.Same())
            {
                CheckEditable();
                g.Write(Order);
                EraseFields(held);
                EraseLinks(linked);
            }
        }

        /// <summary>The fields the cells of the table point at (344), by their handles, each once, row by row.</summary>
        private static List<string> CellFields(Layout layout)
        {
            var fields = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd; i < layout.CellsEnd; i++)
            {
                string handle = layout.Groups.List[i].Key == 344 ? layout.Groups.List[i].Value.Trim() : string.Empty;

                if (handle.Length > 0 && seen.Add(handle))
                {
                    fields.Add(handle);
                }
            }

            return fields;
        }

        /// <summary>
        /// Erases, with their children, the fields of handles given that a cell let go: each a FIELD the table owns (its 330)
        /// that nothing else of the file points at, as <see cref="DxfField.Erase"/> judges it: no cell of the table as it stands
        /// now, no copy of the table in the drawing, which holds its original's fields, no XRECORD and no field outside them.
        /// A field of another owner, or one held, is left. Taken in the order given, the handles of the cells as they stood, row
        /// by row; the file read once for them all.
        /// </summary>
        private void EraseFields(IEnumerable<string> dropped)
        {
            if (!IsResident)
            {
                return;
            }

            HashSet<string> held = null;
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fields = new List<DxfItem>();
            DxfFile file = Item.File;

            foreach (string given in dropped)
            {
                string handle = given?.Trim();

                if (string.IsNullOrEmpty(handle) || !taken.Add(handle))
                {
                    continue;
                }

                // A cell of the table that still holds it keeps it, read without reading the rest of the file.
                held = held ?? new HashSet<string>(CellFields(Read()), StringComparer.OrdinalIgnoreCase);
                DxfItem item = held.Contains(handle) ? null : file.ObjectItem(handle);

                if (item != null && !item.IsRemoved && item.Type == "FIELD" && string.Equals(file.OwnerOf(item), Handle, StringComparison.OrdinalIgnoreCase))
                {
                    fields.Add(item);
                }
            }

            if (fields.Count > 0)
            {
                DxfField.Erase(file, fields);
            }
        }

        private static KeyValuePair<int, string> Counted(KeyValuePair<int, string> group, int count)
            => int.TryParse(group.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int now) && now == count ? group : new KeyValuePair<int, string>(group.Key, count.ToString(CultureInfo.InvariantCulture));

        private static void AddSizes(List<KeyValuePair<int, string>> list, IList<string> heights, IList<string> widths)
        {
            foreach (string height in heights)
            {
                list.Add(new KeyValuePair<int, string>(141, height));
            }

            foreach (string width in widths)
            {
                list.Add(new KeyValuePair<int, string>(142, width));
            }
        }

        /// <summary>The block of merged cells each cell is in, by its index row by row; -1 for none.</summary>
        private static int[] Owners(int rows, int columns, IList<DxfCellRange> blocks)
        {
            var owner = new int[rows * columns];

            for (int k = 0; k < owner.Length; k++)
            {
                owner[k] = -1;
            }

            for (int b = 0; b < blocks.Count; b++)
            {
                for (int r = blocks[b].TopRow; r <= blocks[b].BottomRow; r++)
                {
                    for (int c = blocks[b].LeftColumn; c <= blocks[b].RightColumn; c++)
                    {
                        owner[r * columns + c] = owner[r * columns + c] < 0 ? b : owner[r * columns + c];
                    }
                }
            }

            return owner;
        }

        /// <summary>
        /// A cell's groups with its merging as asked: merged or not (173), and as the top left of a block its width and height in
        /// cells (175, 176), 0 and 0 for the rest; the groups as they are where the cell reads so already, a cell of 173 of 1 and
        /// 175 and 176 of 1 among a block's cells as AutoCAD's own as one of 0 and 0, and a top left cell of the spans asked
        /// whatever its 173, as AutoCAD writes its own 173 0.
        /// </summary>
        private static List<KeyValuePair<int, string>> Merged(List<KeyValuePair<int, string>> groups, int merged, int width, int height)
        {
            int nowMerged = HeadWhole(groups, 173);
            int nowWidth = HeadWhole(groups, 175);
            int nowHeight = HeadWhole(groups, 176);
            bool cornerNow = nowWidth > 1 || nowHeight > 1;
            bool corner = width > 1 || height > 1;

            if (corner ? Math.Max(1, nowWidth) == width && Math.Max(1, nowHeight) == height : !cornerNow && (nowMerged != 0) == (merged != 0))
            {
                return groups;
            }

            CellView cell = View(groups);

            if ((nowMerged != 0) != (merged != 0))
            {
                cell.SetHead(173, merged.ToString(CultureInfo.InvariantCulture));
            }

            if (corner || cornerNow)
            {
                cell.SetHead(175, width.ToString(CultureInfo.InvariantCulture));
                cell.SetHead(176, height.ToString(CultureInfo.InvariantCulture));
            }

            return cell.Groups.List;
        }

        /// <summary>A whole number of a cell's head, before its value; nought where it gives none.</summary>
        private static int HeadWhole(List<KeyValuePair<int, string>> groups, int code)
        {
            for (int i = 1; i < groups.Count; i++)
            {
                if (groups[i].Key == 301 && groups[i].Value.Trim() == "CELL_VALUE")
                {
                    break;
                }

                if (groups[i].Key == code)
                {
                    return int.TryParse(groups[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// The blocks of merged cells of the table, row by row by their top left cells: each cell of a width or a height (175,
        /// 176) above one, whatever its 173, as <see cref="GetMergeRange"/> reads it, cut at the table's last row and column; one
        /// that meets a block found before it is left out.
        /// </summary>
        private static List<DxfCellRange> Blocks(Layout layout)
        {
            var blocks = new List<DxfCellRange>();
            var covered = new bool[layout.Rows * layout.Columns];

            for (int r = 0; r < layout.Rows; r++)
            {
                for (int c = 0; c < layout.Columns; c++)
                {
                    if (covered[r * layout.Columns + c])
                    {
                        continue;
                    }

                    layout.CellAt(r, c).Spans(out int wide, out int high);
                    int width = Math.Max(1, wide);
                    int height = Math.Max(1, high);
                    int bottom = (int)Math.Min(layout.Rows - 1, (long)r + height - 1);
                    int right = (int)Math.Min(layout.Columns - 1, (long)c + width - 1);

                    if ((width == 1 && height == 1) || (bottom == r && right == c) || Covered(covered, layout.Columns, r, c, bottom, right))
                    {
                        continue;
                    }

                    for (int i = r; i <= bottom; i++)
                    {
                        for (int j = c; j <= right; j++)
                        {
                            covered[i * layout.Columns + j] = true;
                        }
                    }

                    blocks.Add(new DxfCellRange(r, c, bottom, right));
                }
            }

            return blocks;
        }

        private static bool Covered(bool[] covered, int columns, int top, int left, int bottom, int right)
        {
            for (int i = top; i <= bottom; i++)
            {
                for (int j = left; j <= right; j++)
                {
                    if (covered[i * columns + j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Adds a block of merged cells, where it is of more than one cell.</summary>
        private static void AddBlock(List<DxfCellRange> blocks, int top, int left, int bottom, int right)
        {
            if (bottom > top || right > left)
            {
                blocks.Add(new DxfCellRange(top, left, bottom, right));
            }
        }

        private static bool Meets(DxfCellRange a, DxfCellRange b)
            => a.TopRow <= b.BottomRow && b.TopRow <= a.BottomRow && a.LeftColumn <= b.RightColumn && b.LeftColumn <= a.RightColumn;

        private static bool Within(DxfCellRange inner, DxfCellRange outer)
            => inner.TopRow >= outer.TopRow && inner.BottomRow <= outer.BottomRow && inner.LeftColumn >= outer.LeftColumn && inner.RightColumn <= outer.RightColumn;

        /// <summary>The groups of the cell of an index, row by row, copied.</summary>
        private static List<KeyValuePair<int, string>> Slice(Layout layout, int index)
            => Slice(layout.CellAt(index / layout.Columns, index % layout.Columns));

        private static List<KeyValuePair<int, string>> Slice(CellView cell) => cell.Groups.List.GetRange(cell.From, cell.To - cell.From);

        private static List<KeyValuePair<int, string>>[] AllSlices(Layout layout)
        {
            var cells = new List<KeyValuePair<int, string>>[layout.CellStart.Count];

            for (int k = 0; k < cells.Length; k++)
            {
                cells[k] = Slice(layout, k);
            }

            return cells;
        }

        /// <summary>A cell's groups seen as a cell, held apart from the record, to be edited on their own.</summary>
        private static CellView View(List<KeyValuePair<int, string>> groups) => new CellView(new DxfDimGroups(groups), 0, groups.Count);

        private static bool SameGroups(List<KeyValuePair<int, string>> a, List<KeyValuePair<int, string>> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Key != b[i].Key || !string.Equals(a[i].Value, b[i].Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// An empty text cell that takes its look from the table style, as AutoCAD writes one: before R2007 its text in 1, from
        /// R2007 on its value of none between <c>301 CELL_VALUE</c> and <c>304 ACVALUE_END</c>, of the flags 7 and the kind 0 and
        /// no data, as AutoCAD 2021 wrote the new cells of its <c>InsertRows</c>, <c>InsertColumns</c> and <c>SetSize</c>, which it
        /// reads as of no value, where it read an empty text the library wrote as a value of an empty text.
        /// </summary>
        private static List<KeyValuePair<int, string>> NewCell(bool valued)
        {
            var cell = new List<KeyValuePair<int, string>>
            {
                new KeyValuePair<int, string>(171, "1"),
                new KeyValuePair<int, string>(172, "0"),
                new KeyValuePair<int, string>(173, "0"),
                new KeyValuePair<int, string>(174, "0"),
                new KeyValuePair<int, string>(175, "0"),
                new KeyValuePair<int, string>(176, "0"),
                new KeyValuePair<int, string>(valued ? 91 : 177, "0"),
                new KeyValuePair<int, string>(178, "0"),
                new KeyValuePair<int, string>(145, "0.0"),
            };

            if (!valued)
            {
                cell.Add(new KeyValuePair<int, string>(1, string.Empty));
                return cell;
            }

            cell.Add(new KeyValuePair<int, string>(92, "0"));
            cell.Add(new KeyValuePair<int, string>(301, "CELL_VALUE"));
            cell.Add(new KeyValuePair<int, string>(93, "7"));
            cell.Add(new KeyValuePair<int, string>(90, "0"));
            cell.Add(new KeyValuePair<int, string>(94, "0"));
            cell.Add(new KeyValuePair<int, string>(300, string.Empty));
            cell.Add(new KeyValuePair<int, string>(302, string.Empty));
            cell.Add(new KeyValuePair<int, string>(304, "ACVALUE_END"));
            return cell;
        }

        /// <summary>
        /// The table's own looks of the kind of each of its rows, the title's, the header's or the data's, as overrides of a
        /// cell, row by row: its alignment (170), text style (7), background (63, 283), colour (64) and text height (140) of
        /// that kind, after its last cell or in its head (their flags in 93), in the order AutoCAD 2021 writes them in a cell;
        /// null where it gives none.
        /// AutoCAD 2021 gave each new cell of the columns its <c>InsertColumns</c> and <c>SetSize</c> put in those of its row's
        /// kind as its own, and read a cell without them by its table style alone: the table of its own header of 9 and data
        /// of 9, GH_TXT, red and top left, given a column, wrote the new header cell <c>63 3</c>, <c>283 0</c>, <c>140 9.0</c>
        /// (91 38) and the new data cell <c>170 1</c>, <c>7 GH_TXT</c>, <c>64 1</c>, <c>140 9.0</c> (91 57); the cells of a row
        /// put in it gave none (D28c o1 to o6).
        /// </summary>
        private List<KeyValuePair<int, string>>[] RowKindLooks(Layout layout)
        {
            var looks = new List<KeyValuePair<int, string>>[3];
            bool any = false;

            for (int rowKind = 0; rowKind < 3; rowKind++)
            {
                looks[rowKind] = new List<KeyValuePair<int, string>>();

                foreach (int code in new[] { 170, 7, 63, 283, 64, 140 })
                {
                    string text = OwnText(layout, RowLooks[code] + rowKind);

                    if (text != null)
                    {
                        looks[rowKind].Add(new KeyValuePair<int, string>(code, text));
                        any = true;
                    }
                }
            }

            if (!any)
            {
                return null;
            }

            // Those of each row, by its kind.
            var rows = new List<KeyValuePair<int, string>>[layout.Rows];

            for (int row = 0; row < layout.Rows; row++)
            {
                DxfRowType type = RowType(row);
                rows[row] = looks[type == DxfRowType.TitleRow ? 0 : type == DxfRowType.HeaderRow ? 1 : 2];
            }

            return rows;
        }

        /// <summary>A new cell (<see cref="NewCell(bool)"/>) of a row of the table as it stands, given the table's own looks of the row's kind (<see cref="RowKindLooks"/>) as its own.</summary>
        private static List<KeyValuePair<int, string>> NewCell(bool valued, List<KeyValuePair<int, string>>[] looks, int row)
        {
            List<KeyValuePair<int, string>> cell = NewCell(valued);

            if (looks == null || looks[row].Count == 0)
            {
                return cell;
            }

            CellView view = View(cell);

            foreach (KeyValuePair<int, string> look in looks[row])
            {
                view.SetOverride(look.Key, look.Value, valued ? 91 : 177);
            }

            return view.Groups.List;
        }

        /// <summary>
        /// The kind of the empty value AutoCAD 2021's <c>InsertRowsAndInherit</c> gave a cell that takes the look of one of a
        /// value of the kind given: that kind; 4, a text, where it has none; 512 for a value of no kind (0), as it wrote the
        /// rows it put in below the linked row of its 73 (D28c i4).
        /// </summary>
        private static string InheritedKind(string kind) => string.IsNullOrEmpty(kind) ? "4" : kind == "0" ? "512" : kind;

        /// <summary>
        /// An empty text cell with the look a cell holds of its own: its overrides with their flags and the edges of its lines,
        /// its turn (145), and from R2007 on its value the empty one AutoCAD 2021's <c>InsertRowsAndInherit</c> wrote, of the
        /// flags 5, the kind of the cell's value (<see cref="InheritedKind"/>) and no data, with the unit (94) and format (300)
        /// of the cell's value.
        /// </summary>
        private static List<KeyValuePair<int, string>> Inherited(CellView source, bool valued)
        {
            List<KeyValuePair<int, string>> cell = NewCell(valued);
            string turn = source.Head(145);

            if (turn != null)
            {
                cell[8] = new KeyValuePair<int, string>(145, turn);
            }

            if (valued)
            {
                string kind = source.ValueFrom >= 0 ? source.InValue(90)?.Trim() : null;
                string unit = source.ValueFrom >= 0 ? source.InValue(94) : null;
                string format = source.ValueFrom >= 0 ? source.InValue(300) : null;
                int at = cell.FindIndex(g => g.Key == 93);
                cell[at] = new KeyValuePair<int, string>(93, "5");
                cell[at + 1] = new KeyValuePair<int, string>(90, InheritedKind(kind));
                cell[at + 2] = new KeyValuePair<int, string>(94, unit ?? "0");
                cell[at + 3] = new KeyValuePair<int, string>(300, format ?? string.Empty);
            }

            List<KeyValuePair<int, string>> inherited = WithLook(cell, source);

            // The cell's own kind of value (0x40000) goes with the format of its value.
            if (((source.Flags ?? 0) & DataTypeFlag) == 0)
            {
                return inherited;
            }

            CellView view = View(inherited);
            view.SetHead(valued ? 91 : 177, ((view.Flags ?? 0) | DataTypeFlag).ToString(CultureInfo.InvariantCulture));
            return view.Groups.List;
        }

        /// <summary>
        /// A cell's groups with the look of another: its own overrides and their flags taken out, the other's, wherever they
        /// stand outside its value, put in after its content with their flags and the edges of its lines (172), its flags of
        /// nothing else (as its kind of value, 0x40000) kept.
        /// </summary>
        private static List<KeyValuePair<int, string>> WithLook(List<KeyValuePair<int, string>> groups, CellView look)
        {
            CellView cell = View(groups);
            int lookFlags = (look.Flags ?? look.Held()) & CellOverrideFlags;
            int? flags = cell.Flags;
            int held = cell.Held();

            if ((((flags ?? 0) | held) & CellOverrideFlags) != 0)
            {
                cell.RemoveOverrides(CellOverrideFlags);
                cell = new CellView(cell.Groups, 0, cell.Groups.List.Count);
            }

            var taken = new HashSet<int>();

            // Its overrides wherever they stand outside its value: AutoCAD 2021 writes them before its 92.
            for (int i = look.From + 1; i < look.To; i++)
            {
                KeyValuePair<int, string> group = look.Groups.List[i];

                if ((look.ValueFrom < 0 || i < look.ValueFrom || i > look.ValueTo) && Layout.CellOverrides.TryGetValue(group.Key, out int bit) && (bit & lookFlags) != 0 && taken.Add(group.Key))
                {
                    cell.Groups.List.Add(group);
                }
            }

            int wanted = ((cell.Flags ?? 0) & ~CellOverrideFlags) | lookFlags;

            if (cell.Flags != wanted && (cell.Flags != null || wanted != 0))
            {
                cell = new CellView(cell.Groups, 0, cell.Groups.List.Count);
                cell.SetHead(cell.Head(91) != null ? 91 : cell.Head(177) != null ? 177 : cell.ValueFrom >= 0 ? 91 : 177, wanted.ToString(CultureInfo.InvariantCulture));
            }

            // The edges of its lines in the edge flags (172), as AutoCAD 2021's InsertRowsAndInherit copied 172 9 with the top and
            // left of the cell (2, 1) (D28c i1, i2).
            int edges = 0;

            for (int edge = 0; edge < 4; edge++)
            {
                edges |= (look.Whole(172, 0) & (1 << edge)) != 0 && (lookFlags & CellView.EdgeFlags(edge)) != 0 ? 1 << edge : 0;
            }

            cell = new CellView(cell.Groups, 0, cell.Groups.List.Count);

            if (cell.Whole(172, 0) != edges)
            {
                cell.SetHead(172, edges.ToString(CultureInfo.InvariantCulture));
            }

            return cell.Groups.List;
        }

        /// <summary>A cell's groups without the pointer to a field (344), the field being the cell's it was copied from.</summary>
        private static List<KeyValuePair<int, string>> WithoutField(List<KeyValuePair<int, string>> groups)
        {
            if (HeadText(groups, 344) == null)
            {
                return groups;
            }

            CellView cell = View(groups);
            cell.DropField();
            return cell.Groups.List;
        }

        private static string HeadText(List<KeyValuePair<int, string>> groups, int code)
        {
            for (int i = 1; i < groups.Count && !(groups[i].Key == 301 && groups[i].Value.Trim() == "CELL_VALUE"); i++)
            {
                if (groups[i].Key == code)
                {
                    return groups[i].Value;
                }
            }

            return null;
        }

        /// <summary>The sizes of rows (or columns) as the record writes them, cut or carried on to a count by the size of the last.</summary>
        private static List<string> Sizes(Layout layout, List<int> places, int count)
        {
            var sizes = new List<string>();

            for (int k = 0; k < count; k++)
            {
                sizes.Add(layout.Groups.List[places[Math.Min(k, places.Count - 1)]].Value);
            }

            return sizes;
        }

        private static IEnumerable<string> Repeat(string text, int count)
        {
            for (int k = 0; k < count; k++)
            {
                yield return text;
            }
        }

        private static void CheckPlace(int index, int count, string parameter, string what)
        {
            if (index < 0 || index > count)
            {
                throw new ArgumentOutOfRangeException(parameter, index, "New " + what + "s go before one of the table's " + count.ToString(CultureInfo.InvariantCulture) + ", from nought, or after the last at " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        /// <summary>Refuses a table of more cells than a whole number of 32 bits counts, which no list of the library holds.</summary>
        private static void CheckCells(long rows, long columns, string parameter)
            => Guard.AtMost(rows * columns, int.MaxValue, parameter, "A table holds at most " + int.MaxValue.ToString(CultureInfo.InvariantCulture) + " cells: " + rows.ToString(CultureInfo.InvariantCulture) + " rows of " + columns.ToString(CultureInfo.InvariantCulture) + " columns are more.");

        private static void CheckCount(int count, string parameter)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(parameter, count, "The count has to be one or more.");
            }
        }

        private static void CheckTaken(int index, int count, int of, string parameter, string countParameter, string what)
        {
            CheckCount(count, countParameter);

            if (index < 0 || (long)index + count > of || count >= of)
            {
                throw new ArgumentOutOfRangeException(parameter, index, count.ToString(CultureInfo.InvariantCulture) + " " + what + "s from " + index.ToString(CultureInfo.InvariantCulture) + " are not of the table's " + of.ToString(CultureInfo.InvariantCulture) + " with one " + what + " at least left.");
            }
        }

        private static void CheckRange(DxfCellRange range, Layout layout, string parameter)
        {
            if (range == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (range.BottomRow >= layout.Rows || range.RightColumn >= layout.Columns)
            {
                throw new ArgumentOutOfRangeException(parameter, range, "The range " + range + " reaches past the table's " + layout.Rows.ToString(CultureInfo.InvariantCulture) + " rows of " + layout.Columns.ToString(CultureInfo.InvariantCulture) + " columns.");
            }
        }

        #endregion
    }
}
