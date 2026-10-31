using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where the rim of an ellipse in space crosses another shape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the circle in space's questions, asked of the rim as <see cref="Arc3"/> asks them of a circle: the rim
    /// is what counts, not the disc, so a segment through the middle of the ellipse that never reaches the rim crosses
    /// nothing. None of it is sampled. Two readings do all the work, as for the circle.
    /// </para>
    /// <para>
    /// <b>In the plane.</b> Where the other shape shares the ellipse's plane, it is laid out in the ellipse's own frame
    /// and <see cref="Ellipse2"/> answers: a segment edge by edge, a circle, an arc or another ellipse as a curve. Whether
    /// the two share a plane is the circle's test: the rim stands off the other plane by no more than the planar
    /// tolerance, and the centre lies on it. A curve laid out keeps its radii, and may lean off the plane, so a crossing
    /// read there is kept only where it lies within the point tolerance of both rims in space, a touch only where the
    /// rims come within it of each other in space, and the rim is then searched in space for where the two pass within it
    /// of each other, as it is for curves not in one plane.
    /// </para>
    /// <para>
    /// <b>Not in the plane.</b> Two planes meet in a line, and every point the rim shares with anything lying in the other
    /// plane is on that line, where the rim is crossed in closed form; the points the other shape holds are kept. A
    /// segment or a ray not lying in the plane meets it at one point, kept when it is on the rim. Two ellipses are read
    /// from the one that comes first in a fixed order, whichever is asked first, and the rim touches another ellipse's, a
    /// circle or an arc besides where they pass within the point tolerance of each other off that line.
    /// </para>
    /// <para>
    /// The order is the circle's: along the line the two planes share, along a segment or a ray, edge by edge for a flat
    /// shape, face by face for a body, and in the plane along the line square to the one through the two centres, as two
    /// circles' crossings lie along their radical line; two ellipses in the plane are put in that order from the one they
    /// are read from, and the other way round where the other is asked first. A place two faces or two edges share is
    /// named once.
    /// </para>
    /// </remarks>
    public static partial class Ellipse3
    {
        /// <summary>
        /// The rounding of one operation on doubles, half the gap from one to the next double above it.
        /// </summary>
        private const double Epsilon = 1.1102230246251565E-16;

        #region Against a plane

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a plane, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPlane3 plane) => GetIntersections(ellipse, plane, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance: a rim standing off the plane by no more than its planar tolerance lies in it.</param>
        /// <returns>
        /// At most two points, in order along the line the two planes share. The ellipse 300 by 100 lying in the plan about
        /// the origin, its major axis along X, crosses the plane x = 150 at (150, -86.603, 0), then at (150, 86.603, 0). An
        /// ellipse lying in the plane crosses it nowhere and gives none, which
        /// <see cref="CollidesWith(GeoEllipse3, GeoPlane3, Tolerance)"/> reports instead.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, plane, tolerance))
            {
                return new GeoPoint3[0];
            }

            return PointsOnPlane(ellipse, plane, tolerance).ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPlane3 plane, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, plane, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a plane, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the plane; otherwise, false, an ellipse lying in it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPlane3 plane, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, plane, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a segment and a ray

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a segment, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoLine3 line) => GetIntersections(ellipse, line, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance: a point of the segment within its point tolerance of the rim, measured in space, is on it.</param>
        /// <returns>
        /// At most two points, in order along the segment, and only ones the segment reaches: where it passes the rim, read
        /// as it lies over the plane, and where it pierces the plane on the rim. The one from (-400, 50, 0) to (400, 50, 0)
        /// crosses the ellipse 300 by 100 lying in the plan about the origin at (-259.808, 50, 0), then at (259.808, 50, 0);
        /// the one from (-400, 50, -0.0009) to (400, 50, 0.0011), leaning a hair out of the plane, at (-259.808, 50, -0.00055)
        /// and (259.808, 50, 0.00075); the one from (300, 0, -10) to (300, 0, 10) at (300, 0, 0); one through the middle of
        /// the disc nowhere. A segment no longer than the point tolerance is read as its start where it does not truly cross
        /// the rim: it is given its start where that lies within the point tolerance of the rim, and nothing where it does not.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoLine3 line, Tolerance tolerance)
            => Stub3.ReadsAsStart(line, tolerance) ? CrossingsOfStub(ellipse, line, tolerance) : Crossings(ellipse, line, tolerance);

        /// <summary>
        /// Gets where a segment no longer than the tolerance, read as its start unless it truly crosses, meets the rim of an
        /// ellipse.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static GeoPoint3[] CrossingsOfStub(GeoEllipse3 ellipse, GeoLine3 line, Tolerance tolerance)
        {
            GeoPoint3[] asItIs = Crossings(ellipse, line, Stub3.AsItIs(line, tolerance));

            return Stub3.Meeting(
                line,
                asItIs,
                asItIs.Length == 0 && GetClosestPointOnBoundary(ellipse, line.StartPoint).DistanceTo(line.StartPoint) <= tolerance.EqualPoint);
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse crosses a segment, as the sides of shapes and the collision cross it, a
        /// segment no longer than the point tolerance read as one longer is.
        /// </summary>
        internal static GeoPoint3[] Crossings(GeoEllipse3 ellipse, GeoLine3 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnLine(ellipse, line.StartPoint, line.Direction, 1.0, tolerance))
            {
                if (Containment3.IsPointOn(line, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a segment, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoLine3 line, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segment crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a ray, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoRay3 ray) => GetIntersections(ellipse, ray, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// At most two points, in order along the ray, and only ones ahead of where it starts. The ray from the origin along
        /// X, in the plane of the ellipse 300 by 100 lying in the plan about the origin, crosses it at (300, 0, 0) only.
        /// </returns>
        /// <remarks>
        /// A ray is read as a segment as far as it can reach the rim: to the point past which it is further from the centre
        /// than the major radius.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoRay3 ray, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();
            GeoVector3 direction = ray.Direction;
            double speed = direction.LengthSquared;
            double reach = 0.0;

            if (speed > 0.0)
            {
                double nearest = ray.Origin.GetVectorTo(ellipse.Center).DotProduct(direction) / speed;
                reach = Math.Max(0.0, nearest + (ellipse.MajorRadius + tolerance.EqualPoint) / Math.Sqrt(speed));
            }

            foreach (GeoPoint3 candidate in PointsOnLine(ellipse, ray.Origin, direction, reach, tolerance))
            {
                if (Containment3.IsPointOn(ray, candidate, tolerance))
                {
                    found.Add(candidate);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a ray, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoRay3 ray, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, ray, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a ray, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the ray crosses or touches the rim; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, ray, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a curve

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses an arc, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoArc3 arc) => GetIntersections(ellipse, arc, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="tolerance">The tolerance: two crossings no further apart than twice its point tolerance are one.</param>
        /// <returns>
        /// The points of the arc on the rim, as the circle in space gives them against an arc, read from the arc: in order
        /// along the line the two planes share, or, in one plane, along the line square to the one from the arc's centre to
        /// the ellipse's. The half circle of radius 50 about (250, 0, 0), normal along Y, from (250, 0, 50) round through
        /// (300, 0, 0) to (250, 0, -50), meets the ellipse 300 by 100 lying in the plan about the origin at (300, 0, 0) only;
        /// the other half meets it nowhere.
        /// </returns>
        /// <remarks>
        /// An arc that passes within the point tolerance of the rim without crossing it touches it, at the middle of the
        /// shortest line between them, as a circle does: not in one plane, away from the line the planes share; in one
        /// plane, wherever the two come within the tolerance of each other in space. At a tolerance of 0.05, the arc of
        /// radius 117.4 lying within it of the plane of an ellipse 31.7 by 0.01, its rim 0.038 from the ellipse's, touches it.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoArc3 arc, Tolerance tolerance)
        {
            if (Arc3.IsCoplanar(arc, ellipse.GetPlane(), tolerance))
            {
                GeoPoint2[] onCircle = Ellipse2.GetIntersections(Flat(ellipse), LayOut(ellipse, arc.Center, arc.Radius), tolerance);
                List<GeoPoint3> kept = Kept(ellipse, onCircle, arc.IsPointOn, tolerance);

                if (!OnOneCircle(ellipse, arc.Center, arc.Radius, tolerance))
                {
                    NearPass3.Target target = NearPass3.Target.Of(arc, tolerance);

                    Passing(kept, ellipse, target, tolerance);
                    NearPass3.Add(kept, AsRim(ellipse, tolerance), target, tolerance);
                }

                return AlongRadicalLine(kept, arc.Normal, arc.Center, ellipse.Center, tolerance);
            }

            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(arc, ellipse, tolerance))
            {
                if (OnRim(ellipse, candidate, tolerance) && arc.IsPointOn(candidate, tolerance))
                {
                    AddApart(found, candidate, tolerance);
                }
            }

            NearPass3.Add(found, AsRim(ellipse, tolerance), NearPass3.Target.Of(arc, tolerance), tolerance);

            return AlongLine(found, arc.Center, Arc3.AlongShared(arc.Normal, ellipse.Normal, arc.Center, ellipse.Center, arc.Center.GetVectorTo(arc.StartPoint), tolerance));
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses an arc, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoArc3 arc, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses an arc, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc crosses or touches the rim; otherwise, false, an arc lying on the rim included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoArc3 arc, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoCircle3 circle) => GetIntersections(ellipse, circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="tolerance">The tolerance: rims that come within its point tolerance of each other touch, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// The points the two rims share: in order along the line the two planes share, or, in one plane, along the line
        /// square to the one from the ellipse's centre to the circle's, as two circles' lie along their radical line; with
        /// one centre on the other, in the order of the eccentric angle. The circle of radius 150 about the origin crosses the
        /// ellipse 300 by 100 lying in the plan about it at four points, the first at (118.585, 91.856, 0); the circle of
        /// radius 50 about (250, 0, 0) standing upright on the X axis meets it at (300, 0, 0) only. None when the two draw
        /// the same curve.
        /// </returns>
        /// <remarks>
        /// Not in one plane, rims that pass within the point tolerance of each other without crossing on the line the planes
        /// share touch, at the middle of the shortest line between them: the circle of radius 50 leaning 0.01 radians, the
        /// end of its diameter along X 0.0005 above (0, 100, 0) and its rim running square to the ellipse's there, touches
        /// the ellipse 300 by 100 lying in the plan about the origin at (0, 100, 0.00025). In one plane they touch wherever
        /// they come within the tolerance of each other in space, the circle read where it is and not laid flat: at a
        /// tolerance of 0.05, an ellipse 262.6 by 0.041 in the plan, within it of the plane of a circle of radius 12 846
        /// leaning 0.61 radians off the plan, its rim 0.0019 from the circle's, touches it.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoCircle3 circle, Tolerance tolerance)
        {
            if (IsCoplanar(ellipse, circle.GetPlane(), tolerance))
            {
                GeoPoint2[] points = Ellipse2.GetIntersections(Flat(ellipse), LayOut(ellipse, circle.Center, circle.Radius), tolerance);
                GeoArc3 laidOver = Arc3.AsArc(circle);
                List<GeoPoint3> kept = Kept(ellipse, points, laidOver.IsPointOn, tolerance);

                if (!OnOneCircle(ellipse, circle.Center, circle.Radius, tolerance))
                {
                    NearPass3.Target target = NearPass3.Target.Of(laidOver, tolerance);

                    Passing(kept, ellipse, target, tolerance);
                    NearPass3.Add(kept, AsRim(ellipse, tolerance), target, tolerance);
                }

                return AlongRadicalLine(kept, ellipse.Normal, ellipse.Center, circle.Center, tolerance);
            }

            GeoArc3 rim = Arc3.AsArc(circle);
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, circle.GetPlane(), tolerance))
            {
                if (OnRim(ellipse, candidate, tolerance) && rim.IsPointOn(candidate, tolerance))
                {
                    AddApart(found, candidate, tolerance);
                }
            }

            NearPass3.Add(found, AsRim(ellipse, tolerance), NearPass3.Target.Of(rim, tolerance), tolerance);

            return AlongLine(found, ellipse.Center, Arc3.AlongShared(ellipse.Normal, circle.Normal, ellipse.Center, circle.Center, ellipse.MajorAxis, tolerance));
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a circle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoCircle3 circle, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rims cross or touch; otherwise, false, for one lying on the other included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoCircle3 circle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rims of two ellipses in space cross, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEllipse3 other) => GetIntersections(ellipse, other, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rims of two ellipses in space cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">
        /// The first ellipse. In one plane the pair is read from the one lying within the planar tolerance of the other's
        /// plane, or, where both do, from the one that comes first in a fixed order, so that it gives the same points to the
        /// last digit whichever is asked first; they are put in order from that one, and in the opposite order where the
        /// other is asked first.
        /// </param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="tolerance">The tolerance: rims that come within its point tolerance of each other touch, and two crossings no further apart than twice it are one.</param>
        /// <returns>
        /// The points the two rims share, in the order the circle's crossings of a circle come in. The ellipse 300 by 100
        /// lying in the plan about the origin and the same ellipse turned a quarter cross at (94.868, 94.868, 0) first, then
        /// at the three points mirrored from it. None when the two are the same ellipse: in one plane, their centres and radii
        /// within the point tolerance and their axes along one line, or both round within it, whatever their axes, as an
        /// ellipse 100.0004 by 99.9998 and one 100.0003 by 99.9999 turned a quarter about the same centre; or, one of them a
        /// circle, its two radii one to within 1E-12 of the larger, the centres and both radii of the other within the
        /// tolerance of the circle's, whichever way its axis points, as the ellipse 100.0007 by 99.9995 and the circle of
        /// radius 100 about its centre read as an ellipse (<see cref="GeoEllipse3.FromCircle(GeoCircle3)"/>), which give
        /// none asked as a circle too.
        /// </returns>
        /// <remarks>
        /// Not in one plane, the two give the same places to the last digit whichever is asked first, and rims that pass
        /// within the point tolerance of each other without crossing touch, at the middle of the shortest line between them:
        /// the ellipse 300 by 100 lying in the plan about the origin and an ellipse 200 by 100 leaning 0.01 radians, the end
        /// of its major axis 0.0005 above (0, 100, 0) and its rim running square to the first there, touch at
        /// (0, 100, 0.00025); 0.0011 above, they do not. In one plane the rims touch wherever they come within the
        /// tolerance of each other in space, and the points come in the opposite order asked the other way: an ellipse
        /// 1.97 by 0.0023 lying within the planar tolerance of the plane of one 15 338 by 15 274, which leans 0.02 radians
        /// off its own, their rims 0.00016 apart, touch near (-5595.742, -6793.740, -12510.089) whichever is asked first.
        /// Either way, where the ellipse not read is narrow, its rim no more than 32 tolerances from its major axis beside a
        /// place found, it is searched too, across its inside from that place and beyond the stretch about it that runs
        /// within the tolerance, so that a touch on its other side, which the one read passes between two of its readings, is
        /// found as well: a rim 30 080 by 18 333 passing 0.000013 from either side of a needle 2.78 by 0.0028, leaning 0.56
        /// radians off its plane, touches it at both sides, 0.0026 apart, and a rim 36.66 by 30.95 passing an ellipse 21.88
        /// by 0.908 within 0.0020 either side of the end of its major axis, the two places 0.056 apart and the gap rising 4 %
        /// above the tolerance between at a tolerance of 0.01, touches it at both. Wider than that beside the place, the
        /// ellipse not read is not searched, and two touches the one read passes between two readings stay one; so do two
        /// touches the reading of a narrow ellipse read first makes one: a needle 0.754 by 0.108 passing a rim 31 536 by
        /// 14 839 at two places 0.0357 apart, the gap rising 15 % above the tolerance between at a tolerance of 0.01, is given
        /// one place. Of 7 500 rims drawn across the end of an ellipse whose b² / a is 2 to 24 tolerances, at tolerances of
        /// 0.001, 0.01 and 0.05, each passing it within the tolerance at two places with the gap rising up to 15 % above it
        /// between, 27 are given one place: 22 read from the ellipse, and 5 read from the rim, the ellipse 14.9 to 21.9
        /// tolerances wide beside the place and the gap rising under 0.7 % above the tolerance between.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            if (InOnePlane(ref ellipse, ref other, tolerance, out bool turned))
            {
                GeoEllipse2 flat = Flat(ellipse);
                GeoEllipse2 laidOut = LayOut(ellipse, other);

                // The same ellipse crosses nowhere, as documented, though its rim runs within the tolerance of the other's.
                if (SameCurve(flat, laidOut, tolerance))
                {
                    return new GeoPoint3[0];
                }

                GeoPoint2[] points = Ellipse2.GetIntersections(flat, laidOut, tolerance);
                List<GeoPoint3> kept = Kept(ellipse, points, (point, within) => OnRim(other, point, within), tolerance);
                NearPass3.Target target = AsTarget(other, tolerance);

                Passing(kept, ellipse, target, tolerance);
                NearPass3.Add(kept, AsRim(ellipse, tolerance), target, tolerance);
                AlongOther(kept, ellipse, other, tolerance);

                return InOrderOf(kept, ellipse, other, turned, tolerance);
            }

            // Read from the one that comes first, so that the places are the same whichever is asked first, and put in order
            // along the line the planes share as this one's normal crossed with the other's runs.
            bool swapped = ComesFirst(other, ellipse);
            GeoEllipse3 first = swapped ? other : ellipse;
            GeoEllipse3 second = swapped ? ellipse : other;
            List<GeoPoint3> found = InSpace(first, second, tolerance);

            GeoVector3 direction = Arc3.AlongShared(first.Normal, second.Normal, first.Center, second.Center, first.MajorAxis, tolerance);

            return AlongLine(found, first.Center, direction.Multiply(swapped ? -1.0 : 1.0));
        }

        /// <summary>
        /// Tries to find where the rims of two ellipses in space cross, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEllipse3 other, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rims of two ellipses in space cross, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The first ellipse.</param>
        /// <param name="other">The other ellipse.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rims cross or touch; otherwise, false, for the same ellipse included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEllipse3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, other, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a flat region

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a triangle, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoTriangle3 triangle) => GetIntersections(ellipse, triangle, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the triangle, the points of the triangle it goes through, in order along the line the two
        /// planes share; where the triangle lies in the ellipse's plane, the points where the rim cuts one of its three edges,
        /// edge by edge. The triangle (150, 0, -10), (150, 200, -10), (150, 0, 10), standing upright on the line x = 150, is
        /// pierced by the ellipse 300 by 100 lying in the plan about the origin at (150, 86.603, 0) only. A triangle with no
        /// area, by <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/>, is crossed where its sides are.
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoTriangle3 triangle, Tolerance tolerance)
        {
            // A triangle with no area is the segment it has collapsed to, crossed where its sides are, as an arc reads it.
            if (triangle.IsDegenerate(tolerance))
            {
                var onSides = new List<GeoPoint3>();

                AddCrossingsOfEdges(onSides, ellipse, Intersection3.SidesOfThin(triangle), tolerance);

                return onSides.ToArray();
            }

            GeoPlane3 plane = triangle.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(triangle, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) }, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a triangle, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the triangle; otherwise, false, an ellipse lying wholly within it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygon3 polygon) => GetIntersections(ellipse, polygon, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the polygon, the points of the region it goes through; where the polygon lies in the
        /// ellipse's plane, the points where the rim cuts the outline, edge by edge. The square 800 by 800 about the origin in
        /// the plan is cut by the ellipse 300 by 100 lying about the same centre at none of its edges; the rectangle 200 by
        /// 300, its first edge along y = -150, at four points, (100, -94.281, 0) on its second edge first.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoPlane3 plane = polygon.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(polygon, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, polygon.GetEdges(), tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the polygon; otherwise, false, an ellipse lying wholly within it included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoFace3 face) => GetIntersections(ellipse, face, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim pierces the face, the points of its material it goes through, so a rim passing down a hole reaches
        /// nothing; where the face lies in the ellipse's plane, the points where the rim cuts the outline, then the rim of each
        /// hole.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPlane3 plane = face.GetPlane();

            if (!IsCoplanar(ellipse, plane, tolerance))
            {
                return PiercingPoints(ellipse, plane, point => Containment3.Contains(face, point, tolerance), tolerance).ToArray();
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfEdges(found, ellipse, face.Boundary.GetEdges(), tolerance);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                AddCrossingsOfEdges(found, ellipse, hole.GetEdges(), tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses a face, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the material of the face; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, face, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against a body or a box

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoSolid3 solid) => GetIntersections(ellipse, solid, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where the rim goes in and where it comes out, face by face, the walls of any opening counted as surface and a
        /// place two faces share named once. The ellipse 300 by 100 lying in the plan about the centre of a box 200 by 300 by
        /// 200 leaves it through the two faces square to its major axis, at (±100, ±94.281, 0). A rim lying wholly inside the
        /// material crosses nothing and gives none, which <see cref="CollidesWith(GeoEllipse3, GeoSolid3, Tolerance)"/>
        /// reports instead.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            var found = new List<GeoPoint3>();

            AddCrossingsOfBody(found, ellipse, solid, solid, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches the surface; otherwise, false, a rim wholly inside included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an oriented box, using the default
        /// tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoObb3 box) => GetIntersections(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The points where the rim meets one of the six faces, face by face, each corner or edge counted once: the ellipse
        /// 300 by 100 lying in the plan about the centre of a box 200 by 300 by 200 meets it at (±100, ±94.281, 0).
        /// </returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoObb3 box, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPolygon3 face in box.GetFaces())
            {
                foreach (GeoPoint3 candidate in GetIntersections(ellipse, face, tolerance))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an oriented box, using the default
        /// tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches a face of the box; otherwise, false, a rim wholly inside included.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an axis-aligned box, using the
        /// default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoAabb3 box) => GetIntersections(ellipse, box, Tolerance.Global);

        /// <summary>
        /// Gets every point where the rim of an ellipse in space crosses the surface of an axis-aligned box, within a
        /// tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box; an empty one holds nothing and is crossed nowhere.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The points the oriented box with the world's axes gives: the box 200 by 300 by 200 about the origin meets the ellipse 300 by 100 lying in the plan about it at (±100, ±94.281, 0).</returns>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(ellipse, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an axis-aligned box, using the default
        /// tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of an ellipse in space crosses the surface of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rim crosses or touches a face of the box; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ellipse, box, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region Against an edge and a curved chain

        /// <summary>
        /// Gets every point where an edge crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEdge3 edge) => GetIntersections(ellipse, edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where an edge crosses the rim of an ellipse in space, within a tolerance: as a segment or as an
        /// arc, whichever the edge is.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(ellipse, edge.ToArc(), tolerance)
                : GetIntersections(ellipse, edge.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where an edge crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEdge3 edge, out GeoPoint3[] intersections)
            => TryIntersectWith(ellipse, edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where an edge crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="edge">The edge.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public static bool TryIntersectWith(GeoEllipse3 ellipse, GeoEdge3 edge, out GeoPoint3[] intersections, Tolerance tolerance)
            => edge.IsArc
                ? TryIntersectWith(ellipse, edge.ToArc(), out intersections, tolerance)
                : TryIntersectWith(ellipse, edge.ToLine(), out intersections, tolerance);

        /// <summary>
        /// Gets every point where a curved chain crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolylineArc3 chain) => GetIntersections(ellipse, chain, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved chain crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="chain">The curved chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>What each edge gives, edge by edge in the chain's order, a corner two edges share named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved chain is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return CrossingsOfEdges(ellipse, chain.GetEdges(), tolerance);
        }

        /// <summary>
        /// Gets every point where a curved loop crosses the rim of an ellipse in space, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygonArc3 loop) => GetIntersections(ellipse, loop, Tolerance.Global);

        /// <summary>
        /// Gets every point where a curved loop crosses the rim of an ellipse in space, within a tolerance.
        /// </summary>
        /// <param name="ellipse">The ellipse.</param>
        /// <param name="loop">The curved loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>What each edge gives, edge by edge in the loop's order, a corner two edges share named once.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the curved loop is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return CrossingsOfEdges(ellipse, loop.GetEdges(), tolerance);
        }

        #endregion

        #region Crossing helpers

        /// <summary>
        /// Determines whether two ellipses are read in one plane, the first within the planar tolerance of the second's, and
        /// puts first the one that lies so where only the second does, or, where both do, the one that comes first by
        /// centre, radii, normal and axis, so that the pair is read the same, to the last digit, whichever is asked first.
        /// </summary>
        /// <remarks>
        /// The rim of the larger one may stand off the smaller one's plane by more than the tolerance where the smaller one
        /// lies within it of the larger one's: an ellipse 192 by 32 in the plan, and one 496 by 46 tilted 3E-6 radians about
        /// a centre 0.0002 under it, their rims crossing within 0.0003 of each other, collided asked the one way, at four
        /// places, and not at all asked the other.
        /// </remarks>
        internal static bool InOnePlane(ref GeoEllipse3 ellipse, ref GeoEllipse3 other, Tolerance tolerance)
            => InOnePlane(ref ellipse, ref other, tolerance, out _);

        /// <summary>
        /// Determines whether two ellipses are read in one plane, as <see cref="InOnePlane(ref GeoEllipse3, ref GeoEllipse3, Tolerance)"/>
        /// does, and whether they were put the other way round, so that the points read are to be given in the opposite order.
        /// </summary>
        private static bool InOnePlane(ref GeoEllipse3 ellipse, ref GeoEllipse3 other, Tolerance tolerance, out bool turned)
        {
            bool first = IsCoplanar(ellipse, other.GetPlane(), tolerance);
            bool second = IsCoplanar(other, ellipse.GetPlane(), tolerance);

            turned = false;

            if (!first && !second)
            {
                return false;
            }

            if (first && (!second || !ComesFirst(other, ellipse)))
            {
                return true;
            }

            turned = true;

            GeoEllipse3 swap = ellipse;
            ellipse = other;
            other = swap;
            return true;
        }

        /// <summary>
        /// Puts the points two ellipses of one plane share in order as the one they are read from gives them: along the
        /// radical line from its centre, or, the centres one, in the order of its eccentric angle, ties in the order they
        /// came in; the other way round where the other was asked first, so that the two orders are each other reversed.
        /// </summary>
        /// <remarks>
        /// Put in order from the one read, whichever was asked first, the two crossings of a thin ellipse 2.11 by 0.0003
        /// lying within the planar tolerance of the plane of one 69 635 by 50 234, which does not lie within it of the thin
        /// one's, came in the same order asked either way.
        /// </remarks>
        private static GeoPoint3[] InOrderOf(List<GeoPoint3> points, GeoEllipse3 ellipse, GeoEllipse3 other, bool turned, Tolerance tolerance)
        {
            GeoPoint3[] sorted = ellipse.Center.GetVectorTo(other.Center).Length > tolerance.EqualPoint
                ? AlongRadicalLine(points, ellipse.Normal, ellipse.Center, other.Center, tolerance)
                : ByEccentricAngle(points, ellipse);

            if (turned)
            {
                Array.Reverse(sorted);
            }

            return sorted;
        }

        /// <summary>
        /// Puts points of the rim in the order of the eccentric angle, from nought up to a whole turn, ties in the order they
        /// came in.
        /// </summary>
        private static GeoPoint3[] ByEccentricAngle(List<GeoPoint3> points, GeoEllipse3 ellipse)
        {
            var keys = new double[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                Frame(ellipse, points[i], out double along, out double across, out _);

                double angle = Math.Atan2(across / ellipse.MinorRadius, along / ellipse.MajorRadius);

                keys[i] = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            }

            var order = new int[points.Count];

            for (int i = 0; i < order.Length; i++)
            {
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
        /// Determines whether an ellipse comes before another in a fixed order of the two: by centre, coordinate by
        /// coordinate, then by radii, normal and axis.
        /// </summary>
        /// <remarks>
        /// Two ellipses not in one plane are read from the one that comes first, whichever is asked first: read from the one
        /// asked first, where its rim crosses the other's plane, the places depended on the order in 134 of 4 000 pairs drawn
        /// to pass within 0.0012 of each other, every one of them tilted more than 0.001 radians.
        /// </remarks>
        private static bool ComesFirst(GeoEllipse3 ellipse, GeoEllipse3 other)
        {
            double[] one = Key(ellipse);
            double[] two = Key(other);

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
        /// Gets the numbers an ellipse is put in order by.
        /// </summary>
        private static double[] Key(GeoEllipse3 ellipse)
        {
            GeoPoint3 center = ellipse.Center;
            GeoVector3 normal = ellipse.Normal;
            GeoVector3 axis = ellipse.MajorAxis;

            return new[]
            {
                center.X, center.Y, center.Z, ellipse.MajorRadius, ellipse.MinorRadius,
                normal.X, normal.Y, normal.Z, axis.X, axis.Y, axis.Z,
            };
        }

        /// <summary>
        /// Gets the places two rims not in one plane share, read from the first: where its rim crosses the other's plane on
        /// the other's rim, and where the two pass within the point tolerance of each other without that.
        /// </summary>
        private static List<GeoPoint3> InSpace(GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, other.GetPlane(), tolerance))
            {
                if (OnRim(ellipse, candidate, tolerance) && OnRim(other, candidate, tolerance))
                {
                    AddApart(found, candidate, tolerance);
                }
            }

            NearPass3.Add(found, AsRim(ellipse, tolerance), AsTarget(other, tolerance), tolerance);
            AlongOther(found, ellipse, other, tolerance);

            return found;
        }

        /// <summary>
        /// Adds a crossing found where one rim crosses the other's plane, unless one already found lies no further than twice
        /// the point tolerance from it: a thin rim 208 by 0.21 crossing the plane of one 148 by 0.19 twice, 0.0013 apart,
        /// beside its rim, was given both.
        /// </summary>
        /// <remarks>
        /// The crossing is kept only where it lies on both rims: where a thin rim only grazes the plane, the crossing is read
        /// at the middle of the chord, which on a rim 182 by 0.004 lay 0.0084 off it, and the search along the rim
        /// (<see cref="NearPass3"/>) gives the place instead.
        /// </remarks>
        private static void AddApart(List<GeoPoint3> found, GeoPoint3 candidate, Tolerance tolerance)
        {
            foreach (GeoPoint3 point in found)
            {
                if (point.DistanceTo(candidate) <= 2.0 * tolerance.EqualPoint)
                {
                    return;
                }
            }

            found.Add(candidate);
        }

        /// <summary>
        /// How narrow the other ellipse may be across its inside, in point tolerances from its rim to its major axis, and still
        /// hide a touch beside a place named: at four, an ellipse 21.25 by 0.93, b² / a 4.07 tolerances, missed one, and at
        /// eight, one 23.46 by 1.379, 9.29 tolerances wide beside the touch; of 7 500 rims drawn across the end of an ellipse
        /// whose b² / a is 2 to 24 tolerances, each passing it at two places, 5 read from the rim are given one at 32.
        /// </summary>
        private const double Narrow = 32.0;

        /// <summary>
        /// Adds the places a search along the other ellipse, against the one read, finds across its inside from the places
        /// named, where it is narrow enough there to hide one, and that no place named stands for.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The rim is read at points no further apart along it than twice the tolerance, so where the gap between two places
        /// the other passes within the tolerance rises above it over a stretch shorter than that, both can lie between two
        /// readings within it and are one place: a rim 30 080 by 18 333 passing 0.000013 from either side of a needle 2.78 by
        /// 0.0028, which leans 0.56 radians off its plane, was named at the one side only, the other 0.0026 along it. The
        /// other comes back within the tolerance so soon only where its two sides lie close, its rim a few tolerances from its
        /// major axis along the normal: b / a times its speed, nowhere less than b² / a. Where that is no more than 32
        /// tolerances, the other side is at the opposite angle, mirrored across the major axis, and the other is searched
        /// along a stretch of it 16 tolerances long about that angle; along the needle the two sides lie a radian apart, and
        /// both are named. Searched only within four tolerances, an ellipse 21.25 by 0.93, b² / a 4.07 tolerances at a
        /// tolerance of 0.01, passing a rim 5.26 by 3.59 within 0.0036 and 0.0072 at two places 0.058 apart, the gap rising
        /// 4.4 % above the tolerance between, was given one place; searched only within eight, an ellipse 23.46 by 1.379,
        /// its rim 9.29 tolerances from its major axis beside the touch, passing a rim 10 336.8 by 7 088.3 within 0.0030 and
        /// 0.00036 at two places 0.0755 apart, the gap rising 4 % above the tolerance between, was given one too.
        /// </para>
        /// <para>
        /// Near the end of the major axis the stretch reaches back over the place named, and the run of that place, the
        /// stretch about its angle the one read holds, read at points as <see cref="Along"/> reads them, is left out of it:
        /// begun inside that run, the search read it and a touch beyond it as one run across the rise between, and took the
        /// touch for the place named. A rim 36.66 by 30.95 passing an ellipse 21.88 by 0.908, b² / a 3.77 tolerances at a
        /// tolerance of 0.01, within 0.0020 at two places 0.056 apart either side of its end, the gap rising 4 % above the
        /// tolerance between, was given one place.
        /// </para>
        /// <para>
        /// A place found there is one already named where it stands within the tolerance of one, or where either curve runs
        /// within the tolerance of the other all the way between the two, each read at points as <see cref="Along"/> reads
        /// them: the end of a rim 100 000 by 1 lies within the tolerance of a copy 0.001 larger all round it, while from the
        /// larger the gap rises a hair above it round the end, and each end is one place. Where the other is nowhere so
        /// narrow, or nothing was named, it is not searched, and the answer is the rim's alone, as before.
        /// </para>
        /// <para>
        /// Only the two sides of the ellipse not read are parted so. Two touches the reading of a narrow ellipse itself makes
        /// one stay one: read first, a needle 0.754 by 0.108 passing a rim 31 536 by 14 839 at two places 0.0357 apart, the gap
        /// rising 15 % above the tolerance between at a tolerance of 0.01, is given one place, its crossing of the rim's plane,
        /// which stands within the tolerance of both.
        /// </para>
        /// </remarks>
        private static void AlongOther(List<GeoPoint3> found, GeoEllipse3 ellipse, GeoEllipse3 other, Tolerance tolerance)
        {
            double a = other.MajorRadius;
            double b = other.MinorRadius;
            double narrow = Narrow * tolerance.EqualPoint;

            if (!(b * b / a <= narrow))
            {
                return;
            }

            NearPass3.Target read = AsTarget(ellipse, tolerance);
            NearPass3.Target back = null;
            GeoEllipse2 flat = Flat(other);
            int named = found.Count;

            for (int i = 0; i < named; i++)
            {
                double angle = AngleOf(other, flat, found[i]);
                double sin = Math.Sin(angle);
                double cos = Math.Cos(angle);
                double speed = Math.Sqrt(a * a * sin * sin + b * b * cos * cos);

                if (!(b / a * speed <= narrow))
                {
                    continue;
                }

                double half = Math.Min(Math.PI, 8.0 * tolerance.EqualPoint / speed);
                double from = -angle - half;
                double to = from + 2.0 * half;

                // The run of the place itself, the stretch about its angle the one read holds, is left out of the stretch
                // searched, so that a search begun inside it does not read a touch beyond it as the same place.
                double at = -angle + Math.IEEERemainder(2.0 * angle, 2.0 * Math.PI);
                double low = at >= from ? Leave(other, at, from, read, tolerance) : at;
                double high = at <= to ? Leave(other, at, to, read, tolerance) : at;
                var more = new List<GeoPoint3>();

                if (low > from)
                {
                    double end = Math.Min(low, to);

                    NearPass3.Add(more, Window(other, from, end == to ? 2.0 * half : end - from, tolerance), read, tolerance);
                }

                if (high < to)
                {
                    double start = Math.Max(high, from);

                    NearPass3.Add(more, Window(other, start, start == from ? 2.0 * half : to - start, tolerance), read, tolerance);
                }

                foreach (GeoPoint3 place in more)
                {
                    if (found.Exists(named => named.IsEqualTo(place, tolerance)) || Along(other, flat, found, place, read, tolerance))
                    {
                        continue;
                    }

                    back = back ?? AsTarget(other, tolerance);

                    if (!Along(ellipse, Flat(ellipse), found, place, back, tolerance))
                    {
                        found.Add(place);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the eccentric angle of the point of an ellipse's rim nearest a point.
        /// </summary>
        private static double AngleOf(GeoEllipse3 ellipse, GeoEllipse2 flat, GeoPoint3 point)
        {
            Frame(ellipse, point, out double along, out double across, out _);

            GeoPoint2 nearest = Ellipse2.GetClosestPointOnBoundary(flat, new GeoPoint2(along, across));

            return Math.Atan2(nearest.Y / ellipse.MinorRadius, nearest.X / ellipse.MajorRadius);
        }

        /// <summary>
        /// Determines whether an ellipse runs within the point tolerance of a curve all the way from its point nearest one of
        /// the places named to its point nearest another place, the short way round: read at points evenly between, at least
        /// 16 and at most 256 of them, no further apart along it than the tolerance where 256 reach, as the search reads the
        /// curve.
        /// </summary>
        private static bool Along(GeoEllipse3 ellipse, GeoEllipse2 flat, List<GeoPoint3> named, GeoPoint3 place, NearPass3.Target curve, Tolerance tolerance)
        {
            double to = AngleOf(ellipse, flat, place);

            foreach (GeoPoint3 point in named)
            {
                double from = AngleOf(ellipse, flat, point);
                double turned = Math.IEEERemainder(to - from, 2.0 * Math.PI);
                int steps = (int)Math.Min(256.0, Math.Max(16.0, Math.Ceiling(Math.Abs(turned) * ellipse.MajorRadius / tolerance.EqualPoint)));
                bool all = true;

                for (int k = 1; k < steps && all; k++)
                {
                    all = curve.Holds(GetPointAtAngle(ellipse, from + turned * k / steps));
                }

                if (all)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the first point of an ellipse's rim, walking from an angle towards another, that a curve does not hold within
        /// the point tolerance, read as <see cref="Along"/> reads it; the angle walked to where the curve holds every point.
        /// </summary>
        private static double Leave(GeoEllipse3 ellipse, double from, double to, NearPass3.Target curve, Tolerance tolerance)
        {
            double turned = to - from;
            int steps = (int)Math.Min(256.0, Math.Max(16.0, Math.Ceiling(Math.Abs(turned) * ellipse.MajorRadius / tolerance.EqualPoint)));

            for (int k = 1; k < steps; k++)
            {
                double angle = from + turned * k / steps;

                if (!curve.Holds(GetPointAtAngle(ellipse, angle)))
                {
                    return angle;
                }
            }

            return to;
        }

        /// <summary>
        /// Reads a stretch of the rim of an ellipse, from an angle through a sweep, to be searched along as an arc of it.
        /// </summary>
        private static NearPass3.Rim Window(GeoEllipse3 ellipse, double from, double sweep, Tolerance tolerance)
            => new NearPass3.Rim(
                ellipse.Center,
                ellipse.MajorAxis,
                ellipse.MinorAxis,
                ellipse.MajorRadius,
                ellipse.MinorRadius,
                from,
                sweep,
                angle => GetPointAtAngle(ellipse, angle),
                point => OnRim(ellipse, point, tolerance));

        /// <summary>
        /// Reads the rim of an ellipse to be searched along for where it passes another curve (<see cref="NearPass3"/>).
        /// </summary>
        private static NearPass3.Rim AsRim(GeoEllipse3 ellipse, Tolerance tolerance)
            => NearPass3.Rim.Of(ellipse, point => OnRim(ellipse, point, tolerance));

        /// <summary>
        /// Reads the rim of an ellipse as the curve a rim searched along passes (<see cref="NearPass3"/>).
        /// </summary>
        private static NearPass3.Target AsTarget(GeoEllipse3 ellipse, Tolerance tolerance)
            => new NearPass3.Target(
                ellipse.GetPlane(),
                ellipse.Normal,
                ellipse.Center,
                ellipse.MajorRadius,
                point => GapAtLeast(ellipse, point),
                point => RimGap(ellipse, point),
                point => GetClosestPointOnBoundary(ellipse, point),
                point => OnRim(ellipse, point, tolerance),
                ellipse.MajorAxis,
                ellipse.MinorAxis,
                ellipse.MajorRadius,
                ellipse.MinorRadius,
                point => Ellipse2.GetClosestPointOnBoundary(Flat(ellipse), point),
                true);

        /// <summary>
        /// Gets a bound the distance from a point to the rim cannot be under: the root of its height above the plane squared
        /// and, squared, how far its foot lies from the rim by <see cref="Ellipse2.Below(GeoEllipse2, double, double)"/>.
        /// </summary>
        private static double GapAtLeast(GeoEllipse3 ellipse, GeoPoint3 point)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            double below = Ellipse2.Below(Flat(ellipse), along, across);

            return Math.Sqrt(height * height + below * below);
        }

        /// <summary>
        /// Puts places in order along a direction, ties in the order they came in.
        /// </summary>
        private static GeoPoint3[] AlongLine(List<GeoPoint3> points, GeoPoint3 from, GeoVector3 direction)
        {
            var keys = new double[points.Count];
            var order = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                keys[i] = from.GetVectorTo(points[i]).DotProduct(direction);
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
        /// Determines whether a plane is the very plane the ellipse lies in.
        /// </summary>
        /// <remarks>
        /// As the circle's test (<see cref="Arc3"/>): the rim stands off a plane through its centre by no more than the
        /// planar tolerance, and the centre lies on the plane. The rim stands off by up to the root of (a n·U)² + (b n·V)²,
        /// where n is the plane's normal: for a circle, its radius times the sine of the angle between the planes.
        /// </remarks>
        internal static bool IsCoplanar(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            GeoVector3 normal = plane.Normal;
            double major = ellipse.MajorRadius * ellipse.MajorAxis.DotProduct(normal);
            double minor = ellipse.MinorRadius * ellipse.MinorAxis.DotProduct(normal);

            return Math.Sqrt(major * major + minor * minor) <= tolerance.EqualPlanar
                && Containment3.IsPointOn(plane, ellipse.Center, tolerance);
        }

        /// <summary>
        /// Determines whether a point lies within the point tolerance of the rim, measured in space, as an arc measures it.
        /// </summary>
        private static bool OnRim(GeoEllipse3 ellipse, GeoPoint3 point, Tolerance tolerance)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            if (!(Math.Abs(height) <= tolerance.EqualPoint))
            {
                return false;
            }

            GeoEllipse2 flat = Flat(ellipse);

            if (Math.Abs(Ellipse2.Below(flat, along, across)) > Ellipse2.Slack(flat, along, across, tolerance))
            {
                return false;
            }

            double gap = Ellipse2.Gap(flat, along, across);

            return Math.Sqrt(height * height + gap * gap) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets the point of the rim where the eccentric angle starts, the end of the major axis: the circle's start.
        /// </summary>
        private static GeoPoint3 StartOf(GeoEllipse3 ellipse) => Lift(ellipse, new GeoPoint2(ellipse.MajorRadius, 0.0));

        /// <summary>
        /// Gets the points of the rim on the line where its plane meets another plane, in order along the line.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The workhorse of every pair not in one plane, as it is for the circle. The line is found from the ellipse's
        /// centre and read in the ellipse's own frame as the line u n_u + v n_v = p, n its unit normal there. With
        /// h = √(a² n_u² + b² n_v²), how far the rim reaches along n, the line cuts a chord whose half is
        /// (a b / h²) √(h² - p²) in drawing units; for a circle, √(r² - p²).
        /// </para>
        /// <para>
        /// A line grazing the rim is judged as the circle judges one, by four times the square of that half chord,
        /// D = 4 (a b)² (h² - p²) / h⁴, negative for a line passing outside: below minus the square of the point
        /// tolerance it misses; within that square either way it touches, at the middle of the chord; above it, it cuts at
        /// two points. For a circle D is the circle's own discriminant and the bounds are the circle's. Judged instead by
        /// how deep the chord cuts, as a segment lying in the plane is, a chord 1.66 long cutting 0.0008 into an ellipse of
        /// radius 431 was one point at its middle, 0.8 from either true crossing, and a circle standing on one of them was
        /// found to cross the ellipse nowhere. Judged in the frame scaled to the unit circle, the bound is stretched by a / b.
        /// </para>
        /// </remarks>
        private static List<GeoPoint3> PointsOnPlane(GeoEllipse3 ellipse, GeoPlane3 plane, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>(2);
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;

            if (!Intersection3.TryGetMeetingLineFrom(ellipse.Center, ellipse.Normal, 2.0 * a, plane, tolerance, out GeoVector3 offset, out GeoVector3 direction, out double rounding))
            {
                return found;
            }

            GeoVector3 major = ellipse.MajorAxis;
            GeoVector3 minor = ellipse.MinorAxis;
            double u0 = offset.DotProduct(major);
            double v0 = offset.DotProduct(minor);
            double du = direction.DotProduct(major);
            double dv = direction.DotProduct(minor);

            // The unit normal of the line in the plane, and how far the line and the rim reach along it.
            double speed = Math.Sqrt(du * du + dv * dv);
            double nu = -dv / speed;
            double nv = du / speed;
            double p = Math.Abs(u0 * nu + v0 * nv);
            double h = Math.Sqrt(a * a * nu * nu + b * b * nv * nv);
            double ab = a * b;
            double chord = 4.0 * ab * ab * ((h - p) * (h + p)) / (h * h * h * h);

            // The circle's band, widened by what rounding can do to D, whose change is 8 (a b)^2 / h^4 times p for each unit
            // the line moves and h for each unit the reach does: where the rim runs flat, at the ends of the minor axis of a
            // thin ellipse, that is 8 (a b)^2 / h^3, and on an ellipse 816 by 1.07 a line moved by its rounding alone cut a
            // chord past the tolerance where it touched. Sized from the stored point of the plane instead, the band merged
            // the crossings 0.04 apart of a plane leaning 1E-5 off a circle of radius 300, its point stored 1E4 away.
            double graze = tolerance.EqualPoint * tolerance.EqualPoint
                + 8.0 * ab * ab / (h * h * h * h) * (p * rounding + h * 4.0 * Epsilon * a);

            if (chord < -graze)
            {
                return found;
            }

            // The middle of the chord, where the line comes nearest the centre in the frame scaled to the unit circle.
            double middle = -(u0 * du / (a * a) + v0 * dv / (b * b)) / (du * du / (a * a) + dv * dv / (b * b));
            GeoPoint3 foot = Lift(ellipse, new GeoPoint2(u0, v0));

            if (chord <= graze)
            {
                found.Add(foot.Add(direction.Multiply(middle)));

                return found;
            }

            double half = 0.5 * Math.Sqrt(chord) / speed;

            found.Add(foot.Add(direction.Multiply(middle - half)));
            found.Add(foot.Add(direction.Multiply(middle + half)));

            return found;
        }

        /// <summary>
        /// Gets the points of an arc on the line where its plane meets the ellipse's, the line found from the arc's centre
        /// and the points read off it as <see cref="Arc3"/> reads them.
        /// </summary>
        private static List<GeoPoint3> PointsOnPlane(GeoArc3 arc, GeoEllipse3 ellipse, Tolerance tolerance)
        {
            if (!Intersection3.TryGetMeetingLineFrom(arc.Center, arc.Normal, 2.0 * arc.Radius, ellipse.GetPlane(), tolerance, out GeoVector3 offset, out GeoVector3 direction, out _))
            {
                return new List<GeoPoint3>();
            }

            return Arc3.PointsOnLine(arc, arc.Center.Add(offset), direction, tolerance);
        }

        /// <summary>
        /// Gets the points of the rim on a segment, the segment read in the plane.
        /// </summary>
        /// <remarks>
        /// The segment is laid out in the ellipse's own frame and <see cref="Ellipse2"/> finds where it crosses the rim,
        /// touching it where it passes within the point tolerance. Each answer is put back on the segment itself, at the same
        /// place along it, as the circle's answers are points of the segment, and kept where it lies within the point
        /// tolerance of the rim in space.
        /// </remarks>
        private static List<GeoPoint3> AlongSegment(GeoEllipse3 ellipse, GeoPoint3 start, GeoVector3 direction, Tolerance tolerance)
        {
            Frame(ellipse, start, out double along, out double across, out _);

            double alongStep = direction.DotProduct(ellipse.MajorAxis);
            double acrossStep = direction.DotProduct(ellipse.MinorAxis);
            var laidOut = new GeoLine2(new GeoPoint2(along, across), new GeoPoint2(along + alongStep, across + acrossStep));

            GeoPoint2[] points = Ellipse2.GetIntersections(Flat(ellipse), laidOut, tolerance);
            var found = new List<GeoPoint3>(points.Length);
            double speed = alongStep * alongStep + acrossStep * acrossStep;

            foreach (GeoPoint2 point in points)
            {
                double at = speed > 0.0 ? ((point.X - along) * alongStep + (point.Y - across) * acrossStep) / speed : 0.0;
                GeoPoint3 onSegment = start.Add(direction.Multiply(at));

                // A segment lying in the plane may still stand off it by up to the planar tolerance, and the rim is asked in
                // space, as an arc asks.
                if (OnRim(ellipse, onSegment, tolerance))
                {
                    found.Add(onSegment);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the candidates a segment or a ray gives against the rim: origin + t·direction for t from nought to a span, in
        /// order along it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two readings, both kept where the rim in space holds them, within the point tolerance as an arc holds a point,
        /// and a place they both find named once. The stretch laid onto the plane is crossed with the rim by
        /// <see cref="Ellipse2"/> and each crossing put back on the stretch at the same place along it, which finds a
        /// stretch lying in the plane or running within the tolerance of it; and the point where it pierces the plane, which
        /// finds a steep one, whose laid-out crossings stand off the rim and drop out.
        /// </para>
        /// <para>
        /// Read only where it pierced, a segment from (-400, 50, -0.0009) to (400, 50, 0.0011), which passes the rim of the
        /// ellipse 300 by 100 within the tolerance at x = ±259.8, touched nothing: it pierces at x = -40.
        /// </para>
        /// <para>
        /// The caller still asks its own shape whether it reaches each candidate, as the circle's do.
        /// </para>
        /// </remarks>
        private static List<GeoPoint3> PointsOnLine(GeoEllipse3 ellipse, GeoPoint3 origin, GeoVector3 direction, double span, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            if (direction.LengthSquared <= tolerance.EqualVector * tolerance.EqualVector)
            {
                // No direction to follow. The one point there is counts if the rim reaches it.
                if (OnRim(ellipse, origin, tolerance))
                {
                    found.Add(origin);
                }

                return found;
            }

            double start = ellipse.Center.GetVectorTo(origin).DotProduct(ellipse.Normal);
            double rise = direction.DotProduct(ellipse.Normal);
            double end = start + span * rise;
            double reach = tolerance.EqualPoint;

            // Every point of a stretch wholly to one side, further off than the rim test reaches, is off the rim.
            if ((start > reach && end > reach) || (start < -reach && end < -reach))
            {
                return found;
            }

            foreach (GeoPoint3 crossing in AlongSegment(ellipse, origin, direction.Multiply(span), tolerance))
            {
                Arc3.AddOnce(found, crossing, tolerance);
            }

            if (Math.Abs(rise) > 0.0)
            {
                AddPierce(found, ellipse, origin.Add(direction.Multiply(-start / rise)), tolerance);
            }

            if (found.Count < 2)
            {
                return found;
            }

            var keys = new double[found.Count];
            var order = new int[found.Count];

            for (int i = 0; i < found.Count; i++)
            {
                keys[i] = origin.GetVectorTo(found[i]).DotProduct(direction);
                order[i] = i;
            }

            Array.Sort(order, (x, y) =>
            {
                int byKey = keys[x].CompareTo(keys[y]);

                return byKey != 0 ? byKey : x.CompareTo(y);
            });

            var sorted = new List<GeoPoint3>(found.Count);

            foreach (int i in order)
            {
                sorted.Add(found[i]);
            }

            return sorted;
        }

        /// <summary>
        /// Adds the point where a stretch pierces the plane, when it is on the rim and not a place a laid-out crossing has
        /// named already.
        /// </summary>
        /// <remarks>
        /// A stretch running within the tolerance of the rim over some length pierces the plane somewhere along it, up to the
        /// tolerance off the rim, and its laid-out crossing lies elsewhere on the same length: a segment lying in the plane
        /// across an ellipse 2.5 by 0.43 gave its two crossings and a third, 0.0023 from one of them. Two such points are one
        /// place where the stretch stays on the rim between them, at their middle; the one nearer the rim is kept.
        /// </remarks>
        private static void AddPierce(List<GeoPoint3> found, GeoEllipse3 ellipse, GeoPoint3 pierce, Tolerance tolerance)
        {
            double gap = RimGap(ellipse, pierce);

            if (!(gap <= tolerance.EqualPoint))
            {
                return;
            }

            // Only the nearest crossing can be the same place: the other end of a short chord is a place of its own.
            int nearest = -1;

            for (int i = 0; i < found.Count; i++)
            {
                if (nearest < 0 || found[i].DistanceTo(pierce) < found[nearest].DistanceTo(pierce))
                {
                    nearest = i;
                }
            }

            if (nearest >= 0)
            {
                GeoPoint3 crossing = found[nearest];
                GeoPoint3 middle = crossing.Add(crossing.GetVectorTo(pierce).Multiply(0.5));

                if (crossing.IsEqualTo(pierce, tolerance) || OnRim(ellipse, middle, tolerance))
                {
                    if (gap < RimGap(ellipse, crossing))
                    {
                        found[nearest] = pierce;
                    }

                    return;
                }
            }

            found.Add(pierce);
        }

        /// <summary>
        /// Gets how far a point lies from the rim, measured in space: the root of its height above the plane squared and the
        /// gap from its foot to the rim squared.
        /// </summary>
        private static double RimGap(GeoEllipse3 ellipse, GeoPoint3 point)
        {
            Frame(ellipse, point, out double along, out double across, out double height);

            double gap = Ellipse2.Gap(Flat(ellipse), along, across);

            return Math.Sqrt(height * height + gap * gap);
        }

        /// <summary>
        /// Gets the points where the rim pierces a flat region, the region's plane not the ellipse's.
        /// </summary>
        private static List<GeoPoint3> PiercingPoints(GeoEllipse3 ellipse, GeoPlane3 plane, Func<GeoPoint3, bool> holds, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoPoint3 candidate in PointsOnPlane(ellipse, plane, tolerance))
            {
                if (holds(candidate))
                {
                    Arc3.AddOnce(found, candidate, tolerance);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets the points where the rim cuts a run of edges lying in its plane, each read there.
        /// </summary>
        private static void AddCrossingsOfEdges(List<GeoPoint3> found, GeoEllipse3 ellipse, IEnumerable<GeoLine3> edges, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                foreach (GeoPoint3 crossing in AlongSegment(ellipse, edge.StartPoint, edge.Direction, tolerance))
                {
                    // Two edges share a corner, so a rim through a corner is found twice.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        /// <summary>
        /// Gathers the points where the rim meets the boundary of a body, openings included, as an arc's are gathered.
        /// </summary>
        private static void AddCrossingsOfBody(List<GeoPoint3> found, GeoEllipse3 ellipse, GeoSolid3 body, GeoSolid3 part, Tolerance tolerance)
        {
            foreach (GeoFace3 face in part.Faces)
            {
                foreach (GeoPoint3 candidate in GetIntersections(ellipse, face, tolerance))
                {
                    if (Containment3.Locate(body, candidate, tolerance) == PointLocation.OnSide)
                    {
                        Arc3.AddOnce(found, candidate, tolerance);
                    }
                }
            }

            foreach (GeoSolid3 opening in part.Openings)
            {
                AddCrossingsOfBody(found, ellipse, body, opening, tolerance);
            }
        }

        /// <summary>
        /// Gathers what each edge of a curved chain or loop gives, naming each place once.
        /// </summary>
        private static GeoPoint3[] CrossingsOfEdges(GeoEllipse3 ellipse, IEnumerable<GeoEdge3> edges, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in edges)
            {
                foreach (GeoPoint3 crossing in edge.IsArc ? GetIntersections(ellipse, edge.ToArc(), tolerance) : Crossings(ellipse, edge.ToLine(), tolerance))
                {
                    // Two edges share a corner, so a crossing at a corner is found by both of them.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Lays a circle of the ellipse's plane out in the ellipse's own frame.
        /// </summary>
        private static GeoCircle2 LayOut(GeoEllipse3 ellipse, GeoPoint3 center, double radius)
        {
            Frame(ellipse, center, out double along, out double across, out _);

            return new GeoCircle2(new GeoPoint2(along, across), radius);
        }

        /// <summary>
        /// Lays another ellipse of the ellipse's plane out in the ellipse's own frame, its radii kept and its axis read in
        /// the plane.
        /// </summary>
        private static GeoEllipse2 LayOut(GeoEllipse3 ellipse, GeoEllipse3 other)
        {
            Frame(ellipse, other.Center, out double along, out double across, out _);

            var axis = new GeoVector2(other.MajorAxis.DotProduct(ellipse.MajorAxis), other.MajorAxis.DotProduct(ellipse.MinorAxis));

            return new GeoEllipse2(new GeoPoint2(along, across), axis, other.MajorRadius, other.MinorRadius);
        }

        /// <summary>
        /// Puts the points found in the plane back into space, keeping the ones the other curve holds there and naming each
        /// place once.
        /// </summary>
        /// <remarks>
        /// The other curve is laid out flat to be read in the plane, but it may stand off it by up to the planar tolerance:
        /// a point the two share in the plane counts only where the curve itself, in space, passes within the point
        /// tolerance of it, as the circle keeps only what the other circle holds. Two points no further apart than twice the
        /// point tolerance are one place, as not in one plane (<see cref="AddApart"/>): merged only within one tolerance, at
        /// a tolerance of 0.05 two crossings of a thin ellipse 280.6 by 0.017 and one 39.6 by 0.035, 0.0916 apart, were
        /// both given.
        /// </remarks>
        private static List<GeoPoint3> Kept(GeoEllipse3 ellipse, GeoPoint2[] points, Func<GeoPoint3, Tolerance, bool> holds, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>(points.Length);

            foreach (GeoPoint3 point in Lift(ellipse, points))
            {
                if (holds(point, tolerance))
                {
                    AddApart(found, point, tolerance);
                }
            }

            return found;
        }

        /// <summary>
        /// Drops the points read in the plane where the rims do not come within the point tolerance of each other in space:
        /// where the point of the rim nearest one stands further than that from the other curve.
        /// </summary>
        /// <remarks>
        /// A touch read in the plane is judged with the other curve laid flat, which may stand off the plane by up to the
        /// planar tolerance, and is put midway between the two, within the tolerance of both where the rims are up to twice
        /// it apart: a thin ellipse 3.73 by 0.027 in the plane of one 98 684 by 68 437, their rims 0.00124 apart, touched at
        /// a point 0.00100 and 0.00042 off them. Where a point is dropped and the rims do come within the tolerance, the
        /// search along the rim (<see cref="NearPass3"/>) names the place.
        /// </remarks>
        private static void Passing(List<GeoPoint3> points, GeoEllipse3 ellipse, NearPass3.Target target, Tolerance tolerance)
        {
            points.RemoveAll(point => !(target.Gap(GetClosestPointOnBoundary(ellipse, point)) <= tolerance.EqualPoint));
        }

        /// <summary>
        /// Determines whether an ellipse and a circle of its plane draw the same curve, as <c>CollidesWith</c> reads it: the
        /// centres and both radii within the point tolerance. Such a pair crosses nowhere, as documented, though the rims run
        /// within the tolerance of each other all the way round.
        /// </summary>
        private static bool OnOneCircle(GeoEllipse3 ellipse, GeoPoint3 center, double radius, Tolerance tolerance)
            => ellipse.Center.IsEqualTo(center, tolerance)
                && Math.Abs(ellipse.MajorRadius - radius) <= tolerance.EqualPoint
                && Math.Abs(ellipse.MinorRadius - radius) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether two ellipses of one plane, the other laid out in the first's frame, draw the same curve: the same
        /// ellipse within the tolerance (<see cref="Ellipse2.IsEqualTo(GeoEllipse2, GeoEllipse2, Tolerance)"/>), or, where one
        /// of them is a circle, its two radii one to within 1E-12 of the larger, as <see cref="Ellipse2"/> reads a circle, the
        /// pair the circle's test calls one circle (<see cref="OnOneCircle(GeoEllipse3, GeoPoint3, double, Tolerance)"/>): the
        /// centres within the point tolerance and both radii of the other within it of the circle's, whichever way the other's
        /// axis points.
        /// </summary>
        /// <remarks>
        /// Read by the axes alone, the ellipse 100.0007 by 99.9995 and the circle of radius 100 about the same centre drew one
        /// curve asked as a circle, and crossed at four places asked as the circle read as an ellipse
        /// (<see cref="GeoEllipse3.FromCircle(GeoCircle3)"/>). The axes are passed over only where one is a circle: two
        /// ellipses 1.04978 by 1.04840 and 1.04936 by 1.04903, the second a circle within the tolerance but not drawn as one,
        /// their centres 0.00032 apart and their axes turned, cross where their rims come within the tolerance of each other,
        /// though each radius of the one lies within it of the other radius of the other. Told by radii exactly one, the
        /// circle of radius 100 drawn as an ellipse 100 by 99.99999999999999 crossed that ellipse 100.0007 by 99.9995, its axis
        /// turned a quarter, at four places.
        /// </remarks>
        private static bool SameCurve(GeoEllipse2 flat, GeoEllipse2 laidOut, Tolerance tolerance)
            => Ellipse2.IsEqualTo(flat, laidOut, tolerance)
                || (laidOut.MajorRadius - laidOut.MinorRadius <= 1E-12 * laidOut.MajorRadius
                    && OnOneCircle(flat, laidOut.Center, laidOut.MajorRadius, tolerance))
                || (flat.MajorRadius - flat.MinorRadius <= 1E-12 * flat.MajorRadius
                    && OnOneCircle(laidOut, flat.Center, flat.MajorRadius, tolerance));

        /// <summary>
        /// Determines whether an ellipse and a circle of one plane, read in one frame, are one circle as
        /// <see cref="OnOneCircle(GeoEllipse3, GeoPoint3, double, Tolerance)"/> reads them: the centres and both radii within
        /// the point tolerance.
        /// </summary>
        private static bool OnOneCircle(GeoEllipse2 ellipse, GeoPoint2 center, double radius, Tolerance tolerance)
            => ellipse.Center.IsEqualTo(center, tolerance)
                && Math.Abs(ellipse.MajorRadius - radius) <= tolerance.EqualPoint
                && Math.Abs(ellipse.MinorRadius - radius) <= tolerance.EqualPoint;

        /// <summary>
        /// Orders the points two curves of one plane share as the circle orders two circles' crossings: along their radical
        /// line, the first curve's normal crossed with the line from its centre to the other's.
        /// </summary>
        /// <remarks>
        /// Where the centres are one, within the point tolerance, there is no such line and the points keep the order of the
        /// eccentric angle they came in. Ties keep the order they came in too, so the result never hangs on rounding alone.
        /// </remarks>
        private static GeoPoint3[] AlongRadicalLine(List<GeoPoint3> points, GeoVector3 normal, GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
        {
            GeoVector3 between = from.GetVectorTo(to);

            if (points.Count < 2 || between.Length <= tolerance.EqualPoint)
            {
                return points.ToArray();
            }

            GeoVector3 across = normal.CrossProduct(between);
            var keys = new double[points.Count];
            var order = new int[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                keys[i] = from.GetVectorTo(points[i]).DotProduct(across);
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

        #endregion
    }
}
