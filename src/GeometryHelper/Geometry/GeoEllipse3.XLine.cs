using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An ellipse asked whether its rim touches an infinite line and where they cross.
    /// </summary>
    public readonly partial struct GeoEllipse3
    {
        #region XLines

        /// <summary>
        /// Determines whether the rim of this ellipse touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether the rim of this ellipse touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Ellipse3.CollidesWith(this, xline, tolerance);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where the rim of this ellipse crosses an infinite line, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Ellipse3.GetIntersections(this, xline, tolerance);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the rim of this ellipse crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance) => Ellipse3.TryIntersectWith(this, xline, out intersections, tolerance);

        #endregion
    }
}
