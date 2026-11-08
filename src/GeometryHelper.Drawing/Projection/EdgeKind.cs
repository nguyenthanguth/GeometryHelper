namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// Why a line of a view is drawn.
    /// </summary>
    public enum EdgeKind
    {
        /// <summary>
        /// An edge where two faces of a body meet at more than the smooth angle: the corner of a plate, the edge of a flange,
        /// the rim of a hole.
        /// </summary>
        Sharp = 0,

        /// <summary>
        /// An edge between two facets of one curved surface, drawn only where one facet is turned to the viewer and the other
        /// away: the two long lines of a tube seen across.
        /// </summary>
        Silhouette = 1,

        /// <summary>The outline of a face a section plane cuts out of a body.</summary>
        SectionOutline = 2,
    }
}
