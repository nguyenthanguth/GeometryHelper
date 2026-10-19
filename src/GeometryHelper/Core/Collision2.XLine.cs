using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Whether an infinite line in the plane touches another shape. The line is cut into the segment that reaches past
    // the shape on both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a
    // point, a ray or another line nothing is cut.
    static partial class Collision2
    {
        internal static bool CollidesWith(GeoXLine2 xline, GeoArc2 arc, Tolerance tolerance)
        {
            return CollidesWith(XLine2.CutPast(xline, arc, tolerance), arc, tolerance);
        }

        internal static bool CollidesWith(GeoCircle2 circle, GeoXLine2 xline, Tolerance tolerance)
        {
            return CollidesWith(circle, XLine2.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoPolygon2 poly, GeoXLine2 xline, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return CollidesWith(poly, XLine2.CutPast(xline, poly, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoPolygonArc2 loop, GeoXLine2 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CollidesWith(loop, XLine2.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoPolyline2 polyline, GeoXLine2 xline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return CollidesWith(polyline, XLine2.CutPast(xline, polyline, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoPolylineArc2 chain, GeoXLine2 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CollidesWith(chain, XLine2.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoRectangle2 rect, GeoXLine2 xline, Tolerance tolerance)
        {
            return CollidesWith(rect, XLine2.CutPast(xline, rect, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoXLine2 xline, GeoLine2 line, Tolerance tolerance)
        {
            return CollidesWith(XLine2.CutPast(xline, line, tolerance), line, tolerance);
        }

        internal static bool CollidesWith(GeoLine2 line, GeoXLine2 xline, Tolerance tolerance)
        {
            return CollidesWith(line, XLine2.CutPast(xline, line, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoXLine2 xline, GeoRay2 ray, Tolerance tolerance)
        {
            return Distance2.DistanceTo(xline, ray, tolerance) <= tolerance.EqualPoint;
        }

        internal static bool CollidesWith(GeoXLine2 xline1, GeoXLine2 xline2, Tolerance tolerance)
        {
            return Distance2.DistanceTo(xline1, xline2, tolerance) <= tolerance.EqualPoint;
        }
    }
}
