using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The walking a chain that may curve needs, shared by every operation over one.
    /// <para>
    /// Everything here measures along the arcs rather than across them, so a chain of arcs answers the same
    /// questions a straight one does and answers them exactly. Only the region operations, which go through
    /// Clipper, need the chain flattened first.
    /// </para>
    /// </summary>
    internal static class ArcChain2
    {
        /// <summary>
        /// A tolerance of nought, for measuring and crossing with no slack.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);

        /// <summary>
        /// Gets the edges of a chain, in order.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolylineArc2 chain)
        {
            var edges = new List<GeoEdge2>(chain.EdgeCount);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                edges.Add(chain.GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a loop, in order, ending with the one that closes it.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolygonArc2 loop)
        {
            var edges = new List<GeoEdge2>(loop.EdgeCount);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                edges.Add(loop.GetEdgeAt(i));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a straight chain, so that one shape can be measured against the other without
        /// either being approximated.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolyline2 chain)
        {
            var edges = new List<GeoEdge2>(chain.VertexCount - 1);

            for (int i = 0; i < chain.VertexCount - 1; i++)
            {
                edges.Add(new GeoEdge2(chain[i], chain[i + 1]));
            }

            return edges;
        }

        /// <summary>
        /// Gets the edges of a straight loop.
        /// </summary>
        internal static List<GeoEdge2> EdgesOf(GeoPolygon2 loop)
        {
            var edges = new List<GeoEdge2>(loop.VertexCount);

            for (int i = 0; i < loop.VertexCount; i++)
            {
                edges.Add(new GeoEdge2(loop[i], loop[(i + 1) % loop.VertexCount]));
            }

            return edges;
        }

        /// <summary>
        /// Gets the length along a run of edges, measured along the arcs.
        /// </summary>
        internal static double LengthOf(List<GeoEdge2> edges)
        {
            double total = 0.0;

            foreach (GeoEdge2 edge in edges)
            {
                total += edge.Length;
            }

            return total;
        }

        /// <summary>
        /// Gets the point a distance along a run of edges, clamped to its two ends.
        /// </summary>
        internal static GeoPoint2 PointAtDistance(List<GeoEdge2> edges, double distance)
        {
            if (double.IsNaN(distance) || distance <= 0.0)
            {
                return edges[0].StartPoint;
            }

            double walked = 0.0;

            foreach (GeoEdge2 edge in edges)
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
        /// Gets the point of a run of edges nearest a point: on an arc the one found at the tolerance, so a point within it of
        /// the centre is carried to the arc's start; on a straight edge the one on the edge as it is however short, where one
        /// no longer than the tolerance offered its start.
        /// </summary>
        internal static GeoPoint2 ClosestPoint(List<GeoEdge2> edges, GeoPoint2 point, Tolerance tolerance)
        {
            GeoPoint2 nearest = edges[0].GetClosestPointOnBoundary(point, edges[0].IsArc ? tolerance : Exactly);
            double best = point.GetDistanceSquaredTo(nearest);

            for (int i = 1; i < edges.Count; i++)
            {
                GeoPoint2 candidate = edges[i].GetClosestPointOnBoundary(point, edges[i].IsArc ? tolerance : Exactly);
                double distance = point.GetDistanceSquaredTo(candidate);

                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Gets how far along a run of edges the point nearest a point lies, each edge read as <see cref="ClosestPoint"/>
        /// reads it: a straight one as it is however short.
        /// </summary>
        internal static double DistanceAtPoint(List<GeoEdge2> edges, GeoPoint2 point, Tolerance tolerance)
        {
            double walked = 0.0;
            double bestWalk = 0.0;
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                Tolerance side = edge.IsArc ? tolerance : Exactly;
                GeoPoint2 candidate = edge.GetClosestPointOnBoundary(point, side);
                double distance = point.GetDistanceSquaredTo(candidate);

                if (distance < best)
                {
                    best = distance;
                    bestWalk = walked + edge.GetParameterAtPoint(point, side) * edge.Length;
                }

                walked += edge.Length;
            }

            return bestWalk;
        }

        /// <summary>
        /// Gets the distance from a run of edges to a point, exactly: the edges are the edges they are however short, and a
        /// point at the centre of an arc is as far from it as any other.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoPoint2 point)
        {
            return point.DistanceTo(ClosestPoint(edges, point, Exactly));
        }

        /// <summary>
        /// Gets the distance between two runs of edges, measured along their arcs. Each run is the sides of a shape, so a
        /// side shorter than the point tolerance is the side it is, not a segment read as its start.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> first, List<GeoEdge2> second)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    double distance = one.DistanceTo(other, Exactly);

                    if (distance < best)
                    {
                        best = distance;

                        if (best <= 0.0)
                        {
                            return 0.0;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Whether a tolerance reads an edge as the point at its start: a straight one no longer than the point tolerance,
        /// which the projection onto a segment and the crossing test take for a point.
        /// </summary>
        internal static bool ReadsAsPoint(GeoEdge2 edge, Tolerance tolerance) => !edge.IsArc && ReadsAsPoint(edge.StartPoint, edge.EndPoint, tolerance);

        /// <summary>
        /// Whether a tolerance reads a side of a polygon, a polyline or a rectangle as the point at its start, as it reads a
        /// straight edge.
        /// </summary>
        internal static bool ReadsAsPoint(GeoLine2 side, Tolerance tolerance) => ReadsAsPoint(side.StartPoint, side.EndPoint, tolerance);

        /// <summary>
        /// Whether a tolerance reads the straight run from one point to another as the point at its start.
        /// </summary>
        private static bool ReadsAsPoint(GeoPoint2 start, GeoPoint2 end, Tolerance tolerance)
        {
            // The projection weighs the square of the run from the start to the end, and the crossing test its root, which
            // can come out no longer than the tolerance where the square is a few parts in 1E16 over it; the root is taken only
            // there.
            double x = end.X - start.X;
            double y = end.Y - start.Y;
            double square = x * x + y * y;
            double limit = tolerance.EqualPoint * tolerance.EqualPoint;

            return square <= limit || (square <= limit * (1.0 + 1E-14) && Math.Sqrt(square) <= tolerance.EqualPoint);
        }

        /// <summary>
        /// Gets the tolerance a side of a shape is joined to something else at: the one given, but nought for a straight side
        /// it would read as a point, which is the side it is however short. A side 0.006 long is joined at nought at a
        /// tolerance of a hundredth, and at the tolerance given at a thousandth.
        /// </summary>
        internal static Tolerance ForSide(GeoEdge2 side, Tolerance tolerance) => ReadsAsPoint(side, tolerance) ? Exactly : tolerance;

        /// <summary>
        /// Gets the tolerance a side of a polygon, a polyline or a rectangle is joined to something else at, as for an edge.
        /// </summary>
        internal static Tolerance ForSide(GeoLine2 side, Tolerance tolerance) => ReadsAsPoint(side, tolerance) ? Exactly : tolerance;

        /// <summary>
        /// Gets the tolerance two sides of shapes are joined at: nought where either is a straight side the tolerance would
        /// read as a point.
        /// </summary>
        private static Tolerance ForSides(GeoEdge2 side, GeoEdge2 other, Tolerance tolerance)
            => ReadsAsPoint(side, tolerance) || ReadsAsPoint(other, tolerance) ? Exactly : tolerance;

        /// <summary>
        /// Gets the tolerance two straight sides of shapes are joined at, as for two edges.
        /// </summary>
        internal static Tolerance ForSides(GeoLine2 side, GeoLine2 other, Tolerance tolerance)
            => ReadsAsPoint(side, tolerance) || ReadsAsPoint(other, tolerance) ? Exactly : tolerance;

        /// <summary>
        /// Gets the shortest segment joining a run of edges to a straight segment.
        /// </summary>
        /// <remarks>
        /// Nothing here knows whether the run closes on itself, so nothing here treats it as a filled
        /// region: the answer runs from the boundary to the other shape, which is what
        /// <see cref="Projection2"/> promises of every shape and where it parts company with
        /// <see cref="Distance2"/>. Each edge is a side of a shape, joined as it is however short (see
        /// <see cref="ForSide(GeoEdge2, Tolerance)"/>).
        /// </remarks>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(line, ForSide(edges[0], tolerance));

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(line, ForSide(edges[i], tolerance)));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a run of edges to an arc, each edge joined as it is however short.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(arc, ForSide(edges[0], tolerance));

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(arc, ForSide(edges[i], tolerance)));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a run of edges to a circle, each edge joined as it is however short.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            GeoLine2 best = edges[0].GetShortestLineTo(circle, ForSide(edges[0], tolerance));

            for (int i = 1; i < edges.Count && best.Length > 0.0; i++)
            {
                Consider(ref best, edges[i].GetShortestLineTo(circle, ForSide(edges[i], tolerance)));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two runs of edges, each edge of either joined as it is however short.
        /// </summary>
        internal static GeoLine2 ShortestLineTo(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            GeoLine2 best = first[0].GetShortestLineTo(second[0], ForSides(first[0], second[0], tolerance));

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    Consider(ref best, one.GetShortestLineTo(other, ForSides(one, other, tolerance)));

                    if (best.Length <= 0.0)
                    {
                        return best;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Keeps the shorter of the segment in hand and the one offered.
        /// </summary>
        /// <remarks>
        /// The comparison is strict, so where two pieces of the run are equally near the earlier one is
        /// the one the answer comes from.
        /// </remarks>
        private static void Consider(ref GeoLine2 best, GeoLine2 candidate)
        {
            if (candidate.Length < best.Length)
            {
                best = candidate;
            }
        }

        /// <summary>
        /// Gets the points where two runs of edges meet, with repeats left out.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    foreach (GeoPoint2 meeting in Meetings(one, other, tolerance))
                    {
                        Keep(found, meeting, tolerance);
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether two runs of edges cross anywhere: two straight edges where they truly cross, at a point on
        /// both, with no slack past their ends, and a pair with an arc in it where they truly meet, with no slack at all.
        /// </summary>
        internal static bool Cross(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    bool crossing = one.IsArc || other.IsArc
                        ? Meet(one, other)
                        : Collision2.Crosses(one.ToLine(), other.ToLine(), tolerance);

                    if (crossing)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges crosses a straight segment anywhere: a straight edge where the two truly
        /// cross, with no slack past their ends, and an arc where it truly meets the segment, with no slack at all.
        /// </summary>
        internal static bool Cross(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            foreach (GeoEdge2 edge in edges)
            {
                bool crossing = edge.IsArc
                    ? Meet(edge, line)
                    : Collision2.Crosses(edge.ToLine(), line, tolerance);

                if (crossing)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two runs of edges truly meet anywhere, with no slack: two straight edges where they cross
        /// or touch, and a pair with an arc in it where <see cref="Arc2.Meets(GeoArc2, GeoLine2)"/> or
        /// <see cref="Arc2.Meets(GeoArc2, GeoArc2)"/> says so.
        /// </summary>
        internal static bool Meet(List<GeoEdge2> first, List<GeoEdge2> second)
        {
            foreach (GeoEdge2 one in first)
            {
                foreach (GeoEdge2 other in second)
                {
                    if (Meet(one, other))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges truly meets a straight segment anywhere, with no slack.
        /// </summary>
        internal static bool Meet(List<GeoEdge2> edges, GeoLine2 line)
        {
            foreach (GeoEdge2 edge in edges)
            {
                if (Meet(edge, line))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges truly meets an arc anywhere, with no slack.
        /// </summary>
        internal static bool Meet(List<GeoEdge2> edges, GeoArc2 arc)
        {
            foreach (GeoEdge2 edge in edges)
            {
                bool meets = Straight(edge, out GeoLine2 straight)
                    ? Arc2.Meets(arc, straight)
                    : Arc2.Meets(edge.ToArc(), arc);

                if (meets)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether two edges truly meet, with no slack.
        /// </summary>
        private static bool Meet(GeoEdge2 edge, GeoEdge2 other)
        {
            if (!edge.IsArc)
            {
                return Meet(other, edge.ToLine());
            }

            GeoArc2 arc = edge.ToArc();

            if (Chord(edge, arc, out GeoLine2 chord))
            {
                return Meet(other, chord);
            }

            if (!other.IsArc)
            {
                return Arc2.Meets(arc, other.ToLine());
            }

            GeoArc2 otherArc = other.ToArc();

            return Chord(other, otherArc, out GeoLine2 otherChord)
                ? Arc2.Meets(arc, otherChord)
                : Arc2.Meets(arc, otherArc);
        }

        /// <summary>
        /// Determines whether an edge truly meets a segment, with no slack.
        /// </summary>
        private static bool Meet(GeoEdge2 edge, GeoLine2 line)
        {
            if (!edge.IsArc)
            {
                return Collision2.Crosses(edge.ToLine(), line, Exactly);
            }

            GeoArc2 arc = edge.ToArc();

            return Chord(edge, arc, out GeoLine2 chord)
                ? Collision2.Crosses(chord, line, Exactly)
                : Arc2.Meets(arc, line);
        }

        /// <summary>
        /// Gets an edge as the segment it is read as: itself when it is straight, and its chord when it bows so little that
        /// the chord is no further from it than the arc's own rounding, see <see cref="Arc2.IsFlat"/>. A bulge of 3E-9 over
        /// a chord of 100, bowing 1.5E-7 on a radius of 8E8, is read so; a bulge of a ten-millionth is not.
        /// </summary>
        /// <returns>true if the edge reads as a segment; false for a true arc.</returns>
        internal static bool Straight(GeoEdge2 edge, out GeoLine2 line)
        {
            if (!edge.IsArc)
            {
                line = edge.ToLine();
                return true;
            }

            return Chord(edge, edge.ToArc(), out line);
        }

        /// <summary>
        /// Gets the chord of a curved edge, given as the arc it is, when the edge bows so little that the chord is read for it.
        /// </summary>
        private static bool Chord(GeoEdge2 edge, GeoArc2 arc, out GeoLine2 chord)
        {
            double sag = Math.Abs(edge.Bulge) * edge.StartPoint.DistanceTo(edge.EndPoint) * 0.5;

            if (Math.Abs(edge.Bulge) <= 1.0 && Arc2.IsFlat(sag, arc.Center, arc.Radius))
            {
                chord = new GeoLine2(edge.StartPoint, edge.EndPoint);
                return true;
            }

            chord = default(GeoLine2);
            return false;
        }

        /// <summary>
        /// Gets the distance from a shape to an edge that may curve, given how the shape measures a segment, an arc and a
        /// point: an edge that bows too little reads as its chord, and a true arc edge also tries its stored end points
        /// where the answer is within the rounding of the arc, so that a vertex the shape shares with the edge reads nought.
        /// </summary>
        internal static double Edge(GeoEdge2 edge, Func<GeoLine2, double> line, Func<GeoArc2, double> arc, Func<GeoPoint2, double> point)
        {
            if (Straight(edge, out GeoLine2 straight))
            {
                return line(straight);
            }

            GeoArc2 round = edge.ToArc();
            double gap = arc(round);

            return Rounds(gap, round) ? Math.Min(gap, Math.Min(point(edge.StartPoint), point(edge.EndPoint))) : gap;
        }

        /// <summary>
        /// Gets the exact distance from an edge to a segment: nought only where they truly meet, a segment no longer than
        /// the tolerance read as its start point where it does not cross.
        /// </summary>
        internal static double Gap(GeoEdge2 edge, GeoLine2 line, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return Distance2.DistanceTo(edge.ToLine(), line, tolerance);
            }

            GeoArc2 arc = edge.ToArc();

            if (Chord(edge, arc, out GeoLine2 chord))
            {
                return Distance2.DistanceTo(chord, line, tolerance);
            }

            double gap = Arc2.Bowed(arc, line, tolerance);

            return Rounds(gap, arc)
                ? Math.Min(gap, Math.Min(Distance2.DistanceTo(line, edge.StartPoint, tolerance), Distance2.DistanceTo(line, edge.EndPoint, tolerance)))
                : gap;
        }

        /// <summary>
        /// Gets the exact distance from an edge of a shape to a segment a caller gave: the edge is the edge it is however
        /// short, and the segment, if no longer than the tolerance, is read as its start point where it does not cross.
        /// </summary>
        internal static double GapOfSide(GeoEdge2 edge, GeoLine2 line, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return Distance2.DistanceToSide(edge.ToLine(), line, tolerance);
            }

            GeoArc2 arc = edge.ToArc();

            if (Chord(edge, arc, out GeoLine2 chord))
            {
                return Distance2.DistanceToSide(chord, line, tolerance);
            }

            return Gap(edge, arc, line, tolerance);
        }

        /// <summary>
        /// Gets the exact distance from an edge to an arc: nought only where they truly meet.
        /// </summary>
        internal static double Gap(GeoEdge2 edge, GeoArc2 arc, Tolerance tolerance)
        {
            if (Straight(edge, out GeoLine2 straight))
            {
                return Arc2.Gap(arc, straight, tolerance);
            }

            GeoArc2 round = edge.ToArc();
            double gap = Arc2.Gap(round, arc);

            return Rounds(gap, round)
                ? Math.Min(gap, Math.Min(Arc2.DistanceTo(arc, edge.StartPoint, Exactly), Arc2.DistanceTo(arc, edge.EndPoint, Exactly)))
                : gap;
        }

        /// <summary>
        /// Gets the exact distance between two edges: nought only where they truly meet, a segment no longer than the
        /// tolerance read as its start point where it does not cross.
        /// </summary>
        internal static double Gap(GeoEdge2 edge, GeoEdge2 other, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                return Gap(other, edge.ToLine(), tolerance);
            }

            GeoArc2 arc = edge.ToArc();

            if (Chord(edge, arc, out GeoLine2 chord))
            {
                return Gap(other, chord, tolerance);
            }

            if (!other.IsArc)
            {
                return Gap(edge, arc, other.ToLine(), tolerance);
            }

            GeoArc2 otherArc = other.ToArc();

            if (Chord(other, otherArc, out GeoLine2 otherChord))
            {
                return Gap(edge, arc, otherChord, tolerance);
            }

            double gap = Arc2.Gap(arc, otherArc);

            if (!Rounds(gap, arc, otherArc))
            {
                return gap;
            }

            // Two edges that share a vertex share it exactly, though each arc places its ends through its centre.
            gap = Math.Min(gap, Math.Min(
                Math.Min(edge.StartPoint.DistanceTo(other.StartPoint), edge.StartPoint.DistanceTo(other.EndPoint)),
                Math.Min(edge.EndPoint.DistanceTo(other.StartPoint), edge.EndPoint.DistanceTo(other.EndPoint))));

            return Math.Min(gap, Math.Min(
                Math.Min(Arc2.DistanceTo(otherArc, edge.StartPoint, Exactly), Arc2.DistanceTo(otherArc, edge.EndPoint, Exactly)),
                Math.Min(Arc2.DistanceTo(arc, other.StartPoint, Exactly), Arc2.DistanceTo(arc, other.EndPoint, Exactly))));
        }

        /// <summary>
        /// Gets the exact distance from a curved edge, given with its arc, to a segment, with the edge's own end points as
        /// candidates where the answer is within the rounding of the arc.
        /// </summary>
        private static double Gap(GeoEdge2 edge, GeoArc2 arc, GeoLine2 line, Tolerance tolerance)
        {
            double gap = Arc2.Bowed(arc, line, tolerance);

            return Rounds(gap, arc)
                ? Math.Min(gap, Math.Min(Distance2.DistanceTo(line, edge.StartPoint, tolerance), Distance2.DistanceTo(line, edge.EndPoint, tolerance)))
                : gap;
        }

        /// <summary>
        /// Whether a distance to an arc is so small that the rounding of the arc's own points, up to 1E-15 of how far its
        /// centre and radius reach from the origin, could be all there is to it: a vertex shared with another edge, which
        /// the arc places a few parts in 1E16 of its radius off.
        /// </summary>
        private static bool Rounds(double gap, GeoArc2 arc)
            => gap <= 1E-14 * (Math.Abs(arc.Center.X) + Math.Abs(arc.Center.Y) + arc.Radius);

        /// <summary>
        /// Whether a distance between two arcs is within the rounding of either.
        /// </summary>
        private static bool Rounds(double gap, GeoArc2 arc, GeoArc2 other) => Rounds(gap, arc) || Rounds(gap, other);

        /// <summary>
        /// Adds a point to a list unless the same point is already in it.
        /// </summary>
        /// <remarks>
        /// Two edges that share a vertex meet there, and so do the arcs either side of it, so the same
        /// meeting point arrives more than once and is worth reporting once.
        /// </remarks>
        internal static void Keep(List<GeoPoint2> found, GeoPoint2 point, Tolerance tolerance)
        {
            foreach (GeoPoint2 already in found)
            {
                if (already.IsEqualTo(point, tolerance))
                {
                    return;
                }
            }

            found.Add(point);
        }

        /// <summary>
        /// Gets the distance from a run of edges to a straight segment.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = GapOfSide(edge, line, tolerance);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the distance from a run of edges to an arc.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoArc2 arc)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = edge.DistanceTo(arc, Exactly);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the distance from a run of edges to a circle.
        /// </summary>
        internal static double DistanceTo(List<GeoEdge2> edges, GeoCircle2 circle)
        {
            double best = double.MaxValue;

            foreach (GeoEdge2 edge in edges)
            {
                double distance = edge.DistanceTo(circle, Exactly);

                if (distance < best)
                {
                    best = distance;

                    if (best <= 0.0)
                    {
                        return 0.0;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Determines whether two runs of edges come within the point tolerance of each other anywhere.
        /// </summary>
        /// <remarks>
        /// Edge against edge, each pair measured exactly, a segment against a segment, a segment against an arc or
        /// two arcs, once a test of their boxes has set aside the pairs more than twice the point tolerance apart; the
        /// first pair near enough ends the search. Nothing here asks whether the two collide, so the collision tests
        /// can lean on it.
        /// </remarks>
        internal static bool ComeWithin(List<GeoEdge2> first, List<GeoEdge2> second, Tolerance tolerance)
        {
            var boxes = new Box[second.Count];

            for (int j = 0; j < second.Count; j++)
            {
                boxes[j] = Box.Of(second[j]);
            }

            foreach (GeoEdge2 one in first)
            {
                Box box = Box.Of(one);

                for (int j = 0; j < second.Count; j++)
                {
                    if (box.Near(boxes[j], tolerance) && one.DistanceTo(second[j], Exactly) <= tolerance.EqualPoint)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges comes within the point tolerance of an arc anywhere.
        /// </summary>
        internal static bool ComeWithin(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            Box box = Box.Around(arc.Center, arc.Radius);

            foreach (GeoEdge2 edge in edges)
            {
                if (box.Near(Box.Of(edge), tolerance) && edge.DistanceTo(arc, Exactly) <= tolerance.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a run of edges comes within the point tolerance of a circle, read as the disc it bounds.
        /// </summary>
        internal static bool ComeWithin(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            Box box = Box.Around(circle.Center, circle.Radius);

            foreach (GeoEdge2 edge in edges)
            {
                if (box.Near(Box.Of(edge), tolerance) && edge.DistanceTo(circle, Exactly) <= tolerance.EqualPoint)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A box holding an edge, an arc or a circle, for setting aside the pairs that are plainly apart.
        /// </summary>
        private readonly struct Box
        {
            private readonly double _minX;
            private readonly double _minY;
            private readonly double _maxX;
            private readonly double _maxY;

            private Box(double minX, double minY, double maxX, double maxY)
            {
                _minX = minX;
                _minY = minY;
                _maxX = maxX;
                _maxY = maxY;
            }

            /// <summary>
            /// The box of an edge. An arc of at most a half turn, a bulge of at most one, lies within its sagitta,
            /// half the chord times the size of the bulge, of its chord, so within the chord's box grown by that much.
            /// A longer one lies within half its chord times the size of the bulge of the chord's middle.
            /// </summary>
            internal static Box Of(GeoEdge2 edge)
            {
                var chord = new Box(
                    Math.Min(edge.StartPoint.X, edge.EndPoint.X), Math.Min(edge.StartPoint.Y, edge.EndPoint.Y),
                    Math.Max(edge.StartPoint.X, edge.EndPoint.X), Math.Max(edge.StartPoint.Y, edge.EndPoint.Y));

                if (!edge.IsArc)
                {
                    return chord;
                }

                double bulge = Math.Abs(edge.Bulge);
                double half = 0.5 * Distance2.DistanceTo(edge.StartPoint, edge.EndPoint);

                if (bulge <= 1.0)
                {
                    double sagitta = half * bulge;

                    return new Box(chord._minX - sagitta, chord._minY - sagitta, chord._maxX + sagitta, chord._maxY + sagitta);
                }

                return Around(
                    new GeoPoint2(0.5 * (edge.StartPoint.X + edge.EndPoint.X), 0.5 * (edge.StartPoint.Y + edge.EndPoint.Y)),
                    half * bulge);
            }

            /// <summary>
            /// The box of everything within a reach of a centre, which holds an arc or a circle about it.
            /// </summary>
            internal static Box Around(GeoPoint2 centre, double reach)
                => new Box(centre.X - reach, centre.Y - reach, centre.X + reach, centre.Y + reach);

            /// <summary>
            /// Whether two boxes come within twice the point tolerance of each other, so that nothing within the
            /// tolerance is set aside.
            /// </summary>
            internal bool Near(Box other, Tolerance tolerance)
            {
                double reach = 2.0 * tolerance.EqualPoint;

                return other._minX - _maxX <= reach
                    && _minX - other._maxX <= reach
                    && other._minY - _maxY <= reach
                    && _minY - other._maxY <= reach;
            }
        }

        /// <summary>
        /// Gets the points where an edge of a shape meets a segment you give: the edge is the edge it is however short,
        /// and the segment, if no longer than the tolerance, crosses a straight edge only where it truly does.
        /// </summary>
        internal static GeoPoint2[] Meetings(GeoEdge2 side, GeoLine2 line, Tolerance tolerance)
        {
            if (side.IsArc)
            {
                return Arc2.GetIntersections(side.ToArc(), line, tolerance);
            }

            return Intersection2.TryCross(side.ToLine(), false, line, true, out GeoPoint2 meeting, tolerance) ? new[] { meeting } : new GeoPoint2[0];
        }

        /// <summary>
        /// Gets the points where two edges of shapes meet, each the edge it is however short: two straight edges cross as
        /// two segments do, neither read as its start.
        /// </summary>
        internal static GeoPoint2[] Meetings(GeoEdge2 side, GeoEdge2 other, Tolerance tolerance)
        {
            if (side.IsArc || other.IsArc)
            {
                return side.GetIntersections(other, tolerance);
            }

            return Intersection2.Crossing(side.ToLine(), other.ToLine(), Enums.LineExtension.None, out GeoPoint2 meeting, tolerance) ? new[] { meeting } : new GeoPoint2[0];
        }

        /// <summary>
        /// Gets the points where a run of edges meets a straight segment.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoLine2 line, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 meeting in Meetings(edge, line, tolerance))
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the points where a run of edges meets an arc.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoArc2 arc, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                foreach (GeoPoint2 meeting in edge.GetIntersections(arc, tolerance))
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the points where a run of edges meets a circle.
        /// </summary>
        internal static GeoPoint2[] Intersections(List<GeoEdge2> edges, GeoCircle2 circle, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (GeoEdge2 edge in edges)
            {
                GeoPoint2[] meetings = edge.IsArc
                    ? Arc2.GetIntersections(edge.ToArc(), circle, tolerance)
                    : Intersection2.GetIntersections(circle, edge.ToLine(), tolerance);

                foreach (GeoPoint2 meeting in meetings)
                {
                    Keep(found, meeting, tolerance);
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Cuts a run of edges at distances along it, and returns the runs between the cuts.
        /// </summary>
        /// <remarks>
        /// A cut inside an arc leaves two arcs of the same radius rather than two chords, and a cut landing
        /// on a vertex ends the run there without splitting anything. A distance at either end, off the
        /// run, or repeated is ignored, so the runs always have something in them.
        /// </remarks>
        internal static List<List<GeoEdge2>> SplitAt(List<GeoEdge2> edges, IEnumerable<double> distances, Tolerance tolerance)
        {
            double total = LengthOf(edges);
            var cuts = new List<double>();

            foreach (double distance in distances)
            {
                if (double.IsNaN(distance) || distance <= tolerance.EqualPoint || distance >= total - tolerance.EqualPoint)
                {
                    continue;
                }

                cuts.Add(distance);
            }

            cuts.Sort();

            var runs = new List<List<GeoEdge2>>();
            var current = new List<GeoEdge2>();
            int next = 0;
            double startOfEdge = 0.0;

            foreach (GeoEdge2 edge in edges)
            {
                GeoEdge2 rest = edge;
                double restStart = startOfEdge;

                while (next < cuts.Count && cuts[next] < startOfEdge + edge.Length - tolerance.EqualPoint)
                {
                    double restLength = rest.Length;
                    double parameter = restLength <= 0.0 ? 0.0 : (cuts[next] - restStart) / restLength;

                    if (rest.TrySplitAtParameter(parameter, out GeoEdge2 before, out GeoEdge2 after, tolerance))
                    {
                        current.Add(before);
                        runs.Add(current);
                        current = new List<GeoEdge2>();
                        restStart = cuts[next];
                        rest = after;
                    }
                    else if (parameter <= 0.0 && current.Count > 0)
                    {
                        // The cut lands on the vertex this edge starts at, so the run ends there whole.
                        runs.Add(current);
                        current = new List<GeoEdge2>();
                    }

                    next++;
                }

                current.Add(rest);
                startOfEdge += edge.Length;
            }

            if (current.Count > 0)
            {
                runs.Add(current);
            }

            return runs;
        }

        /// <summary>
        /// Determines whether a loop that may curve encloses a point, not counting its boundary.
        /// </summary>
        /// <remarks>
        /// Walking the arcs rather than the chords adds the piece each arc cuts off its chord where the arc
        /// bulges outward, and takes it away where the arc bulges inward. So the answer is the straight
        /// loop through the vertices, turned inside out once for every such piece the point lies in. It is
        /// exact: no arc is approximated anywhere in it.
        /// <para>
        /// No tolerance is read here: a point within the tolerance of the true boundary is on it, and is
        /// settled before this is asked. A chord is not the boundary, so the straight loop and each piece
        /// read the side of a chord by one exact rule, <see cref="BeforeChord"/>, and agree on every point
        /// near it. A point exactly on a chord counts as lying a hair towards +X of it, or a hair towards +Y
        /// of a chord that runs along X.
        /// </para>
        /// </remarks>
        internal static bool Encloses(GeoPolygonArc2 loop, GeoPoint2 point)
        {
            // The straight loop through the vertices, by crossings of the ray from the point towards +X: a chord counts
            // when it spans the point's Y, half-open, and the point lies before it.
            bool inside = false;
            int n = loop.VertexCount;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                GeoPoint2 p1 = loop[i];
                GeoPoint2 p2 = loop[j];

                if ((p1.Y > point.Y) != (p2.Y > point.Y) && BeforeChord(p1, p2, point))
                {
                    inside = !inside;
                }
            }

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                if (loop.GetEdgeAt(i).CutsOff(point))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Determines whether a run of edges coming back to where it began encloses a point, not counting its boundary,
        /// read as <see cref="Encloses(GeoPolygonArc2, GeoPoint2)"/> reads a loop: the straight loop through the starts of
        /// the edges, turned inside out once for every piece an arc cuts off its chord that the point lies in.
        /// </summary>
        internal static bool Encloses(List<GeoEdge2> run, GeoPoint2 point)
        {
            bool inside = false;

            for (int i = 0, j = run.Count - 1; i < run.Count; j = i++)
            {
                GeoPoint2 p1 = run[i].StartPoint;
                GeoPoint2 p2 = run[j].StartPoint;

                if ((p1.Y > point.Y) != (p2.Y > point.Y) && BeforeChord(p1, p2, point))
                {
                    inside = !inside;
                }
            }

            foreach (GeoEdge2 edge in run)
            {
                if (edge.CutsOff(point))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Whether a point lies before the line through two points, read exactly: towards -X of it at the point's Y, or,
        /// for a line that runs along X, towards -Y of it. A point on the line is not before it.
        /// </summary>
        /// <remarks>
        /// The straight loop of <see cref="Encloses(GeoPolygonArc2, GeoPoint2)"/> and the pieces of
        /// <see cref="GeoEdge2.CutsOff"/> both read the side of a chord by this, with the chord's ends in the same order,
        /// so the two give the same answer to the last bit.
        /// </remarks>
        internal static bool BeforeChord(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 point)
        {
            if (p1.Y == p2.Y)
            {
                return point.Y < p1.Y;
            }

            return point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X;
        }
    }
}
