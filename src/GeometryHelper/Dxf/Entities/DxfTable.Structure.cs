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
        /// go above moves down with its rows, and so does a range of cells linked to data. The record is written once, its
        /// cells anew, so that 16 000 rows go in in time linear in its groups; the block AutoCAD drew the table with is not drawn
        /// anew. The table of rows 10, 8 and 8 given two rows 6 high before its row 2 has rows 10, 8, 6, 6 and 8, its cell B-12
        /// now in row 4.
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
        /// its column holds of its own: its overrides with their flags (177, or 91 from R2007 on), its turn (145), and from
        /// R2007 on the unit (94) and format (300) of its value; not its content, nor its merging.
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
        /// the content and look of its top left cell go to the first cell of it left, as AutoCAD keeps a merged cell's text. A
        /// block below the rows moves up. A range of cells linked to data shrinks and moves alike, its link named by its new top
        /// left cell, and goes where none of it is left, its DATALINK erased as <see cref="RemoveDataLink()"/> erases one: where it
        /// is anonymous and nothing else of the file points at it. The record is written once, its cells anew; the block AutoCAD
        /// drew the table with is not drawn anew. The table of rows 10, 8 and 8 that loses its row 1 has rows 10 and 8, its cell
        /// B-12 now in row 1.
        /// </summary>
        /// <param name="row">The first row taken out, from nought.</param>
        /// <param name="rows">How many, one or more, one row at least left.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the rows are not of the table's, or none or all of them are taken out.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void DeleteRows(int row, int rows)
        {
            Layout layout = Structured();
            CheckTaken(row, rows, layout.Rows, nameof(row), nameof(rows), "row");
            DeleteLines(layout, true, row, rows);
        }

        /// <summary>
        /// Puts columns in, as AutoCAD's <c>InsertColumns</c>: as many columns as asked, of the width given, before a column
        /// (after the last for <see cref="NumColumns"/>), as <see cref="InsertRows"/> puts rows in, the count (92) and the
        /// widths (142) written with them; the title merged across the columns grows by those put in among them. The table of
        /// columns 40 and 30 given one 25 wide before its column 1 has columns 40, 25 and 30, its title merged across the three.
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
        /// <see cref="InsertRowsAndInherit"/> puts rows in, the columns of the width of the column given.
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
        /// is unmerged, its text Parts in the one cell left.
        /// </summary>
        /// <param name="col">The first column taken out, from nought.</param>
        /// <param name="columns">How many, one or more, one column at least left.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the columns are not of the table's, or none or all of them are taken out.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table's record does not hold its cells, heights and widths, or the table is erased.</exception>
        public void DeleteColumns(int col, int columns)
        {
            Layout layout = Structured();
            CheckTaken(col, columns, layout.Columns, nameof(col), nameof(columns), "column");
            DeleteLines(layout, false, col, columns);
        }

        /// <summary>
        /// Gives the table a number of rows and of columns, as AutoCAD's <c>SetSize</c>: rows and columns taken out from the
        /// bottom and the right, or put in there, each new row as high as the last row and each new column as wide as the last
        /// column, of empty text cells; blocks of merged cells and ranges linked to data cut where they reach past the new size.
        /// Nothing is written where the table is of that size. The table of 3 rows and 2 columns given 5 and 2 has two more rows
        /// 8 high.
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
            bool valued = Item.File.Supports(1021);
            var cells = new List<KeyValuePair<int, string>>[numRows * numCols];

            for (int r = 0; r < numRows; r++)
            {
                for (int c = 0; c < numCols; c++)
                {
                    cells[r * numCols + c] = r < rows && c < columns ? Slice(layout, r * columns + c) : NewCell(valued);
                }
            }

            var blocks = new List<DxfCellRange>();

            foreach (DxfCellRange b in Blocks(layout))
            {
                if (b.TopRow < numRows && b.LeftColumn < numCols)
                {
                    AddBlock(blocks, b.TopRow, b.LeftColumn, Math.Min(b.BottomRow, numRows - 1), Math.Min(b.RightColumn, numCols - 1));
                }
            }

            Rebuild(layout, numRows, numCols, heights, widths, cells, blocks, LinksCut(layout, numRows, numCols));
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
                        cell = Slice(layout, oldRow * columns + oldColumn);
                    }
                    else if (inheritFrom >= 0)
                    {
                        cell = Inherited(rowsOf ? layout.CellAt(inheritFrom, c) : layout.CellAt(r, inheritFrom), valued);
                    }
                    else
                    {
                        cell = NewCell(valued);
                    }

                    cells[r * newColumns + c] = cell;
                }
            }

            var blocks = new List<DxfCellRange>();

            foreach (DxfCellRange b in Blocks(layout))
            {
                int first = rowsOf ? b.TopRow : b.LeftColumn;
                int last = rowsOf ? b.BottomRow : b.RightColumn;
                int newFirst = at <= first ? first + count : first;
                int newLast = at <= last ? last + count : last;
                AddBlock(blocks, rowsOf ? newFirst : b.TopRow, rowsOf ? b.LeftColumn : newFirst, rowsOf ? newLast : b.BottomRow, rowsOf ? b.RightColumn : newLast);
            }

            Rebuild(layout, newRows, newColumns, heights, widths, cells, blocks, LinksInserted(layout, rowsOf, at, count));
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

                // The block's top left cell taken out, its content and look go to the first cell of it left, as AutoCAD keeps
                // a merged cell's text.
                if (first >= at && first < at + count)
                {
                    int newRow = rowsOf ? newFirst : b.TopRow;
                    int newColumn = rowsOf ? b.LeftColumn : newFirst;
                    cells[newRow * newColumns + newColumn] = Slice(layout, b.TopRow * columns + b.LeftColumn);
                }

                AddBlock(blocks, rowsOf ? newFirst : b.TopRow, rowsOf ? b.LeftColumn : newFirst, rowsOf ? newLast : b.BottomRow, rowsOf ? b.RightColumn : newLast);
            }

            Rebuild(layout, newRows, newColumns, heights, widths, cells, blocks, LinksDeleted(layout, rowsOf, at, count));
        }

        #endregion

        #region Merging

        /// <summary>
        /// Merges a block of cells into one, as AutoCAD's <c>MergeCells</c>: each cell of it marked merged (173 of 1), its top
        /// left cell given the block's width and height in cells (175, 176), the others 0 and 0, as <see cref="GetMergeRange"/>
        /// reads them; the content of its top left cell is the block's, the others' kept in the file as they are, unseen. A
        /// block of merged cells within the range becomes part of it. The title of the table of 3 rows and 2 columns, merged with
        /// the row below it, (0, 0) to (1, 1), gives that range from each of its four cells. Nothing is written for a range of
        /// one cell, or one merged so already; the block AutoCAD drew the table with is not drawn anew.
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
                Rebuild(layout, layout.Rows, layout.Columns, null, null, AllSlices(layout), blocks, Links(layout));
            }
        }

        /// <summary>
        /// Unmerges the blocks of merged cells a range meets, as AutoCAD's <c>UnmergeCells</c>: each cell of them marked
        /// unmerged (173, 175 and 176 of 0), each showing its own content again; nothing where the range meets no block. The
        /// block AutoCAD drew the table with is not drawn anew.
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
        /// Fills cells from others, as AutoCAD's <c>Fill</c> does when a cell's grip is dragged: down the columns of the source
        /// range, or along its rows with <see cref="DxfTableFillOptions.Row"/>, each cell of the fill range outside the source
        /// given a copy of the source cell its place repeats, content and look, or with
        /// <see cref="DxfTableFillOptions.GenerateSeries"/> the numbers of the source carried on: a source of one number goes up
        /// by one, 1 filled to 2, 3, 4, and one of several by the step from its first to its last, 10 and 20 filled to 30, 40,
        /// each rounded to the places of decimals of the most precise source and of the step, and to 15 significant figures:
        /// 0.1 and 0.2 filled to 0.3, not 0.30000000000000004; 1, 2 and 4, a step of 1.5, to 5.5, 7, 8.5; 1E-20 and 2E-20 to 3E-20.
        /// A cell above (or left of) the source carries the series back, 10 and 20 to 0 above them. With
        /// <see cref="DxfTableFillOptions.Reverse"/> the step goes the other way, 10 and 20 filled down to 10, 0, -10 from the
        /// source's end, 5 to 4, 3, and the cells copied repeat the source backwards. The numbers are those of a whole number or
        /// a number value, or before R2007 a text that reads as one (from R2007 on a text is a text, whatever it reads),
        /// written of the kind they are, a whole number where every source is one and so is the step; a source of a text that
        /// is no number is copied, series or not.
        /// <see cref="DxfTableFillOptions.CopyContent"/> alone copies the content, <see cref="DxfTableFillOptions.CopyFormat"/>
        /// alone the look; the merges of the table are kept. A date is not carried on: the library knows no groups of a date
        /// value. The record is written once; the block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="fillRange">The cells filled, of the source's columns (or rows), within the table.</param>
        /// <param name="sourceRange">The cells filled from, within the table.</param>
        /// <param name="options">How, of the flags of <see cref="DxfTableFillOptions"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when a range is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a range reaches past the table, or the options hold a flag none of <see cref="DxfTableFillOptions"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the fill range is not of the source's columns, or along the rows of its rows.</exception>
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

            if (along ? fillRange.TopRow != sourceRange.TopRow || fillRange.BottomRow != sourceRange.BottomRow : fillRange.LeftColumn != sourceRange.LeftColumn || fillRange.RightColumn != sourceRange.RightColumn)
            {
                throw new ArgumentException("The fill range " + fillRange + " is of the " + (along ? "rows" : "columns") + " of the source " + sourceRange + ", which it fills " + (along ? "along" : "down") + ".", nameof(fillRange));
            }

            bool reverse = (options & DxfTableFillOptions.Reverse) != 0;
            bool series = (options & DxfTableFillOptions.GenerateSeries) != 0;
            bool onlyContent = (options & DxfTableFillOptions.CopyContent) != 0 && (options & DxfTableFillOptions.CopyFormat) == 0;
            bool onlyLook = (options & DxfTableFillOptions.CopyFormat) != 0 && (options & DxfTableFillOptions.CopyContent) == 0;
            List<KeyValuePair<int, string>>[] cells = AllSlices(layout);
            int count = along ? sourceRange.RightColumn - sourceRange.LeftColumn + 1 : sourceRange.BottomRow - sourceRange.TopRow + 1;
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
                    sources[k] = along ? layout.CellAt(sourceRange.TopRow + line, sourceRange.LeftColumn + k) : layout.CellAt(sourceRange.TopRow + k, sourceRange.LeftColumn + line);
                    numeric &= NumberOf(sources[k], out numbers[k], out bool isWhole, out int decimals);
                    whole &= isWhole;
                    places = Math.Max(places, decimals);
                }

                // The step from the first to the last, one for a source of one number; the other way round in reverse.
                double by = (count == 1 ? 1.0 : (numbers[count - 1] - numbers[0]) / (count - 1)) * (reverse ? -1.0 : 1.0);
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
                int start = along ? sourceRange.LeftColumn : sourceRange.TopRow;
                int end = start + count - 1;
                int from = along ? fillRange.LeftColumn : fillRange.TopRow;
                int to = along ? fillRange.RightColumn : fillRange.BottomRow;

                for (int place = from; place <= to; place++)
                {
                    if (place >= start && place <= end)
                    {
                        continue;
                    }

                    // Past the source's end the series goes on from its last cell, before its start back from its first; the
                    // cells copied repeat it onwards, or backwards in reverse.
                    bool after = place > end;
                    int steps = after ? place - end : start - place;
                    int index = after ? (reverse ? count - 1 - steps : steps - 1) : (reverse ? steps : count - steps);
                    double value = after ? numbers[count - 1] + steps * by : numbers[0] - steps * by;
                    int r = along ? sourceRange.TopRow + line : place;
                    int c = along ? place : sourceRange.LeftColumn + line;
                    CellView source = sources[((index % count) + count) % count];
                    List<KeyValuePair<int, string>> target = cells[r * layout.Columns + c];
                    List<KeyValuePair<int, string>> made;

                    if (onlyLook)
                    {
                        made = WithLook(target, source);
                    }
                    else
                    {
                        made = WithoutField(Slice(source));

                        if (series && numeric)
                        {
                            double rounded = integral && Exact(after ? (long)numbers[count - 1] : (long)numbers[0], after ? steps : -steps, (long)by, out long exact) ? exact : Rounded(value, places);
                            made = WithNumber(made, rounded, whole && rounded >= int.MinValue && rounded <= int.MaxValue);
                        }

                        made = onlyContent ? WithLook(made, View(target)) : made;
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

        /// <summary>A cell's groups given a number: of its kind in its value from R2007 on, else as its text.</summary>
        private static List<KeyValuePair<int, string>> WithNumber(List<KeyValuePair<int, string>> groups, double value, bool whole)
        {
            CellView cell = View(groups);
            Written written = whole ? Whole((int)value) : Real(value, nameof(value));

            if (cell.ValueFrom >= 0)
            {
                cell.WriteValue(((int)written.Kind).ToString(CultureInfo.InvariantCulture), written.Data, written.Text, null, null);
            }
            else
            {
                cell.SetText(written.Text);
            }

            return cell.Groups.List;
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
        /// left out. Null for a cell of no field, or of a field of another kind, as a date. The library does not work a formula
        /// out: the cell's value is the one AutoCAD last wrote.
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
            return string.IsNullOrEmpty(handle) || !IsResident ? null : FormulaOf(Item.File, handle, true);
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
        /// 175 and 176 of 1 among a block's cells as AutoCAD's own as one of 0 and 0.
        /// </summary>
        private static List<KeyValuePair<int, string>> Merged(List<KeyValuePair<int, string>> groups, int merged, int width, int height)
        {
            int nowMerged = HeadWhole(groups, 173);
            int nowWidth = HeadWhole(groups, 175);
            int nowHeight = HeadWhole(groups, 176);
            bool cornerNow = nowMerged != 0 && (nowWidth > 1 || nowHeight > 1);
            bool corner = width > 1 || height > 1;

            if (corner ? nowMerged != 0 && Math.Max(1, nowWidth) == width && Math.Max(1, nowHeight) == height : !cornerNow && (nowMerged != 0) == (merged != 0))
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
        /// The blocks of merged cells of the table, row by row by their top left cells: each cell merged (173) of a width or a
        /// height (175, 176) above one, as <see cref="GetMergeRange"/> reads it, cut at the table's last row and column; one that
        /// meets a block found before it is left out.
        /// </summary>
        private static List<DxfCellRange> Blocks(Layout layout)
        {
            var blocks = new List<DxfCellRange>();
            var covered = new bool[layout.Rows * layout.Columns];

            for (int r = 0; r < layout.Rows; r++)
            {
                for (int c = 0; c < layout.Columns; c++)
                {
                    CellView cell = layout.CellAt(r, c);

                    if (covered[r * layout.Columns + c] || cell.Whole(173, 0) == 0)
                    {
                        continue;
                    }

                    int width = Math.Max(1, cell.Whole(175, 0));
                    int height = Math.Max(1, cell.Whole(176, 0));
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
        /// R2007 on its value of an empty text between <c>301 CELL_VALUE</c> and <c>304 ACVALUE_END</c>.
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
            cell.Add(new KeyValuePair<int, string>(93, "6"));
            cell.Add(new KeyValuePair<int, string>(90, "4"));
            cell.Add(new KeyValuePair<int, string>(1, string.Empty));
            cell.Add(new KeyValuePair<int, string>(94, "0"));
            cell.Add(new KeyValuePair<int, string>(300, string.Empty));
            cell.Add(new KeyValuePair<int, string>(302, string.Empty));
            cell.Add(new KeyValuePair<int, string>(304, "ACVALUE_END"));
            return cell;
        }

        /// <summary>
        /// An empty text cell with the look a cell holds of its own: its overrides with their flags, its turn (145), and from
        /// R2007 on the unit (94) and format (300) of its value.
        /// </summary>
        private static List<KeyValuePair<int, string>> Inherited(CellView source, bool valued)
        {
            List<KeyValuePair<int, string>> cell = NewCell(valued);
            string turn = source.Head(145);

            if (turn != null)
            {
                cell[8] = new KeyValuePair<int, string>(145, turn);
            }

            if (valued && source.ValueFrom >= 0)
            {
                string unit = source.InValue(94);
                string format = source.InValue(300);
                int at = cell.FindIndex(g => g.Key == 94);
                cell[at] = new KeyValuePair<int, string>(94, unit ?? "0");
                cell[at + 1] = new KeyValuePair<int, string>(300, format ?? string.Empty);
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
        /// A cell's groups with the look of another: its own overrides and their flags taken out, the other's put in after its
        /// content with their flags, its flags of nothing else (as its kind of value, 0x40000) kept.
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

            for (int i = look.OverridesFrom; i < look.To; i++)
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
