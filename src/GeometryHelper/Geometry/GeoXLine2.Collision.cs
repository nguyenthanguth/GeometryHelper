using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an infinite line touches another shape.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled: a line reaching its material collides with it. The line through (50, 50) along
    /// X runs through the square 100 by 100 with a corner at the origin and collides with it; the line through
    /// (-50, 150) along X passes above it and does not.
    /// </remarks>
    public readonly partial struct GeoXLine2
    {
        /// <summary>
        /// Determines whether this infinite line touches a line segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying along the line touches it along a length and crosses it nowhere, and still collides with it.
        /// </remarks>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Collision2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 ray, Tolerance tolerance) => Collision2.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches another infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine2 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches another infinite line, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through the origin along X and the one through (50, -50) along Y cross at (50, 0); two lines side
        /// by side touch only where they are within the point tolerance of each other.
        /// </remarks>
        public bool CollidesWith(GeoXLine2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse2 ellipse) => CollidesWith(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an ellipse, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse2 ellipse, Tolerance tolerance) => Ellipse2.CollidesWith(ellipse, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance) => edge.CollidesWith(this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 poly) => CollidesWith(poly, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (500, 50) along X collides with the square 100 by 100 with a corner at the origin, though
        /// it starts far from it: nothing of the line stops short.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(poly, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Collision2.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a rectangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => CollidesWith(rect, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a rectangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(rect, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.CollidesWith(triangle, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);
    }
}
