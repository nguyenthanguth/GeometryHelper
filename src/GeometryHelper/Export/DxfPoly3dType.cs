namespace GeometryHelper.Export
{
    /// <summary>
    /// How a <see cref="DxfPolyline3d"/> is drawn through its vertices, as AutoCAD's <c>Poly3dType</c>: straight from vertex to
    /// vertex, or as a spline on them (flag 4 of group 70, its kind in group 75).
    /// </summary>
    public enum DxfPoly3dType
    {
        /// <summary>Straight segments from vertex to vertex.</summary>
        SimplePoly = 0,

        /// <summary>A quadratic B-spline on the vertices (group 75 of 5).</summary>
        QuadSplinePoly = 1,

        /// <summary>A cubic B-spline on the vertices (group 75 of 6).</summary>
        CubicSplinePoly = 2,
    }
}
