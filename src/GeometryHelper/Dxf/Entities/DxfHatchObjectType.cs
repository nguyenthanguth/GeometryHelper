namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Whether a HATCH is filled with a pattern or a gradient, as AutoCAD's <c>HatchObjectType</c> numbers it: group 450 of the
    /// HATCH, 1 for a gradient; <see cref="HatchObject"/> where it gives none.
    /// </summary>
    public enum DxfHatchObjectType
    {
        /// <summary>A pattern, solid fill among them, 0.</summary>
        HatchObject = 0,

        /// <summary>A gradient of colours, 1.</summary>
        GradientObject = 1,
    }
}
