using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // The shortest segment joining an infinite line in the plane to another shape. The line is cut into the segment
    // that reaches past the shape on both sides (see XLine2) and asked as any segment is, with the tolerance passed
    // through. Against a point, a ray or another line nothing is cut.
    static partial class Projection2
    {
        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoPoint2 point, Tolerance tolerance)
        {
            return new GeoLine2(XLine2.Project(xline, point), point);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoLine2 line, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine2.CutPast(xline, line, tolerance), line, tolerance);
        }

        // Where the line and the ray cross, the point where they do; otherwise from the foot of the origin of the ray on the line
        // to that origin, as Distance2 measures them.
        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoRay2 ray, Tolerance tolerance)
        {
            if (XLine2.TryCross(xline, ray, tolerance, false, out double alongLine, out _))
            {
                GeoPoint2 crossing = xline.GetPointAtDistance(alongLine);

                return new GeoLine2(crossing, crossing);
            }

            return new GeoLine2(XLine2.Project(xline, ray.Origin), ray.Origin);
        }

        // Two lines asked either way round give the same segment turned round, to the bit: it is worked out from the one that
        // comes first (see XLine2.Precedes) and turned round for the other.
        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline1, GeoXLine2 xline2, Tolerance tolerance)
        {
            return XLine2.Precedes(xline2, xline1)
                ? ShortestFromTheFirst(xline2, xline1, tolerance).Reverse()
                : ShortestFromTheFirst(xline1, xline2, tolerance);
        }

        private static GeoLine2 ShortestFromTheFirst(GeoXLine2 xline1, GeoXLine2 xline2, Tolerance tolerance)
        {
            if (XLine2.TryCross(xline1, xline2, tolerance, out double alongFirst))
            {
                GeoPoint2 crossing = xline1.GetPointAtDistance(alongFirst);

                return new GeoLine2(crossing, crossing);
            }

            // The two candidates Distance2.DistanceTo(GeoXLine2, GeoXLine2) weighs, the first kept on a tie.
            GeoLine2 first = new GeoLine2(XLine2.Project(xline1, xline2.Origin), xline2.Origin);
            GeoLine2 second = new GeoLine2(xline1.Origin, XLine2.Project(xline2, xline1.Origin));

            return second.Length < first.Length ? second : first;
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoArc2 arc, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine2.CutPast(xline, arc, tolerance), arc, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoCircle2 circle, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine2.CutPast(xline, circle, tolerance), circle, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetShortestLineTo(XLine2.CutPast(xline, poly, tolerance), poly, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(XLine2.CutPast(xline, loop, tolerance), loop, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetShortestLineTo(XLine2.CutPast(xline, polyline, tolerance), polyline, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(XLine2.CutPast(xline, chain, tolerance), chain, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoXLine2 xline, GeoRectangle2 rect, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine2.CutPast(xline, rect, tolerance), rect, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetShortestLineTo(circle, XLine2.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoXLine2 xline, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return GetShortestLineTo(poly, XLine2.CutPast(xline, poly, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoXLine2 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, XLine2.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoXLine2 xline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return GetShortestLineTo(polyline, XLine2.CutPast(xline, polyline, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoXLine2 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, XLine2.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetShortestLineTo(rect, XLine2.CutPast(xline, rect, tolerance), tolerance);
        }
    }
}
