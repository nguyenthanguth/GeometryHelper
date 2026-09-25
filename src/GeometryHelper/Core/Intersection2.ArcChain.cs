using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a straight shape meets one that curves.
    /// </summary>
    /// <remarks>
    /// The curved shapes could already be asked about the straight ones; these are the pairs that had no
    /// mirror image to fall back on, so that a polygon can be asked about a bare arc, and a rectangle about
    /// a chain, in the same breath as everything else. A rectangle is read as its own polygon, and an arc
    /// is met as an arc rather than cut into pieces first.
    /// </remarks>
    public static partial class Intersection2
    {
        /// <summary>
        /// Gets the points where a segment meets an arc.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoLine2 line, GeoArc2 arc) => GetIntersections(line, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a segment meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoLine2 line, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.GetIntersections(arc, line, tolerance);
        }

        /// <summary>
        /// Gets the points where a circle meets an arc.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoArc2 arc) => GetIntersections(circle, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a circle meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.GetIntersections(arc, circle, tolerance);
        }

        /// <summary>
        /// Gets the points where the boundary of a polygon meets an arc.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoArc2 arc) => GetIntersections(poly, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where the boundary of a polygon meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return ArcChain2.Intersections(ArcChain2.EdgesOf(poly), arc, tolerance);
        }

        /// <summary>
        /// Gets the points where a polyline meets an arc.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoArc2 arc) => GetIntersections(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a polyline meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoArc2 arc, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ArcChain2.Intersections(ArcChain2.EdgesOf(polyline), arc, tolerance);
        }

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets an arc.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoArc2 arc) => GetIntersections(rect, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets an arc, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoArc2 arc, Tolerance tolerance)
        {
            return GetIntersections(rect.ToPolygon(), arc, tolerance);
        }

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets a loop that may curve.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoPolygonArc2 loop) => GetIntersections(rect, loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets a loop that may curve, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetIntersections(loop, rect.ToPolygon(), tolerance);
        }

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets a chain that may curve.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoPolylineArc2 chain) => GetIntersections(rect, chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where the boundary of a rectangle meets a chain that may curve, within a tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetIntersections(chain, rect.ToPolygon(), tolerance);
        }
    }
}
