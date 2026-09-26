using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a ray runs into a body.
    /// </summary>
    public static partial class Intersection3
    {
        /// <summary>
        /// Gets the points where a ray passes through the surface of a solid.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoSolid3 solid) => GetIntersections(ray, solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray passes through the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossings in the order the ray meets them, with repeats left out.</returns>
        /// <remarks>
        /// The surface is walked as triangles, so a crossing that falls on the seam between two of them
        /// arrives twice and is reported once. A ray starting inside the body has an odd number of
        /// crossings, and one starting outside an even number, give or take a face it merely grazes.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, ray, tolerance);

            var reaches = new List<double>();

            foreach (GeoTriangle3 face in solid.Triangulate(tolerance))
            {
                if (TryIntersectWith(ray, face, out GeoPoint3 meeting, tolerance))
                {
                    reaches.Add(ray.GetDistanceAtPoint(meeting));
                }
            }

            reaches.Sort();

            var hits = new List<GeoPoint3>();

            foreach (double reach in reaches)
            {
                if (hits.Count > 0 && reach - ray.GetDistanceAtPoint(hits[hits.Count - 1]) <= tolerance.EqualPoint)
                {
                    continue;
                }

                hits.Add(ray.GetPointAtDistance(reach));
            }

            return hits.ToArray();
        }
    }
}
