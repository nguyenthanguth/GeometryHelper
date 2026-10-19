using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A line segment asked about an infinite line: whether they touch, how far apart they are, where they cross and where the
    /// shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoLine3
    {
        #region XLines

        /// <summary>
        /// Determines whether this line segment touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this line segment touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Collision3.CollidesWith(xline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this line segment to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this line segment to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(xline, this, tolerance);

        /// <summary>
        /// Gets the point where this line segment crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the point where this line segment crosses an infinite line, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Intersection3.GetIntersections(xline, this, tolerance);

        /// <summary>
        /// Tries to find where this line segment crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections) => TryIntersectWith(xline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this line segment crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersections">The crossing point when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance) => Intersection3.TryIntersectWith(xline, this, out intersections, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this line segment and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this line segment and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Projection3.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
