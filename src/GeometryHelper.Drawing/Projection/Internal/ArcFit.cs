using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// Fits arcs and circles back to the chains of short lines a faceted circle leaves: the rim of a hole, a tube seen along
    /// its axis, a rounded corner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lines of the same visibility, kind and objects that meet end to end, two at a corner, form a chain. A stretch of a
    /// chain of three lines or more that turns the same way at each corner, by no more than the smooth angle, and whose
    /// corners lie on one circle within the point tolerance, becomes an arc of that circle; a closed chain that does so all
    /// the way round becomes a circle. A chain that does not, a hexagon or the ellipse a hole seen at a slant leaves, stays
    /// lines, and so does a stretch whose corners stand no further than twice the tolerance off its chord, which is a line.
    /// </para>
    /// <para>
    /// Where a hole's rim is part seen and part hidden, each part ends where the face in front of it begins, in the middle
    /// of a facet: that end lies on the facet's chord, off the circle by up to the facet's sagitta. Such an end is not used to
    /// find the circle, is allowed off it by the sagitta, and the arc ends where the circle passes it.
    /// </para>
    /// </remarks>
    internal static class ArcFit
    {
        internal static List<ProjectedEdge2> Run(List<Piece2> merged, double maxTurn, double tol, string[] ids)
        {
            var pieces = new List<Piece2>(merged);
            pieces.Sort(CompareByClass);
            var edges = new List<ProjectedEdge2>(pieces.Count);
            int lo = 0;

            while (lo < pieces.Count)
            {
                int hi = lo + 1;

                while (hi < pieces.Count && SameClass(pieces[lo], pieces[hi]))
                {
                    hi++;
                }

                Piece2 sample = pieces[lo];
                var objectIds = new string[sample.Objects.Length];

                for (int k = 0; k < objectIds.Length; k++)
                {
                    objectIds[k] = ids[sample.Objects[k]];
                }

                new Chains(pieces, lo, hi, tol, maxTurn, sample.Visibility, sample.Kind, objectIds, edges).Fit();
                lo = hi;
            }

            return edges;
        }

        private static bool SameClass(Piece2 a, Piece2 b) =>
            a.Visibility == b.Visibility && a.Kind == b.Kind && Piece2.CompareObjects(a.Objects, b.Objects) == 0;

        private static int CompareByClass(Piece2 a, Piece2 b)
        {
            int c = a.Visibility.CompareTo(b.Visibility);
            if (c != 0)
                return c;
            c = a.Kind.CompareTo(b.Kind);
            if (c != 0)
                return c;
            c = Piece2.CompareObjects(a.Objects, b.Objects);
            return c != 0 ? c : Piece2.Compare(a, b);
        }

        /// <summary>
        /// The chains of the lines of one class, and the arcs fitted to them.
        /// </summary>
        private sealed class Chains
        {
            private readonly List<Piece2> _pieces;
            private readonly int _lo;
            private readonly int _hi;
            private readonly double _tol;
            private readonly double _maxTurn;
            private readonly EdgeVisibility _visibility;
            private readonly EdgeKind _kind;
            private readonly string[] _objectIds;
            private readonly List<ProjectedEdge2> _edges;
            private readonly List<double> _nx = new List<double>();
            private readonly List<double> _ny = new List<double>();
            private readonly Dictionary<CellKey, int> _heads = new Dictionary<CellKey, int>();
            private readonly List<int> _next = new List<int>();

            internal Chains(
                List<Piece2> pieces,
                int lo,
                int hi,
                double tol,
                double maxTurn,
                EdgeVisibility visibility,
                EdgeKind kind,
                string[] objectIds,
                List<ProjectedEdge2> edges
            )
            {
                _pieces = pieces;
                _lo = lo;
                _hi = hi;
                _tol = tol;
                _maxTurn = maxTurn + 1E-9;
                _visibility = visibility;
                _kind = kind;
                _objectIds = objectIds;
                _edges = edges;
            }

            internal void Fit()
            {
                int count = _hi - _lo;
                var ends = new int[2 * count];

                for (int i = 0; i < count; i++)
                {
                    Piece2 p = _pieces[_lo + i];
                    ends[2 * i] = Node(p.X0, p.Y0);
                    ends[2 * i + 1] = Node(p.X1, p.Y1);
                }

                var at = new List<int>[_nx.Count];

                for (int v = 0; v < at.Length; v++)
                {
                    at[v] = new List<int>(2);
                }

                for (int i = 0; i < count; i++)
                {
                    if (ends[2 * i] == ends[2 * i + 1])
                    {
                        Piece2 p = _pieces[_lo + i];
                        Line(p.X0, p.Y0, p.X1, p.Y1);
                        continue;
                    }

                    at[ends[2 * i]].Add(i);
                    at[ends[2 * i + 1]].Add(i);
                }

                var used = new bool[count];
                var nodes = new List<int>();

                for (int v = 0; v < at.Length; v++)
                {
                    if (at[v].Count == 2)
                    {
                        continue;
                    }

                    foreach (int piece in at[v])
                    {
                        if (!used[piece])
                        {
                            bool closed = Walk(v, piece, ends, at, used, nodes);
                            FitChain(nodes, closed, at[nodes[0]].Count == 1, at[nodes[nodes.Count - 1]].Count == 1);
                        }
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    if (!used[i] && ends[2 * i] != ends[2 * i + 1])
                    {
                        bool closed = Walk(ends[2 * i], i, ends, at, used, nodes);
                        FitChain(nodes, closed, false, false);
                    }
                }
            }

            /// <summary>
            /// Follows a chain from a corner along a line, through corners where two lines meet; true where it comes back.
            /// </summary>
            private static bool Walk(int start, int piece, int[] ends, List<int>[] at, bool[] used, List<int> nodes)
            {
                nodes.Clear();
                nodes.Add(start);
                int current = start;

                while (true)
                {
                    used[piece] = true;
                    int next = ends[2 * piece] == current ? ends[2 * piece + 1] : ends[2 * piece];
                    nodes.Add(next);

                    if (next == start)
                    {
                        return true;
                    }

                    if (at[next].Count != 2)
                    {
                        return false;
                    }

                    int other = at[next][0] == piece ? at[next][1] : at[next][0];

                    if (used[other])
                    {
                        return false;
                    }

                    piece = other;
                    current = next;
                }
            }

            /// <summary>
            /// Fits the arcs of one chain and gives its lines; the chain is a list of corners, its last the first again where
            /// it is closed.
            /// </summary>
            private void FitChain(List<int> nodes, bool closed, bool openStart, bool openEnd)
            {
                int n = nodes.Count - 1;
                var xs = new double[n + 1];
                var ys = new double[n + 1];

                if (closed)
                {
                    // A closed chain turning alike at every corner, its corners on one circle, is a circle.
                    bool round = n >= 3;
                    double sign = 0.0;

                    for (int k = 0; k < n && round; k++)
                    {
                        double turn = Turn(nodes[(k + n - 1) % n], nodes[k], nodes[k + 1]);
                        round = Smooth(turn) && (sign == 0.0 || Math.Sign(turn) == sign);
                        sign = Math.Sign(turn);
                    }

                    if (round)
                    {
                        for (int k = 0; k <= n; k++)
                        {
                            xs[k] = _nx[nodes[k]];
                            ys[k] = _ny[nodes[k]];
                        }

                        if (
                            TryCircle(xs, ys, 0, n - 1, out double cx, out double cy, out double r)
                            && Within(xs, ys, 0, n - 1, cx, cy, r, _tol)
                        )
                        {
                            _edges.Add(
                                new ProjectedEdge2(
                                    new GeoArc2(new GeoPoint2(cx, cy), r, 0.0, 0.0),
                                    _visibility,
                                    _kind,
                                    _objectIds
                                )
                            );
                            return;
                        }
                    }

                    // Otherwise the chain is read from a corner where it does not turn smoothly, the lowest such, or failing
                    // that from its lowest corner.
                    int from = -1;

                    for (int k = 0; k < n; k++)
                    {
                        double turn = Turn(nodes[(k + n - 1) % n], nodes[k], nodes[k + 1]);

                        if (!Smooth(turn) && (from < 0 || nodes[k] < nodes[from]))
                        {
                            from = k;
                        }
                    }

                    if (from < 0)
                    {
                        from = 0;

                        for (int k = 1; k < n; k++)
                        {
                            if (nodes[k] < nodes[from])
                            {
                                from = k;
                            }
                        }
                    }

                    var turned = new List<int>(n + 1);

                    for (int k = 0; k <= n; k++)
                    {
                        turned.Add(nodes[(from + k) % n]);
                    }

                    nodes = turned;
                    openStart = false;
                    openEnd = false;
                }

                for (int k = 0; k <= n; k++)
                {
                    xs[k] = _nx[nodes[k]];
                    ys[k] = _ny[nodes[k]];
                }

                var turns = new double[n + 1];

                for (int k = 1; k < n; k++)
                {
                    turns[k] = Turn(nodes[k - 1], nodes[k], nodes[k + 1]);
                }

                int i = 0;

                while (i < n)
                {
                    int m = i + 1;

                    while (m < n && Smooth(turns[m]) && (m == i + 1 || Math.Sign(turns[m]) == Math.Sign(turns[i + 1])))
                    {
                        m++;
                    }

                    if (m - i >= 3)
                    {
                        int best = TryRun(xs, ys, turns, i, m, n, openStart, openEnd) ? m : -1;

                        // The whole stretch is not one arc: the longest that is, from its start, found by halving.
                        if (best < 0)
                        {
                            for (int low = i + 3, high = m - 1; low <= high; )
                            {
                                int middle = (low + high) / 2;

                                if (TryRun(xs, ys, turns, i, middle, n, openStart, openEnd))
                                {
                                    best = middle;
                                    low = middle + 1;
                                }
                                else
                                {
                                    high = middle - 1;
                                }
                            }
                        }

                        if (best >= 0)
                        {
                            EmitArc(xs, ys, turns, i, best, n, openStart, openEnd);
                            i = best;
                            continue;
                        }
                    }

                    Line(xs[i], ys[i], xs[i + 1], ys[i + 1]);
                    i++;
                }
            }

            /// <summary>
            /// Whether the corners from i to m lie on one circle as an arc of the chain should, with the circle found.
            /// </summary>
            private bool TryRun(
                double[] xs,
                double[] ys,
                double[] turns,
                int i,
                int m,
                int n,
                bool openStart,
                bool openEnd
            ) => TryRun(xs, ys, turns, i, m, n, openStart, openEnd, out _, out _, out _);

            private bool TryRun(
                double[] xs,
                double[] ys,
                double[] turns,
                int i,
                int m,
                int n,
                bool openStart,
                bool openEnd,
                out double cx,
                out double cy,
                out double r
            )
            {
                cx = cy = r = 0.0;
                bool freeStart = openStart && i == 0;
                bool freeEnd = openEnd && m == n;
                int a = freeStart ? i + 1 : i;
                int b = freeEnd ? m - 1 : m;

                if (b - a + 1 < 3)
                {
                    a = i;
                    b = m;
                    freeStart = freeEnd = false;
                }

                if (!TryCircle(xs, ys, a, b, out cx, out cy, out r) || !Within(xs, ys, a, b, cx, cy, r, _tol))
                {
                    return false;
                }

                double steepest = 0.0;

                for (int k = i + 1; k < m; k++)
                {
                    steepest = Math.Max(steepest, Math.Abs(turns[k]));
                }

                double sagitta = r * (1.0 - Math.Cos(0.5 * steepest)) + _tol;

                if (
                    freeStart && !Within(xs, ys, i, i, cx, cy, r, sagitta)
                    || freeEnd && !Within(xs, ys, m, m, cx, cy, r, sagitta)
                )
                {
                    return false;
                }

                // A stretch that stands off its chord by no more than twice the tolerance is a line, not an arc.
                double chordX = xs[m] - xs[i];
                double chordY = ys[m] - ys[i];
                double chord = Math.Sqrt(chordX * chordX + chordY * chordY);
                double off = 0.0;

                for (int k = i + 1; k < m; k++)
                {
                    double d =
                        chord > 0.0
                            ? Math.Abs(chordX * (ys[k] - ys[i]) - chordY * (xs[k] - xs[i])) / chord
                            : Math.Sqrt((xs[k] - xs[i]) * (xs[k] - xs[i]) + (ys[k] - ys[i]) * (ys[k] - ys[i]));
                    off = Math.Max(off, d);
                }

                if (!(off > 2.0 * _tol))
                {
                    return false;
                }

                // Each line has to be a chord the arc runs along the way the chain turns.
                double sign = Math.Sign(turns[i + 1]);

                for (int k = i; k < m; k++)
                {
                    double ux = xs[k] - cx;
                    double uy = ys[k] - cy;
                    double vx = xs[k + 1] - cx;
                    double vy = ys[k + 1] - cy;

                    if (Math.Sign(ux * vy - uy * vx) != sign)
                    {
                        return false;
                    }
                }

                return true;
            }

            private void EmitArc(
                double[] xs,
                double[] ys,
                double[] turns,
                int i,
                int m,
                int n,
                bool openStart,
                bool openEnd
            )
            {
                TryRun(xs, ys, turns, i, m, n, openStart, openEnd, out double cx, out double cy, out double r);
                double start = Math.Atan2(ys[i] - cy, xs[i] - cx);
                double end = Math.Atan2(ys[m] - cy, xs[m] - cx);
                var center = new GeoPoint2(cx, cy);
                GeoArc2 arc =
                    turns[i + 1] > 0.0 ? new GeoArc2(center, r, start, end) : new GeoArc2(center, r, end, start);
                _edges.Add(new ProjectedEdge2(arc, _visibility, _kind, _objectIds));
            }

            private void Line(double x0, double y0, double x1, double y1)
            {
                bool forward = x0 < x1 || x0 == x1 && y0 <= y1;
                var line = forward ? new GeoLine2(x0, y0, x1, y1) : new GeoLine2(x1, y1, x0, y0);
                _edges.Add(new ProjectedEdge2(line, _visibility, _kind, _objectIds));
            }

            private bool Smooth(double turn) => Math.Abs(turn) <= _maxTurn && Math.Abs(turn) > 1E-12;

            /// <summary>
            /// The angle the chain turns by at a corner, positive to the left.
            /// </summary>
            private double Turn(int before, int at, int after)
            {
                double ax = _nx[at] - _nx[before];
                double ay = _ny[at] - _ny[before];
                double bx = _nx[after] - _nx[at];
                double by = _ny[after] - _ny[at];
                return Math.Atan2(ax * by - ay * bx, ax * bx + ay * by);
            }

            /// <summary>
            /// The corner of a point, made one with a corner given before within the tolerance, the first given standing.
            /// </summary>
            private int Node(double x, double y)
            {
                double cell = Math.Max(_tol, 1E-9);
                long ix = (long)Math.Floor(x / cell);
                long iy = (long)Math.Floor(y / cell);
                int best = -1;

                for (long i = ix - 1; i <= ix + 1; i++)
                {
                    for (long j = iy - 1; j <= iy + 1; j++)
                    {
                        if (!_heads.TryGetValue(new CellKey(i, j), out int at))
                        {
                            continue;
                        }

                        for (; at >= 0; at = _next[at])
                        {
                            double dx = _nx[at] - x;
                            double dy = _ny[at] - y;

                            if (dx * dx + dy * dy <= _tol * _tol && (best < 0 || at < best))
                            {
                                best = at;
                            }
                        }
                    }
                }

                if (best >= 0)
                {
                    return best;
                }

                int id = _nx.Count;
                _nx.Add(x);
                _ny.Add(y);
                var key = new CellKey(ix, iy);
                _next.Add(_heads.TryGetValue(key, out int head) ? head : -1);
                _heads[key] = id;
                return id;
            }
        }

        /// <summary>
        /// The circle nearest the corners from a to b, by least squares on the algebraic distance about their middle.
        /// </summary>
        private static bool TryCircle(
            double[] xs,
            double[] ys,
            int a,
            int b,
            out double cx,
            out double cy,
            out double r
        )
        {
            int count = b - a + 1;
            double mx = 0.0,
                my = 0.0;

            for (int k = a; k <= b; k++)
            {
                mx += xs[k];
                my += ys[k];
            }

            mx /= count;
            my /= count;
            double suu = 0.0,
                svv = 0.0,
                suv = 0.0,
                suuu = 0.0,
                svvv = 0.0,
                suvv = 0.0,
                svuu = 0.0;

            for (int k = a; k <= b; k++)
            {
                double u = xs[k] - mx;
                double v = ys[k] - my;
                suu += u * u;
                svv += v * v;
                suv += u * v;
                suuu += u * u * u;
                svvv += v * v * v;
                suvv += u * v * v;
                svuu += v * u * u;
            }

            double det = suu * svv - suv * suv;

            if (!(Math.Abs(det) > 1E-12 * (suu + svv) * (suu + svv)))
            {
                cx = cy = r = 0.0;
                return false;
            }

            double bu = 0.5 * (suuu + suvv);
            double bv = 0.5 * (svvv + svuu);
            double uc = (bu * svv - bv * suv) / det;
            double vc = (bv * suu - bu * suv) / det;
            cx = mx + uc;
            cy = my + vc;
            r = Math.Sqrt(uc * uc + vc * vc + (suu + svv) / count);
            return r > 0.0 && !double.IsInfinity(r);
        }

        private static bool Within(double[] xs, double[] ys, int a, int b, double cx, double cy, double r, double reach)
        {
            for (int k = a; k <= b; k++)
            {
                double d = Math.Sqrt((xs[k] - cx) * (xs[k] - cx) + (ys[k] - cy) * (ys[k] - cy));

                if (!(Math.Abs(d - r) <= reach))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A cell of the grid corners are made one in, as a key with a hash of its own.
        /// </summary>
        private readonly struct CellKey : IEquatable<CellKey>
        {
            private readonly long _x;
            private readonly long _y;

            internal CellKey(long x, long y)
            {
                _x = x;
                _y = y;
            }

            public bool Equals(CellKey other) => _x == other._x && _y == other._y;

            public override bool Equals(object obj) => obj is CellKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    long h = _x * 73856093L ^ _y * 19349663L;
                    return (int)h ^ (int)(h >> 32);
                }
            }
        }
    }
}
