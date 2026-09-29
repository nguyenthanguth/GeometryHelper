using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging.Algorithms
{
    /// <summary>
    /// Heuristic calculations shared among sequential label arrangement algorithms
    /// (<see cref="GreedyAlgorithm"/> and <see cref="BoundedBacktrackingAlgorithm"/>):
    /// determining processing order, measuring clearance, and bounding obstacles filtering.
    /// </summary>
    internal static class PlacementHeuristics
    {
        /// <summary>
        /// Calculates processing (sorting) order of labels to optimize placement success.
        /// </summary>
        internal static IEnumerable<int> GetProcessingOrder(
            IReadOnlyList<ArrangeItem> items, List<Obstacle> staticObstacles, ArrangeOptions options)
        {
            int[] indices = Enumerable.Range(0, items.Count)
                .Where(index => items[index] != null)
                .ToArray();

            if (indices.Length == 0)
            {
                return indices;
            }

            double centroidX = 0;
            double centroidY = 0;

            // Calculate geometric centroid of the entire distribution area if inside-out placement is enabled
            if (options.PlaceFromInsideOut)
            {
                double sumX = 0;
                double sumY = 0;
                foreach (int index in indices)
                {
                    sumX += items[index].Box.Center.X;
                    sumY += items[index].Box.Center.Y;
                }
                centroidX = sumX / indices.Length;
                centroidY = sumY / indices.Length;
            }

            // MODE 1: Geometry-based sorting only (ignores collision freedom)
            if (!options.PlaceMostConstrainedFirst)
            {
                if (options.PlaceFromInsideOut)
                {
                    // Sort in ascending order of squared distance to centroid (from core to outer edge)
                    return indices
                        .OrderBy(index => GetSquaredDistanceToCentroid(items[index], centroidX, centroidY))
                        .ToArray();
                }

                // Default sorting: left to right, bottom to top
                return indices
                    .OrderBy(index => items[index].Box.Center.X)
                    .ThenBy(index => items[index].Box.Center.Y)
                    .ToArray();
            }

            // MODE 2: Collision-optimal sorting (Default).
            // Count free positions for each label to evaluate how constrained it is.
            // Freedom degree is measured against static obstacles, calculated once before placing any labels.
            // Re-measuring after each placement is more accurate but costs quadratic time relative to label count,
            // whereas most constraints originate from static obstacles.
            var freedom = new int[items.Count];
            var room = new int[items.Count];

            foreach (int index in indices)
            {
                freedom[index] = CountFreePlaces(items[index], staticObstacles, options);
                room[index] = CountAllPlaces(items[index], options);
            }

            if (options.PlaceFromInsideOut)
            {
                // Prioritize the most constrained label first. If tie in freedom, prioritize the one closer to the centroid.
                return indices
                    .OrderBy(index => freedom[index])
                    .ThenBy(index => GetSquaredDistanceToCentroid(items[index], centroidX, centroidY))
                    .ToArray();
            }

            // Secondary criteria is total candidates. Preferred candidate groups of two labels can be identical
            // — same origin, same expansion pattern — so free spots count within that group alone cannot distinguish
            // short guide segments from long guide segments. The escape path for long guide segment labels lies in
            // far-sliding candidates, outside the sampling range.
            //
            // LINQ's OrderBy is stable, so labels tying both criteria retain their input order and the result remains reproducible.
            return indices
                .OrderBy(index => freedom[index])
                .ThenBy(index => room[index])
                .ToArray();
        }

        /// <summary>
        /// Calculates the squared distance from the label center to the area centroid.
        /// </summary>
        private static double GetSquaredDistanceToCentroid(ArrangeItem item, double centroidX, double centroidY)
        {
            double dx = item.Box.Center.X - centroidX;
            double dy = item.Box.Center.Y - centroidY;
            return dx * dx + dy * dy;
        }

        /// <summary>
        /// Counts the actual number of free positions within the first sample candidate group.
        /// </summary>
        private static int CountFreePlaces(ArrangeItem item, List<Obstacle> staticObstacles, ArrangeOptions options)
        {
            GeoPoint2 centre = item.Box.Center;
            int free = 0;
            int examined = 0;

            foreach (GeoPoint2 candidate in item.EnumeratePlacePoints(options))
            {
                // Only sample a small quantity configured by FreedomSampleSize (default = 12) to guarantee performance
                if (examined >= options.FreedomSampleSize)
                {
                    return free;
                }

                examined++;

                var translation = centre.GetVectorTo(candidate);
                var moved = item.Box.Translate(translation);

                // If this position does not overlap any obstacles, consider it a free position
                if (!Obstacle.AnyCollides(staticObstacles, moved, options.Tolerance))
                {
                    free++;
                }
            }

            return examined == 0 ? -1 : free;
        }

        /// <summary>
        /// Calculates the maximum total number of candidates that can be generated along the guide segment.
        /// </summary>
        private static int CountAllPlaces(ArrangeItem item, ArrangeOptions options)
        {
            if (!item.TryGetLayout(options, out Layout layout))
            {
                return 0;
            }

            // Each level has 2 middle positions plus 4 positions for each longitudinal shift step, and no more than the
            // cap in all.
            long shiftsPerLevel = (long)Math.Floor(layout.MaximumShift / layout.SlideStep);
            long total = (2L + 4L * shiftsPerLevel) * Math.Max(0, options.PerpendicularLevels);

            return (int)Math.Min(total, Math.Max(0, options.MaximumCandidates));
        }

        /// <summary>
        /// Calculates the minimum boundary-to-boundary distance from the label to all surrounding obstacles when there is no collision.
        /// </summary>
        internal static double MeasureClearance(List<Obstacle> obstacles, GeoRectangle2 moved)
        {
            double best = double.MaxValue;

            foreach (Obstacle obstacle in obstacles)
            {
                double distance = double.MaxValue;

                switch (obstacle.Type)
                {
                    case ObstacleType.Rectangle:
                        distance = moved.DistanceTo(obstacle.Rectangle);
                        break;
                    case ObstacleType.Polygon:
                        distance = moved.DistanceTo(obstacle.Polygon);
                        break;
                    case ObstacleType.Line:
                        distance = moved.DistanceTo(obstacle.Line);
                        break;
                }

                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the bounding box containing all potential candidate points that can be generated.
        /// Used for rough filtering to exclude obstacles too far from the label.
        /// </summary>
        internal static bool TryGetCandidateBounds(ArrangeItem item, ArrangeOptions options, out Bounds bounds)
        {
            if (!item.TryGetLayout(options, out Layout layout))
            {
                bounds = default(Bounds);
                return false;
            }

            // The rows of a side run on from its first in equal steps, so each lies between the first and the last of
            // its side: those four rows, each at both ends of its slide, hold every candidate. The last rows alone
            // do not: a gap negative enough takes the rows of one side across the leader and past those of the other,
            // and a gap between rows negative enough brings each further row back towards the leader.
            int last = Math.Max(0, options.PerpendicularLevels - 1);

            GeoVector2 alongMax = layout.Direction * layout.MaximumShift;
            GeoVector2[] rows =
            {
                layout.GetRow(true, last, options.RowGap),
                layout.GetRow(false, last, options.RowGap),
                layout.GetRow(true, 0, options.RowGap),
                layout.GetRow(false, 0, options.RowGap),
            };

            var corners = new List<GeoPoint2>(2 * rows.Length);
            foreach (GeoVector2 across in rows)
            {
                corners.Add(layout.Anchor + across + alongMax);
                corners.Add(layout.Anchor + across - alongMax);
            }

            // Create bounding box enclosing the outer corners and expand it by NeighbourMargin for safety
            bounds = Bounds.Around(corners).Expand(item.GetBoxSpan() + options.NeighbourMargin);
            return true;
        }
    }
}
