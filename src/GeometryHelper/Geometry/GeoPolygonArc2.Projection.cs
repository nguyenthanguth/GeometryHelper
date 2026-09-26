using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a curved loop nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolygonArc2
    {
        /// <summary>
        /// Gets the point of the loop nearest a point.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Projection2.ProjectToPolygonArc(this, point);

        /// <summary>
        /// Gets the point of the loop nearest a point, within a tolerance.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point, Tolerance tolerance) => Projection2.ProjectToPolygonArc(this, point, tolerance);

        /// <summary>
        /// Gets the edge of this loop nearest a point.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPoint2 point) => ClosestEdge2.GetClosestEdge(this, point);

        /// <summary>
        /// Gets the edge of this loop nearest a point, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoPoint2 point, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(this, point, tolerance);

        /// <summary>
        /// Gets the edge of this loop nearest a segment.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoLine2 line) => ClosestEdge2.GetClosestEdge(this, line);

        /// <summary>
        /// Gets the edge of this loop nearest a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer is a <see cref="GeoEdge2"/> rather than a segment, because the nearest piece
        /// of this loop may be an arc.
        /// </remarks>
        public GeoEdge2 GetClosestEdge(GeoLine2 line, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(this, line, tolerance);

        /// <summary>
        /// Gets the edge of this loop nearest a circle.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoCircle2 circle) => ClosestEdge2.GetClosestEdge(this, circle);

        /// <summary>
        /// Gets the edge of this loop nearest a circle, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoCircle2 circle, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(this, circle, tolerance);

        /// <summary>
        /// Gets the edge of this loop nearest an arc.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoArc2 arc) => ClosestEdge2.GetClosestEdge(this, arc);

        /// <summary>
        /// Gets the edge of this loop nearest an arc, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoArc2 arc, Tolerance tolerance) => ClosestEdge2.GetClosestEdge(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a segment.
        /// </summary>
        /// <remarks>
        /// Both ends sit on a boundary, so a shape lying wholly inside this one still reports the gap out
        /// to the outline rather than nothing at all, where <see cref="DistanceTo(GeoPolygon2)"/> reads a
        /// closed shape as a filled region and answers nothing. The answer is a segment joining the two
        /// shapes; for a piece of this one, see <see cref="GetClosestEdge(GeoLine2)"/>.
        /// </remarks>
        public GeoLine2 GetShortestLineTo(GeoLine2 line) => Projection2.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a segment, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoLine2 line, Tolerance tolerance) => Projection2.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to an arc.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc) => Projection2.GetShortestLineTo(this, arc);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to an arc, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoArc2 arc, Tolerance tolerance) => Projection2.GetShortestLineTo(this, arc, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a circle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle) => Projection2.GetShortestLineTo(this, circle);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a circle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoCircle2 circle, Tolerance tolerance) => Projection2.GetShortestLineTo(this, circle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a polygon.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon) => Projection2.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a polygon, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygon2 polygon, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a polyline.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline) => Projection2.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a polyline, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolyline2 polyline, Tolerance tolerance) => Projection2.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to another curved loop.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 other) => Projection2.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to another curved loop, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolygonArc2 other, Tolerance tolerance) => Projection2.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a curved chain.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain) => Projection2.GetShortestLineTo(this, chain);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a curved chain, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoPolylineArc2 chain, Tolerance tolerance) => Projection2.GetShortestLineTo(this, chain, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a rectangle.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect) => Projection2.GetShortestLineTo(rect, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this curved loop to a rectangle, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoRectangle2 rect, Tolerance tolerance) => Projection2.GetShortestLineTo(rect, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this curved loop and landing on the boundary of a face, using the default tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face) => Face2.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment leaving this curved loop and landing on the boundary of a face, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoFace2 face, Tolerance tolerance) => Face2.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the edge of this curved loop nearest an edge.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoEdge2 edge) => GetClosestEdge(edge, Tolerance.Global);

        /// <summary>
        /// Gets the edge of this curved loop nearest an edge, within a tolerance.
        /// </summary>
        public GeoEdge2 GetClosestEdge(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetClosestEdge(edge.ToArc(), tolerance)
                : GetClosestEdge(edge.ToLine(), tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this curved loop and landing on an edge.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge) => GetShortestLineTo(edge, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this curved loop and landing on an edge, within a tolerance.
        /// </summary>
        public GeoLine2 GetShortestLineTo(GeoEdge2 edge, Tolerance tolerance)
            => edge.IsArc
                ? GetShortestLineTo(edge.ToArc(), tolerance)
                : GetShortestLineTo(edge.ToLine(), tolerance);
    }
}
