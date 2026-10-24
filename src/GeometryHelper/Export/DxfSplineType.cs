namespace GeometryHelper.Export
{
    /// <summary>
    /// How a <see cref="DxfSpline"/> is given, as AutoCAD's <c>SplineType</c>: by the points it passes through, or by its control
    /// points.
    /// </summary>
    public enum DxfSplineType
    {
        /// <summary>By its fit points (groups 11, 21, 31), which the file gives.</summary>
        FitPoints = 0,

        /// <summary>By its control points, knots and weights alone.</summary>
        ControlPoints = 1,
    }
}
