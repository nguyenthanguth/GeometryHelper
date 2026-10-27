using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The cells of a table walked, their values with their kinds and formats, their look set, their overrides, and the cell a
    /// point hits, as AutoCAD's <c>Table</c> has them.
    /// </summary>
    public sealed partial class DxfTable : IEnumerable<DxfCell>
    {
        // The flags of a cell's overrides (177, or 91 from R2007 on) of its look and its borders, the 18 the file has groups for.
        private const int CellOverrideFlags = 0x3FFFF;

        // The flag a cell gives its own kind of value, AutoCAD's override 148, for which the library knows no groups.
        private const int DataTypeFlag = 0x40000;

        // The flags of DxfUnitType, of which a unit is made: Distance to Percentage, and AngleNotTransformed.
        private const int UnitFlags = 0x1003F;

        // The groups of the table's own overrides after its last cell, as the DXF reference lists them.
        private static readonly HashSet<int> TableOverrides = new HashSet<int>
        {
            70, 40, 41, 280, 281, 7, 140, 170, 63, 64, 65, 66, 68, 69, 283, 274, 275, 276, 277, 278, 279, 284, 285, 286, 287, 288, 289, 97, 98, 4,
        };

        #region The cells walked

        /// <summary>
        /// Gets every cell of the table as a range, as AutoCAD's <c>Cells</c>: <c>table.Cells[1, 0]</c> is the cell of the second
        /// row and the first column, a <see cref="DxfCell"/> that reads and sets it; the range of a table of 3 rows and 2 columns
        /// is (0, 0) to (2, 1).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the table's record gives it no rows or no columns.</exception>
        public DxfCellRange Cells
        {
            get
            {
                Layout layout = Read();

                if (layout.Rows < 1 || layout.Columns < 1)
                {
                    throw new InvalidOperationException("The ACAD_TABLE " + (Handle ?? string.Empty) + " has no cells: its record gives it " + layout.Rows.ToString(CultureInfo.InvariantCulture) + " rows and " + layout.Columns.ToString(CultureInfo.InvariantCulture) + " columns.");
                }

                return new DxfCellRange(this, 0, 0, layout.Rows - 1, layout.Columns - 1);
            }
        }

        /// <summary>
        /// Gets the rows of the table, as AutoCAD's <c>Rows</c>: <c>table.Rows[0].Height</c> is the height of the first, read and
        /// set as <see cref="RowHeight(int)"/> and <see cref="SetRowHeight(int, double)"/> do; as many as <see cref="NumRows"/>.
        /// </summary>
        public IReadOnlyList<DxfRow> Rows => new Live<DxfRow>(() => NumRows, i => new DxfRow(this, i));

        /// <summary>
        /// Gets the columns of the table, as AutoCAD's <c>Columns</c>: <c>table.Columns[1].Width</c> is the width of the second,
        /// read and set as <see cref="ColumnWidth(int)"/> and <see cref="SetColumnWidth(int, double)"/> do; as many as
        /// <see cref="NumColumns"/>.
        /// </summary>
        public IReadOnlyList<DxfColumn> Columns => new Live<DxfColumn>(() => NumColumns, i => new DxfColumn(this, i));

        /// <summary>
        /// Walks the cells of the table, as AutoCAD's <c>GetEnumerator</c>: row by row from the top, each from the left, every
        /// cell of a block of merged cells among them; the table of 3 rows and 2 columns gives (0, 0), (0, 1), (1, 0) and on to
        /// (2, 1). A cell the record does not hold, where it holds fewer than its rows by its columns, is not given.
        /// </summary>
        /// <returns>The cells.</returns>
        public IEnumerator<DxfCell> GetEnumerator()
        {
            Layout layout = Read();
            int count = layout.Columns < 1 ? 0 : Math.Min(layout.Rows * layout.Columns, layout.CellStart.Count);

            for (int i = 0; i < count; i++)
            {
                yield return new DxfCell(this, i / layout.Columns, i % layout.Columns);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets the cell a point hits, as AutoCAD's <c>HitTest</c>: the point carried along the direction it is seen in onto the
        /// table's plane, then laid on its columns and rows from <see cref="Position"/> along <see cref="Direction"/>, as
        /// <see cref="GetCellExtents(int, int, bool)"/> lays them; a cell of a block of merged cells gives the block's top left.
        /// On the table of columns 40 and 30 wide and rows 10, 8 and 8 high at (300, -100, 0), seen from above, (350, -112, 5)
        /// hits the cell (1, 1) and (310, -105, 0) the title (0, 0); a point on the line between two cells, within
        /// <see cref="Tolerance.Default"/>, hits the one of the lower row or column index, above where the rows flow down and
        /// below where they flow up, and a point beyond the table hits nothing.
        /// </summary>
        /// <param name="point">The point, in world coordinates.</param>
        /// <param name="viewVector">The direction the point is seen in, of some length; one along the table's plane takes the
        /// point where it is, which hits the table only where it lies in its plane.</param>
        /// <returns>The cell hit, of <see cref="DxfTableHitTestType.Cell"/>, or <see cref="DxfTableHitTestType.None"/> with the
        /// row and the column -1.</returns>
        /// <exception cref="ArgumentException">Thrown when the point or the direction is not finite, or the direction has no length.</exception>
        public DxfTableHitTestInfo HitTest(GeoPoint3 point, GeoVector3 viewVector)
        {
            if (!point.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + point + " is not.", nameof(point));
            }

            if (!viewVector.IsValid || viewVector.IsZeroLength(Tolerance.Default))
            {
                throw new ArgumentException("A direction has to be of finite numbers and some length: " + viewVector + " is not.", nameof(viewVector));
            }

            var none = new DxfTableHitTestInfo(DxfTableHitTestType.None, -1, -1);
            double slack = Tolerance.Default.EqualPoint;
            Layout layout = Read();
            Frame(layout, out GeoVector3 normal, out GeoPoint3 origin, out GeoVector3 along, out GeoVector3 down);
            GeoVector3 view = DxfOcsTransform.Normalized(viewVector);
            double across = normal.DotProduct(view);

            if (Math.Abs(across) <= Tolerance.Default.EqualVector)
            {
                if (Math.Abs(normal.DotProduct(origin.GetVectorTo(point))) > slack)
                {
                    return none;
                }
            }
            else
            {
                point = point.Add(view.Multiply(normal.DotProduct(point.GetVectorTo(origin)) / across));
            }

            GeoVector3 offset = origin.GetVectorTo(point);
            int column = Place(layout, layout.ColumnAt, offset.DotProduct(along), slack);
            int row = Place(layout, layout.RowAt, offset.DotProduct(down), slack);

            if (row < 0 || column < 0 || row >= layout.Rows || column >= layout.Columns || row * layout.Columns + column >= layout.CellStart.Count)
            {
                return none;
            }

            DxfCellRange range = GetMergeRange(row, column);
            return new DxfTableHitTestInfo(DxfTableHitTestType.Cell, range.TopRow, range.LeftColumn);
        }

        /// <summary>
        /// Gets the point a cell's content is placed at, as AutoCAD's <c>AttachmentPoint</c>: the corner, the middle of a side or
        /// the middle of the cell (of its block of merged cells) its alignment names, in from the sides by the table's
        /// <see cref="HorizontalCellMargin"/> and <see cref="VerticalCellMargin"/>. The cell's own alignment where it has one (a
        /// block's, that of its top left cell, which holds its content), else as AutoCAD's table style STANDARD has it, which
        /// the library takes for the table style it does not read: the middle for the title and the header, the middle of the
        /// top for a row of data. The header cell (1, 1) of the table at (300, -100, 0), in the middle of its rows 10 and 8 high
        /// and its columns 40 and 30 wide, is at (355, -114, 0). The top is the cell's top as it is seen, whichever way the rows
        /// flow: the cell (2, 0) of that table with its rows flowing up, from y -82 to -74, is placed at the top middle at
        /// (320, -74.06, 0).
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The point, in world coordinates.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public GeoPoint3 AttachmentPoint(int row, int col)
        {
            IReadOnlyList<GeoPoint3> corners = GetCellExtents(row, col, true);

            // The block's content is its top left cell's, as AutoCAD keeps it.
            DxfCellRange block = GetMergeRange(row, col);
            DxfCellAlignment alignment = Alignment(block.TopRow, block.LeftColumn) ?? (RowType(block.TopRow) == DxfRowType.DataRow ? DxfCellAlignment.TopCenter : DxfCellAlignment.MiddleCenter);
            Frame(Read(), out _, out _, out GeoVector3 along, out GeoVector3 down);
            double width = corners[0].GetVectorTo(corners[1]).DotProduct(along);
            double height = corners[0].GetVectorTo(corners[3]).DotProduct(down);

            // The top is the cell's top as it is seen, whichever way the rows flow: where they flow up, its last corner, and
            // the rows' direction turned round.
            bool up = FlowDirection == DxfFlowDirection.BottomToTop;
            GeoPoint3 top = up ? corners[3] : corners[0];
            GeoVector3 below = up ? down.Multiply(-1.0) : down;
            int across = ((int)alignment - 1) % 3;
            int upDown = ((int)alignment - 1) / 3;
            double x = across == 0 ? HorizontalCellMargin : across == 1 ? width / 2.0 : width - HorizontalCellMargin;
            double y = upDown == 0 ? VerticalCellMargin : upDown == 1 ? height / 2.0 : height - VerticalCellMargin;
            return top.Add(along.Multiply(x)).Add(below.Multiply(y));
        }

        /// <summary>
        /// The frame the table's cells are laid in, as <see cref="GetCellExtents(int, int, bool)"/> lays them: its normal, its
        /// point, the direction of its rows and the one they follow one another in, the point and the direction read from its
        /// layout as it stands, so that a hit test of a table of 1 000 rows reads its record once.
        /// </summary>
        private void Frame(Layout layout, out GeoVector3 normal, out GeoPoint3 origin, out GeoVector3 along, out GeoVector3 down)
        {
            DxfDimGroups g = layout.Groups;

            // The normal as Normal reads it, from the first 210, 220 and 230 of the table's own part, made of unit length.
            bool any = Coordinate(g, 210, out double x) | Coordinate(g, 220, out double y) | Coordinate(g, 230, out double z);
            double length = Math.Sqrt(x * x + y * y + z * z);
            normal = !any || !(length > 0.0) || double.IsInfinity(length) ? new GeoVector3(0.0, 0.0, 1.0)
                : Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(x, y, z) : new GeoVector3(x / length, y / length, z / length);
            origin = OcsToWorld(normal, g.Point(10, g.Base, Origin));
            GeoPoint3 d = g.Point(11, g.Sub, new GeoPoint3(1.0, 0.0, 0.0));
            along = DxfOcsTransform.Normalized(new GeoVector3(d.X, d.Y, d.Z));
            down = DxfOcsTransform.Normalized(normal.CrossProduct(along).Multiply(FlowDirection == DxfFlowDirection.BottomToTop ? 1.0 : -1.0));
        }

        private static bool Coordinate(DxfDimGroups g, int code, out double value)
        {
            if (Numbers.TryRead(g.Text(code, g.All), out value) && Guard.IsFinite(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }

        /// <summary>
        /// The column (or row) a distance along the table falls in, by the widths (or heights) in their places: the first whose
        /// far side it does not pass by more than the slack; -1 before the first or past the last by more than it.
        /// </summary>
        private static int Place(Layout layout, IReadOnlyList<int> places, double distance, double slack)
        {
            if (distance < -slack)
            {
                return -1;
            }

            double far = 0.0;

            for (int k = 0; k < places.Count; k++)
            {
                far += Real(layout.Groups, places[k]);

                if (distance <= far + slack)
                {
                    return k;
                }
            }

            return -1;
        }

        #endregion

        #region Values, their kinds and formats

        /// <summary>
        /// Gets the kind of a cell's value, as AutoCAD's <c>DataType(row, col)</c>: from R2007 on its value's 90,
        /// <see cref="DxfDataType.Double"/> for 12.5 and <see cref="DxfDataType.String"/> for Parts; a text where it gives
        /// none, and before R2007, whose cells hold their text alone.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The kind.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfDataType DataType(int row, int col) => KindOf(Cell(row, col));

        /// <summary>
        /// Gets what a cell's number measures, as AutoCAD's <c>UnitType(row, col)</c>: from R2007 on its value's 94;
        /// <see cref="DxfUnitType.Unitless"/> where it gives none, and before R2007.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The unit.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfUnitType UnitType(int row, int col) => UnitOf(Cell(row, col));

        /// <summary>
        /// Gets the format of a cell's value, as AutoCAD's <c>Format(row, col)</c>: from R2007 on its value's 300, as
        /// <c>%lu2%pr2</c> for a decimal of two places; empty where it gives none, and before R2007.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The format, as AutoCAD writes it.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public string Format(int row, int col) => Cell(row, col).InValue(300) ?? string.Empty;

        /// <summary>Gets the format of a cell's value, as AutoCAD's <c>GetDataFormat(row, column)</c>: as <see cref="Format(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public string GetDataFormat(int row, int column) => Format(row, column);

        /// <summary>Gets the format of a content of a cell, as AutoCAD's <c>GetDataFormat(row, column, contentIndex)</c>: of its one content, nought, as <see cref="Format(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The format.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public string GetDataFormat(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return Format(row, column);
        }

        /// <summary>
        /// Gets the kind of a content of a cell's value with what its number measures, as AutoCAD's
        /// <c>GetDataType(row, column, contentIndex)</c>: of its one content, nought, <see cref="DataType(int, int)"/> and
        /// <see cref="UnitType(int, int)"/> together.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The kind and the unit.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public DxfDataTypeParameter GetDataType(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            CellView cell = Cell(row, column);
            return new DxfDataTypeParameter(KindOf(cell), UnitOf(cell));
        }

        /// <summary>
        /// Sets the value of a cell, as AutoCAD's <c>SetValue(row, col, value)</c>: a <see cref="string"/> a text, an
        /// <see cref="int"/>, <see cref="short"/>, <see cref="byte"/> or a <see cref="long"/> within the 32 bits AutoCAD's whole
        /// number holds a <see cref="DxfDataType.Long"/>, a <see cref="double"/> or <see cref="float"/> a
        /// <see cref="DxfDataType.Double"/>. From R2007 on the value is written of its kind (90), in 91, 140 or 1, its text
        /// (302) the number as the invariant culture writes it, 12.5, its unit (94) and format (300) kept: the library does not
        /// format a value, AutoCAD does when it draws the table anew. Before R2007 a cell holds a text alone, and the value is
        /// written as its text, as <see cref="SetTextString(int, int, string)"/> writes one. A pointer to a field (344) is taken
        /// out, its field erased where the table owns it and nothing else of the file points at it. The block AutoCAD drew the table with
        /// is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or a number is not finite or a whole number beyond 32 bits.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is of another type, as a <see cref="uint"/>, a <see cref="decimal"/> or a <see cref="bool"/>, or a date or a point, for which the library knows no groups (the DXF reference does not describe a cell's value), or a text with a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void SetValue(int row, int col, object value)
        {
            Written written = ValueToWrite(value, nameof(value));
            CellView cell = CellToWrite(row, col);
            CheckText(cell, row, col);

            if (cell.ValueFrom < 0)
            {
                if (string.Equals(written.Text, cell.Text(), StringComparison.Ordinal) && cell.Head(344) == null)
                {
                    return;
                }

                CheckEditable();
                string dropped = cell.Head(344);
                cell.SetText(written.Text);
                cell.Groups.Write(Order);
                EraseFields(new[] { dropped });
                return;
            }

            if (KindOf(cell) == written.Kind && SameValue(cell.Value(), written.Value) && cell.Head(344) == null)
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            cell.WriteValue(((int)written.Kind).ToString(CultureInfo.InvariantCulture), written.Data, written.Text, null, null);
            cell.DropField();
            cell.Groups.Write(Order);
            EraseFields(new[] { field });
        }

        /// <summary>Sets the value of a content of a cell, as AutoCAD's <c>SetValue(row, column, contentIndex, value)</c>: of its one content, nought, as <see cref="SetValue(int, int, object)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the content is not nought, or a number is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown when the value is of a kind not written.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void SetValue(int row, int column, int contentIndex, object value)
        {
            CheckContent(contentIndex);
            SetValue(row, column, value);
        }

        /// <summary>
        /// Empties a cell's value, as AutoCAD's <c>ResetValue</c>: an empty text, as <see cref="SetTextString(int, int, string)"/>
        /// writes one, its unit and format kept from R2007 on; nothing where the value is an empty text already. The block
        /// AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void ResetValue(int row, int col)
        {
            CellView cell = CellToWrite(row, col);
            CheckText(cell, row, col);

            if (KindOf(cell) == DxfDataType.String && cell.Text().Length == 0 && cell.Head(344) == null)
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            cell.SetText(string.Empty);
            cell.Groups.Write(Order);
            EraseFields(new[] { field });
        }

        /// <summary>
        /// Sets the format of a cell's value, as AutoCAD's <c>SetFormat(row, col, format)</c>: its value's 300, as AutoCAD writes
        /// it, <c>%lu2%pr2</c> for a decimal of two places, the value and its text kept; the library does not format the value,
        /// AutoCAD does when it draws the table anew. No flag of the cell's overrides is set: the format is written in its value,
        /// and <see cref="GetOverrides(int, int, int)"/> gives what it gave. A cell before R2007 holds no value to give a format.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="format">The format; empty for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the format is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the format has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds its text alone, as a file before R2007 writes it, or the table is erased.</exception>
        public void SetFormat(int row, int col, string format)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            if (format.IndexOf('\n') >= 0 || format.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A format is written on one line.", nameof(format));
            }

            CellView cell = CellToWrite(row, col);

            if (string.Equals(cell.InValue(300) ?? string.Empty, format, StringComparison.Ordinal))
            {
                return;
            }

            CheckValue(cell, row, col);
            CheckEditable();
            cell.WriteValue(cell.InValue(90) ?? "4", cell.ValueData(), cell.Text(), null, format);
            cell.Groups.Write(Order);
        }

        /// <summary>Sets the format of a cell's value, as AutoCAD's <c>SetDataFormat(row, column, format)</c>: as <see cref="SetFormat(int, int, string)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="format">The format; empty for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the format is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the format has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds no value, or the table is erased.</exception>
        public void SetDataFormat(int row, int column, string format) => SetFormat(row, column, format);

        /// <summary>Sets the format of a content of a cell, as AutoCAD's <c>SetDataFormat(row, column, contentIndex, format)</c>: of its one content, nought, as <see cref="SetFormat(int, int, string)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="format">The format; empty for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the format is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the format has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds no value, or the table is erased.</exception>
        public void SetDataFormat(int row, int column, int contentIndex, string format)
        {
            CheckContent(contentIndex);
            SetFormat(row, column, format);
        }

        /// <summary>
        /// Sets the kind of a cell's value and what its number measures, as AutoCAD's <c>SetDataType(row, col, dataType,
        /// unitType)</c>: its value's 90 and 94, from R2007 on. A value of another kind is turned into the new one: the text 12
        /// into the whole number 12, the whole number 12 into the number 12, a number into the text it shows (302); one that is
        /// not of the new kind, as the text Parts for a number, is refused, as is a kind other than a text, a whole number and
        /// a number, for which the library knows no groups: the DXF reference does not describe a cell's value. The unit alone
        /// is written in place, the value kept; a unit of several flags, as <c>Angle | AngleNotTransformed</c> (65 538), is
        /// written as it is. No flag of the cell's overrides is set, its 0x40000 among them: the kind is written in its value,
        /// and <see cref="GetOverrides(int, int, int)"/> gives what it gave.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="dataType">The kind: <see cref="DxfDataType.String"/>, <see cref="DxfDataType.Long"/> or <see cref="DxfDataType.Double"/>.</param>
        /// <param name="unitType">What its number measures, made of the flags of <see cref="DxfUnitType"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the unit holds a flag none of <see cref="DxfUnitType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind is not one written, or the value is not of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds its text alone, as a file before R2007 writes it, or the table is erased.</exception>
        public void SetDataType(int row, int col, DxfDataType dataType, DxfUnitType unitType)
        {
            CellView cell = CellToWrite(row, col);
            DxfDataType kind = KindOf(cell);

            if (kind == dataType && UnitOf(cell) == unitType)
            {
                return;
            }

            if (((int)unitType & ~UnitFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitType), unitType, "A unit is made of the flags of DxfUnitType, as Angle | AngleNotTransformed.");
            }

            CheckValue(cell, row, col);
            string unit = ((int)unitType).ToString(CultureInfo.InvariantCulture);

            if (kind == dataType)
            {
                CheckEditable();
                cell.WriteValue(cell.InValue(90) ?? "4", cell.ValueData(), cell.Text(), unit, null);
                cell.Groups.Write(Order);
                return;
            }

            Written written = Converted(cell.Value(), cell.Text(), dataType, nameof(dataType));
            CheckEditable();
            cell.WriteValue(((int)dataType).ToString(CultureInfo.InvariantCulture), written.Data, written.Text, unit, null);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Sets the kind of a content of a cell's value with what its number measures, as AutoCAD's <c>SetDataType(row, column,
        /// contentIndex, dataType)</c>: of its one content, nought, as <see cref="SetDataType(int, int, DxfDataType, DxfUnitType)"/>.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="dataType">The kind and the unit.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the content is not nought, or the unit holds a flag none of <see cref="DxfUnitType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind is not one written, or the value is not of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds no value, or the table is erased.</exception>
        public void SetDataType(int row, int column, int contentIndex, DxfDataTypeParameter dataType)
        {
            CheckContent(contentIndex);
            SetDataType(row, column, dataType.DataType, dataType.UnitType);
        }

        private static DxfDataType KindOf(CellView cell)
            => int.TryParse(cell.InValue(90)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind) ? (DxfDataType)kind : DxfDataType.String;

        private static DxfUnitType UnitOf(CellView cell)
            => int.TryParse(cell.InValue(94)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int unit) ? (DxfUnitType)unit : DxfUnitType.Unitless;

        private static bool SameValue(object now, object value)
            => now is double a && value is double b ? Same(a, b) : Equals(now, value);

        /// <summary>A value given as one of the kinds written: a text, a whole number of 32 bits, or a finite number.</summary>
        private static Written ValueToWrite(object value, string parameter)
        {
            switch (value)
            {
                case null:
                    throw new ArgumentNullException(parameter);
                case string text:
                    if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
                    {
                        throw new ArgumentException("A cell's text is written on one line: a new line is \\P.", parameter);
                    }

                    return new Written(DxfDataType.String, text, CellView.Pieces(text), text);
                case int whole:
                    return Whole(whole);
                case short whole:
                    return Whole(whole);
                case byte whole:
                    return Whole(whole);
                case long whole:
                    if (whole < int.MinValue || whole > int.MaxValue)
                    {
                        throw new ArgumentOutOfRangeException(parameter, whole, "AutoCAD's whole number of a cell holds 32 bits.");
                    }

                    return Whole((int)whole);
                case double real:
                    return Real(real, parameter);
                case float real:
                    return Real(real, parameter);
                case DateTime _:
                case GeoPoint2 _:
                case GeoPoint3 _:
                    throw new ArgumentException("A cell's value is written as a text, a whole number or a number: a date or a point is not, the library knowing no groups for it, as the DXF reference does not describe a cell's value.", parameter);
                default:
                    throw new ArgumentException("A cell's value is given as a string, an int, a short, a byte, a long within 32 bits, a double or a float: a " + value.GetType().Name + " is none of them.", parameter);
            }
        }

        private static Written Whole(int whole)
        {
            string text = whole.ToString(CultureInfo.InvariantCulture);
            return new Written(DxfDataType.Long, whole, new[] { new KeyValuePair<int, string>(91, text) }, text);
        }

        private static Written Real(double real, string parameter)
        {
            Guard.Finite(real, parameter, "A cell's number has to be finite.");
            string text = Numbers.Exact(real + 0.0);
            return new Written(DxfDataType.Double, real + 0.0, new[] { new KeyValuePair<int, string>(140, text) }, text);
        }

        /// <summary>A cell's value turned into another kind; refused where it is not of that kind.</summary>
        private static Written Converted(object now, string text, DxfDataType kind, string parameter)
        {
            switch (kind)
            {
                case DxfDataType.String:
                    return new Written(DxfDataType.String, text, CellView.Pieces(text), text);
                case DxfDataType.Long:
                    if (now is int whole)
                    {
                        return Whole(whole);
                    }

                    if (now is double real && Math.Floor(real) == real && real >= int.MinValue && real <= int.MaxValue)
                    {
                        return Whole((int)real);
                    }

                    if (now is string words && int.TryParse(words.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read))
                    {
                        return Whole(read);
                    }

                    throw new ArgumentException("The cell's value " + text + " is not a whole number of 32 bits.", parameter);
                case DxfDataType.Double:
                    if (now is int number)
                    {
                        return Real(number, parameter);
                    }

                    if (now is double kept)
                    {
                        return new Written(DxfDataType.Double, kept, new[] { new KeyValuePair<int, string>(140, Numbers.Exact(kept + 0.0)) }, text);
                    }

                    if (now is string figures && Numbers.TryRead(figures, out double parsed) && Guard.IsFinite(parsed))
                    {
                        return Real(parsed, parameter);
                    }

                    throw new ArgumentException("The cell's value " + text + " is not a number.", parameter);
                default:
                    throw new ArgumentException("A cell's value is written as a text, a whole number or a number: " + kind + " is not, the library knowing no groups for it, as the DXF reference does not describe a cell's value.", parameter);
            }
        }

        /// <summary>Refuses a block cell, whose content is its block, not a value of its own.</summary>
        private static void CheckText(CellView cell, int row, int col)
        {
            if (cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds a block, not a value.");
            }
        }

        /// <summary>Refuses a cell of no value of R2007, which holds its text alone, as a file before R2007 writes it.</summary>
        private static void CheckValue(CellView cell, int row, int col)
        {
            if (cell.ValueFrom < 0)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds its text alone, as a file before R2007 writes it, and no value (301 CELL_VALUE) to give a kind, a unit or a format.");
            }
        }

        /// <summary>A value to write: its kind, itself, its groups of data and its text.</summary>
        private sealed class Written
        {
            internal Written(DxfDataType kind, object value, IEnumerable<KeyValuePair<int, string>> data, string text)
            {
                Kind = kind;
                Value = value;
                Data = data;
                Text = text;
            }

            internal DxfDataType Kind { get; }

            internal object Value { get; }

            internal IEnumerable<KeyValuePair<int, string>> Data { get; }

            internal string Text { get; }
        }

        #endregion

        #region The look of a cell set

        /// <summary>
        /// Sets where a cell's content stands, as AutoCAD's <c>SetAlignment(row, col, align)</c>: the cell's own (170), its flag
        /// 1 set in its 177 (91 from R2007 on). The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="align">The alignment.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the alignment is not one of <see cref="DxfCellAlignment"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetAlignment(int row, int col, DxfCellAlignment align)
        {
            SetOverride(
                row,
                col,
                170,
                ((int)align).ToString(CultureInfo.InvariantCulture),
                now => int.TryParse(now?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value == (int)align,
                () =>
                {
                    if (align < DxfCellAlignment.TopLeft || align > DxfCellAlignment.BottomRight)
                    {
                        throw new ArgumentOutOfRangeException(nameof(align), align, "An alignment is one of DxfCellAlignment, 1 to 9.");
                    }
                });
        }

        /// <summary>
        /// Sets the height of a cell's text, as AutoCAD's <c>SetTextHeight(row, col, height)</c>: the cell's own (140), its flag
        /// 0x20 set. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="height">The height, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetTextHeight(int row, int col, double height)
        {
            SetOverride(
                row,
                col,
                140,
                Numbers.Exact(height + 0.0),
                now => Numbers.TryRead(now, out double value) && Guard.IsFinite(value) && Same(value, height),
                () => Guard.Positive(height, nameof(height), "A text's height has to be a finite number above nought."));
        }

        /// <summary>Sets the height of the text of a content of a cell, as AutoCAD's <c>SetTextHeight(row, column, contentIndex, height)</c>: of its one content, nought, as <see cref="SetTextHeight(int, int, double)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="height">The height, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the content is not nought, or the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetTextHeight(int row, int column, int contentIndex, double height)
        {
            CheckContent(contentIndex);
            SetTextHeight(row, column, height);
        }

        /// <summary>
        /// Sets the text style of a cell's text, as AutoCAD's <c>SetTextStyle(row, col, id)</c> takes its id: the cell's own (7,
        /// by name), its flag 0x10 set; <c>table.SetTextStyle(1, 0, db.TextStyleTable["GH_TXT"])</c> writes 7 GH_TXT. The block
        /// AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="style">The style, a record of the table's drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the style is erased, in no drawing, or of another drawing than the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetTextStyle(int row, int col, DxfTextStyleTableRecord style)
        {
            string name = TextStyleOf(style, nameof(style));
            SetOverride(row, col, 7, name, now => now != null && string.Equals(DxfReader.NameOf(now), name, StringComparison.OrdinalIgnoreCase), null);
        }

        /// <summary>
        /// Sets the colour of a cell's content, as AutoCAD's <c>SetContentColor(row, col, color)</c>: the cell's own (64, of the
        /// index), its flag 0x08 set. A colour of the index, BYLAYER, BYBLOCK or the foreground (7). The block AutoCAD drew the
        /// table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="color">The colour.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none, which the cell's group, of the index, does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetContentColor(int row, int col, DxfColor color) => SetColour(row, col, 64, color, nameof(color));

        /// <summary>Sets the colour of a content of a cell, as AutoCAD's <c>SetContentColor(row, column, contentIndex, color)</c>: of its one content, nought, as <see cref="SetContentColor(int, int, DxfColor)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="color">The colour.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetContentColor(int row, int column, int contentIndex, DxfColor color)
        {
            CheckContent(contentIndex);
            SetContentColor(row, column, color);
        }

        /// <summary>
        /// Sets the colour of a cell's background, as AutoCAD's <c>SetBackgroundColor(row, col, color)</c>: the cell's own (63, of
        /// the index), its flag 0x04 set; whether the background is filled is <see cref="SetBackgroundColorNone(int, int, bool)"/>'s,
        /// left as it is. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="color">The colour: of the index, BYLAYER, BYBLOCK or the foreground (7).</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetBackgroundColor(int row, int col, DxfColor color) => SetColour(row, col, 63, color, nameof(color));

        /// <summary>
        /// Sets whether a cell's background is left unfilled, as AutoCAD's <c>SetBackgroundColorNone(row, col, value)</c>: the
        /// cell's own (283, 0 unfilled and 1 filled), its flag 0x02 set. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="value">Whether it is left unfilled.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetBackgroundColorNone(int row, int col, bool value)
            => SetOverride(row, col, 283, value ? "0" : "1", now => now != null && (now.Trim() == "0") == value, null);

        /// <summary>
        /// Sets what a cell holds, as AutoCAD's <c>SetCellType</c>: a block cell made a text cell (171 of 1), its block, scale and
        /// attribute values (340, 144, 179, 331, 300) taken out and its text empty. A text cell is made a block cell with its
        /// block, which the library does not set, and is refused. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="type">What it holds: <see cref="DxfTableCellType.TextCell"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the kind is none of a text and a block.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a text cell is to be made a block cell, or the table is erased.</exception>
        public void SetCellType(int row, int col, DxfTableCellType type)
        {
            CellView cell = CellToWrite(row, col);

            if (cell.Whole(171, 0) == (int)type)
            {
                return;
            }

            if (type == DxfTableCellType.BlockCell)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds a text: a block cell is made with its block, which the library does not set.");
            }

            if (type != DxfTableCellType.TextCell)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "A cell of a file holds a text or a block.");
            }

            CheckEditable();
            int end = cell.ValueFrom >= 0 ? cell.ValueFrom : cell.OverridesFrom;
            int taken = 0;

            for (int i = end - 1; i > cell.From; i--)
            {
                int code = cell.Groups.List[i].Key;

                if (code == 340 || code == 144 || code == 179 || code == 331 || code == 300)
                {
                    cell.Groups.RemoveAt(i);
                    taken++;
                }
            }

            cell.Groups.List[cell.From] = new KeyValuePair<int, string>(171, "1");
            var text = new CellView(cell.Groups, cell.From, cell.To - taken);
            string field = text.Head(344);
            text.SetText(string.Empty);
            text.Groups.Write(Order);
            EraseFields(new[] { field });
        }

        /// <summary>
        /// Gets the turn of a cell's text, as AutoCAD's <c>TextRotation</c>: its 145, a quarter turn at a time,
        /// <see cref="DxfRotationAngle.Degrees090"/> for pi / 2; <see cref="DxfRotationAngle.DegreesUnknown"/> for an angle not
        /// within <see cref="Tolerance.Default"/> (of a vector, 1E-5) of a quarter turn.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The turn.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfRotationAngle TextRotation(int row, int col) => Quarters(BlockRotation(row, col));

        /// <summary>
        /// Sets the turn of a cell's text, as AutoCAD's <c>SetTextRotation</c>: its 145, a quarter turn at a time, pi / 2 for
        /// <see cref="DxfRotationAngle.Degrees090"/>. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="rot">The turn, one of the four.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the turn is none of the four.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetTextRotation(int row, int col, DxfRotationAngle rot)
        {
            SetHeadNumber(
                row,
                col,
                145,
                (int)rot * (Math.PI / 2.0),
                now => Quarters(now) == rot,
                () =>
                {
                    if (rot < DxfRotationAngle.Degrees000 || rot > DxfRotationAngle.Degrees270)
                    {
                        throw new ArgumentOutOfRangeException(nameof(rot), rot, "A cell's text turns a quarter at a time: 0, 90, 180 or 270 degrees.");
                    }
                });
        }

        /// <summary>
        /// Sets the turn of a cell's content, as AutoCAD's <c>SetRotation(row, column, contentIndex, angle)</c>: its 145, in
        /// radians, of a text cell or a block cell, as <see cref="GetRotation(int, int, int)"/> reads it. The block AutoCAD drew
        /// the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="angle">The angle, in radians.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the content is not nought, or the angle is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetRotation(int row, int column, int contentIndex, double angle)
        {
            CheckContent(contentIndex);
            SetHeadNumber(row, column, 145, angle, now => Same(now, angle), () => Guard.Finite(angle, nameof(angle), "An angle has to be a finite number."));
        }

        /// <summary>Sets the scale of a content of a cell, as AutoCAD's <c>SetScale(row, column, contentIndex, scale)</c>: of its one content, nought, a block cell's, as <see cref="SetBlockScale(int, int, double)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="scale">The scale, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the content is not nought, or the scale is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is not a block cell, or the table is erased.</exception>
        public void SetScale(int row, int column, int contentIndex, double scale)
        {
            CheckContent(contentIndex);
            SetBlockScale(row, column, scale);
        }

        /// <summary>Gets whether a block cell's block is fitted to the cell, as AutoCAD's <c>IsAutoScale</c>: its 174 not nought; false where it gives none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public bool IsAutoScale(int row, int col) => Cell(row, col).Whole(174, 0) != 0;

        /// <summary>Gets whether a content of a cell is fitted to the cell, as AutoCAD's <c>GetIsAutoScale(row, column, contentIndex)</c>: of its one content, nought, as <see cref="IsAutoScale(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public bool GetIsAutoScale(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return IsAutoScale(row, column);
        }

        /// <summary>
        /// Sets whether a block cell's block is fitted to the cell, as AutoCAD's <c>SetAutoScale</c>: its 174, 1 or 0. The block
        /// AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="autoFit">Whether it is fitted.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is not a block cell, or the table is erased.</exception>
        public void SetAutoScale(int row, int col, bool autoFit)
        {
            CellView cell = CellToWrite(row, col);

            if (cell.Whole(171, 1) != (int)DxfTableCellType.BlockCell)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds a text, not a block.");
            }

            if ((cell.Whole(174, 0) != 0) == autoFit)
            {
                return;
            }

            CheckEditable();
            cell.SetHead(174, autoFit ? "1" : "0");
            cell.Groups.Write(Order);
        }

        /// <summary>Sets whether a content of a cell is fitted to the cell, as AutoCAD's <c>SetIsAutoScale(row, column, contentIndex, autoFit)</c>: of its one content, nought, as <see cref="SetAutoScale(int, int, bool)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="autoFit">Whether it is fitted.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is not a block cell, or the table is erased.</exception>
        public void SetIsAutoScale(int row, int column, int contentIndex, bool autoFit)
        {
            CheckContent(contentIndex);
            SetAutoScale(row, column, autoFit);
        }

        /// <summary>
        /// Writes a cell's override and its flag, its flags in a group of 91 from R2007 on, of 177 before, where it gives none;
        /// nothing where the override the cell has of its own (its text, null for none) is the one given, which is checked
        /// before it is written.
        /// </summary>
        private void SetOverride(int row, int col, int code, string text, Func<string, bool> same, Action check)
        {
            CellView cell = CellToWrite(row, col);

            if (same(cell.Override(code, Layout.CellOverrides[code])))
            {
                return;
            }

            check?.Invoke();
            CheckEditable();
            cell.SetOverride(code, text, Item.File.Supports(1021) ? 91 : 177);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Writes a number of a cell's head, in its place among the head's groups; nothing where the number it reads (nought where
        /// it gives none) is the one given, which is checked before it is written.
        /// </summary>
        private void SetHeadNumber(int row, int col, int code, double value, Func<double, bool> same, Action check)
        {
            CellView cell = CellToWrite(row, col);

            if (same(Numbers.TryRead(cell.Head(code), out double now) && Guard.IsFinite(now) ? now : 0.0))
            {
                return;
            }

            check();
            CheckEditable();
            cell.SetHead(code, Numbers.Exact(value + 0.0));
            cell.Groups.Write(Order);
        }

        /// <summary>A turn read as a quarter at a time: the quarter within a vector's tolerance of it; none of the four else.</summary>
        private static DxfRotationAngle Quarters(double angle)
        {
            double quarters = angle / (Math.PI / 2.0);
            double nearest = Math.Round(quarters);

            if (Math.Abs(quarters - nearest) * (Math.PI / 2.0) > Tolerance.Default.EqualVector)
            {
                return DxfRotationAngle.DegreesUnknown;
            }

            return (DxfRotationAngle)(int)((nearest % 4.0 + 4.0) % 4.0);
        }

        /// <summary>Writes a cell's colour of the index (63 or 64) where it differs from the one it has of its own.</summary>
        private void SetColour(int row, int col, int code, DxfColor color, string parameter)
        {
            if (color == null)
            {
                throw new ArgumentNullException(parameter);
            }

            int index;

            switch (color.ColorMethod)
            {
                case DxfColorMethod.ByAci:
                case DxfColorMethod.ByLayer:
                case DxfColorMethod.ByBlock:
                case DxfColorMethod.Foreground:
                    index = color.ColorIndex;
                    break;
                default:
                    throw new ArgumentException("A cell's colour is written as a colour of AutoCAD's index (group " + code.ToString(CultureInfo.InvariantCulture) + "): " + color + " is not one.", parameter);
            }

            DxfColor wanted = DxfColor.OfIndex(index);
            SetOverride(row, col, code, index.ToString(CultureInfo.InvariantCulture), now => wanted.Equals(Colour(now)), null);
        }

        #endregion

        #region Overrides

        /// <summary>
        /// Gets what a content of a cell holds of its own over its table style, as AutoCAD's <c>GetOverrides(row, column,
        /// contentIndex)</c>: by its flags (177, or 91 from R2007 on), its alignment (flag 1) <see cref="DxfCellProperties.Alignment"/>,
        /// its background (2 and 4) <see cref="DxfCellProperties.BackgroundColor"/>, its colour (8)
        /// <see cref="DxfCellProperties.ContentColor"/>, its text style (0x10) and height (0x20), and its kind of value
        /// (0x40000) <see cref="DxfCellProperties.DataType"/>; by the overrides it holds where it gives no flags. The cell of
        /// flags 49 has <see cref="DxfCellProperties.Alignment"/>, <see cref="DxfCellProperties.TextStyle"/> and
        /// <see cref="DxfCellProperties.TextHeight"/>.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The overrides.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public DxfCellProperties GetOverrides(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            CellView cell = Cell(row, column);
            return PropertiesOf(cell.Flags ?? cell.Held());
        }

        /// <summary>
        /// Keeps of what a content of a cell holds of its own over its table style those given, as AutoCAD's
        /// <c>SetOverrides(row, column, contentIndex, override)</c>: the others taken away, their groups and flags, so that the
        /// cell takes them from its table style; <see cref="DxfCellProperties.Invalid"/> takes its look away. One of its look it
        /// does not hold is given by its setter (<see cref="SetAlignment(int, int, DxfCellAlignment)"/> and the others), the
        /// library not reading the table style whose value it would take, and is refused here; its kind of value, which no
        /// setter of the library gives, can be kept only where the cell holds it already. Its kind of value
        /// (<see cref="DxfCellProperties.DataType"/>, the flag 0x40000), for which the library knows no groups, is kept whether
        /// given or not, as <see cref="RemoveAllOverrides(int, int)"/> keeps it: the cell of flags 262 193 given
        /// <see cref="DxfCellProperties.Invalid"/> keeps 262 144. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="overrides">The overrides to keep.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when an override is given that the cell does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetOverrides(int row, int column, int contentIndex, DxfCellProperties overrides)
        {
            CheckContent(contentIndex);
            CellView cell = CellToWrite(row, column);
            DxfCellProperties now = PropertiesOf(cell.Flags ?? cell.Held());
            DxfCellProperties added = overrides & ~now;

            if (added != DxfCellProperties.Invalid)
            {
                string how = added == DxfCellProperties.DataType ? "the library gives no cell that flag, so it is kept only where the cell holds it" : "it is given one by its setter, as SetAlignment";
                throw new ArgumentException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + column.ToString(CultureInfo.InvariantCulture) + ") holds no " + added + " of its own to keep: " + how + ".", nameof(overrides));
            }

            // Its kind of value is kept, given or not, as RemoveAllOverrides keeps it.
            DxfCellProperties taken = now & ~overrides & ~DxfCellProperties.DataType;

            if (taken == DxfCellProperties.Invalid)
            {
                return;
            }

            CheckEditable();
            cell.RemoveOverrides(FlagsOf(taken));
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Takes away all a cell holds of its own over its table style, as AutoCAD's <c>RemoveAllOverrides</c>: its alignment,
        /// background, colours, text style and height, and the colours, lineweights and showing of its borders, their groups
        /// and their 18 flags; a flag the file gives no groups for, as that of its kind of value (0x40000), is kept. The block
        /// AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void RemoveAllOverrides(int row, int column)
        {
            CellView cell = CellToWrite(row, column);

            if ((((cell.Flags ?? 0) | cell.Held()) & CellOverrideFlags) == 0)
            {
                return;
            }

            CheckEditable();
            cell.RemoveOverrides(CellOverrideFlags);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Gets what the table holds of its own over its table style, as AutoCAD's <c>TableStyleOverrides</c>: by the flags of
        /// its head, 93 bit k the override numbered k + 1 (<see cref="DxfTableStyleOverride.TitleSuppressed"/> to
        /// <see cref="DxfTableStyleOverride.DataRowDataType"/>), the colours of its borders 94 from 40 on, their lineweights 95
        /// from 70 on and their showing 96 from 100 on; a table of 93 of 0x08 holds
        /// <see cref="DxfTableStyleOverride.HorizontalCellMargin"/>. The overrides of its cells are
        /// <see cref="CellStyleOverrides(int, int)"/>'s.
        /// </summary>
        /// <returns>The overrides, in the order of their numbers.</returns>
        public DxfTableStyleOverride[] TableStyleOverrides()
        {
            Layout layout = Read();
            DxfDimGroups g = layout.Groups;
            var head = new DxfDimGroups.Range(g.Sub.From, layout.CellStart.Count > 0 ? layout.CellStart[0] : g.Sub.To);
            var found = new List<DxfTableStyleOverride>();
            int[] codes = { 93, 94, 95, 96 };
            int[] firsts = { 1, 40, 70, 100 };
            int[] counts = { 26, 18, 18, 18 };

            for (int k = 0; k < codes.Length; k++)
            {
                int flags = g.Whole(codes[k], head, 0);

                for (int bit = 0; bit < counts[k]; bit++)
                {
                    if ((flags & (1 << bit)) != 0)
                    {
                        found.Add((DxfTableStyleOverride)(firsts[k] + bit));
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Takes away what the table and its cells hold of their own over the table style, as AutoCAD's
        /// <c>ClearTableStyleOverrides(options)</c>: 0 both, 1 the table's alone, 2 its cells' alone. The table's are the groups
        /// after its last cell the DXF reference lists as its overrides (70, 40, 41, 280, 281, the looks by row and borders, 97,
        /// 98 and 4), and its flags 93 to 96 set to nought; a cell's are those <see cref="RemoveAllOverrides(int, int)"/> takes
        /// away. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="options">0, 1 or 2.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the options are none of 0, 1 and 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void ClearTableStyleOverrides(int options)
        {
            if (options < 0 || options > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options, "The options are 0 for the table and its cells, 1 for the table alone, 2 for its cells alone.");
            }

            Layout layout = ReadToWrite();
            DxfDimGroups g = layout.Groups;

            if (options != 2)
            {
                int own = layout.CellStart.Count > 0 ? layout.CellsEnd : g.List.Count;
                int end = own;

                while (end < g.List.Count && g.List[end].Key != 100)
                {
                    end++;
                }

                for (int i = end - 1; i >= own; i--)
                {
                    if (TableOverrides.Contains(g.List[i].Key))
                    {
                        g.RemoveAt(i);
                    }
                }

                int to = layout.CellStart.Count > 0 ? layout.CellStart[0] : g.Sub.To;

                for (int i = g.Sub.From; i < to; i++)
                {
                    int code = g.List[i].Key;

                    if (code >= 93 && code <= 96 && g.List[i].Value.Trim() != "0")
                    {
                        g.List[i] = new KeyValuePair<int, string>(code, "0");
                    }
                }
            }

            // The cells' overrides marked, each group left where it stands, then taken out of the list in one pass.
            if (options != 1)
            {
                var marked = new bool[g.List.Count];

                for (int k = 0; k < Math.Min(layout.Rows * layout.Columns, layout.CellStart.Count); k++)
                {
                    CellView cell = layout.CellAt(k / layout.Columns, k % layout.Columns);

                    if ((((cell.Flags ?? 0) | cell.Held()) & CellOverrideFlags) != 0)
                    {
                        cell.MarkOverrides(CellOverrideFlags, marked);
                    }
                }

                g.RemoveMarked(marked);
            }

            if (!g.Same())
            {
                CheckEditable();
                g.Write(Order);
            }
        }

        private static DxfCellProperties PropertiesOf(int flags)
        {
            DxfCellProperties found = DxfCellProperties.Invalid;
            found |= (flags & 0x01) != 0 ? DxfCellProperties.Alignment : 0;
            found |= (flags & 0x06) != 0 ? DxfCellProperties.BackgroundColor : 0;
            found |= (flags & 0x08) != 0 ? DxfCellProperties.ContentColor : 0;
            found |= (flags & 0x10) != 0 ? DxfCellProperties.TextStyle : 0;
            found |= (flags & 0x20) != 0 ? DxfCellProperties.TextHeight : 0;
            found |= (flags & DataTypeFlag) != 0 ? DxfCellProperties.DataType : 0;
            return found;
        }

        private static int FlagsOf(DxfCellProperties properties)
        {
            int flags = 0;
            flags |= (properties & DxfCellProperties.Alignment) != 0 ? 0x01 : 0;
            flags |= (properties & DxfCellProperties.BackgroundColor) != 0 ? 0x06 : 0;
            flags |= (properties & DxfCellProperties.ContentColor) != 0 ? 0x08 : 0;
            flags |= (properties & DxfCellProperties.TextStyle) != 0 ? 0x10 : 0;
            flags |= (properties & DxfCellProperties.TextHeight) != 0 ? 0x20 : 0;
            return flags;
        }

        #endregion

        /// <summary>A list read as it stands each time: its count and its items worked out when asked for.</summary>
        private sealed class Live<T> : IReadOnlyList<T>
        {
            private readonly Func<int> _count;
            private readonly Func<int, T> _item;

            internal Live(Func<int> count, Func<int, T> item)
            {
                _count = count;
                _item = item;
            }

            public int Count => _count();

            public T this[int index]
            {
                get
                {
                    int count = _count();
                    CheckIndex(index, count, nameof(index), typeof(T) == typeof(DxfRow) ? "row" : "column");
                    return _item(index);
                }
            }

            public IEnumerator<T> GetEnumerator()
            {
                int count = _count();

                for (int i = 0; i < count; i++)
                {
                    yield return _item(i);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
