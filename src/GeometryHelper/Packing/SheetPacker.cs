using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing.Algorithms;

namespace GeometryHelper.Packing
{
    /// <summary>
    /// Packs boxes, or polygons, onto sheets of paper: each group as one block, the blocks from the upper left of the
    /// first sheet, or the corner <see cref="PackOptions.StartCorner"/> names, and onto a new sheet beside it when the
    /// one before is full.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A view cut to a polygon (by VPCLIP in AutoCAD, say) is packed by that polygon: the overloads that take
    /// <see cref="GeoPolygon2"/> lay it out by its true shape, so that an L- or U-shaped view lets another stand in its
    /// notch. What follows is about boxes; the polygons are placed by the same rule, highest top and then furthest left.
    /// </para>
    /// <para>
    /// The boxes are only moved, never turned or resized. A box that is turned is packed by the upright box round it,
    /// and keeps its turn.
    /// </para>
    /// <para>
    /// The boxes of a group are laid out together first, as <see cref="PackOptions.GroupLayout"/> says, and the block
    /// they make goes onto a sheet whole: the groups keep their boxes together, at <see cref="PackOptions.Spacing"/>
    /// from one another, and <see cref="PackOptions.GroupSpacing"/> from other groups. A group too large for one sheet
    /// is split, in its order, into blocks that each go onto one. The blocks are placed by the maximal rectangles
    /// method, each where its top is highest and then its left side furthest left, so that a sheet fills from its
    /// upper left corner as the eye reads it. From another <see cref="PackOptions.StartCorner"/> the packing is the
    /// mirror image of that one, the same boxes on the same sheets.
    /// </para>
    /// <para>
    /// The result is judged as it came out: a box reported <see cref="PackPlacement.Placed"/> lies inside the usable
    /// area of its sheet and clear of every other box, but the boxes of its own group kept as they stood. The same
    /// boxes and options give the same result, and the boxes, the sheet and the options are only read.
    /// </para>
    /// </remarks>
    public static class SheetPacker
    {
        /// <summary>
        /// Packs the boxes onto sheets like the one given, with the default options.
        /// </summary>
        /// <param name="groups">The boxes, in groups of those that belong together. A null group is passed over.</param>
        /// <param name="sheet">The sheet: its size, scale, offsets, where it lies, and where each new one goes.</param>
        /// <returns>Where each box goes, in the shape the boxes were given, and the sheets they took.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="groups"/> or <paramref name="sheet"/> is null.</exception>
        /// <exception cref="ArgumentException">The offsets of the sheet leave it no room.</exception>
        public static PackResult Pack(IReadOnlyList<GeoRectangle2[]> groups, Sheet sheet) =>
            Pack(groups, sheet, PackOptions.Default);

