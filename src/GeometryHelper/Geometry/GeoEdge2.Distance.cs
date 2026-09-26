using System;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an edge is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoEdge2
    {
        /// <summary>
        /// Gets the distance from the edge to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));

        /// <summary>
        /// Gets the distance from the edge to a straight segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => DistanceTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a straight segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.DistanceTo(ToArc(), line, tolerance)
                : Core.Distance2.DistanceTo(ToLine(), line, tolerance);
        }

        /// <summary>
        /// Gets the distance from the edge to another edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to another edge, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Four cases, and each is answered exactly rather than by cutting the arcs up: segment to segment,
        /// segment to arc either way round, and arc to arc.
        /// </remarks>
        public double DistanceTo(GeoEdge2 other, Tolerance tolerance)
        {
            if (IsArc)
            {
                return other.IsArc
                    ? Core.Arc2.DistanceTo(ToArc(), other.ToArc(), tolerance)
                    : Core.Arc2.DistanceTo(ToArc(), other.ToLine(), tolerance);
            }

            return other.IsArc
                ? Core.Arc2.DistanceTo(other.ToArc(), ToLine(), tolerance)
                : Core.Distance2.DistanceTo(ToLine(), other.ToLine(), tolerance);
        }

        /// <summary>
        /// Gets the distance from the edge to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => DistanceTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance)
        {
            // A circle is an arc sweeping a whole turn, so both cases go the same way and both honour the
            // tolerance.
            return IsArc
                ? Core.Arc2.DistanceTo(ToArc(), circle, tolerance)
                : Core.Arc2.DistanceTo(Core.Arc2.AsArc(circle), ToLine(), tolerance);
        }

        /// <summary>
        /// Gets the distance from the edge to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => DistanceTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The arc is taken as it is rather than as an edge, so one sweeping a whole turn is measured
        /// against too.
        /// </remarks>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.DistanceTo(ToArc(), arc, tolerance)
                : Core.Arc2.DistanceTo(arc, ToLine(), tolerance);
        }

        /// <summary>
        /// Gets the distance from this edge to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly)
            => IsArc ? ToArc().DistanceTo(poly) : ToLine().DistanceTo(poly);

        /// <summary>
        /// Gets the distance from this edge to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => DistanceTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this edge to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance)
            => IsArc ? ToArc().DistanceTo(loop, tolerance) : ToLine().DistanceTo(loop, tolerance);

        /// <summary>
        /// Gets the distance from this edge to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline)
            => IsArc ? ToArc().DistanceTo(polyline) : ToLine().DistanceTo(polyline);

        /// <summary>
        /// Gets the distance from this edge to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => DistanceTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this edge to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance)
            => IsArc ? ToArc().DistanceTo(chain, tolerance) : ToLine().DistanceTo(chain, tolerance);

        /// <summary>
        /// Gets the distance from this edge to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect)
            => IsArc ? ToArc().DistanceTo(rect) : ToLine().DistanceTo(rect);
    }
}
