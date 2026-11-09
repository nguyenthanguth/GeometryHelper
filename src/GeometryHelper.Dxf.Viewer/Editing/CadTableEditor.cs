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
        public static string CellName(int row, int column) => ColumnName(column) + (row + 1).ToString(CultureInfo.InvariantCulture);

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

            DxfTableHitTestInfo hit = table.HitTest(new GeoPoint3(point.X, point.Y, 0.0), new GeoVector3(0.0, 0.0, -1.0));

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

            IReadOnlyList<GeoPoint3> first = table.GetCellExtents(range.TopRow, range.LeftColumn, true);
            IReadOnlyList<GeoPoint3> last = table.GetCellExtents(range.BottomRow, range.RightColumn, true);
            return new[] { P(first[0]), P(last[1]), P(last[2]), P(first[3]) };
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

            int top = range.TopRow, left = range.LeftColumn, bottom = range.BottomRow, right = range.RightColumn;
            bool grew = true;

            while (grew)
            {
                grew = false;

                for (int r = top; r <= bottom; r++)
                {
                    for (int c = left; c <= right; c++)
                    {
                        DxfCellRange block = table.GetMergeRange(r, c);

                        if (block.TopRow < top || block.LeftColumn < left || block.BottomRow > bottom || block.RightColumn > right)
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

        /// <summary>Writes the text of a cell, as typing in it does; the same text changes nothing.</summary>
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

            if (string.Equals(old, text, StringComparison.Ordinal))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return Edit(document, handle, "Cell " + CellName(row, column), old, text, t =>
            {
                if (text.Length == 0)
                {
                    t.ResetValue(row, column);
                }
                else
                {
                    t.SetTextString(row, column, text);
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
                return CadEditResult.Refuse("A row cannot be put in " + (below ? "below" : "above") + " row " + (row + 1).ToString(CultureInfo.InvariantCulture) + ".");
            }

            return Edit(document, handle, below ? "Insert Below" : "Insert Above", null, (row + 1).ToString(CultureInfo.InvariantCulture), t => t.InsertRowsAndInherit(at, row, 1));
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

            return Edit(document, handle, "Delete Rows", (row + 1).ToString(CultureInfo.InvariantCulture), null, t => t.DeleteRows(row, count));
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
                return CadEditResult.Refuse("A column cannot be put in " + (right ? "right" : "left") + " of column " + ColumnName(column) + ".");
            }

            return Edit(document, handle, right ? "Insert Right" : "Insert Left", null, ColumnName(column), t => t.InsertColumnsAndInherit(at, column, 1));
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

            return Edit(document, handle, "Delete Columns", ColumnName(column), null, t => t.DeleteColumns(column, count));
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

            return Edit(document, handle, "Merge Cells", null, Name(range), t => t.MergeCells(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn)));
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

            return Edit(document, handle, "Unmerge Cells", Name(range), null, t => t.UnmergeCells(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn)));
        }

        /// <summary>Sets where the content of the cells of a range stands, as the alignment of Cell Styles does.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="alignment">The alignment.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetAlignment(CadDocument document, string handle, DxfCellRange range, DxfCellAlignment alignment)
        {
            DxfTable table = Find(document, handle, out CadEditResult refused);

            if (table == null || !InTable(table, range, out refused))
            {
                return refused;
            }

            return Edit(document, handle, "Alignment", Name(range), alignment.ToString(), t => Each(range, (r, c) => t.SetAlignment(r, c, alignment)));
        }

        /// <summary>Fills the cells of a range with a colour, or leaves them unfilled, as the fill of Cell Styles does.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="range">The cells.</param>
        /// <param name="color">The colour, of the index, by layer or by block; null for none.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult SetBackground(CadDocument document, string handle, DxfCellRange range, DxfColor color)
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

            return Edit(document, handle, "Background fill", Name(range), color == null ? "None" : color.ColorIndex.ToString(CultureInfo.InvariantCulture), t => Each(range, (r, c) =>
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
            }));
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
        public static CadEditResult SetBorders(CadDocument document, string handle, DxfCellRange range, DxfGridLineType lines, bool visible)
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

            var property = new DxfGridPropertyParameter { PropertyMask = DxfGridProperties.Visibility, Visible = visible };
            return Edit(document, handle, "Cell borders", Name(range), (visible ? "Show " : "Hide ") + inRange, t => t.SetGridProperty(new DxfCellRange(range.TopRow, range.LeftColumn, range.BottomRow, range.RightColumn), inRange, property));
        }

        /// <summary>One edit of a table: planned as a change of its property, written, and its block drawn anew.</summary>
        private static CadEditResult Edit(CadDocument document, string handle, string property, string oldValue, string newValue, Action<DxfTable> change)
            => document.ApplyEdit("TABLEDIT", new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property, oldValue, newValue) }, s =>
            {
                if (s.IsHandled(handle) || !(s.Document.FindEntity(handle) is DxfTable table))
                {
                    return;
                }

                change(table);
                table.RecomputeTableBlock();
            });

        private static DxfTable Find(CadDocument document, string handle, out CadEditResult refused)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var table = document.FindEntity(handle) as DxfTable;
            refused = table == null ? CadEditResult.Refuse("The object " + (handle ?? "(none)") + " is not a table.") : null;
            return table;
        }

        private static bool InTable(DxfTable table, int row, int column) => row >= 0 && column >= 0 && row < table.NumRows && column < table.NumColumns;

        private static bool InTable(DxfTable table, DxfCellRange range, out CadEditResult refused)
        {
            if (range == null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            bool inside = InTable(table, range.TopRow, range.LeftColumn) && InTable(table, range.BottomRow, range.RightColumn)
                && range.TopRow <= range.BottomRow && range.LeftColumn <= range.RightColumn;
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

        private static string Name(DxfCellRange range)
            => range.TopRow == range.BottomRow && range.LeftColumn == range.RightColumn ? CellName(range.TopRow, range.LeftColumn)
                : CellName(range.TopRow, range.LeftColumn) + ":" + CellName(range.BottomRow, range.RightColumn);

        private static GeoPoint2 P(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);
    }
}
