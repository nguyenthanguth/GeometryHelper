using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What an arc and a ray in the plane ask each other.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against an arc it is cut: into the segment from its origin out past
    /// the farthest the arc's circle reaches along it, the centre brought onto the ray and the radius added, by a margin
    /// of a millimetre or a hundred point tolerances, whichever is more. Every point of the arc lies behind that end, so
    /// the segment meets the arc where the ray does and is asked as any segment is. Against an arc of radius 50 round
    /// the origin, the ray from (-100, 0) along X is asked as the segment from there to (51, 0).
    /// </remarks>
    public static partial class Arc2
    {
        /// <summary>
        /// Gets the distance between an arc and a ray, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoArc2 arc, GeoRay2 ray) => DistanceTo(arc, ray, Tolerance.Global);

        /// <summary>
        /// Gets the distance between an arc and a ray, within a tolerance.
        /// </summary>
        /// <returns>Zero when they cross, otherwise the shortest gap between them.</returns>
        public static double DistanceTo(GeoArc2 arc, GeoRay2 ray, Tolerance tolerance)
        {
            return DistanceTo(arc, Ray2.CutPast(ray, arc, tolerance), tolerance);
        }

        /// <summary>
        /// Gets where an arc meets a ray, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoRay2 ray) => GetIntersections(arc, ray, Tolerance.Global);

        /// <summary>
        /// Gets where an arc meets a ray, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoRay2 ray, Tolerance tolerance)
        {
            return GetIntersections(arc, Ray2.CutPast(ray, arc, tolerance), tolerance);
        }

        /// <summary>
        /// Tries to find where an arc meets a ray, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public static bool TryIntersectWith(GeoArc2 arc, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(arc, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc meets a ray, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc2 arc, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(arc, Ray2.CutPast(ray, arc, tolerance), out intersections, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving an arc and landing on a ray, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoRay2 ray) => GetShortestLineTo(arc, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving an arc and landing on a ray, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving the arc and landing on the ray, as long as <see cref="DistanceTo(GeoArc2, GeoRay2, Tolerance)"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoRay2 ray, Tolerance tolerance)
        {
            return GetShortestLineTo(arc, Ray2.CutPast(ray, arc, tolerance), tolerance);
        }
    }
}
