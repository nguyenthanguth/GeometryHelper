using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in the plane asked of a triangle. The line is cut into the segment that reaches past the shape
    // on both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a point, a
    // ray or another line nothing is cut.
    static partial class Triangle2
    {
        internal static bool CollidesWith(GeoTriangle2 triangle, GeoXLine2 xline, Tolerance tolerance)
        {
            return CollidesWith(triangle, XLine2.CutPast(xline, triangle, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoTriangle2 triangle, GeoXLine2 xline, Tolerance tolerance)
        {
            // DistanceTo(triangle, line) read with the tolerance passed down, which it takes from the global one.
            return TryAsPolygonByItsSides(triangle, out GeoPolygon2 shape, out GeoLine2 hull)
                ? Distance2.DistanceTo(shape, xline, tolerance)
                : Distance2.DistanceTo(hull, XLine2.CutPast(xline, triangle, tolerance), tolerance);
        }

        internal static GeoPoint2[] GetIntersections(GeoTriangle2 triangle, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetIntersections(triangle, XLine2.CutPast(xline, triangle, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoTriangle2 triangle, GeoXLine2 xline, Tolerance tolerance)
        {
            return GetShortestLineTo(triangle, XLine2.CutPast(xline, triangle, tolerance), tolerance);
        }
    }
}
