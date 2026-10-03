using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Makes the corners of a body's faces that stand within a tolerance of each other one point, and puts each corner on
    /// the edges of other faces it stands on, so the faces meet on the same edges exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A boolean cut within a tolerance closes within that tolerance, and no closer. Two faces of what it gives meeting on
    /// an edge each have it where their own planes put it, and where a plane of one body passes within the tolerance of a
    /// face of the other, those are a few hundredths apart: a slab cut within 0.05 by the planes of a part beside it, one of
    /// which passed 0.011 off the back of a notch in the slab metres from the part, had the pieces cut along that plane
    /// meet the notch's own faces on copies of their edges 0.0113 apart, closed within 0.05 and open within 0.01, the
    /// default. Of 10 314 cuts within 0.05 of the parts of a Tekla model by the parts they meet, 14 came out that way, every
    /// one of them by copies of edges 0.010 to 0.048 apart.
    /// </para>
    /// <para>
    /// The corners within the tolerance of each other are taken together as one, however they chain, and each group is
    /// moved to the corner of it that moves the volume least: a corner moved along the faces it is a corner of moves
    /// nothing, and one moved off them sweeps the triangle it makes with its neighbours on each. A slab's top ran out to a
    /// point 0.0095 off the corner of the wall beside it, the end of a sliver along the wall's top edge; moved to the top's
    /// point, as the corner with the most face round it, the wall's corner tilted the wall, 21.8 metres long, and the slab
    /// came out 41 925 cubic millimetres short, where the point moved to the wall's corner along the top moves nothing. A
    /// corner standing within the tolerance of another face's edge, between its ends, is put on that edge, as the corner
    /// where a long edge meets two short ones. What comes of it is kept only where it closes within the default tolerance
    /// and within its own, and holds the volume it held within the tolerance times its area: a corner moved is moved no
    /// further than the tolerance, and a face moved off flat by that much is flat within it.
    /// </para>
    /// <para>
    /// Only a body open within the default tolerance, or within its own where that is the finer, is welded; one closed
    /// within it is left as it is, closed as the boolean promised and as a caller asks. Welded wherever it did not close
    /// within an eighth of its tolerance, 36 of the cuts of those parts within the default came out other than they were,
    /// 17 of them further from the same cuts within a thousandth and 15 nearer, the worst 7 369 cubic millimetres further.
    /// A body open within its own tolerance is welded too, where that closes it: a slab's side bent by 3.3E-5 rad at one
    /// corner and its top at another 18.28 further along, the edges past the bend on two lines the tolerance could not take
    /// for one, and each corner put on the other's edge closed it.
    /// </para>
    /// </remarks>
    internal static class Weld3
    {
        /// <summary>
        /// Gets a body a boolean made, open within the default tolerance or within its own where that is the finer, with its
        /// corners within its tolerance of each other made one and put on the edges they stand on, where that closes it; the
        /// body as it is otherwise.
        /// </summary>
        internal static GeoSolid3 Sealed(GeoSolid3 solid, Tolerance work)
        {
            Tolerance check = work.EqualPoint < Tolerance.Global.EqualPoint ? work : Tolerance.Global;

            // What a boolean gives mostly closes, and is asked once.
            if (solid == null || solid.IsClosed(check))
            {
                return solid;
            }

            return TryWeld(solid, work, check, out GeoSolid3 welded) ? welded : solid;
        }

        /// <summary>
        /// Welds the corners of a body's faces within a tolerance; false where what comes of it does not close within it and
        /// within the finer one, or moves its volume further than the tolerance times its area.
        /// </summary>
        internal static bool TryWeld(GeoSolid3 solid, Tolerance work, Tolerance fine, out GeoSolid3 welded)
        {
            welded = null;
            IReadOnlyList<GeoFace3> faces = solid.Faces;
            double reach = work.EqualPoint;

            if (!(reach > 0.0))
            {
                return false;
            }

            // Every distinct point once, and the corner it makes on each ring it is a corner of.
            var index = new Dictionary<GeoPoint3, int>();
            var points = new List<GeoPoint3>();
            var corners = new List<List<GeoVector3>>();
            var rings = new List<List<int>>[faces.Count];

            for (int f = 0; f < faces.Count; f++)
            {
                GeoFace3 face = faces[f];
                rings[f] = new List<List<int>>(1 + face.Holes.Count) { Positions(face.Boundary, index, points, corners) };

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    rings[f].Add(Positions(hole, index, points, corners));
                }
            }

            int[] to = Representatives(points, corners, reach);

            // Each ring on the corners it is welded to, a corner repeated in a row once.
            bool anyMoved = false;

            foreach (List<List<int>> faceRings in rings)
            {
                for (int r = 0; r < faceRings.Count; r++)
                {
                    List<int> moved = Collapse(faceRings[r], to, out bool changed);
                    anyMoved |= changed;
                    faceRings[r] = moved;
                }
            }

            bool[] cornered = PutCornersOnEdges(rings, points, reach);

            var built = new List<GeoFace3>(faces.Count);
            Tolerance pieces = LoopAssembly.ForPieces(work);
            bool anyCornered = false;

            for (int f = 0; f < faces.Count; f++)
            {
                List<List<int>> faceRings = rings[f];
                anyCornered |= cornered[f];

                if (!cornered[f] && Unchanged(faces[f], faceRings, points))
                {
                    built.Add(faces[f]);
                    continue;
                }

                // A face whose corners came to fewer than three points covered nothing within the tolerance; the faces
                // beside it meet along its edges without it.
                if (faceRings[0].Count < 3)
                {
                    continue;
                }

                var holes = new List<IEnumerable<GeoPoint3>>();

                for (int r = 1; r < faceRings.Count; r++)
                {
                    if (faceRings[r].Count >= 3)
                    {
                        holes.Add(Corners(faceRings[r], points));
                    }
                }

                built.AddRange(Loops3.ToFaces(Corners(faceRings[0], points), holes, pieces));
            }

            if (!anyMoved && !anyCornered || built.Count < 4)
            {
                return false;
            }

            var candidate = new GeoSolid3(built, solid.Openings);

            if (!candidate.IsClosed(fine) || !candidate.IsClosed(work))
            {
                return false;
            }

            double before = solid.GetSignedVolume();
            double after = candidate.GetSignedVolume();

            if (Math.Abs(after - before) > reach * solid.GrossSurfaceArea + 1E-9 * Math.Abs(before))
            {
                return false;
            }

            welded = candidate;
            return true;
        }

        /// <summary>
        /// The positions of the corners of a ring, each distinct point given one the first time it is met, with the corner it
        /// makes on the ring added to each: half the cross of the edges to its neighbours, the area of the triangle it sweeps
        /// moved one unit off it.
        /// </summary>
        private static List<int> Positions(GeoPolygon3 ring, Dictionary<GeoPoint3, int> index, List<GeoPoint3> points, List<List<GeoVector3>> corners)
        {
            IReadOnlyList<GeoPoint3> vertices = ring.Vertices;
            int count = vertices.Count;
            var ids = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                GeoPoint3 corner = vertices[i];

                if (!index.TryGetValue(corner, out int id))
                {
                    id = points.Count;
                    index.Add(corner, id);
                    points.Add(corner);
                    corners.Add(new List<GeoVector3>(3));
                }

                GeoVector3 back = corner.GetVectorTo(vertices[(i + count - 1) % count]);
                GeoVector3 ahead = corner.GetVectorTo(vertices[(i + 1) % count]);
                corners[id].Add(back.CrossProduct(ahead).Multiply(0.5));
                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// The position each point is welded to: the points within the tolerance of each other, however they chain, are
        /// one group, and each goes to the point of it that takes the most of the group within the tolerance of it and of
        /// those moves the volume least, the first met of those; the rest of the group is settled the same way among
        /// themselves.
        /// </summary>
        private static int[] Representatives(List<GeoPoint3> points, List<List<GeoVector3>> corners, double reach)
        {
            int count = points.Count;
            int[] parent = new int[count];

            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
            }

            // A grid of cells the tolerance wide: points within the tolerance of each other are in the same cell or two
            // touching.
            var cells = new Dictionary<(long, long, long), List<int>>();
            var cellOf = new (long X, long Y, long Z)[count];

            for (int i = 0; i < count; i++)
            {
                GeoPoint3 p = points[i];
                var cell = (Cell(p.X, reach), Cell(p.Y, reach), Cell(p.Z, reach));
                cellOf[i] = cell;

                if (!cells.TryGetValue(cell, out List<int> list))
                {
                    list = new List<int>();
                    cells.Add(cell, list);
                }

                list.Add(i);
            }

            double reachSquared = reach * reach;

            for (int i = 0; i < count; i++)
            {
                (long x, long y, long z) = cellOf[i];

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            if (!cells.TryGetValue((x + dx, y + dy, z + dz), out List<int> near))
                            {
                                continue;
                            }

                            foreach (int j in near)
                            {
                                if (j > i && DistanceSquared(points[i], points[j]) <= reachSquared)
                                {
                                    Union(parent, i, j);
                                }
                            }
                        }
                    }
                }
            }

            // The groups, each in the order its points were met.
            var groups = new Dictionary<int, List<int>>();

            for (int i = 0; i < count; i++)
            {
                int root = Find(parent, i);

                if (!groups.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    groups.Add(root, members);
                }

                members.Add(i);
            }

            int[] to = new int[count];

            foreach (List<int> members in groups.Values)
            {
                List<int> left = members;

                while (left.Count > 0)
                {
                    int chosen = left[0];

                    if (left.Count > 1)
                    {
                        int mostTaken = -1;
                        double leastMoved = double.MaxValue;

                        foreach (int candidate in left)
                        {
                            Weigh(candidate, left, points, corners, reachSquared, out int taken, out double moved);

                            if (taken > mostTaken || taken == mostTaken && moved < leastMoved)
                            {
                                chosen = candidate;
                                mostTaken = taken;
                                leastMoved = moved;
                            }
                        }
                    }

                    var rest = new List<int>();

                    foreach (int m in left)
                    {
                        if (DistanceSquared(points[m], points[chosen]) <= reachSquared)
                        {
                            to[m] = chosen;
                        }
                        else
                        {
                            rest.Add(m);
                        }
                    }

                    left = rest;
                }
            }

            return to;
        }

        /// <summary>
        /// How many points of a group stand within the tolerance of one of them, and how far moving those to it moves the
        /// volume, to the first order: each corner a point makes, its triangle's area times how far it moves off it.
        /// </summary>
        private static void Weigh(int candidate, List<int> group, List<GeoPoint3> points, List<List<GeoVector3>> corners, double reachSquared, out int taken, out double moved)
        {
            taken = 0;
            moved = 0.0;
            GeoPoint3 target = points[candidate];

            foreach (int m in group)
            {
                if (DistanceSquared(points[m], target) > reachSquared)
                {
                    continue;
                }

                taken++;

                if (m == candidate)
                {
                    continue;
                }

                GeoVector3 shift = points[m].GetVectorTo(target);

                foreach (GeoVector3 corner in corners[m])
                {
                    moved += Math.Abs(shift.DotProduct(corner));
                }
            }
        }

        /// <summary>
        /// A ring on the positions its corners are welded to, a position repeated in a row kept once, the last as the first
        /// too.
        /// </summary>
        private static List<int> Collapse(List<int> ring, int[] to, out bool changed)
        {
            var kept = new List<int>(ring.Count);
            changed = false;

            foreach (int id in ring)
            {
                int welded = to[id];
                changed |= welded != id;

                if (kept.Count == 0 || kept[kept.Count - 1] != welded)
                {
                    kept.Add(welded);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1] == kept[0])
            {
                kept.RemoveAt(kept.Count - 1);
            }

            changed |= kept.Count != ring.Count;
            return kept;
        }

        /// <summary>
        /// Puts each corner that stands within the tolerance of an edge, between its ends, on that edge, where the edge is
        /// one an odd number of rings run: a long edge beside two short ones, whose corner between them is not the long
        /// one's. The rings are changed in place.
        /// </summary>
        /// <returns>For each face, whether a corner was put on an edge of it.</returns>
        private static bool[] PutCornersOnEdges(List<List<int>>[] rings, List<GeoPoint3> points, double reach)
        {
            var cornered = new bool[rings.Length];

            // A corner put on an edge splits it, and a piece of it can then meet another corner: a few rounds settle it.
            for (int round = 0; round < 4; round++)
            {
                var counts = new Dictionary<long, int>();

                foreach (List<List<int>> faceRings in rings)
                {
                    foreach (List<int> ring in faceRings)
                    {
                        for (int i = 0; i < ring.Count; i++)
                        {
                            long key = Key(ring[i], ring[(i + 1) % ring.Count]);
                            counts[key] = (counts.TryGetValue(key, out int seen) ? seen : 0) + 1;
                        }
                    }
                }

                // The corners of the edges that do not pair, sorted along each axis.
                var loose = new HashSet<int>();

                foreach (KeyValuePair<long, int> edge in counts)
                {
                    if (edge.Value % 2 != 0)
                    {
                        loose.Add((int)(edge.Key >> 32));
                        loose.Add((int)(edge.Key & 0xFFFFFFFFL));
                    }
                }

                if (loose.Count == 0)
                {
                    break;
                }

                int[][] sorted = new int[3][];

                for (int axis = 0; axis < 3; axis++)
                {
                    int[] along = new int[loose.Count];
                    loose.CopyTo(along);
                    int a = axis;
                    Array.Sort(along, (p, q) => Coordinate(points[p], a).CompareTo(Coordinate(points[q], a)));
                    sorted[axis] = along;
                }

                bool any = false;

                for (int f = 0; f < rings.Length; f++)
                {
                    List<List<int>> faceRings = rings[f];

                    for (int r = 0; r < faceRings.Count; r++)
                    {
                        List<int> ring = faceRings[r];
                        List<int> split = null;

                        for (int i = 0; i < ring.Count; i++)
                        {
                            int from = ring[i];
                            int next = ring[(i + 1) % ring.Count];
                            List<int> on = null;

                            if (counts[Key(from, next)] % 2 != 0)
                            {
                                on = CornersOn(points[from], points[next], from, next, points, sorted, reach);
                            }

                            if (on != null && split == null)
                            {
                                split = new List<int>(ring.GetRange(0, i + 1));
                            }
                            else if (split != null)
                            {
                                split.Add(from);
                            }

                            if (on != null)
                            {
                                split.AddRange(on);
                            }
                        }

                        if (split != null)
                        {
                            faceRings[r] = split;
                            cornered[f] = true;
                            any = true;
                        }
                    }
                }

                if (!any)
                {
                    break;
                }
            }

            return cornered;
        }

        /// <summary>
        /// The corners among those given that stand within the tolerance of an edge, between its ends, in order from its
        /// start; null where there are none.
        /// </summary>
        private static List<int> CornersOn(GeoPoint3 start, GeoPoint3 end, int from, int to, List<GeoPoint3> points, int[][] sorted, double reach)
        {
            GeoVector3 along = start.GetVectorTo(end);
            double length = along.Length;

            if (!(length > reach))
            {
                return null;
            }

            GeoVector3 unit = along.Divide(length);

            // Along the axis the edge spreads least along, the fewest corners lie within its box.
            double[] spread = { Math.Abs(along.X), Math.Abs(along.Y), Math.Abs(along.Z) };
            int axis = spread[0] <= spread[1] && spread[0] <= spread[2] ? 0 : spread[1] <= spread[2] ? 1 : 2;
            double low = Math.Min(Coordinate(start, axis), Coordinate(end, axis)) - reach;
            double high = Math.Max(Coordinate(start, axis), Coordinate(end, axis)) + reach;
            int[] candidates = sorted[axis];

            int first = 0, last = candidates.Length;

            while (first < last)
            {
                int middle = (first + last) >> 1;

                if (Coordinate(points[candidates[middle]], axis) < low)
                {
                    first = middle + 1;
                }
                else
                {
                    last = middle;
                }
            }

            List<(double At, int Id)> on = null;
            double reachSquared = reach * reach;

            for (int k = first; k < candidates.Length && Coordinate(points[candidates[k]], axis) <= high; k++)
            {
                int id = candidates[k];

                if (id == from || id == to)
                {
                    continue;
                }

                GeoVector3 offset = start.GetVectorTo(points[id]);
                double at = offset.DotProduct(unit);

                if (!(at > 0.0 && at < length))
                {
                    continue;
                }

                GeoVector3 across = offset.Subtract(unit.Multiply(at));

                if (across.LengthSquared <= reachSquared)
                {
                    (on = on ?? new List<(double, int)>()).Add((at, id));
                }
            }

            if (on == null)
            {
                return null;
            }

            on.Sort((p, q) => p.At.CompareTo(q.At));
            var ids = new List<int>(on.Count);

            foreach ((double _, int id) in on)
            {
                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// Whether a face's rings are still its own corners, in its own order.
        /// </summary>
        private static bool Unchanged(GeoFace3 face, List<List<int>> faceRings, List<GeoPoint3> points)
        {
            if (faceRings.Count != 1 + face.Holes.Count)
            {
                return false;
            }

            for (int r = 0; r < faceRings.Count; r++)
            {
                IReadOnlyList<GeoPoint3> corners = r == 0 ? face.Boundary.Vertices : face.Holes[r - 1].Vertices;
                List<int> ring = faceRings[r];

                if (ring.Count != corners.Count)
                {
                    return false;
                }

                for (int i = 0; i < ring.Count; i++)
                {
                    if (!points[ring[i]].Equals(corners[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerable<GeoPoint3> Corners(List<int> ring, List<GeoPoint3> points)
        {
            var corners = new GeoPoint3[ring.Count];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = points[ring[i]];
            }

            return corners;
        }

        private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        private static long Cell(double value, double size)
        {
            double scaled = Math.Floor(value / size);
            return (long)scaled;
        }

        private static double Coordinate(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        private static double DistanceSquared(GeoPoint3 a, GeoPoint3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
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
                parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
        }
    }
}
