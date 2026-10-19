using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An edge asked whether it touches an infinite line and where they cross, as a segment or an arc by its kind.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        #region XLines

        /// <summary>
        /// Determines whether this edge touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this edge touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(xline, tolerance) : ToLine().CollidesWith(xline, tolerance);

        /// <summary>
        /// Gets the points where this edge crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this edge crosses an infinite line, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(xline, tolerance) : ToLine().GetIntersections(xline, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(xline, out intersections, tolerance) : ToLine().TryIntersectWith(xline, out intersections, tolerance);

        #endregion
    }
}
