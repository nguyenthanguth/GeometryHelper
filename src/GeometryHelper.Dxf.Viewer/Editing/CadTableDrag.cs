using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a drag of a table's grip changes.</summary>
    public enum CadTableDragKind
    {
        /// <summary>A line between columns, or a table's left or right edge: the width of a column.</summary>
        ColumnLine,

        /// <summary>A line between rows, or a table's top or bottom edge: the height of a row.</summary>
        RowLine,

        /// <summary>A stretch grip of the whole table: every column, every row, or both, in proportion.</summary>
        Size,

        /// <summary>The autofill grip of the cells selected: the cells passed over filled from them.</summary>
        Autofill,
    }

    /// <summary>A cell an autofill will fill and the text it will hold, which the canvas shows while the grip is dragged.</summary>
    public sealed class CadTableCellValue
    {
        internal CadTableCellValue(int row, int column, string text, GeoPoint2 center)
        {
            Row = row;
            Column = column;
            Text = text;
            Center = center;
        }

        /// <summary>Gets the row, from nought.</summary>
        public int Row { get; }

        /// <summary>Gets the column, from nought.</summary>
        public int Column { get; }

        /// <summary>Gets the text the cell will hold.</summary>
        public string Text { get; }

        /// <summary>Gets the middle of the cell, in the plan.</summary>
        public GeoPoint2 Center { get; }
    }

    /// <summary>
    /// A drag of a grip of a table, as AutoCAD 2021 drags them: a line between columns or rows, a stretch grip of the whole
    /// table, or the autofill grip of cells. <see cref="Update"/> follows the cursor and says what the drag will make, the new
    /// line, the new outline, the cells to fill and their values, and a tip; <see cref="Commit"/> makes it, one undo step
    /// ("Column Width", "Row Height", "Table Size" or "Autofill"); dropped, it changes nothing.
    /// </summary>
    /// <remarks>
    /// The line between columns C and D of a table, C 30 wide and D 22.5, dragged 8 to the right makes C 38 and D 14.5, the
    /// table's width kept; with Ctrl, or from a cell's edge grip, C alone grows and the columns right of it move along. No
    /// column goes narrower than <see cref="CadTableEditor.MinimumColumnWidth"/> nor a row lower than its text needs: the line
    /// stops there though the cursor goes on. The left (or top) edge of the first column (or row) moves the table along with
    /// it. A 1 in A3 whose autofill grip is dragged down into A6 previews 2, 3 and 4 in A4 to A6, as Fill Series fills.
    /// </remarks>
    public sealed class CadTableDrag
    {
        /// <summary>The fill an autofill makes unless another is chosen: Fill Series, a series in the look of the source.</summary>
        public const DxfTableFillOptions FillSeries =
            DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.CopyFormat;

        private readonly CadDocument _document;
        private readonly GeoPoint2 _origin;
        private readonly GeoVector2 _across;
        private readonly GeoVector2 _down;
        private readonly double[] _widths;
        private readonly double[] _heights;
        private readonly double[] _least;
        private readonly int _line;
        private readonly bool _edge;
        private readonly bool _stretchesWidth;
        private readonly bool _stretchesHeight;
        private readonly DxfCellRange _source;
        private double[] _newWidths;
        private double[] _newHeights;
        private double _shift;
        private bool _control;
        private GeoPoint2? _cursor;
        private DxfTableFillOptions _fill = FillSeries;
        private DxfTableFillOptions _way;

        private CadTableDrag(
            CadDocument document,
            DxfTable table,
            CadTableDragKind kind,
            int line,
            bool edge,
            bool width,
            bool height,
            DxfCellRange source
        )
        {
            _document = document;
            Handle = table.Handle;
            Kind = kind;
            _line = line;
            _edge = edge;
            _stretchesWidth = width;
            _stretchesHeight = height;
            _source = source;
            GeoPoint2[] first = CadTableEditor.Corners(table, new DxfCellRange(0, 0, 0, 0));
            _origin = first[0];
            _across = CadGrips.Unit(first[1] - first[0], new GeoVector2(1.0, 0.0));
            _down = CadGrips.Unit(first[3] - first[0], new GeoVector2(0.0, -1.0));
            _widths = new double[table.NumColumns];
            _heights = new double[table.NumRows];

            for (int c = 0; c < _widths.Length; c++)
            {
                _widths[c] = table.ColumnWidth(c);
            }

            for (int r = 0; r < _heights.Length; r++)
            {
                _heights[r] = table.RowHeight(r);
            }

            if (kind == CadTableDragKind.ColumnLine || (kind == CadTableDragKind.Size && width))
            {
                _least = new double[_widths.Length];

                for (int c = 0; c < _widths.Length; c++)
                {
                    _least[c] = CadTableEditor.MinimumColumnWidth(table, c);
                }
            }

            if (kind == CadTableDragKind.RowLine || (kind == CadTableDragKind.Size && height))
            {
                double[] needs = CadTableEditor.RowNeeds(document, table);

                if (kind == CadTableDragKind.RowLine)
                {
                    _least = needs;
                }
                else
                {
                    RowLeast = needs;
                }
            }

            _newWidths = (double[])_widths.Clone();
            _newHeights = (double[])_heights.Clone();
        }

        /// <summary>Gets the handle of the table.</summary>
        public string Handle { get; }

        /// <summary>Gets what the drag changes.</summary>
        public CadTableDragKind Kind { get; }

        /// <summary>Gets the widths of the columns as the drag would leave them.</summary>
        public IReadOnlyList<double> Widths => _newWidths;

        /// <summary>Gets the heights of the rows as the drag would leave them.</summary>
        public IReadOnlyList<double> Heights => _newHeights;

        /// <summary>
        /// Gets the two ends of the line dragged, where the drag would leave it, across the whole table, the top (or left) first;
        /// null for a stretch or an autofill.
        /// </summary>
        public GeoPoint2[] Line { get; private set; }

        /// <summary>Gets the corners of the table as a stretch would leave it, the top left first, then about it; null for a drag of another kind.</summary>
        public GeoPoint2[] Outline { get; private set; }

        /// <summary>
        /// Gets the tip the drag shows by the cursor: "C width = 38.00" or "Row 4 height = 12.00" to the drawing's LUPREC, the
        /// table's new size for a stretch, the value of the cell farthest from the source for an autofill; null for none.
        /// </summary>
        public string Tip { get; private set; }

        /// <summary>Gets the cells an autofill would fill: the source's width (or height) from its edge to the cell under the cursor; null where it would fill none.</summary>
        public DxfCellRange FillRange { get; private set; }

        /// <summary>Gets the cells an autofill would fill and what each would hold; empty where none.</summary>
        public IReadOnlyList<CadTableCellValue> Values { get; private set; } = Array.Empty<CadTableCellValue>();

        /// <summary>Gets the cells the autofill grip was dragged from.</summary>
        public DxfCellRange Source => _source;

        /// <summary>
        /// Gets or sets how an autofill fills, as the right-click menu of the drag chooses it: <see cref="FillSeries"/> by
        /// default, a series alone for Fill Series Without Formatting, the content and the format for Copy Cells, the content
        /// alone without formatting, the format alone for Fill Formatting Only. Along the rows and Reverse are added by the way
        /// the grip is dragged, right or left, up or left.
        /// </summary>
        public DxfTableFillOptions FillOptions
        {
            get => _fill;
            set
            {
                _fill = value & ~(DxfTableFillOptions.Row | DxfTableFillOptions.Reverse);

                if (_cursor.HasValue && Kind == CadTableDragKind.Autofill)
                {
                    Update(_cursor.Value, _control);
                }
            }
        }

        /// <summary>
        /// Gets the number of the column (or row) whose size the tip gives, from nought: the one left of (or above) the line
        /// dragged, the first where the line is the table's left (or top) edge; -1 for another kind of drag.
        /// </summary>
        public int Changing =>
            Kind == CadTableDragKind.ColumnLine || Kind == CadTableDragKind.RowLine ? Math.Max(0, _line - 1) : -1;

        /// <summary>Gets whether the line dragged is the table's left (or top) edge, which moves the table along with it.</summary>
        public bool IsFirstEdge =>
            _line == 0 && (Kind == CadTableDragKind.ColumnLine || Kind == CadTableDragKind.RowLine);

        /// <summary>Gets the least height of each row a stretch of the height keeps, what its text needs.</summary>
        private double[] RowLeast { get; }

        /// <summary>
        /// Starts a drag of a line of columns: <paramref name="line"/> 0 is the table's left edge, 1 the line right of column A,
        /// and the number of columns its right edge. A column grip of the whole table drags a line inside it, the table's width
        /// kept unless Ctrl is held; a cell's edge grip changes the column left of the line alone.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="line">The line, from nought.</param>
        /// <param name="cellEdge">Whether it is dragged by a cell's edge grip.</param>
        /// <returns>The drag; null where the handle names no table or the table has no such line.</returns>
        public static CadTableDrag Column(CadDocument document, string handle, int line, bool cellEdge)
        {
            DxfTable table = TableOf(document, handle);
            return table == null || line < 0 || line > table.NumColumns || table.NumRows < 1
                ? null
                : new CadTableDrag(document, table, CadTableDragKind.ColumnLine, line, cellEdge, false, false, null);
        }

        /// <summary>
        /// Starts a drag of a line of rows by a cell's top or bottom edge grip: <paramref name="line"/> 0 is the table's top edge,
        /// 1 the line under row 1, and the number of rows its bottom edge; the row above the line changes alone.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="line">The line, from nought.</param>
        /// <returns>The drag; null where the handle names no table or the table has no such line.</returns>
        public static CadTableDrag Row(CadDocument document, string handle, int line)
        {
            DxfTable table = TableOf(document, handle);
            return table == null || line < 0 || line > table.NumRows || table.NumColumns < 1
                ? null
                : new CadTableDrag(document, table, CadTableDragKind.RowLine, line, true, false, false, null);
        }

        /// <summary>Starts a drag of a stretch grip of a table: its width, its height, or both.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="width">Whether every column stretches.</param>
        /// <param name="height">Whether every row stretches.</param>
        /// <returns>The drag; null where the handle names no table.</returns>
        public static CadTableDrag Size(CadDocument document, string handle, bool width, bool height)
        {
            DxfTable table = TableOf(document, handle);
            return table == null || table.NumRows < 1 || table.NumColumns < 1
                ? null
                : new CadTableDrag(document, table, CadTableDragKind.Size, 0, false, width, height, null);
        }

        /// <summary>Starts a drag of the autofill grip of cells selected.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="source">The cells selected.</param>
        /// <returns>The drag; null where the handle names no table or the cells are not all of it.</returns>
        public static CadTableDrag Autofill(CadDocument document, string handle, DxfCellRange source)
        {
            DxfTable table = TableOf(document, handle);
            return
                table == null
                || source == null
                || source.TopRow < 0
                || source.LeftColumn < 0
                || source.BottomRow >= table.NumRows
                || source.RightColumn >= table.NumColumns
                || source.TopRow > source.BottomRow
                || source.LeftColumn > source.RightColumn
                ? null
                : new CadTableDrag(document, table, CadTableDragKind.Autofill, 0, false, false, false, source);
        }

        /// <summary>
        /// Starts the drag a grip of a whole table makes, as <see cref="CadGrips.Of"/> gives them: a column grip, or a stretch
        /// grip; null for the move grip, which moves the table as any base grip does, the table breaking grip, which is not
        /// offered, and a grip of anything else.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The table's handle.</param>
        /// <param name="gripIndex">The index of the grip.</param>
        /// <returns>The drag, or null.</returns>
        public static CadTableDrag FromGrip(CadDocument document, string handle, int gripIndex)
        {
            if (!(TableOf(document, handle) is DxfTable table))
            {
                return null;
            }

            IReadOnlyList<CadGrip> grips = CadGrips.Of(table);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return null;
            }

            switch (grips[gripIndex].Kind)
            {
                case CadGripKind.TableColumn:
                {
                    int line = 0;

                    for (int k = 0; k <= gripIndex; k++)
                    {
                        line += grips[k].Kind == CadGripKind.TableColumn ? 1 : 0;
                    }

                    return Column(document, handle, line, false);
                }

                case CadGripKind.TableWidth:
                    return Size(document, handle, true, false);
                case CadGripKind.TableHeight:
                    return Size(document, handle, false, true);
                case CadGripKind.TableSize:
                    return Size(document, handle, true, true);
                default:
                    return null;
            }
        }

        /// <summary>Follows the cursor: what the drag would make with the grip let go there.</summary>
        /// <param name="cursor">The cursor, in the plan.</param>
        /// <param name="control">Whether Ctrl is held, which lets a column grip of the whole table change the table's width.</param>
        public void Update(GeoPoint2 cursor, bool control)
        {
            _cursor = cursor;
            _control = control;
            GeoVector2 way = cursor - _origin;
            double x = way.DotProduct(_across);
            double y = way.DotProduct(_down);

            switch (Kind)
            {
                case CadTableDragKind.ColumnLine:
                    MoveLine(_widths, x - Sum(_widths, _line), !_edge && !control, out _newWidths);
                    break;
                case CadTableDragKind.RowLine:
                    MoveLine(_heights, y - Sum(_heights, _line), false, out _newHeights);
                    break;
                case CadTableDragKind.Size:
                    Stretch(x, y);
                    break;
                default:
                    Autofill(cursor);
                    break;
            }
        }

        /// <summary>
        /// Takes a typed size, as the field of dynamic input does: the column (or row) the tip names made that wide (or high),
        /// held at its least, the line moved by as much.
        /// </summary>
        /// <param name="size">The width or height.</param>
        /// <returns>Whether it was taken: false for a stretch, an autofill, or a size not above nought.</returns>
        public bool TypeSize(double size)
        {
            if ((Kind != CadTableDragKind.ColumnLine && Kind != CadTableDragKind.RowLine) || !(size > 0.0))
            {
                return false;
            }

            bool columns = Kind == CadTableDragKind.ColumnLine;
            double[] sizes = columns ? _widths : _heights;
            int changing = Changing;

            // The first line moves the other way: the column (or row) right of (or under) it shrinks as it moves on.
            double moved = _line == 0 ? sizes[0] - size : size - sizes[changing];
            MoveLine(sizes, moved, columns && !_edge && !_control, out double[] made);

            if (columns)
            {
                _newWidths = made;
            }
            else
            {
                _newHeights = made;
            }

            return true;
        }

        /// <summary>Makes the drag: one undo step; nothing written where it changes nothing.</summary>
        /// <returns>What came of it.</returns>
        public CadEditResult Commit()
        {
            switch (Kind)
            {
                case CadTableDragKind.ColumnLine:
                    return CadTableEditor.Resize(_document, Handle, "Column Width", _newWidths, null, _across * _shift);
                case CadTableDragKind.RowLine:
                    return CadTableEditor.Resize(_document, Handle, "Row Height", null, _newHeights, _down * _shift);
                case CadTableDragKind.Size:
                    return CadTableEditor.Resize(
                        _document,
                        Handle,
                        "Table Size",
                        _stretchesWidth ? _newWidths : null,
                        _stretchesHeight ? _newHeights : null,
                        default
                    );
                default:
                    return FillRange == null
                        ? CadEditResult.Done(Array.Empty<CadEditRecord>())
                        : CadTableEditor.Fill(_document, Handle, FillRange, _source, _fill | _way);
            }
        }

        /// <summary>
        /// A line moved by a way along the table: the column (or row) before it takes the change, and with
        /// <paramref name="keep"/> the one after it gives it back; each held at its least. The first line changes the first
        /// column and moves the table with it.
        /// </summary>
        private void MoveLine(double[] sizes, double moved, bool keep, out double[] made)
        {
            made = (double[])sizes.Clone();
            _shift = 0.0;

            if (_line == 0)
            {
                double first = Math.Max(sizes[0] - moved, _least[0]);
                made[0] = first;
                _shift = sizes[0] - first;
            }
            else if (keep && _line < sizes.Length)
            {
                double low = _least[_line - 1] - sizes[_line - 1];
                double high = sizes[_line] - _least[_line];
                double by = low > high ? 0.0 : Math.Min(Math.Max(moved, low), high);
                made[_line - 1] = sizes[_line - 1] + by;
                made[_line] = sizes[_line] - by;
            }
            else
            {
                made[_line - 1] = Math.Max(sizes[_line - 1] + moved, _least[_line - 1]);
            }

            bool columns = Kind == CadTableDragKind.ColumnLine;
            double at = _line == 0 ? _shift : Sum(made, _line);
            double length = columns ? Sum(_heights, _heights.Length) : Sum(_widths, _widths.Length);
            GeoVector2 along = columns ? _across : _down;
            GeoVector2 other = columns ? _down : _across;
            GeoPoint2 start = _origin + along * at;
            Line = new[] { start, start + other * length };
            int changing = Changing;
            string size = made[changing].ToString("F" + Precision(), CultureInfo.InvariantCulture);
            Tip = columns
                ? CadTableEditor.ColumnName(changing) + " width = " + size
                : "Row " + (changing + 1).ToString(CultureInfo.InvariantCulture) + " height = " + size;
        }

        /// <summary>A stretch: the columns (and rows) scaled alike to the cursor, none below its least.</summary>
        private void Stretch(double x, double y)
        {
            double width = Sum(_widths, _widths.Length);
            double height = Sum(_heights, _heights.Length);
            _newWidths = (double[])_widths.Clone();
            _newHeights = (double[])_heights.Clone();

            if (_stretchesWidth && width > 0.0)
            {
                double factor = Factor(x / width, _widths, _least);

                for (int c = 0; c < _widths.Length; c++)
                {
                    _newWidths[c] = _widths[c] * factor;
                }
            }

            if (_stretchesHeight && height > 0.0)
            {
                double factor = Factor(y / height, _heights, RowLeast);

                for (int r = 0; r < _heights.Length; r++)
                {
                    _newHeights[r] = _heights[r] * factor;
                }
            }

            double across = Sum(_newWidths, _newWidths.Length);
            double down = Sum(_newHeights, _newHeights.Length);
            Outline = new[]
            {
                _origin,
                _origin + _across * across,
                _origin + _across * across + _down * down,
                _origin + _down * down,
            };
            string precision = "F" + Precision();
            Tip =
                (_stretchesWidth ? "Width = " + across.ToString(precision, CultureInfo.InvariantCulture) : string.Empty)
                + (_stretchesWidth && _stretchesHeight ? ", " : string.Empty)
                + (
                    _stretchesHeight
                        ? "Height = " + down.ToString(precision, CultureInfo.InvariantCulture)
                        : string.Empty
                );
        }

        /// <summary>A factor of a stretch held where it would take a size below its least.</summary>
        private static double Factor(double wanted, double[] sizes, double[] least)
        {
            double floor = 0.0;

            for (int k = 0; k < sizes.Length; k++)
            {
                if (sizes[k] > 0.0)
                {
                    floor = Math.Max(floor, least[k] / sizes[k]);
                }
            }

            return Math.Max(wanted, floor);
        }

        /// <summary>
        /// The cells an autofill would fill with the cursor at a point: along the way it has moved more cells, down or right, up
        /// or left backwards, from the source's edge to the cell under the cursor, short of the first block of merged cells.
        /// </summary>
        private void Autofill(GeoPoint2 cursor)
        {
            FillRange = null;
            Values = Array.Empty<CadTableCellValue>();
            Tip = null;
            DxfTable table = TableOf(_document, Handle);

            if (table == null || !CadTableEditor.CellNear(table, cursor, out int row, out int column))
            {
                return;
            }

            DxfCellRange s = _source;
            int rows =
                row > s.BottomRow ? row - s.BottomRow
                : row < s.TopRow ? row - s.TopRow
                : 0;
            int columns =
                column > s.RightColumn ? column - s.RightColumn
                : column < s.LeftColumn ? column - s.LeftColumn
                : 0;

            if (rows == 0 && columns == 0)
            {
                return;
            }

            bool down = Math.Abs(rows) >= Math.Abs(columns);
            int step = down ? Math.Sign(rows) : Math.Sign(columns);
            int first = down
                ? (step > 0 ? s.BottomRow + 1 : s.TopRow - 1)
                : (step > 0 ? s.RightColumn + 1 : s.LeftColumn - 1);
            int last = first;
            int end = down ? row : column;

            // Short of the first line of cells that meets a block of merged cells.
            for (int at = first; ; at += step)
            {
                bool merged = false;
                int from = down ? s.LeftColumn : s.TopRow;
                int to = down ? s.RightColumn : s.BottomRow;

                for (int k = from; k <= to && !merged; k++)
                {
                    merged = down ? table.IsMergedCell(at, k, out _) : table.IsMergedCell(k, at, out _);
                }

                if (merged)
                {
                    break;
                }

                last = at;

                if (at == end)
                {
                    break;
                }
            }

            int low = Math.Min(first, last),
                high = Math.Max(first, last);

            if (Blocked(table, down, low, high, s))
            {
                return;
            }

            FillRange = down
                ? new DxfCellRange(low, s.LeftColumn, high, s.RightColumn)
                : new DxfCellRange(s.TopRow, low, s.BottomRow, high);
            _way =
                (down ? DxfTableFillOptions.None : DxfTableFillOptions.Row)
                | (step < 0 ? DxfTableFillOptions.Reverse : 0);
            Values = Coming(table, FillRange);

            // The tip gives the value of the cell farthest from the source, of its first line.
            int farRow = down ? (step > 0 ? high : low) : s.TopRow;
            int farColumn = down ? s.LeftColumn : (step > 0 ? high : low);

            foreach (CadTableCellValue value in Values)
            {
                if (value.Row == farRow && value.Column == farColumn)
                {
                    Tip = value.Text;
                }
            }
        }

        /// <summary>Whether the first line of cells an autofill would take meets a block of merged cells, so that it fills nothing.</summary>
        private static bool Blocked(DxfTable table, bool down, int low, int high, DxfCellRange s)
        {
            for (int at = low; at <= high; at++)
            {
                int from = down ? s.LeftColumn : s.TopRow;
                int to = down ? s.RightColumn : s.BottomRow;

                for (int k = from; k <= to; k++)
                {
                    if (down ? table.IsMergedCell(at, k, out _) : table.IsMergedCell(k, at, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The values an autofill would give the cells, read from a copy of the table filled so; none where the copy cannot be filled.</summary>
        private IReadOnlyList<CadTableCellValue> Coming(DxfTable table, DxfCellRange range)
        {
            var values = new List<CadTableCellValue>();

            try
            {
                var copy = (DxfTable)table.Clone();
                copy.Fill(range, _source, _fill | _way);

                for (int r = range.TopRow; r <= range.BottomRow; r++)
                {
                    for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                    {
                        IReadOnlyList<GeoPoint3> corners = table.GetCellExtents(r, c, false);
                        values.Add(
                            new CadTableCellValue(
                                r,
                                c,
                                copy.TextString(r, c),
                                new GeoPoint2((corners[0].X + corners[2].X) / 2.0, (corners[0].Y + corners[2].Y) / 2.0)
                            )
                        );
                    }
                }
            }
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Debug("CadTableDrag: the autofill preview could not be read: " + exception.Message);
                return Array.Empty<CadTableCellValue>();
            }

            return values;
        }

        /// <summary>The places of decimals sizes are written to: the drawing's LUPREC.</summary>
        private int Precision() => Math.Max(0, Math.Min(8, _document.Database.Luprec));

        private static double Sum(double[] sizes, int count)
        {
            double sum = 0.0;

            for (int k = 0; k < count; k++)
            {
                sum += sizes[k];
            }

            return sum;
        }

        private static DxfTable TableOf(CadDocument document, string handle)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return document.FindEntity(handle) as DxfTable;
        }
    }
}
