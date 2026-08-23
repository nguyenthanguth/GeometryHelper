using System;
using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.Algorithms
{
    /// <summary>
    /// Label arrangement algorithm using continuous physical force simulation (Force-directed),
    /// followed by discrete mapping of label centers to the nearest non-colliding candidate positions.
    /// </summary>
    internal class ForceDirectedAlgorithm : IArrangeAlgorithm
    {
        /// <summary>Influence radius of repulsive force from static obstacles; beyond this, no force is contributed.</summary>
        private const double PushRadius = 1500.0;

        /// <summary>
        /// Arranges the labels using a force-directed algorithm.
        /// </summary>
        /// <param name="arranges">The list of labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>The list of translation vectors for the labels.</returns>
        public List<GeoVector2> Arrange(List<Arrange> arranges, ArrangeOptions options)
        {
            if (arranges.Count == 0)
            {
                return new List<GeoVector2>();
            }

            var staticObstacles = ArrangeAlgorithms.Arrange.CollectStaticObstacles(arranges);
            var anchors = new GeoPoint2[arranges.Count];
            var positions = new GeoPoint2[arranges.Count];

            // STEP 1: Record initial default positions (Anchors)
            for (int i = 0; i < arranges.Count; i++)
            {
                var arrange = arranges[i];
                if (arrange == null) continue;

                anchors[i] = arrange.GeoRectangle2.Center;
                positions[i] = arrange.GeoRectangle2.Center;
            }

            // STEP 2: Run continuous physical force simulation
            int iterations = options.ForceIterations;
            double timestep = 0.5;

            for (int step = 0; step < iterations; step++)
            {
                var forces = new GeoVector2[arranges.Count];
                for (int i = 0; i < arranges.Count; i++)
                {
                    forces[i] = GeoVector2.Zero;
                }

                // 1. Spring Force pulling the label back to its original position to prevent it from drifting too far
                for (int i = 0; i < arranges.Count; i++)
                {
                    if (arranges[i] == null) continue;

                    GeoVector2 toAnchor = positions[i].GetVectorTo(anchors[i]);
                    forces[i] = forces[i].Add(toAnchor * 0.05); // Spring elasticity coefficient
                }

                // 2. Coulomb Repulsive Force pushing labels away from each other
                for (int i = 0; i < arranges.Count; i++)
                {
                    if (arranges[i] == null) continue;

                    for (int j = i + 1; j < arranges.Count; j++)
                    {
                        if (arranges[j] == null) continue;

                        GeoVector2 toOther = positions[i].GetVectorTo(positions[j]);
                        double distance = Math.Max(toOther.Length, 10.0);

                        // Only repel if two labels are too close to each other (threshold 2500mm)
                        if (distance < 2500.0)
                        {
                            double pushMagnitude = 150000.0 / (distance * distance);
                            if (!toOther.TryGetNormal(out GeoVector2 pushDir))
                            {
                                pushDir = GeoVector2.XAxis;
                            }
                            forces[i] = forces[i].Subtract(pushDir * pushMagnitude);
                            forces[j] = forces[j].Add(pushDir * pushMagnitude);
                        }
                    }
                }

                // 3. Repulsive force from static obstacles (block polygons and lines)
                for (int i = 0; i < arranges.Count; i++)
                {
                    if (arranges[i] == null) continue;

                    // Obstacles further than PushRadius do not contribute force. Exclude them using bounding box
                    // overlap checks first, since GetClosestBoundaryPoint must iterate through each edge and is significantly more expensive.
                    Bounds reach = Bounds.Around(new[] { positions[i] }).Expand(PushRadius);

                    foreach (Obstacle obstacle in staticObstacles)
                    {
                        if (!reach.Overlaps(obstacle.Box))
                        {
                            continue;
                        }

                        // Get the closest point on the obstacle boundary, then push the label along the direction
                        // FROM that point TO the label. Taking the opposite direction (label -> obstacle center)
                        // would turn the repulsive force into an attractive force.
                        GeoPoint2 closest = GetClosestBoundaryPoint(obstacle, positions[i]);

                        double dist = Math.Max(closest.DistanceTo(positions[i]), 10.0);
                        if (dist >= PushRadius)
                        {
                            continue;
                        }

                        if (!closest.GetVectorTo(positions[i]).TryGetNormal(out GeoVector2 pushDir))
                        {
                            pushDir = GeoVector2.XAxis;
                        }

                        double pushMagnitude = 200000.0 / (dist * dist);
                        forces[i] = forces[i].Add(pushDir * pushMagnitude);
                    }
                }

                // 4. Update label positions (Enforce maximum displacement to keep system stable)
                for (int i = 0; i < arranges.Count; i++)
                {
                    if (arranges[i] == null) continue;

                    GeoVector2 stepMove = forces[i] * timestep;
                    if (stepMove.Length > 500.0)
                    {
                        stepMove = stepMove.Normalize() * 500.0;
                    }

                    positions[i] = positions[i].Add(stepMove);
                }
            }

            // STEP 3: Discrete Mapping
            // Find the nearest non-colliding discrete candidate point to the final physical position
            var translations = new GeoVector2[arranges.Count];
            var finalOccupied = new List<Obstacle>(staticObstacles);

            for (int i = 0; i < arranges.Count; i++)
            {
                var arrange = arranges[i];
                if (arrange == null) continue;

                GeoPoint2 centre = arrange.GeoRectangle2.Center;
                GeoPoint2 physTarget = positions[i];
                GeoVector2 bestTranslation = GeoVector2.Zero;
                double bestDist = double.MaxValue;
                bool mapped = false;

                // Pre-filter obstacles out of the label's reach. finalOccupied grows as labels
                // are placed, so this filter is beneficial even if the drawing has no static blocked regions.
                List<Obstacle> nearby = PlacementHeuristics.TryGetCandidateBounds(arrange, options, out Bounds region)
                    ? finalOccupied.Where(o => region.Overlaps(o.Box)).ToList()
                    : finalOccupied;

                // Iterate through all discrete candidates of the label
                foreach (GeoPoint2 candidate in arrange.EnumeratePlacePoints(options))
                {
                    GeoVector2 translation = centre.GetVectorTo(candidate);
                    GeoRectangle2 moved = arrange.GeoRectangle2.Translate(translation);

                    // Only accept if the candidate does not collide with static obstacles and previously placed labels
                    if (!ArrangeAlgorithms.Arrange.Collides(nearby, moved, options.Tolerance))
                    {
                        double dist = candidate.DistanceTo(physTarget);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestTranslation = translation;
                            mapped = true;
                        }
                    }
                }

                // If no empty position is found, fallback to the default level 0 position of the label.
                // Placed flag is determined by Arrange.MarkPlacementResults on the final layout.
                if (!mapped)
                {
                    var points = arrange.EnumeratePlacePoints(options).ToList();
                    bestTranslation = points.Count > 0 ? centre.GetVectorTo(points[0]) : GeoVector2.Zero;
                }

                translations[i] = bestTranslation;

                // Add the selected position as a static obstacle for subsequent labels
                GeoRectangle2 finalRect = arrange.GeoRectangle2.Translate(bestTranslation);
                finalOccupied.Add(new Obstacle(finalRect));
            }

            return translations.ToList();
        }

        /// <summary>
        /// Gets the point on the obstacle boundary closest to a given point.
        /// </summary>
        private static GeoPoint2 GetClosestBoundaryPoint(Obstacle obstacle, GeoPoint2 from)
        {
            switch (obstacle.Type)
            {
                case ObstacleType.GeoLine2:
                    return obstacle.GeoLine2.GetClosestPointOnBoundary(from);
                case ObstacleType.GeoRectangle2:
                    return GetClosestPointOnEdges(obstacle.GeoRectangle2.GetEdges(), from);
                default:
                    return GetClosestPointOnEdges(obstacle.GeoPolygon2.GetEdges(), from);
            }
        }

        /// <summary>
        /// Gets the closest point to a given point among the closest points on each edge.
        /// </summary>
        private static GeoPoint2 GetClosestPointOnEdges(IEnumerable<GeoLine2> edges, GeoPoint2 from)
        {
            GeoPoint2 best = from;
            double bestDistance = double.MaxValue;

            foreach (GeoLine2 edge in edges)
            {
                GeoPoint2 candidate = edge.GetClosestPointOnBoundary(from);
                double distance = candidate.DistanceTo(from);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
