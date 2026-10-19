using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A point asked how far it is from an infinite line, and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoPoint3
    {
        #region XLines

        /// <summary>
        /// Calculates the shortest distance from this point to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this point to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(xline, this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on an infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline) => GetShortestLineTo(xline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on an infinite line, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 xline, Tolerance tolerance) => Projection3.GetShortestLineTo(xline, this, tolerance).Reverse();

        #endregion
    }
}
