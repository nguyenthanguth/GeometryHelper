using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Separating a body into the pieces of material that do not touch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoSolid3"/> is a bag of faces, and nothing stops the faces from bounding two blocks that
    /// never meet. The booleans produce such bodies all the time — a bar through two blocks shares two
    /// separate regions with them — and so does cutting a body by a plane that passes exactly along the wall
    /// of a hole: the strip either side of the hole comes out as one body in two pieces. Anything that then
    /// decides what the body is by asking one point of it decides for every piece at once, and is wrong for
    /// all but one. That is how subtracting a box that overlapped an existing hole once threw away a whole
    /// block of material that was nowhere near the box.
    /// </para>
    /// <para>
    /// <b>Faces belong together when they share part of an edge.</b> An edge is matched by overlap along a
    /// common line rather than by its end points, so a long edge beside two short ones — which merging
    /// coplanar faces leaves behind — still joins all three. Where exactly two faces share a stretch of edge
    /// they are one piece. Where more meet — two blocks touching along an edge, four faces round one line —
    /// the faces are taken in turn round the line, and two neighbours are joined only when the wedge between
    /// them is material, which is what keeps two blocks that share nothing but an edge apart.
    /// </para>
    /// <para>
    /// A cavity is a closed shell of its own, wound inwards, and belongs to the piece around it rather than
    /// being a piece in itself. Each opening goes with every piece its box reaches.
    /// </para>
    /// </remarks>
    internal static class Shells3
    {
        /// <summary>
        /// One edge of one face, as it runs along its loop.
        /// </summary>
        private struct Segment
        {
            public GeoPoint3 Start;
            public GeoPoint3 End;
            public int Face;

            /// <summary>The direction from the edge into the face it belongs to, in the plane of the face.</summary>
            public GeoVector3 Inward;

            /// <summary>The outward normal of the face.</summary>
            public GeoVector3 Normal;

            public double MinX;
            public double MaxX;
        }

        /// <summary>
        /// Splits a body into its separate pieces.
        /// </summary>
        /// <returns>
        /// One body per piece of material, in no particular order; the body itself, alone, when it is all one
        /// piece.
        /// </returns>
        internal static List<GeoSolid3> Split(GeoSolid3 solid, Tolerance tolerance)
        {
            IReadOnlyList<GeoFace3> faces = solid.Faces;

            if (faces.Count < 8)
            {
                // Four faces is the least a closed body can have, so fewer than eight cannot hold two.
                return new List<GeoSolid3> { solid };
            }

            int[] parent = new int[faces.Count];

            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            List<Segment> segments = CollectSegments(faces, tolerance);
            JoinAcrossEdges(segments, parent, tolerance);

            // Gather the faces of each component.
            var components = new Dictionary<int, List<GeoFace3>>();

            for (int i = 0; i < faces.Count; i++)
            {
                int root = Find(parent, i);

                if (!components.TryGetValue(root, out List<GeoFace3> list))
                {
                    list = new List<GeoFace3>();
                    components.Add(root, list);
                }

                list.Add(faces[i]);
            }

            if (components.Count == 1)
            {
                return new List<GeoSolid3> { solid };
            }

            return Assemble(new List<List<GeoFace3>>(components.Values), solid.Openings, tolerance);
        }

        #region Matching edges

        /// <summary>
        /// Every edge of every loop of every face, with the direction into its face and the face's normal.
        /// </summary>
        private static List<Segment> CollectSegments(IReadOnlyList<GeoFace3> faces, Tolerance tolerance)
        {
            var segments = new List<Segment>();

            for (int f = 0; f < faces.Count; f++)
            {
                GeoFace3 face = faces[f];
                GeoVector3 normal = face.Boundary.Normal;

                AddLoop(segments, face.Boundary, f, normal, false, tolerance);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    AddLoop(segments, hole, f, normal, true, tolerance);
                }
            }

            segments.Sort((a, b) => a.MinX.CompareTo(b.MinX));

            return segments;
        }

        private static void AddLoop(List<Segment> segments, GeoPolygon3 loop, int face, GeoVector3 normal, bool isHole, Tolerance tolerance)
        {
            int count = loop.VertexCount;

            for (int i = 0; i < count; i++)
            {
                GeoPoint3 start = loop[i];
                GeoPoint3 end = loop[(i + 1) % count];
                GeoVector3 along = start.GetVectorTo(end);
                double length = along.Length;

                if (length <= tolerance.EqualPoint)
                {
                    continue;
                }

                // A loop runs counter-clockwise about its face's normal, so the face lies to the left of an
                // outer edge. A hole is wound the same way as the outline, so the face lies to its right.
                GeoVector3 left = normal.CrossProduct(along.Divide(length));
                GeoVector3 inward = isHole ? left.Negate() : left;

                segments.Add(new Segment
                {
                    Start = start,
                    End = end,
                    Face = face,
                    Inward = inward,
                    Normal = normal,
                    MinX = Math.Min(start.X, end.X) - tolerance.EqualPoint,
                    MaxX = Math.Max(start.X, end.X) + tolerance.EqualPoint,
                });
            }
        }

        /// <summary>
        /// Joins the faces that share a stretch of edge into one piece.
        /// </summary>
        private static void JoinAcrossEdges(List<Segment> segments, int[] parent, Tolerance tolerance)
        {
            // First, which segments run along one line and overlap: those are the edges to be settled, and
            // a group of them is settled together because more than two faces may meet along one line.
            int[] line = new int[segments.Count];

            for (int i = 0; i < line.Length; i++)
            {
                line[i] = i;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                Segment a = segments[i];

                // Sorted by the low end in x, so a segment starting beyond this one's high end in x cannot meet
                // it and neither can any after it.
                for (int j = i + 1; j < segments.Count && segments[j].MinX <= a.MaxX; j++)
                {
                    Segment b = segments[j];

                    if (a.Face != b.Face && Overlap(a, b, tolerance))
                    {
                        Union(line, i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<int>>();

            for (int i = 0; i < segments.Count; i++)
            {
                int root = Find(line, i);

                if (!groups.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    groups.Add(root, members);
                }

                members.Add(i);
            }

            foreach (List<int> members in groups.Values)
            {
                if (members.Count > 1)
                {
                    SettleLine(segments, members, parent, tolerance);
                }
            }
        }

        /// <summary>
        /// Determines whether two segments lie along one line and share a stretch of it longer than the
        /// tolerance.
        /// </summary>
        private static bool Overlap(Segment a, Segment b, Tolerance tolerance)
        {
            GeoVector3 along = a.Start.GetVectorTo(a.End);
            double length = along.Length;
            GeoVector3 unit = along.Divide(length);

            if (OffLine(a.Start, unit, b.Start) > tolerance.EqualPoint || OffLine(a.Start, unit, b.End) > tolerance.EqualPoint)
            {
                return false;
            }

            double t0 = a.Start.GetVectorTo(b.Start).DotProduct(unit);
            double t1 = a.Start.GetVectorTo(b.End).DotProduct(unit);

            double low = Math.Max(0.0, Math.Min(t0, t1));
            double high = Math.Min(length, Math.Max(t0, t1));

            return high - low > tolerance.EqualPoint;
        }

        private static double OffLine(GeoPoint3 origin, GeoVector3 unit, GeoPoint3 point)
        {
            GeoVector3 offset = origin.GetVectorTo(point);

            return offset.Subtract(unit.Multiply(offset.DotProduct(unit))).Length;
        }

        /// <summary>
        /// Settles one line: along every stretch of it, which of the faces meeting there are one piece.
        /// </summary>
        private static void SettleLine(List<Segment> segments, List<int> members, int[] parent, Tolerance tolerance)
        {
            Segment first = segments[members[0]];
            GeoPoint3 origin = first.Start;
            GeoVector3 axis = first.Start.GetVectorTo(first.End).Normalize();

            // Every end point along the line, as a distance from the origin; between two neighbouring ones the
            // same faces meet all the way along, so each stretch is settled once.
            var stops = new List<double>();

            foreach (int m in members)
            {
                stops.Add(origin.GetVectorTo(segments[m].Start).DotProduct(axis));
                stops.Add(origin.GetVectorTo(segments[m].End).DotProduct(axis));
            }

            stops.Sort();

            GetBasis(axis, out GeoVector3 b1, out GeoVector3 b2);

            for (int k = 0; k + 1 < stops.Count; k++)
            {
                if (stops[k + 1] - stops[k] <= tolerance.EqualPoint)
                {
                    continue;
                }

                double middle = (stops[k] + stops[k + 1]) * 0.5;
                var meeting = new List<Segment>();

                foreach (int m in members)
                {
                    Segment s = segments[m];
                    double t0 = origin.GetVectorTo(s.Start).DotProduct(axis);
                    double t1 = origin.GetVectorTo(s.End).DotProduct(axis);

                    if (middle > Math.Min(t0, t1) && middle < Math.Max(t0, t1))
                    {
                        meeting.Add(s);
                    }
                }

                if (meeting.Count == 2)
                {
                    Union(parent, meeting[0].Face, meeting[1].Face);
                }
                else if (meeting.Count > 2)
                {
                    PairRoundTheLine(meeting, axis, b1, b2, parent);
                }
            }
        }

        /// <summary>
        /// Where more than two faces meet along one line, joins each face to its neighbour round the line when
        /// the wedge between the two is material.
        /// </summary>
        /// <remarks>
        /// Two blocks touching along an edge put four faces round one line, and the two wedges of material
        /// between them are separated by two wedges of empty space. Taken in turn round the line, a face and
        /// its neighbour bound the same piece exactly when the wedge between them is material — which is the
        /// side the first face's outward normal points away from.
        /// </remarks>
        private static void PairRoundTheLine(List<Segment> meeting, GeoVector3 axis, GeoVector3 b1, GeoVector3 b2, int[] parent)
        {
            meeting.Sort((a, b) => Angle(a.Inward, b1, b2).CompareTo(Angle(b.Inward, b1, b2)));

            for (int i = 0; i < meeting.Count; i++)
            {
                Segment here = meeting[i];
                Segment next = meeting[(i + 1) % meeting.Count];

                // Turning from this face towards the next goes the positive way round the line.
                GeoVector3 turning = axis.CrossProduct(here.Inward);

                if (here.Normal.Negate().DotProduct(turning) > 0.0)
                {
                    Union(parent, here.Face, next.Face);
                }
            }
        }

        private static double Angle(GeoVector3 direction, GeoVector3 b1, GeoVector3 b2)
            => Math.Atan2(direction.DotProduct(b2), direction.DotProduct(b1));

        private static void GetBasis(GeoVector3 axis, out GeoVector3 b1, out GeoVector3 b2)
        {
            GeoVector3 helper = Math.Abs(axis.X) < 0.9 ? GeoVector3.XAxis : GeoVector3.YAxis;

            b1 = helper.Subtract(axis.Multiply(helper.DotProduct(axis))).Normalize();
            b2 = axis.CrossProduct(b1);
        }

        #endregion

        #region Assembling the pieces

        /// <summary>
        /// Turns the groups of faces into bodies, giving each cavity to the piece around it and each opening
        /// to every piece it reaches.
        /// </summary>
        private static List<GeoSolid3> Assemble(List<List<GeoFace3>> groups, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            var shells = new List<GeoSolid3>();

            foreach (List<GeoFace3> group in groups)
            {
                shells.Add(new GeoSolid3(group));
            }

            // A shell wound inwards encloses no material of its own: it is the wall of a cavity inside some
            // other shell, and goes with the smallest shell that holds it.
            var solids = new List<List<GeoFace3>>();
            var outer = new List<GeoSolid3>();

            for (int i = 0; i < shells.Count; i++)
            {
                if (shells[i].GetSignedVolume() >= 0.0)
                {
                    outer.Add(shells[i]);
                    solids.Add(new List<GeoFace3>(groups[i]));
                }
            }

            for (int i = 0; i < shells.Count; i++)
            {
                if (shells[i].GetSignedVolume() >= 0.0)
                {
                    continue;
                }

                GeoPoint3 witness = groups[i][0].Boundary[0];
                int home = -1;
                double smallest = double.MaxValue;

                for (int o = 0; o < outer.Count; o++)
                {
                    if (outer[o].Volume < smallest
                        && outer[o].GetAabb().Contains(witness, tolerance)
                        && Containment3.Locate(outer[o], witness, tolerance) != PointLocation.OutSide)
                    {
                        home = o;
                        smallest = outer[o].Volume;
                    }
                }

                if (home >= 0)
                {
                    solids[home].AddRange(groups[i]);
                }
                else
                {
                    // Nothing holds it, so the shell is not a cavity of anything here; keep it as it is
                    // rather than lose it.
                    solids.Add(new List<GeoFace3>(groups[i]));
                }
            }

            var pieces = new List<GeoSolid3>(solids.Count);

            foreach (List<GeoFace3> faces in solids)
            {
                var piece = new GeoSolid3(faces);
                var reaching = new List<GeoSolid3>();
                GeoAabb3 box = piece.GetAabb();

                foreach (GeoSolid3 opening in openings)
                {
                    if (opening.GetAabb().CollidesWith(box, tolerance))
                    {
                        reaching.Add(opening);
                    }
                }

                pieces.Add(reaching.Count == 0 ? piece : new GeoSolid3(faces, reaching));
            }

            return pieces;
        }

        #endregion

        #region Union-find

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
                parent[ra] = rb;
            }
        }

        #endregion
    }
}
