using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What an ellipse asks of a ray in the plane.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against an ellipse it is cut: into the segment from its origin out
    /// past the farthest the ellipse could reach along it, the centre brought onto the ray and the major radius added,
    /// by a margin of a millimetre or a hundred point tolerances, whichever is more. Every point of the ellipse lies
    /// behind that end, so the segment meets the ellipse where the ray does and is asked as any segment is. Against the
    /// ellipse round the origin with radii 50 and 30, the ray from (-100, 0) along X is asked as the segment from there
    /// to (51, 0). The ray asks nothing of the ellipse: the ellipse is asked from the ellipse.
    /// </remarks>
    public static partial class Ellipse2
    {
        /// <summary>
        /// Determines whether an ellipse touches a ray, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoRay2 ray) => CollidesWith(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether an ellipse touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ray comes within the point tolerance of the region, crossing the rim or starting inside; otherwise, false.</returns>
        public static bool CollidesWith(GeoEllipse2 ellipse, GeoRay2 ray, Tolerance tolerance)
        {
            return CollidesWith(ellipse, Ray2.CutPast(ray, ellipse, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the distance from an ellipse to a ray, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoRay2 ray) => DistanceTo(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an ellipse to a ray, within a tolerance.
        /// </summary>
        /// <returns>Zero where the ray starts inside the ellipse or runs into it; otherwise the gap between them.</returns>
        public static double DistanceTo(GeoEllipse2 ellipse, GeoRay2 ray, Tolerance tolerance)
        {
            return DistanceTo(ellipse, Ray2.CutPast(ray, ellipse, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray crosses the rim of an ellipse, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoRay2 ray) => GetIntersections(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoRay2 ray, Tolerance tolerance)
        {
            return GetIntersections(ellipse, Ray2.CutPast(ray, ellipse, tolerance), tolerance);
        }

        /// <summary>
        /// Tries to find where a ray crosses the rim of an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(ellipse, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses the rim of an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoEllipse2 ellipse, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(ellipse, Ray2.CutPast(ray, ellipse, tolerance), out intersections, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving the rim of an ellipse and landing on a ray, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoRay2 ray) => GetShortestLineTo(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the rim of an ellipse and landing on a ray, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoRay2 ray, Tolerance tolerance)
        {
            return GetShortestLineTo(ellipse, Ray2.CutPast(ray, ellipse, tolerance), tolerance);
        }
    }
}
