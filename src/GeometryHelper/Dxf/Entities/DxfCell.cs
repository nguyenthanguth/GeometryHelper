using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A cell of a table, as AutoCAD's <c>Cell</c>: <c>table.Cells[1, 0]</c>, or one of the cells a table walks. It reads and
    /// sets the cell through its table, as the table's members of a row and a column do: <c>cell.TextString = "Count"</c> is
    /// <see cref="DxfTable.SetTextString(int, int, string)"/>, and the header cell (1, 0) of flags 49 has
    /// <see cref="Alignment"/> <see cref="DxfCellAlignment.MiddleCenter"/> and <see cref="TextHeight"/> 3.5.
    /// </summary>
    /// <remarks>
    /// A cell's look set to null takes away what the cell holds of its own, as <see cref="DxfTable.SetOverrides"/> does, so
    /// that it takes it from its table style. Two cells are equal where they are of the same table, row and column.
    /// </remarks>
    public sealed class DxfCell : IEquatable<DxfCell>
    {
        internal DxfCell(DxfTable table, int row, int column)
        {
            Table = table;
            Row = row;
            Column = column;
        }

        /// <summary>Gets the row of the cell, from nought, as AutoCAD's <c>Row</c>.</summary>
        public int Row { get; }

        /// <summary>Gets the column of the cell, from nought, as AutoCAD's <c>Column</c>.</summary>
        public int Column { get; }

        /// <summary>Gets what the cell holds, as AutoCAD's <c>CellType</c>: <see cref="DxfTable.CellType(int, int)"/>.</summary>
        public DxfTableCellType CellType => Table.CellType(Row, Column);

        /// <summary>
        /// Gets or sets the text of the cell, as AutoCAD's <c>TextString</c>: <see cref="DxfTable.TextString(int, int)"/> and
        /// <see cref="DxfTable.SetTextString(int, int, string)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell set is a block cell, or the table is erased.</exception>
        public string TextString
        {
            get => Table.TextString(Row, Column);
            set => Table.SetTextString(Row, Column, value);
        }

        /// <summary>
        /// Gets or sets the value of the cell, as AutoCAD's <c>Value</c>: <see cref="DxfTable.Value(int, int)"/> and
        /// <see cref="DxfTable.SetValue(int, int, object)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number set is not finite or a whole number beyond 32 bits.</exception>
        /// <exception cref="ArgumentException">Thrown when the value set is of a kind not written.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell set is a block cell, or the table is erased.</exception>
        public object Value
        {
            get => Table.Value(Row, Column);
            set => Table.SetValue(Row, Column, value);
        }

        /// <summary>
        /// Gets or sets the format of the cell's value, as AutoCAD's <c>DataFormat</c>: <see cref="DxfTable.Format(int, int)"/>
        /// and <see cref="DxfTable.SetFormat(int, int, string)"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the format set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the format set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds no value of R2007, or the table is erased.</exception>
        public string DataFormat
        {
            get => Table.Format(Row, Column);
            set => Table.SetFormat(Row, Column, value);
        }

        /// <summary>
        /// Gets or sets the kind of the cell's value with what its number measures, as AutoCAD's <c>DataType</c>:
        /// <see cref="DxfTable.GetDataType(int, int, int)"/> and <see cref="DxfTable.SetDataType(int, int, DxfDataType, DxfUnitType)"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set holds a flag none of <see cref="DxfUnitType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the kind set is not one written, or the value is not of it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell holds no value of R2007, or the table is erased.</exception>
        public DxfDataTypeParameter DataType
        {
            get => Table.GetDataType(Row, Column, 0);
            set => Table.SetDataType(Row, Column, value.DataType, value.UnitType);
        }

        /// <summary>
        /// Gets or sets where the cell's content stands, as AutoCAD's <c>Alignment</c>: the cell's own, null where it has none;
        /// set, <see cref="DxfTable.SetAlignment(int, int, DxfCellAlignment)"/>, or null to take it away.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the alignment set is not one of <see cref="DxfCellAlignment"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public DxfCellAlignment? Alignment
        {
            get => Table.Alignment(Row, Column);

            set
            {
                if (value.HasValue)
                {
                    Table.SetAlignment(Row, Column, value.Value);
                }
                else
                {
                    TakeAway(DxfCellProperties.Alignment);
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the cell's text, as AutoCAD's <c>TextHeight</c>: the cell's own, null where it has none;
        /// set, <see cref="DxfTable.SetTextHeight(int, int, double)"/>, or null to take it away.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public double? TextHeight
        {
            get => Table.TextHeight(Row, Column);

            set
            {
                if (value.HasValue)
                {
                    Table.SetTextHeight(Row, Column, value.Value);
                }
                else
                {
                    TakeAway(DxfCellProperties.TextHeight);
                }
            }
        }

        /// <summary>
        /// Gets or sets the text style of the cell's text, as AutoCAD's <c>TextStyleId</c> gives its id: the cell's own, null
        /// where it has none; set, <see cref="DxfTable.SetTextStyle(int, int, DxfTextStyleTableRecord)"/>, or null to take it away.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the style set is erased, in no drawing, or of another drawing than the table's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public DxfTextStyleTableRecord TextStyle
        {
            get => Table.TextStyle(Row, Column);

            set
            {
                if (value != null)
                {
                    Table.SetTextStyle(Row, Column, value);
                }
                else
                {
                    TakeAway(DxfCellProperties.TextStyle);
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the cell's content, as AutoCAD's <c>ContentColor</c>: the cell's own, null where it has
        /// none; set, <see cref="DxfTable.SetContentColor(int, int, DxfColor)"/>, or null to take it away.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public DxfColor ContentColor
        {
            get => Table.ContentColor(Row, Column);

            set
            {
                if (value != null)
                {
                    Table.SetContentColor(Row, Column, value);
                }
                else
                {
                    TakeAway(DxfCellProperties.ContentColor);
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the cell's background, as AutoCAD's <c>BackgroundColor</c>: the cell's own, null where it
        /// has none; set, <see cref="DxfTable.SetBackgroundColor(int, int, DxfColor)"/>, or null to take its background away,
        /// its colour and whether it is filled.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public DxfColor BackgroundColor
        {
            get => Table.BackgroundColor(Row, Column);

            set
            {
                if (value != null)
                {
                    Table.SetBackgroundColor(Row, Column, value);
                }
                else
                {
                    TakeAway(DxfCellProperties.BackgroundColor);
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the cell's background is left unfilled, as AutoCAD's <c>IsBackgroundColorNone</c>: the cell's
        /// own, null where it has none; set, <see cref="DxfTable.SetBackgroundColorNone(int, int, bool)"/>, or null to take its
        /// background away, its colour and whether it is filled.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public bool? IsBackgroundColorNone
        {
            get => Table.IsBackgroundColorNone(Row, Column);

            set
            {
                if (value.HasValue)
                {
                    Table.SetBackgroundColorNone(Row, Column, value.Value);
                }
                else
                {
                    TakeAway(DxfCellProperties.BackgroundColor);
                }
            }
        }

        /// <summary>Gets the point the cell's content is placed at, as AutoCAD's <c>AttachmentPoint</c>: <see cref="DxfTable.AttachmentPoint(int, int)"/>.</summary>
        public GeoPoint3 AttachmentPoint => Table.AttachmentPoint(Row, Column);

        /// <summary>The table the cell is of.</summary>
        internal DxfTable Table { get; }

        /// <summary>Gets the corners of the cell, as AutoCAD's <c>GetExtents</c>: <see cref="DxfTable.GetCellExtents(int, int, bool)"/> of the cell alone.</summary>
        /// <returns>The four corners, the top left first.</returns>
        public IReadOnlyList<GeoPoint3> GetExtents() => Table.GetCellExtents(Row, Column, false);

        /// <summary>Gets the block of merged cells the cell is in, as AutoCAD's <c>GetMergeRange</c>: <see cref="DxfTable.GetMergeRange(int, int)"/>.</summary>
        /// <returns>The range.</returns>
        public DxfCellRange GetMergeRange() => Table.GetMergeRange(Row, Column);

        /// <summary>Empties the cell's value, as AutoCAD's <c>ResetValue</c>: <see cref="DxfTable.ResetValue(int, int)"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void ResetValue() => Table.ResetValue(Row, Column);

        /// <summary>Gets whether the cell is another, of the same table, row and column.</summary>
        /// <param name="other">The other cell.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DxfCell other) => other != null && ReferenceEquals(other.Table, Table) && other.Row == Row && other.Column == Column;

        /// <summary>Gets whether an object is a cell of the same table, row and column.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DxfCell other && Equals(other);

        /// <summary>Gets a hash of the row and the column.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked((Row * 397) ^ Column);

        /// <summary>Gives the cell as a line for debugging: <c>(1, 0)</c>.</summary>
        /// <returns>The cell as a line.</returns>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", Row, Column);

        private void TakeAway(DxfCellProperties property)
            => Table.SetOverrides(Row, Column, 0, Table.GetOverrides(Row, Column, 0) & ~property);
    }
}
