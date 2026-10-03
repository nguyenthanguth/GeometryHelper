namespace GeometryHelper.Enums
{
    /// <summary>
    /// How the area of a body's surface is summed over its faces.
    /// </summary>
    /// <remarks>
    /// Over flat faces both methods give the same area, but for the rounding. Over a face a hair out of flat the
    /// triangles lying in it, bent as they are, cover a little more than its outline does read flat.
    /// </remarks>
    public enum AreaMethod
    {
        /// <summary>
        /// Each face's area as its outline gives it, less its holes': the length of its area vector, the area of the face
        /// read flat. What <see cref="Geometry.GeoSolid3.GetSurfaceArea(Tolerance)"/> and <see cref="Geometry.GeoFace3.Area"/> give.
        /// </summary>
        Faces,

        /// <summary>
        /// The triangles lying in each face, on its own corners, its holes left open, each by its own area: the area of
        /// the surface the faces mesh to.
        /// </summary>
        Surface,
    }
}
