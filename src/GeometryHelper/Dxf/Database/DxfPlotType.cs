namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What part of a drawing is plotted, as AutoCAD's <c>PlotType</c> (group 74).
    /// </summary>
    public enum DxfPlotType
    {
        /// <summary>What is shown, 0.</summary>
        Display = 0,

        /// <summary>The extents, 1.</summary>
        Extents = 1,

        /// <summary>The limits, 2.</summary>
        Limits = 2,

        /// <summary>A named view (group 6), 3.</summary>
        View = 3,

        /// <summary>A window (groups 48, 49, 140, 141), 4.</summary>
        Window = 4,

        /// <summary>The layout, 5.</summary>
        Layout = 5,
    }
}
