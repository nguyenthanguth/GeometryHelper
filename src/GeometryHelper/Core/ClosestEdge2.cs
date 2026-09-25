using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Picking the one piece of a shape that lies nearest something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This answers a different question from <see cref="Projection2"/>, which builds the segment joining
    /// two shapes. Nothing is built here: one of the shape's own edges is handed back, whole, the one
    /// lying nearest the probe.
    /// </para>
    /// <para>
    /// The probe is always a single primitive &#8212; a point, a segment, a circle or an arc. A shape that
    /// is itself made of edges is not offered, because the nearest edge of one such shape to another is
    /// really a pair of edges, and that is a different answer from the one this name promises.
    /// </para>
    /// <para>
    /// Each edge is measured on its own, so a probe lying inside a closed shape still names the edge
    /// nearest it rather than reporting nothing, which is where this parts company with
    /// <see cref="Distance2"/>. Where two edges are equally near &#8212; and a probe crossing the outline
    /// touches several at nothing at all &#8212; the earlier one in the shape's own order wins, so the
    /// answer does not drift from one run to the next.
    /// </para>
    /// </remarks>
    public static class ClosestEdge2
    {
        /// <summary>
        /// Walks a shape's pieces once and keeps the one the measure rates lowest.
        /// </summary>
        /// <remarks>
        /// Ties go to the earlier piece, because the comparison is a strict one.
        /// </remarks>
        internal static T Among<T>(IEnumerable<T> edges, Func<T, double> measure)
        {
            T nearest = default(T);
            double best = double.PositiveInfinity;
            bool found = false;

            foreach (T edge in edges)
            {
                double reach = measure(edge);

                if (!found || reach < best)
                {
                    best = reach;
                    nearest = edge;
                    found = true;
                }
            }

            if (!found)
            {
                throw new InvalidOperationException("The shape has no edges to choose between.");
            }

            return nearest;
        }

        #region GeoPolygon2

        /// <summary>
        /// Gets the edge of a polygon nearest a point.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The point to measure against.</param>
        /// <returns>The edge of <paramref name="poly"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolygon2 poly, GeoPoint2 point)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return Among(poly.GetEdges(), edge => Distance2.DistanceTo(edge, point));
        }

        /// <summary>
        /// Gets the edge of a polygon nearest a segment.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <returns>The edge of <paramref name="poly"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolygon2 poly, GeoLine2 line)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return Among(poly.GetEdges(), edge => Distance2.DistanceTo(edge, line));
        }

        /// <summary>
        /// Gets the edge of a polygon nearest a circle.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <returns>The edge of <paramref name="poly"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolygon2 poly, GeoCircle2 circle)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            return Among(poly.GetEdges(), edge => Distance2.DistanceTo(circle, edge));
        }

        #endregion

        #region GeoPolyline2

        /// <summary>
        /// Gets the edge of a polyline nearest a point.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The point to measure against.</param>
        /// <returns>The edge of <paramref name="polyline"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolyline2 polyline, GeoPoint2 point)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Among(polyline.GetEdges(), edge => Distance2.DistanceTo(edge, point));
        }

        /// <summary>
        /// Gets the edge of a polyline nearest a segment.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <returns>The edge of <paramref name="polyline"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolyline2 polyline, GeoLine2 line)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Among(polyline.GetEdges(), edge => Distance2.DistanceTo(edge, line));
        }

        /// <summary>
        /// Gets the edge of a polyline nearest a circle.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <returns>The edge of <paramref name="polyline"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoPolyline2 polyline, GeoCircle2 circle)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return Among(polyline.GetEdges(), edge => Distance2.DistanceTo(circle, edge));
        }

        #endregion

        #region GeoRectangle2

        /// <summary>
        /// Gets the edge of a rectangle nearest a point.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The point to measure against.</param>
        /// <returns>The edge of <paramref name="rect"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoRectangle2 rect, GeoPoint2 point)
        {
            return Among(rect.GetEdges(), edge => Distance2.DistanceTo(edge, point));
        }

        /// <summary>
        /// Gets the edge of a rectangle nearest a segment.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <returns>The edge of <paramref name="rect"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoRectangle2 rect, GeoLine2 line)
        {
            return Among(rect.GetEdges(), edge => Distance2.DistanceTo(edge, line));
        }

        /// <summary>
        /// Gets the edge of a rectangle nearest a circle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <returns>The edge of <paramref name="rect"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoLine2 GetClosestEdge(GeoRectangle2 rect, GeoCircle2 circle)
        {
            return Among(rect.GetEdges(), edge => Distance2.DistanceTo(circle, edge));
        }

        #endregion

        #region GeoPolygonArc2

        /// <summary>
        /// Gets the edge of a curved loop nearest a point.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="point">The point to measure against.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoPoint2 point)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(point));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest a point, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="point">The point to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(point, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest a segment.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoLine2 line)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(line));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest a segment, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(line, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest a circle.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoCircle2 circle)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(circle));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest a circle, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(circle, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest an arc.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="arc">The arc to measure against.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="arc"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoArc2 arc)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(arc));
        }

        /// <summary>
        /// Gets the edge of a curved loop nearest an arc, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="arc">The arc to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="loop"/> lying nearest <paramref name="arc"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return Among(loop.GetEdges(), edge => edge.DistanceTo(arc, tolerance));
        }

        #endregion

        #region GeoPolylineArc2

        /// <summary>
        /// Gets the edge of a curved chain nearest a point.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="point">The point to measure against.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoPoint2 point)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(point));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest a point, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="point">The point to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="point"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoPoint2 point, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(point, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest a segment.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoLine2 line)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(line));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest a segment, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="line">The segment to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="line"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(line, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest a circle.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoCircle2 circle)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(circle));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest a circle, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="circle">The circle to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="circle"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(circle, tolerance));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest an arc.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="arc">The arc to measure against.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="arc"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoArc2 arc)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(arc));
        }

        /// <summary>
        /// Gets the edge of a curved chain nearest an arc, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="arc">The arc to measure against.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The edge of <paramref name="chain"/> lying nearest <paramref name="arc"/>.</returns>
        public static GeoEdge2 GetClosestEdge(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Among(chain.GetEdges(), edge => edge.DistanceTo(arc, tolerance));
        }

        #endregion

    }
}
