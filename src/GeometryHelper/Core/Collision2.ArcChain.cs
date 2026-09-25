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
    /// nothing but a meeting counts.
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
        /// Neither has an inside, so they collide only where they cross.
        /// </remarks>
        public static bool CollidesWith(GeoLine2 line, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.TryIntersectWith(arc, line, out _, tolerance);
        }

        /// <summary>
        /// Determines whether a circle touches or overlaps an arc.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoArc2 arc) => CollidesWith(circle, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a circle touches or overlaps an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A circle is read as a filled disc, so an arc lying wholly within one collides with it.
        /// </remarks>
        public static bool CollidesWith(GeoCircle2 circle, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.TryIntersectWith(arc, circle, out _, tolerance)
                || Containment2.Contains(circle, arc.StartPoint, tolerance);
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
        /// as well.
        /// </remarks>
        public static bool CollidesWith(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

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
        /// Neither has an inside, so they collide only where they cross.
        /// </remarks>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoArc2 arc, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Intersection2.GetIntersections(polyline, arc, tolerance).Length > 0;
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
