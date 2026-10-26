using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The cell a point hits, as AutoCAD's <c>TableHitTestInfo</c>: <see cref="DxfTable.HitTest"/> of the point (310, -105, 0)
    /// on the table of columns 40 and 30 wide and rows 10, 8 and 8 high at (300, -100, 0) gives the cell (0, 0).
    /// </summary>
    public readonly struct DxfTableHitTestInfo
    {
        internal DxfTableHitTestInfo(DxfTableHitTestType type, int row, int column)
        {
            Type = type;
            Row = row;
            Column = column;
        }

        /// <summary>Gets what the point hits, as AutoCAD's <c>Type</c>: a cell, or nothing.</summary>
        public DxfTableHitTestType Type { get; }

        /// <summary>Gets the row of the cell hit, from nought, as AutoCAD's <c>Row</c>; -1 where the point hits nothing.</summary>
        public int Row { get; }

        /// <summary>Gets the column of the cell hit, from nought, as AutoCAD's <c>Column</c>; -1 where the point hits nothing.</summary>
        public int Column { get; }

        /// <summary>Gives what was hit as a line for debugging: <c>Cell (0, 1)</c>.</summary>
        /// <returns>The line.</returns>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} ({1}, {2})", Type, Row, Column);
    }
}
