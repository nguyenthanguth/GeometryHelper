using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a triangle touches another shape.
    /// </summary>
    public readonly partial struct GeoTriangle3
    {
        /// <summary>
        /// Checks whether this triangle touches another triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this triangle touches another triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this triangle touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this triangle touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
    }
}
