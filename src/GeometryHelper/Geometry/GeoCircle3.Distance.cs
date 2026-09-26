using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a circle is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Calculates the shortest distance from this disc to a point.
        /// </summary>
        /// <remarks>
        /// The disc counts as a filled surface, so a point directly above the centre is measured straight
        /// down to the surface rather than out to the circumference.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);
    }
}
