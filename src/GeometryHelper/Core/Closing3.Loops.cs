using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// How many times as far as the widest gap an edge left open may stand from another running back alongside it and
        /// still be a side of a gap with it, two sides of the surface standing apart, rather than the rim of a hole, where a
        /// face is missing.
        /// </summary>
        /// <remarks>
        /// A corner of a face standing 0.02 off where the faces beside it have it leaves its edges 0.007 off theirs at
        /// their middles: a gap, too wide for a gap of five thousandths, which it is reported as, and not a hole. A face of
        /// a part a few millimetres across, missing, leaves rims a few millimetres apart: a hole.
        /// </remarks>
        private const double CrackGaps = 8.0;

        /// <summary>
        /// The cosine of the least angle an edge leaving a corner turns back by from the edge arriving for the two to be the
        /// two sides of a gap meeting there: 150 degrees.
        /// </summary>
        private const double Hairpin = -0.8660254037844386;

        /// <summary>
        /// Reads the edges left open: each the way a face closing it would run it, and whether it is a side of a gap, an
        /// edge left open running back alongside it; false where an edge left open is one more than two faces meet on.
        /// </summary>
        /// <param name="work">The work, its faces cleaned and turned.</param>
        /// <param name="rims">The edges left open, in the order of the stretches; empty where nothing is open.</param>
        /// <returns>false, the trouble noted, where a fin stands on an edge left open.</returns>
        /// <remarks>
        /// A stretch one face runs is open, and the face that would close it runs it the other way. A stretch an odd number
        /// of faces more than one run is open past a fin, a face standing off the surface: which of them a face across it
        /// would close with cannot be told, and a face across the loop round the fin would be the fin again turned over,
        /// two faces back to back that read as closed. So would a stretch run by an even number of faces not as many each
        /// way, after the turning. Either is <see cref="ClosingFailure.NonManifold"/>, at the middle of the stretch, found
        /// before anything is welded or filled.
        /// </remarks>
        private static bool TryReadOpenEdges(Work work, out List<Rim> rims)
        {
            if (TryReadOpenEdges(work.Stretches, work.Dropped, work.Turned, work.Options.MaxGap, out rims, out GeoPoint3 at))
            {
                return true;
            }

            work.Refuse(ClosingFailure.NonManifold, at);
            return false;
        }

        /// <summary>
        /// Reads the edges left open, as <see cref="TryReadOpenEdges(Work, out List{Rim})"/> does, of faces some of which
        /// may be dropped or turned over.
        /// </summary>
        /// <param name="stretches">The stretches the edges of the faces lie along.</param>
        /// <param name="dropped">For each face, whether it is dropped; null where none is.</param>
        /// <param name="turned">For each face, whether it is turned over; null where none is.</param>
        /// <param name="maxGap">The widest gap, which says how far apart two sides of a gap may be; see <see cref="CrackGaps"/>.</param>
        /// <param name="rims">The edges left open; empty where nothing is open.</param>
        /// <param name="fin">The middle of the first stretch a fin stands on, where there is one; the origin otherwise.</param>
        /// <returns>false where a fin stands on an edge left open.</returns>
        private static bool TryReadOpenEdges(List<Stretch> stretches, bool[] dropped, bool[] turned, double maxGap, out List<Rim> rims, out GeoPoint3 fin)
        {
            rims = new List<Rim>();
            fin = GeoPoint3.Origin;

            foreach (Stretch stretch in stretches)
            {
                int count = 0, forward = 0;

                for (int r = 0; r < stretch.Faces.Length; r++)
                {
                    int face = stretch.Faces[r];

                    if (dropped != null && dropped[face])
                    {
                        continue;
                    }

                    count++;

                    if (stretch.Forward[r] != (turned != null && turned[face]))
                    {
                        forward++;
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                if (count % 2 != 0 ? count > 1 : 2 * forward != count)
                {
                    fin = stretch.Middle;
                    return false;
                }

                if (count == 1)
                {
                    rims.Add(forward == 1 ? new Rim(stretch.End, stretch.Start) : new Rim(stretch.Start, stretch.End));
                }
            }

            MarkGaps(rims, CrackGaps * maxGap);
            return true;
        }

        /// <summary>
        /// Marks each edge left open that runs back alongside another, the two sides of a gap: running the other way, the
        /// middle of each within a distance of the other, and notes how far its corners stand from the other side.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        /// <param name="window">How far apart the two sides of a gap may be.</param>
        /// <remarks>
        /// The edges are swept along x by where their boxes start, so that only those whose boxes, widened by the distance,
        /// meet are set against each other; each pair in the order of the edges.
        /// </remarks>
        private static void MarkGaps(List<Rim> rims, double window)
        {
            int count = rims.Count;
            var lows = new double[count];
            var highs = new double[count];
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                lows[i] = Math.Min(rims[i].From.X, rims[i].To.X) - window;
                highs[i] = Math.Max(rims[i].From.X, rims[i].To.X) + window;
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byLow = lows[a].CompareTo(lows[b]);
                return byLow != 0 ? byLow : a.CompareTo(b);
            });

            for (int k = 0; k < count; k++)
            {
                int i = order[k];

                for (int m = k + 1; m < count && lows[order[m]] <= highs[i]; m++)
                {
                    int j = order[m];

                    if (RunsAlongside(rims[i], rims[j], window))
                    {
                        rims[i].Beside(rims[j]);
                        rims[j].Beside(rims[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether two edges left open are two sides of a gap: running the other way from each other, the middle
        /// of each within a distance of the other.
        /// </summary>
        /// <param name="one">The one edge.</param>
        /// <param name="other">The other.</param>
        /// <param name="window">The distance.</param>
        /// <remarks>
        /// Two edges meeting at a corner of a hole run on from each other, and at a sharp corner the second turns back
        /// nearly the way the first came, but the middle of each lies away from the other, by half its length times the
        /// sine of the corner's angle.
        /// </remarks>
        private static bool RunsAlongside(Rim one, Rim other, double window)
        {
            GeoVector3 a = one.From.GetVectorTo(one.To);
            GeoVector3 b = other.From.GetVectorTo(other.To);

            if (!(a.DotProduct(b) < 0.0))
            {
                return false;
            }

            GeoPoint3 middle = one.From.GetMiddlePoint(one.To);
            GeoPoint3 otherMiddle = other.From.GetMiddlePoint(other.To);

            return middle.DistanceTo(NearestOnSegment(other.From, other.To, middle)) <= window
                && otherMiddle.DistanceTo(NearestOnSegment(one.From, one.To, otherMiddle)) <= window;
        }

        /// <summary>
        /// How many of the edges left open are sides of gaps.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        private static int Gaps(List<Rim> rims)
        {
            int gaps = 0;

            foreach (Rim rim in rims)
            {
                if (rim.IsGap)
                {
                    gaps++;
                }
            }

            return gaps;
        }

        /// <summary>
        /// Where a gap is widest: half way across it from the corner of a side of it standing furthest from the other side,
        /// the first of them where two stand as far; null where no edge is a side of a gap.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        /// <remarks>
        /// A corner moved off the corner the faces beside it keep leaves a gap from each corner beside it to the other, its
        /// two sides meeting at those corners and standing furthest apart at the corner moved: there is the trouble, and not
        /// at the middle of the first edge left open, which is as far off as the gap is long.
        /// </remarks>
        private static GeoPoint3? WidestGap(List<Rim> rims)
        {
            double widest = -1.0;
            GeoPoint3? at = null;

            foreach (Rim rim in rims)
            {
                if (rim.IsGap && rim.Across > widest)
                {
                    widest = rim.Across;
                    at = rim.AcrossAt;
                }
            }

            return at;
        }

        /// <summary>
        /// Follows the edges left open round into loops, the way a face closing each would run it, their ends matched within
        /// the point tolerance; false where they cannot be followed round one way only.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        /// <param name="tolerance">The tolerance their ends are matched within.</param>
        /// <param name="loops">The loops, in the order their first edges come; empty where nothing is open.</param>
        /// <param name="failure">Why they cannot be followed round, where they cannot; none otherwise.</param>
        /// <param name="at">Where; the origin where they can.</param>
        /// <remarks>
        /// <para>
        /// At a corner more than one loop runs through, each edge arriving goes on along the edge leaving that turns back on
        /// it, where one does and no other: the corner two gaps meet at, each side of one turning back onto its other side.
        /// Where that does not settle it, as at the corner two holes meet at, the loops could be followed either way, and a
        /// face across either way would differ: <see cref="ClosingFailure.NonManifold"/>, at the corner. A corner the open
        /// edges arrive at more often than they leave it, or the other way round, is one the loops do not close at, and the
        /// body is still open there.
        /// </para>
        /// </remarks>
        private static bool TryChainLoops(List<Rim> rims, Tolerance tolerance, out List<Loop> loops, out ClosingFailure failure, out GeoPoint3 at)
        {
            loops = new List<Loop>();
            failure = ClosingFailure.None;
            at = GeoPoint3.Origin;

            if (rims.Count == 0)
            {
                return true;
            }

            var welder = new VertexWelder(tolerance);
            var points = new List<GeoPoint3>();
            int edges = rims.Count;
            var from = new int[edges];
            var to = new int[edges];

            int Index(GeoPoint3 point)
            {
                int index = welder.GetIndex(point);

                if (index == points.Count)
                {
                    points.Add(point);
                }

                return index;
            }

            for (int e = 0; e < edges; e++)
            {
                from[e] = Index(rims[e].From);
                to[e] = Index(rims[e].To);
            }

            var leaving = new List<int>[points.Count];
            var arriving = new List<int>[points.Count];

            for (int e = 0; e < edges; e++)
            {
                // An edge whose ends are one corner within the tolerance closes nothing, and opens nothing.
                if (from[e] == to[e])
                {
                    continue;
                }

                (leaving[from[e]] ?? (leaving[from[e]] = new List<int>())).Add(e);
                (arriving[to[e]] ?? (arriving[to[e]] = new List<int>())).Add(e);
            }

            var next = new int[edges];

            for (int v = 0; v < points.Count; v++)
            {
                int leaves = leaving[v]?.Count ?? 0;
                int arrives = arriving[v]?.Count ?? 0;

                if (leaves != arrives)
                {
                    failure = ClosingFailure.StillOpen;
                    at = points[v];
                    return false;
                }

                if (leaves == 1)
                {
                    next[arriving[v][0]] = leaving[v][0];
                }
                else if (leaves > 1 && !TryPair(arriving[v], leaving[v], rims, next))
                {
                    failure = ClosingFailure.NonManifold;
                    at = points[v];
                    return false;
                }
            }

            var used = new bool[edges];

            for (int seed = 0; seed < edges; seed++)
            {
                if (used[seed] || from[seed] == to[seed])
                {
                    continue;
                }

                var corners = new List<GeoPoint3>();
                bool hole = false;
                int e = seed;

                do
                {
                    used[e] = true;
                    corners.Add(rims[e].From);
                    hole |= !rims[e].IsGap;
                    e = next[e];
                }
                while (e != seed && !used[e]);

                loops.Add(new Loop(corners, hole));
            }

            return true;
        }

        /// <summary>
        /// Pairs the edges arriving at a corner more than one loop runs through with those leaving it: each with the one
        /// that turns back on it, where one does and no other, the last two left with each other; false where that does
        /// not settle every one.
        /// </summary>
        /// <param name="arriving">The edges arriving, by index, in order.</param>
        /// <param name="leaving">The edges leaving, as many.</param>
        /// <param name="rims">The edges left open.</param>
        /// <param name="next">For each edge, the edge it goes on along; set here for those arriving.</param>
        private static bool TryPair(List<int> arriving, List<int> leaving, List<Rim> rims, int[] next)
        {
            var arrivals = new List<int>(arriving);
            var departures = new List<int>(leaving);

            while (arrivals.Count > 1)
            {
                int bestArrival = -1, bestDeparture = -1;
                double most = double.MaxValue;

                foreach (int a in arrivals)
                {
                    foreach (int d in departures)
                    {
                        double turn = Turn(rims[a], rims[d]);

                        if (turn < most)
                        {
                            most = turn;
                            bestArrival = a;
                            bestDeparture = d;
                        }
                    }
                }

                if (!(most <= Hairpin))
                {
                    return false;
                }

                // No other edge turns back as sharply onto either, or the pairing could go another way.
                foreach (int a in arrivals)
                {
                    if (a != bestArrival && Turn(rims[a], rims[bestDeparture]) <= Hairpin)
                    {
                        return false;
                    }
                }

                foreach (int d in departures)
                {
                    if (d != bestDeparture && Turn(rims[bestArrival], rims[d]) <= Hairpin)
                    {
                        return false;
                    }
                }

                next[bestArrival] = bestDeparture;
                arrivals.Remove(bestArrival);
                departures.Remove(bestDeparture);
            }

            next[arrivals[0]] = departures[0];
            return true;
        }

        /// <summary>
        /// The cosine of the angle between an edge arriving at a corner and one leaving it: one straight on, minus one
        /// straight back.
        /// </summary>
        /// <param name="arriving">The edge arriving.</param>
        /// <param name="leaving">The edge leaving.</param>
        private static double Turn(Rim arriving, Rim leaving)
        {
            GeoVector3 a = arriving.From.GetVectorTo(arriving.To);
            GeoVector3 b = leaving.From.GetVectorTo(leaving.To);
            double scale = a.Length * b.Length;

            return scale > 0.0 ? a.DotProduct(b) / scale : 1.0;
        }

        /// <summary>
        /// Notes the first hole, in the order of the loops, where no hole may be filled: the largest hole nought, or the
        /// strategy <see cref="FillStrategy.None"/>. A gap is not a hole, and is the welds'.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="loops">The loops left open.</param>
        private static void RefuseHolesNotToBeFilled(Work work, List<Loop> loops)
        {
            SolidClosingOptions options = work.Options;

            if (options.MaxHoleArea > 0.0 && options.Fill != FillStrategy.None)
            {
                return;
            }

            foreach (Loop loop in loops)
            {
                if (loop.IsHole)
                {
                    work.Refuse(ClosingFailure.HoleTooLarge, PointIn(loop, loops, work.Tolerance));
                    return;
                }
            }
        }

        /// <summary>
        /// A point in a hole, on the plane fitted to its loop: where the loop lies flat, within the face that would fill
        /// it, the loops lying in its plane inside it taken as holes of that face, or within the face it is a hole of; the
        /// middle of its corners otherwise.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="loops">Every loop left open.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static GeoPoint3 PointIn(Loop loop, List<Loop> loops, Tolerance tolerance)
        {
            if (!(loop.Area > 0.0) || loop.OffFlat > tolerance.EqualPlanar)
            {
                return loop.Middle;
            }

            var plane = new GeoPlane3(loop.Middle, loop.Normal);
            var flat = new List<List<GeoPoint3>>();

            foreach (Loop other in loops)
            {
                if (LiesIn(other, plane, tolerance))
                {
                    flat.Add(other.Corners);
                }
            }

            try
            {
                foreach (GeoFace3 face in LoopAssembly.AssembleFacesIn(flat, plane, tolerance))
                {
                    if (HasCorner(face, loop.Corners[0], tolerance) && TryGetPointOn(face, tolerance, out GeoPoint3 inside))
                    {
                        return inside;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Loops that make no face here say only where they are: the middle of the corners, on their plane.
            }

            return loop.Middle;
        }

        /// <summary>
        /// Determines whether every corner of a loop lies within the planar tolerance of a plane, facing either way.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool LiesIn(Loop loop, GeoPlane3 plane, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in loop.Corners)
            {
                if (Math.Abs(plane.SignedDistanceTo(corner)) > tolerance.EqualPlanar)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a point is a corner of a face, of its boundary or of a hole, within the point tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool HasCorner(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (corner.IsEqualTo(point, tolerance))
                {
                    return true;
                }
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    if (corner.IsEqualTo(point, tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The point of a segment nearest a point.
        /// </summary>
        /// <param name="start">Where the segment starts.</param>
        /// <param name="end">Where it ends.</param>
        /// <param name="point">The point.</param>
        private static GeoPoint3 NearestOnSegment(GeoPoint3 start, GeoPoint3 end, GeoPoint3 point)
        {
            GeoVector3 along = start.GetVectorTo(end);
            double squared = along.LengthSquared;

            if (!(squared > 0.0))
            {
                return start;
            }

            double t = start.GetVectorTo(point).DotProduct(along) / squared;
            return start.Add(along.Multiply(Math.Max(0.0, Math.Min(1.0, t))));
        }

        /// <summary>
        /// An edge left open, the way a face closing it would run it, and whether it is a side of a gap: another edge left
        /// open running back alongside it.
        /// </summary>
        private sealed class Rim
        {
            private double _fromAcross = double.PositiveInfinity;
            private double _toAcross = double.PositiveInfinity;
            private GeoPoint3 _fromFoot;
            private GeoPoint3 _toFoot;

            /// <summary>
            /// Initializes an edge left open.
            /// </summary>
            /// <param name="from">Where a face closing it would run it from: a corner of a face.</param>
            /// <param name="to">Where to: another.</param>
            internal Rim(GeoPoint3 from, GeoPoint3 to)
            {
                From = from;
                To = to;
            }

            /// <summary>Gets where a face closing it would run it from.</summary>
            internal GeoPoint3 From { get; }

            /// <summary>Gets where to.</summary>
            internal GeoPoint3 To { get; }

            /// <summary>Gets whether it is a side of a gap.</summary>
            internal bool IsGap { get; private set; }

            /// <summary>
            /// Gets how far its corner furthest from the other side of its gap stands from it, each corner measured to the
            /// nearest edge of the other side; nought where it is no side of a gap.
            /// </summary>
            internal double Across
            {
                get
                {
                    double from = double.IsInfinity(_fromAcross) ? 0.0 : _fromAcross;
                    double to = double.IsInfinity(_toAcross) ? 0.0 : _toAcross;
                    return Math.Max(from, to);
                }
            }

            /// <summary>Gets the point half way across the gap from that corner.</summary>
            internal GeoPoint3 AcrossAt
            {
                get
                {
                    double from = double.IsInfinity(_fromAcross) ? 0.0 : _fromAcross;
                    double to = double.IsInfinity(_toAcross) ? 0.0 : _toAcross;
                    return to > from ? To.GetMiddlePoint(_toFoot) : From.GetMiddlePoint(_fromFoot);
                }
            }

            /// <summary>
            /// Notes another edge left open running back alongside it: it is a side of a gap, and each of its corners is no
            /// further from the other side than from that edge.
            /// </summary>
            /// <param name="other">The other edge.</param>
            internal void Beside(Rim other)
            {
                IsGap = true;

                GeoPoint3 foot = NearestOnSegment(other.From, other.To, From);
                double distance = From.DistanceTo(foot);

                if (distance < _fromAcross)
                {
                    _fromAcross = distance;
                    _fromFoot = foot;
                }

                foot = NearestOnSegment(other.From, other.To, To);
                distance = To.DistanceTo(foot);

                if (distance < _toAcross)
                {
                    _toAcross = distance;
                    _toFoot = foot;
                }
            }
        }

        /// <summary>
        /// A loop of edges left open, run the way a face closing it would run them, and what it is: its area, how far out of
        /// flat, and whether it is a hole or only the sides of gaps.
        /// </summary>
        private sealed class Loop
        {
            /// <summary>
            /// Reads a loop.
            /// </summary>
            /// <param name="corners">Its corners, in order.</param>
            /// <param name="hole">Whether an edge of it is no side of a gap: a hole, where a face is missing.</param>
            internal Loop(List<GeoPoint3> corners, bool hole)
            {
                Corners = corners;
                IsHole = hole;
                int count = corners.Count;
                GeoPoint3 first = corners[0];
                GeoVector3 area = GeoVector3.Zero;
                GeoVector3 sum = GeoVector3.Zero;

                // Measured from its first corner, so that nothing is lost to the size of the coordinates.
                for (int i = 0; i < count; i++)
                {
                    GeoVector3 here = first.GetVectorTo(corners[i]);
                    GeoVector3 next = first.GetVectorTo(corners[(i + 1) % count]);
                    area = area.Add(here.CrossProduct(next));
                    sum = sum.Add(here);
                }

                AreaVector = area.Multiply(0.5);
                Area = AreaVector.Length;
                Middle = first.Add(sum.Divide(count));
                Normal = Area > 0.0 ? AreaVector.Divide(Area) : GeoVector3.Zero;

                double off = 0.0;

                if (Area > 0.0)
                {
                    foreach (GeoPoint3 corner in corners)
                    {
                        off = Math.Max(off, Math.Abs(Middle.GetVectorTo(corner).DotProduct(Normal)));
                    }
                }

                OffFlat = off;
            }

            /// <summary>Gets its corners, in order.</summary>
            internal List<GeoPoint3> Corners { get; }

            /// <summary>Gets whether it is a hole: an edge of it is no side of a gap.</summary>
            internal bool IsHole { get; }

            /// <summary>Gets its area as a vector square to it, by Newell's method, along the way a face closing it would face.</summary>
            internal GeoVector3 AreaVector { get; }

            /// <summary>Gets its area: the length of <see cref="AreaVector"/>, its area seen square to it.</summary>
            internal double Area { get; }

            /// <summary>Gets the middle of its corners.</summary>
            internal GeoPoint3 Middle { get; }

            /// <summary>Gets the unit vector along <see cref="AreaVector"/>; nought where it encloses no area.</summary>
            internal GeoVector3 Normal { get; }

            /// <summary>Gets how far its corner furthest off the plane through its middle square to its area stands off it.</summary>
            internal double OffFlat { get; }
        }
    }
}
