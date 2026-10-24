using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods for finding where 3D shapes meet.
    /// <para>
    /// Each <c>TryIntersectWith</c> overload returns a single result and reports false when there is not
    /// exactly one. Two shapes that overlap along a whole line or a whole area — a segment lying in a
    /// plane, two coincident planes, two collinear segments — have no single crossing to name, so they
    /// come back false rather than picking an arbitrary point out of the overlap. Ask <c>Distance3</c>
    /// or <c>Collision3</c> when the question is whether they touch at all.
    /// </para>
    /// </summary>
    public static partial class Intersection3
    {
        #region Line - Line

        /// <summary>
        /// Tries to find the point where two line segments meet, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line1, line2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where two line segments meet, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segments meet at a single point; otherwise, false.</returns>
        /// <remarks>
        /// Two segments in space usually pass each other without meeting, so this asks for the closest
        /// approach and accepts it only when the gap is within tolerance. Parallel segments are refused
        /// even when they overlap, matching the 2D behaviour: a shared stretch is not a point.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (line1.IsDegenerate(tolerance) || line2.IsDegenerate(tolerance))
            {
                return false;
            }

            if (IsParallelOver(line1, line2, LineExtension.None, tolerance))
            {
                return false;
            }

            GeoLine3 bridge = Projection3.GetShortestLineTo(line1, line2, tolerance);

            if (bridge.Length > tolerance.EqualPoint)
            {
                return false;
            }

            intersection = bridge.MidPoint;
            return true;
        }

        /// <summary>
        /// Gets the point where two line segments meet, or null when they do not, using the default tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2)
        {
            return GetIntersection(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Gets the point where two line segments meet, or null when they do not, within a tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, out GeoPoint3 point, tolerance) ? point : (GeoPoint3?)null;
        }

        /// <summary>
        /// Tries to find the point where two line segments meet, reading either or both as the infinite line
        /// carrying it, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, LineExtension extension, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line1, line2, extension, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where two line segments meet, reading either or both as the infinite line
        /// carrying it, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="extension">Which of the two may be reached past its endpoints.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the lines meet at a single point within the reach allowed; otherwise, false.</returns>
        /// <remarks>
        /// As without an extension, the lines must actually meet: the gap at their closest approach must be
        /// within tolerance. Two axes that pass each other in space do not meet however far they are extended;
        /// <see cref="Projection3.GetShortestLineTo(GeoLine3, GeoLine3, LineExtension, Tolerance)"/> gives the
        /// segment bridging them. Parallel lines are refused.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the extension is not a defined value.</exception>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, LineExtension extension, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (extension != LineExtension.None && extension != LineExtension.First &&
                extension != LineExtension.Second && extension != LineExtension.Both)
            {
                throw new ArgumentOutOfRangeException(nameof(extension), "Unknown line extension.");
            }

            if (line1.IsDegenerate(tolerance) || line2.IsDegenerate(tolerance))
            {
                return false;
            }

            if (IsParallelOver(line1, line2, extension, tolerance))
            {
                return false;
            }

            GeoLine3 bridge = Projection3.GetShortestLineTo(line1, line2, extension, tolerance);

            if (bridge.Length > tolerance.EqualPoint)
            {
                return false;
            }

            intersection = bridge.MidPoint;
            return true;
        }

        /// <summary>
        /// Gets the point where two line segments meet, reading either or both as the infinite line carrying
        /// it, or null when they do not, using the default tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2, LineExtension extension)
        {
            return GetIntersection(line1, line2, extension, Tolerance.Global);
        }

        /// <summary>
        /// Gets the point where two line segments meet, reading either or both as the infinite line carrying
        /// it, or null when they do not, within a tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2, LineExtension extension, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, extension, out GeoPoint3 point, tolerance) ? point : (GeoPoint3?)null;
        }

        #endregion

        /// <summary>
        /// Determines whether two segments are parallel as far as finding the one point where they cross goes:
        /// drawing apart by less than the point tolerance along the longer of the members drawn.
        /// </summary>
        private static bool IsParallelOver(GeoLine3 line1, GeoLine3 line2, LineExtension extension, Tolerance tolerance)
        {
            GeoVector3 d1 = line1.Direction;
            GeoVector3 d2 = line2.Direction;
            double length1 = d1.Length;
            double length2 = d2.Length;
            double sine = d1.CrossProduct(d2).Length / (length1 * length2);

            return Intersection2.IsParallelOver(
                sine,
                extension == LineExtension.First || extension == LineExtension.Both,
                extension == LineExtension.Second || extension == LineExtension.Both,
                length1,
                length2,
                tolerance);
        }

        #region Line - Plane

        /// <summary>
        /// Tries to find the point where a line segment crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoPlane3 plane, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, plane, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment running parallel to the plane is refused whether it lies in the plane or beside it:
        /// in the first case every one of its points is a crossing, in the second none is, and neither
        /// gives the single point this method promises.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            GeoVector3 direction = line.Direction;
            double length = direction.Length;

            if (length <= tolerance.EqualPoint)
            {
                return false;
            }

            double denominator = direction.DotProduct(plane.Normal);

            // The normal is a unit vector, so this is how far the segment climbs or falls from one end to the
            // other. Less than the planar tolerance and it is parallel to the plane as drawn: it lies in it or
            // beside it, and neither gives one point. The angle alone decided before, against a whole degree,
            // so a ten-metre member crossing the plane at half a degree, its ends forty-four off it, crossed
            // nowhere.
            if (Math.Abs(denominator) <= tolerance.EqualPlanar)
            {
                return false;
            }

            double parameter = line.StartPoint.GetVectorTo(plane.Origin).DotProduct(plane.Normal) / denominator;

            // The parameter is dimensionless, so the slack allowed past an endpoint has to be converted
            // from a distance into parameter space; using EqualPoint directly would let a very long
            // segment reach far beyond its own endpoint.
            double slack = tolerance.EqualPoint / length;

            if (parameter < -slack || parameter > 1.0 + slack)
            {
                return false;
            }

            intersection = Parametrization3.GetPointAtParameter(line, Math.Max(0.0, Math.Min(1.0, parameter)));
            return true;
        }

        /// <summary>
        /// Gets where a segment meets a plane, as <see cref="GeoLine3.GetIntersections(GeoPlane3, Tolerance)"/> gives it, and
        /// whether the point given is that of a touch rather than a crossing.
        /// </summary>
        internal static GeoPoint3[] Meeting(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance, out bool touch)
        {
            touch = false;

            if (TryIntersectWith(line, plane, out GeoPoint3 crossing, tolerance))
            {
                return new[] { crossing };
            }

            GeoPoint3[] touching = Touching(line, plane, tolerance);
            touch = touching.Length > 0;
            return touching;
        }

        /// <summary>
        /// Gets where a segment the crossing test refuses touches a plane, as
        /// <see cref="Collision3.CollidesWith(GeoLine3, GeoPlane3, Tolerance)"/> reads a touch: the point of it nearest the plane,
        /// where its ends stand either side of it or one of them within the planar tolerance of it; none where it touches
        /// nothing, or lies in the plane, longer than the point tolerance and both ends within the planar tolerance of it.
        /// </summary>
        internal static GeoPoint3[] Touching(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance)
        {
            double atStart = plane.SignedDistanceTo(line.StartPoint);
            double atEnd = plane.SignedDistanceTo(line.EndPoint);
            bool startOn = Math.Abs(atStart) <= tolerance.EqualPlanar;
            bool endOn = Math.Abs(atEnd) <= tolerance.EqualPlanar;

            if (startOn && endOn && line.Length > tolerance.EqualPoint)
            {
                return new GeoPoint3[0];
            }

            if ((atStart < 0.0 && atEnd > 0.0) || (atStart > 0.0 && atEnd < 0.0))
            {
                return new[] { line.StartPoint.Add(line.StartPoint.GetVectorTo(line.EndPoint).Multiply(atStart / (atStart - atEnd))) };
            }

            if (startOn || endOn)
            {
                return new[] { Math.Abs(atStart) <= Math.Abs(atEnd) ? line.StartPoint : line.EndPoint };
            }

            return new GeoPoint3[0];
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, plane, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray starting within the planar tolerance of the plane lies in it only where it runs level with it to
        /// rounding, and then meets it along its whole length and names no place. One dipping towards the plane,
        /// however shallowly, crosses it where it reaches height nought, and one rising away from it touches it at
        /// its origin, the one point of it within the tolerance it never comes back to: the ray from (5, 5, 0.0004)
        /// along (1, 0, -0.01) crosses the floor at (5.04, 5, 0), and along (1, 0, 0.01) touches it at
        /// (5, 5, 0.0004). A ray starting off the plane reaches it at one point at any angle but none at all.
        /// </remarks>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
            => TryCrossPlane(ray, plane, out intersection, tolerance, false);

        /// <summary>
        /// The sine below which a ray starting within the planar tolerance of a plane reads as level with it: rounding,
        /// and no more.
        /// </summary>
        private const double LevelToRounding = 1E-12;

        /// <summary>
        /// Tries to find the point where a ray crosses a plane, either as <see cref="TryIntersectWith(GeoRay3, GeoPlane3,
        /// out GeoPoint3, Tolerance)"/> reads it or, for the rays that count crossings to tell inside from outside, as
        /// they have always read it.
        /// </summary>
        /// <remarks>
        /// The rays cast behind <see cref="Containment3.Locate(GeoSolid3, GeoPoint3, Tolerance)"/>, the prepared
        /// solid's Locate and the booleans' depth under a triangle count the faces they cross, and refuse a ray starting
        /// within the planar tolerance and running within the angle tolerance of the plane, as lying in it. They keep
        /// that reading until it is judged on its own, since every containment, boolean, clash and take-off answer
        /// rests on it. The ray members of the index and the prepared solid, which the clash's bars cut their centre
        /// lines by, name crossings rather than count them, and read a ray as the public members do.
        /// </remarks>
        internal static bool TryCrossPlane(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance, bool countingCrossings)
            => countingCrossings
                ? TryCastThroughPlane(ray, plane, out intersection, tolerance)
                : TryReachPlane(ray, plane, out intersection, tolerance);

        /// <summary>
        /// The public reading of <see cref="TryCrossPlane(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance, bool)"/>: a ray
        /// starting within the planar tolerance lies in the plane only where it is level with it to rounding.
        /// </summary>
        private static bool TryReachPlane(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            double denominator = ray.Direction.DotProduct(plane.Normal);
            double rise = ray.Origin.GetVectorTo(plane.Origin).DotProduct(plane.Normal);

            if (Math.Abs(rise) <= tolerance.EqualPlanar)
            {
                // Both vectors are unit length here, so the dot product is the sine of the angle between the ray and
                // the plane. Level to rounding, the ray lies in the plane and gives no single point. Otherwise it
                // reaches height nought ahead of its origin, dipping, or behind it, rising, and then touches the plane
                // at its origin, which lies within the tolerance of it.
                if (!(Math.Abs(denominator) > LevelToRounding))
                {
                    intersection = GeoPoint3.Origin;
                    return false;
                }

                intersection = Parametrization3.GetPointAtDistance(ray, Math.Max(0.0, rise / denominator));
                return true;
            }

            return TryCastThroughPlane(ray, plane, out intersection, tolerance);
        }

        /// <summary>
        /// The reading of <see cref="TryCrossPlane(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance, bool)"/> the ray casts
        /// that count crossings keep, and the public one off the planar tolerance.
        /// </summary>
        private static bool TryCastThroughPlane(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            double denominator = ray.Direction.DotProduct(plane.Normal);
            double rise = ray.Origin.GetVectorTo(plane.Origin).DotProduct(plane.Normal);

            // Both vectors are unit length here, so the dot product is the sine of the angle between the ray
            // and the plane. A ray starting in the plane and running along it within the angle tolerance meets
            // it over a stretch rather than at a point. One starting off the plane reaches it at one point at
            // any angle but none at all, however far along: refusing every crossing under a degree lost the
            // far side of a long member to the ray cast behind Locate, which then called a point inside the
            // member outside.
            if (Math.Abs(rise) <= tolerance.EqualPlanar ? Math.Abs(denominator) <= tolerance.EqualAngleSin : denominator == 0.0)
            {
                return false;
            }

            double distance = rise / denominator;

            if (double.IsInfinity(distance))
            {
                return false;
            }

            if (distance < -tolerance.EqualPoint)
            {
                return false;
            }

            intersection = Parametrization3.GetPointAtDistance(ray, Math.Max(0.0, distance));
            return true;
        }

        #endregion

        #region Line - Triangle

        /// <summary>
        /// Tries to find the point where a line segment crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoTriangle3 triangle, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, triangle, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossing is found against the triangle's carrier plane and then tested for containment, so
        /// the edges of the triangle are decided by the same tolerance-aware test that
        /// <c>Containment3.Contains</c> uses. A segment lying in the plane of the triangle is refused for
        /// the same reason it is refused against a bare plane. Whether the triangle has an area is read by
        /// <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/>: the triangle 0.003 by 0.003 at the origin has, and the segment
        /// from (0.001, 0.001, -1) to (0.001, 0.001, 1) crosses it at (0.001, 0.001, 0). One with none is the segment it has
        /// collapsed to within the point tolerance, and is crossed where its sides are, the longest first: the segment from
        /// (50, 0, -1) to (50, 0, 1) crosses the sliver 100 long and 1E-8 high at (50, 0, 0). Both crossed nothing while twice
        /// the area was read against the vector tolerance.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (triangle.IsDegenerate(tolerance))
            {
                foreach (GeoLine3 side in SidesOfThin(triangle))
                {
                    if (TryIntersectWith(line, side, out intersection, tolerance))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (!TryIntersectWith(line, triangle.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(triangle, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoTriangle3 triangle, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, triangle, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a triangle, within a tolerance: the first point
        /// <see cref="GetIntersections(GeoRay3, GeoTriangle3, Tolerance)"/> gives, and false where it gives none.
        /// </summary>
        /// <remarks>
        /// The answer is the list's, so the two never disagree: the ray is read as the line without ends along it, and is given
        /// what that line meets at its origin or ahead of it, the crossing or the point of a touch. The ray from
        /// (10, 20, 0.0002) falling 4E-6 for each unit along X climbs 0.00032 across the triangle with corners (0, 0, 0),
        /// (100, 0, 0) and (0, 100, 0) and lies level over it within the planar tolerance of a thousandth, so it is refused,
        /// where it was given its crossing of the floor at (60, 20, 0) while the list gave none; the ray from (20, 20, 10)
        /// pointing straight down crosses it at (20, 20, 0).
        /// </remarks>
        public static bool TryIntersectWith(GeoRay3 ray, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance)
            => First(Meeting(ray, triangle, tolerance), out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses a triangle, with the plane read as
        /// <see cref="TryCrossPlane(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance, bool)"/> reads it.
        /// </summary>
        internal static bool TryCrossTriangle(GeoRay3 ray, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance, bool countingCrossings)
        {
            intersection = GeoPoint3.Origin;

            // A triangle with no area is crossed where its sides are, as a segment crosses it.
            if (triangle.IsDegenerate(tolerance))
            {
                foreach (GeoLine3 side in SidesOfThin(triangle))
                {
                    GeoPoint3[] onSide = GetIntersections(side, ray, tolerance);

                    if (onSide.Length > 0)
                    {
                        intersection = onSide[0];
                        return true;
                    }
                }

                return false;
            }

            if (!TryCrossPlane(ray, triangle.GetPlane(), out GeoPoint3 candidate, tolerance, countingCrossings))
            {
                return false;
            }

            if (!Containment3.Contains(triangle, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Gets the sides of a triangle with no area, the longest first and then the others in their order round it: the
        /// longest is the segment the triangle has collapsed to, within the point tolerance of all of it.
        /// </summary>
        internal static GeoLine3[] SidesOfThin(GeoTriangle3 triangle)
        {
            GeoLine3[] sides = { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) };
            int longest = 0;

            for (int i = 1; i < 3; i++)
            {
                if (sides[i].Length > sides[longest].Length)
                {
                    longest = i;
                }
            }

            return longest == 0 ? sides
                : longest == 1 ? new[] { sides[1], sides[0], sides[2] }
                : new[] { sides[2], sides[0], sides[1] };
        }

        #endregion

        #region Plane - Plane

        /// <summary>
        /// Tries to find the line where two planes meet, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPlane3 plane1, GeoPlane3 plane2, out GeoRay3 intersection)
        {
            return TryIntersectWith(plane1, plane2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the line where two planes meet, within a tolerance.
        /// </summary>
        /// <param name="plane1">The first plane.</param>
        /// <param name="plane2">The second plane.</param>
        /// <param name="intersection">
        /// A ray along the line of intersection when the method returns true. The line is infinite in both
        /// directions and the ray only names it: <c>GetPointAtDistance</c> takes negative distances, so
        /// the half behind the origin is reachable too.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the planes meet in a line; otherwise, false.</returns>
        /// <remarks>
        /// Parallel planes are refused whether they are apart, in which case they never meet, or
        /// coincident, in which case they meet everywhere. The origin of the returned ray is the point of
        /// the intersection line closest to the world origin, which makes the result depend only on the
        /// two planes and not on where their own origins happen to sit.
        /// </remarks>
        public static bool TryIntersectWith(GeoPlane3 plane1, GeoPlane3 plane2, out GeoRay3 intersection, Tolerance tolerance)
        {
            // Two planes have no size, so there is nothing to measure how far they draw apart over: they go by
            // the angle.
            return TryGetMeetingLine(plane1, plane2, 0.0, out intersection, tolerance);
        }

        /// <summary>
        /// Gets the line where two planes meet, for a shape of a known size lying in the first.
        /// </summary>
        /// <param name="plane1">The plane the shape lies in.</param>
        /// <param name="plane2">The other plane.</param>
        /// <param name="extent">
        /// How far the shape reaches across its plane, such as the diagonal of its box; nought to read the first
        /// plane as the whole plane and go by the angle alone.
        /// </param>
        /// <param name="intersection">The line, as a ray from a point on it along it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the two are parallel as far as the shape goes.</returns>
        /// <remarks>
        /// Planes within the angle tolerance of each other are parallel as planes go, but a shape of finite size
        /// in one of them still crosses the other wherever it stands off it by more than the planar tolerance —
        /// and it stands off by up to the sine of the angle times its size. So that decides, not the angle:
        /// under a degree, a face poking five hundredths through a cutting plane went uncut, and a box corner
        /// poking into another was counted twice by the union.
        /// </remarks>
        internal static bool TryGetMeetingLine(GeoPlane3 plane1, GeoPlane3 plane2, double extent, out GeoRay3 intersection, Tolerance tolerance)
        {
            intersection = default;

            GeoVector3 n1 = plane1.Normal;
            GeoVector3 n2 = plane2.Normal;
            GeoVector3 direction = n1.CrossProduct(n2);

            // Both normals are unit vectors, so the length of their cross product is the sine of the
            // angle between the planes.
            double sine = direction.Length;

            if (extent > 0.0 ? sine * extent <= tolerance.EqualPlanar : sine <= tolerance.EqualAngleSin)
            {
                return false;
            }

            double d1 = plane1.DistanceFromWorldOrigin;
            double d2 = plane2.DistanceFromWorldOrigin;
            double n1Dotn2 = n1.DotProduct(n2);
            double denominator = 1.0 - n1Dotn2 * n1Dotn2;

            double c1 = (d1 - d2 * n1Dotn2) / denominator;
            double c2 = (d2 - d1 * n1Dotn2) / denominator;

            GeoPoint3 origin = GeoPoint3.Origin.Add(n1.Multiply(c1)).Add(n2.Multiply(c2));

            // The angle has been judged above, by the shape's reach when it has one. The ray would judge the sine
            // again as a length, against the vector tolerance, and refuse planes half a degree apart that the test
            // above has just found crossing, so it is given the direction already of unit length.
            intersection = new GeoRay3(origin, direction.Divide(sine));
            return true;
        }

        #endregion

        #region Line - Polygon

        /// <summary>
        /// Tries to find the point where a line segment crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoPolygon3 polygon, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, polygon, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossing is found against the polygon's carrier plane and then tested for containment, so
        /// the boundary is decided by the same tolerance-aware test <c>Containment3.Contains</c> uses. A
        /// segment lying in the plane of the polygon is refused, as it is against a bare plane: every one
        /// of its points inside the outline would be a crossing.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TryIntersectWith(line, polygon.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(polygon, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPolygon3 polygon, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, polygon, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a polygon, within a tolerance: the first point
        /// <see cref="GetIntersections(GeoRay3, GeoPolygon3, Tolerance)"/> gives, and false where it gives none.
        /// </summary>
        /// <remarks>
        /// The answer is the list's, so the two never disagree: the ray is read as the line without ends along it, and is given
        /// what that line meets at its origin or ahead of it, the crossing or the point of a touch. The ray from
        /// (10, 50, 0.0002) falling 4E-6 for each unit along X climbs 0.0004 across the square 100 by 100 in the floor with a
        /// corner at the origin and lies level over it within the planar tolerance of a thousandth, so it is refused, where it
        /// was given its crossing of the floor at (60, 50, 0) while the list gave none; the ray from (20, 20, 10) pointing
        /// straight down crosses it at (20, 20, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
            => First(Meeting(ray, polygon, tolerance), out intersection);

        /// <summary>
        /// Tries to find the point where a ray crosses a polygon by its crossing of the polygon's plane, held by the polygon: the
        /// reading the library's own questions start from, before the ray is read as its line.
        /// </summary>
        internal static bool TryCrossPolygon(GeoRay3 ray, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TryIntersectWith(ray, polygon.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(polygon, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        #endregion

        #region Line - Box

        /// <summary>
        /// Finds where a line segment enters and leaves an oriented box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoObb3 box) => GetIntersections(line, box, Tolerance.Global);

        /// <summary>
        /// Finds where a line segment enters and leaves an oriented box, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The crossing points in order along the segment: none when it misses the box, one when it starts
        /// or ends inside, two when it passes through, and one, the point of it nearest the box, when it touches the box
        /// within the point tolerance on its own without crossing a face. None for a segment lying wholly inside the box, or
        /// lying along it within the tolerance, which collide with it and cross nothing.
        /// </returns>
        /// <remarks>
        /// This is the slab method. Taking the segment into the local frame of the box turns it into three
        /// pairs of parallel planes, and the part of the segment inside the box is the overlap of the three
        /// intervals those pairs cut out of it. The overlap is empty exactly when some axis separates them,
        /// which is the same reasoning the separating axis test in <c>Collision3</c> rests on.
        /// A segment that barely moves across a pair of faces is held within them when it runs no further
        /// outside than the point tolerance, and is then given its crossings only where it comes within the
        /// point tolerance of the box, as <see cref="Collision3.CollidesWith(GeoLine3, GeoObb3, Tolerance)"/>
        /// reads it: past an edge of the box 10 by 10 by 10 a segment along X standing 0.000714 off both faces
        /// is 0.00101 from the box, and crosses nothing, where it was given two points. The other way round, the slabs
        /// of the axes it moves along are read exactly, so a segment that comes within the tolerance of an edge or a face
        /// without reaching the box touches it and crosses no face: it is given the point of it nearest the box, as the
        /// other shapes give a touch. The segment from (5, 10.1, 9.9007) to (5, 9.9007, 10.1) passes 0.0005 off the edge
        /// of that box and is given (5, 10.00035, 10.00035), where it was given nothing and collided. The touch is given only
        /// where the segment touches the box at one place: one lying along the box within the tolerance, within the planar
        /// tolerance of the plane of the face it stands over or the line of the edge it runs with all along, such as one 0.0005
        /// over the top face and level with it, is given none, as the body of the box gives none and an arc in the same place none, a curve lying on a
        /// surface crossing it nowhere. The point is the same whichever way round the segment is given.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
            => SlabCrossings(line, box, tolerance, true);

        /// <summary>
        /// Gets where a segment crosses the faces of a box, and nothing where it only touches the box: the places the
        /// splitting of a segment or a chain by a box cuts at, since a touch cuts nothing.
        /// </summary>
        internal static GeoPoint3[] CrossingsOf(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
            => SlabCrossings(line, box, tolerance, false);

        /// <summary>
        /// Gets where a segment crosses the faces of a box, by the slabs, and where it touches the box without crossing a
        /// face when asked to.
        /// </summary>
        private static GeoPoint3[] SlabCrossings(GeoLine3 line, GeoObb3 box, Tolerance tolerance, bool touching)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3 start = box.CoordinateSystem.ToLocal(line.StartPoint);
            GeoVector3 direction = box.CoordinateSystem.ToLocal(line.Direction);

            double enter = 0.0;
            double exit = 1.0;
            bool heldOutside = false;

            double[] origins = { start.X, start.Y, start.Z };
            double[] steps = { direction.X, direction.Y, direction.Z };
            double[] extents = { box.ExtentX, box.ExtentY, box.ExtentZ };

            for (int axis = 0; axis < 3; axis++)
            {
                double step = steps[axis];
                double origin = origins[axis];
                double extent = extents[axis];

                if (Math.Abs(step) <= tolerance.EqualPoint)
                {
                    // The segment does not move along this axis, so the slab either holds all of it or
                    // none of it: held where both ends stand within the tolerance of the slab, so alike either way round, and
                    // otherwise asked whether it touches; the cuts of a chain read it from the start, as they always have.
                    double held = touching ? Math.Max(Math.Abs(origin), Math.Abs(origin + step)) : Math.Abs(origin);

                    if (held > extent + tolerance.EqualPoint)
                    {
                        return touching && MayTouch(origins, steps, extents, tolerance) ? Touch(line, box, tolerance) : new GeoPoint3[0];
                    }

                    // Held, either end may stand outside the slab, the one it starts at or the one it ends at.
                    heldOutside |= Math.Max(Math.Abs(origin), Math.Abs(origin + step)) > extent;
                    continue;
                }

                double t1 = (-extent - origin) / step;
                double t2 = (extent - origin) / step;

                if (t1 > t2)
                {
                    double swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                enter = Math.Max(enter, t1);
                exit = Math.Min(exit, t2);

                if (enter > exit)
                {
                    return touching && MayTouch(origins, steps, extents, tolerance) ? Touch(line, box, tolerance) : new GeoPoint3[0];
                }
            }

            // Held within slabs it runs outside of, by up to the tolerance on each, it may stand further than the tolerance
            // off an edge or a corner: rule R, the crossings only where it touches.
            if (heldOutside && !new Gap3.Block(box).Within(line.StartPoint, line.EndPoint, tolerance, Collision3.Reach(tolerance)))
            {
                return new GeoPoint3[0];
            }

            GeoPoint3 entryPoint = Parametrization3.GetPointAtParameter(line, enter);
            GeoPoint3 exitPoint = Parametrization3.GetPointAtParameter(line, exit);

            if (entryPoint.IsEqualTo(exitPoint, tolerance))
            {
                return new[] { entryPoint };
            }

            List<GeoPoint3> hits = new List<GeoPoint3>();

            // A crossing is only reported where the segment actually pierces a face. An endpoint sitting
            // inside the box is a place the segment stops, not a place it crosses the surface.
            if (enter > 0.0)
            {
                hits.Add(entryPoint);
            }

            if (exit < 1.0)
            {
                hits.Add(exitPoint);
            }

            // Held within two slabs it runs outside of, a crossing read off the third stands up to the tolerance off each of
            // their faces, so further than the tolerance off the edge or the corner between them: it is given the point of the
            // segment nearest the box instead, as a touch is.
            if (touching && heldOutside && hits.Count > 0)
            {
                hits = NearOnly(line, box, hits, tolerance);
            }

            // Held within a slab it runs outside of, and touching, it crosses no face it is not held by, and still meets the
            // box: it is given the point of it nearest the box, as one held in no slab is.
            if (touching && hits.Count == 0 && heldOutside)
            {
                return new Gap3.Block(box).TryTouchAlone(line.StartPoint, line.EndPoint, tolerance.EqualPlanar, out GeoPoint3 alone)
                    ? new[] { alone }
                    : new GeoPoint3[0];
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Determines whether the box of a segment, in the frame of a box, stands near enough the box for the two to touch,
        /// so that a segment plainly clear of it is not measured: the reach of <see cref="Collision3.Reach(Tolerance)"/>, and
        /// the rounding of numbers the size of the two besides.
        /// </summary>
        private static bool MayTouch(double[] origins, double[] steps, double[] extents, Tolerance tolerance)
        {
            double squared = 0.0;
            double scale = 0.0;

            for (int axis = 0; axis < 3; axis++)
            {
                double from = origins[axis];
                double to = from + steps[axis];
                double extent = extents[axis];
                double low = from < to ? from : to;
                double high = from < to ? to : from;
                double off = low > extent ? low - extent : high < -extent ? -extent - high : 0.0;

                squared += off * off;
                scale += Math.Abs(from) + Math.Abs(steps[axis]) + extent;
            }

            double reach = Collision3.Reach(tolerance) + 1E-12 * scale;

            // Written as what a touch needs, so that a segment of NaNs goes on to be measured, as it did.
            return !(squared > reach * reach);
        }

        /// <summary>
        /// Gets the point where a segment that crosses no face of a box touches it, as <see cref="Collision3.CollidesWith(GeoLine3, GeoObb3, Tolerance)"/>
        /// reads a touch: the point of the segment nearest the box, where they come within the point tolerance of each other
        /// at one place, and none otherwise, or where the segment lies along the box.
        /// </summary>
        private static GeoPoint3[] Touch(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
        {
            var block = new Gap3.Block(box);

            return block.Within(line.StartPoint, line.EndPoint, tolerance, Collision3.Reach(tolerance))
                && block.TryTouchAlone(line.StartPoint, line.EndPoint, tolerance.EqualPlanar, out GeoPoint3 alone)
                ? new[] { alone }
                : new GeoPoint3[0];
        }

        /// <summary>
        /// Keeps the crossings that stand within the point tolerance of the box, and gives the point of the segment nearest the
        /// box for any that stands further, once and in order along the segment.
        /// </summary>
        private static List<GeoPoint3> NearOnly(GeoLine3 line, GeoObb3 box, List<GeoPoint3> hits, Tolerance tolerance)
        {
            var block = new Gap3.Block(box);
            var kept = new List<GeoPoint3>(hits.Count);
            bool far = false;

            foreach (GeoPoint3 hit in hits)
            {
                if (block.Distance(hit) > tolerance.EqualPoint)
                {
                    far = true;
                }
                else
                {
                    kept.Add(hit);
                }
            }

            if (!far)
            {
                return hits;
            }

            if (block.TryTouchAlone(line.StartPoint, line.EndPoint, tolerance.EqualPlanar, out GeoPoint3 nearest)
                && !kept.Exists(hit => hit.IsEqualTo(nearest, tolerance)))
            {
                // In order along the segment: put before the first crossing that lies further along.
                double along = Parametrization3.GetDistanceAtPoint(line, nearest);
                int at = kept.FindIndex(hit => Parametrization3.GetDistanceAtPoint(line, hit) > along);

                kept.Insert(at < 0 ? kept.Count : at, nearest);
            }

            return kept;
        }

        #endregion

        #region Plane - Solid

        /// <summary>
        /// Finds where a plane cuts the edges of a solid, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoSolid3 solid) => GetIntersections(plane, solid, Tolerance.Global);

        /// <summary>
        /// Finds where a plane cuts the edges of a solid, within a tolerance.
        /// </summary>
        /// <param name="plane">The cutting plane.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossing points, with duplicates from shared edges removed.</returns>
        /// <remarks>
        /// These are the corners of the section the plane cuts, but not the section itself: they come back
        /// as a bag of points with no ordering, because ordering them into a loop needs the face adjacency
        /// that this method does not look at. <c>Splition3</c> is where that belongs.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, plane, tolerance);

            List<GeoPoint3> hits = new List<GeoPoint3>();

            foreach (GeoFace3 face in solid.Faces)
            {
                for (int i = 0; i < face.Boundary.EdgeCount; i++)
                {
                    if (!TryIntersectWith(face.Boundary.GetEdgeAt(i), plane, out GeoPoint3 hit, tolerance))
                    {
                        continue;
                    }

                    bool alreadyFound = false;

                    foreach (GeoPoint3 existing in hits)
                    {
                        if (existing.IsEqualTo(hit, tolerance))
                        {
                            alreadyFound = true;
                            break;
                        }
                    }

                    if (!alreadyFound)
                    {
                        hits.Add(hit);
                    }
                }
            }

            return hits.ToArray();
        }

        #endregion

        #region Face, solid and axis-aligned box

        /// <summary>
        /// Tries to find the point where a line segment crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoFace3 face, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, face, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The holes in the face are respected: a segment threading through a hole passes through nothing
        /// and is reported as missing the face.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!TryIntersectWith(line, face.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!face.Contains(candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoFace3 face, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, face, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses the material of a face, within a tolerance: the first point
        /// <see cref="GetIntersections(GeoRay3, GeoFace3, Tolerance)"/> gives, and false where it gives none.
        /// </summary>
        /// <remarks>
        /// The answer is the list's, so the two never disagree: the ray is read as the line without ends along it, and is given
        /// what that line meets at its origin or ahead of it, the crossing or the point of a touch. The ray from
        /// (10, 50, 0.0002) falling 4E-6 for each unit along X climbs 0.0004 across the square 100 by 100 in the floor with a
        /// corner at the origin and lies level over it within the planar tolerance of a thousandth, so it is refused, where it
        /// was given its crossing of the floor at (60, 50, 0) while the list gave none. The ray from (50, 50, 10) pointing
        /// straight down through a hole 40 by 40 in the middle of the square crosses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryIntersectWith(GeoRay3 ray, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance)
            => First(Meeting(ray, face, tolerance), out intersection);

        /// <summary>
        /// Gives the first of the points of a list, and false where it holds none, the point then the origin.
        /// </summary>
        private static bool First(GeoPoint3[] points, out GeoPoint3 intersection)
        {
            intersection = points.Length > 0 ? points[0] : GeoPoint3.Origin;
            return points.Length > 0;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses the material of a face, with the plane read as
        /// <see cref="TryCrossPlane(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance, bool)"/> reads it.
        /// </summary>
        internal static bool TryCrossFace(GeoRay3 ray, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance, bool countingCrossings)
        {
            intersection = GeoPoint3.Origin;

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!TryCrossPlane(ray, face.GetPlane(), out GeoPoint3 candidate, tolerance, countingCrossings))
            {
                return false;
            }

            if (!face.Contains(candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Finds every point where a line segment crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoSolid3 solid) => GetIntersections(line, solid, Tolerance.Global);

        /// <summary>
        /// Finds every point where a line segment crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossing points, in order along the segment.</returns>
        /// <remarks>
        /// The walls of the openings count as surface, since they too separate material from void. Two
        /// crossings closer together than the tolerance are reported once, so a segment grazing an edge
        /// does not come back as having entered and left.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, line.GetAabb(), tolerance);

            List<double> distances = new List<double>();
            CollectSolidCrossings(line, solid, tolerance, distances);

            distances.Sort();

            List<GeoPoint3> hits = new List<GeoPoint3>();

            foreach (double distance in distances)
            {
                if (hits.Count > 0 && distance - Parametrization3.GetDistanceAtPoint(line, hits[hits.Count - 1]) <= tolerance.EqualPoint)
                {
                    continue;
                }

                hits.Add(Parametrization3.GetPointAtDistance(line, distance));
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Gathers the arc lengths at which a segment meets the surface of a body and of its openings.
        /// </summary>
        private static void CollectSolidCrossings(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance, List<double> distances)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (TryIntersectWith(line, face, out GeoPoint3 hit, tolerance))
                {
                    distances.Add(Parametrization3.GetDistanceAtPoint(line, hit));
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                CollectSolidCrossings(line, opening, tolerance, distances);
            }
        }

        /// <summary>
        /// Finds where a line segment enters and leaves an axis-aligned box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoAabb3 box) => GetIntersections(line, box, Tolerance.Global);

        /// <summary>
        /// Finds where a line segment enters and leaves an axis-aligned box, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(line, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Finds where a ray enters and leaves an oriented box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoObb3 box) => GetIntersections(ray, box, Tolerance.Global);

        /// <summary>
        /// Finds where a ray enters and leaves an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray runs to infinity, so it is sampled as a segment long enough to reach past the far side of
        /// the box. Nothing beyond that point can be a crossing: past it the ray is only moving away. A ray starting within
        /// the point tolerance of the box and running away from it touches it at its origin: the ray from (5, 5, 10.0005) up
        /// along Z meets the box 10 by 10 by 10 there, where it was given nothing and collided.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            double reach = 0.0;

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                reach = Math.Max(reach, Parametrization3.GetDistanceAtPoint(ray, corner));
            }

            // A ray running away from every corner can still start within the tolerance of the box, and then touches it at
            // its origin: it is cut a tolerance long rather than passed over.
            return GetIntersections(ray.ToLine(Math.Max(reach, 0.0) + tolerance.EqualPoint), box, tolerance);
        }

        /// <summary>
        /// Finds where a ray enters and leaves an axis-aligned box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoAabb3 box) => GetIntersections(ray, box, Tolerance.Global);

        /// <summary>
        /// Finds where a ray enters and leaves an axis-aligned box, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(ray, box.ToObb(), tolerance);
        }

        #endregion

        #region Simplicity

        /// <summary>
        /// Checks whether a polygon is simple, using the default tolerance: no edge crosses or touches another
        /// except where neighbours share their vertex.
        /// </summary>
        public static bool IsSimple(GeoPolygon3 polygon) => IsSimple(polygon, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon is simple, within a tolerance: no edge crosses or touches another except where
        /// neighbours share their vertex.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance: edges closer than the point tolerance count as touching.</param>
        /// <returns>true if the polygon is simple; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="GeoPolygon3"/> checks that it is flat but not that it is simple, because the check costs
        /// more than building it. A vertex touching another edge counts as not simple, and so does an edge
        /// folding back over its neighbour. Edges are swept in order along X, so the cost grows with the number
        /// of edges whose extents overlap rather than with the square of the edge count. Where they overlap much
        /// along X, the sweep runs along whichever of X, Y and Z leaves the fewest pairs instead, with the same
        /// answer: a comb of 1 000 teeth 10 000 long laid along X, 4 000 edges, checked as it is and with a corner
        /// pushed in to touch the next tooth, took 35 ms swept along its teeth and takes 2.8 ms swept across them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool IsSimple(GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoLine3[] edges = polygon.GetEdges();
            int count = edges.Length;
            GeoAabb3[] bounds = new GeoAabb3[count];
            double[] keys = new double[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                bounds[i] = edges[i].GetAabb();
                keys[i] = bounds[i].Min.X;
                order[i] = i;
            }

            Array.Sort(keys, order);

            List<int> active = new List<int>();
            double slack = tolerance.EqualPoint;
            long budget = SweepAxisPairs * (long)count;

            foreach (int i in order)
            {
                active.RemoveAll(j => bounds[j].Max.X < bounds[i].Min.X - slack);

                // Past a few pairs for each edge, the axis that leaves the fewest is chosen, once.
                if ((budget -= active.Count) < 0)
                {
                    budget = long.MaxValue;
                    int axis = SweepAxis(bounds, slack);

                    if (axis != 0)
                    {
                        return IsSimpleAlong(axis, edges, bounds, order, tolerance);
                    }
                }

                foreach (int j in active)
                {
                    if (bounds[j].Max.Y < bounds[i].Min.Y - slack || bounds[j].Min.Y > bounds[i].Max.Y + slack ||
                        bounds[j].Max.Z < bounds[i].Min.Z - slack || bounds[j].Min.Z > bounds[i].Max.Z + slack)
                    {
                        continue;
                    }

                    bool iThenJ = j == (i + 1) % count;
                    bool jThenI = i == (j + 1) % count;

                    if (iThenJ || jThenI)
                    {
                        // Neighbours share a vertex and must meet nowhere else: the far end of either lying on the
                        // other means the boundary folds back over itself.
                        GeoLine3 incoming = iThenJ ? edges[i] : edges[j];
                        GeoLine3 outgoing = iThenJ ? edges[j] : edges[i];

                        if (Containment3.IsPointOn(outgoing, incoming.StartPoint, tolerance) ||
                            Containment3.IsPointOn(incoming, outgoing.EndPoint, tolerance))
                        {
                            return false;
                        }
                    }
                    else if (Projection3.GetShortestLineTo(edges[i], edges[j], tolerance).Length <= tolerance.EqualPoint)
                    {
                        return false;
                    }
                }

                active.Add(i);
            }

            return true;
        }

        /// <summary>
        /// How many pairs for each edge <see cref="IsSimple(GeoPolygon3, Tolerance)"/> looks at sweeping along X before
        /// it chooses the axis to sweep along. Choosing costs three sorts and three binary searches for each edge, as
        /// much as the sweep itself where the edges overlap little along X, so it is paid for only where they overlap
        /// much; every axis gives the same answer.
        /// </summary>
        internal const long SweepAxisPairs = 8;

        /// <summary>
        /// The axis a sweep of the edges' boxes looks at the fewest pairs along, 0 for X, 1 for Y and 2 for Z, the first
        /// of them where two look at as many.
        /// </summary>
        /// <param name="bounds">The boxes of the edges.</param>
        /// <param name="slack">How far apart two boxes may stand and still be measured: the point tolerance.</param>
        internal static int SweepAxis(GeoAabb3[] bounds, double slack)
        {
            int count = bounds.Length;
            var lows = new double[count];
            var highs = new double[count];
            int axis = 0;
            long fewest = long.MaxValue;

            for (int candidate = 0; candidate < 3; candidate++)
            {
                for (int i = 0; i < count; i++)
                {
                    lows[i] = Along(bounds[i].Min, candidate);
                    highs[i] = Along(bounds[i].Max, candidate) + slack;
                }

                Array.Sort(lows);
                long tried = Sweeps.Tried(lows, highs);

                if (tried < fewest)
                {
                    fewest = tried;
                    axis = candidate;
                }
            }

            return axis;
        }

        /// <summary>
        /// Checks whether a polygon is simple by a sweep of its edges along Y or Z, measuring every pair the sweep
        /// along X measures, as it measures them.
        /// </summary>
        /// <param name="axis">The axis to sweep along: 1 for Y, 2 for Z.</param>
        /// <param name="edges">The edges.</param>
        /// <param name="bounds">The boxes of the edges.</param>
        /// <param name="order">The edges in the order the sort along X left them, which says which of two the sweep along
        /// X came to second.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The sweep along X compared the boxes of two edges, and measured the distance between them, with the edge it
        /// came to second first, and the shortest line between parallel edges is not the same in the last bit taken the
        /// other way round. So whichever of two edges this sweep comes to second, they are measured the way round the
        /// sweep along X took them. An open edge is let go once it is clear of the one swept in either way round.
        /// </remarks>
        private static bool IsSimpleAlong(int axis, GeoLine3[] edges, GeoAabb3[] bounds, int[] order, Tolerance tolerance)
        {
            int count = edges.Length;
            double slack = tolerance.EqualPoint;
            var rank = new int[count];
            var lows = new double[count];
            var sweep = new int[count];

            for (int k = 0; k < count; k++)
            {
                rank[order[k]] = k;
            }

            for (int i = 0; i < count; i++)
            {
                lows[i] = Along(bounds[i].Min, axis);
                sweep[i] = i;
            }

            Array.Sort(sweep, (a, b) =>
            {
                int byLow = lows[a].CompareTo(lows[b]);
                return byLow != 0 ? byLow : a.CompareTo(b);
            });

            List<int> active = new List<int>();

            foreach (int edge in sweep)
            {
                double low = lows[edge];

                active.RemoveAll(o =>
                {
                    double high = Along(bounds[o].Max, axis);
                    return high < low - slack && low > high + slack;
                });

                foreach (int o in active)
                {
                    int i = rank[edge] > rank[o] ? edge : o;
                    int j = i == edge ? o : edge;

                    if (bounds[j].Max.X < bounds[i].Min.X - slack ||
                        bounds[j].Max.Y < bounds[i].Min.Y - slack || bounds[j].Min.Y > bounds[i].Max.Y + slack ||
                        bounds[j].Max.Z < bounds[i].Min.Z - slack || bounds[j].Min.Z > bounds[i].Max.Z + slack)
                    {
                        continue;
                    }

                    bool iThenJ = j == (i + 1) % count;
                    bool jThenI = i == (j + 1) % count;

                    if (iThenJ || jThenI)
                    {
                        GeoLine3 incoming = iThenJ ? edges[i] : edges[j];
                        GeoLine3 outgoing = iThenJ ? edges[j] : edges[i];

                        if (Containment3.IsPointOn(outgoing, incoming.StartPoint, tolerance) ||
                            Containment3.IsPointOn(incoming, outgoing.EndPoint, tolerance))
                        {
                            return false;
                        }
                    }
                    else if (Projection3.GetShortestLineTo(edges[i], edges[j], tolerance).Length <= tolerance.EqualPoint)
                    {
                        return false;
                    }
                }

                active.Add(edge);
            }

            return true;
        }

        /// <summary>A coordinate of a point: 0 for X, 1 for Y and 2 for Z.</summary>
        private static double Along(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        #endregion
    }
}
