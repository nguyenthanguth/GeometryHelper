namespace GeometryHelper.Export
{
    /// <summary>
    /// Where the pattern of a HATCH comes from, as AutoCAD's <c>HatchPatternType</c> numbers it: group 76 of the HATCH;
    /// <see cref="PreDefined"/> where it gives none.
    /// </summary>
    public enum DxfHatchPatternType
    {
        /// <summary>Lines of one spacing at one angle, crossed where the pattern is double, made by the user, 0.</summary>
        UserDefined = 0,

        /// <summary>A pattern of AutoCAD's own pattern file, as ANSI31 or SOLID, 1.</summary>
        PreDefined = 1,

        /// <summary>A pattern of a pattern file of the user's, 2.</summary>
        CustomDefined = 2,
    }
}
