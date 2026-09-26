using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a circle nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Gets the point on the circumference closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToCircle(this, point);
    }
}
