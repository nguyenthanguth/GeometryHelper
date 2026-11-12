using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// Packs polygons onto sheets by their own shape, as <see cref="SheetPacker"/> packs boxes by theirs: each group laid
    /// out as a block, the blocks placed one after another where their top is highest and then their left side furthest
    /// left, and a new sheet begun when the last is full.
    /// </summary>
    internal static class PolygonPacker
    {
        /// <summary>
        /// Refuses a polygon that cannot be packed: not there, not at finite coordinates, enclosing no area, or crossing
        /// itself.
        /// </summary>
        /// <exception cref="ArgumentException">A polygon is one of those.</exception>
        internal static void Validate(IReadOnlyList<GeoPolygon2[]> groups, Tolerance tolerance)
        {
            for (int g = 0; g < groups.Count; g++)
            {
                GeoPolygon2[] polygons = groups[g];
                if (polygons == null)
                {
                    continue;
                }

                for (int i = 0; i < polygons.Length; i++)
                {
                    GeoPolygon2 polygon = polygons[i];
                    string name = $"Polygon {i} of group {g}";
                    Guard.Argument(polygon != null, nameof(groups), name + " is null.");
                    Guard.Argument(
                        Guard.AreValid(polygon.Vertices),
                        nameof(groups),
                        name + " has a coordinate that is not a finite number."
                    );
                    Guard.Argument(
                        polygon.Area > tolerance.EqualPoint * tolerance.EqualPoint,
                        nameof(groups),
                        name + " encloses no area."
                    );
                    Guard.Argument(
                        polygon.IsSimple(tolerance),
                        nameof(groups),
                        name + " crosses itself: only a polygon that does not can be packed."
                    );
                }
            }
        }

        /// <summary>
        /// Packs the polygons; the arguments are those of <see cref="SheetPacker"/> and have been checked.
        /// </summary>
        internal static PackResult Run(IReadOnlyList<GeoPolygon2[]> groups, Sheet sheet, PackOptions options)
        {
            Tolerance tolerance = options.Tolerance;
            double slack = tolerance.EqualPoint;
            double spacing = options.Spacing * sheet.Scale;
            double groupSpacing = options.GroupSpacing * sheet.Scale;
            Footprint usable = sheet.GetUsableArea(0);

            // One step of the grid every shape is worked out on: a tenth of the tolerance, or less than a thousand
            // millionth of the sheet's side where the tolerance is finer than that.
            double step = Math.Max(slack / 10.0, Math.Max(usable.Width, usable.Height) / 1e9);
            long usableWidth = (long)Math.Round(usable.Width / step),
                usableHeight = (long)Math.Round(usable.Height / step);

            // A packing from another corner than the upper left is the packing from the upper left of the shapes
            // mirrored across the usable area, its result mirrored back.
            bool fromRight =
                options.StartCorner == SheetCorner.UpperRight || options.StartCorner == SheetCorner.LowerRight;
            bool fromBelow =
                options.StartCorner == SheetCorner.LowerLeft || options.StartCorner == SheetCorner.LowerRight;
            double signX = fromRight ? -1.0 : 1.0,
                signY = fromBelow ? -1.0 : 1.0;

            // STEP 1: Every polygon not placed until it is; the blocks of each group from those that fit a sheet at all.
            int total = groups.Sum(g => g == null ? 0 : g.Length);
            var context = new PolygonContext(step, tolerance, spacing, groupSpacing, total);
            var placements = new PackPlacement[groups.Count][];
            var clusters = new List<PolygonCluster>();
            int id = 0;
            for (int g = 0; g < groups.Count; g++)
            {
                GeoPolygon2[] polygons = groups[g] ?? Array.Empty<GeoPolygon2>();
                placements[g] = new PackPlacement[polygons.Length];
                var fitting = new List<PolygonShape>();
                for (int i = 0; i < polygons.Length; i++, id++)
                {
                    Footprint box = Footprint.Of(polygons[i]);
                    placements[g][i] = NotPlaced(polygons[i]);
                    if (box.Width <= usable.Width + slack * 0.5 && box.Height <= usable.Height + slack * 0.5)
                    {
                        fitting.Add(new PolygonShape(id, g, i, polygons[i], signX, signY, step));
                    }
                }

                clusters.AddRange(
                    PolygonClusterBuilder.Build(
                        g,
                        fitting,
                        options.GroupLayout,
                        context,
                        usableWidth,
                        usableHeight,
                        (long)Math.Round(spacing / step)
                    )
                );
            }

            // STEP 2: The blocks in their order, or largest first; the sort keeps the order of blocks of one size.
            IEnumerable<PolygonCluster> order = options.LargestGroupsFirst
                ? clusters.OrderByDescending(c => c.Area)
                : (IEnumerable<PolygonCluster>)clusters;

            // STEP 3: Each block onto the last sheet, or any sheet with room, or a new one.
            var bins = new List<PolygonBin>();
            var spots = new List<PolygonSpot>();
            int clusterIndex = 0;
            foreach (PolygonCluster cluster in order)
            {
                int onSheet = Place(
                    bins,
                    cluster.Body,
                    options.FillEarlierSheets,
                    context,
                    usableWidth,
                    usableHeight,
                    out long x,
                    out long y
                );
                if (onSheet >= 0)
                {
                    Footprint area = sheet.GetUsableArea(onSheet);
                    for (int j = 0; j < cluster.Body.Shapes.Length; j++)
                    {
                        PolygonShape shape = cluster.Body.Shapes[j];
                        double across = (x + cluster.Body.OffsetX[j]) * step,
                            up = (y + cluster.Body.OffsetY[j]) * step;
                        double left = fromRight ? area.MinX + usable.Width - across - shape.WidthD : area.MinX + across;
                        double low = fromBelow ? area.MinY + usable.Height - up - shape.HeightD : area.MinY + up;
                        var translation = new GeoVector2(left - shape.OriginalMinX, low - shape.OriginalMinY);
                        GeoPolygon2 moved = shape.Source.Translate(translation);
                        placements[shape.Group][shape.Member] = new PackPlacement(
                            onSheet,
                            translation,
                            BoxOf(moved),
                            moved,
                            true
                        );
                        spots.Add(
                            new PolygonSpot(shape.Group, shape.Member, onSheet, clusterIndex, cluster.Kept, moved)
                        );
                    }
                }

                clusterIndex++;
            }

            // STEP 4: Judge the polygons as they came out; one that is not where a placed polygon has to be is not placed.
            bool[] faults = PolygonJudge.FindFaults(spots, sheet, tolerance, spacing, groupSpacing);
            for (int s = 0; s < spots.Count; s++)
            {
                if (faults[s])
                {
                    placements[spots[s].Group][spots[s].Member] = NotPlaced(groups[spots[s].Group][spots[s].Member]);
                }
            }

            // STEP 5: The sheets taken, the first always, and how much of them the placed polygons fill.
            int sheetCount = Math.Max(1, bins.Count);
            SheetFrame[] frames = Enumerable.Range(0, sheetCount).Select(sheet.GetFrame).ToArray();
            double filled = 0.0;
            for (int g = 0; g < groups.Count; g++)
            {
                for (int i = 0; i < placements[g].Length; i++)
                {
                    if (placements[g][i].Placed)
                    {
                        filled += groups[g][i].Area;
                    }
                }
            }

            return new PackResult(placements, frames, filled / (sheetCount * usable.Width * usable.Height));
        }

        /// <summary>
        /// Puts a block onto the last sheet, or onto the first with room when earlier sheets may be filled, or onto a new
        /// sheet; the sheet it went on, or -1 when it went on none, as a block that fits an empty sheet cannot.
        /// </summary>
        private static int Place(
            List<PolygonBin> bins,
            PolygonBody body,
            bool fillEarlierSheets,
            PolygonContext context,
            long width,
            long height,
            out long x,
            out long y
        )
        {
            for (int k = fillEarlierSheets ? 0 : Math.Max(0, bins.Count - 1); k < bins.Count; k++)
            {
                if (bins[k].TryPlace(body, out x, out y))
                {
                    return k;
                }
            }

            var bin = new PolygonBin(context, PolygonContext.Group, width, height);
            if (!bin.TryPlace(body, out x, out y))
            {
                return -1;
            }

            bins.Add(bin);
            return bins.Count - 1;
        }

        private static GeoRectangle2 BoxOf(GeoPolygon2 polygon)
        {
            Footprint box = Footprint.Of(polygon);
            return new GeoRectangle2(box.MinX, box.MinY, box.Width, box.Height);
        }

        private static PackPlacement NotPlaced(GeoPolygon2 polygon) =>
            new PackPlacement(-1, GeoVector2.Zero, BoxOf(polygon), polygon, false);
    }
}
