namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which arrowhead of a dimension is meant, as AutoCAD's <c>DimArrowFlag</c> numbers them: the one at the first extension
    /// line or the one at the second.
    /// </summary>
    public enum DxfDimArrowFlag
    {
        /// <summary>The arrowhead at the first extension line, 0.</summary>
        FirstArrow = 0,

        /// <summary>The arrowhead at the second extension line, 1.</summary>
        SecondArrow = 1,
    }
}
