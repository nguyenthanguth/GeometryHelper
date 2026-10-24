namespace GeometryHelper.Export
{
    /// <summary>
    /// What a <see cref="DxfVertex2d"/> is to its polyline, as AutoCAD's <c>Vertex2dType</c> reads it from the flags of its
    /// group 70: one of its own, one of the frame a spline was fitted to (16), one the spline fitting made (8), or one the
    /// curve fitting made (1).
    /// </summary>
    public enum DxfVertex2dType
    {
        /// <summary>A vertex of the polyline as drawn.</summary>
        SimpleVertex = 0,

        /// <summary>A vertex of the frame a spline was fitted to (flag 16), not drawn.</summary>
        SplineControlVertex = 1,

        /// <summary>A vertex the spline fitting made (flag 8).</summary>
        SplineFitVertex = 2,

        /// <summary>A vertex the curve fitting made (flag 1).</summary>
        CurveFitVertex = 3,
    }
}
