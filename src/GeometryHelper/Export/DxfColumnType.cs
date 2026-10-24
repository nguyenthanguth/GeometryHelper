namespace GeometryHelper.Export
{
    /// <summary>
    /// How an MTEXT is set in columns, as AutoCAD's <c>ColumnType</c> numbers it: group 71 of the MTEXT's embedded object from
    /// R2018, or 75 of its extended data of ACAD before; <see cref="NoColumns"/> where it gives none.
    /// </summary>
    public enum DxfColumnType
    {
        /// <summary>In one column, 0.</summary>
        NoColumns = 0,

        /// <summary>In columns of the same height, as many as set, 1.</summary>
        StaticColumns = 1,

        /// <summary>In columns that follow the words, 2.</summary>
        DynamicColumns = 2,
    }
}
