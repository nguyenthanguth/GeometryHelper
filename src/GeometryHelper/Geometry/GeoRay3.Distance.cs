using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a ray is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Calculates the shortest distance from this ray to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this ray and a line segment.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance from this ray to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(this, solid);

        /// <summary>
        /// Gets the distance from this ray to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(this, solid, tolerance);

        /// <summary>
        /// Gets the distance from this ray to a triangle, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Gets the distance from this ray to a triangle, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);
    }
}
