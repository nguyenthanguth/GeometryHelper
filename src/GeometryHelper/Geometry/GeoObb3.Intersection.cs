using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an oriented box crosses another shape.
    /// </summary>
    public sealed partial class GeoObb3
    {
        /// <summary>
        /// Gets every point where a segment crosses the surface of this box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line) => Intersection3.GetIntersections(line, this);

        /// <summary>
        /// Gets every point where a segment crosses the surface of this box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoLine3 line, Tolerance tolerance) => Intersection3.GetIntersections(line, this, tolerance);

        /// <summary>
        /// Gets every point where a ray crosses the surface of this box, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => Intersection3.GetIntersections(ray, this);

        /// <summary>
        /// Gets every point where a ray crosses the surface of this box, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance) => Intersection3.GetIntersections(ray, this, tolerance);
    }
}
