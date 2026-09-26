using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a box is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoObb3
    {
        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside the box is at
        /// distance zero.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this box to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this box to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this box to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a box, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this box to a box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/>
        /// reads this box as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => Distance3.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.SignedDistanceTo(this, point, tolerance);
    }
}
