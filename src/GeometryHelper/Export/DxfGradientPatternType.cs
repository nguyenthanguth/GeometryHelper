namespace GeometryHelper.Export
{
    /// <summary>
    /// Where the gradient of a HATCH comes from, as AutoCAD's <c>GradientPatternType</c> numbers it: one of AutoCAD's nine
    /// named gradients, or one named otherwise.
    /// </summary>
    public enum DxfGradientPatternType
    {
        /// <summary>One of AutoCAD's gradients, LINEAR, CYLINDER, INVCYLINDER, SPHERICAL, INVSPHERICAL, HEMISPHERICAL, INVHEMISPHERICAL, CURVED or INVCURVED, 0.</summary>
        PreDefinedGradient = 0,

        /// <summary>A gradient of another name, 1.</summary>
        UserDefinedGradient = 1,
    }
}
