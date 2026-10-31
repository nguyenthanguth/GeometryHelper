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
        /// <para>
        /// A loop that crosses itself is read as the straight offsets read one, even-odd: a point is inside when it lies inside
        /// an odd number of the simple loops its crossings cut it into, and that region is what is offset, its arcs kept. The
        /// loop from (-7.818, 3.764) round an arc to the origin, round another to (-8.573, 2.412) and straight back crosses
        /// itself at (-1.980, 0.198), where its two arcs meet again beside their corner, and runs round a lens there the
        /// other way: grown by 0.1 with round joins it is one loop of 5.9555 and grown by 0.5 one of 14.5114 (the straight path,
        /// on the loop cut within 1E-5, 5.9555 and 14.5114); it was none. A gear of 642 teeth, whose arcs at each inner vertex
        /// cross again 0.0079 away and make a slit there, grown by 0.5 is one loop of 31 667.81, and shrunk by 0.5 one of
        /// 28 106.67, each slit holding its disc of 0.5 out of it. The loops come back wound as the signed area of the loop
        /// says it runs, outer ones that way and holes the other. Grown with miters or chamfers, each tip of the region where two
        /// lobes of it cross is a corner of its own and is mitered or cut as any corner is: the bow tie from (-10, 2) to
        /// (1, -0.2), (1, 0.2) and (-10, -2), its back an arc of bulge -0.25, grown by 1, keeps the miter of its long lobe out
        /// to (5.099, 0), past the small lobe, and encloses 54.460; the miter was cut off at x = 2, 52.539. A loop of four
        /// corners whose first and last edges cross 1.3 degrees apart, grown by 3 with chamfers, encloses 212.058, where it lost
        /// 0.83 beside the crossing. Where the two quarters of the region at a crossing are one lobe running round a hole, as
        /// the straight path reads them, they are no tips: the square 10 by 10 whose bottom edge curls up into a loop from
        /// (6, 2) to (4, 2), grown by 3, is 253.263. A loop that does not cross itself is offset as it always was. A vertex
        /// that lies on another edge is a crossing when the edge before it comes from one side of that edge and
        /// the edge after it leaves to the other: the loop from the origin to (10, 0), up to (10, 10) by an arc of bulge 0.2,
        /// back to (5, 0) and on to (3, -6) by an arc of bulge -0.2 crosses itself at (5, 0), and grown by 0.5 with round
        /// joins is one loop of 74.484 (the straight path 74.483), shrunk by 0.5 two of 21.342 and 10.017. A vertex that
        /// touches an edge with both its own edges on one side of it, or a hair off it, does not cross it. A spike the loop
        /// runs out to and straight back from, between two straight edges whose far ends lie within the point tolerance of
        /// each other, is dropped first, as the straight offsets drop it, the two ends of its foot one corner: the square 10
        /// by 10 with a needle 0.0005 wide at its foot and 6 tall on its top, grown by 0.3 at a tolerance of 0.001, is the
        /// square grown, 112.283 with round joins, as the straight path gives it; it kept a strip 0.6 wide about the needle,
        /// 115.845.
        /// A region shrunk with miter corners can lose a small island that a miter wedge covers: the offset arcs run on round their
        /// own circles and are cut square at the miter limit times the distance, and what lies inside that wedge is not left. A loop of
        /// five vertices shrunk by 1 with the default limit of 10 has two loops where round corners give three, the
        /// third of 0.203 wholly in a wedge; with a limit of 2 it stays.
        /// </para>
        /// <para>
        /// At a tolerance coarse beside the loop, two moved pieces can cross within the tolerance of where one of them ends and
        /// are not cut there. Where that leaves the offset open, the loop is offset again within a hundredth of the tolerance,
        /// and where a run is still open within a ten-thousandth: at each step cut at its crossings wherever it crosses itself
        /// there, and as a simple loop only where it crosses itself nowhere there. An answer is taken where it leaves fewer
        /// runs open and no fewer loops, with any loop of it finer than the tolerance, as the answer within the finer step has
        /// it. At a tolerance of 0.1 the loop of twelve straight corners in a box 7.9 by 5.1, grown by 4 with round joins, is
        /// one loop of 150.317, as the straight path gives it (150.320); it was none.
        /// </para>
        /// <para>
        /// Crossings of a loop that crosses itself lying within the tolerance of each other, or linked through others that
        /// are, are one place, as long as each lies within the tolerance of the place, where the edges through it are cut: a
        /// row of forty crossings 0.09 apart is twenty places of two. A loop with two places too near along an edge to cut it
        /// at both is cut within a hundredth of the tolerance, or failing that a ten-thousandth, and offset within that: a loop
        /// of fifteen corners 1.2 across, grown by 1 at a tolerance of 0.1, is cut within 1E-5 and is one loop of 7.858. A
        /// loop that cannot be cut at any of the three is offset as a simple loop, as it always was. The loop whose three
        /// crossings lie 0.070, 0.084 and 0.101 apart, grown by 0.3 at a tolerance of 0.1, is an outline of 181.68 and three
        /// holes, as within 1E-3; it was none. A loop that crosses itself at more than 2 000 places within the tolerance is
        /// read within the tolerance only, cut where it can be and otherwise offset as a simple loop, since where its
        /// crossings bunch the work of a loop cut at them grows faster than their square: 60 diameters of a circle, 6 903
        /// crossings, cut within 1E-5 took 21 s. The star {71/35} of radius 10, 2 414 crossings, grown by 0.3 at a tolerance
        /// of 0.1 is so the one loop of 302.631 round it.
        /// </para>
        /// <para>
        /// Grown or shrunk with miters or chamfers, a loop is cut again within a hundredth of the tolerance, and then a
        /// ten-thousandth, where the tolerance does not tell a crossing from what lies beside it: within it two crossings made
        /// one place turn the blunt end of the sliver between them into a point, and a corner of the loop beside a crossing
        /// turns the tip there by its own turn, and a miter runs the error on. A step the loop cannot be cut at is passed
        /// over; one is taken where it is coarse at fewer places than the step before, until none is left, and the loop is
        /// offset within the last taken. A step coarse at as many places, as where three edges cross at one point, ends it,
        /// so a loop is cut at most twice more. A loop of the answer that is a point within the tolerance asked for is left
        /// out. The bow tie above with a corner on its first edge 0.005 past the crossing, where it turns by 1 degree, grown
        /// by 1 at a tolerance of 0.01, keeps the miter of its long lobe and encloses 54.419; the miter was left out, 52.501. A loop of four corners whose spike 2.4 degrees wide is crossed by
        /// an edge where it is 0.0057 wide, grown by 3 with miters at 0.01, is 386.351, as within 1E-4; it was 387.385.
        /// </para>
        /// <para>
        /// A loop is put together edge for edge, its corners counted within the tolerance asked, not the global one: at a
        /// millionth, the rectangle 100 by 50 grown by 0.0004 with round joins keeps its four arcs, 0.00057 across, and
        /// encloses 5 000.12, where they were dropped within the global tolerance of a thousandth.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or the options are null.</exception>
        public static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetOptions options, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (options == null) throw new ArgumentNullException(nameof(options));

            return Offset(loop, distance, options, tolerance, true);
        }

        /// <summary>
        /// Offsets a loop that may curve, saying whether the corners of its region a miter or a chamfer fills are read as the
        /// region has them where it crosses itself.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="distance">How far to move it, outward when positive.</param>
        /// <param name="options">How to fill a corner that opens up, and how far a miter may run.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="asTheRegion">
        /// true to read them as the region has them: grown, each tip where two lobes of it cross is a corner of its own, and
        /// a crossing the tolerance does not tell from a corner of the loop or another crossing beside it is read within a
        /// finer step. false to join the loops round the space between at every crossing, read within the tolerance, as the
        /// offset of a DXF polyline does, whose unlimited miter would run a tip on as far as its sides take it.
        /// </param>
        /// <returns>The offset loops.</returns>
        internal static GeoPolygonArc2[] Offset(GeoPolygonArc2 loop, double distance, OffsetOptions options, Tolerance tolerance, bool asTheRegion)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Corners filled with miters or chamfers are read as the region has them: grown, each tip of the region where two
            // lobes of it cross is a corner of its own, which its miter or its chamfer fills.
            bool filled = asTheRegion && options.Join != OffsetJoin.Round;
            bool tipsApart = filled && distance > 0.0;

            if (Math.Abs(distance) <= tolerance.EqualPoint)
            {
                return new[] { loop.Clone() };
            }

            // Outward is to the right of the way a counter-clockwise loop runs, and to the left of a
            // clockwise one, so which way a positive distance goes depends on the winding and not on the
            // caller having to know it.
            double sideways = loop.IsClockwise ? distance : -distance;
            List<GeoEdge2> edges = ArcChain2.EdgesOf(loop);

            DropSpikes(edges, tolerance);

            // A loop that crosses itself is read even-odd, as the straight offsets read one: cut at its crossings into the
            // simple loops they make, each running with the region on its left, which is then moved to its right whatever
            // way the loop ran, and the loops that come of it wound the way the loop ran.
            bool crossing = ArcOffset2.TrySplitAtCrossings(edges, tolerance, out List<List<GeoEdge2>> region, out List<List<GeoEdge2>> bounds, out int crossings, tipsApart, out int coarse);

            // The tolerance, a hundredth of it and a ten-thousandth of it: a loop it reads in finer steps where the tolerance is
            // coarse beside the shape. One crossing itself at more than 2 000 places is read within the tolerance only, since the
            // offset of a loop cut at so many grows faster than their square where they bunch.
            var ladder = new[] { tolerance, ArcOffset2.Finer(tolerance), ArcOffset2.Finer(ArcOffset2.Finer(tolerance)) };
            bool finely = tolerance.EqualPoint > 0.0 && crossings <= ArcOffset2.MostCrossingsRetried;
            int used = 0;

            // Crossings too near each other along an edge to cut it at both within the tolerance are cut within a hundredth of
            // it, or a ten-thousandth, and the loop is offset within that: the star {37/18} of radius 16.670 within a hundredth
            // of 0.1, a loop of fifteen corners in a box 1.2 across within a ten-thousandth. One that cannot be cut at any step,
            // or crosses itself at too many places to try, is offset as a simple loop, as it always was.
            while (!crossing && crossings > 0 && finely && used + 1 < ladder.Length)
            {
                used++;
                crossing = ArcOffset2.TrySplitAtCrossings(edges, ladder[used], out region, out bounds, out crossings, tipsApart, out coarse);
                finely = crossings <= ArcOffset2.MostCrossingsRetried;
                crossing = crossing && finely;
            }

            // A corner a miter or a chamfer fills is misread where the tolerance does not tell a crossing from what lies beside
            // it: two crossings made one place turn the blunt end of the sliver between them into a point, and a corner of the
            // loop beside a crossing turns the tip there by its own turn, and a miter runs the error on. The loop is cut again
            // at the next steps down, a step it cannot be cut at passed over, as long as each step taken is coarse at fewer
            // places than the one before and some place is still coarse, and offset within the last step taken; a step coarse
            // at as many places as the one before, as where three edges cross at one point, ends it.
            bool finer = false;

            for (int step = used + 1; crossing && coarse > 0 && filled && finely && step < ladder.Length; step++)
            {
                if (!ArcOffset2.TrySplitAtCrossings(edges, ladder[step], out List<List<GeoEdge2>> fineRegion, out List<List<GeoEdge2>> fineBounds, out int fineCrossings, tipsApart, out int fineCoarse)
                    || fineCrossings > ArcOffset2.MostCrossingsRetried)
                {
                    continue;
                }

                if (fineCoarse >= coarse)
                {
                    break;
                }

                used = step;
                region = fineRegion;
                bounds = fineBounds;
                coarse = fineCoarse;
                finer = true;
            }

            if (!crossing)
            {
                used = 0;
            }

            List<List<GeoEdge2>> runs = crossing
                ? ArcOffset2.Offset(bounds, region, -distance, options, ladder[used], out int open)
                : ArcOffset2.Offset(edges, true, sideways, options, tolerance, out open);

            // Two moved pieces that cross within the tolerance of where either ends are not cut there, and each runs on past
            // the crossing by up to the tolerance; at a tolerance coarse beside the shape the runs either side can be left
            // further apart than the tolerance, or a stub the slack lets stand can carry a run past the next, and the loop does
            // not close. Where the offset leaves a run open the loop is offset again at the next steps down, cut at its
            // crossings there where it crosses itself, and taken as a simple loop only where it does not: an answer is taken
            // where it leaves fewer runs open and no fewer loops, a loop of it finer than the tolerance and all.
            for (int step = used + 1; open > 0 && finely && step < ladder.Length; step++)
            {
                bool cut = ArcOffset2.TrySplitAtCrossings(edges, ladder[step], out List<List<GeoEdge2>> fineRegion, out List<List<GeoEdge2>> fineBounds, out int fineCrossings, tipsApart)
                    && fineCrossings <= ArcOffset2.MostCrossingsRetried;

                if (!cut && fineCrossings > 0)
                {
                    continue;
                }

                List<List<GeoEdge2>> again = cut
                    ? ArcOffset2.Offset(fineBounds, fineRegion, -distance, options, ladder[step], out int stillOpen)
                    : ArcOffset2.Offset(edges, true, sideways, options, ladder[step], out stillOpen);

                if (stillOpen < open && again.Count >= runs.Count)
                {
                    runs = again;
                    open = stillOpen;
                    crossing = cut;
                }
            }

            bool clockwise = crossing ? false : loop.IsClockwise;
            var built = new List<GeoPolygonArc2>(runs.Count);
            var closed = new List<List<GeoEdge2>>(runs.Count);
            var against = new List<bool>(runs.Count);

            foreach (List<GeoEdge2> run in runs)
            {
                // Read within a finer step, the answer still goes back within the tolerance asked for: a loop that is a point
                // within it, a speck the finer step leaves beside a crossing, is none.
                if (finer && IsAPoint(run, tolerance))
                {
                    continue;
                }

                if (TryLoop(run, clockwise, tolerance, out GeoPolygonArc2 one, out bool turned))
                {
                    built.Add(one);
                    closed.Add(run);
                    against.Add(turned);
                }
            }

            // A lens running against the loop is a hole where it lies inside a loop running with it.
            bool[] keep = ArcOffset2.KeepHoles(closed, against, clockwise ? -1 : 1);
            var results = new List<GeoPolygonArc2>(built.Count);

            for (int i = 0; i < built.Count; i++)
            {
                if (keep[i])
                {
                    results.Add(crossing && loop.IsClockwise ? built[i].Reverse() : built[i]);
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

            List<List<GeoEdge2>> runs = ArcOffset2.Offset(ArcChain2.EdgesOf(chain), false, distance, options, tolerance, out _);
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
        /// Counts the places a loop crosses itself, as <see cref="Offset(GeoPolygonArc2, double, OffsetOptions, Tolerance, bool)"/>
        /// finds them before it offsets the loop: its spikes dropped first, and a vertex that only touches another edge, or two
        /// edges that only graze, no crossing. The bow tie (-10, 1), (10, -1), (10, 1), (-10, -1) crosses itself once.
        /// </summary>
        internal static int CrossingsOf(GeoPolygonArc2 loop, Tolerance tolerance)
        {
            List<GeoEdge2> edges = ArcChain2.EdgesOf(loop);
            DropSpikes(edges, tolerance);
            return ArcOffset2.CountCrossings(edges, tolerance);
        }

        /// <summary>
        /// Drops each corner a loop runs out to and straight back from, between two straight edges whose far ends lie within
        /// the point tolerance of each other, and the far end of the second with it, the edge after it starting where the
        /// first began, as the straight offsets drop them.
        /// </summary>
        /// <remarks>
        /// Such a spike is narrower than the tolerance where it leaves the loop and encloses nothing at it, and the straight
        /// path reads the loop without it, the two ends of its foot one corner. Offset as drawn, the square 10 by 10 with a
        /// needle 0.0005 wide at its foot and 6 tall on its top, grown by 0.3 with round joins, kept a strip 0.6 wide about
        /// the needle, 115.85 where the straight path gives 112.28; with the needle dropped and its foot left an edge
        /// 0.0004 long, the triangle (0, 0), (10, 0), (5, 10) with such a needle on its top, grown by 0.3 with miters, had
        /// two corners there, 60.1989 where the triangle grown is 60.1795. A spike left behind by the one dropped goes too:
        /// the square with a needle 0.0006 wide at its foot that narrows to 0.0001 three up and runs on to a tip, grown by
        /// 0.3 with round joins, is the square grown, 112.283, where the straight path, which drops the tip only, keeps the
        /// rest, 114.045. A loop is left at least three edges.
        /// </remarks>
        private static void DropSpikes(List<GeoEdge2> edges, Tolerance tolerance)
        {
            for (int i = 0; i < edges.Count && edges.Count > 3;)
            {
                int next = (i + 1) % edges.Count;
                GeoEdge2 into = edges[i];
                GeoEdge2 outOf = edges[next];

                if (into.IsArc || outOf.IsArc || into.StartPoint.DistanceTo(outOf.EndPoint) > tolerance.EqualPoint)
                {
                    i++;
                    continue;
                }

                if (edges.Count > 4)
                {
                    // The foot's far end goes too: the edge after the spike starts where the spike began.
                    int after = (next + 1) % edges.Count;

                    edges[after] = new GeoEdge2(into.StartPoint, edges[after].EndPoint, edges[after].Bulge);
                    edges.RemoveAt(Math.Max(i, next));
                    edges.RemoveAt(Math.Min(i, next));
                }
                else
                {
                    edges[i] = new GeoEdge2(into.StartPoint, outOf.EndPoint);
                    edges.RemoveAt(next);
                }

                // Dropping a spike can leave another behind it.
                i = 0;
            }
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

            if (TryLoopOf(run, tolerance, out loop))
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

            if (!TryLoopOf(halved, tolerance, out loop))
            {
                return false;
            }

            turned = loop.IsClockwise != clockwise;
            return true;
        }

        /// <summary>
        /// Determines whether every corner of a run, and the middle of each of its arcs, lies within the tolerance of every
        /// other: a loop that is a point within it.
        /// </summary>
        private static bool IsAPoint(List<GeoEdge2> run, Tolerance tolerance)
        {
            var points = new List<GeoPoint2>(run.Count * 2);

            foreach (GeoEdge2 edge in run)
            {
                points.Add(edge.StartPoint);

                if (edge.IsArc)
                {
                    points.Add(edge.GetPointAtParameter(0.5));
                }
            }

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (!points[i].IsEqualTo(points[j], tolerance))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool TryLoopOf(List<GeoEdge2> run, Tolerance tolerance, out GeoPolygonArc2 loop)
        {
            loop = null;

            var vertices = new List<GeoPoint2>(run.Count);
            var bulges = new List<double>(run.Count);

            foreach (GeoEdge2 edge in run)
            {
                vertices.Add(edge.StartPoint);
                bulges.Add(edge.Bulge);
            }

            // Fewer than three corners apart within the tolerance is a sliver rather than a shape, and is no answer.
            return GeoPolygonArc2.TryFromEdges(vertices, bulges, tolerance, out loop);
        }

        #endregion
    }
}
