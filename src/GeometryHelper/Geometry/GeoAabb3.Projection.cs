using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an axis-aligned box nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Gets the point of this box closest to a target point. A point inside the box is already on it
        /// under that reading and comes back unchanged.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point)
        {
            if (IsEmpty)
            {
                throw new InvalidOperationException("An empty bounding box has no point to return.");
            }

            return new GeoPoint3(
                Math.Max(Min.X, Math.Min(Max.X, point.X)),
                Math.Max(Min.Y, Math.Min(Max.Y, point.Y)),
                Math.Max(Min.Z, Math.Min(Max.Z, point.Z)));
        }
    }
}
