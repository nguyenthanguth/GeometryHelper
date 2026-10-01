using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What a triangle of the plane meets, how far off it is, and the rest of what the shapes of the plane ask each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A triangle is the polygon of its three corners, and every question here is asked of that polygon, so a triangle
    /// and the polygon <see cref="GeoTriangle2.ToPolygon()"/> makes of it give the same answers. The one thing a polygon
    /// cannot be is a triangle two of whose corners stand on each other, within the point tolerance: a polygon has three
    /// distinct corners at the least. Such a triangle has no width, and is asked as the segment between its two corners
    /// furthest apart, which is all of it there is. The walk along the edges is the one exception: it goes round the
    /// corners as they stand, A to B to C and back, so it reads no tolerance and a triangle of one point is walked too.
    /// </para>
    /// <para>
    /// Each pair is worked out here one way only, the triangle first; the other shapes ask from their side by coming
    /// here, and turn a shortest segment round so that it leaves them.
    /// </para>
    /// </remarks>
    public static class Triangle2
    {
        /// <summary>
        /// Reads a triangle as the polygon of its corners, or, when two of them stand within the point tolerance of each
        /// other, as the segment between the two furthest apart.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance that decides whether two corners stand on each other.</param>
        /// <param name="polygon">The polygon, when the method returns true.</param>
        /// <param name="hull">The segment the triangle is, when the method returns false.</param>
        /// <returns>true when the triangle has three corners apart.</returns>
        internal static bool TryAsPolygon(GeoTriangle2 triangle, Tolerance tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
        {
            if (tolerance == null)
            {
                throw new ArgumentNullException(nameof(tolerance));
            }

            GeoPoint2 a = triangle.A, b = triangle.B, c = triangle.C;

            if (!a.IsEqualTo(b, tolerance) && !b.IsEqualTo(c, tolerance) && !c.IsEqualTo(a, tolerance))
            {
                // Already free of corners on each other, so the polygon is built as it stands rather than filtered again
                // against the global tolerance.
                polygon = new GeoPolygon2(new[] { a, b, c }, 3);
                hull = default;
                return true;
            }

            double ab = a.GetDistanceSquaredTo(b), bc = b.GetDistanceSquaredTo(c), ca = c.GetDistanceSquaredTo(a);
            polygon = null;
            hull = ab >= bc && ab >= ca ? new GeoLine2(a, b) : bc >= ca ? new GeoLine2(b, c) : new GeoLine2(c, a);
            return false;
        }

        /// <summary>
        /// Where two segments meet, as a list: the point they cross at, or none.
        /// </summary>
        private static GeoPoint2[] Meet(GeoLine2 hull, GeoLine2 line, Tolerance tolerance)
            => hull.TryIntersectWith(line, out GeoPoint2 point, tolerance) ? new[] { point } : Array.Empty<GeoPoint2>();

        #region Collision

        /// <summary>
        /// Checks whether a triangle touches an arc, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoArc2 arc) => CollidesWith(triangle, arc, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches an arc, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoArc2 arc, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(arc, tolerance)
                : hull.CollidesWith(arc, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a curved loop, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolygonArc2 loop) => CollidesWith(triangle, loop, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a curved loop, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolygonArc2 loop, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(loop, tolerance)
                : hull.CollidesWith(loop, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a curved chain, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolylineArc2 chain) => CollidesWith(triangle, chain, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a curved chain, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolylineArc2 chain, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(chain, tolerance)
                : hull.CollidesWith(chain, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a rectangle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoRectangle2 rect) => CollidesWith(triangle, rect, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a rectangle, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoRectangle2 rect, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(rect, tolerance)
                : hull.CollidesWith(rect, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a segment, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoLine2 line) => CollidesWith(triangle, line, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a segment, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoLine2 line, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(line, tolerance)
                : hull.CollidesWith(line, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a polygon, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolygon2 polygon) => CollidesWith(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a polygon, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolygon2 polygon, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(polygon, tolerance)
                : hull.CollidesWith(polygon, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a circle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoCircle2 circle) => CollidesWith(triangle, circle, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a circle, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoCircle2 circle, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(circle, tolerance)
                : hull.CollidesWith(circle, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a polyline, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolyline2 polyline) => CollidesWith(triangle, polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a polyline, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoPolyline2 polyline, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(polyline, tolerance)
                : hull.CollidesWith(polyline, tolerance);

        /// <summary>
        /// Checks whether a triangle touches a face, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoFace2 face) => CollidesWith(triangle, face, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches a face, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoFace2 face, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(face, tolerance)
                : hull.CollidesWith(face, tolerance);

        /// <summary>
        /// Checks whether a triangle touches an edge, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoEdge2 edge) => CollidesWith(triangle, edge, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches an edge, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoEdge2 edge, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.CollidesWith(edge, tolerance)
                : hull.CollidesWith(edge, tolerance);

        /// <summary>
        /// Checks whether a triangle touches another triangle, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoTriangle2 other) => CollidesWith(triangle, other, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle touches another triangle, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle2 triangle, GeoTriangle2 other, Tolerance tolerance)
            => TryAsPolygon(other, tolerance, out GeoPolygon2 otherPolygon, out GeoLine2 otherHull)
                ? CollidesWith(triangle, otherPolygon, tolerance)
                : CollidesWith(triangle, otherHull, tolerance);

        #endregion

        #region Distance

        /// <summary>
        /// Gets the distance from a triangle to a point: nought for a point within it.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPoint2 point)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(point)
                : hull.DistanceTo(point);

        /// <summary>
        /// Gets the distance from a triangle to a point, negative for a point within it, using the default tolerance.
        /// </summary>
        public static double SignedDistanceTo(GeoTriangle2 triangle, GeoPoint2 point) => SignedDistanceTo(triangle, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a triangle to a point, negative for a point within it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle of no width has no inside, so no point is within it and the distance is never negative.
        /// </remarks>
        public static double SignedDistanceTo(GeoTriangle2 triangle, GeoPoint2 point, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? polygon.SignedDistanceTo(point, tolerance)
                : hull.DistanceTo(point);

        /// <summary>
        /// Gets the distance from a triangle to a polygon.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolygon2 polygon)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(polygon)
                : hull.DistanceTo(polygon);

        /// <summary>
        /// Gets the distance from a triangle to a segment.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoLine2 line)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(line)
                : hull.DistanceTo(line);

        /// <summary>
        /// Gets the distance from a triangle to a circle.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoCircle2 circle)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(circle)
                : hull.DistanceTo(circle);

        /// <summary>
        /// Gets the distance from a triangle to a polyline.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolyline2 polyline)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(polyline)
                : hull.DistanceTo(polyline);

        /// <summary>
        /// Gets the distance from a triangle to a rectangle.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoRectangle2 rect)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(rect)
                : hull.DistanceTo(rect);

        /// <summary>
        /// Gets the distance from a triangle to an edge.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoEdge2 edge)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(edge)
                : hull.DistanceTo(edge);

        /// <summary>
        /// Gets the distance from a triangle to another triangle.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoTriangle2 other)
            => TryAsPolygon(other, Tolerance.Global, out GeoPolygon2 otherPolygon, out GeoLine2 otherHull)
                ? DistanceTo(triangle, otherPolygon)
                : DistanceTo(triangle, otherHull);

        /// <summary>
        /// Gets the distance from a triangle to an arc, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoArc2 arc) => DistanceTo(triangle, arc, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a triangle to an arc, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoArc2 arc, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(arc, tolerance)
                : hull.DistanceTo(arc, tolerance);

        /// <summary>
        /// Gets the distance from a triangle to a curved loop, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolygonArc2 loop) => DistanceTo(triangle, loop, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a triangle to a curved loop, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolygonArc2 loop, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(loop, tolerance)
                : hull.DistanceTo(loop, tolerance);

        /// <summary>
        /// Gets the distance from a triangle to a curved chain, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolylineArc2 chain) => DistanceTo(triangle, chain, Tolerance.Global);

        /// <summary>
        /// Gets the distance from a triangle to a curved chain, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoTriangle2 triangle, GeoPolylineArc2 chain, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.DistanceTo(chain, tolerance)
                : hull.DistanceTo(chain, tolerance);

        #endregion

        #region Intersection

        /// <summary>
        /// Gets every point where the edges of a triangle meet an arc, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoArc2 arc) => GetIntersections(triangle, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoArc2 arc, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(arc, tolerance)
                : hull.GetIntersections(arc, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a curved loop, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolygonArc2 loop) => GetIntersections(triangle, loop, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a curved loop, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolygonArc2 loop, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(loop, tolerance)
                : hull.GetIntersections(loop, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a curved chain, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolylineArc2 chain) => GetIntersections(triangle, chain, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a curved chain, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolylineArc2 chain, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(chain, tolerance)
                : hull.GetIntersections(chain, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a rectangle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoRectangle2 rect) => GetIntersections(triangle, rect, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a rectangle, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoRectangle2 rect, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(rect, tolerance)
                : hull.GetIntersections(rect, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a segment, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoLine2 line) => GetIntersections(triangle, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a segment, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoLine2 line, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(line, tolerance)
                : Meet(hull, line, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a polygon, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolygon2 polygon) => GetIntersections(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a polygon, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolygon2 polygon, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(polygon, tolerance)
                : hull.GetIntersections(polygon, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoCircle2 circle) => GetIntersections(triangle, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a circle, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoCircle2 circle, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(circle, tolerance)
                : hull.GetIntersections(circle, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a polyline, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolyline2 polyline) => GetIntersections(triangle, polyline, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a polyline, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoPolyline2 polyline, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(polyline, tolerance)
                : hull.GetIntersections(polyline, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a face, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoFace2 face) => GetIntersections(triangle, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet a face, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoFace2 face, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(face, tolerance)
                : hull.GetIntersections(face, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet an edge, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoEdge2 edge) => GetIntersections(triangle, edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet an edge, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoEdge2 edge, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetIntersections(edge, tolerance)
                : hull.GetIntersections(edge, tolerance);

        /// <summary>
        /// Gets every point where the edges of a triangle meet another triangle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoTriangle2 other) => GetIntersections(triangle, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where the edges of a triangle meet another triangle, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoTriangle2 other, Tolerance tolerance)
            => TryAsPolygon(other, tolerance, out GeoPolygon2 otherPolygon, out GeoLine2 otherHull)
                ? GetIntersections(triangle, otherPolygon, tolerance)
                : GetIntersections(triangle, otherHull, tolerance);

        #endregion

        #region Projection

        /// <summary>
        /// Gets the point on the edges of a triangle closest to a point.
        /// </summary>
        public static GeoPoint2 GetClosestPointOnBoundary(GeoTriangle2 triangle, GeoPoint2 point)
            => TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? polygon.GetClosestPointOnBoundary(point)
                : hull.GetClosestPointOnBoundary(point);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a segment, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoLine2 line) => GetShortestLineTo(triangle, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a segment, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoLine2 line, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(line, tolerance)
                : hull.GetShortestLineTo(line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a circle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoCircle2 circle) => GetShortestLineTo(triangle, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a circle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoCircle2 circle, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(circle, tolerance)
                : hull.GetShortestLineTo(circle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a rectangle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoRectangle2 rect) => GetShortestLineTo(triangle, rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a rectangle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoRectangle2 rect, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(rect, tolerance)
                : hull.GetShortestLineTo(rect, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a polyline, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolyline2 polyline) => GetShortestLineTo(triangle, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a polyline, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolyline2 polyline, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(polyline, tolerance)
                : hull.GetShortestLineTo(polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a polygon, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolygon2 polygon) => GetShortestLineTo(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a polygon, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolygon2 polygon, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(polygon, tolerance)
                : hull.GetShortestLineTo(polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on an arc, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoArc2 arc) => GetShortestLineTo(triangle, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on an arc, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoArc2 arc, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(arc, tolerance)
                : hull.GetShortestLineTo(arc, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a curved loop, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolygonArc2 loop) => GetShortestLineTo(triangle, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a curved loop, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolygonArc2 loop, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(loop, tolerance)
                : hull.GetShortestLineTo(loop, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a curved chain, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolylineArc2 chain) => GetShortestLineTo(triangle, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a curved chain, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoPolylineArc2 chain, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(chain, tolerance)
                : hull.GetShortestLineTo(chain, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a face, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoFace2 face) => GetShortestLineTo(triangle, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on a face, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoFace2 face, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(face, tolerance)
                : hull.GetShortestLineTo(face, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on an edge, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoEdge2 edge) => GetShortestLineTo(triangle, edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on an edge, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoEdge2 edge, Tolerance tolerance)
            => TryAsPolygon(triangle, tolerance, out GeoPolygon2 shape, out GeoLine2 hull)
                ? shape.GetShortestLineTo(edge, tolerance)
                : hull.GetShortestLineTo(edge, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on another triangle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoTriangle2 other) => GetShortestLineTo(triangle, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a triangle and landing on another triangle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoTriangle2 other, Tolerance tolerance)
            => TryAsPolygon(other, tolerance, out GeoPolygon2 otherPolygon, out GeoLine2 otherHull)
                ? GetShortestLineTo(triangle, otherPolygon, tolerance)
                : GetShortestLineTo(triangle, otherHull, tolerance);

        /// <summary>
        /// Gets the edge of a triangle closest to a point.
        /// </summary>
        /// <remarks>
        /// A triangle of no width is asked edge by edge, the first of the closest kept.
        /// </remarks>
        public static GeoLine2 GetClosestEdge(GeoTriangle2 triangle, GeoPoint2 point)
        {
            if (TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 polygon, out _))
            {
                return ClosestEdge2.GetClosestEdge(polygon, point);
            }

            GeoLine2 closest = triangle.GetEdgeAt(0);
            double best = closest.DistanceTo(point);

            for (int i = 1; i < 3; i++)
            {
                GeoLine2 edge = triangle.GetEdgeAt(i);
                double distance = edge.DistanceTo(point);

                if (distance < best)
                {
                    best = distance;
                    closest = edge;
                }
            }

            return closest;
        }

        /// <summary>
        /// Gets the edge of a triangle closest to a segment.
        /// </summary>
        /// <remarks>
        /// A triangle of no width is asked edge by edge, the first of the closest kept.
        /// </remarks>
        public static GeoLine2 GetClosestEdge(GeoTriangle2 triangle, GeoLine2 line)
        {
            if (TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 polygon, out _))
            {
                return ClosestEdge2.GetClosestEdge(polygon, line);
            }

            GeoLine2 closest = triangle.GetEdgeAt(0);
            double best = closest.DistanceTo(line);

            for (int i = 1; i < 3; i++)
            {
                GeoLine2 edge = triangle.GetEdgeAt(i);
                double distance = edge.DistanceTo(line);

                if (distance < best)
                {
                    best = distance;
                    closest = edge;
                }
            }

            return closest;
        }

        /// <summary>
        /// Gets the edge of a triangle closest to a circle.
        /// </summary>
        /// <remarks>
        /// A triangle of no width is asked edge by edge, the first of the closest kept.
        /// </remarks>
        public static GeoLine2 GetClosestEdge(GeoTriangle2 triangle, GeoCircle2 circle)
        {
            if (TryAsPolygon(triangle, Tolerance.Global, out GeoPolygon2 polygon, out _))
            {
                return ClosestEdge2.GetClosestEdge(polygon, circle);
            }

            GeoLine2 closest = triangle.GetEdgeAt(0);
            double best = closest.DistanceTo(circle);

            for (int i = 1; i < 3; i++)
            {
                GeoLine2 edge = triangle.GetEdgeAt(i);
                double distance = edge.DistanceTo(circle);

                if (distance < best)
                {
                    best = distance;
                    closest = edge;
                }
            }

            return closest;
        }

        #endregion

        #region Containment

        /// <summary>
        /// Checks whether a triangle holds a segment whole, its edges included, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoTriangle2 triangle, GeoLine2 line) => Contains(triangle, line, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle holds a segment whole, its edges included, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle of no width holds what lies along it, every corner of it on the segment the triangle is.
        /// </remarks>
        public static bool Contains(GeoTriangle2 triangle, GeoLine2 line, Tolerance tolerance)
        {
            if (TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull))
            {
                return Containment2.Contains(polygon, line, tolerance);
            }

            return Containment2.IsPointOn(hull, line.StartPoint, tolerance) && Containment2.IsPointOn(hull, line.EndPoint, tolerance);
        }

        /// <summary>
        /// Checks whether a triangle holds a polyline whole, its edges included, using the default tolerance.
        /// </summary>
        public static bool Contains(GeoTriangle2 triangle, GeoPolyline2 polyline) => Contains(triangle, polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether a triangle holds a polyline whole, its edges included, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle of no width holds what lies along it, every corner of it on the segment the triangle is.
        /// </remarks>
        public static bool Contains(GeoTriangle2 triangle, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (TryAsPolygon(triangle, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull))
            {
                return Containment2.Contains(polygon, polyline, tolerance);
            }

            foreach (GeoPoint2 vertex in polyline.Vertices)
            {
                if (!Containment2.IsPointOn(hull, vertex, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Parallel

        /// <summary>
        /// Checks whether an edge of a triangle runs parallel to a segment, using the default tolerance.
        /// </summary>
        public static bool IsParallel(GeoTriangle2 triangle, GeoLine2 line) => IsParallel(triangle, line, Tolerance.Global);

        /// <summary>
        /// Checks whether an edge of a triangle runs parallel to a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An edge shorter than the point tolerance has no direction, and is parallel to nothing.
        /// </remarks>
        public static bool IsParallel(GeoTriangle2 triangle, GeoLine2 line, Tolerance tolerance)
        {
            if (tolerance == null)
            {
                throw new ArgumentNullException(nameof(tolerance));
            }

            for (int i = 0; i < 3; i++)
            {
                GeoLine2 edge = triangle.GetEdgeAt(i);

                if (edge.Length > tolerance.EqualPoint && Parallel2.IsParallel(edge, line, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Along the edges

        /// <summary>
        /// The boundary a triangle is walked along: its corners in order, A to B to C and back to A, as they stand.
        /// </summary>
        /// <remarks>
        /// Taken without filtering, so the walk reads no tolerance: of a triangle with three corners apart it is the
        /// polygon of them, the one every other question asks; of one of no width it runs along the segment and back;
        /// and of one point it stays there, where a polygon or a chain of distinct corners could not be built at all.
        /// </remarks>
        private static GeoPolygon2 Walk(GeoTriangle2 triangle) => new GeoPolygon2(new[] { triangle.A, triangle.B, triangle.C }, 3);

        /// <summary>
        /// Gets the point at a normalized parameter along the edges of a triangle, from A, where 1 is all the way round; values outside [0, 1] wrap round.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoTriangle2 triangle, double parameter) => Parametrization2.GetPointAtParameter(Walk(triangle), parameter);

        /// <summary>
        /// Gets the normalized parameter along the edges of a triangle of the point on them closest to a point.
        /// </summary>
        public static double GetParameterAtPoint(GeoTriangle2 triangle, GeoPoint2 point) => Parametrization2.GetParameterAtPoint(Walk(triangle), point);

        /// <summary>
        /// Gets the point at a length walked along the edges of a triangle from A; lengths past the perimeter wrap round.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoTriangle2 triangle, double distance) => Parametrization2.GetPointAtDistance(Walk(triangle), distance);

        /// <summary>
        /// Gets the length walked along the edges of a triangle from A to the point on them closest to a point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoTriangle2 triangle, GeoPoint2 point) => Parametrization2.GetDistanceAtPoint(Walk(triangle), point);

        /// <summary>
        /// Gets the length walked along the edges of a triangle from A to a normalized parameter.
        /// </summary>
        public static double GetDistanceAtParameter(GeoTriangle2 triangle, double parameter) => Parametrization2.GetDistanceAtParameter(Walk(triangle), parameter);

        /// <summary>
        /// Gets the normalized parameter at a length walked along the edges of a triangle from A; of a triangle of one point it is 0.
        /// </summary>
        public static double GetParameterAtDistance(GeoTriangle2 triangle, double distance) => Parametrization2.GetParameterAtDistance(Walk(triangle), distance);

        #endregion

        #region Extending and trimming a segment to a triangle

        /// <summary>
        /// Extends a segment at one end to where it meets the edges of a triangle, using the default tolerance.
        /// </summary>
        public static bool TryExtendTo(GeoLine2 line, GeoTriangle2 boundary, LineEnd end, out GeoLine2 result) => TryExtendTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Extends a segment at one end to where it meets the edges of a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is asked as the polygon of its corners, as <see cref="Lengthen2"/> asks a polygon; one of no width as the segment it is.
        /// </remarks>
        public static bool TryExtendTo(GeoLine2 line, GeoTriangle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
            => TryAsPolygon(boundary, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? Lengthen2.TryExtendTo(line, polygon, end, out result, tolerance)
                : Lengthen2.TryExtendTo(line, hull, end, out result, tolerance);

        /// <summary>
        /// Trims a segment at one end to where it meets the edges of a triangle, using the default tolerance.
        /// </summary>
        public static bool TryTrimTo(GeoLine2 line, GeoTriangle2 boundary, LineEnd end, out GeoLine2 result) => TryTrimTo(line, boundary, end, out result, Tolerance.Global);

        /// <summary>
        /// Trims a segment at one end to where it meets the edges of a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is asked as the polygon of its corners, as <see cref="Lengthen2"/> asks a polygon; one of no width as the segment it is.
        /// </remarks>
        public static bool TryTrimTo(GeoLine2 line, GeoTriangle2 boundary, LineEnd end, out GeoLine2 result, Tolerance tolerance)
            => TryAsPolygon(boundary, tolerance, out GeoPolygon2 polygon, out GeoLine2 hull)
                ? Lengthen2.TryTrimTo(line, polygon, end, out result, tolerance)
                : Lengthen2.TryTrimTo(line, hull, end, out result, tolerance);

        #endregion
    }
}
