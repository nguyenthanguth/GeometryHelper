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
        /// Welds shut the gaps a body is open by: the corners across each made one, within the point tolerance, then twice
        /// it, four times and so on while below the widest gap, and the widest gap last, the first reach that makes the body
        /// valid taken; and where no reach of welds alone does, the same again with the corners standing off edges left open
        /// put on them as well.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body as the steps before left it; on return, as this step leaves them.</param>
        /// <param name="origins">For each of the faces, the face given it comes of, by index; on return, for each this step
        /// leaves.</param>
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
        /// A corner put back onto the corner it was moved off is the change the faces round it want, and it is tried at
        /// every reach before any corner is put on an edge: a corner of the top of a prism half a degree sharp, moved 0.003
        /// into it, stands within a thousandth of both sides there, and put on both it closes the prism by a seam of no width
        /// from the sides' corner to it, where welding it back closes it as it was; a strip 0.03 wide missing from a top has
        /// its corners on the edges of the faces beside it, and welding its corners across it closes it, the edges beside it
        /// split for nothing.
        /// </para>
        /// <para>
        /// Where no reach makes the body valid, the least reach that leaves the fewest sides of gaps is kept for the steps
        /// after, the first that leaves none, holes still open, ending the search. A fin left on what is kept, a stretch more
        /// than two faces run that the welding was to make one, is <see cref="ClosingFailure.NonManifold"/>, and so is one a
        /// reach would have left longer than the widest gap, which is why that reach was not taken: a flap 0.004 square beside
        /// a corner moved out by 0.003 is a fin whatever the gap allowed, and welding its corners away would only hide it. A
        /// gap left is wider than the widest allowed, <see cref="ClosingFailure.GapTooWide"/>, where it is widest, the reason
        /// given unless a fill closes the body after.
        /// </para>
        /// <para>
        /// A body open by holes only is not welded; but an edge left open no longer than the widest gap is welded with nothing
        /// running back alongside it: copies of a corner a hair further apart than the tolerance leave one stretch a hair
        /// longer than it open, which bounds no hole and is a gap of no width.
        /// </para>
        /// </remarks>
        private static bool TryWeldGaps(Work work, ref List<GeoFace3> faces, ref List<int> origins, ref List<Rim> rims, out GeoSolid3 closed, out SolidClosing3 report)
        {
            closed = null;
            report = null;
            int gaps = Gaps(rims);

            // An edge left open no longer than the widest gap is welded too, with nothing running back alongside it: copies of
            // a corner a hair further apart than the tolerance leave a stretch that long open by itself.
            if (gaps == 0 && !HasShortRim(rims, work.Options.MaxGap))
            {
                return false;
            }

            // Only corners near the edges left open move: the rest of the body is closed as it is. An edge open along a
            // stretch of it is open, its corners as well as the stretch's.
            var ends = new List<GeoPoint3>(4 * rims.Count);

            foreach (Rim rim in rims)
            {
                ends.Add(rim.From);
                ends.Add(rim.To);
                ends.Add(rim.EdgeStart);
                ends.Add(rim.EdgeEnd);
            }

            List<double> reaches = Reaches(work.Tolerance.EqualPoint, work.Options.MaxGap);
            Welded best = null;

            for (int pass = 0; pass < 2 && (best == null || best.Gaps > 0); pass++)
            {
                foreach (double reach in reaches)
                {
                    Welded welded = WeldWithin(work, faces, origins, rims, ends, reach, pass == 1);

                    if (welded == null)
                    {
                        continue;
                    }

                    if (welded.IsValid)
                    {
                        TurnAgain(work, welded.Again);
                        work.Repairs.AddRange(welded.Repairs);
                        closed = welded.Body;
                        report = new SolidClosing3(work.Repairs.ToArray(), work.AddedArea, VolumeChange(work.Solid, closed), ClosingFailure.None, null);
                        return true;
                    }

                    if (welded.Gaps < (best == null ? gaps : best.Gaps))
                    {
                        best = welded;

                        if (best.Gaps == 0)
                        {
                            break;
                        }
                    }
                }
            }

            if (best != null)
            {
                TurnAgain(work, best.Again);
                work.Repairs.AddRange(best.Repairs);
                work.Current = best.Body;
                work.CurrentCheck = best.Body.Validate(work.Tolerance);
                faces = best.Faces;
                origins = best.Origins;
                rims = best.Rims;
            }

            foreach (Rim rim in rims)
            {
                if (rim.IsFin)
                {
                    work.Refuse(ClosingFailure.NonManifold, rim.From.GetMiddlePoint(rim.To));
                    return false;
                }
            }

            // A reach that would have left a fin longer than the widest gap was not taken, and no reach closed the body: the
            // fin is the trouble, where welding its corners away would only have hidden it.
            if (work.FinLeft.HasValue)
            {
                work.Refuse(ClosingFailure.NonManifold, work.FinLeft.Value);
                return false;
            }

            GeoPoint3? widest = WidestGap(rims);

            if (widest.HasValue)
            {
                work.Refuse(ClosingFailure.GapTooWide, widest.Value);
            }

            return false;
        }

        /// <summary>
        /// Determines whether an edge left open is no longer than the widest gap.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        /// <param name="maxGap">The widest gap.</param>
        private static bool HasShortRim(List<Rim> rims, double maxGap)
        {
            foreach (Rim rim in rims)
            {
                if (!(rim.From.DistanceTo(rim.To) > maxGap))
                {
                    return true;
                }
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
        /// Welds the corners of the edges a body's faces leave open within a reach, as <see cref="WeldOnce"/> does, and again
        /// with the corners of any edge the welds opened that read closed before, until they open no more or a round leaves
        /// no fewer edges open than the one before, eight times at the most; null where nothing comes of it, or what comes of
        /// it cannot be taken.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="origins">For each, the face given it comes of, by index.</param>
        /// <param name="rims">The edges they leave open.</param>
        /// <param name="ends">The corners of the edges they leave open, as the check matches edges.</param>
        /// <param name="reach">The reach.</param>
        /// <param name="onEdges">Whether corners standing off edges left open are put on them, after the welds.</param>
        /// <returns>The body welded, as the last round that came to anything left it.</returns>
        /// <remarks>
        /// Copies of a corner a hair apart whose edges lie within the tolerance of each other's lines read closed, and stay
        /// where they are; but welded, the corners beside them can turn those edges off each other's lines. On a prism of a
        /// thousand sides, each face on copies of its corners a thousandth or two apart, the copies of a corner of the top
        /// read closed until the corner 0.06 along the top from them was welded, and open after: their corners are taken
        /// with the rest, and the reach welded again. A round that leaves as many edges open as the one before has welded
        /// nothing new, and the next would not either: a sphere of six thousand faces each on copies of its own corners, too
        /// far apart for the reach, was welded three times over at it for nothing.
        /// </remarks>
        private static Welded WeldWithin(Work work, List<GeoFace3> faces, List<int> origins, List<Rim> rims, List<GeoPoint3> ends, double reach, bool onEdges)
        {
            var open = new HashSet<GeoPoint3>(ends);
            List<GeoPoint3> taken = ends;
            Welded last = null;

            for (int round = 0; round < 8; round++)
            {
                Welded welded = WeldOnce(work, faces, origins, rims, taken, reach, onEdges);

                if (welded == null)
                {
                    return last;
                }

                if (welded.IsValid)
                {
                    return welded;
                }

                // A round that leaves as many edges open as the one before has opened nothing the next could weld.
                if (last != null && welded.Rims.Count >= last.Rims.Count)
                {
                    return last;
                }

                last = welded;
                List<GeoPoint3> more = null;

                foreach (Rim rim in welded.Rims)
                {
                    foreach (GeoPoint3 corner in new[] { rim.From, rim.To, rim.EdgeStart, rim.EdgeEnd })
                    {
                        if (open.Add(corner))
                        {
                            (more = more ?? new List<GeoPoint3>(taken)).Add(corner);
                        }
                    }
                }

                if (more == null)
                {
                    break;
                }

                taken = more;
            }

            return last;
        }

        /// <summary>
        /// Welds the corners of the edges a body's faces leave open within a reach, and puts those standing off edges left
        /// open on them where asked; null where nothing comes of it, or what comes of it cannot be taken.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="origins">For each, the face given it comes of, by index.</param>
        /// <param name="rims">The edges they leave open.</param>
        /// <param name="ends">The corners of the edges they leave open, as the check matches edges.</param>
        /// <param name="reach">The reach.</param>
        /// <param name="onEdges">Whether corners standing off edges left open are put on them, after the welds.</param>
        /// <returns>The body welded, valid or not, with what was done to it and the edges it leaves open; null where nothing
        /// moved, fewer than four faces are left, a ring doubles back, a face lies back to back with another, the volume
        /// moved further than allowed, or a fin longer than the widest gap is left.</returns>
        /// <remarks>
        /// <para>
        /// The corners that move are those of edges no ring pairs corner for corner that stand within the reach of an edge
        /// left open, as the check matches edges, within the point tolerance as beyond it: corners of a closed body a
        /// rounding apart far from any gap, as at the pole of a sphere meshed by rings, stay where they are. The corners
        /// within the reach of each other are made one, the nearest two first, and two corners of one face only where they are
        /// copies of one corner of it, beside each other on a ring across an edge left open (see <see cref="MayShareFaces"/>):
        /// a corner of a face is no copy of another corner of it as a rule, and made one with it, it pinches the face. A slot
        /// 0.0035 wide beside a gap, its corners across its mouth within the reach of a corner of the gap and on edges no
        /// ring pairs corner for corner, keeps its mouth so. Each group goes to the corner of it that takes the most of it
        /// within the reach and of those bends the faces round it least, each face weighed by its area (see
        /// <see cref="Weigh"/>): a copy of a corner moved in the plane of its own face goes back onto the corner the faces
        /// beside it keep, nothing tilts, and a large cap is not tilted to spare a narrow side. Then, where asked, each corner
        /// standing within the reach of an edge left open, between its ends, is put on it; see
        /// <see cref="PutCornersOnOpenEdges"/>. A face whose corners moved is built again on them, one face where they lie
        /// flat about their middle and triangles on its own corners where they do not; see <see cref="Rebuilt"/>.
        /// </para>
        /// <para>
        /// A ring running out to a corner and straight back is no face, though it reads valid: a corner 0.02 off the corner
        /// the faces beside it keep is 0.014 off the two edges of its face beside it, and put on both within a reach of
        /// 0.016, it closes the face by a needle out to the corner moved and back. Such a reach is not taken, and the next
        /// welds the two. Nor is one that leaves a face built again lying back to back with another, a skin of no thickness
        /// that reads valid too (see <see cref="HasSkin"/>), nor one whose faces built again sweep out more volume than the
        /// reach times their area, each measured from where it was (see <see cref="Swept"/>): a corner moved no further than
        /// the reach sweeps no more.
        /// </para>
        /// <para>
        /// What a reach leaves open is read again only where its faces changed, from them and the faces beside them; the
        /// rest is open as it was (see <see cref="OpenEdgesAfter"/>). Where nothing is left open, every shell closes, and the
        /// faces are oriented again before the body is judged (see <see cref="Reorient"/>): a shell open before, its sign not
        /// read, is turned out now where it encloses its volume inwards. The whole body is checked only then, so that a reach
        /// that closes nothing costs what its faces cost, not what the body does.
        /// </para>
        /// </remarks>
        private static Welded WeldOnce(Work work, List<GeoFace3> faces, List<int> origins, List<Rim> rims, List<GeoPoint3> ends, double reach, bool onEdges)
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
            // edge left open as the check matches edges.
            bool[] unpaired = Unpaired(rings, points.Count);
            bool[] near = NearOpenEdges(points, ends, within);
            var weldable = new bool[points.Count];
            bool any = false;

            for (int i = 0; i < weldable.Length; i++)
            {
                weldable[i] = near[i] && unpaired[i];
                any |= weldable[i];
            }

            if (!any)
            {
                return null;
            }

            int[] target = Weldings(points, FacesOfCorners(rings, weldable), Bends(faces, rings, weldable), within, weldable, rings, OpenEdgesOf(rims, index));
            CollapseAll(rings, target);

            // The welds as they were made, before any corner is put on an edge.
            List<SolidRepair3> repairs = WeldRepairs(points, target);
            bool[] cornered = onEdges
                ? PutCornersOnOpenEdges(rings, points, within, weldable, faces, tolerance, repairs)
                : new bool[faces.Count];

            if (HasNeedle(rings, points, tolerance.EqualPoint))
            {
                return null;
            }

            var built = new List<GeoFace3>(faces.Count);
            var fresh = new List<bool>(faces.Count);
            var from = new List<int>(faces.Count);
            var changed = new List<GeoAabb3>();
            Tolerance pieces = LoopAssembly.ForPieces(tolerance);
            double touched = 0.0;
            double swept = 0.0;

            for (int f = 0; f < faces.Count; f++)
            {
                List<List<int>> faceRings = rings[f];

                if (!cornered[f] && Weld3.Unchanged(faces[f], faceRings, points))
                {
                    built.Add(faces[f]);
                    fresh.Add(false);
                    from.Add(origins[f]);
                    continue;
                }

                touched += faces[f].Area;
                changed.Add(faces[f].GetAabb());

                // A face whose corners came to fewer than three points covered nothing within the reach; the faces beside it
                // meet along its edges without it.
                GeoFace3[] made = faceRings[0].Count < 3 ? new GeoFace3[0] : Rebuilt(faceRings, points, pieces);
                swept += Swept(faces[f], made);

                if (made.Length == 0)
                {
                    repairs.Add(new SolidRepair3(SolidRepairKind.Drop, faces[f].Centroid, faces[f].Area));
                    continue;
                }

                foreach (GeoFace3 piece in made)
                {
                    built.Add(piece);
                    fresh.Add(true);
                    from.Add(origins[f]);
                    changed.Add(piece.GetAabb());
                }
            }

            // A face built again lying back to back with another is a skin of no thickness: a face folded under the one beside
            // it, where a corner of that one was threaded through it.
            if (changed.Count == 0 || built.Count < 4 || swept > within * touched || HasSkin(built, fresh, tolerance, out _))
            {
                return null;
            }

            List<Rim> left = OpenEdgesAfter(built, fresh, changed, rims, tolerance, work.Options.MaxGap, out GeoPoint3 fin, out List<Stretch> stretches, out List<int> localOf);

            if (left == null)
            {
                work.NoteFin(fin);
                return null;
            }

            // Nothing left open: every shell closes now, and is oriented again before the reach is judged.
            List<int> again = left.Count == 0 ? Reorient(work, built, from, stretches, localOf) : new List<int>();
            var body = new GeoSolid3(built, work.Solid.Openings);

            // Nothing left open: the body is checked whole, the volume and all, and taken where it is valid with no ring
            // doubling back.
            bool valid = left.Count == 0 && body.Validate(tolerance).IsValid;

            if (valid && NeedleAt(built, tolerance.EqualPoint, out _))
            {
                return null;
            }

            return new Welded(built, from, repairs, body, valid, left, Gaps(left), again);
        }

        /// <summary>
        /// The edges a body built again leaves open: where its faces changed, read again from the faces reaching there; and
        /// elsewhere those left open before; null where a fin longer than the widest gap is left.
        /// </summary>
        /// <param name="built">The faces of the body built again.</param>
        /// <param name="fresh">For each, whether it was built again.</param>
        /// <param name="changed">The boxes of the faces that changed, as they were and as they are.</param>
        /// <param name="before">The edges left open before.</param>
        /// <param name="tolerance">The tolerance edges are matched within.</param>
        /// <param name="maxGap">The widest gap.</param>
        /// <param name="fin">The middle of the fin left, where one is; the origin otherwise.</param>
        /// <param name="stretches">The stretches of the faces read again, by their places among those faces.</param>
        /// <param name="localOf">For each face read again, its place among the faces of the body.</param>
        /// <remarks>
        /// A stretch of edge lies where its faces' boxes are, and an edge the faces that changed reach is an edge of a face
        /// whose box meets one of theirs: the stretches those faces run in the boxes of the faces that changed are the body's
        /// there, matched as the whole body would match them, and away from them nothing changed. The boxes are filed, each
        /// on its own, so that faces changed in many places, as copies of corners all over a sphere, are read where they
        /// changed and not across the box round them all (see <see cref="FaceBoxes"/>).
        /// </remarks>
        private static List<Rim> OpenEdgesAfter(List<GeoFace3> built, List<bool> fresh, List<GeoAabb3> changed, List<Rim> before, Tolerance tolerance, double maxGap, out GeoPoint3 fin, out List<Stretch> stretches, out List<int> localOf)
        {
            var filed = new FaceBoxes(changed.ToArray(), null, tolerance);
            var near = new List<int>();
            var local = new List<GeoFace3>();
            localOf = new List<int>();

            for (int f = 0; f < built.Count; f++)
            {
                if (fresh[f] || Meets(filed, built[f].GetAabb(), near))
                {
                    local.Add(built[f]);
                    localOf.Add(f);
                }
            }

            stretches = FindStretches(local, new bool[local.Count], tolerance);
            var inside = new List<Stretch>(stretches.Count);

            foreach (Stretch stretch in stretches)
            {
                if (Holds(filed, changed, stretch.Middle, tolerance, near))
                {
                    inside.Add(stretch);
                }
            }

            if (!TryReadOpenEdges(inside, null, null, maxGap, out List<Rim> read, out fin))
            {
                return null;
            }

            var left = new List<Rim>(read.Count + before.Count);

            foreach (Rim rim in read)
            {
                left.Add(rim.Copy());
            }

            foreach (Rim rim in before)
            {
                if (!Holds(filed, changed, rim.From.GetMiddlePoint(rim.To), tolerance, near))
                {
                    left.Add(rim.Copy());
                }
            }

            MarkGaps(left, CrackGaps * maxGap);
            return left;
        }

        /// <summary>
        /// Determines whether a box meets any of some boxes filed, within the point tolerance they were filed with.
        /// </summary>
        /// <param name="filed">The boxes, filed.</param>
        /// <param name="box">The box.</param>
        /// <param name="near">A list to work in.</param>
        private static bool Meets(FaceBoxes filed, GeoAabb3 box, List<int> near)
        {
            filed.Meeting(box, near);
            return near.Count > 0;
        }

        /// <summary>
        /// Determines whether any of some boxes filed holds a point, within a tolerance.
        /// </summary>
        /// <param name="filed">The boxes, filed.</param>
        /// <param name="boxes">The boxes, by the index they were filed by.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="near">A list to work in.</param>
        private static bool Holds(FaceBoxes filed, List<GeoAabb3> boxes, GeoPoint3 point, Tolerance tolerance, List<int> near)
        {
            filed.Meeting(new GeoAabb3(point, point), near);

            foreach (int b in near)
            {
                if (boxes[b].Contains(point, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// For each corner, whether it is a corner of an edge an odd number of rings run, corner for corner.
        /// </summary>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="count">How many positions there are.</param>
        private static bool[] Unpaired(List<List<int>>[] rings, int count)
        {
            var counts = new Dictionary<long, int>(EdgeKeys.Instance);

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

            var unpaired = new bool[count];

            foreach (KeyValuePair<long, int> edge in counts)
            {
                if (edge.Value % 2 != 0)
                {
                    unpaired[(int)(edge.Key >> 32)] = true;
                    unpaired[(int)(edge.Key & 0xFFFFFFFFL)] = true;
                }
            }

            return unpaired;
        }

        /// <summary>
        /// The faces each of some corners is a corner of, by index, in order; null for the others.
        /// </summary>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="of">For each position, whether its faces are wanted.</param>
        private static List<int>[] FacesOfCorners(List<List<int>>[] rings, bool[] of)
        {
            var faces = new List<int>[of.Length];

            for (int f = 0; f < rings.Length; f++)
            {
                foreach (List<int> ring in rings[f])
                {
                    foreach (int id in ring)
                    {
                        if (!of[id])
                        {
                            continue;
                        }

                        List<int> these = faces[id] ?? (faces[id] = new List<int>(3));

                        if (these.Count == 0 || these[these.Count - 1] != f)
                        {
                            these.Add(f);
                        }
                    }
                }
            }

            return faces;
        }

        /// <summary>
        /// The position each corner is welded to: the corners that may move within the reach of each other made one group,
        /// the nearest two first, two corners of one face only where they are neighbours on a ring of it across an edge left
        /// open (see <see cref="MayShareFaces"/>), and each group going to the corner of it that takes the most of it within
        /// the reach and of those moves the volume least, the first of those; the rest of a group settled the same way among
        /// themselves.
        /// </summary>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="faces">The faces each corner that may move is a corner of.</param>
        /// <param name="bends">For each corner that may move, the faces it would bend moved off their planes, each its normal
        /// times its area; see <see cref="Bends"/>.</param>
        /// <param name="reach">The reach.</param>
        /// <param name="movable">For each position, whether it may move.</param>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="openEdges">The edges of the faces that read open, by the keys of their two positions; see <see cref="EdgeKey"/>.</param>
        private static int[] Weldings(List<GeoPoint3> points, List<int>[] faces, List<GeoVector3>[] bends, double reach, bool[] movable, List<List<int>>[] rings, HashSet<long> openEdges)
        {
            int count = points.Count;
            var to = new int[count];
            var parent = new int[count];

            for (int i = 0; i < count; i++)
            {
                to[i] = i;
                parent[i] = i;
            }

            var ids = new List<int>();
            var cells = new Dictionary<(long, long, long), List<int>>();

            for (int i = 0; i < count; i++)
            {
                if (!movable[i])
                {
                    continue;
                }

                ids.Add(i);
                GeoPoint3 p = points[i];
                var cell = (CellOf(p.X, reach), CellOf(p.Y, reach), CellOf(p.Z, reach));

                if (!cells.TryGetValue(cell, out List<int> filed))
                {
                    filed = new List<int>();
                    cells.Add(cell, filed);
                }

                filed.Add(i);
            }

            double reachSquared = reach * reach;
            var pairs = new List<(double Squared, int A, int B)>();

            foreach (int a in ids)
            {
                GeoPoint3 p = points[a];
                long x = CellOf(p.X, reach), y = CellOf(p.Y, reach), z = CellOf(p.Z, reach);

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            if (!cells.TryGetValue((x + dx, y + dy, z + dz), out List<int> filed))
                            {
                                continue;
                            }

                            foreach (int b in filed)
                            {
                                double squared = SquaredDistance(p, points[b]);

                                if (b > a && squared <= reachSquared)
                                {
                                    pairs.Add((squared, a, b));
                                }
                            }
                        }
                    }
                }
            }

            pairs.Sort((p, q) =>
            {
                int bySquared = p.Squared.CompareTo(q.Squared);

                if (bySquared != 0)
                {
                    return bySquared;
                }

                int byA = p.A.CompareTo(q.A);
                return byA != 0 ? byA : p.B.CompareTo(q.B);
            });

            // The faces of the corners of each group, by its root.
            var faceSets = new Dictionary<int, HashSet<int>>();

            HashSet<int> FacesOf(int root)
            {
                if (!faceSets.TryGetValue(root, out HashSet<int> set))
                {
                    set = new HashSet<int>(faces[root] ?? new List<int>());
                    faceSets.Add(root, set);
                }

                return set;
            }

            foreach ((double _, int a, int b) in pairs)
            {
                int ra = Find(parent, a);
                int rb = Find(parent, b);

                if (ra == rb)
                {
                    continue;
                }

                HashSet<int> fa = FacesOf(ra);
                HashSet<int> fb = FacesOf(rb);

                // Two corners of one face are made one only where they are copies of a corner of it, beside each other.
                if (fa.Overlaps(fb) && !MayShareFaces(fa, fb, ra, rb, parent, rings, openEdges))
                {
                    continue;
                }

                int low = Math.Min(ra, rb);
                int high = Math.Max(ra, rb);
                parent[high] = low;
                HashSet<int> kept = low == ra ? fa : fb;
                kept.UnionWith(low == ra ? fb : fa);
                faceSets[low] = kept;
                faceSets.Remove(high);
            }

            // The groups, each in the order of its corners, in the order of their first corners.
            var groups = new Dictionary<int, List<int>>();
            var roots = new List<int>();

            foreach (int id in ids)
            {
                int root = Find(parent, id);

                if (!groups.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    groups.Add(root, members);
                    roots.Add(root);
                }

                members.Add(id);
            }

            foreach (int root in roots)
            {
                List<int> left = groups[root];

                while (left.Count > 1)
                {
                    int chosen = left[0];
                    int mostTaken = -1;
                    double leastMoved = double.MaxValue;

                    foreach (int candidate in left)
                    {
                        Weigh(candidate, left, points, bends, reachSquared, out int taken, out double moved);

                        if (taken > mostTaken || (taken == mostTaken && moved < leastMoved))
                        {
                            chosen = candidate;
                            mostTaken = taken;
                            leastMoved = moved;
                        }
                    }

                    var rest = new List<int>();

                    foreach (int m in left)
                    {
                        if (SquaredDistance(points[m], points[chosen]) <= reachSquared)
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
        /// How many corners of a group stand within the reach of one of them, and how much moving those to it bends the
        /// faces they are corners of: each face its area times how far the corner moves off its plane.
        /// </summary>
        /// <param name="candidate">The corner, by position.</param>
        /// <param name="group">The group.</param>
        /// <param name="points">Every corner, by position.</param>
        /// <param name="corners">For each corner of the group, the faces it would bend, each its normal times its area.</param>
        /// <param name="reachSquared">The square of the reach.</param>
        /// <param name="taken">How many stand within the reach of it, itself included.</param>
        /// <param name="moved">How much moving them bends their faces.</param>
        /// <remarks>
        /// A corner moved off a face's plane tilts the whole face, not the triangle at the corner: weighed by the triangle, a
        /// cap of a thousand corners, its triangle at a corner a sliver, gave way to the sides 0.06 wide beside it and was
        /// tilted out of flat, where weighed by its area it stays, and the sides bend.
        /// </remarks>
        private static void Weigh(int candidate, List<int> group, List<GeoPoint3> points, List<GeoVector3>[] corners, double reachSquared, out int taken, out double moved)
        {
            taken = 0;
            moved = 0.0;
            GeoPoint3 onto = points[candidate];

            foreach (int m in group)
            {
                if (SquaredDistance(points[m], onto) > reachSquared)
                {
                    continue;
                }

                taken++;

                if (m == candidate)
                {
                    continue;
                }

                GeoVector3 shift = points[m].GetVectorTo(onto);

                if (corners[m] == null)
                {
                    continue;
                }

                foreach (GeoVector3 corner in corners[m])
                {
                    moved += Math.Abs(shift.DotProduct(corner));
                }
            }
        }

        /// <summary>
        /// For each corner that may move, the faces it would bend moved off their planes, each its normal times its area;
        /// null for the others.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="movable">For each position, whether it may move.</param>
        private static List<GeoVector3>[] Bends(List<GeoFace3> faces, List<List<int>>[] rings, bool[] movable)
        {
            var bends = new List<GeoVector3>[movable.Length];

            for (int f = 0; f < faces.Count; f++)
            {
                GeoVector3 bend = faces[f].Normal.Multiply(faces[f].Area);

                foreach (List<int> ring in rings[f])
                {
                    foreach (int id in ring)
                    {
                        if (movable[id])
                        {
                            (bends[id] ?? (bends[id] = new List<GeoVector3>(3))).Add(bend);
                        }
                    }
                }
            }

            return bends;
        }

        /// <summary>
        /// The edges of the faces that read open, by the keys of their two positions: the edge of a face each edge left open
        /// lies along, a piece of a gap more than two faces run left out.
        /// </summary>
        /// <param name="rims">The edges left open.</param>
        /// <param name="index">The position of each corner.</param>
        private static HashSet<long> OpenEdgesOf(List<Rim> rims, Dictionary<GeoPoint3, int> index)
        {
            var open = new HashSet<long>(EdgeKeys.Instance);

            foreach (Rim rim in rims)
            {
                if (!rim.IsFin && index.TryGetValue(rim.EdgeStart, out int start) && index.TryGetValue(rim.EdgeEnd, out int end) && start != end)
                {
                    open.Add(EdgeKey(start, end));
                }
            }

            return open;
        }

        /// <summary>
        /// Determines whether two groups of corners that share faces may be made one: in each face they share, they lie on
        /// one ring of it and no other, every edge of that ring from a corner of the one to a corner of the other reads open,
        /// and the ring keeps at least three corners of different groups, none of them twice.
        /// </summary>
        /// <param name="fa">The faces of the one group.</param>
        /// <param name="fb">The faces of the other.</param>
        /// <param name="ra">The one group, by its root.</param>
        /// <param name="rb">The other.</param>
        /// <param name="parent">The parent of each position, as the groups so far make them.</param>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="openEdges">The edges of the faces that read open.</param>
        /// <remarks>
        /// Two corners of one face are no copies of each other as a rule: made one, they pinch the face, and a slot whose
        /// mouth stands open beside a gap is closed across its mouth. But where a face runs through two copies of one corner,
        /// beside each other on its ring, the edge between them is no edge of the body: a top whose ring runs through a corner
        /// and a copy of it 0.003 off, or the top and bottom spanning a crack through the front by an edge as long as the crack
        /// is wide, close where the two are made one, and the face keeps its shape.
        /// </remarks>
        private static bool MayShareFaces(HashSet<int> fa, HashSet<int> fb, int ra, int rb, int[] parent, List<List<int>>[] rings, HashSet<long> openEdges)
        {
            foreach (int f in fa)
            {
                if (!fb.Contains(f))
                {
                    continue;
                }

                List<int> along = null;

                foreach (List<int> ring in rings[f])
                {
                    bool touches = false;

                    foreach (int id in ring)
                    {
                        int root = Find(parent, id);

                        if (root == ra || root == rb)
                        {
                            touches = true;
                            break;
                        }
                    }

                    if (!touches)
                    {
                        continue;
                    }

                    // On two rings of the face, made one they would join the rings.
                    if (along != null)
                    {
                        return false;
                    }

                    along = ring;
                }

                if (along == null || !KeepsRing(along, ra, rb, parent, openEdges))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a ring keeps its shape with two groups of corners on it made one: every edge from a corner of
        /// the one to a corner of the other reads open, there is one, and what is left of the ring has at least three corners,
        /// no group of them twice.
        /// </summary>
        /// <param name="ring">The ring, by position.</param>
        /// <param name="ra">The one group, by its root.</param>
        /// <param name="rb">The other.</param>
        /// <param name="parent">The parent of each position.</param>
        /// <param name="openEdges">The edges of the faces that read open.</param>
        private static bool KeepsRing(List<int> ring, int ra, int rb, int[] parent, HashSet<long> openEdges)
        {
            int count = ring.Count;
            var roots = new int[count];
            bool across = false;

            for (int i = 0; i < count; i++)
            {
                roots[i] = Find(parent, ring[i]);
            }

            for (int i = 0; i < count; i++)
            {
                int p = roots[i], q = roots[(i + 1) % count];

                if ((p == ra && q == rb) || (p == rb && q == ra))
                {
                    if (!openEdges.Contains(EdgeKey(ring[i], ring[(i + 1) % count])))
                    {
                        return false;
                    }

                    across = true;
                }
            }

            if (!across)
            {
                return false;
            }

            var seen = new HashSet<int>();
            int kept = 0;

            for (int i = 0; i < count; i++)
            {
                int here = roots[i] == rb ? ra : roots[i];
                int before = roots[(i + count - 1) % count] == rb ? ra : roots[(i + count - 1) % count];

                if (here == before)
                {
                    continue;
                }

                if (!seen.Add(here))
                {
                    return false;
                }

                kept++;
            }

            return kept >= 3;
        }

        /// <summary>
        /// The square of the distance between two points.
        /// </summary>
        /// <param name="a">The one point.</param>
        /// <param name="b">The other.</param>
        private static double SquaredDistance(GeoPoint3 a, GeoPoint3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// Hashes the key of an edge between two positions so that edges between neighbouring positions spread: the key's
        /// own hash folds its two halves together, and the edges of a ring from one position to the next fold to a few.
        /// </summary>
        private sealed class EdgeKeys : IEqualityComparer<long>
        {
            /// <summary>Gets the one instance.</summary>
            internal static EdgeKeys Instance { get; } = new EdgeKeys();

            /// <inheritdoc/>
            public bool Equals(long x, long y) => x == y;

            /// <inheritdoc/>
            public int GetHashCode(long key) => unchecked((int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32));
        }

        /// <summary>
        /// The welds a reach made: one for each group of corners made one, at the corner they went to and as large as the
        /// furthest of them moved, in the order of the corners they went to.
        /// </summary>
        /// <param name="points">Every corner, by position, where it was.</param>
        /// <param name="target">The position each went to.</param>
        private static List<SolidRepair3> WeldRepairs(List<GeoPoint3> points, int[] target)
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
        /// where they lie flat within the planar tolerance of the plane through their middle (see <see cref="LiesFlat"/>),
        /// triangles on its own corners where they do not.
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

            IEnumerable<GeoPoint3> boundary = Weld3.Corners(faceRings[0], points);

            if (LiesFlat(boundary, holes, pieces.EqualPlanar) && TryOneFace(boundary, holes, LoopAssembly.ForFacePieces(pieces), out GeoFace3 face))
            {
                return new[] { face };
            }

            return Loops3.ToFaces(boundary, holes, pieces);
        }

        /// <summary>
        /// Determines whether every corner of a face's rings stands within the planar tolerance of the plane through the
        /// average of its boundary's corners, square to the boundary's area.
        /// </summary>
        /// <param name="boundary">The corners of the boundary, in order.</param>
        /// <param name="holes">The corners of each hole.</param>
        /// <param name="planar">The planar tolerance.</param>
        /// <remarks>
        /// A polygon measures how flat it is from its own first corner, and from a corner a hair one way a corner a hair the
        /// other stands off by twice that: a cap of a thousand corners welded back within 0.0008 of its plane, measured so,
        /// would be no face, and broken into triangles, a sliver along its rim at every corner. Measured from the plane through
        /// its middle, it stays one face.
        /// </remarks>
        private static bool LiesFlat(IEnumerable<GeoPoint3> boundary, List<IEnumerable<GeoPoint3>> holes, double planar)
        {
            var corners = new List<GeoPoint3>(boundary);

            if (corners.Count < 3)
            {
                return false;
            }

            GeoPoint3 first = corners[0];
            GeoVector3 area = GeoVector3.Zero;
            GeoVector3 sum = GeoVector3.Zero;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoVector3 here = first.GetVectorTo(corners[i]);
                area = area.Add(here.CrossProduct(first.GetVectorTo(corners[(i + 1) % corners.Count])));
                sum = sum.Add(here);
            }

            double length = area.Length;

            if (!(length > 0.0))
            {
                return false;
            }

            GeoVector3 normal = area.Divide(length);
            GeoPoint3 middle = first.Add(sum.Divide(corners.Count));

            foreach (GeoPoint3 corner in corners)
            {
                if (Math.Abs(middle.GetVectorTo(corner).DotProduct(normal)) > planar)
                {
                    return false;
                }
            }

            foreach (IEnumerable<GeoPoint3> hole in holes)
            {
                foreach (GeoPoint3 corner in hole)
                {
                    if (Math.Abs(middle.GetVectorTo(corner).DotProduct(normal)) > planar)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Builds one face of a boundary and its holes within a tolerance; false where it is refused.
        /// </summary>
        /// <param name="boundary">The corners of the boundary, in order.</param>
        /// <param name="holes">The corners of each hole.</param>
        /// <param name="build">The tolerance.</param>
        /// <param name="face">The face; null when the method returns false.</param>
        private static bool TryOneFace(IEnumerable<GeoPoint3> boundary, List<IEnumerable<GeoPoint3>> holes, Tolerance build, out GeoFace3 face)
        {
            try
            {
                var rings = new List<GeoPolygon3>(holes.Count);

                foreach (IEnumerable<GeoPoint3> hole in holes)
                {
                    rings.Add(new GeoPolygon3(hole, build));
                }

                face = new GeoFace3(new GeoPolygon3(boundary, build), rings, build);
                return true;
            }
            catch (ArgumentException)
            {
                face = null;
                return false;
            }
        }

        /// <summary>
        /// Puts each corner of an edge left open that stands within a reach of another edge left open, between its ends, on
        /// that edge, each of two corners as near the start of an edge as the other taken in the order of their positions:
        /// moved onto it within the plane of its own face where the edge lies in that plane, and the edge split through it
        /// where it stands otherwise. The rings and the corners are changed in place.
        /// </summary>
        /// <param name="rings">The rings of each face, by position.</param>
        /// <param name="points">Every corner, by position; a corner moved onto an edge is moved here.</param>
        /// <param name="reach">The reach.</param>
        /// <param name="open">For each position, whether it is a corner of an edge left open, as the check matches edges: only
        /// these are put on edges, and only edges between two of them split.</param>
        /// <param name="faces">The faces, by index, as they were given to the step.</param>
        /// <param name="tolerance">The tolerance, its planar part saying what lies in a face's plane.</param>
        /// <param name="repairs">The changes so far, each corner put on an edge added, as far as it stood off the edge.</param>
        /// <returns>For each face, whether a corner was put on an edge of it.</returns>
        /// <remarks>
        /// <para>
        /// Every corner is put within the reach of an edge as it stands after the welds, and the pieces a split leaves are
        /// not split again: put on the pieces of a split, a corner could stand within the reach of a piece and not of the
        /// edge, and a gap wider than the reach would close by steps no wider.
        /// </para>
        /// <para>
        /// Where a crack lies in the plane of one face, its corners standing off the edge of the face beside it within that
        /// plane, they are moved onto the edge, within their own face's plane, and the edge is split at them: both faces
        /// stay flat. Split through a corner where it stands, the edge would bend out of its face's plane along the face
        /// the corner is of, and the face built again on its corners would fold under that one as flat triangles lying
        /// against it, a skin of no thickness. A corner is moved so only where the edge lies in the plane of every face it is
        /// a corner of, and it is put on no other edge; otherwise it stays, and the edge is split through it: where it lies
        /// in the plane of the edge's face, which stays flat, and where the crack is twisted between the two, which bends.
        /// </para>
        /// </remarks>
        private static bool[] PutCornersOnOpenEdges(List<List<int>>[] rings, List<GeoPoint3> points, double reach, bool[] open, List<GeoFace3> faces, Tolerance tolerance, List<SolidRepair3> repairs)
        {
            var cornered = new bool[rings.Length];
            var counts = new Dictionary<long, int>(EdgeKeys.Instance);

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

            // Every edge to be split and the corners to be put on it, each edge as it stands, before any is split.
            var splits = new List<(int Face, int Ring, int Index, List<int> On)>();
            var edgesOf = new Dictionary<int, int>();

            for (int f = 0; f < rings.Length; f++)
            {
                List<List<int>> faceRings = rings[f];

                for (int r = 0; r < faceRings.Count; r++)
                {
                    List<int> ring = faceRings[r];

                    for (int i = 0; i < ring.Count; i++)
                    {
                        int from = ring[i];
                        int next = ring[(i + 1) % ring.Count];
                        bool unpaired = counts[EdgeKey(from, next)] % 2 != 0 && marked.Contains(from) && marked.Contains(next);
                        List<int> on = unpaired ? CornersOnEdge(points, from, next, sorted, reach) : null;

                        if (on == null)
                        {
                            continue;
                        }

                        splits.Add((f, r, i, on));

                        foreach (int corner in on)
                        {
                            edgesOf[corner] = (edgesOf.TryGetValue(corner, out int seen) ? seen : 0) + 1;
                        }
                    }
                }
            }

            if (splits.Count == 0)
            {
                return cornered;
            }

            // The faces each corner to be put on an edge is a corner of.
            var facesOf = new Dictionary<int, List<int>>();

            for (int f = 0; f < rings.Length; f++)
            {
                foreach (List<int> ring in rings[f])
                {
                    foreach (int id in ring)
                    {
                        if (!edgesOf.ContainsKey(id))
                        {
                            continue;
                        }

                        if (!facesOf.TryGetValue(id, out List<int> of))
                        {
                            of = new List<int>(3);
                            facesOf.Add(id, of);
                        }

                        if (!of.Contains(f))
                        {
                            of.Add(f);
                        }
                    }
                }
            }

            var planes = new Dictionary<int, GeoPlane3>();
            var moveTo = new Dictionary<int, GeoPoint3>();

            foreach ((int f, int r, int i, List<int> on) in splits)
            {
                List<int> ring = rings[f][r];
                GeoPoint3 start = points[ring[i]];
                GeoPoint3 end = points[ring[(i + 1) % ring.Count]];

                foreach (int corner in on)
                {
                    GeoPoint3 foot = NearestOnSegment(start, end, points[corner]);
                    bool moved = edgesOf[corner] == 1 && LiesInPlanesOf(start, end, facesOf[corner], faces, planes, tolerance.EqualPlanar);

                    if (moved)
                    {
                        moveTo[corner] = foot;
                    }

                    repairs.Add(new SolidRepair3(SolidRepairKind.SplitEdge, moved ? foot : points[corner], points[corner].DistanceTo(foot)));
                }
            }

            // Each ring split at its edges, the corners in order along each.
            int s = 0;

            while (s < splits.Count)
            {
                int f = splits[s].Face;
                int r = splits[s].Ring;
                List<int> ring = rings[f][r];
                var split = new List<int>(ring.Count + 4);
                int k = s;

                for (int i = 0; i < ring.Count; i++)
                {
                    split.Add(ring[i]);

                    if (k < splits.Count && splits[k].Face == f && splits[k].Ring == r && splits[k].Index == i)
                    {
                        split.AddRange(splits[k].On);
                        k++;
                    }
                }

                rings[f][r] = split;
                cornered[f] = true;
                s = k;
            }

            foreach (KeyValuePair<int, GeoPoint3> move in moveTo)
            {
                points[move.Key] = move.Value;
            }

            return cornered;
        }

        /// <summary>
        /// Determines whether both ends of an edge lie within the planar tolerance of the plane of each of some faces.
        /// </summary>
        /// <param name="start">Where the edge starts.</param>
        /// <param name="end">Where it ends.</param>
        /// <param name="of">The faces, by index.</param>
        /// <param name="faces">Every face, by index.</param>
        /// <param name="planes">The planes of the faces asked about so far, by index, added to here.</param>
        /// <param name="planar">The planar tolerance.</param>
        private static bool LiesInPlanesOf(GeoPoint3 start, GeoPoint3 end, List<int> of, List<GeoFace3> faces, Dictionary<int, GeoPlane3> planes, double planar)
        {
            foreach (int f in of)
            {
                if (!planes.TryGetValue(f, out GeoPlane3 plane))
                {
                    plane = faces[f].GetPlane();
                    planes.Add(f, plane);
                }

                if (Math.Abs(plane.SignedDistanceTo(start)) > planar || Math.Abs(plane.SignedDistanceTo(end)) > planar)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The cosine of the angle within which the normals of two faces are taken as facing straight at each other: about
        /// two and a half degrees.
        /// </summary>
        private const double Facing = -0.999;

        /// <summary>
        /// Finds a face new to a body lying back to back with another, or another with it: the two facing opposite ways, the
        /// middle of one on the other, a skin of no thickness that reads valid and holds no material.
        /// </summary>
        /// <param name="faces">The faces of the body.</param>
        /// <param name="fresh">For each, whether it is new to the body, built or turned by the closing.</param>
        /// <param name="tolerance">The tolerance a middle lies on a face within.</param>
        /// <param name="at">The middle that lies on the other face; the origin where there is none.</param>
        /// <returns>true where a face new to the body lies back to back with another.</returns>
        /// <remarks>
        /// Faces the body was given lying back to back are its own, as a sheet inside it is, and are not looked at: only a
        /// pair one of which is new. The middle of a face is its centroid, as the boxes of the two first say may lie on it,
        /// and a middle lying on a face lies in its box, so each face new is set only against those whose boxes meet its own
        /// (see <see cref="FaceBoxes"/>): a sphere of six thousand faces each built again is not six thousand times read.
        /// </remarks>
        private static bool HasSkin(IReadOnlyList<GeoFace3> faces, IReadOnlyList<bool> fresh, Tolerance tolerance, out GeoPoint3 at)
        {
            int count = faces.Count;
            var boxes = new GeoAabb3[count];
            var middles = new GeoPoint3?[count];
            bool any = false;

            for (int f = 0; f < count; f++)
            {
                boxes[f] = faces[f].GetAabb();
                any |= fresh[f];
            }

            if (!any)
            {
                at = GeoPoint3.Origin;
                return false;
            }

            GeoPoint3 MiddleOf(int f) => (middles[f] ?? (middles[f] = faces[f].Centroid)).Value;

            var filed = new FaceBoxes(boxes, null, tolerance);
            var near = new List<int>();

            for (int f = 0; f < count; f++)
            {
                if (!fresh[f])
                {
                    continue;
                }

                GeoVector3 normal = faces[f].Normal;
                filed.Meeting(boxes[f], near);

                foreach (int g in near)
                {
                    if (g == f || !(normal.DotProduct(faces[g].Normal) < Facing))
                    {
                        continue;
                    }

                    if (boxes[g].Contains(MiddleOf(f), tolerance) && faces[g].Locate(MiddleOf(f), tolerance) != PointLocation.OutSide)
                    {
                        at = MiddleOf(f);
                        return true;
                    }

                    // A pair of two new faces is looked at from each.
                    if (!fresh[g] && boxes[f].Contains(MiddleOf(g), tolerance) && faces[f].Locate(MiddleOf(g), tolerance) != PointLocation.OutSide)
                    {
                        at = MiddleOf(g);
                        return true;
                    }
                }
            }

            at = GeoPoint3.Origin;
            return false;
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
        /// <remarks>
        /// A corner within the point tolerance of the edge is put on it too: its own edges need not run along the edge, as
        /// where the top of a prism has more corners than its sides, a side's corner 0.0006 off the top's edge, and the top's
        /// edge split there is what pairs it with the sides' edges.
        /// </remarks>
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
            /// <param name="origins">For each, the face given it comes of, by index.</param>
            /// <param name="repairs">What was done, in order.</param>
            /// <param name="body">The body of the faces.</param>
            /// <param name="valid">Whether the body is valid, as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, with no ring doubling back.</param>
            /// <param name="rims">The edges it leaves open; none where it is valid.</param>
            /// <param name="gaps">How many of them are sides of gaps.</param>
            /// <param name="again">The faces given whose faces the reach turned over again, by index, in order.</param>
            internal Welded(List<GeoFace3> faces, List<int> origins, List<SolidRepair3> repairs, GeoSolid3 body, bool valid, List<Rim> rims, int gaps, List<int> again)
            {
                Again = again;
                Faces = faces;
                Origins = origins;
                Repairs = repairs;
                Body = body;
                IsValid = valid;
                Rims = rims;
                Gaps = gaps;
            }

            /// <summary>Gets the faces.</summary>
            internal List<GeoFace3> Faces { get; }

            /// <summary>Gets, for each face, the face given it comes of, by index: a face built again comes of the one it was.</summary>
            internal List<int> Origins { get; }

            /// <summary>Gets what was done, in order.</summary>
            internal List<SolidRepair3> Repairs { get; }

            /// <summary>Gets the body of the faces.</summary>
            internal GeoSolid3 Body { get; }

            /// <summary>Gets whether the body is valid, with no ring doubling back.</summary>
            internal bool IsValid { get; }

            /// <summary>Gets the edges it leaves open; none where it is valid.</summary>
            internal List<Rim> Rims { get; }

            /// <summary>Gets how many of them are sides of gaps.</summary>
            internal int Gaps { get; }

            /// <summary>
            /// Gets the faces given whose faces the reach turned over again, oriented once every shell closed, by index, in
            /// order: each to be reported turned, or no longer turned, where the reach is taken.
            /// </summary>
            internal List<int> Again { get; }
        }
    }
}
