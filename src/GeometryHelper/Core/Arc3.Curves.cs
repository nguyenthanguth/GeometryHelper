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
        /// Gets the points two coplanar arcs share, which lie on the radical line of their circles.
        /// </summary>
        private static List<GeoPoint3> OnRadicalLine(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            GeoVector3 between = first.Center.GetVectorTo(second.Center);
            double apart = between.Length;

            // Concentric: either the same circle, which meets along its length rather than at points, or one
            // inside the other with nothing shared. Neither names a point. Centres closer than the point tolerance are one
            // where the circles stay within the tolerance of each other all round, or where the rounding of the numbers leaves
            // no line to cross on, as in the plane (Intersection2).
            if (apart <= tolerance.EqualPoint && Intersection2.AboutOneCentre(apart, Scale(first, second), first.Radius, second.Radius, tolerance))
            {
                return found;
            }

            // One circle a hair inside the other touches it without crossing, and the radical line is no guide there,
            // as in the plane (Intersection2): it runs away from both as the difference of the radii shrinks. The touch
            // is the point between the two rims where they come nearest, on the line through the centres.
            double inside = Math.Abs(first.Radius - second.Radius) - apart;

            if (inside > 0.0)
            {
                if (inside <= tolerance.EqualPoint)
                {
                    double side = first.Radius >= second.Radius ? 1.0 : -1.0;
                    GeoPoint3 middle = first.Center.Add(between.Multiply(0.5));
                    GeoPoint3 touch = middle.Add(between.Multiply(side * 0.5 * (first.Radius + second.Radius) / apart));

                    if (first.IsPointOn(touch, tolerance) && second.IsPointOn(touch, tolerance))
                    {
                        found.Add(touch);
                    }
                }

                return found;
            }

            // One a hair outside the other is the same: the radical line runs between the rims, off both, and the
            // touch is the middle of the gap on the line through the centres.
            double outside = apart - (first.Radius + second.Radius);

            if (outside > 0.0)
            {
                if (outside <= tolerance.EqualPoint)
                {
                    GeoPoint3 touch = first.Center.Add(between.Multiply((first.Radius + 0.5 * outside) / apart));

                    if (first.IsPointOn(touch, tolerance) && second.IsPointOn(touch, tolerance))
                    {
                        found.Add(touch);
                    }
                }

                return found;
            }

            // Worked from the smaller circle, as in the plane (Intersection2): the foot is placed from its centre and the
            // radical line crossed with that circle. From the circle of radius 2.5E8 behind an edge 100 long of bulge
            // 1E-7, the square of the half chord is the difference of two squares near 6E16, and the crossings with a
            // circle of radius 40 were lost. The line still runs the way it did, so the points come in the same order.
            // The difference of the squared radii is taken as the product of their difference and their sum, as in the plane.
            // The line runs square to the centres, along a vector as long as they are apart; where that is less than the point
            // tolerance it is made a unit long, which the vector tolerance would otherwise take for no direction at all.
            GeoVector3 across = first.Normal.CrossProduct(between);

            if (apart <= tolerance.EqualPoint)
            {
                across = across.Multiply(1.0 / apart);
            }

            if (second.Radius < first.Radius)
            {
                double back = (apart * apart + (second.Radius - first.Radius) * (second.Radius + first.Radius)) / (2.0 * apart);
                GeoPoint3 nearer = second.Center.Add(between.Multiply(-back / apart));

                foreach (GeoPoint3 candidate in PointsOnLine(second, nearer, across, tolerance))
                {
                    if (first.IsPointOn(candidate, tolerance))
                    {
                        AddOnce(found, candidate, tolerance);
                    }
                }

                return found;
            }

            double along =(apart * apart + (first.Radius - second.Radius) * (first.Radius + second.Radius)) / (2.0 * apart);
            GeoPoint3 foot = first.Center.Add(between.Multiply(along / apart));

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
        /// The largest coordinate of the centres of two arcs, or radius, whichever is larger: the size of the numbers whose
        /// rounding <see cref="Intersection2.AboutOneCentre(double, double, double, double, Tolerance)"/> weighs.
        /// </summary>
        private static double Scale(GeoArc3 first, GeoArc3 second)
        {
            GeoPoint3 a = first.Center;
            GeoPoint3 b = second.Center;
            double coordinates = Math.Max(
                Math.Max(Math.Max(Math.Abs(a.X), Math.Abs(a.Y)), Math.Abs(a.Z)),
                Math.Max(Math.Max(Math.Abs(b.X), Math.Abs(b.Y)), Math.Abs(b.Z)));

            return Math.Max(coordinates, Math.Max(first.Radius, second.Radius));
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
        /// The points the two share, the same to the last digit whichever is asked first, in order along a line: in one plane
        /// the line square to the one between the centres, otherwise the line the two planes share. Asked the other way round,
        /// they come in the opposite order. Two arcs of one circle give none: they share a whole stretch rather than a point,
        /// which <see cref="CollidesWith(GeoArc3, GeoArc3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Two arcs in one plane are crossed from the smaller circle, so a nearly flat arc keeps its crossings whichever
        /// is asked first: the edge 100 long of bulge 1E-7 from (0, 0, 0) to (100, 0, 0), an arc of radius 2.5E8, crosses
        /// the arc of radius 40 about (50, 30, 0) sweeping 1.6 radians at (23.54249, 0, 0) either way round.
        /// </para>
        /// <para>
        /// Each arc is read against the other, and a place both readings name is given once, as the first names it: the
        /// first reading is that of the arc lying within the planar tolerance of the other's plane, or, where both or
        /// neither do, of the one that comes first by centre, radius, normal and angles. Where the two pass within the point
        /// tolerance of each other and no reading names a place there, as <see cref="CollidesWith(GeoArc3, GeoArc3, Tolerance)"/>
        /// reads a touch, the middle of the shortest line between them is the place: the arc of radius 100 about the origin
        /// in the plan and an arc of radius 50 leaning 0.01 radians, the end of its diameter along X 0.0005 above
        /// (0, 100, 0) and running square to the first there, touch at (0, 100, 0.00025). Read from the arc asked first
        /// alone, 37 of 4 000 pairs tilted 1E-7 to 1E-3 radians off each other gave a different number of places the one way
        /// round and the other, and 356 collided and were given none.
        /// </para>
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
            => Shared(first, second, tolerance);

        /// <summary>
        /// Gets the places two arcs share, read both ways from the one that leads, with where they pass within the tolerance
        /// of each other, in the order the first asked puts them in.
        /// </summary>
        private static GeoPoint3[] Shared(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            bool firstInPlane = IsCoplanar(first, second.GetPlane(), tolerance);
            bool secondInPlane = IsCoplanar(second, first.GetPlane(), tolerance);
            bool swapped = firstInPlane == secondInPlane ? ComesFirst(second, first) : secondInPlane;
            GeoArc3 lead = swapped ? second : first;
            GeoArc3 follow = swapped ? first : second;

            // Two arcs of one circle meet along a stretch, not at a place.
            if ((firstInPlane || secondInPlane) && OnOneCircle(lead, follow, tolerance))
            {
                return new GeoPoint3[0];
            }

            var points = new List<GeoPoint3>(Read(lead, follow, tolerance));

            foreach (GeoPoint3 point in Read(follow, lead, tolerance))
            {
                AddPlace(points, point, lead, follow, tolerance);
            }

            NearPass3.Add(points, NearPass3.Rim.Of(lead, tolerance), NearPass3.Target.Of(follow, tolerance), tolerance);

            // Along the line read from the one that leads, turned round for the one asked first where that is the other, so
            // that the two orders are each other's reverse whichever way the normals point.
            GeoVector3 direction = (firstInPlane || secondInPlane
                ? lead.Normal.CrossProduct(lead.Center.GetVectorTo(follow.Center))
                : AlongShared(lead.Normal, follow.Normal, lead.Center, follow.Center, lead.Center.GetVectorTo(lead.StartPoint), tolerance))
                .Multiply(swapped ? -1.0 : 1.0);
            var keys = new double[points.Count];
            var order = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                keys[i] = lead.Center.GetVectorTo(points[i]).DotProduct(direction);
                order[i] = i;
            }

            Array.Sort(order, (x, y) =>
            {
                int byKey = keys[x].CompareTo(keys[y]);

                return byKey != 0 ? byKey : x.CompareTo(y);
            });

            var sorted = new GeoPoint3[points.Count];

            for (int i = 0; i < order.Length; i++)
            {
                sorted[i] = points[order[i]];
            }

            return sorted;
        }

        /// <summary>
        /// Gets the direction the places of two curves not in one plane are put in order along: the line the two planes
        /// share; where the normals are parallel within the vector tolerance, so that the planes share none, the line square
        /// to the one between the centres in the first's plane; and with the centres one above the other besides, a
        /// direction of the first's own. Two circles of radius 100 and 90 in planes 0.005 apart, read in space at a point
        /// tolerance of 0.01, came in the order they were found whichever was asked first.
        /// </summary>
        internal static GeoVector3 AlongShared(GeoVector3 normal, GeoVector3 otherNormal, GeoPoint3 center, GeoPoint3 otherCenter, GeoVector3 own, Tolerance tolerance)
        {
            GeoVector3 shared = normal.CrossProduct(otherNormal);

            if (shared.Length > tolerance.EqualVector)
            {
                return shared;
            }

            GeoVector3 across = normal.CrossProduct(center.GetVectorTo(otherCenter));

            return across.Length > tolerance.EqualPoint ? across : own;
        }

        /// <summary>
        /// Determines whether two arcs lie on one circle, as <see cref="OnRadicalLine"/> reads it: centres within the point
        /// tolerance, and the circles within it of each other all round.
        /// </summary>
        private static bool OnOneCircle(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            double apart = first.Center.DistanceTo(second.Center);

            return apart <= tolerance.EqualPoint && Intersection2.AboutOneCentre(apart, Scale(first, second), first.Radius, second.Radius, tolerance);
        }

        /// <summary>
        /// Determines whether an arc comes before another in a fixed order of the two: by centre, coordinate by coordinate,
        /// then by radius, normal and the angles it starts at and sweeps.
        /// </summary>
        private static bool ComesFirst(GeoArc3 arc, GeoArc3 other)
        {
            double[] one = { arc.Center.X, arc.Center.Y, arc.Center.Z, arc.Radius, arc.Normal.X, arc.Normal.Y, arc.Normal.Z, arc.StartAngle, arc.SweptAngle };
            double[] two = { other.Center.X, other.Center.Y, other.Center.Z, other.Radius, other.Normal.X, other.Normal.Y, other.Normal.Z, other.StartAngle, other.SweptAngle };

            for (int i = 0; i < one.Length; i++)
            {
                int order = one[i].CompareTo(two[i]);

                if (order != 0)
                {
                    return order < 0;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the points of one arc on another, read from the first: on the radical line in one plane, on the line the two
        /// planes share otherwise.
        /// </summary>
        private static GeoPoint3[] Read(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
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
        /// They touch where the exact gap between them is no more than the point tolerance, in space and in whatever planes
        /// they lie: two arcs of one circle touch wherever their sweeps overlap or end within the tolerance of one another,
        /// the arc of radius 50 about the origin from (50, 0, 0) a quarter turn counter-clockwise touches the arc of radius
        /// 50.0005 about the same centre, and not the arc of radius 50.0015.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 first, GeoArc3 second, Tolerance tolerance)
        {
            Gap3.Curve one = Gap3.Curve.Of(first);
            Gap3.Curve other = Gap3.Curve.Of(second);

            return one.Box.DistanceTo(other.Box) <= Collision3.Reach(tolerance)
                && one.GapToCurve(other, tolerance.EqualPoint) <= tolerance.EqualPoint;
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
            => GetIntersections(first, second, Tolerance.Global);

        /// <summary>
        /// Gets every point where two circles cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first circle.</param>
        /// <param name="second">The second circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The points the two share, the same to the last digit whichever circle is asked first, in order along a line: in one
        /// plane the line square to the one between the centres, otherwise the line the two planes share. Asked the other way
        /// round, they come in the opposite order, whichever way the two normals point.
        /// </returns>
        /// <remarks>
        /// Each circle is read against the other, and a place both readings name is given once, as the first names it. The
        /// first is the one lying within the planar tolerance of the other's plane, or, where both or neither do, the one
        /// that comes first by centre, radius and normal. Read from the circle asked first alone, the circle of radius 473.706
        /// about the origin in the plan crossed the circle of radius 477.624 about (-37.520, -84.167, 0.0009), tilted 4.6E-6
        /// radians off it, at (421.493, -216.198, 0) asked the one way, and at (-442.545, 168.972, 0.001) as well asked the
        /// other; both ways now give both. Where the two pass within the point tolerance of each other and no reading names a
        /// place there, as <see cref="CollidesWith(GeoCircle3, GeoCircle3, Tolerance)"/> reads a touch, the middle of the
        /// shortest line between them is the place, as for two arcs.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoCircle3 first, GeoCircle3 second, Tolerance tolerance)
            => Shared(AsArc(first), AsArc(second), tolerance);

        /// <summary>
        /// Adds a point two arcs share unless a place already named stands within the tolerance of it, or both arcs pass
        /// within the tolerance of the point halfway between the two.
        /// </summary>
        private static void AddPlace(List<GeoPoint3> found, GeoPoint3 point, GeoArc3 one, GeoArc3 other, Tolerance tolerance)
        {
            foreach (GeoPoint3 named in found)
            {
                GeoPoint3 halfway = named.Add(named.GetVectorTo(point).Multiply(0.5));

                if (named.IsEqualTo(point, tolerance) || (one.IsPointOn(halfway, tolerance) && other.IsPointOn(halfway, tolerance)))
                {
                    return;
                }
            }

            found.Add(point);
        }

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
            => TryIntersectWith(first, second, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two circles cross, within a tolerance.
        /// </summary>
        /// <param name="first">The first circle.</param>
        /// <param name="second">The second circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoCircle3 first, GeoCircle3 second, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(first, second, tolerance);

            return intersections.Length > 0;
        }
    }
}
