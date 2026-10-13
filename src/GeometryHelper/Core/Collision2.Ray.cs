using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether a ray in the plane touches another shape.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against a shape with an end to it, it is cut: into the segment from
    /// its origin out past the farthest the shape reaches along it, by a margin of a millimetre or a hundred point
    /// tolerances, whichever is more. Nothing past that end is nearer the shape than the end itself, so the segment
    /// touches what the ray touches, and is asked as any segment is. Against the square 100 by 100 with a corner at
    /// the origin, the ray from (-50, 50) along X is asked as the segment from there to (101, 50). Against another ray
    /// nothing is cut, as there is no end to cut past.
    /// </remarks>
    public static partial class Collision2
    {
        /// <summary>
        /// Checks whether a ray touches an arc, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay2 ray, GeoArc2 arc) => CollidesWith(ray, arc, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray touches an arc, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay2 ray, GeoArc2 arc, Tolerance tolerance)
        {
            return CollidesWith(Ray2.CutPast(ray, arc, tolerance), arc, tolerance);
        }

        /// <summary>
        /// Checks whether a ray starts inside a circle or runs into it, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoRay2 ray) => CollidesWith(circle, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray starts inside a circle or runs into it, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoRay2 ray, Tolerance tolerance)
        {
            return CollidesWith(circle, Ray2.CutPast(ray, circle, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray starts inside a polygon or runs into it, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoPolygon2 poly, GeoRay2 ray) => CollidesWith(poly, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray starts inside a polygon, runs into it, or passes within the point tolerance of it,
        /// within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoPolygon2 poly, GeoRay2 ray, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return CollidesWith(poly, Ray2.CutPast(ray, poly, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray starts inside a curved loop or runs into it, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoRay2 ray) => CollidesWith(loop, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray starts inside a curved loop or runs into it, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoRay2 ray, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CollidesWith(loop, Ray2.CutPast(ray, loop, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray touches a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoRay2 ray) => CollidesWith(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray touches a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoRay2 ray, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return CollidesWith(polyline, Ray2.CutPast(ray, polyline, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray touches a curved chain, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoRay2 ray) => CollidesWith(chain, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray touches a curved chain, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoRay2 ray, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CollidesWith(chain, Ray2.CutPast(ray, chain, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray starts inside a rectangle or runs into it, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoRay2 ray) => CollidesWith(rect, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray starts inside a rectangle, runs into it, or passes within the point tolerance of it,
        /// within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoRay2 ray, Tolerance tolerance)
        {
            return CollidesWith(rect, Ray2.CutPast(ray, rect, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether a ray touches a line segment, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay2 ray, GeoLine2 line) => CollidesWith(ray, line, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray touches a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying along the ray touches it along a length and crosses it nowhere, so this is not the crossing
        /// test with the place thrown away: it is the two coming within the point tolerance of each other.
        /// </remarks>
        public static bool CollidesWith(GeoRay2 ray, GeoLine2 line, Tolerance tolerance)
        {
            return CollidesWith(Ray2.CutPast(ray, line, tolerance), line, tolerance);
        }

        /// <summary>
        /// Checks whether a line segment touches a ray, using the default tolerance: the same answer as
        /// <see cref="CollidesWith(GeoRay2, GeoLine2)"/>.
        /// </summary>
        public static bool CollidesWith(GeoLine2 line, GeoRay2 ray) => CollidesWith(line, ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment touches a ray, within a tolerance: the same answer as
        /// <see cref="CollidesWith(GeoRay2, GeoLine2, Tolerance)"/>.
        /// </summary>
        public static bool CollidesWith(GeoLine2 line, GeoRay2 ray, Tolerance tolerance)
        {
            return CollidesWith(line, Ray2.CutPast(ray, line, tolerance), tolerance);
        }

        /// <summary>
        /// Checks whether two rays touch, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay2 ray1, GeoRay2 ray2) => CollidesWith(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Checks whether two rays touch, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the distance between the two is at most the point tolerance; otherwise, false.</returns>
        /// <remarks>
        /// The ray from the origin along X and the one from (50, -50) along Y cross at (50, 0); two rays along the X axis
        /// from 0 and from 100, facing each other, touch along the stretch between them; turned back to back, they touch
        /// nowhere. A crossing of the carriers behind an origin does not count: the ray from (0.0009, 0) along X and the one
        /// leaving 0.0009 along 120 degrees from the origin, their origins 0.00156 apart, do not touch at a tolerance of a
        /// thousandth.
        /// </remarks>
        public static bool CollidesWith(GeoRay2 ray1, GeoRay2 ray2, Tolerance tolerance)
        {
            return Distance2.DistanceTo(ray1, ray2, tolerance) <= tolerance.EqualPoint;
        }
    }
}
