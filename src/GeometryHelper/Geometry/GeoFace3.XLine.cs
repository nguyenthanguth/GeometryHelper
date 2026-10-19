using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A face asked about an infinite line: whether they touch, how far apart they are, where they cross and where the shortest
    /// segment between them runs.
    /// </summary>
    public sealed partial class GeoFace3
    {
        #region XLines

        /// <summary>
        /// Determines whether this face touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this face touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Collision3.CollidesWith(xline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this face to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this face to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(xline, this, tolerance);

        /// <summary>
        /// Gets the point where this face crosses an infinite line, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the point where this face crosses an infinite line, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Intersection3.GetIntersections(xline, this, tolerance);

        /// <summary>
        /// Tries to find the point where this face crosses an infinite line, using the default tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3 intersection) => TryIntersectWith(xline, out intersection, Tolerance.Global);

        /// <summary>
        /// Tries to find the point where this face crosses an infinite line, within a tolerance.
        /// </summary>
        /// <param name="xline">The infinite line.</param>
        /// <param name="intersection">The crossing point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoXLine3 xline, out GeoPoint3 intersection, Tolerance tolerance) => Intersection3.TryIntersectWith(xline, this, out intersection, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this face and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this face and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Projection3.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
