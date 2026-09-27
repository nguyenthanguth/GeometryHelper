using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The smallest convex body holding a set of points in space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built incrementally: a tetrahedron on four points far apart, then each further point in turn. A point
    /// within the point tolerance of the hull so far, or inside it, changes nothing. One outside it sees some
    /// faces; those go, and the rim they leave — the horizon — is joined to the point with a fan of new faces.
    /// The triangles are merged where they lie in one plane, so a box comes back with six faces.
    /// </para>
    /// <para>
    /// The work is quadratic in the worst case, which is thousands of points in a moment and fine for the hull
    /// of a part; it is not meant for a laser scan.
    /// </para>
    /// </remarks>
    public static class ConvexHull3
    {
        private struct Face
        {
            public int A;
            public int B;
            public int C;
            public GeoVector3 Normal;
            public double Offset;
            public bool Alive;
        }

        /// <summary>
        /// Gets the convex hull of some points, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Of(IEnumerable<GeoPoint3> points) => Of(points, Tolerance.Global);

        /// <summary>
        /// Gets the convex hull of some points, within a tolerance.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="tolerance">The tolerance deciding whether a point lies on the hull.</param>
        /// <returns>The hull, closed and wound outwards, its corners taken from the points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the points span no volume: fewer than four, or all in one plane.</exception>
        public static GeoSolid3 Of(IEnumerable<GeoPoint3> points, Tolerance tolerance)
        {
            if (!TryOf(points, out GeoSolid3 hull, tolerance))
            {
                throw new ArgumentException("The points span no volume: there are fewer than four, or all lie in one plane.", nameof(points));
            }

            return hull;
        }

        /// <summary>
        /// Tries to get the convex hull of some points, using the default tolerance.
        /// </summary>
        public static bool TryOf(IEnumerable<GeoPoint3> points, out GeoSolid3 hull) => TryOf(points, out hull, Tolerance.Global);

        /// <summary>
        /// Tries to get the convex hull of some points, within a tolerance.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="hull">The hull, closed and wound outwards; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance deciding whether a point lies on the hull.</param>
        /// <returns>false when the points span no volume.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public static bool TryOf(IEnumerable<GeoPoint3> points, out GeoSolid3 hull, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            hull = null;
            var p = new List<GeoPoint3>(points);
            double eps = tolerance.EqualPoint;

            if (p.Count < 4 || !TryStart(p, eps, out int i0, out int i1, out int i2, out int i3))
            {
                return false;
            }

            var faces = new List<Face>();
            var owners = new Dictionary<long, int>();

            GeoPoint3 inside = new GeoPoint3(
                (p[i0].X + p[i1].X + p[i2].X + p[i3].X) / 4,
                (p[i0].Y + p[i1].Y + p[i2].Y + p[i3].Y) / 4,
                (p[i0].Z + p[i1].Z + p[i2].Z + p[i3].Z) / 4);

            AddOutward(faces, owners, p, i0, i1, i2, inside);
            AddOutward(faces, owners, p, i0, i1, i3, inside);
            AddOutward(faces, owners, p, i0, i2, i3, inside);
            AddOutward(faces, owners, p, i1, i2, i3, inside);

            var visible = new List<int>();
            var seen = new HashSet<int>();

            for (int k = 0; k < p.Count; k++)
            {
                if (k == i0 || k == i1 || k == i2 || k == i3)
                {
                    continue;
                }

                visible.Clear();
                seen.Clear();

                for (int f = 0; f < faces.Count; f++)
                {
                    if (faces[f].Alive && Height(faces[f], p[k]) > eps)
                    {
                        visible.Add(f);
                        seen.Add(f);
                    }
                }

                if (visible.Count == 0)
                {
                    continue;
                }

                // The horizon: every edge of a visible face whose neighbour across it is not visible.
                var horizon = new List<(int, int)>();

                foreach (int f in visible)
                {
                    Face face = faces[f];

                    foreach ((int a, int b) in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
                    {
                        if (!owners.TryGetValue(Key(b, a), out int twin) || !seen.Contains(twin))
                        {
                            horizon.Add((a, b));
                        }
                    }
                }

                foreach (int f in visible)
                {
                    Face face = faces[f];
                    face.Alive = false;
                    faces[f] = face;
                    owners.Remove(Key(face.A, face.B));
                    owners.Remove(Key(face.B, face.C));
                    owners.Remove(Key(face.C, face.A));
                }

                // Each horizon edge keeps the way round its visible face had it, so the new face on it faces out.
                foreach ((int a, int b) in horizon)
                {
                    Add(faces, owners, p, a, b, k);
                }
            }

            var triangles = new List<GeoFace3>();

            foreach (Face face in faces)
            {
                if (face.Alive)
                {
                    triangles.Add(new GeoFace3(new GeoPolygon3(p[face.A], p[face.B], p[face.C])));
                }
            }

            hull = Merge3.CoplanarFaces(new GeoSolid3(triangles), tolerance);
            return true;
        }

        private static bool TryStart(List<GeoPoint3> p, double eps, out int i0, out int i1, out int i2, out int i3)
        {
            i0 = 0;

            for (int k = 1; k < p.Count; k++)
            {
                if (p[k].X < p[i0].X || (p[k].X == p[i0].X && (p[k].Y < p[i0].Y || (p[k].Y == p[i0].Y && p[k].Z < p[i0].Z))))
                {
                    i0 = k;
                }
            }

            GeoPoint3 a = p[i0];
            i1 = Farthest(p, k => a.DistanceTo(p[k]));
            GeoVector3 along = a.GetVectorTo(p[i1]);
            int first = i0, second = i1;

            i2 = Farthest(p, k => along.CrossProduct(a.GetVectorTo(p[k])).Length / Math.Max(along.Length, double.Epsilon));
            GeoVector3 normal = along.CrossProduct(a.GetVectorTo(p[i2]));
            int third = i2;

            i3 = Farthest(p, k => Math.Abs(normal.DotProduct(a.GetVectorTo(p[k]))) / Math.Max(normal.Length, double.Epsilon));

            return p[first].DistanceTo(p[second]) > eps
                && along.CrossProduct(a.GetVectorTo(p[third])).Length / along.Length > eps
                && normal.Length > 0.0
                && Math.Abs(normal.DotProduct(a.GetVectorTo(p[i3]))) / normal.Length > eps;
        }

        private static int Farthest(List<GeoPoint3> p, Func<int, double> reach)
        {
            int best = 0;
            double most = double.NegativeInfinity;

            for (int k = 0; k < p.Count; k++)
            {
                double r = reach(k);

                if (r > most)
                {
                    most = r;
                    best = k;
                }
            }

            return best;
        }

        private static void AddOutward(List<Face> faces, Dictionary<long, int> owners, List<GeoPoint3> p, int a, int b, int c, GeoPoint3 inside)
        {
            GeoVector3 normal = p[a].GetVectorTo(p[b]).CrossProduct(p[a].GetVectorTo(p[c]));

            if (normal.DotProduct(p[a].GetVectorTo(inside)) > 0.0)
            {
                Add(faces, owners, p, a, c, b);
            }
            else
            {
                Add(faces, owners, p, a, b, c);
            }
        }

        private static void Add(List<Face> faces, Dictionary<long, int> owners, List<GeoPoint3> p, int a, int b, int c)
        {
            GeoVector3 normal = p[a].GetVectorTo(p[b]).CrossProduct(p[a].GetVectorTo(p[c]));
            double length = normal.Length;
            GeoVector3 unit = length > 0.0 ? normal.Multiply(1.0 / length) : normal;

            faces.Add(new Face
            {
                A = a,
                B = b,
                C = c,
                Normal = unit,
                Offset = unit.DotProduct(new GeoVector3(p[a].X, p[a].Y, p[a].Z)),
                Alive = true,
            });

            int index = faces.Count - 1;
            owners[Key(a, b)] = index;
            owners[Key(b, c)] = index;
            owners[Key(c, a)] = index;
        }

        private static double Height(Face face, GeoPoint3 point)
            => face.Normal.DotProduct(new GeoVector3(point.X, point.Y, point.Z)) - face.Offset;

        private static long Key(int from, int to) => ((long)from << 32) | (uint)to;
    }
}
