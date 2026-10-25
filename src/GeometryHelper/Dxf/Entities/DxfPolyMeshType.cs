namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The surface a <see cref="DxfPolygonMesh"/> is smoothed into, as AutoCAD's <c>PolyMeshType</c>: group 75 of its POLYLINE,
    /// read where flag 4 of its group 70 says it is smoothed.
    /// </summary>
    public enum DxfPolyMeshType
    {
        /// <summary>No smoothing: the grid of its vertices as it is.</summary>
        SimpleMesh = 0,

        /// <summary>A quadratic B-spline surface (5).</summary>
        QuadSurfaceMesh = 5,

        /// <summary>A cubic B-spline surface (6).</summary>
        CubicSurfaceMesh = 6,

        /// <summary>A Bezier surface (8).</summary>
        BezierSurfaceMesh = 8,
    }
}
