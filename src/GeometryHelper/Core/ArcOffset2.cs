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
    /// valid offset stands exactly the offset distance away from the shape it came from. A piece whose
    /// middle stands nearer than that has been folded over by a neighbour and goes.
    /// </para>
    /// </summary>
    internal static class ArcOffset2
    {
        /// <summary>
        /// One piece of the offset, and whether it came from moving an edge or was built to bridge a
        /// corner that opened up.
        /// </summary>
        /// <remarks>
        /// The difference matters when the folded parts are thrown away. A piece that was moved stands
        /// exactly the offset distance from the shape, and anything nearer has been folded over. A bridge
        /// does not: a chamfer across a corner deliberately cuts inside that distance, and is no less
        /// wanted for it.
        /// </remarks>
        private struct Piece
        {
            internal Piece(GeoEdge2 edge, bool bridge)
            {
                Edge = edge;
                Bridge = bridge;
            }

            internal GeoEdge2 Edge { get; }

            internal bool Bridge { get; }

            internal Piece With(GeoEdge2 edge) => new Piece(edge, Bridge);
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

            List<Piece> joined = Join(edges, kept, moved, closed, distance, options, tolerance);

            return Prune(joined, edges, closed, Math.Abs(distance), tolerance);
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
            Tolerance tolerance)
        {
            var result = new List<Piece>(moved.Count * 2);

            for (int i = 0; i < moved.Count; i++)
            {
                result.Add(new Piece(moved[i], false));

                if (i < moved.Count - 1)
                {
                    moved[i + 1] = Corner(result, moved[i + 1], edges[kept[i]].EndPoint, distance, options, tolerance);
                }
            }

            // The corner that closes a loop sits between the piece at the end of the run and the piece at
            // its beginning, and that first piece is already in the run, so it is the one in the run that
            // has to be put right rather than the one it was made from.
            if (closed && result.Count > 1)
            {
                GeoPoint2 corner = edges[kept[moved.Count - 1]].EndPoint;

                result[0] = result[0].With(Corner(result, result[0].Edge, corner, distance, options, tolerance));
            }

            return result;
        }

        /// <summary>
        /// Deals with one corner of the offset: trims the two pieces back to where they cross, or puts a
        /// join in between them when they no longer reach each other.
        /// </summary>
        /// <returns>The piece that follows the corner, cut back when the corner closed up.</returns>
        private static GeoEdge2 Corner(
            List<Piece> result,
            GeoEdge2 after,
            GeoPoint2 corner,
            double distance,
            OffsetOptions options,
            Tolerance tolerance)
        {
            GeoEdge2 before = result[result.Count - 1].Edge;

            if (before.EndPoint.IsEqualTo(after.StartPoint, tolerance))
            {
                return after;
            }

            // Where the two pieces still cross, the corner closed up and both are cut back to the crossing.
            GeoPoint2[] crossings = before.GetIntersections(after, tolerance);

            if (crossings.Length > 0)
            {
                GeoPoint2 at = Nearest(crossings, corner);

                result[result.Count - 1] = result[result.Count - 1].With(Upto(before, at, tolerance));
                return From(after, at, tolerance);
            }

            // Otherwise it opened up, and something has to bridge the gap.
            GeoPoint2 from = before.EndPoint;
            GeoPoint2 to = after.StartPoint;

            switch (options.Join)
            {
                case OffsetJoin.Round:
                    // An arc of the offset distance about the corner stands exactly that far from it, so
                    // it is held to the same rule as everything that was moved.
                    result.Add(new Piece(RoundJoin(from, to, corner, distance), false));
                    return after;

                case OffsetJoin.Chamfer:
                    result.Add(new Piece(new GeoEdge2(from, to), true));
                    return after;

                default:
                    return MiterJoin(result, after, from, to, corner, distance, options, tolerance);
            }
        }

        /// <summary>
        /// Bridges an opened corner with an arc of the offset distance about the corner it came from.
        /// </summary>
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
            double distance,
            OffsetOptions options,
            Tolerance tolerance)
        {
            GeoEdge2 before = result[result.Count - 1].Edge;

            if (TryMeeting(before, after, from, to, corner, distance, options, tolerance, out GeoPoint2 meeting))
            {
                result[result.Count - 1] = result[result.Count - 1].With(CurveMeet2.Stretch(before, meeting, true));

                return CurveMeet2.Stretch(after, meeting, false);
            }

            // A miter that runs away is cut straight across instead.
            result.Add(new Piece(new GeoEdge2(from, to), true));
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

            if (!grown.IsArc)
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
        /// Every point of a valid offset stands exactly the offset distance from the shape it came from.
        /// Where the offset crosses itself, the piece on the wrong side of that crossing stands nearer, and
        /// that is what marks it. Nothing else is needed: no winding rule, no clipper, no tessellation.
        /// </remarks>
        private static List<List<GeoEdge2>> Prune(
            List<Piece> offset,
            List<GeoEdge2> original,
            bool closed,
            double distance,
            Tolerance tolerance)
        {
            List<List<Piece>> runs = Cut(offset, closed, tolerance);
            var survivors = new List<List<GeoEdge2>>();

            // A hair of slack, so that the piece running exactly along the offset is never thrown away.
            double least = distance - Math.Max(tolerance.EqualPoint, distance * 1E-6);

            foreach (List<Piece> run in runs)
            {
                if (!Reaches(run, original, least, tolerance))
                {
                    continue;
                }

                var edges = new List<GeoEdge2>(run.Count);

                foreach (Piece piece in run)
                {
                    edges.Add(piece.Edge);
                }

                survivors.Add(edges);
            }

            return Group(survivors, closed, tolerance);
        }

        /// <summary>
        /// Cuts a run wherever it crosses itself, so that each piece is wholly kept or wholly thrown away.
        /// </summary>
        private static List<List<Piece>> Cut(List<Piece> offset, bool closed, Tolerance tolerance)
        {
            var cuts = new List<List<GeoPoint2>>();

            for (int i = 0; i < offset.Count; i++)
            {
                cuts.Add(new List<GeoPoint2>());
            }

            for (int i = 0; i < offset.Count; i++)
            {
                for (int j = i + 2; j < offset.Count; j++)
                {
                    if (closed && i == 0 && j == offset.Count - 1)
                    {
                        continue;
                    }

                    foreach (GeoPoint2 meeting in offset[i].Edge.GetIntersections(offset[j].Edge, tolerance))
                    {
                        Mark(cuts[i], offset[i].Edge, meeting, tolerance);
                        Mark(cuts[j], offset[j].Edge, meeting, tolerance);
                    }
                }
            }

            var runs = new List<List<Piece>>();
            var current = new List<Piece>();

            for (int i = 0; i < offset.Count; i++)
            {
                GeoEdge2 rest = offset[i].Edge;

                cuts[i].Sort((one, other) => rest.GetParameterAtPoint(one, tolerance).CompareTo(rest.GetParameterAtPoint(other, tolerance)));

                foreach (GeoPoint2 at in cuts[i])
                {
                    double parameter = rest.GetParameterAtPoint(at, tolerance);

                    if (rest.TrySplitAtParameter(parameter, out GeoEdge2 before, out GeoEdge2 after, tolerance))
                    {
                        current.Add(offset[i].With(before));
                        runs.Add(current);
                        current = new List<Piece>();
                        rest = after;
                    }
                    else if (parameter <= 0.0 && current.Count > 0)
                    {
                        runs.Add(current);
                        current = new List<Piece>();
                    }
                }

                current.Add(offset[i].With(rest));
            }

            if (current.Count > 0)
            {
                runs.Add(current);
            }

            return runs;
        }

        /// <summary>
        /// Notes a crossing on an edge, unless it is one of its ends or is already noted.
        /// </summary>
        private static void Mark(List<GeoPoint2> cuts, GeoEdge2 edge, GeoPoint2 at, Tolerance tolerance)
        {
            if (at.IsEqualTo(edge.StartPoint, tolerance) || at.IsEqualTo(edge.EndPoint, tolerance))
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
        /// Determines whether a run really stands the offset distance away from the shape it came from.
        /// </summary>
        private static bool Reaches(List<Piece> run, List<GeoEdge2> original, double least, Tolerance tolerance)
        {
            foreach (Piece piece in run)
            {
                // A bridge was put there on purpose and is allowed to cut inside the distance.
                if (piece.Bridge)
                {
                    continue;
                }

                // The middle of a piece, which is the part of it furthest from wherever it was cut.
                if (ArcChain2.DistanceTo(original, piece.Edge.GetPointAtParameter(0.5), tolerance) < least)
                {
                    return false;
                }
            }

            return true;
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
        private static List<List<GeoEdge2>> Group(List<List<GeoEdge2>> survivors, bool closed, Tolerance tolerance)
        {
            var groups = new List<List<GeoEdge2>>();

            foreach (List<GeoEdge2> run in survivors)
            {
                if (groups.Count > 0 && Meets(groups[groups.Count - 1], run, tolerance))
                {
                    groups[groups.Count - 1].AddRange(run);
                }
                else
                {
                    groups.Add(new List<GeoEdge2>(run));
                }
            }

            if (!closed)
            {
                return groups;
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
