namespace GeometryHelper.Drawing
{
    /// <summary>
    /// How the views along the axes are laid out round the front view. Each view is the same picture either way, the
    /// object as seen from that side; only its place changes.
    /// </summary>
    public enum ProjectionMethod
    {
        /// <summary>
        /// Each view on the side it looks from: the top view above the front view, the right end view on its right, as
        /// American drawings have it. The default.
        /// </summary>
        ThirdAngle,

        /// <summary>
        /// Each view on the side away from where it looks from: the top view below the front view, the right end view on
        /// its left, as ISO drawings in Europe have it.
        /// </summary>
        FirstAngle
    }
}
