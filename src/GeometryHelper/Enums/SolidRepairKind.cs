namespace GeometryHelper.Enums
{
    /// <summary>
    /// What <see cref="Geometry.GeoSolid3.TryClose(out Geometry.GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>
    /// changed of a body to close it, one change at a time.
    /// </summary>
    /// <remarks>
    /// They are made in this order, each only where the ones before did not close the body, and come in that order in
    /// <see cref="SolidClosing3.Repairs"/>. What <see cref="SolidRepair3.Size"/> measures is the kind's.
    /// </remarks>
    public enum SolidRepairKind
    {
        /// <summary>
        /// A face dropped: one with no area, or one of two lying back to back on the same corners, a sheet inside the body
        /// that encloses nothing. The size is the area dropped.
        /// </summary>
        Drop,

        /// <summary>
        /// A face turned over, so that the body is wound alike and outwards. The size is the area turned.
        /// </summary>
        Flip,

        /// <summary>
        /// Corners standing apart across a gap made one point. The size is the furthest a corner of the group moved.
        /// </summary>
        Weld,

        /// <summary>
        /// A corner standing on an edge of another face, between its ends, put on that edge, which is split there: a long
        /// edge beside two short ones. The size is how far the corner stood off the edge.
        /// </summary>
        SplitEdge,

        /// <summary>
        /// The two sides of a crack, chains of open edges running side by side, joined by faces between them. The size is
        /// the widest the crack was.
        /// </summary>
        Stitch,

        /// <summary>
        /// A hole filled, by one face where it is flat and by triangles where it is not. The size is the area of the faces
        /// added.
        /// </summary>
        Fill,
    }
}
