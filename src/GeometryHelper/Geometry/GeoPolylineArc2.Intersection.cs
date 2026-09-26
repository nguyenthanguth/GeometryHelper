using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Where a curved chain crosses another shape.
    /// </summary>
    public sealed partial class GeoPolylineArc2
    {
        /// <summary>
        /// Gets the points where the chain meets a straight segment.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line) => Intersection2.GetIntersections(this, line);

        /// <summary>
        /// Gets the points where the chain meets a straight segment, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoLine2 line, Tolerance tolerance) => Intersection2.GetIntersections(this, line, tolerance);

        /// <summary>
        /// Gets the points where the chain meets an arc.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc) => Intersection2.GetIntersections(this, arc);

        /// <summary>
        /// Gets the points where the chain meets an arc, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoArc2 arc, Tolerance tolerance) => Intersection2.GetIntersections(this, arc, tolerance);

        /// <summary>
        /// Gets the points where the chain meets a circle.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle) => Intersection2.GetIntersections(this, circle);

        /// <summary>
        /// Gets the points where the chain meets a circle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoCircle2 circle, Tolerance tolerance) => Intersection2.GetIntersections(this, circle, tolerance);

        /// <summary>
        /// Gets the points where the chain meets a straight chain.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline) => Intersection2.GetIntersections(this, polyline);

        /// <summary>
        /// Gets the points where the chain meets a straight chain, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolyline2 polyline, Tolerance tolerance) => Intersection2.GetIntersections(this, polyline, tolerance);

        /// <summary>
        /// Gets the points where the chain meets a straight loop.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon) => Intersection2.GetIntersections(this, polygon);

        /// <summary>
        /// Gets the points where the chain meets a straight loop, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygon2 polygon, Tolerance tolerance) => Intersection2.GetIntersections(this, polygon, tolerance);

        /// <summary>
        /// Gets the points where the chain meets a chain that may curve.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 other) => Intersection2.GetIntersections(this, other);

        /// <summary>
        /// Gets the points where the chain meets a chain that may curve, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolylineArc2 other, Tolerance tolerance) => Intersection2.GetIntersections(this, other, tolerance);

        /// <summary>
        /// Gets the points where the chain meets a loop that may curve.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop) => Intersection2.GetIntersections(this, loop);

        /// <summary>
        /// Gets the points where the chain meets a loop that may curve, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, Tolerance tolerance) => Intersection2.GetIntersections(this, loop, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face) => Face2.GetIntersections(face, this);

        /// <summary>
        /// Gets every point where this curved chain crosses the boundary of a face, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoFace2 face, Tolerance tolerance) => Face2.GetIntersections(face, this, tolerance);

        /// <summary>
        /// Gets the points where this curved chain meets a rectangle.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect) => Intersection2.GetIntersections(rect, this);

        /// <summary>
        /// Gets the points where this curved chain meets a rectangle, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoRectangle2 rect, Tolerance tolerance) => Intersection2.GetIntersections(rect, this, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a curved loop, using the default tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(loop, this, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a curved loop, within a tolerance.
        /// </summary>
        /// <param name="loop">The curved loop.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(loop, this, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a arc, using the default tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, arc, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a arc, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, arc, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a circle, using the default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, circle, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a circle, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, circle, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a segment, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, line, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a segment, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, line, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a polygon, using the default tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, polygon, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a polygon, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, polygon, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a polyline, using the default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, polyline, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a polyline, within a tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, polyline, out intersections, tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses a curved chain, using the default tolerance.
        /// </summary>
        /// <param name="other">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoPolylineArc2 other, out GeoPoint2[] intersections) => Intersection2.TryIntersectWith(this, other, out intersections);

        /// <summary>
        /// Tries to find where this curved chain crosses a curved chain, within a tolerance.
        /// </summary>
        /// <param name="other">The curved chain.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoPolylineArc2 other, out GeoPoint2[] intersections, Tolerance tolerance) => Intersection2.TryIntersectWith(this, other, out intersections, tolerance);

        /// <summary>
        /// Gets every point where this curved chain crosses an edge.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge) => GetIntersections(edge, Tolerance.Global);

        /// <summary>
        /// Gets every point where this curved chain crosses an edge, within a tolerance.
        /// </summary>
        public GeoPoint2[] GetIntersections(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetIntersections(edge.ToArc(), tolerance)
                : GetIntersections(edge.ToLine(), tolerance);

        /// <summary>
        /// Tries to find where this curved chain crosses an edge.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections) => TryIntersectWith(edge, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this curved chain crosses an edge, within a tolerance.
        /// </summary>
        public bool TryIntersectWith(GeoEdge2 edge, out GeoPoint2[] intersections, Tolerance tolerance)
            => edge.IsArc
                ? TryIntersectWith(edge.ToArc(), out intersections, tolerance)
                : TryIntersectWith(edge.ToLine(), out intersections, tolerance);
    }
}
