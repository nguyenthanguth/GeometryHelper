using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Measuring a ray in the plane against another shape.
    /// </summary>
    /// <remarks>
    /// A ray runs on for ever in one direction, so against a shape with an end to it, it is cut: into the segment from
    /// its origin out past the farthest the shape reaches along it, by a margin of a millimetre or a hundred point
    /// tolerances, whichever is more. Every point of the shape lies behind that end, so nothing past it is nearer the
    /// shape than the end itself, and the segment is measured as any segment is. Against the square 100 by 100 with a
    /// corner at the origin, the ray from (-50, 150) along X is measured as the segment from there to (101, 150), 50
    /// from the square. Against a point or another ray nothing is cut: every candidate is one the ray really holds, an
    /// origin brought onto the other or the place where the two cross. Every answer against a cut is the cut segment's,
    /// with the tolerance passed through.
    /// </remarks>
    public static partial class Distance2
    {
        /// <summary>
        /// Calculates the distance from a ray to a point, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoRay2 ray, GeoPoint2 point) => DistanceTo(ray, point, Tolerance.Global);

        /// <summary>
        /// Calculates the distance from a ray to a point, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance, taken so that this reads like every other pair; it changes nothing here.</param>
        /// <returns>The distance from the point to its projection on the ray, stopped at the origin.</returns>
        public static double DistanceTo(GeoRay2 ray, GeoPoint2 point, Tolerance tolerance)
        {
            return DistanceTo(point, Ray2.Project(ray, point));
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoRay2 ray, GeoLine2 line) => DistanceTo(ray, line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment, within a tolerance.
        /// </summary>
        /// <returns>Zero where the two cross or touch; otherwise the gap between them.</returns>
        public static double DistanceTo(GeoRay2 ray, GeoLine2 line, Tolerance tolerance)
        {
            return DistanceTo(Ray2.CutPast(ray, line, tolerance), line, tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between two rays, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoRay2 ray1, GeoRay2 ray2) => DistanceTo(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two rays, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Zero where the two truly cross at any angle, at or ahead of both origins; otherwise the distance of the nearer of
        /// the two origins to the other ray, kept even where it is within the point tolerance, as two segments keep theirs.
        /// The ray from (0.0009, 0) along X and the one leaving 0.0009 along 120 degrees from the origin, whose carriers
        /// cross 0.0009 behind each, are 0.00156 apart, origin to origin, at every tolerance. Two rays along the X axis from 0 and from 100, facing each other, are nought apart;
        /// two at half a degree that meet a thousand along are nought apart too, though
        /// <see cref="Intersection2.TryIntersectWith(GeoRay2, GeoRay2, out GeoPoint2, Tolerance)"/> calls them parallel. Two
        /// parallel but for rounding do not cross where a double cannot place the crossing, as two lines without ends do not:
        /// a ray rebuilt through two points a hundred apart of another moved 10 to its side is 10 from it.
        /// </returns>
        /// <remarks>
        /// The ray from the origin along X and the one from (50, 30) along Y are 30 apart, the second origin standing
        /// over the first ray; the one from (50, -30) along Y crosses the first at (50, 0) and is nought from it.
        /// </remarks>
        public static double DistanceTo(GeoRay2 ray1, GeoRay2 ray2, Tolerance tolerance)
        {
            if (Ray2.TryCross(ray1, ray2, tolerance, false, out _))
            {
                return 0.0;
            }

            // Two rays that do not cross are nearest at an origin of one of them: anywhere else both could still be moved
            // towards each other along their lengths. Within the point tolerance they collide and keep their gap, as two
            // segments do.
            return Math.Min(DistanceTo(ray1, ray2.Origin, tolerance), DistanceTo(ray2, ray1.Origin, tolerance));
        }

        /// <summary>
        /// Calculates the shortest distance between a circle and a ray, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoCircle2 circle, GeoRay2 ray) => DistanceTo(circle, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a circle and a ray, within a tolerance.
        /// </summary>
        /// <returns>Zero where the ray starts inside the circle or runs into it; otherwise the gap between them.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoRay2 ray, Tolerance tolerance)
        {
            return DistanceTo(circle, Ray2.CutPast(ray, circle, tolerance), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between a polygon and a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPolygon2 poly, GeoRay2 ray) => DistanceTo(poly, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a polygon and a ray, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where the ray starts in the polygon, on its rim included, or runs into it; otherwise the gap, measured
        /// exactly. The ray from (100.0005, 50) along X is 0.0005 from the square 100 by 100 with a corner at the origin.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPolygon2 poly, GeoRay2 ray, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // DistanceTo(poly, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = Ray2.CutPast(ray, poly, tolerance);

            if (Collision2.Meets(poly, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                double d = DistanceToSide(poly.GetEdgeAt(i), cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a curved loop and a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoRay2 ray) => DistanceTo(loop, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a curved loop and a ray, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where the ray starts in the loop, on its rim included, or runs into it; otherwise the gap, measured
        /// exactly, an arc of the loop included. The ray from (150.0005, 50) along X is 0.0005 from the square 100 by 100
        /// with a corner at the origin whose right side is bowed out to x = 150 by a bulge of 1.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static double DistanceTo(GeoPolygonArc2 loop, GeoRay2 ray, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return DistanceTo(loop, Ray2.CutPast(ray, loop, tolerance), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolyline2 polyline, GeoRay2 ray) => DistanceTo(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a polyline and a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolyline2 polyline, GeoRay2 ray, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            // DistanceTo(polyline, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = Ray2.CutPast(ray, polyline, tolerance);

            if (CrossesOrStartsOn(polyline, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                double d = DistanceToSide(polyline.GetEdgeAt(i), cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a curved chain and a ray, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoRay2 ray) => DistanceTo(chain, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a curved chain and a ray, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static double DistanceTo(GeoPolylineArc2 chain, GeoRay2 ray, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return DistanceTo(chain, Ray2.CutPast(ray, chain, tolerance), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between a rectangle and a ray, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoRay2 ray) => DistanceTo(rect, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a rectangle and a ray, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where the ray starts in the rectangle, on its side included, or runs into it; otherwise the gap,
        /// measured exactly. The ray from (100.00085, 100.00085) along (1, 2) is 0.0012 from the square from the origin to
        /// (100, 100).
        /// </returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoRay2 ray, Tolerance tolerance)
        {
            // DistanceTo(rect, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = Ray2.CutPast(ray, rect, tolerance);

            if (Collision2.Meets(rect, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            foreach (GeoLine2 edge in rect.GetEdges())
            {
                double d = DistanceToSide(edge, cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }
    }
}
