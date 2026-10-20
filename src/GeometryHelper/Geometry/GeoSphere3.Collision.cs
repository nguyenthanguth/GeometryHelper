using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a ball touches another shape.
    /// </summary>
    /// <remarks>
    /// A ball touches a shape where the exact gap between them is no more than the point tolerance, so the answer is that of
    /// <c>DistanceTo</c> read against it: a shape inside the ball collides with it, a shape within the tolerance of its surface collides with it, and the ball
    /// of radius 50 about the origin touches the plane 50.0005 above it and not the plane 50.002 above it. A point answers with
    /// <see cref="Contains(GeoPoint3)"/>.
    /// </remarks>
    public readonly partial struct GeoSphere3
    {
        /// <summary>
        /// Determines whether this ball touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => DistanceTo(line, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => DistanceTo(ray) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => DistanceTo(xline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => DistanceTo(arc, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => DistanceTo(circle, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse3 ellipse) => CollidesWith(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an ellipse, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse3 ellipse, Tolerance tolerance) => DistanceTo(ellipse, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance) => DistanceTo(edge, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance) => DistanceTo(polyline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => CollidesWith(polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => DistanceTo(polygon, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => DistanceTo(chain, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => DistanceTo(loop, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => DistanceTo(triangle) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => DistanceTo(face, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => CollidesWith(plane, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a plane, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => DistanceTo(plane) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => DistanceTo(box) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches an oriented box, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public bool CollidesWith(GeoObb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches an oriented box, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => DistanceTo(box) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid) => CollidesWith(solid, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches a solid, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => DistanceTo(solid, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this ball touches another ball, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this ball touches another ball, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two balls touch where the distance between their centres is no more than the two radii and the tolerance: the ball of radius 50 about the origin touches the ball of radius 10 about (60.0005, 0, 0), and a ball inside it.
        /// </remarks>
        public bool CollidesWith(GeoSphere3 other, Tolerance tolerance) => DistanceTo(other) <= tolerance.EqualPoint;
    }
}
