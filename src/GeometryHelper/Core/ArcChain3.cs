using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Walking a run of edges in space that may curve.
    /// </summary>
    /// <remarks>
    /// The twin of <see cref="ArcChain2"/>, and deliberately much smaller. Everything here works on the
    /// curve itself — its length, a point along it, the point of it nearest another point — and every one of
    /// those is exact, because <see cref="GeoArc3"/> answers a point in closed form. What is missing is
    /// everything that measures a run against another shape, which in space has no closed form once the two
    /// are not in one plane.
    /// </remarks>
    internal static class ArcChain3
    {
        /// <summary>
        /// Gets the edges of a chain that may curve.
        /// </summary>
        internal static List<GeoEdge3> EdgesOf(GeoPolylineArc3 chain)
        {
            var edges = new List<GeoEdge3>(chain.EdgeCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                edges.Add(chain.GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Gets the length of a run of edges, measured along its arcs.
        /// </summary>
        internal static double LengthOf(List<GeoEdge3> edges)
        {
            double total = 0.0;

            foreach (GeoEdge3 edge in edges)
            {
                total += edge.Length;
            }

            return total;
        }

        /// <summary>
        /// Gets the point a given distance along a run of edges.
        /// </summary>
        /// <remarks>
        /// A distance before the start or past the end is held to the run, so the answer is always a point
        /// of it.
        /// </remarks>
        internal static GeoPoint3 PointAtDistance(List<GeoEdge3> edges, double distance)
        {
            if (distance <= 0.0)
            {
                return edges[0].StartPoint;
            }

            double walked = 0.0;

            foreach (GeoEdge3 edge in edges)
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
        /// Gets the point of a run of edges nearest another point.
        /// </summary>
        internal static GeoPoint3 ClosestPoint(List<GeoEdge3> edges, GeoPoint3 point, Tolerance tolerance)
        {
            GeoPoint3 best = edges[0].GetClosestPointOnBoundary(point, tolerance);
            double reach = point.DistanceTo(best);

            for (int i = 1; i < edges.Count; i++)
            {
                GeoPoint3 candidate = edges[i].GetClosestPointOnBoundary(point, tolerance);
                double distance = point.DistanceTo(candidate);

                if (distance < reach)
                {
                    reach = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets how far along a run of edges the point nearest another one sits.
        /// </summary>
        internal static double DistanceAtPoint(List<GeoEdge3> edges, GeoPoint3 point, Tolerance tolerance)
        {
            double walked = 0.0;
            double bestWalk = 0.0;
            double best = double.MaxValue;

            foreach (GeoEdge3 edge in edges)
            {
                GeoPoint3 candidate = edge.GetClosestPointOnBoundary(point, tolerance);
                double distance = point.DistanceTo(candidate);

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
        internal static double DistanceTo(List<GeoEdge3> edges, GeoPoint3 point, Tolerance tolerance)
        {
            return point.DistanceTo(ClosestPoint(edges, point, tolerance));
        }
    }
}
