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
    internal sealed class SplinePartner : Partner
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

        private readonly double[] _min;
        private readonly double[] _max;

        /// <summary>The curve.</summary>
        internal readonly NurbsCurve Curve;

        /// <summary>A partner whose coincident pieces of the other curve are not cut again, since they are known to lie along this one; or null.</summary>
        internal Partner Along;

        /// <summary>
        /// When places are wanted, whether the two curves are in space and share no plane, so that a least of their distance inside a
        /// stretch along is a place (<see cref="Contact.Dip"/>), as the change of sign of the offset is in a plane.
        /// </summary>
        private bool _inSpace;

        /// <summary>
        /// The most the first curve turns, in radians, between two of the looks taken at it along a stretch where two curves in space lie
        /// along each other (<see cref="CrossingsInSpace"/>).
        /// </summary>
        private const double TurnPerLook = 0.05;

        internal SplinePartner(NurbsCurve curve)
        {
            Curve = curve;
            _min = new double[curve.Dim];
            _max = new double[curve.Dim];
            curve.Index.Whole(_min, _max);
        }

        internal override double Scale => Curve.Scale;

        internal override double Extent => Curve.Extent;

        internal override double Gap(double px, double py, double pz, out double qx, out double qy, out double qz, out double other)
        {
            double knot = Curve.ClosestParameter(Curve.Dim == 3 ? new[] { px, py, pz } : new[] { px, py }, out double distance);
            var point = new double[Curve.Dim];
            Curve.PointAt(knot, point);
            qx = point[0];
            qy = point[1];
            qz = Curve.Dim == 3 ? point[2] : 0.0;
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

            for (int k = 0; k < Curve.Dim; k++)
            {
                double below = _min[k] - max[at + k];
                double above = min[at + k] - _max[k];
                double gap = below > 0.0 ? below : above > 0.0 ? above : 0.0;
                square += gap * gap;
            }

            return Math.Sqrt(square);
        }

        internal override bool TryGetFrame(double qx, double qy, double qz, double other, out double r1x, out double r1y, out double r1z, out double r2x, out double r2y, out double r2z)
        {
            double[][] derivatives = Curve.Derivatives(other, 2);
            r1x = derivatives[1][0];
            r1y = derivatives[1][1];
            r1z = NurbsCurve.Z(derivatives[1]);
            r2x = derivatives[2][0];
            r2y = derivatives[2][1];
            r2z = NurbsCurve.Z(derivatives[2]);
            return true;
        }

        // A closed curve has no ends: its nearest point moves on round the seam.
        internal override bool IsFree(double other) => Curve.IsClosed || (other > Curve.Start && other < Curve.End);

        internal override void GetBox(out double minX, out double minY, out double minZ, out double maxX, out double maxY, out double maxZ)
        {
            minX = _min[0];
            minY = _min[1];
            minZ = Curve.Dim == 3 ? _min[2] : 0.0;
            maxX = _max[0];
            maxY = _max[1];
            maxZ = Curve.Dim == 3 ? _max[2] : 0.0;
        }

        internal override int EndCount => 2;

        internal override void EndAt(int index, out double x, out double y, out double z)
        {
            var point = new double[Curve.Dim];
            Curve.PointAt(index == 0 ? Curve.Start : Curve.End, point);
            x = point[0];
            y = point[1];
            z = Curve.Dim == 3 ? point[2] : 0.0;
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
            var point = new double[curve.Dim];
            bool along = true;

            for (int i = 1; i <= 3 && along; i++)
            {
                curve.PointAt(pieces.From[piece] + 0.25 * i * (pieces.To[piece] - pieces.From[piece]), point);
                Curve.ClosestParameter(point, out double distance);
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
            var min = new double[a.Dim];
            var max = new double[a.Dim];
            a.Index.Whole(min, max);

            if (!(Bound(min, max, 0) <= hunt.Limit))
            {
                return;
            }

            // The ends: each end of a curve is where the other comes nearest it, and the least gap can be at one.
            a.Consider(hunt, this, a.Start);
            a.Consider(hunt, this, a.End);
            var point = new double[a.Dim];

            for (int e = 0; e < 2; e++)
            {
                double end = e == 0 ? b.Start : b.End;
                b.PointAt(end, point);
                double knot = a.ClosestParameter(point, out _);
                Pair(a, b, knot, end, hunt);
            }

            // The inside: pairs of pieces cut until straight, the pair whose boxes are nearest first, so that the best found so far falls
            // early and the farther pairs are passed over.
            var visit = new PieceVisit { A = a.Pieces, B = b.Pieces, Hunt = hunt, Found = new List<PiecePair>() };
            a.Index.Pairs(b.Index, ref visit);
            visit.Found.Sort((x, y) => x.Bound != y.Bound ? x.Bound.CompareTo(y.Bound) : x.First != y.First ? x.First.CompareTo(y.First) : x.Second.CompareTo(y.Second));
            List<PiecePair> found = MeetArcs(a, b, visit.Found, hunt);
            int pairs = 0;
            _inSpace = hunt.Reach > 0.0 && a.Dim == 3 && !NurbsCurve.InOnePlane(a, b, out _);

            if (hunt.Reach > 0.0)
            {
                FindRuns(a, b, found, hunt);
            }

            Refine(a, b, found, hunt, ref pairs);
        }

        /// <summary>
        /// Meets the pairs of pieces that are both arcs of circles in closed form, as the circles meet (<see cref="ArcPairs"/>), with the
        /// joins of the second curve's pieces, where it may turn a corner; and gives the other pairs, to be cut, in their order.
        /// </summary>
        private List<PiecePair> MeetArcs(NurbsCurve a, NurbsCurve b, List<PiecePair> found, Hunt hunt)
        {
            List<PiecePair> rest = null;
            bool[] joined = null;
            var point = new double[a.Dim];

            for (int i = 0; i < found.Count; i++)
            {
                PiecePair pair = found[i];

                if (!a.TryGetArc(pair.First, out PieceArc first) || !b.TryGetArc(pair.Second, out PieceArc second) || !ArcPairs.Exact(first, second, hunt))
                {
                    rest?.Add(pair);
                    continue;
                }

                if (rest == null)
                {
                    rest = found.GetRange(0, i);
                    joined = new bool[b.Pieces.Count];
                }

                if (!(pair.Bound <= hunt.Cutoff) || (Along != null && Along.Coincident(a, pair.First)))
                {
                    continue;
                }

                ArcPairs.Meet(a, pair.First, first, second, this, hunt);

                if (!joined[pair.Second])
                {
                    joined[pair.Second] = true;
                    double join = b.Pieces.From[pair.Second];
                    b.PointAt(join, point);
                    Pair(a, b, a.ClosestParameter(point, out _), join, hunt);
                }
            }

            return rest ?? found;
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
            double smaller = Math.Min(Spread(a), Spread(b));
            double stretch = 0.25 * smaller;
            double step = stretch / 16.0;
            // In space a stretch need only stay within the reach: the leasts of the distance in it are places only where the two would
            // cross if brought into one plane (WouldCross), which a level distance is not needed to tell.
            double level = _inSpace ? hunt.Reach : 0.5 * hunt.Reach;
            var raw = new List<double[]>();
            var point = new double[a.Dim];
            var query = new double[a.Dim];
            int previous = -2;
            bool inRun = false;
            double runFrom = 0.0;
            double runTo = 0.0;
            double length = 0.0;
            double low = 0.0;
            double high = 0.0;
            double lastX = 0.0;
            double lastY = 0.0;
            double lastZ = 0.0;

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

                    if (a.Dim == 3)
                    {
                        query[2] = point[2];
                    }

                    b.ClosestParameter(query, out double distance);
                    bool near = distance <= hunt.Reach;
                    double z = a.Dim == 3 ? point[2] : 0.0;

                    if (near && inRun && Math.Max(high, distance) - Math.Min(low, distance) <= level)
                    {
                        length += Math.Sqrt((point[0] - lastX) * (point[0] - lastX) + (point[1] - lastY) * (point[1] - lastY) + (z - lastZ) * (z - lastZ));
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
                    lastZ = z;
                }
            }

            foreach (double[] run in raw)
            {
                foreach (double[] part in ConfirmFromSecond(a, b, run[0], run[1], step, hunt))
                {
                    if (ArcLength(a, part[0], part[1]) >= stretch)
                    {
                        hunt.Runs.Add(new Hunt.AlongRun { Part = Position, From = part[0], To = part[1] });

                        // The crossings inside a stretch are found by the offset of the one curve from the other changing sign, which two
                        // curves in a plane have; two in space have a distance that falls to nought where they cross and does not change sign,
                        // and two that share no plane have the leasts of it for places.
                        if (a.Dim == 2)
                        {
                            CrossingsIn(a, b, part[0], part[1], step, hunt);
                        }
                        else
                        {
                            CrossingsInSpace(a, b, part[0], part[1], step, hunt);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the size of a curve the stretches along are measured by, the same however the curve is turned: the furthest any control
        /// point is from the first. The span along the axes changes as the curve is turned, by as much as the square root of two for a
        /// square turned an eighth of a turn, and with it the stretches looked at and where they end, so that a pair answered in the floor
        /// gave a place more or fewer in a tilted plane, and in the plane 14 of 60 copies of a spline moved by a few ten-thousandths gave a
        /// place more or fewer turned by 0.7 of a radian.
        /// </summary>
        private static double Spread(NurbsCurve curve)
        {
            double[] points = curve.Points;
            int dim = curve.Dim;
            double furthest = 0.0;

            for (int i = dim; i < points.Length; i += dim)
            {
                double dx = points[i] - points[0];
                double dy = points[i + 1] - points[1];
                double dz = dim == 3 ? points[i + 2] - points[2] : 0.0;
                furthest = Math.Max(furthest, dx * dx + dy * dy + dz * dz);
            }

            return Math.Sqrt(furthest);
        }

        private static double PolygonLength(BezierPieces pieces, int piece)
        {
            // The control polygon is no shorter than the piece, and no longer by much where it is nearly straight.
            double polygon = 0.0;
            int at = pieces.PointOffset(piece);
            int dim = pieces.Dim;

            for (int j = 0; j < pieces.Degree; j++)
            {
                double dx = pieces.Points[at + dim * j + dim] - pieces.Points[at + dim * j];
                double dy = pieces.Points[at + dim * j + dim + 1] - pieces.Points[at + dim * j + 1];
                double dz = dim == 3 ? pieces.Points[at + dim * j + dim + 2] - pieces.Points[at + dim * j + 2] : 0.0;
                polygon += Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            return polygon;
        }

        /// <summary>
        /// Gets the length of a stretch of a curve, from a polyline of twenty-four points on it.
        /// </summary>
        private static double ArcLength(NurbsCurve curve, double from, double to)
        {
            var last = new double[curve.Dim];
            var point = new double[curve.Dim];
            curve.PointAt(from, last);
            double length = 0.0;

            for (int i = 1; i <= 24; i++)
            {
                curve.PointAt(from + (to - from) * i / 24.0, point);
                double z = curve.Dim == 3 ? point[2] - last[2] : 0.0;
                length += Math.Sqrt((point[0] - last[0]) * (point[0] - last[0]) + (point[1] - last[1]) * (point[1] - last[1]) + z * z);
                Array.Copy(point, last, curve.Dim);
            }

            return length;
        }

        /// <summary>
        /// Looks at the second curve over the stretch that a stretch of the first maps to, and gives the parts of the stretch outside
        /// the gaps cut round each place the second leaves the reach of the first.
        /// </summary>
        private List<double[]> ConfirmFromSecond(NurbsCurve a, NurbsCurve b, double from, double to, double step, Hunt hunt)
        {
            var point = new double[a.Dim];
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
                        double speed = Math.Sqrt(d[1][0] * d[1][0] + d[1][1] * d[1][1] + NurbsCurve.Z(d[1]) * NurbsCurve.Z(d[1]));
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
        /// Gives the crossings inside a stretch along the second curve, for two curves in space: the places where the distance of the first
        /// from the second, looked at a sixteenth of a stretch apart, falls to a least with the distance rising to more than a hundredth of the
        /// reach either side of it, brought to a least of the two curves by Newton's method and offered as a meeting where it is nought; and
        /// for two curves that share no plane, every least of the distance, which is a place where the two would cross if brought into one
        /// plane (<see cref="WouldCross"/>, <see cref="Contact.Dip"/>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two curves that cross at a shallow angle in a plane are as near as the digits tell for a long way either side of the crossing,
        /// and in space the distance has no sign to change; but the distance from one to the other is nought at the crossing and a hundredth
        /// of the reach away from it a little further out, so a sample that is nearer than its two neighbours has the crossing between them.
        /// Each greatest between two such leasts is tried as well, since two crossings that are nearer each other than the samples leave no
        /// least between them (<see cref="Hump"/>). A change by less than a hundredth of the reach is the rounding of curves that are the
        /// same to a hundred-millionth, which is no place.
        /// </para>
        /// <para>
        /// Two curves that share no plane are looked at four times as close, and closer where the first turns, so that it turns by no more
        /// than <see cref="TurnPerLook"/> between two looks; at every join of its pieces, where it may turn a corner; and where each join of
        /// the second is nearest it, where the second may. A least of the distance of two curves running side by side within the reach is
        /// as wide as the stretch over which one turns away from the other, so these looks have one inside each.
        /// </para>
        /// </remarks>
        private void CrossingsInSpace(NurbsCurve a, NurbsCurve b, double from, double to, double step, Hunt hunt)
        {
            double[] knots = _inSpace ? Looks(a, b, from, to, 0.25 * step) : Even(a, from, to, step);
            int intervals = knots.Length - 1;
            var gaps = new double[intervals + 1];
            var others = new double[intervals + 1];
            var point = new double[a.Dim];

            for (int i = 0; i <= intervals; i++)
            {
                a.PointAt(knots[i], point);
                others[i] = b.ClosestParameter(point, out gaps[i]);
            }

            double flank = 1E-2 * hunt.Reach;

            for (int i = 1; i < intervals; i++)
            {
                if (_inSpace ? gaps[i] < gaps[i - 1] && gaps[i] <= gaps[i + 1] : gaps[i] < gaps[i - 1] && gaps[i] < gaps[i + 1] && gaps[i - 1] > flank && gaps[i + 1] > flank)
                {
                    double s = knots[i];
                    double t = others[i];
                    Polish(a, b, ref s, ref t, 1E-2 * hunt.Snap);

                    if (_inSpace)
                    {
                        Settle(a, b, ref s, ref t, hunt);
                    }

                    Pair(a, b, s, t, hunt, true);
                }
                else if (gaps[i] > gaps[i - 1] && gaps[i] > gaps[i + 1] && gaps[i] > flank)
                {
                    Hump(a, b, knots[i], hunt, true);
                }
            }
        }

        /// <summary>
        /// Gets the knot values of a stretch of a curve a step apart along it, evenly, the two ends among them.
        /// </summary>
        private static double[] Even(NurbsCurve a, double from, double to, double step)
        {
            int intervals = Math.Max(2, (int)Math.Ceiling(ArcLength(a, from, to) / step));
            var knots = new double[intervals + 1];

            for (int i = 0; i <= intervals; i++)
            {
                knots[i] = from + (to - from) * i / intervals;
            }

            return knots;
        }

        /// <summary>
        /// Gets the knot values a stretch of the first curve is looked at, for two curves in space (<see cref="CrossingsInSpace"/>): over each
        /// piece a step apart along it and closer where it turns, at each join, and where each join of the second curve is nearest it, in order.
        /// </summary>
        private static double[] Looks(NurbsCurve a, NurbsCurve b, double from, double to, double step)
        {
            var knots = new List<double>();
            BezierPieces pieces = a.Pieces;
            int first = a.PieceAt(from, out _);
            int last = a.PieceAt(to, out _);

            for (int piece = first; piece <= last; piece++)
            {
                double begin = Math.Max(from, pieces.From[piece]);
                double end = Math.Min(to, pieces.To[piece]);

                if (!(end > begin))
                {
                    continue;
                }

                double share = (end - begin) / (pieces.To[piece] - pieces.From[piece]);
                double byLength = Math.Ceiling(PolygonLength(pieces, piece) * share / step);
                double byTurn = Math.Ceiling(Turning(pieces, piece) * share / TurnPerLook);
                int intervals = (int)Math.Min(4096.0, Math.Max(1.0, Math.Max(byLength, byTurn)));

                for (int k = 0; k < intervals; k++)
                {
                    knots.Add(begin + (end - begin) * k / intervals);
                }
            }

            knots.Add(to);

            // Where the joins of the second curve are nearest the first.
            var point = new double[a.Dim];
            a.PointAt(from, point);
            double t0 = b.ClosestParameter(point, out _);
            a.PointAt(to, point);
            double t1 = b.ClosestParameter(point, out _);
            BezierPieces joins = b.Pieces;

            for (int piece = 1; piece < joins.Count; piece++)
            {
                double join = joins.From[piece];

                if (join > Math.Min(t0, t1) && join < Math.Max(t0, t1))
                {
                    b.PointAt(join, point);
                    double knot = a.ClosestParameter(point, out _);

                    if (knot > from && knot < to)
                    {
                        knots.Add(knot);
                    }
                }
            }

            knots.Sort();
            var looks = new List<double>(knots.Count);

            foreach (double knot in knots)
            {
                if (looks.Count == 0 || knot > looks[looks.Count - 1])
                {
                    looks.Add(knot);
                }
            }

            return looks.ToArray();
        }

        /// <summary>
        /// Gets how far a piece turns, in radians, at most: the sum of the angles between the steps of its control points, which the tangent
        /// of the piece turns no further than.
        /// </summary>
        private static double Turning(BezierPieces pieces, int piece)
        {
            int at = pieces.PointOffset(piece);
            int dim = pieces.Dim;
            double turn = 0.0;

            for (int j = 1; j < pieces.Degree; j++)
            {
                double ax = pieces.Points[at + dim * j] - pieces.Points[at + dim * (j - 1)];
                double ay = pieces.Points[at + dim * j + 1] - pieces.Points[at + dim * (j - 1) + 1];
                double az = dim == 3 ? pieces.Points[at + dim * j + 2] - pieces.Points[at + dim * (j - 1) + 2] : 0.0;
                double bx = pieces.Points[at + dim * (j + 1)] - pieces.Points[at + dim * j];
                double by = pieces.Points[at + dim * (j + 1) + 1] - pieces.Points[at + dim * j + 1];
                double bz = dim == 3 ? pieces.Points[at + dim * (j + 1) + 2] - pieces.Points[at + dim * j + 2] : 0.0;
                double cx = ay * bz - az * by;
                double cy = az * bx - ax * bz;
                double cz = ax * by - ay * bx;
                turn += Math.Atan2(Math.Sqrt(cx * cx + cy * cy + cz * cz), ax * bx + ay * by + az * bz);
            }

            return turn;
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
                int dim = A.Dim;
                double gx = Math.Max(0.0, Math.Max(B.BoxMin[dim * second] - A.BoxMax[dim * first], A.BoxMin[dim * first] - B.BoxMax[dim * second]));
                double gy = Math.Max(0.0, Math.Max(B.BoxMin[dim * second + 1] - A.BoxMax[dim * first + 1], A.BoxMin[dim * first + 1] - B.BoxMax[dim * second + 1]));
                double gz = dim == 3 ? Math.Max(0.0, Math.Max(B.BoxMin[3 * second + 2] - A.BoxMax[3 * first + 2], A.BoxMin[3 * first + 2] - B.BoxMax[3 * second + 2])) : 0.0;
                Found.Add(new PiecePair { First = first, Second = second, Bound = Math.Sqrt(gx * gx + gy * gy + gz * gz) });
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
                // A part inside a stretch that lies along the other curve is not cut: it has no place to give, or in space the leasts of
                // the distance the stretch was looked at for (CrossingsInSpace).
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
                        x.PointAt(0.5, out double mx, out double my, out double mz);
                        Pair(a, b, x.From + 0.5 * (x.To - x.From), b.ClosestParameter(a.Dim == 3 ? new[] { mx, my, mz } : new[] { mx, my }, out _), hunt);
                        continue;
                    }

                    if (y.Along(a, hunt.Scale))
                    {
                        y.PointAt(0.5, out double mx, out double my, out double mz);
                        Pair(a, b, a.ClosestParameter(a.Dim == 3 ? new[] { mx, my, mz } : new[] { mx, my }, out _), y.From + 0.5 * (y.To - y.From), hunt);
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
                // under the reach apart would pair every part with hundreds of the other's. Two parts that cannot meet are measured once both
                // are no larger than the reach, straight or not, in the plane as in space: two places in them would be one, and round a cusp,
                // where a part is straight only when it is a millionth of the curve, two curves within the reach of each other would pair every
                // such part of the one with every such part of the other; a tight degree-four spline and its copy moved 0.00074 took two
                // million pairs and 26 s in the plane where the stretch along them ended short of the cusp, and take 0.3 s.
                bool apartForGood = !hunt.OnlyNearest && task.Bound > hunt.Snap
                    && ((x.Flat && y.Flat && x.Diagonal <= 30.0 * hunt.Reach && y.Diagonal <= 30.0 * hunt.Reach) || (x.Diagonal <= hunt.Reach && y.Diagonal <= hunt.Reach));

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
            Segments(x.X0, x.Y0, x.Z0, x.X1, x.Y1, x.Z1, y.X0, y.Y0, y.Z0, y.X1, y.Y1, y.Z1, out double sigma, out double tau);
            double ex = (x.X0 + sigma * (x.X1 - x.X0)) - (y.X0 + tau * (y.X1 - y.X0));
            double ey = (x.Y0 + sigma * (x.Y1 - x.Y0)) - (y.Y0 + tau * (y.Y1 - y.Y0));
            double ez = (x.Z0 + sigma * (x.Z1 - x.Z0)) - (y.Z0 + tau * (y.Z1 - y.Z0));

            return Math.Sqrt(ex * ex + ey * ey + ez * ez) - x.Reach - y.Reach;
        }

        /// <summary>
        /// Looks at the first curve at a hundred and twenty-eight points, and brings the three nearest of them to the real curves, offering
        /// what it finds.
        /// </summary>
        private void Seed(NurbsCurve a, NurbsCurve b, Hunt hunt)
        {
            const int Points = 128;
            var samples = new List<double[]>();
            var point = new double[a.Dim];

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
            Segments(x.X0, x.Y0, x.Z0, x.X1, x.Y1, x.Z1, y.X0, y.Y0, y.Z0, y.X1, y.Y1, y.Z1, out double sigma, out double tau);

            // The parts are straight to their reach, so they are no nearer than the segments less both reaches.
            double ex = (x.X0 + sigma * (x.X1 - x.X0)) - (y.X0 + tau * (y.X1 - y.X0));
            double ey = (x.Y0 + sigma * (x.Y1 - x.Y0)) - (y.Y0 + tau * (y.Y1 - y.Y0));
            double ez = (x.Z0 + sigma * (x.Z1 - x.Z0)) - (y.Z0 + tau * (y.Z1 - y.Z0));

            if (!(Math.Sqrt(ex * ex + ey * ey + ez * ez) - x.Reach - y.Reach <= hunt.Cutoff))
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
                if (a.Dim == 2)
                {
                    Straddle(a, b, x, knotA, hunt);
                    Shallow(a, b, knotA, hunt);
                }
                else
                {
                    Hump(a, b, knotA, hunt);
                }
            }

            // Where the nearest points are at a join of two pieces of one curve, the curve is not smooth there and the other
            // is nearest in a place the segments only guessed: the end is measured against the whole of the other curve.
            if ((sigma == 0.0 && x.AtStart) || (sigma == 1.0 && x.AtEnd))
            {
                a.Consider(hunt, this, sigma == 0.0 ? x.From : x.To);
            }

            if ((tau == 0.0 && y.AtStart) || (tau == 1.0 && y.AtEnd))
            {
                var point = new double[a.Dim];
                double end = tau == 0.0 ? y.From : y.To;
                b.PointAt(end, point);
                Pair(a, b, a.ClosestParameter(point, out _), end, hunt);
            }
        }

        /// <summary>
        /// Offers the pair of points of the two curves at two knot values.
        /// </summary>
        private void Pair(NurbsCurve a, NurbsCurve b, double knotA, double knotB, Hunt hunt, bool crossing = false)
        {
            knotA = a.Clamp(knotA);
            knotB = b.Clamp(knotB);
            var p = new double[a.Dim];
            var q = new double[a.Dim];
            a.PointAt(knotA, p);
            b.PointAt(knotB, q);
            double dx = p[0] - q[0];
            double dy = p[1] - q[1];
            double dz = a.Dim == 3 ? p[2] - q[2] : 0.0;
            double gap = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            double raw = gap;
            bool meets = gap <= hunt.Snap;
            bool overlap = false;

            if (meets)
            {
                overlap = RunsAlong(a, b, knotA, hunt);
                gap = 0.0;
                Array.Copy(p, q, a.Dim);
            }

            // In space, a least inside a stretch along is a place where the two would cross if brought into one plane.
            bool dip = _inSpace && !meets && gap <= hunt.Reach && hunt.Runs.Count > 0 && hunt.IsAlong(Position, knotA, knotA) && WouldCross(a, b, knotA, knotB, gap, hunt);

            hunt.Offer(new Contact
            {
                Knot = knotA,
                Gap = gap,
                Raw = raw,
                Px = p[0],
                Py = p[1],
                Pz = a.Dim == 3 ? p[2] : 0.0,
                Qx = q[0],
                Qy = q[1],
                Qz = a.Dim == 3 ? q[2] : 0.0,
                Other = knotB,
                Part = Position,
                Overlap = overlap,
                Verified = meets,
                Crossing = crossing && meets,
                Dip = dip,
                From = this
            });

            if (meets)
            {
                hunt.Mark(knotA);
            }
        }

        /// <summary>
        /// Says whether a least of the distance of the first curve from the second is where the two would cross if they were brought into
        /// one plane: seen along the gap, the first curve is on one side of the second just before the least and on the other just after.
        /// </summary>
        /// <param name="a">The first curve.</param>
        /// <param name="b">The second curve.</param>
        /// <param name="knot">The knot value of the least on the first curve.</param>
        /// <param name="other">The knot value of the nearest point of the second curve.</param>
        /// <param name="gap">The distance there.</param>
        /// <param name="hunt">The hunt.</param>
        /// <remarks>
        /// <para>
        /// At a least of the distance of two curves in space the gap stands square to both, and the curves seen along it, as if brought
        /// into the one plane square to it, cross there or touch there. A polyline spline and its copy moved 0.0005 along its plane and
        /// lifted 0.0002 off it come 0.0002 apart at each corner, where seen from above the edge of one goes from one side of the other's to
        /// the other: they would cross, and the corner is a place, as the crossing is in the plane. A spline and its copy moved 0.0005
        /// across its plane with a wobble of 0.0001, lifted off it by a few snaps, come nearest where the wobble brings them nearest, but seen
        /// from above they stay on one side of each other: no place, as there is none in the plane. Two rings 0.0005 apart in two planes are
        /// on neither side of each other, to rounding, and have no place.
        /// </para>
        /// <para>
        /// The side is that of a point of the first curve from the nearest point of the second there, along the direction square to the
        /// gap at the least and to the second curve's direction at that nearest point: the signed offset of the plane, across the curve it
        /// is measured from, so that past a corner of the second its next edge is measured from. It is read a thousandth of the reach
        /// along the first curve either way from the least, and twice as far each time, until it stands a hundredth of the reach out: it
        /// must keep the sign it had once clear of rounding, since one that goes back across sooner is a wiggle of two curves along each
        /// other, which the plane reads as no crossing either, and the least is another's if the distance comes nearer on the way by more
        /// than the snaps Newton's method stops at: a cubic and its copy moved 0.0001 out of its plane have a valley of the distance so
        /// flat, 6E-7 over 90, that a least was settled on its slope and given beside the real one. A least of two curves that cross twice
        /// 17 apart, lifted 0.00003 off their plane, rises 0.000004 between the two, where a rise was once asked of each.
        /// </para>
        /// </remarks>
        private bool WouldCross(NurbsCurve a, NurbsCurve b, double knot, double other, double gap, Hunt hunt)
        {
            double[][] here = a.Derivatives(knot, 1);
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1] + NurbsCurve.Z(here[1]) * NurbsCurve.Z(here[1]));
            double[][] there = b.Derivatives(other, 1);
            double gx = here[0][0] - there[0][0];
            double gy = here[0][1] - there[0][1];
            double gz = NurbsCurve.Z(here[0]) - NurbsCurve.Z(there[0]);
            double length = Math.Sqrt(gx * gx + gy * gy + gz * gz);

            if (!(speed > a.StillSpeed) || !(length > 0.0))
            {
                return false;
            }

            var point = new double[a.Dim];
            double flank = 1E-2 * hunt.Reach;
            int before = 0;

            for (int side = -1; side <= 1; side += 2)
            {
                int sign = 0;
                bool clear = false;
                double from = a.AcrossSeam(knot, side);

                for (double step = 1E-3 * hunt.Reach; step <= 2.0 * a.Extent && !clear; step *= 2.0)
                {
                    double further = a.Around(from + side * step / speed);

                    if (further == from)
                    {
                        break;
                    }

                    a.PointAt(further, point);
                    double at = b.ClosestParameter(point, out double distance);

                    // Nearer on the way, by more than the snaps Newton's method stops at, the least is another's, or no least but a point on
                    // the slope of a valley so flat that the step that found it gained less than a snap.
                    if (distance < gap - 8.0 * hunt.Snap)
                    {
                        return false;
                    }

                    // Which side of the second curve the point is, seen along the gap at the least: across the second's own direction at its
                    // nearest point, the triple product of the offset, the gap and that direction, clear of rounding when it is more than
                    // four snaps.
                    double[][] near = b.Derivatives(at, 1);
                    double sx = gy * NurbsCurve.Z(near[1]) - gz * near[1][1];
                    double sy = gz * near[1][0] - gx * NurbsCurve.Z(near[1]);
                    double sz = gx * near[1][1] - gy * near[1][0];
                    double across = Math.Sqrt(sx * sx + sy * sy + sz * sz);

                    if (!(across > 0.0))
                    {
                        continue;
                    }

                    double offset = ((point[0] - near[0][0]) * sx + (point[1] - near[0][1]) * sy + (NurbsCurve.Z(point) - NurbsCurve.Z(near[0])) * sz) / across;

                    if (Math.Abs(offset) > 4.0 * hunt.Snap)
                    {
                        int now = offset < 0.0 ? -1 : 1;

                        // The side next to the least is the one it has; it goes back across before it is a hundredth of the reach out, it
                        // is a wiggle of the two along each other, which the plane does not read as a crossing either.
                        if (sign != 0 && now != sign)
                        {
                            return false;
                        }

                        sign = now;
                        clear = Math.Abs(offset) > flank;
                    }

                    if (!a.IsClosed && (further == a.Start || further == a.End))
                    {
                        break;
                    }
                }

                if (!clear || sign == before)
                {
                    return false;
                }

                before = sign;
            }

            return true;
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
            double speed = Math.Sqrt(here[1][0] * here[1][0] + here[1][1] * here[1][1] + NurbsCurve.Z(here[1]) * NurbsCurve.Z(here[1]));

            if (!(speed > a.StillSpeed))
            {
                return false;
            }

            double step = 1E-3 * Math.Min(a.Extent, b.Extent) / speed;
            double noise = hunt.Snap;
            var point = new double[a.Dim];

            for (int side = -1; side <= 1; side += 2)
            {
                double from = a.AcrossSeam(knot, side);
                double other = a.Around(from + side * step);

                if (other == from)
                {
                    continue;
                }

                a.PointAt(other, point);
                b.ClosestParameter(point, out double distance);

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
            double least = Square(da[0][0] - db[0][0], da[0][1] - db[0][1], NurbsCurve.Z(da[0]) - NurbsCurve.Z(db[0]));

            for (int step = 0; step < 50 && Math.Sqrt(least) > enough; step++)
            {
                double rx = da[0][0] - db[0][0];
                double ry = da[0][1] - db[0][1];
                double rz = NurbsCurve.Z(da[0]) - NurbsCurve.Z(db[0]);
                double g1 = rx * da[1][0] + ry * da[1][1] + rz * NurbsCurve.Z(da[1]);
                double g2 = -(rx * db[1][0] + ry * db[1][1] + rz * NurbsCurve.Z(db[1]));
                double h11 = da[1][0] * da[1][0] + da[1][1] * da[1][1] + NurbsCurve.Z(da[1]) * NurbsCurve.Z(da[1]) + rx * da[2][0] + ry * da[2][1] + rz * NurbsCurve.Z(da[2]);
                double h12 = -(da[1][0] * db[1][0] + da[1][1] * db[1][1] + NurbsCurve.Z(da[1]) * NurbsCurve.Z(db[1]));
                double h22 = db[1][0] * db[1][0] + db[1][1] * db[1][1] + NurbsCurve.Z(db[1]) * NurbsCurve.Z(db[1]) - (rx * db[2][0] + ry * db[2][1] + rz * NurbsCurve.Z(db[2]));
                double trace = Math.Abs(h11) + Math.Abs(h22);
                bool moved = false;

                // In space two curves that cross at a shallow angle have tangents whose product less the square of their dot is the square
                // of a cross product, 1E-14 of what it is made of at 1E-7 of a radian, and a step that is a difference of such products has no
                // digit left. The step is written with the cross product W of the two tangents, where l11 = |P'|² + e1, l22 = |Q'|² + e2 and
                // the matrix has determinant |W|² + |P'|² e2 + |Q'|² e1 + e1 e2, and the first component of the step is W . (r x Q') + e2 g1 and
                // the second W . (r x P') + e1 g2: the same step, each part computed from what is small and not from what cancels.
                double wx = 0.0, wy = 0.0, wz = 0.0, tangentP = 0.0, tangentQ = 0.0, e1 = 0.0, e2 = 0.0, wq = 0.0, wp = 0.0;

                if (a.Dim == 3)
                {
                    wx = da[1][1] * db[1][2] - da[1][2] * db[1][1];
                    wy = da[1][2] * db[1][0] - da[1][0] * db[1][2];
                    wz = da[1][0] * db[1][1] - da[1][1] * db[1][0];
                    tangentP = da[1][0] * da[1][0] + da[1][1] * da[1][1] + da[1][2] * da[1][2];
                    tangentQ = db[1][0] * db[1][0] + db[1][1] * db[1][1] + db[1][2] * db[1][2];
                    e1 = rx * da[2][0] + ry * da[2][1] + rz * da[2][2];
                    e2 = -(rx * db[2][0] + ry * db[2][1] + rz * db[2][2]);
                    wq = wx * (ry * db[1][2] - rz * db[1][1]) + wy * (rz * db[1][0] - rx * db[1][2]) + wz * (rx * db[1][1] - ry * db[1][0]);
                    wp = wx * (ry * da[1][2] - rz * da[1][1]) + wy * (rz * da[1][0] - rx * da[1][2]) + wz * (rx * da[1][1] - ry * da[1][0]);
                }

                // Two curves in space a hair from crossing have a matrix whose small eigenvalue is |W|² over its trace, 1E-7 of it at 1E-7 of
                // a radian: a lift of a trillionth of the trace is five hundred times that, and the step it damps goes a five-hundredth of the
                // way. The step is tried with no lift first, and kept if the distance does not grow.
                for (double lift = a.Dim == 3 ? 0.0 : 1E-12; lift <= 1E3 && !moved; lift = lift == 0.0 ? 1E-12 : lift * 1E3)
                {
                    double l11 = h11 + lift * trace;
                    double l22 = h22 + lift * trace;
                    double det = l11 * l22 - h12 * h12;
                    double stepS = l22 * g1 - h12 * g2;
                    double stepT = -h12 * g1 + l11 * g2;

                    if (a.Dim == 3)
                    {
                        double f1 = e1 + lift * trace;
                        double f2 = e2 + lift * trace;
                        det = wx * wx + wy * wy + wz * wz + tangentP * f2 + tangentQ * f1 + f1 * f2;
                        stepS = wq + f2 * g1;
                        stepT = wp + f1 * g2;
                    }

                    if (!(det > 0.0) || !(l11 > 0.0))
                    {
                        continue;
                    }

                    double ns = Math.Min(Math.Max(s - stepS / det, a.Start), a.End);
                    double nt = Math.Min(Math.Max(t - stepT / det, b.Start), b.End);

                    if (double.IsNaN(ns) || double.IsNaN(nt))
                    {
                        continue;
                    }

                    double[][] na = a.Derivatives(ns, 2);
                    double[][] nb = b.Derivatives(nt, 2);
                    double value = Square(na[0][0] - nb[0][0], na[0][1] - nb[0][1], NurbsCurve.Z(na[0]) - NurbsCurve.Z(nb[0]));

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

            if (!NurbsCurve.TryGetFoot(this, derivatives[0][0], derivatives[0][1], 0.0, hunt.Scale, 2, out NurbsCurve.Foot foot) || !(foot.G > hunt.Snap))
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
        /// Looks, for two curves in space, for the meetings a pair of parts hides when the nearest points Newton's method settles on are a
        /// greatest of the distance and not a least: two curves that run side by side in a plane and cross twice a hair apart, the nearest
        /// points of two segments there being the middle, with the distance falling to nought either way along the first curve.
        /// </summary>
        /// <remarks>
        /// In the plane the offset of one curve from the other changes sign where they cross (<see cref="Shallow"/>); in space it has no
        /// sign. The distance has the same hump, though, and its quadratic model reaches nought one way along the curve and the other:
        /// from the model's roots, and from a little nearer and further, the pair is brought to a least by Newton's method on the two
        /// curves, and what it settles on is offered, a crossing where the distance comes to nought. A least that is not a meeting, as
        /// it may be, is a place or not as the distance there makes it.
        /// </remarks>
        private void Hump(NurbsCurve a, NurbsCurve b, double knot, Hunt hunt, bool crossing = false)
        {
            double[][] derivatives = a.Derivatives(knot, 2);

            if (!NurbsCurve.TryGetFoot(this, derivatives[0][0], derivatives[0][1], NurbsCurve.Z(derivatives[0]), hunt.Scale, a.Dim, out NurbsCurve.Foot foot) || !(foot.G > hunt.Snap))
            {
                return;
            }

            NurbsCurve.Variation(foot, derivatives, out double slope, out double curvature);

            if (!(curvature < 0.0))
            {
                return;
            }

            double root = Math.Sqrt(slope * slope - 2.0 * curvature * foot.G);
            var query = new double[a.Dim];

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

                    before = next;
                    a.PointAt(next, query);
                    double other = b.ClosestParameter(query, out _);
                    Polish(a, b, ref next, ref other, 1E-2 * hunt.Snap);
                    Pair(a, b, next, other, hunt, crossing);
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
            var query = new double[a.Dim];

            for (int step = 0; step < 30; step++)
            {
                double[][] derivatives = a.Derivatives(s, 2);

                if (!NurbsCurve.TryGetFoot(this, derivatives[0][0], derivatives[0][1], NurbsCurve.Z(derivatives[0]), hunt.Scale, a.Dim, out NurbsCurve.Foot foot) || !(foot.G > hunt.Snap))
                {
                    return;
                }

                NurbsCurve.Variation(foot, derivatives, out double slope, out double curvature);
                double speed2 = derivatives[1][0] * derivatives[1][0] + derivatives[1][1] * derivatives[1][1] + NurbsCurve.Z(derivatives[1]) * NurbsCurve.Z(derivatives[1]);

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

        private static double Square(double x, double y, double z) => x * x + y * y + z * z;

        /// <summary>
        /// Finds the nearest points of two segments, as shares of each from its start, between 0 and 1.
        /// </summary>
        private static void Segments(double ax, double ay, double az, double bx, double by, double bz, double cx, double cy, double cz, double dx, double dy, double dz, out double sigma, out double tau)
        {
            double ux = bx - ax;
            double uy = by - ay;
            double uz = bz - az;
            double vx = dx - cx;
            double vy = dy - cy;
            double vz = dz - cz;
            double wx = ax - cx;
            double wy = ay - cy;
            double wz = az - cz;
            double a = ux * ux + uy * uy + uz * uz;
            double e = vx * vx + vy * vy + vz * vz;
            double f = vx * wx + vy * wy + vz * wz;

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

            double c = ux * wx + uy * wy + uz * wz;

            if (e <= 0.0)
            {
                tau = 0.0;
                sigma = Clamp01(-c / a);
                return;
            }

            double b = ux * vx + uy * vy + uz * vz;
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
            private readonly int _dim;
            private readonly int _degree;
            private readonly bool _rational;
            private readonly int _stride;
            private readonly double _scale;

            internal readonly double From;
            internal readonly double To;
            internal readonly bool AtStart;
            internal readonly bool AtEnd;
            internal readonly double[] Hw;

            internal double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
            internal double X0, Y0, Z0, X1, Y1, Z1;
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
            private double[] _pz;
            private double _ax, _ay, _az;

            // In space, two more unit directions square to the chord and to each other.
            private double _ux, _uy, _uz, _vx, _vy, _vz;

            private Sub(int dim, int degree, bool rational, double from, double to, bool atStart, bool atEnd, double[] hw, double scale)
            {
                _dim = dim;
                _degree = degree;
                _rational = rational;
                _stride = rational ? dim + 1 : dim;
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
                int dim = pieces.Dim;
                int stride = pieces.Rational ? dim + 1 : dim;
                var hw = new double[(p + 1) * stride];
                int at = pieces.PointOffset(piece);

                for (int j = 0; j <= p; j++)
                {
                    double w = pieces.Rational ? pieces.Weights[pieces.WeightOffset(piece) + j] : 1.0;

                    for (int k = 0; k < dim; k++)
                    {
                        hw[j * stride + k] = w * pieces.Points[at + j * dim + k];
                    }

                    if (pieces.Rational)
                    {
                        hw[j * stride + dim] = w;
                    }
                }

                return new Sub(dim, p, pieces.Rational, pieces.From[piece], pieces.To[piece], true, true, hw, scale);
            }

            private double Coordinate(int j, int k) => _rational ? Hw[j * _stride + k] / Hw[j * _stride + _dim] : Hw[j * _stride + k];

            private void Measure(double scale)
            {
                MinX = double.PositiveInfinity;
                MinY = double.PositiveInfinity;
                MinZ = double.PositiveInfinity;
                MaxX = double.NegativeInfinity;
                MaxY = double.NegativeInfinity;
                MaxZ = double.NegativeInfinity;
                _px = new double[_degree + 1];
                _py = new double[_degree + 1];
                _pz = new double[_degree + 1];

                for (int j = 0; j <= _degree; j++)
                {
                    _px[j] = Coordinate(j, 0);
                    _py[j] = Coordinate(j, 1);
                    _pz[j] = _dim == 3 ? Coordinate(j, 2) : 0.0;
                    MinX = Math.Min(MinX, _px[j]);
                    MaxX = Math.Max(MaxX, _px[j]);
                    MinY = Math.Min(MinY, _py[j]);
                    MaxY = Math.Max(MaxY, _py[j]);
                    MinZ = Math.Min(MinZ, _pz[j]);
                    MaxZ = Math.Max(MaxZ, _pz[j]);
                }

                X0 = _px[0];
                Y0 = _py[0];
                Z0 = _pz[0];
                X1 = _px[_degree];
                Y1 = _py[_degree];
                Z1 = _pz[_degree];
                double wx = MaxX - MinX;
                double wy = MaxY - MinY;
                double wz = MaxZ - MinZ;
                Diagonal = Math.Sqrt(wx * wx + wy * wy + wz * wz);

                // How far the control points are from the chord between the ends, which the part is no further from.
                double cx = X1 - X0;
                double cy = Y1 - Y0;
                double cz = Z1 - Z0;
                double chord = Math.Sqrt(cx * cx + cy * cy + cz * cz);
                double deviation = 0.0;

                if (chord > 0.0)
                {
                    _ax = cx / chord;
                    _ay = cy / chord;
                    _az = cz / chord;

                    if (_dim == 3)
                    {
                        Perpendiculars();
                    }
                }

                for (int j = 1; j < _degree; j++)
                {
                    double rx = _px[j] - X0;
                    double ry = _py[j] - Y0;
                    double rz = _pz[j] - Z0;
                    double along = chord > 0.0 ? (rx * cx + ry * cy + rz * cz) / (chord * chord) : 0.0;
                    along = along < 0.0 ? 0.0 : along > 1.0 ? 1.0 : along;
                    double ox = rx - along * cx;
                    double oy = ry - along * cy;
                    double oz = rz - along * cz;
                    deviation = Math.Max(deviation, Math.Sqrt(ox * ox + oy * oy + oz * oz));
                }

                Reach = deviation;
                Flat = deviation <= FlatShare * Diagonal + 1E-15 * scale;
                Fine = deviation <= 1E-9 * Diagonal + 1E-15 * scale;
            }

            /// <summary>
            /// Makes two unit directions square to the chord and to each other: the first square to the chord and to the coordinate axis
            /// it leans on least, the second square to both.
            /// </summary>
            private void Perpendiculars()
            {
                double ax = Math.Abs(_ax);
                double ay = Math.Abs(_ay);
                double az = Math.Abs(_az);
                double hx = ax <= ay && ax <= az ? 1.0 : 0.0;
                double hy = hx == 0.0 && ay <= az ? 1.0 : 0.0;
                double hz = hx == 0.0 && hy == 0.0 ? 1.0 : 0.0;
                double ux = _ay * hz - _az * hy;
                double uy = _az * hx - _ax * hz;
                double uz = _ax * hy - _ay * hx;
                double length = Math.Sqrt(ux * ux + uy * uy + uz * uz);

                _ux = ux / length;
                _uy = uy / length;
                _uz = uz / length;
                _vx = _ay * _uz - _az * _uy;
                _vy = _az * _ux - _ax * _uz;
                _vz = _ax * _uy - _ay * _ux;
            }

            /// <summary>
            /// Gets the point of the part at a share of its parameter, by de Casteljau's cuts in homogeneous form.
            /// </summary>
            internal void PointAt(double share, out double x, out double y, out double z)
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

                x = _rational ? work[0] / work[_dim] : work[0];
                y = _rational ? work[1] / work[_dim] : work[1];
                z = _dim == 3 ? (_rational ? work[2] / work[_dim] : work[2]) : 0.0;
            }

            /// <summary>
            /// Says whether the part lies along a curve: five of its points, the middle first, are within rounding of it.
            /// </summary>
            internal bool Along(NurbsCurve other, double scale)
            {
                double noise = 1E-12 * scale;

                foreach (double share in new[] { 0.5, 0.25, 0.75, 0.0, 1.0 })
                {
                    PointAt(share, out double x, out double y, out double z);
                    other.ClosestParameter(_dim == 3 ? new[] { x, y, z } : new[] { x, y }, out double distance);

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
                double cz = Z1 - Z0;
                double length2 = cx * cx + cy * cy + cz * cz;

                if (!(length2 > 0.0))
                {
                    return From + share * (To - From);
                }

                // How far each control point lies along the chord, as a share of it, mixed as the curve mixes them.
                var along = new double[_degree + 1];
                var weight = new double[_degree + 1];

                for (int j = 0; j <= _degree; j++)
                {
                    double w = _rational ? Hw[j * _stride + _dim] : 1.0;
                    weight[j] = w;
                    along[j] = w * ((_px[j] - X0) * cx + (_py[j] - Y0) * cy + (_pz[j] - Z0) * cz) / length2;
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
                        double ez = _pz[j + 1] - _pz[j];

                        if (ex == 0.0 && ey == 0.0 && ez == 0.0)
                        {
                            continue;
                        }

                        double along = ex * _ax + ey * _ay + ez * _az;
                        double across;

                        if (_dim == 3)
                        {
                            double ox = ex - along * _ax;
                            double oy = ey - along * _ay;
                            double oz = ez - along * _az;
                            across = Math.Sqrt(ox * ox + oy * oy + oz * oz);
                        }
                        else
                        {
                            across = ex * -_ay + ey * _ax;
                        }

                        cone = along <= 0.0 ? 1.5 : Math.Max(cone, Math.Atan2(Math.Abs(across), along));
                    }

                    _cone = _ax == 0.0 && _ay == 0.0 && _az == 0.0 ? 1.5 : cone;
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

                double between;

                if (_dim == 3)
                {
                    double cx = _ay * other._az - _az * other._ay;
                    double cy = _az * other._ax - _ax * other._az;
                    double cz = _ax * other._ay - _ay * other._ax;
                    between = Math.Atan2(Math.Sqrt(cx * cx + cy * cy + cz * cz), Math.Abs(_ax * other._ax + _ay * other._ay + _az * other._az));
                }
                else
                {
                    between = Math.Atan2(Math.Abs(_ax * other._ay - _ay * other._ax), Math.Abs(_ax * other._ax + _ay * other._ay));
                }

                return between > sum;
            }

            /// <summary>
            /// Gets a bound the distance between two parts cannot be less than: the greatest gap between the shadows of their
            /// control points on the axes, each part lying in the hull of its control points and a shadow never being longer than
            /// the distance. The axes are those of the plane or of space and the chord of each part and what is square to it, so that two
            /// parts running side by side are told their distance, which their boxes cannot; for two parts in space the direction square to
            /// both chords is an axis too, which tells two segments that pass each other their distance.
            /// </summary>
            internal double Gap(Sub other)
            {
                double gx = other.MinX - MaxX > 0.0 ? other.MinX - MaxX : MinX - other.MaxX > 0.0 ? MinX - other.MaxX : 0.0;
                double gy = other.MinY - MaxY > 0.0 ? other.MinY - MaxY : MinY - other.MaxY > 0.0 ? MinY - other.MaxY : 0.0;
                double gz = other.MinZ - MaxZ > 0.0 ? other.MinZ - MaxZ : MinZ - other.MaxZ > 0.0 ? MinZ - other.MaxZ : 0.0;
                double bound = Math.Sqrt(gx * gx + gy * gy + gz * gz);

                if (_dim == 3)
                {
                    return GapInSpace(other, bound);
                }

                if (_ax != 0.0 || _ay != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, _ax, _ay, 0.0));
                    bound = Math.Max(bound, Shadow(other, -_ay, _ax, 0.0));
                }

                if (other._ax != 0.0 || other._ay != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, other._ax, other._ay, 0.0));
                    bound = Math.Max(bound, Shadow(other, -other._ay, other._ax, 0.0));
                }

                return bound;
            }

            private double GapInSpace(Sub other, double bound)
            {
                if (_ax != 0.0 || _ay != 0.0 || _az != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, _ax, _ay, _az));
                    bound = Math.Max(bound, Shadow(other, _ux, _uy, _uz));
                    bound = Math.Max(bound, Shadow(other, _vx, _vy, _vz));
                }

                if (other._ax != 0.0 || other._ay != 0.0 || other._az != 0.0)
                {
                    bound = Math.Max(bound, Shadow(other, other._ax, other._ay, other._az));
                    bound = Math.Max(bound, Shadow(other, other._ux, other._uy, other._uz));
                    bound = Math.Max(bound, Shadow(other, other._vx, other._vy, other._vz));
                }

                // The direction square to both chords, where they are not parallel.
                double nx = _ay * other._az - _az * other._ay;
                double ny = _az * other._ax - _ax * other._az;
                double nz = _ax * other._ay - _ay * other._ax;
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);

                if (length > 1E-6)
                {
                    bound = Math.Max(bound, Shadow(other, nx / length, ny / length, nz / length));
                }

                return bound;
            }

            private double Shadow(Sub other, double ax, double ay, double az)
            {
                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;

                for (int j = 0; j <= _degree; j++)
                {
                    double along = _px[j] * ax + _py[j] * ay + _pz[j] * az;
                    low = Math.Min(low, along);
                    high = Math.Max(high, along);
                }

                double otherLow = double.PositiveInfinity;
                double otherHigh = double.NegativeInfinity;

                for (int j = 0; j <= other._degree; j++)
                {
                    double along = other._px[j] * ax + other._py[j] * ay + other._pz[j] * az;
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
                left = new Sub(_dim, _degree, _rational, From, middle, AtStart, false, a, _scale);
                right = new Sub(_dim, _degree, _rational, middle, To, false, AtEnd, b, _scale);
            }
        }
    }
}
