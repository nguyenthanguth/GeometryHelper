using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an arc is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoArc3
    {
        /// <summary>
        /// Gets the distance from this arc to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => point.DistanceTo(GetClosestPointOnBoundary(point));

        /// <summary>
        /// Gets the distance from this arc to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));
    }
}
