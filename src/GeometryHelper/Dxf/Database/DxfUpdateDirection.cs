namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which way a data link was last updated, as AutoCAD's <c>UpdateDirection</c> numbers it: read from group 92 of a DATALINK,
    /// which LibreDWG names no further, so the reading is the library's.
    /// </summary>
    public enum DxfUpdateDirection
    {
        /// <summary>From the source, the spreadsheet, into the drawing's data, 1.</summary>
        SourceToData = 1,

        /// <summary>From the drawing's data back into the source, 2.</summary>
        DataToSource = 2,
    }
}
