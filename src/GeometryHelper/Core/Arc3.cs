using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where an arc in space crosses a plane, a segment or a ray, and whether it touches one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// None of this is sampled. An arc in space lies in a plane, and every flat-faced shape in the library is
    /// made of planes, so every crossing has a closed form and there are never more than two of them per
    /// plane. Two readings do all the work.
    /// </para>
    /// <para>
    /// <b>Coplanar.</b> Where the other shape shares the arc's plane, both go down into that plane through
    /// <see cref="PlanarMap"/> and the whole of <see cref="Arc2"/> answers, which is code already worn in.
    /// </para>
    /// <para>
    /// <b>Not coplanar.</b> Two distinct planes meet in a line, and every point the arc shares with anything
    /// lying in the other plane must be on that line. A line meets a circle in at most two points, so the
    /// candidates are found by solving one quadratic and are then handed to whatever test the other shape
    /// brings. No iteration, and no tolerance bargain.
    /// </para>
    /// <para>
    /// Distance is the one thing not offered. The distance from an arc in space to anything but a point has
    /// no closed form — a segment wants a quartic — and <c>ToPolyline3(chordTolerance)</c> remains the way to
    /// ask for it, erring on the safe side because a sampled chain lies inside the arcs it stands for.
    /// </para>
    /// </remarks>
    public static partial class Arc3
    {
        #region Reading an arc in its own plane

        /// <summary>
        /// Determines whether a plane is the very plane the arc lies in.
        /// </summary>
        /// <remarks>
        /// Being parallel is not enough: two parallel planes a metre apart share nothing. The arc's centre
        /// has to lie in the other plane as well, and that is what separates the one case where the whole
        /// arc is in the plane from the case where it merely runs alongside.
        /// </remarks>
        internal static bool IsCoplanar(GeoArc3 arc, GeoPlane3 plane, Tolerance tolerance)
        {
            return Parallel3.IsParallel(arc.GetPlane(), plane, tolerance)
                && Containment3.IsPointOn(plane, arc.Center, tolerance);
        }

        /// <summary>
        /// Gets the points of the arc that lie on an endless line, which is at most two.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="origin">A point of the line.</param>
        /// <param name="direction">The direction of the line; it need not be a unit vector.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The points shared by the arc and the line, in order along the line.</returns>
        /// <remarks>
        /// The line is read as endless, so a caller holding a segment or a ray has to say afterwards which of
        /// the answers its own shape reaches. Solving <c>|origin + t*direction - centre|² = radius²</c> finds
        /// every point of the arc's <i>circle</i> on the line; <see cref="GeoArc3.IsPointOn(GeoPoint3, Tolerance)"/>
        /// then keeps the ones the arc actually sweeps, which is also what rules out a line that lies in a
        /// parallel plane rather than the arc's own.
        /// </remarks>
        internal static List<GeoPoint3> PointsOnLine(GeoArc3 arc, GeoPoint3 origin, GeoVector3 direction, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            double a = direction.LengthSquared;

            if (a <= tolerance.EqualVector * tolerance.EqualVector)
            {
                // No direction to follow. The one point there is counts if the arc reaches it.
                if (arc.IsPointOn(origin, tolerance))
                {
                    found.Add(origin);
                }

                return found;
            }

            GeoVector3 fromCentre = arc.Center.GetVectorTo(origin);
            double b = 2.0 * fromCentre.DotProduct(direction);
            double c = fromCentre.LengthSquared - arc.Radius * arc.Radius;
            double discriminant = b * b - 4.0 * a * c;

            // A line that grazes the circle gives a discriminant of nought, and rounding puts it either side,
            // so anything within the tolerance of nought is read as the single touching point rather than as
            // two points a hair apart or as none at all.
            double graze = tolerance.EqualPoint * tolerance.EqualPoint * a * a;

            if (discriminant < -graze)
            {
                return found;
            }

            if (discriminant <= graze)
            {
                GeoPoint3 touching = origin.Add(direction.Multiply(-b / (2.0 * a)));

                if (arc.IsPointOn(touching, tolerance))
                {
                    found.Add(touching);
                }

                return found;
            }

            double root = Math.Sqrt(discriminant);

            foreach (double t in new[] { (-b - root) / (2.0 * a), (-b + root) / (2.0 * a) })
            {
                GeoPoint3 crossing = origin.Add(direction.Multiply(t));

                if (arc.IsPointOn(crossing, tolerance))
                {
                    found.Add(crossing);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the points of the arc on the line where its own plane meets another plane.
        /// </summary>
        /// <remarks>
        /// The workhorse of every not-coplanar pair. Where the two planes are parallel and apart there is no
        /// line and nothing to find; where they are the same plane the caller has to handle that itself,
        /// because what to do then depends on the shape, not on the arc.
        /// </remarks>
        internal static List<GeoPoint3> PointsOnPlane(GeoArc3 arc, GeoPlane3 plane, Tolerance tolerance)
        {
            if (!Intersection3.TryIntersectWith(arc.GetPlane(), plane, out GeoRay3 meeting, tolerance))
            {
                return new List<GeoPoint3>();
            }

            return PointsOnLine(arc, meeting.Origin, meeting.Direction, tolerance);
        }

        /// <summary>
        /// Adds a point to a list unless a point already there stands in the same place.
        /// </summary>
        /// <remarks>
        /// Two faces of a body share an edge, so an arc crossing that edge is found twice. The answer has to
        /// name the place once.
        /// </remarks>
        internal static void AddOnce(List<GeoPoint3> found, GeoPoint3 point, Tolerance tolerance)
        {
            foreach (GeoPoint3 already in found)
            {
                if (already.IsEqualTo(point, tolerance))
                {
                    return;
                }
            }

            found.Add(point);
        }

        #endregion

        #region Against a plane

        /// <summary>
        /// Gets every point where an arc crosses a plane.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoPlane3 plane)
            => GetIntersections(arc, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a plane, within a tolerance.
        /// </summary>
        /// <returns>
        /// At most two points. An arc lying in the plane crosses it nowhere and gives none: it is in the
        /// plane rather than through it, which <see cref="CollidesWith(GeoArc3, GeoPlane3, Tolerance)"/>
        /// reports instead.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoPlane3 plane, Tolerance tolerance)
        {
            if (IsCoplanar(arc, plane, tolerance))
            {
                return new GeoPoint3[0];
            }

            return PointsOnPlane(arc, plane, tolerance).ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoPlane3 plane) => CollidesWith(arc, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An arc lying in the plane touches it everywhere, so this is true where
        /// <see cref="GetIntersections(GeoArc3, GeoPlane3, Tolerance)"/> is empty. The two answer different
        /// questions and that is the one case where they part.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoPlane3 plane, Tolerance tolerance)
        {
            return IsCoplanar(arc, plane, tolerance) || PointsOnPlane(arc, plane, tolerance).Count > 0;
        }

        /// <summary>
        /// Tries to find where an arc crosses a plane.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoPlane3 plane, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, plane, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, plane, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a segment and a ray

        /// <summary>
        /// Gets every point where an arc crosses a segment.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoLine3 line)
            => GetIntersections(arc, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a segment, within a tolerance.
        /// </summary>
        /// <returns>
        /// At most two points, and only ones the segment itself reaches: the arithmetic reads the segment as
        /// the endless line carrying it and then keeps what lies between its ends.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoLine3 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnLine(arc, line.StartPoint, line.Direction, tolerance))
            {
                if (Containment3.IsPointOn(line, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoLine3 line) => CollidesWith(arc, line, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they touch only where they cross.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoLine3 line, Tolerance tolerance)
            => GetIntersections(arc, line, tolerance).Length > 0;

        /// <summary>
        /// Tries to find where an arc crosses a segment.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where an arc crosses a ray.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoRay3 ray) => GetIntersections(arc, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where an arc crosses a ray, within a tolerance.
        /// </summary>
        /// <returns>At most two points, and only ones ahead of where the ray starts.</returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoRay3 ray, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnLine(arc, ray.Origin, ray.Direction, tolerance))
            {
                if (Containment3.IsPointOn(ray, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether an arc touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoRay3 ray) => CollidesWith(arc, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a ray, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoRay3 ray, Tolerance tolerance)
            => GetIntersections(arc, ray, tolerance).Length > 0;

        /// <summary>
        /// Tries to find where an arc crosses a ray.
        /// </summary>
        public static bool TryIntersectWith(GeoArc3 arc, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(arc, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an arc crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoArc3 arc, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(arc, ray, tolerance);

            return intersections.Length > 0;
        }

        #endregion
    }
}
