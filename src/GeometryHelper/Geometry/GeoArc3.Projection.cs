using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an arc nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoArc3
    {
        /// <summary>
        /// Gets the point of this arc closest to a point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetPointAtParameter(GetParameterAtPoint(point));

        /// <summary>
        /// Gets the point of this arc closest to a point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => GetPointAtParameter(GetParameterAtPoint(point, tolerance));
    }
}
