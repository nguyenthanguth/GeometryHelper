using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An axis-aligned box asked whether it touches an infinite line, how far apart they are and where they cross.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        #region XLines

        /// <summary>
        /// Determines whether this box touches an infinite line, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline) => CollidesWith(xline, Tolerance.Global);

        /// <summary>
        /// Determines whether this box touches an infinite line, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoXLine3 xline, Tolerance tolerance) => Collision3.CollidesWith(xline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this box to an infinite line, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline) => DistanceTo(xline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this box to an infinite line, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoXLine3 xline, Tolerance tolerance) => Distance3.DistanceTo(xline, this, tolerance);

        /// <summary>
        /// Gets the points where an infinite line crosses this box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline) => GetIntersections(xline, Tolerance.Global);

        /// <summary>
        /// Gets the points where an infinite line crosses this box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoXLine3 xline, Tolerance tolerance) => Intersection3.GetIntersections(xline, this, tolerance);

        #endregion
    }
}
