using System;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an edge crosses another shape.
    /// </summary>
    public readonly partial struct GeoEdge2
    {
        /// <summary>
        /// Gets the points where the edge meets a straight segment.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => GetIntersections(line, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edge meets a straight segment, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance)
        {
            if (IsArc)
            {
                return Core.Arc2.GetIntersections(ToArc(), line, tolerance);
            }

            GeoPoint2? meeting = Core.Intersection2.GetIntersection(ToLine(), line, tolerance);

            return meeting.HasValue ? new[] { meeting.Value } : new GeoPoint2[0];
        }

        /// <summary>
        /// Gets the points where the edge meets another edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 other) => GetIntersections(other, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edge meets another edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 other, Tolerance tolerance)
        {
            if (IsArc)
            {
                return other.IsArc
                    ? Core.Arc2.GetIntersections(ToArc(), other.ToArc(), tolerance)
                    : Core.Arc2.GetIntersections(ToArc(), other.ToLine(), tolerance);
            }

            return other.IsArc
                ? Core.Arc2.GetIntersections(other.ToArc(), ToLine(), tolerance)
                : GetIntersections(other.ToLine(), tolerance);
        }

        /// <summary>
        /// Gets the points where the edge meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => GetIntersections(arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edge meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance)
        {
            return IsArc
                ? Core.Arc2.GetIntersections(ToArc(), arc, tolerance)
                : Core.Arc2.GetIntersections(arc, ToLine(), tolerance);
        }

        /// <summary>
        /// Gets every point where this edge crosses a circle.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => GetIntersections(circle, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(circle, tolerance) : ToLine().GetIntersections(circle, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a face.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => GetIntersections(face, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(face, tolerance) : ToLine().GetIntersections(face, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a polygon.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => GetIntersections(poly, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a polygon, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(poly, tolerance) : ToLine().GetIntersections(poly, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a curved loop.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => GetIntersections(loop, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(loop, tolerance) : ToLine().GetIntersections(loop, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a polyline.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => GetIntersections(polyline, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a polyline, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(polyline, tolerance) : ToLine().GetIntersections(polyline, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a curved chain.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => GetIntersections(chain, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(chain, tolerance) : ToLine().GetIntersections(chain, tolerance);

        /// <summary>
        /// Gets every point where this edge crosses a rectangle.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => GetIntersections(rect, Tolerance.Global);

        /// <summary>
        /// Gets every point where this edge crosses a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance)
            => IsArc ? ToArc().GetIntersections(rect, tolerance) : ToLine().GetIntersections(rect, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses an arc.
        /// </summary>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses an arc, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(arc, out intersections, tolerance) : ToLine().TryIntersectWith(arc, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a circle.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a circle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(circle, out intersections, tolerance) : ToLine().TryIntersectWith(circle, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a curved loop.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a curved loop, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(loop, out intersections, tolerance) : ToLine().TryIntersectWith(loop, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this edge crosses a curved chain.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this edge crosses a curved chain, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
            => IsArc ? ToArc().TryIntersectWith(chain, out intersections, tolerance) : ToLine().TryIntersectWith(chain, out intersections, tolerance);
    }
}
