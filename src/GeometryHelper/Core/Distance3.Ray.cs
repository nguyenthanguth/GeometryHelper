using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Measuring a ray against a face or a body.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so it cannot be cut into a segment and measured that way
    /// without choosing how far to cut. Every candidate here is one the ray really holds: an end of the
    /// other shape brought onto the ray, the origin of the ray brought onto the other shape, or the place
    /// where the two run past each other.
    /// </remarks>
    public static partial class Distance3
    {
        /// <summary>
        /// Calculates the shortest distance between a ray and a triangle.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoTriangle3 triangle) => DistanceTo(ray, triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a triangle, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Zero only where the two truly meet, as where the ray pierces the face, starts on it or lies in its plane
        /// towards it, otherwise the gap between them, measured exactly.
        /// </returns>
        /// <remarks>
        /// A ray that pierces the face touches none of its edges, so the crossing is looked for first.
        /// After that the nearest pair lies either between the ray and an edge, or under the origin of the
        /// ray where it hangs over the face without ever reaching it. The ray is taken as far as the corner farthest from
        /// its origin, past which it only draws away.
        /// </remarks>
        public static double DistanceTo(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Gap3.Ray(Gap3.Region.Of(triangle), ray, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a solid.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoSolid3 solid) => DistanceTo(ray, solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a solid, within a tolerance.
        /// </summary>
        /// <returns>Zero where the ray starts inside the body or runs into it, otherwise the gap.</returns>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            double best = Gap3.SurfaceRay(solid, ray, 0.0);

            return best <= 0.0 || Gap3.Holds(solid, ray.Origin) ? 0.0 : best;
        }
    }
}
