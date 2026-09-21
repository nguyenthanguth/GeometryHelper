namespace GeometryHelper.Enums
{
    /// <summary>
    /// Names one end of a line segment: the end an extend, trim or lengthen operation moves.
    /// <para>
    /// A segment runs from its start point to its end point, so the two ends are told apart by that order
    /// rather than by position. The end that is not named stays where it is.
    /// </para>
    /// </summary>
    public enum LineEnd
    {
        /// <summary>
        /// The start point of the segment.
        /// </summary>
        Start,

        /// <summary>
        /// The end point of the segment.
        /// </summary>
        End
    }
}
