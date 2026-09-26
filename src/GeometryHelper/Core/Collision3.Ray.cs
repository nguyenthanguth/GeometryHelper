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
        /// it starts is asked before any face is tried.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, ray, tolerance);

            if (Containment3.Contains(solid, ray.Origin, tolerance))
            {
                return true;
            }

            foreach (GeoTriangle3 face in solid.Triangulate(tolerance))
            {
                if (Intersection3.TryIntersectWith(ray, face, out _, tolerance))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
