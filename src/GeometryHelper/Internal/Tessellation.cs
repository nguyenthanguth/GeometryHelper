using System;

namespace GeometryHelper.Internal
{
    /// <summary>
    /// How many straight pieces a round curve is cut into, and nothing else: the shapes themselves place
    /// the points, each in its own dimension.
    /// <para>
    /// A curve can be cut three ways, and each answers a different question. By chord tolerance: how far
    /// the straight pieces may stray from the true curve, which is what drawing and exporting care about.
    /// By spacing: how far apart the points may be along the curve, which is what setting out and dividing
    /// care about. By count: exactly how many pieces, which is what a caller who has already decided asks
    /// for.
    /// </para>
    /// </summary>
    internal static class Tessellation
    {
        /// <summary>
        /// The chord tolerance used when none is given: the same share of the radius that an offset uses
        /// for its round corners, which is about fifty pieces for a full turn.
        /// </summary>
        public const double AutomaticChordRatio = OffsetOptions.AutomaticArcToleranceRatio;

        /// <summary>
        /// The most pieces a full turn is ever cut into, so that a tolerance of nearly nothing asks for a
        /// long loop rather than an endless one.
        /// </summary>
        public const int MaxSegmentsPerTurn = 4096;

        /// <summary>
        /// Gets how many pieces a round curve is cut into so that no piece strays further than a chord
        /// tolerance from the true curve.
        /// </summary>
        /// <param name="radius">The radius of the curve.</param>
        /// <param name="sweptAngle">How far the curve turns, in radians; a full circle is two pi.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the curve, in drawing units. Zero picks <see cref="AutomaticChordRatio"/> of the radius.</param>
        /// <returns>The number of pieces, at least one.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number, the swept angle is not a positive number, or the tolerance is negative or not a number.</exception>
        public static int SegmentsForChordTolerance(double radius, double sweptAngle, double chordTolerance)
        {
            RequirePositive(radius, nameof(radius));
            RequirePositive(sweptAngle, nameof(sweptAngle));

            if (double.IsNaN(chordTolerance) || chordTolerance < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chordTolerance), "The chord tolerance must be zero (automatic) or a positive distance.");
            }

            double error = chordTolerance > 0.0 ? chordTolerance : radius * AutomaticChordRatio;

            // A piece spanning an angle a strays r(1 - cos(a/2)) from the curve at its middle, so the angle
            // a tolerance allows is twice the arc cosine of what is left of the radius.
            double cosine = 1.0 - error / radius;
            double perSegment = cosine <= -1.0 ? Math.PI * 2.0 : 2.0 * Math.Acos(cosine);

            return Clamp(sweptAngle, perSegment);
        }

        /// <summary>
        /// Gets how many pieces a curve of a given length is cut into so that no two points are further
        /// apart along it than a spacing.
        /// </summary>
        /// <param name="length">The length of the curve.</param>
        /// <param name="sweptAngle">How far the curve turns, in radians, which caps how fine the cut may get.</param>
        /// <param name="spacing">The largest distance allowed between two points, measured along the curve.</param>
        /// <returns>The number of pieces, at least one.</returns>
        /// <remarks>
        /// The length rarely divides by the spacing exactly. The pieces are made equal and the spacing is
        /// taken as a limit rather than a step, so what comes back is evenly divided with no short piece
        /// left over at the end.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length or the spacing is not a positive number.</exception>
        public static int SegmentsForSpacing(double length, double sweptAngle, double spacing)
        {
            RequirePositive(length, nameof(length));
            RequirePositive(spacing, nameof(spacing));

            double count = Math.Ceiling(length / spacing);

            return Clamp(sweptAngle, sweptAngle / Math.Max(count, 1.0));
        }

        /// <summary>
        /// Checks that a caller-supplied piece count is usable.
        /// </summary>
        /// <param name="segmentCount">The count asked for.</param>
        /// <param name="minimum">The fewest pieces this shape can be cut into.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when fewer than the minimum are asked for.</exception>
        public static void RequireSegmentCount(int segmentCount, int minimum)
        {
            if (segmentCount < minimum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(segmentCount),
                    minimum == 1 ? "A curve needs at least 1 piece." : $"A polygon needs at least {minimum} edges.");
            }
        }

        private static int Clamp(double sweptAngle, double perSegment)
        {
            if (perSegment <= 0.0 || double.IsNaN(perSegment))
            {
                return 1;
            }

            double count = Math.Ceiling(sweptAngle / perSegment);
            double cap = Math.Ceiling(MaxSegmentsPerTurn * sweptAngle / (Math.PI * 2.0));

            return (int)Math.Max(1.0, Math.Min(count, Math.Max(cap, 1.0)));
        }

        private static void RequirePositive(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "The value must be a positive number.");
            }
        }
    }
}
