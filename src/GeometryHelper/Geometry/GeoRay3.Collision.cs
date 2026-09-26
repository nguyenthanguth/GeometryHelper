using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a ray touches another shape.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Checks whether this ray touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this ray touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a triangle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle) => Collision3.CollidesWith(this, triangle);

        /// <summary>
        /// Checks whether this ray runs into a triangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoTriangle3 triangle, Tolerance tolerance) => Collision3.CollidesWith(this, triangle, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon) => Collision3.CollidesWith(this, polygon);

        /// <summary>
        /// Checks whether this ray runs into a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon3 polygon, Tolerance tolerance) => Collision3.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Checks whether this ray runs into a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face) => Collision3.CollidesWith(this, face);

        /// <summary>
        /// Checks whether this ray runs into a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace3 face, Tolerance tolerance) => Collision3.CollidesWith(this, face, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside an axis-aligned box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this ray starts inside an axis-aligned box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoAabb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this ray starts inside an oriented box or runs into it, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this ray starts inside an oriented box or runs into it, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);
        /// <summary>
        /// Checks whether this ray touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this ray touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this ray touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this ray touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

    }
}
