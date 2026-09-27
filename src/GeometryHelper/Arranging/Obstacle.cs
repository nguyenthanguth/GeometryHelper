using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Specifies the type of geometric obstacle.
    /// </summary>
    internal enum ObstacleType
    {
        /// <summary>A polygon obstacle.</summary>
        GeoPolygon2,
        /// <summary>A line segment obstacle.</summary>
        GeoLine2,
        /// <summary>A rectangular obstacle.</summary>
        GeoRectangle2
    }

    /// <summary>
    /// Represents a static obstacle or an occupied label.
    /// </summary>
    internal readonly struct Obstacle
    {
        /// <summary>Gets the type of the obstacle.</summary>
        internal ObstacleType Type { get; }
        /// <summary>Gets the underlying polygon geometry if type is GeoPolygon2.</summary>
        internal GeoPolygon2 GeoPolygon2 { get; }
        /// <summary>Gets the underlying line segment geometry if type is GeoLine2.</summary>
        internal GeoLine2 GeoLine2 { get; }
        /// <summary>Gets the underlying rectangle geometry if type is GeoRectangle2.</summary>
        internal GeoRectangle2 GeoRectangle2 { get; }
        /// <summary>Gets the bounding box of the obstacle.</summary>
        internal Bounds Box { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a polygon.
        /// </summary>
        /// <param name="polygon">The polygon geometry.</param>
        internal Obstacle(GeoPolygon2 polygon)
        {
            Type = ObstacleType.GeoPolygon2;
            GeoPolygon2 = polygon;
            GeoLine2 = default(GeoLine2);
            GeoRectangle2 = default(GeoRectangle2);
            Box = Bounds.Of(polygon);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a line segment.
        /// </summary>
        /// <param name="line">The line segment geometry.</param>
        internal Obstacle(GeoLine2 line)
        {
            Type = ObstacleType.GeoLine2;
            GeoPolygon2 = null;
            GeoLine2 = line;
            GeoRectangle2 = default(GeoRectangle2);
            Box = Bounds.Of(line);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle geometry.</param>
        internal Obstacle(GeoRectangle2 rectangle)
        {
            Type = ObstacleType.GeoRectangle2;
            GeoPolygon2 = null;
            GeoLine2 = default(GeoLine2);
            GeoRectangle2 = rectangle;
            Box = Bounds.Of(rectangle);
        }

        /// <summary>
        /// Collects and deduplicates static obstacles from the given list of labels.
        /// <para>
        /// Duplicate obstacles are deduplicated to retain only a single instance. A common library usage pattern
        /// is to assign the same set of blocked regions to every label — e.g., each label avoids all other path segments —
        /// causing the list to grow quadratically with the number of labels, even though the number of distinct
        /// geometries remains small. Deduplication here benefits all algorithms.
        /// </para>
        /// </summary>
        /// <param name="arranges">The list of labels containing obstacles.</param>
        /// <returns>A list of deduplicated obstacles.</returns>
        internal static List<Obstacle> CollectStatic(List<Arrange> arranges)
        {
            var occupied = new List<Obstacle>();
            if (arranges == null) return occupied;

            var seenPolygons = new HashSet<GeoPolygon2>();
            var seenLines = new HashSet<GeoLine2>();

            foreach (Arrange arrange in arranges)
            {
                if (arrange == null) continue;

                if (arrange.BlockPolygons != null)
                {
                    foreach (GeoPolygon2 block in arrange.BlockPolygons)
                    {
                        if (block != null && seenPolygons.Add(block))
                        {
                            occupied.Add(new Obstacle(block));
                        }
                    }
                }

                if (arrange.BlockLines != null)
                {
                    foreach (GeoLine2 block in arrange.BlockLines)
                    {
                        if (seenLines.Add(block))
                        {
                            occupied.Add(new Obstacle(block));
                        }
                    }
                }
            }
            return occupied;
        }

        /// <summary>
        /// Checks whether a translated rectangle collides with any of the static obstacles.
        /// </summary>
        /// <param name="obstacles">The list of static obstacles.</param>
        /// <param name="moved">The translated rectangle to check.</param>
        /// <param name="tolerance">The geometric tolerance.</param>
        /// <returns>True if a collision is detected; otherwise, false.</returns>
        internal static bool AnyCollides(List<Obstacle> obstacles, GeoRectangle2 moved, Tolerance tolerance)
        {
            var movedBox = Bounds.Of(moved);

            foreach (Obstacle obstacle in obstacles)
            {
                // Rough filtering using bounding box (AABB) first to improve collision check performance
                if (!movedBox.Overlaps(obstacle.Box))
                {
                    continue;
                }

                // Detailed collision check based on specific geometric type
                switch (obstacle.Type)
                {
                    case ObstacleType.GeoRectangle2:
                        // OBB vs OBB: Using SAT (Separating Axis Theorem)
                        if (moved.CollidesWith(obstacle.GeoRectangle2, tolerance))
                            return true;
                        break;
                    case ObstacleType.GeoPolygon2:
                        // OBB vs Polygon: Check edge intersections and containment
                        if (moved.CollidesWith(obstacle.GeoPolygon2, tolerance))
                            return true;
                        break;
                    case ObstacleType.GeoLine2:
                        // OBB vs Line Segment: Check edge intersections and endpoints
                        if (moved.CollidesWith(obstacle.GeoLine2, tolerance))
                            return true;
                        break;
                }
            }

            return false;
        }
    }
}
