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
        /// both ways, the shell cannot be wound alike: a surface with one side only. Only stretches longer than the widest gap
        /// are read so: on a prism of a thousand sides each face on copies of its corners a thousandth or two apart, the
        /// bottom edges of two sides beside a corner run on so nearly straight that their copies come within the tolerance of
        /// one line for a thousandth or two, both running it the same way, and read so, they would turn a side over.
        /// </para>
        /// <para>
        /// Then the volume each shell encloses says which way it faces, each shell on its own: the faces of two shells, one
        /// wound each way, enclose nothing together, and the whole body would say nothing of either. A shell inside another
        /// is a cavity, and faces inwards; one inside two is material in a cavity, and faces outwards. Which shell is inside
        /// which is read from where a point on each lies, by how much of the sphere round it the other's faces cover, which
        /// for a shell that does not close quite still tells inside from out. A shell enclosing no more than the tolerance
        /// times its area, such as a face on its own, says nothing of which way it faces, and is left as it is.
        /// </para>
        /// <para>
        /// Every face turned over is one change, of its area.
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

            foreach (Stretch stretch in work.Stretches)
            {
                // A stretch no longer than the widest gap says nothing of which way its faces face: copies of the edges of
                // two faces beside a corner, run on nearly straight, come within the tolerance of one line for a hair, both
                // running it the same way, and are a piece of a gap for the welding.
                if (!(stretch.Start.DistanceTo(stretch.End) > shortest))
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

                int a = live.Count == 2 ? stretch.Faces[live[0]] : -1;
                int b = live.Count == 2 ? stretch.Faces[live[1]] : -1;

                if (a < 0 || a == b)
                {
                    continue;
                }

                // Two faces wound alike run the stretch one each way; run the same way, one of them is to be turned.
                bool apart = stretch.Forward[live[0]] == stretch.Forward[live[1]];

                if (!TryJoin(parent, odd, a, b, apart))
                {
                    work.Refuse(ClosingFailure.NonManifold, stretch.Middle);
                    return false;
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

            bool[] whole = OutwardsOrIn(work, shells, turn);

            for (int f = 0; f < count; f++)
            {
                if (shellOf[f] >= 0)
                {
                    work.Turned[f] = turn[f] != whole[shellOf[f]];
                }
            }

            for (int f = 0; f < count; f++)
            {
                if (work.Turned[f])
                {
                    work.Repairs.Add(new SolidRepair3(SolidRepairKind.Flip, faces[f].Centroid, faces[f].Area));
                }
            }

            return true;
        }

        /// <summary>
        /// For each shell, wound alike, whether it is to be turned over whole: where it encloses a volume the wrong way for
        /// where it lies, outwards inside an even number of others, inwards inside an odd number.
        /// </summary>
        /// <param name="work">The work.</param>
        /// <param name="shells">The faces of each shell, by index.</param>
        /// <param name="turn">For each face, whether it is turned over to wind its shell alike.</param>
        private static bool[] OutwardsOrIn(Work work, List<List<int>> shells, bool[] turn)
        {
            int count = shells.Count;
            var oriented = new List<GeoFace3>[count];
            var boxes = new GeoAabb3[count];
            var volumes = new double[count];
            var areas = new double[count];

            for (int s = 0; s < count; s++)
            {
                var faces = new List<GeoFace3>(shells[s].Count);
                GeoAabb3 box = GeoAabb3.Empty;

                foreach (int f in shells[s])
                {
                    GeoFace3 face = work.Faces[f];
                    faces.Add(turn[f] ? face.Flip() : face);
                    box = box.Union(face.GetAabb());
                    areas[s] += face.Area;
                }

                oriented[s] = faces;
                boxes[s] = box;
                volumes[s] = SignedVolumeOf(faces);
            }

            var whole = new bool[count];

            for (int s = 0; s < count; s++)
            {
                // As the check reads a body enclosing nothing: no thicker than the tolerance on average.
                if (!(Math.Abs(volumes[s]) > work.Tolerance.EqualPoint * areas[s]))
                {
                    continue;
                }

                int depth = 0;

                for (int t = 0; t < count; t++)
                {
                    if (t != s && boxes[t].Contains(boxes[s], work.Tolerance) && Holds(oriented[t], oriented[s], work.Tolerance))
                    {
                        depth++;
                    }
                }

                bool outwards = depth % 2 == 0;
                whole[s] = (volumes[s] > 0.0) != outwards;
            }

            return whole;
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
    }
}
