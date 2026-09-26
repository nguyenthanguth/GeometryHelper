using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a point is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoPoint2
    {
        /// <summary>
        /// Calculates the Euclidean distance to another point.
        /// </summary>
        public double DistanceTo(GeoPoint2 other) => Distance2.DistanceTo(this, other);

        /// <summary>
        /// Calculates the Euclidean distance to a line segment.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Distance2.DistanceTo(line, this);

        /// <summary>
        /// Calculates the Euclidean distance to a circle boundary.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Distance2.DistanceTo(circle, this);

        /// <summary>
        /// Calculates the Euclidean distance to a rectangle.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Distance2.DistanceTo(rect, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polygon boundary.
        /// </summary>
        public double DistanceTo(GeoPolygon2 poly) => Distance2.DistanceTo(poly, this);

        /// <summary>
        /// Calculates the Euclidean distance to a polyline.
        /// </summary>
        public double DistanceTo(GeoPolyline2 polyline) => Distance2.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this point to a arc, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Arc2.DistanceTo(arc, this);

        /// <summary>
        /// Gets the distance from this point to a arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Arc2.DistanceTo(arc, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a face, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoFace2 face) => Distance2.DistanceTo(face, this);

        /// <summary>
        /// Gets the distance from this point to a face, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoFace2 face, Tolerance tolerance) => Distance2.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved loop, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop) => Distance2.DistanceTo(loop, this);

        /// <summary>
        /// Gets the distance from this point to a curved loop, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.DistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to a curved chain, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain) => Distance2.DistanceTo(chain, this);

        /// <summary>
        /// Gets the distance from this point to a curved chain, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Distance2.DistanceTo(chain, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a circle, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoCircle2 circle) => Distance2.SignedDistanceTo(circle, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a circle, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoCircle2 circle, Tolerance tolerance) => Distance2.SignedDistanceTo(circle, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a rectangle, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoRectangle2 rect) => Distance2.SignedDistanceTo(rect, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a rectangle, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Distance2.SignedDistanceTo(rect, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a polygon, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPolygon2 poly) => Distance2.SignedDistanceTo(poly, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a polygon, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPolygon2 poly, Tolerance tolerance) => Distance2.SignedDistanceTo(poly, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a curved loop, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPolygonArc2 loop) => Distance2.SignedDistanceTo(loop, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a curved loop, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Distance2.SignedDistanceTo(loop, this, tolerance);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a face, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoFace2 face) => Distance2.SignedDistanceTo(face, this);

        /// <summary>
        /// Gets the signed distance from this point to the boundary of a face, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoFace2 face, Tolerance tolerance) => Distance2.SignedDistanceTo(face, this, tolerance);

        /// <summary>
        /// Gets the distance from this point to an edge.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge)
            => edge.IsArc
                ? DistanceTo(edge.ToArc())
                : DistanceTo(edge.ToLine());
    }
}
