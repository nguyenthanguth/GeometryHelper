using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A polyline asked about an infinite line: whether they touch, how far apart they are, where they cross and where the
    /// shortest segment between them runs.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        #region XLines

        /// <summary>
        /// Determines whether this polyline touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this polyline touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Collision3.CollidesWith(this, xline, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this polyline to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this polyline to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(this, xline, tolerance);

        /// <summary>
        /// Gets the points where this polyline crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this polyline crosses an infinite line, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Intersection3.GetIntersections(this, xline, tolerance);

        /// <summary>
        /// Tries to find where this polyline crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this polyline crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(this, xline, out intersections, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this polyline and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this polyline and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Projection3.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
