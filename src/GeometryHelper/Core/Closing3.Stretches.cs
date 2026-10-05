using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    internal static partial class Closing3
    {
        /// <summary>
        /// Every stretch the edges of the faces lie along, and how each edge covering it runs it.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="skip">For each face, whether it is left out.</param>
        /// <param name="tolerance">The tolerance edges are matched within.</param>
        /// <returns>The stretches, line by line in the order a sweep meets the lines, and along each line in order.</returns>
        /// <remarks>
        /// <para>
        /// The edges are matched as <see cref="Shells3.UnevenStretches"/> matches them for
        /// <see cref="GeoSolid3.Validate(Tolerance)"/>, by overlap along a line within the point tolerance, so that a stretch
        /// read open here is one the check reads open: edges no longer than the tolerance are left out, two edges are on one
        /// line where each end of the shorter stands within the tolerance of the longer's line and they share more than the
        /// tolerance of it, and a line is cut into stretches at every end of an edge along it, those closer together than
        /// the tolerance taken as one.
        /// </para>
        /// <para>
        /// Unlike the check, every stretch is given, those closed alike as well, and each with the face of every edge on it
        /// and which way that edge runs it. The ends of a stretch are corners of the faces, not points on the line through
        /// them, so that the stretches of two lines meeting at a corner meet at that corner. Wherever two things weigh
        /// alike, the first by index is taken: the order the faces come in decides, never the order a sort happens to leave.
        /// </para>
        /// </remarks>
        private static List<Stretch> FindStretches(IReadOnlyList<GeoFace3> faces, bool[] skip, Tolerance tolerance)
        {
            double reach = tolerance.EqualPoint;
            var segments = new List<Segment>();

            for (int f = 0; f < faces.Count; f++)
            {
                if (skip[f])
                {
                    continue;
                }

                AddRing(segments, faces[f].Boundary, f, false, reach);

                foreach (GeoPolygon3 hole in faces[f].Holes)
                {
                    AddRing(segments, hole, f, true, reach);
                }
            }

            var stretches = new List<Stretch>();
            int count = segments.Count;

            if (count == 0)
            {
                return stretches;
            }

            // Swept along the axis that leaves the fewest pairs to try, as the check sweeps; see Shells3.
            int axis = SweepAxis(segments, reach);
            var lows = new double[count];
            var highs = new double[count];
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                Extent(segments[i], axis, reach, out lows[i], out highs[i]);
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byLow = lows[a].CompareTo(lows[b]);
                return byLow != 0 ? byLow : a.CompareTo(b);
            });

            var line = new int[count];

            for (int i = 0; i < count; i++)
            {
                line[i] = i;
            }

            for (int k = 0; k < count; k++)
            {
                int i = order[k];

                for (int m = k + 1; m < count && lows[order[m]] <= highs[i]; m++)
                {
                    int j = order[m];

                    if (Overlap(segments, i, j, reach))
                    {
                        Union(line, i, j);
                    }
                }
            }

            var lines = new List<List<int>>();
            var lineOf = new Dictionary<int, List<int>>();

            foreach (int i in order)
            {
                int root = Find(line, i);

                if (!lineOf.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    lineOf.Add(root, members);
                    lines.Add(members);
                }

                members.Add(i);
            }

            foreach (List<int> members in lines)
            {
                members.Sort();
                AddStretches(segments, members, reach, stretches);
            }

            return stretches;
        }

        /// <summary>
        /// Adds the edges of a ring longer than the tolerance.
        /// </summary>
        /// <param name="segments">The edges so far.</param>
        /// <param name="ring">The ring.</param>
        /// <param name="face">The face it is a ring of, by index.</param>
        /// <param name="hole">Whether it is a hole.</param>
        /// <param name="reach">The point tolerance.</param>
        private static void AddRing(List<Segment> segments, GeoPolygon3 ring, int face, bool hole, double reach)
        {
            IReadOnlyList<GeoPoint3> corners = ring.Vertices;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint3 start = corners[i];
                GeoPoint3 end = corners[(i + 1) % corners.Count];

                if (start.DistanceTo(end) <= reach)
                {
                    continue;
                }

                segments.Add(new Segment { Start = start, End = end, Face = face, Hole = hole });
            }
        }

        /// <summary>
        /// Adds the stretches of one line: between each two neighbouring ends of its edges further apart than the
        /// tolerance, the edges covering the middle of it, and which way each runs it.
        /// </summary>
        /// <param name="segments">Every edge.</param>
        /// <param name="members">The edges along the line, by index, lowest first.</param>
        /// <param name="reach">The point tolerance.</param>
        /// <param name="stretches">The stretches so far.</param>
        private static void AddStretches(List<Segment> segments, List<int> members, double reach, List<Stretch> stretches)
        {
            // Along the longest edge, whose direction is the truest, the first of them where two are as long.
            int longest = members[0];

            foreach (int m in members)
            {
                if (LengthSquared(segments[m]) > LengthSquared(segments[longest]))
                {
                    longest = m;
                }
            }

            GeoPoint3 origin = segments[longest].Start;
            GeoVector3 along = origin.GetVectorTo(segments[longest].End);
            GeoVector3 axis = along.Divide(along.Length);

            var from = new double[members.Count];
            var to = new double[members.Count];
            var stops = new List<(double At, int Member, bool IsEnd, GeoPoint3 Point)>(2 * members.Count);

            for (int k = 0; k < members.Count; k++)
            {
                Segment s = segments[members[k]];
                from[k] = origin.GetVectorTo(s.Start).DotProduct(axis);
                to[k] = origin.GetVectorTo(s.End).DotProduct(axis);
                stops.Add((from[k], k, false, s.Start));
                stops.Add((to[k], k, true, s.End));
            }

            stops.Sort((a, b) =>
            {
                int byAt = a.At.CompareTo(b.At);

                if (byAt != 0)
                {
                    return byAt;
                }

                int byMember = a.Member.CompareTo(b.Member);
                return byMember != 0 ? byMember : a.IsEnd.CompareTo(b.IsEnd);
            });

            var faces = new List<int>();
            var forward = new List<bool>();

            for (int k = 0; k + 1 < stops.Count; k++)
            {
                // Ends closer together than the tolerance are one corner, as the check reads them.
                if (stops[k + 1].At - stops[k].At <= reach)
                {
                    continue;
                }

                double middle = (stops[k].At + stops[k + 1].At) * 0.5;
                faces.Clear();
                forward.Clear();

                for (int m = 0; m < members.Count; m++)
                {
                    if (!(middle > Math.Min(from[m], to[m]) && middle < Math.Max(from[m], to[m])))
                    {
                        continue;
                    }

                    Segment s = segments[members[m]];
                    faces.Add(s.Face);

                    // A hole is wound as the boundary is, its face on its right, so it runs the other way round with its
                    // face on its left.
                    forward.Add((to[m] > from[m]) != s.Hole);
                }

                stretches.Add(new Stretch(stops[k].Point, stops[k + 1].Point, faces.ToArray(), forward.ToArray()));
            }
        }

        /// <summary>
        /// The axis a sweep of the edges tries the fewest pairs along, the first of them where two try as many.
        /// </summary>
        /// <param name="segments">The edges.</param>
        /// <param name="reach">The point tolerance.</param>
        private static int SweepAxis(List<Segment> segments, double reach)
        {
            int count = segments.Count;
            var lows = new double[count];
            var highs = new double[count];
            int axis = 0;
            long fewest = long.MaxValue;

            for (int candidate = 0; candidate < 3; candidate++)
            {
                for (int i = 0; i < count; i++)
                {
                    Extent(segments[i], candidate, reach, out lows[i], out highs[i]);
                }

                Array.Sort(lows);
                long tried = Tried(lows, highs);

                if (tried < fewest)
                {
                    fewest = tried;
                    axis = candidate;
                }
            }

            return axis;
        }

        /// <summary>
        /// How many pairs a sweep tries: each edge, taken by where it starts, against every later one that starts no further
        /// along than it ends.
        /// </summary>
        /// <param name="lows">Where the edges start, sorted.</param>
        /// <param name="highs">Where they end, in any order.</param>
        private static long Tried(double[] lows, double[] highs)
        {
            long reached = 0;

            foreach (double high in highs)
            {
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

        /// <summary>
        /// Where an edge starts and ends along an axis, widened by the tolerance either way.
        /// </summary>
        /// <param name="segment">The edge.</param>
        /// <param name="axis">The axis: 0, 1 or 2 for x, y or z.</param>
        /// <param name="reach">The point tolerance.</param>
        /// <param name="low">Where it starts, less the tolerance.</param>
        /// <param name="high">Where it ends, and the tolerance more.</param>
        private static void Extent(Segment segment, int axis, double reach, out double low, out double high)
        {
            double a = Coordinate(segment.Start, axis);
            double b = Coordinate(segment.End, axis);

            low = Math.Min(a, b) - reach;
            high = Math.Max(a, b) + reach;
        }

        /// <summary>
        /// Determines whether two edges lie along one line and share a stretch of it longer than the tolerance, measured
        /// along the longer of the two, the first by index where they are as long.
        /// </summary>
        /// <param name="segments">Every edge.</param>
        /// <param name="i">The one edge, by index.</param>
        /// <param name="j">The other.</param>
        /// <param name="reach">The point tolerance.</param>
        private static bool Overlap(List<Segment> segments, int i, int j, double reach)
        {
            Segment a = segments[i];
            Segment b = segments[j];
            double first = LengthSquared(a);
            double second = LengthSquared(b);

            if (second > first || (second == first && j < i))
            {
                (a, b) = (b, a);
            }

            GeoVector3 along = a.Start.GetVectorTo(a.End);
            double length = along.Length;
            GeoVector3 unit = along.Divide(length);

            if (OffLine(a.Start, unit, b.Start) > reach || OffLine(a.Start, unit, b.End) > reach)
            {
                return false;
            }

            double t0 = a.Start.GetVectorTo(b.Start).DotProduct(unit);
            double t1 = a.Start.GetVectorTo(b.End).DotProduct(unit);

            double low = Math.Max(0.0, Math.Min(t0, t1));
            double high = Math.Min(length, Math.Max(t0, t1));

            return high - low > reach;
        }

        /// <summary>
        /// How far a point stands off a line.
        /// </summary>
        /// <param name="origin">A point of the line.</param>
        /// <param name="unit">Its direction, of unit length.</param>
        /// <param name="point">The point.</param>
        private static double OffLine(GeoPoint3 origin, GeoVector3 unit, GeoPoint3 point)
        {
            GeoVector3 offset = origin.GetVectorTo(point);

            return offset.Subtract(unit.Multiply(offset.DotProduct(unit))).Length;
        }

        /// <summary>
        /// The square of how long an edge is.
        /// </summary>
        /// <param name="segment">The edge.</param>
        private static double LengthSquared(Segment segment) => segment.Start.GetVectorTo(segment.End).LengthSquared;

        /// <summary>
        /// A coordinate of a point: 0, 1 or 2 for x, y or z.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="axis">Which.</param>
        private static double Coordinate(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        /// <summary>
        /// The root of a set, the sets kept by their lowest member.
        /// </summary>
        /// <param name="parent">The parent of each member.</param>
        /// <param name="i">The member.</param>
        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        /// <summary>
        /// Joins the sets of two members, under the lower root.
        /// </summary>
        /// <param name="parent">The parent of each member.</param>
        /// <param name="a">The one member.</param>
        /// <param name="b">The other.</param>
        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);

            if (ra != rb)
            {
                parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
        }

        /// <summary>
        /// One edge of one ring of one face, as the ring runs it.
        /// </summary>
        private struct Segment
        {
            /// <summary>Where it starts.</summary>
            internal GeoPoint3 Start;

            /// <summary>Where it ends.</summary>
            internal GeoPoint3 End;

            /// <summary>The face it is an edge of, by index.</summary>
            internal int Face;

            /// <summary>Whether it is an edge of a hole, wound as the boundary is, its face on its right.</summary>
            internal bool Hole;
        }

        /// <summary>
        /// A stretch of one line the edges of the faces lie along, between two neighbouring ends of them, and how each edge
        /// covering it runs it.
        /// </summary>
        private sealed class Stretch
        {
            /// <summary>
            /// Initializes a stretch.
            /// </summary>
            /// <param name="start">Where it starts: a corner of a face.</param>
            /// <param name="end">Where it ends: another.</param>
            /// <param name="faces">The face of each edge covering it.</param>
            /// <param name="forward">Whether each edge runs it from its start to its end, its face on the left.</param>
            internal Stretch(GeoPoint3 start, GeoPoint3 end, int[] faces, bool[] forward)
            {
                Start = start;
                End = end;
                Faces = faces;
                Forward = forward;
            }

            /// <summary>Gets where it starts: a corner of a face.</summary>
            internal GeoPoint3 Start { get; }

            /// <summary>Gets where it ends: another.</summary>
            internal GeoPoint3 End { get; }

            /// <summary>Gets the face of each edge covering it, by index, in the order of the edges.</summary>
            internal int[] Faces { get; }

            /// <summary>Gets whether each edge covering it runs it from its start to its end, its face on the left.</summary>
            internal bool[] Forward { get; }

            /// <summary>Gets the middle of it.</summary>
            internal GeoPoint3 Middle => Start.GetMiddlePoint(End);
        }
    }
}
