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
        /// <para>
        /// A point whose foot in the plane of a bend falls within the tolerance of the centre of its arc is read as at the
        /// centre, which has no direction, and that bend offers its start, as
        /// <see cref="GeoEdge3.GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> does; the start is one candidate among the
        /// edges' points, and another edge can still be nearer. The distance to the point is the exact one, so the line can be
        /// longer than it, by at most twice the foot's offset from the centre.
        /// </para>
        /// <para>
        /// A straight edge no longer than the tolerance offers its start, as it does on its own, though the distance measures
        /// it as it is, so beside it the line can be longer than the distance by up to the edge's length: at a tolerance of a
        /// thousandth, the chain built of the straight edges from (0, 0, 0) to (10, 0, 0) and on to (10, 0.0006, 0) is 0.0009
        /// from (10.0009, 0.0006, 0), and the closest point is (10, 0, 0), 0.00108 away.
        /// </para>
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

        /// <summary>
        /// Gets the shortest segment joining this chain to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, measured
        /// on the arcs, so for a point away from the centre of a bend it is as long as
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0) with both
        /// legs straight, the point (50, 30, 40) gets the one from (50, 0, 0), 50 long. Near the centre of a bend it can be
        /// longer, and beside a straight edge no longer than the tolerance, by up to that edge's length, as
        /// <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> says.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining this chain to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the point <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives, measured
        /// on the arcs, so for a point away from the centre of a bend it is as long as
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>: from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0) with both
        /// legs straight, the point (50, 30, 40) gets the one from (50, 0, 0), 50 long. Near the centre of a bend it can be
        /// longer, by at most twice the foot's offset from the centre, as
        /// <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> says. Beside a straight edge no longer than the
        /// tolerance it can be longer by up to that edge's length: at a tolerance of a thousandth, the chain built of the
        /// straight edges from (0, 0, 0) to (10, 0, 0) and on to (10, 0.0006, 0) is 0.0009 from (10.0009, 0.0006, 0), and the
        /// line runs from (10, 0, 0), 0.00108 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => new GeoLine3(GetClosestPointOnBoundary(point, tolerance), point);
    }
}
