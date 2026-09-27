using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
{
    /// <summary>
    /// Label arrangement algorithm using a Simulated Annealing strategy.
    /// Simulates physical thermodynamics to reduce collisions and find global optimal solutions.
    /// </summary>
    internal class SimulatedAnnealingAlgorithm : IArrangeAlgorithm
    {
        /// <summary>
        /// Arranges the labels using a simulated annealing algorithm.
        /// </summary>
        /// <param name="items">The labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        public GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            if (items.Count == 0)
            {
                return new GeoVector2[0];
            }

            // STEP 1: Collect initial static obstacles
            var staticObstacles = Obstacle.CollectStatic(items);

            // STEP 2: Pre-generate lists of all candidate translation points for each label
            var candidates = new List<List<(GeoVector2 Translation, GeoPoint2 GeoPoint2)>>();
            var currentTranslations = new GeoVector2[items.Count];
            var currentIndices = new int[items.Count];

            // Pre-filter nearby obstacles for each label once here.
            // The filtered region only depends on the guide segment and label dimensions, so it remains constant
            // throughout the annealing process, whereas CalculateEnergy is called thousands of times.
            // Filtering inside the loop would waste performance.
            var nearby = new List<Obstacle>[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var list = new List<(GeoVector2 Translation, GeoPoint2 GeoPoint2)>();
                nearby[i] = staticObstacles;

                if (item != null)
                {
                    GeoPoint2 centre = item.Box.Center;
                    foreach (GeoPoint2 p in item.EnumeratePlacePoints(options))
                    {
                        list.Add((centre.GetVectorTo(p), p));
                    }

                    if (PlacementHeuristics.TryGetCandidateBounds(item, options, out Bounds region))
                    {
                        nearby[i] = staticObstacles.Where(o => region.Overlaps(o.Box)).ToList();
                    }
                }

                candidates.Add(list);
                // Start at default level 0 position
                if (list.Count > 0)
                {
                    currentTranslations[i] = list[0].Translation;
                    currentIndices[i] = 0;
                }
                else
                {
                    currentTranslations[i] = GeoVector2.Zero;
                    currentIndices[i] = -1;
                }
            }

            double currentEnergy = CalculateEnergy(items, currentTranslations, nearby, options);
            double bestEnergy = currentEnergy;
            var bestTranslations = currentTranslations.ToArray();

            // STEP 3: Configure simulated annealing temperature parameters
            double T = options.AnnealingInitialTemperature;
            double coolingRate = options.AnnealingCoolingRate;
            var random = new Random(42); // Fixed seed for reproducible results

            // STEP 4: Cooling loop
            while (T > 0.1)
            {
                // Perform 50 neighbor modification attempts at each temperature level
                for (int step = 0; step < 50; step++)
                {
                    // Randomly select a label
                    int i = random.Next(items.Count);
                    if (candidates[i].Count == 0) continue;

                    // Randomly select a different translation candidate for that label
                    int oldCandidateIndex = currentIndices[i];
                    int newCandidateIndex = random.Next(candidates[i].Count);
                    if (newCandidateIndex == oldCandidateIndex) continue;

                    GeoVector2 oldTranslation = currentTranslations[i];
                    GeoVector2 newTranslation = candidates[i][newCandidateIndex].Translation;

                    // Try updating
                    currentTranslations[i] = newTranslation;
                    currentIndices[i] = newCandidateIndex;

                    double nextEnergy = CalculateEnergy(items, currentTranslations, nearby, options);
                    double delta = nextEnergy - currentEnergy;

                    // Accept the new state based on lower energy or Boltzmann probability distribution
                    if (delta < 0 || random.NextDouble() < Math.Exp(-delta / T))
                    {
                        currentEnergy = nextEnergy;

                        // Record if this is the best solution
                        if (currentEnergy < bestEnergy)
                        {
                            bestEnergy = currentEnergy;
                            bestTranslations = currentTranslations.ToArray();
                        }
                    }
                    else
                    {
                        // Restore the previous state
                        currentTranslations[i] = oldTranslation;
                        currentIndices[i] = oldCandidateIndex;
                    }
                }

                // Cool down temperature
                T *= coolingRate;
            }

            // Whether a label is placed is judged by Arranger on the final layout.
            return bestTranslations;
        }

        /// <summary>
        /// Calculates the total energy (penalty function) of the current label configuration.
        /// </summary>
        private static double CalculateEnergy(
            IReadOnlyList<ArrangeItem> items, GeoVector2[] translations, List<Obstacle>[] nearby, ArrangeOptions options)
        {
            double energy = 0.0;

            var movedRects = new GeoRectangle2[items.Count];
            var movedBoxes = new Bounds[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;

                movedRects[i] = items[i].Box.Translate(translations[i]);

                movedBoxes[i] = Bounds.Of(movedRects[i]);
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) continue;

                GeoRectangle2 rect = movedRects[i];
                Bounds box = movedBoxes[i];

                // 1. Penalty for colliding with static obstacles
                if (Obstacle.AnyCollides(nearby[i], rect, options.Tolerance))
                {
                    energy += 10000.0;
                }

                // 2. Penalty for cross-collisions between mobile labels
                for (int j = i + 1; j < items.Count; j++)
                {
                    if (items[j] == null) continue;

                    if (box.Overlaps(movedBoxes[j]))
                    {
                        if (rect.CollidesWith(movedRects[j], options.Tolerance))
                        {
                            energy += 10000.0;
                        }
                    }
                }

                // 3. Penalty for label translation distance from original position (prioritizes labels staying closest to guide segment)
                energy += translations[i].Length * 0.1;
            }

            return energy;
        }
    }
}
