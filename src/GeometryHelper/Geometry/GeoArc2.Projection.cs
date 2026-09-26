using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an arc nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoArc2
    {
        /// <summary>
        /// Gets the point of this arc closest to a point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Arc2.ProjectToArc(this, point);

        /// <summary>
        /// Gets the point of this arc closest to a point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point, Tolerance tolerance) => Arc2.ProjectToArc(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a point, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => Arc2.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a point, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => Arc2.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a segment, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Arc2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Arc2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a arc, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 other) => Arc2.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 other, Tolerance tolerance) => Arc2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Arc2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Arc2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a polygon, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => Projection2.GetShortestLineTo(poly, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a polygon, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetShortestLineTo(poly, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a curved loop, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(loop, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(loop, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a polyline, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a polyline, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a curved chain, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(chain, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(chain, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a rectangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(rect, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(rect, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the edge of a curved loop nearest this arc, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop) => ClosestEdge2.GetClosestEdge(loop, this);

        /// <summary>
        /// Gets the edge of a curved loop nearest this arc, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(loop, this, tolerance);

        /// <summary>
        /// Gets the edge of a curved chain nearest this arc, using the default tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain) => ClosestEdge2.GetClosestEdge(chain, this);

        /// <summary>
        /// Gets the edge of a curved chain nearest this arc, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(chain, this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this arc and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);
    }
}
