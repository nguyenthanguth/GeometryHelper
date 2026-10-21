using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where the endless curves behind two edges cross: the whole line a segment lies on, and the whole
    /// circle an arc was cut from.
    /// <para>
    /// The public intersections stop at the ends of the pieces they are asked about, which is what a caller
    /// almost always wants. Two things here want the opposite: rounding a corner, where the centre of the
    /// fillet lies on the curves moved sideways whether or not the pieces reach that far, and mitering one,
    /// where the pieces are run on until they meet. Both are about the curves rather than the pieces.
    /// </para>
    /// </summary>
    internal static class CurveMeet2
    {
        /// <summary>
        /// Gets where the endless curves behind two edges cross, each moved sideways by a distance first.
        /// </summary>
        /// <param name="first">The first edge.</param>
        /// <param name="moveFirst">How far to move the first curve sideways, to the left of the way it runs when positive.</param>
        /// <param name="second">The second edge.</param>
        /// <param name="moveSecond">How far to move the second curve sideways.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every crossing, which may be none, one or two.</returns>
        internal static List<GeoPoint2> Where(GeoEdge2 first, double moveFirst, GeoEdge2 second, double moveSecond, Tolerance tolerance)
        {
            var found = new List<GeoPoint2>();

            bool firstCurves = first.IsArc;
            bool secondCurves = second.IsArc;

            if (firstCurves && secondCurves)
            {
                if (TryCircle(first, moveFirst, tolerance, out GeoPoint2 oneCentre, out double oneRadius) &&
                    TryCircle(second, moveSecond, tolerance, out GeoPoint2 otherCentre, out double otherRadius))
                {
                    Circles(found, oneCentre, oneRadius, otherCentre, otherRadius, tolerance);
                }

                return found;
            }

            if (firstCurves || secondCurves)
            {
                GeoEdge2 curved = firstCurves ? first : second;
                GeoEdge2 straight = firstCurves ? second : first;
                double moveCurved = firstCurves ? moveFirst : moveSecond;
                double moveStraight = firstCurves ? moveSecond : moveFirst;

                if (TryCircle(curved, moveCurved, tolerance, out GeoPoint2 centre, out double radius) &&
                    TryLine(straight, moveStraight, out GeoLine2 line))
                {
                    CircleAndLine(found, centre, radius, line, tolerance);
                }

                return found;
            }

            if (TryLine(first, moveFirst, out GeoLine2 one) && TryLine(second, moveSecond, out GeoLine2 other))
            {
                Lines(found, one, other, tolerance);
            }

            return found;
        }

        /// <summary>
        /// Gets the endless line a straight edge lies on, moved sideways.
        /// </summary>
        internal static bool TryLine(GeoEdge2 edge, double distance, out GeoLine2 line)
        {
            line = default(GeoLine2);

            GeoVector2 along = edge.StartPoint.GetVectorTo(edge.EndPoint);

            if (!along.TryGetNormal(out GeoVector2 unit))
            {
                return false;
            }

            var sideways = new GeoVector2(-unit.Y * distance, unit.X * distance);

            line = new GeoLine2(edge.StartPoint.Add(sideways), edge.EndPoint.Add(sideways));
            return true;
        }

        /// <summary>
        /// Gets the whole circle an arc was cut from, moved sideways.
        /// </summary>
        /// <remarks>
        /// Moving an arc sideways to the left means toward its centre when it runs counter-clockwise and
        /// away from it when it runs clockwise, which is the same rule the offset itself follows.
        /// </remarks>
        internal static bool TryCircle(GeoEdge2 edge, double distance, Tolerance tolerance, out GeoPoint2 centre, out double radius)
        {
            centre = default(GeoPoint2);
            radius = 0.0;

            if (!edge.IsArc)
            {
                return false;
            }

            GeoArc2 arc = edge.ToArc();

            centre = arc.Center;
            radius = arc.Radius - Math.Sign(arc.SweptAngle) * distance;

            return radius > tolerance.EqualPoint;
        }

        /// <summary>
        /// Adds where two endless lines cross.
        /// </summary>
        /// <remarks>
        /// Only lines exactly parallel never cross. Two that are a hair off parallel cross far away, and how far is
        /// for the caller to judge: a miter's limit says whether it is too far. They were taken as parallel within the
        /// angle tolerance, so the loop (0, 0), (50, 0), (100, 0.698), (100, 50), (0, 50), turning 0.8 degrees at
        /// (50, 0), grown by 80 had no miter there, though it lies at (50.558, -80), 80.002 from the corner. A line
        /// drawn no longer than the point tolerance has no direction to cross by.
        /// </remarks>
        internal static void Lines(List<GeoPoint2> found, GeoLine2 first, GeoLine2 second, Tolerance tolerance)
        {
            GeoVector2 along = first.Direction;
            GeoVector2 other = second.Direction;

            if (along.Length <= tolerance.EqualPoint || other.Length <= tolerance.EqualPoint)
            {
                return;
            }

            double turn = along.CrossProduct(other);

            if (turn == 0.0)
            {
                return;
            }

            // Worked as Intersection2 works it, so that lines it would have crossed meet at the same point to the bit.
            double parameter = first.StartPoint.GetVectorTo(second.StartPoint).CrossProduct(other) / turn;

            found.Add(first.GetPointAtParameter(parameter));
        }

        /// <summary>
        /// Adds where a circle crosses an endless line.
        /// </summary>
        internal static void CircleAndLine(List<GeoPoint2> found, GeoPoint2 centre, double radius, GeoLine2 line, Tolerance tolerance)
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

            if (across > 0.0)
            {
                found.Add(closest.Add(unit.Multiply(-across)));
            }
        }

        /// <summary>
        /// Adds where two circles cross.
        /// </summary>
        internal static void Circles(List<GeoPoint2> found, GeoPoint2 first, double firstRadius, GeoPoint2 second, double secondRadius, Tolerance tolerance)
        {
            GeoVector2 between = first.GetVectorTo(second);
            double apart = between.Length;

            if (apart <= tolerance.EqualPoint || apart > firstRadius + secondRadius || apart < Math.Abs(firstRadius - secondRadius))
            {
                return;
            }

            // Worked from the smaller circle, as Intersection2 does. From the circle of radius 2.5E8 behind an edge 100
            // long of bulge 1E-7, the half chord to a circle of radius 40 is the root of the difference of two squares
            // near 6E16 and came out 0.076 off, so a miter beside a nearly flat edge was run on to the wrong point. The
            // difference of the squared radii is the product of their difference and their sum, as Intersection2 takes it.
            var unit = new GeoVector2(between.X / apart, between.Y / apart);

            if (secondRadius < firstRadius)
            {
                double back = ((secondRadius - firstRadius) * (secondRadius + firstRadius) + apart * apart) / (2.0 * apart);
                double remaining = secondRadius * secondRadius - back * back;
                double half = remaining <= 0.0 ? 0.0 : Math.Sqrt(remaining);
                double footX = second.X - unit.X * back;
                double footY = second.Y - unit.Y * back;

                found.Add(new GeoPoint2(footX - unit.Y * half, footY + unit.X * half));

                if (half > 0.0)
                {
                    found.Add(new GeoPoint2(footX + unit.Y * half, footY - unit.X * half));
                }

                return;
            }

            double along = ((firstRadius - secondRadius) * (firstRadius + secondRadius) + apart * apart) / (2.0 * apart);
            double square = firstRadius * firstRadius - along * along;
            double across = square <= 0.0 ? 0.0 : Math.Sqrt(square);

            GeoPoint2 middle = first.Add(unit.Multiply(along));

            found.Add(new GeoPoint2(middle.X - unit.Y * across, middle.Y + unit.X * across));

            if (across > 0.0)
            {
                found.Add(new GeoPoint2(middle.X + unit.Y * across, middle.Y - unit.X * across));
            }
        }

        /// <summary>
        /// Gets an edge run on, or cut back, to a point on the endless curve behind it.
        /// </summary>
        /// <param name="edge">The edge to stretch.</param>
        /// <param name="at">The point to stretch it to; it must lie on the curve the edge lies on.</param>
        /// <param name="toEnd">true to move the end of the edge, false to move its start.</param>
        /// <returns>The stretched edge, drawn along the same line or the same circle.</returns>
        /// <remarks>
        /// A segment is stretched by moving one of its ends. An arc is stretched by sweeping further round
        /// the circle it was cut from, which changes its bulge and leaves its radius alone.
        /// </remarks>
        internal static GeoEdge2 Stretch(GeoEdge2 edge, GeoPoint2 at, bool toEnd)
        {
            if (!edge.IsArc)
            {
                return toEnd ? new GeoEdge2(edge.StartPoint, at) : new GeoEdge2(at, edge.EndPoint);
            }

            GeoArc2 arc = edge.ToArc();
            bool counterClockwise = arc.SweptAngle > 0.0;

            double from = toEnd ? arc.StartAngle : AngleOf(arc, at);
            double to = toEnd ? AngleOf(arc, at) : arc.StartAngle + arc.SweptAngle;

            double swept = Swept(from, to, counterClockwise);

            return toEnd
                ? new GeoEdge2(edge.StartPoint, at, Math.Tan(swept * 0.25))
                : new GeoEdge2(at, edge.EndPoint, Math.Tan(swept * 0.25));
        }

        /// <summary>
        /// Gets the angle of a point about the centre of an arc.
        /// </summary>
        private static double AngleOf(GeoArc2 arc, GeoPoint2 point)
        {
            return Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        }

        /// <summary>
        /// Gets the angle swept from one angle to another, going the way asked for.
        /// </summary>
        private static double Swept(double from, double to, bool counterClockwise)
        {
            double full = Math.PI * 2.0;
            double swept = (to - from) % full;

            if (swept < 0.0)
            {
                swept += full;
            }

            return counterClockwise ? swept : swept - full;
        }
    }
}
