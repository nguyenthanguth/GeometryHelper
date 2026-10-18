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

            return Gap3.SurfaceRay(solid, ray, tolerance.EqualPoint) <= tolerance.EqualPoint || Gap3.Holds(solid, ray.Origin);
        }
    }
}
