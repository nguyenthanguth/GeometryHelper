using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where two curves in space cross each other, and whether they touch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both cases come down to a line, which is why they share
    /// <see cref="PointsOnLine(GeoArc3, GeoPoint3, GeoVector3, Tolerance)"/>.
    /// </para>
    /// <para>
    /// <b>Not coplanar.</b> The two planes meet in a line and anything common to both curves is on it.
    /// </para>
    /// <para>
    /// <b>Coplanar.</b> Two circles in one plane meet on their radical line, which is square to the line
    /// joining their centres and at <c>(d² + r₁² - r₂²) / 2d</c> along it. Every point of one circle on that
    /// line is on the other as well, so the candidates come out of the same quadratic as everywhere else and
    /// no projection into the plane is needed.
    /// </para>
    /// <para>
    /// <b>Two arcs of one circle</b> are the case that is not about points at all. Concentric circles of equal
    /// radius lie on each other and share a whole stretch, so there is nothing to name as a crossing; whether
    /// they touch is settled by asking whether either holds an end of the other, which is where an overlap
    /// begins and ends. <see cref="Collision2"/> reaches the same answer the same way in the plane.
    /// </para>
    /// </remarks>
    public static partial class Arc3
    {
        /// <summary>
        /// Determines whether two arcs run on the very same circle.
        /// </summary>
        private static bool OnOneCircle(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            return IsCoplanar(first, second.GetPlane(), tolerance)
                && first.Center.IsEqualTo(second.Center, tolerance)
                && Math.Abs(first.Radius - second.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets the points two coplanar arcs share, which lie on the radical line of their circles.
        /// </summary>
        private static List<GeoPoint3> OnRadicalLine(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            GeoVector3 between = first.Center.GetVectorTo(second.Center);
            double apart = between.Length;

            // Concentric: either the same circle, which meets along its length rather than at points, or one
            // inside the other with nothing shared. Neither names a point.
            if (apart <= tolerance.EqualPoint)
            {
                return found;
            }

            double along = (apart * apart + first.Radius * first.Radius - second.Radius * second.Radius) / (2.0 * apart);
            GeoPoint3 foot = first.Center.Add(between.Multiply(along / apart));
            GeoVector3 across = first.Normal.CrossProduct(between);

            foreach (GeoPoint3 candidate in PointsOnLine(first, foot, across, tolerance))
            {
                if (second.IsPointOn(candidate, tolerance))
                {
                    AddOnce(found, candidate, tolerance);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets every point where two arcs cross.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 first, GeoArc3 second)
            => GetIntersections(first, second, Tolerance.Global);

        /// <summary>
        /// Gets every point where two arcs cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// At most two points. Two arcs of one circle give none: they share a whole stretch rather than a
        /// point, which <see cref="CollidesWith(GeoArc3, GeoArc3, Tolerance)"/> reports instead.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            if (IsCoplanar(first, second.GetPlane(), tolerance))
            {
                return OnRadicalLine(first, second, tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(first, second.GetPlane(), tolerance))
            {
                if (second.IsPointOn(candidate, tolerance))
                {
                    AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether two arcs touch.
        /// </summary>
        public static bool CollidesWith(GeoArc3 first, GeoArc3 second) => CollidesWith(first, second, Tolerance.Global);

        /// <summary>
        /// Determines whether two arcs touch, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Two arcs of one circle cross nowhere and still touch wherever their sweeps overlap, so those are
        /// settled by asking whether either holds an end of the other.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            if (OnOneCircle(first, second, tolerance))
            {
                return first.IsPointOn(second.StartPoint, tolerance)
                    || first.IsPointOn(second.EndPoint, tolerance)
                    || second.IsPointOn(first.StartPoint, tolerance)
                    || second.IsPointOn(first.EndPoint, tolerance);
            }

            return GetIntersections(first, second, tolerance).Length > 0;
        }

        /// <summary>
        /// Tries to find where two arcs cross.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 first, GeoArc3 second, out GeoPoint3[] intersections)
            => TryIntersectWith(first, second, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two arcs cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 first, GeoArc3 second, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(first, second, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where an arc crosses a circle.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoCircle3 circle)
            => GetIntersections(arc, AsArc(circle));

        /// <summary>
        /// Gets every point where an arc crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoCircle3 circle, Tolerance tolerance)
            => GetIntersections(arc, AsArc(circle), tolerance);

        /// <summary>
        /// Determines whether an arc touches a circle.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoCircle3 circle) => CollidesWith(arc, AsArc(circle));

        /// <summary>
        /// Determines whether an arc touches a circle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoArc3 arc, GeoCircle3 circle, Tolerance tolerance)
            => CollidesWith(arc, AsArc(circle), tolerance);

        /// <summary>
        /// Tries to find where an arc crosses a circle.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoCircle3 circle, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, AsArc(circle), out intersections);

        /// <summary>
        /// Tries to find where an arc crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(arc, AsArc(circle), out intersections, tolerance);

        /// <summary>
        /// Gets every point where two circles cross.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoCircle3 first, GeoCircle3 second)
            => GetIntersections(AsArc(first), AsArc(second));

        /// <summary>
        /// Gets every point where two circles cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first circle.</param>
        /// <param name="second">The second circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoCircle3 first, GeoCircle3 second, Tolerance tolerance)
            => GetIntersections(AsArc(first), AsArc(second), tolerance);

        /// <summary>
        /// Determines whether two circles touch.
        /// </summary>
        public static bool CollidesWith(GeoCircle3 first, GeoCircle3 second) => CollidesWith(AsArc(first), AsArc(second));

        /// <summary>
        /// Determines whether two circles touch, within a tolerance.
        /// </summary>
        /// <param name="first">The first circle.</param>
        /// <param name="second">The second circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool CollidesWith(GeoCircle3 first, GeoCircle3 second, Tolerance tolerance)
            => CollidesWith(AsArc(first), AsArc(second), tolerance);

        /// <summary>
        /// Tries to find where two circles cross.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle3 first, GeoCircle3 second, out GeoPoint3[] intersections)
            => TryIntersectWith(AsArc(first), AsArc(second), out intersections);

        /// <summary>
        /// Tries to find where two circles cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first circle.</param>
        /// <param name="second">The second circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 first, GeoCircle3 second, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(AsArc(first), AsArc(second), out intersections, tolerance);
    }
}
