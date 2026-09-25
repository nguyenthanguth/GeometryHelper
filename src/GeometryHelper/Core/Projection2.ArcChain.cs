using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a chain or loop that may curve to something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both ends of the segment sit on a boundary, so a shape lying wholly inside a closed one still
    /// reports the gap out to its outline rather than nothing at all. <see cref="Distance2"/> takes the
    /// opposite view and reads a closed shape as a filled region, so the two deliberately disagree for
    /// that pair; everywhere else the segment is exactly as long as the distance.
    /// </para>
    /// <para>
    /// Arcs are measured as arcs, not cut into pieces first, so the answer is exact. Where the two
    /// boundaries cross, the segment has no length and sits at the crossing.
    /// </para>
    /// </remarks>
    public static partial class Projection2
    {
        #region GeoPolygonArc2

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a segment.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoLine2 line) => GetShortestLineTo(loop, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a segment, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="line">The segment the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="line"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), line, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoArc2 arc) => GetShortestLineTo(loop, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to an arc, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a circle.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoCircle2 circle) => GetShortestLineTo(loop, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a circle, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="circle">The circle the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="circle"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), circle, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a polygon.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolygon2 polygon) => GetShortestLineTo(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a polygon, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="polygon">The polygon the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="polygon"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a polyline.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolyline2 polyline) => GetShortestLineTo(loop, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a polyline, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="polyline">The polyline the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="polyline"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to another curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolygonArc2 other) => GetShortestLineTo(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to another curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="other">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="other"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (other == null) throw new ArgumentNullException(nameof(other));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolylineArc2 chain) => GetShortestLineTo(loop, chain, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved loop to a curved chain, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop the segment leaves.</param>
        /// <param name="chain">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="loop"/> and landing on <paramref name="chain"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain), tolerance);
        }

        #endregion

        #region GeoPolylineArc2

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a segment.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoLine2 line) => GetShortestLineTo(chain, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a segment, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="line">The segment the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="line"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), line, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to an arc.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoArc2 arc) => GetShortestLineTo(chain, arc, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to an arc, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="arc">The arc the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="arc"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), arc, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a circle.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoCircle2 circle) => GetShortestLineTo(chain, circle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a circle, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="circle">The circle the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="circle"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), circle, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a polygon.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolygon2 polygon) => GetShortestLineTo(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a polygon, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="polygon">The polygon the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="polygon"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a polyline.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolyline2 polyline) => GetShortestLineTo(chain, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a polyline, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="polyline">The polyline the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="polyline"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a curved loop.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolygonArc2 loop) => GetShortestLineTo(chain, loop, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to a curved loop, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="loop">The curved loop the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="loop"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop), tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a curved chain to another curved chain.
        /// </summary>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolylineArc2 other) => GetShortestLineTo(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a curved chain to another curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain the segment leaves.</param>
        /// <param name="other">The curved chain the segment lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="chain"/> and landing on <paramref name="other"/>.</returns>
        public static GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (other == null) throw new ArgumentNullException(nameof(other));

            return ArcChain2.ShortestLineTo(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(other), tolerance);
        }

        #endregion

    }
}
