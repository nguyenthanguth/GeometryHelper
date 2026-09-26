using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a face is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Calculates the shortest distance from this face to a point.
        /// </summary>
        /// <remarks>
        /// The face is read as material with holes in it, so a point over a hole is over nothing and is
        /// measured to the rim of that hole rather than to the surface that is not there behind it.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this face to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.DistanceTo(this, point, tolerance);
    }
}
