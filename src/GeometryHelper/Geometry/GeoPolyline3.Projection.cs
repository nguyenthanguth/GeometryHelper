using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a polyline nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Gets the point on this polyline closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPolyline(this, point);

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolyline3)"/> turned round, so that it leaves this chain and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolyline3, Tolerance)"/> turned round, so that it
        /// leaves this chain and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other, Tolerance tolerance)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this, tolerance).Reverse();
    }
}
