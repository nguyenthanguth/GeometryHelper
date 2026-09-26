using System;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Whether an edge touches another shape.
    /// </summary>
    public readonly partial struct GeoEdge2
    {
        /// <summary>
        /// Checks whether the edge touches a segment.
        /// </summary>
        public bool CollidesWith(GeoLine2 line) => CollidesWith(line, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An edge is a segment or an arc and nothing else, so every one of these reads it as whichever it
        /// is and asks that shape. What counts as touching is then whatever it counts as for a segment or
        /// an arc: neither has an inside, so against another open shape only a crossing counts, while a
        /// closed one swallowing the edge whole counts as well.
        /// </remarks>
        public bool CollidesWith(GeoLine2 line, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(line, tolerance) : ToLine().CollidesWith(line, tolerance);

        /// <summary>
        /// Checks whether the edge touches an arc.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc) => CollidesWith(arc, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches an arc, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoArc2 arc, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(arc, tolerance) : ToLine().CollidesWith(arc, tolerance);

        /// <summary>
        /// Checks whether the edge touches another edge.
        /// </summary>
        public bool CollidesWith(GeoEdge2 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches another edge, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoEdge2 other, Tolerance tolerance)
            => other.IsArc ? CollidesWith(other.ToArc(), tolerance) : CollidesWith(other.ToLine(), tolerance);

        /// <summary>
        /// Checks whether the edge touches a circle.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle) => CollidesWith(circle, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a circle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoCircle2 circle, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(circle, tolerance) : ToLine().CollidesWith(circle, tolerance);

        /// <summary>
        /// Checks whether the edge touches a rectangle.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect) => CollidesWith(rect, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a rectangle, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRectangle2 rect, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(rect, tolerance) : ToLine().CollidesWith(rect, tolerance);

        /// <summary>
        /// Checks whether the edge touches a polygon.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly) => CollidesWith(poly, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a polygon, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygon2 poly, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(poly, tolerance) : ToLine().CollidesWith(poly, tolerance);

        /// <summary>
        /// Checks whether the edge touches a polyline.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline) => CollidesWith(polyline, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a polyline, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolyline2 polyline, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(polyline, tolerance) : ToLine().CollidesWith(polyline, tolerance);

        /// <summary>
        /// Checks whether the edge touches a curved loop.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop) => CollidesWith(loop, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a curved loop, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolygonArc2 loop, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(loop, tolerance) : ToLine().CollidesWith(loop, tolerance);

        /// <summary>
        /// Checks whether the edge touches a curved chain.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain) => CollidesWith(chain, Tolerance.Global);

        /// <summary>
        /// Checks whether the edge touches a curved chain, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoPolylineArc2 chain, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(chain, tolerance) : ToLine().CollidesWith(chain, tolerance);

        /// <summary>
        /// Checks whether this edge touches a face.
        /// </summary>
        public bool CollidesWith(GeoFace2 face) => CollidesWith(face, Tolerance.Global);

        /// <summary>
        /// Checks whether this edge touches a face, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoFace2 face, Tolerance tolerance)
            => IsArc ? ToArc().CollidesWith(face, tolerance) : ToLine().CollidesWith(face, tolerance);
    }
}
