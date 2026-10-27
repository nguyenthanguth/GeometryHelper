namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the shadows of a sun are made, as AutoCAD's <c>ShadowType</c> of its shadow parameters numbers it (group 70 of a
    /// SUN).
    /// </summary>
    public enum DxfShadowType
    {
        /// <summary>Traced along the rays of light, sharp, 0.</summary>
        RayTraced = 0,

        /// <summary>Drawn from maps of shadows, soft, 1.</summary>
        Maps = 1,

        /// <summary>Sampled, as AutoCAD's renderer makes them, 2; not among the types the DXF reference gives a SUN.</summary>
        Sampled = 2,
    }
}
