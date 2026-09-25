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
        /// <returns>Zero where the ray pierces the face, otherwise the gap between them.</returns>
        /// <remarks>
        /// A ray that pierces the face touches none of its edges, so the crossing is looked for first.
        /// After that the nearest pair lies either between the ray and an edge, or under the origin of the
        /// ray where it hangs over the face without ever reaching it.
        /// </remarks>
        public static double DistanceTo(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (Intersection3.TryIntersectWith(ray, triangle, out _, tolerance))
            {
                return 0.0;
            }

            double best = ray.Origin.DistanceTo(Projection3.ProjectToTriangle(triangle, ray.Origin));

            for (int i = 0; i < 3; i++)
            {
                best = Math.Min(best, DistanceTo(ray, triangle.GetEdgeAt(i), tolerance));
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a solid.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoSolid3 solid) => DistanceTo(ray, solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a solid, within a tolerance.
        /// </summary>
        /// <returns>Zero where the ray starts inside the body or runs into it, otherwise the gap.</returns>
        public static double DistanceTo(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (Containment3.Contains(solid, ray.Origin, tolerance))
            {
                return 0.0;
            }

            double best = double.MaxValue;

            foreach (GeoTriangle3 face in solid.Triangulate(tolerance))
            {
                best = Math.Min(best, DistanceTo(ray, face, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }
    }
}
