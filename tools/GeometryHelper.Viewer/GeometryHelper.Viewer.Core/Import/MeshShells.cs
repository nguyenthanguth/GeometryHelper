using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// One body, or one loose piece of surface, of a mesh: the faces of its outer shell, the shells inside it that are voids, and whether it closes.
    /// </summary>
    internal sealed class ShellPiece
    {
        /// <summary>
        /// The faces of the piece, outer shell and voids together, by index, in ascending order.
        /// </summary>
        internal List<int> Faces = new List<int>();

        /// <summary>
        /// The faces of the outer shell, by index; for a piece that does not close, all its faces.
        /// </summary>
        internal List<int> Outer = new List<int>();

        /// <summary>
        /// The shells inside the outer one that run inside out, one list of faces each: the voids of the body.
        /// </summary>
        internal List<List<int>> Voids = new List<List<int>>();

        /// <summary>
        /// Whether every edge of the piece is used by two faces that run it opposite ways, so that it bounds a volume.
        /// </summary>
        internal bool Closed;

        /// <summary>
        /// Whether the piece is a closed shell that runs inside out and has no body round it: a void with nothing to be the void of.
        /// </summary>
        internal bool InsideOut;
    }

    /// <summary>
    /// Tells the bodies of a mesh apart, by its edges and not by the corners its faces share. A file that writes a body as triangles gives each triangle
    /// corners of its own, or corners that are the same to the bit, or a corner a hair off, so what makes two faces one body is an edge, matched
    /// within the tolerance: where two faces use it and run it opposite ways, they are joined. An edge that more faces use, as the four of two boxes
    /// lying face to face use theirs, is told by going round it: the faces are put in order by angle, and each face that begins a wedge of material is
    /// paired with the face that ends it, so that two boxes stay two bodies and a box and its neighbour are not one. Closed shells that run inside out are
    /// the voids of the smallest closed shell that holds them. Pieces that do not close and meet along an edge are one piece, so that a body with a
    /// face turned over is still one item, with the fault in it.
    /// </summary>
    internal static class MeshShells
    {
        private const double Quantum = 1E-9;

        private struct Use
        {
            internal long Key;
            internal int Face;
            internal bool Forward;
            internal double Angle;
        }

        /// <summary>
        /// Splits a mesh into its pieces, in the order of their first face.
        /// </summary>
        /// <param name="points">The corners.</param>
        /// <param name="faces">The faces, each the indexes of its corners in order.</param>
        /// <param name="tolerance">How far apart two corners may be and be one.</param>
        /// <param name="cancel">Stops the work.</param>
        internal static List<ShellPiece> Split(IReadOnlyList<Vec3> points, IReadOnlyList<int[]> faces, double tolerance, CancellationToken cancel)
        {
            int count = faces.Count;
            int[] cluster = Cluster(points, tolerance, out Vec3[] rep);

            // The faces on their clusters, a corner that repeats the one before dropped; a face of fewer than three is no face to join by.
            var corners = new int[count][];
            var normal = new Vec3[count];
            var valid = new bool[count];

            for (int f = 0; f < count; f++)
            {
                var ids = new List<int>(faces[f].Length);

                foreach (int p in faces[f])
                {
                    int id = p >= 0 && p < cluster.Length ? cluster[p] : -1;

                    if (id >= 0 && (ids.Count == 0 || ids[ids.Count - 1] != id))
                    {
                        ids.Add(id);
                    }
                }

                while (ids.Count > 1 && ids[0] == ids[ids.Count - 1])
                {
                    ids.RemoveAt(ids.Count - 1);
                }

                corners[f] = ids.ToArray();

                if (ids.Count >= 3)
                {
                    normal[f] = Newell(ids, rep);
                    valid[f] = normal[f].Length > 0.0;
                }
            }

            // The uses of every edge, ordered by the edge.
            var uses = new List<Use>();

            for (int f = 0; f < count; f++)
            {
                if (!valid[f])
                {
                    continue;
                }

                int[] ids = corners[f];

                for (int k = 0; k < ids.Length; k++)
                {
                    int a = ids[k];
                    int b = ids[(k + 1) % ids.Length];

                    if (a == b)
                    {
                        continue;
                    }

                    uses.Add(new Use { Key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a, Face = f, Forward = a < b });
                }
            }

            Use[] sorted = uses.ToArray();
            Array.Sort(sorted, (x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Face != y.Face ? x.Face.CompareTo(y.Face) : x.Forward.CompareTo(y.Forward));

            var parent = new int[count];
            var unpaired = new bool[count];

            for (int f = 0; f < count; f++)
            {
                parent[f] = f;
                unpaired[f] = !valid[f];
            }

            var loose = new List<int[]>();
            int start = 0;

            while (start < sorted.Length)
            {
                if ((start & 8191) == 0)
                {
                    cancel.ThrowIfCancellationRequested();
                }

                int end = start + 1;

                while (end < sorted.Length && sorted[end].Key == sorted[start].Key)
                {
                    end++;
                }

                int n = end - start;
                bool[] paired = Pair(sorted, start, n, normal, rep, parent, out bool overlapping);

                var left = new List<int>();

                for (int i = 0; i < n; i++)
                {
                    if (!paired[i])
                    {
                        unpaired[sorted[start + i].Face] = true;
                        left.Add(sorted[start + i].Face);
                    }
                }

                // Bodies that run into each other along an edge are not joined by it, whatever it leaves unpaired: each is left to be closed on its own.
                if (left.Count > 1 && !overlapping)
                {
                    loose.Add(left.ToArray());
                }

                start = end;
            }

            // Faces that run an edge the same way, or alone, or that more than two share without balance, are the fault of one body: they are one piece.
            foreach (int[] group in loose)
            {
                for (int i = 1; i < group.Length; i++)
                {
                    Union(parent, group[0], group[i]);
                }
            }

            // Pieces, in the order of their first face.
            var pieceOf = new Dictionary<int, int>();
            var members = new List<List<int>>();

            for (int f = 0; f < count; f++)
            {
                int root = Find(parent, f);

                if (!pieceOf.TryGetValue(root, out int at))
                {
                    at = members.Count;
                    pieceOf.Add(root, at);
                    members.Add(new List<int>());
                }

                members[at].Add(f);
            }

            var closed = new bool[members.Count];
            var volume = new double[members.Count];

            for (int p = 0; p < members.Count; p++)
            {
                closed[p] = members[p].All(f => !unpaired[f]);

                if (closed[p])
                {
                    volume[p] = Volume(members[p], faces, points);
                }
            }

            return Assemble(members, closed, volume, faces, points, tolerance, cancel);
        }

        /// <summary>
        /// Takes some faces of a mesh out as a mesh of their own: the corners they name, and the faces on them.
        /// </summary>
        /// <param name="points">The corners of the mesh.</param>
        /// <param name="faces">Its faces.</param>
        /// <param name="chosen">The indexes of the faces to take, ascending.</param>
        /// <param name="fileOrder">Whether the corners keep the order they have in the mesh, else the order the chosen faces first name them.</param>
        /// <param name="corners">The corners of the new mesh.</param>
        /// <param name="taken">Its faces.</param>
        internal static void Extract(IReadOnlyList<Vec3> points, IReadOnlyList<int[]> faces, IReadOnlyList<int> chosen, bool fileOrder, out List<Vec3> corners, out List<int[]> taken)
        {
            var local = new Dictionary<int, int>();
            corners = new List<Vec3>();
            taken = new List<int[]>(chosen.Count);

            if (fileOrder)
            {
                var used = new SortedSet<int>();

                foreach (int f in chosen)
                {
                    foreach (int i in faces[f])
                    {
                        used.Add(i);
                    }
                }

                foreach (int i in used)
                {
                    local.Add(i, corners.Count);
                    corners.Add(points[i]);
                }
            }

            foreach (int f in chosen)
            {
                int[] face = faces[f];
                var mapped = new int[face.Length];

                for (int k = 0; k < face.Length; k++)
                {
                    if (!local.TryGetValue(face[k], out int at))
                    {
                        at = corners.Count;
                        local.Add(face[k], at);
                        corners.Add(points[face[k]]);
                    }

                    mapped[k] = at;
                }

                taken.Add(mapped);
            }
        }

        #region Clusters

        /// <summary>
        /// Puts the corners into clusters: the connected pieces of the graph in which two corners are joined where they are within the tolerance of each other,
        /// found with a grid a tolerance across. A cluster is the same whatever order the corners come in, and it stands at its lowest corner (by x, then y,
        /// then z), so that nothing that follows depends on the order of a file.
        /// </summary>
        private static int[] Cluster(IReadOnlyList<Vec3> points, double tolerance, out Vec3[] rep)
        {
            int n = points.Count;

            // Corners that are the same to the bit are one, so that a hundred triangles meeting at an apex do not make a hundred squared tests.
            var first = new Dictionary<BitKey, int>();
            var unique = new List<int>();
            var uniqueOf = new int[n];

            for (int i = 0; i < n; i++)
            {
                if (!first.TryGetValue(new BitKey(points[i]), out int at))
                {
                    at = unique.Count;
                    first.Add(new BitKey(points[i]), at);
                    unique.Add(i);
                }

                uniqueOf[i] = at;
            }

            int m = unique.Count;
            var parent = new int[m];
            double cell = tolerance > 0.0 ? tolerance : 1E-12;
            var grid = new Dictionary<CellKey, List<int>>(new CellComparer());

            for (int u = 0; u < m; u++)
            {
                parent[u] = u;
                Vec3 p = points[unique[u]];

                if (!p.IsFinite)
                {
                    continue;
                }

                long cx = Cell(p.X, cell);
                long cy = Cell(p.Y, cell);
                long cz = Cell(p.Z, cell);

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            if (grid.TryGetValue(new CellKey(cx + dx, cy + dy, cz + dz), out List<int> near))
                            {
                                foreach (int other in near)
                                {
                                    if ((points[unique[other]] - p).Length <= tolerance)
                                    {
                                        Union(parent, u, other);
                                    }
                                }
                            }
                        }
                    }
                }

                var key = new CellKey(cx, cy, cz);

                if (!grid.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(1);
                    grid.Add(key, list);
                }

                list.Add(u);
            }

            // The lowest corner of each piece stands for it.
            var lowest = new int[m];

            for (int u = 0; u < m; u++)
            {
                lowest[u] = -1;
            }

            for (int u = 0; u < m; u++)
            {
                int root = Find(parent, u);

                if (lowest[root] < 0 || Lower(points[unique[u]], points[unique[lowest[root]]]))
                {
                    lowest[root] = u;
                }
            }

            var ids = new int[m];
            var representatives = new List<Vec3>();
            var idOfRoot = new Dictionary<int, int>();

            for (int u = 0; u < m; u++)
            {
                int root = Find(parent, u);

                if (!idOfRoot.TryGetValue(root, out int id))
                {
                    id = representatives.Count;
                    idOfRoot.Add(root, id);
                    representatives.Add(points[unique[lowest[root]]]);
                }

                ids[u] = id;
            }

            var cluster = new int[n];

            for (int i = 0; i < n; i++)
            {
                cluster[i] = ids[uniqueOf[i]];
            }

            rep = representatives.ToArray();
            return cluster;
        }

        private static bool Lower(Vec3 a, Vec3 b)
            => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;

        private static long Cell(double v, double cell)
        {
            double c = Math.Floor(v / cell);
            return c > 4E18 ? long.MaxValue / 2 : c < -4E18 ? long.MinValue / 2 : (long)c;
        }

        private readonly struct CellKey
        {
            internal readonly long X;
            internal readonly long Y;
            internal readonly long Z;

            internal CellKey(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private sealed class CellComparer : IEqualityComparer<CellKey>
        {
            public bool Equals(CellKey a, CellKey b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

            public int GetHashCode(CellKey k)
            {
                unchecked
                {
                    ulong h = (ulong)k.X * 0x9E3779B97F4A7C15UL;
                    h ^= h >> 31;
                    h = (h + (ulong)k.Y) * 0xBF58476D1CE4E5B9UL;
                    h ^= h >> 29;
                    h = (h + (ulong)k.Z) * 0x94D049BB133111EBUL;
                    h ^= h >> 32;
                    return (int)h;
                }
            }
        }

        #endregion

        #region Edges

        /// <summary>
        /// Pairs the uses of one edge, joining the faces of each pair. Two uses that run opposite ways are a pair; more than two are put in order round the
        /// edge, and each that begins a wedge of material is paired with the one that ends it.
        /// </summary>
        private static bool[] Pair(Use[] uses, int start, int n, Vec3[] normal, Vec3[] rep, int[] parent, out bool overlapping)
        {
            var paired = new bool[n];
            overlapping = false;

            if (n == 2)
            {
                if (uses[start].Forward != uses[start + 1].Forward && uses[start].Face != uses[start + 1].Face)
                {
                    Union(parent, uses[start].Face, uses[start + 1].Face);
                    paired[0] = paired[1] = true;
                }

                return paired;
            }

            int forward = 0;

            for (int i = 0; i < n; i++)
            {
                forward += uses[start + i].Forward ? 1 : 0;
            }

            if (n % 2 != 0 || forward * 2 != n)
            {
                return paired;
            }

            // The edge from its lower corner to its higher, and a frame across it to measure angles in.
            int a = (int)(uses[start].Key >> 32);
            int b = (int)(uses[start].Key & 0xFFFFFFFFL);
            Vec3 e = (rep[b] - rep[a]).Normalized();

            if (e.Length == 0.0)
            {
                return paired;
            }

            Vec3 axis = Math.Abs(e.X) <= Math.Abs(e.Y) && Math.Abs(e.X) <= Math.Abs(e.Z) ? new Vec3(1, 0, 0) : Math.Abs(e.Y) <= Math.Abs(e.Z) ? new Vec3(0, 1, 0) : new Vec3(0, 0, 1);
            Vec3 u = Vec3.Cross(e, axis).Normalized();
            Vec3 v = Vec3.Cross(e, u);
            var order = new List<int>(n);

            for (int i = 0; i < n; i++)
            {
                // The way the face leans away from the edge: to the left of the edge as the face runs it, in the plane of the face.
                Use use = uses[start + i];
                Vec3 d = use.Forward ? e : -e;
                Vec3 w = Vec3.Cross(normal[use.Face], d);
                // In steps of a billionth of a radian, and half a turn is one angle whichever side of it the rounding falls, so that faces on it tie.
                double steps = Math.Round(Math.Atan2(Vec3.Dot(w, v), Vec3.Dot(w, u)) / Quantum);
                double half = Math.Round(Math.PI / Quantum);
                uses[start + i].Angle = (steps >= half ? steps - (2.0 * half) : steps) * Quantum;
                order.Add(i);
            }

            // A use that runs the edge backwards has its material on the side of increasing angle: it begins a wedge. One that runs it forwards ends one.
            // Where a wedge ends at the angle another begins, the end comes first.
            order.Sort((x, y) =>
            {
                Use p = uses[start + x];
                Use q = uses[start + y];
                int c = p.Angle.CompareTo(q.Angle);

                if (c != 0)
                {
                    return c;
                }

                c = q.Forward.CompareTo(p.Forward);
                return c != 0 ? c : p.Face.CompareTo(q.Face);
            });

            // The wedges of material round an edge begin and end in turn. Where two begin or two end one after the other, the bodies overlap along it, and
            // nothing is paired: which face meets which cannot be told from the edge.
            for (int k = 0; k < n; k++)
            {
                if (uses[start + order[k]].Forward == uses[start + order[(k + 1) % n]].Forward)
                {
                    overlapping = true;
                    return paired;
                }
            }

            var open = new Stack<int>();
            var spare = new List<int>();

            foreach (int i in order)
            {
                if (!uses[start + i].Forward)
                {
                    open.Push(i);
                }
                else if (open.Count > 0)
                {
                    Join(uses, start, open.Pop(), i, paired, parent);
                }
                else
                {
                    spare.Add(i);
                }
            }

            // The wedge that runs through the angle where the order starts: the last begun is closed by the first spare end.
            foreach (int i in spare)
            {
                if (open.Count > 0)
                {
                    Join(uses, start, open.Pop(), i, paired, parent);
                }
            }

            return paired;
        }

        private static void Join(Use[] uses, int start, int begin, int end, bool[] paired, int[] parent)
        {
            if (uses[start + begin].Face == uses[start + end].Face)
            {
                return;
            }

            paired[begin] = paired[end] = true;
            Union(parent, uses[start + begin].Face, uses[start + end].Face);
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);

            if (ra != rb)
            {
                // The lower root wins, so that the result does not depend on the order of the unions.
                parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
        }

        private static Vec3 Newell(List<int> ids, Vec3[] rep)
        {
            double x = 0.0, y = 0.0, z = 0.0;

            for (int i = 0; i < ids.Count; i++)
            {
                Vec3 p = rep[ids[i]];
                Vec3 q = rep[ids[(i + 1) % ids.Count]];
                x += (p.Y - q.Y) * (p.Z + q.Z);
                y += (p.Z - q.Z) * (p.X + q.X);
                z += (p.X - q.X) * (p.Y + q.Y);
            }

            return new Vec3(x, y, z).Normalized();
        }

        #endregion

        #region Shells and voids

        /// <summary>
        /// The volume of a closed shell by the divergence theorem, negative where it runs inside out.
        /// </summary>
        private static double Volume(List<int> faceIndexes, IReadOnlyList<int[]> faces, IReadOnlyList<Vec3> points)
        {
            Vec3 origin = default(Vec3);
            bool first = true;
            double sum = 0.0;

            foreach (int f in faceIndexes)
            {
                int[] face = faces[f];

                if (face.Length < 3)
                {
                    continue;
                }

                if (first)
                {
                    origin = points[face[0]];
                    first = false;
                }

                Vec3 a = points[face[0]] - origin;

                for (int k = 1; k + 1 < face.Length; k++)
                {
                    sum += Vec3.Dot(a, Vec3.Cross(points[face[k]] - origin, points[face[k + 1]] - origin));
                }
            }

            return sum / 6.0;
        }

        private static List<ShellPiece> Assemble(List<List<int>> members, bool[] closed, double[] volume, IReadOnlyList<int[]> faces, IReadOnlyList<Vec3> points, double tolerance, CancellationToken cancel)
        {
            // A closed shell that runs inside out is a void; it goes to the smallest outward shell that holds it.
            var voidOf = new int[members.Count];
            var boxes = new Bounds3[members.Count];

            for (int p = 0; p < members.Count; p++)
            {
                voidOf[p] = -1;

                if (closed[p])
                {
                    Bounds3 box = default(Bounds3);

                    foreach (int f in members[p])
                    {
                        foreach (int i in faces[f])
                        {
                            box = box.Include(points[i]);
                        }
                    }

                    boxes[p] = box;
                }
            }

            double slack = Math.Max(tolerance, 1E-9);

            for (int p = 0; p < members.Count; p++)
            {
                if (!closed[p] || !(volume[p] < 0.0))
                {
                    continue;
                }

                cancel.ThrowIfCancellationRequested();
                int[] firstFace = faces[members[p][0]];
                Vec3 inside = Centre(firstFace, points);
                int best = -1;

                for (int o = 0; o < members.Count; o++)
                {
                    if (!closed[o] || !(volume[o] > 0.0) || !Holds(boxes[o], boxes[p], slack))
                    {
                        continue;
                    }

                    if ((best < 0 || volume[o] < volume[best]) && Inside(members[o], faces, points, inside))
                    {
                        best = o;
                    }
                }

                voidOf[p] = best;
            }

            var pieces = new List<ShellPiece>();
            var pieceOfShell = new int[members.Count];

            for (int p = 0; p < members.Count; p++)
            {
                pieceOfShell[p] = -1;

                if (voidOf[p] >= 0)
                {
                    continue;
                }

                pieceOfShell[p] = pieces.Count;
                var piece = new ShellPiece { Closed = closed[p], InsideOut = closed[p] && volume[p] < 0.0 };
                piece.Outer.AddRange(members[p]);
                piece.Faces.AddRange(members[p]);
                pieces.Add(piece);
            }

            for (int p = 0; p < members.Count; p++)
            {
                if (voidOf[p] >= 0)
                {
                    ShellPiece owner = pieces[pieceOfShell[voidOf[p]]];
                    owner.Voids.Add(members[p]);
                    owner.Faces.AddRange(members[p]);
                }
            }

            foreach (ShellPiece piece in pieces)
            {
                piece.Faces.Sort();
            }

            // In the order of the first face of each, voids included.
            pieces.Sort((a, b) => a.Faces[0].CompareTo(b.Faces[0]));
            return pieces;
        }

        private static bool Holds(Bounds3 outer, Bounds3 inner, double slack)
            => outer.HasValue && inner.HasValue
                && outer.Min.X - slack <= inner.Min.X && outer.Min.Y - slack <= inner.Min.Y && outer.Min.Z - slack <= inner.Min.Z
                && outer.Max.X + slack >= inner.Max.X && outer.Max.Y + slack >= inner.Max.Y && outer.Max.Z + slack >= inner.Max.Z;

        private static Vec3 Centre(int[] face, IReadOnlyList<Vec3> points)
        {
            double x = 0.0, y = 0.0, z = 0.0;

            foreach (int i in face)
            {
                x += points[i].X;
                y += points[i].Y;
                z += points[i].Z;
            }

            return new Vec3(x / face.Length, y / face.Length, z / face.Length);
        }

        /// <summary>
        /// Whether a point is inside a closed shell, by the parity of the crossings of a ray with its faces. The ray's direction is one no face is likely to be
        /// parallel to or an edge to lie along.
        /// </summary>
        private static bool Inside(List<int> shell, IReadOnlyList<int[]> faces, IReadOnlyList<Vec3> points, Vec3 p)
        {
            var direction = new Vec3(0.5773502691896258, 0.3131593720119837, 0.7517146271033391);
            int crossings = 0;

            foreach (int f in shell)
            {
                int[] face = faces[f];

                for (int k = 1; k + 1 < face.Length; k++)
                {
                    if (RayHits(p, direction, points[face[0]], points[face[k]], points[face[k + 1]]))
                    {
                        crossings++;
                    }
                }
            }

            return (crossings & 1) == 1;
        }

        private static bool RayHits(Vec3 origin, Vec3 direction, Vec3 a, Vec3 b, Vec3 c)
        {
            Vec3 e1 = b - a;
            Vec3 e2 = c - a;
            Vec3 h = Vec3.Cross(direction, e2);
            double det = Vec3.Dot(e1, h);

            if (Math.Abs(det) < 1E-300)
            {
                return false;
            }

            double inv = 1.0 / det;
            Vec3 s = origin - a;
            double u = inv * Vec3.Dot(s, h);

            if (u < 0.0 || u > 1.0)
            {
                return false;
            }

            Vec3 q = Vec3.Cross(s, e1);
            double v = inv * Vec3.Dot(direction, q);

            if (v < 0.0 || u + v > 1.0)
            {
                return false;
            }

            return inv * Vec3.Dot(e2, q) > 0.0;
        }

        #endregion
    }
}
