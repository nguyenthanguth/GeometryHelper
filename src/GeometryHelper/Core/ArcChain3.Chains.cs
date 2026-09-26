using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where one chain in space crosses another, and whether two chains touch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This was the one reading held back on purpose. Every other question a chain answers walks its edges once;
    /// this one walks every pair of edges, so it grows with the two edge counts multiplied and not added — two
    /// forty-edge bars are sixteen hundred edge pairs, each of them an arc against an arc.
    /// </para>
    /// <para>
    /// What makes it worth offering is that the work per pair is decided before any arithmetic is done: each edge
    /// carries a box round itself, and a pair whose boxes do not overlap is dropped without being solved. Bars in
    /// a model are mostly far apart, so the pairs that survive are the few that could possibly meet. The boxes of
    /// the second chain are worked out once and kept, rather than once per edge of the first.
    /// </para>
    /// <para>
    /// The answer itself is the union of what the edge pairs answer, with each place named once, which is the
    /// same reading every other chain question takes. Two edges lying along each other meet along a length and
    /// name no place, so <c>CollidesWith</c> is what says they touch — the reading two arcs of one circle get.
    /// </para>
    /// </remarks>
    internal static partial class ArcChain3
    {
        #region The walk over edge pairs

        /// <summary>
        /// Gathers where two runs of edges cross, naming each place once and dropping the pairs whose boxes
        /// cannot reach each other.
        /// </summary>
        private static GeoPoint3[] CrossingsOfChains(IEnumerable<GeoEdge3> first, IEnumerable<GeoEdge3> second, Tolerance tolerance)
        {
            List<GeoEdge3> others = new List<GeoEdge3>(second);
            var boxes = new GeoAabb3[others.Count];

            for (int i = 0; i < others.Count; i++)
            {
                boxes[i] = others[i].GetAabb();
            }

            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in first)
            {
                GeoAabb3 box = edge.GetAabb();

                for (int i = 0; i < others.Count; i++)
                {
                    if (!box.CollidesWith(boxes[i], tolerance))
                    {
                        continue;
                    }

                    foreach (GeoPoint3 crossing in edge.GetIntersections(others[i], tolerance))
                    {
                        Arc3.AddOnce(found, crossing, tolerance);
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether any edge of one run touches any edge of another, stopping at the first that does.
        /// </summary>
        private static bool TouchesChain(IEnumerable<GeoEdge3> first, IEnumerable<GeoEdge3> second, Tolerance tolerance)
        {
            List<GeoEdge3> others = new List<GeoEdge3>(second);
            var boxes = new GeoAabb3[others.Count];

            for (int i = 0; i < others.Count; i++)
            {
                boxes[i] = others[i].GetAabb();
            }

            foreach (GeoEdge3 edge in first)
            {
                GeoAabb3 box = edge.GetAabb();

                for (int i = 0; i < others.Count; i++)
                {
                    if (box.CollidesWith(boxes[i], tolerance) && edge.CollidesWith(others[i], tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The edges of a straight chain, read as edges with no bulge.
        /// </summary>
        private static IEnumerable<GeoEdge3> EdgesOfStraight(GeoPolyline3 polyline)
        {
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 leg = polyline.GetEdgeAt(i);

                yield return new GeoEdge3(leg.StartPoint, leg.EndPoint);
            }
        }

        #endregion
    }
}
