using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether a curved chain touches another shape.
    /// </summary>
    public sealed partial class GeoPolylineArc2
    {
        /// <summary>
        /// Determines whether the chain touches or overlaps a straight segment.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => Collision2.CollidesWith(this, line);

        /// <summary>
        /// Determines whether the chain touches or overlaps a straight segment, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance) => Collision2.CollidesWith(this, line, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => Collision2.CollidesWith(this, arc);

        /// <summary>
        /// Determines whether the chain touches or overlaps an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance) => Collision2.CollidesWith(this, arc, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps a circle.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => Collision2.CollidesWith(this, circle);

        /// <summary>
        /// Determines whether the chain touches or overlaps a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance) => Collision2.CollidesWith(this, circle, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps a straight chain.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => Collision2.CollidesWith(this, polyline);

        /// <summary>
        /// Determines whether the chain touches or overlaps a straight chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance) => Collision2.CollidesWith(this, polyline, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps a straight loop.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon) => Collision2.CollidesWith(this, polygon);

        /// <summary>
        /// Determines whether the chain touches or overlaps a straight loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 polygon, Tolerance tolerance) => Collision2.CollidesWith(this, polygon, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps a chain that may curve.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 other) => Collision2.CollidesWith(this, other);

        /// <summary>
        /// Determines whether the chain touches or overlaps a chain that may curve, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 other, Tolerance tolerance) => Collision2.CollidesWith(this, other, tolerance);

        /// <summary>
        /// Determines whether the chain touches or overlaps a loop that may curve.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => Collision2.CollidesWith(this, loop);

        /// <summary>
        /// Determines whether the chain touches or overlaps a loop that may curve, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance) => Collision2.CollidesWith(this, loop, tolerance);

        /// <summary>
        /// Determines whether this curved chain touches or overlaps a rectangle.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => Collision2.CollidesWith(rect, this);

        /// <summary>
        /// Determines whether this curved chain touches or overlaps a rectangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance) => Collision2.CollidesWith(rect, this, tolerance);

        /// <summary>
        /// Checks whether this curved chain reaches the material of a face, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => Face2.CollidesWith(face, this);

        /// <summary>
        /// Checks whether this curved chain reaches the material of a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance) => Face2.CollidesWith(face, this, tolerance);

        /// <summary>
        /// Checks whether this curved chain touches an edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge) => CollidesWith(edge, Tolerance.Global);

        /// <summary>
        /// Checks whether this curved chain touches an edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? CollidesWith(edge.ToArc(), tolerance)
                : CollidesWith(edge.ToLine(), tolerance);
    }
}
