using System;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an arc touches another shape.
    /// </summary>
    public readonly partial struct GeoArc3
    {
        /// <summary>
        /// Checks whether this arc touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => Arc3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether this arc touches a plane, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Arc3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether this arc touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Arc3.CollidesWith(this, line);

        /// <summary>
        /// Checks whether this arc touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Arc3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this arc touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Arc3.CollidesWith(this, ray);

        /// <summary>
        /// Checks whether this arc touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Arc3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Checks whether this arc touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 other) => Arc3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this arc touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 other, Tolerance tolerance) => Arc3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this arc touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(this, circle);

        /// <summary>
        /// Checks whether this arc touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(this, circle, tolerance);

    }
}
