using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an ellipse is from another shape, and how deep inside it a point sits.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        /// <summary>
        /// Gets the distance from this ellipse to a point, nought for one inside it, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point) => Ellipse2.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this ellipse to a point, nought for one inside it, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Ellipse2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the distance from this ellipse to a point, negative for a point inside it, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the rim, whichever side of it the point is on, and the sign says which side:
        /// negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint2)"/> reads this ellipse as filled
        /// and so answers nought for a point inside, which is the one place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Ellipse2.SignedDistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this ellipse to a point, negative for a point inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Ellipse2.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a segment, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line) => Ellipse2.DistanceTo(this, line);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine2 line, Tolerance tolerance) => Ellipse2.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline) => Ellipse2.DistanceTo(this, polyline);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline2 polyline, Tolerance tolerance) => Ellipse2.DistanceTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon) => Ellipse2.DistanceTo(this, polygon);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon2 polygon, Tolerance tolerance) => Ellipse2.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a rectangle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect) => Ellipse2.DistanceTo(this, rect);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a rectangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRectangle2 rect, Tolerance tolerance) => Ellipse2.DistanceTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a triangle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle) => Ellipse2.DistanceTo(this, triangle);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle2 triangle, Tolerance tolerance) => Ellipse2.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to an edge, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge) => Ellipse2.DistanceTo(this, edge);

        /// <summary>
        /// Gets the shortest distance from this ellipse to an edge, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEdge2 edge, Tolerance tolerance) => Ellipse2.DistanceTo(this, edge, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a circle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle) => Ellipse2.DistanceTo(this, circle);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a circle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoCircle2 circle, Tolerance tolerance) => Ellipse2.DistanceTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to an arc, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc) => Ellipse2.DistanceTo(this, arc);

        /// <summary>
        /// Gets the shortest distance from this ellipse to an arc, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoArc2 arc, Tolerance tolerance) => Ellipse2.DistanceTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain) => Ellipse2.DistanceTo(this, chain);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public double DistanceTo(GeoPolylineArc2 chain, Tolerance tolerance) => Ellipse2.DistanceTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop) => Ellipse2.DistanceTo(this, loop);

        /// <summary>
        /// Gets the shortest distance from this ellipse to a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public double DistanceTo(GeoPolygonArc2 loop, Tolerance tolerance) => Ellipse2.DistanceTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest distance from this ellipse to another ellipse, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoEllipse2 other) => Ellipse2.DistanceTo(this, other);

        /// <summary>
        /// Gets the shortest distance from this ellipse to another ellipse, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.DistanceTo(this, other, tolerance);
    }
}
