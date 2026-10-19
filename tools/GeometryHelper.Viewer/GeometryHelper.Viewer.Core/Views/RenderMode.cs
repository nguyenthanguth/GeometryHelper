namespace GeometryHelper.Viewer
{
    /// <summary>
    /// How a view draws the scene.
    /// </summary>
    public enum RenderMode
    {
        /// <summary>
        /// Lines only.
        /// </summary>
        Wireframe,

        /// <summary>
        /// Surfaces only; shapes with no surface keep their lines.
        /// </summary>
        Shaded,

        /// <summary>
        /// Surfaces and their edges.
        /// </summary>
        ShadedEdges,

        /// <summary>
        /// Surfaces see-through, with their edges.
        /// </summary>
        Transparent,
    }
}
