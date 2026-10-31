using System;
using System.Runtime.CompilerServices;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Whether a side of a curved chain or loop touches another shape, as the chains walk their sides: a bend as it is, and a
    // straight side as the side it is however short. Its edge on its own is a segment you give, and one no longer than the
    // point tolerance is read as its start (see Stub3), so a chain was read as touching or clear by the start of its short
    // side. A side longer than the tolerance is asked as its edge is, and keeps its answer to the bit; the test for a short
    // side is written at each walk, so that the long ones pay no more than it.
    internal static partial class ArcChain3
    {
        /// <summary>
        /// Determines whether a side is straight and no longer than the point tolerance, by the measure <see cref="Gap3"/>
        /// takes: one its edge on its own reads as its start.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsShortStraight(GeoEdge3 side, Tolerance tolerance)
        {
            GeoPoint3 from = side.StartPoint;
            GeoPoint3 to = side.EndPoint;
            double x = to.X - from.X;
            double y = to.Y - from.Y;
            double z = to.Z - from.Z;

            return x * x + y * y + z * z <= tolerance.EqualPoint * tolerance.EqualPoint && !side.IsArc;
        }

        /// <summary>
        /// Determines whether a short straight side touches a triangle, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoTriangle3 triangle, Tolerance tolerance)
            => Gap3.Segment(Gap3.Region.Of(triangle), side.StartPoint, side.EndPoint, false, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches a polygon, read filled, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoPolygon3 polygon, Tolerance tolerance)
            => Gap3.Segment(Gap3.Region.Of(polygon ?? throw new ArgumentNullException(nameof(polygon))), side.StartPoint, side.EndPoint, false, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches the material of a face, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoFace3 face, Tolerance tolerance)
            => Gap3.Segment(Gap3.Region.Of(face ?? throw new ArgumentNullException(nameof(face))), side.StartPoint, side.EndPoint, false, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches an oriented box, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoObb3 box, Tolerance tolerance)
            => new Gap3.Block(box ?? throw new ArgumentNullException(nameof(box))).Segment(side.StartPoint, side.EndPoint, false, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches an axis-aligned box, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoAabb3 box, Tolerance tolerance)
            => !box.IsEmpty && ShortTouches(side, box.ToObb(), tolerance);

        /// <summary>
        /// Determines whether a short straight side touches a solid, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoSolid3 solid, Tolerance tolerance)
            => Collision3.CollidesWith(side.ToLine(), solid, false, tolerance);

        /// <summary>
        /// Determines whether a short straight side touches an arc, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoArc3 arc, Tolerance tolerance)
            => Gap3.Curve.Of(arc).GapToSegment(side.StartPoint, side.EndPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches the rim of a circle, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoCircle3 circle, Tolerance tolerance)
            => ShortTouches(side, Arc3.AsArc(circle), tolerance);

        /// <summary>
        /// Determines whether a short straight side touches a segment you give, the side measured as it is and the segment
        /// read as <see cref="Stub3"/> reads one.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoLine3 line, Tolerance tolerance)
            => Gap3.Segments(side.StartPoint, side.EndPoint, false, line.StartPoint, line.EndPoint, true, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a short straight side touches a ray, measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoRay3 ray, Tolerance tolerance)
        {
            // The ray as far as the far end of the side, past which it only draws away, as a ray is measured to a segment.
            double reach = Math.Max(ray.Origin.DistanceTo(side.StartPoint), ray.Origin.DistanceTo(side.EndPoint));

            return Gap3.Segments(ray.Origin, Gap3.RayEnd(ray, reach), false, side.StartPoint, side.EndPoint, false, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether two sides touch, one or both straight and short, each measured as it is.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShortTouches(GeoEdge3 side, GeoEdge3 other, Tolerance tolerance)
        {
            if (side.IsArc || other.IsArc)
            {
                GeoEdge3 straight = side.IsArc ? other : side;

                return Gap3.Curve.Of((side.IsArc ? side : other).ToArc()).GapToSegment(straight.StartPoint, straight.EndPoint) <= tolerance.EqualPoint;
            }

            return Gap3.Segments(side.StartPoint, side.EndPoint, false, other.StartPoint, other.EndPoint, false, tolerance) <= tolerance.EqualPoint;
        }
    }
}
