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
    /// nothing, and one moved off them sweeps the triangle it makes with its neighbours on each, a neighbour the face's edge
    /// runs straight on past taken for the corner the face turns at further along. A slab's top ran out to a point 0.0095
    /// off the corner of the wall beside it, the end of a sliver along the wall's top edge; moved to the top's point, as the
    /// corner with the most face round it, the wall's corner tilted the wall, 21.8 metres long, and the slab came out
    /// 41 925 cubic millimetres short, where the point moved to the wall's corner along the top moves nothing. A
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
    /// <para>
    /// Where that does not close it, the corners of the edges still left unpaired, and those only, are taken together
    /// within twice the tolerance, and then four times, as far as a sliver is closed across (see
    /// <see cref="LoopAssembly.TryCloseSlivers"/>), the rest within the tolerance as before. The faces are built again
    /// within the tolerance itself, so that one a corner moved takes off flat comes as triangles on its corners, and what
    /// comes of it is kept on the same terms, its volume held within the wider reach times its area. A slab of a Tekla
    /// model, cut one after another by the parts it meets within a thousandth, had a face stepped by 0.00107 where two
    /// faces lying back to back across the step had been taken from each other, and the faces either side of the step met
    /// on copies of an edge 0.00107 apart: open within a thousandth, and closed welded within two. Of eight such cuts of
    /// slabs of two Tekla models that came out open, six close so; the two left open share slivers two to six thousandths
    /// thick with what cuts them, which a cut within a thousandth takes for material.
    /// </para>
    /// </remarks>
    internal static class Weld3
    {
        /// <summary>
        /// Gets a body a boolean made, open within the default tolerance or within its own where that is the finer, with its
        /// corners within its tolerance of each other made one and put on the edges they stand on, where that closes it, or
        /// failing that the corners of the edges left open within up to four times the tolerance; the body as it is
        /// otherwise.
        /// </summary>
        internal static GeoSolid3 Sealed(GeoSolid3 solid, Tolerance work)
        {
            Tolerance check = work.EqualPoint < Tolerance.Global.EqualPoint ? work : Tolerance.Global;

            // What a boolean gives mostly closes, and is asked once.
            if (solid == null || solid.ClosesByCorners(check))
            {
                return solid;
            }

            if (TryWeld(solid, work, check, out GeoSolid3 welded))
            {
                return welded;
            }

            // No tolerance welds nothing, and widening nought stays nought.
            if (!(work.EqualPoint > 0.0))
            {
                return solid;
            }

            // The corners of the edges still left unpaired then, and those only, within twice the tolerance, and then four
            // times, as far as a sliver is bridged; see the remarks on the class.
            for (double wide = 2.0 * work.EqualPoint; wide <= LoopAssembly.Bridge * work.EqualPoint; wide *= 2.0)
            {
                if (TryWeld(solid, work, check, wide, out welded))
                {
                    return welded;
                }
            }

            return solid;
        }

        /// <summary>
        /// Gets a body a boolean made sealed as <see cref="Sealed"/> seals it, within the tolerance the boolean was given and
        /// no other, taken as closed only where <see cref="GeoSolid3.Validate(Tolerance)"/> reads it so as well
        /// (<see cref="Shells3.Closes"/>), and only where the weld moves the volume no further than moving each corner it moves
        /// by the point tolerance can: one that <see cref="GeoSolid3.ClosesByCorners(Tolerance)"/> calls closed on two copies of an edge
        /// about the tolerance apart is welded too.
        /// </summary>
        /// <remarks>
        /// Moving a corner by a distance sweeps, on each face it is a corner of, no more than the distance times a third of the
        /// triangle it makes with its two neighbours: so much, at the point tolerance, for each corner moved, bounds the weld.
        /// Bounded only by the volumes the operation can give, the union of a cube of 100 and a block 40 by 30 by 50 running
        /// 0.001 into its top over an edge, the two turned, welded closed held 1 059 999.53 cubic millimetres, less than any
        /// reading of the block: 1 060 000 without the overlap, 1 060 000.6 with it.
        /// </remarks>
        /// <param name="solid">The body a boolean made.</param>
        /// <param name="work">The tolerance the boolean was given.</param>
        /// <param name="moved">How far the weld taken moved the volume; nought where none was.</param>
        internal static GeoSolid3 SealedAsValidated(GeoSolid3 solid, Tolerance work, out double moved)
        {
            moved = 0.0;

            // No tolerance welds nothing, and widening nought stays nought.
            if (solid == null || !(work.EqualPoint > 0.0) || Shells3.Closes(solid, work))
            {
                return solid;
            }

            for (double wide = work.EqualPoint; wide <= LoopAssembly.Bridge * work.EqualPoint; wide *= 2.0)
            {
                if (TryWeld(solid, work, work, wide, true, out GeoSolid3 welded, out moved))
                {
                    return welded;
                }
            }

            moved = 0.0;
            return solid;
        }

        /// <summary>
        /// Welds the corners of a body's faces within a tolerance; false where what comes of it does not close within it and
        /// within the finer one, or moves its volume further than the tolerance times its area.
        /// </summary>
        internal static bool TryWeld(GeoSolid3 solid, Tolerance work, Tolerance fine, out GeoSolid3 welded)
            => TryWeld(solid, work, fine, work.EqualPoint, out welded);

        /// <summary>
        /// Welds the corners of a body's faces within a tolerance, and the corners of the edges that leaves unpaired within a
        /// wider reach; false where what comes of it does not close within the tolerance and within the finer one, or moves
        /// its volume further than the wider reach times its area.
        /// </summary>
        private static bool TryWeld(GeoSolid3 solid, Tolerance work, Tolerance fine, double wide, out GeoSolid3 welded)
            => TryWeld(solid, work, fine, wide, false, out welded, out _);

        /// <summary>
        /// Welds as <see cref="TryWeld(GeoSolid3, Tolerance, Tolerance, double, out GeoSolid3)"/> does, what comes of it read
        /// as closed by <see cref="Shells3.Closes"/> where asked to be.
        /// </summary>
        private static bool TryWeld(GeoSolid3 solid, Tolerance work, Tolerance fine, double wide, bool asValidated, out GeoSolid3 welded, out double shift)
        {
            welded = null;
            shift = 0.0;
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

            // The rings as they came, for a weld that keeps every face on its plane; see OnTheirPlanes.
            var given = new List<List<int>>[faces.Count];

            for (int f = 0; f < faces.Count; f++)
            {
                given[f] = new List<List<int>>(rings[f]);
            }

            // Where each group goes is weighed by the corners each point turns; see Turns.
            Func<int, List<GeoVector3>> turns = Turns(rings, points, reach);
            int[] to = Representatives(points, turns, reach);
            int[] wider = null;

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

            if (wide > reach)
            {
                // A point welded to stands for every copy welded to it, and is weighed by the corners all of them turn: a
                // column's corner made one with its copy on a sliver beside it a few bits away, and weighed by the sliver's
                // corners only, was taken 0.001 off the column's face, 300 by 1 000, to a beam's corner, and the union of the
                // two, the beam's end exactly the tolerance into the column, came out 7.6 cubic millimetres short of the least
                // it can hold.
                var standsFor = new List<int>[points.Count];

                for (int i = 0; i < points.Count; i++)
                {
                    if (standsFor[to[i]] == null)
                    {
                        standsFor[to[i]] = new List<int>(2);
                    }

                    standsFor[to[i]].Add(i);
                }

                List<GeoVector3> TurnsOfAll(int id)
                {
                    var all = new List<GeoVector3>();

                    foreach (int copy in standsFor[id])
                    {
                        all.AddRange(turns(copy));
                    }

                    return all;
                }

                wider = Representatives(points, TurnsOfAll, wide, Loose(rings));

                foreach (List<List<int>> faceRings in rings)
                {
                    for (int r = 0; r < faceRings.Count; r++)
                    {
                        List<int> moved = Collapse(faceRings[r], wider, out bool changed);
                        anyMoved |= changed;
                        faceRings[r] = moved;
                    }
                }
            }

            bool[] cornered = PutCornersOnEdges(rings, points, wide);

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

            double bound = asValidated ? Swept(points, corners, to, wider, reach) : wide * solid.GrossSurfaceArea;
            double rounding = Splition3.Rounding(solid);

            // A weld that takes a corner off the plane of a face it is a corner of tilts the face, and moves the volume by
            // the wedge it sweeps; one keeping every face on its plane, the steps between closed by faces of their own, is
            // taken first where it closes wound alike and moves the volume less. Only where the tilt is worth it: faces tilted
            // by more than a hundredth of the tolerance, and the weld moving the volume by more than a tenth of the tolerance
            // times their area. The flange of a column 4 000 long, tilted at a corner where a beam met it, moved a union by 4
            // to 370 cubic millimetres, a thirtieth of that or less, and was welded as before, where the beam's end is tilted
            // now (see Turns); kept on its plane, the flange of one with a beam framing a fifth of the tolerance into it moved
            // the volume by 14.2 where tilting it moved it by 3.7. The strips of a beam's end, 30 000 square millimetres, moved
            // a union by 707, and a box's side, 4 000, a difference by 6.5.
            double tilted = TiltedArea(faces, rings, points, Math.Max(rounding, 0.01 * reach));
            double tilting = tilted > 0.0 ? Math.Abs(new GeoSolid3(built).GetSignedVolume() - solid.GetSignedVolume()) : 0.0;

            if (tilting > 0.1 * reach * tilted)
            {
                List<GeoFace3> flat = OnTheirPlanes(faces, given, points, corners, to, wider, rounding, wide, pieces);

                if (flat != null && Shells3.ClosesAlike(flat, fine) && Accept(solid, flat, work, fine, asValidated, bound, out welded, out shift)
                    && shift <= tilting + 1E-9 * Math.Abs(solid.GetSignedVolume()))
                {
                    return true;
                }
            }

            return Accept(solid, built, work, fine, asValidated, bound, out welded, out shift);
        }

        /// <summary>
        /// Takes the faces a weld built where they close within the tolerance and within the finer one, as
        /// <see cref="Shells3.Closes"/> reads it where asked to, and move the volume no further than the bound.
        /// </summary>
        private static bool Accept(GeoSolid3 solid, List<GeoFace3> built, Tolerance work, Tolerance fine, bool asValidated, double bound, out GeoSolid3 welded, out double shift)
        {
            welded = null;
            shift = 0.0;

            if (built == null || built.Count < 4)
            {
                return false;
            }

            var candidate = new GeoSolid3(built, solid.Openings);

            if (asValidated
                ? !Shells3.Closes(candidate, fine) || !Shells3.Closes(candidate, work)
                : !candidate.ClosesByCorners(fine) || !candidate.ClosesByCorners(work))
            {
                return false;
            }

            double before = solid.GetSignedVolume();
            double after = candidate.GetSignedVolume();

            if (Math.Abs(after - before) > bound + 1E-9 * Math.Abs(before))
            {
                return false;
            }

            shift = Math.Abs(after - before);
            welded = candidate;
            return true;
        }

        /// <summary>
        /// Gets the area of the faces a weld tilts: those it puts a corner on standing off the face's plane further than
        /// its own corners do by more than the least given, a corner moved off it or one put on its edge from off it.
        /// </summary>
        private static double TiltedArea(IReadOnlyList<GeoFace3> faces, List<List<int>>[] rings, List<GeoPoint3> points, double least)
        {
            double area = 0.0;

            for (int f = 0; f < faces.Count; f++)
            {
                if (Unchanged(faces[f], rings[f], points))
                {
                    continue;
                }

                GeoPlane3 plane = faces[f].GetPlane();
                double own = OffPlane(plane, faces[f].Boundary.Vertices);

                foreach (GeoPolygon3 hole in faces[f].Holes)
                {
                    own = Math.Max(own, OffPlane(plane, hole.Vertices));
                }

                double tilt = 0.0;

                foreach (List<int> ring in rings[f])
                {
                    foreach (int id in ring)
                    {
                        tilt = Math.Max(tilt, Math.Abs(plane.SignedDistanceTo(points[id])) - own);
                    }
                }

                if (tilt > least)
                {
                    area += faces[f].Area;
                }
            }

            return area;
        }

        private static double OffPlane(GeoPlane3 plane, IReadOnlyList<GeoPoint3> corners)
        {
            double off = 0.0;

            foreach (GeoPoint3 corner in corners)
            {
                off = Math.Max(off, Math.Abs(plane.SignedDistanceTo(corner)));
            }

            return off;
        }

        /// <summary>
        /// Gets the faces of a weld that moves no corner off the plane of a face it is a corner of; null where the steps it
        /// leaves do not close.
        /// </summary>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="given">The rings of each face, on the positions of its corners, as they came.</param>
        /// <param name="points">Every distinct corner.</param>
        /// <param name="corners">The triangle each makes on each ring it is a corner of, as an area vector.</param>
        /// <param name="to">Where each corner is welded to.</param>
        /// <param name="wider">Where each welded corner is welded to next, or null.</param>
        /// <param name="rounding">How far off a plane a corner stands by rounding alone.</param>
        /// <param name="wide">How far apart corners are taken together; a step is no wider across than <see cref="LoopAssembly.Bridge"/> times it.</param>
        /// <param name="pieces">The tolerance the faces are built within, as the weld builds them.</param>
        /// <remarks>
        /// <para>
        /// A face keeps a corner where it would be moved off its plane, and takes the corner it is welded to where that lies
        /// on it; where a face moves a corner along its plane and a face beside it, sharing an edge at that corner, keeps it,
        /// the one takes both, the corner it keeps between, so the two meet on their edge as they did. What is left open
        /// between the corners kept and the corners moved to is a step a few thousandths across, closed by a face of its
        /// own; a corner is put on an edge only where it lies on the edge's line.
        /// </para>
        /// <para>
        /// A column 300 by 300 joined to a beam 383 wide and 366 high whose end stood 0.008 clear of the column's face, at a
        /// tolerance of a hundredth, welded with the beam's end corners on the column's face: the parts of the end beyond
        /// the column, two strips 41.6 wide, were tilted into wedges, and the union held 999.8 cubic millimetres more than
        /// the two, where filling the gap holds 878. Kept on their plane, with a face 0.008 wide closing each side of the
        /// gap, it holds the two and the gap filled. And a box less a cube in its corner had a corner of its top 0.0099 in
        /// from the edge put on the edge of its side, which took the side 4 000 square millimetres off flat, fanned, 6.5
        /// cubic millimetres short; the sliver between the top's corner and the side's edge is closed by a face of its own.
        /// </para>
        /// </remarks>
        private static List<GeoFace3> OnTheirPlanes(IReadOnlyList<GeoFace3> faces, List<List<int>>[] given, List<GeoPoint3> points, List<List<GeoVector3>> corners, int[] to, int[] wider, double rounding, double wide, Tolerance pieces)
        {
            int count = faces.Count;
            var normals = new GeoVector3[count];
            var runBy = new Dictionary<long, List<int>>(PackedKeys.Instance);

            // Corners within rounding of each other are one corner: two faces meeting at a corner each hold their own copy
            // of it, a few bits apart.
            int[] same = Representatives(points, i => corners[i], rounding);
            var held = new List<List<int>>[count];

            for (int f = 0; f < count; f++)
            {
                normals[f] = faces[f].Normal;
                held[f] = new List<List<int>>(given[f].Count);

                foreach (List<int> ring in given[f])
                {
                    held[f].Add(Collapse(ring, same, out _));
                }

                foreach (List<int> ring in held[f])
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        long key = Key(ring[i], ring[(i + 1) % ring.Count]);

                        if (!runBy.TryGetValue(key, out List<int> by))
                        {
                            by = new List<int>(2);
                            runBy.Add(key, by);
                        }

                        if (by.Count == 0 || by[by.Count - 1] != f)
                        {
                            by.Add(f);
                        }
                    }
                }
            }

            int Final(int id) => wider == null ? to[id] : wider[to[id]];

            // Whether a face keeps a corner, which its weld would take off the face's plane.
            bool Keeps(int f, int id)
            {
                int at = Final(id);
                return at != id && Math.Abs(points[id].GetVectorTo(points[at]).DotProduct(normals[f])) > rounding;
            }

            // Whether another face running an edge keeps a corner of it.
            bool KeptBeside(long edge, int f, int id)
            {
                if (runBy.TryGetValue(edge, out List<int> by))
                {
                    foreach (int g in by)
                    {
                        if (g != f && Keeps(g, id))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            var rings = new List<List<int>>[count];

            for (int f = 0; f < count; f++)
            {
                rings[f] = new List<List<int>>(held[f].Count);

                foreach (List<int> ring in held[f])
                {
                    var placed = new List<int>(ring.Count + 4);
                    int n = ring.Count;

                    for (int i = 0; i < n; i++)
                    {
                        int id = ring[i];
                        int at = Final(id);

                        if (at == id || Keeps(f, id))
                        {
                            Place(placed, id);
                            continue;
                        }

                        int previous = ring[(i + n - 1) % n], next = ring[(i + 1) % n];
                        bool before = KeptBeside(Key(previous, id), f, id);
                        bool after = KeptBeside(Key(id, next), f, id);

                        // Keeping the corner beside the one moved to, along the edge they would share, would run back over
                        // that edge: the face keeps its corner instead.
                        if (before && after || before && Between(points[at], points[previous], points[id], rounding)
                            || after && Between(points[at], points[id], points[next], rounding))
                        {
                            Place(placed, id);
                            continue;
                        }

                        if (before)
                        {
                            Place(placed, id);
                        }

                        Place(placed, at);

                        if (after)
                        {
                            Place(placed, id);
                        }
                    }

                    while (placed.Count > 1 && placed[placed.Count - 1] == placed[0])
                    {
                        placed.RemoveAt(placed.Count - 1);
                    }

                    rings[f].Add(placed);
                }
            }

            PutCornersOnEdges(rings, points, rounding);

            if (!TryCloseSteps(rings, points, wide, out List<List<int>> steps) && !TryCloseGaps(faces, normals, rings, points, wide, out steps))
            {
                return null;
            }

            // Built as the weld builds its faces, but with no corner taken for its neighbour: a step's corners stand within
            // the tolerance of each other.
            var exact = new Tolerance(rounding, pieces.EqualVector, pieces.EqualAngleRad, pieces.EqualPlanar);
            var built = new List<GeoFace3>(count + steps.Count);
            var asCame = new List<bool>(count + steps.Count);

            for (int f = 0; f < count; f++)
            {
                List<List<int>> faceRings = rings[f];

                if (Unchanged(faces[f], faceRings, points))
                {
                    built.Add(faces[f]);
                    asCame.Add(true);
                    continue;
                }

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

                foreach (GeoFace3 face in Loops3.ToFaces(Corners(faceRings[0], points), holes, exact))
                {
                    built.Add(face);
                    asCame.Add(false);
                }
            }

            int kept = built.Count;

            foreach (List<int> step in steps)
            {
                built.AddRange(Loops3.ToFaces(Corners(step, points), null, exact));
            }

            // A face that runs back over itself, or two faces lying over each other in one plane, either way round, fold the
            // surface over itself or cover it twice: closed to every reading and counting the area they share twice. The weld
            // is made as before then. A union of a cube of 100 and another tilted 0.0099 off its face, at a tolerance of a
            // hundredth, came out with a step lying on the first cube's top, 100 000 square millimetres of surface where it
            // has 99 998.02; and a column and a beam framing 0.002 into its flange, the beam's flange beyond the column's edge,
            // with two steps 0.002 by 16 lying on the side of the column's flange.
            var fine = new Tolerance(Math.Max(1E-7, 10.0 * rounding), Math.Min(pieces.EqualVector, 1E-9), pieces.EqualAngleRad, Math.Max(1E-7, 10.0 * rounding));
            var flat = new Tolerance(fine.EqualPoint, fine.EqualVector, pieces.EqualAngleRad, Math.Max(fine.EqualPlanar, pieces.EqualPlanar));
            var boxes = new GeoAabb3[built.Count];
            var planes = new GeoPlane3[built.Count];

            for (int k = 0; k < built.Count; k++)
            {
                if ((k >= kept || !asCame[k]) && !IsSimple(built[k], fine))
                {
                    return null;
                }

                boxes[k] = built[k].GetAabb();
                planes[k] = built[k].GetPlane();
            }

            for (int k = 0; k < built.Count; k++)
            {
                for (int g = k + 1; g < built.Count; g++)
                {
                    // Faces of the body as they came are not this weld's to answer for, between themselves.
                    if (k < kept && asCame[k] && g < kept && asCame[g])
                    {
                        continue;
                    }

                    if (Math.Abs(built[g].Normal.DotProduct(built[k].Normal)) < 1.0 - 1E-6 || !boxes[g].CollidesWith(boxes[k], flat)
                        || OffPlane(planes[g], built[k].Boundary.Vertices) > flat.EqualPlanar
                        || OffPlane(planes[k], built[g].Boundary.Vertices) > flat.EqualPlanar)
                    {
                        continue;
                    }

                    if (!(Overlap(built[k], built[g], flat) <= wide * wide))
                    {
                        return null;
                    }
                }
            }

            return built;
        }

        /// <summary>
        /// Gets the area two faces lying in one plane within the planar tolerance both cover, the second laid into the
        /// first's plane; not a number where it cannot be worked out, which is read as an overlap.
        /// </summary>
        private static double Overlap(GeoFace3 face, GeoFace3 other, Tolerance flat)
        {
            double area = 0.0;

            try
            {
                foreach (GeoFace3 piece in Boolean3.Intersect(face, other, flat))
                {
                    area += piece.Area;
                }
            }
            catch (Exception exception) when (Boolean3.IsUnworkable(exception))
            {
                return double.NaN;
            }

            return area;
        }

        /// <summary>
        /// Whether a face's boundary and holes are each simple, no edge crossing or running back over another.
        /// </summary>
        private static bool IsSimple(GeoFace3 face, Tolerance fine)
        {
            if (!face.Boundary.IsSimple(fine))
            {
                return false;
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                if (!hole.IsSimple(fine))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a point lies on the segment between two others, within rounding of its line and between its ends.
        /// </summary>
        private static bool Between(GeoPoint3 point, GeoPoint3 start, GeoPoint3 end, double rounding)
        {
            GeoVector3 along = start.GetVectorTo(end);
            double length = along.Length;

            if (!(length > 0.0))
            {
                return false;
            }

            GeoVector3 unit = along.Divide(length);
            GeoVector3 offset = start.GetVectorTo(point);
            double at = offset.DotProduct(unit);

            return at > 0.0 && at < length && offset.Subtract(unit.Multiply(at)).Length <= rounding;
        }

        private static void Place(List<int> ring, int id)
        {
            if (ring.Count == 0 || ring[ring.Count - 1] != id)
            {
                ring.Add(id);
            }
        }

        /// <summary>
        /// Gets the loops of corners closing the edges rings run more often one way than the other, each from its end back
        /// to its start; false where they do not all make steps (<see cref="Step"/>).
        /// </summary>
        private static bool TryCloseSteps(List<List<int>>[] rings, List<GeoPoint3> points, double wide, out List<List<int>> steps)
        {
            steps = new List<List<int>>();
            List<(int From, int To)> open = Unpaired(rings);

            if (open.Count == 0)
            {
                return true;
            }

            var leaving = new Dictionary<int, List<int>>(open.Count);

            for (int e = 0; e < open.Count; e++)
            {
                if (!leaving.TryGetValue(open[e].From, out List<int> ways))
                {
                    ways = new List<int>(1);
                    leaving.Add(open[e].From, ways);
                }

                ways.Add(e);
            }

            // Each loop is walked edge by edge from the first edge left; where a corner has more than one way on, the ways
            // are tried in turn, and a loop is kept only where it is a step: two steps meeting at a corner are two loops,
            // and a walk round both is no strip. The tries are bounded.
            var used = new bool[open.Count];
            int budget = 16 * open.Count + 256;

            for (int e = 0; e < open.Count; e++)
            {
                if (used[e])
                {
                    continue;
                }

                int start = open[e].From;
                var path = new List<int> { e };
                var cursors = new List<int> { 0 };
                var on = new HashSet<int> { start };
                used[e] = true;
                List<int> loop = null;

                while (path.Count > 0)
                {
                    if (--budget < 0)
                    {
                        return false;
                    }

                    int at = open[path[path.Count - 1]].To;

                    if (at == start)
                    {
                        var corners = new List<int>(path.Count);

                        foreach (int walked in path)
                        {
                            corners.Add(open[walked].From);
                        }

                        int step = Step(corners, points, wide);

                        if (step >= 0)
                        {
                            loop = step > 0 ? corners : new List<int>();
                            break;
                        }
                    }
                    else if (leaving.TryGetValue(at, out List<int> ways) && !on.Contains(at))
                    {
                        int cursor = cursors[cursors.Count - 1];

                        while (cursor < ways.Count && used[ways[cursor]])
                        {
                            cursor++;
                        }

                        cursors[cursors.Count - 1] = cursor + 1;

                        if (cursor < ways.Count)
                        {
                            used[ways[cursor]] = true;
                            on.Add(at);
                            path.Add(ways[cursor]);
                            cursors.Add(0);
                            continue;
                        }
                    }

                    // Back one edge: nothing on from here closes a step.
                    int last = path[path.Count - 1];

                    if (path.Count == 1)
                    {
                        break;
                    }

                    used[last] = false;
                    path.RemoveAt(path.Count - 1);
                    cursors.RemoveAt(cursors.Count - 1);
                    on.Remove(open[last].From);
                }

                if (loop == null)
                {
                    return false;
                }

                if (loop.Count > 0)
                {
                    steps.Add(loop);
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the edges the rings run more often one way than the other, each the way a face closing it runs it, as many
        /// times as it is left unpaired, in order.
        /// </summary>
        private static List<(int From, int To)> Unpaired(List<List<int>>[] rings)
        {
            var runs = new Dictionary<long, int>(PackedKeys.Instance);

            foreach (List<List<int>> faceRings in rings)
            {
                foreach (List<int> ring in faceRings)
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        int from = ring[i];
                        int next = ring[(i + 1) % ring.Count];

                        if (from != next)
                        {
                            long run = ((long)from << 32) | (uint)next;
                            runs[run] = (runs.TryGetValue(run, out int seen) ? seen : 0) + 1;
                        }
                    }
                }
            }

            var open = new List<(int From, int To)>();

            foreach (KeyValuePair<long, int> run in runs)
            {
                int from = (int)(run.Key >> 32);
                int next = (int)(run.Key & 0xFFFFFFFFL);
                int back = runs.TryGetValue(((long)next << 32) | (uint)from, out int seen) ? seen : 0;

                for (int extra = run.Value - back; extra > 0; extra--)
                {
                    open.Add((next, from));
                }
            }

            open.Sort();
            return open;
        }

        /// <summary>
        /// Gets the faces closing the gap between loops of unpaired edges that face each other a few thousandths apart,
        /// corner over corner: one strip along each edge of a loop, from it to the edge over it; false where the edges left
        /// open are not such loops.
        /// </summary>
        /// <remarks>
        /// A face kept on its plane where it lies against a face of the other body a gap within the tolerance away leaves
        /// both rims of the gap open, each a loop, and no step: a strip runs along an edge from one loop to the other, and is
        /// no loop of edges left open. A column 300 by 300 joined, within a hundredth, to a beam 387 wide and 118 high whose
        /// end stood 0.008 clear of the column's face was welded with the beam's end on the column's face: the parts of the
        /// end beyond the column, two strips 43.5 wide, were tilted into wedges, and the union held 41.06 cubic millimetres
        /// more than the two and the gap filled. Closed by four strips round the gap, it holds the two and the gap filled.
        /// <para>
        /// Only a gap is closed so: the faces running the two rims face each other across it, each toward the other rim, and
        /// what lies between them is no material of either. Rims a few thousandths apart whose faces face away from each
        /// other bound a slab both bodies hold: a beam 252 wide whose end ran exactly a hundredth into a column 300 by 300,
        /// joined to it within a hundredth, came out with such rims, the column's face and the beam's end within it, and
        /// closed by strips round them held the slab twice, 375.06 cubic millimetres over the union.
        /// </para>
        /// </remarks>
        private static bool TryCloseGaps(IReadOnlyList<GeoFace3> faces, GeoVector3[] normals, List<List<int>>[] rings, List<GeoPoint3> points, double wide, out List<List<int>> steps)
        {
            steps = new List<List<int>>();
            List<(int From, int To)> open = Unpaired(rings);

            // The faces running each edge, by the way they run it: an edge left open the way a face closing it runs it is run
            // the other way by the face whose rim it is, and by the faces meeting that face along it.
            var runBy = new Dictionary<long, List<int>>(PackedKeys.Instance);

            for (int f = 0; f < rings.Length; f++)
            {
                foreach (List<int> ring in rings[f])
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        long run = ((long)ring[i] << 32) | (uint)ring[(i + 1) % ring.Count];

                        if (!runBy.TryGetValue(run, out List<int> by))
                        {
                            by = new List<int>(2);
                            runBy.Add(run, by);
                        }

                        by.Add(f);
                    }
                }
            }

            // Whether no material of the body lies beside an edge left open, between it and the rim over it the way given:
            // no face running the edge faces away from that rim, its body behind it on the gap's side, and none standing across
            // the gap runs on from the edge toward that rim.
            bool Clear(int from, int to, GeoVector3 toward)
            {
                if (!runBy.TryGetValue(((long)to << 32) | (uint)from, out List<int> by))
                {
                    return false;
                }

                GeoPoint3 middle = points[from].Add(points[from].GetVectorTo(points[to]).Multiply(0.5));
                double length = toward.Length;

                foreach (int f in by)
                {
                    double facing = normals[f].DotProduct(toward);

                    if (facing < -0.5 * length || Math.Abs(facing) <= 0.5 * length && middle.GetVectorTo(faces[f].Centroid).DotProduct(toward) > 0.0)
                    {
                        return false;
                    }
                }

                return true;
            }

            var leaving = new Dictionary<int, int>(open.Count);

            for (int e = 0; e < open.Count; e++)
            {
                if (leaving.ContainsKey(open[e].From))
                {
                    return false;
                }

                leaving.Add(open[e].From, e);
            }

            // Every corner left open once, so the edges make loops, each walked from the first of its edges in order.
            var used = new bool[open.Count];
            var loops = new List<List<int>>();

            for (int e = 0; e < open.Count; e++)
            {
                if (used[e])
                {
                    continue;
                }

                var loop = new List<int>();
                int at = e;

                while (!used[at])
                {
                    used[at] = true;
                    loop.Add(open[at].From);

                    if (!leaving.TryGetValue(open[at].To, out at))
                    {
                        return false;
                    }
                }

                if (at != e)
                {
                    return false;
                }

                loops.Add(loop);
            }

            // Each loop with the first after it whose corners stand over its own within a bridge of the reach, in turn the
            // other way round, as the faces on the two sides of a gap run its rims.
            double across = LoopAssembly.Bridge * wide;
            var paired = new bool[loops.Count];

            for (int i = 0; i < loops.Count; i++)
            {
                if (paired[i])
                {
                    continue;
                }

                List<int> one = loops[i];
                int n = one.Count;
                bool found = false;

                for (int j = i + 1; j < loops.Count && !found; j++)
                {
                    List<int> other = loops[j];

                    if (paired[j] || other.Count != n || n < 3)
                    {
                        continue;
                    }

                    for (int shift = 0; shift < n && !found; shift++)
                    {
                        bool over = true;

                        for (int t = 0; t < n && over; t++)
                        {
                            over = points[one[t]].DistanceTo(points[other[((shift - t) % n + n) % n]]) <= across;
                        }

                        if (!over)
                        {
                            continue;
                        }

                        // The strip along each edge of the one loop runs that edge and the edge over it the ways they are
                        // left open, and its two ends each way, so the strips side by side pair their ends.
                        for (int t = 0; t < n; t++)
                        {
                            int a = one[t], b = one[(t + 1) % n], c = other[((shift - t - 1) % n + n) % n], d = other[((shift - t) % n + n) % n];
                            var strip = new List<int> { a, b, c, d };
                            GeoVector3 gap = points[a].GetVectorTo(points[d]).Add(points[b].GetVectorTo(points[c]));

                            if (Step(strip, points, wide) < 0 || !Clear(a, b, gap) || !Clear(c, d, gap.Multiply(-1.0)))
                            {
                                return false;
                            }

                            steps.Add(strip);
                        }

                        paired[i] = paired[j] = true;
                        found = true;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a loop of corners is a step a weld closes by a face: 1 where it is a strip along its longest edge, every
        /// corner within <see cref="LoopAssembly.Bridge"/> times the reach of that edge's line; 0 where it encloses no area,
        /// an edge run beside two that make it up, passed over; -1 otherwise. A loop folded back on itself encloses next to
        /// no area however far it runs, and is no step.
        /// </summary>
        private static int Step(List<int> loop, List<GeoPoint3> points, double wide)
        {
            if (loop.Count < 3)
            {
                return -1;
            }

            GeoVector3 area = new GeoVector3(0.0, 0.0, 0.0);
            int longest = 0;
            double longestLength = -1.0;
            GeoPoint3 first = points[loop[0]];

            for (int i = 0; i < loop.Count; i++)
            {
                GeoPoint3 a = points[loop[i]];
                GeoPoint3 b = points[loop[(i + 1) % loop.Count]];
                area = area.Add(first.GetVectorTo(a).CrossProduct(first.GetVectorTo(b)));
                double length = a.DistanceTo(b);

                if (length > longestLength)
                {
                    longest = i;
                    longestLength = length;
                }
            }

            if (0.5 * area.Length <= wide * wide)
            {
                return 0;
            }

            if (!(longestLength > 0.0))
            {
                return -1;
            }

            GeoPoint3 from = points[loop[longest]];
            GeoVector3 along = from.GetVectorTo(points[loop[(longest + 1) % loop.Count]]).Divide(longestLength);
            double across = LoopAssembly.Bridge * wide;

            foreach (int id in loop)
            {
                GeoVector3 offset = from.GetVectorTo(points[id]);

                if (offset.Subtract(along.Multiply(offset.DotProduct(along))).Length > across)
                {
                    return -1;
                }
            }

            return 1;
        }

        /// <summary>
        /// Gets how much volume moving each corner a weld moves by a distance can sweep: on each face it is a corner of, the
        /// distance times a third of the triangle it makes with its two neighbours.
        /// </summary>
        /// <param name="points">Every distinct corner.</param>
        /// <param name="corners">The triangle each makes on each ring it is a corner of, as an area vector.</param>
        /// <param name="to">Where each is welded to.</param>
        /// <param name="wider">Where each welded corner is welded to next, or null.</param>
        /// <param name="distance">How far a corner may move: one moved further is taken to move so far.</param>
        private static double Swept(List<GeoPoint3> points, List<List<GeoVector3>> corners, int[] to, int[] wider, double distance)
        {
            double swept = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                int at = wider == null ? to[i] : wider[to[i]];

                double moved = Math.Min(points[at].DistanceTo(points[i]), distance);

                if (!(moved > 0.0))
                {
                    continue;
                }

                foreach (GeoVector3 corner in corners[i])
                {
                    swept += moved * corner.Length / 3.0;
                }
            }

            return swept;
        }

        /// <summary>
        /// The positions of the corners of a ring, each distinct point given one the first time it is met, with the corner it
        /// makes on the ring added to each: half the cross of the edges to its neighbours, the area of the triangle it sweeps
        /// moved one unit off it.
        /// </summary>
        internal static List<int> Positions(GeoPolygon3 ring, Dictionary<GeoPoint3, int> index, List<GeoPoint3> points, List<List<GeoVector3>> corners)
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
        /// The corner each point turns on each ring it is a corner of, as an area vector, worked out the first time it is asked
        /// for: half the cross of the edges to the nearest corners either side that the ring does not run straight on to, a
        /// corner standing within the reach of the line from the point to the corner after it, between the two, passed over.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A face cut into strips keeps a corner on its edges where each strip ended, and a corner beside them makes a sliver
        /// of a triangle with its neighbours, a strip wide, however far the face reaches. Weighed by that triangle, the weld
        /// closing the step of a beam's end 0.002 into a column's flange of 300 strips took the column face's corner to the
        /// beam's, off the face's plane, where it moved the face's fan from its first corner, and the union came out 98.92
        /// cubic millimetres short of the two where their overlap is 17.04; weighed by the corner the face turns, its edges 750
        /// and 120 long, the beam's end is tilted instead, as with the column in one piece, the two less 18.96.
        /// </para>
        /// <para>
        /// Only the points of a group are weighed, so only theirs are worked out: walking every corner of a face of 1 000
        /// strips to the end of its edge took a quarter of a second.
        /// </para>
        /// </remarks>
        private static Func<int, List<GeoVector3>> Turns(List<List<int>>[] rings, List<GeoPoint3> points, double reach)
        {
            // Where each point stands on the rings, as they are now: a weld builds new rings and leaves these as they were.
            var standing = new List<(List<int> Ring, int At)>[points.Count];

            foreach (List<List<int>> faceRings in rings)
            {
                foreach (List<int> ring in faceRings)
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        if (standing[ring[i]] == null)
                        {
                            standing[ring[i]] = new List<(List<int>, int)>(3);
                        }

                        standing[ring[i]].Add((ring, i));
                    }
                }
            }

            var turns = new List<GeoVector3>[points.Count];

            return id =>
            {
                if (turns[id] == null)
                {
                    turns[id] = new List<GeoVector3>(3);
                    GeoPoint3 corner = points[id];

                    foreach ((List<int> ring, int at) in standing[id])
                    {
                        GeoVector3 back = corner.GetVectorTo(Turning(ring, at, -1, points, reach));
                        GeoVector3 ahead = corner.GetVectorTo(Turning(ring, at, 1, points, reach));
                        turns[id].Add(back.CrossProduct(ahead).Multiply(0.5));
                    }
                }

                return turns[id];
            };
        }

        /// <summary>
        /// The nearest corner of a ring from the one at a position, stepping one way, past every corner that stands within the
        /// reach of the line from that position's corner to the corner after it, between the two.
        /// </summary>
        private static GeoPoint3 Turning(List<int> ring, int at, int step, List<GeoPoint3> points, double reach)
        {
            int count = ring.Count;
            GeoPoint3 corner = points[ring[at]];
            int near = (at + step + count) % count;

            for (int next = (near + step + count) % count; next != at; next = (next + step + count) % count)
            {
                GeoPoint3 passed = points[ring[near]];
                GeoVector3 line = corner.GetVectorTo(points[ring[next]]);
                double length = line.DotProduct(line);
                double along = line.DotProduct(corner.GetVectorTo(passed));

                if (!(along > 0.0 && along < length) || corner.Add(line.Multiply(along / length)).DistanceTo(passed) > reach)
                {
                    break;
                }

                near = next;
            }

            return points[ring[near]];
        }

        /// <summary>
        /// The position each point is welded to: the points within the tolerance of each other, however they chain, are
        /// one group, and each goes to the point of it that takes the most of the group within the tolerance of it and of
        /// those moves the volume least, the first met of those; the rest of the group is settled the same way among
        /// themselves.
        /// </summary>
        private static int[] Representatives(List<GeoPoint3> points, Func<int, List<GeoVector3>> corners, double reach)
            => Representatives(points, corners, reach, null);

        /// <summary>
        /// The position each point is welded to, as the overload without marks gives it, of the points marked only where
        /// marks are given: every other point stays where it is.
        /// </summary>
        internal static int[] Representatives(List<GeoPoint3> points, Func<int, List<GeoVector3>> corners, double reach, bool[] only)
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
                if (!Marked(only, i))
                {
                    continue;
                }

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
                if (!Marked(only, i))
                {
                    continue;
                }

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
        private static void Weigh(int candidate, List<int> group, List<GeoPoint3> points, Func<int, List<GeoVector3>> corners, double reachSquared, out int taken, out double moved)
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

                foreach (GeoVector3 corner in corners(m))
                {
                    moved += Math.Abs(shift.DotProduct(corner));
                }
            }
        }

        /// <summary>
        /// A ring on the positions its corners are welded to, a position repeated in a row kept once, the last as the first
        /// too.
        /// </summary>
        internal static List<int> Collapse(List<int> ring, int[] to, out bool changed)
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
        /// The corners of the edges an odd number of rings run, the edges a body is open by.
        /// </summary>
        internal static bool[] Loose(List<List<int>>[] rings)
        {
            var counts = new Dictionary<long, int>(PackedKeys.Instance);
            int highest = -1;

            foreach (List<List<int>> faceRings in rings)
            {
                foreach (List<int> ring in faceRings)
                {
                    for (int i = 0; i < ring.Count; i++)
                    {
                        long key = Key(ring[i], ring[(i + 1) % ring.Count]);
                        counts[key] = (counts.TryGetValue(key, out int seen) ? seen : 0) + 1;
                        highest = Math.Max(highest, ring[i]);
                    }
                }
            }

            var loose = new bool[highest + 1];

            foreach (KeyValuePair<long, int> edge in counts)
            {
                if (edge.Value % 2 != 0)
                {
                    loose[(int)(edge.Key >> 32)] = true;
                    loose[(int)(edge.Key & 0xFFFFFFFFL)] = true;
                }
            }

            return loose;
        }

        private static bool Marked(bool[] only, int i) => only == null || i < only.Length && only[i];

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
                var counts = new Dictionary<long, int>(PackedKeys.Instance);

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
        internal static bool Unchanged(GeoFace3 face, List<List<int>> faceRings, List<GeoPoint3> points)
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

        /// <summary>
        /// The corners of a ring of positions, in its order.
        /// </summary>
        internal static IEnumerable<GeoPoint3> Corners(List<int> ring, List<GeoPoint3> points)
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
