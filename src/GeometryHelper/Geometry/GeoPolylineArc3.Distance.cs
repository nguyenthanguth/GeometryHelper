using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved chain is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolylineArc3
    {
        /// <summary>
        /// Gets the distance from the chain to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the chain to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));
    }
}
