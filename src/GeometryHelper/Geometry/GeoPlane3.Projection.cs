using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a plane nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Gets the point of this plane nearest a target point.
        /// </summary>
        /// <remarks>
        /// A plane is endless, so this is the foot of the perpendicular and never anything else. The sign of
        /// which side the point was on is in <see cref="SignedDistanceTo(GeoPoint3)"/>.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPlane(this, point);

        /// <summary>
        /// Gets the point of this plane nearest a target point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The tolerance changes nothing here; it is taken so that this reads like every other shape.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToPlane(this, point);
    }
}
