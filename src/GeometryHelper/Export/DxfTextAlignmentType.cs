namespace GeometryHelper.Export
{
    /// <summary>
    /// How the words of a MULTILEADER are lined up, as AutoCAD's <c>TextAlignmentType</c> numbers it: group 175 of the
    /// MULTILEADER.
    /// </summary>
    public enum DxfTextAlignmentType
    {
        /// <summary>On the left, 0.</summary>
        LeftAlignment = 0,

        /// <summary>In the middle, 1.</summary>
        CenterAlignment = 1,

        /// <summary>On the right, 2.</summary>
        RightAlignment = 2,
    }
}
