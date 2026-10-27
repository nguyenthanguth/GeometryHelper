namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Whether an annotative object keeps the way up of the sheet, as AutoCAD's <c>PaperOrientationStates</c> names and numbers
    /// it: a text that matches the orientation of its layout reads level on the sheet in a viewport whose view is turned. Read by
    /// <see cref="DxfDBObject.PaperOrientation"/>.
    /// </summary>
    public enum DxfPaperOrientationStates
    {
        /// <summary>The object keeps the way up of the sheet, 0.</summary>
        True = 0,

        /// <summary>The object can keep the way up of the sheet and does not, 1.</summary>
        False = 1,

        /// <summary>The object is of a kind that has no orientation on the sheet, as a line or a dimension, 2.</summary>
        NotApplicable = 2,
    }
}
