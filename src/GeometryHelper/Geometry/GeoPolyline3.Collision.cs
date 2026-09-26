using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a polyline touches another shape.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Determines whether this chain touches or overlaps a solid.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid) => Collision3.CollidesWith(this, solid);

        /// <summary>
        /// Determines whether this chain touches or overlaps a solid, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 solid, Tolerance tolerance) => Collision3.CollidesWith(this, solid, tolerance);

        /// <summary>
        /// Determines whether this chain touches a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc3 other) => Core.ArcChain3.CollidesWith(other, this);

        /// <summary>
        /// Determines whether this chain touches a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc3 other, Tolerance tolerance) => Core.ArcChain3.CollidesWith(other, this, tolerance);

        /// <summary>
        /// Determines whether this chain touches a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc3 other) => Core.ArcChain3.CollidesWith(other, this);

        /// <summary>
        /// Determines whether this chain touches a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc3 other, Tolerance tolerance) => Core.ArcChain3.CollidesWith(other, this, tolerance);
    }
}
