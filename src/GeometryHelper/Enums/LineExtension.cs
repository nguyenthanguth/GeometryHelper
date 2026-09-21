namespace GeometryHelper.Enums
{
    /// <summary>
    /// Says which of two line segments an intersection may reach past, by reading it as the infinite line
    /// carrying it. This is what AutoCAD's <c>Intersect</c> option does for <c>IntersectWith</c>.
    /// <para>
    /// A segment that is not extended must contain the intersection itself, within tolerance. An extended one
    /// only has to point at it, however far away.
    /// </para>
    /// </summary>
    public enum LineExtension
    {
        /// <summary>
        /// Neither segment is extended: the intersection must lie on both.
        /// </summary>
        None,

        /// <summary>
        /// The first segment (the one the method is called on) is read as an infinite line; the second must
        /// contain the intersection.
        /// </summary>
        First,

        /// <summary>
        /// The second segment (the argument) is read as an infinite line; the first must contain the
        /// intersection.
        /// </summary>
        Second,

        /// <summary>
        /// Both segments are read as infinite lines.
        /// </summary>
        Both
    }
}
