namespace GeometryHelper.Meshing
{
    /// <summary>
    /// How a closed shape of the plane is broken into the faces of a <see cref="GeoMesh2"/>.
    /// </summary>
    /// <remarks>
    /// Whichever kind, every face is a simple polygon with no hole, running counter-clockwise, and lies within the
    /// shape's material; the faces cover the material without overlapping, holes left open, and meet edge to edge.
    /// </remarks>
    public enum MeshKind
    {
        /// <summary>
        /// Triangles on the shape's own corners, as <c>TriangulateSurface</c> gives them: as few as cover it, and no
        /// point added unless the shape's rings touch each other.
        /// </summary>
        Triangles,

        /// <summary>
        /// The cells of a regular grid, as panels of formwork or tiles are laid: whole where the material holds them,
        /// cut to the boundary and the holes where those cross them, and split through a hole one holds whole.
        /// </summary>
        Grid,

        /// <summary>
        /// Strips along one direction, cut at every corner: trapezoids whose parallel sides run that way, or triangles
        /// where a strip comes to a point.
        /// </summary>
        Strips,

        /// <summary>
        /// Convex pieces, few of them: the triangles merged across every edge whose two sides stay convex together,
        /// which leaves no more than four times as many pieces as the fewest there could be.
        /// </summary>
        Convex,
    }
}
