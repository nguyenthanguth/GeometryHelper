using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What can be asked of an arc: where a point falls on it, how far a shape is from it, and where it
    /// meets a segment, a circle or another arc.
    /// <para>
    /// An arc is part of a circle, so every answer here is the circle's answer kept only where the arc
    /// actually reaches. A crossing beyond the ends of the arc belongs to the circle, not to the arc, and
    /// is left out; a point off the ends is measured to whichever end is nearer.
    /// </para>
    /// </summary>
    public static partial class Arc2
    {
        #region Projection and distance

        /// <summary>
        /// Gets the point of an arc closest to a point, using the default tolerance.
        /// </summary>
        public static GeoPoint2 ProjectToArc(GeoArc2 arc, GeoPoint2 point) => ProjectToArc(arc, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of an arc closest to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point within the tolerance of the centre of an arc is read as at the centre, which has no direction, and
        /// is carried to the arc's start; the distance to the point is the exact one, so the line can be longer than
        /// it, by at most twice the point's offset from the centre. At a tolerance of a thousandth, the point (0,
        /// -0.0009) and the arc of radius 50 about the origin from (-50, 0) to (50, 0) through (0, -50): the closest
        /// point is (-50, 0), 50.00000001 away, where the distance is 49.9991.
        /// </remarks>
        /// <param name="arc">The arc.</param>
        /// <param name="point">The point; it does not have to lie on the arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The point of the arc closest to the given point, which is one of its ends when the point falls beyond them.</returns>
        public static GeoPoint2 ProjectToArc(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return arc.GetPointAtParameter(arc.GetParameterAtPoint(point, tolerance));
        }

        /// <summary>
        /// Gets the distance from an arc to a point, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoArc2 arc, GeoPoint2 point) => DistanceTo(arc, point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from an arc to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The distance is the exact one, and the tolerance changes nothing in it: a point at the centre of the arc is as far
        /// from it as any other, and a point near the centre is not carried to the arc's start. The arc of radius 50 from
        /// (-50, 0) to (50, 0) through (0, 50) is 49.9991 from (0, -0.0009), at a tolerance of a thousandth. The projection
        /// <see cref="ProjectToArc(GeoArc2, GeoPoint2, Tolerance)"/> keeps that snap.
        /// </remarks>
        public static double DistanceTo(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return point.DistanceTo(ProjectToArc(arc, point, Exactly));
        }

        /// <summary>
        /// Gets the distance between an arc and a segment, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoArc2 arc, GeoLine2 line) => DistanceTo(arc, line, Tolerance.Global);

        /// <summary>
        /// Gets the distance between an arc and a segment, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The nearest pair is either at an end of one of them, or where the segment runs straight at the
        /// centre of the arc. Both are checked, so the answer is exact rather than sampled. A segment ending 0.0005 above
        /// the top of the arc of radius 50 about the origin is 0.0005 from it, whatever the tolerance; the tolerance only
        /// reads a segment no longer than itself as its start point, and an arc bowing less than its own rounding as its
        /// chord.
        /// </remarks>
        public static double DistanceTo(GeoArc2 arc, GeoLine2 line, Tolerance tolerance) => Gap(arc, line, tolerance);

        /// <summary>
        /// Gets the distance between two arcs, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoArc2 first, GeoArc2 second) => DistanceTo(first, second, Tolerance.Global);

        /// <summary>
        /// Gets the distance between two arcs, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="tolerance">The tolerance, taken so that this reads like every other pair; it changes nothing here.</param>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The nearest pair is either at an end of one of them, or on the line joining the two centres,
        /// where two full circles are nearest. Both are checked. The upper half of the circle of radius 50 about the
        /// origin, with its top at (0, 50), and the lower half of the circle of radius 50 about (0, 100.0005), with its
        /// bottom at (0, 50.0005), are 0.0005 apart whatever the tolerance; two arcs of one radius that cross 5E-6 of a
        /// radian inside both their ends are nought apart.
        /// </remarks>
        public static double DistanceTo(GeoArc2 first, GeoArc2 second, Tolerance tolerance) => Gap(first, second);

        #endregion

        #region Containment

        /// <summary>
        /// Gets the distance between an arc and a circle.
        /// </summary>
        public static double DistanceTo(GeoArc2 arc, GeoCircle2 circle) => DistanceTo(arc, circle, Tolerance.Global);

        /// <summary>
        /// Gets the distance between an arc and a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The circle is read as the disc it bounds, as every closed shape is by <c>DistanceTo</c>: an arc lying
        /// inside it, or crossing its rim, is nought away. Beyond it the nearest point of the disc lies on the
        /// rim straight towards the centre, so the distance is how near the arc comes to the centre, less the
        /// radius. It was measured to the rim as one arc to another, so an arc inside a disc of radius a
        /// hundred was seventy away while <c>CollidesWith</c> said the two touched.
        /// <see cref="GetShortestLineTo(GeoArc2, GeoCircle2, Tolerance)"/> still runs rim to rim.
        /// </remarks>
        public static double DistanceTo(GeoArc2 arc, GeoCircle2 circle, Tolerance tolerance)
        {
            // The centre is measured with no snapping: a centre within the point tolerance of the arc's own would be taken for
            // the arc's start, though every point of a concentric arc is as far from it as any other.
            return Math.Max(0.0, DistanceTo(arc, circle.Center, Exactly) - circle.Radius);
        }

        /// <summary>
        /// Gets a circle as the arc that sweeps a whole turn round it.
        /// </summary>
        internal static GeoArc2 AsArc(GeoCircle2 circle) => new GeoArc2(circle.Center, circle.Radius, 0.0, 0.0);

        /// <summary>
        /// Determines whether a point lies on an arc, using the default tolerance.
        /// </summary>
        public static bool IsPointOn(GeoArc2 arc, GeoPoint2 point) => IsPointOn(arc, point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on an arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the point lies on the arc, not merely on the circle carrying it; otherwise, false.</returns>
        public static bool IsPointOn(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return DistanceTo(arc, point, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Says where a point sits relative to an arc, using the default tolerance.
        /// </summary>
        public static PointLocation Locate(GeoArc2 arc, GeoPoint2 point) => Locate(arc, point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to an arc, within a tolerance.
        /// </summary>
        /// <returns><see cref="PointLocation.OnSide"/> when the point lies on the arc; otherwise <see cref="PointLocation.OutSide"/>.</returns>
        /// <remarks>
        /// An arc is a curve, so it encloses nothing and never answers <see cref="PointLocation.Inside"/>,
        /// as every other curve in the library behaves.
        /// </remarks>
        public static PointLocation Locate(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return IsPointOn(arc, point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;
        }

        #endregion

        #region Intersection

        /// <summary>
        /// Finds where an arc meets a segment, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoArc2 arc, GeoLine2 line, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(arc, line, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Finds where an arc meets a segment, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossings, empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if they meet at least once; otherwise, false.</returns>
        /// <remarks>
        /// The circle carrying the arc may be met where the arc itself does not reach, and those crossings
        /// are left out. A segment whose line runs inside the rim within the point tolerance of it, and that
        /// meets the arc nowhere so, still touches it where it comes within the tolerance of the arc: once, at
        /// the foot of the perpendicular from the centre when the foot is on the segment and the arc holds
        /// it, else at the point of the segment nearest the arc. At a tolerance of 0.001, a segment ending
        /// 0.0002 from the end of an arc, its line 0.0002 inside the rim, touches the arc at that end of the
        /// segment.
        /// </remarks>
        public static bool TryIntersectWith(GeoArc2 arc, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            Intersection2.TryIntersectWith(arc.GetCircle(), line, out GeoPoint2[] onCircle, tolerance);

            if (KeepWhatTheArcsReach(onCircle, arc, null, tolerance, out intersections))
            {
                return true;
            }

            return TryTouchInsideTheRim(arc, line, tolerance, out intersections);
        }

        /// <summary>
        /// Finds the one point where a segment whose line runs inside an arc's rim, within the point tolerance of it,
        /// touches the arc without meeting it: at the foot of the perpendicular from the centre when the foot is on the
        /// segment and the arc holds it, else at the point of the segment nearest the arc. None when the segment does
        /// not come within the tolerance of the arc.
        /// </summary>
        private static bool TryTouchInsideTheRim(GeoArc2 arc, GeoLine2 line, Tolerance tolerance, out GeoPoint2[] intersections)
        {
            intersections = Array.Empty<GeoPoint2>();

            GeoVector2 d = line.Direction;
            double dLength = d.Length;

            if (dLength <= tolerance.EqualPoint)
            {
                return false;
            }

            // The foot of the perpendicular, read as Intersection2 reads it.
            double centerParameter = line.StartPoint.GetVectorTo(arc.Center).DotProduct(d) / (dLength * dLength);
            double centerDistance = Distance2.DistanceTo(arc.Center, line.GetPointAtParameter(centerParameter));

            if (!(centerDistance < arc.Radius && centerDistance >= arc.Radius - tolerance.EqualPoint))
            {
                return false;
            }

            GeoLine2 nearest = NearestPair(arc, line, tolerance);

            if (!(nearest.Length <= tolerance.EqualPoint))
            {
                return false;
            }

            double parameterTolerance = tolerance.EqualPoint / dLength;

            if (centerParameter >= -parameterTolerance && centerParameter <= 1.0 + parameterTolerance)
            {
                GeoPoint2 foot = line.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, centerParameter)));

                if (IsPointOn(arc, foot, tolerance))
                {
                    intersections = new[] { foot };
                    return true;
                }
            }

            intersections = new[] { nearest.EndPoint };
            return true;
        }

        /// <summary>
        /// Finds where an arc meets a circle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoArc2 arc, GeoCircle2 circle, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(arc, circle, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Finds where an arc meets a circle, within a tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoArc2 arc, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            Intersection2.TryIntersectWith(arc.GetCircle(), circle, out GeoPoint2[] onCircle, tolerance);

            return KeepWhatTheArcsReach(onCircle, arc, null, tolerance, out intersections);
        }

        /// <summary>
        /// Finds where two arcs meet, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoArc2 first, GeoArc2 second, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(first, second, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Finds where two arcs meet, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="intersections">The crossings, empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if they meet at least once; otherwise, false.</returns>
        /// <remarks>
        /// A crossing counts only where both arcs reach it, so two arcs of the same circle that do not
        /// overlap have none even though their circles are everywhere the same. The crossings do not depend on which arc
        /// is asked first, however flat one of them is: the edge 100 long of bulge 1E-7 from (0, 0) to (100, 0), an arc
        /// of radius 2.5E8, crosses the arc of radius 40 about (50, 30) from 3.4 radians sweeping 1.6 at (23.54249, 0)
        /// either way round.
        /// </remarks>
        public static bool TryIntersectWith(GeoArc2 first, GeoArc2 second, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            Intersection2.TryIntersectWith(first.GetCircle(), second.GetCircle(), out GeoPoint2[] onCircles, tolerance);

            return KeepWhatTheArcsReach(onCircles, first, second, tolerance, out intersections);
        }

        /// <summary>
        /// Gets where an arc meets a segment, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoLine2 line)
        {
            TryIntersectWith(arc, line, out GeoPoint2[] intersections);
            return intersections;
        }

        /// <summary>
        /// Gets where an arc meets a segment, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            TryIntersectWith(arc, line, out GeoPoint2[] intersections, tolerance);
            return intersections;
        }

        /// <summary>
        /// Gets where an arc meets a circle, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoCircle2 circle)
        {
            TryIntersectWith(arc, circle, out GeoPoint2[] intersections);
            return intersections;
        }

        /// <summary>
        /// Gets where an arc meets a circle, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoCircle2 circle, Tolerance tolerance)
        {
            TryIntersectWith(arc, circle, out GeoPoint2[] intersections, tolerance);
            return intersections;
        }

        /// <summary>
        /// Gets where two arcs meet, using the default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 first, GeoArc2 second)
        {
            TryIntersectWith(first, second, out GeoPoint2[] intersections);
            return intersections;
        }

        /// <summary>
        /// Gets where two arcs meet, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoArc2 first, GeoArc2 second, Tolerance tolerance)
        {
            TryIntersectWith(first, second, out GeoPoint2[] intersections, tolerance);
            return intersections;
        }

        #endregion

        #region Splitting

        /// <summary>
        /// Splits an arc at a normalized parameter, using the default tolerance.
        /// </summary>
        public static bool TrySplitAt(GeoArc2 arc, double parameter, out GeoArc2[] pieces) => TrySplitAt(arc, parameter, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits an arc at a normalized parameter, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc to cut.</param>
        /// <param name="parameter">Where to cut it, from 0 at its start to 1 at its end.</param>
        /// <param name="pieces">The pieces in order along the arc, or the arc whole when the method returns false.</param>
        /// <param name="tolerance">The tolerance: a cut within it of either end leaves the arc whole.</param>
        /// <returns>true if the arc was cut in two; otherwise, false.</returns>
        public static bool TrySplitAt(GeoArc2 arc, double parameter, out GeoArc2[] pieces, Tolerance tolerance)
        {
            double atEnds = tolerance.EqualPoint / Math.Max(arc.Length, tolerance.EqualPoint);

            if (double.IsNaN(parameter) || parameter <= atEnds || parameter >= 1.0 - atEnds)
            {
                pieces = new[] { arc };
                return false;
            }

            double cutAngle = arc.StartAngle + arc.SweptAngle * parameter;

            pieces = new[]
            {
                new GeoArc2(arc.Center, arc.Radius, arc.StartAngle, cutAngle, arc.IsClockwise),
                new GeoArc2(arc.Center, arc.Radius, cutAngle, arc.StartAngle + arc.SweptAngle, arc.IsClockwise)
            };

            return true;
        }

        /// <summary>
        /// Splits an arc at the point of it nearest a point, using the default tolerance.
        /// </summary>
        public static bool TrySplitAt(GeoArc2 arc, GeoPoint2 point, out GeoArc2[] pieces) => TrySplitAt(arc, point, out pieces, Tolerance.Global);

        /// <summary>
        /// Splits an arc at the point of it nearest a point, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc to cut.</param>
        /// <param name="point">Where to cut it; a point off the arc cuts at the point of the arc nearest it.</param>
        /// <param name="pieces">The pieces in order along the arc, or the arc whole when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the arc was cut in two; otherwise, false.</returns>
        public static bool TrySplitAt(GeoArc2 arc, GeoPoint2 point, out GeoArc2[] pieces, Tolerance tolerance)
        {
            return TrySplitAt(arc, arc.GetParameterAtPoint(point, tolerance), out pieces, tolerance);
        }

        #endregion

        #region Shortest line

        /// <summary>
        /// Gets the shortest segment joining an arc to a point, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoPoint2 point) => GetShortestLineTo(arc, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining an arc to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point within the tolerance of the centre of an arc is read as at the centre, which has no direction, and
        /// is carried to the arc's start; the distance to the point is the exact one, so the line can be longer than
        /// it, by at most twice the point's offset from the centre. At a tolerance of a thousandth, the point (0,
        /// -0.0009) and the arc of radius 50 about the origin from (-50, 0) to (50, 0) through (0, -50): the closest
        /// point is (-50, 0), 50.00000001 away, where the distance is 49.9991.
        /// </remarks>
        /// <param name="arc">The arc.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving the arc and ending at <paramref name="point"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return new GeoLine2(ProjectToArc(arc, point, tolerance), point);
        }

        /// <summary>
        /// Gets the shortest segment joining an arc to a straight segment, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoLine2 line) => GetShortestLineTo(arc, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining an arc to a straight segment, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving the arc and landing on <paramref name="line"/>, of no length at all where the two cross.</returns>
        /// <remarks>
        /// The candidates are the ones <see cref="DistanceTo(GeoArc2, GeoLine2, Tolerance)"/> already
        /// weighs &#8212; an end of one against the other, and the place where the segment runs straight at
        /// the centre &#8212; so this hands back the pair that measurement was of, and its length is that
        /// same distance rather than anything sampled. The exception is a touch: where the two come within the point
        /// tolerance, this reads them as meeting and hands back a segment of no length, as the line to cut and extend
        /// by, where the distance is the gap.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            if (TryIntersectWith(arc, line, out GeoPoint2[] meetings, tolerance) && meetings.Length > 0)
            {
                return new GeoLine2(meetings[0], meetings[0]);
            }

            return NearestPair(arc, line, tolerance);
        }

        /// <summary>
        /// A tolerance of nought, for measuring with no slack.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);

        /// <summary>
        /// Gets the exact distance between an arc and a segment: nought only where they truly meet.
        /// </summary>
        /// <remarks>
        /// <see cref="DistanceTo(GeoArc2, GeoLine2, Tolerance)"/> used to read nought wherever they came within the point
        /// tolerance, as its crossing test does; this one reads the gap however small, and a crossing however short
        /// the segment. A segment ending 0.0005 off an arc is 0.0005 from it.
        /// </remarks>
        internal static double Gap(GeoArc2 arc, GeoLine2 line) => Gap(arc, line, Exactly);

        /// <summary>
        /// Gets the exact distance between an arc and a segment, a segment no longer than the tolerance read as its start
        /// point where it does not cross the arc.
        /// </summary>
        internal static double Gap(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            if (TryChord(arc, out GeoLine2 chord))
            {
                // The chord is the arc's own side, however short; only the segment is read as its start point.
                return Distance2.DistanceToSide(chord, line, tolerance);
            }

            return Bowed(arc, line, tolerance);
        }

        /// <summary>
        /// Gets the exact distance between an arc that bows, one <see cref="TryChord"/> does not read as its chord, and a
        /// segment.
        /// </summary>
        internal static double Bowed(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            return Meets(arc, line) ? 0.0 : NearestPair(arc, line, tolerance, Exactly).Length;
        }

        /// <summary>
        /// Gets the exact distance between two arcs: nought only where they truly meet.
        /// </summary>
        /// <remarks>
        /// Where the circles cross, the crossings are solved for directly and each is kept where both arcs sweep
        /// through it, with no slack at the ends. Otherwise the nearest pair is an end of one against the other arc, or a
        /// point of each on the line joining the centres, all of which are weighed. An arc bowing less than its own rounding
        /// is read as its chord.
        /// </remarks>
        internal static double Gap(GeoArc2 first, GeoArc2 second)
        {
            if (TryChord(first, out GeoLine2 chord))
            {
                return Gap(second, chord);
            }

            if (TryChord(second, out chord))
            {
                return Gap(first, chord);
            }

            if (Meets(first, second))
            {
                return 0.0;
            }

            double best = Math.Min(
                Math.Min(DistanceTo(first, second.StartPoint, Exactly), DistanceTo(first, second.EndPoint, Exactly)),
                Math.Min(DistanceTo(second, first.StartPoint, Exactly), DistanceTo(second, first.EndPoint, Exactly)));

            GeoVector2 betweenCenters = first.Center.GetVectorTo(second.Center);
            double apart = betweenCenters.Length;

            if (apart > 0.0)
            {
                GeoVector2 unit = betweenCenters.Multiply(1.0 / apart);

                // Two whole circles are nearest and furthest at points on the line joining their centres: facing each other
                // when the circles lie apart, on the same side when one lies inside the other. All four pairings are
                // weighed, each point clamped onto its own arc: an arc that does not reach that far round is nearest at an
                // end instead, and every end is weighed above.
                foreach (int sideFirst in new[] { 1, -1 })
                {
                    foreach (int sideSecond in new[] { 1, -1 })
                    {
                        GeoPoint2 onFirst = first.Center.Add(unit.Multiply(sideFirst * first.Radius));
                        GeoPoint2 onSecond = second.Center.Add(unit.Multiply(sideSecond * second.Radius));

                        best = Math.Min(
                            best,
                            ProjectToArc(first, onFirst, Exactly).DistanceTo(ProjectToArc(second, onSecond, Exactly)));
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the chord of an arc that bows less off it than its own points are known to, which reads as the arc.
        /// </summary>
        private static bool TryChord(GeoArc2 arc, out GeoLine2 chord)
        {
            double sweep = Math.Abs(arc.SweptAngle);

            // The sag of the arc, 2 r sin(sweep / 4) squared, is at least 0.8 times r sweep squared over 8 up to a half turn,
            // so an arc that bows plainly more than its rounding is settled without the sine.
            if (sweep <= Math.PI
                && 0.1 * arc.Radius * sweep * sweep <= (Math.Abs(arc.Center.X) + Math.Abs(arc.Center.Y) + arc.Radius) * FlatFraction)
            {
                // Half the sweep's sine squared, twice, is the sag from the chord without cancelling.
                double quarter = Math.Sin(arc.SweptAngle * 0.25);
                double sag = 2.0 * arc.Radius * quarter * quarter;

                if (IsFlat(sag, arc.Center, arc.Radius))
                {
                    chord = new GeoLine2(arc.StartPoint, arc.EndPoint);
                    return true;
                }
            }

            chord = default(GeoLine2);
            return false;
        }

        /// <summary>
        /// Determines whether an arc bows so little off its chord that the chord is read for it: its sag at most 2.5 parts
        /// in 1E16 of how far its centre and its radius reach from the origin.
        /// </summary>
        /// <remarks>
        /// An arc kept as its centre and radius places its points through the centre, within up to 2.4 parts in 1E16 of that
        /// reach of where they are, and the circle test squares numbers that large: an arc 1 000 long bowing a trillionth,
        /// of radius 2.5E14, met a segment 0.002 off it. Where it bows no more than that, the chord is no further from the
        /// arc than the centre route's own rounding, so it is read in its place, and is exact. A bulge of a ten-millionth
        /// over a chord of 100, which bows 5E-7 on a radius of 2.5E8, is not read so: the centre route is the better there.
        /// </remarks>
        internal static bool IsFlat(double sag, GeoPoint2 center, double radius)
            => sag <= (Math.Abs(center.X) + Math.Abs(center.Y) + radius) * FlatFraction;

        /// <summary>
        /// How much of the reach of an arc's centre and radius the arc may bow by and be read as its chord.
        /// </summary>
        private const double FlatFraction = 2.5E-16;

        /// <summary>
        /// Determines whether a segment truly meets an arc, with no tolerance: where the segment crosses or touches
        /// the circle at a point the arc sweeps through.
        /// </summary>
        /// <remarks>
        /// The two places the segment's line meets the circle are solved for directly, and each is kept where it lies on
        /// the segment and the arc sweeps through its angle. A touch lost to rounding leaves a gap of a few parts in
        /// 1E16 of the radius, which the nearest pair then measures.
        /// </remarks>
        internal static bool Meets(GeoArc2 arc, GeoLine2 line)
        {
            GeoVector2 along = line.Direction;
            GeoVector2 fromCentre = arc.Center.GetVectorTo(line.StartPoint);

            double a = along.DotProduct(along);
            double c = fromCentre.DotProduct(fromCentre) - arc.Radius * arc.Radius;

            if (!(a > 0.0))
            {
                return c == 0.0 && Sweeps(arc, line.StartPoint);
            }

            double b = 2.0 * fromCentre.DotProduct(along);
            double discriminant = b * b - 4.0 * a * c;

            if (discriminant < 0.0)
            {
                return false;
            }

            double root = Math.Sqrt(discriminant);

            double first = (-b - root) / (2.0 * a);
            double second = (-b + root) / (2.0 * a);

            return (first >= 0.0 && first <= 1.0 && Sweeps(arc, line.GetPointAtParameter(first)))
                || (second >= 0.0 && second <= 1.0 && Sweeps(arc, line.GetPointAtParameter(second)));
        }

        /// <summary>
        /// Determines whether two arcs truly meet, with no tolerance: where their circles cross or touch at a point both
        /// arcs sweep through, or lie on each other along a stretch.
        /// </summary>
        /// <remarks>
        /// The crossings are worked out from the smaller circle, as <see cref="Intersection2"/> works them, so they hold
        /// when the other is huge. Circles on each other have no crossings, and the arcs meet where either holds an end of
        /// the other. A touch lost to rounding leaves a gap of a few parts in 1E16 of the radius, which the nearest pair
        /// then measures.
        /// </remarks>
        internal static bool Meets(GeoArc2 first, GeoArc2 second)
        {
            double towardsX = second.Center.X - first.Center.X;
            double towardsY = second.Center.Y - first.Center.Y;
            double apart = Math.Sqrt(towardsX * towardsX + towardsY * towardsY);

            if (apart > first.Radius + second.Radius || apart < Math.Abs(first.Radius - second.Radius))
            {
                return false;
            }

            if (apart == 0.0)
            {
                return Sweeps(first, second.StartPoint) || Sweeps(first, second.EndPoint)
                    || Sweeps(second, first.StartPoint) || Sweeps(second, first.EndPoint);
            }

            // Of two circles of one radius, the one whose centre comes first by X, then by Y, is worked from.
            bool fromSecond = second.Radius < first.Radius
                || (!(first.Radius < second.Radius)
                    && (second.Center.X < first.Center.X || (!(first.Center.X < second.Center.X) && second.Center.Y < first.Center.Y)));

            GeoPoint2 near = fromSecond ? second.Center : first.Center;
            double nearRadius = fromSecond ? second.Radius : first.Radius;
            double farRadius = fromSecond ? first.Radius : second.Radius;
            double directionX = fromSecond ? -towardsX : towardsX;
            double directionY = fromSecond ? -towardsY : towardsY;

            double along = (nearRadius * nearRadius - farRadius * farRadius + apart * apart) / (2.0 * apart);
            double heightSquared = nearRadius * nearRadius - along * along;

            if (heightSquared < 0.0)
            {
                return false;
            }

            double height = Math.Sqrt(heightSquared);
            double footX = near.X + along * directionX / apart;
            double footY = near.Y + along * directionY / apart;
            double acrossX = -directionY * (height / apart);
            double acrossY = directionX * (height / apart);

            var one = new GeoPoint2(footX + acrossX, footY + acrossY);
            var other = new GeoPoint2(footX - acrossX, footY - acrossY);

            return (Sweeps(first, one) && Sweeps(second, one)) || (Sweeps(first, other) && Sweeps(second, other));
        }

        /// <summary>
        /// Determines whether an arc sweeps through the angle of a point about its centre, with no tolerance.
        /// </summary>
        private static bool Sweeps(GeoArc2 arc, GeoPoint2 point)
        {
            double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
            double into = (angle - arc.StartAngle) * (arc.SweptAngle < 0.0 ? -1.0 : 1.0);

            into -= 2.0 * Math.PI * Math.Floor(into / (2.0 * Math.PI));

            return into <= Math.Abs(arc.SweptAngle);
        }

        /// <summary>
        /// Gets the nearest pair of an arc and a segment that do not cross, from the arc to the segment: an end of
        /// one against the other, or the place where the segment runs straight at the centre.
        /// </summary>
        private static GeoLine2 NearestPair(GeoArc2 arc, GeoLine2 line, Tolerance tolerance) => NearestPair(arc, line, tolerance, tolerance);

        /// <summary>
        /// Gets the nearest pair of an arc and a segment as above, a point read on the arc with its own tolerance: nought for the
        /// exact distance, which carries no point near the centre of the arc to its start.
        /// </summary>
        private static GeoLine2 NearestPair(GeoArc2 arc, GeoLine2 line, Tolerance tolerance, Tolerance onArc)
        {
            // Each end of an arc costs a cosine and a sine, so each is worked out once.
            GeoPoint2 start = arc.StartPoint;
            GeoPoint2 end = arc.EndPoint;
            var best = new GeoLine2(start, Projection2.ProjectToLine(line, start, tolerance));

            Consider(ref best, end, Projection2.ProjectToLine(line, end, tolerance));
            Consider(ref best, ProjectToArc(arc, line.StartPoint, onArc), line.StartPoint);
            Consider(ref best, ProjectToArc(arc, line.EndPoint, onArc), line.EndPoint);

            // Where the segment runs straight at the centre, the nearest point of the circle lies on that
            // line, clamped onto the arc so that the pair is one the arc really holds.
            GeoPoint2 nearestOnLine = Projection2.ProjectToLine(line, arc.Center, tolerance);

            if (arc.Center.GetVectorTo(nearestOnLine).Length > tolerance.EqualPoint)
            {
                Consider(ref best, ProjectToArc(arc, nearestOnLine, onArc), nearestOnLine);
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two arcs, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoArc2 first, GeoArc2 second) => GetShortestLineTo(first, second, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two arcs, within a tolerance.
        /// </summary>
        /// <param name="first">The arc the segment leaves.</param>
        /// <param name="second">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="first"/> and landing on <paramref name="second"/>, of no length at all where the two cross.</returns>
        /// <remarks>
        /// As with an arc and a segment, the pair is one of those
        /// <see cref="DistanceTo(GeoArc2, GeoArc2, Tolerance)"/> already weighs: an end of one against the
        /// other, or a point of each on the line joining the centres. Where they come within the point tolerance, this
        /// reads them as meeting and hands back a segment of no length, where the distance is the gap.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoArc2 first, GeoArc2 second, Tolerance tolerance)
        {
            if (TryIntersectWith(first, second, out GeoPoint2[] meetings, tolerance) && meetings.Length > 0)
            {
                return new GeoLine2(meetings[0], meetings[0]);
            }

            var best = new GeoLine2(ProjectToArc(first, second.StartPoint, tolerance), second.StartPoint);

            Consider(ref best, ProjectToArc(first, second.EndPoint, tolerance), second.EndPoint);
            Consider(ref best, first.StartPoint, ProjectToArc(second, first.StartPoint, tolerance));
            Consider(ref best, first.EndPoint, ProjectToArc(second, first.EndPoint, tolerance));

            GeoVector2 betweenCenters = first.Center.GetVectorTo(second.Center);
            double apart = betweenCenters.Length;

            if (apart > tolerance.EqualPoint)
            {
                GeoVector2 unit = betweenCenters.Multiply(1.0 / apart);

                // All four pairings of the points on the line joining the centres, as in DistanceTo: the
                // circle inside another is nearest it on the same side, not facing it.
                foreach (int sideFirst in new[] { 1, -1 })
                {
                    foreach (int sideSecond in new[] { 1, -1 })
                    {
                        GeoPoint2 onFirst = first.Center.Add(unit.Multiply(sideFirst * first.Radius));
                        GeoPoint2 onSecond = second.Center.Add(unit.Multiply(sideSecond * second.Radius));

                        Consider(
                            ref best,
                            ProjectToArc(first, onFirst, tolerance),
                            ProjectToArc(second, onSecond, tolerance));
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining an arc to a circle, using the default tolerance.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoCircle2 circle) => GetShortestLineTo(arc, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining an arc to a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A circle is an arc that sweeps a whole turn, so the two are joined the way two arcs are.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoCircle2 circle, Tolerance tolerance)
        {
            return GetShortestLineTo(arc, AsArc(circle), tolerance);
        }

        /// <summary>
        /// Keeps the shorter of the segment in hand and the pair offered.
        /// </summary>
        /// <remarks>
        /// The comparison is strict, so the first pair of equal length is the one kept and the answer does
        /// not turn on the order the candidates happen to be weighed in.
        /// </remarks>
        private static void Consider(ref GeoLine2 best, GeoPoint2 from, GeoPoint2 to)
        {
            if (from.DistanceTo(to) < best.Length)
            {
                best = new GeoLine2(from, to);
            }
        }

        #endregion

        #region Helpers


        private static bool KeepWhatTheArcsReach(
            GeoPoint2[] candidates,
            GeoArc2 first,
            GeoArc2? second,
            Tolerance tolerance,
            out GeoPoint2[] intersections)
        {
            if (candidates == null || candidates.Length == 0)
            {
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            var kept = new List<GeoPoint2>(candidates.Length);

            foreach (GeoPoint2 candidate in candidates)
            {
                // Every candidate already lies on the circle carrying each arc, so asking whether the arc
                // holds the point is the whole of the question. It is asked as a distance: the angular
                // tolerance is a whole degree by default, and a degree of a large arc is a long way, so a
                // point that far past the end would be reported as a crossing of arcs that never touch.
                if (!IsPointOn(first, candidate, tolerance))
                {
                    continue;
                }

                if (second.HasValue && !IsPointOn(second.Value, candidate, tolerance))
                {
                    continue;
                }

                kept.Add(candidate);
            }

            intersections = kept.ToArray();
            return kept.Count > 0;
        }

        #endregion
    }
}
