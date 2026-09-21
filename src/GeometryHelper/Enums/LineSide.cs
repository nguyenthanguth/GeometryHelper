namespace GeometryHelper.Enums
{
    /// <summary>
    /// Which side of a directed segment a point lies on, seen looking along it from its start to its end.
    /// <para>
    /// This is the counterpart in the plane of <see cref="PlaneSide"/> in space. A segment is read as the
    /// infinite line carrying it, so a point beyond either end still has a side; what decides the answer is
    /// only which way the point lies off the line, never how far along it sits.
    /// </para>
    /// </summary>
    public enum LineSide
    {
        /// <summary>
        /// The point lies to the left of the direction of the segment: the side its normal turned a quarter
        /// turn counter-clockwise points to.
        /// </summary>
        Left,

        /// <summary>
        /// The point lies to the right of the direction of the segment.
        /// </summary>
        Right,

        /// <summary>
        /// The point lies on the line carrying the segment, within tolerance.
        /// </summary>
        On
    }
}
