using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a segment is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoLine3
    {
        /// <summary>
        /// Calculates the shortest distance from this segment to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this segment and another segment.
        /// </summary>
        public double DistanceTo(GeoLine3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this segment and a ray.
        /// </summary>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a plane.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a triangle.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance between this segment and a triangle, within a tolerance: the same answer as
        /// <see cref="GeoTriangle3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and a polygon.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this segment and a polygon, within a tolerance: the same answer as
        /// <see cref="GeoPolygon3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and a solid. A segment reaching into the
        /// body is at distance zero.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(this, solid);

        /// <summary>
        /// Calculates the shortest distance between this segment and a solid, within a tolerance: the same answer as
        /// <see cref="GeoSolid3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(this, solid, tolerance);

        /// <summary>
        /// Gets the distance from this segment to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this segment to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);
    }
}