        /// <summary>
        /// Packs the boxes onto sheets like the one given.
        /// </summary>
        /// <param name="groups">The boxes, in groups of those that belong together. A null group is passed over.</param>
        /// <param name="sheet">The sheet: its size, scale, offsets, where it lies, and where each new one goes.</param>
        /// <param name="options">The gaps, how a group is laid out, the order the groups go in, and the tolerance.</param>
        /// <returns>Where each box goes, in the shape the boxes were given, and the sheets they took.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="groups"/>, <paramref name="sheet"/> or <paramref name="options"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">The offsets of the sheet leave it no room.</exception>
        public static PackResult Pack(IReadOnlyList<GeoRectangle2[]> groups, Sheet sheet, PackOptions options)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }

            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!sheet.HasRoom)
            {
                throw new ArgumentException(sheet.NoRoomMessage, nameof(sheet));
            }

            double slack = options.Tolerance.EqualPoint;
            double spacing = options.Spacing * sheet.Scale;
            double groupSpacing = options.GroupSpacing * sheet.Scale;
            Footprint usable = sheet.GetUsableArea(0);

            // STEP 1: Every box not placed until it is; the blocks of each group from those that fit a sheet at all.
            var placements = new PackPlacement[groups.Count][];
            var footprints = new Footprint[groups.Count][];
            var clusters = new List<Cluster>();
            for (int g = 0; g < groups.Count; g++)
            {
                GeoRectangle2[] boxes = groups[g] ?? Array.Empty<GeoRectangle2>();
                placements[g] = new PackPlacement[boxes.Length];
                footprints[g] = new Footprint[boxes.Length];

                var fitting = new List<int>();
                for (int i = 0; i < boxes.Length; i++)
                {
                    Footprint box = Footprint.Of(boxes[i]);
                    footprints[g][i] = box;
                    placements[g][i] = NotPlaced(boxes[i]);
                    if (box.IsFinite && box.Width <= usable.Width + slack && box.Height <= usable.Height + slack)
                    {
                        fitting.Add(i);
                    }
                }

                clusters.AddRange(
                    ClusterBuilder.Build(
                        g,
                        footprints[g],
                        fitting,
                        options.GroupLayout,
                        spacing,
                        usable.Width,
                        usable.Height,
                        slack
                    )
                );
            }

            // STEP 2: The blocks in their order, or largest first; the sort keeps the order of blocks of one size.
            IEnumerable<Cluster> order = options.LargestGroupsFirst
                ? clusters.OrderByDescending(c => c.Area)
                : (IEnumerable<Cluster>)clusters;

            // STEP 3: Each block onto the last sheet, or any sheet with room, or a new one. Each block with the group
            // spacing added on its right and above, into sheets the spacing wider and higher: the blocks then stand the
            // spacing apart, and flush with the edges of the usable area. From another corner than the upper left, each
            // block and each box of a block laid out afresh is mirrored across the usable area and its block.
            bool fromRight =
                options.StartCorner == SheetCorner.UpperRight || options.StartCorner == SheetCorner.LowerRight;
            bool fromBelow =
                options.StartCorner == SheetCorner.LowerLeft || options.StartCorner == SheetCorner.LowerRight;
            var bins = new List<MaxRectsBin>();
            var spots = new List<Spot>();
            int clusterIndex = 0;
            foreach (Cluster cluster in order)
            {
                int onSheet = Place(
                    bins,
                    cluster.Width + groupSpacing,
                    cluster.Height + groupSpacing,
                    options.FillEarlierSheets,
                    usable.Width + groupSpacing,
                    usable.Height + groupSpacing,
                    slack,
                    out double x,
                    out double y
                );
                if (onSheet >= 0)
                {
                    Footprint area = sheet.GetUsableArea(onSheet);
                    double blockX = fromRight ? usable.Width - x - cluster.Width : x;
                    double blockY = fromBelow ? usable.Height - y - cluster.Height : y;
                    for (int j = 0; j < cluster.Members.Count; j++)
                    {
                        int g = cluster.Group,
                            i = cluster.Members[j];
                        Footprint box = footprints[g][i];
                        double offsetX =
                            fromRight && !cluster.Kept
                                ? cluster.Width - cluster.Offsets[j].X - box.Width
                                : cluster.Offsets[j].X;
                        double offsetY =
                            fromBelow && !cluster.Kept
                                ? cluster.Height - cluster.Offsets[j].Y - box.Height
                                : cluster.Offsets[j].Y;
                        var translation = new GeoVector2(
                            area.MinX + blockX + offsetX - box.MinX,
                            area.MinY + blockY + offsetY - box.MinY
                        );
                        GeoRectangle2 moved = groups[g][i].Translate(translation);
                        placements[g][i] = new PackPlacement(onSheet, translation, moved, true);
                        spots.Add(new Spot(g, i, onSheet, clusterIndex, cluster.Kept, Footprint.Of(moved)));
                    }
                }

                clusterIndex++;
            }

            // STEP 4: Judge the boxes as they came out; one that is not where a placed box has to be is not placed.
            bool[] faults = PackJudge.FindFaults(spots, sheet, slack);
            for (int s = 0; s < spots.Count; s++)
            {
                if (faults[s])
                {
                    placements[spots[s].Group][spots[s].Member] = NotPlaced(groups[spots[s].Group][spots[s].Member]);
                }
            }

            // STEP 5: The sheets taken, the first always, and how much of them the placed boxes fill.
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
            List<MaxRectsBin> bins,
            double width,
            double height,
            bool fillEarlierSheets,
            double binWidth,
            double binHeight,
            double slack,
            out double x,
            out double y
        )
        {
            for (int k = fillEarlierSheets ? 0 : Math.Max(0, bins.Count - 1); k < bins.Count; k++)
            {
                if (bins[k].TryPlace(width, height, out x, out y))
                {
                    return k;
                }
            }

            var bin = new MaxRectsBin(binWidth, binHeight, slack);
            if (!bin.TryPlace(width, height, out x, out y))
            {
                return -1;
            }

            bins.Add(bin);
            return bins.Count - 1;
        }

        /// <summary>
        /// Packs the polygons onto sheets like the one given, by their own shape, with the default options.
        /// </summary>
        /// <param name="groups">
        /// The polygons, in groups of those that belong together: views cut to a polygon, an L or a U among them. A null
        /// group is passed over.
        /// </param>
        /// <param name="sheet">The sheet: its size, scale, offsets, where it lies, and where each new one goes.</param>
        /// <returns>
        /// Where each polygon goes, in the shape the polygons were given, and the sheets they took.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="groups"/> or <paramref name="sheet"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The offsets of the sheet leave it no room; or a polygon is null, not at finite coordinates, encloses no area, or
        /// crosses itself.
        /// </exception>
        /// <remarks>See <see cref="Pack(IReadOnlyList{GeoPolygon2[]}, Sheet, PackOptions)"/>.</remarks>
        public static PackResult Pack(IReadOnlyList<GeoPolygon2[]> groups, Sheet sheet) =>
            Pack(groups, sheet, PackOptions.Default);

        /// <summary>
        /// Packs the polygons onto sheets like the one given, by their own shape: a polygon with a notch lets another
        /// stand in it.
        /// </summary>
        /// <param name="groups">
        /// The polygons, in groups of those that belong together: views cut to a polygon, an L or a U among them. A null
        /// group is passed over.
        /// </param>
        /// <param name="sheet">The sheet: its size, scale, offsets, where it lies, and where each new one goes.</param>
        /// <param name="options">The gaps, how a group is laid out, the order the groups go in, and the tolerance.</param>
        /// <returns>
        /// Where each polygon goes, in the shape the polygons were given, and the sheets they took:
        /// <see cref="PackPlacement.ViewPolygon"/> is the polygon moved and <see cref="PackPlacement.ViewBox"/> the box
        /// round it.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The polygons are only moved, never turned or resized, and they are packed by their true shape, not by the box
        /// round it. The places a polygon may stand are those where it stays inside the usable area of the sheet and
        /// clear of the polygons already there, found from the no-fit polygon of each pair, so that an L-shaped view lets
        /// a small one stand in its notch. Each is put where its top is highest and then its left side furthest left, as
        /// <see cref="Pack(IReadOnlyList{GeoRectangle2[]}, Sheet, PackOptions)"/> puts a box. From another
        /// <see cref="PackOptions.StartCorner"/> the packing is the mirror image of that one, the polygons as they are
        /// given taken to the mirror image of the places: the same views, mirrored across the sheet, would pack the same.
        /// </para>
        /// <para>
        /// <see cref="PackOptions.Spacing"/> is the least gap between two polygons of one group and
        /// <see cref="PackOptions.GroupSpacing"/> between two groups, measured between the polygons themselves, not their
        /// boxes. A group laid out afresh (<see cref="GroupLayout.Compact"/>) is packed together first, in its order from
        /// the start corner, as close together as it goes, and goes onto the sheet as one block; a group kept as drawn
        /// (<see cref="GroupLayout.Keep"/>) moves as one block with its polygons as they stand, overlapping or not. A
        /// block is placed by the shape of each of its polygons, so that polygons of another group may stand between
        /// them, in a notch, as long as the group spacing is kept. A group too large for one sheet is split, in its
        /// order, into blocks that each go onto one.
        /// </para>
        /// <para>
        /// A polygon larger than the usable area of a sheet is not placed. The result is judged as it came out: a polygon
        /// reported <see cref="PackPlacement.Placed"/> lies inside the usable area of its sheet and clear of every
        /// other, the gaps kept, but the polygons of a kept group; the tolerance of the options is how far it may pass
        /// either. The same polygons and options give the same result, and the polygons, the sheet and the options are
        /// only read. A polygon takes time with the number of polygons already on its sheet and the corners of each:
        /// 200 polygons of up to a fifth of an A1 take a few seconds.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="groups"/>, <paramref name="sheet"/> or <paramref name="options"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// The offsets of the sheet leave it no room; or a polygon is null, not at finite coordinates, encloses no area
        /// (its area is no more than the tolerance squared), or crosses itself.
        /// </exception>
        public static PackResult Pack(IReadOnlyList<GeoPolygon2[]> groups, Sheet sheet, PackOptions options)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }

            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!sheet.HasRoom)
            {
                throw new ArgumentException(sheet.NoRoomMessage, nameof(sheet));
            }

            PolygonPacker.Validate(groups, options.Tolerance);
            return PolygonPacker.Run(groups, sheet, options);
        }

        private static PackPlacement NotPlaced(GeoRectangle2 box) => new PackPlacement(-1, GeoVector2.Zero, box, false);
    }
}
