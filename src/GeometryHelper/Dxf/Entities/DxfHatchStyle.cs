namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Which of the areas a HATCH's loops close it fills, as AutoCAD's <c>HatchStyle</c> numbers it: group 75 of the HATCH;
    /// <see cref="Normal"/> where it gives none.
    /// </summary>
    public enum DxfHatchStyle
    {
        /// <summary>Every other area from the outside in, an island filled within an island left empty, 0.</summary>
        Normal = 0,

        /// <summary>The outermost area alone, from the outer loops to the islands just inside them, 1.</summary>
        Outer = 1,

        /// <summary>All of the area within the outer loops, the islands ignored, 2.</summary>
        Ignore = 2,
    }
}
