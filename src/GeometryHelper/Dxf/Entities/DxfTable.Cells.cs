using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
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
        /// (302) the number as the invariant culture writes it, 7, its unit (94) kept and its format (300) taken away, as
        /// AutoCAD 2021 wrote 100.125 given to a cell of 1 in <c>%lu4%pr3</c> (D33 u7), its flags (93) made 6 where they had
        /// the flag 1 of no value, as AutoCAD writes a value it is given; the text of a number of its kind
        /// <see cref="DxfDataType.Double"/> as AutoCAD 2021 wrote it and showed it, of six places of decimals, 12.500000 for
        /// 12.5 (D28e v09, w30 to w33), where the library wrote 12.5, which AutoCAD showed as it was. Before R2007 a cell holds a text alone, and the value is
        /// written as its text, as <see cref="SetTextString(int, int, string)"/> writes one, a number as the invariant culture
        /// writes it, 12.5, which keeps it whole where AutoCAD 2021 wrote 12.500000 (w09): the text is all the cell holds. A pointer to a field (344) is taken
        /// out, its field erased where the table owns it and nothing else of the file points at it. The value the cell has, a
        /// text as the file writes it or as it reads, changes nothing. The block AutoCAD drew the table with is not drawn anew.
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
            string shown = written.Value is double real && cell.ValueFrom >= 0 ? RealText(real, null) : written.Text;

            if (cell.ValueFrom < 0)
            {
                if (cell.Holds(shown) && cell.Head(344) == null)
                {
                    return;
                }

                CheckEditable();
                string dropped = cell.Head(344);
                cell.SetText(shown);
                cell.Groups.Write(Order);
                EraseFields(new[] { dropped });
                return;
            }

            object now = cell.Value();

            if (KindOf(cell) == written.Kind && (SameValue(now, written.Value) || (now is string text && SameValue(DxfReader.Decode(text), written.Value))) && cell.Head(344) == null)
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            cell.WriteValue(((int)written.Kind).ToString(CultureInfo.InvariantCulture), written.Data, shown, null, string.Empty, true);
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
        /// Empties a cell's value, as AutoCAD's <c>ResetValue</c>: from R2007 on a value of none, of the flags 7 and the kind 0
        /// and an empty text, its unit and format kept, the flag of its kind of value (0x40000) taken out, as AutoCAD 2021 wrote
        /// Qty reset (<c>91 0</c>, <c>93 7</c>, <c>90 0</c>, D28e v10) and read it as of no value, where the library wrote an
        /// empty text; before R2007 an empty text. Nothing where the cell holds no value already. The block AutoCAD drew the
        /// table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void ResetValue(int row, int col)
        {
            CellView cell = CellToWrite(row, col);
            CheckText(cell, row, col);

            if (cell.HoldsNoValue())
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            cell.SetNoValue();
            cell.Groups.Write(Order);
            EraseFields(new[] { field });
        }

        /// <summary>
        /// Sets the format of a cell's value, as AutoCAD's <c>SetFormat(row, col, format)</c>: its value's 300, as AutoCAD writes
        /// it, <c>%lu2%pr2</c> for a decimal of two places, the value kept; the text of a number of its kind
        /// <see cref="DxfDataType.Double"/> written as AutoCAD 2021 wrote it, of the places a decimal format gives, 12.500 for
        /// <c>%lu2%pr3</c>, in feet and inches for an architectural format, 1'-0 1/2" for 12.5 in <c>%lu4%pr2</c> (D33 u1 to u8;
        /// to the nearest 1/2^N of an inch, four where no <c>%prN</c> is given, <c>%zs1</c> and <c>%zs2</c> leaving out nought
        /// feet and nought inches), and of six where the format is taken away (D28e v11), any other text kept, the library not
        /// formatting the value of a format of another unit or another kind, nor one of more feet than 32 bits hold. No flag of the cell's overrides is set: the format is written in its value,
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
            string shown = cell.Value() is double real ? RealText(real, format) ?? cell.Text() : cell.Text();
            cell.WriteValue(cell.InValue(90) ?? "4", cell.ValueData(), shown, null, format);
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
        /// unitType)</c>: its value's 90 and 94, from R2007 on, as AutoCAD 2021 turned each value (D33 d1 to d12, t1 to t16).
        /// A whole number becomes the number 7, shown 7.000000, or the text 7; a number the whole number it holds cut towards
        /// nought, 12.7 12 and -12.7 -12, one beyond 32 bits -2147483648, or the text it shows, 12.500000; a text the whole
        /// number it reads, 12 of " 12 " and of 12.25, or the number it reads, shown in the drawing's units and given the
        /// format <c>%lu6</c> of them, 12.0000 of 12 where LUNITS is 2 and LUPREC 4, or <c>%lu1</c> of powers of ten for a text
        /// written in them, 1.0000E+03 of 1e3. Each takes away the flag 2 of the value (93 6 made 4) but a text of a whole number
        /// made that number. A value not of the new kind, the text B-12 for a number, becomes a value of none of that kind,
        /// <c>93 7</c>, an empty text, its unit not written; a value of none stays one, its flag 2 taken away (<c>93 5</c>).
        /// AutoCAD refuses no kind; a kind other than a text, a whole number and a number is refused here, the library knowing
        /// no groups for it: the DXF reference does not describe a cell's value. The unit alone is written in place, the value
        /// kept; a unit of several flags, as <c>Angle | AngleNotTransformed</c> (65 538), is written as it is. No flag of the
        /// cell's overrides is set, its 0x40000 among them: the kind is written in its value, and
        /// <see cref="GetOverrides(int, int, int)"/> gives what it gave. A drawing's units of engineering or fractions are not
        /// written by the library: a text read as a number in them is written as the invariant culture writes it.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="dataType">The kind: <see cref="DxfDataType.String"/>, <see cref="DxfDataType.Long"/> or <see cref="DxfDataType.Double"/>.</param>
        /// <param name="unitType">What its number measures, made of the flags of <see cref="DxfUnitType"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the unit holds a flag none of <see cref="DxfUnitType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind is not one written.</exception>
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

            if (dataType != DxfDataType.String && dataType != DxfDataType.Long && dataType != DxfDataType.Double)
            {
                throw new ArgumentException("A cell's value is written as a text, a whole number or a number: " + dataType + " is not, the library knowing no groups for it, as the DXF reference does not describe a cell's value.", nameof(dataType));
            }

            int flags = int.TryParse(cell.InValue(93)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read) ? read : 6;
            string newKind = ((int)dataType).ToString(CultureInfo.InvariantCulture);
            bool keepFlags = false;
            string format = null;
            Written written = (flags & 1) != 0 ? null : Turned(cell, dataType, out keepFlags, out format);
            CheckEditable();

            if (written == null)
            {
                // No value of the new kind: a value of none (93 7) where the cell held one, the flag 2 taken away where it held none (93 5); its unit not written.
                string none = ((flags & 1) != 0 ? flags & ~2 : flags | 1).ToString(CultureInfo.InvariantCulture);
                cell.WriteValue(newKind, new List<KeyValuePair<int, string>>(), string.Empty, null, null, false, none);
            }
            else
            {
                cell.WriteValue(newKind, written.Data, written.Text, unit, format, false, (keepFlags ? flags : flags & ~2).ToString(CultureInfo.InvariantCulture));
            }

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

                    return new Written(DxfDataType.String, text, CellView.Pieces(text, 2, 1), text);
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

        /// <summary>
        /// The text AutoCAD 2021 shows a number by, as it wrote it (D28e v09, v11, w30 to w33): of six places of decimals where
        /// the value has no format, 12.500000, 0.333333 and -0.125000; of the places of a decimal format (<c>%lu2%prN</c>), 12.500
        /// for <c>%lu2%pr3</c>; of feet and inches for an architectural format (<c>%lu4</c>, <see cref="Architectural"/>);
        /// null for a format of another kind, which the library does not write. The sign is kept on a decimal number that
        /// rounds to nought, as AutoCAD's printf writes it, on every runtime.
        /// </summary>
        private static string RealText(double value, string format)
        {
            string places = string.IsNullOrEmpty(format) ? "6" : null;
            Match match = places == null ? Regex.Match(format, "^%lu2%pr([0-9]{1,2})$") : null;
            places = match != null && match.Success ? match.Groups[1].Value : places;

            if (places == null)
            {
                Match feet = Regex.Match(format, "^%lu4(?:%pr([0-9]))?(?:%zs([0-9]{1,2}))?$");
                return feet.Success ? Architectural(value, feet.Groups[1].Success ? feet.Groups[1].Value[0] - '0' : 4, feet.Groups[2].Success ? int.Parse(feet.Groups[2].Value, CultureInfo.InvariantCulture) : 0) : null;
            }

            string text = Math.Abs(value).ToString("F" + places, CultureInfo.InvariantCulture);
            return value < 0.0 ? "-" + text : text;
        }

        /// <summary>
        /// A number of inches in feet and inches, as AutoCAD 2021 wrote a value of an architectural format (D33 P, P2, u1 to
        /// u8): to the nearest 1/2^N of an inch for <c>%prN</c> (four where none is given, N from 0 to 9), a half away from
        /// nought, the fraction reduced, 12.5 of <c>%lu4%pr2</c> 1'-0 1/2" and 11.99 1'-0"; a minus only where the number
        /// rounded is not nought, -0.25 -0'-0 1/4" and -0.001 0'-0"; with <c>%zs1</c> nought feet left out (7.25 7 1/4",
        /// 0.75 3/4", 0 0") and with <c>%zs2</c> nought inches (12 1', 0 0'), the other flags changing nothing. Null for a
        /// number of more feet than 32 bits hold, which AutoCAD writes as garbage or in powers of ten.
        /// </summary>
        private static string Architectural(double value, int precision, int zeros)
        {
            long parts = 1L << precision;
            double rounded = Math.Round(Math.Abs(value) * parts, MidpointRounding.AwayFromZero);

            if (rounded / (12 * parts) > int.MaxValue)
            {
                return null;
            }

            long count = (long)rounded;
            long feet = count / (12 * parts);
            long inches = count % (12 * parts) / parts;
            long fraction = count % parts;
            long under = parts;

            while (fraction != 0 && fraction % 2 == 0)
            {
                fraction /= 2;
                under /= 2;
            }

            string sign = count != 0 && value < 0.0 ? "-" : string.Empty;
            string inch = fraction == 0 ? inches.ToString(CultureInfo.InvariantCulture) : inches == 0 && feet == 0 && (zeros & 1) != 0 ? fraction + "/" + under : inches + " " + fraction + "/" + under;
            string text;

            if (feet == 0 && (zeros & 1) != 0)
            {
                text = inch + "\"";
            }
            else if (inches == 0 && fraction == 0 && (zeros & 2) != 0)
            {
                text = feet.ToString(CultureInfo.InvariantCulture) + "'";
            }
            else
            {
                text = feet.ToString(CultureInfo.InvariantCulture) + "'-" + inch + "\"";
            }

            return sign + text;
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

        /// <summary>
        /// A cell's value turned into another kind as AutoCAD 2021 turns it (D33 d1 to d12, t1 to t16): its data, its text,
        /// whether the flags of the value are kept (a text of a whole number made that number) or lose the flag 2, and the
        /// format written (<c>%lu6</c> or <c>%lu1</c> for a text read as a number, null to keep the cell's); null where the
        /// value is not of the kind, which leaves a value of none.
        /// </summary>
        private Written Turned(CellView cell, DxfDataType kind, out bool keepFlags, out string format)
        {
            object now = cell.Value();
            string text = cell.Text();
            keepFlags = false;
            format = null;

            switch (kind)
            {
                case DxfDataType.String:
                    string shown = now is int whole ? whole.ToString(CultureInfo.InvariantCulture) : text;
                    return new Written(DxfDataType.String, shown, CellView.Pieces(shown, 2, 1), shown);
                case DxfDataType.Long:
                    if (now is double real)
                    {
                        return Whole(Cut(real));
                    }

                    if (now is string words && int.TryParse(words.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read))
                    {
                        keepFlags = true;
                        return Whole(read);
                    }

                    return now is string figures && Numbers.TryRead(figures, out double parsed) && Guard.IsFinite(parsed) ? Whole(Cut(parsed)) : null;
                default:
                    if (now is int number)
                    {
                        return new Written(DxfDataType.Double, (double)number, new[] { new KeyValuePair<int, string>(140, Numbers.Exact(number + 0.0)) }, RealText(number, cell.InValue(300)) ?? RealText(number, null));
                    }

                    if (!(now is string digits) || !Numbers.TryRead(digits, out double value) || !Guard.IsFinite(value))
                    {
                        return null;
                    }

                    bool tens = digits.IndexOf('e') >= 0 || digits.IndexOf('E') >= 0;
                    DxfDatabase database = IsResident ? Item.File.Database : null;
                    format = tens ? "%lu1" : "%lu6";
                    return new Written(DxfDataType.Double, value + 0.0, new[] { new KeyValuePair<int, string>(140, Numbers.Exact(value + 0.0)) }, UnitsText(value, tens ? 1 : database?.Lunits ?? 2, database?.Luprec ?? 4));
            }
        }

        /// <summary>A number cut towards nought to a whole number, as AutoCAD 2021 cut 12.7 to 12 and -12.7 to -12; one beyond 32 bits -2147483648, as it wrote 3E9 (D33 t4, t5, DT).</summary>
        private static int Cut(double value)
        {
            double cut = Math.Truncate(value);
            return cut < int.MinValue || cut > int.MaxValue ? int.MinValue : (int)cut;
        }

        /// <summary>
        /// A number shown in a drawing's units (LUNITS) to its places (LUPREC), as AutoCAD 2021 showed a text made a number of
        /// <c>%lu6</c> or <c>%lu1</c> (D33 t1, t2, t16): of powers of ten 1.0000E+03, decimal 12.0000 and -0.5000, architectural
        /// as <see cref="Architectural"/>; of the units of engineering and fractions, which the library does not write, as the
        /// invariant culture writes it.
        /// </summary>
        private static string UnitsText(double value, int units, int places)
        {
            switch (units)
            {
                case 1:
                    return value.ToString((places > 0 ? "0." + new string('0', places) : "0") + "E+00", CultureInfo.InvariantCulture);
                case 2:
                    return value.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                case 4:
                    return Architectural(value, Math.Min(places, 9), 0) ?? Numbers.Exact(value + 0.0);
                default:
                    return Numbers.Exact(value + 0.0);
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
        /// by name), its flag 0x10 set; <c>table.SetTextStyle(1, 0, db.TextStyleTable["GH_TXT"])</c> writes 7 GH_TXT. A cell that
        /// has a 7 already has it written in its place, as each override of a cell is, before its 92 where AutoCAD 2021 writes
        /// one, so that the cell holds one 7: a second after its value AutoCAD read, and the library the first. The block AutoCAD
        /// drew the table with is not drawn anew.
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
        /// Sets whether a cell's background is left unfilled, as AutoCAD's <c>SetBackgroundColorNone(row, col, value)</c>, as
        /// AutoCAD 2021 wrote it in a drawing of R2018 and of R2000 alike (D28e v18, v47, m1, w18): filled, the cell's own 283 of
        /// 0 with the flag 0x02 and a colour of its own (63, flag 0x04), BYBLOCK (<c>63 0</c>) where it has none; unfilled, its
        /// 283 and 63 and their flags taken out where its row's kind is unfilled by the table and its style, as AutoCAD read
        /// Qty given green and then none as of no background of its own, else 283 of 1 and <c>63 0</c>, as AutoCAD wrote the
        /// header cell Mark of a table whose header is green. The library wrote 283 of 1 for filled and 0 for unfilled, which
        /// AutoCAD read the other way about. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="value">Whether it is left unfilled.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetBackgroundColorNone(int row, int col, bool value)
        {
            CellView cell = CellToWrite(row, col);
            string fill = cell.Override(283, 0x02)?.Trim();
            string colour = cell.Override(63, 0x04)?.Trim();
            int flagsCode = Item.File.Supports(1021) ? 91 : 177;

            if (!value)
            {
                if (fill == "0" && colour != null)
                {
                    return;
                }

                CheckEditable();
                cell.SetOverride(283, "0", flagsCode);

                if (colour == null)
                {
                    cell = new CellView(cell.Groups, cell.From, cell.To);
                    cell.SetOverride(63, "0", flagsCode);
                }

                cell.Groups.Write(Order);
                return;
            }

            if (!KindFilled(row))
            {
                if (fill == null && colour == null)
                {
                    return;
                }

                CheckEditable();
                cell.RemoveOverrides(0x02 | 0x04);
                cell.Groups.Write(Order);
                return;
            }

            if (fill == "1" && colour == "0")
            {
                return;
            }

            CheckEditable();
            cell.SetOverride(283, "1", flagsCode);
            cell = new CellView(cell.Groups, cell.From, cell.To);
            cell.SetOverride(63, "0", flagsCode);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Whether the table fills the background of a row's kind: by its own look of the kind (283 after its flags in 93, 0
        /// filled), else by its table style's (<see cref="StyleLooks"/>).
        /// </summary>
        private bool KindFilled(int row)
        {
            DxfRowType type = RowType(row);
            int rowKind = type == DxfRowType.TitleRow ? 0 : type == DxfRowType.HeaderRow ? 1 : 2;
            string fill = OwnText(Read(), RowLooks[283] + rowKind) ?? StyleLooks()[rowKind][3];
            return fill.Trim() == "0";
        }

        /// <summary>
        /// Sets what a cell holds, as AutoCAD's <c>SetCellType</c>: a block cell made a text cell (171 of 1), its block, scale and
        /// attribute values (340, 144, 179, 331, 300) taken out and its value one of none (from R2007 on <c>93 7</c>, <c>90 0</c>,
        /// its flag 0x40000 taken out), as AutoCAD 2021 wrote the block cell M12 made a text cell (D28e v19), where the library
        /// wrote an empty text; before R2007 its text empty. A text cell is made a block cell with its
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
            text.SetNoValue();
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
        /// Gives a content of a cell the overrides named of its own over its table style, as AutoCAD's <c>SetOverrides(row,
        /// column, contentIndex, override)</c> does (D33 o1 to o14, o20 to o28, and D28e v42): its flags made those named, the
        /// alignment (flag 1) and background (2 and 4) of the cell kept where it holds them, as AutoCAD keeps them whatever is
        /// named. One named it does not hold is given the value AutoCAD gives it: the alignment its row shows, the header's
        /// middle centre (<c>170 5</c>) of AutoCAD's Standard; a background of none (<c>63 0</c>, <c>283 1</c>); the colour
        /// by block (<c>64 0</c>); the text height of its content's own format, 0.18 (<c>140 0.18</c>), not the 4.5 the cell
        /// showed; a text style of none, its flag alone; and its kind of value (<see cref="DxfCellProperties.DataType"/> or
        /// <see cref="DxfCellProperties.DataFormat"/>, the flag 0x40000), its flag alone. The colour, text style, height and
        /// kind of value not named are taken away, their groups and flags, so that the cell takes them from its table style;
        /// the others named, as its margins or its turn, write nothing. Qty of flags 262 144 given
        /// <see cref="DxfCellProperties.TextHeight"/> is written <c>91 32</c> and <c>140 0.18</c>; Mark of 170 5, GH_TXT and
        /// 3.5 given <see cref="DxfCellProperties.Invalid"/> keeps its alignment alone, <c>91 1</c>. The block AutoCAD drew the
        /// table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="overrides">The overrides the content has of its own.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetOverrides(int row, int column, int contentIndex, DxfCellProperties overrides)
        {
            CheckContent(contentIndex);
            CellView cell = CellToWrite(row, column);
            int now = cell.Flags ?? cell.Held();
            int named = FlagsOf(overrides) | ((overrides & (DxfCellProperties.DataType | DxfCellProperties.DataFormat)) != 0 ? DataTypeFlag : 0);
            int wanted = (now & 0x07) | named;
            int flagsCode = Item.File.Supports(1021) ? 91 : 177;

            if (wanted == now && cell.Flags.HasValue)
            {
                return;
            }

            CheckEditable();
            cell.RemoveOverrides(now & ~wanted & (0x08 | 0x10 | 0x20 | DataTypeFlag));
            cell = new CellView(cell.Groups, cell.From, cell.To);
            int added = wanted & ~now;

            if ((added & 0x01) != 0)
            {
                DxfRowType type = RowType(row);
                int rowKind = type == DxfRowType.TitleRow ? 0 : type == DxfRowType.HeaderRow ? 1 : 2;
                cell.SetOverride(170, OwnText(Read(), RowLooks[170] + rowKind) ?? StyleLooks()[rowKind][0], flagsCode);
            }

            if ((added & 0x06) != 0)
            {
                cell.SetOverride(63, "0", flagsCode);
                cell.SetOverride(283, "1", flagsCode);
            }

            if ((added & 0x08) != 0)
            {
                cell.SetOverride(64, "0", flagsCode);
            }

            if ((added & 0x20) != 0)
            {
                cell.SetOverride(140, "0.18", flagsCode);
            }

            cell.SetFlags(wanted, flagsCode);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Takes away the overrides of a cell's look named, their groups and their flags, so that it takes them from its table
        /// style: what <see cref="DxfCell"/> does with a look set to null. The others, its kind of value among them, are kept.
        /// </summary>
        internal void TakeOverrides(int row, int column, DxfCellProperties properties)
        {
            CellView cell = CellToWrite(row, column);
            int mask = FlagsOf(properties);

            if ((((cell.Flags ?? 0) | cell.Held()) & mask) == 0)
            {
                return;
            }

            CheckEditable();
            cell.RemoveOverrides(mask);
            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Takes away all a cell holds of its own over its table style, as AutoCAD's <c>RemoveAllOverrides</c>: its alignment,
        /// background, colours, text style and height, and the colours, lineweights and showing of its borders, their groups
        /// wherever they stand outside its value (AutoCAD 2021 writes them before its 92, the library after its value), their
        /// 18 flags and the edges of its edge flags (172), as AutoCAD 2021's own wrote the cell (1, 0) of 170 5, 7 GH_TXT and
        /// 140 3.5 before its 92 as 91 262144 and nothing else, and a cell of 172 9 as 172 0 (D28c x1 to x3); a flag the file
        /// gives no groups for, as that of its kind of value (0x40000), is kept. The block AutoCAD drew the table with is not
        /// drawn anew. The cell's groups alone are written, the rest of the record copied as it is: the overrides of every cell
        /// of a table of 403 rows taken away one by one in about 2 s, where writing the whole record each time took 6.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void RemoveAllOverrides(int row, int column)
        {
            Layout layout = Read();
            CellView cell = layout.CellOf(row, column);

            if ((((cell.Flags ?? 0) | cell.Held()) & CellOverrideFlags) == 0)
            {
                return;
            }

            CheckEditable();

            // The cell's own groups alone written anew in the place of their bytes, the rest of the record copied as it is: a
            // cell of a table of 400 rows costs a reading of its record, not a writing of every group of it.
            CellView alone = View(Slice(cell));
            alone.RemoveOverrides(CellOverrideFlags);
            var places = new List<int>();
            Item.File.OwnGroupsOf(Item, 0, places);
            int first = layout.Groups.Marked ? 1 : 0;
            Item.File.SpliceRecord(Item, 0, places[2 * (cell.From + first)], places[2 * (cell.To - 1 + first) + 1], alone.Groups.List, false);
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
        /// the DXF reference lists as its overrides (70, 40, 41, 280, 281, the looks by row and borders, 97, 98 and 4), after its
        /// last cell where the library writes them or in its head after its flags where AutoCAD 2021 writes them, and its flags
        /// 93 to 96 set to nought; a cell's are those <see cref="RemoveAllOverrides(int, int)"/> takes away. The block AutoCAD drew the table with is not drawn anew.
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

            // The table's own in its head after its flags, where AutoCAD 2021 writes them, taken out last: the cells after them
            // are read by the places of the layout until then.
            if (options != 2)
            {
                HeadOwn(g, layout.CellStart.Count > 0 ? layout.CellStart[0] : g.Sub.To, out int ownFrom, out int ownTo);

                for (int i = ownTo - 1; i >= ownFrom; i--)
                {
                    if (TableOverrides.Contains(g.List[i].Key))
                    {
                        g.RemoveAt(i);
                    }
                }
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
