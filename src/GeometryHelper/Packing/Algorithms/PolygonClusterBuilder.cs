using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// A group of polygons laid out as one block, which is packed onto a sheet whole.
    /// </summary>
    internal sealed class PolygonCluster
    {
        /// <summary>
        /// Initializes a block.
        /// </summary>
        /// <param name="group">Which group of the input the polygons belong to.</param>
        /// <param name="body">The polygons, each at its place in the block.</param>
        /// <param name="kept">Whether the polygons stand as they were given, so that they may overlap one another.</param>
        internal PolygonCluster(int group, PolygonBody body, bool kept)
        {
            Group = group;
            Body = body;
            Kept = kept;
            Area = body.Shapes.Sum(s => s.Area);
        }

        /// <summary>Gets which group of the input the polygons belong to.</summary>
        internal int Group { get; }

        /// <summary>Gets the polygons, each at its place in the block.</summary>
        internal PolygonBody Body { get; }

        /// <summary>Gets whether the polygons stand as they were given, so that they may overlap one another.</summary>
        internal bool Kept { get; }

        /// <summary>Gets the area the polygons cover, summed.</summary>
        internal double Area { get; }
    }

    /// <summary>
    /// Lays the polygons of a group out as blocks, each of which goes onto one sheet whole, as
    /// <see cref="ClusterBuilder"/> does for boxes.
    /// </summary>
    internal static class PolygonClusterBuilder
    {
        /// <summary>
        /// The most widths a compact block is tried at: enough to find a block near square, few enough that a group of
        /// a thousand polygons costs no more than a few packings of it.
        /// </summary>
        private const int MostWidths = 16;

        /// <summary>
        /// Lays out the polygons of a group that fit a sheet at all.
        /// </summary>
        /// <param name="group">Which group of the input it is.</param>
        /// <param name="shapes">The polygons to lay out, in their order; each fits the usable area.</param>
        /// <param name="layout">Whether to lay them out afresh or keep them as they stand.</param>
        /// <param name="context">What the packing shares.</param>
        /// <param name="usableWidth">The width of the usable area of a sheet, in steps.</param>
        /// <param name="usableHeight">The height of the usable area of a sheet, in steps.</param>
        /// <param name="spacing">The gap between two polygons of the group, in steps.</param>
        /// <returns>
        /// One block, or, for a group too large for one sheet, as many as it takes, each holding the polygons that follow
        /// on from the one before.
        /// </returns>
        internal static List<PolygonCluster> Build(
            int group,
            IReadOnlyList<PolygonShape> shapes,
            GroupLayout layout,
            PolygonContext context,
            long usableWidth,
            long usableHeight,
            long spacing
        )
        {
            var clusters = new List<PolygonCluster>();
            if (shapes.Count == 0)
            {
                return clusters;
            }

            if (layout == GroupLayout.Keep)
            {
                PolygonCluster kept = Keep(group, shapes, context, usableWidth, usableHeight);
                if (kept != null)
                {
                    clusters.Add(kept);
                    return clusters;
                }
            }

            PolygonCluster whole = Compact(group, shapes, context, usableWidth, usableHeight, spacing);
            if (whole != null)
            {
                clusters.Add(whole);
                return clusters;
            }

            foreach (List<PolygonShape> run in Runs(shapes, context, usableWidth, usableHeight))
            {
                PolygonCluster cluster = Compact(group, run, context, usableWidth, usableHeight, spacing);
                if (cluster != null)
                {
                    clusters.Add(cluster);
                }
            }

            return clusters;
        }

        /// <summary>
        /// The polygons as they stand, as one block; null when the block is too large for one sheet.
        /// </summary>
        private static PolygonCluster Keep(
            int group,
            IReadOnlyList<PolygonShape> shapes,
            PolygonContext context,
            long usableWidth,
            long usableHeight
        )
        {
            double minX = shapes.Min(s => s.MirrorMinX),
                minY = shapes.Min(s => s.MirrorMinY);
            var offsetX = new long[shapes.Count];
            var offsetY = new long[shapes.Count];
            long width = 0,
                height = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                offsetX[i] = (long)Math.Round((shapes[i].MirrorMinX - minX) / context.Step);
                offsetY[i] = (long)Math.Round((shapes[i].MirrorMinY - minY) / context.Step);
                width = Math.Max(width, offsetX[i] + shapes[i].Width);
                height = Math.Max(height, offsetY[i] + shapes[i].Height);
            }

            if (width > usableWidth + context.FitSlack || height > usableHeight + context.FitSlack)
            {
                return null;
            }

            return new PolygonCluster(group, new PolygonBody(shapes.ToArray(), offsetX, offsetY, width, height), true);
        }

        /// <summary>
        /// Splits the polygons, in their order, into runs that each go onto one sheet: a run ends at the first polygon that
        /// no longer fits beside those before it.
        /// </summary>
        private static List<List<PolygonShape>> Runs(
            IReadOnlyList<PolygonShape> shapes,
            PolygonContext context,
            long usableWidth,
            long usableHeight
        )
        {
            var runs = new List<List<PolygonShape>>();
            var run = new List<PolygonShape>();
            var bin = new PolygonBin(context, PolygonContext.Spacing, usableWidth, usableHeight);
            foreach (PolygonShape shape in shapes)
            {
                PolygonBody body = PolygonBody.Of(shape);
                if (!bin.TryPlace(body, out _, out _))
                {
                    if (run.Count > 0)
                    {
                        runs.Add(run);
                        run = new List<PolygonShape>();
                    }

                    // Each polygon fits an empty sheet, so it starts the next run; one that did not would be left out, and
                    // reported not placed.
                    bin = new PolygonBin(context, PolygonContext.Spacing, usableWidth, usableHeight);
                    if (!bin.TryPlace(body, out _, out _))
                    {
                        continue;
                    }
                }

                run.Add(shape);
            }

            if (run.Count > 0)
            {
                runs.Add(run);
            }

            return runs;
        }

        /// <summary>
        /// Lays a run of polygons out as close together as they go: packed in their order, from the upper left, into strips
        /// of several widths, the block of least perimeter kept, then of least area, then the narrowest. Null when the run
        /// goes onto no strip.
        /// </summary>
        private static PolygonCluster Compact(
            int group,
            IReadOnlyList<PolygonShape> run,
            PolygonContext context,
            long usableWidth,
            long usableHeight,
            long spacing
        )
        {
            if (run.Count == 1)
            {
                return new PolygonCluster(group, PolygonBody.Of(run[0]), false);
            }

            PolygonBody best = null;
            foreach (long width in Widths(run, spacing, usableWidth))
            {
                PolygonBody tried = Pack(run, context, width, usableHeight);
                if (tried == null)
                {
                    continue;
                }

                if (
                    best == null
                    || tried.Width + tried.Height < best.Width + best.Height
                    || (
                        tried.Width + tried.Height == best.Width + best.Height
                        && tried.Width * tried.Height < best.Width * best.Height
                    )
                )
                {
                    best = tried;
                }
            }

            return best == null ? null : new PolygonCluster(group, best, false);
        }

        /// <summary>
        /// The widths a run is tried at, narrowest first: the widest polygon alone, each row the polygons make from the
        /// first on, and the usable width, no more than <see cref="MostWidths"/> of them.
        /// </summary>
        private static List<long> Widths(IReadOnlyList<PolygonShape> run, long spacing, long usableWidth)
        {
            long widest = run.Max(s => s.Width);
            var rows = new List<long>();
            long row = 0;
            for (int i = 0; i < run.Count; i++)
            {
                row += run[i].Width + (i > 0 ? spacing : 0L);
                if (row > widest)
                {
                    rows.Add(Math.Min(row, usableWidth));
                }
            }

            var widths = new List<long> { Math.Min(widest, usableWidth) };
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
        private static PolygonBody Pack(
            IReadOnlyList<PolygonShape> run,
            PolygonContext context,
            long width,
            long height
        )
        {
            var bin = new PolygonBin(context, PolygonContext.Spacing, width, height);
            var xs = new long[run.Count];
            var ys = new long[run.Count];
            long minX = long.MaxValue,
                minY = long.MaxValue;
            long maxX = long.MinValue,
                maxY = long.MinValue;
            for (int i = 0; i < run.Count; i++)
            {
                if (!bin.TryPlace(PolygonBody.Of(run[i]), out xs[i], out ys[i]))
                {
                    return null;
                }

                minX = Math.Min(minX, xs[i]);
                minY = Math.Min(minY, ys[i]);
                maxX = Math.Max(maxX, xs[i] + run[i].Width);
                maxY = Math.Max(maxY, ys[i] + run[i].Height);
            }

            for (int i = 0; i < run.Count; i++)
            {
                xs[i] -= minX;
                ys[i] -= minY;
            }

            return new PolygonBody(run.ToArray(), xs, ys, maxX - minX, maxY - minY);
        }
    }
}
