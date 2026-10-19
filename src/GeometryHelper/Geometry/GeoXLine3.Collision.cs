using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an infinite line touches another shape in space.
    /// </summary>
    /// <remarks>
    /// A closed shape is read as filled: a line reaching its material collides with it. The line through (50, 50, 10) along X
    /// runs through the box 100 by 100 by 20 with a corner at the origin and collides with it; the line through (-50, 150, 10)
    /// along X passes beside it and does not. Two shapes touch where the exact gap between them is no more than the point
    /// tolerance, so a line 0.0005 above the top of the box touches it and one 0.002 above does not.
    /// </remarks>
    public readonly partial struct GeoXLine3
    {
        /// <summary>
        /// Determines whether this infinite line touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid) => CollidesWith(solid, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through (500, 50, 10) along X collides with the box 100 by 100 by 20 with a corner at the origin, though it
        /// starts far from it: nothing of the line stops short.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => CollidesWith(polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A hole is not material: the line through (50, 50, 10) along -Z passes through the hole 40 by 40 in the middle of the
        /// square 100 by 100 in the floor and collides with nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => CollidesWith(plane, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A line whose origin lies within the planar tolerance of the plane touches it. Otherwise it touches where it crosses:
        /// the line through (0, 0, 10) along (1, 0, 0.0001) touches the floor a hundred thousand behind its origin; at a sine
        /// of 1E-9 or less a crossing further out than a double can place within the point tolerance is refused, so the
        /// line through (0, 0, 100) along (1, 0, -1E-11) does not touch the floor.
        /// </remarks>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Collision3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a line segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying along the line touches it along a length and crosses it nowhere, and still collides with it.
        /// </remarks>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches another infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches another infinite line, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The line through the origin along X and the one through (50, -50, 0) along Y cross at (50, 0, 0); the one through
        /// (50, -50, 10) along Y passes 10 over it and does not touch it. Two lines side by side touch only where they are
        /// within the point tolerance of each other.
        /// </remarks>
        public bool CollidesWith(GeoXLine3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches the rim of a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse3 ellipse) => CollidesWith(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches the rim of an ellipse, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse3 ellipse, Tolerance tolerance) => Ellipse3.CollidesWith(ellipse, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance) => edge.CollidesWith(this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance) => Collision3.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => ArcChain3.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => ArcChain3.CollidesWith(loop, this, tolerance);
    }
}
