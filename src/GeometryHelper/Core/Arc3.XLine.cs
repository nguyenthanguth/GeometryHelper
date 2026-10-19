using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in space asked of an arc and of a circle. The line is cut into the segment that reaches past the shape on
    // both sides (see XLine3) and asked as any segment is, with the tolerance passed through.
    static partial class Arc3
    {
        internal static bool CollidesWith(GeoArc3 arc, GeoXLine3 xline, Tolerance tolerance)
        {
            return CollidesWith(arc, XLine3.CutPast(xline, arc, tolerance), tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoArc3 arc, GeoXLine3 xline, Tolerance tolerance)
        {
            return GetIntersections(arc, XLine3.CutPast(xline, arc, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoArc3 arc, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(arc, XLine3.CutPast(xline, arc, tolerance), out intersections, tolerance);
        }

        internal static bool CollidesWith(GeoCircle3 circle, GeoXLine3 xline, Tolerance tolerance)
        {
            return CollidesWith(circle, XLine3.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoCircle3 circle, GeoXLine3 xline, Tolerance tolerance)
        {
            return GetIntersections(circle, XLine3.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoCircle3 circle, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(circle, XLine3.CutPast(xline, circle, tolerance), out intersections, tolerance);
        }
    }
}
