using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a curved chain nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolylineArc3
    {
        /// <summary>
        /// Gets the point of the chain nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the chain nearest another point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Measured on the arcs, so a point sitting over the middle of a bulge is answered with a point on
        /// the bulge and not with one on the chord across it.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.ClosestPoint(ArcChain3.EdgesOf(this), point, tolerance);
    }
}
