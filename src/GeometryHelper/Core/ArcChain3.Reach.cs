using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a curved chain in space crosses another shape, and whether it touches one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chain is a run of edges and an edge is a straight leg or a bend, so every answer here is the union of
    /// what its edges answer, with the shared corners counted once. Nothing new is worked out: the arithmetic
    /// is <see cref="Arc3"/>'s, reached through <see cref="GeoEdge3"/>, which reads each edge as whichever of
    /// the two it is.
    /// </para>
    /// <para>
    /// This is the shape a reinforcing bar is, so these are the questions a bar is asked: where it crosses a
    /// pour break, whether it hits an embed, where it leaves the concrete.
    /// </para>
    /// </remarks>
    internal static partial class ArcChain3
    {
        /// <summary>
        /// Gathers what each edge of a chain answers, naming each place once.
        /// </summary>
        private static GeoPoint3[] Crossings(IEnumerable<GeoEdge3> edges, Func<GeoEdge3, GeoPoint3[]> of, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in edges)
            {
                foreach (GeoPoint3 crossing in of(edge))
                {
                    // Two edges share a corner, so a crossing at a corner is found by both of them.
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether any edge of a chain touches a shape.
        /// </summary>
        private static bool Touches(IEnumerable<GeoEdge3> edges, Func<GeoEdge3, bool> of)
        {
            foreach (GeoEdge3 edge in edges)
            {
                if (of(edge))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
