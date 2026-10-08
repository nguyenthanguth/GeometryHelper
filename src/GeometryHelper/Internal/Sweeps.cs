namespace GeometryHelper.Internal
{
    /// <summary>
    /// What a sort-and-sweep has to look at, for choosing the axis it sweeps along.
    /// </summary>
    /// <remarks>
    /// A sweep along a fixed axis goes quadratic on things that all overlap along it: the long edges of a body along
    /// its length, every edge of a slab along the axis it is thin along. Counting the pairs each axis would leave costs
    /// a sort and a binary search for each thing, and the sweep then runs along the axis with the fewest.
    /// </remarks>
    internal static class Sweeps
    {
        /// <summary>
        /// How many pairs a sweep tries: each thing, taken by where it starts, against every later one that starts no
        /// further along than it ends.
        /// </summary>
        /// <param name="lows">Where the things start, sorted.</param>
        /// <param name="highs">Where they end, grown by the reach of the sweep, in any order.</param>
        internal static long Tried(double[] lows, double[] highs)
        {
            long reached = 0;

            foreach (double high in highs)
            {
                // How many things start no further along than this one ends: itself, those before it in the sweep and
                // those it is tried against.
                int below = 0, above = lows.Length;

                while (below < above)
                {
                    int middle = (below + above) >> 1;

                    if (lows[middle] <= high)
                    {
                        below = middle + 1;
                    }
                    else
                    {
                        above = middle;
                    }
                }

                reached += below;
            }

            long count = lows.Length;
            return reached - count * (count + 1) / 2;
        }
    }
}
