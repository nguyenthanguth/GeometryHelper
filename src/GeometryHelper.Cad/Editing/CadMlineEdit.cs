using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
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
                if (run.To <= from + Tiny || run.From >= to - Tiny)
                {
                    left.Add(run);
                    continue;
                }

                if (run.From < from - Tiny)
                {
                    left.Add(new DxfMlineBuilder.Run(run.From, from));
                }

                if (run.To > to + Tiny)
                {
                    left.Add(new DxfMlineBuilder.Run(to, run.To));
                }
            }

            return left;
        }

        /// <summary>How the search for the two segments that two multilines are crossed or joined at ended.</summary>
        internal enum PairKind
        {
            /// <summary>A pair of segments whose lines meet on both, or past the free end of an open multiline.</summary>
            Found,

            /// <summary>
            /// No such pair, but the lines of one meet past a vertex of a multiline's first or last segment while the
            /// other runs on past its free end: AutoCAD 2021 refuses it without saying the lines miss.
            /// </summary>
            Refused,

            /// <summary>No pair of segments whose lines meet where both can be run to.</summary>
            NoMeeting,
        }

        /// <summary>
        /// Finds the pair of segments two multilines are crossed or joined at, as AutoCAD 2021 does: not the segments the picks lie
        /// on, but of every pair of segments whose lines meet on both (or past the start of a multiline's first segment or
        /// the end of its last: a closed multiline is taken as the run of its segments from the first to the last, as an open one
        /// is) the one whose meeting is nearest the two picks along the multilines, the distance along the first from its pick and
        /// along the second from its pick added (a tie goes to the later pair, and to the earlier where either multiline is
        /// closed). A closed multiline's distance along itself counts from its start the shorter way round (see
        /// <see cref="Folded"/>): the rectangle (0, 0)-(200, 0)-(200, 100)-(0, 100) has the crossing of its left edge at (0, 50)
        /// 550 along its 600, counted as 50. Two segments in a pair whose lines run parallel are not a pair.
        /// </summary>
        /// <param name="first">The first multiline.</param>
        /// <param name="firstPick">Where the first was picked.</param>
        /// <param name="second">The second multiline.</param>
        /// <param name="secondPick">Where the second was picked.</param>
        /// <param name="firstSegment">The segment of the first the pair has.</param>
        /// <param name="secondSegment">The segment of the second the pair has.</param>
        internal static PairKind FindPair(
            DxfMlineBuilder first,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            GeoPoint3 secondPick,
            out int firstSegment,
            out int secondSegment
        )
        {
            firstSegment = 0;
            secondSegment = 0;
            double firstArc = Folded(first, ArcAt(first, firstPick));
            double secondArc = Folded(second, ArcAt(second, secondPick));
            bool found = false;
            bool refused = false;
            double best = 0.0;

            for (int i = 0; i < first.SegmentCount; i++)
            {
                for (int j = 0; j < second.SegmentCount; j++)
                {
                    if (
                        !Meet(
                            first.Points[i],
                            first.SegmentDirection(i),
                            second.Points[j],
                            second.SegmentDirection(j),
                            out double onFirst,
                            out double onSecond
                        )
                    )
                    {
                        continue;
                    }

                    bool firstOn = OnRun(first, i, onFirst);
                    bool secondOn = OnRun(second, j, onSecond);

                    if (firstOn && secondOn)
                    {
                        double score =
                            Math.Abs(firstArc - Folded(first, ArcStart(first, i) + onFirst))
                            + Math.Abs(secondArc - Folded(second, ArcStart(second, j) + onSecond));

                        if (!found || (first.IsClosed || second.IsClosed ? score < best - Tiny : score <= best + Tiny))
                        {
                            found = true;
                            best = score;
                            firstSegment = i;
                            secondSegment = j;
                        }
                    }
                    else if (firstOn != secondOn)
                    {
                        // One line meets the other past a vertex of its segment while the other segment is run on past a free end.
                        refused |= secondOn
                            ? EndOfRun(first, i) && Beyond(second, j, onSecond)
                            : EndOfRun(second, j) && Beyond(first, i, onFirst);
                    }
                }
            }

            return found ? PairKind.Found
                : refused ? PairKind.Refused
                : PairKind.NoMeeting;
        }

        /// <summary>
        /// Finds the pair of segments a tee is made at, as AutoCAD 2021 does (not the ones the picks lie on): of every pair of
        /// segments whose lines meet, with the stem met on a segment or run on past the free end of its first or last, and the
        /// second met on a segment or run on past its own free end, the one whose meeting is nearest the picks along the
        /// multilines, the distance along the stem from its pick and along the second from its pick added (a closed second's
        /// distance along itself counted as <see cref="Folded"/> counts it). Of two pairs as
        /// near, where one meets the second on a segment and the other past its free end the one on the earlier segment of
        /// the second wins, else the one whose stem segment is farther from the stem's pick, then the one nearer along the
        /// stem, then the earlier (356 ties probed, 334 so). That pair alone says whether the tee is made: it is <see cref="PairKind.Found"/> where the
        /// second is met on a segment (the end of its last is not on it) and <see cref="PairKind.NoMeeting"/> where it is met
        /// past its free end. Where there is no such pair, the tee is <see cref="PairKind.Refused"/> if any pair has the stem
        /// run on past its free end to a second met past a vertex of its own, on its first or last segment, or has the stem
        /// met past a vertex of its own and the second past its free end, and <see cref="PairKind.NoMeeting"/> otherwise.
        /// </summary>
        /// <param name="first">The stem.</param>
        /// <param name="firstPick">Where the stem was picked.</param>
        /// <param name="second">The multiline the stem meets.</param>
        /// <param name="secondPick">Where the second was picked.</param>
        /// <param name="firstSegment">The segment of the stem the pair has.</param>
        /// <param name="secondSegment">The segment of the second the pair has.</param>
        internal static PairKind FindTeePair(
            DxfMlineBuilder first,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            GeoPoint3 secondPick,
            out int firstSegment,
            out int secondSegment
        )
        {
            firstSegment = 0;
            secondSegment = 0;
            double firstArc = ArcAt(first, firstPick);
            double secondArc = Folded(second, ArcAt(second, secondPick));
            bool found = false;
            bool refused = false;
            bool bestOn = false;
            double best = 0.0;
            double bestGap = 0.0;
            double bestAlong = 0.0;

            for (int i = 0; i < first.SegmentCount; i++)
            {
                for (int j = 0; j < second.SegmentCount; j++)
                {
                    if (
                        !Meet(
                            first.Points[i],
                            first.SegmentDirection(i),
                            second.Points[j],
                            second.SegmentDirection(j),
                            out double onFirst,
                            out double onSecond
                        )
                    )
                    {
                        continue;
                    }

                    bool firstOn = OnSegmentOrItsOpenEnd(first, i, onFirst);
                    // The end of a free end's segment is not on it; the end that another segment goes on from is.
                    bool secondOn =
                        onSecond >= -Tiny
                        && (
                            onSecond < Length(second, j) - Tiny
                            || (
                                onSecond <= Length(second, j) + Tiny && (second.IsClosed || j < second.SegmentCount - 1)
                            )
                        );

                    bool secondFree = !secondOn && !EndPast(second, j, onSecond);

                    if (firstOn && (secondOn || secondFree))
                    {
                        double alongStem = Math.Abs(firstArc - (ArcStart(first, i) + onFirst));
                        double score = alongStem + Math.Abs(secondArc - Folded(second, ArcStart(second, j) + onSecond));
                        double pickGap = DistanceToSegment(firstPick, first.Points[i], first.Points[Next(first, i)]);

                        // Two pairs as near as each other: where one meets the second on a segment and the other past its free
                        // end, the one on the earlier segment of the second; else the one whose stem segment is farther from
                        // the stem's pick, then the nearer along the stem, then the earlier.
                        bool better =
                            !found
                            || score < best - Tiny
                            || (
                                score <= best + Tiny
                                && (
                                    secondOn != bestOn ? j < secondSegment
                                    : Math.Abs(pickGap - bestGap) > Tiny ? pickGap > bestGap
                                    : alongStem < bestAlong - Tiny
                                )
                            );

                        if (better)
                        {
                            found = true;
                            best = score;
                            bestOn = secondOn;
                            bestGap = pickGap;
                            bestAlong = alongStem;
                            firstSegment = i;
                            secondSegment = j;
                        }
                    }
                    else
                    {
                        bool firstExt = firstOn && Beyond(first, i, onFirst);
                        refused |=
                            (firstExt && !secondOn && !secondFree && EndLike(second, j))
                            || (
                                !firstOn
                                && secondFree
                                && (onSecond < -Tiny || onSecond > Length(second, j) + Tiny)
                                && EndLike(first, i)
                            );
                    }
                }
            }

            return found ? (bestOn ? PairKind.Found : PairKind.NoMeeting)
                : refused ? PairKind.Refused
                : PairKind.NoMeeting;
        }

        /// <summary>Whether a point along a segment's line is past a vertex that another segment of the multiline goes on from (not past a free end).</summary>
        private static bool EndPast(DxfMlineBuilder mline, int segment, double along) =>
            (along < -Tiny && (mline.IsClosed || segment > 0))
            || (along > Length(mline, segment) + Tiny && (mline.IsClosed || segment < mline.SegmentCount - 1));

        /// <summary>Whether a segment is the first or last of an open multiline, or any segment of a closed one.</summary>
        private static bool EndLike(DxfMlineBuilder mline, int segment) =>
            mline.IsClosed || segment == 0 || segment == mline.SegmentCount - 1;

        /// <summary>Whether a point along a segment's line is before its start or past its end.</summary>
        private static bool Beyond(DxfMlineBuilder mline, int segment, double along) =>
            along < -Tiny || along > Length(mline, segment) + Tiny;

        /// <summary>The distance along a multiline's line from its start to the start of a segment.</summary>
        private static double ArcStart(DxfMlineBuilder mline, int segment)
        {
            double arc = 0.0;

            for (int s = 0; s < segment; s++)
            {
                arc += Length(mline, s);
            }

            return arc;
        }

        /// <summary>
        /// A distance along a multiline's line from its start as <see cref="FindPair"/> and <see cref="FindTeePair"/> reckon it: on a closed multiline the
        /// shorter way round to the start, so a point on its last segment lies near the start however long the multiline is.
        /// </summary>
        private static double Folded(DxfMlineBuilder mline, double arc) =>
            mline.IsClosed ? Math.Min(arc, ArcStart(mline, mline.SegmentCount) - arc) : arc;

        /// <summary>The distance along a multiline's line from its start to the point of the line nearest a pick.</summary>
        private static double ArcAt(DxfMlineBuilder mline, GeoPoint3 pick)
        {
            Nearest(mline, pick, out int segment, out int _);
            double along = mline.Points[segment].GetVectorTo(pick).DotProduct(mline.SegmentDirection(segment));
            return ArcStart(mline, segment) + Math.Max(0.0, Math.Min(Length(mline, segment), along));
        }

        /// <summary>
        /// Crosses two multilines at a pair of segments <see cref="FindPair"/> found. Each element of the target named is broken
        /// from where it meets the one outer element of the other multiline to where it meets the other, as AutoCAD's closed,
        /// open and merged crosses break them: on the segment of the element's own line the meeting is nearest the picks on
        /// (see <see cref="ElementSegment"/>), one element after another, stopping at the first that cannot be broken.
        /// </summary>
        /// <param name="first">The multiline whose elements are all broken.</param>
        /// <param name="firstSegment">The segment of the first that is crossed.</param>
        /// <param name="firstPick">Where the first was picked.</param>
        /// <param name="second">The multiline that crosses it.</param>
        /// <param name="secondSegment">The segment of the second that crosses it.</param>
        /// <param name="secondPick">Where the second was picked.</param>
        /// <param name="joint">Closed, open or merged.</param>
        /// <param name="selfIntersecting">True where an element breaks past the end of an element where another segment goes on
        /// (and that one is the first or last segment of an open multiline, or the multiline is closed), as AutoCAD 2021 refuses
        /// with "Invalid operation on a self intersecting multiline." leaving the elements before it broken and the rest as they were.</param>
        internal static void Cross(
            DxfMlineBuilder first,
            int firstSegment,
            GeoPoint3 firstPick,
            DxfMlineBuilder second,
            int secondSegment,
            GeoPoint3 secondPick,
            Joint joint,
            out bool selfIntersecting
        )
        {
            selfIntersecting = false;
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
                BreakPairs(first, firstSegment, firstPick, second, secondSegment, secondPick, ref selfIntersecting);

                if (!selfIntersecting)
                {
                    BreakPairs(second, secondSegment, secondPick, first, firstSegment, firstPick, ref selfIntersecting);
                }

                return;
            }

            foreach (int e in all)
            {
                int at = ElementSegment(
                    first,
                    e,
                    firstPick,
                    second,
                    secondSegment,
                    secondPick,
                    firstSegment,
                    secondLeast,
                    secondMost
                );
                Break(first, at, new[] { e }, second, secondSegment, secondLeast, secondMost, ref selfIntersecting);

                if (selfIntersecting)
                {
                    return;
                }
            }

            if (joint != Joint.Closed)
            {
                Break(
                    second,
                    secondSegment,
                    secondOuter,
                    first,
                    firstSegment,
                    firstLeast,
                    firstMost,
                    ref selfIntersecting
                );
            }
        }

        /// <summary>
        /// Whether the meeting of the lines of two segments is on a segment of a multiline taken as a run of segments from the
        /// first to the last (a closed multiline too: AutoCAD 2021 runs its first segment on past its start and its last past its
        /// end, and not the others): from its start to its end, or past the start of the first segment or the end of the last.
        /// </summary>
        private static bool OnRun(DxfMlineBuilder mline, int segment, double along) =>
            (along >= -Tiny || segment == 0)
            && (along <= Length(mline, segment) + Tiny || segment == mline.SegmentCount - 1);

        /// <summary>Whether a segment is the first or the last of a multiline, closed or not.</summary>
        private static bool EndOfRun(DxfMlineBuilder mline, int segment) =>
            segment == 0 || segment == mline.SegmentCount - 1;

        /// <summary>
        /// Whether the meeting of the lines of two segments is on a segment of a multiline: from its start to its end, or past
        /// the start of the first segment or the end of the last of an open multiline, which AutoCAD 2021 runs on.
        /// </summary>
        private static bool OnSegmentOrItsOpenEnd(DxfMlineBuilder mline, int segment, double along) =>
            (along >= -Tiny || (!mline.IsClosed && segment == 0))
            && (along <= Length(mline, segment) + Tiny || (!mline.IsClosed && segment == mline.SegmentCount - 1));

        /// <summary>Breaks each element of a multiline across the pair of elements of another as deep in from its outside, where it has one.</summary>
        private static void BreakPairs(
            DxfMlineBuilder target,
            int segment,
            GeoPoint3 targetPick,
            DxfMlineBuilder by,
            int bySegment,
            GeoPoint3 byPick,
            ref bool selfIntersecting
        )
        {
            for (int e = 0; e < target.ElementCount; e++)
            {
                if (Pair(by, Depth(target, e), out int least, out int most))
                {
                    int at = ElementSegment(target, e, targetPick, by, bySegment, byPick, segment, least, most);
                    Break(target, at, new[] { e }, by, bySegment, least, most, ref selfIntersecting);

                    if (selfIntersecting)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>Whether the break of an element across the two lines of another multiline lies on the segment, not before its start or past its end where another segment goes on.</summary>
        private static bool BreaksWithin(
            DxfMlineBuilder mline,
            int segment,
            int element,
            DxfMlineBuilder by,
            int bySegment,
            int least,
            int most
        )
        {
            GeoPoint3 start = mline.ElementPoint(segment, element);
            GeoVector3 way = mline.SegmentDirection(segment);
            GeoVector3 byWay = by.SegmentDirection(bySegment);

            if (
                !Meet(start, way, by.ElementPoint(bySegment, least), byWay, out double a, out double _)
                || !Meet(start, way, by.ElementPoint(bySegment, most), byWay, out double b, out double _)
            )
            {
                return true;
            }

            double length = start.GetVectorTo(mline.ElementPoint(Next(mline, segment), element)).DotProduct(way);
            bool goesOnBefore = mline.IsClosed || segment > 0;
            bool goesOnAfter = mline.IsClosed || segment < mline.SegmentCount - 1;
            return !(goesOnBefore && Math.Min(a, b) < -Tiny) && !(goesOnAfter && Math.Max(a, b) > length + Tiny);
        }

        /// <summary>
        /// The segment an element of a multiline is broken on where another multiline crosses it: AutoCAD 2021 looks for the
        /// pair of segments on the line of the element itself, not the line the multiline is drawn about, so that near a
        /// corner (within the width of the multiline) the elements of one crossing are broken on different segments. Of the
        /// segments of the element whose line meets the other multiline's segment where both can be run to (as
        /// <see cref="FindPair"/> counts it), the one whose meeting is nearest the two picks along the element and the other
        /// multiline, the later where two are as near; the segment of the multiline's pair where none is, and where the
        /// break of the element on that segment lies wholly on it.
        /// </summary>
        private static int ElementSegment(
            DxfMlineBuilder mline,
            int element,
            GeoPoint3 pick,
            DxfMlineBuilder by,
            int bySegment,
            GeoPoint3 byPick,
            int pairSegment,
            int least,
            int most
        )
        {
            int count = mline.SegmentCount;

            if (count == 1 || BreaksWithin(mline, pairSegment, element, by, bySegment, least, most))
            {
                return pairSegment;
            }

            var starts = new GeoPoint3[count + 1];
            var lengths = new double[count];

            for (int k = 0; k < count; k++)
            {
                starts[k] = mline.ElementPoint(k, element);
                starts[k + 1] = mline.ElementPoint(Next(mline, k), element);
                lengths[k] = starts[k].GetVectorTo(starts[k + 1]).DotProduct(mline.SegmentDirection(k));
            }

            // The distance along the element to the point of it nearest the pick.
            double pickArc = 0.0;
            double nearest = double.MaxValue;
            double arc = 0.0;

            for (int k = 0; k < count; k++)
            {
                GeoVector3 way = mline.SegmentDirection(k);
                double along = Math.Max(0.0, Math.Min(lengths[k], starts[k].GetVectorTo(pick).DotProduct(way)));
                double distance = pick.DistanceTo(starts[k].Add(way.Multiply(along)));

                if (distance < nearest - Tiny)
                {
                    nearest = distance;
                    pickArc = arc + along;
                }

                arc += lengths[k];
            }

            double byArc = ArcAt(by, byPick);
            int best = pairSegment;
            double bestScore = 0.0;
            bool found = false;
            arc = 0.0;

            for (int k = 0; k < count; k++)
            {
                if (
                    Meet(
                        starts[k],
                        mline.SegmentDirection(k),
                        by.Points[bySegment],
                        by.SegmentDirection(bySegment),
                        out double onElement,
                        out double onBy
                    )
                    && (onElement >= -Tiny || (!mline.IsClosed && k == 0))
                    && (onElement <= lengths[k] + Tiny || (!mline.IsClosed && k == count - 1))
                    && OnSegmentOrItsOpenEnd(by, bySegment, onBy)
                )
                {
                    double score =
                        Math.Abs(pickArc - (arc + onElement)) + Math.Abs(byArc - (ArcStart(by, bySegment) + onBy));

                    if (!found || score <= bestScore + Tiny)
                    {
                        found = true;
                        bestScore = score;
                        best = k;
                    }
                }

                arc += lengths[k];
            }

            return best;
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
            int most,
            ref bool selfIntersecting
        )
        {
            GeoVector3 way = target.SegmentDirection(segment);
            GeoVector3 byWay = by.SegmentDirection(bySegment);

            foreach (int e in elements)
            {
                GeoPoint3 start = target.ElementPoint(segment, e);

                if (
                    !Meet(start, way, by.ElementPoint(bySegment, least), byWay, out double a, out double onLeast)
                    || !Meet(start, way, by.ElementPoint(bySegment, most), byWay, out double b, out double onMost)
                )
                {
                    continue;
                }

                double lengthOfElement = start
                    .GetVectorTo(target.ElementPoint(Next(target, segment), e))
                    .DotProduct(way);
                double from = Math.Min(a, b);
                double to = Math.Max(a, b);

                // Where the break lies before the start or past the end of this element and another segment goes on there,
                // AutoCAD 2021 breaks the element between where it meets the two lines of the other multiline, along the
                // elements' own path, over whole segments: it refuses that where either line meets the element not once, and
                // always where the multiline is closed; so it does where the break meets an element of the other multiline
                // before its start or past its end and another segment goes on there.
                bool goesOnBefore = target.IsClosed || segment > 0;
                bool goesOnAfter = target.IsClosed || segment < target.SegmentCount - 1;
                bool spills = (goesOnBefore && from < -Tiny) || (goesOnAfter && to > lengthOfElement + Tiny);
                bool refused =
                    (spills && (target.IsClosed || !BreakAlong(target, e, by, bySegment, least, most, false)))
                    || !Within(by, bySegment, least, onLeast)
                    || !Within(by, bySegment, most, onMost);

                // It goes element by element and stops at the one it refuses, the ones before it already broken.
                if (refused)
                {
                    selfIntersecting = true;
                    return;
                }

                if (spills)
                {
                    BreakAlong(target, e, by, bySegment, least, most, true);
                    continue;
                }

                // A break wholly past the free end of the last segment (a cross that runs the segment on) takes nothing out of
                // the element. AutoCAD writes it inside out and draws a stray piece there, which is not copied.
                if (!target.IsClosed && segment == target.SegmentCount - 1 && from >= lengthOfElement - Tiny)
                {
                    continue;
                }

                CutAcross(target, segment, e, from, to, lengthOfElement);
            }
        }

        /// <summary>
        /// Breaks an element of an open multiline between where it meets the two lines of an element each of the other multiline
        /// along the element's whole path, as AutoCAD 2021 breaks one a crossing runs across a corner of: the stretches
        /// of the segments it passes whole are cut away, those it starts and ends on from and up to the meeting.
        /// </summary>
        /// <param name="target">The multiline whose element is broken.</param>
        /// <param name="element">The element.</param>
        /// <param name="by">The multiline that crosses it.</param>
        /// <param name="bySegment">The segment of that multiline that crosses it.</param>
        /// <param name="least">The one element of that segment.</param>
        /// <param name="most">The other.</param>
        /// <param name="apply">Whether to cut, or only to say whether each line meets the element once.</param>
        /// <returns>True where each of the two lines meets the element once.</returns>
        private static bool BreakAlong(
            DxfMlineBuilder target,
            int element,
            DxfMlineBuilder by,
            int bySegment,
            int least,
            int most,
            bool apply
        )
        {
            int count = target.SegmentCount;
            GeoVector3 byWay = by.SegmentDirection(bySegment);
            var meetings = new List<KeyValuePair<int, double>>();

            foreach (int line in new[] { least, most })
            {
                GeoPoint3 at = by.ElementPoint(bySegment, line);
                int found = 0;

                for (int k = 0; k < count; k++)
                {
                    GeoPoint3 start = target.ElementPoint(k, element);
                    double length = start
                        .GetVectorTo(target.ElementPoint(Next(target, k), element))
                        .DotProduct(target.SegmentDirection(k));

                    if (
                        Meet(start, target.SegmentDirection(k), at, byWay, out double along, out double _)
                        && along >= -Tiny
                        && (along < length - Tiny || (k == count - 1 && along <= length + Tiny))
                    )
                    {
                        found++;
                        meetings.Add(new KeyValuePair<int, double>(k, along));
                    }
                }

                if (found != 1)
                {
                    return false;
                }
            }

            if (!apply)
            {
                return true;
            }

            meetings.Sort(
                (x, y) =>
                {
                    int by2 = x.Key.CompareTo(y.Key);
                    return by2 != 0 ? by2 : x.Value.CompareTo(y.Value);
                }
            );
            KeyValuePair<int, double> low = meetings[0];
            KeyValuePair<int, double> high = meetings[1];

            if (low.Key == high.Key)
            {
                target.SetRuns(low.Key, element, Without(target.RunsOf(low.Key, element), low.Value, high.Value));
                return true;
            }

            target.SetRuns(
                low.Key,
                element,
                Without(target.RunsOf(low.Key, element), low.Value, double.PositiveInfinity)
            );

            for (int k = low.Key + 1; k < high.Key; k++)
            {
                target.SetRuns(k, element, new DxfMlineBuilder.Run[0]);
            }

            target.SetRuns(
                high.Key,
                element,
                Without(target.RunsOf(high.Key, element), double.NegativeInfinity, high.Value)
            );
            return true;
        }

        /// <summary>
        /// Takes a stretch out of an element along a segment, as AutoCAD writes a break: a stretch left that would start at the
        /// end of the element is nothing to draw, and a break that reaches the end of the last segment of an open multiline
        /// leaves nothing beyond it.
        /// </summary>
        private static void CutAcross(
            DxfMlineBuilder target,
            int segment,
            int element,
            double from,
            double to,
            double lengthOfElement
        )
        {
            List<DxfMlineBuilder.Run> runs = Without(target.RunsOf(segment, element), from, to);
            runs.RemoveAll(r => r.From >= lengthOfElement - Tiny);
            target.SetRuns(segment, element, runs);

            if (
                !target.IsClosed
                && segment == target.SegmentCount - 1
                && to >= lengthOfElement - Tiny
                && from < lengthOfElement - Tiny
            )
            {
                target.SetEndExtension(element, 0.0);
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
            bool keepsStart = ArcAt(first, firstPick) < ArcStart(first, firstSegment) + onFirst;
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
                        out double _
                    )
                )
                {
                    return false;
                }

                passes[e] = ElementHit(first, firstSegment, e, second, secondSegment, target, ref along);
                stops[e] = first.ElementPoint(firstSegment, e).Add(firstWay.Multiply(along));
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
                double lengthOfElement = second
                    .ElementPoint(secondSegment, opening.Key)
                    .GetVectorTo(second.ElementPoint(Next(second, secondSegment), opening.Key))
                    .DotProduct(secondWay);
                CutAcross(second, secondSegment, opening.Key, opening.Value[0], opening.Value[1], lengthOfElement);
            }

            return true;
        }

        /// <summary>
        /// Where an element of a stem meets an element of the second multiline, as AutoCAD 2021 stops it: not only on the
        /// segment of the pair, but on whichever piece of the second's element (the mitred path it is drawn along) the stem's
        /// element line meets, the piece nearest the pair's segment where it meets more than one.
        /// </summary>
        /// <param name="first">The stem.</param>
        /// <param name="segment">The stem's segment of the pair.</param>
        /// <param name="element">The stem's element.</param>
        /// <param name="second">The multiline met.</param>
        /// <param name="pairSegment">The second's segment of the pair.</param>
        /// <param name="target">The second's element the stem's element stops at.</param>
        /// <param name="along">The distance along the stem's element from its point on the segment to where it meets the
        /// target on the segment of the pair; set to where it meets the nearest piece.</param>
        /// <returns>False where the stem's element meets no piece of the target.</returns>
        private static bool ElementHit(
            DxfMlineBuilder first,
            int segment,
            int element,
            DxfMlineBuilder second,
            int pairSegment,
            int target,
            ref double along
        )
        {
            GeoPoint3 start = first.ElementPoint(segment, element);
            GeoVector3 way = first.SegmentDirection(segment);
            int best = -1;
            double bestAlong = along;

            for (int k = 0; k < second.SegmentCount; k++)
            {
                GeoPoint3 from = second.ElementPoint(k, target);
                GeoVector3 across = second.SegmentDirection(k);
                double length = from.GetVectorTo(second.ElementPoint(Next(second, k), target)).DotProduct(across);

                if (
                    Meet(start, way, from, across, out double hit, out double onPiece)
                    && (onPiece >= -Tiny || (!second.IsClosed && k == 0))
                    && (onPiece <= length + Tiny || (!second.IsClosed && k == second.SegmentCount - 1))
                    && (best < 0 || Math.Abs(k - pairSegment) < Math.Abs(best - pairSegment))
                )
                {
                    best = k;
                    bestAlong = hit;
                }
            }

            along = bestAlong;
            return best >= 0;
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

            // A pick on the very point the lines meet at keeps the start, as AutoCAD 2021 does.
            bool firstKeepsStart =
                first.Points[firstSegment].GetVectorTo(firstPick).DotProduct(firstWay) <= onFirst + Tiny;
            bool secondKeepsStart =
                second.Points[secondSegment].GetVectorTo(secondPick).DotProduct(secondWay) <= onSecond + Tiny;

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

        /// <summary>Whether, on a closed multiline, going on from the first pick to the second takes more than half the way round.</summary>
        private static bool GoesTheLongWay(DxfMlineBuilder mline, GeoPoint3 firstPick, GeoPoint3 secondPick)
        {
            double total = ArcStart(mline, mline.SegmentCount);
            double round = ArcAt(mline, secondPick) - ArcAt(mline, firstPick);
            round -= Math.Floor(round / total) * total;
            return round * 2.0 > total + Tiny;
        }

        /// <summary>
        /// Cuts one element, or every element, between the two picks, measured along each element; or welds what is cut between
        /// them back (the stretches there made whole). Where the picks fall on two segments, as AutoCAD 2021 cuts it: from the
        /// first pick to the end of its segment, the segments between whole, and the second pick's segment up to the pick,
        /// going on from the first segment (round past the first vertex of a closed multiline, where AutoCAD starts the cut on
        /// the first segment at the vertex rather than at the element's start); the picks of an open one taken in its order, and of a
        /// closed one the shorter way round from one to the other whichever was picked first.
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
            if (mline.IsClosed ? GoesTheLongWay(mline, firstPick, secondPick) : secondSegment < segment)
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
                    else if (from < length - Tiny)
                    {
                        // A cut that starts at or past the end of the element (the picks lie beyond the end of a mitred
                        // element's stretch, as at a concave corner) takes nothing out of it.
                        List<DxfMlineBuilder.Run> left = Without(mline.RunsOf(s, e), from, to);

                        // A stretch that would start at the end of the element is nothing to draw, as AutoCAD writes it.
                        left.RemoveAll(r => r.From >= length - Tiny);
                        mline.SetRuns(s, e, left);
                    }
                }
            }
        }
    }
}
