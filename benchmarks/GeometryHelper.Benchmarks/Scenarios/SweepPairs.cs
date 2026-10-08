using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// How many pairs a sort-and-sweep along one axis has to look at: the pairs whose spans along it come within a reach
    /// of each other.
    /// </summary>
    /// <remarks>
    /// A sweep takes the spans in the order they start and looks at every span still open when the next starts, so the
    /// pairs it looks at are those whose spans overlap, grown by the reach the sweep allows, whatever the other axes say.
    /// That is the count an axis is judged by in the plan of the adaptive sweep, and the count the notes of the axis-trap
    /// cases give for each axis.
    /// </remarks>
    internal static class SweepPairs
    {
        /// <summary>
        /// Counts the pairs whose spans come within a reach of each other.
        /// </summary>
        /// <param name="lows">Where each span starts.</param>
        /// <param name="highs">Where each span ends, in the same order.</param>
        /// <param name="reach">How far apart two spans may stand and still be looked at.</param>
        /// <returns>The number of pairs, each counted once.</returns>
        internal static long Count(IReadOnlyList<double> lows, IReadOnlyList<double> highs, double reach)
        {
            int n = lows.Count;
            int[] order = Enumerable.Range(0, n).ToArray();
            double[] keys = lows.ToArray();
            Array.Sort(keys, order);

            long pairs = 0;

            for (int i = 0; i < n; i++)
            {
                // The spans after this one start no earlier, and meet it while they start within its end and the reach.
                double end = highs[order[i]] + reach;
                int lo = i + 1, hi = n;

                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;

                    if (keys[mid] <= end)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid;
                    }
                }

                pairs += lo - i - 1;
            }

            return pairs;
        }

        /// <summary>
        /// Says how many pairs a sweep along each axis looks at, and which axis looks at fewest.
        /// </summary>
        /// <param name="counts">The pairs along each axis, X first.</param>
        /// <returns>A note such as "pairs a sweep looks at: X 12 004 650, Y 171 500, Z 171 500; fewest on Y, X 70 times as many".</returns>
        internal static string Describe(params long[] counts)
        {
            string[] axes = { "X", "Y", "Z" };
            int best = 0;

            for (int a = 1; a < counts.Length; a++)
            {
                if (counts[a] < counts[best])
                {
                    best = a;
                }
            }

            var parts = new List<string>();

            for (int a = 0; a < counts.Length; a++)
            {
                parts.Add(axes[a] + " " + Report.Count(counts[a]));
            }

            string fewest = "; fewest on " + axes[best];

            if (best > 0)
            {
                fewest += counts[best] == 0
                    ? ", none at all"
                    : ", X " + ((double)counts[0] / counts[best]).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " times as many";
            }

            return "pairs a sweep looks at: " + string.Join(", ", parts) + fewest;
        }
    }
}
