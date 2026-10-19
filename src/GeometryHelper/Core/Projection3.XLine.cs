using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // The shortest segment joining an infinite line in space to another shape, leaving the line and landing on the shape. The
    // line is cut into the segment that reaches past the shape on both sides (see XLine3) and asked as any segment is, with the
    // tolerance passed through. Against a point, a plane, a ray or another line nothing is cut, and the answer is a closed form.
    static partial class Projection3
    {
        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoPoint3 point, Tolerance tolerance)
        {
            return new GeoLine3(XLine3.Project(xline, point), point);
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoLine3 line, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine3.CutPast(xline, line, tolerance), line, tolerance);
        }

        // Where the ray reaches the common perpendicular of the two lines, that segment, as Distance3 measures it; otherwise
        // from the foot of the origin of the ray on the line to that origin.
        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoRay3 ray, Tolerance tolerance)
        {
            if (XLine3.TryBridge(xline, ray, tolerance, out _, out double alongRay, out _) && alongRay >= 0.0)
            {
                // The foot of the point of the ray on the line, so that the segment is as long as the point is far from the line, and
                // square to it, whatever the sine: the feet of the perpendicular itself are only as good as the offset over the sine.
                GeoPoint3 onRay = ray.GetPointAtDistance(alongRay);

                return new GeoLine3(XLine3.Project(xline, onRay), onRay);
            }

            return new GeoLine3(XLine3.Project(xline, ray.Origin), ray.Origin);
        }

        // Two lines asked either way round give the same segment turned round, to the bit: it is worked out from the one that
        // comes first (see XLine3.Precedes) and turned round for the other.
        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance)
        {
            return XLine3.Precedes(xline2, xline1)
                ? ShortestFromTheFirst(xline2, xline1, tolerance).Reverse()
                : ShortestFromTheFirst(xline1, xline2, tolerance);
        }

        private static GeoLine3 ShortestFromTheFirst(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance)
        {
            if (XLine3.TryBridge(xline1, xline2, tolerance, out double alongFirst, out _, out _))
            {
                // The foot of a point of the first line on the second, as for a ray: as long as the point is far from the second line.
                GeoPoint3 onFirst = xline1.GetPointAtDistance(alongFirst);

                return new GeoLine3(onFirst, XLine3.Project(xline2, onFirst));
            }

            // The two candidates Distance3.DistanceTo(GeoXLine3, GeoXLine3) weighs, the first kept on a tie.
            GeoLine3 first = new GeoLine3(XLine3.Project(xline1, xline2.Origin), xline2.Origin);
            GeoLine3 second = new GeoLine3(xline1.Origin, XLine3.Project(xline2, xline1.Origin));

            return second.Length < first.Length ? second : first;
        }

        // A segment of no length where the line crosses the plane; otherwise it runs alongside or lies in it, and the segment
        // is the perpendicular from the origin, as Distance3 measures it.
        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoPlane3 plane, Tolerance tolerance)
        {
            if (XLine3.TryCrossPlane(xline, plane, tolerance, out double along))
            {
                GeoPoint3 crossing = xline.GetPointAtDistance(along);

                return new GeoLine3(crossing, crossing);
            }

            return new GeoLine3(xline.Origin, ProjectToPlane(plane, xline.Origin));
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine3.CutPast(xline, triangle, tolerance), triangle, tolerance);
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine3.CutPast(xline, polygon, tolerance), polygon, tolerance);
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoFace3 face, Tolerance tolerance)
        {
            return GetShortestLineTo(XLine3.CutPast(xline, face, tolerance), face, tolerance);
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoSolid3 solid, Tolerance tolerance)
        {
            return GetShortestLineTo(solid, XLine3.CutPast(xline, solid, tolerance), tolerance).Reverse();
        }

        internal static GeoLine3 GetShortestLineTo(GeoXLine3 xline, GeoPolyline3 polyline, Tolerance tolerance)
        {
            return GetShortestLineTo(polyline, XLine3.CutPast(xline, polyline, tolerance), tolerance).Reverse();
        }
    }
}
