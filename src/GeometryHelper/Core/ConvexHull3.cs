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
    /// from a point: 5 of 1 000 plates 100 by 100 and from 0.002 to 1 thick left a point up to 1.08E-3 outside the merged
    /// hull. Where a point stands further than the tolerance outside it, the triangles are merged again within a tenth of
    /// the planar tolerance, then a hundredth, and kept as they are where that does not hold it either, so every point is
    /// within the tolerance of the hull given; a merged hull that holds every point is kept as it was, to the bit.
    /// </para>
    /// <para>
    /// A point is read only against the faces that can stand below it, found by the way they face, so the work no longer
    /// grows with the square of the points where they all lie on the hull: 10 000 points in a cube take 50 milliseconds as
    /// they did, and 12 000 on a sphere 0.18 seconds where they took 2.4. It is still not meant for a laser scan.
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
            var depth = new double[p.Count];
            var byNormal = new FacesByNormal(p.Count, inside, rounding, false);
            var candidates = new List<int>();

            // Below every plane by more than this, a point is below by more than rounding and by more than three tolerances,
            // which is all that is asked of how high it stands where it sees no face.
            double lowest = Math.Min(-3.0 * eps, Math.Min(eps, rounding));

            for (int k = 0; k < p.Count; k++)
            {
                if (k == i0 || k == i1 || k == i2 || k == i3)
                {
                    continue;
                }

                visible.Clear();
                seen.Clear();
                double highest = double.NegativeInfinity;
                bool every = !byNormal.TryGather(faces, p, p[k], lowest, candidates);

                for (int i = 0, count = every ? faces.Count : candidates.Count; i < count; i++)
                {
                    int f = every ? i : candidates[i];

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

                // Oldest first, as every face was read.
                if (!every)
                {
                    visible.Sort();
                }

                if (visible.Count == 0)
                {
                    // Below every plane of the hull so far, so within what its corners span; see HoldsEveryPoint. The faces not
                    // read stand lower than the lowest asked, so the highest read tells as much as the highest of all.
                    below[k] = highest <= rounding;
                    depth[k] = highest;
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
                depth = null;
            }

            // Merged no more loosely than the points were taken on: a point left out lies within the point tolerance
            // of the triangles, and triangles merged within a wider planar one could lay the face that far past it,
            // so that the hull no longer held a point it was built from.
            var triangles = new GeoSolid3(Triangles(faces, p));
            hull = Merge3.CoplanarFaces(triangles, Boolean3.ForWork(tolerance));

            // Merged within the tolerance, a face can still stand past a point by up to the tolerance again: merged within a tenth
            // and a hundredth of it where it does, and left as its triangles, which hold every point, where that does not do.
            for (int step = 0; step < 3 && !HoldsWithin(hull, p, depth, eps); step++)
            {
                double planar = Boolean3.ForWork(tolerance).EqualPlanar * (step == 0 ? 0.1 : 0.01);
                hull = step < 2 ? Merge3.CoplanarFaces(triangles, new Tolerance(eps, tolerance.EqualVector, tolerance.EqualAngleRad, planar)) : triangles;
            }

            return true;
        }

        /// <summary>
        /// Whether every point stands within the point tolerance of a hull: a corner of a face of it is on it, one left out of the build
        /// further than three tolerances below every plane of the hull it was asked of is inside it (the hull only grew, and merging moves
        /// a face no further than the tolerance), one below the plane of every face is in it, and any other is measured to the faces it
        /// stands above or within the tolerance below, a point outside a convex body being nearest it on a face it stands above.
        /// </summary>
        /// <param name="hull">The hull, its faces merged.</param>
        /// <param name="p">The points.</param>
        /// <param name="depth">For each point left out by the build kept, how far above every plane of the hull it was asked of it stood, nought for the others; null where the hull was built again.</param>
        /// <param name="eps">The point tolerance.</param>
        private static bool HoldsWithin(GeoSolid3 hull, List<GeoPoint3> p, double[] depth, double eps)
        {
            IReadOnlyList<GeoFace3> faces = hull.Faces;
            var planes = new double[6 * faces.Count];
            var regions = new Gap3.Region[faces.Count];
            var corners = new HashSet<GeoPoint3>();

            for (int i = 0; i < faces.Count; i++)
            {
                GeoPolygon3 boundary = faces[i].Boundary;
                GeoVector3 normal = boundary.Normal;
                GeoPoint3 on = boundary.Vertices[0];
                planes[6 * i] = normal.X;
                planes[6 * i + 1] = normal.Y;
                planes[6 * i + 2] = normal.Z;
                planes[6 * i + 3] = on.X;
                planes[6 * i + 4] = on.Y;
                planes[6 * i + 5] = on.Z;

                foreach (GeoPoint3 corner in boundary.Vertices)
                {
                    corners.Add(corner);
                }
            }

            for (int k = 0; k < p.Count; k++)
            {
                GeoPoint3 point = p[k];

                if ((depth != null && depth[k] < -3.0 * eps) || corners.Contains(point))
                {
                    continue;
                }

                bool above = false;

                for (int i = 0; i < faces.Count && !above; i++)
                {
                    above = HeightOver(planes, i, point) > 0.0;
                }

                if (!above)
                {
                    continue;
                }

                double nearest = double.PositiveInfinity;

                for (int i = 0; i < faces.Count && nearest > eps; i++)
                {
                    if (HeightOver(planes, i, point) > -eps)
                    {
                        Gap3.Region region = regions[i] ?? (regions[i] = Gap3.Region.Of(faces[i]));
                        nearest = Math.Min(nearest, region.PointGap(point));
                    }
                }

                if (nearest > eps)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// How far a point stands above the plane of a face, given by its unit normal and a corner, six numbers a face.
        /// </summary>
        private static double HeightOver(double[] planes, int face, GeoPoint3 point)
        {
            int at = 6 * face;

            return planes[at] * (point.X - planes[at + 3]) + planes[at + 1] * (point.Y - planes[at + 4]) + planes[at + 2] * (point.Z - planes[at + 5]);
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
            var start = new GeoPoint3(
                (p[i0].X + p[i1].X + p[i2].X + p[i3].X) / 4,
                (p[i0].Y + p[i1].Y + p[i2].Y + p[i3].Y) / 4,
                (p[i0].Z + p[i1].Z + p[i2].Z + p[i3].Z) / 4);
            var byNormal = new FacesByNormal(p.Count, start, Rounding(p), true);
            var candidates = new List<int>();

            for (int k = 0; k < p.Count; k++)
            {
                if (k == i0 || k == i1 || k == i2 || k == i3)
                {
                    continue;
                }

                visible.Clear();
                seen.Clear();
                double nearest = double.PositiveInfinity;
                bool every = !byNormal.TryGather(faces, p, p[k], 0.0, candidates);

                for (int i = 0, count = every ? faces.Count : candidates.Count; i < count; i++)
                {
                    int f = every ? i : candidates[i];
                    Face face = faces[f];

                    if (face.Alive && Orientation(p[face.A], p[face.B], p[face.C], p[k]) > 0)
                    {
                        visible.Add(f);
                        seen.Add(f);
                        GeoPoint3 foot = Projection3.ProjectToTriangle(new GeoTriangle3(p[face.A], p[face.B], p[face.C]), p[k]);
                        nearest = Math.Min(nearest, foot.DistanceTo(p[k]));
                    }
                }

                // Oldest first, as every face was read.
                if (!every)
                {
                    visible.Sort();
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

        /// <summary>
        /// The faces of a hull being built, filed by the way they face, so that those a point may stand above are found
        /// without reading the others.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every point was read against every face built so far, standing or gone: 12 000 points on a sphere, each outside the
        /// hull of those before it, were read against tens of thousands of faces each, and took 2.4 seconds. The normals are
        /// filed on the faces of a cube, each cut into cells, and a cell's normals lie within a cone round its middle. How far
        /// a point can stand above the plane of a face is then bounded for the whole cell: no further than it reaches along
        /// the nearest normal in the cone, measured from a point inside the hull, less the least distance from there to a plane
        /// of the cell. Only the cells where a point can stand as high as asked, rounding allowed for, are read, each face as
        /// before, and the faces it sees put oldest first, as every face was read; so the same faces are found, and the hull
        /// is the same to the bit. Until the faces outnumber the cells sixteen times over every face is read, which is faster
        /// for so few.
        /// </para>
        /// <para>
        /// Where a point's side of a face is read exactly, the normal worked out in floating point is off the exact one by an
        /// angle its rounding bounds; the cone is widened by that angle, and the distance to the plane taken less by as much,
        /// and a face so thin that its normal is not known to half a radian is read every time.
        /// </para>
        /// </remarks>
        private sealed class FacesByNormal
        {
            /// <summary>
            /// Until the faces built outnumber the cells this many times over, reading every face is faster than the cells.
            /// </summary>
            private const int FacesACell = 16;

            private readonly int _side;
            private List<int>[] _cells;
            private double[] _axes;
            private double[] _angles;
            private double[] _lowest;
            private double[] _spread;
            private double[] _cos;
            private double[] _sin;
            private readonly List<double> _faceLowest = new List<double>();
            private readonly List<double> _faceSpread = new List<double>();
            private readonly GeoPoint3 _inside;
            private readonly double _margin;
            private readonly bool _exact;

            /// <param name="points">How many points the hull is built on, which sets how finely the normals are filed.</param>
            /// <param name="inside">A point inside the hull from its start.</param>
            /// <param name="rounding">As much as rounding can put a point above a plane it lies in.</param>
            /// <param name="exact">Whether a point's side of a face is read exactly, rather than from the face's normal.</param>
            internal FacesByNormal(int points, GeoPoint3 inside, double rounding, bool exact)
            {
                _side = Math.Max(1, Math.Min(12, (int)Math.Round(Math.Sqrt(Math.Sqrt(points) / 2.0))));
                _inside = inside;
                _margin = 10.0 * rounding;
                _exact = exact;
            }

            /// <summary>
            /// Gathers the faces standing that a point may stand above by a height or more, and perhaps a few more, in no order;
            /// false, and every face to be read, while they are few. The cells are laid out the first time they are read, and
            /// faces added since the last time are filed first.
            /// </summary>
            internal bool TryGather(List<Face> faces, List<GeoPoint3> p, GeoPoint3 point, double height, List<int> found)
            {
                found.Clear();

                if (_cells == null)
                {
                    if (faces.Count < FacesACell * (6 * _side * _side + 1))
                    {
                        return false;
                    }

                    Lay();
                }

                for (int f = _faceLowest.Count; f < faces.Count; f++)
                {
                    File(faces[f], p);
                }

                double wx = point.X - _inside.X, wy = point.Y - _inside.Y, wz = point.Z - _inside.Z;
                double length = Math.Sqrt(wx * wx + wy * wy + wz * wz);
                int last = _cells.Length - 1;

                for (int c = 0; c <= last; c++)
                {
                    if (_cells[c].Count == 0)
                    {
                        continue;
                    }

                    if (c < last && Reach(c, wx, wy, wz, length) - _lowest[c] + _margin < height)
                    {
                        continue;
                    }

                    Gather(faces, c, found);
                }

                return true;
            }

            /// <summary>
            /// Lays out the cells: on each face of the cube a square of them, each with the cone round its middle that holds it.
            /// </summary>
            private void Lay()
            {
                int cells = 6 * _side * _side;
                _cells = new List<int>[cells + 1];
                _axes = new double[3 * cells];
                _angles = new double[cells + 1];
                _lowest = new double[cells + 1];
                _spread = new double[cells + 1];
                _cos = new double[cells + 1];
                _sin = new double[cells + 1];

                for (int c = 0; c <= cells; c++)
                {
                    _cells[c] = new List<int>();
                    _lowest[c] = double.PositiveInfinity;
                }

                for (int face = 0; face < 6; face++)
                {
                    for (int i = 0; i < _side; i++)
                    {
                        for (int j = 0; j < _side; j++)
                        {
                            // The cone round the middle of the cell holds its four corners, and so the whole of it.
                            int c = (face * _side + i) * _side + j;
                            double s0 = -1.0 + 2.0 * i / _side, s1 = -1.0 + 2.0 * (i + 1) / _side;
                            double t0 = -1.0 + 2.0 * j / _side, t1 = -1.0 + 2.0 * (j + 1) / _side;
                            GeoVector3 axis = Direction(face, 0.5 * (s0 + s1), 0.5 * (t0 + t1));
                            double cos = 1.0;

                            foreach (GeoVector3 corner in new[] { Direction(face, s0, t0), Direction(face, s1, t0), Direction(face, s0, t1), Direction(face, s1, t1) })
                            {
                                cos = Math.Min(cos, axis.DotProduct(corner));
                            }

                            _axes[3 * c] = axis.X;
                            _axes[3 * c + 1] = axis.Y;
                            _axes[3 * c + 2] = axis.Z;
                            _angles[c] = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos))) + 1E-9;
                            Widen(c);
                        }
                    }
                }
            }

            /// <summary>
            /// How far along a normal within the cone of a cell a point reaches, measured from the point inside.
            /// </summary>
            private double Reach(int c, double wx, double wy, double wz, double length)
            {
                if (!(length > 0.0))
                {
                    return length == 0.0 ? 0.0 : double.NaN;
                }

                double along = Math.Max(-1.0, Math.Min(1.0, (_axes[3 * c] * wx + _axes[3 * c + 1] * wy + _axes[3 * c + 2] * wz) / length));

                if (along >= _cos[c])
                {
                    return length;
                }

                // The angle off the axis less the cone's: its cosine, from the two angles' sines and cosines.
                return length * (along * _cos[c] + Math.Sqrt(Math.Max(0.0, 1.0 - along * along)) * _sin[c]);
            }

            /// <summary>
            /// Adds the faces of a cell still standing to a list, and drops those that are not from the cell.
            /// </summary>
            private void Gather(List<Face> faces, int c, List<int> found)
            {
                List<int> cell = _cells[c];
                int kept = 0;

                for (int i = 0; i < cell.Count; i++)
                {
                    int f = cell[i];

                    if (faces[f].Alive)
                    {
                        cell[kept++] = f;
                        found.Add(f);
                    }
                }

                if (kept == cell.Count)
                {
                    return;
                }

                cell.RemoveRange(kept, cell.Count - kept);
                _lowest[c] = double.PositiveInfinity;
                _spread[c] = 0.0;

                foreach (int f in cell)
                {
                    _lowest[c] = Math.Min(_lowest[c], _faceLowest[f]);
                    _spread[c] = Math.Max(_spread[c], _faceSpread[f]);
                }

                Widen(c);
            }

            /// <summary>
            /// Files a face under the cell its normal falls in, with how far its plane lies from the point inside, less what
            /// the angle its normal may be off by can take off that.
            /// </summary>
            private void File(Face face, List<GeoPoint3> p)
            {
                GeoVector3 n = face.Normal;
                double spread = _exact ? Spread(p[face.A], p[face.B], p[face.C]) : 0.0;
                GeoPoint3 a = p[face.A];
                double lowest = face.Offset - n.DotProduct(new GeoVector3(_inside.X, _inside.Y, _inside.Z)) - spread * a.DistanceTo(_inside);
                int c = double.IsNaN(lowest) || double.IsInfinity(lowest) ? _cells.Length - 1 : CellOf(n, spread);

                _faceLowest.Add(lowest);
                _faceSpread.Add(spread);
                _cells[c].Add(_faceLowest.Count - 1);
                _lowest[c] = Math.Min(_lowest[c], lowest);

                if (spread > _spread[c])
                {
                    _spread[c] = spread;
                    Widen(c);
                }
            }

            /// <summary>
            /// The cell a normal falls in: on the face of the cube its largest coordinate points to, at the place it meets it;
            /// the last cell, read every time, where it has no direction or may be off by half a radian or more.
            /// </summary>
            private int CellOf(GeoVector3 n, double spread)
            {
                double ax = Math.Abs(n.X), ay = Math.Abs(n.Y), az = Math.Abs(n.Z);
                int face;
                double most, s, t;

                if (ax >= ay && ax >= az)
                {
                    most = ax;
                    face = n.X >= 0.0 ? 0 : 1;
                    s = n.Y / most;
                    t = n.Z / most;
                }
                else if (ay >= az)
                {
                    most = ay;
                    face = n.Y >= 0.0 ? 2 : 3;
                    s = n.Z / most;
                    t = n.X / most;
                }
                else
                {
                    most = az;
                    face = n.Z >= 0.0 ? 4 : 5;
                    s = n.X / most;
                    t = n.Y / most;
                }

                if (!(most > 0.0) || !(spread < 0.5) || double.IsNaN(s) || double.IsNaN(t))
                {
                    return _cells.Length - 1;
                }

                int i = Math.Max(0, Math.Min(_side - 1, (int)Math.Floor((s + 1.0) * 0.5 * _side)));
                int j = Math.Max(0, Math.Min(_side - 1, (int)Math.Floor((t + 1.0) * 0.5 * _side)));
                return (face * _side + i) * _side + j;
            }

            /// <summary>
            /// The direction through a place on a face of the cube, as <see cref="CellOf"/> reads it, of unit length.
            /// </summary>
            private static GeoVector3 Direction(int face, double s, double t)
            {
                double sign = face % 2 == 0 ? 1.0 : -1.0;
                GeoVector3 d = face < 2 ? new GeoVector3(sign, s, t) : face < 4 ? new GeoVector3(t, sign, s) : new GeoVector3(s, t, sign);
                return d.Multiply(1.0 / d.Length);
            }

            /// <summary>
            /// Sets the cosine and sine of the half angle of a cell's cone, widened by the angle its normals may be off by.
            /// </summary>
            private void Widen(int c)
            {
                double angle = Math.Min(Math.PI, _angles[c] + _spread[c]);
                _cos[c] = Math.Cos(angle);
                _sin[c] = Math.Sin(angle);
            }

            /// <summary>
            /// The angle the unit normal of a triangle, worked out in floating point as <see cref="Add"/> works it out, may be
            /// off the exact one by: the rounding of its cross product over its length, twice over, and a little for scaling it.
            /// </summary>
            private static double Spread(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c)
            {
                double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
                double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                double error = 4E-15 * (Math.Abs(ux) + Math.Abs(uy) + Math.Abs(uz)) * (Math.Abs(vx) + Math.Abs(vy) + Math.Abs(vz));

                return error < 0.25 * length ? 4.0 * error / length + 1E-14 : double.PositiveInfinity;
            }
        }
    }
}
