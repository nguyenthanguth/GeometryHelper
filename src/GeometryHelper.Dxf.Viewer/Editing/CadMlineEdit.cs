using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The geometry of MLEDIT: where a multiline is crossed, joined at a tee or at a corner, cut, welded, given a vertex or one
    /// taken away, worked out on the <see cref="DxfMlineBuilder"/> of each multiline as AutoCAD 2021 leaves them (its records
    /// read by an accoreconsole probe): the stretches of each element broken where another multiline's elements cross it, an
    /// end moved to where the two lines of the multilines meet, the elements stopped where the other multiline's elements
    /// stand, matched by rank across the corner.
    /// </summary>
    internal static class CadMlineEdit
    {
        private const double Tiny = 1E-9;

        /// <summary>How a crossing leaves the two multilines: the first broken through, or both opened, or both merged.</summary>
        internal enum Joint
        {
            /// <summary>Every element of the first is broken where the second crosses; the second is whole.</summary>
            Closed,

            /// <summary>Every element of the first and the outer ones of the second are broken.</summary>
            Open,

            /// <summary>The outer elements of both are broken, the inner ones run through.</summary>
            Merged,
        }

        /// <summary>Whether an entity is an MLINE.</summary>
        internal static bool IsMline(DxfEntity entity) => entity is DxfUnknownEntity u && u.DxfName == "MLINE";

        /// <summary>The segment and the element of a multiline nearest a point: by the distance to each element along each segment.</summary>
        internal static void Nearest(DxfMlineBuilder mline, GeoPoint3 point, out int segment, out int element)
        {
            segment = 0;
            element = 0;
            double best = double.MaxValue;

            for (int s = 0; s < mline.SegmentCount; s++)
            {
                for (int e = 0; e < mline.ElementCount; e++)
                {
                    double distance = DistanceToSegment(
                        point,
                        mline.ElementPoint(s, e),
                        mline.ElementPoint(Next(mline, s), e)
                    );

                    if (distance < best - Tiny)
                    {
                        best = distance;
                        segment = s;
                        element = e;
                    }
                }
            }
        }

        private static int Next(DxfMlineBuilder mline, int segment) => (segment + 1) % mline.Points.Count;

        private static double DistanceToSegment(GeoPoint3 point, GeoPoint3 from, GeoPoint3 to)
        {
            GeoVector3 way = from.GetVectorTo(to);
            double length = way.LengthSquared;
            double t =
                length > 0.0 ? Math.Max(0.0, Math.Min(1.0, from.GetVectorTo(point).DotProduct(way) / length)) : 0.0;
            return point.DistanceTo(from.Add(way.Multiply(t)));
        }

        /// <summary>The length of a segment of a multiline's line.</summary>
        private static double Length(DxfMlineBuilder mline, int segment) =>
            mline.Points[segment].DistanceTo(mline.Points[Next(mline, segment)]);

        /// <summary>The parameters at which two lines meet, each along its own unit direction from its own point; false where they run parallel.</summary>
        private static bool Meet(
            GeoPoint3 one,
            GeoVector3 oneWay,
            GeoPoint3 two,
            GeoVector3 twoWay,
            out double onePart,
            out double twoPart
        )
        {
            GeoVector3 cross = oneWay.CrossProduct(twoWay);
            double size = cross.LengthSquared;
            onePart = 0.0;
            twoPart = 0.0;

            if (size < 1E-12)
            {
                return false;
            }

            GeoVector3 gap = one.GetVectorTo(two);
            onePart = gap.CrossProduct(twoWay).DotProduct(cross) / size;
            twoPart = gap.CrossProduct(oneWay).DotProduct(cross) / size;
            return true;
        }

        /// <summary>The elements whose offset is the least or the greatest: the outer lines of the multiline.</summary>
        private static List<int> Outer(DxfMlineBuilder mline, out int least, out int most)
        {
            least = 0;
            most = 0;

            for (int e = 1; e < mline.ElementCount; e++)
            {
                if (mline.ElementShift(e) < mline.ElementShift(least) - Tiny)
                {
                    least = e;
                }

                if (mline.ElementShift(e) > mline.ElementShift(most) + Tiny)
                {
                    most = e;
                }
            }

            var outer = new List<int>();

            for (int e = 0; e < mline.ElementCount; e++)
            {
                double shift = mline.ElementShift(e);

                if (
                    Math.Abs(shift - mline.ElementShift(least)) < Tiny
                    || Math.Abs(shift - mline.ElementShift(most)) < Tiny
                )
                {
                    outer.Add(e);
                }
            }

            return outer;
        }

        /// <summary>The stretches left of some after the part from a to b is taken out of them.</summary>
        private static List<DxfMlineBuilder.Run> Without(IEnumerable<DxfMlineBuilder.Run> runs, double from, double to)
        {
            var left = new List<DxfMlineBuilder.Run>();

            foreach (DxfMlineBuilder.Run run in runs)
            {
                if (run.To <= from || run.From >= to)
                {
                    left.Add(run);
                    continue;
                }

                if (run.From < from)
                {
                    left.Add(new DxfMlineBuilder.Run(run.From, from));
                }

                if (run.To > to)
                {
                    left.Add(new DxfMlineBuilder.Run(to, run.To));
                }
            }

            return left;
        }

        /// <summary>
        /// Crosses two multilines, each at the segment nearest where it was picked: the lines of the two segments have to meet
        /// within both. Each element of the target named is broken from where it meets the one outer element of the other
        /// multiline to where it meets the other, as AutoCAD's closed, open and merged crosses break them.
        /// </summary>
        /// <returns>False where the segments' lines do not cross within both.</returns>
        internal static bool Cross(
            DxfMlineBuilder first,
            int firstSegment,
            DxfMlineBuilder second,
            int secondSegment,
            Joint joint
        )
        {
            GeoVector3 firstWay = first.SegmentDirection(firstSegment);
            GeoVector3 secondWay = second.SegmentDirection(secondSegment);

            if (
                !Meet(
                    first.Points[firstSegment],
                    firstWay,
                    second.Points[secondSegment],
                    secondWay,
                    out double onFirst,
                    out double onSecond
                )
                || onFirst < -Tiny
                || onFirst > Length(first, firstSegment) + Tiny
                || onSecond < -Tiny
                || onSecond > Length(second, secondSegment) + Tiny
            )
            {
                return false;
            }

            List<int> firstOuter = Outer(first, out int firstLeast, out int firstMost);
            List<int> secondOuter = Outer(second, out int secondLeast, out int secondMost);
            var all = new List<int>();

            for (int e = 0; e < first.ElementCount; e++)
            {
                all.Add(e);
            }

            Break(
                first,
                firstSegment,
                joint == Joint.Merged ? firstOuter : all,
                second,
                secondSegment,
                secondLeast,
                secondMost
            );

            if (joint != Joint.Closed)
            {
                Break(second, secondSegment, secondOuter, first, firstSegment, firstLeast, firstMost);
            }

            return true;
        }

        /// <summary>Breaks some elements of a multiline across the width of another: from where each meets one outer element of it to where it meets the other.</summary>
        private static void Break(
            DxfMlineBuilder target,
            int segment,
            IEnumerable<int> elements,
            DxfMlineBuilder by,
            int bySegment,
            int least,
            int most
        )
        {
            GeoVector3 way = target.SegmentDirection(segment);
            GeoVector3 byWay = by.SegmentDirection(bySegment);

            foreach (int e in elements)
            {
                GeoPoint3 start = target.ElementPoint(segment, e);

                if (
                    !Meet(start, way, by.ElementPoint(bySegment, least), byWay, out double a, out double _)
                    || !Meet(start, way, by.ElementPoint(bySegment, most), byWay, out double b, out double _)
                )
                {
                    continue;
                }

                target.SetRuns(segment, e, Without(target.RunsOf(segment, e), Math.Min(a, b), Math.Max(a, b)));
            }
        }

        /// <summary>
        /// Joins the first multiline to the second as a tee: the first is trimmed or extended to where the lines meet, on the
        /// side of the meeting the pick on it was, its elements stopping at the second's near outer element (closed and open) or,
        /// for the merged tee, the inner ones at the second's inner elements, rank for rank from the near side; the open and
        /// merged tees also break the second's near outer element across the first's width.
        /// </summary>
        /// <returns>False where the lines do not meet within the second's segment, or the first is closed.</returns>
        internal static bool Tee(
            DxfMlineBuilder first,
            int firstSegment,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            int secondSegment,
            Joint joint
        )
        {
            if (first.IsClosed)
            {
                return false;
            }

            GeoVector3 firstWay = first.SegmentDirection(firstSegment);
            GeoVector3 secondWay = second.SegmentDirection(secondSegment);

            if (
                !Meet(
                    first.Points[firstSegment],
                    firstWay,
                    second.Points[secondSegment],
                    secondWay,
                    out double onFirst,
                    out double onSecond
                )
                || onSecond < -Tiny
                || onSecond > Length(second, secondSegment) + Tiny
            )
            {
                return false;
            }

            GeoPoint3 meeting = first.Points[firstSegment].Add(firstWay.Multiply(onFirst));
            bool keepsStart = first.Points[firstSegment].GetVectorTo(firstPick).DotProduct(firstWay) < onFirst;
            bool secondsLeft = meeting.GetVectorTo(firstPick).DotProduct(second.SegmentLeft(secondSegment)) > 0.0;

            // The second's elements from the side the first comes from, nearest first.
            List<int> near = Ranked(second, secondsLeft ? -1.0 : 1.0);
            int nearest = near[0];
            var stops = new GeoPoint3[first.ElementCount];

            for (int e = 0; e < first.ElementCount; e++)
            {
                int target = near[Math.Min(joint == Joint.Merged ? Depth(first, e) : 0, near.Count - 1)];

                if (
                    !Meet(
                        first.ElementPoint(firstSegment, e),
                        firstWay,
                        second.ElementPoint(secondSegment, target),
                        secondWay,
                        out double along,
                        out double _
                    )
                )
                {
                    return false;
                }

                stops[e] = first.ElementPoint(firstSegment, e).Add(firstWay.Multiply(along));
            }

            // The first's outer lines, which the open and merged tees break the second's near element between, before the first moves.
            Outer(first, out int firstLeast, out int firstMost);
            double[] opening = null;

            if (joint != Joint.Closed)
            {
                GeoVector3 across = secondWay;
                GeoPoint3 start = second.ElementPoint(secondSegment, nearest);

                if (
                    Meet(
                        start,
                        across,
                        first.ElementPoint(firstSegment, firstLeast),
                        firstWay,
                        out double a,
                        out double _
                    )
                    && Meet(
                        start,
                        across,
                        first.ElementPoint(firstSegment, firstMost),
                        firstWay,
                        out double b,
                        out double _
                    )
                )
                {
                    opening = new[] { Math.Min(a, b), Math.Max(a, b) };
                }
            }

            Stop(first, firstSegment, firstWay, meeting, keepsStart, stops);

            if (opening != null)
            {
                second.SetRuns(
                    secondSegment,
                    nearest,
                    Without(second.RunsOf(secondSegment, nearest), opening[0], opening[1])
                );
            }

            return true;
        }

        /// <summary>
        /// Joins two multilines at a corner: each trimmed or extended to where their lines meet, on the side of the meeting its
        /// pick was, the elements of one stopping at those of the other that meet them in a mitre, matched by rank from one side
        /// of the bend to the other.
        /// </summary>
        /// <returns>False where the lines are parallel, or either multiline is closed.</returns>
        internal static bool Corner(
            DxfMlineBuilder first,
            int firstSegment,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            int secondSegment,
            GeoPoint3 secondPick
        )
        {
            if (first.IsClosed || second.IsClosed || first.ElementCount != second.ElementCount)
            {
                return false;
            }

            GeoVector3 firstWay = first.SegmentDirection(firstSegment);
            GeoVector3 secondWay = second.SegmentDirection(secondSegment);

            if (
                !Meet(
                    first.Points[firstSegment],
                    firstWay,
                    second.Points[secondSegment],
                    secondWay,
                    out double onFirst,
                    out double onSecond
                )
            )
            {
                return false;
            }

            GeoPoint3 meeting = first.Points[firstSegment].Add(firstWay.Multiply(onFirst));
            bool firstKeepsStart = first.Points[firstSegment].GetVectorTo(firstPick).DotProduct(firstWay) < onFirst;
            bool secondKeepsStart =
                second.Points[secondSegment].GetVectorTo(secondPick).DotProduct(secondWay) < onSecond;

            // Walking from the first's kept part to the meeting and on along the second's, the offset of each element to the left.
            double firstSign = firstKeepsStart ? 1.0 : -1.0;
            double secondSign = secondKeepsStart ? -1.0 : 1.0;
            List<int> firstRank = Ranked(first, firstSign);
            List<int> secondRank = Ranked(second, secondSign);
            var firstStops = new GeoPoint3[first.ElementCount];
            var secondStops = new GeoPoint3[second.ElementCount];

            for (int k = 0; k < firstRank.Count; k++)
            {
                int a = firstRank[firstRank.Count - 1 - k];
                int b = secondRank[secondRank.Count - 1 - k];

                if (
                    !Meet(
                        first.ElementPoint(firstSegment, a),
                        firstWay,
                        second.ElementPoint(secondSegment, b),
                        secondWay,
                        out double alongFirst,
                        out double alongSecond
                    )
                )
                {
                    return false;
                }

                firstStops[a] = first.ElementPoint(firstSegment, a).Add(firstWay.Multiply(alongFirst));
                secondStops[b] = second.ElementPoint(secondSegment, b).Add(secondWay.Multiply(alongSecond));
            }

            Stop(first, firstSegment, firstWay, meeting, firstKeepsStart, firstStops);
            Stop(second, secondSegment, secondWay, meeting, secondKeepsStart, secondStops);
            return true;
        }

        /// <summary>
        /// The elements of a multiline in order of their offset to the left of a way of walking along it (1 along the multiline,
        /// -1 against it), the least first, two of one offset in the order of the style.
        /// </summary>
        private static List<int> Ranked(DxfMlineBuilder mline, double sign)
        {
            var order = new List<int>();

            for (int e = 0; e < mline.ElementCount; e++)
            {
                order.Add(e);
            }

            order.Sort(
                (a, b) =>
                {
                    int by = (mline.ElementShift(a) * sign).CompareTo(mline.ElementShift(b) * sign);
                    return by != 0 ? by : a.CompareTo(b);
                }
            );
            return order;
        }

        /// <summary>
        /// How deep an element lies from the outside of its multiline: 0 for the two outer ones, 1 for the next pair in, and the
        /// middle one of an odd number as deep as the pairs.
        /// </summary>
        private static int Depth(DxfMlineBuilder mline, int element)
        {
            List<int> order = Ranked(mline, 1.0);
            int at = order.IndexOf(element);
            return Math.Min(at, order.Count - 1 - at);
        }

        /// <summary>
        /// Ends a multiline at a point: kept from its start to the segment, or from the segment to its end; then each element
        /// stopped at the point given for it, its stretches broken there, or run on past the end it stops at (a start before the
        /// first vertex at the start).
        /// </summary>
        private static void Stop(
            DxfMlineBuilder mline,
            int segment,
            GeoVector3 way,
            GeoPoint3 meeting,
            bool keepsStart,
            GeoPoint3[] stops
        )
        {
            if (keepsStart)
            {
                mline.KeepStart(segment, meeting);
            }
            else
            {
                mline.KeepEnd(segment, meeting);
            }

            int at = keepsStart ? segment : 0;

            for (int e = 0; e < mline.ElementCount; e++)
            {
                GeoPoint3 start = mline.ElementPoint(at, e);
                double stop = start.GetVectorTo(stops[e]).DotProduct(way);

                if (keepsStart)
                {
                    double length = start.GetVectorTo(mline.ElementPoint(at + 1, e)).DotProduct(way);

                    if (stop < length - Tiny)
                    {
                        mline.SetRuns(at, e, Without(mline.RunsOf(at, e), stop, double.PositiveInfinity));
                    }
                    else if (stop > length + Tiny)
                    {
                        mline.SetEndExtension(e, stop - length);
                    }
                }
                else
                {
                    List<DxfMlineBuilder.Run> runs = Without(mline.RunsOf(at, e), double.NegativeInfinity, stop);

                    if (stop < -Tiny)
                    {
                        runs.Add(new DxfMlineBuilder.Run(stop, 0.0));
                    }

                    mline.SetRuns(at, e, runs);
                }
            }
        }

        /// <summary>Puts a vertex on the line of a multiline at the point nearest the pick, on the segment nearest it; false where that is one of its vertices.</summary>
        internal static bool AddVertex(DxfMlineBuilder mline, GeoPoint3 pick)
        {
            Nearest(mline, pick, out int segment, out int _);
            GeoPoint3 from = mline.Points[segment];
            GeoVector3 way = mline.SegmentDirection(segment);
            double along = Math.Max(0.0, Math.Min(Length(mline, segment), from.GetVectorTo(pick).DotProduct(way)));
            GeoPoint3 point = from.Add(way.Multiply(along));
            double tolerance = Tolerance.Default.EqualPoint;

            if (
                point.DistanceTo(from) <= tolerance
                || point.DistanceTo(mline.Points[Next(mline, segment)]) <= tolerance
            )
            {
                return false;
            }

            mline.InsertVertex(segment + 1, point);
            return true;
        }

        /// <summary>Takes out the vertex nearest the pick, where the multiline keeps two points (three where closed); false where it cannot.</summary>
        internal static bool DeleteVertex(DxfMlineBuilder mline, GeoPoint3 pick)
        {
            if (mline.Points.Count <= (mline.IsClosed ? 3 : 2))
            {
                return false;
            }

            int nearest = 0;
            double best = double.MaxValue;

            for (int i = 0; i < mline.Points.Count; i++)
            {
                double distance = mline.Points[i].DistanceTo(pick);

                if (distance < best - Tiny)
                {
                    best = distance;
                    nearest = i;
                }
            }

            mline.RemoveVertex(nearest);
            return true;
        }

        /// <summary>
        /// Cuts one element, or every element, of the segment the first pick falls on between the two picks, measured along each
        /// element; or welds what is cut between them back (the stretches there made whole).
        /// </summary>
        internal static void Cut(
            DxfMlineBuilder mline,
            int segment,
            int element,
            bool allElements,
            GeoPoint3 firstPick,
            GeoPoint3 secondPick,
            bool weld
        )
        {
            GeoVector3 way = mline.SegmentDirection(segment);

            for (int e = allElements ? 0 : element; e < (allElements ? mline.ElementCount : element + 1); e++)
            {
                GeoPoint3 start = mline.ElementPoint(segment, e);
                double a = start.GetVectorTo(firstPick).DotProduct(way);
                double b = start.GetVectorTo(secondPick).DotProduct(way);
                double length = start.GetVectorTo(mline.ElementPoint(Next(mline, segment), e)).DotProduct(way);
                double from = Math.Max(0.0, Math.Min(a, b));
                double to = Math.Min(length, Math.Max(a, b));
                List<DxfMlineBuilder.Run> runs = mline.RunsOf(segment, e);

                if (weld)
                {
                    runs.Add(new DxfMlineBuilder.Run(from, to));
                    mline.SetRuns(segment, e, runs);
                }
                else
                {
                    mline.SetRuns(segment, e, Without(runs, from, to));
                }
            }
        }
    }
}
