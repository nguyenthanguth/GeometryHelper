using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// Represents an AABB bounding box for fast collision filtering.
    /// </summary>
    internal readonly struct Bounds
    {
        /// <summary>Gets the minimum X coordinate.</summary>
        internal double MinX { get; }
        /// <summary>Gets the minimum Y coordinate.</summary>
        internal double MinY { get; }
        /// <summary>Gets the maximum X coordinate.</summary>
        internal double MaxX { get; }
        /// <summary>Gets the maximum Y coordinate.</summary>
        internal double MaxY { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bounds"/> struct with coordinates.
        /// </summary>
        private Bounds(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        /// <summary>
        /// Creates a bounding box enclosing a polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoPolygon2 polygon)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            int count = polygon.VertexCount;
            for (int i = 0; i < count; i++)
            {
                var v = polygon[i];
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }
            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Creates a bounding box enclosing a line segment.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoLine2 line)
        {
            return new Bounds(
                Math.Min(line.StartPoint.X, line.EndPoint.X),
                Math.Min(line.StartPoint.Y, line.EndPoint.Y),
                Math.Max(line.StartPoint.X, line.EndPoint.X),
                Math.Max(line.StartPoint.Y, line.EndPoint.Y)
            );
        }

        /// <summary>
        /// Creates a bounding box enclosing a rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Of(GeoRectangle2 rect)
        {
            var vertices = rect.GetVertices();
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            foreach (var v in vertices)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }
            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Creates a bounding box enclosing a list of points.
        /// </summary>
        /// <param name="points">The list of points.</param>
        /// <returns>The calculated bounds.</returns>
        internal static Bounds Around(IReadOnlyList<GeoPoint2> points)
        {
            double minX = points[0].X;
            double minY = points[0].Y;
            double maxX = minX;
            double maxY = minY;

            for (int i = 1; i < points.Count; i++)
            {
                minX = Math.Min(minX, points[i].X);
                minY = Math.Min(minY, points[i].Y);
                maxX = Math.Max(maxX, points[i].X);
                maxY = Math.Max(maxY, points[i].Y);
            }

            return new Bounds(minX, minY, maxX, maxY);
        }

        /// <summary>
        /// Expands the bounds outward by a margin.
        /// </summary>
        /// <param name="margin">The margin to expand.</param>
        /// <returns>The expanded bounds.</returns>
        internal Bounds Expand(double margin)
        {
            return new Bounds(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);
        }

        /// <summary>
        /// Checks whether these bounds overlap other bounds.
        /// </summary>
        /// <param name="other">The other bounds to check overlap against.</param>
        /// <returns>True if they overlap; otherwise, false.</returns>
        internal bool Overlaps(Bounds other)
        {
            return MinX <= other.MaxX && other.MinX <= MaxX
                                      && MinY <= other.MaxY && other.MinY <= MaxY;
        }
    }
}
