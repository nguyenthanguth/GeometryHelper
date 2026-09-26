using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a polyline crosses another shape.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Gets every point where this chain crosses a curved chain.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other) => Core.ArcChain3.GetIntersections(other, this);

        /// <summary>
        /// Gets every point where this chain crosses a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolylineArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(other, this, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a curved chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a curved chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this chain crosses a curved loop.
        /// </summary>
        /// <remarks>Every pair of edges is asked, so the work grows with the two edge counts multiplied. Each edge carries a box round itself and a pair whose boxes cannot reach each other is dropped before any arithmetic, which is what makes it usable on bars that are mostly far apart.</remarks>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other) => Core.ArcChain3.GetIntersections(other, this);

        /// <summary>
        /// Gets every point where this chain crosses a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPolygonArc3 other, Tolerance tolerance)
            => Core.ArcChain3.GetIntersections(other, this, tolerance);

        /// <summary>
        /// Tries to find where this chain crosses a curved loop.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections);

        /// <summary>
        /// Tries to find where this chain crosses a curved loop, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc3 other, out GeoPoint3[] intersections, Tolerance tolerance)
            => Core.ArcChain3.TryIntersectWith(other, this, out intersections, tolerance);

    }
}