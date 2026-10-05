using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// How much wider than itself a reach is taken, so that a gap as wide as the reach is within it however the rounding
        /// of the distance falls.
        /// </summary>
        private const double Hair = 1E-9;

        /// <summary>
        /// Welds shut the gaps a body is open by: the corners across each made one, and the corners standing on edges left
        /// open put on them, within the point tolerance, then twice it, four times and so on while below the widest gap,
        /// and the widest gap last, the first reach that makes the body valid taken.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body as the steps before left it; on return, as this step leaves them.</param>
        /// <param name="rims">The edges left open in them; on return, those this step leaves.</param>
        /// <param name="closed">The body welded valid; null when the method returns false.</param>
        /// <param name="report">What was done to it; null when the method returns false.</param>
        /// <returns>true where a reach makes the body valid.</returns>
        /// <remarks>
        /// <para>
        /// The whole body is welded before any loop of it is read as a hole: the faces of a box each on copies of its
        /// corners a few thousandths apart leave every edge open, each face's outline a loop as large as the face, and
        /// every edge of it a side of a gap. Each reach starts from the body as the steps before left it; see
        /// <see cref="WeldWithin"/>. The least that closes the body is taken, so that a corner moves no further than it has
        /// to: a chamfer 0.05 along each side, its ends 0.07 apart at the top and corners of edges open where a corner beside
        /// them stands 0.0015 off, would be made one within a tenth, and the corner is welded within two thousandths.
        /// </para>
        /// <para>
        /// Where no reach makes the body valid, the least reach that leaves the fewest sides of gaps is kept for the steps
        /// after, the first that leaves none, holes still open, ending the search; and a gap left is wider than the widest
        /// allowed: <see cref="ClosingFailure.GapTooWide"/>, where it is widest. A body open by holes only is not welded.
        /// </para>
        /// </remarks>
        private static bool TryWeldGaps(Work work, ref List<GeoFace3> faces, ref List<Rim> rims, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = null;
            int gaps = Gaps(rims);

            if (gaps == 0)
            {
                return false;
            }

            Welded best = null;

            // Only corners near the edges left open move: the rest of the body is closed as it is.
            var ends = new List<GeoPoint3>(2 * rims.Count);

            foreach (Rim rim in rims)
            {
                ends.Add(rim.From);
                ends.Add(rim.To);
            }

            foreach (double reach in Reaches(work.Tolerance.EqualPoint, work.Options.MaxGap))
            {
                Welded welded = WeldWithin(work, faces, ends, reach);

                if (welded == null)
                {
                    continue;
                }

                if (welded.Check.IsValid)
                {
                    work.Repairs.AddRange(welded.Repairs);
                    closed = welded.Body;
                    report = new SolidClosing3(work.Repairs.ToArray(), work.AddedArea, VolumeChange(work.Solid, closed), ClosingFailure.None, null);
                    return true;
                }

                if (welded.Rims != null && welded.Gaps < (best == null ? gaps : best.Gaps))
                {
                    best = welded;

                    if (best.Gaps == 0)
                    {
                        break;
                    }
                }
            }

            if (best != null)
            {
                work.Repairs.AddRange(best.Repairs);
                work.Current = best.Body;
                work.CurrentCheck = best.Check;
                faces = best.Faces;
                rims = best.Rims;
            }

            GeoPoint3? widest = WidestGap(rims);

            if (widest.HasValue)
            {
                work.Refuse(ClosingFailure.GapTooWide, widest.Value);
            }

            return false;
        }

        /// <summary>
        /// The reaches the corners are welded within, in order: the point tolerance, twice it, four times and so on while
        /// below the widest gap, and the widest gap itself last.
        /// </summary>
        /// <param name="tolerance">The point tolerance.</param>
        /// <param name="maxGap">The widest gap.</param>
        private static List<double> Reaches(double tolerance, double maxGap)
        {
            var reaches = new List<double>();

            if (tolerance > 0.0)
            {
                for (double reach = tolerance; reach < maxGap; reach *= 2.0)
                {
                    reaches.Add(reach);
                }
            }

            reaches.Add(maxGap);
            return reaches;
        }

        /// <summary>
        /// Welds the corners of the edges a body's faces leave open within a reach, and puts those standing on edges left
        /// open on them; null where nothing comes of it, or what comes of it cannot be taken.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="ends">The corners of the edges they leave open, as the check matches edges.</param>
        /// <param name="reach">The reach.</param>
        /// <returns>The body welded, valid or not, with what was done to it and the edges it leaves open; null where nothing
        /// moved, fewer than four faces are left, a ring doubles back, or the volume moved further than allowed.</returns>
        /// <remarks>
        /// <para>
        /// The corners are made one as <see cref="Weld3"/> makes them for the booleans, but only those of edges no ring
        /// pairs corner for corner that stand within the reach of an edge left open, as the check matches edges, within the
        /// point tolerance as beyond it: a slot thinner than the gap, closed, stays, and so do corners of a closed body a
        /// rounding apart far from any gap, as at the pole of a sphere meshed by rings, where welding them changes nothing.
        /// The corners within the reach of each other,
        /// however they chain, are a group, and each goes to the corner of it that takes the most of it within the reach and
        /// of those moves the volume least: a copy of a corner moved in the plane of its own face goes back onto the
        /// corner the faces beside it keep, and nothing tilts. Then each corner standing within the reach of an edge left
        /// open, between its ends, is put on it, as the corner where a long edge meets two short ones. A face whose corners
        /// moved is built again on them, as triangles on its own corners where they no longer lie flat.
        /// </para>
        /// <para>
        /// A ring running out to a corner and straight back is no face, though it reads valid: a corner 0.02 off the corner
        /// the faces beside it keep is 0.014 off the two edges of its face beside it, and put on both within a reach of
        /// 0.016, it closes the face by a needle out to the corner moved and back. Such a reach is not taken, and the next
        /// welds the two. Nor is one whose faces built again sweep out more volume than the reach times their area, each
        /// measured from where it was (see <see cref="Swept"/>): a corner moved no further than the reach sweeps no more.
        /// </para>
        /// </remarks>
        private static Welded WeldWithin(Work work, List<GeoFace3> faces, List<GeoPoint3> ends, double reach)
        {
            Tolerance tolerance = work.Tolerance;
            double within = reach * (1.0 + Hair);
            var index = new Dictionary<GeoPoint3, int>();
            var points = new List<GeoPoint3>();
            var corners = new List<List<GeoVector3>>();
            var rings = new List<List<int>>[faces.Count];

            for (int f = 0; f < faces.Count; f++)
            {
                GeoFace3 face = faces[f];
                rings[f] = new List<List<int>>(1 + face.Holes.Count) { Weld3.Positions(face.Boundary, index, points, corners) };

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    rings[f].Add(Weld3.Positions(hole, index, points, corners));
                }
            }

            // The corners that may move: those of edges no ring pairs corner for corner, within the reach of a corner of an
            // edge left open as the check matches edges. A copy of a corner on edges the check reads closed, within the
            // tolerance of the copies beside it, moves with them when they are welded, or they would leave it; one far from
            // anything open, as the corners of a closed body a rounding apart, stays.
            bool[] unpaired = Weld3.Loose(rings);
            bool[] near = NearOpenEdges(points, ends, within);
            var weldable = new bool[points.Count];

            for (int i = 0; i < weldable.Length; i++)
            {
                weldable[i] = near[i] && i < unpaired.Length && unpaired[i];
            }

            var loose = (bool[])weldable.Clone();

            // Within the tolerance first, then within the reach those still on edges no ring pairs.
            double first = Math.Min(within, tolerance.EqualPoint * (1.0 + Hair));
            int[] target = new int[points.Count];

            for (int i = 0; i < target.Length; i++)
            {
                target[i] = i;
            }

            if (first > 0.0)
            {
                target = Weld3.Representatives(points, corners, first, loose);
                CollapseAll(rings, target);
            }

            if (within > first)
            {
                bool[] still = Weld3.Loose(rings);

                for (int i = 0; i < loose.Length; i++)
                {
                    loose[i] &= i < still.Length && still[i];
                }

                int[] wider = Weld3.Representatives(points, corners, within, loose);
                CollapseAll(rings, wider);

                for (int i = 0; i < target.Length; i++)
                {
                    target[i] = wider[target[i]];
                }
            }

            var put = new List<(int Corner, int From, int To)>();
            bool[] cornered = PutCornersOnOpenEdges(rings, points, within, put, weldable);

            if (HasNeedle(rings, points, tolerance.EqualPoint))
            {
                return null;
            }

            var built = new List<GeoFace3>(faces.Count);
            var vanished = new List<int>();
            Tolerance pieces = LoopAssembly.ForPieces(tolerance);
            bool changed = false;
            double touched = 0.0;
            double swept = 0.0;

            for (int f = 0; f < faces.Count; f++)
            {
                List<List<int>> faceRings = rings[f];

                if (!cornered[f] && Weld3.Unchanged(faces[f], faceRings, points))
                {
                    built.Add(faces[f]);
                    continue;
                }

                changed = true;
                touched += faces[f].Area;

                // A face whose corners came to fewer than three points covered nothing within the reach; the faces beside it
                // meet along its edges without it.
                GeoFace3[] made = faceRings[0].Count < 3 ? new GeoFace3[0] : Rebuilt(faceRings, points, pieces);
                swept += Swept(faces[f], made);

                if (made.Length == 0)
                {
                    vanished.Add(f);
                    continue;
                }

                built.AddRange(made);
            }

            if (!changed || built.Count < 4 || swept > within * touched)
            {
                return null;
            }

            var body = new GeoSolid3(built, work.Solid.Openings);
            SolidValidation3 check = body.Validate(tolerance);
            List<Rim> left = null;
            int gaps = 0;

            if (check.IsValid && NeedleAt(built, tolerance.EqualPoint, out _))
            {
                return null;
            }

            // A body still open says what it leaves open; one with a fin on an open edge leaves nothing to go on with.
            if (!check.IsValid)
            {
                List<Stretch> stretches = FindStretches(built, new bool[built.Count], tolerance);

                if (TryReadOpenEdges(stretches, null, null, work.Options.MaxGap, out left, out _))
                {
                    gaps = Gaps(left);
                }
                else
                {
                    left = null;
                }
            }

            return new Welded(built, Repairs(points, target, put, faces, vanished), body, check, left, gaps);
        }

        /// <summary>
        /// The changes a reach made, in the order it made them: one weld for each group of corners made one, at the corner
        /// they went to and as large as the furthest of them moved; each corner put on an edge, as far as it stood off it;
        /// and each face that came to nothing.
        /// </summary>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="target">The position each went to.</param>
        /// <param name="put">Each corner put on an edge, and the edge.</param>
        /// <param name="faces">The faces as they were.</param>
        /// <param name="vanished">The faces that came to nothing, by index.</param>
        private static List<SolidRepair3> Repairs(List<GeoPoint3> points, int[] target, List<(int Corner, int From, int To)> put, List<GeoFace3> faces, List<int> vanished)
        {
            var repairs = new List<SolidRepair3>();
            var furthest = new Dictionary<int, double>();
            var groups = new List<int>();

            for (int i = 0; i < target.Length; i++)
            {
                int to = target[i];

                if (to == i)
                {
                    continue;
                }

                double moved = points[i].DistanceTo(points[to]);

                if (!furthest.TryGetValue(to, out double most))
                {
                    groups.Add(to);
                    furthest.Add(to, moved);
                }
                else if (moved > most)
                {
                    furthest[to] = moved;
                }
            }

            groups.Sort();

            foreach (int to in groups)
            {
                repairs.Add(new SolidRepair3(SolidRepairKind.Weld, points[to], furthest[to]));
            }

            foreach ((int corner, int from, int to) in put)
            {
                GeoPoint3 at = points[corner];
                repairs.Add(new SolidRepair3(SolidRepairKind.SplitEdge, at, at.DistanceTo(NearestOnSegment(points[from], points[to], at))));
            }

            foreach (int f in vanished)
            {
                repairs.Add(new SolidRepair3(SolidRepairKind.Drop, faces[f].Centroid, faces[f].Area));
            }

            return repairs;
        }

        /// <summary>
        /// The volume a face built again sweeps out from where it was: what lies between its pieces and the face as it was,
        /// measured from the face's centroid, on its plane.
        /// </summary>
        /// <param name="face">The face as it was.</param>
        /// <param name="pieces">The faces it was built again as; none where it came to nothing.</param>
        /// <remarks>
        /// Measured from a point on the face itself, a corner moved within the face's plane sweeps nothing, and one moved off
        /// it by a distance sweeps a third of that times the area beside it, wherever the body lies: the faces of a body that
        /// does not close, measured from one point far off, change by as much as a sliver of a face times that distance.
        /// </remarks>
        private static double Swept(GeoFace3 face, GeoFace3[] pieces)
        {
            GeoPoint3 apex = face.Centroid;
            double after = SignedVolumeAbout(pieces, apex);
            double before = SignedVolumeAbout(new[] { face }, apex);

            return Math.Abs(after - before);
        }

        /// <summary>
        /// A face built again on the corners its rings came to, its holes of fewer than three corners left out: one face
        /// where they lie flat, triangles on its own corners where they do not.
        /// </summary>
        /// <param name="faceRings">The rings, the boundary first.</param>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="pieces">The tolerance a piece of a surface is built within.</param>
        private static GeoFace3[] Rebuilt(List<List<int>> faceRings, List<GeoPoint3> points, Tolerance pieces)
        {
            var holes = new List<IEnumerable<GeoPoint3>>();

            for (int r = 1; r < faceRings.Count; r++)
            {
                if (faceRings[r].Count >= 3)
                {
                    holes.Add(Weld3.Corners(faceRings[r], points));
                }
            }

            return Loops3.ToFaces(Weld3.Corners(faceRings[0], points), holes, pieces);
        }

        /// <summary>
        /// Puts each corner of an edge left open that stands within a reach of another edge left open, between its ends, on
        /// that edge, as <see cref="Weld3"/> does for the booleans, each of two corners as near the start of an edge as the
        /// other taken in the order of their positions. The rings are changed in place.
        /// </summary>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="reach">The reach.</param>
        /// <param name="put">Each corner put on an edge and the edge, added to in the order they are put.</param>
        /// <param name="open">For each position, whether it is a corner of an edge left open, as the check matches edges: only
        /// these are put on edges, and only edges between two of them split.</param>
        /// <returns>For each face, whether a corner was put on an edge of it.</returns>
        /// <remarks>
        /// Every corner is put within the reach of an edge as it stands after the welds, and the pieces a split leaves are
        /// not split again: put on the pieces of a split, a corner could stand within the reach of a piece and not of the
        /// edge, and a gap wider than the reach would close by steps no wider.
        /// </remarks>
        private static bool[] PutCornersOnOpenEdges(List<List<int>>[] rings, List<GeoPoint3> points, double reach, List<(int Corner, int From, int To)> put, bool[] open)
        {
            var cornered = new bool[rings.Length];
            var counts = new Dictionary<long, int>();

            foreach (List<List<int>> faceRings in rings)
            {
                foreach (List<int> ring in faceRings)
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        long key = EdgeKey(ring[i], ring[(i + 1) % ring.Count]);
                        counts[key] = (counts.TryGetValue(key, out int seen) ? seen : 0) + 1;
                    }
                }
            }

            // The corners of the edges an odd number of rings run, by position, sorted along each axis.
            var loose = new List<int>();
            var marked = new HashSet<int>();

            foreach (KeyValuePair<long, int> edge in counts)
            {
                if (edge.Value % 2 != 0)
                {
                    int a = (int)(edge.Key >> 32);
                    int b = (int)(edge.Key & 0xFFFFFFFFL);

                    if (open[a] && marked.Add(a))
                    {
                        loose.Add(a);
                    }

                    if (open[b] && marked.Add(b))
                    {
                        loose.Add(b);
                    }
                }
            }

            if (loose.Count == 0)
            {
                return cornered;
            }

            loose.Sort();
            var sorted = new int[3][];

            for (int axis = 0; axis < 3; axis++)
            {
                int[] along = loose.ToArray();
                int a = axis;

                Array.Sort(along, (p, q) =>
                {
                    int byCoordinate = Coordinate(points[p], a).CompareTo(Coordinate(points[q], a));
                    return byCoordinate != 0 ? byCoordinate : p.CompareTo(q);
                });

                sorted[axis] = along;
            }

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
                        bool unpaired = counts[EdgeKey(from, next)] % 2 != 0 && marked.Contains(from) && marked.Contains(next);
                        List<int> on = unpaired ? CornersOnEdge(points, from, next, sorted, reach) : null;

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

                            foreach (int corner in on)
                            {
                                put.Add((corner, from, next));
                            }
                        }
                    }

                    if (split != null)
                    {
                        faceRings[r] = split;
                        cornered[f] = true;
                    }
                }
            }

            return cornered;
        }

        /// <summary>
        /// The corners among those given that stand within a reach of an edge, between its ends, in order from its start, the
        /// one first in position where two are as far along; null where there are none.
        /// </summary>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="from">The position the edge starts at.</param>
        /// <param name="to">The position it ends at.</param>
        /// <param name="sorted">The corners to look among, sorted along each axis.</param>
        /// <param name="reach">The reach.</param>
        private static List<int> CornersOnEdge(List<GeoPoint3> points, int from, int to, int[][] sorted, double reach)
        {
            GeoPoint3 start = points[from];
            GeoPoint3 end = points[to];
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

                if (offset.Subtract(unit.Multiply(at)).LengthSquared <= reachSquared)
                {
                    (on = on ?? new List<(double, int)>()).Add((at, id));
                }
            }

            if (on == null)
            {
                return null;
            }

            on.Sort((p, q) =>
            {
                int byAt = p.At.CompareTo(q.At);
                return byAt != 0 ? byAt : p.Id.CompareTo(q.Id);
            });

            var ids = new List<int>(on.Count);

            foreach ((double _, int id) in on)
            {
                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// For each corner, whether it stands within a reach of a corner of an edge left open.
        /// </summary>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="ends">The corners of the edges left open.</param>
        /// <param name="reach">The reach.</param>
        /// <remarks>
        /// The corners of the edges left open are filed in cubes as wide as the reach, so that a corner within it of one lies
        /// in the cube of that one or a cube touching it.
        /// </remarks>
        private static bool[] NearOpenEdges(List<GeoPoint3> points, List<GeoPoint3> ends, double reach)
        {
            var near = new bool[points.Count];

            if (!(reach > 0.0))
            {
                return near;
            }

            var cells = new Dictionary<(long, long, long), List<GeoPoint3>>();

            foreach (GeoPoint3 end in ends)
            {
                var cell = (CellOf(end.X, reach), CellOf(end.Y, reach), CellOf(end.Z, reach));

                if (!cells.TryGetValue(cell, out List<GeoPoint3> filed))
                {
                    filed = new List<GeoPoint3>();
                    cells.Add(cell, filed);
                }

                filed.Add(end);
            }

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint3 point = points[i];
                long x = CellOf(point.X, reach), y = CellOf(point.Y, reach), z = CellOf(point.Z, reach);

                for (long dx = -1; dx <= 1 && !near[i]; dx++)
                {
                    for (long dy = -1; dy <= 1 && !near[i]; dy++)
                    {
                        for (long dz = -1; dz <= 1 && !near[i]; dz++)
                        {
                            if (!cells.TryGetValue((x + dx, y + dy, z + dz), out List<GeoPoint3> filed))
                            {
                                continue;
                            }

                            foreach (GeoPoint3 end in filed)
                            {
                                if (point.DistanceTo(end) <= reach)
                                {
                                    near[i] = true;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            return near;
        }

        /// <summary>
        /// The cube a coordinate falls in, cubes a size wide.
        /// </summary>
        /// <param name="value">The coordinate.</param>
        /// <param name="size">The size.</param>
        private static long CellOf(double value, double size) => (long)Math.Floor(value / size);

        /// <summary>
        /// Puts every ring on the positions its corners are welded to.
        /// </summary>
        /// <param name="rings">The rings of each face, by position, changed in place.</param>
        /// <param name="to">The position each position is welded to.</param>
        private static void CollapseAll(List<List<int>>[] rings, int[] to)
        {
            foreach (List<List<int>> faceRings in rings)
            {
                for (int r = 0; r < faceRings.Count; r++)
                {
                    faceRings[r] = Weld3.Collapse(faceRings[r], to, out _);
                }
            }
        }

        /// <summary>
        /// Determines whether a ring runs out to a corner and straight back: the corners either side of one of its corners
        /// one point within the point tolerance.
        /// </summary>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="reach">The point tolerance.</param>
        private static bool HasNeedle(List<List<int>>[] rings, List<GeoPoint3> points, double reach)
        {
            foreach (List<List<int>> faceRings in rings)
            {
                foreach (List<int> ring in faceRings)
                {
                    int count = ring.Count;

                    if (count < 3)
                    {
                        continue;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        int before = ring[(i + count - 1) % count];
                        int after = ring[(i + 1) % count];

                        if (before == after || points[before].DistanceTo(points[after]) <= reach)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a corner of a face's ring where the ring runs out to it and straight back, as <see cref="HasNeedle"/> reads
        /// rings.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="reach">The point tolerance.</param>
        /// <param name="tip">The corner the ring runs out to; the origin where there is none.</param>
        /// <returns>true where a ring doubles back.</returns>
        private static bool NeedleAt(IReadOnlyList<GeoFace3> faces, double reach, out GeoPoint3 tip)
        {
            foreach (GeoFace3 face in faces)
            {
                if (RingNeedleAt(face.Boundary, reach, out tip))
                {
                    return true;
                }

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    if (RingNeedleAt(hole, reach, out tip))
                    {
                        return true;
                    }
                }
            }

            tip = GeoPoint3.Origin;
            return false;
        }

        /// <summary>
        /// Finds a corner of a ring where it runs out to it and straight back.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <param name="reach">The point tolerance.</param>
        /// <param name="tip">The corner; the origin where there is none.</param>
        private static bool RingNeedleAt(GeoPolygon3 ring, double reach, out GeoPoint3 tip)
        {
            int count = ring.VertexCount;

            for (int i = 0; i < count; i++)
            {
                if (ring[(i + count - 1) % count].DistanceTo(ring[(i + 1) % count]) <= reach)
                {
                    tip = ring[i];
                    return true;
                }
            }

            tip = GeoPoint3.Origin;
            return false;
        }

        /// <summary>
        /// The key of an edge between two positions, the same either way round.
        /// </summary>
        /// <param name="a">The one position.</param>
        /// <param name="b">The other.</param>
        private static long EdgeKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>
        /// The box of faces.
        /// </summary>
        /// <param name="faces">The faces.</param>
        private static GeoAabb3 BoxOf(IReadOnlyList<GeoFace3> faces)
        {
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (GeoFace3 face in faces)
            {
                box = box.Union(face.GetAabb());
            }

            return box;
        }

        /// <summary>
        /// A body as a reach of the welding left it: its faces, what was done, whether it is valid, and the edges it leaves
        /// open.
        /// </summary>
        private sealed class Welded
        {
            /// <summary>
            /// Holds what a reach came to.
            /// </summary>
            /// <param name="faces">The faces.</param>
            /// <param name="repairs">What was done, in order.</param>
            /// <param name="body">The body of the faces.</param>
            /// <param name="check">What <see cref="GeoSolid3.Validate(Tolerance)"/> found of it.</param>
            /// <param name="rims">The edges it leaves open; null where it is valid, or a fin stands on one.</param>
            /// <param name="gaps">How many of them are sides of gaps.</param>
            internal Welded(List<GeoFace3> faces, List<SolidRepair3> repairs, GeoSolid3 body, SolidValidation3 check, List<Rim> rims, int gaps)
            {
                Faces = faces;
                Repairs = repairs;
                Body = body;
                Check = check;
                Rims = rims;
                Gaps = gaps;
            }

            /// <summary>Gets the faces.</summary>
            internal List<GeoFace3> Faces { get; }

            /// <summary>Gets what was done, in order.</summary>
            internal List<SolidRepair3> Repairs { get; }

            /// <summary>Gets the body of the faces.</summary>
            internal GeoSolid3 Body { get; }

            /// <summary>Gets what <see cref="GeoSolid3.Validate(Tolerance)"/> found of it.</summary>
            internal SolidValidation3 Check { get; }

            /// <summary>Gets the edges it leaves open; null where it is valid, or a fin stands on one.</summary>
            internal List<Rim> Rims { get; }

            /// <summary>Gets how many of them are sides of gaps.</summary>
            internal int Gaps { get; }
        }
    }
}
