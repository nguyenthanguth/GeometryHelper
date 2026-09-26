using System;
using System.Linq;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a rectangle touches another shape.
    /// </summary>
    public readonly partial struct GeoRectangle2
    {
        /// <summary>
        /// Determines whether this rectangle touches or overlaps an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Collision2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Collision2.CollidesWith(this, loop);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => Collision2.CollidesWith(this, chain);

        /// <summary>
        /// Determines whether this rectangle touches or overlaps a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance) => Collision2.CollidesWith(this, chain, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with another rectangle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 other) => Collision2.CollidesWith(this, other, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with another rectangle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a line segment using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine) => Collision2.CollidesWith(this, geoLine, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a line segment within tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 geoLine, Tolerance tolerance) => Collision2.CollidesWith(this, geoLine, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a polygon using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => Collision2.CollidesWith(this, poly, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a polygon within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance) => Collision2.CollidesWith(this, poly, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a circle using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(circle, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a circle within tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(circle, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle collides with a polyline using default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(polyline, this, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle collides with a polyline within tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(polyline, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle reaches the material of a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => Face2.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this rectangle reaches the material of a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this rectangle touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this rectangle touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);
    }
}
