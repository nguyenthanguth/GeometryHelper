using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// An edge of a body that may be drawn, in view coordinates, with the faces it bounds.
    /// </summary>
    internal struct CandidateEdge
    {
        internal int Object;
        internal double Ax, Ay, Az, Bx, By, Bz;
        internal EdgeKind Kind;

        /// <summary>The view-wide numbers of the faces the edge bounds; none of them hides it.</summary>
        internal int[] Faces;
    }

    /// <summary>
    /// A face turned to the viewer, which hides what lies behind it: its loops seen in the view, and its plane.
    /// </summary>
    internal sealed class Occluder
    {
        internal int Object;
        internal int Face;

        /// <summary>The corners of every loop, the boundary first; loop k runs from <see cref="Starts"/>[k] up to [k + 1].</summary>
        internal double[] Xs;
        internal double[] Ys;
        internal int[] Starts;

        internal double MinX, MinY, MaxX, MaxY, MaxZ;

        /// <summary>The unit normal in view coordinates, turned to the viewer (Nz above nought), and the plane's offset.</summary>
        internal double Nx, Ny, Nz, D;
    }

    /// <summary>
    /// One body seen in a view: its corners made one within the point tolerance, its faces as loops of those corners, the
    /// edges that may be drawn and the faces that hide.
    /// </summary>
    /// <remarks>
    /// Two faces share an edge where they run between the same two corners. A face whose side runs past a corner of the face
    /// beside it, as the faces a boolean leaves can, has that side split at the corner, so that the two still share it.
    /// </remarks>
    internal static class ViewBody
    {
        /// <summary>
        /// Adds the edges and the hiding faces of a body to the view's lists.
        /// </summary>
        /// <param name="solid">The body, wound outwards, its openings cut in already.</param>
        /// <param name="obj">The number of the object in the view.</param>
        /// <param name="frame">The view frame.</param>
        /// <param name="cut">The section plane in view coordinates (normal and offset), or null.</param>
        /// <param name="smoothAngleRad">The smooth angle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="faceBase">The view-wide number of the body's first face; moved past its last.</param>
        /// <param name="edges">The edges that may be drawn.</param>
        /// <param name="occluders">The faces that hide.</param>
        internal static void Add(GeoSolid3 solid, int obj, GeoCoordinateSystem3 frame, double[] cut, double smoothAngleRad, Tolerance tolerance, ref int faceBase, List<CandidateEdge> edges, List<Occluder> occluders)
        {
            double tol = tolerance.EqualPoint;
            double eps = tolerance.EqualVector;
            var welder = new Welder(tol);
            IReadOnlyList<GeoFace3> faces = solid.Faces;
            var loops = new List<int[]>[faces.Count];
            var normals = new double[faces.Count * 3];

            for (int f = 0; f < faces.Count; f++)
            {
                GeoFace3 face = faces[f];
                int[] boundary = Weld(face.Boundary, frame, welder);

                if (boundary == null)
                {
                    continue;
                }

                var faceLoops = new List<int[]> { boundary };

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    int[] loop = Weld(hole, frame, welder);

                    if (loop != null)
                    {
                        faceLoops.Add(loop);
                    }
                }

                loops[f] = faceLoops;
                GeoVector3 n = frame.ToLocal(face.Normal);
                normals[3 * f] = n.X;
                normals[3 * f + 1] = n.Y;
                normals[3 * f + 2] = n.Z;
            }

            double[] xs = welder.Xs.ToArray();
            double[] ys = welder.Ys.ToArray();
            double[] zs = welder.Zs.ToArray();

            List<EdgeRecord> records = Pair(loops, xs, ys, zs, tol);
            // Facets meeting at the smooth angle itself, as those of a bar of 12 sides do at 30 degrees, are smooth whichever
            // way the last bit of the cosine falls.
            double cosSmooth = Math.Cos(Math.Max(smoothAngleRad, tolerance.EqualAngleRad) + 1E-9);

            foreach (EdgeRecord record in records)
            {
                int a = record.A;
                int b = record.B;
                double dx = xs[b] - xs[a];
                double dy = ys[b] - ys[a];

                // Seen end-on, an edge is a point of the view.
                if (dx * dx + dy * dy <= tol * tol)
                {
                    continue;
                }

                EdgeKind kind = EdgeKind.Sharp;

                if (record.Faces.Count == 2 && record.Faces[0] != record.Faces[1])
                {
                    int f1 = record.Faces[0];
                    int f2 = record.Faces[1];
                    double cos = normals[3 * f1] * normals[3 * f2] + normals[3 * f1 + 1] * normals[3 * f2 + 1] + normals[3 * f1 + 2] * normals[3 * f2 + 2];

                    if (cos >= cosSmooth)
                    {
                        if (Facing(normals[3 * f1 + 2], eps) == Facing(normals[3 * f2 + 2], eps))
                        {
                            continue;
                        }

                        kind = EdgeKind.Silhouette;
                    }
                }

                if (cut != null && Math.Abs(cut[0] * xs[a] + cut[1] * ys[a] + cut[2] * zs[a] - cut[3]) <= tol && Math.Abs(cut[0] * xs[b] + cut[1] * ys[b] + cut[2] * zs[b] - cut[3]) <= tol)
                {
                    kind = EdgeKind.SectionOutline;
                }

                var faceIds = new int[record.Faces.Count];

                for (int i = 0; i < faceIds.Length; i++)
                {
                    faceIds[i] = faceBase + record.Faces[i];
                }

                edges.Add(new CandidateEdge
                {
                    Object = obj,
                    Ax = xs[a], Ay = ys[a], Az = zs[a],
                    Bx = xs[b], By = ys[b], Bz = zs[b],
                    Kind = kind,
                    Faces = faceIds,
                });
            }

            for (int f = 0; f < faces.Count; f++)
            {
                if (loops[f] == null || !(normals[3 * f + 2] > eps))
                {
                    continue;
                }

                occluders.Add(MakeOccluder(loops[f], normals, f, obj, faceBase + f, xs, ys, zs));
            }

            faceBase += faces.Count;
        }

        /// <summary>
        /// Which way a face is turned: +1 to the viewer, -1 away, 0 seen edge-on within the vector tolerance.
        /// </summary>
        private static int Facing(double nz, double eps) => nz > eps ? 1 : nz < -eps ? -1 : 0;

        private static Occluder MakeOccluder(List<int[]> faceLoops, double[] normals, int f, int obj, int faceId, double[] xs, double[] ys, double[] zs)
        {
            int count = 0;

            foreach (int[] loop in faceLoops)
            {
                count += loop.Length;
            }

            var occluder = new Occluder
            {
                Object = obj,
                Face = faceId,
                Xs = new double[count],
                Ys = new double[count],
                Starts = new int[faceLoops.Count + 1],
                MinX = double.PositiveInfinity,
                MinY = double.PositiveInfinity,
                MaxX = double.NegativeInfinity,
                MaxY = double.NegativeInfinity,
                MaxZ = double.NegativeInfinity,
                Nx = normals[3 * f],
                Ny = normals[3 * f + 1],
                Nz = normals[3 * f + 2],
            };

            int k = 0;
            double offset = 0.0;
            int[] boundary = faceLoops[0];

            for (int l = 0; l < faceLoops.Count; l++)
            {
                occluder.Starts[l] = k;

                foreach (int v in faceLoops[l])
                {
                    occluder.Xs[k] = xs[v];
                    occluder.Ys[k] = ys[v];
                    k++;
                    occluder.MinX = Math.Min(occluder.MinX, xs[v]);
                    occluder.MinY = Math.Min(occluder.MinY, ys[v]);
                    occluder.MaxX = Math.Max(occluder.MaxX, xs[v]);
                    occluder.MaxY = Math.Max(occluder.MaxY, ys[v]);
                    occluder.MaxZ = Math.Max(occluder.MaxZ, zs[v]);
                }
            }

            occluder.Starts[faceLoops.Count] = k;

            // The plane through the middle of the boundary's corners, so that a face a hair off flat stands as near as it
            // can to each of them.
            foreach (int v in boundary)
            {
                offset += occluder.Nx * xs[v] + occluder.Ny * ys[v] + occluder.Nz * zs[v];
            }

            occluder.D = offset / boundary.Length;
            return occluder;
        }

        /// <summary>
        /// The loop of a polygon as welded corners, each corner once in a row; null where fewer than three are left.
        /// </summary>
        private static int[] Weld(GeoPolygon3 polygon, GeoCoordinateSystem3 frame, Welder welder)
        {
            var ids = new List<int>(polygon.VertexCount);

            foreach (GeoPoint3 vertex in polygon.Vertices)
            {
                int id = welder.Find(vertex);

                if (id < 0)
                {
                    GeoPoint3 p = frame.ToLocal(vertex);
                    id = welder.Add(p.X, p.Y, p.Z);
                    welder.Remember(vertex, p, id);
                }

                if (ids.Count == 0 || ids[ids.Count - 1] != id)
                {
                    ids.Add(id);
                }
            }

            while (ids.Count > 1 && ids[ids.Count - 1] == ids[0])
            {
                ids.RemoveAt(ids.Count - 1);
            }

            return ids.Count >= 3 ? ids.ToArray() : null;
        }

        /// <summary>
        /// The edges of the faces, each with the faces that run along it, in the order the faces give them.
        /// </summary>
        private static List<EdgeRecord> Pair(List<int[]>[] loops, double[] xs, double[] ys, double[] zs, double tol)
        {
            var records = new List<EdgeRecord>();
            var index = new Dictionary<EdgeKey, int>();

            for (int f = 0; f < loops.Length; f++)
            {
                if (loops[f] == null)
                {
                    continue;
                }

                foreach (int[] loop in loops[f])
                {
                    for (int i = 0; i < loop.Length; i++)
                    {
                        Record(records, index, loop[i], loop[(i + 1) % loop.Length], f);
                    }
                }
            }

            // An edge run by one face only is where a side of a face runs past a corner of the face beside it, which then
            // meets it in pieces. Each such side is split at the corners of the other edges run once that lie on it, and the
            // pieces paired again; an edge still run once is the rim of an open body, and drawn as a sharp one.
            var single = new List<EdgeRecord>();
            var corners = new List<int>();

            foreach (EdgeRecord record in records)
            {
                if (record.Faces.Count == 1)
                {
                    single.Add(record);
                    corners.Add(record.A);
                    corners.Add(record.B);
                }
            }

            if (single.Count == 0)
            {
                return records;
            }

            corners.Sort();
            var split = new List<EdgeRecord>();
            var splitIndex = new Dictionary<EdgeKey, int>();

            foreach (EdgeRecord record in records)
            {
                if (record.Faces.Count != 1)
                {
                    splitIndex.Add(new EdgeKey(record.A, record.B), split.Count);
                    split.Add(record);
                }
            }

            var onEdge = new List<KeyValuePair<double, int>>();

            foreach (EdgeRecord record in single)
            {
                int a = record.From;
                int b = record.To;
                double ex = xs[b] - xs[a];
                double ey = ys[b] - ys[a];
                double ez = zs[b] - zs[a];
                double length2 = ex * ex + ey * ey + ez * ez;
                onEdge.Clear();
                int last = -1;

                foreach (int v in corners)
                {
                    if (v == last || v == a || v == b)
                    {
                        continue;
                    }

                    last = v;
                    double px = xs[v] - xs[a];
                    double py = ys[v] - ys[a];
                    double pz = zs[v] - zs[a];
                    double t = (px * ex + py * ey + pz * ez) / length2;

                    if (!(t > 0.0) || !(t < 1.0))
                    {
                        continue;
                    }

                    double qx = px - t * ex;
                    double qy = py - t * ey;
                    double qz = pz - t * ez;

                    if (qx * qx + qy * qy + qz * qz <= tol * tol)
                    {
                        onEdge.Add(new KeyValuePair<double, int>(t, v));
                    }
                }

                if (onEdge.Count == 0)
                {
                    Record(split, splitIndex, a, b, record.Faces[0]);
                    continue;
                }

                onEdge.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : p.Value.CompareTo(q.Value));
                int from = a;

                foreach (KeyValuePair<double, int> corner in onEdge)
                {
                    if (corner.Value != from)
                    {
                        Record(split, splitIndex, from, corner.Value, record.Faces[0]);
                        from = corner.Value;
                    }
                }

                Record(split, splitIndex, from, b, record.Faces[0]);
            }

            return split;
        }

        private static void Record(List<EdgeRecord> records, Dictionary<EdgeKey, int> index, int from, int to, int face)
        {
            if (from == to)
            {
                return;
            }

            var key = new EdgeKey(Math.Min(from, to), Math.Max(from, to));

            if (!index.TryGetValue(key, out int at))
            {
                at = records.Count;
                index.Add(key, at);
                records.Add(new EdgeRecord(key.A, key.B, from, to));
            }

            records[at].Faces.Add(face);
        }

        /// <summary>
        /// An edge between two welded corners, and the faces that run along it.
        /// </summary>
        private sealed class EdgeRecord
        {
            internal EdgeRecord(int a, int b, int from, int to)
            {
                A = a;
                B = b;
                From = from;
                To = to;
            }

            /// <summary>The lower corner.</summary>
            internal int A { get; }

            /// <summary>The higher corner.</summary>
            internal int B { get; }

            /// <summary>The corner the first face runs the edge from.</summary>
            internal int From { get; }

            /// <summary>The corner the first face runs the edge to.</summary>
            internal int To { get; }

            internal List<int> Faces { get; } = new List<int>(2);
        }

        /// <summary>
        /// The two corners of an edge, the lower first, as a key with a hash of its own.
        /// </summary>
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            internal EdgeKey(int a, int b)
            {
                A = a;
                B = b;
            }

            internal int A { get; }

            internal int B { get; }

            public bool Equals(EdgeKey other) => A == other.A && B == other.B;

            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

            public override int GetHashCode() => unchecked(A * 486187739 + B * 16777619);
        }

        /// <summary>
        /// Makes corners within the point tolerance of each other one, the first given standing for them all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The corners go into cells as wide as the tolerance, so two within it lie in the same cell or in cells side by
        /// side. A corner near several is made the one of them given first, whatever order the cells are read in.
        /// </para>
        /// <para>
        /// A corner of the model given again to the bit, as each corner of a body is by every face that meets there, is made
        /// what it was made the first time without being looked up again: the one it was made then still lies within the
        /// tolerance of it, and every corner kept since comes after it.
        /// </para>
        /// </remarks>
        private sealed class Welder
        {
            private readonly Dictionary<CellKey, int> _heads = new Dictionary<CellKey, int>();
            private readonly Dictionary<CellKey, int> _known = new Dictionary<CellKey, int>();
            private readonly List<int> _next = new List<int>();
            private readonly double _cell;
            private readonly double _tol2;

            internal Welder(double tol)
            {
                _cell = Math.Max(tol, 1E-9);
                _tol2 = tol * tol;
            }

            /// <summary>
            /// The corner a point of the model was made before, found by the bits of its coordinates; -1 where it was not.
            /// </summary>
            internal int Find(GeoPoint3 vertex) => _known.TryGetValue(Bits(vertex), out int id) ? id : -1;

            /// <summary>
            /// Keeps the corner a point of the model was made, where the point is finite in the view: one that is not lies
            /// within the tolerance of nothing, and is a corner of its own each time it is given.
            /// </summary>
            internal void Remember(GeoPoint3 vertex, GeoPoint3 seen, int id)
            {
                if (seen.IsValid)
                {
                    _known[Bits(vertex)] = id;
                }
            }

            private static CellKey Bits(GeoPoint3 p)
                => new CellKey(BitConverter.DoubleToInt64Bits(p.X), BitConverter.DoubleToInt64Bits(p.Y), BitConverter.DoubleToInt64Bits(p.Z));

            internal List<double> Xs { get; } = new List<double>();

            internal List<double> Ys { get; } = new List<double>();

            internal List<double> Zs { get; } = new List<double>();

            internal int Add(double x, double y, double z)
            {
                long ix = (long)Math.Floor(x / _cell);
                long iy = (long)Math.Floor(y / _cell);
                long iz = (long)Math.Floor(z / _cell);
                int best = -1;

                for (long i = ix - 1; i <= ix + 1; i++)
                {
                    for (long j = iy - 1; j <= iy + 1; j++)
                    {
                        for (long k = iz - 1; k <= iz + 1; k++)
                        {
                            if (!_heads.TryGetValue(new CellKey(i, j, k), out int at))
                            {
                                continue;
                            }

                            for (; at >= 0; at = _next[at])
                            {
                                double dx = Xs[at] - x;
                                double dy = Ys[at] - y;
                                double dz = Zs[at] - z;

                                if (dx * dx + dy * dy + dz * dz <= _tol2 && (best < 0 || at < best))
                                {
                                    best = at;
                                }
                            }
                        }
                    }
                }

                if (best >= 0)
                {
                    return best;
                }

                int id = Xs.Count;
                Xs.Add(x);
                Ys.Add(y);
                Zs.Add(z);
                var key = new CellKey(ix, iy, iz);
                _next.Add(_heads.TryGetValue(key, out int head) ? head : -1);
                _heads[key] = id;
                return id;
            }
        }

        /// <summary>
        /// A cell of the welding grid, as a key with a hash of its own.
        /// </summary>
        private readonly struct CellKey : IEquatable<CellKey>
        {
            private readonly long _x;
            private readonly long _y;
            private readonly long _z;

            internal CellKey(long x, long y, long z)
            {
                _x = x;
                _y = y;
                _z = z;
            }

            public bool Equals(CellKey other) => _x == other._x && _y == other._y && _z == other._z;

            public override bool Equals(object obj) => obj is CellKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    long h = _x * 73856093L ^ _y * 19349663L ^ _z * 83492791L;
                    return (int)h ^ (int)(h >> 32);
                }
            }
        }
    }
}
