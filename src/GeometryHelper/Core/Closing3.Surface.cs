using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        #region Faces of no area, and copies

        /// <summary>
        /// Drops each face that covers nothing within the tolerance: a needle or a point, every corner of it within the point
        /// tolerance of one line.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <remarks>
        /// Such a face runs along its line and back, so it neither opens nor closes the body; but lying along an open edge it
        /// puts three edges on it, which reads as a fin.
        /// </remarks>
        private static void DropFacesOfNoArea(Work work)
        {
            for (int f = 0; f < work.Faces.Count; f++)
            {
                if (CoversNothing(work.Faces[f].Boundary, work.Tolerance.EqualPoint))
                {
                    Drop(work, f);
                }
            }
        }

        /// <summary>
        /// Determines whether every corner of a ring stands within a distance of the line through two corners of it furthest
        /// apart, as far as the corner furthest from its first corner and the corner furthest from that one tell.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <param name="reach">The distance.</param>
        private static bool CoversNothing(GeoPolygon3 ring, double reach)
        {
            IReadOnlyList<GeoPoint3> corners = ring.Vertices;
            GeoPoint3 one = Furthest(corners, corners[0]);
            GeoPoint3 other = Furthest(corners, one);
            GeoVector3 along = one.GetVectorTo(other);
            double length = along.Length;

            // No corner further from the one than the other is: a point.
            if (!(length > reach))
            {
                return true;
            }

            GeoVector3 unit = along.Divide(length);

            foreach (GeoPoint3 corner in corners)
            {
                if (OffLine(one, unit, corner) > reach)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The corner furthest from a point, the first of them where two are as far.
        /// </summary>
        /// <param name="corners">The corners.</param>
        /// <param name="from">The point.</param>
        private static GeoPoint3 Furthest(IReadOnlyList<GeoPoint3> corners, GeoPoint3 from)
        {
            GeoPoint3 furthest = from;
            double most = -1.0;

            foreach (GeoPoint3 corner in corners)
            {
                double distance = from.GetVectorTo(corner).LengthSquared;

                if (distance > most)
                {
                    most = distance;
                    furthest = corner;
                }
            }

            return furthest;
        }

        /// <summary>
        /// Drops a face, and says so.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="face">The face, by index.</param>
        private static void Drop(Work work, int face)
        {
            GeoFace3 dropped = work.Faces[face];
            work.Dropped[face] = true;
            work.Repairs.Add(new SolidRepair3(SolidRepairKind.Drop, dropped.Centroid, dropped.Area));
        }

        /// <summary>
        /// Takes each face given more than once, on the same corners either way round, once or not at all, as the faces round
        /// it want it.
        /// </summary>
        /// <param name="work">The work, its stretches found.</param>
        /// <remarks>
        /// <para>
        /// A face given twice the same way round puts three edges on each of its edges, two running it one way, and the body
        /// reads open; the copy goes, the face first given staying. A face and a copy of it turned over lie back to back, and
        /// read the same; the one wound alike with the faces round it, which run each of its edges the other way, stays, and
        /// the one wound against them goes, whichever comes first. Two faces back to back with no face round them are a sheet
        /// holding nothing, and both go. Two faces back to back between two blocks, the wall they share, each close one of
        /// them, and both stay; see <see cref="Settle"/>.
        /// </para>
        /// <para>
        /// The faces are filed by the low corner of their box, and each compared only with the later faces filed near it, by
        /// its corners within the tolerance: two copies have the same box.
        /// </para>
        /// </remarks>
        private static void DropCopies(Work work)
        {
            IReadOnlyList<GeoFace3> faces = work.Faces;
            Tolerance tolerance = work.Tolerance;
            int count = faces.Count;
            var corners = new PointGrid(tolerance);

            for (int f = 0; f < count; f++)
            {
                if (!work.Dropped[f])
                {
                    corners.Add(faces[f].GetAabb().Min, f);
                }
            }

            var taken = new bool[count];
            var near = new List<int>();
            List<int>[] along = null;

            for (int i = 0; i < count; i++)
            {
                if (work.Dropped[i] || taken[i])
                {
                    continue;
                }

                corners.Near(faces[i].GetAabb().Min, near);
                List<int> group = null;
                List<bool> same = null;
                GeoFace3 turned = null;

                foreach (int j in near)
                {
                    if (j <= i || work.Dropped[j] || taken[j] || !SameShape(faces[i], faces[j]))
                    {
                        continue;
                    }

                    bool alike = faces[j].IsEqualTo(faces[i], tolerance);

                    if (!alike)
                    {
                        turned = turned ?? faces[i].Flip();

                        if (!faces[j].IsEqualTo(turned, tolerance))
                        {
                            continue;
                        }
                    }

                    if (group == null)
                    {
                        group = new List<int> { i };
                        same = new List<bool> { true };
                    }

                    group.Add(j);
                    same.Add(alike);
                    taken[j] = true;
                }

                if (group == null)
                {
                    continue;
                }

                along = along ?? StretchesOfFaces(work);
                Settle(work, group, same, along);
            }
        }

        /// <summary>
        /// Determines whether two faces have as many corners on their boundaries and as many holes.
        /// </summary>
        /// <param name="one">The one face.</param>
        /// <param name="other">The other.</param>
        private static bool SameShape(GeoFace3 one, GeoFace3 other)
            => one.Boundary.VertexCount == other.Boundary.VertexCount && one.Holes.Count == other.Holes.Count;

        /// <summary>
        /// The stretches each face has an edge on, by index, in order; null for a face with none.
        /// </summary>
        /// <param name="work">The work, its stretches found.</param>
        private static List<int>[] StretchesOfFaces(Work work)
        {
            var along = new List<int>[work.Faces.Count];

            for (int s = 0; s < work.Stretches.Count; s++)
            {
                foreach (int face in work.Stretches[s].Faces)
                {
                    List<int> of = along[face] ?? (along[face] = new List<int>());

                    if (of.Count == 0 || of[of.Count - 1] != s)
                    {
                        of.Add(s);
                    }
                }
            }

            return along;
        }

        /// <summary>
        /// Settles which of a group of faces on the same corners stay, by how the faces round them run their edges; see
        /// <see cref="DropCopies"/>.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="group">The faces, by index, lowest first.</param>
        /// <param name="same">For each, whether it runs the way the first does.</param>
        /// <param name="along">The stretches each face has an edge on.</param>
        /// <remarks>
        /// <para>
        /// What the faces round the group need of it is read stretch by stretch along the edges of its first face. Where
        /// an odd number of them run each stretch, one face of the group closes it, and one stays: the one wound alike with
        /// most of them, which run its edges the other way, the first face where as many are each way. A face round it
        /// wound the wrong way is turned after, and which of the copies stayed makes no difference to the body then, only
        /// to which change is reported. Where an even number run each, as round the wall two blocks share, one of each way
        /// round stays, and where none do, the group is a sheet holding nothing and goes whole, or, all one way round, a
        /// face on its own given more than once, and one stays. Where the stretches disagree, one of each way round stays,
        /// and the open edges say the rest.
        /// </para>
        /// </remarks>
        private static void Settle(Work work, List<int> group, List<bool> same, List<int>[] along)
        {
            int first = group[0];
            int alike = 0, against = 0;
            bool anyOdd = false, anyEven = false, anyRound = false;

            foreach (int s in along[first] ?? new List<int>())
            {
                Stretch stretch = work.Stretches[s];
                int at = Array.IndexOf(stretch.Faces, first);
                bool mine = stretch.Forward[at];
                int round = 0;

                for (int r = 0; r < stretch.Faces.Length; r++)
                {
                    int other = stretch.Faces[r];

                    if (work.Dropped[other] || group.Contains(other))
                    {
                        continue;
                    }

                    round++;

                    // A face wound alike with the first runs the stretch the other way.
                    if (stretch.Forward[r] != mine)
                    {
                        alike++;
                    }
                    else
                    {
                        against++;
                    }
                }

                anyOdd |= round % 2 != 0;
                anyEven |= round % 2 == 0;
                anyRound |= round > 0;
            }

            int firstTurned = -1;

            for (int k = 1; k < group.Count; k++)
            {
                if (!same[k])
                {
                    firstTurned = group[k];
                    break;
                }
            }

            var keep = new List<int>(2);

            if (anyOdd && !anyEven)
            {
                keep.Add(against > alike && firstTurned >= 0 ? firstTurned : first);
            }
            else if (anyRound || firstTurned < 0)
            {
                keep.Add(first);

                if (anyRound && firstTurned >= 0)
                {
                    keep.Add(firstTurned);
                }
            }

            foreach (int face in group)
            {
                if (!keep.Contains(face))
                {
                    Drop(work, face);
                }
            }
        }

        #endregion

        #region Turning the faces

        /// <summary>
        /// Turns faces over so that each shell is wound alike, and each faces outwards, or inwards where it lies inside an
        /// odd number of other shells, as a cavity does; false where a shell cannot be wound alike.
        /// </summary>
        /// <param name="work">The work, its stretches found and its copies dropped.</param>
        /// <returns>false, the trouble noted, where the faces of a shell cannot be wound alike whichever are turned.</returns>
        /// <remarks>
        /// <para>
        /// The faces are taken shell by shell, a shell being the faces joined by stretches two of them meet on. Two faces wound
        /// alike run such a stretch one each way, and where they run it the same way, one of them is to be turned over. Of
        /// the two sides that leaves in a shell, the one of less area is turned: the least change that winds the shell
        /// alike, the side of its first face staying where the two are as large. Where the stretches ask a face to be turned
        /// both ways, the shell cannot be wound alike: a surface with one side only. The stretches longer than the widest gap
        /// are read first, and they alone refuse: on a prism of a thousand sides each face on copies of its corners a
        /// thousandth or two apart, the bottom edges of two sides beside a corner run on so nearly straight that their copies
        /// come within the tolerance of one line for a thousandth or two, both running it the same way, and read so, they
        /// would turn a side over. A shorter stretch then joins two faces not joined yet only where it is the whole of an edge
        /// of each (see <see cref="IsWholeEdge"/>), and never refuses: a quad 0.004 a side wound against a patch of such quads
        /// in a top, closed within a gap of 0.005, has no stretch longer than the gap and is turned back all the same.
        /// </para>
        /// <para>
        /// Then the volume a closed shell encloses says which way it faces, each shell on its own: the faces of two shells,
        /// one wound each way, enclose nothing together, and the whole body would say nothing of either. Only a shell inside
        /// no other is turned so, where it encloses its volume inwards, and with it every shell inside it; a shell inside
        /// another keeps its winding against it, a block within it wound the same way and a cavity wound the other, since
        /// both read valid (see <see cref="TurnedOutwards"/>). Which shell is inside a closed one is read from where a point
        /// on it lies, by how much of the sphere round it the closed one's faces cover; a shell still open is taken to hold
        /// every shell within its box until it closes. A shell enclosing no more than the tolerance times its area, such as a
        /// face on its own, says nothing of which way it faces, and is left as it is; and so is a shell that is open, any of
        /// its stretches run an odd number of times, which keeps its winding as given until the welding or the filling closes
        /// it (see <see cref="Reorient"/> and <see cref="FaceShellsOutwards"/>), as is a shell within the box of one that is
        /// open.
        /// </para>
        /// <para>
        /// Every face turned over is one change, of its area, reported once the stray faces are dropped; see
        /// <see cref="ReportTurned"/>. The way a shell open by a hole faces is read again once the hole is filled; see
        /// <see cref="FaceShellsOutwards"/>.
        /// </para>
        /// </remarks>
        private static bool TryTurnAlike(Work work)
        {
            IReadOnlyList<GeoFace3> faces = work.Faces;
            int count = faces.Count;
            var parent = new int[count];
            var odd = new bool[count];

            for (int f = 0; f < count; f++)
            {
                parent[f] = f;
            }

            var live = new List<int>(4);
            double shortest = work.Options.MaxGap;
            var open = new bool[count];

            // The long stretches first, a face asked to be turned both ways refusing; then the short ones, only where a
            // stretch is the whole of an edge of both its faces and the two are not joined yet, so that none refuses.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Stretch stretch in work.Stretches)
                {
                    if (stretch.Start.DistanceTo(stretch.End) > shortest != (pass == 0))
                    {
                        continue;
                    }

                    live.Clear();

                    for (int r = 0; r < stretch.Faces.Length; r++)
                    {
                        if (!work.Dropped[stretch.Faces[r]])
                        {
                            live.Add(r);
                        }
                    }

                    // Run an odd number of times, the stretch leaves its faces' shell open.
                    if (live.Count % 2 != 0)
                    {
                        foreach (int r in live)
                        {
                            open[stretch.Faces[r]] = true;
                        }
                    }

                    int a = live.Count == 2 ? stretch.Faces[live[0]] : -1;
                    int b = live.Count == 2 ? stretch.Faces[live[1]] : -1;

                    if (a < 0 || a == b)
                    {
                        continue;
                    }

                    // Two faces wound alike run the stretch one each way; run the same way, one of them is to be turned.
                    bool apart = stretch.Forward[live[0]] == stretch.Forward[live[1]];

                    if (pass == 0)
                    {
                        if (!TryJoin(parent, odd, a, b, apart))
                        {
                            work.Refuse(ClosingFailure.NonManifold, stretch.Middle);
                            return false;
                        }
                    }
                    else if (FindOdd(parent, odd, a, out _) != FindOdd(parent, odd, b, out _) && IsWholeEdge(stretch, live, work.Tolerance.EqualPoint))
                    {
                        TryJoin(parent, odd, a, b, apart);
                    }
                }
            }

            // The shells, each in the order of its faces, in the order of their first faces.
            var shellOf = new int[count];
            var parity = new bool[count];
            var shells = new List<List<int>>();
            var shellOfRoot = new Dictionary<int, int>();

            for (int f = 0; f < count; f++)
            {
                shellOf[f] = -1;

                if (work.Dropped[f])
                {
                    continue;
                }

                int root = FindOdd(parent, odd, f, out parity[f]);

                if (!shellOfRoot.TryGetValue(root, out int shell))
                {
                    shell = shells.Count;
                    shellOfRoot.Add(root, shell);
                    shells.Add(new List<int>());
                }

                shells[shell].Add(f);
                shellOf[f] = shell;
            }

            // Within each shell, the side of less area is turned to the other.
            var turn = new bool[count];

            foreach (List<int> shell in shells)
            {
                double even = 0.0, other = 0.0;
                bool any = false;

                foreach (int f in shell)
                {
                    if (parity[f])
                    {
                        other += faces[f].Area;
                        any = true;
                    }
                    else
                    {
                        even += faces[f].Area;
                    }
                }

                if (!any)
                {
                    continue;
                }

                // The shell's first face is its root, on the even side, and stays where the two sides are as large.
                bool turnOdd = other <= even;

                foreach (int f in shell)
                {
                    turn[f] = parity[f] == turnOdd;
                }
            }

            bool[] whole = OutwardsOrIn(work, shells, turn, open);

            for (int f = 0; f < count; f++)
            {
                if (shellOf[f] >= 0)
                {
                    work.Turned[f] = turn[f] != whole[shellOf[f]];
                }
            }

            work.ShellOf = shellOf;
            return true;
        }

        /// <summary>
        /// Orients the faces of a body welded shut again, now that every shell of it closes, and turns them over in place
        /// where they are to be; the faces given whose faces were turned are given back, to be reported so.
        /// </summary>
        /// <param name="work">The work, its faces turned.</param>
        /// <param name="built">The faces of the body welded; turned over in place where they are to be.</param>
        /// <param name="from">For each, the face given it comes of, by index.</param>
        /// <param name="stretches">The stretches read again where faces changed, by the places of their faces among those read.</param>
        /// <param name="localOf">For each face read again, its place among the faces welded.</param>
        /// <returns>The faces given whose faces were turned over, by index, in order.</returns>
        /// <remarks>
        /// <para>
        /// The faces are joined as the turning joined the faces they come of, which it wound alike, and along the stretches
        /// read again where faces changed, as it joins them: the long ones first, and then the short ones that are whole edges
        /// of both their faces, only between faces not joined yet. A shell the welding joined of shells the turning could not,
        /// patches of a sphere each face of which stood on copies of its own corners, is so wound alike the way the faces of
        /// it given were, its side of less area turned to the other. Then each shell, closed, is read as the turning reads a
        /// closed shell: one inside no other enclosing its volume inwards is turned over whole, with every shell inside it,
        /// and a shell inside another keeps its winding against it (see <see cref="TurnedOutwards"/>). A box whose faces stood
        /// on copies of their corners, every face wound inwards, open all round as given, is turned out once welded shut.
        /// </para>
        /// <para>
        /// Only the stretches where faces changed are read: elsewhere the faces are as the turning joined them. A body of
        /// six thousand faces, one copy of a corner of it welded, is so oriented in no more than it takes to add its volume.
        /// </para>
        /// </remarks>
        private static List<int> Reorient(Work work, List<GeoFace3> built, List<int> from, List<Stretch> stretches, List<int> localOf)
        {
            int count = built.Count;
            var parent = new int[count];
            var odd = new bool[count];

            for (int k = 0; k < count; k++)
            {
                parent[k] = k;
            }

            // The faces of one shell as the turning left it are wound alike already.
            var first = new Dictionary<int, int>();

            for (int k = 0; k < count; k++)
            {
                int shell = work.ShellOf[from[k]];

                if (shell < 0)
                {
                    continue;
                }

                if (first.TryGetValue(shell, out int one))
                {
                    TryJoin(parent, odd, one, k, false);
                }
                else
                {
                    first.Add(shell, k);
                }
            }

            double shortest = work.Options.MaxGap;
            var both = new List<int> { 0, 1 };

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Stretch stretch in stretches)
                {
                    if (stretch.Faces.Length != 2 || stretch.Start.DistanceTo(stretch.End) > shortest != (pass == 0))
                    {
                        continue;
                    }

                    int a = localOf[stretch.Faces[0]];
                    int b = localOf[stretch.Faces[1]];

                    if (a == b)
                    {
                        continue;
                    }

                    // A face asked to be turned both ways is left as it is: the check reads the body after.
                    bool apart = stretch.Forward[0] == stretch.Forward[1];

                    if (pass == 0 || (FindOdd(parent, odd, a, out _) != FindOdd(parent, odd, b, out _) && IsWholeEdge(stretch, both, work.Tolerance.EqualPoint)))
                    {
                        TryJoin(parent, odd, a, b, apart);
                    }
                }
            }

            // The shells, each wound alike, the side of less area turned to the other.
            var shellOf = new int[count];
            var parity = new bool[count];
            var shells = new List<List<int>>();
            var shellOfRoot = new Dictionary<int, int>();

            for (int k = 0; k < count; k++)
            {
                int root = FindOdd(parent, odd, k, out parity[k]);

                if (!shellOfRoot.TryGetValue(root, out int shell))
                {
                    shell = shells.Count;
                    shellOfRoot.Add(root, shell);
                    shells.Add(new List<int>());
                }

                shells[shell].Add(k);
                shellOf[k] = shell;
            }

            var turn = new bool[count];

            foreach (List<int> shell in shells)
            {
                double even = 0.0, other = 0.0;

                foreach (int k in shell)
                {
                    if (parity[k])
                    {
                        other += built[k].Area;
                    }
                    else
                    {
                        even += built[k].Area;
                    }
                }

                if (other > 0.0)
                {
                    bool turnOdd = other <= even;

                    foreach (int k in shell)
                    {
                        turn[k] = parity[k] == turnOdd;
                    }
                }
            }

            var closedShells = new bool[shells.Count];

            for (int s = 0; s < closedShells.Length; s++)
            {
                closedShells[s] = true;
            }

            bool[] whole = TurnedOutwards(built, shells, turn, closedShells, work.Tolerance);
            var again = new List<int>();
            var seen = new HashSet<int>();

            for (int k = 0; k < count; k++)
            {
                if (turn[k] != whole[shellOf[k]])
                {
                    built[k] = built[k].Flip();

                    if (seen.Add(from[k]))
                    {
                        again.Add(from[k]);
                    }
                }
            }

            again.Sort();
            return again;
        }

        /// <summary>
        /// For each shell, wound alike once its faces are turned where they are to be, whether it is to be turned over whole:
        /// an outermost closed shell, inside no other, that encloses its volume inwards, and with it every shell inside it.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="shells">The faces of each shell, by index.</param>
        /// <param name="turn">For each face, whether it is turned over to wind its shell alike; null where none is.</param>
        /// <param name="closed">For each shell, whether it is closed.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// <para>
        /// A shell inside another keeps the way it is wound against it: the same way, it is a block within it, the other way
        /// a cavity, and neither is turned, since the check reads only the sign of the whole body's volume and both read
        /// valid. A part of a Tekla model came with three boxes inside its main shell, all wound outwards; with one face of
        /// the main shell wound the wrong way, the boxes read as nested were turned into cavities, and the part lost 3.9 %
        /// of its volume, where the one face is all that is turned now. A box with a box inside, both wound outwards, 6 600,
        /// one face of the outer turned, gets that face turned back and holds 6 600.
        /// </para>
        /// <para>
        /// An outermost shell wound inwards is turned with every shell inside it, so that each keeps its winding against the
        /// one round it: a box inside out with a cavity comes back a box with a cavity. A shell within the box of a shell
        /// still open may lie inside it, however little of the sphere round it that shell's faces cover, and is no outermost
        /// one until that shell closes: a cavity in a box missing its top and bottom stays a cavity, though the four sides
        /// cover about a third of the sphere round it. A shell enclosing no more than the tolerance times its area says
        /// nothing of which way it faces.
        /// </para>
        /// </remarks>
        private static bool[] TurnedOutwards(IReadOnlyList<GeoFace3> faces, List<List<int>> shells, bool[] turn, bool[] closed, Tolerance tolerance)
        {
            int count = shells.Count;
            var oriented = new List<GeoFace3>[count];
            var boxes = new GeoAabb3[count];
            var volumes = new double[count];
            var areas = new double[count];

            for (int s = 0; s < count; s++)
            {
                var these = new List<GeoFace3>(shells[s].Count);
                GeoAabb3 box = GeoAabb3.Empty;

                foreach (int k in shells[s])
                {
                    these.Add(turn != null && turn[k] ? faces[k].Flip() : faces[k]);
                    box = box.Union(faces[k].GetAabb());
                    areas[s] += faces[k].Area;
                }

                oriented[s] = these;
                boxes[s] = box;
                volumes[s] = SignedVolumeOf(these);
            }

            var whole = new bool[count];

            for (int s = 0; s < count; s++)
            {
                // Only a closed shell wound inwards, saying so by more than the tolerance, can be one to turn.
                if (!closed[s] || !(Math.Abs(volumes[s]) > tolerance.EqualPoint * areas[s]) || volumes[s] > 0.0)
                {
                    continue;
                }

                bool nested = false;

                for (int t = 0; t < count && !nested; t++)
                {
                    // A shell still open may yet hold one in its box, however little of the sphere round it its faces cover.
                    nested = t != s && boxes[t].Contains(boxes[s], tolerance) && (!closed[t] || Holds(oriented[t], oriented[s], tolerance));
                }

                if (nested)
                {
                    continue;
                }

                whole[s] = true;

                for (int t = 0; t < count; t++)
                {
                    if (t != s && boxes[s].Contains(boxes[t], tolerance) && Holds(oriented[s], oriented[t], tolerance))
                    {
                        whole[t] = true;
                    }
                }
            }

            return whole;
        }

        /// <summary>
        /// Turns faces given the other way from how the steps before left them, each as <see cref="TurnAgain(Work, int)"/>
        /// does, in order.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="faces">The faces, by index.</param>
        private static void TurnAgain(Work work, List<int> faces)
        {
            foreach (int face in faces)
            {
                TurnAgain(work, face);
            }
        }

        /// <summary>
        /// Determines whether a stretch is the whole of an edge of each face running it, within the point tolerance at either
        /// end: not a hair of two longer edges' copies overlapping, which says nothing of which way they face.
        /// </summary>
        /// <param name="stretch">The stretch.</param>
        /// <param name="live">The edges covering it to look at, by their place on it.</param>
        /// <param name="reach">The point tolerance.</param>
        private static bool IsWholeEdge(Stretch stretch, List<int> live, double reach)
        {
            double length = stretch.Start.DistanceTo(stretch.End);

            foreach (int r in live)
            {
                if (stretch.Edges[r].Start.DistanceTo(stretch.Edges[r].End) > length + (2.0 * reach))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reports each face kept that is turned over, one change of its area, in the order of the faces; and notes where
        /// the turnings end among the changes, so that a face turned again later is reported among them.
        /// </summary>
        /// <param name="work">The work, its faces turned and the stray ones dropped.</param>
        private static void ReportTurned(Work work)
        {
            for (int f = 0; f < work.Faces.Count; f++)
            {
                if (work.Turned[f] && !work.Dropped[f])
                {
                    var flip = new SolidRepair3(SolidRepairKind.Flip, work.Faces[f].Centroid, work.Faces[f].Area);
                    work.Repairs.Add(flip);
                    work.FlipOf[f] = flip;
                }
            }

            work.TurningsEnd = work.Repairs.Count;
        }

        /// <summary>
        /// Turns a face given the other way from how the steps before left it: a face they turned is back as it was given,
        /// and its turning no change; one they did not is turned, one change of its area, reported with the other turnings.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="face">The face, by index.</param>
        private static void TurnAgain(Work work, int face)
        {
            if (work.Turned[face])
            {
                int at = work.Repairs.IndexOf(work.FlipOf[face]);
                work.Repairs.RemoveAt(at);
                work.FlipOf[face] = null;

                if (at < work.TurningsEnd)
                {
                    work.TurningsEnd--;
                }
            }
            else
            {
                var flip = new SolidRepair3(SolidRepairKind.Flip, work.Faces[face].Centroid, work.Faces[face].Area);
                work.Repairs.Insert(work.TurningsEnd, flip);
                work.TurningsEnd++;
                work.FlipOf[face] = flip;
            }

            work.Turned[face] = !work.Turned[face];
        }

        /// <summary>
        /// Drops each stray face: one no other face runs an edge of, lying back to back on a face of the body, as the two are
        /// to be turned, every corner of it and its middle on that face. It is a sheet of no thickness and holds nothing.
        /// </summary>
        /// <param name="work">The work, its faces turned.</param>
        /// <remarks>
        /// <para>
        /// Its rim is a loop of its own, every edge open; filled, the loop would take the face again turned over, the two
        /// lying back to back on the face beneath, which reads valid and holds nothing. Only a face every edge of which is
        /// open is dropped so: a thin plate, two faces back to back with walls round them, runs its edges with the walls,
        /// and stays, however thin.
        /// </para>
        /// <para>
        /// The faces are filed by their boxes, and each face every edge of which is open is set only against the faces whose
        /// boxes meet its own; see <see cref="FaceBoxes"/>.
        /// </para>
        /// </remarks>
        private static void DropStrayFaces(Work work)
        {
            IReadOnlyList<GeoFace3> faces = work.Faces;
            List<int>[] along = StretchesOfFaces(work);
            var loose = new List<int>();

            for (int f = 0; f < faces.Count; f++)
            {
                if (!work.Dropped[f] && along[f] != null && IsLoose(work, f, along[f]))
                {
                    loose.Add(f);
                }
            }

            if (loose.Count == 0)
            {
                return;
            }

            Tolerance tolerance = work.Tolerance;
            var boxes = new FaceBoxes(faces, work.Dropped, tolerance);
            var near = new List<int>();

            foreach (int f in loose)
            {
                GeoFace3 face = faces[f];
                GeoVector3 normal = work.Turned[f] ? face.Normal.Negate() : face.Normal;
                boxes.Meeting(face.GetAabb(), near);

                foreach (int g in near)
                {
                    if (g == f || work.Dropped[g])
                    {
                        continue;
                    }

                    GeoVector3 other = work.Turned[g] ? faces[g].Normal.Negate() : faces[g].Normal;

                    if (normal.DotProduct(other) < Facing && LiesOn(face, faces[g], tolerance))
                    {
                        Drop(work, f);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether no other face kept runs any stretch a face has an edge on: every edge of it open.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="face">The face, by index.</param>
        /// <param name="along">The stretches it has an edge on, by index.</param>
        private static bool IsLoose(Work work, int face, List<int> along)
        {
            foreach (int s in along)
            {
                foreach (int other in work.Stretches[s].Faces)
                {
                    if (other != face && !work.Dropped[other])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether every corner of a face's boundary, and its middle, lie on another face, within the tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="other">The other face.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static bool LiesOn(GeoFace3 face, GeoFace3 other, Tolerance tolerance)
        {
            GeoAabb3 box = other.GetAabb();

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (!box.Contains(corner, tolerance) || other.Locate(corner, tolerance) == PointLocation.OutSide)
                {
                    return false;
                }
            }

            GeoPoint3 middle = face.Centroid;
            return box.Contains(middle, tolerance) && other.Locate(middle, tolerance) != PointLocation.OutSide;
        }

        /// <summary>
        /// For each shell, wound alike, whether it is to be turned over whole: an outermost closed shell wound inwards, and
        /// every shell inside it with it; see <see cref="TurnedOutwards"/>.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="shells">The faces of each shell, by index.</param>
        /// <param name="turn">For each face, whether it is turned over to wind its shell alike.</param>
        /// <param name="open">For each face, whether it runs a stretch an odd number of faces run: its shell is open.</param>
        /// <remarks>
        /// An open shell keeps its winding as given: it encloses a volume only from where it is measured, and a patch of a few
        /// faces of a sphere, each on copies of its own corners, measured from the middle of its own box, which lies outside
        /// the sphere, seems wound inwards. Its sign is read once it is closed, by the welding or the filling.
        /// </remarks>
        private static bool[] OutwardsOrIn(Work work, List<List<int>> shells, bool[] turn, bool[] open)
        {
            var closed = new bool[shells.Count];

            for (int s = 0; s < shells.Count; s++)
            {
                closed[s] = !shells[s].Exists(f => open[f]);
            }

            return TurnedOutwards(work.Faces, shells, turn, closed, work.Tolerance);
        }

        /// <summary>
        /// Determines whether the faces of one shell lie inside another's: points on them, one on each of its faces in turn,
        /// until one lies clearly inside or clearly outside, as the share of the sphere round it the other's faces cover
        /// says; failing that, more than half of it on average.
        /// </summary>
        /// <param name="container">The faces of the other shell, wound alike.</param>
        /// <param name="inner">The faces of the one.</param>
        /// <param name="tolerance">The tolerance the faces are broken into triangles within.</param>
        /// <remarks>
        /// Two shells that do not cross lie wholly inside or wholly outside each other, so any point of the one not on the
        /// other settles it, and a point on both does not: the first few faces are tried.
        /// </remarks>
        private static bool Holds(List<GeoFace3> container, List<GeoFace3> inner, Tolerance tolerance)
        {
            double sum = 0.0;
            int tried = 0;

            for (int k = 0; k < inner.Count && tried < 8; k++)
            {
                if (!TryGetPointOn(inner[k], tolerance, out GeoPoint3 point))
                {
                    continue;
                }

                double share = Math.Abs(Winding(container, point));

                if (share >= 0.75)
                {
                    return true;
                }

                if (share <= 0.25)
                {
                    return false;
                }

                sum += share;
                tried++;
            }

            return tried > 0 && sum > 0.5 * tried;
        }

        /// <summary>
        /// A point well within a face: the centroid of the largest of the triangles it breaks into, the first of them where
        /// two are as large.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance it is broken into triangles within.</param>
        /// <param name="point">The point; the origin when the method returns false.</param>
        /// <returns>false where the face breaks into no triangle.</returns>
        private static bool TryGetPointOn(GeoFace3 face, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;
            double largest = 0.0;

            foreach (GeoTriangle3 triangle in face.TriangulateSurface(tolerance))
            {
                double area = triangle.Area;

                if (area > largest)
                {
                    largest = area;
                    point = triangle.Centroid;
                }
            }

            return largest > 0.0;
        }

        /// <summary>
        /// How many times faces wind round a point: the solid angle they cover seen from it over that of the whole sphere,
        /// signed by which way they face it. One inside a closed shell wound outwards, minus one inside one wound inwards,
        /// nought outside, and between for faces that do not close.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="point">The point.</param>
        private static double Winding(List<GeoFace3> faces, GeoPoint3 point)
        {
            double total = 0.0;

            foreach (GeoFace3 face in faces)
            {
                total += SolidAngle(face.Boundary, point);

                // A hole is wound as the boundary is, so what it covers is taken away.
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    total -= SolidAngle(hole, point);
                }
            }

            return total / (4.0 * Math.PI);
        }

        /// <summary>
        /// The solid angle a ring covers seen from a point, signed by which way it faces the point: the sum over the fan of
        /// the ring from its first corner, each triangle by Van Oosterom and Strackee's formula.
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <param name="point">The point.</param>
        private static double SolidAngle(GeoPolygon3 ring, GeoPoint3 point)
        {
            IReadOnlyList<GeoPoint3> corners = ring.Vertices;
            GeoVector3 a = point.GetVectorTo(corners[0]);
            double la = a.Length;
            double total = 0.0;

            for (int i = 1; i + 1 < corners.Count; i++)
            {
                GeoVector3 b = point.GetVectorTo(corners[i]);
                GeoVector3 c = point.GetVectorTo(corners[i + 1]);
                double lb = b.Length;
                double lc = c.Length;
                double above = a.TripleProduct(b, c);
                double across = la * lb * lc + a.DotProduct(b) * lc + a.DotProduct(c) * lb + b.DotProduct(c) * la;

                total += 2.0 * Math.Atan2(above, across);
            }

            return total;
        }

        /// <summary>
        /// The root of a face's shell, the shells kept by their lowest face, and whether the face is on the other side of
        /// its shell from the root: to be turned over where the root is not, or the other way round.
        /// </summary>
        /// <param name="parent">The parent of each face.</param>
        /// <param name="odd">For each face, whether it is on the other side from its parent.</param>
        /// <param name="i">The face.</param>
        /// <param name="parity">Whether it is on the other side from the root.</param>
        private static int FindOdd(int[] parent, bool[] odd, int i, out bool parity)
        {
            int root = i;
            bool total = false;

            while (parent[root] != root)
            {
                total ^= odd[root];
                root = parent[root];
            }

            // Every face on the way is hung from the root at once, with its own side.
            bool toRoot = total;
            int node = i;

            while (parent[node] != node)
            {
                int next = parent[node];
                bool own = odd[node];
                parent[node] = root;
                odd[node] = toRoot;
                toRoot ^= own;
                node = next;
            }

            parity = total;
            return root;
        }

        /// <summary>
        /// Joins the shells of two faces, on the same side or on the other side of each other; false where they are joined
        /// already the other way.
        /// </summary>
        /// <param name="parent">The parent of each face.</param>
        /// <param name="odd">For each face, whether it is on the other side from its parent.</param>
        /// <param name="a">The one face.</param>
        /// <param name="b">The other.</param>
        /// <param name="apart">Whether the two are on the other side of each other.</param>
        private static bool TryJoin(int[] parent, bool[] odd, int a, int b, bool apart)
        {
            int ra = FindOdd(parent, odd, a, out bool pa);
            int rb = FindOdd(parent, odd, b, out bool pb);

            if (ra == rb)
            {
                return (pa != pb) == apart;
            }

            // Hung from the lower root, so that the root of a shell is its first face.
            int low = Math.Min(ra, rb);
            int high = Math.Max(ra, rb);
            parent[high] = low;
            odd[high] = pa ^ pb ^ apart;
            return true;
        }

        #endregion

        #region Faces filed by their boxes

        /// <summary>
        /// Faces filed by where their boxes start along one axis, so that those whose boxes meet a box are found by halving
        /// rather than by setting each against every other.
        /// </summary>
        /// <remarks>
        /// A box meeting another starts no further before it than the longest box is long, so only the boxes starting within
        /// that of it are looked at. The axis is the one along which the longest box is shortest beside the spread of them
        /// all: on a sphere of small faces, a thin slice whichever the axis. Where a box spans the rest along every axis, as
        /// the top of a plate does, every box is looked at for each box asked about.
        /// </remarks>
        private sealed class FaceBoxes
        {
            private readonly GeoAabb3[] _boxes;
            private readonly int[] _order;
            private readonly double[] _lows;
            private readonly int _axis;
            private readonly double _longest;
            private readonly Tolerance _tolerance;

            /// <summary>
            /// Files faces by their boxes.
            /// </summary>
            /// <param name="faces">The faces.</param>
            /// <param name="skip">For each face, whether it is left out; null where none is.</param>
            /// <param name="tolerance">The tolerance two boxes meet within.</param>
            internal FaceBoxes(IReadOnlyList<GeoFace3> faces, bool[] skip, Tolerance tolerance)
                : this(BoxesOf(faces, skip), skip, tolerance)
            {
            }

            /// <summary>
            /// Files boxes, each by its index.
            /// </summary>
            /// <param name="boxes">The boxes.</param>
            /// <param name="skip">For each box, whether it is left out; null where none is.</param>
            /// <param name="tolerance">The tolerance two boxes meet within.</param>
            internal FaceBoxes(GeoAabb3[] boxes, bool[] skip, Tolerance tolerance)
            {
                _tolerance = tolerance;
                _boxes = boxes;
                var kept = new List<int>(boxes.Length);
                GeoAabb3 all = GeoAabb3.Empty;

                for (int f = 0; f < boxes.Length; f++)
                {
                    if ((skip != null && skip[f]) || boxes[f].IsEmpty)
                    {
                        continue;
                    }

                    all = all.Union(boxes[f]);
                    kept.Add(f);
                }

                double least = double.PositiveInfinity;

                for (int axis = 0; axis < 3; axis++)
                {
                    double longest = 0.0;

                    foreach (int f in kept)
                    {
                        longest = Math.Max(longest, Coordinate(_boxes[f].Max, axis) - Coordinate(_boxes[f].Min, axis));
                    }

                    double spread = Coordinate(all.Max, axis) - Coordinate(all.Min, axis);
                    double share = spread > 0.0 ? longest / spread : 1.0;

                    if (share < least)
                    {
                        least = share;
                        _axis = axis;
                        _longest = longest;
                    }
                }

                var lows = new double[boxes.Length];

                foreach (int f in kept)
                {
                    lows[f] = Coordinate(_boxes[f].Min, _axis);
                }

                _order = kept.ToArray();

                Array.Sort(_order, (a, b) =>
                {
                    int byLow = lows[a].CompareTo(lows[b]);
                    return byLow != 0 ? byLow : a.CompareTo(b);
                });

                _lows = new double[_order.Length];

                for (int k = 0; k < _order.Length; k++)
                {
                    _lows[k] = lows[_order[k]];
                }
            }

            /// <summary>
            /// The boxes of faces, each but those left out; the empty box for those.
            /// </summary>
            /// <param name="faces">The faces.</param>
            /// <param name="skip">For each face, whether it is left out; null where none is.</param>
            private static GeoAabb3[] BoxesOf(IReadOnlyList<GeoFace3> faces, bool[] skip)
            {
                var boxes = new GeoAabb3[faces.Count];

                for (int f = 0; f < faces.Count; f++)
                {
                    if (skip == null || !skip[f])
                    {
                        boxes[f] = faces[f].GetAabb();
                    }
                }

                return boxes;
            }

            /// <summary>
            /// Finds the faces filed whose boxes meet a box within the point tolerance, in the order of the faces.
            /// </summary>
            /// <param name="box">The box.</param>
            /// <param name="into">Filled with the faces, by index; cleared first.</param>
            internal void Meeting(GeoAabb3 box, List<int> into)
            {
                into.Clear();

                if (box.IsEmpty)
                {
                    return;
                }

                double reach = _tolerance.EqualPoint;
                double low = Coordinate(box.Min, _axis) - reach - _longest;
                double high = Coordinate(box.Max, _axis) + reach;

                // The first box filed that starts no earlier than the longest box before the one asked about.
                int first = 0, past = _lows.Length;

                while (first < past)
                {
                    int middle = first + (past - first) / 2;

                    if (_lows[middle] < low)
                    {
                        first = middle + 1;
                    }
                    else
                    {
                        past = middle;
                    }
                }

                for (int k = first; k < _lows.Length && _lows[k] <= high; k++)
                {
                    int f = _order[k];

                    if (_boxes[f].CollidesWith(box, _tolerance))
                    {
                        into.Add(f);
                    }
                }

                into.Sort();
            }
        }

        #endregion
    }
}
