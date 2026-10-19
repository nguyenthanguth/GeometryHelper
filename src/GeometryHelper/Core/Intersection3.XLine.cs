using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Where an infinite line in space crosses another shape. The line is cut into the segment that reaches past the shape on
    // both sides (see XLine3) and asked as any segment is, with the tolerance passed through. Against a plane, a ray or
    // another line nothing is cut, and the crossing is a closed form.
    static partial class Intersection3
    {
        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoAabb3 box, Tolerance tolerance)
        {
            return GetIntersections(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoObb3 box, Tolerance tolerance)
        {
            return GetIntersections(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoSolid3 solid, Tolerance tolerance)
        {
            return GetIntersections(XLine3.CutPast(xline, solid, tolerance), solid, tolerance);
        }

        // The one point where the line crosses the plane, where a double can place it, or nothing: a line lying in the plane
        // names no place, as a segment lying in it names none.
        internal static bool TryIntersectWith(GeoXLine3 xline, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (!XLine3.TryCrossPlane(xline, plane, tolerance, out double along))
            {
                return false;
            }

            intersection = xline.GetPointAtDistance(along);
            return true;
        }

        // The line two planes share, read as the ray of TryIntersectWith(GeoPlane3, GeoPlane3, out GeoRay3) names it: its origin
        // the point of it nearest the world origin and its direction the cross product of the two normals.
        internal static bool TryGetIntersectionLine(GeoPlane3 plane1, GeoPlane3 plane2, out GeoXLine3 intersection, Tolerance tolerance)
        {
            intersection = default(GeoXLine3);

            if (!TryGetMeetingLine(plane1, plane2, 0.0, out GeoRay3 meeting, tolerance))
            {
                return false;
            }

            intersection = new GeoXLine3(meeting.Origin, meeting.Direction, true);
            return true;
        }

        internal static bool TryIntersectWith(GeoXLine3 xline, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(XLine3.CutPast(xline, triangle, tolerance), triangle, out intersection, tolerance);
        }

        internal static bool TryIntersectWith(GeoXLine3 xline, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(XLine3.CutPast(xline, polygon, tolerance), polygon, out intersection, tolerance);
        }

        internal static bool TryIntersectWith(GeoXLine3 xline, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance)
        {
            return TryIntersectWith(XLine3.CutPast(xline, face, tolerance), face, out intersection, tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoPlane3 plane, Tolerance tolerance)
        {
            return TryIntersectWith(xline, plane, out GeoPoint3 crossing, tolerance) ? new[] { crossing } : new GeoPoint3[0];
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return TryIntersectWith(xline, triangle, out GeoPoint3 crossing, tolerance) ? new[] { crossing } : new GeoPoint3[0];
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return TryIntersectWith(xline, polygon, out GeoPoint3 crossing, tolerance) ? new[] { crossing } : new GeoPoint3[0];
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoFace3 face, Tolerance tolerance)
        {
            return TryIntersectWith(xline, face, out GeoPoint3 crossing, tolerance) ? new[] { crossing } : new GeoPoint3[0];
        }

        internal static GeoPoint3[] GetIntersections(GeoPolyline3 polyline, GeoXLine3 xline, Tolerance tolerance)
        {
            return GetIntersections(polyline, XLine3.CutPast(xline, polyline, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoPolyline3 polyline, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            return TryIntersectWith(polyline, XLine3.CutPast(xline, polyline, tolerance), out intersections, tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoLine3 line, Tolerance tolerance)
        {
            return GetIntersections(XLine3.CutPast(xline, line, tolerance), line, tolerance);
        }

        internal static bool TryIntersectWith(GeoXLine3 xline, GeoLine3 line, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline, line, tolerance);

            return intersections.Length > 0;
        }

        // A line crosses a ray where the shortest segment between them is no longer than the point tolerance, and the place is
        // where that segment lands on the ray, never behind its origin. Parallel is read by the angle alone, as for a segment
        // and a ray.
        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline, GeoRay3 ray, Tolerance tolerance)
        {
            // Both directions are of unit length, so the length of their cross product is the sine of the angle between them;
            // a NaN is parallel to everything.
            if (!(xline.Direction.CrossProduct(ray.Direction).Length > tolerance.EqualAngleSin))
            {
                return new GeoPoint3[0];
            }

            GeoLine3 join = Projection3.GetShortestLineTo(xline, ray, tolerance);

            return join.Length <= tolerance.EqualPoint ? new[] { join.EndPoint } : new GeoPoint3[0];
        }

        internal static bool TryIntersectWith(GeoXLine3 xline, GeoRay3 ray, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline, ray, tolerance);

            return intersections.Length > 0;
        }

        // Two lines cross where the common perpendicular between them is no longer than the point tolerance, and the place is
        // the middle of it, which is the same whichever of the two asks. Two lines parallel by angle are refused, even where
        // they are the same line, as neither has a length to measure a drift over.
        internal static GeoPoint3[] GetIntersections(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance)
        {
            // Both directions are of unit length, so the length of their cross product is the sine of the angle between them;
            // a NaN is parallel to everything.
            if (!(xline1.Direction.CrossProduct(xline2.Direction).Length > tolerance.EqualAngleSin)
                || !XLine3.TryBridge(xline1, xline2, tolerance, out double alongFirst, out double alongSecond, out double gap)
                || !(gap <= tolerance.EqualPoint))
            {
                return new GeoPoint3[0];
            }

            return new[] { xline1.GetPointAtDistance(alongFirst).GetMiddlePoint(xline2.GetPointAtDistance(alongSecond)) };
        }

        internal static bool TryIntersectWith(GeoXLine3 xline1, GeoXLine3 xline2, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(xline1, xline2, tolerance);

            return intersections.Length > 0;
        }
    }
}
