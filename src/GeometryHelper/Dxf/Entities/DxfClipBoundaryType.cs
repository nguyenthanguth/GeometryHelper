namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The kind of boundary an IMAGE or a WIPEOUT is clipped by, as AutoCAD's <c>ClipBoundaryType</c> numbers it: group 71 of
    /// its record.
    /// </summary>
    public enum DxfClipBoundaryType
    {
        /// <summary>No boundary, 0, or a number the DXF reference does not list.</summary>
        Invalid = 0,

        /// <summary>A rectangle, 1, the record holding two of its corners.</summary>
        Rectangle = 1,

        /// <summary>A polygon, 2, the record holding its corners, the first again at the end.</summary>
        Poly = 2,
    }
}
