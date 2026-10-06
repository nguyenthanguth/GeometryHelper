using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of the rim of an ellipse nearest a point, the shortest segment joining its rim to another shape, and which
    /// edge of a shape lies nearest it.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        /// <summary>
        /// Gets the point of the rim of this ellipse nearest a point, including for points inside it. The centre gives the
        /// end of the minor axis at t = 90°, and a point on the major axis inside, which two points of the rim are equally
        /// near, gives the one on the positive side. A circle's centre gives t = 90° too, where
        /// <see cref="GeoCircle2.GetClosestPointOnBoundary(GeoPoint2)"/> gives angle nought.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Ellipse2.GetClosestPointOnBoundary(this, point);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a segment, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Ellipse2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Ellipse2.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon) => Ellipse2.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Ellipse2.GetShortestLineTo(this, rect);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle) => Ellipse2.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a triangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Ellipse2.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on the boundary of a face, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, face, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on an edge, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => Ellipse2.GetShortestLineTo(this, edge);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, edge, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Ellipse2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on an arc, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Ellipse2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Ellipse2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Ellipse2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on another ellipse, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 other) => Ellipse2.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment leaving the rim of this ellipse and landing on another ellipse, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the edge of a polygon nearest this ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetClosestEdge(GeoPolygon2 polygon) => Ellipse2.GetClosestEdge(this, polygon);

        /// <summary>
        /// Gets the edge of a polygon nearest this ellipse, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetClosestEdge(GeoPolygon2 polygon, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, polygon, tolerance);

        /// <summary>
        /// Gets the edge of a polyline nearest this ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetClosestEdge(GeoPolyline2 polyline) => Ellipse2.GetClosestEdge(this, polyline);

        /// <summary>
        /// Gets the edge of a polyline nearest this ellipse, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetClosestEdge(GeoPolyline2 polyline, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, polyline, tolerance);

        /// <summary>
        /// Gets the edge of a rectangle nearest this ellipse, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoRectangle2 rect) => Ellipse2.GetClosestEdge(this, rect);

        /// <summary>
        /// Gets the edge of a rectangle nearest this ellipse, within a tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoRectangle2 rect, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, rect, tolerance);

        /// <summary>
        /// Gets the edge of a triangle nearest this ellipse, using the default tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoTriangle2 triangle) => Ellipse2.GetClosestEdge(this, triangle);

        /// <summary>
        /// Gets the edge of a triangle nearest this ellipse, within a tolerance.
        /// </summary>
        public GeoLine2 GetClosestEdge(GeoTriangle2 triangle, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, triangle, tolerance);

        /// <summary>
        /// Gets the edge of a curved loop nearest this ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop) => Ellipse2.GetClosestEdge(this, loop);

        /// <summary>
        /// Gets the edge of a curved loop nearest this ellipse, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, loop, tolerance);

        /// <summary>
        /// Gets the edge of a curved chain nearest this ellipse, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain) => Ellipse2.GetClosestEdge(this, chain);

        /// <summary>
        /// Gets the edge of a curved chain nearest this ellipse, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, Tolerance tolerance) => Ellipse2.GetClosestEdge(this, chain, tolerance);
    }
}
