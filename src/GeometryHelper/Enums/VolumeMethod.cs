namespace GeometryHelper.Enums
{
    /// <summary>
    /// How the volume of a body is summed over its faces: each face broken into triangles, or read flat, and every
    /// triangle taken as a tetrahedron to the middle of the body's box, as the divergence theorem has it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Over flat faces every method gives the same volume, centroid and moments, but for the rounding: the divergence
    /// theorem is exact for them. They part only where a face is a hair out of flat, as the planar tolerance lets one be,
    /// and there no one surface is the face, so no one volume is the body's: a face of four corners with one of them
    /// lifted holds a third of the lift times its area more split along the diagonal through that corner, a sixth more
    /// split along the other, and a quarter more read flat. How far the methods part says how far the faces leave the
    /// volume open; <see cref="Core.Measure3.Compare(Geometry.GeoSolid3, Tolerance)"/> sets them side by side.
    /// </para>
    /// </remarks>
    public enum VolumeMethod
    {
        /// <summary>
        /// The fan of each face's boundary from its first corner, less the fans of its holes: what
        /// <see cref="Geometry.GeoSolid3.GetVolume(Tolerance)"/> sums, and the quickest. Whatever the face's shape, its fan sums to the
        /// face read flat through that corner, a third of its area vector against it, so over a face a hair out of flat
        /// the volume moves by a third of the face's area times how far its first corner stands off its middle plane.
        /// Tekla Structures reports the volume of a part read so.
        /// </summary>
        Fan,

        /// <summary>
        /// The triangles lying in each face, on its own corners, its holes left open: the volume the faces hold as the
        /// closed surface of triangles they mesh to, which over a face a hair out of flat is bent along the diagonals
        /// the triangulation chose. What <see cref="Geometry.GeoSolid3.GetMassProperties(double, Tolerance)"/> sums.
        /// </summary>
        Surface,

        /// <summary>
        /// Each face read flat: laid onto the plane square to its area that runs through the middle of its corners, as
        /// Newell's method fits a plane, which is the face's own plane where it is flat. It rests on no triangulation:
        /// over a face of four corners a hair out of flat, it is the middle of the two ways of splitting it.
        /// </summary>
        FlatFaces,
    }
}
