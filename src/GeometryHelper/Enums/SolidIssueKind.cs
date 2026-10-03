namespace GeometryHelper.Enums
{
    /// <summary>
    /// What <see cref="Geometry.GeoSolid3.Validate(Tolerance)"/> found wrong with the faces of a body, or worth knowing
    /// about them.
    /// </summary>
    /// <remarks>
    /// The first four leave a body no boolean or measure can read as the material it is meant to be, and make it not
    /// valid; the rest are noted. They come in this order in <see cref="SolidValidation3.Issues"/>.
    /// </remarks>
    public enum SolidIssueKind
    {
        /// <summary>
        /// A stretch of edge an odd number of faces meet on: one, the rim of a gap, or three, a fin standing off the
        /// surface. The body is not closed.
        /// </summary>
        OpenEdge,

        /// <summary>
        /// A stretch of edge an even number of faces meet on, not as many running it one way as the other: a face wound
        /// the wrong way round beside its neighbours. <see cref="Geometry.GeoSolid3.IsClosed(Tolerance)"/> counts the faces
        /// on an edge and not which way they run it, and calls such a body closed.
        /// </summary>
        SameWayEdge,

        /// <summary>
        /// Faces enclosing less than nothing: the body is wound inwards, as <see cref="Geometry.GeoSolid3.TurnOutwards"/>
        /// sets right.
        /// </summary>
        InsideOut,

        /// <summary>
        /// Faces enclosing no more volume than the point tolerance times their area: a sheet, or faces lying back to back,
        /// thinner than the tolerance everywhere.
        /// </summary>
        NoVolume,

        /// <summary>
        /// A stretch of edge four or more faces meet on, as many running it each way: two blocks meeting along an edge.
        /// The body is closed; noted.
        /// </summary>
        NonManifoldEdge,

        /// <summary>
        /// An edge of a face no longer than the point tolerance: two corners that are one point. Noted.
        /// </summary>
        ShortEdge,

        /// <summary>
        /// A face no wider on average than the point tolerance, its area no more than half the tolerance times its
        /// perimeter: within the tolerance it covers nothing. Noted.
        /// </summary>
        SliverFace,

        /// <summary>
        /// A face with a corner further off its plane than the planar tolerance, measured as the booleans measure it, from
        /// the plane of its first corner; the booleans break such a face into triangles on its own corners before they
        /// cut it. Noted.
        /// </summary>
        NotFlatFace,
    }
}
