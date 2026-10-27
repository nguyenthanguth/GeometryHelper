namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Whether an object is annotative, as AutoCAD's <c>AnnotativeStates</c> names and numbers it: drawn, in AutoCAD, at the
    /// size its scale gives it on the sheet. Read and set by <see cref="DxfDBObject.Annotative"/>.
    /// </summary>
    public enum DxfAnnotativeStates
    {
        /// <summary>The object is annotative, 0.</summary>
        True = 0,

        /// <summary>The object can be annotative and is not, 1.</summary>
        False = 1,

        /// <summary>The object is of a kind that is never annotative, as a line or a circle, 2.</summary>
        NotApplicable = 2,
    }
}
