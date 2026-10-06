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
            => WithOptions(TryUnion, first, second, options, false, out result, out outcome, out _, out _, out _, out _);

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
            => WithOptions(TryIntersect, first, second, options, false, out result, out outcome, out _, out _, out _, out _);

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
            => WithOptions(TrySubtract, subject, tool, options, false, out result, out outcome, out _, out _, out _, out _);

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
            GuardSubtractAll(subject, tools, options);
            return SubtractAll(subject, tools, options, null, out result, out report);
        }

        /// <summary>
        /// Takes bodies out of a solid one after another as the options say, each result checked, as
        /// <see cref="TrySubtractAll(GeoSolid3, IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>
        /// does; and closes a cut that one would skip, by
        /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>, where that closes it and
        /// the body closed holds what the cut can leave.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tools">The bodies to take away, in the order they are taken.</param>
        /// <param name="result">What is left of the subject; null when a tool took all of it.</param>
        /// <param name="options">How each difference is worked out.</param>
        /// <param name="closing">How a cut that would be skipped is closed.</param>
        /// <param name="report">Which tools were taken away, which within the fallback, which closed, and which could not be.</param>
        /// <returns>false when a tool took all that was left; true otherwise, whatever was skipped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the subject, the tools, the options or the closing options are null.</exception>
        /// <exception cref="ArgumentException">Thrown when one of the tools is null.</exception>
        /// <remarks>
        /// <para>
        /// Every cut the other overload takes is taken here the same, bit for bit. Only a cut it would skip, whose result
        /// is valid neither within the options' tolerance nor within the fallback, is closed: the result worked out within
        /// the tolerance first, the tool put onto the body as the contact says; where that does not close, the result
        /// worked out within the fallback, where there is a fallback and it gave a body. A cut that could not be worked out
        /// at all, giving no body, is skipped as before, and so is one neither result of which closes.
        /// </para>
        /// <para>
        /// A body closed is taken only where it is valid within the options' tolerance as well as within the closing's,
        /// both checks passing whichever tolerance is the finer; and where it holds what a difference can leave: no more
        /// than the body before the cut, and no less than that less the whole of the tool, each within the options' point
        /// tolerance times the area of the two, and a hair more for the rounding, no bound from below being asked where
        /// either carries openings. A closed body holding more or less is refused, and the next tried or the cut skipped;
        /// <see cref="SubtractReport.RefusedByVolume"/> says which cuts were skipped so.
        /// </para>
        /// <para>
        /// Of the 35 cuts that 17 parts of a Tekla model left skipped, cut within a thousandth with a contact and a fallback
        /// of a hundredth, closing the result within the thousandth with a gap of 0.005, holes up to 100 and 0.01 out of
        /// flat closed 23, each within 2.1E-6 of what the cut within the hundredth holds.
        /// </para>
        /// </remarks>
        public static bool TrySubtractAll(GeoSolid3 subject, IEnumerable<GeoSolid3> tools, out GeoSolid3 result, SolidBooleanOptions options, SolidClosingOptions closing, out SubtractReport report)
        {
            GuardSubtractAll(subject, tools, options);

            if (closing == null)
            {
                throw new ArgumentNullException(nameof(closing));
            }

            return SubtractAll(subject, tools, options, closing, out result, out report);
        }

        /// <summary>
        /// Checks the arguments of a run of cuts.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tools">The bodies to take away.</param>
        /// <param name="options">How each difference is worked out.</param>
        /// <exception cref="ArgumentNullException">Thrown when any of them is null.</exception>
        private static void GuardSubtractAll(GeoSolid3 subject, IEnumerable<GeoSolid3> tools, SolidBooleanOptions options)
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
        }

        /// <summary>
        /// Takes bodies out of a solid one after another, each result checked, and where asked closes a cut that would be
        /// skipped; see <see cref="TrySubtractAll(GeoSolid3, IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, SolidClosingOptions, out SubtractReport)"/>.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tools">The bodies to take away, in the order they are taken.</param>
        /// <param name="options">How each difference is worked out.</param>
        /// <param name="closing">How a cut that would be skipped is closed; null where none is.</param>
        /// <param name="result">What is left of the subject; null when a tool took all of it.</param>
        /// <param name="report">What was done.</param>
        private static bool SubtractAll(GeoSolid3 subject, IEnumerable<GeoSolid3> tools, SolidBooleanOptions options, SolidClosingOptions closing, out GeoSolid3 result, out SubtractReport report)
        {
            GeoSolid3 current = subject;
            var skipped = new List<int>();
            var withinFallback = new List<int>();
            var closed = new List<int>();
            var closings = new List<SolidClosing3>();
            var refused = new List<int>();
            int tried = 0, taken = 0;

            foreach (GeoSolid3 tool in tools)
            {
                if (tool == null)
                {
                    throw new ArgumentException("A body to take away cannot be null.", nameof(tools));
                }

                int at = tried++;
                bool left = WithOptions(TrySubtract, current, tool, options, true, out GeoSolid3 rest, out BooleanOutcome outcome, out bool good, out bool again, out GeoSolid3 put, out GeoSolid3 restAgain);

                if (!good)
                {
                    // A cut that would be skipped, closed where asked and where that can be done.
                    if (closing == null)
                    {
                        skipped.Add(at);
                        continue;
                    }

                    if (!TryCloseCut(current, put, left ? rest : null, restAgain, options, closing, out rest, out SolidClosing3 how, out again, out bool outOfBounds))
                    {
                        if (outOfBounds)
                        {
                            refused.Add(at);
                        }

                        skipped.Add(at);
                        continue;
                    }

                    closed.Add(at);
                    closings.Add(how);
                    left = true;
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
                    report = new SubtractReport(tried, taken, skipped, withinFallback, closed, closings, refused);
                    return false;
                }

                current = rest;
            }

            result = current;
            report = new SubtractReport(tried, taken, skipped, withinFallback, closed, closings, refused);
            return true;
        }

        /// <summary>
        /// Closes a cut whose result is not valid: the result within the tolerance first, then the one within the fallback,
        /// the first that closes valid within the booleans' tolerance and holding what the cut can leave taken.
        /// </summary>
        /// <param name="before">The body before the cut.</param>
        /// <param name="tool">The tool as it was taken away, put onto the body.</param>
        /// <param name="withinTolerance">The result worked out within the tolerance; null where there is none.</param>
        /// <param name="withinFallback">The result worked out within the fallback; null where there is none.</param>
        /// <param name="options">How the difference was worked out.</param>
        /// <param name="closing">How a result is closed.</param>
        /// <param name="closed">The body closed; null when the method returns false.</param>
        /// <param name="how">What the closing did; null when the method returns false.</param>
        /// <param name="fallback">Whether the body closed is the result within the fallback.</param>
        /// <param name="outOfBounds">Whether a body closed was refused for the volume it holds.</param>
        private static bool TryCloseCut(GeoSolid3 before, GeoSolid3 tool, GeoSolid3 withinTolerance, GeoSolid3 withinFallback, SolidBooleanOptions options, SolidClosingOptions closing, out GeoSolid3 closed, out SolidClosing3 how, out bool fallback, out bool outOfBounds)
        {
            outOfBounds = false;
            GeoSolid3[] candidates = { withinTolerance, withinFallback };

            for (int k = 0; k < candidates.Length; k++)
            {
                GeoSolid3 candidate = candidates[k];

                if (candidate == null || !candidate.TryClose(out GeoSolid3 body, closing, out SolidClosing3 report) || !body.Validate(options.Tolerance).IsValid)
                {
                    continue;
                }

                if (!HoldsWhatACutLeaves(body, before, tool, options.Tolerance))
                {
                    outOfBounds = true;
                    continue;
                }

                closed = body;
                how = report;
                fallback = k == 1;
                return true;
            }

            closed = null;
            how = null;
            fallback = false;
            return false;
        }

        /// <summary>
        /// Determines whether a body holds what a difference can leave: no more than the body it was cut from, and no less
        /// than that less the whole of the tool, each within the point tolerance times the area of the two and a hair more
        /// for the rounding; no bound from below where either carries openings, which the faces hold more than.
        /// </summary>
        /// <param name="cut">The body the cut left.</param>
        /// <param name="before">The body it was cut from.</param>
        /// <param name="tool">The tool taken away.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool HoldsWhatACutLeaves(GeoSolid3 cut, GeoSolid3 before, GeoSolid3 tool, Tolerance tolerance)
        {
            double v = cut.GrossVolume;
            double vBefore = before.GrossVolume;
            double vTool = tool.GrossVolume;
            double slack = (tolerance.EqualPoint * (before.GrossSurfaceArea + tool.GrossSurfaceArea)) + (1E-9 * (Math.Abs(vBefore) + Math.Abs(vTool)));
            bool whole = before.Openings.Count == 0 && tool.Openings.Count == 0;

            return v <= vBefore + slack && (!whole || v >= vBefore - vTool - slack);
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
        /// <param name="put">The second body as it was used, put onto the first.</param>
        /// <param name="resultAgain">Where the result is not valid and was worked out again within the fallback, what that
        /// gave, though not valid; null otherwise, and where it gave no body.</param>
        private static bool WithOptions(Operation operation, GeoSolid3 first, GeoSolid3 second, SolidBooleanOptions options, bool check, out GeoSolid3 result, out BooleanOutcome outcome, out bool good, out bool again, out GeoSolid3 put, out GeoSolid3 resultAgain)
        {
            Guard(first, second);

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            again = false;
            resultAgain = null;
            Tolerance tolerance = options.Tolerance;
            GeoSolid3 tool = Touching3.PutOnto(first, second, options.Contact, tolerance);
            put = tool;
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

            bool madeAgain = operation(first, tool, out GeoSolid3 worked, options.Fallback.Value, out BooleanOutcome outcomeAgain);

            if (!Valid(madeAgain, worked, outcomeAgain, tolerance))
            {
                // Kept for a closing, which tries the result within the tolerance first.
                resultAgain = madeAgain ? worked : null;
                return made;
            }

            result = worked;
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
