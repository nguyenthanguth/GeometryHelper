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
        /// The most pieces a full turn is cut into by <see cref="SegmentsForChordTolerance"/>, which is not what keeps a
        /// chord tolerance: <see cref="SegmentsForArc"/> lifts it and refuses past <see cref="MostSegments"/> instead. It is
        /// also the size at which the writers stop taking the ellipse's corners from the library.
        /// </summary>
        public const int MaxSegmentsPerTurn = 4096;

        /// <summary>
        /// The most pieces one curve is ever cut into when its chord tolerance is kept, an ellipse's corners all round
        /// included; a tolerance that needs more is refused.
        /// </summary>
        public const int MostSegments = 1048576;

        /// <summary>
        /// Gets how many pieces a round curve is cut into so that no piece strays further than a chord
        /// tolerance from the true curve, up to <see cref="MaxSegmentsPerTurn"/> a turn: a finer tolerance is not kept.
        /// <see cref="SegmentsForArc"/> keeps it.
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

            return Clamp(sweptAngle, PerSegmentAngle(radius, chordTolerance));
        }

        /// <summary>
        /// Gets how many pieces an arc is cut into so that no piece strays further than a chord tolerance from the arc,
        /// however fine the tolerance: the count of <see cref="SegmentsForChordTolerance"/> where its cap of
        /// <see cref="MaxSegmentsPerTurn"/> a turn does not bind, and the count the tolerance needs where it does.
        /// </summary>
        /// <param name="radius">The radius of the arc.</param>
        /// <param name="sweptAngle">How far the arc turns, in radians.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc, in drawing units. Zero picks <see cref="AutomaticChordRatio"/> of the radius.</param>
        /// <returns>The number of pieces, at least one.</returns>
        /// <remarks>
        /// An arc of radius 83 333 sweeping 0.0012 radians cut within 1E-4 needs 13 pieces; the cap allows one, the chord,
        /// which passes 0.015 from the middle of the arc. Where the cap binds the count is worked out from the arc sine,
        /// which keeps its digits at a tolerance far below the radius.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius or the swept angle is not a positive number, when the tolerance is negative or not a number, or when the tolerance needs more than <see cref="MostSegments"/> pieces.</exception>
        public static int SegmentsForArc(double radius, double sweptAngle, double chordTolerance)
        {
            int library = SegmentsForChordTolerance(radius, sweptAngle, chordTolerance);

            // The automatic share is about fifty pieces a turn, which the cap never reaches.
            if (chordTolerance == 0.0)
            {
                return library;
            }

            double perSegment = PerSegmentAngle(radius, chordTolerance);
            double wanted = perSegment > 0.0 ? Math.Ceiling(sweptAngle / perSegment) : double.PositiveInfinity;
            double cap = Math.Max(Math.Ceiling(MaxSegmentsPerTurn * sweptAngle / (Math.PI * 2.0)), 1.0);

            if (wanted <= cap)
            {
                return library;
            }

            double exact = ExactSegments(radius, sweptAngle, chordTolerance);

            if (exact > MostSegments)
            {
                throw TooMany(chordTolerance);
            }

            return Math.Max(library, (int)exact);
        }

        /// <summary>
        /// Gets how many pieces an arc needs so that none strays further than a chord tolerance, worked out from the arc
        /// sine, with no cap: the sweep over the angle a piece may span, rounded up.
        /// </summary>
        /// <param name="radius">The radius of the arc.</param>
        /// <param name="sweptAngle">How far the arc turns, in radians.</param>
        /// <param name="chordTolerance">The largest gap allowed between a piece and the arc; above nought.</param>
        /// <returns>The count as a double, which is infinite where the tolerance is nought or the angle is too small to tell from it.</returns>
        public static double ExactSegments(double radius, double sweptAngle, double chordTolerance)
        {
            double share = chordTolerance / (2.0 * radius);
            double perChord = share >= 1.0 ? Math.PI * 2.0 : 4.0 * Math.Asin(Math.Sqrt(share));

            return Math.Ceiling(sweptAngle / perChord);
        }

        /// <summary>
        /// Gets the refusal of a chord tolerance that needs more than <see cref="MostSegments"/> pieces for one curve.
        /// </summary>
        public static ArgumentOutOfRangeException TooMany(double chordTolerance)
            => new ArgumentOutOfRangeException(
                nameof(chordTolerance),
                chordTolerance,
                "A chord tolerance of " + chordTolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " would cut the curve into more than " + MostSegments + " chords; ask for a larger one.");

        private static double PerSegmentAngle(double radius, double chordTolerance)
        {
            double error = chordTolerance > 0.0 ? chordTolerance : radius * AutomaticChordRatio;

            // A piece spanning an angle a strays r(1 - cos(a/2)) from the curve at its middle, so the angle
            // a tolerance allows is twice the arc cosine of what is left of the radius.
            double cosine = 1.0 - error / radius;
            return cosine <= -1.0 ? Math.PI * 2.0 : 2.0 * Math.Acos(cosine);
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
