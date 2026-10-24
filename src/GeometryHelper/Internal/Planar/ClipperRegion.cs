using System;
using System.Collections.Generic;
using GeometryHelper.Clipper;
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
        public static List<LoopGroup> Resolve(IEnumerable<IReadOnlyList<GeoPoint2>> loops, Clipper.FillRule rule, int precision, Tolerance tolerance)
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
            Clipper.FillRule rule,
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

            PathsD solution = Run(clipper, clipType, rule);

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
        /// Combines two regions as <see cref="Execute"/> does, but hands the loops back as Clipper2 gives them once their
        /// vertices have their full precision back: a loop running through a vertex twice is split there, and the loops are
        /// grouped, but nothing is merged within a tolerance and no thin loop left out. Pieces cut from one region side by
        /// side then keep the very points where they meet, as a mesh of them needs; only a point repeated in a row, a point
        /// standing on a straight run within rounding, and a loop enclosing nothing at all go.
        /// </summary>
        public static List<LoopGroup> ExecuteExact(
            ClipType clipType,
            IEnumerable<IReadOnlyList<GeoPoint2>> subject,
            IEnumerable<IReadOnlyList<GeoPoint2>> clip,
            Clipper.FillRule rule,
            int precision)
        {
            List<IReadOnlyList<GeoPoint2>> inputs = new List<IReadOnlyList<GeoPoint2>>(subject);
            ClipperD clipper = new ClipperD(precision) { PreserveCollinear = false };
            clipper.AddSubject(ToPaths(inputs));

            List<IReadOnlyList<GeoPoint2>> clipLoops = new List<IReadOnlyList<GeoPoint2>>(clip);
            clipper.AddClip(ToPaths(clipLoops));
            inputs.AddRange(clipLoops);

            PathsD solution = Run(clipper, clipType, rule);

            // Clipper2 crosses two long edges in its integers a few steps of its grid off where they cross: a cell's side
            // and an edge running a metre past it were crossed four steps off, and the cell beside it, whose crossing was
            // restored, then met it at a point of its own. Every crossing is restored from further off here, so that
            // pieces side by side come back with the same one.
            ExactPoints exact = new ExactPoints(inputs, precision, 16.0);
            List<List<GeoPoint2>> loops = new List<List<GeoPoint2>>();

            foreach (PathD path in solution)
            {
                foreach (List<GeoPoint2> piece in SplitAtRepeats(path))
                {
                    List<GeoPoint2> restored = exact.Restore(piece);
                    List<GeoPoint2> kept = new List<GeoPoint2>(restored.Count);

                    foreach (GeoPoint2 point in restored)
                    {
                        if (kept.Count == 0 || !kept[kept.Count - 1].Equals(point))
                        {
                            kept.Add(point);
                        }
                    }

                    while (kept.Count > 1 && kept[kept.Count - 1].Equals(kept[0]))
                    {
                        kept.RemoveAt(kept.Count - 1);
                    }

                    DropStraight(kept, 16.0 * Math.Pow(10.0, -precision));

                    if (kept.Count >= 3 && LoopTools.SignedArea(kept) != 0.0)
                    {
                        loops.Add(kept);
                    }
                }
            }

            return LoopTools.Group(loops);
        }

        /// <summary>
        /// Drops the points of a loop that stand on the line between their neighbours, within a distance.
        /// </summary>
        /// <remarks>
        /// Where edges of the two regions run along each other, Clipper2 can split the edge across from where they end, and
        /// hand the split back as a point of its own, rounded to its grid: a cell laid along the side of a rectangle came
        /// back with a corner halfway along its other side. Such a point is no crossing and no input vertex, so nothing
        /// gives it its digits back, and the pieces either side of that side would meet at it only in part. A point the
        /// pieces beside need is put back on the side by the mesh that joins them.
        /// </remarks>
        private static void DropStraight(List<GeoPoint2> loop, double within)
        {
            bool dropped = true;

            while (dropped && loop.Count > 3)
            {
                dropped = false;

                for (int i = 0; i < loop.Count && loop.Count > 3; i++)
                {
                    GeoPoint2 previous = loop[(i + loop.Count - 1) % loop.Count];
                    GeoPoint2 point = loop[i];
                    GeoPoint2 next = loop[(i + 1) % loop.Count];
                    double dx = next.X - previous.X;
                    double dy = next.Y - previous.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);

                    if (!(length > 0.0))
                    {
                        continue;
                    }

                    // On the chord between its neighbours, and between them along it rather than past either.
                    double off = Math.Abs((point.X - previous.X) * dy - (point.Y - previous.Y) * dx) / length;
                    double along = ((point.X - previous.X) * dx + (point.Y - previous.Y) * dy) / (length * length);

                    if (off <= within && along > 0.0 && along < 1.0)
                    {
                        loop.RemoveAt(i);
                        dropped = true;
                        i--;
                    }
                }
            }
        }

        /// <summary>
        /// Resolves loops under a fill rule and returns every boundary loop of the result, flat, with collinear
        /// points kept, so that each edge still lies along the input edge it came from. The vertices get their
        /// full precision back, but nothing is merged or dropped.
        /// </summary>
        public static List<List<GeoPoint2>> ResolveOutline(IEnumerable<IReadOnlyList<GeoPoint2>> loops, Clipper.FillRule rule, int precision)
        {
            List<IReadOnlyList<GeoPoint2>> inputs = new List<IReadOnlyList<GeoPoint2>>(loops);
            ClipperD clipper = new ClipperD(precision) { PreserveCollinear = true };
            clipper.AddSubject(ToPaths(inputs));

            PathsD solution = Run(clipper, ClipType.Union, rule);

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

        /// <summary>
        /// Runs the clipper and gives the closed paths of its answer. A sweep that fails, which no case tried has made it
        /// do, gives none, and a warning says so, rather than the part built before it failed passing for the region.
        /// </summary>
        public static PathsD Run(ClipperD clipper, ClipType clipType, Clipper.FillRule rule)
        {
            PathsD solution = new PathsD();

            if (!clipper.Execute(clipType, rule, solution))
            {
                solution.Clear();
                GeometryHelperLog.Warn($"Planar region: Clipper2 failed to resolve a {clipType} under the {rule} fill rule, so it is left empty.");
            }

            return solution;
        }

        #region Regions of the library's shapes

        /// <summary>
        /// The loops of a polygon's region, read under the even-odd rule as <see cref="Containment2"/> reads it,
        /// resolved into boundary loops with the region on their left.
        /// </summary>
        public static List<List<GeoPoint2>> RegionOf(GeoPolygon2 polygon, GeoPoint2 origin, int precision, Tolerance tolerance)
        {
            return Flatten(Resolve(new[] { ToLocal(polygon.Vertices, origin) }, Clipper.FillRule.EvenOdd, precision, tolerance));
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
        /// <remarks>
        /// A crossing was restored from the edges filed under the cell of a grid 64 cells across the drawing that the
        /// point falls in, each edge filed under every cell of its box. On a long shape those cells grew long with it,
        /// and every point looked through thousands of edges: a comb of 40 000 teeth took 4.4 seconds to offset by a
        /// half, where Clipper2 itself took a third of one. The edges are found in cells sized and shaped by them now, each
        /// filed under the cells along it rather than the whole of its box, and still kept only where they lie in the
        /// point's cell of the 64 and within reach of it, in the order they were added; and each input vertex is filed
        /// once, under its own cell, rather than under every cell a grid step from it. The same points are restored to
        /// the bit, and the comb is offset in 0.8 seconds.
        /// </remarks>
        private sealed class ExactPoints
        {
            private const int CellsAcross = 64;

            /// <summary>How many fine cells the edges are filed under, an edge on the whole, at most, beyond a few each.</summary>
            private const int CellsAnEdge = 16;

            private readonly double _grid;
            private readonly double _steps;
            private readonly double _cell;

            // Each input vertex once, under the cell of four grid steps it falls in: the last filed there, and before each the
            // one filed there before it.
            private readonly Dictionary<(long, long), int> _vertexCells = new Dictionary<(long, long), int>();
            private readonly List<GeoPoint2> _vertices = new List<GeoPoint2>();
            private readonly List<int> _filedBefore = new List<int>();
            private readonly List<int> _nearby = new List<int>();
            private readonly List<GeoPoint2> _from = new List<GeoPoint2>();
            private readonly List<GeoPoint2> _to = new List<GeoPoint2>();

            // The cells of the 64 each edge was filed under, four numbers an edge: first and last across, then up.
            private readonly List<long> _wide = new List<long>();

            // The finer cells edges are looked up in: their width and height, the corner of the drawing's box they are counted
            // from, and how many the box spans.
            private readonly Dictionary<long, List<int>> _fine = new Dictionary<long, List<int>>(PackedKeys.Instance);
            private readonly double _fineX;
            private readonly double _fineY;
            private readonly double _minX;
            private readonly double _minY;
            private readonly long _columns;
            private readonly long _rows;

            /// <param name="loops">The loops handed to Clipper2.</param>
            /// <param name="precision">The decimal places it rounded to.</param>
            /// <param name="steps">
            /// How many steps of its grid a crossing it computed may stand off the crossing of the two edges it came from.
            /// </param>
            public ExactPoints(IEnumerable<IReadOnlyList<GeoPoint2>> loops, int precision, double steps = 4.0)
            {
                _grid = Math.Pow(10.0, -precision);
                _steps = steps;
                double extent = Extent(loops);
                _cell = Math.Max(extent * 2.0 / CellsAcross, _grid * 16.0);

                // Fine cells holding a few edges each: as many cells as edges where the drawing has an area, along its length
                // where it has none, each as much wider than tall as the edges run further across than up on the whole, so
                // that an edge is filed under few of them. An edge is filed under every cell along it, so the cells are no
                // finer than lets the edges fill sixteen of them an edge on the whole, and never coarser than the 64: 16 000
                // strips 100 000 long and 1 wide, side by side, filed under square cells as many as the edges, were filed 600
                // million times and took 4 GB.
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                double acrossX = 0.0, acrossY = 0.0;
                long count = 0;

                foreach (IReadOnlyList<GeoPoint2> loop in loops)
                {
                    for (int i = 0; i < loop.Count; i++)
                    {
                        GeoPoint2 point = loop[i];
                        GeoPoint2 next = loop[(i + 1) % loop.Count];
                        double dx = Math.Abs(next.X - point.X), dy = Math.Abs(next.Y - point.Y);
                        minX = Math.Min(minX, point.X);
                        minY = Math.Min(minY, point.Y);
                        maxX = Math.Max(maxX, point.X);
                        maxY = Math.Max(maxY, point.Y);
                        count++;

                        if (dx < double.PositiveInfinity && dy < double.PositiveInfinity)
                        {
                            acrossX += dx;
                            acrossY += dy;
                        }
                    }
                }

                double width = count > 0 ? maxX - minX : 0.0;
                double height = count > 0 ? maxY - minY : 0.0;
                double edges = Math.Max(count, 1);
                double fineX, fineY;

                if (width > 0.0 && height > 0.0 && acrossX > 0.0 && acrossY > 0.0)
                {
                    fineX = Math.Sqrt(width * height / edges * acrossX / acrossY);
                    fineY = Math.Sqrt(width * height / edges * acrossY / acrossX);
                }
                else
                {
                    fineX = fineY = Math.Max(width, height) / edges;
                }

                fineX = Math.Max(Math.Max(fineX, width / (CellsAnEdge * edges)), _grid * 16.0);
                fineY = Math.Max(Math.Max(fineY, height / (CellsAnEdge * edges)), _grid * 16.0);
                double filed = acrossX / fineX + acrossY / fineY;

                if (filed > CellsAnEdge * edges)
                {
                    fineX *= filed / (CellsAnEdge * edges);
                    fineY *= filed / (CellsAnEdge * edges);
                }

                _fineX = Math.Min(_cell, fineX);
                _fineY = Math.Min(_cell, fineY);
                _minX = count > 0 ? minX : 0.0;
                _minY = count > 0 ? minY : 0.0;
                _columns = (long)(width / _fineX) + 4;
                _rows = (long)(height / _fineY) + 4;

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

                if (Nearby(Key(rounded)))
                {
                    bool found = false;
                    GeoPoint2 best = rounded;
                    double bestDistance = double.MaxValue;

                    foreach (int v in _nearby)
                    {
                        GeoPoint2 vertex = _vertices[v];
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
                if (!TryFine(rounded.X - _minX, rounded.Y - _minY, out long key) || !_fine.TryGetValue(key, out List<int> candidates))
                {
                    return rounded;
                }

                (long x, long y) = CellOf(rounded);
                double reach = _steps * _grid;
                List<int> close = new List<int>();

                foreach (int id in candidates)
                {
                    if (_wide[4 * id] <= x && x <= _wide[4 * id + 1] && _wide[4 * id + 2] <= y && y <= _wide[4 * id + 3]
                        && DistanceToSegment(rounded, _from[id], _to[id]) <= reach)
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
                _filedBefore.Add(_vertexCells.TryGetValue(key, out int before) ? before : -1);
                _vertexCells[key] = _vertices.Count;
                _vertices.Add(vertex);
            }

            /// <summary>
            /// Gathers, in the order they were added, the input vertices that a rounded point in a cell may have been rounded
            /// from: near a cell border the rounded point may fall in the neighbouring cell, so those are the vertices a grid
            /// step or less from the cell, each filed under its own cell, one of the nine round it. Whether any were found.
            /// </summary>
            private bool Nearby((long, long) cell)
            {
                _nearby.Clear();
                bool any = false;

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        if (!_vertexCells.TryGetValue((cell.Item1 + dx, cell.Item2 + dy), out int v))
                        {
                            continue;
                        }

                        for (; v >= 0; v = _filedBefore[v])
                        {
                            GeoPoint2 vertex = _vertices[v];
                            (long left, long low) = Key(new GeoPoint2(vertex.X - _grid, vertex.Y - _grid));
                            (long right, long high) = Key(new GeoPoint2(vertex.X + _grid, vertex.Y + _grid));

                            if (left <= cell.Item1 && cell.Item1 <= right && low <= cell.Item2 && cell.Item2 <= high)
                            {
                                _nearby.Add(v);
                                any = true;
                            }
                        }
                    }
                }

                _nearby.Sort();
                return any;
            }

            private void AddEdge(GeoPoint2 a, GeoPoint2 b)
            {
                int id = _from.Count;
                _from.Add(a);
                _to.Add(b);

                double margin = _steps * _grid;
                _wide.Add((long)Math.Floor((Math.Min(a.X, b.X) - margin) / _cell));
                _wide.Add((long)Math.Floor((Math.Max(a.X, b.X) + margin) / _cell));
                _wide.Add((long)Math.Floor((Math.Min(a.Y, b.Y) - margin) / _cell));
                _wide.Add((long)Math.Floor((Math.Max(a.Y, b.Y) + margin) / _cell));

                // Filed under every fine cell holding a point within twice the reach of it, which leaves rounding ample
                // room: cell by cell along its longer way, and across as far as it goes within that cell.
                bool alongX = Math.Abs(b.X - a.X) / _fineX >= Math.Abs(b.Y - a.Y) / _fineY;
                double sa = alongX ? a.X - _minX : a.Y - _minY;
                double sb = alongX ? b.X - _minX : b.Y - _minY;
                double ta = alongX ? a.Y - _minY : a.X - _minX;
                double tb = alongX ? b.Y - _minY : b.X - _minX;
                double wide = 2.0 * margin + 1E-12 * (Math.Abs(_minX) + Math.Abs(_minY) + Math.Abs(sa) + Math.Abs(ta));
                long limitS = alongX ? _columns : _rows;
                long limitT = alongX ? _rows : _columns;
                double cellS = alongX ? _fineX : _fineY;
                double cellT = alongX ? _fineY : _fineX;
                double lowS = Math.Min(sa, sb), highS = Math.Max(sa, sb);

                for (long s = Fine(lowS - wide, cellS, limitS), last = Fine(highS + wide, cellS, limitS); s <= last; s++)
                {
                    double t1 = Across(Math.Max(s * cellS - wide, lowS), sa, sb, ta, tb, 0.0);
                    double t2 = Across(Math.Min((s + 1) * cellS + wide, highS), sa, sb, ta, tb, 1.0);

                    for (long t = Fine(Math.Min(t1, t2) - wide, cellT, limitT), end = Fine(Math.Max(t1, t2) + wide, cellT, limitT); t <= end; t++)
                    {
                        long key = alongX ? FineKey(s, t) : FineKey(t, s);

                        if (!_fine.TryGetValue(key, out List<int> list))
                        {
                            list = new List<int>();
                            _fine.Add(key, list);
                        }

                        list.Add(id);
                    }
                }
            }

            /// <summary>
            /// Where across an edge from (sa, ta) to (sb, tb) is at a point along it, its fraction kept within the edge; where
            /// the edge has no length that way, the end the fraction stands for.
            /// </summary>
            private static double Across(double s, double sa, double sb, double ta, double tb, double fraction)
            {
                double f = sb != sa ? Math.Max(0.0, Math.Min(1.0, (s - sa) / (sb - sa))) : fraction;
                return ta + (tb - ta) * f;
            }

            /// <summary>
            /// The fine cell a distance from the corner of the box falls in, kept within four cells of the box.
            /// </summary>
            private static long Fine(double offset, double cell, long limit) => Math.Max(-4L, Math.Min(limit + 4L, (long)Math.Floor(offset / cell)));

            private static long FineKey(long column, long row) => ((column + 4L) << 32) | (uint)(row + 4L);

            /// <summary>
            /// The key of the fine cell a point at a distance from the corner of the box falls in; false where that is
            /// more than four cells off the box, which no edge is filed under.
            /// </summary>
            private bool TryFine(double dx, double dy, out long key)
            {
                double column = Math.Floor(dx / _fineX);
                double row = Math.Floor(dy / _fineY);
                key = 0;

                if (!(column >= -4.0 && column <= _columns + 4.0 && row >= -4.0 && row <= _rows + 4.0))
                {
                    return false;
                }

                key = FineKey((long)column, (long)row);
                return true;
            }

            private (long, long) Key(GeoPoint2 point) => ((long)Math.Floor(point.X / (_grid * 4.0)), (long)Math.Floor(point.Y / (_grid * 4.0)));

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
