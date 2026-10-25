namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a plot is turned on its sheet, as AutoCAD's <c>PlotRotation</c> (group 73).
    /// </summary>
    public enum DxfPlotRotation
    {
        /// <summary>Not turned, 0.</summary>
        Degrees000 = 0,

        /// <summary>A quarter turn, 1.</summary>
        Degrees090 = 1,

        /// <summary>A half turn, 2.</summary>
        Degrees180 = 2,

        /// <summary>Three quarters of a turn, 3.</summary>
        Degrees270 = 3,
    }
}
