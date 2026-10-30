using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric intersections and finding exact intersection points.
    /// </summary>
    public static partial class Intersection2
    {
        #region Line - Line

        /// <summary>
        /// Tries to calculate the intersection point between two line segments using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <returns>true if the line segments intersect; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, out GeoPoint2 intersection)
        {
            return TryIntersectWith(line1, line2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the line segments intersect within tolerance; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, out GeoPoint2 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, LineExtension.None, out intersection, tolerance);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments, reading either or both as
        /// the infinite line carrying it, using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="extension">Which of the two may be reached past its endpoints.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <returns>true if the lines meet within the reach allowed; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, LineExtension extension, out GeoPoint2 intersection)
        {
            return TryIntersectWith(line1, line2, extension, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments, reading either or both as
        /// the infinite line carrying it, within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="extension">Which of the two may be reached past its endpoints.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the lines meet within the reach allowed; otherwise, false.</returns>
        /// <remarks>
        /// This is AutoCAD's <c>IntersectWith</c> with its <c>Intersect</c> option: <see cref="LineExtension.Both"/>
        /// finds where two axes would cross however short they are drawn, <see cref="LineExtension.First"/>
        /// where the first would reach the second if it were extended. Parallel lines are refused whatever the
        /// extension, as they are without one: they either never meet or share a whole stretch, and neither is
        /// a single point. The point returned lies on every segment that was not extended.
        /// <para>
        /// A segment that is not extended is reached up to the point tolerance past its ends, and the point is clamped
        /// onto it, so that splitting, trimming and extending find the crossing on both. This places points; it is not a
        /// test of contact. The segment ending at (10, 0) and the one leaving (10.00135, 0.00078) at 60 degrees meet here
        /// at (10, 0) at a tolerance of a thousandth, though they are 0.00156 apart and do not collide.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the extension is not a defined value.</exception>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, LineExtension extension, out GeoPoint2 intersection, Tolerance tolerance)
        {
            bool extendFirst;
            bool extendSecond;

            switch (extension)
            {
                case LineExtension.None: extendFirst = false; extendSecond = false; break;
                case LineExtension.First: extendFirst = true; extendSecond = false; break;
                case LineExtension.Second: extendFirst = false; extendSecond = true; break;
                case LineExtension.Both: extendFirst = true; extendSecond = true; break;
                default: throw new ArgumentOutOfRangeException(nameof(extension), "Unknown line extension.");
            }

            intersection = new GeoPoint2(0, 0);
            GeoVector2 r = line1.Direction;
            GeoVector2 s = line2.Direction;

            double rLength = r.Length;
            double sLength = s.Length;

            // A degenerate segment has no direction, so no intersection parameter can be derived.
            if (rLength <= tolerance.EqualPoint || sLength <= tolerance.EqualPoint)
            {
                return false;
            }

            double rCrossS = r.CrossProduct(s);

            if (IsParallelOver(Math.Abs(rCrossS) / (rLength * sLength), extendFirst, extendSecond, rLength, sLength, tolerance))
            {
                return false; // parallel or collinear
            }

            GeoVector2 qMinusP = line1.StartPoint.GetVectorTo(line2.StartPoint);
            double t = qMinusP.CrossProduct(s) / rCrossS;
            double u = qMinusP.CrossProduct(r) / rCrossS;

            // t and u are dimensionless parameters along each segment, so the slack allowed past an
            // endpoint has to be converted from a distance into parameter space. Using EqualPoint
            // directly would let a 100000 unit long segment reach 10 units beyond its own endpoint.
            double tTolerance = tolerance.EqualPoint / rLength;
            double uTolerance = tolerance.EqualPoint / sLength;

            if (!extendFirst && (t < -tTolerance || t > 1.0 + tTolerance))
            {
                return false;
            }

            if (!extendSecond && (u < -uTolerance || u > 1.0 + uTolerance))
            {
                return false;
            }

            // The point is placed on a segment that was not extended, clamped into it, so that a crossing
            // accepted within the endpoint slack still lies on that segment exactly.
            if (!extendFirst)
            {
                intersection = line1.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, t)));
            }
            else if (!extendSecond)
            {
                intersection = line2.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, u)));
            }
            else
            {
                intersection = line1.GetPointAtParameter(t);
            }

            return true;
        }

        /// <summary>
        /// Determines whether two members are parallel as far as finding the one point where they cross goes.
        /// </summary>
        /// <param name="sine">The sine of the angle between the two.</param>
        /// <param name="extendFirst">Whether the first is read as its infinite line.</param>
        /// <param name="extendSecond">Whether the second is read as its infinite line.</param>
        /// <param name="length1">The length of the first as drawn.</param>
        /// <param name="length2">The length of the second as drawn.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when there is no single crossing to give.</returns>
        /// <remarks>
        /// Two members are parallel when they draw apart by less than the point tolerance along the longer of the
        /// members drawn: to within the tolerance they are then one line or two lines side by side, and neither
        /// gives one point. The angle alone decided before, against a whole degree, however long the members —
        /// two ten-metre members crossing at 0.9 degrees, 157 apart at their ends, crossed nowhere while
        /// CollidesWith said true and DistanceTo nought. Only two infinite lines have no length to measure over,
        /// and those still go by the angle.
        /// </remarks>
        internal static bool IsParallelOver(double sine, bool extendFirst, bool extendSecond, double length1, double length2, Tolerance tolerance)
        {
            double reach = extendFirst
                ? (extendSecond ? 0.0 : length2)
                : (extendSecond ? length1 : Math.Max(length1, length2));

            return reach > 0.0
                ? sine * reach <= tolerance.EqualPoint
                : sine <= tolerance.EqualAngleSin;
        }

        /// <summary>
        /// Gets the intersection point between two line segments using default tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2)
        {
            return GetIntersection(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Gets the intersection point between two line segments within tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, out GeoPoint2 pt, tolerance) ? pt : (GeoPoint2?)null;
        }

        /// <summary>
        /// Gets the intersection point between two line segments, reading either or both as the infinite line
        /// carrying it, using default tolerance. Returns null if they do not meet within the reach allowed.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, LineExtension extension)
        {
            return GetIntersection(line1, line2, extension, Tolerance.Global);
        }

        /// <summary>
        /// Gets the intersection point between two line segments, reading either or both as the infinite line
        /// carrying it, within tolerance. Returns null if they do not meet within the reach allowed.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, LineExtension extension, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, extension, out GeoPoint2 pt, tolerance) ? pt : (GeoPoint2?)null;
        }

        #endregion

        #region Circle - Line

        /// <summary>
        /// Tries to calculate the intersection points between a circle and a line segment using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoLine2 line, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(circle, line, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection points between a circle and a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// A line passing outside the circle within the point tolerance of its rim touches it once, at the foot of the
        /// perpendicular from the centre. A line passing inside it crosses it twice, each crossing kept where it lies on
        /// the segment; two crossings no further apart than twice the point tolerance are one, at the foot. The segment
        /// 50 long from (0, 49.9995) along (1, 0.001), against the circle of radius 50 about the origin at a tolerance of
        /// 0.001, leaves the circle once, at (0.1791, 49.99968). A segment whose line runs inside the rim within the
        /// tolerance of it, but which crosses the rim nowhere along its length, touches it once where it comes within
        /// the tolerance: at the foot when the foot is on it. At a tolerance of 0.1 the segment from (-0.2, 49.97) to
        /// (0.2, 49.97) touches the circle of radius 50 at (0, 49.97).
        /// <para>
        /// Any other segment touches the circle exactly where it comes within the point tolerance of its rim, as the
        /// shortest line to the rim measures it, however far its line runs on before it would cross: once, at its point
        /// nearest the centre where it lies outside, else at its end nearer the rim. At a tolerance of 0.001, the segment
        /// from (0.05, 50.0005) to (10, 50.0005), whose line passes nearest the circle of radius 50 0.05 behind its start,
        /// touches it at its start, 0.000525 off; the one from (0, 49.95) to (2.225, 49.95) at its end, 0.00047 inside;
        /// and the one from (0.0009, 0.0019) to (1, 0.0019), whose line passes 0.0009 above the circle of radius 0.001
        /// about the origin just behind its start, does not touch it, the start being 0.0011 off. A crossing up to the
        /// tolerance past an end, taken at that end, within twice the tolerance of the other crossing is the same touch,
        /// and counts once: the segment from (-1, 0) to (0.0004, 0) meets the circle of radius 0.0012 about the origin
        /// at (-0.0012, 0) only. A segment no longer than the tolerance is read as its start.
        /// </para>
        /// </remarks>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(circle, line, out intersections, tolerance, true);
        }

        /// <summary>
        /// Tries to calculate the intersection points between a circle and a line segment within tolerance, with or
        /// without the touches of a segment that comes within the tolerance of the rim away from where its line crosses
        /// it.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The points, empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="touches">
        /// true for <see cref="TryIntersectWith(GeoCircle2, GeoLine2, out GeoPoint2[], Tolerance)"/>; false for the
        /// crossings an offset cuts its pieces at, read as they were before: a segment that crosses nowhere touches only
        /// where its line runs within the tolerance inside the rim, a foot up to the tolerance past an end is taken at
        /// that end however far it is from the rim, and a crossing taken at an end is kept beside the other.
        /// </param>
        internal static bool TryIntersectWith(GeoCircle2 circle, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance, bool touches)
        {
            GeoVector2 d = line.Direction;
            double dLength = d.Length;

            if (dLength <= tolerance.EqualPoint)
            {
                // Degenerate segment: it can only meet the circle if its single point lies on the circumference.
                if (Math.Abs(Distance2.DistanceTo(circle.Center, line.StartPoint) - circle.Radius) <= tolerance.EqualPoint)
                {
                    intersections = new[] { line.StartPoint };
                    return true;
                }
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            // Solving b * b - 4 * a * c would compare a value in units of length to the fourth against a
            // length threshold, which makes the tangency band meaningless at large scales and far too wide
            // at small ones. Working from the perpendicular distance between the centre and the infinite
            // line keeps every comparison in the same units as EqualPoint.
            double centerParameter = line.StartPoint.GetVectorTo(circle.Center).DotProduct(d) / (dLength * dLength);
            double centerDistance = Distance2.DistanceTo(circle.Center, line.GetPointAtParameter(centerParameter));

            if (centerDistance > circle.Radius + tolerance.EqualPoint)
            {
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            List<GeoPoint2> points = new List<GeoPoint2>(2);

            // Outside the rim, within the tolerance of it, the line touches at the foot of the perpendicular. Inside, it
            // crosses twice, and the two are one only when they lie within twice the tolerance of each other: a line a
            // hair inside the rim still crosses it where it does, which may be well away from the foot.
            double halfChord = centerDistance >= circle.Radius
                ? 0.0
                : Math.Sqrt(circle.Radius * circle.Radius - centerDistance * centerDistance) / dLength;

            bool merged = halfChord * dLength <= tolerance.EqualPoint;

            if (merged)
            {
                AddPointOnSegment(points, line, centerParameter, dLength, tolerance);

                // Outside the rim, a foot up to the tolerance past an end leaves that end further from the rim than the
                // line is: it touches only where the end itself comes within the tolerance.
                if (touches && points.Count > 0 && centerDistance >= circle.Radius && (centerParameter < 0.0 || centerParameter > 1.0)
                    && Distance2.DistanceTo(circle.Center, points[0]) - circle.Radius > tolerance.EqualPoint)
                {
                    points.Clear();
                }
            }
            else
            {
                AddPointOnSegment(points, line, centerParameter - halfChord, dLength, tolerance);
                AddPointOnSegment(points, line, centerParameter + halfChord, dLength, tolerance);

                // A crossing up to the tolerance past an end, taken at that end, within twice the tolerance of the other
                // crossing is the same touch: the segment runs into the rim and stops within the tolerance of it.
                if (touches && points.Count == 2 && Distance2.DistanceTo(points[0], points[1]) <= 2.0 * tolerance.EqualPoint)
                {
                    bool firstPast = centerParameter - halfChord < 0.0 || centerParameter - halfChord > 1.0;
                    bool secondPast = centerParameter + halfChord < 0.0 || centerParameter + halfChord > 1.0;

                    if (firstPast && !secondPast)
                    {
                        points.RemoveAt(0);
                    }
                    else if (secondPast && !firstPast)
                    {
                        points.RemoveAt(1);
                    }
                    else if (firstPast)
                    {
                        // Both past an end: the segment lies within the tolerance of the rim along its whole length, and
                        // touches at its end nearer it.
                        double firstGap = Math.Abs(Distance2.DistanceTo(circle.Center, points[0]) - circle.Radius);
                        double secondGap = Math.Abs(Distance2.DistanceTo(circle.Center, points[1]) - circle.Radius);
                        points.RemoveAt(firstGap <= secondGap ? 1 : 0);
                    }
                }
            }

            // Inside the rim within the tolerance of it, a segment that crosses it nowhere along its length may still lie
            // within the tolerance of it: it touches once, at the foot when the foot is on it.
            bool insideTheRim = centerDistance < circle.Radius && centerDistance >= circle.Radius - tolerance.EqualPoint;

            if (points.Count == 0 && !merged && insideTheRim)
            {
                AddPointOnSegment(points, line, centerParameter, dLength, tolerance);
            }

            // Any other segment that crosses the rim nowhere, nor within the tolerance past an end, touches it once where
            // it comes within the tolerance of it, as its distance to the rim says: at its point nearest the centre where
            // it lies outside, else at its end nearer the rim. Without the touches only one whose line runs inside the
            // rim within the tolerance is read so, which places the same point.
            if (points.Count == 0 && (touches || insideTheRim))
            {
                AddNearestToTheRim(points, circle, line, centerParameter, tolerance);
            }

            intersections = points.ToArray();
            return intersections.Length > 0;
        }

        /// <summary>
        /// Adds the point of a segment that meets a circle's rim nowhere where it comes nearest the rim, if that is within
        /// the point tolerance of it: the point nearest the centre where the segment lies outside the circle, else the
        /// end further from the centre, the start where the two are as far.
        /// </summary>
        private static void AddNearestToTheRim(List<GeoPoint2> points, GeoCircle2 circle, GeoLine2 line, double centerParameter, Tolerance tolerance)
        {
            GeoPoint2 nearest = centerParameter <= 0.0 ? line.StartPoint
                : centerParameter >= 1.0 ? line.EndPoint
                : line.GetPointAtParameter(centerParameter);
            double nearestDistance = Distance2.DistanceTo(circle.Center, nearest);

            if (nearestDistance >= circle.Radius)
            {
                if (nearestDistance - circle.Radius <= tolerance.EqualPoint)
                {
                    points.Add(nearest);
                }

                return;
            }

            double startDistance = Distance2.DistanceTo(circle.Center, line.StartPoint);
            double endDistance = Distance2.DistanceTo(circle.Center, line.EndPoint);

            if (circle.Radius - Math.Max(startDistance, endDistance) <= tolerance.EqualPoint)
            {
                points.Add(startDistance >= endDistance ? line.StartPoint : line.EndPoint);
            }
        }

        /// <summary>
        /// Gets all intersection points between a circle and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoLine2 line)
        {
            return GetIntersections(circle, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a circle and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            TryIntersectWith(circle, line, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        #endregion

        #region Circle - Circle

        /// <summary>
        /// Tries to calculate the intersection points between two circles using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 c1, GeoCircle2 c2, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(c1, c2, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection points between two circles within tolerance.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The crossings are worked out from the smaller circle, so they are the same two points whichever circle is
        /// asked first, and stay accurate when the other is huge. The circle of radius 2.5E8 that carries an edge 100
        /// long of bulge 1E-7, from (0, 0) to (100, 0), meets the circle of radius 40 about (50, 30) at (23.54249, 0)
        /// and (76.45751, 0) either way round; worked from the larger circle, they came out 0.076 off.
        /// </para>
        /// <para>
        /// Two circles that stay within the point tolerance of each other all the way round, their centres and their radii
        /// no more than the tolerance apart put together, are one circle and cross nowhere; so are two whose centres are
        /// closer than the tolerance where the rounding of the numbers leaves no line to cross on (see
        /// <see cref="AboutOneCentre(double, double, double, double, Tolerance)"/>). Otherwise the circles cross where they do,
        /// however close their centres: the circle of radius 1000 about the origin and the circle of radius 1000.0005 about
        /// (0.0006, 0) cross at (-833.33324, 552.77094) and (-833.33324, -552.77094).
        /// </para>
        /// </remarks>
        public static bool TryIntersectWith(GeoCircle2 c1, GeoCircle2 c2, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            double d = Distance2.DistanceTo(c1.Center, c2.Center);

            // Separate circles, one inside the other, one circle, or two about one centre.
            if (d > c1.Radius + c2.Radius + tolerance.EqualPoint ||
                d < Math.Abs(c1.Radius - c2.Radius) - tolerance.EqualPoint ||
                (d <= tolerance.EqualPoint && AboutOneCentre(d, Scale(c1, c2), c1.Radius, c2.Radius, tolerance)))
            {
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            // One circle a hair inside the other touches it without crossing. The radical line is no guide there: its
            // distance from the centre below grows as fast as the radii's difference shrinks, so a circle of radius 99.94
            // 0.0593 inside one of 100 would touch at (101.18, 0), 1.18 off both. The touch is the point between the two
            // rims where they come nearest, on the line through the centres.
            if (d < Math.Abs(c1.Radius - c2.Radius))
            {
                double side = c1.Radius >= c2.Radius ? 1.0 : -1.0;
                double ux = side * (c2.Center.X - c1.Center.X) / d;
                double uy = side * (c2.Center.Y - c1.Center.Y) / d;
                double reach = 0.5 * (c1.Radius + c2.Radius);

                intersections = new[]
                {
                    new GeoPoint2(
                        0.5 * (c1.Center.X + c2.Center.X) + reach * ux,
                        0.5 * (c1.Center.Y + c2.Center.Y) + reach * uy)
                };
                return true;
            }

            // Worked from the smaller circle. From the larger, the square of its radius swamps the other's: an edge 100
            // long of bulge 1E-7 lies on a circle of radius 2.5E8, whose square is held to about 8, and the half chord
            // to a circle of radius 40, the root of the difference of two such squares, came out 0.076 off, so the edge
            // missed an arc it crosses. Of two circles of one radius, the one whose centre comes first by X, then by Y,
            // is worked from, so the answer is the same to the last bit whichever is asked first.
            double firstRadius = c1.Radius, secondRadius = c2.Radius;
            GeoPoint2 first = c1.Center, second = c2.Center;
            bool fromSecond = secondRadius < firstRadius
                || (!(firstRadius < secondRadius)
                    && (second.X < first.X || (!(first.X < second.X) && second.Y < first.Y)));

            double nearX = fromSecond ? second.X : first.X;
            double nearY = fromSecond ? second.Y : first.Y;
            double nearRadius = fromSecond ? secondRadius : firstRadius;
            double farRadius = fromSecond ? firstRadius : secondRadius;
            double towardsX = fromSecond ? first.X - second.X : second.X - first.X;
            double towardsY = fromSecond ? first.Y - second.Y : second.Y - first.Y;

            // The difference of the squared radii is taken as the product of their difference and their sum, so that two
            // radii of 1000 and 1000.00002 leave it all its digits rather than the last few of a million.
            double a = ((nearRadius - farRadius) * (nearRadius + farRadius) + d * d) / (2.0 * d);
            double hSq = nearRadius * nearRadius - a * a;
            double h = hSq > 0 ? Math.Sqrt(hSq) : 0.0;

            GeoPoint2 p2 = new GeoPoint2(
                nearX + a * towardsX / d,
                nearY + a * towardsY / d);

            if (h <= tolerance.EqualPoint)
            {
                intersections = new[] { p2 };
                return true;
            }

            double rx = -(c2.Center.Y - c1.Center.Y) * (h / d);
            double ry = (c2.Center.X - c1.Center.X) * (h / d);

            intersections = new[]
            {
                new GeoPoint2(p2.X + rx, p2.Y + ry),
                new GeoPoint2(p2.X - rx, p2.Y - ry)
            };
            return true;
        }

        /// <summary>
        /// Gets all intersection points between two circles using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 c1, GeoCircle2 c2)
        {
            return GetIntersections(c1, c2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between two circles within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 c1, GeoCircle2 c2, Tolerance tolerance)
        {
            TryIntersectWith(c1, c2, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        /// <summary>
        /// The relative rounding of a double: the gap between one and the next number above it.
        /// </summary>
        private const double Epsilon = 2.2204460492503131E-16;

        /// <summary>
        /// Whether two circles whose centres stand no further apart than the point tolerance are one circle, or about one
        /// centre: whether they stay within the point tolerance of each other all the way round, or the rounding of the
        /// numbers they are made of leaves the line they cross on nowhere in particular.
        /// </summary>
        /// <param name="apart">How far apart the centres are, no more than the point tolerance.</param>
        /// <param name="scale">The largest coordinate of the centres, or radius, whichever is larger.</param>
        /// <param name="firstRadius">The radius of the first circle.</param>
        /// <param name="secondRadius">The radius of the second circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// <para>
        /// Two circles are never further apart than the distance between their centres and the difference of their radii put
        /// together; where that is no more than the point tolerance they are one circle, which meets along its length rather
        /// than at points. Two arcs of the circle of radius 750 about (1234.5678, 2345.6789) whose ends are rounded to six places
        /// have centres 1.6E-6 apart, and they cross nowhere, where they were cut at (1650.09, 2970.05). The circles of radius
        /// 1000 and 1000.00002 with their centres a ten-thousandth apart are within 1.2E-4 of each other all round and are one
        /// at a tolerance of a thousandth.
        /// </para>
        /// <para>
        /// Further apart than that, two circles cross on their radical line, the difference of their squared radii over twice
        /// the distance between their centres from the centre. A rounding of the numbers, the largest of them times the
        /// rounding of a double, moves that line by about as much times the sum of the radii over that distance; where that is
        /// more than the point tolerance the line cannot be placed, and the centres are one. The circles of radius 1000 and
        /// 1000.0005 with their centres 6E-4 apart, as much as 1.1E-3 apart in places, place it to 7.4E-7 and cross. It is the
        /// rule of the line without ends (see <see cref="XLine2"/>), whose crossing is no crossing where a double cannot place it.
        /// </para>
        /// </remarks>
        internal static bool AboutOneCentre(double apart, double scale, double firstRadius, double secondRadius, Tolerance tolerance)
        {
            if (apart + Math.Abs(firstRadius - secondRadius) <= tolerance.EqualPoint)
            {
                return true;
            }

            // Written as what has to hold for the line to be placed, so that a NaN reads as one centre, as it did.
            return !(apart * tolerance.EqualPoint > Epsilon * scale * (firstRadius + secondRadius));
        }

        /// <summary>
        /// The largest coordinate of the centres of two circles, or radius, whichever is larger: the size of the numbers whose
        /// rounding <see cref="AboutOneCentre(double, double, double, double, Tolerance)"/> weighs.
        /// </summary>
        private static double Scale(GeoCircle2 c1, GeoCircle2 c2)
            => Math.Max(
                Math.Max(Math.Max(Math.Abs(c1.Center.X), Math.Abs(c1.Center.Y)), Math.Max(Math.Abs(c2.Center.X), Math.Abs(c2.Center.Y))),
                Math.Max(c1.Radius, c2.Radius));

        #endregion

        #region Rectangle - Shapes

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoLine2 line)
        {
            return GetIntersections(rect, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in rect.GetEdges())
            {
                if (TryIntersectWith(edge, line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two rotated rectangles using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect1, GeoRectangle2 rect2)
        {
            return GetIntersections(rect1, rect2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two rotated rectangles within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect1, GeoRectangle2 rect2, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] edges1 = rect1.GetEdges();
            GeoLine2[] edges2 = rect2.GetEdges();

            foreach (var e1 in edges1)
            {
                foreach (var e2 in edges2)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoCircle2 circle)
        {
            return GetIntersections(rect, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoCircle2 circle, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in rect.GetEdges())
            {
                if (TryIntersectWith(circle, edge, out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        #endregion

        #region Polygon - Shapes

        /// <summary>
        /// Gets all intersection points between a polygon's boundary and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoLine2 line)
        {
            return GetIntersections(poly, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon's boundary and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in poly.GetEdges())
            {
                if (TryIntersectWith(edge, line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two polygons using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly1, GeoPolygon2 poly2)
        {
            return GetIntersections(poly1, poly2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two polygons within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var e1 in poly1.GetEdges())
            {
                foreach (var e2 in poly2.GetEdges())
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a rectangle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRectangle2 rect)
        {
            return GetIntersections(poly, rect, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a rectangle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] rectEdges = rect.GetEdges();

            foreach (var e1 in poly.GetEdges())
            {
                foreach (var e2 in rectEdges)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoCircle2 circle)
        {
            return GetIntersections(poly, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoCircle2 circle, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in poly.GetEdges())
            {
                if (TryIntersectWith(circle, edge, out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        #endregion

        #region Polyline - Shapes

        /// <summary>
        /// Tries to calculate all intersection points between a polyline and a line segment using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoLine2 line, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(polyline, line, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate all intersection points between a polyline and a line segment within tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (TryIntersectWith(polyline.GetEdgeAt(i), line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }

            intersections = points.ToArray();
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoLine2 line)
        {
            return GetIntersections(polyline, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            TryIntersectWith(polyline, line, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        /// <summary>
        /// Gets all intersection points between two polylines using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            return GetIntersections(pl1, pl2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between two polylines within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 pl1, GeoPolyline2 pl2, Tolerance tolerance)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 e1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    GeoLine2 e2 = pl2.GetEdgeAt(j);
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a rectangle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRectangle2 rect)
        {
            return GetIntersections(polyline, rect, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a rectangle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] rectEdges = rect.GetEdges();

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 e1 = polyline.GetEdgeAt(i);
                foreach (var e2 in rectEdges)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoCircle2 circle)
        {
            return GetIntersections(polyline, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoCircle2 circle, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (TryIntersectWith(circle, polyline.GetEdgeAt(i), out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a polygon using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoPolygon2 poly)
        {
            return GetIntersections(polyline, poly, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a polygon within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 e1 = polyline.GetEdgeAt(i);
                foreach (var e2 in poly.GetEdges())
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        #endregion

        #region Simplicity

        /// <summary>
        /// Checks whether a polygon is simple, using default tolerance: no edge crosses or touches another
        /// except where neighbours share their vertex.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <returns>true if the polygon is simple; otherwise, false.</returns>
        public static bool IsSimple(GeoPolygon2 polygon)
        {
            return IsSimple(polygon, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon is simple, within tolerance: no edge crosses or touches another except
        /// where neighbours share their vertex.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance: edges closer than the point tolerance count as touching.</param>
        /// <returns>true if the polygon is simple; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="GeoPolygon2"/> does not check this when it is built, because the check costs more than
        /// building it. A polygon that is not simple is still read consistently, under the even-odd rule, but
        /// its area no longer means what it says. A vertex touching another edge counts as not simple, and so
        /// does an edge folding back over its neighbour. Two edges that are not neighbours touch where they come
        /// within the point tolerance of each other, measured with no slack past their ends: the loop with corners
        /// (10, 0) and (10.0009, 0.0009), 0.00127 apart, is simple at a tolerance of a thousandth and not at two.
        /// Edges are swept in order along X, so the cost grows with the number of edges whose extents overlap
        /// rather than with the square of the edge count.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool IsSimple(GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            int count = polygon.EdgeCount;
            GeoLine2[] edges = new GeoLine2[count];
            double[] minX = new double[count];
            double[] maxX = new double[count];
            double[] minY = new double[count];
            double[] maxY = new double[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                edges[i] = polygon.GetEdgeAt(i);
                minX[i] = Math.Min(edges[i].StartPoint.X, edges[i].EndPoint.X);
                maxX[i] = Math.Max(edges[i].StartPoint.X, edges[i].EndPoint.X);
                minY[i] = Math.Min(edges[i].StartPoint.Y, edges[i].EndPoint.Y);
                maxY[i] = Math.Max(edges[i].StartPoint.Y, edges[i].EndPoint.Y);
                order[i] = i;
            }

            double[] keys = (double[])minX.Clone();
            Array.Sort(keys, order);

            List<int> active = new List<int>();
            double slack = tolerance.EqualPoint;

            foreach (int i in order)
            {
                active.RemoveAll(j => maxX[j] < minX[i] - slack);

                foreach (int j in active)
                {
                    if (maxY[j] < minY[i] - slack || minY[j] > maxY[i] + slack)
                    {
                        continue;
                    }

                    bool iThenJ = j == (i + 1) % count;
                    bool jThenI = i == (j + 1) % count;

                    if (iThenJ || jThenI)
                    {
                        // Neighbours share a vertex and must meet nowhere else: the far end of either lying on
                        // the other means the boundary folds back over itself.
                        GeoLine2 incoming = iThenJ ? edges[i] : edges[j];
                        GeoLine2 outgoing = iThenJ ? edges[j] : edges[i];

                        if (Containment2.IsPointOn(outgoing, incoming.StartPoint, tolerance) ||
                            Containment2.IsPointOn(incoming, outgoing.EndPoint, tolerance))
                        {
                            return false;
                        }
                    }
                    else if (Collision2.CollidesWith(edges[i], edges[j], tolerance))
                    {
                        return false;
                    }
                }

                active.Add(i);
            }

            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Adds the point at the given parameter when it falls on the segment, allowing EqualPoint of
        /// slack past either endpoint. The slack is converted from a distance into parameter space so
        /// that it stays the same real distance regardless of how long the segment is.
        /// </summary>
        private static void AddPointOnSegment(List<GeoPoint2> points, GeoLine2 line, double parameter, double lineLength, Tolerance tolerance)
        {
            double parameterTolerance = tolerance.EqualPoint / lineLength;
            if (parameter < -parameterTolerance || parameter > 1.0 + parameterTolerance)
            {
                return;
            }
            points.Add(line.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, parameter))));
        }

        /// <summary>
        /// Adds a point to the list only if it is not already present within the specified tolerance.
        /// </summary>
        /// <param name="list">The target list of points.</param>
        /// <param name="pt">The point to be added.</param>
        /// <param name="tolerance">The tolerance used to evaluate if points are equal.</param>
        private static void AddUniquePoint(List<GeoPoint2> list, GeoPoint2 pt, Tolerance tolerance)
        {
            foreach (var p in list)
            {
                if (p.IsEqualTo(pt, tolerance))
                {
                    return;
                }
            }
            list.Add(pt);
        }

        #endregion
        #region Chains that may curve

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoLine2 line) => GetIntersections(chain, line, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), line, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(chain, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoArc2 arc) => GetIntersections(chain, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), arc, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(chain, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoCircle2 circle) => GetIntersections(chain, circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), circle, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(chain, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolyline2 polyline) => GetIntersections(chain, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(chain, polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygon2 polygon) => GetIntersections(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(chain, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolylineArc2 other) => GetIntersections(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolylineArc2 other, out GeoPoint2[] intersections) => TryIntersectWith(chain, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolylineArc2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygonArc2 loop) => GetIntersections(chain, loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(chain, loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, loop, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoLine2 line) => GetIntersections(loop, line, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), line, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(loop, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoArc2 arc) => GetIntersections(loop, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), arc, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(loop, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoCircle2 circle) => GetIntersections(loop, circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), circle, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(loop, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolyline2 polyline) => GetIntersections(loop, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(loop, polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygon2 polygon) => GetIntersections(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(loop, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolylineArc2 chain) => GetIntersections(loop, chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(loop, chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygonArc2 other) => GetIntersections(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygonArc2 other, out GeoPoint2[] intersections) => TryIntersectWith(loop, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygonArc2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, other, tolerance);

            return intersections.Length > 0;
        }

        #endregion

    }
}
