using System;
using System.Collections.Generic;
using GeometryHelper.Arranging.Algorithms;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Places labels so that they overlap neither one another nor what they have to keep clear of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every label is tried at the same candidate positions around its leader, those
    /// <see cref="ArrangeItem.GetPlacePoints(ArrangeOptions)"/> lists; the algorithm
    /// <see cref="ArrangeOptions.Algorithm"/> names decides which of them each label takes.
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
        /// <param name="options">The algorithm, where the candidate positions lie, and the tolerance.</param>
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

            IArrangeAlgorithm algorithm = Choose(options.Algorithm);

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
        /// Makes the algorithm <paramref name="algorithm"/> names; the greedy one for a value it does not know.
        /// </summary>
        private static IArrangeAlgorithm Choose(ArrangeAlgorithmType algorithm)
        {
            switch (algorithm)
            {
                case ArrangeAlgorithmType.BoundedBacktracking:
                    return new BoundedBacktrackingAlgorithm();
                case ArrangeAlgorithmType.SimulatedAnnealing:
                    return new SimulatedAnnealingAlgorithm();
                case ArrangeAlgorithmType.ForceDirected:
                    return new ForceDirectedAlgorithm();
                case ArrangeAlgorithmType.ConstraintSatisfaction:
                    return new ConstraintSatisfactionAlgorithm();
                case ArrangeAlgorithmType.Greedy:
                default:
                    return new GreedyAlgorithm();
            }
        }

        /// <summary>
        /// Tries the labels the first pass left overlapping once more, with their block lines lifted and the labels it
        /// placed standing as regions to keep clear of, and writes where they go into <paramref name="translations"/>.
        /// </summary>
        /// <returns>False when every label was placed, so that there was nothing to try again.</returns>
        /// <remarks>
        /// The labels are tried as copies carrying the relaxed blocks. Lending the items themselves the relaxed blocks
        /// and handing their own back afterwards lost them for good when an item was listed twice, the second loan
        /// taking the first for the label's own, and showed the loan to anything reading the items meanwhile.
        /// </remarks>
        private static bool Relax(IReadOnlyList<ArrangeItem> items, GeoVector2[] translations, bool[] placed,
            IArrangeAlgorithm algorithm, ArrangeOptions options)
        {
            var failed = new List<int>();
            var settled = new List<GeoPolygon2>();

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                {
                    continue;
                }

                if (placed[i])
                {
                    settled.Add(new GeoPolygon2(items[i].Box.Translate(translations[i]).GetVertices()));
                }
                else
                {
                    failed.Add(i);
                }
            }

            if (failed.Count == 0)
            {
                return false;
            }

            var relaxed = new ArrangeItem[failed.Count];
            for (int k = 0; k < failed.Count; k++)
            {
                ArrangeItem item = items[failed[k]];

                // Lifted: the block lines. Kept: the block polygons, and every label the first pass placed.
                var blocks = new List<GeoPolygon2>();
                if (item.BlockPolygons != null)
                {
                    blocks.AddRange(item.BlockPolygons);
                }

                blocks.AddRange(settled);

                relaxed[k] = new ArrangeItem
                {
                    Box = item.Box,
                    Leader = item.Leader,
                    Offset = item.Offset,
                    OffsetTop = item.OffsetTop,
                    OffsetBottom = item.OffsetBottom,
                    BlockPolygons = blocks,
                    BlockLines = Array.Empty<GeoLine2>(),
                };
            }

            GeoVector2[] second = algorithm.Arrange(relaxed, options);
            for (int k = 0; k < failed.Count; k++)
            {
                translations[failed[k]] = second[k];
            }

            return true;
        }

        /// <summary>
        /// Judges, on the final layout, whether each label overlaps nothing.
        /// <para>
        /// An algorithm only knows the layout at the moment it places a label, so what it knows means "this spot was
        /// clear when my turn came". A label placed later, when stuck, may fall back onto one placed earlier, which
        /// would still believe itself clear. What the caller needs to know is whether the final layout has overlaps,
        /// so that is judged here, after every label has settled.
        /// </para>
        /// </summary>
        /// <returns>For each item, whether it is placed; false for a null entry.</returns>
        private static bool[] Judge(IReadOnlyList<ArrangeItem> items, GeoVector2[] translations, ArrangeOptions options)
        {
            List<Obstacle> blocks = Obstacle.CollectStatic(items);

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
            }

            var placed = new bool[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                // A label that cannot form a layout was never arranged. That it happens to overlap nothing does not
                // make it placed.
                if (items[i] == null || !items[i].TryGetLayout(options, out _))
                {
                    continue;
                }

                bool clear = !Obstacle.AnyCollides(blocks, boxes[i], options.Tolerance);

                for (int j = 0; j < items.Count && clear; j++)
                {
                    if (i == j || items[j] == null || !bounds[i].Overlaps(bounds[j]))
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
