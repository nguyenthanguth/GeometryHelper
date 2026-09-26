using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an axis-aligned box crosses another shape.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Gets every point where a segment crosses the surface of this box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Core.Intersection3.GetIntersections(line, this);

        /// <summary>
        /// Gets every point where a segment crosses the surface of this box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Core.Intersection3.GetIntersections(line, this, tolerance);

        /// <summary>
        /// Gets every point where a ray crosses the surface of this box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Core.Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets every point where a ray crosses the surface of this box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Core.Intersection3.GetIntersections(ray, this, tolerance);
    }
}
