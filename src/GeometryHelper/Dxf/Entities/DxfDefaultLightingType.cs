namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The light a viewport is shaded by where the drawing has none of its own, as AutoCAD's <c>DefaultLightingType</c>
    /// (group 282).
    /// </summary>
    public enum DxfDefaultLightingType
    {
        /// <summary>One distant light, 0.</summary>
        OneDistantLight = 0,

        /// <summary>Two distant lights, 1.</summary>
        TwoDistantLights = 1,
    }
}
