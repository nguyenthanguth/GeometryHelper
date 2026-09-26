using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a triangle is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoTriangle3
    {
        /// <summary>
        /// Calculates the shortest distance from this triangle to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this triangle to a segment, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Gets the distance from this triangle to a segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a ray, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Gets the distance from this triangle to a ray, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this triangle to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this triangle to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a triangle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this triangle to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);
    }
}
