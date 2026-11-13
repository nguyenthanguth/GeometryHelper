namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which line of a multiline stands on the points it is drawn through, as AutoCAD's <c>MlineJustification</c>: group 70 of
    /// an MLINE, and the value of <c>$CMLJUST</c> new multilines take.
    /// </summary>
    /// <remarks>
    /// A multiline of the style STANDARD, whose two elements stand 0.5 either side of the line drawn, at scale 20 through
    /// (0, 0) and (100, 0), has its elements 0 and -20 off the line at <see cref="Top"/> (the left one on the points, the
    /// other 20 to their right), 10 and -10 at <see cref="Zero"/> and 20 and 0 at <see cref="Bottom"/>.
    /// </remarks>
    public enum DxfMlineJustification
    {
        /// <summary>The element of the greatest offset, the left one of STANDARD, stands on the points (0).</summary>
        Top = 0,

        /// <summary>The line of offset nought, the middle of STANDARD, stands on the points (1).</summary>
        Zero = 1,

        /// <summary>The element of the least offset, the right one of STANDARD, stands on the points (2).</summary>
        Bottom = 2,
    }
}
