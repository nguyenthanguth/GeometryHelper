using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public readonly partial struct GeoTriangle2
    {
        /// <summary>
        /// Gets the point on the edges of this triangle closest to a point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Triangle2.GetClosestPointOnBoundary(this, point);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a segment, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Triangle2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Triangle2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a rectangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Triangle2.GetShortestLineTo(this, rect);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a polyline, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Triangle2.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a polyline, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a polygon, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon) => Triangle2.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a polygon, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on an arc, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Triangle2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a curved loop, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Triangle2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a curved chain, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Triangle2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Triangle2.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, face, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on an edge, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => Triangle2.GetShortestLineTo(this, edge);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, edge, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on another triangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 other) => Triangle2.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment leaving this triangle and landing on another triangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 other, Tolerance tolerance) => Triangle2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the edge of this triangle closest to a point.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoPoint2 point) => Triangle2.GetClosestEdge(this, point);

        /// <summary>
        /// Gets the edge of this triangle closest to a segment.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoLine2 line) => Triangle2.GetClosestEdge(this, line);

        /// <summary>
        /// Gets the edge of this triangle closest to a circle.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoCircle2 circle) => Triangle2.GetClosestEdge(this, circle);
    }
}
