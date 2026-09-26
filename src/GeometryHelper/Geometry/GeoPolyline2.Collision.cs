using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a polyline touches another shape.
    /// </summary>
    public sealed partial class GeoPolyline2
    {
        /// <summary>
        /// Determines whether this polyline touches or overlaps an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Collision2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this polyline touches or overlaps an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this polyline touches or overlaps a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Collision2.CollidesWith(loop, this);

        /// <summary>
        /// Determines whether this polyline touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(loop, this, tolerance);

        /// <summary>
        /// Determines whether this polyline touches or overlaps a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => Collision2.CollidesWith(chain, this);

        /// <summary>
        /// Determines whether this polyline touches or overlaps a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Collision2.CollidesWith(chain, this, tolerance);

        /// <summary>
        /// Checks whether this polyline collides with a line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Collision2.CollidesWith(this, line, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline collides with a line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Collision2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Checks whether this polyline collides with a rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(this, rect, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline collides with a rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(this, rect, tolerance);

        /// <summary>
        /// Checks whether this polyline collides with a circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline collides with a circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this polyline collides with a polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(this, poly, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline collides with a polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(this, poly, tolerance);

        /// <summary>
        /// Checks whether this polyline collides with another polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline collides with another polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this polyline reaches the material of a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => Face2.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this polyline reaches the material of a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this polyline touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this polyline touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);
    }
}
