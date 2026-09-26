using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a polygon touches another shape.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Checks whether this polygon touches an oriented box, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box) => Collision3.CollidesWith(this, box);

        /// <summary>
        /// Checks whether this polygon touches an oriented box, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoObb3 box, Tolerance tolerance) => Collision3.CollidesWith(this, box, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a solid, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Checks whether this polygon touches a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a segment, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line) => Collision3.CollidesWith(line, this);

        /// <summary>
        /// Checks whether this polygon touches a segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine3 line, Tolerance tolerance) => Collision3.CollidesWith(line, this, tolerance);

        /// <summary>
        /// Checks whether a ray runs into this polygon, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => Collision3.CollidesWith(ray, this);

        /// <summary>
        /// Checks whether a ray runs into this polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance) => Collision3.CollidesWith(ray, this, tolerance);
        /// <summary>
        /// Checks whether this polygon touches an arc, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc) => Arc3.CollidesWith(arc, this);

        /// <summary>
        /// Checks whether this polygon touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc3 arc, Tolerance tolerance) => Arc3.CollidesWith(arc, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches a circle, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle) => Arc3.CollidesWith(circle, this);

        /// <summary>
        /// Checks whether this polygon touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle3 circle, Tolerance tolerance) => Arc3.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this polygon touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge3 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this polygon touches an edge, within a tolerance.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool CollidesWith(GeoEdge3 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);

    }
}
