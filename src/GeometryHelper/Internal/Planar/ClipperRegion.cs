using System;
using System.Collections.Generic;
using Clipper2Lib;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The bridge to Clipper2, which resolves regions for <see cref="Boolean2"/> and <see cref="Offset2"/>.
    /// <para>
    /// Everything crossing it is in a local frame: the caller moves its shapes so that one of their vertices
    /// sits at the origin, because Clipper2 rounds coordinates to a fixed number of decimal places and works on
    /// the integers that gives. Near the origin eight decimal places fit comfortably; a drawing hundreds of
    /// kilometres out would not, and would lose the digits that matter.
    /// </para>
    /// <para>
    /// A region handed to Clipper2 is a set of loops read under a fill rule; what comes back is cleaned up in
    /// three steps before the rest of the library sees it. A loop that touches itself at a vertex, as two pieces
    /// meeting at a point come back, is split there into simple loops. Every vertex then gets its full
    /// precision back: one that is an input vertex rounded is replaced by that vertex, and one where two input
    /// edges cross is replaced by their crossing computed in double precision, so rounding to the grid leaves
    /// no trace in the answer. Last, points closer than the point tolerance are merged, loops thinner than it
    /// are dropped, and the loops are grouped, every outer boundary with the holes directly inside it.
    /// </para>
    /// </summary>
    internal static class ClipperRegion
    {
        /// <summary>
        /// Chooses how many decimal places Clipper2 rounds to: eight, its finest, unless the drawing is so large
        /// that the integers would grow past 1e14, which leaves room for the products its tests form.
        /// </summary>
        public static int GetPrecision(double extent)
        {
            int precision = 8;
            double size = Math.Max(extent, 1.0);

            while (precision > 0 && size * Math.Pow(10.0, precision) > 1e14)
            {
                precision--;
            }

            return precision;
        }

        /// <summary>
        /// The distance below which a point lying off a straight run counts as lying on it, used to drop the
        /// points splitting leaves along straight edges: a hundredth of the point tolerance, but never finer
        /// than the grid Clipper2 rounds to.
        /// </summary>
        public static double GetStraightness(Tolerance tolerance, int precision)
        {
            return Math.Max(tolerance.EqualPoint * 1e-2, 4.0 * Math.Pow(10.0, -precision));
        }

        /// <summary>
        /// Resolves a region given by loops under a fill rule and groups the result.
        /// </summary>
        public static List<LoopGroup> Resolve(IEnumerable<IReadOnlyList<GeoPoint2>> loops, Clipper2Lib.FillRule rule, int precision, Tolerance tolerance)
        {
            return Execute(ClipType.Union, loops, null, rule, precision, tolerance);
        }

        /// <summary>
        /// Combines two regions, each given by loops, and groups the result.
        /// </summary>
        public static List<LoopGroup> Execute(
            ClipType clipType,
            IEnumerable<IReadOnlyList<GeoPoint2>> subject,
            IEnumerable<IReadOnlyList<GeoPoint2>> clip,
            Clipper2Lib.FillRule rule,
            int precision,
            Tolerance tolerance)
        {
            List<IReadOnlyList<GeoPoint2>> inputs = new List<IReadOnlyList<GeoPoint2>>(subject);
            ClipperD clipper = new ClipperD(precision);
            clipper.AddSubject(ToPaths(inputs));

            if (clip != null)
            {
                List<IReadOnlyList<GeoPoint2>> clipLoops = new List<IReadOnlyList<GeoPoint2>>(clip);
                clipper.AddClip(ToPaths(clipLoops));
                inputs.AddRange(clipLoops);
            }

            PathsD solution = new PathsD();
            clipper.Execute(clipType, rule, solution);

            ExactPoints exact = new ExactPoints(inputs, precision);
            double straight = GetStraightness(tolerance, precision);
            List<List<GeoPoint2>> loops = new List<List<GeoPoint2>>();

            foreach (PathD path in solution)
            {
                foreach (List<GeoPoint2> piece in SplitAtRepeats(path))
                {
                    List<GeoPoint2> cleaned = Clean(exact.Restore(piece), tolerance, straight);

                    if (cleaned != null)
                    {
                        loops.Add(cleaned);
                    }
                }
            }

            return LoopTools.Group(loops);
        }

        /// <summary>
        /// Resolves loops under a fill rule and returns every boundary loop of the result, flat, with collinear
        /// points kept, so that each edge still lies along the input edge it came from. The vertices get their
        /// full precision back, but nothing is merged or dropped.
        /// </summary>
        public static List<List<GeoPoint2>> ResolveOutline(IEnumerable<IReadOnlyList<GeoPoint2>> loops, Clipper2Lib.FillRule rule, int precision)
        {
            List<IReadOnlyList<GeoPoint2>> inputs = new List<IReadOnlyList<GeoPoint2>>(loops);
            ClipperD clipper = new ClipperD(precision) { PreserveCollinear = true };
            clipper.AddSubject(ToPaths(inputs));

            PathsD solution = new PathsD();
            clipper.Execute(ClipType.Union, rule, solution);

            ExactPoints exact = new ExactPoints(inputs, precision);
            List<List<GeoPoint2>> result = new List<List<GeoPoint2>>(solution.Count);

            foreach (PathD path in solution)
            {
                List<GeoPoint2> loop = new List<GeoPoint2>(path.Count);

                foreach (PointD point in path)
                {
                    loop.Add(new GeoPoint2(point.x, point.y));
                }

                result.Add(exact.Restore(loop));
            }

            return result;
        }

        #region Regions of the library's shapes

        /// <summary>
        /// The loops of a polygon's region, read under the even-odd rule as <see cref="Containment2"/> reads it,
        /// resolved into boundary loops with the region on their left.
        /// </summary>
        public static List<List<GeoPoint2>> RegionOf(GeoPolygon2 polygon, GeoPoint2 origin, int precision, Tolerance tolerance)
        {
            return Flatten(Resolve(new[] { ToLocal(polygon.Vertices, origin) }, Clipper2Lib.FillRule.EvenOdd, precision, tolerance));
        }

        /// <summary>
        /// The loops of a face's region: inside its boundary and outside every hole, each read under the
        /// even-odd rule. The boundary's loops keep their winding and the holes' loops are reversed, so under the
        /// positive rule a point counts once for the boundary and loses one for every hole it lies in.
        /// </summary>
        public static List<List<GeoPoint2>> RegionOf(GeoFace2 face, GeoPoint2 origin, int precision, Tolerance tolerance)
        {
            List<List<GeoPoint2>> loops = RegionOf(face.Boundary, origin, precision, tolerance);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                foreach (List<GeoPoint2> loop in RegionOf(hole, origin, precision, tolerance))
                {
                    loop.Reverse();
                    loops.Add(loop);
                }
            }

            return loops;
        }

        /// <summary>
        /// Every loop of a grouped region, outer boundaries and holes alike.
        /// </summary>
        public static List<List<GeoPoint2>> Flatten(List<LoopGroup> groups)
        {
            List<List<GeoPoint2>> loops = new List<List<GeoPoint2>>();

            foreach (LoopGroup group in groups)
            {
                loops.Add(group.Outer);
                loops.AddRange(group.Holes);
            }

            return loops;
        }

        #endregion

        #region Conversion

        public static List<GeoPoint2> ToLocal(IReadOnlyList<GeoPoint2> points, GeoPoint2 origin)
        {
            List<GeoPoint2> local = new List<GeoPoint2>(points.Count);

            foreach (GeoPoint2 point in points)
            {
                local.Add(new GeoPoint2(point.X - origin.X, point.Y - origin.Y));
            }

            return local;
        }

        /// <summary>
        /// Builds a polygon from a cleaned loop, reversed when asked. The loop has already been cleaned against
        /// the caller's tolerance, so the validated constructor is used rather than filtering again against the
        /// global one.
        /// </summary>
        public static GeoPolygon2 ToPolygon(IReadOnlyList<GeoPoint2> loop, GeoPoint2 origin, bool reverse)
        {
            GeoPoint2[] points = new GeoPoint2[loop.Count];

            for (int i = 0; i < loop.Count; i++)
            {
                GeoPoint2 local = reverse ? loop[loop.Count - 1 - i] : loop[i];
                points[i] = new GeoPoint2(local.X + origin.X, local.Y + origin.Y);
            }

            return new GeoPolygon2(points, points.Length);
        }

        /// <summary>
        /// Builds faces from grouped loops: the outer boundaries counter-clockwise and the holes clockwise, or
        /// all reversed when asked.
        /// </summary>
        public static GeoFace2[] ToFaces(List<LoopGroup> groups, GeoPoint2 origin, bool reverse)
        {
            GeoFace2[] faces = new GeoFace2[groups.Count];

            for (int i = 0; i < groups.Count; i++)
            {
                GeoPolygon2[] holes = new GeoPolygon2[groups[i].Holes.Count];

                for (int h = 0; h < holes.Length; h++)
                {
                    holes[h] = ToPolygon(groups[i].Holes[h], origin, reverse);
                }

                faces[i] = new GeoFace2(ToPolygon(groups[i].Outer, origin, reverse), holes);
            }

            return faces;
        }

        /// <summary>
        /// The largest absolute coordinate of a set of loops, which sets how finely Clipper2 can round them.
        /// </summary>
        public static double Extent(IEnumerable<IReadOnlyList<GeoPoint2>> loops)
        {
            double extent = 0.0;

            foreach (IReadOnlyList<GeoPoint2> loop in loops)
            {
                foreach (GeoPoint2 point in loop)
                {
                    extent = Math.Max(extent, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
                }
            }

            return extent;
        }

        /// <summary>
        /// The largest absolute coordinate of some points measured from an origin.
        /// </summary>
        public static double Extent(IEnumerable<GeoPoint2> points, GeoPoint2 origin)
        {
            double extent = 0.0;

            foreach (GeoPoint2 point in points)
            {
                extent = Math.Max(extent, Math.Max(Math.Abs(point.X - origin.X), Math.Abs(point.Y - origin.Y)));
            }

            return extent;
        }

        private static PathsD ToPaths(IEnumerable<IReadOnlyList<GeoPoint2>> loops)
        {
            PathsD paths = new PathsD();

            foreach (IReadOnlyList<GeoPoint2> loop in loops)
            {
                PathD path = new PathD(loop.Count);

                foreach (GeoPoint2 point in loop)
                {
                    path.Add(new PointD(point.X, point.Y));
                }

                paths.Add(path);
            }

            return paths;
        }

        #endregion

        #region Clean-up

        /// <summary>
        /// Splits a loop that runs through the same vertex more than once into simple loops, one per lobe. Two
        /// pieces touching at a point come back from Clipper2 as one such loop.
        /// </summary>
        private static List<List<GeoPoint2>> SplitAtRepeats(PathD path)
        {
            List<List<GeoPoint2>> pieces = new List<List<GeoPoint2>>();
            List<GeoPoint2> stack = new List<GeoPoint2>(path.Count);
            Dictionary<(double, double), int> seen = new Dictionary<(double, double), int>();

            foreach (PointD point in path)
            {
                GeoPoint2 current = new GeoPoint2(point.x, point.y);

                if (seen.TryGetValue((point.x, point.y), out int at))
                {
                    // Everything since the first visit closes a lobe here.
                    pieces.Add(stack.GetRange(at, stack.Count - at));

                    for (int k = at + 1; k < stack.Count; k++)
                    {
                        seen.Remove((stack[k].X, stack[k].Y));
                    }

                    stack.RemoveRange(at + 1, stack.Count - at - 1);
                }
                else
                {
                    seen.Add((point.x, point.y), stack.Count);
                    stack.Add(current);
                }
            }

            pieces.Add(stack);
            pieces.RemoveAll(piece => piece.Count < 3);
            return pieces;
        }

        /// <summary>
        /// Cleans a loop against the tolerance. Returns null for a loop with nothing left, or one thinner than
        /// the point tolerance.
        /// </summary>
        private static List<GeoPoint2> Clean(List<GeoPoint2> loop, Tolerance tolerance, double straight)
        {
            List<GeoPoint2> cleaned = LoopTools.Clean(loop, tolerance.EqualPoint, straight);

            if (cleaned == null)
            {
                return null;
            }

            // Area over perimeter is half the width of a thin loop: one narrower than the point tolerance is a
            // seam left where two edges almost met, not a shape.
            if (Math.Abs(LoopTools.SignedArea(cleaned)) <= 0.5 * tolerance.EqualPoint * LoopTools.Perimeter(cleaned))
            {
                return null;
            }

            return cleaned;
        }

        /// <summary>
        /// The input vertices and edges, used to give the vertices Clipper2 returns the digits its rounding took
        /// off.
        /// </summary>
        private sealed class ExactPoints
        {
            private const int CellsAcross = 64;

            private readonly double _grid;
            private readonly double _cell;
            private readonly Dictionary<(long, long), List<GeoPoint2>> _vertices = new Dictionary<(long, long), List<GeoPoint2>>();
            private readonly Dictionary<(long, long), List<int>> _edgeCells = new Dictionary<(long, long), List<int>>();
            private readonly List<GeoPoint2> _from = new List<GeoPoint2>();
            private readonly List<GeoPoint2> _to = new List<GeoPoint2>();

            public ExactPoints(IEnumerable<IReadOnlyList<GeoPoint2>> loops, int precision)
            {
                _grid = Math.Pow(10.0, -precision);
                double extent = Extent(loops);
                _cell = Math.Max(extent * 2.0 / CellsAcross, _grid * 16.0);

                foreach (IReadOnlyList<GeoPoint2> loop in loops)
                {
                    for (int i = 0; i < loop.Count; i++)
                    {
                        GeoPoint2 a = loop[i];
                        GeoPoint2 b = loop[(i + 1) % loop.Count];
                        AddVertex(a);

                        if (a.DistanceSquaredTo(b) > 0.0)
                        {
                            AddEdge(a, b);
                        }
                    }
                }
            }

            public List<GeoPoint2> Restore(List<GeoPoint2> loop)
            {
                List<GeoPoint2> restored = new List<GeoPoint2>(loop.Count);

                foreach (GeoPoint2 point in loop)
                {
                    restored.Add(Restore(point));
                }

                return restored;
            }

            /// <summary>
            /// The exact point a rounded one stands for: the input vertex it was rounded from, the crossing of two
            /// input edges it was computed from, or the point itself when it is neither.
            /// </summary>
            public GeoPoint2 Restore(GeoPoint2 rounded)
            {
                // Rounding moves each coordinate by at most half a grid step, so a rounded input vertex lies
                // within a step of where it came from.
                double reachSquared = _grid * _grid;

                if (_vertices.TryGetValue(Key(rounded), out List<GeoPoint2> nearby))
                {
                    bool found = false;
                    GeoPoint2 best = rounded;
                    double bestDistance = double.MaxValue;

                    foreach (GeoPoint2 vertex in nearby)
                    {
                        double distance = vertex.DistanceSquaredTo(rounded);

                        if (distance <= reachSquared && distance < bestDistance)
                        {
                            best = vertex;
                            bestDistance = distance;
                            found = true;
                        }
                    }

                    if (found)
                    {
                        return best;
                    }
                }

                return RestoreCrossing(rounded);
            }

            /// <summary>
            /// Recomputes a crossing of two input edges that both pass within a few grid steps of the point, taking
            /// the pair meeting at the widest angle, which pins the crossing down best.
            /// </summary>
            private GeoPoint2 RestoreCrossing(GeoPoint2 rounded)
            {
                if (!_edgeCells.TryGetValue(CellOf(rounded), out List<int> candidates))
                {
                    return rounded;
                }

                double reach = 4.0 * _grid;
                List<int> close = new List<int>();

                foreach (int id in candidates)
                {
                    if (DistanceToSegment(rounded, _from[id], _to[id]) <= reach)
                    {
                        close.Add(id);
                    }
                }

                GeoPoint2 best = rounded;
                double bestSine = 1e-6;

                for (int i = 0; i < close.Count; i++)
                {
                    GeoPoint2 a = _from[close[i]];
                    GeoVector2 da = _to[close[i]] - a;

                    for (int j = i + 1; j < close.Count; j++)
                    {
                        GeoPoint2 c = _from[close[j]];
                        GeoVector2 dc = _to[close[j]] - c;
                        double cross = da.Cross(dc);
                        double sine = Math.Abs(cross) / (da.Length * dc.Length);

                        if (sine <= bestSine)
                        {
                            continue;
                        }

                        double t = (c - a).Cross(dc) / cross;
                        GeoPoint2 crossing = a + da * t;

                        // The recomputed crossing must be the one Clipper2 rounded, not another further along.
                        if (crossing.DistanceTo(rounded) <= reach)
                        {
                            best = crossing;
                            bestSine = sine;
                        }
                    }
                }

                return best;
            }

            private void AddVertex(GeoPoint2 vertex)
            {
                (long, long) key = Key(vertex);

                if (!_vertices.TryGetValue(key, out List<GeoPoint2> list))
                {
                    list = new List<GeoPoint2>();
                    _vertices.Add(key, list);
                }

                list.Add(vertex);

                // Near a cell border the rounded point may fall in the neighbouring cell, so the vertex is filed
                // under every cell within a grid step of it.
                foreach ((long, long) neighbour in NeighbourKeys(vertex))
                {
                    if (neighbour.Equals(key))
                    {
                        continue;
                    }

                    if (!_vertices.TryGetValue(neighbour, out List<GeoPoint2> other))
                    {
                        other = new List<GeoPoint2>();
                        _vertices.Add(neighbour, other);
                    }

                    other.Add(vertex);
                }
            }

            private void AddEdge(GeoPoint2 a, GeoPoint2 b)
            {
                int id = _from.Count;
                _from.Add(a);
                _to.Add(b);

                double margin = 4.0 * _grid;
                long x0 = (long)Math.Floor((Math.Min(a.X, b.X) - margin) / _cell);
                long x1 = (long)Math.Floor((Math.Max(a.X, b.X) + margin) / _cell);
                long y0 = (long)Math.Floor((Math.Min(a.Y, b.Y) - margin) / _cell);
                long y1 = (long)Math.Floor((Math.Max(a.Y, b.Y) + margin) / _cell);

                for (long x = x0; x <= x1; x++)
                {
                    for (long y = y0; y <= y1; y++)
                    {
                        if (!_edgeCells.TryGetValue((x, y), out List<int> list))
                        {
                            list = new List<int>();
                            _edgeCells.Add((x, y), list);
                        }

                        list.Add(id);
                    }
                }
            }

            private (long, long) Key(GeoPoint2 point) => ((long)Math.Floor(point.X / (_grid * 4.0)), (long)Math.Floor(point.Y / (_grid * 4.0)));

            private IEnumerable<(long, long)> NeighbourKeys(GeoPoint2 point)
            {
                HashSet<(long, long)> keys = new HashSet<(long, long)>();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        keys.Add(Key(new GeoPoint2(point.X + dx * _grid, point.Y + dy * _grid)));
                    }
                }

                return keys;
            }

            private (long, long) CellOf(GeoPoint2 point) => ((long)Math.Floor(point.X / _cell), (long)Math.Floor(point.Y / _cell));

            private static double DistanceToSegment(GeoPoint2 point, GeoPoint2 a, GeoPoint2 b)
            {
                GeoVector2 direction = b - a;
                double lengthSquared = direction.LengthSquared;
                double t = lengthSquared > 0.0 ? Math.Max(0.0, Math.Min(1.0, (point - a).Dot(direction) / lengthSquared)) : 0.0;
                return point.DistanceTo(a + direction * t);
            }
        }

        #endregion
    }
}
