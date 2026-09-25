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
    public static class Arc2
    {
        #region Projection and distance

        /// <summary>
        /// Gets the point of an arc closest to a point, using the default tolerance.
        /// </summary>
        public static GeoPoint2 ProjectToArc(GeoArc2 arc, GeoPoint2 point) => ProjectToArc(arc, point, Tolerance.Global);

        /// <summary>
        /// Gets the point of an arc closest to a point, within a tolerance.
        /// </summary>
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
        public static double DistanceTo(GeoArc2 arc, GeoPoint2 point, Tolerance tolerance)
        {
            return point.DistanceTo(ProjectToArc(arc, point, tolerance));
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
        /// <returns>Zero when they cross, otherwise the shortest gap between them.</returns>
        /// <remarks>
        /// The nearest pair is either at an end of one of them, or where the segment runs straight at the
        /// centre of the arc. Both are checked, so the answer is exact rather than sampled.
        /// </remarks>
        public static double DistanceTo(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            if (TryIntersectWith(arc, line, out _, tolerance))
            {
                return 0.0;
            }

            double best = Math.Min(
                Math.Min(DistanceTo(arc, line.StartPoint, tolerance), DistanceTo(arc, line.EndPoint, tolerance)),
                Math.Min(Distance2.DistanceTo(line, arc.StartPoint), Distance2.DistanceTo(line, arc.EndPoint)));

            // Where the segment runs straight at the centre, the nearest point of the circle lies on that
            // line. Clamping it onto the arc keeps the candidate on the arc whether or not the arc reaches
            // that far round: when it does the clamp changes nothing, and when it does not the candidate
            // falls back to the end, which is where the nearest pair really is.
            GeoPoint2 nearestOnLine = Projection2.ProjectToLine(line, arc.Center);

            if (arc.Center.GetVectorTo(nearestOnLine).Length > tolerance.EqualPoint)
            {
                best = Math.Min(best, ProjectToArc(arc, nearestOnLine, tolerance).DistanceTo(nearestOnLine));
            }

            return best;
        }

        /// <summary>
        /// Gets the distance between two arcs, using the default tolerance.
        /// </summary>
        public static double DistanceTo(GeoArc2 first, GeoArc2 second) => DistanceTo(first, second, Tolerance.Global);

        /// <summary>
        /// Gets the distance between two arcs, within a tolerance.
        /// </summary>
        /// <param name="first">The first arc.</param>
        /// <param name="second">The second arc.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Zero when they cross, otherwise the shortest gap between them.</returns>
        /// <remarks>
        /// The nearest pair is either at an end of one of them, or on the line joining the two centres,
        /// where two full circles are nearest. Both are checked.
        /// </remarks>
        public static double DistanceTo(GeoArc2 first, GeoArc2 second, Tolerance tolerance)
        {
            if (TryIntersectWith(first, second, out _, tolerance))
            {
                return 0.0;
            }

            double best = Math.Min(
                Math.Min(DistanceTo(first, second.StartPoint, tolerance), DistanceTo(first, second.EndPoint, tolerance)),
                Math.Min(DistanceTo(second, first.StartPoint, tolerance), DistanceTo(second, first.EndPoint, tolerance)));

            GeoVector2 betweenCenters = first.Center.GetVectorTo(second.Center);
            double apart = betweenCenters.Length;

            if (apart > tolerance.EqualPoint)
            {
                GeoVector2 unit = betweenCenters.Multiply(1.0 / apart);

                // The two points of the circles on the line joining the centres, facing each other and
                // facing away, are where two whole circles are nearest and furthest. Each is clamped onto
                // its own arc: an arc that does not reach that far round is nearest at an end instead, and
                // every end is weighed above. Testing the direction against Tolerance.EqualAngleRad
                // instead would admit a point a whole degree past the end, which on a radius of a hundred
                // is nearly two of whatever the drawing is measured in.
                foreach (int side in new[] { 1, -1 })
                {
                    GeoPoint2 onFirst = first.Center.Add(unit.Multiply(side * first.Radius));
                    GeoPoint2 onSecond = second.Center.Add(unit.Multiply(-side * second.Radius));

                    best = Math.Min(
                        best,
                        ProjectToArc(first, onFirst, tolerance).DistanceTo(ProjectToArc(second, onSecond, tolerance)));
                }
            }

            return best;
        }

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
        /// A circle is an arc that sweeps a whole turn, so the two are measured against each other the way
        /// two arcs are, exactly and without either being cut into pieces.
        /// </remarks>
        public static double DistanceTo(GeoArc2 arc, GeoCircle2 circle, Tolerance tolerance)
        {
            return DistanceTo(arc, AsArc(circle), tolerance);
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
        /// are left out.
        /// </remarks>
        public static bool TryIntersectWith(GeoArc2 arc, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            Intersection2.TryIntersectWith(arc.GetCircle(), line, out GeoPoint2[] onCircle, tolerance);

            return KeepWhatTheArcsReach(onCircle, arc, null, tolerance, out intersections);
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
        /// overlap have none even though their circles are everywhere the same.
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
        /// same distance rather than anything sampled.
        /// </remarks>
        public static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            if (TryIntersectWith(arc, line, out GeoPoint2[] meetings, tolerance) && meetings.Length > 0)
            {
                return new GeoLine2(meetings[0], meetings[0]);
            }

            var best = new GeoLine2(arc.StartPoint, Projection2.ProjectToLine(line, arc.StartPoint));

            Consider(ref best, arc.EndPoint, Projection2.ProjectToLine(line, arc.EndPoint));
            Consider(ref best, ProjectToArc(arc, line.StartPoint, tolerance), line.StartPoint);
            Consider(ref best, ProjectToArc(arc, line.EndPoint, tolerance), line.EndPoint);

            // Where the segment runs straight at the centre, the nearest point of the circle lies on that
            // line, clamped onto the arc so that the pair is one the arc really holds.
            GeoPoint2 nearestOnLine = Projection2.ProjectToLine(line, arc.Center);

            if (arc.Center.GetVectorTo(nearestOnLine).Length > tolerance.EqualPoint)
            {
                Consider(ref best, ProjectToArc(arc, nearestOnLine, tolerance), nearestOnLine);
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
        /// other, or the two points facing each other along the line joining the centres.
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

                // The two points of the circles on the line joining the centres, facing each other and
                // facing away, are where two whole circles are nearest and furthest. Each is clamped onto
                // its own arc: an arc that does not reach that far round is nearest at an end instead, and
                // every end is weighed above. Testing the direction against Tolerance.EqualAngleRad
                // instead would admit a point a whole degree past the end, which on a radius of a hundred
                // is nearly two of whatever the drawing is measured in.
                foreach (int side in new[] { 1, -1 })
                {
                    GeoPoint2 onFirst = first.Center.Add(unit.Multiply(side * first.Radius));
                    GeoPoint2 onSecond = second.Center.Add(unit.Multiply(-side * second.Radius));

                    Consider(
                        ref best,
                        ProjectToArc(first, onFirst, tolerance),
                        ProjectToArc(second, onSecond, tolerance));
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
