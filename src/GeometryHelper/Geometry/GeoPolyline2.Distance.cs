using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a polyline is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolyline2
    {
        /// <summary>
        /// Calculates the shortest Euclidean distance from this polyline to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(this, rect);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(this, circle);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(this, poly);

        /// <summary>
        /// Calculates the shortest distance between two polylines.
        /// </summary>
        public double DistanceTo(GeoPolyline2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this polyline to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from this polyline to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Calculates the shortest distance from this polyline to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the distance from this polyline to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge)
            => edge.IsArc
                ? DistanceTo(edge.ToArc())
                : DistanceTo(edge.ToLine());
    }
}
