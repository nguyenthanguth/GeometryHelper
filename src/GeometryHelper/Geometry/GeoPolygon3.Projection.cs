using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a polygon nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Gets the point on this polygon closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPolygon(this, point);

        /// <summary>
        /// Gets the shortest line from this polygon to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this polygon does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this polygon to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it
        /// leaves this polygon and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this polygon does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();
    }
}
