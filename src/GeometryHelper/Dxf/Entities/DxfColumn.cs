namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A column of a table, as AutoCAD's <c>Column</c>: <c>table.Columns[1]</c>, its width read and set through its table, as
    /// <see cref="DxfTable.ColumnWidth(int)"/> and <see cref="DxfTable.SetColumnWidth(int, double)"/> do; the second column of the
    /// table of columns 40 and 30 wide has <see cref="Width"/> 30.
    /// </summary>
    public sealed class DxfColumn
    {
        private readonly DxfTable _table;
        private readonly int _column;

        internal DxfColumn(DxfTable table, int column)
        {
            _table = table;
            _column = column;
        }

        /// <summary>
        /// Gets or sets the width of the column, as AutoCAD's <c>Width</c>: <see cref="DxfTable.ColumnWidth(int)"/> and
        /// <see cref="DxfTable.SetColumnWidth(int, double)"/>; the block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the column is no longer one of the table's, or the width set is not a finite number above nought.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the table is erased.</exception>
        public double Width
        {
            get => _table.ColumnWidth(_column);
            set => _table.SetColumnWidth(_column, value);
        }
    }
}
