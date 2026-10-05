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
        /// open, would be filled by itself turned over. Nor is one crossing a face of the body as filled so far, a face it was
        /// given or one added across a hole before, an edge of either passing through the inside of the other, which reads
        /// valid as well (see <see cref="CrossesBody"/>). Where a hole has one way of filling it lying on no face of the body
        /// and crossing none, that way is taken, whatever the strategy: the caps of a hole through a plate whose walls are
        /// missing, a post standing in the hole through the plane of a cap, are no way, and the walls round the post are the
        /// one. Where it has two, as the two ends of that hole have with nothing in it, capped or walled,
        /// <see cref="FillStrategy.WhenUnambiguous"/> takes neither and the report says
        /// <see cref="ClosingFailure.HoleAmbiguous"/>, and <see cref="FillStrategy.MinArea"/> takes the one adding the least
        /// area, the caps where the two add as much. Where no way may be taken, the hole is left open,
        /// <see cref="ClosingFailure.StillOpen"/> at a point where its face or a cap lies on a face of the body, or where an
        /// edge passes through one.
        /// </para>
        /// <para>
        /// A hole out of flat is filled by the triangles of least area across it, of the ways lying on no face of the body,
        /// as <see cref="TryTriangulate"/> says. Only the triangles taken are set against crossing the body as filled so far,
        /// not the three million or so the ways across a loop of 256 corners are made of, so that where they cross it the hole
        /// is left open, <see cref="ClosingFailure.StillOpen"/>, rather than filled another way: of two boxes over one square,
        /// open towards each other, their rims well out of flat, the triangles across the second rim would cross those across
        /// the first, and the second hole is left open where they do. The faces taken across each hole are held to
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

                List<Patch> fill;

                if (hole.Warped >= 0)
                {
                    // Out of flat: the triangles of least area across it, lying on no face of the body, and crossing none.
                    if (!TryTriangulate(loops[hole.Warped], hole.Warped, faces, boxes, options, out Patch triangles, out ClosingFailure failure, out GeoPoint3 at))
                    {
                        work.Refuse(failure, at);
                        return false;
                    }

                    fill = new List<Patch> { triangles };

                    if (CrossesBody(fill, faces, boxes, taken, near, tolerance, out GeoPoint3 crossing))
                    {
                        work.Refuse(ClosingFailure.StillOpen, crossing);
                        return false;
                    }
                }
                else if (!TryChoose(work, faces, holes, h, boxes, taken, near, out fill))
                {
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
        /// Chooses the faces a flat hole is filled by: its face, or for the two ends of a hole through the body, the caps or
        /// the walls, as the strategy says where both may be taken, lying on no face of the body and crossing none of it as
        /// filled so far, the walls closing it; false, the trouble noted where the face or the caps meet it, where neither
        /// may be.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="holes">The holes.</param>
        /// <param name="h">The hole, by index.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="taken">The fills taken before, of the holes before it.</param>
        /// <param name="near">A list to work in.</param>
        /// <param name="fill">The fills taken; null when the method returns false.</param>
        private static bool TryChoose(Work work, List<GeoFace3> faces, List<Hole> holes, int h, FaceBoxes boxes, List<Patch> taken, List<int> near, out List<Patch> fill)
        {
            Tolerance tolerance = work.Tolerance;
            Hole hole = holes[h];
            fill = null;

            bool caps = LieOnNoFace(hole.Caps, faces, boxes, near, tolerance, out GeoPoint3 trouble)
                && !CrossesBody(hole.Caps, faces, boxes, taken, near, tolerance, out trouble);
            List<Patch> walled = hole.Walls != null ? new List<Patch> { hole.Walls } : null;
            bool walls = walled != null
                && LieOnNoFace(walled, faces, boxes, near, tolerance, out _)
                && !CrossesBody(walled, faces, boxes, taken, near, tolerance, out _)
                && WallsClose(faces, holes, h, tolerance);

            if (caps && walls)
            {
                if (work.Options.Fill == FillStrategy.WhenUnambiguous)
                {
                    work.Refuse(ClosingFailure.HoleAmbiguous, PointOn(hole.Caps[0], tolerance));
                    return false;
                }

                fill = hole.Walls.Area < AreaOf(hole.Caps) ? walled : hole.Caps;
                return true;
            }

            if (caps || walls)
            {
                fill = caps ? hole.Caps : walled;
                return true;
            }

            work.Refuse(ClosingFailure.StillOpen, trouble);
            return false;
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
        /// as the holes of that face; two such faces of no holes at the two ends of a hole through a shell can be walls
        /// between them instead; and a loop out of flat by no more than the options allow, with no loop inside it, is filled
        /// by triangles.
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
        /// Two faces of no holes on loops of one shell are the two ends of a hole through it whose walls are missing where flat
        /// walls run between them, each from an edge of the one to an edge of the other (see <see cref="WallsBetween"/>): the
        /// faces cap it, and the walls close it round the hole instead, straight through it, slanting or tapering. A box
        /// missing its top and bottom has two such loops, and the walls there lie on its sides; a plate missing the walls of a
        /// hole through it has two, and the walls lie on nothing; a channel pierced through its top and its bottom has two,
        /// and the walls cross its plates on their way, which is no way of closing it either (see <see cref="CrossesBody"/>).
        /// </para>
        /// <para>
        /// A loop further out of flat than the planar tolerance and no further than
        /// <see cref="SolidClosingOptions.MaxOffFlat"/> is filled by triangles on its corners (see <see cref="TryTriangulate"/>),
        /// and one further out is <see cref="ClosingFailure.HoleOffFlat"/>. Triangles are found across a loop of no more than
        /// 256 corners, and one out of flat with more is <see cref="ClosingFailure.HoleTooLarge"/>, at its corner furthest
        /// off flat. They are found across a loop with nothing inside it only: a loop out of flat holding another, or a flat
        /// one holding a loop out of flat, which its face cannot take as a hole, is <see cref="ClosingFailure.HoleAmbiguous"/>
        /// whatever the strategy, at the middle of the loop holding it. Where either loop is out of flat, one lies in the
        /// other's plane within the planar tolerance and as far again as a hole may stand out of flat. A loop out of flat
        /// held by a hole of a flat face is an island, and filled by triangles of its own. A loop enclosing no area, or one
        /// whose face cannot be built, leaves the body still open.
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
            var warped = new bool[count];
            var holes = new List<Hole>();

            for (int i = 0; i < count; i++)
            {
                Loop loop = loops[i];
                ClosingFailure failure = ClosingFailure.None;
                GeoPoint3 at = loop.Middle;

                if (!(loop.Area > 0.0))
                {
                    failure = ClosingFailure.StillOpen;
                }
                else if (loop.OffFlat <= tolerance.EqualPlanar)
                {
                    rings[i] = TryRing(loop.Corners, build);
                    failure = rings[i] == null ? ClosingFailure.StillOpen : ClosingFailure.None;
                }
                else if (loop.OffFlat > options.MaxOffFlat)
                {
                    failure = ClosingFailure.HoleOffFlat;
                }
                else if (loop.Corners.Count > MostCornersOutOfFlat)
                {
                    failure = ClosingFailure.HoleTooLarge;
                    at = FurthestOff(loop);
                }
                else
                {
                    // Read square to its plane, as the triangles across it will be, on its corners as they are.
                    warped[i] = true;
                    rings[i] = GeoPolygon3.FromValidated(loop.Corners.ToArray(), loop.Normal, loop.Area);
                }

                if (failure != ClosingFailure.None)
                {
                    holes.Add(Hole.Unread(i, failure, at));
                    continue;
                }

                planes[i] = new GeoPlane3(loop.Middle, loop.Normal);
                boxes[i] = GeoAabb3.FromPoints(loop.Corners);
            }

            int[] holder = Holders(loops, rings, planes, boxes, warped, tolerance.EqualPlanar + options.MaxOffFlat, tolerance);

            // Each loop read the boundary of a face or a hole of one, the larger first, as the loops holding them are.
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
            var holdsAcross = new bool[count];

            foreach (int i in order)
            {
                int h = holder[i];

                // Triangles are found across a loop with nothing inside it, and a face takes no hole out of flat.
                if (h >= 0 && (warped[h] || (warped[i] && boundary[h])))
                {
                    holdsAcross[h] = true;
                    boundary[i] = true;
                    continue;
                }

                boundary[i] = h < 0 || !boundary[h] || warped[i] || loops[i].Normal.DotProduct(loops[h].Normal) >= 0.0;

                if (!boundary[i])
                {
                    (inner[h] ?? (inner[h] = new List<int>())).Add(i);
                }
            }

            var patches = new Patch[count];

            for (int i = 0; i < count; i++)
            {
                if (boundary[i] && !warped[i] && !holdsAcross[i])
                {
                    inner[i]?.Sort();
                    patches[i] = TryPatch(i, inner[i], rings, build);
                }
            }

            List<GeoPoint3[]>[] walls = PairEnds(loops, patches, inner, tolerance, shells, out int[] other);

            for (int i = 0; i < count; i++)
            {
                // The other end of a hole through the body goes with the first.
                if (!boundary[i] || (other[i] >= 0 && other[i] < i))
                {
                    continue;
                }

                var hole = new Hole(First(i, inner[i], other[i]));

                if (holdsAcross[i])
                {
                    hole.Fail(ClosingFailure.HoleAmbiguous, loops[i].Middle);
                }
                else if (warped[i])
                {
                    hole.Warped = i;
                }
                else if (patches[i] == null)
                {
                    hole.Fail(ClosingFailure.StillOpen, loops[i].Middle);
                }
                else
                {
                    hole.Caps.Add(patches[i]);

                    if (other[i] >= 0)
                    {
                        hole.Caps.Add(patches[other[i]]);
                        hole.Walls = TryWalls(walls[i], i, other[i], build);
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
        /// For each loop read, the smallest loop read holding it, in whose plane it lies with a corner of it inside; below
        /// nought where none does, and for the loops not read.
        /// </summary>
        /// <param name="loops">The loops.</param>
        /// <param name="rings">The polygon of each loop read, flat or out of flat; null for the others.</param>
        /// <param name="planes">The plane of each loop read.</param>
        /// <param name="boxes">The box of each loop read.</param>
        /// <param name="warped">For each loop, whether it is out of flat.</param>
        /// <param name="band">How far off the plane of a loop another may stand and lie in it, where either is out of flat:
        /// the planar tolerance and as far again as a hole may stand out of flat.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The loops are filed by their boxes, and each is set only against those whose boxes hold its own. Two flat loops
        /// lie in one plane within the planar tolerance, as a face takes its holes.
        /// </remarks>
        private static int[] Holders(List<Loop> loops, GeoPolygon3[] rings, GeoPlane3[] planes, GeoAabb3[] boxes, bool[] warped, double band, Tolerance tolerance)
        {
            int count = loops.Count;
            var holder = new int[count];
            var skip = new bool[count];
            bool anyWarped = false;

            for (int i = 0; i < count; i++)
            {
                holder[i] = -1;
                skip[i] = rings[i] == null;
                anyWarped |= warped[i];
            }

            var filed = new FaceBoxes(boxes, skip, tolerance);
            var near = new List<int>();

            for (int b = 0; b < count; b++)
            {
                if (rings[b] == null)
                {
                    continue;
                }

                filed.Meeting(anyWarped ? boxes[b].Expand(band) : boxes[b], near);

                foreach (int a in near)
                {
                    if (a == b || !(loops[a].Area > loops[b].Area))
                    {
                        continue;
                    }

                    bool across = warped[a] || warped[b];
                    double off = across ? band : tolerance.EqualPlanar;

                    if (!Holds(boxes[a], boxes[b], across ? Math.Max(band, tolerance.EqualPoint) : tolerance.EqualPoint))
                    {
                        continue;
                    }

                    // The smallest holding it, the first by index of two as large.
                    if (holder[b] >= 0 && !(loops[a].Area < loops[holder[b]].Area))
                    {
                        continue;
                    }

                    if (LiesWithin(loops[b], planes[a], off) && IsInside(loops[b], rings[a], planes[a], tolerance))
                    {
                        holder[b] = a;
                    }
                }
            }

            return holder;
        }

        /// <summary>
        /// Determines whether one box holds another, each side of it within a margin.
        /// </summary>
        /// <param name="outer">The box holding.</param>
        /// <param name="inner">The box held.</param>
        /// <param name="margin">The margin.</param>
        private static bool Holds(GeoAabb3 outer, GeoAabb3 inner, double margin)
        {
            return inner.Min.X >= outer.Min.X - margin && inner.Max.X <= outer.Max.X + margin
                && inner.Min.Y >= outer.Min.Y - margin && inner.Max.Y <= outer.Max.Y + margin
                && inner.Min.Z >= outer.Min.Z - margin && inner.Max.Z <= outer.Max.Z + margin;
        }

        /// <summary>
        /// Determines whether every corner of a loop lies within a distance of a plane, either side.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="distance">The distance.</param>
        private static bool LiesWithin(Loop loop, GeoPlane3 plane, double distance)
        {
            foreach (GeoPoint3 corner in loop.Corners)
            {
                if (Math.Abs(plane.SignedDistanceTo(corner)) > distance)
                {
                    return false;
                }
            }

            return true;
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
        /// Pairs the faces of no holes at the two ends of a hole through a shell, each, in the order of the loops, with the
        /// first such face not paired already: faces on loops of one shell, with walls between them; see
        /// <see cref="WallsBetween"/>.
        /// </summary>
        /// <param name="loops">The loops.</param>
        /// <param name="patches">The face built on each loop that is the boundary of one; null for the others.</param>
        /// <param name="inner">The holes of each such face, by loop; null where there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="shells">The shells of the faces the loops are left open in.</param>
        /// <param name="other">For each loop, the loop at the other end of its hole; below nought where there is none.</param>
        /// <returns>For each loop paired, the corners of the walls between it and the loop at the other end, each wall on four;
        /// null for the others.</returns>
        private static List<GeoPoint3[]>[] PairEnds(List<Loop> loops, Patch[] patches, List<int>[] inner, Tolerance tolerance, FaceShells shells, out int[] other)
        {
            int count = loops.Count;
            other = new int[count];
            var walls = new List<GeoPoint3[]>[count];
            var caps = new List<int>();

            for (int i = 0; i < count; i++)
            {
                other[i] = -1;

                if (patches[i] != null && inner[i] == null)
                {
                    caps.Add(i);
                }
            }

            foreach (int i in caps)
            {
                if (other[i] >= 0)
                {
                    continue;
                }

                foreach (int j in caps)
                {
                    if (j == i || other[j] >= 0)
                    {
                        continue;
                    }

                    List<GeoPoint3[]> between = WallsBetween(loops[i], loops[j], tolerance);

                    if (between != null && shells.SameShell(loops[i], loops[j]))
                    {
                        other[i] = j;
                        other[j] = i;
                        walls[i] = between;
                        walls[j] = between;
                        break;
                    }
                }
            }

            return walls;
        }

        /// <summary>
        /// The walls between two loops that are the two ends of a hole through a body: each wall on four of their corners,
        /// from an edge of the one to the edge of the other matched with it; null where they are no such ends.
        /// </summary>
        /// <param name="one">The one loop.</param>
        /// <param name="other">The other.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// <para>
        /// The two are such ends where they turn at as many corners, three or more, a corner a rim runs straight on through,
        /// within the point tolerance of the line between its neighbours, not counted; where the faces across them face
        /// opposite ways, and away from each other, the middle of each further than the point tolerance behind the other's
        /// face; and where corner c of the one, matched with corner s less c of the other for some s, makes every wall flat
        /// within the planar tolerance and convex. Of several such s, the one whose walls add the least area is taken.
        /// </para>
        /// <para>
        /// So the walls of a hole slanting through a plate are parallelograms, and those of one tapering through it trapezoids,
        /// whichever corner each rim is listed from. Two holes in the faces of a channel's arms facing each other across its
        /// gap face each other, not away, and are no ends of one hole: walls between them would lay a bar across the gap.
        /// </para>
        /// </remarks>
        private static List<GeoPoint3[]> WallsBetween(Loop one, Loop other, Tolerance tolerance)
        {
            double reach = tolerance.EqualPoint;

            if (!(one.Normal.DotProduct(other.Normal) < Facing))
            {
                return null;
            }

            GeoVector3 move = one.Middle.GetVectorTo(other.Middle);

            if (!(move.DotProduct(one.Normal) < -reach) || !(move.DotProduct(other.Normal) > reach))
            {
                return null;
            }

            List<GeoPoint3> a = Turning(one.Corners, reach);
            List<GeoPoint3> b = Turning(other.Corners, reach);
            int count = a.Count;

            if (count < 3 || b.Count != count)
            {
                return null;
            }

            List<GeoPoint3[]> best = null;
            double least = double.MaxValue;

            for (int shift = 0; shift < count; shift++)
            {
                var walls = new List<GeoPoint3[]>(count);
                double area = 0.0;
                bool fit = true;

                for (int c = 0; c < count && fit; c++)
                {
                    // Each wall runs the edge of each end the way a face closing it would.
                    var wall = new[] { a[c], a[(c + 1) % count], b[(((shift - c - 1) % count) + count) % count], b[(((shift - c) % count) + count) % count] };
                    fit = FlatAndConvex(wall, tolerance.EqualPlanar, out double wallArea);
                    area += wallArea;
                    walls.Add(wall);
                }

                if (fit && area < least)
                {
                    least = area;
                    best = walls;
                }
            }

            return best;
        }

        /// <summary>
        /// The corners of a loop where it turns: those it runs straight on through, within a reach of the line between their
        /// neighbours, left out, three at the least kept.
        /// </summary>
        /// <param name="corners">The corners, in order.</param>
        /// <param name="reach">The reach.</param>
        private static List<GeoPoint3> Turning(List<GeoPoint3> corners, double reach)
        {
            var kept = new List<GeoPoint3>(corners);
            bool dropped = true;

            while (dropped && kept.Count > 3)
            {
                dropped = false;

                for (int i = 0; i < kept.Count; i++)
                {
                    GeoPoint3 before = kept[(i + kept.Count - 1) % kept.Count];
                    GeoPoint3 after = kept[(i + 1) % kept.Count];

                    if (kept[i].DistanceTo(NearestOnSegment(before, after, kept[i])) <= reach)
                    {
                        kept.RemoveAt(i);
                        dropped = true;
                        break;
                    }
                }
            }

            return kept;
        }

        /// <summary>
        /// Determines whether four corners lie flat, within a planar tolerance of the plane through their average square to
        /// their area, and turn the same way at each corner; and the area they enclose.
        /// </summary>
        /// <param name="quad">The corners, in order.</param>
        /// <param name="planar">The planar tolerance.</param>
        /// <param name="area">The area they enclose, by Newell's method.</param>
        private static bool FlatAndConvex(GeoPoint3[] quad, double planar, out double area)
        {
            double nx = 0.0, ny = 0.0, nz = 0.0, cx = 0.0, cy = 0.0, cz = 0.0;

            for (int i = 0; i < 4; i++)
            {
                GeoPoint3 p = quad[i], q = quad[(i + 1) % 4];
                nx += (p.Y - q.Y) * (p.Z + q.Z);
                ny += (p.Z - q.Z) * (p.X + q.X);
                nz += (p.X - q.X) * (p.Y + q.Y);
                cx += p.X / 4.0;
                cy += p.Y / 4.0;
                cz += p.Z / 4.0;
            }

            double length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
            area = length / 2.0;

            if (!(length > 0.0))
            {
                return false;
            }

            var normal = new GeoVector3(nx / length, ny / length, nz / length);
            var middle = new GeoPoint3(cx, cy, cz);

            for (int i = 0; i < 4; i++)
            {
                if (Math.Abs(middle.GetVectorTo(quad[i]).DotProduct(normal)) > planar)
                {
                    return false;
                }

                GeoVector3 ahead = quad[i].GetVectorTo(quad[(i + 1) % 4]);
                GeoVector3 next = quad[(i + 1) % 4].GetVectorTo(quad[(i + 2) % 4]);

                if (!(ahead.CrossProduct(next).DotProduct(normal) > 0.0))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Builds the walls between the two ends of a hole through a body on their corners as the body has them; null where
        /// one cannot be built.
        /// </summary>
        /// <param name="corners">The corners of each wall.</param>
        /// <param name="one">The loop at the one end, by index.</param>
        /// <param name="other">The loop at the other.</param>
        /// <param name="build">The tolerance the walls are built within.</param>
        private static Patch TryWalls(List<GeoPoint3[]> corners, int one, int other, Tolerance build)
        {
            var walls = new GeoFace3[corners.Count];

            try
            {
                for (int c = 0; c < corners.Count; c++)
                {
                    walls[c] = new GeoFace3(new GeoPolygon3(corners[c], build), null, build);
                }
            }
            catch (ArgumentException)
            {
                return null;
            }

            return new Patch(walls, new[] { one, other });
        }

        /// <summary>
        /// Determines whether a face of some fills crosses a face of the body as filled so far, a face it was given or one of
        /// a fill taken before: an edge of the one passing through the inside of the other, either way round (see
        /// <see cref="EdgesPierce"/>); each face added is set only against the faces whose boxes meet its own.
        /// </summary>
        /// <param name="fills">The fills.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="boxes">The faces filed by their boxes.</param>
        /// <param name="taken">The fills taken before, in the order they were taken.</param>
        /// <param name="near">A list to work in.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">The point where an edge passes through a face; the origin where none does.</param>
        /// <remarks>
        /// <para>
        /// Validation reads no face crossing another. A box 30 by 20 by 10 missing its top, a post 4 by 4 standing in it 5
        /// through the plane of the top, is closed by the top with every edge paired, valid and holding 6 208, the four walls
        /// of the post through it; walls through the gap of a channel, from a hole in its top to one in its bottom, close it
        /// as well, crossing the plates on their way. Neither is a way of closing the hole. A face of the body is set against
        /// a face added only where it stands across the face added's plane (see <see cref="StandsAcross"/>).
        /// </para>
        /// <para>
        /// Two flat fills cannot cross unseen: every edge of a flat face or of a cap is an edge of a face given as well, and is
        /// set against the other fill with it. The diagonals of triangles and the edges of the walls between the two ends of a
        /// hole are edges of no face given, and two fills can cross by them, each crossing nothing given: two boxes over one
        /// square 6 by 6, open towards each other, their rims about 1.8 out of flat, are filled the lower by triangles rising
        /// to 16 across the middle and the upper by triangles dipping to 14.5 there, and the two closed would read valid, 861.
        /// So each fill is set against the fills taken before it as well, in the order the holes are filled, each face by its
        /// box: a body has few holes.
        /// </para>
        /// </remarks>
        private static bool CrossesBody(List<Patch> fills, List<GeoFace3> faces, FaceBoxes boxes, List<Patch> taken, List<int> near, Tolerance tolerance, out GeoPoint3 at)
        {
            foreach (Patch fill in fills)
            {
                foreach (GeoFace3 added in fill.Faces)
                {
                    GeoAabb3 box = added.GetAabb();
                    boxes.Meeting(box, near);

                    foreach (int g in near)
                    {
                        if (Crosses(faces[g], added, tolerance, out at))
                        {
                            return true;
                        }
                    }

                    foreach (Patch before in taken)
                    {
                        foreach (GeoFace3 face in before.Faces)
                        {
                            if (face.GetAabb().CollidesWith(box, tolerance) && Crosses(face, added, tolerance, out at))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            at = GeoPoint3.Origin;
            return false;
        }

        /// <summary>
        /// Determines whether a face of the body, as filled so far, and a face added cross: the one standing across the plane
        /// of the other, and an edge of either passing through the inside of the other.
        /// </summary>
        /// <param name="face">The face of the body.</param>
        /// <param name="added">The face added.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">The point where an edge passes through a face; the origin where none does.</param>
        private static bool Crosses(GeoFace3 face, GeoFace3 added, Tolerance tolerance, out GeoPoint3 at)
        {
            at = GeoPoint3.Origin;
            return StandsAcross(face, added, tolerance)
                && (EdgesPierce(face, added, tolerance, out at) || EdgesPierce(added, face, tolerance, out at));
        }

        /// <summary>
        /// Determines whether an edge of one face passes through the inside of another: its ends further than the planar
        /// tolerance either side of the other's plane, the point where it meets the plane inside the other, and neither end
        /// on the other's edges.
        /// </summary>
        /// <param name="edges">The face whose edges are looked at.</param>
        /// <param name="face">The face they may pass through.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">The point where an edge passes through the face; the origin where none does.</param>
        /// <remarks>
        /// A face touched at an edge or a corner is not crossed: an edge lying in its plane, or one ending in it, has no end
        /// further off it than the planar tolerance. Nor is one an edge runs from, out of a corner of it or a point on its
        /// edges: a straight edge meets the plane of a flat face once, where it touches. The plane is read through the face's
        /// first corner, from which a corner of a face a hair out of flat can stand twice the planar tolerance off: a hopper
        /// missing its top, a corner of its rim lifted 0.0036 and the rim flat within the tolerance, has the edges of its
        /// walls sloping in under the hole from such a corner meet the plane of the top 0.002 in from both its sides, where
        /// they only touch it.
        /// </remarks>
        private static bool EdgesPierce(GeoFace3 edges, GeoFace3 face, Tolerance tolerance, out GeoPoint3 at)
        {
            GeoPoint3 origin = face.Boundary[0];
            GeoVector3 normal = face.Normal;
            double planar = tolerance.EqualPlanar;
            var rings = new List<GeoPolygon3> { edges.Boundary };
            rings.AddRange(edges.Holes);

            foreach (GeoPolygon3 ring in rings)
            {
                int count = ring.VertexCount;

                for (int i = 0; i < count; i++)
                {
                    GeoPoint3 p = ring[i], q = ring[(i + 1) % count];
                    double dp = origin.GetVectorTo(p).DotProduct(normal);
                    double dq = origin.GetVectorTo(q).DotProduct(normal);

                    if (!((dp > planar && dq < -planar) || (dp < -planar && dq > planar)))
                    {
                        continue;
                    }

                    GeoPoint3 crossing = p.Add(p.GetVectorTo(q).Multiply(dp / (dp - dq)));

                    if (face.Locate(crossing, tolerance) == PointLocation.Inside && !OnEdgesOf(face, p, tolerance) && !OnEdgesOf(face, q, tolerance))
                    {
                        at = crossing;
                        return true;
                    }
                }
            }

            at = GeoPoint3.Origin;
            return false;
        }

        /// <summary>
        /// Determines whether a face stands across the plane of another, corners of its boundary further than the planar
        /// tolerance off that plane on either side.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="other">The other, whose plane is read through its first corner, as <see cref="EdgesPierce"/> reads it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A face lying to one side of the plane of a face added, or in it, crosses it neither way: no edge of it reaches
        /// through the plane, and an edge of the face added, lying in the plane, meets it at its edges at most. So of a top
        /// filling a prism of a thousand sides, only the four corners of each wall standing under it are read, and not its own
        /// thousand edges against every wall, which added 19 ms to the 41 the prism took to close under .NET Framework 4.8.
        /// </remarks>
        private static bool StandsAcross(GeoFace3 face, GeoFace3 other, Tolerance tolerance)
        {
            GeoPoint3 origin = other.Boundary[0];
            GeoVector3 normal = other.Normal;
            double planar = tolerance.EqualPlanar;
            bool above = false, below = false;

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                double off = origin.GetVectorTo(corner).DotProduct(normal);
                above |= off > planar;
                below |= off < -planar;

                if (above && below)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a point lies on an edge of a face, of its boundary or of a hole, within the point tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool OnEdgesOf(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            var rings = new List<GeoPolygon3> { face.Boundary };
            rings.AddRange(face.Holes);

            foreach (GeoPolygon3 ring in rings)
            {
                int count = ring.VertexCount;

                for (int i = 0; i < count; i++)
                {
                    if (point.DistanceTo(NearestOnSegment(ring[i], ring[(i + 1) % count], point)) <= tolerance.EqualPoint)
                    {
                        return true;
                    }
                }
            }

            return false;
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
        /// Turns over each outermost shell of the body filled that encloses its volume inwards, now that it is closed, and
        /// every shell inside it with it, their fills with them, the faces given turned again reported so; see
        /// <see cref="TurnedOutwards"/>.
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
        /// of the loops it closes. Every shell is read again, as the turning reads a closed shell: one closed already was
        /// turned then where it was to be, and one inside a shell open then was left as it was, its sign waiting for the
        /// closing, so that a cavity in a box missing its top stays a cavity.
        /// </para>
        /// <para>
        /// A face given turned again is back as it was given where the turning had turned it, which is then no change, and
        /// turned where it had not; see <see cref="TurnAgain(Work, int)"/>.
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

            var shellFaces = new List<List<int>>(order.Count);
            var closed = new bool[order.Count];

            for (int s = 0; s < order.Count; s++)
            {
                shellFaces.Add(members[order[s]]);
                closed[s] = true;
            }

            // Every shell is closed now: an outermost one wound inwards is turned with every shell inside it, and the rest
            // keep their winding against the shell they lie in.
            bool[] whole = TurnedOutwards(all, shellFaces, null, closed, tolerance);
            var again = new List<int>();

            for (int s = 0; s < order.Count; s++)
            {
                if (!whole[s])
                {
                    continue;
                }

                foreach (int k in shellFaces[s])
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
        /// those two that would close it instead; or the loop out of flat it is, to be filled by triangles; or why it cannot
        /// be filled, and where.
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

            /// <summary>
            /// Gets or sets the loop out of flat the hole is, by index, to be filled by triangles across it; below nought where
            /// it is flat.
            /// </summary>
            internal int Warped { get; set; } = -1;

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
