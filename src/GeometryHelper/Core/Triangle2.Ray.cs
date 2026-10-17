using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What a triangle of the plane asks of a ray.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against a triangle it is cut: into the segment from its origin out
    /// past the farthest corner along it, by a margin of a millimetre or a hundred point tolerances, whichever is more.
    /// Every point of the triangle lies behind that end, so the segment meets the triangle where the ray does and is
    /// asked as any segment is. Against the triangle (0, 0), (100, 0), (0, 100), the ray from (-50, 20) along X is
    /// asked as the segment from there to (101, 20).
    /// </remarks>
    public static partial class Triangle2
    {
        /// <summary>
        /// Checks whether a triangle touches a ray, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoRay2 ray) => CollidesWith(triangle, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a ray, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoRay2 ray, Tolerance tolerance)
        {
            return CollidesWith(triangle, Ray2.CutPast(ray, triangle, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the distance from a triangle to a ray, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoRay2 ray) => DistanceTo(triangle, ray, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a triangle to a ray, within a tolerance.
        /// </summary>
        /// <returns>Zero where the ray starts inside the triangle or runs into it; otherwise the gap between them.</returns>
        public static double DistanceTo(GeoTriangle2 triangle, GeoRay2 ray, Tolerance tolerance)
        {
            // DistanceTo(triangle, line) read with the tolerance passed down, which it takes from the global one.
            return TryAsPolygonByItsSides(triangle, out GeoPolygon2 shape, out GeoLine2 hull)
                ? Distance2.DistanceTo(shape, ray, tolerance)
                : Distance2.DistanceTo(hull, Ray2.CutPast(ray, triangle, tolerance), tolerance);
        }

        /// <summary>
        /// Gets every point where the edges of a triangle meet a ray, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoRay2 ray) => GetIntersections(triangle, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a ray, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoRay2 ray, Tolerance tolerance)
        {
            return GetIntersections(triangle, Ray2.CutPast(ray, triangle, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a ray, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoRay2 ray) => GetShortestLineTo(triangle, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a ray, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoRay2 ray, Tolerance tolerance)
        {
            return GetShortestLineTo(triangle, Ray2.CutPast(ray, triangle, tolerance), tolerance);
        }
    }
}
