using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Finds the pairs of boxes that overlap, and the pairs of points that are close, by sorting along the axis with the fewest candidate
    /// pairs and sweeping along it. A sweep along a fixed axis goes quadratic on shapes thin or long in it: a loop 1 000 000 long and 1
    /// wide along X holds every edge in the same few thousandths of Y, so the axis is chosen by counting.
    /// </summary>
    public static class Sweep
    {
        /// <summary>
        /// Calls the visitor once for each pair of boxes that overlap when grown by a distance, the lower index first. The order of the
        /// calls follows the axis chosen, so a caller that wants an order sorts what it collects.
        /// </summary>
        /// <param name="lo">The least corner of each box, three numbers to a box.</param>
        /// <param name="hi">The greatest corner of each box, three numbers to a box.</param>
        /// <param name="count">The number of boxes.</param>
        /// <param name="expand">How much each box is grown by.</param>
        /// <param name="visit">What to do with a pair.</param>
        /// <param name="cancel">Stops the sweep.</param>
        public static void Pairs(double[] lo, double[] hi, int count, double expand, Action<int, int> visit, CancellationToken cancel)
        {
            if (count < 2)
            {
                return;
            }

            int best = 0;
            long bestPairs = long.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                long pairs = CountPairs(lo, hi, count, expand, axis);

                if (pairs < bestPairs)
                {
                    bestPairs = pairs;
                    best = axis;
                }
            }

            int[] order = Order(lo, count, best);
            int other1 = (best + 1) % 3;
            int other2 = (best + 2) % 3;

            for (int a = 0; a < count; a++)
            {
                if ((a & 1023) == 0)
                {
                    cancel.ThrowIfCancellationRequested();
                }

                int i = order[a];
                double limit = hi[3 * i + best] + expand;

                for (int b = a + 1; b < count; b++)
                {
                    int j = order[b];

                    if (lo[3 * j + best] > limit)
                    {
                        break;
                    }

                    if (lo[3 * j + other1] <= hi[3 * i + other1] + expand && lo[3 * i + other1] <= hi[3 * j + other1] + expand
                        && lo[3 * j + other2] <= hi[3 * i + other2] + expand && lo[3 * i + other2] <= hi[3 * j + other2] + expand)
                    {
                        visit(Math.Min(i, j), Math.Max(i, j));
                    }
                }
            }
        }

        /// <summary>
        /// Finds the pairs of points no further apart than a distance.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="distance">The distance; a pair exactly that far apart is close.</param>
        /// <param name="cancel">Stops the sweep.</param>
        /// <returns>The pairs as lower index, higher index and distance, ordered by the two indexes.</returns>
        public static List<(int First, int Second, double Distance)> ClosePairs(Vec3[] points, double distance, CancellationToken cancel)
        {
            int n = points.Length;
            var lo = new double[3 * n];

            for (int i = 0; i < n; i++)
            {
                lo[3 * i] = points[i].X;
                lo[3 * i + 1] = points[i].Y;
                lo[3 * i + 2] = points[i].Z;
            }

            var result = new List<(int, int, double)>();

            // A point is a box of no size; the boxes are grown by the distance in each axis, and the pair then judged by its true distance.
            Pairs(
                lo,
                lo,
                n,
                distance,
                (i, j) =>
                {
                    double d = (points[i] - points[j]).Length;

                    if (d <= distance)
                    {
                        result.Add((i, j, d));
                    }
                },
                cancel);

            result.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
            return result;
        }

        private static int[] Order(double[] lo, int count, int axis)
        {
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = lo[3 * a + axis].CompareTo(lo[3 * b + axis]);
                return c != 0 ? c : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>
        /// Counts the pairs that overlap along one axis, which is what a sweep along it visits.
        /// </summary>
        private static long CountPairs(double[] lo, double[] hi, int count, double expand, int axis)
        {
            int[] order = Order(lo, count, axis);
            var sorted = new double[count];

            for (int a = 0; a < count; a++)
            {
                sorted[a] = lo[3 * order[a] + axis];
            }

            long pairs = 0;

            for (int a = 0; a < count; a++)
            {
                double limit = hi[3 * order[a] + axis] + expand;

                // The first position whose start is past the limit.
                int low = a + 1;
                int high = count;

                while (low < high)
                {
                    int mid = (low + high) >> 1;

                    if (sorted[mid] <= limit)
                    {
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                pairs += low - a - 1;
            }

            return pairs;
        }
    }
}
