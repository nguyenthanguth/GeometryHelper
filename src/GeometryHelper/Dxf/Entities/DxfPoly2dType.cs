namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How a <see cref="DxfPolyline2d"/> is drawn through its vertices, as AutoCAD's <c>Poly2dType</c>: straight from vertex to
    /// vertex, or fitted with a curve (flag 2 of group 70) or a spline (flag 4, its kind in group 75).
    /// </summary>
    public enum DxfPoly2dType
    {
        /// <summary>Straight segments and the arcs of the bulges, as drawn from its vertices.</summary>
        SimplePoly = 0,

        /// <summary>A curve fitted through the vertices (flag 2 of group 70).</summary>
        FitCurvePoly = 1,

        /// <summary>A quadratic B-spline on the vertices (flag 4, group 75 of 5).</summary>
        QuadSplinePoly = 2,

        /// <summary>A cubic B-spline on the vertices (flag 4, group 75 of 6).</summary>
        CubicSplinePoly = 3,
    }
}
