using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a solid crosses another shape.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Gets the points where a line segment passes through the surface of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(line, this);

        /// <summary>
        /// Gets the points where a line segment passes through the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(line, this, tolerance);

        /// <summary>
        /// Gets the points where a plane cuts the edges of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane) => Intersection3.GetIntersections(plane, this);

        /// <summary>
        /// Gets the points where a plane cuts the edges of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoPlane3 plane, Tolerance tolerance) => Intersection3.GetIntersections(plane, this, tolerance);

        /// <summary>
        /// Gets the points where a ray passes through the surface of this solid.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets the points where a ray passes through the surface of this solid, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);
    }
}
