using System;
using System.Globalization;
using GeometryHelper.Enums;

namespace GeometryHelper
{
    /// <summary>
    /// How an offset shapes the corners where its edges pull apart: the join, how far a sharp corner may
    /// reach, and how finely a round corner is drawn.
    /// <para>
    /// The options are immutable, so one instance can be shared between threads and kept as a setting.
    /// <see cref="Default"/> gives sharp corners, as AutoCAD's OFFSET does with its default gap type.
    /// </para>
    /// </summary>
    public sealed class OffsetOptions : IEquatable<OffsetOptions>
    {
        /// <summary>
        /// The default <see cref="MiterLimit"/>: a sharp corner may reach ten times the offset distance from
        /// its vertex, which keeps every corner of 11.5 degrees or wider sharp.
        /// </summary>
        public const double DefaultMiterLimit = 10.0;

        /// <summary>
        /// The share of the offset distance allowed as chord error on a round join when
        /// <see cref="ArcTolerance"/> is left automatic: 0.2 %, about 50 segments for a full turn.
        /// </summary>
        public const double AutomaticArcToleranceRatio = 0.002;

        /// <summary>
        /// Sharp corners (<see cref="OffsetJoin.Miter"/>) with the default miter limit.
        /// </summary>
        public static OffsetOptions Default { get; } = new OffsetOptions(OffsetJoin.Miter);

        /// <summary>
        /// Initializes offset options.
        /// </summary>
        /// <param name="join">How the gap at a corner is closed.</param>
        /// <param name="miterLimit">
        /// How far a <see cref="OffsetJoin.Miter"/> corner may reach from its vertex, as a multiple of the
        /// offset distance; beyond it the corner is cut off square at that distance. At least 1, and
        /// <see cref="double.PositiveInfinity"/> keeps every corner sharp. Ignored by the other joins.
        /// </param>
        /// <param name="arcTolerance">
        /// The largest gap allowed between a <see cref="OffsetJoin.Round"/> corner and the true arc, in
        /// drawing units. Zero (the default) picks <see cref="AutomaticArcToleranceRatio"/> of the offset
        /// distance. Ignored by the other joins.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the join is not a defined value, the miter limit is below 1 or not a number, or the
        /// arc tolerance is negative, infinite or not a number.
        /// </exception>
        public OffsetOptions(OffsetJoin join, double miterLimit = DefaultMiterLimit, double arcTolerance = 0.0)
        {
            if (join != OffsetJoin.Miter && join != OffsetJoin.Round && join != OffsetJoin.Chamfer)
            {
                throw new ArgumentOutOfRangeException(nameof(join), "Unknown offset join.");
            }

            // A miter point sits at least the offset distance away from its vertex, so a limit below 1 would
            // cut every corner inside the offset edges themselves.
            if (double.IsNaN(miterLimit) || miterLimit < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(miterLimit), "The miter limit must be at least 1.");
            }

            if (double.IsNaN(arcTolerance) || double.IsInfinity(arcTolerance) || arcTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(arcTolerance), "The arc tolerance must be zero (automatic) or a positive distance.");
            }

            Join = join;
            MiterLimit = miterLimit;
            ArcTolerance = arcTolerance;
        }

        /// <summary>
        /// Gets how the gap at a corner is closed.
        /// </summary>
        public OffsetJoin Join { get; }

        /// <summary>
        /// Gets how far a sharp corner may reach from its vertex, as a multiple of the offset distance.
        /// </summary>
        public double MiterLimit { get; }

        /// <summary>
        /// Gets the largest gap allowed between a round corner and the true arc, in drawing units; zero means
        /// automatic.
        /// </summary>
        public double ArcTolerance { get; }

        /// <summary>
        /// Gets the chord error a round join is drawn to for a given offset distance: <see cref="ArcTolerance"/>
        /// when one was set, otherwise <see cref="AutomaticArcToleranceRatio"/> of the distance.
        /// </summary>
        /// <param name="distance">The offset distance; its sign does not matter.</param>
        public double GetArcTolerance(double distance)
        {
            return ArcTolerance > 0.0 ? ArcTolerance : Math.Abs(distance) * AutomaticArcToleranceRatio;
        }

        /// <summary>
        /// Determines whether another set of options gives the same result.
        /// </summary>
        public bool Equals(OffsetOptions other)
        {
            return other != null &&
                   Join == other.Join &&
                   MiterLimit.Equals(other.MiterLimit) &&
                   ArcTolerance.Equals(other.ArcTolerance);
        }

        /// <summary>
        /// Determines whether the specified object is an equal set of options.
        /// </summary>
        public override bool Equals(object obj) => obj is OffsetOptions other && Equals(other);

        /// <summary>
        /// Returns the hash code for these options.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Join;
                hash = hash * 397 ^ MiterLimit.GetHashCode();
                hash = hash * 397 ^ ArcTolerance.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Describes the options.
        /// </summary>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Join: {0}, MiterLimit: {1}, ArcTolerance: {2})",
                Join,
                MiterLimit,
                ArcTolerance > 0.0 ? ArcTolerance.ToString(CultureInfo.InvariantCulture) : "automatic");
        }
    }
}
