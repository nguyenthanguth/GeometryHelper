using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Insert Table dialog of TABLE, as data its command and the window share: the window fills it and says what was
    /// pressed in <see cref="Action"/>; the command asks the insertion point (or the window) and makes the table. A test fills
    /// it with no window.
    /// </summary>
    public sealed class CadInsertTableRequest
    {
        /// <summary>Makes the request of a document, as TABLE opens its dialog: the current table style, and the columns and rows used last.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadInsertTableRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            TableStyle = CadTableStyleEditor.Current(document.Database);
        }

        /// <summary>Gets the document the table is made in.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets the table style the table is written in; Standard is made where the drawing has none.</summary>
        public string TableStyle { get; set; }

        /// <summary>Gets or sets whether the table is placed by a window (Specify window) rather than by its insertion point.</summary>
        public bool ByWindow { get; set; }

        /// <summary>Gets or sets the number of columns, 1 or more; 5 as AutoCAD's dialog opens.</summary>
        public int Columns { get; set; } = 5;

        /// <summary>Gets or sets the width of each column; 63.5 as AutoCAD's dialog opens.</summary>
        public double ColumnWidth { get; set; } = 63.5;

        /// <summary>Gets or sets the number of data rows, 1 or more, under the title and the header; 4 as the design opens it.</summary>
        public int DataRows { get; set; } = 4;

        /// <summary>Gets or sets the height of a data row in lines of its text, 1 or more.</summary>
        public int RowHeightLines { get; set; } = 1;

        /// <summary>Gets or sets what was pressed: OK, or Cancel.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>
    /// TABLE (TB) and -TABLE, TABLESTYLE (TS) and TABLEDIT, as AutoCAD 2021's: a table made from the Insert Table dialog or the
    /// command line, then its first cell opened for typing; a cell of a table picked and typed in, Tab, Shift+Tab and Enter
    /// moving on to the next, the one before and the one below. Each table made and each cell written is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddTablesTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("TABLE", Table));
            registry.Add(new CadCommand("-TABLE", TableTyped));
            registry.Add(new CadCommand("TABLESTYLE", TableStyleDialog));
            registry.Add(new CadCommand("TABLEDIT", TableEdit));
        }

        private static IEnumerable<CadPrompt> Table(CadCommandContext c)
        {
            var request = new CadInsertTableRequest(c.Document)
            {
                Columns = (int)c.Remembered("table.columns", 5),
                ColumnWidth = c.Remembered("table.width", 63.5),
                DataRows = (int)c.Remembered("table.rows", 4),
                RowHeightLines = (int)c.Remembered("table.lines", 1),
                Action = CadDialogAction.Cancel,
            };

            if (!c.Session.Request("TABLE", c.Document, request))
            {
                foreach (CadPrompt prompt in TableTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            if (request.Columns < 1 || request.DataRows < 1 || request.RowHeightLines < 1 || !(request.ColumnWidth > 0.0) || double.IsInfinity(request.ColumnWidth))
            {
                c.Write("A table needs a column and a data row at least, of a width above nought.");
                yield break;
            }

            c.Remember("table.columns", request.Columns);
            c.Remember("table.width", request.ColumnWidth);
            c.Remember("table.rows", request.DataRows);
            c.Remember("table.lines", request.RowHeightLines);
            GeoPoint2 corner;
            GeoPoint2? opposite = null;

            if (request.ByWindow)
            {
                yield return CadPrompt.ForPoint("Specify first corner");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                corner = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second corner", corner);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                opposite = c.Input.Point;
            }
            else
            {
                yield return CadPrompt.ForPoint("Specify insertion point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                corner = c.Input.Point;
            }

            string handle = MakeTable(c, request.TableStyle, corner, opposite, request.Columns, request.ColumnWidth, request.DataRows, request.RowHeightLines);

            if (handle == null)
            {
                yield break;
            }

            // The first cell is opened for typing, as AutoCAD's TABLE opens the title.
            foreach (CadPrompt prompt in EditCells(c, handle, 0, 0))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -TABLE: the columns and the data rows, then the insertion point, its Style, Width and Height changed first where asked,
        /// as AutoCAD's command line asks them.
        /// </summary>
        private static IEnumerable<CadPrompt> TableTyped(CadCommandContext c)
        {
            string style = CadTableStyleEditor.Current(c.Database);
            double width = c.Remembered("table.width", 63.5);
            int lines = (int)c.Remembered("table.lines", 1);
            c.Write("Current table style: \"" + style + "\"  Cell width: " + Number(width) + "  Cell height: " + lines.ToString(CultureInfo.InvariantCulture) + " line(s)");
            int columns = (int)c.Remembered("table.columns", 5);
            yield return CadPrompt.ForInteger("Enter number of columns", null, columns.ToString(CultureInfo.InvariantCulture));

            if (c.Input.Kind == CadInputKind.Number)
            {
                columns = (int)c.Input.Number;
            }

            int rows = (int)c.Remembered("table.rows", 1);
            yield return CadPrompt.ForInteger("Enter number of rows", null, rows.ToString(CultureInfo.InvariantCulture));

            if (c.Input.Kind == CadInputKind.Number)
            {
                rows = (int)c.Input.Number;
            }

            if (columns < 1 || rows < 1)
            {
                c.Write("Value must be a positive whole number.");
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify insertion point", null, new[] { "Style", "Width", "Height" });

                if (c.Input.Is("Style"))
                {
                    yield return CadPrompt.ForString("Enter a table style name or [?]", style);
                    string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                    if (typed == "?")
                    {
                        foreach (string name in CadTableStyleEditor.Styles(c.Database))
                        {
                            c.Write("  " + name);
                        }
                    }
                    else if (typed.Length > 0)
                    {
                        DxfTableStyle found = CadTableStyleEditor.Find(c.Database, typed);

                        if (found == null)
                        {
                            c.Write("Cannot find table style \"" + typed + "\".");
                        }
                        else
                        {
                            style = found.Name;
                        }
                    }

                    continue;
                }

                if (c.Input.Is("Width"))
                {
                    yield return CadPrompt.ForDistance("Specify column width", null, null, Number(width));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        width = c.Input.Number;
                    }

                    continue;
                }

                if (c.Input.Is("Height"))
                {
                    yield return CadPrompt.ForInteger("Specify row height in lines", null, lines.ToString(CultureInfo.InvariantCulture));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number >= 1.0)
                    {
                        lines = (int)c.Input.Number;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            c.Remember("table.columns", columns);
            c.Remember("table.width", width);
            c.Remember("table.rows", rows);
            c.Remember("table.lines", lines);
            MakeTable(c, style, c.Input.Point, null, columns, width, rows, lines);
        }

        private static IEnumerable<CadPrompt> TableStyleDialog(CadCommandContext c)
        {
            if (!c.Session.Request("TABLESTYLE", c.Document))
            {
                c.Write("Current table style: \"" + CadTableStyleEditor.Current(c.Database) + "\". The Table Style dialog is shown by the window.");
            }

            yield break;
        }

        /// <summary>TABLEDIT: a cell picked, then typed in as <see cref="EditCells"/> does.</summary>
        private static IEnumerable<CadPrompt> TableEdit(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Pick a table cell");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            if (!CadTableEditor.TryCellAt(c.Space, c.Input.Point, out string handle, out int row, out int column))
            {
                c.Write("No table cell was picked.");
                yield break;
            }

            foreach (CadPrompt prompt in EditCells(c, handle, row, column))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// Makes a table at a corner, or filling a window, of the style given: a title, a header and the data rows, the title
        /// and header one line of their text high and the data the lines given, each line 4/3 of the text height and the margins
        /// above and below, as AutoCAD lays out a new table; one undo step. The style is made current where the drawing can hold
        /// it. Gives the table's handle, or null where it was not made.
        /// </summary>
        private static string MakeTable(CadCommandContext c, string styleName, GeoPoint2 corner, GeoPoint2? opposite, int columns, double columnWidth, int dataRows, int lines)
        {
            c.Document.EnsureNamedObjectsDictionary();
            c.OpenUndoStep();
            string handle = null;
            CadEditResult result = c.Apply(null, s =>
            {
                CadTableStyleEditor.EnsureStandard(s.Database);
                DxfTableStyle style = CadTableStyleEditor.Find(s.Database, styleName) ?? CadTableStyleEditor.Find(s.Database, "Standard");
                double margin = style.VerticalCellMargin;
                double Row(DxfRowType kind, int count) => count * style.TextHeight(kind) * 4.0 / 3.0 + 2.0 * margin;
                int rows = dataRows + 2;
                double[] heights = new double[rows];
                heights[0] = Row(DxfRowType.TitleRow, 1);
                heights[1] = Row(DxfRowType.HeaderRow, 1);

                for (int r = 2; r < rows; r++)
                {
                    heights[r] = Row(DxfRowType.DataRow, lines);
                }

                double width = columnWidth;
                GeoPoint2 top = corner;

                // A window: the columns share its width, the rows its height in the proportions they would have.
                if (opposite.HasValue)
                {
                    GeoPoint2 far = opposite.Value;
                    width = Math.Abs(far.X - corner.X) / columns;
                    double total = 0.0;

                    foreach (double h in heights)
                    {
                        total += h;
                    }

                    double scale = Math.Abs(far.Y - corner.Y) / total;

                    for (int r = 0; r < rows; r++)
                    {
                        heights[r] *= scale;
                    }

                    top = new GeoPoint2(Math.Min(corner.X, far.X), Math.Max(corner.Y, far.Y));
                }

                if (!(width > 0.0) || Array.Exists(heights, h => !(h > 0.0)))
                {
                    throw new InvalidOperationException("The window has no width or height to fill.");
                }

                var table = new DxfTable(P3(top), rows, columns, heights[2], width);
                s.Append(c.Space, Current(c, table));
                table.TableStyle = style;

                for (int r = 0; r < 2; r++)
                {
                    table.SetRowHeight(r, heights[r]);
                }

                table.RecomputeTableBlock();
                handle = table.Handle;
            });

            c.CloseUndoStep();

            if (result.Succeeded && !string.Equals(CadTableStyleEditor.Current(c.Database), styleName, StringComparison.OrdinalIgnoreCase))
            {
                // The style chosen is current next time, a step of its own, where the drawing can hold CTABLESTYLE; one that
                // cannot keeps its own, and the table is made all the same (a refusal inside the table's step would take it back).
                CadTableStyleEditor.SetCurrent(c.Document, styleName);
            }

            return result.Succeeded ? handle : null;
        }

        /// <summary>
        /// Opens cells of a table for typing one after another, as AutoCAD's editor of a cell does: each in the MTEXT editor over
        /// its cell, its words written as one undo step, then the next cell along (Tab), the one before (Shift+Tab) or the one
        /// below (Enter), a block of merged cells as its top left cell; the editing ends at Esc, a click away, or the table's end.
        /// </summary>
        internal static IEnumerable<CadPrompt> EditCells(CadCommandContext c, string handle, int row, int column)
        {
            while (true)
            {
                if (!(c.Document.FindEntity(handle) is DxfTable table) || row < 0 || column < 0 || row >= table.NumRows || column >= table.NumColumns)
                {
                    yield break;
                }

                DxfCellRange block = table.GetMergeRange(row, column);
                row = block.TopRow;
                column = block.LeftColumn;
                CadInPlaceEdit edit = CellEdit(table, row, column);
                yield return CadPrompt.ForInPlaceEdit("Edit table cell " + CadTableEditor.CellName(row, column), edit);

                if (c.Input.Kind == CadInputKind.Text)
                {
                    c.OpenUndoStep();
                    CadEditResult result = CadTableEditor.SetText(c.Document, handle, row, column, c.Input.Text);
                    c.CloseUndoStep();

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                        yield break;
                    }
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }

                if (edit.Move == CadTableCellMove.None || !(c.Document.FindEntity(handle) is DxfTable moved) || !NextCell(moved, edit.Move, ref row, ref column))
                {
                    yield break;
                }
            }
        }

        /// <summary>The cell a move goes to from a cell, past the cells a block of merged cells hides; false past the table's end.</summary>
        private static bool NextCell(DxfTable table, CadTableCellMove move, ref int row, ref int column)
        {
            DxfCellRange from = table.GetMergeRange(row, column);
            int r = row, c = column;

            while (true)
            {
                switch (move)
                {
                    case CadTableCellMove.Next:
                        c++;

                        if (c >= table.NumColumns)
                        {
                            c = 0;
                            r++;
                        }

                        break;
                    case CadTableCellMove.Previous:
                        c--;

                        if (c < 0)
                        {
                            c = table.NumColumns - 1;
                            r--;
                        }

                        break;
                    default:
                        r = Math.Max(r, from.BottomRow) + 1;
                        break;
                }

                if (r < 0 || r >= table.NumRows)
                {
                    return false;
                }

                DxfCellRange block = table.GetMergeRange(r, c);

                if (block.TopRow == from.TopRow && block.LeftColumn == from.LeftColumn)
                {
                    continue;
                }

                // A cell a block hides is passed over to the block's top left, where Tab reaches it.
                if (move != CadTableCellMove.Down && (block.TopRow != r || block.LeftColumn != c))
                {
                    continue;
                }

                row = block.TopRow;
                column = block.LeftColumn;
                return true;
            }
        }

        /// <summary>
        /// The text of a cell as the MTEXT editor opens it: at its attachment point, as wide as the cell less its margins, in the
        /// height, style and alignment the cell shows, its own else its table style's.
        /// </summary>
        internal static CadInPlaceEdit CellEdit(DxfTable table, int row, int column)
        {
            DxfRowType kind = table.RowType(row);
            var style = table.TableStyle as DxfTableStyle;
            double height = table.TextHeight(row, column) ?? (style != null ? style.TextHeight(kind) : kind == DxfRowType.TitleRow ? 6.0 : 4.5);
            DxfCellAlignment alignment = table.Alignment(row, column) ?? (style != null ? style.Alignment(kind) : kind == DxfRowType.DataRow ? DxfCellAlignment.TopCenter : DxfCellAlignment.MiddleCenter);
            string styleName = table.TextStyle(row, column)?.Name ?? style?.TextStyleName(kind) ?? "Standard";
            IReadOnlyList<GeoPoint3> corners = table.GetCellExtents(row, column, true);
            double width = corners[0].DistanceTo(corners[1]);
            double tall = corners[0].DistanceTo(corners[3]);
            GeoVector3 along = corners[0].GetVectorTo(corners[1]).Divide(width);
            GeoVector3 down = corners[0].GetVectorTo(corners[3]).Divide(tall);
            double h = table.HorizontalCellMargin;
            double v = table.VerticalCellMargin;
            int across = ((int)alignment - 1) % 3;
            int upDown = ((int)alignment - 1) / 3;
            double x = across == 0 ? h : across == 1 ? width / 2.0 : width - h;
            double y = upDown == 0 ? v : upDown == 1 ? tall / 2.0 : tall - v;
            GeoPoint3 at = corners[0].Add(along.Multiply(x)).Add(down.Multiply(y));
            return new CadInPlaceEdit(CadInPlaceKind.MText, table.Handle, table.TextString(row, column), new GeoPoint2(at.X, at.Y), height, Math.Atan2(along.Y, along.X), Math.Max(0.0, width - 2.0 * h), styleName, (int)alignment)
            {
                TableRow = row,
                TableColumn = column,
            };
        }
    }
}
