using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a spline touches another shape in the plane.
    /// </summary>
    /// <remarks>
    /// The rule is the others': two shapes collide when they come within the point tolerance of each other, a closed shape read as
    /// filled. It is read from <see cref="DistanceTo(GeoPoint2)"/> itself, so that a collision and a distance never disagree.
    /// </remarks>
    public sealed partial class GeoSpline2
    {
        /// <summary>
        /// Determines whether this spline touches a line segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a line segment, exactly when <see cref="DistanceTo(GeoLine2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => DistanceTo(line, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay2 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a ray, exactly when <see cref="DistanceTo(GeoRay2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoRay2 ray, Tolerance tolerance) => DistanceTo(ray, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine2 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an infinite line, exactly when <see cref="DistanceTo(GeoXLine2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoXLine2 xline, Tolerance tolerance) => DistanceTo(xline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a polyline, exactly when <see cref="DistanceTo(GeoPolyline2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => DistanceTo(polyline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 polygon) => CollidesWith(polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a polygon, exactly when <see cref="DistanceTo(GeoPolygon2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon2 polygon, Tolerance tolerance) => DistanceTo(polygon, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a rectangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => CollidesWith(rect, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a rectangle, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a rectangle, exactly when <see cref="DistanceTo(GeoRectangle2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => DistanceTo(rect, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle2 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a triangle, exactly when <see cref="DistanceTo(GeoTriangle2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoTriangle2 triangle, Tolerance tolerance) => DistanceTo(triangle, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a face, exactly when <see cref="DistanceTo(GeoFace2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => DistanceTo(face, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an arc, exactly when <see cref="DistanceTo(GeoArc2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => DistanceTo(arc, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a circle, exactly when <see cref="DistanceTo(GeoCircle2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => DistanceTo(circle, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse2 ellipse) => CollidesWith(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an ellipse, exactly when <see cref="DistanceTo(GeoEllipse2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoEllipse2 ellipse, Tolerance tolerance) => DistanceTo(ellipse, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an edge, exactly when <see cref="DistanceTo(GeoEdge2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance) => DistanceTo(edge, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a curved chain, exactly when <see cref="DistanceTo(GeoPolylineArc2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => DistanceTo(chain, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a curved loop, exactly when <see cref="DistanceTo(GeoPolygonArc2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the curved loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => DistanceTo(loop, tolerance) <= tolerance.EqualPoint;
    }
}
