using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an infinite line nearest a point, and the shortest segment joining a line to another shape.
    /// </summary>
    /// <remarks>
    /// Both ends of a shortest segment sit on a boundary, and it leaves this line. A line runs on both ways for ever,
    /// so one through a closed shape crosses its outline, and the segment is the point where it does: the line through
    /// (50, 50) along X, in the square 100 by 100 with a corner at the origin, gets the segment of no length at (0, 50)
    /// or (100, 50).
    /// </remarks>
    public readonly partial struct GeoXLine2
    {
        /// <summary>
        /// Gets the closest point on this line to a target point: the foot of the perpendicular.
        /// </summary>
        /// <remarks>
        /// From the line through the origin along X, the points (50, 40) and (-30, 40) land at (50, 0) and (-30, 0): unlike a
        /// ray, a line has no start to stop at.
        /// </remarks>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point)
        {
            return XLine2.Project(this, point);
        }

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a point, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// It starts where <see cref="GetClosestPointOnBoundary(GeoPoint2)"/> lands and is exactly as long as
        /// <see cref="DistanceTo(GeoPoint2)"/>: from the line through the origin along X to the point (-30, 40), it
        /// runs from (-30, 0) and is 40 long.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => Projection2.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a line segment, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a line segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ray, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray) => GetShortestLineTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Where the two cross it is the point where they do; otherwise it runs from the line to the origin of the ray.
        /// The line through the origin along X gets the segment from (50, 0) to (50, 30) to the ray from (50, 30) along
        /// Y.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRay2 ray, Tolerance tolerance) => Projection2.GetShortestLineTo(this, ray, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on another infinite line, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on another infinite line, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an arc, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => GetShortestLineTo(arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => GetShortestLineTo(circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an ellipse, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse) => GetShortestLineTo(ellipse, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an ellipse, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, Tolerance tolerance) => Ellipse2.GetShortestLineTo(ellipse, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an edge, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance) => edge.GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => GetShortestLineTo(poly, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A line through the square runs on through its outline, so the segment is of no length, at one of the
        /// crossings; a line beside it gets the segment from the line to the nearest corner.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetShortestLineTo(this, poly, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => GetShortestLineTo(chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => GetShortestLineTo(loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a rectangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => GetShortestLineTo(rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle) => GetShortestLineTo(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a triangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.GetShortestLineTo(triangle, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();
    }
}
