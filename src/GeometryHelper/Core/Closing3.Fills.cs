using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// Determines whether options allow any hole to be filled: a largest hole above nought, and a strategy that fills.
        /// </summary>
        /// <param name="options">The options.</param>
        private static bool MayFill(SolidClosingOptions options) => options.MaxHoleArea > 0.0 && options.Fill != FillStrategy.None;

        /// <summary>
        /// Fills the loops left open, holes and gaps too wide for the welds alike, each hole by the faces the options allow,
        /// and takes the body where it comes out valid; false, the trouble noted, where a hole cannot be filled or what comes
        /// of it is not a body.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body as the welding left them.</param>
        /// <param name="origins">For each, the face given it comes of, by index.</param>
        /// <param name="loops">The loops left open in them, in order.</param>
        /// <param name="closed">The body filled; null when the method returns false.</param>
        /// <param name="report">What was done to it; null when the method returns false.</param>
        /// <returns>true where every loop is filled and the body comes out valid.</returns>
        /// <remarks>
        /// <para>
        /// The holes are those <see cref="ReadHoles"/> reads, each filled in the order of its first loop, and the first that
        /// cannot be filled stops the filling there. A face added lying back to back with a face of the body is a skin of no
        /// thickness that reads valid and holds nothing, and is not taken: a face lying on the top of a box, every edge of it
        /// open, would be filled by itself turned over. Where a hole has one way of filling it lying on no face of the body,
        /// that way is taken, whatever the strategy. Where it has two, as the two ends of a hole through a plate whose walls
        /// are missing have, capped or walled, <see cref="FillStrategy.WhenUnambiguous"/> takes neither and the report says
        /// <see cref="ClosingFailure.HoleAmbiguous"/>, and <see cref="FillStrategy.MinArea"/> takes the one adding the least
        /// area, the caps where the two add as much. The faces taken across each hole are held to
        /// <see cref="SolidClosingOptions.MaxHoleArea"/>, and a hole needing more is <see cref="ClosingFailure.HoleTooLarge"/>,
        /// at a point within the faces it would take.
        /// </para>
        /// <para>
        /// The way each shell faces is read again on the body closed: a shell open by a hole encloses a volume only from
        /// where it is measured, and the volume a closed shell encloses says which way it faces for certain; see
        /// <see cref="FaceShellsOutwards"/>. Then the body is checked as the welding's is: valid within the tolerance, no
        /// ring running out to a corner and straight back, and no face new to it lying back to back with another.
        /// </para>
        /// <para>
        /// Each hole filled is one change: its size is the area added across it, and its place the middle of the faces
        /// added, where they would balance.
        /// </para>
        /// </remarks>
        private static bool TryFillHoles(Work work, List<GeoFace3> faces, List<int> origins, List<Loop> loops, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = null;

            if (loops.Count == 0)
            {
                return false;
            }

            Tolerance tolerance = work.Tolerance;
            SolidClosingOptions options = work.Options;
            var shells = new FaceShells(faces, tolerance);
            List<Hole> holes = ReadHoles(loops, options, shells);
            var boxes = new FaceBoxes(faces, null, tolerance);
            var near = new List<int>();
            var taken = new List<Patch>();

            for (int h = 0; h < holes.Count; h++)
            {
                Hole hole = holes[h];

                if (hole.Failure != ClosingFailure.None)
                {
                    work.Refuse(hole.Failure, hole.FailureAt);
                    return false;
                }

                bool caps = LieOnNoFace(hole.Caps, faces, boxes, near, tolerance, out GeoPoint3 skin);
                bool walls = hole.Walls != null
                    && LieOnNoFace(new List<Patch> { hole.Walls }, faces, boxes, near, tolerance, out _)
                    && WallsClose(faces, holes, h, tolerance);

                List<Patch> fill;

                if (caps && walls)
                {
                    if (options.Fill == FillStrategy.WhenUnambiguous)
                    {
                        work.Refuse(ClosingFailure.HoleAmbiguous, PointOn(hole.Caps[0], tolerance));
                        return false;
                    }

                    fill = hole.Walls.Area < AreaOf(hole.Caps) ? new List<Patch> { hole.Walls } : hole.Caps;
                }
                else if (caps || walls)
                {
                    fill = caps ? hole.Caps : new List<Patch> { hole.Walls };
                }
                else
                {
                    work.Refuse(ClosingFailure.StillOpen, skin);
                    return false;
                }

                foreach (Patch patch in fill)
                {
                    // A hole as large as allowed is filled.
                    if (patch.Area > options.MaxHoleArea)
                    {
                        work.Refuse(ClosingFailure.HoleTooLarge, PointOn(patch, tolerance));
                        return false;
                    }
                }

                taken.AddRange(fill);
            }

            var all = new List<GeoFace3>(faces);

            foreach (Patch patch in taken)
            {
                all.AddRange(patch.Faces);
            }

            FaceShellsOutwards(work, all, faces.Count, origins, taken, loops, shells);
            return TryTakeFilled(work, all, faces.Count, origins, taken, out closed, out report);
        }

        /// <summary>
        /// Takes the body filled where it is valid within the tolerance, with no ring doubling back and no face new to it
        /// lying back to back with another; false, the trouble noted, otherwise.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="all">The faces of the body filled: those the welding left, then the faces added.</param>
        /// <param name="given">How many of them the welding left.</param>
        /// <param name="origins">For each of those, the face given it comes of, by index.</param>
        /// <param name="taken">The fills, in order.</param>
        /// <param name="closed">The body; null when the method returns false.</param>
        /// <param name="report">What was done to it; null when the method returns false.</param>
        private static bool TryTakeFilled(Work work, List<GeoFace3> all, int given, List<int> origins, List<Patch> taken, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = null;
            Tolerance tolerance = work.Tolerance;
            var body = new GeoSolid3(all, work.Solid.Openings);
            SolidValidation3 check = body.Validate(tolerance);
            work.Current = body;
            work.CurrentCheck = check;

            if (!check.IsValid)
            {
                work.Refuse(ClosingFailure.StillOpen, TroubleAt(check, body));
                return false;
            }

            if (NeedleAt(all, tolerance.EqualPoint, out GeoPoint3 tip))
            {
                work.Refuse(ClosingFailure.StillOpen, tip);
                return false;
            }

            // New to the body: the faces added, and those turned over.
            var fresh = new List<bool>(all.Count);

            for (int k = 0; k < all.Count; k++)
            {
                fresh.Add(k >= given || work.Turned[origins[k]]);
            }

            if (HasSkin(all, fresh, tolerance, out GeoPoint3 skin))
            {
                work.Refuse(ClosingFailure.StillOpen, skin);
                return false;
            }

            foreach (Patch patch in taken)
            {
                work.Repairs.Add(new SolidRepair3(SolidRepairKind.Fill, patch.Middle, patch.Area));
                work.AddedArea += patch.Area;
            }

            closed = body;
            report = new SolidClosing3(work.Repairs.ToArray(), work.AddedArea, VolumeChange(work.Solid, body), ClosingFailure.None, null);
            return true;
        }

        /// <summary>
        /// Reads the loops left open as holes to fill, in the order of their first loops: a loop flat within the planar
        /// tolerance is filled by one face on its corners, the loops lying in its plane inside it the other way round taken
        /// as the holes of that face; and two such faces of no holes at the two ends of a hole through a shell can be walls
        /// between them instead.
        /// </summary>
        /// <param name="loops">The loops left open, in order.</param>
        /// <param name="options">The options.</param>
        /// <param name="shells">The shells of the faces the loops are left open in.</param>
        /// <returns>The holes, each with the reason it cannot be filled where its loops cannot be read as one.</returns>
        /// <remarks>
        /// <para>
        /// A loop is flat where none of its corners stands further than the planar tolerance off the plane through the
        /// average of its corners square to its area. Each face is built on the loops' corners as the body has them, bit
        /// for bit, facing the way a face closing the edges would: a box missing its top is filled by a face on the very
        /// corners of the faces round the hole, though the ends of the stretches the check reads along them can stand a
        /// rounding off them.
        /// </para>
        /// <para>
        /// A flat loop lies inside another where it lies in the other's plane, within the planar tolerance, and a corner of
        /// it, the first not on the other's edges, lies inside the other, seen square to it: the middle of a loop round a
        /// corner need not lie inside it. Of the loops it lies inside, the smallest holds it. A loop held by the boundary of
        /// a face and running the other way round is a hole of that face; one held by a hole of a face is a face of its own
        /// again, an island, as is one running the same way round as the loop holding it.
        /// </para>
        /// <para>
        /// Two faces of no holes, one the other moved along their normal by more than the point tolerance, corner for corner
        /// within it, facing away from each other, on loops of one shell, are the two ends of a hole through it whose walls
        /// are missing: the faces cap it, and walls from each edge of the one to the edge of the other it moved onto close it
        /// round the hole instead. A box missing its top and bottom has two such loops, and the walls there lie on its sides;
        /// a plate missing the walls of a hole through it has two, and the walls lie on nothing.
        /// </para>
        /// <para>
        /// A loop out of flat takes no face: further out of flat than <see cref="SolidClosingOptions.MaxOffFlat"/> it is
        /// <see cref="ClosingFailure.HoleOffFlat"/>, and within it, no triangles are made across it here, and the body is
        /// still open there. A loop enclosing no area, or one whose face cannot be built, leaves the body still open too.
        /// </para>
        /// </remarks>
        private static List<Hole> ReadHoles(List<Loop> loops, SolidClosingOptions options, FaceShells shells)
        {
            Tolerance tolerance = options.Tolerance;
            Tolerance build = ForFills(tolerance);
            int count = loops.Count;
            var rings = new GeoPolygon3[count];
            var planes = new GeoPlane3[count];
            var boxes = new GeoAabb3[count];
            var holes = new List<Hole>();

            for (int i = 0; i < count; i++)
            {
                Loop loop = loops[i];

                if (loop.Area > 0.0 && loop.OffFlat <= tolerance.EqualPlanar)
                {
                    rings[i] = TryRing(loop.Corners, build);
                    planes[i] = new GeoPlane3(loop.Middle, loop.Normal);
                    boxes[i] = GeoAabb3.FromPoints(loop.Corners);
                }

                if (rings[i] == null)
                {
                    bool offFlat = loop.Area > 0.0 && loop.OffFlat > Math.Max(tolerance.EqualPlanar, options.MaxOffFlat);
                    holes.Add(Hole.Unread(i, offFlat ? ClosingFailure.HoleOffFlat : ClosingFailure.StillOpen, loop.Middle));
                }
            }

            int[] holder = Holders(loops, rings, planes, boxes, tolerance);

            // Each flat loop the boundary of a face or a hole of one, the larger first, as the loops holding them are.
            var order = new List<int>();

            for (int i = 0; i < count; i++)
            {
                if (rings[i] != null)
                {
                    order.Add(i);
                }
            }

            order.Sort((a, b) =>
            {
                int byArea = loops[b].Area.CompareTo(loops[a].Area);
                return byArea != 0 ? byArea : a.CompareTo(b);
            });

            var boundary = new bool[count];
            var inner = new List<int>[count];

            foreach (int i in order)
            {
                int h = holder[i];
                boundary[i] = h < 0 || !boundary[h] || loops[i].Normal.DotProduct(loops[h].Normal) >= 0.0;

                if (!boundary[i])
                {
                    (inner[h] ?? (inner[h] = new List<int>())).Add(i);
                }
            }

            var patches = new Patch[count];

            for (int i = 0; i < count; i++)
            {
                if (boundary[i])
                {
                    inner[i]?.Sort();
                    patches[i] = TryPatch(i, inner[i], rings, build);
                }
            }

            int[][] across = PairEnds(loops, rings, patches, inner, tolerance, shells, out int[] other);

            for (int i = 0; i < count; i++)
            {
                // The other end of a hole through the body goes with the first.
                if (!boundary[i] || (other[i] >= 0 && other[i] < i))
                {
                    continue;
                }

                var hole = new Hole(First(i, inner[i], other[i]));

                if (patches[i] == null)
                {
                    hole.Fail(ClosingFailure.StillOpen, loops[i].Middle);
                }
                else
                {
                    hole.Caps.Add(patches[i]);

                    if (other[i] >= 0)
                    {
                        hole.Caps.Add(patches[other[i]]);
                        hole.Walls = TryWalls(i, other[i], across[i], loops, build);
                    }
                }

                holes.Add(hole);
            }

            holes.Sort((a, b) => a.First.CompareTo(b.First));
            return holes;
        }

        /// <summary>
        /// The first of the loops of a hole, by index: the boundary, the holes of its face, and the other end of a hole
        /// through the body, where there is one.
        /// </summary>
        /// <param name="boundary">The loop of the boundary.</param>
        /// <param name="inner">The loops of the face's holes, in order; null where there are none.</param>
        /// <param name="other">The loop at the other end; below nought where there is none.</param>
        private static int First(int boundary, List<int> inner, int other)
        {
            int first = boundary;

            if (inner != null)
            {
                first = Math.Min(first, inner[0]);
            }

            return other >= 0 ? Math.Min(first, other) : first;
        }

        /// <summary>
        /// For each flat loop, the smallest flat loop holding it, in whose plane it lies with a corner of it inside; below
        /// nought where none does, and for the loops not flat.
        /// </summary>
        /// <param name="loops">The loops.</param>
        /// <param name="rings">The polygon of each flat loop; null for the others.</param>
        /// <param name="planes">The plane of each flat loop.</param>
        /// <param name="boxes">The box of each flat loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The loops are filed by their boxes, and each is set only against those whose boxes hold its own.
        /// </remarks>
        private static int[] Holders(List<Loop> loops, GeoPolygon3[] rings, GeoPlane3[] planes, GeoAabb3[] boxes, Tolerance tolerance)
        {
            int count = loops.Count;
            var holder = new int[count];
            var skip = new bool[count];

            for (int i = 0; i < count; i++)
            {
                holder[i] = -1;
                skip[i] = rings[i] == null;
            }

            var filed = new FaceBoxes(boxes, skip, tolerance);
            var near = new List<int>();

            for (int b = 0; b < count; b++)
            {
                if (rings[b] == null)
                {
                    continue;
                }

                filed.Meeting(boxes[b], near);

                foreach (int a in near)
                {
                    if (a == b || !(loops[a].Area > loops[b].Area) || !boxes[a].Contains(boxes[b], tolerance))
                    {
                        continue;
                    }

                    // The smallest holding it, the first by index of two as large.
                    if (holder[b] >= 0 && !(loops[a].Area < loops[holder[b]].Area))
                    {
                        continue;
                    }

                    if (LiesIn(loops[b], planes[a], tolerance) && IsInside(loops[b], rings[a], planes[a], tolerance))
                    {
                        holder[b] = a;
                    }
                }
            }

            return holder;
        }

        /// <summary>
        /// Determines whether a loop lies inside a polygon in whose plane it lies, as its first corner not on the polygon's
        /// edges says, seen square to the plane; false where every corner of it is on them.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="ring">The polygon.</param>
        /// <param name="plane">The polygon's plane.</param>
        /// <param name="tolerance">The tolerance a corner is on an edge within.</param>
        private static bool IsInside(Loop loop, GeoPolygon3 ring, GeoPlane3 plane, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in loop.Corners)
            {
                PointLocation where = Containment3.LocateInPlane(ring, plane, corner, tolerance);

                if (where != PointLocation.OnSide)
                {
                    return where == PointLocation.Inside;
                }
            }

            return false;
        }

        /// <summary>
        /// Pairs the faces of no holes at the two ends of a hole through a shell: one the other moved along their normal,
        /// corner for corner, facing away from each other, on loops of one shell; each, in the order of the loops, with the
        /// first such face not paired already.
        /// </summary>
        /// <param name="loops">The loops.</param>
        /// <param name="rings">The polygon of each flat loop; null for the others.</param>
        /// <param name="patches">The face built on each loop that is the boundary of one; null for the others.</param>
        /// <param name="inner">The holes of each such face, by loop; null where there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="shells">The shells of the faces the loops are left open in.</param>
        /// <param name="other">For each loop, the loop at the other end of its hole; below nought where there is none.</param>
        /// <returns>For each loop paired, the corner of the loop at the other end each of its corners moved onto, by index;
        /// null for the others.</returns>
        /// <remarks>
        /// Two such faces are as large as each other, so the faces are filed by area, and each is set only against those as
        /// large within what the point tolerance allows along its edges.
        /// </remarks>
        private static int[][] PairEnds(List<Loop> loops, GeoPolygon3[] rings, Patch[] patches, List<int>[] inner, Tolerance tolerance, FaceShells shells, out int[] other)
        {
            int count = loops.Count;
            other = new int[count];
            var across = new int[count][];
            var caps = new List<int>();

            for (int i = 0; i < count; i++)
            {
                other[i] = -1;

                if (patches[i] != null && inner[i] == null)
                {
                    caps.Add(i);
                }
            }

            var byArea = new List<int>(caps);

            byArea.Sort((a, b) =>
            {
                int byLoopArea = loops[a].Area.CompareTo(loops[b].Area);
                return byLoopArea != 0 ? byLoopArea : a.CompareTo(b);
            });

            var place = new int[count];

            for (int k = 0; k < byArea.Count; k++)
            {
                place[byArea[k]] = k;
            }

            foreach (int i in caps)
            {
                if (other[i] >= 0)
                {
                    continue;
                }

                double slack = 2.0 * tolerance.EqualPoint * rings[i].Length;
                int best = -1;
                int[] moved = null;

                foreach (int step in new[] { -1, 1 })
                {
                    for (int k = place[i] + step; k >= 0 && k < byArea.Count && Math.Abs(loops[byArea[k]].Area - loops[i].Area) <= slack; k += step)
                    {
                        int j = byArea[k];

                        if (other[j] >= 0 || (best >= 0 && j > best))
                        {
                            continue;
                        }

                        int[] onto = Moved(loops[i], loops[j], tolerance);

                        if (onto != null && shells.SameShell(loops[i], loops[j]))
                        {
                            best = j;
                            moved = onto;
                        }
                    }
                }

                if (best < 0)
                {
                    continue;
                }

                other[i] = best;
                other[best] = i;
                across[i] = moved;
                across[best] = new int[moved.Length];

                for (int c = 0; c < moved.Length; c++)
                {
                    across[best][moved[c]] = c;
                }
            }

            return across;
        }

        /// <summary>
        /// For each corner of one loop, the corner of another the first is moved onto, where the other is the one moved
        /// along its normal by more than the point tolerance, corner for corner within it, and facing the other way: run the
        /// other way round, as the far end of a hole through a body is; null where it is not.
        /// </summary>
        /// <param name="one">The one loop.</param>
        /// <param name="other">The other.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static int[] Moved(Loop one, Loop other, Tolerance tolerance)
        {
            int count = one.Corners.Count;

            if (other.Corners.Count != count || !(one.Normal.DotProduct(other.Normal) < Facing))
            {
                return null;
            }

            double reach = tolerance.EqualPoint;
            GeoVector3 move = one.Middle.GetVectorTo(other.Middle);
            double along = move.DotProduct(one.Normal);

            if (!(Math.Abs(along) > reach) || move.Subtract(one.Normal.Multiply(along)).Length > reach)
            {
                return null;
            }

            GeoPoint3 start = one.Corners[0].Add(move);
            int first = -1;

            for (int m = 0; m < count; m++)
            {
                if (other.Corners[m].DistanceTo(start) <= reach)
                {
                    first = m;
                    break;
                }
            }

            if (first < 0)
            {
                return null;
            }

            var onto = new int[count];

            for (int c = 0; c < count; c++)
            {
                onto[c] = (first - c + count) % count;

                if (other.Corners[onto[c]].DistanceTo(one.Corners[c].Add(move)) > reach)
                {
                    return null;
                }
            }

            return onto;
        }

        /// <summary>
        /// Builds the walls between the two ends of a hole through a body: from each edge of the one to the edge of the other
        /// it moved onto, each wall on the four corners as the body has them; null where one cannot be built.
        /// </summary>
        /// <param name="one">The loop at the one end, by index.</param>
        /// <param name="other">The loop at the other.</param>
        /// <param name="onto">For each corner of the one, the corner of the other it moved onto.</param>
        /// <param name="loops">The loops.</param>
        /// <param name="build">The tolerance the walls are built within.</param>
        private static Patch TryWalls(int one, int other, int[] onto, List<Loop> loops, Tolerance build)
        {
            List<GeoPoint3> near = loops[one].Corners;
            List<GeoPoint3> far = loops[other].Corners;
            int count = near.Count;
            var walls = new GeoFace3[count];

            try
            {
                // Each wall runs the edge of each end the way a face closing it would.
                for (int c = 0; c < count; c++)
                {
                    int next = (c + 1) % count;
                    var corners = new[] { near[c], near[next], far[onto[next]], far[onto[c]] };
                    walls[c] = new GeoFace3(new GeoPolygon3(corners, build), null, build);
                }
            }
            catch (ArgumentException)
            {
                return null;
            }

            return new Patch(walls, new[] { one, other });
        }

        /// <summary>
        /// Builds the polygon of a loop's corners as they are, within a tolerance; null where it is refused.
        /// </summary>
        /// <param name="corners">The corners.</param>
        /// <param name="build">The tolerance.</param>
        private static GeoPolygon3 TryRing(List<GeoPoint3> corners, Tolerance build)
        {
            try
            {
                return new GeoPolygon3(corners, build);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the face across a loop, the loops inside it its holes; null where it is refused.
        /// </summary>
        /// <param name="boundary">The loop of the boundary, by index.</param>
        /// <param name="inner">The loops of its holes, in order; null where there are none.</param>
        /// <param name="rings">The polygon of each flat loop.</param>
        /// <param name="build">The tolerance the face is built within.</param>
        private static Patch TryPatch(int boundary, List<int> inner, GeoPolygon3[] rings, Tolerance build)
        {
            var holes = new List<GeoPolygon3>();
            var closes = new List<int> { boundary };

            if (inner != null)
            {
                foreach (int h in inner)
                {
                    holes.Add(rings[h]);
                    closes.Add(h);
                }
            }

            try
            {
                return new Patch(new[] { new GeoFace3(rings[boundary], holes, build) }, closes.ToArray());
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the tolerance a face filling a hole is built within: the one given, with three times its planar tolerance and
        /// as small an area as a piece of a surface may have.
        /// </summary>
        /// <param name="tolerance">The tolerance the body is closed within.</param>
        /// <remarks>
        /// A loop flat within the planar tolerance stands within it of the plane through the average of its corners; a
        /// polygon is measured from a corner of its own, and from a corner a hair one way, a corner a hair the other stands
        /// off by twice that, as a piece of a face cut in two does (see <see cref="LoopAssembly.ForFacePieces"/>). A gap too
        /// wide for the welds can be filled by a face as thin as the pieces a cut leaves (see
        /// <see cref="LoopAssembly.ForPieces"/>).
        /// </remarks>
        private static Tolerance ForFills(Tolerance tolerance) => LoopAssembly.ForFacePieces(LoopAssembly.ForPieces(tolerance));

        /// <summary>
        /// Determines whether none of the faces of some fills lies back to back with a face of the body.
        /// </summary>
        /// <param name="fills">The fills.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="near">A list to work in.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">Where a face lies back to back with one of the body; the origin where none does.</param>
        private static bool LieOnNoFace(List<Patch> fills, List<GeoFace3> faces, FaceBoxes boxes, List<int> near, Tolerance tolerance, out GeoPoint3 at)
        {
            foreach (Patch fill in fills)
            {
                foreach (GeoFace3 face in fill.Faces)
                {
                    if (BacksOnto(face, faces, boxes, near, tolerance, out at))
                    {
                        return false;
                    }
                }
            }

            at = GeoPoint3.Origin;
            return true;
        }

        /// <summary>
        /// Determines whether a face added lies back to back with a face of the body, as <see cref="HasSkin"/> reads two
        /// faces: facing opposite ways, a point well within the one on the other, or the middle of the other on the one.
        /// </summary>
        /// <param name="added">The face added.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="near">A list to work in.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">The point that lies on the other face; the origin where there is none.</param>
        /// <remarks>
        /// The point of the face added is one well within it (see <see cref="TryGetPointOn"/>): the middle of a face with a
        /// hole can lie in the hole.
        /// </remarks>
        private static bool BacksOnto(GeoFace3 added, List<GeoFace3> faces, FaceBoxes boxes, List<int> near, Tolerance tolerance, out GeoPoint3 at)
        {
            GeoAabb3 box = added.GetAabb();
            GeoPoint3 point = TryGetPointOn(added, tolerance, out GeoPoint3 inside) ? inside : added.Centroid;
            GeoVector3 normal = added.Normal;
            boxes.Meeting(box, near);

            foreach (int g in near)
            {
                GeoFace3 other = faces[g];

                if (!(normal.DotProduct(other.Normal) < Facing))
                {
                    continue;
                }

                if (other.GetAabb().Contains(point, tolerance) && other.Locate(point, tolerance) != PointLocation.OutSide)
                {
                    at = point;
                    return true;
                }

                GeoPoint3 middle = other.Centroid;

                if (box.Contains(middle, tolerance) && added.Locate(middle, tolerance) != PointLocation.OutSide)
                {
                    at = middle;
                    return true;
                }
            }

            at = GeoPoint3.Origin;
            return false;
        }

        /// <summary>
        /// Determines whether the walls between the two ends of a hole through a body close it, every other hole capped as it
        /// would be: closed and wound alike, enclosing a volume, whichever way it faces, which is read again after.
        /// </summary>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="holes">The holes.</param>
        /// <param name="walled">The hole walled, by index.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool WallsClose(List<GeoFace3> faces, List<Hole> holes, int walled, Tolerance tolerance)
        {
            var all = new List<GeoFace3>(faces);

            for (int h = 0; h < holes.Count; h++)
            {
                if (h == walled)
                {
                    all.AddRange(holes[h].Walls.Faces);
                    continue;
                }

                foreach (Patch cap in holes[h].Caps)
                {
                    all.AddRange(cap.Faces);
                }
            }

            if (all.Count < 4)
            {
                return false;
            }

            SolidValidation3 check = new GeoSolid3(all).Validate(tolerance);

            if (!check.IsClosed || !check.IsWoundAlike)
            {
                return false;
            }

            foreach (SolidIssue3 issue in check.Issues)
            {
                if (issue.Kind == SolidIssueKind.NoVolume)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A point well within the first face of a fill, or its middle where it breaks into no triangle.
        /// </summary>
        /// <param name="fill">The fill.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static GeoPoint3 PointOn(Patch fill, Tolerance tolerance)
            => TryGetPointOn(fill.Faces[0], tolerance, out GeoPoint3 point) ? point : fill.Faces[0].Centroid;

        /// <summary>
        /// The area some fills add.
        /// </summary>
        /// <param name="fills">The fills.</param>
        private static double AreaOf(List<Patch> fills)
        {
            double area = 0.0;

            foreach (Patch fill in fills)
            {
                area += fill.Area;
            }

            return area;
        }

        /// <summary>
        /// Turns over each shell of the body filled that takes a fill and faces the wrong way for where it lies: outwards
        /// inside an even number of other shells, inwards inside an odd number, as the volume it encloses closed says; its
        /// fills with it, and the faces given turned again reported so.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="all">The faces of the body filled, turned over in place where their shell is.</param>
        /// <param name="given">How many of them the welding left, before the faces added.</param>
        /// <param name="origins">For each of those, the face given it comes of, by index.</param>
        /// <param name="taken">The fills, in order, their faces after those the welding left.</param>
        /// <param name="loops">The loops filled.</param>
        /// <param name="shells">The shells of the faces the welding left.</param>
        /// <remarks>
        /// <para>
        /// A shell open by a hole was turned by the volume it enclosed measured from the middle of its box, which for a
        /// shell missing much of itself says little; closed, it says which way the shell faces for certain. Five faces of a
        /// box wound alike into it, its top missing, enclose a volume from the middle the wrong way, and are turned before
        /// the top is filled; were they not, their sign would turn them here, the top with them. A fill joins the shells
        /// of the loops it closes, and only a shell that takes one is read again: one closed already was read closed.
        /// </para>
        /// <para>
        /// A face given turned again is back as it was given where the turning had turned it, which is then no change, and
        /// turned where it had not; see <see cref="TurnAgain"/>.
        /// </para>
        /// </remarks>
        private static void FaceShellsOutwards(Work work, List<GeoFace3> all, int given, List<int> origins, List<Patch> taken, List<Loop> loops, FaceShells shells)
        {
            Tolerance tolerance = work.Tolerance;
            var parent = (int[])shells.Parent.Clone();
            var joins = new int[taken.Count];

            // Each fill joins the shells of the loops it closes.
            for (int p = 0; p < taken.Count; p++)
            {
                joins[p] = -1;

                foreach (int l in taken[p].Loops)
                {
                    int face = shells.FaceOf(loops[l]);

                    if (face < 0)
                    {
                        continue;
                    }

                    if (joins[p] < 0)
                    {
                        joins[p] = face;
                    }
                    else
                    {
                        Union(parent, joins[p], face);
                    }
                }
            }

            // A shell by the lowest face given of it, or past the faces for a fill joining none.
            var shellOf = new int[all.Count];

            for (int k = 0; k < given; k++)
            {
                shellOf[k] = Find(parent, k);
            }

            for (int p = 0, k = given; p < taken.Count; p++)
            {
                int shell = joins[p] >= 0 ? Find(parent, joins[p]) : given + p;

                foreach (GeoFace3 face in taken[p].Faces)
                {
                    shellOf[k++] = shell;
                }
            }

            var members = new Dictionary<int, List<int>>();
            var order = new List<int>();

            for (int k = 0; k < all.Count; k++)
            {
                if (!members.TryGetValue(shellOf[k], out List<int> of))
                {
                    of = new List<int>();
                    members.Add(shellOf[k], of);
                    order.Add(shellOf[k]);
                }

                of.Add(k);
            }

            var faces = new List<GeoFace3>[order.Count];
            var boxes = new GeoAabb3[order.Count];

            for (int s = 0; s < order.Count; s++)
            {
                faces[s] = new List<GeoFace3>(members[order[s]].Count);

                foreach (int k in members[order[s]])
                {
                    faces[s].Add(all[k]);
                }

                boxes[s] = BoxOf(faces[s]);
            }

            var again = new List<int>();

            for (int s = 0; s < order.Count; s++)
            {
                List<int> of = members[order[s]];

                if (of[of.Count - 1] < given)
                {
                    continue;
                }

                double volume = SignedVolumeOf(faces[s]);
                double area = 0.0;

                foreach (GeoFace3 face in faces[s])
                {
                    area += face.Area;
                }

                // As the check reads a body enclosing nothing: no thicker than the tolerance on average.
                if (!(Math.Abs(volume) > tolerance.EqualPoint * area))
                {
                    continue;
                }

                int depth = 0;

                for (int t = 0; t < order.Count; t++)
                {
                    if (t != s && boxes[t].Contains(boxes[s], tolerance) && Holds(faces[t], faces[s], tolerance))
                    {
                        depth++;
                    }
                }

                if ((volume > 0.0) == (depth % 2 == 0))
                {
                    continue;
                }

                foreach (int k in of)
                {
                    all[k] = all[k].Flip();

                    if (k < given && !again.Contains(origins[k]))
                    {
                        again.Add(origins[k]);
                    }
                }
            }

            again.Sort();

            foreach (int face in again)
            {
                TurnAgain(work, face);
            }
        }

        /// <summary>
        /// A hole to fill: the faces across it, one or the two at the ends of a hole through the body, and the walls between
        /// those two that would close it instead; or why it cannot be filled, and where.
        /// </summary>
        private sealed class Hole
        {
            /// <summary>
            /// Begins a hole.
            /// </summary>
            /// <param name="first">The first of its loops, by index.</param>
            internal Hole(int first)
            {
                First = first;
            }

            /// <summary>Gets the first of its loops, by index: the holes are filled in this order.</summary>
            internal int First { get; }

            /// <summary>Gets the faces across it: one, or one at each end of a hole through the body.</summary>
            internal List<Patch> Caps { get; } = new List<Patch>();

            /// <summary>Gets or sets the walls between the two ends of a hole through the body; null where it is no such hole.</summary>
            internal Patch Walls { get; set; }

            /// <summary>Gets why it cannot be filled; none where it can be read.</summary>
            internal ClosingFailure Failure { get; private set; }

            /// <summary>Gets a point at that trouble.</summary>
            internal GeoPoint3 FailureAt { get; private set; }

            /// <summary>
            /// A loop that cannot be read as a hole to fill, and why.
            /// </summary>
            /// <param name="loop">The loop, by index.</param>
            /// <param name="failure">Why.</param>
            /// <param name="at">A point at the trouble.</param>
            internal static Hole Unread(int loop, ClosingFailure failure, GeoPoint3 at)
            {
                var hole = new Hole(loop);
                hole.Fail(failure, at);
                return hole;
            }

            /// <summary>
            /// Notes why it cannot be filled, and where.
            /// </summary>
            /// <param name="failure">Why.</param>
            /// <param name="at">A point at the trouble.</param>
            internal void Fail(ClosingFailure failure, GeoPoint3 at)
            {
                Failure = failure;
                FailureAt = at;
            }
        }

        /// <summary>
        /// The faces added across one hole, one change, and the loops they close.
        /// </summary>
        private sealed class Patch
        {
            /// <summary>
            /// Holds the faces added across a hole.
            /// </summary>
            /// <param name="faces">The faces.</param>
            /// <param name="loops">The loops they close, by index.</param>
            internal Patch(GeoFace3[] faces, int[] loops)
            {
                Faces = faces;
                Loops = loops;

                foreach (GeoFace3 face in faces)
                {
                    Area += face.Area;
                }
            }

            /// <summary>Gets the faces.</summary>
            internal GeoFace3[] Faces { get; }

            /// <summary>Gets the loops they close, by index.</summary>
            internal int[] Loops { get; }

            /// <summary>Gets the area they add.</summary>
            internal double Area { get; }

            /// <summary>
            /// Gets the middle of the faces, where they would balance: the centroid of the one face, or of all weighed by
            /// their areas.
            /// </summary>
            internal GeoPoint3 Middle
            {
                get
                {
                    if (Faces.Length == 1 || !(Area > 0.0))
                    {
                        return Faces[0].Centroid;
                    }

                    double x = 0.0, y = 0.0, z = 0.0;

                    foreach (GeoFace3 face in Faces)
                    {
                        GeoPoint3 centroid = face.Centroid;
                        x += centroid.X * face.Area;
                        y += centroid.Y * face.Area;
                        z += centroid.Z * face.Area;
                    }

                    return new GeoPoint3(x / Area, y / Area, z / Area);
                }
            }
        }

        /// <summary>
        /// The shells of a body's faces, the faces joined by stretches two of them or more run, read when first asked for;
        /// and the face each corner is a corner of.
        /// </summary>
        private sealed class FaceShells
        {
            private readonly List<GeoFace3> _faces;
            private readonly Tolerance _tolerance;
            private int[] _parent;
            private Dictionary<GeoPoint3, int> _faceOf;

            /// <summary>
            /// Begins reading the shells of faces.
            /// </summary>
            /// <param name="faces">The faces.</param>
            /// <param name="tolerance">The tolerance their edges are matched within.</param>
            internal FaceShells(List<GeoFace3> faces, Tolerance tolerance)
            {
                _faces = faces;
                _tolerance = tolerance;
            }

            /// <summary>
            /// Gets, for each face, the parent of the sets the faces of one shell make, as <see cref="Find"/> reads them.
            /// </summary>
            internal int[] Parent
            {
                get
                {
                    if (_parent == null)
                    {
                        var parent = new int[_faces.Count];

                        for (int f = 0; f < parent.Length; f++)
                        {
                            parent[f] = f;
                        }

                        foreach (Stretch stretch in FindStretches(_faces, new bool[_faces.Count], _tolerance))
                        {
                            for (int r = 1; r < stretch.Faces.Length; r++)
                            {
                                Union(parent, stretch.Faces[0], stretch.Faces[r]);
                            }
                        }

                        _parent = parent;
                    }

                    return _parent;
                }
            }

            /// <summary>
            /// The face the first corner of a loop is a corner of, the first by index where it is one of more; below nought
            /// where none is.
            /// </summary>
            /// <param name="loop">The loop.</param>
            /// <remarks>
            /// The corners of a loop are the corners of the faces whose edges it runs along, as they are, so they are found as
            /// they are.
            /// </remarks>
            internal int FaceOf(Loop loop)
            {
                if (_faceOf == null)
                {
                    _faceOf = new Dictionary<GeoPoint3, int>();

                    for (int f = 0; f < _faces.Count; f++)
                    {
                        Add(_faces[f].Boundary, f);

                        foreach (GeoPolygon3 hole in _faces[f].Holes)
                        {
                            Add(hole, f);
                        }
                    }
                }

                return _faceOf.TryGetValue(loop.Corners[0], out int face) ? face : -1;
            }

            /// <summary>
            /// Determines whether two loops are left open in one shell.
            /// </summary>
            /// <param name="one">The one loop.</param>
            /// <param name="other">The other.</param>
            internal bool SameShell(Loop one, Loop other)
            {
                int a = FaceOf(one);
                int b = FaceOf(other);
                return a >= 0 && b >= 0 && Find(Parent, a) == Find(Parent, b);
            }

            /// <summary>
            /// Notes the face of each corner of a ring not noted already.
            /// </summary>
            /// <param name="ring">The ring.</param>
            /// <param name="face">Its face, by index.</param>
            private void Add(GeoPolygon3 ring, int face)
            {
                foreach (GeoPoint3 corner in ring.Vertices)
                {
                    if (!_faceOf.ContainsKey(corner))
                    {
                        _faceOf.Add(corner, face);
                    }
                }
            }
        }
    }
}
