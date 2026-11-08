namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What a point a view can be dimensioned to is on its object.
    /// </summary>
    public enum DimensionPointKind
    {
        /// <summary>
        /// A corner of the outline as the view draws it: the end of a straight line of the view that is a corner of the
        /// body. A plate 300 by 200 seen from above has four, (0, 0) to (300, 200).
        /// </summary>
        Corner = 0,

        /// <summary>
        /// The centre of a hole: a whole circle of the view drawn for a part, or a hole object's axis. A hole of 22 at 50
        /// from two edges of a plate seen from above has its centre at (50, 50).
        /// </summary>
        HoleCentre = 1,

        /// <summary>
        /// The middle of a bolt's axis, half way along the bolt: a point in the view seen along the bolt, a point on its
        /// centre line seen across it.
        /// </summary>
        BoltCentre = 2,

        /// <summary>
        /// An end of a part's reference line: the origin of its frame, and the point as far along its frame's X as the
        /// body reaches. A beam 3 000 long from the origin has its ends at 0 and 3 000.
        /// </summary>
        PartEnd = 3,

        /// <summary>An end of the reference line of the main part, which the ordinate rule measures from.</summary>
        WorkPoint = 4,
    }
}
