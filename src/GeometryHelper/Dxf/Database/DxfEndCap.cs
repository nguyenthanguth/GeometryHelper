namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the ends of a wide line are drawn, as AutoCAD's <c>EndCap</c> numbers them (<c>$ENDCAPS</c>, group 280).
    /// </summary>
    public enum DxfEndCap
    {
        /// <summary>None, 0: the end cut square at the end point.</summary>
        None = 0,

        /// <summary>Round, 1.</summary>
        Round = 1,

        /// <summary>Angled, 2.</summary>
        Angle = 2,

        /// <summary>Square, 3: the end carried half the width past the end point.</summary>
        Square = 3,
    }
}
