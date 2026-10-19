using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Measuring an infinite line in the plane against another shape. The line is cut into the segment that reaches
    // past the shape on both sides (see XLine2) and asked as any segment is, with the tolerance passed through.
    // Against a point, a ray or another line nothing is cut.
    static partial class Distance2
    {
        internal static double DistanceTo(GeoXLine2 xline, GeoPoint2 point, Tolerance tolerance)
        {
            return DistanceTo(point, XLine2.Project(xline, point));
        }

        internal static double DistanceTo(GeoXLine2 xline, GeoLine2 line, Tolerance tolerance)
        {
            return DistanceTo(XLine2.CutPast(xline, line, tolerance), line, tolerance);
        }

        // Nought where the line and the ray truly cross, ahead of the origin of the ray; otherwise the distance of that origin
        // from the line, kept even where it is within the point tolerance, as two segments keep theirs.
        internal static double DistanceTo(GeoXLine2 xline, GeoRay2 ray, Tolerance tolerance)
        {
            if (XLine2.TryCross(xline, ray, tolerance, false, out _, out _))
            {
                return 0.0;
            }

            return DistanceTo(xline, ray.Origin, tolerance);
        }

        // Nought where the two lines truly cross at any angle; otherwise they run side by side, and the gap is the distance
        // of one origin from the other line, the nearer of the two where rounding makes them differ.
        internal static double DistanceTo(GeoXLine2 xline1, GeoXLine2 xline2, Tolerance tolerance)
        {
            if (XLine2.TryCross(xline1, xline2, tolerance, out _))
            {
                return 0.0;
            }

            return Math.Min(DistanceTo(xline1, xline2.Origin, tolerance), DistanceTo(xline2, xline1.Origin, tolerance));
        }

        internal static double DistanceTo(GeoCircle2 circle, GeoXLine2 xline, Tolerance tolerance)
        {
            return DistanceTo(circle, XLine2.CutPast(xline, circle, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoPolygon2 poly, GeoXLine2 xline, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // DistanceTo(poly, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = XLine2.CutPast(xline, poly, tolerance);

            if (Collision2.Meets(poly, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                double d = DistanceToSide(poly.GetEdgeAt(i), cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        internal static double DistanceTo(GeoPolygonArc2 loop, GeoXLine2 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return DistanceTo(loop, XLine2.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoPolyline2 polyline, GeoXLine2 xline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            // DistanceTo(polyline, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = XLine2.CutPast(xline, polyline, tolerance);

            if (CrossesOrStartsOn(polyline, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                double d = DistanceToSide(polyline.GetEdgeAt(i), cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        internal static double DistanceTo(GeoPolylineArc2 chain, GeoXLine2 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return DistanceTo(chain, XLine2.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoRectangle2 rect, GeoXLine2 xline, Tolerance tolerance)
        {
            // DistanceTo(rect, line) read with the tolerance passed down, which it takes from the global one.
            GeoLine2 cut = XLine2.CutPast(xline, rect, tolerance);

            if (Collision2.Meets(rect, cut))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            foreach (GeoLine2 edge in rect.GetEdges())
            {
                double d = DistanceToSide(edge, cut, tolerance);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }
    }
}
