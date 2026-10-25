namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the INSERTs of a block may scale it, as AutoCAD's <c>BlockScaling</c> numbers it, group 281 of its BLOCK_RECORD.
    /// </summary>
    public enum DxfBlockScaling
    {
        /// <summary>By any scale along each axis, 0: what a block that gives none takes.</summary>
        Any = 0,

        /// <summary>By the same scale along every axis, 1.</summary>
        Uniform = 1,
    }
}
