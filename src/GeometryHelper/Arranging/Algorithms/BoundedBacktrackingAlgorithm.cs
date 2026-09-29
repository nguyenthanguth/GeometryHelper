using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
{
    /// <summary>
    /// Label arrangement algorithm using a Bounded Backtracking strategy.
    /// Helps resolve local optima by trying alternative candidates when subsequent labels are stuck.
    /// </summary>
    internal class BoundedBacktrackingAlgorithm : IArrangeAlgorithm
    {
        /// <summary>
        /// The steps back the search has taken: the times it took up a label it had placed, to move it on.
        /// </summary>
        private int _stepsCount;

        /// <summary>
        /// Arranges the labels using a bounded backtracking algorithm.
        /// </summary>
        /// <param name="items">The labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        public GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            var translations = new GeoVector2[items.Count];
            // STEP 1: Collect initial static obstacles
            var occupied = Obstacle.CollectStatic(items);

            // STEP 2: Calculate label processing priority order
            var processingOrder = PlacementHeuristics.GetProcessingOrder(items, occupied, options).ToArray();
            var sortedItems = processingOrder.Select(idx => items[idx]).ToList();
            var sortedTranslations = new GeoVector2[sortedItems.Count];

            _stepsCount = 0;

            // STEP 3: Run the backtracking search
            bool success = Search(sortedItems, occupied, sortedTranslations, options);

            // STEP 4: If backtracking fails completely (no collision-free configuration is found),
            // fallback to the Greedy solution to ensure all labels still have a visible position.
            if (!success)
            {
                var greedy = new GreedyAlgorithm();
                return greedy.Arrange(items, options);
            }

            // STEP 5: Map arrangement results back to the original input order
            for (int i = 0; i < processingOrder.Length; i++)
            {
                int originalIndex = processingOrder[i];
                translations[originalIndex] = sortedTranslations[i];
            }

            return translations;
        }

        /// <summary>
        /// Places the labels in turn, each at the first of its free places that leaves a place for every label after it.
        /// When a label finds no free place, the search goes back to the label before it and moves that one on to its
        /// next place, as far back as it has to.
        /// </summary>
        /// <remarks>
        /// A loop over the labels rather than a call for each: a call for each label went as deep as there were labels,
        /// and on some hosts a few hundred of them ran the stack dry.
        /// </remarks>
        /// <returns>True when every label has a place; false when there is none for some label, or the steps ran out.</returns>
        private bool Search(IReadOnlyList<ArrangeItem> sortedItems, List<Obstacle> occupied, GeoVector2[] translations, ArrangeOptions options)
        {
            int count = sortedItems.Count;

            // The free places of each label as they stood when the search came to it, in the order it tries them, and the
            // next of them to try. Null for a label that cannot be arranged: it stays where it is, and has no other place.
            var places = new List<GeoVector2>[count];
            var next = new int[count];

            int index = 0;
            bool arriving = true;
            while (true)
            {
                if (arriving)
                {
                    // Every label has its place.
                    if (index >= count)
                    {
                        return true;
                    }

                    places[index] = GetFreePlaces(sortedItems[index], occupied, options);
                    next[index] = 0;

                    if (places[index] == null)
                    {
                        // Cannot form layout for this label: leave it in place and proceed.
                        // Arranger, judging the final layout, will see it was never arranged.
                        translations[index] = GeoVector2.Zero;
                        index++;
                        continue;
                    }
                }

                List<GeoVector2> free = places[index];
                if (free != null && next[index] < free.Count)
                {
                    // Place the label at its next free place, which stands as an obstacle for the labels after it.
                    GeoVector2 translation = free[next[index]++];
                    translations[index] = translation;
                    occupied.Add(new Obstacle(sortedItems[index].Box.Translate(translation)));

                    index++;
                    arriving = true;
                    continue;
                }

                // No place left for this label: back to the one before it, whose place is taken up again, so that it can
                // move on to its next. A label that was never arranged has no other place, and the search goes on back.
                if (index == 0)
                {
                    return false;
                }

                index--;
                arriving = false;
                if (places[index] != null)
                {
                    // A step back, and only that counts: placing a label costs none, so a search that never has to go
                    // back is never cut short, however many labels there are. Each label counting as a step, a run of
                    // more labels than steps gave up every time and fell back to the greedy algorithm.
                    _stepsCount++;
                    if (_stepsCount > options.MaxBacktrackSteps)
                    {
                        return false;
                    }

                    occupied.RemoveAt(occupied.Count - 1);
                }
            }
        }

        /// <summary>
        /// Gets the places of a label that overlap none of the obstacles, nearest first; null when the label cannot form
        /// a layout.
        /// </summary>
        private static List<GeoVector2> GetFreePlaces(ArrangeItem item, List<Obstacle> occupied, ArrangeOptions options)
        {
            if (!PlacementHeuristics.TryGetCandidateBounds(item, options, out Bounds region))
            {
                return null;
            }

            GeoPoint2 centre = item.Box.Center;

            // Fast filtering of nearby obstacles
            List<Obstacle> nearby = occupied.Where(obstacle => region.Overlaps(obstacle.Box)).ToList();

            var free = new List<GeoVector2>();
            foreach (GeoPoint2 candidate in item.EnumeratePlacePoints(options))
            {
                GeoVector2 translation = centre.GetVectorTo(candidate);
                if (!Obstacle.AnyCollides(nearby, item.Box.Translate(translation), options.Tolerance))
                {
                    free.Add(translation);
                }
            }

            // Try positions closer to the guide segment first, break ties by selecting the one with more clearance.
            //
            // Previously this was sorted purely by descending clearance, meaning it always tried the FURTHEST position first and
            // almost always stopped there. Measured consequence: average distance from label to guide segment was 2405
            // compared to 550 for Greedy — every label was thrown to the outermost perpendicular level even if closer spots were empty.
            // All default values (ArrangeItem.OffsetTop and OffsetBottom, LongitudinalOvershootRatio) indicate that labels should stay close
            // to the guide segment, so translation magnitude is the primary criteria.
            //
            // Clearance is still useful to break ties: with the same gap on both sides of the guide segment, the candidate
            // generator makes equidistant positions (top/bottom of guide segment, forward/backward slides) tie exactly very
            // frequently. It is measured for the places in a tie only, as it decides nothing else: measuring it for every
            // free place cost several times the rest of the search.
            List<GeoVector2> sorted = free.OrderBy(t => t.Length).ToList();
            for (int start = 0; start < sorted.Count;)
            {
                double length = sorted[start].Length;
                int end = start + 1;
                while (end < sorted.Count && sorted[end].Length.CompareTo(length) == 0)
                {
                    end++;
                }

                if (end - start > 1)
                {
                    List<GeoVector2> tie = sorted.GetRange(start, end - start)
                        .OrderByDescending(t => PlacementHeuristics.MeasureClearance(nearby, item.Box.Translate(t)))
                        .ToList();
                    for (int k = 0; k < tie.Count; k++)
                    {
                        sorted[start + k] = tie[k];
                    }
                }

                start = end;
            }

            return sorted;
        }
    }
}
