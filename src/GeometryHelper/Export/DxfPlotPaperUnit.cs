namespace GeometryHelper.Export
{
    /// <summary>
    /// The units of a sheet's margins and size, as AutoCAD's <c>PlotPaperUnit</c> (group 72 of a layout's plot settings);
    /// the sizes in the file are millimetres whatever they are.
    /// </summary>
    public enum DxfPlotPaperUnit
    {
        /// <summary>Inches, 0.</summary>
        Inches = 0,

        /// <summary>Millimetres, 1.</summary>
        Millimeters = 1,

        /// <summary>Pixels, of a raster plot, 2.</summary>
        Pixels = 2,
    }
}
