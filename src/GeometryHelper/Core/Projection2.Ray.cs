using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a ray in the plane to another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ray runs on for ever in one direction, so against a shape with an end to it, it is cut: into the segment from
    /// its origin out past the farthest the shape reaches along it, by a margin of a millimetre or a hundred point
    /// tolerances, whichever is more. Every point of the shape lies behind that end, so a shortest segment the cut gets
    /// rightly is the ray's own, and the cut is asked as any segment is. Against the square 100 by 100 with a corner at
    /// the origin, the ray from (-50, 150) along X is asked as the segment from there to (101, 150), and gets a segment
    /// 50 long down onto the top edge.
    /// </para>
    /// <para>
    /// Both ends sit on a boundary, as for every pair here, and the segment leaves the first shape named. Against a
    /// point or another ray nothing is cut: every candidate is one the ray really holds, an origin brought onto the other
    /// or the place where the two cross.
    /// </para>
    /// </remarks>
    public static partial class Projection2
    {
        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a point, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPoint2 point) => GetShortestLineTo(ray, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a point, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance, taken so that this reads like every other pair; it changes nothing here.</param>
        /// <returns>
        /// The segment from the projection of the point on the ray, stopped at the origin, to the point: from the ray
        /// leaving the origin along X, the point (-30, 40) gets the one from the origin, 50 long.
        /// </returns>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPoint2 point, Tolerance tolerance)
        {
            return new GeoLine2(Ray2.Project(ray, point), point);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a line segment, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoLine2 line) => GetShortestLineTo(ray, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a line segment, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving the ray and landing on <paramref name="line"/>, of no length at all where the two cross.</returns>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoLine2 line, Tolerance tolerance)
        {
            return GetShortestLineTo(Ray2.CutPast(ray, line, tolerance), line, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving one ray and landing on another, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray1, GeoRay2 ray2) => GetShortestLineTo(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving one ray and landing on another, within a tolerance.
        /// </summary>
        /// <param name="ray1">The ray the segment leaves.</param>
        /// <param name="ray2">The ray the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The segment of no length at the crossing where the two truly cross, at or ahead of both origins; otherwise the
        /// shorter of two candidates, the origin of the second brought onto the first and the origin of the first brought
        /// onto the second, the first of them on a tie.
        /// </returns>
        /// <remarks>
        /// The ray from the origin along X gets the segment from (50, 0) to (50, 30) to the ray from (50, 30) along Y.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray1, GeoRay2 ray2, Tolerance tolerance)
        {
            if (Ray2.TryCross(ray1, ray2, tolerance, false, out GeoPoint2 crossing))
            {
                return new GeoLine2(crossing, crossing);
            }

            // The two candidates Distance2.DistanceTo(GeoRay2, GeoRay2) weighs, the first kept on a tie.
            GeoLine2 first = new GeoLine2(Ray2.Project(ray1, ray2.Origin), ray2.Origin);
            GeoLine2 second = new GeoLine2(ray1.Origin, Ray2.Project(ray2, ray1.Origin));

            return second.Length < first.Length ? second : first;
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on an arc, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoArc2 arc) => GetShortestLineTo(ray, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on an arc, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoArc2 arc, Tolerance tolerance)
        {
            return GetShortestLineTo(Ray2.CutPast(ray, arc, tolerance), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the circumference of a circle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoCircle2 circle) => GetShortestLineTo(ray, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the circumference of a circle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoCircle2 circle, Tolerance tolerance)
        {
            return GetShortestLineTo(Ray2.CutPast(ray, circle, tolerance), circle, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolygon2 poly) => GetShortestLineTo(ray, poly, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetShortestLineTo(Ray2.CutPast(ray, poly, tolerance), poly, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolygonArc2 loop) => GetShortestLineTo(ray, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(Ray2.CutPast(ray, loop, tolerance), loop, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolyline2 polyline) => GetShortestLineTo(ray, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetShortestLineTo(Ray2.CutPast(ray, polyline, tolerance), polyline, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolylineArc2 chain) => GetShortestLineTo(ray, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(Ray2.CutPast(ray, chain, tolerance), chain, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoRectangle2 rect) => GetShortestLineTo(ray, rect, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a ray and landing on the boundary of a rectangle, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRay2 ray, GeoRectangle2 rect, Tolerance tolerance)
        {
            return GetShortestLineTo(Ray2.CutPast(ray, rect, tolerance), rect, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving the circumference of a circle and landing on a ray, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoRay2 ray) => GetShortestLineTo(circle, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the circumference of a circle and landing on a ray, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoRay2 ray, Tolerance tolerance)
        {
            return GetShortestLineTo(circle, Ray2.CutPast(ray, circle, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a polygon and landing on a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoRay2 ray) => GetShortestLineTo(poly, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a polygon and landing on a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoRay2 ray, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetShortestLineTo(poly, Ray2.CutPast(ray, poly, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a curved loop and landing on a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoRay2 ray) => GetShortestLineTo(loop, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a curved loop and landing on a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoRay2 ray, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, Ray2.CutPast(ray, loop, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a polyline and landing on a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoRay2 ray) => GetShortestLineTo(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a polyline and landing on a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoRay2 ray, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetShortestLineTo(polyline, Ray2.CutPast(ray, polyline, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving a curved chain and landing on a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoRay2 ray) => GetShortestLineTo(chain, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving a curved chain and landing on a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoRay2 ray, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, Ray2.CutPast(ray, chain, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a rectangle and landing on a ray, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoRay2 ray) => GetShortestLineTo(rect, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving the boundary of a rectangle and landing on a ray, within a tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoRay2 ray, Tolerance tolerance)
        {
            return GetShortestLineTo(rect, Ray2.CutPast(ray, rect, tolerance), tolerance);
        }
    }
}
