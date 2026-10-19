using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in the plane asked of an ellipse. The line is cut into the segment that reaches past the shape
    // on both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a point, a
    // ray or another line nothing is cut.
    static partial class Ellipse2
    {
        internal static bool CollidesWith(GeoEllipse2 ellipse, GeoXLine2 xline, Tolerance tolerance)
        {
            return CollidesWith(ellipse, XLine2.CutPast(xline, ellipse, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoEllipse2 ellipse, GeoXLine2 xline, Tolerance tolerance)
        {
            return DistanceTo(ellipse, XLine2.CutPast(xline, ellipse, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoEllipse2 ellipse, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetIntersections(ellipse, XLine2.CutPast(xline, ellipse, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoEllipse2 ellipse, GeoXLine2 xline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(ellipse, XLine2.CutPast(xline, ellipse, tolerance), out intersections, tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoEllipse2 ellipse, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetShortestLineTo(ellipse, XLine2.CutPast(xline, ellipse, tolerance), tolerance);
        }
    }
}
