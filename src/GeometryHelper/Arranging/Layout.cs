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
        /// <param name="baseOffset">The base perpendicular offset from the path.</param>
        /// <param name="maximumShift">The maximum longitudinal shift distance.</param>
        internal Layout(GeoPoint2 anchor, GeoVector2 direction, GeoVector2 perpendicular,
            double height, double baseOffset, double maximumShift)
        {
            Anchor = anchor;
            Direction = direction;
            Perpendicular = perpendicular;
            Height = height;
            BaseOffset = baseOffset;
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
        /// <summary>Gets the base perpendicular offset from the path.</summary>
        internal double BaseOffset { get; }
        /// <summary>Gets the maximum longitudinal shift distance.</summary>
        internal double MaximumShift { get; }
    }
}
