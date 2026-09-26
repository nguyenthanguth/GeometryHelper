using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an arc is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoArc2
    {
        /// <summary>
        /// Gets the distance from this arc to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Arc2.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this arc to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Arc2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance between this arc and a segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Arc2.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance between this arc and a segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Arc2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Gets the distance between this arc and another.
        /// </summary>
        public double DistanceTo(GeoArc2 other) => Arc2.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance between this arc and another, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 other, Tolerance tolerance) => Arc2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a circle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Arc2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the distance from this arc to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Arc2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a polygon, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Gets the distance from this arc to a polygon, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.DistanceTo(poly, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a curved loop, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Gets the distance from this arc to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this arc to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a curved chain, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Gets the distance from this arc to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to a rectangle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Gets the distance from this arc to a rectangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the distance from this arc to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this arc to an edge, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? DistanceTo(edge.ToArc(), tolerance)
                : DistanceTo(edge.ToLine(), tolerance);
    }
}
