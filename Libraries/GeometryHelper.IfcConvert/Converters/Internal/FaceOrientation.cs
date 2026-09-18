using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Makes the faces of a closed body wind consistently and point outwards.
    /// <para>
    /// xBIM exposes a face's loops in edge order, which ignores the face orientation. Bodies produced by a
    /// boolean (IfcBooleanClippingResult, cut openings) therefore mix outward and inward faces: the walls of a
    /// pocket come from the cutting tool and arrive reversed. Checking only the sign of the total volume cannot
    /// repair that, so orientation is propagated across shared edges instead: two faces meeting on an edge of a
    /// consistently oriented surface traverse it in opposite directions.
    /// </para>
    /// </summary>
    internal static class FaceOrientation
    {
        /// <summary>
        /// Returns the faces reoriented so that neighbours agree and every connected shell encloses positive volume.
        /// </summary>
        /// <param name="faces">The faces of one body.</param>
        /// <param name="tolerance">Tolerance used to match shared vertices.</param>
        public static List<GeoFace3> Orient(IReadOnlyList<GeoFace3> faces, Tolerance tolerance)
        {
            int count = faces.Count;
            PointIndex index = new PointIndex(tolerance.EqualPoint);

            // Undirected edge -> (face, traversed from the lower to the higher vertex index)
            Dictionary<long, List<(int Face, bool Forward)>> edges = new Dictionary<long, List<(int, bool)>>();
            List<long>[] faceEdges = new List<long>[count];

            for (int f = 0; f < count; f++)
            {
                faceEdges[f] = new List<long>();
                GeoFace3 face = faces[f];
                AddRing(face.Boundary.Vertices, false, f, index, edges, faceEdges[f]);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    // Treat every hole as running against the boundary, whichever way it was stored.
                    bool reverse = hole.Normal.DotProduct(face.Normal) > 0.0;
                    AddRing(hole.Vertices, reverse, f, index, edges, faceEdges[f]);
                }
            }

            bool[] visited = new bool[count];
            bool[] flip = new bool[count];
            List<GeoFace3> result = new List<GeoFace3>(count);

            for (int seed = 0; seed < count; seed++)
            {
                if (visited[seed])
                {
                    continue;
                }

                List<int> component = new List<int>();
                Queue<int> queue = new Queue<int>();
                visited[seed] = true;
                queue.Enqueue(seed);

                while (queue.Count > 0)
                {
                    int f = queue.Dequeue();
                    component.Add(f);

                    foreach (long key in faceEdges[f])
                    {
                        List<(int Face, bool Forward)> users = edges[key];
                        if (users.Count != 2)
                        {
                            // Open or non-manifold edge: it says nothing reliable about orientation.
                            continue;
                        }

                        (int Face, bool Forward) mine = users[0].Face == f ? users[0] : users[1];
                        (int Face, bool Forward) other = users[0].Face == f ? users[1] : users[0];
                        if (other.Face == f || visited[other.Face])
                        {
                            continue;
                        }

                        // Consistent neighbours traverse the shared edge in opposite directions.
                        bool mineForward = mine.Forward ^ flip[f];
                        flip[other.Face] = other.Forward == mineForward;
                        visited[other.Face] = true;
                        queue.Enqueue(other.Face);
                    }
                }

                // A consistently wound shell is either all outward or all inward; make it outward.
                double signedVolume = 0.0;
                foreach (int f in component)
                {
                    GeoFace3 oriented = flip[f] ? faces[f].Flip() : faces[f];
                    signedVolume += SignedVolumeContribution(oriented);
                }

                foreach (int f in component)
                {
                    bool outward = flip[f] ^ (signedVolume < 0.0);
                    result.Add(outward ? faces[f].Flip() : faces[f]);
                }
            }

            return result;
        }

        private static void AddRing(IReadOnlyList<GeoPoint3> ring, bool reverse, int face, PointIndex index,
            Dictionary<long, List<(int, bool)>> edges, List<long> faceEdges)
        {
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                int a = index.GetIndex(ring[i]);
                int b = index.GetIndex(ring[(i + 1) % n]);
                if (a == b)
                {
                    continue;
                }

                if (reverse)
                {
                    (a, b) = (b, a);
                }

                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (!edges.TryGetValue(key, out List<(int, bool)> users))
                {
                    users = new List<(int, bool)>(2);
                    edges[key] = users;
                }

                users.Add((face, a < b));
                faceEdges.Add(key);
            }
        }

        // Divergence theorem: a planar face contributes (area * normal . point) / 3 to the enclosed volume.
        private static double SignedVolumeContribution(GeoFace3 face)
        {
            GeoPoint3 p = face.Boundary.Vertices[0];
            GeoVector3 n = face.Normal;
            return face.Area * (n.X * p.X + n.Y * p.Y + n.Z * p.Z) / 3.0;
        }

        /// <summary>
        /// Assigns one index per distinct position, matching positions within a tolerance through a hash grid.
        /// </summary>
        private sealed class PointIndex
        {
            private readonly Dictionary<(long, long, long), List<int>> _cells = new Dictionary<(long, long, long), List<int>>();
            private readonly List<GeoPoint3> _points = new List<GeoPoint3>();
            private readonly double _tolerance;
            private readonly double _cellSize;

            public PointIndex(double tolerance)
            {
                _tolerance = tolerance;
                _cellSize = tolerance > 0.0 ? tolerance * 2.0 : 1E-9;
            }

            public int GetIndex(GeoPoint3 point)
            {
                long cx = (long)Math.Floor(point.X / _cellSize);
                long cy = (long)Math.Floor(point.Y / _cellSize);
                long cz = (long)Math.Floor(point.Z / _cellSize);

                for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                for (long dz = -1; dz <= 1; dz++)
                {
                    if (_cells.TryGetValue((cx + dx, cy + dy, cz + dz), out List<int> candidates))
                    {
                        foreach (int candidate in candidates)
                        {
                            GeoPoint3 q = _points[candidate];
                            double ddx = q.X - point.X, ddy = q.Y - point.Y, ddz = q.Z - point.Z;
                            if (ddx * ddx + ddy * ddy + ddz * ddz <= _tolerance * _tolerance)
                            {
                                return candidate;
                            }
                        }
                    }
                }

                int created = _points.Count;
                _points.Add(point);
                if (!_cells.TryGetValue((cx, cy, cz), out List<int> cell))
                {
                    cell = new List<int>(1);
                    _cells[(cx, cy, cz)] = cell;
                }

                cell.Add(created);
                return created;
            }
        }
    }
}
