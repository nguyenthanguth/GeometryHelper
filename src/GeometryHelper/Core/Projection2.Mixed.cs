using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a straight shape to one that curves.
    /// </summary>
    /// <remarks>
    /// The same segment as the one the curved shape would hand back, turned round so that it leaves the
    /// shape it was asked of. A rectangle is read as its own polygon; an arc is joined as an arc.
    /// </remarks>
    public static partial class Projection2
    {
        #region GeoLine2

        /// <summary>
        /// Gets the shortest segment joining a segment to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoArc2 arc) => GetShortestLineTo(line, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a segment to an arc, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="line"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.GetShortestLineTo(arc, line, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a segment to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolygonArc2 loop) => GetShortestLineTo(line, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a segment to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="line"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, line, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a segment to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolylineArc2 chain) => GetShortestLineTo(line, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a segment to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="line"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoLine2 line, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, line, tolerance).Reverse();
        }

        #endregion

        #region GeoCircle2

        /// <summary>
        /// Gets the shortest segment joining a circle to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoArc2 arc) => GetShortestLineTo(circle, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a circle to an arc, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="circle"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoArc2 arc, Tolerance tolerance)
        {
            return Arc2.GetShortestLineTo(arc, circle, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a circle to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolygonArc2 loop) => GetShortestLineTo(circle, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a circle to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="circle"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, circle, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a circle to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolylineArc2 chain) => GetShortestLineTo(circle, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a circle to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="circle"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoCircle2 circle, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, circle, tolerance).Reverse();
        }

        #endregion

        #region GeoRectangle2

        /// <summary>
        /// Gets the shortest segment joining a rectangle to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoArc2 arc) => GetShortestLineTo(rect, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a rectangle to an arc, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="rect"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoArc2 arc, Tolerance tolerance)
        {
            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(rect.ToPolygon()), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a rectangle to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolygonArc2 loop) => GetShortestLineTo(rect, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a rectangle to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="rect"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, rect.ToPolygon(), tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a rectangle to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolylineArc2 chain) => GetShortestLineTo(rect, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a rectangle to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="rect">The rectangle the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="rect"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoRectangle2 rect, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, rect.ToPolygon(), tolerance).Reverse();
        }

        #endregion

        #region GeoPolygon2

        /// <summary>
        /// Gets the shortest segment joining a polygon to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoArc2 arc) => GetShortestLineTo(poly, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polygon to an arc, within a tolerance.
        /// </summary>
        /// <param name="poly">The polygon the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="poly"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoArc2 arc, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(poly), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a polygon to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolygonArc2 loop) => GetShortestLineTo(poly, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polygon to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="poly">The polygon the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="poly"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, poly, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a polygon to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolylineArc2 chain) => GetShortestLineTo(poly, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polygon to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="poly">The polygon the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="poly"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygon2 poly, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, poly, tolerance).Reverse();
        }

        #endregion

        #region GeoPolyline2

        /// <summary>
        /// Gets the shortest segment joining a polyline to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoArc2 arc) => GetShortestLineTo(polyline, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to an arc, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="polyline"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoArc2 arc, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(polyline), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolygonArc2 loop) => GetShortestLineTo(polyline, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="polyline"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetShortestLineTo(loop, polyline, tolerance).Reverse();
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolylineArc2 chain) => GetShortestLineTo(polyline, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="polyline"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetShortestLineTo(chain, polyline, tolerance).Reverse();
        }

        #endregion

    }
}
