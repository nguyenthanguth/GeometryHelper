using System;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an edge nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoEdge2
    {
        /// <summary>
        /// Gets the point of the edge closest to a point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the edge closest to a point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.ProjectToArc(ToArc(), point, tolerance)
                : Core.Projection2.ProjectToLine(ToLine(), point, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the edge to a straight segment.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the edge to a straight segment, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving this edge and landing on <paramref name="line"/>, of no length at all where the two cross.</returns>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.GetShortestLineTo(ToArc(), line, tolerance)
                : Core.Projection2.GetShortestLineTo(ToLine(), line, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the edge to another edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the edge to another edge, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Four cases, as with the distance: segment to segment, segment to arc either way round, and arc
        /// to arc. Where the arc is the second of the two the answer is turned round, so the segment always
        /// leaves this edge.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoEdge2 other, Tolerance tolerance)
        {
            if (IsArc)
            {
                return other.IsArc
                    ? Core.Arc2.GetShortestLineTo(ToArc(), other.ToArc(), tolerance)
                    : Core.Arc2.GetShortestLineTo(ToArc(), other.ToLine(), tolerance);
            }

            return other.IsArc
                ? Core.Arc2.GetShortestLineTo(other.ToArc(), ToLine(), tolerance).Reverse()
                : Core.Projection2.GetShortestLineTo(ToLine(), other.ToLine(), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the edge to a circle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the edge to a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance)
        {
            // A circle is an arc sweeping a whole turn, so both cases go the same way.
            return IsArc
                ? Core.Arc2.GetShortestLineTo(ToArc(), circle, tolerance)
                : Core.Arc2.GetShortestLineTo(Core.Arc2.AsArc(circle), ToLine(), tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining the edge to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the edge to an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The arc is taken as it is rather than as an edge, so one sweeping a whole turn is joined to as
        /// well.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.GetShortestLineTo(ToArc(), arc, tolerance)
                : Core.Arc2.GetShortestLineTo(arc, ToLine(), tolerance).Reverse();
        }

        /// <summary>
        /// Gets the edge of this edge nearest a curved loop.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop) => GetClosestEdge(loop, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this edge nearest a curved loop, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, Tolerance tolerance)
            => IsArc ? ToArc().GetClosestEdge(loop, tolerance) : ToLine().GetClosestEdge(loop, tolerance);

        /// <summary>
        /// Gets the edge of this edge nearest a curved chain.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain) => GetClosestEdge(chain, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this edge nearest a curved chain, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, Tolerance tolerance)
            => IsArc ? ToArc().GetClosestEdge(chain, tolerance) : ToLine().GetClosestEdge(chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a face.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(face, tolerance) : ToLine().GetShortestLineTo(face, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a polygon.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => GetShortestLineTo(poly, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a polygon, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(poly, tolerance) : ToLine().GetShortestLineTo(poly, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => GetShortestLineTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(loop, tolerance) : ToLine().GetShortestLineTo(loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a polyline.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a polyline, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(polyline, tolerance) : ToLine().GetShortestLineTo(polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(chain, tolerance) : ToLine().GetShortestLineTo(chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a rectangle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => GetShortestLineTo(rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this edge and landing on a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance)
            => IsArc ? ToArc().GetShortestLineTo(rect, tolerance) : ToLine().GetShortestLineTo(rect, tolerance);
    }
}
