using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where the rim of an ellipse crosses another shape.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a segment, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Ellipse2.GetIntersections(this, line);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Ellipse2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Ellipse2.GetIntersections(this, polyline);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Ellipse2.GetIntersections(this, polyline, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon) => Ellipse2.GetIntersections(this, polygon);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon, Tolerance tolerance) => Ellipse2.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Ellipse2.GetIntersections(this, rect);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Ellipse2.GetIntersections(this, rect, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle) => Ellipse2.GetIntersections(this, triangle);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle, Tolerance tolerance) => Ellipse2.GetIntersections(this, triangle, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Ellipse2.GetIntersections(this, face);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses the boundary of a face, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Ellipse2.GetIntersections(this, face, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an edge, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => Ellipse2.GetIntersections(this, edge);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance) => Ellipse2.GetIntersections(this, edge, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Ellipse2.GetIntersections(this, circle);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Ellipse2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Ellipse2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Ellipse2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Ellipse2.GetIntersections(this, chain);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Ellipse2.GetIntersections(this, chain, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Ellipse2.GetIntersections(this, loop);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Ellipse2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses another ellipse, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEllipse2 other) => Ellipse2.GetIntersections(this, other);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses another ellipse, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEllipse2 other, Tolerance tolerance) => Ellipse2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an edge, using the default tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, edge, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, edge, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, arc, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, arc, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, chain, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, chain, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, loop, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, loop, out intersections, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses another ellipse, using the default tolerance.
        /// </summary>
        /// <param name="other">The ellipse.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse2 other, out GeoPoint2[] intersections) => Ellipse2.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses another ellipse, within a tolerance.
        /// </summary>
        /// <param name="other">The ellipse.</param>
        /// <param name="intersections">The crossings when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse2 other, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(this, other, out intersections, tolerance);
    }
}
