namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Whether the frames of a drawing's images are shown, as AutoCAD's <c>FrameSetting</c> numbers it: group 70 of its
    /// RASTERVARIABLES, which AutoCAD 2021 writes as the number of the setting, 0 to 3.
    /// </summary>
    public enum DxfFrameSetting
    {
        /// <summary>A number the DXF reference does not list, -1.</summary>
        ImageFrameInvalid = -1,

        /// <summary>No frame shown, 0.</summary>
        ImageFrameOff = 0,

        /// <summary>The frame shown, 1, AutoCAD's own setting.</summary>
        ImageFrameAbove = 1,

        /// <summary>The setting AutoCAD names below, 2.</summary>
        ImageFrameBelow = 2,

        /// <summary>The frame shown and not plotted, 3.</summary>
        ImageFrameOnNoPlot = 3,
    }
}
