using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// What <see cref="GeoSolid3.TrySubtractAll(IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>
    /// did with each of the bodies it took away: which it could not take away, and which it took away within the
    /// fallback tolerance.
    /// </summary>
    public sealed class SubtractReport
    {
        internal SubtractReport(int tools, int taken, IReadOnlyList<int> skipped, IReadOnlyList<int> withinFallback)
        {
            Tools = tools;
            Taken = taken;
            Skipped = skipped;
            WithinFallback = withinFallback;
        }

        /// <summary>
        /// Gets how many bodies were tried, in the order given; fewer than were given where one took all that was left.
        /// </summary>
        public int Tools { get; }

        /// <summary>
        /// Gets how many of those were taken away, those that took nothing because they did not reach the body included.
        /// </summary>
        public int Taken { get; }

        /// <summary>
        /// Gets the place in the order given of each body that could not be taken away, the body being kept as it was
        /// before it: what it would have taken is still in the result.
        /// </summary>
        public IReadOnlyList<int> Skipped { get; }

        /// <summary>
        /// Gets the place in the order given of each body taken away within the fallback tolerance, where within the
        /// tolerance the result was not valid.
        /// </summary>
        public IReadOnlyList<int> WithinFallback { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "(Tools: {0}, Taken: {1}, Skipped: {2}, WithinFallback: {3})", Tools, Taken, Skipped.Count, WithinFallback.Count);
        }
    }
}
