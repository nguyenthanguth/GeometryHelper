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
        /// <returns>
        /// The offset loops, which may be none when the loop is eaten away, or more than one when it is pinched in two.
        /// Outer loops run the way the loop ran; where a growing loop closes a gap around empty space, that space comes
        /// back as a hole wound the other way, so that signed areas add up, as the straight offsets give it, whether the
        /// hole is a lens of two arcs or one of two a pinch leaves: the square of side 100 with a lens cut into it, tips
        /// (-20, 0) and a mouth 1 wide, sides of bulge -0.5, grown by 3, keeps the lens of radius 22.002 as a hole of
        /// 327.842.
        /// </returns>
        /// <remarks>
        /// An arc whose radius runs out has no offset left, and the loop goes round its two ends instead, on the circle
        /// of the distance about each, whatever the join: that is what stands the distance from the shape there. The
        /// rectangle 100 by 30 with its right side bowed 6 in, a radius of 21.75, grown by 40 with round joins: the
        /// circles of 40 about (100, 0) and (100, 30) meet at (137.081, 15), and the loop encloses 18 397.80, the same
        /// whichever corner it starts from and whichever way it runs. A loop whose pieces meet out of the order they
        /// were cut in is put together where they meet, and a lens left between two arcs comes back as a loop of three
        /// corners.
        /// <para>
        /// A corner that opens is filled as the straight offsets fill it. A chamfer is cut square to the corner's
        /// bisector at the distance, touching the arc a round join would draw: the square of side 10 grown by 2 is cut
        /// at its corner (10, 0) from (8 + 2 sqrt 2, -2) to (12, 2 - 2 sqrt 2), and encloses 193.255. A miter that would
        /// reach further than <see cref="OffsetOptions.MiterLimit"/> times the distance is squared off there, its pieces
        /// run on as the miter runs them: the triangle with a tip of 10 degrees, grown by 1, is squared off 10 from the
        /// tip, from (-9.973, -0.743) to (-9.951, -1). Two straight edges a fraction of a degree off straight are mitered
        /// like any others. Where there is neither a miter nor a square to cut, because an arc and the piece next to it
        /// never meet and the miter has no limit, or an arc's circle never reaches the cut, the corner is rounded off at
        /// the distance.
        /// </para>
        /// </remarks>
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
            var built = new List<GeoPolygonArc2>(runs.Count);
            var closed = new List<List<GeoEdge2>>(runs.Count);
            var against = new List<bool>(runs.Count);

            foreach (List<GeoEdge2> run in runs)
            {
                if (TryLoop(run, loop.IsClockwise, tolerance, out GeoPolygonArc2 one, out bool turned))
                {
                    built.Add(one);
                    closed.Add(run);
                    against.Add(turned);
                }
            }

            // A lens running against the loop is a hole where it lies inside a loop running with it.
            bool[] keep = ArcOffset2.KeepHoles(closed, against, loop.IsClockwise ? -1 : 1);
            var results = new List<GeoPolygonArc2>(built.Count);

            for (int i = 0; i < built.Count; i++)
            {
                if (keep[i])
                {
                    results.Add(built[i]);
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
        /// <param name="tolerance">The tolerance, which also says where two pieces of the offset meet.</param>
        /// <returns>The offset chains, which may be none when nothing survives.</returns>
        /// <remarks>
        /// The pieces are joined within the tolerance given, and the chains are put together within it too, not within
        /// the global one: the chain from the origin to (-5, 0) and round an arc of bulge -0.5 to (-7, 5), offset by 0.3
        /// with an unlimited miter at a tolerance of 0.1, is one chain from (0, -0.3) to (-7.256, 5.156). A curve that
        /// cannot stand the distance away anywhere gives nothing: the U from (10, 0) to the origin, round the half
        /// circle of radius 2 about (0, 2) and back to (10, 4), offset by -2.5 to its inside, which is 4 wide. A piece
        /// is cut where another part of the chain comes nearer than the distance, and what still stands the distance
        /// away stays: the hairpin 1 wide, 10 along x, 1 down and 9 back, offset by -0.6 into its inside, keeps the
        /// start of its first leg, from (0, -0.6) to (0.553, -0.6), short of the circle of 0.6 about the chain's end.
        /// An arc whose radius runs out has no offset left, and the chain goes round its two ends instead, on the circle
        /// of the distance about each, whatever the join: the chain 10 along x, a half turn of radius 5 below the axis
        /// to (20, 0) and 10 more, offset by 10 upwards, runs along y = 10 to (10, 10), round the circle of 10 about
        /// (10, 0) to (15, 8.660), round the circle about (20, 0) to (20, 10) and on to (30, 10), 30.472 long.
        /// <para>
        /// A corner that opens is filled as the straight offsets fill it: a chamfer square to the corner's bisector at
        /// the distance, and a miter past <see cref="OffsetOptions.MiterLimit"/> squared off at the limit. The chain
        /// (0, 0), (10, 0), (10, 10) offset by -2 with a chamfer runs along y = -2 to (8 + 2 sqrt 2, -2), across to
        /// (12, 2 - 2 sqrt 2) and on to (12, 10). Where there is neither a miter nor a square to cut, the corner is
        /// rounded off at the distance.
        /// </para>
        /// </remarks>
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
                    // The pieces were joined within the tolerance passed, so they are put together within it.
                    results.Add(GeoPolylineArc2.FromEdges(run, tolerance));
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Builds a loop from a run of edges that comes back to where it began, dropping the closing edge
        /// because a loop closes itself.
        /// </summary>
        /// <remarks>
        /// Two arcs between the same two points enclose a lens, which is a shape, though a loop needs three corners: the
        /// longest arc is halved to give it one. A lens running against the loop it came from is a fold turned inside
        /// out, or a hole, which <paramref name="turned"/> says for the caller to tell apart.
        /// </remarks>
        private static bool TryLoop(List<GeoEdge2> run, bool clockwise, Tolerance tolerance, out GeoPolygonArc2 loop, out bool turned)
        {
            turned = false;

            if (TryLoopOf(run, out loop))
            {
                return true;
            }

            int longest = -1;

            for (int i = 0; i < run.Count; i++)
            {
                if (run[i].IsArc && (longest < 0 || run[i].Length > run[longest].Length))
                {
                    longest = i;
                }
            }

            if (longest < 0 || !run[longest].TrySplitAtParameter(0.5, out GeoEdge2 first, out GeoEdge2 second, tolerance))
            {
                return false;
            }

            var halved = new List<GeoEdge2>(run);
            halved[longest] = first;
            halved.Insert(longest + 1, second);

            if (!TryLoopOf(halved, out loop))
            {
                return false;
            }

            turned = loop.IsClockwise != clockwise;
            return true;
        }

        private static bool TryLoopOf(List<GeoEdge2> run, out GeoPolygonArc2 loop)
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
                loop = GeoPolygonArc2.FromEdges(vertices, bulges);
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
