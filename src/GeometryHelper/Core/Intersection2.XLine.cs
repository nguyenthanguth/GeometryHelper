using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Where an infinite line in the plane crosses another shape. The line is cut into the segment that reaches past
    // the shape on both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a
    // point, a ray or another line nothing is cut.
    static partial class Intersection2
    {
        internal static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetIntersections(circle, XLine2.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoXLine2 xline, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetIntersections(poly, XLine2.CutPast(xline, poly, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoXLine2 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetIntersections(loop, XLine2.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoXLine2 xline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetIntersections(polyline, XLine2.CutPast(xline, polyline, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoXLine2 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetIntersections(chain, XLine2.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetIntersections(rect, XLine2.CutPast(xline, rect, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoLine2 line, GeoXLine2 xline, out GeoPoint2 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(line, XLine2.CutPast(xline, line, tolerance), out intersection, tolerance);
        }

        // Where a line crosses a ray, placed on the ray and never behind its origin. Parallel is read by the angle alone, as for two
        // rays, and a crossing up to the point tolerance behind the origin of the ray counts. Both sides ask this one.
        internal static bool TryIntersectWith(GeoRay2 ray, GeoXLine2 xline, out GeoPoint2 intersection, Tolerance tolerance)
        {
            intersection = new GeoPoint2(0, 0);

            // Both directions are of unit length, so their cross product is the sine of the angle between them; a NaN is
            // parallel to everything.
            double sine = xline.Direction.CrossProduct(ray.Direction);

            if (!(Math.Abs(sine) > tolerance.EqualAngleSin) || !XLine2.TryCross(xline, ray, tolerance, true, out _, out double alongRay))
            {
                return false;
            }

            intersection = ray.GetPointAtDistance(Math.Max(0.0, alongRay));
            return true;
        }

        // Where two lines cross, placed on the one that comes first (see XLine2.Precedes), so that asked either way round they give
        // the same point to the bit. Two lines parallel by angle are refused, even where they are the same line.
        internal static bool TryIntersectWith(GeoXLine2 xline1, GeoXLine2 xline2, out GeoPoint2 intersection, Tolerance tolerance)
        {
            intersection = new GeoPoint2(0, 0);

            bool swap = XLine2.Precedes(xline2, xline1);
            GeoXLine2 first = swap ? xline2 : xline1;
            GeoXLine2 second = swap ? xline1 : xline2;
            double sine = first.Direction.CrossProduct(second.Direction);

            if (!(Math.Abs(sine) > tolerance.EqualAngleSin) || !XLine2.TryCross(first, second, tolerance, out double alongFirst))
            {
                return false;
            }

            intersection = first.GetPointAtDistance(alongFirst);
            return true;
        }

        internal static bool TryIntersectWith(GeoCircle2 circle, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(circle, XLine2.CutPast(xline, circle, tolerance), out intersections, tolerance);
        }

        internal static bool TryIntersectWith(GeoPolygonArc2 loop, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return TryIntersectWith(loop, XLine2.CutPast(xline, loop, tolerance), out intersections, tolerance);
        }

        internal static bool TryIntersectWith(GeoPolyline2 polyline, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return TryIntersectWith(polyline, XLine2.CutPast(xline, polyline, tolerance), out intersections, tolerance);
        }

        internal static bool TryIntersectWith(GeoPolylineArc2 chain, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return TryIntersectWith(chain, XLine2.CutPast(xline, chain, tolerance), out intersections, tolerance);
        }
    }
}
