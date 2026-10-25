namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Where the leaders of a MULTILEADER meet its block, as AutoCAD's <c>BlockConnectionType</c> numbers it: group 176 of the
    /// MULTILEADER; <see cref="ConnectExtents"/> where it gives none.
    /// </summary>
    public enum DxfBlockConnectionType
    {
        /// <summary>At the edge of the box the block fills, 0.</summary>
        ConnectExtents = 0,

        /// <summary>At the point the block is placed by, 1.</summary>
        ConnectBase = 1,
    }
}
