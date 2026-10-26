namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What a point hits of a table, as AutoCAD's <c>TableHitTestType</c> numbers it: a cell, or nothing.
    /// </summary>
    public enum DxfTableHitTestType
    {
        /// <summary>Nothing of the table, 0.</summary>
        None = 0,

        /// <summary>A cell, 1.</summary>
        Cell = 1,
    }
}
