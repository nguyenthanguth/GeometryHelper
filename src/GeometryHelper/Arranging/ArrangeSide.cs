namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Which side of its leader a label may stand on.
    /// </summary>
    /// <remarks>
    /// The sides are those of <see cref="ArrangeItem.OffsetTop"/> and <see cref="ArrangeItem.OffsetBottom"/>: which one
    /// faces up in the drawing does not depend on which way the leader runs, and a vertical leader has its top on the
    /// left.
    /// </remarks>
    public enum ArrangeSide
    {
        /// <summary>
        /// Either side: the rows of both are tried, the nearest first. The default.
        /// </summary>
        Both,

        /// <summary>
        /// The side of the leader that faces up in the drawing, towards greater Y; the left of a vertical leader.
        /// </summary>
        Top,

        /// <summary>
        /// The side of the leader that faces down in the drawing, towards smaller Y; the right of a vertical leader.
        /// </summary>
        Bottom,
    }
}
