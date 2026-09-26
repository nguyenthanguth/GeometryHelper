using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a curved loop nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Gets the point of the outline nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the outline nearest another point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.ClosestPoint(Edges(), point, tolerance);
    }
}
