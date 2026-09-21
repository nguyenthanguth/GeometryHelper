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
    /// Moving each curve either way gives four crossings to choose between, and more than one of them can
    /// be a real answer. The one taken is the one whose two touching points both lie on the pieces as they
    /// stand, so that rounding shortens them rather than stretching them, and of those the one nearest the
    /// corner.
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
        /// <returns>true when a fillet of that radius fits on both edges; otherwise, false.</returns>
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

            bool found = false;
            double nearest = double.MaxValue;

            foreach (GeoPoint2 centre in Centres(incoming, outgoing, radius, tolerance))
            {
                if (!Touches(incoming, centre, radius, tolerance, out GeoPoint2 first) ||
                    !Touches(outgoing, centre, radius, tolerance, out GeoPoint2 second))
                {
                    continue;
                }

                double reach = corner.GetDistanceSquaredTo(centre);

                if (reach >= nearest)
                {
                    continue;
                }

                // The arc runs from the first touching point to the second, the short way round, which is
                // the way that stays inside the corner.
                GeoVector2 toFirst = centre.GetVectorTo(first);
                GeoVector2 toSecond = centre.GetVectorTo(second);

                var candidate = new GeoArc2(
                    centre,
                    radius,
                    Math.Atan2(toFirst.Y, toFirst.X),
                    Math.Atan2(toSecond.Y, toSecond.X),
                    toFirst.CrossProduct(toSecond) < 0.0);

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
        /// Gets every point that could be the centre of a fillet of that radius between the two edges.
        /// </summary>
        private static IEnumerable<GeoPoint2> Centres(GeoEdge2 first, GeoEdge2 second, double radius, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            foreach (int sideOfFirst in new[] { 1, -1 })
            {
                foreach (int sideOfSecond in new[] { 1, -1 })
                {
                    Add(found, first, sideOfFirst * radius, second, sideOfSecond * radius, tolerance);
                }
            }

            return found;
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

            double along = (firstRadius * firstRadius - secondRadius * secondRadius + apart * apart) / (2.0 * apart);
            double square = firstRadius * firstRadius - along * along;
            double across = square <= 0.0 ? 0.0 : Math.Sqrt(square);

            var unit = new GeoVector2(between.X / apart, between.Y / apart);
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
        /// stretched is not an answer here.
        /// </remarks>
        private static bool Touches(GeoEdge2 edge, GeoPoint2 centre, double radius, Tolerance tolerance, out GeoPoint2 at)
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
                && edge.DistanceTo(at, tolerance) <= tolerance.EqualPoint;
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
