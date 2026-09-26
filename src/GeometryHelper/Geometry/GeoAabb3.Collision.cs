using GeometryHelper.Core;
using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an axis-aligned box touches another shape.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Checks whether this box overlaps another one, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this box overlaps another one, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 other, Tolerance tolerance)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return false;
            }

            double t = tolerance.EqualPoint;

            return Min.X - t <= other.Max.X && Max.X + t >= other.Min.X &&
                   Min.Y - t <= other.Max.Y && Max.Y + t >= other.Min.Y &&
                   Min.Z - t <= other.Max.Z && Max.Z + t >= other.Min.Z;
        }

        /// <summary>
        /// Checks whether this box touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Core.Collision3.CollidesWith(box, this);

        /// <summary>
        /// Checks whether this box touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Core.Collision3.CollidesWith(box, this, tolerance);

        /// <summary>
        /// Checks whether this box touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Core.Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this box touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Core.Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Core.Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray starts inside this box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Core.Collision3.CollidesWith(ray, this, tolerance);

        /// <summary>
        /// Checks whether this box touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Core.Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this box touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Core.Collision3.CollidesWith(this, solid, tolerance);
        /// <summary>
        /// Checks whether this axis-aligned box touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this axis-aligned box touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this axis-aligned box touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

    }
}
