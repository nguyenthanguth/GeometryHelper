using System;
using System.Collections.Generic;
using System.Numerics;
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
    /// Which faces a point sees is read in floating point there, and a point is taken only where it stands further than the
    /// tolerance off a face's plane. That build stops at the first rim that is not one loop, or as soon as it stands more
    /// triangles than a closed surface on the points taken can have; and where the hull it gives does not close, folds back
    /// over itself, or leaves a point further than the tolerance outside its triangles, the hull is built again with an
    /// exact test of sides, from the widest tetrahedron the points give, each point left out only where it is within the
    /// tolerance of the triangles themselves. A hull that closes and holds every point is kept as it was built, to the bit.
    /// The faces of a plate a few thousandths thick meet at its rim so nearly flat that a point 10 off the rim stands
    /// within a thousandth of every plane: of 300 sets of 40 points on plates 0.002 thick, every hull was open or wound
    /// against itself, 185 left a point out, by up to 24, and none does now; and 200 points in a plate 100 by 100 and 0.002
    /// thick ran the faster build out of memory, as 10 000 in a cube and 3 000 on a sphere did, and take from under a
    /// millisecond to an eighth of a second now. Merging the triangles within the tolerance can stand a face as far again
    /// from a point: up to 0.0011 off on those plates, a tenth of the tolerance past it.
    /// </para>
    /// <para>
    /// The work is quadratic in the worst case: 10 000 points in a cube take 45 milliseconds, 3 000 on a sphere 120,
    /// which is fine for the hull of a part; it is not meant for a laser scan.
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
        /// <returns>
        /// false when the points span no volume: fewer than four, or all within the point tolerance of a line or of the plane
        /// the hull starts from. A plate no thicker than twice the tolerance lies within it of its middle plane, and whether
        /// the start finds that depends on the points.
        /// </returns>
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
            var owners = new Dictionary<long, int>(PackedKeys.Instance);

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
            var next = new Dictionary<int, int>();
            int taken = 4, standing = 4;
            bool broken = false;
            double rounding = Rounding(p);
            var below = new bool[p.Count];

            for (int k = 0; k < p.Count; k++)
            {
                if (k == i0 || k == i1 || k == i2 || k == i3)
                {
                    continue;
                }

                visible.Clear();
                seen.Clear();
                double highest = double.NegativeInfinity;

                for (int f = 0; f < faces.Count; f++)
                {
                    if (!faces[f].Alive)
                    {
                        continue;
                    }

                    double height = Height(faces[f], p[k]);

                    if (height > highest)
                    {
                        highest = height;
                    }

                    if (height > eps)
                    {
                        visible.Add(f);
                        seen.Add(f);
                    }
                }

                if (visible.Count == 0)
                {
                    // Below every plane of the hull so far, so within what its corners span; see HoldsEveryPoint.
                    below[k] = highest <= rounding;
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

                // Read in floating point, the faces a point sees can make a rim that is not one loop, and the faces put on
                // it then no longer close the hull; built on, such a hull multiplied its faces with every point until it ran
                // out of memory. It is built exactly instead; see the remarks on the class.
                if (!IsOneLoop(horizon, next))
                {
                    broken = true;
                    break;
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

                // A closed surface of triangles the shape of a sphere has two less than twice as many triangles as corners.
                taken++;
                standing += horizon.Count - visible.Count;

                if (standing > 2 * taken - 4)
                {
                    broken = true;
                    break;
                }
            }

            // Built with a test of sides that rounds, the hull is built again exactly where it does not close, or leaves a
            // point further than the tolerance outside it; see the remarks on the class.
            if (broken || !HoldsEveryPoint(faces, owners, p, below, eps, rounding))
            {
                if (!TryBuildExactly(p, eps, out List<Face> exact))
                {
                    return false;
                }

                faces = exact ?? faces;
            }

            // Merged no more loosely than the points were taken on: a point left out lies within the point tolerance
            // of the triangles, and triangles merged within a wider planar one could lay the face that far past it,
            // so that the hull no longer held a point it was built from.
            hull = Merge3.CoplanarFaces(new GeoSolid3(Triangles(faces, p)), Boolean3.ForWork(tolerance));
            return true;
        }

        /// <summary>
        /// Whether the edges of a horizon, each run the way round its face had it, make one closed loop.
        /// </summary>
        /// <param name="horizon">The edges.</param>
        /// <param name="next">A map to work in, cleared first.</param>
        private static bool IsOneLoop(List<(int, int)> horizon, Dictionary<int, int> next)
        {
            next.Clear();

            foreach ((int from, int to) in horizon)
            {
                if (next.ContainsKey(from))
                {
                    return false;
                }

                next[from] = to;
            }

            int start = horizon[0].Item1, at = start, steps = 0;

            do
            {
                if (!next.TryGetValue(at, out at) || ++steps > horizon.Count)
                {
                    return false;
                }
            }
            while (at != start);

            return steps == horizon.Count;
        }

        /// <summary>
        /// The faces of a hull still standing, each a triangle with an area.
        /// </summary>
        private static List<GeoFace3> Triangles(List<Face> faces, List<GeoPoint3> p)
        {
            var triangles = new List<GeoFace3>();

            foreach (Face face in faces)
            {
                if (!face.Alive)
                {
                    continue;
                }

                // Built as they are, from their corners and the normal they were found with. A polygon reads its area
                // against the vector tolerance, and refused a face a tenth of a millimetre across, which the corners of a
                // body a boolean left made on its hull: the hull threw, and the box fitted round the body with it.
                GeoVector3 twice = p[face.A].GetVectorTo(p[face.B]).CrossProduct(p[face.A].GetVectorTo(p[face.C]));

                if (twice.Length > 0.0)
                {
                    triangles.Add(new GeoFace3(GeoPolygon3.FromValidated(new[] { p[face.A], p[face.B], p[face.C] }, face.Normal, 0.5 * twice.Length)));
                }
            }

            return triangles;
        }

        /// <summary>
        /// As much as rounding can put a point above the plane of a face it lies in: a trillionth of the size of the
        /// coordinates.
        /// </summary>
        private static double Rounding(List<GeoPoint3> p)
        {
            double reach = 0.0;

            foreach (GeoPoint3 point in p)
            {
                reach = Math.Max(reach, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));
            }

            return 1E-12 * (1.0 + reach);
        }

        /// <summary>
        /// Whether the triangles of a hull close and hold every point within the tolerance: each edge run the other way by
        /// exactly one other triangle, each triangle with an area, the surface one sphere, and each point that is not a corner
        /// inside it or no further than the tolerance from it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A triangle is put in only for a point further than the tolerance off the plane of a face it sees, so the faces
        /// beside the ones it replaces can be left standing a little above its own, and the hull folds in there; and where it
        /// reads which faces a point sees wrong in the last bit, it can come out folded over itself, closed edge for edge. A
        /// fold of a thousandth in the rim of a plate 0.005 thick left a point below every plane 0.0042 outside, and the rim
        /// of one 0.01 thick, convex at every edge, wound twice round a point 0.0016 inside it, which the hull then held
        /// outside.
        /// </para>
        /// <para>
        /// A hull with no corner above the plane of any triangle by more than rounding is convex: it holds what stands
        /// below all its planes, and is nearest a point outside on a triangle the point stands above. A point left out
        /// below every plane of the hull it was asked of is within what that hull's corners span, and so within this
        /// one, and is not asked again. A hull that folds by more, as 4 in 30 hulls of 200 points on a sphere and two
        /// in three of points on a lattice with copies do while holding every point, is taken only where no triangle is
        /// laid back over its neighbour and each point is held: one within the tolerance of a plane by being within it
        /// of a triangle, and one further inside all of them by the surface winding round it once, counted by the solid
        /// angles its triangles fill seen from the point.
        /// </para>
        /// </remarks>
        /// <param name="faces">The triangles, those still standing marked alive.</param>
        /// <param name="owners">The triangle each edge, run its way, belongs to.</param>
        /// <param name="p">The points.</param>
        /// <param name="below">For each point left out, whether it stood below every plane of the hull it was asked of.</param>
        /// <param name="eps">The point tolerance.</param>
        /// <param name="rounding">As much as rounding can put a point above a plane it lies in.</param>
        private static bool HoldsEveryPoint(List<Face> faces, Dictionary<long, int> owners, List<GeoPoint3> p, bool[] below, double eps, double rounding)
        {
            var alive = new List<int>();
            var isCorner = new bool[p.Count];
            int corners = 0;

            for (int f = 0; f < faces.Count; f++)
            {
                if (!faces[f].Alive)
                {
                    continue;
                }

                Face face = faces[f];

                if (!(p[face.A].GetVectorTo(p[face.B]).CrossProduct(p[face.A].GetVectorTo(p[face.C])).Length > 0.0))
                {
                    return false;
                }

                foreach ((int a, int b) in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
                {
                    if (!owners.TryGetValue(Key(a, b), out int own) || own != f
                        || !owners.TryGetValue(Key(b, a), out int twin) || !faces[twin].Alive)
                    {
                        return false;
                    }

                    if (!isCorner[a])
                    {
                        isCorner[a] = true;
                        corners++;
                    }
                }

                alive.Add(f);
            }

            // Corners less edges plus faces: two for one surface the shape of a sphere, each edge counted by its two faces.
            if (corners - 3 * alive.Count / 2 + alive.Count != 2)
            {
                return false;
            }

            double[] planes = Planes(faces, alive);
            bool convex = true;

            for (int k = 0; k < p.Count && convex; k++)
            {
                convex = !isCorner[k] || !IsAbove(planes, p[k], rounding);
            }

            if (!convex && FoldsBack(faces, alive, owners, p, rounding))
            {
                return false;
            }

            for (int k = 0; k < p.Count; k++)
            {
                if (isCorner[k] || convex && below[k])
                {
                    continue;
                }

                if (convex)
                {
                    if (IsAbove(planes, p[k], rounding) && !IsNear(faces, alive, p, p[k], eps, rounding))
                    {
                        return false;
                    }
                }
                else if (IsAbove(planes, p[k], -eps) ? !IsNear(faces, alive, p, p[k], eps, double.NegativeInfinity) : !(Math.Abs(Winding(faces, alive, p, p[k]) - 1.0) < 0.5))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a closed surface of triangles folds back over itself somewhere: a triangle whose neighbour across an edge
        /// turns back to the same side of that edge, laid over it, and no lower than its plane.
        /// </summary>
        /// <remarks>
        /// A convex surface goes on past each edge, or at a knife edge, the rim of a plate, turns back below it. One laid back
        /// over a triangle covers twice what lies under the two, and winds round it twice: the hull of 40 points on a plate a
        /// thousandth thick so folded held them all within the tolerance of its triangles, and 9.57 cubic millimetres for
        /// 6.72.
        /// </remarks>
        private static bool FoldsBack(List<Face> faces, List<int> alive, Dictionary<long, int> owners, List<GeoPoint3> p, double rounding)
        {
            foreach (int f in alive)
            {
                Face face = faces[f];

                foreach ((int a, int b, int c) in new[] { (face.A, face.B, face.C), (face.B, face.C, face.A), (face.C, face.A, face.B) })
                {
                    Face twin = faces[owners[Key(b, a)]];
                    int beyond = twin.A != a && twin.A != b ? twin.A : twin.B != a && twin.B != b ? twin.B : twin.C;
                    GeoVector3 along = p[a].GetVectorTo(p[b]);
                    double own = face.Normal.DotProduct(along.CrossProduct(p[a].GetVectorTo(p[c])));
                    double other = face.Normal.DotProduct(along.CrossProduct(p[a].GetVectorTo(p[beyond])));

                    if (own > 0.0 && other > 0.0 && Height(face, p[beyond]) >= -rounding)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// How many times the triangles of a closed surface wind round a point: the solid angles they fill seen from it,
        /// over the whole sphere's, positive where they face away from it.
        /// </summary>
        private static double Winding(List<Face> faces, List<int> alive, List<GeoPoint3> p, GeoPoint3 point)
        {
            double sum = 0.0;

            foreach (int f in alive)
            {
                Face face = faces[f];
                double ax = p[face.A].X - point.X, ay = p[face.A].Y - point.Y, az = p[face.A].Z - point.Z;
                double bx = p[face.B].X - point.X, by = p[face.B].Y - point.Y, bz = p[face.B].Z - point.Z;
                double cx = p[face.C].X - point.X, cy = p[face.C].Y - point.Y, cz = p[face.C].Z - point.Z;
                double la = Math.Sqrt(ax * ax + ay * ay + az * az);
                double lb = Math.Sqrt(bx * bx + by * by + bz * bz);
                double lc = Math.Sqrt(cx * cx + cy * cy + cz * cz);
                double triple = ax * (by * cz - bz * cy) + ay * (bz * cx - bx * cz) + az * (bx * cy - by * cx);
                double below = la * lb * lc + (ax * bx + ay * by + az * bz) * lc + (ax * cx + ay * cy + az * cz) * lb + (bx * cx + by * cy + bz * cz) * la;

                // Van Oosterom and Strackee: the solid angle of a triangle is twice this.
                sum += 2.0 * Math.Atan2(triple, below);
            }

            return sum / (4.0 * Math.PI);
        }

        /// <summary>
        /// The planes of some triangles, four numbers each: the unit normal and how far along it the plane lies.
        /// </summary>
        private static double[] Planes(List<Face> faces, List<int> alive)
        {
            var planes = new double[4 * alive.Count];

            for (int i = 0; i < alive.Count; i++)
            {
                Face face = faces[alive[i]];
                planes[4 * i] = face.Normal.X;
                planes[4 * i + 1] = face.Normal.Y;
                planes[4 * i + 2] = face.Normal.Z;
                planes[4 * i + 3] = face.Offset;
            }

            return planes;
        }

        /// <summary>
        /// Whether a point stands above any of some planes by more than a height, read as <see cref="Height"/> reads it.
        /// </summary>
        private static bool IsAbove(double[] planes, GeoPoint3 point, double height)
        {
            for (int i = 0; i < planes.Length; i += 4)
            {
                if (planes[i] * point.X + planes[i + 1] * point.Y + planes[i + 2] * point.Z - planes[i + 3] > height)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a point stands within a distance of one of some triangles whose planes it stands above by more than a
        /// height.
        /// </summary>
        /// <remarks>
        /// A point outside a convex body is nearest it on a face it stands outside of.
        /// </remarks>
        private static bool IsNear(List<Face> faces, List<int> alive, List<GeoPoint3> p, GeoPoint3 point, double distance, double height)
        {
            foreach (int f in alive)
            {
                Face face = faces[f];

                if (Height(face, point) > height
                    && Projection3.ProjectToTriangle(new GeoTriangle3(p[face.A], p[face.B], p[face.C]), point).DistanceTo(point) <= distance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds the hull with an exact test of which side of a face a point stands on, from the widest tetrahedron the
        /// points give, each further point taken unless it lies inside or within the point tolerance of the hull so far.
        /// </summary>
        /// <param name="p">The points.</param>
        /// <param name="eps">The point tolerance.</param>
        /// <param name="faces">
        /// The triangles, those still standing marked alive; null where a point has a coordinate that is not a number, or a
        /// rim is not one loop, which exact sides do not give.
        /// </param>
        /// <returns>false where the points all lie within the point tolerance of one plane, and so span no volume.</returns>
        /// <remarks>
        /// Read exactly, the faces a point outside the hull stands outside of are one patch whose rim is one loop, so the
        /// faces put in its place close the hull again, as they need not where a rounded test reads a face beside the patch
        /// as within it or not by the last bit. A point is left out only where it is within the tolerance of the triangles,
        /// measured to them and not to their planes: the faces of a plate a thousandth thick meet at its rim so nearly flat
        /// that a point 10 off the rim stands within a thousandth of every plane.
        /// </remarks>
        private static bool TryBuildExactly(List<GeoPoint3> p, double eps, out List<Face> faces)
        {
            faces = new List<Face>();

            foreach (GeoPoint3 point in p)
            {
                // A number that is not one has no side to be read on; the hull is left as the faster build made it.
                if (!IsFinite(point))
                {
                    faces = null;
                    return true;
                }
            }

            // The two of the six points furthest along an axis either way that lie furthest apart, the point furthest from
            // the line through them, and the one furthest from their plane; the first found of any that tie.
            var extremes = new int[6];

            for (int k = 1; k < p.Count; k++)
            {
                if (p[k].X < p[extremes[0]].X) extremes[0] = k;
                if (p[k].X > p[extremes[1]].X) extremes[1] = k;
                if (p[k].Y < p[extremes[2]].Y) extremes[2] = k;
                if (p[k].Y > p[extremes[3]].Y) extremes[3] = k;
                if (p[k].Z < p[extremes[4]].Z) extremes[4] = k;
                if (p[k].Z > p[extremes[5]].Z) extremes[5] = k;
            }

            int i0 = extremes[0], i1 = extremes[0];
            double widest = -1.0;

            for (int s = 0; s < 6; s++)
            {
                for (int t = s + 1; t < 6; t++)
                {
                    double apart = p[extremes[s]].GetDistanceSquaredTo(p[extremes[t]]);

                    if (apart > widest)
                    {
                        widest = apart;
                        i0 = extremes[s];
                        i1 = extremes[t];
                    }
                }
            }

            GeoPoint3 a = p[i0];
            GeoVector3 along = a.GetVectorTo(p[i1]);
            int i2 = Farthest(p, k => along.CrossProduct(a.GetVectorTo(p[k])).Length / Math.Max(along.Length, double.Epsilon));
            GeoVector3 normal = along.CrossProduct(a.GetVectorTo(p[i2]));
            int i3 = Farthest(p, k => Math.Abs(normal.DotProduct(a.GetVectorTo(p[k]))) / Math.Max(normal.Length, double.Epsilon));

            if (!(along.Length > eps)
                || !(along.CrossProduct(a.GetVectorTo(p[i2])).Length / along.Length > eps)
                || !(Math.Abs(normal.DotProduct(a.GetVectorTo(p[i3]))) / normal.Length > eps))
            {
                faces = null;
                return false;
            }

            var owners = new Dictionary<long, int>(PackedKeys.Instance);
            AddFacingAway(faces, owners, p, i0, i1, i2, i3);
            AddFacingAway(faces, owners, p, i0, i1, i3, i2);
            AddFacingAway(faces, owners, p, i0, i2, i3, i1);
            AddFacingAway(faces, owners, p, i1, i2, i3, i0);

            var visible = new List<int>();
            var seen = new HashSet<int>();
            var next = new Dictionary<int, int>();

            for (int k = 0; k < p.Count; k++)
            {
                if (k == i0 || k == i1 || k == i2 || k == i3)
                {
                    continue;
                }

                visible.Clear();
                seen.Clear();
                double nearest = double.PositiveInfinity;

                for (int f = 0; f < faces.Count; f++)
                {
                    Face face = faces[f];

                    if (face.Alive && Orientation(p[face.A], p[face.B], p[face.C], p[k]) > 0)
                    {
                        visible.Add(f);
                        seen.Add(f);
                        GeoPoint3 foot = Projection3.ProjectToTriangle(new GeoTriangle3(p[face.A], p[face.B], p[face.C]), p[k]);
                        nearest = Math.Min(nearest, foot.DistanceTo(p[k]));
                    }
                }

                // Inside, or outside the faces it sees by no more than the tolerance, which a convex body is nearest it on.
                if (visible.Count == 0 || nearest <= eps)
                {
                    continue;
                }

                var horizon = new List<(int, int)>();

                foreach (int f in visible)
                {
                    Face face = faces[f];

                    foreach ((int from, int to) in new[] { (face.A, face.B), (face.B, face.C), (face.C, face.A) })
                    {
                        if (!owners.TryGetValue(Key(to, from), out int twin) || !seen.Contains(twin))
                        {
                            horizon.Add((from, to));
                        }
                    }
                }

                // Read exactly, the rim is one loop; were it not, the faces put on it would not close the hull, and building
                // on would multiply them. The hull is left as the faster build made it.
                if (!IsOneLoop(horizon, next))
                {
                    faces = null;
                    return true;
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

                foreach ((int from, int to) in horizon)
                {
                    Add(faces, owners, p, from, to, k);
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a face on three points wound so that a fourth stands inside it, exactly.
        /// </summary>
        private static void AddFacingAway(List<Face> faces, Dictionary<long, int> owners, List<GeoPoint3> p, int a, int b, int c, int inside)
        {
            if (Orientation(p[a], p[b], p[c], p[inside]) > 0)
            {
                Add(faces, owners, p, a, c, b);
            }
            else
            {
                Add(faces, owners, p, a, b, c);
            }
        }

        private static bool IsFinite(GeoPoint3 point)
        {
            double sum = point.X + point.Y + point.Z;
            return !double.IsNaN(sum) && !double.IsInfinity(sum);
        }

        /// <summary>
        /// Which side of the plane through three points, wound counter-clockwise about its normal, a fourth stands on,
        /// exactly: 1 along the normal, -1 against it, 0 in the plane.
        /// </summary>
        /// <remarks>
        /// Worked out in floating point first, and exactly only where that could be wrong in sign: the rounding of the
        /// differences and the products is bounded by a small multiple of the sum of the products' sizes.
        /// </remarks>
        private static int Orientation(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoPoint3 d)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
            double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            double wx = d.X - a.X, wy = d.Y - a.Y, wz = d.Z - a.Z;

            double det = wx * (uy * vz - uz * vy) + wy * (uz * vx - ux * vz) + wz * (ux * vy - uy * vx);
            double size = Math.Abs(wx) * (Math.Abs(uy * vz) + Math.Abs(uz * vy))
                + Math.Abs(wy) * (Math.Abs(uz * vx) + Math.Abs(ux * vz))
                + Math.Abs(wz) * (Math.Abs(ux * vy) + Math.Abs(uy * vx));

            // Shewchuk's first bound for this determinant is 7.8E-16 of that sum.
            if (Math.Abs(det) > 1E-15 * size)
            {
                return det > 0.0 ? 1 : -1;
            }

            return ExactOrientation(a, b, c, d);
        }

        /// <summary>
        /// Which side of the plane through three points a fourth stands on, worked out in whole numbers: each coordinate is
        /// a whole number times a power of two, the least of the twelve taken out of all of them.
        /// </summary>
        private static int ExactOrientation(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoPoint3 d)
        {
            double[] values = { a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z, d.X, d.Y, d.Z };
            var mantissas = new long[12];
            var exponents = new int[12];
            int least = int.MaxValue;

            for (int i = 0; i < 12; i++)
            {
                long bits = BitConverter.DoubleToInt64Bits(values[i]);
                int exponent = (int)((bits >> 52) & 0x7FF);
                long mantissa = bits & 0xFFFFFFFFFFFFFL;

                if (exponent == 0)
                {
                    exponents[i] = -1074;
                }
                else
                {
                    mantissa |= 1L << 52;
                    exponents[i] = exponent - 1075;
                }

                mantissas[i] = bits < 0 ? -mantissa : mantissa;

                if (mantissa != 0)
                {
                    least = Math.Min(least, exponents[i]);
                }
            }

            if (least == int.MaxValue)
            {
                return 0;
            }

            var whole = new BigInteger[12];

            for (int i = 0; i < 12; i++)
            {
                whole[i] = mantissas[i] == 0 ? BigInteger.Zero : new BigInteger(mantissas[i]) << (exponents[i] - least);
            }

            BigInteger ux = whole[3] - whole[0], uy = whole[4] - whole[1], uz = whole[5] - whole[2];
            BigInteger vx = whole[6] - whole[0], vy = whole[7] - whole[1], vz = whole[8] - whole[2];
            BigInteger wx = whole[9] - whole[0], wy = whole[10] - whole[1], wz = whole[11] - whole[2];

            return (wx * (uy * vz - uz * vy) + wy * (uz * vx - ux * vz) + wz * (ux * vy - uy * vx)).Sign;
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
