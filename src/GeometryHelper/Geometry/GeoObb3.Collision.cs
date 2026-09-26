using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an oriented box touches another shape.
    /// </summary>
    public sealed partial class GeoObb3
    {
        /// <summary>
        /// Checks whether this box overlaps another one, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 other) => Collision3.CollidesWith(this, other);

        /// <summary>
        /// Checks whether this box overlaps another one, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 other, Tolerance tolerance) => Collision3.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this box touches an axis-aligned box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this box touches an axis-aligned box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this box touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this box touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this box touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this box touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether this box touches a polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(polygon, this);

        /// <summary>
        /// Checks whether this box touches a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(polygon, this, tolerance);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
    }
}
