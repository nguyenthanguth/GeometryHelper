using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an ellipse touches another shape.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        /// <summary>
        /// Determines whether this ellipse touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Ellipse2.CollidesWith(this, line);

        /// <summary>
        /// Determines whether this ellipse touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Ellipse2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline) => Ellipse2.CollidesWith(this, polyline);

        /// <summary>
        /// Determines whether this ellipse touches a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Ellipse2.CollidesWith(this, polyline, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 polygon) => Ellipse2.CollidesWith(this, polygon);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 polygon, Tolerance tolerance) => Ellipse2.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a rectangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Ellipse2.CollidesWith(this, rect);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a rectangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Ellipse2.CollidesWith(this, rect, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle) => Ellipse2.CollidesWith(this, triangle);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle, Tolerance tolerance) => Ellipse2.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Determines whether this ellipse reaches the material of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face) => Ellipse2.CollidesWith(this, face);

        /// <summary>
        /// Determines whether this ellipse reaches the material of a face, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Ellipse2.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => Ellipse2.CollidesWith(this, edge);

        /// <summary>
        /// Determines whether this ellipse touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance) => Ellipse2.CollidesWith(this, edge, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Ellipse2.CollidesWith(this, circle);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Ellipse2.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Ellipse2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this ellipse touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Ellipse2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain) => Ellipse2.CollidesWith(this, chain);

        /// <summary>
        /// Determines whether this ellipse touches a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Ellipse2.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop) => Ellipse2.CollidesWith(this, loop);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Ellipse2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps another ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse2 other) => Ellipse2.CollidesWith(this, other);

        /// <summary>
        /// Determines whether this ellipse touches or overlaps another ellipse, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.CollidesWith(this, other, tolerance);
    }
}
