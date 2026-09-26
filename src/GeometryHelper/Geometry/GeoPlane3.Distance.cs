using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a plane is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Calculates the unsigned distance from a point to the plane.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from a line segment to the plane. A segment that crosses the
        /// plane is at distance zero.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Calculates the signed distance from a point to the plane.
        /// </summary>
        /// <param name="point">The target point.</param>
        /// <returns>Positive if the point is on the side the normal points towards, negative otherwise.</returns>
        public double SignedDistanceTo(GeoPoint3 point) => Origin.GetVectorTo(point).DotProduct(Normal);

        /// <summary>
        /// Gets the distance from this plane to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this plane to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this plane to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this plane to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this plane to a plane, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPlane3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this plane to a plane, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPlane3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);
    }
}
