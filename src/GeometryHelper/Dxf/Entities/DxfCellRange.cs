using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A block of cells of a table, as AutoCAD's <c>CellRange</c>: from its top row and left column to its bottom row and right
    /// column, all counted from nought and taken in. The title of a table of two columns, merged across them, is the range
    /// (0, 0) to (0, 1).
    /// </summary>
    public sealed class DxfCellRange : IEquatable<DxfCellRange>
    {
        /// <summary>Initializes a range, as AutoCAD's <c>CellRange.Create(table, topRow, leftColumn, bottomRow, rightColumn)</c>.</summary>
        /// <param name="topRow">The top row, from nought.</param>
        /// <param name="leftColumn">The left column, from nought.</param>
        /// <param name="bottomRow">The bottom row, the top row or below it.</param>
        /// <param name="rightColumn">The right column, the left column or right of it.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a row or column is below nought, or the bottom row is above the top, or the right column left of the left.</exception>
        public DxfCellRange(int topRow, int leftColumn, int bottomRow, int rightColumn)
        {
            if (topRow < 0 || leftColumn < 0 || bottomRow < topRow || rightColumn < leftColumn)
            {
                throw new ArgumentOutOfRangeException(nameof(topRow), "A range runs from its top row and left column, from nought, to its bottom row and right column.");
            }

            TopRow = topRow;
            LeftColumn = leftColumn;
            BottomRow = bottomRow;
            RightColumn = rightColumn;
        }

        /// <summary>A range of the cells of a table, as <see cref="DxfTable.Cells"/> gives it.</summary>
        internal DxfCellRange(DxfTable table, int topRow, int leftColumn, int bottomRow, int rightColumn)
            : this(topRow, leftColumn, bottomRow, rightColumn)
        {
            Table = table;
        }

        /// <summary>Gets the top row, as AutoCAD's <c>TopRow</c>.</summary>
        public int TopRow { get; }

        /// <summary>Gets the left column, as AutoCAD's <c>LeftColumn</c>.</summary>
        public int LeftColumn { get; }

        /// <summary>Gets the bottom row, as AutoCAD's <c>BottomRow</c>.</summary>
        public int BottomRow { get; }

        /// <summary>Gets the right column, as AutoCAD's <c>RightColumn</c>.</summary>
        public int RightColumn { get; }

        /// <summary>
        /// Gets a cell of the range, as AutoCAD's <c>CellRange</c> indexer: <c>table.Cells[1, 0]</c> is the cell of the second
        /// row and the first column, its row and column counted in the table, from nought.
        /// </summary>
        /// <param name="row">The row, from nought, one of the range's.</param>
        /// <param name="column">The column, from nought, one of the range's.</param>
        /// <returns>The cell.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the range is one made in code, of no table.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not in the range.</exception>
        public DxfCell this[int row, int column]
        {
            get
            {
                if (Table == null)
                {
                    throw new InvalidOperationException("The range " + ToString() + " is of no table: take a cell from a table's Cells.");
                }

                if (row < TopRow || row > BottomRow || column < LeftColumn || column > RightColumn)
                {
                    throw new ArgumentOutOfRangeException(row < TopRow || row > BottomRow ? nameof(row) : nameof(column), "The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + column.ToString(CultureInfo.InvariantCulture) + ") is not in the range " + ToString() + ".");
                }

                return new DxfCell(Table, row, column);
            }
        }

        /// <summary>The table the range is of; null for one made in code.</summary>
        internal DxfTable Table { get; }

        /// <summary>Gets whether the range is of the same rows and columns as another.</summary>
        /// <param name="other">The other range.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DxfCellRange other)
            => other != null && other.TopRow == TopRow && other.LeftColumn == LeftColumn && other.BottomRow == BottomRow && other.RightColumn == RightColumn;

        /// <summary>Gets whether an object is a range of the same rows and columns.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => obj is DxfCellRange other && Equals(other);

        /// <summary>Gets a hash of the rows and columns.</summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => unchecked((((TopRow * 397) ^ LeftColumn) * 397 ^ BottomRow) * 397 ^ RightColumn);

        /// <summary>Gives the range as a line for debugging: <c>(0, 0)-(0, 1)</c>.</summary>
        /// <returns>The range as a line.</returns>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "({0}, {1})-({2}, {3})", TopRow, LeftColumn, BottomRow, RightColumn);
    }
}
