namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the spacing of the lines of an MTEXT is taken, as AutoCAD's <c>LineSpacingStyle</c> numbers it: group 73 of the MTEXT,
    /// <see cref="AtLeast"/> where it gives none.
    /// </summary>
    public enum DxfLineSpacingStyle
    {
        /// <summary>At least the spacing asked for, wider where a line holds taller letters, 1.</summary>
        AtLeast = 1,

        /// <summary>Exactly the spacing asked for, 2.</summary>
        Exactly = 2,
    }
}
