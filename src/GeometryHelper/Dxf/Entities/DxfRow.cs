namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A row of a table, as AutoCAD's <c>Row</c>: <c>table.Rows[0]</c>, its height read and set through its table, as
    /// <see cref="DxfTable.RowHeight(int)"/> and <see cref="DxfTable.SetRowHeight(int, double)"/> do; the title row of the table
    /// of rows 10, 8 and 8 high has <see cref="Height"/> 10.
    /// </summary>
    public sealed class DxfRow
    {
        private readonly DxfTable _table;
        private readonly int _row;

        internal DxfRow(DxfTable table, int row)
        {
            _table = table;
            _row = row;
        }

        /// <summary>
        /// Gets or sets the height of the row, as AutoCAD's <c>Height</c>: <see cref="DxfTable.RowHeight(int)"/> and
        /// <see cref="DxfTable.SetRowHeight(int, double)"/>; the block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the row is no longer one of the table's, or the height set is not a finite number above nought.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the table is erased.</exception>
        public double Height
        {
            get => _table.RowHeight(_row);
            set => _table.SetRowHeight(_row, value);
        }
    }
}
