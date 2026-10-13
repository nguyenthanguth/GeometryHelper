using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Offsetting a chain or a loop that may curve, keeping its arcs as arcs.
    /// <para>
    /// Clipper, which resolves the straight offsets, knows only straight edges, so it is no use here.
    /// Nothing is needed from it: offsetting one piece is exact and easy — a segment moves sideways and an
    /// arc keeps its centre and changes its radius — and what is left is joining the pieces up again and
    /// throwing away the parts that fold over. Those two are what this file is. An arc whose radius runs out
    /// is carried through its centre and folds over all along, and the corners either side are joined about
    /// its own two ends.
    /// </para>
    /// <para>
    /// The test that decides what folds over is the one fact an offset cannot break: every point of a
    /// valid offset stands exactly the offset distance away from the shape it came from. Each piece is cut
    /// where it comes to that distance from another part of the shape, and the stretches standing nearer
    /// have been folded over and go; the rest of the piece stays.
    /// </para>
    /// </summary>
    internal static partial class ArcOffset2
    {
        /// <summary>
        /// One piece of the offset, and whether it came from moving an edge or was built to bridge a
        /// corner that opened up.
        /// </summary>
        /// <remarks>
        /// The difference matters when the folded parts are thrown away. A piece that was moved stands
        /// exactly the offset distance from the shape, and anything nearer has been folded over; so does a
        /// round join, and a chamfer or a square cut stands that far or further. A bridge does not: a chord tied
        /// across a corner that closes, or across one that has nothing else to join it, cuts inside that distance,
        /// so round the corner it crosses the offset leaves out only the triangle under it. A bridge left joining
        /// nothing goes.
        /// </remarks>
        private struct Piece
        {
            internal Piece(GeoEdge2 edge, bool bridge, int source, int corner, bool across)
                : this(edge, bridge, source, corner, across, false)
            {
            }

            internal Piece(GeoEdge2 edge, bool bridge, int source, int corner, bool across, bool spent)
            {
                Edge = edge;
                Bridge = bridge;
                Source = source;
                Corner = corner;
                Across = across;
                Spent = spent;
            }

            internal GeoEdge2 Edge { get; }

            internal bool Bridge { get; }

            /// <summary>
            /// Gets the index of the edge the piece was moved from, or -1 for a piece built at a corner.
            /// </summary>
            internal int Source { get; }

            /// <summary>
            /// Gets the index of the vertex a piece built at a corner was built about, or -1 for a moved piece.
            /// </summary>
            internal int Corner { get; }

            /// <summary>
            /// Gets a value indicating whether a piece built at a corner spans an edge dropped between the two pieces
            /// it joins, an arc whose radius ran out.
            /// </summary>
            internal bool Across { get; }

            /// <summary>
            /// Gets a value indicating whether the piece is an arc whose radius ran out, moved through its centre to
            /// the far side or to within the tolerance of a point there: none of it is kept.
            /// </summary>
            internal bool Spent { get; }

            internal Piece With(GeoEdge2 edge) => new Piece(edge, Bridge, Source, Corner, Across, Spent);
        }

        /// <summary>
        /// Offsets a run of edges to the left of the way it runs, and gives back the runs that survive.
        /// </summary>
        /// <param name="edges">The edges, each starting where the one before it ended.</param>
        /// <param name="closed">true when the last edge closes back to the first.</param>
        /// <param name="distance">How far to move it, to the left of the way it runs when positive.</param>
        /// <param name="options">How to fill a corner that opens up.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The runs of the offset, each closed when the input was.</returns>
        internal static List<List<GeoEdge2>> Offset(
            List<GeoEdge2> edges,
            bool closed,
            double distance,
            OffsetOptions options,
            Tolerance tolerance)
        {
            var moved = new List<GeoEdge2>(edges.Count * 2);
            var kept = new List<int>(edges.Count);
            var spent = new List<bool>(edges.Count);

            for (int i = 0; i < edges.Count; i++)
            {
                if (TryMove(edges[i], distance, tolerance, out GeoEdge2 piece))
                {
                    moved.Add(piece);
                    kept.Add(i);
                    spent.Add(false);
                }
                else if (edges[i].IsArc)
                {
                    moved.Add(Spend(edges[i], distance, tolerance, out bool through));
                    kept.Add(i);
                    spent.Add(through);
                }
            }

            if (moved.Count == 0)
            {
                return new List<List<GeoEdge2>>();
            }

            var mitres = new List<Mitre>();
            List<Piece> joined = Join(edges, kept, moved, spent, closed, distance, options, mitres, tolerance);

            return Prune(joined, mitres, edges, closed, distance, tolerance);
        }

        /// <summary>
        /// Moves one edge sideways: a segment straight across, an arc in or out about its own centre.
        /// </summary>
        /// <returns>false when the edge has nothing left of it, which is an arc whose radius runs out.</returns>
        private static bool TryMove(GeoEdge2 edge, double distance, Tolerance tolerance, out GeoEdge2 moved)
        {
            moved = edge;

            if (!edge.IsArc)
            {
                GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);

                if (!along.TryGetNormal(out GeoVector2 unit))
                {
                    return false;
                }

                var sideways = new GeoVector2(-unit.Y * distance, unit.X * distance);

                moved = new GeoEdge2(edge.StartPoint.Add(sideways), edge.EndPoint.Add(sideways));
                return true;
            }

            GeoArc2 arc = edge.ToArc();

            // The left of an arc running counter-clockwise is the inside of it, so the radius shrinks; the
            // left of one running clockwise is the outside, so it grows. The sweep does not change, which
            // is why the bulge comes through untouched.
            if (!arc.TryOffset(-Math.Sign(arc.SweptAngle) * distance, out GeoArc2 concentric, tolerance))
            {
                return false;
            }

            moved = new GeoEdge2(concentric);
            return true;
        }

        /// <summary>
        /// Moves an arc whose radius runs out the distance along its normals all the same, which carries every point of
        /// it towards the centre and, further than the radius, through it onto the circle on the far side.
        /// </summary>
        /// <param name="edge">The arc.</param>
        /// <param name="distance">How far to move it, to the left of the way it runs when positive.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="through">
        /// Set to true when the arc went through its centre, or came to within the tolerance of a point there.
        /// </param>
        /// <remarks>
        /// None of an arc moved through its centre is ever kept: a point of it stands the distance from the point of the
        /// arc it came from and nearer to every other, so the pruning throws all of it away. It is there for its two
        /// ends, each the distance from an end of the arc along the arc's own normal, so that the corner either side is
        /// joined about that end of the arc as any corner is about its vertex. Moved to within the tolerance of a point,
        /// it is that point, and goes the same way. Short of the centre and longer than the tolerance, it is an arc of a
        /// radius no bigger than the tolerance standing the distance away, and is kept as any moved piece is.
        /// </remarks>
        private static GeoEdge2 Spend(GeoEdge2 edge, double distance, Tolerance tolerance, out bool through)
        {
            GeoArc2 arc = edge.ToArc();

            // Towards the centre for an arc running counter-clockwise, as TryMove moves it.
            double share = Math.Sign(arc.SweptAngle) * distance / arc.Radius;
            GeoPoint2 start = edge.StartPoint.Add(edge.StartPoint.GetVectorTo(arc.Center) * share);
            GeoPoint2 end = edge.EndPoint.Add(edge.EndPoint.GetVectorTo(arc.Center) * share);

            if (start.IsEqualTo(end, tolerance))
            {
                through = true;
                return new GeoEdge2(start, end);
            }

            through = share >= 1.0;
            return new GeoEdge2(start, end, edge.Bulge);
        }

        /// <summary>
        /// Joins the moved pieces back into one run, trimming the corners that closed up and filling the
        /// ones that opened.
        /// </summary>
        private static List<Piece> Join(
            List<GeoEdge2> edges,
            List<int> kept,
            List<GeoEdge2> moved,
            List<bool> spent,
            bool closed,
            double distance,
            OffsetOptions options,
            List<Mitre> mitres,
            Tolerance tolerance)
        {
            var result = new List<Piece>(moved.Count * 2);

            for (int i = 0; i < moved.Count; i++)
            {
                result.Add(new Piece(moved[i], false, kept[i], -1, false, spent[i]));

                if (i < moved.Count - 1)
                {
                    bool across = kept[i + 1] != kept[i] + 1;

                    moved[i + 1] = Corner(result, moved[i + 1], spent[i + 1], edges[kept[i]], edges[kept[i + 1]], kept[i] + 1, across, distance, options, mitres, tolerance);
                }
            }

            // The corner that closes a loop sits between the piece at the end of the run and the piece at
            // its beginning, and that first piece is already in the run, so it is the one in the run that
            // has to be put right rather than the one it was made from.
            if (closed && result.Count > 1)
            {
                int vertex = (kept[moved.Count - 1] + 1) % edges.Count;
                bool across = kept[0] != vertex;

                result[0] = result[0].With(Corner(result, result[0].Edge, result[0].Spent, edges[kept[moved.Count - 1]], edges[kept[0]], vertex, across, distance, options, mitres, tolerance));
            }

            MeetAcrossSpent(result, closed, tolerance);
            return result;
        }

        /// <summary>
        /// Cuts the two pieces either side of each arc whose radius ran out back to where they cross.
        /// </summary>
        /// <remarks>
        /// The arc's own offset is folded over all along, so what is left of the offset there is where the joins about
        /// its two ends meet: the corner the arc collapses to. The rectangle 100 by 30 with its right side bowed 6 in, a
        /// radius of 21.75, grown by 40: the joins of 40 about (100, 0) and (100, 30) cross at
        /// (100 + sqrt 1375, 15) = (137.081, 15), and the offset runs round the one into the other there. Cut here, the
        /// crossing is found even where it lies within the tolerance of the spent arc's ends, which the pruning takes
        /// for the pieces' own ends and leaves uncut.
        /// </remarks>
        private static void MeetAcrossSpent(List<Piece> result, bool closed, Tolerance tolerance)
        {
            int count = result.Count;

            for (int i = 0; i < count; i++)
            {
                if (!result[i].Spent)
                {
                    continue;
                }

                int left = i - 1;
                int right = i + 1;

                if (closed)
                {
                    left = (left + count) % count;
                    right %= count;
                }
                else if (left < 0 || right >= count)
                {
                    continue;
                }

                if (left == right || result[left].Spent || result[right].Spent || IsPoint(result[left].Edge) || IsPoint(result[right].Edge))
                {
                    continue;
                }

                GeoEdge2 before = result[left].Edge;
                GeoEdge2 after = result[right].Edge;
                GeoPoint2[] crossings = before.GetIntersections(after, tolerance);

                if (crossings.Length == 0)
                {
                    continue;
                }

                GeoEdge2 spent = result[i].Edge;
                GeoPoint2 at = Nearest(crossings, spent.StartPoint.GetMiddlePoint(spent.EndPoint));

                // A crossing where either piece begins or ends would cut it back to nothing, and is the pruning's.
                if (at.IsEqualTo(before.StartPoint, tolerance) || at.IsEqualTo(after.EndPoint, tolerance))
                {
                    continue;
                }

                result[left] = result[left].With(Upto(before, at, tolerance));
                result[right] = result[right].With(From(after, at, tolerance));
            }
        }

        /// <summary>
        /// Deals with one corner of the offset: trims the two pieces back to where they cross, or puts a
        /// join in between them when they no longer reach each other.
        /// </summary>
        /// <returns>The piece that follows the corner, cut back when the corner closed up.</returns>
        /// <remarks>
        /// Only a corner that turns towards the side the offset goes closes up, and only there are the pieces cut
        /// back to where they cross: a crossing at a corner that opens lies further along them, where one folds
        /// over the other, and is the pruning's to deal with. A closing corner whose pieces do not reach each other
        /// is tied by a chord and never filled, and the pruning throws the chord away with the parts of the pieces
        /// the shape comes nearer to. The chain 10 along x and then round an arc of bulge 0.5 that leaves turning 168
        /// degrees back to the left, offset by 1 to the left: the moved segment and the moved arc never meet, and
        /// what stands 1 from the chain is the segment as far as x = 7.271 and the arc from (7.484, -1) on.
        /// </remarks>
        private static GeoEdge2 Corner(
            List<Piece> result,
            GeoEdge2 after,
            bool afterSpent,
            GeoEdge2 arriving,
            GeoEdge2 leaving,
            int vertex,
            bool across,
            double distance,
            OffsetOptions options,
            List<Mitre> mitres,
            Tolerance tolerance)
        {
            GeoEdge2 before = result[result.Count - 1].Edge;
            GeoPoint2 corner = arriving.EndPoint;

            if (before.EndPoint.IsEqualTo(after.StartPoint, tolerance))
            {
                return after;
            }

            // Where the corner closed up and the two pieces still cross, both are cut back to the crossing. Where it
            // opened, they cross only further along, unless they touch within the tolerance where they end. Which it
            // does is read off the edges themselves, since a moved piece may have been cut back to nothing.
            // Across an edge that was dropped the two edges do not meet, and their turn says nothing: the corner is
            // read as it always was, cut back wherever the pieces cross and bridged where they do not.
            // Next to an arc whose radius ran out there is nothing to cross or run on: the other piece stands the
            // distance or further from the corner all along, and the spent arc the distance or nearer, so the two touch
            // at most at the end they share.
            bool spent = afterSpent || result[result.Count - 1].Spent;
            bool closes = !across && Closes(arriving, leaving, distance, tolerance);
            GeoPoint2[] crossings = spent ? new GeoPoint2[0] : before.GetIntersections(after, tolerance);

            if (crossings.Length > 0)
            {
                GeoPoint2 at = Nearest(crossings, corner);

                if (closes || across || at.IsEqualTo(before.EndPoint, tolerance) || at.IsEqualTo(after.StartPoint, tolerance))
                {
                    result[result.Count - 1] = result[result.Count - 1].With(Upto(before, at, tolerance));
                    return From(after, at, tolerance);
                }
            }

            GeoPoint2 from = before.EndPoint;
            GeoPoint2 to = after.StartPoint;

            // A corner that closes is never filled: the pieces are only tied by a straight chord, which stands nearer
            // to the shape than the distance unless the corner hardly turns, and goes with the folded parts.
            if (closes)
            {
                result.Add(new Piece(new GeoEdge2(from, to), true, -1, -1, false));
                return after;
            }

            // Otherwise the corner opened up, and something has to bridge the gap. Next to an arc whose radius ran
            // out, that is round about the corner whatever the join: the spent arc has no curve to run on or to cut
            // across to, and what stands the distance from the corner there is the circle about it.
            if (spent)
            {
                result.Add(new Piece(RoundJoin(from, to, corner, distance), false, -1, vertex, across));
                return after;
            }

            switch (options.Join)
            {
                case OffsetJoin.Round:
                    // An arc of the offset distance about the corner stands exactly that far from it, so
                    // it is held to the same rule as everything that was moved.
                    result.Add(new Piece(RoundJoin(from, to, corner, distance), false, -1, vertex, across));
                    return after;

                case OffsetJoin.Chamfer:
                    // Across an edge that was dropped the corner is not the pieces' own, and is tied as it always was.
                    if (across)
                    {
                        result.Add(new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
                        return after;
                    }

                    // The cut touches the circle a round join would draw, square to the corner's bisector.
                    if (TrySquareJoin(result, after, arriving, leaving, from, to, corner, vertex, Math.Abs(distance), false, mitres, tolerance, out GeoEdge2 cut))
                    {
                        return cut;
                    }

                    result.Add(new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
                    return after;

                default:
                    return MiterJoin(result, after, arriving, leaving, from, to, corner, vertex, across, distance, options, mitres, tolerance);
            }
        }

        /// <summary>
        /// Determines whether the corner between two edges turns towards the side the offset goes, so that the moved
        /// pieces run into each other there rather than apart.
        /// </summary>
        /// <remarks>
        /// A corner turning right back, within the point tolerance over its edges, turns both ways at once, and is
        /// read by how the two edges bend away from the line they leave it along: towards the side the offset goes,
        /// the corner opens round the tip of the cusp; away from it, or for two straight edges, it closes, and where
        /// its pieces cross, they are cut back to the crossing. The chain along x to (100, 0), round the half circle of
        /// radius 50 back over itself to (100, 100) and along x again, offset by 10 into the U: the moved line y = 10
        /// and the moved circle of radius 60 meet at (55.28, 10), where the corner closes. The rectangle 20 by 10 whose
        /// left side is a half circle bowed in to (5, 5), grown by 2: the half circle leaves both its ends back along
        /// the sides there, bending away from them, and the offset goes round each end on a half turn of radius 2.
        /// </remarks>
        private static bool Closes(GeoEdge2 arriving, GeoEdge2 leaving, double distance, Tolerance tolerance)
        {
            GeoVector2 first = Heading(arriving, arriving.EndPoint);
            GeoVector2 second = Heading(leaving, leaving.StartPoint);
            double turn = first.CrossProduct(second);

            // Back along itself as near as the tolerance can tell: the two draw apart by no more than the point
            // tolerance over the longer edge, the reading Intersection2 gives parallel lines.
            double sine = Math.Abs(turn) / (first.Length * second.Length);
            double reach = Math.Max(arriving.Length, leaving.Length);

            if (first.DotProduct(second) < 0.0 && sine * reach <= tolerance.EqualPoint)
            {
                // The edge leaving and the one arriving, run backwards, set off from the corner the same way, and the
                // turn says nothing; how they bend away from that line does. Where they bend apart towards the side the
                // offset goes, it goes round the tip of the cusp; two straight edges folding back are read as closing.
                return (Bend(arriving) + Bend(leaving)) * distance <= 0.0;
            }

            return turn * distance > 0.0;
        }

        /// <summary>
        /// Gets how sharply an edge bends to the left of the way it runs: one over its radius for an arc running
        /// counter-clockwise, less that for one running clockwise, and nothing for a segment.
        /// </summary>
        private static double Bend(GeoEdge2 edge)
        {
            if (!edge.IsArc)
            {
                return 0.0;
            }

            GeoArc2 arc = edge.ToArc();

            return Math.Sign(arc.SweptAngle) / arc.Radius;
        }

        /// <summary>
        /// Gets the way an edge runs at a point of it.
        /// </summary>
        private static GeoVector2 Heading(GeoEdge2 edge, GeoPoint2 at)
        {
            if (!edge.IsArc)
            {
                return edge.StartPoint.GetVectorTo(edge.EndPoint);
            }

            GeoArc2 arc = edge.ToArc();
            GeoVector2 radial = arc.Center.GetVectorTo(at);

            return arc.SweptAngle > 0.0 ? new GeoVector2(-radial.Y, radial.X) : new GeoVector2(radial.Y, -radial.X);
        }

        /// <summary>
        /// Bridges an opened corner with an arc of the offset distance about the corner it came from.
        /// </summary>
        /// <remarks>
        /// Where the two ends lie on one line out from the corner, the arc about it runs from a point back to the same
        /// point, a whole turn or none, while the pieces stop apart. That is no turn to round, and the two are joined
        /// straight instead, held to the distance like the rest. Both moved pieces end the distance from the corner
        /// they share, so this happens only past an edge that was dropped, such as an arc whose radius ran out: the U
        /// from (10, 0) to the origin, round the half circle of radius 2 about (0, 2) and back to (10, 4), offset 2.5
        /// to its inside, ends the two straights at (0, 2.5) and (0, 1.5), both straight above the origin.
        /// </remarks>
        private static GeoEdge2 RoundJoin(GeoPoint2 from, GeoPoint2 to, GeoPoint2 corner, double distance)
        {
            GeoVector2 first = corner.GetVectorTo(from);
            GeoVector2 second = corner.GetVectorTo(to);

            // The short way round, which is the way that stays on the side the offset went: at a corner that opens, the
            // way the corner turns, clockwise for an offset to the left. At the tip of a cusp both ways are half a turn,
            // and only the side the offset went says which.
            bool clockwise = first.CrossProduct(second) < 0.0;

            if (clockwise != distance > 0.0 && first.DotProduct(second) < 0.0)
            {
                clockwise = !clockwise;
            }

            var arc = new GeoArc2(
                corner,
                Math.Abs(distance),
                Math.Atan2(first.Y, first.X),
                Math.Atan2(second.Y, second.X),
                clockwise);

            // Read the way GeoEdge2 reads a whole turn: within rounding of one, or of none.
            double swept = Math.Abs(arc.SweptAngle);

            if (swept <= 1E-12 || swept >= Math.PI * 2.0 - 1E-12)
            {
                return new GeoEdge2(from, to);
            }

            return new GeoEdge2(arc);
        }

        /// <summary>
        /// Bridges an opened corner by running both pieces on to where they would meet, or, when that point is
        /// further away than the miter limit allows or there is none, cuts the corner off square at the limit.
        /// </summary>
        /// <remarks>
        /// The pieces are run on along the curves behind them, so an arc reaches further round its own
        /// circle rather than being cut straight across: that is what AutoCAD's OFFSET does at a corner.
        /// A miter can run away at a sharp corner, and the limit is where it is given up on: the corner is
        /// squared off there, as <see cref="OffsetOptions.MiterLimit"/> says and the straight offsets do. Where
        /// there is no sharp corner and no square to cut, because the curves never meet and there is no limit, or
        /// an arc's circle never reaches the cut at the limit, the corner is rounded off on the circle of the
        /// distance about it, which stands exactly the distance from it, as it is beside an arc whose radius ran
        /// out. The chain 100 along x and round an arc of bulge -0.9 to (140, 60), offset by 20 to its right, would
        /// be squared off 200 from the corner (100, 0), which the arc's moved circle, of radius 16.16, never reaches:
        /// it is rounded off by the circle of 20 about the corner instead.
        /// </remarks>
        private static GeoEdge2 MiterJoin(
            List<Piece> result,
            GeoEdge2 after,
            GeoEdge2 arriving,
            GeoEdge2 leaving,
            GeoPoint2 from,
            GeoPoint2 to,
            GeoPoint2 corner,
            int vertex,
            bool across,
            double distance,
            OffsetOptions options,
            List<Mitre> mitres,
            Tolerance tolerance)
        {
            GeoEdge2 before = result[result.Count - 1].Edge;
            double limit = Math.Abs(distance) * options.MiterLimit;

            if (TryMeeting(before, after, from, to, corner, limit, tolerance, out GeoPoint2 meeting))
            {
                if (!across)
                {
                    mitres.Add(new Mitre(vertex, before, after, meeting));
                }

                result[result.Count - 1] = result[result.Count - 1].With(CurveMeet2.Stretch(before, meeting, true));

                return CurveMeet2.Stretch(after, meeting, false);
            }

            // Across an edge that was dropped the corner is not the pieces' own, and is tied straight across as it was.
            if (across)
            {
                result.Add(new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
                return after;
            }

            // Past the limit the corner is squared off there, the pieces run on as the miter would run them.
            if (!double.IsInfinity(limit) && TrySquareJoin(result, after, arriving, leaving, from, to, corner, vertex, limit, true, mitres, tolerance, out GeoEdge2 squared))
            {
                return squared;
            }

            result.Add(new Piece(RoundJoin(from, to, corner, distance), false, -1, vertex, false));
            return after;
        }

        /// <summary>
        /// Bridges an opened corner by cutting it off square: both pieces are run on to the line square to the
        /// corner's bisector a given reach from the corner, and joined along it.
        /// </summary>
        /// <param name="result">The run so far, ending with the piece before the corner.</param>
        /// <param name="after">The piece after the corner.</param>
        /// <param name="arriving">The edge of the shape arriving at the corner.</param>
        /// <param name="leaving">The edge of the shape leaving it.</param>
        /// <param name="from">Where the piece before the corner ends.</param>
        /// <param name="to">Where the piece after it starts.</param>
        /// <param name="corner">The vertex the corner was moved from.</param>
        /// <param name="vertex">The index of that vertex.</param>
        /// <param name="reach">How far out from the corner the cut lies.</param>
        /// <param name="roundTheCurves">
        /// true to run an arc on round its own circle, as a miter does; false to run every piece on along the line it
        /// leaves its end by, the tangent of the round join there.
        /// </param>
        /// <param name="mitres">The joins whose inside the pruning leaves out, which this one is added to.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="next">Set to the piece after the corner, run back to the cut.</param>
        /// <returns>
        /// false, leaving the run as it was, where a piece cannot be run on to the cut: running it on takes it away
        /// from the cut, or it is an arc whose circle does not reach the cut short of a whole turn.
        /// </returns>
        /// <remarks>
        /// <para>
        /// A chamfer is the cut at the offset distance, touching the circle a round join would draw where it crosses
        /// the bisector, between the lines that touch that circle where the two pieces end: the round join's corner
        /// drawn as three straight sides, as the straight offsets cut it. Next to a segment the line is the segment
        /// run on; next to an arc it is a short straight piece leaving the arc's end along its tangent. The square of
        /// side 10 grown by 2 with chamfers is cut at the corner (10, 0) from (8 + 2 sqrt 2, -2) to (12, 2 - 2 sqrt 2),
        /// 2 from the corner at its middle, and encloses 193.255.
        /// </para>
        /// <para>
        /// A miter past its limit is the miter cut off at the limit times the distance, as the straight offsets cut
        /// it: the pieces are run on as the miter runs them, an arc round its own circle, until they reach the cut.
        /// The triangle with a tip of 10 degrees at the origin, grown by 1, would miter 11.47 from the tip, past the
        /// default limit of 10, and is squared off 10 from it, from (-9.973, -0.743) to (-9.951, -1). Pieces that cross
        /// short of the cut, as two arcs curling towards each other can, meet where they cross.
        /// </para>
        /// </remarks>
        private static bool TrySquareJoin(
            List<Piece> result,
            GeoEdge2 after,
            GeoEdge2 arriving,
            GeoEdge2 leaving,
            GeoPoint2 from,
            GeoPoint2 to,
            GeoPoint2 corner,
            int vertex,
            double reach,
            bool roundTheCurves,
            List<Mitre> mitres,
            Tolerance tolerance,
            out GeoEdge2 next)
        {
            GeoEdge2 before = result[result.Count - 1].Edge;

            next = after;

            if (IsPoint(before) ||
                IsPoint(after) ||
                !corner.GetVectorTo(from).TryGetNormal(out GeoVector2 outFirst, tolerance) ||
                !corner.GetVectorTo(to).TryGetNormal(out GeoVector2 outSecond, tolerance) ||
                !Heading(arriving, corner).TryGetNormal(out GeoVector2 onward, tolerance) ||
                !Heading(leaving, corner).TryGetNormal(out GeoVector2 onwardAfter, tolerance))
            {
                return false;
            }

            // The bisector of the two ways out from the corner to the moved ends. Where the corner turns right back
            // they point apart, and the corner is squared off straight ahead, the way the chain was running.
            if (!(outFirst + outSecond).TryGetNormal(out GeoVector2 bisector, tolerance))
            {
                bisector = onward;
            }

            // The pieces run on the way the shape's edges run at the corner, which a piece cut back past its own end
            // by the corner before no longer does.
            if (!TryRunOn(before, true, onward, corner, bisector, reach, roundTheCurves, tolerance, out GeoPoint2 first) ||
                !TryRunOn(after, false, -onwardAfter, corner, bisector, reach, roundTheCurves, tolerance, out GeoPoint2 second))
            {
                return false;
            }

            // The cut runs from the side the first piece ends on to the side the second begins on. Pieces that come
            // to it the other way round have crossed short of it, and meet where they cross.
            if (first.GetVectorTo(second).DotProduct(outSecond - outFirst) <= 0.0)
            {
                if (!roundTheCurves || !TryMeeting(before, after, from, to, corner, double.PositiveInfinity, tolerance, out GeoPoint2 meeting))
                {
                    return false;
                }

                mitres.Add(new Mitre(vertex, before, after, meeting));
                result[result.Count - 1] = result[result.Count - 1].With(CurveMeet2.Stretch(before, meeting, true));
                next = CurveMeet2.Stretch(after, meeting, false);
                return true;
            }

            // A cut no longer than the tolerance is the corner itself: both pieces end where the first one reaches it.
            bool cut = !first.IsEqualTo(second, tolerance);

            if (!cut)
            {
                second = first;
            }

            // A segment is run on along itself; an arc run on along its tangent gets a straight piece of its own.
            bool straightFirst = !roundTheCurves && before.IsArc;
            bool straightSecond = !roundTheCurves && after.IsArc;

            mitres.Add(new Mitre(vertex, before, after, first, second, cut, straightFirst, straightSecond));

            if (straightFirst)
            {
                result.Add(new Piece(new GeoEdge2(from, first), false, -1, vertex, false));
            }
            else
            {
                result[result.Count - 1] = result[result.Count - 1].With(CurveMeet2.Stretch(before, first, true));
            }

            if (cut)
            {
                result.Add(new Piece(new GeoEdge2(first, second), false, -1, vertex, false));
            }

            if (straightSecond)
            {
                result.Add(new Piece(new GeoEdge2(second, to), false, -1, vertex, false));
                return true;
            }

            next = CurveMeet2.Stretch(after, second, false);
            return true;
        }

        /// <summary>
        /// Finds where a piece reaches the line square to a corner's bisector a given reach from it, run on past its
        /// end, or back past its start.
        /// </summary>
        /// <param name="edge">The piece.</param>
        /// <param name="atEnd">true to run it on past its end, false to run it back past its start.</param>
        /// <param name="onward">The way to run it on, a unit vector along it there.</param>
        /// <param name="corner">The corner.</param>
        /// <param name="bisector">The way out from the corner, a unit vector, that the line is square to.</param>
        /// <param name="reach">How far out from the corner the line lies.</param>
        /// <param name="roundTheCurve">true to run an arc on round its own circle, false along its tangent.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="at">Set to where the piece reaches the line.</param>
        /// <returns>
        /// false when running the piece on takes it away from the line, or when it is an arc run round its circle that
        /// does not reach the line short of a whole turn.
        /// </returns>
        private static bool TryRunOn(GeoEdge2 edge, bool atEnd, GeoVector2 onward, GeoPoint2 corner, GeoVector2 bisector, double reach, bool roundTheCurve, Tolerance tolerance, out GeoPoint2 at)
        {
            GeoPoint2 end = atEnd ? edge.EndPoint : edge.StartPoint;

            at = end;

            // How much further out along the bisector the line lies, and how fast running on gains on it.
            double gap = reach - corner.GetVectorTo(end).DotProduct(bisector);
            double gains = onward.DotProduct(bisector);

            if (gap <= 0.0)
            {
                return true;
            }

            if (gains <= 0.0)
            {
                return false;
            }

            if (!roundTheCurve || !edge.IsArc)
            {
                at = end.Add(onward.Multiply(gap / gains));
                return true;
            }

            GeoArc2 arc = edge.ToArc();
            GeoVector2 radial = arc.Center.GetVectorTo(end);
            double radius = arc.Radius;

            // Turned on by an angle t, the end moves radial (cos t - 1) + onward radius sin t, so it gains
            // radius gains sin t - (radial . bisector)(1 - cos t) on the line. That is the gap at the first root of a
            // quadratic in tan(t / 2), taken in the form that keeps its digits when the circle is nearly flat.
            double ahead = radius * gains;
            double square = ahead * ahead - gap * (2.0 * radial.DotProduct(bisector) + gap);

            if (square < 0.0)
            {
                return false;
            }

            double half = Math.Atan(gap / (ahead + Math.Sqrt(square)));
            double sine = Math.Sin(half);

            // Stretched a whole turn or more, an arc would end where it began.
            if (Math.Abs(arc.SweptAngle) + half * 2.0 >= Math.PI * 2.0 - tolerance.EqualAngleRad)
            {
                return false;
            }

            at = end.Add(radial.Multiply(-2.0 * sine * sine)).Add(onward.Multiply(radius * Math.Sin(half * 2.0)));
            return true;
        }

        /// <summary>
        /// Finds where the curves behind two pieces would meet if both were run on, and says whether that
        /// point is near enough to the corner to be worth using.
        /// </summary>
        private static bool TryMeeting(
            GeoEdge2 before,
            GeoEdge2 after,
            GeoPoint2 from,
            GeoPoint2 to,
            GeoPoint2 corner,
            double limit,
            Tolerance tolerance,
            out GeoPoint2 meeting)
        {
            meeting = default(GeoPoint2);

            // The pieces have already been moved, so the curves behind them are taken as they stand.
            List<GeoPoint2> candidates = CurveMeet2.Where(before, 0.0, after, 0.0, tolerance);

            double nearest = double.MaxValue;
            bool found = false;

            foreach (GeoPoint2 candidate in candidates)
            {
                double reach = corner.DistanceTo(candidate);

                if (reach > limit || reach >= nearest)
                {
                    continue;
                }

                // Both pieces have to grow to reach it rather than shrink away from it: a crossing behind
                // either of them belongs to the other side of the shape.
                if (candidate.GetDistanceSquaredTo(from) > 0.0 && !Grows(before, candidate, from, tolerance))
                {
                    continue;
                }

                if (candidate.GetDistanceSquaredTo(to) > 0.0 && !Grows(after.Reverse(), candidate, to, tolerance))
                {
                    continue;
                }

                nearest = reach;
                meeting = candidate;
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Determines whether running an edge on past its end reaches a point, rather than turning back on
        /// itself to get there.
        /// </summary>
        private static bool Grows(GeoEdge2 edge, GeoPoint2 at, GeoPoint2 end, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                // Along the way it was already going.
                GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);
                GeoVector2 onward = end.GetVectorTo(at);

                return along.DotProduct(onward) > 0.0;
            }

            // Round the way it was already turning, and less than a whole turn further.
            GeoArc2 arc = edge.ToArc();
            GeoEdge2 grown = CurveMeet2.Stretch(edge, at, true);

            // Run on to its own start, an arc would sweep a whole turn and end where it began.
            if (!grown.IsArc || IsPoint(grown))
            {
                return false;
            }

            double swept = grown.ToArc().SweptAngle;

            return Math.Sign(swept) == Math.Sign(arc.SweptAngle) && Math.Abs(swept) >= Math.Abs(arc.SweptAngle) - tolerance.EqualAngleRad;
        }

        /// <summary>
        /// Gets the candidate nearest a point.
        /// </summary>
        private static GeoPoint2 Nearest(GeoPoint2[] candidates, GeoPoint2 point)
        {
            GeoPoint2 best = candidates[0];
            double distance = point.GetDistanceSquaredTo(best);

            for (int i = 1; i < candidates.Length; i++)
            {
                double other = point.GetDistanceSquaredTo(candidates[i]);

                if (other < distance)
                {
                    distance = other;
                    best = candidates[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the part of an edge from its start up to a point on it.
        /// </summary>
        private static GeoEdge2 Upto(GeoEdge2 edge, GeoPoint2 at, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return new GeoEdge2(edge.StartPoint, at);
            }

            double parameter = edge.GetParameterAtPoint(at, tolerance);

            return new GeoEdge2(edge.StartPoint, at, Math.Tan(edge.ToArc().SweptAngle * parameter * 0.25));
        }

        /// <summary>
        /// Gets the part of an edge from a point on it to its end.
        /// </summary>
        private static GeoEdge2 From(GeoEdge2 edge, GeoPoint2 at, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return new GeoEdge2(at, edge.EndPoint);
            }

            double parameter = edge.GetParameterAtPoint(at, tolerance);

            return new GeoEdge2(at, edge.EndPoint, Math.Tan(edge.ToArc().SweptAngle * (1.0 - parameter) * 0.25));
        }

        /// <summary>
        /// Throws away the parts of the offset that folded over, and puts what is left back into runs.
        /// </summary>
        /// <remarks>
        /// Every point of a valid offset stands exactly the offset distance from the shape it came from. A piece is
        /// cut wherever it crosses another piece, and wherever it comes to that distance from another part of the
        /// shape: where it crosses the curve that part moves to on either side, or the circle of the distance about
        /// one of the shape's vertices. Each stretch between the cuts then stands the distance away or nearer, and
        /// the nearer ones go. One more thing marks a fold where a piece crosses a join, which may stand further
        /// than the distance: the part of a piece leaving a crossing to the side of the other piece that faces
        /// the shape is inside the offset, and goes too. The hairpin 1 wide, 10 along x, 1 down and 9 back,
        /// offset by -0.6 into its inside, keeps the first leg moved to y = -0.6 as far as x = 0.553, where it
        /// comes within 0.6 of the chain's end, and nothing else.
        /// </remarks>
        private static List<List<GeoEdge2>> Prune(
            List<Piece> offset,
            List<Mitre> mitres,
            List<GeoEdge2> original,
            bool closed,
            double sideways,
            Tolerance tolerance)
        {
            double distance = Math.Abs(sideways);

            // A hair of slack, so that the piece running exactly along the offset is never thrown away.
            double slack = Math.Max(tolerance.EqualPoint, distance * 1E-6);

            var shape = new Shape(original, closed, sideways, slack, offset, mitres, tolerance);
            List<List<Meet>> crossings = Crossings(offset, closed, tolerance);
            var parts = new List<KeyValuePair<GeoEdge2, bool>>[offset.Count];

            for (int i = 0; i < offset.Count; i++)
            {
                // An arc whose radius ran out stands nearer than the distance all along, and breaks the run it is in.
                if (offset[i].Spent)
                {
                    parts[i] = new List<KeyValuePair<GeoEdge2, bool>> { new KeyValuePair<GeoEdge2, bool>(offset[i].Edge, false) };
                }
                else if (!IsPoint(offset[i].Edge))
                {
                    // An arc cut back to a point is no piece, and nothing can be read off it.
                    parts[i] = Parts(offset[i], crossings[i], shape, sideways, slack, tolerance);
                }
            }

            // A join built across an edge that was dropped stands about a corner that is not its own, so it is read
            // as it always was: it stays only as long as the pieces it joins stay where they meet it.
            for (int i = 0; i < offset.Count; i++)
            {
                if (parts[i] == null || !offset[i].Across)
                {
                    continue;
                }

                List<KeyValuePair<GeoEdge2, bool>> before = Neighbour(parts, i, -1, closed);
                List<KeyValuePair<GeoEdge2, bool>> after = Neighbour(parts, i, 1, closed);

                if (before != null && before[before.Count - 1].Value && after != null && after[0].Value)
                {
                    continue;
                }

                for (int j = 0; j < parts[i].Count; j++)
                {
                    parts[i][j] = new KeyValuePair<GeoEdge2, bool>(parts[i][j].Key, false);
                }
            }

            DropCovered(parts, tolerance);

            var survivors = new List<List<GeoEdge2>>();
            var held = new List<bool>();
            var current = new List<GeoEdge2>();
            bool holds = false;

            for (int i = 0; i < offset.Count; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                foreach (KeyValuePair<GeoEdge2, bool> part in parts[i])
                {
                    // A join built across a dropped edge about the wrong corner can end short of the piece after it;
                    // a chain does not carry on over the gap. A loop is built from where its edges start, and closes
                    // over it as it always did.
                    if (!closed && part.Value && current.Count > 0 && !current[current.Count - 1].EndPoint.IsEqualTo(part.Key.StartPoint, tolerance))
                    {
                        survivors.Add(current);
                        held.Add(holds);
                        current = new List<GeoEdge2>();
                        holds = false;
                    }

                    if (part.Value)
                    {
                        current.Add(part.Key);
                        holds |= !offset[i].Bridge;
                    }
                    else if (current.Count > 0)
                    {
                        survivors.Add(current);
                        held.Add(holds);
                        current = new List<GeoEdge2>();
                        holds = false;
                    }
                }
            }

            if (current.Count > 0)
            {
                survivors.Add(current);
                held.Add(holds);
            }

            return Group(survivors, held, closed, original, tolerance);
        }

        /// <summary>
        /// Drops each kept part that runs along another kept part, within the tolerance from end to end: of two that
        /// run along each other end to end, the later.
        /// </summary>
        /// <remarks>
        /// Two pieces can stand the distance away along the same stretch: two edges on one line, either side of a
        /// notch, each moved and run on to its miter, or the teeth of a comb whose feet line up. The offset passes
        /// there once. Kept twice, the stretch leaves the run it belongs to ending where the next one does not begin,
        /// and a loop round it never closes. Which of the two goes does not depend on which came first: the shorter
        /// goes, and the longer carries the run on. Grown by 20 with chamfers at a tolerance of 0.1, a notch whose
        /// two outer sides lie all but on one line is crossed by the straight piece leaving one side's moved arc and
        /// by the other side's moved arc, 0.04 apart; when the shorter was kept for coming first, the loop broke open
        /// at it from four of its eight starting corners. Parts running the other way along each other are a spike,
        /// and stay.
        /// </remarks>
        private static void DropCovered(List<KeyValuePair<GeoEdge2, bool>>[] parts, Tolerance tolerance)
        {
            // Every kept part in the order of the run, and whether it is kept still.
            var carriers = new List<Carrier>();
            var boxes = new List<Box>();
            var places = new List<KeyValuePair<int, int>>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < parts[i].Count; j++)
                {
                    if (parts[i][j].Value)
                    {
                        carriers.Add(new Carrier(parts[i][j].Key));
                        boxes.Add(Box.Of(parts[i][j].Key).Grown(tolerance.EqualPoint));
                        places.Add(new KeyValuePair<int, int>(i, j));
                    }
                }
            }

            var alive = new bool[carriers.Count];
            var order = new BoxOrder(boxes);
            var near = new List<int>();

            for (int n = 0; n < carriers.Count; n++)
            {
                GeoEdge2 part = carriers[n].Edge;
                bool covered = false;

                // The parts kept before this one whose boxes overlap its own, in the order they came.
                order.Overlapping(boxes[n], near);

                foreach (int k in near)
                {
                    if (k >= n || covered)
                    {
                        break;
                    }

                    if (!alive[k] || !boxes[k].Overlaps(boxes[n]))
                    {
                        continue;
                    }

                    // A part runs along another only if its ends lie in the other's box, grown by the tolerance.
                    covered = boxes[k].Holds(part.StartPoint) && boxes[k].Holds(part.EndPoint) && Covers(carriers[k], part, tolerance);

                    // A part kept before that runs along this one, and is shorter, goes instead, whichever came first:
                    // the run it was cut from carries on where this one does.
                    GeoEdge2 earlier = carriers[k].Edge;

                    if (!covered && boxes[n].Holds(earlier.StartPoint) && boxes[n].Holds(earlier.EndPoint) && Covers(carriers[n], earlier, tolerance))
                    {
                        KeyValuePair<int, int> place = places[k];
                        parts[place.Key][place.Value] = new KeyValuePair<GeoEdge2, bool>(carriers[k].Edge, false);
                        alive[k] = false;
                    }
                }

                if (covered)
                {
                    KeyValuePair<int, int> place = places[n];
                    parts[place.Key][place.Value] = new KeyValuePair<GeoEdge2, bool>(part, false);
                    continue;
                }

                alive[n] = true;
            }
        }

        /// <summary>
        /// Determines whether one edge runs along another the same way, its ends and its middle all within the
        /// tolerance of it.
        /// </summary>
        private static bool Covers(Carrier over, GeoEdge2 part, Tolerance tolerance)
        {
            if (!Touches(over, part.StartPoint, tolerance) || !Touches(over, part.EndPoint, tolerance))
            {
                return false;
            }

            GeoPoint2 middle = part.GetPointAtParameter(0.5);
            GeoPoint2 near = over.Closest(middle, tolerance);

            return middle.IsEqualTo(near, tolerance) && Heading(over.Edge, near).DotProduct(Heading(part, middle)) > 0.0;
        }

        private static bool Touches(Carrier edge, GeoPoint2 point, Tolerance tolerance)
        {
            return point.IsEqualTo(edge.Closest(point, tolerance), tolerance);
        }

        /// <summary>
        /// Gets the parts of the nearest piece before or after one that is a piece at all, or null at the end of a
        /// chain.
        /// </summary>
        private static List<KeyValuePair<GeoEdge2, bool>> Neighbour(List<KeyValuePair<GeoEdge2, bool>>[] parts, int index, int step, bool closed)
        {
            for (int k = 1; k < parts.Length; k++)
            {
                int at = index + step * k;

                if (closed)
                {
                    at = ((at % parts.Length) + parts.Length) % parts.Length;
                }
                else if (at < 0 || at >= parts.Length)
                {
                    return null;
                }

                if (parts[at] != null)
                {
                    return parts[at];
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether an edge is an arc with its two ends in one place, which no arc can be read from.
        /// </summary>
        private static bool IsPoint(GeoEdge2 edge)
        {
            // The test GeoArc2.FromBulge refuses such an edge by.
            return edge.IsArc && edge.StartPoint.GetDistanceSquaredTo(edge.EndPoint) <= 0.0;
        }

        /// <summary>
        /// Finds where the pieces of a run cross each other, other than where neighbours meet.
        /// </summary>
        private static List<List<Meet>> Crossings(List<Piece> offset, bool closed, Tolerance tolerance)
        {
            var cuts = new List<List<Meet>>(offset.Count);
            var boxes = new Box[offset.Count];

            for (int i = 0; i < offset.Count; i++)
            {
                cuts.Add(new List<Meet>());
                // A spent arc is thrown away whole, and a piece crossing it is already nearer than the distance there.
                boxes[i] = IsPoint(offset[i].Edge) || offset[i].Spent ? Box.Empty : Box.Of(offset[i].Edge).Grown(tolerance.EqualPoint);
            }

            // Each piece is tried against the later ones whose boxes overlap its own along the axis that parts them most,
            // in the order they come in the run, as if every pair were tried.
            var order = new BoxOrder(boxes);
            var near = new List<int>();

            for (int i = 0; i < offset.Count; i++)
            {
                order.Overlapping(boxes[i], near);

                foreach (int j in near)
                {
                    if (j < i + 2 || (closed && i == 0 && j == offset.Count - 1))
                    {
                        continue;
                    }

                    // Two pieces whose boxes stay further apart than the tolerance meet nowhere.
                    if (!boxes[i].Overlaps(boxes[j]))
                    {
                        continue;
                    }

                    foreach (GeoPoint2 meeting in offset[i].Edge.GetIntersections(offset[j].Edge, tolerance))
                    {
                        // Where one piece only touches the other with an end, neither crosses to a side of the other.
                        bool touches = Ends(offset[i].Edge, meeting, tolerance) || Ends(offset[j].Edge, meeting, tolerance);

                        Mark(cuts[i], offset[i].Edge, meeting, touches ? default(GeoVector2) : Heading(offset[j].Edge, meeting), tolerance);
                        Mark(cuts[j], offset[j].Edge, meeting, touches ? default(GeoVector2) : Heading(offset[i].Edge, meeting), tolerance);
                    }
                }
            }

            return cuts;
        }

        /// <summary>
        /// Cuts one piece where it crosses another and where it comes to the offset distance from the shape, and
        /// says of each part whether it stands far enough away to be kept.
        /// </summary>
        /// <remarks>
        /// A crossing is always cut, as it always was. A place where the piece only comes to the distance is cut
        /// only where the stretches either side of it differ, so a piece standing the distance away all along
        /// comes back whole.
        /// </remarks>
        private static List<KeyValuePair<GeoEdge2, bool>> Parts(
            Piece piece,
            List<Meet> crossings,
            Shape shape,
            double sideways,
            double slack,
            Tolerance tolerance)
        {
            GeoEdge2 edge = piece.Edge;
            double distance = Math.Abs(sideways);
            var points = new List<GeoPoint2>(crossings.Count);

            // A bridge across an edge that was dropped joins pieces whose corner is not where it stands, so it is
            // read as it always was: kept, and cut only where another piece crosses it.
            bool asBefore = piece.Bridge && piece.Across;

            foreach (Meet crossing in crossings)
            {
                points.Add(crossing.Point);
            }

            if (!piece.Bridge)
            {
                shape.AddReaches(piece, points, tolerance);
            }

            var cuts = new List<Cut>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                GeoVector2 across = i < crossings.Count ? crossings[i].Across : default(GeoVector2);

                cuts.Add(new Cut(edge.GetParameterAtPoint(points[i], tolerance), points[i], i < crossings.Count, across));
            }

            cuts.Sort((one, other) =>
            {
                int byParameter = one.Parameter.CompareTo(other.Parameter);

                if (byParameter != 0)
                {
                    return byParameter;
                }

                int byX = one.Point.X.CompareTo(other.Point.X);

                return byX != 0 ? byX : one.Point.Y.CompareTo(other.Point.Y);
            });

            // Whether each stretch between the cuts stands far enough away, read at its middle. A stretch lies wholly on
            // one side of the distance, but one that runs to an end of the piece rather than to a cut can come nearer
            // towards that end than it is at its middle, by more than the slack where its middle is within it, so it
            // is read at that end as well. The distance from the shape changes no faster than the piece runs, so an
            // end that a cut was too near to make stands within the tolerance of the distance, and still stands. At a
            // tolerance of 0.1 the chamfer 0.25 long across a notch 0.28 wide, grown by 0.3, stands 0.08 too near at
            // its middle and 0.17 too near at its end.
            var stands = new bool[cuts.Count + 1];
            double from = 0.0;

            for (int i = 0; i <= cuts.Count; i++)
            {
                double to = i < cuts.Count ? cuts[i].Parameter : 1.0;

                stands[i] = asBefore || Stands(edge.GetPointAtParameter((from + to) * 0.5), shape, distance, slack);

                if (stands[i] && !asBefore && i == 0)
                {
                    stands[i] = Stands(edge.StartPoint, shape, distance, slack);
                }

                if (stands[i] && !asBefore && i == cuts.Count)
                {
                    stands[i] = Stands(edge.EndPoint, shape, distance, slack);
                }

                from = to;
            }

            // Where another piece crosses this one, the part of this one on the side of it facing the shape is
            // inside the offset. Where that piece was moved, the distance has already said so; where it is a join,
            // such as a miter run on past its edge, the part inside can stand further than the distance and only
            // this tells it.
            for (int i = 0; i < cuts.Count; i++)
            {
                if (!cuts[i].Crossing)
                {
                    continue;
                }

                GeoVector2 along = Heading(edge, cuts[i].Point);
                double turn = cuts[i].Across.CrossProduct(along) * Math.Sign(sideways);

                // Two pieces running along each other within the angle tolerance give no side.
                if (Math.Abs(turn) <= tolerance.EqualAngleSin * cuts[i].Across.Length * along.Length)
                {
                    continue;
                }

                if (turn < 0.0)
                {
                    stands[i + 1] = false;
                }
                else
                {
                    stands[i] = false;
                }
            }

            var parts = new List<KeyValuePair<GeoEdge2, bool>>(cuts.Count + 1);
            GeoEdge2 rest = edge;

            for (int i = 0; i < cuts.Count; i++)
            {
                if (!cuts[i].Crossing && stands[i] == stands[i + 1])
                {
                    continue;
                }

                double parameter = rest.GetParameterAtPoint(cuts[i].Point, tolerance);

                if (rest.TrySplitAtParameter(parameter, out GeoEdge2 before, out GeoEdge2 after, tolerance))
                {
                    parts.Add(new KeyValuePair<GeoEdge2, bool>(before, stands[i]));
                    rest = after;
                }
            }

            parts.Add(new KeyValuePair<GeoEdge2, bool>(rest, stands[cuts.Count]));
            return parts;
        }

        /// <summary>
        /// Determines whether a point of a piece stands far enough from the shape for the piece to be kept there.
        /// </summary>
        private static bool Stands(GeoPoint2 point, Shape shape, double distance, double slack)
        {
            return !shape.Nearer(point, distance - slack) && !shape.InMitre(point);
        }

        /// <summary>
        /// Notes where another piece crosses an edge, and the way that one runs there, unless it is one of the
        /// edge's ends or is already noted.
        /// </summary>
        private static void Mark(List<Meet> cuts, GeoEdge2 edge, GeoPoint2 at, GeoVector2 across, Tolerance tolerance)
        {
            if (Ends(edge, at, tolerance))
            {
                return;
            }

            foreach (Meet already in cuts)
            {
                if (already.Point.IsEqualTo(at, tolerance))
                {
                    return;
                }
            }

            cuts.Add(new Meet(at, across));
        }

        /// <summary>
        /// Determines whether a point is one of the ends of an edge.
        /// </summary>
        private static bool Ends(GeoEdge2 edge, GeoPoint2 at, Tolerance tolerance)
        {
            return at.IsEqualTo(edge.StartPoint, tolerance) || at.IsEqualTo(edge.EndPoint, tolerance);
        }

        /// <summary>
        /// Notes a place to cut an edge, unless it is one of its ends or is already noted.
        /// </summary>
        private static void Mark(List<GeoPoint2> cuts, GeoEdge2 edge, GeoPoint2 at, Tolerance tolerance)
        {
            if (Ends(edge, at, tolerance))
            {
                return;
            }

            foreach (GeoPoint2 already in cuts)
            {
                if (already.IsEqualTo(at, tolerance))
                {
                    return;
                }
            }

            cuts.Add(at);
        }

        /// <summary>
        /// A corner joined by a miter, or cut off square, and the two moved pieces it ran on, as they stood before.
        /// </summary>
        private struct Mitre
        {
            internal Mitre(int corner, GeoEdge2 before, GeoEdge2 after, GeoPoint2 meeting)
                : this(corner, before, after, meeting, meeting, false, false, false)
            {
            }

            internal Mitre(int corner, GeoEdge2 before, GeoEdge2 after, GeoPoint2 first, GeoPoint2 second, bool square, bool straightFirst, bool straightSecond)
            {
                Corner = corner;
                Before = before;
                After = after;
                First = first;
                Second = second;
                Square = square;
                StraightFirst = straightFirst;
                StraightSecond = straightSecond;
            }

            internal int Corner { get; }

            internal GeoEdge2 Before { get; }

            internal GeoEdge2 After { get; }

            /// <summary>
            /// Gets where the piece before the corner was run on to: the miter, or the start of the square cut.
            /// </summary>
            internal GeoPoint2 First { get; }

            /// <summary>
            /// Gets where the piece after the corner was run back to: the miter again, or the end of the square cut.
            /// </summary>
            internal GeoPoint2 Second { get; }

            /// <summary>
            /// Gets a value indicating whether the piece before was run on straight, along the line it leaves its end by,
            /// rather than round its own curve.
            /// </summary>
            internal bool StraightFirst { get; }

            /// <summary>
            /// Gets a value indicating whether the piece after was run back straight.
            /// </summary>
            internal bool StraightSecond { get; }

            /// <summary>
            /// Gets a value indicating whether a cut lies between the two pieces as run on: false for a miter, and for a
            /// square cut no longer than the tolerance, where both pieces end at one point.
            /// </summary>
            internal bool Square { get; }
        }

        /// <summary>
        /// Where another piece crosses a piece, and the way it runs there.
        /// </summary>
        private struct Meet
        {
            internal Meet(GeoPoint2 point, GeoVector2 across)
            {
                Point = point;
                Across = across;
            }

            internal GeoPoint2 Point { get; }

            internal GeoVector2 Across { get; }
        }

        /// <summary>
        /// A place to cut a piece: how far along it, where, and, where another piece crosses it, the way that one
        /// runs.
        /// </summary>
        private struct Cut
        {
            internal Cut(double parameter, GeoPoint2 point, bool crossing, GeoVector2 across)
            {
                Parameter = parameter;
                Point = point;
                Crossing = crossing;
                Across = across;
            }

            internal double Parameter { get; }

            internal GeoPoint2 Point { get; }

            internal bool Crossing { get; }

            internal GeoVector2 Across { get; }
        }

        /// <summary>
        /// Puts the surviving runs back together into as many shapes as they make.
        /// </summary>
        /// <remarks>
        /// The runs are still in the order they were cut from the offset, and that order is what joins them
        /// up: two runs that follow each other there are genuinely next to each other on the curve, and a
        /// gap between them is exactly where a folded part was thrown away. Matching runs up by their end
        /// points instead would step into the wrong branch wherever the offset pinches itself, and one
        /// shape would come back where two were wanted. Only the runs that order leaves open are then
        /// matched up by their ends.
        /// </remarks>
        private static List<List<GeoEdge2>> Group(List<List<GeoEdge2>> survivors, List<bool> held, bool closed, List<GeoEdge2> original, Tolerance tolerance)
        {
            var groups = new List<List<GeoEdge2>>();
            var holding = new List<bool>();

            for (int i = 0; i < survivors.Count; i++)
            {
                if (groups.Count > 0 && Meets(groups[groups.Count - 1], survivors[i], tolerance))
                {
                    groups[groups.Count - 1].AddRange(survivors[i]);
                    holding[holding.Count - 1] |= held[i];
                }
                else
                {
                    groups.Add(new List<GeoEdge2>(survivors[i]));
                    holding.Add(held[i]);
                }
            }

            if (!closed)
            {
                // A bridge left with nothing it joins is no answer.
                var chains = new List<List<GeoEdge2>>(groups.Count);

                for (int i = 0; i < groups.Count; i++)
                {
                    if (holding[i])
                    {
                        chains.Add(groups[i]);
                    }
                }

                return chains;
            }

            // The offset was cut from a loop, so the run it ends on may carry on into the one it starts
            // with: where a walk begins is not a break in it.
            if (groups.Count > 1 && Meets(groups[groups.Count - 1], groups[0], tolerance))
            {
                List<GeoEdge2> last = groups[groups.Count - 1];
                groups.RemoveAt(groups.Count - 1);
                last.AddRange(groups[0]);
                groups[0] = last;
            }

            bool[] joined = CloseEndToEnd(groups, tolerance);
            int winding = 0;

            var loops = new List<List<GeoEdge2>>();

            for (int i = 0; i < groups.Count; i++)
            {
                List<GeoEdge2> group = groups[i];

                // A run that never comes back to where it began encloses nothing, and is no answer.
                if (!IsClosedRun(group, tolerance))
                {
                    continue;
                }

                // Nor is one that closes only out of order and runs against the loop it came from: that is a fold
                // turned inside out.
                if (joined[i])
                {
                    winding = winding != 0 ? winding : Math.Sign(SignedArea(original));

                    if (Math.Sign(SignedArea(group)) != winding)
                    {
                        continue;
                    }
                }

                loops.Add(group);
            }

            return loops;
        }

        /// <summary>
        /// Carries each run of a loop that does not come back to where it began on into another such run that begins
        /// where it ends, until none does.
        /// </summary>
        /// <remarks>
        /// The order the runs were cut in joins them wherever the offset runs along the loop it came from. Where a fold
        /// is thrown away the offset can carry on into a run cut from further round, or from before, and the runs that
        /// follow each other there are not the ones that meet. Left apart, neither comes back to where it began, and the
        /// loop they make is lost. Only runs that do not close are joined, so a loop that pinches into two at a point
        /// stays two.
        /// </remarks>
        /// <returns>For each run left, whether it was carried on into another.</returns>
        private static bool[] CloseEndToEnd(List<List<GeoEdge2>> groups, Tolerance tolerance)
        {
            int count = groups.Count;
            var ends = new GeoPoint2[count];
            var next = new int[count];
            var last = new int[count];
            var gone = new bool[count];
            var carried = new bool[count];
            var open = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                ends[i] = groups[i][groups[i].Count - 1].EndPoint;
                next[i] = -1;
                last[i] = i;

                if (!ends[i].IsEqualTo(groups[i][0].StartPoint, tolerance))
                {
                    open.Add(i);
                }
            }

            if (open.Count < 2)
            {
                return carried;
            }

            // The starts of the open runs in order along the axis they spread furthest on, so that those within the
            // tolerance of an end are found in one stretch of the order.
            bool alongX = Spread(groups, open, true) >= Spread(groups, open, false);

            open.Sort((one, other) =>
            {
                GeoPoint2 a = groups[one][0].StartPoint;
                GeoPoint2 b = groups[other][0].StartPoint;
                int byAxis = alongX ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);

                if (byAxis != 0)
                {
                    return byAxis;
                }

                int byOther = alongX ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);

                return byOther != 0 ? byOther : one.CompareTo(other);
            });

            var keys = new double[open.Count];

            for (int k = 0; k < open.Count; k++)
            {
                GeoPoint2 start = groups[open[k]][0].StartPoint;
                keys[k] = alongX ? start.X : start.Y;
            }

            // Each run in turn is carried on as long as an open run begins where it now ends: the nearest, and of those
            // as near, the first. A run can only lose candidates as others are carried on, so none is visited twice.
            for (int i = 0; i < count; i++)
            {
                while (!gone[i] && !ends[last[i]].IsEqualTo(groups[i][0].StartPoint, tolerance))
                {
                    GeoPoint2 end = ends[last[i]];
                    double at = alongX ? end.X : end.Y;
                    double reach = tolerance.EqualPoint * 2.0;
                    int best = -1;
                    double nearest = double.MaxValue;

                    for (int k = LowerBound(keys, at - reach); k < keys.Length && keys[k] <= at + reach; k++)
                    {
                        int j = open[k];

                        if (j == i || gone[j] || ends[last[j]].IsEqualTo(groups[j][0].StartPoint, tolerance))
                        {
                            continue;
                        }

                        GeoPoint2 start = groups[j][0].StartPoint;

                        if (!end.IsEqualTo(start, tolerance))
                        {
                            continue;
                        }

                        double gap = end.GetDistanceSquaredTo(start);

                        if (gap < nearest || (gap == nearest && j < best))
                        {
                            nearest = gap;
                            best = j;
                        }
                    }

                    if (best < 0)
                    {
                        break;
                    }

                    next[last[i]] = best;
                    last[i] = last[best];
                    gone[best] = true;
                    carried[i] = true;
                }
            }

            var kept = new List<List<GeoEdge2>>(count);
            var flags = new List<bool>(count);

            for (int i = 0; i < count; i++)
            {
                if (gone[i])
                {
                    continue;
                }

                List<GeoEdge2> run = groups[i];

                for (int j = next[i]; j >= 0; j = next[j])
                {
                    run.AddRange(groups[j]);
                }

                kept.Add(run);
                flags.Add(carried[i]);
            }

            groups.Clear();
            groups.AddRange(kept);

            return flags.ToArray();
        }

        /// <summary>
        /// Gets how far the starts of some runs spread along one axis.
        /// </summary>
        private static double Spread(List<List<GeoEdge2>> groups, List<int> runs, bool alongX)
        {
            double low = double.MaxValue;
            double high = double.MinValue;

            foreach (int run in runs)
            {
                GeoPoint2 start = groups[run][0].StartPoint;
                double value = alongX ? start.X : start.Y;

                low = Math.Min(low, value);
                high = Math.Max(high, value);
            }

            return high - low;
        }

        /// <summary>
        /// Gets the first place in sorted values holding one no smaller than a value.
        /// </summary>
        private static int LowerBound(double[] values, double value)
        {
            int low = 0;
            int high = values.Length;

            while (low < high)
            {
                int middle = low + (high - low) / 2;

                if (values[middle] < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// Gets the area a run of edges encloses as a loop, positive where it runs counter-clockwise: the straight loop
        /// through the ends of the edges, with what each arc adds beyond its chord, as GeoPolygonArc2 measures it.
        /// </summary>
        private static double SignedArea(List<GeoEdge2> run)
        {
            GeoPoint2 reference = run[0].StartPoint;
            double twice = 0.0;
            double arcs = 0.0;

            foreach (GeoEdge2 edge in run)
            {
                GeoVector2 from = reference.GetVectorTo(edge.StartPoint);
                GeoVector2 to = reference.GetVectorTo(edge.EndPoint);

                twice += from.CrossProduct(to);

                if (edge.IsArc && !IsPoint(edge))
                {
                    GeoArc2 arc = edge.ToArc();
                    double swept = arc.SweptAngle;

                    arcs += arc.Radius * arc.Radius * 0.5 * (swept - Math.Sin(swept));
                }
            }

            return twice * 0.5 + arcs;
        }

        private static bool IsClosedRun(List<GeoEdge2> run, Tolerance tolerance)
        {
            return run[run.Count - 1].EndPoint.IsEqualTo(run[0].StartPoint, tolerance);
        }

        /// <summary>
        /// Determines whether one run carries straight on into another.
        /// </summary>
        private static bool Meets(List<GeoEdge2> before, List<GeoEdge2> after, Tolerance tolerance)
        {
            return before[before.Count - 1].EndPoint.IsEqualTo(after[0].StartPoint, tolerance);
        }
    }
}
