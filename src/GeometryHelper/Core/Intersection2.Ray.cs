using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a ray in the plane crosses another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ray runs on for ever in one direction, so against a shape with an end to it, it is cut: into the segment from
    /// its origin out past the farthest the shape reaches along it, by a margin of a millimetre or a hundred point
    /// tolerances, whichever is more. Every crossing lies before that end, and the end itself is never one, so the
    /// segment crosses where the ray does and is asked as any segment is, its crossings ordered and de-duplicated as a
    /// segment's are. Against the square 100 by 100 with a corner at the origin, the ray from (-50, 50) along X is asked
    /// as the segment from there to (101, 50), and crosses at (0, 50) and (100, 50).
    /// </para>
    /// <para>
    /// The parallel test of a segment measures the drift over the longer of the two, and for the cut that is as far as
    /// the other shape reaches: the ray is drawn as far as the shape makes it. Against another ray nothing is cut, and
    /// parallel is read by angle alone.
    /// </para>
    /// </remarks>
    public static partial class Intersection2
    {
        /// <summary>
        /// Gets the points where a ray meets an arc, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRay2 ray, GeoArc2 arc) => GetIntersections(ray, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRay2 ray, GeoArc2 arc, Tolerance tolerance)
        {
            return GetIntersections(Ray2.CutPast(ray, arc, tolerance), arc, tolerance);
        }

        /// <summary>
        /// Gets the points where a ray crosses the circumference of a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoRay2 ray) => GetIntersections(circle, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray crosses the circumference of a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The ray from (-100, 0) along X crosses the circle of radius 50 round the origin at (-50, 0) and (50, 0); the ray
        /// from the centre crosses it once, at (50, 0).
        /// </remarks>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoRay2 ray, Tolerance tolerance)
        {
            return GetIntersections(circle, Ray2.CutPast(ray, circle, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray crosses the boundary of a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRay2 ray) => GetIntersections(poly, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray crosses the boundary of a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRay2 ray, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetIntersections(poly, Ray2.CutPast(ray, poly, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray meets a curved loop, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoRay2 ray) => GetIntersections(loop, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray meets a curved loop, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoRay2 ray, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetIntersections(loop, Ray2.CutPast(ray, loop, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray crosses a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRay2 ray) => GetIntersections(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray crosses a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRay2 ray, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetIntersections(polyline, Ray2.CutPast(ray, polyline, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray meets a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoRay2 ray) => GetIntersections(chain, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray meets a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoRay2 ray, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetIntersections(chain, Ray2.CutPast(ray, chain, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the points where a ray crosses the boundary of a rectangle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoRay2 ray) => GetIntersections(rect, ray, Tolerance.Global);

        /// <summary>
        /// Gets the points where a ray crosses the boundary of a rectangle, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoRay2 ray, Tolerance tolerance)
        {
            return GetIntersections(rect, Ray2.CutPast(ray, rect, tolerance), tolerance);
        }

        /// <summary>
        /// Tries to find where a ray crosses a line segment, using the default tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <returns>true if the two cross; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoRay2 ray, GeoLine2 line, out GeoPoint2 intersection)
            => TryIntersectWith(ray, line, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a line segment, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The line segment.</param>
        /// <param name="intersection">The crossing point if successful, placed on the ray, never behind its origin.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two cross; otherwise, false, as for a segment parallel to the ray.</returns>
        public static bool TryIntersectWith(GeoRay2 ray, GeoLine2 line, out GeoPoint2 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(Ray2.CutPast(ray, line, tolerance), line, out intersection, tolerance);
        }

        /// <summary>
        /// Tries to find where a line segment crosses a ray, using the default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <returns>true if the two cross; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line, GeoRay2 ray, out GeoPoint2 intersection)
            => TryIntersectWith(line, ray, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where a line segment crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersection">The crossing point if successful, placed on the segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two cross; otherwise, false, as for a segment parallel to the ray.</returns>
        public static bool TryIntersectWith(GeoLine2 line, GeoRay2 ray, out GeoPoint2 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(line, Ray2.CutPast(ray, line, tolerance), out intersection, tolerance);
        }

        /// <summary>
        /// Tries to find where two rays cross, using the default tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <returns>true if the two cross; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoRay2 ray1, GeoRay2 ray2, out GeoPoint2 intersection)
            => TryIntersectWith(ray1, ray2, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find where two rays cross, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="intersection">The crossing point if successful, placed on the first ray, never behind its origin.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true if the carrier lines cross at a point each ray reaches, within the point tolerance of its origin or ahead of
        /// it; false for two rays parallel by angle, which neither has a length to measure a drift over.
        /// </returns>
        /// <remarks>
        /// The ray from the origin along X and the one from (50, -50) along Y cross at (50, 0); the one from (50, 50) along
        /// Y runs away from the first and does not.
        /// </remarks>
        public static bool TryIntersectWith(GeoRay2 ray1, GeoRay2 ray2, out GeoPoint2 intersection, Tolerance tolerance)
        {
            intersection = new GeoPoint2(0, 0);

            // Both directions are of unit length, so their cross product is the sine of the angle between them. Neither ray has
            // a length to measure a drift over, so parallel is read by the angle alone; a NaN is parallel to everything.
            double sine = ray1.Direction.CrossProduct(ray2.Direction);

            if (!(Math.Abs(sine) > tolerance.EqualAngleSin))
            {
                return false;
            }

            return Ray2.TryCross(ray1, ray2, tolerance, out intersection);
        }

        /// <summary>
        /// Tries to find where a ray crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(circle, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(circle, Ray2.CutPast(ray, circle, tolerance), out intersections, tolerance);
        }

        /// <summary>
        /// Tries to find where a ray crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(loop, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return TryIntersectWith(loop, Ray2.CutPast(ray, loop, tolerance), out intersections, tolerance);
        }

        /// <summary>
        /// Tries to find where a ray crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(polyline, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return TryIntersectWith(polyline, Ray2.CutPast(ray, polyline, tolerance), out intersections, tolerance);
        }

        /// <summary>
        /// Tries to find where a ray crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoRay2 ray, out GeoPoint2[] intersections)
            => TryIntersectWith(chain, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a ray crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoRay2 ray, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return TryIntersectWith(chain, Ray2.CutPast(ray, chain, tolerance), out intersections, tolerance);
        }
    }
}
