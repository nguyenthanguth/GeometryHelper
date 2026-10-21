using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Rounding the corner between two pieces of a chain when either of them curves.
    /// <para>
    /// A circle of radius r touching two curves has its centre exactly r from both, which is to say on
    /// each of those curves moved sideways by r. So the centre is where the two moved curves cross, and
    /// finding it needs nothing but the intersections the library already works out: a line moved sideways
    /// is a line, and an arc moved sideways is a circle about the same point.
    /// </para>
    /// <para>
    /// The arc has to run the way the pieces run and turn the way the corner turns, so its centre lies on
    /// the inside of the turn from both of them: each piece is moved towards that side only, never away
    /// from it. A circle moved off the other side touches the pieces too, but the arc on it runs back
    /// against them and leaves a cusp; at a corner turning 144.5° and a radius of 0.002, the nearest such
    /// circle touched the first piece 0.00064 past the corner. Of the crossings left, the one taken has
    /// both touching points on the pieces as they stand, neither of them past the corner, so that rounding
    /// shortens the pieces rather than stretching them, and of those the one nearest the corner.
    /// </para>
    /// <para>
    /// A corner that does not turn, by no more than <see cref="Tolerance.EqualAngleRad"/>, has no inside
    /// and nothing to round. That is the joint where a straight piece runs on into an arc it is tangent to,
    /// as along a bent bar, and it is left as it is rather than given an arc of no sweep.
    /// </para>
    /// </summary>
    internal static class ArcFillet2
    {
        /// <summary>
        /// Rounds the corner between two edges that meet, whether either of them curves or neither does.
        /// </summary>
        /// <param name="incoming">The edge running into the corner.</param>
        /// <param name="outgoing">The edge running out of it; it must start where the first one ends.</param>
        /// <param name="radius">The radius of the arc to put at the corner.</param>
        /// <param name="arc">The arc, when the method returns true.</param>
        /// <param name="trimmed1">The first edge cut back to where the arc touches it.</param>
        /// <param name="trimmed2">The second edge cut back to where the arc touches it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when a fillet of that radius fits on both edges; false when it does not, or when the
        /// corner does not turn.</returns>
        internal static bool TryFillet(
            GeoEdge2 incoming,
            GeoEdge2 outgoing,
            double radius,
            out GeoArc2 arc,
            out GeoEdge2 trimmed1,
            out GeoEdge2 trimmed2,
            Tolerance tolerance)
        {
            arc = default(GeoArc2);
            trimmed1 = incoming;
            trimmed2 = outgoing;

            GeoPoint2 corner = incoming.EndPoint;

            if (!corner.IsEqualTo(outgoing.StartPoint, tolerance))
            {
                return false;
            }

            // Which way the corner turns, read from the way the two pieces run where they meet.
            GeoVector2 arriving = Heading(incoming, true);
            GeoVector2 leaving = Heading(outgoing, false);
            double turn = arriving.CrossProduct(leaving);

            // A corner that does not turn has no inside to put an arc on: there would be nothing but an arc
            // of no sweep and a repeated vertex. One that turns straight back has no inside either.
            if (turn == 0.0 || Math.Atan2(Math.Abs(turn), arriving.DotProduct(leaving)) <= tolerance.EqualAngleRad)
            {
                return false;
            }

            int side = turn > 0.0 ? 1 : -1;

            var centres = new List<GeoPoint2>(2);

            Add(centres, incoming, Sideways(incoming, side * radius), outgoing, Sideways(outgoing, side * radius), tolerance);

            bool found = false;
            double nearest = double.MaxValue;

            foreach (GeoPoint2 centre in centres)
            {
                if (!Touches(incoming, centre, radius, true, tolerance, out GeoPoint2 first) ||
                    !Touches(outgoing, centre, radius, false, tolerance, out GeoPoint2 second))
                {
                    continue;
                }

                double reach = corner.GetDistanceSquaredTo(centre);

                if (reach >= nearest)
                {
                    continue;
                }

                // The arc runs from the first touching point to the second the way the corner turns, which,
                // with its centre on the inside of both pieces, is the way they run where it touches them.
                GeoVector2 toFirst = centre.GetVectorTo(first);
                GeoVector2 toSecond = centre.GetVectorTo(second);

                var candidate = new GeoArc2(
                    centre,
                    radius,
                    Math.Atan2(toFirst.Y, toFirst.X),
                    Math.Atan2(toSecond.Y, toSecond.X),
                    side < 0);

                // A sweep of half a turn or more is not taken: it is the circle met the wrong way round,
                // the second touching point before the first.
                if (Math.Abs(candidate.SweptAngle) >= Math.PI)
                {
                    continue;
                }

                nearest = reach;
                arc = candidate;
                trimmed1 = Upto(incoming, first, tolerance);
                trimmed2 = From(outgoing, second, tolerance);
                found = true;
            }

            return found;
        }

        /// <summary>
        /// Gets the direction an edge runs in at one of its ends, not made a unit.
        /// </summary>
        private static GeoVector2 Heading(GeoEdge2 edge, bool atEnd)
        {
            if (!edge.IsArc)
            {
                return edge.StartPoint.GetVectorTo(edge.EndPoint);
            }

            GeoArc2 arc = edge.ToArc();
            GeoVector2 outward = arc.Center.GetVectorTo(atEnd ? edge.EndPoint : edge.StartPoint);

            return arc.IsClockwise ? new GeoVector2(outward.Y, -outward.X) : new GeoVector2(-outward.Y, outward.X);
        }

        /// <summary>
        /// Gets how far to move an edge, as <see cref="Add"/> reads the move, to put it a distance to its left,
        /// or to its right when the distance is negative.
        /// </summary>
        /// <remarks>
        /// A straight edge is moved along its left-hand normal. An arc is moved by changing its radius, and its
        /// left is towards its centre when it runs counter-clockwise and away from it when it runs clockwise.
        /// </remarks>
        private static double Sideways(GeoEdge2 edge, double distance)
        {
            return edge.IsArc && !edge.ToArc().IsClockwise ? -distance : distance;
        }

        /// <summary>
        /// Works out where the two edges, each moved sideways, cross, and adds those points to a list.
        /// </summary>
        private static void Add(List<GeoPoint2> found, GeoEdge2 first, double moveFirst, GeoEdge2 second, double moveSecond, Tolerance tolerance)
        {
            if (first.IsArc)
            {
                GeoArc2 one = first.ToArc();
                double radius = one.Radius + moveFirst;

                if (radius <= tolerance.EqualPoint)
                {
                    return;
                }

                if (second.IsArc)
                {
                    GeoArc2 other = second.ToArc();
                    double otherRadius = other.Radius + moveSecond;

                    if (otherRadius > tolerance.EqualPoint)
                    {
                        Meet(found, one.Center, radius, other.Center, otherRadius, tolerance);
                    }

                    return;
                }

                Meet(found, one.Center, radius, Moved(second, moveSecond), tolerance);
                return;
            }

            if (second.IsArc)
            {
                GeoArc2 other = second.ToArc();
                double radius = other.Radius + moveSecond;

                if (radius > tolerance.EqualPoint)
                {
                    Meet(found, other.Center, radius, Moved(first, moveFirst), tolerance);
                }

                return;
            }

            Meet(found, Moved(first, moveFirst), Moved(second, moveSecond), tolerance);
        }

        /// <summary>
        /// Gets a straight edge moved sideways, as the endless line it lies on.
        /// </summary>
        private static GeoLine2 Moved(GeoEdge2 edge, double distance)
        {
            GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);

            along.TryGetNormal(out GeoVector2 unit);

            var sideways = new GeoVector2(-unit.Y * distance, unit.X * distance);

            return new GeoLine2(edge.StartPoint.Add(sideways), edge.EndPoint.Add(sideways));
        }

        /// <summary>
        /// Adds where two circles cross.
        /// </summary>
        private static void Meet(List<GeoPoint2> found, GeoPoint2 first, double firstRadius, GeoPoint2 second, double secondRadius, Tolerance tolerance)
        {
            GeoVector2 between = first.GetVectorTo(second);
            double apart = between.Length;

            if (apart <= tolerance.EqualPoint || apart > firstRadius + secondRadius || apart < Math.Abs(firstRadius - secondRadius))
            {
                return;
            }

            // Worked from the smaller circle, as Intersection2 does. From the circle of radius 2.5E8 behind an edge 100
            // long of bulge 1E-7, the half chord to a circle of radius 40 is the root of the difference of two squares
            // near 6E16 and came out 0.076 off, so a corner beside a nearly flat edge was left unrounded. The difference of the
            // squared radii is the product of their difference and their sum, as Intersection2 takes it.
            var unit = new GeoVector2(between.X / apart, between.Y / apart);

            if (secondRadius < firstRadius)
            {
                double back = ((secondRadius - firstRadius) * (secondRadius + firstRadius) + apart * apart) / (2.0 * apart);
                double remaining = secondRadius * secondRadius - back * back;
                double half = remaining <= 0.0 ? 0.0 : Math.Sqrt(remaining);
                double footX = second.X - unit.X * back;
                double footY = second.Y - unit.Y * back;

                found.Add(new GeoPoint2(footX - unit.Y * half, footY + unit.X * half));
                found.Add(new GeoPoint2(footX + unit.Y * half, footY - unit.X * half));

                return;
            }

            double along = ((firstRadius - secondRadius) * (firstRadius + secondRadius) + apart * apart) / (2.0 * apart);
            double square = firstRadius * firstRadius - along * along;
            double across = square <= 0.0 ? 0.0 : Math.Sqrt(square);

            GeoPoint2 middle = first.Add(unit.Multiply(along));

            found.Add(new GeoPoint2(middle.X - unit.Y * across, middle.Y + unit.X * across));
            found.Add(new GeoPoint2(middle.X + unit.Y * across, middle.Y - unit.X * across));
        }

        /// <summary>
        /// Adds where a circle crosses an endless line.
        /// </summary>
        private static void Meet(List<GeoPoint2> found, GeoPoint2 centre, double radius, GeoLine2 line, Tolerance tolerance)
        {
            GeoVector2 along = line.StartPoint.GetVectorTo(line.EndPoint);

            if (!along.TryGetNormal(out GeoVector2 unit))
            {
                return;
            }

            double reach = line.StartPoint.GetVectorTo(centre).DotProduct(unit);
            GeoPoint2 closest = line.StartPoint.Add(unit.Multiply(reach));

            double square = radius * radius - closest.GetDistanceSquaredTo(centre);

            if (square < -tolerance.EqualPoint)
            {
                return;
            }

            double across = square <= 0.0 ? 0.0 : Math.Sqrt(square);

            found.Add(closest.Add(unit.Multiply(across)));
            found.Add(closest.Add(unit.Multiply(-across)));
        }

        /// <summary>
        /// Adds where two endless lines cross.
        /// </summary>
        private static void Meet(List<GeoPoint2> found, GeoLine2 first, GeoLine2 second, Tolerance tolerance)
        {
            if (Intersection2.TryIntersectWith(first, second, Enums.LineExtension.Both, out GeoPoint2 meeting, tolerance))
            {
                found.Add(meeting);
            }
        }

        /// <summary>
        /// Determines whether a circle of that radius about a point touches an edge, and where.
        /// </summary>
        /// <remarks>
        /// The touching point has to be on the edge as it stands rather than beyond either of its ends,
        /// because rounding a corner shortens the pieces that meet there; one that would have to be
        /// stretched is not an answer here. Past the corner it gets no tolerance at all, only the rounding
        /// of the arithmetic: a piece run on past the corner and then met by an arc turning back is a cusp,
        /// however short the run.
        /// </remarks>
        private static bool Touches(GeoEdge2 edge, GeoPoint2 centre, double radius, bool cornerAtEnd, Tolerance tolerance, out GeoPoint2 at)
        {
            at = default(GeoPoint2);

            if (edge.IsArc)
            {
                GeoArc2 arc = edge.ToArc();
                GeoVector2 outward = arc.Center.GetVectorTo(centre);

                if (!outward.TryGetNormal(out GeoVector2 unit))
                {
                    return false;
                }

                at = arc.Center.Add(unit.Multiply(arc.Radius));
            }
            else
            {
                GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);

                if (!along.TryGetNormal(out GeoVector2 unit))
                {
                    return false;
                }

                double reach = edge.StartPoint.GetVectorTo(centre).DotProduct(unit);

                at = edge.StartPoint.Add(unit.Multiply(reach));
            }

            // It has to be a touch rather than a crossing, and it has to land on the piece itself.
            return Math.Abs(centre.DistanceTo(at) - radius) <= Math.Max(tolerance.EqualPoint, radius * 1E-9)
                && edge.DistanceTo(at, tolerance) <= tolerance.EqualPoint
                && !PastCorner(edge, at, cornerAtEnd);
        }

        /// <summary>
        /// Determines whether a point on an edge, or on the line or circle it lies on, is beyond the end of
        /// it where the corner is, by more than the rounding of the arithmetic.
        /// </summary>
        private static bool PastCorner(GeoEdge2 edge, GeoPoint2 at, bool cornerAtEnd)
        {
            GeoPoint2 corner = cornerAtEnd ? edge.EndPoint : edge.StartPoint;
            double rounding = 1E-12 * (1.0 + Math.Abs(corner.X) + Math.Abs(corner.Y) + edge.Length);

            if (!edge.IsArc)
            {
                GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);
                double beyond = corner.GetVectorTo(at).DotProduct(along) / along.Length;

                return cornerAtEnd ? beyond > rounding : beyond < -rounding;
            }

            // On the circle of an arc, the angle from the corner, counted the way the arc runs: the arc
            // itself lies behind the corner when it ends there and ahead of it when it starts there. Of
            // the rest of the circle, the half nearer the corner is past it.
            GeoArc2 arc = edge.ToArc();
            GeoVector2 toCorner = arc.Center.GetVectorTo(corner);
            GeoVector2 toPoint = arc.Center.GetVectorTo(at);

            double angle = Math.Atan2(toCorner.CrossProduct(toPoint), toCorner.DotProduct(toPoint));

            if (arc.IsClockwise)
            {
                angle = -angle;
            }

            double halfGap = (Math.PI * 2.0 - Math.Abs(arc.SweptAngle)) * 0.5;
            double slack = rounding / arc.Radius;

            return cornerAtEnd
                ? angle > slack && angle < halfGap
                : angle < -slack && angle > -halfGap;
        }

        /// <summary>
        /// Gets how much of an edge a fillet touching it at a point takes, measured along the edge from the
        /// corner, and read on past the far end when the point lies beyond it.
        /// </summary>
        /// <remarks>
        /// The far end may be overrun by up to the tolerance, and the piece left then runs backwards: its length
        /// says how far, but not which way. A fillet touching a side 10 long at 0.04 beyond its far end takes
        /// 10.04 of it, not the 9.96 the length of what is left would say.
        /// </remarks>
        internal static double Taken(GeoEdge2 edge, GeoPoint2 at, bool cornerAtEnd)
        {
            GeoPoint2 corner = cornerAtEnd ? edge.EndPoint : edge.StartPoint;

            if (!edge.IsArc)
            {
                GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);
                double ahead = corner.GetVectorTo(at).DotProduct(along) / along.Length;

                return cornerAtEnd ? -ahead : ahead;
            }

            // As in PastCorner: the angle from the corner, the way the arc runs, with the far half of the rest
            // of the circle read as lying beyond the far end.
            GeoArc2 arc = edge.ToArc();
            GeoVector2 toCorner = arc.Center.GetVectorTo(corner);
            GeoVector2 toPoint = arc.Center.GetVectorTo(at);

            double angle = Math.Atan2(toCorner.CrossProduct(toPoint), toCorner.DotProduct(toPoint));

            if (arc.IsClockwise)
            {
                angle = -angle;
            }

            double halfGap = (Math.PI * 2.0 - Math.Abs(arc.SweptAngle)) * 0.5;

            if (cornerAtEnd)
            {
                return -(angle > halfGap ? angle - Math.PI * 2.0 : angle) * arc.Radius;
            }

            return (angle < -halfGap ? angle + Math.PI * 2.0 : angle) * arc.Radius;
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
    }
}
