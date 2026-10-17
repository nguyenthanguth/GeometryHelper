using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether a straight shape touches or overlaps one that curves.
    /// </summary>
    /// <remarks>
    /// The rule is the one used everywhere else in this class: two shapes collide when their boundaries
    /// meet, or when one lies inside the other and the two never cross at all. A polygon, a rectangle and
    /// a circle are closed and so have an inside; a segment, a polyline and an arc do not, so for those
    /// nothing but a meeting counts. Two shapes meet where they cross or come within the point tolerance of
    /// each other, measured edge by edge along the arcs.
    /// </remarks>
    public static partial class Collision2
    {
        /// <summary>
        /// Determines whether a segment touches an arc.
        /// </summary>
        public static bool CollidesWith(GeoLine2 line, GeoArc2 arc) => CollidesWith(line, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each
        /// other, as two segments do. At a tolerance of a thousandth, a segment ending half a thousandth short of
        /// an arc collides with it, and one ending two thousandths short does not.
        /// </remarks>
        public static bool CollidesWith(GeoLine2 line, GeoArc2 arc, Tolerance tolerance)
        {
            // The distance is nought where they cross.
            return !Clear(line, arc, tolerance) && Arc2.DistanceTo(arc, line, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether an arc touches another arc.
        /// </summary>
        public static bool CollidesWith(GeoArc2 first, GeoArc2 second) => CollidesWith(first, second, Tolerance.Global);

        /// <summary>
        /// Determines whether an arc touches another arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each
        /// other. At a tolerance of a thousandth, an arc ending half a thousandth above the top of the circle of
        /// radius 50 about the origin collides with its upper half, and one ending two thousandths above does not.
        /// They collide exactly where <see cref="Arc2.DistanceTo(GeoArc2, GeoArc2, Tolerance)"/> is at most the point
        /// tolerance, also for arcs of a radius smaller than it: two arcs of radius 0.0004 about the origin, one of them
        /// 1E-7 outside the other, collide where their spans overlap. Two arcs of one circle cross nowhere, but an overlap
        /// of them always begins and ends at an end of one, which the nearest pair weighs.
        /// </remarks>
        public static bool CollidesWith(GeoArc2 first, GeoArc2 second, Tolerance tolerance)
        {
            // Two arcs come no nearer than their whole circles do, so circles more than the point tolerance apart, one
            // beside the other or one inside the other, settle it without measuring.
            double apart = Distance2.DistanceTo(first.Center, second.Center);

            if (apart > first.Radius + second.Radius + tolerance.EqualPoint
                || apart < Math.Abs(first.Radius - second.Radius) - tolerance.EqualPoint)
            {
                return false;
            }

            // The distance is nought where they cross.
            return Arc2.DistanceTo(first, second, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a circle touches or overlaps an arc.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoArc2 arc) => CollidesWith(circle, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a circle touches or overlaps an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A circle is read as a filled disc, so an arc lying wholly within one collides with it, and so does one
        /// coming within the point tolerance of its rim from outside.
        /// </remarks>
        public static bool CollidesWith(GeoCircle2 circle, GeoArc2 arc, Tolerance tolerance)
        {
            // The distance to the disc is nought for an arc inside it or crossing its rim; the crossings of two circles are
            // read with a band of their own, which lets in a touch past the tolerance.
            return Arc2.DistanceTo(arc, circle, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a polygon touches or overlaps an arc.
        /// </summary>
        public static bool CollidesWith(GeoPolygon2 poly, GeoArc2 arc) => CollidesWith(poly, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon touches or overlaps an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An arc lying wholly inside the polygon crosses none of its edges, so where it starts is asked
        /// as well. Outside, an arc coming within the point tolerance of an edge collides with it.
        /// </remarks>
        public static bool CollidesWith(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return CrossesOrStartsIn(poly, arc, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(poly), arc, tolerance);
        }

        /// <summary>
        /// Whether an arc crosses the boundary of a polygon or starts inside it: where the distance between them is
        /// taken as nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            return Intersection2.GetIntersections(poly, arc, tolerance).Length > 0
                || Containment2.Contains(poly, arc.StartPoint, tolerance);
        }

        /// <summary>
        /// Determines whether a polyline touches an arc.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoArc2 arc) => CollidesWith(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a polyline touches an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each other.
        /// </remarks>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoArc2 arc, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Intersection2.GetIntersections(polyline, arc, tolerance).Length > 0
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(polyline), arc, tolerance);
        }

        /// <summary>
        /// Determines whether a rectangle touches or overlaps an arc.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoArc2 arc) => CollidesWith(rect, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a rectangle touches or overlaps an arc, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoArc2 arc, Tolerance tolerance)
        {
            return CollidesWith(rect.ToPolygon(), arc, tolerance);
        }

        /// <summary>
        /// Determines whether a rectangle touches or overlaps a loop that may curve.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolygonArc2 loop) => CollidesWith(rect, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether a rectangle touches or overlaps a loop that may curve, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CollidesWith(loop, rect.ToPolygon(), tolerance);
        }

        /// <summary>
        /// Determines whether a rectangle touches or overlaps a chain that may curve.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolylineArc2 chain) => CollidesWith(rect, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether a rectangle touches or overlaps a chain that may curve, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CollidesWith(chain, rect.ToPolygon(), tolerance);
        }
    }
}
