using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The edits of a table's cells, as AutoCAD's Table Cell tab and the right-click menu of a cell make them: the text of a
    /// cell, rows and columns put in and taken out, cells merged and unmerged, and the alignment, fill and borders of cells.
    /// Each is one undo step through <see cref="CadDocument.ApplyEdit"/>, asked of the gate as a change of the table's
    /// properties, and draws the table anew in its block (<see cref="DxfTable.RecomputeTableBlock"/>).
    /// </summary>
    /// <remarks>
    /// The schedule of a title, a header and four rows of five columns, given a row below its row 3, has seven rows, the new
    /// one of the look of row 3 and empty; its cells B3 to C4 merged read as one cell, its text that of B3.
    /// </remarks>
    public static class CadTableEditor
    {
        /// <summary>Gets the name AutoCAD gives a cell: its column's letters and its row's number from 1, as C3 for the row 2 and the column 2.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The name.</returns>
        public static string CellName(int row, int column) =>
            ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the letters AutoCAD gives a column: A to Z, then AA, AB and on.</summary>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The letters.</returns>
        public static string ColumnName(int column)
        {
            string name = string.Empty;

            for (int n = column + 1; n > 0; n = (n - 1) / 26)
            {
                name = (char)('A' + (n - 1) % 26) + name;
            }

            return name;
        }

        /// <summary>Gets the cell of a table a point of the plan falls in, seen from above; false where it falls in none.</summary>
        /// <param name="table">The table.</param>
        /// <param name="point">The point.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>Whether it falls in a cell.</returns>
        public static bool TryHit(DxfTable table, GeoPoint2 point, out int row, out int column)
        {
            row = -1;
            column = -1;

            if (table == null)
            {
                return false;
            }

            DxfTableHitTestInfo hit = table.HitTest(
                new GeoPoint3(point.X, point.Y, 0.0),
                new GeoVector3(0.0, 0.0, -1.0)
            );

            if (hit.Type != DxfTableHitTestType.Cell)
            {
                return false;
            }

            row = hit.Row;
            column = hit.Column;
            return true;
        }

        /// <summary>Gets the table of a space a point of the plan falls in, and its cell, the table appended last first; false where it falls in none.</summary>
        /// <param name="space">The space.</param>
        /// <param name="point">The point.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>Whether it falls in a cell.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space is null.</exception>
        public static bool TryCellAt(CadSpace space, GeoPoint2 point, out string handle, out int row, out int column)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            handle = null;
            row = -1;
            column = -1;
            var tables = new List<DxfTable>();

            foreach (DxfEntity entity in space.Block)
            {
                if (entity is DxfTable table)
                {
                    tables.Add(table);
                }
            }

            for (int k = tables.Count - 1; k >= 0; k--)
            {
                if (TryHit(tables[k], point, out row, out column))
                {
                    handle = tables[k].Handle;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gets the corners of a range of cells, the top left first, then about it, in the plan.</summary>
        /// <param name="table">The table.</param>
        /// <param name="range">The range, each of its cells grown to the block of merged cells it is in.</param>
        /// <returns>The four corners.</returns>
        public static GeoPoint2[] Corners(DxfTable table, DxfCellRange range)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            // Each corner from the cell at that corner of the range, so that a range of several rows is a rectangle.
            IReadOnlyList<GeoPoint3> topLeft = table.GetCellExtents(range.TopRow, range.LeftColumn, true);
            IReadOnlyList<GeoPoint3> topRight = table.GetCellExtents(range.TopRow, range.RightColumn, true);
            IReadOnlyList<GeoPoint3> bottomRight = table.GetCellExtents(range.BottomRow, range.RightColumn, true);
            IReadOnlyList<GeoPoint3> bottomLeft = table.GetCellExtents(range.BottomRow, range.LeftColumn, true);
            return new[] { P(topLeft[0]), P(topRight[1]), P(bottomRight[2]), P(bottomLeft[3]) };
        }

        /// <summary>Gives a range of cells grown to take in every block of merged cells it meets, as a selection of cells takes them.</summary>
        /// <param name="table">The table.</param>
        /// <param name="range">The range.</param>
        /// <returns>The range grown.</returns>
        public static DxfCellRange Grown(DxfTable table, DxfCellRange range)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            int top = range.TopRow,
                left = range.LeftColumn,
                bottom = range.BottomRow,
                right = range.RightColumn;
            bool grew = true;

            while (grew)
            {
                grew = false;

                for (int r = top; r <= bottom; r++)
                {
                    for (int c = left; c <= right; c++)
                    {
                        DxfCellRange block = table.GetMergeRange(r, c);

                        if (
                            block.TopRow < top
                            || block.LeftColumn < left
                            || block.BottomRow > bottom
                            || block.RightColumn > right
                        )
                        {
                            top = Math.Min(top, block.TopRow);
                            left = Math.Min(left, block.LeftColumn);
                            bottom = Math.Max(bottom, block.BottomRow);
                            right = Math.Max(right, block.RightColumn);
                            grew = true;
                        }
                    }
                }
            }

            return new DxfCellRange(top, left, bottom, right);
        }

        /// <summary>
        /// Writes the text of a cell, as typing in it does; the same text changes nothing. A number typed, spaces round it left
        /// out, is kept as a number unless the cell is made to hold text, as AutoCAD 2021 reads a text it parses into a value
        /// (<c>ConvertTextToValue</c>): 1 is the whole number 1 and 007 the whole number 7, so that the autofill grip dragged
        /// from it counts 2, 3, 4; 1.5 a number shown in the drawing's units, 1.5000 at LUPREC 4, 1e3 one shown 1.0000E+03 and
        /// 1/2 one shown 1/2, each standing top right where the cell has no alignment of its own; 1,5 the point (1, 5, 0)
        /// shown 1.0000, 5.0000, 0.0000; and a number typed in a cell of a number keeps the cell's kind and format, 12 over
        /// 1.25 shown 12.0000. A date or a time, in the order of the culture of the machine, is a date as AutoCAD 2021 reads it:
        /// 1/2/2020 or 12:30 of a cell of no kind, shown 1/2/2020 or 12:30:00 PM (the formats <c>%x</c>, <c>%X</c> and <c>%c</c>
        /// for a date, a time and both), and any other words over a cell of a date are refused. b1 and 12mm stay text.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="text">The text, with its MTEXT codes; empty to clear the cell.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetText(CadDocument document, string handle, int row, int column, string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (!InTable(table, row, column))
            {
                return CadEditResult.Refuse("The table has no cell " + CellName(row, column) + ".");
            }

            string old = table.TextString(row, column);

            // The words the cell shows, or those the editor opened it with (1.5 for 1.5000), change nothing.
            if (
                string.Equals(old, text, StringComparison.Ordinal)
                || string.Equals(table.TextStringForEditing(row, column), text, StringComparison.Ordinal)
            )
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            // A text that is not a number over a number, or a number over a point, as AutoCAD 2021 refuses it: the cell keeps its
            // value. Empty words clear the cell, as Delete does.
            if (text.Length != 0 && table.RefusesText(row, column, text))
            {
                DxfDataType held = table.DataType(row, column);
                string kind =
                    held == DxfDataType.Point3d ? "point"
                    : held == DxfDataType.Date ? "date"
                    : "number";
                return CadEditResult.Refuse(
                    "Cell "
                        + CellName(row, column)
                        + " holds a "
                        + kind
                        + ", and \""
                        + text
                        + "\" is not a "
                        + kind
                        + "."
                );
            }

            // A whole number, as AutoCAD 2021's SetValue of a text with ConvertTextToValue made 1 and 007 the numbers 1 and 7
            // and left a cell made to hold text with its text, where the cell takes no value read from the text (a file before
            // R2007, whose cell holds its text alone).
            int number = 0;
            bool whole =
                table.DataType(row, column) != DxfDataType.String
                && int.TryParse(
                    text,
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out number
                );

            return Edit(document, handle, "Cell " + CellName(row, column), old, text, t =>
            {
                if (text.Length == 0)
                {
                    t.ResetValue(row, column);
                }
                else if (!t.SetValueFromText(row, column, text))
                {
                    if (whole)
                    {
                        t.SetValue(row, column, number);
                    }
                    else
                    {
                        t.SetTextString(row, column, text);
                    }
                }
            });
        }

        /// <summary>Empties the cells of a range, as Delete and Delete All Contents do: their text or block taken, their look kept.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <returns>What came of it; nothing written where every cell is empty.</returns>
        public static CadEditResult ClearContents(CadDocument document, string handle, DxfCellRange range)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            var cells = new List<int[]>();

            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    if (!table.IsEmpty(r, c))
                    {
                        cells.Add(new[] { r, c });
                    }
                }
            }

            if (cells.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return Edit(document, handle, "Delete All Contents", Name(range), null, t =>
            {
                foreach (int[] cell in cells)
                {
                    t.ResetValue(cell[0], cell[1]);
                }
            });
        }

        /// <summary>Puts in a row above or below a row, of its look, as Insert Above and Insert Below do.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The row selected, from nought.</param>
        /// <param name="below">Whether the new row goes below it.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult InsertRow(CadDocument document, string handle, int row, bool below)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            int at = below ? row + 1 : row;

            if (row < 0 || row >= table.NumRows || !table.CanInsertRow(at))
            {
                return CadEditResult.Refuse(
                    "A row cannot be put in "
                        + (below ? "below" : "above")
                        + " row "
                        + (row + 1).ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }

            return Edit(
                document,
                handle,
                below ? "Insert Below" : "Insert Above",
                null,
                (row + 1).ToString(CultureInfo.InvariantCulture),
                t => t.InsertRowsAndInherit(at, row, 1)
            );
        }

        /// <summary>Takes out rows, as Delete Row(s) does; refused where no row would be left.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The first row, from nought.</param>
        /// <param name="count">How many.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult DeleteRows(CadDocument document, string handle, int row, int count)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (count < 1 || row < 0 || row + count > table.NumRows || !table.CanDeleteRows(row, count))
            {
                return CadEditResult.Refuse("Those rows cannot be deleted: a table keeps a row at least.");
            }

            return Edit(document, handle, "Delete Rows", (row + 1).ToString(CultureInfo.InvariantCulture), null, t =>
                t.DeleteRows(row, count)
            );
        }

        /// <summary>Puts in a column left or right of a column, of its look, as Insert Left and Insert Right do.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="column">The column selected, from nought.</param>
        /// <param name="right">Whether the new column goes right of it.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult InsertColumn(CadDocument document, string handle, int column, bool right)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            int at = right ? column + 1 : column;

            if (column < 0 || column >= table.NumColumns || !table.CanInsertColumn(at))
            {
                return CadEditResult.Refuse(
                    "A column cannot be put in " + (right ? "right" : "left") + " of column " + ColumnName(column) + "."
                );
            }

            return Edit(document, handle, right ? "Insert Right" : "Insert Left", null, ColumnName(column), t =>
                t.InsertColumnsAndInherit(at, column, 1)
            );
        }

        /// <summary>Takes out columns, as Delete Column(s) does; refused where no column would be left.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="column">The first column, from nought.</param>
        /// <param name="count">How many.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult DeleteColumns(CadDocument document, string handle, int column, int count)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (count < 1 || column < 0 || column + count > table.NumColumns || !table.CanDeleteColumns(column, count))
            {
                return CadEditResult.Refuse("Those columns cannot be deleted: a table keeps a column at least.");
            }

            return Edit(document, handle, "Delete Columns", ColumnName(column), null, t =>
                t.DeleteColumns(column, count)
            );
        }

        /// <summary>Merges the cells of a range into one, as Merge All does: its text that of its top left cell.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells, two or more.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Merge(CadDocument document, string handle, DxfCellRange range)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            if (range.TopRow == range.BottomRow && range.LeftColumn == range.RightColumn)
            {
                return CadEditResult.Refuse("Select two cells or more to merge.");
            }

            return Edit(document, handle, "Merge Cells", null, Name(range), t =>
                t.MergeCells(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn))
            );
        }

        /// <summary>Unmerges every block of merged cells a range meets, as Unmerge Cells does.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <returns>What came of it; nothing written where it meets no block.</returns>
        public static CadEditResult Unmerge(CadDocument document, string handle, DxfCellRange range)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            bool any = false;

            for (int r = range.TopRow; r <= range.BottomRow && !any; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn && !any; c++)
                {
                    any = table.IsMergedCell(r, c, out _);
                }
            }

            if (!any)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return Edit(document, handle, "Unmerge Cells", Name(range), null, t =>
                t.UnmergeCells(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn))
            );
        }

        /// <summary>Sets where the content of the cells of a range stands, as the alignment of Cell Styles does.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="alignment">The alignment.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetAlignment(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfCellAlignment alignment
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            return Edit(document, handle, "Alignment", Name(range), alignment.ToString(), t =>
                Each(range, (r, c) => t.SetAlignment(r, c, alignment))
            );
        }

        /// <summary>Fills the cells of a range with a colour, or leaves them unfilled, as the fill of Cell Styles does.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="color">The colour, of the index, by layer or by block; null for none.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetBackground(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfColor color
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            if (color != null && color.ColorMethod != DxfColorMethod.ByAci && !color.IsByBlock && !color.IsByLayer)
            {
                return CadEditResult.Refuse("A cell is filled with a colour of AutoCAD's index.");
            }

            return Edit(
                document,
                handle,
                "Background fill",
                Name(range),
                color == null ? "None" : color.ColorIndex.ToString(CultureInfo.InvariantCulture),
                t => Each(range, (r, c) =>
                    {
                        if (color == null)
                        {
                            t.SetBackgroundColorNone(r, c, true);
                        }
                        else
                        {
                            // The colour, and the fill turned on (283 0), as AutoCAD writes a cell filled.
                            t.SetBackgroundColor(r, c, color);
                            t.SetBackgroundColorNone(r, c, false);
                        }
                    })
            );
        }

        /// <summary>
        /// Draws or hides lines of a range of cells, as the border buttons do: All borders, Outside borders, Inside borders, the
        /// bottom, left, top or right, or none (<see cref="DxfGridLineType.AllGridLines"/> hidden).
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="lines">The lines of the range, one or more of the six.</param>
        /// <param name="visible">Whether they are drawn.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetBorders(
            CadDocument document,
            string handle,
            DxfCellRange range,
            DxfGridLineType lines,
            bool visible
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            // A range of one row or column has no lines inside it of that way.
            DxfGridLineType inRange = lines;

            if (range.TopRow == range.BottomRow)
            {
                inRange &= ~DxfGridLineType.HorizontalInside;
            }

            if (range.LeftColumn == range.RightColumn)
            {
                inRange &= ~DxfGridLineType.VerticalInside;
            }

            if (inRange == DxfGridLineType.InvalidGridLine)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            var property = new DxfGridPropertyParameter
            {
                PropertyMask = DxfGridProperties.Visibility,
                Visible = visible,
            };
            return Edit(document, handle, "Cell borders", Name(range), (visible ? "Show " : "Hide ") + inRange, t =>
                t.SetGridProperty(
                    new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn),
                    inRange,
                    property
                )
            );
        }

        /// <summary>
        /// Fills cells from others, as the autofill grip of a cell does: <see cref="DxfTable.Fill"/> in one undo step,
        /// "Autofill". A 1 in A3 filled down into A4 to A6 with <see cref="DxfTableFillOptions.GenerateSeries"/> and
        /// <see cref="DxfTableFillOptions.CopyFormat"/>, as Fill Series fills, gives 2, 3 and 4, as AutoCAD 2021 filled them.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="fillRange">The cells filled.</param>
        /// <param name="sourceRange">The cells filled from.</param>
        /// <param name="options">How, of the flags of <see cref="DxfTableFillOptions"/>.</param>
        /// <returns>What came of it: refused where a range is not all of the table, or the fill range holds rows (columns, along
        /// the rows) of the source.</returns>
        public static CadEditResult Fill(
            CadDocument document,
            string handle,
            DxfCellRange fillRange,
            DxfCellRange sourceRange,
            DxfTableFillOptions options
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, fillRange, out refused) || !InTable(table, sourceRange, out refused))
            {
                return refused;
            }

            bool along = (options & DxfTableFillOptions.Row) != 0;
            int from = along ? fillRange.LeftColumn : fillRange.TopRow;
            int to = along ? fillRange.RightColumn : fillRange.BottomRow;
            int start = along ? sourceRange.LeftColumn : sourceRange.TopRow;
            int end = along ? sourceRange.RightColumn : sourceRange.BottomRow;

            if (from <= end && to >= start)
            {
                return CadEditResult.Refuse("The cells " + Name(fillRange) + " meet the cells filled from.");
            }

            return Edit(document, handle, "Autofill", "Autofill", Name(sourceRange), Name(fillRange), t =>
                t.Fill(fillRange, sourceRange, options)
            );
        }

        /// <summary>
        /// Sets the widths of columns, from one on, in one undo step, "Column Width", as a drag of a column's line does; the
        /// rows then grow to hold text that wraps anew. A width below <see cref="MinimumColumnWidth"/> is taken as that, as
        /// AutoCAD 2021 holds a column there: columns of 30 and 22.5 given 38 and 14.5 keep the table's width, and one given 1
        /// in AutoCAD's Standard is 9 wide.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="column">The first column, from nought.</param>
        /// <param name="widths">The widths of it and the columns after it.</param>
        /// <returns>What came of it; nothing written where every width is as it was.</returns>
        public static CadEditResult SetColumnWidths(
            CadDocument document,
            string handle,
            int column,
            IReadOnlyList<double> widths
        )
        {
            if (widths == null)
            {
                throw new ArgumentNullException(nameof(widths));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (column < 0 || widths.Count == 0 || column + widths.Count > table.NumColumns)
            {
                return CadEditResult.Refuse("The table has no such columns.");
            }

            double[] all = Sizes(table.NumColumns, table.ColumnWidth);

            for (int k = 0; k < widths.Count; k++)
            {
                if (!(widths[k] > 0.0) || double.IsInfinity(widths[k]))
                {
                    return CadEditResult.Refuse("A column's width is above nought.");
                }

                all[column + k] = Math.Max(widths[k], MinimumColumnWidth(table, column + k));
            }

            return Resize(document, handle, "Column Width", all, null, default);
        }

        /// <summary>
        /// Sets the heights of rows, from one on, in one undo step, "Row Height", as a drag of a row's line does; a row is never
        /// lower than its text needs, grown back to it as the table is drawn anew.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="row">The first row, from nought.</param>
        /// <param name="heights">The heights of it and the rows after it, each above nought.</param>
        /// <returns>What came of it; nothing written where every height is as it was.</returns>
        public static CadEditResult SetRowHeights(
            CadDocument document,
            string handle,
            int row,
            IReadOnlyList<double> heights
        )
        {
            if (heights == null)
            {
                throw new ArgumentNullException(nameof(heights));
            }

            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            if (row < 0 || heights.Count == 0 || row + heights.Count > table.NumRows)
            {
                return CadEditResult.Refuse("The table has no such rows.");
            }

            double[] all = Sizes(table.NumRows, table.RowHeight);

            for (int k = 0; k < heights.Count; k++)
            {
                if (!(heights[k] > 0.0) || double.IsInfinity(heights[k]))
                {
                    return CadEditResult.Refuse("A row's height is above nought.");
                }

                all[row + k] = heights[k];
            }

            return Resize(document, handle, "Row Height", null, all, default);
        }

        /// <summary>
        /// Takes away what the cells of a range hold of their own over the table style, as Remove All Property Overrides does
        /// (<see cref="DxfTable.RemoveAllOverrides"/>): their text, values and merges kept.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult RemoveOverrides(CadDocument document, string handle, DxfCellRange range)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            return Edit(document, handle, "TABLEDIT", "Remove All Property Overrides", Name(range), null, t =>
                Each(range, (r, c) => t.RemoveAllOverrides(r, c))
            );
        }

        /// <summary>
        /// Gets the cell of a table nearest a point of the plan: the cell it falls in, or where it falls outside the table, the
        /// cell of the last row or column on that side, as a drag across cells holds the range at the table's edge.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="point">The point.</param>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>False for a table of no cells.</returns>
        public static bool CellNear(DxfTable table, GeoPoint2 point, out int row, out int column)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            row = -1;
            column = -1;

            if (table.NumRows < 1 || table.NumColumns < 1)
            {
                return false;
            }

            GeoPoint2[] corners = Corners(table, new DxfCellRange(0, 0, 0, 0));
            GeoVector2 across = corners[1] - corners[0];
            GeoVector2 down = corners[3] - corners[0];
            across = across.Length > 0.0 ? across / across.Length : new GeoVector2(1.0, 0.0);
            down = down.Length > 0.0 ? down / down.Length : new GeoVector2(0.0, -1.0);
            GeoVector2 way = point - corners[0];
            row = Nearest(table.NumRows, table.RowHeight, way.X * down.X + way.Y * down.Y);
            column = Nearest(table.NumColumns, table.ColumnWidth, way.X * across.X + way.Y * across.Y);
            return true;
        }

        /// <summary>
        /// Gets the least width a column of a table may be dragged to, as AutoCAD 2021's <c>MinimumColumnWidth</c>: its two
        /// horizontal margins and the tallest text height of its cells. AutoCAD's Standard of margins 1.5 and a title of 6 gives
        /// 9.
        /// </summary>
        /// <param name="table">The table.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The width.</returns>
        public static double MinimumColumnWidth(DxfTable table, int column)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            var style = new Looks(table);
            double tallest = 0.0;

            for (int r = 0; r < table.NumRows; r++)
            {
                tallest = Math.Max(tallest, TextHeightOf(table, style, r, column, table.RowType(r)));
            }

            return 2.0 * table.HorizontalCellMargin + tallest;
        }

        /// <summary>
        /// Gets the height each row of a table needs to hold its cells, as <see cref="FitRows"/> grows them, a block of merged
        /// cells across rows left out: the least a drag of a row's line goes to.
        /// </summary>
        internal static double[] RowNeeds(CadDocument document, DxfTable table)
        {
            var needs = new double[table.NumRows];

            foreach (KeyValuePair<DxfCellRange, double> ask in RowAsks(document, table, new Looks(table)))
            {
                if (ask.Key.TopRow == ask.Key.BottomRow)
                {
                    needs[ask.Key.TopRow] = Math.Max(needs[ask.Key.TopRow], ask.Value);
                }
            }

            return needs;
        }

        /// <summary>
        /// What each block of merged cells (a cell alone is a block of one) asks of the rows it spans, in the order of its top left
        /// cell, row by row: its words as <see cref="CellNeeds"/> measures them, or a block cell's block as it stands (a block
        /// fitted to its cell asks nothing), the margins above and below in; a cell with no words asks nothing. The first cell of
        /// each row also asks, for its row alone, for one line of its text height, as AutoCAD 2021 lays a row out by the one line
        /// of its first cell (a first cell of 6 in a table whose data is 8 high makes its row 11, not the 13.66667 the cells
        /// beside it at 8 would, and a second cell of 12 holding one word makes it 15, its word and the margins).
        /// </summary>
        private static List<KeyValuePair<DxfCellRange, double>> RowAsks(
            CadDocument document,
            DxfTable table,
            Looks style
        )
        {
            double hMargin = table.HorizontalCellMargin;
            double vMargin = table.VerticalCellMargin;
            var asks = new List<KeyValuePair<DxfCellRange, double>>();

            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    DxfCellRange block = table.GetMergeRange(r, c);

                    if (block.TopRow != r || block.LeftColumn != c)
                    {
                        continue;
                    }

                    if (table.CellType(r, c) == DxfTableCellType.BlockCell)
                    {
                        if (table.TryBlockSize(r, c, out _, out double high))
                        {
                            asks.Add(new KeyValuePair<DxfCellRange, double>(block, high + 2.0 * vMargin));
                        }

                        continue;
                    }

                    double need = CellNeeds(
                        document,
                        table,
                        style,
                        r,
                        c,
                        block,
                        hMargin,
                        table.TextString(r, c),
                        false
                    );

                    if (need > 0.0)
                    {
                        asks.Add(new KeyValuePair<DxfCellRange, double>(block, need + 2.0 * vMargin));
                    }
                }

                // The first cell of the row, whatever it holds or a block of merged cells hides it, asks for one line of its text
                // height: the row stands at that least, as AutoCAD lays out a row by its first cell's.
                if (table.CellType(r, 0) != DxfTableCellType.BlockCell)
                {
                    double oneLine = TextHeightOf(table, style, r, 0, table.RowType(r)) * 4.0 / 3.0;
                    asks.Add(
                        new KeyValuePair<DxfCellRange, double>(new DxfCellRange(r, 0, r, 0), oneLine + 2.0 * vMargin)
                    );
                }
            }

            return asks;
        }

        /// <summary>What each block cell's block asks of the columns it spans: its width as it stands, the margins either side in, as <see cref="RowAsks"/> counts a height; the ranges turned, a row for a column, as <see cref="Wanted"/> takes them.</summary>
        private static List<KeyValuePair<DxfCellRange, double>> ColumnAsks(DxfTable table)
        {
            double hMargin = table.HorizontalCellMargin;
            var asks = new List<KeyValuePair<DxfCellRange, double>>();

            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    DxfCellRange block = table.GetMergeRange(r, c);

                    if (
                        block.TopRow == r
                        && block.LeftColumn == c
                        && table.CellType(r, c) == DxfTableCellType.BlockCell
                        && table.TryBlockSize(r, c, out double wide, out _)
                    )
                    {
                        asks.Add(
                            new KeyValuePair<DxfCellRange, double>(
                                new DxfCellRange(block.LeftColumn, block.TopRow, block.RightColumn, block.BottomRow),
                                wide + 2.0 * hMargin
                            )
                        );
                    }
                }
            }

            return asks;
        }

        /// <summary>
        /// Sets every width and height of a table and moves it along its plane, one undo step of the name given, as the drags of
        /// its lines and grips make them; null keeps the widths (or heights), and nothing is written where all are as they were.
        /// </summary>
        internal static CadEditResult Resize(
            CadDocument document,
            string handle,
            string step,
            double[] widths,
            double[] heights,
            GeoVector2 shift
        )
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null)
            {
                return refused;
            }

            double[] oldWidths = Sizes(table.NumColumns, table.ColumnWidth);
            double[] oldHeights = Sizes(table.NumRows, table.RowHeight);
            bool moves = shift.Length > 0.0;

            if (!moves && (widths == null || Same(widths, oldWidths)) && (heights == null || Same(heights, oldHeights)))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            string was = widths != null ? Join(oldWidths) : Join(oldHeights);
            string went = widths != null ? Join(widths) : Join(heights);
            return Edit(document, handle, step, step, was, went, t =>
            {
                if (moves)
                {
                    t.TransformBy(GeoTransform3.Translation(new GeoVector3(shift.X, shift.Y, 0.0)));
                }

                for (int c = 0; widths != null && c < widths.Length; c++)
                {
                    if (widths[c] != t.ColumnWidth(c))
                    {
                        t.SetColumnWidth(c, widths[c]);
                    }
                }

                for (int r = 0; heights != null && r < heights.Length; r++)
                {
                    if (heights[r] != t.RowHeight(r))
                    {
                        t.SetRowHeight(r, heights[r]);
                    }
                }
            });
        }

        /// <summary>One edit of a table, a step of TABLEDIT: planned as a change of its property, written, and its block drawn anew.</summary>
        private static CadEditResult Edit(
            CadDocument document,
            string handle,
            string property,
            string oldValue,
            string newValue,
            Action<DxfTable> change
        ) => Edit(document, handle, "TABLEDIT", property, oldValue, newValue, change);

        /// <summary>One edit of a table, an undo step of the name given: planned as a change of its property, written, and its block drawn anew.</summary>
        private static CadEditResult Edit(
            CadDocument document,
            string handle,
            string step,
            string property,
            string oldValue,
            string newValue,
            Action<DxfTable> change
        ) =>
            document.ApplyEdit(
                step,
                new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property, oldValue, newValue) },
                s =>
                {
                    if (s.IsHandled(handle) || !(s.Document.FindEntity(handle) is DxfTable table))
                    {
                        return;
                    }

                    change(table);
                    Redraw(table, s.Document);
                }
            );

        /// <summary>
        /// Draws a table anew in its block as AutoCAD lays it out: each row first grown to hold the tallest of its cells
        /// (<see cref="FitRows"/>), never shrunk, then its block drawn (<see cref="DxfTable.RecomputeTableBlock"/>), in the
        /// fonts of the document given; with none, the rows keep their heights.
        /// </summary>
        internal static void Redraw(DxfTable table, CadDocument document)
        {
            if (document != null)
            {
                FitRows(document, table);
            }

            table.RecomputeTableBlock();
        }

        /// <summary>
        /// Grows each row of a table to hold its cells, as AutoCAD 2021's table lays itself out: a cell's text wraps at the
        /// cell's width less its margins, as an MTEXT of that width (<see cref="CadMTextLayout"/>), and needs its actual
        /// height and the margin above and below, a row never lower than one line, four thirds of the text height, of its first
        /// cell; a row is as high as the
        /// most any of its cells needs, and never lower than it was. A block of merged cells across rows grows the rows it spans
        /// in proportion to their heights until they hold it (<see cref="Wanted"/>). In AutoCAD's Standard (text 4.5, margins 1.5) a row of nothing but one-line words is 9 high, and
        /// "HEA300 S355 with end plates both sides" in a column 30 wide, wrapped to five lines of ghsimplex, makes its row
        /// 45.42857, as AutoCAD 2021 laid them out. A block cell not fitted to its cell asks the box its block's extents fill at the
        /// cell's scale and turn, 43 high for a block 10 by 40 at scale 1, and widens its columns first where it is wider
        /// (<see cref="ColumnAsks"/>); a fitted one asks nothing.
        /// </summary>
        internal static void FitRows(CadDocument document, DxfTable table)
        {
            int rows = table.NumRows;
            int columns = table.NumColumns;

            // The columns first, for a block cell's block wider than its column, as AutoCAD lays them out: the words of the other
            // cells then wrap at the widths they come to.
            var widths = new double[columns];

            for (int c = 0; c < columns; c++)
            {
                widths[c] = table.ColumnWidth(c);
            }

            double[] wider = Wanted(widths, ColumnAsks(table));

            for (int c = 0; c < columns; c++)
            {
                if (wider[c] > widths[c])
                {
                    table.SetColumnWidth(c, wider[c]);
                }
            }

            var heights = new double[rows];

            for (int r = 0; r < rows; r++)
            {
                heights[r] = table.RowHeight(r);
            }

            double[] wanted = Wanted(heights, RowAsks(document, table, new Looks(table)));

            for (int r = 0; r < rows; r++)
            {
                if (wanted[r] > heights[r])
                {
                    table.SetRowHeight(r, wanted[r]);
                }
            }
        }

        /// <summary>
        /// The height the block of merged cells whose top left cell is given needs to hold words, the margins above and below
        /// in, as <see cref="FitRows"/> counts it for the words the cell holds: what an editor typing in the cell asks of its
        /// rows. Null for a block cell, which <see cref="FitRows"/> leaves as it is.
        /// </summary>
        internal static double? BlockNeeds(CadDocument document, DxfTable table, int row, int column, string words)
        {
            if (table.CellType(row, column) == DxfTableCellType.BlockCell)
            {
                return null;
            }

            double need = CellNeeds(
                document,
                table,
                new Looks(table),
                row,
                column,
                table.GetMergeRange(row, column),
                table.HorizontalCellMargin,
                words,
                column == 0
            );
            return need > 0.0 ? need + 2.0 * table.VerticalCellMargin : 0.0;
        }

        /// <summary>
        /// The height each row is wanted at, given the heights it stands at and the height each block of merged cells needs (its
        /// margins in): a block of one row raises it to what the block needs; a block across rows, taken in the order of its
        /// top left cell, row by row, shares what it needs among the rows it spans in proportion to the heights they stand at
        /// then (those of the cells of one row first), so that the rows come to the sum the block needs together, as AutoCAD
        /// 2021's <c>GenerateLayout</c> does: a block of 12 lines over two rows of 9, which needs 90.42857, makes 45.21429 each,
        /// and over rows of 12 and 20 makes 33.91071 and 56.51786.
        /// </summary>
        internal static double[] Wanted(double[] heights, List<KeyValuePair<DxfCellRange, double>> needs)
        {
            var wanted = (double[])heights.Clone();

            foreach (KeyValuePair<DxfCellRange, double> block in needs)
            {
                if (block.Key.BottomRow == block.Key.TopRow)
                {
                    wanted[block.Key.TopRow] = Math.Max(wanted[block.Key.TopRow], block.Value);
                }
            }

            // A block across rows: what the rows it spans lack goes to its last row.
            foreach (KeyValuePair<DxfCellRange, double> block in needs)
            {
                if (block.Key.BottomRow == block.Key.TopRow)
                {
                    continue;
                }

                double spanned = 0.0;

                for (int r = block.Key.TopRow; r <= block.Key.BottomRow; r++)
                {
                    spanned += wanted[r];
                }

                if (block.Value > spanned)
                {
                    double grow = block.Value / spanned;

                    for (int r = block.Key.TopRow; r <= block.Key.BottomRow; r++)
                    {
                        wanted[r] *= grow;
                    }
                }
            }

            return wanted;
        }

        /// <summary>
        /// The height a cell's words need, margins left out: laid out as an MTEXT as wide as the cell less its margins. A cell of
        /// the first column (<paramref name="first"/>) needs one line of its text height at least, four thirds of it, even with no
        /// words, as AutoCAD 2021 lays a row out by its first cell; any other cell needs what its words fill, and nothing with none.
        /// </summary>
        private static double CellNeeds(
            CadDocument document,
            DxfTable table,
            Looks style,
            int row,
            int column,
            DxfCellRange block,
            double hMargin,
            string words,
            bool first
        )
        {
            DxfRowType kind = table.RowType(row);
            double height = TextHeightOf(table, style, row, column, kind);
            double oneLine = first ? height * 4.0 / 3.0 : 0.0;

            if (words.Length == 0)
            {
                return oneLine;
            }

            double width = 0.0;

            for (int c = block.LeftColumn; c <= block.RightColumn; c++)
            {
                width += table.ColumnWidth(c);
            }

            var text = new DxfMText
            {
                Contents = words,
                TextHeight = height,
                Width = Math.Max(0.0, width - 2.0 * hMargin),
            };
            string styleName = table.TextStyle(row, column)?.Name ?? style.StyleName(kind);

            if (!string.IsNullOrEmpty(styleName))
            {
                text.TextStyleName = styleName;
            }

            return Math.Max(oneLine, document.LayoutOf(text).ActualHeight);
        }

        /// <summary>The text height of a cell of a row of a kind: its own, else its table's own for the kind, else its style's, else AutoCAD's Standard (6 for a title, else 4.5).</summary>
        private static double TextHeightOf(DxfTable table, Looks style, int row, int column, DxfRowType kind)
        {
            return table.TextHeight(row, column) ?? style.Height(kind);
        }

        /// <summary>The looks of a table that decide how its words lay out, read once for its rows: its own for the text height and style of each kind of row, then its style's.</summary>
        private sealed class Looks
        {
            private readonly DxfTableStyle _style;
            private readonly double?[] _heights;
            private readonly string[] _styles;

            internal Looks(DxfTable table)
            {
                _style = table.TableStyle as DxfTableStyle;
                table.OwnTextLooks(out _heights, out _styles);
            }

            private static int Index(DxfRowType kind) =>
                kind == DxfRowType.TitleRow ? 0
                : kind == DxfRowType.HeaderRow ? 1
                : 2;

            /// <summary>The text height of a row of a kind: the table's own, else its style's, else AutoCAD's Standard (6 for a title, else 4.5).</summary>
            internal double Height(DxfRowType kind) =>
                _heights[Index(kind)]
                ?? (
                    _style != null ? _style.TextHeight(kind)
                    : kind == DxfRowType.TitleRow ? 6.0
                    : 4.5
                );

            /// <summary>The name of the text style of a row of a kind: the table's own, else its style's; null for none.</summary>
            internal string StyleName(DxfRowType kind) => _styles[Index(kind)] ?? _style?.TextStyleName(kind);
        }

        /// <summary>The widths of the columns, or the heights of the rows, as they are.</summary>
        private static double[] Sizes(int count, Func<int, double> size)
        {
            var sizes = new double[count];

            for (int k = 0; k < count; k++)
            {
                sizes[k] = size(k);
            }

            return sizes;
        }

        private static bool Same(double[] a, double[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int k = 0; k < a.Length; k++)
            {
                if (a[k] != b[k])
                {
                    return false;
                }
            }

            return true;
        }

        private static string Join(double[] sizes) =>
            string.Join(",", Array.ConvertAll(sizes, s => s.ToString("R", CultureInfo.InvariantCulture)));

        /// <summary>The row (or column) a distance from the table's top (or left) edge falls in, held at the first and the last.</summary>
        private static int Nearest(int count, Func<int, double> size, double at)
        {
            double edge = 0.0;

            for (int k = 0; k < count - 1; k++)
            {
                edge += size(k);

                if (at < edge)
                {
                    return k;
                }
            }

            return count - 1;
        }

        private static DxfTable Find(CadDocument document, string handle, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var table = document.FindEntity(handle) as DxfTable;
            refused =
                table == null ? CadEditResult.Refuse("The object " + (handle ?? "(none)") + " is not a table.") : null;
            return table;
        }

        private static bool InTable(DxfTable table, int row, int column) =>
            row >= 0 && column >= 0 && row < table.NumRows && column < table.NumColumns;

        private static bool InTable(DxfTable table, DxfCellRange range, out CadEditResult refused)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            bool inside =
                InTable(table, range.TopRow, range.LeftColumn)
                && InTable(table, range.BottomRow, range.RightColumn)
                && range.TopRow <= range.BottomRow
                && range.LeftColumn <= range.RightColumn;
            refused = inside ? null : CadEditResult.Refuse("The cells " + Name(range) + " are not all of the table.");
            return inside;
        }

        private static void Each(DxfCellRange range, Action<int, int> act)
        {
            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    act(r, c);
                }
            }
        }

        private static string Name(DxfCellRange range) =>
            range.TopRow == range.BottomRow && range.LeftColumn == range.RightColumn
                ? CellName(range.TopRow, range.LeftColumn)
                : CellName(range.TopRow, range.LeftColumn) + ":" + CellName(range.BottomRow, range.RightColumn);

        private static GeoPoint2 P(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);
    }
}
