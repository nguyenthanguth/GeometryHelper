using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a spline touches another shape in space.
    /// </summary>
    /// <remarks>
    /// The rule is the others': two shapes collide when they come within the point tolerance of each other, a flat shape and a body read as
    /// filled. It is read from <see cref="DistanceTo(GeoPoint3)"/> itself, so that a collision and a distance never disagree.
    /// </remarks>
    public sealed partial class GeoSpline3
    {
        /// <summary>
        /// Determines whether this spline touches a line segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a line segment, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a line segment, exactly when <see cref="DistanceTo(GeoLine3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => DistanceTo(line, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a ray, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a ray, exactly when <see cref="DistanceTo(GeoRay3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => DistanceTo(ray, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an infinite line, exactly when <see cref="DistanceTo(GeoXLine3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => DistanceTo(xline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => CollidesWith(plane, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a plane, exactly when <see cref="DistanceTo(GeoPlane3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => DistanceTo(plane, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a polyline, exactly when <see cref="DistanceTo(GeoPolyline3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polyline is null.</exception>
        public bool CollidesWith(GeoPolyline3 polyline, Tolerance tolerance) => DistanceTo(polyline, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon) => CollidesWith(polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a polygon, exactly when <see cref="DistanceTo(GeoPolygon3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => DistanceTo(polygon, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => CollidesWith(triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a triangle, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a triangle, exactly when <see cref="DistanceTo(GeoTriangle3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => DistanceTo(triangle, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a face, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a face, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a face, exactly when <see cref="DistanceTo(GeoFace3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => DistanceTo(face, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an axis-aligned box, exactly when <see cref="DistanceTo(GeoAabb3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => DistanceTo(box, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an oriented box, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box) => CollidesWith(box, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an oriented box, within a tolerance.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an oriented box, exactly when <see cref="DistanceTo(GeoObb3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the box is null.</exception>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => DistanceTo(box, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid) => CollidesWith(solid, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance: the point tolerance is the distance a collision is within, and the tolerance cuts the solid's openings in.</param>
        /// <returns>true if the spline comes within the point tolerance of a solid, exactly when <see cref="DistanceTo(GeoSolid3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => DistanceTo(solid, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a ball, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere) => CollidesWith(sphere, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a ball, exactly when <see cref="DistanceTo(GeoSphere3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoSphere3 sphere, Tolerance tolerance) => DistanceTo(sphere, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an arc, exactly when <see cref="DistanceTo(GeoArc3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => DistanceTo(arc, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a circle, exactly when <see cref="DistanceTo(GeoCircle3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => DistanceTo(circle, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an ellipse, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEllipse3 ellipse) => CollidesWith(ellipse, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an ellipse, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an ellipse, exactly when <see cref="DistanceTo(GeoEllipse3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoEllipse3 ellipse, Tolerance tolerance) => DistanceTo(ellipse, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches an edge, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of an edge, exactly when <see cref="DistanceTo(GeoEdge3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance) => DistanceTo(edge, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a curved chain, exactly when <see cref="DistanceTo(GeoPolylineArc3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the chain is null.</exception>
        public bool CollidesWith(GeoPolylineArc3 chain, Tolerance tolerance) => DistanceTo(chain, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether this spline touches a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the spline comes within the point tolerance of a curved loop, exactly when <see cref="DistanceTo(GeoPolygonArc3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the loop is null.</exception>
        public bool CollidesWith(GeoPolygonArc3 loop, Tolerance tolerance) => DistanceTo(loop, tolerance) <= tolerance.EqualPoint;
    }
}
