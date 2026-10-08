using System;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// What a set of timed samples comes to: how many, the least and the most, the median and the 95th percentile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every figure is read off the samples sorted from the fastest, so the same samples give the same figures in any
    /// order. The median of an odd count is the middle sample, and of an even count the mean of the two middle ones.
    /// </para>
    /// <para>
    /// The 95th percentile is taken by nearest rank: the sample at rank ceil(0.95 n), counting the fastest as rank 1,
    /// so a sample that was taken, never a value between two. Of 20 samples it is the 19th fastest; of 15 or fewer it
    /// is the slowest, since ceil(0.95 n) is then n.
    /// </para>
    /// </remarks>
    internal sealed class Statistics
    {
        private Statistics(int count, double median, double p95, double min, double max)
        {
            Count = count;
            Median = median;
            P95 = p95;
            Min = min;
            Max = max;
        }

        /// <summary>The convention of <see cref="P95"/>, in words, for the reports.</summary>
        internal const string P95Convention = "nearest rank: the ceil(0.95 n)-th fastest of the n samples";

        /// <summary>Gets how many samples there are.</summary>
        internal int Count { get; }

        /// <summary>Gets the median, in milliseconds.</summary>
        internal double Median { get; }

        /// <summary>Gets the 95th percentile by nearest rank, in milliseconds.</summary>
        internal double P95 { get; }

        /// <summary>Gets the fastest sample, in milliseconds.</summary>
        internal double Min { get; }

        /// <summary>Gets the slowest sample, in milliseconds.</summary>
        internal double Max { get; }

        /// <summary>
        /// Works out the figures of a set of samples.
        /// </summary>
        /// <param name="samples">The samples, in milliseconds, in the order they were taken; at least one.</param>
        /// <returns>The figures.</returns>
        internal static Statistics Of(double[] samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length == 0)
            {
                throw new ArgumentException("There are no samples to work out figures from.", nameof(samples));
            }

            var sorted = (double[])samples.Clone();
            Array.Sort(sorted);

            int n = sorted.Length;
            double median = n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;

            // The rank in whole numbers, so that 0.95 * 20, which in binary need not come to 19 exactly, cannot take the 20th.
            int rank = (95 * n + 99) / 100;

            return new Statistics(n, median, sorted[rank - 1], sorted[0], sorted[n - 1]);
        }
    }
}
