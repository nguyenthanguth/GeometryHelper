using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Internal structure containing base geometric layout information to generate candidates.
    /// </summary>
    internal readonly struct Layout
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Layout"/> struct.
        /// </summary>
        /// <param name="anchor">The anchor point on the path segment.</param>
        /// <param name="direction">The longitudinal direction axis of the path.</param>
        /// <param name="perpendicular">The perpendicular direction axis of the path.</param>
        /// <param name="height">The actual height of the label.</param>
        /// <param name="positiveOffset">How far from the path the centre of the first row lies on the side the perpendicular points to.</param>
        /// <param name="negativeOffset">How far from the path the centre of the first row lies on the other side.</param>
        /// <param name="maximumShift">The maximum longitudinal shift distance.</param>
        internal Layout(GeoPoint2 anchor, GeoVector2 direction, GeoVector2 perpendicular,
            double height, double positiveOffset, double negativeOffset, double maximumShift)
        {
            Anchor = anchor;
            Direction = direction;
            Perpendicular = perpendicular;
            Height = height;
            PositiveOffset = positiveOffset;
            NegativeOffset = negativeOffset;
            MaximumShift = maximumShift;
        }

        /// <summary>Gets the anchor point on the path segment.</summary>
        internal GeoPoint2 Anchor { get; }
        /// <summary>Gets the longitudinal direction axis of the path.</summary>
        internal GeoVector2 Direction { get; }
        /// <summary>Gets the perpendicular direction axis of the path.</summary>
        internal GeoVector2 Perpendicular { get; }
        /// <summary>Gets the actual height of the label.</summary>
        internal double Height { get; }
        /// <summary>Gets how far from the path the centre of the first row lies on the side <see cref="Perpendicular"/> points to.</summary>
        internal double PositiveOffset { get; }
        /// <summary>Gets how far from the path the centre of the first row lies on the other side.</summary>
        internal double NegativeOffset { get; }
        /// <summary>Gets the maximum longitudinal shift distance.</summary>
        internal double MaximumShift { get; }

        /// <summary>
        /// Gets how far each slide along the leader steps: a twentieth of <see cref="MaximumShift"/>, or the height of
        /// the label where that comes to less than 0.1, so that a reach of next to nothing cannot step by next to nothing.
        /// </summary>
        internal double SlideStep
        {
            get
            {
                double step = MaximumShift / 20.0;
                return step < 0.1 ? Height : step;
            }
        }

        /// <summary>
        /// Gets how far from the leader the centre of a row lies: the first row of its side, then the height of the label
        /// and the gap between rows for each row beyond.
        /// </summary>
        /// <param name="positiveSide">True for the side <see cref="Perpendicular"/> points to, false for the other.</param>
        /// <param name="level">The row, counted from nought, the first.</param>
        /// <param name="rowGap">The gap between rows.</param>
        internal double GetRowOffset(bool positiveSide, int level, double rowGap)
            => (positiveSide ? PositiveOffset : NegativeOffset) + level * (Height + rowGap);

        /// <summary>
        /// Gets the way from <see cref="Anchor"/> straight across the leader to the centre of a row.
        /// </summary>
        /// <param name="positiveSide">True for the side <see cref="Perpendicular"/> points to, false for the other.</param>
        /// <param name="level">The row, counted from nought, the first.</param>
        /// <param name="rowGap">The gap between rows.</param>
        internal GeoVector2 GetRow(bool positiveSide, int level, double rowGap)
        {
            double offset = GetRowOffset(positiveSide, level, rowGap);
            return positiveSide ? Perpendicular * offset : Perpendicular * -offset;
        }
    }
}
