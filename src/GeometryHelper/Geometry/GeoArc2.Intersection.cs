using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where an arc crosses another shape.
    /// </summary>
    public readonly partial struct GeoArc2
    {
        /// <summary>
        /// Finds where this arc meets a segment.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections) => Arc2.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Finds where this arc meets a segment, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Finds where this arc meets a circle.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => Arc2.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Finds where this arc meets a circle, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Finds where this arc meets another.
        /// </summary>
        public bool TryIntersectWith(GeoArc2 other, out GeoPoint2[] intersections) => Arc2.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Finds where this arc meets another, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoArc2 other, out GeoPoint2[] intersections, Tolerance tolerance) => Arc2.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets where this arc meets a segment.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Arc2.GetIntersections(this, line);

        /// <summary>
        /// Gets every point where this arc crosses a segment, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Arc2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets where this arc meets a circle.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Arc2.GetIntersections(this, circle);

        /// <summary>
        /// Gets every point where this arc crosses a circle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Arc2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets where this arc meets another.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 other) => Arc2.GetIntersections(this, other);

        /// <summary>
        /// Gets every point where this arc crosses a arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 other, Tolerance tolerance) => Arc2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this arc crosses the boundary of a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a polygon, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly) => Intersection2.GetIntersections(poly, this);

        /// <summary>
        /// Gets every point where this arc crosses a polygon, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 poly, Tolerance tolerance) => Intersection2.GetIntersections(poly, this, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a curved loop, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(loop, this);

        /// <summary>
        /// Gets every point where this arc crosses a curved loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(loop, this, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a polyline, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(polyline, this);

        /// <summary>
        /// Gets every point where this arc crosses a polyline, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(polyline, this, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a curved chain, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain) => Intersection2.GetIntersections(chain, this);

        /// <summary>
        /// Gets every point where this arc crosses a curved chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, Tolerance tolerance) => Intersection2.GetIntersections(chain, this, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses a rectangle, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this);

        /// <summary>
        /// Gets every point where this arc crosses a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this arc crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(chain, this, out intersections);

        /// <summary>
        /// Tries to find where this arc crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="chain">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(chain, this, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this arc crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this arc crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where this arc crosses an edge.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this arc crosses an edge, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
            => edge.IsArc
                ? TryIntersectWith(edge.ToArc(), out intersections, tolerance)
                : TryIntersectWith(edge.ToLine(), out intersections, tolerance);
    }
}
