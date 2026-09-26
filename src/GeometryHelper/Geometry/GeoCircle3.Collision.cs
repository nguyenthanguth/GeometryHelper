using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a circle touches another shape.
    /// </summary>
    public readonly partial struct GeoCircle3
    {
        /// <summary>
        /// Checks whether this circle touches a plane, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane) => Arc3.CollidesWith(this, plane);

        /// <summary>
        /// Checks whether this circle touches a plane, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPlane3 plane, Tolerance tolerance) => Arc3.CollidesWith(this, plane, tolerance);

        /// <summary>
        /// Checks whether this circle touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Arc3.CollidesWith(this, line);

        /// <summary>
        /// Checks whether this circle touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Arc3.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this circle touches a ray, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Arc3.CollidesWith(this, ray);

        /// <summary>
        /// Checks whether this circle touches a ray, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Arc3.CollidesWith(this, ray, tolerance);

        /// <summary>
        /// Checks whether this circle touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this circle touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this circle touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 other) => Arc3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this circle touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 other, Tolerance tolerance) => Arc3.CollidesWith(this, other, tolerance);

    }
}
