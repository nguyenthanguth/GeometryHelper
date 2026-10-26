using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether a ray reaches a body.
    /// </summary>
    public static partial class Collision3
    {
        /// <summary>
        /// Determines whether a ray starts inside a solid or runs into it.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoSolid3 solid) => CollidesWith(ray, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray starts inside a solid or runs into it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray leaving from inside the body never crosses its surface on the way out of nothing, so where
        /// it starts is asked before any face is tried. They touch where
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoSolid3, Tolerance)"/> is no longer than the point tolerance: a ray in
        /// the plane of the top of the box 100 by 100 by 50, starting 0.0005 off its side and pointing away, touches it.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, ray, tolerance);

            return RayTouches(solid, ray, tolerance);
        }

        /// <summary>
        /// Determines whether a ray comes within the point tolerance of the surface of a body or starts in it, to a billionth: the
        /// one test the body is asked about a ray by, by <c>CollidesWith</c> and by the list of where they meet alike.
        /// </summary>
        /// <param name="material">The body, its openings already cut in where the ray can reach them.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A face further from the ray than the reach of the tolerance cannot come within it and is passed over unweighed, and an
        /// origin further than a millionth outside the box of the body is not held in it to a billionth, and is not asked.
        /// </remarks>
        internal static bool RayTouches(GeoSolid3 material, GeoRay3 ray, Tolerance tolerance)
            => Gap3.SurfaceRayWithin(material, ray, tolerance.EqualPoint) || MayHold(material, ray.Origin);

        /// <summary>
        /// Determines whether a body holds a point to a billionth, asked only where the point stands within a millionth of the
        /// body's box.
        /// </summary>
        private static bool MayHold(GeoSolid3 material, GeoPoint3 point)
            => !(material.GetAabb().DistanceTo(point) > 1E-6) && Gap3.Holds(material, point);

        /// <summary>
        /// Determines whether the origin of a ray stands within the point tolerance of the surface of a body, where a meeting at
        /// the origin is a meeting with the surface: one held deeper in the body meets no surface there.
        /// </summary>
        internal static bool StartTouches(GeoSolid3 material, GeoRay3 ray, Tolerance tolerance)
            => !(Gap3.SurfacePoint(material, ray.Origin) > tolerance.EqualPoint);
    }
}
