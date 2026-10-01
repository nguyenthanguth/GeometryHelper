using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public readonly partial struct GeoTriangle2
    {
        /// <summary>
        /// Gets the distance from this triangle to a point: nought for a point within it.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Triangle2.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this triangle to a point, negative for a point within it, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the edges, whichever side of them the point is on, and the sign says
        /// which side: negative inside, nought on them, positive outside.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Triangle2.SignedDistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this triangle to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Triangle2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 polygon) => Triangle2.DistanceTo(this, polygon);

        /// <summary>
        /// Gets the distance from this triangle to a segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Triangle2.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance from this triangle to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Triangle2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the distance from this triangle to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Triangle2.DistanceTo(this, polyline);

        /// <summary>
        /// Gets the distance from this triangle to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Triangle2.DistanceTo(this, rect);

        /// <summary>
        /// Gets the distance from this triangle to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => Triangle2.DistanceTo(this, edge);

        /// <summary>
        /// Gets the distance from this triangle to another triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle2 other) => Triangle2.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this triangle to an arc, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Triangle2.DistanceTo(this, arc);

        /// <summary>
        /// Gets the distance from this triangle to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Triangle2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a curved loop, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Triangle2.DistanceTo(this, loop);

        /// <summary>
        /// Gets the distance from this triangle to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Triangle2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a curved chain, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Triangle2.DistanceTo(this, chain);

        /// <summary>
        /// Gets the distance from this triangle to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Triangle2.DistanceTo(this, chain, tolerance);
    }
}
