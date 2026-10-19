using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in the plane asked of an arc. The line is cut into the segment that reaches past the shape on
    // both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a point, a ray
    // or another line nothing is cut.
    static partial class Arc2
    {
        internal static double DistanceTo(GeoArc2 arc, GeoXLine2 xline, Tolerance tolerance)
        {
            return DistanceTo(arc, XLine2.CutPast(xline, arc, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoArc2 arc, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetIntersections(arc, XLine2.CutPast(xline, arc, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoArc2 arc, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(arc, XLine2.CutPast(xline, arc, tolerance), out intersections, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoArc2 arc, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetShortestLineTo(arc, XLine2.CutPast(xline, arc, tolerance), tolerance);
        }
    }
}
