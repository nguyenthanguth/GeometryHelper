using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.Algorithms
{
    /// <summary>
    /// Label arrangement algorithm using a Greedy strategy.
    /// </summary>
    internal class GreedyAlgorithm : IArrangeAlgorithm
    {
        /// <summary>
        /// Arranges the labels using a greedy algorithm.
        /// </summary>
        /// <param name="arranges">The list of labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>The list of translation vectors for the labels.</returns>
        public List<GeoVector2> Arrange(List<Arrange> arranges, ArrangeOptions options)
        {
            var translations = new GeoVector2[arranges.Count];
            // STEP 1: Collect all static obstacles from the input (block polygons and block lines)
            var occupied = ArrangeAlgorithms.Arrange.CollectStaticObstacles(arranges);

            // STEP 2: Determine placement order of labels.
            var processingOrder = PlacementHeuristics.GetProcessingOrder(arranges, occupied, options);

            // STEP 3: Sequentially place each label according to the calculated order.
            foreach (int index in processingOrder)
            {
                translations[index] = Place(arranges[index], occupied, options);
            }

            return translations.ToList();
        }

        /// <summary>
        /// Finds the best placement position for a single label using a greedy strategy.
        /// </summary>
        private GeoVector2 Place(Arrange arrange, List<Obstacle> occupied, ArrangeOptions options)
        {
            // Verify label box validity and calculate local filter Bounds
            if (!PlacementHeuristics.TryGetCandidateBounds(arrange, options, out Bounds region))
            {
                // Cannot calculate candidate: label stays in place, but we must still record the area it occupies so subsequent labels do not overlap it.
                AddBox(arrange, occupied, GeoVector2.Zero);
                return GeoVector2.Zero;
            }

            GeoPoint2 centre = arrange.GeoRectangle2.Center;

            // Fast filtering: Keep only obstacles that could potentially collide in the neighborhood
            List<Obstacle> nearby = occupied.Where(obstacle => region.Overlaps(obstacle.Box)).ToList();

            GeoVector2 chosen = GeoVector2.Zero;
            GeoVector2 firstCandidate = GeoVector2.Zero;
            double bestClearance = -1.0;
            int freeSeen = 0;
            bool hasCandidate = false;

            // Iterate through search positions to find empty candidates
            foreach (GeoPoint2 candidate in arrange.EnumeratePlacePoints(options))
            {
                GeoVector2 translation = centre.GetVectorTo(candidate);

                // Save the first candidate as a fallback option if all positions collide
                if (!hasCandidate)
                {
                    firstCandidate = translation;
                    hasCandidate = true;
                }

                GeoRectangle2 moved = arrange.GeoRectangle2.Translate(translation);

                // Detailed collision check
                if (ArrangeAlgorithms.Arrange.Collides(nearby, moved, options.Tolerance))
                {
                    continue;
                }

                // Measure clearance to all surrounding obstacles to evaluate openness.
                // Among the first group of empty positions, select the one with the maximum clearance.
                // Strict comparison ensures that in case of a tie, the candidate found earlier — i.e., higher priority — still wins.
                double clearance = PlacementHeuristics.MeasureClearance(nearby, moved);

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
                    AddBox(arrange, occupied, GeoVector2.Zero);
                    return GeoVector2.Zero;
                }

                chosen = firstCandidate;
            }

            // Add the newly chosen position to the static obstacle list for subsequent labels
            AddBox(arrange, occupied, chosen);

            // Ignore negligible tiny movements
            return chosen.Length > options.MinimumMoveDistance ? chosen : GeoVector2.Zero;
        }

        /// <summary>
        /// Translates the label box and adds it to the occupied static obstacle list.
        /// </summary>
        private static void AddBox(Arrange arrange, List<Obstacle> occupied, GeoVector2 translation)
        {
            var moved = arrange.GeoRectangle2.Translate(translation);
            occupied.Add(new Obstacle(moved));
        }
    }
}
