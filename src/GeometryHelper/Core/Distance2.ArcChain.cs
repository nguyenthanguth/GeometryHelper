using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Measuring a straight shape against one that curves.
    /// </summary>
    /// <remarks>
    /// The curved shapes could already be measured against the straight ones; these are the pairs that had
    /// no mirror image to fall back on, so that a polygon can be asked about an arc, and a rectangle about
    /// a chain, in the same breath as everything else. A rectangle is read as its own polygon, and an arc
    /// is measured as an arc rather than cut into pieces first.
    /// </remarks>
    public static partial class Distance2
    {
        /// <summary>
        /// Calculates the shortest distance from a polygon to an arc.
        /// </summary>
        public static double DistanceTo(GeoPolygon2 poly, GeoArc2 arc) => DistanceTo(poly, arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a polygon to an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A polygon is a filled region here, as everywhere else in this class, so an arc lying wholly
        /// inside one is at no distance from it.
        /// </remarks>
        public static double DistanceTo(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Containment2.Contains(poly, arc.StartPoint, tolerance)) return 0.0;

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(poly), arc, tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance from a polyline to an arc.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 polyline, GeoArc2 arc) => DistanceTo(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a polyline to an arc, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 polyline, GeoArc2 arc, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(polyline), arc, tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance from a rectangle to an arc.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoArc2 arc) => DistanceTo(rect, arc, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a rectangle to an arc, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoArc2 arc, Tolerance tolerance)
        {
            return DistanceTo(rect.ToPolygon(), arc, tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance from a rectangle to a loop that may curve.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolygonArc2 loop) => DistanceTo(rect, loop, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a rectangle to a loop that may curve, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            return DistanceTo(loop, rect.ToPolygon(), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance from a rectangle to a chain that may curve.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolylineArc2 chain) => DistanceTo(rect, chain, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a rectangle to a chain that may curve, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            return DistanceTo(chain, rect.ToPolygon(), tolerance);
        }
    }
}
