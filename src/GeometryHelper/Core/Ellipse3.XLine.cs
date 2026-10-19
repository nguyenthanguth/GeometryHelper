using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in space asked of an ellipse. The line is cut into the segment that reaches past the shape on both sides
    // (see XLine3) and asked as any segment is, with the tolerance passed through.
    static partial class Ellipse3
    {
        internal static bool CollidesWith(GeoEllipse3 ellipse, GeoXLine3 xline, Tolerance tolerance)
        {
            return CollidesWith(ellipse, XLine3.CutPast(xline, ellipse, tolerance), tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoEllipse3 ellipse, GeoXLine3 xline, Tolerance tolerance)
        {
            return GetIntersections(ellipse, XLine3.CutPast(xline, ellipse, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoEllipse3 ellipse, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(ellipse, XLine3.CutPast(xline, ellipse, tolerance), out intersections, tolerance);
        }
    }
}
