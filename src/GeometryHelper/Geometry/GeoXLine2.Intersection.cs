using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an infinite line crosses another shape.
    /// </summary>
    /// <remarks>
    /// The crossings are those of the boundary, in the order and with the repeats left out that a segment's are. The
    /// line through (-50, 50) along X crosses the square 100 by 100 with a corner at the origin at (0, 50) and at
    /// (100, 50), and a line through the middle of the square gives the same two whatever its origin.
    /// </remarks>
    public readonly partial struct GeoXLine2
    {
        /// <summary>
        /// Gets the points where this infinite line crosses an arc, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Arc2.GetIntersections(arc, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a circle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (-100, 0) along X crosses the circle of radius 50 about the origin at (-50, 0) and (50, 0),
        /// and so does the line through (100, 0) along X.
        /// </remarks>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(circle, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an ellipse, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEllipse2 ellipse) => GetIntersections(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an ellipse, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, Tolerance tolerance) => Ellipse2.GetIntersections(ellipse, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses an edge, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance) => edge.GetIntersections(this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => GetIntersections(poly, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (-50, 50) along X crosses the square 100 by 100 with a corner at the origin at (0, 50) and
        /// (100, 50).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a rectangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => GetIntersections(rect, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a triangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle) => GetIntersections(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a triangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.GetIntersections(triangle, this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2 intersection) => TryIntersectWith(line, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful, placed on the segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(line, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoRay2 ray, out GeoPoint2 intersection) => TryIntersectWith(ray, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point if successful, placed on the ray, never behind its origin.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A ray parallel to the line, within the angle tolerance, is refused, even where it lies along the line.
        /// </remarks>
        public bool TryIntersectWith(GeoRay2 ray, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(ray, this, out intersection, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses another infinite line, using the default tolerance.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoXLine2 other, out GeoPoint2 intersection) => TryIntersectWith(other, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses another infinite line, within a tolerance.
        /// </summary>
        /// <param name="other">The other line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Two lines parallel by angle are refused, as neither has a length to measure a drift over, even where they
        /// are the same line. The line through the origin along X and the one through (50, -50) along Y cross at
        /// (50, 0). The point is the same to the bit whichever of the two asks.
        /// </remarks>
        public bool TryIntersectWith(GeoXLine2 other, out GeoPoint2 intersection, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, out intersection, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses an arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(arc, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(circle, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses an ellipse, using the default tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoEllipse2 ellipse, out GeoPoint2[] intersections) => TryIntersectWith(ellipse, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoEllipse2 ellipse, out GeoPoint2[] intersections, Tolerance tolerance) => Ellipse2.TryIntersectWith(ellipse, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(polyline, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(chain, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a rectangle, using the default tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections) => TryIntersectWith(rect, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoRectangle2 rect, out GeoPoint2[] intersections, Tolerance tolerance) => rect.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a triangle, using the default tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections) => TryIntersectWith(triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoTriangle2 triangle, out GeoPoint2[] intersections, Tolerance tolerance) => triangle.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses a face, using the default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections) => TryIntersectWith(face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool TryIntersectWith(GeoFace2 face, out GeoPoint2[] intersections, Tolerance tolerance) => face.TryIntersectWith(this, out intersections, tolerance);
    }
}
