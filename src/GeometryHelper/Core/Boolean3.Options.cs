using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Booleans of solids worked out with <see cref="SolidBooleanOptions"/>: the second body's faces lying against the
    /// first's put onto them, and a result that is not valid worked out again within the fallback tolerance.
    /// </summary>
    public static partial class Boolean3
    {
        #region Options

        private delegate bool Operation(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome);

        /// <summary>
        /// Joins two solids into one as the options say; see <see cref="SolidBooleanOptions"/>.
        /// </summary>
        /// <param name="first">The first body; it is not moved.</param>
        /// <param name="second">The second body; its faces lying against the first's are put onto them.</param>
        /// <param name="result">The combined body.</param>
        /// <param name="options">How the union is worked out.</param>
        /// <param name="outcome">How it came out, as <see cref="TryUnion(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> says.</param>
        /// <returns>false when the two could not be combined, which is logged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either body or the options are null.</exception>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, SolidBooleanOptions options, out BooleanOutcome outcome)
            => WithOptions(TryUnion, first, second, options, false, out result, out outcome, out _, out _);

        /// <summary>
        /// Gets the solid two bodies share as the options say; see <see cref="SolidBooleanOptions"/>.
        /// </summary>
        /// <param name="first">The first body; it is not moved.</param>
        /// <param name="second">The second body; its faces lying against the first's are put onto them.</param>
        /// <param name="result">The common part.</param>
        /// <param name="options">How the intersection is worked out.</param>
        /// <param name="outcome">How it came out, as <see cref="TryIntersect(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> says.</param>
        /// <returns>false when the two share nothing, or when it cannot be worked out.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either body or the options are null.</exception>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, SolidBooleanOptions options, out BooleanOutcome outcome)
            => WithOptions(TryIntersect, first, second, options, false, out result, out outcome, out _, out _);

        /// <summary>
        /// Takes one solid out of another as the options say; see <see cref="SolidBooleanOptions"/>.
        /// </summary>
        /// <param name="subject">The body to cut material from; it is not moved.</param>
        /// <param name="tool">The body to remove; its faces lying against the subject's are put onto them.</param>
        /// <param name="result">What is left of the subject.</param>
        /// <param name="options">How the difference is worked out.</param>
        /// <param name="outcome">How it came out, as <see cref="TrySubtract(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> says.</param>
        /// <returns>false when nothing is left, or when it cannot be worked out.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either body or the options are null.</exception>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, SolidBooleanOptions options, out BooleanOutcome outcome)
            => WithOptions(TrySubtract, subject, tool, options, false, out result, out outcome, out _, out _);

        /// <summary>
        /// Takes bodies out of a solid one after another as the options say, each result checked, and says what it did.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tools">The bodies to take away, in the order they are taken.</param>
        /// <param name="result">What is left of the subject; null when a tool took all of it.</param>
        /// <param name="options">How each difference is worked out.</param>
        /// <param name="report">Which tools were taken away, which within the fallback, and which could not be.</param>
        /// <returns>false when a tool took all that was left; true otherwise, whatever was skipped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the subject, the tools or the options are null.</exception>
        /// <exception cref="ArgumentException">Thrown when one of the tools is null.</exception>
        /// <remarks>
        /// <para>
        /// Each cut is the one <see cref="TrySubtract(GeoSolid3, GeoSolid3, out GeoSolid3, SolidBooleanOptions, out BooleanOutcome)"/>
        /// works out, and its result is checked by <see cref="GeoSolid3.Validate(Tolerance)"/> within the options' tolerance
        /// whether or not there is a fallback. A cut whose result is not valid, within the tolerance or the fallback, or
        /// which could not be worked out, is skipped: the body is kept as it was before it, and what the tool would have
        /// taken stays in the result, which is better than a body with the wrong volume. The report says which.
        /// </para>
        /// <para>
        /// This is the net body of a part of a model, cut by every part it meets: of the 79 864 parts of a Tekla model so
        /// cut within a thousandth, 180 had a cut skipped, 323 cuts in all; with a contact of a hundredth, 109 and 196;
        /// with a fallback of a hundredth as well, 19 and 38, and 136 cuts were taken within the fallback.
        /// </para>
        /// </remarks>
        public static bool TrySubtractAll(GeoSolid3 subject, IEnumerable<GeoSolid3> tools, out GeoSolid3 result, SolidBooleanOptions options, out SubtractReport report)
        {
            if (subject == null)
            {
                throw new ArgumentNullException(nameof(subject));
            }

            if (tools == null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            GeoSolid3 current = subject;
            var skipped = new List<int>();
            var withinFallback = new List<int>();
            int tried = 0, taken = 0;

            foreach (GeoSolid3 tool in tools)
            {
                if (tool == null)
                {
                    throw new ArgumentException("A body to take away cannot be null.", nameof(tools));
                }

                int at = tried++;
                bool left = WithOptions(TrySubtract, current, tool, options, true, out GeoSolid3 rest, out BooleanOutcome outcome, out bool good, out bool again);

                if (!good)
                {
                    skipped.Add(at);
                    continue;
                }

                taken++;

                if (again)
                {
                    withinFallback.Add(at);
                }

                if (!left)
                {
                    // All that was left is taken.
                    result = null;
                    report = new SubtractReport(tried, taken, skipped, withinFallback);
                    return false;
                }

                current = rest;
            }

            result = current;
            report = new SubtractReport(tried, taken, skipped, withinFallback);
            return true;
        }

        /// <summary>
        /// Works out a boolean as the options say: the second body put onto the first where they touch, the result checked
        /// where there is a fallback or where asked, and worked out again within the fallback where it is not valid.
        /// </summary>
        /// <param name="operation">The boolean.</param>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="options">The options.</param>
        /// <param name="check">Whether the result is checked even with no fallback.</param>
        /// <param name="result">The result.</param>
        /// <param name="outcome">How it came out.</param>
        /// <param name="good">Whether the result taken was checked and is valid, or nothing was checked.</param>
        /// <param name="again">Whether the result taken was worked out within the fallback.</param>
        private static bool WithOptions(Operation operation, GeoSolid3 first, GeoSolid3 second, SolidBooleanOptions options, bool check, out GeoSolid3 result, out BooleanOutcome outcome, out bool good, out bool again)
        {
            Guard(first, second);

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            again = false;
            Tolerance tolerance = options.Tolerance;
            GeoSolid3 tool = Touching3.PutOnto(first, second, options.Contact, tolerance);
            bool made = operation(first, tool, out result, tolerance, out outcome);

            if (!check && !options.Fallback.HasValue)
            {
                good = true;
                return made;
            }

            good = Valid(made, result, outcome, tolerance);

            if (good || !options.Fallback.HasValue)
            {
                return made;
            }

            bool madeAgain = operation(first, tool, out GeoSolid3 resultAgain, options.Fallback.Value, out BooleanOutcome outcomeAgain);

            if (!Valid(madeAgain, resultAgain, outcomeAgain, tolerance))
            {
                return made;
            }

            result = resultAgain;
            outcome = outcomeAgain;
            good = true;
            again = true;
            return madeAgain;
        }

        /// <summary>
        /// Whether what a boolean gave can be taken: a valid body within the tolerance, or nothing at all where nothing is
        /// what it came to.
        /// </summary>
        private static bool Valid(bool made, GeoSolid3 result, BooleanOutcome outcome, Tolerance tolerance)
            => made ? result != null && result.Validate(tolerance).IsValid : outcome == BooleanOutcome.Empty;

        #endregion
    }
}
