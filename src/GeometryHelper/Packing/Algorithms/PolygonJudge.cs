using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Clipper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// Judges the packing of polygons as it came out, polygon by polygon, so that one reported placed is one that is:
    /// inside the usable area of its sheet, and clear of every other, the gap asked for kept, but those of a kept block
    /// it belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The packing is built to that, and this is the check that it held: it reads the polygons as moved, and knows nothing
    /// of the no-fit polygons the packing was made from. Two polygons stand clear when each, grown by half of the gap
    /// less half of the tolerance, leaves the other alone: they may overlap, or come nearer than the gap, by up to the
    /// tolerance and no more. Only polygons whose boxes, grown by half of the largest gap, meet are compared.
    /// </para>
    /// </remarks>
    internal static class PolygonJudge
    {
        private const double MiterLimit = 2.0;

        /// <summary>
        /// Finds the placed polygons that are not where a placed polygon has to be.
        /// </summary>
        /// <param name="spots">The placed polygons.</param>
        /// <param name="sheet">The sheet they were packed onto.</param>
        /// <param name="tolerance">How far a polygon may pass the edge of its area, or into another polygon, and still be clear.</param>
        /// <param name="spacing">The gap between two polygons of one block, in the units of the polygons.</param>
        /// <param name="groupSpacing">The gap between two blocks, in the units of the polygons.</param>
        /// <returns>For each spot, whether it is out of its area, or into another polygon or too near it.</returns>
        internal static bool[] FindFaults(
            IReadOnlyList<PolygonSpot> spots,
            Sheet sheet,
            Tolerance tolerance,
            double spacing,
            double groupSpacing
        )
        {
            double slack = tolerance.EqualPoint;
            var faults = new bool[spots.Count];
            var areas = new Dictionary<int, Footprint>();
            for (int s = 0; s < spots.Count; s++)
            {
                if (!areas.TryGetValue(spots[s].Sheet, out Footprint area))
                {
                    area = sheet.GetUsableArea(spots[s].Sheet);
                    areas.Add(spots[s].Sheet, area);
                }

                Footprint box = spots[s].Box;
                faults[s] = !(
                    box.MinX >= area.MinX - slack
                    && box.MinY >= area.MinY - slack
                    && box.MaxX <= area.MaxX + slack
                    && box.MaxY <= area.MaxY + slack
                );
            }

            double margin = Math.Max(spacing, groupSpacing);
            var cores = new Dictionary<int, Paths64[]>();
            foreach (IGrouping<int, int> onSheet in Enumerable.Range(0, spots.Count).GroupBy(s => spots[s].Sheet))
            {
                Footprint area = areas[onSheet.Key];
                double step = Math.Max(slack / 10.0, Math.Max(area.Width, area.Height) / 1e9);
                int[] items = onSheet.ToArray();
                bool alongX = Meets(items, spots, margin, true) <= Meets(items, spots, margin, false);
                int[] order = items
                    .OrderBy(s => alongX ? spots[s].Box.MinX : spots[s].Box.MinY)
                    .ThenBy(s => s)
                    .ToArray();
                for (int a = 0; a < order.Length; a++)
                {
                    PolygonSpot first = spots[order[a]];
                    double firstHigh = (alongX ? first.Box.MaxX : first.Box.MaxY) + margin;
                    for (int b = a + 1; b < order.Length; b++)
                    {
                        PolygonSpot second = spots[order[b]];
                        if ((alongX ? second.Box.MinX : second.Box.MinY) >= firstHigh)
                        {
                            break;
                        }

                        if (first.Kept && second.Cluster == first.Cluster)
                        {
                            continue;
                        }

                        bool otherMeet = alongX
                            ? first.Box.MinY < second.Box.MaxY + margin && second.Box.MinY < first.Box.MaxY + margin
                            : first.Box.MinX < second.Box.MaxX + margin && second.Box.MinX < first.Box.MaxX + margin;
                        if (!otherMeet)
                        {
                            continue;
                        }

                        int kind = second.Cluster == first.Cluster ? 0 : 1;
                        double gap = kind == 0 ? spacing : groupSpacing;
                        if (Overlap(order[a], order[b], kind, gap, spots, area, step, slack, cores))
                        {
                            faults[order[a]] = true;
                            faults[order[b]] = true;
                        }
                    }
                }
            }

            return faults;
        }

        /// <summary>
        /// Counts the pairs whose boxes meet along an axis, to sweep along the axis with the fewest.
        /// </summary>
        private static long Meets(int[] items, IReadOnlyList<PolygonSpot> spots, double margin, bool alongX)
        {
            double[] lows = items.Select(s => alongX ? spots[s].Box.MinX : spots[s].Box.MinY).ToArray();
            double[] highs = items.Select(s => (alongX ? spots[s].Box.MaxX : spots[s].Box.MaxY) + margin).ToArray();
            double[] sorted = (double[])lows.Clone();
            Array.Sort(sorted);
            long pairs = 0;
            for (int i = 0; i < lows.Length; i++)
            {
                pairs += Math.Max(0, Below(sorted, highs[i]) - Below(sorted, lows[i]) - 1);
            }

            return pairs;
        }

        // How many of the sorted values are below a value.
        private static int Below(double[] sorted, double value)
        {
            int low = 0,
                high = sorted.Length;
            while (low < high)
            {
                int middle = (low + high) >> 1;
                if (sorted[middle] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Whether two polygons overlap, or come nearer than the gap, by more than the tolerance.
        /// </summary>
        private static bool Overlap(
            int a,
            int b,
            int kind,
            double gap,
            IReadOnlyList<PolygonSpot> spots,
            Footprint area,
            double step,
            double slack,
            Dictionary<int, Paths64[]> cores
        )
        {
            Paths64 first = Core(a, kind, gap, spots, area, step, slack, cores);
            Paths64 second = Core(b, kind, gap, spots, area, step, slack, cores);
            if (first.Count == 0 || second.Count == 0)
            {
                return false;
            }

            return Clipper2.Intersect(first, second, FillRule.NonZero).Count > 0;
        }

        /// <summary>
        /// The polygon grown by half of the gap less half of the tolerance, less one step, on the grid of its sheet.
        /// </summary>
        private static Paths64 Core(
            int spot,
            int kind,
            double gap,
            IReadOnlyList<PolygonSpot> spots,
            Footprint area,
            double step,
            double slack,
            Dictionary<int, Paths64[]> cores
        )
        {
            if (!cores.TryGetValue(spot, out Paths64[] made))
            {
                made = new Paths64[2];
                cores.Add(spot, made);
            }

            if (made[kind] != null)
            {
                return made[kind];
            }

            GeoPolygon2 polygon = spots[spot].Polygon;
            var ring = new Path64(polygon.VertexCount);
            foreach (GeoPoint2 vertex in polygon.Vertices)
            {
                ring.Add(new Point64((vertex.X - area.MinX) / step, (vertex.Y - area.MinY) / step));
            }

            if (Clipper2.Area(ring) < 0.0)
            {
                ring.Reverse();
            }

            long grow = (long)Math.Round((gap - slack) * 0.5 / step) - 1;
            Paths64 core = new Paths64 { ring };
            if (grow != 0)
            {
                core = Clipper2.InflatePaths(core, grow, JoinType.Miter, EndType.Polygon, MiterLimit);
            }

            made[kind] = core;
            return core;
        }
    }
}
