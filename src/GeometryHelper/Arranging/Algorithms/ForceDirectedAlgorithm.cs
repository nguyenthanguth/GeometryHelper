using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
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
        /// <param name="items">The labels to arrange.</param>
        /// <param name="options">The arrangement options.</param>
        /// <returns>How far each label moves, in the order of the labels.</returns>
        public GeoVector2[] Arrange(IReadOnlyList<ArrangeItem> items, ArrangeOptions options)
        {
            if (items.Count == 0)
            {
                return new GeoVector2[0];
            }

            var staticObstacles = Obstacle.CollectStatic(items);
            var anchors = new GeoPoint2[items.Count];
            var positions = new GeoPoint2[items.Count];
            var escapes = new GeoVector2[items.Count];

            // STEP 1: Record initial default positions (Anchors)
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                anchors[i] = item.Box.Center;
                positions[i] = item.Box.Center;
                escapes[i] = GetEscape(item, options);
            }

            // STEP 2: Run continuous physical force simulation
            int iterations = options.ForceIterations;
            double timestep = 0.5;

            for (int step = 0; step < iterations; step++)
            {
                var forces = new GeoVector2[items.Count];
                for (int i = 0; i < items.Count; i++)
                {
                    forces[i] = GeoVector2.Zero;
                }

                // 1. Spring Force pulling the label back to its original position to prevent it from drifting too far
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

                    GeoVector2 toAnchor = positions[i].GetVectorTo(anchors[i]);
                    forces[i] = forces[i].Add(toAnchor * 0.05); // Spring elasticity coefficient
                }

                // 2. Coulomb Repulsive Force pushing labels away from each other
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

                    for (int j = i + 1; j < items.Count; j++)
                    {
                        if (items[j] == null) continue;

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
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

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
                            pushDir = escapes[i];
                        }

                        double pushMagnitude = 200000.0 / (dist * dist);
                        forces[i] = forces[i].Add(pushDir * pushMagnitude);
                    }
                }

                // 4. Update label positions (Enforce maximum displacement to keep system stable)
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == null) continue;

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
            var translations = new GeoVector2[items.Count];
            var finalOccupied = new List<Obstacle>(staticObstacles);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;

                GeoPoint2 centre = item.Box.Center;
                GeoPoint2 physTarget = positions[i];
                GeoVector2 bestTranslation = GeoVector2.Zero;
                double bestDist = double.MaxValue;
                bool mapped = false;

                // Pre-filter obstacles out of the label's reach. finalOccupied grows as labels
                // are placed, so this filter is beneficial even if the drawing has no static blocked regions.
                List<Obstacle> nearby = PlacementHeuristics.TryGetCandidateBounds(item, options, out Bounds region)
                    ? finalOccupied.Where(o => region.Overlaps(o.Box)).ToList()
                    : finalOccupied;

                // Iterate through all discrete candidates of the label
                foreach (GeoPoint2 candidate in item.EnumeratePlacePoints(options))
                {
                    GeoVector2 translation = centre.GetVectorTo(candidate);
                    GeoRectangle2 moved = item.Box.Translate(translation);

                    // Only accept if the candidate does not collide with static obstacles and previously placed labels
                    if (!Obstacle.AnyCollides(nearby, moved, options.Tolerance))
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
                // Whether a label is placed is judged by Arranger on the final layout.
                if (!mapped)
                {
                    var points = item.EnumeratePlacePoints(options).ToList();
                    bestTranslation = points.Count > 0 ? centre.GetVectorTo(points[0]) : GeoVector2.Zero;
                }

                translations[i] = bestTranslation;

                // Add the selected position as a static obstacle for subsequent labels
                GeoRectangle2 finalRect = item.Box.Translate(bestTranslation);
                finalOccupied.Add(new Obstacle(finalRect));
            }

            return translations;
        }

        /// <summary>
        /// Gets the way a label is pushed off what it sits right on, where the push has no way of its own: a label
        /// centred on its own leader, among what it keeps clear of, is pushed to the side of the leader with the smaller
        /// gap. Where both sides have the same gap, or the label cannot be arranged, it is pushed along +X.
        /// </summary>
        /// <remarks>
        /// Always along +X, a label on a leader whose sides had different gaps was pushed off to whichever side +X
        /// falls on, the right of a vertical leader, and then went to the candidate nearest to where the push left it,
        /// on that side, however wide its gap.
        /// </remarks>
        private static GeoVector2 GetEscape(ArrangeItem item, ArrangeOptions options)
        {
            if (!item.TryGetLayout(options, out Layout layout))
            {
                return GeoVector2.XAxis;
            }

            if (layout.PositiveOffset < layout.NegativeOffset)
            {
                return layout.Perpendicular;
            }

            return layout.NegativeOffset < layout.PositiveOffset ? -layout.Perpendicular : GeoVector2.XAxis;
        }

        /// <summary>
        /// Gets the point on the obstacle boundary closest to a given point.
        /// </summary>
        private static GeoPoint2 GetClosestBoundaryPoint(Obstacle obstacle, GeoPoint2 from)
        {
            switch (obstacle.Type)
            {
                case ObstacleType.Line:
                    return obstacle.Line.GetClosestPointOnBoundary(from);
                case ObstacleType.Rectangle:
                    return GetClosestPointOnEdges(obstacle.Rectangle.GetEdges(), from);
                default:
                    return GetClosestPointOnEdges(obstacle.Polygon.GetEdges(), from);
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
