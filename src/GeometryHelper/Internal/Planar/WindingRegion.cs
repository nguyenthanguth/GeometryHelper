using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Internal.Planar
{
    /// <summary>
    /// Which winding numbers count as inside a region traced by loops that may cross themselves and each other.
    /// </summary>
    internal enum FillRule
    {
        /// <summary>Inside where the loops wind round a point a positive number of times.</summary>
        Positive,

        /// <summary>Inside where the loops wind round a point any number of times but zero.</summary>
        NonZero,

        /// <summary>Inside where the loops wind round a point an odd number of times.</summary>
        EvenOdd
    }

    /// <summary>
    /// One boundary loop of a resolved region, running with the region on its left: counter-clockwise round an
    /// outer boundary, clockwise round a hole.
    /// </summary>
    internal sealed class RegionLoop
    {
        public RegionLoop(GeoPoint2[] points, int[] flags, double signedArea)
        {
            Points = points;
            Flags = flags;
            SignedArea = signedArea;
        }

        /// <summary>The corners, without the first repeated at the end.</summary>
        public GeoPoint2[] Points { get; }

        /// <summary>For each corner, the flags of the input edges the boundary leaving it came from.</summary>
        public int[] Flags { get; }

        /// <summary>Positive for an outer boundary, negative for a hole.</summary>
        public double SignedArea { get; }
    }

    /// <summary>
    /// Resolves directed edges into the region they enclose under a fill rule, and hands back the region's
    /// boundary as simple loops.
    /// <para>
    /// The edges are laid out as a planar arrangement: every crossing, every endpoint touching another edge
    /// and every stretch two edges share becomes a vertex, so what is left are pieces that meet only at their
    /// ends. Pieces lying on top of each other are merged, counting how many times the input runs along them
    /// each way. The faces of the arrangement are then traced, each is given its winding number by walking
    /// across the pieces from the unbounded face, which winds zero times, and the boundary of the region is
    /// every piece with a filled face on one side and an empty one on the other.
    /// </para>
    /// <para>
    /// This is what turns a raw offset, whose loops fold over themselves at every tight spot, into the clean
    /// outline it describes; it also reads a polygon whose edges cross under the even-odd rule. Coordinates
    /// are expected near the origin: callers move their input there first, so that the snapping distance means
    /// the same everywhere.
    /// </para>
    /// </summary>
    internal sealed class WindingRegion
    {
        private const int MaxSplitPasses = 8;

        private readonly List<GeoPoint2> _from = new List<GeoPoint2>();
        private readonly List<GeoPoint2> _to = new List<GeoPoint2>();
        private readonly List<int> _flags = new List<int>();

        public int EdgeCount => _from.Count;

        /// <summary>
        /// Adds one directed edge, tagged with flags that follow it into the result.
        /// </summary>
        public void AddEdge(GeoPoint2 from, GeoPoint2 to, int flags)
        {
            _from.Add(from);
            _to.Add(to);
            _flags.Add(flags);
        }

        /// <summary>
        /// Adds a closed loop: an edge from each point to the next, and from the last back to the first.
        /// </summary>
        public void AddLoop(IReadOnlyList<GeoPoint2> loop, int flags)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                AddEdge(loop[i], loop[(i + 1) % loop.Count], flags);
            }
        }

        /// <summary>
        /// Resolves the edges added so far into the boundary loops of the region they fill.
        /// </summary>
        /// <param name="rule">Which winding numbers count as inside.</param>
        /// <param name="snap">
        /// The distance within which two points are one: endpoints are merged, and an endpoint this close to
        /// another edge splits it. It should sit well above rounding noise and well below any feature.
        /// </param>
        /// <returns>The boundary loops, with the region on their left.</returns>
        public List<RegionLoop> Resolve(FillRule rule, double snap)
        {
            List<RegionLoop> result = new List<RegionLoop>();

            if (_from.Count == 0)
            {
                return result;
            }

            if (!(snap > 0.0))
            {
                snap = 1e-12;
            }

            VertexStore store = new VertexStore(snap);
            List<WorkEdge> edges = new List<WorkEdge>(_from.Count);

            for (int i = 0; i < _from.Count; i++)
            {
                int u = store.Add(_from[i]);
                int v = store.Add(_to[i]);

                if (u != v)
                {
                    edges.Add(new WorkEdge(u, v, _flags[i]));
                }
            }

            // Snapping a crossing onto the grid of merged points can move a piece far enough to touch another,
            // so the pieces are checked again until a pass finds nothing new.
            int pass = 0;
            while (pass < MaxSplitPasses && SplitAtCrossings(edges, store, snap))
            {
                pass++;
            }

            if (pass == MaxSplitPasses)
            {
                GeometryHelperLog.Debug("Planar region: crossings were still being found after " + MaxSplitPasses + " passes; the outline may be off near them.");
            }

            Arrangement arrangement = Arrangement.Build(edges, store.Points);

            if (arrangement == null)
            {
                return result;
            }

            return arrangement.ExtractBoundary(rule);
        }

        #region Splitting

        private readonly struct WorkEdge
        {
            public WorkEdge(int u, int v, int flags)
            {
                U = u;
                V = v;
                Flags = flags;
            }

            public int U { get; }

            public int V { get; }

            public int Flags { get; }
        }

        /// <summary>
        /// Splits every edge at every point where another edge crosses or touches it. Returns whether
        /// anything was split.
        /// </summary>
        private static bool SplitAtCrossings(List<WorkEdge> edges, VertexStore store, double snap)
        {
            int count = edges.Count;
            List<GeoPoint2> points = store.Points;
            double[] minX = new double[count];
            double[] maxX = new double[count];
            double[] minY = new double[count];
            double[] maxY = new double[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                GeoPoint2 a = points[edges[i].U];
                GeoPoint2 b = points[edges[i].V];
                minX[i] = Math.Min(a.X, b.X);
                maxX[i] = Math.Max(a.X, b.X);
                minY[i] = Math.Min(a.Y, b.Y);
                maxY[i] = Math.Max(a.Y, b.Y);
                order[i] = i;
            }

            double[] keys = (double[])minX.Clone();
            Array.Sort(keys, order);

            List<int>[] splits = new List<int>[count];
            bool any = false;
            List<int> active = new List<int>();

            foreach (int i in order)
            {
                double left = minX[i] - snap;
                int kept = 0;

                for (int r = 0; r < active.Count; r++)
                {
                    int j = active[r];

                    if (maxX[j] >= left)
                    {
                        active[kept++] = j;
                    }
                }

                active.RemoveRange(kept, active.Count - kept);

                foreach (int j in active)
                {
                    if (maxY[j] < minY[i] - snap || minY[j] > maxY[i] + snap)
                    {
                        continue;
                    }

                    any |= TestPair(edges, i, j, store, snap, splits);
                }

                active.Add(i);
            }

            if (!any)
            {
                return false;
            }

            List<WorkEdge> rebuilt = new List<WorkEdge>(count + 16);

            for (int i = 0; i < count; i++)
            {
                WorkEdge edge = edges[i];

                if (splits[i] == null)
                {
                    rebuilt.Add(edge);
                    continue;
                }

                GeoPoint2 a = points[edge.U];
                GeoVector2 direction = points[edge.V] - a;
                double lengthSquared = direction.LengthSquared;
                List<int> ids = splits[i];
                double[] along = new double[ids.Count];

                for (int k = 0; k < ids.Count; k++)
                {
                    along[k] = (points[ids[k]] - a).Dot(direction) / lengthSquared;
                }

                int[] sorted = ids.ToArray();
                Array.Sort(along, sorted);

                int previous = edge.U;

                foreach (int id in sorted)
                {
                    if (id == previous || id == edge.V)
                    {
                        continue;
                    }

                    rebuilt.Add(new WorkEdge(previous, id, edge.Flags));
                    previous = id;
                }

                if (previous != edge.V)
                {
                    rebuilt.Add(new WorkEdge(previous, edge.V, edge.Flags));
                }
            }

            edges.Clear();
            edges.AddRange(rebuilt);
            return true;
        }

        /// <summary>
        /// Records where two edges touch or cross. Returns whether a split was recorded.
        /// </summary>
        private static bool TestPair(List<WorkEdge> edges, int i, int j, VertexStore store, double snap, List<int>[] splits)
        {
            WorkEdge e1 = edges[i];
            WorkEdge e2 = edges[j];
            List<GeoPoint2> points = store.Points;
            GeoPoint2 a = points[e1.U];
            GeoPoint2 b = points[e1.V];
            GeoPoint2 c = points[e2.U];
            GeoPoint2 d = points[e2.V];
            bool any = false;

            // An endpoint of one lying on the other: a T junction, or the end of a shared stretch.
            if (e2.U != e1.U && e2.U != e1.V) any |= TryAddIncidence(splits, i, a, b, c, e2.U, snap);
            if (e2.V != e1.U && e2.V != e1.V) any |= TryAddIncidence(splits, i, a, b, d, e2.V, snap);
            if (e1.U != e2.U && e1.U != e2.V) any |= TryAddIncidence(splits, j, c, d, a, e1.U, snap);
            if (e1.V != e2.U && e1.V != e2.V) any |= TryAddIncidence(splits, j, c, d, b, e1.V, snap);

            if (e1.U == e2.U || e1.U == e2.V || e1.V == e2.U || e1.V == e2.V)
            {
                return any;
            }

            // A crossing: each edge has its ends on opposite sides of the other, however slightly. The point is
            // then merged into the store like any other, so a crossing within the snap distance of an endpoint
            // becomes that endpoint and the other edge is split there. Asking for the ends to lie clear of the
            // snap distance instead would let two edges cross unnoticed when each passes that close to the
            // other's end but the two ends lie just too far apart to merge: two arcs whose chords meet almost
            // exactly where both have a vertex do that, and the faces traced round a missed crossing are wrong.
            GeoVector2 d1 = b - a;
            double length1 = d1.Length;
            double sideC = d1.Cross(c - a) / length1;
            double sideD = d1.Cross(d - a) / length1;

            if (!((sideC > 0.0 && sideD < 0.0) || (sideC < 0.0 && sideD > 0.0)))
            {
                return any;
            }

            GeoVector2 d2 = d - c;
            double length2 = d2.Length;
            double sideA = d2.Cross(a - c) / length2;
            double sideB = d2.Cross(b - c) / length2;

            if (!((sideA > 0.0 && sideB < 0.0) || (sideA < 0.0 && sideB > 0.0)))
            {
                return any;
            }

            // The distance to the second line falls from sideA to sideB along the first edge; it is zero this
            // far along. The two have opposite signs, so the division is safe and the result lies within the
            // edge, clamped only against rounding.
            double t = Math.Max(0.0, Math.Min(1.0, sideA / (sideA - sideB)));
            int crossing = store.Add(a + d1 * t);

            if (crossing != e1.U && crossing != e1.V)
            {
                AddSplit(splits, i, crossing);
                any = true;
            }

            if (crossing != e2.U && crossing != e2.V)
            {
                AddSplit(splits, j, crossing);
                any = true;
            }

            return any;
        }

        private static bool TryAddIncidence(List<int>[] splits, int edge, GeoPoint2 a, GeoPoint2 b, GeoPoint2 point, int pointId, double snap)
        {
            GeoVector2 direction = b - a;
            double lengthSquared = direction.LengthSquared;

            if (lengthSquared <= 0.0)
            {
                return false;
            }

            double length = Math.Sqrt(lengthSquared);
            double along = (point - a).Dot(direction) / length;

            if (along <= snap || along >= length - snap)
            {
                return false;
            }

            if (Math.Abs(direction.Cross(point - a)) / length > snap)
            {
                return false;
            }

            AddSplit(splits, edge, pointId);
            return true;
        }

        private static void AddSplit(List<int>[] splits, int edge, int pointId)
        {
            List<int> list = splits[edge] ?? (splits[edge] = new List<int>());

            if (!list.Contains(pointId))
            {
                list.Add(pointId);
            }
        }

        #endregion

        #region Vertices

        /// <summary>
        /// The points of the arrangement, with every point added within the snap distance of an earlier one
        /// taken to be that one.
        /// </summary>
        private sealed class VertexStore
        {
            private readonly double _snap;
            private readonly double _snapSquared;
            private readonly Dictionary<(long, long), List<int>> _grid = new Dictionary<(long, long), List<int>>();

            public VertexStore(double snap)
            {
                _snap = snap;
                _snapSquared = snap * snap;
            }

            public List<GeoPoint2> Points { get; } = new List<GeoPoint2>();

            public int Add(GeoPoint2 point)
            {
                long cx = (long)Math.Floor(point.X / _snap);
                long cy = (long)Math.Floor(point.Y / _snap);
                int best = -1;
                double bestDistance = double.MaxValue;

                for (long gx = cx - 1; gx <= cx + 1; gx++)
                {
                    for (long gy = cy - 1; gy <= cy + 1; gy++)
                    {
                        if (!_grid.TryGetValue((gx, gy), out List<int> cell))
                        {
                            continue;
                        }

                        foreach (int id in cell)
                        {
                            double distance = Points[id].DistanceSquaredTo(point);

                            if (distance <= _snapSquared && distance < bestDistance)
                            {
                                best = id;
                                bestDistance = distance;
                            }
                        }
                    }
                }

                if (best >= 0)
                {
                    return best;
                }

                int added = Points.Count;
                Points.Add(point);

                if (!_grid.TryGetValue((cx, cy), out List<int> home))
                {
                    home = new List<int>();
                    _grid.Add((cx, cy), home);
                }

                home.Add(added);
                return added;
            }
        }

        #endregion

        #region Arrangement

        /// <summary>
        /// The pieces left after splitting, merged where they coincide, as a half-edge structure whose faces
        /// carry winding numbers.
        /// </summary>
        private sealed class Arrangement
        {
            private readonly List<GeoPoint2> _points;
            private readonly int[] _origin;
            private readonly int[] _net;
            private readonly int[] _flags;
            private readonly int[] _position;
            private readonly List<int>[] _outgoing;
            private int[] _face;
            private int _faceCount;

            private Arrangement(List<GeoPoint2> points, int[] origin, int[] net, int[] flags)
            {
                _points = points;
                _origin = origin;
                _net = net;
                _flags = flags;
                _position = new int[origin.Length];
                _outgoing = new List<int>[points.Count];
            }

            private int Destination(int halfEdge) => _origin[halfEdge ^ 1];

            public static Arrangement Build(List<WorkEdge> edges, List<GeoPoint2> points)
            {
                // Pieces running between the same two points are one piece, crossed as many times as the input
                // runs along it one way minus the other; one crossed as often each way bounds nothing.
                Dictionary<long, int> slot = new Dictionary<long, int>();
                List<int> lows = new List<int>();
                List<int> highs = new List<int>();
                List<int> nets = new List<int>();
                List<int> flags = new List<int>();

                foreach (WorkEdge edge in edges)
                {
                    int low = Math.Min(edge.U, edge.V);
                    int high = Math.Max(edge.U, edge.V);
                    long key = ((long)low << 32) | (uint)high;

                    if (!slot.TryGetValue(key, out int index))
                    {
                        index = lows.Count;
                        slot.Add(key, index);
                        lows.Add(low);
                        highs.Add(high);
                        nets.Add(0);
                        flags.Add(0);
                    }

                    nets[index] += edge.U == low ? 1 : -1;
                    flags[index] |= edge.Flags;
                }

                List<int> kept = new List<int>();

                for (int i = 0; i < nets.Count; i++)
                {
                    if (nets[i] != 0)
                    {
                        kept.Add(i);
                    }
                }

                if (kept.Count == 0)
                {
                    return null;
                }

                int[] origin = new int[kept.Count * 2];
                int[] net = new int[kept.Count * 2];
                int[] halfFlags = new int[kept.Count * 2];

                for (int k = 0; k < kept.Count; k++)
                {
                    int i = kept[k];
                    origin[2 * k] = lows[i];
                    origin[2 * k + 1] = highs[i];
                    net[2 * k] = nets[i];
                    net[2 * k + 1] = -nets[i];
                    halfFlags[2 * k] = flags[i];
                    halfFlags[2 * k + 1] = flags[i];
                }

                Arrangement arrangement = new Arrangement(points, origin, net, halfFlags);
                arrangement.SortAroundVertices();
                arrangement.TraceFaces();
                return arrangement;
            }

            /// <summary>
            /// Orders the half-edges leaving each vertex counter-clockwise by direction.
            /// </summary>
            private void SortAroundVertices()
            {
                double[] angle = new double[_origin.Length];

                for (int h = 0; h < _origin.Length; h++)
                {
                    GeoPoint2 from = _points[_origin[h]];
                    GeoPoint2 to = _points[Destination(h)];
                    angle[h] = Math.Atan2(to.Y - from.Y, to.X - from.X);

                    List<int> list = _outgoing[_origin[h]] ?? (_outgoing[_origin[h]] = new List<int>());
                    list.Add(h);
                }

                foreach (List<int> list in _outgoing)
                {
                    if (list == null)
                    {
                        continue;
                    }

                    list.Sort((p, q) => angle[p] != angle[q] ? angle[p].CompareTo(angle[q]) : p.CompareTo(q));

                    for (int k = 0; k < list.Count; k++)
                    {
                        _position[list[k]] = k;
                    }
                }
            }

            /// <summary>
            /// The half-edge that follows one round the face on its left: at its destination, the first
            /// half-edge clockwise from the way back.
            /// </summary>
            private int Next(int halfEdge)
            {
                int twin = halfEdge ^ 1;
                List<int> around = _outgoing[_origin[twin]];
                int k = _position[twin];
                return around[(k - 1 + around.Count) % around.Count];
            }

            private void TraceFaces()
            {
                _face = new int[_origin.Length];

                for (int h = 0; h < _face.Length; h++)
                {
                    _face[h] = -1;
                }

                for (int h = 0; h < _face.Length; h++)
                {
                    if (_face[h] >= 0)
                    {
                        continue;
                    }

                    int face = _faceCount++;
                    int walk = h;
                    int guard = 0;

                    do
                    {
                        _face[walk] = face;
                        walk = Next(walk);
                    }
                    while (walk != h && ++guard <= _face.Length);
                }
            }

            public List<RegionLoop> ExtractBoundary(FillRule rule)
            {
                int[] winding = ComputeWindings();
                bool[] filled = new bool[_faceCount];

                for (int f = 0; f < _faceCount; f++)
                {
                    filled[f] = IsFilled(winding[f], rule);
                }

                bool[] boundary = new bool[_origin.Length];

                for (int h = 0; h < _origin.Length; h++)
                {
                    boundary[h] = filled[_face[h]] && !filled[_face[h ^ 1]];
                }

                List<RegionLoop> loops = new List<RegionLoop>();
                bool[] used = new bool[_origin.Length];

                for (int h = 0; h < _origin.Length; h++)
                {
                    if (!boundary[h] || used[h])
                    {
                        continue;
                    }

                    List<GeoPoint2> points = new List<GeoPoint2>();
                    List<int> flags = new List<int>();
                    int walk = h;
                    bool closed = false;

                    while (walk >= 0 && !used[walk])
                    {
                        used[walk] = true;
                        points.Add(_points[_origin[walk]]);
                        flags.Add(_flags[walk]);
                        walk = NextOnBoundary(walk, boundary);

                        if (walk == h)
                        {
                            closed = true;
                            break;
                        }
                    }

                    if (!closed || points.Count < 3)
                    {
                        GeometryHelperLog.Debug("Planar region: a boundary loop did not close and was left out.");
                        continue;
                    }

                    loops.Add(new RegionLoop(points.ToArray(), flags.ToArray(), SignedArea(points)));
                }

                return loops;
            }

            /// <summary>
            /// The boundary half-edge that follows one along the boundary: at its destination, the first
            /// boundary half-edge clockwise from the way back, which keeps the region on the left and takes the
            /// tightest turn, so two parts of a region touching at a point come out as two loops.
            /// </summary>
            private int NextOnBoundary(int halfEdge, bool[] boundary)
            {
                int twin = halfEdge ^ 1;
                List<int> around = _outgoing[_origin[twin]];
                int k = _position[twin];

                for (int step = 1; step <= around.Count; step++)
                {
                    int candidate = around[((k - step) % around.Count + around.Count) % around.Count];

                    if (boundary[candidate])
                    {
                        return candidate;
                    }
                }

                return -1;
            }

            /// <summary>
            /// Gives every face its winding number. Each connected part of the arrangement is entered from its
            /// own outer face, whose winding is what the other parts wind round it; from there every step across
            /// a piece changes the winding by the piece's count.
            /// </summary>
            private int[] ComputeWindings()
            {
                int[] component = FindComponents();
                int componentCount = 0;

                foreach (int c in component)
                {
                    componentCount = Math.Max(componentCount, c + 1);
                }

                // The face of each component with the most negative area is the one outside it.
                double[] area = new double[_faceCount];
                int[] firstEdge = new int[_faceCount];

                for (int f = 0; f < _faceCount; f++)
                {
                    firstEdge[f] = -1;
                }

                GeoPoint2 reference = _points[_origin[0]];

                for (int h = 0; h < _origin.Length; h++)
                {
                    int f = _face[h];
                    GeoVector2 from = _points[_origin[h]] - reference;
                    GeoVector2 to = _points[Destination(h)] - reference;
                    area[f] += from.Cross(to) * 0.5;

                    if (firstEdge[f] < 0)
                    {
                        firstEdge[f] = h;
                    }
                }

                int[] outer = new int[componentCount];

                for (int c = 0; c < componentCount; c++)
                {
                    outer[c] = -1;
                }

                for (int f = 0; f < _faceCount; f++)
                {
                    int c = component[_origin[firstEdge[f]]];

                    if (outer[c] < 0 || area[f] < area[outer[c]])
                    {
                        outer[c] = f;
                    }
                }

                int[] winding = new int[_faceCount];
                bool[] known = new bool[_faceCount];
                int[] faceEdges = GroupEdgesByFace(out int[] faceStart);
                Queue<int> queue = new Queue<int>();
                int conflicts = 0;

                for (int c = 0; c < componentCount; c++)
                {
                    if (outer[c] < 0)
                    {
                        continue;
                    }

                    int start = outer[c];
                    GeoPoint2 probe = _points[_origin[firstEdge[start]]];
                    winding[start] = WindingOfOthers(component, c, probe);
                    known[start] = true;
                    queue.Enqueue(start);

                    while (queue.Count > 0)
                    {
                        int f = queue.Dequeue();

                        for (int k = faceStart[f]; k < faceStart[f + 1]; k++)
                        {
                            int h = faceEdges[k];
                            int across = _face[h ^ 1];
                            int value = winding[f] - _net[h];

                            if (!known[across])
                            {
                                known[across] = true;
                                winding[across] = value;
                                queue.Enqueue(across);
                            }
                            else if (winding[across] != value)
                            {
                                conflicts++;
                            }
                        }
                    }
                }

                if (conflicts > 0)
                {
                    GeometryHelperLog.Debug("Planar region: " + conflicts + " winding conflicts; the input folds too finely for the snap distance.");
                }

                return winding;
            }

            private int[] GroupEdgesByFace(out int[] faceStart)
            {
                faceStart = new int[_faceCount + 1];

                for (int h = 0; h < _origin.Length; h++)
                {
                    faceStart[_face[h] + 1]++;
                }

                for (int f = 0; f < _faceCount; f++)
                {
                    faceStart[f + 1] += faceStart[f];
                }

                int[] fill = (int[])faceStart.Clone();
                int[] grouped = new int[_origin.Length];

                for (int h = 0; h < _origin.Length; h++)
                {
                    grouped[fill[_face[h]]++] = h;
                }

                return grouped;
            }

            /// <summary>
            /// The winding number round a point of every connected part but one. The point lies on that one
            /// part, and so clear of all the others.
            /// </summary>
            private int WindingOfOthers(int[] component, int exclude, GeoPoint2 point)
            {
                int winding = 0;

                for (int h = 0; h < _origin.Length; h += 2)
                {
                    if (component[_origin[h]] == exclude)
                    {
                        continue;
                    }

                    GeoPoint2 a = _points[_origin[h]];
                    GeoPoint2 b = _points[Destination(h)];
                    double side = (b - a).Cross(point - a);

                    if (a.Y <= point.Y)
                    {
                        if (b.Y > point.Y && side > 0.0)
                        {
                            winding += _net[h];
                        }
                    }
                    else if (b.Y <= point.Y && side < 0.0)
                    {
                        winding -= _net[h];
                    }
                }

                return winding;
            }

            private int[] FindComponents()
            {
                int[] parent = new int[_points.Count];

                for (int i = 0; i < parent.Length; i++)
                {
                    parent[i] = i;
                }

                int Find(int x)
                {
                    while (parent[x] != x)
                    {
                        parent[x] = parent[parent[x]];
                        x = parent[x];
                    }

                    return x;
                }

                for (int h = 0; h < _origin.Length; h += 2)
                {
                    int a = Find(_origin[h]);
                    int b = Find(_origin[h + 1]);

                    if (a != b)
                    {
                        parent[a] = b;
                    }
                }

                int[] label = new int[_points.Count];
                Dictionary<int, int> index = new Dictionary<int, int>();

                for (int i = 0; i < parent.Length; i++)
                {
                    int root = Find(i);

                    if (!index.TryGetValue(root, out int c))
                    {
                        c = index.Count;
                        index.Add(root, c);
                    }

                    label[i] = c;
                }

                return label;
            }

            private static bool IsFilled(int winding, FillRule rule)
            {
                switch (rule)
                {
                    case FillRule.Positive: return winding > 0;
                    case FillRule.NonZero: return winding != 0;
                    default: return (winding & 1) != 0;
                }
            }

            private static double SignedArea(List<GeoPoint2> loop)
            {
                double sum = 0.0;
                GeoPoint2 reference = loop[0];

                for (int i = 1; i + 1 < loop.Count; i++)
                {
                    sum += (loop[i] - reference).Cross(loop[i + 1] - reference);
                }

                return sum * 0.5;
            }
        }

        #endregion
    }
}
