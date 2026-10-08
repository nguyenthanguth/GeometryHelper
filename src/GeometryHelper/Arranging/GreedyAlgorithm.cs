using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Label arrangement algorithm using a Greedy strategy.
    /// </summary>
    internal class GreedyAlgorithm
    {
        /// <summary>
        /// Arranges the labels using a greedy algorithm.
        /// </summary>
        /// <param name="items">The labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        public GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            var translations = new GeoVector2[items.Count];
            // STEP 1: Collect all static obstacles from the input (block polygons and block lines), held in an index
            // that the box of each label joins as it is placed
            var occupied = new ObstacleSpatialIndex(Obstacle.CollectStatic(items));

            // STEP 2: Determine placement order of labels.
            var processingOrder = PlacementHeuristics.GetProcessingOrder(items, occupied, options);

            // STEP 3: Sequentially place each label according to the calculated order.
            foreach (int index in processingOrder)
            {
                translations[index] = Place(items[index], occupied, options);
            }

            return translations;
        }

        /// <summary>
        /// Finds the best placement position for a single label using a greedy strategy.
        /// </summary>
        private GeoVector2 Place(ArrangeItem item, ObstacleSpatialIndex occupied, ArrangeOptions options)
        {
            // Verify label box validity and calculate local filter Bounds
            if (!PlacementHeuristics.TryGetCandidateBounds(item, options, out Bounds region))
            {
                // Cannot calculate candidate: label stays in place, but we must still record the area it occupies so subsequent labels do not overlap it.
                AddBox(item, occupied, GeoVector2.Zero);
                return GeoVector2.Zero;
            }

            GeoPoint2 centre = item.Box.Center;

            // Fast filtering: Keep only obstacles that could potentially collide in the neighborhood. The index finds
            // those whose box the region overlaps, in the order they joined, the very list going over all of them gives:
            // one obstacle more would count in the clearance below, and could change the place chosen.
            List<Obstacle> nearby = occupied.Overlapping(region);

            GeoVector2 chosen = GeoVector2.Zero;
            GeoVector2 firstCandidate = GeoVector2.Zero;
            double bestClearance = double.NegativeInfinity;
            int freeSeen = 0;
            bool hasCandidate = false;

            // Iterate through search positions to find empty candidates
            foreach (Candidate candidate in item.EnumerateCandidates(options))
            {
                GeoVector2 translation = centre.GetVectorTo(candidate.Centre);

                // Save the first candidate as a fallback option if all positions collide
                if (!hasCandidate)
                {
                    firstCandidate = translation;
                    hasCandidate = true;
                }

                GeoRectangle2 moved = item.Box.Translate(translation);

                // Detailed collision check
                if (Obstacle.AnyCollides(nearby, moved, options.Tolerance))
                {
                    continue;
                }

                // Measure clearance to all surrounding obstacles to evaluate openness.
                // Among the first group of empty positions, select the one with the maximum clearance.
                // Strict comparison ensures that in case of a tie, the candidate found earlier — i.e., higher priority — still wins.
                //
                // Clearance counts beyond what the side of the place asks for over the other side, its Surplus. A label's
                // own leader is often among what it keeps clear of, and then each place in the first row of a side is
                // that side's gap clear of it: counted as measured, the side with the wider gap, the one the label is to
                // keep further off, always won. Where both sides have the same gap, nothing is taken off.
                double clearance = PlacementHeuristics.MeasureClearance(nearby, moved) - candidate.Surplus;

                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    chosen = translation;
                }

                freeSeen++;

                // Apply look-ahead mechanism (LookAheadCandidates) to stop early when enough empty candidates are found, avoiding redundant scans
                if (freeSeen >= options.LookAheadCandidates)
                {
                    break;
                }
            }

            // All candidates collide. The label must still be placed somewhere — overlapping at a predictable
            // position is still easier to manually fix than leaving the label arbitrarily in its original place.
            //
            // Always fallback to the first candidate, do NOT search for the least overlapping spot.
            // Sounds counter-intuitive but measured: searching for the least overlapping spot significantly degrades results
            // (80 crowded labels, 16 seeds: 32.3% -> 22.9% clean labels). The reason is that stuck labels will migrate
            // to quiet regions and ruin well-arranged labels there, then propagate. A fixed rule keeps all stuck labels
            // close to their own guide segment and pushed to the same side, preventing damage from spreading.
            if (freeSeen == 0)
            {
                if (!hasCandidate)
                {
                    AddBox(item, occupied, GeoVector2.Zero);
                    return GeoVector2.Zero;
                }

                chosen = firstCandidate;
            }

            // Add the newly chosen position to the static obstacle list for subsequent labels
            AddBox(item, occupied, chosen);

            // Ignore negligible tiny movements
            return chosen.Length > options.MinimumMoveDistance ? chosen : GeoVector2.Zero;
        }

        /// <summary>
        /// Translates the label box and adds it to the occupied obstacles.
        /// </summary>
        private static void AddBox(ArrangeItem item, ObstacleSpatialIndex occupied, GeoVector2 translation)
        {
            var moved = item.Box.Translate(translation);
            occupied.Add(new Obstacle(moved));
        }
    }
}
