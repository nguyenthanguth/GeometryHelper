namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How the leader lines of a MULTILEADER are drawn, as AutoCAD's <c>LeaderType</c> numbers it: group 170 of the MULTILEADER;
    /// <see cref="StraightLeader"/> where it gives none.
    /// </summary>
    public enum DxfLeaderType
    {
        /// <summary>Not drawn, 0.</summary>
        InVisibleLeader = 0,

        /// <summary>Straight from vertex to vertex, 1.</summary>
        StraightLeader = 1,

        /// <summary>A spline through the vertices, 2.</summary>
        SplineLeader = 2,
    }
}
