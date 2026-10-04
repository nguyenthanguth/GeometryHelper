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
    /// <para>
    /// A group of faces enclosing nothing — a lone face, or two lying back to back as a sheet of no thickness
    /// — is no piece either. It goes with the piece holding it, so that nothing the body was given is lost,
    /// and is dropped only when no piece holds it. Such a group once reached the constructor of a body and
    /// made splitting throw.
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

            /// <summary>Whether the edge is one of a hole, wound as the outline is, with its face on the right.</summary>
            public bool Hole;

            /// <summary>The direction from the edge into the face it belongs to, in the plane of the face.</summary>
            public GeoVector3 Inward;

            /// <summary>The outward normal of the face.</summary>
            public GeoVector3 Normal;

            /// <summary>Where the edge starts along the axis the edges are swept along, less the tolerance.</summary>
            public double Low;

            /// <summary>Where it ends along that axis, and the tolerance more.</summary>
            public double High;
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

            // Swept along the axis that leaves the fewest pairs to try. Along an axis the edges hardly spread along, as
            // the axis a slab is thin along, every edge overlaps every other there, and so do the long edges of a long
            // body along its length: a drum of 4 096 sides took eleven seconds either way, and a few hundredths across.
            var lows = new double[segments.Count];
            var highs = new double[segments.Count];
            int axis = 0;
            long fewest = long.MaxValue;

            for (int candidate = 0; candidate < 3; candidate++)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    Extent(segments[i], candidate, tolerance, out lows[i], out highs[i]);
                }

                Array.Sort(lows);
                long tried = Tried(lows, highs);

                if (tried < fewest)
                {
                    fewest = tried;
                    axis = candidate;
                }
            }

            for (int i = 0; i < segments.Count; i++)
            {
                Segment s = segments[i];
                Extent(s, axis, tolerance, out s.Low, out s.High);
                segments[i] = s;
            }

            segments.Sort((a, b) => a.Low.CompareTo(b.Low));

            return segments;
        }

        /// <summary>Where a segment starts and ends along an axis, widened by the tolerance either way.</summary>
        private static void Extent(Segment segment, int axis, Tolerance tolerance, out double low, out double high)
        {
            double a = axis == 0 ? segment.Start.X : axis == 1 ? segment.Start.Y : segment.Start.Z;
            double b = axis == 0 ? segment.End.X : axis == 1 ? segment.End.Y : segment.End.Z;

            low = Math.Min(a, b) - tolerance.EqualPoint;
            high = Math.Max(a, b) + tolerance.EqualPoint;
        }

        /// <summary>
        /// How many pairs a sweep tries: each segment, taken by where it starts, against every later one that starts no
        /// further along than it ends.
        /// </summary>
        /// <param name="lows">Where the segments start, sorted.</param>
        /// <param name="highs">Where they end, in any order.</param>
        private static long Tried(double[] lows, double[] highs)
        {
            long reached = 0;

            foreach (double high in highs)
            {
                // How many segments start no further along than this one ends: itself, those before it in the sweep and
                // those it is tried against.
                int below = 0, above = lows.Length;

                while (below < above)
                {
                    int middle = (below + above) >> 1;

                    if (lows[middle] <= high)
                    {
                        below = middle + 1;
                    }
                    else
                    {
                        above = middle;
                    }
                }

                reached += below;
            }

            long count = lows.Length;
            return reached - count * (count + 1) / 2;
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
                    Hole = isHole,
                    Inward = inward,
                    Normal = normal,
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

                // Sorted by the low end along the sweep, so a segment starting beyond this one's high end cannot meet
                // it and neither can any after it.
                for (int j = i + 1; j < segments.Count && segments[j].Low <= a.High; j++)
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
        /// Determines whether faces close up: every stretch of every edge is shared by an even number of them.
        /// </summary>
        /// <remarks>
        /// Edges are matched by overlap along a line, as for splitting, so a long edge beside two short ones is
        /// matched stretch by stretch. An odd count is an open rim, one face, or a fin, three; two close the
        /// surface, and so do four where two blocks meet along an edge.
        /// </remarks>
        internal static bool ClosesUp(IReadOnlyList<GeoFace3> faces, Tolerance tolerance)
        {
            List<Segment> segments = CollectSegments(faces, tolerance);

            if (segments.Count == 0)
            {
                return false;
            }

            int[] line = new int[segments.Count];

            for (int i = 0; i < line.Length; i++)
            {
                line[i] = i;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                Segment a = segments[i];

                for (int j = i + 1; j < segments.Count && segments[j].Low <= a.High; j++)
                {
                    if (Overlap(a, segments[j], tolerance))
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
                if (members.Count == 1 || !EvenAlong(segments, members, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether every stretch of one line is covered by an even number of the edges along it.
        /// </summary>
        private static bool EvenAlong(List<Segment> segments, List<int> members, Tolerance tolerance)
        {
            // Along the longest of them, whose direction is the truest; see Overlap.
            Segment first = segments[members[0]];

            foreach (int m in members)
            {
                if (segments[m].Start.GetVectorTo(segments[m].End).LengthSquared > first.Start.GetVectorTo(first.End).LengthSquared)
                {
                    first = segments[m];
                }
            }

            GeoPoint3 origin = first.Start;

            // Every member shares a stretch longer than the point tolerance with another, so it has a length to
            // divide by. Normalize would judge that length against the default tolerance instead of this one, and
            // refuse a segment a boolean run tighter than the default is right to keep.
            GeoVector3 along = first.Start.GetVectorTo(first.End);
            GeoVector3 axis = along.Divide(along.Length);

            var stops = new List<double>();

            foreach (int m in members)
            {
                stops.Add(origin.GetVectorTo(segments[m].Start).DotProduct(axis));
                stops.Add(origin.GetVectorTo(segments[m].End).DotProduct(axis));
            }

            stops.Sort();

            for (int k = 0; k + 1 < stops.Count; k++)
            {
                if (stops[k + 1] - stops[k] <= tolerance.EqualPoint)
                {
                    continue;
                }

                double middle = (stops[k] + stops[k + 1]) * 0.5;
                int covering = 0;

                foreach (int m in members)
                {
                    double t0 = origin.GetVectorTo(segments[m].Start).DotProduct(axis);
                    double t1 = origin.GetVectorTo(segments[m].End).DotProduct(axis);

                    if (middle > Math.Min(t0, t1) && middle < Math.Max(t0, t1))
                    {
                        covering++;
                    }
                }

                if (covering % 2 != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether two segments lie along one line and share a stretch of it longer than the
        /// tolerance.
        /// </summary>
        private static bool Overlap(Segment a, Segment b, Tolerance tolerance)
        {
            // Measured along the longer of the two: the direction of a short edge, carried out along a long one, strays
            // by more than the tolerance where the long one does not. An edge a millimetre long a thousandth out of
            // true was read as off the line of the 300 mm edge beside it, and a closed body was called open.
            if (b.Start.GetVectorTo(b.End).LengthSquared > a.Start.GetVectorTo(a.End).LengthSquared)
            {
                (a, b) = (b, a);
            }

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
        /// A stretch of one line edges of the faces lie along, and how they cover it.
        /// </summary>
        internal struct Stretch
        {
            /// <summary>Where it starts, on the line of the longest edge along it.</summary>
            public GeoPoint3 Start;

            /// <summary>Where it ends.</summary>
            public GeoPoint3 End;

            /// <summary>How many of the edges covering it run from its start to its end, their face on the left.</summary>
            public int Forward;

            /// <summary>How many run from its end to its start.</summary>
            public int Backward;

            /// <summary>The faces the edges covering it belong to, by index, each once, lowest first.</summary>
            public int[] Faces;
        }

        /// <summary>
        /// Determines whether faces close up wound alike: every stretch of every edge shared by an even number of them, as
        /// many running it one way as the other.
        /// </summary>
        /// <remarks>
        /// <see cref="ClosesUp"/> asks only the first. A face turned the wrong way round closes up as well, and so do two
        /// faces lying over each other facing the same way, and either encloses the wrong volume.
        /// </remarks>
        internal static bool ClosesAlike(IReadOnlyList<GeoFace3> faces, Tolerance tolerance)
        {
            if (faces.Count == 0)
            {
                return false;
            }

            foreach (Stretch stretch in UnevenStretches(faces, tolerance))
            {
                if ((stretch.Forward + stretch.Backward) % 2 != 0 || stretch.Forward != stretch.Backward)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Every stretch of edge the faces do not close up along as two faces wound alike do: covered by an odd number of
        /// edges, by not as many one way as the other, or by more than two.
        /// </summary>
        /// <remarks>
        /// The edges are matched as <see cref="ClosesUp"/> matches them, by overlap along a line, so the faces close up
        /// exactly where no stretch is covered an odd number of times. Each edge is taken the way it runs with its face on
        /// the left, a hole's the other way round from how it is wound, and the two faces meeting on a stretch of a body
        /// wound alike run it once each way. Neighbouring stretches covered alike, by the same faces, are one.
        /// </remarks>
        internal static List<Stretch> UnevenStretches(IReadOnlyList<GeoFace3> faces, Tolerance tolerance)
        {
            List<Segment> segments = CollectSegments(faces, tolerance);
            int[] line = new int[segments.Count];

            for (int i = 0; i < line.Length; i++)
            {
                line[i] = i;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                Segment a = segments[i];

                for (int j = i + 1; j < segments.Count && segments[j].Low <= a.High; j++)
                {
                    if (Overlap(a, segments[j], tolerance))
                    {
                        Union(line, i, j);
                    }
                }
            }

            // Taken in the order the sweep met them, so the same faces give the same stretches in the same order.
            var groups = new Dictionary<int, List<int>>();
            var lines = new List<List<int>>();

            for (int i = 0; i < segments.Count; i++)
            {
                int root = Find(line, i);

                if (!groups.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    groups.Add(root, members);
                    lines.Add(members);
                }

                members.Add(i);
            }

            var found = new List<Stretch>();

            foreach (List<int> members in lines)
            {
                AddUneven(segments, members, tolerance, found);
            }

            return found;
        }

        /// <summary>
        /// Adds the stretches of one line its edges do not close up along.
        /// </summary>
        private static void AddUneven(List<Segment> segments, List<int> members, Tolerance tolerance, List<Stretch> found)
        {
            // Along the longest of them, whose direction is the truest; see Overlap.
            Segment first = segments[members[0]];

            foreach (int m in members)
            {
                if (segments[m].Start.GetVectorTo(segments[m].End).LengthSquared > first.Start.GetVectorTo(first.End).LengthSquared)
                {
                    first = segments[m];
                }
            }

            GeoPoint3 origin = first.Start;

            // Every member is longer than the point tolerance, so it has a length to divide by; see EvenAlong.
            GeoVector3 along = first.Start.GetVectorTo(first.End);
            GeoVector3 axis = along.Divide(along.Length);

            var stops = new List<double>();

            foreach (int m in members)
            {
                stops.Add(origin.GetVectorTo(segments[m].Start).DotProduct(axis));
                stops.Add(origin.GetVectorTo(segments[m].End).DotProduct(axis));
            }

            stops.Sort();

            var covering = new List<int>();
            bool pending = false;
            Stretch run = default;

            for (int k = 0; k + 1 < stops.Count; k++)
            {
                // A stretch no longer than the tolerance is the corners of two edges that are one point, as EvenAlong
                // reads it.
                if (stops[k + 1] - stops[k] <= tolerance.EqualPoint)
                {
                    continue;
                }

                double middle = (stops[k] + stops[k + 1]) * 0.5;
                int forward = 0, backward = 0;
                covering.Clear();

                foreach (int m in members)
                {
                    Segment s = segments[m];
                    double t0 = origin.GetVectorTo(s.Start).DotProduct(axis);
                    double t1 = origin.GetVectorTo(s.End).DotProduct(axis);

                    if (!(middle > Math.Min(t0, t1) && middle < Math.Max(t0, t1)))
                    {
                        continue;
                    }

                    // A hole is wound as the outline is, its face on its right, so it runs the other way round with
                    // its face on its left.
                    if ((t1 > t0) != s.Hole)
                    {
                        forward++;
                    }
                    else
                    {
                        backward++;
                    }

                    if (!covering.Contains(s.Face))
                    {
                        covering.Add(s.Face);
                    }
                }

                int count = forward + backward;
                bool uneven = count % 2 != 0 || forward != backward || count > 2;

                covering.Sort();

                if (pending && uneven && run.Forward == forward && run.Backward == backward && SameFaces(run.Faces, covering))
                {
                    run.End = origin.Add(axis.Multiply(stops[k + 1]));
                    continue;
                }

                if (pending)
                {
                    found.Add(run);
                    pending = false;
                }

                if (uneven)
                {
                    run = new Stretch
                    {
                        Start = origin.Add(axis.Multiply(stops[k])),
                        End = origin.Add(axis.Multiply(stops[k + 1])),
                        Forward = forward,
                        Backward = backward,
                        Faces = covering.ToArray(),
                    };
                    pending = true;
                }
            }

            if (pending)
            {
                found.Add(run);
            }
        }

        private static bool SameFaces(int[] faces, List<int> other)
        {
            if (faces.Length != other.Count)
            {
                return false;
            }

            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] != other[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Settles one line: along every stretch of it, which of the faces meeting there are one piece.
        /// </summary>
        private static void SettleLine(List<Segment> segments, List<int> members, int[] parent, Tolerance tolerance)
        {
            Segment first = segments[members[0]];
            GeoPoint3 origin = first.Start;

            // Every member shares a stretch longer than the point tolerance with another, so it has a length to
            // divide by. Normalize would judge that length against the default tolerance instead of this one, and
            // refuse a segment a boolean run tighter than the default is right to keep.
            GeoVector3 along = first.Start.GetVectorTo(first.End);
            GeoVector3 axis = along.Divide(along.Length);

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
        private static List<GeoSolid3> Assemble(List<List<GeoFace3>> found, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            var groups = new List<List<GeoFace3>>();
            var sheets = new List<List<GeoFace3>>();
            var shells = new List<GeoSolid3>();

            foreach (List<GeoFace3> group in found)
            {
                if (EnclosesNothing(group, tolerance))
                {
                    sheets.Add(group);
                    continue;
                }

                groups.Add(group);
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
                    if (outer[o].GrossVolume < smallest
                        && outer[o].GetAabb().Contains(witness, tolerance)
                        && Containment3.Locate(outer[o], witness, tolerance) != PointLocation.OutSide)
                    {
                        home = o;
                        smallest = outer[o].GrossVolume;
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

            // A sheet goes with the smallest piece holding it. One that no piece holds and that cannot close, a
            // lone face or two back to back, stands nowhere in the material, so there is nothing to give it to;
            // one that can is a sliver of material thinner than the tolerance, and is a piece of its own. Dropped,
            // the slivers a plane left along the face of a slab, two hundredths of a millimetre at their thickest
            // and tens of metres long, took seventy thousand cubic millimetres of it away.
            foreach (List<GeoFace3> sheet in sheets)
            {
                GeoPoint3 witness = sheet[0].Centroid;
                int home = -1;
                double smallest = double.MaxValue;

                for (int o = 0; o < outer.Count; o++)
                {
                    if (outer[o].GrossVolume < smallest
                        && outer[o].GetAabb().Contains(witness, tolerance)
                        && Containment3.Locate(outer[o], witness, tolerance) != PointLocation.OutSide)
                    {
                        home = o;
                        smallest = outer[o].GrossVolume;
                    }
                }

                if (home >= 0)
                {
                    solids[home].AddRange(sheet);
                }
                else if (sheet.Count >= 4)
                {
                    solids.Add(new List<GeoFace3>(sheet));
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

        /// <summary>
        /// Determines whether a group of faces encloses no volume: too few faces to close, or no thicker than
        /// the tolerance on average.
        /// </summary>
        private static bool EnclosesNothing(List<GeoFace3> group, Tolerance tolerance)
        {
            if (group.Count < 4)
            {
                return true;
            }

            // The divergence theorem face by face: each plane face adds a third of its area times the reach of
            // its plane from the origin along its outward normal.
            double volume = 0.0;
            double area = 0.0;

            foreach (GeoFace3 face in group)
            {
                GeoVector3 normal = face.Boundary.Normal;
                GeoPoint3 point = face.Boundary[0];

                volume += (normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z) * face.Area;
                area += face.Area;
            }

            return Math.Abs(volume / 3.0) <= tolerance.EqualPoint * area;
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
