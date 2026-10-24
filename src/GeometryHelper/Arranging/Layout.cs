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
        /// <param name="positiveOpen">Whether the label may stand on the side the perpendicular points to.</param>
        /// <param name="negativeOpen">Whether the label may stand on the other side.</param>
        internal Layout(GeoPoint2 anchor, GeoVector2 direction, GeoVector2 perpendicular,
            double height, double positiveOffset, double negativeOffset, double maximumShift, bool positiveOpen, bool negativeOpen)
        {
            Anchor = anchor;
            Direction = direction;
            Perpendicular = perpendicular;
            Height = height;
            PositiveOffset = positiveOffset;
            NegativeOffset = negativeOffset;
            MaximumShift = maximumShift;
            PositiveOpen = positiveOpen;
            NegativeOpen = negativeOpen;
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
        /// <summary>Gets whether the label may stand on the side <see cref="Perpendicular"/> points to.</summary>
        internal bool PositiveOpen { get; }
        /// <summary>Gets whether the label may stand on the other side.</summary>
        internal bool NegativeOpen { get; }

        /// <summary>
        /// Gets how far each slide along the leader steps: a twentieth of <see cref="MaximumShift"/>, or the height of
        /// the label where that comes to less than 0.1, so that a reach of next to nothing cannot step by next to nothing.
        /// A reach within the point tolerance of 2 counts as 2, so a reach of 2 that rounding takes a hair under it, where
        /// the scene lies far from the origin, steps by a twentieth as it does at the origin.
        /// </summary>
        /// <param name="tolerance">The tolerance of the options.</param>
        internal double GetSlideStep(Tolerance tolerance)
            => StepsByHeight(tolerance) ? Height : MaximumShift / 20.0;

        /// <summary>
        /// Gets how many steps of <see cref="GetSlideStep(Tolerance)"/> a row slides each way: twenty where the step is a
        /// twentieth of <see cref="MaximumShift"/>, the last reaching it, and otherwise as many as reach no further than it,
        /// within the point tolerance. Counted rather than stepped off one by one, so that the last slide does not come or
        /// go with the rounding of where the scene lies: a leader 60 long whose label slides 30 each way does so 20 times,
        /// at the origin as a million off it.
        /// </summary>
        /// <param name="tolerance">The tolerance of the options.</param>
        internal long GetSlideCount(Tolerance tolerance)
        {
            if (!StepsByHeight(tolerance))
            {
                return 20;
            }

            double count = System.Math.Floor((MaximumShift + tolerance.EqualPoint) / Height);

            // A label of no height steps by nothing, and goes on until the cap on candidates stops it.
            return !(count < long.MaxValue) ? long.MaxValue : count < 0.0 ? 0 : (long)count;
        }

        /// <summary>
        /// Whether a twentieth of the reach, within the point tolerance, comes to less than 0.1.
        /// </summary>
        private bool StepsByHeight(Tolerance tolerance) => (MaximumShift + tolerance.EqualPoint) / 20.0 < 0.1;

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
