using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Whether an infinite line in space touches another shape. The line is cut into the segment that reaches past the shape
    // on both sides (see XLine3) and asked as any segment is, with the tolerance passed through. Against a point, a plane, a
    // ray or another line nothing is cut: they touch where the exact gap is no more than the point tolerance.
    static partial class Collision3
    {
        internal static bool CollidesWith(GeoXLine3 xline, GeoAabb3 box, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoObb3 box, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoSolid3 solid, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, solid, tolerance), solid, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, triangle, tolerance), triangle, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, polygon, tolerance), polygon, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoFace3 face, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, face, tolerance), face, tolerance);
        }

        internal static bool CollidesWith(GeoPolyline3 polyline, GeoXLine3 xline, Tolerance tolerance)
        {
            return CollidesWith(polyline, XLine3.CutPast(xline, polyline, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoLine3 line, Tolerance tolerance)
        {
            return CollidesWith(XLine3.CutPast(xline, line, tolerance), line, tolerance);
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoRay3 ray, Tolerance tolerance)
        {
            return Distance3.DistanceTo(xline, ray, tolerance) <= tolerance.EqualPoint;
        }

        internal static bool CollidesWith(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance)
        {
            return Distance3.DistanceTo(xline1, xline2, tolerance) <= tolerance.EqualPoint;
        }

        internal static bool CollidesWith(GeoXLine3 xline, GeoPlane3 plane, Tolerance tolerance)
        {
            return Distance3.DistanceTo(xline, plane, tolerance) <= tolerance.EqualPoint;
        }
    }
}
