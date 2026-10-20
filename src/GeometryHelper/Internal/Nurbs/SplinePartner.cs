using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// Another curve as a partner: an ellipse made into a spline, or a spline. The pieces of the two are cut in halves
    /// while their boxes are near enough to matter and they are not yet straight, then each pair of straight pieces is
    /// measured, and the best of those found by Newton's method on the real curves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Bézier piece lies in the hull of its control points, so two pieces whose hulls are further apart than the least gap
    /// found so far (less a hair, a billionth of the size of the shapes and never more than a thousandth of the gap, and never
    /// below the snap), or than the reach asked, cannot come nearer or meet: that is the certified bound, and everything within it
    /// is cut again. The hulls are measured by their boxes and by the shadows of their control points on the chord of each
    /// piece and its square, so two pieces running side by side are told their distance, which boxes cannot do. The hair is
    /// the whole of the doubt about the nearest approach: it is 0.001 at a size of a million, and where the gap is 0.005 it is
    /// 0.000005, so a near miss is never read above the point tolerance for a nearer pair left uncut.
    /// </para>
    /// <para>
    /// A pair whose control points lie within a thousandth of its size of the chord is read as two segments, which is enough
    /// to start from when the tangents of the two can never run the same way, since they then meet at most once; where they
    /// could meet twice, as two curves that cross at a shallow angle or touch can, they are cut until they are straight to a
    /// billionth. When places are wanted, two straight parts that cannot meet and are small beside the reach are measured too, not cut
    /// further: a near touch has Newton's method to find it. Newton's method on the two curves, started from the closest points of
    /// the segments, brings the pair to rounding, and then on the distance itself along the first curve (<see cref="Settle"/>), which
    /// takes a tangent touch to its place in a step where on the squared distance it would take a halving of the step at a time. A
    /// part that lies along the other curve to rounding is not cut: it meets it along its whole length and crosses it nowhere; and
    /// when places are wanted, a stretch of the first curve that stays within the reach of the other, level, for a quarter of the size
    /// of the smaller curve is found first (<see cref="FindRuns"/>) and not cut at all.
    /// </para>
    /// </remarks>
    internal sealed class SplinePartner : Partner2
    {
        /// <summary>How near a straight line, as a share of its size, the control points of a piece lie for it to be read as a segment.</summary>
        private const double FlatShare = 1E-3;

        /// <summary>The most pairs of pieces looked at in one question; past it the rest are left, and the answer is the best of the rest.</summary>
        private const int MostPairs = 2000000;

        /// <summary>The most pairs of parts cut for the nearest approach, which are kept waiting in the order of their bounds and so cost memory: past it the best found is the answer.</summary>
        private const int MostNearestPairs = 40000;

        /// <summary>How many pairs of parts a question for the nearest approach may cut before its curve is looked at all along for the nearest places.</summary>
        private const int SeedAfter = 2000;

        /// <summary>The deepest a piece is cut in halves.</summary>
        private const int MostDepth = 60;

        private readonly double[] _min = new double[2];
        private readonly double[] _max = new double[2];

        /// <summary>The curve.</summary>
        internal readonly NurbsCurve Curve;

        /// <summary>A partner whose coincident pieces of the other curve are not cut again, since they are known to lie along this one; or null.</summary>
        internal Partner2 Along;

        internal SplinePartner(NurbsCurve curve)
        {
            Curve = curve;
            curve.Index.Whole(_min, _max);
        }

        internal override double Scale => Curve.Scale;

        internal override double Extent => Curve.Extent;

        internal override double Gap(double px, double py, out double qx, out double qy, out double other)
        {
            double knot = Curve.ClosestParameter(new[] { px, py }, out double distance);
            var point = new double[2];
            Curve.PointAt(knot, point);
            qx = point[0];
            qy = point[1];
            other = knot;

            return distance;
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            if (min[at] > max[at])
            {
                return double.PositiveInfinity;
            }

            double square = 0.0;

            for (int k = 0; k < 2; k++)
            {
                double below = _min[k] - max[at + k];
                double above = min[at + k] - _max[k];
                double gap = below > 0.0 ? below : above > 0.0 ? above : 0.0;
                square += gap * gap;
            }

            return Math.Sqrt(square);
        }

        internal override bool TryGetFrame(double qx, double qy, double other, out double r1x, out double r1y, out double r2x, out double r2y)
        {
            double[][] derivatives = Curve.Derivatives(other, 2);
            r1x = derivatives[1][0];
            r1y = derivatives[1][1];
            r2x = derivatives[2][0];
            r2y = derivatives[2][1];
            return true;
        }

        internal override bool IsFree(double other) => other > Curve.Start && other < Curve.End;

        internal override void GetBox(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = _min[0];
            minY = _min[1];
            maxX = _max[0];
            maxY = _max[1];
        }

        internal override int EndCount => 2;

        internal override void EndAt(int index, out double x, out double y)
        {
            var point = new double[2];
            Curve.PointAt(index == 0 ? Curve.Start : Curve.End, point);
            x = point[0];
            y = point[1];
        }

        internal override void PieceCandidates(NurbsCurve curve, BezierPieces pieces, int piece, Hunt hunt)
        {
            throw new NotSupportedException("A curve is met by pairs of pieces, in Find.");
        }

        private Dictionary<int, bool> _coincident;

        // What has been found about which pieces of the curve lie along this partner, made when first asked.
        private Dictionary<int, bool> Memo => _coincident ?? (_coincident = new Dictionary<int, bool>());

        internal override bool Coincident(NurbsCurve curve, int piece)
        {
            if (Memo.TryGetValue(piece, out bool known))
            {
                return known;
            }

            // Along this curve if three points of the piece, a quarter, a half and three quarters of the way, are within
            // rounding of it.
            BezierPieces pieces = curve.Pieces;
            double noise = 1E-12 * Math.Max(curve.Scale, Curve.Scale);
            var point = new double[2];
            bool along = true;

            for (int i = 1; i <= 3 && along; i++)
            {
                curve.PointAt(pieces.From[piece] + 0.25 * i * (pieces.To[piece] - pieces.From[piece]), point);
                Curve.ClosestParameter(new[] { point[0], point[1] }, out double distance);
                along = distance <= noise;
            }

            Memo[piece] = along;
            return along;
        }

        internal override void Find(NurbsCurve a, Hunt hunt)
        {
            NurbsCurve b = Curve;

            if (a.SameAs(b))
            {
                // The same curve lies along itself, from its start to its end.
                a.Consider(hunt, this, a.Start);
                hunt.Mark(a.Start);
                return;
            }

            // The curve is no nearer than its box.
            var min = new double[2];
            var max = new double[2];
            a.Index.Whole(min, max);

            if (!(Bound(min, max, 0) <= hunt.Limit))
            {
                return;
            }

            // The ends: each end of a curve is where the other comes nearest it, and the least gap can be at one.
            a.Consider(hunt, this, a.Start);
            a.Consider(hunt, this, a.End);
            var point = new double[2];

            for (int e = 0; e < 2; e++)
            {
                double end = e == 0 ? b.Start : b.End;
                b.PointAt(end, point);
                double knot = a.ClosestParameter(new[] { point[0], point[1] }, out _);
                Pair(a, b, knot, end, hunt);
            }

            // The inside: pairs of pieces cut until straight, the pair whose boxes are nearest first, so that the best found so far falls
            // early and the farther pairs are passed over.
            var visit = new PieceVisit { A = a.Pieces, B = b.Pieces, Hunt = hunt, Found = new List<PiecePair>() };
            a.Index.Pairs(b.Index, ref visit);
            visit.Found.Sort((x, y) => x.Bound != y.Bound ? x.Bound.CompareTo(y.Bound) : x.First != y.First ? x.First.CompareTo(y.First) : x.Second.CompareTo(y.Second));
            int pairs = 0;

            if (hunt.Reach > 0.0)
            {
                FindRuns(a, b, visit.Found, hunt);
            }

            Refine(a, b, visit.Found, hunt, ref pairs);
        }

        /// <summary>
        /// Finds the stretches of the first curve that lie along the second: stretches a quarter of the size of the smaller curve long,
        /// along which each curve stays within the reach of the other and the first keeps its distance from the second to within half the
        /// reach. Two curves that stay that near and that level for that far are as good as one for the places asked for, and cutting
        /// them to find where the rounding of their numbers has them touch or cross would take millions of parts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A touch is not level: two curves that touch are within half a thousandth of each other for 2 in all if their radii are 1000
        /// and infinite, which is not a quarter of 2000, and the distance there changes by the whole reach before it leaves; and a
        /// crossing at any angle goes from nought to the reach and past it. Only a distance that stays about the same for a quarter of
        /// the size is a stretch along: two circles 0.0005 apart, a spline and its copy moved by 0.0004, a line and a curve fitted to it
        /// by a hundred-millionth.
        /// </para>
        /// <para>
        /// The first curve is looked at over the pieces that have a piece of the second near enough to matter (the pairs found), at points
        /// a sixteenth of a stretch apart along it, so that it costs a few hundred looks at most. Then the second curve is looked at over
        /// the stretch the first's ends map to, at points as far apart along it, and where it leaves the reach of the first, as a dart does
        /// that goes out of the stretch and back, a gap of two steps either side of where it does is cut out of the run, which is
        /// cut in two. A curve that darts away and back between two looks, narrower than a step, is read as staying near; those of a
        /// polyline are not missed, since each of its vertices is looked at. Last, the offset of the first curve from the second is
        /// followed through each stretch (<see cref="Offset"/>): where it changes sign by more than a hundredth of the reach each way, the
        /// curves cross at a shallow angle inside the stretch, and the crossing is a place, as the exact curves give it. A change of
        /// sign by less (a hundredth of the reach is 1E-5) is the rounding of curves that are the same to a hundred-millionth, which is no place.
        /// </para>
        /// </remarks>
        private void FindRuns(NurbsCurve a, NurbsCurve b, List<PiecePair> pairs, Hunt hunt)
        {
            var candidates = new List<int>();

            foreach (PiecePair pair in pairs)
            {
                candidates.Add(pair.First);
            }

            candidates.Sort();
            BezierPieces pieces = a.Pieces;
            double smaller = Math.Min(a.Extent, b.Extent);
            double stretch = 0.25 * smaller;
            double step = stretch / 16.0;
            double level = 0.5 * hunt.Reach;
            var raw = new List<double[]>();
            var point = new double[2];
            var query = new double[2];
            int previous = -2;
            bool inRun = false;
            double runFrom = 0.0;
            double runTo = 0.0;
            double length = 0.0;
            double low = 0.0;
            double high = 0.0;
            double lastX = 0.0;
            double lastY = 0.0;

            for (int c = 0; c <= candidates.Count; c++)
            {
                int piece = c < candidates.Count ? candidates[c] : -1;

                if (piece == previous)
                {
                    continue;
                }

                bool contiguous = previous >= 0 && piece == previous + 1;

                if (!contiguous)
                {
                    if (inRun && length >= stretch)
                    {
                        raw.Add(new[] { runFrom, runTo });
                    }

                    inRun = false;
                }

                previous = piece;

                if (piece < 0)
                {
                    break;
                }

                int intervals = Math.Max(1, (int)Math.Ceiling(PolygonLength(pieces, piece) / step));

                // The first point of a piece that follows another is the last of that one, looked at already.
                for (int k = contiguous ? 1 : 0; k <= intervals; k++)
                {
                    double knot = pieces.From[piece] + (pieces.To[piece] - pieces.From[piece]) * k / intervals;
                    a.PointAt(knot, point);
                    query[0] = point[0];
                    query[1] = point[1];
                    b.ClosestParameter(query, out double distance);
                    bool near = distance <= hunt.Reach;

                    if (near && inRun && Math.Max(high, distance) - Math.Min(low, distance) <= level)
                    {
                        length += Math.Sqrt((point[0] - lastX) * (point[0] - lastX) + (point[1] - lastY) * (point[1] - lastY));
                        low = Math.Min(low, distance);
                        high = Math.Max(high, distance);
                        runTo = knot;
                    }
                    else
                    {
                        if (inRun && length >= stretch)
                        {
                            raw.Add(new[] { runFrom, runTo });
                        }

                        // A point near but not level with the run so far starts the next.
                        inRun = near;
                        runFrom = knot;
                        runTo = knot;
                        length = 0.0;
                        low = distance;
                        high = distance;
                    }

                    lastX = point[0];
                    lastY = point[1];
                }
            }

            foreach (double[] run in raw)
            {
                foreach (double[] part in ConfirmFromSecond(a, b, run[0], run[1], step, hunt))
                {
                    if (ArcLength(a, part[0], part[1]) >= stretch)
                    {
                        hunt.Runs.Add(new Hunt.AlongRun { Part = Position, From = part[0], To = part[1] });
                        CrossingsIn(a, b, part[0], part[1], step, hunt);
                    }
                }
            }
        }

        private static double PolygonLength(BezierPieces pieces, int piece)
        {
            // The control polygon is no shorter than the piece, and no longer by much where it is nearly straight.
            double polygon = 0.0;
            int at = pieces.PointOffset(piece);

            for (int j = 0; j < pieces.Degree; j++)
            {
                double dx = pieces.Points[at + 2 * j + 2] - pieces.Points[at + 2 * j];
                double dy = pieces.Points[at + 2 * j + 3] - pieces.Points[at + 2 * j + 1];
                polygon += Math.Sqrt(dx * dx + dy * dy);
            }

            return polygon;
        }

        /// <summary>
        /// Gets the length of a stretch of a curve, from a polyline of twenty-four points on it.
        /// </summary>
        private static double ArcLength(NurbsCurve curve, double from, double to)
        {
            var last = new double[2];
            var point = new double[2];
            curve.PointAt(from, last);
            double length = 0.0;

            for (int i = 1; i <= 24; i++)
            {
                curve.PointAt(from + (to - from) * i / 24.0, point);
                length += Math.Sqrt((point[0] - last[0]) * (point[0] - last[0]) + (point[1] - last[1]) * (point[1] - last[1]));
                last[0] = point[0];
                last[1] = point[1];
            }

            return length;
        }

        /// <summary>
        /// Looks at the second curve over the stretch that a stretch of the first maps to, and gives the parts of the stretch outside
        /// the gaps cut round each place the second leaves the reach of the first.
        /// </summary>
        private List<double[]> ConfirmFromSecond(NurbsCurve a, NurbsCurve b, double from, double to, double step, Hunt hunt)
        {
            var point = new double[2];
            a.PointAt(from, point);
            double t0 = b.ClosestParameter(point, out _);
            a.PointAt(to, point);
            double t1 = b.ClosestParameter(point, out _);
            double lowest = Math.Min(t0, t1);
            double highest = Math.Max(t0, t1);
            BezierPieces pieces = b.Pieces;
            int first = b.PieceAt(lowest, out _);
            int last = b.PieceAt(highest, out _);
            var cuts = new List<double[]>();

            for (int piece = first; piece <= last; piece++)
            {
                double begin = Math.Max(lowest, pieces.From[piece]);
                double end = Math.Min(highest, pieces.To[piece]);
                double share = pieces.To[piece] > pieces.From[piece] ? (end - begin) / (pieces.To[piece] - pieces.From[piece]) : 1.0;
                int intervals = Math.Max(1, (int)Math.Ceiling(PolygonLength(pieces, piece) * share / step));

                for (int k = 0; k <= intervals; k++)
                {
                    double knot = begin + (end - begin) * k / intervals;
                    b.PointAt(knot, point);
                    double nearest = a.ClosestParameter(point, out double distance);

                    if (distance > hunt.Reach && nearest >= from && nearest <= to)
                    {
                        double[][] d = a.Derivatives(nearest, 1);
                        double speed = Math.Sqrt(d[1][0] * d[1][0] + d[1][1] * d[1][1]);
                        double width = speed > 0.0 ? 2.0 * step / speed : (to - from);
                        cuts.Add(new[] { nearest - width, nearest + width });
                    }
                }
            }

            cuts.Sort((x, y) => x[0].CompareTo(y[0]));
            var parts = new List<double[]>();
            double start = from;

            foreach (double[] cut in cuts)
            {
                if (cut[0] > start)
                {
                    parts.Add(new[] { start, Math.Min(cut[0], to) });
                }

                start = Math.Max(start, cut[1]);
            }

            if (start < to)
            {
                parts.Add(new[] { start, to });
            }

            return parts;
        }

        /// <summary>
        /// Gives the crossings inside a stretch along the second curve: the places the offset of the first from the second changes sign,
        /// when it goes more than a hundredth of the reach to each side.
        /// </summary>
        private void CrossingsIn(NurbsCurve a, NurbsCurve b, double from, double to, double step, Hunt hunt)
        {
            int intervals = Math.Max(2, (int)Math.Ceiling(ArcLength(a, from, to) / step));
            var knots = new double[intervals + 1];
            var offsets = new double[intervals + 1];
            var others = new double[intervals + 1];
            double most = 0.0;
            double least = 0.0;

            for (int i = 0; i <= intervals; i++)
            {
                knots[i] = from + (to - from) * i / intervals;
                offsets[i] = Offset(a, knots[i], out others[i]);
                most = Math.Max(most, offsets[i]);
                least = Math.Min(least, offsets[i]);
            }

            if (!(most > 1E-2 * hunt.Reach && -least > 1E-2 * hunt.Reach))
            {
                return;
            }

            for (int i = 0; i < intervals; i++)
            {
                if (offsets[i] != 0.0 && offsets[i + 1] != 0.0 && (offsets[i] < 0.0) != (offsets[i + 1] < 0.0))
                {
                    Cross(a, b, knots[i], knots[i + 1], others[i + 1], offsets[i] < 0.0, hunt);
                }
            }
        }

        /// <summary>
        /// Bisects the crossing between a knot value where the offset of the first curve from the second is on one side and one where it
        /// is on the other, and offers it as a meeting.
        /// </summary>
        private void Cross(NurbsCurve a, NurbsCurve b, double inside, double outside, double outsideOther, bool insideNegative, Hunt hunt)
        {
            for (int step = 0; step < 100; step++)
            {
                double middle = 0.5 * (inside + outside);

                if (middle == inside || middle == outside)
                {
                    break;
                }

                double value = Offset(a, middle, out double middleOther);

                if (value == 0.0 || (value < 0.0) != insideNegative)
                {
                    outside = middle;
                    outsideOther = middleOther;
                }
                else
                {
                    inside = middle;
                }
            }

            Pair(a, b, outside, outsideOther, hunt, true);
        }

        private struct PiecePair
        {
            internal int First;
            internal int Second;
            internal double Bound;
        }

        private struct PieceVisit : IPairVisit
        {
            internal BezierPieces A;
            internal BezierPieces B;
            internal Hunt Hunt;
            internal List<PiecePair> Found;

            public double Limit => Hunt.Cutoff;

            public void Pair(int first, int second)
            {
                double gx = Math.Max(0.0, Math.Max(B.BoxMin[2 * second] - A.BoxMax[2 * first], A.BoxMin[2 * first] - B.BoxMax[2 * second]));
                double gy = Math.Max(0.0, Math.Max(B.BoxMin[2 * second + 1] - A.BoxMax[2 * first + 1], A.BoxMin[2 * first + 1] - B.BoxMax[2 * second + 1]));
                Found.Add(new PiecePair { First = first, Second = second, Bound = Math.Sqrt(gx * gx + gy * gy) });
            }
        }

        private struct Task
        {
            internal Sub First;
            internal Sub Second;
            internal int Depth;

            /// <summary>A bound the distance between the two parts cannot be less than.</summary>
            internal double Bound;

            /// <summary>The order the task was made in, which settles ties between equal bounds.</summary>
            internal long Order;

            /// <summary>The pieces a pair that has not been made into parts is of.</summary>
            internal int FirstPiece, SecondPiece;
        }

        /// <summary>
        /// Tasks waiting to be cut: nearest bound first for the nearest approach, so that the work goes where the least can be and stops
        /// when nothing left can beat the best found; last in first out for the places, which want every part within the reach whatever
        /// the order.
        /// </summary>
        private sealed class Queue
        {
            private readonly List<Task> _items = new List<Task>();
            private readonly bool _nearest;
            private long _order;

            internal Queue(bool nearest)
            {
                _nearest = nearest;
            }

            internal int Count => _items.Count;

            /// <summary>Gets the least bound of the tasks waiting, which is the bound of the first of them to come out in the nearest order.</summary>
            internal double LeastBound => _items.Count > 0 ? _items[0].Bound : double.PositiveInfinity;

            private static bool Before(Task x, Task y) => x.Bound != y.Bound ? x.Bound < y.Bound : x.Order < y.Order;

            internal void Push(Task task)
            {
                task.Order = _order++;
                _items.Add(task);

                if (!_nearest)
                {
                    return;
                }

                int at = _items.Count - 1;

                while (at > 0)
                {
                    int parent = (at - 1) / 2;

                    if (!Before(_items[at], _items[parent]))
                    {
                        break;
                    }

                    Task swap = _items[at];
                    _items[at] = _items[parent];
                    _items[parent] = swap;
                    at = parent;
                }
            }

            internal Task Pop()
            {
                if (!_nearest)
                {
                    Task last = _items[_items.Count - 1];
                    _items.RemoveAt(_items.Count - 1);
                    return last;
                }

                Task top = _items[0];
                Task end = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);

                if (_items.Count > 0)
                {
                    _items[0] = end;
                    int at = 0;

                    while (true)
                    {
                        int child = 2 * at + 1;

                        if (child >= _items.Count)
                        {
                            break;
                        }

                        if (child + 1 < _items.Count && Before(_items[child + 1], _items[child]))
                        {
                            child++;
                        }

                        if (!Before(_items[child], _items[at]))
                        {
                            break;
                        }

                        Task swap = _items[at];
                        _items[at] = _items[child];
                        _items[child] = swap;
                        at = child;
                    }
                }

                return top;
            }
        }

        /// <summary>
        /// Gets a bound the distance between two parts cannot be less than: that of their hulls, and for two straight parts the tighter
        /// one of their chords less their reaches.
        /// </summary>
        private static double BoundOf(Sub x, Sub y)
        {
            double bound = x.Gap(y);

            if (x.Flat && y.Flat)
            {
                bound = Math.Max(bound, ChordBound(x, y));
            }

            return bound;
        }

        /// <summary>
        /// Cuts the pairs of pieces, one of each curve, until the parts that can matter are straight, and measures those.
        /// </summary>
        /// <remarks>
        /// For the nearest approach the pairs come out nearest bound first, whichever piece they are of, so that the budget goes where the
        /// least can be and the cutting ends when the nearest bound left is no nearer than the best found less the hair, which is
        /// the certificate of the answer. If the budget of <see cref="MostNearestPairs"/> runs out first, the best found is the answer and it is
        /// no nearer than the nearest bound left, which is logged.
        /// </remarks>
        private void Refine(NurbsCurve a, NurbsCurve b, List<PiecePair> found, Hunt hunt, ref int pairs)
        {
            int most = hunt.OnlyNearest ? MostNearestPairs : MostPairs;
            var queue = new Queue(hunt.OnlyNearest);

            // The nearest first, which is the last in for the stack.
            var roots = new List<PiecePair>(found);

            if (!hunt.OnlyNearest)
            {
                roots.Reverse();
            }

            foreach (PiecePair pair in roots)
            {
                if (pair.Bound <= hunt.Cutoff && (Along == null || !Along.Coincident(a, pair.First)))
                {
                    queue.Push(new Task { Depth = 0, Bound = pair.Bound, FirstPiece = pair.First, SecondPiece = pair.Second });
                }
            }

            while (queue.Count > 0)
            {
                Task task = queue.Pop();

                if (!(task.Bound <= hunt.Cutoff))
                {
                    if (hunt.OnlyNearest)
                    {
                        // Nothing left is nearer.
                        return;
                    }

                    continue;
                }

                if (task.First == null)
                {
                    // A pair of pieces is made into parts only when it comes out, and bounded as tightly as parts are: two pieces
                    // straight to a thousandth are told apart by their chords, where their boxes would keep them.
                    task.First = Sub.Of(a.Pieces, task.FirstPiece, a.Scale);
                    task.Second = Sub.Of(b.Pieces, task.SecondPiece, a.Scale);
                    task.Bound = BoundOf(task.First, task.Second);

                    if (!(task.Bound <= hunt.Cutoff))
                    {
                        continue;
                    }
                }

                // The pairs counted are those that are cut or measured, not those put aside by their bounds.
                if (++pairs > most)
                {
                    // Curves that meet are nought apart, which nothing is nearer than: the answer is certain and there is nothing to say.
                    if (!hunt.OnlyNearest || hunt.Least > hunt.Snap)
                    {
                        GeometryHelperLog.Debug("Spline queries: two splines of " + a.Pieces.Count + " and " + b.Pieces.Count + " pieces were cut into more than " + most + " pairs of parts" + (hunt.OnlyNearest ? "; the nearest approach found is " + hunt.Least + " and nothing left uncut is nearer than " + task.Bound : "; the places are those of the parts looked at") + ".");
                    }

                    return;
                }

                Sub x = task.First;
                Sub y = task.Second;
                // A part inside a stretch that lies along the other curve is not cut: it has no place to give.
                if (hunt.Runs.Count > 0 && hunt.IsAlong(Position, x.From, x.To))
                {
                    continue;
                }

                // Once the curves are known to meet, a part that starts after the lowest place they do cannot come first.
                if (hunt.OnlyNearest && hunt.Least <= hunt.Snap && x.From > hunt.ZeroKnot)
                {
                    continue;
                }

                // A part that lies along the other curve, to rounding, meets it along its whole length and crosses it nowhere: it is a
                // meeting for the distance and is not cut again, which would take it to a billionth of its size along the whole stretch.
                if (task.Depth % 3 == 0 && !(x.Fine && y.Fine))
                {
                    if (x.Along(b, hunt.Scale))
                    {
                        x.PointAt(0.5, out double mx, out double my);
                        Pair(a, b, x.From + 0.5 * (x.To - x.From), b.ClosestParameter(new[] { mx, my }, out _), hunt);
                        continue;
                    }

                    if (y.Along(a, hunt.Scale))
                    {
                        y.PointAt(0.5, out double mx, out double my);
                        Pair(a, b, a.ClosestParameter(new[] { mx, my }, out _), y.From + 0.5 * (y.To - y.From), hunt);
                        continue;
                    }
                }

                // A question that is not done in a couple of thousand pairs has two curves that run side by side, so that no pair can be put aside
                // until it is very small, and the budget is spent before any pair is measured: the curve is looked at once at a hundred and
                // twenty-eight points, and its leasts brought to the real curves, so that the best found is near the truth by then and the pairs
                // that cannot beat it are passed over.
                if (hunt.OnlyNearest && pairs == SeedAfter)
                {
                    Seed(a, b, hunt);
                }

                // Straight to a thousandth is enough to start from when the two can meet only once there, their tangents never
                // running the same way; where they could meet twice, as two curves crossing at a shallow angle or touching can,
                // they are cut until they are straight to a billionth. When places are wanted, two straight parts that cannot meet (the bound
                // between them is more than the snap) have a crossing in neither, and a near touch within the reach has Newton's method to
                // find it: they are measured once they are small beside the reach, not cut to a billionth, which for two curves a hair
                // under the reach apart would pair every part with hundreds of the other's.
                bool apartForGood = !hunt.OnlyNearest && x.Flat && y.Flat && x.Diagonal <= 30.0 * hunt.Reach && y.Diagonal <= 30.0 * hunt.Reach && task.Bound > hunt.Snap;

                if (task.Depth >= MostDepth || (x.Fine && y.Fine) || (x.Flat && y.Flat && x.Apart(y)) || apartForGood)
                {
                    Leaf(a, b, x, y, hunt);
                    continue;
                }

                if (!x.Fine && (y.Fine || (!x.Flat && y.Flat) || (x.Flat == y.Flat && x.Diagonal >= y.Diagonal)))
                {
                    x.Split(out Sub left, out Sub right);
                    Task near = new Task { First = left, Second = y, Depth = task.Depth + 1, Bound = BoundOf(left, y) };
                    Task far = new Task { First = right, Second = y, Depth = task.Depth + 1, Bound = BoundOf(right, y) };

                    // The nearer half is taken first, which is the last in for the stack.
                    if (far.Bound < near.Bound)
                    {
                        Task swap = near;
                        near = far;
                        far = swap;
                    }

                    queue.Push(far);
                    queue.Push(near);
                }
                else
                {
                    y.Split(out Sub left, out Sub right);
                    Task near = new Task { First = x, Second = left, Depth = task.Depth + 1, Bound = BoundOf(x, left) };
                    Task far = new Task { First = x, Second = right, Depth = task.Depth + 1, Bound = BoundOf(x, right) };

                    if (far.Bound < near.Bound)
                    {
                        Task swap = near;
                        near = far;
                        far = swap;
                    }

                    queue.Push(far);
                    queue.Push(near);
                }
            }
        }
        /// <summary>
        /// Gets a bound the distance between two parts that are straight to their reach cannot be less than: the distance between their
        /// chords less the two reaches.
        /// </summary>
        private static double ChordBound(Sub x, Sub y)
        {
            Segments(x.X0, x.Y0, x.X1, x.Y1, y.X0, y.Y0, y.X1, y.Y1, out double sigma, out double tau);
            double ex = (x.X0 + sigma * (x.X1 - x.X0)) - (y.X0 + tau * (y.X1 - y.X0));
            double ey = (x.Y0 + sigma * (x.Y1 - x.Y0)) - (y.Y0 + tau * (y.Y1 - y.Y0));

            return Math.Sqrt(ex * ex + ey * ey) - x.Reach - y.Reach;
        }

        /// <summary>
        /// Looks at the first curve at a hundred and twenty-eight points, and brings the three nearest of them to the real curves, offering
        /// what it finds.
        /// </summary>
        private void Seed(NurbsCurve a, NurbsCurve b, Hunt hunt)
        {
            const int Points = 128;
            var samples = new List<double[]>();
            var point = new double[2];

            for (int i = 0; i <= Points; i++)
            {
                double knot = a.Start + (a.End - a.Start) * i / Points;
                a.PointAt(knot, point);
                double other = b.ClosestParameter(point, out double distance);
                samples.Add(new[] { distance, knot, other });
            }

            samples.Sort((x, y) => x[0] != y[0] ? x[0].CompareTo(y[0]) : x[1].CompareTo(y[1]));

            for (int i = 0; i < 3 && i < samples.Count; i++)
            {
                double knotA = samples[i][1];
                double knotB = samples[i][2];
                Polish(a, b, ref knotA, ref knotB, 1E-2 * hunt.Snap);
                Settle(a, b, ref knotA, ref knotB, hunt);
                Pair(a, b, knotA, knotB, hunt);
            }
        }

        /// <summary>
        /// Measures a pair of straight pieces and brings the nearest pair of points to the real curves.
        /// </summary>
        private void Leaf(NurbsCurve a, NurbsCurve b, Sub x, Sub y, Hunt hunt)
        {
            Segments(x.X0, x.Y0, x.X1, x.Y1, y.X0, y.Y0, y.X1, y.Y1, out double sigma, out double tau);

            // The parts are straight to their reach, so they are no nearer than the segments less both reaches.
            double ex = (x.X0 + sigma * (x.X1 - x.X0)) - (y.X0 + tau * (y.X1 - y.X0));
            double ey = (x.Y0 + sigma * (x.Y1 - x.Y0)) - (y.Y0 + tau * (y.Y1 - y.Y0));

            if (!(Math.Sqrt(ex * ex + ey * ey) - x.Reach - y.Reach <= hunt.Cutoff))
            {
                return;
            }

            double knotA = x.KnotAt(sigma);
            double knotB = y.KnotAt(tau);

            Polish(a, b, ref knotA, ref knotB, 1E-2 * hunt.Snap);
            Settle(a, b, ref knotA, ref knotB, hunt);
            Pair(a, b, knotA, knotB, hunt);

            // Two parts whose hulls meet may hold crossings the nearest points settled on do not show.
            if (x.Gap(y) <= hunt.Snap)
            {
                Straddle(a, b, x, knotA, hunt);
                Shallow(a, b, knotA, hunt);
            }

            // Where the nearest points are at a join of two pieces of one curve, the curve is not smooth there and the other
            // is nearest in a place the segments only guessed: the end is measured against the whole of the other curve.
            if ((sigma == 0.0 && x.AtStart) || (sigma == 1.0 && x.AtEnd))
            {
                a.Consider(hunt, this, sigma == 0.0 ? x.From : x.To);
            }

            if ((tau == 0.0 && y.AtStart) || (tau == 1.0 && y.AtEnd))
            {
                var point = new double[2];
                double end = tau == 0.0 ? y.From : y.To;
                b.PointAt(end, point);
                Pair(a, b, a.ClosestParameter(new[] { point[0], point[1] }, out _), end, hunt);
            }
        }

        /// <summary>
        /// Offers the pair of points of the two curves at two knot values.
        /// </summary>
        private void Pair(NurbsCurve a, NurbsCurve b, double knotA, double knotB, Hunt hunt, bool crossing = false)
        {
            knotA = a.Clamp(knotA);
            knotB = b.Clamp(knotB);
            var p = new double[2];
            var q = new double[2];
            a.PointAt(knotA, p);
            b.PointAt(knotB, q);
            double dx = p[0] - q[0];
            double dy = p[1] - q[1];
            double gap = Math.Sqrt(dx * dx + dy * dy);
            double raw = gap;
            bool meets = gap <= hunt.Snap;
            bool overlap = false;

            if (meets)
            {
                overlap = RunsAlong(a, b, knotA, hunt);
                gap = 0.0;
                q[0] = p[0];
                q[1] = p[1];
            }

            hunt.Offer(new Contact
            {
                Knot = knotA,
                Gap = gap,
                Raw = raw,
                Px = p[0],
                Py = p[1],
                Qx = q[0],
                Qy = q[1],
                Other = knotB,
                Part = Position,
                Overlap = overlap,
                Verified = meets,
                Crossing = crossing && meets,
                From = this
            });

            if (meets)
            {
                hunt.Mark(knotA);
            }
        }

        /// <summary>
        /// Says whether the first curve runs along the second at a knot value: a thousandth of the size of the smaller curve either
        /// side of it, along the curve, where it is near the second only to rounding.
        /// </summary>
        private static bool RunsAlong(NurbsCurve a, NurbsCurve b, double knot, Hunt hunt)
        {
            // How far to look, by length along the curve and not by a share of one of its pieces, which may be a hundred-thousandth of
            // it: a thousandth of the size of the smaller curve. Two curves that touch run side by side for a little way, but not for
            // that far, however short the pieces are and however far from the origin they lie.
            double[][] here = a.Derivatives(knot, 1);
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1]);

            if (!(speed > a.StillSpeed))
            {
                return false;
            }

            double step = 1E-3 * Math.Min(a.Extent, b.Extent) / speed;
            double noise = hunt.Snap;
            var point = new double[2];

            for (int side = -1; side <= 1; side += 2)
            {
                double other = a.Clamp(knot + side * step);

                if (other == knot)
                {
                    continue;
                }

                a.PointAt(other, point);
                b.ClosestParameter(new[] { point[0], point[1] }, out double distance);

                if (distance > noise)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Brings a pair of knot values near the nearest points of two curves to them, by Newton's method on the squared
        /// distance with a share of the curvature's size added to the diagonal that is raised until a step does not make the
        /// distance grow, and kept inside the domains. Where the two curves cross it is Newton's method on the crossing, and
        /// where they run parallel it settles on a nearest pair as far as it can.
        /// </summary>
        private static void Polish(NurbsCurve a, NurbsCurve b, ref double s, ref double t, double enough)
        {
            double[][] da = a.Derivatives(s, 2);
            double[][] db = b.Derivatives(t, 2);
            double least = Square(da[0][0] - db[0][0], da[0][1] - db[0][1]);

            for (int step = 0; step < 50 && Math.Sqrt(least) > enough; step++)
            {
                double rx = da[0][0] - db[0][0];
                double ry = da[0][1] - db[0][1];
                double g1 = rx * da[1][0] + ry * da[1][1];
                double g2 = -(rx * db[1][0] + ry * db[1][1]);
                double h11 = da[1][0] * da[1][0] + da[1][1] * da[1][1] + rx * da[2][0] + ry * da[2][1];
                double h12 = -(da[1][0] * db[1][0] + da[1][1] * db[1][1]);
                double h22 = db[1][0] * db[1][0] + db[1][1] * db[1][1] - (rx * db[2][0] + ry * db[2][1]);
                double trace = Math.Abs(h11) + Math.Abs(h22);
                bool moved = false;

                for (double lift = 1E-12; lift <= 1E3 && !moved; lift *= 1E3)
                {
                    double l11 = h11 + lift * trace;
                    double l22 = h22 + lift * trace;
                    double det = l11 * l22 - h12 * h12;

                    if (!(det > 0.0) || !(l11 > 0.0))
                    {
                        continue;
                    }

                    double ns = Math.Min(Math.Max(s - (l22 * g1 - h12 * g2) / det, a.Start), a.End);
                    double nt = Math.Min(Math.Max(t - (-h12 * g1 + l11 * g2) / det, b.Start), b.End);

                    if (double.IsNaN(ns) || double.IsNaN(nt))
                    {
                        continue;
                    }

                    double[][] na = a.Derivatives(ns, 2);
                    double[][] nb = b.Derivatives(nt, 2);
                    double value = Square(na[0][0] - nb[0][0], na[0][1] - nb[0][1]);

                    if (value <= least)
                    {
                        bool settled = Math.Abs(ns - s) <= 1E-16 * (1.0 + Math.Abs(s)) && Math.Abs(nt - t) <= 1E-16 * (1.0 + Math.Abs(t));
                        s = ns;
                        t = nt;
                        da = na;
                        db = nb;
                        least = value;
                        moved = !settled;
                        break;
                    }
                }

                if (!moved)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Gets how far to the side of the second curve a point of the first is, signed: the offset along the normal of the second at
        /// the nearest point, which changes sign where the first crosses the second and nowhere else, however shallow the crossing.
        /// </summary>
        /// <param name="a">The first curve.</param>
        /// <param name="knot">The knot value of the first curve.</param>
        /// <param name="other">The knot value of the nearest point of the second.</param>
        private double Offset(NurbsCurve a, double knot, out double other)
        {
            var point = new double[2];
            a.PointAt(knot, point);
            other = Curve.ClosestParameter(point, out _);
            double[][] d = Curve.Derivatives(other, 1);
            double speed = Math.Sqrt(d[1][0] * d[1][0] + d[1][1] * d[1][1]);

            return speed > 0.0 ? ((point[0] - d[0][0]) * -d[1][1] + (point[1] - d[0][1]) * d[1][0]) / speed : 0.0;
        }

        /// <summary>
        /// Looks for a crossing inside a part of the first curve by the offset of the curve from the second (<see cref="Offset"/>) at its
        /// two ends and at the nearest point found: where it changes sign, the curves cross between, however shallow the angle, and
        /// the crossing is bisected to the last digit. Two curves that cross at a millionth of a radian are as near as rounding can tell
        /// for a long way either side of the crossing, where the nearest points of two segments are not to be found at all.
        /// </summary>
        private void Straddle(NurbsCurve a, NurbsCurve b, Sub x, double knot, Hunt hunt)
        {
            double[] knots = { x.From, a.Clamp(knot), x.To };
            Array.Sort(knots);
            var offsets = new double[3];
            var others = new double[3];

            for (int i = 0; i < 3; i++)
            {
                offsets[i] = Offset(a, knots[i], out others[i]);
            }

            for (int i = 0; i < 2; i++)
            {
                if (knots[i] < knots[i + 1] && offsets[i] != 0.0 && offsets[i + 1] != 0.0 && (offsets[i] < 0.0) != (offsets[i + 1] < 0.0))
                {
                    Cross(a, b, knots[i], knots[i + 1], others[i + 1], offsets[i] < 0.0, hunt);
                }
            }
        }

        /// <summary>
        /// Looks for the crossings that a pair of parts hides when the nearest points Newton's method settles on are a greatest of the
        /// distance and not a least: two curves that cross twice, a hair apart, run parallel between the crossings, where the nearest
        /// points of two segments are the middle, and the distance has nought on either side of it and a hump there that Newton's
        /// method on the squared distance sits on, with no slope to go by.
        /// </summary>
        /// <remarks>
        /// At such a place the distance curves down: it falls to nought either way along the first curve, at the roots of its quadratic
        /// model. The offset of the first curve from the second (<see cref="Offset"/>) is tried at those places, then a little nearer and
        /// further, and where it has the other sign the crossing lies between and is bisected to the last digit. A crossing is a meeting
        /// of the two, so it is offered as one, and the distance is nought.
        /// </remarks>
        private void Shallow(NurbsCurve a, NurbsCurve b, double knot, Hunt hunt)
        {
            double[][] derivatives = a.Derivatives(knot, 2);

            if (!NurbsCurve.TryGetFoot(this, derivatives[0][0], derivatives[0][1], hunt.Scale, out NurbsCurve.Foot foot) || !(foot.G > hunt.Snap))
            {
                return;
            }

            NurbsCurve.Variation(foot, derivatives, out double slope, out double curvature);

            if (!(curvature < 0.0))
            {
                return;
            }

            double here = Offset(a, knot, out _);
            double root = Math.Sqrt(slope * slope - 2.0 * curvature * foot.G);

            for (int side = 0; side < 2; side++)
            {
                // The roots of the quadratic model, one each way along the curve.
                double reach = side == 0 ? (-slope + root) / curvature : (-slope - root) / curvature;
                double before = knot;

                foreach (double share in new[] { 0.25, 0.5, 1.0, 1.5, 2.5, 4.0 })
                {
                    double next = a.Clamp(knot + share * reach);

                    if (next == before)
                    {
                        break;
                    }

                    double there = Offset(a, next, out double otherNext);

                    if (there == 0.0 || (there < 0.0) != (here < 0.0))
                    {
                        // The crossing lies between: bisect.
                        Cross(a, b, before, next, otherNext, here < 0.0, hunt);
                        break;
                    }

                    before = next;
                }
            }
        }

        /// <summary>
        /// Brings a place of the first curve where the distance to the second is not nought to the least of it, by Newton's method
        /// on the distance along the first curve, not on its square: two curves that touch, tangent, are at a distance that is the
        /// square of how far from the touch they are, and the squared distance is the fourth power, which Newton's method on it takes
        /// a halving of the step at a time to reach, where on the distance it takes one step.
        /// </summary>
        /// <remarks>
        /// It moves only inside the piece of the first curve it is on, only where the distance has a positive curvature, and only to a
        /// place that is no farther from the second curve, so it can leave a place where it was but never make it worse.
        /// </remarks>
        private void Settle(NurbsCurve a, NurbsCurve b, ref double s, ref double t, Hunt hunt)
        {
            BezierPieces pieces = a.Pieces;
            int piece = a.PieceAt(s, out _);
            double low = pieces.From[piece];
            double high = pieces.To[piece];
            var query = new double[2];

            for (int step = 0; step < 30; step++)
            {
                double[][] derivatives = a.Derivatives(s, 2);

                if (!NurbsCurve.TryGetFoot(this, derivatives[0][0], derivatives[0][1], hunt.Scale, out NurbsCurve.Foot foot) || !(foot.G > hunt.Snap))
                {
                    return;
                }

                NurbsCurve.Variation(foot, derivatives, out double slope, out double curvature);
                double speed2 = derivatives[1][0] * derivatives[1][0] + derivatives[1][1] * derivatives[1][1];

                if (!(curvature > 1E-9 / hunt.Extent * speed2))
                {
                    return;
                }

                double move = -slope / curvature;
                bool moved = false;

                for (int half = 0; half < 6 && !moved && Math.Abs(move) > 1E-15 * (1.0 + Math.Abs(s)); half++)
                {
                    double next = Math.Min(Math.Max(s + move, low), high);
                    a.PointAt(next, query);
                    double nextOther = b.ClosestParameter(query, out double distance);

                    if (next != s && distance <= foot.G)
                    {
                        s = next;
                        t = nextOther;
                        moved = true;

                        // A step that gains less than a snap is no gain: along a stretch where the curves are as near as rounding
                        // tells, the distance is level and Newton's method would go on for ever.
                        if (foot.G - distance <= hunt.Snap)
                        {
                            return;
                        }
                    }

                    move *= 0.5;
                }

                if (!moved)
                {
                    return;
                }
            }
        }

        private static double Square(double x, double y) => x * x + y * y;

        /// <summary>
        /// Finds the nearest points of two segments, as shares of each from its start, between 0 and 1.
        /// </summary>
        private static void Segments(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, out double sigma, out double tau)
        {
            double ux = bx - ax;
            double uy = by - ay;
            double vx = dx - cx;
            double vy = dy - cy;
            double wx = ax - cx;
            double wy = ay - cy;
            double a = ux * ux + uy * uy;
            double e = vx * vx + vy * vy;
            double f = vx * wx + vy * wy;

            if (a <= 0.0 && e <= 0.0)
            {
                sigma = 0.0;
                tau = 0.0;
                return;
            }

            if (a <= 0.0)
            {
                sigma = 0.0;
                tau = Clamp01(f / e);
                return;
            }

            double c = ux * wx + uy * wy;

            if (e <= 0.0)
            {
                tau = 0.0;
                sigma = Clamp01(-c / a);
                return;
            }

            double b = ux * vx + uy * vy;
            double denominator = a * e - b * b;
            sigma = denominator > 0.0 ? Clamp01((b * f - c * e) / denominator) : 0.0;
            tau = (b * sigma + f) / e;

            if (tau < 0.0)
            {
                tau = 0.0;
                sigma = Clamp01(-c / a);
            }
            else if (tau > 1.0)
            {
                tau = 1.0;
                sigma = Clamp01((b - c) / a);
            }
        }

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;

        /// <summary>
        /// A part of a Bézier piece: its control points, the box they fill, and how far from straight they are.
        /// </summary>
        private sealed class Sub
        {
            private readonly int _degree;
            private readonly int _stride;
            private readonly double _scale;

            internal readonly double From;
            internal readonly double To;
            internal readonly bool AtStart;
            internal readonly bool AtEnd;
            internal readonly double[] Hw;

            internal double MinX, MinY, MaxX, MaxY;
            internal double X0, Y0, X1, Y1;
            internal bool Flat;
            internal double Diagonal;

            // How far the control points are from the chord between the ends, taken as a segment: no point of the part is further.
            internal double Reach;

            // Whether the part is straight to a billionth of its size, and how far the tangent can turn from the chord (radians), worked out when asked.
            internal bool Fine;
            private double _cone = -1.0;

            // The control points as points, and the unit direction of the chord between the ends (nought if it has no length).
            private double[] _px;
            private double[] _py;
            private double _ax, _ay;

            private Sub(int degree, int stride, double from, double to, bool atStart, bool atEnd, double[] hw, double scale)
            {
                _degree = degree;
                _stride = stride;
                From = from;
                To = to;
                AtStart = atStart;
                AtEnd = atEnd;
                Hw = hw;
                _scale = scale;
                Measure(scale);
            }

            internal static Sub Of(BezierPieces pieces, int piece, double scale)
            {
                int p = pieces.Degree;
                int stride = pieces.Rational ? 3 : 2;
                var hw = new double[(p + 1) * stride];
                int at = pieces.PointOffset(piece);

                for (int j = 0; j <= p; j++)
                {
                    double w = pieces.Rational ? pieces.Weights[pieces.WeightOffset(piece) + j] : 1.0;
                    hw[j * stride] = w * pieces.Points[at + j * 2];
                    hw[j * stride + 1] = w * pieces.Points[at + j * 2 + 1];

                    if (pieces.Rational)
                    {
                        hw[j * stride + 2] = w;
                    }
                }

                return new Sub(p, stride, pieces.From[piece], pieces.To[piece], true, true, hw, scale);
            }

            private double X(int j) => _stride == 3 ? Hw[j * 3] / Hw[j * 3 + 2] : Hw[j * 2];

            private double Y(int j) => _stride == 3 ? Hw[j * 3 + 1] / Hw[j * 3 + 2] : Hw[j * 2 + 1];

            private void Measure(double scale)
            {
                MinX = double.PositiveInfinity;
                MinY = double.PositiveInfinity;
                MaxX = double.NegativeInfinity;
                MaxY = double.NegativeInfinity;
                _px = new double[_degree + 1];
                _py = new double[_degree + 1];

                for (int j = 0; j <= _degree; j++)
                {
                    _px[j] = X(j);
                    _py[j] = Y(j);
                    MinX = Math.Min(MinX, _px[j]);
                    MaxX = Math.Max(MaxX, _px[j]);
                    MinY = Math.Min(MinY, _py[j]);
                    MaxY = Math.Max(MaxY, _py[j]);
                }

                X0 = X(0);
                Y0 = Y(0);
                X1 = X(_degree);
                Y1 = Y(_degree);
                double wx = MaxX - MinX;
                double wy = MaxY - MinY;
                Diagonal = Math.Sqrt(wx * wx + wy * wy);

                // How far the control points are from the chord between the ends, which the part is no further from.
                double cx = X1 - X0;
                double cy = Y1 - Y0;
                double chord = Math.Sqrt(cx * cx + cy * cy);
                double deviation = 0.0;

                if (chord > 0.0)
                {
                    _ax = cx / chord;
                    _ay = cy / chord;
                }

                for (int j = 1; j < _degree; j++)
                {
                    double rx = _px[j] - X0;
                    double ry = _py[j] - Y0;
                    double along = chord > 0.0 ? (rx * cx + ry * cy) / (chord * chord) : 0.0;
                    along = along < 0.0 ? 0.0 : along > 1.0 ? 1.0 : along;
                    double ox = rx - along * cx;
                    double oy = ry - along * cy;
                    deviation = Math.Max(deviation, Math.Sqrt(ox * ox + oy * oy));
                }

                Reach = deviation;
                Flat = deviation <= FlatShare * Diagonal + 1E-15 * scale;
                Fine = deviation <= 1E-9 * Diagonal + 1E-15 * scale;
            }

            /// <summary>
            /// Gets the point of the part at a share of its parameter, by de Casteljau's cuts in homogeneous form.
            /// </summary>
            internal void PointAt(double share, out double x, out double y)
            {
                var work = (double[])Hw.Clone();

                for (int level = 1; level <= _degree; level++)
                {
                    for (int j = 0; j <= _degree - level; j++)
                    {
                        for (int k = 0; k < _stride; k++)
                        {
                            work[j * _stride + k] = (1.0 - share) * work[j * _stride + k] + share * work[(j + 1) * _stride + k];
                        }
                    }
                }

                x = _stride == 3 ? work[0] / work[2] : work[0];
                y = _stride == 3 ? work[1] / work[2] : work[1];
            }

            /// <summary>
            /// Says whether the part lies along a curve: five of its points, the middle first, are within rounding of it.
            /// </summary>
            internal bool Along(NurbsCurve other, double scale)
            {
                double noise = 1E-12 * scale;

                foreach (double share in new[] { 0.5, 0.25, 0.75, 0.0, 1.0 })
                {
                    PointAt(share, out double x, out double y);
                    other.ClosestParameter(new[] { x, y }, out double distance);

                    if (distance > noise)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Gets the knot value at which the part has come a share of the way along its chord: a straight part is not run at an
            /// even speed, and a rational one least of all, so the share of the chord is not the share of the knots, and a start
            /// taken as if it were can lie far from the point it was meant for.
            /// </summary>
            internal double KnotAt(double share)
            {
                if (!(share > 0.0))
                {
                    return From;
                }

                if (!(share < 1.0))
                {
                    return To;
                }

                double cx = X1 - X0;
                double cy = Y1 - Y0;
                double length2 = cx * cx + cy * cy;

                if (!(length2 > 0.0))
                {
                    return From + share * (To - From);
                }

                // How far each control point lies along the chord, as a share of it, mixed as the curve mixes them.
                var along = new double[_degree + 1];
                var weight = new double[_degree + 1];

                for (int j = 0; j <= _degree; j++)
                {
                    double w = _stride == 3 ? Hw[j * 3 + 2] : 1.0;
                    weight[j] = w;
                    along[j] = w * ((_px[j] - X0) * cx + (_py[j] - Y0) * cy) / length2;
                }

                double low = 0.0;
                double high = 1.0;

                for (int step = 0; step < 60; step++)
                {
                    double mid = 0.5 * (low + high);

                    if (Mix(along, weight, mid) < share)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid;
                    }
                }

                return From + 0.5 * (low + high) * (To - From);
            }

            private static double Mix(double[] along, double[] weight, double s)
            {
                int n = along.Length - 1;
                var a = (double[])along.Clone();
                var w = (double[])weight.Clone();

                for (int level = 1; level <= n; level++)
                {
                    for (int i = 0; i <= n - level; i++)
                    {
                        a[i] = (1.0 - s) * a[i] + s * a[i + 1];
                        w[i] = (1.0 - s) * w[i] + s * w[i + 1];
                    }
                }

                return a[0] / w[0];
            }

            /// <summary>
            /// Gets how far the tangent of the part can turn from its chord, in radians: the tangent of a Bézier piece, rational
            /// too, is a positive mix of the steps between the control points, so it lies between the steps that turn furthest
            /// either way. Past a quarter turn, or where a step has no length or goes back, it is a quarter turn: no bound.
            /// </summary>
            private double Cone
            {
                get
                {
                    if (_cone >= 0.0)
                    {
                        return _cone;
                    }

                    double cone = 0.0;

                    for (int j = 0; j < _degree && cone < 1.5; j++)
                    {
                        double ex = _px[j + 1] - _px[j];
                        double ey = _py[j + 1] - _py[j];

                        if (ex == 0.0 && ey == 0.0)
                        {
                            continue;
                        }

                        double along = ex * _ax + ey * _ay;
                        double across = ex * -_ay + ey * _ax;
                        cone = along <= 0.0 ? 1.5 : Math.Max(cone, Math.Atan2(Math.Abs(across), along));
                    }

                    _cone = _ax == 0.0 && _ay == 0.0 ? 1.5 : cone;
                    return _cone;
                }
            }

            /// <summary>
            /// Says whether the tangents of two parts can never run the same way, or back along each other: the angle between
            /// their chords, as a line, is more than the two cones together, so the two curves meet at most once.
            /// </summary>
            internal bool Apart(Sub other)
            {
                double sum = Cone + other.Cone;

                if (sum >= 1.5)
                {
                    return false;
                }

                double between = Math.Atan2(Math.Abs(_ax * other._ay - _ay * other._ax), Math.Abs(_ax * other._ax + _ay * other._ay));

                return between > sum;
            }

            /// <summary>
            /// Gets a bound the distance between two parts cannot be less than: the greatest gap between the shadows of their
            /// control points on the axes, each part lying in the hull of its control points and a shadow never being longer than
            /// the distance. The axes are the two of the plane and the chord of each part and its square, so that two parts running
            /// side by side are told their distance, which their boxes cannot.
            /// </summary>
            internal double Gap(Sub other)
            {
                double gx = other.MinX - MaxX > 0.0 ? other.MinX - MaxX : MinX - other.MaxX > 0.0 ? MinX - other.MaxX : 0.0;
                double gy = other.MinY - MaxY > 0.0 ? other.MinY - MaxY : MinY - other.MaxY > 0.0 ? MinY - other.MaxY : 0.0;
                double bound = Math.Sqrt(gx * gx + gy * gy);

                if (_ax != 0.0 || _ay != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, _ax, _ay));
                    bound = Math.Max(bound, Shadow(other, -_ay, _ax));
                }

                if (other._ax != 0.0 || other._ay != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, other._ax, other._ay));
                    bound = Math.Max(bound, Shadow(other, -other._ay, other._ax));
                }

                return bound;
            }

            private double Shadow(Sub other, double ax, double ay)
            {
                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;

                for (int j = 0; j <= _degree; j++)
                {
                    double along = _px[j] * ax + _py[j] * ay;
                    low = Math.Min(low, along);
                    high = Math.Max(high, along);
                }

                double otherLow = double.PositiveInfinity;
                double otherHigh = double.NegativeInfinity;

                for (int j = 0; j <= other._degree; j++)
                {
                    double along = other._px[j] * ax + other._py[j] * ay;
                    otherLow = Math.Min(otherLow, along);
                    otherHigh = Math.Max(otherHigh, along);
                }

                // A shadow measured to rounding is not allowed to push a bound past the truth.
                double gap = Math.Max(otherLow - high, low - otherHigh) - 1E-15 * (Math.Abs(high) + Math.Abs(low) + Math.Abs(otherHigh) + Math.Abs(otherLow));

                return gap > 0.0 ? gap : 0.0;
            }

            /// <summary>
            /// Cuts the part in halves, in homogeneous form for a rational one.
            /// </summary>
            internal void Split(out Sub left, out Sub right)
            {
                NurbsCurve.SplitHomogeneous(Hw, _degree, _stride, out double[] a, out double[] b);
                double middle = 0.5 * (From + To);
                left = new Sub(_degree, _stride, From, middle, AtStart, false, a, _scale);
                right = new Sub(_degree, _stride, middle, To, false, AtEnd, b, _scale);
            }
        }
    }
}
