namespace GeometryHelper.Export
{
    /// <summary>
    /// What a <see cref="DxfPolylineVertex3d"/> is to its polyline, as AutoCAD's <c>Vertex3dType</c> reads it from the flags of
    /// its group 70: one of its own, one of the frame a spline was fitted to (16), or one the spline fitting made (8).
    /// </summary>
    public enum DxfVertex3dType
    {
        /// <summary>A vertex of the polyline as drawn.</summary>
        SimpleVertex = 0,

        /// <summary>A vertex of the frame a spline was fitted to (flag 16), not drawn.</summary>
        ControlVertex = 1,

        /// <summary>A vertex the spline fitting made (flag 8).</summary>
        FitVertex = 2,
    }
}
