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

        /// <summary>
        /// Gets the shortest line from this arc to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoArc3)"/> turned round, so that it leaves this arc and lands
        /// on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this arc does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this arc to the boundary of a curved loop in the same plane, within a tolerance:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoArc3, Tolerance)"/> turned round, so that it leaves this arc
        /// and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this arc does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this arc to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3)"/> gives, so it is as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>: from the quarter arc of radius 50 about the origin on the XY plane,
        /// running from (50, 0, 0) round to (0, 50, 0), the point (80, 0, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => new GeoLine3(GetClosestPointOnBoundary(point), point);

        /// <summary>
        /// Gets the shortest segment joining this arc to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, so it is as
        /// long as <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the quarter arc of radius 50 about the origin on
        /// the XY plane, running from (50, 0, 0) round to (0, 50, 0), the point (80, 0, 40) gets the one from (50, 0, 0),
        /// 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => new GeoLine3(GetClosestPointOnBoundary(point, tolerance), point);
    }
}
