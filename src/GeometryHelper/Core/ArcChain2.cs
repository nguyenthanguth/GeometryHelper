using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The walking a chain that may curve needs, shared by every operation over one.
    /// <para>
    /// Everything here measures along the arcs rather than across them, so a chain of arcs answers the same
    /// questions a straight one does and answers them exactly. Only the region operations, which go through
    /// Clipper, need the chain flattened first.
    /// </para>
    /// </summary>
    internal static class ArcChain2
    {
        /// <summary>
        /// Gets the edges of a chain, in order.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolylineArc2 chain)
        {
            var edges = new List<GeoEdge2>(chain.EdgeCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                edges.Add(chain.GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a loop, in order, ending with the one that closes it.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolygonArc2 loop)
        {
            var edges = new List<GeoEdge2>(loop.EdgeCount);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                edges.Add(loop.GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a straight chain, so that one shape can be measured against the other without
        /// either being approximated.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolyline2 chain)
        {
            var edges = new List<GeoEdge2>(chain.VertexCount - 1);

            for (int i = 0; i < chain.VertexCount - 1; i++)
            {
                edges.Add(new GeoEdge2(chain[i], chain[i + 1]));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a straight loop.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolygon2 loop)
        {
            var edges = new List<GeoEdge2>(loop.VertexCount);

            for (int i = 0; i < loop.VertexCount; i++)
            {
                edges.Add(new GeoEdge2(loop[i], loop[(i + 1) % loop.VertexCount]));
            }

            return edges;
        }

        /// <summary>
        /// Gets the length along a run of edges, measured along the arcs.
        /// </summary>
        internal static double LengthOf(List<GeoEdge2> edges)
        {
            double total = 0.0;

            foreach (GeoEdge2 edge in edges)
            {
                total += edge.Length;
            }

            return total;
        }

        /// <summary>
        /// Gets the point a distance along a run of edges, clamped to its two ends.
        /// </summary>
        internal static GeoPoint2 PointAtDistance(List<GeoEdge2> edges, double distance)
        {
            if (double.IsNaN(distance) || distance <= 0.0)
            {
                return edges[0].StartPoint;
            }

            double walked = 0.0;

            foreach (GeoEdge2 edge in edges)
            {
                double length = edge.Length;

                if (distance <= walked + length)
                {
                    return length <= 0.0
                        ? edge.StartPoint
                        : edge.GetPointAtParameter((distance - walked) / length);
                }

                walked += length;
            }

            return edges[edges.Count - 1].EndPoint;
        }

        /// <summary>
        /// Gets the point of a run of edges nearest a point.
        /// </summary>
        internal static GeoPoint2 ClosestPoint(List<GeoEdge2> edges, GeoPoint2 point, Tolerance tolerance)
        {
            GeoPoint2 nearest = edges[0].GetClosestPointOnBoundary(point, tolerance);
            double best = point.GetDistanceSquaredTo(nearest);

            for (int i = 1; i < edges.Count; i++)
            {
                GeoPoint2 candidate = edges[i].GetClosestPointOnBoundary(point, tolerance);
                double distance = point.GetDistanceSquaredTo(candidate);

                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Gets how far along a run of edges the point nearest a point lies.
        /// </summary>
        internal static double DistanceAtPoint(List<GeoEdge2> edges, GeoPoint2 point, Tolerance tolerance)
        {
            double walked = 0.0;
            double bestWalk = 0.0;
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                GeoPoint2 candidate = edge.GetClosestPointOnBoundary(point, tolerance);
                double distance = point.GetDistanceSquaredTo(candidate);

                if (distance < best)
                {
                    best = distance;
                    bestWalk = walked + edge.GetParameterAtPoint(point, tolerance) * edge.Length;
                }

                walked += edge.Length;
            }

            return bestWalk;
        }

        /// <summary>
        /// Gets the distance from a run of edges to a point.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoPoint2 point, Tolerance tolerance)
        {
            return point.DistanceTo(ClosestPoint(edges, point, tolerance));
        }

        /// <summary>
        /// Gets the distance between two runs of edges, measured along their arcs.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    double distance = one.DistanceTo(other, tolerance);

                    if (distance < best)
                    {
                        best = distance;

                        if (best <= 0.0)
                        {
                            return 0.0;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a run of edges to a straight segment.
        /// </summary>
        /// <remarks>
        /// Nothing here knows whether the run closes on itself, so nothing here treats it as a filled
        /// region: the answer runs from the boundary to the other shape, which is what
        /// <see cref="Projection2"/> promises of every shape and where it parts company with
        /// <see cref="Distance2"/>.
        /// </remarks>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(line, tolerance);

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(line, tolerance));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a run of edges to an arc.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(arc, tolerance);

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(arc, tolerance));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a run of edges to a circle.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(circle, tolerance);

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(circle, tolerance));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two runs of edges.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            GeoLine2 best = first[0].GetShortestLineTo(second[0], tolerance);

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    Consider(ref best, one.GetShortestLineTo(other, tolerance));

                    if (best.Length <= 0.0)
                    {
                        return best;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Keeps the shorter of the segment in hand and the one offered.
        /// </summary>
        /// <remarks>
        /// The comparison is strict, so where two pieces of the run are equally near the earlier one is
        /// the one the answer comes from.
        /// </remarks>
        private static void Consider(ref GeoLine2 best, GeoLine2 candidate)
        {
            if (candidate.Length < best.Length)
            {
                best = candidate;
            }
        }

        /// <summary>
        /// Gets the points where two runs of edges meet, with repeats left out.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    foreach (GeoPoint2 meeting in one.GetIntersections(other, tolerance))
                    {
                        Keep(found, meeting, tolerance);
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether two runs of edges cross anywhere: two straight edges where they truly cross, at a point on
        /// both, with no slack past their ends, and a pair with an arc in it where its crossings say so.
        /// </summary>
        internal static bool Cross(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    bool crossing = one.IsArc || other.IsArc
                        ? one.GetIntersections(other, tolerance).Length > 0
                        : Collision2.Crosses(one.ToLine(), other.ToLine(), tolerance);

                    if (crossing)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges crosses a straight segment anywhere: a straight edge where the two truly
        /// cross, with no slack past their ends, and an arc where its crossings say so.
        /// </summary>
        internal static bool Cross(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            foreach (GeoEdge2 edge in edges)
            {
                bool crossing = edge.IsArc
                    ? edge.GetIntersections(line, tolerance).Length > 0
                    : Collision2.Crosses(edge.ToLine(), line, tolerance);

                if (crossing)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a point to a list unless the same point is already in it.
        /// </summary>
        /// <remarks>
        /// Two edges that share a vertex meet there, and so do the arcs either side of it, so the same
        /// meeting point arrives more than once and is worth reporting once.
        /// </remarks>
        internal static void Keep(List<GeoPoint2> found, GeoPoint2 point, Tolerance tolerance)
        {
            foreach (GeoPoint2 already in found)
            {
                if (already.IsEqualTo(point, tolerance))
                {
                    return;
                }
            }

            found.Add(point);
        }

        /// <summary>
        /// Gets the distance from a run of edges to a straight segment.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = edge.DistanceTo(line, tolerance);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the distance from a run of edges to an arc.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = edge.DistanceTo(arc, tolerance);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the distance from a run of edges to a circle.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = edge.DistanceTo(circle, tolerance);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Determines whether two runs of edges come within the point tolerance of each other anywhere.
        /// </summary>
        /// <remarks>
        /// Edge against edge, each pair measured exactly, a segment against a segment, a segment against an arc or
        /// two arcs, once a test of their boxes has set aside the pairs more than twice the point tolerance apart; the
        /// first pair near enough ends the search. Nothing here asks whether the two collide, so the collision tests
        /// can lean on it.
        /// </remarks>
        internal static bool ComeWithin(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            var boxes = new Box[second.Count];

            for (int j = 0; j < second.Count; j++)
            {
                boxes[j] = Box.Of(second[j]);
            }

            foreach (GeoEdge2 one in first)
            {
                Box box = Box.Of(one);

                for (int j = 0; j < second.Count; j++)
                {
                    if (box.Near(boxes[j], tolerance) && one.DistanceTo(second[j], tolerance) <= tolerance.EqualPoint)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges comes within the point tolerance of an arc anywhere.
        /// </summary>
        internal static bool ComeWithin(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            Box box = Box.Around(arc.Center, arc.Radius);

            foreach (GeoEdge2 edge in edges)
            {
                if (box.Near(Box.Of(edge), tolerance) && edge.DistanceTo(arc, tolerance) <= tolerance.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges comes within the point tolerance of a circle, read as the disc it bounds.
        /// </summary>
        internal static bool ComeWithin(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            Box box = Box.Around(circle.Center, circle.Radius);

            foreach (GeoEdge2 edge in edges)
            {
                if (box.Near(Box.Of(edge), tolerance) && edge.DistanceTo(circle, tolerance) <= tolerance.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A box holding an edge, an arc or a circle, for setting aside the pairs that are plainly apart.
        /// </summary>
        private readonly struct Box
        {
            private readonly double _minX;
            private readonly double _minY;
            private readonly double _maxX;
            private readonly double _maxY;

            private Box(double minX, double minY, double maxX, double maxY)
            {
                _minX = minX;
                _minY = minY;
                _maxX = maxX;
                _maxY = maxY;
            }

            /// <summary>
            /// The box of an edge. An arc of at most a half turn, a bulge of at most one, lies within its sagitta,
            /// half the chord times the size of the bulge, of its chord, so within the chord's box grown by that much.
            /// A longer one lies within half its chord times the size of the bulge of the chord's middle.
            /// </summary>
            internal static Box Of(GeoEdge2 edge)
            {
                var chord = new Box(
                    Math.Min(edge.StartPoint.X, edge.EndPoint.X), Math.Min(edge.StartPoint.Y, edge.EndPoint.Y),
                    Math.Max(edge.StartPoint.X, edge.EndPoint.X), Math.Max(edge.StartPoint.Y, edge.EndPoint.Y));

                if (!edge.IsArc)
                {
                    return chord;
                }

                double bulge = Math.Abs(edge.Bulge);
                double half = 0.5 * Distance2.DistanceTo(edge.StartPoint, edge.EndPoint);

                if (bulge <= 1.0)
                {
                    double sagitta = half * bulge;

                    return new Box(chord._minX - sagitta, chord._minY - sagitta, chord._maxX + sagitta, chord._maxY + sagitta);
                }

                return Around(
                    new GeoPoint2(0.5 * (edge.StartPoint.X + edge.EndPoint.X), 0.5 * (edge.StartPoint.Y + edge.EndPoint.Y)),
                    half * bulge);
            }

            /// <summary>
            /// The box of everything within a reach of a centre, which holds an arc or a circle about it.
            /// </summary>
            internal static Box Around(GeoPoint2 centre, double reach)
                => new Box(centre.X - reach, centre.Y - reach, centre.X + reach, centre.Y + reach);

            /// <summary>
            /// Whether two boxes come within twice the point tolerance of each other, so that nothing within the
            /// tolerance is set aside.
            /// </summary>
            internal bool Near(Box other, Tolerance tolerance)
            {
                double reach = 2.0 * tolerance.EqualPoint;

                return other._minX - _maxX <= reach
                    && _minX - other._maxX <= reach
                    && other._minY - _maxY <= reach
                    && _minY - other._maxY <= reach;
            }
        }

        /// <summary>
        /// Gets the points where a run of edges meets a straight segment.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 meeting in edge.GetIntersections(line, tolerance))
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the points where a run of edges meets an arc.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 meeting in edge.GetIntersections(arc, tolerance))
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the points where a run of edges meets a circle.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                GeoPoint2[] meetings = edge.IsArc
                    ? Arc2.GetIntersections(edge.ToArc(), circle, tolerance)
                    : Intersection2.GetIntersections(circle, edge.ToLine(), tolerance);

                foreach (GeoPoint2 meeting in meetings)
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Cuts a run of edges at distances along it, and returns the runs between the cuts.
        /// </summary>
        /// <remarks>
        /// A cut inside an arc leaves two arcs of the same radius rather than two chords, and a cut landing
        /// on a vertex ends the run there without splitting anything. A distance at either end, off the
        /// run, or repeated is ignored, so the runs always have something in them.
        /// </remarks>
        internal static List<List<GeoEdge2>> SplitAt(List<GeoEdge2> edges, IEnumerable<double> distances, Tolerance tolerance)
        {
            double total = LengthOf(edges);
            var cuts = new List<double>();

            foreach (double distance in distances)
            {
                if (double.IsNaN(distance) || distance <= tolerance.EqualPoint || distance >= total - tolerance.EqualPoint)
                {
                    continue;
                }

                cuts.Add(distance);
            }

            cuts.Sort();

            var runs = new List<List<GeoEdge2>>();
            var current = new List<GeoEdge2>();
            int next = 0;
            double startOfEdge = 0.0;

            foreach (GeoEdge2 edge in edges)
            {
                GeoEdge2 rest = edge;
                double restStart = startOfEdge;

                while (next < cuts.Count && cuts[next] < startOfEdge + edge.Length - tolerance.EqualPoint)
                {
                    double restLength = rest.Length;
                    double parameter = restLength <= 0.0 ? 0.0 : (cuts[next] - restStart) / restLength;

                    if (rest.TrySplitAtParameter(parameter, out GeoEdge2 before, out GeoEdge2 after, tolerance))
                    {
                        current.Add(before);
                        runs.Add(current);
                        current = new List<GeoEdge2>();
                        restStart = cuts[next];
                        rest = after;
                    }
                    else if (parameter <= 0.0 && current.Count > 0)
                    {
                        // The cut lands on the vertex this edge starts at, so the run ends there whole.
                        runs.Add(current);
                        current = new List<GeoEdge2>();
                    }

                    next++;
                }

                current.Add(rest);
                startOfEdge += edge.Length;
            }

            if (current.Count > 0)
            {
                runs.Add(current);
            }

            return runs;
        }

        /// <summary>
        /// Determines whether a loop that may curve encloses a point, not counting its boundary.
        /// </summary>
        /// <remarks>
        /// Walking the arcs rather than the chords adds the piece each arc cuts off its chord where the arc
        /// bulges outward, and takes it away where the arc bulges inward. So the answer is the straight
        /// loop through the vertices, turned inside out once for every such piece the point lies in. It is
        /// exact: no arc is approximated anywhere in it.
        /// <para>
        /// No tolerance is read here: a point within the tolerance of the true boundary is on it, and is
        /// settled before this is asked. A chord is not the boundary, so the straight loop and each piece
        /// read the side of a chord by one exact rule, <see cref="BeforeChord"/>, and agree on every point
        /// near it. A point exactly on a chord counts as lying a hair towards +X of it, or a hair towards +Y
        /// of a chord that runs along X.
        /// </para>
        /// </remarks>
        internal static bool Encloses(GeoPolygonArc2 loop, GeoPoint2 point)
        {
            // The straight loop through the vertices, by crossings of the ray from the point towards +X: a chord counts
            // when it spans the point's Y, half-open, and the point lies before it.
            bool inside = false;
            int n = loop.VertexCount;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                GeoPoint2 p1 = loop[i];
                GeoPoint2 p2 = loop[j];

                if ((p1.Y > point.Y) != (p2.Y > point.Y) && BeforeChord(p1, p2, point))
                {
                    inside = !inside;
                }
            }

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                if (loop.GetEdgeAt(i).CutsOff(point))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Determines whether a run of edges coming back to where it began encloses a point, not counting its boundary,
        /// read as <see cref="Encloses(GeoPolygonArc2, GeoPoint2)"/> reads a loop: the straight loop through the starts of
        /// the edges, turned inside out once for every piece an arc cuts off its chord that the point lies in.
        /// </summary>
        internal static bool Encloses(List<GeoEdge2> run, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = run.Count - 1; i < run.Count; j = i++)
            {
                GeoPoint2 p1 = run[i].StartPoint;
                GeoPoint2 p2 = run[j].StartPoint;

                if ((p1.Y > point.Y) != (p2.Y > point.Y) && BeforeChord(p1, p2, point))
                {
                    inside = !inside;
                }
            }

            foreach (GeoEdge2 edge in run)
            {
                if (edge.CutsOff(point))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Whether a point lies before the line through two points, read exactly: towards -X of it at the point's Y, or,
        /// for a line that runs along X, towards -Y of it. A point on the line is not before it.
        /// </summary>
        /// <remarks>
        /// The straight loop of <see cref="Encloses(GeoPolygonArc2, GeoPoint2)"/> and the pieces of
        /// <see cref="GeoEdge2.CutsOff"/> both read the side of a chord by this, with the chord's ends in the same order,
        /// so the two give the same answer to the last bit.
        /// </remarks>
        internal static bool BeforeChord(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 point)
        {
            if (p1.Y == p2.Y)
            {
                return point.Y < p1.Y;
            }

            return point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X;
        }
    }
}
