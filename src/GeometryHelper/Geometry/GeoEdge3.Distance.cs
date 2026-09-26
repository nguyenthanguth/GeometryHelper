using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an edge is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        /// <summary>
        /// Gets the distance from the edge to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));
    }
}
