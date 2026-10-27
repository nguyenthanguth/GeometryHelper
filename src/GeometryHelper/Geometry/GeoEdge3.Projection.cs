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
        /// <remarks>
        /// A point whose foot in the plane of a bend falls within the tolerance of the centre of its arc is read as at the
        /// centre, which has no direction, and is carried to the bend's start; the distance to the point is the exact one, so
        /// the line can be longer than it, by at most twice the foot's offset from the centre. At a tolerance of a thousandth,
        /// the point (0, -0.0009, 0) and the edge from (-50, 0, 0) to (50, 0, 0) bulging by 1 about Z, through (0, -50, 0):
        /// the closest point is (-50, 0, 0), 50.00000001 away, where the distance is 49.9991.
        /// </remarks>
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
        /// or the bend, so for a point away from the centre of a bend it is as long as
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the straight edge joining (0, 0, 0) and (100, 0, 0), the
        /// point (50, 30, 40) gets the one from (50, 0, 0), 50 long. Near the centre it can be longer, as
        /// <see cref="GetShortestLineTo(GeoPoint3, Tolerance)"/> says.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining this edge to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, on the leg
        /// or the bend, so for a point away from the centre of a bend it is as long as
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the straight edge joining (0, 0, 0) and (100, 0, 0), the
        /// point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// <para>
        /// A point whose foot in the plane of a bend falls within the tolerance of the centre of its arc is read as at the
        /// centre, which has no direction, and is carried to the bend's start; the distance to the point is the exact one, so
        /// the line can be longer than it, by at most twice the foot's offset from the centre. At a tolerance of a thousandth,
        /// the point (0, -0.0009, 0) and the edge from (-50, 0, 0) to (50, 0, 0) bulging by 1 about Z, through (0, -50, 0):
        /// the line runs from (-50, 0, 0), 50.00000001 long, where the distance is 49.9991.
        /// </para>
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => new GeoLine3(GetClosestPointOnBoundary(point, tolerance), point);
    }
}
