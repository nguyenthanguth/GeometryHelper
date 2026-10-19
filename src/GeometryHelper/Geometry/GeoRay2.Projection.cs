using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a ray nearest a point, and the shortest segment joining a ray to another shape.
    /// </summary>
    /// <remarks>
    /// Both ends of a shortest segment sit on a boundary, and it leaves this ray. A ray runs on for ever, so one starting
    /// inside a closed shape crosses its outline on the way out, and the segment is the point where it does: the ray
    /// from (50, 50) along X inside the square 100 by 100 with a corner at the origin gets the segment of no length at
    /// (100, 50).
    /// </remarks>
    public readonly partial struct GeoRay2
    {
        /// <summary>
        /// Gets the closest point on this ray to a target point, clamped to the origin.
        /// </summary>
        /// <remarks>
        /// From the ray leaving the origin along X, the point (50, 40) lands at (50, 0) and the point (-30, 40) at the
        /// origin.
        /// </remarks>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point)
        {
            return Ray2.Project(this, point);
        }

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// It starts where <see cref="GetClosestPointOnBoundary(GeoPoint2)"/> lands and is exactly as long as
        /// <see cref="DistanceTo(GeoPoint2)"/>: from the ray leaving the origin along X to the point (-30, 40), it runs
        /// from the origin and is 50 long.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point) => Projection2.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a point, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPoint2 point, Tolerance tolerance) => Projection2.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a line segment, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a line segment, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving this ray and landing on <paramref name="line"/>, of no length at all where the two cross.</returns>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on another ray, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRay2 other) => Projection2.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on another ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Where the two cross it is the point where they do; otherwise one of its ends is an origin. The ray from the
        /// origin along X gets the segment from (50, 0) to (50, 30) to the ray from (50, 30) along Y.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoRay2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the circumference of a circle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Projection2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the circumference of a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(this, rect);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(this, rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly) => Projection2.GetShortestLineTo(this, poly);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 poly, Tolerance tolerance) => Projection2.GetShortestLineTo(this, poly, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this ray to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a curved loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop) => Projection2.GetShortestLineTo(this, loop);

        /// <summary>
        /// Gets the shortest segment joining this ray to a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, Tolerance tolerance) => Projection2.GetShortestLineTo(this, loop, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a curved chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this ray to a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on the boundary of a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);

        #region Triangles

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle) => Triangle2.GetShortestLineTo(triangle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on a triangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, Tolerance tolerance) => Triangle2.GetShortestLineTo(triangle, this, tolerance).Reverse();

        #endregion

        #region XLines

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ray and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoXLine2 xline, Tolerance tolerance) => Projection2.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
