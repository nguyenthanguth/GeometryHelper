using System;
using System.Collections.Generic;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Cuts the triangles of an item by a plane and joins the cuts into lines: the outline a clip plane leaves on a body or a mesh, so that a section
    /// of a solid can be read. It is computed in double precision from the triangles that are drawn.
    /// </summary>
    /// <remarks>
    /// Where two triangles share an edge the plane crosses, both find the same point to the bit, since the point is taken along the edge from its
    /// lower end whichever triangle asks. Triangles that lie in the plane add nothing, and an edge that lies in the plane is taken once.
    /// A box 30 by 20 by 10 cut by the plane at half its height gives one closed line of four corners, 100 long.
    /// </remarks>
    public static class SectionBuilder
    {
        /// <summary>
        /// Gets the lines in which a plane cuts what an item is drawn with.
        /// </summary>
        /// <param name="item">The item; only its triangles are cut.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="weld">The distance, in millimetres, within which a vertex is taken to lie in the plane, two ends of a cut are one point, and a middle point on a straight run is dropped: the slack of the clip set that cut the scene, so that what the section calls on the plane is what the drawing and the picking call on it; it is raised to a trillionth of the largest coordinate where the coordinates would swallow it.</param>
        /// <param name="cancel">Stops the work, between thousands of triangles.</param>
        /// <returns>
        /// The chains, closed where they join up, in an order that follows the triangles; none where the plane misses the item. Points on a straight run
        /// are left out, so the section of a box is its four corners and not the points where the triangles of its sides meet the plane.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the item or the plane is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the work is cancelled.</exception>
        public static List<DrawPolyline> Chains(ItemPrimitives item, ClipPlane plane, double weld, CancellationToken cancel = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (plane == null)
            {
                throw new ArgumentNullException(nameof(plane));
            }

            var chains = new List<DrawPolyline>();

            if (item.Triangles.Count == 0)
            {
                return chains;
            }

            var cuts = new List<Cut>();
            double biggest = 0.0;

            foreach (DrawTriangle t in item.Triangles)
            {
                biggest = Math.Max(biggest, Math.Max(Math.Max(Math.Abs(t.A.X), Math.Abs(t.A.Y)), Math.Abs(t.A.Z)));
            }

            weld = Math.Max(weld, 1E-12 * Math.Max(biggest, Math.Abs(plane.Point.X) + Math.Abs(plane.Point.Y) + Math.Abs(plane.Point.Z)));

            if (!(weld > 0.0))
            {
                weld = 1E-12;
            }

            int count = 0;

            foreach (DrawTriangle t in item.Triangles)
            {
                if ((++count & 1023) == 0)
                {
                    cancel.ThrowIfCancellationRequested();
                }

                CutTriangle(t, plane, weld, cuts);
            }

            if (cuts.Count == 0)
            {
                return chains;
            }

            Join(cuts, weld, chains);
            return chains;
        }

        /// <summary>
        /// Gets the total length of some lines, a closed line counting the piece that closes it.
        /// </summary>
        /// <param name="lines">The lines.</param>
        /// <exception cref="ArgumentNullException">Thrown when the lines are null.</exception>
        public static double Length(IEnumerable<DrawPolyline> lines)
        {
            if (lines == null)
            {
                throw new ArgumentNullException(nameof(lines));
            }

            double total = 0.0;

            foreach (DrawPolyline line in lines)
            {
                int n = line.Points.Length;

                for (int i = 0; i + 1 < n; i++)
                {
                    total += (line.Points[i + 1] - line.Points[i]).Length;
                }

                if (line.Closed && n > 1)
                {
                    total += (line.Points[0] - line.Points[n - 1]).Length;
                }
            }

            return total;
        }

        private struct Cut
        {
            internal Vec3 A;
            internal Vec3 B;
        }

        private static int Side(double distance, double weld) => distance > weld ? 1 : distance < -weld ? -1 : 0;

        private static void CutTriangle(DrawTriangle t, ClipPlane plane, double weld, List<Cut> cuts)
        {
            double d0 = plane.SignedDistance(t.A);
            double d1 = plane.SignedDistance(t.B);
            double d2 = plane.SignedDistance(t.C);
            int s0 = Side(d0, weld);
            int s1 = Side(d1, weld);
            int s2 = Side(d2, weld);
            int positive = (s0 > 0 ? 1 : 0) + (s1 > 0 ? 1 : 0) + (s2 > 0 ? 1 : 0);
            int negative = (s0 < 0 ? 1 : 0) + (s1 < 0 ? 1 : 0) + (s2 < 0 ? 1 : 0);
            int zero = 3 - positive - negative;

            if (positive == 0 && negative == 0)
            {
                return;
            }

            Vec3 V(int i) => i == 0 ? t.A : i == 1 ? t.B : t.C;
            double D(int i) => i == 0 ? d0 : i == 1 ? d1 : d2;
            int S(int i) => i == 0 ? s0 : i == 1 ? s1 : s2;

            if (zero == 2)
            {
                // The third corner is off the plane and the other two on it: an edge in the plane, which the triangle beyond it finds too.
                int off = S(0) != 0 ? 0 : S(1) != 0 ? 1 : 2;
                cuts.Add(new Cut { A = V((off + 1) % 3), B = V((off + 2) % 3) });
                return;
            }

            if (zero == 1)
            {
                if (positive == 1 && negative == 1)
                {
                    int on = S(0) == 0 ? 0 : S(1) == 0 ? 1 : 2;
                    int p = (on + 1) % 3;
                    int q = (on + 2) % 3;
                    cuts.Add(new Cut { A = V(on), B = CrossingPoint(V(p), D(p), V(q), D(q)) });
                }

                return;
            }

            if (positive == 0 || negative == 0)
            {
                return;
            }

            // One corner is alone on its side: the cut runs between the points where its two edges meet the plane.
            int lone = s0 == s1 ? 2 : s0 == s2 ? 1 : 0;
            int a = (lone + 1) % 3;
            int b = (lone + 2) % 3;
            cuts.Add(new Cut { A = CrossingPoint(V(lone), D(lone), V(a), D(a)), B = CrossingPoint(V(lone), D(lone), V(b), D(b)) });
        }

        private static Vec3 CrossingPoint(Vec3 u, double du, Vec3 v, double dv)
        {
            // Along the edge from its lower end, so that the two triangles on an edge get the same point to the bit.
            if (Before(v, u))
            {
                Vec3 t = u;
                u = v;
                v = t;
                double td = du;
                du = dv;
                dv = td;
            }

            return u + (v - u) * (du / (du - dv));
        }

        private static bool Before(Vec3 a, Vec3 b)
            => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;

        /// <summary>
        /// Joins the cuts into chains: ends within the weld are one point, a cut twice is one, and a chain is followed from an end, or from a
        /// point where three or more meet, before the closed ones are.
        /// </summary>
        private static void Join(List<Cut> cuts, double weld, List<DrawPolyline> chains)
        {
            var points = new List<Vec3>();
            var cells = new Dictionary<(long, long, long), List<int>>();

            int Id(Vec3 p)
            {
                long cx = (long)Math.Floor(p.X / weld);
                long cy = (long)Math.Floor(p.Y / weld);
                long cz = (long)Math.Floor(p.Z / weld);
                int found = -1;

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            if (cells.TryGetValue((cx + dx, cy + dy, cz + dz), out List<int> near))
                            {
                                foreach (int i in near)
                                {
                                    if ((points[i] - p).Length <= weld && (found < 0 || i < found))
                                    {
                                        found = i;
                                    }
                                }
                            }
                        }
                    }
                }

                if (found >= 0)
                {
                    return found;
                }

                points.Add(p);

                if (!cells.TryGetValue((cx, cy, cz), out List<int> own))
                {
                    own = new List<int>();
                    cells[(cx, cy, cz)] = own;
                }

                own.Add(points.Count - 1);
                return points.Count - 1;
            }

            var edges = new List<(int, int)>();
            var seen = new HashSet<(int, int)>();

            foreach (Cut cut in cuts)
            {
                int a = Id(cut.A);
                int b = Id(cut.B);

                if (a != b && seen.Add(a < b ? (a, b) : (b, a)))
                {
                    edges.Add((a, b));
                }
            }

            var neighbours = new List<int>[points.Count];

            for (int i = 0; i < neighbours.Length; i++)
            {
                neighbours[i] = new List<int>();
            }

            foreach ((int a, int b) in edges)
            {
                neighbours[a].Add(b);
                neighbours[b].Add(a);
            }

            var used = new HashSet<(int, int)>();

            bool Take(int a, int b) => used.Add(a < b ? (a, b) : (b, a));

            bool Free(int a, int b) => !used.Contains(a < b ? (a, b) : (b, a));

            for (int start = 0; start < points.Count; start++)
            {
                if (neighbours[start].Count == 2)
                {
                    continue;
                }

                foreach (int first in neighbours[start])
                {
                    if (!Take(start, first))
                    {
                        continue;
                    }

                    var path = new List<int> { start, first };
                    int previous = start;
                    int current = first;

                    while (neighbours[current].Count == 2)
                    {
                        int next = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];

                        if (!Take(current, next))
                        {
                            break;
                        }

                        path.Add(next);
                        previous = current;
                        current = next;
                    }

                    Emit(path, false);
                }
            }

            for (int start = 0; start < points.Count; start++)
            {
                foreach (int first in neighbours[start])
                {
                    if (!Free(start, first))
                    {
                        continue;
                    }

                    Take(start, first);
                    var path = new List<int> { start, first };
                    int previous = start;
                    int current = first;

                    while (current != start)
                    {
                        int next = neighbours[current][0] == previous ? neighbours[current][1] : neighbours[current][0];
                        Take(current, next);
                        path.Add(next);
                        previous = current;
                        current = next;
                    }

                    path.RemoveAt(path.Count - 1);
                    Emit(path, true);
                }
            }

            void Emit(List<int> path, bool closed)
            {
                var line = new List<Vec3>(path.Count);

                foreach (int i in path)
                {
                    line.Add(points[i]);
                }

                List<Vec3> simple = Straighten(line, closed, weld);

                if (simple.Count >= 2 && !(closed && simple.Count < 3))
                {
                    chains.Add(new DrawPolyline(simple.ToArray(), closed));
                }
            }
        }

        /// <summary>
        /// Leaves out the points that lie on the straight run between their neighbours, to within the weld.
        /// </summary>
        private static List<Vec3> Straighten(List<Vec3> points, bool closed, double weld)
        {
            int n = points.Count;

            if (n < 3)
            {
                return points;
            }

            int first = 0;

            if (closed)
            {
                first = -1;

                for (int i = 0; i < n; i++)
                {
                    if (!OnRun(points[(i + n - 1) % n], points[i], points[(i + 1) % n], weld))
                    {
                        first = i;
                        break;
                    }
                }

                if (first < 0)
                {
                    return points;
                }
            }

            var kept = new List<Vec3> { points[first] };
            int count = closed ? n : n - 1;

            for (int k = 1; k < count; k++)
            {
                Vec3 here = points[(first + k) % n];
                Vec3 next = points[(first + k + 1) % n];

                if (!OnRun(kept[kept.Count - 1], here, next, weld))
                {
                    kept.Add(here);
                }
            }

            if (!closed)
            {
                kept.Add(points[n - 1]);
            }

            return kept;
        }

        private static bool OnRun(Vec3 a, Vec3 b, Vec3 c, double weld)
        {
            Vec3 ac = c - a;
            double length = ac.Length;

            if (!(length > 0.0))
            {
                return false;
            }

            return Vec3.Cross(ac, b - a).Length / length <= weld && Vec3.Dot(b - a, c - b) > 0.0;
        }
    }
}
