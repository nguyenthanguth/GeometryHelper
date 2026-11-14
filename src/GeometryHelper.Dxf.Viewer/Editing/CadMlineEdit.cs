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

            Outer(first, out int firstLeast, out int firstMost);
            List<int> secondOuter = Outer(second, out int secondLeast, out int secondMost);
            var all = new List<int>();

            for (int e = 0; e < first.ElementCount; e++)
            {
                all.Add(e);
            }

            if (joint == Joint.Merged)
            {
                // Each pair of elements broken across the pair of the other as deep in from its outside, as AutoCAD 2021's
                // merged cross of four elements breaks the inner pair of each across the inner pair of the other; a middle
                // element, or a pair the other has none as deep of, runs through.
                BreakPairs(first, firstSegment, second, secondSegment);
                BreakPairs(second, secondSegment, first, firstSegment);
                return true;
            }

            Break(first, firstSegment, all, second, secondSegment, secondLeast, secondMost);

            if (joint != Joint.Closed)
            {
                Break(second, secondSegment, secondOuter, first, firstSegment, firstLeast, firstMost);
            }

            return true;
        }

        /// <summary>Breaks each element of a multiline across the pair of elements of another as deep in from its outside, where it has one.</summary>
        private static void BreakPairs(DxfMlineBuilder target, int segment, DxfMlineBuilder by, int bySegment)
        {
            for (int e = 0; e < target.ElementCount; e++)
            {
                if (Pair(by, Depth(target, e), out int least, out int most))
                {
                    Break(target, segment, new[] { e }, by, bySegment, least, most);
                }
            }
        }

        /// <summary>The two elements of a multiline as deep in from its outside, the least offset first; false where there is no such pair.</summary>
        private static bool Pair(DxfMlineBuilder mline, int depth, out int least, out int most)
        {
            List<int> order = Ranked(mline, 1.0);
            least = -1;
            most = -1;

            if (depth >= order.Count - 1 - depth)
            {
                return false;
            }

            least = order[depth];
            most = order[order.Count - 1 - depth];
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
        /// merged tees also break the second's near outer element across the first's width. An element of the first that meets
        /// the second's element before its start or after its end, where another segment goes on from there (the closed
        /// multiline's corners, the open one's inner vertices), is left as it was and the second is left whole, as AutoCAD
        /// 2021 leaves them with "Invalid operation on a self intersecting multiline.": the elements before it are stopped.
        /// </summary>
        /// <param name="first">The multiline that is trimmed or extended.</param>
        /// <param name="firstSegment">The segment of the first the pick was on.</param>
        /// <param name="firstPick">Where the first was picked.</param>
        /// <param name="second">The multiline that is met.</param>
        /// <param name="secondSegment">The segment of the second the pick was on.</param>
        /// <param name="joint">Closed, open or merged.</param>
        /// <param name="selfIntersecting">True where an element of the first meets the second past a corner, as above.</param>
        /// <returns>False where the lines do not meet within the second's segment, or the first is closed.</returns>
        internal static bool Tee(
            DxfMlineBuilder first,
            int firstSegment,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            int secondSegment,
            Joint joint,
            out bool selfIntersecting
        )
        {
            selfIntersecting = false;

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
            var stops = new GeoPoint3[first.ElementCount];
            var passes = new bool[first.ElementCount];

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
                        out double onTarget
                    )
                )
                {
                    return false;
                }

                stops[e] = first.ElementPoint(firstSegment, e).Add(firstWay.Multiply(along));
                passes[e] = Within(second, secondSegment, target, onTarget);
                selfIntersecting |= !passes[e];
            }

            // The first's pairs of lines, which the open and merged tees break the second's near elements between, before the
            // first moves: the open tee the nearest across the outer pair; the merged tee, as AutoCAD 2021's of four elements,
            // each of the near half across the pair of the first as deep in, where the first has one.
            int depths =
                selfIntersecting ? 0
                : joint == Joint.Merged ? near.Count / 2
                : joint == Joint.Open ? 1
                : 0;
            var openings = new List<KeyValuePair<int, double[]>>();

            for (int k = 0; k < depths; k++)
            {
                if (!Pair(first, k, out int firstLeast, out int firstMost))
                {
                    break;
                }

                GeoVector3 across = secondWay;
                GeoPoint3 start = second.ElementPoint(secondSegment, near[k]);

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
                    openings.Add(new KeyValuePair<int, double[]>(near[k], new[] { Math.Min(a, b), Math.Max(a, b) }));
                }
            }

            Stop(first, firstSegment, firstWay, meeting, keepsStart, stops, passes);

            foreach (KeyValuePair<int, double[]> opening in openings)
            {
                second.SetRuns(
                    secondSegment,
                    opening.Key,
                    Without(second.RunsOf(secondSegment, opening.Key), opening.Value[0], opening.Value[1])
                );
            }

            return true;
        }

        /// <summary>
        /// Whether a point along an element of a multiline's segment is on the element: from its start to its end, past either
        /// of which another segment goes on where the multiline is closed or the vertex is not its first or last.
        /// </summary>
        private static bool Within(DxfMlineBuilder mline, int segment, int element, double along)
        {
            double length = mline
                .ElementPoint(segment, element)
                .GetVectorTo(mline.ElementPoint(Next(mline, segment), element))
                .DotProduct(mline.SegmentDirection(segment));
            bool goesOnBefore = mline.IsClosed || segment > 0;
            bool goesOnAfter = mline.IsClosed || segment < mline.SegmentCount - 1;
            return (!goesOnBefore || along >= -Tiny) && (!goesOnAfter || along <= length + Tiny);
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
        /// first vertex at the start). An element the caller names in <paramref name="passes"/> as false is left as it was, drawn
        /// whole to the end it would have stopped at.
        /// </summary>
        private static void Stop(
            DxfMlineBuilder mline,
            int segment,
            GeoVector3 way,
            GeoPoint3 meeting,
            bool keepsStart,
            GeoPoint3[] stops,
            bool[] passes = null
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
                if (passes != null && !passes[e])
                {
                    if (keepsStart)
                    {
                        mline.SetRuns(at + 1, e, new[] { new DxfMlineBuilder.Run(0.0, double.PositiveInfinity) });
                    }
                    else
                    {
                        List<DxfMlineBuilder.Run> kept = mline.RunsOf(at, e);

                        if (kept.Count > 0 && double.IsNegativeInfinity(kept[0].From))
                        {
                            kept[0] = new DxfMlineBuilder.Run(0.0, kept[0].To);
                        }

                        mline.SetRuns(at, e, kept);
                    }

                    continue;
                }

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
        /// Welds an element's stretches along a segment as AutoCAD 2021 does: it takes out every start and stop that lies between
        /// two places, one by one, not a cut and its two ends together, so that a cut the range only half covers is left turned
        /// inside out (a stop gone and its start left, the element drawn only before it). A segment between the picks is made
        /// whole, and so is one every start and stop of which went.
        /// </summary>
        private static void Weld(
            DxfMlineBuilder mline,
            int segment,
            int element,
            double from,
            double to,
            bool fromExcluded,
            bool whole
        )
        {
            const double Near = 1E-6;
            var entries = new List<double>();

            foreach (DxfMlineBuilder.Run run in mline.RunsOf(segment, element))
            {
                entries.Add(run.From);

                if (!double.IsPositiveInfinity(run.To))
                {
                    entries.Add(run.To);
                }
            }

            entries.RemoveAll(v => whole || ((fromExcluded ? v > from + Near : v >= from - Near) && v <= to + Near));
            var runs = new List<DxfMlineBuilder.Run>();

            for (int i = 0; i < entries.Count; i += 2)
            {
                runs.Add(
                    new DxfMlineBuilder.Run(
                        entries[i],
                        i + 1 < entries.Count ? entries[i + 1] : double.PositiveInfinity
                    )
                );
            }

            if (runs.Count == 0)
            {
                runs.Add(new DxfMlineBuilder.Run(0.0, double.PositiveInfinity));
            }

            mline.SetRuns(segment, element, runs);
        }

        /// <summary>
        /// Cuts one element, or every element, between the two picks, measured along each element; or welds what is cut between
        /// them back (the stretches there made whole). Where the picks fall on two segments, as AutoCAD 2021 cuts it: from the
        /// first pick to the end of its segment, the segments between whole, and the second pick's segment up to the pick,
        /// going on from the first segment (round past the first vertex of a closed multiline, where AutoCAD starts the cut on
        /// the first segment at the vertex rather than at the element's start); the picks of an open one taken in its order.
        /// </summary>
        internal static void Cut(
            DxfMlineBuilder mline,
            int segment,
            int element,
            bool allElements,
            GeoPoint3 firstPick,
            int secondSegment,
            GeoPoint3 secondPick,
            bool weld
        )
        {
            if (!mline.IsClosed && secondSegment < segment)
            {
                int swap = segment;
                segment = secondSegment;
                secondSegment = swap;
                GeoPoint3 other = firstPick;
                firstPick = secondPick;
                secondPick = other;
            }

            var sequence = new List<int> { segment };

            for (int s = segment; s != secondSegment; )
            {
                s = Next(mline, s) % mline.SegmentCount;
                sequence.Add(s);
            }

            for (int e = allElements ? 0 : element; e < (allElements ? mline.ElementCount : element + 1); e++)
            {
                for (int k = 0; k < sequence.Count; k++)
                {
                    int s = sequence[k];
                    GeoVector3 way = mline.SegmentDirection(s);
                    GeoPoint3 start = mline.ElementPoint(s, e);
                    double length = start.GetVectorTo(mline.ElementPoint(Next(mline, s), e)).DotProduct(way);
                    double from;
                    double to;

                    // Where the pick range lies along this segment's element, before it is held to the element: what a weld takes
                    // the stops and starts of.
                    double reachFrom;
                    double reachTo;

                    if (sequence.Count == 1)
                    {
                        double a = start.GetVectorTo(firstPick).DotProduct(way);
                        double b = start.GetVectorTo(secondPick).DotProduct(way);
                        from = Math.Max(0.0, Math.Min(a, b));
                        to = Math.Min(length, Math.Max(a, b));
                        reachFrom = Math.Min(a, b);
                        reachTo = Math.Max(a, b);
                    }
                    else if (k == 0)
                    {
                        reachFrom = start.GetVectorTo(firstPick).DotProduct(way);
                        from = Math.Max(0.0, reachFrom);
                        to = double.PositiveInfinity;
                        reachTo = double.PositiveInfinity;
                    }
                    else if (k == sequence.Count - 1)
                    {
                        // Round past the first vertex AutoCAD 2021 starts the cut at the vertex, not at the element's start.
                        from =
                            s == 0 && mline.IsClosed
                                ? start.GetVectorTo(mline.Points[0]).DotProduct(way)
                                : double.NegativeInfinity;
                        reachTo = start.GetVectorTo(secondPick).DotProduct(way);
                        to = Math.Min(length, reachTo);
                        reachFrom = from;
                    }
                    else
                    {
                        from = double.NegativeInfinity;
                        to = double.PositiveInfinity;
                        reachFrom = double.NegativeInfinity;
                        reachTo = double.PositiveInfinity;
                    }

                    if (weld)
                    {
                        Weld(
                            mline,
                            s,
                            e,
                            reachFrom,
                            reachTo,
                            // The vertex a cut started at is not itself welded.
                            sequence.Count > 1
                                && k == sequence.Count - 1
                                && s == 0
                                && mline.IsClosed,
                            sequence.Count > 2 && k > 0 && k < sequence.Count - 1
                        );
                    }
                    else
                    {
                        mline.SetRuns(s, e, Without(mline.RunsOf(s, e), from, to));
                    }
                }
            }
        }
    }
}
