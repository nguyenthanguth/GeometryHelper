using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// Picks the parallel curve of an open chain out of the outline of the band the chain sweeps sideways, for
    /// both the plane library and the solid one: the stretches of the outline that lie along the raw offset, less
    /// those that run on past the other leg of a sharp inside corner and those that lie along the chain itself,
    /// put in order along the parallel curve.
    /// </summary>
    internal sealed class ChainRuns
    {
        private readonly SegmentIndex _chain;
        private readonly SegmentIndex _pastTheOtherLeg;
        private readonly double _margin;
        private readonly double _equalPoint;
        private readonly List<List<GeoPoint2>> _runs = new List<List<GeoPoint2>>();

        /// <param name="path">The chain the band was swept from.</param>
        /// <param name="pastTheOtherLeg">The stretches of the raw offset that run on past the other leg of a sharp inside corner.</param>
        /// <param name="extent">The largest coordinate of the band.</param>
        /// <param name="margin">How far an outline point may lie from the edge it came from.</param>
        /// <param name="equalPoint">The point tolerance: an outline stretch this close to the chain lies on it.</param>
        public ChainRuns(IReadOnlyList<GeoPoint2> path, List<(GeoPoint2 From, GeoPoint2 To)> pastTheOtherLeg, double extent, double margin, double equalPoint)
        {
            _margin = margin;
            _equalPoint = equalPoint;
            _chain = new SegmentIndex(extent);

            for (int i = 0; i + 1 < path.Count; i++)
            {
                _chain.Add(path[i], path[i + 1], equalPoint);
            }

            // A stretch no longer than the tolerance is left alone: what it could add is no longer than that.
            _pastTheOtherLeg = new SegmentIndex(extent);

            foreach ((GeoPoint2 from, GeoPoint2 to) in pastTheOtherLeg)
            {
                if (from.DistanceTo(to) > equalPoint)
                {
                    _pastTheOtherLeg.Add(from, to, margin);
                }
            }
        }

        /// <summary>
        /// Collects the stretches of one outline loop that are parallel curve.
        /// </summary>
        /// <param name="loop">The loop, with the band on its left.</param>
        /// <param name="alongRaw">For each edge, whether it lies along the raw offset rather than along the chain, its ends or a detour.</param>
        public void Collect(IReadOnlyList<GeoPoint2> loop, bool[] alongRaw)
        {
            int edges = loop.Count;
            List<GeoPoint2> points = new List<GeoPoint2>(edges);
            List<bool> isOffset = new List<bool>(edges);

            for (int k = 0; k < edges; k++)
            {
                GeoPoint2 p = loop[k];
                GeoPoint2 q = loop[(k + 1) % edges];
                points.Add(p);

                // Off the raw offset, or past the other leg of a sharp inside corner, where the raw offset stands on
                // that leg's far side.
                if (!alongRaw[k] || _pastTheOtherLeg.Count > 0 && _pastTheOtherLeg.Covers(p, q, _margin))
                {
                    isOffset.Add(false);
                    continue;
                }

                // Where the raw offset lies on another part of the chain, that stretch is the chain itself: it is
                // split off the edge, which keeps the rest.
                List<(double Start, double End)> onChain = AlongTheChain(p, q);
                double at = 0.0;

                foreach ((double start, double end) in onChain)
                {
                    if (start > at)
                    {
                        if (at > 0.0)
                        {
                            points.Add(p + (q - p) * at);
                        }

                        isOffset.Add(true);
                    }

                    if (start > 0.0)
                    {
                        points.Add(p + (q - p) * start);
                    }

                    isOffset.Add(false);
                    at = end;
                }

                if (at < 1.0)
                {
                    if (at > 0.0)
                    {
                        points.Add(p + (q - p) * at);
                    }

                    isOffset.Add(true);
                }
            }

            Walk(points, isOffset);
        }

        /// <summary>
        /// The pieces collected, each running the way the chain ran, in order along the parallel curve. Each is
        /// placed by the middle of its longest edge: a piece that runs on across a pinch to a later part of the
        /// curve, or a closed one, has no first point that says where it lies.
        /// </summary>
        /// <param name="raw">The raw offset of the path, running the way the path ran.</param>
        /// <param name="toLeft">Whether the chain was offset to its left, so that the path is the chain and the pieces run against it.</param>
        public List<List<GeoPoint2>> InOrder(List<GeoPoint2> raw, bool toLeft)
        {
            // The band lies on the left of its outline, so the pieces run against the path: with the chain for a
            // right offset and against it for a left one.
            if (toLeft)
            {
                foreach (List<GeoPoint2> run in _runs)
                {
                    run.Reverse();
                }
            }

            if (_runs.Count < 2)
            {
                return _runs;
            }

            List<GeoPoint2> forward = new List<GeoPoint2>(raw);

            if (!toLeft)
            {
                forward.Reverse();
            }

            Positions along = new Positions(forward, _margin);
            int count = _runs.Count;
            double[] keys = new double[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                keys[i] = along.Of(MiddleOfLongestEdge(_runs[i]));
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int compared = keys[a].CompareTo(keys[b]);
                return compared != 0 ? compared : CompareRuns(_runs[a], _runs[b], a, b);
            });

            List<List<GeoPoint2>> sorted = new List<List<GeoPoint2>>(count);

            foreach (int i in order)
            {
                sorted.Add(_runs[i]);
            }

            return sorted;
        }

        /// <summary>
        /// The middle of a piece's longest edge, the first of them on a tie.
        /// </summary>
        private static GeoPoint2 MiddleOfLongestEdge(List<GeoPoint2> run)
        {
            int longest = 0;
            double length = -1.0;

            for (int i = 0; i + 1 < run.Count; i++)
            {
                double edge = run[i].DistanceSquaredTo(run[i + 1]);

                if (edge > length)
                {
                    length = edge;
                    longest = i;
                }
            }

            return PlanarMath.Midpoint(run[longest], run[longest + 1]);
        }

        /// <summary>
        /// The stretches of an edge, as fractions of it in order, that lie along a segment of the chain within the
        /// point tolerance for longer than the tolerance, merged where they overlap.
        /// </summary>
        private List<(double Start, double End)> AlongTheChain(GeoPoint2 p, GeoPoint2 q)
        {
            List<(double Start, double End)> stretches = new List<(double Start, double End)>();
            GeoVector2 direction = q - p;
            double lengthSquared = direction.LengthSquared;
            double length = Math.Sqrt(lengthSquared);

            if (length <= _equalPoint)
            {
                return stretches;
            }

            foreach (int id in _chain.Near(p, q))
            {
                GeoPoint2 a = _chain.From(id);
                GeoPoint2 b = _chain.To(id);
                double ta = (a - p).Dot(direction) / lengthSquared;
                double tb = (b - p).Dot(direction) / lengthSquared;
                double start = Math.Max(0.0, Math.Min(ta, tb));
                double end = Math.Min(1.0, Math.Max(ta, tb));

                // The part of the edge beside the segment lies along it when both its ends do, since the distance
                // to a segment rises and falls only once along a straight edge.
                if ((end - start) * length > _equalPoint
                    && SegmentIndex.DistanceToSegment(p + direction * start, a, b) <= _equalPoint
                    && SegmentIndex.DistanceToSegment(p + direction * end, a, b) <= _equalPoint)
                {
                    stretches.Add((start, end));
                }
            }

            if (stretches.Count < 2)
            {
                return stretches;
            }

            stretches.Sort((x, y) => x.Start != y.Start ? x.Start.CompareTo(y.Start) : x.End.CompareTo(y.End));
            List<(double Start, double End)> merged = new List<(double Start, double End)> { stretches[0] };

            for (int i = 1; i < stretches.Count; i++)
            {
                (double start, double end) = merged[merged.Count - 1];

                if (stretches[i].Start <= end)
                {
                    merged[merged.Count - 1] = (start, Math.Max(end, stretches[i].End));
                }
                else
                {
                    merged.Add(stretches[i]);
                }
            }

            return merged;
        }

        /// <summary>
        /// Walks a loop whose edges are marked as parallel curve or not, and keeps each unbroken stretch of
        /// parallel curve as a piece.
        /// </summary>
        private void Walk(List<GeoPoint2> loop, List<bool> isOffset)
        {
            int count = loop.Count;
            int start = isOffset.IndexOf(false);

            if (start < 0)
            {
                // The whole loop is offset curve: a closed chain offset to where nothing else remains.
                List<GeoPoint2> closed = new List<GeoPoint2>(loop) { loop[0] };
                _runs.Add(closed);
                return;
            }

            List<GeoPoint2> current = null;

            for (int step = 1; step <= count; step++)
            {
                int edge = (start + step) % count;

                if (isOffset[edge])
                {
                    if (current == null)
                    {
                        current = new List<GeoPoint2> { loop[edge] };
                    }

                    current.Add(loop[(edge + 1) % count]);
                }
                else if (current != null)
                {
                    _runs.Add(current);
                    current = null;
                }
            }

            if (current != null)
            {
                _runs.Add(current);
            }
        }

        /// <summary>
        /// Orders two pieces at the same place along the parallel curve by their points, then by their number,
        /// so that no order is left to the sort.
        /// </summary>
        private static int CompareRuns(List<GeoPoint2> a, List<GeoPoint2> b, int indexA, int indexB)
        {
            int shared = Math.Min(a.Count, b.Count);

            for (int i = 0; i < shared; i++)
            {
                int compared = a[i].X.CompareTo(b[i].X);

                if (compared == 0)
                {
                    compared = a[i].Y.CompareTo(b[i].Y);
                }

                if (compared != 0)
                {
                    return compared;
                }
            }

            int byCount = a.Count.CompareTo(b.Count);
            return byCount != 0 ? byCount : indexA.CompareTo(indexB);
        }

        /// <summary>
        /// The arc length along a chain to the point on it nearest a given point, found through a grid of the
        /// chain's segments: the same answer as walking every segment, in time that does not grow with the chain.
        /// </summary>
        private sealed class Positions
        {
            private readonly List<GeoPoint2> _chain;
            private readonly double[] _travelled;
            private readonly SegmentIndex _index;
            private readonly double _reach;

            public Positions(List<GeoPoint2> chain, double margin)
            {
                _chain = chain;
                _reach = margin;
                _travelled = new double[Math.Max(chain.Count - 1, 0)];
                _index = new SegmentIndex(Extent(chain));
                double travelled = 0.0;

                for (int i = 0; i + 1 < chain.Count; i++)
                {
                    _travelled[i] = travelled;
                    travelled += (chain[i + 1] - chain[i]).Length;

                    // Registered twice as wide as the reach, so that every segment within the reach of a point is
                    // in that point's cell whatever the rounding.
                    _index.Add(chain[i], chain[i + 1], 2.0 * margin);
                }
            }

            public double Of(GeoPoint2 point)
            {
                double best = double.MaxValue;
                double position = 0.0;

                foreach (int i in _index.Near(point))
                {
                    Nearer(i, point, ref best, ref position);
                }

                // Nothing within the reach: the nearest segment may lie in another cell, so all are walked.
                if (best > _reach * _reach)
                {
                    best = double.MaxValue;

                    for (int i = 0; i + 1 < _chain.Count; i++)
                    {
                        Nearer(i, point, ref best, ref position);
                    }
                }

                return position;
            }

            /// <summary>
            /// Takes segment <paramref name="i"/> when it comes nearer the point than the best so far; on a tie
            /// the earlier segment stays.
            /// </summary>
            private void Nearer(int i, GeoPoint2 point, ref double best, ref double position)
            {
                GeoPoint2 a = _chain[i];
                GeoVector2 direction = _chain[i + 1] - a;
                double length = direction.Length;
                double along = length > 0.0 ? Math.Max(0.0, Math.Min(length, (point - a).Dot(direction) / length)) : 0.0;
                GeoPoint2 nearest = length > 0.0 ? a + direction * (along / length) : a;
                double distance = nearest.DistanceSquaredTo(point);

                if (distance < best)
                {
                    best = distance;
                    position = _travelled[i] + along;
                }
            }

            private static double Extent(List<GeoPoint2> chain)
            {
                double extent = 0.0;

                foreach (GeoPoint2 point in chain)
                {
                    extent = Math.Max(extent, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
                }

                return extent;
            }
        }
    }
}
