using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// What <see cref="GeoSolid3.TrySubtractAll(IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>
    /// did with each of the bodies it took away: which it could not take away, and which it took away within the
    /// fallback tolerance; and, for
    /// <see cref="GeoSolid3.TrySubtractAll(IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, SolidClosingOptions, out SubtractReport)"/>,
    /// which it took away closed.
    /// </summary>
    public sealed class SubtractReport
    {
        internal SubtractReport(int tools, int taken, IReadOnlyList<int> skipped, IReadOnlyList<int> withinFallback, IReadOnlyList<int> closed, IReadOnlyList<SolidClosing3> closings, IReadOnlyList<int> refusedByVolume)
        {
            Tools = tools;
            Taken = taken;
            Skipped = skipped;
            WithinFallback = withinFallback;
            Closed = closed;
            Closings = closings;
            RefusedByVolume = refusedByVolume;
        }

        /// <summary>
        /// Gets how many bodies were tried, in the order given; fewer than were given where one took all that was left.
        /// </summary>
        public int Tools { get; }

        /// <summary>
        /// Gets how many of those were taken away, those that took nothing because they did not reach the body included,
        /// and those whose cut was closed.
        /// </summary>
        public int Taken { get; }

        /// <summary>
        /// Gets the place in the order given of each body that could not be taken away, the body being kept as it was
        /// before it: what it would have taken is still in the result.
        /// </summary>
        public IReadOnlyList<int> Skipped { get; }

        /// <summary>
        /// Gets the place in the order given of each body taken away within the fallback tolerance, where within the
        /// tolerance the result was not valid; a cut closed from the result within the fallback among them.
        /// </summary>
        public IReadOnlyList<int> WithinFallback { get; }

        /// <summary>
        /// Gets the place in the order given of each body whose cut, not valid as worked out, was taken closed by
        /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>; counted in
        /// <see cref="Taken"/>, not in <see cref="Skipped"/>. Empty where no closing was asked for.
        /// </summary>
        /// <remarks>
        /// A place also in <see cref="WithinFallback"/> is a cut closed from the result worked out within the fallback; the
        /// others were closed from the result within the tolerance. No cut is closed where the body before it, or the tool
        /// as put onto it, carries openings: it is skipped as without closing.
        /// </remarks>
        public IReadOnlyList<int> Closed { get; }

        /// <summary>
        /// Gets what the closing did to each cut in <see cref="Closed"/>, in the same order.
        /// </summary>
        public IReadOnlyList<SolidClosing3> Closings { get; }

        /// <summary>
        /// Gets the place in the order given of each body skipped though its cut closed, the body closed holding more than
        /// the body before the cut, or less than that less the whole of the tool; each is in <see cref="Skipped"/> too.
        /// Empty where no closing was asked for.
        /// </summary>
        public IReadOnlyList<int> RefusedByVolume { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "(Tools: {0}, Taken: {1}, Skipped: {2}, WithinFallback: {3}, Closed: {4})", Tools, Taken, Skipped.Count, WithinFallback.Count, Closed.Count);
        }
    }
}
