using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Measuring an infinite line in space against another shape. The line is cut into the segment that reaches past the shape
    // on both sides (see XLine3) and asked as any segment is, with the tolerance passed through. Against a point, a plane, a
    // ray or another line nothing is cut, and the answer is a closed form.
    static partial class Distance3
    {
        internal static double DistanceTo(GeoXLine3 xline, GeoPoint3 point, Tolerance tolerance)
        {
            return DistanceTo(XLine3.Project(xline, point), point);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoLine3 line, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, line, tolerance), line, tolerance);
        }

        // Where the line and the ray come nearest, the gap between them if the ray reaches the place; otherwise the distance
        // of the origin of the ray from the line, which is where the ray comes nearest. The gap is nought only where the two
        // truly meet.
        internal static double DistanceTo(GeoXLine3 xline, GeoRay3 ray, Tolerance tolerance)
        {
            if (XLine3.TryBridge(xline, ray, tolerance, out _, out double alongRay, out double gap) && alongRay >= 0.0)
            {
                return gap;
            }

            return DistanceTo(xline, ray.Origin, tolerance);
        }

        // Two lines in space are as far apart as their common perpendicular is long, nought where they meet; parallel by
        // rounding, the gap is the distance of one origin from the other line, the nearer of the two where rounding makes them
        // differ.
        internal static double DistanceTo(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance)
        {
            if (XLine3.TryBridge(xline1, xline2, tolerance, out _, out _, out double gap))
            {
                return gap;
            }

            return Math.Min(DistanceTo(xline1, xline2.Origin, tolerance), DistanceTo(xline2, xline1.Origin, tolerance));
        }

        // Nought where the line starts within the planar tolerance of the plane, as a ray starting there touches it (so the line
        // lies in the plane, or crosses it at once) and where it crosses the plane at a distance a double can place; otherwise it
        // runs alongside, and the gap is the height of its origin over the plane. Nothing is read from the tilt of a line that
        // starts off the plane but its crossing, so the distance never falls as the line is raised.
        internal static double DistanceTo(GeoXLine3 xline, GeoPlane3 plane, Tolerance tolerance)
        {
            double height = Math.Abs(plane.SignedDistanceTo(xline.Origin));

            // Written as what has to hold, so that a NaN fails it.
            if (height <= tolerance.EqualPlanar || XLine3.TryCrossPlane(xline, plane, tolerance, out _))
            {
                return 0.0;
            }

            return height;
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, triangle, tolerance), triangle, tolerance);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, polygon, tolerance), polygon, tolerance);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoFace3 face, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, face, tolerance), face, tolerance);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoSolid3 solid, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, solid, tolerance), solid, tolerance);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoAabb3 box, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static double DistanceTo(GeoXLine3 xline, GeoObb3 box, Tolerance tolerance)
        {
            return DistanceTo(XLine3.CutPast(xline, box, tolerance), box, tolerance);
        }

        internal static double DistanceTo(GeoPolyline3 polyline, GeoXLine3 xline, Tolerance tolerance)
        {
            return DistanceTo(polyline, XLine3.CutPast(xline, polyline, tolerance), tolerance);
        }
    }
}
