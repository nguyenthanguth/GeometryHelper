namespace GeometryHelper.Export
{
    /// <summary>
    /// What a row of a table is, as AutoCAD's <c>RowType</c> numbers it: its title, its header or a row of data.
    /// </summary>
    public enum DxfRowType
    {
        /// <summary>Unknown, 0.</summary>
        UnknownRow = 0,

        /// <summary>A row of data, 1.</summary>
        DataRow = 1,

        /// <summary>The title, 2: the first row, where the table does not leave it out.</summary>
        TitleRow = 2,

        /// <summary>The header, 4: the row after the title, where the table does not leave it out.</summary>
        HeaderRow = 4,
    }
}
