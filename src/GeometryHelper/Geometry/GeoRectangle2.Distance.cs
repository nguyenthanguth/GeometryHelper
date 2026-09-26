using System;
using System.Linq;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a rectangle is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoRectangle2
    {
        /// <summary>
        /// Calculates the shortest Euclidean distance from this rectangle to a point.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this rectangle to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the outline, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint2)"/>
        /// reads this rectangle as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this rectangle to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(this, poly);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 GeoLine2) => Distance2.DistanceTo(this, GeoLine2);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to a circle.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest boundary distance from this rectangle to another rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to an arc.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Distance2.DistanceTo(this, arc);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Distance2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved loop.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(this, loop);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved chain.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(this, chain);

        /// <summary>
        /// Calculates the shortest distance from this rectangle to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(this, chain, tolerance);

        /// <summary>
        /// Gets the distance from this rectangle to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge)
            => edge.IsArc
                ? DistanceTo(edge.ToArc())
                : DistanceTo(edge.ToLine());
    }
}
