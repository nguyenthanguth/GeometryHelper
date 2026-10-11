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

        /// <summary>
        /// Gets the shortest line from this edge to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoEdge3)"/> turned round, so that it leaves this edge and lands
        /// on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this edge does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this edge to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoEdge3, Tolerance)"/> turned round, so that it
        /// leaves this edge and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this edge does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this edge to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, on the leg
        /// or the bend, so it is as long as <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the straight edge
        /// joining (0, 0, 0) and (100, 0, 0), the point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining this edge to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, on the leg
        /// or the bend, so it is as long as <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the straight edge
        /// joining (0, 0, 0) and (100, 0, 0), the point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => new GeoLine3(GetClosestPointOnBoundary(point, tolerance), point);
    }
}
