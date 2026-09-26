using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a face nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Gets the point of this face nearest a target point.
        /// </summary>
        /// <remarks>
        /// The foot of the perpendicular where that lands on the material, the rim of a hole where it lands
        /// in one, and the outline where it lands off the face altogether. This reads the way
        /// <see cref="GeoPolygon3.GetClosestPointOnBoundary(GeoPoint3)"/> reads, which answers with a point
        /// of the region rather than of its outline.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToFace(this, point);

        /// <summary>
        /// Gets the point of this face nearest a target point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToFace(this, point, tolerance);
    }
}
