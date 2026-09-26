using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved chain is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolylineArc2
    {
        /// <summary>
        /// Gets the distance from the chain to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from the chain to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a straight segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance from the chain to a straight segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Distance2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Gets the distance from the chain to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Gets the distance from the chain to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a straight chain.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(this, polyline);

        /// <summary>
        /// Gets the distance from the chain to a straight chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Distance2.DistanceTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a straight loop.
        /// </summary>
        public double DistanceTo(GeoPolygon2 polygon) => Distance2.DistanceTo(this, polygon);

        /// <summary>
        /// Gets the distance from the chain to a straight loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygon2 polygon, Tolerance tolerance) => Distance2.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a chain that may curve.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from the chain to a chain that may curve, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 other, Tolerance tolerance) => Distance2.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a loop that may curve.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(this, loop);

        /// <summary>
        /// Gets the distance from the chain to a loop that may curve, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Gets the distance from the chain to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the distance from the chain to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this curved chain to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the shortest distance from this curved chain to a rectangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.DistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the distance from this curved chain to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => DistanceTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this curved chain to an edge, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? DistanceTo(edge.ToArc(), tolerance)
                : DistanceTo(edge.ToLine(), tolerance);
    }
}
