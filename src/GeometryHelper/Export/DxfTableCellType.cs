namespace GeometryHelper.Export
{
    /// <summary>
    /// What a cell of a table holds, as AutoCAD's <c>TableCellType</c> numbers it: group 171 of the cell of an ACAD_TABLE.
    /// </summary>
    public enum DxfTableCellType
    {
        /// <summary>Unknown, 0.</summary>
        UnknownCell = 0,

        /// <summary>A text, 1.</summary>
        TextCell = 1,

        /// <summary>A block, 2.</summary>
        BlockCell = 2,

        /// <summary>Several contents, 3.</summary>
        MultipleContentCell = 3,
    }
}
