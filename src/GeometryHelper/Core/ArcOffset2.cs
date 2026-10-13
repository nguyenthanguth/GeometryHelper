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
    /// throwing away the parts that fold over. Those two are what this file is.
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
        /// exactly the offset distance from the shape, and anything nearer has been folded over. A bridge
        /// does not: a chamfer across a corner deliberately cuts inside that distance, and is no less
        /// wanted for it, so round the corner it crosses the offset leaves out only the triangle under it.
        /// A bridge left joining nothing goes.
        /// </remarks>
        private struct Piece
        {
            internal Piece(GeoEdge2 edge, bool bridge, int source, int corner, bool across)
            {
                Edge = edge;
                Bridge = bridge;
                Source = source;
                Corner = corner;
                Across = across;
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

            internal Piece With(GeoEdge2 edge) => new Piece(edge, Bridge, Source, Corner, Across);
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

            for (int i = 0; i < edges.Count; i++)
            {
                if (TryMove(edges[i], distance, tolerance, out GeoEdge2 piece))
                {
                    moved.Add(piece);
                    kept.Add(i);
                }
            }

            if (moved.Count == 0)
            {
                return new List<List<GeoEdge2>>();
            }

            var mitres = new List<Mitre>();
            List<Piece> joined = Join(edges, kept, moved, closed, distance, options, mitres, tolerance);

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
        /// Joins the moved pieces back into one run, trimming the corners that closed up and filling the
        /// ones that opened.
        /// </summary>
        private static List<Piece> Join(
            List<GeoEdge2> edges,
            List<int> kept,
            List<GeoEdge2> moved,
            bool closed,
            double distance,
            OffsetOptions options,
            List<Mitre> mitres,
            Tolerance tolerance)
        {
            var result = new List<Piece>(moved.Count * 2);

            for (int i = 0; i < moved.Count; i++)
            {
                result.Add(new Piece(moved[i], false, kept[i], -1, false));

                if (i < moved.Count - 1)
                {
                    bool across = kept[i + 1] != kept[i] + 1;

                    moved[i + 1] = Corner(result, moved[i + 1], edges[kept[i]], edges[kept[i + 1]], kept[i] + 1, across, distance, options, mitres, tolerance);
                }
            }

            // The corner that closes a loop sits between the piece at the end of the run and the piece at
            // its beginning, and that first piece is already in the run, so it is the one in the run that
            // has to be put right rather than the one it was made from.
            if (closed && result.Count > 1)
            {
                int vertex = (kept[moved.Count - 1] + 1) % edges.Count;
                bool across = kept[0] != vertex;

                result[0] = result[0].With(Corner(result, result[0].Edge, edges[kept[moved.Count - 1]], edges[kept[0]], vertex, across, distance, options, mitres, tolerance));
            }

            return result;
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
            bool closes = !across && Closes(arriving, leaving, distance, tolerance);
            GeoPoint2[] crossings = before.GetIntersections(after, tolerance);

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

            // Otherwise the corner opened up, and something has to bridge the gap.
            switch (options.Join)
            {
                case OffsetJoin.Round:
                    // An arc of the offset distance about the corner stands exactly that far from it, so
                    // it is held to the same rule as everything that was moved.
                    result.Add(new Piece(RoundJoin(from, to, corner, distance), false, -1, vertex, across));
                    return after;

                case OffsetJoin.Chamfer:
                    result.Add(new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
                    return after;

                default:
                    return MiterJoin(result, after, from, to, corner, vertex, across, distance, options, mitres, tolerance);
            }
        }

        /// <summary>
        /// Determines whether the corner between two edges turns towards the side the offset goes, so that the moved
        /// pieces run into each other there rather than apart.
        /// </summary>
        /// <remarks>
        /// A corner turning right back, within the point tolerance over its edges, turns both ways at once, and is
        /// read as closing: where its pieces cross, they are cut back to the crossing. The chain along x to (100, 0),
        /// round the half circle of radius 50 back over itself to (100, 100) and along x again, offset by 10 into the
        /// U: the moved line y = 10 and the moved circle of radius 60 meet at (55.28, 10), where the corner closes.
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
                return true;
            }

            return turn * distance > 0.0;
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

            // The short way round, which is the way that stays on the side the offset went.
            bool clockwise = first.CrossProduct(second) < 0.0;

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
        /// Bridges an opened corner by running both pieces on to where they would meet, or gives up and
        /// cuts straight across when that point is further away than the miter limit allows.
        /// </summary>
        /// <remarks>
        /// The pieces are run on along the curves behind them, so an arc reaches further round its own
        /// circle rather than being cut straight across: that is what AutoCAD's OFFSET does at a corner.
        /// A miter can run away at a nearly straight corner, and the limit is where it is given up on.
        /// </remarks>
        private static GeoEdge2 MiterJoin(
            List<Piece> result,
            GeoEdge2 after,
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

            if (TryMeeting(before, after, from, to, corner, distance, options, tolerance, out GeoPoint2 meeting))
            {
                if (!across)
                {
                    mitres.Add(new Mitre(vertex, before, after, meeting));
                }

                result[result.Count - 1] = result[result.Count - 1].With(CurveMeet2.Stretch(before, meeting, true));

                return CurveMeet2.Stretch(after, meeting, false);
            }

            // A miter that runs away is cut straight across instead.
            result.Add(new Piece(new GeoEdge2(from, to), true, -1, vertex, across));
            return after;
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
            double distance,
            OffsetOptions options,
            Tolerance tolerance,
            out GeoPoint2 meeting)
        {
            meeting = default(GeoPoint2);

            // The pieces have already been moved, so the curves behind them are taken as they stand.
            List<GeoPoint2> candidates = CurveMeet2.Where(before, 0.0, after, 0.0, tolerance);

            double limit = Math.Abs(distance) * options.MiterLimit;
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
                // An arc cut back to a point is no piece, and nothing can be read off it.
                if (!IsPoint(offset[i].Edge))
                {
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

            return Group(survivors, held, closed, tolerance);
        }

        /// <summary>
        /// Drops each kept part that runs along one kept before it, within the tolerance from end to end.
        /// </summary>
        /// <remarks>
        /// Two pieces can stand the distance away along the same stretch: two edges on one line, either side of a
        /// notch, each moved and run on to its miter, or the teeth of a comb whose feet line up. The offset passes
        /// there once. Kept twice, the stretch leaves the run it belongs to ending where the next one does not begin,
        /// and a loop round it never closes. Parts running the other way along each other are a spike, and stay.
        /// </remarks>
        private static void DropCovered(List<KeyValuePair<GeoEdge2, bool>>[] parts, Tolerance tolerance)
        {
            var kept = new List<Carrier>();
            var boxes = new List<Box>();

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < parts[i].Count; j++)
                {
                    if (!parts[i][j].Value)
                    {
                        continue;
                    }

                    GeoEdge2 part = parts[i][j].Key;
                    Box box = Box.Of(part).Grown(tolerance.EqualPoint);
                    bool covered = false;

                    for (int k = 0; k < kept.Count && !covered; k++)
                    {
                        covered = boxes[k].Overlaps(box) && Covers(kept[k], part, tolerance);
                    }

                    if (covered)
                    {
                        parts[i][j] = new KeyValuePair<GeoEdge2, bool>(part, false);
                        continue;
                    }

                    kept.Add(new Carrier(part));
                    boxes.Add(box);
                }
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
                boxes[i] = IsPoint(offset[i].Edge) ? Box.Empty : Box.Of(offset[i].Edge).Grown(tolerance.EqualPoint);
            }

            for (int i = 0; i < offset.Count; i++)
            {
                for (int j = i + 2; j < offset.Count; j++)
                {
                    if (closed && i == 0 && j == offset.Count - 1)
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

            // Whether each stretch between the cuts stands far enough away, read at its middle.
            var stands = new bool[cuts.Count + 1];
            double from = 0.0;

            for (int i = 0; i <= cuts.Count; i++)
            {
                double to = i < cuts.Count ? cuts[i].Parameter : 1.0;

                stands[i] = asBefore || Stands(edge.GetPointAtParameter((from + to) * 0.5), shape, distance, slack);
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
        /// A corner joined by a miter, and the two moved pieces it ran on, as they stood before.
        /// </summary>
        private struct Mitre
        {
            internal Mitre(int corner, GeoEdge2 before, GeoEdge2 after, GeoPoint2 meeting)
            {
                Corner = corner;
                Before = before;
                After = after;
                Meeting = meeting;
            }

            internal int Corner { get; }

            internal GeoEdge2 Before { get; }

            internal GeoEdge2 After { get; }

            internal GeoPoint2 Meeting { get; }
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
        /// shape would come back where two were wanted.
        /// </remarks>
        private static List<List<GeoEdge2>> Group(List<List<GeoEdge2>> survivors, List<bool> held, bool closed, Tolerance tolerance)
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

            var loops = new List<List<GeoEdge2>>();

            foreach (List<GeoEdge2> group in groups)
            {
                // A run that never comes back to where it began encloses nothing, and is no answer.
                if (group[group.Count - 1].EndPoint.IsEqualTo(group[0].StartPoint, tolerance))
                {
                    loops.Add(group);
                }
            }

            return loops;
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
