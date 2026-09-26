using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved loop is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Gets the distance from the outline of the loop to a point.
        /// </summary>
        /// <remarks>
        /// Measured to the outline, so a point inside the loop is not nought away. That is what
        /// <see cref="Contains(GeoPoint3)"/> is for.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the outline of the loop to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));
    }
}
