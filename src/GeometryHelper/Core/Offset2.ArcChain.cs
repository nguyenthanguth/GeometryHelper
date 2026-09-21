using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Offsetting the chains and loops that may curve.
    /// </summary>
    public static partial class Offset2
    {
        #region Chains that may curve

        /// <summary>
        /// Offsets a loop that may curve, keeping its arcs as arcs.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="distance">How far to move the boundary: outward when positive, inward when negative.</param>
        /// <returns>The offset loops, which may be none when the loop is eaten away, or more than one when it is pinched in two.</returns>
        /// <remarks>
        /// Nothing is flattened: an arc of radius R comes back an arc of radius R plus or minus the
        /// distance, about the same centre, and a corner that opens up is filled the way
        /// <see cref="OffsetOptions"/> says. This is the one region operation that does not go through
        /// Clipper, which is why it can keep the arcs at all.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance) => Offset(loop, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Offsets a loop that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, Tolerance tolerance) => Offset(loop, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Offsets a loop that may curve, filling opened corners a given way.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetJoin join) => Offset(loop, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Offsets a loop that may curve, filling opened corners a given way, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetJoin join, Tolerance tolerance) => Offset(loop, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Offsets a loop that may curve, with options.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the options are null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetOptions options) => Offset(loop, distance, options, Tolerance.Global);

        /// <summary>
        /// Offsets a loop that may curve, with options, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="distance">How far to move the boundary: outward when positive, inward when negative.</param>
        /// <param name="options">How to fill a corner that opens up, and how far a miter may run.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The offset loops, each running the way the one that made it ran.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the options are null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { loop.Clone() };
            }

            // Outward is to the right of the way a counter-clockwise loop runs, and to the left of a
            // clockwise one, so which way a positive distance goes depends on the winding and not on the
            // caller having to know it.
            double sideways = loop.IsClockwise ? distance : -distance;

            List<List<GeoEdge2>> runs = ArcOffset2.Offset(ArcChain2.EdgesOf(loop), true, sideways, options, tolerance);
            var results = new List<GeoPolygonArc2>(runs.Count);

            foreach (List<GeoEdge2> run in runs)
            {
                if (TryLoop(run, out GeoPolygonArc2 one))
                {
                    results.Add(one);
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Offsets a chain that may curve, keeping its arcs as arcs.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance">How far to move it, to the left of the way it runs when positive.</param>
        /// <returns>The offset chains, which may be none when nothing survives.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance) => Offset(chain, distance, OffsetOptions.Default, Tolerance.Global);

        /// <summary>
        /// Offsets a chain that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance, Tolerance tolerance) => Offset(chain, distance, OffsetOptions.Default, tolerance);

        /// <summary>
        /// Offsets a chain that may curve, filling opened corners a given way.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance, OffsetJoin join) => Offset(chain, distance, new OffsetOptions(join), Tolerance.Global);

        /// <summary>
        /// Offsets a chain that may curve, filling opened corners a given way, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance, OffsetJoin join, Tolerance tolerance) => Offset(chain, distance, new OffsetOptions(join), tolerance);

        /// <summary>
        /// Offsets a chain that may curve, with options.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the options are null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance, OffsetOptions options) => Offset(chain, distance, options, Tolerance.Global);

        /// <summary>
        /// Offsets a chain that may curve, with options, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance">How far to move it, to the left of the way it runs when positive.</param>
        /// <param name="options">How to fill a corner that opens up, and how far a miter may run.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The offset chains.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the options are null.</exception>
        public static GeoPolylineArc2[] Offset(GeoPolylineArc2 chain, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { chain.Clone() };
            }

            List<List<GeoEdge2>> runs = ArcOffset2.Offset(ArcChain2.EdgesOf(chain), false, distance, options, tolerance);
            var results = new List<GeoPolylineArc2>(runs.Count);

            foreach (List<GeoEdge2> run in runs)
            {
                if (run.Count > 0)
                {
                    results.Add(new GeoPolylineArc2(run));
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Builds a loop from a run of edges that comes back to where it began, dropping the closing edge
        /// because a loop closes itself.
        /// </summary>
        private static bool TryLoop(List<GeoEdge2> run, out GeoPolygonArc2 loop)
        {
            loop = null;

            var vertices = new List<GeoPoint2>(run.Count);
            var bulges = new List<double>(run.Count);

            foreach (GeoEdge2 edge in run)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            try
            {
                loop = new GeoPolygonArc2(vertices, bulges);
                return true;
            }
            catch (ArgumentException)
            {
                // Fewer than three distinct vertices is a sliver rather than a shape, and is no answer.
                return false;
            }
        }

        #endregion
    }
}
