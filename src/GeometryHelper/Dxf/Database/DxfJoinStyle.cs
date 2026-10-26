namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How two wide lines that meet are joined, as AutoCAD's <c>JoinStyle</c> numbers it (<c>$JOINSTYLE</c>, group 280).
    /// </summary>
    public enum DxfJoinStyle
    {
        /// <summary>None, 0.</summary>
        StyleNone = 0,

        /// <summary>Round, 1.</summary>
        StyleRound = 1,

        /// <summary>Angled, 2.</summary>
        StyleAngle = 2,

        /// <summary>Flat, 3.</summary>
        StyleFlat = 3,
    }
}
