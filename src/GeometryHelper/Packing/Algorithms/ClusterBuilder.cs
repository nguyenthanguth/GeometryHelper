using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// Lays the boxes of a group out as blocks, each of which goes onto one sheet whole.
    /// </summary>
    internal static class ClusterBuilder
    {
        /// <summary>
        /// The most widths a compact block is tried at: enough to find a block near square, few enough that a group of
        /// a thousand boxes costs no more than a few packings of it.
        /// </summary>
        private const int MostWidths = 16;

        /// <summary>
        /// Lays out the boxes of a group that fit a sheet at all.
        /// </summary>
        /// <param name="group">Which group of the input it is.</param>
        /// <param name="footprints">The footprints of all the boxes of the group, by their place in it.</param>
        /// <param name="members">Which of them to lay out, in their order; each fits the usable area.</param>
        /// <param name="layout">Whether to lay them out afresh or keep them as they stand.</param>
        /// <param name="spacing">The gap between two boxes of the group, in the units of the boxes.</param>
        /// <param name="usableWidth">The width of the usable area of a sheet.</param>
        /// <param name="usableHeight">The height of the usable area of a sheet.</param>
        /// <param name="slack">How far past a free space a box may reach and still go in.</param>
        /// <returns>
        /// One block, or, for a group too large for one sheet, as many as it takes, each holding the boxes that follow
        /// on from the one before.
        /// </returns>
        internal static List<Cluster> Build(int group, IReadOnlyList<Footprint> footprints, IReadOnlyList<int> members, GroupLayout layout,
            double spacing, double usableWidth, double usableHeight, double slack)
        {
            var clusters = new List<Cluster>();
            if (members.Count == 0)
            {
                return clusters;
            }

            if (layout == GroupLayout.Keep)
            {
                Cluster kept = Keep(group, footprints, members, usableWidth, usableHeight, slack);
                if (kept != null)
                {
                    clusters.Add(kept);
                    return clusters;
                }
            }

            foreach (List<int> run in Runs(footprints, members, spacing, usableWidth, usableHeight, slack))
            {
                // A run went onto one sheet, so it makes a block; were it not to, its boxes would be reported not placed.
                Cluster cluster = Compact(group, footprints, run, spacing, usableWidth, usableHeight, slack);
                if (cluster != null)
                {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        /// <summary>
        /// The boxes as they stand, as one block; null when the block is too large for one sheet.
        /// </summary>
        private static Cluster Keep(int group, IReadOnlyList<Footprint> footprints, IReadOnlyList<int> members,
            double usableWidth, double usableHeight, double slack)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (int m in members)
            {
                minX = Math.Min(minX, footprints[m].MinX);
                minY = Math.Min(minY, footprints[m].MinY);
                maxX = Math.Max(maxX, footprints[m].MaxX);
                maxY = Math.Max(maxY, footprints[m].MaxY);
            }

            double width = maxX - minX, height = maxY - minY;
            if (!(width <= usableWidth + slack && height <= usableHeight + slack))
            {
                return null;
            }

            var offsets = members.Select(m => (footprints[m].MinX - minX, footprints[m].MinY - minY)).ToArray();
            return new Cluster(group, members.ToArray(), offsets, width, height, kept: true);
        }

        /// <summary>
        /// Splits the boxes, in their order, into runs that each go onto one sheet: a run ends at the first box that no
        /// longer fits beside those before it.
        /// </summary>
        private static List<List<int>> Runs(IReadOnlyList<Footprint> footprints, IReadOnlyList<int> members, double spacing,
            double usableWidth, double usableHeight, double slack)
        {
            var runs = new List<List<int>>();
            var run = new List<int>();
            var bin = new MaxRectsBin(usableWidth + spacing, usableHeight + spacing, slack);
            foreach (int m in members)
            {
                double width = footprints[m].Width + spacing, height = footprints[m].Height + spacing;
                if (!bin.TryPlace(width, height, out _, out _))
                {
                    if (run.Count > 0)
                    {
                        runs.Add(run);
                        run = new List<int>();
                    }

                    // Each box fits an empty sheet, so it starts the next run; one that did not would be left out, and
                    // reported not placed.
                    bin = new MaxRectsBin(usableWidth + spacing, usableHeight + spacing, slack);
                    if (!bin.TryPlace(width, height, out _, out _))
                    {
                        continue;
                    }
                }

                run.Add(m);
            }

            if (run.Count > 0)
            {
                runs.Add(run);
            }

            return runs;
        }

        /// <summary>
        /// Lays a run of boxes out as close together as they go: packed in their order, from the upper left, into
        /// strips of several widths, the block of least perimeter kept, then of least area, then the narrowest.
        /// </summary>
        /// <remarks>
        /// The perimeter first, because it keeps the boxes near one another: by area alone, a column of boxes the
        /// spacing apart came out smaller than the square block they make, which has more gaps in it.
        /// </remarks>
        private static Cluster Compact(int group, IReadOnlyList<Footprint> footprints, List<int> run, double spacing,
            double usableWidth, double usableHeight, double slack)
        {
            if (run.Count == 1)
            {
                Footprint only = footprints[run[0]];
                return new Cluster(group, run.ToArray(), new[] { (0.0, 0.0) }, only.Width, only.Height, kept: false);
            }

            Cluster best = null;
            double bestArea = double.PositiveInfinity, bestPerimeter = double.PositiveInfinity;
            foreach (double width in Widths(footprints, run, spacing, usableWidth))
            {
                Cluster tried = Pack(group, footprints, run, spacing, width, usableHeight, slack);
                if (tried == null)
                {
                    continue;
                }

                double perimeter = tried.Width + tried.Height;
                if (perimeter < bestPerimeter || (perimeter == bestPerimeter && tried.Area < bestArea))
                {
                    best = tried;
                    bestArea = tried.Area;
                    bestPerimeter = perimeter;
                }
            }

            // The run went onto one sheet packed at its full width, so that width at least takes it.
            return best ?? Pack(group, footprints, run, spacing, usableWidth, usableHeight, slack);
        }

        /// <summary>
        /// The widths a run is tried at, narrowest first: the widest box alone, each row the boxes make from the first
        /// on, and the usable width, no more than <see cref="MostWidths"/> of them.
        /// </summary>
        private static List<double> Widths(IReadOnlyList<Footprint> footprints, List<int> run, double spacing, double usableWidth)
        {
            double widest = run.Max(m => footprints[m].Width);
            var rows = new List<double>();
            double row = 0.0;
            for (int i = 0; i < run.Count; i++)
            {
                row += footprints[run[i]].Width + (i > 0 ? spacing : 0.0);
                if (row > widest)
                {
                    rows.Add(Math.Min(row, usableWidth));
                }
            }

            var widths = new List<double> { Math.Min(widest, usableWidth) };
            if (rows.Count <= MostWidths - 1)
            {
                widths.AddRange(rows);
            }
            else
            {
                // Evenly through the rows, the last, the widest, always among them.
                for (int k = 1; k < MostWidths; k++)
                {
                    widths.Add(rows[(int)((long)k * (rows.Count - 1) / (MostWidths - 1))]);
                }
            }

            return widths.Distinct().OrderBy(w => w).ToList();
        }

        /// <summary>
        /// Packs a run, in its order, into a strip of the given width and the height of the usable area; null when it
        /// does not go in.
        /// </summary>
        private static Cluster Pack(int group, IReadOnlyList<Footprint> footprints, List<int> run, double spacing, double width,
            double usableHeight, double slack)
        {
            // Each box with the gap added on its right and above, into a strip the gap wider and higher: the boxes then
            // stand the gap apart, and flush with the strip's edges.
            var bin = new MaxRectsBin(width + spacing, usableHeight + spacing, slack);
            var corners = new (double X, double Y)[run.Count];
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < run.Count; i++)
            {
                Footprint box = footprints[run[i]];
                if (!bin.TryPlace(box.Width + spacing, box.Height + spacing, out double x, out double y))
                {
                    return null;
                }

                corners[i] = (x, y);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + box.Width);
                maxY = Math.Max(maxY, y + box.Height);
            }

            var offsets = corners.Select(c => (c.X - minX, c.Y - minY)).ToArray();
            return new Cluster(group, run.ToArray(), offsets, maxX - minX, maxY - minY, kept: false);
        }
    }
}
