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
            // The circle stands off a plane through its centre by up to the sine of the angle between the two
            // times its radius; within the planar tolerance it lies in the plane. Parallel within the angle
            // tolerance was not enough: a whole degree off, a circle of a metre stands seventeen off the plane.
            double sine = arc.GetPlane().Normal.CrossProduct(plane.Normal).Length;

            return sine * arc.Radius <= tolerance.EqualPlanar
                && Containment3.IsPointOn(plane, arc.Center, tolerance);
        }

        /// <summary>
        /// Gets the bulge and the normal of the arc from a start through a point to an end, read from its chord.
        /// </summary>
        /// <param name="toEnd">From the start to the end.</param>
        /// <param name="toMiddle">From the start to a point of the arc between its ends.</param>
        /// <param name="bulge">The bulge, positive: the arc runs counter-clockwise round the normal.</param>
        /// <param name="unitNormal">The unit normal of its plane.</param>
        /// <returns>false when the three points lie on one line to the last digit, so no arc runs through them.</returns>
        /// <remarks>
        /// Nothing is measured from the centre: the sag is worked out across the chord, so an arc 100 long bowing a
        /// trillionth, of radius 2.5E13, comes out as exactly as a half circle. The point need not be the arc's middle:
        /// an arc stretched unevenly is answered with the arc through where its ends and its middle land.
        /// </remarks>
        internal static bool TryBulgeThrough(GeoVector3 toEnd, GeoVector3 toMiddle, out double bulge, out GeoVector3 unitNormal)
        {
            bulge = 0.0;
            unitNormal = default(GeoVector3);

            double chord = toEnd.Length;

            if (!(chord > 0.0))
            {
                return false;
            }

            GeoVector3 along = toEnd.Multiply(1.0 / chord);
            GeoVector3 offMiddle = toMiddle.Subtract(toEnd.Multiply(0.5));
            double ahead = offMiddle.DotProduct(along);
            GeoVector3 aside = offMiddle.Subtract(along.Multiply(ahead));
            double off = aside.Length;

            if (!(off > 0.0))
            {
                return false;
            }

            // The centre stands back from the chord's middle, away from the point, by `back`; past a half turn it stands
            // on the point's side and `back` is negative. The sag on the point's side is the radius less `back`, worked
            // out without the difference of two near-equal large numbers.
            double halfSquared = chord * chord * 0.25;
            double back = (halfSquared - ahead * ahead - off * off) / (2.0 * off);
            double radius = Math.Sqrt(halfSquared + back * back);
            double sag = back >= 0.0 ? halfSquared / (radius + back) : radius - back;

            GeoVector3 bow = aside.Multiply(1.0 / off);
            bulge = 2.0 * sag / chord;
            unitNormal = bow.CrossProduct(along);
            return true;
        }

        /// <summary>
        /// Determines whether an arc between two points lies in a plane: whether no point of it stands further off the
        /// plane than the planar tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="end">Where it ends.</param>
        /// <param name="bulge">Its bulge; nought for a straight piece.</param>
        /// <param name="unitNormal">The unit normal of the plane it bulges in.</param>
        /// <param name="tolerance">The tolerance; its planar threshold decides.</param>
        /// <remarks>
        /// Its normal against the plane's says nothing on its own: a half circle 1 000 across turned 0.99 degrees out of
        /// the plane stands 8.64 off it, and an arc 100 long bowing a ten-millionth turned 30 degrees stands 0.0000025
        /// off. See <see cref="StandOff(GeoPlane3, GeoPoint3, GeoPoint3, double, GeoVector3)"/>.
        /// </remarks>
        internal static bool LiesIn(GeoPlane3 plane, GeoPoint3 start, GeoPoint3 end, double bulge, GeoVector3 unitNormal, Tolerance tolerance)
            => !(StandOff(plane, start, end, bulge, unitNormal) > tolerance.EqualPlanar);

        /// <summary>
        /// Gets how far an arc between two points may stand off a plane: no less than any point of it does.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="start">Where the arc starts.</param>
        /// <param name="end">Where it ends.</param>
        /// <param name="bulge">Its bulge; nought for a straight piece.</param>
        /// <param name="unitNormal">The unit normal of the plane it bulges in.</param>
        /// <returns>The greatest distance from the plane of a corner of the box the arc lies in, never below the arc's own.</returns>
        /// <remarks>
        /// It is read from the chord rather than the centre, so an arc 100 long bowing a trillionth, of radius 2.5E13,
        /// is read as exactly as a half circle, where its centre is known only to about 0.005. An arc of up to a half
        /// turn lies in the box between its chord and the line beside it through its middle, its sag of half the chord
        /// times the bulge away on the side it bows to; a longer one runs on past the ends of its chord, by up to its
        /// radius from the chord's middle either way. The distance from a plane changes evenly across the box, so its
        /// greatest is at a corner: an arc whose ends stand 0.0004 above the plane and which bows down across it to
        /// 0.0004 below stands 0.0004 off, not the 0.0008 of its ends and its dip added up.
        /// </remarks>
        internal static double StandOff(GeoPlane3 plane, GeoPoint3 start, GeoPoint3 end, double bulge, GeoVector3 unitNormal)
        {
            double fromStart = plane.SignedDistanceTo(start);
            double fromEnd = plane.SignedDistanceTo(end);

            GeoVector3 chord = start.GetVectorTo(end);
            double length = chord.Length;

            if (bulge == 0.0 || !(length > 0.0))
            {
                return Math.Max(Math.Abs(fromStart), Math.Abs(fromEnd));
            }

            GeoVector3 along = chord.Multiply(1.0 / length);
            GeoVector3 bow = (bulge > 0.0 ? along.CrossProduct(unitNormal) : unitNormal.CrossProduct(along));
            double bowLength = bow.Length;
            double magnitude = Math.Abs(bulge);
            double sag = magnitude * length * 0.5;

            // How far the far side of the box, the sag away on the bowing side, rises from the chord's own side.
            double rise = bowLength > 0.0 ? sag * bow.DotProduct(plane.Normal) / bowLength : sag;

            if (magnitude <= 1.0)
            {
                return Math.Max(
                    Math.Max(Math.Abs(fromStart), Math.Abs(fromEnd)),
                    Math.Max(Math.Abs(fromStart + rise), Math.Abs(fromEnd + rise)));
            }

            double radius = length * (1.0 + magnitude * magnitude) / (4.0 * magnitude);
            double middle = (fromStart + fromEnd) * 0.5;
            double reach = radius * along.DotProduct(plane.Normal);

            return Math.Max(
                Math.Max(Math.Abs(middle - reach), Math.Abs(middle + reach)),
                Math.Max(Math.Abs(middle - reach + rise), Math.Abs(middle + reach + rise)));
        }

        /// <summary>
        /// Gets how far an arc in space may stand off a plane: no less than any point of it does.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="arc">The arc.</param>
        /// <returns>The distance.</returns>
        /// <remarks>
        /// Up to a half turn it is read from the chord, as <see cref="StandOff(GeoPlane3, GeoPoint3, GeoPoint3, double, GeoVector3)"/>.
        /// Past a half turn the chord may be lost to rounding, its two ends one point to the last digit an arc a hair
        /// short of a whole turn, while the centre is well known: the distance is read round the circle, at the arc's
        /// ends and wherever between them the rim stands furthest up or down.
        /// </remarks>
        internal static double StandOff(GeoPlane3 plane, GeoArc3 arc)
        {
            if (Math.Abs(arc.SweptAngle) <= Math.PI)
            {
                return StandOff(plane, arc.StartPoint, arc.EndPoint, Math.Tan(arc.SweptAngle * 0.25), arc.Normal);
            }

            arc.GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            double centre = plane.SignedDistanceTo(arc.Center);
            double alongU = arc.Radius * uAxis.DotProduct(plane.Normal);
            double alongV = arc.Radius * vAxis.DotProduct(plane.Normal);

            double At(double angle) => Math.Abs(centre + alongU * Math.Cos(angle) + alongV * Math.Sin(angle));

            double from = arc.StartAngle;
            double sweep = arc.SweptAngle;
            double most = Math.Max(At(from), At(from + sweep));

            // The rim stands furthest from the plane at the angle of (alongU, alongV) and half a turn on.
            double crest = Math.Atan2(alongV, alongU);

            foreach (double angle in new[] { crest, crest + Math.PI })
            {
                double into = (angle - from) * Math.Sign(sweep);
                into -= 2.0 * Math.PI * Math.Floor(into / (2.0 * Math.PI));

                if (into <= Math.Abs(sweep))
                {
                    most = Math.Max(most, At(angle));
                }
            }

            return most;
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
            // two points a hair apart or as none at all. The discriminant is 4a times the square of the half
            // chord, so the band is a half chord of half the point tolerance, in drawing units: read against a
            // times a, it grew with the length of the segment, and a segment 1 000 long merged a chord of
            // nearly a millimetre into one point off the rim.
            double graze = tolerance.EqualPoint * tolerance.EqualPoint * a;

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
        /// The line is found from the arc's own centre rather than from the planes' distances to the world origin:
        /// two circles 30 000 out along each axis, in planes a thousandth of a degree apart, lost both their crossings
        /// to the rounding of that distance.
        /// </remarks>
        internal static List<GeoPoint3> PointsOnPlane(GeoArc3 arc, GeoPlane3 plane, Tolerance tolerance)
        {
            if (!Intersection3.TryGetMeetingLineFrom(arc.Center, arc.Normal, 2.0 * arc.Radius, plane, tolerance, out GeoVector3 offset, out GeoVector3 direction, out _))
            {
                return new List<GeoPoint3>();
            }

            return PointsOnLine(arc, arc.Center.Add(offset), direction, tolerance);
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

        /// <summary>
        /// Adds a point to a list unless a point already there stands in the same place, and where the one there is the point
        /// of a touch and this one a crossing, puts the crossing in its place.
        /// </summary>
        /// <param name="found">The points named so far.</param>
        /// <param name="touches">Whether each of them is the point of a touch.</param>
        /// <param name="point">The point.</param>
        /// <param name="touch">Whether the point is the point of a touch rather than a crossing.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A chain whose segment ends 0.0005 over a plane, its next segment crossing the plane a little further along, was given
        /// the touch at the corner in place of the crossing a hair away from it.
        /// </remarks>
        internal static void AddOnce(List<GeoPoint3> found, List<bool> touches, GeoPoint3 point, bool touch, Tolerance tolerance)
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].IsEqualTo(point, tolerance))
                {
                    if (touches[i] && !touch)
                    {
                        found[i] = point;
                        touches[i] = false;
                    }

                    return;
                }
            }

            found.Add(point);
            touches.Add(touch);
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
        /// the endless line carrying it and then keeps what lies between its ends. Where the two only touch, as
        /// <see cref="CollidesWith(GeoArc3, GeoLine3, Tolerance)"/> reads a touch, without a crossing, the point of the
        /// arc nearest the segment: the segment from (0, 50.0006, 1) to (0, 50.0006, -1), through the plane of the arc of
        /// radius 50 about the origin 0.0006 beside its rim, touches it at (0, 50, 0).
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoLine3 line, Tolerance tolerance) => GetIntersections(arc, line, tolerance, out _);

        /// <summary>
        /// Gets every point where an arc crosses a segment, within a tolerance, and whether the one given is the point of a
        /// touch rather than a crossing.
        /// </summary>
        internal static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoLine3 line, Tolerance tolerance, out bool touch)
        {
            var found = new List<GeoPoint3>();
            touch = false;

            foreach (GeoPoint3 candidate in PointsOnLine(arc, line.StartPoint, line.Direction, tolerance))
            {
                if (Containment3.IsPointOn(line, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            if (found.Count == 0 && MayTouch(arc, line.StartPoint, line.EndPoint, tolerance))
            {
                Gap3.Curve curve = Gap3.Curve.Of(arc);

                if (Touches(curve, line, tolerance))
                {
                    found.Add(curve.NearestToCallersSegment(line.StartPoint, line.EndPoint, tolerance));
                    touch = true;
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether a segment can come within the point tolerance of an arc's circle at all: whether the part of it
        /// within the tolerance of the circle's plane reaches into the shell the tolerance either side of the sphere the
        /// circle lies on. Most segments that miss an arc are settled so, without its gap.
        /// </summary>
        private static bool MayTouch(GeoArc3 arc, GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
        {
            // A hair over the tolerance, as Gap3 settles a segment plainly clear of a circle, and the rounding of the heights
            // and distances read here besides: a few parts in 1E16 of how far the centre, the radius and the segment reach.
            // An arc of radius 1E11 a few million from the origin was read clear of a segment it touched.
            GeoPoint3 centre = arc.Center;
            double reach = Math.Abs(centre.X) + Math.Abs(centre.Y) + Math.Abs(centre.Z) + arc.Radius
                + Math.Abs(from.X) + Math.Abs(from.Y) + Math.Abs(from.Z) + Math.Abs(to.X) + Math.Abs(to.Y) + Math.Abs(to.Z);
            double near = tolerance.EqualPoint * (1.0 + 1E-6) + 1E-9 + 1E-15 * reach;
            GeoVector3 normal = arc.Normal;
            double fromHeight = normal.DotProduct(arc.Center.GetVectorTo(from));
            double rise = normal.DotProduct(from.GetVectorTo(to));
            double low = 0.0;
            double high = 1.0;

            // The height changes evenly along the segment, so the part of it within the tolerance of the plane is one piece.
            if (rise != 0.0)
            {
                double one = (-near - fromHeight) / rise;
                double other = (near - fromHeight) / rise;

                low = Math.Max(low, Math.Min(one, other));
                high = Math.Min(high, Math.Max(one, other));
            }
            else if (Math.Abs(fromHeight) > near)
            {
                return false;
            }

            if (!(low <= high))
            {
                return false;
            }

            GeoVector3 along = from.GetVectorTo(to);
            GeoPoint3 start = from.Add(along.Multiply(low));
            GeoPoint3 end = from.Add(along.Multiply(high));
            double farthest = Math.Max(arc.Center.DistanceTo(start), arc.Center.DistanceTo(end));

            return !(Gap3.PointToSegment(arc.Center, start, end) - arc.Radius > near) && !(arc.Radius - farthest > near);
        }

        /// <summary>
        /// Determines whether an arc touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoArc3 arc, GeoLine3 line) => CollidesWith(arc, line, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they touch where the exact gap between them is no more than the point tolerance, in
        /// space and in whatever plane they lie: the arc of radius 50 about the origin from (50, 0, 0) a quarter turn
        /// counter-clockwise touches the segment from (50.0005, 0, 0) to (80, 0, 0), 0.0005 off its start, and not the one
        /// from (50.0015, 0, 0), and the one from (20, 40, 0.0003) to (40, 40, 0.0003), hovering 0.0003 over the arc where
        /// it crosses (30, 40), touches it. A segment no longer than the tolerance is read as its start unless it truly
        /// meets the arc.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoLine3 line, Tolerance tolerance)
            => Touches(Gap3.Curve.Of(arc), line, tolerance);

        /// <summary>
        /// Determines whether an arc, read for the gap, touches a segment a caller gave, within a tolerance.
        /// </summary>
        private static bool Touches(Gap3.Curve curve, GeoLine3 line, Tolerance tolerance)
        {
            return Gap3.BoxGap(line.StartPoint, line.EndPoint, curve.Box) <= Collision3.Reach(tolerance)
                && curve.GapToCallersSegment(line.StartPoint, line.EndPoint, tolerance) <= tolerance.EqualPoint;
        }

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
        /// <returns>
        /// At most two points, and only ones ahead of where the ray starts; where the two only touch without a crossing, the
        /// point of the arc nearest the ray, as for a segment.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoRay3 ray, Tolerance tolerance) => GetIntersections(arc, ray, tolerance, out _);

        /// <summary>
        /// Gets every point where an arc crosses a ray, within a tolerance, and whether the one given is the point of a touch
        /// rather than a crossing.
        /// </summary>
        internal static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoRay3 ray, Tolerance tolerance, out bool touch)
        {
            var found = new List<GeoPoint3>();
            touch = false;

            foreach (GeoPoint3 candidate in PointsOnLine(arc, ray.Origin, ray.Direction, tolerance))
            {
                if (Containment3.IsPointOn(ray, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            // The ray goes no nearer the circle past its point as far from its origin as the circle's far side.
            if (found.Count == 0
                && MayTouch(arc, ray.Origin, Parametrization3.GetPointAtDistance(ray, ray.Origin.DistanceTo(arc.Center) + arc.Radius), tolerance))
            {
                Gap3.Curve curve = Gap3.Curve.Of(arc);

                if (curve.GapToRay(ray, tolerance.EqualPoint) <= tolerance.EqualPoint)
                {
                    found.Add(curve.NearestToRay(ray));
                    touch = true;
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
        /// <remarks>
        /// They touch where the exact gap between them is no more than the point tolerance, as for a segment.
        /// </remarks>
        public static bool CollidesWith(GeoArc3 arc, GeoRay3 ray, Tolerance tolerance)
            => Gap3.Curve.Of(arc).GapToRay(ray, tolerance.EqualPoint) <= tolerance.EqualPoint;

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
