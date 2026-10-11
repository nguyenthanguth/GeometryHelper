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

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolylineArc3)"/> turned round, so that it leaves this chain
        /// and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolylineArc3, Tolerance)"/> turned round, so that
        /// it leaves this chain and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other, Tolerance tolerance)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this, tolerance).Reverse();
    }
}
