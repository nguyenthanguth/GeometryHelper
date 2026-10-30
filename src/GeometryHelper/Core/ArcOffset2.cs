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
        /// Gets the next step down a loop is read within where the tolerance is coarse beside it: a hundredth of the
        /// tolerance given, the angle kept.
        /// </summary>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The finer tolerance.</returns>
        internal static Tolerance Finer(Tolerance tolerance)
            => new Tolerance(tolerance.EqualPoint * 0.01, tolerance.EqualVector * 0.01, tolerance.EqualAngleRad, tolerance.EqualPlanar * 0.01);

        /// <summary>
        /// The most places a loop may cross itself at, within the tolerance given or a finer step of it, for its offset to be
        /// cut or done again within a hundredth or a ten-thousandth of the tolerance. Where crossings bunch, the moved pieces
        /// of a loop cut at them cross each other some square of their number of times: 40 diameters of a circle of radius
        /// 10, 3 003 crossings, cut within 1E-5 and grown by 0.3 take 2.2 s and 180 MB, 60 diameters, 6 903 crossings, take
        /// 21 s and 1.2 GB, and 200 diameters ran out of memory.
        /// </summary>
        internal const int MostCrossingsRetried = 2000;

        /// <summary>
        /// Offsets a run of edges to the left of the way it runs, and gives back the runs that survive.
        /// </summary>
        /// <param name="edges">The edges, each starting where the one before it ended.</param>
        /// <param name="closed">true when the last edge closes back to the first.</param>
        /// <param name="distance">How far to move it, to the left of the way it runs when positive.</param>
        /// <param name="options">How to fill a corner that opens up.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="open">How many runs of the offset of a loop were left open, which close no loop; none for a chain.</param>
        /// <returns>The runs of the offset, each closed when the input was.</returns>
        internal static List<List<GeoEdge2>> Offset(
            List<GeoEdge2> edges,
            bool closed,
            double distance,
            OffsetOptions options,
            Tolerance tolerance,
            out int open)
        {
            open = 0;

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

            return Prune(joined, mitres, edges, closed, distance, tolerance, Spans.Single(joined.Count), Spans.Single(edges.Count), null, 0, out open);
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
            Tolerance tolerance,
            bool exactTurns = false)
        {
            var result = new List<Piece>(moved.Count * 2);

            for (int i = 0; i < moved.Count; i++)
            {
                result.Add(new Piece(moved[i], false, kept[i], -1, false, spent[i]));

                if (i < moved.Count - 1)
                {
                    bool across = kept[i + 1] != kept[i] + 1;

                    moved[i + 1] = Corner(result, moved[i + 1], spent[i + 1], edges[kept[i]], edges[kept[i + 1]], kept[i] + 1, across, distance, options, mitres, tolerance, exactTurns);
                }
            }

            // The corner that closes a loop sits between the piece at the end of the run and the piece at
            // its beginning, and that first piece is already in the run, so it is the one in the run that
            // has to be put right rather than the one it was made from.
            if (closed && result.Count > 1)
            {
                int vertex = (kept[moved.Count - 1] + 1) % edges.Count;
                bool across = kept[0] != vertex;

                result[0] = result[0].With(Corner(result, result[0].Edge, result[0].Spent, edges[kept[moved.Count - 1]], edges[kept[0]], vertex, across, distance, options, mitres, tolerance, exactTurns));
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
                GeoPoint2[] crossings = CrossingsOf(before, after, tolerance);

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
            Tolerance tolerance,
            bool exactTurns)
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
            bool closes = !across && Closes(arriving, leaving, distance, tolerance, exactTurns);
            GeoPoint2[] crossings = spent ? new GeoPoint2[0] : CrossingsOf(before, after, tolerance);

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

                    // Where no cut can be made, a loop that crosses itself is rounded off about the corner, as a miter is that has
                    // none to square; the chord would run straight across a corner that turns right back, through its tip.
                    result.Add(exactTurns
                        ? new Piece(RoundJoin(from, to, corner, distance), false, -1, vertex, across)
                        : new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
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
        private static bool Closes(GeoEdge2 arriving, GeoEdge2 leaving, double distance, Tolerance tolerance, bool exactTurns)
        {
            GeoVector2 first = Heading(arriving, arriving.EndPoint);
            GeoVector2 second = Heading(leaving, leaving.StartPoint);
            double turn = first.CrossProduct(second);

            // Back along itself as near as the tolerance can tell: the two draw apart by no more than the point
            // tolerance over the longer edge, the reading Intersection2 gives parallel lines.
            double sine = Math.Abs(turn) / (first.Length * second.Length);
            double reach = Math.Max(arriving.Length, leaving.Length);

            // Turning back within the tolerance but by an angle the edges' headings tell exactly, as the two arcs of a loop that
            // crosses itself beside a vertex do, the turn is read: it is that angle, and not the way the edges bend, that says
            // which side of each the other lies on. Unless the edges cross again within the tolerance of the corner, when the
            // lens between them is too small to tell from it, and they are read as a cusp.
            bool backAlong = first.DotProduct(second) < 0.0;

            if (backAlong && ((sine * reach <= tolerance.EqualPoint && !(exactTurns && sine > 1E-10 && MeetAgain(arriving, leaving, tolerance))) || (exactTurns && TinyLens(arriving, leaving, tolerance))))
            {
                // The edge leaving and the one arriving, run backwards, set off from the corner the same way, and the
                // turn says nothing; how they bend away from that line does. Where they bend apart towards the side the
                // offset goes, it goes round the tip of the cusp; two straight edges folding back are read as closing, unless
                // they are not on one line, when the way they turn is read, as it is for a needle thinner than the tolerance:
                // one 0.0005 wide and 6 long, standing on a square of side 10 and grown by 0.3, is gone round at its tip, since the
                // pieces moved along its two sides stand 0.6 apart and never cross. Not on one line is told from the rounding of
                // the coordinates: the far end of the longer edge stands off the other's line by more than twice their spacing.
                if (!arriving.IsArc && !leaving.IsArc && sine * reach > CoordinateSpacing * Scale(arriving, leaving))
                {
                    return turn * distance > 0.0;
                }

                return (Bend(arriving) + Bend(leaving)) * distance <= 0.0;
            }

            return turn * distance > 0.0;
        }

        /// <summary>
        /// Twice the relative spacing of the doubles: how far apart the least significant digits of a coordinate lie, in
        /// units of the coordinate, to within a factor of two.
        /// </summary>
        private const double CoordinateSpacing = 4.4E-16;

        /// <summary>
        /// Gets the size the coordinates of two edges are rounded at: the largest coordinate they have, or their length where
        /// that is the larger.
        /// </summary>
        private static double Scale(GeoEdge2 arriving, GeoEdge2 leaving)
        {
            double scale = Math.Max(arriving.Length, leaving.Length);

            foreach (GeoPoint2 point in new[] { arriving.StartPoint, arriving.EndPoint, leaving.EndPoint })
            {
                scale = Math.Max(scale, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
            }

            return scale;
        }

        /// <summary>
        /// Determines whether two edges that meet at a corner meet again further along them than the tolerance.
        /// </summary>
        /// <remarks>
        /// Two arcs that leave a corner an angle of 2E-5 apart and bend the same way cross again 0.0007 from it, a lens too
        /// small to tell from the corner, which is a cusp whichever side of the other each arc lies on first. Arcs that meet
        /// again 0.0079 away, as the two at an inner vertex of a gear of 642 teeth do, make a lens that is a part of the
        /// loop, and the side each lies on is read from their headings.
        /// </remarks>
        private static bool MeetAgain(GeoEdge2 arriving, GeoEdge2 leaving, Tolerance tolerance)
        {
            GeoPoint2 corner = arriving.EndPoint;

            foreach (GeoPoint2 at in CrossingsOf(arriving, leaving, tolerance))
            {
                if (!at.IsEqualTo(corner, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two edges that meet at a corner meet again within the tolerance of it, so that the lens between
        /// them is too small to tell from the corner.
        /// </summary>
        /// <remarks>
        /// Two circles through a point meet again at its mirror image in the line of their centres, and a circle and a line at
        /// its mirror image in the line through the centre square to the line. The arcs of 8.57 and 2.57 long that leave the
        /// corner (9.524, -6.196) an angle of 2E-4 apart cross again 0.0005 from it: a lens of 1E-9 in area, which does not
        /// make the corner a part of the loop that turns the way their headings say, and is read as a cusp.
        /// </remarks>
        private static bool TinyLens(GeoEdge2 arriving, GeoEdge2 leaving, Tolerance tolerance)
        {
            if (!arriving.IsArc && !leaving.IsArc)
            {
                return false;
            }

            GeoPoint2 corner = arriving.EndPoint;
            GeoPoint2 mirrored;

            if (arriving.IsArc && leaving.IsArc)
            {
                GeoPoint2 one = arriving.ToArc().Center;
                GeoPoint2 other = leaving.ToArc().Center;

                if (one.GetDistanceSquaredTo(other) == 0.0)
                {
                    return false;
                }

                mirrored = Mirror(corner, one, other);
            }
            else
            {
                GeoEdge2 line = arriving.IsArc ? leaving : arriving;
                GeoPoint2 centre = (arriving.IsArc ? arriving : leaving).ToArc().Center;
                GeoVector2 along = line.StartPoint.GetVectorTo(line.EndPoint);

                if (along.Length == 0.0)
                {
                    return false;
                }

                mirrored = Mirror(corner, centre, centre.Add(new GeoVector2(-along.Y, along.X)));
            }

            double apart = corner.DistanceTo(mirrored);

            return apart > 0.0
                && apart <= tolerance.EqualPoint
                && mirrored.IsEqualTo(arriving.GetPointAtParameter(arriving.GetParameterAtPoint(mirrored, tolerance)), tolerance)
                && mirrored.IsEqualTo(leaving.GetPointAtParameter(leaving.GetParameterAtPoint(mirrored, tolerance)), tolerance);
        }

        /// <summary>
        /// Gets the mirror image of a point in the line through two points.
        /// </summary>
        private static GeoPoint2 Mirror(GeoPoint2 point, GeoPoint2 from, GeoPoint2 to)
        {
            GeoVector2 along = from.GetVectorTo(to);
            double reach = from.GetVectorTo(point).DotProduct(along) / along.DotProduct(along);
            GeoPoint2 foot = from.Add(along * reach);

            return new GeoPoint2(2.0 * foot.X - point.X, 2.0 * foot.Y - point.Y);
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
        /// be squared off 200 from the corner (100, 0), which the arc's moved circle, of radius 16.256, never reaches:
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

            GeoEdge2 beforeCurve = before;
            GeoEdge2 afterCurve = after;
            bool met = TryMeeting(before, after, from, to, corner, limit, tolerance, out GeoPoint2 meeting);

            // A piece cut back by the corner before it to no longer than the tolerance, or a hair past its own end, has no way
            // of its own left to run on, and runs on along the whole of the edge it was moved from: the loop 2:100 grown by 0.3
            // is mitered at its corner (3.416, -7.019), the tip 1.546 from the corner and the piece before run on 1.509 to it,
            // where that piece was cut to 0.0002, turned backwards, and the corner was rounded off.
            if (!met && (before.Length <= tolerance.EqualPoint || after.Length <= tolerance.EqualPoint))
            {
                beforeCurve = before.Length <= tolerance.EqualPoint && TryMove(arriving, distance, tolerance, out GeoEdge2 wholeBefore) ? wholeBefore : before;
                afterCurve = after.Length <= tolerance.EqualPoint && TryMove(leaving, distance, tolerance, out GeoEdge2 wholeAfter) ? wholeAfter : after;
                met = TryMeeting(beforeCurve, afterCurve, from, to, corner, limit, tolerance, out meeting);
            }

            if (met)
            {
                if (!across)
                {
                    mitres.Add(new Mitre(vertex, beforeCurve, afterCurve, meeting));
                }

                result[result.Count - 1] = result[result.Count - 1].With(StretchAlong(before, beforeCurve, meeting, true));

                return StretchAlong(after, afterCurve, meeting, false);
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
            // to it the other way round have crossed short of it, and meet where they cross: a crossing that lies short
            // of the cut along the bisector, though it may stand further than the reach from the corner to one side. One
            // beyond the cut is no miter within the limit, and the corner is left to be rounded.
            if (first.GetVectorTo(second).DotProduct(outSecond - outFirst) <= 0.0)
            {
                if (!roundTheCurves ||
                    !TryMeeting(before, after, from, to, corner, double.PositiveInfinity, tolerance, out GeoPoint2 meeting) ||
                    !IsAlong(corner, meeting, bisector, reach, tolerance))
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
        /// Determines whether a point lies out from a corner along its bisector, no further than the reach: between the
        /// corner and the line square to the bisector at the reach, within the tolerance.
        /// </summary>
        private static bool IsAlong(GeoPoint2 corner, GeoPoint2 point, GeoVector2 bisector, double reach, Tolerance tolerance)
        {
            double along = corner.GetVectorTo(point).DotProduct(bisector);

            return along >= -tolerance.EqualPoint && along <= reach + tolerance.EqualPoint;
        }

        /// <summary>
        /// Runs a piece on to a point, or back to it, along a curve: its own, or, for a piece too short to have one, the whole
        /// moved edge it was cut from, keeping the piece's other end.
        /// </summary>
        /// <param name="piece">The piece.</param>
        /// <param name="curve">The curve to run it along: the piece itself, or the moved edge it was cut from.</param>
        /// <param name="at">The point, on the curve.</param>
        /// <param name="toEnd">true to move the end of the piece, false to move its start.</param>
        /// <returns>The piece run on or back, never round more than half a turn of the curve's circle.</returns>
        internal static GeoEdge2 StretchAlong(GeoEdge2 piece, GeoEdge2 curve, GeoPoint2 at, bool toEnd)
        {
            if (curve.Equals(piece))
            {
                return CurveMeet2.Stretch(piece, at, toEnd);
            }

            GeoPoint2 kept = toEnd ? piece.StartPoint : piece.EndPoint;

            if (!curve.IsArc)
            {
                return toEnd ? new GeoEdge2(kept, at) : new GeoEdge2(at, kept);
            }

            // Round the curve's circle the way it runs, from the end kept to the point.
            GeoArc2 arc = curve.ToArc();
            double from = Math.Atan2(kept.Y - arc.Center.Y, kept.X - arc.Center.X);
            double to = Math.Atan2(at.Y - arc.Center.Y, at.X - arc.Center.X);
            double swept = toEnd ? to - from : from - to;
            double full = Math.PI * 2.0;

            swept %= full;

            if (arc.SweptAngle > 0.0 ? swept < 0.0 : swept > 0.0)
            {
                swept += arc.SweptAngle > 0.0 ? full : -full;
            }

            // A sliver turned backwards that ends a hair past the point runs back to it, and not on round nearly a whole turn:
            // run along a quarter circle of radius 10 to 0.02 past its end, from a sliver 0.05 past it, it swept 359.8 degrees.
            swept -= Math.Abs(swept) > Math.PI ? (swept > 0.0 ? full : -full) : 0.0;

            double bulge = Math.Tan(swept * 0.25);

            return toEnd ? new GeoEdge2(kept, at, bulge) : new GeoEdge2(at, kept, bulge);
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
            Tolerance tolerance,
            Spans pieceSpans,
            Spans edgeSpans,
            Region region,
            int winding,
            out int open)
        {
            // A hair of slack, so that the piece running exactly along the offset is never thrown away.
            double slack = Math.Max(tolerance.EqualPoint, Math.Abs(sideways) * 1E-6);

            List<List<GeoEdge2>> loops = Prune(offset, mitres, original, closed, sideways, tolerance, pieceSpans, edgeSpans, region, winding, slack, slack, out open);

            // A stretch of a piece that lies inside the point of another corner's miter by less than the slack is kept, and
            // a stub of it longer than the tolerance leaves the run it ends open by its own length: a slot 5.12 wide cut
            // 15 deep into a rectangle, grown by 5 with miters, fills, and its two stubs 0.0014 long leave two runs open
            // and nothing of the rectangle. A shape of one loop that left a run open is read again with only a stretch
            // inside the point of a miter by more than a thousandth of the slack thrown away, and is the answer where it
            // leaves none open.
            if (region == null && open > 0)
            {
                List<List<GeoEdge2>> again = Prune(offset, mitres, original, closed, sideways, tolerance, pieceSpans, edgeSpans, region, winding, slack, slack * 1E-3, out int stillOpen);

                if (stillOpen == 0 && again.Count >= loops.Count)
                {
                    open = 0;
                    return again;
                }
            }

            return loops;
        }

        /// <summary>
        /// Reads the offset once, with a point counted as inside the point of a miter when it lies further than a given depth
        /// inside it, and says how many runs were left open.
        /// </summary>
        private static List<List<GeoEdge2>> Prune(
            List<Piece> offset,
            List<Mitre> mitres,
            List<GeoEdge2> original,
            bool closed,
            double sideways,
            Tolerance tolerance,
            Spans pieceSpans,
            Spans edgeSpans,
            Region region,
            int winding,
            double slack,
            double depth,
            out int open)
        {
            double distance = Math.Abs(sideways);

            var shape = new Shape(original, closed, edgeSpans, region, sideways, slack, depth, offset, mitres, tolerance);
            List<List<Meet>> crossings = Crossings(offset, closed, pieceSpans, region != null, sideways, tolerance);
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

                List<KeyValuePair<GeoEdge2, bool>> before = Neighbour(parts, i, -1, closed, pieceSpans);
                List<KeyValuePair<GeoEdge2, bool>> after = Neighbour(parts, i, 1, closed, pieceSpans);

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
            var loopOf = new List<int>();
            var current = new List<GeoEdge2>();
            bool holds = false;
            int currentLoop = 0;

            for (int i = 0; i < offset.Count; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                // A run does not carry on from one loop into the next: where the offsets of two loops meet, the runs are
                // put together by their ends.
                if (pieceSpans.First[i] != currentLoop)
                {
                    if (current.Count > 0)
                    {
                        survivors.Add(current);
                        held.Add(holds);
                        loopOf.Add(currentLoop);
                        current = new List<GeoEdge2>();
                        holds = false;
                    }

                    currentLoop = pieceSpans.First[i];
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
                        loopOf.Add(currentLoop);
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
                        loopOf.Add(currentLoop);
                        current = new List<GeoEdge2>();
                        holds = false;
                    }
                }
            }

            if (current.Count > 0)
            {
                survivors.Add(current);
                held.Add(holds);
                loopOf.Add(currentLoop);
            }

            // Of loops offset together, the stretches thrown away are kept by, to close a run with where one stands the distance
            // from the shape, as the pieces kept do, to within twice the slack.
            List<GeoEdge2> spare = null;
            Func<GeoEdge2, bool> stands = null;

            if (region != null)
            {
                spare = new List<GeoEdge2>();

                for (int i = 0; i < offset.Count; i++)
                {
                    if (parts[i] == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<GeoEdge2, bool> part in parts[i])
                    {
                        if (!part.Value)
                        {
                            spare.Add(part.Key);
                        }
                    }
                }

                stands = edge => Stands(edge.GetPointAtParameter(0.25), shape, distance, 2.0 * slack)
                    && Stands(edge.GetPointAtParameter(0.5), shape, distance, 2.0 * slack)
                    && Stands(edge.GetPointAtParameter(0.75), shape, distance, 2.0 * slack);
            }

            return Group(survivors, held, loopOf, closed, original, winding, tolerance, region != null, out open, spare, stands);
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
        private static List<KeyValuePair<GeoEdge2, bool>> Neighbour(List<KeyValuePair<GeoEdge2, bool>>[] parts, int index, int step, bool closed, Spans spans)
        {
            // The loop the piece is in, which is all of them but for a shape that crosses itself.
            int first = spans.First[index];
            int length = spans.Last[index] - first + 1;

            for (int k = 1; k < length; k++)
            {
                int at = index + step * k;

                if (closed)
                {
                    at = first + ((((at - first) % length) + length) % length);
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
        /// Finds where the pieces of a run cross each other, other than where neighbours meet, which two segments do only there.
        /// </summary>
        private static List<List<Meet>> Crossings(List<Piece> offset, bool closed, Spans spans, bool alsoNeighbours, double sideways, Tolerance tolerance)
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
            // in the order they come in the run, as if every pair were tried. Neighbours come last, and only where one of the two is
            // an arc, since two segments meet in one point and neighbours meet in their corner: a second place where an arc and its
            // neighbour cross, as the run-on of a miter can cross the piece after its neighbour, is noted if no other pair has
            // noted it. A crossing noted already stays as it was found.
            var order = new BoxOrder(boxes);
            var near = new List<int>();

            for (int pass = 0; pass < (alsoNeighbours ? 1 : 2); pass++)
            {
                for (int i = 0; i < offset.Count; i++)
                {
                    if (pass == 0)
                    {
                        order.Overlapping(boxes[i], near);
                    }
                    else
                    {
                        // The piece after this one, and for the first of a loop the last.
                        near.Clear();

                        if (i + 1 < offset.Count)
                        {
                            near.Add(i + 1);
                        }

                        if (closed && i == spans.First[i] && spans.Last[i] != i + 1)
                        {
                            near.Add(spans.Last[i]);
                        }
                    }

                    foreach (int j in near)
                    {
                        if (j <= i)
                        {
                            continue;
                        }

                        bool neighbours = !alsoNeighbours && spans.First[i] == spans.First[j] && (j == i + 1 || (closed && i == spans.First[i] && j == spans.Last[i]));

                        if (neighbours != (pass == 1) || (neighbours && !offset[i].Edge.IsArc && !offset[j].Edge.IsArc))
                        {
                            continue;
                        }

                        // Two pieces whose boxes stay further apart than the tolerance meet nowhere.
                        if (!boxes[i].Overlaps(boxes[j]))
                        {
                            continue;
                        }

                        foreach (GeoPoint2 meeting in CrossingsOf(offset[i].Edge, offset[j].Edge, tolerance))
                        {
                            // Where one piece only touches the other with an end, neither crosses to a side of the other.
                            bool touches = Ends(offset[i].Edge, meeting, tolerance) || Ends(offset[j].Edge, meeting, tolerance);

                            // Of loops offset together, a piece that runs on through a place where the other ends is crossed by the
                            // line the other lies along, and has a side of it to be read.
                            bool endsI = alsoNeighbours ? Ends(offset[i].Edge, meeting, tolerance) : touches;
                            bool endsJ = alsoNeighbours ? Ends(offset[j].Edge, meeting, tolerance) : touches;

                            // A piece that the other runs on into, along it the same way, is not crossed there.
                            if (alsoNeighbours && !endsI && endsJ && RunsOnAlong(offset, spans, j, offset[i].Edge, meeting, tolerance))
                            {
                                endsI = true;
                            }

                            if (alsoNeighbours && !endsJ && endsI && RunsOnAlong(offset, spans, i, offset[j].Edge, meeting, tolerance))
                            {
                                endsJ = true;
                            }

                            GeoVector2 acrossI = endsI ? default(GeoVector2) : Heading(offset[j].Edge, meeting);
                            GeoVector2 acrossJ = endsJ ? default(GeoVector2) : Heading(offset[i].Edge, meeting);

                            // A piece that runs through the corner where the other ends is on the side of the run that turns there which
                            // its two ways out lie on, and not of the one piece alone.
                            if (alsoNeighbours && !endsI && endsJ)
                            {
                                acrossI = AcrossAtACorner(offset, spans, j, offset[i].Edge, meeting, sideways, acrossI, tolerance);
                            }

                            if (alsoNeighbours && !endsJ && endsI)
                            {
                                acrossJ = AcrossAtACorner(offset, spans, i, offset[j].Edge, meeting, sideways, acrossJ, tolerance);
                            }

                            Mark(cuts[i], offset[i].Edge, meeting, acrossI, tolerance);
                            Mark(cuts[j], offset[j].Edge, meeting, acrossJ, tolerance);
                        }
                    }
                }
            }

            return cuts;
        }

        /// <summary>
        /// Gets the way a piece that runs through a corner of the run is to be read as crossing it: the heading of a piece,
        /// with the sense of it said by which of the piece's two ways out of the corner lie inside the offset.
        /// </summary>
        /// <param name="offset">The pieces of the run.</param>
        /// <param name="spans">Where the run of each piece begins and ends.</param>
        /// <param name="ending">The piece that ends or begins at the corner.</param>
        /// <param name="through">The piece that runs through it.</param>
        /// <param name="at">The corner.</param>
        /// <param name="sideways">The distance of the offset, to the left of the way the run goes when positive.</param>
        /// <param name="otherwise">What the corner is read as when it is no corner to the run: the way the piece ending there runs.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A heading that reads, as <see cref="Parts"/> reads it, as the piece running through the corner from the side the offset
        /// stands on to the side it does not, or the other way, or as not crossing it where its two ways out lie on one side.
        /// </returns>
        /// <remarks>
        /// The inside of the offset is to the right of the run when the distance is positive. At a corner that turns towards its
        /// inside it is what lies inside both pieces, and at one that turns away it is what lies inside either. A piece that runs
        /// through the corner with one way out of it inside and the other outside crosses the run, and one with both ways out on
        /// one side of it crosses nothing, though read by the piece ending there alone it would be crossing it.
        /// </remarks>
        private static GeoVector2 AcrossAtACorner(List<Piece> offset, Spans spans, int ending, GeoEdge2 through, GeoPoint2 at, double sideways, GeoVector2 otherwise, Tolerance tolerance)
        {
            int first = spans.First[ending];
            int last = spans.Last[ending];
            bool atEnd = at.IsEqualTo(offset[ending].Edge.EndPoint, tolerance);
            int other = atEnd ? (ending == last ? first : ending + 1) : (ending == first ? last : ending - 1);

            if (other == ending || IsPoint(offset[other].Edge) || IsPoint(offset[ending].Edge))
            {
                return otherwise;
            }

            // The ways the run goes in and out by.
            GeoVector2 arriving = atEnd ? Heading(offset[ending].Edge, at) : Heading(offset[other].Edge, at);
            GeoVector2 leaving = atEnd ? Heading(offset[other].Edge, at) : Heading(offset[ending].Edge, at);
            GeoVector2 along = Heading(through, at);
            bool insideIsLeft = sideways < 0.0;
            double corner = arriving.CrossProduct(leaving);
            bool turnsInwards = insideIsLeft ? corner > 0.0 : corner < 0.0;

            bool ahead = InsideTheCorner(arriving, leaving, along, insideIsLeft, turnsInwards);
            bool behind = InsideTheCorner(arriving, leaving, along * -1.0, insideIsLeft, turnsInwards);

            if (ahead == behind)
            {
                return default(GeoVector2);
            }

            // The heading Parts reads as dropping what comes after, or what comes before.
            GeoVector2 turned = new GeoVector2(-along.Y, along.X) * Math.Sign(sideways);

            return ahead ? turned : turned * -1.0;
        }

        /// <summary>
        /// Determines whether a direction out of a corner lies inside it: on the inside of both of the two ways it is made of
        /// where it turns towards its inside, and of either where it turns away.
        /// </summary>
        private static bool InsideTheCorner(GeoVector2 arriving, GeoVector2 leaving, GeoVector2 direction, bool insideIsLeft, bool turnsInwards)
        {
            bool inside = insideIsLeft ? arriving.CrossProduct(direction) > 0.0 : arriving.CrossProduct(direction) < 0.0;
            bool insideAfter = insideIsLeft ? leaving.CrossProduct(direction) > 0.0 : leaving.CrossProduct(direction) < 0.0;

            return turnsInwards ? inside && insideAfter : inside || insideAfter;
        }

        /// <summary>
        /// Determines whether the neighbour of a piece at the place where the piece ends or begins runs along an edge the same
        /// way: the piece before it where it begins there, the piece after it where it ends.
        /// </summary>
        private static bool RunsOnAlong(List<Piece> offset, Spans spans, int index, GeoEdge2 along, GeoPoint2 at, Tolerance tolerance)
        {
            int first = spans.First[index];
            int last = spans.Last[index];
            int neighbour;

            if (at.IsEqualTo(offset[index].Edge.EndPoint, tolerance))
            {
                neighbour = index == last ? first : index + 1;
            }
            else
            {
                neighbour = index == first ? last : index - 1;
            }

            if (neighbour == index || IsPoint(offset[neighbour].Edge))
            {
                return false;
            }

            GeoVector2 one = Heading(offset[neighbour].Edge, at);
            GeoVector2 other = Heading(along, at);

            return one.DotProduct(other) > 0.0 && Math.Abs(one.CrossProduct(other)) <= 0.05 * one.Length * other.Length;
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
                if (Math.Abs(turn) <= (shape.ExactSides ? 1E-9 : tolerance.EqualAngleSin) * cuts[i].Across.Length * along.Length)
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
            return !shape.Nearer(point, distance - slack) && !shape.InMitre(point) && shape.OnTheOffsetSide(point);
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
        private static List<List<GeoEdge2>> Group(List<List<GeoEdge2>> survivors, List<bool> held, List<int> loopOf, bool closed, List<GeoEdge2> original, int winding, Tolerance tolerance, bool ofSeveralLoops, out int open, List<GeoEdge2> spare = null, Func<GeoEdge2, bool> stands = null)
        {
            open = 0;
            var groups = new List<List<GeoEdge2>>();
            var holding = new List<bool>();

            // The runs of one loop follow each other in the order they were cut in, and are put together there. Those of
            // different loops are not: where the offset of one loop meets the offset of another is a place for the ends
            // to be matched, below.
            var groupLoop = new List<int>();
            int first = 0;

            for (int i = 0; i < survivors.Count; i++)
            {
                if (i > 0 && loopOf[i] != loopOf[i - 1])
                {
                    first = groups.Count;
                }

                if (groups.Count > first && Meets(groups[groups.Count - 1], survivors[i], tolerance))
                {
                    groups[groups.Count - 1].AddRange(survivors[i]);
                    holding[holding.Count - 1] |= held[i];
                }
                else
                {
                    groups.Add(new List<GeoEdge2>(survivors[i]));
                    holding.Add(held[i]);
                    groupLoop.Add(loopOf[i]);
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
            // Of each loop, likewise: the loop's last run carries on into its first.
            for (int b = groups.Count - 1; b > 0; b--)
            {
                int a = b;

                while (a > 0 && groupLoop[a - 1] == groupLoop[b])
                {
                    a--;
                }

                if (a < b && Meets(groups[b], groups[a], tolerance))
                {
                    List<GeoEdge2> last = groups[b];
                    groups.RemoveAt(b);
                    groupLoop.RemoveAt(b);
                    last.AddRange(groups[a]);
                    groups[a] = last;
                }

                b = a;
            }

            // Where several loops were offset together, the runs of one meet the runs of another at ends that each stand the
            // distance from the shape to within the slack, and so may be twice the slack apart.
            Tolerance meeting = ofSeveralLoops
                ? new Tolerance(3.0 * tolerance.EqualPoint, tolerance.EqualVector, tolerance.EqualAngleRad, tolerance.EqualPlanar)
                : tolerance;

            if (ofSeveralLoops)
            {
                BridgeWithSpare(groups, spare, stands, meeting);
                JoinAlongEdges(groups, meeting);
            }

            bool[] joined = CloseEndToEnd(groups, meeting, null, ofSeveralLoops ? 2 : 1);

            if (ofSeveralLoops)
            {
                // A run that comes back to where it began, or to where it ends, before it is done is the loop it makes and a
                // run beside it, and the run beside it may be closed in turn.
                var carried = new List<bool>(joined);

                if (SplitAtReturns(groups, carried, meeting))
                {
                    BridgeWithSpare(groups, spare, stands, meeting);
                    JoinAlongEdges(groups, meeting);
                    joined = CloseEndToEnd(groups, meeting, carried.ToArray(), 2);
                }
            }

            var loops = new List<List<GeoEdge2>>();
            var against = new List<bool>();

            for (int i = 0; i < groups.Count; i++)
            {
                List<GeoEdge2> group = groups[i];

                // A run that never comes back to where it began encloses nothing, and is no answer.
                if (!IsClosedRun(group, meeting) || (ofSeveralLoops && group.Count < 2))
                {
                    open++;
                    continue;
                }

                // One that closes only out of order and runs against the loop it came from is a fold turned inside out,
                // or a hole the offset closed round empty space, which runs against it too.
                bool doubtful = false;

                if (joined[i])
                {
                    winding = winding != 0 ? winding : Math.Sign(SignedArea(original));
                    doubtful = Math.Sign(SignedArea(group)) != winding;
                }

                loops.Add(group);
                against.Add(doubtful);
            }

            bool[] keep = KeepHoles(loops, against, winding);
            var kept = new List<List<GeoEdge2>>(loops.Count);

            for (int i = 0; i < loops.Count; i++)
            {
                if (keep[i])
                {
                    kept.Add(loops[i]);
                }
            }

            return kept;
        }

        /// <summary>
        /// Joins a run that ends where no run begins to a run whose first edge it ends on, and one that begins where no run ends to
        /// a run whose last edge it begins on, cutting that edge there.
        /// </summary>
        /// <remarks>
        /// Two pieces of the offset can lie along one line, a hair apart, each kept where the other was thrown away: the run of
        /// one ends part-way along the first edge of the run of the other, and the loop does not close, though the two cover the
        /// line between them once. The edge is cut where the other run meets it and the part it overlaps is left to that run. The
        /// shrunk chamfer of 16.30 beside a corner cut 0.3 across, at a tolerance of 1E-4, is two runs that meet so at both ends.
        /// </remarks>
        private static void JoinAlongEdges(List<List<GeoEdge2>> groups, Tolerance tolerance)
        {
            int count = groups.Count;

            if (count < 2 || count > MostRunsToBridge)
            {
                return;
            }

            var endOpen = new bool[count];
            var startOpen = new bool[count];

            for (int a = 0; a < count; a++)
            {
                endOpen[a] = true;
                startOpen[a] = true;
            }

            for (int a = 0; a < count; a++)
            {
                GeoPoint2 end = groups[a][groups[a].Count - 1].EndPoint;

                for (int b = 0; b < count; b++)
                {
                    if (startOpen[b] && end.IsEqualTo(groups[b][0].StartPoint, tolerance))
                    {
                        endOpen[a] = false;
                        startOpen[b] = false;
                        break;
                    }
                }
            }

            for (int a = 0; a < count; a++)
            {
                if (!endOpen[a])
                {
                    continue;
                }

                GeoEdge2 arriving = groups[a][groups[a].Count - 1];

                for (int b = 0; b < count; b++)
                {
                    if (b == a || !startOpen[b] || !RunsAlong(groups[b][0], arriving, arriving.EndPoint, tolerance, out double at))
                    {
                        continue;
                    }

                    if (groups[b][0].TrySplitAtParameter(at, out GeoEdge2 overlapped, out GeoEdge2 rest, tolerance))
                    {
                        groups[b][0] = rest;
                        endOpen[a] = false;
                        startOpen[b] = false;
                        break;
                    }
                }
            }

            for (int b = 0; b < count; b++)
            {
                if (!startOpen[b])
                {
                    continue;
                }

                GeoEdge2 leaving = groups[b][0];

                for (int a = 0; a < count; a++)
                {
                    GeoEdge2 last = groups[a][groups[a].Count - 1];

                    if (a == b || !endOpen[a] || !RunsAlong(last, leaving, leaving.StartPoint, tolerance, out double at))
                    {
                        continue;
                    }

                    if (last.TrySplitAtParameter(at, out GeoEdge2 head, out GeoEdge2 overhang, tolerance))
                    {
                        groups[a][groups[a].Count - 1] = head;
                        endOpen[a] = false;
                        startOpen[b] = false;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether a point of one edge's ends lies along another edge within the tolerance, between its ends, with the
        /// two running the same way there, and says where along it.
        /// </summary>
        private static bool RunsAlong(GeoEdge2 edge, GeoEdge2 other, GeoPoint2 point, Tolerance tolerance, out double parameter)
        {
            parameter = edge.GetParameterAtPoint(point, tolerance);

            if (!(parameter > 0.0 && parameter < 1.0))
            {
                return false;
            }

            GeoPoint2 on = edge.GetPointAtParameter(parameter);

            if (!on.IsEqualTo(point, tolerance))
            {
                return false;
            }

            GeoVector2 one = Heading(other, point);
            GeoVector2 two = Heading(edge, on);

            return one.DotProduct(two) > 0.0 && Math.Abs(one.CrossProduct(two)) <= 0.05 * one.Length * two.Length;
        }

        /// <summary>
        /// Joins each run that ends where no run begins to a run that begins where none ends, by up to three stretches that were
        /// thrown away and run from the one to the other.
        /// </summary>
        /// <remarks>
        /// Where three pieces cross within a few hundredths of a millimetre, the stretches of one between its crossings can be
        /// thrown away by the way the others cross it, and the runs they lay between end apart. A stretch thrown away that stands
        /// the distance from the shape, to within twice the slack, is a part of the offset all the same, and runs from the end of
        /// one run to the start of another, or to the start of the same run, is what the loop is whole with; so are two or three
        /// of them end to end. The loop of 30.86 left of a region of 33.2 with a thin lens in it that touches the corner of a
        /// triangle, shrunk by 0.05, is two runs ending 0.0043 from each other's starts at both ends, and a loop of 186.18 is two
        /// runs 0.0076 apart through two stretches of 0.0038 each: both were lost.
        /// </remarks>
        private static void BridgeWithSpare(List<List<GeoEdge2>> groups, List<GeoEdge2> spare, Func<GeoEdge2, bool> stands, Tolerance tolerance)
        {
            int count = groups.Count;

            if (spare == null || stands == null || spare.Count == 0 || count == 0 || count > MostRunsToBridge)
            {
                return;
            }

            var endOpen = new bool[count];
            var startOpen = new bool[count];
            var verdict = new bool?[spare.Count];

            for (int a = 0; a < count; a++)
            {
                endOpen[a] = true;
                startOpen[a] = true;
            }

            for (int a = 0; a < count; a++)
            {
                GeoPoint2 end = groups[a][groups[a].Count - 1].EndPoint;

                for (int b = 0; b < count; b++)
                {
                    if (startOpen[b] && end.IsEqualTo(groups[b][0].StartPoint, tolerance))
                    {
                        endOpen[a] = false;
                        startOpen[b] = false;
                        break;
                    }
                }
            }

            for (int a = 0; a < count; a++)
            {
                if (!endOpen[a])
                {
                    continue;
                }

                var path = new List<int>();
                var best = new List<int>();
                int bestRun = -1;
                double nearest = double.MaxValue;

                Reach(groups, spare, stands, verdict, startOpen, groups[a][groups[a].Count - 1].EndPoint, 0.0, path, best, ref bestRun, ref nearest, tolerance);

                if (bestRun >= 0)
                {
                    foreach (int e in best)
                    {
                        groups[a].Add(spare[e]);
                    }

                    endOpen[a] = false;
                    startOpen[bestRun] = false;
                }
            }
        }

        /// <summary>
        /// Looks for the way from a point to the start of a run that begins where none ends, along at most three stretches thrown away:
        /// the way of fewest stretches, and of those the nearest at the ends.
        /// </summary>
        private static void Reach(List<List<GeoEdge2>> groups, List<GeoEdge2> spare, Func<GeoEdge2, bool> stands, bool?[] verdict, bool[] startOpen, GeoPoint2 from, double gaps, List<int> path, List<int> best, ref int bestRun, ref double nearest, Tolerance tolerance)
        {
            for (int e = 0; e < spare.Count; e++)
            {
                if (!spare[e].StartPoint.IsEqualTo(from, tolerance) || path.Contains(e))
                {
                    continue;
                }

                // Only a stretch that stands the distance from the shape is part of the offset.
                if (!verdict[e].HasValue)
                {
                    verdict[e] = stands(spare[e]);
                }

                if (!verdict[e].Value)
                {
                    continue;
                }

                double gap = gaps + spare[e].StartPoint.GetDistanceSquaredTo(from);

                path.Add(e);

                for (int b = 0; b < groups.Count; b++)
                {
                    if (!startOpen[b] || !spare[e].EndPoint.IsEqualTo(groups[b][0].StartPoint, tolerance))
                    {
                        continue;
                    }

                    double total = gap + spare[e].EndPoint.GetDistanceSquaredTo(groups[b][0].StartPoint);

                    if (bestRun < 0 || path.Count < best.Count || (path.Count == best.Count && total < nearest))
                    {
                        bestRun = b;
                        nearest = total;
                        best.Clear();
                        best.AddRange(path);
                    }
                }

                if (path.Count < MostStretchesToBridge && (bestRun < 0 || path.Count + 1 < best.Count))
                {
                    Reach(groups, spare, stands, verdict, startOpen, spare[e].EndPoint, gap, path, best, ref bestRun, ref nearest, tolerance);
                }

                path.RemoveAt(path.Count - 1);
            }
        }

        /// <summary>
        /// The most stretches thrown away that are put end to end to join two runs.
        /// </summary>
        private const int MostStretchesToBridge = 3;

        /// <summary>
        /// The most runs that stretches thrown away are tried between: past it the runs are not bridged, which keeps the quadratic
        /// search short.
        /// </summary>
        private const int MostRunsToBridge = 2048;

        /// <summary>
        /// Splits each run that does not close where it comes back to where it began, or to where it ends, before it is done: into
        /// the loop that makes and what is left of the run.
        /// </summary>
        /// <param name="groups">The runs; those split are changed and the rest of each is added at the end.</param>
        /// <param name="carried">For each run, whether it was carried on into another, kept in step with the runs.</param>
        /// <param name="tolerance">The tolerance runs meet within.</param>
        /// <returns>true when a run was split.</returns>
        /// <remarks>
        /// <para>
        /// Where three pieces cross within a few hundredths of a millimetre, the last of the run can be a spur: a stretch the
        /// pruning kept that runs on past the place the loop closed, and ends where nothing joins it. The loop is whole without
        /// the spur, and with it is lost. The hole of 4.31 between the offsets of two arcs and a line, grown by 0.05 with miter
        /// corners, comes back from its start to within 0.0004 of it, 0.016 before the run ends. A run that comes back to the
        /// place it began part-way along its last edge is cut there.
        /// </para>
        /// <para>
        /// A run can as well pass through one place twice: two holes that meet at a point, grown by 0.3, are one run that closes on
        /// itself at that point and goes on to close on itself again. What is left of the run once the first loop is split off
        /// is a run in its own right, and is closed in turn.
        /// </para>
        /// </remarks>
        private static bool SplitAtReturns(List<List<GeoEdge2>> groups, List<bool> carried, Tolerance tolerance)
        {
            bool split = false;

            for (int i = 0; i < groups.Count; i++)
            {
                List<GeoEdge2> run = groups[i];

                if (run.Count < 2 || IsClosedRun(run, tolerance))
                {
                    continue;
                }

                GeoPoint2 start = run[0].StartPoint;
                GeoEdge2 lastEdge = run[run.Count - 1];
                GeoPoint2 end = lastEdge.EndPoint;

                // Within its last edge, which is then cut there, or within its first.
                double atStart = lastEdge.GetParameterAtPoint(start, tolerance);

                if (lastEdge.GetPointAtParameter(atStart).IsEqualTo(start, tolerance)
                    && lastEdge.TrySplitAtParameter(atStart, out GeoEdge2 keptLast, out GeoEdge2 overhang, tolerance))
                {
                    run[run.Count - 1] = keptLast;
                    split = true;
                    continue;
                }

                GeoEdge2 firstEdge = run[0];
                double atEnd = firstEdge.GetParameterAtPoint(end, tolerance);

                if (firstEdge.GetPointAtParameter(atEnd).IsEqualTo(end, tolerance)
                    && firstEdge.TrySplitAtParameter(atEnd, out GeoEdge2 spurFirst, out GeoEdge2 keptFirst, tolerance))
                {
                    run[0] = keptFirst;
                    split = true;
                    continue;
                }

                // Or at a corner of the run.
                for (int k = 1; k < run.Count; k++)
                {
                    if (run[k - 1].EndPoint.IsEqualTo(start, tolerance) || (k < run.Count - 1 && run[k].StartPoint.IsEqualTo(end, tolerance)))
                    {
                        groups.Add(run.GetRange(k, run.Count - k));
                        carried.Add(carried[i]);
                        groups[i] = run.GetRange(0, k);
                        split = true;
                        break;
                    }
                }
            }

            return split;
        }

        /// <summary>
        /// Says which loops of an offset stay: of those running against the loop they came from that may be folds turned
        /// inside out, the holes, lying inside a loop that runs as it ran; and every other loop.
        /// </summary>
        /// <remarks>
        /// A fold turned inside out and a hole both run against the loop, so the winding cannot tell them apart. Where
        /// they lie can: a hole is space the offset closed round, inside one of its outer loops, and a fold turned
        /// inside out is outside every one. The square of side 100 with a cavity whose bottom and top bow 20 into it,
        /// grown by 11 until the bows pinch it, keeps both halves of the cavity as holes, though the second is put
        /// together out of the order it was cut in.
        /// </remarks>
        /// <param name="loops">The loops, in order.</param>
        /// <param name="against">For each loop, whether it is one of those in doubt.</param>
        /// <param name="winding">The sign of the area of the loop they came from.</param>
        /// <returns>For each loop, whether it stays.</returns>
        internal static bool[] KeepHoles(List<List<GeoEdge2>> loops, List<bool> against, int winding)
        {
            var keep = new bool[loops.Count];

            for (int i = 0; i < loops.Count; i++)
            {
                keep[i] = !against[i];
            }

            if (!against.Contains(true))
            {
                return keep;
            }

            var outer = new List<List<GeoEdge2>>(loops.Count);

            for (int i = 0; i < loops.Count; i++)
            {
                if (!against[i] && Math.Sign(SignedArea(loops[i])) == winding)
                {
                    outer.Add(loops[i]);
                }
            }

            for (int i = 0; i < loops.Count; i++)
            {
                keep[i] = keep[i] || LiesInside(loops[i], outer);
            }

            return keep;
        }

        /// <summary>
        /// Determines whether a loop lies inside one of some others, read at the middle of its longest edge: the loops of
        /// one offset do not cross, so one point of it says where the whole of it lies.
        /// </summary>
        private static bool LiesInside(List<GeoEdge2> loop, List<List<GeoEdge2>> others)
        {
            GeoEdge2 longest = loop[0];

            foreach (GeoEdge2 edge in loop)
            {
                if (edge.Length > longest.Length)
                {
                    longest = edge;
                }
            }

            GeoPoint2 point = longest.MidPoint;

            foreach (List<GeoEdge2> other in others)
            {
                if (ArcChain2.Encloses(other, point))
                {
                    return true;
                }
            }

            return false;
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
        private static bool[] CloseEndToEnd(List<List<GeoEdge2>> groups, Tolerance tolerance, bool[] before, int fewest)
        {
            int count = groups.Count;
            var ends = new GeoPoint2[count];
            var next = new int[count];
            var last = new int[count];
            var gone = new bool[count];
            var carried = new bool[count];
            var open = new List<int>(count);

            // A run is closed when it comes back to where it began, and has the edges a loop needs.
            bool Closed(int run) => (groups[run].Count >= fewest || last[run] != run) && ends[last[run]].IsEqualTo(groups[run][0].StartPoint, tolerance);

            if (before != null)
            {
                Array.Copy(before, carried, count);
            }

            for (int i = 0; i < count; i++)
            {
                ends[i] = groups[i][groups[i].Count - 1].EndPoint;
                next[i] = -1;
                last[i] = i;

                if (!Closed(i))
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
                while (!gone[i] && !Closed(i))
                {
                    GeoPoint2 end = ends[last[i]];
                    double at = alongX ? end.X : end.Y;
                    double reach = tolerance.EqualPoint * 2.0;
                    int best = -1;
                    double nearest = double.MaxValue;

                    for (int k = LowerBound(keys, at - reach); k < keys.Length && keys[k] <= at + reach; k++)
                    {
                        int j = open[k];

                        if (j == i || gone[j] || Closed(j))
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
