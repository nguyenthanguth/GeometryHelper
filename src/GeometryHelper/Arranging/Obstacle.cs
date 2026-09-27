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
        Polygon,
        /// <summary>A line segment obstacle.</summary>
        Line,
        /// <summary>A rectangular obstacle.</summary>
        Rectangle
    }

    /// <summary>
    /// Represents a static obstacle or an occupied label.
    /// </summary>
    internal readonly struct Obstacle
    {
        /// <summary>Gets the type of the obstacle.</summary>
        internal ObstacleType Type { get; }
        /// <summary>Gets the polygon, when the type is <see cref="ObstacleType.Polygon"/>.</summary>
        internal GeoPolygon2 Polygon { get; }
        /// <summary>Gets the segment, when the type is <see cref="ObstacleType.Line"/>.</summary>
        internal GeoLine2 Line { get; }
        /// <summary>Gets the rectangle, when the type is <see cref="ObstacleType.Rectangle"/>.</summary>
        internal GeoRectangle2 Rectangle { get; }
        /// <summary>Gets the bounding box of the obstacle.</summary>
        internal Bounds Box { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a polygon.
        /// </summary>
        /// <param name="polygon">The polygon geometry.</param>
        internal Obstacle(GeoPolygon2 polygon)
        {
            Type = ObstacleType.Polygon;
            Polygon = polygon;
            Line = default(GeoLine2);
            Rectangle = default(GeoRectangle2);
            Box = Bounds.Of(polygon);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a line segment.
        /// </summary>
        /// <param name="line">The line segment geometry.</param>
        internal Obstacle(GeoLine2 line)
        {
            Type = ObstacleType.Line;
            Polygon = null;
            Line = line;
            Rectangle = default(GeoRectangle2);
            Box = Bounds.Of(line);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Obstacle"/> struct wrapping a rectangle.
        /// </summary>
        /// <param name="rectangle">The rectangle geometry.</param>
        internal Obstacle(GeoRectangle2 rectangle)
        {
            Type = ObstacleType.Rectangle;
            Polygon = null;
            Line = default(GeoLine2);
            Rectangle = rectangle;
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
        /// <param name="items">The labels; a null entry is passed over.</param>
        /// <returns>A list of deduplicated obstacles.</returns>
        internal static List<Obstacle> CollectStatic(IReadOnlyList<ArrangeItem> items)
        {
            var occupied = new List<Obstacle>();
            if (items == null) return occupied;

            var seenPolygons = new HashSet<GeoPolygon2>();
            var seenLines = new HashSet<GeoLine2>();

            foreach (ArrangeItem item in items)
            {
                if (item == null) continue;

                if (item.BlockPolygons != null)
                {
                    foreach (GeoPolygon2 block in item.BlockPolygons)
                    {
                        if (block != null && seenPolygons.Add(block))
                        {
                            occupied.Add(new Obstacle(block));
                        }
                    }
                }

                if (item.BlockLines != null)
                {
                    foreach (GeoLine2 block in item.BlockLines)
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
                    case ObstacleType.Rectangle:
                        // OBB vs OBB: Using SAT (Separating Axis Theorem)
                        if (moved.CollidesWith(obstacle.Rectangle, tolerance))
                            return true;
                        break;
                    case ObstacleType.Polygon:
                        // OBB vs Polygon: Check edge intersections and containment
                        if (moved.CollidesWith(obstacle.Polygon, tolerance))
                            return true;
                        break;
                    case ObstacleType.Line:
                        // OBB vs Line Segment: Check edge intersections and endpoints
                        if (moved.CollidesWith(obstacle.Line, tolerance))
                            return true;
                        break;
                }
            }

            return false;
        }
    }
}
