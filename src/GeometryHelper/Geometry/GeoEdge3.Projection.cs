using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an edge nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        /// <summary>
        /// Gets the point of the edge nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the edge nearest another point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
        {
            return IsArc
                ? ToArc().GetClosestPointOnBoundary(point, tolerance)
                : GetPointAtParameter(GetParameterAtPoint(point, tolerance));
        }
    }
}
