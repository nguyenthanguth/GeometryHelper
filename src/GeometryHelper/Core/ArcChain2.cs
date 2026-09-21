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
        /// </remarks>
        internal static bool Encloses(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            var chords = new GeoPoint2[loop.VertexCount];

            for (int i = 0; i < loop.VertexCount; i++)
            {
                chords[i] = loop[i];
            }

            bool inside = Containment2.Contains(new GeoPolygon2(chords), point, tolerance);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                if (loop.GetEdgeAt(i).CutsOff(point, tolerance))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
