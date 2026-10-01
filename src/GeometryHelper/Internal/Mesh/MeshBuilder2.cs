using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Gathers the faces of a mesh of the plane as loops of points and joins them into a <see cref="GeoMesh2"/>.
    /// <para>
    /// The pieces come from the meshing algorithms with the tolerance already applied to the shape they were cut from, so
    /// joining them reads no tolerance: points the same but for rounding become one vertex, the shape's own corners first,
    /// so that a face meeting one takes it exactly, and a vertex standing on the side of a face across from it, within
    /// rounding, is made one of that face's corners too, so that the faces meet edge to edge. For the kinds made of
    /// triangles a triangle so given a corner on its side is split there instead, and for <see cref="MeshKind.Convex"/> the
    /// triangles are then merged across every edge whose two sides stay convex together, longest edges first, as Hertel and
    /// Mehlhorn merge them.
    /// </para>
    /// <para>
    /// Nearness within the point tolerance would join more, and wrongly. Where features of the shape stand closer than the
    /// tolerance the pieces between them are slivers, and their corners stand that near the sides of faces they do not
    /// belong to: joined, a sliver collapses, and a face taking in a corner across one is pulled over its neighbour.
    /// </para>
    /// </summary>
    internal sealed class MeshBuilder2
    {
        private readonly Tolerance _tolerance;
        private readonly double _rounding;
        private readonly double _cell;
        private readonly List<GeoPoint2> _points = new List<GeoPoint2>();
        private readonly Dictionary<(long, long), List<int>> _buckets = new Dictionary<(long, long), List<int>>();
        private readonly List<List<int>> _faces = new List<List<int>>();
        private readonly List<bool> _whole = new List<bool>();

        /// <summary>
        /// Initializes a builder for a mesh of a shape.
        /// </summary>
        /// <param name="tolerance">The tolerance the mesh is made within, kept for the mesh and for merging convex pieces.</param>
        /// <param name="scale">The largest coordinate of the shape, which sets how far rounding can move a point.</param>
        /// <param name="extent">
        /// How far the shape reaches from the point its pieces were laid out from, which sets the grid Clipper2 rounds them
        /// to: a point the cutting hands back as it computed it, rather than as the crossing of two edges, can stand up to a
        /// step of that grid off.
        /// </param>
        public MeshBuilder2(Tolerance tolerance, double scale, double extent)
        {
            _tolerance = tolerance;

            // A millionth of the point tolerance; for points far out, a few hundred steps of their last digit; and for
            // pieces cut by Clipper2, as many steps of the grid it rounds to as a crossing it computed is restored from.
            double clipped = 16.0 * Math.Pow(10.0, -ClipperRegion.GetPrecision(extent));
            _rounding = Math.Max(Math.Max(tolerance.EqualPoint * 1E-6, Math.Abs(scale) * 1E-14), Math.Max(clipped, 1E-12));
            _cell = 2.0 * _rounding;
        }

        /// <summary>
        /// Gives the shape's own corners their places before any face comes, so that a point of a face that is one but for
        /// rounding is that corner, exactly.
        /// </summary>
        public void Seed(IEnumerable<GeoPoint2> corners)
        {
            foreach (GeoPoint2 corner in corners)
            {
                IndexOf(corner);
            }
        }

        /// <summary>
        /// Adds a face given by its corners in turn, either way round. A face running through a place twice, or touching a
        /// side of its own at a corner, is split there into the loops either side, and a loop enclosing nothing is dropped.
        /// </summary>
        /// <param name="corners">The corners of the face.</param>
        /// <param name="whole">Whether the face is a whole cell of a grid.</param>
        public void Add(IReadOnlyList<GeoPoint2> corners, bool whole)
        {
            var loop = new List<int>(corners.Count);

            foreach (GeoPoint2 corner in corners)
            {
                int index = IndexOf(corner);

                if (loop.Count == 0 || loop[loop.Count - 1] != index)
                {
                    loop.Add(index);
                }
            }

            while (loop.Count > 1 && loop[loop.Count - 1] == loop[0])
            {
                loop.RemoveAt(loop.Count - 1);
            }

            var pieces = new List<List<int>>();

            foreach (List<int> lobe in SplitAtRepeats(loop))
            {
                SplitAtTouches(lobe, pieces);
            }

            foreach (List<int> piece in pieces)
            {
                double area = SignedArea(piece);

                if (!HasArea(piece, area))
                {
                    continue;
                }

                if (area < 0.0)
                {
                    piece.Reverse();
                }

                _faces.Add(piece);
                _whole.Add(whole);
            }
        }

        /// <summary>
        /// Joins the faces gathered into a mesh of a kind.
        /// </summary>
        public GeoMesh2 Build(MeshKind kind)
        {
            bool triangles = kind == MeshKind.Triangles || kind == MeshKind.Convex;
            int[][] corners = null;

            if (triangles)
            {
                corners = new int[_faces.Count][];

                for (int f = 0; f < corners.Length; f++)
                {
                    corners[f] = _faces[f].ToArray();
                }
            }

            InsertJunctions();

            if (triangles)
            {
                SplitTriangles(corners);
            }

            if (kind == MeshKind.Convex)
            {
                MergeConvex();
            }

            return Compact(kind);
        }

        #region Places

        private bool Same(GeoPoint2 a, GeoPoint2 b) => a.GetDistanceSquaredTo(b) <= _rounding * _rounding;

        private int IndexOf(GeoPoint2 point)
        {
            long x = (long)Math.Floor(point.X / _cell);
            long y = (long)Math.Floor(point.Y / _cell);

            for (long dx = -1; dx <= 1; dx++)
            {
                for (long dy = -1; dy <= 1; dy++)
                {
                    if (!_buckets.TryGetValue((x + dx, y + dy), out List<int> bucket))
                    {
                        continue;
                    }

                    foreach (int index in bucket)
                    {
                        if (Same(_points[index], point))
                        {
                            return index;
                        }
                    }
                }
            }

            int added = _points.Count;
            _points.Add(point);

            if (!_buckets.TryGetValue((x, y), out List<int> own))
            {
                own = new List<int>();
                _buckets.Add((x, y), own);
            }

            own.Add(added);
            return added;
        }

        /// <summary>
        /// Splits a loop that comes back to a vertex it has passed into the loops between, one per lobe.
        /// </summary>
        private static List<List<int>> SplitAtRepeats(List<int> loop)
        {
            var pieces = new List<List<int>>();
            var stack = new List<int>(loop.Count);
            var seen = new Dictionary<int, int>();

            foreach (int index in loop)
            {
                if (seen.TryGetValue(index, out int at))
                {
                    pieces.Add(stack.GetRange(at, stack.Count - at));

                    for (int k = at + 1; k < stack.Count; k++)
                    {
                        seen.Remove(stack[k]);
                    }

                    stack.RemoveRange(at + 1, stack.Count - at - 1);
                }
                else
                {
                    seen.Add(index, stack.Count);
                    stack.Add(index);
                }
            }

            pieces.Add(stack);
            pieces.RemoveAll(piece => piece.Count < 3);
            return pieces;
        }

        /// <summary>
        /// Splits a loop where a corner of it stands on a side of its own, within rounding, into the loops either side,
        /// when both enclose something: where a hole touches another corner to side, the material between them is pinched
        /// to that point, and the region comes back as one loop running through it twice, once as a corner and once along a
        /// side.
        /// </summary>
        private void SplitAtTouches(List<int> loop, List<List<int>> pieces)
        {
            int n = loop.Count;
            double reachSquared = _rounding * _rounding;

            for (int i = 0; i < n && n > 3; i++)
            {
                GeoPoint2 corner = _points[loop[i]];

                for (int k = 1; k + 1 < n; k++)
                {
                    // The side from the corner k ahead to the one after, which does not end at the corner itself.
                    int j = (i + k) % n;
                    GeoPoint2 start = _points[loop[j]];
                    GeoPoint2 end = _points[loop[(j + 1) % n]];
                    double dx = end.X - start.X;
                    double dy = end.Y - start.Y;
                    double lengthSquared = dx * dx + dy * dy;

                    if (!(lengthSquared > 0.0))
                    {
                        continue;
                    }

                    double t = ((corner.X - start.X) * dx + (corner.Y - start.Y) * dy) / lengthSquared;

                    if (!(t > 0.0 && t < 1.0))
                    {
                        continue;
                    }

                    double ox = start.X + t * dx - corner.X;
                    double oy = start.Y + t * dy - corner.Y;

                    if (ox * ox + oy * oy > reachSquared || Same(corner, start) || Same(corner, end))
                    {
                        continue;
                    }

                    // From the corner on to the side's start, and from the corner again from the side's end round to it.
                    var ahead = new List<int>(k + 1);
                    var behind = new List<int>(n - k + 1);

                    for (int m = 0; m <= k; m++)
                    {
                        ahead.Add(loop[(i + m) % n]);
                    }

                    behind.Add(loop[i]);

                    for (int m = k + 1; m < n; m++)
                    {
                        behind.Add(loop[(i + m) % n]);
                    }

                    if (ahead.Count >= 3 && behind.Count >= 3 && HasArea(ahead, SignedArea(ahead)) && HasArea(behind, SignedArea(behind)))
                    {
                        SplitAtTouches(ahead, pieces);
                        SplitAtTouches(behind, pieces);
                        return;
                    }
                }
            }

            pieces.Add(loop);
        }

        /// <summary>
        /// Whether a loop encloses more than rounding leaves: wider, on average, than rounding can move a point.
        /// </summary>
        /// <remarks>
        /// A piece of the material is kept however small or thin it is, as the triangles of the surface are, so that the
        /// faces cover all of it: a cell's corner a tenth of a millimetre inside the boundary leaves a triangle of a few
        /// thousandths of a square millimetre in that cell, and a hole a few hundredths from the boundary leaves a sliver.
        /// Only a loop of corners in a row encloses nothing.
        /// </remarks>
        private bool HasArea(List<int> loop, double signedArea)
        {
            double perimeter = 0.0;

            for (int i = 0; i < loop.Count; i++)
            {
                perimeter += _points[loop[i]].DistanceTo(_points[loop[(i + 1) % loop.Count]]);
            }

            return 2.0 * Math.Abs(signedArea) > _rounding * perimeter;
        }

        private double SignedArea(List<int> loop)
        {
            GeoPoint2 reference = _points[loop[0]];
            double twice = 0.0;

            for (int i = 1; i + 1 < loop.Count; i++)
            {
                GeoPoint2 a = _points[loop[i]];
                GeoPoint2 b = _points[loop[i + 1]];
                twice += (a.X - reference.X) * (b.Y - reference.Y) - (b.X - reference.X) * (a.Y - reference.Y);
            }

            return twice * 0.5;
        }

        #endregion

        #region Edge to edge

        /// <summary>
        /// Makes every corner of a face across a side, standing on that side within rounding and apart from its ends, a
        /// corner of the face the side belongs to as well, in order along it.
        /// </summary>
        /// <remarks>
        /// A side another face runs back along end to end is shared whole, and nothing is put on it. On any other side, a
        /// vertex counts only where a side of another face ends at it and runs back along the line of this one: a corner of
        /// the face across, which is what needs one here. The algorithms put such a corner on the side as they compute it,
        /// where a strip line or a cut crosses the side, so rounding is all that stands between them.
        /// </remarks>
        private void InsertJunctions()
        {
            if (_faces.Count == 0)
            {
                return;
            }

            var sides = new HashSet<(int, int)>();
            var about = new Dictionary<int, List<(int From, int To, int Face)>>();
            double total = 0.0;
            int count = 0;

            for (int f = 0; f < _faces.Count; f++)
            {
                List<int> face = _faces[f];

                for (int i = 0; i < face.Count; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Count];
                    sides.Add((a, b));
                    About(about, a).Add((a, b, f));
                    About(about, b).Add((a, b, f));
                    total += _points[a].DistanceTo(_points[b]);
                    count++;
                }
            }

            // Cells about as wide as an edge is long: a side then crosses a few, each holding a few vertices.
            double size = Math.Max(total / count, 4.0 * _rounding);
            var cells = new Dictionary<(long, long), List<int>>();

            for (int v = 0; v < _points.Count; v++)
            {
                if (!about.ContainsKey(v))
                {
                    continue;
                }

                (long, long) key = ((long)Math.Floor(_points[v].X / size), (long)Math.Floor(_points[v].Y / size));

                if (!cells.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    cells.Add(key, list);
                }

                list.Add(v);
            }

            var found = new List<(double T, int Vertex)>();
            var own = new HashSet<int>();

            for (int f = 0; f < _faces.Count; f++)
            {
                List<int> face = _faces[f];
                var joined = new List<int>(face.Count);
                bool changed = false;
                own.Clear();
                own.UnionWith(face);

                for (int i = 0; i < face.Count; i++)
                {
                    int a = face[i];
                    int b = face[(i + 1) % face.Count];
                    joined.Add(a);

                    if (sides.Contains((b, a)))
                    {
                        continue;
                    }

                    Junctions(a, b, f, size, cells, about, own, found);

                    foreach ((double _, int vertex) in found)
                    {
                        joined.Add(vertex);
                        changed = true;

                        // A vertex near a narrow corner may stand on both its sides; it is a corner of the face once.
                        own.Add(vertex);
                    }
                }

                if (changed)
                {
                    _faces[f] = joined;
                }
            }
        }

        private static List<(int From, int To, int Face)> About(Dictionary<int, List<(int From, int To, int Face)>> about, int vertex)
        {
            if (!about.TryGetValue(vertex, out List<(int From, int To, int Face)> list))
            {
                list = new List<(int From, int To, int Face)>(4);
                about.Add(vertex, list);
            }

            return list;
        }

        /// <summary>
        /// Finds the corners of faces across a side that stand on it, apart from its ends and from the face's own corners,
        /// in order from its start.
        /// </summary>
        private void Junctions(
            int a,
            int b,
            int face,
            double size,
            Dictionary<(long, long), List<int>> cells,
            Dictionary<int, List<(int From, int To, int Face)>> about,
            HashSet<int> own,
            List<(double T, int Vertex)> found)
        {
            found.Clear();

            GeoPoint2 start = _points[a];
            GeoPoint2 end = _points[b];
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;

            if (!(lengthSquared > 0.0))
            {
                return;
            }

            double reach = _rounding;
            double reachSquared = reach * reach;
            long x0 = (long)Math.Floor((Math.Min(start.X, end.X) - reach) / size);
            long x1 = (long)Math.Floor((Math.Max(start.X, end.X) + reach) / size);
            long y0 = (long)Math.Floor((Math.Min(start.Y, end.Y) - reach) / size);
            long y1 = (long)Math.Floor((Math.Max(start.Y, end.Y) + reach) / size);

            bool OnLine(GeoPoint2 point)
            {
                double cross = (point.X - start.X) * dy - (point.Y - start.Y) * dx;
                return cross * cross <= reachSquared * lengthSquared;
            }

            // Whether a side of another face ends at the vertex and runs back along the line of this one: from the vertex,
            // which stands on this side, the two overlap however far the other reaches, past an end of this one or not.
            bool EndsARunBack(int v)
            {
                foreach ((int from, int to, int owner) in about[v])
                {
                    if (owner == face)
                    {
                        continue;
                    }

                    GeoPoint2 p = _points[from];
                    GeoPoint2 q = _points[to];

                    if ((q.X - p.X) * dx + (q.Y - p.Y) * dy >= 0.0)
                    {
                        continue;
                    }

                    int other = from == v ? to : from;

                    if (other == a || other == b || OnLine(_points[other]))
                    {
                        return true;
                    }
                }

                return false;
            }

            void Consider(int v)
            {
                if (own.Contains(v))
                {
                    return;
                }

                GeoPoint2 point = _points[v];
                double t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;

                if (t > 0.0 && t < 1.0 && OnLine(point) && !Same(point, start) && !Same(point, end) && EndsARunBack(v))
                {
                    found.Add((t, v));
                }
            }

            // A long side across a dense mesh would visit more cells than there are vertices: then every vertex is asked.
            if ((double)(x1 - x0 + 1) * (y1 - y0 + 1) > about.Count)
            {
                foreach (int v in about.Keys)
                {
                    Consider(v);
                }
            }
            else
            {
                for (long x = x0; x <= x1; x++)
                {
                    for (long y = y0; y <= y1; y++)
                    {
                        if (cells.TryGetValue((x, y), out List<int> list))
                        {
                            foreach (int v in list)
                            {
                                Consider(v);
                            }
                        }
                    }
                }
            }

            found.Sort((left, right) => left.T.CompareTo(right.T));
        }

        /// <summary>
        /// Splits every triangle given a corner on a side into triangles meeting there, so that the triangles meet edge to
        /// edge and stay triangles.
        /// </summary>
        /// <param name="corners">The three corners each face had before corners were put on its sides.</param>
        private void SplitTriangles(int[][] corners)
        {
            int count = _faces.Count;

            for (int f = 0; f < count; f++)
            {
                List<int> face = _faces[f];

                if (face.Count == 3)
                {
                    continue;
                }

                int[] own = corners[f];
                int start = face.IndexOf(own[0]);
                var sides = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };
                int side = 0;

                for (int k = 1; k < face.Count; k++)
                {
                    int vertex = face[(start + k) % face.Count];

                    if (side < 2 && vertex == own[side + 1])
                    {
                        side++;
                    }
                    else
                    {
                        sides[side].Add(vertex);
                    }
                }

                var pieces = new List<List<int>>();
                Split(own[0], own[1], own[2], sides[0], sides[1], sides[2], pieces);

                _faces[f] = pieces[0];

                for (int p = 1; p < pieces.Count; p++)
                {
                    _faces.Add(pieces[p]);
                    _whole.Add(false);
                }
            }
        }

        /// <summary>
        /// Splits a triangle with corners on its sides, each side's in order from its start, at the middle corner of a
        /// side, until no side has one.
        /// </summary>
        private static void Split(int u, int v, int w, List<int> uv, List<int> vw, List<int> wu, List<List<int>> pieces)
        {
            if (uv.Count == 0)
            {
                if (vw.Count > 0)
                {
                    Split(v, w, u, vw, wu, uv, pieces);
                }
                else if (wu.Count > 0)
                {
                    Split(w, u, v, wu, uv, vw, pieces);
                }
                else
                {
                    pieces.Add(new List<int> { u, v, w });
                }

                return;
            }

            // The corner on the side splits the triangle into two, both of area since it stands apart from the side's ends
            // and the third corner stands off the side; the line from it to that corner is new, with nothing on it.
            int m = uv.Count / 2;
            int p = uv[m];

            Split(u, p, w, uv.GetRange(0, m), new List<int>(), wu, pieces);
            Split(p, v, w, uv.GetRange(m + 1, uv.Count - m - 1), vw, new List<int>(), pieces);
        }

        #endregion

        #region Convex pieces

        /// <summary>
        /// Merges the triangles across every edge whose two sides stay convex at both its ends once merged, longest edges
        /// first.
        /// </summary>
        /// <remarks>
        /// A corner counts as convex while it stands no further inside the line between its neighbours than the point
        /// tolerance, so that three corners a hair out of line do not keep two pieces apart.
        /// </remarks>
        private void MergeConvex()
        {
            var owner = new Dictionary<(int, int), int>();

            for (int f = 0; f < _faces.Count; f++)
            {
                List<int> face = _faces[f];

                for (int i = 0; i < face.Count; i++)
                {
                    owner[(face[i], face[(i + 1) % face.Count])] = f;
                }
            }

            var shared = new List<(int A, int B, double Length)>();

            foreach ((int a, int b) in owner.Keys)
            {
                if (a < b && owner.ContainsKey((b, a)))
                {
                    shared.Add((a, b, _points[a].DistanceTo(_points[b])));
                }
            }

            shared.Sort((left, right) =>
            {
                int byLength = right.Length.CompareTo(left.Length);

                if (byLength != 0)
                {
                    return byLength;
                }

                int byA = left.A.CompareTo(right.A);
                return byA != 0 ? byA : left.B.CompareTo(right.B);
            });

            var alive = new bool[_faces.Count];

            for (int f = 0; f < alive.Length; f++)
            {
                alive[f] = true;
            }

            foreach ((int a, int b, double _) in shared)
            {
                if (!owner.TryGetValue((a, b), out int first) || !owner.TryGetValue((b, a), out int second) || first == second)
                {
                    continue;
                }

                List<int> merged = Join(_faces[first], _faces[second], a, b);

                if (merged == null || !IsConvexAt(merged, _faces[first].Count - 1) || !IsConvexAt(merged, 0))
                {
                    continue;
                }

                List<int> gone = _faces[second];

                for (int i = 0; i < gone.Count; i++)
                {
                    owner[(gone[i], gone[(i + 1) % gone.Count])] = first;
                }

                owner.Remove((a, b));
                owner.Remove((b, a));
                _faces[first] = merged;
                alive[second] = false;
            }

            var kept = new List<List<int>>();
            var whole = new List<bool>();

            for (int f = 0; f < _faces.Count; f++)
            {
                if (alive[f])
                {
                    kept.Add(_faces[f]);
                    whole.Add(_whole[f]);
                }
            }

            _faces.Clear();
            _faces.AddRange(kept);
            _whole.Clear();
            _whole.AddRange(whole);
        }

        /// <summary>
        /// Joins two faces across the edge one runs from a to b and the other back: the first's corners from b round to a,
        /// then the second's after a up to b. Null when the two share another corner, which would leave the join touching
        /// itself there.
        /// </summary>
        private static List<int> Join(List<int> first, List<int> second, int a, int b)
        {
            int i = IndexOfEdge(first, a, b);
            int j = IndexOfEdge(second, b, a);

            if (i < 0 || j < 0)
            {
                return null;
            }

            var merged = new List<int>(first.Count + second.Count - 2);

            for (int k = 1; k <= first.Count; k++)
            {
                merged.Add(first[(i + k) % first.Count]);
            }

            for (int k = 2; k < second.Count; k++)
            {
                merged.Add(second[(j + k) % second.Count]);
            }

            var distinct = new HashSet<int>(merged);
            return distinct.Count == merged.Count ? merged : null;
        }

        private static int IndexOfEdge(List<int> face, int a, int b)
        {
            for (int i = 0; i < face.Count; i++)
            {
                if (face[i] == a && face[(i + 1) % face.Count] == b)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsConvexAt(List<int> loop, int at)
        {
            GeoPoint2 previous = _points[loop[(at + loop.Count - 1) % loop.Count]];
            GeoPoint2 corner = _points[loop[at]];
            GeoPoint2 next = _points[loop[(at + 1) % loop.Count]];

            double chordX = next.X - previous.X;
            double chordY = next.Y - previous.Y;
            double cross = (corner.X - previous.X) * (next.Y - corner.Y) - (corner.Y - previous.Y) * (next.X - corner.X);

            // How far the corner stands inside the line between its neighbours, times the length of that line.
            return cross >= -_tolerance.EqualPoint * Math.Sqrt(chordX * chordX + chordY * chordY);
        }

        #endregion

        /// <summary>
        /// Numbers the vertices the faces use in the order the faces come to them, and hands the mesh over.
        /// </summary>
        private GeoMesh2 Compact(MeshKind kind)
        {
            var renumbered = new int[_points.Count];

            for (int v = 0; v < renumbered.Length; v++)
            {
                renumbered[v] = -1;
            }

            var vertices = new List<GeoPoint2>();
            var faces = new int[_faces.Count][];

            for (int f = 0; f < faces.Length; f++)
            {
                List<int> face = _faces[f];
                faces[f] = new int[face.Count];

                for (int i = 0; i < face.Count; i++)
                {
                    int v = face[i];

                    if (renumbered[v] < 0)
                    {
                        renumbered[v] = vertices.Count;
                        vertices.Add(_points[v]);
                    }

                    faces[f][i] = renumbered[v];
                }
            }

            return new GeoMesh2(kind, vertices.ToArray(), faces, _whole.ToArray(), _tolerance);
        }
    }
}
