namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How finely the images of a drawing are shown on the screen, as AutoCAD's <c>ImageQuality</c> numbers it: group 71 of
    /// its RASTERVARIABLES.
    /// </summary>
    public enum DxfImageQuality
    {
        /// <summary>A number the DXF reference does not list, -1.</summary>
        Invalid = -1,

        /// <summary>Shown roughly, 0, to be drawn faster.</summary>
        Draft = 0,

        /// <summary>Shown finely, 1, AutoCAD's own.</summary>
        High = 1,
    }
}
