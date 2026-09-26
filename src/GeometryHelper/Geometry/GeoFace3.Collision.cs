using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a face touches another shape.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Checks whether this face touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this face touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this face touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this face touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
    }
}
