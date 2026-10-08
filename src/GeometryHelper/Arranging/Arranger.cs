using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Places labels so that they overlap neither one another nor what they have to keep clear of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every label is tried at the same candidate positions around its leader, those
    /// <see cref="ArrangeItem.GetPlacePoints(ArrangeOptions)"/> lists, and the labels are placed greedily, one after
    /// another and never taken up again: by default those with the fewest free places first, and of two with as few the
    /// one nearer the middle of them all, each at the most open of its first few free places, and each kept clear of by
    /// every label after it.
    /// </para>
    /// <para>
    /// A run goes over the labels twice. The first pass places every label under every constraint. The labels it
    /// leaves overlapping something are tried once more with the block lines lifted, keeping clear of the labels the
    /// first pass placed. Whether each label is placed is then judged on the final layout as a whole.
    /// </para>
    /// <para>
    /// The items are only read, and a result comes back for each, in the order the items were given.
    /// </para>
    /// </remarks>
    public static class Arranger
    {
        /// <summary>
        /// Places the labels with the default options.
        /// </summary>
        /// <param name="items">
        /// The labels. A null entry is passed over, and its result is <c>default</c>: not moved, not placed.
        /// </param>
        /// <returns>What became of each label, in the order the labels were given.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
        public static ArrangeResult[] Run(IReadOnlyList<ArrangeItem> items) => Run(items, ArrangeOptions.Default);

        /// <summary>
        /// Places the labels.
        /// </summary>
        /// <param name="items">
        /// The labels. A null entry is passed over, and its result is <c>default</c>: not moved, not placed.
        /// </param>
        /// <param name="options">Where the candidate positions lie, the order the labels are placed in, and the tolerance.</param>
        /// <returns>What became of each label, in the order the labels were given.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="options"/> is null.</exception>
        public static ArrangeResult[] Run(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var algorithm = new GreedyAlgorithm();

            // --- PASS 1: every label, every constraint ---
            GeoVector2[] translations = algorithm.Arrange(items, options);
            bool[] placed = Judge(items, translations, options);

            // --- PASS 2: the labels left overlapping, once more, with the block lines lifted ---
            if (Relax(items, translations, placed, algorithm, options))
            {
                placed = Judge(items, translations, options);
            }

            var results = new ArrangeResult[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    results[i] = new ArrangeResult(translations[i], placed[i]);
                }
            }

            return results;
        }

        /// <summary>
        /// Tries the labels the first pass left overlapping once more, with their block lines lifted and the labels it
        /// placed standing as regions to keep clear of, and writes where they go into <paramref name="translations"/>.
        /// </summary>
        /// <returns>False when every label was placed, so that there was nothing to try again.</returns>
        /// <remarks>
        /// <para>
        /// The labels are tried as copies carrying the relaxed blocks. Lending the items themselves the relaxed blocks
        /// and handing their own back afterwards lost them for good when an item was listed twice, the second loan
        /// taking the first for the label's own, and showed the loan to anything reading the items meanwhile.
        /// </para>
        /// <para>
        /// Every copy carries the regions of every item, as every label keeps clear of them. The copies are arranged
        /// among themselves, so a region given only to an item the first pass placed was lost when each copy carried
        /// its own item's regions alone.
        /// </para>
        /// </remarks>
        private static bool Relax(IReadOnlyList<ArrangeItem> items, GeoVector2[] translations, bool[] placed,
            GreedyAlgorithm algorithm, ArrangeOptions options)
        {
            var failed = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !placed[i])
                {
                    failed.Add(i);
                }
            }

            if (failed.Count == 0)
            {
                return false;
            }

            var settled = new List<GeoPolygon2>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && placed[i] && TryGetRegion(items[i].Box.Translate(translations[i]), out GeoPolygon2 region))
                {
                    settled.Add(region);
                }
            }

            // Lifted: the block lines. Kept: the block polygons of every item, each once, and every label the first
            // pass placed. One list for every copy.
            var blocks = new List<GeoPolygon2>();
            var seen = new HashSet<GeoPolygon2>();
            foreach (ArrangeItem item in items)
            {
                if (item?.BlockPolygons == null)
                {
                    continue;
                }

                foreach (GeoPolygon2 polygon in item.BlockPolygons)
                {
                    if (polygon != null && seen.Add(polygon))
                    {
                        blocks.Add(polygon);
                    }
                }
            }

            blocks.AddRange(settled);

            var relaxed = new ArrangeItem[failed.Count];
            for (int k = 0; k < failed.Count; k++)
            {
                relaxed[k] = items[failed[k]].WithBlocks(blocks, Array.Empty<GeoLine2>());
            }

            GeoVector2[] second = algorithm.Arrange(relaxed, options);
            for (int k = 0; k < failed.Count; k++)
            {
                translations[failed[k]] = second[k];
            }

            return true;
        }

        /// <summary>
        /// Makes the region a placed label takes up, for the second pass to keep clear of.
        /// </summary>
        /// <returns>
        /// False for a box two of whose corners run together, which makes no region: one moved so far off that the
        /// numbers no longer tell its corners apart, as a gap as wide as a number goes takes it, or one smaller than
        /// the global tolerance of points. Judging the final layout still sees it.
        /// </returns>
        private static bool TryGetRegion(GeoRectangle2 box, out GeoPolygon2 region)
        {
            GeoPoint2[] corners = box.GetVertices();
            for (int i = 0; i < corners.Length; i++)
            {
                // A polygon drops each corner that falls on the one before it, the first coming after the last: a box
                // that loses one is no box.
                if (corners[i].IsEqualTo(corners[(i + 1) % corners.Length]))
                {
                    region = null;
                    return false;
                }
            }

            region = new GeoPolygon2(corners);
            return true;
        }

        /// <summary>
        /// Judges, on the final layout, whether each label overlaps nothing.
        /// <para>
        /// A pass only knows the layout at the moment it places a label, so what it knows means "this spot was
        /// clear when my turn came". A label placed later, when stuck, may fall back onto one placed earlier, which
        /// would still believe itself clear. What the caller needs to know is whether the final layout has overlaps,
        /// so that is judged here, after every label has settled.
        /// </para>
        /// </summary>
        /// <returns>For each item, whether it is placed; false for a null entry.</returns>
        private static bool[] Judge(IReadOnlyList<ArrangeItem> items, GeoVector2[] translations, ArrangeOptions options)
        {
            var blocks = new ObstacleSpatialIndex(Obstacle.CollectStatic(items));

            // The labels in an index of their own, added in the order of the items, itemOf giving the item of each. The
            // box the index holds for a label is Bounds.Of its box, the very bounds the label has below.
            var labels = new ObstacleSpatialIndex();
            var itemOf = new List<int>();
            var boxes = new GeoRectangle2[items.Count];
            var bounds = new Bounds[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                boxes[i] = items[i].Box.Translate(translations[i]);
                bounds[i] = Bounds.Of(boxes[i]);
                labels.Add(new Obstacle(boxes[i]));
                itemOf.Add(i);
            }

            var placed = new bool[items.Count];
            var near = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                // A label that cannot form a layout was never arranged. That it happens to overlap nothing does not
                // make it placed.
                if (items[i] == null || !items[i].TryGetLayout(options, out _))
                {
                    continue;
                }

                // AnyCollides passes over every block whose box does not overlap that of the label, so the blocks the
                // index finds for it are all it would look at, in the same order.
                bool clear = !Obstacle.AnyCollides(blocks.Overlapping(bounds[i]), boxes[i], options.Tolerance);

                // Only the labels whose boxes overlap, bounds[i].Overlaps(bounds[j]), are judged against this one, in
                // the order of the items. Two boxes standing apart by less than the tolerance are not judged, as before:
                // CollidesWith, within the tolerance, would call them a collision.
                labels.Query(bounds[i], near);
                for (int k = 0; k < near.Count && clear; k++)
                {
                    int j = itemOf[near[k]];
                    if (i == j)
                    {
                        continue;
                    }

                    if (boxes[i].CollidesWith(boxes[j], options.Tolerance))
                    {
                        clear = false;
                    }
                }

                placed[i] = clear;
            }

            return placed;
        }
    }
}
